using System;using System.Collections;using System.Collections.Generic;using System.Linq;
using AmongUs.GameOptions;
namespace AmongUs.GameOptions {public enum RoleTypes {Crewmate,Impostor,CrewmateGhost,ImpostorGhost,SpiritGuide}}
namespace UnityEngine {public sealed class WaitForSeconds {public WaitForSeconds(float value){}}}
namespace Hazel {public enum SendOption{Reliable}public class MessageWriter{}}
namespace TownOfHost {
public enum CustomGameMode {Standard,Other}public enum CustomRoleTypes {Crewmate,Impostor,Madmate,Neutral}
public enum GameOverReason {ImpostorsByKill,ImpostorsBySabotage,ImpostorsByVote,CrewmatesByVote,CrewmatesByTask}
public enum RpcCalls {SetColor}public enum CustomRPC {EndGame}
public class Opt{public bool Value;public bool GetBool()=>Value;public int GetInt()=>0;}
public static class Options {public static Opt CommsCamouflage=new(),NoGameEnd=new(),OptionSabotageFinAllKill=new(),OutroCrewWinreasonchenge=new();public static CustomGameMode CurrentGameMode=CustomGameMode.Standard;}
public static class GameStates{public static bool IsLobby,IsOutro,task;public static float turntimer;}
public static class Main{public static Dictionary<byte,bool> CheckShapeshift=new();public static Dictionary<byte,byte> ShapeshiftTarget=new();public static bool DontGameSet;public static Dictionary<byte,CustomDeathReason> AfterMeetingDeathPlayers=new();}
public static class Palette{public static int[] PlayerColors=new int[32];}
public class NetworkedPlayerInfo {
 public uint NetId=50;public byte PlayerId;public bool IsDead,Disconnected;public RoleData Role=new();public PlayerOutfit DefaultOutfit=new();
 public class RoleData {public RoleTypes Role=RoleTypes.Crewmate;}
 public sealed class PlayerOutfit {
  public string PlayerName="synthetic",HatId="",SkinId="",VisorId="";public string RawPet="";public int RawColor=1;public bool BadPet,BadColor;public int PetReads;
  public int ColorId{get{if(BadColor)throw new ArgumentOutOfRangeException("native color");return RawColor;}set{RawColor=value;}}
  public string PetId{get{PetReads++;if(BadPet)throw new ArgumentOutOfRangeException("length","Length cannot be less than zero.");return RawPet;}set{RawPet=value;}}
 }
}
public class PlayerControl {
 public static PlayerControl LocalPlayer;public byte PlayerId;public uint NetId=20;public NetworkedPlayerInfo Data=new();public bool inVent,shapeshifting,ThrowColor,ThrowShift,ThrowAfterSetOnce;public int ColorCalls,ShiftCalls;public CustomRoles Role=CustomRoles.Crewmate;public CustomRoleTypes Team=CustomRoleTypes.Crewmate;public Roles.Core.RoleBase Active;
 public void SetColor(int color){ColorCalls++;if(ThrowColor)throw new InvalidOperationException("synthetic local color");Data.DefaultOutfit.RawColor=color;if(ThrowAfterSetOnce){ThrowAfterSetOnce=false;throw new InvalidOperationException("synthetic partial color");}}
 public void RpcShapeshift(PlayerControl pc,bool flag){ShiftCalls++;if(ThrowShift)throw new InvalidOperationException("synthetic shapeshift");shapeshifting=false;}
 public void RpcSetRole(RoleTypes role,bool flag){Data.Role.Role=role;Data.IsDead=true;}
 public void RpcExileV3(){Data.IsDead=true;}public void SetRealKiller(PlayerControl pc,bool flag){}public void ResetPlayerCam(float n){}public void RpcMurderPlayer(PlayerControl pc){Data.IsDead=true;}
 public bool IsAlive()=>Data!=null&&!Data.IsDead&&!Data.Disconnected;
 public bool Is(CustomRoles r)=>Role==r;public bool Is(CustomRoleTypes t)=>Team==t;public bool IsLovers()=>false;public bool IsWinner()=>CustomWinnerHolder.WinnerIds.Contains(PlayerId);
 public CustomRoles GetCustomRole()=>Role;public Roles.Core.RoleBase GetRoleClass()=>Active;public PlayerState GetPlayerState()=>PlayerState.GetByPlayerId(PlayerId);public string GetNameWithRole()=>"synthetic";
}
public static class Ext {public static void Do<T>(this IEnumerable<T> source,Action<T> action){foreach(var item in source)action(item);}public static string RemoveHtmlTags(this string s)=>s;public static string GetLogPlayerName(this NetworkedPlayerInfo p)=>"synthetic";public static bool IsLovers(this CustomRoles r)=>false;public static bool IsLovers(this CustomWinner r)=>false;public static Info GetRoleInfo(this CustomRoles r)=>new();public static IEnumerator WrapToIl2Cpp(this IEnumerator e)=>e;}
public sealed class Info{public bool IsDesyncImpostor;}
public static class PlayerCatch {public static List<PlayerControl> AllPlayerControls=new();public static IEnumerable<PlayerControl> AllAlivePlayerControls=>AllPlayerControls.Where(x=>x!=null&&x.IsAlive());public static PlayerControl GetPlayerById(byte id)=>AllPlayerControls.FirstOrDefault(x=>x.PlayerId==id);public static bool AnyModClient()=>Probe.ModClient;}
public class PlayerState{static Dictionary<byte,PlayerState> states=new();public bool IsDead,WasInfluencer;public CustomDeathReason DeathReason;public static PlayerState GetByPlayerId(byte id){if(!states.TryGetValue(id,out var s))states[id]=s=new();return s;}public void SetDead(){IsDead=true;}public Tasks GetTaskState()=>new();}
public class Tasks{public int CompletedTasksCount,AllTasksCount;public bool IsTaskFinished;}
public static class UtilsTask{public static bool HasTasks(NetworkedPlayerInfo i,bool flag)=>true;}
public static class Logger{public static List<string> Warnings=new();public static void Warn(string m,string tag)=>Warnings.Add(tag+":"+m);public static void Info(string m,string tag){}public static void Error(string m,string tag){}public static void Exception(Exception e,string tag){}public static void seeingame(string m){}}
public static class UtilsNotifyRoles{public static void NotifyRoles(bool NoCache=false){}}
public class CustomRpcSender:IDisposable {public static List<CustomRpcSender> All=new();public static bool ThrowSend,ThrowWrite,ThrowAfterSend;public bool Disposed,Sent;public List<long> Payload=new();public static CustomRpcSender Create(string name){var s=new CustomRpcSender();All.Add(s);return s;}public CustomRpcSender AutoStartRpc(uint id,byte rpc){Payload.Add(id);Payload.Add(rpc);return this;}public CustomRpcSender Write(uint n){if(ThrowWrite)throw new InvalidOperationException("synthetic write");Payload.Add(n);return this;}public CustomRpcSender Write(int n){Payload.Add(n);return this;}public CustomRpcSender EndRpc()=>this;public void SendMessage(){if(ThrowSend)throw new InvalidOperationException("synthetic send");Sent=true;if(ThrowAfterSend)throw new InvalidOperationException("synthetic after-send callback");}public void Dispose(){Disposed=true;}}
public class AmongUsClient{public static AmongUsClient Instance=new();public bool AmHost=true;public void StartCoroutine(IEnumerator co){Probe.Coroutines++;while(co.MoveNext()){} }public Hazel.MessageWriter StartRpcImmediately(uint id,byte rpc,Hazel.SendOption op)=>new();public void FinishRpcImmediately(Hazel.MessageWriter w){Probe.WinnerRpc++;}}
public class GameData{public static GameData Instance=new();public NetworkedPlayerInfo GetPlayerById(byte id)=>PlayerCatch.GetPlayerById(id).Data;}
public class GameManager{public static GameManager Instance=new();public void RpcEndGame(GameOverReason reason,bool flag){Probe.EndCalls++;Probe.EndReason=reason;}}
public class ShipStatus{public static ShipStatus Instance=new();public bool enabled=true;}
public static class GameDataSerializePatch{public static int SerializeMessageCount;}
public static class RPC{public static void RpcSyncAllNetworkedPlayer(){Probe.Syncs++;}}
public static class CheckGetNomalAchievement{public static void CallEndGame(GameOverReason r){}}
public static class Probe{public static int EndCalls,Coroutines,WinnerRpc,Summaries,Syncs;public static bool ModClient;public static GameOverReason EndReason;}
public static class CustomWinnerHolder{public static CustomWinner WinnerTeam=CustomWinner.Default;public static HashSet<byte> WinnerIds=new(),CantWinPlayerIds=new(),NeutralWinnerIds=new();public static HashSet<CustomRoles> WinnerRoles=new(),AdditionalWinnerRoles=new();public static List<CustomWinner> winners=new();public static void ResetAndSetWinner(CustomWinner w){WinnerTeam=w;WinnerIds.Clear();WinnerRoles.Clear();AdditionalWinnerRoles.Clear();winners.Clear();winners.Add(w);}public static bool ResetAndSetAndChWinner(CustomWinner w,byte id){ResetAndSetWinner(w);return true;}public static void WriteTo(Hazel.MessageWriter writer){}}
public abstract class GameEndPredicate{public abstract bool CheckForEndGame(out GameOverReason r);}
public class FixedPredicate:GameEndPredicate{public CustomWinner Winner;public GameOverReason Reason=GameOverReason.ImpostorsByKill;public override bool CheckForEndGame(out GameOverReason r){r=Reason;CustomWinnerHolder.ResetAndSetWinner(Winner);return true;}}
public static class NaturalDisasters{public static bool IsThisMode;public static void FinishSurvival(){}}
public static class CustomSpawnEditor{public static bool ActiveEditMode;}
public static class SuddenDeathMode{public static bool NowSuddenDeathMode,NowSuddenDeathTemeMode;public static void TeamAllWin(){}}
public static class DarkHide{public static void CheckWin(ref GameOverReason r){}}
public static class Zombie{public static void TryTakeOverCrewWin(ref GameOverReason r,CustomWinner team){}}
public static class Onmyoji{public static void TryTakeOverCrewWin(ref GameOverReason r){}}
public static class BatGirl{public static void TryTakeOverSoloWin(ref GameOverReason r){}}
public static class LessThan{public static bool TryChooseWinner()=>false;public static void FinalizeSoloWinner(){}public static void EndRound(){}}
public static class MagicalGirl{public static bool CanWinAsCrewmateStaff(PlayerControl p)=>true;}
public static class Monochromer{public static bool CheckWin(GameOverReason r)=>false;}
public static class Egoist{public static bool CheckWin()=>false;}
public static class Lovers{public static void LoversSoloWin(ref GameOverReason r){}public static void LoversAddWin(){}}
public static class LastNeutral{public static void CheckAddWin(PlayerControl p,GameOverReason r){}}
public static class Amanojaku{public static void CheckWin(PlayerControl p,GameOverReason r){}}
public static class CurseMaker{public static void CheckWin(){}}
public static class Fox{public static void SFoxCheckWin(ref GameOverReason r){}}
public static class Tuna{public static void CheckWin(ref GameOverReason r){}}
public static class Spelunker{public static void CheckWin(ref GameOverReason r){}}
public static class Chatter{public static void CheckWin(ref GameOverReason r){}}
public static class AsistingAngel{public static void CheckAddWin(){}}
public static class Twins{public static void CheckAddWin(){}}
public static class Triplets{public static void CheckAddWin(){}}
public static class Faction{public static void CheckWin(){}}
public static class Ruler{public static void ApplyGameEndRules(){}}
public static class Eliminator{public static void ApplyAdditionalWinners(){}public static void EndRound(){}}
}
namespace TownOfHost.Roles.Core {
public class RoleBase{public virtual void CheckWinner(TownOfHost.GameOverReason r){}public virtual void ChangeColor(){}}
public static class CustomRoleManager{public static Dictionary<byte,RoleBase> AllActiveRoles=new();public static RoleBase GetByPlayerId(byte id)=>AllActiveRoles.GetValueOrDefault(id);}
}
namespace TownOfHost.Roles.Core.Interfaces{public interface IAdditionalWinner{bool CheckWin(ref TownOfHost.CustomRoles role);}}
namespace TownOfHost.Roles.Impostor {
public class BeginnerImpostor:Core.RoleBase{public void EnforceDummyKillWinRequirement(){}}
public class Abuser:Core.RoleBase{public void EnforceWinRequirement(){}}
public class Victim:Core.RoleBase{public void EnforceFactionWin(){}}
}
namespace TownOfHost.Roles.Neutral {
public class Executioner:Core.RoleBase{public byte TargetId;public static void ChangeRoleByTarget(byte id){}}
public class PhantomThief:Core.RoleBase{public void CheckWin(){}}
public static class Flatfish{public static void CheckWin(ref TownOfHost.GameOverReason r){}}
public static class Parity{public static void CheckWin(){}}
public static class Lupin{public static void CheckWin(ref TownOfHost.GameOverReason r){}}
public static class DarkSheriff{public static void CheckWin(ref TownOfHost.GameOverReason r){}}
public static class RaccoonParent{public static void CheckWin(ref TownOfHost.GameOverReason r){}}
public static class Tiger{public static void TryChooseWinner(){}}
public static class God{public static TownOfHost.Opt RequireTasksToWinOpt=new(),TaskCountOpt=new();}
public static class Streamer{public static void ApplyEndWinners(){}}
public static class Prankster{public static void ApplyAdditionalWinners(){}}
}
namespace TownOfHost.Roles.AddOns.Common {public static class UserAddons2026{public static void CheckWin(TownOfHost.GameOverReason r){}}}
