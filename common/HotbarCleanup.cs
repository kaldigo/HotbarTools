namespace HotbarTools;

public static class HotbarCleanup
{
    // The caller supplies final layout destinations, after variant resolution and compaction.
    public static List<SlotEdit> Plan(uint job, IEnumerable<Position> layout,
        IEnumerable<SlotMap> bridgeMaps, Func<int,bool> isShared,
        Func<Position,SlotValue> read)
    {
        if(job is 0 or >42)throw new InvalidDataException("Invalid cleanup job.");
        var keep=layout.ToHashSet();
        // Preserve shared pairs even when disabled or belonging to another profile.
        keep.UnionWith(bridgeMaps.Where(m=>m.SharedAcrossJobs).SelectMany(m=>new[]{m.Regular,m.Cross}));
        if(keep.Any(p=>!p.Valid))throw new InvalidDataException("Invalid protected cleanup position.");
        var result=new List<SlotEdit>();
        for(var bar=0;bar<18;bar++)
        {
            if(isShared(bar))continue;
            for(var slot=0;slot<(bar<10?12:16);slot++)
            {
                var p=new Position(bar,slot);
                if(keep.Contains(p))continue;
                var before=read(p).Normalized;
                if(before!=default)result.Add(new(job,p,before,default));
            }
        }
        return result;
    }
}
