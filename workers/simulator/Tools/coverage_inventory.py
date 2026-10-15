#!/usr/bin/env python3
"""Read local FBX geometry in Blender, then build conservative semantic coverage.

Source files are read-only. Bounds use the Blender FBX import frame and are never
presented as surveyed world dimensions. Run --scan through Blender before build.
"""
import argparse
import hashlib
import json
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DATA = ROOT / 'workers/simulator/data'
SOURCES = {
    'ktx': 'Assets/ChooGuard/Art/Fps/KTX/ktx-single-10car-left-open.fbx',
    'platforms': 'Assets/ChooGuard/Art/OfficialBusanStation/OfficialBusanStation_PlatformsParking.fbx',
    'platform-details': 'Assets/ChooGuard/Art/OfficialBusanStation/OfficialBusanStation_Layer0PlatformsDetails.fbx',
    'station-points': 'Assets/ChooGuard/Art/Emergency/station-points.json',
}


def source_to_world(point):
    # Verified source FBX import conversion and retained KTXSource quaternion.
    # Blender asset x is reflected by Unity's FBX handedness conversion; unit
    # conversion is already represented in Blender's meter-frame geometry.
    q = (.12476154, .6960133, .6960134, -.12476152)
    norm = math.sqrt(sum(v*v for v in q)); x,y,z,w = [v/norm for v in q]
    rotation = ((1-2*y*y-2*z*z, 2*x*y-2*z*w, 2*x*z+2*y*w),
                (2*x*y+2*z*w, 1-2*x*x-2*z*z, 2*y*z-2*x*w),
                (2*x*z-2*y*w, 2*y*z+2*x*w, 1-2*x*x-2*y*y))
    value = (-point[0], point[1], point[2]); offset = (88.91879, -.73000026, 76.442825)
    return tuple(offset[i]+sum(rotation[i][j]*value[j] for j in range(3)) for i in range(3))


def qualify_car04(physical, consist, sources):
    """Promote only the root-reviewed source leaves/seats; retain all render rows."""
    folder = ROOT / 'workers/simulator/geometry_runtime/car04-source-qualification'
    evidence = {
        'car04-proposal': folder / 'passenger-semantic-proposal.json',
        'car04-source-leaves': folder / 'source-leaf-contract.json',
        'car04-source-seats': folder / 'source-seat-correspondence-packet.json',
        'car04-source-frame': folder / 'source-geometry.json',
        'car04-native-proof': folder / 'unity-source-intercar-v4-local-precision-PASS.json',
    }
    # Pin the independently reviewed acceptance artifact, not an arbitrary PASS file.
    assert digest(evidence['car04-native-proof']) == '70636069729c70d8f51464d03955f779d54ddea0874cb34133c72b26435a507f'
    proposal = json.loads(evidence['car04-proposal'].read_text())
    proof = json.loads(evidence['car04-native-proof'].read_text())
    assert proof['passed'] and not proof['source_assets_modified']
    assert proposal['source_sha256'] == digest(ROOT / SOURCES['ktx'])
    assert len(proposal['seats']) == 44 and len(proposal['cabin_doors']) == 2
    access = dict(proposal)
    access.update(schema='car04-access-v1', status='SOURCE_QUALIFIED_PRODUCTION_CONTRACT', production_activated=True)
    access['frame'] = json.loads(evidence['car04-source-frame'].read_text())['frame']
    access['source_provenance'] = [{'id': key, 'path': str(path.relative_to(ROOT)), 'sha256': digest(path)} for key, path in evidence.items()]
    access['manual_operation_claim'] = 'Cabin doors are manually operable training abstractions. power=1 is a legacy availability marker, not an electrical supply or fault capability; open/close do not require electricity. The .85m lateral animation is approximate.'
    access['occupancy_claim'] = 'Source cushion/backrest correspondence is qualified; aisle approach and abstract occupied/sit/stand state do not prove physical seated pose or collider fit.'
    access_path = DATA / 'car04-access.json'
    write_json(access_path, access)
    sources.extend(access['source_provenance'])
    sources.append({'id': 'car04-access', 'path': str(access_path.relative_to(ROOT)), 'sha256': digest(access_path)})
    doors = [d['id'] for d in access['cabin_doors']]
    car = next(e for e in physical if e['id'] == 'car:4')
    car['state'].update(reachable=1, accessible=1)
    car['operations'] = ['move_to']
    car['links'] = doors
    car['source_refs'].append({'source_id': 'car04-access', 'pointer': '/body_id'})
    car['claim'] = 'Source passenger compartment accessible via reviewed original intercar floors and two source cabin leaves. External door remains blocked; no electrical, thermal, combustible or structural capability inferred.'
    car['operation_claim'] = 'move_to uses the compartment aisle; native routes require the corresponding cabin leaf open.'
    for index, seat in enumerate(access['seats']):
        state = {'occupied': 0, 'accessible': 1, 'reachable': 1, 'seat.yaw': seat['facing_yaw']}
        state.update({'destination.'+axis: seat['aisle_approach'][axis] for axis in ('x', 'y', 'z')})
        physical.append({'id': seat['id'], 'parent_id': 'car:4', 'kind': 'seat', 'mode': 'add',
                         'position': seat['floor_anchor'], 'tags': ['semantic', 'seat', 'place', 'occupiable', 'source-coordinate', 'training-model'],
                         'state': state, 'operations': ['move_to', 'sit', 'stand'], 'links': ['car:4', *doors],
                         'source_refs': [{'source_id': 'car04-access', 'pointer': '/seats/'+str(index)}, *seat['source_refs']],
                         'claim': access['seat_claim'], 'operation_claim': access['occupancy_claim']})
    for index, door in enumerate(access['cabin_doors']):
        physical.append({'id': door['id'], 'parent_id': 'car:4', 'kind': 'door', 'mode': 'add',
                         'position': door['position'], 'tags': ['semantic', 'source-coordinate', 'door', 'training-model'],
                         'state': {'open': 1, 'jammed': 0, 'locked': 0, 'integrity': 1, 'power': 1, 'accessible': 1, 'reachable': 1},
                         'operations': ['move_to', 'open_door', 'close_door'],
                         'links': ['car:4', 'car:'+str(access['access_routes'][index]['neighbor_car'])],
                         'source_refs': [{'source_id': 'car04-access', 'pointer': '/cabin_doors/'+str(index)}],
                         'claim': 'Only the reviewed 276-triangle source leaf subset is articulated; its three combined source meshes remain partially unqualified.',
                         'operation_claim': access['manual_operation_claim']})
    body = next(c for c in consist if c['physical_entity_id'] == 'car:4')
    body['operationally_connected'] = True
    body['reason'] = 'Source-qualified original intercar routes and two manually operable cabin leaves; no public external door:4.'


def derive_car04():
    """Reproduce the existing local top-surface scan on actual read-only FBX."""
    import bpy
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT / SOURCES['ktx']))
    station = json.loads((ROOT / SOURCES['station-points']).read_text())
    source_cars = {c['name']:c for c in station['train']['cars']}
    axis = Vector(tuple(station['train']['axis'][k] for k in ('x','y','z')))
    lateral = Vector((axis.z, 0, -axis.x))
    strip = next(p for p in station['platforms'] if p['id']=='p56')
    platform_center = Vector(tuple((strip['a'][k]+strip['b'][k])/2 for k in ('x','y','z')))
    evidence = {'source_sha256':digest(ROOT / SOURCES['ktx']),
                'coordinate_sha256':digest(ROOT / SOURCES['station-points']),
                'derivation':'Existing StationSurvey.ScanInterior 0.1m top-surface heuristic reproduced in local Blender BVH; source geometry only, not site measurements or validated passenger operations.',
                'omission_reason':'StationSurvey.Train accepts _D L_/_D R_ names only and skips Car04_T4_Door before interior scan.',
                'known_passenger_validation':[], 'candidate':None}
    for car in sorted((o for o in bpy.data.objects if o.type=='EMPTY' and re.match(r'^Car0[1-8]_',o.name)),key=lambda o:o.name):
        meshes=[o for o in car.children_recursive if o.type=='MESH']
        vertices=[]; polygons=[]; world_by_name={}
        for mesh in meshes:
            world=[Vector(source_to_world(mesh.matrix_world @ v.co)) for v in mesh.data.vertices]
            world_by_name[mesh.name]=world
            start=len(vertices);vertices.extend(world)
            polygons.extend(tuple(start+i for i in p.vertices) for p in mesh.data.polygons)
        # Unity Renderer.bounds uses transformed per-mesh local AABB corners,
        # which differs from tightly bounding the transformed vertices.
        corners=[Vector(source_to_world(mesh.matrix_world @ Vector(p))) for mesh in meshes for p in mesh.bound_box]
        bounds_min=Vector(tuple(min(v[i] for v in corners) for i in range(3)))
        bounds_max=Vector(tuple(max(v[i] for v in corners) for i in range(3)))
        center=(bounds_min+bounds_max)*.5; center.y=0
        known=source_cars.get(car.name)
        if known:
            lift=world_by_name[known['door']['lift']]
            opening=Vector(tuple((min(v[i] for v in lift)+max(v[i] for v in lift))/2 for i in range(3)))
        else:
            # Bilateral source door geometry: select the platform-facing side,
            # retain as candidate only until a physical opening/leaf split passes.
            door=[v for name,vs in world_by_name.items() if '_T4_Door_' in name for v in vs]
            projected=[(v.dot(lateral),v.dot(axis),v.y) for v in door]
            middle=(min(p[0] for p in projected)+max(p[0] for p in projected))/2
            sign=1 if platform_center.dot(lateral)>middle else -1
            side=[v for v in door if (v.dot(lateral)-middle)*sign>.8]
            opening=Vector(tuple((min(v[i] for v in side)+max(v[i] for v in side))/2 for i in range(3)))
        outward=lateral*(1 if (platform_center-opening).dot(lateral)>=0 else -1)
        bvh=BVHTree.FromPolygons(vertices,polygons)
        nu,nv,step=261,33,.1
        height=[]
        for i in range(nu):
            row=[]
            for k in range(nv):
                p=center+axis*((i-nu//2)*step)-outward*((k-nv//2)*step);p.y=2.15
                hit=bvh.ray_cast(p,Vector((0,-1,0)),3.2)[0]
                row.append(None if hit is None else hit.y)
            height.append(row)
        samples=sorted(h for row in height for h in row if h is not None and .2<h<.65)
        if len(samples)<50:
            evidence['known_passenger_validation'].append({'car':car.name,'error':'insufficient floor samples','count':len(samples)})
            continue
        floor=samples[len(samples)//2]
        is_seat=lambda h:h is not None and floor+.3<h<floor+.75
        is_back=lambda h:h is not None and h>floor+.8
        rows=[];start=None
        for i in range(nu):
            on=sum(is_seat(h) for h in height[i])>=8
            if on and start is None:start=i
            if not on and start is not None:
                if i-start>=2:rows.append((start,i-1))
                start=None
        merged=[]
        for row in rows:
            if merged and (row[0]-merged[-1][1])*step<.45:merged[-1]=(merged[-1][0],row[1])
            else:merged.append(row)
        rows=merged;blocks_per_row=[];aisle_centers=[]
        for lo,hi in rows:
            mid=(lo+hi)//2;blocks=[];start=None
            for k in range(nv):
                on=is_seat(height[mid][k]) or is_seat(height[max(lo,mid-1)][k]) or is_seat(height[min(hi,mid+1)][k])
                if on and start is None:start=k
                if not on and start is not None:
                    if k-start>=3:blocks.append((start,k-1))
                    start=None
            if start is not None and nv-start>=3:blocks.append((start,nv-1))
            blocks_per_row.append(blocks)
            for first,second in zip(blocks,blocks[1:]):
                if second[0]-first[1]>=4:aisle_centers.append((first[1]+second[0])*.5)
        if len(aisle_centers)<3:
            evidence['known_passenger_validation'].append({'car':car.name,'error':'insufficient aisle samples','count':len(aisle_centers)})
            continue
        aisle_k=sorted(aisle_centers)[len(aisle_centers)//2]
        def world(u,v):
            p=center+axis*u-outward*v;p.y=floor
            return {k:float(p[i]) for i,k in enumerate(('x','y','z'))}
        U=lambda i:(i-nu//2)*step;V=lambda k:(k-nv//2)*step
        door_u=(opening-center).dot(axis)
        first,last=U(rows[0][0]),U(rows[-1][1]);far=first if abs(first-door_u)>abs(last-door_u) else last
        aisle=[world(door_u,V(aisle_k))];direction=1 if far>door_u else -1;u=door_u+direction
        while (far-u)*direction>-.01:
            aisle.append(world(u,V(aisle_k)));u+=direction
        facings=[]
        for (lo,hi),blocks in zip(rows,blocks_per_row):
            probe=(blocks[0][0]+blocks[0][1])//2 if blocks else nv//2
            below=lo-2>=0 and is_back(height[lo-2][probe]);above=hi+2<nu and is_back(height[hi+2][probe])
            facings.append(1 if below and not above else -1 if above and not below else 0)
        majority=1 if sum(facings)>=0 else -1;seats=[];row_number=0
        for (lo,hi),blocks,facing in zip(rows,blocks_per_row,facings):
            if facing!=0 and facing!=majority:continue
            row_number+=1
            for b0,b1 in blocks:
                count=max(1,min(3,round((b1-b0+1)*step/.5)))
                for s in range(count):
                    k=b0+(b1-b0+1)*(s+.5)/count-.5;u=U((lo+hi)*.5)
                    anchor=world(u,V(k));front={key:anchor[key]+float(axis[i])*majority*.46 for i,key in enumerate(('x','y','z'))}
                    seats.append({'id':car.name[3:5]+'-'+str(row_number).zfill(2)+chr(65+sum(t['row']==row_number for t in seats)),
                                  'row':row_number,'anchor':anchor,'front':front,'aisle':world(u+majority*.46,V(aisle_k)),
                                  'yaw':math.degrees(math.atan2(axis.x*majority,axis.z*majority))})
        if known:
            bounds_error=max([abs(bounds_min[i]-known['min'][key]) for i,key in enumerate(('x','y','z'))]+[abs(bounds_max[i]-known['max'][key]) for i,key in enumerate(('x','y','z'))])
            opening_expected=Vector(tuple((known['door']['outside'][key]*.95+known['door']['inside'][key]*.8)/1.75 if key!='y' else opening.y for key in ('x','y','z')))
            by_id={s['id']:s for s in seats};errors=[math.sqrt(sum((s['anchor'][k]-by_id[s['id']]['anchor'][k])**2 for k in ('x','y','z'))) for s in known['seats'] if s['id'] in by_id]
            evidence['known_passenger_validation'].append({'car':car.name,'bounds_max_error':bounds_error,'door_opening_error':float((opening-opening_expected).length),
                                                          'source_seats':len(known['seats']),'derived_seats':len(seats),'matching_ids':len(errors),'max_seat_error':max(errors) if errors else None,
                                                          'floor_error':abs(known['floor']-floor)})
        else:
            evidence['candidate']={'name':car.name,'number':4,'label':'4호차 (source-derived candidate)', 'floor':floor,'reachable':False,
                                   'min':dict(zip(('x','y','z'),map(float,bounds_min))),'max':dict(zip(('x','y','z'),map(float,bounds_max))),
                                   'door':{'leaves':[],'lift':'','outside':{k:float((opening+outward*.8)[i]) if k!='y' else 0 for i,k in enumerate(('x','y','z'))},
                                           'inside':{k:float((opening-outward*.95)[i]) if k!='y' else floor for i,k in enumerate(('x','y','z'))},
                                           'openDelta':{'x':0,'y':0,'z':0},'liftDelta':{'x':0,'y':0,'z':0}},
                                   'aisle':aisle,'seats':seats,'source_meshes':len(meshes),'source_vertices':len(vertices),
                                   'door_source_nodes':[name for name in world_by_name if '_T4_Door_' in name],
                                   'hold_reason':'Combined bilateral static door mesh has no independently qualified sliding leaves/lift. Runtime split/capsule/navigation evidence required before operational activation.'}
    write_json(DATA / 'semantic-coverage-car04-candidate.json',evidence)
    print(json.dumps({'validation':evidence['known_passenger_validation'],'candidate_seats':len(evidence['candidate']['seats']) if evidence['candidate'] else None}))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')


def scan():
    import bpy
    from mathutils import Vector
    result = {'schema_version': 1, 'tool': 'Blender ' + bpy.app.version_string,
              'claim': 'Actual source FBX object inventory; imported asset-frame bounds, not site measurements.',
              'sources': [], 'objects': []}
    for source_id, relative in SOURCES.items():
        if source_id == 'station-points':
            continue
        source_path = ROOT / relative
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(source_path))
        result['sources'].append({'id': source_id, 'path': relative, 'sha256': digest(source_path)})

        def node_path(obj):
            return node_path(obj.parent) + '/' + obj.name if obj.parent else obj.name

        for obj in sorted(bpy.data.objects, key=node_path):
            record = {'source_id': source_id, 'node_path': node_path(obj), 'name': obj.name,
                      'parent_path': node_path(obj.parent) if obj.parent else None, 'type': obj.type}
            if obj.type == 'MESH':
                points = [obj.matrix_world @ Vector(p) for p in obj.bound_box]
                record.update(vertices=len(obj.data.vertices), polygons=len(obj.data.polygons),
                              bounds={'min': [min(p[i] for p in points) for i in range(3)],
                                      'max': [max(p[i] for p in points) for i in range(3)]},
                              materials=[s.material.name if s.material else None for s in obj.material_slots])
            result['objects'].append(record)
        print(source_id, 'objects', len(bpy.data.objects), 'meshes', sum(o.type == 'MESH' for o in bpy.data.objects))
    write_json(DATA / 'semantic-coverage-inventory.json', result)


def build():
    inventory_path = DATA / 'semantic-coverage-inventory.json'
    inventory = json.loads(inventory_path.read_text())
    for source in inventory['sources']:
        if digest(ROOT / source['path']) != source['sha256']:
            raise SystemExit('Source changed since Blender scan: ' + source['path'])
    station = json.loads((ROOT / SOURCES['station-points']).read_text())
    cars = station['train']['cars']
    sources = [{'id': key, 'path': path, 'sha256': digest(ROOT / path)} for key, path in SOURCES.items()]
    sources.append({'id':'fbx-inventory','path':str(inventory_path.relative_to(ROOT)),'sha256':digest(inventory_path)})
    physical, navigation, visual, unsupported = [], [], [], []

    def entity(entity_id, parent, kind, position, tags, state, operations, pointer, mode='existing'):
        return {'id': entity_id, 'parent_id': parent, 'kind': kind, 'position': position,
                'tags': tags, 'state': state, 'operations': operations, 'mode': mode,
                'source_refs': [{'source_id': 'station-points', 'pointer': pointer}],
                'claim': 'Coordinates and identity are model-derived. State and operations are training abstractions; material/field accuracy is unverified.'}

    train = entity('train:ktx', None, 'train', {'x':88.91879, 'y':-.73000026, 'z':76.442825},
                   ['train'], {'at-platform':1, 'hold':0}, ['hold_train'], '/train')
    train['source_refs'].append({'source_id':'ktx', 'node_path':'KTX_Sancheon_Single_10Car_Static'})
    physical.append(train)
    for index, platform in enumerate(station['platforms']):
        position = {k:(platform['a'][k]+platform['b'][k])/2 for k in ('x','y','z')}
        physical.append(entity('platform:'+platform['id'], None, 'platform', position,
                               ['walkable'], {'integrity':1, 'blocked':0}, ['move_to','cordon','clear_cordon'], '/platforms/'+str(index)))
    qualified_door_nodes = {}
    passenger_roots = {}
    for index, car in enumerate(cars):
        car_id = 'car:'+str(car['number']); door_id = 'door:'+str(car['number'])
        passenger_roots[car['name']] = car_id
        pointer = '/train/cars/'+str(index)
        position = {k:(car['min'][k]+car['max'][k])/2 for k in ('x','y','z')}; position['y']=car['floor']
        physical.append(entity(car_id, 'train:ktx', 'car', position,
                               ['semantic'], {'integrity':1, 'occupancy':0}, ['move_to'], pointer))
        physical.append(entity(door_id, car_id, 'door', car['door']['outside'],
                               ['semantic','door','electrical'], {'open':1, 'jammed':0, 'locked':0, 'power':1},
                               ['move_to','open_door','close_door','isolate_power'], pointer+'/door'))
        for node in [*car['door']['leaves'], car['door']['lift']]:
            qualified_door_nodes[(car['name'], node)] = door_id
        for seat_index, seat in enumerate(car['seats']):
            state = {'occupied':0, 'accessible':1, 'reachable':1, 'seat.yaw':seat['yaw']}
            state.update({'destination.'+k:seat['aisle'][k] for k in ('x','y','z')})
            entry = entity('seat:'+seat['id'], car_id, 'seat', seat['anchor'],
                           ['semantic','seat','place','occupiable','source-coordinate','training-model'], state,
                           ['move_to','sit','stand'], pointer+'/seats/'+str(seat_index), 'add')
            entry['links'] = [car_id, door_id]
            entry['operation_claim'] = 'move_to approaches source aisle; sit/stand are abstract occupancy states, not certified seat collision or posture animation.'
            physical.append(entry)
    for index, point in enumerate(station['points']):
        if point['kind'] != 'PlatformWait' or point['zone'] != 'tracks':
            continue
        entry = entity('source-point:'+point['id'], 'platform:'+station['train']['platform'], 'platform-wait',
                       point['position'], ['semantic','place','source-coordinate','training-model'],
                       {'accessible':1, 'reachable':1}, ['move_to'], '/points/'+str(index), 'add')
        entry['links'] = ['door:'+point['id'].split('-')[1]]
        navigation.append(entry)

    candidate_path = DATA / 'semantic-coverage-car04-candidate.json'
    candidate_evidence = json.loads(candidate_path.read_text())
    sources.append({'id':'car04-derivation','path':str(candidate_path.relative_to(ROOT)),'sha256':digest(candidate_path)})
    candidate = candidate_evidence['candidate']
    if candidate_evidence['source_sha256'] != digest(ROOT / SOURCES['ktx']):
        raise SystemExit('Car04 candidate source hash differs from current FBX; rederive first.')
    position={k:(candidate['min'][k]+candidate['max'][k])/2 for k in ('x','y','z')};position['y']=candidate['floor']
    car04=entity('car:4','train:ktx','car',position,['semantic','source-coordinate','training-model'],
                 {'integrity':1,'occupancy':0,'reachable':0,'accessible':0},[],None,'add')
    car04['source_refs']=[{'source_id':'ktx','node_path':'KTX_Sancheon_Single_10Car_Static/Car04_T4 4car'}]
    car04['claim']='Actual source passenger compartment body/seat/door geometry. Position is source-derived. Combined door operation and 40 candidate seat locations remain unverified; no new independent hazard capabilities.'
    physical.append(car04)
    passenger_roots[candidate['name']]='car:4'

    for obj in inventory['objects']:
        if obj['type'] != 'MESH':
            continue
        part_id = 'visual:'+hashlib.sha256((obj['source_id']+'|'+obj['node_path']).encode()).hexdigest()[:24]
        parent_name = (obj['parent_path'] or '').split('/')[-1]
        key = (parent_name, obj['name'])
        parent = None
        if obj['source_id'] == 'ktx' and key in qualified_door_nodes:
            parent = qualified_door_nodes[key]; status='operation-bound'
            reason='Exact source DTO door leaf/lift reference; one logical door owns all referenced render parts.'
        elif obj['source_id'] == 'ktx':
            parent = passenger_roots.get(parent_name, 'train:ktx'); status='parent-only'
            reason='Hierarchy belongs to a known train/car only. Independent component function, material and operation are unqualified.'
            unsupported.append({'id':part_id, 'category':'component-semantics-unqualified', 'reason':reason})
        else:
            status='unsupported'
            reason='Aggregated official FBX mesh cannot be assigned uniquely to one platform strip or equipment object from available source data.'
            unsupported.append({'id':part_id, 'category':'aggregate-platform-geometry', 'reason':reason})
        visual.append({'id':part_id, 'source_id':obj['source_id'], 'node_path':obj['node_path'],
                       'name':obj['name'], 'physical_parent_id':parent, 'status':status, 'reason':reason,
                       'vertices':obj['vertices'], 'polygons':obj['polygons'], 'bounds':obj['bounds']})

    consist = []
    for obj in inventory['objects']:
        if obj['source_id']!='ktx' or obj['type']=='MESH' or not re.match(r'^Car\d\d_', obj['name']):
            continue
        car_id = passenger_roots.get(obj['name'])
        role = 'passenger-compartment' if car_id else ('powerhead' if obj['name'] in ('Car00_Pc1','Car09_Pc2') else 'visual-car-unqualified-interior')
        consist.append({'id':'visual-space:'+obj['name'].split('_')[0], 'node_path':obj['node_path'],
                        'role':role, 'passenger_supported':car_id is not None, 'physical_entity_id':car_id,
                        'operationally_connected':car_id is not None and car_id!='car:4',
                        'reason':('Actual source passenger body; omitted from DTO due door-name mismatch. Combined-door operation and candidate seat inventory pending.' if car_id=='car:4' else 'Qualified by station-points passenger record.') if car_id else 'Powerhead, not passenger space; no passenger-coordinate/door operation record.'})
    categories = {}
    qualify_car04(physical, consist, sources)
    for item in unsupported:
        categories[item['category']] = categories.get(item['category'], 0)+1
    report = {'visual_parts':len(visual), 'physical_entities':len(physical),
              'state_connected':sum(bool(e['state']) for e in physical),
              'operation_connected':sum(bool(e['operations']) for e in physical),
              'navigation_points':len(navigation), 'source_seats':sum(e['kind']=='seat' for e in physical),
              'passenger_compartments':sum(e['kind']=='car' for e in physical), 'visual_cars':len(consist),
              'operational_passenger_compartments':sum(c['operationally_connected'] for c in consist),
              'operation_bound_visual_parts':sum(p['status']=='operation-bound' for p in visual),
              'unsupported_visual_parts':len(unsupported), 'unsupported_categories':categories,
              'ids_unique':True, 'parent_dedup':True,
              'count_claim':'State/operation counts describe declared model contracts, not all FBX component behaviors, runtime navigation acceptance or site accuracy.'}
    manifest = {'schema_version':2, 'claim':'Source-qualified identities/coordinates with explicit abstract training state; unsupported visuals retained without new hazard semantics.',
                'inventory_sha256':digest(inventory_path), 'sources':sources, 'visual_consist':consist,
                'visual_parts':visual, 'physical_entities':physical, 'navigation_points':navigation,
                'source_gaps':[{'id':'car:4','category':'historical-candidate-superseded','candidate_path':str(candidate_path.relative_to(ROOT)),
                                'candidate_sha256':digest(candidate_path),'candidate_seats':len(candidate['seats']),
                                'reason':'Prior40heuristic candidates are immutable history, not reused. External door remains blocked by original luggage rack.',
                                'seat_claim':'350 unchanged DTO anchors plus44 source-qualified Car04 cushions/backrests; no verified field labels or seated physical pose.'}],
                'unsupported':unsupported, 'report':report}
    write_json(DATA / 'semantic-coverage.json', manifest)
    print(json.dumps(report, ensure_ascii=False))


def main():
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scan', action='store_true')
    parser.add_argument('--derive-car04', action='store_true')
    parsed = parser.parse_args(args)
    if parsed.scan:
        scan()
    elif parsed.derive_car04:
        derive_car04()
    else:
        build()


if __name__ == '__main__':
    main()
