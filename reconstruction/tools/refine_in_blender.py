"""Blender 4.5 LTS: preserve raw GLB, edit supported surfaces, stage FBX for Unity.

Usage: Blender --background --factory-startup --python-exit-code 1 --python SCRIPT --
       --input-dir reconstruction/output/busan-concourse-pilot
No rendering, bake, hole fill, invented architecture or metric rescaling.
"""

import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--input-dir', required=True, type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    directory = args.input_dir.resolve()
    manifest_path = directory / 'review-manifest.json'
    manifest = json.loads(manifest_path.read_text())
    source = directory / 'review-surfaces.glb'
    if sha256(source) != manifest['artifacts']['review-surfaces.glb']:
        raise ValueError('Source GLB hash does not match review manifest')
    if manifest.get('colorSpaces', {}).get('glbColor0') != 'linear-rgb8-normalized':
        raise ValueError('Source vertex colors must use the explicit linear glTF encoding')
    if manifest['coordinates']['metersPerUnit'] is not None:
        raise ValueError('This pilot recipe supports uncalibrated review only')
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)
    raw = bpy.data.collections.new('RAW_DA3_UNCALIBRATED')
    refined = bpy.data.collections.new('EDITABLE_OBSERVED_SURFACES')
    bpy.context.scene.collection.children.link(raw)
    bpy.context.scene.collection.children.link(refined)
    bpy.context.view_layer.active_layer_collection = bpy.context.view_layer.layer_collection
    bpy.ops.import_scene.gltf(filepath=str(source))
    imported = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    edits = []
    for obj in imported:
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        raw.objects.link(obj)
        obj['geometry_status'] = 'raw relative-depth surface, masked, not metric, not collision'
        obj.hide_render = True
        obj.hide_set(True)
        copy = obj.copy()
        copy.data = obj.data.copy()
        copy.name = obj.name + '_refined'
        refined.objects.link(copy)
        copy.hide_render = False
        copy.hide_set(False)
        copy['geometry_status'] = 'limited planar dissolve only; no new observed area'
        bpy.ops.object.select_all(action='DESELECT')
        copy.select_set(True)
        bpy.context.view_layer.objects.active = copy
        before = len(copy.data.polygons)
        modifier = copy.modifiers.new('Conservative_planar_cleanup', 'DECIMATE')
        modifier.decimate_type = 'DISSOLVE'
        modifier.angle_limit = math.radians(.5)
        modifier.use_dissolve_boundaries = False
        modifier.delimit = {'NORMAL', 'MATERIAL', 'SEAM', 'SHARP', 'UV'}
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        triangulate = copy.modifiers.new('Explicit_triangles', 'TRIANGULATE')
        bpy.ops.object.modifier_apply(modifier=triangulate.name)
        # Shared vertex normals prevent the exporter splitting every triangle corner.
        # Color remains observed RGB; this does not add geometric detail.
        for polygon in copy.data.polygons:
            polygon.use_smooth = True
        edits.append({'id': obj.name, 'object': copy.name, 'inputTriangles': before,
                      'outputTriangles': len(copy.data.polygons)})
    raw.hide_viewport = True
    raw.hide_render = True
    bpy.context.scene.unit_settings.system = 'NONE'
    bpy.context.scene['review_status'] = 'Model-relative partial upper concourse; no metric scale'
    bpy.context.scene['review_manifest_sha256'] = sha256(manifest_path)
    # Leave an editable native source in ordinary Blender/glTF orientation.
    bpy.ops.wm.save_as_mainfile(filepath=str(directory / 'concourse-refinement.blend'))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in refined.objects:
        obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(directory / 'refined-review.glb'),
                              export_format='GLB', use_selection=True, export_yup=True)
    # Rotate staging only. Blender(-x,z,y) -> Unity(-Bx,Bz,-By) -> (x,y,-z).
    rotate = Matrix.Rotation(math.pi, 4, 'Z')
    unity_from_blender = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0),
                                 (0, -1, 0, 0), (0, 0, 0, 1)))
    for obj, edit in zip(refined.objects, edits):
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        obj.matrix_world = rotate @ obj.matrix_world
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        vertices = [unity_from_blender @ obj.matrix_world @ v.co for v in obj.data.vertices]
        lo = Vector(tuple(min(v[a] for v in vertices) for a in range(3)))
        hi = Vector(tuple(max(v[a] for v in vertices) for a in range(3)))
        edit['expectedUnityBounds'] = {'center': dict(zip('xyz', (lo+hi)/2)),
                                       'size': dict(zip('xyz', hi-lo))}
        edit['vertices'] = len(vertices)
        attribute = obj.data.color_attributes.active_color
        if attribute is None:
            raise ValueError('No observed vertex colors in Blender surface')
        samples = []
        for index in [0, len(attribute.data)//2, len(attribute.data)-1]:
            vertex_index = obj.data.loops[index].vertex_index if attribute.domain == 'CORNER' else index
            samples.append({'position': dict(zip('xyz', vertices[vertex_index])),
                            'color': dict(zip('rgba', attribute.data[index].color))})
        edit['colorSamples'] = samples
    bpy.ops.object.select_all(action='DESELECT')
    for obj in refined.objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(directory / 'refined-review.fbx'), use_selection=True,
                             object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                             apply_unit_scale=True, bake_space_transform=False,
                             colors_type='LINEAR',
                             use_mesh_modifiers=True, add_leaf_bones=False,
                             path_mode='STRIP', embed_textures=False)
    artifacts = ['concourse-refinement.blend', 'refined-review.glb', 'refined-review.fbx']
    receipt = {'schemaVersion': 'blender-reconstruction-review-1',
               'blenderVersion': bpy.app.version_string,
               'recipeSha256': sha256(__file__),
               'inputManifestSha256': sha256(manifest_path),
               'sourceGlbSha256': sha256(source), 'views': edits,
               'fbxSha256': sha256(directory / 'refined-review.fbx'),
               'unit': 'model_relative', 'metersPerUnit': None,
               'colorEncoding': 'linear-rgb',
               'unityFromReviewRowMajor': [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1],
               'procedure': '0.5 degree limited planar dissolve, preserve boundaries, triangulate',
               'collisionApproved': False, 'modelingAcceptance': 'partial observation study',
               'artifacts': {name: sha256(directory/name) for name in artifacts}}
    (directory / 'review-manifest.blender.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print('CHOO_BLENDER_RECONSTRUCTION ' + json.dumps(receipt))


if __name__ == '__main__':
    main()
