using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Game;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using HotbarTools;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Sheets=Lumina.Excel.Sheets;

namespace JobSetup;

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
    private readonly IChatGui chat;
    private readonly IDtrBarEntry status;
    private string statusText="";
    private uint statusJob;
    private ulong statusCharacter;
    private DateTime nextStatusCheck;
    private VariantSelection? activeAutomation;
    private bool recordSelection;
    private MacroOperation? macro;
    private sealed record MacroOperation(VariantSelection Previous,bool All,bool Shared,uint Job,ulong Character);
    private Config config;
    private ActionCatalog? actions;
    private bool visible,all,shared;
    private bool firstOpen=true;
    private int request;
    private WrathLifecycle? wrathCycle;
    private Task? lifecycleTask;
    private bool restoringWrath;
    private int actionAfterUnload;
    private string operationMessage="";
    private string message="Choose hotbars or Wrath settings, then preview. Neither is applied automatically.";
    private List<SlotEdit>? hotbarPreview;
    private JsonObject? wrathPreview;
    private Dictionary<uint,Dictionary<string,Position>>? routePreview,routeOriginal;
    private string routesAtPreview="",mappingAtPreview="";
    private readonly List<string> layoutSummary=[];
    private string wrathOriginal="",backupInput="",previewIdentity="";
    private uint previewJob;
    private string previewScope="";
    public Plugin(IDalamudPluginInterface pi,ICommandManager commands,IFramework framework,IObjectTable objects,IPlayerState player,ICondition condition,IDataManager data,IPluginLog log,IDtrBar dtr,IChatGui chat)
    {
        this.pi=pi;this.commands=commands;this.framework=framework;this.objects=objects;this.player=player;this.condition=condition;this.data=data;this.log=log;
        config=pi.GetPluginConfig() as Config??new();
        this.chat=chat;
        status=dtr.Get("Job Settings");status.Shown=false;
        status.Tooltip=new SeStringBuilder().AddIcon(BitmapFontIcon.Tank).AddText(" Mitigation (M)  ")
            .AddIcon(BitmapFontIcon.SwordUnsheathed).AddText(" Burst (B)  ")
            .AddIcon(BitmapFontIcon.AnyClass).AddText(" Job mechanics (J)\nChecked against the current job�s Wrath settings. Click to open Job Setup.").Build();
        status.OnClick=_=>Open();
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("JobSetup.presets.json")!;
        pack=JsonSerializer.Deserialize<PresetPack>(stream)??throw new InvalidDataException("Missing presets");
        commands.AddHandler("/jobsetup",new CommandInfo((_,args)=>Command(args)){HelpMessage=AutomationCommands.Help});
        pi.UiBuilder.Draw+=Draw;pi.UiBuilder.OpenMainUi+=Open;framework.Update+=Update;
    }
    private void Command(string args)
    {
        if(string.IsNullOrWhiteSpace(args)){Open();return;}
        if(args.Trim().Equals("help",StringComparison.OrdinalIgnoreCase)){chat.Print(AutomationCommands.Help,"Job Setup");return;}
        try
        {
            if(macro!=null || lifecycleTask!=null || request!=0)throw new InvalidOperationException("Job Setup is busy. Wait for the current operation to finish.");
            var words=args.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            var basis=config.Automation;
            if(words.Length==3 && words[0].Equals("current",StringComparison.OrdinalIgnoreCase) && new[]{"on","off","toggle"}.Contains(words[1],StringComparer.OrdinalIgnoreCase))
                basis=ReadActiveAutomation()??throw new InvalidOperationException("Current job settings do not match a known variant. Apply an exact selection first, such as /jobsetup current MBJ.");
            var parsed=AutomationCommands.Parse(args,basis);
            var job=objects.LocalPlayer?.ClassJob.RowId??0;
            if(job==0 || player.ContentId==0)throw new InvalidOperationException("Log in before applying job settings.");
            macro=new(config.Automation,all,shared,job,player.ContentId);
            config.Automation=parsed.Selection;all=parsed.All;shared=false;
            ClearPreview();request=7;
        }
        catch(Exception e){chat.PrintError(e.Message,"Job Setup");}
    }
    private void FinishMacro(bool success)
    {
        if(macro is not { } pending)return;
        if(!success)config.Automation=pending.Previous;
        all=pending.All;shared=pending.Shared;macro=null;
        if(success){pi.SavePluginConfig(config);chat.Print(wrathCycle==null?message:message.Replace("Re-enable Wrath Combo.","Restoring Wrath automatically."),"Job Setup");}
        else chat.PrintError(message,"Job Setup");
    }
    private VariantSelection? ReadActiveAutomation()
    {
        var job=objects.LocalPlayer?.ClassJob.RowId??0;
        var preset=pack.Jobs.FirstOrDefault(p=>p.JobId==job || p.BaseClasses.Contains(job));
        if(preset==null || !File.Exists(WrathPath))return null;
        var actual=JsonNode.Parse(File.ReadAllText(WrathPath))!.AsObject();
        if(WrathLoaded && actual["EnabledActionsV6"] is JsonArray array)
        {
            var ids=array.Select(n=>n!.GetValue<int>()).ToHashSet();
            var relevant=new[]{preset.AutoBurst,preset.AutoMitigation,preset.AutoMechanics}.OfType<PresetVariant>()
                .SelectMany(v=>v.Enable.Concat(v.Disable)).Distinct();
            foreach(var id in relevant)
            {
                if(!LivePresetPlan.Supported.TryGetValue(id,out var name))return null;
                var on=pi.GetIpcSubscriber<string,bool>("WrathCombo.GetComboOptionState").InvokeFunc(name);
                if(on)ids.Add(id);else ids.Remove(id);
            }
            actual["EnabledActionsV6"]=new JsonArray(ids.Select(id=>(JsonNode?)JsonValue.Create(id)).ToArray());
        }
        return AutomationStatus.Infer(preset,actual,config.AppliedAutomation.GetValueOrDefault(preset.Job));
    }
    private void UpdateStatus()
    {
        var job=objects.LocalPlayer?.ClassJob.RowId??0;
        if(job!=statusJob || player.ContentId!=statusCharacter)
        {
            statusJob=job;statusCharacter=player.ContentId;activeAutomation=null;nextStatusCheck=default;status.Shown=false;
        }
        if(job==0 || !WrathLoaded || condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51]){status.Shown=false;return;}
        if(macro!=null || lifecycleTask!=null){status.Shown=false;return;}
        if(DateTime.UtcNow>=nextStatusCheck)
        {
            nextStatusCheck=DateTime.UtcNow.AddSeconds(2);
            try {activeAutomation=ReadActiveAutomation();}
            catch {activeAutomation=null;} // Unavailable IPC or a partial save cannot establish active settings.
        }
        var text=activeAutomation==null?"":AutomationCommands.Status(activeAutomation);
        if(text!=statusText)
        {
            var value=activeAutomation??new();
            var builder=new SeStringBuilder().AddText("Job Settings: ");
            if(value.AutoMitigation)builder.AddIcon(BitmapFontIcon.Tank);
            if(value.AutoBurst)builder.AddIcon(BitmapFontIcon.SwordUnsheathed);
            if(value.AutoMechanics)builder.AddIcon(BitmapFontIcon.AnyClass);
            status.Text=builder.Build();statusText=text;
        }
        status.Shown=text.Length>0;
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
    private JobPreset Current(uint id)=>pack.Jobs.FirstOrDefault(j=>j.JobId==id || j.BaseClasses.Contains(id))??throw new InvalidOperationException("No reviewed preset for this class/job. Blue Mage is not included.");
    private IEnumerable<JobPreset> Baselines(uint job)=>all?pack.Jobs:[Current(job)];
    private IEnumerable<JobPreset> Selected(uint job)=>Baselines(job).Select(p=>HotbarTools.Variants.Resolve(p,config.Automation));
    private void DrawVariants()
    {
        ImGui.Separator();ImGui.TextUnformatted("Automation");
        var selection=config.Automation;
        Toggle("Auto burst",selection.AutoBurst,v=>selection.AutoBurst=v);
        Toggle("Auto mitigation",selection.AutoMitigation,v=>selection.AutoMitigation=v);
        Toggle("Auto job mechanics",selection.AutoMechanics,v=>selection.AutoMechanics=v);
        ImGui.TextWrapped("These global selections apply to the current class/job or all supported classes/jobs using the scope above. Preview and apply to update hotbars and Wrath settings.");
        void Toggle(string label,bool value,Action<bool> set)
        {
            if(ImGui.Checkbox(label,ref value)){set(value);ClearPreview();pi.SavePluginConfig(config);}
        }
    }
    private void ClearPreview(){recordSelection=false;hotbarPreview=null;wrathPreview=null;routePreview=null;routeOriginal=null;layoutSummary.Clear();}
    private void Draw()
    {
        if(!visible)return;
        WindowLayout.Prepare(ref firstOpen);
        if(ImGui.Begin("Job Setup",ref visible))
        {
            ImGui.BeginDisabled(lifecycleTask!=null || macro!=null);
            ImGui.TextWrapped("21 combat jobs, eight crafters, Miner, Botanist, Fisher and nine base-class aliases. Applying is deliberate, not continuous.");
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
            var liveWrath=config.PreferLiveWrath;
            if(ImGui.Checkbox("Prefer live Wrath updates when supported",ref liveWrath)){config.PreferLiveWrath=liveWrath;ClearPreview();pi.SavePluginConfig(config);}
            ImGui.TextWrapped("Reviewed feature-switch changes use Wrath's own commands without unloading. Other settings require manual or automatic unload/reload.");
            var autoWrath=config.AutoManageWrath;
            if(ImGui.Checkbox("Automatically unload/reload Wrath (experimental)",ref autoWrath)){config.AutoManageWrath=autoWrath;ClearPreview();pi.SavePluginConfig(config);}
            ImGui.TextWrapped("Uses version-sensitive Dalamud internals. Stops if incompatible. Restores Wrath after preview/apply when it was previously loaded; leaves a manually disabled Wrath disabled.");
            var compact=config.CompactKeyboard;
            if(ImGui.Checkbox("Compact keyboard layout",ref compact)){config.CompactKeyboard=compact;ClearPreview();pi.SavePluginConfig(config);}
            ImGui.TextWrapped("Fill keyboard 1–4, then Shift positions. ST, AoE, mobility and tank/healer reserves stay fixed. Controller positions stay fixed. Turn off to restore original spacing when applying.");
            DrawVariants();
            ImGui.BeginDisabled(WrathLoaded && !config.AutoManageWrath && !config.PreferLiveWrath);
            if(ImGui.Button("Preview variant: hotbars + Wrath together"))request=6;
            ImGui.EndDisabled();
            if(ImGui.Button("Preview hotbar layouts"))request=1;
            ImGui.Separator();
            ImGui.TextWrapped($"Wrath: {(WrathLoaded?config.AutoManageWrath?"enabled — automatic unload selected":"ENABLED — writes blocked":"disabled or not installed")}. Presets reviewed against {pack.WrathVersion}.");
            if(ImGui.Button("Open Wrath in plugin installer"))pi.OpenPluginInstallerTo(Dalamud.Interface.PluginInstallerOpenKind.InstalledPlugins,"Wrath");
            ImGui.TextWrapped("Use the installer button to disable Wrath before preview/apply, then re-enable it afterward.");
            if(ImGui.Checkbox("Also apply shared targeting/role options (global, affects every job)",ref shared))ClearPreview();
            ImGui.BeginDisabled(WrathLoaded && !config.AutoManageWrath && !config.PreferLiveWrath);
            if(ImGui.Button("Preview Wrath settings"))request=2;
            ImGui.EndDisabled();
            if(hotbarPreview!=null || wrathPreview!=null)
            {
                ImGui.Separator();ImGui.TextWrapped("Preview scope: "+previewScope);
                if(hotbarPreview!=null)
                {
                    foreach(var line in layoutSummary)ImGui.TextWrapped(line);
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
                ImGui.BeginDisabled(wrathPreview!=null && WrathLoaded && !config.AutoManageWrath && !config.PreferLiveWrath);
                if(ImGui.Button("Apply preview"))request=3;
                ImGui.EndDisabled();ImGui.SameLine();if(ImGui.Button("Cancel preview"))ClearPreview();
            }
            if(ImGui.CollapsingHeader("Backups / restore"))
            {
                ImGui.TextWrapped("Backup root: "+Root);
                ImGui.InputText("Backup JSON path",ref backupInput,1024);
                if(ImGui.Button("Preview hotbar restore"))request=4;
                ImGui.BeginDisabled(WrathLoaded && !config.AutoManageWrath && !config.PreferLiveWrath);
                if(ImGui.Button("Preview Wrath restore"))request=5;
                ImGui.EndDisabled();
            }
            ImGui.EndDisabled();
        }
        ImGui.End();
    }
    private unsafe void Update(IFramework _)
    {
        UpdateStatus();
        if(lifecycleTask!=null)
        {
            if(!lifecycleTask.IsCompleted)return;
            var finished=lifecycleTask;lifecycleTask=null;
            try
            {
                finished.GetAwaiter().GetResult();
                if(restoringWrath){wrathCycle=null;restoringWrath=false;message=operationMessage.Replace("Re-enable Wrath Combo.","").Replace("Re-enable Wrath after applying.","")+" Wrath restored.";return;}
                request=actionAfterUnload;
            }
            catch(Exception e)
            {
                ClearPreview();request=0;message="Wrath lifecycle failed: "+e.GetBaseException().Message;log.Error(e,"Automatic Wrath reload failed");
                if(macro!=null)FinishMacro(false);else chat.PrintError(message,"Job Setup");
                if(!restoringWrath && wrathCycle!=null)StartWrathRestore();else {wrathCycle=null;restoringWrath=false;}
                return;
            }
        }
        if(request==0)return;
        var action=request;request=0;
        try
        {
            var job=objects.LocalPlayer?.ClassJob.RowId??0;
            NativeHotbars.Ready(job);
            if(macro is { } pending && (job!=pending.Job || player.ContentId!=pending.Character))throw new InvalidOperationException("Character/job changed during macro application. Run the command again.");
            if(condition[ConditionFlag.InCombat] || condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51])throw new InvalidOperationException("Leave combat and wait for loading to finish.");
            if(config.AutoManageWrath && WrathLoaded && !CanLiveOperation(action,job) && (action is 2 or 5 or 6 or 7 || action==3 && wrathPreview!=null))
            {
                wrathCycle=WrathLifecycle.Resolve();actionAfterUnload=action;restoringWrath=false;
                lifecycleTask=wrathCycle.UnloadAsync();message="Waiting for Wrath to unload...";return;
            }
            if(action!=3){ClearPreview();previewJob=job;previewIdentity=Character;previewScope=all?"All 32 presets plus nine base-class layouts":"Current class/job";}
            switch(action)
            {
                case 1: PreviewHotbars(job);break;
                case 7:
                case 6:
                case 2:
                    if(WrathLoaded && !CanLiveOperation(action,job))throw new InvalidOperationException("This change includes settings that cannot be updated live. Enable automatic unload/reload or disable Wrath manually.");
                    if(!WrathLoaded)RequireWrathDisabled();
                    if(action is 6 or 7)PreviewHotbars(job);
                    wrathOriginal=File.ReadAllText(WrathPath);
                    wrathPreview=WrathMerge.Merge(JsonNode.Parse(wrathOriginal)!.AsObject(),Selected(job),shared?pack.SharedSettings:null);
                    recordSelection=true;
                    previewScope+=(shared?" + global targeting/role settings":all?"; global targeting preserved":"; other jobs/global targeting preserved");
                    previewScope+=string.Join("",Baselines(job).Where(p=>p.AutoBurst!=null||p.AutoMitigation!=null).Select(p=>{
                        var v=config.Automation;
                        return $"; {p.Job} burst {(v.AutoBurst?"auto":"baseline")}, mitigation {(v.AutoMitigation?"auto":"baseline")}, mechanics {(v.AutoMechanics?"auto":"baseline")}{(p.MechanicsAlreadyAutomatic?" (already covered)":"")}";
                    }));
                    previewScope+=WrathLoaded?"; live Wrath commands (no unload)":"; unloaded configuration write";
                    message=action is 6 or 7?"Combined preview ready: matching hotbars and Wrath settings. Re-enable Wrath after applying.":"Wrath preview ready. Re-enable Wrath after applying; hotbars are separate in this mode.";
                    if(action==7){Apply(job);FinishMacro(true);}
                    break;
                case 3: Apply(job);break;
                case 4:
                    PreviewRestore(job);break;
                case 5:
                    RequireWrathDisabled();RequireBackup(backupInput,WrathBackups);wrathOriginal=File.ReadAllText(WrathPath);wrathPreview=JsonNode.Parse(File.ReadAllText(backupInput))!.AsObject();
                    if(wrathPreview["Version"]?.GetValue<int>()!=6)throw new InvalidDataException("Invalid Wrath backup schema.");
                    previewScope="Full Wrath backup restore (all settings)";break;
            }
        }
        catch(Exception e){ClearPreview();message=e.Message;log.Error(e,"Job Setup operation failed");FinishMacro(false);}
        if(wrathCycle!=null && lifecycleTask==null)StartWrathRestore();
    }
    private void StartWrathRestore()
    {
        operationMessage=message;restoringWrath=true;lifecycleTask=wrathCycle!.RestoreAsync();message="Restoring Wrath... "+operationMessage;
    }
    private static void RequireBackup(string path,string directory)
    {
        if(!Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Select a backup from the displayed plugin backup directory.");
    }
    private unsafe void PreviewHotbars(uint current)
    {
        if(config.RegularBar1 is <1 or >10 || config.RegularBar2 is <1 or >10 || config.RegularBar1==config.RegularBar2 || config.CrossSet is <1 or >8)throw new InvalidOperationException("Choose distinct regular bars 1–10 and cross set 1–8.");
        List<SlotMap>? bridge=null;
        if(BridgeLoaded)
        {
            if(!config.ApplyCross)throw new InvalidOperationException("Enable cross layout application while Bridge is loaded so both sides and its routes stay consistent.");
            mappingAtPreview=pi.GetIpcSubscriber<string>("HotbarBridge.GetMapping").InvokeFunc();
            bridge=JsonSerializer.Deserialize<List<SlotMap>>(mappingAtPreview)??throw new InvalidOperationException("Bridge mapping unavailable.");
            Defaults.Validate(bridge);
            routesAtPreview=pi.GetIpcSubscriber<string>("HotbarBridge.GetJobRoutes").InvokeFunc();
            var old=JsonSerializer.Deserialize<Dictionary<uint,Dictionary<string,Position>>>(routesAtPreview)!;
            routeOriginal=new();routePreview=new();
            foreach(var preset in Selected(current))foreach(var job in all?new[]{preset.JobId}.Concat(preset.BaseClasses):new[]{current})
                routeOriginal[job]=old.GetValueOrDefault(job)??new();
        }
        var edits=new Dictionary<(uint,Position),SlotEdit>();
        actions ??= new ActionCatalog(data.GetExcelSheet<Sheets.Action>(ClientLanguage.English),data.GetExcelSheet<Sheets.CraftAction>(ClientLanguage.English));
        foreach(var preset in Selected(current))
        {
            var layout=KeyboardLayout.Compile(preset,config.CompactKeyboard);
            layoutSummary.Add(preset.Job+": "+string.Join("; ",preset.Slots.Where(s=>s.Action!="Empty").OrderBy(s=>Array.IndexOf(KeyboardLayout.Positions,layout[new(s.RegularBar-1,s.RegularSlot-1)])).Select(s=>{
                var p=layout[new(s.RegularBar-1,s.RegularSlot-1)];
                var key=p.Bar==0?$"{p.Slot+1}":p.Slot==11?"Shift+=":p.Slot==10?"Shift+-":$"Shift+{p.Slot+1}";
                return key+" "+s.Action;
            })));
            var jobs=all?new[]{preset.JobId}.Concat(preset.BaseClasses):new[]{current};
            foreach(var job in jobs)
            {
                if(routePreview!=null)routePreview[job]=new();
                var defaults=Defaults.Maps().Where(m=>m.Applies(preset.JobId)&&!m.SharedAcrossJobs).ToList();
                foreach(var source in KeyboardLayout.Positions)
                {
                    var slot=preset.Slots.SingleOrDefault(s=>new Position(s.RegularBar-1,s.RegularSlot-1)==source);
                    var value=slot==null?default:actions.ResolveSlot(preset.Job,slot.Action);
                    var original=Remap(source);
                    var regular=Remap(layout[source]);
                    var destination=new Position(config.CrossSet+9,defaults.Single(m=>m.Regular==source).Cross.Slot);
                    if(bridge!=null)
                    {
                        var map=bridge.SingleOrDefault(m=>m.Applies(job)&&m.Regular==original);
                        if(map==null || map.SharedAcrossJobs)throw new InvalidOperationException($"Bridge has no job-specific pair for {preset.Job} {original}. Update Bridge or adjust its base map.");
                        destination=map.Cross;
                        routePreview![job][map.Id]=regular;
                    }
                    Add(regular);
                    if(config.ApplyCross)Add(destination);
                    void Add(Position position)
                    {
                        if(!position.Valid)throw new InvalidOperationException("Invalid destination.");
                        if(NativeHotbars.Ready(current)->IsHotbarShared((uint)position.Bar))throw new InvalidOperationException($"{position}: destination is natively shared. Choose job-specific bars first.");
                        var old=NativeHotbars.Read(job,position);
                        if(old!=value && !edits.TryAdd((job,position),new SlotEdit(job,position,old,value)))throw new InvalidOperationException("Duplicate preset destination.");
                    }
                }
                if(bridge!=null)LayoutRoutes.Effective(bridge.Where(m=>m.Applies(job)),routePreview![job]);
            }
        }
        Position Remap(Position p)=>new((p.Bar==0?config.RegularBar1:config.RegularBar2)-1,p.Slot);
        hotbarPreview=edits.Values.ToList();message=$"Hotbar preview ready: {hotbarPreview.Count} changes. Actions not yet learned remain locked by the game.";
    }
    private void Apply(uint job)
    {
        if(job!=previewJob || Character!=previewIdentity)throw new InvalidOperationException("Character/job changed. Preview again.");
        if(wrathPreview==null && hotbarPreview==null)throw new InvalidOperationException("Preview first.");
        if(wrathPreview!=null)CheckWrath();
        var bridge=hotbarPreview!=null && BridgeLoaded;
        if(routePreview!=null && !bridge)throw new InvalidOperationException("Bridge was disabled after preview. Preview again.");
        if(bridge && routePreview==null)throw new InvalidOperationException("Bridge loaded after preview. Preview again.");
        if(bridge && !pi.GetIpcSubscriber<bool>("HotbarBridge.BeginExternalEdit").InvokeFunc())throw new InvalidOperationException("Bridge is busy. Try again.");
        try
        {
            if(bridge && (pi.GetIpcSubscriber<string>("HotbarBridge.GetJobRoutes").InvokeFunc()!=routesAtPreview || pi.GetIpcSubscriber<string>("HotbarBridge.GetMapping").InvokeFunc()!=mappingAtPreview))throw new InvalidOperationException("Bridge mapping changed after preview. Preview again.");
            if(hotbarPreview!=null)
            {
                // This complete backup also preserves the routing needed for controller sync.
                JsonStore.Write(Path.Combine(HotbarBackups,$"layout-{DateTime.UtcNow:yyyyMMdd-HHmmss-ffff}-{Guid.NewGuid():N}.json"),new LayoutBackup { Edits=hotbarPreview,Routes=routeOriginal });
                PairedApply.Run(()=>NativeHotbars.Apply(job,hotbarPreview,HotbarBackups),()=>{
                    if(bridge)SetRoutes(routePreview!);
                    if(wrathPreview!=null)WriteWrath();
                },()=>{
                    try {NativeHotbars.Apply(job,hotbarPreview.Select(e=>new SlotEdit(e.Job,e.Position,e.After,e.Before)).ToList(),HotbarBackups);}
                    finally {if(bridge)SetRoutes(routeOriginal!);}
                });
            }
            else WriteWrath();
            message=wrathPreview!=null?"Selected settings applied"+(hotbarPreview!=null?" with matching hotbars":"")+(WrathLoaded?". Wrath updated live. Backups saved.":". Re-enable Wrath Combo. Backups saved."):"Hotbar layouts and sync routes applied. Backup saved; test the actions in game.";
        }
        finally { if(bridge)pi.GetIpcSubscriber<bool>("HotbarBridge.EndExternalEdit").InvokeFunc(); }
        if(wrathPreview!=null)
        {
            if(recordSelection)foreach(var preset in Baselines(job))
                config.AppliedAutomation[preset.Job]=new(){AutoBurst=config.Automation.AutoBurst,AutoMitigation=config.Automation.AutoMitigation,AutoMechanics=config.Automation.AutoMechanics};
            else config.AppliedAutomation.Clear();
            pi.SavePluginConfig(config);
        }
        nextStatusCheck=default;
        ClearPreview();
    }
    private void SetRoutes(Dictionary<uint,Dictionary<string,Position>> routes)
    {
        if(!pi.GetIpcSubscriber<string,bool>("HotbarBridge.SetJobRoutes").InvokeFunc(JsonSerializer.Serialize(routes)))throw new InvalidOperationException("Bridge rejected layout routes.");
    }
    private void PreviewRestore(uint job)
    {
        RequireBackup(backupInput,HotbarBackups);
        var json=File.ReadAllText(backupInput);
        if(JsonNode.Parse(json) is JsonObject)
        {
            var backup=JsonSerializer.Deserialize<LayoutBackup>(json)!;
            hotbarPreview=backup.Edits.Select(e=>new SlotEdit(e.Job,e.Position,NativeHotbars.Read(e.Job,e.Position),e.Before)).ToList();
            if(backup.Routes!=null && !BridgeLoaded)throw new InvalidOperationException("Enable Bridge to restore this layout's sync routes.");
            routePreview=backup.Routes;
        }
        else hotbarPreview=NativeHotbars.RestorePlan(job,backupInput);
        if(BridgeLoaded)
        {
            mappingAtPreview=pi.GetIpcSubscriber<string>("HotbarBridge.GetMapping").InvokeFunc();
            routesAtPreview=pi.GetIpcSubscriber<string>("HotbarBridge.GetJobRoutes").InvokeFunc();
            var old=JsonSerializer.Deserialize<Dictionary<uint,Dictionary<string,Position>>>(routesAtPreview)!;
            routeOriginal=(routePreview?.Keys??hotbarPreview.Select(e=>e.Job).Distinct()).ToDictionary(j=>j,j=>old.GetValueOrDefault(j)??new Dictionary<string,Position>());
            if(routePreview==null)routePreview=routeOriginal;
        }
        previewScope="Hotbar backup restore";
    }
    private bool CanLiveOperation(int action,uint job)
    {
        if(!config.PreferLiveWrath)return false;
        if(action==3 && wrathPreview!=null)return LivePresetPlan.Create(JsonNode.Parse(wrathOriginal)!.AsObject(),wrathPreview)!=null;
        if(action is not (2 or 6 or 7))return false;
        var before=JsonNode.Parse(File.ReadAllText(WrathPath))!.AsObject();
        return LivePresetPlan.Create(before,WrathMerge.Merge(before,Selected(job),shared?pack.SharedSettings:null))!=null;
    }
    private void WriteLiveWrath()
    {
        var before=JsonNode.Parse(wrathOriginal)!.AsObject();
        var plan=LivePresetPlan.Create(before,wrathPreview!)??throw new InvalidOperationException("No reviewed live update path.");
        var old=before["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
        foreach(var id in plan.Keys)
            if(ReadLive(id)!=old.Contains(id))throw new InvalidOperationException("Wrath's live state differs from saved settings (possibly IPC control). No commands sent.");
        try
        {
            foreach(var (id,enabled) in plan)Send(id,enabled);
            foreach(var (id,enabled) in plan)if(ReadLive(id)!=enabled)throw new InvalidOperationException("Wrath did not accept a live setting; restoring the previous switches.");
            var saved=JsonNode.Parse(File.ReadAllText(WrathPath))!["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
            var expected=wrathPreview!["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
            if(!saved.SetEquals(expected))throw new InvalidOperationException("Wrath did not persist the expected switches; restoring the previous switches.");
        }
        catch(Exception failure)
        {
            try
            {
                var restore=LivePresetPlan.Create(wrathPreview!,before)??throw new InvalidOperationException("No safe reverse command plan; use the backup.");
                foreach(var (id,enabled) in restore)Send(id,enabled);
                foreach(var id in plan.Keys)if(ReadLive(id)!=old.Contains(id))throw new InvalidOperationException("Wrath switch rollback failed; use the backup.");
                var saved=JsonNode.Parse(File.ReadAllText(WrathPath))!["EnabledActionsV6"]!.AsArray().Select(n=>n!.GetValue<int>()).ToHashSet();
                if(!saved.SetEquals(old))throw new InvalidOperationException("Wrath did not persist the rollback; use the backup.");
            }
            catch(Exception rollback){throw new AggregateException("Live update and rollback failed.",failure,rollback);}
            throw;
        }
        bool ReadLive(int id)=>pi.GetIpcSubscriber<string,bool>("WrathCombo.GetComboOptionState").InvokeFunc(LivePresetPlan.Supported[id]);
        void Send(int id,bool enabled)
        {
            if(!commands.ProcessCommand($"/wrath {(enabled?"set":"unset")} {id}"))throw new InvalidOperationException("Wrath command unavailable.");
        }
    }
    private void CheckWrath()
    {
        if(!WrathLoaded)RequireWrathDisabled();
        else if(!config.PreferLiveWrath || wrathPreview==null || LivePresetPlan.Create(JsonNode.Parse(wrathOriginal)!.AsObject(),wrathPreview)==null)
            throw new InvalidOperationException("Disable Wrath or use automatic unload/reload for these settings.");
        if(File.ReadAllText(WrathPath)!=wrathOriginal)throw new InvalidOperationException("Wrath configuration changed since preview. Preview again.");
    }
    private void WriteWrath()
    {
        CheckWrath();
        Directory.CreateDirectory(WrathBackups);
        File.Copy(WrathPath,Path.Combine(WrathBackups,$"{DateTime.UtcNow:yyyyMMdd-HHmmss-ffff}-{Guid.NewGuid():N}.json"));
        if(WrathLoaded){WriteLiveWrath();return;}
        var temporary=WrathPath+".jobsetup.tmp";
        File.WriteAllText(temporary,wrathPreview!.ToJsonString(JsonStore.Options));
        CheckWrath();
        RequireWrathDisabled();
        File.Replace(temporary,WrathPath,null);
    }
    public void Dispose(){
        if(macro!=null){message="Macro application cancelled because Job Setup was unloaded.";FinishMacro(false);}
        if(wrathCycle is { } cycle)
        {
            var pending=lifecycleTask;
            _=Task.Run(async()=>{try {if(pending!=null)try {await pending;}catch { } await cycle.RestoreAsync();}catch(Exception e){log.Error(e,"Restore Wrath manually after Job Setup unload");}});
        }
        status.Remove();framework.Update-=Update;pi.UiBuilder.Draw-=Draw;pi.UiBuilder.OpenMainUi-=Open;commands.RemoveHandler("/jobsetup");}
}
