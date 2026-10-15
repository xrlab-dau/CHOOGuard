#!/usr/bin/env python3
"""Run a real local-model evaluation through the TypeSafe-shaped C# contract; no TypeSafe API call."""
from pathlib import Path
import importlib.util
import os
import subprocess
ROOT=Path(__file__).resolve().parents[3]
HERE=ROOT/'workers/simulator'; OUT=ROOT/'output/simulator/model-benchmark'; BUILD=OUT/'build'
spec=importlib.util.spec_from_file_location('checks',HERE/'run_checks.py');checks=importlib.util.module_from_spec(spec);spec.loader.exec_module(checks)
mono,compiler=checks.toolchain();exe=BUILD/'LocalChoiceEvaluation.exe'
subprocess.run([str(mono),str(compiler),'-nologo','-langversion:latest','-out:'+str(exe),'-r:'+str(BUILD/'LocalModelBenchmark.exe'),'-r:'+str(BUILD/'Newtonsoft.Json.dll'),'-r:'+str(mono.parent.parent/'lib/mono/4.5/Facades/netstandard.dll'),str(HERE/'model_service/LocalChoiceEvaluation.cs')],check=True,cwd=ROOT)
env=dict(os.environ);env['NO_PROXY']=env['no_proxy']='127.0.0.1,localhost'
raise SystemExit(subprocess.run([str(mono),str(exe),str(OUT)],cwd=ROOT,env=env).returncode)
