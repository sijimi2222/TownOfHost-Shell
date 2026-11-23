using TownOfHost.Roles.Core;
namespace UnityEngine { public static class Color { public static object cyan => null; } }
namespace TownOfHost {
 public sealed class PlayerControl { public byte PlayerId; public bool Alive=true; public PlayerData Data=new(); public object Role; public bool IsAlive()=>Alive; public object GetRoleClass()=>Role; }
 public sealed class PlayerData { public string PlayerName="test"; public string GetLogPlayerName()=>"test"; }
 public static class PlayerCatch { public static Dictionary<byte,PlayerControl> Players=new(); public static PlayerControl GetPlayerById(byte id)=>Players.GetValueOrDefault(id); }
 public static class GameStates { public static bool IsMeeting; }
 public static class Translator { public static string GetString(string key)=>key; }
 public static class Logger { public static void Exception(Exception e,string category){} public static void Info(string message,string category){} }
 public static class Utils { public static List<(string Text,byte Target)> Messages=new(); public static void SendMessage(string text,byte target,string title=null)=>Messages.Add((text,target)); public static string ColorString(object color,string text)=>text; }
 public static class Event { public static bool CheckRole(CustomRoles role)=>true; }
 public sealed class FakeOption { public int Value; public bool GetBool()=>Value>0; public int GetInt()=>Value; }
}
namespace TownOfHost.Modules { public static class SelfVoteManager { public enum VoteStatus {Self,Skip,Vote} public static bool Enabled=true; public static bool Canuseability()=>Enabled; public static bool CheckSelfVoteMode(TownOfHost.PlayerControl me,byte voted,out VoteStatus status){status=voted==me.PlayerId?VoteStatus.Self:voted==253?VoteStatus.Skip:VoteStatus.Vote;return true;} public static void SetMode(TownOfHost.PlayerControl p,bool enabled){} } }
namespace TownOfHost.Roles.Crewmate { public sealed class Sender { public int SyncCount; public void SendRPC()=>SyncCount++; } }
namespace TownOfHost.Roles.Madmate { public sealed class MadSender { public int SyncCount; public void SendRPC()=>SyncCount++; } }