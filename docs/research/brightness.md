# Brightness, Auto and channel-key research

Scope: **2021 LS03A Frame, Tizen 6.0/ARMv7, T-NKM2DEUC-2301.1**. Reconciled from retained static, runtime, source and physical evidence on 22 October 2026; no new TV tests were run for this publication. Native Art brightness is a separate system. These private interfaces must be validated on another firmware before use; a symbol being exported does not promise permission for an installed app.

The reusable backend is [`native/FrameBrightness.cs`](../../native/FrameBrightness.cs). The public source and a later privately installed input candidate are not identical; the distinction matters for channel controls and repeat behavior below.

## BRIGHTNESS-ABI — what the menu actually controls

**Static, later corroborated by runtime.** In the inspected `expert-settings.json`, menu item **`brightness`** resolves to `menu/picture/backlight`, provider `picture-firstdepth-provider`: it is the normal panel-backlight slider. Do not confuse that name with the video black-level setting, a 0–100 lower-level picture API, or Art brightness.

The tested menu range is **0–50**, with hardware increment 5 representing a 10% UI step. The provider's `menu/picture/backlight` enum 1 minimum branch stores 0; the AVOC setting 7 boundary is model-dependent 0–50 or 0–100. Only the inspected TV/menu corroborates the 0–50 contract used in this project. Do not infer a universal range from a wrapper's100 comparison or assume−1 is reset.

Recovered native declarations:

```csharp
[DllImport("libsettings-api.so")]
[return: MarshalAs(UnmanagedType.I1)]
static extern bool CSharpGetItemValue(string id,
    out IntPtr name, out IntPtr value);

[DllImport("libsettings-api.so")]
[return: MarshalAs(UnmanagedType.I1)]
static extern bool CSharpSetItemValue(string id, string value);

[DllImport("libsettings-api.so")]
[return: MarshalAs(UnmanagedType.I1)]
static extern bool CSharpGetItemEnable(string id,
    [MarshalAs(UnmanagedType.I1)] out bool enabled);

[DllImport("libavoc.so")]
static extern int avoc_get_backlight(int window, out int value, int saveType);
```

`CSharpGetItemValue` returns **two allocated strings**, display name then value; native code duplicates them with `strdup`. Release each distinct nonzero pointer with `libc:free` in `finally`. They are not caller-filled buffers. `CSharpGetItemDataList` instead takes a caller buffer. Its managed wrapper ignores the native success flag and returns true, so managed “success” is not proof of a slider range; the live slider data-list query returned false.

The managed `Tizen.TV.Service.Settings` wrapper checks internal platform privilege through Cynara. The successful native path under the tested ordinary identity is a separate runtime fact; declaring a public launch privilege does not grant that managed platform entitlement.

### Read-only diagnostic pattern

A small installed, ordinary-identity diagnostic can call `FrameBrightness.Read()` and `ReadAutomatic()` and report availability/value/status. The same methods run in the production helper. A developer-shell call proves only that shell identity, not the installed package's access. Capture mode/source and Art state alongside results; avoid writing merely to test a getter.

The settings API returns a **configured value**, not a measured optical level. A getter value remaining constant while the sensor changes actual panel output is not automatically a failed sensor.

## BRIGHTNESS-PROFILES — configured, volatile and default values

**Static + Runtime.** AVOC has current/default/volatile in-memory tables plus persistent profile rows. The recovered `avoc_get_backlight(window,out value,saveType)` interpretation is:

| `saveType` | Meaning supported by inspected service code | Limit |
|---|---|---|
|0 | Active/volatile cached view | Not independently established as measured panel luminance. |
|1 | Saved/configured current value | Cross-checked against Settings `brightness` before default restoration. |
|2 | Current profile's default | Must be validated in the same profile/context; not a universal 50%. |

`avocd_data_get_db_setting_value` selects different GHashTables, rather than reading SQLite for each call. Profile/source reloads populate those tables. A representative lookup combines profile/source-specific and fallback rows; visible picture-mode names alone do not identify the exact persisted row.

The inspected persistent schema is:

```sql
SETTING(
  setting_id INT NOT NULL,
  current_value INT NOT NULL,
  default_value INT NOT NULL DEFAULT -1,
  picture_mode INT NOT NULL DEFAULT 0,
  input_source INT NOT NULL DEFAULT 0
)
```

The unique key is `(setting_id,picture_mode,input_source)`. Setting 7 is `AVOCD_SETTING_BACKLIGHT`; 266 is calibration backlight and 315 shop-mode backlight. The path is `/opt/dbspace/.avoc.db`. This schema is included to explain behavior, **not as permission or a recommendation to write that database**.

An authorized read-only local backup can corroborate profile data without a setter:

```sql
SELECT setting_id, picture_mode, input_source, current_value, default_value
FROM SETTING WHERE setting_id=7
ORDER BY picture_mode, input_source;
```

A current-context setter can update caches/hardware and, under its profile/apply policy, persistence. It can insert a missing source/profile row. Matching old/new cached values can return before SQL. Therefore a database edit alone need not update the live display, and a successful hardware call alone need not prove every stored row changed.

## BRIGHTNESS-AUTO — correct default restoration and sensor control

**Source + Runtime + Physical within limits.** Automatic normal-picture control is Settings item **`brightness-optimization`**, provider `system-provider`, key `menu/system/eco_solution/eco_sensor`.0 is off, 1 is on. Its enable getter must also report available. Minimum-backlight, energy saving, intelligent/adaptive picture and Art controls are distinct settings.

The initial implementation only toggled Eco. Runtime showed Eco could already be on while the configured manual backlight stayed low: off/on confirmed the flag but did not restore the default or move the slider. That earlier design was insufficient for the intended Auto action.

The corrected `FrameBrightness.Update` transaction is:

1. Require normal picture mode: `device_power_get_ambient_mode()==0`.
2. Read current menu brightness and Auto availability/state.
3. For a manual target, require 0–50 and an exact `expected` match with current brightness; refuse stale changes.
4. For Auto-on, cross-check AVOC kind 1 against the menu value, read kind 2 default within 0–50, then re-read current brightness to detect a changed context/value.
5. Disable Eco, restore **only the current profile's backlight default** with the Settings setter, then enable Eco.
6. Confirm exact readback. On failure, while still in normal mode, attempt restoration of this transaction's original backlight and Auto flag; propagate failure rather than claiming success.
7. For manual input, explicitly disable Eco if on, then set/confirm the chosen backlight. Auto-off changes only the flag and holds the resulting configured level.

The backlight setter waits150 ms before its readback. This is a confirmation delay, not justification for delaying selection or dropping another key press. The public controller still has busy-drop behavior; later input work is documented separately below.

A bounded backend diagnostic observed a profile default different from its current manual value, then successfully exercised **Auto→manual→Auto→off** and restored the original level/flag. It used the actual source transaction and ordinary developer-shell identity. Later installed-package/file verification and owner confirmation established that the UI's corrected Auto action changed the slider and actual brightness. The owner has **not yet conclusively accepted normal-viewing response to changing room light**; that remains a separate physical check. Native Art adaptation has been confirmed independently and does not establish ordinary-viewing adaptation.

This restores neither a full picture preset nor a factory profile. It does not touch minimum-backlight, other profiles, calibration/shop settings or Art. The returned level is still a configured setting, not a promise to track sensor-adjusted output in the slider.

### Owned AppControl contract

The public native helper uses the default operation, with allowed Home/helper sender identities checked from the OS-reported caller. Illustrative requests from the **owned Home**, all string extra-data values:

```json
{"action":"get-brightness"}
{"action":"set-brightness","value":"30","expected":"25"}
{"action":"set-auto-brightness","automatic":"1"}
{"action":"set-auto-brightness","automatic":"0"}
```

Success replies include `brightness` on the 0–50 scale and `automatic` 0/1 or unavailable −1. Invalid/stale requests fail. In Art, the helper returns the explicit “Art Mode controls its own brightness” result instead of writing. Enabling Auto does not accept an arbitrary user-supplied factory default.

Do not expose this hardware IPC to arbitrary app senders or trust a caller-provided `entry_point` as authentication. A timeout only stops the requester waiting; it cannot guarantee cancellation of a firmware operation already in progress.

## BRIGHTNESS-CHANNEL — key mappings and the sender boundary

**Static + Runtime + Physical, different scopes.** The remote rocker has Up, Down and an **inward press**. That inward press can invoke Guide/channel list/scan; suppressing Up/Down alone does not suppress it.

The visible native handler recognizes:

| Intent | Observed/handled native names |
|---|---|
| Brighter | `XF86RaiseChannel`, `XF86ChannelUp`, `ChannelUp` |
| Dimmer | `XF86LowerChannel`, `XF86ChannelDown`, `ChannelDown` |
| Inward/guide/list/previous | `XF86ChannelGuide`, `Guide`, `XF86ChannelList`, `ChannelList`, `XF86PreviousChannel`, `PreviousChannel` |

The web Home registers supported TV key codes through its device adapter, and consumes both keydown and keyup for channel-related actions. Only Up/Down changes brightness; guide/list/previous is consumed while our UI owns input. The documented web codes are Guide458, ChannelList10073, PreviousChannel10190, ChannelUp427 and ChannelDown428; these do not replace inspecting the actual key phases of a held inward press. Volume/mute, playback and native Power/Art remain outside that brightness mapping. Physical channel scan no longer appeared in the latest owner observation; that is not exhaustive acceptance of every short/long/context combination.

An earlier model-specific manager registration added channel launch routes constrained to the four default app IDs, excluding monopoly/halt/picture-off contexts, and later owned-partial cases. A conceptual row is:

```json
{
  "key":"XF86RaiseChannel",
  "source_selector":"package:org.tizen.netflix-app,~context:monopoly,~context:halt,~context:picture_off",
  "action":"launch",
  "target_selector":"package:org.framehome.overlayprobe",
  "args":"entry_point:brightness_key brightness_direction:up"
}
```

This describes a retained route, not a portable mutation recipe. A different foreground identity (such as native Cobalt YouTube) can miss a wrapper-only selector. The receiver also checks normal/Art mode and appropriate foreground/partial context before a background write.

### Why another key launch rule is not an exclusive scan blocker

The inspected stock policy can launch `name:operation.channel-list` when `ch_list_blocked` is absent; a launch listener then brings up `tv-viewer`. This explains how an inward channel action can reach the tuner/scan surface. Stock EPG has a separate media-app block, but that does not prove every wrapper/native player sets the same context.

A `key_filter` callback can be followed by the next registered receiver in the inspected key path. Another launch handler is therefore not evidence of consuming Guide/list globally. Static `context-common.so` tracing also found a task-launch blocker checked before state update/launch listeners. A proposed, unexecuted design would block only EPG/channel-list operations in verified custom/media contexts, exclude running Art and CSFS, and preserve explicit stock visits. It would also suppress direct guide/list entry points in those contexts, so that broader effect must be intentional.

The `with-package` selector scans any task in Run state, not just the current fullscreen identity; pause is a different state. Native Cobalt versus its wrapper adds another identity ambiguity. The saved task-filter table had no exercised blocker entries, and no authorized ordinary-app writer or runtime loader proof was established. Thus this is a static research avenue, **not a deployed scan fix or a ready SQL patch**. Verify scope, true running IDs, row loading and an exact inverse before any deliberate policy change. The original contexts/digests are identified in the [platform chapter](platform-and-routing.md#platform-evidence--source-identities-and-reconciliation).

### Public source deliberately rejects the unvalidated external sender

The **public source** accepts hardware `action` IPC and `entry_point=brightness_key` only from OS-reported `Fr4meHome1.Launcher` or `org.framehome.overlayprobe`. A non-owned sender is refused, replying failure where requested, **without hiding or pausing** the current sidebar. Thus captured channel keys inside our own focused UI are supported, but externally routed closed-sidebar brightness is **not advertised as working in this public build**.

Two retained background channel requests over Netflix reported caller **`tvctx-manager`**. That is useful runtime identity evidence, **not completed authorization validation**. It does not establish which manager callers/routes are trusted, whether a malicious package can reach that context, release semantics, or that allowlisting that name alone is safe. The older privately installed build intentionally excluded this hardening edit to preserve the already-tested channel path. Do not silently remove the public guard or present the private installed behavior as public-source acceptance.

## BRIGHTNESS-INPUT — dropped taps and the later private candidate

**Source + software verification; real-remote acceptance pending.** The public Home/native controllers use a busy flag while a checked brightness write is running. Manual requests arriving during that window can be dropped. The owner observed single-step holds and unresponsive rapid taps in both the slider and channel rocker.

A later **privately installed Home 0.3.54/native 0.4.43 candidate**, not this public source baseline, retains the latest desired manual target, paints it immediately, serializes checked writes, prevents stale readbacks rewinding the UI, and retains rapid Auto intent. Failure clears pending work rather than issuing blind writes.

For raw web/NUI key down/up it owns bounded hold timing: first repeat after 500 ms,350 ms intervals initially,150 ms after 1.4 s; release/focus exit/hidden/pause/failure cancel; a 12-second cutoff bounds a lost release. Software stand-ins and browser hold behavior passed, and the installed candidate's files matched its built packages. **This does not prove actual remote key-edge delivery.** Manager routes carry direction-only AppControl, with no reliable release event established, so an indefinite background hold timer was not added. The physical channel rocker might arrive through that route even when the visible overlay is involved.

This distinction prevents two unsafe conclusions: not every delivered launch implies a fresh physical press, and a direction-only launch cannot safely start a timer that assumes a later release will arrive. A future public input fix should preserve immediate desired-state feedback while serializing hardware confirmation, and verify both raw keys and manager delivery before claiming acceleration works globally.

## BRIGHTNESS-NIGHT — historical bulk dimming and complete restoration

**Static + Runtime, historical maintenance only.** A temporary overnight request led to investigation of `avoc_set_lamp_schedule_backlight`. The name sounds like a temporary clamp, but the recovered implementation does a **bulk normal-profile write plus a hardware update**:

`EPTizenPicture::SetBacklightToAll` → `avoc_set_lamp_schedule_backlight` → AVOC D-Bus `SetLampScheduleBacklight` → service setting 7/cache update → every setting 7 `current_value` row → engine backlight.

The D-Bus service/object/interface are `org.tizen.tv.avoc`, `/org/tizen/tv/avoc`, `org.tizen.tv.avoc.AvOutputControl`. The selected bulk helper has no saved-originals store, retained override flag or automatic expiry. It changes current values, not defaults, and has no picture-mode/source filter. A database-write failure can still be followed by a hardware call, so a success status alone does not prove every profile changed. Later profile/context updates can replace a direct one-off hardware value.

The historical experiment saved each normal profile's distinct original and verified that only the intended backlight-current fields changed. A separate dark pixel curtain was a visual aid, **not proof of minimum panel backlight**. Normal brightness was subsequently restored at the owner's request: all saved normal-profile values and the active cached getter were verified; temporary dimmer/guard was stopped and its appended UI curtain removed. **It is not active project functionality.**

The reusable lesson is recovery ordering: preserve exact keys/defaults/original values *before* modifying; stop the owned guard; restore the active context's cache/hardware through the native setter while context is stable; restore each saved row by exact key as the final persistence operation; then verify all originals, unrelated settings and integrity. Restoring only SQLite leaves caches stale; applying one common value afterward can flatten distinct restored profiles. These observations do not establish a universal safe cache-reload method, a permanent low-brightness ceiling across Art/calibration/shop modes, or a generic database-write procedure for other TVs. No root helper, SQL mutation script or signing-policy bypass is provided here.

## BRIGHTNESS-EVIDENCE — artifact and verification index

| Evidence ID | Artifact / member identity | Evidence and limit |
|---|---|---|
| BRIGHTNESS-ABI | `Tizen.TV.Service.Settings.dll`, SHA256 `76f84e498ca0ac3b846c534f7f8064af325aee5974ddb0fd1e99e04949f72d89`; GetItemValue token `0x06000024`, SetItemValue `0x06000023`, GetItemDataList `0x06000020` | Static argument ownership/order and misleading list wrapper return. |
| BRIGHTNESS-ABI | `libsettings-api.so`, SHA256 `c51cfcd7ef5a3790963bae57fab4780559f77305814c80a3c0926edeadc7471b` | Native getter duplicates outputs with `strdup`; native Boolean ABI corroborated. |
| BRIGHTNESS-ABI | `libpicture-firstdepth-provider.so`, SHA256 `05055d2e8f62be24f2554f6754fab077087b684c2836a1ccded4458f7afac31e`; backlight-minimum branch Thumb `0x4318` | Static zero minimum; runtime menu scale separately validated on tested TV. |
| BRIGHTNESS-PROFILES | `libavoc.so`, SHA256 `492d50ee9c830b4cebabba0e8cbaf8db14beac221099bab2502d3d7f1327f593`; `avoc_get_backlight` Thumb `0x465b47bc` | Current/default native boundary and successful bounded getter comparison. |
| BRIGHTNESS-PROFILES / BRIGHTNESS-NIGHT | `avocd`, SHA256 `667eb879ebbf9314d76da11100b72d928c329e26ba905295f3ce1799ecf50eba`; cache getter `0x84d28`, bulk helper `0xb9e8c` | Static cache/persistence behavior; historical apply/restore independently compared. |
| BRIGHTNESS-NIGHT | `libep-system-api-picture.so`, SHA256 `1bbc7575e9b0d15ba048c2e03a73f47af0b8bfd805fa7ba144bc703a1eba57a8`; `SetBacklightToAll` Thumb `0xa6b8` | Bulk API is not a retained temporary clamp. |
| BRIGHTNESS-AUTO | Current `FrameBrightness.Update`, retained active-profile default probe and installed 0.3.51/0.4.40 acceptance | Backend read/write/default restoration and owner-visible Auto pass; room-light response in ordinary viewing remains tentative. |
| BRIGHTNESS-CHANNEL | Retained manager route journal, focused key handling, later count/state-only caller observations | Scan suppression observed; closed-sidebar public sender authorization remains deliberately unresolved. |
| BRIGHTNESS-INPUT | Later private0.3.54/0.4.43 input source and 18 software cases | Candidate implementation/software pass; not public code or real-remote hold acceptance. |

Artifact addresses are included only to identify the selected inspected function; mixed ARM/Thumb requires correct decoding and the matching binary. They are not offsets to reuse blindly after firmware updates.

### What still needs physical acceptance

- Ordinary-viewing Auto adjustment after actual room-light change, separate from native Art.
- Minimum/maximum and rapid/held changes, including raw-key and manager-routed delivery, without late writes after release or context switch.
- An exact-video YouTube foreground with its different ID, and non-default pinned apps.
- Channel inward short/long press and closed-sidebar playback after public sender validation.
- Failure/context-change handling while a confirmation is pending; no writes to Art.

The owner already confirmed the corrected Auto action and personal artwork selection, so those narrow successes should not be relabeled untested merely because a different sensor/input edge case remains open.
