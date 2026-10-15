#!/usr/bin/env python3
"""Network-denied native Copilot handshake; never authenticates or sends a prompt."""
import asyncio
import hashlib
import json
from pathlib import Path
import shutil
import sys
import tempfile
import sidecar  # task-local SDK dependency path
from copilot_adapter import verify_sdk_pin


async def probe():
    if sys.platform!='darwin' or not Path('/usr/bin/sandbox-exec').is_file():
        raise RuntimeError('network_fence_unavailable')
    copilot=verify_sdk_pin()
    from copilot.client import RuntimeConnection
    from copilot._cli_version import get_runtime_platform
    binary=Path(__file__).with_name('.sdk-runtime')/'prebuilds'/get_runtime_platform()/'copilot-runtime'
    with tempfile.TemporaryDirectory(prefix='chooguard-copilot-probe-') as temporary:
        task=Path(temporary);config=task/'copilot-data';config.mkdir(mode=0o700)
        client=copilot.CopilotClient(connection=RuntimeConnection.for_stdio(path='/usr/bin/sandbox-exec',
            args=['-p','(version 1) (allow default) (deny network*)',str(binary.resolve())]),
            github_token=None,use_logged_in_user=False,mode='empty',base_directory=str(config),
            working_directory=str(task),env={'PATH':'/usr/bin:/bin:/opt/homebrew/bin',
                'XDG_CONFIG_HOME':str(task/'config'),'COPILOT_SKIP_CLI_DOWNLOAD':'1'},
            log_level='error',builtin_plugin_directories=[],enable_remote_sessions=False)
        try:
            await asyncio.wait_for(client.start(),20)
            ping=await asyncio.wait_for(client.ping('CHOOGuard local native handshake'),10)
            return {'native_handshake_passed':True,'ping_type':type(ping).__name__,
                'network_fence':'sandbox-exec deny network*','use_logged_in_user':False,
                'mode':'empty','home_environment_overridden':False,'actual_login_requests':0,
                'actual_model_requests':0,'provider_credentials_read':False,
                'runtime_sha256':hashlib.sha256(binary.read_bytes()).hexdigest()}
        finally:await client.stop()


def main():
    try:result=asyncio.run(probe())
    except Exception as e:
        result={'native_handshake_passed':False,'error_type':type(e).__name__,
                'actual_login_requests':0,'actual_model_requests':0,'network_fence':'sandbox-exec deny network*'}
    path=Path(__file__).with_name('copilot-native-probe.json');path.write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2));return 0 if result['native_handshake_passed'] else 2

if __name__=='__main__':sys.exit(main())
