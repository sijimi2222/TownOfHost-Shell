// Unity/network/options substitutes; Express and RoleBase.Dispose are production code.
using TownOfHost.Roles.Core;
namespace AmongUs.GameOptions { public enum RoleTypes { Crewmate } }
public sealed class PlayerControl { public byte PlayerId { get; set; } }
namespace TownOfHost {
 public static class Main { public static Dictionary<byte,float> AllPlayerSpeed = new(); }
 public enum CustomRoles { Express } public enum CustomRoleTypes { Crewmate }
 public enum OptionFormat { Multiplier }
 public sealed record FloatRule(float Min,float Max,float Step);
 public class OptionItem { public float Value; public static OptionItem Last; public float GetFloat()=>Value; public OptionItem SetValueFormat(OptionFormat f)=>this; }
 public static class FloatOptionItem { public static OptionItem Create(SimpleRoleInfo info,int offset,object name,FloatRule rule,float value,bool flag)=>OptionItem.Last=new(){Value=value}; }
}
namespace TownOfHost.Modules {
 public static class PlayerGameOptionsSender { public static List<byte> Dirty=new(); public static void SetDirty(byte id)=>Dirty.Add(id); }
}
namespace TownOfHost.Roles.Core {
 public enum From { TownOfHost_Y }
 public sealed class SimpleRoleInfo {
  public Action Setup;
  public static SimpleRoleInfo Create(Type type,Func<PlayerControl,RoleBase> factory,TownOfHost.CustomRoles role,Func<AmongUs.GameOptions.RoleTypes> baseRole,TownOfHost.CustomRoleTypes faction,int id,Action setup,string alias,string color,(int,int) order,From from)=>new(){Setup=setup};
 }
 public static class CustomRoleManager { public static Dictionary<byte,RoleBase> AllActiveRoles=new(); }
 public abstract partial class RoleBase {
  public PlayerControl Player {get;set;}
  protected RoleBase(SimpleRoleInfo info,PlayerControl player){Player=player;CustomRoleManager.AllActiveRoles[player.PlayerId]=this;}
  public virtual void Add(){} public virtual void ChengeRoleAdd(){} public virtual void OnDestroy(){}
 }
}
