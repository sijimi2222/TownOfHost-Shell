using TownOfHost;using InnerNet;using UnityEngine;
static class Program
{
 static int passed,failed;
 static void Check(bool value){if(!value)throw new Exception("assertion");}
 static void Reset(){Zoom.Reset();HudManager.Instance=new();Camera.main=new();AmongUsClient.Instance=new();PlayerControl.LocalPlayer=new();MeetingHud.Instance=null;GameStates.IsInGame=GameStates.IsInTask=true;GameStates.IsMeeting=GameStates.CalledMeeting=GameStates.Intro=GameStates.ExiledAnimate=false;Options.UseZoom=new();Input.Alt=Input.Shift=false;Input.mouseScrollDelta=new(0,-1);Screen.height=1080;Screen.width=1920;ResolutionManager.ResolutionChanged=new();}
 static void Test(string name,Action action){try{Reset();action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+" "+e);}}
 static void Main()
 {
  foreach(var platform in Enum.GetValues<Platforms>())foreach(bool room in new[]{false,true})foreach(bool host in new[]{false,true})Test($"public binary never enables living zoom {platform}/{room}/{host}",()=>{Constants.Platform=platform;AmongUsClient.Instance.IsGamePublic=room;AmongUsClient.Instance.AmHost=host;TownOfHost.Development.ShellDebugRuntime.ZoomEnabled=true;Zoom.Postfix();Check(Camera.main.orthographicSize==3&&HudManager.Instance.UICamera.orthographicSize==3);});
  Test("public death zoom retained",()=>{PlayerControl.LocalPlayer.Alive=false;Zoom.Postfix();Check(Camera.main.orthographicSize==4);Zoom.GameEnded();Check(Camera.main.orthographicSize==3);});
  Test("public option OFF denies dead zoom",()=>{PlayerControl.LocalPlayer.Alive=false;Options.UseZoom.Value=false;Zoom.Postfix();Check(Camera.main.orthographicSize==3);});
  Console.WriteLine($"RESULT {passed} passed, {failed} failed");Environment.ExitCode=failed==0?0:1;
 }
}
