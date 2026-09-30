"""Procedural Blender models of the LP-panel breaker deck and its lever (2026-09-30, w4-electric-plaza) - nothing is downloaded,
every mesh and texture is generated here, deterministically (two runs give byte-identical files).

    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P make_breakers.py -- [Name ...]   (no names = all)

Names: BreakerLever BreakerDeck.
Output per prop: Assets/ChooGuard/Art/Emergency/Equipment/Models/<Name>/<Name>.obj (project-made, not third-party) (+ .mtl, .json
sidecar, PNG maps), the same layout as make_props.py (w5-kitchen-gas). Previews go to /tmp/w4/breaker-preview/.

BreakerDeck  = dead-front of a small Korean LP (lighting/outlet) panel: RAL 7035 powder-coated steel plate (0.40 x 0.5255 m, 3 mm) with
               eight 60 x 100 mm cut-outs and eight 2-pole molded-case breakers (dark grey ABS: chamfered body, recessed lever slot,
               four terminal screws, printed rating + ON/OFF, the MAIN one an ELB with a TEST button) standing in them. The plate
               carries printed circuit numbers (MAIN, 1..7), a 감전주의 sticker, four corner screws and a name plate.
BreakerLever = one toggle lever, origin exactly on its hinge axis (X), neutral pose pointing out of the panel (+Z Unity), 28 mm long,
               30 mm wide, 10 mm thick, black ABS with a rounded tip and finger grooves. The game rotates 8 copies about local X:
               Unity's +X rotation pitches +Z downwards, so tip UP (ON) = Euler(-25, 0, 0), tip DOWN (OFF) = Euler(+25, 0, 0).

Deck frame (Unity metres, the frame of DistributionBoard.obj): origin back-plane bottom centre, +Y up, +Z out of the wall, plate front
face exactly z = 0.240. Blender axes (the project OBJ round trip): (bx, by, bz) -> Unity (-bx, bz, -by), i.e. front = -Y and
bx = -ux, by = -uz, bz = uy; everything below is written in Blender axes, in metres.

Label textures need Pillow, which Blender's Python lacks: the script re-runs itself with the Pillow interpreter
(~/.cache/chooguard-melotts/bin/python make_breakers.py --textures <dir> Name ...) and reads the PNGs back."""
import json
import math
import shutil
import subprocess
import sys
from pathlib import Path

try:
    import bmesh
    import bpy
    import numpy as np
    from mathutils import Vector
    IN_BLENDER = True
except ImportError:
    IN_BLENDER = False

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OUT = ROOT / "Assets/ChooGuard/Art/Emergency/Equipment/Models"
FONT = ROOT / "Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf"
BOARD_OBJ = ROOT / "Assets/ChooGuard/ThirdParty/Models/Objaverse/DistributionBoard/DistributionBoard.obj"
PILPY = Path.home() / ".cache/chooguard-melotts/bin/python"
TEX = Path("/tmp/w4/breaker-tex")
PREV = Path("/tmp/w4/breaker-preview")

ORDER = ["BreakerLever", "BreakerDeck"]
TEXTURES = {"BreakerDeck": ("plate", "face")}   # <TEX>/<Name>_<key>.png (drawn by the Pillow half below)

# ------------------------------------------------------------ layout (Unity metres unless a name says otherwise)
PLATE_HALF_W, PLATE_Y0, PLATE_Y1 = .20, .2220, .7475      # plate x in [-.20, .20], y in [.222, .7475]
PLATE_Z, PLATE_T = .240, .003                             # front face z, thickness (back face z = .237)
CHAMFER = .001                                            # 1 mm chamfer on the plate outline and on every cut-out
COL_UX = (.09, -.09)                                      # col 0 = viewer's left when looking at the front from +Z
ROW_UY = (.665, .545, .425, .305)                         # row 0 = top
SLOTS = [(COL_UX[s % 2], ROW_UY[s // 2]) for s in range(8)]   # s = row * 2 + col
CUT_W, CUT_H = .060, .100
BODY_W, BODY_H = .055, .095
Z_FRONT, Z_BACK = .285, .230                              # breaker front face / back (body stands on the tray floor at z = .230)
HINGE_Z = .283
RATINGS = (50, 30, 20, 20, 30, 20, 20, 20)                # printed ampere ratings; slot 0 is the 2P ELB main breaker
LABELS = ("MAIN", "1", "2", "3", "4", "5", "6", "7")
LEVER_LEN, LEVER_W, LEVER_T = .028, .030, .010

# face-print atlas: one 56 x 96 mm cell per breaker (front face is 52 x 92 mm, 2 mm padding), 8 px/mm, 4 x 2 cells
FACE_PX, CELL_MM, ATLAS = 8, (56, 96), (4, 2)
PLATE_TEX = (2048, 2691)                                  # 5.12 px/mm over 0.400 x 0.5255 m
BODY_RGB = (31, 31, 34)                                   # breaker ABS (sRGB), shared by the flat material and the print background
RAL7035 = (203, 208, 204)


# ============================================================ Pillow half: label textures
def textures_main(outdir, names):
    from PIL import Image, ImageDraw, ImageFont
    outdir = Path(outdir)
    outdir.mkdir(parents=True, exist_ok=True)

    def font(size):
        return ImageFont.truetype(str(FONT), size)

    def layer(text, size, fill, stroke=0):
        f = font(size)
        l, t, r, b = f.getbbox(text, stroke_width=stroke)
        im = Image.new("RGBA", (r - l + 6, b - t + 6), (0, 0, 0, 0))
        ImageDraw.Draw(im).text((3 - l, 3 - t), text, font=f, fill=fill, stroke_width=stroke, stroke_fill=fill)
        return im

    def fit(text, max_w, max_h, fill, stroke=0):
        """Largest font size whose text layer stays inside max_w x max_h (pixels)."""
        lo, hi = 8, 400
        while lo < hi:
            mid = (lo + hi + 1) // 2
            im = layer(text, mid, fill, stroke)
            if im.width <= max_w and im.height <= max_h:
                lo = mid
            else:
                hi = mid - 1
        return layer(text, lo, fill, stroke)

    def put(img, lay, cx, cy):
        img.alpha_composite(lay, (round(cx - lay.width / 2), round(cy - lay.height / 2)))

    # ---- plate: RAL 7035 sheet with the circuit labels, the shock sticker and the name plate.  Coordinates are Blender bx / bz in metres
    def plate_front():
        W, H = PLATE_TEX
        img = Image.new("RGBA", (W, H), RAL7035 + (255,))
        d = ImageDraw.Draw(img)
        sx, sy = W / (2 * PLATE_HALF_W), H / (PLATE_Y1 - PLATE_Y0)

        def X(bx):
            return (bx + PLATE_HALF_W) * sx

        def Y(bz):
            return (PLATE_Y1 - bz) * sy

        ink = (18, 18, 18)
        for s in range(8):
            col, row = s % 2, s // 2
            cx = -.16 if col == 0 else .16                     # label beside its breaker, on the outer margin
            cz = ROW_UY[row]
            d.rounded_rectangle([X(cx - .022), Y(cz + .010), X(cx + .022), Y(cz - .010)], radius=.002 * sx,
                                fill=(244, 244, 240), outline=(56, 58, 58), width=max(4, round(.0009 * sx)))
            put(img, fit(LABELS[s], .038 * sx, .0125 * sy, ink, stroke=3), X(cx), Y(cz))
        # 감전주의 sticker (top centre): yellow, black frame, warning triangle with a bolt
        zc = PLATE_Y1 - .0165
        d.rounded_rectangle([X(-.056), Y(zc + .012), X(.056), Y(zc - .012)], radius=.002 * sx, fill=(250, 204, 21),
                            outline=(18, 18, 18), width=max(3, round(.0009 * sx)))
        ix, iy, sc = X(-.037), Y(zc), .0092 * sx
        tri = [(ix, iy - .62 * sc), (ix - .78 * sc, iy + .52 * sc), (ix + .78 * sc, iy + .52 * sc)]
        d.polygon(tri, fill=(250, 204, 21), outline=(18, 18, 18), width=max(3, round(.0008 * sx)))
        bolt = [(.06, -.40), (-.22, .10), (-.02, .10), (-.12, .44), (.24, -.08), (.03, -.08), (.16, -.40)]
        d.polygon([(ix + a * sc, iy + b * sc * .95 + .06 * sc) for a, b in bolt], fill=(18, 18, 18))
        put(img, fit("감전주의", .066 * sx, .0135 * sy, ink, stroke=3), X(.014), Y(zc))
        # name plate (bottom strip)
        put(img, fit("분전반   AC 220V 60Hz", .250 * sx, .0085 * sy, (40, 42, 42), stroke=1), X(0), Y(PLATE_Y0 + .0165))
        return img

    # ---- breaker faces: printed white marks on the ABS colour; cell (a, b) origin = breaker front-face centre, mm, b up
    def face_cell(idx):
        W, H = CELL_MM[0] * FACE_PX, CELL_MM[1] * FACE_PX
        img = Image.new("RGBA", (W, H), BODY_RGB + (255,))
        d = ImageDraw.Draw(img)
        ink = (230, 230, 224)

        def at(a, b):
            return W / 2 + a * FACE_PX, H / 2 - b * FACE_PX

        def txt(text, a, b, max_w, max_h, stroke=0):
            put(img, fit(text, max_w * FACE_PX, max_h * FACE_PX, ink, stroke), *at(a, b))

        txt(f"{RATINGS[idx]}A", 0, 27, 38, 10.5, stroke=1)
        txt("ON", 0, 13.2, 18, 4.2, stroke=1)
        txt("OFF", 0, -13.2, 18, 4.2, stroke=1)
        if idx == 0:
            txt("ELB 2P 30mA", 0, 19.5, 44, 3.8)
            cx, cy = at(10, -25.5)
            r = 5.4 * FACE_PX
            d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(150, 150, 146), width=3)   # bezel of the TEST button
            txt("TEST", -9, -25.5, 18, 3.8)
            txt("누전차단기", -9, -31, 24, 3.6)
        else:
            txt("2P 220V", 0, 19.5, 44, 3.8)
            txt("MCCB", 0, -22, 30, 4.6, stroke=1)
            txt("배선용차단기", 0, -28.5, 34, 3.8)
        return img

    def face_atlas():
        cw, ch = CELL_MM[0] * FACE_PX, CELL_MM[1] * FACE_PX
        img = Image.new("RGB", (cw * ATLAS[0], ch * ATLAS[1]), BODY_RGB)
        for s in range(8):
            img.paste(face_cell(s).convert("RGB"), ((s % ATLAS[0]) * cw, (s // ATLAS[0]) * ch))
        return img

    makers = {"plate": plate_front, "face": face_atlas}
    for name in names:
        for key in TEXTURES.get(name, ()):
            makers[key]().convert("RGB").save(outdir / f"{name}_{key}.png")
            print("CG_TEXTURE", name, key, flush=True)


if not IN_BLENDER:
    if len(sys.argv) > 2 and sys.argv[1] == "--textures":
        textures_main(sys.argv[2], sys.argv[3:])
    else:
        print(__doc__)
    sys.exit(0)


# ============================================================ Blender half
def S(*c):
    """sRGB -> linear (what Blender's Base Color holds; the JSON keeps the sRGB value)."""
    return tuple(((v + .055) / 1.055) ** 2.4 if v > .04045 else v / 12.92 for v in c)


def rgb(*c):
    return tuple(v / 255 for v in c)


def paint(c, r=.45, m=0., tex=None):
    return {"c": c, "m": m, "r": r, "tex": tex}


MAT = {
    "Plate_Paint": paint(rgb(*RAL7035), .55, .02),
    "Plate_Front": paint((1, 1, 1), .55, .02, tex="plate"),
    "Breaker_Body": paint(rgb(*BODY_RGB), .36),
    "Breaker_Face": paint((1, 1, 1), .36, tex="face"),
    "Breaker_Void": paint((.016, .016, .018), .85),
    "Screw_Steel": paint((.70, .72, .74), .35, .9),
    "Screw_Slot": paint((.05, .05, .055), .7),
    "Test_Button": paint((.95, .58, .05), .32),
    "Lever_ABS": paint((.035, .035, .040), .30),
}


# ------------------------------------------------------------ mesh building blocks
class Part:
    """One prop: a single bmesh, material slots by name."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.uvl = self.bm.loops.layers.uv.new("UVMap")

    def mat(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def face(self, verts, mat, smooth=False):
        f = self.bm.faces.new(verts)
        f.material_index = self.mat(mat)
        f.smooth = smooth
        return f


def pt(x, zf, z):
    """Blender point from bx, Unity depth zf (out of the wall; Blender y = -zf) and bz."""
    return Vector((x, -zf, z))


def facep(p, mat, pts, n, uv=None, smooth=False):
    """Flat polygon whose winding is flipped, if needed, so that its normal points along n (a desired direction)."""
    vs = [Vector(q) for q in pts]
    nn = Vector()
    for i in range(len(vs)):
        a, b = vs[i], vs[(i + 1) % len(vs)]
        nn += Vector(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
    order = list(range(len(vs)))
    if nn.dot(Vector(n)) < 0:
        order.reverse()
    f = p.face([p.bm.verts.new(vs[i]) for i in order], mat, smooth)
    if uv:
        for loop, i in zip(f.loops, order):
            loop[p.uvl].uv = uv[i]
    return f


def rect_y(p, mat, x0, z0, x1, z1, zf, front=True, uvf=None):
    """Rectangle in the plane of depth zf, facing the front (Blender -Y) or the back."""
    c = ((x0, z0), (x1, z0), (x1, z1), (x0, z1))
    return facep(p, mat, [pt(x, zf, z) for x, z in c], (0, -1 if front else 1, 0), [uvf(x, z) for x, z in c] if uvf else None)


def wall_x(p, mat, x, z0, z1, za, zb, nx):
    """Wall in the plane x = const between depths za and zb, normal sign nx."""
    return facep(p, mat, [pt(x, za, z0), pt(x, zb, z0), pt(x, zb, z1), pt(x, za, z1)], (nx, 0, 0))


def wall_z(p, mat, z, x0, x1, za, zb, nz):
    return facep(p, mat, [pt(x0, za, z), pt(x1, za, z), pt(x1, zb, z), pt(x0, zb, z)], (0, 0, nz))


def rect_walls(p, mat, x0, z0, x1, z1, za, zb, inward):
    """Four walls round a rectangle between depths za and zb; normals point into the rectangle (a hole) or out of it (a block)."""
    s = 1 if inward else -1
    wall_x(p, mat, x0, z0, z1, za, zb, s)
    wall_x(p, mat, x1, z0, z1, za, zb, -s)
    wall_z(p, mat, z0, x0, x1, za, zb, s)
    wall_z(p, mat, z1, x0, x1, za, zb, -s)


def lathe(p, mat, prof, o, w, ref=(0, 0, 1), seg=16, sharp=()):
    """Surface of revolution about the axis o + t*w. prof [(r, t)] traversed with the solid on its left (outer wall up, cap inward);
    r == 0 makes an apex vertex (cap fan). sharp = ring indices whose edges are hard."""
    o = Vector(o)
    w = Vector(w).normalized()
    u = Vector(ref) - w * Vector(ref).dot(w)
    u.normalize()
    v = w.cross(u)
    rings = []
    for r, t in prof:
        if r <= 1e-9:
            rings.append([p.bm.verts.new(o + w * t)])
            continue
        rings.append([p.bm.verts.new(o + w * t + (u * math.cos(2 * math.pi * i / seg) + v * math.sin(2 * math.pi * i / seg)) * r)
                      for i in range(seg)])
    for k in range(len(prof) - 1):
        a, b = rings[k], rings[k + 1]
        for i in range(seg):
            j = (i + 1) % seg
            vs = [a[i if len(a) > 1 else 0], a[j if len(a) > 1 else 0], b[j if len(b) > 1 else 0], b[i if len(b) > 1 else 0]]
            quad = []
            for x in vs:
                if x not in quad:
                    quad.append(x)
            p.face(quad, mat, True)
    for k in sharp:
        ring = rings[k]
        if len(ring) > 1:
            for i in range(len(ring)):
                e = p.bm.edges.get((ring[i], ring[(i + 1) % len(ring)]))
                if e:
                    e.smooth = False


def screw(p, o, r, h):
    """Pan-head Phillips screw standing on a face at o, pointing to the front; the cross is a 0.15 mm dark decal on the flat top."""
    lathe(p, "Screw_Steel", [(r, 0), (r, h * .5), (r * .84, h), (0, h)], o, (0, -1, 0), seg=10, sharp=(1, 2))
    zt = -o.y + h + .00015           # depth of the decal plane
    cx, cz, L, W = o.x, o.z, r * .62, r * .13
    for hx, hz in ((L, W), (W, L)):
        rect_y(p, "Screw_Slot", cx - hx, cz - hz, cx + hx, cz + hz, zt)


# ------------------------------------------------------------ BreakerDeck
def face_uv(s, a_mm, b_mm):
    cw, ch = CELL_MM
    px = ((s % ATLAS[0]) * cw + cw / 2 + a_mm) * FACE_PX
    py = ((s // ATLAS[0]) * ch + ch / 2 - b_mm) * FACE_PX
    return px / (ATLAS[0] * cw * FACE_PX), 1 - py / (ATLAS[1] * ch * FACE_PX)


def slot_bl(s):
    """Blender x, z of a breaker slot centre."""
    ux, uy = SLOTS[s]
    return -ux, uy


def build_plate(p):
    c = CHAMFER
    zf, zc, zb = PLATE_Z, PLATE_Z - c, PLATE_Z - PLATE_T
    cuts = []
    for s in range(8):
        sx, sz = slot_bl(s)
        cuts.append((sx - CUT_W / 2, sz - CUT_H / 2, sx + CUT_W / 2, sz + CUT_H / 2))
    hw, y0, y1 = PLATE_HALF_W, PLATE_Y0, PLATE_Y1
    X = sorted({-hw + c, hw - c} | {x for r in cuts for x in (r[0] - c, r[2] + c)})
    Z = sorted({y0 + c, y1 - c} | {z for r in cuts for z in (r[1] - c, r[3] + c)})
    Xo, Zo = [-hw] + X[1:-1] + [hw], [y0] + Z[1:-1] + [y1]        # outline of the chamfer's outer edge, same subdivision as the front grid

    def uvf(x, z):
        return (x + hw) / (2 * hw), (z - y0) / (y1 - y0)

    # front face: grid of quads, cut-outs left open
    for i in range(len(X) - 1):
        for j in range(len(Z) - 1):
            cx, cz = (X[i] + X[i + 1]) / 2, (Z[j] + Z[j + 1]) / 2
            if any(r[0] - c < cx < r[2] + c and r[1] - c < cz < r[3] + c for r in cuts):
                continue
            rect_y(p, "Plate_Front", X[i], Z[j], X[i + 1], Z[j + 1], zf, True, uvf)
    # outer chamfer and outer side wall (grid intervals shared with the front face so no T-junctions)
    for i in range(len(X) - 1):
        for y, yi, n in ((y0, y0 + c, (0, -1, -1)), (y1, y1 - c, (0, -1, 1))):
            facep(p, "Plate_Paint", [pt(Xo[i], zc, y), pt(Xo[i + 1], zc, y), pt(X[i + 1], zf, yi), pt(X[i], zf, yi)], n)
            wall_z(p, "Plate_Paint", y, Xo[i], Xo[i + 1], zc, zb, -1 if y == y0 else 1)
    for j in range(len(Z) - 1):
        for x, xi, n in ((-hw, -hw + c, (-1, -1, 0)), (hw, hw - c, (1, -1, 0))):
            facep(p, "Plate_Paint", [pt(x, zc, Zo[j]), pt(x, zc, Zo[j + 1]), pt(xi, zf, Z[j + 1]), pt(xi, zf, Z[j])], n)
            wall_x(p, "Plate_Paint", x, Zo[j], Zo[j + 1], zc, zb, -1 if x < 0 else 1)
    # back face
    Xb = sorted(set(Xo) | {x for r in cuts for x in (r[0], r[2])})
    Zb = sorted(set(Zo) | {z for r in cuts for z in (r[1], r[3])})
    for i in range(len(Xb) - 1):
        for j in range(len(Zb) - 1):
            cx, cz = (Xb[i] + Xb[i + 1]) / 2, (Zb[j] + Zb[j + 1]) / 2
            if any(r[0] < cx < r[2] and r[1] < cz < r[3] for r in cuts):
                continue
            rect_y(p, "Plate_Paint", Xb[i], Zb[j], Xb[i + 1], Zb[j + 1], zb, False)
    # cut-outs: chamfer, sheet edge (paint), dark tray wall down to the tray floor the breaker stands on
    for x0, z0, x1, z1 in cuts:
        ox0, oz0, ox1, oz1 = x0 - c, z0 - c, x1 + c, z1 + c
        facep(p, "Plate_Paint", [pt(ox0, zf, oz0), pt(ox1, zf, oz0), pt(x1, zc, z0), pt(x0, zc, z0)], (0, -1, 1))
        facep(p, "Plate_Paint", [pt(ox0, zf, oz1), pt(ox1, zf, oz1), pt(x1, zc, z1), pt(x0, zc, z1)], (0, -1, -1))
        facep(p, "Plate_Paint", [pt(ox0, zf, oz0), pt(ox0, zf, oz1), pt(x0, zc, z1), pt(x0, zc, z0)], (1, -1, 0))
        facep(p, "Plate_Paint", [pt(ox1, zf, oz0), pt(ox1, zf, oz1), pt(x1, zc, z1), pt(x1, zc, z0)], (-1, -1, 0))
        rect_walls(p, "Plate_Paint", x0, z0, x1, z1, zc, zb, True)
        rect_walls(p, "Breaker_Void", x0, z0, x1, z1, zb, Z_BACK, True)
        rect_y(p, "Breaker_Void", x0, z0, x1, z1, Z_BACK)
    # four corner screws
    for sxn in (-1, 1):
        for zz in (y1 - .0165, y0 + .0165):
            screw(p, pt(sxn * .183, zf, zz), .0040, .0022)


def build_breaker(p, s):
    sx, sz = slot_bl(s)
    hw, hh = BODY_W / 2, BODY_H / 2
    fw, fh = .026, .046                                     # front face 52 x 92 mm inside the 1.5 mm chamfer
    zf, zc, zb = Z_FRONT, Z_FRONT - .0015, Z_BACK

    def uvf(x, z):
        return face_uv(s, (x - sx) * 1000, (z - sz) * 1000)

    def face(a0, b0, a1, b1):
        rect_y(p, "Breaker_Face", sx + a0, sz + b0, sx + a1, sz + b1, zf, True, uvf)

    # body: four sides down to the tray floor (no back face: it stands on it), with a parting line (cover / base) 1 mm wide and
    # 0.6 mm deep, 22.5 mm behind the front face, and the chamfer up to the front face
    zg0, zg1, gi = .2615, .2625, .0006
    rect_walls(p, "Breaker_Body", sx - hw, sz - hh, sx + hw, sz + hh, zb, zg0, False)
    rect_walls(p, "Breaker_Body", sx - hw + gi, sz - hh + gi, sx + hw - gi, sz + hh - gi, zg0, zg1, False)
    rect_walls(p, "Breaker_Body", sx - hw, sz - hh, sx + hw, sz + hh, zg1, zc, False)
    for depth, front in ((zg0, False), (zg1, True)):
        o, i = (sx - hw, sz - hh, sx + hw, sz + hh), (sx - hw + gi, sz - hh + gi, sx + hw - gi, sz + hh - gi)
        rect_y(p, "Breaker_Body", o[0], o[1], o[2], i[1], depth, front)
        rect_y(p, "Breaker_Body", o[0], i[3], o[2], o[3], depth, front)
        rect_y(p, "Breaker_Body", o[0], i[1], i[0], i[3], depth, front)
        rect_y(p, "Breaker_Body", i[2], i[1], o[2], i[3], depth, front)
    for pts, n in (([(-hw, zc, hh), (hw, zc, hh), (fw, zf, fh), (-fw, zf, fh)], (0, -1, 1)),
                   ([(-hw, zc, -hh), (hw, zc, -hh), (fw, zf, -fh), (-fw, zf, -fh)], (0, -1, -1)),
                   ([(-hw, zc, -hh), (-hw, zc, hh), (-fw, zf, fh), (-fw, zf, -fh)], (-1, -1, 0)),
                   ([(hw, zc, -hh), (hw, zc, hh), (fw, zf, fh), (fw, zf, -fh)], (1, -1, 0))):
        facep(p, "Breaker_Body", [pt(sx + a, d, sz + b) for a, d, b in pts], n)
    # front face: middle band round the lever window, terminal strips with a square screw pocket on each side of centre.
    # Quads that meet the band are split at the window edges (+-wa) and the centre so every joint shares its vertices.
    wa, wb = .017, .009                                     # lever window half sizes

    def row(cuts, b0, b1):
        for a0, a1 in zip(cuts, cuts[1:]):
            face(a0, b0, a1, b1)

    row((-fw, -wa, 0, wa, fw), wb, .033)
    row((-fw, -wa, 0, wa, fw), -.033, -wb)
    face(-fw, -wb, -wa, wb)
    face(wa, -wb, fw, wb)
    ph, pv, pd = .0055, .00425, .004                        # terminal pocket half width / half height / depth
    for sign in (1, -1):                                    # top and bottom strip
        b0, b1 = (.033, fh) if sign > 0 else (-fh, -.033)
        bc = (b0 + b1) / 2
        for ac in (-.013, .013):
            a0, a1 = (-fw, 0) if ac < 0 else (0, fw)
            cuts = (a0, -wa, a1) if ac < 0 else (a0, wa, a1)
            row(cuts if sign > 0 else (a0, a1), b0, bc - pv)      # the quad on the band side is the split one
            row(cuts if sign < 0 else (a0, a1), bc + pv, b1)
            face(a0, bc - pv, ac - ph, bc + pv)
            face(ac + ph, bc - pv, a1, bc + pv)
            x0, z0, x1, z1 = sx + ac - ph, sz + bc - pv, sx + ac + ph, sz + bc + pv
            rect_walls(p, "Breaker_Void", x0, z0, x1, z1, zf, zf - pd, True)
            rect_y(p, "Breaker_Void", x0, z0, x1, z1, zf - pd)
            screw(p, pt(sx + ac, zf - pd, sz + bc), .0036, .0020)
    # recessed lever slot
    zs = .2775
    x0, z0, x1, z1 = sx - wa, sz - wb, sx + wa, sz + wb
    rect_walls(p, "Breaker_Void", x0, z0, x1, z1, zf, zs, True)
    rect_y(p, "Breaker_Void", x0, z0, x1, z1, zs)
    if s == 0:                                              # ELB test button (bezel is printed)
        lathe(p, "Test_Button", [(.0042, 0), (.0042, .0009), (.0037, .0013), (0, .0013)], pt(sx + .010, zf, sz - .0255), (0, -1, 0),
              seg=14, sharp=(1,))


def build_BreakerDeck(p):
    build_plate(p)
    for s in range(8):
        build_breaker(p, s)


# ------------------------------------------------------------ BreakerLever
def lever_profile():
    """Closed (y, z) profile in metres, hinge at the origin: the lever extends towards -Y (front); z is its thickness. Returns
    (points ccw, indices whose edge along the lever's width stays smooth)."""
    def hth(L):
        return .0050 - .0007 * L / .026            # half thickness: 5.0 mm at the hinge, 4.3 mm near the tip

    rc, Lc = .0022, LEVER_LEN - .0022               # tip corner radius / start of the rounding
    tc = hth(Lc) - rc
    grooves = (.012, .0165, .021)
    pts, smooth = [], set()
    for k in range(9):                              # barrel round the hinge, top -> back -> bottom
        phi = math.pi * k / 8
        smooth.add(len(pts))
        pts.append((-.0050 * math.sin(phi), .0050 * math.cos(phi)))
    for L0 in grooves:                              # bottom side towards the tip, finger grooves 0.6 mm deep
        h = hth(L0)
        pts += [(L0, -h), (L0 + .0003, -h + .0006), (L0 + .0013, -h + .0006), (L0 + .0016, -h)]
    for k in range(4):                              # bottom tip corner
        phi = math.radians(30 * k)
        smooth.add(len(pts))
        pts.append((Lc + rc * math.sin(phi), -tc - rc * math.cos(phi)))
    for k in range(3, -1, -1):                      # top tip corner
        phi = math.radians(30 * k)
        smooth.add(len(pts))
        pts.append((Lc + rc * math.sin(phi), tc + rc * math.cos(phi)))
    for L0 in reversed(grooves):                    # top side back to the hinge
        h = hth(L0)
        pts += [(L0 + .0016, h), (L0 + .0013, h - .0006), (L0 + .0003, h - .0006), (L0, h)]
    prof = [(-L, t) for L, t in pts]                # length L towards the front = -Y
    area = sum(prof[i][0] * prof[(i + 1) % len(prof)][1] - prof[(i + 1) % len(prof)][0] * prof[i][1] for i in range(len(prof)))
    if area < 0:
        prof.reverse()
        smooth = {len(prof) - 1 - i for i in smooth}
    return prof, smooth


def build_BreakerLever(p):
    prof, smooth = lever_profile()
    n = len(prof)
    cy = -.012                                      # scale the end stations about the middle of the lever: a bevel round the two ends
    stations = [(-LEVER_W / 2, .84), (-LEVER_W / 2 + .0012, 1.0), (LEVER_W / 2 - .0012, 1.0), (LEVER_W / 2, .84)]
    rings = [[p.bm.verts.new((x, cy + (y - cy) * sc, z * sc)) for y, z in prof] for x, sc in stations]
    for k in range(len(stations) - 1):
        for i in range(n):
            j = (i + 1) % n
            p.face([rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]], "Lever_ABS", True)
    for ring in rings:                              # every station is a hard edge; the profile keeps its own smooth stretches
        for i in range(n):
            p.bm.edges.get((ring[i], ring[(i + 1) % n])).smooth = False
    for k in range(len(rings) - 1):
        for i in range(n):
            if i not in smooth:
                p.bm.edges.get((rings[k][i], rings[k + 1][i])).smooth = False
    p.face(list(reversed(rings[0])), "Lever_ABS", False)
    p.face(list(rings[-1]), "Lever_ABS", False)


BUILD = {"BreakerLever": (build_BreakerLever, 6000), "BreakerDeck": (build_BreakerDeck, 6000)}


# ============================================================ realise, export, check, preview
def make_material(name):
    spec = MAT[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*S(*spec["c"]), 1)
    bsdf.inputs["Metallic"].default_value = spec["m"]
    bsdf.inputs["Roughness"].default_value = spec["r"]
    return mat


def realise(p):
    """bmesh -> one triangulated mesh object with material slots."""
    bm = p.bm
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    mesh = bpy.data.meshes.new(p.name)
    bm.to_mesh(mesh)
    bm.free()
    for name in p.mats:
        mesh.materials.append(make_material(name))
    ob = bpy.data.objects.new(p.name, mesh)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def constant_map(path, metallic, roughness):
    """4 x 4 MetallicSmoothness map of one material (R metallic, A 1 - roughness)."""
    out = bpy.data.images.new(path.stem, 4, 4, alpha=True, float_buffer=False)
    out.colorspace_settings.name = "Non-Color"
    px = np.zeros((4, 4, 4), dtype=np.float32)
    px[..., 0] = metallic
    px[..., 3] = 1 - roughness
    out.pixels = px.ravel()
    out.filepath_raw = str(path)
    out.file_format = "PNG"
    out.save()
    return path.name


def export_materials(ob, name, folder):
    entries = []
    for i, mat in enumerate(ob.data.materials):
        spec = MAT[mat.name]
        entry = {"name": f"m{i}", "source": mat.name, "baseColor": [1, 1, 1, 1], "metallic": round(spec["m"], 3),
                 "roughness": round(spec["r"], 3), "albedo": None, "normal": None, "metallicSmoothness": None,
                 "alphaMode": "OPAQUE", "faces": sum(1 for q in ob.data.polygons if q.material_index == i)}
        if spec["tex"]:
            dst = folder / f"{name}_m{i}_Color.png"
            shutil.copyfile(TEX / f"{name}_{spec['tex']}.png", dst)
            entry["albedo"] = dst.name
            bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
            node = mat.node_tree.nodes.new("ShaderNodeTexImage")
            node.image = bpy.data.images.load(str(dst))
            mat.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
        else:
            entry["baseColor"] = [round(v, 4) for v in (*spec["c"], 1.0)]
        entry["metallicSmoothness"] = constant_map(folder / f"{name}_m{i}_MetallicSmoothness.png", spec["m"], spec["r"])
        mat.name = f"m{i}"
        entries.append(entry)
    return entries


def obj_unity_vertices(path):
    """Vertices of the exported OBJ as Unity sees them: the importer mirrors x, so Unity = (-ox, oy, oz)."""
    rows = [ln.split() for ln in Path(path).read_text().splitlines() if ln.startswith("v ")]
    return np.array([(-float(r[1]), float(r[2]), float(r[3])) for r in rows])


def fail(msg):
    raise RuntimeError("CHECK FAILED: " + msg)


def check_deck(folder, info):
    v = obj_unity_vertices(folder / "BreakerDeck.obj")
    lo, hi = v.min(axis=0), v.max(axis=0)
    print(f"CG_CHECK deck bounds x[{lo[0]:.4f},{hi[0]:.4f}] y[{lo[1]:.4f},{hi[1]:.4f}] z[{lo[2]:.4f},{hi[2]:.4f}] (must stay in z >= .21)")
    if lo[2] < .21:
        fail("deck reaches behind z = .21")
    win = v[(v[:, 2] >= PLATE_Z - PLATE_T - 1e-4) & (v[:, 2] <= PLATE_Z + 1e-4)]   # plate, cut-out walls and the screw feet
    plo, phi = win.min(axis=0), win.max(axis=0)
    zfront = np.unique(np.round(v[np.isclose(np.abs(v[:, 0]), PLATE_HALF_W - CHAMFER, atol=1e-6), 2], 6))
    print(f"CG_CHECK plate x[{plo[0]:.4f},{phi[0]:.4f}] y[{plo[1]:.4f},{phi[1]:.4f}] z[{plo[2]:.4f},{phi[2]:.4f}] front face z={list(zfront)}")
    ok = (abs(plo[0] + .20) < 1e-3 and abs(phi[0] - .20) < 1e-3 and abs(plo[1] - PLATE_Y0) < 1e-3 and abs(phi[1] - PLATE_Y1) < 1e-3
          and list(zfront) == [PLATE_Z])
    if not ok:
        fail("plate bounds / front face")
    worst = 0.0
    for s, (ux, uy) in enumerate(SLOTS):
        def box(z, sel_hw, sel_hh):
            m = ((abs(v[:, 0] - ux) <= sel_hw) & (abs(v[:, 1] - uy) <= sel_hh) & (abs(v[:, 2] - z) <= 2e-4))
            a, b = v[m].min(axis=0), v[m].max(axis=0)
            return (a[0] + b[0]) / 2, (a[1] + b[1]) / 2, b[0] - a[0], b[1] - a[1]
        bx, by, bw, bh = box(Z_FRONT, .0265, .0465)                        # front face of the breaker
        cx, cy, cw, ch = box(PLATE_Z - CHAMFER, .0305, .0505)              # cut-out edge
        ox, oy, ow, oh = box(Z_FRONT - .0015, .0280, .0480)                # body outline
        d = max(abs(bx - ux), abs(by - uy), abs(cx - ux), abs(cy - uy), abs(ox - ux), abs(oy - uy),
                abs(cw - CUT_W), abs(ch - CUT_H), abs(ow - BODY_W), abs(oh - BODY_H))
        worst = max(worst, d)
        print(f"CG_CHECK slot {s}: target ({ux:+.3f},{uy:.3f})  breaker face centre ({bx:+.4f},{by:.4f})  cut-out centre ({cx:+.4f},{cy:.4f}) "
              f"size {cw:.4f}x{ch:.4f}  body {ow:.4f}x{oh:.4f}  max dev {d * 1000:.3f} mm")
    print(f"CG_CHECK worst deviation over 8 slots: {worst * 1000:.3f} mm (limit 1 mm)")
    if worst > 1e-3:
        fail("slot layout off by more than 1 mm")


def check_lever(folder):
    v = obj_unity_vertices(folder / "BreakerLever.obj")
    lo, hi = v.min(axis=0), v.max(axis=0)
    print(f"CG_CHECK lever bounds x[{lo[0]:.4f},{hi[0]:.4f}] y[{lo[1]:.4f},{hi[1]:.4f}] z[{lo[2]:.4f},{hi[2]:.4f}]  (hinge axis = origin, tip at z={hi[2]:.4f})")
    if not (abs(hi[0] + lo[0]) < 1e-6 and abs(hi[0] - LEVER_W / 2) < 1e-4 and abs(hi[2] - LEVER_LEN) < 1e-4 and abs(hi[1] + lo[1]) < 2e-4):
        fail("lever bounds")
    front = v[:, 2] >= LEVER_LEN - 1e-6                      # the end face of the tip
    for ang in (25, -25):                                   # Unity Euler(ang, 0, 0): y' = y cos - z sin, z' = y sin + z cos
        a = math.radians(ang)
        y2 = v[:, 1] * math.cos(a) - v[:, 2] * math.sin(a)
        z2 = v[:, 1] * math.sin(a) + v[:, 2] * math.cos(a) + HINGE_Z
        inside = z2 <= Z_FRONT                              # part of the lever inside the slot depth: must clear the 34 x 18 mm window
        clear = min(.017 - np.abs(v[:, 0]).max(), .009 - np.abs(y2[inside]).max())
        floor = z2.min() - .2775                            # and stay above the slot floor
        print(f"CG_CHECK lever Euler({ang:+d},0,0): tip z={z2.max():.4f} (limit .317)  tip y offset {y2[front].mean() * 1000:+.1f} mm "
              f"({'up' if y2[front].mean() > 0 else 'DOWN'})  window clearance {clear * 1000:.2f} mm  slot floor clearance {floor * 1000:.2f} mm")
        if z2.max() >= .317 or clear <= 0 or floor <= 0:
            fail(f"lever at {ang} deg")


def convert(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    fn, budget = BUILD[name]
    p = Part(name)
    fn(p)
    ob = realise(p)
    folder = OUT / name
    folder.mkdir(parents=True, exist_ok=True)
    for old in folder.glob(name + "_m*_*.png"):
        old.unlink()
    materials = export_materials(ob, name, folder)
    co = np.array([v.co[:] for v in ob.data.vertices])
    lo, hi = co.min(axis=0), co.max(axis=0)
    tris = len(ob.data.polygons)
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.ops.wm.obj_export(filepath=str(folder / (name + ".obj")), export_selected_objects=True, export_materials=True,
                          path_mode="STRIP", forward_axis="NEGATIVE_Z", up_axis="Y", export_uv=True, export_normals=True,
                          export_triangulated_mesh=True)
    size = [round(float(hi[0] - lo[0]), 4), round(float(hi[2] - lo[2]), 4), round(float(hi[1] - lo[1]), 4)]
    info = {"name": name, "source": "make_breakers.py (procedural, Blender)", "scale": 1.0, "triangles": tris,
            "materials": materials, "size": size, "front": "+z"}
    if name == "BreakerDeck":
        info["slots"] = [[ux, uy] for ux, uy in SLOTS]
        info["plate"] = {"z": PLATE_Z, "min": [-PLATE_HALF_W, PLATE_Y0, PLATE_Z - PLATE_T], "max": [PLATE_HALF_W, PLATE_Y1, PLATE_Z]}
        info["ratings"] = list(RATINGS)
    if name == "BreakerLever":
        info["hinge"] = [0.0, 0.0, 0.0]
        info["length"] = LEVER_LEN
        info["unityEulerX"] = {"tipUp": -25, "tipDown": 25}   # Unity's +X rotation pitches +Z down: Euler(+25,0,0) = tip DOWN
    (folder / (name + ".json")).write_text(json.dumps(info, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    flag = "" if tris <= budget else f" OVER_BUDGET({budget})"
    print(f"CG_PROP {name} tris={tris} size={size[0]},{size[1]},{size[2]}{flag}", flush=True)
    if name == "BreakerDeck":
        check_deck(folder, info)
    else:
        check_lever(folder)
    preview(name)


# ---- previews: re-import the exported OBJ with its JSON materials, so the files themselves are what gets looked at
def import_prop(name):
    folder = OUT / name
    info = json.loads((folder / (name + ".json")).read_text(encoding="utf-8"))
    before = set(bpy.context.scene.objects)
    bpy.ops.wm.obj_import(filepath=str(folder / (name + ".obj")), forward_axis="NEGATIVE_Z", up_axis="Y")
    obs = [o for o in bpy.context.scene.objects if o not in before]
    for ob in obs:
        for slot in ob.material_slots:
            mat = slot.material
            entry = next((m for m in info["materials"] if m["name"] == mat.name), None)
            if not entry:
                continue
            mat.use_nodes = True
            mat.use_backface_culling = True                 # flipped faces show up as holes
            bsdf = mat.node_tree.nodes.get("Principled BSDF")
            if entry.get("albedo"):
                tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
                tex.image = bpy.data.images.load(str(folder / entry["albedo"]))
                mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
            else:
                bsdf.inputs["Base Color"].default_value = (*S(*entry["baseColor"][:3]), 1)
            bsdf.inputs["Metallic"].default_value = entry["metallic"]
            bsdf.inputs["Roughness"].default_value = entry["roughness"]
    return obs, info


def preview(name):
    from mathutils import Vector as V
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    PREV.mkdir(parents=True, exist_ok=True)
    obs, info = import_prop(name)
    sc.render.engine = "BLENDER_EEVEE"
    sc.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("w")
    sc.world = world
    world.color = (.16, .17, .19)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    key = bpy.data.objects.new("key", bpy.data.lights.new("key", "AREA"))
    sc.collection.objects.link(key)
    fill = bpy.data.objects.new("fill", bpy.data.lights.new("fill", "SUN"))
    sc.collection.objects.link(fill)
    fill.rotation_euler = (math.radians(-60), math.radians(20), math.radians(25))
    fill.data.energy = .15

    def shoot(tag, loc, target, lens=50, res=(1280, 960), energy=28):
        target, loc = V(target), V(loc)
        cam.data.lens = lens
        cam.data.clip_start, cam.data.clip_end = .01, 20
        cam.location = loc
        cam.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()
        key.location = target + V((-.5, -1.0, .7)).normalized() * 1.5   # same light distance for every shot: same exposure
        key.rotation_euler = (target - key.location).to_track_quat("-Z", "Y").to_euler()
        key.data.size = 1.0
        key.data.energy = energy
        sc.render.resolution_x, sc.render.resolution_y = res
        sc.render.filepath = str(PREV / f"{name}_{tag}.png")
        bpy.ops.render.render(write_still=True)
        print("CG_PREVIEW", sc.render.filepath, flush=True)

    if name == "BreakerLever":
        lever = obs[0]
        for k, ang in enumerate((-25, 0, 25)):              # Blender +X rotation = tip down (same as Unity Euler(+a, 0, 0))
            o = bpy.data.objects.new(f"lever{k}", lever.data)
            sc.collection.objects.link(o)
            o.location = ((k - 1) * .045, 0, 0)
            o.rotation_euler = (lever.rotation_euler.x + math.radians(ang), 0, 0)   # the importer leaves its 90 deg axis fix on the object
        bpy.data.objects.remove(lever, do_unlink=True)
        shoot("persp", (-.11, -.20, .11), (0, -.010, 0), lens=60, res=(1200, 800))
        shoot("side", (.24, -.012, 0), (0, -.012, 0), lens=80, res=(1000, 700))
        for m in bpy.data.materials:                        # clay copy: the black ABS hides the form
            m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.45, .45, .47, 1)
        shoot("persp_clay", (-.11, -.20, .11), (0, -.010, 0), lens=60, res=(1200, 800))
        shoot("side_clay", (.24, -.012, 0), (0, -.012, 0), lens=80, res=(1000, 700))
        return

    lever_obs, _ = import_prop("BreakerLever")
    lever = lever_obs[0]
    states = (-25, -25, 25, -25, -25, 25, -25, -25)        # -25 = tip up (ON), +25 = tip down (OFF), Blender +X rotation = tip down
    for s in range(8):
        ux, uy = info["slots"][s]
        o = bpy.data.objects.new(f"lever{s}", lever.data)
        sc.collection.objects.link(o)
        o.location = (-ux, -HINGE_Z, uy)
        o.rotation_euler = (lever.rotation_euler.x + math.radians(states[s]), 0, 0)
    bpy.data.objects.remove(lever, do_unlink=True)
    back = bpy.data.meshes.new("back")                      # the dark board interior behind the plate
    back.from_pydata([(-.30, -.20, .08), (.30, -.20, .08), (.30, -.20, .85), (-.30, -.20, .85)], [], [(0, 1, 2, 3)])
    bo = bpy.data.objects.new("back", back)
    sc.collection.objects.link(bo)
    bm = bpy.data.materials.new("back")
    bm.use_nodes = True
    bm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.03, .03, .03, 1)
    back.materials.append(bm)
    cz = (PLATE_Y0 + PLATE_Y1) / 2
    shoot("front", (0, -2.2, cz), (0, -.24, cz), lens=110, res=(1000, 1300))
    shoot("threequarter", (.62, -1.0, .78), (0, -.26, cz), lens=45)
    shoot("closeup", (.20, -.62, .70), (-.035, -.27, .60), lens=50)
    shoot("closeup2", (-.02, -.40, .70), (-.07, -.26, .66), lens=50)
    shoot("closeup_main", (-.16, -.44, .77), (-.10, -.27, .67), lens=65)
    shoot("lowangle", (-.55, -.65, .30), (-.05, -.26, .40), lens=45)
    bpy.data.objects.remove(bo, do_unlink=True)              # the deck in its board
    before = set(sc.objects)
    bpy.ops.wm.obj_import(filepath=str(BOARD_OBJ), forward_axis="NEGATIVE_Z", up_axis="Y")
    for o in [o for o in sc.objects if o not in before]:
        for slot in o.material_slots:
            slot.material.use_backface_culling = False
    shoot("board", (.55, -1.2, .95), (0, -.20, cz - .02), lens=42)


def main():
    names = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = names or ORDER
    need = [n for n in names if n in TEXTURES]
    if need:
        subprocess.run([str(PILPY), str(Path(__file__).resolve()), "--textures", str(TEX), *need], check=True)
    for name in names:
        try:
            convert(name)
        except Exception as e:
            import traceback
            traceback.print_exc()
            print("CG_FAILED", name, repr(e), flush=True)


main()
