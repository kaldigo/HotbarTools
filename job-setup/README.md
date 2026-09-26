# Job Setup

See the [repository README](../README.md) for installation and usage. The plugin is separate from Hotbar Bridge and can be installed independently.

`/jobsetup` provides separate hotbar and Wrath operations, each for the current class/job or all supported classes/jobs. Every operation requires preview then application. Hotbar assignments use reviewed native upgrade roots rather than copying Wrath's transient action icons. No keybindings are changed.

Wrath integration writes its configuration only while Wrath is disabled. IPC does not support the detailed option values required by this preset pack. Apply and restore have preflight and final loaded-state checks, compare the file against its preview, and create a backup before atomic replacement. Re-enable Wrath to load changes. Current-job application changes that job's preset membership/settings; global role/targeting options are explicit opt-in. Unknown fields and other jobs are preserved.

All-class hotbar application prepares 21 job layouts and nine inherited base-class layouts, including saved layouts for locked jobs. Crafting, gathering and BLU remain unsupported and are not touched. Game unlock requirements still govern using the assigned skills. Job destinations must not be natively shared; utility positions remain untouched.

Backups are local. Hotbar backups are character-partitioned; full Wrath backups are separate. Both restores are previewed and backed up again before applying. A full Wrath restore replaces the complete configuration, clearly labeled in its preview.

Optional Bridge IPC supplies its cross map and brackets bulk slot writes. The API consists of `HotbarBridge.GetMapping` (JSON), `HotbarBridge.BeginExternalEdit` and `HotbarBridge.EndExternalEdit`. A missing/old loaded Bridge fails the operation instead of racing an unknown sync engine.

Build and logic tests pass. Native saved-hotbar writes, action resolution and actual Wrath reload behavior require in-game testing of the initial release.

## Job variants

Variant definitions are reviewed per job in `variants/`; the generator embeds them in the portable preset pack. Gunbreaker is the first reviewed job. Other jobs retain their baseline until individually assessed. The original per-job planning document remains the baseline.

Auto burst and Auto mitigation are independent switches, saved per job. Auto job mechanics is a third category: the user can select it even when it changes nothing. Gunbreaker documents no additional change because cartridge spending, Gnashing Fang and damage follow-ups are already automated in its baseline. It is not a switch to disable the core ST/AoE consolidation. These modes still require repeated action-button presses; they do not activate Wrath's hands-free auto-rotation.

- Auto burst: enables ST/AoE No Mercy and Bloodfest. Logical burst slot 3 (keyboard 4 / RT+D-Up) is emptied. Turning it off restores the manual No Mercy/Bloodfest button. Scripted openers remain off.
- Auto mitigation: adds pack Rampart, Great Nebula and Reprisal. Existing enemy-count, enemy-HP, movement, active-mitigation and weave conditions still apply. Adds boss tankbuster Rampart/Great Nebula under the baseline casual-content filter; Great Nebula is preferred and recent-use checks space these two. Keeps the mitigation selector and targeted Corundum, Superbolide and party-defense access. Cooldowns are shared with automatic use. Boss party mitigation and Arm's Length stay manual. Turning this switch off restores the existing Aurora/pack-Camouflage automation; it does not disable all routine defense.
- Both on combines those changes without changing mobility, enmity, stance or Shirk.

Use **Preview variant: hotbars + Wrath together** to apply matching selections as a pair. All-job scope uses each reviewed job's saved selections and retains baseline presets for unreviewed jobs. Individual hotbar/settings operations remain available. Combined application preflights Wrath, pauses Bridge around hotbar writes, backs up both and rolls back hotbar changes if the settings write fails. In-game validation remains pending.

Dalamud's public API 15 `IExposedPlugin` exposes loaded state and UI entry points, not load/unload methods. Its core commands do not offer per-plugin enable/disable. Job Setup therefore provides **Open Wrath in plugin installer**, retaining manual disable/re-enable and checks immediately before the write. It does not use private reflection or confuse pausing Wrath rotation with unloading its configuration.

Sources: [public plugin interface](https://dalamud.dev/api/Dalamud.Plugin/Interfaces/IExposedPlugin/), [Dalamud commands](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Interface/Internal/DalamudCommands.cs), and [reviewed GNB behavior](https://github.com/PunishXIV/WrathCombo/blob/55d55a3ebc48c1b6018b445b58a89ccfd84c3cbf/WrathCombo/Combos/PvE/GNB/GNB_Helper.cs).

All three switches remain user-facing for each reviewed job. Selecting every switch means all reviewed supported automation, not removing uncovered functions. A hotbar slot is only cleared when its specific function is covered and that job's review approves its removal. Unsupported/manual mobility, targeted support, invulnerability and tank utility remain available.
