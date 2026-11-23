using System;using System.Collections.Generic;
namespace AmongUs.GameOptions{public enum RoleTypes{Crewmate,Impostor}}
namespace Hazel{public enum SendOption{Reliable}public class Writer{public string Text;public void Write(string s)=>Text=s;}}
namespace TownOfHost{
public class PlayerControl{public static PlayerControl LocalPlayer;public bool AmOwner;public Data Data=new();public int ClientId=7;public byte PlayerId;public uint NetId;public int Revives,RoleChanges;public void Revive()=>Revives++;public void RpcSetRole(AmongUs.GameOptions.RoleTypes r,bool b)=>RoleChanges++;public void RpcSetCustomRole(CustomRoles r,bool a,bool b)=>RoleChanges++;public bool IsModClient()=>true;public string GetNameWithRole()=>"FakePlayer";}
public class Data{public bool Disconnected,IsDead;}
public class AmongUsClient{public static AmongUsClient Instance=new();public bool AmHost=true;public int HostId=1,Sent;public Hazel.Writer StartRpcImmediately(uint n,byte rpc,Hazel.SendOption o,int target){Sent++;return new();}public void FinishRpcImmediately(Hazel.Writer w){}}
public enum CustomRPC{RequestCommandProcessing=239}public enum CustomRoles{Crewmate,Tuner}public enum CustomDeathReason{etc}
public static class PlayerCatch{public static Dictionary<int,PlayerControl> Players=new();public static PlayerControl GetPlayerById(int id)=>Players.TryGetValue(id,out var p)?p:null;}
public class OptionItem{public bool Value;public bool GetBool()=>Value;}public static class DebugModeManager{public static bool AmDebugger;public static OptionItem EnableDebugMode=new(),EnableTOHhmDebugMode=new();}
public static class GameStates{public static bool InGame=true;}public static class Logger{public static List<string> Messages=new();public static void Warn(string s,string k)=>Messages.Add(s);public static void Info(string s,string k)=>Messages.Add(s);}
public class ChatController{public float timeSinceLastMessage;public QuickField quickChatField=new();public FreeField freeChatField=new();public List<string> Texts=new();public void AddChat(PlayerControl p,string s)=>Texts.Add(s);}
public class QuickField{public bool Visible;}public class FreeField{public TextArea textArea=new();}public class TextArea{public string text;public void Clear()=>text="";}
public class HudManager{public static HudManager Instance=new();public ChatController Chat=new();}public static class NameColorManager{public static int Removals;public static void RemoveAll(byte id)=>Removals++;}
public static class RPC{public static int Syncs;public static void RpcSyncAllNetworkedPlayer()=>Syncs++;}public class PlayerState{public bool IsDead=true;public CustomDeathReason DeathReason;public CustomRoles MainRole=CustomRoles.Crewmate;public static PlayerState State=new();public static PlayerState GetByPlayerId(byte id)=>State;}
public static class Extensions{public static string RemoveHtmlTags(this string s)=>s;public static AmongUs.GameOptions.RoleTypes GetRoleTypes(this CustomRoles r)=>AmongUs.GameOptions.RoleTypes.Crewmate;}
public static partial class ChatCommands{public static bool GetRoleByInputName(string s,out CustomRoles role,bool b){role=CustomRoles.Tuner;return s=="Tuner";}}
}
