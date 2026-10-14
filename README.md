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
- `docs/`: [setup](docs/SETUP.md), [architecture](docs/ARCHITECTURE.md), [validation](docs/VALIDATION.md), [recovery](docs/RECOVERY.md), [privacy/release](docs/RELEASE.md), and [critique](docs/PROJECT-CRITIQUE.md).

Layout tokens drive both renderers. Browser and native TV windows use different platform APIs; they are not the same widget implementation. No Pen export or Pen MCP is involved in editing, building or running this version.

## Status

This public source includes a caller-authorization correction that has not been installed on the reference TV. Background manager-routed channel keys are refused until Samsung’s sender identity is verified; brightness controls and captured keys inside our own UI are unaffected.

The current installed pair is **Home 0.3.53/native 0.4.42**. All55 non-signature files matched TV readback and Home launch succeeded. Auto restores the active picture profile’s default backlight before enabling the sensor. The owner confirms Auto works; physical appearance of the refined Auto icon and sensor adaptation remain pending. Updates reuse this TV’s existing private signing identity; portable SDK-profile setup still needs validation. The local preview has a subsequent corner-badge icon refinement that is not yet installed on the TV.

Established earlier: transparent sidebar over uninterrupted Netflix/YouTube playback; Home/Back handling; Settings and native Art returns; manual brightness; real Netflix/YouTube feed reads; Netflix selected-title launch. The latest YouTube fragment correction, cursor handoff, some channel cases and Auto sensor behavior still need attended acceptance. Automatic cold startup, Samsung pin/order synchronization, and a custom artwork gallery remain unfinished. The Artwork shortcut currently targets Samsung’s selection browser; synced move/remove needs the routing work described in ARCHITECTURE.

**Installing the packages alone does not replace Samsung's Home or boot policy.** Existing research registration is not a public, portable installer. The public setup provides an ordinary app launch path; it does not automate firmware-specific routing changes or request new privileges.

## Build for a TV

Read [setup](docs/SETUP.md) first. Obtain your own Samsung SDK tools, reference assemblies and certificate profile. Local endpoint/Plex configuration belongs in ignored `local.json` or environment variables. No vendor binaries, certificates or signing keys are distributed here.

```sh
npm run build
```

[Deployment and recovery](docs/RECOVERY.md) explain the verified-pair updater and first-install limitation. The TV can run the installed apps without a development computer; Plex's own server remains a dependency of its content.

## License and assets

Project code is licensed under MIT. Inter uses SIL OFL and the interface glyphs derive from Lucide/Feather; see [third-party notices](THIRD-PARTY-NOTICES.md). App artwork is read from installed apps and is not distributed in this source. No recommendation snapshots, artwork URLs, account data or provider tokens are included.
