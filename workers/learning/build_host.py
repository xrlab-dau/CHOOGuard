"""Compile the research host with Unity's Mono compiler; no dotnet SDK required."""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
UNITY_MONO = Path("/Applications/Unity/Unity-6000.3.23f1/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge")
SOURCES = [
    "Assets/ChooGuard/Contracts/ContentTypes.cs",
    "Assets/ChooGuard/Contracts/OperationMessages.cs",
    "Assets/ChooGuard/Contracts/OperationsPorts.cs",
    "Assets/ChooGuard/Contracts/RuleTypes.cs",
    "Assets/ChooGuard/Contracts/Gameplay/GameplayContracts.cs",
    "Assets/ChooGuard/Domain/RunState.cs",
    "Assets/ChooGuard/Domain/AuthorityPolicy.cs",
    "Assets/ChooGuard/Domain/ReservationPlanner.cs",
    "Assets/ChooGuard/Domain/SupportRequest.cs",
    "Assets/ChooGuard/Domain/Gameplay/ActionRules.cs",
    "Assets/ChooGuard/Domain/Gameplay/TransitionKernel.cs",
    "Assets/ChooGuard/Application/OperationsSession.cs",
    "Assets/ChooGuard/Application/Gameplay/WorldSession.cs",
    "Assets/ChooGuard/Application/Gameplay/NpcPlanner.cs",
    "Assets/ChooGuard/Application/Gameplay/Content/GameplayContentLoader.cs",
    "Assets/ChooGuard/Application/Gameplay/Content/GameplayInitialWorld.cs",
    "Assets/ChooGuard/App/Fps/Runtime/GameplayCodec.cs",
    "Assets/ChooGuard/App/Fps/Runtime/GameplayInferenceClient.cs",
    "workers/learning/host/LearningHost.cs",
]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mono-root", type=Path, default=UNITY_MONO)
    parser.add_argument("--newtonsoft", type=Path)
    parser.add_argument("--output", type=Path, default=ROOT / "workers/learning/.build")
    parser.add_argument("--print-command", action="store_true")
    args = parser.parse_args()
    assemblies = sorted(ROOT.glob("Library/PackageCache/com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll"))
    assembly = args.newtonsoft or (assemblies[0] if len(assemblies) == 1 else None)
    if assembly is None:
        parser.error("Exactly one installed Unity Newtonsoft assembly required, or pass --newtonsoft")
    command = [str(args.mono_root / "bin/mono"), str(args.mono_root / "lib/mono/4.5/csc.exe"),
               "-nologo", "-langversion:9.0", "-target:exe",
               "-main:ChooGuard.Learning.LearningHost", f"-out:{args.output / 'LearningHost.exe'}",
               f"-r:{assembly}", "-r:System.Net.Http.dll",
               f"-r:{args.mono_root / 'lib/mono/4.5/Facades/netstandard.dll'}"] + [str(ROOT / source) for source in SOURCES]
    if args.print_command:
        print(json.dumps(command, ensure_ascii=False, indent=2))
        return
    args.output.mkdir(parents=True, exist_ok=True)
    subprocess.run(command, check=True, cwd=ROOT)
    shutil.copy2(assembly, args.output / "Newtonsoft.Json.dll")
    receipt = {
        "compiler_command": command,
        "source_sha256": {source: hashlib.sha256((ROOT / source).read_bytes()).hexdigest() for source in SOURCES},
        "newtonsoft_sha256": hashlib.sha256(assembly.read_bytes()).hexdigest(),
        "host_sha256": hashlib.sha256((args.output / "LearningHost.exe").read_bytes()).hexdigest(),
        "runtime_command": [str(args.mono_root / "bin/mono"), str(args.output / "LearningHost.exe")],
    }
    (args.output / "build-receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
