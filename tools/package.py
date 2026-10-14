"""Package generated files; signing uses the owner's SDK certificate profile."""
import argparse
import os
import shutil
import subprocess
import zipfile
from project import ROOT

parser = argparse.ArgumentParser()
parser.add_argument('target', nargs='?', choices=('web', 'native'), default='web')
parser.add_argument('--unsigned', action='store_true', help='Create a local inspection archive, not installable on the TV')
args = parser.parse_args()
# Only production build directories are package inputs. Preview cannot be signed.
if args.target == 'web':
    directory, name, kind = ROOT / 'build/web', 'FrameHome.wgt', 'wgt'
else:
    directory, name, kind = ROOT / 'build/native-package', 'FrameDock.tpk', 'tpk'
output = ROOT / 'build' / name
if not directory.is_dir():
    raise SystemExit('Build the production ' + args.target + ' target before packaging.')
if args.unsigned:
    output = ROOT / 'build/inspection' / output.name
output.parent.mkdir(parents=True, exist_ok=True)
if args.unsigned:
    with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as bundle:
        for path in sorted(directory.rglob('*')):
            if path.is_file() and path.suffix not in ('.wgt', '.tpk'):
                bundle.write(path, path.relative_to(directory))
    print('Created unsigned inspection archive:', output.name)
else:
    cli = os.environ.get('FRAME_TIZEN') or shutil.which('tizen')
    profile = os.environ.get('FRAME_SIGN_PROFILE')
    if not cli or not profile:
        raise SystemExit('Set FRAME_TIZEN and FRAME_SIGN_PROFILE for your Samsung SDK certificate profile, or use --unsigned for inspection only.')
    # Require exactly one new SDK output; never reuse an older signed bundle.
    for path in directory.glob('*.' + kind):
        path.unlink()
    subprocess.run([cli, 'package', '-t', kind, '-s', profile, '--', str(directory)], check=True)
    generated = list(directory.glob('*.' + kind))
    if len(generated) != 1:
        raise SystemExit('SDK did not produce exactly one fresh package; inspect its output before deployment.')
    shutil.move(str(generated[0]), output)
    print('Signed package:', output.name)
