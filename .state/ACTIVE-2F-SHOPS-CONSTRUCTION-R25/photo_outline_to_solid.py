"""Model a physical sign from a field-photo mask; never creates scene text.

The source image is evidence only. The output is a closed, front/back/side mesh
with all letter holes retained, suitable for each store's own C180 constructor.
Image left becomes local +X so the word reads from its outward local +Z face.
"""
import cv2, numpy as np
from shapely.geometry import Polygon
from shapely import constrained_delaunay_triangles
from shapely.geometry.polygon import orient


def masked_polygons(mask, minimum_area=20, epsilon=.45):
    contours, hierarchy=cv2.findContours(mask.astype('uint8'),cv2.RETR_CCOMP,cv2.CHAIN_APPROX_SIMPLE)
    if hierarchy is None:return []
    polygons=[]
    for i, contour in enumerate(contours):
        if hierarchy[0,i,3]!=-1 or cv2.contourArea(contour)<minimum_area:continue
        outer=cv2.approxPolyDP(contour,epsilon,True).reshape(-1,2)
        holes=[];child=hierarchy[0,i,2]
        while child>=0:
            if cv2.contourArea(contours[child])>minimum_area*.05:
                holes.append(cv2.approxPolyDP(contours[child],epsilon,True).reshape(-1,2))
            child=hierarchy[0,child,0]
        p=Polygon(outer,holes).buffer(0)
        polygons.extend([p] if p.geom_type=='Polygon' else list(p.geoms))
    return polygons


def closed_solid_from_mask(mask, width, height, depth=.035, minimum_area=20, epsilon=.45):
    polygons=masked_polygons(mask,minimum_area,epsilon)
    h,w=mask.shape[:2];vertices=[];triangles=[]
    def point(x,y,z):return [-(x/w-.5)*width,(.5-y/h)*height,z]
    def triangle(a,b,c):
        k=len(vertices);vertices.extend([a,b,c]);triangles.extend([k,k+1,k+2])
    for p in polygons:
        p=orient(p,sign=1)
        for t in constrained_delaunay_triangles(p).geoms:
            a,b,c=list(t.exterior.coords)[:3]
            # Ensure mathematical +Z front normal after both image-axis flips.
            if np.cross(np.array(b)-a,np.array(c)-a)<0:b,c=c,b
            triangle(point(*a,0),point(*b,0),point(*c,0))
            triangle(point(*c,-depth),point(*b,-depth),point(*a,-depth))
        for ring in [p.exterior,*p.interiors]:
            coords=list(ring.coords)
            for a,b in zip(coords,coords[1:]):
                aa,bb,cc,dd=point(*a,-depth),point(*b,-depth),point(*b,0),point(*a,0)
                triangle(aa,bb,cc);triangle(aa,cc,dd)
    return {'vertices':vertices,'triangles':triangles,'depth_m':depth,'source_polygons':len(polygons),'front_normal_local':[0,0,1],'source_raster_used_as_visible_plane':False,'text_component':False}
