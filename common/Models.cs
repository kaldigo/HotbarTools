using System.Text.Json;
using System.Text.Json.Nodes;

namespace HotbarTools;

public readonly record struct SlotValue(byte Type, uint Id)
{
    public SlotValue Normalized => Id == 0 ? default : this;
    public override string ToString() => Id == 0 ? "Empty" : $"Type {Type}, ID {Id}";
}
public readonly record struct Position(int Bar, int Slot)
{
    public bool Valid => Bar is >= 0 and < 18 && Slot >= 0 && Slot < (Bar < 10 ? 12 : 16);
    public override string ToString() => $"{(Bar < 10 ? "Bar" : "Cross")} {(Bar < 10 ? Bar + 1 : Bar - 9)} / {Slot + 1}";
}
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
