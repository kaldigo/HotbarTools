using System.Text.Json.Nodes;
using HotbarTools;
if(args.Length!=1)throw new ArgumentException("Pass the installed game's sqpack directory.");
using var game=new Lumina.GameData(args[0]);
var catalog=new ActionCatalog(game.Excel.GetSheet<Lumina.Excel.Sheets.Action>(Lumina.Data.Language.English));
var pack=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"presets.json")));
var count=0;
foreach(var job in pack!["Jobs"]!.AsArray())
foreach(var slot in job!["Slots"]!.AsArray())
{
 var name=slot!["Action"]!.GetValue<string>();
 if(name=="Empty")continue;
 catalog.Resolve(job!["Job"]!.GetValue<string>(),name);count++;
}
foreach(var (job,name,id) in new[]{("GNB","Provoke",7533u),("WHM","Swiftcast",7561u),("WHM","Surecast",7559u),("SMN","Outburst",16511u)})
 if(catalog.Resolve(job,name)!=id)throw new Exception("Obsolete/NPC action collision: "+name);
Console.WriteLine($"{count} preset assignments uniquely resolved; four obsolete/NPC regressions pass.");
