namespace HotbarTools;

public enum SaveConfirmation { Waiting, Confirmed, TimedOut }
public static class LiveSaveConfirmation
{
    public static SaveConfirmation Check(bool liveMatches,bool savedMatches,DateTime now,DateTime deadline)
        =>liveMatches&&savedMatches?SaveConfirmation.Confirmed:now>=deadline?SaveConfirmation.TimedOut:SaveConfirmation.Waiting;
    public static bool Matches(IEnumerable<int> saved,IReadOnlyDictionary<int,bool> expected)
    {
        var ids=saved.ToHashSet();return expected.All(p=>ids.Contains(p.Key)==p.Value);
    }
}
