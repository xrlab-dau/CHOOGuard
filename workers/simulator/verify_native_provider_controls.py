#!/usr/bin/env python3
"""C# native menu contracts against an ephemeral mock OAuth helper; no Unity or real provider."""
from pathlib import Path
import hashlib
import json
import os
import select
import shutil
import subprocess
import sys
from run_checks import ROOT, HERE, toolchain

sys.path.insert(0, str(HERE / 'provider_auth'))
import test_native_control as fixture

PROBE = r'''
using System;
using System.IO;
using System.Linq;
using System.Threading;
using ChooGuard.Seeded;
using ChooGuard.Seeded.Tests;
public static class NativeMenuProbe {
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 static NativeProviderControlReply Await(System.Threading.Tasks.Task<NativeProviderControlReply> task){return task.GetAwaiter().GetResult();}
 static int conflicts;
 static NativeProviderControlReply Completed(NativeProviderControlClient client,string flow,CancellationToken ct){
  try{return Await(client.PollLoginAsync(flow,ct));}
  catch(NativeProviderControlException e){if(e.Code!="provider_session_changed")throw;conflicts++;Await(client.RefreshAsync(ct));return Await(client.PollLoginAsync(flow,ct));}
 }
 public static int Main(string[] args){try{
  NativeProviderControlTests.Run();
  using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(60))){
   var ct=deadline.Token;var connection=SubscriptionConnection.ParseDescriptor(File.ReadAllText(args[0]));bool editable=true;
   var client=connection.CreateControlClient(()=>editable);var initial=Await(client.RefreshAsync(ct));
   Check(initial.State.Accounts.Count==0&&initial.State.Models.Count==0&&initial.State.Subscription.Selection==null,"Fresh native state without any account or selection");
   var first=Await(client.StartLoginAsync("openai-chatgpt",null,false,ct)).Flow;
   Check(first.Pending&&first.AuthorizationUrl.StartsWith("https://auth.openai.com/"),"External OAuth link only");
   Console.WriteLine("LOGIN_A_PENDING");Console.Out.Flush();Console.ReadLine();
   Check(Completed(client,first.FlowId,ct).Flow.Completed,"First login callback and stale-generation recovery");
   var second=Await(client.StartLoginAsync("mock-beta",null,false,ct)).Flow;
   Console.WriteLine("LOGIN_B_PENDING");Console.Out.Flush();Console.ReadLine();
   Check(Completed(client,second.FlowId,ct).Flow.Completed,"Second provider login completion");
   var catalog=Await(client.RefreshCatalogAsync(ct)).State;
   var twins=catalog.Models.Where(m=>m.ModelId=="second").ToArray();Check(twins.Length==2,"Union retains two providers with the same model ID");
   var a=twins.Single(m=>m.ProviderId=="openai-chatgpt");var b=twins.Single(m=>m.ProviderId=="mock-beta");
   var selectedA=Await(client.SelectModelAsync(a,ct)).State;Check(!selectedA.Subscription.CloudEnabled,"Selection never grants subscription consent");
   selectedA=Await(client.SetConsentAsync(true,null,null,ct)).State;var sessionA=connection.LockSelection(selectedA.Subscription);
   var selectedB=Await(client.SelectModelAsync(b,ct)).State;Check(!selectedB.Subscription.CloudEnabled&&!sessionA.IsCurrent,"Selecting another provider invalidates the previous locked model");
   selectedB=Await(client.SetConsentAsync(true,null,null,ct)).State;var sessionB=connection.LockSelection(selectedB.Subscription);
   Check(sessionA.Selection.ModelId==sessionB.Selection.ModelId&&sessionA.Selection.CacheVersion!=sessionB.Selection.CacheVersion,"Provider/account policy identity remains distinct");
   Check(!selectedB.InitialAllowed,"Regular consent does not grant initial-world generation");
   var armed=Await(client.SetConsentAsync(true,1000,128,ct)).State;Check(armed.InitialAllowed&&armed.Subscription.Requests==0,"Explicit one-shot permission does not infer");
   Check(Await(client.RefreshAsync(ct)).State.InitialAllowed&&Await(client.RefreshCatalogAsync(ct)).State.InitialAllowed,"Status and catalog refresh preserve one-shot allowance");
   editable=false;bool locked=false;try{Await(client.SelectModelAsync(a,ct));}catch(NativeProviderControlException e){locked=e.Code=="native_controls_locked_for_play";}Check(locked,"Playing blocks native selection mutation");editable=true;
   var pending=Await(client.StartLoginAsync("openai-chatgpt",null,false,ct)).Flow;
   Check(Await(client.CancelLoginAsync(pending.FlowId,ct)).Flow.Status=="cancelled","Native cancellation leaves no hidden pending login");
   var logout=Await(client.LogoutAsync(b.AccountId,ct));Check(logout.LocalSessionCleared==true,"Explicit account logout succeeds");
   Check(Await(client.ClearSelectionAsync(ct)).State.Subscription.Selection==null,"Selection can be cleared independently from remaining logins");
   Check(conflicts>=1,"The first browser callback exercised generation conflict recovery");
   Console.WriteLine("PASS_NATIVE_MENU_HTTP");return 0;
  }
 }catch(NativeProviderControlException e){Console.Error.WriteLine("NativeControl:"+e.Code);return 1;}
 catch(Exception e){Console.Error.WriteLine(e.GetType().Name+":"+e.Message);return 1;}}
}
'''


def main():
    mono, compiler = toolchain()
    output = ROOT / 'output/simulator/native-provider-controls'
    output.mkdir(parents=True, exist_ok=True)
    sources = sorted([*HERE.glob('Runtime/Core/*.cs'), *HERE.glob('Runtime/Planning/*.cs')])
    tests = HERE / 'Tests/NativeProviderControlTests.cs'
    tracked = [*sources, tests, Path(__file__), *sorted((HERE / 'provider_auth').glob('*.py'))]
    hashes = {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in tracked}
    probe = output / 'NativeMenuProbe.cs';probe.write_text(PROBE)
    exe = output / 'NativeMenuProbe.exe'
    json_dll = next((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    shutil.copy2(json_dll, output / json_dll.name)
    facade = mono.parent.parent / 'lib/mono/4.5/Facades/netstandard.dll'
    subprocess.run([str(mono), str(compiler), '-nologo', '-langversion:latest', '-out:' + str(exe), '-r:' + str(json_dll), '-r:' + str(facade), '-r:System.Net.Http.dll', str(probe), str(tests), *map(str, sources)], cwd=ROOT, check=True)
    test = fixture.NativeControlTests('test_status_is_cached_readonly_and_tokens_never_returned');test.setUp()
    child = None;routes = []
    try:
        original = test.server.native_control.dispatch

        def dispatch(method, path, body, generation):
            routes.append({'method': method, 'path': '/native/v1/auth/flows/{flow_id}' if path.startswith('/native/v1/auth/flows/') else path})
            return original(method, path, body, generation)

        test.server.native_control.dispatch = dispatch
        descriptor = Path(test.temp.name) / 'menu-connection.json'
        descriptor.write_text(json.dumps({'schema': 1, 'endpoint': test.p.endpoint, 'capability': test.p.capability, 'browser_url': test.p.endpoint + '/#' + test.p.bootstrap}));os.chmod(descriptor, 0o600)
        child = subprocess.Popen([str(mono), str(exe), str(descriptor)], cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        for marker, provider, transport in [('LOGIN_A_PENDING', 'openai-chatgpt', test.a), ('LOGIN_B_PENDING', 'mock-beta', test.b)]:
            if not select.select([child.stdout], [], [], 20)[0]:raise TimeoutError('Native control probe phase deadline')
            line = child.stdout.readline().strip()
            if line != marker:
                stdout, stderr = child.communicate(timeout=5)
                raise RuntimeError('Native menu phase failed: ' + stderr)
            flow = next(item for item in test.server.native_control.flows.values() if item['provider_id'] == provider and item['status'] not in ('completed', 'cancelled', 'failed', 'expired'))
            test.complete(test.server.native_control.public_flow(flow, start=True), transport=transport, client='OWN_NATIVE_' + provider)
            child.stdin.write('continue\n');child.stdin.flush()
        stdout, stderr = child.communicate(timeout=20)
        inference_calls = sum(call[1].endswith('/responses') for call in test.a.calls + test.b.calls + test.device.calls)
        source_drift = [str(path.relative_to(ROOT)) for path in tracked if hashlib.sha256(path.read_bytes()).hexdigest() != hashes[str(path.relative_to(ROOT))]]
        passed = child.returncode == 0 and 'PASS_NATIVE_MENU_HTTP' in stdout and inference_calls == 0 and not source_drift
        report = {'schema': 'chooguard-native-provider-controls-v1', 'passed': passed, 'exit_code': child.returncode,
            'source_hashes': hashes, 'source_changed_during_run': bool(source_drift), 'source_drift': source_drift,
            'portable_native_control_contracts_passed': child.returncode == 0, 'native_http_oauth_catalog_selection_consent_logout_passed': child.returncode == 0,
            'two_mock_providers_same_model_id': child.returncode == 0, 'callback_generation_conflict_recovered': child.returncode == 0,
            'playing_mutations_refused': child.returncode == 0, 'noncooperative_cancel_and_late200_refused': child.returncode == 0,
            'real_provider_requests': 0, 'model_inference_posts': inference_calls, 'real_credentials_read': False, 'active_helper_changed': False,
            'unity_engine_started': False, 'browser_windows_opened': 0, 'native_routes': routes,
            'stdout': stdout, 'stderr': stderr,
            'claim': 'Actual C# loopback HTTP with synthetic OAuth accounts. Product Unity rendering, focus/cursor behavior and real provider login remain unverified.'}
        (output / 'receipt.json').write_text(json.dumps(report, indent=2) + '\n')
        print(json.dumps({key: report[key] for key in ['passed', 'two_mock_providers_same_model_id', 'callback_generation_conflict_recovered', 'playing_mutations_refused', 'noncooperative_cancel_and_late200_refused', 'real_provider_requests', 'model_inference_posts', 'unity_engine_started']}))
        return 0 if passed else 1
    finally:
        if child is not None and child.poll() is None:child.kill();child.communicate(timeout=5)
        test.tearDown()


if __name__ == '__main__':
    sys.exit(main())
