global using static TownOfHost.Translator;
using TownOfHost; using TownOfHost.Roles.Core; using UnityEngine;
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class|AttributeTargets.Method,AllowMultiple=true)] public class HarmonyPatch:Attribute{public HarmonyPatch(params object[] x){}}
 public class HarmonyPriority:Attribute{public HarmonyPriority(int x){}}
 public class HarmonyArgument:Attribute{public HarmonyArgument(int x){}}
}
namespace AmongUs.GameOptions {
 public enum RoleTypes {Crewmate,Impostor,Phantom,Engineer,Scientist,Shapeshifter}
 public interface IGameOptions{}
}
namespace UnityEngine {
 public struct Color{public static Color yellow,gray; }
 public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static implicit operator Vector2(Vector3 v)=>new(v.x,v.y);public static float Distance(Vector2 a,Vector2 b)=>MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}
 public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}}
 public static class Time{public static float time=100,fixedDeltaTime=.02f;}
}
namespace Hazel {
 public enum SendOption{None,Reliable}
 public class MessageWriter {
  public readonly MemoryStream Buffer=new();public List<object> Values=new();readonly Stack<int> frames=new();public SendOption SendOption;public bool Recycled;
  public int Length=>(int)Buffer.Length;public static MessageWriter Get(SendOption option){var w=new MessageWriter{SendOption=option};w.Buffer.WriteByte(option==SendOption.Reliable?(byte)1:(byte)0);if(option==SendOption.Reliable){w.Buffer.WriteByte(0);w.Buffer.WriteByte(0);}return w;}
  public void StartMessage(byte tag){frames.Push((int)Buffer.Position);Buffer.WriteByte(0);Buffer.WriteByte(0);Buffer.WriteByte(tag);}
  public void EndMessage(){int start=frames.Pop();long end=Buffer.Position;int n=(int)end-start-3;Buffer.Position=start;Buffer.WriteByte((byte)n);Buffer.WriteByte((byte)(n>>8));Buffer.Position=end;}
  public void Write(byte v){Values.Add(v);Buffer.WriteByte(v);}public void Write(bool v){Values.Add(v);Buffer.WriteByte(v?(byte)1:(byte)0);}public void Write(int v){Values.Add(v);Buffer.Write(BitConverter.GetBytes(v));}public void Write(uint v){Values.Add(v);Buffer.Write(BitConverter.GetBytes(v));}public void Write(float v){Values.Add(v);Buffer.Write(BitConverter.GetBytes(v));}
  public void Write(string v){Values.Add(v);byte[] bytes=System.Text.Encoding.UTF8.GetBytes(v);WritePacked(bytes.Length);Buffer.Write(bytes);}public void Write(byte[] v)=>Buffer.Write(v);
  public void WritePacked(int v)=>WritePacked((uint)v);public void WritePacked(uint v){while(v>=128){Buffer.WriteByte((byte)(v|128));v>>=7;}Buffer.WriteByte((byte)v);}
  public byte[] ToByteArray(bool header)=>Buffer.ToArray();public void Recycle(){Recycled=true;}
 }
 public class MessageReader {
  readonly byte[] buffer;public int Position;public int Length=>buffer.Length;
  public MessageReader(params object[] values){var writer=new MessageWriter();foreach(var value in values){switch(value){case int n:writer.Write(n);break;case byte b:writer.Write(b);break;case bool flag:writer.Write(flag);break;case string text:writer.Write(text);break;case byte[] raw:writer.Write(raw);break;default:throw new Exception("unsupported fixture type");}}buffer=writer.ToByteArray(true);}
  void Require(int count){if(count<0||Position+count>Length)throw new EndOfStreamException();}
  public byte ReadByte(){Require(1);return buffer[Position++];}public bool ReadBoolean()=>ReadByte()!=0;
  public int ReadInt32(){Require(4);int n=BitConverter.ToInt32(buffer,Position);Position+=4;return n;}
  public int ReadPackedInt32(){uint n=0;for(int i=0;i<5;i++){byte b=ReadByte();n|=(uint)(b&127)<<(i*7);if((b&128)==0)return (int)n;}throw new InvalidDataException();}
  public string ReadString(){int n=ReadPackedInt32();Require(n);string text=System.Text.Encoding.UTF8.GetString(buffer,Position,n);Position+=n;return text;}
 }
}
namespace InnerNet {public class InnerNetClient {public void FixedUpdate(){}public virtual void SendOrDisconnect(Hazel.MessageWriter w){}}}
public class AmongUsClient:InnerNet.InnerNetClient {
 public static AmongUsClient Instance=new();public bool AmHost=true,AmConnected=true;public int GameId=100,ClientId=10;public List<byte[]> Packets=new();public int ThrowOnSend;
 public List<(int Target,Hazel.MessageWriter Writer)> Rpcs=new();public int RpcTarget;public Hazel.MessageWriter StartRpcImmediately(uint net,byte rpc,Hazel.SendOption option,int target){RpcTarget=target;return new();}public void FinishRpcImmediately(Hazel.MessageWriter w)=>Rpcs.Add((RpcTarget,w));
 public override void SendOrDisconnect(Hazel.MessageWriter w){if(ThrowOnSend>0 && Packets.Count+1==ThrowOnSend)throw new IOException("fixture transport");Packets.Add(w.ToByteArray(true));}
}
public enum RpcCalls:byte{SetName=6,SetColor=8,SetHatStr=9,SetSkinStr=10,SetVisorStr=11}
public class NetworkedPlayerInfo {
 public byte PlayerId;public uint NetId;public bool Disconnected,IsDead;
 public string GetLogPlayerName()=>"SYNTHETIC";public class PlayerOutfit{public int ColorId;public string HatId="hat",SkinId="skin",VisorId="visor";}
}
public class Transform{public Vector3 position;}
public class Palette {public static Color CrewmateBlue;}
public class PlayerControl {
 public static PlayerControl LocalPlayer;public byte PlayerId;public uint NetId;public int ClientId;public bool Mod=true;public CustomRoles Role;public NetworkedPlayerInfo Data=new();public Transform transform=new();public PlayerControl Target;public int Cooldowns;public List<string> Cosmetics=new();
 public void SetColor(int c)=>Cosmetics.Add("color:"+c);public void SetHat(string h,int c)=>Cosmetics.Add("hat:"+h);public void SetSkin(string s,int c)=>Cosmetics.Add("skin:"+s);public void SetVisor(string v,int c)=>Cosmetics.Add("visor:"+v);public byte GetNextRpcSequenceId(RpcCalls rpc)=>1;
}
public enum GameOverReason{Crew,Impostor}
public class HudButton{public bool Visible;public void ToggleVisible(bool value)=>Visible=value;}
public class HudManager{public static bool InstanceExists=true;public static HudManager Instance=new();public HudButton KillButton=new(),ImpostorVentButton=new(),SabotageButton=new();}
public class Minigame{public static Minigame Instance;public T TryCast<T>() where T:class=>this as T;}
public class ShapeshifterMinigame:Minigame{}
public class RoleManager{public static RoleManager Instance=new();public List<(byte Player,AmongUs.GameOptions.RoleTypes Role)> Changes=new();public void SetRole(PlayerControl p,AmongUs.GameOptions.RoleTypes role)=>Changes.Add((p.PlayerId,role));}
public class TranslationController {public string GetString(string key)=>key;}
public class DestroyableSingleton<T> where T:new(){public static T Instance=new();}
namespace TownOfHost.Attributes {public class GameModuleInitializerAttribute:Attribute{}public class PluginModuleInitializerAttribute:Attribute{}}
namespace TownOfHost.Patches {
 public static class PetActionManager {public static Dictionary<byte,Action> Actions=new();public static Dictionary<byte,Action> Handlers=>Actions;public static void Register(byte id,Action action)=>Actions[id]=action;public static void Unregister(byte id)=>Actions.Remove(id);}
 public static class MeetingHudPatch{public static class StartPatch{public static List<(byte Player,string Text,string Title)> meetingsends=new();}}
}
namespace TownOfHost.Roles.AddOns.Common {public static class Amnesia{public static bool Blocked;public static bool CheckAbilityreturn(PlayerControl p)=>Blocked;}}
namespace TownOfHost.Roles.Core.Interfaces {public interface IImpostor{} public interface IUsePhantomButton{}public interface IDoubleTrigger{}public interface IKillFlashSeeable{}public interface IDeathReasonSeeable{}}
namespace TownOfHost.Roles.Crewmate {public class Sheriff{public static OptionItem StartInTaskMode=new();}}
namespace TownOfHost.Roles.Core {
 public enum HasTask{False,True,ForRecompute}public enum From{TownOfHost_hamo}public enum TabGroup{Combinations}
 public class RoleAssignInfo {public RoleAssignInfo(CustomRoles r,CustomRoleTypes t){}public IntegerValueRule AssignCountRule;public CustomRoles[] AssignUnitRoles;public Func<bool> IsInitiallyAssignableCallBack=()=>true;}
 public class SimpleRoleInfo {
  public CustomRoles RoleName;public int ConfigId;public Action OptionCreator;public Color RoleColor;public string RoleColorCode="#fff";public bool IsDesyncImpostor;public RoleAssignInfo AssignInfo;
  public static SimpleRoleInfo Create(Type type,Func<PlayerControl,RoleBase> factory,CustomRoles role,Func<AmongUs.GameOptions.RoleTypes> baseRole,CustomRoleTypes team,int id,Action setup,string alias,string colorCode="",(int,int) OptionSort=default,Func<object> introSound=null,From from=default,bool isNewRole=false,TabGroup tab=default,RoleAssignInfo assignInfo=null,CombinationRoles combination=default)=>new(){RoleName=role,ConfigId=id,OptionCreator=setup,AssignInfo=assignInfo};
 }
 public class RoleBase {
  public PlayerControl Player;public int Tasks,AllTasks=4;public bool IsTaskFinished=>Tasks>=AllTasks;
  public RoleBase(SimpleRoleInfo info,PlayerControl player,Func<HasTask> tasks=null){Player=player;player.Role=info.RoleName;CustomRoleManager.AllActiveRoles[player.PlayerId]=this;}
  public virtual void Add(){}public virtual void OnDestroy(){}public virtual bool OnCompleteTask(uint id)=>true;
  public virtual void OnDead(PlayerControl player){}public virtual void OnMurderPlayerAsTarget(MurderInfo info){}public virtual void CheckWinner(GameOverReason reason){}
  public virtual void OnExileWrapUp(NetworkedPlayerInfo info,ref bool decided){}public virtual void AfterMeetingTasks(){}
  public virtual void ApplyGameOptions(AmongUs.GameOptions.IGameOptions options){}public virtual bool CheckShapeshift(PlayerControl target,ref bool animate)=>true;
  public virtual bool NotifyRolesCheckOtherName=>false;public virtual AmongUs.GameOptions.RoleTypes? AfterMeetingRole=>null;
  public virtual void OnFixedUpdate(PlayerControl player){}public virtual void OnReportDeadBody(PlayerControl reporter,NetworkedPlayerInfo body){}
  public virtual string GetSuffix(PlayerControl seer,PlayerControl seen=null,bool isForMeeting=false)=>"";
  public virtual string GetMark(PlayerControl seer,PlayerControl seen=null,bool isForMeeting=false)=>"";
  public virtual string GetProgressText(bool comms=false,bool gamelog=false)=>"";public virtual string GetAbilityButtonText()=>"";
  public virtual string MeetingAddMessage()=>"";public virtual void ReceiveRPC(Hazel.MessageReader r){}public virtual CustomRoles TellResults(PlayerControl p)=>CustomRoles.Crewmate;
  protected Sender CreateSender(){var sender=new Sender();CustomRoleManager.RoleSends.Add(sender.Writer);return sender;}protected static object GetIntroSound(AmongUs.GameOptions.RoleTypes type)=>null;
 }
 public class Sender:IDisposable {public Hazel.MessageWriter Writer=new();public void Dispose(){}}
 public class MurderInfo{}
 public static class CustomRoleManager {public static Dictionary<byte,RoleBase> AllActiveRoles=new();public static List<Hazel.MessageWriter> RoleSends=new();}
}
namespace TownOfHost {
 public enum CustomRoles {Crewmate=0,Impostor=1,LessThan=376,MadKind=377,Framer=378,NiceHacker=379,Romeo=380,Juliet=381,NotAssigned=500}
 public enum CustomRoleTypes{Crewmate,Impostor,Madmate,Neutral}public enum CombinationRoles{None,RomeoandJuliet}
 public enum CustomWinner {Crewmate=0,Impostor=1,LessThan=376,Romeo=380,Default=-2,None=-3,Draw=-1}
 public enum CustomRPC:byte {CustomRoleSync=81}
 public enum GeneralOption{KillCooldown,Cooldown,OptionCount,CanVent}public enum OptionFormat{Seconds,Times}public enum OptionZeroNotation{Hyphen}
 public record FloatValueRule(float Min,float Max,float Step);public record IntegerValueRule(int Min,int Max,int Step);
 public class OptionItem {
  public int Id;public string Name;public float Value;public Func<string> Display;public bool GetBool()=>Value!=0;public int GetInt()=>(int)Value;public float GetFloat()=>Value;
  public OptionItem SetValueFormat(OptionFormat x)=>this;public OptionItem SetZeroNotation(OptionZeroNotation x)=>this;public OptionItem SetOptionName(Func<string> x){Display=x;return this;}public OptionItem RegisterUpdateValueEvent(Action<object,object> callback)=>this;
 }
 public static class TestOptions {public static Dictionary<(CustomRoles,string),OptionItem> All=new();public static OptionItem Add(SimpleRoleInfo info,int offset,object name,float value){var option=new OptionItem{Id=info.ConfigId+offset,Name=name.ToString(),Value=value};All.Add((info.RoleName,name.ToString()),option);return option;}}
 public static class FloatOptionItem{public static OptionItem Create(SimpleRoleInfo info,int offset,object name,FloatValueRule rule,float value,bool single,OptionItem parent=null)=>TestOptions.Add(info,offset,name,value);}
 public static class IntegerOptionItem{public static OptionItem Create(SimpleRoleInfo info,int offset,object name,IntegerValueRule rule,int value,bool single,OptionItem parent=null)=>TestOptions.Add(info,offset,name,value);}
 public static class BooleanOptionItem{public static OptionItem Create(SimpleRoleInfo info,int offset,object name,bool value,bool single,OptionItem parent=null)=>TestOptions.Add(info,offset,name,value?1:0);}
 public class SoloWinOption {
  public static Dictionary<CustomRoles,SoloWinOption> AllData=new();public OptionItem OptionWin;
  public static SoloWinOption Create(SimpleRoleInfo info,int offset,int defo=0){var data=new SoloWinOption{OptionWin=TestOptions.Add(info,offset,"WinPriority",defo)};AllData.Add(info.RoleName,data);return data;}
 }
 public static class OverrideTasksData{public static (bool,int,int,int) Last;public static void Create(SimpleRoleInfo info,int offset,(bool,int,int,int) tasks){Last=tasks;}}
 public static class Options{public static OptionItem MadmateCanSeeKillFlash=new(),MadmateCanSeeDeathReason=new();public static CustomRoles MadTellOpt()=>CustomRoles.Impostor;}
 public static class AURoleOptions {public static float PhantomCooldown,ShapeshifterCooldown,ShapeshifterDuration;}
 public static class Main{public static bool DebugVersion;public static float LagTime=.1f;}
 public static class GameStates{public static bool InGame=true,IsInGame=true,IsInTask=true,IsMeeting,CalledMeeting,Intro;}
 public static class PlayerCatch{public static Dictionary<byte,PlayerControl> Players=new();public static IEnumerable<PlayerControl> AllPlayerControls=>Players.Values;public static IEnumerable<PlayerControl> AllAlivePlayerControls=>Players.Values.Where(p=>p.IsAlive());public static PlayerControl GetPlayerById(byte id)=>Players.GetValueOrDefault(id);}
 public static class Utils {
  public static List<(string Text,byte To)> Messages=new();public static string ColorString(Color color,string text)=>text;
  public static void SendMessage(string text,byte to=255,string title="")=>Messages.Add((text,to));
 }
 public static class UtilsNotifyRoles{public static void NotifyRoles(bool OnlyMeName=false,PlayerControl SpecifySeer=null,bool ForceLoop=false){}}
 public static class UtilsRoleText {public static Color GetRoleColor(CustomRoles role)=>default;public static string GetRoleColorAndtext(CustomRoles role)=>role.ToString();}
 public static class UtilsGameLog{public static void AddGameLog(string name,string text){}}
 public static class Logger{public static List<string> Logs=new();public static void Info(string text,string tag)=>Logs.Add(text);public static void Warn(string text,string tag)=>Logs.Add(text);public static void Error(string text,string tag)=>Logs.Add(text);}
 public static class Translator{public static string GetString(string key)=>key=="NiceHackerAdminTitle"?"Admin {0}":key;}
 public static class Camouflage{public static bool IsCamouflage;public static Dictionary<byte,NetworkedPlayerInfo.PlayerOutfit> PlayerSkins=new();}
 public class LateTask{public static List<Action> Pending=new();public LateTask(Action action,float seconds,string name,bool log=false)=>Pending.Add(action);public static void Run(){var copy=Pending.ToArray();Pending.Clear();foreach(var action in copy)action();}}
 public static class TargetArrow{public static void Add(byte a,byte b){}public static string GetArrows(PlayerControl p,byte other)=>"arrow";}
 public static class AdminProvider {public class Entry{public string Room="Room";public int TotalPlayers=1,NumDeadBodies;}public static Dictionary<string,Entry> Entries=new();public static Dictionary<string,Entry> CalculateAdmin()=>Entries;}
 public class CustomRpcSender {
  public static List<CustomRpcSender> Sent=new();public List<int> Destinations=new();public List<object> Values=new();public static CustomRpcSender Create(string name)=>new();
  public CustomRpcSender AutoStartRpc(uint net,byte rpc,int target){Destinations.Add(target);return this;}public CustomRpcSender Write(object x){Values.Add(x);return this;}public CustomRpcSender EndRpc()=>this;public void SendMessage()=>Sent.Add(this);
 }
 public static class InnerNetClientPatch{public static bool DontTouch;}public static class AntiBlackout{public static bool IsCached;}
 public static class Extensions {
  public static bool IsAlive(this PlayerControl p)=>p?.Data!=null&&!p.Data.Disconnected&&!p.Data.IsDead;
  public static bool Is(this PlayerControl p,CustomRoles role)=>p?.Role==role;public static CustomRoles GetCustomRole(this PlayerControl p)=>p.Role;public static bool IsImpostor(this CustomRoles role)=>role is CustomRoles.Impostor or CustomRoles.Framer;
  public static bool IsModClient(this PlayerControl p)=>p.Mod;public static RoleBase GetRoleClass(this PlayerControl p)=>CustomRoleManager.AllActiveRoles.GetValueOrDefault(p.PlayerId);public static int GetClientId(this PlayerControl p)=>p.ClientId;public static void AddDoubleTrigger(this PlayerControl p){}
  public static string GetNameWithRole(this PlayerControl p)=>"Synthetic";public static string RemoveHtmlTags(this string text)=>text;public static string Mark(this string text,Color c)=>text;public static string GetPlayerColor(this PlayerControl p)=>"Color";
  public static PlayerControl GetKillTarget(this PlayerControl p,bool ignore)=>p.Target;public static void SetKillCooldown(this PlayerControl p,float cooldown=0,bool delay=false)=>p.Cooldowns++;
  public static AmongUs.GameOptions.RoleTypes GetRoleTypes(this CustomRoles role)=>role.IsImpostor()?AmongUs.GameOptions.RoleTypes.Impostor:AmongUs.GameOptions.RoleTypes.Crewmate;
  public static void RpcSetRoleDesync(this PlayerControl p,AmongUs.GameOptions.RoleTypes role,int client)=>RoleManager.Instance.Changes.Add((p.PlayerId,role));
 }
}
