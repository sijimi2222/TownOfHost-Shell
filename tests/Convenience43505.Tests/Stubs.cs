namespace HarmonyLib{public class HarmonyPatch:Attribute{public HarmonyPatch(params object[] v){}}public class HarmonyPriority:Attribute{public HarmonyPriority(int n){}}public class HarmonyPostfix:Attribute{}public static class Priority{public const int First=800;}}
public class MeetingHud{public float discussionTimer;public void Update(){}}
public class ChatController{public Field freeChatField;public void SendChat(){} public class Field{public Area textArea;}public class Area{public string text;public void Clear(){text="";}}}
public class AmongUsClient{public static AmongUsClient Instance=new();public bool AmHost=true;public void OnGameEnd(){}}
public class PlayerControl{public byte PlayerId;public DataRecord Data=new();public static List<PlayerControl> AllPlayerControls=new();public static PlayerControl LocalPlayer;public TownOfHost.CustomRoles Role;public class DataRecord{public string FriendCode,PlayerName;}}
namespace TownOfHost.Roles.Core{public class SimpleRoleInfo{public Type ClassType;public TownOfHost.CustomRoles RoleName;public int ConfigId;public TownOfHost.TabGroup Tab;}}
namespace TownOfHost.Roles.Core.Interfaces{public interface IKiller{}}
namespace TownOfHost{
public enum CustomRoles{NotAssigned=500,GM=1,Crewmate=0,Impostor=2,Neutral=3,StepBomber=370,Locksmith=371,Coroner=372}
public enum CustomWinner{Draw,Crewmate,Impostor}
public static class CustomWinnerHolder{public static CustomWinner WinnerTeam;public static HashSet<byte> WinnerIds=new();}
public static class Main{public static string BaseDirectory;public static List<byte> winnerList=new();public static Dictionary<byte,string> AllPlayerNames=new();public static Settings NormalOptions=new();public class Settings{public int VotingTime=60,DiscussionTime=15;}}
public static class GameStates{public static bool ExiledAnimate;}
public static class GameModeManager{public static bool Standard=true;public static bool IsStandardClass()=>Standard;}
public class PlayerState{public static Dictionary<byte,PlayerState> AllPlayerStates=new();public static PlayerState GetByPlayerId(byte id)=>AllPlayerStates.GetValueOrDefault(id);public CustomRoles MainRole;public bool IsDead;public int Kills;public TaskState taskState;public int GetKillCount(bool b)=>Kills;public class TaskState{public bool hasTasks;public int AllTasksCount,CompletedTasksCount;}}
public enum TabGroup{ImpostorRoles}public enum OptionZeroNotation{Off}public enum OptionFormat{Seconds}
public class OptionItem{public static List<OptionItem> AllOptions=new();public int Id;public bool Value;public float Float;public OptionItem Parent;public bool GetBool()=>Value;public float GetFloat()=>Float;public int GetInt()=>(int)Float;public OptionItem SetParent(OptionItem p){Parent=p;return this;}public OptionItem SetParentRole(CustomRoles r)=>this;public OptionItem SetZeroNotation(OptionZeroNotation z)=>this;public OptionItem SetValueFormat(OptionFormat f)=>this;public OptionItem SetOptionName(Func<string> f)=>this;}
public record FloatValueRule(float Min,float Max,float Step);
public class FloatOptionItem:OptionItem{public static FloatOptionItem Create(int id,string name,FloatValueRule rule,float value,TabGroup tab,bool single){var o=new FloatOptionItem{Id=id,Float=value};AllOptions.Add(o);return o;}}
public static class Options{public static Dictionary<CustomRoles,OptionItem> CustomRoleSpawnChances=new();}
public class IRandom{public int Next(int min,int max)=>min;}
public static class Logger{public static void Info(string s,string t){}public static void Warn(string s,string t){}}
public static class UtilsRoleText{public static string GetRoleName(CustomRoles r)=>r.ToString();}
public static class Utils{public static List<(string Text,byte To,string Title)> Sent=new();public static void SendMessage(string text,byte to=255,string title="")=>Sent.Add((text,to,title));}
public static class Extensions{public static string RemoveHtmlTags(this string s)=>System.Text.RegularExpressions.Regex.Replace(s,@"<[^>]+>","");public static bool IsCrewmate(this CustomRoles r)=>r==CustomRoles.Crewmate;public static bool IsImpostorTeam(this CustomRoles r)=>r==CustomRoles.Impostor;public static bool IsNeutral(this CustomRoles r)=>r==CustomRoles.Neutral;public static CustomRoles GetCustomRole(this PlayerControl p)=>p.Role;}
}