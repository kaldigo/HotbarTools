using System.Text.Json;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using HotbarTools;

namespace HotbarBridge;

public sealed class BridgeConfig : IPluginConfiguration
{
    public int Version { get; set; }=1;
    public int Revision { get; set; }
    public bool Enabled { get; set; }
    public List<SlotMap> Maps { get; set; }=Defaults.Maps();
}
public sealed class BridgeState
{
    public Dictionary<string,SlotValue> Baselines { get; set; }=[];
    public Dictionary<string,SlotValue> Shared { get; set; }=[];
}
public sealed class SyncController : IDisposable
{
    private readonly IDalamudPluginInterface pi;
    private readonly IObjectTable objects;
    private readonly IPlayerState player;
    private readonly ICondition condition;
    private readonly IPluginLog log;
    private BridgeConfig config;
    private BridgeState state=new();
    private string character="", context="", message="Mapped hotbars are monitored continuously while live sync is enabled.";
    private DateTime stableAfter,nextTick,observedAt;
    private string observed="";
    private bool external;
    private bool mappingDirty;
    private string liveStatus="Waiting for login";
    private int request;
    private uint previewJob;
    private string previewCharacter="";
    private List<SlotEdit>? preview;
    private string restorePath="";
    private readonly List<SlotMap> conflicts=[];
    private readonly Dalamud.Plugin.Ipc.ICallGateProvider<string> mapping;
    private readonly Dalamud.Plugin.Ipc.ICallGateProvider<bool> begin;
    private readonly Dalamud.Plugin.Ipc.ICallGateProvider<bool> end;

    public SyncController(IDalamudPluginInterface pi,IObjectTable objects,IPlayerState player,ICondition condition,IPluginLog log)
    {
        this.pi=pi;this.objects=objects;this.player=player;this.condition=condition;this.log=log;
        config=pi.GetPluginConfig() as BridgeConfig ?? new();
        Defaults.Validate(config.Maps);
        mapping=pi.GetIpcProvider<string>("HotbarBridge.GetMapping");
        mapping.RegisterFunc(()=>JsonSerializer.Serialize(config.Maps));
        begin=pi.GetIpcProvider<bool>("HotbarBridge.BeginExternalEdit");
        end=pi.GetIpcProvider<bool>("HotbarBridge.EndExternalEdit");
        begin.RegisterFunc(()=>{if(external)return false;external=true;preview=null;return true;});
        end.RegisterFunc(()=>{external=false;context="";return true;});
    }
    private string StatePath=>Path.Combine(pi.GetPluginConfigDirectory(),"state",character+".json");
    private string BackupPath=>Path.Combine(pi.GetPluginConfigDirectory(),"backups",character);
    private string Key(SlotMap map,uint job)=>$"{config.Revision}:{job}:{map.Id}";
    private string SharedKey(SlotMap map)=>$"{config.Revision}:{map.Id}";
    private void Save()=>pi.SavePluginConfig(config);
    public void Tick()
    {
        try { TickCore(); }
        catch(Exception e) { config.Enabled=false;Save();preview=null;message="Paused: "+e.Message;log.Error(e,"Hotbar sync paused"); }
    }
    private unsafe void TickCore()
    {
        if(external){liveStatus="Waiting for Job Setup to finish";return;}
        var current=objects.LocalPlayer;
        if(current==null || condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51]) { context="";preview=null;liveStatus="Waiting for login/loading";return; }
        var job=current.ClassJob.RowId;
        var m=FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureHotbarModule.Instance();
        if(m==null || !m->ModuleReady || m->PvPHotbarsActive || m->ActiveHotbarClassJobId!=job) {context="";preview=null;liveStatus="Waiting for ready PvE hotbars";return;}
        var identity=NativeHotbars.CharacterKey(player.ContentId);
        var ctx=$"{identity}:{job}:{config.Revision}";
        if(ctx!=context)
        {
            if(identity!=character)
            {
                character=identity;
                state=File.Exists(StatePath)?JsonSerializer.Deserialize<BridgeState>(File.ReadAllText(StatePath))??new():new();
            }
            context=ctx;stableAfter=DateTime.UtcNow.AddSeconds(2);observed="";preview=null;conflicts.Clear();liveStatus="Waiting for job change to settle";return;
        }
        if(DateTime.UtcNow<stableAfter){liveStatus="Waiting for job change to settle";return;}
        if(condition[ConditionFlag.InCombat]){liveStatus="Temporarily paused during combat; resumes automatically";return;}
        liveStatus="ON - watching edits in both directions, even with this window closed";
        if(request!=0)
        {
            var action=request;request=0;
            Defaults.Validate(config.Maps);
            if(mappingDirty)throw new InvalidOperationException("Save your mapping changes before enabling live sync.");
            if(action==6)
            {
                if(LiveSyncStart.Decide(mappingDirty,state.Baselines.Keys,config.Revision)==LiveStartChoice.Resume)
                {
                    config.Enabled=true;preview=null;observed="";Save();
                    message="Live sync resumed. Previous baselines are preserved; no new initialization is needed.";
                    return;
                }
                action=1;
                message="First use: review the initial regular-to-cross alignment below, then start live sync.";
            }
            if(action==1 || action==4 || action==5)
            {
                var edits=new List<SlotEdit>();
                foreach(var map in config.Maps.Where(x=>x.Applies(job)))
                {
                    ValidateSharing(map,job);
                    var r=NativeHotbars.Read(job,map.Regular);var c=NativeHotbars.Read(job,map.Cross);
                    if(action!=1 && !conflicts.Contains(map))continue;
                    var desired=action==5?c:r;
                    if(r!=desired)edits.Add(new(job,map.Regular,r,desired));
                    if(c!=desired)edits.Add(new(job,map.Cross,c,desired));
                }
                preview=edits;previewJob=job;previewCharacter=character;message=$"Initial alignment: {edits.Count} changes. Confirm once below; subsequent edits sync automatically in either direction.";
            }
            if(action==2)
            {
                if(preview==null || previewJob!=job || previewCharacter!=character)throw new InvalidOperationException("Preview the current character/job first.");
                NativeHotbars.Apply(job,preview,BackupPath);preview=null;
                foreach(var map in config.Maps.Where(x=>x.Applies(job)))
                {
                    var r=NativeHotbars.Read(job,map.Regular);var c=NativeHotbars.Read(job,map.Cross);
                    if(r!=c)continue;
                    state.Baselines[Key(map,job)]=r;
                    if(map.SharedAcrossJobs)state.Shared[SharedKey(map)]=r;
                }
                JsonStore.Write(StatePath,state);config.Enabled=true;Save();conflicts.Clear();message="Live sync is ON. Close this window and edit either mapped hotbar; the other updates automatically after about half a second, outside combat.";
            }
            if(action==3)
            {
                if(!File.Exists(restorePath))throw new InvalidOperationException("Choose an existing backup file.");
                if(!Path.GetFullPath(restorePath).StartsWith(Path.GetFullPath(BackupPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Select a backup for this character.");
                preview=NativeHotbars.RestorePlan(job,restorePath);previewJob=job;previewCharacter=character;message=$"Restore preview: {preview.Count} slots. Apply creates a new backup first.";
            }
            return;
        }
        if(!config.Enabled || preview!=null || DateTime.UtcNow<nextTick)return;
        nextTick=DateTime.UtcNow.AddMilliseconds(200);
        var active=config.Maps.Where(x=>x.Applies(job)).ToList();
        var fingerprint=string.Join(";",active.Select(x=>$"{x.Id}:{NativeHotbars.Read(job,x.Regular)}:{NativeHotbars.Read(job,x.Cross)}"));
        if(fingerprint!=observed) { observed=fingerprint;observedAt=DateTime.UtcNow;return; }
        if(DateTime.UtcNow-observedAt<TimeSpan.FromMilliseconds(400))return;
        var changes=new List<SlotEdit>();
        var updates=new List<(SlotMap,SlotValue)>();conflicts.Clear();
        foreach(var map in active)
        {
            ValidateSharing(map,job);
            var r=NativeHotbars.Read(job,map.Regular);var c=NativeHotbars.Read(job,map.Cross);
            SlotValue desired;
            if(!state.Baselines.TryGetValue(Key(map,job),out var previous))
                desired=map.SharedAcrossJobs && state.Shared.TryGetValue(SharedKey(map),out var stored)?stored:r;
            else if(map.SharedAcrossJobs && state.Shared.TryGetValue(SharedKey(map),out var canonical) && canonical!=previous)
                desired=canonical; // Another job updated the shared value while this job was inactive.
            else
            {
                var decision=SyncRules.Decide(r,c,previous);
                if(decision==SyncChoice.Conflict) { conflicts.Add(map);continue; }
                desired=decision==SyncChoice.ToRegular?c:r;
            }
            if(r!=desired)changes.Add(new(job,map.Regular,r,desired));
            if(c!=desired)changes.Add(new(job,map.Cross,c,desired));
            updates.Add((map,desired));
        }
        if(conflicts.Count>0) { message=$"{conflicts.Count} conflicting pairs. Review and choose a source below; sync is waiting.";return; }
        if(changes.Count>0)NativeHotbars.Apply(job,changes,BackupPath);
        var dirty=false;
        foreach(var (map,value) in updates)
        {
            var key=Key(map,job);
            if(!state.Baselines.TryGetValue(key,out var old)||old!=value){state.Baselines[key]=value;dirty=true;}
            if(map.SharedAcrossJobs && (!state.Shared.TryGetValue(SharedKey(map),out old)||old!=value)){state.Shared[SharedKey(map)]=value;dirty=true;}
        }
        if(dirty)JsonStore.Write(StatePath,state);
        if(changes.Count>0)message=$"Synced {changes.Count} slot changes; backup saved.";
    }
    private static unsafe void ValidateSharing(SlotMap map,uint job)
    {
        var m=NativeHotbars.Ready(job);
        if(!map.SharedAcrossJobs && (m->IsHotbarShared((uint)map.Regular.Bar)||m->IsHotbarShared((uint)map.Cross.Bar)))
            throw new InvalidOperationException($"{map.Label}: job-specific pair targets a natively shared bar. Adjust sharing or the map first.");
    }
    public void Draw()
    {
        ImGui.TextWrapped("Live two-way sync mirrors changes to mapped slots automatically. The window does not need to stay open.");
        var running=config.Enabled;
        ImGui.BeginDisabled(mappingDirty || preview!=null);
        if(ImGui.Checkbox("Enable live two-way sync",ref running))
        {
            if(running)request=6;
            else {config.Enabled=false;Save();message="Live sync is OFF. Turn the switch back on to resume.";}
        }
        ImGui.EndDisabled();
        var status=!config.Enabled
            ? mappingDirty ? "OFF - save mapping changes first" : preview!=null ? "OFF - confirm initial alignment below to start" : "OFF"
            : conflicts.Count>0 ? "Waiting for conflict resolution" : liveStatus;
        ImGui.TextWrapped("Live sync status: "+status);
        ImGui.TextWrapped(message);
        ImGui.TextWrapped("First activation previews the regular-to-cross alignment. After that, the switch resumes continuous sync without another alignment. Combat/loading pauses resume automatically.");
        if(ImGui.CollapsingHeader("Reinitialize from regular hotbars"))
            if(ImGui.Button("Preview a fresh regular -> cross alignment")){config.Enabled=false;Save();request=1;}
        if(preview!=null)
        {
            ImGui.TextWrapped($"{preview.Count} pending changes for job {previewJob}. No changes yet.");
            if(ImGui.BeginChild("sync-preview",new System.Numerics.Vector2(0,200)))
                foreach(var e in preview)ImGui.TextWrapped($"Job {e.Job} {e.Position}: {e.Before} -> {e.After}");
            ImGui.EndChild();
            if(ImGui.Button("Apply alignment and start LIVE sync"))request=2;
            ImGui.SameLine();if(ImGui.Button("Cancel preview"))preview=null;
        }
        if(conflicts.Count>0)
        {
            foreach(var c in conflicts)ImGui.TextWrapped($"Conflict: {c.Label} ({c.Regular} <-> {c.Cross})");
            if(ImGui.Button("Preview conflicts: regular wins"))request=4;
            if(ImGui.Button("Preview conflicts: cross wins"))request=5;
        }
        if(ImGui.CollapsingHeader("Slot mapping editor"))
        {
            ImGui.TextWrapped("Numbers are 1-based. Cross slots: 1 LT Down, 2 LT Left, 3 LT Right, 4 LT Up, 5 LT X, 6 LT Y, 7 LT B, 8 LT A; 9-16 repeat for RT. Mapping edits pause sync until saved and previewed.");
            var changed=false;int remove=-1;
            if(ImGui.BeginChild("mapping-list",new System.Numerics.Vector2(0,300)))
            for(var i=0;i<config.Maps.Count;i++)
            {
                var map=config.Maps[i];ImGui.PushID(i);
                if(ImGui.TreeNode($"{map.Label}: {map.Regular} <-> {map.Cross}"))
                {
                    var enabled=map.Enabled;if(ImGui.Checkbox("Enabled",ref enabled)){map.Enabled=enabled;changed=true;}
                    var label=map.Label;if(ImGui.InputText("Label",ref label,100)){map.Label=label;changed=true;}
                    var rb=map.RegularBar;var rs=map.RegularSlot;var cb=map.CrossSet;var cs=map.CrossSlot;
                    changed|=ImGui.InputInt("Regular bar",ref rb);changed|=ImGui.InputInt("Regular slot",ref rs);
                    changed|=ImGui.InputInt("Cross set",ref cb);changed|=ImGui.InputInt("Cross slot",ref cs);
                    map.RegularBar=rb;map.RegularSlot=rs;map.CrossSet=cb;map.CrossSlot=cs;
                    var profile=Array.IndexOf(new[]{"All","Combat","Healer"},map.Profile);
                    if(ImGui.Combo("Profile",ref profile,new[]{"All","Combat","Healer"},3)){map.Profile=new[]{"All","Combat","Healer"}[profile];changed=true;}
                    var shared=map.SharedAcrossJobs;if(ImGui.Checkbox("Share these assignments across jobs",ref shared)){map.SharedAcrossJobs=shared;changed=true;}
                    if(ImGui.Button("Remove pair"))remove=i;
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }
            ImGui.EndChild();
            if(remove>=0){config.Maps.RemoveAt(remove);changed=true;}
            if(ImGui.Button("Add pair")){config.Maps.Add(new SlotMap{Enabled=false});changed=true;}
            if(changed){config.Enabled=false;preview=null;mappingDirty=true;message="Mapping edited: save it before restarting live sync.";}
            if(ImGui.Button("Validate and save mapping")) Try(()=>{Defaults.Validate(config.Maps);config.Revision++;config.Enabled=false;mappingDirty=false;Save();context="";preview=null;message="Mapping saved. Turn on live sync to review the new alignment.";});
            if(ImGui.Button("Reset map to shipped defaults (sync stays off)")){config.Maps=Defaults.Maps();config.Revision++;config.Enabled=false;mappingDirty=false;Save();context="";preview=null;}
            if(ImGui.Button("Copy mapping JSON"))ImGui.SetClipboardText(JsonSerializer.Serialize(config.Maps,JsonStore.Options));
            if(ImGui.Button("Import mapping JSON from clipboard"))Try(()=>{var maps=JsonSerializer.Deserialize<List<SlotMap>>(ImGui.GetClipboardText())??throw new InvalidDataException("Invalid JSON");Defaults.Validate(maps);config.Maps=maps;config.Enabled=false;config.Revision++;mappingDirty=false;Save();context="";preview=null;});
        }
        if(ImGui.CollapsingHeader("Restore a hotbar backup"))
        {
            ImGui.TextWrapped("Backups for this character: "+(character==""?"Log in first":BackupPath));
            ImGui.InputText("Backup JSON path",ref restorePath,1024);
            if(ImGui.Button("Preview backup restore")){config.Enabled=false;Save();request=3;}
        }
    }
    private void Try(Action action){try{action();}catch(Exception e){message=e.Message;}}
    public void Dispose(){mapping.UnregisterFunc();begin.UnregisterFunc();end.UnregisterFunc();}
}
