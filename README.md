# Hotbar Tools

Two independent Dalamud API 15 plugins for keyboard/controller hotbar layouts.

**Initial test release:** logic tests and local builds pass; game-memory writes still need in-game testing. Installation alone does not change hotbars or Wrath settings.

## Install

Add this URL in `/xlsettings` → Experimental → Custom Plugin Repositories:

```
https://raw.githubusercontent.com/kaldigo/HotbarTools/main/pluginmaster.json
```

Then find **Hotbar Bridge** and **Job Setup** in `/xlplugins`. Install either or both. Disable a development copy before enabling its repository copy.

### Hotbar Bridge — `/hotbarbridge`

Configurable two-way syncing between regular and cross hotbars, with individual shared slots and backups. Default job controls use regular bars 1/2 and cross set 1. Utilities use regular bar 10 plus bar 9 slots 1–4, mapped to cross set 2. Every pair is editable in game.

Start with **Preview regular -> cross**, review, then **Apply preview and enable sync**. The initial regular layout wins. Later edits propagate in either direction, including emptying a slot; simultaneous conflicting edits wait for a source choice. Sync pauses during combat and loading. First visits to another job initialize its mapped cross slots from regular slots; individual shared controls use the remembered shared assignments.

Your default regular bar 1 slots 9–12 become individually shared on both sides without making the entire bar shared. All ordinary job pairs require job-specific bars. Healers have a separate default mapping. Craft/gather classes only use utility/shared pairs until you configure other mappings.

### Job Setup — `/jobsetup`

Separate **hotbar layouts** and **Wrath settings** previews, each with **current class/job** or **all supported classes/jobs** scope. Includes 21 combat-job presets and nine base-class aliases. Crafting, gathering and Blue Mage presets are not included yet.

- Only planned job positions are overwritten; the four shared utility controls and side utility bars are preserved.
- All-class hotbar application includes saved layouts for locked classes/jobs. Those actions remain unusable until unlocked. Base-class assignments inherit their job layout, with native upgrade roots where appropriate.
- **Disable Wrath Combo before applying or restoring its settings.** Re-enable it afterward. The plugin checks loaded state before preview and immediately before replacement.
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
python tools/package_release.py --repository kaldigo/HotbarTools --tag v0.2.0
```

`common/` is linked source, not a third plugin. The packaged curated presets are in `job-setup/JobSetup/presets.json`. `tools/generate_presets.py` is an authoring tool for the private planning workspace; building this repository uses the checked-in preset pack and does not require that workspace.

Tagging `v*` runs the release workflow, builds/tests both projects, creates ZIP assets and updates the repository feed. CI uses the current API-15 stable development download; it must be reassessed when Dalamud changes API levels. Review releases before distributing them widely.

## References and authorship

[Dalamud development](https://dalamud.dev/) · [Custom repository format](https://dalamud.dev/plugin-publishing/custom-repositories/) · [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs) · [Wrath IPC limitations](https://github.com/PunishXIV/WrathCombo/blob/main/docs/IPC.md).

This project was developed with AI assistance. It is an independent custom-repository project, not affiliated with Dalamud or Wrath Combo.
