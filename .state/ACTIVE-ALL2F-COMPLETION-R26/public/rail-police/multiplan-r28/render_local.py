import json
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
r=Path(__file__).parent
a=json.loads((r/'CURRENT-LOCAL-WALL-COLUMN-GEOMETRY.json').read_text())
for key,xlim,ylim in [('local',(20,92),(-24,9)),('all',(-15,100),(-50,20))]:
 scale=24;w=int((xlim[1]-xlim[0])*scale)+80;h=int((ylim[1]-ylim[0])*scale)+80
 im=Image.new('RGB',(w,h),'white');dd=ImageDraw.Draw(im)
 def xy(p):return(40+(p[0]-xlim[0])*scale,40+(p[1]-ylim[0])*scale)
 for x in range(xlim[0]//5*5,xlim[1]+1,5):
  dd.line([xy((x,ylim[0])),xy((x,ylim[1]))],fill='#eeeeee');dd.text((xy((x,ylim[0]))[0],10),str(x),fill='black')
 for y in range(ylim[0]//5*5,ylim[1]+1,5):
  dd.line([xy((xlim[0],y)),xy((xlim[1],y))],fill='#eeeeee');dd.text((2,xy((xlim[0],y))[1]),str(y),fill='black')
 for o in a:
  iswall='WALL-FOOTPRINT' in o['path'];native='OfficialStation' in o['path'];col=any(s in o['path'] for s in ['NATIVE_COL','C177','NATIVE_EXTENSION'])
  if not(iswall or native or col):continue
  for p in o['footprints_cad']:
   dd.line([xy((v[0],-v[1])) for v in p['exterior']],fill='#bbbbbb' if native else '#333333',width=1 if native else 2)
  q=o['bounds_qx_qy']
  if iswall and q[0]>=xlim[0] and q[2]<=xlim[1] and q[1]>=ylim[0] and q[3]<=ylim[1]:dd.text(xy(((q[0]+q[2])/2,(q[1]+q[3])/2)),o['id'],fill='blue')
 im.save(r/f'analysis-cad-{key}.png')
