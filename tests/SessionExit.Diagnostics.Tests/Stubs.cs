namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)]public sealed class HarmonyPatch:Attribute {public Type Target;public string Method;public HarmonyPatch(Type type,string method){Target=type;Method=method;}}
 [AttributeUsage(AttributeTargets.Method)]public sealed class HarmonyPrefix:Attribute{}
 [AttributeUsage(AttributeTargets.Method)]public sealed class HarmonyPostfix:Attribute{}
 [AttributeUsage(AttributeTargets.Method)]public sealed class HarmonyFinalizer:Attribute{}
 [AttributeUsage(AttributeTargets.Method)]public sealed class HarmonyPriority:Attribute{public HarmonyPriority(int n){}}
 public static class Priority{public const int First=800,Last=0;}
}
namespace InnerNet {public enum DisconnectReasons{ExitGame,Error}public class InnerNetClient {public void DisconnectInternal(){}}}
namespace UnityEngine {public static class Application {
 static Action handlers;public static int Subscriptions;public static event Action quitting{add{Subscriptions++;handlers+=value;}remove{handlers-=value;}}
 public static void Quit()=>handlers?.Invoke();
}}
namespace TownOfHost {
 public class AmongUsClient:InnerNet.InnerNetClient{public void ExitGame(){}public void OnActiveSceneChange(){}}
 public class HudManager{public void OnDestroy(){}}
 public class MainMenuManager{public void Start(){}public void LateUpdate(){}}
 public class VersionShower{public void Start(){}}
 public class ModManager{public void LateUpdate(){}}
 internal class SeasideMainMenuPatch {internal class HomeView {private void Invoke(string id,bool leavesHome){}}}
 public static class Logger{public static readonly List<string> Lines=new();public static void Info(string message,string tag)=>Lines.Add(tag+" "+message);}
 internal class StreamerInfo {internal enum ConnectionStatus{Paused,Receiving}internal static ConnectionStatus Status;public static void StopNetwork(){} }
}
namespace TownOfHost.Modules {public static class CustomNetObject{public static readonly List<object> AllObjects=new();public static void Reset(){}}}
namespace TownOfHost.Roles.Core {public static class CustomRoleManager{public static readonly Dictionary<byte,object> AllActiveRoles=new();public static void Dispose(){}}}
namespace TownOfHost.Development {internal static class ShellDebugRuntime {internal static int Stops;internal static void Stop()=>Stops++;}}
