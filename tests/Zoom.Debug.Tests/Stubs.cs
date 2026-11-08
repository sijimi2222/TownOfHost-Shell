using System;
namespace HarmonyLib{[AttributeUsage(AttributeTargets.All,AllowMultiple=true)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string m){}}public class HarmonyPrefix:Attribute{}}
namespace UnityEngine{public class Camera{public static Camera main;public float orthographicSize=3;}
 public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public enum KeyCode{LeftAlt,LeftShift}public static class Input{public static Vector2 mouseScrollDelta;public static bool Alt,Shift;public static bool GetKey(KeyCode k)=>k==KeyCode.LeftAlt&&Alt;public static bool GetKeyDown(KeyCode k)=>k==KeyCode.LeftShift&&Shift;}
 public static class Mathf{public static int RoundToInt(float n)=>(int)MathF.Round(n);public static int Max(int a,int b)=>Math.Max(a,b);public static bool Approximately(float a,float b)=>MathF.Abs(a-b)<.0001;}
 public static class Screen{public static int width=1920,height=1080;public static bool fullScreen;}}
namespace InnerNet{public enum Platforms{StandaloneSteamPC,StandaloneEpicPC,Android,IPhone,Switch}public enum NetworkModes{OnlineGame,LocalGame,FreePlay}}
public class Role{}
public class Data{public Role Role=new();public bool Disconnected;}
public class PlayerControl{public static PlayerControl LocalPlayer;public Data Data=new();public bool Alive=true,Ghost,CanMove=true,AmOwner=true;public bool IsAlive()=>Alive;public bool IsGhostRole()=>Ghost;}
public class HudManager{public static HudManager Instance;public UnityEngine.Camera UICamera=new();public void Update(){}public void OnDestroy(){}}
public class AmongUsClient{public enum GameStates{NotJoined,Joined,Started,Ended}public static AmongUsClient Instance;public bool AmHost=true,IsGamePublic;public InnerNet.NetworkModes NetworkMode=InnerNet.NetworkModes.OnlineGame;public GameStates GameState=GameStates.Started;public void OnGameEnd(){}}
public class MainMenuManager{public void Start(){}}
public static class Constants{public static InnerNet.Platforms Platform;public static InnerNet.Platforms GetPlatformType()=>Platform;}
public class LayoutEvent{public int Calls;public bool Throw;public void Invoke(float a,int w,int h,bool f){Calls++;if(Throw)throw new InvalidOperationException("synthetic event");}}
public static class ResolutionManager{public static LayoutEvent ResolutionChanged=new();}
namespace TownOfHost{public static class GameStates{public static bool IsInGame=true,IsInTask=true,IsMeeting,CalledMeeting,Intro,ExiledAnimate;public static bool IsFreePlay=>AmongUsClient.Instance.NetworkMode==InnerNet.NetworkModes.FreePlay;}public class Toggle{public bool Value=true;public bool GetBool()=>Value;}public static class Options{public static Toggle UseZoom=new();}public class MeetingHud{public static MeetingHud Instance;}public static class Logger{public static int Warnings;public static void Warn(string m,string c){Warnings++;}}}

namespace TownOfHost.Development {public static class ShellDebugRuntime {public static bool ZoomEnabled=true;}}
