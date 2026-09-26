using System.Text.Json.Nodes;
namespace HotbarTools;

/// <summary>Change only reviewed variant feature IDs. Initial setup owns all detailed configuration.</summary>
public static class VariantSwitch
{
    public static JsonObject Merge(JsonObject original,IEnumerable<JobPreset> baselines,VariantSelection selection)
    {
        if(original["Version"]?.GetValue<int>()!=6 || original["EnabledActionsV6"] is not JsonArray array)
            throw new InvalidDataException("Expected Wrath configuration version 6.");
        var result=(JsonObject)original.DeepClone();
        var enabled=array.Select(n=>n!.GetValue<int>()).ToHashSet();
        foreach(var baseline in baselines)
        {
            var variants=new[]{baseline.AutoBurst,baseline.AutoMitigation,baseline.AutoMechanics}.OfType<PresetVariant>().ToArray();
            if(variants.Any(v=>v.Settings.Count!=0))throw new InvalidDataException("Variant options must be configured during initial setup.");
            var controlled=variants.SelectMany(v=>v.Enable.Concat(v.Disable)).ToHashSet();
            var target=Variants.Resolve(baseline,selection).Settings["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
            enabled.ExceptWith(controlled);enabled.UnionWith(target.Intersect(controlled));
        }
        result["EnabledActionsV6"]=new JsonArray(enabled.Order().Select(n=>(JsonNode?)JsonValue.Create(n)).ToArray());
        return result;
    }
}
