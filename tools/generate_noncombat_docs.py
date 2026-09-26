"""Compile noncombat tables/usage and separate omission notes from the portable sources."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
controls=['LT+D-Down','LT+D-Left','LT+D-Right','LT+D-Up','LT+X','LT+Y','LT+B','LT+A','RT+D-Down','RT+D-Left','RT+D-Right','RT+D-Up','RT+X','RT+Y','RT+B','RT+A']
plans=[];review=['# Noncombat scope and omissions','', 'Each class uses one crossbar page: 12 job actions plus four preserved shared utility positions. The side utility bar is unchanged. All three automation switches intentionally make no additional changes on these classes. Crafting has no Wrath rotation; gathering/fishing replacements are included in the baseline. These compact layouts deliberately omit useful actions and are not complete specialist toolkits.','', 'Actions require their normal levels and quest unlocks. The gathering pair shares Wrath options, so applying either Miner or Botanist also sets those same two shared gathering features. Their layouts remain separate.','', 'Crafting prioritizes basic progress, the two-touch combo, durability tools, buffs, a quality finisher and a choice of opener. It sacrifices faster high-level synthesis/touch alternatives and specialist tools. Fishing balances rod and spear actions using eight context replacements; it is not an ocean-fishing score or all-big-fish preset. Adjust assignments for those activities.','', 'Reviewed against [Wrath source](https://github.com/PunishXIV/WrathCombo/blob/55d55a3ebc48c1b6018b445b58a89ccfd84c3cbf/WrathCombo/Combos/PvE/DOL/DOL.cs) and the locally installed game sheets. Build/data validation does not establish in-game usability.','']
for p in sorted((root/'job-setup/noncombat').glob('*.json')):
 j=json.loads(p.read_text(encoding='utf-8-sig'))
 plans += [f'# {j["Job"]}','','| Slot | Keyboard | Controller | Action | Function |','| --- | --- | --- | --- | --- |']
 for s in j['Slots']:
  key=str(s['RegularSlot']) if s['RegularBar']==1 else 'Shift+'+{12:'=',11:'-'}.get(s['RegularSlot'],str(s['RegularSlot']))
  plans.append(f'| {s["LogicalSlot"]} | {key} | {controls[s["CrossSlot"]-1]} | {s["Action"]} | {s["Function"]} |')
 plans += ['','## Usage','',j['Usage'],'']
 review += [f'## {j["Job"]}','','Omitted from the page: '+', '.join(j['OmittedActions'])+'.','']
(root/'job-setup/noncombat-hotbars.md').write_text('\n'.join(plans),encoding='utf-8')
(root/'job-setup/noncombat-omissions.md').write_text('\n'.join(review),encoding='utf-8')
