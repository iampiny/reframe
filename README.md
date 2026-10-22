# Reframe

An experimental, standalone Home screen and transparent playback sidebar for Samsung Frame TVs. Existing streaming apps handle accounts and playback. A desktop preview runs the production Home UI with simulated device responses, without a TV.

Reframe is an independent project, not affiliated with or endorsed by Samsung. Samsung and The Frame are trademarks of their respective owners.

The implementation was investigated on a **2021 Frame, Tizen 6.0, ARMv7, firmware T-NKM2DEUC-2301.1**. Private Samsung interfaces are firmware-specific. Other models and firmware versions are not validated.

## Try the preview

Install Python 3.10+ and Node.js, then run:

```sh
npm run preview
```

Open `http://127.0.0.1:8765/`. Source changes rebuild and reload the page automatically. No npm packages, design tools, accounts or TV connection are required. The preview uses fictional recommendations and optional local icon fixtures. Arrow keys navigate; Enter opens a simulated app; H opens its sidebar, then full Home; R or Backspace/Delete dismisses the playback sidebar. It simulates interaction, not streaming or hardware behavior.

## Source layout

- `app/`: readable Home HTML, CSS, JavaScript, shared `ui-layout.json`, and project-owned assets.
- `native/`: playback sidebar/lifecycle, feed reader, guarded hardware brightness access, and bounded startup candidate.
- `demo/`: fictional, offline preview data; excluded from TV packages.
- `dev/`: asynchronous simulated device responses, keyboard mappings and external-app stand-ins; excluded from TV packages.
- `tools/`: standalone build, SDK signing, paired deployment and source release.
- `docs/`: [developer research handbook](docs/research/README.md), [setup](docs/SETUP.md), [architecture](docs/ARCHITECTURE.md), [validation](docs/VALIDATION.md), [recovery](docs/RECOVERY.md), [privacy/release](docs/RELEASE.md), and [critique](docs/PROJECT-CRITIQUE.md).

Layout tokens drive both renderers. Browser and native TV windows use different platform APIs; they are not the same widget implementation. No Pen export or Pen MCP is involved in editing, building or running this version.

## Developer research

The [research handbook](docs/research/README.md) contains recovered interfaces and synthetic examples for [Art Mode](docs/research/art-mode.md), [content feeds/deep links](docs/research/content-feeds.md), [Home/startup/pins](docs/research/platform-and-routing.md), [brightness](docs/research/brightness.md), and [development/performance/validation](docs/research/development-and-validation.md). It distinguishes static analysis, installed diagnostics, physical observations and unresolved proposals. [Evidence and status](docs/research/evidence-and-status.md) reconciles older failed/pending notes with later results.

These are original technical explanations, not Samsung firmware or a complete map of every TV function. Personal data, runtime payloads, copied decompiler output, proprietary binaries and signing material are excluded. [Artwork source research](docs/research/art-sources.md) retains record/license links without distributing images.

## Status

The public runtime source remains **Home 0.3.53 / native 0.4.42**, including a source-only caller-authorization correction. This documentation update does not change runtime code or the TV. The last readback-verified reference installation was **Home 0.3.54 / native 0.4.43**, with later brightness input changes but without that caller correction. Do not assume reference-TV observations validate this exact public build.

On the configured reference TV, the owner confirmed transparent navigation over uninterrupted Netflix/YouTube playback, Home/Back behavior, direct Settings/Apps, deliberate Samsung-bar visits, manual/Auto brightness response, exact Netflix title/YouTube video opening, personal-photo display, native Art light adaptation and Home idle-to-Art entry. Selection continuity works but has visual/timing issues; Art exit can show seconds of black screen/flicker. See the [capability ledger](docs/research/evidence-and-status.md#capability-ledger) for the precise scope.

Automatic cold startup, shared Samsung pin ordering, a custom Art gallery/category navigation, motion-specific wake and explicit latest-feature standalone/network-recovery tests remain unfinished or unaccepted. Normal-viewing sensor adaptation remains separate from the accepted Art light test. Public background `brightness_key` requests refuse non-owned senders; the observed Samsung manager identity still needs scoped authorization/acceptance. Fresh SDK-profile first-install signing is not yet validated end to end.

**Installing the packages alone does not replace Samsung's Home or boot policy.** Reference routing was separately researched; a portable routing installer is not implemented. Use the ordinary app launch path and read the [routing limits](docs/research/platform-and-routing.md) before attempting firmware integration.

## Build for a TV

Read [setup](docs/SETUP.md) first. Obtain your own Samsung SDK tools, reference assemblies and certificate profile. Local endpoint/Plex configuration belongs in ignored `local.json` or environment variables. No vendor binaries, certificates or signing keys are distributed here.

```sh
npm run build
```

[Deployment and recovery](docs/RECOVERY.md) explain the verified-pair updater and first-install limitation. The TV can run the installed apps without a development computer; Plex's own server remains a dependency of its content.

## License and assets

Project code is licensed under MIT. Inter uses SIL OFL and the interface glyphs derive from Lucide/Feather; see [third-party notices](THIRD-PARTY-NOTICES.md). App artwork is read from installed apps and is not distributed in this source. No recommendation snapshots, artwork URLs, account data or provider tokens are included.
