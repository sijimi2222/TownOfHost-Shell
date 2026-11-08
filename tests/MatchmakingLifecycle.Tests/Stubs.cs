using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string method){}}
 [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPostfix:Attribute{}
 [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPrefix:Attribute{}
 [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPriority:Attribute{public HarmonyPriority(int v){}}
 public static class Priority{public const int Last=0;}
}
namespace InnerNet {public class InnerNetClient{public void FixedUpdate(){}}}
namespace TownOfHost.Roles.Core {public enum CountTypes{Impostor,Jackal,Remotekiller,GrimReaper,MilkyWay,Pavlov,StandMaster,Villain,Crewmate}}
namespace TownOfHost.Modules {public static class MatchmakingWordManager{public const int MaxCommentLength=1000;public static string GetCurrentWord()=>"";}}
namespace TownOfHost {
 public static class Utils{}
 public static class Main {public static string MatchmakingRelayUrl="https://relay.example/";public static string MatchmakingDevelopmentRelayUrl="";public const string PluginShowVersion="2.1.0";public static NormalOptions NormalOptions=new();public static bool IsAndroid()=>false;}
 public class NormalOptions {public byte MapId=0;public int MaxPlayers=15;}
 public static class Options {public static FakeOption GameMode=new();}
 public class FakeOption{public string GetString()=>"スタンダード";}
 public static class TextExtensions{public static string RemoveHtmlTags(this string s)=>s;}
 public class GameStartManager{public Button HostPrivateButton=new(),HostPublicButton=new();public void Start(){}public void MakePublic(){}}
 public class Button{public Label buttonText=new();}
 public class Label{public string text="";public void DestroyTranslator(){}}
 public class AmongUsClient{public static AmongUsClient Instance=new();public bool AmHost=true,IsGameStarted;public List<int> allClients=new(){1,2,3,4,5};public int GameId=1;public void ExitGame(){}public void OnDisconnected(){}public void StartGame(){}}
 public static class GameCode{public static string IntToGameName(int i)=>i==1?"ABCDEF":"UVWXYZ";}
 public static class GameStates{public static bool IsInGame=false,IsLobby=true;}
 public class Region{public string Name="Asia";}
 public class ServerManager{public static ServerManager Instance=new();public Region CurrentRegion=new();}
 public static class Constants{public static string[] MapNames={"Skeld"};}
 public class PlayerData{public string PlayerName="Host";}
 public class PlayerControl{public static PlayerControl LocalPlayer=new();public PlayerData Data=new();public bool IsAlive()=>true;public Roles.Core.CountTypes GetCountTypes()=>Roles.Core.CountTypes.Crewmate;}
 public static class PlayerCatch{public static List<PlayerControl> AllPlayerControls=new(){PlayerControl.LocalPlayer};}
 public class GameData{public static GameData Instance=new();public int TotalTasks=10,CompletedTasks=0;}
 public static class Logger{public static ConcurrentQueue<string> Lines=new();public static void Info(string text,string name)=>Lines.Enqueue(text);public static void Warn(string text,string name)=>Lines.Enqueue(text);public static void seeingame(string text)=>Lines.Enqueue(text);public static void Exception(Exception e,string name)=>Lines.Enqueue("Exception type: "+e.GetType().Name);}
 internal static class LifecycleHarness {
  internal static HttpClient Http=new(new FakeHandler());
  internal sealed class Session {internal string Token;internal bool Ended;internal string Room;}
  internal sealed record Call(string Path,string Body);
  internal static ConcurrentQueue<Call> Calls=new();
  internal static Dictionary<string,Session> Sessions=new();
  internal static List<string> Tokens=new();
  internal static bool FailNextUpdate, FailAllRequests, Maintenance;
  internal static TaskCompletionSource<bool> HoldUpdate,UpdateEntered;
  static string NewToken(){var s=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();Tokens.Add(s);return s;}
  static HttpResponseMessage Json(object value,int status=200)=>new((HttpStatusCode)status){Content=new StringContent(JsonSerializer.Serialize(value),Encoding.UTF8,"application/json")};
  sealed class FakeHandler:HttpMessageHandler{
   protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
    var body=await request.Content.ReadAsStringAsync();var path=request.RequestUri.AbsolutePath;Calls.Enqueue(new(path,body));
    if(Maintenance)return Json(new{error="maintenance"},503);
    if(FailAllRequests)throw new HttpRequestException("Details intentionally withheld.");
    if(request.Headers.Contains("X-Relay-Secret"))return Json(new{error="legacy_rejected"},400);
    using var doc=JsonDocument.Parse(body);var root=doc.RootElement;
    if(path.EndsWith("/start")){var id=Guid.NewGuid().ToString("N");var capability=NewToken();Sessions[id]=new(){Token=capability};return Json(new{session_id=id,session_token=capability,expires_in=600,heartbeat_interval=120});}
    var sid=root.GetProperty("session_id").GetString();
    if(!Sessions.TryGetValue(sid,out var session)||session.Token!=request.Headers.Authorization?.Parameter)return Json(new{error="invalid_token"},401);
    if(path.EndsWith("/end")){session.Ended=true;return Json(new{ok=true,ended=true});}
    if(session.Ended)return Json(new{error="session_ended"},410);
    if(FailNextUpdate){FailNextUpdate=false;throw new HttpRequestException("Details intentionally withheld.");}
    if(HoldUpdate!=null){var wait=HoldUpdate;HoldUpdate=null;UpdateEntered.TrySetResult(true);await wait.Task;}
    session.Room=root.GetProperty("lobby").GetProperty("roomCode").GetString();session.Token=NewToken();
    return Json(new{ok=true,sequence=root.GetProperty("sequence").GetInt64(),expires_in=600,session_token=session.Token});
   }
  }
 }
}
