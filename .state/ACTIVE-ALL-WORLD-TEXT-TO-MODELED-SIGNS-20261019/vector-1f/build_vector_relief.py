#!/opt/homebrew/bin/python3
"""Construct continuous, closed, untextured source-art relief meshes.

Only this lane's output directory is written. Original source bytes are read-only.
"""
from pathlib import Path
from collections import Counter, defaultdict
import hashlib
import json
import math
import re
import time

import cv2
import numpy as np
from PIL import Image, ImageDraw
import shapely
from shapely.geometry import Polygon, box, LineString
from shapely.geometry.polygon import orient
from shapely.ops import polygonize

BASE = Path(__file__).resolve().parent
SOURCE = BASE.parent / 'RASTER-SIGNS-1F.json'
ROOT = BASE.parents[2]
UPSCALE = 3
EPSILON_SOURCE_PX = 0.45
MIN_COMPONENT_AREA_SOURCE_PX = 0.60
MIN_HOLE_AREA_SOURCE_PX = 0.40


def polygon_parts(g):
    if g.is_empty:
        return []
    if g.geom_type == 'Polygon':
        return [g]
    if hasattr(g, 'geoms'):
        return [p for sub in g.geoms for p in polygon_parts(sub)]
    return []


def artwork_image(rgb, uv):
    ih, iw = rgb.shape[:2]
    uv = np.asarray(uv, dtype=float)
    u0, v0 = uv.min(axis=0)
    u1, v1 = uv.max(axis=0)
    x0, y0, x1, y1 = u0 * iw, (1 - v1) * ih, u1 * iw, (1 - v0) * ih
    width, height = max(1, round(x1 - x0)), max(1, round(y1 - y0))
    # Sample only the exact actual mesh-UV bounds, without moving artwork within it.
    xx, yy = np.meshgrid(x0 + (np.arange(width) + .5) * (x1 - x0) / width - .5,
                         y0 + (np.arange(height) + .5) * (y1 - y0) / height - .5)
    result = cv2.remap(rgb, xx.astype(np.float32), yy.astype(np.float32),
                       cv2.INTER_CUBIC, borderMode=cv2.BORDER_REPLICATE)
    # Nonrectangular S13 artwork quads are clipped to the actual existing face UVs.
    normalized = np.column_stack(((uv[:, 0] - u0) / (u1 - u0) * width,
                                  (v1 - uv[:, 1]) / (v1 - v0) * height))
    if len(normalized) == 4:
        footprint = Polygon(normalized)
    else:
        # These source meshes are full rectangular curved sampling grids.
        footprint = box(0, 0, width, height)
    return result, {'method': 'exact_existing_mesh_uv_bounds', 'source_uv_bounds': [u0, v0, u1, v1],
                    'source_pixel_bounds': [x0, y0, x1, y1], 'artwork_size': [width, height],
                    'source_uv_footprint': uv.tolist()}, footprint


def quantize(rgb, index):
    # Keep actual source ink contours separate from bounded photographic artwork.
    # Print gets four or five source colors, food only six smooth source colors.
    h, w = rgb.shape[:2]
    photo = np.zeros((h, w), np.uint8)
    boxes = {
        2: [(0,108,400,155),(0,216,400,259),(0,318,400,360)],
        3: [(0,107,400,158),(0,211,400,261),(0,317,400,361),(0,499,400,537)],
        8: [(8,8,90,59),(215,0,286,60),(287,0,363,41),(93,61,176,125),
            (289,81,363,140),(0,141,84,200),(194,142,281,200)],
        9: [(0,22,94,100)],
        10: [(79,26,139,81),(7,91,75,139),(30,148,139,207),(18,238,87,277)],
        11: [(30,12,99,68),(19,108,104,160),(68,194,125,229),(5,232,71,262)],
        12: [(0,248,122,310)],
        13: [(0,224,127,284)],
        14: [(0,231,121,308)],
    }
    for x0,y0,x1,y1 in boxes.get(index, []):
        photo[max(0,y0):min(h,y1),max(0,x0):min(w,x1)] = 1
    counts = {0: 4, 1: 3, 2: 4, 3: 4, 4: 4, 5: 5, 6: 5, 7: 5,
              8: 4, 9: 4, 10: 5, 11: 5, 12: 4, 13: 4, 14: 4}
    cv2.setRNGSeed(200 + index)
    criterion = (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_MAX_ITER, 30, .15)
    lab = cv2.cvtColor(rgb, cv2.COLOR_RGB2LAB).astype(np.float32)
    if index in (2, 3):
        # These actual posters have cream, neutral dark, red and green print.
        # Collapse camera/antialias shades onto their observed source ink color,
        # avoiding thousands of beige fragments around each tiny printed glyph.
        hsv=cv2.cvtColor(rgb,cv2.COLOR_RGB2HSV)
        gray=cv2.cvtColor(rgb,cv2.COLOR_RGB2GRAY)
        red=((hsv[:,:,0]<16)|(hsv[:,:,0]>170))&(hsv[:,:,1]>65)&(hsv[:,:,2]<248)&(rgb[:,:,0].astype(float)>rgb[:,:,1]*1.3)&(rgb[:,:,0].astype(float)>rgb[:,:,2]*1.25)
        green=(hsv[:,:,0]>30)&(hsv[:,:,0]<100)&(hsv[:,:,1]>45)&(hsv[:,:,2]<225)
        labels=np.zeros((h,w),np.int32)
        labels[gray<185]=1
        labels[red]=2;labels[green]=3
        colors=[]
        for c in range(4):
            mask=(labels==c)&(photo==0)
            if c==1: mask=mask&(gray<115)
            colors.append(np.median(rgb[mask],axis=0).astype(np.uint8))
    else:
        _, _, centers = cv2.kmeans(lab[photo == 0], counts[index], None, criterion, 2, cv2.KMEANS_PP_CENTERS)
        distance = np.sum((lab[:,:,None,:] - centers[None,None,:,:])**2, axis=3)
        labels = np.argmin(distance, axis=2).astype(np.int32)
        colors = [np.median(rgb[(labels == c) & (photo == 0)],axis=0).astype(np.uint8) for c in range(counts[index])]
    if photo.any():
        blurred = cv2.GaussianBlur(cv2.bilateralFilter(rgb, 9, 55, 4), (0,0), 1.4)
        plab = cv2.cvtColor(blurred,cv2.COLOR_RGB2LAB).astype(np.float32)
        _, _, pc = cv2.kmeans(plab[photo == 1],6,None,criterion,2,cv2.KMEANS_PP_CENTERS)
        dist = np.sum((plab[:,:,None,:] - pc[None,None,:,:])**2,axis=3)
        source_photo_labels = np.argmin(dist,axis=2).astype(np.uint8)
        # Only photo labels are denoised. Glyph masks remain their original outlines.
        source_photo_labels = cv2.medianBlur(source_photo_labels,5)
        for c in range(6):
            mask = (source_photo_labels == c) & (photo == 1)
            if not mask.any(): continue
            color = np.median(rgb[mask],axis=0).astype(np.uint8)
            near = min(range(len(colors)),key=lambda n:np.linalg.norm(colors[n].astype(float)-color))
            if np.linalg.norm(colors[near].astype(float)-color) < 35:
                labels[mask] = near
            else:
                labels[mask] = len(colors); colors.append(color)
    order=sorted(range(len(colors)),key=lambda c:float(np.dot(colors[c],[.2126,.7152,.0722])))
    return labels,[(i,colors[i]) for i in order]


def contours_for(mask):
    h, w = mask.shape
    # Threefold interpolation gives smooth subpixel contour vertices while keeping
    # thin glyphs. The final contour tolerance is well below one source pixel.
    padded = np.pad(mask.astype(np.uint8) * 255, 1)
    large = cv2.resize(padded, None, fx=UPSCALE, fy=UPSCALE, interpolation=cv2.INTER_LINEAR)
    large = (large >= 128).astype(np.uint8)
    contours, hierarchy = cv2.findContours(large, cv2.RETR_CCOMP, cv2.CHAIN_APPROX_SIMPLE)
    if hierarchy is None:
        return []
    hierarchy = hierarchy[0]
    result = []

    def ring(c):
        points = cv2.approxPolyDP(c.astype(np.float32), EPSILON_SOURCE_PX * UPSCALE, True).reshape(-1, 2)
        points = (points + 0.5) / UPSCALE - 1.0
        points[:, 0] = np.clip(points[:, 0], 0, w)
        points[:, 1] = np.clip(points[:, 1], 0, h)
        return points

    for i, contour in enumerate(contours):
        if hierarchy[i][3] != -1:
            continue
        shell = ring(contour)
        if len(shell) < 3:
            continue
        holes = []
        child = hierarchy[i][2]
        while child != -1:
            inner = ring(contours[child])
            if len(inner) >= 3 and Polygon(inner).area >= MIN_HOLE_AREA_SOURCE_PX:
                holes.append(inner)
            child = hierarchy[child][0]
        geom = Polygon(shell, holes)
        if not geom.is_valid:
            geom = shapely.make_valid(geom)
        for p in polygon_parts(geom.intersection(box(0, 0, w, h))):
            if p.area >= MIN_COMPONENT_AREA_SOURCE_PX:
                result.append(orient(p, sign=1.0))
    return result


def shared_color_regions(labelmap, footprint):
    """Trace each shared interface once, then polygonize the complete subdivision.

    This avoids the white cracks caused by independently eroded class masks. Each
    interface is continuous and shared by both neighboring source colors. One
    Chaikin corner round stays within 0.354 source pixels of its original boundary.
    """
    h, w = labelmap.shape
    edges = []
    graph = defaultdict(list)

    def add(a, b, ca, cb):
        i = len(edges)
        edges.append((a, b, tuple(sorted((int(ca), int(cb))))))
        graph[a].append(i); graph[b].append(i)

    ys, xs = np.nonzero(labelmap[:, :-1] != labelmap[:, 1:])
    for y, x in zip(ys.tolist(), xs.tolist()):
        add((x + 1, y), (x + 1, y + 1), labelmap[y, x], labelmap[y, x + 1])
    ys, xs = np.nonzero(labelmap[:-1, :] != labelmap[1:, :])
    for y, x in zip(ys.tolist(), xs.tolist()):
        add((x, y + 1), (x + 1, y + 1), labelmap[y, x], labelmap[y + 1, x])
    for x in range(w):
        add((x, 0), (x + 1, 0), -1, labelmap[0, x])
        add((x, h), (x + 1, h), -1, labelmap[-1, x])
    for y in range(h):
        add((0, y), (0, y + 1), -1, labelmap[y, 0])
        add((w, y), (w, y + 1), -1, labelmap[y, -1])
    visited = np.zeros(len(edges), dtype=bool)

    def junction(node, pair):
        adjacent = graph[node]
        return len(adjacent) != 2 or any(edges[e][2] != pair for e in adjacent)

    paths = []
    for i, (a, b, pair) in enumerate(edges):
        if visited[i]:
            continue
        start = a if junction(a, pair) else b if junction(b, pair) else a
        points = [start]
        node, e = start, i
        while not visited[e]:
            visited[e] = True
            a, b, _ = edges[e]
            node = b if node == a else a
            points.append(node)
            if junction(node, pair):
                break
            candidates = [v for v in graph[node] if not visited[v]]
            if not candidates:
                break
            e = candidates[0]
        coords = np.asarray(points, dtype=float)
        if -1 not in pair and len(coords) > 2:
            closed = np.array_equal(coords[0], coords[-1])
            if closed:
                p = coords[:-1]
                q = np.roll(p, -1, axis=0)
                rounded = np.empty((len(p) * 2 + 1, 2))
                rounded[:-1:2] = .75 * p + .25 * q
                rounded[1:-1:2] = .25 * p + .75 * q
                rounded[-1] = rounded[0]
            else:
                p, q = coords[:-1], coords[1:]
                rounded = np.empty((len(p) * 2 + 2, 2))
                rounded[0], rounded[-1] = coords[0], coords[-1]
                rounded[1:-1:2] = .75 * p + .25 * q
                rounded[2:-1:2] = .25 * p + .75 * q
            line = LineString(rounded).simplify(EPSILON_SOURCE_PX, preserve_topology=True)
        else:
            line = LineString(coords).simplify(EPSILON_SOURCE_PX, preserve_topology=True)
        paths.append(line)
    by_color = defaultdict(list)
    for poly in polygonize(paths):
        q = poly.representative_point()
        color = int(labelmap[min(h - 1, max(0, int(q.y))), min(w - 1, max(0, int(q.x)))])
        clipped = poly.intersection(footprint)
        for p in polygon_parts(clipped):
            # Small source components are retained to preserve tiny glyph strokes.
            if p.area >= .05:
                by_color[color].append(orient(p, sign=1.0))
    return by_color


def closed_mesh(polygons, width, height, back_z=0, front_z=1):
    vertices, triangles = [], []
    lookup = {}
    hole_count = 0
    area_px = 0.0

    def vertex(x, y, z):
        u, v = float(x) / width, 1.0 - float(y) / height
        actual_z=back_z if z==0 else front_z
        key = (round(u, 8), round(v, 8), actual_z)
        if key not in lookup:
            lookup[key] = len(vertices)
            vertices.append(list(key))
        return lookup[key]

    def cap_triangle(coords):
        # Image y-down flips orientation; explicitly enforce +z front.
        xyz = [(float(x) / width, 1 - float(y) / height) for x, y in coords]
        cross = (xyz[1][0] - xyz[0][0]) * (xyz[2][1] - xyz[0][1]) - (xyz[1][1] - xyz[0][1]) * (xyz[2][0] - xyz[0][0])
        if abs(cross) < 1e-14:
            return
        if cross < 0:
            coords = [coords[0], coords[2], coords[1]]
        front = [vertex(x, y, 1) for x, y in coords]
        back = [vertex(x, y, 0) for x, y in coords]
        triangles.extend(front)
        triangles.extend([back[2], back[1], back[0]])

    # Source pixel masks can contain a ring or counter touching another ring at a
    # mathematical point. A 0.0001 source-pixel inward offset separates that pinch
    # into true closed solids without a visible change or invented glyph feature.
    if back_z > 0:
        polygons=[part for poly in polygons for part in polygon_parts(poly.buffer(-.0001,join_style='mitre'))]
    for poly in polygons:
        # Keep distinct closed solids topologically distinct when their actual
        # source contours meet at a corner. Welding their vertical edges would
        # incorrectly turn two closed components into a nonmanifold index mesh.
        lookup = {}
        area_px += poly.area
        hole_count += len(poly.interiors)
        triangulation = shapely.constrained_delaunay_triangles(poly)
        for tri in polygon_parts(triangulation):
            if poly.covers(tri.representative_point()):
                cap_triangle(list(tri.exterior.coords)[:3])
        # Reverse image rings to get CCW outer/CW inner in normalized XY space.
        for ring in [poly.exterior, *poly.interiors]:
            points = list(ring.coords)[:-1][::-1]
            for a, b in zip(points, points[1:] + points[:1]):
                ai0 = vertex(*a, 0)
                bi0 = vertex(*b, 0)
                ai1 = vertex(*a, 1)
                bi1 = vertex(*b, 1)
                triangles.extend([ai0, bi0, bi1, ai0, bi1, ai1])
    return vertices, triangles, {'components': len(polygons), 'holes': hole_count, 'source_area_pixels': round(area_px, 3)}


def svg_path(poly):
    parts = []
    for ring in [poly.exterior, *poly.interiors]:
        pts = list(ring.coords)[:-1]
        parts.append('M' + ' L'.join(f'{x:.3f},{y:.3f}' for x, y in pts) + ' Z')
    return ' '.join(parts)


def render_vector(key, artwork, layers):
    h, w = artwork.shape[:2]
    scale = max(2.0, min(6.0, 1000 / max(w, h)))
    ow, oh = math.ceil(w * scale), math.ceil(h * scale)
    result = Image.new('RGB', (ow, oh), (255, 255, 255))
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">']
    for color, polys in layers:
        mask = Image.new('L', (ow, oh), 0)
        draw = ImageDraw.Draw(mask)
        for p in polys:
            draw.polygon([(x * scale, y * scale) for x, y in p.exterior.coords], fill=255)
            for hole in p.interiors:
                draw.polygon([(x * scale, y * scale) for x, y in hole.coords], fill=0)
        result.paste(Image.new('RGB', result.size, tuple(map(int, color))), (0, 0), mask)
        value = '#' + ''.join(f'{int(v):02X}' for v in color)
        path = ' '.join(svg_path(p) for p in polys)
        svg.append(f'<path fill="{value}" fill-rule="evenodd" d="{path}"/>')
    svg.append('</svg>')
    (BASE / f'{key}.svg').write_text('\n'.join(svg))
    result.save(BASE / f'{key}-vector.png')
    Image.fromarray(artwork).save(BASE / f'{key}-source-artwork.png')
    return result


def main():
    src = json.loads(SOURCE.read_text())['items']
    textures = list(dict.fromkeys(i['texture'] for i in src))
    packet = {'textures': []}
    summary = []
    previews = []
    t0 = time.time()
    for index, path in enumerate(textures):
        disk = ROOT / path
        image = Image.open(disk).convert('RGB')
        rgb = np.asarray(image)
        matching = [i for i in src if i['texture'] == path]
        uv_sets = {json.dumps(i['mesh_uv']) for i in matching}
        if len(uv_sets) != 1:
            raise ValueError(f'Differing UV footprints for {path}')
        artwork, crop, footprint = artwork_image(rgb, matching[0]['mesh_uv'])
        h, w = artwork.shape[:2]
        key = f'F1_SOURCE_ART_{index:02d}_{hashlib.sha256(path.encode()).hexdigest()[:8].upper()}'
        labelmap, palette = quantize(artwork, index)
        out = {'texture': path, 'key': key, 'source_sha256': hashlib.sha256(disk.read_bytes()).hexdigest(),
               'source_size': [image.width, image.height], 'artwork_size': [w, h], 'source_crop': crop,
               'source_uv_bounds': crop['source_uv_bounds'],
               'palette': []}
        layers = []
        detail = {'index': index, 'key': key, 'texture': path, 'instances': sum(i['texture'] == path for i in src),
                  'source_size': out['source_size'], 'artwork_size': [w, h], 'layers': []}
        regions = shared_color_regions(labelmap, footprint)
        base_color=int(np.bincount(labelmap.reshape(-1)).argmax())
        # A real backing is a complete thin slab. Its color counters are exposed
        # through actual glyph holes rather than duplicated cutout backing walls.
        regions[base_color]=polygon_parts(footprint)
        for layer_index, (c, color) in enumerate(palette):
            polys = regions[c]
            if not polys:
                continue
            is_base=c==base_color
            verts, tris, stats = closed_mesh(polys, w, h,0 if is_base else .12,.12 if is_base else 1)
            name = f'actual_source_color_{layer_index:02d}'
            value = '#' + ''.join(f'{int(v):02X}' for v in color)
            out['palette'].append({'name': name, 'color': value, 'vertices': verts, 'triangles': tris,
                                   'role':'physical_backing_slab' if is_base else 'actual_source_ink_or_artwork_relief'})
            detail['layers'].append({'name': name, 'color': value, 'vertices': len(verts), 'triangles': len(tris) // 3, **stats})
            layers.append((color, polys))
        # Backing is drawn before every actual-source surface contour.
        layers.sort(key=lambda layer:0 if np.array_equal(layer[0],dict(palette)[base_color]) else 1)
        preview = render_vector(key, artwork, layers)
        previews.append((key, Image.fromarray(artwork), preview))
        detail.update({'vertices': sum(len(p['vertices']) for p in out['palette']),
                       'triangles': sum(len(p['triangles']) // 3 for p in out['palette']),
                       'closed_components': sum(p['components'] for p in detail['layers']),
                       'preserved_holes': sum(p['holes'] for p in detail['layers'])})
        packet['textures'].append(out)
        summary.append(detail)
        print(json.dumps({k: detail[k] for k in ('index', 'key', 'vertices', 'triangles', 'closed_components', 'preserved_holes')}), flush=True)
    (BASE / 'VECTOR-1F-PACKET.json').write_text(json.dumps(packet, separators=(',', ':')))
    totals = {'textures': len(summary), 'instances': len(src), 'vertices': sum(d['vertices'] for d in summary),
              'triangles': sum(d['triangles'] for d in summary), 'closed_components': sum(d['closed_components'] for d in summary),
              'preserved_holes': sum(d['preserved_holes'] for d in summary)}
    report = {'method': 'continuous actual-source palette contours with closed polygon relief meshes',
              'no_text_components': True, 'no_font_generation': True, 'no_texture_materials': True,
              'no_image_planes': True, 'no_pixel_cubes': True, 'source_images_immutable': True,
              'source_pixel_contour_tolerance': EPSILON_SOURCE_PX,
              'maximum_boundary_rounding_plus_simplification_source_pixels': .804,
              'point_contact_separation_source_pixels': .0001,
              'minimum_component_area_source_pixels': .05,
              'minimum_hole_area_source_pixels': 0,
              'shared_color_interfaces': True,
              'coordinates': 'u=x/artworkWidth, v=1-y/artworkHeight; physical backing z=0..0.12, source ink/art relief z=.12..1; root maps actual face and depth',
              'totals': totals, 'textures': summary,
              'limitations': ['Small source print, blur, occlusion, perspective and source-era contents remain source-limited; no invented replacement contents.',
                              'Vector relief is an actual-source artwork construction, not a field measurement or completeness claim.',
                              'Every source crop is bounded by exact existing mesh UVs, including actual self-bar plaque selection; source files are unchanged.',
                              'Geometry prepared offline only; no Unity/Assets mutation or agent runtime QA was performed.'],
              'elapsed_seconds': round(time.time() - t0, 1)}
    (BASE / 'VECTOR-1F-PROVENANCE.json').write_text(json.dumps(report, indent=2))
    # Labeled offline construction preview, not a Unity capture.
    cw, ch = 590, 380
    sheet = Image.new('RGB', (cw * 3, ch * math.ceil(len(previews) / 3)), (216, 216, 216))
    draw = ImageDraw.Draw(sheet)
    for n, (key, original, preview) in enumerate(previews):
        x, y = n % 3 * cw, n // 3 * ch
        draw.text((x + 8, y + 5), f'{n:02d} SOURCE | CONSTRUCTED VECTOR ART', fill=(0, 0, 0))
        for side, im in enumerate((original, preview)):
            thumb = im.copy(); thumb.thumbnail((280, 340))
            sheet.paste(thumb, (x + side * 292 + (284 - thumb.width) // 2, y + 25 + (340 - thumb.height) // 2))
    sheet.save(BASE / 'VECTOR-1F-CONSTRUCTION-CONTACT.jpg', quality=94)
    print(json.dumps({'complete': True, 'totals': totals, 'elapsed_seconds': report['elapsed_seconds']}), flush=True)


if __name__ == '__main__':
    main()
