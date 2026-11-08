using System; using System.Linq; using System.Collections.Generic;
namespace AmongUs.GameOptions { public enum RoleTypes{Impostor} }
namespace InnerNet { }
namespace UnityEngine { public static class Time { public static float fixedDeltaTime=.02f; } public static class Screen { public static bool fullScreen; } public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);} public class GameObject{ public bool activeInHierarchy=true; } }
public class TextBoxTMP { public UnityEngine.GameObject gameObject=new(); public bool isActiveAndEnabled=true; public int Calls; public void GiveFocus()=>Calls++; }
namespace Hazel { public enum SendOption{Reliable}  public class MessageReader { Queue<object> q;public MessageReader(IEnumerable<object> data){q=new(data);}public int ReadInt32()=>(int)q.Dequeue();public bool ReadBoolean()=>(bool)q.Dequeue();public float ReadSingle()=>(float)q.Dequeue(); } }
public class AmongUsClient { public static AmongUsClient Instance=new();public bool AmHost=true; }
public class NetworkedPlayerInfo { public byte PlayerId;public bool Disconnected; public List<TaskInfo> Tasks=new(); }
public class TaskInfo { public uint Id; }
public class Outfit {public byte ColorId;}
public class PlayerControl { public static PlayerControl LocalPlayer;public int ClientId=2;public byte Color;public Outfit CurrentOutfit=new();public void SetColor(byte c)=>Color=c;public void SetHat(string s,byte c){}public void SetSkin(string s,byte c){}public void SetVisor(string s,byte c){}public void SetPet(string s,byte c){}public int GetNextRpcSequenceId(TownOfHost.RpcCalls c)=>1; public byte PlayerId;public uint NetId=100;public bool Alive=true;public NetworkedPlayerInfo Data=new();public TownOfHost.Roles.Impostor.Experiencer Active;public float Cooldown;public int Exiles,Syncs;public List<TownOfHost.Roles.Core.CustomRoles> Addons=new(); }
namespace TownOfHost {
public enum GeneralOption{KillCooldown} public enum OptionFormat{Seconds} public enum OptionZeroNotation{Off} public enum Sounds{TaskComplete}
public record NumberRule(float Min,float Max,float Step);
public class OptionItem { public static Dictionary<int,OptionItem> Items=new(); public float Value; public int Id;public object Key; public OptionItem Parent; public Func<bool> Enabled;
 public int GetInt()=>(int)Value;public float GetFloat()=>Value;public OptionItem SetValueFormat(OptionFormat f)=>this;public OptionItem SetZeroNotation(OptionZeroNotation n)=>this;public OptionItem SetEnabled(Func<bool> f){Enabled=f;return this;}
 public static OptionItem Add(TownOfHost.Roles.Core.SimpleRoleInfo info,int offset,object key,float value,OptionItem parent){var o=new OptionItem{Id=info.ConfigId+offset,Key=key,Value=value,Parent=parent};Items.Add(o.Id,o);return o;}}
public class FloatOptionItem:OptionItem {public static OptionItem Create(TownOfHost.Roles.Core.SimpleRoleInfo i,int offset,object key,NumberRule r,float value,bool single,OptionItem parent=null)=>Add(i,offset,key,value,parent);}
public class IntegerOptionItem:OptionItem {public static OptionItem Create(TownOfHost.Roles.Core.SimpleRoleInfo i,int offset,object key,NumberRule r,int value,bool single,OptionItem parent=null)=>Add(i,offset,key,value,parent);}
public static class GameStates{public static bool IsMeeting=true,IsInTask=true,IsLobby;}
public static class Main{public static Dictionary<byte,float> AllPlayerKillCooldown=new();}
public static class Utils{public static List<(string Text,byte Target)> Messages=new(); public static void SendMessage(string text,byte id=255)=>Messages.Add((text,id));}
public static class RPC{public static void PlaySoundRPC(byte id,Sounds sound){} }
public static class Logger{public static void Warn(string t,string tag){} }
public static class Ext{ public static int GetClientId(this PlayerControl p)=>p.ClientId;public static TownOfHost.Roles.Impostor.Experiencer GetRoleClass(this PlayerControl p)=>p.Active;public static bool IsAlive(this PlayerControl p)=>p!=null&&p.Alive&&!p.Data.Disconnected;public static void SetKillCooldown(this PlayerControl p)=>p.Cooldown=p.Active.CalculateKillCooldown();public static void SyncSettings(this PlayerControl p)=>p.Syncs++;public static void RpcProtectedMurderPlayer(this PlayerControl p){}public static void RpcSetCustomRole(this PlayerControl p,TownOfHost.Roles.Core.CustomRoles role)=>p.Addons.Add(role);public static void RpcExileV3(this PlayerControl p){p.Alive=false;p.Exiles++;}}
}
namespace TownOfHost.Roles.Core {
public enum CustomRoles{Experiencer=369,Guesser=525,MagicHand=560} public enum CustomRoleTypes{Impostor}public enum From{TownOfHost_hamo}
public class SimpleRoleInfo {public int ConfigId;public Action Setup;public CustomRoles RoleName;public bool New;public static SimpleRoleInfo Create(Type t,Func<PlayerControl,TownOfHost.Roles.Impostor.Experiencer> factory,CustomRoles role,Func<AmongUs.GameOptions.RoleTypes> native,CustomRoleTypes camp,int id,Action options,string alias,(int,int)? OptionSort=null,From from=From.TownOfHost_hamo,bool isNewRole=false)=>new(){ConfigId=id,Setup=options,RoleName=role,New=isNewRole};}
public class TestWriter{public List<object> Values=new();public void Write(int v)=>Values.Add(v);public void Write(bool v)=>Values.Add(v);public void Write(float v)=>Values.Add(v);}
public class Sender:IDisposable{public TestWriter Writer=new();public void Dispose(){RoleBase.LastRpc=Writer.Values;}}
public class RoleBase {public PlayerControl Player;public static List<object> LastRpc;public RoleBase(SimpleRoleInfo i,PlayerControl p){Player=p;}public bool Is(PlayerControl p)=>p==Player;public static string GetString(string s)=>s;protected Sender CreateSender()=>new(); public enum DontReportreson{None,Impostor}
public virtual void Add(){}public virtual bool OnCompleteTask(uint id)=>true;public virtual void OnFixedUpdate(PlayerControl p){}public virtual void OnReportDeadBody(PlayerControl p,NetworkedPlayerInfo n){}public virtual void OnDead(PlayerControl p){}public virtual void OnDestroy(){}public virtual bool CancelReportDeadBody(PlayerControl p,NetworkedPlayerInfo n,ref DontReportreson r)=>false;public virtual void ReceiveRPC(Hazel.MessageReader r){}public virtual string GetProgressText(bool comms=false,bool GameLog=false)=>"";}
public class MurderInfo {public PlayerControl AttemptKiller,AttemptTarget;public PlayerControl AppearanceKiller=>AttemptKiller;public PlayerControl AppearanceTarget=>AttemptTarget;public (PlayerControl,PlayerControl) AttemptTuple=>(AttemptKiller,AttemptTarget);public bool IsSuicide=>AttemptKiller==AttemptTarget;public bool IsFakeSuicide;public bool CanKill=true,DoKill=true,Guard;public bool CheckHasGuard()=>Guard; public MurderInfo(PlayerControl k,PlayerControl t){AttemptKiller=k;AttemptTarget=t;}}
public static class CustomRoleManager{public static int Resolved,Deaths;public static bool OnCheckMurder(PlayerControl k,PlayerControl t,PlayerControl ak,PlayerControl at,bool force=false){Resolved++;OnMurderPlayer(k,t);return true;} public static void OnMurderPlayer(PlayerControl k,PlayerControl t){Deaths++;k.Active.OnMurderPlayerAsKiller(new(k,t));}}
}
namespace TownOfHost.Roles.Core.Interfaces{public interface IImpostor{} }

namespace TownOfHost {
public static class Options{public static bool firstturnmeeting;}public static class MeetingStates{public static bool FirstMeeting;}
public enum RpcCalls{SetColor,SetHatStr,SetSkinStr,SetVisorStr,SetPetStr}
public class CustomRpcSender{public static List<int> Destinations=new();int destination;public static CustomRpcSender Create(string n,Hazel.SendOption s)=>new();public void StartMessage(int id)=>destination=id;public CustomRpcSender StartRpc(uint id,RpcCalls rpc)=>this;public CustomRpcSender Write(object o)=>this;public void EndRpc(){}public void EndMessage(){}public void SendMessage()=>Destinations.Add(destination);}
}
