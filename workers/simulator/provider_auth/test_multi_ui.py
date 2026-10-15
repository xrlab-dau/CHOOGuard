"""Node VM DOM harness; does not claim native browser verification."""
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest
from browser_ui import multi_page


class MultiUiTests(unittest.TestCase):
    def test_script_syntax_and_cross_provider_single_model_choice(self):
        node=shutil.which('node')
        if not node:self.skipTest('Existing Node required')
        html=multi_page('fixture-nonce');script=re.search(r'<script nonce="fixture-nonce">(.*?)</script>',html,re.S).group(1)
        with tempfile.TemporaryDirectory(prefix='chooguard-ui-node-') as t:
            path=Path(t)/'script.js';path.write_text(script)
            result=subprocess.run([node,'--check',str(path)],capture_output=True,text=True)
            self.assertEqual(0,result.returncode,result.stderr)
        harness=r'''
const vm=require('vm');let source='SCRIPT_PLACEHOLDER';source=JSON.parse(source);
class Element { constructor(id){this.id=id;this.value='';this.checked=false;this.disabled=false;this.options=[];this.dataset={};this.textContent='';}replaceChildren(...x){this.options=x;this.value=x[0]?.value||'';}add(x){this.options.push(x);} }
const elements=new Map();const document={getElementById:id=>{if(!elements.has(id))elements.set(id,new Element(id));return elements.get(id);}};
class Option {constructor(text,value){this.text=text;this.value=value;this.disabled=false;}}
let calls=[],server={provider:'',model:'',selection:null,cloud_enabled:false,accounts:[{id:'A',provider_id:'provider-a',label:'Account A',signed_in:true,login_state:'signed_in',catalog_state:'ready',inference_state:'catalog_eligible_quota_checked_on_request'},{id:'B',provider_id:'provider-b',label:'Account B',signed_in:true,login_state:'signed_in',catalog_state:'ready',inference_state:'catalog_eligible_quota_checked_on_request'}],providers:[{id:'provider-a',label:'Provider A',registered:true},{id:'provider-b',label:'Provider B',registered:true},{id:'anthropic',label:'Claude',registered:false,reason:'provider_policy_restricted'}],usage:{input_tokens:0,output_tokens:0},initial_world:{remaining:0},total_admitted_attempts:0};
const models=[{provider_id:'provider-a',account_id:'A',model_id:'same',display_name:'Same',catalog_version:'a'.repeat(64),selectable:true},{provider_id:'provider-b',account_id:'B',model_id:'same',display_name:'Same',catalog_version:'b'.repeat(64),selectable:true}];
const fetch=async(path,opts={})=>{const body=opts.body?JSON.parse(opts.body):null;calls.push({path,body});let result=server;if(path==='/models')result={models};if(path==='/session'){if(body.model){server.provider=body.provider_id;server.model=body.model;server.cloud_enabled=body.cloud_enabled;server.selection={provider_id:body.provider_id,account_id:body.account_id,model_id:body.model};}if(body.initial_world_allowance)server.initial_world.remaining=1;}return {ok:true,json:async()=>JSON.parse(JSON.stringify(result))};};
const context={document,Option,fetch,location:{hash:''},history:{replaceState(){}},window:{open(){}},setTimeout,clearTimeout,console};vm.runInNewContext(source,context);
(async()=>{await new Promise(r=>setImmediate(r));await elements.get('catalog').onclick();if(elements.get('model').options.length!==3)throw Error('Missing union catalog');elements.get('model').value='1';elements.get('cloud').checked=true;await elements.get('choose').onclick();const request=calls.find(c=>c.path==='/session'&&c.body.model);if(request.body.provider_id!=='provider-b'||request.body.account_id!=='B'||request.body.model!=='same'||request.body.catalog_version!=='b'.repeat(64))throw Error('Cross-provider choice lost its account binding');elements.get('provider').value='anthropic';elements.get('provider').onchange();if(!elements.get('login').disabled||!elements.get('providerReason').textContent.includes('policy_restricted'))throw Error('Restricted provider displayed as loginable');if(calls.some(c=>c.path.includes('/chat/completions')))throw Error('UI auto-called a model');console.log('DOM_HARNESS_PASS');})().catch(e=>{console.error(e.message);process.exitCode=1;});
'''
        harness=harness.replace("'SCRIPT_PLACEHOLDER'",json.dumps(json.dumps(script)))
        result=subprocess.run([node,'-e',harness],capture_output=True,text=True,timeout=10)
        self.assertEqual(0,result.returncode,result.stderr);self.assertIn('DOM_HARNESS_PASS',result.stdout)


if __name__=='__main__':unittest.main()
