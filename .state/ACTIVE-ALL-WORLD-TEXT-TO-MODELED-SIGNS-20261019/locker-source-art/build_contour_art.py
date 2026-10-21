#!/opt/homebrew/bin/python3
"""Trace observed/primary-source outlines into closed relief, without fonts."""
import json
import hashlib
from pathlib import Path
from collections import Counter

import cv2
import numpy as np
from PIL import Image, ImageDraw
from shapely import Polygon, constrained_delaunay_triangles, make_valid
from shapely.geometry.polygon import orient

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[2]
SRC = ROOT / 'source-evidence/retained-work-originals/workers/twin/output/architecture-resume-20261010/MAIN1F-approved-interior-ESC-stairs/targeted-ESC-locker-photos/original-03.jpeg'
REF = OUT / 'locker-sign-rectified-reference.png'


def polygons(mask, minimum=8):
    contours, hierarchy = cv2.findContours(mask, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE)
    if hierarchy is None:
        return []
    hierarchy = hierarchy[0]
    result = []
    for i, contour in enumerate(contours):
        parent = hierarchy[i][3]
        depth = 0
        while parent >= 0:
            depth += 1
            parent = hierarchy[parent][3]
        if depth % 2 or cv2.contourArea(contour) < minimum:
            continue
        exterior = cv2.approxPolyDP(contour, .55, True).reshape(-1, 2).tolist()
        if len(exterior) < 3:
            continue
        holes = []
        child = hierarchy[i][2]
        while child >= 0:
            ring = cv2.approxPolyDP(contours[child], .55, True).reshape(-1, 2).tolist()
            if len(ring) >= 3 and cv2.contourArea(contours[child]) >= 2:
                holes.append(ring)
            child = hierarchy[child][0]
        poly = make_valid(Polygon(exterior, holes))
        if poly.geom_type == 'Polygon':
            result.append(poly)
        else:
            result.extend(g for g in poly.geoms if g.geom_type == 'Polygon' and g.area >= minimum)
    return result


def closed_relief(polys, width, height):
    vertices, triangles, lookup = [], [], {}

    def vid(x, y, z):
        p = (round(float(x) / width, 8), round(1-float(y)/height, 8), int(z))
        if p not in lookup:
            lookup[p] = len(vertices)
            vertices.append(list(p))
        return lookup[p]

    for raw in polys:
        # Source image y-down changes orientation in UV. Use source clockwise.
        poly = orient(raw, sign=-1)
        for tri in constrained_delaunay_triangles(poly).geoms:
            q = list(tri.exterior.coords)[:3]
            uv = [(x/width, 1-y/height) for x, y in q]
            signed = sum(uv[i][0]*uv[(i+1)%3][1]-uv[(i+1)%3][0]*uv[i][1] for i in range(3))
            if signed < 0:
                q = [q[0], q[2], q[1]]
            a, b, c = [vid(x, y, 1) for x, y in q]
            triangles.extend([a, b, c])
            a, b, c = [vid(x, y, 0) for x, y in q]
            triangles.extend([c, b, a])
        for ring in [poly.exterior, *poly.interiors]:
            pts = list(ring.coords)[:-1]
            for i, (x, y) in enumerate(pts):
                x2, y2 = pts[(i+1) % len(pts)]
                a, b, c, d = vid(x,y,0), vid(x2,y2,0), vid(x2,y2,1), vid(x,y,1)
                triangles.extend([a,b,c,a,c,d])
    counts = Counter()
    for i in range(0,len(triangles),3):
        a,b,c=triangles[i:i+3]
        for x,y in [(a,b),(b,c),(c,a)]:
            counts[tuple(sorted((x,y)))] += 1
    return vertices, triangles, sum(n != 2 for n in counts.values())


def trace(key, image, box, source, color, minarea=8):
    crop = image.crop(box).convert('RGBA')
    a = np.array(crop)
    if key == 'etude_crest_wordmark':
        mask = (a[:,:,3] > 128).astype('uint8')*255
    else:
        # Bright source lettering on blue; no generic type or text renderer.
        # Reject luminous blue/white bloom bridges: retain the actual white core.
        threshold = 225 if key == 'locker_korean' else 210 if key == 'locker_english' else 185
        mask = ((a[:,:,:3].min(axis=2) > threshold) & (a[:,:,:3].max(axis=2)-a[:,:,:3].min(axis=2) < 90)).astype('uint8')*255
    nonzero = cv2.findNonZero(mask)
    x,y,w,h=cv2.boundingRect(nonzero)
    mask=mask[y:y+h,x:x+w]
    crop=crop.crop((x,y,x+w,y+h))
    crop.save(OUT/(key+'-source-crop.png'))
    polys=polygons(mask,minarea)
    # This preview is a rasterization of vector polygons, not a scene sign plane.
    preview=Image.new('RGB',(w,h),'#182247' if key!='etude_crest_wordmark' else '#F7E6EC')
    draw=ImageDraw.Draw(preview)
    for poly in polys:
        draw.polygon(list(poly.exterior.coords),fill=color)
        for ring in poly.interiors:
            draw.polygon(list(ring.coords),fill='#182247' if key!='etude_crest_wordmark' else '#F7E6EC')
    preview.save(OUT/(key+'-vector-preview.png'))
    vertices,triangles,bad=closed_relief(polys,w-1,h-1)
    svgpaths=[]
    for poly in polys:
        rings=[poly.exterior,*poly.interiors]
        svgpaths.append(' '.join('M'+' L'.join(f'{p[0]:.3f},{p[1]:.3f}' for p in list(r.coords)[:-1])+' Z' for r in rings))
    svg='<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 '+str(w)+' '+str(h)+'"><g fill="'+color+'" fill-rule="evenodd">'+''.join('<path d="'+p+'"/>' for p in svgpaths)+'</g></svg>'
    (OUT/(key+'-contours.svg')).write_text(svg)
    return {'key':key,'source':source,'source_crop':str(OUT/(key+'-source-crop.png')),'vector_svg':str(OUT/(key+'-contours.svg')),'preview':str(OUT/(key+'-vector-preview.png')),'canvas_px':[w,h],'aspect':w/h,'coordinate_convention':'u right; v up; tight source-art canvas0..1; z0back/z1front; scale depth in Unity','source_box_before_tight_crop':box,'closed_edge_incidence_error_count':bad,'polygon_count':len(polys),'counter_holes':sum(len(p.interiors) for p in polys),'palette':[{'name':'observed_white_ink' if key!='etude_crest_wordmark' else 'photo_matched_etude_burgundy','color':color,'vertices':vertices,'triangles':triangles}]}


ref=Image.open(REF)
source={'file':str(SRC),'sha256':hashlib.sha256(SRC.read_bytes()).hexdigest(),'basis':'Actual Busan1F locker sign, original3000x2250 retained photograph. Perspective rectified only for graphic extraction.','rectified_reference':str(REF),'source_quad_px':[[267,352],[730,385],[728,511],[265,482]],'not_a_font':True}
arts=[]
arts.append(trace('locker_korean',ref,[413,53,1340,218],source,'#FFFFFF'))
arts.append(trace('locker_english',ref,[422,221,761,299],source,'#FFFFFF',4))
arts.append(trace('locker_pictogram',ref,[57,47,359,300],source,'#FFFFFF'))
brand=Image.open(OUT/'etude-official-crest-wordmark.png')
brand_source={'url':'https://www.etude.com/wp-content/uploads/2023/04/loading-logo2023-1.svg','file':str(OUT/'etude-official-logo2023.svg'),'sha256':hashlib.sha256((OUT/'etude-official-logo2023.svg').read_bytes()).hexdigest(),'basis':'Official primary SVG ornamental crest plus ETUDE wordmark; same crest/word identity visible in retained actual vending-machine photo. Photo color burgundy is MODEL matching, not a field color measurement.','site_photo':str(SRC.with_name('original-01.jpeg')),'not_a_font':True}
arts.append(trace('etude_crest_wordmark',brand,[0,0,*brand.size],brand_source,'#8E3452',8))
terminal=json.loads(json.dumps(arts[0]));terminal['key']='locker_terminal_korean';terminal['source_reuse_qualification']='Exact photographed hanging-sign Korean silhouette reused at existing terminal header. Terminal typography not independently resolved; placement/size MODEL.';arts.append(terminal)
packet={'schema':'chooguard.normalized.closed.source.contour.art.v1','arts':arts,'recommendations':[{'target_gameobject_id':'990235646','current_text':'Lovely Cosmetics','action':'REMOVE_UNSUPPORTED_WORDS','basis':'Not observed in actual vending roof original01; actual source displays ornamental crest and ETUDE only.'}],'limits':['Source white contours preserve photographic edges and visible glyph counters; no generic font substitution, TMP baking or image billboard.','English source is small in the actual photograph; minor edge rounding is source limited.','Chinese/Japanese supplementary hanging-sign line is outside this assigned contour lane.','Terminal header uses qualified reuse rather than claiming a separate observed terminal font.'],'source_files_preserved':True,'unity_assets_modified':False}
(OUT/'LOCKER-SOURCE-CONTOUR-ART.json').write_text(json.dumps(packet,ensure_ascii=False,separators=(',',':'))+'\n')
print(json.dumps([{'key':a['key'],'canvas':a['canvas_px'],'polygons':a['polygon_count'],'holes':a['counter_holes'],'vertices':len(a['palette'][0]['vertices']),'triangles':len(a['palette'][0]['triangles'])//3,'bad_edges':a['closed_edge_incidence_error_count']} for a in arts],ensure_ascii=False,indent=2))
