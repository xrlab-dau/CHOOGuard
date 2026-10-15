import pathlib,subprocess,sys,json
p=pathlib.Path(__file__).parent
r=subprocess.run(['aside','repl',(p/sys.argv[1]).read_text()],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
(p/(sys.argv[2] if len(sys.argv)>2 else pathlib.Path(sys.argv[1]).stem+'.txt')).write_text(r.stdout)
for l in r.stdout.splitlines():
 if l.startswith('SEARCH_JSON:'):
  d=json.loads(l[12:]);print(d['query']);print(d['text'][:9500]);print(json.dumps(d['links'][:45],ensure_ascii=False))
 elif l.startswith('PAGE_JSON:'):
  d=json.loads(l[10:]);(p/(d['id']+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2));print(d['id'],d['url'],d['title']);print(d['text'][:6500]);print('IMAGES',len(d['images']))
 else:print(l[:1200])
sys.exit(r.returncode)
