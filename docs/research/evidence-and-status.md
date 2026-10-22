# Evidence and current status

This is the authoritative reconciliation for the research handbook, dated22 October2026. The latest retained physical feedback is from15 October; no fresh device or sensor test was run for publication. Labels are defined in the [handbook index](README.md#evidence-labels).

## Public source versus the reference installation

The public code accompanying this handbook is **Home 0.3.53 / native 0.4.42**, with an additional source-only IPC caller-authorization correction. The last readback-verified reference installation is **Home 0.3.54 / native 0.4.43**. That later private development revision queues brightness intent, adds accelerated raw-key holds and launch-timing fields, and deliberately retains the earlier caller contract. It is **not included by this documentation-only update**.

Consequently, physical reports about the reference installation do not prove that installing the current public source reproduces every behavior. In particular, public `brightness_key` IPC rejects non-owned senders. Existing event evidence identifies `tvctx-manager` for two background channel requests, but a narrowly scoped authorization change and its acceptance remain outstanding. Do not remove caller checks to make a route work.

Installing the two public apps supplies an ordinary launcher, not the reference TV's separately researched Home/Settings/Art routing. There is no portable routing installer, universal SQL patch or validated fresh-device setup for those changes. The package updater never applies them.

## Capability ledger

| Capability | Best retained evidence | Public implementation and limits |
| --- | --- | --- |
| Full Home and desktop simulation | Software plus physical use | Shared production web controller; simulator only replaces device responses. Preview is not TV acceptance. |
| Transparent native sidebar over video | Runtime + physical Netflix/YouTube | NUI partial window implemented. Video/audio continued; Back restored underlying control. Trigger routing is separate. |
| Home opens sidebar; second Home opens full Home | Physical on configured reference setup | Native dispatch/selection handoff implemented. Requires that setup's external routing; cold/extra-pin timing remains imperfect. |
| Repeated Home while on full Home stays steady | Physical | Reload suppression and foreground guard implemented. |
| Current-app selection/cursor handoff | Physical, with reported defects | Works functionally; visible glitches and slowness remain. First opening over an extra pin needs coverage. |
| Settings direct launch and return | Physical on configured setup | Shortcut payload and native launch exist. Return-policy changes are not installed by the public package updater. |
| Samsung Apps launch and return | Physical | Native store shortcut exists; actual store remains Samsung's app. |
| Deliberate Samsung UI visit | Physical | Opens Samsung's navigable partial bar over the current screen; Back hides it. It does not necessarily terminate playback or enter a persistent stock mode. |
| Automatic cold startup | Failed physical tests + runtime timeout | Bounded service candidate exists. A fresh retest is requested; no automatic-boot success is claimed. |
| Shared Samsung/custom pin order | Static + runtime barrier; incomplete | Public owned pin list is not authoritative Samsung synchronization. Stock Move/Remove works; custom reflection can remain stale. |
| Original installed app icons | Runtime + physical/UI iterations | Icon catalogue/masks implemented. A symbol or file path is not a right to redistribute brand artwork. |
| Native provider feed reads | Runtime + physical rows | Native ContentService path implemented. Direct package-cache reads were denied under the app identity. |
| Netflix selected-title opening | Physical | Guarded provider action implemented. Exact episode, playback position, profile/lock/age-state and all warm/cold variants remain unchecked. |
| YouTube exact selected video | Latest physical feedback passes | Older generic-launch/video-unavailable reports are superseded for the tested path. Both warm/cold variants and future provider versions are not blanket accepted. |
| Plex own-library data | Static + bounded runtime investigation | Only library `server://` actions are accepted as content; free streaming catalogue is not the target. Content depends on its own server. Exact-card activation is unavailable. |
| GeForce NOW and other provider previews | Limited inventory/schema evidence | No general personalized-feed/selected-game guarantee. Use only the supplied validated model; absent rows are not a failed launch. |
| Manual normal-picture brightness | Runtime + physical | Current profile0–50; UI10% stops. Art settings are separate. |
| Normal-picture Auto enable/disable/default restoration | Runtime + physical toggle/brightness confirmation | Current-profile default restored before Eco Sensor is enabled. Configured value is not measured panel output. |
| Normal-picture Auto response to room lighting | Tentative physical report | Still needs deliberate normal-viewing dark/light acceptance. Do not transfer Art results to this control. |
| Rapid/held brightness input | Later private software checks; physical pending | Public code still drops busy manual presses. Later reference update passed18 software cases; routed channel holds lack a verified release signal. |
| Channel-scan suppression | Physical in reported use | Broader inward/held/closed-sidebar contexts remain pending; public manager route is fail-closed. |
| Home idle-to-Art after five minutes | Physical idle-entry confirmation | Monotonic Home-only candidate implemented. Input-reset and playback exclusion pass software checks but still need explicit physical acceptance. |
| Art chooser and personal-photo display | Physical | Artwork launches Samsung's selection browser. This is not a completed custom gallery or proof of every recovered direct-selection request. |
| Standalone local Art current-selection/catalogue read | Runtime | Exact-peer loopback diagnostic passed under ordinary installed identity. Research contract is documented; no production custom Art client is shipped. |
| Standalone Art import and exact cleanup | Runtime, one disposable fixture | Native importer registered one returned ID; cleanup removed only that ID and restored baseline membership/selection. Output quality/rendering were not tested by that diagnostic. |
| Input overlay above native Art | Runtime + physical | Four arrows/Back captured; native picture stayed visible. Custom category/picture stepping is not implemented. |
| Native Art light adaptation with overlay | Physical | Dark/light response while persistent probe was visible and after dismissal. No claim about every overlay lifetime or firmware. |
| Native Art sleep and motion wake | Runtime sleep + inconclusive wake | Sleep/suspend occurred after a temporary five-minute interval. Return crossed a kernel boot change; wake reason was brightness-related. Motion-specific wake remains unproved. Original interval restored. |
| Leave Art into custom Home | Physical destination pass, later regression report | Home/short Power returns reached custom Home, but seconds of black screen/flicker were reported. Smooth return is unresolved. |
| Standalone without development computer | Architectural on-TV runtime + partial earlier observations | No Mac relay in product design. Explicit full latest-feature disconnection/network recovery acceptance remains pending; Plex retains its server dependency. |
| Full-TV screenshots | Limited runtime | Owned native overlay capture succeeded; it excluded the underlying app. Complete composed-screen capture remains unresolved. |
| Xbox cloud gaming | Source/device readiness comparison only | No Xbox playback route was validated; a readiness widget was prepared, not installed. See [adjacent investigations](development-and-validation.md#adjacent-cloud-gaming-investigation). |

## How historical conflicts were reconciled

| Earlier record | Reconciled conclusion |
| --- | --- |
| Web/native cache attempts gave security errors | Access denial is real for that identity/path; later native ContentService reads are a separate successful route, not proof that private files became readable. |
| Local Art connection succeeded but getters timed out | Transport connected, yet `to: host` echoed. Later discovery of the unique other host's client ID produced correlated replies; role/privilege was not changed. |
| Art API/selection inferred from decompilation | Static request shape is documented separately from the successful getter/import diagnostics and manual chooser/photo test. Those do not automatically prove a custom gallery client. |
| Native Art Home/Power returned Samsung | Later configured routing reached custom Home; the latest report adds delay/flicker. Current entry records retain both destination success and unresolved smoothness. |
| Home should toggle the overlay closed | Revised accepted behavior is first Home→sidebar, second Home→full Home. Back dismisses only the sidebar. |
| Samsung option should enter a whole stock homepage | Accepted simpler behavior is a stock-bar visit over the current background, dismissed with Back. Persistent Samsung mode was proposed, not implemented. |
| Hold OK edits Samsung pins | Incorrect for this reference remote/bar. Highlight app→Down→Move/Remove worked. Custom long-press reordering was not promoted as synchronized editing. |
| Dynamic catalogue or Add to Home means pins synchronize | Installation catalogue and owned pins are different. First-screen identity freezes Samsung's readable ordered projection; Move/Remove remains unsynchronized. |
| An app with `on-boot=true` will start | Manifest text is not registration or dispatch proof. Installer/certificate policy rewrote the field; later startup readiness timed out. Cold boot remains unresolved. |
| Native sleep/wake can preserve our process | A sleep/return test changed kernel boot ID. Reconnect/boot readiness and preservation of native Art must be considered independently. |
| Tiny JavaScript handler times prove smooth input | Later callbacks exposed long stalls outside the initial measurement window. Six-frame/longer sampling plus physical input were needed. |
| A Home dark overlay is sufficient night dimming | Playback replaced it with a brighter surface. Backend backlight is separate, profile-specific and must be restored exactly. No all-profile dimmer is a production feature. |
| Auto flag changing means sensor output changed | Profile setting, Auto flag and physical sensor-adjusted light output are distinct observations. |
| Latest private input fix is public | It is not. This release changes documentation only; source/installed differences remain explicit above. |

## Evidence and reproducibility

Each domain chapter names relevant interface/assembly/member identities, retained diagnostic kind and limitations. Source code links point to files present in this public tree. Original personal payloads and vendor binaries are not necessary to explain the contracts and are not published.

To extend a finding to another device, record its model/year, firmware, architecture, relevant artifact hashes, caller identity, app state and test procedure. Use a fresh correlation ID and a bounded observation window. Do not reuse a historical diagnostic package, a baseline database or an old image ID. Reproduce the specific claim before extending its scope.

No public test should be marked passed solely because the reference maintainer previously saw it. Keep separate fields for interface existence, installed-identity authorization, reply/result correctness, visible behavior, recovery and cross-device compatibility.
