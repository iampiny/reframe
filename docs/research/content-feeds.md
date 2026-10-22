# Provider previews and title activation

Reframe can reuse the small set of cards that installed apps publish to Samsung Home. It does **not** have a general Netflix/YouTube account, recommendation, history or playback-position API. Keep the original apps responsible for authentication, playback and profile selection.

Scope: retained evidence from a 2021 Frame, Tizen 6.0, firmware 2301.1. Reconciled on 22 October 2026 without new TV tests. These private firmware contracts may differ on other models, regions, app releases or firmware. Artifact digests below identify the inspected builds; proprietary binaries, raw personal feeds and decompiled vendor code are not distributed.

## Status and source boundary

| Capability | Evidence and status | Limit |
| --- | --- | --- |
| Native Netflix/YouTube feed read | **Installed-package verified:** ordinary native package identity read populated models; TV-only presentation subsequently used real rows and artwork. [FEED-ACCESS](#feed-access-native-reader-and-permissions) | Not a promise that every public-signed app or firmware permits it. SDK reads alone are weaker evidence. |
| Netflix title activation | **Owner verified:** selecting a card opens the intended title. [FEED-NETFLIX](#feed-netflix-title-activation) | Correct episode, resume position, profile and separate cold/already-running cases remain unverified. |
| YouTube video activation | **Owner verified:** selected cards now open the correct video. [FEED-YOUTUBE](#feed-youtube-native-recipient-contract) | Separate warm/cold acceptance, account correctness and every action shape remain unverified. Earlier failures are superseded, not silently omitted. |
| Plex own-library rows | **Captured data + implementation:** server-source cards can be separated from catalogue cards. | Exact library title activation is not implemented in the public reader; server/profile/resume semantics remain unverified. |
| GeForce NOW previews | **Unknown:** an inspected snapshot encoded an empty string; no populated action contract was recovered. | Empty once does not mean permanently unsupported. The public reader does not query this provider. |
| Refresh after viewing/profile changes | **Partially mapped:** publisher, storage and consumer mechanisms are known. | Latency, completeness, profile coherence and stock/custom-mode equivalence need attended comparison. |
| No-Mac operation | **Architecture implemented:** the native helper reads and fetches images on the TV. | An explicit Mac-disconnected plus temporary-network-failure acceptance run remains pending. Plex naturally still needs its configured server. |

Implementation references: [FrameFeeds.cs](../../native/FrameFeeds.cs), [FrameOverlayApp.cs](../../native/FrameOverlayApp.cs), [app.js](../../app/app.js), [architecture](../ARCHITECTURE.md). The public checkout reviewed for this chapter declares Home 0.3.53/native 0.4.42. Later owner confirmations and installed input fixes exist beyond that exact source pair. Public source also contains a fail-closed caller-hardening edit that was deliberately excluded from the last accepted TV deployment. A successful observation on the household TV is not acceptance of every current public-source change.

## FEED-PIPELINE: who produces and consumes cards

The useful model is:

```text
provider publishing service
  -> ContentServiceManager / content-tiles-provider
  -> native get_preview_metadata(package)
  -> native-owned parsed model + opaque card references
  -> Home presentation and bounded image requests
  -> provider-owned title action on selection
```

The inspected web publishing path is `webapis.preview.setPreviewData` -> `Preview_setPreviewData`. The extension sets the package to the caller's own package; it is a publisher, not a cross-app getter. Static analysis resolves `SET_PREVIEW_DATA` messages to `ContentServiceManager`, port `PARTNER_REQUEST_PORT`, with a START/count phase and ordered 0x800-byte data fragments. The manager has an `update_json_str_to_preview_db` persistence call. The complete worker dispatch between those endpoints remains partly unrecovered. This does not establish that Netflix's or YouTube's current provider uses that particular web path.

Stock CoBA calls the native getter through `ContentDBAPI.GetPreviewMetadata` and `FSContentDBUtil.GetPreviewMetadata`, builds its model, and separately writes a presentation snapshot. That snapshot is **not the authoritative database**. Its encoding is .NET `BinaryWriter.Write(string)`: unsigned 7-bit encoded byte length followed by UTF-8 bytes. A one-byte zero file is a valid empty string. It is distinct from `{ "sections": [] }`, a missing file, denial, malformed JSON or an interrupted write. The inspected writer uses create/write rather than demonstrated atomic replacement; diagnostic snapshot readers must tolerate partial writes.

Stock preview events carry `app_pkg_name`, `section_id`, `OPERATION`: Undefined=0, DeleteAll=1, InsertTile=2, InsertSection=3, InsertAll=4, Modify=5. CoBA refreshes by package, deduplicates work and rate-limits it at approximately one second before retrying. Its renderer checks a 40-tile bound. These are stock-consumer details; Reframe does not currently subscribe through this event interface. Reframe uses bounded reads and a 60-second model cache, invalidated for refresh when Home resumes.

The inspected stock consumer waits for service readiness, BPI availability, then a 60-second stabilization sleep before listener setup. That does **not** mean every provider always needs exactly 60 seconds after startup. Separately, `ContentServiceManager.LauncherVisible/LauncherHidden` changes scheduler inputs and queues provider work. Keeping Samsung's process alive does not prove refresh is unaffected by replacing its visible Home. Keep its publishing/services available until those lifecycle differences are measured.

Do not confuse `preview-updater.dll` plugins or `preview-downloader.dll` live/DVR recommendations with the personal OTT feed pipeline solely because their names include “preview.” They have different inspected responsibilities.

## FEED-ACCESS: native reader and permissions

Public JavaScript file access returned `SecurityError`; an ordinary installed-native attempt to open Samsung's snapshot files was also denied. Native ContentService IPC is a different access path. It succeeded under an installed UID 5001/package-specific SMACK identity for Netflix and YouTube. A temporary Node service once read snapshots, but failed after cold start and was removed; it is not a runtime dependency or recommended fallback.

The Samsung managed wrapper checks `http://tizen.org/privilege/internal/default/platform`. Its diagnostic may mention ordinary app-launch privilege even though the actual Cynara check is the platform privilege. Copying that wrapper or declaring app-launch permission does not grant platform access. The native IPC result is a firmware-specific observed permission outcome, not a way to bypass Cynara/SMACK. No root daemon, policy modification, provider impersonation or Mac feed relay is required by the current reader.

The relevant ABI is:

```c
/* Contract summary, not copied vendor implementation. */
char *get_preview_metadata(const char *package_utf8);
void finalize_json_bytes(void *allocated_json);
```

Both symbols are Cdecl exports from `libcontent-db-api.so.0`. The getter returns an allocated NUL-terminated UTF-8 string; retain the pointer, copy bounded bytes, and **always** call `finalize_json_bytes` in `finally`. Do not substitute a generic free for the matching finalizer. A null pointer or empty string is handled as an empty result. The library's getter ultimately uses ContentService SELECT RPCs, rather than simply opening the presentation file.

Current `FrameFeeds` constraints:

| Boundary | Implemented limit/behavior |
| --- | --- |
| Providers | Fixed Netflix, YouTube and Plex package/app mappings only. |
| Getter execution | `Task.Run`; one process-wide reader semaphore with nonblocking acquisition. Busy callers fail rather than accumulate unbounded waiting getters. |
| Cancellation | Firmware getter has no demonstrated caller cancellation. A UI deadline does not abort its underlying native call. |
| Result copy/parser | At most 2 MiB before NUL; strict UTF-8; JSON depth 32; package identity must match request. |
| Rows/cards | At most 8 sections and 40 retained cards total; provider array order preserved. |
| Text/action | Title/subtitle capped at 512 characters; section title 160; actions above 32 KiB rejected, never silently truncated. |
| Eligibility | Display windows checked while reading and when resolving a card. Unix seconds; zero end means no supplied end bound. |
| Reference | New opaque 32-character ID; unchanged image/action pairs can retain an ID in process. IDs are not provider media identifiers. |
| Cache | Model reused for up to 60 seconds only while its retained cards remain within display windows. Native model/image files use owned private storage with directory 0700/files 0600. |

There are two important caveats. First, the firmware getter has an internal full-JSON log statement: count-only application diagnostics do not guarantee the platform itself logs no private data. Avoid broad vendor log collection. Second, inspecting the getter revealed a first-use registration helper that can insert an accelerator row for an unregistered provider. Do not call arbitrary package names and describe the operation as universally side-effect free. Reframe deliberately excludes GeForce NOW from this reader.

### Native SELECT transport and permission filtering

The retained client trace uses the system bus interface `org.tizen.content_db_service`, object `/org/tizen/content_db_service`, method `RequestDBAction`, opcode 6 for the relevant SELECT request. Each SELECT has a 4,000 ms response-semaphore wait; this is not a deadline for initial connection setup, all queries or the whole getter. The client starts its own detached GLib signal-loop thread, so a console diagnostic did not require an invented UI host. A product UI timeout cannot cancel that thread/native call. The caller’s 2 MiB copy cap also cannot bound allocations already performed inside the vendor getter.

The tiles provider normalizes the requested app to a package, computes a read-permission level, rejects below 1, restricts projected columns and constructs a permission-filtered selection. Existing `com.samsung.px.permission.READ_TILES` maps to level 1, MVPD to 3 and internal to 4 in the inspected code. Installed metadata fallback checks the presence of `use.accelerator` or `use.preview`, not their textual value. These are internal provider rules, not manifest privileges to claim or credentials to forge; the first-use accelerator insertion caveat still applies.

Replies use transient 0600 shared-memory objects. The consumer opens/consumes and unlinks the IPC name; that does not mean feed rows were deleted. Bounded broker methods did not inspect the actual bus sender in the traced bodies, but external system-bus/SMACK/dependency policy was not fully recovered. This is not a claim that arbitrary callers are authorized. Use the ordinary getter with a genuine caller context and reproduce installed-identity access, rather than hand-crafting identity fields or changing system policy.

These additional static anchors are `content-db-provider` SHA256 `e3208f88a22aade75e0bf55327c51321f4aeddbfbe177490af7dbfb685a46b7e` and `content-tiles-provider` SHA256 `392e9e1c698eb7adef1ed1ec2e0540b28213e0e3269b8c876bef10267252d457`. The invoked SELECT/read path later succeeded in installed diagnostics; the entire native service and policy surface was not audited.

## FEED-SCHEMA: what is actually in the model

The observed union is below; fields are not guaranteed populated or semantically identical for every provider.

| Level | Observed fields |
| --- | --- |
| Root | `_id`, `app_pkg_name`, `enabled`, `sections` |
| Section | `_id`, `accelerator_id`, `position`, `title`, `title_display_mode`, `subtitle_display_mode`, `tiles`; Plex also had `id` |
| Tile | `_id`, `section_id`, `position`, `title`, optional `subtitle`, `image_url`, `image_ratio`, `width`, `height`, `action_type`, `action_play_url`, `display_from`, `display_until`, `is_playable`, `is_progress`, `is_favorite`, `is_notification`, `public_extra` |

`public_extra` is a **JSON-encoded string**, not an object. Observed keys were `is_resumable` and `video_url`; sampled values were false/empty. No structured duration, offset, completion percentage, synopsis, episode number, profile object, trailer or alternative hero-image collection was present. Generic database columns for alternate actions/icons exist, but were empty in inspected provider records. Schema capability is not populated-data capability.

This example is entirely synthetic and cannot launch a real title:

```json
{
  "_id": 100,
  "app_pkg_name": "org.example.provider",
  "enabled": 0,
  "sections": [{
    "_id": 200, "accelerator_id": 100, "position": -1,
    "title": "Recommended", "title_display_mode": 1,
    "subtitle_display_mode": 1,
    "tiles": [{
      "_id": 300, "section_id": 200, "position": 65534,
      "title": "Example programme", "subtitle": "Example creator",
      "image_url": "https://images.example.invalid/card.jpg",
      "image_ratio": "16by9", "width": 0, "height": 0,
      "action_type": "EXAMPLE_ONLY",
      "action_play_url": "opaque-example-action-not-for-dispatch",
      "display_from": 0, "display_until": 0,
      "is_playable": 1, "is_progress": 0,
      "is_favorite": 0, "is_notification": 0,
      "public_extra": "{\"is_resumable\":false,\"video_url\":\"\"}"
    }]
  }]
}
```

Populated captured roots had `enabled=0`, so the implementation does not discard all cards based on that unresolved flag. Netflix section/tile position values tied; array order is safer than inventing order from those values. `_id` identifies database rows, not a stable title across database recreation. Keep provider-supplied row titles/language; do not relabel recommendations as Continue Watching based only on app identity.

### Per-provider useful data

- **Netflix:** Continue-Watching-labelled and other personalized/profile-associated sections; title, occasional subtitle, one artwork URL and opaque `NETFLIX_LAUNCH` action. The sampled action includes expiry/tracking context and can include an encrypted profile marker. It does not expose a readable current-profile API. Numeric progress flags were zero; that does not disprove resume performed inside Netflix.
- **YouTube:** a recommended/profile-associated section with title, subtitle, one thumbnail and `WAS_LAUNCH` video action. No demonstrated subscriptions, playlists, full history, Continue Watching or numeric position. Do not assume subtitle is a formally typed channel field or that the active account has been established.
- **Plex:** own-library Continue Watching/recently-added candidates alongside unrelated catalogue rows. Actions are JSON with `context` and `metadataSourceUri`. Public code retains `server://` candidates and excludes `provider://` catalogue entries. Artwork can carry `X-Plex-Token`; it remains native-private. Filtering by source scheme is implemented, but correct active library/profile and complete section coverage need visual comparison.
- **GeForce NOW/other installed apps:** no populated game feed/action was recovered for the inspected GeForce state. Other providers need their own schema, policy and activation investigation. Installing/pinning an app does not automatically make it a supported preview provider.

## FEED-IMAGES: images, lifecycle and input performance

A card exposes one image URL, not a known high-resolution artwork service. Earlier runtime fetches decoded Netflix and YouTube JPEGs at roughly 620×349 and 640×360. Netflix reported a square ratio despite a landscape image; all sampled width/height fields were zero. Size from decoded content, then deliberately contain/crop. Do not stretch small thumbnails into full-screen hero images and claim an unavailable variant.

The native allowlist is provider-specific: Netflix HTTPS port 443 at `nflxso.net` or subdomains; YouTube HTTPS port 443 at exactly `i.ytimg.com`; Plex only the explicitly configured server origin, with matching scheme/host/port. Userinfo/fragments and oversized URLs are rejected. Plex may use HTTP for a configured local server; that is an explicit deployment choice, not a general permission to fetch arbitrary LAN URLs.

Image requests disable redirects, cookies, default credentials and proxies, retain normal TLS validation, require HTTP 200 and `image/jpeg`, validate JPEG signature, and bound streamed length to 512 KiB. Two concurrent requests and a five-second request timeout cap work. Only opaque references reach the browser; actual URLs/actions remain native. JPEGs are returned as Base64 data. Removed model generations have their image files pruned after refresh; image/action pairs can reuse current references.

The UI's performance history matters: rebuilding card rows or decoding images during every app-arrow press caused visible input delay on this hardware, including for apps with no feed. Current code retains per-provider row views, reuses unchanged views, computes movement from shared geometry rather than repeated layout reads, and waits 160 ms after selection activity before starting nearby artwork work. This defers expensive assets, **not the selection feedback**. Artwork is requested around the current row/card, at most two at a time. Responses for hidden/suspended/obsolete views do not paint over the active UI. Full Home owns these rows; the partial sidebar over playback must not load/show recommendations over the running video.

Both UI and helper drain outstanding device reads before title launch, avoiding a late reply resuming the overlay over the target app. `OpenFeedItem` serializes launches and reports errors rather than silently claiming a title was selected. UI-only timeouts must not queue replacement native getters indefinitely. Remaining physical stutter and selection-continuity glitches are not disproved by a smooth desktop preview.

## FEED-NETFLIX: title activation

Public code resolves the opaque reference in the latest native model, checks current display bounds and later rechecks object identity after policy reads. It targets `org.tizen.netflix-app`, using the real helper caller. Case-sensitive extras are:

```text
--root              app
--app-js            netflix
callerid            <real calling helper ID>
action_play_url     <complete original provider action>
__K_SPLASH_SCREEN   LA_UI_DEFAULT
__LEGACY_LAUNCH__   true
```

The original action is a query-like opaque string, not a JSON object. Observed key names include `expireAt`, `listContext`, `m`, `source_type`, `source_type_payload`, and optional `encryptedProfileGuid`/`badges`. Nested encoded context contains attribution as well as a title identifier. Preserve its exact bytes; reconstructing a URL from a movie ID discards contract data. Public code requires exactly one parseable `expireAt` still in the future and the expected action type. No real action values belong in documentation, fixtures, screenshots or logs.

Stock's locked-app branch takes a different policy-wrapper path. Reframe does not emulate a Samsung caller or copy its stock relocation hints. It reads current WAS app policy and refuses title launch if installation/identity, lock, availability, age/update policy or readable state is not established. The source's `RequireUnlocked` uses `libwas_db_api.so:was_get_app_info`, a firmware-2301.1 layout of 24 inline 128-byte strings plus 35 integers (3212 bytes). Store/app/package/state checks and policy offsets are in the source; do not transplant this ABI to another firmware without validating it. A stale backup database is not a current lock check. Refusal directs the user to the app/Samsung UI rather than bypassing its lock screen.

Owner confirmation now supersedes earlier “title activation pending” notes for **correct title opening only**. No exact resume/episode/profile promise follows from a successful AppControl callback or from that observation.

## FEED-YOUTUBE: native recipient contract

YouTube's installed wrapper is `9Ur5IzDKqV.TizenYouTube`; title delivery ultimately targets its validated native recipient `com.samsung.tv.cobalt-yt`. Copying Samsung's intermediate broker request straight to the wrapper did not work. The current translation uses installed package metadata, current policy and the actual receiver contract:

| Item | Current source behavior |
| --- | --- |
| Metadata | Reads only `app.link`, `launch_social_v.link`, `native.userdata`, `nativeID` for the fixed wrapper from the package parser database, opened read-only. |
| Configuration checks | Native ID must match known Cobalt recipient; `app.link` must agree with userdata's HTTPS `www.youtube.com/tv` base; suffix must be `#?v=`. |
| Payload prefix | Reads `rtc/memory/FSWebApp/9Ur5IzDKqV.TizenYouTube_URL` with `vconf_get_str`, releasing that allocation with `free`. A missing pointer maps to **empty string**, matching stock behavior. |
| Action composition | Configured prefix + installed `#?v=` suffix + original provider action suffix. Preserve provider query arguments. Stock-compatible splitting on literal `v=` is not a general URL parser. |
| Recipient | `com.samsung.tv.cobalt-yt` from validated `nativeID`. |
| Actual AppControl operation | `http://samsung.com/appcontrol/operation/eden_resume` |
| Extras | Uppercase `USERDATA` from installed metadata, lowercase `callerid` with real caller, uppercase `PAYLOAD` with composed action. No invented AppControl URI. |

A synthetic shape, **not playable data**, is `#?v=<video-id>&launch=<provider-value>&deeplinkId=<provider-value>`. Keep live values private. When the volatile prefix is absent, do not synthesize a full web URL: the native warm path handles leading fragments differently from full addresses. The initial implementation added a base URL and reached YouTube's unavailable-video screen despite accepted launch delivery. Correcting the missing-prefix behavior was followed by owner confirmation of exact video opening.

Reconciled sequence: (1) app opener only -> default YouTube; (2) uppercase `PAYLOAD` alone -> still default; (3) native target/userdata/actual operation -> delivery but video error; (4) stock empty-prefix fragment handling -> owner-confirmed exact video. Static receiver strings justified each investigation step, but did not by themselves prove playback. Separate already-running and cold-start video trials remain desirable; no blanket claim is made from one acceptance statement.

## FEED-HISTORY: Continue Watching and routes that did not yield OTT history

Continue Watching **section membership** is useful; it is not numeric playback progress. The inspected per-app samples did not contain duration/offset values usable for a reliable progress bar. False `is_resumable` or zero `is_progress` does not disprove resume handled privately inside the target app.

A separate `CWPkgInfo.db` registry has package, background-service, foreground-app and enabled columns. It schedules producers; it contains no titles, profiles or positions. Plex was registered; Netflix/YouTube/GeForce NOW were absent in that snapshot. That absence does not rule out their ordinary preview sections or another backend.

`Tizen.TV.Multimedia.ContentService` has `DURATION`, `PLAYED_POSITION`, `PLAYED_TIME`, `PLAYED_COUNT` getters. Native `libcsf-content-agent.so` tracing grounded the inspected source selection in MSC/PTP/DLNA and media-file/PVR/UHD databases. It is **not an established Netflix/YouTube watch-history aggregator**. Some managed getters ignore status and return zero; zero is not proof of no playback. Units/completion/profile semantics must be established before reuse.

The inspected Content Panel SmartThings resource declaration lists now-playing title/season/episode/duration/position fields. Its actual request callback removes requested resources, and the service exits after five seconds; no usable metadata response was found in the inspected binary. A declared schema is not an implemented feed. Likewise `SaveHistory` in CoBA records component order, and request “progress” lists track outstanding requests, not playback progress.

Plex's authenticated server API may supply richer own-library data, but needs separate explicit server configuration/authentication and implementation. This chapter supplies no credentials or assumption that Plex's free catalogue is the desired library. The current public `PrepareLaunch` deliberately refuses Plex title activation; its `eden_resume`/`assetId` manifest declarations alone do not map the entire `{context,metadataSourceUri}` action into a proven resume request.

## FEED-EVIDENCE: provenance and limits

These are semantic evidence IDs for the conclusions in this chapter, not external download links. Digests identify inspected vendor artifacts; the artifacts and private records are retained outside the public repository. Source inspection identifies what this checkout implements; it is not a substitute for physical acceptance. No live TV/account/media tests were performed for this documentation pass.

| ID | Retained artifact/member identity | Kind and supported conclusion |
| --- | --- | --- |
| FEED-PIPELINE | `webapis-preview.so`, SHA-256 `934d8a7e2dcff51ea81e93acdbb0c91c6a4e531e105324390523ebacb52e3c41`; `ContentServiceManager`, SHA-256 `c264c9d2bef38ee356232abf389d183c0e24f9e0de7eda0fe8e7ff35330f00da` | Static publisher transport/persistence/scheduler anchors. Individual provider publisher bodies and complete intermediate dispatch not recovered. |
| FEED-ACCESS | `libcontent-db-api.so.0`, SHA-256 `cf887a4d3959d919ff41f0d2ac191b48b7c1d73f08242928d51ca1fa15271f6e`; `get_preview_metadata`, `finalize_json_bytes`, `select_request`; ContentService wrapper | Static allocation/IPC/logging contract plus retained ordinary-installed-package runtime success. Firmware-specific boundary. |
| FEED-SCHEMA | Retained provider models and read-only provider-table comparisons; no raw records published | Captured field/type/action shapes. Different snapshots changed, but this does not establish active profile or refresh latency. |
| FEED-IMAGES | Installed native JPEG fetch/decode diagnostics; public `FrameFeeds.Image`, `app.js` retained-view/artwork queue | Runtime image-path proof for sampled Netflix/YouTube images; source limits and measured/owner-observed input regressions. Not every image/provider accepted. |
| FEED-NETFLIX | `Tizen.TV.Service.CoBA.Core.dll`, SHA-256 `817b4b38a8083b788a18433063e9fcbd93c9b36526c14474dade6903a2c4301f`, `WebAppTileLauncher.LaunchNetflix`; `CoBA.Common.dll`, SHA-256 `7381da9da00ee0a7e499e5fec690f3f341466d98e7e7c4dc8bb23c0edbd2464c`, `Util.AppLauncher` | Static target/extras/lock branch plus later owner exact-title confirmation. Resume/profile cases remain open. |
| FEED-YOUTUBE | `cobalt_launcher`, SHA-256 `978fbf90939f767c3b2e8fa32088835b2f41bd7a18f09a8ef409c35fe29986ca`; `litewebappservice`, SHA-256 `3df2a16575c7700e4bb08d8c9cda920f143d41f085f6bfe50bd0dc37a799e95d`; Cobalt receiver/broker and warm-fragment paths | Static native receiver contract, bounded normal-identity shape diagnostic and later owner exact-video confirmation. Earlier wrong-payload attempts failed. |
| FEED-HISTORY | `libcsf-content-agent.so`, SHA-256 `efbef754c581e4d8217b9af0b237574f5f2427064436cc9972466636992bcb3b`; `content-panel`, SHA-256 `27d8aff2968ff0e4072ac3c1f4daa9268f3ea1a3ea471dbd9fa440787f21d6c8` | Static source/getter/callback analysis; rejects these inspected routes as demonstrated OTT-history readers. No global absence claim. |

## Building another feature safely

Use the native-private feed boundary and extend the presentation model only for fields actually observed. Keep authentication and title actions with the original app. Do not export full URLs, encrypted profile identifiers, provider actions, server tokens or household titles to a public preview, source archive, issue attachment or broad diagnostic log. Publish synthetic fixtures only. Public UI receives `appId/name/status/fetchedAt`, row IDs/titles and card `id/title/subtitle/hasArtwork`, not raw actions or URLs.

Before adding another provider, validate its registration, ordinary-package permissions, populated schema, exact image origin policy and app-lock behavior. Do not call an unregistered getter or spoof Samsung's caller as an exploratory shortcut. Treat empty, expired, unavailable, malformed and busy as different states; preserve usable UI when network or a provider fails. The public source bounds what it can read but does not establish every firmware's underlying authorization or platform-log behavior.

Remaining acceptance should compare stock/custom rows and refresh after ordinary viewing; validate profile switches with the owner; check Netflix episode/resume and both providers warm/cold; exercise expired/replaced cards; test offline recovery and Mac-disconnected operation. A successful launch return means delivery was accepted, **not** that the intended media, profile or resume position appeared.
