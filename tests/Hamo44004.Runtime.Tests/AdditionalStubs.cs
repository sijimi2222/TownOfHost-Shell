global using Object = UnityEngine.Object;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace UnityEngine {
 public partial struct Vector2 {
  public static Vector2 left=>new(-1,0);public static Vector2 right=>new(1,0);public static Vector2 up=>new(0,1);public static Vector2 down=>new(0,-1);
  public Vector2 normalized=>magnitude>0?this/magnitude:zero;
 }
 public static partial class Mathf {
  public static float Clamp(float v,float min,float max)=>Math.Clamp(v,min,max);
  public static float Clamp01(float v)=>Math.Clamp(v,0,1);
  public static float Sqrt(float v)=>MathF.Sqrt(v); public static float Sign(float v)=>v<0?-1:1;
 }
 public partial class Color {public static Color yellow=new(),cyan=new(),gray=new();}
 public class Object {public static List<object> Items=new();public static T[] FindObjectsOfType<T>()=>Items.OfType<T>().ToArray();}
 public struct RaycastHit2D {public object collider;public float distance;}
 public static class Physics2D {public static float? HitAt;public static RaycastHit2D Raycast(Vector2 from,Vector2 dir,float distance,int mask)=>HitAt is float d?new(){collider=new object(),distance=d}:default;}
}
public static class StubExtensions {public static Color ShadeColor(this Color c,float f)=>c;}
public partial class NetworkedPlayerInfo {public string PlayerName="synthetic";public PlayerControl Object;}
public partial class PlayerControl {
 public float MaxReportDistance=5f;public bool isDummy,notRealPlayer;public bool walkingToVent;
 public bool IsTestBot()=>false;
 public string GetRealName(bool x)=>Data.PlayerName;
 public PlayerState GetPlayerState()=>PlayerState.GetByPlayerId(PlayerId);
 public bool IsLovers2=>false;
 public void AddDoubleTrigger(){}
 public void Exiled(){Data.IsDead=true;}
 public void RpcProtectedMurderPlayer(PlayerControl target){}
 public void RpcSnapToForced(Vector2 v,Hazel.SendOption option){RpcSnapToForced(v);}
}
public class DeadBody {public bool Reported;public byte ParentId;public Transform transform=new();public GameObject gameObject=new(){Active=true};public Vector2 TruePosition=>transform.position;}
public class Ladder {public bool IsTop;public Ladder Destination;public Transform transform=new();public float UsableDistance=1;}
public enum MapNames {Skeld=0,MiraHQ=1,Polus=2,Dleks=3,Airship=4,Fungle=5}
public static class NormalGameOptionsV12 {public static float[] KillDistances={1,1.8f,2.5f};}
public enum RpcCalls {Exiled=4}
public partial class ShipStatus {public Ladder[] Ladders=Array.Empty<Ladder>();}
public partial class NormalOptions {public float PlayerSpeedMod=1;}
public partial class AmongUsClient {
 public static List<(uint Net,byte Rpc,int Target)> Calls=new();
 public Hazel.MessageWriter StartRpcImmediately(uint net,byte rpc,Hazel.SendOption send,int target=-1){Calls.Add((net,rpc,target));return new();}
 public void FinishRpcImmediately(Hazel.MessageWriter writer){}
}
namespace AmongUs.GameOptions {public static partial class ExtraRoleOptions { } }
namespace TownOfHost {
 public static partial class GameStates {public static bool InGame=true,Intro,IsEnded;}
 public static partial class Palette {public static Color DisabledGrey=new(),CrewmateBlue=new();}
 public static partial class Constants {public const int ShipOnlyMask=2;}
 public partial class OptionItem {
  public string Name;public Dictionary<string,string> ReplacementDictionary;
  public OptionItem SetParent(OptionItem p)=>this;public OptionItem SetSubRoleOptionItem(CustomRoles r)=>this;
  public OptionItem SetOptionName(Func<string> f)=>this;
 }
 public static class ObjectOptionitem {public static OptionItem Create(int id,string key,bool d,string title,TabGroup tab)=>new(){Id=id};}
 public enum TabGroup {Addons}
 public static partial class Options {
  public static OptionItem SetupRoleOptions(int id,TabGroup tab,CustomRoles role){var o=new OptionItem{Id=id};OptionItem.All.Add(id,o);return o;}
 }
 public static class UtilsRoleText {public static Color GetRoleColor(CustomRoles r)=>new();public static string GetRoleColorAndtext(CustomRoles r)=>r.ToString();}
 public static partial class UtilsName {public static string GetPlayerColor(byte id,bool x)=>"p"+id;}
 public static class UtilsGameLog {public static void AddGameLog(string tag,string m){}}
 public static class MeetingHudPatch {public static class StartPatch {public static List<(byte,string,string)> meetingsends=new();}}
 public static partial class ReportDeadBodyPatch {public static int Meetings;public static void ExReportDeadBody(PlayerControl reporter,NetworkedPlayerInfo target=null){Meetings++;}}
 public static partial class CustomWinnerHolder {
  public static HashSet<byte> CantWinPlayerIds=new();
  public static void ResetAndSetAndChWinner(CustomWinner w,byte id,bool alive){WinnerTeam=w;}
 }
 public static class CustomRoleHelpers {
  public static CustomRoleTypes GetCustomRoleTypes(this CustomRoles r)=>r.IsImpostor()?CustomRoleTypes.Impostor:CustomRoleTypes.Crewmate;
 }
}
namespace TownOfHost.Roles.AddOns.Common {
 public static class AddOnsAssignData {public static void Create(int id,CustomRoles r,bool a,bool b,bool c,bool d){}}
}
namespace TownOfHost.Roles.Madmate {public static class MadCleaner {public static HashSet<byte> Cleaned=new();public static bool IsCleaned(byte id)=>Cleaned.Contains(id);}}
namespace TownOfHost.Roles.Core {
 public partial class MurderInfo {
  public PlayerControl AppearanceKiller,AppearanceTarget;public bool IsSuicide=>AttemptKiller==AttemptTarget;public bool IsCanKilling=true;
 }
 public abstract partial class RoleBase {
  public bool Is(PlayerControl pc)=>pc!=null&&Player!=null&&pc.PlayerId==Player.PlayerId;
  public virtual bool CanUseAbilityButton()=>true;public virtual void StartGameTasks(){}
  public virtual string GetMark(PlayerControl seer,PlayerControl seen=null,bool isForMeeting=false)=>"";
  public bool IsTaskFinished=>MyTaskState.Completed>=3;
  protected static object GetIntroSound(AmongUs.GameOptions.RoleTypes r)=>null;
 }
 public static partial class CustomRoleManager {
  public static HashSet<Func<PlayerControl,PlayerControl,bool,string>> MarkOthers=new();
  public static int CompletedNoBody;public static byte LastKiller;
  public static void OnMurderPlayer(PlayerControl killer,PlayerControl target){CompletedNoBody++;LastKiller=killer.PlayerId;}
 }
}
namespace TownOfHost.Roles.Core.Interfaces {
 public interface IAdditionalWinner {bool CheckWin(ref CustomRoles role);}
 public interface IDoubleTrigger{}
}
namespace Hazel {
 public partial class MessageReader {public int ReadInt32(){byte[] b=new byte[4];for(int i=0;i<4;i++)b[i]=ReadByte();return BitConverter.ToInt32(b);}public int ReadPackedInt32()=>ReadInt32();}
}

namespace TownOfHost {public static partial class BooleanOptionItem {
 public static OptionItem Create(int id,string key,bool def,TabGroup tab,bool single){var o=new OptionItem{Id=id,Name=key,Value=def?1:0};OptionItem.Registrations.Add(id);OptionItem.All.Add(id,o);return o;}
}}
