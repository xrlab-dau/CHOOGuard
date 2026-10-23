#!/opt/homebrew/bin/python3
"""Observed-card-only source extraction; existing full-face UV frame stays fixed."""
from pathlib import Path
from collections import Counter
import json,hashlib,math
import numpy as np
import cv2
import build_vector_reliefs as b

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
CARDS={
 32:{'quad':[[0,23],[87,19],[92,177],[0,180]],
     'clip':[[0,24],[74,19],[81,22],[86,30],[89,60],[92,166],[88,175],[0,179]],
     'truncated_left':True},
 33:{'quad':[[28,16],[118,15],[121,163],[31,164]],
     'clip':[[29,22],[38,15],[106,15],[115,20],[119,28],[121,153],[118,161],[35,163],[30,158],[28,25]],
     'truncated_left':False},
 34:{'quad':[[14,29],[109,29],[108,160],[13,161]],
     'clip':[[15,36],[23,29],[98,29],[106,35],[109,43],[108,152],[104,159],[21,161],[14,156],[13,43]],
     'truncated_left':False},
 35:{'quad':[[0,39],[111,47],[107,161],[0,158]],
     'clip':[[0,40],[103,46],[110,50],[107,152],[103,160],[0,157]],
     'truncated_left':True}
}

def key_for(num,texture):
    import re
    return f't{num:02d}_'+re.sub('[^a-z0-9]+','_',Path(texture).stem.lower()).strip('_')

def rectify(image,spec):
    h,w=image.shape[:2]
    quad=np.asarray(spec['quad'],np.float32)
    clip=np.asarray(spec['clip'],np.int32)
    dst_w=round((np.linalg.norm(quad[1]-quad[0])+np.linalg.norm(quad[2]-quad[3]))*.5)
    dst_h=round((np.linalg.norm(quad[3]-quad[0])+np.linalg.norm(quad[2]-quad[1]))*.5)
    source_mask=np.zeros((h,w),np.uint8)
    cv2.fillPoly(source_mask,[clip],255)
    hsv=cv2.cvtColor(image,cv2.COLOR_BGR2HSV)
    paper_pixels=image[(source_mask>0)&(hsv[:,:,1]<70)&(hsv[:,:,2]>120)]
    paper=np.median(paper_pixels,axis=0).astype(np.uint8)
    destination=np.asarray([[0,0],[dst_w-1,0],[dst_w-1,dst_h-1],[0,dst_h-1]],np.float32)
    transform=cv2.getPerspectiveTransform(quad,destination)
    artwork=cv2.warpPerspective(image,transform,(dst_w,dst_h),flags=cv2.INTER_LINEAR)
    mask=cv2.warpPerspective(source_mask,transform,(dst_w,dst_h),flags=cv2.INTER_NEAREST)
    # Keep the current face silhouette: observed rounded-edge corner exclusions
    # receive actual paper color, never surrounding photographic contents.
    artwork[mask==0]=paper
    return artwork,paper,transform

def vectorize(num,texture,spec):
    source=ROOT/texture
    original=cv2.imread(str(source),cv2.IMREAD_COLOR)
    orig_h,orig_w=original.shape[:2]
    artwork,paper,transform=rectify(original,spec)
    h,w=artwork.shape[:2]
    key=key_for(num,texture)
    labels,colors,removed=b.palette_labels(artwork,texture)
    # The actual paper tone is the physical backing; choose nearest palette tone
    # to the observed low-saturation card paper sample.
    paper_rgb=paper[::-1]
    dominant=int(np.argmin([np.sum((rgb.astype(float)-paper_rgb.astype(float))**2) for rgb in colors]))
    dominant_rgb=colors[dominant]
    bv,bt=b.backing_slab()
    xx=[p[0] for p in spec['clip']];yy=[p[1] for p in spec['clip']]
    entry={'texture':texture,'key':key,'source_uv_bounds':[0,0,1,1],
        'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
        'source_size':[orig_w,orig_h],'artwork_raster_size':[w,h],
        'artwork_reframed_to_existing_full_face_uv':True,
        'source_art_crop_uv_bounds':[min(xx)/orig_w,1-max(yy)/orig_h,max(xx)/orig_w,1-min(yy)/orig_h],
        'actual_card_source_quad_pixels':spec['quad'],
        'actual_card_source_clip_polygon_pixels':spec['clip'],
        'source_to_artwork_homography':transform.tolist(),
        'source_left_edge_truncated':spec['truncated_left'],
        'photo_surroundings_excluded':True,
        'silhouette':'Existing physical face bounds retained; excluded observed-card corner pixels use sampled actual paper backing.',
        'palette':[{'name':'Actual_Card_Paper_Backing','color':'#'+''.join(f'{c:02X}' for c in dominant_rgb),'vertices':bv,'triangles':bt}]}
    preview=np.full((h,w,3),dominant_rgb[::-1],np.uint8)
    stats={'key':key,'source_size':[orig_w,orig_h],'artwork_raster_size':[w,h],
        'source':texture,'palette':1,'polygons':1,'holes':0,'vertices':8,'triangles':12,
        'absorbed_isolated_source_pixels':removed,
        'actual_card_only':True,'source_left_edge_truncated':spec['truncated_left']}
    for color,rgb in enumerate(colors):
        if color==dominant or not np.any(labels==color):continue
        polygons=b.polygons_from_mask(labels==color)
        if not polygons:continue
        vertices,triangles,holes=b.extrude(polygons,w,h)
        entry['palette'].append({'name':f'Actual_Card_Ink_Color_{color:02d}',
            'color':'#'+''.join(f'{c:02X}' for c in rgb),'vertices':vertices,'triangles':triangles})
        stats['palette']+=1;stats['polygons']+=len(polygons);stats['holes']+=holes
        stats['vertices']+=len(vertices);stats['triangles']+=len(triangles)//3
        mask=np.zeros((h,w),np.uint8)
        for polygon in sorted(polygons,key=lambda p:p.area,reverse=True):
            cv2.fillPoly(mask,[np.rint(np.asarray(polygon.exterior.coords)).astype(np.int32)],255)
            for ring in polygon.interiors:cv2.fillPoly(mask,[np.rint(np.asarray(ring.coords)).astype(np.int32)],0)
        preview[mask>0]=rgb[::-1]
    (OUT/(key+'.json')).write_text(json.dumps(entry,separators=(',',':')))
    cv2.imwrite(str(OUT/(key+'_CARD-ONLY-RECTIFIED.png')),artwork)
    cv2.imwrite(str(OUT/(key+'_VECTOR.png')),preview)
    return stats

def contact_sheet(index):
    cellw,cellh=300,260
    sheet=np.full(((len(index)+4)//5*cellh,5*cellw,3),245,np.uint8)
    for i,item in enumerate(index):
        im=cv2.imread(str(OUT/(item['key']+'_VECTOR.png')))
        h,w=im.shape[:2];scale=min((cellw-12)/w,(cellh-42)/h)
        sm=cv2.resize(im,(round(w*scale),round(h*scale)),interpolation=cv2.INTER_AREA)
        xx=i%5*cellw+(cellw-sm.shape[1])//2;yy=i//5*cellh+8
        sheet[yy:yy+sm.shape[0],xx:xx+sm.shape[1]]=sm
        cv2.putText(sheet,item['key'][:38],(i%5*cellw+6,i//5*cellh+cellh-14),cv2.FONT_HERSHEY_SIMPLEX,.36,(20,20,20),1,cv2.LINE_AA)
    cv2.imwrite(str(OUT/'VECTOR-ART-CONTACT-SHEET.png'),sheet)
    crops=np.full((520,4*300,3),245,np.uint8)
    for j,num in enumerate(CARDS):
        item=index[num]
        for row,suffix in enumerate(['_CARD-ONLY-RECTIFIED.png','_VECTOR.png']):
            im=cv2.imread(str(OUT/(item['key']+suffix)))
            h,w=im.shape[:2];scale=min(280/w,230/h)
            sm=cv2.resize(im,(round(w*scale),round(h*scale)),interpolation=cv2.INTER_NEAREST)
            x=j*300+(300-sm.shape[1])//2;y=row*260+10
            crops[y:y+sm.shape[0],x:x+sm.shape[1]]=sm
            cv2.putText(crops,('RECTIFIED ' if row==0 else 'VECTOR ')+str(num),(j*300+10,row*260+253),cv2.FONT_HERSHEY_SIMPLEX,.5,(20,20,20),1,cv2.LINE_AA)
    cv2.imwrite(str(OUT/'HWANG-CARD-ONLY-ARTWORK.png'),crops)

def main():
    faces=json.loads((OUT.parent/'RASTER-SIGNS-2F.json').read_text())['items']
    textures=list(dict.fromkeys(a['texture'] for a in faces))
    index=[{'texture':texture,'key':key_for(i,texture),'relative_file':key_for(i,texture)+'.json'} for i,texture in enumerate(textures)]
    updates={}
    for num,spec in CARDS.items():
        stats=vectorize(num,textures[num],spec);updates[stats['source']]=stats
        print(json.dumps(stats),flush=True)
    (OUT/'ACTUAL-SOURCE-RELIEF-INDEX.json').write_text(json.dumps({'textures':index},indent=2))
    # Stream already-final entries from their individual files. Other32 entries
    # stay byte-for-byte unchanged and native imports can use the tiny index.
    packet=OUT/'ACTUAL-SOURCE-RELIEF-PACKET.json'
    with packet.open('w') as f:
        f.write('{"schema":"ActualSourceContinuousReliefV1","textures":[')
        for n,item in enumerate(index):
            if n:f.write(',')
            with (OUT/item['relative_file']).open() as source:
                for block in iter(lambda:source.read(4*1024*1024),''):f.write(block)
        f.write('],"construction":')
        json.dump({'palette_colors_max':b.COLORS,'large_source_palette_colors_max':8,
            'pixel_edge_contours':True,'simplify_tolerance_source_pixels_small_glyphs':.6,
            'simplify_tolerance_source_pixels_larger_regions':.8,
            'closed_front_back_and_all_ring_sides':True,
            'physical_layers':{'backing_z':[0,.12],'actual_source_ink_z':[.12,1]},
            'source_local_stroke_protection':{'dark_delta':6,'light_delta':10,'neighborhood':15},
            'fonts_or_text_components':False,'textured_image_planes':False,'source_assets_immutable':True,
            'hwang_card_only_source_rectification':True},f,separators=(',',':'))
        f.write('}')
    summary=json.loads((OUT/'CONSTRUCTION-SUMMARY.json').read_text())
    summary['sources']=[updates.get(a['source'],a) for a in summary['sources']]
    keys=['palette','polygons','holes','vertices','triangles']
    summary['totals']={'textures':len(textures),'face_instances':len(faces),
        **{k:sum(a[k] for a in summary['sources']) for k in keys}}
    frequencies=Counter(a['texture'] for a in faces)
    for k in ['vertices','triangles']:
        summary['totals']['expanded_face_instance_'+k]=sum(a[k]*frequencies[a['source']] for a in summary['sources'])
    sha=hashlib.sha256()
    with packet.open('rb') as f:
        for block in iter(lambda:f.read(4*1024*1024),b''):sha.update(block)
    summary['packet']={'path':str(packet),'sha256':sha.hexdigest(),'bytes':packet.stat().st_size}
    summary['per_texture_index']=str(OUT/'ACTUAL-SOURCE-RELIEF-INDEX.json')
    summary['largest_sources']=sorted(summary['sources'],key=lambda a:a['triangles'],reverse=True)[:6]
    summary['limits']=[a for a in summary['limits'] if not a.startswith('Four Hwang2F')]
    summary['limits'].append('Four Hwang cards are clipped/rectified to actual observed card artwork. Source0/3 already truncate the left card edge; missing source artwork is not invented. Existing physical face UV bounds and silhouette remain fixed.')
    summary['hwang_card_only_corrections']=list(updates.values())
    (OUT/'CONSTRUCTION-SUMMARY.json').write_text(json.dumps(summary,indent=2))
    contact_sheet(index)
    print(json.dumps({'totals':summary['totals'],'packet':summary['packet'],'index':summary['per_texture_index']},indent=2),flush=True)

if __name__=='__main__':main()
