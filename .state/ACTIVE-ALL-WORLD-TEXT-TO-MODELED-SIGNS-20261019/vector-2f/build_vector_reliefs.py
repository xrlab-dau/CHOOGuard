#!/opt/homebrew/bin/python3
"""Source-color contour tracing, closed solid extrusion, no fonts or image planes."""
from pathlib import Path
from collections import defaultdict
import hashlib, json, time, re, sys, os
import cv2
import numpy as np
import shapely
from shapely.geometry import Polygon, Point
from shapely.geometry.polygon import orient
from shapely.strtree import STRtree

OUT = Path(__file__).resolve().parent
sys.path.insert(0,str(OUT/'deps'))
import mapbox_earcut
ROOT = OUT.parents[2]
SRC = OUT.parent / 'RASTER-SIGNS-2F.json'
COLORS = 12

def palette_labels(image,texture):
    rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    # Exact observed food-photo cells on the tall Soban source menu. This is
    # artwork processing only: all type regions lie outside these rectangles.
    if texture.endswith('/soban_printed_menu.png'):
        rh,rw=rgb.shape[:2]
        for xa,xb in [(0.075,.31),(.38,.615),(.69,.925)]:
            for ya,yb in [(.125,.208),(.268,.348),(.405,.493),(.543,.625),(.685,.758)]:
                x0,x1=int(xa*rw),int(xb*rw);y0,y1=int(ya*rh),int(yb*rh)
                rgb[y0:y1,x0:x1]=cv2.medianBlur(rgb[y0:y1,x0:x1],9)
    gray=cv2.cvtColor(rgb,cv2.COLOR_RGB2GRAY).astype(np.float32)
    local=cv2.GaussianBlur(gray,(15,15),0)
    stroke_protection=(gray<local-6)|(gray>local+10)
    smoothed=cv2.bilateralFilter(rgb,9,60,5)
    smoothed[stroke_protection]=rgb[stroke_protection]
    lab = cv2.cvtColor(smoothed, cv2.COLOR_RGB2LAB).astype(np.float32)
    flat = lab.reshape(-1, 3)
    sample = flat[np.linspace(0, len(flat)-1, min(48000, len(flat)), dtype=int)]
    cv2.setRNGSeed(13579)
    target_colors=8 if rgb.shape[0]*rgb.shape[1]>500000 else COLORS
    _, _, centers = cv2.kmeans(sample, target_colors, None,
        (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_MAX_ITER, 40, .2),
        2, cv2.KMEANS_PP_CENTERS)
    # Adjacent subtle paper/display/photo tones need no separate physical parts.
    # Strong actual text/logo color differences remain distinct.
    centers=list(centers)
    while len(centers)>5:
        arr=np.asarray(centers)
        dist=((arr[:,None,:]-arr[None,:,:])**2).sum(axis=2)
        np.fill_diagonal(dist,np.inf)
        aa,bb=np.unravel_index(np.argmin(dist),dist.shape)
        if dist[aa,bb]>=6**2:break
        centers[aa]=(centers[aa]+centers[bb])*.5
        centers.pop(bb)
    centers=np.asarray(centers)
    ncolors=len(centers)
    labels = np.empty(len(flat), np.uint8)
    for start in range(0, len(flat), 40000):
        block=flat[start:start+40000]
        distances=((block[:,None,:]-centers[None,:,:])**2).sum(axis=2)
        labels[start:start+len(block)] = distances.argmin(axis=1)
    labels=labels.reshape(rgb.shape[:2])
    # Absorb isolated source-pixel specks into their closest actual neighbor color.
    # Long 1px-wide glyph strokes survive because connected area exceeds 1px.
    removed = 0
    min_area=max(1, int(rgb.shape[0]*rgb.shape[1]/700000))
    for color in range(ncolors):
        count, component, stats, _ = cv2.connectedComponentsWithStats(
            (labels==color).astype(np.uint8), connectivity=4)
        for n in np.where((stats[:,cv2.CC_STAT_AREA]<=600)&(np.arange(count)>0))[0]:
            x,y,w,h,a=stats[n]
            y0,y1=max(y-1,0),min(y+h+1,labels.shape[0])
            x0,x1=max(x-1,0),min(x+w+1,labels.shape[1])
            block=labels[y0:y1,x0:x1]
            comp=component[y0:y1,x0:x1]==n
            border=cv2.dilate(comp.astype(np.uint8),np.ones((3,3),np.uint8)).astype(bool)&~comp
            options=np.unique(block[border&(block!=color)])
            if len(options):
                distances=((centers[options]-centers[color])**2).sum(axis=1)
                replacement=options[distances.argmin()]
                delta=float(np.sqrt(distances.min()))
                protected=bool(np.any(stroke_protection[y0:y1,x0:x1]&comp))
                if a<=min_area or (not protected and ((a<=16 and delta<38) or delta<30)):
                    labels[y:y+h,x:x+w][component[y:y+h,x:x+w]==n]=replacement
                    removed+=int(a)
    colors=[]
    for color in range(ncolors):
        pixels=rgb[labels==color]
        colors.append(np.median(pixels,axis=0).astype(np.uint8) if len(pixels) else np.zeros(3,np.uint8))
    return labels,colors,removed

def trace_rings(mask):
    """Exact directed pixel-edge loops. Right turns keep diagonal regions separate."""
    h,w=mask.shape; stride=w+1
    padded=np.pad(mask,1)
    top=mask & ~padded[:-2,1:-1]
    right=mask & ~padded[1:-1,2:]
    bottom=mask & ~padded[2:,1:-1]
    left=mask & ~padded[1:-1,:-2]
    edges=defaultdict(list)
    for selector,dx0,dy0,dx1,dy1,direction in [
        (top,0,0,1,0,0),(right,1,0,1,1,1),
        (bottom,1,1,0,1,2),(left,0,1,0,0,3)]:
        yy,xx=np.nonzero(selector)
        starts=(yy+dy0)*stride+xx+dx0; ends=(yy+dy1)*stride+xx+dx1
        for a,b in zip(starts.tolist(),ends.tolist()):edges[a].append((b,direction))
    rings=[]
    turn_priority={1:0,0:1,3:2,2:3}
    while edges:
        first,options=edges.popitem(); end,direction=options.pop()
        if options:edges[first]=options
        points=[first]; current=end
        while current!=first:
            points.append(current)
            options=edges[current]
            choice=min(range(len(options)),key=lambda i:turn_priority[(options[i][1]-direction)%4])
            nxt,direction=options.pop(choice)
            if not options:del edges[current]
            current=nxt
        if len(points)>=4:
            ring=np.array([(p%stride,p//stride) for p in points],np.float64)
            # Every straight run collapses before bounded-distance simplification.
            before=np.roll(ring,1,axis=0);after=np.roll(ring,-1,axis=0)
            cross=(ring[:,0]-before[:,0])*(after[:,1]-ring[:,1])-(ring[:,1]-before[:,1])*(after[:,0]-ring[:,0])
            ring=ring[cross!=0]
            if len(ring)>=3:rings.append(ring)
    return rings

def signed_area(ring):
    return np.sum(ring[:,0]*np.roll(ring[:,1],-1)-np.roll(ring[:,0],-1)*ring[:,1])*.5

def polygons_from_mask(mask):
    rings=trace_rings(mask)
    outer=[r for r in rings if signed_area(r)>0]
    inner=[r for r in rings if signed_area(r)<0]
    shells=[Polygon(r) for r in outer]
    holes=defaultdict(list)
    if inner and shells:
        tree=STRtree(shells)
        for ring in inner:
            point=Polygon(ring).representative_point()
            owners=[int(i) for i in tree.query(point) if shells[i].covers(point)]
            if owners:holes[min(owners,key=lambda i:shells[i].area)].append(ring)
    result=[]
    for i,shell in enumerate(outer):
        poly=Polygon(shell,holes[i])
        if not poly.is_valid:poly=shapely.make_valid(poly)
        geoms=[poly] if poly.geom_type=='Polygon' else [g for g in poly.geoms if g.geom_type=='Polygon']
        for g in geoms:
            simple=g.simplify(.6 if g.area<80 else .8,preserve_topology=True)
            if not simple.is_empty and simple.area>0:
                result.append(orient(simple,sign=1.0))
    return result

def extrude(polygons,w,h):
    vertices=[];indices=[];lookup={};holes=0
    def vertex(x,y,z):
        key=(round(float(x)/w,8),round(1.0-float(y)/h,8),.12 if z==0 else 1)
        if key not in lookup:lookup[key]=len(vertices);vertices.append(list(key))
        return lookup[key]
    for polygon in polygons:
        holes+=len(polygon.interiors)
        rings=[np.asarray(ring.coords,dtype=np.float64)[:-1] for ring in [polygon.exterior,*polygon.interiors]]
        points=np.concatenate(rings)
        ends=np.cumsum([len(ring) for ring in rings],dtype=np.uint32)
        triangulation=mapbox_earcut.triangulate_float64(points,ends)
        for ia,ib,ic in triangulation.reshape(-1,3):
            coords=[points[ia],points[ib],points[ic]]
            ids=[vertex(x,y,1) for x,y in coords]
            a,b,c=[vertices[i] for i in ids]
            cross=(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
            if cross<0:ids=[ids[0],ids[2],ids[1]];coords=[coords[0],coords[2],coords[1]]
            if abs(cross)>1e-16:
                indices.extend(ids)
                indices.extend([vertex(coords[2][0],coords[2][1],0),vertex(coords[1][0],coords[1][1],0),vertex(coords[0][0],coords[0][1],0)])
        for ring in [polygon.exterior,*polygon.interiors]:
            coords=list(ring.coords)
            # Source y-down rings have outer CCW mathematically, so normalized
            # UV flips winding. Reverse to put interior to the UV left.
            coords=list(reversed(coords))
            for (x1,y1),(x2,y2) in zip(coords,coords[1:]):
                a,b,c,d=vertex(x1,y1,0),vertex(x2,y2,0),vertex(x2,y2,1),vertex(x1,y1,1)
                indices.extend([a,b,c,a,c,d])
    return vertices,indices,holes

def backing_slab():
    vertices=[[0,0,0],[1,0,0],[1,1,0],[0,1,0],
        [0,0,.12],[1,0,.12],[1,1,.12],[0,1,.12]]
    triangles=[4,5,6,4,6,7,2,1,0,3,2,0,
        0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7]
    return vertices,triangles

def main():
    t=time.monotonic()
    sources=json.loads(SRC.read_text())['items']
    textures=list(dict.fromkeys(x['texture'] for x in sources))
    packet={'schema':'ActualSourceContinuousReliefV1','textures':[],'construction':{
        'palette_colors_max':COLORS,'pixel_edge_contours':True,
        'simplify_tolerance_source_pixels_small_glyphs':.6,
        'simplify_tolerance_source_pixels_larger_regions':.8,
        'bilateral_denoise':{'diameter':9,'sigma_color':60,'sigma_space':5},
        'large_source_palette_colors_max':8,
        'physical_layers':{'backing_z':[0,.12],'actual_source_ink_z':[.12,1]},
        'close_source_palette_tones_merged_below_Lab_distance':6,
        'source_local_stroke_protection':{'dark_delta':6,'light_delta':10,'neighborhood':15},
        'closed_front_back_and_all_ring_sides':True,
        'fonts_or_text_components':False,'textured_image_planes':False,
        'source_assets_immutable':True}}
    report=[]; previews=[]
    selected=os.environ.get('RELIEF_SOURCE_INDICES')
    selected={int(n) for n in selected.split(',')} if selected else None
    for num,texture in enumerate(textures):
        if selected is not None and num not in selected:continue
        print('START',num,texture,flush=True)
        source=ROOT/texture
        image=cv2.imread(str(source),cv2.IMREAD_COLOR)
        if image is None:raise RuntimeError('Missing source '+str(source))
        h,w=image.shape[:2]
        key=f't{num:02d}_'+re.sub('[^a-z0-9]+','_',source.stem.lower()).strip('_')
        labels,colors,removed=palette_labels(image,texture)
        print('PALETTE',num,round(time.monotonic()-t,1),flush=True)
        entry={'texture':texture,'key':key,'source_uv_bounds':[0,0,1,1],'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
            'source_size':[w,h],'palette':[]}
        dominant=int(np.bincount(labels.ravel()).argmax())
        dominant_rgb=colors[dominant]
        bv,bt=backing_slab()
        entry['palette'].append({'name':'Actual_Continuous_Backing',
            'color':'#'+''.join(f'{c:02X}' for c in dominant_rgb),
            'vertices':bv,'triangles':bt})
        preview=np.full((h,w,3),dominant_rgb[::-1],np.uint8)
        stats={'key':key,'source_size':[w,h],'source':texture,'palette':1,
            'polygons':1,'holes':0,'vertices':8,'triangles':12,
            'absorbed_isolated_source_pixels':removed}
        for color,rgb in enumerate(colors):
            if color==dominant:continue
            if not np.any(labels==color):continue
            polygons=polygons_from_mask(labels==color)
            if not polygons:continue
            vertices,triangles,holes=extrude(polygons,w,h)
            entry['palette'].append({'name':f'Actual_Contour_Color_{color:02d}',
                'color':'#'+''.join(f'{c:02X}' for c in rgb),
                'vertices':vertices,'triangles':triangles})
            stats['palette']+=1;stats['polygons']+=len(polygons);stats['holes']+=holes
            stats['vertices']+=len(vertices);stats['triangles']+=len(triangles)//3
            # Reconstruction from polygon artwork, never the original photograph.
            # Paint larger shells before nested islands; one raster mask per
            # palette layer avoids scanning the full image per glyph contour.
            temp=np.zeros((h,w),np.uint8)
            for poly in sorted(polygons,key=lambda p:p.area,reverse=True):
                cv2.fillPoly(temp,[np.rint(np.asarray(poly.exterior.coords)).astype(np.int32)],255)
                for ring in poly.interiors:
                    cv2.fillPoly(temp,[np.rint(np.asarray(ring.coords)).astype(np.int32)],0)
            preview[temp>0]=rgb[::-1]
            print('COLOR',num,color,'polys',len(polygons),'elapsed',round(time.monotonic()-t,1),flush=True)
        packet['textures'].append(entry);report.append(stats)
        cv2.imwrite(str(OUT/(key+'_VECTOR.png')),preview)
        previews.append((key,preview))
        (OUT/(key+'.json')).write_text(json.dumps(entry,separators=(',',':')))
        print(json.dumps(stats),flush=True)
    (OUT/'ACTUAL-SOURCE-RELIEF-PACKET.json').write_text(json.dumps(packet,separators=(',',':')))
    (OUT/'CONSTRUCTION-SUMMARY.json').write_text(json.dumps({'sources':report,
        'totals':{'textures':len(packet['textures']),'face_instances':len(sources),
        **{k:sum(s[k] for s in report) for k in ['palette','polygons','holes','vertices','triangles']}},
        'elapsed_seconds':round(time.monotonic()-t,1),
        'limits':['Source photograph epoch and source resolution retained. No surveyed equality claim.',
            'Source-blurred tiny glyphs cannot be recovered without inventing text.',
            'Up to12 untextured source-color layers approximate photographic gradients as continuous contour regions.']},indent=2))
    cellw,cellh=300,260; sheet=np.full(((len(previews)+4)//5*cellh,5*cellw,3),245,np.uint8)
    for i,(key,im) in enumerate(previews):
        h,w=im.shape[:2]; scale=min((cellw-12)/w,(cellh-42)/h)
        sm=cv2.resize(im,(round(w*scale),round(h*scale)),interpolation=cv2.INTER_AREA)
        xx=i%5*cellw+(cellw-sm.shape[1])//2; yy=i//5*cellh+8
        sheet[yy:yy+sm.shape[0],xx:xx+sm.shape[1]]=sm
        cv2.putText(sheet,key[:38],(i%5*cellw+6,i//5*cellh+cellh-14),cv2.FONT_HERSHEY_SIMPLEX,.36,(20,20,20),1,cv2.LINE_AA)
    cv2.imwrite(str(OUT/'VECTOR-ART-CONTACT-SHEET.png'),sheet)
    print('DONE',round(time.monotonic()-t,1),flush=True)

if __name__=='__main__':main()
