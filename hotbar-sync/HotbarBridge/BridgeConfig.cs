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
    public int Revision { get; set; }
    public bool Enabled { get; set; }
    // Dalamud uses Newtonsoft: replace defaults instead of appending saved pairs.
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public Dictionary<uint,Dictionary<string,Position>> JobRoutes { get; set; }=new();
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public List<SlotMap> Maps { get; set; }=Defaults.Maps();
}
