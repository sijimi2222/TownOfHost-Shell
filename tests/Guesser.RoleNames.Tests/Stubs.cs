using System.Text.RegularExpressions;
namespace TownOfHost;
public sealed class Option { public bool Value; public bool GetBool()=>Value; }
public sealed class PlayerControl {
 public byte PlayerId; public Outfit cosmetics=new(); public PlayerData Data=new(); public CustomRoles Role;
 public CustomRoles GetCustomRole()=>Role; public bool Is(CustomRoles role)=>Role==role;
}
public sealed class Outfit { public int ColorId; }
public sealed class PlayerData { public bool IsDead; }
public static class PlayerCatch {
 public static readonly List<PlayerControl> AllPlayerControls=new();
 public static PlayerControl GetPlayerById(byte id)=>AllPlayerControls.FirstOrDefault(p=>p.PlayerId==id);
}
public static class ModColors { public enum PlayerColor { Red,Blue,Green,Pink,Orange,Yellow,Black,white,Purple,Brown,Cyan,Lime,Maroon,Rose,Banana,Gray,Tan,Coral } }
public static class MagicalGirl { public static bool IsMatchedGuess(PlayerControl target,CustomRoles role)=>false; }
public static class Extensions {
 public static string RemoveHtmlTags(this string value)=>Regex.Replace(value,"<[^>]*>","");
 public static bool IsVanilla(this CustomRoles role)=>role is CustomRoles.Crewmate or CustomRoles.Impostor or CustomRoles.Engineer or CustomRoles.Scientist or CustomRoles.GuardianAngel or CustomRoles.Shapeshifter or CustomRoles.Phantom or CustomRoles.Tracker or CustomRoles.Noisemaker or CustomRoles.Viper or CustomRoles.Detective;
}
public static class Translator {
 public static readonly Dictionary<string,string[]> Rows=new(StringComparer.Ordinal); public static int Language=2;
 public static string GetString(string key)=>Rows.TryGetValue(key,out var row)&&row.Length>Language&&!string.IsNullOrEmpty(row[Language])?row[Language]:key;
}
