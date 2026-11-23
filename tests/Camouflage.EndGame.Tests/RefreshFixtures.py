from pathlib import Path
import json,re,hashlib
T=Path(__file__).resolve().parent;R=T.parents[1]
flow=(R/'Patches/CheckGameEndPatch.cs').read_text(encoding='utf-8-sig');prefix=flow[flow.index('        public static bool Prefix()'):flow.index('        public static void StartEndGame')].strip();co=flow[flow.index('        public static void StartEndGame'):flow.index('        private static void SetRoleSummaryText')].strip()
normal=(R/'Patches/GameEndPredicate/NormalGameEndPredicate.cs').read_text(encoding='utf-8-sig');normal=normal[normal.index('*/')+2:];end=normal.index('//インポスター勝利');begin=normal.rfind('else if (',0,end);condition=normal[begin:end].strip().removeprefix('else ')
template='''// Extracted production methods; source hash is checked at runtime.
using System;using System.Collections;using System.Collections.Generic;using System.Linq;
using AmongUs.GameOptions;using TownOfHost.Roles.Core;using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;using TownOfHost.Roles.Neutral;using UnityEngine;
namespace TownOfHost {
internal static class GameEndChecker {
static GameEndPredicate predicate;static bool _isEndProcessing;const float EndGameDelay=.2f;
internal static void SetPredicate(GameEndPredicate p){predicate=p;_isEndProcessing=false;}
static void EnsurePredicate()=>predicate=new NormalGameEndPredicate();
static void SetRoleSummaryText(){Probe.Summaries++;}
PREFIX
COROUTINE
}
class NormalGameEndPredicate:GameEndPredicate {
public override bool CheckForEndGame(out GameOverReason reason){
reason=GameOverReason.ImpostorsByKill;if(CustomWinnerHolder.WinnerTeam!=CustomWinner.Default)return false;
int Imp=PlayerCatch.AllAlivePlayerControls.Count(p=>p.Team==CustomRoleTypes.Impostor),FoxAndCrew=PlayerCatch.AllAlivePlayerControls.Count(p=>p.Team==CustomRoleTypes.Crewmate);
int Jackal=0,Remotekiller=0,MilkyWay=0,MadBetrayer=0,Pavlov=0,StandMasterCount=0,EaterCount=0,DaoshiAlive=0,LessThanAlive=0;
if(Imp+FoxAndCrew==0)return false;
CONDITION {CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);return true;}return false;
}}
}
'''.replace('PREFIX',prefix).replace('COROUTINE',co).replace('CONDITION',condition)
(T/'GeneratedFlow.cs').write_text(template,encoding='utf-8')
for name,expr in [('CustomRoles',r'CustomRoles\.(\w+)'),('CustomWinner',r'CustomWinner\.(\w+)'),('CustomDeathReason',r'CustomDeathReason\.(\w+)')]:
 names=sorted(set(re.findall(expr,template))|({'Crewmate','Impostor','Default','None','Special'} if name in ['CustomRoles','CustomWinner'] else {'Kill','Suicide'}))
 if name=='CustomWinner':names.remove('Default');names=['Default']+names
 path=T/(name+'.cs');path.write_text('namespace TownOfHost;public enum '+name+' {'+','.join(x+'='+str(i) for i,x in enumerate(names))+'}\n',encoding='utf-8')
hashes=dict(prefix=hashlib.sha256(prefix.encode()).hexdigest().upper(),coroutine=hashlib.sha256(co.encode()).hexdigest().upper(),population_condition=hashlib.sha256(condition.encode()).hexdigest().upper())
(T/'GeneratedHashes.cs').write_text('static class GeneratedHashes {'+''.join('public const string '+k+'="'+v+'";' for k,v in hashes.items())+'}',encoding='utf-8')
(T/'Camouflage.EndGame.Tests.csproj').write_text('''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net6.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable><CheckEolTargetFramework>false</CheckEolTargetFramework></PropertyGroup><ItemGroup><Compile Include="../../Modules/Camouflague.cs" Link="Camouflague.cs"/></ItemGroup></Project>''',encoding='utf-8')
print('GENERATED_FLOW',hashes)
