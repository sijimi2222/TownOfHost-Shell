using TownOfHost;using UnityEngine;using InnerNet;using System.Reflection;
static class Program {
 static int passed,failed;
 static void Check(bool b,string why="assertion"){if(!b)throw new Exception(why);}
 static void Setup(){TownOfHost.Development.ShellDebugRuntime.ZoomEnabled=true;Zoom.Reset();HudManager.Instance=new();Camera.main=new();PlayerControl.LocalPlayer=new();AmongUsClient.Instance=new();Constants.Platform=Platforms.StandaloneSteamPC;Options.UseZoom=new();GameStates.IsInGame=GameStates.IsInTask=true;GameStates.IsMeeting=GameStates.CalledMeeting=GameStates.Intro=GameStates.ExiledAnimate=false;MeetingHud.Instance=null;Input.mouseScrollDelta=new();Input.Alt=Input.Shift=false;ResolutionManager.ResolutionChanged=new();Screen.width=1920;Screen.height=1080;Logger.Warnings=0;}
 static void Test(string n,Action a){try{Setup();a();passed++;Console.WriteLine("PASS "+n);}catch(Exception e){failed++;Console.WriteLine("FAIL "+n+": "+e.Message);}}
 static void Wheel(float y){Input.mouseScrollDelta=new(0,y);Zoom.Postfix();Input.mouseScrollDelta=new();}
 static void Zoomed(){Wheel(-1);Check(Camera.main.orthographicSize==4&&HudManager.Instance.UICamera.orthographicSize==4);}
 static void Normal(){Check(Camera.main.orthographicSize==3&&HudManager.Instance.UICamera.orthographicSize==3);}
 static void Denied(){Wheel(-1);Normal();}
 static void Main(){
 foreach(var p in new[]{Platforms.StandaloneSteamPC,Platforms.StandaloneEpicPC})Test("living private PC host zoom "+p,()=>{Constants.Platform=p;Zoomed();});
 Test("wheel zoom out and in reuse the existing unit step",()=>{Zoomed();Wheel(-1);Check(Camera.main.orthographicSize==5);Wheel(1);Check(Camera.main.orthographicSize==4);});
 Test("zoom-in retains old minimum two",()=>{for(int i=0;i<20;i++)Wheel(1);Check(Camera.main.orthographicSize==2);});
 Test("living non-host cannot zoom",()=>{AmongUsClient.Instance.AmHost=false;Denied();});
 Test("living public host cannot zoom",()=>{AmongUsClient.Instance.IsGamePublic=true;Denied();});
 Test("local-player ownership required for living zoom",()=>{PlayerControl.LocalPlayer.AmOwner=false;Denied();});
 foreach(var p in new[]{Platforms.Android,Platforms.IPhone,Platforms.Switch})Test("living non-PC host denied "+p,()=>{Constants.Platform=p;Denied();});
 Test("offline local living host denied",()=>{AmongUsClient.Instance.NetworkMode=NetworkModes.LocalGame;Denied();});
 Test("freeplay Alt cannot bypass living private-online restriction",()=>{AmongUsClient.Instance.NetworkMode=NetworkModes.FreePlay;Input.Alt=true;Denied();});
 Test("option off blocks living zoom",()=>{Options.UseZoom.Value=false;Denied();});
 Test("option off also blocks old freeplay Alt exception",()=>{AmongUsClient.Instance.NetworkMode=NetworkModes.FreePlay;Input.Alt=true;PlayerControl.LocalPlayer.Alive=false;Options.UseZoom.Value=false;Denied();});
 Test("dead mod guest retains public room zoom",()=>{PlayerControl.LocalPlayer.Alive=false;AmongUsClient.Instance.AmHost=false;AmongUsClient.Instance.IsGamePublic=true;Zoomed();});
 Test("ghost-role exclusion is preserved",()=>{PlayerControl.LocalPlayer.Alive=false;PlayerControl.LocalPlayer.Ghost=true;Denied();});
 Test("existing dead freeplay Alt path remains",()=>{PlayerControl.LocalPlayer.Alive=false;PlayerControl.LocalPlayer.Ghost=true;AmongUsClient.Instance.NetworkMode=NetworkModes.FreePlay;Input.Alt=true;Zoomed();});
 Test("living to dead transition retains the same camera",()=>{Zoomed();PlayerControl.LocalPlayer.Alive=false;Wheel(-1);Check(Camera.main.orthographicSize==5);});
 Test("option off restores immediately even with input locked",()=>{Zoomed();PlayerControl.LocalPlayer.CanMove=false;Options.UseZoom.Value=false;Zoom.Postfix();Normal();});
 Test("option re-enable starts from the normal scale",()=>{Zoomed();Options.UseZoom.Value=false;Zoom.Postfix();Options.UseZoom.Value=true;Zoomed();});
 Test("public-room transition removes living zoom",()=>{Zoomed();AmongUsClient.Instance.IsGamePublic=true;Zoom.Postfix();Normal();});
 Test("host authority loss removes living zoom",()=>{Zoomed();AmongUsClient.Instance.AmHost=false;Zoom.Postfix();Normal();});
 foreach(string phase in new[]{"meeting","called meeting","meeting hud","intro","exile","not task","not in game","lobby","ended","disconnected"})Test("restores during "+phase,()=>{Zoomed();PlayerControl.LocalPlayer.CanMove=false;switch(phase){case "meeting":GameStates.IsMeeting=true;break;case "called meeting":GameStates.CalledMeeting=true;break;case "meeting hud":MeetingHud.Instance=new();break;case "intro":GameStates.Intro=true;break;case "exile":GameStates.ExiledAnimate=true;break;case "not task":GameStates.IsInTask=false;break;case "not in game":GameStates.IsInGame=false;break;case "lobby":AmongUsClient.Instance.GameState=AmongUsClient.GameStates.Joined;break;case "ended":AmongUsClient.Instance.GameState=AmongUsClient.GameStates.Ended;break;case "disconnected":PlayerControl.LocalPlayer.Data.Disconnected=true;break;}Zoom.Postfix();Normal();});
 Test("chat locks wheel without changing current camera",()=>{Zoomed();PlayerControl.LocalPlayer.CanMove=false;Wheel(-1);Check(Camera.main.orthographicSize==4);});
 Test("game-end hook restores before a further HUD update",()=>{Zoomed();Zoom.GameEnded();Normal();});
 Test("HUD-destroy hook restores before objects disappear",()=>{Zoomed();Zoom.HudDestroyed();Normal();});
 Test("main-menu hook restores without touching a new camera",()=>{Zoomed();var old=Camera.main;var menu=new Camera{orthographicSize=7};Camera.main=menu;Zoom.MainMenuOpened();Check(menu.orthographicSize==7&&old.orthographicSize==3);});
 Test("lost local player does not strand zoom",()=>{Zoomed();PlayerControl.LocalPlayer=null;Zoom.Postfix();Normal();});
 Test("lost player data restores safely",()=>{Zoomed();PlayerControl.LocalPlayer.Data=null;Zoom.Postfix();Normal();});
 Test("lost role restores safely",()=>{Zoomed();PlayerControl.LocalPlayer.Data.Role=null;Zoom.Postfix();Normal();});
 Test("lost HUD still restores tracked cameras",()=>{Zoomed();var oldUi=HudManager.Instance.UICamera;HudManager.Instance=null;Zoom.Postfix();Check(Camera.main.orthographicSize==3&&oldUi.orthographicSize==3);});
 Test("null UI camera restores previous UI camera",()=>{Zoomed();var ui=HudManager.Instance.UICamera;HudManager.Instance.UICamera=null;Zoom.Postfix();Check(ui.orthographicSize==3&&Camera.main.orthographicSize==3);});
 Test("fractional and differing base sizes are restored exactly",()=>{HudManager.Instance.UICamera.orthographicSize=3.25f;Camera.main.orthographicSize=4.5f;Wheel(-1);Options.UseZoom.Value=false;Zoom.Postfix();Check(HudManager.Instance.UICamera.orthographicSize==3.25f&&Camera.main.orthographicSize==4.5f);});
 Test("Shift restores actual baseline rather than hardcoded size",()=>{HudManager.Instance.UICamera.orthographicSize=5;Camera.main.orthographicSize=6;Wheel(-1);Input.Shift=true;Zoom.Postfix();Check(HudManager.Instance.UICamera.orthographicSize==5&&Camera.main.orthographicSize==6);});
 Test("replacement HUD does not inherit an old zoom lease",()=>{Zoomed();var oldUi=HudManager.Instance.UICamera;HudManager.Instance=new(){UICamera=new(){orthographicSize=6}};Camera.main=new(){orthographicSize=6};Zoom.Postfix();Check(oldUi.orthographicSize==3&&Camera.main.orthographicSize==6);});
 Test("a third camera is never modified",()=>{var other=new Camera{orthographicSize=9};Zoomed();Zoom.GameEnded();Check(other.orthographicSize==9);});
 Test("an unrelated external camera-size write is not clobbered",()=>{Zoomed();Camera.main.orthographicSize=9;Zoom.GameEnded();Check(Camera.main.orthographicSize==9&&HudManager.Instance.UICamera.orthographicSize==3);});
 Test("repeated reset does not produce repeated layout events",()=>{Zoomed();Zoom.Reset();int n=ResolutionManager.ResolutionChanged.Calls;for(int i=0;i<20;i++)Zoom.Reset();Check(n==ResolutionManager.ResolutionChanged.Calls);});
 Test("unchanged frames do not trigger layout rebuilds",()=>{Zoomed();int n=ResolutionManager.ResolutionChanged.Calls;for(int i=0;i<100;i++)Zoom.Postfix();Check(n==ResolutionManager.ResolutionChanged.Calls);});
 Test("layout callback failure cannot prevent scale restoration",()=>{Zoomed();ResolutionManager.ResolutionChanged.Throw=true;Zoom.GameEnded();Normal();Check(Logger.Warnings==1);});
 Test("minimized window avoids invalid aspect division",()=>{Screen.height=0;Zoomed();Check(ResolutionManager.ResolutionChanged.Calls==0);Zoom.Reset();Normal();});
 Test("missing world camera still supports UI cleanup",()=>{Camera.main=null;Wheel(-1);Check(HudManager.Instance.UICamera.orthographicSize==4);Zoom.Reset();Check(HudManager.Instance.UICamera.orthographicSize==3);});
 Test("invalid camera baseline is not captured",()=>{HudManager.Instance.UICamera.orthographicSize=float.NaN;Wheel(-1);Check(Camera.main.orthographicSize==3);});
 Test("controller gate OFF denies living zoom",()=>{TownOfHost.Development.ShellDebugRuntime.ZoomEnabled=false;Denied();});
 Test("controller gate OFF restores living zoom immediately",()=>{Zoomed();TownOfHost.Development.ShellDebugRuntime.ZoomEnabled=false;Zoom.Postfix();Normal();});
 Console.WriteLine($"RESULT {passed} passed, {failed} failed (production Zoom; synthetic Unity/client)");Environment.ExitCode=failed==0?0:1;
 }
}
