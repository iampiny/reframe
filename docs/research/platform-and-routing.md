# Platform, routing and app-list research

This chapter records reusable findings from a **2021 Samsung The Frame, LS03A family, Tizen 6.0, ARMv7, firmware T-NKM2DEUC-2301.1**. It is a reconstruction of the inspected firmware, not a public Samsung API contract or a complete map of every TV function. Documentation reconciled on 22 October 2026; **no new TV test was run for this publication**.

Evidence labels used below are **Static** (inspected shipped code/configuration), **Runtime** (bounded calls or state readback), **Physical** (someone observed the TV/remote), **Source** (present project behavior), and **Proposed** (not validated). A launch return code, a database value and a visible result establish different things. Local raw logs, Samsung binaries and personal app/account state are intentionally not distributed. Stable evidence IDs and artifact digests below identify the retained basis without requiring those private files.

## PLATFORM-CORE — full apps, partial apps and policy

**Static + Runtime + Physical.** Samsung's `tvctx-manager` combines `/etc/tvctx-manager/appctx-config.json` with registered rows in `/opt/dbspace/.app-context.db`. It manages a full-screen app and a partial/OSD app separately. An app can remain visible and playing underneath an input-owning partial window. This is how the custom sidebar can appear over YouTube or Netflix without replacing their players or logins.

| Component / ID | Role established here |
|---|---|
| `tvctx-manager` and `context-common.so` | Managed task classification, focus/stacking, key routing and first-screen returns. |
| `app-boot-manager` | Early normal/last-app/first-screen startup policy; separate from a physical Home-key route. |
| `com.samsung.tv.csfs` | Samsung Home/bar and stock app-list/cache producer, using CoBA. Keeping its service available matters while relying on its data. |
| `org.tizen.tv-viewer` | Live-TV/last-source surface and participant in source/audio policy. An aerial-disconnected message does not make this backend safe to remove. |
| `org.tizen.menu` | Samsung Settings. |
| `com.samsung.tv.store` | Samsung Apps store. |
| `org.tizen.art-app` | Native artwork renderer/browser, sensors, timers and ambient power transitions. See the Art chapter. |
| `Fr4meHome1.Launcher` | This project's full web Home. The historical ID is retained for installed-package identity, despite the current Reframe name. |
| `org.framehome.overlayprobe` | This project's native partial sidebar/dispatcher. Its historical probe ID remains part of the deployed identity. |
| `org.framehome.overlayprobe.startup` | Bounded startup candidate, not a permanent supervisor. |

The tested default media IDs are `org.tizen.netflix-app`, `9Ur5IzDKqV.TizenYouTube`, `kIciSQlYEM.plex`, and `w0wZu3xkQq.GeForceNOW`. They are **installed app identifiers, not portable store identifiers**. YouTube's exact-video path can run under `com.samsung.tv.cobalt-yt`; matching only its wrapper ID misses that foreground state. Discover the installed catalogue on a new TV rather than treating these IDs as universal.

### Read-only state diagnostics

From an already-authorized developer shell, the following queries read state without changing routes or launching apps:

```sh
vconftool get memory/appfw/current_fullscreen_appid
vconftool get memory/appfw/current_partial_appid
vconftool get db/first_screen/type
vconftool get memory/eden/show_status
vconftool get db/menu/smart_hub/ontvsetting/auto_start
vconftool get db/menu/app_booting
```

The code uses `libvconf.so.0:vconf_get_str`, whose returned allocation is released with `free`. Power/Art state is read through `libdeviced.so.1:device_power_get_state` and `device_power_get_ambient_mode`. The startup guard uses power `0` and ambient `0` for its tested normal-viewing condition. Do not assign an intuitive enum name to every untraced numeric value.

Diagnostics should record only the state needed for the question, monotonic timing and a local correlation token. Full AppControl bundles, feed URLs, photo identifiers and boot IDs can contain private or device-specific state. Do not include their raw values in a public bug report.

## PLATFORM-LIFECYCLE — a transparent window is not enough

**Runtime + Physical.** The native sidebar had to be registered as a **partial managed app** and Samsung's manager had to reload that registration. Merely creating a transparent window or receiving a successful launch acknowledgement did not establish correct focus, full/partial state or return behavior.

The working dismissal path consumes **both edges** of Back, waits for key release, and asks the context manager to pause the **owned** package:

```csharp
[DllImport("libcapi-appfw-app-context-manager.so")]
static extern int app_context_manager_request_task(string target, string action);

// Only for this application's registered partial surface.
int result = app_context_manager_request_task(
    "package:org.framehome.overlayprobe", "pause");
```

A return of `0`, `OnPause`, a cleared partial identity, restored underlying input and the visible result were checked separately. Earlier `Lower`, `Hide` and process-exit experiments caused Samsung's bar to appear. Consuming Back alone was not sufficient. Copying the last `Lower()` call out of Samsung's lifecycle code does not reproduce its prerequisite focus/application state.

The stock `ImplNUI.dll` shows `NUIApp._OnPause` clearing resume/focus state before base pause, and `_OnResume` restoring TV key handling. Its `WindowHider.OnHideFinished` lowers a window only after checking window-control, paused-with-show and resume state. The practical rule is to coordinate manager lifecycle and your own visibility/input state rather than fighting the compositor.

**Physical, later than the initial probes:** the sidebar has appeared over both YouTube and Netflix while video/audio continued; arrows stayed in the sidebar; Back dismissed only the sidebar; underlying Play/Pause remained usable. Opening the sidebar, then pressing Home again, reaches full custom Home. Full Home does not open a duplicate sidebar over itself. These observations supersede early notes that called the overlay merely a temporary unverified prototype. They do not prove compatibility with every streaming player, DRM mode or future firmware.

The web Home and native sidebar still have **separate rendering code**, sharing geometry/assets and data contracts. A single persistent native sidebar over both Home and players is a documented architectural possibility, not the current implementation. A browser simulation of the overlay is not proof of NUI rendering or TV focus behavior.

### Other native interactions to preserve

The inspected stock policy also routes mute/volume through `org.tizen.tv-viewer` (`mute=toggle`, `volume=up/down`, and `up2/down2` repeats), separately registers volume/Bluetooth-audio surfaces, and registers `org.tizen.hdmicec`. Source-change events stop or pause channel-management surfaces. These are **static leads**, not a validated custom audio/source API. Keep native source, audio, external-device volume and HDMI-CEC services intact. A replacement Home is not a replacement HDMI/power/audio stack; dedicated physical coverage is still needed if extending these controls.

### Web/native transport and trust

Early direct requests from the separate web package to a native service failed before the service handler logged a request. Same-package native calls worked. The strongest supported explanation is the cross-package service launch restriction, not a missing Internet permission or a need to declare platform privileges.

A bounded **web → native UI entry → same-package service → correlated reply** diagnostic later succeeded with Home still visible/focused. It proved that read transport, not a complete shipping service architecture. The current public helper instead dispatches bounded requests through its native UI entry and runs blocking work away from remote-key handling; its startup service is not the old read relay.

The received AppControl wrapper clones the native handle, allowing an asynchronous reply to the retained request object. Authenticate the actual OS caller, accept only a fixed action/schema, bound payloads and deadlines, and do not treat unknown extras as trusted code or destinations. A hidden read entry must not show/focus the dock or pause a newer user-opened dock when an older read completes. Current `ReadRequest` tracks shared in-flight reads, replies before background pause, and waits for the drain before launching another app. That distinction matters to both responsiveness and privacy: a successful UI transport does not grant cross-package filesystem access.

## PLATFORM-ROUTES — Home, first-screen and registration

**Static + Runtime.** There are at least three independent layers:

1. The short Home key's `key_filter` route.
2. A managed native task registration, including `ui_type=partial` for the sidebar.
3. The broader cached first-screen destination in `db/first_screen/type`.

The investigated replacement changed short Home from a full-web target to the native dispatcher, and changed first-screen from stock CSFS to that same native dispatcher. An illustrative **record shape**, not an installation command, is:

```json
{
  "shortHome": {
    "appid": "com.samsung.tv.csfs",
    "key": "XF86Home",
    "longpress": null,
    "target_selector": "package:org.framehome.overlayprobe",
    "args": "entry_point:smarthub_key"
  },
  "managedApp": {
    "appid": "org.framehome.overlayprobe",
    "ui_type": "partial",
    "source": null
  },
  "firstScreen": {
    "key": "db/first_screen/type",
    "value": "org.framehome.overlayprobe"
  }
}
```

This is model-specific recovered state. The database has more columns, selectors and policies than shown. The public project does **not** include a universal database-write installer. Ordinary app-launch privileges do not grant permission to edit these root-owned registration stores.

### Why first-screen is broader than Home

`app-boot-manager:mgr_util_get_firstscreen_appid` reads the configured string; the inspected producer passes it through job scheduling to AUL without a CSFS-only whitelist in that layer. This proves producer-side forwarding, **not** that any web app is a valid first-screen replacement.

`context-common.so` constructs the `firstscreen` role as a native `capp`/OSD task and adapts full/partial classification from `memory/eden/show_status`. `tvctx-manager` separately caches this target and uses it for wake/relocation and some ambient exits. A `__K_RELOCATE_BELOW` bundle can request stacking below a restored app. Literal CSFS fallback attachments also remain in the static policy. Changing one vconf string live does not prove all consumers refreshed, or that every Samsung-bar path was replaced.

This explains why the current project uses a native entry to decide between full Home and a partial sidebar, rather than assigning the full web page every Samsung role. `reload=disable` keeps repeated web launches from unnecessarily reloading Home; it does not solve manager classification or startup.

### Registration and rollback discipline

The original investigation saved an online SQLite backup, exact old row values, vconf values, schema/integrity results and an inverse before each scoped mutation. Changes were constrained to known rows, with exact-match preconditions and changed-row counts; long Home/Power rules and stock executables were retained. Inverses were exercised and logical tables compared.

For a new firmware, begin with read-only schema/capability inspection. For example, on a **local backup** made by an authorized device owner:

```sql
SELECT name, sql FROM sqlite_master
WHERE type IN ('table', 'index')
  AND (name IN ('key_filter', 'managed_app')
       OR tbl_name IN ('key_filter', 'managed_app'));
SELECT appid, key, longpress, target_selector, args
FROM key_filter WHERE key = 'XF86Home';
PRAGMA integrity_check;
```

A backup is evidence and recovery material, not something to copy wholesale over a live database. A scoped inverse must refuse unexpected new values, preserve unrelated settings and handle interruption between SQLite and vconf writes. Cached consumers may require an attended restart to adopt restored policy. The original privileged maintenance path is **not published as a reusable privilege-grant or root-install recipe**.

## PLATFORM-KEYS — event delivery and consumption

**Static + Runtime + Physical.** NUI key-grab enums differ from event key codes: the inspected adaptor uses **Back 166** and **Home 183** for `GrabKey`/`UngrabKey`; integer `9` is Escape in that API. Topmost registration/release returned true under the ordinary installed identity, while shared registration failed. Registration acceptance alone did not prove hidden delivery or precedence over Art.

`Window.KeyEvent` is a void notification with no `Handled` flag; observing it is not proof of consuming the key. A focused `View.KeyEvent` callback returning true is a different contract. NUI grab modes are Topmost=0, Shared=1, OverrideExclusive=2 and Exclusive=3 in the inspected API. Samsung TV.FLUX has a different backend/enum and managed platform-privilege gate; its void wrapper discards the native registration result. Do not interchange those mode numbers or count a void call as an ordinary-app interception pass.

Focused Home delivery was later physically observed over custom Home and native Art. Hidden global Home interception remains unproved; the first press therefore relies on Samsung's manager route. While the sidebar is visible, it temporarily grabs Home and releases it on pause. The production handler limits itself to navigation/activation/dismissal and brightness-related keys; unrelated playback, volume/mute and Power remain native.

| Action | Established behavior / boundary |
|---|---|
| Short Home over playback | Custom sidebar over continuing video/audio; second Home reaches full custom Home. **Physical pass.** |
| Repeated Home on full custom Home | Screen remains steady, no duplicate overlay. **Physical pass.** |
| Back in custom sidebar | Dismisses only sidebar after both key edges and owned context pause. **Physical pass.** |
| Back inside another app | App retains its own navigation/exit semantics. It is not globally redefined as exit-to-custom. |
| Long Home | Separate saved route to `org.tizen.remote-management` with stored `longpress=5300`. Not changed or fully physically tested. |
| Long Back | Separate saved `XF86Back` route with stored `longpress=5000`, targeting Settings' `menuitem:picture-test`. Not a normal short-Back rule. |
| Exit | Static `XF86Exit` policy can attach stock CSFS above `last:source`. Do not indiscriminately delete recovery/fallback routes. |
| Short Power / Art | Retains Samsung native transition; not equivalent to launching an ordinary fullscreen app. See Art chapter. |

Stored `longpress` numbers are configuration values, not measured hold thresholds on every remote.

## PLATFORM-RETURNS — Settings, Apps, Samsung visits and Art

The default AppControl operation is `http://tizen.org/appcontrol/operation/default`. These are **recovered firmware contracts**, not generic web URLs:

| Destination | Application ID | Extra data |
|---|---|---|
| Full Samsung Settings | `org.tizen.menu` | `uri=/opt/apps/org.tizen.menu/res/json/ATSC/menu.json` |
| Samsung Apps store | `com.samsung.tv.store` | `Sub_Menu=main` |
| Deliberate Samsung bar visit | `com.samsung.tv.csfs` | `entry_point=smarthub_key` |
| Artwork selection browser | `org.tizen.art-app` | No mode/content selection. |
| Native full picture mode | `org.tizen.art-app` | `mode=fullscreen`, guarded against inappropriate power/Art context. |
| Deliberate Art teardown | `org.tizen.art-app` | `mode=exit`, `reason=paused`. |

Minimal launch example:

```csharp
var request = new AppControl {
    ApplicationId = "com.samsung.tv.store",
    Operation = "http://tizen.org/appcontrol/operation/default"
};
request.ExtraData.Add("Sub_Menu", "main");
var result = await AppControl.SendLaunchRequestAsync(request, null);
// Then check foreground/partial state and the actual TV result.
```

**Settings:** static `ExitWork` reads `db/first_screen/type` and launches that target before closing Settings. No caller-supplied return override was found in that normal branch; `return_to_menu` is outgoing origin data, not an established incoming override. This is why remapping only Home originally left Samsung's bar on Settings Back. Later direct Settings launch and return to full custom Home without either extra bar **physically passed**. Consuming every Settings Back externally would break submenu navigation. The same `ExitWork` has timeout/outside-click callers, so root Back is not its only trigger. Settings sends `calledApp=org.tizen.menu` and outgoing `return_to_menu=1` for a visible main menu; `launchby`, `launchoption` and `return_to_menu` are not proven incoming return-target overrides. A separate shortcut-view branch compares the real OS caller with first-screen and can relaunch it carrying original menuitem/type data, except `networkstatus`. Keep submenu Back, root exit and shortcut return distinct. Static anchor: `menu` SHA256 `611fb7d0607b042223c4e7b408987e57b4a228c8215093982e54762d7cf467bb`.

**Apps:** opening the stock store and returning to custom Home **physically passed**. Installing an app, pinning it, and silently installing arbitrary packages are different capabilities. The Apps shortcut intentionally opens Samsung's store.

**Samsung visit:** pausing the owned sidebar *before* launching CSFS prevents a later pause from hiding the newly opened Samsung OSD. Stock bar launch and return to full Home passed with unchanged route tables during the original probe. The later UI behavior is a deliberate stock-bar visit over the current full app; Back hides that bar. This simpler behavior was physically accepted. A persistent session-wide “Samsung mode” with a boot-ID-tagged flag was an earlier proposal, **not an implemented requirement or feature**. Opening this stock bar need not close background playback.

Bounded alternative launches of `com.samsung.tv.coba.home` and `com.samsung.tv.coba.edenview` returned without a changed sampled full/partial/Eden state. CSFS `contentbrowser=1`, recovered from `Program.OpenContentsBrowser`, produced a CSFS partial over Home with Eden still none, not a verified full stock landing. These are negative/state-only results, not alternate working fullscreen APIs. They explain choosing the physically accepted stock-bar visit.

**Art:** Home within Art can terminate Art with an `Itself` reason, after which the manager can launch its cached first-screen. Short Power follows a separate ambient/system route. The `exit/paused` diagnostic used native teardown and did not produce a stock partial at its sampled state; the underlying tuner resumed and required a separate Home launch. Later Home/short-Power exit to custom Home physically passed. The latest observation adds a **remaining regression**: several seconds of black screen and occasional flicker during Art exit. Historical launch timings are consistent with a slow launch, but do not prove the cause of the visible flicker. Do not claim seamless Art return is fully accepted.

The source dispatcher preserves native Art when `entry_point=booting` arrives with ambient mode active. Normal first-screen entry, an unused tuner, an empty full identity or an Art return can instead lead to full Home. `ShowDock` deliberately refuses another sidebar over full Home. These guards are product-specific policy, not a complete reconstruction of Samsung's wake state machine.

## PLATFORM-STARTUP — why cold boot is still open

**Static + Runtime + Physical failure.** Installing a launchable app, routing Home and configuring first-screen did not establish automatic cold startup. Attended restarts still reached the stock initial bar and tuner/aerial-disconnected screen; waiting roughly two minutes did not open custom Home. Manual Home eventually worked, sometimes after another press. Cold startup must be retested, not described as fixed.

### Certificate policy and late on-boot dispatch

The copied native installer deliberately rewrites `service-application` `on-boot` and `auto-restart` to false for PUBLIC/non-PARTNER/non-PLATFORM TV packages targeting API **2.4 or later**. The test package targets API6.0. The input manifest retained true, but the processed manifest/registration became false. The common manifest parser faithfully inserted that processed value; it was not the component stripping it.

`libapp-installers.so.1.33.0` derives the privilege level from certificate/signature context. Merely declaring another manifest privilege or changing spelling does not grant it. Lowering API version to evade this condition misrepresents compatibility and is not the supported project approach. Ordinary author-signing, distributor authorization and update identity remain separate installation requirements; public sources contain no private keys/certificate chains and do not grant Samsung partner/platform status.

After a separately scoped historical registration diagnostic, an ordinary-identity observer did execute after reboot, first sampled around **99.45 seconds** uptime. This established execution, not early visible startup. Static `libamd.so.1` tracing found normal boot-completion callbacks plus a **90-second fallback**, followed by the configured one-second service queue. A delayed owner-manager target remained activating, and the inspected waiting helper could bypass its deadline through a blocking-read loop. That is a supported explanation for late dispatch, not a complete proof of the live blocked syscall or a repaired system service. Raising on-boot priority does not remove the gate before the queue.

AMD releases the queue when `memory/sysman/booting_status` becomes 1, or after both systemd `StartupFinished` and `UserSessionStartupFinished` (with UID argument) are observed, forming its internal `startup.finished`. `BootingDone` on `org.tizen.system.Booting` is a different signal. A sampled script wrote status 3 and exited 0: successful process exit did not prove boot completion. Owner-manager finish occurs only after pending jobs empty; `basic.target` readiness can precede it. The static 90 s fallback fits a later dispatch cohort but no retained callback trace proves its exact arm/release instant.

### Owner-manager access and missing path ambiguity

An ordinary installed diagnostic used `libsystemd:sd_bus_open_user` successfully; the returned endpoint was `kernel:path=/sys/fs/kdbus/5001-user/bus`. Missing `/run/user/5001/bus` or an SDK-identity subprocess failure therefore did not establish that the installed package could not reach its owner manager. Fixed read-only `org.freedesktop.DBus.Properties.Get` requests to `org.freedesktop.systemd1` at `/org/freedesktop/systemd1` read Manager `UnitPath` (`as`), `SystemState` (`s`) and `FinishTimestampMonotonic` (`t`), plus selected unit properties.

The compiled path builder supports `HOME/.config/systemd/user` and XDG config/data equivalents; `lookup_paths_reduce` prunes currently nonexistent directories from `UnitPath`. A package marker established access to a config parent, not unit loading, early cold dispatch or authority to repair stock services. If investigating paths, collect only the required HOME/XDG/SYSTEMD_UNIT_PATH variables, never dump the full environment. Static anchors: `libsystemd.so.0` SHA256 `b477d14d18d7e84cc0b97bf5539fd7bcffed9e2fec0a19d17197ffcd3bc6f918`; `libsystemd-shared-244.so` SHA256 `9aeacb10c444f948dc7e57a9465f1f8d7bdd5ad9fd57a0fdd541764b85c61a53`. No public boot-unit installer is supplied.

### Current bounded candidate and its failure guard

[`FrameStartup.cs`](../../native/FrameStartup.cs) is an ordinary `ServiceApplication` candidate with no polling daemon or route writes. It checks:

- UID/EUID 5001 and its exact owned package SMACK label on the inspected device;
- valid per-boot identity and uptime no greater than 120 seconds;
- no earlier attempt for that boot;
- up to 50 seconds of readiness, within a 60-second process watchdog;
- four stable 500 ms samples of power 0, ambient 0, tuner full app, no partial or stock CSFS partial, and known AirPlay state that is not running.

It preserves native Art, exits if Home already exists, and does not steal focus from playback, setup, Settings, Apps or AirPlay. It marks the attempted boot **before** launching to prevent crash loops. These hardcoded identity/policy assumptions require adaptation and validation on other systems.

A retained later startup journal records **readiness timeout before any Home launch attempt**: power 2, empty full identity and transient stock partial never met its guard. This is not a fresh October 22 cold test. An owned user-manager unit had also been activated historically and its inverse rehearsed; that does not establish successful automatic custom Home. The public manifest leaves `on-boot=false`, and the public setup does not install a universal boot unit or patch installer policy.

An earlier aggregate `File.Exists` readiness predicate stayed false while a separate SDK snapshot showed its markers present. That did not isolate a failed path or prove a MAC denial because caller identities/times differed. Separate getter outcomes superseded that predicate; the historical enable-cold-start prototype is not current startup logic.

A meaningful new acceptance test needs a collector prepared before unplugging, reconnect-tolerant developer reads, monotonic startup/state samples and an observer watching the first visible screen. Manual launch must be identified separately. Normal cold startup, warm standby, native Art wake, motion wake, HDMI-CEC and network/AirPlay recovery remain distinct cases.

## PLATFORM-PINS — authority, projections and the synchronization blocker

**Static + Runtime + Physical.** Samsung's stock live app list and alternating app-layer cache own its ordering. The readable vconf value `db/eden/launcher/list` is a **published projection**, not an authoritative live list under every route.

The recovered stock producer `NotifyComponent` checks cached `FSSystemProperty.IsEdenProfile`: it compares the configured first-screen identity to the stock process identity. Assigning first-screen to our helper makes that publication guard false. Thus showing/resuming Samsung's bar does not necessarily refresh the readable projection. The sampled cache had a later, larger ordered list while vconf retained the older subset; this is a measured divergence, not a UI-scroll bug.

The alternating files `.appLayerCache.1` and `.appLayerCache.2` contain a generation and a complete ordered component serialization ending with `EOFCOBA`. Select a complete valid generation, not simply the newest modification time, in any future authorized reader. These files live in **CSFS's private data directory**. The ordinary custom package could traverse the shared Home ancestors, but access to the CSFS directory/slots failed with `UnauthorizedAccessException`. Their package SMACK label had no matching allow rule for our package. A slot's permissive Unix mode did not override that MAC boundary. SDK-shell readability must not be confused with installed-app access.

Read-only WAS metadata at `/opt/usr/apps/webappservice/wasdb_backup/.webappservice.db`, `widgetinfo(tizen_id,id,icon_path)`, maps launch IDs to store IDs and default icon paths. It is catalogue metadata, **not pin authority**, and the backup can lag installations. `FrameOverlayApp.ReadStoreApps` opens it read-only and validates accessible icons.

**What the current source actually does:** `get-order` returns the project's own `pinned-apps.json` order plus installed catalogue. `PinFromStore` recognizes Samsung Store's first-screen AppControl carrying an `appId`/store ID, resolves it to an installed app and appends it to that owned list. It does **not** currently forward the addition to CSFS or provide two-way stock synchronization. An earlier `appId=<store-ID>` forward with `__NO_RAISE_WINDOW=enable` committed stock additions, but restoring that alone would not mirror subsequent stock Move/Remove.

The shipped dispatcher inspection found no general external ordered-list query, unpin or reorder endpoint in its bounded scope. This is not proof no firmware IPC can ever provide one. Do not implement a second independently editable order while claiming synchronization.

The working stock management path was physically confirmed:

1. Open Samsung UI.
2. Highlight the app in its horizontal bar.
3. Press **Down**.
4. Choose **Move** or **Remove**.

Holding OK was an incorrect instruction for this TV; it immediately launched the app. Move/Remove currently affect stock state, not reliably the custom list.

**Proposed next avenue:** preserve Samsung's first-screen identity, while separately routing custom Home/startup/Settings/Art returns through owned supported paths. That is a routing redesign with regression requirements, not a pin-only patch. Reading stock private cache through new MAC permission, caller spoofing, global file permission changes or a privileged daemon is not implemented or recommended here. Bidirectional synchronization remains unfinished.

## PLATFORM-CAPTURE — overlay-only screenshots

**Runtime.** An installed ordinary native probe invoked:

```csharp
Capture.Start(Window.Instance.GetDefaultLayer(),
    new Size(1920, 1080), ownedOutputPath, Color.Transparent);
```

It produced a 1920×1080 RGBA PNG containing the native sidebar, label and clock. Pixels in the underlying web Home region were transparent; its artwork/text were absent. This captures the **owned NUI layer**, not the whole compositor. It is useful for native alignment/icon review and does not prove Home is black or missing. Full-screen composition and protected streaming content were not captured or validated.

A separate native screenshot library was located statically, but full-TV capture is unresolved. A restricted compositor utility returned permission denied under SDK identity; permissions were not changed. Phone photographs remain the established way to review the complete TV image.

## PLATFORM-EVIDENCE — source identities and reconciliation

The following SHA-256 identities bind static conclusions to the inspected firmware. No vendor files, decompilation dumps, personal database snapshots or session identifiers are shipped with this handbook.

| Evidence ID | Artifact / selected member | Basis and limits |
|---|---|---|
| PLATFORM-CORE | `appctx-config.json`, SHA256 `6a2f6c1953660eacbe7653cf38e566204312986c9a359d10d745972b1f3c7848` | Static policy; runtime state can differ after registration/caching. |
| PLATFORM-LIFECYCLE | `ImplNUI.dll`, SHA256 `0868d04c47528ee81d1b2f5ad6407c87c2cf1381d79eb94a936fb265b2a5b24d`; `_OnPause` token `0x060004a0`, `_OnResume` `0x0600049a`, `OnHideFinished` `0x060004b9` | Static lifecycle plus independent installed pause/physical playback observations. |
| PLATFORM-ROUTES | `app-boot-manager`, SHA256 `888d70f79f9b31f7f0db074ee1f65d46d5598ba83b0f984a6564eb5cd6180568`; `mgr_util_get_firstscreen_appid` ELF `0x13ba4` | Producer forwards configured ID; no claim of end-to-end acceptance for arbitrary apps. |
| PLATFORM-ROUTES | `tvctx-manager`, SHA256 `9c9a61a9a98e36bdc6f275e4afc57d08f61d5bdbe184b8acfaba1f8ce3cf097f`; `context-common.so`, SHA256 `3bc643f0cd35e4c73541018560fac2cbb50c63cf5d2088d48929ce81747c1905` | Cached first-screen/task semantics; source-specific numeric addresses are not reusable ABI offsets. |
| PLATFORM-STARTUP | `libtpk-installer.so`, SHA256 `c6872b4641e2fbd4f17b376ab667a2fde06e5346ddbc555f5c6cee88fd18e781`; `StepManifestAdjustment::process` ELF `0xc970` | Static certificate-level on-boot restriction plus input/processed manifest comparison. |
| PLATFORM-STARTUP | `libapp-installers.so.1.33.0`, SHA256 `e6bfcbf4f40e300a822a1d48f8081ba5b39e610845034c1674d05bd32ded499f`; `StepGetPrivilegeLevel::process` ELF `0x567d8` | Signature-derived enum 0 UNTRUSTED / 1 PUBLIC / 2 PARTNER / 3 PLATFORM. No bypass supplied. |
| PLATFORM-STARTUP | `libpkgmgr_parser.so.0`, SHA256 `7b990f74e71116008e8528230b1916938e69a23a1f356632d2f7077d55620f94` | Parser preserves processed on-boot field; installer adjusts earlier. |
| PLATFORM-STARTUP | `libamd.so.1`, SHA256 `fef118cec497ea3dd2a1a87d8fda8189e3539b406db7cdc3614f7c11a868d027` | Boot callback/state8→on-boot loader/queue; runtime timing supports fallback, not live syscall proof. |
| PLATFORM-PINS | `Tizen.TV.Service.CoBA.Core.dll`, SHA256 `817b4b38a8083b788a18433063e9fcbd93c9b36526c14474dade6903a2c4301f`; `NotifyComponent` / `FSSystemProperty.IsEdenProfile` | Static projection gate plus sampled cache/projection divergence and ordinary-identity access denial. |
| PLATFORM-CAPTURE | `Tizen.NUI.dll`, SHA256 `afcd8d277f96318f9820083e354d1bf77e43fd5cdfc956e523b13b32903d7188` | Capture API inventory, then installed runtime image with native-only content. |

Early reports used “research only” or “not yet implemented” because they preceded the later routing slice. The later physical Home/Back/Settings/Apps/Art-return observations supersede those narrow status statements, not the recovered mechanisms. Conversely, later successful manual Home does not supersede failed cold startup; photo selection does not prove motion wake; a local simulator does not prove native geometry. The latest Art-exit flicker, cursor-transition visual delay, cold startup, pin synchronization, motion wake and deliberate disconnected/network-recovery acceptance remain open.
