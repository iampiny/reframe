# Validation

## Completed for the cleanup candidate

JavaScript syntax, Python syntax, web generation and both native compilations are checked. Unsigned package inspection verifies the shared layout/config assets and the absence of preview/provider data. The browser is checked at a single 16:9 TV stage: H/Backspace playback-sidebar reopening, second-H cursor handoff, media-card focus, and Auto on/off pass. A fresh extraction of the source archive builds its demo without private code, dependencies or a design tool. Preview results do not prove TV rendering/performance.

No permanent implementation-mirroring test suite is required for this cleanup. Changes must still pass the relevant build checks and the physical cases below before being called accepted.

## Second review verification

Home 0.3.46/native 0.4.37 compile and package for unsigned inspection; they are not installed. Existing signed installation/recovery files remain byte-for-byte unchanged. JavaScript/Python syntax and diff whitespace checks pass.

Temporary copies verify: untracked files inside public source roots are excluded; tracked personal-path fixtures refuse export; the 58-file source archive builds its offline demo after extraction without Git/private assets; production builds leave preview outputs intact; both inspection archives exclude preview assets; packaging selects exactly one fresh SDK output; edited clock/Auto/viewport tokens change generated CSS; unsigned deployment is refused before device operations. A signing-tool stand-in checks tool flow, not certificate validity.

A simulated device verifies update success and both readback/launch failure paths: both applications and the saved pair end in the correct state. This does not prove a real TV can install or restore the packages. The local 16:9 preview retains the previous shared dimensions and passes media focus, Home/sidebar handoff, Auto on/off and R dismissal. All seven shared SVG glyphs load at their expected sizes, including the inverted enabled Auto icon. No permanent test files or TV policy changes were added.

## Prepared owner batch

1. Open YouTube, Home once: its tile should be selected, with no rows over playback. Browse to another tile, Back, then Home: the playing app should be selected again. Browse to Plex and Home again: full Home should retain Plex selection. Repeat with Netflix.
2. Open two recognisable YouTube cards, including a warm launch. Check the exact video rather than a generic app screen/error. Check a Netflix selected-title/resume action separately.
3. Press the channel rocker inward, short and held, on Home, over playback with our sidebar open, and with it closed. Report each separately. Up/Down brightness must not interrupt playback or open a scan screen. Closed-sidebar suppression is not accepted.
4. Verify full Home and native sidebar: 4:3 rounded tiles, equal gaps, visible selected endpoints, eased scrolling, left-aligned utility labels and responsive movement. Settings Back and native Art exit should reach custom Home under existing routing. Samsung UI should remain until explicitly dismissed.
5. Brightness: 10% steps, endpoints, slider/Auto focus transfers, Enter toggles Auto twice, enabled visual state, sensor adaptation and unchanged Art dimming. Home idle should enter native Art after five minutes; playback must remain playing.

Prepare collectors before requesting owner readiness. No surprise timed probes. Do not request a restart unless a registration change genuinely requires it. Cold boot still needs a separate recorder/owner batch; manually opening Home is not a startup pass.

## Shared preview/device refactor

Home 0.3.49/native 0.4.38 is a source candidate, not a TV deployment. Browser checks cover asynchronous fixture loading, tile and selected-card launches, H/Back/R, current-app selection, second-Home cursor continuity, no rows over playback, Auto on/off, delayed optimistic brightness and failed brightness confirmation. A delayed failed launch holds selection while pending and restores navigable Home afterward. A source edit rebuilds the existing preview and reloads its script digest without a manual refresh.

Production inspection must include `tv.js` and exclude `simulator.js`, `simulator.css`, all preview fixtures and the development reload endpoint. The preview must select only its simulated device boundary. Native compilation and signed-baseline preservation remain separate checks. SDK-profile signing and physical regression of this refactor are not established by browser simulation.

Isolated checks confirm byte-identical shared `app.js` in preview/production, selected boundary scripts, no simulator/data in TV archives, previous-output retention after assembly/JavaScript failures, and successful corrected rebuilds. A fresh source release builds its fictional preview without Git/private assets. SDK stand-ins verify that helper targets, Settings/Apps payloads, owned-caller focus validation and channel registration remain unchanged; these checks do not execute Samsung firmware. Both native projects compile; the original signed recovery pair remains byte-identical.

The implementation critique adds focused asynchronous lifecycle checks: return before launch acknowledgement, idempotent focus, visible failure recovery, later suspension clearing a queued return, bounded SDK/helper completion, late replies and launch draining. Simulator installation persistence excludes defaults and survives reload; watcher snapshots include its settings dependency. Browser Home/sidebar cursor handoff, R/Back dismissal, Auto on/off, selected-card stand-in launch and delayed failed-launch recovery pass on 0.3.49. The TV boundary payload/caller/channel checks still pass with SDK stand-ins. This review changes no native code and does not repeat or claim physical acceptance.

## Control gradients, Auto and Artwork follow-up

Source Home0.3.49/native0.4.38: one shared full-frame SVG fades behind the clock and brightness, with no internal rectangular clipping. The builder versions local SVG/image/font references as well as scripts/styles. Browser checks over light simulated YouTube playback confirm smooth edges, clock contrast, quick Right→Left→Enter Auto activation, including during sidebar-entry confirmation, click toggle off, and Artwork stand-in/Back. SDK stand-ins verify Artwork's empty-data default app-control target; copied Art CIL traces the empty mode to its selection browser. Both native projects compile. These are source/package/browser checks, not installed TV acceptance.

Read-only TV evidence establishes the current pin-cache permission barrier and stale projection. The Auto getter reports availability/off. A bounded timing probe refused to write because native Art was active; no Auto value changed. Actual TV Auto on/off in normal mode, Artwork launch/return, native SVG appearance and a future pin-routing refactor remain unaccepted. The normal SDK/certificate profile is not configured on this host, so this candidate was not installed; the signed recovery pair is preserved.

Focused shared-controller checks cover pending Auto intent after a confirmed read, paired presses cancelling, suspension cancelling, and Art refusal causing no setter. The native controller uses the same intent rule and cancels it on pause; physical acceptance remains pending. An isolated SVG-only edit changes both its referenced digest and stylesheet digest. Both packages contain identical full-frame scrim bytes; signed recovery packages remain unchanged.

## Auto response and focus-state correction

Source Home0.3.50/native0.4.39: simulator Auto confirms 50%, moving the slider and changing its simulated picture; manual steps switch it off and Auto restores the demo-room target. Browser checks confirm 70%→Auto50%, manual40%→50%, and Auto returning to50%, with dark focused and translucent unfocused capsules. Auto has a separate enabled dot. Focused rendering checks verify repeated level/busy updates do not rewrite its pressed state. SDK/device stand-ins verify the confirmed value changes; production includes no simulator target. Matching native visuals compile and are now installed; physical appearance remains unaccepted. Actual sensor-output readback is an outstanding hardware contract.

## Previous paired installation

Home0.3.50/native0.4.39 was installed. The existing saved pair matched the TV before replacement; all53 new non-signature files matched readback afterward, and Home launch reported success. The prior pair is retained for recovery. This local update reused the exact installed author/distributor certificate chains with a private packaging bridge; it does not validate the public SDK-profile signing path. No firmware, routing or TV settings were changed by the updater. Physical Auto/manual appearance, Artwork return and lifecycle checks remain pending.

## Auto profile restoration correction

Home0.3.51/native0.4.40 adds verified active-profile default backlight restoration when enabling Auto. The owner confirms Artwork works again. A bounded actual-backend TV probe confirms default28/50, manual23/50 with Auto off, Auto restoring28/50, and Auto off holding28/50; it restores the original10/50 and sensor flag afterward. Read-only static/runtime evidence establishes the AVOC getter’s default selector and0–50 scale for this firmware. No database, other picture settings or Art settings were written. Matching source/native compile checks pass. The corrected pair is now installed; all53 non-signature files match TV readback and Home launch succeeds. Physical slider/brightness response and sensor adaptation remain pending attended acceptance.

## Auto state glyphs

The owner confirms Auto works in0.3.51/0.4.40. Home0.3.52/native0.4.41 replaces the sun-and-moon/dot with a shared sun-A icon: outline off, filled white centre/dark A on. Focus remains the existing dark capsule and size change. Browser checks confirm off/on glyph visibility, centred32px dimensions and the unchanged confirmed50% demo target. Both native projects compile without warnings/errors. Both packages are installed, all55 non-signature files match TV readback, Home launch succeeds and both packages contain identical state SVGs. Brightness backend is unchanged; native visual acceptance remains pending.

## Full-frame local backlight simulation

Development-only visual backlight now applies to the whole16:9 stage rather than two partial layers. Browser checks confirm Home at0%/100% maps to a0.15/1 brightness filter, the page background stays unchanged, and simulated YouTube with its sidebar dims at40% and returns to50% with Auto. Home/controller source remains byte-identical to the installed production bundle; the simulator is excluded from that bundle. No TV commands, setting changes or deployment were performed for this preview correction.

## Publication security review

A source-only hardening change closes the actionless `brightness_key` caller-check bypass. Both owned sender IDs pass; external or missing sender IDs fail before dispatch, including malformed or missing action cases. Refusal replies with failure and does not pause the sidebar. No trusted Samsung channel sender is inferred from an entry label; background routed channel keys require identity evidence and attended acceptance before deployment. The installed TV pair is unchanged. The isolated public checkout compiles both native entrypoints with zero warnings/errors. A temporary harness executes the exact dispatcher guard over30 owned/foreign/missing-sender, action and reply cases; refused callers reach neither hardware dispatch nor UI pause. Production inspection packages exclude simulator and signing/private assets.

Publication uses a new Git repository created from the reviewed allowlisted source export, with a GitHub no-reply author email. Private development commits, metadata and recovery files are not copied. Privacy review covers the exported source, asset metadata and every new public commit; it does not rewrite private history.

## Auto icon family refinement

Home0.3.53/native0.4.42 reuses the exact curved ray and centre contours from brightness-icon.svg for both Auto states, with only an 18% centre enlargement and the A added. Browser off/on checks pass and the preview loads the newly hashed SVGs. Both native projects compile; the final icon-only pair excludes unrelated in-progress native hardening. Physical icon appearance remains pending; the brightness backend is unchanged.

## Unchanged-sun Auto badge preview

The previous enlarged centre still looked unlike the existing sun. The revised preview preserves brightness-icon.svg’s complete path and viewBox exactly, overlaying only a corner A badge. The badge is outlined off and filled on. Source equality checks and browser off/on, new asset hashes and confirmed50% simulator response pass. This revision is preview-only; no TV commands or deployment were performed.
