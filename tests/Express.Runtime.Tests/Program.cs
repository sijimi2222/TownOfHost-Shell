using System.Runtime.CompilerServices;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Crewmate;
class Program {
 static int passed,failed;
 static string Here([CallerFilePath]string path="")=>Path.GetDirectoryName(path);
 static void Check(bool ok,string why="assertion"){if(!ok)throw new Exception(why);}
 static void Near(float expected,float actual)=>Check(Math.Abs(expected-actual)<0.00001f,$"expected={expected} actual={actual}");
 static Express Make(byte id=1,float option=1.5f){OptionItem.Last.Value=option;return new Express(new PlayerControl{PlayerId=id});}
 static void Test(string name,Action test){TownOfHost.Main.AllPlayerSpeed=new(){{1,1f},{2,1f}};CustomRoleManager.AllActiveRoles.Clear();PlayerGameOptionsSender.Dirty.Clear();try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static int MainEntry(){
  Express.RoleInfo.Setup();
  Test("production Dispose parity",()=>{var source=File.ReadAllText(Path.Combine(Here(),"../../Roles/Core/RoleBase.cs")).Replace("\r\n","\n");var generated=File.ReadAllText(Path.Combine(Here(),"GeneratedDisposal.cs")).Replace("\r\n","\n");var start=source.IndexOf("    public void Dispose()");var end=source.IndexOf("#pragma warning restore",start);Check(generated.Contains(source[start..end].Trim()));});
  Test("initial role keeps existing additive speed",()=>{var r=Make();r.Add();Near(2.5f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("role removal via production Dispose restores speed",()=>{var r=Make();r.Add();r.Dispose();Near(1f,TownOfHost.Main.AllPlayerSpeed[1]);Check(r.Player==null&&!CustomRoleManager.AllActiveRoles.ContainsKey(1));});
  Test("late creation hook must not cancel Express",()=>{var r=Make();r.Add();r.ChengeRoleAdd();Near(2.5f,TownOfHost.Main.AllPlayerSpeed[1]);r.Dispose();Near(1f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("repeat late creation notification does not remove speed",()=>{var r=Make();r.Add();r.ChengeRoleAdd();r.ChengeRoleAdd();Near(2.5f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("duplicate Add does not stack",()=>{var r=Make();r.Add();r.Add();Near(2.5f,TownOfHost.Main.AllPlayerSpeed[1]);r.Dispose();Near(1f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("duplicate cleanup does not subtract twice",()=>{var r=Make();r.Add();r.OnDestroy();r.OnDestroy();Near(1f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("cleanup before Add does not subtract",()=>{var r=Make();r.OnDestroy();Near(1f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("cleared player reference is not accessed during cleanup",()=>{var r=Make();r.Add();r.Player=null;r.OnDestroy();Near(1f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("removed speed entry is not recreated",()=>{var r=Make();r.Add();TownOfHost.Main.AllPlayerSpeed.Remove(1);r.OnDestroy();Check(!TownOfHost.Main.AllPlayerSpeed.ContainsKey(1));});
  Test("new match dictionary is not changed by old role",()=>{var r=Make();r.Add();var old=TownOfHost.Main.AllPlayerSpeed;TownOfHost.Main.AllPlayerSpeed=new(){{1,1f}};r.OnDestroy();Near(1f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("other additive modifier survives removal",()=>{var r=Make();r.Add();TownOfHost.Main.AllPlayerSpeed[1]+=0.75f;r.Dispose();Near(1.75f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("multiple owners remove independent contributions",()=>{var a=Make(1,1.5f);a.Add();var b=Make(2,3f);b.Add();a.Dispose();Near(1f,TownOfHost.Main.AllPlayerSpeed[1]);Near(4f,TownOfHost.Main.AllPlayerSpeed[2]);b.Dispose();Near(1f,TownOfHost.Main.AllPlayerSpeed[2]);});
  Test("same owner replacement does not accumulate",()=>{var a=Make();a.Add();a.Dispose();var b=Make();b.Add();Near(2.5f,TownOfHost.Main.AllPlayerSpeed[1]);b.Dispose();Near(1f,TownOfHost.Main.AllPlayerSpeed[1]);});
  Test("constructed owners retain their own configuration",()=>{var a=Make(1,1.5f);var b=Make(2,3f);a.Add();b.Add();Near(2.5f,TownOfHost.Main.AllPlayerSpeed[1]);Near(4f,TownOfHost.Main.AllPlayerSpeed[2]);a.Dispose();b.Dispose();Near(1f,TownOfHost.Main.AllPlayerSpeed[1]);Near(1f,TownOfHost.Main.AllPlayerSpeed[2]);});
  Test("removal marks only owner settings dirty",()=>{var r=Make();r.Add();PlayerGameOptionsSender.Dirty.Clear();r.OnDestroy();Check(PlayerGameOptionsSender.Dirty.SequenceEqual(new byte[]{1}));r.OnDestroy();Check(PlayerGameOptionsSender.Dirty.Count==1);});
  Test("stale role cleanup does not dirty new match",()=>{var r=Make();r.Add();TownOfHost.Main.AllPlayerSpeed=new(){{1,1f}};PlayerGameOptionsSender.Dirty.Clear();r.OnDestroy();Check(PlayerGameOptionsSender.Dirty.Count==0);});
  for(float option=1.5f;option<=10f;option+=0.25f){float value=option;Test("configured add/remove "+value,()=>{var r=Make(1,value);r.Add();Near(1f+value,TownOfHost.Main.AllPlayerSpeed[1]);r.Dispose();Near(1f,TownOfHost.Main.AllPlayerSpeed[1]);});}
  Console.WriteLine($"RESULT {passed} passed, {failed} failed (actual Express and Dispose; Unity/network substitutes)");return failed==0?0:1;
 }
 static int Main()=>MainEntry();
}
