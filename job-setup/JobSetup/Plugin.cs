using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Game;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using HotbarTools;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Sheets=Lumina.Excel.Sheets;

namespace JobSetup;

public sealed class Config : IPluginConfiguration
{
    public int Version { get; set; }=1;
    public int RegularBar1 { get; set; }=1;
    public int RegularBar2 { get; set; }=2;
    public int CrossSet { get; set; }=1;
    public bool ApplyCross { get; set; }=true;
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling=Newtonsoft.Json.ObjectCreationHandling.Replace)]
    public Dictionary<string,VariantSelection> Variants { get; set; }=new();
}
public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IObjectTable objects;
    private readonly IPlayerState player;
    private readonly ICondition condition;
    private readonly IDataManager data;
    private readonly IPluginLog log;
    private readonly PresetPack pack;
    private Config config;
    private ActionCatalog? actions;
    private bool visible,all,shared;
    private bool firstOpen=true;
    private int request;
    private string message="Choose hotbars or Wrath settings, then preview. Neither is applied automatically.";
    private List<SlotEdit>? hotbarPreview;
    private JsonObject? wrathPreview;
    private string wrathOriginal="",backupInput="",previewIdentity="";
    private uint previewJob;
    private string previewScope="";
    public Plugin(IDalamudPluginInterface pi,ICommandManager commands,IFramework framework,IObjectTable objects,IPlayerState player,ICondition condition,IDataManager data,IPluginLog log)
    {
        this.pi=pi;this.commands=commands;this.framework=framework;this.objects=objects;this.player=player;this.condition=condition;this.data=data;this.log=log;
        config=pi.GetPluginConfig() as Config??new();
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("JobSetup.presets.json")!;
        pack=JsonSerializer.Deserialize<PresetPack>(stream)??throw new InvalidDataException("Missing presets");
        commands.AddHandler("/jobsetup",new CommandInfo((_,_)=>visible=true){HelpMessage="Preview/apply curated hotbar layouts or Wrath settings."});
        pi.UiBuilder.Draw+=Draw;pi.UiBuilder.OpenMainUi+=Open;framework.Update+=Update;
    }
    private void Open()=>visible=true;
    private bool WrathLoaded=>pi.InstalledPlugins.Any(p=>p.InternalName=="WrathCombo" && p.IsLoaded);
    private bool BridgeLoaded=>pi.InstalledPlugins.Any(p=>p.InternalName=="HotbarBridge" && p.IsLoaded);
    private string Root=>pi.GetPluginConfigDirectory();
    private string WrathPath=>Path.Combine(Directory.GetParent(Root)!.FullName,"WrathCombo.json");
    private string Character=>NativeHotbars.CharacterKey(player.ContentId);
    private string HotbarBackups=>Path.Combine(Root,"hotbar-backups",Character);
    private string WrathBackups=>Path.Combine(Root,"wrath-backups");
    private void RequireWrathDisabled()
    {
        WrathWritePolicy.Validate(pi.InstalledPlugins.Any(p=>p.InternalName=="WrathCombo"),WrathLoaded);
    }
    private JobPreset Current(uint id)=>pack.Jobs.FirstOrDefault(j=>j.JobId==id || j.BaseClasses.Contains(id))??throw new InvalidOperationException("No reviewed preset for this class/job. Crafting, gathering and Blue Mage are not included yet.");
    private IEnumerable<JobPreset> Baselines(uint job)=>all?pack.Jobs:[Current(job)];
    private IEnumerable<JobPreset> Selected(uint job)=>Baselines(job).Select(p=>HotbarTools.Variants.Resolve(p,config.Variants.GetValueOrDefault(p.Job)??new()));
    private void DrawVariants()
    {
        ImGui.Separator();ImGui.TextWrapped("Job variants — independent switches, reviewed per job");
        var job=objects.LocalPlayer?.ClassJob.RowId??0;
        var presets=all?pack.Jobs:pack.Jobs.Where(p=>p.JobId==job||p.BaseClasses.Contains(job));
        foreach(var preset in presets)
        {
            if(preset.AutoBurst==null && preset.AutoMitigation==null && preset.AutoMechanics==null && !preset.MechanicsAlreadyAutomatic)
            { if(!all)ImGui.TextWrapped(preset.Job+": variants not reviewed yet; current curated setup is preserved.");continue; }
            if(!config.Variants.TryGetValue(preset.Job,out var selection))config.Variants[preset.Job]=selection=new();
            ImGui.PushID(preset.Job);ImGui.TextUnformatted(preset.Job);
            Toggle("Auto burst",preset.AutoBurst,selection.AutoBurst,v=>selection.AutoBurst=v);
            Toggle("Auto mitigation",preset.AutoMitigation,selection.AutoMitigation,v=>selection.AutoMitigation=v);
            if(preset.MechanicsAlreadyAutomatic)
            {
                var mechanics=selection.AutoMechanics;
                if(ImGui.Checkbox("Auto job mechanics",ref mechanics)){selection.AutoMechanics=mechanics;ClearPreview();pi.SavePluginConfig(config);}
                ImGui.TextWrapped("No additional change for this job: "+preset.MechanicsDescription);
            }
            else Toggle("Auto job mechanics",preset.AutoMechanics,selection.AutoMechanics,v=>selection.AutoMechanics=v);
            ImGui.PopID();
        }
        ImGui.TextWrapped("Off restores the existing curated behavior. Only reviewed variants change; other jobs keep their baseline. These options select actions on your button presses, not hands-free rotation.");
        void Toggle(string label,PresetVariant? variant,bool value,Action<bool> set)
        {
            ImGui.BeginDisabled(variant==null);
            if(ImGui.Checkbox(label,ref value)){set(value);ClearPreview();pi.SavePluginConfig(config);}
            ImGui.EndDisabled();
            if(variant!=null)ImGui.TextWrapped(variant.Description);
        }
    }
    private void ClearPreview(){hotbarPreview=null;wrathPreview=null;}
    private void Draw()
    {
        if(!visible)return;
        WindowLayout.Prepare(ref firstOpen);
        if(ImGui.Begin("Job Setup",ref visible))
        {
            ImGui.TextWrapped("21 reviewed combat jobs and nine base-class aliases. No crafting/gathering presets yet. Applying is deliberate, not continuous.");
            if(ImGui.Checkbox("All supported classes/jobs (otherwise current)",ref all))ClearPreview();
            ImGui.TextWrapped(message);
            if(ImGui.CollapsingHeader("Hotbar destinations"))
            {
                var b1=config.RegularBar1;var b2=config.RegularBar2;var cross=config.CrossSet;var both=config.ApplyCross;
                var changed=ImGui.InputInt("First regular bar",ref b1);changed|=ImGui.InputInt("Second regular bar",ref b2);
                changed|=ImGui.InputInt("Cross set when Bridge is absent",ref cross);changed|=ImGui.Checkbox("Also apply cross layout",ref both);
                if(changed){config.RegularBar1=b1;config.RegularBar2=b2;config.CrossSet=cross;config.ApplyCross=both;ClearPreview();}
                if(ImGui.Button("Save destinations"))pi.SavePluginConfig(config);
                ImGui.TextWrapped("Only planned job slots are touched. Utility keys 9/0/-/= and side bars are preserved. With Bridge loaded, its applicable map supplies cross destinations. All-class hotbars also prepare saved slots for locked classes; abilities remain unusable until unlocked.");
            }
            DrawVariants();
            ImGui.BeginDisabled(WrathLoaded);
            if(ImGui.Button("Preview variant: hotbars + Wrath together"))request=6;
            ImGui.EndDisabled();
            if(ImGui.Button("Preview hotbar layouts"))request=1;
            ImGui.Separator();
            ImGui.TextWrapped($"Wrath: {(WrathLoaded?"ENABLED — writes blocked":"disabled or not installed")}. Presets reviewed against {pack.WrathVersion}.");
            if(ImGui.Button("Open Wrath in plugin installer"))pi.OpenPluginInstallerTo(Dalamud.Interface.PluginInstallerOpenKind.InstalledPlugins,"Wrath");
            ImGui.TextWrapped("Use the installer button to disable Wrath before preview/apply, then re-enable it afterward.");
            if(ImGui.Checkbox("Also apply shared targeting/role options (global, affects every job)",ref shared))ClearPreview();
            ImGui.BeginDisabled(WrathLoaded);
            if(ImGui.Button("Preview Wrath settings"))request=2;
            ImGui.EndDisabled();
            if(hotbarPreview!=null || wrathPreview!=null)
            {
                ImGui.Separator();ImGui.TextWrapped("Preview scope: "+previewScope);
                if(hotbarPreview!=null)
                {
                    ImGui.TextWrapped($"{hotbarPreview.Count} changed slots. Backup is created before writes.");
                    if(ImGui.BeginChild("changes",new System.Numerics.Vector2(0,230)))
                        foreach(var e in hotbarPreview)ImGui.TextWrapped($"Job {e.Job} {e.Position}: {e.Before} -> {e.After}");
                    ImGui.EndChild();
                }
                if(wrathPreview!=null && ImGui.CollapsingHeader("Full merged Wrath configuration preview"))
                {
                    if(ImGui.BeginChild("wrath-preview",new System.Numerics.Vector2(0,240)))ImGui.TextUnformatted(wrathPreview!.ToJsonString(JsonStore.Options));
                    ImGui.EndChild();
                }
                ImGui.BeginDisabled(wrathPreview!=null && WrathLoaded);
                if(ImGui.Button("Apply preview"))request=3;
                ImGui.EndDisabled();ImGui.SameLine();if(ImGui.Button("Cancel preview"))ClearPreview();
            }
            if(ImGui.CollapsingHeader("Backups / restore"))
            {
                ImGui.TextWrapped("Backup root: "+Root);
                ImGui.InputText("Backup JSON path",ref backupInput,1024);
                if(ImGui.Button("Preview hotbar restore"))request=4;
                ImGui.BeginDisabled(WrathLoaded);
                if(ImGui.Button("Preview Wrath restore"))request=5;
                ImGui.EndDisabled();
            }
        }
        ImGui.End();
    }
    private unsafe void Update(IFramework _)
    {
        if(request==0)return;
        var action=request;request=0;
        try
        {
            var job=objects.LocalPlayer?.ClassJob.RowId??0;
            NativeHotbars.Ready(job);
            if(condition[ConditionFlag.InCombat] || condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51])throw new InvalidOperationException("Leave combat and wait for loading to finish.");
            if(action!=3){ClearPreview();previewJob=job;previewIdentity=Character;previewScope=all?"All 21 jobs plus nine base-class layouts":"Current class/job";}
            switch(action)
            {
                case 1: PreviewHotbars(job);break;
                case 6:
                case 2:
                    RequireWrathDisabled();
                    if(action==6)PreviewHotbars(job);
                    wrathOriginal=File.ReadAllText(WrathPath);
                    wrathPreview=WrathMerge.Merge(JsonNode.Parse(wrathOriginal)!.AsObject(),Selected(job),shared?pack.SharedSettings:null);
                    previewScope+=(shared?" + global targeting/role settings":all?"; global targeting preserved":"; other jobs/global targeting preserved");
                    previewScope+=string.Join("",Baselines(job).Where(p=>p.AutoBurst!=null||p.AutoMitigation!=null).Select(p=>{
                        var v=config.Variants.GetValueOrDefault(p.Job)??new();
                        return $"; {p.Job} burst {(v.AutoBurst?"auto":"baseline")}, mitigation {(v.AutoMitigation?"auto":"baseline")}, mechanics {(v.AutoMechanics?"auto":"baseline")}{(p.MechanicsAlreadyAutomatic?" (already covered)":"")}";
                    }));
                    message=action==6?"Combined preview ready: matching hotbars and Wrath settings. Re-enable Wrath after applying.":"Wrath preview ready. Re-enable Wrath after applying; hotbars are separate in this mode.";break;
                case 3: Apply(job);break;
                case 4:
                    RequireBackup(backupInput,HotbarBackups);hotbarPreview=NativeHotbars.RestorePlan(job,backupInput);previewScope="Hotbar backup restore";break;
                case 5:
                    RequireWrathDisabled();RequireBackup(backupInput,WrathBackups);wrathOriginal=File.ReadAllText(WrathPath);wrathPreview=JsonNode.Parse(File.ReadAllText(backupInput))!.AsObject();
                    if(wrathPreview["Version"]?.GetValue<int>()!=6)throw new InvalidDataException("Invalid Wrath backup schema.");
                    previewScope="Full Wrath backup restore (all settings)";break;
            }
        }
        catch(Exception e){ClearPreview();message=e.Message;log.Error(e,"Job Setup operation failed");}
    }
    private static void RequireBackup(string path,string directory)
    {
        if(!Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Select a backup from the displayed plugin backup directory.");
    }
    private unsafe void PreviewHotbars(uint current)
    {
        if(config.RegularBar1 is <1 or >10 || config.RegularBar2 is <1 or >10 || config.RegularBar1==config.RegularBar2 || config.CrossSet is <1 or >8)throw new InvalidOperationException("Choose distinct regular bars 1–10 and cross set 1–8.");
        List<SlotMap>? bridge=null;
        if(BridgeLoaded && config.ApplyCross)
        {
            bridge=JsonSerializer.Deserialize<List<SlotMap>>(pi.GetIpcSubscriber<string>("HotbarBridge.GetMapping").InvokeFunc())??throw new InvalidOperationException("Bridge mapping unavailable.");
            Defaults.Validate(bridge);
        }
        var edits=new Dictionary<(uint,Position),SlotEdit>();
        actions ??= new ActionCatalog(data.GetExcelSheet<Sheets.Action>(ClientLanguage.English));
        foreach(var preset in Selected(current))
        {
            var jobs=all?new[]{preset.JobId}.Concat(preset.BaseClasses):new[]{current};
            foreach(var job in jobs)
            foreach(var slot in preset.Slots)
            {
                var id=actions.Resolve(preset.Job,slot.Action);
                var value=id==0?default:new SlotValue(1,id);
                var regular=new Position((slot.RegularBar==1?config.RegularBar1:config.RegularBar2)-1,slot.RegularSlot-1);
                Add(regular);
                if(config.ApplyCross)
                {
                    var destination=new Position(config.CrossSet+9,slot.CrossSlot-1);
                    if(bridge!=null)
                    {
                        var map=bridge.SingleOrDefault(m=>m.Applies(job)&&m.Regular==regular);
                        if(map==null || map.SharedAcrossJobs)throw new InvalidOperationException($"Bridge has no job-specific pair for {preset.Job} {regular}. Edit its mapping or turn off cross application.");
                        destination=map.Cross;
                    }
                    Add(destination);
                }
                void Add(Position position)
                {
                    if(!position.Valid)throw new InvalidOperationException("Invalid destination.");
                    if(NativeHotbars.Ready(current)->IsHotbarShared((uint)position.Bar))throw new InvalidOperationException($"{position}: destination is natively shared. Choose job-specific bars first.");
                    var old=NativeHotbars.Read(job,position);
                    if(old!=value)
                    {
                        var edit=new SlotEdit(job,position,old,value);
                        if(!edits.TryAdd((job,position),edit))throw new InvalidOperationException("Duplicate preset destination.");
                    }
                }
            }
        }
        hotbarPreview=edits.Values.ToList();message=$"Hotbar preview ready: {hotbarPreview.Count} changes. Actions not yet learned remain locked by the game.";
    }
    private void Apply(uint job)
    {
        if(job!=previewJob || Character!=previewIdentity)throw new InvalidOperationException("Character/job changed. Preview again.");
        if(wrathPreview==null && hotbarPreview==null)throw new InvalidOperationException("Preview first.");
        if(wrathPreview!=null)CheckWrath();
        var bridge=hotbarPreview!=null && BridgeLoaded;
        if(bridge && !pi.GetIpcSubscriber<bool>("HotbarBridge.BeginExternalEdit").InvokeFunc())throw new InvalidOperationException("Bridge is busy. Try again.");
        try
        {
            if(hotbarPreview!=null && wrathPreview!=null)
                PairedApply.Run(()=>NativeHotbars.Apply(job,hotbarPreview,HotbarBackups),WriteWrath,
                    ()=>NativeHotbars.Apply(job,hotbarPreview.Select(e=>new SlotEdit(e.Job,e.Position,e.After,e.Before)).ToList(),HotbarBackups));
            else if(wrathPreview!=null)WriteWrath();
            else NativeHotbars.Apply(job,hotbarPreview!,HotbarBackups);
            message=wrathPreview!=null?"Selected settings applied"+(hotbarPreview!=null?" with matching hotbars":"")+". Re-enable Wrath Combo. Backups saved.":"Hotbar layouts applied and verified in memory. Backup saved; test the actions in game.";
        }
        finally { if(bridge)pi.GetIpcSubscriber<bool>("HotbarBridge.EndExternalEdit").InvokeFunc(); }
        ClearPreview();
    }
    private void CheckWrath()
    {
        RequireWrathDisabled();
        if(File.ReadAllText(WrathPath)!=wrathOriginal)throw new InvalidOperationException("Wrath configuration changed since preview. Preview again.");
    }
    private void WriteWrath()
    {
        CheckWrath();
        Directory.CreateDirectory(WrathBackups);
        File.Copy(WrathPath,Path.Combine(WrathBackups,$"{DateTime.UtcNow:yyyyMMdd-HHmmss-ffff}-{Guid.NewGuid():N}.json"));
        var temporary=WrathPath+".jobsetup.tmp";
        File.WriteAllText(temporary,wrathPreview!.ToJsonString(JsonStore.Options));
        CheckWrath();
        File.Replace(temporary,WrathPath,null);
    }
    public void Dispose(){framework.Update-=Update;pi.UiBuilder.Draw-=Draw;pi.UiBuilder.OpenMainUi-=Open;commands.RemoveHandler("/jobsetup");}
}
