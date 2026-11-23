using System.Net.Sockets;using System.Text;using System.Text.Json;using System.Text.Json.Nodes;using TownOfHost;using TownOfHost.Modules;
partial class Program{
static int passed;static void Check(bool b,string m="assertion failed"){if(!b)throw new Exception(m);}static void Test(string n,Action a){a();passed++;Console.WriteLine("PASS "+n);}
static string Request(LoopbackOverlayServer server,string path="/state",string headers="",string host=null,string method="GET"){using var c=new TcpClient();c.Connect("127.0.0.1",server.Port);c.ReceiveTimeout=3000;using var s=c.GetStream();var data=Encoding.ASCII.GetBytes(method+" "+path+" HTTP/1.1\r\nHost: "+(host??"localhost:"+server.Port)+"\r\n"+headers+"\r\n");s.Write(data);using var r=new StreamReader(s);return r.ReadToEnd();}
static void Reset(){YouTubeMapVote.Reset();YouTubeMapVote.Enabled=new(){Value=true};AmongUsClient.Instance=new();TownOfHost.Main.NormalOptions.MapId=99;}
static void Main(){
if(Environment.GetEnvironmentVariable("TOHS_LIVE_PROBE")=="1") throw new InvalidOperationException("Live probes are disabled; mock API tests only.");
if(Environment.GetEnvironmentVariable("TOHS_STREAM_EOF_ONLY")=="1"){RunNormalEof();Console.WriteLine($"RESULT {passed} passed, 0 failed (mock normal EOF and continuation; no external requests)");return;}
if(Environment.GetEnvironmentVariable("TOHS_CHAT_DIAGNOSTICS_ONLY")=="1"){RunChatDiagnostics();Console.WriteLine($"RESULT {passed} passed, 0 failed (mock HTTP/gRPC diagnostics; no external requests)");return;}

foreach(var url in new[]{"https://www.youtube.com/watch?v=AbCdef12_-3","https://youtu.be/AbCdef12_-3?si=test","https://youtube.com/live/AbCdef12_-3","https://m.youtube.com/shorts/AbCdef12_-3","https://www.youtube.com/embed/AbCdef12_-3"})Test("supported YouTube URL "+new Uri(url).Host+new Uri(url).AbsolutePath,()=>Check(StreamSafety.VideoId(url)=="AbCdef12_-3"));
foreach(var url in new[]{"http://youtube.com/watch?v=AbCdef12_-3","https://youtube.com.evil/watch?v=AbCdef12_-3","https://evilyoutube.com/watch?v=AbCdef12_-3","https://youtube.com@evil.com/watch?v=AbCdef12_-3","https://x@youtube.com/watch?v=AbCdef12_-3","https://youtube.com:444/watch?v=AbCdef12_-3","https://youtu.be/short","https://youtube.com/watch?v=AbCdef12_-3&v=AbCdef12_-3","https://youtu.be/AbCdef12_-3/extra","file:///youtube.com/watch?v=AbCdef12_-3"})Test("reject unsupported URL form "+passed,()=>Check(StreamSafety.VideoId(url)==""));
Test("reject oversized URL",()=>Check(StreamSafety.VideoId(new string('a',2049))==""));
Test("safe Japanese Unicode and TMP text",()=>Check(StreamSafety.Plain("<b>あ😀\u0001</b>")=="＜b＞あ😀＜/b＞"));
Test("scalar truncation preserves surrogate pair",()=>Check(StreamSafety.Plain("😀日本語",2)=="😀日"));
Test("color rejects CSS injection",()=>Check(StreamSafety.Color("red;url(x)")=="#ffffff"&&StreamSafety.Color("#aAbB00")=="#aAbB00"));
Test("roles hidden by default including host/local",()=>{foreach(var dead in new[]{true,false})Check(!StreamSafety.RevealRoles(0,true,dead));});
Test("roles never exposed in lobby",()=>{foreach(var mode in new[]{0,1,2,99})Check(!StreamSafety.RevealRoles(mode,false,true));});
Test("dead-only role option requires death",()=>Check(!StreamSafety.RevealRoles(1,true,false)&&StreamSafety.RevealRoles(1,true,true)));
Test("explicit spoiler mode only in game",()=>Check(StreamSafety.RevealRoles(2,true,false)));
Test("unknown role mode fails closed",()=>Check(!StreamSafety.RevealRoles(99,true,true)));
foreach (bool enabled in new[] { false, true }) Test("retired map vote inert with saved flag " + enabled, () => {
 Reset(); YouTubeMapVote.Enabled.Value=enabled;
 foreach(var command in new[]{"!polus","!airship","！エアシップ","!skeld"}) YouTubeMapVote.OnComment("viewer",command);
 for(int i=0;i<3;i++)YouTubeMapVote.ApplyAtGameStart();
 Check(!YouTubeMapVote.IsOn && YouTubeMapVote.Summary()=="" && YouTubeMapVote.Version==0 && TownOfHost.Main.NormalOptions.MapId==99);
});
Test("retired map vote safe without host or settings",()=>{YouTubeMapVote.Enabled=null;AmongUsClient.Instance=null;YouTubeMapVote.OnComment(null,null);YouTubeMapVote.Reset();YouTubeMapVote.ApplyAtGameStart();Check(!YouTubeMapVote.IsOn);AmongUsClient.Instance=new();});
using(var server=new LoopbackOverlayServer()){
Test("actual loopback listener starts",()=>Check(server.Start(0)&&server.Port>0));
Test("GET overlay HTML",()=>Check(Request(server,"/").StartsWith("HTTP/1.1 200")&&Request(server,"/").Contains("text/html")));
Test("GET snapshot UTF8 JSON",()=>{server.Publish("{\"name\":\"日本語😀\"}");Check(Request(server).Contains("日本語😀"));});
Test("no caching or CORS and restrictive CSP",()=>{var r=Request(server);Check(r.Contains("Cache-Control: no-store")&&r.Contains("default-src 'none'")&&!r.Contains("Access-Control-Allow-Origin"));});
Test("127.0.0.1 Host accepted",()=>Check(Request(server,host:"127.0.0.1:"+server.Port).StartsWith("HTTP/1.1 200")));
Test("foreign Host denied DNS rebinding",()=>Check(Request(server,host:"evil.example:"+server.Port).StartsWith("HTTP/1.1 403")));
Test("foreign Origin denied",()=>Check(Request(server,headers:"Origin: https://evil.example\r\n").StartsWith("HTTP/1.1 403")));
Test("null Origin denied",()=>Check(Request(server,headers:"Origin: null\r\n").StartsWith("HTTP/1.1 403")));
Test("same Origin accepted",()=>Check(Request(server,headers:"Origin: http://localhost:"+server.Port+"\r\n").StartsWith("HTTP/1.1 200")));
Test("cross site denied",()=>Check(Request(server,headers:"Sec-Fetch-Site: cross-site\r\n").StartsWith("HTTP/1.1 403")));
Test("duplicate Host rejected",()=>Check(Request(server,headers:"Host: localhost:"+server.Port+"\r\n").StartsWith("HTTP/1.1 400")));
Test("POST cannot change state",()=>Check(Request(server,method:"POST").StartsWith("HTTP/1.1 405")));
Test("RPC/token/data paths not exposed",()=>{foreach(var p in new[]{"/auth","/token","/config","/state?secret=x","/../"})Check(Request(server,p).StartsWith("HTTP/1.1 404"));});
Test("request body denied",()=>Check(Request(server,headers:"Content-Length: 1\r\n").StartsWith("HTTP/1.1 403")));
Test("oversized snapshot discarded",()=>{server.Publish(new string('x',65537));Check(Request(server).EndsWith("{}"));});
Test("stale snapshot erased within two seconds",()=>{server.Publish("{\"stale\":true}");Thread.Sleep(2100);Check(Request(server).EndsWith("{}"));});
Test("page uses textContent not HTML injection",()=>Check(ObsOverlayPage.Html.Contains("textContent")&&!ObsOverlayPage.Html.Contains("innerHTML")&&!ObsOverlayPage.Html.Contains("https://")));
Test("stop cancels partial request and restart works",()=>{using var c=new TcpClient("127.0.0.1",server.Port);c.GetStream().Write(Encoding.ASCII.GetBytes("GET /"));Thread.Sleep(50);server.Stop();Check(!server.Running);Check(server.Start(0));Check(Request(server,"/").StartsWith("HTTP/1.1 200"));});
Test("port already occupied fails safely",()=>{using var other=new LoopbackOverlayServer();Check(!other.Start(server.Port));Check(!other.Running&&other.Start(0));});
}
RunOfficialApi();RunChatDiagnostics();RunNormalEof();RunLifecycle();RunSnapshots();RunCommentFix();RunInterestTests();RunInterestInFlight();RunInterestUi();RunSourceBoundaries();
Console.WriteLine($"RESULT {passed} passed, 0 failed (real loopback HTTP, no external requests)");}}
static class CloneExtensions{public static JsonNode DeepCloneForTest(this JsonNode n)=>JsonNode.Parse(n.ToJsonString());}
