using System.Text.Json;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using Sheets = Lumina.Excel.Sheets;

namespace HotbarBridge;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly IFramework framework;
    private readonly IObjectTable objects;
    private readonly IGameGui gui;
    private readonly IDataManager data;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly SyncController sync;
    private bool visible;
    private bool firstOpen=true;
    private bool requested;
    private string status = "Switch to Gunbreaker, leave duty/PvP, and show your regular utility bars before exporting.";
    private string? lastExport;

    public Plugin(IDalamudPluginInterface pi, ICommandManager commands, IFramework framework,
        IObjectTable objects, IGameGui gui, IDataManager data, IChatGui chat, IPluginLog log, IPlayerState player, ICondition condition)
    {
        this.pi = pi; this.commands = commands; this.framework = framework;
        this.objects = objects; this.gui = gui; this.data = data; this.chat = chat; this.log = log;
        sync = new SyncController(pi, objects, player, condition, log);
        commands.AddHandler("/hotbarbridge", new CommandInfo(OnCommand) { HelpMessage = "Open hotbar sync settings; /hotbarbridge export captures current hotbars." });
        pi.UiBuilder.Draw += Draw;
        pi.UiBuilder.OpenMainUi += Open;
        framework.Update += Update;
    }

    private void Open() => visible = true;
    private void OnCommand(string command, string args)
    {
        visible = true;
        if (args.Trim().Equals("export", StringComparison.OrdinalIgnoreCase)) requested = true;
    }
    private void Draw()
    {
        if (!visible) return;
        HotbarTools.WindowLayout.Prepare(ref firstOpen);
        if (ImGui.Begin("Hotbar Bridge", ref visible))
        {
            sync.Draw();
            ImGui.Separator();
            ImGui.TextWrapped("Read-only diagnostic export:");
            if (ImGui.Button("Export current hotbars")) requested = true;
            ImGui.TextWrapped(status);
            if (lastExport != null && ImGui.Button("Copy export folder path")) ImGui.SetClipboardText(lastExport);
        }
        ImGui.End();
    }
    private void Update(IFramework _)
    {
        sync.Tick();
        if (!requested) return;
        requested = false;
        try { Capture(); }
        catch (Exception ex)
        {
            status = "Export failed: " + ex.Message;
            log.Error(ex, "Hotbar export failed");
            chat.PrintError("[Hotbar Bridge] " + status);
        }
    }

    private unsafe void Capture()
    {
        var player = objects.LocalPlayer;
        var module = RaptureHotbarModule.Instance();
        var input = UIInputData.Instance();
        if (player == null || module == null || !module->ModuleReady || input == null)
            throw new InvalidOperationException("Log in and wait for your character and hotbars to finish loading.");
        var job = player.ClassJob.RowId;
        if (module->PvPHotbarsActive || module->ActiveHotbarClassJobId != job)
            throw new InvalidOperationException("Wait for job switching to finish and use PvE hotbars.");

        var bars = new List<object>();
        for (var bar = 0; bar < 18; bar++)
        {
            var slots = new List<object>();
            var count = bar < 10 ? 12 : 16;
            for (var slot = 0; slot < count; slot++)
            {
                // Capture assigned commands, never use adjusted icon IDs as assignments.
                ref var value = ref module->Hotbars[bar].Slots[slot];
                var type = value.CommandType.ToString();
                slots.Add(new {
                    Slot = slot + 1, NativeSlotIndex = slot, CommandType = type,
                    CommandTypeId = (byte)value.CommandType, value.CommandId,
                    AssignedName = NameFor(type, value.CommandId),
                    CachedKeybindHint = value.PopUpKeybindHintString,
                    CachedDisplayHelp = value.PopUpHelp.ToString(),
                });
            }
            bars.Add(new { Kind = bar < 10 ? "Regular" : "Cross", Number = bar < 10 ? bar + 1 : bar - 9,
                NativeBarIndex = bar, Shared = module->IsHotbarShared((uint)bar), Slots = slots });
        }

        var bindings = new List<object>();
        var bindSpan = input->GetKeybindSpan();
        foreach (var id in Enum.GetValues<InputId>())
        {
            var name = id.ToString();
            if (!name.StartsWith("HOTBAR_", StringComparison.Ordinal) || (int)id < 0 || (int)id >= bindSpan.Length) continue;
            ref var bind = ref bindSpan[(int)id];
            var keys = new List<object>();
            for (var i = 0; i < 2; i++)
            {
                var key = bind.KeySettings[i];
                keys.Add(new { Alternative = i + 1, Key = key.Key.ToString(), KeyCode = (byte)key.Key,
                    Modifiers = key.KeyModifier.ToString(), ModifierBits = (byte)key.KeyModifier });
            }
            bindings.Add(new { InputName = name, InputId = (int)id, Keys = keys });
        }

        var geometry = new List<object>();
        for (var bar = 0; bar < 10; bar++)
        {
            var addonName = bar == 0 ? "_ActionBar" : $"_ActionBar{bar:00}";
            var addon = (AddonActionBarX*)gui.GetAddonByName(addonName).Address;
            if (addon == null) { geometry.Add(new { Addon = addonName, Loaded = false }); continue; }
            var positions = new List<object>();
            for (var slot = 0; slot < Math.Min(12, addon->ActionBarSlotVector.Count); slot++)
            {
                var icon = addon->ActionBarSlotVector[slot].Icon;
                if (icon == null) continue;
                // Icon-local coordinates are often (0,0). Walk the parent chain to
                // include the layout container's actual position and scaling.
                var node = &icon->AtkResNode;
                float x = 0, y = 0;
                var ancestors = new List<object>();
                var depth = 0;
                while (node != null && depth < 64)
                {
                    ancestors.Add(new { Depth = depth, X = node->X, Y = node->Y, ScaleX = node->ScaleX, ScaleY = node->ScaleY });
                    x = x * node->ScaleX + node->X;
                    y = y * node->ScaleY + node->Y;
                    node = node->ParentNode;
                    depth++;
                }
                positions.Add(new { Slot = slot + 1, X = x, Y = y,
                    ParentChainComplete = node == null, Ancestors = ancestors });
            }
            geometry.Add(new { Addon = addonName, Loaded = true, Visible = addon->IsVisible,
                ReferencedNativeBarIndex = addon->RaptureHotbarId, Layout = addon->ActionBarLayout.ToString(),
                Coordinates = "Icon position accumulated through parent translation/scaling; compare within this bar only. Ancestors retained for verification. Rotation is not applied.", Slots = positions });
        }
        var timestamp = DateTimeOffset.UtcNow;
        var snapshot = new {
            SchemaVersion = 2, CapturedUtc = timestamp, JobId = job,
            Job = player.ClassJob.Value.Abbreviation.ToString(), Level = player.Level,
            PluginVersion = "0.2.2", ClientStructsVersion = typeof(RaptureHotbarModule).Assembly.GetName().Version?.ToString(),
            Notes = new[] { "Read-only live PvE capture. No character name or content ID is exported.",
                "One-based Bar/Slot labels; Native indices are zero-based. Cross slots are raw indices, not inferred controller labels.",
                "Cached hints can be stale on hidden bars; Keybinds are the input configuration. HOTBAR_1 follows the main cycling bar.",
                "Key enum names are advisory: the first capture labeled minus/equal as F21/F19 despite matching displayed minus/equal hints. Preserve raw codes and confirm labels in game.",
                "Sync configuration is separate from this diagnostic capture. Preset loading belongs to Job Setup." },
            Bars = bars, Keybinds = bindings, RegularBarGeometry = geometry,
        };
        var folder = Path.Combine(pi.GetPluginConfigDirectory(), "exports", $"{timestamp:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "hotbars.json"), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        lastExport = folder;
        status = $"Exported {snapshot.Job}: 10 regular and 8 cross hotbars to {folder}";
        chat.Print("[Hotbar Bridge] " + status);
    }

    private string? NameFor(string type, uint id)
    {
        if (id == 0) return null;
        // Preserve original IDs, including HQ item markers, in the snapshot.
        return type switch {
            "Action" => data.GetExcelSheet<Sheets.Action>().GetRowOrDefault(id)?.Name.ToString(),
            "GeneralAction" => data.GetExcelSheet<Sheets.GeneralAction>().GetRowOrDefault(id)?.Name.ToString(),
            "Item" => data.GetExcelSheet<Sheets.Item>().GetRowOrDefault(id % 1_000_000)?.Name.ToString(),
            "CraftAction" => data.GetExcelSheet<Sheets.CraftAction>().GetRowOrDefault(id)?.Name.ToString(),
            _ => null,
        };
    }
    public void Dispose()
    {
        sync.Dispose();
        framework.Update -= Update;
        pi.UiBuilder.Draw -= Draw;
        pi.UiBuilder.OpenMainUi -= Open;
        commands.RemoveHandler("/hotbarbridge");
    }
}
