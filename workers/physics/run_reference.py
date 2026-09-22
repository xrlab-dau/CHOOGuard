#!/usr/bin/env python3
"""Run the fixed FDS deck locally, preserving stdout/stderr and hashes."""
import datetime,hashlib,json,os,pathlib,subprocess,sys
HERE=pathlib.Path(__file__).resolve().parent
ROOT=HERE.parent.parent
CASE=HERE/'cases/reference-hall'
BINARY=ROOT/'.tools/physics/fds/FDS-6.11.1_SMV-6.11.2_osx/bin/fds_openmp'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def receipt(exit_code):
    completed=exit_code==0 and 'FDS completed successfully' in (CASE/'solver.stderr.log').read_text()
    value={'caseId':'reference-hall-30x20-v1','recordedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'solver':'FDS','solverVersion':'6.11.1','solverRevision':'FDS-6.11.1-0-gff928db-release','solverExitCode':exit_code,'completed':completed,'platform':'macOS arm64 host; official x86_64 solver under Rosetta','binarySha256':sha(BINARY),'installerSha256':sha(ROOT/'.tools/physics/fds-installer.sh'),'source':'https://github.com/firemodels/fds/releases/download/FDS-6.11.1/FDS-6.11.1_SMV-6.11.2_osx.sh','command':['/usr/bin/arch','-x86_64',str(BINARY),'hall.fds'],'environment':{'OMP_NUM_THREADS':'2'},'deckSha256':sha(CASE/'hall.fds'),'outputSha256':{p.name:sha(p) for p in sorted(CASE.iterdir()) if p.suffix in ('.sf','.smv','.out','.csv')},'simulationIntervalSeconds':[0,120],'gridMeters':[.5,.5,.5],'cells':[60,40,8],'sliceHeightMeters':1.5,'scope':'Uncalibrated reference hall; no mesh/time convergence or Busan validation'}
    (CASE/'receipt.json').write_text(json.dumps(value,indent=2)+'\n')
    return value
if __name__=='__main__':
    if '--receipt-existing-success' in sys.argv:
        if 'FDS completed successfully' not in (CASE/'solver.stderr.log').read_text(): raise SystemExit('No successful solver log')
        result=receipt(0)
    else:
        if not BINARY.exists(): raise SystemExit('Scoped FDS binary unavailable')
        env=dict(os.environ,OMP_NUM_THREADS='2')
        with (CASE/'solver.stdout.log').open('w') as out,(CASE/'solver.stderr.log').open('w') as err:
            code=subprocess.run(['/usr/bin/arch','-x86_64',str(BINARY),'hall.fds'],cwd=CASE,env=env,stdout=out,stderr=err).returncode
        result=receipt(code)
    print(json.dumps({'completed':result['completed'],'solverExitCode':result['solverExitCode']}))
