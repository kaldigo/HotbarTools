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
Test("empty type noise ignored",()=>Equal(SyncRules.Decide(new(10,0),default,old),SyncChoice.None));
Test("default mappings valid",()=>Defaults.Validate(Defaults.Maps()));
Test("overlapping pairs rejected",()=>{var m=Defaults.Maps();m.Add(new SlotMap{Profile="All"});Throws(()=>Defaults.Validate(m));});
Test("out-of-range mapping rejected",()=>{var m=Defaults.Maps();m[0].CrossSet=9;Throws(()=>Defaults.Validate(m));});
Test("regular/cross position bounds",()=>{Equal(new Position(9,12).Valid,false);Equal(new Position(10,15).Valid,true);Equal(new Position(18,0).Valid,false);});
Test("healer physical buttons",()=>{var m=Defaults.Maps().Single(x=>x.Applies(24)&&x.Regular==new Position(0,2));Equal(m.Cross,new Position(10,12));});
Test("gunbreaker physical buttons",()=>{var m=Defaults.Maps().Single(x=>x.Applies(37)&&x.Regular==new Position(0,2));Equal(m.Cross,new Position(10,13));});
Test("utility bar exact verified positions",()=>{var m=Defaults.Maps().Where(x=>x.Id.StartsWith("utility-")).ToList();Equal(m.Count,16);Equal(m.Count(x=>x.RegularBar==10),12);Equal(string.Join(",",m.Where(x=>x.RegularBar==9).Select(x=>x.RegularSlot)),"1,2,3,4");});
Test("shared keys exact destinations",()=>{var m=Defaults.Maps().Where(x=>x.Id.StartsWith("shared-")).ToList();Equal(string.Join(",",m.Select(x=>x.RegularSlot)),"9,10,11,12");Equal(string.Join(",",m.Select(x=>x.CrossSlot)),"5,6,7,8");});
var original=JsonNode.Parse("""{"Version":6,"EnabledActionsV6":[10,20,999],"CustomIntValuesV6":{"A":1,"B":2},"Other":"keep"}""")!.AsObject();
var job=new JobPreset{OwnedPresetIds=[10,11],Settings=JsonNode.Parse("""{"EnabledActionsV6":[11],"CustomIntValuesV6":{"A":3}}""")!.AsObject()};
Test("current-job merge preserves other jobs",()=>{var result=WrathMerge.Merge(original,[job],null);Equal(result["EnabledActionsV6"]!.ToJsonString(),"[11,20,999]");Equal(result["CustomIntValuesV6"]!["B"]!.GetValue<int>(),2);Equal(result["Other"]!.GetValue<string>(),"keep");});
Test("merge leaves original untouched",()=>{WrathMerge.Merge(original,[job],null);Equal(original["CustomIntValuesV6"]!["A"]!.GetValue<int>(),1);});
Test("shared options require explicit argument",()=>{var shared=JsonNode.Parse("""{"UseCustomHealStack":true}""")!.AsObject();Equal(WrathMerge.Merge(original,[job],null).ContainsKey("UseCustomHealStack"),false);Equal(WrathMerge.Merge(original,[job],shared)["UseCustomHealStack"]!.GetValue<bool>(),true);});
Test("wrong Wrath schema rejected",()=>Throws(()=>WrathMerge.Merge(new JsonObject{["Version"]=7},[job],null)));
var pack=JsonSerializer.Deserialize<PresetPack>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"presets.json")))!;
Test("21 jobs and 9 base classes",()=>{Equal(pack.Jobs.Count,21);Equal(pack.Jobs.Sum(j=>j.BaseClasses.Length),9);Equal(pack.Jobs.SelectMany(j=>j.BaseClasses).Distinct().Count(),9);});
Test("preset mappings match Bridge defaults",()=>{foreach(var j in pack.Jobs)foreach(var s in j.Slots){var map=Defaults.Maps().Single(m=>m.Applies(j.JobId)&&m.Regular==new Position(s.RegularBar-1,s.RegularSlot-1));Equal(map.Cross,new Position(10,s.CrossSlot-1));Equal(map.SharedAcrossJobs,false);}});
Test("all preset targets are unique",()=>{foreach(var j in pack.Jobs){Equal(j.Slots.Select(s=>(s.RegularBar,s.RegularSlot)).Distinct().Count(),j.Slots.Length);Equal(j.Slots.Select(s=>s.CrossSlot).Distinct().Count(),j.Slots.Length);}});
Test("no shared utility is overwritten by job presets",()=>{foreach(var j in pack.Jobs)foreach(var s in j.Slots){Equal(s.RegularBar==1&&s.RegularSlot>=9,false);Equal(s.CrossSlot is >=5 and <=8,false);}});
Test("base action roots preserve level-sync use",()=>{Equal(pack.Jobs.Single(j=>j.Job=="GNB").Slots.Single(s=>s.LogicalSlot==6).Action,"Heart of Stone");Equal(pack.Jobs.Single(j=>j.Job=="WHM").Slots.Single(s=>s.LogicalSlot==1).Action,"Stone");});
Test("whole pack merges without duplicate presets",()=>{var r=WrathMerge.Merge(original,pack.Jobs,pack.SharedSettings);var ids=r["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToList();Equal(ids.Count,ids.Distinct().Count());});
Test("Wrath writes blocked while loaded",()=>{try{WrathWritePolicy.Validate(true,true);}catch(InvalidOperationException){return;}throw new Exception("Loaded Wrath permitted");});
Test("Wrath writes blocked when absent",()=>{try{WrathWritePolicy.Validate(false,false);}catch(InvalidOperationException){return;}throw new Exception("Absent Wrath permitted");});
Test("Wrath writes permitted only installed and disabled",()=>WrathWritePolicy.Validate(true,false));
Console.WriteLine($"{count} tests passed.");
