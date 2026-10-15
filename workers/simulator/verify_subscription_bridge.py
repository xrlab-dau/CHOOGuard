#!/usr/bin/env python3
"""Exercise the actual C# HTTP adapter against a local mock OAuth/Responses sidecar."""
from pathlib import Path
import hashlib
import json
import os
import select
import shutil
import subprocess
import sys
import tempfile
import threading
import urllib.parse
from dataclasses import replace
from run_checks import ROOT, HERE, toolchain

sys.path.insert(0, str(HERE / 'provider_auth'))
import test_sidecar as fixture
from multi_provider import MultiProvider
from provider_registry import OPENAI, Registry

PROBE = r'''
using System;
using System.IO;
using System.Threading;
using ChooGuard.Seeded;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public static class NativeProbe {
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
 static void Pair(SubscriptionConnection.SubscriptionSession session,CancellationToken cancellation){
  var w=new World{Seed=42};var grid=new Entity{Id="grid",Kind="service-cabinet"};
  foreach(var tag in new[]{"semantic","electrical","thermal"})grid.Tags.Add(tag);
  grid.Set("power",1);grid.Set("wetness",.1);grid.Set("insulation",.8);grid.Set("temperature",20);w.Add(grid);
  var player=new Entity{Id="player",Kind="staff"};player.Tags.Add("npc");player.Tags.Add("staff");w.Add(player);
  var original=JsonConvert.DeserializeObject<World>(JsonConvert.SerializeObject(w));
  var composer=new EmergenceComposer(session.CreateEmergenceProvider());
  var record=composer.GenerateAndApplyAsync(w,false,cancellation:cancellation,authorized:true).GetAwaiter().GetResult();
  Check(record.Disposition=="accepted"&&w.Entities["grid"].Get("wetness")==.8,"C# selected initial-world HTTP request and atomic apply");
  string reason;Check(EmergenceComposer.ValidateAndApplyRecorded(original,record,false,out reason)&&original.Digest()==w.Digest(),"Recorded initial condition replay");
  var provider=session.CreateGenerator(128);
  var request=new PlanRequest{Id="native-probe",ActorId="player",Goal="isolate",ContextJson="{}"};
  request.Offers.Add(new OfferedAction{operation="isolate_power",actor_id="player",target_id="grid"});
  var reply=provider.Generate(request,cancellation).GetAwaiter().GetResult();
  Check(reply.Error==null&&reply.Steps.Count==1&&reply.InputTokens==8,"C# ordinary selected plan HTTP contract");
  var catalog=OperationCatalog.CreateDefault();var executor=new PlanExecutor(catalog);
  var plan=ActionPlan.Capture(w,"g",provider.Version,"v",OperationCatalog.Version,"n",reply.Steps);
  Check(executor.TryExecute(w,plan,0,"n",out reason)&&w.Entities["grid"].Get("power.isolated")==1,"HTTP plan executes supported intervention");
 }
 public static int Main(string[] args){try {
  using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30))) {
  var cancellation=deadline.Token;
  var connection=SubscriptionConnection.ParseDescriptor(File.ReadAllText(args[0]));
  var status=connection.Refresh(cancellation).GetAwaiter().GetResult();Check(status.Ready,"HTTP status contract");
  var first=connection.LockSelection(status);Pair(first,cancellation);
  Console.WriteLine("READY_FOR_SESSION_SWITCH");Console.Out.Flush();Console.ReadLine();
  var request=new PlanRequest{Id="stale",ActorId="player",Goal="isolate",ContextJson="{}"};
  var reply=first.CreateGenerator().Generate(request,cancellation).GetAwaiter().GetResult();
  Check(reply.Error=="generator_http_409","Actual HTTP stale-session call refused");
  Check(!first.IsCurrent,"HTTP409 invalidates the native locked selection");
  var staleInitial=first.CreateEmergenceProvider().Generate(new EmergenceRequest{Seed=42,Json="{}"},cancellation).GetAwaiter().GetResult();
  Check(staleInitial.Error.StartsWith("generator_http_409"),"Actual HTTP stale initial-session call refused");
  status=connection.Refresh(cancellation).GetAwaiter().GetResult();Check(status.Ready,"Second provider status ready");var second=connection.LockSelection(status);
  Check(first.Selection.ProviderId!=second.Selection.ProviderId&&first.Selection.ModelId==second.Selection.ModelId&&first.Selection.CacheVersion!=second.Selection.CacheVersion,"Two providers same model ID cannot share selection or cache");
  Pair(second,cancellation);
  Console.WriteLine("PASS_NATIVE_HTTP_BRIDGE");return 0;}
 }catch(Exception error){Console.Error.WriteLine(error.GetType().Name+":"+error.Message);return 1;}}
}
'''


def main():
    mono, compiler = toolchain()
    out = ROOT / 'output/simulator/native-auth-bridge'
    out.mkdir(parents=True, exist_ok=True)
    sources = sorted([*HERE.glob('Runtime/Core/*.cs'), *HERE.glob('Runtime/Planning/*.cs')])
    tracked = [*sources, Path(__file__), *sorted((HERE / 'provider_auth').glob('*.py'))]
    hashes = {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in tracked}
    probe = out / 'NativeProbe.cs'
    probe.write_text(PROBE)
    exe = out / 'NativeProbe.exe'
    json_dll = sorted((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))[0]
    shutil.copy2(json_dll, out / json_dll.name)
    facade = mono.parent.parent / 'lib/mono/4.5/Facades/netstandard.dll'
    subprocess.run([str(mono), str(compiler), '-nologo', '-langversion:latest', '-out:' + str(exe), '-r:' + str(json_dll), '-r:' + str(facade), '-r:System.Net.Http.dll', str(probe), *map(str, sources)], check=True, cwd=ROOT)
    temporary = tempfile.TemporaryDirectory(prefix='chooguard-multi-native-mock-')
    store = fixture.s.PrivateStore(Path(temporary.name) / 'private')
    transports = {'openai-chatgpt': fixture.MockTransport(), 'mock-beta': fixture.MockTransport()}
    registry = Registry([OPENAI, replace(OPENAI, id='mock-beta', label='Mock Beta')])
    provider = MultiProvider(store, transports=transports, registry=registry)
    server = fixture.s.Server(('127.0.0.1', 0), provider)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    child = None
    try:
        accounts = {}
        for provider_id, transport in transports.items():
            start = provider.auth_start(provider_id=provider_id)
            query = {key: value[0] for key, value in urllib.parse.parse_qs(urllib.parse.urlsplit(start['authorization_url']).query).items()}
            transport.codes['native-login'] = transport.token('oaiapp_native_mock', query['nonce'])
            provider.callback({'state': query['state'], 'code': 'native-login', 'client_id': 'oaiapp_native_mock'})
            accounts[provider_id] = next(iter(provider.adapters[provider_id].store.data['accounts']))
            original_request = transport.request

            def model_reply(method, url, headers=None, body=None, transport=transport, original_request=original_request):
                upstream = json.loads(body)
                data = json.loads(next(item['content'] for item in upstream['input'] if item['role'] == 'user'))
                if data.get('contract') == 'initial-causal-conditions-v1':
                    content = json.dumps({'seed': data['seed'], 'basis_digest': data['basis_digest'], 'changes': [{'entity_id': 'grid', 'property': 'wetness', 'value': .8}]})
                else:
                    content = json.dumps({'steps': [{'operation': 'isolate_power', 'actor_id': 'player', 'target_id': 'grid'}]})
                transport.stream = fixture.events([{'type': 'response.completed', 'response': {'id': 'mock-native', 'status': 'completed', 'model': upstream['model'], 'output': [{'type': 'message', 'content': [{'type': 'output_text', 'text': content}]}], 'usage': {'input_tokens': 8, 'output_tokens': 4}}}])
                return original_request(method, url, headers, body)

            transport.request = model_reply
        catalog = provider.models()['models']
        if len([row for row in catalog if row['model_id'] == 'second']) != 2:
            raise AssertionError('Both logged-in providers must retain their identically named authorized model')

        def choose(provider_id):
            row = next(row for row in catalog if row['provider_id'] == provider_id and row['model_id'] == 'second')
            return provider.session({'provider_id': provider_id, 'account_id': accounts[provider_id], 'model': 'second', 'catalog_version': row['catalog_version'],
                'cloud_enabled': True, 'initial_world_allowance': {'input_tokens': 192000, 'output_tokens': 2048}})

        choose('openai-chatgpt')
        routed = []
        original_chat = provider.chat

        def trace_chat(body, generation):
            binding = body.get('chooguard_selection')
            if not isinstance(binding, dict):
                raise AssertionError('New C# requests require exact selection binding')
            routed.append({'selection': dict(binding), 'purpose': body.get('chooguard_purpose', 'npc_plan')})
            return original_chat(body, generation)

        provider.chat = trace_chat
        descriptor = Path(temporary.name) / 'native-connection.json'
        descriptor.write_text(json.dumps({'schema': 1, 'endpoint': provider.endpoint, 'capability': provider.capability, 'browser_url': provider.endpoint + '/#' + provider.bootstrap}))
        os.chmod(descriptor, 0o600)
        child = subprocess.Popen([str(mono), str(exe), str(descriptor)], cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        # communicate has an owner timeout; the probe's first two local calls also have a deadline.
        if not select.select([child.stdout], [], [], 35)[0]:
            raise TimeoutError('Bounded native bridge probe exceeded its owner deadline')
        ready = child.stdout.readline().strip()
        if ready != 'READY_FOR_SESSION_SWITCH':
            stdout, stderr = child.communicate(timeout=10)
            raise RuntimeError('Native bridge did not reach its session guard: ' + stderr)
        before = sum(sum(call[1].endswith('/responses') for call in transport.calls) for transport in transports.values())
        choose('mock-beta')
        stdout, stderr = child.communicate('continue\n', timeout=20)
        counts = {id: sum(call[1].endswith('/responses') for call in transport.calls) for id, transport in transports.items()}
        after = sum(counts.values())
        same_pair = len(routed) == 5 and routed[0]['selection'] == routed[1]['selection'] and routed[-2]['selection'] == routed[-1]['selection']
        exact_routing = same_pair and routed[0]['selection']['provider_id'] == 'openai-chatgpt' and routed[-2]['selection']['provider_id'] == 'mock-beta'
        stale_zero_extra = before == 2 and counts == {'openai-chatgpt': 2, 'mock-beta': 2} and routed[2]['selection'] == routed[0]['selection']
        passed = child.returncode == 0 and 'PASS_NATIVE_HTTP_BRIDGE' in stdout and after == 4 and exact_routing and stale_zero_extra
        report = {'exit_code': child.returncode, 'passed': passed, 'mock_completed_inferences': after, 'stale_session_prevented_extra_call': stale_zero_extra,
                  'providers_tested': 2, 'selection_routing_passed': exact_routing, 'initial_and_npc_same_selection': same_pair,
                  'mock_inferences_by_provider': counts, 'same_model_id_across_providers': True,
                  'real_provider_requests': 0, 'provider_requests_mock_only': True, 'unity_engine_started': False,
                  'source_hashes': hashes, 'source_changed_during_run': any(hashlib.sha256(path.read_bytes()).hexdigest() != hashes[str(path.relative_to(ROOT))] for path in tracked),
                  'live_provider_login': False, 'live_provider_inference': False, 'stdout': stdout, 'stderr': stderr,
                  'claim': 'Actual C# HttpClient -> localhost authenticated sidecar -> synthetic OAuth/Responses transport. No real credentials, provider requests or subscription usage.'}
        (ROOT / 'output/simulator/native-auth-bridge.json').write_text(json.dumps(report, indent=2) + '\n')
        print(json.dumps({'passed': passed, 'mock_completed_inferences': after, 'providers_tested': 2, 'selection_routing_passed': exact_routing, 'stale_session_prevented_extra_call': stale_zero_extra}))
        return 0 if passed else 1
    finally:
        if child is not None and child.poll() is None:
            child.kill()
            child.communicate(timeout=5)
        server.shutdown();server.server_close();thread.join();store.close();temporary.cleanup()


if __name__ == '__main__':
    sys.exit(main())
