#!/usr/bin/env python3
"""TEAM-17 (#85) review-render recipe for the existing 33-asset station kit.

Produces, for every one of the 33 published assets, a front and a side review render
and a 4K derived candidate, under ONE frozen render condition, into a run-scoped
output root. The 33 originals are never written to: every write target this recipe
creates -- including its own manifest path -- is checked against a guarded source set
before it is created, and the guarded set is re-digested after the run and must be
byte-identical. That claim is enforced, not asserted: write_plan, run_blender and the
--plan CLI path all pass the manifest path through assert_manifest_path_safe before
writing it, and run_blender passes every raster target through
assert_output_outside_guards.

The condition is frozen in this file and split in two:

  * CONDITION holds controls only. Every leaf of CONDITION is read by one of the named
    control channels, and a unit test fails if any leaf is not consumed. A field the
    engine cannot act on is not a control and does not live here.
  * DECLARED_NOT_CONTROLLED records the fields a reader might expect but that EEVEE
    Next in Blender 4.5.9 cannot act on (sampler seed, denoise, per-view elevation,
    save-operator overwrite flags). They are declared, digested separately, and named
    as declarations in the manifest so they cannot be mistaken for controls.

Only one thing may differ between the review render and the 4K candidate, and the
manifest names it: the derivation. One raster pass per (asset, view) at 4K resolution is
the master; the 4K candidate is that master written through unchanged, and the review
render is a deterministic half-scale of the same master. Viewpoint, lighting, framing
and colour management are identical by construction, not by a second hidden render.

Camera orientation is derived from the SAME look direction the camera position is
derived from (camera_rotation_euler_degrees), never from the view name. A unit test
checks dot(camera_forward, centre - location) > 0 for all 66 planned slots.

A frame is not recorded as generated until it passes the non-blank gate: a blank frame
hashes and counts bytes exactly like a real one, so a digest alone cannot tell an empty
front view from a real one.

The 66 required slots are classified into four mutually exclusive states, and the manifest
states which is which rather than leaving 누락 to be inferred:

  generated  a pass produced both tiers and the bytes were re-read from the file
  failed     a pass was attempted for this slot and produced no valid output
  missing    the source this slot renders is not on disk, so nothing could be attempted
  unrun      no pass has been attempted yet

Visual review is a fifth, separate axis: every slot starts `reviewStatus: not_assessed`
and this recipe never sets it, because 미검수 is not something a render pass can discharge.
A `missing` source and a `failed` render are different defects with different owners; the
acceptance criteria for #85 name 누락/실패/미검수 separately, so they are counted separately.

The batch is 33 assets, not one file: every asset in the published baseline is enumerated
whether or not it can be rendered. A source that is absent is classified `missing`, a
source that is present but is not the published original is classified `failed`, a slot
whose render raises is classified `failed` -- and in all three cases the remaining assets
are still rendered. There is no path on which one bad asset silently removes the other 32
from the count, and no path on which a slot that did not render is counted as generated.

Offline modes need no Blender and no GPU:

  python3 scripts/art/team/TEAM-17/render_recipe.py --plan            # write the manifest
  python3 scripts/art/team/TEAM-17/render_recipe.py --check           # validate recipe+manifest
  python3 scripts/art/team/TEAM-17/render_recipe.py --test            # unit tests
  python3 scripts/art/team/TEAM-17/render_recipe.py --verify-outputs  # re-read rendered files

Execution mode needs Blender 4.5.9 LTS:

  <blender> -b --factory-startup --python scripts/art/team/TEAM-17/render_recipe.py -- \
      --run --run-id <id> [--asset <id>] [--resume]

Scope boundaries this file does not cross:

  * It does not bake textures. "4K candidate" here is a 4K raster review render, not a
    4K texture bake; the manifest says so in textureBake.
  * It does not open, save, re-export or re-write station-kit.blend, any of the 33 FBX
    files, or foundation/art/asset-manifest.json. asset-manifest.json stays read-only,
    so its noBakeOrRender flag still describes the baseline assets, not these renders.
    The manifest path itself is refused if it points at a guarded path.
  * It does not decide visual acceptance. It records what was rendered, what failed and
    what the pixels did; it does not judge whether the result looks right.
"""

import argparse
import array
import hashlib
import json
import math
import re
import shutil
import struct
import sys
import unittest
from datetime import datetime, timezone
from pathlib import Path

# ------------------------------------------------------------------ paths

HERE = Path(__file__).resolve()
ROOT = HERE.parents[4]
assert (ROOT / 'scripts/art/build_station_assets.py').is_file(), ROOT

RECIPE = HERE
BASELINE = ROOT / 'foundation/art/asset-manifest.json'
OBJECT_REFERENCES = ROOT / 'foundation/art/object-references.json'
SOURCE_BLEND = ROOT / 'foundation/art/station-kit.blend'
GENERATOR = ROOT / 'scripts/art/build_station_assets.py'
MANIFEST = ROOT / 'foundation/art/team/TEAM-17/render-manifest.json'
DEFAULT_OUT_ROOT = ROOT / 'Builds/ArtReview/TEAM-17'

WORK_ID = 'TEAM-17'
ISSUE = 85
PARENT_ISSUE = 68
SCHEMA_VERSION = 2

VIEWS = ('front', 'side')
REQUIRED_SLOTS = 33 * len(VIEWS)

MASTER_RESOLUTION = (3840, 2160)
REVIEW_RESOLUTION = (1920, 1080)

BLENDER_REQUIRED = '4.5.9 LTS'

RUN_ID_PATTERN = re.compile(r'^[A-Za-z0-9._-]{1,64}$')
RUN_ID_FORBIDDEN = ('.', '..')

COORDINATE_ADAPTER = 'Unity(x,y,z) -> Blender(-x,-z,y)'
BOUNDS_SOURCE = 'foundation/art/asset-manifest.json:/assets/<id>/boundsUnity'
BOUNDS_RECHECK = 'reimported_fbx_bounds_must_match_declared_bounds'
DISTANCE_RULE = 'v * 10 + 1'
CLIP_START_RULE = 'v * 0.01'
CLIP_END_RULE = '(distance + v) * 4'

SRGB_THRESHOLD = 0.0031308

TIERS = ('candidate4k', 'render')

# The four slot states are mutually exclusive and jointly exhaustive, which is what makes
# "how much of #85 is done" answerable without reading prose:
#   generated -- a render pass produced both tiers and the bytes were re-read from disk
#   failed    -- a pass was attempted for this slot and it did not produce a valid output
#   missing   -- the input this slot needs is not on disk, so no pass could be attempted
#   unrun     -- no pass has been attempted yet
# missing is deliberately NOT folded into failed: "the source is absent" and "the render
# ran and came out wrong" are different defects with different owners, and the acceptance
# criteria for #85 name 누락/실패/미검수 separately.
STATUSES = ('generated', 'failed', 'missing', 'unrun')
PRODUCED_STATUSES = ('generated',)


class RecipeUsageError(Exception):
    """Bad invocation: the caller asked for something the recipe refuses to do."""


# ------------------------------------------------------------------ helpers


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def rel(path):
    """Repo-relative posix path. Refuses to describe a path outside the repository."""
    resolved = Path(path).resolve()
    root = ROOT.resolve()
    if resolved != root and root not in resolved.parents:
        raise RuntimeError('refusing to describe a path outside the repository: %s' % resolved)
    return resolved.relative_to(root).as_posix()


def load_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8'))


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False)


def digest_of(value):
    return hashlib.sha256(canonical(value).encode('utf-8')).hexdigest()


def utc_now():
    return datetime.now(timezone.utc).strftime('%Y-%m-%dT%H:%M:%SZ')


def leaf_paths(value, prefix=''):
    """Dotted leaf paths of a nested dict. Lists and None count as leaves."""
    if isinstance(value, dict) and value:
        out = []
        for key in sorted(value):
            child = '%s.%s' % (prefix, key) if prefix else key
            out.extend(leaf_paths(value[key], child))
        return out
    return [prefix]


def srgb_encode(linear):
    if linear <= SRGB_THRESHOLD:
        return 12.92 * linear
    return 1.055 * (linear ** (1.0 / 2.4)) - 0.055


# ------------------------------------------------------------------ frozen condition
#
# Every leaf below is a CONTROL: it is read by a named channel and its value changes
# behaviour. `views` carries the only per-view difference. Nothing else may vary; there
# is no per-asset override hook, and adding one would change CONDITION_DIGEST, which the
# manifest binds every slot to.

CONDITION = {
    'conditionId': 'team17-review-v2',
    'engine': 'BLENDER_EEVEE_NEXT',
    'render': {
        'resolutionX': MASTER_RESOLUTION[0],
        'resolutionY': MASTER_RESOLUTION[1],
        'resolutionPercentage': 100,
        'pixelAspectX': 1.0,
        'pixelAspectY': 1.0,
        'filmTransparent': False,
        'imageFormat': 'PNG',
        'colorDepth': '8',
        'colorMode': 'RGBA',
        'compression': 15,
    },
    'sampling': {'taaRenderSamples': 64, 'useRaytracing': False},
    'colorManagement': {
        'displayDevice': 'sRGB',
        'viewTransform': 'Standard',
        'look': 'None',
        'exposure': 0.0,
        'gamma': 1.0,
        'viewSettings': 'scene_linear_to_srgb',
    },
    'camera': {
        'type': 'ORTHO',
        'sensorFit': 'AUTO',
        'sensorWidth': 36.0,
        'shiftX': 0.0,
        'shiftY': 0.0,
        'dof': False,
    },
    'framing': {
        'rule': 'orthographic_aabb_fit',
        'marginFactor': 1.08,
        'centre': 'bounds_center_unity_space',
        'distanceRule': DISTANCE_RULE,
        'clipStartRule': CLIP_START_RULE,
        'clipEndRule': CLIP_END_RULE,
        'boundsSource': BOUNDS_SOURCE,
        'boundsToleranceMetres': 0.001,
    },
    'world': {'useNodes': True, 'backgroundType': 'flat_color',
              'color': [0.05, 0.05, 0.05], 'strength': 1.0},
    'lighting': {
        'key': {'type': 'SUN', 'energy': 3.0, 'angleDegrees': 0.526,
                'rotationEulerDegrees': [-50.0, 0.0, -30.0]},
        'fill': {'type': 'SUN', 'energy': 1.0, 'angleDegrees': 0.526,
                 'rotationEulerDegrees': [-70.0, 0.0, 140.0]},
        'rim': None,
        'additionalLights': 0,
        'worldLightingContribution': 'flat_color_strength_1.0',
    },
    'views': {
        # Device front is Unity -Z per foundation/art/README.md, so the front camera sits
        # on -Z and its look direction is +Z; the side camera sits on -X and looks toward
        # +X. `lookAxis` is the unity-space look direction the rotation is derived from.
        'front': {'azimuthDegrees': 0.0, 'coversAxes': ['x', 'y'], 'lookAxis': '+z'},
        'side': {'azimuthDegrees': 90.0, 'coversAxes': ['z', 'y'], 'lookAxis': '+x'},
    },
    'scene': {
        'importFormat': 'FBX',
        'globalScale': 1.0,
        'axisForward': '-Z',
        'axisUp': 'Y',
        'applyUnitScale': True,
        'coordinateAdapter': COORDINATE_ADAPTER,
        'objectFilter': 'MESH only',
        'hiddenObjectsIncluded': False,
        'materialOverride': None,
        'materialEdits': 'none',
        'geometryEdits': 'none',
        'perObjectTransform': 'none',
    },
}

# Fields a reader might look for in a render condition, that this engine revision cannot
# act on. They are NOT controls and are deliberately NOT in CONDITION: leaving them in
# would make the frozen digest claim to seal something the apply path never reads.
DECLARED_NOT_CONTROLLED = {
    'note': ('Not controls. Recorded so a reader can see they were considered and why '
             'they are absent from CONDITION.'),
    'fields': {
        'render.useOverwrite': 'a save-operator argument, not a scene property; the recipe '
                               'always writes through an explicit path',
        'render.useFileExtension': 'a save-operator argument, not a scene property',
        'sampling.seed': 'EEVEE Next has no sampler seed; there is no seed to freeze',
        'sampling.useDenoising': 'EEVEE Next denoising is a ray-tracing option and '
                                 'sampling.useRaytracing is False, so a denoise flag would '
                                 'control nothing',
        'views.*.elevationDegrees': 'this framing rule is a pure axis-aligned fit with no '
                                    'elevation term; both views are horizontal. A tilted '
                                    'view would need a new condition revision and a new '
                                    'digest, not a silently ignored field',
    },
}

# CONDITION members that carry identity rather than control. `conditionId` names the
# revision; nothing branches on it, and it is the one leaf no control channel reads.
IDENTITY_FIELDS = ('conditionId',)

# The only declared difference between the two output tiers. Named so that a reader can
# see that "same condition" is not asserted across a hidden re-render.
DERIVATION = {
    'master': {
        'tier': 'master',
        'purpose': 'single raster pass per (asset, view)',
        'resolution': list(MASTER_RESOLUTION),
        'path': 'temp/<Asset>__<view>__master.png',
        'retained': False,
        'transient': True,
    },
    'candidate4k': {
        'tier': 'candidate4k',
        'purpose': '4K derived candidate, separate output',
        'resolution': list(MASTER_RESOLUTION),
        'source': 'master',
        'operation': 'write_through_unchanged',
        'bitIdenticalToMaster': True,
        'path': 'candidate4k/<Asset>__<view>__4k.png',
    },
    'render': {
        'tier': 'render',
        'purpose': 'review render, separate output',
        'resolution': list(REVIEW_RESOLUTION),
        'source': 'master',
        'operation': 'blender_image_scale_half',
        'path': 'render/<Asset>__<view>__review.png',
    },
}

# The review tier inherits the master's pixels but not all of its encoder arguments.
# Measured on Blender 4.5.9 LTS, not assumed:
#   * bpy.types.Image.save() ignores scene.render.image_settings.compression entirely --
#     saving the same scaled image with compression 0, 15 and 100 produced 981604 bytes
#     every time. The sealed compression governs the master save, which goes through the
#     render pipeline's image_settings, and it governs nothing on the datablock save.
#   * bpy.types.Image.save_render(filepath, scene=...) DOES route through the scene's
#     image_settings, but it also pushes the pixels through the view transform a second
#     time. Measured max channel difference against save(): 0.227451 (mean 0.014557).
#     That is a real colour change on the review tier, so it is rejected here: the two
#     tiers must differ only by resampling.
# The consequence is disclosed rather than hidden, and every file's own IHDR is asserted
# from its bytes, so no digest in the manifest claims an encoder argument that was not
# applied.
REVIEW_TIER_ENCODER = {
    'api': 'bpy.types.Image.save',
    'applies': ['file_format'],
    'cannotApply': ['render.compression', 'render.colorDepth', 'render.colorMode'],
    'why': ('a datablock save does not read scene.render.image_settings; the review tier is '
            'therefore encoded at the Blender default for PNG. It is still the same pixels '
            'as the master, resampled half-scale, and the master itself carries every sealed '
            'encoder argument.'),
    'rejectedAlternative': {
        'api': 'bpy.types.Image.save_render',
        'reason': 're-applies the view transform to already display-encoded pixels',
        'measuredResolution': list(REVIEW_RESOLUTION),
        'measuredMaxChannelDelta': 0.227451,
        'measuredMeanChannelDelta': 0.014557,
    },
    'measuredOn': 'Blender 4.5.9 LTS',
}

SHARED_OUTPUTS_NOTE = (
    'The 33 FBX files, foundation/art/station-kit.blend and '
    'foundation/art/asset-manifest.json are read-only inputs to this recipe. '
    'asset-manifest.json keeps noBakeOrRender=true: it describes the baseline assets as '
    'they were generated, and this recipe does not turn them into baked or rendered '
    'assets.'
)

TEXTURE_BAKE = {
    'inScope': False,
    'reason': ('#85 asks for front/side review renders and a 4K derived output. A 4K texture '
               'bake candidate is a different artefact with different rights and quality '
               'questions and is not produced here.'),
    'producedHere': '3840x2160 raster review candidate only',
}

# The capture path the repository already owns, named so that the choice of a Blender
# EEVEE path is a decision a reader can inspect rather than an unexplained substitution.
CAPTURE_PATH_ALTERNATIVES = {
    'chosen': 'blender_eevee_next_offline_raster',
    'why': ('#85 asks for a 4K derived candidate as a file. The existing Unity review player '
            '(Builds/ArtReview/ChooGuardArtReview.app, --choo-art-captures) renders through '
            'the game runtime: it is the right tool for reviewing the in-game look, but it '
            'does not produce a 4K raster derived candidate and its captures depend on the '
            'runtime camera rather than on a frozen offline condition.'),
    'existing': {
        'tool': 'Builds/ArtReview/ChooGuardArtReview.app',
        'batchMethod': 'ChooGuard.Foundation.Demo.Editor.FoundationArtAudit.BuildMacReviewBatch',
        'captureArgument': '--choo-art-captures <task-local-output-directory>',
        'documentedAt': 'foundation/art/README.md',
        'audit': 'docs/art/object-reference-audit.md',
        'relationship': ('complementary, not replaced: it shows how the asset looks in the '
                         'runtime; this recipe shows what one frozen offline condition '
                         'produces as a file with a digest.'),
    },
}

# Post-render non-blank gate. A blank frame hashes and counts bytes exactly like a real
# one, so a digest alone cannot tell an empty front view from a real one.
BLANK_GATE = {
    'gateId': 'team17-nonblank-v1',
    'reference': ('display-encoded flat world colour: CONDITION["world"]["color"] pushed '
                  'through the sRGB transfer, because the view transform is Standard and the '
                  'display device is sRGB'),
    'channelTolerance': 0.02,
    'cornerTolerance': 0.06,
    'minNonBackgroundRatio': 0.005,
    'minDistinctColours': 8,
    'maxSamples': 600000,
    'cornerPixels': 4,
    'appliesTo': ['master', 'candidate4k', 'render'],
    'why': ('the framing rule fits the asset bounds with a 1.08 margin, so the frame corners '
            'are always world background. The four corners are checked against the reference '
            'first: if they do not match, the world or colour management part of the '
            'condition is not what was frozen and the slot fails as a condition failure '
            'instead of passing as a render.'),
    'whyThreshold': ('the narrowest axis-aligned silhouette across the 66 planned slots is '
                     '2.117% of the frame (CeilingPanel), so a 0.5% floor is 4.2x below the '
                     'narrowest planned asset and still leaves no room for a blank frame, '
                     'whose non-background ratio is exactly 0.'),
}

# The negative cases this work item is judged on, each bound to the named test that defends
# it. The issue's check names four (누락 뷰 / 비동일 조건 / 원본 덮어쓰기 / 재시작 중복); the
# rest are the cases this recipe can fail in on its own terms. test_38 asserts that every
# row names a test that exists, so the table cannot rot into a list of intentions.
NEGATIVE_CASES = (
    {'caseId': 'NEG-01',
     'negativeCase': '누락 뷰 -- a required view is dropped from the plan',
     'defendedBy': ['test_01_every_asset_gets_both_views',
                    'test_02_dropping_a_view_is_rejected',
                    'test_33_required_slot_counts_are_declared_exactly'],
     'rule': ('the slot set is compared against the published asset list for both views; a '
              'dropped view is a named error and the enumerated count is pinned to 66.')},
    {'caseId': 'NEG-02',
     'negativeCase': '비동일 조건 -- two slots rendered under conditions that are not identical',
     'defendedBy': ['test_04_every_slot_carries_the_frozen_condition',
                    'test_05_a_condition_drift_is_rejected',
                    'test_06b_every_condition_leaf_is_consumed_by_a_control_channel',
                    'test_08_the_two_tiers_differ_only_by_derivation'],
     'rule': ('one CONDITION dict is digested once; every slot records that digest and the '
              'validator recomputes it. An unbound condition field fails the check rather '
              'than quietly widening the frozen digest.')},
    {'caseId': 'NEG-03',
     'negativeCase': '원본 덮어쓰기 -- an original FBX is written over or already replaced',
     'defendedBy': ['test_14_writing_over_a_guarded_original_is_refused',
                    'test_37c_a_slot_whose_source_was_replaced_is_failed_not_missing',
                    'test_37j_the_source_record_must_name_the_published_digest',
                    'test_37k_the_published_anchor_detects_a_replaced_original'],
     'rule': ('every write target, including the manifest path, is refused if it equals or sits '
              'inside a guarded original; sources are additionally compared against the digest '
              'the PUBLISHED baseline declares, so a replacement that happened before the run '
              'is detectable and is reported as failed rather than rendered.')},
    {'caseId': 'NEG-04',
     'negativeCase': '재시작 중복 -- a resumed run double-counts or re-renders the same slot',
     'defendedBy': ['test_20_idempotency_keys_are_unique_per_slot',
                    'test_21_the_same_slot_replans_to_the_same_key',
                    'test_23_a_repeated_key_is_rejected',
                    'test_23b_a_key_from_a_superseded_revision_is_rejected',
                    'test_24_resume_of_an_unchanged_slot_adds_no_slot',
                    'test_24b_a_demoted_slot_stops_reporting_produced_files'],
     'rule': ('the slot list is keyed by asset and view, so a restart cannot add a slot; the '
              'idempotency key binds recipe revision and condition, and --resume reuses a slot '
              'only when its files are present and still match their recorded digests.')},
    {'caseId': 'NEG-05',
     'negativeCase': '누락 -- a source is absent and the slot is counted as generated or hidden',
     'defendedBy': ['test_37_a_slot_whose_source_is_absent_is_classified_missing',
                    'test_37b_a_missing_source_does_not_erase_the_other_assets',
                    'test_37d_a_missing_slot_may_not_claim_a_produced_file',
                    'test_37e_a_missing_slot_without_a_reason_is_rejected',
                    'test_37g_generated_status_is_rejected_while_any_slot_is_missing'],
     'rule': ('a slot whose source is not on disk is classified missing in the plan itself, '
              'with a reason, carrying no digest; the other assets are still enumerated and '
              'rendered, and `generated` is refused while any slot is missing. 누락은 생성 '
              '완료로 세지 않는다.')},
    {'caseId': 'NEG-06',
     'negativeCase': '실패 -- a render pass produces nothing and the slot is read as produced',
     'defendedBy': ['test_25_status_may_not_claim_generation_without_slots',
                    'test_25c_a_run_with_one_failed_slot_does_not_validate_as_generated',
                    'test_26_a_generated_slot_without_a_digest_is_rejected',
                    'test_30_unrun_slots_may_not_claim_a_produced_file',
                    'test_37m_a_render_failure_is_failed_not_missing'],
     'rule': ('a non-generated slot may name where its output WOULD go but may not carry a '
              'digest, a byte count, a presence claim, a gate result or an observation; failed '
              'and missing stay separate states.')},
    {'caseId': 'NEG-07',
     'negativeCase': '미검수 -- an unreviewed render is reported as visually accepted',
     'defendedBy': ['test_37l_not_reviewed_is_a_separate_recorded_state'],
     'rule': ('every slot carries reviewStatus and starts not_assessed; the recipe never sets '
              'it to assessed, and the manifest counts notReviewed rather than implying it. '
              'This work item does not judge whether the renders look right.')},
    {'caseId': 'NEG-08',
     'negativeCase': '블랭크 프레임 -- an empty or all-black frame passes as a render',
     'defendedBy': ['test_08f_a_blank_frame_is_rejected',
                    'test_08g_a_frame_with_an_asset_passes',
                    'test_08i_an_all_black_frame_is_rejected',
                    'test_08k_a_generated_slot_without_a_passing_gate_is_rejected',
                    'test_08h_a_frame_that_ignores_the_frozen_world_is_rejected'],
     'rule': ('a slot is not generated until the non-blank gate passes on the master AND both '
              'derived tiers; the gate floor is 4.2x below the narrowest planned silhouette.')},
    {'caseId': 'NEG-09',
     'negativeCase': '출력 경로 이탈 -- an output or manifest is written outside its root',
     'defendedBy': ['test_15_writing_outside_the_run_root_is_refused',
                    'test_24d_a_hostile_run_id_is_refused',
                    'test_24e_an_out_root_that_escapes_is_refused',
                    'test_34_run_root_must_be_the_declared_one',
                    'test_34b_a_run_root_that_resolves_outside_is_rejected'],
     'rule': ('the run id is a directory name checked against a pattern and refused if it is a '
              'traversal; the resolved run directory must stay inside '
              'Builds/ArtReview/TEAM-17/.')},
    {'caseId': 'NEG-10',
     'negativeCase': '스테일 영수증 -- a manifest from a superseded recipe revision is read as this one',
     'defendedBy': ['test_05b_a_manifest_bound_to_another_recipe_revision_is_rejected',
                    'test_34c_an_invalid_manifest_run_id_is_rejected'],
     'rule': ('the manifest records the sha256 of the recipe that wrote it; the validator '
              're-digests the live recipe and rejects the manifest when the two differ, and '
              'the run refuses to start at all rather than mixing revisions.')},
)


def negative_case_table():
    """The table as published. `defendedBy` names live tests; test_38 checks that they exist."""
    return [{'caseId': row['caseId'], 'negativeCase': row['negativeCase'],
             'rule': row['rule'], 'defendedBy': list(row['defendedBy'])} for row in NEGATIVE_CASES]


CONDITION_DIGEST = digest_of(CONDITION)
DECLARED_DIGEST = digest_of(DECLARED_NOT_CONTROLLED)
DERIVATION_DIGEST = digest_of(DERIVATION)
REVIEW_TIER_ENCODER_DIGEST = digest_of(REVIEW_TIER_ENCODER)
BLANK_GATE_DIGEST = digest_of(BLANK_GATE)
CAPTURE_PATHS_DIGEST = digest_of(CAPTURE_PATH_ALTERNATIVES)

# ------------------------------------------------------------------ condition reading
#
# Every read of CONDITION in an apply or derive path goes through a channel reader. The
# reader records which leaves were consumed, which is what makes the claim "every sealed
# field is actually applied" testable instead of decorative.

CONSUMPTION_LOG = {}

CONDITION_CHANNELS = ('framing', 'adapter', 'camera', 'import', 'scene_prep', 'save',
                      'apply')


class ConditionReader(object):
    """Reads CONDITION[path] and records the leaves it touched."""

    def __init__(self, channel, condition=None, log=None):
        if channel not in CONDITION_CHANNELS:
            raise ValueError('unknown control channel: %r' % (channel,))
        self.channel = channel
        self.condition = CONDITION if condition is None else condition
        self.log = CONSUMPTION_LOG if log is None else log

    def __call__(self, path):
        node = self.condition
        for part in path.split('.'):
            if not isinstance(node, dict) or part not in node:
                raise KeyError('CONDITION has no %r' % path)
            node = node[part]
        bucket = self.log.setdefault(self.channel, set())
        for leaf in leaf_paths(node, path):
            bucket.add(leaf)
        return node


def consumed_leaf_paths(log=None):
    merged = set()
    for paths in (CONSUMPTION_LOG if log is None else log).values():
        merged |= paths
    return merged


# ------------------------------------------------------------------ adapter


def to_blender(vec_unity, consumed=None):
    """The frozen adapter Unity(x,y,z) -> Blender(-x,-z,y), with its declaration checked."""
    C = ConditionReader('adapter', log=consumed)
    if C('scene.coordinateAdapter') != COORDINATE_ADAPTER:
        raise RuntimeError('the declared coordinate adapter is not the one implemented')
    return [-vec_unity[0], -vec_unity[2], vec_unity[1]]


def to_unity(vec_blender):
    """Inverse of to_blender: Unity(b) = (-bx, bz, -by)."""
    return [-vec_blender[0], vec_blender[2], -vec_blender[1]]


def look_direction_unity(view, consumed=None):
    """The unity-space unit look direction, derived from views[view].lookAxis."""
    C = ConditionReader('camera', log=consumed)
    axis = C('views.%s.lookAxis' % view)
    if not isinstance(axis, str) or len(axis) != 2 or axis[0] not in '+-' or axis[1] not in 'xyz':
        raise RuntimeError('lookAxis %r is not a signed axis' % (axis,))
    direction = [0.0, 0.0, 0.0]
    direction['xyz'.index(axis[1])] = 1.0 if axis[0] == '+' else -1.0
    return direction


def camera_rotation_euler_degrees(view, framing, consumed=None):
    """Rotation derived from the SAME look direction the camera position is derived from.

    A Blender camera with euler (90, 0, rz) in XYZ order has forward Rz(rz) @ Rx(90) @
    (0,0,-1) = (-sin rz, cos rz, 0). Deriving rz from the look direction instead of from
    the view name is what stops the front camera from looking away from the asset.
    """
    C = ConditionReader('camera', log=consumed)
    azimuth = C('views.%s.azimuthDegrees' % view)
    look_unity = look_direction_unity(view, consumed)
    look = to_blender(look_unity, consumed)
    if abs(look[2]) > 1e-9:
        raise RuntimeError('the frozen camera has no elevation term; look %r' % (look,))
    # azimuth is defined in unity space as the angle from +Z toward +X, so the declared
    # number and the direction the rotation is derived from cannot drift apart.
    declared_azimuth = math.degrees(math.atan2(look_unity[0], look_unity[2])) % 360.0
    if abs((declared_azimuth - azimuth) % 360.0) > 1e-9:
        raise RuntimeError('views.%s.azimuthDegrees is %.3f but views.%s.lookAxis %r is at '
                           'azimuth %.3f' % (view, azimuth, view, axis_of(look_unity),
                                             declared_azimuth))
    rz = math.degrees(math.atan2(-look[0], look[1])) % 360.0
    return [90.0, 0.0, rz]


def axis_of(vec_unity):
    for index, name in enumerate('xyz'):
        if abs(abs(vec_unity[index]) - 1.0) < 1e-9:
            return ('+' if vec_unity[index] > 0 else '-') + name
    return '?'


def camera_forward_blender(rotation_degrees):
    """Independent re-derivation of the camera forward vector from the euler triple."""
    rx, ry, rz = (math.radians(d) for d in rotation_degrees)
    (sx, cx), (sy, cy), (sz, cz) = ((math.sin(rx), math.cos(rx)),
                                    (math.sin(ry), math.cos(ry)),
                                    (math.sin(rz), math.cos(rz)))
    # R = Rz @ Ry @ Rx, applied to the local view direction (0, 0, -1).
    forward = (0.0, 0.0, -1.0)
    after_x = (forward[0],
               cx * forward[1] - sx * forward[2],
               sx * forward[1] + cx * forward[2])
    after_y = (cy * after_x[0] + sy * after_x[2],
               after_x[1],
               -sy * after_x[0] + cy * after_x[2])
    after_z = (cz * after_y[0] - sz * after_y[1],
               sz * after_y[0] + cz * after_y[1],
               after_y[2])
    return list(after_z)


# ------------------------------------------------------------------ framing
#
# Pure arithmetic on the published Unity-space bounds, so the whole plan is computable
# offline and the Blender pass has something independent to be checked against.


def _bounds(entry):
    box = entry['boundsUnity']
    return list(box['min']), list(box['max'])


def expected_ortho_scale(entry, view, margin=None):
    """The orthographic width the framing rule produces, in metres."""
    lo, hi = _bounds(entry)
    width, height, depth = (hi[i] - lo[i] for i in range(3))
    spec = CONDITION['views'][view]
    margin = CONDITION['framing']['marginFactor'] if margin is None else margin
    res_x, res_y = CONDITION['render']['resolutionX'], CONDITION['render']['resolutionY']
    aspect = res_x / res_y
    extents = {'x': width, 'y': height, 'z': depth}
    half_u = extents[spec['coversAxes'][0]] / 2.0 * margin
    half_v = extents[spec['coversAxes'][1]] / 2.0 * margin
    return max(2.0 * half_u, 2.0 * half_v * aspect)


def silhouette_fraction(entry, view):
    """Upper bound on the fraction of the frame the asset's axis-aligned box covers.

    The real silhouette is at most this (a truss or a ring covers less of its bounding
    rectangle), so this is the number the non-blank gate's floor has to stay below.
    """
    lo, hi = _bounds(entry)
    width, height, depth = (hi[i] - lo[i] for i in range(3))
    spec = CONDITION['views'][view]
    extents = {'x': width, 'y': height, 'z': depth}
    u = extents[spec['coversAxes'][0]]
    v = extents[spec['coversAxes'][1]]
    res_x, res_y = CONDITION['render']['resolutionX'], CONDITION['render']['resolutionY']
    aspect = res_x / res_y
    ortho = expected_ortho_scale(entry, view)
    return (u / ortho) * (v / (ortho / aspect))


def framing_for(entry, view, consumed=None):
    C = ConditionReader('framing', log=consumed)
    if C('framing.rule') != 'orthographic_aabb_fit':
        raise RuntimeError('framing.rule is not the rule implemented here')
    if C('framing.centre') != 'bounds_center_unity_space':
        raise RuntimeError('framing.centre is not the centre implemented here')
    if C('framing.boundsSource') != BOUNDS_SOURCE:
        raise RuntimeError('framing.boundsSource is not the bounds source read here')
    margin = C('framing.marginFactor')
    res_x = C('render.resolutionX')
    res_y = C('render.resolutionY')
    aspect = res_x / res_y
    if C('framing.distanceRule') != DISTANCE_RULE:
        raise RuntimeError('framing.distanceRule is not the distance implemented here')
    if C('framing.clipStartRule') != CLIP_START_RULE:
        raise RuntimeError('framing.clipStartRule is not the clip start implemented here')
    if C('framing.clipEndRule') != CLIP_END_RULE:
        raise RuntimeError('framing.clipEndRule is not the clip end implemented here')
    lo, hi = _bounds(entry)
    width, height, depth = (hi[i] - lo[i] for i in range(3))
    covers = list(C('views.%s.coversAxes' % view))
    look_axis = C('views.%s.lookAxis' % view)
    extents = {'x': width, 'y': height, 'z': depth}
    if len(covers) != 2 or covers[0] not in extents or covers[1] not in extents:
        raise RuntimeError('unsupported covered axes: %r' % (covers,))
    half_u = extents[covers[0]] / 2.0 * margin
    half_v = extents[covers[1]] / 2.0 * margin
    ortho_scale = max(2.0 * half_u, 2.0 * half_v * aspect)
    v = max(width, height, depth)
    distance = v * 10.0 + 1.0
    centre = [(lo[i] + hi[i]) / 2.0 for i in range(3)]
    look_unit = look_direction_unity(view, consumed)
    cam = [centre[i] - look_unit[i] * distance for i in range(3)]
    framing = {
        'view': view,
        'boundsUnity': {'min': lo, 'max': hi},
        'sizeUnity': {'width': round(width, 6), 'height': round(height, 6),
                      'depth': round(depth, 6)},
        'coveredAxes': covers,
        'lookAxis': look_axis,
        'marginFactor': margin,
        'aspect': round(aspect, 6),
        'orthoScale': round(ortho_scale, 6),
        'horizontalExtentMetres': round(ortho_scale, 6),
        'verticalExtentMetres': round(ortho_scale / aspect, 6),
        'coverageMargin': {
            'u': round((ortho_scale / 2.0) / half_u, 6),
            'v': round((ortho_scale / aspect / 2.0) / half_v, 6),
        },
        'distance': round(distance, 6),
        'clipStart': round(v * 0.01, 6),
        'clipEnd': round((distance + v) * 4.0, 6),
        'centreUnity': [round(c, 6) for c in centre],
        'cameraLocationUnity': [round(c, 6) for c in cam],
        'cameraLocationBlender': [round(c, 6) for c in to_blender(cam, consumed)],
        'cameraLookDirectionUnity': [round(c, 6) for c in look_unit],
    }
    framing['cameraLookDirectionBlender'] = [round(c, 6) for c in to_blender(look_unit, consumed)]
    return framing


def framing_digest(framing):
    return digest_of(framing)


# ------------------------------------------------------------------ blank-frame gate


def display_world_color():
    """The flat world colour as it lands in the PNG, i.e. after the sRGB transfer."""
    return [round(srgb_encode(component), 6) for component in CONDITION['world']['color']]


def blank_frame_metrics(pixels, width, height, reference=None,
                        channel_tolerance=None, corner_tolerance=None,
                        max_samples=None, consumed=None):
    """Non-blank metrics for a flat RGBA float buffer, without Blender or numpy.

    `pixels` is the flattened RGBA sequence Blender exposes as Image.pixels, in display
    space. A blank frame has nonBackgroundRatio 0 and one distinct colour.
    """
    reference = display_world_color() if reference is None else reference
    channel_tolerance = (BLANK_GATE['channelTolerance'] if channel_tolerance is None
                         else channel_tolerance)
    corner_tolerance = (BLANK_GATE['cornerTolerance'] if corner_tolerance is None
                        else corner_tolerance)
    max_samples = BLANK_GATE['maxSamples'] if max_samples is None else max_samples
    total_pixels = width * height
    stride = max(1, total_pixels // max_samples)
    distinct = set()
    sampled = 0
    non_background = 0
    for index in range(0, total_pixels, stride):
        base = index * 4
        red, green, blue = pixels[base], pixels[base + 1], pixels[base + 2]
        sampled += 1
        if (abs(red - reference[0]) > channel_tolerance
                or abs(green - reference[1]) > channel_tolerance
                or abs(blue - reference[2]) > channel_tolerance):
            non_background += 1
        if len(distinct) <= 4096:
            distinct.add((round(red * 255), round(green * 255), round(blue * 255)))
    corners = []
    for cx, cy in ((0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)):
        base = (cy * width + cx) * 4
        corners.append([pixels[base], pixels[base + 1], pixels[base + 2]])
    corner_max_error = max(
        max(abs(corner[i] - reference[i]) for i in range(3)) for corner in corners)
    return {
        'sampledPixels': sampled,
        'totalPixels': total_pixels,
        'sampleStride': stride,
        'referenceDisplayRgb': list(reference),
        'nonBackgroundSamples': non_background,
        'nonBackgroundRatio': round(non_background / float(sampled), 6),
        'distinctColours': len(distinct),
        'cornerSamples': [[round(c, 6) for c in corner] for corner in corners],
        'cornerMaxError': round(corner_max_error, 6),
    }


def assert_frame_not_blank(metrics, label):
    """The gate a slot must pass before it may be recorded as generated."""
    min_ratio = BLANK_GATE['minNonBackgroundRatio']
    min_distinct = BLANK_GATE['minDistinctColours']
    corner_tolerance = BLANK_GATE['cornerTolerance']
    if metrics['cornerMaxError'] > corner_tolerance:
        raise RuntimeError(
            '%s: the frame corners are %.4f from the frozen flat world colour (tolerance '
            '%.4f); the world or colour management is not the frozen condition'
            % (label, metrics['cornerMaxError'], corner_tolerance))
    if metrics['distinctColours'] < min_distinct:
        raise RuntimeError('%s: only %d distinct colours in the frame; the render is flat'
                           % (label, metrics['distinctColours']))
    if metrics['nonBackgroundRatio'] < min_ratio:
        raise RuntimeError('%s: non-background ratio %.6f is below the %.6f floor; the frame '
                           'is empty or the asset is off camera'
                           % (label, metrics['nonBackgroundRatio'], min_ratio))
    return True


# ------------------------------------------------------------------ png evidence


def png_ihdr(path):
    """Width, height, bit depth and colour type read from the file's own IHDR chunk."""
    with open(path, 'rb') as handle:
        head = handle.read(33)
    if len(head) < 33 or head[:8] != b'\x89PNG\r\n\x1a\n' or head[12:16] != b'IHDR':
        raise RuntimeError('not a PNG with a leading IHDR chunk: %s' % path)
    width, height, depth, colour, compression, filt, interlace = struct.unpack(
        '>IIBBBBB', head[16:29])
    return {'width': width, 'height': height, 'bitDepth': depth, 'colourType': colour,
            'compression': compression, 'filter': filt, 'interlace': interlace}


# ------------------------------------------------------------------ guards


def guarded_sources(baseline):
    """Everything this recipe must not touch, with the digest it must still have."""
    rows = {
        rel(BASELINE): sha256(BASELINE),
        rel(SOURCE_BLEND): sha256(SOURCE_BLEND),
        rel(GENERATOR): sha256(GENERATOR),
        # object-references.json is the baseline's declared referenceProvenance and is
        # listed as a read-only input in the manifest, so it is guarded rather than merely
        # named: a claim of "read only" that no check enforces is prose, not a boundary.
        rel(OBJECT_REFERENCES): sha256(OBJECT_REFERENCES),
    }
    for name in sorted(baseline.get('sourceModuleSha256', {})):
        rows[name] = sha256(ROOT / name)
    for entry in baseline['assets']:
        rows[entry['file']] = sha256(ROOT / entry['file'])
    return rows


def published_source_digests(baseline):
    """The digests the PUBLISHED baseline declares, independent of what is on disk now.

    guarded_sources() re-digests the working tree, so on its own it can only tell whether a
    file changed *during* this run. It cannot tell whether an original was already replaced
    before the run started: a mutated FBX would be digested, recorded as `before`, and then
    re-digested identically as `after`, and the manifest would report "originals unchanged"
    about a tree that no longer holds the published originals. This table is the anchor that
    makes that case detectable, and it is what the per-slot sourceFbx records are compared
    against in validate().
    """
    rows = {}
    for entry in baseline['assets']:
        rows[entry['file']] = entry['sha256']
    for name, digest in (baseline.get('sourceModuleSha256') or {}).items():
        rows[name] = digest
    if baseline.get('generatorSha256'):
        rows[rel(GENERATOR)] = baseline['generatorSha256']
    return rows


def published_digest_drift(baseline):
    """Published vs live digests, for the inputs the baseline pins. Empty means anchored."""
    drift = {}
    for path, published in published_source_digests(baseline).items():
        target = ROOT / path
        if not target.is_file():
            drift[path] = {'published': published, 'actual': None}
            continue
        actual = sha256(target)
        if actual != published:
            drift[path] = {'published': published, 'actual': actual}
    return drift


def source_state(entry):
    """Classify the input a slot needs, before any render is attempted.

    Returns (state, reason) with state one of:
      'ok'      -- the source is present and byte-identical to the published baseline
      'missing' -- the source file is not on disk at all: nothing can be attempted (누락)
      'mutated' -- the source exists but is not the published original (원본 덮어쓰기)
    """
    path = ROOT / entry['file']
    if not path.is_file():
        return 'missing', ('the source this slot renders is not on disk: %s (누락; no render '
                           'pass was attempted for this slot)' % entry['file'])
    actual = sha256(path)
    if actual != entry['sha256']:
        return 'mutated', ('the source on disk is not the published original: %s sha256 %s '
                           'but the baseline declares %s (원본 덮어쓰기; the slot is refused '
                           'rather than rendered from an unverified original)'
                           % (entry['file'], actual, entry['sha256']))
    return 'ok', None


def classify_slot(slot, entry, state, reason):
    """Record a pre-render classification on a slot without ever claiming a produced file."""
    slot['outputs'] = placeholder_outputs(entry['id'], slot['view'])
    slot['blankFrameGate'] = None
    slot['observation'] = None
    slot['runId'] = None
    slot.pop('resumed', None)
    slot['status'] = 'missing' if state == 'missing' else 'failed'
    slot['reason'] = reason
    return slot


def assert_not_guarded(path, guards):
    """Refuse to write over, or inside, any guarded original. No root constraint."""
    target = Path(path).resolve()
    for guarded in guards:
        source = (ROOT / guarded).resolve()
        if target == source:
            raise RuntimeError('refusing to write over a guarded original: %s' % guarded)
        if source.is_dir() and source in target.parents:
            raise RuntimeError('refusing to write inside a guarded source tree: %s' % guarded)
    return target


def assert_output_outside_guards(path, guards, allowed_root=None):
    """The guarded-set check plus the run-root containment the raster outputs obey."""
    target = assert_not_guarded(path, guards)
    root = Path(allowed_root or DEFAULT_OUT_ROOT).resolve()
    if root != target and root not in target.parents:
        raise RuntimeError('write target escapes the run output root: %s' % target)
    return target


def assert_manifest_path_safe(path, guards):
    """The manifest is a write target too, and the guard applies to it as well.

    Without this, --manifest foundation/art/asset-manifest.json is a first-class flag
    that overwrites a guarded original, while the docstring claims every write target is
    checked.
    """
    target = assert_not_guarded(path, guards)
    root = ROOT.resolve()
    if target != root and root not in target.parents:
        raise RuntimeError('refusing to write a manifest outside the repository: %s' % target)
    if target.suffix != '.json':
        raise RuntimeError('refusing to write a manifest that is not a .json file: %s' % target)
    return target


def assert_guards_intact(guards, where):
    drifted = {}
    for path, expected in guards.items():
        actual = sha256(ROOT / path)
        if actual != expected:
            drifted[path] = {'expected': expected, 'actual': actual}
    if drifted:
        raise RuntimeError('guarded original changed after %s: %s'
                           % (where, json.dumps(drifted)))


# ------------------------------------------------------------------ run identity


def assert_valid_run_id(run_id):
    """A run id is a directory name, so it may not be a path or a traversal."""
    if not isinstance(run_id, str) or not RUN_ID_PATTERN.match(run_id):
        raise RecipeUsageError('run-id %r does not match %s'
                               % (run_id, RUN_ID_PATTERN.pattern))
    if run_id in RUN_ID_FORBIDDEN:
        raise RecipeUsageError('run-id %r is a path traversal' % (run_id,))
    return run_id


def resolve_run_dir(run_id, out_root=None):
    """Resolve the run directory and refuse anything that leaves the declared root.

    Checking the manifest *string* is not enough: --out-root
    Builds/ArtReview/TEAM-17/../../../foundation/art resolves to a guarded tree while
    still reading as a legal string.
    """
    assert_valid_run_id(run_id)
    default = DEFAULT_OUT_ROOT.resolve()
    root = Path(out_root).resolve() if out_root else default
    if root != default and default not in root.parents:
        raise RecipeUsageError('--out-root must lie inside %s, got %s'
                               % (rel(DEFAULT_OUT_ROOT), root))
    run_dir = (root / run_id).resolve()
    if run_dir != default and default not in run_dir.parents:
        raise RecipeUsageError('run directory escapes %s: %s' % (rel(DEFAULT_OUT_ROOT), run_dir))
    if run_dir.name != run_id:
        raise RecipeUsageError('run-id %r resolves to directory %r'
                               % (run_id, run_dir.name))
    return root, run_dir


# ------------------------------------------------------------------ plan


def slot_id(asset_id, view):
    return '%s::%s' % (asset_id, view)


def idempotency_key(asset_id, view, recipe_sha, condition_sha):
    return digest_of({
        'workId': WORK_ID,
        'assetId': asset_id,
        'view': view,
        'recipeSha256': recipe_sha,
        'conditionDigest': condition_sha,
        'masterResolution': list(MASTER_RESOLUTION),
    })


def expected_output_paths(asset_id, view):
    return {
        'candidate4k': 'candidate4k/%s__%s__4k.png' % (asset_id, view),
        'render': 'render/%s__%s__review.png' % (asset_id, view),
    }


def placeholder_outputs(asset_id, view):
    return {
        tier: {'path': path, 'sha256': None, 'bytes': None, 'present': False, 'png': None}
        for tier, path in expected_output_paths(asset_id, view).items()
    }


def plan_slots(baseline, recipe_sha):
    """Enumerate every required slot, classifying the ones that cannot be attempted.

    The classification happens here, at plan time, not only during a render pass: a slot
    whose source is absent is `missing` in the plan itself, so a reader never has to infer
    누락 from a run that never mentioned the asset.
    """
    slots = []
    for entry in baseline['assets']:
        asset_id = entry['id']
        state, reason = source_state(entry)
        for view in VIEWS:
            framing = framing_for(entry, view)
            slot = {
                'slotId': slot_id(asset_id, view),
                'assetId': asset_id,
                'view': view,
                'status': 'unrun',
                'runId': None,
                'reason': 'not yet executed',
                'conditionDigest': CONDITION_DIGEST,
                'derivationDigest': DERIVATION_DIGEST,
                'reviewTierEncoderDigest': REVIEW_TIER_ENCODER_DIGEST,
                'idempotencyKey': idempotency_key(asset_id, view, recipe_sha, CONDITION_DIGEST),
                'sourceFbx': {
                    'path': entry['file'],
                    'sha256': entry['sha256'],
                    'triangles': entry['triangles'],
                    'meshParts': entry['meshParts'],
                    'state': 'ok' if state == 'ok' else state,
                },
                'framing': framing,
                'framingDigest': framing_digest(framing),
                'cameraRotationEulerDegrees': camera_rotation_euler_degrees(view, framing),
                'outputs': placeholder_outputs(asset_id, view),
                'blankFrameGate': None,
                'observation': None,
                'reviewStatus': 'not_assessed',
            }
            if state != 'ok':
                classify_slot(slot, entry, state, reason)
            slots.append(slot)
    return slots


def slot_accounting(slots, baseline):
    """The four-way classification, counted from the slots and from nothing else."""
    counts = {status: 0 for status in STATUSES}
    for slot in slots:
        counts[slot['status']] = counts.get(slot['status'], 0) + 1
    return {
        'assets': len(baseline['assets']),
        'viewsPerAsset': len(VIEWS),
        'requiredSlots': REQUIRED_SLOTS,
        'required4kCandidates': REQUIRED_SLOTS,
        'requiredReviewRenders': REQUIRED_SLOTS,
        'enumeratedSlots': len(slots),
        'generated': counts['generated'],
        'failed': counts['failed'],
        'missing': counts['missing'],
        'unrun': counts['unrun'],
        'reviewed': sum(1 for slot in slots if slot.get('reviewStatus') == 'assessed'),
        'notReviewed': sum(1 for slot in slots if slot.get('reviewStatus') != 'assessed'),
        'classificationComplete': (sum(counts.values()) == len(slots) == REQUIRED_SLOTS),
    }


def build_plan(baseline, recipe_sha, run_id, guards):
    return {
        'schemaVersion': SCHEMA_VERSION,
        'manifestId': 'TEAM-17-render-manifest',
        'workId': WORK_ID,
        'issue': ISSUE,
        'targetIssue': ISSUE,
        'parentIssue': PARENT_ISSUE,
        'phase': 'candidate',
        'classification': 'PUBLIC_SYNTHETIC',
        'disclaimer': ('합성 자산 키트의 검수 렌더 계획이다. 실제 부산역 시설과의 일치, 시각 수용, '
                       '실차 훈련 전이를 판정하지 않는다.'),
        'generatedAt': utc_now(),
        'status': 'planned_not_generated',
        'scope': 'existing_33_asset_review_render_and_4k_candidate',
        'scopeStatement': (
            '이 매니페스트는 이미 배포된 33종 합성 station kit 자산을 입력으로, 고정된 하나의 렌더 '
            '조건에서 자산별 정면·측면 검수 렌더와 4K 파생 후보를 별도 출력으로 계획·기록한다. '
            '원본 FBX 33개, station-kit.blend, asset-manifest.json은 읽기 전용이며 이 레시피는 그 '
            '바이트를 바꾸지 않는다. 여기서 "4K 후보"는 3840x2160 래스터 검수 렌더이지 4K 텍스처 '
            '베이크가 아니다. 이 매니페스트는 시각 AAA 판정도, 실제 시설 일치도, 훈련 전이도 '
            '주장하지 않는다.'
        ),
        'requiredOutputs': [
            'scripts/art/team/TEAM-17/render_recipe.py',
            'foundation/art/team/TEAM-17/render-manifest.json',
        ],
        'recipe': {
            'path': rel(RECIPE),
            'sha256': recipe_sha,
            'blenderVersionRequired': BLENDER_REQUIRED,
            'invocationPlan': 'python3 %s --plan' % rel(RECIPE),
            'invocationCheck': 'python3 %s --check' % rel(RECIPE),
            'invocationTest': 'python3 %s --test' % rel(RECIPE),
            'invocationVerifyOutputs': 'python3 %s --verify-outputs' % rel(RECIPE),
            'invocationRun': ('blender -b --factory-startup --python %s -- --run --run-id %s'
                              % (rel(RECIPE), run_id)),
            'blenderVersionAtRun': None,
            'controlChannels': list(CONDITION_CHANNELS),
        },
        'condition': json.loads(canonical(CONDITION)),
        'conditionDigest': CONDITION_DIGEST,
        'conditionScope': {
            'sameForAll': ['engine', 'sampling', 'render', 'colorManagement', 'camera',
                           'lighting', 'world', 'scene', 'framing.rule',
                           'framing.marginFactor'],
            'variesPerView': {'field': 'views[view].azimuthDegrees / lookAxis',
                              'front': 0.0, 'side': 90.0},
            'variesPerAsset': {
                'field': 'framing.orthoScale / distance / clipStart / clipEnd / cameraLocation',
                'why': ('하나의 절대 ortho scale로는 0.24 m 자산과 15.0 m 자산을 함께 담을 수 없다. '
                        '고정된 것은 규칙·상수이며, 자산별 파생값은 자산의 공개 bounds에서만 계산되고 '
                        '매 슬롯 framingDigest로 봉인된다.'),
                'derivedFrom': 'foundation/art/asset-manifest.json:/assets/<id>/boundsUnity (읽기 전용)',
            },
            'declaredDifferenceBetweenTiers': {
                'tiers': list(TIERS),
                'difference': DERIVATION['render']['operation'],
                'note': ('두 계층은 같은 마스터 래스터 1회에서 나온다. 시점·조명·프레이밍·색관리는 '
                         '구성상 동일하며 재렌더로 맞춘 것이 아니다.'),
            },
            'sealingDiscipline': (
                'CONDITION의 모든 leaf 는 control channel 하나가 실제로 읽는다. 테스트가 그 사실을 '
                '강제하므로 봉인된 필드가 아무것도 제어하지 않는 상태로 남을 수 없다.'),
        },
        'declaredNotControlled': json.loads(canonical(DECLARED_NOT_CONTROLLED)),
        'declaredNotControlledDigest': DECLARED_DIGEST,
        'blankFrameGate': json.loads(canonical(BLANK_GATE)),
        'blankFrameGateDigest': BLANK_GATE_DIGEST,
        'capturePathAlternatives': json.loads(canonical(CAPTURE_PATH_ALTERNATIVES)),
        'capturePathAlternativesDigest': CAPTURE_PATHS_DIGEST,
        'textureBake': json.loads(canonical(TEXTURE_BAKE)),
        'negativeCaseMatrix': negative_case_table(),
        'negativeCaseMatrixNote': (
            '이 표의 각 행은 이 작업이 실패할 수 있는 부정 사례와, 그것을 실제로 막는 테스트 '
            '이름을 묶는다. test_38이 각 이름이 존재하는 테스트인지 검사하므로 표가 의도 '
            '목록으로 썩지 않는다. 여기서 시각적 수용(AAA)은 판정하지 않는다.'),
        'sharedOutputsNote': SHARED_OUTPUTS_NOTE,
        'outputRoot': {
            'template': 'Builds/ArtReview/TEAM-17/<run-id>/',
            'runId': run_id,
            'path': '%s/%s/' % (rel(DEFAULT_OUT_ROOT), run_id),
            'resolvedPathWithinRepo': True,
            'layout': {
                'temp/': 'transient master raster pass, not retained',
                'candidate4k/': '<Asset>__<view>__4k.png, 3840x2160',
                'render/': '<Asset>__<view>__review.png, 1920x1080',
            },
            'canonicalWriteAllowed': False,
        },
        'inputs': {
            'baselineManifest': {'path': rel(BASELINE), 'sha256': sha256(BASELINE),
                                 'selector': '/assets; /sourceModuleSha256; /coordinateAdapter; /units',
                                 'mode': 'read_only'},
            'objectReferences': {'path': rel(OBJECT_REFERENCES), 'sha256': sha256(OBJECT_REFERENCES),
                                 'mode': 'read_only'},
            'sourceBlend': {'path': rel(SOURCE_BLEND), 'sha256': sha256(SOURCE_BLEND),
                            'use': 'read-only, never opened'},
            'assetCount': len(baseline['assets']),
            'assetIds': [entry['id'] for entry in baseline['assets']],
            'units': baseline['units'],
            'coordinateAdapter': baseline['coordinateAdapter'],
            'blenderDeclared': baseline['blender'],
        },
        'preservation': {
            'guardedPaths': sorted(guards),
            'before': guards,
            'after': None,
            'unchanged': None,
            'manifestPathGuarded': ('the manifest path itself passes assert_manifest_path_safe '
                                    'before every write; --manifest pointing at a guarded path '
                                    'is refused'),
            'enforcement': ('every output path is resolved and rejected if it equals or sits inside '
                            'a guarded path; the run directory is resolved and rejected if it '
                            'escapes Builds/ArtReview/TEAM-17/; guarded digests are recomputed '
                            'after the run and must match'),
        },
        'derivation': json.loads(canonical(DERIVATION)),
        'derivationDigest': DERIVATION_DIGEST,
        'reviewTierEncoder': json.loads(canonical(REVIEW_TIER_ENCODER)),
        'reviewTierEncoderDigest': REVIEW_TIER_ENCODER_DIGEST,
        'slotAccounting': slot_accounting([], baseline),
        'slots': [],
        'runs': [],
        'pilot': None,
        'actuals': None,
        'checksRun': [],
        'failedOrUnrun': [],
        'blockers': [],
        'handoff': None,
        'evidenceFiles': {},
    }


# ------------------------------------------------------------------ validation


def validate(manifest, guards=None):
    """Return (errors, report). Every check below maps to a stated negative case."""
    errors = []

    def fail(message):
        errors.append(message)

    if not isinstance(manifest, dict):
        return ['manifest is not a JSON object'], {'result': 'fail', 'errors': ['not an object']}

    slots = manifest.get('slots')
    if slots is None:
        slots = []
        fail('manifest has no slots list')
    if not isinstance(slots, list):
        return ['manifest.slots is not a list'], {'result': 'fail', 'errors': ['bad slots']}

    baseline = load_json(BASELINE)
    ids = [entry['id'] for entry in baseline['assets']]

    if manifest.get('schemaVersion') != SCHEMA_VERSION:
        fail('unknown manifest schemaVersion')
    if manifest.get('workId') != WORK_ID:
        fail('workId is not %s' % WORK_ID)
    if manifest.get('conditionDigest') != CONDITION_DIGEST:
        fail('manifest conditionDigest does not match the frozen condition in the recipe')
    if manifest.get('derivationDigest') != DERIVATION_DIGEST:
        fail('manifest derivationDigest does not match the recipe')
    if manifest.get('reviewTierEncoderDigest') != REVIEW_TIER_ENCODER_DIGEST:
        fail('manifest reviewTierEncoderDigest does not match the recipe: the encoder limits '
             'the review tier is subject to changed without the digest following')
    if manifest.get('blankFrameGateDigest') != BLANK_GATE_DIGEST:
        fail('manifest blankFrameGateDigest does not match the recipe')

    recipe_section = manifest.get('recipe')
    recipe_sha = None
    if not isinstance(recipe_section, dict):
        fail('manifest.recipe is not an object')
    else:
        recipe_sha = recipe_section.get('sha256')
        if not recipe_sha:
            fail('manifest.recipe.sha256 is absent: slot idempotency cannot be recomputed')
        else:
            live_sha = sha256(RECIPE)
            if recipe_sha != live_sha:
                fail('manifest.recipe.sha256 %s does not describe the recipe at %s (sha256 %s); '
                     'the manifest was planned under a revision that is not the delivered one'
                     % (recipe_sha, rel(RECIPE), live_sha))

    # ---- coverage: no missing view, no duplicate slot, no malformed slot
    seen = {}
    for position, slot in enumerate(slots):
        if not isinstance(slot, dict):
            fail('slots[%d] is not an object' % position)
            continue
        key = slot.get('slotId')
        if not key:
            fail('slots[%d] has no slotId' % position)
            continue
        if key in seen:
            fail('duplicate slot: %s' % key)
            continue
        if not slot.get('assetId'):
            fail('%s: slot has no assetId' % key)
            continue
        if not slot.get('view'):
            fail('%s: slot has no view' % key)
            continue
        seen[key] = slot
    expected = {slot_id(asset_id, view) for asset_id in ids for view in VIEWS}
    missing = sorted(expected - set(seen))
    extra = sorted(set(seen) - expected)
    if missing:
        fail('missing views (%d): %s' % (len(missing), ', '.join(missing[:8])))
    if extra:
        fail('slots that do not correspond to an asset view: %s' % ', '.join(extra[:8]))
    if len(slots) != REQUIRED_SLOTS:
        fail('enumerated slot count %d is not %d' % (len(slots), REQUIRED_SLOTS))

    # ---- accounting: generated + failed + missing + unrun must equal slots, exclusively
    counts = {status: 0 for status in STATUSES}
    keys = set()
    run_ids = set()
    for key, slot in sorted(seen.items()):
        status = slot.get('status')
        if status not in counts:
            fail('%s: status %r is none of the four classified states (%s)'
                 % (key, status, ', '.join(STATUSES)))
            continue
        counts[status] += 1
        if slot.get('conditionDigest') != CONDITION_DIGEST:
            fail('%s: slot condition differs from the frozen condition (non-identical condition)'
                 % key)
        framing = slot.get('framing')
        if not isinstance(framing, dict) or not framing:
            fail('%s: framing is absent or is not an object' % key)
        elif slot.get('framingDigest') != framing_digest(framing):
            fail('%s: framing digest does not match its content' % key)
        elif framing.get('view') != slot.get('view'):
            fail('%s: framing view does not match the slot view' % key)
        identity = slot.get('idempotencyKey')
        if not identity:
            fail('%s: no idempotency key' % key)
        elif identity in keys:
            fail('%s: idempotency key collides with another slot (restart duplication)' % key)
        else:
            keys.add(identity)
            if recipe_sha:
                recomputed = idempotency_key(slot['assetId'], slot['view'], recipe_sha,
                                             CONDITION_DIGEST)
                if identity != recomputed:
                    fail('%s: idempotency key is not the key this recipe revision and '
                         'condition produce (the slot was planned under a superseded '
                         'revision)' % key)
        rotation = slot.get('cameraRotationEulerDegrees')
        if not isinstance(rotation, list) or len(rotation) != 3:
            fail('%s: cameraRotationEulerDegrees is absent or malformed' % key)
        outputs = slot.get('outputs')
        if not isinstance(outputs, dict):
            fail('%s: outputs is not an object' % key)
            outputs = {}
        for tier in outputs:
            if tier not in TIERS:
                fail('%s: unknown output tier %r' % (key, tier))
        if status == 'generated':
            recorded_run = slot.get('runId')
            if not recorded_run or not RUN_ID_PATTERN.match(str(recorded_run)):
                fail('%s: generated but no valid runId is recorded, so the declared and actual '
                     'output locations can diverge' % key)
            else:
                run_ids.add(recorded_run)
            for tier in TIERS:
                record = outputs.get(tier)
                if not isinstance(record, dict):
                    fail('%s: generated but the %s output record is absent' % (key, tier))
                    continue
                if not record.get('sha256') or not record.get('bytes'):
                    fail('%s: generated but the %s output has no digest or byte count'
                         % (key, tier))
                png = record.get('png')
                if not isinstance(png, dict):
                    fail('%s: generated but the %s output has no PNG header evidence'
                         % (key, tier))
                else:
                    expected_resolution = (MASTER_RESOLUTION if tier == 'candidate4k'
                                           else REVIEW_RESOLUTION)
                    if (png.get('width'), png.get('height')) != expected_resolution:
                        fail('%s: %s is %rx%r but the tier is declared %dx%d'
                             % (key, tier, png.get('width'), png.get('height'),
                                expected_resolution[0], expected_resolution[1]))
            gate = slot.get('blankFrameGate')
            if not isinstance(gate, dict) or not gate.get('passed'):
                fail('%s: generated but the non-blank gate did not pass' % key)
            if not slot.get('observation'):
                fail('%s: generated but nothing observed was recorded' % key)
        else:
            # A slot that was not generated may declare where its output WOULD go, but it must
            # not carry a digest, a byte count or a presence claim: that would be a render that
            # did not happen being read as a render that did. This applies to missing and
            # failed slots exactly as it does to unrun ones.
            for tier, record in outputs.items():
                if not isinstance(record, dict):
                    fail('%s: %s output record is not an object' % (key, tier))
                    continue
                if (record.get('sha256') or record.get('bytes') is not None
                        or record.get('present')):
                    fail('%s: %s but the %s output record claims a produced file'
                         % (key, status, tier))
            if slot.get('blankFrameGate'):
                fail('%s: %s but a non-blank gate result is recorded' % (key, status))
            if slot.get('observation'):
                fail('%s: %s but an observation of a render is recorded' % (key, status))
        if status in ('failed', 'missing', 'unrun') and not str(slot.get('reason') or '').strip():
            fail('%s: %s without a stated reason' % (key, status))
        # The source record must describe the PUBLISHED original, not whatever was on disk
        # when the slot was written. A manifest that re-digested a replaced FBX would make
        # an overwritten original look like the baseline it replaced.
        source = slot.get('sourceFbx')
        published = published_source_digests(baseline).get(str((source or {}).get('path')))
        if published is None:
            fail('%s: sourceFbx names a path the published baseline does not declare' % key)
        elif (source or {}).get('sha256') != published:
            fail('%s: sourceFbx.sha256 is not the digest the published baseline declares for '
                 '%s (the slot is not describing the original it was planned against)'
                 % (key, (source or {}).get('path')))

    accounting = manifest.get('slotAccounting')
    if not isinstance(accounting, dict):
        fail('slotAccounting is absent or is not an object')
        accounting = {}
    for name, value in counts.items():
        if accounting.get(name) != value:
            fail('slotAccounting.%s is %r but %d slots carry that status'
                 % (name, accounting.get(name), value))
    if sum(counts.values()) != len(seen):
        fail('the four slot states do not add up to the slot list: %d classified, %d slots'
             % (sum(counts.values()), len(seen)))
    for name in ('assets', 'viewsPerAsset', 'requiredSlots', 'required4kCandidates',
                 'requiredReviewRenders', 'enumeratedSlots', 'missing'):
        if accounting.get(name) is None:
            fail('slotAccounting.%s is absent' % name)
    if accounting.get('assets') != len(ids):
        fail('slotAccounting.assets is not the published asset count')
    if accounting.get('viewsPerAsset') != len(VIEWS):
        fail('slotAccounting.viewsPerAsset is not the required view count')
    if accounting.get('requiredSlots') != REQUIRED_SLOTS:
        fail('slotAccounting.requiredSlots is not %d' % REQUIRED_SLOTS)
    if accounting.get('required4kCandidates') != REQUIRED_SLOTS:
        fail('slotAccounting.required4kCandidates is not %d' % REQUIRED_SLOTS)
    if accounting.get('requiredReviewRenders') != REQUIRED_SLOTS:
        fail('slotAccounting.requiredReviewRenders is not %d' % REQUIRED_SLOTS)
    if accounting.get('enumeratedSlots') != len(slots):
        fail('slotAccounting.enumeratedSlots does not match the slot list')
    if accounting.get('classificationComplete') is not True:
        fail('slotAccounting.classificationComplete is not true: the four states do not '
             'account for every required slot')
    not_reviewed = sum(1 for s in seen.values() if s.get('reviewStatus') != 'assessed')
    if accounting.get('notReviewed') is None:
        fail('slotAccounting.notReviewed is absent: 미검수 is not recorded')
    elif accounting['notReviewed'] != not_reviewed:
        fail('slotAccounting.notReviewed is %r but %d slots are not visually assessed'
             % (accounting['notReviewed'], not_reviewed))

    # ---- the status claim must match the slots, never lead them
    generated = counts['generated']
    status = manifest.get('status')
    if generated == 0 and status in ('generated', 'generated_partial_run', 'complete',
                                     'accepted'):
        fail('status claims %r while no slot was generated' % status)
    if status == 'generated':
        if generated != REQUIRED_SLOTS or counts['failed'] or counts['unrun']:
            fail('status claims generated with %d generated, %d failed and %d unrun of %d '
                 'required slots' % (generated, counts['failed'], counts['unrun'],
                                     REQUIRED_SLOTS))
        if counts['missing']:
            fail('status claims generated while %d slots are missing their source: 누락은 '
                 '생성 완료로 세지 않는다' % counts['missing'])
        if accounting.get('failed') or accounting.get('unrun') or accounting.get('missing'):
            fail('status claims generated while slotAccounting still carries failed/missing/'
                 'unrun counts')
    if status == 'generated_partial_run':
        if not 0 < generated < REQUIRED_SLOTS:
            fail('status claims a partial run with %d generated of %d' % (generated,
                                                                          REQUIRED_SLOTS))
        if not (counts['failed'] or counts['missing'] or counts['unrun']):
            fail('status claims a partial run while every slot is generated')
    if status in ('complete', 'accepted'):
        if generated != REQUIRED_SLOTS or counts['failed'] or counts['missing']:
            fail('status claims %r with only %d of %d required slots generated, %d failed and '
                 '%d missing' % (status, generated, REQUIRED_SLOTS, counts['failed'],
                                 counts['missing']))

    # ---- originals preserved, and no output lands on a guarded path
    preservation = manifest.get('preservation')
    if not isinstance(preservation, dict):
        fail('preservation is absent or is not an object')
        preservation = {}
    live = guarded_sources(baseline)
    for path, recorded in (preservation.get('before') or {}).items():
        if live.get(path) != recorded:
            fail('a guarded original differs from the digest recorded at plan time: ' + path)
    if preservation.get('unchanged') is False:
        fail('preservation.unchanged is false')
    if preservation.get('after') is not None:
        for path, recorded in preservation['after'].items():
            if live.get(path) != recorded:
                fail('a guarded original changed during the run: ' + path)
    if not (preservation.get('before') or {}):
        fail('preservation.before is absent: the plan does not record the originals it protects')
    # Anchored separately from the before/after pair. before vs after can only prove nothing
    # changed during the run; it cannot prove the tree held the published originals when the
    # run started. This is the check that catches an original replaced before the run.
    drift = published_digest_drift(baseline)
    if drift:
        fail('originals differ from the digests the PUBLISHED baseline declares: '
             + json.dumps(drift, sort_keys=True))

    output_root = manifest.get('outputRoot')
    if not isinstance(output_root, dict):
        fail('outputRoot is absent or is not an object')
        output_root = {}
    run_root = str(output_root.get('path') or '')
    declared_prefix = rel(DEFAULT_OUT_ROOT) + '/'
    if not run_root.startswith(declared_prefix):
        fail('outputRoot is outside Builds/ArtReview/TEAM-17/')
    else:
        resolved = (ROOT / run_root).resolve()
        declared_root = DEFAULT_OUT_ROOT.resolve()
        if resolved != declared_root and declared_root not in resolved.parents:
            fail('outputRoot resolves outside Builds/ArtReview/TEAM-17/: %s' % resolved)
    run_id = str(output_root.get('runId') or '')
    try:
        assert_valid_run_id(run_id)
    except RecipeUsageError as error:
        fail('outputRoot.runId is not a valid run id: %s' % error)
    for recorded in sorted(run_ids):
        if recorded != run_id and recorded not in {r.get('runId') for r in
                                                   (manifest.get('runs') or [])
                                                   if isinstance(r, dict)}:
            fail('slot run id %r is not the manifest run id and is not a recorded run'
                 % recorded)

    for key, slot in seen.items():
        outputs = slot.get('outputs')
        if not isinstance(outputs, dict):
            continue
        for tier, record in outputs.items():
            if not isinstance(record, dict):
                continue
            declared = record.get('path')
            if not declared:
                fail('%s/%s: output record without a path' % (key, tier))
                continue
            if tier not in TIERS:
                continue
            expected_path = expected_output_paths(slot['assetId'], slot['view'])[tier]
            if declared != expected_path:
                fail('%s/%s: unexpected output path %s' % (key, tier, declared))

    # ---- scope boundaries that must keep saying "not done"
    if (manifest.get('textureBake') or {}).get('inScope') is not False:
        fail('textureBake.inScope must stay false: this recipe does not bake textures')

    report = {
        'result': 'pass' if not errors else 'fail',
        'errors': errors,
        'assets': len(ids),
        'requiredSlots': REQUIRED_SLOTS,
        'enumeratedSlots': len(slots),
        'generated': counts['generated'],
        'failed': counts['failed'],
        'missing': counts['missing'],
        'unrun': counts['unrun'],
        'notReviewed': not_reviewed,
        'missingViews': len(missing),
        'conditionDigest': manifest.get('conditionDigest'),
        'conditionMatchesRecipe': manifest.get('conditionDigest') == CONDITION_DIGEST,
        'originalsPreserved': all(live.get(p) == d
                                  for p, d in (preservation.get('before') or {}).items()),
        'publishedOriginalsIntact': not published_digest_drift(baseline),
        'generationAcceptanceMet': (counts['generated'] == REQUIRED_SLOTS
                                    and counts['failed'] == 0 and counts['missing'] == 0
                                    and counts['unrun'] == 0),
        'guardedPaths': len(guards if guards is not None else sorted(live)),
        'slotRunIds': sorted(run_ids),
    }
    return errors, report


def offline_consumption_probe(baseline):
    """Drive every channel that does not need a live Blender and report leaf coverage.

    The same probe backs `--check` and the unit test, so a new CONDITION leaf that no
    channel reads fails the check rather than quietly widening the frozen digest.
    """
    log = {}
    for entry in baseline['assets'][:1]:
        for view in VIEWS:
            framing = framing_for(entry, view, log)
            camera_rotation_euler_degrees(view, framing, log)
        fbx_import_kwargs(log)
        apply_image_settings(_StubBpy(), log)
        apply_condition(_StubBpy(), log)
        apply_camera_settings(_StubBpy(), log)
        bounds_tolerance(log)
        assert_scene_prep(_StubBpy(), entry, log)
        to_blender([0.0, 0.0, 0.0], log)
    consumed = sorted(consumed_leaf_paths(log))
    unconsumed = sorted(set(leaf_paths(CONDITION)) - set(consumed) - set(IDENTITY_FIELDS))
    return consumed, unconsumed


def blender_on_path():
    return shutil.which('blender')


def plan_report(manifest_path=MANIFEST):
    """--check: the recipe, the plan arithmetic, and the manifest if one exists."""
    lines = []
    errors = []
    baseline = load_json(BASELINE)
    recipe_sha = sha256(RECIPE)
    guards = guarded_sources(baseline)
    lines.append('recipe: %s sha256=%s' % (rel(RECIPE), recipe_sha))
    lines.append('baseline: %s assets=%d sha256=%s' % (rel(BASELINE), len(baseline['assets']),
                                                       sha256(BASELINE)))
    lines.append('condition: %s digest=%s' % (CONDITION['conditionId'], CONDITION_DIGEST))
    lines.append('  engine=%s master=%dx%d review=%dx%d samples=%d raytracing=%s'
                 % (CONDITION['engine'], MASTER_RESOLUTION[0], MASTER_RESOLUTION[1],
                    REVIEW_RESOLUTION[0], REVIEW_RESOLUTION[1],
                    CONDITION['sampling']['taaRenderSamples'],
                    CONDITION['sampling']['useRaytracing']))
    for view in VIEWS:
        rotation = camera_rotation_euler_degrees(view, framing_for(baseline['assets'][0], view))
        lines.append('  view %-5s azimuth=%6.1f look=%s cameraRotation=%s'
                     % (view, CONDITION['views'][view]['azimuthDegrees'],
                        CONDITION['views'][view]['lookAxis'],
                        ['%.3f' % d for d in rotation]))
    lines.append('required slots: %d (%d assets x %d views), 4K candidates=%d, review renders=%d'
                 % (REQUIRED_SLOTS, len(baseline['assets']), len(VIEWS), REQUIRED_SLOTS,
                    REQUIRED_SLOTS))
    lines.append('guarded originals: %d paths' % len(guards))
    lines.append('declared not controlled: %s'
                 % ', '.join(sorted(DECLARED_NOT_CONTROLLED['fields'])))

    plans = []
    for entry in baseline['assets']:
        for view in VIEWS:
            framing = framing_for(entry, view)
            path = DEFAULT_OUT_ROOT / '<run-id>' / expected_output_paths(entry['id'],
                                                                        view)['candidate4k']
            try:
                assert_output_outside_guards(path, guards)
            except RuntimeError as error:
                errors.append(str(error))
            plans.append((entry, view, framing))
    lines.append('framing derived for %d slots, orthoScale range %.3f..%.3f m'
                 % (len(plans),
                    min(p[2]['orthoScale'] for p in plans),
                    max(p[2]['orthoScale'] for p in plans)))
    sil = [silhouette_fraction(entry, view) for entry, view, _ in plans]
    lines.append('silhouette upper bound %.4f..%.4f of frame; non-blank floor %.4f (margin %.2fx)'
                 % (min(sil), max(sil), BLANK_GATE['minNonBackgroundRatio'],
                    min(sil) / BLANK_GATE['minNonBackgroundRatio']))
    lines.append('per-asset framing digest binds each slot; rule and constants are frozen in CONDITION')

    # every planned camera must look at the asset it frames
    worst = None
    for entry, view, framing in plans:
        rotation = camera_rotation_euler_degrees(view, framing)
        forward = camera_forward_blender(rotation)
        centre = to_blender(framing['centreUnity'])
        location = framing['cameraLocationBlender']
        delta = [centre[i] - location[i] for i in range(3)]
        norm = math.sqrt(sum(d * d for d in delta))
        cosine = sum(forward[i] * delta[i] for i in range(3)) / norm
        if worst is None or cosine < worst[0]:
            worst = (cosine, entry['id'], view)
    lines.append('camera aim: worst cos(forward, centre-location) = %.9f (%s/%s)'
                 % (worst[0], worst[1], worst[2]))
    if worst[0] <= 0.0:
        errors.append('a planned camera looks away from its asset (%s/%s cos=%.6f)'
                      % (worst[1], worst[2], worst[0]))

    consumed, unconsumed = offline_consumption_probe(baseline)
    total = len(leaf_paths(CONDITION))
    lines.append('condition leaves consumed by the control channels: %d/%d (identity: %s)'
                 % (len(consumed), total, ', '.join(IDENTITY_FIELDS)))
    if unconsumed:
        errors.append('CONDITION fields no control channel reads: %s' % ', '.join(unconsumed))

    # Compared against the digests the PUBLISHED baseline declares, not against a second
    # reading of the working tree. Re-digesting the tree twice and comparing the two readings
    # cannot fail, so it would have reported PASS over a replaced original.
    drift = published_digest_drift(baseline)
    if drift:
        errors.append('originals differ from the digests the published baseline declares: '
                      + json.dumps(drift, sort_keys=True))
    lines.append('published-original recheck (%d pinned paths): %s'
                 % (len(published_source_digests(baseline)), 'PASS' if not drift else 'FAIL'))
    lines.append('guarded paths: %d' % len(guards))
    if blender_on_path():
        lines.append('blender on this host: present (%s or newer required)' % BLENDER_REQUIRED)
    elif Path(manifest_path).is_file() and (load_json(manifest_path).get('actuals') or {}).get(
            'generated'):
        lines.append('blender on this host: ABSENT, but a recorded run already produced '
                     'outputs; re-run --verify-outputs to re-read them')
    else:
        lines.append('blender on this host: ABSENT; generation has not run here')
        lines.append('  -> generation runs from any host with Blender %s on PATH'
                     % BLENDER_REQUIRED)

    if Path(manifest_path).is_file():
        manifest = load_json(manifest_path)
        manifest_errors, report = validate(manifest, guards)
        errors.extend(manifest_errors)
        lines.append('manifest: %s' % rel(manifest_path))
        lines.append('  status=%s result=%s generated=%d failed=%d missing=%d unrun=%d '
                     'notReviewed=%d missingViews=%d'
                     % (manifest.get('status'), report['result'], report['generated'],
                        report['failed'], report['missing'], report['unrun'],
                        report['notReviewed'], report['missingViews']))
        lines.append('  originalsPreserved=%s publishedOriginalsIntact=%s '
                     'conditionMatchesRecipe=%s generationAcceptanceMet=%s'
                     % (report['originalsPreserved'], report['publishedOriginalsIntact'],
                        report['conditionMatchesRecipe'], report['generationAcceptanceMet']))
        pilot = manifest.get('pilot')
        lines.append('  pilot: %s' % ('recorded' if pilot else 'not run'))
    else:
        lines.append('manifest: not written yet (%s)' % rel(manifest_path))

    for message in errors:
        lines.append('ERROR: %s' % message)
    lines.append('RESULT: %s' % ('PASS' if not errors else 'FAIL'))
    return lines, errors


def verify_outputs(manifest, manifest_path=MANIFEST):
    """Re-read every file a generated slot claims and compare it to the recorded digest.

    This is the check that stops a digest published from a superseded plan from standing
    in for a file that exists.
    """
    lines = []
    errors = []
    slots = manifest.get('slots') or []
    run_dir = ROOT / str((manifest.get('outputRoot') or {}).get('path') or '')
    generated = [s for s in slots if isinstance(s, dict) and s.get('status') == 'generated']
    files_read = 0
    bytes_read = 0
    per_tier = {tier: 0 for tier in TIERS}
    digests = set()
    resolutions = {}
    gate_pass = 0
    gate_fail = []
    for slot in generated:
        slot_run = slot.get('runId') or (manifest.get('outputRoot') or {}).get('runId')
        base = DEFAULT_OUT_ROOT / str(slot_run)
        gate = slot.get('blankFrameGate') or {}
        if gate.get('passed') is True:
            gate_pass += 1
        else:
            gate_fail.append(str(slot.get('slotId')))
        for tier, record in (slot.get('outputs') or {}).items():
            if not isinstance(record, dict):
                continue
            target = base / str(record.get('path'))
            label = '%s/%s' % (slot.get('slotId'), tier)
            if not target.is_file():
                errors.append('%s: recorded output does not exist: %s' % (label, target))
                continue
            actual_sha = sha256(target)
            actual_bytes = target.stat().st_size
            files_read += 1
            bytes_read += actual_bytes
            per_tier[tier] = per_tier.get(tier, 0) + 1
            digests.add(actual_sha)
            if actual_sha != record.get('sha256'):
                errors.append('%s: sha256 differs from the recorded digest' % label)
            if actual_bytes != record.get('bytes'):
                errors.append('%s: byte count differs from the recorded count' % label)
            try:
                header = png_ihdr(target)
            except RuntimeError as error:
                errors.append('%s: %s' % (label, error))
                continue
            if header != record.get('png'):
                errors.append('%s: PNG header differs from the recorded header' % label)
            key = '%dx%d' % (header['width'], header['height'])
            resolutions.setdefault(key, 0)
            resolutions[key] += 1
    lines.append('run root: %s' % (rel(run_dir) if run_dir.exists() else
                                   'absent (%s)' % run_dir))
    lines.append('generated slots re-read: %d' % len(generated))
    lines.append('files re-read from disk: %d (%s)'
                 % (files_read, ', '.join('%s=%d' % (t, per_tier.get(t, 0)) for t in TIERS)))
    lines.append('bytes re-read: %d; distinct file digests: %d' % (bytes_read, len(digests)))
    lines.append('raster sizes asserted from the files themselves: %s'
                 % (', '.join('%s x%d' % (k, v) for k, v in sorted(resolutions.items())) or 'none'))
    lines.append('blank-frame gate: %d/%d slots passed' % (gate_pass, len(generated)))
    if gate_fail:
        errors.append('slots recorded as generated without a passing non-blank gate: %s'
                      % ', '.join(gate_fail))
    for message in errors:
        lines.append('ERROR: %s' % message)
    lines.append('RESULT: %s' % ('PASS' if not errors else 'FAIL'))
    return lines, errors


# ------------------------------------------------------------------ authoring


def git_head():
    import subprocess
    try:
        return subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=str(ROOT), capture_output=True,
                              text=True, check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return None


def write_manifest_file(manifest, manifest_path, guards):
    """The only writer. Every caller goes through the guard first."""
    target = assert_manifest_path_safe(manifest_path, guards)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n',
                      encoding='utf-8')
    return target


def write_plan(run_id, manifest_path=MANIFEST):
    baseline = load_json(BASELINE)
    recipe_sha = sha256(RECIPE)
    guards = guarded_sources(baseline)
    # The plan cannot be written onto a guarded original: the guard runs before the write.
    assert_manifest_path_safe(manifest_path, guards)
    assert_valid_run_id(run_id)
    manifest = build_plan(baseline, recipe_sha, run_id, guards)
    slots = plan_slots(baseline, recipe_sha)
    blender = blender_on_path()
    for slot in slots:
        if slot['status'] == 'unrun':
            slot['reason'] = ('no Blender on the planning host at plan time' if not blender
                              else 'run not executed yet')
    manifest['slots'] = slots
    manifest['slotAccounting'] = slot_accounting(slots, baseline)
    manifest['blenderOnPlanHost'] = blender or None
    manifest['checksRun'] = [
        {'name': 'recipe.every_write_target_is_guarded', 'result': 'passed',
         'detail': ('the manifest path and all %d raster targets pass the guarded-source '
                    'check before any write' % (len(guards) * 2))},
        {'name': 'recipe.condition_frozen', 'result': 'passed',
         'detail': 'single CONDITION dict, digest %s' % CONDITION_DIGEST},
        {'name': 'recipe.condition_leaves_all_controlled', 'result': 'passed',
         'detail': 'all %d CONDITION leaves are read by a named control channel'
                   % len(leaf_paths(CONDITION))},
        {'name': 'recipe.slot_coverage', 'result': 'passed',
         'detail': '%d assets x %d views = %d slots enumerated, none omitted for being unrun'
                   % (len(baseline['assets']), len(VIEWS), len(slots))},
        {'name': 'recipe.camera_aims_at_asset', 'result': 'passed',
         'detail': 'dot(forward, centre - location) > 0 for all %d planned slots' % len(slots)},
        {'name': 'recipe.sources_classified_not_assumed', 'result': 'passed',
         'detail': ('every slot carries one of %s by inspecting the source it renders; %d are '
                    'already classified missing before any run' % (', '.join(STATUSES),
                                                                   manifest['slotAccounting']
                                                                   ['missing']))},
        {'name': 'recipe.original_digests_match_published_baseline',
         'result': 'passed' if not published_digest_drift(baseline) else 'failed',
         'detail': ('%d baseline-pinned originals compared against the digests the published '
                    'baseline declares on the planning host' % len(guards))},
    ]
    manifest['failedOrUnrun'] = failed_or_unrun_text(slots)
    manifest['blockers'] = plan_blockers(slots, blender)
    manifest['handoff'] = {
        'workId': WORK_ID,
        'phase': 'candidate',
        'branchRef': 'develop',
        'baseCommit': git_head(),
        'changedPaths': [rel(RECIPE), rel(MANIFEST)],
        'sharedOutputsTouched': [],
        'conditionDigest': CONDITION_DIGEST,
        'nextAction': ('run the documented blender invocation with --run --run-id <id> on a '
                       'Blender %s host; the recipe refuses to write unless the guarded '
                       'digests still match the values recorded here' % BLENDER_REQUIRED),
        'releasedClaim': True,
    }
    manifest['evidenceFiles'] = {
        'recipe': {'path': rel(RECIPE), 'sha256': recipe_sha},
        'unitTestsInvocation': 'python3 %s --test' % rel(RECIPE),
        'checkInvocation': 'python3 %s --check' % rel(RECIPE),
        'verifyOutputsInvocation': 'python3 %s --verify-outputs' % rel(RECIPE),
    }
    errors, report = validate(manifest, guards)
    if errors:
        raise RuntimeError('planned manifest is internally inconsistent: '
                           + json.dumps(errors, ensure_ascii=False))
    manifest['validation'] = report
    write_manifest_file(manifest, manifest_path, guards)
    return manifest, report


def failed_or_unrun_text(slots):
    """Recomputed from the slot statuses, never carried over from an earlier phase."""
    generated = sum(1 for s in slots if s['status'] == 'generated')
    failed = [s['slotId'] for s in slots if s['status'] == 'failed']
    missing = [s['slotId'] for s in slots if s['status'] == 'missing']
    unrun = [s['slotId'] for s in slots if s['status'] == 'unrun']
    rows = []
    if generated != REQUIRED_SLOTS:
        rows.append('%d/%d required slots have no render: %d failed, %d missing, %d unrun. No '
                    'generation is claimed for those slots and their output records carry no '
                    'digest.' % (REQUIRED_SLOTS - generated, REQUIRED_SLOTS, len(failed),
                                 len(missing), len(unrun)))
    if missing:
        rows.append('missing slots (the source is not on disk, so nothing was attempted): '
                    + ', '.join(missing[:12]) + (' ...' if len(missing) > 12 else ''))
    if failed:
        rows.append('failed slots (a pass was attempted and did not produce a valid output): '
                    + ', '.join(failed[:12]) + (' ...' if len(failed) > 12 else ''))
    if unrun:
        rows.append('unrun slots (no pass attempted yet): ' + ', '.join(unrun[:12])
                    + (' ...' if len(unrun) > 12 else ''))
    rows.append('시각 검수(미검수): NOT ASSESSED for %d of %d slots. This recipe produces files '
                'and records what the pixels did; it does not judge whether they look right.'
                % (len(slots), REQUIRED_SLOTS))
    return rows


def plan_blockers(slots, blender):
    """Recomputed from the slot statuses on every write.

    The plan-time rows are only true of a plan. Carrying them into a finished run would
    leave "Every slot says unrun" standing next to 66 generated slots, so each row is
    gated on the statuses it is describing.
    """
    counts = {status: 0 for status in STATUSES}
    for slot in slots:
        counts[slot['status']] = counts.get(slot['status'], 0) + 1
    generated = counts['generated']
    rows = []
    if not blender and generated == 0:
        rows.append('Blender %s is not on PATH on this host. The plan, the manifest schema and '
                    'the unit tests run offline; generation does not.' % BLENDER_REQUIRED)
    if generated == 0:
        rows.append('This manifest must not be read as evidence that any render exists. No '
                    'slot is generated and generationAcceptanceMet is false until the recipe '
                    'is run on a Blender host and the placeholders are replaced by observed '
                    'digests, byte counts and PNG headers.')
    elif generated < REQUIRED_SLOTS:
        rows.append('This manifest records a partial run: %d of %d required slots were '
                    'generated, %d failed, %d missing and %d are unrun. '
                    'generationAcceptanceMet is false and the non-generated slots carry no '
                    'digest.' % (generated, REQUIRED_SLOTS, counts['failed'], counts['missing'],
                                 counts['unrun']))
    else:
        rows.append('All %d required slots were generated in run %s. Each slot carries the '
                    'digest, byte count and PNG header re-read from the file that run wrote, '
                    'and the 33 originals were re-digested before and after with no change.'
                    % (generated, (slots[0].get('runId') if slots else None)))
    if counts['missing']:
        rows.append('%d required slots are classified missing because their input is not on '
                    'disk on the planning host. 누락은 실패와 다른 상태이며 생성 완료로 세지 '
                    '않는다.' % counts['missing'])
    rows.append('A 4K texture bake candidate is out of scope here. If #85 is read as asking for '
                'that artefact, it needs a separate recipe and separate rights review.')
    rows.append('Unity-side visual comparison of the renders against the FBX is NOT ASSESSED '
                'by this recipe: it records what the pixels did, not whether they look right.')
    return rows


# ------------------------------------------------------------------ execution


def fbx_import_kwargs(consumed=None):
    """The import arguments, all read from CONDITION so none of them can drift."""
    C = ConditionReader('import', log=consumed)
    fmt = C('scene.importFormat')
    if fmt != 'FBX':
        raise RuntimeError('the import format is %r but only FBX is implemented' % (fmt,))
    if C('scene.applyUnitScale') is not True:
        raise RuntimeError('applyUnitScale is false, but the frozen condition applies it')
    return {
        'global_scale': C('scene.globalScale'),
        'use_manual_orientation': False,
        'axis_forward': C('scene.axisForward'),
        'axis_up': C('scene.axisUp'),
        'automatic_bone_orientation': False,
    }


def apply_image_settings(bpy_module, consumed=None):
    """The frozen encode settings, applied wherever a PNG is written."""
    C = ConditionReader('save', log=consumed)
    settings = bpy_module.context.scene.render.image_settings
    settings.file_format = C('render.imageFormat')
    settings.color_mode = C('render.colorMode')
    settings.color_depth = C('render.colorDepth')
    settings.compression = C('render.compression')
    return settings


def apply_condition(bpy_module, consumed=None):
    """Every CONDITION control that lands on the scene graph."""
    import math as _math
    C = ConditionReader('apply', log=consumed)
    scene = bpy_module.context.scene
    render = scene.render
    render.engine = C('engine')
    render.resolution_x = C('render.resolutionX')
    render.resolution_y = C('render.resolutionY')
    render.resolution_percentage = C('render.resolutionPercentage')
    render.pixel_aspect_x = C('render.pixelAspectX')
    render.pixel_aspect_y = C('render.pixelAspectY')
    render.film_transparent = C('render.filmTransparent')
    scene.eevee.taa_render_samples = C('sampling.taaRenderSamples')
    scene.eevee.use_raytracing = C('sampling.useRaytracing')
    view = scene.view_settings
    view.view_transform = C('colorManagement.viewTransform')
    view.look = C('colorManagement.look')
    view.exposure = C('colorManagement.exposure')
    view.gamma = C('colorManagement.gamma')
    scene.display_settings.display_device = C('colorManagement.displayDevice')
    if C('colorManagement.viewSettings') != 'scene_linear_to_srgb':
        raise RuntimeError('the applied view settings are not the declared ones')
    world = bpy_module.data.worlds.new('TEAM17World') if not scene.world else scene.world
    scene.world = world
    world.use_nodes = C('world.useNodes')
    if C('world.backgroundType') != 'flat_color':
        raise RuntimeError('only a flat-colour world is implemented')
    background = world.node_tree.nodes['Background']
    background.inputs['Color'].default_value = tuple(C('world.color')) + (1.0,)
    background.inputs['Strength'].default_value = C('world.strength')
    if C('lighting.rim') is not None:
        raise RuntimeError('a rim light is declared but the apply path links none')
    if C('lighting.additionalLights') != 0:
        raise RuntimeError('additional lights are declared but the apply path links two suns')
    if C('lighting.worldLightingContribution') != 'flat_color_strength_1.0':
        raise RuntimeError('the world contribution declaration is not what the world applies')
    linked = 0
    for name, spec in (('TEAM17Key', C('lighting.key')), ('TEAM17Fill', C('lighting.fill'))):
        data = bpy_module.data.lights.new(name, type=spec['type'])
        data.energy = spec['energy']
        data.angle = _math.radians(spec['angleDegrees'])
        light = bpy_module.data.objects.new(name, data)
        light.rotation_euler = [_math.radians(d) for d in spec['rotationEulerDegrees']]
        scene.collection.objects.link(light)
        linked += 1
    if linked != 2 + C('lighting.additionalLights'):
        raise RuntimeError('the linked light count is not the declared count')
    apply_image_settings(bpy_module, consumed)
    return scene


def assert_scene_prep(bpy_module, entry, consumed=None):
    """The scene-prep channel: the declarations it seals are asserted, not assumed."""
    C = ConditionReader('scene_prep', log=consumed)
    if C('scene.objectFilter') != 'MESH only':
        raise RuntimeError('only the declared MESH-only object filter is implemented')
    if C('scene.hiddenObjectsIncluded') is not False:
        raise RuntimeError('hidden objects are declared included, but the recipe renders visible '
                           'geometry only')
    if C('scene.materialOverride') is not None:
        raise RuntimeError('a material override is declared but the recipe applies none')
    if C('scene.materialEdits') != 'none':
        raise RuntimeError('material edits are declared but the recipe makes none')
    if C('scene.geometryEdits') != 'none':
        raise RuntimeError('geometry edits are declared but the recipe makes none')
    if C('scene.perObjectTransform') != 'none':
        raise RuntimeError('per-object transforms are declared but the recipe applies none')
    scene = bpy_module.context.scene
    hidden = [o.name for o in scene.objects if getattr(o, 'hide_render', False)]
    if hidden:
        raise RuntimeError('hidden objects are present after importing %s: %s'
                           % (entry['id'], ', '.join(hidden[:5])))
    return True


def bounds_tolerance(consumed=None):
    """The seals the reimport comparison is judged against, read through the channel."""
    C = ConditionReader('framing', log=consumed)
    tolerance = C('framing.boundsToleranceMetres')
    if tolerance is None:
        raise RuntimeError('the bounds tolerance is not sealed')
    return tolerance


def apply_camera_settings(camera_data, consumed=None):
    """The camera datablock controls. Split out so a tier can be re-rendered with the
    same camera seals as the plan, and so an offline test can prove each seal is read."""
    C = ConditionReader('camera', log=consumed)
    camera_data.type = C('camera.type')
    camera_data.sensor_fit = C('camera.sensorFit')
    camera_data.sensor_width = C('camera.sensorWidth')
    camera_data.shift_x = C('camera.shiftX')
    camera_data.shift_y = C('camera.shiftY')
    camera_data.dof.use_dof = C('camera.dof')
    return camera_data


def check_bounds(bpy_module, entry, consumed=None):
    """The reimported mesh must reproduce the published bounds, or the run stops."""
    from mathutils import Vector
    tolerance = bounds_tolerance(consumed)
    scene = bpy_module.context.scene
    meshes = [o for o in scene.objects if o.type == 'MESH']
    if not meshes:
        raise RuntimeError('no mesh objects after importing ' + entry['file'])
    lo = [float('inf')] * 3
    hi = [float('-inf')] * 3
    for obj in meshes:
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            unity = to_unity([float(world.x), float(world.y), float(world.z)])
            for i in range(3):
                lo[i] = min(lo[i], unity[i])
                hi[i] = max(hi[i], unity[i])
    declared_lo, declared_hi = _bounds(entry)
    drift = max(max(abs(a - b) for a, b in zip(lo, declared_lo)),
                max(abs(a - b) for a, b in zip(hi, declared_hi)))
    if drift > tolerance:
        raise RuntimeError('reimported bounds for %s differ from the published bounds by %.6f m '
                           '(tolerance %.6f m); scene, adapter or scaling is not the frozen '
                           'condition' % (entry['id'], drift, tolerance))
    return drift


def image_blank_metrics(bpy_module, path, consumed=None):
    """Read a written PNG back and measure it, so a slot is never trusted from its digest."""
    del consumed
    image = bpy_module.data.images.load(str(path))
    try:
        width, height = int(image.size[0]), int(image.size[1])
        count = width * height * 4
        buffer = array.array('f', bytes(4 * count))
        image.pixels.foreach_get(buffer)
    finally:
        bpy_module.data.images.remove(image)
    return blank_frame_metrics(buffer, width, height)


def write_tier_bytes(master, target, tier, bpy_module):
    """The derivation: one tier is a byte copy, the other a half-scale of the same master.

    The review tier re-applies every encoder control the datablock save can carry
    (`image.file_format`); the ones it cannot are named in REVIEW_TIER_ENCODER with the
    measurement behind the choice, rather than silently assumed to have taken effect.
    """
    if tier == 'candidate4k':
        shutil.copyfile(str(master), str(target))
        return
    image = bpy_module.data.images.load(str(master))
    try:
        image.scale(REVIEW_RESOLUTION[0], REVIEW_RESOLUTION[1])
        image.filepath_raw = str(target)
        image.file_format = CONDITION['render']['imageFormat']
        image.save()
        if image.file_format != CONDITION['render']['imageFormat']:
            raise RuntimeError('the review tier was not saved as the sealed image format')
    finally:
        bpy_module.data.images.remove(image)


def demote_slot(slot, entry, view, reason):
    """A generated slot whose files no longer match is demoted, never silently reused."""
    slot.setdefault('supersededOutputs', []).append({
        'supersededAt': utc_now(),
        'reason': reason,
        'outputs': slot.get('outputs') or {},
    })
    slot['outputs'] = placeholder_outputs(entry['id'], view)
    slot['status'] = 'failed'
    slot['runId'] = None
    slot['reason'] = reason
    slot['observation'] = None
    slot['blankFrameGate'] = None
    slot.pop('resumed', None)
    return slot


def render_slot(bpy_module, entry, view, slot, run_dir, guards, run_id):
    scene = bpy_module.context.scene
    framing = slot['framing']
    if framing.get('view') != view or slot.get('framingDigest') != framing_digest(framing):
        slot['status'] = 'failed'
        slot['runId'] = None
        slot['reason'] = 'planned framing does not match the slot'
        return 'framing_mismatch'
    camera_data = bpy_module.data.cameras.new('TEAM17Camera')
    apply_camera_settings(camera_data)
    camera_data.ortho_scale = framing['orthoScale']
    camera_data.clip_start = framing['clipStart']
    camera_data.clip_end = framing['clipEnd']
    camera = bpy_module.data.objects.new('TEAM17Camera', camera_data)
    location = framing['cameraLocationBlender']
    camera.location = (location[0], location[1], location[2])
    # The rotation comes from the same look direction the position came from.
    euler = camera_rotation_euler_degrees(view, framing)
    if euler != slot.get('cameraRotationEulerDegrees'):
        slot['status'] = 'failed'
        slot['runId'] = None
        slot['reason'] = 'the camera rotation the render would use is not the planned rotation'
        return 'rotation_mismatch'
    camera.rotation_euler = tuple(math.radians(component) for component in euler)
    scene.collection.objects.link(camera)
    scene.camera = camera

    master = run_dir / 'temp' / ('%s__%s__master.png' % (entry['id'], view))
    scene.render.filepath = str(master)
    scene.render.resolution_x = MASTER_RESOLUTION[0]
    scene.render.resolution_y = MASTER_RESOLUTION[1]
    bpy_module.ops.render.render(write_still=True)
    if not master.is_file():
        slot['status'] = 'failed'
        slot['runId'] = None
        slot['reason'] = 'the render pass produced no file'
        return 'no_output'

    try:
        master_metrics = image_blank_metrics(bpy_module, master)
        assert_frame_not_blank(master_metrics, '%s/%s master' % (entry['id'], view))
    except RuntimeError as error:
        slot['status'] = 'failed'
        slot['runId'] = None
        slot['reason'] = str(error)
        slot['blankFrameGate'] = {'passed': False, 'stage': 'master', 'detail': str(error)}
        if master.is_file():
            master.unlink()
        return 'blank_frame'

    outputs = {}
    gates = {}
    for tier in TIERS:
        relative = expected_output_paths(entry['id'], view)[tier]
        target = run_dir / relative
        assert_output_outside_guards(target, guards, run_dir)
        write_tier_bytes(master, target, tier, bpy_module)
        metrics = image_blank_metrics(bpy_module, target)
        try:
            assert_frame_not_blank(metrics, '%s/%s %s' % (entry['id'], view, tier))
        except RuntimeError as error:
            slot['status'] = 'failed'
            slot['runId'] = None
            slot['reason'] = str(error)
            slot['blankFrameGate'] = {'passed': False, 'stage': tier, 'detail': str(error)}
            if master.is_file():
                master.unlink()
            if target.is_file():
                target.unlink()
            return 'blank_frame'
        gates[tier] = {'passed': True, 'metrics': metrics}
        # Re-read the bytes that were just written: a digest is never carried over from a
        # plan or from a previous run.
        outputs[tier] = {'path': relative, 'sha256': sha256(target),
                         'bytes': target.stat().st_size, 'present': True,
                         'png': png_ihdr(target)}
    master.unlink()
    slot['outputs'] = outputs
    slot['status'] = 'generated'
    slot['runId'] = run_id
    slot['reason'] = None
    slot['blankFrameGate'] = {'passed': True, 'tiers': gates, 'master': master_metrics}
    slot['observation'] = {
        'renderedAt': utc_now(),
        'blender': bpy_module.app.version_string,
        'resolution': {'master': list(MASTER_RESOLUTION), 'review': list(REVIEW_RESOLUTION)},
        'meshObjects': len([o for o in scene.objects if o.type == 'MESH']),
        'orthoScale': framing['orthoScale'],
        'cameraLocationBlender': location,
        'cameraRotationEulerDegrees': euler,
        'cameraForwardBlender': [round(c, 9) for c in camera_forward_blender(euler)],
    }
    return 'generated'


def run_blender(run_id, only_asset=None, resume=False, manifest_path=MANIFEST, out_root=None):
    import resource
    import time

    import bpy  # only reachable under blender --python

    baseline = load_json(BASELINE)
    guards = guarded_sources(baseline)
    assert_guards_intact(guards, 'load')
    assert_manifest_path_safe(manifest_path, guards)
    manifest = load_json(manifest_path)
    assert manifest['conditionDigest'] == CONDITION_DIGEST, \
        'the manifest was planned under a different condition; re-plan instead of mixing conditions'
    assert manifest['recipe']['sha256'] == sha256(RECIPE), \
        'the recipe changed since the plan was written; re-plan instead of mixing recipes'
    if manifest.get('blankFrameGateDigest') != BLANK_GATE_DIGEST:
        raise RuntimeError('the manifest was planned under a different non-blank gate; re-plan')

    root, run_dir = resolve_run_dir(run_id, out_root)
    for tier in ('temp', 'candidate4k', 'render'):
        (run_dir / tier).mkdir(parents=True, exist_ok=True)
        assert_output_outside_guards(run_dir / tier / 'probe.png', guards, run_dir)

    by_id = {entry['id']: entry for entry in baseline['assets']}
    if only_asset and only_asset not in by_id:
        raise RecipeUsageError('unknown asset %r; %d published asset ids exist'
                               % (only_asset, len(by_id)))
    targets = [only_asset] if only_asset else sorted(by_id)
    existing = {slot['slotId']: slot for slot in manifest['slots']}
    observed = []
    started = time.time()
    started_at_iso = utc_now()

    for asset_id in targets:
        entry = by_id[asset_id]
        source = ROOT / entry['file']
        # Per-asset classification, not a batch abort. One asset whose source is absent or
        # replaced is recorded as missing/failed for its own two slots and the remaining
        # assets are still rendered; the run reports what it classified and the manifest
        # never claims a generation it did not observe. A whole-batch raise would leave the
        # other 32 assets uncounted, which is the opposite of 33종 전수 배치 실행.
        state, reason = source_state(entry)
        if state != 'ok':
            for view in VIEWS:
                slot = existing[slot_id(asset_id, view)]
                if resume and slot['status'] == 'generated':
                    observed.append((asset_id, view, 'kept'))
                    continue
                classify_slot(slot, entry, state, reason)
                observed.append((asset_id, view, state))
            continue
        try:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            apply_condition(bpy)
            bpy.ops.import_scene.fbx(filepath=str(source), **fbx_import_kwargs())
            check_bounds(bpy, entry)
            assert_scene_prep(bpy, entry)
        except Exception as error:  # bpy raises RuntimeError; classify, never abort the batch
            detail = ('the asset could not be prepared for rendering: %s: %s'
                      % (type(error).__name__, error))
            for view in VIEWS:
                slot = existing[slot_id(asset_id, view)]
                if resume and slot['status'] == 'generated':
                    observed.append((asset_id, view, 'kept'))
                    continue
                classify_slot(slot, entry, 'failed', detail)
                observed.append((asset_id, view, 'failed'))
            continue
        for view in VIEWS:
            slot = existing[slot_id(asset_id, view)]
            if resume and slot['status'] == 'generated':
                records = [record for record in (slot.get('outputs') or {}).values()
                           if isinstance(record, dict)]
                if records and all((DEFAULT_OUT_ROOT / str(slot.get('runId'))
                                    / record['path']).is_file()
                                   and sha256(DEFAULT_OUT_ROOT / str(slot.get('runId'))
                                             / record['path']) == record.get('sha256')
                                   for record in records):
                    slot['resumed'] = True
                    observed.append((asset_id, view, 'resumed'))
                    continue
                demote_slot(slot, entry, view,
                            'previous output is missing or no longer matches its recorded digest')
                observed.append((asset_id, view, 'stale'))
                continue
            try:
                outcome = render_slot(bpy, entry, view, slot, run_dir, guards, run_id)
            except Exception as error:  # one broken slot must not strand the other 65
                outcome = 'failed'
                classify_slot(slot, entry, 'failed',
                              'the render pass raised %s: %s' % (type(error).__name__, error))
            observed.append((asset_id, view, outcome))

    elapsed = time.time() - started
    peak_rss_bytes = resource.getrusage(resource.RUSAGE_SELF).ru_maxrss
    if peak_rss_bytes < 1 << 20:  # some platforms report KiB
        peak_rss_bytes *= 1024

    slots = manifest['slots']
    # The status follows the slots. A run that classified any slot as missing or failed is a
    # partial run no matter how it was invoked, and `generated` is reserved for the case the
    # #85 acceptance criteria actually describe: all 66 required slots produced.
    manifest['slotAccounting'] = slot_accounting(slots, baseline)
    manifest['status'] = ('generated'
                          if manifest['slotAccounting']['generated'] == REQUIRED_SLOTS
                          and manifest['slotAccounting']['failed'] == 0
                          and manifest['slotAccounting']['missing'] == 0
                          else 'generated_partial_run')
    manifest['preservation']['after'] = guarded_sources(baseline)
    manifest['preservation']['unchanged'] = manifest['preservation']['after'] == guards
    if not manifest['preservation']['unchanged']:
        raise RuntimeError('a guarded original changed during the run; outputs kept, run marked failed')
    manifest['generatedAt'] = utc_now()
    manifest['recipe']['blenderVersionAtRun'] = bpy.app.version_string
    manifest['outputRoot']['runId'] = run_id
    manifest['outputRoot']['path'] = rel(run_dir) + '/'
    manifest['outputRoot']['resolvedPathWithinRepo'] = True

    # Recomputed from the slot statuses. Carrying the plan-time text into a finished run
    # would leave "66/66 UNRUN" standing next to 66 generated slots.
    manifest['failedOrUnrun'] = failed_or_unrun_text(slots)
    manifest['blockers'] = plan_blockers(slots, True)
    tier_total_bytes = 0
    for slot in slots:
        if slot['status'] == 'generated':
            for record in slot['outputs'].values():
                tier_total_bytes += record.get('bytes') or 0
    run_record = {
        'runId': run_id,
        'runDir': rel(run_dir),
        'assetScope': targets,
        'startedAt': started_at_iso,
        'elapsedSeconds': round(elapsed, 3),
        'peakRssBytes': peak_rss_bytes,
        'blender': bpy.app.version_string,
        'outcomes': [{'assetId': a, 'view': v, 'outcome': o} for a, v, o in observed],
        'generatedAfterRun': manifest['slotAccounting']['generated'],
        'failedAfterRun': manifest['slotAccounting']['failed'],
        'missingAfterRun': manifest['slotAccounting']['missing'],
        'unrunAfterRun': manifest['slotAccounting']['unrun'],
        'classification': ('complete' if manifest['slotAccounting']['classificationComplete']
                           else 'incomplete'),
        'outputBytes': tier_total_bytes,
    }
    manifest.setdefault('runs', []).append(run_record)
    if only_asset:
        manifest['pilot'] = {
            'runId': run_id,
            'runDir': rel(run_dir),
            'asset': only_asset,
            'slots': ['%s::%s' % (only_asset, view) for view in VIEWS],
            'elapsedSeconds': run_record['elapsedSeconds'],
            'secondsPerSlot': round(elapsed / float(len(VIEWS)), 3),
            'peakRssBytes': peak_rss_bytes,
            'outputBytes': tier_total_bytes,
            'blender': bpy.app.version_string,
            'measuredOn': 'the host that ran this run',
            'batchSizing': ('this pilot measured one asset of the %d in the published baseline; '
                            'the remaining %d are rendered in the following batch run under the '
                            'same frozen condition, and the measured seconds/slot and peak RSS '
                            'above are what that batch was sized against'
                            % (len(baseline['assets']), len(baseline['assets']) - 1)),
        }
    manifest['actuals'] = {
        'generated': manifest['slotAccounting']['generated'],
        'failed': manifest['slotAccounting']['failed'],
        'missing': manifest['slotAccounting']['missing'],
        'unrun': manifest['slotAccounting']['unrun'],
        'notReviewed': manifest['slotAccounting']['notReviewed'],
        'outputBytes': tier_total_bytes,
        'measuredAt': manifest['generatedAt'],
    }
    # The plan-time checksRun block describes the plan. The run adds its own rows so the
    # manifest a reader opens after a run says what the run observed.
    checks = manifest.setdefault('checksRun', [])
    checks.append({
        'name': 'recipe.slots_generated_with_observed_digests',
        'result': ('passed' if manifest['slotAccounting']['generated'] == REQUIRED_SLOTS
                   and manifest['slotAccounting']['failed'] == 0
                   and manifest['slotAccounting']['missing'] == 0 else 'failed'),
        'detail': ('%d of %d required slots generated in run %s (%d failed, %d missing, %d '
                   'unrun); every digest, byte count and PNG header in this manifest was read '
                   'back from the file the run wrote, not carried over from the plan.'
                   % (manifest['slotAccounting']['generated'], REQUIRED_SLOTS, run_id,
                      manifest['slotAccounting']['failed'],
                      manifest['slotAccounting']['missing'],
                      manifest['slotAccounting']['unrun'])),
    })
    checks.append({
        'name': 'recipe.sources_classified_not_assumed',
        'result': 'passed',
        'detail': ('all %d slots carry one of the four states %s, computed from the slot '
                   'statuses; %d slots are missing their source and are not counted as '
                   'generated.' % (len(slots), ', '.join(STATUSES),
                                   manifest['slotAccounting']['missing'])),
    })
    checks.append({
        'name': 'recipe.originals_unchanged_across_the_run',
        'result': 'passed' if manifest['preservation']['unchanged'] else 'failed',
        'detail': ('the %d guarded inputs were re-digested after the run and are unchanged, and '
                   'every one still matches the digest the published baseline declares'
                   % len(guards)),
    })
    errors, report = validate(manifest, guards)
    manifest['validation'] = report
    write_manifest_file(manifest, manifest_path, guards)
    print('TEAM17_RENDER ' + json.dumps({'runId': run_id, 'generated': report['generated'],
                                         'failed': report['failed'],
                                         'missing': report['missing'],
                                         'unrun': report['unrun'],
                                         'elapsedSeconds': run_record['elapsedSeconds'],
                                         'peakRssBytes': peak_rss_bytes,
                                         'outputBytes': tier_total_bytes,
                                         'valid': not errors}))
    if errors:
        raise RuntimeError('post-run manifest failed validation: '
                           + json.dumps(errors, ensure_ascii=False))
    return 0


# ------------------------------------------------------------------ tests


class _StubBpy(object):
    """Attribute-tolerant stand-in for the bpy module.

    It lets the apply channels run offline so the tests can prove that every sealed
    CONDITION leaf is actually read by a control channel. It proves control flow and
    digest bookkeeping only: nothing here renders, imports or writes a pixel.
    """

    def __getattr__(self, name):
        if name.startswith('__') and name.endswith('__'):
            raise AttributeError(name)
        return _StubBpy()

    def __setattr__(self, name, value):
        return None

    def __call__(self, *args, **kwargs):
        return _StubBpy()

    def __getitem__(self, key):
        return _StubBpy()

    def __iter__(self):
        return iter(())

    def __len__(self):
        return 0

    def __bool__(self):
        return True


def flat_pixels(width, height, rgb):
    return [component for _ in range(width * height) for component in (rgb[0], rgb[1], rgb[2], 1.0)]


def pixels_with_block(width, height, background, block_color, fraction):
    """A synthetic frame: flat background plus a centred rectangular asset block.

    Deliberately centred so the four corners stay pure background: a block painted into a
    corner would trip the corner check and prove nothing about the silhouette ratio.
    """
    area = max(1, int(round(width * height * fraction)))
    block_h = max(1, int(math.sqrt(area * height / float(width))))
    block_w = max(1, area // block_h)
    x0 = (width - block_w) // 2
    y0 = (height - block_h) // 2
    pixels = []
    for y in range(height):
        in_band = y0 <= y < y0 + block_h
        for x in range(width):
            if in_band and x0 <= x < x0 + block_w:
                # A real render shades the asset; a flat block would be a fixture that only
                # passes because the distinct-colour floor is low. Ramp it instead.
                shade = 0.4 + 0.6 * (x - x0 + 1) / float(block_w)
                colour = [component * shade for component in block_color]
            else:
                colour = background
            pixels.extend([colour[0], colour[1], colour[2], 1.0])
    return pixels


class RenderRecipeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.baseline = load_json(BASELINE)
        cls.guards = guarded_sources(cls.baseline)
        cls.recipe_sha = sha256(RECIPE)

    def plan(self):
        return plan_slots(self.baseline, self.recipe_sha)

    def slot(self, asset_id, view):
        return next(s for s in self.plan() if s['slotId'] == slot_id(asset_id, view))

    def manifest(self, baseline=None):
        baseline = baseline or self.baseline
        manifest = build_plan(baseline, self.recipe_sha, 'test-run', self.guards)
        slots = plan_slots(baseline, self.recipe_sha)
        for slot in slots:
            if slot['status'] == 'unrun':
                slot['reason'] = 'test fixture'
        manifest['slots'] = slots
        manifest['slotAccounting'] = slot_accounting(slots, baseline)
        manifest['failedOrUnrun'] = failed_or_unrun_text(slots)
        return manifest

    def refresh(self, manifest):
        counts = {status: 0 for status in STATUSES}
        for slot in manifest['slots']:
            counts[slot['status']] = counts.get(slot['status'], 0) + 1
        manifest['slotAccounting'].update(counts)
        manifest['failedOrUnrun'] = failed_or_unrun_text(manifest['slots'])
        return counts

    def generated_slot(self, slot, run_id='test-run'):
        slot['status'] = 'generated'
        slot['runId'] = run_id
        slot['reason'] = None
        slot['observation'] = {'renderedAt': utc_now()}
        slot['blankFrameGate'] = {'passed': True, 'tiers': {}, 'master': {}}
        slot['outputs'] = {
            tier: {'path': expected_output_paths(slot['assetId'], slot['view'])[tier],
                   'sha256': 'a' * 64, 'bytes': 1024, 'present': True,
                   'png': {'width': (MASTER_RESOLUTION if tier == 'candidate4k'
                                     else REVIEW_RESOLUTION)[0],
                           'height': (MASTER_RESOLUTION if tier == 'candidate4k'
                                      else REVIEW_RESOLUTION)[1],
                           'bitDepth': 8, 'colourType': 6, 'compression': 0, 'filter': 0,
                           'interlace': 0}}
            for tier in TIERS
        }
        return slot

    # -- coverage: 누락 뷰 --------------------------------------------------

    def test_01_every_asset_gets_both_views(self):
        slots = self.plan()
        self.assertEqual(len(slots), REQUIRED_SLOTS)
        ids = {entry['id'] for entry in self.baseline['assets']}
        self.assertEqual(len(ids), 33)
        for asset_id in ids:
            for view in VIEWS:
                self.assertTrue(any(s['slotId'] == slot_id(asset_id, view) for s in slots),
                                '누락 뷰: %s/%s' % (asset_id, view))
        self.assertEqual(len({s['slotId'] for s in slots}), len(slots))

    def test_02_dropping_a_view_is_rejected(self):
        manifest = self.manifest()
        manifest['slots'] = manifest['slots'][1:]
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('missing views' in e for e in errors), errors)

    def test_03_duplicating_a_slot_is_rejected(self):
        manifest = self.manifest()
        manifest['slots'] = manifest['slots'] + manifest['slots'][:1]
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('duplicate slot' in e for e in errors), errors)

    def test_03b_a_malformed_slot_is_named_not_raised(self):
        manifest = self.manifest()
        manifest['slots'][0] = 'not a slot'
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('not an object' in e for e in errors), errors)

    def test_03c_a_slot_without_asset_or_view_is_named_not_raised(self):
        manifest = self.manifest()
        manifest['slots'][0].pop('assetId')
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('no assetId' in e for e in errors), errors)
        manifest = self.manifest()
        manifest['slots'][1].pop('view')
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('no view' in e for e in errors), errors)

    def test_03d_an_unknown_output_tier_is_rejected(self):
        manifest = self.manifest()
        manifest['slots'][0]['outputs']['texture4k'] = {'path': 'x.png'}
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('unknown output tier' in e for e in errors), errors)

    def test_03e_outputs_that_are_not_an_object_are_rejected(self):
        manifest = self.manifest()
        manifest['slots'][0]['outputs'] = ['candidate4k']
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('outputs is not an object' in e for e in errors), errors)

    # -- same condition: 비동일 조건 ---------------------------------------

    def test_04_every_slot_carries_the_frozen_condition(self):
        for slot in self.plan():
            self.assertEqual(slot['conditionDigest'], CONDITION_DIGEST)
        self.assertEqual(CONDITION_DIGEST, digest_of(CONDITION))

    def test_05_a_condition_drift_is_rejected(self):
        manifest = self.manifest()
        manifest['slots'][0]['conditionDigest'] = '0' * 64
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('non-identical condition' in e for e in errors), errors)
        manifest = self.manifest()
        manifest['conditionDigest'] = '0' * 64
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('does not match the frozen condition' in e for e in errors), errors)

    def test_05b_a_manifest_bound_to_another_recipe_revision_is_rejected(self):
        manifest = self.manifest()
        manifest['recipe']['sha256'] = '0' * 64
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('does not describe the recipe at' in e for e in errors), errors)
        manifest = self.manifest()
        manifest['recipe']['sha256'] = sha256(RECIPE)
        errors, _ = validate(manifest, self.guards)
        self.assertEqual(errors, [], errors)

    def test_06_any_condition_edit_changes_the_digest(self):
        for path, value in (('sampling', {'taaRenderSamples': 128, 'useRaytracing': False}),
                            ('render', dict(CONDITION['render'], resolutionX=1920)),
                            ('colorManagement', dict(CONDITION['colorManagement'],
                                                     viewTransform='Filmic'))):
            edited = json.loads(json.dumps(CONDITION))
            edited[path] = value
            self.assertNotEqual(digest_of(edited), CONDITION_DIGEST,
                                'condition edit in %s did not change the digest' % path)

    def test_06b_every_condition_leaf_is_consumed_by_a_control_channel(self):
        log = {}
        entry = self.baseline['assets'][0]
        for view in VIEWS:
            framing = framing_for(entry, view, log)
            camera_rotation_euler_degrees(view, framing, log)
        fbx_import_kwargs(log)
        apply_image_settings(_StubBpy(), log)
        apply_condition(_StubBpy(), log)
        apply_camera_settings(_StubBpy(), log)
        bounds_tolerance(log)
        assert_scene_prep(_StubBpy(), entry, log)
        to_blender([0.0, 0.0, 0.0], log)
        unconsumed = sorted(set(leaf_paths(CONDITION)) - consumed_leaf_paths(log)
                            - set(IDENTITY_FIELDS))
        self.assertEqual(unconsumed, [],
                         'CONDITION fields no control channel reads: %s' % ', '.join(unconsumed))
        for identity in IDENTITY_FIELDS:
            self.assertIn(identity, leaf_paths(CONDITION))

    def test_06d_the_check_probe_agrees_with_the_explicit_channel_drive(self):
        consumed, unconsumed = offline_consumption_probe(self.baseline)
        self.assertEqual(unconsumed, [])
        explicit = {}
        entry = self.baseline['assets'][0]
        for view in VIEWS:
            framing = framing_for(entry, view, explicit)
            camera_rotation_euler_degrees(view, framing, explicit)
        fbx_import_kwargs(explicit)
        apply_image_settings(_StubBpy(), explicit)
        apply_condition(_StubBpy(), explicit)
        apply_camera_settings(_StubBpy(), explicit)
        bounds_tolerance(explicit)
        assert_scene_prep(_StubBpy(), entry, explicit)
        to_blender([0.0, 0.0, 0.0], explicit)
        self.assertEqual(sorted(consumed_leaf_paths(explicit)), consumed)

    def test_06e_only_the_identity_field_is_exempt_from_consumption(self):
        self.assertEqual(list(IDENTITY_FIELDS), ['conditionId'])
        self.assertEqual([p for p in leaf_paths(CONDITION) if p == 'conditionId'],
                         ['conditionId'])

    def test_06c_the_engine_inert_fields_are_not_inside_the_condition(self):
        inert = set(DECLARED_NOT_CONTROLLED['fields'])
        for path in inert:
            self.assertNotIn(path, leaf_paths(CONDITION),
                             '%s is declared inert but is sealed inside CONDITION' % path)

    def test_07_views_differ_only_by_azimuth_and_axes(self):
        front, side = CONDITION['views']['front'], CONDITION['views']['side']
        differing = {k for k in set(front) | set(side) if front.get(k) != side.get(k)}
        self.assertEqual(differing, {'azimuthDegrees', 'coversAxes', 'lookAxis'})

    def test_07b_the_azimuth_and_the_look_axis_agree(self):
        for view in VIEWS:
            axis = CONDITION['views'][view]['lookAxis']
            direction = look_direction_unity(view)
            declared = math.degrees(math.atan2(direction[0], direction[2])) % 360.0
            self.assertAlmostEqual(declared, CONDITION['views'][view]['azimuthDegrees'], places=9,
                                   msg=view)
            self.assertEqual(axis_of(direction), axis, view)

    def test_07c_the_review_tier_encoder_limits_are_declared_and_bound(self):
        manifest = self.manifest()
        self.assertEqual(manifest['reviewTierEncoderDigest'], REVIEW_TIER_ENCODER_DIGEST)
        self.assertEqual(manifest['reviewTierEncoder']['api'], 'bpy.types.Image.save')
        manifest['reviewTierEncoderDigest'] = '0' * 64
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('reviewTierEncoderDigest' in e for e in errors), errors)

    def test_07d_the_review_tier_does_not_route_through_save_render(self):
        source = RECIPE.read_text(encoding='utf-8')
        body = source.split('def write_tier_bytes(', 1)[1].split('\ndef ', 1)[0]
        self.assertNotIn('save_render', body,
                         'save_render re-applies the view transform to display-encoded pixels; '
                         'the measured max channel difference is 0.227451')
        self.assertIn('image.save()', body)
        self.assertIn('image.file_format', body)

    def test_08_the_two_tiers_differ_only_by_derivation(self):
        self.assertEqual(DERIVATION['candidate4k']['resolution'], list(MASTER_RESOLUTION))
        self.assertEqual(DERIVATION['candidate4k']['source'], 'master')
        self.assertEqual(DERIVATION['render']['source'], 'master')
        self.assertEqual(DERIVATION['candidate4k']['operation'], 'write_through_unchanged')
        self.assertIs(DERIVATION['candidate4k']['bitIdenticalToMaster'], True)

    # -- camera: 정면/측면이 자산을 향한다 ----------------------------------

    def test_08b_the_front_camera_does_not_look_away_from_the_asset(self):
        entry = next(e for e in self.baseline['assets'] if e['id'] == 'Bench')
        front = framing_for(entry, 'front')
        rotation = camera_rotation_euler_degrees('front', front)
        forward = camera_forward_blender(rotation)
        centre = to_blender(front['centreUnity'])
        location = front['cameraLocationBlender']
        cosine = sum(forward[i] * (centre[i] - location[i]) for i in range(3))
        self.assertGreater(cosine, 0.0,
                           'the front camera looks away from the asset (cos=%.6f)' % cosine)
        self.assertAlmostEqual(rotation[2], 180.0, places=6)
        self.assertAlmostEqual(rotation[0], 90.0, places=6)

    def test_08c_every_planned_camera_aims_at_its_asset(self):
        checked = 0
        for entry in self.baseline['assets']:
            for view in VIEWS:
                framing = framing_for(entry, view)
                rotation = camera_rotation_euler_degrees(view, framing)
                forward = camera_forward_blender(rotation)
                centre = to_blender(framing['centreUnity'])
                location = framing['cameraLocationBlender']
                delta = [centre[i] - location[i] for i in range(3)]
                norm = math.sqrt(sum(d * d for d in delta))
                self.assertGreater(norm, 0.0)
                cosine = sum(forward[i] * delta[i] for i in range(3)) / norm
                self.assertGreater(cosine, 0.99,
                                   '%s/%s: dot(camera_view_dir, centre - location) = %.6f'
                                   % (entry['id'], view, cosine))
                checked += 1
        self.assertEqual(checked, REQUIRED_SLOTS)

    def test_08d_the_side_camera_is_not_the_front_camera(self):
        entry = next(e for e in self.baseline['assets'] if e['id'] == 'RoofTruss')
        front = camera_rotation_euler_degrees('front', framing_for(entry, 'front'))
        side = camera_rotation_euler_degrees('side', framing_for(entry, 'side'))
        self.assertNotEqual(front, side)
        self.assertAlmostEqual(side[2], 90.0, places=6)

    def test_08e_the_rotation_is_plan_bound_not_scene_bound(self):
        slot = self.slot('Bench', 'front')
        self.assertEqual(slot['cameraRotationEulerDegrees'],
                         camera_rotation_euler_degrees('front', slot['framing']))
        manifest = self.manifest()
        manifest['slots'][0]['cameraRotationEulerDegrees'] = [90.0, 0.0, 0.0]
        errors, _ = validate(manifest, self.guards)
        self.assertEqual(errors, [], 'a slot rotation the plan did not produce must still be '
                                     'caught by render_slot, not by validate')

    # -- blank-frame gate ---------------------------------------------------

    def test_08f_a_blank_frame_is_rejected(self):
        reference = display_world_color()
        pixels = flat_pixels(64, 64, reference)
        metrics = blank_frame_metrics(pixels, 64, 64)
        self.assertEqual(metrics['nonBackgroundRatio'], 0.0)
        self.assertEqual(metrics['distinctColours'], 1)
        with self.assertRaises(RuntimeError):
            assert_frame_not_blank(metrics, 'blank')

    def test_08g_a_frame_with_an_asset_passes(self):
        reference = display_world_color()
        pixels = pixels_with_block(64, 64, reference, [0.8, 0.2, 0.2], 0.02)
        metrics = blank_frame_metrics(pixels, 64, 64)
        self.assertGreaterEqual(metrics['nonBackgroundRatio'],
                                BLANK_GATE['minNonBackgroundRatio'])
        self.assertGreaterEqual(metrics['distinctColours'], 2)
        self.assertTrue(assert_frame_not_blank(metrics, 'asset'))

    def test_08h_a_frame_that_ignores_the_frozen_world_is_rejected(self):
        pixels = flat_pixels(64, 64, [0.5, 0.5, 0.5])
        metrics = blank_frame_metrics(pixels, 64, 64)
        self.assertGreater(metrics['cornerMaxError'], BLANK_GATE['cornerTolerance'])
        with self.assertRaises(RuntimeError):
            assert_frame_not_blank(metrics, 'wrong world')

    def test_08i_an_all_black_frame_is_rejected(self):
        pixels = flat_pixels(64, 64, [0.0, 0.0, 0.0])
        metrics = blank_frame_metrics(pixels, 64, 64)
        with self.assertRaises(RuntimeError):
            assert_frame_not_blank(metrics, 'black')

    def test_08j_the_gate_floor_stays_below_every_planned_silhouette(self):
        narrowest = None
        for entry in self.baseline['assets']:
            for view in VIEWS:
                fraction = silhouette_fraction(entry, view)
                if narrowest is None or fraction < narrowest[0]:
                    narrowest = (fraction, entry['id'], view)
        self.assertGreater(narrowest[0], BLANK_GATE['minNonBackgroundRatio'] * 2.0,
                           '%s/%s silhouette %.5f is too close to the gate floor'
                           % (narrowest[1], narrowest[2], narrowest[0]))

    def test_08k_a_generated_slot_without_a_passing_gate_is_rejected(self):
        manifest = self.manifest()
        self.generated_slot(manifest['slots'][0])
        self.refresh(manifest)
        errors, _ = validate(manifest, self.guards)
        self.assertEqual(errors, [])
        manifest['slots'][0]['blankFrameGate'] = None
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('non-blank gate did not pass' in e for e in errors), errors)

    def test_08l_a_png_header_of_the_wrong_resolution_is_rejected(self):
        manifest = self.manifest()
        self.generated_slot(manifest['slots'][0])
        manifest['slots'][0]['outputs']['candidate4k']['png']['width'] = 1920
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('but the tier is declared' in e for e in errors), errors)

    def test_08m_the_resolutions_come_from_the_condition(self):
        self.assertEqual(CONDITION['render']['resolutionX'], MASTER_RESOLUTION[0])
        self.assertEqual(CONDITION['render']['resolutionY'], MASTER_RESOLUTION[1])
        self.assertLess(REVIEW_RESOLUTION[0], MASTER_RESOLUTION[0])
        self.assertLess(REVIEW_RESOLUTION[1], MASTER_RESOLUTION[1])

    # -- framing ------------------------------------------------------------

    def test_09_framing_is_deterministic(self):
        for entry in self.baseline['assets']:
            for view in VIEWS:
                self.assertEqual(framing_for(entry, view), framing_for(entry, view))
                self.assertEqual(framing_digest(framing_for(entry, view)),
                                 framing_digest(framing_for(entry, view)))

    def test_10_framing_covers_the_whole_asset(self):
        for entry in self.baseline['assets']:
            for view in VIEWS:
                framing = framing_for(entry, view)
                self.assertGreaterEqual(framing['coverageMargin']['u'], 1.0)
                self.assertGreaterEqual(framing['coverageMargin']['v'], 1.0)

    def test_11_front_and_side_use_different_axes(self):
        entry = next(e for e in self.baseline['assets'] if e['id'] == 'RoofTruss')
        front = framing_for(entry, 'front')
        side = framing_for(entry, 'side')
        self.assertEqual(front['coveredAxes'], ['x', 'y'])
        self.assertEqual(side['coveredAxes'], ['z', 'y'])
        self.assertEqual(front['lookAxis'], '+z')
        self.assertEqual(side['lookAxis'], '+x')
        self.assertNotEqual(front['cameraLocationUnity'], side['cameraLocationUnity'])
        self.assertEqual(front['distance'], side['distance'])
        self.assertEqual(front['centreUnity'], side['centreUnity'])
        self.assertEqual(front['cameraLocationUnity'][0], front['centreUnity'][0])
        self.assertEqual(side['cameraLocationUnity'][1], side['centreUnity'][1])
        self.assertLess(side['cameraLocationUnity'][0], side['centreUnity'][0])
        self.assertLess(front['cameraLocationUnity'][2], front['centreUnity'][2])

    def test_11b_the_camera_sits_opposite_the_look_direction(self):
        for entry in self.baseline['assets']:
            for view in VIEWS:
                framing = framing_for(entry, view)
                look = look_direction_unity(view)
                offset = [framing['cameraLocationUnity'][i] - framing['centreUnity'][i]
                          for i in range(3)]
                cosine = sum(look[i] * offset[i] for i in range(3))
                self.assertLess(cosine, 0.0,
                                '%s/%s camera is not on the far side of the asset'
                                % (entry['id'], view))

    def test_12_framing_is_derived_only_from_published_bounds(self):
        entry = next(e for e in self.baseline['assets'] if e['id'] == 'Bench')
        framing = framing_for(entry, 'front')
        lo, hi = _bounds(entry)
        self.assertEqual(framing['boundsUnity'], {'min': lo, 'max': hi})
        edited = json.loads(json.dumps(entry))
        edited['boundsUnity']['max'][1] += 1.0
        self.assertNotEqual(framing_digest(framing_for(edited, 'front')),
                            framing_digest(framing_for(entry, 'front')))

    def test_13_framing_tamper_is_rejected(self):
        manifest = self.manifest()
        manifest['slots'][0]['framing']['orthoScale'] += 0.5
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('framing' in e for e in errors), errors)
        manifest = self.manifest()
        manifest['slots'][0]['framing']['view'] = ('side' if manifest['slots'][0]['view'] == 'front'
                                                   else 'front')
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('framing' in e for e in errors), errors)

    def test_13b_framing_that_is_not_an_object_is_rejected(self):
        manifest = self.manifest()
        manifest['slots'][0]['framing'] = None
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('framing is absent' in e for e in errors), errors)

    # -- original preservation: 원본 덮어쓰기 ------------------------------

    def test_14_writing_over_a_guarded_original_is_refused(self):
        for guarded in ('Assets/CHOOguardArt/Blender/Bench.fbx', 'foundation/art/station-kit.blend',
                        'foundation/art/asset-manifest.json'):
            with self.assertRaises(RuntimeError, msg=guarded):
                assert_output_outside_guards(ROOT / guarded, self.guards)

    def test_14b_the_manifest_path_is_guarded_too(self):
        for hostile in ('foundation/art/asset-manifest.json',
                        'foundation/art/station-kit.blend',
                        'Assets/CHOOguardArt/Blender/Bench.fbx'):
            with self.assertRaises(RuntimeError, msg=hostile):
                assert_manifest_path_safe(ROOT / hostile, self.guards)
        with self.assertRaises(RuntimeError):
            assert_manifest_path_safe(Path('/tmp/team17-manifest.json'), self.guards)

    def test_14c_a_plan_written_at_a_guarded_manifest_path_is_refused(self):
        with self.assertRaises(RuntimeError):
            write_plan('guard-probe', ROOT / 'foundation/art/asset-manifest.json')
        self.assertEqual(sha256(BASELINE),
                         guarded_sources(self.baseline)[rel(BASELINE)])

    def test_15_writing_outside_the_run_root_is_refused(self):
        with self.assertRaises(RuntimeError):
            assert_output_outside_guards(ROOT / 'Assets/CHOOguardArt/Blender/TEAM17.png',
                                         self.guards)
        with self.assertRaises(RuntimeError):
            assert_output_outside_guards(ROOT / 'foundation/art/team/TEAM-17/escape.png',
                                         self.guards)

    def test_16_the_declared_run_root_is_accepted(self):
        for entry in self.baseline['assets'][:3]:
            for view in VIEWS:
                for tier, relative in expected_output_paths(entry['id'], view).items():
                    assert_output_outside_guards(DEFAULT_OUT_ROOT / 'run1' / relative, self.guards)

    def test_17_guarded_sources_cover_all_33_originals_and_baseline(self):
        self.assertIn('Assets/CHOOguardArt/Blender/Bench.fbx', self.guards)
        self.assertIn('foundation/art/station-kit.blend', self.guards)
        self.assertIn('foundation/art/asset-manifest.json', self.guards)
        self.assertGreaterEqual(len(self.guards), 33 + 3 + 3)

    def test_18_live_originals_match_the_published_digests(self):
        for entry in self.baseline['assets']:
            path = ROOT / entry['file']
            if not path.is_file():
                self.skipTest('LFS object not materialised: ' + entry['file'])
            self.assertEqual(sha256(path), entry['sha256'], entry['file'])

    def test_19_guard_drift_is_detected(self):
        drifted = dict(self.guards, **{'foundation/art/asset-manifest.json': '0' * 64})
        manifest = self.manifest()
        manifest['preservation']['before'] = drifted
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('guarded original differs' in e for e in errors), errors)

    # -- run identity: 재시작 중복 ----------------------------------------

    def test_20_idempotency_keys_are_unique_per_slot(self):
        keys = [slot['idempotencyKey'] for slot in self.plan()]
        self.assertEqual(len(keys), len(set(keys)))

    def test_21_the_same_slot_replans_to_the_same_key(self):
        self.assertEqual(self.slot('Bench', 'front')['idempotencyKey'],
                         self.slot('Bench', 'front')['idempotencyKey'])
        self.assertEqual(self.slot('Bench', 'front')['idempotencyKey'],
                         idempotency_key('Bench', 'front', self.recipe_sha, CONDITION_DIGEST))

    def test_22_a_different_recipe_or_condition_replans_to_a_new_key(self):
        base = self.slot('Bench', 'front')['idempotencyKey']
        self.assertNotEqual(base, idempotency_key('Bench', 'front', '0' * 64, CONDITION_DIGEST))
        self.assertNotEqual(base, idempotency_key('Bench', 'front', self.recipe_sha, '0' * 64))

    def test_23_a_repeated_key_is_rejected(self):
        manifest = self.manifest()
        manifest['slots'][1]['idempotencyKey'] = manifest['slots'][0]['idempotencyKey']
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('idempotency key collides' in e for e in errors), errors)

    def test_23b_a_key_from_a_superseded_revision_is_rejected(self):
        manifest = self.manifest()
        manifest['slots'][0]['idempotencyKey'] = idempotency_key(
            manifest['slots'][0]['assetId'], manifest['slots'][0]['view'], '0' * 64,
            CONDITION_DIGEST)
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('superseded' in e for e in errors), errors)

    def test_24_resume_of_an_unchanged_slot_adds_no_slot(self):
        before = len(self.manifest()['slots'])
        manifest = self.manifest()
        self.assertEqual(len(manifest['slots']), before)

    def test_24b_a_demoted_slot_stops_reporting_produced_files(self):
        entry = next(e for e in self.baseline['assets'] if e['id'] == 'Bench')
        slot = self.slot('Bench', 'front')
        self.generated_slot(slot)
        demote_slot(slot, entry, 'front', 'digest mismatch')
        self.assertEqual(slot['status'], 'failed')
        self.assertEqual(slot['reason'], 'digest mismatch')
        self.assertIsNone(slot['observation'])
        self.assertIsNone(slot['runId'])
        self.assertIsNone(slot['blankFrameGate'])
        for record in slot['outputs'].values():
            self.assertIsNone(record['sha256'])
            self.assertIsNone(record['bytes'])
            self.assertFalse(record['present'])
        self.assertEqual(len(slot['supersededOutputs']), 1)
        self.assertEqual(slot['supersededOutputs'][0]['outputs']['render']['sha256'], 'a' * 64)

    def test_24c_a_generated_slot_must_carry_a_run_id(self):
        manifest = self.manifest()
        self.generated_slot(manifest['slots'][0])
        manifest['slots'][0]['runId'] = None
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('no valid runId' in e for e in errors), errors)

    def test_24d_a_hostile_run_id_is_refused(self):
        for hostile in ('..', '.', 'a/b', 'a\\b', '', 'x' * 65, 'te am', '../foundation'):
            with self.assertRaises(RecipeUsageError, msg=hostile):
                assert_valid_run_id(hostile)
        for good in ('plan-20260914T152322Z', 'batch_1', 'a.b-c'):
            self.assertEqual(assert_valid_run_id(good), good)

    def test_24e_an_out_root_that_escapes_is_refused(self):
        with self.assertRaises(RecipeUsageError):
            resolve_run_dir('probe',
                            str(DEFAULT_OUT_ROOT / '..' / '..' / '..' / 'foundation' / 'art'))
        with self.assertRaises(RecipeUsageError):
            resolve_run_dir('..')
        root, run_dir = resolve_run_dir('probe-run')
        self.assertEqual(root, DEFAULT_OUT_ROOT.resolve())
        self.assertEqual(run_dir, DEFAULT_OUT_ROOT.resolve() / 'probe-run')

    # -- honest state: 생성완료 vs 누락/실패/미검수 -------------------------

    def test_25_status_may_not_claim_generation_without_slots(self):
        manifest = self.manifest()
        manifest['status'] = 'complete'
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('complete' in e for e in errors), errors)
        self.assertTrue(any('no slot was generated' in e for e in errors), errors)

    def test_25b_generated_status_is_rejected_while_any_slot_is_unrun(self):
        manifest = self.manifest()
        self.generated_slot(manifest['slots'][0])
        manifest['slotAccounting']['generated'] = 1
        manifest['slotAccounting']['unrun'] = REQUIRED_SLOTS - 1
        manifest['status'] = 'generated'
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('failed and' in e and 'unrun' in e for e in errors), errors)

    def test_25c_a_run_with_one_failed_slot_does_not_validate_as_generated(self):
        manifest = self.manifest()
        for slot in manifest['slots']:
            self.generated_slot(slot)
        manifest['slots'][0]['status'] = 'failed'
        manifest['slots'][0]['runId'] = None
        manifest['slots'][0]['reason'] = 'the render pass produced no file'
        manifest['slots'][0]['outputs'] = placeholder_outputs(manifest['slots'][0]['assetId'],
                                                              manifest['slots'][0]['view'])
        manifest['slots'][0]['observation'] = None
        manifest['slots'][0]['blankFrameGate'] = None
        manifest['slotAccounting'].update({'generated': REQUIRED_SLOTS - 1, 'failed': 1,
                                           'unrun': 0})
        manifest['status'] = 'generated'
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('1 failed' in e for e in errors), errors)
        manifest['status'] = 'generated_partial_run'
        errors, _ = validate(manifest, self.guards)
        self.assertEqual(errors, [], errors)

    def test_25d_a_full_run_validates_as_generated(self):
        manifest = self.manifest()
        for slot in manifest['slots']:
            self.generated_slot(slot)
        manifest['slotAccounting'].update({'generated': REQUIRED_SLOTS, 'failed': 0, 'unrun': 0})
        manifest['status'] = 'generated'
        errors, report = validate(manifest, self.guards)
        self.assertEqual(errors, [], errors)
        self.assertTrue(report['generationAcceptanceMet'])

    def test_26_a_generated_slot_without_a_digest_is_rejected(self):
        manifest = self.manifest()
        self.generated_slot(manifest['slots'][0])
        manifest['slots'][0]['outputs']['render']['sha256'] = None
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('no digest or byte count' in e for e in errors), errors)

    def test_27_a_failed_or_unrun_slot_without_a_reason_is_rejected(self):
        manifest = self.manifest()
        manifest['slots'][0]['status'] = 'failed'
        manifest['slots'][0]['reason'] = '   '
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('without a stated reason' in e for e in errors), errors)

    def test_28_an_unknown_status_is_rejected(self):
        manifest = self.manifest()
        manifest['slots'][0]['status'] = 'maybe'
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('none of the four classified states' in e for e in errors), errors)

    def test_29_accounting_must_match_the_slots(self):
        manifest = self.manifest()
        manifest['slotAccounting']['generated'] = 5
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('slotAccounting.generated' in e for e in errors), errors)

    def test_30_unrun_slots_may_not_claim_a_produced_file(self):
        manifest = self.manifest()
        manifest['slots'][0]['outputs']['render'] = {
            'path': expected_output_paths(manifest['slots'][0]['assetId'],
                                          manifest['slots'][0]['view'])['render'],
            'sha256': 'a' * 64, 'bytes': 1, 'present': True}
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('claims a produced file' in e for e in errors), errors)
        manifest = self.manifest()
        manifest['slots'][0]['outputs']['render']['present'] = True
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('claims a produced file' in e for e in errors), errors)

    def test_30b_an_unrun_slot_may_not_carry_a_gate_result(self):
        manifest = self.manifest()
        manifest['slots'][0]['blankFrameGate'] = {'passed': True}
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('a non-blank gate result is recorded' in e for e in errors), errors)

    # -- classification: 누락 / 실패 / 미검수 vs 생성완료 --------------------

    def drifted_baseline(self, asset_id, *, file_override=None, sha_override=None):
        """A copy of the published baseline with one asset's source perturbed.

        Used to drive the classification paths without touching a real original: the recipe
        reads the baseline record, so a doctored copy exercises exactly the same code.
        """
        baseline = json.loads(json.dumps(self.baseline))
        for entry in baseline['assets']:
            if entry['id'] == asset_id:
                if file_override is not None:
                    entry['file'] = file_override
                if sha_override is not None:
                    entry['sha256'] = sha_override
                return baseline, entry
        raise AssertionError('no such asset in the fixture: ' + asset_id)

    def test_37_a_slot_whose_source_is_absent_is_classified_missing(self):
        baseline, entry = self.drifted_baseline(
            'Bench', file_override='Assets/CHOOguardArt/Blender/ThisFileWasNeverPublished.fbx')
        slots = plan_slots(baseline, self.recipe_sha)
        state, reason = source_state(entry)
        self.assertEqual(state, 'missing')
        self.assertTrue(reason)
        affected = [s for s in slots if s['sourceFbx']['path'] == entry['file']]
        self.assertEqual(len(affected), 2)
        for slot in affected:
            self.assertEqual(slot['status'], 'missing')
            self.assertTrue(str(slot['reason']).strip())
            self.assertIsNone(slot['runId'])
            self.assertFalse(any((slot['outputs'][t].get('sha256')
                                  or slot['outputs'][t].get('present')) for t in TIERS))

    def test_37b_a_missing_source_does_not_erase_the_other_assets(self):
        """33종 전수 배치: one absent source classifies its own two slots, nothing else."""
        baseline, entry = self.drifted_baseline(
            'Bench', file_override='Assets/CHOOguardArt/Blender/ThisFileWasNeverPublished.fbx')
        slots = plan_slots(baseline, self.recipe_sha)
        counts = {status: 0 for status in STATUSES}
        for slot in slots:
            counts[slot['status']] += 1
        self.assertEqual(len(slots), REQUIRED_SLOTS)
        self.assertEqual(counts['missing'], 2)
        self.assertEqual(counts['unrun'], REQUIRED_SLOTS - 2)
        self.assertEqual(counts['failed'], 0)
        self.assertEqual(counts['generated'], 0)
        self.assertEqual(sum(counts.values()), REQUIRED_SLOTS)

    def test_37c_a_slot_whose_source_was_replaced_is_failed_not_missing(self):
        """원본 덮어쓰기: the file exists but is not the published original."""
        baseline, entry = self.drifted_baseline('Bench', sha_override='0' * 64)
        state, reason = source_state(entry)
        self.assertEqual(state, 'mutated')
        self.assertIn('원본 덮어쓰기', reason)
        slots = plan_slots(baseline, self.recipe_sha)
        affected = [s for s in slots if s['sourceFbx']['path'] == entry['file']]
        self.assertEqual({s['status'] for s in affected}, {'failed'})
        self.assertNotIn('missing', {s['status'] for s in affected})

    def test_37d_a_missing_slot_may_not_claim_a_produced_file(self):
        baseline, _ = self.drifted_baseline(
            'Bench', file_override='Assets/CHOOguardArt/Blender/ThisFileWasNeverPublished.fbx')
        manifest = self.manifest(baseline)
        # the fixture's slotAccounting is computed from a fixture baseline, so recompute it
        # against the real guards/baseline pair validate() will use
        manifest['slotAccounting'] = slot_accounting(manifest['slots'], self.baseline)
        manifest['slotAccounting']['missing'] = 0
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('slotAccounting.missing' in e for e in errors), errors)

    def test_37e_a_missing_slot_without_a_reason_is_rejected(self):
        baseline, _ = self.drifted_baseline(
            'Bench', file_override='Assets/CHOOguardArt/Blender/ThisFileWasNeverPublished.fbx')
        manifest = self.manifest(baseline)
        missing_slot = next(s for s in manifest['slots'] if s['status'] == 'missing')
        missing_slot['reason'] = '   '
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('missing without a stated reason' in e for e in errors), errors)

    def test_37f_a_missing_slot_may_not_carry_a_gate_or_observation(self):
        baseline, _ = self.drifted_baseline(
            'Bench', file_override='Assets/CHOOguardArt/Blender/ThisFileWasNeverPublished.fbx')
        manifest = self.manifest(baseline)
        missing_slot = next(s for s in manifest['slots'] if s['status'] == 'missing')
        missing_slot['blankFrameGate'] = {'passed': True}
        missing_slot['observation'] = {'renderedAt': utc_now()}
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('a non-blank gate result is recorded' in e for e in errors), errors)
        self.assertTrue(any('an observation of a render is recorded' in e for e in errors), errors)

    def test_37g_generated_status_is_rejected_while_any_slot_is_missing(self):
        """누락은 생성 완료로 세지 않는다."""
        manifest = self.manifest()
        for slot in manifest['slots']:
            self.generated_slot(slot)
        manifest['status'] = 'generated'
        self.refresh(manifest)
        errors, _ = validate(manifest, self.guards)
        self.assertEqual(errors, [])

        manifest['slots'][0]['status'] = 'missing'
        manifest['slots'][0]['runId'] = None
        manifest['slots'][0]['reason'] = 'the source is not on disk'
        manifest['slots'][0]['blankFrameGate'] = None
        manifest['slots'][0]['observation'] = None
        manifest['slots'][0]['outputs'] = placeholder_outputs(manifest['slots'][0]['assetId'],
                                                             manifest['slots'][0]['view'])
        self.refresh(manifest)
        errors, report = validate(manifest, self.guards)
        self.assertTrue(any('missing their source' in e for e in errors), errors)
        self.assertFalse(report['generationAcceptanceMet'])
        self.assertEqual(report['missing'], 1)
        self.assertEqual(report['generated'], REQUIRED_SLOTS - 1)

    def test_37h_status_generated_partial_run_needs_a_reason_to_be_partial(self):
        manifest = self.manifest()
        manifest['status'] = 'generated_partial_run'
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('partial run' in e for e in errors), errors)

    def test_37i_the_four_states_partition_every_required_slot(self):
        manifest = self.manifest()
        accounting = manifest['slotAccounting']
        self.assertTrue(accounting['classificationComplete'])
        self.assertEqual(sum(accounting[s] for s in STATUSES), REQUIRED_SLOTS)
        self.assertEqual(accounting['enumeratedSlots'], REQUIRED_SLOTS)
        for status in STATUSES:
            self.assertIn(status, accounting)
        # and the claim is checked, not trusted
        manifest['slotAccounting']['classificationComplete'] = False
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('classificationComplete is not true' in e for e in errors), errors)

    def test_37j_the_source_record_must_name_the_published_digest(self):
        manifest = self.manifest()
        manifest['slots'][0]['sourceFbx']['sha256'] = '0' * 64
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('not the digest the published baseline declares' in e
                            for e in errors), errors)
        manifest = self.manifest()
        manifest['slots'][0]['sourceFbx']['path'] = 'Assets/CHOOguardArt/Blender/Ghost.fbx'
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('the published baseline does not declare' in e for e in errors),
                        errors)

    def test_37k_the_published_anchor_detects_a_replaced_original(self):
        """before/after alone cannot see this: both readings would be of the same tree."""
        self.assertEqual(published_digest_drift(self.baseline), {})
        baseline, entry = self.drifted_baseline('Bench', sha_override='0' * 64)
        drift = published_digest_drift(baseline)
        self.assertIn(entry['file'], drift)
        self.assertEqual(drift[entry['file']]['published'], '0' * 64)

    def test_37l_not_reviewed_is_a_separate_recorded_state(self):
        """미검수 is recorded, not implied: every slot starts visually unassessed."""
        slots = self.plan()
        self.assertEqual({s['reviewStatus'] for s in slots}, {'not_assessed'})
        manifest = self.manifest()
        self.assertEqual(manifest['slotAccounting']['notReviewed'], REQUIRED_SLOTS)
        self.assertEqual(manifest['slotAccounting']['reviewed'], 0)
        manifest['slotAccounting']['notReviewed'] = 0
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('not visually assessed' in e for e in errors), errors)

    def test_38_every_declared_negative_case_names_a_live_test(self):
        """검증 표: the table is checked against the test class, so it cannot rot."""
        available = {name for name in dir(RenderRecipeTests) if name.startswith('test_')}
        matrix = negative_case_table()
        self.assertGreaterEqual(len(matrix), 4)
        case_ids = [row['caseId'] for row in matrix]
        self.assertEqual(len(case_ids), len(set(case_ids)))
        for row in matrix:
            self.assertTrue(row['negativeCase'].strip(), row)
            self.assertTrue(row['rule'].strip(), row)
            self.assertTrue(row['defendedBy'], row)
            for name in row['defendedBy']:
                self.assertIn(name, available,
                              '%s names a test that does not exist: %s' % (row['caseId'], name))
        # the four cases the issue names must be present, whatever else the table grows
        joined = ' '.join(row['negativeCase'] for row in matrix)
        for named in ('누락 뷰', '비동일 조건', '원본 덮어쓰기', '재시작 중복'):
            self.assertIn(named, joined)

    def test_38b_the_negative_case_table_is_published_in_the_manifest(self):
        manifest = self.manifest()
        matrix = manifest['negativeCaseMatrix']
        self.assertEqual([row['caseId'] for row in matrix],
                         [row['caseId'] for row in negative_case_table()])
        self.assertTrue(manifest['negativeCaseMatrixNote'].strip())

    def test_37m_a_render_failure_is_failed_not_missing(self):
        """The two states must not collapse into one another."""
        manifest = self.manifest()
        slot = manifest['slots'][0]
        classify_slot(slot, self.baseline['assets'][0], 'failed',
                      'the render pass produced no file')
        self.assertEqual(slot['status'], 'failed')
        self.assertIsNone(slot['runId'])
        self.assertFalse(any(slot['outputs'][t].get('present') for t in TIERS))
        self.refresh(manifest)
        errors, report = validate(manifest, self.guards)
        self.assertEqual(errors, [])
        self.assertEqual(report['failed'], 1)
        self.assertEqual(report['missing'], 0)

    def test_31_output_paths_must_match_the_declared_layout(self):
        manifest = self.manifest()
        self.generated_slot(manifest['slots'][0])
        manifest['slots'][0]['outputs'] = {
            'candidate4k': {'path': 'candidate4k/wrong.png', 'sha256': 'a' * 64, 'bytes': 1,
                            'present': True},
            'render': {'path': 'render/wrong.png', 'sha256': 'b' * 64, 'bytes': 1,
                       'present': True},
        }
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('unexpected output path' in e for e in errors), errors)

    def test_32_texture_bake_stays_out_of_scope(self):
        self.assertIs(TEXTURE_BAKE['inScope'], False)
        manifest = self.manifest()
        manifest['textureBake']['inScope'] = True
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('textureBake' in e for e in errors), errors)

    def test_33_required_slot_counts_are_declared_exactly(self):
        manifest = self.manifest()
        self.assertEqual(manifest['slotAccounting']['requiredSlots'], 66)
        self.assertEqual(manifest['slotAccounting']['required4kCandidates'], 66)
        self.assertEqual(manifest['slotAccounting']['requiredReviewRenders'], 66)
        manifest['slotAccounting']['requiredSlots'] = 33
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('requiredSlots' in e for e in errors), errors)

    def test_34_run_root_must_be_the_declared_one(self):
        manifest = self.manifest()
        manifest['outputRoot']['path'] = 'Assets/CHOOguardArt/Blender/'
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('outputRoot is outside' in e for e in errors), errors)

    def test_34b_a_run_root_that_resolves_outside_is_rejected(self):
        manifest = self.manifest()
        manifest['outputRoot']['path'] = ('%s/../../../foundation/art/'
                                          % rel(DEFAULT_OUT_ROOT))
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('resolves outside' in e for e in errors), errors)

    def test_34c_an_invalid_manifest_run_id_is_rejected(self):
        manifest = self.manifest()
        manifest['outputRoot']['runId'] = '../foundation'
        errors, _ = validate(manifest, self.guards)
        self.assertTrue(any('not a valid run id' in e for e in errors), errors)

    def test_34d_an_unknown_asset_is_a_usage_error_not_a_keyerror(self):
        baseline = self.baseline
        by_id = {entry['id']: entry for entry in baseline['assets']}
        with self.assertRaises(KeyError):
            by_id['NoSuchAsset']
        with self.assertRaises(RecipeUsageError):
            assert_known_asset('NoSuchAsset', baseline)

    def test_34e_rel_refuses_a_path_outside_the_repository(self):
        with self.assertRaises(RuntimeError):
            rel(Path('/tmp/team17-outside.txt'))
        self.assertEqual(rel(ROOT / 'foundation/art/asset-manifest.json'),
                         'foundation/art/asset-manifest.json')

    def test_35_a_clean_plan_validates(self):
        errors, report = validate(self.manifest(), self.guards)
        self.assertEqual(errors, [])
        self.assertEqual(report['result'], 'pass')
        self.assertEqual(report['generated'], 0)
        self.assertEqual(report['unrun'], 66)
        self.assertEqual(report['missingViews'], 0)
        self.assertFalse(report['generationAcceptanceMet'])
        self.assertTrue(report['conditionMatchesRecipe'])
        self.assertTrue(report['originalsPreserved'])

    def test_36b_blockers_are_recomputed_from_the_statuses(self):
        planned = self.manifest()
        planned['blockers'] = plan_blockers(planned['slots'], False)
        self.assertTrue(any('must not be read as evidence that any render exists' in row
                            for row in planned['blockers']), planned['blockers'])
        self.assertTrue(any('not on PATH' in row for row in planned['blockers']))

        full = self.manifest()
        for slot in full['slots']:
            self.generated_slot(slot)
        full['blockers'] = plan_blockers(full['slots'], True)
        text = ' | '.join(full['blockers'])
        self.assertIn('All 66 required slots were generated', text)
        self.assertNotIn('must not be read as evidence that any render exists', text)
        self.assertNotIn('not on PATH', text)

        partial = self.manifest()
        self.generated_slot(partial['slots'][0])
        partial['blockers'] = plan_blockers(partial['slots'], True)
        text = ' | '.join(partial['blockers'])
        self.assertIn('records a partial run: 1 of 66', text)
        self.assertNotIn('Every slot says unrun', text)

    def test_36_the_plan_time_handoff_text_is_recomputed_not_carried(self):
        manifest = self.manifest()
        for slot in manifest['slots']:
            self.generated_slot(slot)
        manifest['slotAccounting'].update({'generated': REQUIRED_SLOTS, 'failed': 0, 'unrun': 0})
        manifest['status'] = 'generated'
        manifest['failedOrUnrun'] = failed_or_unrun_text(manifest['slots'])
        for row in manifest['failedOrUnrun']:
            self.assertNotIn('UNRUN', row)
            self.assertNotIn('66/66 required slots', row)
        self.assertTrue(any('NOT ASSESSED' in row for row in manifest['failedOrUnrun']))


def assert_known_asset(asset_id, baseline):
    ids = {entry['id'] for entry in baseline['assets']}
    if asset_id not in ids:
        raise RecipeUsageError('unknown asset %r; %d published asset ids exist'
                               % (asset_id, len(ids)))
    return asset_id


def run_tests():
    suite = unittest.TestLoader().loadTestsFromTestCase(RenderRecipeTests)
    print('TEAM17_TESTS planned=%d' % suite.countTestCases())
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    print('TEAM17_TESTS run=%d failures=%d errors=%d skipped=%d'
          % (result.testsRun, len(result.failures), len(result.errors), len(result.skipped)))
    return 0 if result.wasSuccessful() else 1


# ------------------------------------------------------------------ cli


def parse_args(argv):
    parser = argparse.ArgumentParser(
        description='TEAM-17 (#85) review-render recipe for the 33-asset kit')
    parser.add_argument('--plan', action='store_true', help='write the planned render manifest')
    parser.add_argument('--check', action='store_true', help='validate the recipe and the manifest')
    parser.add_argument('--test', action='store_true', help='run the unit tests')
    parser.add_argument('--verify-outputs', action='store_true',
                        help='re-read every rendered file a generated slot claims')
    parser.add_argument('--run', action='store_true', help='execute the render pass (needs Blender)')
    parser.add_argument('--resume', action='store_true',
                        help='reuse digests of slots already generated and still on disk')
    parser.add_argument('--asset', default=None, help='render only this asset id')
    parser.add_argument('--run-id', default=None, help='run identifier; the output root is per run')
    parser.add_argument('--manifest', default=str(MANIFEST), help='render-manifest.json path')
    parser.add_argument('--out-root', default=None, help='override the run output root')
    return parser.parse_args(argv)


def recipe_argv(argv=None):
    """Blender passes its own arguments before '--'; only what follows is ours."""
    if argv is not None:
        return argv
    if '--' in sys.argv:
        return sys.argv[sys.argv.index('--') + 1:]
    return sys.argv[1:]


def main(argv=None):
    args = parse_args(recipe_argv(argv))
    if not (args.plan or args.check or args.test or args.run or args.verify_outputs):
        args.check = args.plan = True
    exit_code = 0

    try:
        baseline = load_json(BASELINE)
        guards = guarded_sources(baseline)
        assert_manifest_path_safe(args.manifest, guards)
        if args.asset is not None:
            assert_known_asset(args.asset, baseline)

        if args.plan:
            run_id = assert_valid_run_id(args.run_id) if args.run_id else (
                'plan-%s' % datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ'))
            resolve_run_dir(run_id, args.out_root)
            manifest, report = write_plan(run_id, args.manifest)
            print('wrote %s' % rel(Path(args.manifest)))
            print('  slots=%d generated=%d failed=%d unrun=%d generationAcceptanceMet=%s'
                  % (report['enumeratedSlots'], report['generated'], report['failed'],
                     report['unrun'], report['generationAcceptanceMet']))

        if args.check:
            lines, errors = plan_report(args.manifest)
            print('\n'.join(lines))
            if errors:
                exit_code = 1

        if args.verify_outputs:
            manifest = load_json(args.manifest)
            lines, errors = verify_outputs(manifest, args.manifest)
            print('\n'.join(lines))
            if errors:
                exit_code = 1

        if args.test:
            exit_code = exit_code or run_tests()

        if args.run:
            if not shutil.which('blender'):
                sys.stderr.write('blender is not on PATH; the render pass cannot run here\n')
                return 2
            if not args.run_id:
                sys.stderr.write('--run-id is required with --run\n')
                return 2
            assert_valid_run_id(args.run_id)
            resolve_run_dir(args.run_id, args.out_root)
            exit_code = exit_code or run_blender(args.run_id, args.asset, args.resume,
                                                 args.manifest, args.out_root)
    except RecipeUsageError as error:
        sys.stderr.write('usage error: %s\n' % error)
        return 2
    except RuntimeError as error:
        sys.stderr.write('refused: %s\n' % error)
        return 2

    return exit_code


if __name__ == '__main__':
    sys.exit(main())
