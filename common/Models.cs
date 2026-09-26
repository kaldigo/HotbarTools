using System.Text.Json;
using System.Text.Json.Nodes;

namespace HotbarTools;

public readonly record struct SlotValue(byte Type, uint Id)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public SlotValue Normalized => Type == 0 ? default : this;
    public override string ToString() => Type == 0 ? "Empty" : $"Type {Type}, ID {Id}";
}
public readonly record struct Position(int Bar, int Slot)
{
    public bool Valid => Bar is >= 0 and < 18 && Slot >= 0 && Slot < (Bar < 10 ? 12 : 16);
    public override string ToString() => $"{(Bar < 10 ? "Bar" : "Cross")} {(Bar < 10 ? Bar + 1 : Bar - 9)} / {Slot + 1}";
}
public sealed record SlotEdit(uint Job, Position Position, SlotValue Before, SlotValue After);
public sealed class SlotMap
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = "New pair";
    public int RegularBar { get; set; } = 1;
    public int RegularSlot { get; set; } = 1;
    public int CrossSet { get; set; } = 1;
    public int CrossSlot { get; set; } = 1;
    public string Profile { get; set; } = "Combat";
    public bool SharedAcrossJobs { get; set; }
    public bool Enabled { get; set; } = true;
    public Position Regular => new(RegularBar - 1, RegularSlot - 1);
    public Position Cross => new(CrossSet + 9, CrossSlot - 1);
    public bool Applies(uint job) => Enabled && (Profile == "All" || (Profile == "Healer" ? Defaults.IsHealer(job) : Defaults.IsCombat(job) && !Defaults.IsHealer(job)));
}
public static class Defaults
{
    public static bool IsHealer(uint job) => job is 6 or 24 or 28 or 33 or 40;
    public static bool IsCombat(uint job) => job is >= 1 and <= 7 or >= 19 and <= 42 or 29;
    public static List<SlotMap> Maps()
    {
        var result = new List<SlotMap>();
        var positions = new[] {(1,1),(1,2),(1,3),(1,4),(2,1),(2,2),(2,3),(2,4),(2,12),(2,11),(2,5),(2,6)};
        var normal = new[] {16,15,14,12,13,11,9,10,4,2,3,1};
        var healer = new[] {16,15,13,14,10,12,9,11,4,2,3,1};
        foreach (var profile in new[] {"Combat","Healer"})
            for (var i=0;i<positions.Length;i++)
                result.Add(new SlotMap { Id=$"{profile}-{i}", Label=$"{profile} control {i+1}", Profile=profile,
                    RegularBar=positions[i].Item1, RegularSlot=positions[i].Item2, CrossSet=1, CrossSlot=(profile=="Healer"?healer:normal)[i] });
        for(var i=0;i<4;i++) result.Add(new SlotMap { Id=$"shared-{i}", Label=new[]{"Potion (9)","Potion (0)","Limit Break (-)","Sprint (=)"}[i], Profile="All", RegularBar=1, RegularSlot=9+i, CrossSlot=5+i, SharedAcrossJobs=true });
        for(var i=0;i<16;i++) result.Add(new SlotMap { Id=$"utility-{i}", Label=$"Utility {i+1}", Profile="All", RegularBar=i<12?10:9, RegularSlot=i<12?i+1:i-11, CrossSet=2, CrossSlot=i+1, SharedAcrossJobs=true });
        return result;
    }
    public static void Validate(IEnumerable<SlotMap> maps)
    {
        var list=maps.ToList();
        if(list.Count>128 || list.Select(m=>m.Id).Distinct().Count()!=list.Count) throw new InvalidDataException("Too many pairs or duplicate pair IDs.");
        foreach(var m in list)
            if(!m.Regular.Valid || m.Regular.Bar>=10 || !m.Cross.Valid || m.Cross.Bar<10 || m.Profile is not ("All" or "Combat" or "Healer")) throw new InvalidDataException($"Invalid mapping: {m.Label}");
        foreach(uint job in Enumerable.Range(1,42).Select(x=>(uint)x))
        {
            var used=new HashSet<Position>();
            foreach(var m in list.Where(m=>m.Applies(job)))
                if(!used.Add(m.Regular) || !used.Add(m.Cross)) throw new InvalidDataException($"Overlapping pairs for class/job {job}: {m.Label}");
        }
    }
}
public enum SyncChoice { None, ToCross, ToRegular, Conflict }
public static class SyncRules
{
    public static SyncChoice Decide(SlotValue regular, SlotValue cross, SlotValue previous)
    {
        regular=regular.Normalized;cross=cross.Normalized;previous=previous.Normalized;
        if(regular==cross)return SyncChoice.None;
        if(regular==previous)return SyncChoice.ToRegular;
        if(cross==previous)return SyncChoice.ToCross;
        return SyncChoice.Conflict;
    }
}
public sealed class PresetSlot
{
    public int LogicalSlot { get; set; }
    public int RegularBar { get; set; }
    public int RegularSlot { get; set; }
    public int CrossSlot { get; set; }
    public string Action { get; set; } = "";
    public string Function { get; set; } = "";
}
public sealed class JobPreset
{
    public uint JobId { get; set; }
    public string Job { get; set; } = "";
    public uint[] BaseClasses { get; set; } = [];
    public PresetSlot[] Slots { get; set; } = [];
    public int[] OwnedPresetIds { get; set; } = [];
    public JsonObject Settings { get; set; } = new();
    public PresetVariant? AutoBurst { get; set; }
    public PresetVariant? AutoMitigation { get; set; }
    public PresetVariant? AutoMechanics { get; set; }
    public bool MechanicsAlreadyAutomatic { get; set; }
    public string MechanicsDescription { get; set; } = "";
}
public sealed class PresetPack
{
    public int SchemaVersion { get; set; }
    public string WrathVersion { get; set; } = "";
    public List<JobPreset> Jobs { get; set; } = [];
    public JsonObject SharedSettings { get; set; } = new();
}
public static class WrathMerge
{
    public static JsonObject Merge(JsonObject original,IEnumerable<JobPreset> jobs,JsonObject? shared)
    {
        if(original["Version"]?.GetValue<int>()!=6)throw new InvalidDataException("Expected Wrath configuration version 6.");
        var result=(JsonObject)original.DeepClone();
        var enabled=(result["EnabledActionsV6"] as JsonArray ?? throw new InvalidDataException("Missing enabled presets.")).Select(n=>n!.GetValue<int>()).ToHashSet();
        foreach(var job in jobs)
        {
            enabled.ExceptWith(job.OwnedPresetIds);
            Apply(job.Settings);
        }
        if(shared!=null)Apply(shared);
        result["EnabledActionsV6"]=new JsonArray(enabled.Order().Select(n=>JsonValue.Create(n) as JsonNode).ToArray());
        return result;
        void Apply(JsonObject fragment)
        {
            foreach(var (key,value) in fragment)
            {
                if(key=="EnabledActionsV6") { foreach(var n in (JsonArray)value!)enabled.Add(n!.GetValue<int>()); }
                else if(value is JsonObject obj)
                {
                    if(result[key] is not JsonObject)result[key]=new JsonObject();
                    foreach(var (k,v) in obj) ((JsonObject)result[key]!)[k]=v?.DeepClone();
                }
                else result[key]=value?.DeepClone();
            }
        }
    }
}
public static class JsonStore
{
    public static readonly JsonSerializerOptions Options=new(){WriteIndented=true};
    public static void Write(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp=path+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(value,Options));
        File.Move(temp,path,true);
    }
}

public static class WrathWritePolicy
{
    public static void Validate(bool installed,bool loaded)
    {
        if(loaded)throw new InvalidOperationException("Disable Wrath Combo in /xlplugins first. Apply and restore are blocked while it is enabled.");
        if(!installed)throw new InvalidOperationException("Install Wrath Combo first.");
    }
}

public enum LiveStartChoice { SaveMapping, Initialize, Resume }
public static class LiveSyncStart
{
    public static LiveStartChoice Decide(bool mappingDirty,IEnumerable<string> baselineKeys,int revision)
    {
        if(mappingDirty)return LiveStartChoice.SaveMapping;
        return baselineKeys.Any(k=>k.StartsWith($"{revision}:",StringComparison.Ordinal))
            ? LiveStartChoice.Resume : LiveStartChoice.Initialize;
    }
}

public sealed class VariantSelection
{
    public bool AutoBurst { get; set; }
    public bool AutoMitigation { get; set; }
    public bool AutoMechanics { get; set; }
}
public sealed class PresetVariant
{
    public string Description { get; set; } = "";
    public int[] Enable { get; set; } = [];
    public int[] Disable { get; set; } = [];
    public JsonObject Settings { get; set; } = new();
    public int[] EmptySlots { get; set; } = [];
}
public static class Variants
{
    public static JobPreset Resolve(JobPreset baseline,VariantSelection selection)
    {
        var result=JsonSerializer.Deserialize<JobPreset>(JsonSerializer.Serialize(baseline))!;
        if(selection.AutoBurst)Apply(baseline.AutoBurst);
        if(selection.AutoMitigation)Apply(baseline.AutoMitigation);
        if(selection.AutoMechanics && !baseline.MechanicsAlreadyAutomatic)Apply(baseline.AutoMechanics);
        return result;
        void Apply(PresetVariant? variant)
        {
            if(variant==null)throw new InvalidDataException($"This variant has not been reviewed for {baseline.Job}.");
            if(variant.Enable.Concat(variant.Disable).Any(id=>!baseline.OwnedPresetIds.Contains(id)))
                throw new InvalidDataException("Variant references another job's presets.");
            var enabled=result.Settings["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
            enabled.ExceptWith(variant.Disable);enabled.UnionWith(variant.Enable);
            result.Settings["EnabledActionsV6"]=new JsonArray(enabled.Order().Select(n=>(JsonNode?)JsonValue.Create(n)).ToArray());
            foreach(var (key,value) in variant.Settings)
            {
                if(key=="EnabledActionsV6")throw new InvalidDataException("Use variant Enable/Disable for preset membership.");
                if(value is JsonObject obj)
                {
                    if(result.Settings[key] is not JsonObject)result.Settings[key]=new JsonObject();
                    foreach(var (k,v) in obj)result.Settings[key]![k]=v?.DeepClone();
                }
                else result.Settings[key]=value?.DeepClone();
            }
            foreach(var id in variant.EmptySlots)
            {
                var slot=result.Slots.Single(s=>s.LogicalSlot==id);
                slot.Action="Empty";slot.Function="Unused with selected automation variant";
            }
        }
    }
}
public static class PairedApply
{
    public static void Run(Action hotbars,Action settings,Action rollbackHotbars)
    {
        hotbars();
        try { settings(); }
        catch(Exception original)
        {
            try { rollbackHotbars(); }
            catch(Exception rollback) { throw new AggregateException("Settings failed and hotbar rollback failed; use the saved backup.",original,rollback); }
            throw;
        }
    }
}

public static class KeyboardLayout
{
    public static readonly Position[] Positions=[new(0,0),new(0,1),new(0,2),new(0,3),new(1,0),new(1,1),new(1,2),new(1,3),new(1,4),new(1,5),new(1,11),new(1,10)];
    // Maps a function's original regular position to its compiled destination.
    public static Dictionary<Position,Position> Compile(JobPreset preset,bool compact)
    {
        var result=Positions.ToDictionary(p=>p,p=>p);
        if(!compact)return result;
        var healer=Defaults.IsHealer(preset.JobId);
        var tank=preset.JobId is 19 or 21 or 32 or 37;
        var locked=preset.Slots.Where(s=>s.LogicalSlot is 1 or 2 ||
            ((tank||healer)&&s.LogicalSlot is 9 or 10) ||
            (!healer && s.LogicalSlot==4 && s.Action!="Empty"))
            .Select(s=>new Position(s.RegularBar-1,s.RegularSlot-1)).ToHashSet();
        var actions=preset.Slots.Where(s=>s.Action!="Empty").Select(s=>new Position(s.RegularBar-1,s.RegularSlot-1)).ToHashSet();
        var available=Positions.Where(p=>!locked.Contains(p)).ToArray();
        var ordered=available.Where(actions.Contains).Concat(available.Where(p=>!actions.Contains(p))).ToArray();
        for(var i=0;i<available.Length;i++)result[ordered[i]]=available[i];
        return result;
    }
}
public static class LayoutRoutes
{
    public static List<SlotMap> Effective(IEnumerable<SlotMap> maps,Dictionary<string,Position>? routes)
    {
        var result=JsonSerializer.Deserialize<List<SlotMap>>(JsonSerializer.Serialize(maps))!;
        if(routes!=null)foreach(var map in result)
            if(routes.TryGetValue(map.Id,out var p))
            {
                if(map.SharedAcrossJobs)throw new InvalidDataException("Shared controls cannot be moved by a job layout.");
                map.RegularBar=p.Bar+1;map.RegularSlot=p.Slot+1;
            }
        Defaults.Validate(result);return result;
    }
}

public sealed class LayoutBackup
{
    public List<SlotEdit> Edits { get; set; }=[];
    public Dictionary<uint,Dictionary<string,Position>>? Routes { get; set; }
}

public static class LivePresetPlan
{
    // Reviewed GNB options, including removal/restoration of the conflicting manual No Mercy feature.
    public static readonly Dictionary<int,string> Supported=new()
    {
        [7008]="GNB_ST_NoMercy",[7011]="GNB_ST_Bloodfest",[7201]="GNB_AoE_NoMercy",[7204]="GNB_AoE_Bloodfest",
        [7702]="GNB_Mit_Advanced_NonBoss_Rampart",[7703]="GNB_Mit_Advanced_NonBoss_Nebula",[7708]="GNB_Mit_Advanced_NonBoss_Reprisal",
        [7718]="GNB_Mit_Advanced_Boss_Nebula",[7719]="GNB_Mit_Advanced_Boss_Rampart",
        [7500]="GNB_NM_Features",[7501]="GNB_NM_Bloodfest"
    };
    public static Dictionary<int,bool>? Create(JsonObject before,JsonObject after)
    {
        if(before["Version"]?.GetValue<int>()!=6||after["Version"]?.GetValue<int>()!=6)return null;
        var a=(JsonObject)before.DeepClone();var b=(JsonObject)after.DeepClone();
        if(a["EnabledActionsV6"] is not JsonArray aa || b["EnabledActionsV6"] is not JsonArray bb)return null;
        var old=aa.Select(n=>n!.GetValue<int>()).ToHashSet();var next=bb.Select(n=>n!.GetValue<int>()).ToHashSet();
        a.Remove("EnabledActionsV6");b.Remove("EnabledActionsV6");
        if(!JsonNode.DeepEquals(a,b))return null;
        var changes=old.Except(next).ToDictionary(id=>id,_=>false);
        foreach(var id in next.Except(old))changes[id]=true;
        if(changes.Keys.Any(id=>!Supported.ContainsKey(id)))return null;
        // Enabling a child must not cause Wrath's command to enable a previously-disabled parent.
        foreach(var id in changes.Where(p=>p.Value).Select(p=>p.Key))
        {
            var parents=id==7500?Array.Empty<int>():id==7501?new[]{7500}:id is 7008 or 7011?new[]{7003}:id is 7201 or 7204?new[]{7200}:id is 7702 or 7703 or 7708?new[]{7700,7701}:new[]{7700,7711};
            if(parents.Any(p=>!next.Contains(p)))return null;
        }
        if(next.Contains(7500) && (next.Contains(7008)||next.Contains(7201)))return null;
        return changes.OrderBy(p=>p.Value).ThenBy(p=>p.Key==7500?0:p.Key).ToDictionary(p=>p.Key,p=>p.Value);
    }
}
