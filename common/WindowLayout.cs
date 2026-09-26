using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace HotbarTools;

public static class WindowLayout
{
    public static void Prepare(ref bool firstOpen)
    {
        var display=ImGui.GetIO().DisplaySize;
        var maximum=new Vector2(Math.Max(320,display.X-40),Math.Max(260,display.Y-60));
        var minimum=Vector2.Min(new Vector2(650,420),maximum);
        ImGui.SetNextWindowSizeConstraints(minimum,maximum);
        if(!firstOpen)return;
        ImGui.SetNextWindowSize(Vector2.Min(new Vector2(900,680),maximum),ImGuiCond.Always);
        ImGui.SetNextWindowPos(display/2,ImGuiCond.Always,new Vector2(0.5f));
        firstOpen=false;
    }
}
