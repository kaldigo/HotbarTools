using Dalamud.Configuration;
using HotbarTools;

namespace JobSetup;

public sealed class ManagedSetupRecord
{
    public DateTimeOffset? FirstAppliedUtc { get; set; }
    public DateTimeOffset? LastAppliedUtc { get; set; }
    public string WrathVersion { get; set; }="";
    public int SetupRevision { get; set; }=1;
    public bool ImportedFromHistory { get; set; }
}

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
    // Applied outcomes, not per-job user choices. Only resolves indistinguishable no-op switches.
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling=Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public Dictionary<string,VariantSelection> AppliedAutomation { get; set; }=new();
    public bool ManagedHistoryMigrated { get; set; }
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling=Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public Dictionary<string,ManagedSetupRecord> ManagedJobs { get; set; }=new();
    public bool MigrateManagedHistory(IEnumerable<JobPreset> presets)
    {
        if(ManagedHistoryMigrated)return false;
        foreach(var preset in presets.Where(p=>AppliedAutomation.ContainsKey(p.Job)))
            ManagedJobs.TryAdd(preset.Job,new(){ImportedFromHistory=true});
        ManagedHistoryMigrated=true;return true;
    }
    public void RecordSetup(string job,string version,DateTimeOffset now,int revision=1)
    {
        if(!ManagedJobs.TryGetValue(job,out var record))ManagedJobs[job]=record=new(){FirstAppliedUtc=now};
        record.LastAppliedUtc=now;record.WrathVersion=version;record.SetupRevision=revision;
    }
    public List<JobPreset> SelectPresets(IEnumerable<JobPreset> presets,uint current,bool all,bool managedOnly)
    {
        var result=presets.Where(p=>all || p.JobId==current || p.BaseClasses.Contains(current))
            .Where(p=>!managedOnly || ManagedJobs.ContainsKey(p.Job)).ToList();
        if(result.Count==0)throw new InvalidOperationException(managedOnly
            ?"No registered base setup in this scope. Apply base setup in /jobsetup first."
            :"No reviewed preset for this class/job. Blue Mage is not included.");
        if(managedOnly)
        {
            var stale=result.Where(p=>ManagedJobs[p.Job].SetupRevision<p.SetupRevision).Select(p=>p.Job).ToArray();
            if(stale.Length>0)throw new InvalidOperationException("Apply updated base setup once for "+string.Join(", ",stale)+" before switching variants. Targeting and sliders are never changed by a variant switch.");
        }
        return result;
    }
}
