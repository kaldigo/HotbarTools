using System.Collections;
using System.Reflection;
using Dalamud.Plugin;

namespace JobSetup;

// Optional adapter to Dalamud internals. Resolve and validate the entire contract before unloading.
internal sealed class WrathLifecycle
{
    private readonly object plugin;
    private readonly MethodInfo unload,load;
    private readonly PropertyInfo state;
    private readonly object disposal;
    private bool ownsUnload;
    private WrathLifecycle(object plugin,MethodInfo unload,MethodInfo load,PropertyInfo state,object disposal)
    {this.plugin=plugin;this.unload=unload;this.load=load;this.state=state;this.disposal=disposal;}
    private string State=>state.GetValue(plugin)?.ToString()??"Unknown";
    public static WrathLifecycle Resolve()
    {
        var assembly=typeof(IDalamudPluginInterface).Assembly;
        var managerType=assembly.GetType("Dalamud.Plugin.Internal.PluginManager",true)!;
        var service=assembly.GetType("Dalamud.Service`1",true)!.MakeGenericType(managerType);
        var get=service.GetMethod("Get",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,Type.EmptyTypes)??throw new NotSupportedException("Dalamud service accessor changed.");
        var manager=get.Invoke(null,null)??throw new NotSupportedException("Plugin manager unavailable.");
        var plugins=managerType.GetProperty("InstalledPlugins")?.GetValue(manager) as IEnumerable??throw new NotSupportedException("Plugin list API changed.");
        var matches=plugins.Cast<object>().Where(p=>p.GetType().GetProperty("InternalName")?.GetValue(p)?.ToString()=="WrathCombo").ToArray();
        if(matches.Length!=1)throw new NotSupportedException("Automatic reload requires exactly one installed Wrath copy.");
        var plugin=matches[0];var type=plugin.GetType();
        var state=type.GetProperty("State")??throw new NotSupportedException("Plugin state API changed.");
        if(state.GetValue(plugin)?.ToString()!="Loaded")throw new InvalidOperationException("Wrath is not in a stable loaded state.");
        var disposalType=assembly.GetType("Dalamud.Plugin.Internal.Types.PluginLoaderDisposalMode",true)!;
        var unload=type.GetMethod("UnloadAsync",[disposalType]);
        var load=type.GetMethod("LoadAsync",[typeof(PluginLoadReason),typeof(bool),typeof(CancellationToken)]);
        if(unload?.ReturnType!=typeof(Task)||load?.ReturnType!=typeof(Task))throw new NotSupportedException("Dalamud lifecycle API changed; disable Wrath manually.");
        return new(plugin,unload,load,state,Enum.Parse(disposalType,"WaitBeforeDispose"));
    }
    public Task UnloadAsync()=>Task.Run(async()=>{
        ownsUnload=true;
        await ((Task)unload.Invoke(plugin,[disposal])!).ConfigureAwait(false);
        if(State!="Unloaded")throw new InvalidOperationException("Wrath did not finish unloading; settings were not written.");
    });
    public Task RestoreAsync()=>Task.Run(async()=>{
        if(!ownsUnload)return;
        if(State=="Loaded"){ownsUnload=false;return;}
        if(State!="Unloaded")throw new InvalidOperationException($"Wrath is {State}; automatic restore stopped. Check /xlplugins.");
        await ((Task)load.Invoke(plugin,[PluginLoadReason.Reload,true,CancellationToken.None])!).ConfigureAwait(false);
        if(State!="Loaded")throw new InvalidOperationException("Wrath did not finish loading. Enable it in /xlplugins.");
        ownsUnload=false;
    });
}
