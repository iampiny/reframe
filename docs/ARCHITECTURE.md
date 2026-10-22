# Architecture

For recovered firmware contracts, source identities and current confidence levels, use the [developer research handbook](research/README.md). Its [status ledger](research/evidence-and-status.md) separates the public source from the later reference installation. This page describes public code ownership, not portable firmware routing.

## Runtime ownership

`app/app.js` owns full Home, focus, retained provider card views, bounded artwork requests and launch/suspend/resume state. `app/tv.js` contains the narrow Samsung request, launch, key-registration and lifecycle boundary. `dev/simulator.js` supplies asynchronous responses and fake external screens for local development; it does not render a second Home. `native/FrameOverlayApp.cs` owns the transparent NUI partial window, key capture, app-control dispatch, installed-app catalogue and app lifecycle. `FrameFeeds.cs` owns Samsung preview reads, opaque card references, bounded image fetching and guarded provider launch translation. `FrameBrightness.cs` owns current-profile hardware transactions. `FrameStartup.cs` is a bounded cold-start candidate, not a persistent service.

The TV remains the runtime. The computer builds/deploys and serves an optional development preview. Existing apps retain their logins/playback; the project does not implement Netflix or YouTube clients.

## UI contracts

`app/ui-layout.json` is canonical geometry. The web builder embeds its JSON/CSS values; the native package copies it unchanged. Static HTML uses `data-ui` names. Both renderers use `controls-scrim.svg` for full-frame radial contrast behind the clock and brightness; it has no internal clipping rectangle or animation. Sidebar/control dimensions use generated CSS variables; clock and brightness rectangles are generated from the same JSON consumed by NUI. Card wrapper/row widths and heights also use those tokens; card artwork retains a 16:9 aspect ratio. Dynamic selection, scrolling and brightness fill remain renderer-owned. The stage is 1920×1080, scaled to the display; this is not a native 4K rendering pipeline. Browser and native controls load the same SVG glyph files. Images, masks, icon dimensions and spring-like curves remain shared conventions; native NUI and the web engine must still be inspected independently on the TV. No runtime design-tool dependency exists.

Recommendations appear only on full Home. The playback sidebar contains navigation and brightness controls and leaves streaming content visible underneath. Opening it selects the current pinned app; second Home hands its cursor to full Home by app ID. Web Home accepts that handoff only from its owned helper.

## Feed boundary

The firmware getter allocates provider preview metadata that must be released with its matching finalizer. Reads are serialized because the native getter has no cancellation. The implementation bounds feed size, nesting, section/card counts and image transfers; provider payloads/URLs remain inside the helper. Home sees presentation data, images and opaque references.

Netflix and YouTube supplies are real provider rows; language/order are retained. Plex rows are restricted to `server://` library actions. Images use explicit host/origin allowlists, no redirects/cookies/default credentials, and bounded concurrency. Original provider launch data is not serialized into public UI models or logs. Expired/missing references refuse activation. Live app-lock/update checks precede the selected-title request; unknown policy refuses it. YouTube translation uses installed vendor metadata and the stock missing-prefix fragment behavior. Later reference-TV feedback confirms exact-video opening; separate warm/cold, profile and future-version acceptance remain open. See [feed evidence](research/content-feeds.md).

## Integration limits

The tested device uses Tizen 6.0/ARMv7 and firmware-specific private interfaces, including vconf/app-context/settings/content ABI. Two renderers are justified by the current partial-window architecture. UID/SMACK guards and live policy checks remain; this cleanup neither grants privileges nor changes routing. Helper IPC actions and `brightness_key` entries accept only the OS-reported Home/helper sender IDs. Rejection returns failure without hiding or pausing the sidebar. Retained events identify `tvctx-manager` for two background channel requests, but its scoped authorization/acceptance is not implemented; that external IPC route remains refused; captured keys inside our UI remain supported. Startup and native Art wake remain separate: native light/motion/power behavior is preserved.

Custom pins are an owned list shared by the two views. Under the current route, Add to Home from Samsung Apps arrives at `PinFromStore`, which saves only our own list. Samsung Move/Remove synchronization is unfinished. Do not describe this as a bidirectionally synchronized Samsung list.

Fresh read-only investigation confirms the barrier: Samsung's alternating app-layer cache contains a newer, longer list while its readable vconf projection retains an older subset. The native helper can traverse the Home directories but has no SMACK permission to traverse/read CSFS's package-private data. The stock publisher also caches a first-screen identity check; our custom identity prevents it refreshing that projection. No general external reorder/remove/query operation was found in the inspected dispatchers. This is bounded evidence, not a claim about every firmware interface.

The working management path is **Samsung UI → highlight an app → Down → Move or Remove**. Those operations currently change Samsung's list, not reliably ours. Completing synchronization without new privileges requires a routing design that preserves Samsung's first-screen identity while keeping custom Home, Settings, Art and startup transitions. That design and its attended regressions remain pending. No cache editing, MAC changes, caller spoofing or separate editable custom order was introduced.

Artwork now targets Samsung's existing selection browser with a default app-control request and no mode/content selection. The inspected `ArtLaunchControl.StartArt` empty-mode path calls `StartWithLaunchBrowser`; `fullscreen` is reserved for idle entry/native Power. A custom gallery remains unfinished. The reference owner confirmed chooser launch and personal-photo display, while Art exit delay/flicker remains unresolved. The [Art chapter](research/art-mode.md) documents the separate standalone getter/import proof and direct-selection limits.

Normal-TV Auto is separate from native Art sensors. A later bounded backend diagnostic in normal mode verified manual changes disabling Auto, profile-default restoration before enabling Auto, and exact restoration of the original value/flag. The owner confirmed toggle/brightness response; deliberate normal-viewing room-light adaptation remains pending. Configured profile readback is not measured sensor-adjusted panel output. See [brightness contracts](research/brightness.md). The public controllers retain Auto intent while confirming, but still drop busy manual presses; the later queued/held-input change belongs to the separate reference installation.

## Build and release boundaries

`build/preview/` is for the loopback simulator and may contain private local fixtures. `build/web/` and `build/native-package/` are production package inputs. Packaging accepts only those two named targets; preview builds cannot replace production inputs. Unsigned inspection goes into `build/inspection/`; signed installation files remain in `build/`.

Source releases intersect Git-tracked files with the public roots. Untracked files, private evidence and Git history are excluded. A release uses current working-file contents, so review the diff and archive before publishing; being tracked is not proof of privacy.

## Follow-up cases found in this review

A cold overlay process starts with four default apps before its asynchronous installed-app catalogue resolves. Selecting an additional pinned app on that first opening needs an attended check: the later catalogue refresh preserves the initial cursor, which may not yet identify the playing app. Keep that investigation with current-app selection rather than adding a second catalogue or waiting on it before showing the bar.

Closed-sidebar brightness currently permits the four default wrapper IDs. Exact-title YouTube uses a distinct native fullscreen ID, so that launch path needs its own channel-button check. Neither case is described as accepted TV behavior.

## Local preview contract

The web builder selects exactly one device script: `tv.js` for production, or `simulator.js` plus its shell stylesheet for preview. The shared `app.js`, HTML, geometry, glyphs and Home styles are unchanged between targets. The core owns busy state, reply validation, bounded SDK/helper completion, pending-work tracking, bounded artwork loading, launch draining, error recovery and idle eligibility. There is no simulated Tizen SDK, platform registry or second web renderer.

Both tile and media-card launches use `performLaunch`: stop new artwork work, wait for outstanding device replies, invoke the selected device operation, then confirm or restore Home on failure. Late feed replies update retained data while Home is suspended; they do not repaint its overview or steal focus. Preview brightness goes through the same optimistic update, busy guard and confirmation validation as TV brightness. The fake device supplies a 0–50 value and Auto flag asynchronously; it never bypasses the common response handler.

The device boundary supplies `request`, `launch`, key translation, Back handling and startup lifecycle callbacks. Production preserves owned-helper caller validation before accepting a focus handoff and keeps the current Samsung app-control payloads. Home queues a return received before launch acknowledgement, clears it on a later suspension, and ignores redundant active focus notifications. A late launch failure does not resume a hidden view. Timeouts stop waiting but cannot cancel the external Samsung operation. Presentation mode is reported to the shared Home view; the simulator owns which external screen is underneath it. Its sidebar view remains a browser simulation of NUI, not evidence of native rendering or firmware behavior.

`tools/preview.py` binds loopback, watches source/fixtures/configuration, checks preview JavaScript, invokes the existing builder, and notifies the page to reload after a successful build. Local assets referenced by HTML/styles receive content hashes, including SVGs and fonts, so a rebuild invalidates the browser cache. Builds are assembled before replacing the generated target; failed assembly retains the previous build. Reload uses a full page load rather than a hot-module framework. The watcher is development tooling and is never packaged on the TV.

Brightness focus uses a dark layer behind the glyphs/track; unfocused backgrounds use half opacity. Auto preserves the entire brightness sun path and viewBox unchanged. A corner A badge is outlined when off and filled when on, independent of focus. Web Auto pressed/disabled attributes update only when those states change, avoiding repeated state repaint during manual slider updates. Native uses the same focus SVGs, inactive opacity and paired state glyphs. The fixed 50% room response is isolated to `dev/simulator.js`; production restores the active picture profile’s own backlight default when Auto is enabled, then turns on the Eco Sensor. It verifies AVOC’s configured value against the menu scale and confirms writes; it never resets other picture settings or writes the database.

The production `brightness` getter reports the current picture-profile setting. Whether it exposes the effective sensor-adjusted panel output still requires a normal-TV runtime check. Do not reset a TV profile to the simulator target or call its configured setting a measured sensor output.
