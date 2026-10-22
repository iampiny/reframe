# Setup

For firmware contracts and extension work, start with the [research handbook](research/README.md) and [evidence ledger](research/evidence-and-status.md). These explain reference-device research; they do not supply a portable Home/boot routing installer.

## Desktop UI

Python 3.10+ and Node.js are sufficient. `npm run preview` builds and serves `build/preview/` on loopback, then watches source changes and reloads the browser after successful builds. JavaScript/build errors appear in the terminal; failed builds leave the previous output in place. Production builds go to the separate `build/web/` directory, so packaging does not pick up preview fixtures and TV builds do not erase the running preview. There are no npm dependencies. Fictional data in `demo/` is copied only for preview builds. Optional private preview files use `preview-*` names in `private/preview/` and are never included in source releases.

Edit `app/index.html`, `app/styles.css`, and `app/app.js` directly. Change shared geometry in `app/ui-layout.json`; `npm run build:web` embeds the same tokens in Home that the native sidebar reads. No design application is required.

## TV prerequisites

Use a supported Samsung TV SDK, its SDB utility, .NET SDK/reference assemblies compatible with the TV's netcoreapp3.1/Tizen 6.0 runtime, and a certificate profile you own. Follow Samsung's [SDK setup](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk.html), [certificate creation](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/creating-certificates.html), and [CLI packaging](https://developer.samsung.com/smarttv/develop/getting-started/using-sdk/command-line-interface.html).

On the TV, open Apps and enter `12345` to reach Developer Mode. Enable it, enter the development computer's LAN address, and restart when prompted by Samsung. Connect both devices to the same LAN. Do not upgrade TV firmware merely to follow these instructions: the private integration is validated only against the stated firmware.

Configure local paths without committing them:

```sh
cp local.example.json local.json
# Edit tvHost to the TV's LAN address.
# Set plexOrigin only if you want artwork from your own Plex server.
export FRAME_SDB="/path/to/sdk/tools/sdb"
export FRAME_TIZEN="/path/to/sdk/tools/ide/bin/tizen"
export FRAME_DOTNET="/path/to/dotnet"
export FRAME_TIZEN_REFERENCES="/path/to/tizen-reference-assemblies"
export FRAME_TIZEN_SERVICE_REFERENCE="$FRAME_TIZEN_REFERENCES/Tizen.Applications.Service.dll"
export FRAME_SIGN_PROFILE="your-certificate-profile"
npm run build
```

`FRAME_TV_HOST` overrides `tvHost`; `FRAME_PLEX_ORIGIN` overrides `plexOrigin`. Plex accepts an HTTP(S) origin without credentials/query/path. Only matching-origin artwork requests are allowed. Without one, Plex artwork is unavailable rather than sent to an invented default server.

## First installation

The paired updater intentionally requires a saved, verified installed pair, so **do not use `npm run deploy` for first installation**. Use Samsung Device Manager or the official CLI to install the two signed files in `build/`, following its normal target/certificate checks. Launch `Fr4meHome1.Launcher` normally. Preserve those exact packages under `private/deployments/installed-pair/` as `FrameHome.wgt` and `FrameDock.tpk` before using the paired updater.

This supplies the Home app, not automatic Home-button/boot interception. A portable routing installer does not exist yet. Do not reuse another TV's app-context database or undocumented research patch. Record the ordinary launcher and native behavior before attempting separately reviewed routing research.

## Inspection without certificates

```sh
npm run build:web
FRAME_UNSIGNED=1 npm run build:native
python3 tools/package.py web --unsigned
```

Unsigned archives are written to `build/inspection/`, separate from signed installation packages. They are for checking contents and local builds. They are **not signed TV installations**. Public SDK-profile signing still needs end-to-end validation on a fresh developer setup; neither unsigned archives nor successful compilation proves that another TV will accept installation.

Packaging has two targets: `python3 tools/package.py web` and `python3 tools/package.py native`. Both consume only their production build directories. `--unsigned` writes inspection archives; `FRAME_UNSIGNED=1` is supported for native inspection builds and is rejected by the updater.

## Simulator behavior

The preview uses the production Home controller. Its device replies are asynchronous (40 ms by default), so loading, busy controls, optimistic brightness, request draining and launch failures follow the same UI paths. `H` opens the sidebar over a simulated app; a second `H` returns to full Home with the selected cursor. Backspace/Delete or `R` closes the playback sidebar. Back inside brightness or cards returns to the sidebar first. App stand-ins do not implement real playback, Samsung routing or native sensor behavior.

To inspect delayed or failed responses, use a preview URL such as `http://127.0.0.1:8765/?delay=1500&fail=set-brightness`. `delay` is milliseconds (0–30000); `fail` rejects the named device operation. Existing examples include `launch`, `get-feed`, `get-feed-image`, `open-feed-item`, `get-brightness`, `set-brightness` and `set-auto-brightness`. Remove the query to restore normal responses. These controls exist only in the simulator.

The watcher monitors `app/`, `dev/`, `demo/`, optional `private/preview/`, `local.json`, the web builder, and its `tools/project.py` settings dependency. Restart `npm run preview` after changing the watcher itself. It deliberately uses a full page reload; source changes reset the simulated external screen. Fixture copying cannot override application code/assets. Public demo data remains offline, and private local fixtures remain excluded from production packages and source releases.

The simulator's fixed demo room uses a 50% Auto brightness target. Enabling Auto updates the confirmed slider value and simulated backlight; disabling Auto holds the resulting level. A manual change turns Auto off. This fixture value never reaches the TV: production restores the active picture profile’s own backlight default, enables Samsung's Eco Sensor and reports the configured setting returned by its own backend. The browser has no physical light sensor. Simulated backlight dims the entire 16:9 TV frame (Home, cards, controls and app stand-ins), while the surrounding page stays unchanged. The minimum retains 15% visibility so the controls remain usable; this display simulation never changes the laptop’s physical backlight or the TV.
