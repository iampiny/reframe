"""Build standalone Home from HTML/CSS/JS and shared layout tokens. No design tool."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile
import shutil
from urllib.parse import urlsplit
from project import ROOT, settings

parser = argparse.ArgumentParser()
parser.add_argument('--preview', action='store_true', help='Include local-only preview fixtures')
args = parser.parse_args()
source = ROOT / 'app'
target = ROOT / 'build' / ('preview' if args.preview else 'web')
target.parent.mkdir(parents=True, exist_ok=True)
# Assemble and validate before publishing, so a failed edit leaves the last build.
with tempfile.TemporaryDirectory(prefix='frame-web-', dir=target.parent) as temporary:
    output = Path(temporary) / 'site'
    output.mkdir()
    for path in source.iterdir():
        if path.name.startswith('preview-') or (args.preview and path.name == 'tv.js'):
            continue
        if path.is_dir():
            shutil.copytree(path, output / path.name)
        else:
            shutil.copy2(path, output / path.name)
    layout = json.loads((source / 'ui-layout.json').read_text())
    variables = {key: str(value) + 'px' for key, value in layout.items() if type(value) is int}
    for name in ('contentIdleScale', 'contentFocusScale', 'appFocusScale', 'actionFocusScale'):
        variables[name] = str(layout[name])
    b = layout['brightness']
    variables.update(brightnessIdleScale=str(b['idleScale']), brightnessInactiveOpacity=str(b['inactiveOpacity']),
        focusedTileHeight=str(round(layout['tileHeight'] * layout['appFocusScale'])) + 'px',
        iconCollapsedY=str(b['icon']['y']) + 'px',
        iconLift=str(b['iconOpenY'] - b['icon']['y']) + 'px', sheetOffset=str(b['sheetOffset']) + 'px')
    css = ';'.join('--' + key + ':' + value for key, value in variables.items())
    # The same rectangles feed native NUI and static browser CSS. Focus/level state
    # remains in the renderers; no geometry is recalculated during remote input.
    rectangles = {'clock': layout['clock']}
    for name, key in [('control', 'control'), ('surface', 'surface'), ('track', 'track'),
                      ('thumb', 'thumb'), ('icon', 'icon'), ('dim-icon', 'dimIcon'),
                      ('auto-button', 'auto'), ('focus-surface', 'focusSurface'),
                      ('auto-focus-surface', 'autoFocusSurface')]:
        rectangles['brightness-' + name] = b[key]
    geometry = []
    for name, rect in rectangles.items():
        declarations = ';'.join(prop + ':' + str(rect[key]) + 'px'
            for prop, key in [('left', 'x'), ('top', 'y'), ('width', 'width'), ('height', 'height')])
        geometry.append('[data-ui="' + name + '"]{' + declarations + '}')
    geometry.append('[data-ui="clock"]{font-size:' + str(layout['clock']['fontSize']) + 'px}')
    geometry.append('[data-ui="clock"]{opacity:' + str(layout['clock']['opacity']) + '}')
    geometry.append('[data-ui="brightness-dim-icon"]{opacity:' + str(b['dimIcon']['opacity']) + '}')
    color = b['auto']['color']
    geometry.append('[data-ui="brightness-auto-button"]{background-color:rgba(' + ','.join(str(round(c * 255)) for c in color[:3]) + ',' + str(color[3] * b['inactiveOpacity']) + ')}')
    geometry.append('[data-ui="brightness-sheet"]{left:0;top:0;width:' + str(b['control']['width']) + 'px;height:' + str(b['control']['height']) + 'px}')
    geometry.append('[data-ui="brightness-auto-icon"],[data-ui="brightness-auto-icon-on"]{width:' + str(b['autoIcon']['width']) + 'px;height:' + str(b['autoIcon']['height']) + 'px}')
    geometry.append('[data-ui="brightness-fill"]{left:' + str(b['track']['x']) + 'px;width:' + str(b['track']['width']) + 'px}')
    device_script = 'simulator.js' if args.preview else 'tv.js'
    if args.preview:
        for name in ('simulator.js', 'simulator.css'):
            shutil.copy2(ROOT / 'dev' / name, output / name)
    html = (source / 'index.html').read_text()
    assert html.count('<!-- FRAME_DEVICE -->') == 1, 'Expected one device-script placeholder'
    html = html.replace('<!-- FRAME_DEVICE -->', '<script src="' + device_script + '"></script>')
    if args.preview:
        html = html.replace('</head>', '<link rel="stylesheet" href="simulator.css">\n</head>')
    assert html.count('<!-- FRAME_LAYOUT -->') == 1, 'Expected one shared-layout placeholder'
    html = html.replace('<!-- FRAME_LAYOUT -->', '<script id="ui-layout" type="application/json">' + json.dumps(layout) + '</script>\n<style>*,::before,::after{box-sizing:border-box}body{margin:0}.frame-home{' + css + '}' + ''.join(geometry) + '</style>')
    # A stylesheet digest alone does not invalidate its cached SVG/font URLs.
    # Version only local assets referenced by our HTML and shared stylesheet.
    stylesheet = (output / 'styles.css').read_text()
    for asset in sorted(output.rglob('*')):
        if not asset.is_file() or asset.suffix.lower() not in ('.svg', '.png', '.jpg', '.webp', '.woff2'):
            continue
        name = asset.relative_to(output).as_posix()
        digest = hashlib.sha256(asset.read_bytes()).hexdigest()[:12]
        versioned = name + '?v=' + digest
        html = html.replace('src="' + name + '"', 'src="' + versioned + '"')
        for quote in ("'", '"', ''):
            stylesheet = stylesheet.replace('url(' + quote + name + quote + ')', 'url(' + quote + versioned + quote + ')')
    (output / 'styles.css').write_text(stylesheet)
    for name in ['styles.css', 'app.js', device_script] + (['simulator.css'] if args.preview else []):
        digest = hashlib.sha256((output / name).read_bytes()).hexdigest()[:12]
        html = html.replace('"' + name + '"', '"' + name + '?v=' + digest + '"')
    (output / 'index.html').write_text(html)
    origin = os.environ.get('FRAME_PLEX_ORIGIN') or settings().get('plexOrigin', '')
    if origin:
        url = urlsplit(origin)
        if url.scheme not in ('http', 'https') or not url.hostname or url.username or url.password or url.query or url.fragment or url.path not in ('', '/'):
            raise SystemExit('plexOrigin must be a server origin without credentials, path, query or fragment.')
    (output / 'runtime-config.json').write_text(json.dumps({'plexOrigin': origin}) + '\n')
    if args.preview:
        for path in (ROOT / 'demo').iterdir():
            if (output / path.name).exists():
                raise SystemExit('Demo fixture would replace an application asset: ' + path.name)
            shutil.copy2(path, output / path.name)
        fixtures = ROOT / 'private/preview'
        if fixtures.exists():
            for path in fixtures.iterdir():
                if not path.name.startswith('preview-'):
                    continue
                if path.is_dir():
                    shutil.copytree(path, output / path.name)
                else:
                    shutil.copy2(path, output / path.name)
    previous = Path(temporary) / 'previous'
    if target.exists():
        target.rename(previous)
    try:
        output.rename(target)
    except OSError:
        if previous.exists():
            previous.rename(target)
        raise
print('Built standalone Home in', target.relative_to(ROOT))
