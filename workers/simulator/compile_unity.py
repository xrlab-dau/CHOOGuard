#!/usr/bin/env python3
"""Compile isolated runtime + editor sources against installed Unity assemblies.

This is a source/API check. It never starts Unity or proves physics/navigation.
"""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys
from run_checks import ROOT, HERE, toolchain


def main():
    mono, compiler = toolchain()
    scripting = mono.parent.parent.parent
    # Use modular editor assemblies, not the legacy consolidated UnityEngine.dll
    # which declares the same types and causes duplicate-definition errors.
    refs = list((scripting / 'Managed/UnityEngine').glob('UnityEngine.*.dll'))
    refs += list((scripting / 'Managed/UnityEngine').glob('UnityEditor.*.dll'))
    refs += list((ROOT / 'Library/ScriptAssemblies').glob('Unity.*.dll'))
    # uGUI uses a UnityEngine.* assembly name, outside the Unity.* package glob.
    ui_reference = ROOT / 'Library/ScriptAssemblies/UnityEngine.UI.dll'
    if ui_reference.is_file():
        refs.append(ui_reference)
    refs += list((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    refs += [mono.parent.parent / 'lib/mono/4.5/Facades/netstandard.dll']
    refs = sorted(set(refs))
    sources = sorted([*HERE.glob('Runtime/**/*.cs'), *HERE.glob('Editor/*.cs')])
    output = ROOT / 'output/simulator/checks'
    output.mkdir(parents=True, exist_ok=True)
    dll = output / ('UnitySourceCheck-' + str(os.getpid()) + '.dll')
    hashes = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}
    command = [str(mono), str(compiler), '-nologo', '-langversion:latest', '-define:UNITY_EDITOR', '-target:library', '-out:' + str(dll), '-r:System.Net.Http.dll']
    command += ['-r:' + str(ref) for ref in refs] + [str(p) for p in sources]
    result = subprocess.run(command, cwd=ROOT, capture_output=True, text=True)
    print(result.stdout, end='')
    print(result.stderr, end='', file=sys.stderr)
    record = {'exit_code': result.returncode, 'sources': hashes, 'reference_count': len(refs) + 1, 'artifact': str(dll), 'stdout': result.stdout, 'stderr': result.stderr,
              'source_changed_during_compile': any(hashlib.sha256(p.read_bytes()).hexdigest() != hashes[str(p.relative_to(ROOT))] for p in sources),
              'scope': 'UNITY_EDITOR source/API compilation only; no Unity process, GPU, NavMesh or scene execution.'}
    (output / 'unity-source-compile.json').write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps({'compile': 'PASS' if result.returncode == 0 else 'FAIL', 'sources': len(sources), 'references': len(refs) + 1, 'scope': record['scope']}))
    return result.returncode


if __name__ == '__main__':
    sys.exit(main())
