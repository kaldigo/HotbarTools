"""Generate portable reviewed presets, never ship the personal original configuration."""
import json,re
from pathlib import Path
root=Path(__file__).resolve().parents[1]
wrath=root/'wrath'
ids=dict(PLD=19,WAR=21,DRK=32,GNB=37,WHM=24,SCH=28,AST=33,SGE=40,MNK=20,DRG=22,NIN=30,SAM=34,RPR=39,VPR=41,BRD=23,MCH=31,DNC=38,BLM=25,SMN=27,RDM=35,PCT=42)
base={'PLD':[1],'WAR':[3],'WHM':[6],'MNK':[2],'DRG':[4],'NIN':[29],'BRD':[5],'BLM':[7],'SMN':[26]}
# Native upgrade roots keep assignments usable when level-synced / on base classes.
roots={'Heart of Corundum':'Heart of Stone','Holy Sheltron':'Sheltron','Bloodwhetting':'Raw Intuition','Inner Release':'Berserk','Glare III':'Stone','Holy III':'Holy','Broil IV':'Ruin','Concitation':'Succor','Art of War II':'Art of War','Fall Malefic':'Malefic','Gravity II':'Gravity','Dosis III':'Dosis','Dyskrasia II':'Dyskrasia'}
keyboard={'1':(1,1),'2':(1,2),'3':(1,3),'4':(1,4),'Shift+1':(2,1),'Shift+2':(2,2),'Shift+3':(2,3),'Shift+4':(2,4),'Shift+5':(2,5),'Shift+6':(2,6),'Shift+=':(2,12),'Shift+-':(2,11)}
cross=dict(zip(['LT+D-Down','LT+D-Left','LT+D-Right','LT+D-Up','LT+X','LT+Y','LT+B','LT+A','RT+D-Down','RT+D-Left','RT+D-Right','RT+D-Up','RT+X','RT+Y','RT+B','RT+A'],range(1,17)))
cat=json.loads((wrath/'reference/presets.json').read_text());jobs=[]
for job,jid in ids.items():
 slots=[]
 for line in (wrath/f'jobs/{job}/hotbar.md').read_text(encoding='utf-8-sig').splitlines():
  if not re.match(r'^\| \d+ \|',line):continue
  row=[x.strip() for x in line.split('|')[1:-1]];slot,controller,key,function,action=row[:5]
  action=action.split(' / ')[0].replace('\u2019',"'");action=roots.get(action,action)
  bar,pos=keyboard[key]
  slots.append(dict(LogicalSlot=int(slot),RegularBar=bar,RegularSlot=pos,CrossSlot=cross[controller],Action=action,Function=function))
 settings=json.loads((wrath/f'jobs/{job}/settings.json').read_text())
 jobs.append(dict(JobId=jid,Job=job,BaseClasses=base.get(job,[]),Slots=slots,OwnedPresetIds=[int(n) for n,v in cat.items() if v['job']==job and 'pvp' not in v['name'].lower()],Settings=settings))
for job in jobs:
 variant=root/'job-setup/variants'/f"{job['Job']}.json"
 if variant.exists():job.update(json.loads(variant.read_text(encoding='utf-8-sig')))
s=json.loads((wrath/'shared.json').read_text())
shared={k:s[k] for k in ('EnabledActionsV6','CustomHealStack','RaiseStack')}
# Explicit shared behavior keys only, not every original UI preference/metadata field.
for section in ('CustomBoolValuesV6','CustomBoolArrayValuesV6','CustomIntValuesV6'):
 values={k:v for k,v in s[section].items() if k.startswith(('ALL_Healer_Rescue','AllTank','AllCaster','AllMelee','AllRanged'))}
 if values:shared[section]=values
# Carry the targeting stack mode fields needed by the actual shared resolver.
for k,v in s.items():
 if any(x in k for x in ('HealStack','RaiseStack')) and k not in shared:shared[k]=v
pack=dict(SchemaVersion=1,WrathVersion='1.0.4.26',Jobs=jobs,SharedSettings=shared)
out=root/'job-setup/JobSetup/presets.json';out.write_text(json.dumps(pack,indent=2)+'\n',encoding='utf-8')
print('Generated',len(jobs),'job presets and',sum(len(j['BaseClasses']) for j in jobs),'base-class aliases. Shared keys:',list(shared))
