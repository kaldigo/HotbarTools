"""Regenerate live command allowlist from reviewed variants and pinned public metadata."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
rules={int(k):v for k,v in json.loads((root/'job-setup/preset-rules.json').read_text()).items()}
variants=[json.loads(p.read_text(encoding='utf-8-sig')) for p in (root/'job-setup/variants').glob('*.json')]
allowed={i for d in variants for v in d.values() if isinstance(v,dict) for k in ['Enable','Disable'] for i in v.get(k,[])}
assert allowed <= rules.keys()
lines=['    // Generated from pinned Wrath preset metadata; only reviewed variant deltas are writable.','    public static readonly Dictionary<int,string> Supported=new()','    {']
lines += [f'        [{i}]="{rules[i]["Name"]}",' for i in sorted(allowed)]
lines += ['    };','    public static readonly Dictionary<int,int[]> Parents=new()','    {']+[f'        [{i}]=[{",".join(map(str,rules[i]["Parents"]))}],' for i in sorted(rules)]+['    };','    public static readonly Dictionary<int,int[]> Conflicts=new()','    {']+[f'        [{i}]=[{",".join(map(str,rules[i]["Conflicts"]))}],' for i in sorted(rules) if rules[i]['Conflicts']]+['    };','    private static int Depth(int id)=>Parents.GetValueOrDefault(id,[]).Select(p=>1+Depth(p)).DefaultIfEmpty(0).Max();','']
p=root/'common/Models.cs';s=p.read_text();start=s.index('    // Generated from pinned Wrath');end=s.index('    public static Dictionary<int,bool>? Create',start)
p.write_text(s[:start]+'\n'.join(lines)+s[end:])
print(f'Generated {len(allowed)} reviewed live preset IDs')
