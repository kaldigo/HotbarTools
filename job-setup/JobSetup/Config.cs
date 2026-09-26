using Dalamud.Configuration;
using HotbarTools;

namespace JobSetup;

public sealed class Config : IPluginConfiguration
{
    public int Version { get; set; }=1;
    public int RegularBar1 { get; set; }=1;
    public int RegularBar2 { get; set; }=2;
    public int CrossSet { get; set; }=1;
    public bool ApplyCross { get; set; }=true;
    public bool CompactKeyboard { get; set; }=true;
    public bool AutoManageWrath { get; set; }
    public bool PreferLiveWrath { get; set; }=true;
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling=Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public VariantSelection Automation { get; set; }=new();
}
