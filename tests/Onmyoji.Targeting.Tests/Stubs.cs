using System;
using System.Collections.Generic;
using System.Linq;
namespace TownOfHost {
 public enum CustomRoleTypes { Crewmate, Impostor, Neutral }
 public class PlayerControl {
  public byte PlayerId; public CustomRoles Role; public CustomRoleTypes Faction=CustomRoleTypes.Neutral; public bool Alive=true,KillEnabled=true; public object State;
  public bool IsAlive()=>Alive; public bool Is(CustomRoles r)=>Role==r; public bool Is(CustomRoleTypes f)=>Faction==f;
  public CustomRoles GetCustomRole()=>Role; public object GetRoleClass()=>State;
  public void RpcSetCustomRole(CustomRoles r,object log=null){Role=r;State=new Roles.Neutral.Shikigami();TestEvents.Conversions++;}
 }
 public static class PlayerCatch { public static Dictionary<byte,PlayerControl> Players=new(); public static IEnumerable<PlayerControl> AllPlayerControls=>Players.Values; public static PlayerControl GetPlayerById(byte id)=>Players.GetValueOrDefault(id); }
 public class TestRoleBase { public PlayerControl Player; public bool Is(PlayerControl p)=>p?.PlayerId==Player?.PlayerId; public virtual bool CheckVoteAsVoter(byte id,PlayerControl voter)=>true; public virtual string GetMark(PlayerControl a,PlayerControl b=null,bool isForMeeting=false)=>""; }
 public class MurderInfo {public PlayerControl AttemptKiller,AttemptTarget;public bool IsSuicide,DoKill=true;public (PlayerControl,PlayerControl) AttemptTuple=>(AttemptKiller,AttemptTarget);}
 public class Option {public bool Value=true;public float Float=20;public bool GetBool()=>Value;public float GetFloat()=>Float;}
 public static class TestEvents {public static int Conversions,RoleSync,Notifications;public static List<string> Messages=new(); public static void Reset(){Conversions=RoleSync=Notifications=0;Messages.Clear();}}
 public static class Utils {public static List<byte> RoleSendList=new();public static void SendMessage(string text,byte id)=>TestEvents.Messages.Add(text);}
 public static class Translator {public static string GetString(string key)=>key;}
 public static class UtilsNotifyRoles {public static void NotifyRoles(bool OnlyMeName=false,PlayerControl SpecifySeer=null)=>TestEvents.Notifications++;}
 public static class UtilsName {public static string GetPlayerColor(PlayerControl p,bool b)=>p.PlayerId.ToString();}
 public static class UtilsRoleText {public static string GetRoleColorCode(CustomRoles r)=>"#123456";}
 public static class NameColorManager {public static Dictionary<(byte,byte),string> Colors=new();public static void RemoveAll(byte id){foreach(var k in Colors.Keys.Where(k=>k.Item1==id).ToArray())Colors.Remove(k);}public static void Add(byte a,byte b,string color)=>Colors[(a,b)]=color;}
 public static class TargetArrow {public static List<(byte,byte)> Links=new();public static void Add(byte a,byte b)=>Links.Add((a,b));public static string GetArrows(PlayerControl a,byte b)=>Links.Contains((a.PlayerId,b))?"→":"";}
 public class LateTask {public static List<Action> Pending=new();public LateTask(Action action,float delay,string name="",bool log=false)=>Pending.Add(action);public static void Drain(){foreach(var a in Pending.ToArray()){Pending.Remove(a);a();}}}
}
namespace TownOfHost.Modules {
 public static class SelfVoteManager {public enum VoteStatus{Self,Skip,Vote}public static bool Active; public static bool CheckSelfVoteMode(TownOfHost.PlayerControl p,byte id,out VoteStatus status){status=id==p.PlayerId?VoteStatus.Self:id==253?VoteStatus.Skip:VoteStatus.Vote;return id==p.PlayerId||Active;}public static void SetMode(TownOfHost.PlayerControl p,bool b)=>Active=b;}
}
namespace TownOfHost.Roles.Neutral {
 public class Victim {public bool IsAwakened;public bool PerformingForcedKill;}
 public static class StandMaster {public static bool EnableKillAbility;}
 public static class Opportunist {public static bool HasKillButton;}
 public static class Walkure {public enum RoleChangeSource{Jackal}public static bool Reject;public static bool TryRejectRoleChange(TownOfHost.PlayerControl a,TownOfHost.PlayerControl b,RoleChangeSource source)=>Reject;}
 public class Shikigami {public byte Owner;public void SetOwner(byte id)=>Owner=id;}
 public partial class Onmyoji {
  bool nominateMode;public List<byte> ShikigamiIds=new();public byte NextShikigamiCandidate=255;float nearTimer,createCooldownTimer;
  static TownOfHost.Option OptionCanCreateShikigami=new(),OptionCreateShikigamiCooldown=new();
  public Onmyoji(TownOfHost.PlayerControl p){Player=p;}
  void ApplyModeDesync(bool value){nominateMode=value;}
  void SendRPC()=>TownOfHost.TestEvents.RoleSync++;
  public bool Killer(TownOfHost.PlayerControl p)=>IsOnmyojiKillerTarget(p);public bool Valid(TownOfHost.PlayerControl p)=>IsValidShikigamiTarget(p);public bool Star(TownOfHost.PlayerControl p)=>IsStarReadingTarget(p);
  public void SetNomination(bool value)=>nominateMode=value;public void SetCreation(bool value)=>OptionCanCreateShikigami.Value=value;public void Refresh()=>RefreshStarReadingTargets();
 }
}
