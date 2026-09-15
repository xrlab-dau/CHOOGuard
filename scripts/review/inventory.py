#!/usr/bin/env python3
"""Census immutable Git text, export code/context, partition review ranges. Not approval."""
from __future__ import annotations
import argparse
import collections
import hashlib
import json
import pathlib
import re
import subprocess
import zipfile

CODE = {'.cs', '.py', '.js', '.mjs', '.cjs', '.ts', '.tsx', '.jsx', '.sh', '.ps1', '.bat', '.cmd', '.cpp', '.c', '.h', '.hpp', '.rs', '.go', '.shader', '.hlsl', '.cginc'}
CONFIG = {'.json', '.jsonld', '.toml', '.yaml', '.yml', '.asmdef', '.asmref', '.xml', '.csproj', '.props', '.targets'}
DOC = {'.md', '.rst', '.txt', '.csv', '.tsv'}
SERIALIZED = {'.unity', '.prefab', '.asset', '.meta', '.mat', '.anim', '.controller'}
VENDORED = {'.agents', '_bmad', '.specify', 'node_modules', 'vendor', 'third_party'}
PROTECTED = {'private-data', '.env', 'credentials', 'secrets', 'keys', 'Library', 'Temp', 'Obj', 'Logs'}
MAX_BLOB = 16 * 1024 * 1024
MAX_EXPORT = 128 * 1024 * 1024


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def git(root: pathlib.Path, *args: str) -> bytes:
    return subprocess.check_output(['git', '-C', str(root), *args], stderr=subprocess.DEVNULL, timeout=120)


def protected(path: str) -> bool:
    p = pathlib.PurePosixPath(path)
    return any(x in PROTECTED for x in p.parts) or p.name.startswith('.env') or p.suffix.lower() in {'.pem', '.key', '.ulf'}


def category(path: str, text: str | None) -> str:
    p = pathlib.PurePosixPath(path)
    if protected(path): return 'protected_not_read'
    if text is None: return 'binary_or_non_utf8'
    if p.parts[0] in VENDORED: return 'vendored_tooling'
    if p.suffix.lower() in SERIALIZED: return 'serialized_asset'
    if p.suffix.lower() in CODE:
        if any(x.lower() in {'test', 'tests'} for x in p.parts) or p.stem.startswith('test_') or '.test.' in p.name or p.stem.endswith('Tests'):
            return 'test_code'
        return 'source_code'
    if p.suffix.lower() in CONFIG: return 'configuration_or_data'
    if p.suffix.lower() in DOC: return 'documentation'
    return 'other_text'


def domain(path: str) -> str:
    p = path.lower()
    if 'voice' in p or 'livekit' in p or 'pushtotalk' in p: return 'voice'
    if 'crowd' in p or 'evacuee' in p: return 'crowd_evacuation'
    if 'motion' in p or 'geometry' in p or 'motor' in p or 'portal' in p or 'movingframe' in p: return 'geometry_motion'
    if 'authoritative' in p or 'worldcontracts' in p or 'checkpoint' in p or 'journal' in p: return 'authority_persistence'
    if 'network' in p or 'multiplayer' in p: return 'network_integration'
    if 'simulation' in p or 'physics' in p: return 'simulation'
    if p.startswith('reconstruction/'): return 'legacy_reconstruction_readonly'
    if '/editor/' in p or p.startswith('scripts/art/'): return 'editor_art'
    if '/team/' in p or p.startswith('scripts/ci/') or p.startswith('scripts/bootstrap/'): return 'policy_security_ci'
    if p.startswith('scripts/context/'): return 'context_orchestration'
    if 'scenario' in p or 'training' in p or 'demoflow' in p: return 'scenario_training'
    return 'supporting_code'


def census(root: pathlib.Path, ref: str, out: pathlib.Path) -> dict:
    if not re.fullmatch('[0-9a-f]{40}', ref): raise ValueError('immutable_sha_required')
    if git(root, 'cat-file', '-t', ref).strip() != b'commit': raise ValueError('commit_required')
    if out.exists() or out.is_symlink(): raise ValueError('fresh_output_required')
    rows, units, payloads = [], [], []
    total_export = 0
    for entry in git(root, 'ls-tree', '-rlz', ref).split(b'\0'):
        if not entry: continue
        meta, rawpath = entry.split(b'\t', 1)
        mode, kind, blob, size = meta.split()
        path = rawpath.decode('utf-8')
        p = pathlib.PurePosixPath(path)
        if p.is_absolute() or '..' in p.parts or '\\' in path: raise ValueError('unsafe_git_path')
        row = {'path': path, 'blob': blob.decode(), 'bytes': int(size) if size != b'-' else None,
               'category': 'non_regular_not_read', 'lines': None, 'sha256': None, 'exported': False}
        if protected(path): row['category'] = 'protected_not_read'
        elif kind == b'blob' and mode in (b'100644', b'100755'):
            if int(size) > MAX_BLOB:
                row['category'] = 'oversized_not_read'
            else:
                data = git(root, 'cat-file', 'blob', blob.decode())
                if len(data) != int(size): raise ValueError('blob_size_changed')
                text = None
                if b'\0' not in data:
                    try: text = data.decode('utf-8')
                    except UnicodeDecodeError: pass
                row['category'] = category(path, text)
                row['sha256'] = sha(data)
                if text is not None:
                    row['lines'] = data.count(b'\n') + int(bool(data) and not data.endswith(b'\n'))
                if row['category'] in {'source_code', 'test_code', 'configuration_or_data', 'documentation'}:
                    total_export += len(data)
                    if total_export > MAX_EXPORT: raise ValueError('export_limit_exceeded')
                    row['exported'] = True
                    payloads.append((path, data))
                if row['category'] in {'source_code', 'test_code'}:
                    for start in range(1, row['lines'] + 1, 800):
                        units.append({'id': f'RU-{len(units)+1:05d}', 'domain': domain(path), 'path': path,
                                      'blob': row['blob'], 'startLine': start, 'endLine': min(start+799,row['lines']),
                                      'state': 'UNREVIEWED', 'writeLeaseKey': path,
                                      'partitionKind': 'physical_range_requires_symbol_and_dependency_context',
                                      'requirementsState': 'UNMAPPED', 'reviewerReceipt': None})
        rows.append(row)
    out.mkdir(parents=True)
    with zipfile.ZipFile(out/'sources.zip', 'x', compression=zipfile.ZIP_DEFLATED) as z:
        for path, data in payloads:
            info = zipfile.ZipInfo(path, (1980,1,1,0,0,0)); info.compress_type = zipfile.ZIP_DEFLATED
            info.create_system = 3; info.external_attr = 0o100644 << 16
            z.writestr(info, data)
    grouped = {}
    for c in sorted({r['category'] for r in rows}):
        selected = [r for r in rows if r['category']==c]
        grouped[c] = {'files':len(selected), 'measuredLines':sum(r['lines'] or 0 for r in selected),
                      'unmeasuredFiles':sum(r['lines'] is None for r in selected)}
    result = {'schemaVersion':1, 'sourceCommit':ref, 'state':'INVENTORIED_NOT_REVIEWED',
              'lineDefinition':'LF-delimited physical lines; unterminated last line counted; blank/comment lines included',
              'files':rows, 'totals':grouped, 'reviewUnits':len(units),
              'archiveSha256':sha((out/'sources.zip').read_bytes())}
    for name, data in [('inventory.json',result),('review-units.json', {'schemaVersion':1,'sourceCommit':ref,'units':units})]:
        (out/name).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    return result


def main() -> int:
    ap=argparse.ArgumentParser(description=__doc__); ap.add_argument('--root',type=pathlib.Path,default=pathlib.Path('.'))
    ap.add_argument('--ref',required=True); ap.add_argument('--output',type=pathlib.Path,required=True)
    args=ap.parse_args()
    try:
        result=census(args.root,args.ref,args.output)
        print(json.dumps({k:result[k] for k in ('sourceCommit','state','totals','reviewUnits','archiveSha256')}))
        return 0
    except (ValueError,OSError,subprocess.SubprocessError):
        print(json.dumps({'state':'BLOCKED','reason':'inventory_input_or_io_failure'})); return 2

if __name__=='__main__': raise SystemExit(main())
