# Job Setup

See the [repository README](../README.md) for installation and usage. The plugin is separate from Hotbar Bridge and can be installed independently.

`/jobsetup` provides separate hotbar and Wrath operations, each for the current class/job or all supported classes/jobs. Every operation requires preview then application. Hotbar assignments use reviewed native upgrade roots rather than copying Wrath's transient action icons. No keybindings are changed.

Wrath integration writes its configuration only while Wrath is disabled. IPC does not support the detailed option values required by this preset pack. Apply and restore have preflight and final loaded-state checks, compare the file against its preview, and create a backup before atomic replacement. Re-enable Wrath to load changes. Current-job application changes that job's preset membership/settings; global role/targeting options are explicit opt-in. Unknown fields and other jobs are preserved.

All-class hotbar application prepares 21 job layouts and nine inherited base-class layouts, including saved layouts for locked jobs. Crafting, gathering and BLU remain unsupported and are not touched. Game unlock requirements still govern using the assigned skills. Job destinations must not be natively shared; utility positions remain untouched.

Backups are local. Hotbar backups are character-partitioned; full Wrath backups are separate. Both restores are previewed and backed up again before applying. A full Wrath restore replaces the complete configuration, clearly labeled in its preview.

Optional Bridge IPC supplies its cross map and brackets bulk slot writes. The API consists of `HotbarBridge.GetMapping` (JSON), `HotbarBridge.BeginExternalEdit` and `HotbarBridge.EndExternalEdit`. A missing/old loaded Bridge fails the operation instead of racing an unknown sync engine.

Build and logic tests pass. Native saved-hotbar writes, action resolution and actual Wrath reload behavior require in-game testing of the initial release.
