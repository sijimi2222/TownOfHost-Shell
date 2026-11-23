global using static TownOfHost.Translator;
using TownOfHost;
using TownOfHost.Roles.Core;
namespace AmongUs.GameOptions { public enum RoleTypes { Crewmate, Impostor } }
namespace Hazel {
 public enum SendOption {None,Reliable}
 public sealed class MessageWriter {public List<object> Values=new();public int Target;public byte Rpc;public void Write(int x)=>Values.Add(x);public void Write(byte x)=>Values.Add(x);public void Write(bool x)=>Values.Add(x);public void WritePacked(int x)=>Values.Add(x);}
 public sealed class MessageReader {readonly Queue<object> q;public int Position;public int Length;public MessageReader(params object[] x){q=new(x);Length=x.Length;}object Next(){Position++;return q.Dequeue();}public bool ReadBoolean()=>(bool)Next();public int ReadInt32()=>(int)Next();public int ReadPackedInt32()=>(int)Next();public byte ReadByte()=>(byte)Next();}
}
public class PlayerControl {static int next;public int NativeId=++next;public byte PlayerId;public uint NetId;public bool isDummy,notRealPlayer,Bot,Mod;public int ClientId=20;public NetworkedPlayerInfo Data=new();public CustomRoles Role=CustomRoles.Crewmate;public CountTypes Count=CountTypes.Crew;public static PlayerControl LocalPlayer;public int CooldownCalls;public void SetKillCooldown(bool delay=true)=>CooldownCalls++;public static bool operator==(PlayerControl a,PlayerControl b)=>ReferenceEquals(a,b)||(!ReferenceEquals(a,null)&&!ReferenceEquals(b,null)&&a.NativeId==b.NativeId);public static bool operator!=(PlayerControl a,PlayerControl b)=>!(a==b);public override bool Equals(object o)=>ReferenceEquals(this,o);public override int GetHashCode()=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);}
public class NetworkedPlayerInfo {public bool Disconnected,IsDead;}
public class MeetingHud {public static MeetingHud Instance;public PlayerVoteArea[] playerStates=Array.Empty<PlayerVoteArea>();public void Update(){}}
public class PlayerVoteArea {public byte PlayerId;public TextStub NameText=new();}public class TextStub {public string text="FAKE_PLAYER";}
public class AmongUsClient {public static AmongUsClient Instance=new();public bool AmHost=true;public List<Hazel.MessageWriter> Sent=new();public Hazel.MessageWriter StartRpcImmediately(uint net,byte rpc,Hazel.SendOption option,int target=-1)=>new(){Target=target,Rpc=rpc};public void FinishRpcImmediately(Hazel.MessageWriter writer)=>Sent.Add(writer);}
namespace TownOfHost.Attributes {public class GameModuleInitializerAttribute:Attribute{}public class PluginModuleInitializerAttribute:Attribute{}}
namespace TownOfHost {
 public enum CustomRoles {Crewmate=0,Impostor=1,GM=2,OtherNeutral=3,CrewKiller=4,Jackal=5,LessThan=376,Tiger=498,NotAssigned=500}
 public enum CustomWinner {Crewmate=0,Impostor=1,OtherNeutral=3,LessThan=376,Tiger=498,Default=-2,Draw=-1,None=-3}
 public enum CustomRoleTypes {Crewmate,Impostor,Neutral}
 public enum CountTypes {OutOfGame,None,Crew,Impostor,Jackal,Hunter,Remotekiller,TaskPlayer,GrimReaper,Fox,MilkyWay,Pavlov,Eater,Victim,Monika,StandMaster,Villain,Daoshi,LessThan}
 public enum GeneralOption {KillCooldown,CanVent}public enum OptionFormat{Seconds,Times}
 public enum CustomRPC:byte {CustomRoleSync=81}
 public enum CustomDeathReason{Kill,Suicide}
 public record FloatValueRule(float MinValue,float MaxValue,float Step);
 public record IntegerValueRule(int MinValue,int MaxValue,int Step);
 public record ClampedFloatValueRule(float Min,float Max,float Increment):FloatValueRule(Min,Max,Increment);
 public record ClampedIntegerValueRule(int Min,int Max,int Increment):IntegerValueRule(Min,Max,Increment);
 public class OptionItem {public Func<string> DisplayName;public OptionItem SetOptionName(Func<string> value){DisplayName=value;return this;}public int Id;public string Name;public float Value;public object Rule;public OptionItem Parent;public OptionFormat Format;public int GetInt()=>(int)Value;public float GetFloat()=>Value;public bool GetBool()=>Value!=0;public OptionItem SetValueFormat(OptionFormat f){Format=f;return this;}}
 public static class TestOptions {public static Dictionary<string,OptionItem> All=new();public static OptionItem Add(SimpleRoleInfo r,int offset,string name,float value,object rule,OptionItem parent){var o=new OptionItem{Id=r.ConfigId+offset,Name=name,Value=value,Rule=rule,Parent=parent??r.RoleOption};All.Add(name,o);return o;}}
 public static class FloatOptionItem {public static OptionItem Create(SimpleRoleInfo r,int n,Enum name,FloatValueRule rule,float d,bool header)=>TestOptions.Add(r,n,name.ToString(),d,rule,null);}
 public static class IntegerOptionItem {public static OptionItem Create(SimpleRoleInfo r,int n,Enum name,IntegerValueRule rule,int d,bool header,OptionItem parent=null)=>TestOptions.Add(r,n,name.ToString(),d,rule,parent);}
 public static class BooleanOptionItem {public static OptionItem Create(SimpleRoleInfo r,int n,Enum name,bool d,bool header,OptionItem parent=null)=>TestOptions.Add(r,n,name.ToString(),d?1:0,null,parent);}
 public class SoloWinOption {public static Dictionary<CustomRoles,SoloWinOption> AllData=new();public OptionItem OptionWin;public static SoloWinOption Create(SimpleRoleInfo r,int n,int defo,IntegerValueRule rule){var o=TestOptions.Add(r,n,"SoloWinOption",defo,rule,null);return AllData[r.RoleName]=new(){OptionWin=o};}}
 public static class Options {public const float DefaultKillCooldown=30;}
 public static class Main {public static bool DebugVersion;}
 public static class GameStates {public static bool InGame=true,IsInTask=true,Intro,IsMeeting,CalledMeeting,IsOutro,ExiledAnimate;}
 public static class PlayerCatch {public static Dictionary<byte,PlayerControl> Players=new();public static IEnumerable<PlayerControl> AllPlayerControls=>Players.Values;public static IEnumerable<PlayerControl> AllAlivePlayerControls=>Players.Values.Where(p=>p.IsAlive());public static PlayerControl GetPlayerById(byte id)=>Players.GetValueOrDefault(id);}
 public interface IRandom {static IRandom Instance {get;set;}=new TestRandom();int Next(int min,int max);}
 public sealed class TestRandom:IRandom {public Queue<int> Values=new();public int Calls;public int Next(int min,int max){if(min!=1||max!=101)throw new Exception("incorrect random bounds");Calls++;return Values.Count>0?Values.Dequeue():50;}}
 public static class Logger {public static List<string> Messages=new();public static void Info(string x,string tag)=>Messages.Add(x);public static void Error(string x,string tag)=>Messages.Add(x);}
 public static class UtilsRoleText {public static string GetRoleColorAndtext(CustomRoles role)=>role.ToString();}
 public static class UtilsGameLog {public static void AddGameLog(string x,string y){}}
 public static class Translator {public static string GetString(string key)=>key;}
 public static class Extensions {public static bool IsAlive(this PlayerControl p)=>p!=null&&p.Data!=null&&!p.Data.Disconnected&&!p.Data.IsDead;public static bool Is(this PlayerControl p,CustomRoles role)=>p.Role==role;public static bool IsTestBot(this PlayerControl p)=>p.Bot;public static bool IsModClient(this PlayerControl p)=>p.Mod;public static int GetClientId(this PlayerControl p)=>p.ClientId;public static RoleBase GetRoleClass(this PlayerControl p)=>CustomRoleManager.AllActiveRoles.GetValueOrDefault(p.PlayerId);public static CustomRoles GetCustomRole(this PlayerControl p)=>p.Role;public static CountTypes GetCountTypes(this PlayerControl p)=>p.Count;public static bool IsImpostor(this CustomRoles r)=>r==CustomRoles.Impostor;public static bool IsNeutral(this CustomRoles r)=>r is CustomRoles.OtherNeutral or CustomRoles.Jackal or CustomRoles.LessThan or CustomRoles.Tiger;public static bool IsNeutralKiller(this PlayerControl p)=>p.Role is CustomRoles.Jackal or CustomRoles.LessThan or CustomRoles.Tiger;}
}
namespace TownOfHost.Modules {
 public static class SelfVoteManager {public static bool Allowed=true;public static bool Canuseability()=>Allowed;}
 public static class UtilsNotifyRoles {public static List<(bool OnlyMe,PlayerControl Seer)> Calls=new();public static void NotifyRoles(bool OnlyMeName=false,PlayerControl SpecifySeer=null)=>Calls.Add((OnlyMeName,SpecifySeer));}
}
namespace TownOfHost.Roles.AddOns.Common {public static class Amnesia {public static HashSet<byte> Blocked=new();public static bool CheckAbility(PlayerControl p)=>!Blocked.Contains(p.PlayerId);}}
namespace TownOfHost.Roles.Core {
 public enum HasTask {False,True}public enum From {None,TownOfHost_Shell}
 public class SimpleRoleInfo {public CustomRoles RoleName;public Func<PlayerControl,RoleBase> Factory;public Func<AmongUs.GameOptions.RoleTypes> BaseRoleType;public CustomRoleTypes Team;public int ConfigId;public Action OptionCreator;public string ChatCommand,Color;public From From;public CountTypes CountType;public bool Desync,New;public OptionItem RoleOption=new();public static SimpleRoleInfo Create(Type type,Func<PlayerControl,RoleBase> create,CustomRoles role,Func<AmongUs.GameOptions.RoleTypes> baseRole,CustomRoleTypes team,int id,Action setup,string alias,string colorCode="",(int,int) OptionSort=default,bool isDesyncImpostor=false,CountTypes countType=CountTypes.Crew,From from=From.None,bool isNewRole=false)=>new(){RoleName=role,Factory=create,BaseRoleType=baseRole,Team=team,ConfigId=id,OptionCreator=setup,ChatCommand=alias,Color=colorCode,From=from,CountType=countType,Desync=isDesyncImpostor,New=isNewRole};public string RoleColorCode=>Color;}
 public class RoleBase {public PlayerControl Player;public RoleBase(SimpleRoleInfo info,PlayerControl p,Func<HasTask> tasks=null){Player=p;if(info!=null){p.Role=info.RoleName;p.Count=info.CountType;}CustomRoleManager.AllActiveRoles[p.PlayerId]=this;}public virtual void Add(){}public virtual void OnSpawn(bool initialState=false){}public virtual void OnFixedUpdate(PlayerControl p){}public virtual void OnDestroy(){}public virtual void OnStartMeeting(){}public virtual void AfterMeetingTasks(){}public virtual void ReceiveRPC(Hazel.MessageReader reader){}public virtual string GetMark(PlayerControl seer,PlayerControl seen=null,bool isForMeeting=false)=>"";}
 public class MurderInfo {public PlayerControl AttemptKiller,AttemptTarget;public bool IsSuicide=>AttemptKiller==AttemptTarget;public bool DoKill=true,CanKill=true;public int KillPower=1,GuardPower;}
 public static class CustomRoleManager {public static Dictionary<byte,RoleBase> AllActiveRoles=new();public static int Suicides;public static bool OnCheckMurder(PlayerControl killer,PlayerControl target,PlayerControl ak,PlayerControl at,bool force,bool dontRole,int power,CustomDeathReason reason){if(killer!=target||!force||!dontRole||power!=99||reason!=CustomDeathReason.Suicide)throw new Exception("unsafe suicide");Suicides++;target.Data.IsDead=true;return true;}}
}

namespace HarmonyLib{public class HarmonyPatch:Attribute{public HarmonyPatch(Type type,string name){}}}
namespace TownOfHost{public static class ChatUpdatePatch{public static bool BlockSendName;}public static class NameColorManager{public static List<(PlayerControl Target,PlayerControl Seer)> Calls=new();public static void RpcMeetingColorName(PlayerControl target=null,PlayerControl onlySeer=null)=>Calls.Add((target,onlySeer));}}
