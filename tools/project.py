"""Local build settings and the shared, explicit TV developer endpoint."""
import json
import os
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]


def settings():
    path = ROOT / 'local.json'
    return json.loads(path.read_text()) if path.exists() else {}


def tv_serial():
    host = os.environ.get('FRAME_TV_HOST') or settings().get('tvHost')
    if not host or any(char.isspace() for char in host):
        raise SystemExit('Set FRAME_TV_HOST or tvHost in local.json before connecting to a TV.')
    return host if ':' in host else host + ':26101'


def sdb_path():
    command = os.environ.get('FRAME_SDB') or shutil.which('sdb')
    if not command:
        raise SystemExit('Install the Samsung TV SDK and set FRAME_SDB or put sdb on PATH.')
    return command
