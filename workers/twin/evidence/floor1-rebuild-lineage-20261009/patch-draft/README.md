# Reviewable retained-source patch draft

`source-rebuild-lineage.patch` modifies three source files. `modified/` contains the proposed complete files and `snapshots/` pins the input versions. `migration-receipt.json` contains the only canonical data migration: overlay its `set_fields` (`source_asset`, `source_preprocess`) onto the single LIVE row matching `entry_key`. Do not replace the entire registry. The stale full-registry candidate was removed. This overlay was verified against the current 54-row registry and retains 23 unique sources for the 1F shop root, preserving every other live field and row.

The source replay lane uses the canonical observed spec hash, exact root/folder, all 16 observed ShopInterior elements, their unchanged buildEnvelope:false/collider:true policies, and the four registered retained sources. It runs in both KitBuild and KitVerifyCorrections and restores static state in both finally blocks. It generates original source ceiling/downlights and collision walls; production withholding disables each of the three retained cap renderer subtrees after Batch.Save, including child ShadowCasters.

Strict preprocessing first validates the pinned original raw source YAML hash/complete digest, FixturesOnly baseline YAML hash/complete digest, ordinal authority file hash, and exact 252 removed / 132 retained source indices. After those authority checks, exact reviewed baseline and latest target digests are valid saved inputs. All other inputs must be the exact fresh raw 768-vertex / 384-triangle source. The freshly partitioned clone must match the complete FixturesOnly digest before returning its persistent baseline to the existing guide checks. Extra bones, bindposes or blend shapes are rejected. No topology or channel tolerance was widened.

The common installer DeepClone-preserves prior row metadata before updating downstream guide/packet fields, so Unit4/Unit8 and later layers retain source_preprocess and the raw-source mapping.

Validation completed

- All three modified C# files compile together using bundled Roslyn and current Unity/CHOOGuard references, without launching Unity. `compile.log` has zero errors. The response file suppresses only assembly-version/reference-type warnings caused by compiling replacement types alongside existing editor assemblies.
- `verify_draft.py` verifies the live two-field migration overlay, exact 23-source set, pinned authority hashes, guard order, shared replay setup/restoration, cap withholding order and installer metadata preservation.
- Six stored-channel fixtures pass the static state model: original raw, saved baseline and current guide target are recognized; real UV0 byte mutations of each retain topology but are rejected. This is an offline fixture/model check, not runtime execution of Unity mesh guards.
- No canonical file, asset, scene or receipt was modified; no Unity call was made.

Root gates and practical limits

1. Review/apply code and only the two-field live-row migration together; use sources-manifest.json to detect source drift since the draft snapshots. Preserve current WorldRigid, replacement, packet and layering fields.
2. Compile/import the actual patched editor, then run actual canonical KitVerifyCorrections. Require exactly 23 unique registered source comparisons, no omissions/extras, fresh raw 768 / 384 complete digest equality, exact partition, and each guide acceptance. This branch has not generated those batches.
3. Run saved baseline/target acceptance checks and real in-memory UV/position/normal/tangent/index negative fixtures against the patched registry. Authority file drift must reject even exact saved inputs.
4. Before production installation, verify the three cap renderer subtrees are disabled, fixture collision is the current reviewed guide target, and no duplicate wall collision survives. Source reenactment itself must never become the installed collision.
5. Any future canonical spec edit invalidates its pinned replay hash and requires a new comparison and migration; the draft intentionally fails that condition.

Reproduce static checks with `python3 workers/twin/evidence/floor1-rebuild-lineage-20261009/patch-draft/verify_draft.py`. Compile command: bundled Mono runs bundled Roslyn csc.exe with `-noconfig @.../compile.rsp`. This checks source/type validity only; runtime behavior remains the root gate.
