import pathlib, subprocess, sys
folder = pathlib.Path(__file__).parent
script = folder / sys.argv[1]
output = folder / (sys.argv[2] if len(sys.argv)>2 else script.stem+'.txt')
result = subprocess.run(['aside','repl',script.read_text()],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
output.write_text(result.stdout)
for line in result.stdout.splitlines():
    if line.startswith('SEARCH_JSON:'):
        import json
        d=json.loads(line[12:]); print(d['query']); print(d['text'][:5500]); print(json.dumps(d['links'][:35],ensure_ascii=False))
    elif line.startswith('PAGE_JSON:'):
        import json
        d=json.loads(line[10:]); (folder/(d['id']+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2))
        print(d['id'],d['url'],d['title']); print(d['text'][:3500]); print('IMAGES',len(d['images']))
        for im in d['images']:
            if im['w']>250 or 'blogthumb' in im['full']:
                print(im['n'],im['w'],im['h'],im['full'])
    elif 'ASSET_JSON:' not in line:
        print(line[:1800])
sys.exit(result.returncode)
