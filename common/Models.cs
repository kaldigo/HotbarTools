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
    public bool Applies(uint job) => Enabled && (Profile == "All" || (Profile == "Noncombat" ? job is >=8 and <=18 : Profile == "Healer" ? Defaults.IsHealer(job) : Defaults.IsCombat(job) && !Defaults.IsHealer(job)));
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
        foreach (var profile in new[] {"Combat","Healer","Noncombat"})
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
            if(!m.Regular.Valid || m.Regular.Bar>=10 || !m.Cross.Valid || m.Cross.Bar<10 || m.Profile is not ("All" or "Combat" or "Healer" or "Noncombat")) throw new InvalidDataException($"Invalid mapping: {m.Label}");
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
    public string Usage { get; set; } = "";
    public string[] OmittedActions { get; set; } = [];
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
            (Defaults.IsCombat(preset.JobId) && !healer && s.LogicalSlot==4 && s.Action!="Empty"))
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
    // Generated from pinned Wrath preset metadata; only reviewed variant deltas are writable.
    public static readonly Dictionary<int,string> Supported=new()
    {
        [1075]="AST_Raidwide",
        [1076]="AST_Raidwide_CollectiveUnconscious",
        [1077]="AST_Raidwide_NeutralSect",
        [2001]="BLM_ST_SimpleMode",
        [2002]="BLM_AoE_SimpleMode",
        [2052]="BLM_Blizzard1and3",
        [2071]="BLM_B1toB4",
        [2100]="BLM_ST_AdvancedMode",
        [2103]="BLM_ST_LeyLines",
        [2199]="BLM_ST_Manaward",
        [2200]="BLM_AoE_AdvancedMode",
        [2202]="BLM_AoE_LeyLines",
        [3009]="BRD_ST_AdvMode",
        [3015]="BRD_AoE_AdvMode",
        [3017]="BRD_Adv_Buffs",
        [3032]="BRD_AoE_Adv_Buffs",
        [3035]="BRD_AoE_SimpleMode",
        [3036]="BRD_ST_SimpleMode",
        [3070]="BRD_Adv_NaturesMinne",
        [4001]="DNC_ST_SimpleMode",
        [4002]="DNC_AoE_SimpleMode",
        [4010]="DNC_ST_AdvancedMode",
        [4018]="DNC_ST_Adv_TS",
        [4040]="DNC_AoE_AdvancedMode",
        [4045]="DNC_AoE_Adv_TS",
        [4070]="DNC_ST_MultiButton",
        [4071]="DNC_ST_EspritOvercap",
        [4072]="DNC_ST_FanDanceOvercap",
        [4073]="DNC_ST_FanDance34",
        [4090]="DNC_AoE_MultiButton",
        [4091]="DNC_AoE_EspritOvercap",
        [4092]="DNC_AoE_FanDanceOvercap",
        [4093]="DNC_AoE_FanDance34",
        [5001]="DRK_ST_Simple",
        [5002]="DRK_AoE_Simple",
        [5010]="DRK_ST_Adv",
        [5013]="DRK_ST_CDs",
        [5015]="DRK_ST_CD_Delirium",
        [5016]="DRK_ST_CD_Shadow",
        [5050]="DRK_AoE_Adv",
        [5051]="DRK_AoE_CDs",
        [5054]="DRK_AoE_CD_Delirium",
        [5055]="DRK_AoE_CD_Shadow",
        [5300]="DRK_Mitigation",
        [5301]="DRK_Mitigation_NonBoss",
        [5302]="DRK_Mitigation_NonBoss_Rampart",
        [5303]="DRK_Mitigation_NonBoss_ShadowWall",
        [5309]="DRK_Mitigation_Boss",
        [5313]="DRK_Mitigation_NonBoss_Reprisal",
        [5315]="DRK_Mitigation_Boss_ShadowWall",
        [5316]="DRK_Mitigation_Boss_Rampart",
        [6001]="DRG_ST_SimpleMode",
        [6100]="DRG_ST_AdvancedMode",
        [6102]="DRG_ST_Buffs",
        [6103]="DRG_ST_BattleLitany",
        [6104]="DRG_ST_LanceCharge",
        [6105]="DRG_ST_Damage",
        [6116]="DRG_ST_Geirskogul",
        [6195]="DRG_ST_Feint",
        [6200]="DRG_AoE_SimpleMode",
        [6201]="DRG_AoE_AdvancedMode",
        [6202]="DRG_AoE_Buffs",
        [6203]="DRG_AoE_BattleLitany",
        [6204]="DRG_AoE_LanceCharge",
        [6205]="DRG_AoE_Damage",
        [6216]="DRG_AoE_Geirskogul",
        [7008]="GNB_ST_NoMercy",
        [7011]="GNB_ST_Bloodfest",
        [7201]="GNB_AoE_NoMercy",
        [7204]="GNB_AoE_Bloodfest",
        [7500]="GNB_NM_Features",
        [7501]="GNB_NM_Bloodfest",
        [7702]="GNB_Mit_Advanced_NonBoss_Rampart",
        [7703]="GNB_Mit_Advanced_NonBoss_Nebula",
        [7708]="GNB_Mit_Advanced_NonBoss_Reprisal",
        [7718]="GNB_Mit_Advanced_Boss_Nebula",
        [7719]="GNB_Mit_Advanced_Boss_Rampart",
        [8001]="MCH_ST_SimpleMode",
        [8100]="MCH_ST_AdvancedMode",
        [8108]="MCH_ST_Adv_WildFire",
        [9003]="MNK_AoE_SimpleMode",
        [9004]="MNK_ST_SimpleMode",
        [9005]="MNK_ST_AdvancedMode",
        [9008]="MNK_STUseBuffs",
        [9009]="MNK_STUseBrotherhood",
        [9011]="MNK_STUseROF",
        [9013]="MNK_STUsePerfectBalance",
        [9027]="MNK_AoE_AdvancedMode",
        [9029]="MNK_AoEUseBuffs",
        [9030]="MNK_AoEUseBrotherhood",
        [9032]="MNK_AoEUseROF",
        [9034]="MNK_AoEUsePerfectBalance",
        [9095]="MNK_ST_Feint",
        [10000]="NIN_ST_SimpleMode",
        [10001]="NIN_AoE_SimpleMode",
        [10002]="NIN_ST_AdvancedMode",
        [10003]="NIN_AoE_AdvancedMode",
        [10006]="NIN_ST_AdvancedMode_TrickAttack",
        [10007]="NIN_ST_AdvancedMode_Mug",
        [10010]="NIN_ST_AdvancedMode_Kassatsu",
        [10011]="NIN_ST_AdvancedMode_TenChiJin",
        [10013]="NIN_ST_AdvancedMode_Meisui",
        [10020]="NIN_ST_AdvancedMode_Feint",
        [10022]="NIN_AoE_AdvancedMode_TrickAttack",
        [10023]="NIN_AoE_AdvancedMode_Mug",
        [10026]="NIN_AoE_AdvancedMode_Kassatsu",
        [10027]="NIN_AoE_AdvancedMode_TenChiJin",
        [10029]="NIN_AoE_AdvancedMode_Meisui",
        [11000]="PLD_ST_SimpleMode",
        [11001]="PLD_AoE_SimpleMode",
        [11002]="PLD_ST_AdvancedMode",
        [11003]="PLD_ST_AdvancedMode_FoF",
        [11015]="PLD_AoE_AdvancedMode",
        [11016]="PLD_AoE_AdvancedMode_FoF",
        [11086]="PLD_Mitigation",
        [11087]="PLD_Mitigation_NonBoss",
        [11088]="PLD_Mitigation_NonBoss_Rampart",
        [11089]="PLD_Mitigation_NonBoss_Sentinel",
        [11095]="PLD_Mitigation_Boss",
        [11099]="PLD_Mitigation_NonBoss_Reprisal",
        [11101]="PLD_Mitigation_Boss_Sentinel",
        [11102]="PLD_Mitigation_Boss_Rampart",
        [12000]="RPR_ST_SimpleMode",
        [12001]="RPR_ST_AdvancedMode",
        [12006]="RPR_ST_ArcaneCircle",
        [12010]="RPR_ST_Enshroud",
        [12022]="RPR_ST_ArcaneCrest",
        [12095]="RPR_ST_Feint",
        [12100]="RPR_AoE_SimpleMode",
        [12101]="RPR_AoE_AdvancedMode",
        [12105]="RPR_AoE_ArcaneCircle",
        [12109]="RPR_AoE_Enshroud",
        [13000]="RDM_ST_SimpleMode",
        [13001]="RDM_ST_DPS",
        [13010]="RDM_ST_Embolden",
        [13011]="RDM_ST_Manafication",
        [13200]="RDM_AoE_SimpleMode",
        [13201]="RDM_AoE_DPS",
        [13207]="RDM_AoE_Embolden",
        [13208]="RDM_AoE_Manafication",
        [14069]="SGE_Raidwide",
        [14071]="SGE_Raidwide_Kerachole",
        [14072]="SGE_Raidwide_Holos",
        [15002]="SAM_ST_SimpleMode",
        [15003]="SAM_ST_AdvancedMode",
        [15011]="SAM_ST_Adv_CDs",
        [15012]="SAM_ST_Adv_Ikishoten",
        [15018]="SAM_ST_Adv_Meikyo",
        [15095]="SAM_ST_Adv_Feint",
        [15102]="SAM_AoE_SimpleMode",
        [15103]="SAM_AoE_AdvancedMode",
        [15108]="SAM_AoE_Adv_Ikishoten",
        [15114]="SAM_AoE_Adv_Meikyo",
        [15115]="SAM_AoE_Adv_CDs",
        [16018]="SCH_AoE_Heal",
        [16023]="SCH_ST_Heal",
        [16040]="SCH_ST_Heal_Dissipation",
        [16041]="SCH_AoE_Heal_Dissipation",
        [16059]="SCH_Raidwide_SacredSoil",
        [16065]="SCH_Raidwide",
        [16074]="SCH_Retarget_Physick",
        [16084]="SCH_Simple_AoE_Heal",
        [16085]="SCH_Simple_ST_Heal",
        [17000]="SMN_ST_Advanced_Combo",
        [17017]="SMN_ST_Advanced_Combo_SearingLight",
        [17041]="SMN_ST_Simple_Combo",
        [17049]="SMN_AoE_Advanced_Combo",
        [17053]="SMN_AoE_Advanced_Combo_SearingLight",
        [17066]="SMN_AoE_Simple_Combo",
        [18000]="WAR_ST_Simple",
        [18001]="WAR_AoE_Simple",
        [18002]="WAR_ST_Advanced",
        [18003]="WAR_ST_InnerRelease",
        [18016]="WAR_AoE_Advanced",
        [18019]="WAR_AoE_InnerRelease",
        [18131]="WAR_Mitigation",
        [18132]="WAR_Mitigation_NonBoss",
        [18133]="WAR_Mitigation_NonBoss_Rampart",
        [18135]="WAR_Mitigation_NonBoss_Vengeance",
        [18137]="WAR_Mitigation_NonBoss_Reprisal",
        [18142]="WAR_Mitigation_Boss",
        [18148]="WAR_Mitigation_Boss_Vengeance",
        [18149]="WAR_Mitigation_Boss_Rampart",
        [19220]="WHM_Raidwide",
        [19221]="WHM_Raidwide_Asylum",
        [19222]="WHM_Raidwide_Temperance",
        [20000]="PCT_ST_SimpleMode",
        [20001]="PCT_AoE_SimpleMode",
        [20005]="PCT_ST_AdvancedMode",
        [20021]="PCT_ST_AdvancedMode_ScenicMuse",
        [20040]="PCT_AoE_AdvancedMode",
        [20054]="PCT_AoE_AdvancedMode_ScenicMuse",
        [20071]="PCT_ST_AdvancedMode_Tempera",
        [30000]="VPR_ST_SimpleMode",
        [30001]="VPR_ST_AdvancedMode",
        [30005]="VPR_ST_SerpentsIre",
        [30011]="VPR_ST_Reawaken",
        [30094]="VPR_ST_Feint",
        [30100]="VPR_AoE_SimpleMode",
        [30101]="VPR_AoE_AdvancedMode",
        [30104]="VPR_AoE_SerpentsIre",
        [30110]="VPR_AoE_Reawaken",
        [30209]="VPR_Legacies",
        [30210]="VPR_SerpentsTail",
    };
    public static readonly Dictionary<int,int[]> Parents=new()
    {
        [1075]=[],
        [1076]=[1075],
        [1077]=[1075],
        [2001]=[],
        [2002]=[],
        [2052]=[],
        [2071]=[],
        [2100]=[],
        [2103]=[2100],
        [2199]=[2100],
        [2200]=[],
        [2202]=[2200],
        [3009]=[],
        [3015]=[],
        [3017]=[3009],
        [3032]=[3015],
        [3035]=[],
        [3036]=[],
        [3070]=[3009],
        [4001]=[],
        [4002]=[],
        [4010]=[],
        [4018]=[4010],
        [4040]=[],
        [4045]=[4040],
        [4070]=[],
        [4071]=[4070],
        [4072]=[4070],
        [4073]=[4070],
        [4090]=[],
        [4091]=[4090],
        [4092]=[4090],
        [4093]=[4090],
        [5001]=[],
        [5002]=[],
        [5010]=[],
        [5013]=[5010],
        [5015]=[5013],
        [5016]=[5013],
        [5050]=[],
        [5051]=[5050],
        [5054]=[5051],
        [5055]=[5051],
        [5300]=[],
        [5301]=[5300],
        [5302]=[5301],
        [5303]=[5301],
        [5309]=[5300],
        [5313]=[5301],
        [5315]=[5309],
        [5316]=[5309],
        [6001]=[],
        [6100]=[],
        [6102]=[6100],
        [6103]=[6102],
        [6104]=[6102],
        [6105]=[6100],
        [6116]=[6105],
        [6195]=[6100],
        [6200]=[],
        [6201]=[],
        [6202]=[6201],
        [6203]=[6202],
        [6204]=[6202],
        [6205]=[6201],
        [6216]=[6205],
        [7001]=[],
        [7002]=[],
        [7003]=[],
        [7008]=[7003],
        [7011]=[7003],
        [7200]=[],
        [7201]=[7200],
        [7204]=[7200],
        [7500]=[],
        [7501]=[7500],
        [7700]=[],
        [7701]=[7700],
        [7702]=[7701],
        [7703]=[7701],
        [7708]=[7701],
        [7711]=[7700],
        [7718]=[7711],
        [7719]=[7711],
        [8001]=[],
        [8100]=[],
        [8108]=[8100],
        [9003]=[],
        [9004]=[],
        [9005]=[],
        [9008]=[9005],
        [9009]=[9008],
        [9011]=[9008],
        [9013]=[9005],
        [9027]=[],
        [9029]=[9027],
        [9030]=[9029],
        [9032]=[9029],
        [9034]=[9027],
        [9095]=[9005],
        [10000]=[],
        [10001]=[],
        [10002]=[],
        [10003]=[],
        [10006]=[10002],
        [10007]=[10002],
        [10010]=[10002],
        [10011]=[10002],
        [10013]=[10002],
        [10020]=[10002],
        [10022]=[10003],
        [10023]=[10003],
        [10026]=[10003],
        [10027]=[10003],
        [10029]=[10003],
        [11000]=[],
        [11001]=[],
        [11002]=[],
        [11003]=[11002],
        [11015]=[],
        [11016]=[11015],
        [11086]=[],
        [11087]=[11086],
        [11088]=[11087],
        [11089]=[11087],
        [11095]=[11086],
        [11099]=[11087],
        [11101]=[11095],
        [11102]=[11095],
        [12000]=[],
        [12001]=[],
        [12006]=[12001],
        [12010]=[12001],
        [12022]=[12001],
        [12095]=[12001],
        [12100]=[],
        [12101]=[],
        [12105]=[12101],
        [12109]=[12101],
        [13000]=[],
        [13001]=[],
        [13010]=[13001],
        [13011]=[13001],
        [13200]=[],
        [13201]=[],
        [13207]=[13201],
        [13208]=[13201],
        [14069]=[],
        [14071]=[14069],
        [14072]=[14069],
        [15002]=[],
        [15003]=[],
        [15011]=[15003],
        [15012]=[15011],
        [15018]=[15011],
        [15095]=[15003],
        [15102]=[],
        [15103]=[],
        [15108]=[15115],
        [15114]=[15115],
        [15115]=[15103],
        [16018]=[],
        [16023]=[],
        [16040]=[16023],
        [16041]=[16018],
        [16059]=[16065],
        [16065]=[],
        [16073]=[],
        [16074]=[16073],
        [16084]=[],
        [16085]=[],
        [17000]=[],
        [17017]=[17000],
        [17041]=[],
        [17049]=[],
        [17053]=[17049],
        [17066]=[],
        [18000]=[],
        [18001]=[],
        [18002]=[],
        [18003]=[18002],
        [18016]=[],
        [18019]=[18016],
        [18131]=[],
        [18132]=[18131],
        [18133]=[18132],
        [18135]=[18132],
        [18137]=[18132],
        [18142]=[18131],
        [18148]=[18142],
        [18149]=[18142],
        [19220]=[],
        [19221]=[19220],
        [19222]=[19220],
        [20000]=[],
        [20001]=[],
        [20005]=[],
        [20021]=[20005],
        [20040]=[],
        [20054]=[20040],
        [20071]=[20005],
        [30000]=[],
        [30001]=[],
        [30005]=[30001],
        [30011]=[30001],
        [30015]=[],
        [30094]=[30001],
        [30100]=[],
        [30101]=[],
        [30104]=[30101],
        [30110]=[30101],
        [30203]=[],
        [30209]=[],
        [30210]=[],
    };
    public static readonly Dictionary<int,int[]> Conflicts=new()
    {
        [2001]=[2100,2052,2071],
        [2002]=[2200],
        [2052]=[2001,2100,2071],
        [2071]=[2001,2100,2052],
        [2100]=[2001,2052,2071],
        [2200]=[2002],
        [3009]=[3036],
        [3015]=[3035],
        [3035]=[3015],
        [3036]=[3009],
        [4001]=[4070,4010],
        [4002]=[4090,4040],
        [4010]=[4070,4001],
        [4040]=[4090,4002],
        [4070]=[4010,4001],
        [4090]=[4040,4002],
        [5001]=[5010],
        [5002]=[5050],
        [5010]=[5001],
        [5050]=[5002],
        [6001]=[6100],
        [6100]=[6001],
        [6200]=[6201],
        [6201]=[6200],
        [7001]=[7003],
        [7002]=[7200],
        [7003]=[7001],
        [7008]=[7500],
        [7200]=[7002],
        [7201]=[7500],
        [8001]=[8100],
        [8100]=[8001],
        [9003]=[9027],
        [9004]=[9005],
        [9005]=[9004],
        [9027]=[9003],
        [10000]=[10002],
        [10001]=[10003],
        [10002]=[10000],
        [10003]=[10001],
        [11000]=[11002],
        [11001]=[11015],
        [11002]=[11000],
        [11015]=[11001],
        [12000]=[12001],
        [12001]=[12000],
        [12100]=[12101],
        [12101]=[12100],
        [13000]=[13001],
        [13001]=[13000],
        [13200]=[13201],
        [13201]=[13200],
        [15002]=[15003],
        [15003]=[15002],
        [15102]=[15103],
        [15103]=[15102],
        [16018]=[16084],
        [16023]=[16085,16074],
        [16074]=[16085,16023],
        [16084]=[16018],
        [16085]=[16023,16074],
        [17000]=[17041],
        [17041]=[17000],
        [17049]=[17066],
        [17066]=[17049],
        [18000]=[18002],
        [18001]=[18016],
        [18002]=[18000],
        [18016]=[18001],
        [20000]=[20005],
        [20001]=[20040],
        [20005]=[20000],
        [20040]=[20001],
        [30000]=[30001,30210,30209],
        [30001]=[30000,30210,30209],
        [30015]=[30203,30209,30210],
        [30100]=[30101,30210],
        [30101]=[30100,30210],
        [30203]=[30209,30015],
        [30209]=[30000,30001,30210,30203,30015],
        [30210]=[30000,30100,30001,30101,30209,30015],
    };
    private static int Depth(int id)=>Parents.GetValueOrDefault(id,[]).Select(p=>1+Depth(p)).DefaultIfEmpty(0).Max();
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
        // Model Wrath's implicit parent enabling and conflict disabling before any command runs.
        foreach(var id in changes.Where(p=>p.Value).Select(p=>p.Key))
            if(!HasParents(id))return null;
        foreach(var id in next)
            if(Conflicts.GetValueOrDefault(id,[]).Any(next.Contains))return null;
        return changes.OrderBy(p=>p.Value).ThenBy(p=>p.Value?Depth(p.Key):-Depth(p.Key)).ThenBy(p=>p.Key).ToDictionary(p=>p.Key,p=>p.Value);
        bool HasParents(int id)=>Parents.GetValueOrDefault(id,[]).All(p=>next.Contains(p)&&HasParents(p));
    }
}
