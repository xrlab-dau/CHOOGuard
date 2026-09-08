"""Blender: editable volumetric members from a reviewed relative-coordinate recipe.

The recipe must distinguish triangulated anchors and estimated member sections.
No measured scale, hidden station layout, floor, collider or safety path is created.
"""

import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Euler, Matrix, Vector


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def linear_color(rgb):
    return tuple(v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in rgb)


def select_only(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--recipe', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    recipe = json.loads(args.recipe.read_text())
    if recipe.get('unit') != 'model_relative' or recipe.get('metricApproved') is not False:
        raise ValueError('Only explicitly uncalibrated structural studies are accepted')
    if not recipe.get('members') or not recipe.get('nodes'):
        raise ValueError('No reviewed structural members')
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    collection = bpy.data.collections.new('EDITABLE_PHOTO_GUIDED_STRUCTURE')
    bpy.context.scene.collection.children.link(collection)
    parent = bpy.data.objects.new('concourse-structural-study_refined', None)
    collection.objects.link(parent)
    parent['status'] = 'Relative structural study; visible intervals and estimated sections only'
    to_blender = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))
    nodes = {name: to_blender @ Vector(value['pointReview']) for name, value in recipe['nodes'].items()}
    objects = []
    member_receipts = []
    for member in recipe['members']:
        start, end = nodes[member['start']], nodes[member['end']]
        direction = end-start
        length = direction.length
        section = member['section']
        width = float(section['widthUnits'])
        if not math.isfinite(width) or not math.isfinite(length) or width <= 0 or length <= 1e-8:
            raise ValueError('Invalid section width or degenerate structural member')
        if section.get('authority') not in {'projected_photo_width_estimate', 'photo_ratio_estimate'}:
            raise ValueError('Member section needs an explicit observed/estimated basis')
        if section['shape'] == 'round':
            if section.get('profile') == 'three_band_cover_estimate':
                # Visible collar bands only; no invented hidden bolts or load path.
                profile = [(-.5,.94),(-.43,1),(-.22,1),(-.18,.94),(-.14,1),
                           (.10,1),(.14,.94),(.18,1),(.43,1),(.5,.94)]
                vertices = [(width/2*r*math.cos(2*math.pi*i/32),
                             width/2*r*math.sin(2*math.pi*i/32), length*z)
                            for z,r in profile for i in range(32)]
                faces = [(j*32+i,j*32+(i+1)%32,(j+1)*32+(i+1)%32,(j+1)*32+i)
                         for j in range(len(profile)-1) for i in range(32)]
                faces.extend([tuple(reversed(range(32))),
                              tuple((len(profile)-1)*32+i for i in range(32))])
                mesh = bpy.data.meshes.new(member['id']+'_profile')
                mesh.from_pydata(vertices, [], faces)
                mesh.update()
                obj = bpy.data.objects.new(member['id'], mesh)
                collection.objects.link(obj)
                select_only(obj)
            else:
                bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=width/2, depth=length,
                                                    end_fill_type='NGON')
        elif section['shape'] == 'box':
            thickness = float(section.get('depthUnits', width))
            if thickness <= 0 or not math.isfinite(thickness):
                raise ValueError('Invalid box depth')
            bpy.ops.mesh.primitive_cube_add(size=1)
            bpy.context.object.scale = (width, thickness, length)
            bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        else:
            raise ValueError('Unsupported observed section shape')
        obj = bpy.context.object
        obj.name = member['id']
        for c in list(obj.users_collection):
            c.objects.unlink(obj)
        collection.objects.link(obj)
        obj.parent = parent
        obj.location = (start+end)/2
        obj.rotation_mode = 'QUATERNION'
        obj.rotation_quaternion = direction.to_track_quat('Z', 'Y')
        obj['sourceMemberId'] = member['id']
        obj['sectionAuthority'] = section['authority']
        obj['observedIntervalOnly'] = True
        select_only(obj)
        bevel = obj.modifiers.new('Small_manufactured_edge', 'BEVEL')
        bevel.width = width*.012
        bevel.segments = 2
        bpy.ops.object.modifier_apply(modifier=bevel.name)
        triangulate = obj.modifiers.new('Explicit_triangles', 'TRIANGULATE')
        bpy.ops.object.modifier_apply(modifier=triangulate.name)
        for polygon in obj.data.polygons:
            polygon.use_smooth = section['shape'] == 'round' and abs(polygon.normal.z) < .99
        edge_uses = Counter(tuple(sorted(edge)) for polygon in obj.data.polygons
                            for edge in polygon.edge_keys)
        if not edge_uses or any(count != 2 for count in edge_uses.values()):
            raise ValueError('Authored member contains an open or nonmanifold boundary')
        rgba = (*linear_color(member.get('colorSrgb', [.76, .77, .77])), 1.)
        attribute = obj.data.color_attributes.new(name='ObservedPaint', type='FLOAT_COLOR', domain='POINT')
        for color in attribute.data:
            color.color = rgba
        material = bpy.data.materials.new(member['id']+'_paint')
        material.use_nodes = True
        bsdf = material.node_tree.nodes.get('Principled BSDF')
        bsdf.inputs['Base Color'].default_value = rgba
        bsdf.inputs['Metallic'].default_value = .05
        bsdf.inputs['Roughness'].default_value = .36
        obj.data.materials.clear()
        obj.data.materials.append(material)
        objects.append(obj)
        member_receipts.append({'id': member['id'], 'triangles': len(obj.data.polygons),
                                'sectionAuthority': section['authority'], 'closedVolume': True,
                                'boundaryEdges': 0})
    bpy.context.scene.unit_settings.system = 'NONE'
    bpy.context.scene['recipeSha256'] = sha(args.recipe)
    bpy.context.scene['metricApproved'] = False
    # Open the native source already framed on the small relative-unit assembly.
    world_vertices = [obj.matrix_world @ v.co for obj in objects for v in obj.data.vertices]
    view_lo = Vector(tuple(min(v[a] for v in world_vertices) for a in range(3)))
    view_hi = Vector(tuple(max(v[a] for v in world_vertices) for a in range(3)))
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                space = area.spaces.active
                space.region_3d.view_location = (view_lo+view_hi)/2
                space.region_3d.view_distance = max((view_hi-view_lo).length*2, .05)
                space.region_3d.view_rotation = Euler((math.radians(75), 0, math.radians(25))).to_quaternion()
                space.region_3d.view_perspective = 'PERSP'
                space.shading.type = 'MATERIAL'
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(output/'structural-study.blend'))
    bpy.ops.object.select_all(action='DESELECT')
    parent.select_set(True)
    for obj in objects:
        obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(output/'review-surfaces.glb'), export_format='GLB',
                              use_selection=True, export_yup=True)
    # Same proven FBX axis contract as the depth-surface path.
    rotate = Matrix.Rotation(math.pi, 4, 'Z')
    unity_from_blender = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0),
                                 (0, -1, 0, 0), (0, 0, 0, 1)))
    points, samples, total_vertices = [], [], 0
    for obj in objects:
        select_only(obj)
        obj.matrix_world = rotate @ obj.matrix_world
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        current = [unity_from_blender @ obj.matrix_world @ v.co for v in obj.data.vertices]
        points.extend(current)
        total_vertices += len(current)
        if len(samples) < 3:
            samples.append({'position': dict(zip('xyz', current[0])),
                            'color': dict(zip('rgba', obj.data.color_attributes['ObservedPaint'].data[0].color))})
    if len(samples) != 3:
        raise ValueError('A structural assembly requires at least three independently colored members')
    bpy.ops.object.select_all(action='DESELECT')
    parent.select_set(True)
    for obj in objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(output/'refined-review.fbx'), use_selection=True,
                             object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y',
                             colors_type='LINEAR', apply_unit_scale=True, bake_space_transform=False,
                             use_mesh_modifiers=True, add_leaf_bones=False, path_mode='STRIP')
    lo = Vector(tuple(min(v[a] for v in points) for a in range(3)))
    hi = Vector(tuple(max(v[a] for v in points) for a in range(3)))
    manifest = {'schemaVersion': 'structural-study-1', 'status': 'photo_guided_authored_volumes',
                'recipeSha256': sha(args.recipe), 'builderSha256': sha(__file__),
                'references': recipe.get('references', {}), 'members': member_receipts,
                'metricApproved': False, 'collisionApproved': False,
                'limitations': recipe['limitations']}
    (output/'review-manifest.json').write_text(json.dumps(manifest, indent=2)+'\n')
    triangles = sum(item['triangles'] for item in member_receipts)
    receipt = {'schemaVersion': 'blender-reconstruction-review-1', 'blenderVersion': bpy.app.version_string,
               'recipeSha256': sha(__file__), 'inputManifestSha256': sha(output/'review-manifest.json'),
               'sourceGlbSha256': sha(output/'review-surfaces.glb'), 'fbxSha256': sha(output/'refined-review.fbx'),
               'unit': 'model_relative', 'metersPerUnit': None, 'colorEncoding': 'linear-rgb',
               'shadingMode': 'authored-lit', 'collisionApproved': False,
               'unityFromReviewRowMajor': [1,0,0,0,0,1,0,0,0,0,-1,0,0,0,0,1],
               'views': [{'id': 'concourse-structural-study', 'object': parent.name,
                          'vertices': total_vertices, 'inputTriangles': triangles, 'outputTriangles': triangles,
                          'expectedUnityBounds': {'center': dict(zip('xyz', (lo+hi)/2)),
                                                  'size': dict(zip('xyz', hi-lo))}, 'colorSamples': samples}],
               'modelingAcceptance': 'bounded photo-guided volume study, unmeasured sections and endpoints',
               'artifacts': {name: sha(output/name) for name in
                             ['structural-study.blend', 'review-surfaces.glb', 'refined-review.fbx']}}
    (output/'review-manifest.blender.json').write_text(json.dumps(receipt, indent=2)+'\n')
    print('CHOO_STRUCTURE_STUDY '+json.dumps(receipt))


if __name__ == '__main__':
    main()
