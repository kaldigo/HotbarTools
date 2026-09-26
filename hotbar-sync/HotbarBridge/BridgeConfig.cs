using Dalamud.Configuration;
using HotbarTools;

namespace HotbarBridge;

public sealed class BridgeConfig : IPluginConfiguration
{
    public int Version { get; set; }=1;
    public int Revision { get; set; }
    public bool Enabled { get; set; }
    // Dalamud uses Newtonsoft: replace defaults instead of appending saved pairs.
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public Dictionary<uint,Dictionary<string,Position>> JobRoutes { get; set; }=new();
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public List<SlotMap> Maps { get; set; }=Defaults.Maps();
}
