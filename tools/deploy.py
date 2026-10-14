"""Build, install and verify the Home/overlay pair. Never changes TV policy."""
from datetime import datetime, timezone
import json
import os
import shutil
import subprocess
import zipfile

from project import ROOT, sdb_path, tv_serial
if os.environ.get('FRAME_UNSIGNED') == '1':
    raise SystemExit('Deployment requires fresh signed packages; unset FRAME_UNSIGNED.')
SDB = sdb_path()
TV = tv_serial()
INSTALLED = ROOT / 'private/deployments/installed-pair'
PACKAGES = [('FrameDock.tpk', 'tpk', '/opt/usr/apps/org.framehome.overlayprobe/'),
            ('FrameHome.wgt', 'wgt', '/opt/usr/apps/Fr4meHome1/res/wgt/')]


def sdb(*arguments):
    result = subprocess.run([str(SDB), *arguments], capture_output=True, text=True, timeout=45)
    output = result.stdout + result.stderr
    if result.returncode != 0:
        raise RuntimeError(output.strip())
    return output


def verify(directory, evidence):
    checks = {}
    for name, _, prefix in PACKAGES:
        with zipfile.ZipFile(directory / name) as bundle:
            for item in bundle.infolist():
                if item.is_dir() or item.filename in ('author-signature.xml', 'signature1.xml'):
                    continue
                target = evidence / name / item.filename
                target.parent.mkdir(parents=True, exist_ok=True)
                sdb('-s', TV, 'pull', prefix + item.filename, str(target))
                checks[name + '/' + item.filename] = target.read_bytes() == bundle.read(item.filename)
    (evidence / 'checks.json').write_text(json.dumps(checks, indent=2) + '\n')
    return all(checks.values()), len(checks)


def install(directory):
    for name, kind, _ in PACKAGES:
        print('Installing ' + name, flush=True)
        sdb('-s', TV, 'push', str(directory / name), '/home/owner/share/tmp/sdk_tools/' + name)
        output = sdb('-s', TV, 'shell', '0', 'appinstall', kind, name)
        (directory / (name + '-install.txt')).write_text(output)
        succeeded = 'key[end] val[ok]' in output if kind == 'wgt' else 'app_id[org.framehome.overlayprobe] install completed' in output
        if not succeeded:
            raise RuntimeError('Installation did not confirm success: ' + output)


# Fail any source/build checks before touching either installed package.
subprocess.run(['npm', 'run', 'check'], cwd=ROOT, check=True)
output = ROOT / 'private/deployments' / ('ui-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ'))
output.mkdir(parents=True)
before = output / 'before'
before.mkdir()
for name, _, _ in PACKAGES:
    if (INSTALLED / name).exists():
        shutil.copy2(INSTALLED / name, before / name)
subprocess.run(['python3', 'tools/build-web.py'], cwd=ROOT, check=True)
subprocess.run(['sh', 'tools/build-native.sh'], cwd=ROOT, check=True)
subprocess.run(['npm', 'run', 'package'], cwd=ROOT, check=True)
for name, _, _ in PACKAGES:
    shutil.copy2(ROOT / 'build' / name, output / name)
if not any(line.split()[:2] == [TV, 'device'] for line in sdb('devices').splitlines()):
    sdb('connect', TV)
if not any(line.split()[:2] == [TV, 'device'] for line in sdb('devices').splitlines()):
    raise RuntimeError('TV developer connection unavailable; both packages are built, not installed.')
rollback_ready = all((before / name).exists() for name, _, _ in PACKAGES)
if rollback_ready:
    rollback_ready, _ = verify(before, before / 'readback')
if not rollback_ready:
    raise RuntimeError('The saved installed pair does not match the TV; no package was installed. Preserve and verify the current pair first.')
try:
    install(output)
    matched, count = verify(output, output / 'readback')
    if not matched:
        raise RuntimeError('Installed files do not match the signed pair')
    launch = sdb('-s', TV, 'shell', '0', 'execute', 'Fr4meHome1.Launcher')
    if 'successfully launched pid' not in launch:
        raise RuntimeError('Launch did not report success: ' + launch)
except Exception:
    print('Restoring the verified previous pair…', flush=True)
    install(before)
    restored, _ = verify(before, before / 'restored')
    if not restored:
        raise RuntimeError('Rollback readback failed; deployment evidence: ' + str(output))
    sdb('-s', TV, 'shell', '0', 'execute', 'Fr4meHome1.Launcher')
    raise
INSTALLED.mkdir(parents=True, exist_ok=True)
for name, _, _ in PACKAGES:
    shutil.copy2(output / name, INSTALLED / name)
(output / 'status.json').write_text(json.dumps({'installed': True, 'verifiedFiles': count, 'homeLaunched': True}, indent=2) + '\n')
print(f'Both packages installed; {count} files verified; Home opened. Evidence: {output}', flush=True)
