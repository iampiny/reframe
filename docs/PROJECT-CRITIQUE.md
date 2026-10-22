# Project critique and cleanup

Historical engineering-review record. For current firmware behavior and acceptance, use the [research handbook](research/README.md) and its [status ledger](research/evidence-and-status.md); a pending statement below retains the review-stage context rather than overriding later results.

## Complexity audit

| Finding | Concrete problem | Cleaner shape |
| --- | --- | --- |
| Design export as build authority | An external Pen file and importer regenerated HTML/assets, while web/native overrides accumulated around export attributes. | Own readable HTML/CSS/JS; keep shared geometry in one JSON file. Build locally with no Pen export or runtime requirement. |
| Stale status mixed with history | README advertised an old installed pair; acceptance instructions mixed current batches with superseded experiments. | Current setup/architecture/validation/recovery guides; preserve original evidence privately. Distinguish cleanup candidate, deployed pair and physical acceptance. |
| Personal runtime defaults | TV/Plex addresses and local paths were embedded in tooling/backend; a tool imported a private research module. | Explicit ignored local configuration; public tools have no private-code imports or personal defaults. |
| Signing identity entangled with tooling | Packaging created an author identity using bundled SDK development signer material. | Official SDK packaging with each developer's own certificate profile. Do not distribute signer keys or copy the owner's identity. |
| Unclear public asset rights | Imported official app artwork was tracked beside project assets. | Read installed artwork at runtime; public demo uses original abstract art/text. Include Inter and Lucide/Feather notices. |
| Hardware policy inside window rendering | Brightness profile reads/writes were embedded in the native overlay app. | Extract only the guarded transaction to FrameBrightness; window/input state stays in the overlay. Both UI request paths keep the same transaction. |
| Two renderers mistaken for one implementation | Shared geometry/assets did not make web and NUI the same component. | Keep explicit renderer ownership and shared token contract, with paired physical checks. Do not add a new rendering framework to conceal this boundary. |
| Production and preview/research mixed | Real feed snapshots, obsolete experiments and signed bundles lived near source; old history included personal details. | Bounded fictional demo, private local artifacts, and an allowlisted history-free source release. |

## Updated path

The cleanup candidate preserves current lifecycle, native feed policy and measured painting changes. It adds no frontend dependency/framework. The web app remains a cohesive focus/render/input coordinator rather than being split into arbitrary wrappers; hardware brightness is the one extraction with a clear existing boundary. Private Samsung ABI guards and separate NUI/web renderers are justified complexity.

The candidate builds and can be inspected without touching the TV. SDK-profile signing/fresh installation and physical UI/input acceptance remain unproved. A public one-click Home/boot installer is not implemented. No cleanup claim should turn those unfinished product capabilities into accepted behavior.

The owner approved MIT and the reviewed local migration on 14 October. The replacement was applied to the main checkout after verifying its source manifest against the reviewed candidate and the original tracked files against the saved archive. Retired files are retained under the ignored `private/archive/2026-10-14-project-cleanup/`. Git history, private research, installed rollback packages and sibling Xbox work are preserved. The installed TV pair is unchanged; no publication occurred.

## Second review: build, release and layout ownership

| Finding | Evidence and consequence | Resolution |
| --- | --- | --- |
| Preview reused production package inputs | Preview fixtures and fictional artwork were copied into `build/web`; signed packaging took that directory verbatim. Basename filtering in unsigned archives did not exclude files nested in preview image directories. | Separate `build/preview`; package only the named production targets. No fixture-name heuristics are needed in packaging. |
| Public roots included untracked files | Recursively exporting `app/tools/docs` could publish a newly created local file that had never been staged or reviewed. | Intersect public roots with Git-tracked paths; retain the bounded privacy scan and manual archive review. |
| Shared layout still duplicated in HTML | Clock, brightness, viewport, utility and card rectangles were hard-coded even though native NUI read the layout JSON. Token changes could diverge across renderers. | Generate static clock/brightness rectangles from JSON and bind sidebar/utility/card CSS to shared variables. Remove the corresponding inline copies. No per-input DOM measurement or painting change. |
| Control glyphs duplicated across renderers | HTML embedded SVG paths already present in native-shared assets. | Both renderers load the same glyph files; browser Auto uses a small blackening filter when enabled. |
| Launch failure advanced the deployment baseline | Home launch confirmation occurred after the installed-pair files were overwritten. | Include launch in the install/readback rollback transaction and advance the saved pair afterward. |
| Unsigned updater could reuse an old native package | Inspection writes elsewhere while the updater still copied the signed `build/FrameDock.tpk`. | Reject unsigned deployment before build/device operations. Signed packaging removes old generated SDK outputs before requiring exactly one fresh output. |
| Documentation overstated rendering/source guarantees | Card views were called virtual rows; source membership and the 1080p rendering stage were unclear. | Document retained views, the scaled 1920×1080 stage, explicit release membership, and software versus physical acceptance. |

The web coordinator remains cohesive: focus, bounded cached views and native request draining share one lifecycle. Moving them into wrappers would add concepts without eliminating the current synchronization work. Feed ABI/policy, brightness transactions and partial-window lifecycle already have distinct owners. No runtime framework, shared browser/native widget claim or new background daemon is introduced.

The first-opening catalogue selection and native YouTube closed-sidebar brightness cases are recorded in ARCHITECTURE.md and the private workspace backlog. They need targeted TV observation; this source review does not mark them accepted or alter TV policy.

## Development preview refactor

The earlier recommendation to move preview code into a file was insufficient: simulated brightness returned before production confirmation handling, and simulated title launches skipped pending-work draining. The useful boundary is external device operations, while the actual Home lifecycle stays shared.

The implementation keeps one web Home controller. It extracts the existing Samsung operations into `app/tv.js` and moves fake app screens, catalogue fixtures, installation controls, keyboard translation and asynchronous device replies into `dev/`. Both launch paths use one suspension/drain/confirmation/recovery function, and both brightness paths use the same busy/readback validation. Renderer/cache policy stays in Home; firmware policy stays native. The builder chooses one boundary, so simulator code and styling never enter TV packages.

The watcher reuses the existing builder and full-page reloads. It adds no frontend dependency, generic platform framework, fake SDK or duplicate UI. Native NUI continues to require its own physical checks; the simulator does not establish native correctness or selected-video activation on the TV.

## Implementation review: shared preview lifecycle

| Finding | Evidence and consequence | Resolution |
| --- | --- | --- |
| Return events were discarded during launch | `activateHome` returned immediately while `launching`. A TV return/focus event arriving before the SDK acknowledgement could leave Home suspended, with artwork and idle work stopped. | Home retains the return request until launch completion. A later suspension clears it. Ordinary focus before an external departure is ignored. |
| Ordinary SDK launches had no deadline | Helper replies timed out, but `device.launch` could leave launch/input busy indefinitely if Samsung did not call either callback. | One bounded device-call function owns completion, pending tracking and late-callback settlement for both paths. The operation deadline is 12 seconds by default; brightness/catalogue retain their 8-second limits. |
| Duplicate focus notifications restarted work | Focus and visible events always focused the current tile and requested brightness/catalogue, even when Home was already active. | Activation is idempotent for an already-active presentation with no explicit cursor handoff. Startup begins suspended and uses the same activation path. |
| Failure recovery could unpause hidden Home | Launch catch always cleared suspension, including after the TV switched away. | Only visible Home resumes on failure. A queued return uses the normal activation path; a hidden view stays hidden. |
| Simulator installs depended on six defaults | Saving `apps.slice(6)` treated catalogue position as installation ownership. Changing the demo defaults would silently persist or lose the wrong entries. | Keep one validated installed-app collection and persist that collection directly. |
| Watcher omitted an actual build dependency | `build-web.py` imports `tools/project.py`, but editing that module did not cause a rebuild. | Include that exact module in the watched inputs. No general dependency tracker. |

Focused verification executes the actual lifecycle/device-call functions with asynchronous SDK stand-ins: return-before-acknowledgement, repeated activation, failed launch recovery, a second suspension, timeout, late callbacks and pending-read draining. Browser checks exercise the unchanged Home/sidebar/brightness paths and delayed failure handling. These checks do not establish real Samsung callback ordering or hardware acceptance.

Timeout stops waiting; it cannot cancel Samsung's external operation. An app can still open after its SDK confirmation deadline. The simulator likewise provides external-screen stand-ins, not actual playback, firmware policy or a completed Artwork browser. Native NUI remains a separate renderer with a shared layout/assets contract; merging it with the browser controller would require replacing the validated partial-window implementation and is outside this cleanup.
