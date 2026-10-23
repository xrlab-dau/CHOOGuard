"""Freeze a root-registered individual-bank batch; never invokes native tools."""
import argparse
import hashlib
import json
import math
from pathlib import Path

BASE = Path(__file__).resolve().parent
REPO = next(x for x in BASE.parents if (x / 'AGENTS.md').exists())
OWNER = 'classic:/root/r31_waiting_bench_model'
authority_file = BASE.parent / 'ROOT-CONSTRUCTION-AUTHORITY-R31.json'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--scope', required=True)
parser.add_argument('--model', required=True)
parser.add_argument('--name', required=True)
args = parser.parse_args()
authority = json.loads(authority_file.read_text())
elected = [x for x in authority['authorized_placement_packets'] if x['constructor_scope'] == args.scope]
if len(elected) != 1 or authority['author'] != OWNER:
    raise SystemExit('Exact root-elected scope and author are required before freezing.')
elected = elected[0]
pose_file = REPO / elected['path']
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
if sha(pose_file) != elected['sha256']:
    raise SystemExit('Root-elected pose SHA mismatch.')
model_file = (REPO / args.model).resolve()
model_file.relative_to(BASE.parent / 'mesh-packet')
model = json.loads(model_file.read_text())
assembly = 'explicit_count_bank'
if model['assemblies'][assembly]['construction_module_only']:
    raise SystemExit('Cannot place a construction module as a physical bank.')
model_sha = sha(model_file)
if elected['family'].startswith('PRIORITY') and model_sha != authority['mesh_packet_sha256']:
    raise SystemExit('Exact preserved priority model SHA required.')
canonical = lambda v: hashlib.sha256(json.dumps(v, separators=(',', ':'), ensure_ascii=False).encode()).hexdigest()
mesh_sha = {k: canonical(v) for k, v in model['mesh_library'].items()}
material_sha = {k: canonical(v) for k, v in model['materials'].items()}
pose = json.loads(pose_file.read_text())
if 'frozen_individual_source_records' in pose:
    source_banks = pose['frozen_individual_source_records']
    yaw = pose['Unity_yaw_degrees']
elif 'individual_banks' in pose:
    elected_ids = set(elected['bank_ids'])
    source_banks = [x for x in pose['individual_banks'] if x['id'] in elected_ids]
    axis = pose['native_localX_world_xyz']
    yaw = math.degrees(math.atan2(-axis[2], axis[0]))
else:
    source_banks = [{'id': pose['bank'], 'parent_world_xyz': pose['exact_selected_center_world_xyz']}]
    yaw = pose['Unity_rotation_Euler_Y_degrees']
if sorted(x['id'] for x in source_banks) != sorted(elected['bank_ids']):
    raise SystemExit('Exact root-elected bank IDs required; no grid or clone expansion.')
family = 'priority' if elected['family'].startswith('PRIORITY') else 'standard'
if bool(model['parameters_MODEL']['priority_variant']) != (family == 'priority'):
    raise SystemExit('Source family and model variant disagree.')
radians = math.radians(yaw)
banks = []
for record in source_banks:
    banks.append(dict(id=record['id'], family=family,
                      root_name='MAIN2F_R31_PUBLIC_WAITING_BENCH_' + record['id'],
                      parent_path='FPSWorld/Architecture/2F',
                      pose_file=elected['path'], pose_sha256=elected['sha256'],
                      model_file=str(model_file.relative_to(REPO)), model_sha256=model_sha,
                      assembly=assembly, mesh_sha256=mesh_sha, material_sha256=material_sha,
                      world_origin=record['parent_world_xyz'], world_yaw=yaw,
                      localX_world=[math.cos(radians), 0, -math.sin(radians)],
                      localZ_world=[math.sin(radians), 0, math.cos(radians)],
                      source_record=record))
packet = dict(schema='CHOOGUARD_R31_FROZEN_NATIVE_BENCH_BATCH_V1', author=OWNER, scope=args.scope,
              banks=banks, receipt_path=str(BASE.relative_to(REPO) / ('APPLIED-' + args.name + '.json')),
              authority_file=str(authority_file.relative_to(REPO)),
              source_classification='SOURCE_MODEL_NOT_SURVEYED_2026',
              no_existing_object_or_source_asset_change=True)
batch_file = BASE / ('FROZEN-' + args.name + '.json')
batch_file.write_text(json.dumps(packet, ensure_ascii=False, separators=(',', ':')))
code = (BASE / 'WaitingBenchBuilderR31.template.cs').read_text()
code = code.replace('__SCOPE__', args.scope).replace('__BATCH_PATH__', str(batch_file.relative_to(REPO))).replace('__BATCH_SHA__', sha(batch_file))
code_file = BASE / ('WaitingBenchBuilderR31_' + args.name.replace('-', '_') + '.cs')
code_file.write_text(code)
request = dict(thread=OWNER, scope=args.scope, name='R31-PUBLIC-WAITING-BENCH-' + args.name,
               file=str(code_file.relative_to(REPO)), entry='WaitingBenchBuilderR31.Apply', timeout_ms=600000)
request_file = BASE / ('BROKER-REQUEST-' + args.name + '.json')
request_file.write_text(json.dumps(request, indent=2))
manifest = dict(status='FROZEN_ROOT_ELECTED_BATCH_READY_FOR_C180_ONLY',
                batch=str(batch_file.relative_to(REPO)), batch_sha256=sha(batch_file),
                script=str(code_file.relative_to(REPO)), script_sha256=sha(code_file),
                request=str(request_file.relative_to(REPO)), bank_ids=[x['id'] for x in banks],
                original_model_sha256=model_sha, no_native_execution_by_preparer=True)
(BASE / ('PREPARED-' + args.name + '.json')).write_text(json.dumps(manifest, indent=2))
print(json.dumps(manifest))
