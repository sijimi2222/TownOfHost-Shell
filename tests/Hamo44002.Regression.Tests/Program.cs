using System.Reflection;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using UnityEngine;
static class Program
{
 static int passed,failed;
 static void Test(string name,Action test){Reset();try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+" "+e);}}
 static void Check(bool value,string why="assertion"){if(!value)throw new Exception(why);}
 static void Eq<T>(T expected,T actual){if(!Equals(expected,actual))throw new Exception($"expected {expected}, got {actual}");}
 static void Reset(){OptionItem.All.Clear();Madpsycho.RoleInfo.Setup();DoubleKiller.RoleInfo.Setup();HateKiller.RoleInfo.Setup();Hunter.RoleInfo.Setup();CustomRoleManager.AllActiveRoles.Clear();PlayerCatch.All.Clear();PlayerState.Reset();TownOfHost.Main.AllPlayerSpeed.Clear();LateTask.Jobs.Clear();CustomRoleManager.Accept=true;CustomRoleManager.Throw=false;CustomRoleManager.Validate=true;CustomRoleManager.Attempts=CustomRoleManager.DirectMurders=0;CustomRoleManager.Observe=null;CustomRoleManager.LastInfo=null;AmongUsClient.Instance=new();GameStates.IsInTask=true;GameStates.IsMeeting=GameStates.CalledMeeting=false;MeetingHud.Instance=null;Options.CurrentGameMode=CustomGameMode.Standard;Options.firstturnmeeting=Options.IsStandardHAS=MeetingStates.FirstMeeting=false;Options.HideAndSeekKillDelayTimer=0;TownOfHost.Main.LagTime=0;CheckMurderPatch.TimeSinceLastKill.Clear();SuddenDeathMode.NowSuddenDeathMode=false;RPC.SoundsPlayed=0;Utils.Messages.Clear();Time.fixedDeltaTime=.02f;Time.time=0;ShipStatus.Instance=null;GameOptionsManager.Instance=new();PlayerControl.LocalPlayer=null;Hunter.achievements.Clear();Hunter.Load();Hunter.ResetForGame();VentLock.Reset();VentLock.ResendAll=false;UpdatePatch.Logger=null;Hazel.MessageWriter.Created.Clear();}
 static PlayerControl P(byte id,CustomRoles role=CustomRoles.Crewmate,float x=0){var p=new PlayerControl{PlayerId=id,OwnerId=id+100,Role=role};p.transform.position=new Vector3(x,0);PlayerCatch.All.Add(p);return p;}
 static Madpsycho Mad(byte id=1,int completed=1){var r=new Madpsycho(P(id,CustomRoles.Madpsycho));r.MyTaskState.Completed=completed;return r;}
 static DoubleKiller Dk(byte id=1){var r=new DoubleKiller(P(id,CustomRoles.DoubleKiller));r.Player.killTimer=7;return r;}
 static void Click(DoubleKiller r){bool adjust=true;bool? reset=true;((IUsePhantomButton)r).OnClick(ref adjust,ref reset);Check(!adjust&&reset==false,"secondary kill must not reset ordinary timer via shared interface");}
 static void Vote(HateKiller role,int count){NetworkedPlayerInfo exile=null;bool tie=false;role.VotingResults(ref exile,ref tie,new(){{role.Player.PlayerId,count}},Array.Empty<byte>(),false);}
 static Hunter Hunt(byte id=1)=>new(P(id,CustomRoles.Hunter));
 static void Target(Hunter r,PlayerControl p){bool animate=true;r.CheckShapeshift(p,ref animate);Check(!animate);}
 static void Main()
 {
  Test("Mad legacy ConfigId",()=>Eq(22800,Madpsycho.RoleInfo.ConfigId));
  foreach(var id in new[]{22809,22810,22811})Test("Mad legacy detail "+id,()=>Check(OptionItem.All.ContainsKey(id)));
  Test("Mad does not register upstream 21500",()=>Check(!OptionItem.All.Keys.Any(id=>id>=21500&&id<21600)));
  Test("Mad recomputes task presence",()=>Eq(HasTask.ForRecompute,Mad().TaskMode()));
  foreach(var (required,completed,can) in new[]{(0,0,true),(1,0,false),(1,1,true),(3,2,false),(3,3,true),(99,98,false),(99,99,true)})Test($"Mad tasks {completed}/{required}",()=>{OptionItem.All[22811].Value=required;Eq(can,Mad(completed:completed).CanPsycho());});
  Test("Mad owner task states independent",()=>{var a=Mad(1,0);var b=Mad(2,1);Check(!a.CanPsycho()&&b.CanPsycho());});
  Test("Mad unarmed does not absorb ordinary kill",()=>{var m=Mad(completed:0);Check(m.OnCheckMurderAsTarget(new(){AttemptKiller=P(2,CustomRoles.DoubleKiller),AttemptTarget=m.Player}));Eq(0,CustomRoleManager.Attempts);});
  Test("Mad armed reflects normal attack once",()=>{var m=Mad();var k=P(2,CustomRoles.DoubleKiller);Check(!m.OnCheckMurderAsTarget(new(){AttemptKiller=k,AttemptTarget=m.Player}));Check(!k.IsAlive()&&m.Player.IsAlive());Eq(1,CustomRoleManager.Attempts);Eq(CustomDeathReason.Counter,PlayerState.GetByPlayerId(2).DeathReason);});
  Test("Mad forged normal murder denied",()=>{var m=Mad();Check(!CheckMurderPatch.CheckForInvalidMurdering(new(){AttemptKiller=m.Player,AttemptTarget=P(2)},true));});
  Test("Mad forged power10 denied",()=>{var m=Mad();Check(!CheckMurderPatch.CheckForInvalidMurdering(new(){AttemptKiller=m.Player,AttemptTarget=P(2),KillPower=10},true));});
  Test("Mad ticket scoped to designated target",()=>{var m=Mad();var k=P(2,CustomRoles.DoubleKiller);var other=P(3);bool observed=false;CustomRoleManager.Observe=i=>{observed=true;Check(Madpsycho.IsReflectionAttempt(i));Check(!Madpsycho.IsReflectionAttempt(new(){AttemptKiller=m.Player,AttemptTarget=other,KillPower=10}));};m.TryPsycho(k,1);Check(observed);Check(!Madpsycho.IsReflectionAttempt(CustomRoleManager.LastInfo));});
  Test("Mad ticket cleared on exception",()=>{var m=Mad();var k=P(2,CustomRoles.DoubleKiller);CustomRoleManager.Throw=true;try{m.TryPsycho(k,1);}catch(InvalidOperationException){}Check(!Madpsycho.IsReflectionAttempt(new(){AttemptKiller=m.Player,AttemptTarget=k,KillPower=10}));Eq(CustomDeathReason.Kill,PlayerState.GetByPlayerId(2).DeathReason);});
  foreach(int power in new[]{2,3,10})Test("Mad does not reflect power "+power,()=>Check(!Mad().TryPsycho(P(2,CustomRoles.DoubleKiller),power)));
  Test("Mad guard rejection restores death reason",()=>{var m=Mad();var k=P(2,CustomRoles.DoubleKiller);k.Guarded=true;Check(m.TryPsycho(k,1));Check(k.IsAlive());Eq(CustomDeathReason.Kill,PlayerState.GetByPlayerId(2).DeathReason);});
  Test("Mad cannot reflect itself",()=>{var m=Mad();Check(!m.TryPsycho(m.Player,1));});
  Test("Mad nonhost rejected",()=>{var m=Mad();AmongUsClient.Instance.AmHost=false;Check(!m.TryPsycho(P(2),1));Eq(0,CustomRoleManager.Attempts);});
  Test("Mad dead owner rejected",()=>{var m=Mad();m.Player.Data.IsDead=true;Check(!m.TryPsycho(P(2),1));});
  Test("Mad disconnected attacker rejected",()=>{var m=Mad();var k=P(2);k.Data.Disconnected=true;Check(!m.TryPsycho(k,1));});
  Test("Mad disposed rejected",()=>{var m=Mad();m.OnDestroy();Check(!m.TryPsycho(P(2),1));});
  Test("Mad replaced instance rejected",()=>{var m=Mad();_ = new Madpsycho(m.Player);Check(!m.TryPsycho(P(2),1));});
  Test("Mad meeting task path rejected",()=>{var m=Mad();GameStates.IsMeeting=true;Check(!m.TryPsycho(P(2),1));});
  Test("Mad meeting route must really be meeting",()=>Check(!Mad().TryPsycho(P(2),1,true)));
  Test("Mad meeting reflection uses established RPC",()=>{var m=Mad();GameStates.IsMeeting=true;MeetingHud.Instance=new();var k=P(2);Check(m.TryPsycho(k,1,true));Eq(1,CustomRoleManager.DirectMurders);Eq(0,CustomRoleManager.Attempts);Check(!k.IsAlive());});
  Test("Mad meeting prevented reflection retains reason",()=>{var m=Mad();GameStates.IsMeeting=true;var k=P(2);k.Guarded=true;m.TryPsycho(k,1,true);Check(k.IsAlive());Eq(CustomDeathReason.Kill,PlayerState.GetByPlayerId(2).DeathReason);});
  Test("Double legacy Config and defaults",()=>{Eq(3400,DoubleKiller.RoleInfo.ConfigId);Eq(30f,OptionItem.All[3410].Default);Eq(30f,OptionItem.All[3411].Default);Eq(1f,OptionItem.All[3414].Default);Eq(14,((IntRule)OptionItem.All[3414].Rule).Max);});
  foreach(bool enabled in new[]{false,true})Test("Double vent/sabotage positive flags "+enabled,()=>{OptionItem.All[3412].Value=OptionItem.All[3413].Value=enabled?1:0;var d=Dk();Eq(enabled,d.CanUseImpostorVentButton());Eq(enabled,d.CanUseSabotageButton());Eq(enabled,d.CanClickUseVentButton);Eq(enabled,d.OnEnterVent(null,0));Eq(enabled,d.CanVentMoving(null,0));});
  Test("Double ordinary kill retained",()=>{var d=Dk();var target=P(2,CustomRoles.Crewmate,.1f);Check(CustomRoleManager.OnCheckMurder(d.Player,target,d.Player,target));Check(!target.IsAlive());});
  Test("Double no target no consumption",()=>{var d=Dk();Click(d);Check(((IUsePhantomButton)d).IsPhantomRole);Eq(0,LateTask.Jobs.Count);});
  Test("Double out of range no consumption",()=>{var d=Dk();P(2,CustomRoles.Crewmate,3);Click(d);Eq(0,CustomRoleManager.Attempts);Check(((IUsePhantomButton)d).IsPhantomRole);});
  Test("Double accepted press consumes exactly once",()=>{OptionItem.All[3414].Value=2;var d=Dk();P(2,CustomRoles.Crewmate,.1f);Click(d);Check(d.GetProgressText().Contains("(1)"));Click(d);Eq(1,CustomRoleManager.Attempts);Eq(1,LateTask.Jobs.Count);});
  Test("Double max cap stops further use",()=>{var d=Dk();P(2,CustomRoles.Crewmate,.1f);Click(d);P(3,CustomRoles.Crewmate,.1f);for(int i=0;i<100;i++)d.OnFixedUpdate(d.Player);Click(d);Eq(1,CustomRoleManager.Attempts);Check(!((IUsePhantomButton)d).IsPhantomRole);});
  Test("Double fixed updates do not enqueue tasks",()=>{OptionItem.All[3414].Value=2;var d=Dk();P(2,CustomRoles.Crewmate,.1f);Click(d);for(int i=0;i<500;i++)d.OnFixedUpdate(d.Player);Eq(1,LateTask.Jobs.Count);Check(d.CanSubkill);});
  Test("Double restores ordinary cooldown once",()=>{var d=Dk();P(2,CustomRoles.Crewmate,.1f);Click(d);LateTask.Drain();Eq(7f,d.Player.killTimer);LateTask.Drain();Eq(7f,d.Player.killTimer);});
  foreach(string stop in new[]{"meeting","report","destroy","dead","replace"})Test("Double no stale cooldown after "+stop,()=>{var d=Dk();P(2,CustomRoles.Crewmate,.1f);Click(d);if(stop=="meeting")d.OnStartMeeting();if(stop=="report")d.OnReportDeadBody(null,null);if(stop=="destroy")d.OnDestroy();if(stop=="dead")d.Player.Data.IsDead=true;if(stop=="replace")_ =new DoubleKiller(d.Player);d.Player.killTimer=19;LateTask.Drain();Eq(19f,d.Player.killTimer);});
  Test("Double guards prevent teleport and sound",()=>{var d=Dk();P(2,CustomRoles.Crewmate,.1f).Guarded=true;Click(d);Eq(0,d.Player.Teleports);Eq(0,RPC.SoundsPlayed);});
  Test("Double successful kill teleports",()=>{var d=Dk();P(2,CustomRoles.Crewmate,.5f);Click(d);Eq(1,d.Player.Teleports);Eq(1,RPC.SoundsPlayed);});
  Test("Double excludes impostors normally",()=>{var d=Dk();P(2,CustomRoles.DoubleKiller,.1f);Click(d);Eq(0,CustomRoleManager.Attempts);});
  Test("Double sudden death permits impostor target",()=>{var d=Dk();P(2,CustomRoles.DoubleKiller,.1f);SuddenDeathMode.NowSuddenDeathMode=true;Click(d);Eq(1,CustomRoleManager.Attempts);});
  Test("Double skips dead and disconnected",()=>{var d=Dk();P(2,CustomRoles.Crewmate,.1f).Data.IsDead=true;P(3,CustomRoles.Crewmate,.1f).Data.Disconnected=true;Click(d);Eq(0,CustomRoleManager.Attempts);});
  Test("Double unarmed Mad ability proceeds",()=>{var d=Dk();var m=Mad(2,0);m.Player.transform.position=new Vector3(.1f,0);Click(d);Check(!m.Player.IsAlive()&&d.Player.IsAlive());});
  Test("Double armed Mad reflects once",()=>{var d=Dk();var m=Mad(2,1);m.Player.transform.position=new Vector3(.1f,0);Click(d);Check(m.Player.IsAlive()&&!d.Player.IsAlive());Eq(1,CustomRoleManager.Attempts);});
  Test("Double host ignores role RPC",()=>{var d=Dk();d.ReceiveRPC(new Hazel.MessageReader(0,14));Check(d.CanSubkill&&((IUsePhantomButton)d).IsPhantomRole);});
  Test("Double client accepts upstream readiness only",()=>{var d=Dk();AmongUsClient.Instance.AmHost=false;d.ReceiveRPC(new Hazel.MessageReader(0));Check(!d.CanSubkill);});
  Test("Double client accepts count payload",()=>{OptionItem.All[3414].Value=3;var d=Dk();AmongUsClient.Instance.AmHost=false;d.ReceiveRPC(new Hazel.MessageReader(1,2));Check(d.GetProgressText().Contains("(1)"));});
  Test("Double client count clamped",()=>{var d=Dk();AmongUsClient.Instance.AmHost=false;d.ReceiveRPC(new Hazel.MessageReader(1,255));Check(!((IUsePhantomButton)d).IsPhantomRole);});
  Test("Double nonhost cannot activate",()=>{var d=Dk();P(2,CustomRoles.Crewmate,.1f);AmongUsClient.Instance.AmHost=false;Click(d);Eq(0,CustomRoleManager.Attempts);});
  Test("Double meeting cannot activate",()=>{var d=Dk();P(2,CustomRoles.Crewmate,.1f);GameStates.IsMeeting=true;Click(d);Eq(0,CustomRoleManager.Attempts);});
  Test("Hate counters independent",()=>{var a=new HateKiller(P(1,CustomRoles.HateKiller));var b=new HateKiller(P(2,CustomRoles.HateKiller));a.AddImpVote();Vote(a,1);Vote(b,1);Eq(35f,a.CalculateKillCooldown());Eq(31f,b.CalculateKillCooldown());});
  Test("Hate self vote excluded",()=>{var a=new HateKiller(P(1,CustomRoles.HateKiller));a.AddSelfVote();Vote(a,1);Eq(35f,a.CalculateKillCooldown());});
  Test("Hate configured self and imp votes count",()=>{OptionItem.All[127013].Value=OptionItem.All[127014].Value=1;var a=new HateKiller(P(1,CustomRoles.HateKiller));a.AddSelfVote();a.AddImpVote();Vote(a,2);Eq(27f,a.CalculateKillCooldown());});
  Test("Hate zero eligible votes retains earned cooldown",()=>{var a=new HateKiller(P(1,CustomRoles.HateKiller));Vote(a,2);Eq(27f,a.CalculateKillCooldown());Vote(a,0);Eq(27f,a.CalculateKillCooldown());});
  Test("Hate lower bound",()=>{var a=new HateKiller(P(1,CustomRoles.HateKiller));Vote(a,99);Eq(15f,a.CalculateKillCooldown());});
  Test("Hate weighted impostor votes excluded by final weight",()=>{var a=new HateKiller(P(1,CustomRoles.HateKiller));a.AddImpVote(3);Vote(a,4);Eq(31f,a.CalculateKillCooldown());});
  Test("Hate Marshall zero-power vote subtracts zero",()=>{var a=new HateKiller(P(1,CustomRoles.HateKiller));a.AddImpVote(0);Vote(a,1);Eq(31f,a.CalculateKillCooldown());});
  Test("Hate negative exclusion cannot create votes",()=>{var a=new HateKiller(P(1,CustomRoles.HateKiller));a.AddSelfVote(-2);Vote(a,1);Eq(31f,a.CalculateKillCooldown());});
  Test("Hate repeat tally resets exclusion before recount",()=>{var a=new HateKiller(P(1,CustomRoles.HateKiller));a.AddImpVote(2);a.AddSelfVote();a.ResetVoteCounts();a.AddImpVote(2);Vote(a,3);Eq(31f,a.CalculateKillCooldown());});
  Test("Hate meeting clears only owner's counters",()=>{var a=new HateKiller(P(1,CustomRoles.HateKiller));var b=new HateKiller(P(2,CustomRoles.HateKiller));a.AddImpVote();b.AddImpVote();a.AfterMeetingTasks();Vote(a,1);Vote(b,1);Eq(31f,a.CalculateKillCooldown());Eq(35f,b.CalculateKillCooldown());});
  Test("Hunter multiple assignment limit enabled",()=>Eq(15,Hunter.RoleInfo.AssignInfo.AssignCountRule.Max));
  Test("Hunter color is per owner",()=>{var a=Hunt(1);var b=Hunt(2);a.PublicRoleColor=true;Check(Hunter.KnowTargetRoleColor(a.Player,false));Check(!Hunter.KnowTargetRoleColor(b.Player,false));Check(!Hunter.KnowTargetRoleColor(a.Player,true));});
  Test("Hunter idle no lookup or murder",()=>{var h=Hunt();h.OnFixedUpdate(h.Player);Eq(0,CustomRoleManager.Attempts);});
  foreach(string state in new[]{"dead","disconnected","nonhost"})Test("Hunter rejects chase "+state,()=>{var h=Hunt();var p=P(2);Target(h,p);if(state=="dead")p.Data.IsDead=true;if(state=="disconnected")p.Data.Disconnected=true;if(state=="nonhost")AmongUsClient.Instance.AmHost=false;h.OnFixedUpdate(h.Player);Eq(0,CustomRoleManager.Attempts);});
  Test("Hunter range respected",()=>{var h=Hunt();Target(h,P(2,CustomRoles.Crewmate,6));h.OnFixedUpdate(h.Player);Eq(0,CustomRoleManager.Attempts);});
  Test("Hunter successful chase uses shared murder",()=>{var h=Hunt();var target=P(2,CustomRoles.Crewmate,1);Target(h,target);h.OnFixedUpdate(h.Player);Eq(1,CustomRoleManager.Attempts);Check(!target.IsAlive());Eq(1,h.Player.Teleports);Eq(1,RPC.SoundsPlayed);});
  Test("Hunter rejected murder does not teleport",()=>{var h=Hunt();var target=P(2,CustomRoles.Crewmate,1);target.Guarded=true;Target(h,target);h.OnFixedUpdate(h.Player);Eq(0,h.Player.Teleports);Eq(0,RPC.SoundsPlayed);});
  Test("Hunter meeting clears pending target",()=>{var h=Hunt();Target(h,P(2));h.AfterMeetingTasks();h.OnFixedUpdate(h.Player);Eq(0,CustomRoleManager.Attempts);});
  Test("Hunter attack participates in defensive hooks without kill button",()=>{var h=Hunt();Check(((IKiller)h).IsKiller);Check(!((IKiller)h).CanKill);});
  Test("Hunter rejected attack grants no boost or blackout",()=>{var h=Hunt();var target=P(2,CustomRoles.Crewmate,1);target.Guarded=true;var bystander=P(3);Target(h,target);h.OnFixedUpdate(h.Player);Check(!h.PublicRoleColor);Check(!TownOfHost.Main.AllPlayerSpeed.ContainsKey(1));Check(!PlayerState.GetByPlayerId(bystander.PlayerId).IsBlackOut);Eq(0,LateTask.Jobs.Count);});
  Test("Hunter null selection is safe",()=>{var h=Hunt();Target(h,null);h.OnFixedUpdate(h.Player);Eq(0,CustomRoleManager.Attempts);Check(!Hunter.KnowTargetRoleColor(null,false));});
  Test("Hunter self selection is rejected",()=>{var h=Hunt();Target(h,h.Player);h.OnFixedUpdate(h.Player);Eq(0,CustomRoleManager.Attempts);});
  foreach(string stop in new[]{"meeting","report","destroy","dead"})Test("Hunter restores effects and cancels stale callbacks on "+stop,()=>{var h=Hunt();TownOfHost.Main.AllPlayerSpeed[1]=1.25f;var bystander=P(3,CustomRoles.Crewmate,.2f);Target(h,P(2,CustomRoles.Crewmate,1));h.OnFixedUpdate(h.Player);Check(h.PublicRoleColor&&PlayerState.GetByPlayerId(3).IsBlackOut);if(stop=="meeting")h.OnStartMeeting();if(stop=="report")h.OnReportDeadBody(null,null);if(stop=="destroy")h.OnDestroy();if(stop=="dead"){h.Player.Data.IsDead=true;h.OnFixedUpdate(h.Player);}Eq(1.25f,TownOfHost.Main.AllPlayerSpeed[1]);Check(!h.PublicRoleColor&&!PlayerState.GetByPlayerId(3).IsBlackOut);int resets=h.Player.ResetCount;LateTask.Drain();Eq(resets,h.Player.ResetCount);Eq(1.25f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("Vent upstream null logger masks ship-null failure",()=>{var p=P(1);p.AmOwner=true;PlayerControl.LocalPlayer=p;bool threw=false;try{UpdatePatch.Postfix(p);}catch(NullReferenceException){threw=true;}Check(threw,"upstream null Logger defect was not reproduced");});
  Test("Vent upstream local movement restriction has no host gate",()=>{AmongUsClient.Instance.AmHost=false;var a=new Vent{Id=6};bool result=true;Check(!MovePatch.Prefix(a,new Vent{Id=7},ref result));Check(!result);});
  Test("Vent upstream blocked pairs bidirectional",()=>{Check(VentLock.IsBlocked(6,7));Check(VentLock.IsBlocked(7,6));Check(!VentLock.IsBlocked(6,1));});
  Test("Vent fake cleaning packet leaves host dictionary intact",()=>{var p=P(2);ShipStatus.Instance=new();var sys=new VentilationSystem();sys.PlayersInsideVents[2]=6;sys.PlayersCleaningVents[3]=1;ShipStatus.Instance.Systems[SystemTypes.Ventilation]=sys;ShipStatus.Instance.AllVents=new[]{new Vent{Id=6,NearbyVents=new[]{new Vent{Id=7},new Vent{Id=5}}}};VentLock.Update();Eq(1,sys.PlayersCleaningVents.Count);Eq(1,AmongUsClient.Instance.Packets.Count);Check(AmongUsClient.Instance.Packets[0].Contains((byte)250));Check(Hazel.MessageWriter.Created.All(w=>w.Recycled));});
  Test("Vent repeated same state bounded by refresh",()=>{P(2);ShipStatus.Instance=new();var sys=new VentilationSystem();sys.PlayersInsideVents[2]=6;ShipStatus.Instance.Systems[SystemTypes.Ventilation]=sys;ShipStatus.Instance.AllVents=new[]{new Vent{Id=6,NearbyVents=new[]{new Vent{Id=7}}}};VentLock.Update();for(int i=0;i<100;i++)VentLock.Update();Eq(1,AmongUsClient.Instance.Packets.Count);Time.time=1.1f;VentLock.Update();Eq(2,AmongUsClient.Instance.Packets.Count);});
  Console.WriteLine($"HAMO44002_RESULT passed={passed} failed={failed}");Environment.ExitCode=failed==0?0:1;
 }
}
