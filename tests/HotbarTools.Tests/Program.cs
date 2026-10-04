using HotbarTools;
using System.Text.Json;
using System.Text.Json.Nodes;
int count=0;
void Test(string name,Action action){action();count++;Console.WriteLine("PASS "+name);}
void Equal<T>(T a,T b){if(!EqualityComparer<T>.Default.Equals(a,b))throw new Exception($"Expected {b}, got {a}");}
void Throws(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("Expected rejection");}
var old=new SlotValue(1,10);var a=new SlotValue(1,11);var b=new SlotValue(1,12);
Test("regular edit propagates",()=>Equal(SyncRules.Decide(a,old,old),SyncChoice.ToCross));
Test("cross edit propagates",()=>Equal(SyncRules.Decide(old,a,old),SyncChoice.ToRegular));
Test("simultaneous edits conflict",()=>Equal(SyncRules.Decide(a,b,old),SyncChoice.Conflict));
Test("equal edits converge",()=>Equal(SyncRules.Decide(a,a,old),SyncChoice.None));
Test("deletion propagates",()=>Equal(SyncRules.Decide(default,old,old),SyncChoice.ToCross));
Test("empty slots ignore stale IDs",()=>Equal(SyncRules.Decide(new(0,10),default,old),SyncChoice.None));
Test("default mappings valid",()=>Defaults.Validate(Defaults.Maps()));
Test("overlapping pairs rejected",()=>{var m=Defaults.Maps();m.Add(new SlotMap{Profile="All"});Throws(()=>Defaults.Validate(m));});
Test("out-of-range mapping rejected",()=>{var m=Defaults.Maps();m[0].CrossSet=9;Throws(()=>Defaults.Validate(m));});
Test("regular/cross position bounds",()=>{Equal(new Position(9,12).Valid,false);Equal(new Position(10,15).Valid,true);Equal(new Position(18,0).Valid,false);});
Test("healer physical buttons",()=>{var m=Defaults.Maps().Single(x=>x.Applies(24)&&x.Regular==new Position(0,2));Equal(m.Cross,new Position(10,12));});
Test("gunbreaker physical buttons",()=>{var m=Defaults.Maps().Single(x=>x.Applies(37)&&x.Regular==new Position(0,2));Equal(m.Cross,new Position(10,13));});
Test("utility bar exact verified positions",()=>{var m=Defaults.Maps().Where(x=>x.Id.StartsWith("utility-")).ToList();Equal(m.Count,16);Equal(m.All(x=>x.CrossSet==8),true);Equal(string.Join(",",m.Select(x=>x.CrossSlot)),string.Join(",",Enumerable.Range(1,16)));Equal(m.Count(x=>x.RegularBar==10),12);Equal(string.Join(",",m.Where(x=>x.RegularBar==9).Select(x=>x.RegularSlot)),"1,2,3,4");});
Test("utility default migration pauses sync and preserves routes and labels",()=>{
    var c=new HotbarBridge.BridgeConfig{Revision=9,Enabled=true};
    foreach(var m in c.Maps.Where(m=>m.Id.StartsWith("utility-")))m.CrossSet=2;
    c.Maps.Single(m=>m.Id=="utility-0").Label="My utility";
    c.JobRoutes[37]=new(){["Combat-0"]=new(0,0)};
    Equal(c.MigrateUtilityCrossbar(out var notice),true);Equal(notice!=null,true);
    Equal(c.Maps.Where(m=>m.Id.StartsWith("utility-")).All(m=>m.CrossSet==8),true);
    Equal(c.Revision,10);Equal(c.Enabled,false);Equal(c.JobRoutes[37]["Combat-0"],new Position(0,0));
    Equal(c.Maps.Single(m=>m.Id=="utility-0").Label,"My utility");
    Equal(c.MigrateUtilityCrossbar(out _),false);Equal(c.Revision,10);
    Defaults.Validate(c.Maps);
});
Test("utility migration preserves customized empty and new maps",()=>{
    var c=new HotbarBridge.BridgeConfig{Enabled=true};
    foreach(var m in c.Maps.Where(m=>m.Id.StartsWith("utility-")))m.CrossSet=2;
    c.Maps.Single(m=>m.Id=="utility-0").CrossSet=7;
    var before=JsonSerializer.Serialize(c.Maps);c.MigrateUtilityCrossbar(out _);
    Equal(JsonSerializer.Serialize(c.Maps),before);Equal(c.Enabled,true);Equal(c.Revision,0);
    var empty=new HotbarBridge.BridgeConfig{Maps=[]};empty.MigrateUtilityCrossbar(out _);Equal(empty.Maps.Count,0);
    var fresh=new HotbarBridge.BridgeConfig{Enabled=true};fresh.MigrateUtilityCrossbar(out _);Equal(fresh.Enabled,true);Equal(fresh.Revision,0);
});
Test("utility migration is atomic when last crossbar is already mapped",()=>{
    var c=new HotbarBridge.BridgeConfig{Enabled=true,Revision=4};
    foreach(var m in c.Maps.Where(m=>m.Id.StartsWith("utility-")))m.CrossSet=2;
    c.Maps.Add(new(){Id="custom",Profile="All",RegularBar=3,RegularSlot=1,CrossSet=8,CrossSlot=1});
    var before=JsonSerializer.Serialize(c.Maps);c.MigrateUtilityCrossbar(out var notice);
    Equal(notice!=null,true);Equal(JsonSerializer.Serialize(c.Maps),before);Equal(c.Enabled,true);Equal(c.Revision,4);
    Defaults.Validate(c.Maps);
});
Test("old utility cleanup requires both source and destination confirmation",()=>{
    var maps=Defaults.Maps();
    var values=new Dictionary<(uint,Position),SlotValue>();
    foreach(uint job in new uint[]{24,37})foreach(var map in maps.Where(m=>m.Id.StartsWith("utility-")))
    {
        values[(job,map.Regular)]=new(1,(uint)map.CrossSlot);
        values[(job,map.Cross)]=new(1,(uint)map.CrossSlot);
        values[(job,new(11,map.Cross.Slot))]=new(1,(uint)map.CrossSlot);
    }
    values[(24,new(11,0))]=new(1,999); // User changed an old slot.
    values[(24,new(17,1))]=default; // Destination has not been aligned.
    maps.Add(new(){Id="custom",RegularBar=3,RegularSlot=1,CrossSet=2,CrossSlot=3});
    var edits=UtilityCleanup.Plan(24,new uint[]{24,37},maps,false,(job,p)=>values.GetValueOrDefault((job,p)));
    Equal(edits.Count,28);Equal(edits.All(e=>e.Position.Bar==11 && e.After==default),true);
    Equal(edits.Any(e=>e.Position.Slot==2),false);
    Equal(edits.Count(e=>e.Job==24),13);Equal(edits.Count(e=>e.Job==37),15);
    var shared=UtilityCleanup.Plan(24,new uint[]{24,37},maps,true,(job,p)=>values.GetValueOrDefault((job,p)));
    Equal(shared.Count,13);Equal(shared.All(e=>e.Job==24),true);
    var current=UtilityCleanup.Plan(24,new uint[]{24},maps,false,(job,p)=>values.GetValueOrDefault((job,p)));
    Equal(current.Count,13);
});
Test("old utility cleanup ignores empty and customized utility mappings",()=>{
    var maps=Defaults.Maps();maps.Single(m=>m.Id=="utility-0").CrossSet=7;
    var edits=UtilityCleanup.Plan(37,new uint[]{37},maps,false,(_,_)=>new SlotValue(1,100));
    Equal(edits.Count,15);Equal(edits.Any(e=>e.Position.Slot==0),false);
    Equal(UtilityCleanup.Plan(37,new uint[]{37},maps,false,(_,_)=>new SlotValue(0,100)).Count,0);
});
Test("shared keys exact destinations",()=>{var m=Defaults.Maps().Where(x=>x.Id.StartsWith("shared-")).ToList();Equal(string.Join(",",m.Select(x=>x.RegularSlot)),"9,10,11,12");Equal(string.Join(",",m.Select(x=>x.CrossSlot)),"5,6,7,8");});
var original=JsonNode.Parse("""{"Version":6,"EnabledActionsV6":[10,20,999],"CustomIntValuesV6":{"A":1,"B":2},"Other":"keep"}""")!.AsObject();
var job=new JobPreset{OwnedPresetIds=[10,11],Settings=JsonNode.Parse("""{"EnabledActionsV6":[11],"CustomIntValuesV6":{"A":3}}""")!.AsObject()};
Test("current-job merge preserves other jobs",()=>{var result=WrathMerge.Merge(original,[job],null);Equal(result["EnabledActionsV6"]!.ToJsonString(),"[11,20,999]");Equal(result["CustomIntValuesV6"]!["B"]!.GetValue<int>(),2);Equal(result["Other"]!.GetValue<string>(),"keep");});
Test("merge leaves original untouched",()=>{WrathMerge.Merge(original,[job],null);Equal(original["CustomIntValuesV6"]!["A"]!.GetValue<int>(),1);});
Test("shared options require explicit argument",()=>{var shared=JsonNode.Parse("""{"UseCustomHealStack":true}""")!.AsObject();Equal(WrathMerge.Merge(original,[job],null).ContainsKey("UseCustomHealStack"),false);Equal(WrathMerge.Merge(original,[job],shared)["UseCustomHealStack"]!.GetValue<bool>(),true);});
Test("wrong Wrath schema rejected",()=>Throws(()=>WrathMerge.Merge(new JsonObject{["Version"]=7},[job],null)));
var pack=JsonSerializer.Deserialize<PresetPack>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"presets.json")))!;
Test("32 presets and 9 base classes",()=>{Equal(pack.Jobs.Count,32);Equal(pack.Jobs.Sum(j=>j.BaseClasses.Length),9);Equal(pack.Jobs.SelectMany(j=>j.BaseClasses).Distinct().Count(),9);});
Test("preset mappings match Bridge defaults",()=>{foreach(var j in pack.Jobs)foreach(var s in j.Slots){var map=Defaults.Maps().Single(m=>m.Applies(j.JobId)&&m.Regular==new Position(s.RegularBar-1,s.RegularSlot-1));Equal(map.Cross,new Position(10,s.CrossSlot-1));Equal(map.SharedAcrossJobs,false);}});
Test("shared targeting apply enables retargeting and preserves unrelated preferences",()=>{
    var existing=JsonNode.Parse("""{"Version":6,"EnabledActionsV6":[],"RetargetHealingActionsToStack":false,"CustomHealStack":["ModelMouseOverTarget","Self"],"UseFieldMouseoverOverridesInDefaultHealStack":true,"Unrelated":"keep"}""")!.AsObject();
    var result=WrathMerge.Merge(existing,[],pack.SharedSettings);
    Equal(result["RetargetHealingActionsToStack"]!.GetValue<bool>(),true);
    Equal(result["UseFieldMouseoverOverridesInDefaultHealStack"]!.GetValue<bool>(),false);
    Equal(result["UseCustomHealStack"]!.GetValue<bool>(),true);
    Equal(string.Join(",",result["CustomHealStack"]!.AsArray().Select(n=>n!.GetValue<string>())),"FocusTarget,UIMouseOverTarget,SoftTarget,HardTarget,TargetsTarget,AnyLivingTank,Self");
    Equal(result["Unrelated"]!.GetValue<string>(),"keep");
    Equal(WrathMerge.Merge(existing,[],null)["RetargetHealingActionsToStack"]!.GetValue<bool>(),false);
});
Test("cleanup removes Shift8 and old pages but preserves layouts and shared assignments",()=>{
    var layout=new[]{new Position(0,0),new Position(1,0),new Position(10,15)};
    var maps=Defaults.Maps();
    maps.Add(new SlotMap{RegularBar=3,RegularSlot=7,CrossSet=4,CrossSlot=8,SharedAcrossJobs=true,Enabled=false});
    var filled=new Dictionary<Position,SlotValue>{
        [new(0,0)]=new(1,100),[new(1,0)]=new(1,101),[new(10,15)]=new(1,102),
        [new(1,7)]=new(1,200),[new(3,2)]=new(7,12),[new(12,0)]=new(1,201),
        [new(0,8)]=new(1,300),[new(10,4)]=new(1,301),[new(9,0)]=new(1,302),
        [new(2,6)]=new(1,303),[new(13,7)]=new(1,304),[new(8,10)]=new(1,305)};
    var edits=HotbarCleanup.Plan(37,layout,maps,bar=>bar==8,p=>filled.GetValueOrDefault(p));
    Equal(edits.Count,3);
    Equal(edits.All(e=>e.Job==37 && e.After==default),true);
    Equal(edits.Any(e=>e.Position==new Position(1,7)),true);
    Equal(edits.Any(e=>e.Position==new Position(3,2) && e.Before==new SlotValue(7,12)),true);
});
Test("cleanup preserves final compacted destinations for every variant",()=>{
    foreach(var baseline in pack.Jobs)foreach(var variant in Enumerable.Range(0,8))
    {
        var preset=Variants.Resolve(baseline,new(){AutoBurst=(variant&1)!=0,AutoMitigation=(variant&2)!=0,AutoMechanics=(variant&4)!=0});
        var final=KeyboardLayout.Compile(preset,true).Values.ToHashSet();
        var edits=HotbarCleanup.Plan(preset.JobId,final,Defaults.Maps(),_=>false,_=>new SlotValue(1,100));
        Equal(edits.Any(e=>final.Contains(e.Position)),false);
        Equal(edits.Any(e=>e.Position==new Position(1,7)),true);
    }
});
Test("cleanup ignores empty slots and rejects invalid destinations",()=>{
    Equal(HotbarCleanup.Plan(24,[],[],_=>false,_=>new SlotValue(0,99)).Count,0);
    Throws(()=>HotbarCleanup.Plan(0,[],[],_=>false,_=>default));
    Throws(()=>HotbarCleanup.Plan(24,[new Position(20,0)],[],_=>false,_=>default));
});
Test("all preset targets are unique",()=>{foreach(var j in pack.Jobs){Equal(j.Slots.Select(s=>(s.RegularBar,s.RegularSlot)).Distinct().Count(),j.Slots.Length);Equal(j.Slots.Select(s=>s.CrossSlot).Distinct().Count(),j.Slots.Length);}});
Test("no shared utility is overwritten by job presets",()=>{foreach(var j in pack.Jobs)foreach(var s in j.Slots){Equal(s.RegularBar==1&&s.RegularSlot>=9,false);Equal(s.CrossSlot is >=5 and <=8,false);}});
Test("base action roots preserve level-sync use",()=>{Equal(pack.Jobs.Single(j=>j.Job=="GNB").Slots.Single(s=>s.LogicalSlot==6).Action,"Heart of Stone");Equal(pack.Jobs.Single(j=>j.Job=="WHM").Slots.Single(s=>s.LogicalSlot==1).Action,"Stone");});
Test("whole pack merges without duplicate presets",()=>{var r=WrathMerge.Merge(original,pack.Jobs,pack.SharedSettings);var ids=r["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToList();Equal(ids.Count,ids.Distinct().Count());});
Test("Wrath writes blocked while loaded",()=>{try{WrathWritePolicy.Validate(true,true);}catch(InvalidOperationException){return;}throw new Exception("Loaded Wrath permitted");});
Test("Wrath writes blocked when absent",()=>{try{WrathWritePolicy.Validate(false,false);}catch(InvalidOperationException){return;}throw new Exception("Absent Wrath permitted");});
Test("Wrath writes permitted only installed and disabled",()=>WrathWritePolicy.Validate(true,false));
Test("slot value backup serialization roundtrip",()=>{var json=JsonSerializer.Serialize(a);Equal(JsonSerializer.Deserialize<SlotValue>(json),a);Equal(json.Contains("Normalized"),false);});
Test("empty slot state serialization roundtrip",()=>{var json=JsonSerializer.Serialize(new Dictionary<string,SlotValue>{{"shared",default}});Equal(JsonSerializer.Deserialize<Dictionary<string,SlotValue>>(json)!["shared"],default);});
Test("zero-based macro/gearset IDs are preserved",()=>Equal(new SlotValue(7,0).Normalized,new SlotValue(7,0)));
Test("hotbar backup roundtrip",()=>{var edits=new[]{new SlotEdit(37,new Position(0,0),old,a)};var restored=JsonSerializer.Deserialize<SlotEdit[]>(JsonSerializer.Serialize(edits))!;Equal(restored[0],edits[0]);});
Test("first live activation requires alignment",()=>Equal(LiveSyncStart.Decide(false,[],0),LiveStartChoice.Initialize));
Test("live resume preserves existing baselines",()=>Equal(LiveSyncStart.Decide(false,["0:37:Combat-0"],0),LiveStartChoice.Resume));
Test("new mapping revision requires new alignment",()=>Equal(LiveSyncStart.Decide(false,["0:37:Combat-0"],1),LiveStartChoice.Initialize));
Test("unsaved mapping cannot resume",()=>Equal(LiveSyncStart.Decide(true,["0:37:Combat-0"],0),LiveStartChoice.SaveMapping));
Test("saved mappings replace defaults on every reload",()=>{
    var config=new HotbarBridge.BridgeConfig { Revision=7,Enabled=true };
    config.Maps[0].Label="Custom label";
    config.Maps[0].Enabled=false;
    config.Maps.RemoveAt(1);
    for(var i=0;i<3;i++) {
        config=Newtonsoft.Json.JsonConvert.DeserializeObject<HotbarBridge.BridgeConfig>(Newtonsoft.Json.JsonConvert.SerializeObject(config))!;
        Equal(config.Maps.Count,55);Equal(config.Maps[0].Label,"Custom label");Equal(config.Maps[0].Enabled,false);
        Equal(config.Revision,7);Equal(config.Enabled,true);Defaults.Validate(config.Maps);
    }
});
Test("explicit empty mapping stays empty",()=>Equal(Newtonsoft.Json.JsonConvert.DeserializeObject<HotbarBridge.BridgeConfig>("{\"Maps\":[]}")!.Maps.Count,0));
Test("legacy config without mappings receives defaults",()=>Defaults.Validate(Newtonsoft.Json.JsonConvert.DeserializeObject<HotbarBridge.BridgeConfig>("{}")!.Maps));
if(args.Length>0) {
    var saved=Newtonsoft.Json.JsonConvert.DeserializeObject<HotbarBridge.BridgeConfig>(File.ReadAllText(args[0]))!;
    Defaults.Validate(saved.Maps);Console.WriteLine($"Saved configuration verified: {saved.Maps.Count} pairs.");
}
var gnb=pack.Jobs.Single(j=>j.Job=="GNB");
var baselineJson=JsonSerializer.Serialize(gnb);
foreach(var burst in new[]{false,true})foreach(var mit in new[]{false,true})foreach(var mechanics in new[]{false,true})
Test($"GNB independent variants burst={burst} mitigation={mit} mechanics={mechanics}",()=>{
    var resolved=Variants.Resolve(gnb,new(){AutoBurst=burst,AutoMitigation=mit,AutoMechanics=mechanics});
    var enabled=resolved.Settings["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
    foreach(var id in new[]{7008,7011,7201,7204})Equal(enabled.Contains(id),burst);
    foreach(var id in new[]{7500,7501})Equal(enabled.Contains(id),!burst);
    foreach(var id in new[]{7702,7703,7708,7718,7719})Equal(enabled.Contains(id),mit);
    foreach(var id in new[]{7006,7706,7721,7710,7716,7717,7713,7714,7707})Equal(enabled.Contains(id),false);
    Equal(resolved.Slots.Single(s=>s.LogicalSlot==3).Action,burst?"Empty":"No Mercy");
    foreach(var slot in gnb.Slots.Where(s=>s.LogicalSlot!=3))
        Equal(JsonSerializer.Serialize(resolved.Slots.Single(s=>s.LogicalSlot==slot.LogicalSlot)),JsonSerializer.Serialize(slot));
    Equal(JsonSerializer.Serialize(gnb),baselineJson);
    var merged=WrathMerge.Merge(new JsonObject{["Version"]=6,["EnabledActionsV6"]=new JsonArray(999999)},[resolved],null);
    Equal(merged["EnabledActionsV6"]!.AsArray().Any(n=>n!.GetValue<int>()==999999),true);
});
Test("switches off restore exact baseline",()=>Equal(JsonSerializer.Serialize(Variants.Resolve(gnb,new())),baselineJson));
Test("unreviewed variants rejected",()=>Throws(()=>Variants.Resolve(new JobPreset{Job="unreviewed"},new(){AutoBurst=true})));
Test("paired apply writes hotbars then settings",()=>{
    var calls="";PairedApply.Run(()=>calls+="H",()=>calls+="S",()=>calls+="R");Equal(calls,"HS");
});
Test("settings failure rolls back hotbars",()=>{
    var calls="";try { PairedApply.Run(()=>calls+="H",()=>{calls+="S";throw new InvalidDataException();},()=>calls+="R"); }
    catch(InvalidDataException) { Equal(calls,"HSR");return; }throw new Exception("Failure swallowed");
});
Test("hotbar failure never writes settings",()=>{
    var calls="";try { PairedApply.Run(()=>throw new InvalidDataException(),()=>calls+="S",()=>calls+="R"); }
    catch(InvalidDataException) { Equal(calls,"");return; }throw new Exception("Failure swallowed");
});
Test("rollback failure reports both errors",()=>{
    try { PairedApply.Run(()=>{},()=>throw new InvalidDataException("settings"),()=>throw new InvalidOperationException("rollback")); }
    catch(AggregateException e) { Equal(e.InnerExceptions.Count,2);return; }throw new Exception("Failures swallowed");
});
Test("compaction matrix preserves every remaining function for all jobs",()=>{
    foreach(var baseline in pack.Jobs)
    for(var mask=0;mask<64;mask++)
    {
        var p=JsonSerializer.Deserialize<JobPreset>(JsonSerializer.Serialize(baseline))!;
        // Exercise arbitrary automation removals without inventing actual job settings.
        var optional=p.Slots.Where(x=>x.LogicalSlot is >=3 and <=8).ToArray();
        for(var i=0;i<optional.Length;i++)if((mask&(1<<i))!=0)optional[i].Action="Empty";
        var layout=KeyboardLayout.Compile(p,true);
        Equal(layout.Count,12);Equal(layout.Values.Distinct().Count(),12);
        Equal(layout[new(0,0)],new Position(0,0));Equal(layout[new(0,1)],new Position(0,1));
        var healer=Defaults.IsHealer(p.JobId);var tank=p.JobId is 19 or 21 or 32 or 37;
        if(tank||healer){Equal(layout[new(1,11)],new Position(1,11));Equal(layout[new(1,10)],new Position(1,10));}
        if(Defaults.IsCombat(p.JobId) && !healer && p.Slots.Single(x=>x.LogicalSlot==4).Action!="Empty")Equal(layout[new(1,0)],new Position(1,0));
        var active=p.Slots.Where(x=>x.Action!="Empty").ToArray();
        Equal(active.Select(x=>layout[new(x.RegularBar-1,x.RegularSlot-1)]).Distinct().Count(),active.Length);
        foreach(var job in new[]{p.JobId}.Concat(p.BaseClasses))
        {
            var maps=Defaults.Maps().Where(m=>m.Applies(job)).ToList();
            var routes=maps.Where(m=>!m.SharedAcrossJobs).ToDictionary(m=>m.Id,m=>layout[m.Regular]);
            var effective=LayoutRoutes.Effective(maps,routes);
            foreach(var m in maps){var actual=effective.Single(x=>x.Id==m.Id);Equal(actual.Cross,m.Cross);if(m.SharedAcrossJobs)Equal(actual.Regular,m.Regular);}
        }
    }
});
Test("GNB burst compaction keeps mobility and reserves fixed",()=>{
    var p=Variants.Resolve(gnb,new(){AutoBurst=true});var layout=KeyboardLayout.Compile(p,true);
    Equal(layout[new(1,1)],new Position(0,3));Equal(layout[new(1,2)],new Position(1,1));Equal(layout[new(1,3)],new Position(1,2));
    Equal(layout[new(1,0)],new Position(1,0));
});
Test("compaction off preserves all original positions",()=>{
    foreach(var p in pack.Jobs)foreach(var pair in KeyboardLayout.Compile(p,false))Equal(pair.Key,pair.Value);
});
Test("empty mobility slot is available when job has no gap closer",()=>{
    var p=pack.Jobs.Single(j=>j.Job=="MCH");Equal(KeyboardLayout.Compile(p,true)[new(1,1)],new Position(1,0));
});
Test("only general tank healer reserves are pinned",()=>{
    var p=pack.Jobs.Single(j=>j.Job=="SAM");Equal(KeyboardLayout.Compile(p,true)[new(1,11)],new Position(1,4));
});
Test("shared routes and colliding routes are rejected",()=>{
    Throws(()=>LayoutRoutes.Effective(Defaults.Maps(),new(){{"shared-0",new(0,2)}}));
    Throws(()=>LayoutRoutes.Effective(Defaults.Maps(),new(){{"Combat-0",new(0,1)}}));
});
Test("saved compaction routes reload without changing positions",()=>{
    var c=new HotbarBridge.BridgeConfig();c.JobRoutes[37]=new(){{"Combat-0",new(0,0)}};
    var result=Newtonsoft.Json.JsonConvert.DeserializeObject<HotbarBridge.BridgeConfig>(Newtonsoft.Json.JsonConvert.SerializeObject(c))!;
    Equal(result.JobRoutes[37]["Combat-0"],new Position(0,0));Equal(result.Maps.Count,56);
});
Test("complete layout backup retains routing and assignments",()=>{
    var backup=new LayoutBackup {Edits=[new(37,new(0,3),a,b)],Routes=new(){{37,new(){{"Combat-3",new(1,0)}}}}};
    var copy=JsonSerializer.Deserialize<LayoutBackup>(JsonSerializer.Serialize(backup))!;
    Equal(copy.Edits[0],backup.Edits[0]);Equal(copy.Routes![37]["Combat-3"],new Position(1,0));
});
Test("live plans handle all GNB variant transitions",()=>{
    JsonObject Config(bool burst,bool mit)=>WrathMerge.Merge(new JsonObject{["Version"]=6,["EnabledActionsV6"]=new JsonArray()},[Variants.Resolve(gnb,new(){AutoBurst=burst,AutoMitigation=mit})],null);
    foreach(var b1 in new[]{false,true})foreach(var m1 in new[]{false,true})foreach(var b2 in new[]{false,true})foreach(var m2 in new[]{false,true})
    {
        var before=Config(b1,m1);var after=Config(b2,m2);var plan=LivePresetPlan.Create(before,after)??throw new Exception("Expected live plan");
        var ids=before["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
        foreach(var (id,on) in plan){if(on)ids.Add(id);else ids.Remove(id);if(ids.Contains(7500)&&(ids.Contains(7008)||ids.Contains(7201)))throw new Exception("Conflicting manual burst enabled");}
        Equal(ids.SetEquals(after["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>())),true);
    }
});
Test("live plan refuses custom values and unknown preset changes",()=>{
    var before=WrathMerge.Merge(new JsonObject{["Version"]=6,["EnabledActionsV6"]=new JsonArray()},[gnb],null);
    var after=(JsonObject)before.DeepClone();after["CustomIntValuesV6"]!["GNB_ST_NoMercyStop"]=123;
    Equal(LivePresetPlan.Create(before,after)==null,true);
    after=(JsonObject)before.DeepClone();after["EnabledActionsV6"]!.AsArray().Add(999999);Equal(LivePresetPlan.Create(before,after)==null,true);
});
Test("noncombat migration preserves custom maps and is idempotent",()=>{
    var c=new HotbarBridge.BridgeConfig{Maps=Defaults.Maps().Where(m=>m.Profile!="Noncombat").ToList()};
    c.Maps[0].Label="User label";c.Revision=8;
    Equal(c.AddNoncombatDefaults(),true);Equal(c.Maps.Count,56);Equal(c.Maps[0].Label,"User label");Equal(c.Revision,8);
    Equal(c.AddNoncombatDefaults(),false);Defaults.Validate(c.Maps);
    var empty=new HotbarBridge.BridgeConfig{Maps=[]};empty.AddNoncombatDefaults();Equal(empty.Maps.Count,0);
    var custom=new HotbarBridge.BridgeConfig{Maps=[new(){Id="custom",Profile="All",RegularBar=1,RegularSlot=1,CrossSlot=16}]};
    custom.AddNoncombatDefaults();Equal(custom.Maps[0].Id,"custom");Defaults.Validate(custom.Maps);
});
Test("all noncombat layouts use twelve slots and document omissions",()=>{
    foreach(var p in pack.Jobs.Where(p=>p.JobId is >=8 and <=18)){
        Equal(p.Slots.Length,12);Equal(p.OmittedActions.Length>0,true);Equal(p.Usage.Length>50,true);
    }
});
Test("all variants preserve baseline and generate valid routes",()=>{
    foreach(var p in pack.Jobs)for(var bits=0;bits<8;bits++){
        var original=JsonSerializer.Serialize(p);
        var v=Variants.Resolve(p,new(){AutoBurst=(bits&1)!=0,AutoMitigation=(bits&2)!=0,AutoMechanics=(bits&4)!=0});
        var ids=v.Settings["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
        foreach(var id in ids){
            if(LivePresetPlan.Parents.GetValueOrDefault(id,[]).Any(parent=>!ids.Contains(parent)))throw new Exception(p.Job+" missing parent");
            if(LivePresetPlan.Conflicts.GetValueOrDefault(id,[]).Any(ids.Contains))throw new Exception(p.Job+" conflicting features");
        }
        var route=KeyboardLayout.Compile(v,true);Equal(route.Values.Distinct().Count(),12);
        Equal(JsonSerializer.Serialize(p),original);
    }
});
Test("every preset-only variant transition models Wrath command side effects",()=>{
    foreach(var p in pack.Jobs){
        var states=Enumerable.Range(0,8).Select(bits=>WrathMerge.Merge(new JsonObject{["Version"]=6,["EnabledActionsV6"]=new JsonArray()},[Variants.Resolve(p,new(){AutoBurst=(bits&1)!=0,AutoMitigation=(bits&2)!=0,AutoMechanics=(bits&4)!=0})],null)).ToArray();
        foreach(var before in states)foreach(var after in states){
            var x=(JsonObject)before.DeepClone();var y=(JsonObject)after.DeepClone();x.Remove("EnabledActionsV6");y.Remove("EnabledActionsV6");
            var plan=LivePresetPlan.Create(before,after);
            if(!JsonNode.DeepEquals(x,y)){Equal(plan==null,true);continue;}
            if(plan==null)throw new Exception(p.Job+" should support preset-only transition");
            var ids=before["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
            void Enable(int id){ids.Add(id);foreach(var parent in LivePresetPlan.Parents.GetValueOrDefault(id,[]))Enable(parent);ids.ExceptWith(LivePresetPlan.Conflicts.GetValueOrDefault(id,[]));}
            var initial=ids.ToHashSet();
            var reverse=LivePresetPlan.Create(after,before)??throw new Exception("Missing reverse plan");
            // Every partial command failure must be reversible, including implicit parents/conflicts.
            for(var prefix=0;prefix<=plan.Count;prefix++){
                ids=initial.ToHashSet();
                foreach(var (id,on) in plan.Take(prefix)){if(on)Enable(id);else ids.Remove(id);}
                foreach(var (id,on) in reverse){if(on)Enable(id);else ids.Remove(id);}
                Equal(ids.SetEquals(initial),true);
            }
            ids=initial.ToHashSet();
            foreach(var (id,on) in plan){if(on)Enable(id);else ids.Remove(id);}
            Equal(ids.SetEquals(after["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>())),true);
        }
    }
});
Test("global automation selection persists without per-job selections",()=>{
    var c=new JobSetup.Config{Automation=new(){AutoBurst=true,AutoMitigation=false,AutoMechanics=true}};
    var json=Newtonsoft.Json.JsonConvert.SerializeObject(c);
    var restored=Newtonsoft.Json.JsonConvert.DeserializeObject<JobSetup.Config>(json)!;
    Equal(restored.Automation.AutoBurst,true);Equal(restored.Automation.AutoMitigation,false);Equal(restored.Automation.AutoMechanics,true);
    Equal(json.Contains("Variants"),false);
    var legacy=Newtonsoft.Json.JsonConvert.DeserializeObject<JobSetup.Config>("""{"Variants":{"GNB":{"AutoBurst":true}},"CompactKeyboard":false}""")!;
    Equal(legacy.Automation.AutoBurst,false);Equal(legacy.CompactKeyboard,false);
});
Test("macro exact presets, scope and status cover all eight combinations",()=>{
    for(var bits=0;bits<8;bits++){
        var expected=new VariantSelection{AutoMitigation=(bits&1)!=0,AutoBurst=(bits&2)!=0,AutoMechanics=(bits&4)!=0};
        var flags=AutomationCommands.Letters(expected);
        foreach(var scope in new[]{"current","all"}){
            var command=AutomationCommands.Parse(scope+" "+(flags.Length==0?"none":flags),new(){AutoBurst=true,AutoMitigation=true,AutoMechanics=true});
            Equal(command.All,scope=="all");Equal(AutomationCommands.Letters(command.Selection),flags);
            Equal(AutomationCommands.Status(command.Selection),flags.Length==0?"":"Job Settings: "+flags);
        }
    }
});
Test("macro toggles preserve unrelated selections and input",()=>{
    var current=new VariantSelection{AutoMitigation=true,AutoBurst=false,AutoMechanics=true};
    Equal(AutomationCommands.Letters(AutomationCommands.Parse("current toggle mb",current).Selection),"BJ");
    Equal(AutomationCommands.Letters(AutomationCommands.Parse("all on b",current).Selection),"MBJ");
    Equal(AutomationCommands.Letters(AutomationCommands.Parse("all off mj",current).Selection),"");
    Equal(AutomationCommands.Letters(AutomationCommands.Parse(" CURRENT ",current).Selection),"MJ");
    Equal(AutomationCommands.Letters(current),"MJ");
});
Test("invalid macro arguments fail without changing selection",()=>{
    var selection=new VariantSelection{AutoBurst=true};
    foreach(var input in new[]{"", "mbj", "alll mbj", "all toggle", "current off none", "all mm", "all mx", "current set mbj", "all mbj extra", "all on b extra"})
        Throws(()=>AutomationCommands.Parse(input,selection));
    Equal(AutomationCommands.Letters(selection),"B");
});
Test("active status recognizes every job variant independently of global selection",()=>{
    foreach(var preset in pack.Jobs)for(var mask=0;mask<8;mask++){
        var choice=new VariantSelection{AutoBurst=(mask&1)!=0,AutoMitigation=(mask&2)!=0,AutoMechanics=(mask&4)!=0};
        var actual=WrathMerge.Merge(new JsonObject{["Version"]=6,["EnabledActionsV6"]=new JsonArray()},[Variants.Resolve(preset,choice)],null);
        var found=AutomationStatus.Infer(preset,actual,choice)??throw new Exception(preset.Job+" unknown status");
        Equal(AutomationCommands.Letters(found),AutomationCommands.Letters(choice));
        Equal(AutomationStatus.Infer(preset,actual,null)!=null,true);
    }
});
Test("class change reads each jobs actual settings and detects external changes",()=>{
    var war=pack.Jobs.Single(j=>j.Job=="WAR");
    var actual=WrathMerge.Merge(new JsonObject{["Version"]=6,["EnabledActionsV6"]=new JsonArray()},[Variants.Resolve(gnb,new(){AutoBurst=true}),war],null);
    Equal(AutomationStatus.Infer(gnb,actual,null)!.AutoBurst,true);
    Equal(AutomationStatus.Infer(war,actual,null)!.AutoBurst,false);
    actual=WrathMerge.Merge(actual,[gnb],null);
    Equal(AutomationStatus.Infer(gnb,actual,new(){AutoBurst=true})!.AutoBurst,false);
    // A half-enabled automatic burst is not a supported variant; never display a false active state.
    actual["EnabledActionsV6"]!.AsArray().Add(7008);
    Equal(AutomationStatus.Infer(gnb,actual,null)==null,true);
});
Test("no-op status uses applied history without changing global controls",()=>{
    var crafter=pack.Jobs.Single(j=>j.Job=="CRP");
    var actual=new JsonObject{["Version"]=6,["EnabledActionsV6"]=new JsonArray()};
    Equal(AutomationCommands.Letters(AutomationStatus.Infer(crafter,actual,null)!),"");
    Equal(AutomationCommands.Letters(AutomationStatus.Infer(crafter,actual,new(){AutoMechanics=true})!),"J");
    var config=new JobSetup.Config{Automation=new(){AutoBurst=true},AppliedAutomation=new(){{"CRP",new(){AutoMechanics=true}}}};
    var restored=Newtonsoft.Json.JsonConvert.DeserializeObject<JobSetup.Config>(Newtonsoft.Json.JsonConvert.SerializeObject(config))!;
    Equal(AutomationCommands.Letters(restored.Automation),"B");Equal(AutomationCommands.Letters(restored.AppliedAutomation["CRP"]),"J");
});
Test("registered-only variants leave unregistered jobs out of every scope",()=>{
    var config=new JobSetup.Config();config.RecordSetup("GNB",pack.WrathVersion,DateTimeOffset.UtcNow);
    Equal(config.SelectPresets(pack.Jobs,37,false,true).Single().Job,"GNB");
    Equal(config.SelectPresets(pack.Jobs,21,true,true).Single().Job,"GNB");
    try {config.SelectPresets(pack.Jobs,21,false,true);throw new Exception("Unregistered current job allowed");}catch(InvalidOperationException){}
    Equal(config.SelectPresets(pack.Jobs,21,false,false).Single().Job,"WAR");
    Equal(config.SelectPresets(pack.Jobs,21,true,false).Count,32);
    var original=WrathMerge.Merge(new JsonObject{["Version"]=6,["EnabledActionsV6"]=new JsonArray()},pack.Jobs,null);
    var changed=WrathMerge.Merge(original,config.SelectPresets(pack.Jobs,37,true,true).Select(p=>Variants.Resolve(p,new(){AutoBurst=true})),null);
    var war=pack.Jobs.Single(p=>p.Job=="WAR");
    Equal(AutomationStatus.Infer(war,changed,null)!.AutoBurst,false);
    Equal(AutomationStatus.Infer(gnb,changed,null)!.AutoBurst,true);
});
Test("managed registry migrates only recorded applications and persists timestamps",()=>{
    var config=new JobSetup.Config{AppliedAutomation=new(){{"GNB",new(){AutoBurst=true}}}};
    Equal(config.MigrateManagedHistory(pack.Jobs),true);Equal(config.ManagedJobs.Count,1);
    Equal(config.ManagedJobs["GNB"].ImportedFromHistory,true);Equal(config.ManagedJobs["GNB"].FirstAppliedUtc==null,true);
    var first=DateTimeOffset.UtcNow;config.RecordSetup("PLD",pack.WrathVersion,first);config.RecordSetup("PLD",pack.WrathVersion,first.AddMinutes(1));
    Equal(config.ManagedJobs["PLD"].FirstAppliedUtc,first);Equal(config.ManagedJobs["PLD"].LastAppliedUtc,first.AddMinutes(1));
    Equal(config.SelectPresets(pack.Jobs,1,false,true).Single().Job,"PLD");
    var copy=Newtonsoft.Json.JsonConvert.DeserializeObject<JobSetup.Config>(Newtonsoft.Json.JsonConvert.SerializeObject(config))!;
    Equal(copy.ManagedJobs["PLD"].FirstAppliedUtc,first);Equal(copy.ManagedJobs.Count,2);
    copy.ManagedJobs.Clear();copy.AppliedAutomation.Clear();Equal(copy.MigrateManagedHistory(pack.Jobs),false);Equal(copy.ManagedJobs.Count,0);
});
Test("variant switches preserve targeting sliders arrays and unrelated feature choices",()=>{
    foreach(var preset in pack.Jobs){
        var original=WrathMerge.Merge(new JsonObject{["Version"]=6,["EnabledActionsV6"]=new JsonArray(999999)},[preset],null);
        original["CustomIntValuesV6"]=new JsonObject{["CustomUserSlider"]=47};
        original["UseFocusTargetOverrideInDefaultHealStack"]=false;
        original["CustomHealStack"]=new JsonArray(4,3,2,1);
        for(var mask=0;mask<8;mask++){
            var result=VariantSwitch.Merge(original,[preset],new(){AutoBurst=(mask&1)!=0,AutoMitigation=(mask&2)!=0,AutoMechanics=(mask&4)!=0});
            var before=(JsonObject)original.DeepClone();var after=(JsonObject)result.DeepClone();before.Remove("EnabledActionsV6");after.Remove("EnabledActionsV6");
            Equal(JsonNode.DeepEquals(before,after),true);
            Equal(result["EnabledActionsV6"]!.AsArray().Any(n=>n!.GetValue<int>()==999999),true);
            Equal(LivePresetPlan.Create(original,result)!=null,true);
        }
    }
});
Test("all eight prepared variants switch live without custom-value changes",()=>{
    foreach(var preset in pack.Jobs){
        var prepared=WrathMerge.Merge(new JsonObject{["Version"]=6,["EnabledActionsV6"]=new JsonArray()},[preset],null);
        var states=Enumerable.Range(0,8).Select(mask=>VariantSwitch.Merge(prepared,[preset],new(){AutoBurst=(mask&1)!=0,AutoMitigation=(mask&2)!=0,AutoMechanics=(mask&4)!=0})).ToArray();
        foreach(var before in states)foreach(var after in states)Equal(LivePresetPlan.Create(before,after)!=null,true);
    }
});
Test("static burst setup preserves a complete manual Dancer button",()=>{
    var dancer=pack.Jobs.Single(p=>p.Job=="DNC");var ids=dancer.Settings["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
    Equal(ids.Contains(4018)||ids.Contains(4045),false);Equal(ids.Contains(4111)&&ids.Contains(4112),true);
    Equal(dancer.Settings["CustomIntValuesV6"]!["DNC_ST_ADV_TS_IncludeTS"]!.GetValue<int>(),1);
    Equal(dancer.Settings["CustomIntValuesV6"]!["DNC_AoE_Adv_TS_IncludeTS"]!.GetValue<int>(),1);
    foreach(var p in pack.Jobs)foreach(var variant in new[]{p.AutoBurst,p.AutoMitigation,p.AutoMechanics}.OfType<PresetVariant>())Equal(variant.Settings.Count,0);
});
Test("queued saves wait without premature rollback and require persisted confirmation",()=>{
    var start=DateTime.UtcNow;var deadline=start.AddSeconds(10);
    Equal(LiveSaveConfirmation.Check(true,false,start,deadline),SaveConfirmation.Waiting);
    Equal(LiveSaveConfirmation.Check(true,false,start.AddMilliseconds(100),deadline),SaveConfirmation.Waiting);
    Equal(LiveSaveConfirmation.Check(true,true,start.AddSeconds(1),deadline),SaveConfirmation.Confirmed);
    Equal(LiveSaveConfirmation.Check(false,true,start.AddSeconds(1),deadline),SaveConfirmation.Waiting);
    Equal(LiveSaveConfirmation.Check(true,false,deadline,deadline),SaveConfirmation.TimedOut);
    Equal(LiveSaveConfirmation.Matches(new[]{1,9,88},new Dictionary<int,bool>{{1,true},{2,false}}),true);
    Equal(LiveSaveConfirmation.Matches(new[]{1,2},new Dictionary<int,bool>{{1,true},{2,false}}),false);
});
Test("old Bard Dancer Black Mage setup revisions require one explicit refresh",()=>{
    foreach(var name in new[]{"BRD","DNC","BLM"}){
        var job=pack.Jobs.Single(p=>p.Job==name);var config=new JobSetup.Config();config.RecordSetup(name,pack.WrathVersion,DateTimeOffset.UtcNow);
        try{config.SelectPresets(pack.Jobs,job.JobId,false,true);throw new Exception("Old setup accepted");}catch(InvalidOperationException){}
        config.RecordSetup(name,pack.WrathVersion,DateTimeOffset.UtcNow,job.SetupRevision);
        Equal(config.SelectPresets(pack.Jobs,job.JobId,false,true).Single().Job,name);
    }
});
Console.WriteLine($"{count} tests passed.");
