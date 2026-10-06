# Job Setup

See the [repository README](../README.md) for installation and usage. The plugin is separate from Hotbar Bridge and can be installed independently.

`/jobsetup` provides separate hotbar and Wrath operations, each for the current class/job or all supported classes/jobs. Every operation requires preview then application. Hotbar assignments use reviewed native upgrade roots rather than copying Wrath's transient action icons. No keybindings are changed.

Direct Wrath configuration-file replacement only occurs while Wrath is disabled; reviewed live preset commands are described below. IPC does not support the detailed option values required by this preset pack. Apply and restore have preflight and final loaded-state checks, compare the file against its preview, and create a backup before atomic replacement. Re-enable Wrath to load changes. Current-job application changes that job's preset membership/settings; global role/targeting options are explicit opt-in. Unknown fields and other jobs are preserved.

All-class hotbar application prepares 32 layouts and nine inherited base-class layouts, including saved layouts for locked jobs. The 32 comprise 21 combat jobs, eight crafters and three gatherers. BLU remains unsupported and is not touched. Game unlock requirements still govern using the assigned skills. Job destinations must not be natively shared; utility positions remain untouched.

Backups are local. Hotbar backups are character-partitioned; full Wrath backups are separate. Both restores are previewed and backed up again before applying. A full Wrath restore replaces the complete configuration, clearly labeled in its preview.

Optional Bridge IPC supplies its cross map and brackets bulk slot writes. The API consists of `HotbarBridge.GetMapping` (JSON), `HotbarBridge.BeginExternalEdit` and `HotbarBridge.EndExternalEdit`. A missing/old loaded Bridge fails the operation instead of racing an unknown sync engine.

Build and logic tests pass. Native saved-hotbar writes, action resolution and actual Wrath reload behavior require in-game testing of the initial release.

## Job variants

Variant definitions are reviewed per job in `variants/`; the generator embeds them in the portable preset pack. All 21 combat jobs now have reviewed variants. Crafting and gathering expose the same switches as documented no-ops. The original per-job planning document remains the baseline.

Auto burst and Auto mitigation are independent switches, saved per job. Auto job mechanics is a third category: the user can select it even when it changes nothing. Gunbreaker documents no additional change because cartridge spending, Gnashing Fang and damage follow-ups are already automated in its baseline. It is not a switch to disable the core ST/AoE consolidation. These modes still require repeated action-button presses; they do not activate Wrath's hands-free auto-rotation.

- Auto burst: enables ST/AoE No Mercy and Bloodfest. Logical burst slot 3 (keyboard 4 / RT+D-Up) is emptied. Turning it off restores the manual No Mercy/Bloodfest button. Scripted openers remain off.
- Auto mitigation: adds pack Rampart, Great Nebula and Reprisal. Existing enemy-count, enemy-HP, movement, active-mitigation and weave conditions still apply. Adds boss tankbuster Rampart/Great Nebula under the baseline casual-content filter; Great Nebula is preferred and recent-use checks space these two. Keeps the mitigation selector and targeted Corundum, Superbolide and party-defense access. Cooldowns are shared with automatic use. Boss party mitigation and Arm's Length stay manual. Turning this switch off restores the existing Aurora/pack-Camouflage automation; it does not disable all routine defense.
- Both on combines those changes without changing mobility, enmity, stance or Shirk.

Use **Preview variant: hotbars + Wrath together** to apply matching selections as a pair. One global set of three switches supplies the selections for either the current job or all jobs, according to the scope control. Individual hotbar/settings operations remain available. Combined application preflights Wrath, pauses Bridge around hotbar writes, backs up both and rolls back hotbar changes if the settings write fails. In-game validation remains pending.

Dalamud's public API 15 `IExposedPlugin` exposes loaded state and UI entry points, not load/unload methods. Its core commands do not offer per-plugin enable/disable. Job Setup therefore provides **Open Wrath in plugin installer**, retaining manual disable/re-enable and checks immediately before the write. Version 0.2.6 adds a separate opt-in experimental internal adapter described below; pausing rotation is never treated as unloading configuration.

Sources: [public plugin interface](https://dalamud.dev/api/Dalamud.Plugin/Interfaces/IExposedPlugin/), [Dalamud commands](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Interface/Internal/DalamudCommands.cs), and [reviewed GNB behavior](https://github.com/PunishXIV/WrathCombo/blob/55d55a3ebc48c1b6018b445b58a89ccfd84c3cbf/WrathCombo/Combos/PvE/GNB/GNB_Helper.cs).

All three switches remain user-facing for each reviewed job. Selecting every switch means all reviewed supported automation, not removing uncovered functions. A hotbar slot is only cleared when its specific function is covered and that job's review approves its removal. Unsupported/manual mobility, targeted support, invulnerability and tank utility remain available.

## Optional keyboard compaction (0.2.6)

Compaction applies to every supported job and base class, independently of whether its automation variants have been reviewed. It preserves every non-empty function, fills bound keyboard positions 1-4 then the Shift positions in their existing order, and clears vacated job positions. ST, AoE, actual mobility, and tank/healer general reserves remain fixed. A missing mobility action does not reserve a gap. Non-general extras (Samurai Meditate and Reaper Soulsow) may compact. Controller function positions never move. Shared keyboard 9/0/-/= and side utilities are untouched. The noncombat presets have twelve occupied slots, so compaction does not change their baseline assignments. No keybindings change; BLU stays excluded.

Turn **Compact keyboard layout** off and apply to restore the original spacing. Both directions must be applied when Bridge is loaded. Update both plugins: Bridge 0.2.6 persists per-job routes separately from its configurable base map. Job Setup validates the base map and route snapshot before writing. Its `layout-*.json` backups include assignments and prior routes; choose these for a complete restore. Older plain slot backups preserve the current routes.

## Experimental automatic Wrath reload (0.2.6)

The optional **Automatically unload/reload Wrath (experimental)** setting uses reflection into Dalamud's internal PluginManager and LocalPlugin lifecycle. It resolves the exact installed method signatures and requires exactly one Wrath instance in a stable loaded state before starting. This is not a public API and may stop working after Dalamud updates. Local API-15 XML signatures match the adapter; actual lifecycle behavior requires in-game validation.

For preview or application, Job Setup waits asynchronously for a completed unload, then performs the operation on the framework thread with existing loaded-state/file-change guards, and restores Wrath. Already-disabled Wrath stays disabled. Failure stops writes and attempts restoration where the internal state permits it. If reload fails, use the installer shortcut and read the error. Closing the window does not stop an in-progress lifecycle. Unloading Job Setup during an operation schedules restoration after the pending transition. No profile preferences are changed.

Implementation evidence: [LocalPlugin lifecycle](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Internal/Types/LocalPlugin.cs). This opt-in workaround supersedes the earlier public-API-only limitation; the manual path remains supported.

## Initial setup, then live variant switching

**Prefer live Wrath updates when supported** is on by default. Apply the full curated setup initially with Wrath unloaded (manually or using the optional lifecycle adapter). Later variant changes can stay live when the complete requested configuration differs only in reviewed preset switches. This covers reviewed preset-only transitions across all combat jobs, including removing/restoring conflicting manual selectors. Since 0.2.12, all detailed values are installed during base setup; every prepared variant transition changes only reviewed feature IDs.

Live application invokes Wrath's `/wrath set` and `/wrath unset` commands; Wrath updates its own configuration, not an external file replacement while loaded. Read-only `GetComboOptionState` verifies runtime state before and after; saved enabled IDs are checked as well. Unexpected state or IPC control stops the operation, and failures attempt to restore the previous switches and hotbars/routes. Backups still precede application. Direct config-file replacement always requires unloaded Wrath.

Variant operations never change custom values or fall back to unloading. Missing/outdated preparation or incompatible preset state causes a clear refusal; explicit initial setup is the separate path. This is why initial setup may require unloading even though switching reviewed variants afterward does not. Detailed numeric values and priorities are not exposed by Wrath's public IPC. Live support is deliberately scoped to reviewed transitions, with explicit per-job definitions.

Sources: [Wrath command handler](https://github.com/PunishXIV/WrathCombo/blob/55d55a3ebc48c1b6018b445b58a89ccfd84c3cbf/WrathCombo/Commands.cs), [preset changes and persistence](https://github.com/PunishXIV/WrathCombo/blob/main/WrathCombo/Core/Presets.cs), [IPC capabilities](https://github.com/PunishXIV/WrathCombo/blob/main/docs/IPC.md).

## Version 0.2.7 presets

[Automation behavior and retained controls](variant-review.md) describes each combat job. [Noncombat buttons and usage](noncombat-hotbars.md) contains only tables and usage; [scope and omissions](noncombat-omissions.md) lists every intentionally omitted action. The sources are `variants/*.json` and `noncombat/*.json`; crafting assignments resolve class-specific CraftAction commands and ordinary crafting buffs separately.

All 11 noncombat classes use one page with twelve job controls and four shared utility controls. Crafting is manual. Miner and Botanist share the same two Wrath gathering options. Fishing supports rod/spear context replacements but is not a complete ocean-fishing or big-fish toolkit. Omitted actions are documented outside the plugin UI. Update both plugins for the Noncombat mapping profile; custom conflicting mappings require user adjustment.

## Global automation controls (0.2.8)

There is one persistent set of Auto burst, Auto mitigation and Auto job mechanics switches. Changing job or scope does not change these selections. The current/all setting controls the jobs included in preview and application; each preset interprets the same selections using its own reviewed behavior. Switching a checkbox does not immediately change game settings. Legacy per-job selections are no longer used; the new global selection starts with all three off. Existing hotbars and Wrath settings stay as applied until the user previews and applies a selection.

## Macro commands and server-info icons (0.2.9)

Place one command in a game macro:

| Command | Result |
| --- | --- |
| `/jobsetup current MBJ` | Enable all three and apply to the current class/job. |
| `/jobsetup all MBJ` | Enable all three and apply to all registered classes/jobs. |
| `/jobsetup current BJ` | Enable burst and job mechanics, disable automatic mitigation, then apply to current. |
| `/jobsetup current none` | Turn all three off and apply the baseline to current. |
| `/jobsetup all none` | Turn all three off and apply the baseline to all registered jobs. |
| `/jobsetup current toggle M` | Toggle mitigation from the current job�s checked state, preserve its other switches, apply to current. |
| `/jobsetup all on BJ` | Enable burst/mechanics, preserve mitigation, apply to all. |
| `/jobsetup current off B` | Disable burst, preserve the other selections, apply to current. |
| `/jobsetup current` / `/jobsetup all` | Apply the existing global selections to that scope. |
| `/jobsetup help` | Print command syntax. |

M means mitigation, B means burst, J means job mechanics. Letters are case-insensitive and may be combined. An exact letter set replaces all three selections; `on`, `off` and `toggle` only change the named selections. These commands explicitly authorize immediate combined application, using the normal internal preview, backup, validation and rollback path. They do not require pressing Apply in the window. They preserve shared targeting options and the window's scope checkbox. Successful commands save the new global selections; rejected operations restore the preceding selections.

Run outside combat and loading. A command is rejected while another operation is queued or running; use a single combined command rather than several toggle lines in the same macro. Variant macros never unload/reload Wrath. Initial setup and full configuration restoration retain their separate reload requirements. Changing character or job during the operation cancels it before application. Commands do not activate hands-free rotation.

The server-info entry reads **Job Settings:** followed by native icons, in mitigation/burst/job-mechanics order: tank/shield, drawn sword, class/job. Disabled icons disappear; when all three are off, the whole entry hides. It also hides while logged out. Hover for the legend; click to open Job Setup. The entry checks the active job against Wrath on class/character change, after application, and every two seconds. It checks relevant live preset states through Wrath IPC and the registered setup revision. Unapplied global checkbox edits do not change the active-job display. The entry hides during application/loading, while Wrath is disabled, or when the current job cannot be matched to a reviewed combination. No-op switches cannot be inferred from identical Wrath settings, so these retain that job�s last successful application result; without a record they display off. These records are outcomes, not per-job user controls. Dalamud's server-info settings can independently hide or reorder the entry.

Build and logic checks cannot verify the appearance and placement of native icons in your game UI; test those in game.

Current-job `on`, `off` and `toggle` commands start from the checked active-job state. If it cannot be determined, use an exact selection such as `/jobsetup current MBJ`. All-job per-switch commands use the global selection because jobs may currently differ. Merely changing class does not apply anything or change the global selection.

## Base-setup registry (0.2.11)

Job Setup keeps an internal `ManagedJobs` record in its configuration, including the first/last successful application time and Wrath version. Only a successful complete preset application to Wrath registers a job. This may be the baseline or a selected variant of the complete preset. Previewing, applying hotbars alone, or a failed/rolled-back operation does not register it.

Use **Preview base setup: hotbars + Wrath** to initialize new jobs. The existing separate Wrath-settings application also registers jobs after success. These explicit setup operations honor current/all scope and may register new jobs. **Preview variant: registered jobs only**, every macro variant operation, and the server-info checks ignore unregistered jobs. An all-job variant command includes only registered jobs and their inherited base-class layouts; a current-job command on an unregistered job asks you to apply base setup first. Variant operations preserve global targeting settings.

The registry follows canonical Wrath jobs: a base class and its upgraded job share settings and registration. It is local to this installation, alongside Wrath's job settings. It does not change the one global set of user-facing switches. Existing successful application history from 0.2.10 is migrated once; unknown dates stay unknown. Similar-looking Wrath settings alone do not enroll a job. Jobs applied in older releases without recorded history need one explicit setup application to register them.

Restoring a full Wrath backup clears registrations and applied-choice history, because the restored file may predate our setup. Apply setup again for the jobs you want managed. Hotbar-only restores leave Wrath registration unchanged.

## Preset-only switching and queued saves (0.2.12)

Initial setup installs targeting, priorities, sliders and option arrays once. Variant previews/macros update only the reviewed feature IDs and corresponding hotbars. They preserve every custom field and unrelated preset choice, even if you changed them after initial setup. The live preference/reload options apply to full setup operations; prepared variants always use live commands while Wrath is enabled and never silently unload it.

**One-time update:** BRD, DNC and BLM require updated base setup (revision 2). Bard buff arrays are set once with automatic buff features off in baseline. Black Mage's Manaward trigger is set once, with the Manaward automation feature off in baseline. Dancer's automatic Technical Step options are prepared once, with its ST/AoE Technical features off in baseline; the dedicated Technical Step button instead handles all four dance steps and the finish. With automatic burst enabled, ST/AoE initiates and completes Technical Step. With burst disabled, press the Technical button repeatedly until finished, then resume ST/AoE. These are stable configurations across all switch combinations.

Wrath's pinned [save implementation](https://github.com/PunishXIV/WrathCombo/blob/55d55a3ebc48c1b6018b445b58a89ccfd84c3cbf/WrathCombo/Core/ConfigurationHelper.cs) queues saves. After sending commands, Job Setup waits across framework updates for both IPC and disk confirmation, up to ten seconds, without blocking the game thread. Only then are the hotbars committed. A failure starts a separately confirmed rollback of the affected switches; unrelated saved changes are ignored. Failure diagnostics distinguish a confirmed rollback from one requiring the saved backup. This replaces the erroneous immediate disk check that reverted valid queued changes.

## Shared healing targeting (0.2.13)

Apply shared targeting/role options with Preview Wrath settings to install the updated global healing priority: **focus → UI/party-list mouseover → controller soft target → selected friendly target → selected target’s friendly target → first living party tank → self**. World/nameplate mouseover is excluded. Wrath exposes UI mouseover, not a strict party-list-only filter. A valid focus wins even at full HP; clear/change it to heal another ally. The tank fallback does not identify the main tank, so focus the intended tank in two-tank groups. Shared options affect all jobs using this resolver; native actions, ground placements and separate job resolvers retain their documented behavior.

The portable shared settings now include `RetargetHealingActionsToStack`, previously omitted by the exporter. This detailed update uses the guarded initial-settings path (Wrath unloaded manually or through the optional automatic lifecycle); variant macros preserve targeting and stay live. No hotbar changes are required. Source-checked; recipient behavior needs in-game verification.

## Clear obsolete hotbar assignments (0.2.14)

In Job Setup, choose current/all jobs, variants and keyboard compaction, then open **Clean up old job hotbars** and select **Preview layouts and clear unused slots**. Applying the preview loads those layouts and empties other filled slots across all ten regular bars and eight cross sets for the selected jobs and base classes. For example, an obsolete action on Shift+8 is removed. This is a separate, deliberate hotbar-only operation; ordinary layout/variant applications do not run this cleanup, and Wrath settings are unchanged. Choose variants matching the settings you intend to use.

Hotbar Bridge must be loaded so its actual configured shared pairs can be protected, including customized or disabled shared pairs. Entire bars marked shared by the game are also preserved. Cleanup uses final layout destinations after compaction, not the original keyboard positions. It runs outside combat/loading, checks the character/job, mapping, native bar sharing and previewed values before writing, and pauses Bridge during the write. Backups include cleared assignments and the prior Bridge routes; use **Preview hotbar restore** to undo. No game assignments are changed until the preview is applied. Build/tests do not establish in-game validation.

## Machinist Auto burst correction (0.2.16)

Auto burst now enables Barrel Stabilizer in both advanced damage rotations, as well as Wildfire in ST. Previously it enabled only Wildfire, leaving Barrel and therefore access to Full Metal Field dependent on the manual button. Reapply `/jobsetup current on B` on Machinist after updating; this is a live feature-only update and needs no new base setup. Switching burst off restores manual Barrel/Wildfire initiation.

Existing detailed settings are preserved: the reviewed ST settings are boss-only for Barrel/Wildfire, and AoE Barrel requires target HP above 25%. Wrath still checks weave windows, Overheat and tool/cooldown timing. Wildfire has no automatic AoE branch; retain Heat Blast for that manual path. Scripted openers and automatic Flamethrower remain off. Source checked against the pinned implementation and upstream MCH helper on 2026-10-07; in-game timing remains unverified.
