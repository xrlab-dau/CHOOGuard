#!/usr/bin/env python3
"""Measure complete current C# provider prompts using a historical 5,105-state world.

The transport is an in-memory capture. No helper, account, model, Unity engine or
credential is accessed. Graph roundtrip and both admission estimates are recorded.
"""
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time

SIMULATOR = Path(__file__).resolve().parents[1]
ROOT = SIMULATOR.parents[1]
sys.path.insert(0, str(SIMULATOR))
from run_checks import toolchain

PROBE = r'''using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ChooGuard.Seeded;
public static class CompleteProviderInputProbe {
public static int Main(string[] args) { try { Run(args).GetAwaiter().GetResult(); return 0; }
catch(Exception e) { Console.Error.WriteLine(e.ToString()); return 1; } }
static async Task Run(string[] args) {
var header=LocalTapeCodec.ParseJsonData(File.ReadLines(args[0]).First());
var world=WorldStateCodec.FromToken(header["initial_state"]);
if(world.Entities.Count!=5105 || world.Tick!=0)throw new Exception("Exact historical 5105 initial state required");
var rows=new JArray();
for(int i=1;i<args.Length-1;i++) {
var coverage=WorldCoverage.Parse(File.ReadAllText(args[i]));
var baseline=EmergenceComposer.CreateRequest(world,coverage,false);
var compact=EmergenceComposer.CreateRequest(world,coverage,true);
var original=(JObject)JObject.Parse(baseline.Json)["graph"];
var decoded=EmergenceGraphCodec.Decode((JObject)JObject.Parse(compact.Json)["graph"]);
if(!JToken.DeepEquals(original["entities"],decoded["entities"]) ||
!JToken.DeepEquals(original["visuals"],decoded["visuals"]) ||
!JToken.DeepEquals(original["link_sets"],decoded["link_sets"]) ||
!JToken.DeepEquals(original["state_schemas"],decoded["state_schemas"]))
throw new Exception("Complete graph roundtrip mismatch");
foreach(var variant in new[]{baseline,compact}) {
string body=null;
ModelTransport capture=(endpoint,key,payload,ct)=> {
if(key!=null)throw new Exception("No developer key allowed"); body=payload;
return Task.FromResult(new ModelHttpReply{Status=200,Body=JsonConvert.SerializeObject(new{
model="gpt-6.1-sol",usage=new{prompt_tokens=0,completion_tokens=0},choices=new[]{new{
message=new{content=JsonConvert.SerializeObject(new{basis_digest=variant.BasisDigest,seed=variant.Seed.ToString(),priors=new object[0]})}}}})}); };
var provider=new OpenAiCompatibleEmergenceProvider("http://127.0.0.1:1/v1/chat/completions","gpt-6.1-sol",null,512,capture);
var reply=await provider.Generate(variant,CancellationToken.None);
if(body==null || reply.Error!=null)throw new Exception("Production capture failed: "+reply.Error);
var payload=JObject.Parse(body);var messages=(JArray)payload["messages"];
long helperEstimate=messages.Sum(m=>(long)Encoding.UTF8.GetByteCount((string)m["content"])+32)+64;
long csharpEstimate=provider.EstimateInputTokens(variant);
string name=Path.GetFileNameWithoutExtension(args[i])+"-"+(ReferenceEquals(variant,compact)?"compact":"raw");
File.WriteAllText(Path.Combine(args.Last(),name+".body.json"),body,new UTF8Encoding(false));
rows.Add(new JObject{["variant"]=name,["world_entities"]=world.Entities.Count,["visual_rows"]=coverage.visual_parts.Length,
["graph_encoding"]=(string)JObject.Parse(variant.Json)["graph"]["encoding"]??"raw",
["graph_roundtrip_preserved"]=true,["body_utf8_bytes"]=Encoding.UTF8.GetByteCount(body),
["client_model_input_sha256"]=provider.InputFingerprint(variant),["request_utf8_bytes"]=Encoding.UTF8.GetByteCount(variant.Json),
["csharp_reservation_units"]=csharpEstimate,["helper_reservation_units"]=helperEstimate,
["csharp_256000_admits"]=csharpEstimate<=256000,["helper_default_192000_admits"]=helperEstimate<=192000,
["helper_max_256000_admits"]=helperEstimate<=256000,["output_tokens_requested"]=512,
["coverage_sha256"]=Stable.Hash(File.ReadAllText(args[i])),["world_digest"]=world.Digest(),
["source_epoch_claim"]="Historical frozen ec2f 5105-state baseline with current source serializer; not current original/Unity proof"});
}
}
Console.WriteLine(rows.ToString(Formatting.None));
}
}'''


def main():
    mono, compiler = toolchain()
    output = ROOT / 'output/simulator/all-domains-provider/full-input'
    run = output / (str(time.time_ns()) + '-' + str(os.getpid()))
    run.mkdir(parents=True)
    world = ROOT / 'output/simulator/platform-ktx-unity/Runs/seed-20261013-20261014T013756412.jsonl'
    coverage = [SIMULATOR / 'data/semantic-coverage.json', ROOT / 'output/simulator/platform-ktx-unity/Assets/Simulator/Resources/SemanticCoverage.json']
    sources = sorted([*SIMULATOR.glob('Runtime/Core/*.cs'), *SIMULATOR.glob('Runtime/Planning/*.cs')])
    tracked = [*sources, world, *coverage, Path(__file__)]
    hashes = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in tracked}
    probe = run / 'CompleteProviderInputProbe.cs'
    probe.write_text(PROBE)
    dll = next((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    shutil.copy2(dll, run / dll.name)
    facade = mono.parent.parent / 'lib/mono/4.5/Facades/netstandard.dll'
    exe = run / 'CompleteProviderInputProbe.exe'
    compiled = subprocess.run([str(mono), str(compiler), '-nologo', '-langversion:latest', '-main:CompleteProviderInputProbe', '-out:' + str(exe), '-r:' + str(dll), '-r:' + str(facade), '-r:System.Net.Http.dll', str(probe), *map(str, sources)], cwd=ROOT, capture_output=True, text=True)
    result = compiled if compiled.returncode else subprocess.run([str(mono), str(exe), str(world), *map(str, coverage), str(run)], cwd=ROOT, capture_output=True, text=True, timeout=60)
    measurements = json.loads(result.stdout) if result.returncode == 0 else []
    receipt = {'schema': 'complete-provider-input-measurement-v1', 'passed': result.returncode == 0,
               'measurement_passed_is_not_budget_admission_passed': True,
               'exit_code': result.returncode, 'measurements': measurements, 'source_hashes': hashes,
               'source_changed_during_run': any(hashlib.sha256(p.read_bytes()).hexdigest() != hashes[str(p.relative_to(ROOT))] for p in tracked),
               'stderr': result.stderr, 'real_provider_requests': 0, 'real_credentials_read': False,
               'helper_restarted': False, 'unity_engine_started': False,
               'claim': 'Complete historical 5105 state prompt measurement; no actual tokenizer, billing, inference quality or current original runtime claim.'}
    text = json.dumps(receipt, ensure_ascii=False, indent=2) + '\n'
    (run / 'receipt.json').write_text(text)
    (output / 'receipt.json').write_text(text)
    print(json.dumps({'receipt': str((run / 'receipt.json').relative_to(ROOT)), 'passed': receipt['passed'], 'measurements': measurements, 'stderr': result.stderr}, ensure_ascii=False))
    return result.returncode


if __name__ == '__main__':
    raise SystemExit(main())
