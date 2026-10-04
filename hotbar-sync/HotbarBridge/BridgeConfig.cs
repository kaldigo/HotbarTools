using Dalamud.Configuration;
using HotbarTools;

namespace HotbarBridge;

public sealed class BridgeConfig : IPluginConfiguration
{
    public int Version { get; set; }=1;
    public bool NoncombatMapsReviewed { get; set; }
    public bool AddNoncombatDefaults()
    {
        if(NoncombatMapsReviewed)return false;
        if(Maps.Count==0){NoncombatMapsReviewed=true;return true;}
        // Only add disjoint pairs; custom All-profile assignments take precedence.
        foreach(var map in Defaults.Maps().Where(m=>m.Profile=="Noncombat"))
        {
            if(Maps.Any(m=>m.Id==map.Id))continue;
            var proposed=Maps.Append(map).ToList();
            try { Defaults.Validate(proposed);Maps=proposed; }
            catch(InvalidDataException) { }
        }
        NoncombatMapsReviewed=true;return true;
    }
    public bool LegacyUtilityCleanupAvailable { get; set; }
    public bool UtilityCrossbarReviewed { get; set; }
    public bool MigrateUtilityCrossbar(out string? notice)
    {
        notice=null;
        if(UtilityCrossbarReviewed)return false;
        UtilityCrossbarReviewed=true;
        var defaults=Defaults.Maps().Where(m=>m.Id.StartsWith("utility-")).ToList();
        // Migrate the complete old default group only; partial/custom mappings are intentional.
        if(!defaults.All(d=>Maps.Any(m=>m.Id==d.Id && m.Regular==d.Regular &&
            m.CrossSet==2 && m.CrossSlot==d.CrossSlot && m.Profile=="All" && m.SharedAcrossJobs && m.Enabled)))
            return true;
        var proposed=System.Text.Json.JsonSerializer.Deserialize<List<SlotMap>>(System.Text.Json.JsonSerializer.Serialize(Maps))!;
        foreach(var map in proposed.Where(m=>defaults.Any(d=>d.Id==m.Id)))map.CrossSet=8;
        try
        {
            Defaults.Validate(proposed);
            foreach(var (job,routes) in JobRoutes)LayoutRoutes.Effective(proposed.Where(m=>m.Applies(job)),routes);
        }
        catch(InvalidDataException)
        {
            notice="Utility mapping left unchanged: cross hotbar 8 conflicts with a custom mapping. Review the slot mapping editor.";
            return true;
        }
        Maps=proposed;Revision++;Enabled=false;LegacyUtilityCleanupAvailable=true;
        notice="Utility defaults moved to cross hotbar 8. Enable live sync to preview regular-to-cross alignment before applying. Existing cross hotbar 2 contents are unchanged.";
        return true;
    }
    public int Revision { get; set; }
    public bool Enabled { get; set; }
    // Dalamud uses Newtonsoft: replace defaults instead of appending saved pairs.
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public Dictionary<uint,Dictionary<string,Position>> JobRoutes { get; set; }=new();
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public List<SlotMap> Maps { get; set; }=Defaults.Maps();
}
