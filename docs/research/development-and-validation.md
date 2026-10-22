# Building on the findings

This guide turns the research into a development workflow. It separates an ordinary app change from firmware integration and from physical acceptance. See the [status ledger](evidence-and-status.md) before assuming the reference TV or public code already supplies a feature.

## Reproducing the reverse-engineering method

Identify the exact artifact/version before tracing a feature. Use a managed-assembly inspector for PE/CLI methods/resources and a native disassembler for ARM/Thumb ELF symbols/control flow. [REA](https://github.com/morluto/rea) was used to organize target/member evidence; it is an optional research tool, not a Reframe build or runtime dependency. An exported method name is a starting point, not proof of authorization or behavior.

Trace each feature from entry point (key, AppControl, channel message or configuration producer) through its state/permission checks to the consumer, native call and cleanup. Inspect matching packaged configuration/resources alongside code. Record artifact/member identity and distinguish an actual source signature from an inferred decompiler type or process-specific address.

Then choose the smallest discriminating runtime test under the intended installed identity. Keep connection, command acknowledgement, state result and physical output as separate observations. Do not repeat a failed probe unchanged, infer a missing library from one Boolean readiness check, or treat a successful developer/root experiment as an ordinary-app API. Keep raw evidence private; publish a synthetic contract, source identities, procedure, bounded result and remaining uncertainty.

## Put changes in the existing layer

| Responsibility | Public source |
| --- | --- |
| Full Home interaction, focus, content rows, request deadlines, suspend/resume and idle eligibility | [`app/app.js`](../../app/app.js) |
| Actual Samsung web launch/request/key/lifecycle boundary | [`app/tv.js`](../../app/tv.js) |
| Fake external screens and asynchronous local-device replies | [`dev/simulator.js`](../../dev/simulator.js) |
| Shared dimensions, scale factors, insets, card and brightness geometry | [`app/ui-layout.json`](../../app/ui-layout.json) |
| Web visual treatment and transitions | [`app/styles.css`](../../app/styles.css) |
| Transparent native partial-window lifecycle, input, app catalogue and IPC | [`native/FrameOverlayApp.cs`](../../native/FrameOverlayApp.cs) |
| Provider reads, private actions/images, validation and activation | [`native/FrameFeeds.cs`](../../native/FrameFeeds.cs) |
| Normal-profile backlight and Auto transactions | [`native/FrameBrightness.cs`](../../native/FrameBrightness.cs) |
| Bounded cold-start readiness candidate | [`native/FrameStartup.cs`](../../native/FrameStartup.cs) |
| Assembly, asset hashes, separate production/preview outputs, signing and verified pair deployment | [`tools/`](../../tools/) |

The preview and TV use the same web controller, not separate implementations of Home. The playback overlay is a different native renderer because a normal web app does not provide the proven partial-window behavior. Shared geometry/assets reduce drift; they do not make CSS and NUI the same component. Reproduce focus, shape and clipping checks in both after relevant visual changes.

A new device operation needs an explicit contract at the existing request boundary: allowed sender, action, parameters, state preconditions, reply/error, deadline and cancellation limitations. Implement hardware behavior in the native domain owner; implement only a deterministic fake response in the simulator. Keep provider payloads, account state and hardware calls out of rendering code. A mock result must not bypass the normal confirmation or error path.

The prototype native UI→same-package service relay proved an earlier transport option. Current production handles its bounded requests in the native UI entry; do not add another service merely to match a historical diagram.

## Lifecycle and remote behavior

Normal accepted reference interaction is:

```text
Full Home → launch existing app
Existing app + Home → native sidebar over the still-running app
Sidebar + Back → original app regains input
Sidebar + Home → full custom Home with its selected app ID
Settings opened from Home + Back → Home, without another sidebar
Samsung UI option → stock partial bar over current screen; Back dismisses it
```

The first Home trigger and Settings/Art returns depend on firmware routing configured separately on the reference TV. Implementing those handlers inside an app does not register global routing. Public `frameFocus` handoff is accepted only from the owned helper, not from arbitrary external senders.

Application launch acknowledgement and visible foreground change are different events. A return can precede the SDK launch reply; it must not be discarded. A late failure must not reactivate a hidden screen. Repeated focus/visibility notifications should be idempotent. Timeouts stop our wait, not the Samsung operation itself.

Before switching apps, stop scheduling new artwork requests, drain already-started owned requests, then launch and confirm or recover the UI on failure. Suspending Home must stop idle timers, delayed repaint/report work and inappropriate input intent. Do not pause a newly opened Samsung partial window during cleanup of our own request.

Remote input belongs to the currently focused layer. Consume both press/release edges of our dismissal keys, then pause only our partial surface after release. Preserve Power, volume/mute and underlying media controls. Ordinary H/Backspace/Delete/R simulation checks sequencing only; it does not prove a physical Home grab, manager priority or channel-release event.

## Geometry, image quality and motion

The application stage is 1920×1080 and scales to the panel. A 4K panel does not make this a native 4K rendering pipeline. Avoid promising sharper output merely by increasing asset dimensions.

Current app tiles are 256×192 (4:3), media artwork 16:9. Native icon masks must account for the original image size: use the source-aware mask-content scale rather than assuming every installed icon matches the destination rectangle. Animate the complete parent surface to keep the logo/text centred and scaling together; repeatedly resizing decoded image children caused extra rendering work.

Reserve the selected tile's grown height in list layout. Scroll one combined apps-plus-utilities list: anchor at the start/end and keep an intermediate focused item near the middle. Viewport padding must include focus growth; an inset on a clipped parent alone can cut off the top/bottom tile. The native viewport offset and inner horizontal inset together determine the screen edge, so changing one token without the other can create mismatched margins.

Content rows intentionally extend beneath the left sidebar/right controls and clip only at the TV frame. Do not introduce an internal rectangle that truncates the first/last cards at a decorative panel boundary. Labels need explicit beginning/left alignment in NUI; matching CSS alone does not establish that alignment on TV.

CSS cubic-bezier transitions and NUI `Animation`/`AlphaFunction` provided the current easing/overshoot. These are spring-like curves, not a velocity-preserving physics spring. Interrupted animations need stop/dispose ownership. The [API6 NUI Animation reference](https://samsung.github.io/TizenFX/API6/api/Tizen.NUI.Animation.html) documents the platform primitive; the repository’s compile/runtime results establish the tested subset.

## Performance: what the experiments actually showed

The reference TV felt slow on full-Home app selection after recommendations were introduced, while native overlay and utility navigation stayed fast. Fast handler execution did not rule out later compositor/paint stalls.

The retained timing experiments measured actual Home focus code and consecutive `requestAnimationFrame` callback gaps. They did **not** measure remote-to-photon latency, GPU time, sustained frame rate or another model. Conditions were sequential; warming/order/pending work remained confounders. A short two-callback observation missed later stalls, so later comparisons sampled six callbacks and then 40 callbacks around scene/image changes.

| Comparison | Retained bounded result | Engineering consequence |
| --- | --- | --- |
| Disable native reads alone | Large app/card stalls remained | Do not assume every hitch is network IPC. |
| Remove masks | Worse outliers in that run | No blanket mask-removal fix. |
| Decode images before mount | Large following-frame gaps remained despite decoded images | `Image.decode()` availability is not proof of a solution. |
| Limit painting to six cards / retain hidden DOM views | Did not consistently remove stalls | No production six-card cap or hidden-view framework was promoted. |
| Prepare whole media-card transform surface | Sampled YouTube gap max about28ms versus about1332ms baseline | Existing whole-card transform preparation was selected; no text-only or artwork-only redesign. |
| Cache gradient as a coloured bitmap and prepare the background surface |24 app selections had maximum consecutive gap about71ms versus roughly1455ms baseline | Keep the cached palette/background path; live radial recomputation/preparation alone was insufficient. |
| Prepare existing media boundary; avoid repeating unchanged row reports | Later40-callback study: late app gap max31ms, initial scene gap still about76ms | Keep work/reporting off the input path; do not claim all stutter solved. |

These are experimental comparisons, not performance guarantees. Later physical feedback said stutter was improved; selection continuity still has visible glitches/delay. Preserve this limitation when benchmarking new changes.

Useful implementation constraints from those experiments:

- Cache and bound feed models/images; preserve the last good model after a transient failure.
- Schedule overview changes outside the synchronous arrow handler; do not fetch, decode, reparse unchanged rows or rebuild the catalogue per key.
- Pause thumbnail work over playback; the native sidebar has no preview cards.
- Use the existing cached background palettes and prepared transform boundaries rather than layering speculative GPU hints everywhere.
- Keep rendering diagnostics numeric and bounded. Re-emitting the same report on every cached selection adds work without new evidence.
- Recheck asset bytes/cache hashes after a build; stale SVG/font URLs previously hid actual changes.

### Rejected MessagePort prototype

A same-certificate trusted-port experiment transferred metadata but failed thumbnail sends with `ArgumentOutOfRangeException`. It was not promoted. Empty-image timing is not a successful image-path test, and the run did not establish an exact universal byte limit. Samsung’s [MessagePort reference](https://developer.samsung.com/smarttv/develop/api-references/tizen-web-device-api-references/messageport-api.html) describes trust/message-size constraints; matching permissions and available bytes must still be measured on the target.

### Performance evidence identity

`PERF-CARD-14OCT`: repeated32-sample warmed card-focus comparisons, dedicated native-read gate; selected whole-card preparation, followed by normal-request production comparison. `PERF-APPS-14OCT`: six-frame app switching,24-selection final cached-background comparison. `PERF-LATE-14OCT`:40-callback feed-backed scene comparisons and initial-only row diagnostics. These are retained numeric research summaries, not public replay files. Personal feed fixtures, original reports and diagnostic packages are omitted. Reproduce with synthetic data first and a fresh bounded instrumented run before assigning a new device the same result.

### Measurement and address pitfalls

Use the target’s actual `CLK_TCK` to convert process start ticks; the inspected TV used 250, and an earlier 100 Hz conversion was wrong. A first managed log timestamp is not process creation or timer-arm time. Accept .NET timestamps with seven fractional digits and correlate fresh run/boot identity before rerunning a successful device probe merely because a local parser failed.

For static addresses distinguish nominal ELF addresses, Ghidra’s imported PIE base (a retained import added 0x10000), process addresses and ARM/Thumb decoding state. An initially misbased Settings export was rejected rather than used as evidence. Prefer matching artifact/member hashes and explicit address-space notes over a naked numeric offset.

## Repeatable validation without private artifacts

Do not install a historical test package or restore an old version just because it appears in the research. Stage a separately named temporary candidate from current reviewed source, keep a known installed pair, verify package identity and retain an inverse for each deliberate setting change. Never restart a Samsung core service to refresh metadata during a UI experiment.

Suggested acceptance record:

```json
{
  "case": "overlay-back-during-playback",
  "model_family": "LS03A",
  "firmware": "<reported firmware>",
  "architecture": "<observed architecture>",
  "web_version": "<installed version>",
  "native_version": "<installed version>",
  "artifact_sha256": "<candidate digest>",
  "evidence_kind": "physical",
  "precondition": "video visibly playing; sidebar closed",
  "actions": ["Home", "Left", "Right", "Back"],
  "observed": "<picture, audio, focus and input outcome>",
  "result": "unverified",
  "restoration": "<exact scoped result>"
}
```

No serial/MAC/account/profile name, photo ID or provider URL belongs in a public report. An API reply can be summarized by counts/types/correlation success rather than publishing its payload. Record protected-app behavior without bypassing its lock/profile/account checks.

### Prepare these batches before requesting a person

| Batch | Preconditions and actions | What establishes a pass |
| --- | --- | --- |
| Overlay/playback | Current pair and recorder ready; play YouTube, then Netflix; Home, arrows, Back, play/pause; second Home into full Home | Video/audio continue, input returns, no unwanted stock bar, selected pinned app/cursor retained. |
| Direct utilities | Open Settings/Apps from Home, then Back; deliberately visit Samsung UI and dismiss | Correct native screen and intended return destination; no duplicate sidebar on Home. |
| Selected content | Open two recognisable cards per provider, including when already running | Exact content, not just app launch; separately record Netflix episode/position/profile and locks. |
| Brightness | Save exact active-profile value/Auto state; rapid taps, hold/release, bounds; slider versus channel routing; Auto off/on; repeat over playback | No dropped intended state or continuing hold after release, no scan/Guide, correct setting/Auto and visible response. Restore originals. Public versus later private input revisions differ. |
| Art input/light | Existing native picture; overlay arrows/Back; change room light while overlay remains visible and after dismissal | Same native picture remains, input belongs to our surface, dim/bright behavior preserved. No photo/import deletion needed. |
| Art selection/import | Follow the separate [Art protocol prerequisites](art-mode.md); select existing content separately from import/cleanup | Correlated result and exact intended picture/library change; no unrelated changes; bounded fixture ownership and cleanup proved. |
| Cold startup | Recorder running before unplug; start on Home; unplug 10 s, reconnect, no keys 120 s; if absent, Home once and wait 15 s | Automatic arrival versus manual launch recorded separately; boot ID/readiness route captured. Current automatic result is not accepted. |
| Pins | Record names/order locally; stock Down→Move/Remove on one disposable pin; compare custom Home and overlay; restore | Same ordered list through add/move/remove. This currently fails the synchronization goal; a successful install refresh alone is not enough. |
| Art sleep/wake | Recorder prepared, exact motion/sleep baseline saved, explicit temporary interval only if necessary | Native sleep and specifically motion-caused wake distinguished from light/power/boot. Restore exact interval. |
| Standalone/recovery | Disconnect development computer; use Home/feeds/controls; separately test temporary TV network loss/restoration | No UI freeze; recovery without relay. Exclude Plex’s expected server dependency from Home/Netflix/YouTube conclusions. |
| Home idle | Observe five minutes on full Home; separate input-reset and >five-minute playback runs | Idle entry already observed; reset/playback exclusion need their own acceptance. |

A host-side recorder can reconnect through SDB and collect existing startup/overlay journals, foreground IDs and a scoped pin projection. It must not launch Home during a cold-start test: that would manufacture the expected result. Store the boot identifier privately so a warm state or an Art sleep/reboot cannot be mistaken for the intended cold boot. A recorder that dies during native sleep cannot prove the wake cause by its absence.

### Temporary diagnostics and restoration

Use a fresh run identity, expected mode/sample count, candidate source/package hashes and a completion marker. Reject a stale file even if its numbers look plausible. A successful install or launch message is not evidence that the intended code was loaded; reload suppression can keep an old process alive. Verify installed bytes and, if required, gracefully restart only the owned app.

Compile with matching device-era references rather than assuming the latest API exists. NUI image decoding/disposal succeeded only with the appropriate native UI-stage/thread context in the retained tests; a service-only decoder failure was not a blanket image-format failure. Keep native surface ownership around framework graphics objects until disposal completes.

Package installation is transactional in [`tools/deploy.py`](../../tools/deploy.py): build/sign first, verify the saved pair against installed bytes, update both apps, read back every non-signature file and confirm Home launch. On failure restore/verify the saved pair. That rollback does not restore external routing or unrelated TV settings; [Recovery](../RECOVERY.md) explains the boundary.

## Adjacent cloud-gaming investigation

A separate retained comparison examined official TV apps, browser wrappers, Better xCloud/TizenBrew and two PS5 clients. **No Xbox cloud playback, controller/game session or Samsung-native port was validated.** Its optional offline readiness widget was prepared but not installed; Gamepad/WebRTC/MIME availability would establish API hints, not playable video/audio/controller latency.

Useful reusable conclusions are narrower than a deployment recommendation:

- Check exact model/year, store region and service/account eligibility independently. An installable client does not establish cloud-service availability.
- Samsung’s [web-engine specifications](https://developer.samsung.com/smarttv/develop/specifications/web-engine-specifications.html) map2021 Tizen 6.0 TVs to Chromium M76. This constrains web features; it does not prove a cloud game is playable.
- The inspected PS5 projects were [PSBox](https://github.com/RafaelNGP/xbox-cloud-gaming-ps5/tree/e8a05232d41eb92f7ca04878e012f682bbd22d87) and [Nubix](https://github.com/JBuildr/nubix/tree/12bb423be1849065aed395b58ccbfa9c4a71f81a). At those retained revisions their inspected build targets did not supply a Tizen package. Protocol portability is separate from hardware decoding, audio, controllers, packaging and signing.
- The inspected [Better xCloud module revision](https://github.com/redphx/bx/blob/7c5604ca9c357e38b6284a85b0b57ec075f1edf1/package.json) is not proof of a 2021-compatible replacement streaming engine. Do not run third-party installers or change firmware/region merely to test the assumption.

Sources were inspected, not executed. Date-sensitive country/subscription/controller claims from the original private report are deliberately not republished as current advice; consult the official service and device support pages when selecting a real test. No account country, location, network endpoint, subscription or controller from the reference household is included here.
