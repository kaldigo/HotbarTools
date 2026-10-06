# Automation variant review

All three switches are independent on all 32 presets. Off preserves the curated baseline, which may already automate routine actions. Openers and hands-free rotation remain off. Manual and automatic access share cooldowns. Only functions covered on both relevant rotation paths are removed; retained buttons may still be needed for prepull, targeting, movement or manual timing.

All variants are now preset-only after preparation. BRD, DNC and BLM require one updated base setup to install their static options; subsequent switching never changes sliders, targeting or arrays and never unloads Wrath. Detection-based defense is conditional; keep the manual controls.

[Reviewed Wrath preset definitions](https://github.com/PunishXIV/WrathCombo/blob/55d55a3ebc48c1b6018b445b58a89ccfd84c3cbf/WrathCombo/Combos/CustomComboPreset.cs). The pinned metadata in `preset-rules.json` records parents and conflicts; regenerate its C# allowlist using `tools/generate_live_rules.py` after changing reviewed variant IDs.

## AST

**Auto burst:** No additional change: this healer already uses automatic offensive cooldowns; healing remains the priority. Manual slots remain available.

**Auto mitigation:** Enables Collective Unconscious and Neutral Sect for detected raidwide damage. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Draw/play, Lord and Earthly Star are already automated. Synastry and Macrocosmos timing stay manual. Manual slots remain available.

## BLM

**Auto burst:** Enables Ley Lines. Continue pressing ST/AoE; scripted openers stay off. Ley Lines stays because the same button also supplies manual Retrace. Manual slots remain available.

**Auto mitigation:** Enables Manaward through ST for detected group damage. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Fire/ice and resource flow are already handled. Transpose stays for downtime. Manual slots remain available.

## BRD

**Auto burst:** Enables Raging Strikes, Battle Voice, Barrage and Radiant Finale. Continue pressing ST/AoE; scripted openers stay off.  Cleared logical slots: 3.

**Auto mitigation:** Enables Nature’s Minne through ST, alongside the existing conditional Troubadour automation. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** All three songs cycle through ST/AoE; DoTs and procs already run automatically. Manual slots remain available.

## DNC

**Manual baseline:** Press Technical Step repeatedly for the four steps and finish, then resume ST/AoE.

**Auto burst:** Enables Technical Step initiation. Continue pressing ST/AoE; scripted openers stay off. Improvisation and Curing Waltz remain manual; dance partner selection is unchanged. Cleared logical slots: 3.

**Auto mitigation:** No additional change: the baseline already enables the reviewed conditional defensive automation. Keep manual defenses for AoE, planned timing and uncovered conditions. Manual slots remain available.

**Auto job mechanics:** Standard Step, Flourish and procs are already handled. Improvisation and Curing Waltz remain manual; dance partner selection is unchanged. Manual slots remain available.

## DRG

**Auto burst:** Enables Battle Litany and Lance Charge. Continue pressing ST/AoE; scripted openers stay off.  Cleared logical slots: 3.

**Auto mitigation:** Enables Feint through ST. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Enables Geirskogul/Life of the Dragon through ST/AoE.  Cleared logical slots: 6.

## DRK

**Auto burst:** Enables Delirium and Living Shadow. Continue pressing ST/AoE; scripted openers stay off. Carve and Spit stays available because its selector also provides manual interrupt access. Manual slots remain available.

**Auto mitigation:** Enables pack Rampart, Shadow Wall/Shadowed Vigil and Reprisal; detected boss tankbuster Rampart and Shadow Wall/Shadowed Vigil. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Blood/MP spending and damage follow-ups already run through ST/AoE. Targeted TBN stays manual. Manual slots remain available.

## GNB

**Auto burst:** No Mercy and Bloodfest are woven through ST/AoE. Burst slot 3 is empty. Scripted openers remain off. Cleared logical slots: 3.

**Auto mitigation:** Adds pack Rampart, Great Nebula and Reprisal, plus detected boss tankbuster Rampart/Great Nebula in casual content. Keeps current Aurora/Camouflage automation and all manual defense buttons. Corundum, Superbolide, boss party mitigation and Arms Length stay manual; automated actions share cooldowns with their manual buttons. Manual slots remain available.

**Auto job mechanics:** Cartridge spending, Gnashing Fang and damage follow-ups already run through ST/AoE in every Gunbreaker variant. Manual mobility, targeted support, invulnerability and tank utility remain deliberate. Manual slots remain available.

## MCH

**Auto burst:** Enables Barrel Stabilizer on ST/AoE and Wildfire on ST; Full Metal Field, Hypercharge and follow-up attacks are already automated. ST Barrel/Wildfire follow the saved boss-only settings; AoE Barrel follows its saved target-HP threshold. Keep pressing the damage button. Heat Blast remains for manual Wildfire during AoE and deliberate overrides; Flamethrower remains manual. Scripted opener stays off. Manual slots remain available.

**Auto mitigation:** No additional change: conditional Tactician is already enabled through ST. Dismantle stays manual on the shared defense button. In the pinned Wrath version its advanced ST automation requires an existing Dismantled duration above the configured threshold, so it cannot reliably initiate protection. Keep manual defense for planned timing and AoE. Manual slots remain available.

**Auto job mechanics:** Heat, battery and tools are already handled. Heat Blast stays as the manual AoE burst route; Wildfire automation is ST-only. Flamethrower remains manual. Manual slots remain available.

## MNK

**Auto burst:** Enables Brotherhood and Riddle of Fire. Continue pressing ST/AoE; scripted openers stay off.  Cleared logical slots: 3.

**Auto mitigation:** Enables Feint through ST. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Enables Perfect Balance through ST/AoE.  Cleared logical slots: 6.

## NIN

**Auto burst:** Enables Trick Attack and Mug upgrades. Continue pressing ST/AoE; scripted openers stay off. Hide stays for prepull mudra reset; Kassatsu stays for prepull use. Manual slots remain available.

**Auto mitigation:** Enables Feint through ST. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Enables Kassatsu, Ten Chi Jin and Meisui during combat through ST/AoE. Hide stays for prepull mudra reset; Kassatsu stays for prepull use. Cleared logical slots: 8.

## PCT

**Auto burst:** Enables Starry Muse. Continue pressing ST/AoE; scripted openers stay off. Hammer Stamp stays for deliberate spending and movement; Tempera stays for manual party shielding. Cleared logical slots: 3.

**Auto mitigation:** Enables conditional Tempera Coat through ST. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Motifs, palette and hammer follow-ups are already handled. Hammer Stamp stays for deliberate spending and movement; Tempera stays for manual party shielding. Manual slots remain available.

## PLD

**Auto burst:** Enables Fight or Flight. Continue pressing ST/AoE; scripted openers stay off.  Cleared logical slots: 3.

**Auto mitigation:** Enables pack Rampart, Sentinel/Guardian and Reprisal; detected boss tankbuster Rampart and Sentinel/Guardian. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Oath support stays targeted; damage and gauge follow-ups already run through ST/AoE. Manual slots remain available.

## RDM

**Auto burst:** Enables Embolden and Manafication. Continue pressing ST/AoE; scripted openers stay off.  Cleared logical slots: 3.

**Auto mitigation:** No additional change: the baseline already enables the reviewed conditional defensive automation. Keep manual defenses for AoE, planned timing and uncovered conditions. Manual slots remain available.

**Auto job mechanics:** Mana and melee finishers are already handled. Vercure and Raise remain available. Manual slots remain available.

## RPR

**Auto burst:** Enables Arcane Circle. Continue pressing ST/AoE; scripted openers stay off.  Cleared logical slots: 3.

**Auto mitigation:** Enables Feint and conditional Arcane Crest through ST. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Enables Enshroud through ST/AoE.  Cleared logical slots: 6.

## SAM

**Auto burst:** Enables Ikishoten. Continue pressing ST/AoE; scripted openers stay off.  Cleared logical slots: 3.

**Auto mitigation:** Enables Feint through ST. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Enables Meikyo Shisui through ST/AoE.  Cleared logical slots: 6.

## SCH

**Auto burst:** No additional change: this healer already uses automatic offensive cooldowns; healing remains the priority. Manual slots remain available.

**Auto mitigation:** Enables Sacred Soil for detected raidwide damage. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Enables conditional Dissipation through single-target and party healing. Conditional Dissipation may dismiss the fairy to replenish Aetherflow during healing. Keep the button for deliberate timing; normal fairy following remains the plan. Manual slots remain available.

## SGE

**Auto burst:** No additional change: this healer already uses automatic offensive cooldowns; healing remains the priority. Manual slots remain available.

**Auto mitigation:** Enables Kerachole and conditional Holos for detected raidwide damage. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Addersgall and Rhizomata are already handled. Kardia remains manual: its automatic resolver does not implement the agreed focus-target override. Pneuma remains deliberate. Manual slots remain available.

## SMN

**Auto burst:** Enables Searing Light. Continue pressing ST/AoE; scripted openers stay off.  Cleared logical slots: 3.

**Auto mitigation:** No additional change: the baseline already enables the reviewed conditional defensive automation. Keep manual defenses for AoE, planned timing and uncovered conditions. Manual slots remain available.

**Auto job mechanics:** Gem/demi sequencing already runs automatically. Astral Flow stays for movement and targeted Rekindle. Manual slots remain available.

## VPR

**Auto burst:** Enables Serpent’s Ire. Continue pressing ST/AoE; scripted openers stay off.  Cleared logical slots: 3.

**Auto mitigation:** Enables Feint through ST. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Enables Reawaken through ST/AoE.  Cleared logical slots: 6.

## WAR

**Auto burst:** Enables Berserk/Inner Release. Continue pressing ST/AoE; scripted openers stay off.  Cleared logical slots: 3.

**Auto mitigation:** Enables pack Rampart, Vengeance/Damnation and Reprisal; detected boss tankbuster Rampart and Vengeance/Damnation. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Infuriate, gauge spending and damage follow-ups already run through ST/AoE. Targeted support stays manual. Manual slots remain available.

## WHM

**Auto burst:** No additional change: this healer already uses automatic offensive cooldowns; healing remains the priority. Manual slots remain available.

**Auto mitigation:** Enables Asylum and Temperance for detected raidwide damage. Wrath applies its readiness, content, HP and damage-detection conditions. Keep manual defenses for deliberate timing and paths not covered by automation. Automated and manual uses share cooldowns. Manual slots remain available.

**Auto job mechanics:** Lily healing/damage follow-ups already run through the existing buttons. Bell placement and early release stay manual. Manual slots remain available.

## Crafting and gathering

All three switches are deliberate no-ops. See [twelve-slot layouts](noncombat-hotbars.md) and [omitted actions](noncombat-omissions.md).

## Retained controls audit

Of the 17 non-healer combat jobs, automatic burst clears the dedicated burst button on 13. The four retained controls have other duties: DRK retains manual interrupt access on Carve and Spit, NIN retains Hide for prepull mudra reset, BLM retains Retrace on Ley Lines, and MCH retains the manual AoE burst route because Wildfire automation is ST-only. Healers already have automatic offense; their healing buttons are not burst buttons.

Every mitigation control remains intentionally available. Tank selectors still cover party/knockback tools and deliberate timing outside Wrath's content/target gates; invulnerabilities and targeted support remain manual. Healer raidwide automation depends on detection and healing/resource conditions. Melee Feint, physical-ranged support and caster defenses have ST-only or other conditional coverage; they do not replace manual AoE, party shielding or encounter timing. MCH, DNC, RDM and SMN mitigation switches add no new feature because their reviewed baseline automation is already enabled. MCH Dismantle specifically remains manual due to the pinned implementation's duration check. These choices preserve uncovered functions, rather than promising complete automatic defense.
