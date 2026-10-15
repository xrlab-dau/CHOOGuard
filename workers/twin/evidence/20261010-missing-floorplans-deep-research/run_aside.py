import base64
import json
import pathlib
import subprocess
import sys

folder = pathlib.Path(__file__).parent
script = folder / sys.argv[1]
result = subprocess.run(['aside', '--session', '2026-10-09_SJFqp9IKNOOmaH9x', 'repl', script.read_text()], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
safe_lines = []
for line in result.stdout.splitlines():
    if line.startswith('ASSET_JSON:'):
        item = json.loads(line[len('ASSET_JSON:'):])
        payload = base64.b64decode(item.pop('base64'))
        (folder / item['filename']).write_bytes(payload)
        (folder / (item['id'] + '.json')).write_text(json.dumps(item, ensure_ascii=False, indent=2))
        safe_lines.append('ASSET_METADATA:'+json.dumps(item, ensure_ascii=False))
        print(json.dumps(item, ensure_ascii=False))
    elif line.startswith('SOURCE_JSON:'):
        safe_lines.append(line)
        item = json.loads(line[len('SOURCE_JSON:'):])
        (folder / (item['id'] + '.json')).write_text(json.dumps(item, ensure_ascii=False, indent=2))
        print(item['id'], item.get('query', item.get('url')))
        print(item.get('text', '')[:2600])
        print(json.dumps([{**x,'url':x.get('url','')[:240]} for x in item.get('links', []) if x.get('url') and 'google.com' not in x.get('url','')][:8], ensure_ascii=False))
    else:
        safe_lines.append(line)
        if len(line)<600:
            print(line)
(folder / (script.stem + '.stdout.log')).write_text('\n'.join(safe_lines))
sys.exit(result.returncode)
