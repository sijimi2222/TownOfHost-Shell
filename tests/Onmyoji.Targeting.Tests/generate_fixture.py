# Manual regeneration only. Tests compare each compiled slice to its production source.
from pathlib import Path
import hashlib,json,re
root=Path(__file__).resolve().parents[2]
folder=Path(__file__).resolve().parent
source=(root/'Roles/Neutral/Onmyoji.cs').read_text(encoding='utf-8-sig')
methods=['IsOnmyojiKillerTarget','IsStarReadingTarget','IsValidShikigamiTarget','OnCheckMurderAsKiller','AddShikigami','CheckVoteAsVoter','RefreshStarReadingTargets','GetMark']
def body(text,name):
 match=re.search(r'^    (?:public override |public |private |static )*(?:bool|string|void) '+name+r'\(',text,re.M)
 assert match,name
 first=match.start();brace=text.index('{',match.end());depth=0
 for last in range(brace,len(text)):
  if text[last]=='{':depth+=1
  if text[last]=='}':
   depth-=1
   if depth==0:return text[first:last+1]
 raise Exception(name)
parts={name:body(source,name) for name in methods}
header='// Generated from production Onmyoji.cs. Regenerate with generate_fixture.py after reviewed source changes.\nusing TownOfHost;\nusing static TownOfHost.PlayerCatch;\nusing static TownOfHost.Utils;\nusing static TownOfHost.Translator;\nusing static TownOfHost.Modules.SelfVoteManager;\nnamespace TownOfHost.Roles.Neutral;\npublic partial class Onmyoji : TestRoleBase {\n'
(folder/'GeneratedTargeting.cs').write_text(header+'\n\n'.join(parts.values())+'\n}\n',encoding='utf-8')
(folder/'FixtureHashes.json').write_text(json.dumps({n:hashlib.sha256(x.encode()).hexdigest().upper() for n,x in parts.items()},indent=2))
enum=(root/'Roles/Core/CustomRoleManager.cs').read_text(encoding='utf-8-sig');first=enum.index('public enum CustomRoles');brace=enum.index('{',first);last=enum.index('}',brace);text=enum[first:last+1]
(folder/'GeneratedRoles.cs').write_text('// Actual numeric enum; no product IDs are modified.\nnamespace TownOfHost;\n'+text+'\n',encoding='utf-8')
