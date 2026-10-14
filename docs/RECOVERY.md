# Deployment and recovery

`tools/deploy.py` updates two already-installed applications as one pair. It first builds/signs both replacements and saves per-attempt evidence, then reads back the saved baseline before installation. It verifies every non-signature installed file and requires Home launch confirmation. If installation, readback or launch fails, it restores and verifies both previous packages. The saved installed pair advances only after all three checks pass. Unsigned deployment is rejected before any TV command. It never modifies TV routing policy.

Keep `private/deployments/installed-pair/` and per-attempt directories private. They may contain device-specific signatures, runtime evidence, and Plex configuration. They are not public release artifacts. Do not replace the known installed pair with unsigned inspection archives.

The first-install path is Samsung's normal SDK installation; see SETUP.md. Uninstalling an app removes the ordinary launcher. If separate Home/Art/Settings routing research has been applied, uninstalling alone does not restore those routes. Use the **exact backup and inverse for that TV's existing changes** before uninstalling. A portable routing rollback installer is not implemented, and no universal command is claimed.

Keep the native Samsung UI reachable through the menu while experimenting. Never restore a database copied from another television. Do not treat a compiler/deployment check as acceptance of remote input, playback continuity, sensor behavior or cold startup.
