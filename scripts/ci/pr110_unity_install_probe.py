"""Bounded official Unity installer/startup probe. Never uses account credentials.
This does not run CHOOGuard tests; a fresh unlicensed host is reported as blocked.
"""
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tarfile
import time
import urllib.request

VERSION = '6000.3.23f1'
INSTALLER = 'https://download.unity3d.com/download_unity/09d2ecc7fb28/LinuxEditorInstaller/Unity-6000.3.23f1.tar.xz'
API = 'https://services.api.unity.com/unity/editor/release/v1/releases?limit=1&version=6000.3.23f1'
ROOT = Path(os.environ['RUNNER_TEMP']) / 'chooguard-unity-probe'
EVIDENCE = Path(os.environ['GITHUB_WORKSPACE']) / 'unity-install-probe-evidence'
MAX_BYTES = 6 * 1024**3


def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    receipt = {
        'schemaVersion': 1,
        'scope': 'official_editor_install_and_empty_project_startup_only',
        'requestedVersion': VERSION,
        'sourceSha': os.environ['CANDIDATE_SHA'],
        'runId': os.environ['GITHUB_RUN_ID'],
        'runAttempt': os.environ['GITHUB_RUN_ATTEMPT'],
        'startedAtUtc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
        'installerUrl': INSTALLER,
        'installation': 'NOT_RUN',
        'versionProbe': 'NOT_RUN',
        'emptyProjectStartup': 'NOT_RUN',
        'chooguardCompilation': 'NOT_RUN',
        'unityEditMode': 'NOT_RUN',
        'unityPlayMode': 'NOT_RUN',
        'windowsBuild': 'NOT_RUN',
        'licenseCredentialsUsed': False,
        'aaa': 'BLOCKED',
    }
    try:
        with urllib.request.urlopen(API, timeout=30) as response:
            raw = response.read(4 * 1024**2 + 1)
        if len(raw) > 4 * 1024**2:
            raise ValueError('release metadata exceeds size bound')
        metadata = json.loads(raw)
        releases = [r for r in metadata['results'] if r['version'] == VERSION]
        if len(releases) != 1:
            raise ValueError('exact release not found uniquely')
        matches = [d for d in releases[0]['downloads'] if d['url'] == INSTALLER]
        if len(matches) != 1:
            raise ValueError('pinned Linux installer missing from official release metadata')
        download = matches[0]
        integrity = download.get('integrity', '')
        match = re.fullmatch(r'(sha256|sha384|sha512|md5)-([A-Za-z0-9+/=]+)', integrity)
        if not match:
            raise ValueError('official integrity missing or unsupported; refusing extraction')
        algorithm, encoded = match.groups()
        expected = base64.b64decode(encoded, validate=True)
        official = hashlib.new(algorithm)
        if len(expected) != official.digest_size:
            raise ValueError('invalid official digest length')
        receipt['officialIntegrity'] = integrity
        receipt['officialIntegrityAlgorithm'] = algorithm
        receipt['officialIntegritySecurityNote'] = 'Publisher digest over HTTPS; MD5, if provided, is a corruption check, not a collision-resistant signature.'
        free = shutil.disk_usage(ROOT).free
        receipt['freeBytesBefore'] = free
        size = download.get('downloadSize', {})
        installed = download.get('installedSize', {})
        required = 13 * 1024**3
        if size.get('unit') == 'BYTE' and installed.get('unit') == 'BYTE':
            required = int(size['value']) + int(installed['value']) + 512 * 1024**2
        if free < required:
            raise ValueError('insufficient runner disk for bounded installer and extraction')
        archive = ROOT / 'editor.tar.xz'
        sha256 = hashlib.sha256()
        downloaded = 0
        with urllib.request.urlopen(INSTALLER, timeout=60) as response, archive.open('xb') as output:
            if not response.geturl().startswith('https://download.unity3d.com/'):
                raise ValueError('unexpected installer redirect host')
            while True:
                chunk = response.read(4 * 1024**2)
                if not chunk:
                    break
                downloaded += len(chunk)
                if downloaded > MAX_BYTES:
                    raise ValueError('installer exceeds download bound')
                official.update(chunk)
                sha256.update(chunk)
                output.write(chunk)
        receipt['installerBytes'] = downloaded
        receipt['installerSha256'] = sha256.hexdigest()
        if official.digest() != expected:
            raise ValueError('official installer digest mismatch')
        if size.get('unit') == 'BYTE' and int(size['value']) != downloaded:
            raise ValueError('official download size mismatch')
        receipt['officialDigestMatched'] = True
        target = ROOT / 'editor'
        target.mkdir()
        # The data filter refuses escaping paths, unsafe symlinks and special devices.
        with tarfile.open(archive, 'r|xz') as bundle:
            bundle.extractall(target, filter='data')
        archive.unlink()
        editor = target / 'Editor' / 'Unity'
        if not editor.is_file():
            raise ValueError('expected Editor/Unity executable absent')
        receipt['installation'] = 'EXTRACTED_AND_PUBLISHER_DIGEST_VERIFIED'
        version = subprocess.run([str(editor), '-version'], capture_output=True, text=True, timeout=60)
        receipt['versionExitCode'] = version.returncode
        receipt['versionReported'] = VERSION if VERSION in version.stdout + version.stderr else None
        if version.returncode != 0 or receipt['versionReported'] is None:
            receipt['versionProbe'] = 'FAILED'
            raise ValueError('version probe did not report the exact requested Editor')
        receipt['versionProbe'] = 'PASS_BINARY_VERSION_ONLY'
        log = ROOT / 'startup.log'
        command = [str(editor), '-batchmode', '-nographics', '-quit', '-createProject', str(ROOT / 'empty-project'), '-logFile', str(log)]
        receipt['startupArguments'] = ['-batchmode', '-nographics', '-quit', '-createProject', '<isolated-empty-project>', '-logFile', '<unpublished-log>']
        try:
            startup = subprocess.run(command, capture_output=True, text=True, timeout=180)
            receipt['startupExitCode'] = startup.returncode
            output = startup.stdout + startup.stderr
        except subprocess.TimeoutExpired:
            receipt['emptyProjectStartup'] = 'TIMEOUT'
            raise ValueError('empty project startup exceeded 180-second bound')
        if log.exists():
            output += log.read_text(encoding='utf-8', errors='replace')
        # Never upload raw Editor/licensing output, machine identifiers, or license files.
        receipt['licenseFailureMarkerFound'] = bool(re.search(r'no valid (?:unity )?license|failed to (?:activate|update.*license)|license.*(?:not found|not available)|no.*(?:entitlement|license).*found', output, re.I))
        receipt['csharpErrorMarkerFound'] = bool(re.search(r'error CS\d+', output))
        if startup.returncode == 0:
            receipt['emptyProjectStartup'] = 'EMPTY_PROJECT_ONLY_EXIT_ZERO'
        elif receipt['licenseFailureMarkerFound']:
            receipt['emptyProjectStartup'] = 'BLOCKED_LICENSE'
        else:
            receipt['emptyProjectStartup'] = 'FAILED_UNCLASSIFIED'
        receipt['result'] = 'INSTALLATION_PROBED_PROJECT_VERIFICATION_STILL_BLOCKED'
        return 0 if startup.returncode == 0 else 1
    except Exception as error:
        receipt['result'] = 'BLOCKED'
        receipt['errorType'] = type(error).__name__
        receipt['error'] = str(error).replace(str(ROOT), '<probe-root>')[:800]
        return 1
    finally:
        receipt['finishedAtUtc'] = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
        (EVIDENCE / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
        print(json.dumps(receipt, indent=2))


if __name__ == '__main__':
    sys.exit(main())
