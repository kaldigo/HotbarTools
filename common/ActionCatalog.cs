using Sheets=Lumina.Excel.Sheets;
namespace HotbarTools;

/// <summary>Resolve native assignments, including class-specific crafting commands.</summary>
public sealed class ActionCatalog
{
    private readonly List<Sheets.Action> actions;
    private readonly List<Sheets.CraftAction> crafts;
    public ActionCatalog(IEnumerable<Sheets.Action> rows,IEnumerable<Sheets.CraftAction>? craftRows=null)
    {
        actions=rows.Where(a=>a.IsPlayerAction&&!a.IsPvP).ToList();
        crafts=craftRows?.ToList()??[];
    }
    public uint Resolve(string job,string name)=>ResolveSlot(job,name).Id;
    public SlotValue ResolveSlot(string job,string name)
    {
        if(name=="Empty")return default;
        var property=typeof(Sheets.ClassJobCategory).GetProperty(job)??throw new InvalidDataException($"Unknown job {job}");
        string[] noncombat=["CRP","BSM","ARM","GSM","LTW","WVR","ALC","CUL","MIN","BTN","FSH"];
        var index=Array.IndexOf(noncombat,job);
        var matches=actions.Where(a=>string.Equals(a.Name.ToString(),name,StringComparison.OrdinalIgnoreCase))
            .Where(a=>index>=0?a.ClassJob.RowId==index+8:(bool)property.GetValue(a.ClassJobCategory.Value)!)
            .Select(a=>new SlotValue(1,a.RowId)).ToList();
        if(index is >=0 and <8)
            matches.AddRange(crafts.Where(a=>a.ClassJob.RowId==index+8 && string.Equals(a.Name.ToString(),name,StringComparison.OrdinalIgnoreCase)).Select(a=>new SlotValue(9,a.RowId)));
        if(matches.Count!=1)throw new InvalidDataException($"Expected one current player action for {job} / {name}, found {matches.Count}. Preset needs review.");
        return matches[0];
    }
}
