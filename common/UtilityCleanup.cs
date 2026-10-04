namespace HotbarTools;

public static class UtilityCleanup
{
    public static List<SlotEdit> Plan(uint current,IEnumerable<uint> jobs,IReadOnlyList<SlotMap> maps,
        bool oldPageShared,Func<uint,Position,SlotValue> read)
    {
        var defaults=Defaults.Maps().Where(m=>m.Id.StartsWith("utility-")).ToList();
        var protectedSlots=maps.SelectMany(m=>new[]{m.Regular,m.Cross}).ToHashSet();
        var candidates=maps.Where(m=>m.Enabled && m.SharedAcrossJobs && m.Profile=="All" &&
            defaults.Any(d=>d.Id==m.Id && d.Regular==m.Regular && d.Cross==m.Cross)).ToList();
        var edits=new List<SlotEdit>();
        foreach(var job in (oldPageShared?new[]{current}:jobs).Distinct())
        foreach(var map in candidates)
        {
            var old=new Position(11,map.Cross.Slot);
            if(protectedSlots.Contains(old))continue;
            var value=read(job,old).Normalized;
            if(value==default || value!=read(job,map.Regular).Normalized || value!=read(job,map.Cross).Normalized)continue;
            edits.Add(new(job,old,value,default));
        }
        return edits;
    }
}
