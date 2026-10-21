"""Offline source-model construction. Run with Blender, never Unity.

Default: a single-bay construction module plus independent end supports.
Full bank: --bays N --slot-bays comma,separated,indices (or 'none').
Bank count, floor/world pose and actual source correspondence are NOT chosen here.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

BASE = Path(__file__).resolve().parent
REPO = next(p for p in BASE.parents if (p / 'AGENTS.md').exists())
argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ap = argparse.ArgumentParser(description=__doc__)
ap.add_argument('--bays', type=int)
ap.add_argument('--slot-bays', help="Explicit source-bound indices; 'none' for no slots")
ap.add_argument('--pitch', type=float, default=0.60)
ap.add_argument('--depth', type=float, default=0.56)
ap.add_argument('--seat-height', type=float, default=0.45)
ap.add_argument('--end-margin', type=float, default=0.04)
ap.add_argument('--length', type=float)
ap.add_argument('--seat-thickness', type=float, default=0.050)
ap.add_argument('--pedestal-spacing', type=float)
ap.add_argument('--priority', action='store_true')
ap.add_argument('--tag', default='R31')
args = ap.parse_args(argv)
if args.bays is not None and (args.bays < 1 or args.slot_bays is None):
    ap.error('An explicit bank requires bays >=1 and an explicit --slot-bays list or none.')
n = args.bays or 1
module_only = args.bays is None
if args.length is not None:
    args.pitch = (args.length - 2 * args.end_margin) / n
slot_bays = [0] if module_only else ([] if args.slot_bays == 'none' else [int(x) for x in args.slot_bays.split(',')])
if any(i < 0 or i >= n for i in slot_bays):
    ap.error('A slot bay index falls outside the requested bank.')
if not (0.48 <= args.pitch <= 0.72 and 0.45 <= args.depth <= 0.66 and 0.40 <= args.seat_height <= 0.49):
    ap.error('Requested dimensions fall outside this qualified human/photo MODEL parameter range.')
safe_tag = ''.join(c for c in args.tag if c.isalnum() or c in '-_')
mode = 'SINGLE-BAY-MODULE' if module_only else f'PARAMETRIC-BANK-{n}-BAYS'
prefix = f'WAITING-BENCH-{mode}-{safe_tag}'
OUT = BASE / 'mesh-packet'
OUT.mkdir(exist_ok=True)
for ob in list(bpy.data.objects):
    bpy.data.objects.remove(ob, do_unlink=True)
for mesh in list(bpy.data.meshes):
    if mesh.users == 0:
        bpy.data.meshes.remove(mesh)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1.0

MAT_SPECS = {
    'WoodHoney': {'base_color_srgb': [0.69, 0.36, 0.10, 1], 'metallic': 0, 'roughness': 0.29},
    'WoodGolden': {'base_color_srgb': [0.77, 0.45, 0.16, 1], 'metallic': 0, 'roughness': 0.29},
    'WoodBrown': {'base_color_srgb': [0.48, 0.24, 0.075, 1], 'metallic': 0, 'roughness': 0.32},
    'WoodLight': {'base_color_srgb': [0.81, 0.50, 0.20, 1], 'metallic': 0, 'roughness': 0.30},
    'SatinSilver': {'base_color_srgb': [0.59, 0.61, 0.62, 1], 'metallic': 0.78, 'roughness': 0.30},
    'PedestalSilver': {'base_color_srgb': [0.55, 0.56, 0.56, 1], 'metallic': 0.45, 'roughness': 0.39},
    'UnderbeamGray': {'base_color_srgb': [0.22, 0.24, 0.25, 1], 'metallic': 0.6, 'roughness': 0.4},
    'KorailSourceNavy': {'base_color_srgb': [0.025, 0.075, 0.15, 1], 'metallic': 0, 'roughness': 0.4},
    'PriorityCharcoal': {'base_color_srgb': [0.17, 0.19, 0.235, 1], 'metallic': 0, 'roughness': 0.41},
    'PriorityLetterIvory': {'base_color_srgb': [0.87, 0.87, 0.83, 1], 'metallic': 0, 'roughness': 0.45},
}

def srgb_to_linear(x):
    return x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4

MATS = {}
for name, spec in MAT_SPECS.items():
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get('Principled BSDF')
    c = spec['base_color_srgb']
    rgba = [srgb_to_linear(x) for x in c[:3]] + [c[3]]
    spec['base_color_linear'] = rgba
    node.inputs['Base Color'].default_value = rgba
    node.inputs['Metallic'].default_value = spec['metallic']
    node.inputs['Roughness'].default_value = spec['roughness']
    mat.diffuse_color = rgba
    MATS[name] = mat

def activate(ob):
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob

def apply_modifier(ob, modifier):
    activate(ob)
    bpy.ops.object.modifier_apply(modifier=modifier.name)

def cube(name, dims, loc=(0, 0, 0), material=None, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    ob = bpy.context.object
    ob.name = name
    ob.dimensions = dims
    activate(ob)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = ob.modifiers.new('PhysicalRoundedEdges', 'BEVEL')
        mod.width = bevel
        mod.segments = 3
        apply_modifier(ob, mod)
    if material:
        ob.data.materials.append(MATS[material])
    return ob

def normalize_mesh(ob):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.0000002)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()
    # Keep broad timber/pedestal faces flat and soften only curved bevel bands.
    for poly in ob.data.polygons:
        m = max(abs(v) for v in poly.normal)
        poly.use_smooth = m < 0.999

def group(name, obs):
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    for ob in obs:
        ob.parent = root
    return root

pitch, depth, height = args.pitch, args.depth, args.seat_height
wood_thickness = args.seat_thickness
length = n * pitch + 2 * args.end_margin
deck = cube('CONSTRUCTION_MASTER_CONTINUOUS_WOOD_DECK', (length, depth, wood_thickness), (0, 0, height - wood_thickness / 2), bevel=0.012)

# Every selected slot is a real through-cut with a rounded end.
cutters = []
slot_width, slot_length, slot_gap = 0.009, 0.119, 0.022
for bay in slot_bays:
    bx = (bay + 0.5 - n / 2) * pitch
    for k in (-1, 0, 1):
        cutters.append(cube(f'SourceThroughSlot_{bay}_{k}', (slot_width, slot_length, wood_thickness + 0.10), (bx + k * slot_gap, 0, height - wood_thickness / 2), bevel=0.0043))
if cutters:
    activate(cutters[0])
    for ob in cutters:
        ob.select_set(True)
    bpy.ops.object.join()
    cutter = bpy.context.object
    mod = deck.modifiers.new('ActualThreeSlotGroups', 'BOOLEAN')
    mod.operation, mod.solver, mod.object = 'DIFFERENCE', 'EXACT', cutter
    apply_modifier(deck, mod)
    bpy.data.objects.remove(cutter, do_unlink=True)

# Source timber strips are continuous for the full bank length. They are
# separately editable, with no invented seat-bay joints or air gaps.
wood_objects = []
strip_count = 11
wood_palette = ['WoodHoney', 'WoodGolden', 'WoodLight', 'WoodHoney', 'WoodBrown', 'WoodGolden', 'WoodLight', 'WoodHoney', 'WoodBrown', 'WoodGolden', 'WoodHoney']
if args.priority:
    wood_palette[0] = wood_palette[-1] = 'PriorityCharcoal'
for i in range(strip_count):
    ob = bpy.data.objects.new(f'Wood_ContinuousLamination_{i:02d}', deck.data.copy())
    bpy.context.collection.objects.link(ob)
    ob.location = deck.location.copy()
    band = depth / strip_count
    center = -depth / 2 + band * (i + 0.5)
    clip = cube('CONSTRUCTION_LAMINATION_CLIP', (length + 1, band, 1), (0, center, height))
    mod = ob.modifiers.new('SeparateSourceTimberMember', 'BOOLEAN')
    mod.operation, mod.solver, mod.object = 'INTERSECT', 'EXACT', clip
    apply_modifier(ob, mod)
    bpy.data.objects.remove(clip, do_unlink=True)
    ob.data.materials.clear()
    ob.data.materials.append(MATS[wood_palette[i]])
    normalize_mesh(ob)
    ob['semantic'] = 'continuous laminated timber seat member; source-proportioned MODEL'
    wood_objects.append(ob)
bpy.data.objects.remove(deck, do_unlink=True)

def u_rail(name, x):
    z0 = height - 0.007
    ztop = height + 0.127
    span = depth - 0.12
    corner = 0.018
    path = [(-span / 2, z0), (-span / 2, ztop - corner)]
    for j in range(1, 7):
        a = math.pi - j * math.pi / 12
        path.append((-span / 2 + corner + math.cos(a) * corner, ztop - corner + math.sin(a) * corner))
    path.append((span / 2 - corner, ztop))
    for j in range(1, 7):
        a = math.pi / 2 - j * math.pi / 12
        path.append((span / 2 - corner + math.cos(a) * corner, ztop - corner + math.sin(a) * corner))
    path.append((span / 2, z0))
    verts, faces = [], []
    half_width, half_thickness = 0.0085, 0.0065
    for i, (py, pz) in enumerate(path):
        prev, nex = Vector(path[max(0, i - 1)]), Vector(path[min(len(path) - 1, i + 1)])
        tangent = (nex - prev).normalized()
        ny, nz = -tangent.y, tangent.x
        for side, wall in [(-1, -1), (1, -1), (1, 1), (-1, 1)]:
            verts.append((side * half_width, py + wall * half_thickness * ny, pz + wall * half_thickness * nz))
    for i in range(len(path) - 1):
        for j in range(4):
            faces.append((4*i+j, 4*i+(j+1)%4, 4*(i+1)+(j+1)%4, 4*(i+1)+j))
    faces += [(3, 2, 1, 0), tuple(4*(len(path)-1)+i for i in range(4))]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(MATS['SatinSilver'])
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(ob)
    ob.location.x = x
    normalize_mesh(ob)
    ob['semantic'] = 'observed low inverted-U flat silver divider, no invented end armrest'
    return ob

rail_positions = [pitch / 2] if module_only else [(i - n / 2) * pitch for i in range(1, n)]
rail_objects = [u_rail(f'SilverDivider_{i:02d}', x) for i, x in enumerate(rail_positions)]
beam = cube('Separate_Underseat_Crossbeam', (length - 0.10, 0.085, 0.070), (0, 0, height - wood_thickness - 0.035), 'UnderbeamGray', bevel=0.004)
normalize_mesh(beam)
priority_objects = []
if args.priority:
    trace = json.loads((BASE / 'PRIORITY-SOURCE-CONTOURS-R31.json').read_text())
    for layer in trace['layers']:
        cv = bpy.data.curves.new(layer['name'] + '_SourceContours', 'CURVE')
        cv.dimensions, cv.fill_mode, cv.extrude, cv.resolution_u = '2D', 'BOTH', 0.00035, 1
        if layer['surface'] == 'top':
            all_points = [p for row in layer['contours'] for p in row['points_source_pixels']]
            center_u = (min(p[0] for p in all_points) + max(p[0] for p in all_points)) / 2
            center_v = (min(p[1] for p in all_points) + max(p[1] for p in all_points)) / 2
            source_h = max(p[1] for p in all_points) - min(p[1] for p in all_points)
            glyph_scale = min(length/2200, (depth/11*.70)/source_h)
        for row in layer['contours']:
            if layer['surface'] == 'front_vertical':
                pts = [((px/2200-.5)*length, height-py/90*wood_thickness) for px, py in row['points_source_pixels']]
            else:
                pts = [((px-center_u)*glyph_scale, (py-center_v)*glyph_scale) for px, py in row['points_source_pixels']]
            area = sum(pts[i][0]*pts[(i+1)%len(pts)][1]-pts[(i+1)%len(pts)][0]*pts[i][1] for i in range(len(pts)))
            if (area > 0) == row['hole']:
                pts.reverse()
            sp = cv.splines.new('POLY')
            sp.points.add(len(pts)-1)
            for pp, (u, v) in zip(sp.points, pts):
                pp.co = (u, v, 0, 1)
            sp.use_cyclic_u = True
        cv.materials.append(MATS['PriorityLetterIvory'])
        ob = bpy.data.objects.new(layer['name'] + '_PhysicalClosedGlyphs', cv)
        bpy.context.collection.objects.link(ob)
        activate(ob)
        bpy.ops.object.convert(target='MESH')
        ob = bpy.context.object
        if layer['surface'] == 'front_vertical':
            for vert in ob.data.vertices:
                u, v, w = vert.co
                vert.co = (u, -w, v)
            ob.location = (0, -depth/2-0.0005, 0)
        else:
            sign = -1 if 'Left' in layer['name'] else 1
            ob.location = (sign*length/6, -depth/2+depth/22, height+0.0005)
            # Both long-edge top marks are actually visible in the source.
            # Reuse the observed contours at the opposite outward-reading mark.
            back = bpy.data.objects.new(layer['name']+'_OppositeObservedTopMark', ob.data)
            bpy.context.collection.objects.link(back)
            back.location = (sign*length/6, depth/2-depth/22, height+0.0005)
            back.rotation_euler.z = math.pi
            back['semantic'] = 'source-visible opposite top-band guidance; shared source contour solid mesh'
            priority_objects.append(back)
        ob['semantic'] = 'actual source-traced priority guidance closed solids with counters; no font or image glyph'
        normalize_mesh(ob)
        priority_objects.append(ob)
bay_root = group('SOURCE_MODEL_SINGLE_BAY_CONSTRUCTION_MODULE' if module_only else 'SOURCE_MODEL_PARAMETRIC_BANK_NO_WORLD_POSE', wood_objects + rail_objects + [beam] + priority_objects)

# Trace the observed mark, including its actual counterholes. No fonts, texture
# sampling, alpha planes, image glyphs or hidden live text are used.
logo_source = json.loads((BASE / 'KORAIL-SOURCE-CONTOURS-R31.json').read_text())
curve = bpy.data.curves.new('KORAIL_ActualSourceClosedContours', 'CURVE')
curve.dimensions, curve.fill_mode = '2D', 'BOTH'
curve.extrude, curve.resolution_u = 0.00035, 1
logo_width = 0.145
logo_height = logo_width * logo_source['mask_size'][1] / logo_source['mask_size'][0]
for contour in logo_source['contours']:
    points = [(p[0] * logo_width - logo_width / 2, p[1] * logo_height - logo_height / 2) for p in contour['points_uv']]
    area = sum(points[i][0] * points[(i+1)%len(points)][1] - points[(i+1)%len(points)][0] * points[i][1] for i in range(len(points)))
    # Outer CCW, counters CW, so 2D fill retains real openings.
    if (area > 0) == contour['hole']:
        points.reverse()
    spline = curve.splines.new('POLY')
    spline.points.add(len(points) - 1)
    for bp, (u, v) in zip(spline.points, points):
        bp.co = (u, v, 0, 1)
    spline.use_cyclic_u = True
curve.materials.append(MATS['KorailSourceNavy'])
logo = bpy.data.objects.new('KORAIL_SourceContour_ClosedSolid', curve)
bpy.context.collection.objects.link(logo)
activate(logo)
bpy.ops.object.convert(target='MESH')
logo = bpy.context.object
normalize_mesh(logo)

def pedestal(name, center_x, outward):
    # Visible outer plate + two side returns + top/base plates; inner face open.
    # Hidden folds/welds/fasteners are deliberately not manufactured as facts.
    t, d, h = 0.165, depth - 0.10, height - wood_thickness
    parts = [
        cube(name + '_OuterLogoFacePlate', (0.010, d, h), (center_x + outward * (t/2 - 0.005), 0, h/2), 'PedestalSilver', 0.0025),
        cube(name + '_FrontReturn', (t, 0.010, h), (center_x, -d/2+0.005, h/2), 'PedestalSilver', 0.0025),
        cube(name + '_RearReturn', (t, 0.010, h), (center_x, d/2-0.005, h/2), 'PedestalSilver', 0.0025),
        cube(name + '_TopPlate', (t, d, 0.012), (center_x, 0, h-0.006), 'PedestalSilver', 0.0025),
        cube(name + '_BasePlate', (t, d, 0.012), (center_x, 0, 0.006), 'PedestalSilver', 0.0025),
    ]
    for ob in parts:
        normalize_mesh(ob)
        ob['semantic'] = 'separate source-visible rectangular silver box pedestal member; hidden folds MODEL'
    ob = bpy.data.objects.new(name + '_KORAIL_ActualClosedGlyphs', logo.data)
    bpy.context.collection.objects.link(ob)
    # Map source u/v into outward-facing end plane. Rotations avoid reflections,
    # so letters read normally from each exterior bank end.
    from mathutils import Matrix
    u = Vector((0, outward, 0))
    v = Vector((0, 0, 1))
    normal = Vector((outward, 0, 0))
    rot = Matrix((u, v, normal)).transposed().to_4x4()
    rot.translation = Vector((center_x + outward * (t/2 + 0.0005), 0, h * 0.68))
    ob.matrix_world = rot
    ob['semantic'] = 'actual KORAIL source-pixel solid contours with four counterholes; no bitmap or font'
    if not args.priority:
        parts.append(ob)
    else:
        # No legible KORAIL mark is established on the selected priority bank's
        # support faces. The ordinary source end module retains its actual mark.
        bpy.data.objects.remove(ob, do_unlink=True)
    root = group(name, parts)
    return root, parts

pedestal_roots = []
pedestal_objects = []
if not module_only:
    for i, sign in enumerate((-1, 1)):
        px = args.pedestal_spacing/2 if args.pedestal_spacing is not None else length/2-0.16
        root, obs = pedestal(f'EndSupport_{i:02d}', sign * px, sign)
        pedestal_roots.append(root)
        pedestal_objects += obs
else:
    root, obs = pedestal('INDEPENDENT_LEFT_END_SUPPORT_ASSEMBLY', 0, -1)
    pedestal_roots.append(root)
    pedestal_objects += obs
bpy.data.objects.remove(logo, do_unlink=True)

def unity(v):
    return [round(float(v[0]), 8), round(float(v[2]), 8), round(float(-v[1]), 8)]

mesh_library = {}
mesh_ids = {}

def serialize_object(ob):
    mesh = ob.data
    mesh.calc_loop_triangles()
    # Meshes remain local and instance transforms carry positions. Triangle and
    # split-normal records retain hard broad faces and smooth actual curves.
    verts, normals, indices, uv = [], [], [], []
    loop_map = {}
    for tri in mesh.loop_triangles:
        for loop_id in tri.loops:
            loop = mesh.loops[loop_id]
            co = mesh.vertices[loop.vertex_index].co
            norm = mesh.corner_normals[loop_id].vector
            key = (loop.vertex_index, tuple(round(float(x), 6) for x in norm))
            if key not in loop_map:
                loop_map[key] = len(verts)
                verts.append(unity(co))
                normals.append(unity(norm))
                # Timber U follows actual full-bank length; no photo is sampled.
                uv.append([round(float(co.x), 6), round(float(co.y), 6)])
            indices.append(loop_map[key])
    raw = dict(vertices=verts, normals=normals, uv=uv, triangles=indices)
    digest = hashlib.sha256(json.dumps(raw, separators=(',', ':')).encode()).hexdigest()
    if digest not in mesh_ids:
        mid = f'mesh_{len(mesh_ids):03d}_{digest[:10]}'
        mesh_ids[digest] = mid
        mesh_library[mid] = dict(name=mesh.name, **raw)
    # Bake world transformation into a proper Unity Y-up column-major matrix.
    from mathutils import Matrix
    conv = Matrix(((1,0,0,0),(0,0,1,0),(0,-1,0,0),(0,0,0,1)))
    mat = conv @ ob.matrix_world @ conv.inverted()
    return dict(name=ob.name, mesh=mesh_ids[digest], material=mesh.materials[0].name,
                local_to_module_matrix_rows=[[round(float(x), 9) for x in row] for row in mat],
                semantic=ob.get('semantic', 'source-proportioned separately editable component'))

assemblies = {}
if module_only:
    assemblies['single_bay_module'] = dict(objects=[serialize_object(o) for o in wood_objects + rail_objects + [beam] + priority_objects], construction_module_only=True)
    assemblies['independent_end_support_left'] = dict(objects=[serialize_object(o) for o in pedestal_objects], construction_module_only=True)
else:
    assemblies['explicit_count_bank'] = dict(objects=[serialize_object(o) for o in wood_objects + rail_objects + [beam] + pedestal_objects + priority_objects], construction_module_only=False)

sources = [
    dict(file='source-evidence/retained-work-originals/workers/twin/output/architecture-resume-20261010/MAIN1F-native-panorama-reference-20261011/native/google-pano-01-provider-s0.jpg', role='actual2Fhall despite historical1F folder; backless timber/deck slots/dividers/box feet/KORAIL contours', epoch='EXIF DateTime2023:12:01 20:49:18; capture epoch not independently verified'),
    dict(file='.state/ACTIVE-ALL2F-COMPLETION-R26/public/southwest-field-identity/sources/r29-wide-route/DAILY-FIELD-21.jpg', role='same ordinary honey-wood/silver-divider family actual2024-08-07 field image'),
    dict(file='asset-library/research-public/2026-10-08/measured-twin/clips/RY2qvE0Tugk_215-670.webm', role='retained continuous2023walk PTS116/120/124/128/132/136 family corroboration; no original timeline offset assertion'),
]
for source in sources:
    path = REPO / source['file']
    source['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()

packet = dict(
    schema='CHOOGUARD_WAITING_BENCH_EDITABLE_MESH_PACKET_R31_V1',
    status='READY_OFFLINE_MODEL_ONLY_NO_WORLD_PLACEMENT',
    native_or_unity_or_assets_calls=False,
    no_agent_qa_tests_Play_reopen_probes_or_scene_captures=True,
    coordinates='right-handed meter local coordinates, Unity Y-up; +X bench long axis; +Z deck cross axis; Y0 is local support floor',
    source_family=('selected source priority bank with dark long-edge strips and actual physical priority guidance; ' if args.priority else 'ordinary unwrapped bank; ') + 'BACKLESS continuous laminated honey timber deck, real throughslots, low silver inverted-U divider rails and independent lightgray box pedestals',
    source_old_backrest_hypothesis='WITHDRAWN by actual panorama01 and elevated2023source view',
    count_role='single-bay construction module, NOT an asserted one-seat physical bank' if module_only else 'caller-selected parametric count; not a field-confirmed physical bank count',
    field_confirmed_bank_count=None, world_pose=None, world_footprint=None,
    parameters_MODEL=dict(bays=n, pitch=pitch, depth=depth, seat_height=height, end_margin=args.end_margin,
                          overall_local_length=length, timber_thickness=wood_thickness, lamination_count=strip_count,
                          slots_by_bay=slot_bays, slots_per_selected_bay=3, slot_width=slot_width, slot_length=slot_length,
                          divider_above_deck=0.127, pedestal_end_setback=(length-args.pedestal_spacing)/2 if args.pedestal_spacing is not None else 0.16,
                          pedestal_center_spacing=args.pedestal_spacing, priority_variant=args.priority),
    dimensions_qualification='Photo/human-proportioned MODEL parameters, not surveyed dimensions; 11 lamination subdivisions and exact hidden metal folds are compact modeling choices.',
    parameter_ranges_MODEL_m=dict(pitch=[0.48,0.72], depth=[0.45,0.66], seat_height=[0.40,0.49]),
    regeneration=dict(blender_script=str((BASE/'build_bench_family.py').relative_to(REPO)),
                      full_bank_command='blender -b --factory-startup --python <script> -- --bays N --slot-bays 0,LAST_INDEX --tag <newtag>',
                      full_bank_caution='N and integer slot indices must come from the placement/source constructor. Use none when a source-bound bank has no slots. Regenerate full-length timber; do not repeat short bay blocks as physical wood joints.'),
    exclusions=['backrests','generic advertising','unreadable/invented secondary text','USB/AC chargers','hidden floor bolts','end armrests','scene Lights','TextMesh/TMP','sourcebitmap/atlas/decal lettering','hidden far vertical-face priority text'],
    materials=MAT_SPECS, mesh_library=mesh_library, assemblies=assemblies, sources=sources,
    logo_provenance='PRIORITY-SOURCE-CONTOURS-R31.json; actual white priority-guidance outer/counter contours; no hidden far vertical-face copy. Selected priority pedestals have no independently readable KORAIL mark.' if args.priority else 'KORAIL-SOURCE-CONTOURS-R31.json; actual source-pixel outer/inner contours, closed extrusion with four actual counterholes; coarse-source MODEL, no vector-master equality claim',
    original_StationBench4=dict(readonly_inspected=True, reused=False, sha256='be40e0311cdfc22fd8746df44e89294ac3203f0452c0c132d276e04408d37f73', license='pre-existing user project source, author/license not independently established; no unlicensed or CC0 claim'),
    attribution='Custom source-observed geometry authored for this project. Retained Google panorama/field/video images remain read-only evidence; no source photo is embedded in any model/material. Observed KORAIL trademark is reproduced as source-contour physical geometry; no new brand or license claim.',
    construction_next_step='Root placement author resolves actual fixed-point binding and chooses count/slot pattern. Every later Unity/Assets write requires the current C180 FIFO scoped lease; this packet has no placement authorization.')
(OUT/(prefix+'.json')).write_text(json.dumps(packet, separators=(',', ':'), ensure_ascii=False))

def export_selected(filename, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:
        ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(OUT/filename), export_format='GLB', use_selection=True,
                              export_yup=True, export_materials='EXPORT', export_animations=False,
                              export_cameras=False, export_lights=False, export_extras=True)

if module_only:
    export_selected(prefix+'.glb', wood_objects + rail_objects + [beam] + priority_objects)
    export_selected('WAITING-BENCH-INDEPENDENT-END-SUPPORT-'+safe_tag+'.glb', pedestal_objects)
else:
    export_selected(prefix+'.glb', wood_objects + rail_objects + [beam] + pedestal_objects + priority_objects)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(prefix+'.blend')))
summary = dict(packet=(OUT/(prefix+'.json')).name, mode=mode, unique_meshes=len(mesh_library),
               mesh_instances=sum(len(a['objects']) for a in assemblies.values()),
               unique_vertices=sum(len(m['vertices']) for m in mesh_library.values()),
               unique_triangles=sum(len(m['triangles'])//3 for m in mesh_library.values()),
               model_only=True, scene_assets_untouched=True)
(OUT/(prefix+'-BUILD-RECEIPT.json')).write_text(json.dumps(summary, indent=2))
print('OFFLINE_MODEL_CONSTRUCTION_COMPLETE '+json.dumps(summary))
