namespace HotbarTools;

public sealed record AutomationCommand(bool All,VariantSelection Selection);
public static class AutomationCommands
{
    public const string Help="/jobsetup current|all [MBJ|none] or /jobsetup current|all on|off|toggle MBJ. M=mitigation, B=burst, J=job mechanics. Applies hotbars and Wrath together.";
    public static string Letters(VariantSelection value)=>(value.AutoMitigation?"M":"")+(value.AutoBurst?"B":"")+(value.AutoMechanics?"J":"");
    public static string Status(VariantSelection value)=>Letters(value) is {Length:>0} flags?"Job Settings: "+flags:"";
    public static AutomationCommand Parse(string input,VariantSelection current)
    {
        var words=input.ToLowerInvariant().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        if(words.Length is <1 or >3 || words[0] is not ("current" or "all"))throw new InvalidDataException(Help);
        var result=new VariantSelection{AutoBurst=current.AutoBurst,AutoMitigation=current.AutoMitigation,AutoMechanics=current.AutoMechanics};
        if(words.Length==1)return new(words[0]=="all",result);
        var operation=words.Length==2?"set":words[1];
        var flags=words[^1];
        if(words.Length==3 && operation=="set" || operation is not ("set" or "on" or "off" or "toggle") ||
           (flags!="none" && (flags.Length==0 || flags.Any(c=>!"mbj".Contains(c)) || flags.Distinct().Count()!=flags.Length)) ||
           (flags=="none" && operation!="set"))throw new InvalidDataException(Help);
        bool Value(char flag,bool old)=>operation=="set"?flags!="none"&&flags.Contains(flag):!flags.Contains(flag)?old:operation=="on"||operation=="toggle"&&!old;
        result.AutoMitigation=Value('m',current.AutoMitigation);result.AutoBurst=Value('b',current.AutoBurst);result.AutoMechanics=Value('j',current.AutoMechanics);
        return new(words[0]=="all",result);
    }
}
