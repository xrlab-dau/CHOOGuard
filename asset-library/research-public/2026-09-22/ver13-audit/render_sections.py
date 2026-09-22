"""수직 단면 렌더 — 기존 감사는 상면도(위에서 내려다본 컷어웨이)만 냈다.

상면도로는 '바닥이 있는가'는 보여도 '벽으로 둘러싸인 속이 빈 공간인가'는 안 보인다.
직교 카메라를 옆에서 두고 near clip 으로 앞쪽 절반을 잘라내면, 층·벽·천장이 한 장에 드러난다.

격자 프로브가 z=7.0m 에 `-부산역` 면 267개를 찾았다(share 0.203). 그것이 2F 콩코스 바닥인지
캐노피 지붕인지를 이 렌더가 판정한다.
"""
import bpy, pathlib, math, sys
from mathutils import Vector

ROOT = pathlib.Path.cwd()
OUT = ROOT / 'asset-library/research-public/2026-09-22/ver13-audit'
bpy.ops.wm.open_mainfile(filepath=str(OUT / 'ver13-batched.blend'))
s = bpy.context.scene

# 역사 본체만 남긴다. 주변 건물·도로·기차는 단면을 가린다.
KEEP = {'-부산역', '-부산역_선로상층부', '-부산역_선로상층부(주차장)', '-부산역_출구지붕', 'Layer0'}
for o in list(s.objects):
    if o.type == 'MESH' and o.get('source_layer') not in KEEP:
        bpy.data.objects.remove(o, do_unlink=True)

lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
for o in s.objects:
    if o.type != 'MESH': continue
    for c in o.bound_box:
        w = o.matrix_world @ Vector(c)
        for k in range(3):
            lo[k] = min(lo[k], w[k]); hi[k] = max(hi[k], w[k])
centre = (lo + hi) / 2
size = hi - lo
print('KEPT_BOUNDS', [round(v, 1) for v in lo], [round(v, 1) for v in hi], flush=True)

cam_data = bpy.data.cameras.new('SectionCam')
cam_data.type = 'ORTHO'
cam = bpy.data.objects.new('SectionCam', cam_data)
s.collection.objects.link(cam); s.camera = cam

sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
sun.data.energy = 4.0; sun.rotation_euler = (math.radians(50), 0, math.radians(30))
s.collection.objects.link(sun)

s.render.engine = 'BLENDER_EEVEE_NEXT'
s.render.resolution_x = 1600; s.render.resolution_y = 900
s.render.film_transparent = False
if s.world is None:
    s.world = bpy.data.worlds.new('W')
s.world.use_nodes = True
s.world.node_tree.nodes['Background'].inputs[0].default_value = (0.5, 0.55, 0.6, 1)

def shoot(name, axis, cut_fraction):
    """axis: 'y' 는 남->북 바라보는 단면, 'x' 는 서->동. cut_fraction 만큼 앞을 잘라낸다."""
    span = size.y if axis == 'y' else size.x
    dist = span * 1.2
    if axis == 'y':
        cam.location = (centre.x, lo.y - dist, centre.z)
        cam.rotation_euler = (math.radians(90), 0, 0)
        cam_data.ortho_scale = max(size.x, size.z) * 1.05
    else:
        cam.location = (lo.x - dist, centre.y, centre.z)
        cam.rotation_euler = (math.radians(90), 0, math.radians(-90))
        cam_data.ortho_scale = max(size.y, size.z) * 1.05
    # near clip 을 건물 중간까지 밀어 앞쪽 절반을 제거한다 = 단면
    cam_data.clip_start = dist + span * cut_fraction
    cam_data.clip_end = dist + span * 2
    path = OUT / f'section-{name}.png'
    s.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    print('SECTION', name, path.name, 'clip_start', round(cam_data.clip_start, 1), flush=True)

s.eevee.taa_render_samples = 16
shoot('north-at-35pct', 'y', 0.35)
shoot('north-at-55pct', 'y', 0.55)
shoot('east-at-45pct', 'x', 0.45)
print('DONE', flush=True)
