# Hotbar Bridge

See the [repository README](../README.md) for installation, usage and release status.

Core feature: in-game configurable two-way slot synchronization. Preset application belongs to the independent Job Setup plugin.

## Shipped mapping

All numbers below are 1-based. Cross set 1 is the job set; cross set 2 is the default utility set. The editor permits changing any pair.

| Regular | Combat cross slot | Healer cross slot |
| --- | --- | --- |
| 1:1 | 16 (RT+A) | 16 (RT+A) |
| 1:2 | 15 (RT+B) | 15 (RT+B) |
| 1:3 | 14 (RT+Y) | 13 (RT+X) |
| 1:4 | 12 (RT+Up) | 14 (RT+Y) |
| 2:1 | 13 (RT+X) | 10 (RT+Left) |
| 2:2 | 11 (RT+Right) | 12 (RT+Up) |
| 2:3 | 9 (RT+Down) | 9 (RT+Down) |
| 2:4 | 10 (RT+Left) | 11 (RT+Right) |
| 2:12 | 4 (LT+Up) | 4 (LT+Up) |
| 2:11 | 2 (LT+Left) | 2 (LT+Left) |
| 2:5 | 3 (LT+Right) | 3 (LT+Right) |
| 2:6 | 1 (LT+Down) | 1 (LT+Down) |

Bar 1 slots 9–12 map to cross 1 slots 5–8 (LT+X/Y/B/A) and are individually shared across jobs. The assignments come from the player's bars; no particular potion IDs are imposed on other users.

Utility bar 10 slots 1–12 map to cross 2 slots 1–12; bar 9 slots 1–4 map to cross 2 slots 13–16. The top-four regular-bar coordinates were verified in the original 2×6 HUD. Utility controller ordering is an editable starting default, not an inference from the unfinished original cross layout.

Profiles: Combat excludes healers and crafting/gathering; Healer includes Conjurer/WHM/SCH/AST/SGE; All applies to every class. Per-pair enable, position and shared-scope editing and clipboard mapping import/export are available. Arbitrary named per-job profiles are a possible later extension, not implemented here.

Initial sync requires preview/application. After activation, a new job initializes from its own regular assignments, with canonical shared values. Simultaneous conflicting changes wait for a choice. Backups precede all write batches, and verification checks the resulting native slots. Native API/game updates can invalidate behavior; build success alone is not runtime validation.

The original read-only capture uses schema 2 and includes 10 regular bars, eight cross sets, actual keybinding records and parent-chain geometry. Cached text may be stale on hidden bars. Raw key codes are preserved because some enum labels differ from the game's displayed minus/equal keys.

Version 0.2.3 makes live operation explicit: a persistent enable switch, separate live status, and resume without repeated alignment. First activation requires reviewing initial differences. Mapping edits require saving and reinitializing; ordinary hotbar edits never require opening the window or pressing a sync button. Both plugin windows open at 900×680 (clamped to the display), with resizable bounds and scrollable long content.
