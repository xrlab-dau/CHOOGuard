"""Synthetic review integrity API. Caller roles/digests are trusted fixture inputs,
not authenticated OS identities; this is not a production sandbox or review gate.
"""
from dataclasses import dataclass
from functools import wraps
import hashlib
import importlib.util
import json
from pathlib import Path, PurePosixPath
import re
import shutil

REPO = Path(__file__).resolve().parents[3]
RESERVED = {'input', 'execution', 'evidence', 'request.json'}


def module(name, relative):
    spec = importlib.util.spec_from_file_location(name, REPO / relative)
    loaded = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(loaded)
    return loaded


native = module('m107_manifest', 'scripts/dev/native_manifest.py')
boundary = module('m107_boundary', 'scripts/team/M1-02/permission_boundary.py')


class Refused(ValueError):
    pass


def guarded(function):
    @wraps(function)
    def call(*args, **kwargs):
        try:
            return function(*args, **kwargs)
        except Refused:
            raise
        except (OSError, ValueError, KeyError, TypeError):
            raise Refused('cannot_proceed: invalid_or_inaccessible_fixture') from None
    return call


def encoded(value):
    return (json.dumps(value, sort_keys=True, indent=2, ensure_ascii=False) + '\n').encode('utf-8')


def digest(data):
    return hashlib.sha256(data).hexdigest()


def local(path):
    path = Path(path).absolute()
    if path.drive.startswith('\\\\') or any(boundary.is_link(p) for p in (path, *path.parents)):
        raise Refused('cannot_proceed: linked_or_network_path')
    return path.resolve()


def verified(root, record):
    try:
        return native.verify(root, record)
    except (OSError, ValueError):
        raise Refused('cannot_proceed: target_bytes_changed') from None


def canonical(name):
    """The one accepted spelling of a portable relative fixture path.

    Native manifest keys are the caller's literal strings, while the copied files on
    disk carry the normalized relative name. Any spelling whose normalization differs
    from the text itself (a leading './', a '.' segment, a doubled slash) would freeze
    a manifest key that no longer matches the materialized file, so it is refused here
    instead of producing a run that can never be inspected or finalized.
    """
    if not isinstance(name, str) or not name or '\\' in name or ':' in name:
        raise Refused('cannot_proceed: invalid_relative_name')
    relative = PurePosixPath(name)
    if relative.is_absolute() or not relative.parts or '..' in relative.parts or relative.parts[0] == '.git':
        raise Refused('cannot_proceed: invalid_relative_name')
    if relative.as_posix() != name:
        raise Refused('cannot_proceed: non_canonical_relative_name')
    return name


def policy():
    model = boundary.load_model()
    record = json.loads((REPO / 'docs/team/M1-02/permission-boundary.json').read_bytes())
    profile = record['profile']
    if (record['state'] != 'passed' or profile['policyHashRecomputed'] != model['policyHash']
            or profile['fileSha256'] != native.sha256(REPO / 'docs/team/M0-03/execution-profile.json')
            or record['settings']['fileSha256'] != native.sha256(REPO / '.pi/settings.json')
            or model['profile']['policy']['allowlistSha256'] != native.sha256(REPO / 'docs/team/M0-03/allowlist.json')):
        raise Refused('cannot_proceed: supplier_policy_drift')
    return model


def identity(model, author, reviewer):
    for actor in (author, reviewer):
        if (set(actor) != {'sessionId', 'provider', 'model'}
                or any(not isinstance(v, str) or not v.strip() for v in actor.values())
                or '/' in actor['provider'] or '/' in actor['model']):
            raise Refused('cannot_proceed: identity_missing_or_invalid')
    if author['provider'] == reviewer['provider'] or author['sessionId'] == reviewer['sessionId']:
        raise Refused('cannot_proceed: reviewer_not_independent')
    for actor, role, agent in ((author, 'role:authoring-agent', 'worker'), (reviewer, 'role:independent-reviewer', 'reviewer')):
        decision = boundary.decide(model, {'actor': role, 'action': 'select_model', 'agent': agent,
                                          'model': actor['provider'] + '/' + actor['model']}, REPO)
        if decision['decision'] != 'allow':
            raise Refused('cannot_proceed: ' + decision['code'])


@dataclass(frozen=True)
class Run:
    root: Path
    request_hash: str


@guarded
def begin(source, output, names, base, head, author, reviewer, round_number=1):
    source, output = local(source), local(output)
    if output.exists() or output.is_relative_to(source) or source.is_relative_to(output):
        raise Refused('cannot_proceed: output_exists_or_overlaps_target')
    if (type(round_number) is not int or not 1 <= round_number <= 3
            or not all(isinstance(ref, str) and re.fullmatch('[a-f0-9]{40}', ref) for ref in (base, head))):
        raise Refused('cannot_proceed: invalid_round_or_revision')
    names = [canonical(name) for name in names]
    if not names or len(set(names)) != len(names):
        raise Refused('cannot_proceed: empty_or_duplicate_target')
    for name in names:
        local(native.contained_file(source, name))
        if PurePosixPath(name).parts[0] in RESERVED:
            raise Refused('cannot_proceed: protocol_output_is_not_target_input')
    model = policy()
    identity(model, author, reviewer)
    target = native.manifest(source, names, 'synthetic-review-target', baseRef=base, headRef=head)
    request = {'schemaVersion': 1, 'scope': 'synthetic_integrity_fixture', 'round': round_number,
               'author': author, 'reviewer': reviewer, 'target': target,
               'uncommittedFiles': target['files'], 'policyHash': model['policyHash']}
    output.mkdir(parents=True, exist_ok=False)
    for area in ('input', 'execution', 'evidence'):
        (output / area).mkdir()
    for name in names:
        for area in ('input', 'execution'):
            destination = native.contained_file(output / area, name)
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(native.contained_file(source, name), destination)
    for area in ('input', 'execution'):
        verified(output / area, target)
    verified(source, target)
    data = encoded(request)
    with (output / 'request.json').open('xb') as stream:
        stream.write(data)
    return Run(output, digest(data))


def inspect(run):
    local(run.root)
    request_path = local(run.root / 'request.json')
    data = request_path.read_bytes()
    if digest(data) != run.request_hash:
        raise Refused('cannot_proceed: request_digest_changed')
    request = json.loads(data)
    model = policy()
    if request['scope'] != 'synthetic_integrity_fixture' or request['policyHash'] != model['policyHash']:
        raise Refused('cannot_proceed: policy_or_scope_changed')
    identity(model, request['author'], request['reviewer'])
    for area in ('input', 'execution'):
        root = local(run.root / area)
        for entry in root.rglob('*'):
            local(entry)
            if entry.is_file():
                name = entry.relative_to(root).as_posix()
                if name not in request['target']['files'] and not (area == 'execution' and name.startswith('generated/')):
                    raise Refused('cannot_proceed: undeclared_target_file')
        verified(root, request['target'])
    return request


@guarded
def write_generated(run, name, data):
    inspect(run)
    name = canonical(name)
    if not name.startswith('generated/'):
        raise Refused('cannot_proceed: write_outside_generated_scope')
    path = native.contained_file(run.root / 'execution', name)
    local(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        with path.open('xb') as stream:
            stream.write(data)
    except FileExistsError:
        raise Refused('cannot_proceed: generated_output_exists') from None


def validate_result(result):
    if (set(result) != {'outcome', 'actionable', 'notes'}
            or result['outcome'] not in ('approved', 'changes_required', 'cannot_proceed')
            or not isinstance(result['notes'], str) or not isinstance(result['actionable'], list)):
        raise Refused('cannot_proceed: result_schema_mismatch')
    for finding in result['actionable']:
        if (not isinstance(finding, dict) or set(finding) != {'id', 'severity', 'title', 'problem', 'fix', 'locations'}
                or any(not isinstance(finding[k], str) or not finding[k].strip() for k in ('id', 'severity', 'title', 'problem', 'fix'))
                or not isinstance(finding['locations'], list) or not finding['locations']
                or any(not isinstance(p, str) or not p.strip() for p in finding['locations'])):
            raise Refused('cannot_proceed: finding_schema_mismatch')
    if result['outcome'] == 'approved' and result['actionable']:
        raise Refused('cannot_proceed: findings_cannot_be_approved')


@guarded
def finalize(run, actor, result):
    if actor != 'role:independent-reviewer':
        raise Refused('cannot_proceed: writer_cannot_finalize')
    request = inspect(run)
    validate_result(result)
    path = local(run.root / 'evidence/receipt.json')
    receipt = {'schemaVersion': 1, 'scope': 'synthetic_integrity_fixture', 'requestSha256': run.request_hash,
               'targetFilesDigest': request['target']['filesDigest'], 'reviewer': request['reviewer'], 'result': result}
    data = encoded(receipt)
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        with path.open('xb') as stream:
            stream.write(data)
    except FileExistsError:
        raise Refused('cannot_proceed: receipt_already_exists') from None
    return digest(data)


@guarded
def verify_receipt(run, expected_receipt_hash, *, current_source, base, head):
    request = inspect(run)
    source = local(current_source)
    for name in request['target']['files']:
        local(native.contained_file(source, name))
    current = native.manifest(source, request['target']['files'], 'synthetic-review-target', baseRef=base, headRef=head)
    if current != request['target']:
        raise Refused('cannot_proceed: current_writer_target_changed')
    data = local(run.root / 'evidence/receipt.json').read_bytes()
    if digest(data) != expected_receipt_hash:
        raise Refused('cannot_proceed: receipt_digest_changed')
    receipt = json.loads(data)
    if (receipt['requestSha256'] != run.request_hash or receipt['targetFilesDigest'] != request['target']['filesDigest']
            or receipt['reviewer'] != request['reviewer'] or receipt['scope'] != 'synthetic_integrity_fixture'):
        raise Refused('cannot_proceed: receipt_binding_changed')
    validate_result(receipt['result'])
    return receipt
