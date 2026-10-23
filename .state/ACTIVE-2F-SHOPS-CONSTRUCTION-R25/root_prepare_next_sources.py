from pathlib import Path
import requests,json,concurrent.futures,hashlib,re,sys
from bs4 import BeautifulSoup
from PIL import Image,ImageDraw
B=Path(__file__).parent
jobs=[('story-lamant','aru0219','224439792763'),('story-lamant','oyj10244','223446992106'),('hawaiian','zlsangkkun','224445787295'),('hawaiian','tkvlf14','224399139134'),('redbean-bread','gpwl98','224399041903'),('redbean-bread','gkdudtjr2','224325969836'),('boksoon','sjj926','224417262910'),('boksoon','ha_yeon_-','224383979559')]
def post(job):
 k,a,i=job;p=B/k/'sources';p.mkdir(exist_ok=True);key='ROOT-'+a+'-'+i;u='https://m.blog.naver.com/'+a+'/'+i
 r=requests.get(u,headers={'User-Agent':'Mozilla/5.0'},timeout=30);r.raise_for_status();(p/(key+'.html')).write_text(r.text);s=BeautifulSoup(r.text,'html.parser');body=s.select_one('.se-main-container') or s
 text=body.get_text(' ',strip=True);(p/(key+'.txt')).write_text(text);ims=[]
 for im in body.select('img.se-image-resource'):
  v=im.get('data-lazy-src') or im.get('data-src') or im.get('src','')
  if v.startswith('http'):
   v=v.split('?')[0]+'?type=w966'
   if v not in ims:ims.append(v)
 def dl(t):
  ix,v=t;d=requests.get(v,headers={'User-Agent':'Mozilla/5.0'},timeout=30);d.raise_for_status();f=p/(key+f'-{ix:02}.jpg');f.write_bytes(d.content);return {'index':ix,'url':v,'path':str(f),'sha256':hashlib.sha256(d.content).hexdigest()}
 records=[]
 with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:
  for f in ex.map(dl,enumerate(ims)):records.append(f)
 date=s.select_one('.blog_date') or s.select_one('.se_publishDate')
 if not date:
  found=re.findall(r'20[12]\d[.\-]\s*\d{1,2}[.\-]\s*\d{1,2}',r.text);date=' '.join(dict.fromkeys(found))[:250]
 else:date=date.get_text(' ',strip=True)
 out=Image.new('RGB',(1000,max(1,(len(ims)+3)//4)*172),'white');d=ImageDraw.Draw(out)
 for ix in range(len(ims)):
  try:
   im=Image.open(p/(key+f'-{ix:02}.jpg'));im.thumbnail((245,147));x=ix%4*250;y=ix//4*172;out.paste(im,(x,y));d.text((x,y+149),f'{ix:02}',fill='black')
  except:pass
 out.save(p/(key+'-contact.jpg'))
 receipt={'store_key':k,'url':u,'title':s.title.get_text() if s.title else '', 'source_date':str(date),'images':records,'images_directly_viewed':False,'exact_branch_epoch_must_be_bound_by_constructor':True}
 (p/(key+'-INDEX.json')).write_text(json.dumps(receipt,ensure_ascii=False,indent=2));return {'store':k,'post':a+'/'+i,'source_date':str(date),'images':len(records),'contact':str(p/(key+'-contact.jpg'))}
if __name__=='__main__':
 if len(sys.argv)>1:jobs=json.loads(Path(sys.argv[1]).read_text())
 with concurrent.futures.ThreadPoolExecutor(max_workers=4) as ex:
  fs={ex.submit(post,j):j for j in jobs}
  for fut in concurrent.futures.as_completed(fs):
   try:print(json.dumps(fut.result(),ensure_ascii=False),flush=True)
   except Exception as e:print(json.dumps({'job':fs[fut],'error':str(e)},ensure_ascii=False),flush=True)
