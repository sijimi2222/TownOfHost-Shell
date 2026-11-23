// Native Unity services are stubbed; CSV parsing, initialization, lookups and fallback use production Translator.cs.
public enum SupportedLangs { English=0,Latam=1,Brazilian=2,Portuguese=3,Korean=4,Russian=5,Dutch=6,Filipino=7,French=8,German=9,Italian=10,Japanese=11,Spanish=12,SChinese=13,TChinese=14,Irish=15 }
public enum StringNames { NativeFallback }
public class TranslationController {
 public static TranslationController Instance=new(); public static bool InstanceExists=true;
 public Language currentLanguage=new(); public class Language { public SupportedLangs languageID=SupportedLangs.Japanese; }
 public string GetString(StringNames name,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object> args)=>"native fallback";
}
public class DestroyableSingleton<T> where T:new() { public static T Instance=new(); }
namespace Il2CppSystem { public class Object {} }
namespace Il2CppInterop.Runtime.InteropTypes.Arrays { public class Il2CppReferenceArray<T> { public Il2CppReferenceArray(int length){} } }
namespace HarmonyLib { public static class Extensions { public static string Join(this IEnumerable<string> values,string delimiter)=>string.Join(delimiter,values); } }
namespace TownOfHost.Attributes { public class PluginModuleInitializerAttribute:Attribute {} }
namespace TownOfHost.Roles.Core { public class NamespaceMarker {} }
namespace TownOfHost {
 public enum CustomRoles { Cakeshop,Vega,Altair,Amateras }
 public sealed class Flag { public bool Value; }
 public static class Main {
  // Skip filesystem custom-language/template operations in this isolated executable; no game files are touched.
  public static bool IsAndroid()=>true;
  public static Flag ForceJapanese=new(),JapaneseRoleName=new(){Value=true},CustomName=new();
  public static bool UseingJapanese=true;
 }
 public static class Event { public static HashSet<string> OptionLoad=new(); public static bool Special,IsChristmas,IsHalloween,White,GoldenWeek,Tanabata; public static bool CheckRole(CustomRoles role)=>true; }
 public static class EnumHelper { public static T[] GetAllValues<T>() where T:struct,Enum=>Enum.GetValues<T>(); }
 public static class Logger {
  public static readonly List<string> Warnings=new();
  public static void Warn(string message,string tag)=>Warnings.Add(message);
  public static void Info(string message,string tag){} public static void Error(string message,string tag){}
 }
}
