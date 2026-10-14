#!/bin/sh
set -eu
frame_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
frame_dotnet=${FRAME_DOTNET:-dotnet}
: "${FRAME_TIZEN_REFERENCES:?Set FRAME_TIZEN_REFERENCES to the copied Tizen reference DLL directory}"
"$frame_dotnet" build "$frame_root/native/FrameOverlayApp.csproj" -p:UseSharedCompilation=false -p:TizenReferenceDir="$FRAME_TIZEN_REFERENCES" -o "$frame_root/build/native-out"
: "${FRAME_TIZEN_SERVICE_REFERENCE:?Set FRAME_TIZEN_SERVICE_REFERENCE to the copied Service DLL}"
"$frame_dotnet" build "$frame_root/native/FrameStartup.csproj" -p:UseSharedCompilation=false -p:TizenReferenceDir="$FRAME_TIZEN_REFERENCES" -p:TizenServiceReference="$FRAME_TIZEN_SERVICE_REFERENCE" -o "$frame_root/build/native-out"
frame_package="$frame_root/build/native-package"
rm -rf "$frame_package"
mkdir -p "$frame_package/bin" "$frame_package/shared/res"
# Copy exactly the two owned app entrypoints and bundled assets.
for frame_name in FrameOverlayApp.dll FrameOverlayApp.deps.json FrameOverlayApp.runtimeconfig.json FrameStartup.dll FrameStartup.deps.json FrameStartup.runtimeconfig.json; do
 cp "$frame_root/build/native-out/$frame_name" "$frame_package/bin/$frame_name"
done
cp "$frame_root/native/tizen-manifest.xml" "$frame_package/tizen-manifest.xml"
cp "$frame_root/native/res/brightness-track.png" "$frame_root/native/res/brightness-fill.png" "$frame_root/native/res/brightness-thumb.png" "$frame_root/native/res/brightness-surface.png" "$frame_root/native/res/sidebar-gradient.png" "$frame_root/native/res/tile-surface.png" "$frame_root/native/res/action-surface.png" "$frame_root/native/res/probe.png" "$frame_package/shared/res/"
for frame_name in controls-scrim.svg runtime-config.json tile-mask.png ui-layout.json control-artwork.svg control-store.svg control-settings.svg control-samsung.svg brightness-auto.svg brightness-auto-on.svg brightness-focus.svg brightness-auto-focus.svg brightness-icon.svg brightness-dim-icon.svg; do
 cp "$frame_root/build/web/$frame_name" "$frame_package/shared/res/$frame_name"
done
mkdir -p "$frame_package/shared/res/fonts"
cp "$frame_root/native/res/fonts/"* "$frame_package/shared/res/fonts/"
if [ "${FRAME_UNSIGNED:-0}" = 1 ]; then
 python3 "$frame_root/tools/package.py" native --unsigned
else
 python3 "$frame_root/tools/package.py" native
fi
