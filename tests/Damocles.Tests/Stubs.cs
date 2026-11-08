global using static TownOfHost.Translator;
namespace UnityEngine {
 public struct Color {public string Hex;public Color(string s){Hex=s;}}
 public static class Mathf {public static float Clamp(float f,float min,float max)=>Math.Clamp(f,min,max);}
 public static class Time {public static float fixedDeltaTime=.02f;}
 public static class Random {public static Queue<float> Values=new();public static int Calls;public static float Range(float min,float max){Calls++;return Values.Count>0?Values.Dequeue():99.999f;}}
}
namespace AmongUs.GameOptions {public enum RoleTypes {Crewmate,Impostor,Phantom}public interface IGameOptions{}public class GameOptions:IGameOptions{}}
namespace Hazel {public enum SendOption{None,Reliable}public class MessageWriter {public List<object> Values=new();public void Write(bool v)=>Values.Add(v);public void Write(float v)=>Values.Add(v);}public class MessageReader {readonly Queue<object> q;public MessageReader(params object[] values){q=new(values);}public bool ReadBoolean()=>(bool)q.Dequeue();public float ReadSingle()=>(float)q.Dequeue();}}
public class PlayerControl {static int nextIdentity;public int NativeIdentity=System.Threading.Interlocked.Increment(ref nextIdentity);public static bool operator ==(PlayerControl a,PlayerControl b)=>ReferenceEquals(a,b)||(!ReferenceEquals(a,null)&&!ReferenceEquals(b,null)&&a.NativeIdentity==b.NativeIdentity);public static bool operator !=(PlayerControl a,PlayerControl b)=>!(a==b);public override bool Equals(object o)=>o is PlayerControl p&&this==p;public override int GetHashCode()=>NativeIdentity;public byte PlayerId;public NetworkedPlayerInfo Data=new();public bool inVent;public float NativeKillTimer;public List<float> AppliedKillTimers=new();public int SyncCalls;public static PlayerControl LocalPlayer;}
public class NetworkedPlayerInfo {public bool IsDead,Disconnected;public string PlayerName="test";}
public class AmongUsClient {public static AmongUsClient Instance=new();public bool AmHost=true;}
public static class Palette {public static UnityEngine.Color ImpostorRed=new("#ff1919");}
namespace TownOfHost.Roles.Core.Interfaces {public interface IImpostor:IKiller{}}
namespace TownOfHost.Roles.Core {
 public enum From{None,TownOfHost_Shell}public class SimpleRoleInfo {
 public Type ClassType;public Func<PlayerControl,RoleBase> CreateInstance;public TownOfHost.CustomRoles RoleName;public Func<AmongUs.GameOptions.RoleTypes> BaseRoleType;public TownOfHost.CustomRoleTypes CustomRoleType;public int ConfigId;public Action OptionCreator;public string ChatCommand;public From From;public bool IsNewRole;
 public static SimpleRoleInfo Create(Type type,Func<PlayerControl,RoleBase> create,TownOfHost.CustomRoles role,Func<AmongUs.GameOptions.RoleTypes> baseRole,TownOfHost.CustomRoleTypes team,int id,Action setup,string alias,From from=From.None,bool isNewRole=false)=>new(){ClassType=type,CreateInstance=create,RoleName=role,BaseRoleType=baseRole,CustomRoleType=team,ConfigId=id,OptionCreator=setup,ChatCommand=alias,From=from,IsNewRole=isNewRole};}
 public class RoleBase {public PlayerControl Player;public RoleBase(SimpleRoleInfo info,PlayerControl p){Player=p;CustomRoleManager.AllActiveRoles[p.PlayerId]=this;}public virtual void ApplyGameOptions(AmongUs.GameOptions.IGameOptions opt){}public virtual string GetProgressText(bool comms=false,bool gamelog=false)=>"";public virtual void ReceiveRPC(Hazel.MessageReader r){}public virtual void OnStartMeeting(){}public virtual void OnFixedUpdate(PlayerControl p){}public virtual void OnDestroy(){}protected Sender CreateSender(Hazel.SendOption option=Hazel.SendOption.None)=>new(option);public void Dispose(){OnDestroy();CustomRoleManager.AllActiveRoles.Remove(Player.PlayerId);Player=null;}}
 public class Sender:IDisposable {public Hazel.MessageWriter Writer=new();public Hazel.SendOption Option;public Sender(Hazel.SendOption o){Option=o;}public void Dispose(){CustomRoleManager.Sent.Add((Option,Writer.Values.ToArray()));}}
 public class MurderInfo {public PlayerControl AttemptKiller,AttemptTarget,AppearanceKiller,AppearanceTarget;public bool DoKill=true,CanKill=true;public bool IsSuicide=>AttemptKiller==AttemptTarget;public (PlayerControl killer,PlayerControl target) AppearanceTuple=>(AppearanceKiller,AppearanceTarget);}
 public static class CustomRoleManager {public static Dictionary<byte,RoleBase> AllActiveRoles=new();public static List<(Hazel.SendOption Option,object[] Values)> Sent=new();public static List<(byte Killer,byte Target,bool? Force,bool? DontRoleAbility,int Power,TownOfHost.CustomDeathReason Reason)> Kills=new();public static bool OnCheckMurder(PlayerControl k,PlayerControl t,PlayerControl a,PlayerControl b,bool? force=false,bool? DontRoleAbility=false,int Killpower=1,TownOfHost.CustomDeathReason deathReason=TownOfHost.CustomDeathReason.Suicide){Kills.Add((k.PlayerId,t.PlayerId,force,DontRoleAbility,Killpower,deathReason));t.Data.IsDead=true;return true;}}
}
namespace TownOfHost {
 public enum CustomRoles{Damocles=373}public enum CustomRoleTypes{Impostor}public enum CustomDeathReason{Suicide}public enum OptionFormat{Seconds,Percent}
 public static class AURoleOptions{public static float PhantomCooldown=30;}
 public class OptionItem {public int Id;public string Name;public float Value;public FloatValueRule Rule;public OptionFormat Format;public float GetFloat()=>Value;public OptionItem SetValueFormat(OptionFormat f){Format=f;return this;}public bool GetBool()=>Value!=0;}
 public record FloatValueRule(float MinValue,float MaxValue,float Step);
 public static class FloatOptionItem {public static Dictionary<string,OptionItem> Registered=new();public static OptionItem Create(Roles.Core.SimpleRoleInfo info,int offset,object name,FloatValueRule rule,float value,bool header){var option=new OptionItem{Id=info.ConfigId+offset,Name=name.ToString(),Value=value,Rule=rule};Registered.Add(option.Name,option);return option;}}
 public static class Options {public static float DefaultKillCooldown=30;public static OptionItem FixFirstKillCooldown=new();}
 public static class Main{public static Dictionary<byte,float> AllPlayerKillCooldown=new();}
 public static class InitialKillCooldownOption {public static bool TryGet(PlayerControl p,out float f){f=0;return false;}}
 public static class MeetingStates {public static bool FirstMeeting;}
 public class PlayerState {public bool Is10secKillButton;public static PlayerState GetByPlayerId(byte id)=>new();}
 public static class GameStates {public static bool InGame=true,IsInTask=true,IsMeeting,CalledMeeting,Intro;}
 public static class PlayerCatch {public static Dictionary<byte,PlayerControl> Players=new();public static PlayerControl GetPlayerById(byte id)=>Players.GetValueOrDefault(id);}
 public static class Utils{public static string ColorString(UnityEngine.Color c,string text)=>text;}
 public static class UtilsNotifyRoles {public static int Calls;public static void NotifyRoles(PlayerControl SpecifySeer=null){Calls++;}}
 public static class Translator {public static string GetString(string key)=>key=="DamoclesKingMode"?"王権":key;}
 public static class Logger {public static void Info(string s,string tag){}}
 public class LateTask {public static List<(Action Run,float Delay,string Name)> Pending=new();public LateTask(Action run,float time,string name="",bool? noLog=false){Pending.Add((run,time,name));}public static void RunAll(){var pending=Pending.ToArray();Pending.Clear();foreach(var task in pending)task.Run();}}
 public static class Extensions {
 public static bool IsAlive(this PlayerControl p)=>p!=null&&p.Data!=null&&!p.Data.IsDead&&!p.Data.Disconnected;
 public static Roles.Core.RoleBase GetRoleClass(this PlayerControl p)=>Roles.Core.CustomRoleManager.AllActiveRoles.GetValueOrDefault(p.PlayerId);
 public static void ResetKillCooldown(this PlayerControl p)=>Main.AllPlayerKillCooldown[p.PlayerId]=(p.GetRoleClass() as Roles.Core.Interfaces.IKiller)?.CalculateKillCooldown()??Options.DefaultKillCooldown;
 public static void SetKillCooldown(this PlayerControl p,float time=-1,PlayerControl target=null,bool force=true,bool delay=true){var cd=time>=0?time:Main.AllPlayerKillCooldown[p.PlayerId];p.NativeKillTimer=cd;p.AppliedKillTimers.Add(cd);Roles.Core.Interfaces.IUsePhantomButton.IPPlayerKillCooldown[p.PlayerId]=0;}
 public static void SyncSettings(this PlayerControl p)=>p.SyncCalls++;
 public static string GetLogPlayerName(this NetworkedPlayerInfo p)=>"test";
 }
}