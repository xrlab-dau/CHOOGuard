#!/usr/bin/env python3
"""Portable provider/material/cache/ledger contracts; no provider network or Unity engine."""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import os
import time
from run_checks import HERE, ROOT, toolchain


def main():
    mono, compiler = toolchain()
    output = ROOT / 'output/simulator/aaa-provider-material'
    output.mkdir(parents=True, exist_ok=True)
    run_output = output / 'runs' / (str(time.time_ns()) + '-' + str(os.getpid()))
    run_output.mkdir(parents=True)
    sources = sorted([*HERE.glob('Runtime/Core/*.cs'), *HERE.glob('Runtime/Planning/*.cs')])
    tests = [HERE / ('Tests/' + name + '.cs') for name in ['PlanningTests', 'SubscriptionTests', 'ProviderSelectionTests', 'NativeProviderControlTests', 'MaterialStoreTests']]
    tests += [HERE / 'model_service/InitialWorldNative.cs', HERE / 'model_service/InitialWorldNativeTests.cs', HERE / 'model_service/LocalChoiceEvaluation.cs']
    tracked = [*sources, *tests, Path(__file__)]
    hashes = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in tracked}
    probe = run_output / 'ProviderMaterialProbe.cs'
    probe.write_text('''using System; using ChooGuard.Seeded.Tests;
public static class ProviderMaterialProbe { public static int Main() { try {
PlanningTests.Run(); SubscriptionTests.Run(); ProviderSelectionTests.Run(); NativeProviderControlTests.Run(); MaterialStoreTests.Run(); InitialWorldNativeTests.Run();
Console.WriteLine("PASS_PROVIDER_MATERIAL_CACHE_BUDGET_LEDGER");return 0;
} catch(Exception e) { Console.Error.WriteLine(e.ToString());return 1; } } }''')
    dll = next((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    shutil.copy2(dll, run_output / dll.name)
    facade = mono.parent.parent / 'lib/mono/4.5/Facades/netstandard.dll'
    exe = run_output / 'ProviderMaterialProbe.exe'
    compile_result = subprocess.run([str(mono), str(compiler), '-nologo', '-langversion:latest', '-main:ProviderMaterialProbe', '-out:' + str(exe), '-r:' + str(dll), '-r:' + str(facade), '-r:System.Net.Http.dll', str(probe), *map(str, sources), *map(str, tests)], cwd=ROOT, capture_output=True, text=True)
    result = compile_result if compile_result.returncode else subprocess.run([str(mono), str(exe)], cwd=ROOT, capture_output=True, text=True, timeout=60)
    receipt = {'schema': 'aaa-provider-material-portable-v1', 'passed': result.returncode == 0, 'exit_code': result.returncode, 'source_hashes': hashes,
               'source_changed_during_run': any(hashlib.sha256(p.read_bytes()).hexdigest() != hashes[str(p.relative_to(ROOT))] for p in tracked),
               'real_provider_requests': 0, 'login_or_catalog_requests': 0, 'unity_engine_started': False,
               'active_helper_restarted': False, 'real_credentials_read': False,
               'local_tape_consumer_compiled': compile_result.returncode == 0,
               'stdout': result.stdout, 'stderr': result.stderr,
               'claim': 'Portable synthetic provider/material/cache/budget/ordered-record contracts; no real subscription or Unity gameplay proof.'}
    receipt_text = json.dumps(receipt, ensure_ascii=False, indent=2) + '\n'
    (run_output / 'receipt.json').write_text(receipt_text)
    (output / 'receipt.json').write_text(receipt_text)
    print(result.stdout, end='');print(result.stderr, end='')
    return result.returncode


if __name__ == '__main__':
    raise SystemExit(main())
