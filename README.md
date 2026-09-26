# Hotbar Tools

Two independent Dalamud API 15 plugins for keyboard/controller hotbar layouts.

**Initial test release:** 62 logic tests, local/CI builds and resolution of all 186 non-empty preset assignments against installed game data pass; game-memory writes still need in-game testing. Installation alone does not change hotbars or Wrath settings.

## Install

Add this URL in `/xlsettings` → Experimental → Custom Plugin Repositories:

```
https://raw.githubusercontent.com/kaldigo/HotbarTools/main/pluginmaster.json
```

Then find **Hotbar Bridge** and **Job Setup** in `/xlplugins`. Install either or both. Disable a development copy before enabling its repository copy.

### Hotbar Bridge — `/hotbarbridge`

Configurable two-way syncing between regular and cross hotbars, with individual shared slots and backups. Default job controls use regular bars 1/2 and cross set 1. Utilities use regular bar 10 plus bar 9 slots 1–4, mapped to cross set 2. Every pair is editable in game.

Turn on **Enable live two-way sync**. On first activation, review the initial alignment and click **Apply alignment and start LIVE sync**. After that the switch pauses/resumes continuous sync without another alignment, and the window can stay closed. Live status identifies OFF, watching, conflicts, combat and loading. The initial regular layout wins. Later edits propagate in either direction, including emptying a slot; simultaneous conflicting edits wait for a source choice. Sync pauses during combat and loading. First visits to another job initialize its mapped cross slots from regular slots; individual shared controls use the remembered shared assignments.

Your default regular bar 1 slots 9–12 become individually shared on both sides without making the entire bar shared. All ordinary job pairs require job-specific bars. Healers have a separate default mapping. Craft/gather classes only use utility/shared pairs until you configure other mappings.

### Job Setup — `/jobsetup`

**Compact keyboard layout** works for all supported jobs: fills bound keyboard positions while keeping ST, AoE, mobility and tank/healer reserves fixed. Controller positions stay fixed. Turn it off and apply to restore gaps. Update both plugins to 0.2.7 so Bridge follows the generated per-job routes.

One global set of three independent automation selections is available: **Auto burst**, **Auto mitigation** and **Auto job mechanics**. All 21 combat jobs have individual variants; a switch can deliberately make no additional change where the baseline already covers it. Use the combined preview to apply matching hotbars and Wrath settings. Unautomated manual controls remain available. See [variant review](job-setup/variant-review.md).

Separate **hotbar layouts** and **Wrath settings** previews, each with **current class/job** or **all supported classes/jobs** scope. Includes 21 combat jobs, eight crafters, Miner, Botanist, Fisher and nine base-class aliases. Blue Mage is excluded. Noncombat layouts use 12 job slots on one page; see [buttons and usage](job-setup/noncombat-hotbars.md) and [documented omissions](job-setup/noncombat-omissions.md).

- Only planned job positions are overwritten; the four shared utility controls and side utility bars are preserved.
- All-class hotbar application includes saved layouts for locked classes/jobs. Those actions remain unusable until unlocked. Base-class assignments inherit their job layout, with native upgrade roots where appropriate.
- Initial full setup needs Wrath unloaded. Subsequent reviewed preset-only variant changes use live Wrath commands by default, without unloading. External config-file replacement always requires unloaded Wrath. Disable/re-enable it manually, or opt into **Automatically unload/reload Wrath (experimental)**. The internal adapter checks compatibility and awaits unload completion; loaded-state checks remain immediately before replacement. In-game testing is still required.
- Current-job Wrath application preserves other jobs and PvP presets. Global targeting/role settings require the separate, clearly labeled checkbox.
- Presets were reviewed against Wrath 1.0.4.26 (configuration schema 6). Later feature changes require reassessment; do not assume schema compatibility guarantees every option still exists.
- If Bridge is installed, Job Setup uses its applicable cross mappings and pauses synchronization around bulk changes. Otherwise cross destinations are configurable directly.

## Backups

Both plugins save hotbar backups in their own Dalamud configuration folders, partitioned by a local hashed character key. Each has a preview/restore interface. Wrath backups are full configuration snapshots, and restoring one replaces all Wrath settings. Never commit personal snapshots or backups.

The read-only Bridge export remains available with `/hotbarbridge export` for diagnosis. It exports no character name or content ID. No plugin transmits settings or snapshots over the network.

## Develop

Requires .NET 10 SDK and Dalamud API 15 development assemblies in the normal XIVLauncher location, or `DALAMUD_HOME` pointing at a compatible development directory.

```
dotnet build hotbar-sync/HotbarBridge/HotbarBridge.csproj -c Release
dotnet build job-setup/JobSetup/JobSetup.csproj -c Release
dotnet run --project tests/HotbarTools.Tests -c Release
python tools/package_release.py --repository kaldigo/HotbarTools --tag v0.2.1
```

Optional installed-game action validation:

```
dotnet run --project tests/ActionCatalogCheck -c Release -- "<game>/sqpack"
```

This rejects obsolete/NPC/PvP action-name collisions and checks job eligibility. It requires installed game data and is not run on GitHub runners.

`common/` is linked source, not a third plugin. The packaged curated presets are in `job-setup/JobSetup/presets.json`. `tools/generate_presets.py` is an authoring tool for the private planning workspace; building this repository uses the checked-in preset pack and does not require that workspace.

Tagging `v*` runs the release workflow, builds/tests both projects, creates ZIP assets and updates the repository feed. CI uses the current API-15 stable development download; it must be reassessed when Dalamud changes API levels. Review releases before distributing them widely.

## References and authorship

[Dalamud development](https://dalamud.dev/) · [Custom repository format](https://dalamud.dev/plugin-publishing/custom-repositories/) · [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) · [Wrath IPC limitations](https://github.com/PunishXIV/WrathCombo/blob/main/docs/IPC.md).

This project was developed with AI assistance. It is an independent custom-repository project, not affiliated with Dalamud or Wrath Combo.

Version 0.2.7 adds a Noncombat sync profile. Existing custom mappings are preserved; only non-overlapping default pairs are added once. An intentionally empty map stays empty. If a custom All-profile pair blocks a job position, adjust the map in Bridge before applying that layout. Bard burst, Dancer burst and Black Mage mitigation change detailed settings and still require unload/reload; the remaining reviewed preset-only transitions support live updates.

Job Setup 0.2.8 uses the same global automation selections for current-job and all-job application. Omission notes remain in documentation. On upgrade the new global switches start off; existing game settings are unchanged until application. Hotbar Bridge remains at 0.2.7.

Job Setup 0.2.9 adds `/jobsetup current MBJ`, `/jobsetup all MBJ` and per-switch `on|off|toggle` macro commands. The server-info entry uses native mitigation/burst/job icons and hides when all switches are off. Version 0.2.10 re-checks the active job on class changes; unapplied global selections do not alter its display. See [macro syntax and behavior](job-setup/README.md#macro-commands-and-server-info-icons-029).
