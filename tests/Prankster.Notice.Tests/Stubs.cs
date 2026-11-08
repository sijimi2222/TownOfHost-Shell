using System;using System.Linq;using System.Collections.Generic;using System.Text.RegularExpressions;using TownOfHost.Roles.Core;using Hazel;
namespace HarmonyLib {public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string s){}}}
namespace UnityEngine {public class Color{} public static class Time{public static float deltaTime=.1f;}}
namespace AmongUs.GameOptions{public enum RoleTypes{Crewmate}}
namespace Hazel{public enum SendOption{None,Reliable} public class MessageReader{public int ReadPackedInt32()=>0;public bool ReadBoolean()=>false;public byte ReadByte()=>0;}}
namespace TownOfHost.Attributes{public class GameModuleInitializer:Attribute{}}
namespace TownOfHost {
public enum RpcCalls{SetName,SendChat,SendChatNote,SendQuickChat}public enum DeathReason{Kill}public enum QuickChatPhraseType{SimplePhrase}public enum StringNames{QCGG}
public enum CustomRoles{Prankster,GM}public enum CustomRoleTypes{Neutral}public enum CustomWinner{Default,Prankster,Draw,None}public enum CustomDeathReason{Spell}
public enum SystemTypes{Reactor}public class ReactorSystemType{}public class HeliSabotageSystem{}public class LifeSuppSystemType{}public class HqHudSystemType{}public class SwitchSystem{}public class HudOverrideSystemType{}
public class PlayerControl{public static PlayerControl LocalPlayer;public byte PlayerId;public uint NetId;public Data Data=new();public string name="Player";public int ClientId=3;public bool Alive=true,isDummy,notRealPlayer,TestBot,Mod;public CustomRoles Role=CustomRoles.Prankster;public bool AmOwner=>this==LocalPlayer;public int GetClientId()=>isDummy?-1:ClientId;public bool IsAlive()=>Alive;public bool Is(CustomRoles r)=>r==CustomRoles.GM&&Role==r;public bool IsTestBot()=>TestBot;public bool IsModClient()=>Mod;public void RpcSetName(string n)=>SetName(n);public void SetName(string n){Data.PlayerName=n;}public void Die(DeathReason d,bool a){Alive=false;Data.IsDead=true;}public string GetRealName(bool isMeeting)=>"Player"+PlayerId;public void SetRealKiller(PlayerControl p){} }
public class NetworkedPlayerInfo{public byte PlayerId;}
public class Data{public bool Disconnected,IsDead;public string PlayerName="Player";public uint NetId;public string GetLogPlayerName()=>PlayerName;public void Serialize(FakeWriter w,bool b){w.Values.Add(IsDead);}}
public static class PlayerCatch{public static List<PlayerControl> AllPlayerControls=new();public static IEnumerable<PlayerControl> AllAlivePlayerControls=>AllPlayerControls.Where(p=>p.Alive);public static PlayerControl GetPlayerById(byte id)=>AllPlayerControls.FirstOrDefault(p=>p.PlayerId==id);}
public class ChatController{public float timeSinceLastMessage;public bool IsOpenOrOpening,Throw;public List<string> Texts=new();public void AddChat(PlayerControl p,string m){if(Throw)throw new Exception("test sink failure");Texts.Add(m);}}
public class HudManager{public static HudManager Instance=new();public ChatController Chat=new();}public static class DestroyableSingleton<T>where T:new(){public static T Instance= (T)(object)HudManager.Instance;}
public class AmongUsClient{public static AmongUsClient Instance=new();public bool AmHost=true;}
public static class Main{public static string ModColor="#abc123";public static List<(string,byte,string)> MessagesToSend=new();public static int MegCount;public static WaitValue MessageWait=new();public static float LagTime;public static Dictionary<(byte,byte),string> LastNotifyNames=new();public static Dictionary<byte,object> AfterMeetingDeathPlayers=new();public static Dictionary<byte,string> AllPlayerNames=new();}public class WaitValue{public float Value=>1;}
public static class GameStates{public static bool CalledMeeting=true,IsMeeting=true,ExiledAnimate,IsOutro;}
public static partial class Utils{public static bool Restriction=true;public static bool IsRestriction()=>Restriction;public static void ApplySuffix(object o,bool b){}public static string ColorString(UnityEngine.Color c,string t)=>t;}
public static class VersionInfoManager{public static bool GetCustomFlag(int n)=>false;}
public static class Options{public static BoolOption ExRpcWeightR=new(),ExHideChatCommand=new(),FirstTurnMeetingCantability=new();public static bool firstturnmeeting;}
public class BoolOption{public bool Enabled;public bool GetBool()=>Enabled;}
public static class UtilsNotifyRoles{public static void NotifyRoles(bool a=false,bool b=false,bool c=false,PlayerControl[] players=null,bool ForceLoop=false){}}
public static class NameColorManager{public static void RpcMeetingColorName(PlayerControl p=null){}public static void Add(byte a,byte b,string color){}}
public static class GameDataSerializePatch{public static int SerializeMessageCount;}
public class LateTask{public LateTask(Action a,float delay,string n,bool b){}}
public static class Extensions{public static string RemoveHtmlTags(this string s)=>Regex.Replace(s,"<.*?>","");public static string RemoveColorTags(this string s)=>s.RemoveHtmlTags();public static string RemoveDeltext(this string s,string d,string r="")=>Regex.Replace(s,d,r);public static string RemoveText(this string s)=>s.RemoveHtmlTags();public static string ApplyNameColorData(this string s,PlayerControl a,PlayerControl b,bool c)=>s;public static void Do<T>(this IEnumerable<T> items,Action<T> action){foreach(var item in items)action(item);}}
public class FakeWriter{public List<object> Values=new();public void StartMessage(int tag){}public void WritePacked(uint n){}public void EndMessage(){}public void Write<T>(T v){}public void WritePacked(int v){} }
public class Packet{public int Target;public List<(uint NetId,RpcCalls Call,List<object> Values)> Rpcs=new();public List<object> Snapshots=new();}
public class CustomRpcSender{public static List<Packet> Sent=new();Packet packet=new();List<object> values;public static CustomRpcSender Create(string n,SendOption o)=>new();public CustomRpcSender StartMessage(int id){packet.Target=id;return this;}public CustomRpcSender StartRpc(uint id,RpcCalls call){values=new();packet.Rpcs.Add((id,call,values));return this;}public CustomRpcSender StartRpc(uint id,byte call)=>StartRpc(id,(RpcCalls)call);public CustomRpcSender Write<T>(T value){values.Add(value);return this;}public CustomRpcSender Write(Action<FakeWriter> f,bool b){var w=new FakeWriter();f(w);packet.Snapshots.AddRange(w.Values);return this;}public CustomRpcSender EndRpc()=>this;public void EndMessage(){}public void SendMessage()=>Sent.Add(packet);}
public static class Logger{public static List<string> Entries=new();public static void Info(string t,string k)=>Entries.Add(k+":"+t);public static void Warn(string t,string k)=>Entries.Add(k+":"+t);}
public static class MeetingHudPatch{public static List<byte> Deaths=new();public static void TryAddAfterMeetingDeathPlayers(CustomDeathReason r,byte id)=>Deaths.Add(id);}
public class MeetingHud{public static MeetingHud Instance=new();public List<Area> playerStates=new();public void Update(){}}
public class Area{public byte PlayerId;public Text NameText=new();}public class Text{public string text="Player";}
public static class CustomWinnerHolder{public static CustomWinner WinnerTeam=CustomWinner.Default;public static HashSet<byte> WinnerIds=new(),NeutralWinnerIds=new();public static HashSet<CustomRoles> AdditionalWinnerRoles=new();public static bool ResetAndSetAndChWinner(CustomWinner w,byte id,bool AddWin){WinnerTeam=w;return true;}}
public class GameManager{public static GameManager Instance=new();public Flow LogicFlow=new();}public class Flow{public void CheckEndCriteria(){}}
public class IRandom{public static IRandom Instance=new();public int Next(int n)=>0;}
public class ClampedIntegerValueRule{public ClampedIntegerValueRule(int a,int b,int c){}}
public class OptionItem{public int Number=5;public bool Flag;public int GetInt()=>Number;public bool GetBool()=>Flag;}
public class IntegerOptionItem{public static OptionItem Create(SimpleRoleInfo i,int n,object name,ClampedIntegerValueRule rule,int def,bool b)=>new(){Number=def};}
public class BooleanOptionItem{public static OptionItem Create(SimpleRoleInfo i,int n,object name,bool def,bool b)=>new(){Flag=def};}
public class SoloWinOption{public static void Create(SimpleRoleInfo i,int n,Func<bool> show,int defo,ClampedIntegerValueRule rule){}}
public class DisposableSender:IDisposable{public FakeWriter Writer=new();public void Dispose(){}}
}
namespace TownOfHost{public static class ChatUpdatePatch{public static bool DoBlockChat,BlockSendName;}public static class ChatControllerUpdatePatch{public static bool IsQuickChatOnly;}}
namespace TownOfHost.Modules{public static class MeetingVoteManager{public static byte Skip=253;}public static class RoomTaskAssign{public static List<int> AllRoomTasker=new();}public static class MeetingStates{public static bool FirstMeeting;}}
namespace TownOfHost.Roles.Impostor{public static class Assassin{public static bool NowUse;}}
namespace TownOfHost.Roles.Madmate{public static class MadAvenger{public static bool Skill;}}
namespace TownOfHost.Roles.Crewmate{public static class Chief{public static string MeetingMark(PlayerControl a,PlayerControl b)=>"";}}
namespace TownOfHost.Roles.Core{
public enum From{TownOfHost_Shell}
public class SimpleRoleInfo{public string RoleColorCode="#C86A18";public UnityEngine.Color RoleColor=new();public static SimpleRoleInfo Create(Type t,Func<PlayerControl,RoleBase> f,CustomRoles role,Func<AmongUs.GameOptions.RoleTypes> v,CustomRoleTypes cr,int id,Action setup,string alias,string color,(int,int) order,From from)=>new();}
public static class CustomRoleManager{public static List<Func<PlayerControl,PlayerControl,bool,string>> MarkOthers=new();public static Dictionary<byte,RoleBase> AllActiveRoles=new();}
public class RoleBase{public PlayerControl Player;public RoleBase(SimpleRoleInfo i,PlayerControl p){Player=p;}protected static string GetString(string s)=>Translator.GetString(s);protected bool Is(PlayerControl p)=>p==Player;protected DisposableSender CreateSender(SendOption o)=>new();public virtual void OnStartMeeting(){}public virtual bool CheckVoteAsVoter(byte id,PlayerControl p)=>true;public virtual string GetProgressText(bool comms=false,bool gamelog=false)=>"";public virtual void OnDead(PlayerControl p){}public virtual void OnLeftPlayer(PlayerControl p){}public virtual void OnExileWrapUp(NetworkedPlayerInfo p,ref bool d){}public virtual void OnFixedUpdate(PlayerControl p){}public virtual void ReceiveRPC(MessageReader r){}public virtual void OnDestroy(){} }
}
namespace TownOfHost.Roles.Core.Interfaces{public interface ISelfVoter{}public interface IAdditionalWinner{bool CheckWin(ref CustomRoles r);}public interface ISystemTypeUpdateHook{bool UpdateReactorSystem(ReactorSystemType s,byte a);bool UpdateHeliSabotageSystem(HeliSabotageSystem s,byte a);bool UpdateLifeSuppSystem(LifeSuppSystemType s,byte a);bool UpdateHqHudSystem(HqHudSystemType s,byte a);bool UpdateSwitchSystem(SwitchSystem s,byte a);bool UpdateHudOverrideSystem(HudOverrideSystemType s,byte a);}}
