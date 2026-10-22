# Native Art Mode integration

This chapter documents the reusable contracts recovered on a **2021 Samsung The Frame, QE55LS03AAUXXH, firmware T-NKM2DEUC-2301.1, Tizen 6.0**. They are observed firmware behavior, not a supported cross-model Samsung API. Art's copied mobile API version is **2.03**. Revalidate both permission and behavior on another firmware.

The useful boundary is a custom gallery and input layer which asks **Samsung's existing `org.tizen.art-app`** to render a selected photo. Native Art owns the image catalogue, conversion, thumbnails, display transitions, timers, sensors and power integration. A full-screen image in a web app does not inherit these behaviors. No Samsung binaries or decompiled implementation are distributed here.

This is a reconciliation of retained research and observations through **15 October 2026**, published on **22 October 2026**. No new TV tests were run for this documentation pass. An early note saying “pending” is superseded only by the specifically identified later result below.

## Evidence and current capability

**Static** means inspected shipped code; **runtime** means a bounded executed diagnostic with recorded identity and results; **physical** means an attended visible/input observation; **software** means local/simulated verification. A proposal is not an implemented or accepted feature.

| Capability | Evidence | Current conclusion |
| --- | --- | --- |
| TV-local current-selection and library reads | Runtime, ordinary installed native identity; [ART-READ](#art-read--standalone-channel-and-getters) | Verified for the two getters, using exact peer addressing. No Mac service or reused pairing token required. |
| Import a JPEG into native My Photos, then remove only the created item | Separate runtime mutation and cleanup; [ART-IMPORT](#art-import--native-upload-and-exact-cleanup) | One complete cycle verified. Not a production gallery/import feature or proof of every supported image format. |
| Launch a particular existing native photo from a custom gallery | Static contract; [ART-SELECT](#art-select--deterministic-photo-selection) | Recoverable interface. A later personal-photo chooser test passed, but that does not independently establish this custom-gallery direct-selection pathway. |
| Select an existing personal photo through the currently opened chooser | Physical, later owner observation; [ART-PHOTO](#art-photo--later-photo-selection-and-routing-results) | The expected photo displayed in native Art. Custom category stepping was not tested. |
| Transparent native partial overlay, four arrows and Back over Art | Runtime plus physical; [ART-INPUT](#art-input--overlay-and-remote-ownership) | Tested picture continuity and dismissal. Does not prove invisible interception or custom image/category navigation. |
| Ambient-light adaptation with the overlay closed and open | Physical; [ART-LIGHT](#art-light--native-sensors-and-power-state) | Brightening/dimming observed in both tested situations. Not a claim about every sensor or setting. |
| Five-minute motion-timeout sleep | Runtime; [ART-SLEEP](#art-sleep--sleep-proved-motion-wake-unresolved) | Standby/Art Suspend recorded after the temporary five-minute interval. |
| Motion-triggered wake | Inconclusive; [ART-SLEEP](#art-sleep--sleep-proved-motion-wake-unresolved) | Picture was active on return, but the recorded wake reason and changed boot identity do not prove motion caused it. |
| Home or short Power leaving Art returns to custom Home | Earlier physical pass, later latency issue; [ART-PHOTO](#art-photo--later-photo-selection-and-routing-results) | Destination worked. Later black screen for several seconds and occasional flicker remain unresolved. |
| Five-minute Home inactivity enters native Art | Software plus later physical observation; [ART-IDLE](#art-idle--home-inactivity-entry) | Entry observed. Physical input-reset and playback-exclusion checks remain separate. |
| Reconnection after a cold boot or deep sleep | Not accepted | Rediscover channel peers each connection; never assume helper-process or peer-ID persistence. |

## ART-READ — standalone channel and getters

### Connection and peer discovery

The verified installed native route uses ordinary `.NET ClientWebSocket` to:

```text
ws://127.0.0.1:8001/api/v2/channels/com.samsung.art-app?name=<URI-encoded-base64-client-name>
```

Use a truthful UTF-8 client label, base64-encode it, then URI-encode the query value. The copied managed SmartView SDK constructs the same local service/channel family, but its wrapper checks the internal platform privilege. Loading that SDK into an ordinary application is not the proved solution; the executed diagnostic used the normal WebSocket client directly.

1. Read `ms.channel.connect`; its `data` may be a JSON object or a JSON string. Obtain our own nonempty `id` and its `clients` array.
2. Select clients with `isHost=true`, a nonempty `id`, and an ID different from our own. Require **exactly one** such peer. Keep its ID in memory.
3. Wait for **`ms.channel.ready`**, not just the earlier connection greeting.
4. Address requests to that peer's **exact returned client ID**. Abort on zero or multiple candidates; do not guess, broadcast, reuse an old ID, or change broker-assigned roles.
5. Treat rejection, timeout, disconnect or malformed response as a failed/unknown read. Reconnect with fresh discovery, not a cached session.

Both our installed caller and Art were classified as hosts in the original runs. Sending to the special recipient `host` echoed requests without correlated Art replies. Adding a normal name or using the TV's verified self-LAN address did not fix that. Exact-peer addressing did. Those earlier timeouts were **recipient-routing failures in that experiment**, not established authorization denials. The normal [SmartView publish API](https://smartviewsdk.github.io/API-GUIDE/js-api/docs/api.html#channelpublishevent-message-target-payload) supports a connected client ID as recipient.

The getter report recorded UID **5001**, the installed package's unchanged SMACK identity and original manifest. It required no new privilege, role override, stored token or Mac runtime. This does not promise the same access on every firmware or for every Art command. Web Home's sandbox is a separate boundary; the proof is in the installed **native** process.

### Envelope, correlation and double JSON encoding

Construct the inner request, serialize it to a **string**, then serialize the outer envelope. For example, this is schematic JSON, with fresh identifiers represented by placeholders:

```json
{
  "method": "ms.channel.emit",
  "params": {
    "event": "art_app_request",
    "to": "<unique peer ID from this connection>",
    "data": "{\"request\":\"get_current_artwork\",\"id\":\"<fresh request ID>\",\"request_id\":\"<same fresh request ID>\"}"
  }
}
```

The inner `data` must be a JSON string on this firmware: `ArtMSFControl` casts it to `System.String` before parsing. Do not manually concatenate user data into the escaped string; serialize twice.

Art responses arrive as `d2d_service_message`. Decode the outer `data` if it is string-encoded, require the expected response event, and match `request_id` (or `id` when absent) to the request. Do not accept unsolicited broadcasts as the result of a request. `get_content_list` adds another decoding step: its **`content_list` field is itself a JSON-encoded array string** in the inspected firmware. A client may tolerate an already-decoded object/array only after validating the expected shape.

| Request | Response event / key fields | Meaning and limitations |
| --- | --- | --- |
| `get_current_artwork` | `current_artwork`; `content_id`, matte fields | Saved valid full-screen selection. It can produce a default returned choice if saved state is invalid. **Not proof that the panel is displaying Art now.** |
| `get_content_list` | `content_list`; string-encoded array with `content_id`, `category_id` | Category rows across Recent, My Photos, Favorites, Purchased and optional MyShelf. The inspected handler has no category filter. One photo can have multiple rows. |
| `get_artmode_status` | Art-mode status response | Handler maps Art UI status to `off`, `nav`, `on`. Used as a precondition in the import diagnostic. |
| `get_auto_rotation_status` | Saved slideshow/rotation status | Used to require automatic slideshow off before the diagnostic. `get_current_rotation` is panel orientation and is **not** this precondition. |

The first two getters were verified together, with correlated replies. The status/slideshow getters were subsequently used during the bounded import/cleanup diagnostic. The copied handler returns error **-10** for an empty total content list; that code alone is not an access-denied result.

Use bounded messages, totals and deadlines. The successful getter probe capped an individual message at 256 KiB, total received data at 512 KiB, 20 messages, a nine-second cancellation and a twelve-second process watchdog. These are diagnostic safeguards, not discovered server limits or recommended universal library-size limits. Choose explicit limits for a product and report truncation; do not silently present a partial catalogue as complete.

Catalogue responses can include private photo metadata. Samsung's own implementation logs response strings internally. Our diagnostic retained only allowlisted event/status summaries and kept identities/recovery metadata private. Avoid collecting broad system logs during personal-library operations.

## ART-SELECT — deterministic photo selection

The recovered AppControl target is `org.tizen.art-app`, using operation `http://tizen.org/appcontrol/operation/default` and these extras:

```text
mode       = fullscreen
requester  = mobile
contentid  = <existing content ID returned by Art>
categoryid = MY-C0002
```

`MY-C0002` is the native **My Photos** constant, not a custom category. Imported IDs have a `MY_F` plus digits shape, allocated by Art. Validate an ID against a fresh successful native catalogue and the matching category; a plausible string is insufficient. Never allocate native IDs yourself. The inspected launch code resolves the content and category, then creates request type **Full=3**. Invalid lookup can fall back to Samsung's browser.

This AppControl interface uses **`contentid` / `categoryid` without underscores**. The channel protocol uses `content_id` / `category_id`. Mixing the two produces a different request. `mobileMessage` is optional stock response data, not necessary for the proposed direct selection.

Equivalent channel intent:

```json
{
  "request": "select_image",
  "id": "<fresh request ID>",
  "request_id": "<same fresh request ID>",
  "content_id": "<existing content ID returned by Art>",
  "category_id": "MY-C0002",
  "show": true
}
```

Static tracing shows `show=true` calls the native launch-with-content path. **`show=false` still mutates state**: it saves the selected full-screen choice and emits `image_selected`; it is not a read-only probe. Supply the actual category rather than relying on the handler's default-category lookup. Neither exact custom-gallery pathway has been independently accepted merely because the existing chooser displays a photo successfully.

The `requester` parser maps `mobile` to its mobile requester enum and Eden to another enum; unrecognized strings fall back to UI. `requester=remote` appeared in an early proposal but is not a special remote authorization token. Prefer the recovered stock `mobile` shape; caller permission still applies.

### Two non-equivalent shortcuts

**Empty `mode=fullscreen` is stateful.** With no content/category it opens the latest photo from UI None, but exits from Browser/Full UI states. It is a toggle, not an idempotent “ensure Art on.” The project's idle entry uses it once only after normal-power, Art-off and foreground guards.

**`mode=image` with `imagepath` is FilePreview=2.** The code checks a local file and creates a temporary preview request. It is not a demonstrated durable My Photos import or persistent selection. Cross-package file readability and save behavior were not established by inspecting that path.

### Categories, mattes and image quality

Keep seasons, themes and navigation categories in the custom app's own data as lists of known native content IDs. Native photos remain in `MY-C0002` unless an independently verified native category API says otherwise. Do not write invented categories into Art's database.

The protocol carries `matte_id` and `portrait_matte_id`; the saved selection exposes matte metadata. The diagnostic used `none` for import and compared original selection/matte metadata during cleanup. It did **not** establish all accepted matte IDs, portrait/crop behavior, a matte-selection API or output-image quality. Query/validate the firmware's actual options before offering them. A source image's dimensions or hash need not survive Samsung's conversion unchanged.

## ART-IMPORT — native upload and exact cleanup

This operation changes the personal library. The completed proof imported **one known public-domain JPEG**, registered it through Samsung's own importer, identified the fresh ID, then removed **only that created ID** and verified the original library metadata and saved selection. The fixture was never selected for display. This proves one standalone add/remove cycle, not arbitrary format, size, orientation, malformed-input or storage-exhaustion behavior.

Static producer/consumer flow:

```text
send_image + conn_info on Art channel
  → Art content-sharing controller
  → d2d-provider receives disposable staging file
  → native importer allocates ID, converts image and creates thumbnails
  → My Photos records + correlated image_added
```

The importer can **delete its input staging file**, including some failure cases. Transfer a copy from your app's own data. Never supply the user's only original file or bypass the importer by copying files into Samsung's private photo directory/database.

### Negotiation and transfer framing

1. Connect/discover/wait using [ART-READ](#art-read--standalone-channel-and-getters).
2. Send one correlated `send_image` request to the exact peer.
3. Require matching `ready_to_use`; decode its `conn_info` string (or a validated object on compatible implementations).
4. Validate the returned endpoint and security mode, then send the framed header and exact image bytes over the negotiated D2D socket.
5. Keep the Art WebSocket open and require matching `image_added`. Persist its returned native ID privately before another action.

Request schema, before double serialization in the Art envelope:

```text
request:             "send_image"
id, request_id:      same fresh upload correlation ID
file_type:           "jpg" (normalize "jpeg" to "jpg")
file_size:           exact integer image byte count, JSON number
image_date:          "YYYY:MM:DD HH:MM:SS"
matte_id:            "none"
portrait_matte_id:   "none"
conn_info:
  d2d_mode:          "socket"
  connection_id:     fresh unsigned 32-bit integer, JSON number
  id:                same fresh upload correlation ID
```

The reply supplies `ip`, `port`, `key` and optionally `secured`. The executed diagnostic accepted only numeric loopback or the TV's **exact own IPv4 address verified on a local interface**, with a validated negotiated port. It did not follow arbitrary hosts or guess fallback ports. Its security mode was absent/non-TLS; unknown or TLS-required mode stopped without transfer. TLS behavior is present in the pinned clients but was not exercised in this installed diagnostic. Do not disable certificate validation to force a new case to pass.

Use the **fresh reply's** key as `secKey` in this ASCII-JSON transfer header:

```text
num:         0
total:       1
fileLength:  exact integer image byte count
fileName:    "image" (non-private basename)
fileType:    "jpg" or "png", matching the bytes
secKey:      fresh ready_to_use key
version:     "0.0.1"
```

Socket framing is:

```text
uint32 big-endian HEADER_BYTE_LENGTH | ASCII JSON header | EXACT IMAGE BYTES
```

Count encoded header bytes, not characters. Use complete-write semantics, explicit size/I/O/overall limits and one negotiated socket per upload. Keep the WebSocket for the completion response. Never log `key` or whole `conn_info`, and never replay a prior session after timeout. An uncertain result needs reconciliation, not an automatic second upload.

This framing comes from two independently retained, commit-pinned public client implementations, corroborated end to end by the installed diagnostic. The complete native CoSS/D2D parser and authorization enforcement were **not** recovered. The firmware reports Art API **2.03**; the upstream binary-WebSocket-upload special case for API **0.97** is not an appropriate guessed fallback.

### Exact cleanup and recovery

The verified diagnostic required Art UI and automatic slideshow off, saved the original library/current-selection metadata privately, transferred one known fixture with bounded deadlines, and required all of the following before cleanup:

- The returned ID has the native My Photos syntax and was absent from the complete baseline.
- Exactly one fresh My Photos ID appeared and it equals the returned ID.
- Every original ID/retained metadata field and the original saved selection remain unchanged.
- The created image is not selected, including a final check immediately before deletion.

Only then send:

```json
{
  "request": "delete_image_list",
  "id": "<fresh cleanup request ID>",
  "request_id": "<same fresh cleanup request ID>",
  "content_id_list": [{"content_id": "<only the ID created by this upload>"}]
}
```

Require correlated `image_list_deleted` identifying exactly that item. On the inspected firmware its acknowledgement’s `content_id_list` is itself JSON-string encoded; decode that layer, validate its shape, then require exactly the created ID. A completion event name alone is not sufficient. Re-read all category rows and saved selection; require baseline ID/metadata equality and absence of the created item everywhere. Deleting a selected image or emptying a selected slideshow category can reset full-screen state, which is why the selection guard matters.

Stop on concurrent library edits, timeout, unexpected selection change or ambiguous ID ownership. Keep a private recovery journal; do not delete “the last image,” everything absent from a baseline, by filename, by numeric ordering or by wildcard. Restoring membership/selection does not rewind Art's allocation counter or normal logs. Do not overwrite a whole database or decrement its counter to conceal expected bookkeeping.

## ART-INPUT — overlay and remote ownership

The stock full-screen key handler treats all four arrows and Back/Return as full-screen-exit inputs; Down can first open Quick Info. Thus a custom Left/Right photo step and Up/Down category step needs input ownership **before** stock Art receives those keys. Reacting after a stock arrow can already have run its browser/ambient transition.

A temporary ordinary native **partial overlay** was physically visible over Art. Its arrows and Back worked, the native photo stayed underneath, and Back left the photo visible. A later focused overlay also received/consumed physical Home Down/Up above Art. These are bounded successful overlay interactions, not invisible global interception or an accepted custom gallery.

In the successful focused probe, the NUI view’s `KeyEvent` callback returned **true** to consume the event. A window-level notification or accepted grab alone does not establish consumption. Consume both Down/Up of Back and release the owned Home grab before requesting the owned pause; otherwise stock input can leak through.

The focused Home test registered NUI Home **183** with Topmost and released it successfully. The delivered event's code was **71**, with symbolic name `XF86Home`. These belong to different key-code domains; do not interchange them. The earlier key-registration experiment's numeric 9 was Escape, not Back. Successful registration alone did not establish delivery: a hidden-window experiment recorded no Home events, while the focused visible overlay did.

Stock Art itself grabs broad keys with a separate FLUX API and grabs Home Shared; it releases some system keys including `XF86PowerOff`. Do not copy its global grab policy or assume those enums match NUI. Power, native ambient entry and sensor wake require their own state-aware paths.

`ArtFullScreenLayout.OnKeyEvent` resets the native off timer for certain non-exit keys through `ArtDataAPI.ResetOffTimer`. That timer lives **inside the Art process**. Loading a copied singleton into our process would not reset the original timer. Whether custom photo/category commands should affect timer behavior remains a product question requiring native observation; it is not solved by capturing arrow events.

Proposed gallery interaction: hold only the required arrows/activation/Back while the owned partial view is active; maintain categories in app-owned data; select a valid native photo explicitly. Release focus/grabs on dismissal and leave native renderer/power/sensors in charge. Empty categories should leave the current image unchanged. Actual within-category stepping, cross-category stepping, timer reset behavior and persistence across low-power transitions remain unvalidated.

## ART-LIGHT — native sensors and power state

The owner observed native Art brighten after turning room lights on and dim after turning them off, first with the bar dismissed and later **while the bar remained visible**. In the persistent-overlay trial each adjustment took roughly three seconds. One Back dismissed the overlay and kept Art. These later results supersede the early notes saying persistent-overlay light adaptation had not yet been tested.

This does not establish every sensor control's normal-app permission. Some read-only state observers ran through developer/external UID **901**, while standalone channel/overlay diagnostics used installed UID **5001**. A successful developer-shell getter is not proof that an ordinary installed app may call it.

Do not merge these numeric state domains:

| Domain | Values recovered from shipped definitions |
| --- | --- |
| Device power | Normal=0, PictureOff=1, Standby=2, Off=3, SuspendStandby=4 |
| Device ambient mode | Off=0, FullPower=1, LowPower=2, Suspend=3 |
| Device ambient type | FrameTV=4 |
| Screen-manager ambient screen state | None=0, Browser=1, FullPower=2, LowPower=3, Suspend=4 |
| Screen-manager termination reason | None/Paused=0, Itself=1, ByMultiviewKey=2 |
| Screen-manager lifestyle type | Frame=2; **not** ambient type FrameTV=4 |
| Lifestyle picture setting | Brightness=0 |

The shipped Screen binding imports these getter symbols from `libcapi-screen.so.0`:

```text
screen_manager_get_ambient_screen_state(out state)
screen_manager_get_ambientapp_terminate_reason(out reason)
screen_manager_get_auto_brightness_setting_value(out enabled, lifestyleType)
screen_manager_get_motion_sensor_setting_value(out value)
screen_manager_get_brightness_sensor_enable_value(out enabled)
screen_manager_get_picture_setting_value_by_type(lifestyleType, setting, out value)
```

The retained sensor diagnostic also used `screen_manager_get_ambient_offtimer_setting_value(ref int)` and `screen_manager_get_suspend_reason(ref int)`. Its motion interval enum was Off=0,5min=1,15min=2,30min=3,60min=4,120min=5,240min=6; Max=7 is a sentinel, not a selectable duration. The separate ambient off-timer enum was Off=0,1hour=1,2hours=2,3hours=3,4hours=4. These values are not interchangeable with power/ambient enums above. A temporary external diagnostic exercised `screen_manager_set_motion_sensor_setting_value(int)` and restored its exact original; ordinary installed permission for changing it is not established, and no production sensor setter is introduced here.

These are semantic signatures, **not ready-to-copy P/Invoke declarations**. Match the inspected binding's ABI, including Boolean width, before calling. Record return code **and** untouched output sentinel. The inspected termination-reason implementation can return zero even when its internal configuration read fails; a default-initialized zero would incorrectly look like Paused. Treat failed/denied/missing reads as unknown; never “initialize” with a setter.

Normal viewing's profile backlight/brightness and Eco optimization are distinct from native Art brightness and its light adaptation. The normal 0–50 backlight setter is not an Art brightness API. Do not apply it during Art to simulate native dimming or infer sensor output from a configured slider value.

## ART-SLEEP — sleep proved, motion wake unresolved

A bounded external diagnostic temporarily changed the motion interval from Off to five minutes, with the original setting recorded for exact restoration. At **301.461 seconds**, it recorded native **Standby=2** and Art screen **Suspend=4**, with Art still the full app and no partial overlay. That establishes the tested sleep transition.

On return the owner found the picture active and appropriately bright. However, the kernel boot identity had changed, the recorder process was absent, and the native wake getter reported **87, BrightnessSensor**. Therefore **motion-caused wake remains unproved**. The evidence cannot distinguish the normal deep-sleep fresh-boot path from an intervening restart. It also does not establish process-preserving suspend/resume.

Developer connectivity disappeared during the interval and was recovered without another owner restart. The exact original motion setting was restored/read back; the separate off-timer stayed unchanged. Any future wake test needs a recorder capable of reconnecting, a known unchanged setting baseline and a controlled trigger. Do not launch Home on every Normal-power event: that can steal a native Art sensor wake.

## ART-EXIT — deliberate teardown and first-screen routing

The recovered deliberate exit request is:

```text
target:     org.tizen.art-app
operation:  http://tizen.org/appcontrol/operation/default
mode:       exit
reason:     paused
```

Static tracing shows `reason=paused` requests termination reason 0, while missing/different reasons choose 1. A completed installed ordinary-identity diagnostic observed ambient Off/screen None/Paused and no partial Samsung bar at the three-second sample. The underlying tuner became foreground. Its narrow guard then stopped **before the planned Home launch**, so that diagnostic was not a completed automatic exit-and-Home transaction. A separate normal Home launch restored the UI, and saved selection/matte/library comparisons matched. That exit diagnostic compared a digest of three selection fields (`content_id`, `matte_id`, `portrait_matte_id`), verified slideshow off and compared category-row counts; it was not a byte-for-byte library/image comparison. No photo/sensor setter was used.

In the inspected manager, reason Paused skips the first-screen-launch branch; reason Itself (such as stock Home exiting Art) can restore a source or launch its cached first-screen target. Art performs its native ambient transition, lowers its window and releases grabs. Pausing the stock process is not equivalent to this teardown. The mobile `set_artmode_status=off` path does not supply `reason=paused`, so do not treat it as the same request.

The manager caches `db/first_screen/type`; its consumers include ambient exit and startup, not only the Home button. A live configuration write does not prove the running cache was refreshed. The recovered manager context also carries relocation/source data such as `__K_RELOCATE_BELOW`. Home-routing changes therefore need the broader [routing/startup documentation](platform-and-routing.md), not a one-off Art exit patch.

## ART-PHOTO — later photo selection and routing results

Later attended tests confirmed short Power entered Art, Home returned to custom Home, and short Power again left Art for custom Home. This supersedes the earlier stock-only return failure as a destination result for the tested custom routing build. It does not erase later defects: on 15 October the owner reported a black screen lasting several seconds and occasional flicker when leaving Art.

Retained runtime timing is consistent with a slow Home launch, but does not isolate whether the black/flicker comes from ambient transitions, manager routing, read-drain wait, app startup or painting. Thus “returns to custom Home” is a functional pass with an unresolved transition-quality issue, **not** an immediate/flicker-free pass. No later cold-start or deep-sleep acceptance is inferred.

The owner separately confirmed choosing a personal photo displays that expected photo. This accepts the visible chooser path. It does not prove the recovered custom-gallery AppControl selector, all crop/matte choices, image conversion quality, arbitrary new uploads or custom arrow/category navigation.

## ART-IDLE — Home inactivity entry

The current design arms a monotonic **300,000 ms** inactivity interval on full custom Home. User keyboard/remote/pointer/touch input resets it; clock/feed/focus bookkeeping does not. Leaving/hiding Home cancels it and returning starts a fresh interval. Software checks covered reset, one-shot dispatch, app/overlay/utility exclusions and cleanup.

The native `enter-art` action is guarded by caller identity, Normal power, ambient Off, custom Home foreground and no unrelated partial app, then sends a single empty-fullscreen request. It does not select/import a photo or change sensors, slideshow or brightness. These guards matter because empty fullscreen toggles an already active Art session.

The owner later saw Home enter Art after inactivity. That is a physical entry pass. Physical input-reset and “playing video never triggers it” are not separately established by that observation; the implementation/software checks and attended acceptance should remain distinct.

## Evidence identity and reproducibility

The following are fingerprints/member anchors for the inspected firmware, not redistributed binary payloads. Hashes identify exact research inputs; they do not make results portable to a different build. Semantic `ART-*` headings above are stable public evidence identifiers. Original private reports include identity/bounds/restoration records; personal IDs, library counts, sessions, raw photos and local paths are intentionally omitted here.

| Identifier | Evidence identity / key members | Kind and scope |
| --- | --- | --- |
| ART-STATIC-APP | `ArtApp.dll` SHA256 `2c3084c2407fd7f9acf191ec6b120459adeff81d54167cb0d524e34eedaf9be9`; `ArtLaunchControl.LaunchFullScreen` token `0x060000ed`; `LaunchExit` `0x060000f0`; `ArtApp.HandleAppKey` `0x0600000c` | Static request/transition/key control flow only. |
| ART-STATIC-DATA | `ArtDataManagement.dll` SHA256 `da8643fb11663260323e45d9649107fc5e550a8f7d2bad8e6449a509825f608f`; `ArtMobileGetCurrentArtworkCommand`, `ArtMobileGetContentListCommand`, `ArtMobileSelectImageCommand`, `ArtMobileSendImageCommand`, `ArtMobileDeleteImageCommand`, `ArtContentDefine.CategoryID` | Static getter, import, selection, deletion and category semantics. |
| ART-STATIC-CHANNEL | `Tizen.TV.SmartView.dll` SHA256 `7f7d4d5ebc1042e2cbff94700ae31d267de2dca6947ed30835f4acb3de982eda`; `Service.GetLocal`, `Channel.GetChannelUri`, `PublishMessage`, `PrivilegeUtil.PrivilegeChecker` | Static local URL, envelope, recipient and wrapper-privilege evidence; actual access proved separately by ART-READ. |
| ART-STATIC-SCREEN | `Tizen.TV.System.Screen.dll` SHA256 `e720cf1187d17ecbe9ce9964012a95323f4bf7b41294ed227042405bbd6c8ef2`; `libcapi-screen.so.0` SHA256 `75b5a8645d6a00a9ac7486ad7f192ea764e9be352dbdec05b20a983b2a3bf245` | Getter names, enums and termination-reason sentinel caveat; not blanket installed-caller permission. |
| ART-STATIC-MANAGER | `tvctx-manager` SHA256 `9c9a61a9a98e36bdc6f275e4afc57d08f61d5bdbe184b8acfaba1f8ce3cf097f`; recovered `ambient_mode_changed` at `0x19814` | Build-specific native exit/first-screen branch reconstruction; recovered types/addresses, not original source. |
| ART-READ | Installed getter v5, subsequent import baseline reads | Runtime exact-peer/current/library proof with unchanged ordinary package identity. |
| ART-IMPORT | Separate one-image import/register/exact-delete diagnostic | Runtime one JPEG and exact library/selection restoration; no rendering-quality claim. |
| ART-INPUT / ART-LIGHT | Attended Art overlay/focused-Home/light-response trials | Physical behavior in the stated scenarios; hidden interception and custom stepping remain open. |
| ART-SLEEP | Attended six-minute interval plus state recorder | Sleep proved; wake cause unresolved. |
| ART-EXIT | Installed exit/paused diagnostic plus separate restoration | Native state transition proved; planned final automated Home step did not execute. |
| ART-PHOTO / ART-IDLE | Later owner feedback reconciled through 15 October | Chooser/idle-entry confirmed; Art exit delay remains. |

Selected raw method-body SHA256 anchors (these are **hashes**, not source or method bodies):

| Assembly member | Token | Raw IL SHA256 |
| --- | --- | --- |
| ArtApp `ArtLaunchControl.LaunchFullScreen` | `0x060000ed` | `9c553d6e8f919d0e261cff7174c94cf799e1997183c2db69c1308be790548971` |
| ArtApp `ArtLaunchControl.LaunchExit` | `0x060000f0` | `1e37c073098aea87ab64064698ca6b45b5bf9598691eaabbc76ab4c8b4f9f6c4` |
| ArtApp `ArtApp.HandleAppKey` | `0x0600000c` | `18e2958f6485bb51bcfeea33c542038178117b8ea75ebee477be87db2495d029` |
| ArtApp `ArtMainLayout.Exit` | `0x0600066b` | `5172623da4082fea8e1352474b51e1932f727cadcb544daa650f6b2ce71ecb2f` |
| ArtDataManagement `ArtScreenAPI.SetUIStatus` | `0x0600091c` | `19a842b891af829839cf026fd0ad8b4c85ee3f3ef7eb6f0ac59be6568b6b125e` |

### Pinned public protocol references

These primary client implementation links were retained during the original research; the xchwarze source page and SmartView publish reference were reachable during the 22 October documentation review. The pinned NickWaterton page could not be retrieved by the review tool; that does not invalidate the retained source/hash or establish that the link is permanently unavailable:

- [xchwarze, commit e48d6377faede37db1f034d726a079b9d8034fac, Art upload/D2D implementation](https://github.com/xchwarze/samsung-tv-ws-api/blob/e48d6377faede37db1f034d726a079b9d8034fac/samsungtvws/art/art.py#L647). Retained source SHA256 `71ed224fe2c197800d562c363cb6a40b860571272c085fcfc53a13a743fe1699`.
- [NickWaterton, commit fe95ef1d784cd32f49bf9a07ec479576574eea07, independent upload implementation](https://github.com/NickWaterton/samsung-tv-ws-api/blob/fe95ef1d784cd32f49bf9a07ec479576574eea07/samsungtvws/art.py#L376). Retained source SHA256 `3f86304c4fea5e7de1e66bc3c6fee1e32705f352515a76c742eba90cf19ec70c`.
- [Pinned connection construction](https://github.com/xchwarze/samsung-tv-ws-api/blob/e48d6377faede37db1f034d726a079b9d8034fac/samsungtvws/connection.py#L62) and [client-name encoding](https://github.com/xchwarze/samsung-tv-ws-api/blob/e48d6377faede37db1f034d726a079b9d8034fac/samsungtvws/helper.py#L24).

The inspected upstream client is LGPL-3.0. These links and independently written contract descriptions do not relicense upstream implementation under this project's MIT license; no upstream client code was copied into this chapter or integrated by this research pass.

For candidate artwork, rights and source-quality limits, see [art-sources.md](art-sources.md).
