"""Loopback preview: rebuild the existing web target and reload on source changes."""
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import json
import subprocess
import sys
import threading
import time
from project import ROOT

revision = str(time.time_ns())


class PreviewHandler(SimpleHTTPRequestHandler):
    def do_GET(self):
        if self.path == '/__dev_version':
            data = json.dumps({'revision': revision}).encode()
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.send_header('Cache-Control', 'no-store')
            self.send_header('Content-Length', str(len(data)))
            self.end_headers()
            self.wfile.write(data)
        else:
            super().do_GET()

    def log_message(self, format, *args):
        # Keep source/build errors visible without one log line per reload poll.
        if self.path != '/__dev_version':
            super().log_message(format, *args)


def snapshot():
    files = [ROOT / 'local.json', ROOT / 'tools/build-web.py', ROOT / 'tools/project.py']
    for name in ('app', 'dev', 'demo', 'private/preview'):
        files.extend(path for path in (ROOT / name).rglob('*') if path.is_file())
    stamp = {}
    for path in files:
        try:
            stat = path.stat()
            stamp[str(path)] = (stat.st_mtime_ns, stat.st_size)
        except FileNotFoundError:
            pass  # Editors may replace a file atomically during the scan.
    return stamp


def build():
    for name in ('app/app.js', 'dev/simulator.js'):
        if subprocess.run(['node', '--check', name], cwd=ROOT).returncode != 0:
            return False
    return subprocess.run([sys.executable, 'tools/build-web.py', '--preview'], cwd=ROOT).returncode == 0


# Bind before building, so a second invocation never disturbs an active preview.
with ThreadingHTTPServer(('127.0.0.1', 8765), partial(PreviewHandler, directory=str(ROOT / 'build/preview'))) as server:
    if not build():
        raise SystemExit('Preview build failed; fix the source and restart.')
    stamp = snapshot()
    threading.Thread(target=server.serve_forever, daemon=True).start()
    print('Preview: http://127.0.0.1:8765/ — watching app/dev/demo and local fixtures', flush=True)
    try:
        while True:
            time.sleep(.25)
            current = snapshot()
            if current == stamp:
                continue
            time.sleep(.15)  # Coalesce a short editor save burst into one build.
            stamp = snapshot()
            if build():
                revision = str(time.time_ns())
            else:
                print('Preview rebuild failed; waiting for the next source edit.', flush=True)
    except KeyboardInterrupt:
        print('\nPreview stopped.', flush=True)
    finally:
        server.shutdown()
