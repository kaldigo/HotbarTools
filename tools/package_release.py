"""Package standalone plugin ZIPs and a Dalamud custom repository feed."""
import argparse,hashlib,json,zipfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--repository',required=True);p.add_argument('--tag',required=True);a=p.parse_args()
root=Path(__file__).resolve().parents[1];out=root/'artifacts';out.mkdir(exist_ok=True)
feed=[];hashes=[]
for folder,name in [('hotbar-sync','HotbarBridge'),('job-setup','JobSetup')]:
 build=root/f'{folder}/{name}/bin/Release'
 manifest=json.loads((build/f'{name}.json').read_text(encoding='utf-8-sig'))
 assert manifest['DalamudApiLevel']==15
 assert manifest['InternalName']==name
 manifest['RepoUrl']=f'https://github.com/{a.repository}'
 manifest['Author']='kaldigo'
 zip_path=out/f'{name}.zip'
 with zipfile.ZipFile(zip_path,'w',zipfile.ZIP_DEFLATED) as z:
  z.write(build/f'{name}.dll',f'{name}.dll')
  z.writestr(f'{name}.json',json.dumps(manifest,indent=2)+'\n')
  if (build/f'{name}.deps.json').exists():z.write(build/f'{name}.deps.json',f'{name}.deps.json')
  z.write(root/'LICENSE','LICENSE')
 url=f'https://github.com/{a.repository}/releases/download/{a.tag}/{name}.zip'
 manifest.update(DownloadLinkInstall=url,DownloadLinkUpdate=url,DownloadLinkTesting=url,Changelog='Initial test release. Review previews before applying; disable Wrath before settings writes.')
 feed.append(manifest);hashes.append(f'{hashlib.sha256(zip_path.read_bytes()).hexdigest()}  {zip_path.name}')
text=json.dumps(feed,indent=2)+'\n';(root/'pluginmaster.json').write_text(text);(out/'pluginmaster.json').write_text(text)
(out/'SHA256SUMS.txt').write_text('\n'.join(hashes)+'\n')
print('Packaged:',', '.join(x['InternalName'] for x in feed))
