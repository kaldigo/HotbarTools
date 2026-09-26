using Sheets=Lumina.Excel.Sheets;
namespace HotbarTools;

/// <summary>Resolve assigned player actions, excluding obsolete/NPC/PvP name collisions.</summary>
public sealed class ActionCatalog
{
    private readonly Dictionary<string,List<Sheets.Action>> actions;
    public ActionCatalog(IEnumerable<Sheets.Action> rows)
    {
        actions=rows.Where(a=>a.IsPlayerAction&&!a.IsPvP)
            .GroupBy(a=>a.Name.ToString(),StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.OrdinalIgnoreCase);
    }
    public uint Resolve(string job,string name)
    {
        if(name=="Empty")return 0;
        var property=typeof(Sheets.ClassJobCategory).GetProperty(job)??throw new InvalidDataException($"Unknown job {job}");
        var matches=actions.GetValueOrDefault(name,[])
            .Where(a=>(bool)property.GetValue(a.ClassJobCategory.Value)!).ToList();
        if(matches.Count!=1)throw new InvalidDataException($"Expected one current player action for {job} / {name}, found {matches.Count}. Preset needs review.");
        return matches[0].RowId;
    }
}
