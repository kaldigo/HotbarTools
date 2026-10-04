using System.Text.Json;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using HotbarTools;

namespace HotbarBridge;

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
    private bool utilityCleanupAll,utilityCleanupPreview;
    private string cleanupMapState="";
    private List<SlotEdit> cleanupChecks=[];
    private unsafe string CleanupMapState(uint job)
    {
        var m=NativeHotbars.Ready(job);var sharing="";
        for(var bar=0;bar<18;bar++)sharing+=m->IsHotbarShared((uint)bar)?"1":"0";
        return sharing+JsonSerializer.Serialize(config.Maps)+JsonSerializer.Serialize(config.JobRoutes);
    }
    private readonly List<SlotMap> conflicts=[];
    private readonly Dalamud.Plugin.Ipc.ICallGateProvider<string> mapping;
    private readonly Dalamud.Plugin.Ipc.ICallGateProvider<bool> begin;
    private readonly Dalamud.Plugin.Ipc.ICallGateProvider<bool> end;
    private readonly Dalamud.Plugin.Ipc.ICallGateProvider<string> getRoutes;
    private readonly Dalamud.Plugin.Ipc.ICallGateProvider<string,bool> setRoutes;

    public SyncController(IDalamudPluginInterface pi,IObjectTable objects,IPlayerState player,ICondition condition,IPluginLog log)
    {
        this.pi=pi;this.objects=objects;this.player=player;this.condition=condition;this.log=log;
        config=pi.GetPluginConfig() as BridgeConfig ?? new();
        var changed=config.AddNoncombatDefaults();
        changed|=config.MigrateUtilityCrossbar(out var migrationNotice);
        if(changed)pi.SavePluginConfig(config);
        if(migrationNotice!=null)message=migrationNotice;
        Defaults.Validate(config.Maps);
        mapping=pi.GetIpcProvider<string>("HotbarBridge.GetMapping");
        mapping.RegisterFunc(()=>JsonSerializer.Serialize(config.Maps));
        begin=pi.GetIpcProvider<bool>("HotbarBridge.BeginExternalEdit");
        end=pi.GetIpcProvider<bool>("HotbarBridge.EndExternalEdit");
        begin.RegisterFunc(()=>{if(external || mappingDirty)return false;external=true;preview=null;return true;});
        end.RegisterFunc(()=>{external=false;context="";return true;});
        getRoutes=pi.GetIpcProvider<string>("HotbarBridge.GetJobRoutes");
        getRoutes.RegisterFunc(()=>JsonSerializer.Serialize(config.JobRoutes));
        setRoutes=pi.GetIpcProvider<string,bool>("HotbarBridge.SetJobRoutes");
        setRoutes.RegisterFunc(json=>{
            if(!external || mappingDirty)throw new InvalidOperationException("Begin external edit before updating job routes.");
            var updates=JsonSerializer.Deserialize<Dictionary<uint,Dictionary<string,Position>>>(json)??throw new InvalidDataException("Missing routes.");
            foreach(var (job,routes) in updates)
            {
                if(job is 0 or >42)throw new InvalidDataException("Invalid route job.");
                var active=config.Maps.Where(m=>m.Applies(job)).ToList();
                if(routes.Keys.Any(id=>!active.Any(m=>m.Id==id && !m.SharedAcrossJobs)))throw new InvalidDataException("Unknown or shared route.");
                LayoutRoutes.Effective(active,routes);
            }
            var old=JsonSerializer.Serialize(config.JobRoutes);
            try {foreach(var (job,routes) in updates)config.JobRoutes[job]=routes;Save();}
            catch {config.JobRoutes=JsonSerializer.Deserialize<Dictionary<uint,Dictionary<string,Position>>>(old)!;throw;}
            foreach(var job in updates.Keys)
                foreach(var key in state.Baselines.Keys.Where(k=>k.StartsWith($"{config.Revision}:{job}:") && !config.Maps.Any(m=>m.SharedAcrossJobs && k==Key(m,job))).ToArray())state.Baselines.Remove(key);
            context="";return true;
        });
    }
    private List<SlotMap> ActiveMaps(uint job)=>LayoutRoutes.Effective(config.Maps.Where(m=>m.Applies(job)),config.JobRoutes.GetValueOrDefault(job));
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
            if(action==7)
            {
                if(!config.LegacyUtilityCleanupAvailable)throw new InvalidOperationException("No migrated utility page to clean up.");
                cleanupChecks=[];
                SlotValue ReadChecked(uint target,Position p)
                {
                    var value=NativeHotbars.Read(target,p);
                    cleanupChecks.Add(new(target,p,value,value));return value;
                }
                preview=UtilityCleanup.Plan(job,utilityCleanupAll?Enumerable.Range(1,42).Select(i=>(uint)i):new[]{job},config.Maps,
                    m->IsHotbarShared(11),ReadChecked);
                previewJob=job;previewCharacter=character;utilityCleanupPreview=true;cleanupMapState=CleanupMapState(job);
                message=$"Old utility page cleanup: {preview.Count} confirmed duplicates on cross hotbar 2. Other assignments are preserved. Shared cross hotbar 2 changes affect all jobs.";
            }
            if(action==8)
            {
                if(!utilityCleanupPreview || preview==null || previewJob!=job || previewCharacter!=character)
                    throw new InvalidOperationException("Preview utility cleanup for this character/job first.");
                if(cleanupMapState!=CleanupMapState(job) || cleanupChecks.Any(e=>NativeHotbars.Read(e.Job,e.Position)!=e.Before))
                    throw new InvalidOperationException("Utility assignments, mapping or sharing changed. Preview cleanup again.");
                var count=preview.Count;
                if(count>0)NativeHotbars.Apply(job,preview,BackupPath);
                preview=null;utilityCleanupPreview=false;cleanupChecks=[];observed="";
                message=$"Cleared {count} old utility duplicates. {(count>0?"Backup saved. ":"")}Live sync remains {(config.Enabled?"on":"off")}.";
            }
            if(action==1 || action==4 || action==5)
            {
                utilityCleanupPreview=false;
                var edits=new List<SlotEdit>();
                foreach(var map in ActiveMaps(job))
                {
                    ValidateSharing(map,job);
                    var r=NativeHotbars.Read(job,map.Regular);var c=NativeHotbars.Read(job,map.Cross);
                    if(action!=1 && !conflicts.Any(c=>c.Id==map.Id))continue;
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
                foreach(var map in ActiveMaps(job))
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
                utilityCleanupPreview=false;
                if(!File.Exists(restorePath))throw new InvalidOperationException("Choose an existing backup file.");
                if(!Path.GetFullPath(restorePath).StartsWith(Path.GetFullPath(BackupPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Select a backup for this character.");
                preview=NativeHotbars.RestorePlan(job,restorePath);previewJob=job;previewCharacter=character;message=$"Restore preview: {preview.Count} slots. Apply creates a new backup first.";
            }
            return;
        }
        if(!config.Enabled || preview!=null || DateTime.UtcNow<nextTick)return;
        nextTick=DateTime.UtcNow.AddMilliseconds(200);
        var active=ActiveMaps(job).ToList();
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
        if(config.LegacyUtilityCleanupAvailable && ImGui.CollapsingHeader("Clean up old utility page"))
        {
            ImGui.TextWrapped("After aligning cross hotbar 8, remove matching utility duplicates from cross hotbar 2. Changed slots and mapped slots are preserved. Preview and backup required; a game-shared cross hotbar affects every job.");
            if(ImGui.Checkbox("Clean all classes/jobs (otherwise current)",ref utilityCleanupAll) && utilityCleanupPreview)preview=null;
            ImGui.BeginDisabled(mappingDirty || external);
            if(ImGui.Button("Preview old utility page cleanup"))request=7;
            ImGui.EndDisabled();
        }
        if(preview!=null)
        {
            ImGui.TextWrapped($"{preview.Count} pending changes for job {previewJob}. No changes yet.");
            if(ImGui.BeginChild("sync-preview",new System.Numerics.Vector2(0,200)))
                foreach(var e in preview)ImGui.TextWrapped($"Job {e.Job} {e.Position}: {e.Before} -> {e.After}");
            ImGui.EndChild();
            if(ImGui.Button(utilityCleanupPreview?"Clear previewed old utility slots":"Apply alignment and start LIVE sync"))request=utilityCleanupPreview?8:2;
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
            ImGui.TextWrapped($"{config.JobRoutes.Count} per-job keyboard layouts supplied by Job Setup. Apply a layout with compaction off to restore its spacing. The editor below configures the base map.");
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
                    var profile=Array.IndexOf(new[]{"All","Combat","Healer","Noncombat"},map.Profile);
                    if(ImGui.Combo("Profile",ref profile,new[]{"All","Combat","Healer","Noncombat"},4)){map.Profile=new[]{"All","Combat","Healer","Noncombat"}[profile];changed=true;}
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
            if(ImGui.Button("Validate and save mapping")) Try(()=>{Defaults.Validate(config.Maps);foreach(var job in config.JobRoutes.Keys)ActiveMaps(job);config.Revision++;config.Enabled=false;mappingDirty=false;Save();context="";preview=null;message="Mapping saved. Turn on live sync to review the new alignment.";});
            if(ImGui.Button("Reset map to shipped defaults (sync stays off)")){config.Maps=Defaults.Maps();config.JobRoutes.Clear();config.Revision++;config.Enabled=false;mappingDirty=false;Save();context="";preview=null;}
            if(ImGui.Button("Copy mapping JSON"))ImGui.SetClipboardText(JsonSerializer.Serialize(config.Maps,JsonStore.Options));
            if(ImGui.Button("Import mapping JSON from clipboard"))Try(()=>{var maps=JsonSerializer.Deserialize<List<SlotMap>>(ImGui.GetClipboardText())??throw new InvalidDataException("Invalid JSON");Defaults.Validate(maps);foreach(var (job,routes) in config.JobRoutes)LayoutRoutes.Effective(maps.Where(m=>m.Applies(job)),routes);config.Maps=maps;config.Enabled=false;config.Revision++;mappingDirty=false;Save();context="";preview=null;});
        }
        if(ImGui.CollapsingHeader("Restore a hotbar backup"))
        {
            ImGui.TextWrapped("Backups for this character: "+(character==""?"Log in first":BackupPath));
            ImGui.InputText("Backup JSON path",ref restorePath,1024);
            if(ImGui.Button("Preview backup restore")){config.Enabled=false;Save();request=3;}
        }
    }
    private void Try(Action action){try{action();}catch(Exception e){message=e.Message;}}
    public void Dispose(){mapping.UnregisterFunc();begin.UnregisterFunc();end.UnregisterFunc();getRoutes.UnregisterFunc();setRoutes.UnregisterFunc();}
}
