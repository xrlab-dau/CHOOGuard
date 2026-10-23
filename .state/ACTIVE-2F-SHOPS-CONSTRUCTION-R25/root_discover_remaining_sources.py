import requests,bs4,concurrent.futures,json,pathlib,urllib.parse,re,sys
B=pathlib.Path(__file__).parent
jobs=[('storyway-north','부산역 2층 파스쿠찌 앞 스토리웨이'),('weenybeeny','부산역 위니비니'),('railway-goods','부산역 철도 기념품 굿즈샵 2층'),('starlight-sand','부산역 별빛샌드'),('olive-young','부산역 2층 올리브영'),('bnc-west','부산역 2층 비엔씨제과'),('hwang-2f','부산역 2층 환공어묵'),('storyway-southwest','부산역 2층 신맞이방 스토리웨이'),('yeongjudang','부산역 영주당 부산과자집'),('krispy-southwest','부산역 신맞이방 크리스피크림도넛'),('busan-oden','부산역 2층 부산오뎅'),('cheongdo-gimbap','부산역 2층 청도할매김밥'),('banwoldang','부산역 2층 반월당닭강정'),('palgong','부산역 2층 팔공티'),('namcheon','부산역 2층 남천할매떡볶이')]
def f(t):
 k,q=t;u='https://search.naver.com/search.naver?ssc=tab.blog.all&query='+urllib.parse.quote(q);r=requests.get(u,headers={'User-Agent':'Mozilla/5.0'},timeout=35);r.raise_for_status();p=B/k/'sources';p.mkdir(exist_ok=True);(p/'ROOT-NAVER-SOURCE-SEARCH.html').write_text(r.text);s=bs4.BeautifulSoup(r.text,'html.parser');out=[];seen=set()
 for a in s.select('a[href]'):
  v=a['href']
  if re.match(r'https://(?:m\.)?blog\.naver\.com/[^/?]+/\d+',v) and v not in seen:seen.add(v);out.append({'url':v,'title':a.get_text(' ',strip=True)[:220]})
 (p/'ROOT-NAVER-SOURCE-SEARCH.json').write_text(json.dumps({'query':q,'url':u,'links':out,'source_branch_not_yet_bound':True},ensure_ascii=False,indent=2));return k,out[:4]
if len(sys.argv)>1:jobs=json.loads(pathlib.Path(sys.argv[1]).read_text())
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as ex:
 fs={ex.submit(f,j):j for j in jobs}
 for fut in concurrent.futures.as_completed(fs):
  try:print(json.dumps(fut.result(),ensure_ascii=False),flush=True)
  except Exception as e:print(json.dumps({'job':fs[fut],'error':str(e)},ensure_ascii=False),flush=True)
