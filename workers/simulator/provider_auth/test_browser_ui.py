#!/usr/bin/env python3
"""Real browser DOM test with isolated mock credentials; no actual user browser or provider calls."""
import hashlib
import json
import os
from pathlib import Path
import select
import subprocess
import sys
import tempfile
import time
import test_sidecar as fixture

ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).parent


def main():
    began=time.monotonic()
    package=ROOT/'.planning/2026-10-13-proposal-aaa/browser-qa/node_modules/playwright'
    candidates=sorted((Path.home()/'Library/Caches/ms-playwright').glob('chromium_headless_shell-*/chrome-headless-shell-mac-arm64/chrome-headless-shell'))
    if not package.exists() or not candidates:
        print('Existing local Playwright/browser required; no dependency will be downloaded.')
        return 2
    paths=[Path(__file__),HERE/'browser_ui_probe.js',HERE/'browser_ui.py',HERE/'sidecar.py']
    hashes={str(path.relative_to(ROOT)):hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
    test=fixture.HttpTests('test_http_page_plain_status_csp_and_valid_script');test.setUp()
    child=None
    try:
        test.login()
        config=Path(test.temp.name)/'browser-probe.json'
        config.write_text(json.dumps({'browser_url':test.p.endpoint+'/#'+test.p.bootstrap,'executable':str(candidates[-1])}))
        os.chmod(config,0o600)
        child=subprocess.Popen(['node',str(HERE/'browser_ui_probe.js'),str(package),str(config)],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,cwd=ROOT)
        for expected in ('READY_FOR_RESTART','READY_FOR_STALE'):
            if not select.select([child.stdout],[],[],30)[0]:raise TimeoutError('Bounded browser probe timeout')
            line=child.stdout.readline().strip()
            if line!=expected:
                stdout,stderr=child.communicate(timeout=5)
                raise RuntimeError('Browser probe did not reach checkpoint: '+stderr[-500:])
            if expected=='READY_FOR_RESTART':
                current=test.p.status()
                if not(current['cloud_enabled'] and current['model']=='second' and current['initial_world']['remaining']==1):raise RuntimeError('Browser actions not saved on server')
                replacement=fixture.s.Provider(test.store,test.transport)
                test.server.provider=replacement;test.p=replacement;replacement.configure(test.server.server_port)
            else:
                test.p.ui_cookie='synthetic-invalid-cookie-for-legacy-regression'
            child.stdin.write('continue\n');child.stdin.flush()
        stdout,stderr=child.communicate(timeout=15)
        result=json.loads(stdout.strip())
        report={**result,'exit_code':child.returncode,'elapsed_seconds':round(time.monotonic()-began,3),
                'source_hashes':hashes,'source_changed_during_run':any(hashlib.sha256(path.read_bytes()).hexdigest()!=hashes[str(path.relative_to(ROOT))] for path in paths),
                'live_provider_login':False,'live_provider_inference':False,'existing_cli_credentials_read':False,
                'mock_provider_inference_attempts':test.p.usage['requests'],'stderr':stderr}
        (HERE/'browser-test-report.json').write_text(json.dumps(report,indent=2)+'\n')
        print(json.dumps({key:report[key] for key in ('passed','exit_code','actual_headless_browser','stable_cookie_across_restart','stale_connection_explicit_error','mock_provider_inference_attempts')}))
        return 0 if report['passed'] and child.returncode==0 else 1
    except Exception:
        # Environment/library diagnostics can contain URLs; retain only a safe failure classification.
        report={'passed':False,'exit_code':2,'error':'browser_environment_or_probe_unavailable',
                'actual_headless_browser_completed':False,'elapsed_seconds':round(time.monotonic()-began,3),
                'source_hashes':hashes,'live_provider_login':False,'live_provider_inference':False,
                'existing_cli_credentials_read':False,'mock_provider_inference_attempts':test.p.usage['requests']}
        (HERE/'browser-test-report.json').write_text(json.dumps(report,indent=2)+'\n')
        print(json.dumps({key:report[key] for key in ('passed','exit_code','error','actual_headless_browser_completed')}))
        return 2
    finally:
        if child and child.poll() is None:child.kill();child.communicate(timeout=5)
        test.tearDown()


if __name__=='__main__':sys.exit(main())
