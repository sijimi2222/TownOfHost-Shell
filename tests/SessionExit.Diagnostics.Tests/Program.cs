using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using TownOfHost;
using TownOfHost.Development;
using HarmonyLib;
partial class Program {
 static int passed,failed;static void Check(bool b,string why="assertion"){if(!b)throw new Exception(why);}
 static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.GetType().Name);}}
 static string Root([CallerFilePath]string file="")=>Environment.GetEnvironmentVariable("TOHS_SESSION_SOURCE_ROOT")??Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file),"../.."));
 static object Invoke(string patch,string method,params object[] args)=>typeof(SessionExitDiagnostics).GetNestedType(patch,BindingFlags.NonPublic).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
 static void Main(){
 Test("no lifecycle logging before an actual exit",()=>{var lines=new List<string>();var t=new SessionExitTrace(lines.Add,()=>0);foreach(var s in Enum.GetValues<ExitStage>())t.Emit(s);Check(lines.Count==0);});
 Test("complete synthetic teardown and menu-return sequence",()=>{
  var lines=new List<string>();long clock=0;var t=new SessionExitTrace(lines.Add,()=>clock);t.Begin(0);
  foreach(var s in new[]{ExitStage.DisconnectStarted,ExitStage.ObjectsResetStarted,ExitStage.ObjectsResetReturned,ExitStage.CommentsStopStarted,ExitStage.CommentsStopReturned,ExitStage.RolesDisposeStarted,ExitStage.RolesDisposeReturned,ExitStage.HudDestroyStarted,ExitStage.HudDestroyReturned,ExitStage.SceneChangeStarted,ExitStage.SceneChangeReturned,ExitStage.MenuStartStarted,ExitStage.MenuStartReturned,ExitStage.DisconnectReturned,ExitStage.ExitReturned}){clock+=5;t.Emit(s);}
  t.MenuTick();t.MenuTick();Check(!t.Active&&lines.Count==18&&lines.Last().Contains("TrackingComplete"));
 });
 Test("nested disconnect keeps the same exit sequence",()=>{var lines=new List<string>();var t=new SessionExitTrace(lines.Add,()=>0);t.Begin(0);t.Begin(1);Check(t.Sequence==1&&lines.Count==1);});
 Test("repeated create-close cycles do not retain stage suppression",()=>{var lines=new List<string>();var t=new SessionExitTrace(lines.Add,()=>0);for(int i=0;i<50;i++){t.Begin(0);t.Emit(ExitStage.DisconnectStarted);t.MenuTick();t.MenuTick();Check(t.Sequence==i+1&&!t.Active);}Check(lines.Count==200);});
 Test("repeated teardown and frame callbacks cannot flood logs",()=>{var lines=new List<string>();var t=new SessionExitTrace(lines.Add,()=>0);t.Begin(0);for(int i=0;i<10000;i++)t.Emit(ExitStage.ObjectsResetStarted);t.MenuTick();t.MenuTick();for(int i=0;i<10000;i++)t.MenuTick();Check(lines.Count==5&&lines.Count(x=>x.Contains("repeat=2plus"))==1);});
 Test("concurrent observations are bounded and unique",()=>{var lines=new List<string>();var t=new SessionExitTrace(lines.Add,()=>0);t.Begin(0);Parallel.For(0,2000,i=>t.Emit((ExitStage)(i%Enum.GetValues<ExitStage>().Length)));Check(lines.Count<=SessionExitTrace.MaximumLines&&lines.Distinct().Count()==lines.Count);});
 foreach(Exception error in new Exception[]{new InvalidOperationException("synthetic-private-token"),new ObjectDisposedException("synthetic-client-id"),new ArgumentException("synthetic-secret"),new NullReferenceException("synthetic-chat-body")})Test("exception messages never enter diagnostics: "+error.GetType().Name,()=>{var lines=new List<string>();var t=new SessionExitTrace(lines.Add,()=>0);t.Begin(0);t.Emit(ExitStage.LifecycleException,error);Check(lines.Count==2&&lines.All(x=>!x.Contains("synthetic-")));});
 Test("diagnostic sink failures do not interrupt cleanup",()=>{var t=new SessionExitTrace(_=>throw new Exception("synthetic"),()=>0);t.Begin(0);t.Emit(ExitStage.HudDestroyReturned);t.MenuTick();t.MenuTick();Check(!t.Active);});
 Test("negative clock movement cannot produce negative elapsed times",()=>{long clock=100;var lines=new List<string>();var t=new SessionExitTrace(lines.Add,()=>clock);t.Begin(0);clock=0;t.Emit(ExitStage.DisconnectReturned);Check(lines.Last().Contains("elapsedMs=0"));});
 Test("quitting can be observed after the menu tracking completes",()=>{var lines=new List<string>();var t=new SessionExitTrace(lines.Add,()=>0);t.Begin(0);t.MenuTick();t.MenuTick();t.Emit(ExitStage.ApplicationQuitting,afterCompletion:true);Check(lines.Last().Contains("ApplicationQuitting"));});
 Test("one explicit target and an unsuppressing finalizer per lifecycle patch",()=>{foreach(var nested in typeof(SessionExitDiagnostics).GetNestedTypes(BindingFlags.NonPublic)){var target=nested.GetCustomAttribute<HarmonyPatch>();if(target==null)continue;Check(nested.GetCustomAttributes<HarmonyPatch>().Count()==1);var finalizer=nested.GetMethod("Finalizer",BindingFlags.Static|BindingFlags.NonPublic);if(nested.Name=="MenuActionPatch"){Check(finalizer==null);continue;}Check(finalizer?.GetCustomAttribute<HarmonyFinalizer>()!=null);var error=new InvalidOperationException("synthetic-private");Check(ReferenceEquals(finalizer.Invoke(null,new object[]{error}),error));Check(finalizer.Invoke(null,new object[]{null})==null);}});
 Test("observer installation is idempotent",()=>{int before=UnityEngine.Application.Subscriptions;for(int i=0;i<100;i++)SessionExitDiagnostics.Install();Check(UnityEngine.Application.Subscriptions==before+1);});
 Test("actual patch adapters record teardown without changing game or stream state",()=>{TownOfHost.Modules.CustomNetObject.AllObjects.Add(new object());TownOfHost.Roles.Core.CustomRoleManager.AllActiveRoles[1]=new object();StreamerInfo.Status=StreamerInfo.ConnectionStatus.Receiving;Invoke("ExitPatch","Prefix",InnerNet.DisconnectReasons.ExitGame);foreach(var p in new[]{"DisconnectPatch","ObjectsPatch","CommentsPatch","RolesPatch","HudPatch","ScenePatch","MenuPatch"}){if(p=="DisconnectPatch")Invoke(p,"Prefix",InnerNet.DisconnectReasons.ExitGame);else Invoke(p,"Prefix");Invoke(p,"Postfix");}Invoke("ExitPatch","Postfix");Invoke("MenuTickPatch","Postfix");Invoke("MenuTickPatch","Postfix");Check(TownOfHost.Modules.CustomNetObject.AllObjects.Count==1&&TownOfHost.Roles.Core.CustomRoleManager.AllActiveRoles.Count==1&&StreamerInfo.Status==StreamerInfo.ConnectionStatus.Receiving);Check(Logger.Lines.Any(x=>x.Contains("SESSION_EXIT_V1")&&x.Contains("objects=1")));});
 Test("normal Quit invocation is distinct from room closure",()=>{Invoke("MenuActionPatch","Prefix","Quit");UnityEngine.Application.Quit();Check(Logger.Lines.Any(x=>x.Contains("stage=MenuQuitInvocation"))&&Logger.Lines.Any(x=>x.Contains("stage=ApplicationQuitting")));});
 Test("public Release excludes the entire development diagnostics folder",()=>{var xml=XDocument.Load(Path.Combine(Root(),"TownOfHost.csproj"));Check(xml.Descendants("Compile").Any(x=>(string)x.Attribute("Remove")=="Development\\DebugController\\**\\*.cs"&&((string)x.Attribute("Condition")).Contains("ShellDebugController")));});
 RunTitleFrameTests();
 RunDebugLifetimeTests();
 Console.WriteLine($"RESULT {passed} passed, {failed} failed (lifecycle observers and synthetic adapters; no native game test)");Environment.ExitCode=failed==0?0:1;
 }
}
