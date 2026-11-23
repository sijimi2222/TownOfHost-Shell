using System.Reflection;
using System.Text;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;
using UnityEngine;

static class Program
{
 static int passed,failed,epoch=100;
 static readonly SimpleRoleInfo[] infos={Slugger.RoleInfo,Pusher.RoleInfo,Gimlet.RoleInfo,Cleaner.RoleInfo,Informant.RoleInfo,LifeSaver.RoleInfo,Bloodlust.RoleInfo,EvilButtoner.RoleInfo};
 static void Check(bool b,string m="assertion failed"){if(!b)throw new Exception(m);}
 static void Eq<T>(T expected,T actual){if(!Equals(expected,actual))throw new Exception($"expected {expected}, actual {actual}");}
 static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
 static void Test(string n,Action action){Reset();try{action();passed++;Console.WriteLine("PASS "+n);}catch(Exception e){failed++;Console.WriteLine("FAIL "+n+": "+e);}}
 static void Reset()
 {
  AmongUsClient.Instance=new(){GameId=++epoch};AmongUsClient.Calls.Clear();
  GameStates.InGame=GameStates.IsInTask=true;GameStates.Intro=GameStates.IsEnded=GameStates.IsMeeting=GameStates.CalledMeeting=false;
  MeetingHud.Instance=null;CustomWinnerHolder.WinnerTeam=CustomWinner.Default;CustomWinnerHolder.CantWinPlayerIds.Clear();
  PlayerCatch.All.Clear();CustomRoleManager.AllActiveRoles.Clear();CustomRoleManager.MarkOthers.Clear();
  TownOfHost.Main.AllPlayerSpeed.Clear();TownOfHost.Main.NormalOptions=new();TownOfHost.Main.NormalOptions.MapId=4;
  CustomRoleManager.Attempts=CustomRoleManager.CompletedNoBody=0;CustomRoleManager.Accept=true;CustomRoleManager.Throw=false;CustomRoleManager.Validate=false;
  CustomRoleManager.LastInfo=null;CustomRoleManager.Observe=null;PlayerState.Reset();
  Time.time=0;Time.fixedDeltaTime=.02f;LateTask.Jobs.Clear();CustomNetObject.Reset();Object.Items.Clear();
  PhysicsHelpers.Block=null;Physics2D.HitAt=null;TownOfHost.Roles.Madmate.MadCleaner.Cleaned.Clear();DeadBodyEraser.Init();
  ShipStatus.Instance=null;Sender.Packets.Clear();ReportDeadBodyPatch.Meetings=0;UtilsNotifyRoles.Calls=0;
  OptionItem.All.Clear();OptionItem.Registrations.Clear();foreach(var info in infos)info.Setup();TownOfHost.Roles.AddOns.Common.Notice.SetupCustomOption();MeetingHudPatch.StartPatch.meetingsends.Clear();Utils.Messages.Clear();
  PlayerControl.LocalPlayer=P(0,CustomRoles.Impostor,50,50);
 }
 static PlayerControl P(byte id,CustomRoles r=CustomRoles.Crewmate,float x=0,float y=0)
 {
  var p=new PlayerControl{PlayerId=id,Role=r,OwnerId=id+100};p.Data.Object=p;p.Data.PlayerName="synthetic-"+id;
  p.transform.position=new(x,y);PlayerCatch.All.Add(p);TownOfHost.Main.AllPlayerSpeed[id]=1.25f;return p;
 }
 static RoleBase Role(string kind,byte id=1)
 {
  var info=infos.Single(i=>i.RoleName.ToString()==kind);var p=P(id,info.RoleName);
  return kind switch{"Slugger"=>new Slugger(p),"Pusher"=>new Pusher(p),"Gimlet"=>new Gimlet(p),"Cleaner"=>new Cleaner(p),"Informant"=>new Informant(p),"LifeSaver"=>new LifeSaver(p),"Bloodlust"=>new Bloodlust(p),"EvilButtoner"=>new EvilButtoner(p),_=>throw new Exception(kind)};
 }
 static bool Press(RoleBase r){bool adjust=false;bool? reset=null;((IUsePhantomButton)r).OnClick(ref adjust,ref reset);return reset==true;}
 static void Advance(float end,params RoleBase[] roles)
 {
  while(Time.time+0.0001f<end){var now=MathF.Min(end,Time.time+.02f);LateTask.At(now);foreach(var r in roles)r.OnFixedUpdate(r.Player);CustomNetObject.Tick();}
 }
 static DeadBody Body(byte id,float x=0,float y=0){var b=new DeadBody{ParentId=id};b.transform.position=new(x,y);Object.Items.Add(b);return b;}
 static Slugger SwingSetup(bool multi=false)
 {
  OptionItem.All[994013].Value=multi?1:0;var s=(Slugger)Role("Slugger");Press(s);Time.time=3.1f;Press(s);return s;
 }
 static void Main()
 {
  foreach(var info in infos)Test("role registers unique config "+info.RoleName,()=>Check(info.ConfigId>=994000&&info.ConfigId<=994700));
  Test("every new role has noncolliding option slots",()=>Eq(OptionItem.Registrations.Count,OptionItem.Registrations.Distinct().Count()));
  Test("Slugger preserves published role ID 28",()=>Eq(28,(int)CustomRoles.Slugger));
  Test("Destroyer preserves published role ID 5",()=>Eq(5,(int)CustomRoles.Destroyer));
  foreach(var(kind,id) in new[]{("Slugger",994000),("Pusher",994100),("Gimlet",994200),("Cleaner",994700)})
   Test("default kill cooldown "+kind,()=>{Eq(30f,OptionItem.All[id+10].GetFloat());Role(kind);});
  foreach(var name in new[]{"Slugger","Pusher","Gimlet","Cleaner","Informant"})
   foreach(var gate in new[]{"nonhost","dead","disconnected","intro","meeting","called","ended","not-task","winner"})
    Test("ability gate "+name+" / "+gate,()=>{var r=Role(name);P(2,CustomRoles.Crewmate,1);Body(2,1);if(gate=="nonhost")AmongUsClient.Instance.AmHost=false;if(gate=="dead")r.Player.Data.IsDead=true;if(gate=="disconnected")r.Player.Data.Disconnected=true;if(gate=="intro")GameStates.Intro=true;if(gate=="meeting")GameStates.IsMeeting=true;if(gate=="called")GameStates.CalledMeeting=true;if(gate=="ended")GameStates.IsEnded=true;if(gate=="not-task")GameStates.IsInTask=false;if(gate=="winner")CustomWinnerHolder.WinnerTeam=CustomWinner.Crewmate;Check(!Press(r));Eq(0,CustomRoleManager.Attempts);Eq(0,ReportDeadBodyPatch.Meetings);Eq(0,AmongUsClient.Calls.Count);});
  Test("Slugger cannot swing before charge",()=>{var s=Role("Slugger");Press(s);Time.time=1;Press(s);Check(Field<bool>(s,"charging"));Check(!Field<bool>(s,"waiting"));});
  Test("Slugger second press starts windup",()=>{var s=SwingSetup();Check(Field<bool>(s,"waiting"));Check(!Field<bool>(s,"charging"));});
  Test("Slugger windup repeat cannot schedule another swing",()=>{var s=SwingSetup();float due=Field<float>(s,"swingAt");Press(s);Eq(due,Field<float>(s,"swingAt"));});
  Test("Slugger nearest front target only",()=>{var s=SwingSetup();var near=P(2,CustomRoles.Crewmate,1);var far=P(3,CustomRoles.Crewmate,1.4f);var back=P(4,CustomRoles.Crewmate,-1);Advance(4.2f,s);Check(!near.IsAlive()&&far.IsAlive()&&back.IsAlive());Eq(1,CustomRoleManager.CompletedNoBody);Eq((byte)1,CustomRoleManager.LastKiller);});
  Test("Slugger multi hit",()=>{var s=SwingSetup(true);var a=P(2,CustomRoles.Crewmate,1);var b=P(3,CustomRoles.Crewmate,1.3f);Advance(4.2f,s);Check(!a.IsAlive()&&!b.IsAlive());Eq(2,CustomRoleManager.CompletedNoBody);});
  Test("Slugger wall prevents target selection",()=>{var s=SwingSetup();P(2,CustomRoles.Crewmate,1);PhysicsHelpers.Block=(_,_,_,_)=>true;Advance(4.2f,s);Eq(0,CustomRoleManager.Attempts);});
  Test("Slugger blocked kill leaves living target and no exile",()=>{var s=SwingSetup();var p=P(2,CustomRoles.Crewmate,1);p.Guarded=true;Advance(4.2f,s);Check(p.IsAlive());Eq(0,AmongUsClient.Calls.Count);});
  foreach(var stop in new[]{"meeting","destroy","dead","game","replace"})Test("Slugger pending hit cancelled "+stop,()=>{var s=SwingSetup();var p=P(2,CustomRoles.Crewmate,1);Advance(3.7f,s);if(stop=="meeting"){s.OnStartMeeting();GameStates.CalledMeeting=true;}if(stop=="destroy")s.OnDestroy();if(stop=="dead"){s.Player.Data.IsDead=true;s.OnDead(s.Player);}if(stop=="game")AmongUsClient.Instance.GameId++;if(stop=="replace")CustomRoleManager.AllActiveRoles.Remove(1);Advance(4.3f);Check(p.IsAlive());Eq(0,AmongUsClient.Calls.Count);});
  foreach(bool forward in new[]{false,true})foreach(bool flip in new[]{false,true})Test("fan art stays below 800 bytes "+forward+flip,()=>{var s=Role("Slugger");s.Player.cosmetics.FlipX=flip;var art=(string)typeof(Slugger).GetMethod("FanArt",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(s,new object[]{forward});Check(Encoding.UTF8.GetByteCount(art)<=NetworkSafety.NameByteLimit);Check(art.Contains("D7263D")&&art.Contains("FFFFFF"));});
  foreach(var stop in new[]{"meeting","destroy","death"})Test("Slugger saved movement restores "+stop,()=>{OptionItem.All[994017].Value=1;var s=Role("Slugger");Press(s);Eq(TownOfHost.Main.MinSpeed,TownOfHost.Main.AllPlayerSpeed[1]);if(stop=="meeting")s.OnStartMeeting();if(stop=="destroy")s.OnDestroy();if(stop=="death"){s.Player.Data.IsDead=true;s.OnDead(s.Player);}Eq(1.25f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("two Sluggers have independent charge",()=>{var a=Role("Slugger",1);var b=Role("Slugger",2);Press(a);Check(Field<bool>(a,"charging"));Check(!Field<bool>(b,"charging"));});
  Test("Pusher no valid spot does not consume",()=>{var p=Role("Pusher");P(2,CustomRoles.Crewmate,1);Check(!Press(p));Eq(0,LateTask.Jobs.Count);});
  foreach(var stop in new[]{"none","meeting","destroy","game","replace"})Test("Pusher lifecycle "+stop,()=>{var p=Role("Pusher");p.Player.transform.position=new(5,8.95f);var t=P(2,CustomRoles.Crewmate,5.8f,8.95f);Check(Press(p));if(stop=="meeting"){p.OnStartMeeting();GameStates.CalledMeeting=true;}if(stop=="destroy")p.OnDestroy();if(stop=="game")AmongUsClient.Instance.GameId++;if(stop=="replace")CustomRoleManager.AllActiveRoles.Remove(1);Advance(.3f);Eq(stop!="none",t.IsAlive());});
  Test("Pusher guarded target restored to original coordinate",()=>{var p=Role("Pusher");p.Player.transform.position=new(5,8.95f);var t=P(2,CustomRoles.Crewmate,5.8f,8.95f);t.Guarded=true;Press(p);Advance(.3f);Check(t.IsAlive());Check(Vector2.Distance(t.GetTruePosition(),new(5.8f,8.95f))<.001f);});
  Test("Pusher pending click ignored",()=>{var p=Role("Pusher");p.Player.transform.position=new(5,8.95f);P(2,CustomRoles.Crewmate,5.8f,8.95f);Check(Press(p));Check(!Press(p));Eq(1,LateTask.Jobs.Count);});
  foreach(var stop in new[]{"dead","disconnected"})Test("Pusher released pending after target "+stop,()=>{var p=Role("Pusher");p.Player.transform.position=new(5,8.95f);var t=P(2,CustomRoles.Crewmate,5.8f,8.95f);Check(Press(p));if(stop=="dead")t.Data.IsDead=true;else t.Data.Disconnected=true;Advance(.3f);P(3,CustomRoles.Crewmate,5.8f,8.95f);Check(Press(p));});
  Test("Pusher ladder fallback",()=>{TownOfHost.Main.NormalOptions.MapId=1;var p=Role("Pusher");P(2,CustomRoles.Crewmate,.5f);ShipStatus.Instance=new(){Ladders=new[]{new Ladder{IsTop=true,Destination=new Ladder()}}};Check(Press(p));Advance(.3f);Eq(1,CustomRoleManager.CompletedNoBody);});
  Test("Gimlet starts and restores movement at wall",()=>{var g=Role("Gimlet");Check(Press(g));Eq(TownOfHost.Main.MinSpeed,TownOfHost.Main.AllPlayerSpeed[1]);PhysicsHelpers.Block=(_,_,_,_)=>true;Advance(.2f,g);Check(!Field<bool>(g,"dashing"));Eq(1.25f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("Gimlet protected target attempted once per dash",()=>{var g=Role("Gimlet");var t=P(2,CustomRoles.Crewmate,.4f);t.Guarded=true;Press(g);Advance(.7f,g);Eq(1,CustomRoleManager.Attempts);Check(t.IsAlive());});
  Test("Gimlet hits multiple independent victims",()=>{var g=Role("Gimlet");var a=P(2,CustomRoles.Crewmate,.4f);var b=P(3,CustomRoles.Crewmate,1);Press(g);Advance(.4f,g);Check(!a.IsAlive()&&!b.IsAlive());});
  foreach(var stop in new[]{"meeting","destroy","death"})Test("Gimlet cancellation restores speed "+stop,()=>{var g=Role("Gimlet");Press(g);if(stop=="meeting")g.OnStartMeeting();if(stop=="destroy")g.OnDestroy();if(stop=="death"){g.Player.Data.IsDead=true;g.OnDead(g.Player);}Check(!Field<bool>(g,"dashing"));Eq(1.25f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("two Gimlets do not share dash",()=>{var a=Role("Gimlet",1);var b=Role("Gimlet",2);Press(a);Check(!Field<bool>(b,"dashing"));});
  Test("Cleaner keeps body visible and victim dead",()=>{var c=Role("Cleaner");var p=P(2,CustomRoles.Crewmate,1);p.Data.IsDead=true;var b=Body(2,1);Check(Press(c));Check(b.Reported&&b.gameObject.Active&&p.Data.IsDead&&DeadBodyEraser.IsUnreportable(2));Eq(0,AmongUsClient.Calls.Count);Eq(0,CustomNetObject.AllObjects.Count);});
  Test("Cleaner no body consumes no cooldown",()=>Check(!Press(Role("Cleaner"))));
  Test("Cleaner nearest reportable corpse",()=>{Role("Cleaner");var a=Body(2,1);var b=Body(3,.5f);a.Reported=true;Eq(b,DeadBodyEraser.FindNearestBody(new(),1.5f));});
  Test("Cleaner wall prevents cleanup",()=>{var c=Role("Cleaner");Body(2,1);PhysicsHelpers.Block=(_,_,_,_)=>true;Check(!Press(c));});
  Test("Cleaner cannot consume MadCleaner body",()=>{var c=Role("Cleaner");Body(2,1);TownOfHost.Roles.Madmate.MadCleaner.Cleaned.Add(2);Check(!Press(c));});
  Test("two Cleaners cannot consume same corpse",()=>{var a=Role("Cleaner",1);var b=Role("Cleaner",3);Body(2,1);Check(Press(a));Check(!Press(b));});
  Test("Cleaner destroyed previous corpse does not lock replacement",()=>{var c=Role("Cleaner");var old=Body(2);Check(Press(c));old.gameObject.Active=false;Check(!DeadBodyEraser.IsUnreportable(2));var fresh=Body(2);Check(Press(c));Check(fresh.Reported);});
  Test("Cleaner revival fresh corpse independently reportable",()=>{var c=Role("Cleaner");var old=Body(2);Check(Press(c));var fresh=Body(2);Check(!DeadBodyEraser.IsUnreportable(2));Eq(fresh,DeadBodyEraser.FindNearestBody(new(),1.5f));Check(Press(c));Check(old.Reported&&fresh.Reported&&DeadBodyEraser.IsUnreportable(2));});
  Test("Cleaner next game resets reports",()=>{DeadBodyEraser.MarkUnreportable(Body(2));DeadBodyEraser.Init();Check(!DeadBodyEraser.IsUnreportable(2));});
  Test("no-body guard uses existing defense pipeline",()=>{var k=P(1);var t=P(2);t.Guarded=true;Check(!DeadBodyEraser.KillWithoutBody(k,t,CustomDeathReason.Hit));Check(t.IsAlive());Eq(0,AmongUsClient.Calls.Count);});
  Test("no-body failure cannot leak into later normal kill",()=>{var k=P(1);var t=P(2);CustomRoleManager.Throw=true;try{DeadBodyEraser.KillWithoutBody(k,t,CustomDeathReason.Hit);}catch(InvalidOperationException){}CustomRoleManager.Throw=false;Check(!DeadBodyEraser.TryCompleteNoBodyKill(new(){AttemptKiller=k,AttemptTarget=t,AppearanceTarget=t}));});
  Test("no-body sends only standard Exiled RPC",()=>{var k=P(1);var t=P(2);Check(DeadBodyEraser.KillWithoutBody(k,t,CustomDeathReason.Fall));Eq(1,AmongUsClient.Calls.Count);Eq((byte)RpcCalls.Exiled,AmongUsClient.Calls[0].Rpc);Eq(t.NetId,AmongUsClient.Calls[0].Net);Eq((byte)1,CustomRoleManager.LastKiller);});
  Test("Bloodlust two exposed players remain registered",()=>{OptionItem.All[994511].Value=5;var a=(Bloodlust)Role("Bloodlust",1);a.Add();var b=(Bloodlust)Role("Bloodlust",2);b.Add();Advance(5.2f,a,b);Check(a.IsNameExposed&&b.IsNameExposed);Check(Bloodlust.OtherMark(a.Player,a.Player)!=""&&Bloodlust.OtherMark(a.Player,b.Player)!="");});
  Test("Bloodlust meeting does not advance timer",()=>{var b=(Bloodlust)Role("Bloodlust");GameStates.IsMeeting=true;Advance(50,b);Check(!b.IsNameExposed);});
  Test("Bloodlust successful kill resets only owner",()=>{OptionItem.All[994511].Value=5;var a=(Bloodlust)Role("Bloodlust",1);var b=(Bloodlust)Role("Bloodlust",2);Advance(5.2f,a,b);((IKiller)a).OnMurderPlayerAsKiller(new(){AttemptKiller=a.Player,AttemptTarget=P(3)});Check(!a.IsNameExposed&&b.IsNameExposed);});
  Test("EvilButtoner rejects deceased selected target",()=>{var e=(EvilButtoner)Role("EvilButtoner");var p=P(2);e.SingleAction(e.Player,p);p.Data.IsDead=true;Check(!Press(e));Eq(0,ReportDeadBodyPatch.Meetings);});
  Test("EvilButtoner valid target uses existing meeting path",()=>{var e=(EvilButtoner)Role("EvilButtoner");e.SingleAction(e.Player,P(2));Check(Press(e));Eq(1,ReportDeadBodyPatch.Meetings);});
  Test("EvilButtoner disconnected target falls back to allowed self call",()=>{var e=(EvilButtoner)Role("EvilButtoner");var p=P(2);e.SingleAction(e.Player,p);PlayerCatch.All.Remove(p);e.OnFixedUpdate(e.Player);Eq((byte)1,Field<byte>(e,"TargetId"));Check(Press(e));});
  Test("LifeSaver barrier has guard power one",()=>{var l=(LifeSaver)Role("LifeSaver");var m=new MurderInfo{AttemptKiller=P(2),AttemptTarget=l.Player};Check(l.OnCheckMurderAsTarget(m));Eq(1,m.GuardPower);});
  Test("LifeSaver task threshold enforced",()=>{var l=(LifeSaver)Role("LifeSaver");Check(!l.CanClickUseVentButton);l.MyTaskState.Completed=3;Check(l.CanClickUseVentButton);l.OnEnterVent(l.Player.MyPhysics,1);Check(!l.CanClickUseVentButton);CustomRoles w=CustomRoles.LifeSaver;Check(l.CheckWin(ref w));});
  Test("LifeSaver mode is per player",()=>{var a=(LifeSaver)Role("LifeSaver",1);var b=(LifeSaver)Role("LifeSaver",2);a.MyTaskState.Completed=3;a.OnEnterVent(a.Player.MyPhysics,1);CustomRoles w=CustomRoles.LifeSaver;Check(a.CheckWin(ref w));Check(!b.CheckWin(ref w));});
  Test("Informant selects first and queues second target",()=>{var i=(Informant)Role("Informant");var a=P(2,CustomRoles.Crewmate,1);Check(!Press(i));Eq((byte)2,Field<byte>(i,"pickedId"));a.transform.position=new(10,0);P(3,CustomRoles.Crewmate,1);Check(Press(i));Eq(byte.MaxValue,Field<byte>(i,"pickedId"));i.OnStartMeeting();CustomRoles w=CustomRoles.Informant;Check(!i.CheckWin(ref w));});
  Test("Informant idle meeting clears stale selected id on wire",()=>{var i=Role("Informant");P(2,CustomRoles.Crewmate,1);Press(i);int n=Sender.Packets.Count;i.OnStartMeeting();Check(Sender.Packets.Count>n);Eq(byte.MaxValue,Field<byte>(i,"pickedId"));});
  Test("Informant hostile count is bounded",()=>{var i=Role("Informant");bool failed=false;try{i.ReceiveRPC(new Hazel.MessageReader(new byte[]{255,16,0,0,0}));}catch(System.IO.InvalidDataException){failed=true;}Check(failed);});
  Test("TextDummy holds until binding ready",()=>{var r=Role("Slugger");var d=new TextDummy(new(r));d.Show("safe");d.MoveTo(new(1,1));Advance(.5f);Check(!d.IsReady);Advance(1.3f);Check(d.IsReady);Check(CustomNetObject.Names.Contains("safe"));Eq(0,CustomNetObject.RawNameCalls);});
  foreach(var stop in new[]{"remove","game","replace","meeting"})Test("TextDummy queued creation cancellation "+stop,()=>{var r=Role("Slugger");var d=new TextDummy(new(r));if(stop=="remove")d.Remove();if(stop=="game")AmongUsClient.Instance.GameId++;if(stop=="replace")CustomRoleManager.AllActiveRoles.Remove(1);if(stop=="meeting")GameStates.CalledMeeting=true;Advance(2);Check(!d.IsReady);Eq(0,CustomNetObject.Names.Count);});
  Test("TextDummy restores host outfit on send exception",()=>{var r=Role("Slugger");var d=new TextDummy(new(r));d.Show("safe");var o=PlayerControl.LocalPlayer.Data.DefaultOutfit;var before=(o.PlayerName,o.ColorId,o.HatId,o.SkinId,o.PetId,o.VisorId);CustomNetObject.ThrowAppearance=true;Advance(1.3f);Eq(before,(o.PlayerName,o.ColorId,o.HatId,o.SkinId,o.PetId,o.VisorId));Check(!d.IsReady);});
  Test("TextDummy invalid name rejected without raw RPC",()=>{var d=new TextDummy(new(Role("Slugger")));d.Show(new string('a',801));Advance(2);Check(!d.IsReady);Eq(0,CustomNetObject.RawNameCalls);});
  Test("TextDummy move failure removes visual",()=>{var d=new TextDummy(new(Role("Slugger")));d.Show("safe");d.MoveTo(new(1,1));CustomNetObject.ThrowMove=true;Advance(1.3f);Check(!d.IsReady);});
  Test("visual budget hard cap over one second",()=>{var b=new RoleVisualBudget(16,4);int accepted=0;for(int i=0;i<1000;i++)if(b.Take(1,i/1000f))accepted++;Check(accepted<=20);});
  Test("visual budget separate game resets",()=>{var b=new RoleVisualBudget(1,1);Check(b.Take(1,0));Check(!b.Take(1,0));Check(b.Take(2,0));});
  Test("visual budget rejects nonfinite time",()=>Check(!new RoleVisualBudget(1,1).Take(1,float.NaN)));
  Test("Notice non-addon corpse sends no message",()=>{var corpse=P(2);corpse.Data.IsDead=true;TownOfHost.Roles.AddOns.Common.Notice.OnReportDeadBody(P(1),corpse.Data);Eq(0,Utils.Messages.Count);});
  Test("Notice respects host authority",()=>{AmongUsClient.Instance.AmHost=false;var corpse=P(2,CustomRoles.Notice);corpse.Data.IsDead=true;TownOfHost.Roles.AddOns.Common.Notice.OnReportDeadBody(P(1),corpse.Data);Eq(0,Utils.Messages.Count);});
  Test("Notice private result is queued for reporter",()=>{var corpse=P(2,CustomRoles.Notice);corpse.Data.IsDead=true;TownOfHost.Roles.AddOns.Common.Notice.OnReportDeadBody(P(1),corpse.Data);Eq((byte)1,Utils.Messages.Single());Eq((byte)1,MeetingHudPatch.StartPatch.meetingsends.Single().Item1);});
  Test("Notice public option uses broadcast game message",()=>{OptionItem.All[994851].Value=1;var corpse=P(2,CustomRoles.Notice);corpse.Data.IsDead=true;TownOfHost.Roles.AddOns.Common.Notice.OnReportDeadBody(P(1),corpse.Data);Eq((byte)255,Utils.Messages.Single());});
  Test("Notice alive target produces no result",()=>{TownOfHost.Roles.AddOns.Common.Notice.OnReportDeadBody(P(1),P(2,CustomRoles.Notice).Data);Eq(0,Utils.Messages.Count);});
  Console.WriteLine($"HAMO44004_RUNTIME_RESULT {passed} passed, {failed} failed (actual imported roles/adapters; native Unity/network substitutes, no game or external traffic)");
  Environment.ExitCode=failed==0?0:1;
 }
}


