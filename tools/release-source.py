"""Create an allowlisted source release. Never exports Git history or local TV data."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tarfile
from project import ROOT

roots = ['README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'LICENSES', '.gitignore',
         'package.json', 'local.example.json', 'app', 'native', 'demo', 'dev', 'tools', 'docs']
excluded_dirs = {'obj', 'bin', '__pycache__', 'sdk-certificates', 'node_modules', 'private', 'build', '.git'}
excluded_suffixes = {'.p12', '.pfx', '.pem', '.pw', '.dll', '.wgt', '.tpk', '.pyc'}
# Git defines reviewed source membership, while roots define release scope.
# Untracked files under app/tools/docs never become public by accident.
try:
    tracked = subprocess.run(['git', 'ls-files', '-z', '--', *roots], cwd=ROOT,
                             capture_output=True, check=True).stdout.decode().split('\0')
except (subprocess.CalledProcessError, OSError) as error:
    raise SystemExit('Create releases from a Git checkout; stage reviewed new source files first.') from error
if 'package.json' not in tracked:
    raise SystemExit('Run source export in this project’s Git checkout, with package.json tracked.')
files = []
for name in sorted(filter(None, tracked)):
    relative = Path(name)
    item = ROOT / relative
    if not item.is_file() or any((ROOT / Path(*relative.parts[:i])).is_symlink() for i in range(1, len(relative.parts) + 1)):
        continue
    if excluded_dirs.intersection(relative.parts) or item.suffix in excluded_suffixes:
        continue
    if relative.parts[0] == 'app' and item.name.startswith('preview-'):
        continue
    if item.name in {'sdb', 'local.json', 'runtime-config.json'}:
        continue
    files.append(item)
# Refuse obvious personal paths, LAN defaults, signing keys and browser design markers.
# This is a bounded automated check, not proof that every possible PII is absent.
patterns = [r'/Users/[A-Za-z0-9._-]+/', r'192\.168\.\d{1,3}\.\d{1,3}',
            r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----', r'data-pencil-(?:name|id)=',
            r'Photos Library\.photoslibrary', r'pencil://[A-Za-z0-9]', r'\.pen(?:[\"\'\s]|$)']
violations = []
for path in files:
    try:
        content = path.read_text()
    except UnicodeDecodeError:
        continue
    for pattern in patterns:
        if re.search(pattern, content):
            violations.append(str(path.relative_to(ROOT)) + ': ' + pattern)
if violations:
    raise SystemExit('Release privacy review required:\n' + '\n'.join(violations))
version = json.loads((ROOT / 'package.json').read_text())['version']
out = ROOT / 'build/releases';out.mkdir(parents=True, exist_ok=True)
archive = out / ('frame-home-source-' + version + '.tar.gz')
manifest = {str(path.relative_to(ROOT)): hashlib.sha256(path.read_bytes()).hexdigest() for path in files}
with tarfile.open(archive, 'w:gz') as bundle:
    for path in files:
        info = bundle.gettarinfo(str(path), arcname='frame-home/' + str(path.relative_to(ROOT)))
        info.uid = info.gid = 0
        info.uname = info.gname = ''
        info.mtime = 0
        info.mode = 0o755 if path.suffix == '.sh' else 0o644
        with path.open('rb') as source:
            bundle.addfile(info, source)
report = {'fileCount': len(files), 'automatedPrivacyCheck': 'passed',
          'manualReviewRequired': True, 'gitHistoryIncluded': False, 'files': manifest,
          'archiveSha256': hashlib.sha256(archive.read_bytes()).hexdigest()}
(out / 'source-manifest.json').write_text(json.dumps(report, indent=2) + '\n')
print('Created', archive.name, 'with', len(files), 'files; automated privacy check passed; manual review remains required.')
