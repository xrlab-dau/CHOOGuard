#!/usr/bin/env python3
"""Clip verified GBA raster and describe unknown OSM footprints; never edit OSM."""
import hashlib,json,math,re,xml.etree.ElementTree as ET
from pathlib import Path
import numpy as np
import rasterio
from rasterio.warp import transform_bounds,transform_geom
from rasterio.windows import from_bounds,Window
from rasterio.features import geometry_mask,geometry_window
OUT=Path('asset-library/space-references/busan-reconstruction/building-height-source')
BASE=OUT.parent
NAME='129.0_35.2_129.2_35.0_sr_ss.tif'
BBOX=[129.02,35.09,129.065,35.127]
receipt=json.loads((OUT/'gba-acquisition-receipt.json').read_text())
assert receipt['strictRangeAndCrcVerified']
assert hashlib.sha256((OUT/NAME).read_bytes()).hexdigest()==receipt['tiffSha256']
source=BASE/'busan-openworld.json';xml=BASE/'osm-busan-openworld.xml'
original_hash=hashlib.sha256(source.read_bytes()).hexdigest();xml_hash=hashlib.sha256(xml.read_bytes()).hexdigest()
j=json.loads(source.read_text());buildings=[f for f in j['features'] if f['kind']=='building']
root=ET.parse(xml).getroot();nodes={n.attrib['id']:[float(n.attrib['lon']),float(n.attrib['lat'])] for n in root.findall('node')};ways={w.attrib['id']:w for w in root.findall('way')}
records=[];comparisons=[]
with rasterio.open(OUT/NAME) as src:
 bounds=transform_bounds('EPSG:4326',src.crs,*BBOX,densify_pts=21)
 w=from_bounds(*bounds,transform=src.transform)
 window=Window(math.floor(w.col_off),math.floor(w.row_off),math.ceil(w.col_off+w.width)-math.floor(w.col_off),math.ceil(w.row_off+w.height)-math.floor(w.row_off))
 arr=src.read(window=window);profile=src.profile.copy();profile.update(width=arr.shape[2],height=arr.shape[1],transform=src.window_transform(window),compress='deflate',tiled=True,blockxsize=256,blockysize=256)
 with rasterio.open(OUT/'busan-roi-gba-height.tif','w',**profile) as dst:
  dst.write(arr);dst.update_tags(source_url=receipt['url'],height_semantics='ML estimated height in metres; not survey',epoch='2019 imagery with possible 2018 supplementation; tile epoch unspecified')
 metadata={'crs':str(src.crs),'epsg':src.crs.to_epsg(),'width':src.width,'height':src.height,'count':src.count,'dtype':src.dtypes[0],'nodata':src.nodata,'transform':list(src.transform),'resolution':list(src.res),'scales':list(src.scales),'offsets':list(src.offsets),'unitTags':list(src.units),'tags':src.tags(),'valueUnit':'metres from publisher height semantics; TIFF unit tag absent','epoch':'2019 primary imagery, 2018 fallback; tile acquisition date not encoded','window':{'colOffset':window.col_off,'rowOffset':window.row_off,'width':window.width,'height':window.height},'clipBounds':list(rasterio.windows.bounds(window,src.transform))}
 for f in buildings:
  if not f['height'] and f['levels']:continue
  known=bool(f['height'])
  rec={'osmWayId':f['id'],'originalHeight':f['height'],'originalLevels':f['levels'],'source':'GBA.Height','classification':'ML_estimate_not_survey','status':'unresolved','heightEstimateM':None}
  way=ways.get(f['id'])
  if way is None:rec['reason']='OSM way missing';records.append(rec);continue
  coords=[nodes[n.attrib['ref']] for n in way.findall('nd')]
  if coords[0]!=coords[-1]:rec['reason']='Unclosed footprint';records.append(rec);continue
  geom=transform_geom('EPSG:4326',src.crs,{'type':'Polygon','coordinates':[coords]})
  try:
   fw=geometry_window(src,[geom]);data=src.read(1,window=fw,masked=True)
   inside=geometry_mask([geom],out_shape=data.shape,transform=src.window_transform(fw),invert=True,all_touched=False)
   valid=inside & ~np.ma.getmaskarray(data) & np.isfinite(data.data) & (data.data>=0)
   vals=data.data[valid];count=int(inside.sum());n=int(vals.size)
   rec.update(pixelSelection='pixel centres within original OSM polygon',footprintPixelCount=count,validPixelCount=n,validFraction=n/count if count else 0)
   if n:
    rec['statisticsM']={'min':float(vals.min()),'median':float(np.median(vals)),'p90':float(np.percentile(vals,90)),'max':float(vals.max()),'mean':float(vals.mean()),'std':float(vals.std())}
   if n>=4 and n/count>=0.8 and float(np.median(vals))>0:
    rec.update(status='reviewable_estimate',heightEstimateM=float(vals.max()),estimator='maximum_valid_pixel_height',uncertainty='Separate variance tile not acquired; within-footprint spread is not calibrated uncertainty')
   else:rec['reason']='Insufficient valid interior pixels or nonpositive prediction'
  except Exception as e:rec['reason']=str(e)
  tags={t.attrib['k']:t.attrib['v'] for t in way.findall('tag')}
  start_year=re.match(r'^(\d{4})',tags.get('start_date',''))
  if tags.get('building')=='construction' or (start_year and int(start_year.group(1))>2019):
   rec.update(status='unresolved',heightEstimateM=None,reason='Construction or start_date later than primary imagery epoch')
  if rec.get('statisticsM') and rec['statisticsM']['max']<2:
   rec['reviewFlags']=['Sub-2m prediction; potential absent building, low structure or temporal mismatch; no automatic height assignment']
  if known:
   match=re.fullmatch(r'\s*([0-9]+(?:\.[0-9]+)?)\s*(?:m)?\s*',f['height'])
   comparisons.append({'osmWayId':f['id'],'osmHeightTag':f['height'],'osmNumericHeightM':float(match.group(1)) if match else None,'statisticsM':rec.get('statisticsM'),'validPixelCount':rec.get('validPixelCount',0),'validFraction':rec.get('validFraction',0)})
  else:records.append(rec)
assert len(records)==665
assert hashlib.sha256(source.read_bytes()).hexdigest()==original_hash
assert hashlib.sha256(xml.read_bytes()).hexdigest()==xml_hash
result={'schemaVersion':'1.0','sourceInputSha256':original_hash,'sourceXmlSha256':xml_hash,'sourceUrl':receipt['url'],'paperUrl':'https://essd.copernicus.org/articles/17/6647/2025/','license':'CC BY-NC 4.0','notSurvey':True,'knownEvidencePreserved':{'heightTagged':sum(bool(f['height']) for f in buildings),'levelsOnly':sum(not f['height'] and bool(f['levels']) for f in buildings)},'unknownFootprints':len(records),'reviewableEstimates':sum(r['status']=='reviewable_estimate' for r in records),'policy':'Separate lookup only. Original tags untouched. Max interior valid pixel; >=4 pixels and >=80% valid. Max is sensitive to roof features and footprint alignment. No claims of ground-truth accuracy.','records':records}
summaries={}
for key in ['median','p90','max']:
 pairs=[(c['osmNumericHeightM'],c['statisticsM'][key]) for c in comparisons if c['osmNumericHeightM'] is not None and c['statisticsM'] and c['validPixelCount']>=4 and c['validFraction']>=0.8]
 errors=np.array([pred-tag for tag,pred in pairs])
 summaries[key]={'pairs':len(pairs),'meanSignedDifferenceM':float(errors.mean()) if len(errors) else None,'medianAbsoluteDifferenceM':float(np.median(np.abs(errors))) if len(errors) else None,'meanAbsoluteDifferenceM':float(np.abs(errors).mean()) if len(errors) else None}
(OUT/'osm-tag-consistency.json').write_text(json.dumps({'purpose':'Source consistency only, not ground-truth accuracy; OSM tag dates and model imagery dates differ','knownTaggedFeatures':len(comparisons),'aggregations':summaries,'records':comparisons},indent=2)+'\n')
(OUT/'unknown-building-height-estimates.json').write_text(json.dumps(result,indent=2)+'\n');(OUT/'raster-metadata.json').write_text(json.dumps(metadata,indent=2)+'\n')
print(json.dumps({k:v for k,v in result.items() if k!='records'},indent=2));print(json.dumps(metadata,indent=2))
