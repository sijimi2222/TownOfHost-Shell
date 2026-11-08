using TownOfHost;
using TownOfHost.Roles.Core;
namespace UnityEngine {
 public class AudioClip {}
 public struct Color {}
 public static class ColorUtility { public static bool TryParseHtmlString(string s,out Color c){c=new();return true;} }
}
namespace AmongUs.GameOptions { public enum RoleTypes { Crewmate,Impostor,Engineer,Scientist,Tracker,Noisemaker,Detective,Judge,GuardianAngel,SpiritGuide,Shapeshifter,Phantom,Viper } }
public class PlayerControl {}
namespace TownOfHost.Roles.Core {
 public class RoleBase {}
 public class RoleAssignInfo { public RoleAssignInfo(CustomRoles r,CustomRoleTypes t){} public IntegerValueRule AssignCountRule=new(1,15,1); }
 public static class CustomRoleManager {
  public static Dictionary<CustomRoles,SimpleRoleInfo> AllRolesInfo=new();
  public static Dictionary<int,CustomRoles> CustomRoleIds=new();
  public static List<CustomRoles> SortCustomRoles=new();
 }
}
namespace TownOfHost.Roles.Core.Descriptions {
 public class RoleDescription {}
 public class SingleRoleDescription:RoleDescription { public SingleRoleDescription(SimpleRoleInfo i){} }
 public class VanillaRoleDescription:RoleDescription { public VanillaRoleDescription(SimpleRoleInfo i,AmongUs.GameOptions.RoleTypes t){} }
}
namespace TownOfHost {
 public static class Event { public static int GetCurrentConfigId(CustomRoles r,int id)=>id; }
 public class IntegerValueRule { public int MinValue,MaxValue,Step; public IntegerValueRule(int min,int max,int step){MinValue=min;MaxValue=max;Step=step;} }
 public class OptionItem {
  public int Id,Value; public string Name,Fromtext; public TabGroup Tab; public OptionItem Parent;
  public CustomRoles CustomRole=CustomRoles.NotAssigned,ParentRole=CustomRoles.NotAssigned;
  public class UpdateValueEventArgs:EventArgs {}
  public OptionItem SetColorcode(string v)=>this;
  public OptionItem SetColor(UnityEngine.Color v)=>this;
  public OptionItem SetCustomRole(CustomRoles r){CustomRole=r;return this;}
  public OptionItem SetValueFormat(OptionFormat v)=>this;
  public OptionItem SetHeader(bool v)=>this;
  public OptionItem SetEnabled(Func<bool> v)=>this;
  public OptionItem SetHidden(bool v)=>this;
  public OptionItem SetTag(CustomOptionTags t)=>this;
  public OptionItem SetParent(OptionItem p){Parent=p;return this;}
  public OptionItem SetParentRole(CustomRoles r){ParentRole=r;return this;}
  public OptionItem RegisterUpdateValueEvent(Action<object,UpdateValueEventArgs> a)=>this;
  public void Refresh(){}
 }
 public class IntegerOptionItem:OptionItem {
  public static IntegerOptionItem Create(int id,string name,IntegerValueRule rule,int value,TabGroup tab,bool single,string From="",bool HideValue=false)=>new(){Id=id,Name=name,Value=value,Tab=tab,Fromtext=From};
 }
 public static partial class Options {
  public static Dictionary<CustomRoles,OptionItem> CustomRoleSpawnChances=new(),CustomRoleCounts=new();
  private static List<CombinationRoles> Combinations=new();
  public static void Reset(){CustomRoleSpawnChances.Clear();CustomRoleCounts.Clear();Combinations.Clear();CustomRoleManager.AllRolesInfo.Clear();CustomRoleManager.CustomRoleIds.Clear();CustomRoleManager.SortCustomRoles.Clear();}
 }
 public static class SlotRoleAssign { public static bool IsSeted(CustomRoles r)=>false; }
 public static class Translator { public static string GetString(string key)=>key; }
 public static class UtilsRoleText {
  public static UnityEngine.Color GetRoleColor(CustomRoles r,bool b=false)=>new();
  public static string GetRoleColorCode(CustomRoles r)=>"#abcdef";
 }
 public static class Utils {
  public static string ColorString(UnityEngine.Color c,string v)=>v;
 }
 public static class TestExtensions {
  public static string RemoveHtmlTags(this string t)=>System.Text.RegularExpressions.Regex.Replace(t,@"<[^>]+>","");
  public static SimpleRoleInfo GetRoleInfo(this CustomRoles r)=>CustomRoleManager.AllRolesInfo.GetValueOrDefault(r);
  public static bool IsAddOn(this CustomRoles r)=>(int)r>=5000;
  public static bool IsGhostRole(this CustomRoles r)=>(int)r>=500&&(int)r<1000;
  public static bool IsLovers(this CustomRoles r)=>false;
  public static bool IsVanilla(this CustomRoles r)=>r is CustomRoles.Crewmate or CustomRoles.Impostor;
  public static CustomRoles GetCombination(this CustomRoles r)=>CustomRoles.NotAssigned;
  public static bool IsCombinationRole(this CustomRoles r)=>r is CustomRoles.Trauma or CustomRoles.Surmount or CustomRoles.Catalyst or CustomRoles.Conductor;
 }
}
