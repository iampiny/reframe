# Samsung Frame developer research

This handbook publishes the reusable findings behind Reframe: recovered interfaces, working request shapes, failed approaches, and the evidence needed to extend them. It is a reference for building features, **not a claim that the entire TV has been reverse engineered or a portable Home-replacement installer**.

Reconciled **22 October 2026**, from retained investigation and acceptance records dated 11–15 October. This publication performs no new TV tests or settings changes. Unless a section explicitly says otherwise, findings apply to a **2021 Samsung Frame LS03A, Tizen 6.0/ARMv7, firmware T-NKM2DEUC-2301.1**. Similar model names or newer SDK documentation do not establish compatibility.

## Start here

| Goal | Read |
| --- | --- |
| Understand what is actually known and where code differs from the reference installation | [Evidence and current status](evidence-and-status.md) |
| Replace Home/Back interactions, understand startup, Settings, stock Samsung visits, pins and permissions | [Platform and routing](platform-and-routing.md) |
| Build an Art catalogue/gallery, import personal images, select native Art, preserve power/sensors | [Art Mode](art-mode.md) |
| Find artwork/photography and retain source/license records | [Artwork sources](art-sources.md) |
| Read recommendations/Continue Watching and open the selected Netflix/YouTube item | [Content feeds and activation](content-feeds.md) |
| Control normal-viewing backlight and Auto without confusing them with Art settings | [Brightness](brightness.md) |
| Extend the app cleanly, avoid measured rendering traps, prepare reproducible physical tests | [Development and validation](development-and-validation.md) |

For ordinary build/install commands use [Setup](../SETUP.md), for source ownership use [Architecture](../ARCHITECTURE.md), and for package rollback use [Recovery](../RECOVERY.md). Read the limitations before attempting a firmware integration.

## Evidence labels

- **Static:** a named member, symbol, resource or parser was inspected in a matching shipped artifact. It establishes that code path, not permission or successful execution by our app.
- **Runtime:** a bounded installed-identity diagnostic returned the expected result. Scope includes the caller, exact operation, state and firmware; a successful connection alone is not a successful command.
- **Physical:** a person observed the picture, remote response, audio or light response on the reference TV. This does not replace timing instrumentation or establish every app/version.
- **Software:** build, source, mock, simulator or scheduling checks. This cannot establish on-screen output, remote priority, sensor behavior or another TV's permission policy.
- **Unverified/proposed:** a design or plausible interface still needs the stated proof. **Failed/limited** observations are retained with their scope; failure of one method is not proof that no method can work.

A claim may have more than one label. Where older notes conflict, the current status ledger states what was superseded and what remains unknown. Per-chapter evidence identifiers name the operation and source rather than linking to inaccessible local files.

## What is and is not distributed

Included: original explanations, public project code references, synthetic examples, native constant names, selected assembly identities/digests, reproducibility instructions, useful negative results and external source links.

Excluded: personal recommendations/profile names, photo inventories/IDs, artwork URL query strings, account material, device serials/MACs/LAN addresses, signing keys, raw journals, proprietary firmware/assemblies, copied decompiler listings, private Git history and machine-specific policy backups. An example `<fresh UUID>` or `<existing content ID>` is a placeholder, never a value to replay.

Some Samsung services log content internally even if a diagnostic emits only counts. Collect narrowly and review outputs before sharing them. A digest identifies a locally obtained artifact; it is neither a download link nor a guarantee that a different firmware uses the same interface.

## Design boundaries established by the investigation

Keep Samsung's existing streaming clients, media playback, native Art renderer, sensors, store and Settings backends. Reframe supplies navigation and presentation. A browser showing a photograph is not native Art Mode; launching an app is not opening a selected title; displaying a recommendation is not proof of resume/profile semantics.

Use the lowest existing interface that actually works under the installed caller identity. Preserve authorization checks, matching allocators/finalizers, response correlation, exact ownership checks for cleanup and a scoped inverse for deliberate changes. Do not infer a privilege from an open socket, Unix file mode or successful compilation.
