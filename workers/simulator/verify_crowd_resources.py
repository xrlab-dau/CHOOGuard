#!/usr/bin/env python3
"""Synthetic all-individual CPU/memory probe; never a Unity/GPU FPS test.

Fresh cases: 1k/10k/25k; seed123, dt.5, two metre synthetic grid,
one full kernel/community tick, full digest/position trace. Each case has a
60 second deadline and 512 MiB observed RSS envelope; violations remain FAIL.
"""
from pathlib import Path
import hashlib
import json
import selectors
import shutil
import subprocess
import sys
import time
from run_checks import ROOT, HERE, toolchain

SOURCE = r'''
using System;using System.Diagnostics;using System.Linq;using ChooGuard.Seeded;using Newtonsoft.Json;using Newtonsoft.Json.Linq;
class CrowdResourceProbe {
static int size;static void Stage(string name,double ms) {Console.WriteLine(new JObject{["type"]="stage",["npcs"]=size,["stage"]=name,["elapsed_ms"]=ms}.ToString(Formatting.None));Console.Out.Flush();}
static void Main(string[] args){size=int.Parse(args[0]);var sw=Stopwatch.StartNew();
var flow=CrowdFlowPlan.Generate(123,Array.Empty<CrowdArrivalSource>(),new[]{new CrowdTrainWave{Id="synthetic-wave",TrainEntityId="train",EntryEntityId="entry",DestinationEntityId="exit",AtSeconds=0,Capacity=size,OccupancyFraction=1,MinimumStaySeconds=1200,MaximumStaySeconds=1200}},size);Stage("flow_generation",sw.Elapsed.TotalMilliseconds);
var world=new World{Seed=123};world.Fields["environment.autonomous"]=0;world.Add(new Entity{Id="entry",Kind="place"});world.Add(new Entity{Id="exit",Kind="place",X=10});world.Add(new Entity{Id="train",Kind="train"});sw.Restart();flow.RegisterAll(world);
int i=0;foreach(var p in world.Entities.Values.Where(e=>e.Has("crowd-individual"))){p.Tags.Add("npc");p.Tags.Add("synthetic-active-benchmark-only");p.X=(i%320)*2;p.Z=(i/320)*2;i++;p.Set("health",1);p.Set("mobility",1);p.Set("mobility.base",1);p.Set("sight.radius",12);}Stage("register_all_and_explicit_synthetic_activation",sw.Elapsed.TotalMilliseconds);
Stage("kernel_started",0);sw.Restart();new CausalKernel().Step(world);Stage("kernel_one_fixed_tick",sw.Elapsed.TotalMilliseconds);
Stage("community_started",0);sw.Restart();new Community().Step(world);Stage("community_one_fixed_tick",sw.Elapsed.TotalMilliseconds);
sw.Restart();string before=world.Digest();Stage("full_world_digest",sw.Elapsed.TotalMilliseconds);sw.Restart();var detail=CrowdDetailSelection.Select(world,0,0,0,60,128);Stage("detail_selection",sw.Elapsed.TotalMilliseconds);bool unchanged=world.Digest()==before;
sw.Restart();string trace=LocalTapeCodec.Encode((JObject)LocalTapeCodec.FromObject(new{type="SyntheticAllIndividualPositions",individuals=world.Entities.Values.Where(e=>e.Has("npc")).Select(e=>new{id=e.Id,x=e.X,y=e.Y,z=e.Z})})).ToString(Formatting.None);Stage("all_individual_position_trace",sw.Elapsed.TotalMilliseconds);
Console.WriteLine(new JObject{["type"]="complete",["npcs"]=size,["all_active_individuals_preserved"]=world.Entities.Values.Count(e=>e.Has("npc"))==size,["LOD_state_unchanged"]=unchanged,["detailed_selection_count"]=detail.Count,["trace_utf8_bytes"]=System.Text.Encoding.UTF8.GetByteCount(trace),["managed_memory_bytes"]=GC.GetTotalMemory(false),["state_digest"]=world.Digest()}.ToString(Formatting.None));}
}'''


def main():
    output = ROOT / 'output/simulator/all-domains-core' / ('crowd-resource-' + str(time.time_ns()))
    output.mkdir(parents=True)
    mono, compiler = toolchain()
    probe = output / 'CrowdResourceProbe.cs'
    probe.write_text(SOURCE)
    sources = sorted([*HERE.glob('Runtime/Core/*.cs'), *HERE.glob('Runtime/Planning/*.cs'), probe])
    tracked = [*sources, Path(__file__)]
    hashes = {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in tracked}
    dll = next((ROOT / 'Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll'))
    shutil.copy2(dll, output / dll.name)
    exe = output / 'CrowdResourceProbe.exe'
    build = subprocess.run([str(mono), str(compiler), '-nologo', '-langversion:latest', '-out:' + str(exe), '-r:' + str(dll), '-r:System.Net.Http.dll', '-r:' + str(mono.parent.parent / 'lib/mono/4.5/Facades/netstandard.dll'), *map(str, sources)], capture_output=True, text=True)
    rows = []
    if build.returncode == 0:
        for count in [1000, 10000, 25000]:
            process = subprocess.Popen([str(mono), str(exe), str(count)], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1)
            began = time.monotonic(); peak = 0; events = []; violation = None
            selector = selectors.DefaultSelector(); selector.register(process.stdout, selectors.EVENT_READ)
            while process.poll() is None:
                for key, _ in selector.select(timeout=.2):
                    line = key.fileobj.readline()
                    if line:
                        try: event = json.loads(line)
                        except json.JSONDecodeError: event = {'type': 'diagnostic', 'text': line.strip()}
                        events.append(event); print(json.dumps(event), flush=True)
                rss = subprocess.run(['ps', '-o', 'rss=', '-p', str(process.pid)], capture_output=True, text=True).stdout.strip()
                if rss.isdigit(): peak = max(peak, int(rss) * 1024)
                if peak > 512 * 1024 * 1024: violation = 'RSS_ENVELOPE_EXCEEDED'
                elif time.monotonic() - began > 60: violation = 'DEADLINE_EXCEEDED'
                if violation:
                    process.kill(); break  # This exact child only; no shared/native processes.
            tail = process.communicate()[0]
            for line in tail.splitlines():
                try: events.append(json.loads(line))
                except json.JSONDecodeError: events.append({'type': 'diagnostic', 'text': line})
            selector.close()
            complete = next((e for e in events if e.get('type') == 'complete'), None)
            row = {'requested_N': count, 'exit_code': process.returncode, 'violation': violation, 'peak_RSS_bytes': peak, 'elapsed_seconds': time.monotonic() - began, 'events': events,
                   'functional_completed': violation is None and process.returncode == 0 and complete is not None and complete['all_active_individuals_preserved'] and complete['LOD_state_unchanged']}
            rows.append(row)
            (output / ('case-' + str(count) + '.json')).write_text(json.dumps(row, indent=2) + '\n')
    receipt = {'schema': 'all-individual-synthetic-resource-v2', 'cases': rows, 'compile_exit_code': build.returncode, 'compile_stderr': build.stderr, 'functional_completed_all': len(rows) == 3 and all(r['functional_completed'] for r in rows),
               'sources': hashes, 'source_drift': [str(p.relative_to(ROOT)) for p in tracked if hashlib.sha256(p.read_bytes()).hexdigest() != hashes[str(p.relative_to(ROOT))]],
               'seed': 123, 'step_seconds': .5, 'source_grid_spacing_m': 2, 'per_case_deadline_seconds': 60, 'per_case_RSS_envelope_bytes': 512 * 1024 * 1024,
               'physical_Nav_or_GPU_FPS_measured': False, 'actual_60FPS_1080p_verdict': 'UNVERIFIED', 'actual_Busan_occupancy_verdict': 'UNVERIFIED', 'Unity_executions': 0, 'real_model_requests': 0,
               'claim': 'Functional completion and CPU/RSS values only; no real crowd density, native movement/render, 60FPS or field-effectiveness PASS.'}
    (output / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps({'receipt': str(output / 'receipt.json'), 'functional_completed_all': receipt['functional_completed_all'], 'source_drift': receipt['source_drift']}), flush=True)
    return 0 if receipt['functional_completed_all'] and not receipt['source_drift'] else 1


if __name__ == '__main__':
    sys.exit(main())
