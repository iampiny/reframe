# Public source release

Use `npm run release:source` to create a fresh source archive from a Git checkout. The exporter includes only Git-tracked files inside the public root allowlist, using their current working-file contents. Stage reviewed new source files before exporting; untracked files are never included. It contains app/native sources, project-owned assets, demo data, development simulator, build tools, current guides and license notices. It excludes local configuration, build outputs, SDK/vendor binaries, certificates, recommendation snapshots, runtime journals, private research, and all Git history.

The existing development checkout and Git history are **not suitable for a direct public push**. History includes personal author identity, local paths, device addresses, previous imported app artwork and private investigation context. Cleaning HEAD does not clean old commits. Publish the reviewed source archive into a fresh public repository; do not copy `.git/`, the original research tree or signed local packages. No history rewrite is performed by this preparation.

Before publishing:

- Confirm the project license and review all third-party notices.
- Inspect the source archive and privacy-scan result, including new files added since the last review.
- Keep credentials/account feeds and personal data in ignored local/private files.
- Validate SDK-profile signing and ordinary first installation on a fresh developer setup.
- Keep the documented firmware/model limits and unfinished routing/activation features visible.
- Check the current owner acceptance batch before claiming tested TV support for the cleanup.

App icons come from installed applications at runtime. Their names/trademarks belong to their owners; project licensing does not grant brand-artwork rights. Third-party TV firmware, copied native libraries, signing identities and research logs are not part of the open-source distribution.

The extracted source archive can build its preview without Git. Creating another source release requires a Git checkout so source membership stays explicit. Automated checks catch known patterns; manual review is still necessary for personal data, tokens and asset rights.

The source release includes the development simulator and watcher so contributors can work locally. Production TV archives include only the selected TV device boundary; development fixtures, simulator styling/code and reload polling are excluded.

## Create the public checkout

Extract the reviewed source archive into a new directory and initialize a new Git repository there. Never clone the private checkout or copy its `.git`. Set the new repository’s local `user.email` to your GitHub no-reply address before the first commit; do not change the private checkout’s author configuration. Commit only the exported file list, then inspect the initial commit and author metadata. Push only the new repository’s `main` branch, without tags or references from the private checkout.

Private changes are promoted by a fresh reviewed source export, not by merging the private branch or adding it as a remote. Review both additions and deletions against the public tree before committing subsequent updates. Keep the private exporter’s manifest outside the public Git repository as local review evidence.

The public project name is Reframe. Preserve that branding and the repository-local no-reply author when promoting development changes. Existing `Fr4meHome1` and `org.framehome.overlayprobe` application IDs remain stable integration identifiers; changing them requires a separate TV migration.
