# Validation and evidence

The [capability ledger](research/evidence-and-status.md#capability-ledger) is the current source of truth for verified, limited and unverified behavior. It reconciles historical research through 15 October and was edited 22 October 2026. This documentation release ran no new TV tests, installed no packages and changed no TV state.

## Source/installation boundary

Public source: Home 0.3.53 / native 0.4.42 plus source-only caller hardening. Last verified reference installation: Home 0.3.54 / native 0.4.43 with queued/held brightness input and the earlier caller guard. Neither the public caller correction nor later input behavior should be inferred accepted merely because the other setup was tested.

## What counts as evidence

- Build/source/mock checks establish software behavior only.
- An installed diagnostic establishes its exact operation under its recorded identity/state, not arbitrary firmware APIs.
- A visible/input/audio/sensor report establishes that physical scenario, not every version/profile/launch state.
- Package byte comparison and launch acknowledgement establish installation/invocation, not displayed content, responsiveness or sensor output.

Use [the handbook evidence definitions](research/README.md#evidence-labels), [reconciled status](research/evidence-and-status.md), and [prepared validation batches](research/development-and-validation.md#prepare-these-batches-before-requesting-a-person). Domain evidence/member identities are retained in the Art, feed, routing and brightness chapters. Original personal reports, photos/provider data, signing keys and vendor code are not public fixtures.

## Existing software verification

Retained checks cover JavaScript syntax, both native entrypoint compilations against the copied target-era references, separate preview/production package contents, asset cache hashes, request deadlines/error handling, late lifecycle callbacks, privacy export boundaries and simulated paired rollback. The reference deployment read back 55 non-signature files and confirmed Home launch. Later brightness input verification passed 11 extracted-web-function cases and 7 native-method stand-in cases; that runtime update is not part of this public documentation release.

These are scoped results, not a permanent regression suite or acceptance of public fresh SDK signing. Current source/publication checks and new physical acceptance should always record the exact version/artifact and their own result.

## Remaining physical work

Fresh cold startup; Samsung move/remove synchronization; Art exit timing/flicker; motion-specific wake; normal-picture Auto adaptation; rapid/held/channel input across contexts; initial overlay focus on extra pins; Netflix episode/resume/profile; broader YouTube warm/cold behavior; idle input-reset/playback exclusion; explicit standalone/network recovery. Some are implementation gaps before they can become acceptance passes. Do not ask a person to repeat already accepted paths without a new regression reason.

Complete screenshots are also unresolved: the tested native capture contained only our overlay, excluding the underlying app. A smooth local preview or overlay-only screenshot cannot validate full composed-TV appearance.
