using TownOfHost;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Modules;
static class Program {
 static int passed;
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Test(string name,Action action){Reset();action();passed++;Console.WriteLine("PASS "+name);}
 static void Reset(){GameStates.IsMeeting=true;PlayerCatch.Players.Clear();Utils.Messages.Clear();SelfVoteManager.Enabled=true;}
 static (SenderState,PlayerControl,PlayerControl) Setup(object role=null){var me=new PlayerControl{PlayerId=1,Role=role??new Sender()};var target=new PlayerControl{PlayerId=2};PlayerCatch.Players[1]=me;PlayerCatch.Players[2]=target;return(new SenderState(),me,target);}
 public static void Main(){
 Test("runtime role IDs are unique",()=>{var ids=Enum.GetValues<CustomRoles>();Check(ids.Length==ids.Distinct().Count(),"duplicate enum value");});
 Test("new addon classifications",()=>{foreach(var r in new[]{CustomRoles.Surviving,CustomRoles.Berserker,CustomRoles.ToughGuy,CustomRoles.Comorbidity,CustomRoles.Murder,CustomRoles.BetrayGuesser})Check(r.IsAddOn(),"missing addon");});
 Test("buff and debuff classifications stay disjoint",()=>{foreach(var r in new[]{CustomRoles.Berserker,CustomRoles.ToughGuy,CustomRoles.Comorbidity})Check(r.IsBuffAddon()&&!r.IsDebuffAddon(),"buff misclassified");foreach(var r in new[]{CustomRoles.Surviving,CustomRoles.Murder,CustomRoles.BetrayGuesser})Check(r.IsDebuffAddon()&&!r.IsBuffAddon(),"debuff misclassified");});
 Test("Arsene and Kyonshi are ghost roles",()=>{Check(CustomRoles.Arsene.IsGhostRole()&&CustomRoles.Kyonshi.IsGhostRole(),"missing ghosts");Check(!CustomRoles.Lupin.IsGhostRole(),"main role is ghost");});
 Test("combination discovery is symmetric",()=>{foreach(var p in new[]{(CustomRoles.Trauma,CustomRoles.Surmount),(CustomRoles.Catalyst,CustomRoles.Conductor)})Check(p.Item1.GetCombination()==p.Item2&&p.Item2.GetCombination()==p.Item1,"wrong partner");});
 Test("partner availability follows leader and disabled leader",()=>{foreach(var p in new[]{(CustomRoles.Trauma,CustomRoles.Surmount),(CustomRoles.Catalyst,CustomRoles.Conductor)}){Options.CustomRoleSpawnChances[p.Item1]=new(){Value=100};Options.CustomRoleCounts[p.Item1]=new(){Value=1};Check(Options.GetRoleCount(p.Item2)==1&&Options.GetRoleChance(p.Item2)==100,"partner unavailable");Options.CustomRoleSpawnChances[p.Item1].Value=0;Check(Options.GetRoleCount(p.Item2)==0,"disabled partner available");}});
 Test("new role order places latest imports first",()=>{Check(Hamo435RoleOrder.Priority[CustomRoles.Parity]<Hamo435RoleOrder.Priority[CustomRoles.Trauma],"wrong import order");Check(Hamo435RoleOrder.Priority.Values.All(v=>v<0),"imports below prior additions");});
 Test("Sender delivers only to target and sender",()=>{var(s,me,t)=Setup();s.CheckVote(me,t.PlayerId,me,3);Utils.Messages.Clear();Check(s.TryIntercept(me,"private text",3),"not intercepted");Check(s.Used==1&&Utils.Messages.Count==2,"wrong count");Check(Utils.Messages.Single(m=>m.Target==t.PlayerId).Text=="private text","wrong target");Check(((Sender)me.Role).SyncCount==1,"missing sync");});
 Test("MadSender shares the same private routing",()=>{var role=new TownOfHost.Roles.Madmate.MadSender();var(s,me,t)=Setup(role);s.CheckVote(me,t.PlayerId,me,2);Check(s.TryIntercept(me,"private",2)&&role.SyncCount==1,"mad sender not synced");});
 Test("Sender target is consumed exactly once",()=>{var(s,me,t)=Setup();s.CheckVote(me,t.PlayerId,me,3);Check(s.TryIntercept(me,"one",3),"first failed");Check(!s.TryIntercept(me,"two",3)&&s.Used==1,"target reused");});
 Test("Sender leaves commands untouched",()=>{var(s,me,t)=Setup();s.CheckVote(me,t.PlayerId,me,1);Check(!s.TryIntercept(me,"/cmd h",1)&&s.Used==0,"command intercepted");Check(s.TryIntercept(me,"text",1),"target lost by command");});
 Test("Sender rejects dead or disconnected target",()=>{var(s,me,t)=Setup();s.CheckVote(me,t.PlayerId,me,2);PlayerCatch.Players.Remove(t.PlayerId);Check(!s.TryIntercept(me,"text",2)&&s.Used==0,"disconnected target consumed use");});
 Test("Sender enforces usage limit",()=>{var(s,me,t)=Setup();s.Used=1;s.CheckVote(me,t.PlayerId,me,1);Check(!s.TryIntercept(me,"text",1)&&s.Left(1)==0,"exceeded limit");});
 Test("meeting reset clears pending target and preserves total uses",()=>{var(s,me,t)=Setup();s.Used=1;s.CheckVote(me,t.PlayerId,me,3);s.Reset();Check(!s.TryIntercept(me,"text",3)&&s.Used==1,"meeting reset failed");});
 Test("Sender cannot send outside meetings or after death",()=>{var(s,me,t)=Setup();s.CheckVote(me,t.PlayerId,me,3);GameStates.IsMeeting=false;Check(!s.TryIntercept(me,"text",3),"sent outside meeting");GameStates.IsMeeting=true;me.Alive=false;Check(!s.TryIntercept(me,"text",3),"dead sender sent");});
 Test("Sender rejects self target",()=>{var(s,me,t)=Setup();s.CheckVote(me,me.PlayerId,me,3);Check(!s.TryIntercept(me,"text",3),"self targeted");});
 Test("Sender skip cancels pending target",()=>{var(s,me,t)=Setup();s.CheckVote(me,t.PlayerId,me,3);s.CheckVote(me,253,me,3);Check(!s.TryIntercept(me,"text",3),"skip not cancelled");});
 Test("ability parser rejects ordinary chat",()=>{Check(!RoleCommandParsing.TryParseCommand("mes hello",out _)&&!RoleCommandParsing.TryParseCommand("mm",out _),"ordinary text consumed");});
 Test("ability parser handles cmd and alias input",()=>{Check(RoleCommandParsing.TryParseCommand("/cmd mes 2 hello",out var a)&&a.SequenceEqual(new[]{"/mes","2","hello"}),"cmd parse");Check(RoleCommandParsing.TryParseCommand("/MM 1",out var b)&&b.SequenceEqual(new[]{"/mm","1"}),"alias parse");});
 Test("ability parser ignores unrelated and empty commands",()=>{Check(!RoleCommandParsing.TryParseCommand("/cmd",out _)&&!RoleCommandParsing.TryParseCommand("/cmd h",out _)&&!RoleCommandParsing.TryParseCommand("",out _),"unrelated command consumed");});
 Test("pending flag follows selection, consume and meeting reset",()=>{var(s,me,t)=Setup();Check(!s.AwaitingMessage,"initial pending");s.CheckVote(me,t.PlayerId,me,3);Check(s.AwaitingMessage,"selection not pending");s.TryIntercept(me,"text",3);Check(!s.AwaitingMessage,"consumed still pending");s.CheckVote(me,t.PlayerId,me,3);s.Reset();Check(!s.AwaitingMessage,"reset still pending");});
 Test("one failed Vanilla option recipient cannot stop remaining clients",()=>{var failed=new TestOptionsSender{Throw=true};var next=new TestOptionsSender();var invalid=new TestOptionsSender{Valid=false};OptionsBroadcast.AllSenders=new(){failed,next,invalid};OptionsBroadcast.SendAllGameOptions();Check(failed.Calls==1&&next.Calls==1&&invalid.Calls==0,"batch aborted");Check(OptionsBroadcast.AllSenders.Count==2,"invalid recipient retained");});
 Console.WriteLine($"RESULT {passed} passed, 0 failed");
 }
}