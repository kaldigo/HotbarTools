using System.Text.Json.Nodes;

namespace HotbarTools;

/// <summary>Infer a job's enabled variants from Wrath, retaining applied history only for indistinguishable no-op choices.</summary>
public static class AutomationStatus
{
    public static VariantSelection? Infer(JobPreset preset,JsonObject actual,VariantSelection? lastApplied)
    {
        if(actual["Version"]?.GetValue<int>()!=6 || actual["EnabledActionsV6"] is not JsonArray array)return null;
        var enabled=array.Select(n=>n!.GetValue<int>()).ToHashSet();
        var variants=new[]{preset.AutoBurst,preset.AutoMitigation,preset.AutoMechanics}.OfType<PresetVariant>().ToArray();
        var ids=variants.SelectMany(v=>v.Enable.Concat(v.Disable)).Distinct().ToArray();
        var keys=variants.SelectMany(v=>v.Settings.SelectMany(section=>section.Value is JsonObject obj
            ?obj.Select(field=>(section.Key,Field:(string?)field.Key))
            :new[]{(section.Key,Field:(string?)null)})).Distinct().ToArray();
        var candidates=new List<VariantSelection>();
        for(var mask=0;mask<8;mask++)
        {
            var choice=new VariantSelection{AutoBurst=(mask&1)!=0,AutoMitigation=(mask&2)!=0,AutoMechanics=(mask&4)!=0};
            var desired=Variants.Resolve(preset,choice).Settings;
            var wanted=desired["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
            if(ids.Any(id=>enabled.Contains(id)!=wanted.Contains(id)))continue;
            if(keys.Any(k=>!JsonNode.DeepEquals(k.Field==null?actual[k.Key]:actual[k.Key]?[k.Field],k.Field==null?desired[k.Key]:desired[k.Key]?[k.Field])))continue;
            candidates.Add(choice);
        }
        var prior=lastApplied??new();
        return candidates.OrderBy(v=>(v.AutoBurst!=prior.AutoBurst?1:0)+(v.AutoMitigation!=prior.AutoMitigation?1:0)+(v.AutoMechanics!=prior.AutoMechanics?1:0)).FirstOrDefault();
    }
}
