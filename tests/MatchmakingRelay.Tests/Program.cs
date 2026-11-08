using System.Net;
using System.Text;
using System.Text.Json;
using TownOfHost.Modules;
using Client = TownOfHost.Modules.MatchmakingRelayClient;

static class Program
{
    static int passed;
    const string Lobby = "{\"roomCode\":\"ABCDEF\",\"players\":5}";
    static readonly string SessionId = new('a',32);
    static readonly string Token = new('b',64);
    static HttpResponseMessage Json(object value, int status=200) => new((HttpStatusCode)status) {Content=new StringContent(JsonSerializer.Serialize(value),Encoding.UTF8,"application/json")};
    static HttpResponseMessage Start() => Json(new {session_id=SessionId,session_token=Token,expires_in=600,heartbeat_interval=120});
    static HttpResponseMessage Update(long seq) => Json(new {ok=true,sequence=seq,expires_in=600,session_token=new string('c',64)});
    static void Check(bool condition) {if(!condition)throw new Exception("Assertion failed (sensitive values omitted).");}
    static Client Make(Handler h, Func<long> clock=null) => new(new HttpClient(h),"https://relay.example/",clock);
    static async Task Test(string name,Func<Task> run) {await run();passed++;Console.WriteLine("PASS "+name);}
    public static async Task Main(string[] args)
    {
        await Test("start/update/end, isolated bearer, no shared-secret header",async()=>{
            var h=new Handler((r,b,n)=>Task.FromResult(n==1?Start():n==2?Update(1):Json(new{ok=true,ended=true})));
            var c=Make(h);Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Success);
            Check(h.Calls[0].Path=="/v2/sessions/start"&&h.Calls[0].Bearer=="");
            Check(h.Calls[1].Bearer==Token);Check(h.Calls.All(x=>!x.SharedSecret));
            Check(h.Calls.All(x=>!x.Body.Contains("messageId")&&!x.Body.Contains("account_id")));
            Check((await c.SendAsync("",true)).Status==Client.Outcome.Ended);Check(!c.HasSession);
        });
        await Test("lost start response retries same bootstrap identifier",async()=>{
            var h=new Handler((r,b,n)=>n==1?throw new HttpRequestException("sensitive omitted"):Task.FromResult(n==2?Start():Update(1)));
            var c=Make(h);Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Retry);
            Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Success);Check(h.Calls[0].Body==h.Calls[1].Body);
        });
        await Test("timeout retry preserves same operation and original snapshot",async()=>{
            var h=new Handler((r,b,n)=>n==2?throw new HttpRequestException():Task.FromResult(n==1?Start():Update(1)));
            var c=Make(h);Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Retry);
            Check((await c.SendAsync("{\"players\":9}",false)).Status==Client.Outcome.Success);
            Check(h.Calls[1].Body==h.Calls[2].Body);Check(c.LastAppliedLobby==Lobby);
        });
        await Test("401/409/410 terminal responses clear capability",async()=>{
            foreach(var status in new[]{401,409,410}){
                var h=new Handler((r,b,n)=>Task.FromResult(n==1?Start():Json(new{error="sensitive omitted"},status)));
                var c=Make(h);var result=await c.SendAsync(Lobby,false);Check(result.Status!=Client.Outcome.Success&&result.Status!=Client.Outcome.Retry);Check(!c.HasSession);
                await c.SendAsync(Lobby,false);Check(h.Calls.Count==2);
            }
        });
        await Test("429 honours bounded Retry-After and never prints error body",async()=>{
            var h=new Handler((r,b,n)=>{var res=n==1?Start():Json(new{error="sensitive omitted"},429);res.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(120));return Task.FromResult(res);});
            var c=Make(h);var result=await c.SendAsync(Lobby,false);Check(result.Status==Client.Outcome.Retry&&result.RetrySeconds==120);
        });
        await Test("expired local lease stops without a new session or remote update",async()=>{
            long now=1000;var h=new Handler((r,b,n)=>Task.FromResult(n==1?Start():Update(1)));var c=Make(h,()=>now);
            await c.SendAsync(Lobby,false);now+=601000;Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Ended);Check(h.Calls.Count==2);
        });
        await Test("malformed/oversized success response is terminal and safe",async()=>{
            foreach(var text in new[]{"{",new string('x',8193)}){
                var h=new Handler((r,b,n)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(text,Encoding.UTF8,"application/json")}));
                var c=Make(h);Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Rejected);Check(!c.HasSession);
            }
        });
        await Test("concurrent sends cannot issue multiple starts",async()=>{
            var wait=new TaskCompletionSource<HttpResponseMessage>();var h=new Handler((r,b,n)=>n==1?wait.Task:Task.FromResult(Update(1)));var c=Make(h);
            var first=c.SendAsync(Lobby,false);Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Retry);Check(h.Calls.Count==1);
            wait.SetResult(Start());Check((await first).Status==Client.Outcome.Success);
        });
        await Test("end before session never creates a recruitment",async()=>{
            var h=new Handler((r,b,n)=>throw new Exception());var c=Make(h);Check((await c.SendAsync("",true)).Status==Client.Outcome.Ended);Check(h.Calls.Count==0);
        });
        await Test("non-HTTPS and credential-bearing endpoints rejected",()=>{
            foreach(var uri in new[]{"http://relay.example/","https://user:pass@relay.example/","https://relay.example/?token=x"}){
                bool rejected=false;try{_=new Client(new HttpClient(),uri);}catch(ArgumentException){rejected=true;}Check(rejected);
            }return Task.CompletedTask;
        });
        await Test("end resolves a lost rotated update response before deleting",async()=>{
            var h=new Handler((r,b,n)=>n==2?throw new HttpRequestException():Task.FromResult(n==1?Start():n==3?Update(1):Json(new{ok=true,ended=true})));
            var c=Make(h);Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Retry);
            Check((await c.SendAsync("",true)).Status==Client.Outcome.Ended);
            Check(h.Calls.Count==4&&h.Calls[1].Body==h.Calls[2].Body);
            Check(h.Calls[3].Path=="/v2/sessions/end"&&h.Calls[3].Bearer==new string('c',64));
        });
        await Test("maintenance is terminal and never retried as a network failure",async()=>{
            var h=new Handler((r,b,n)=>Task.FromResult(Json(new{error="maintenance"},503)));var c=Make(h);
            Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Maintenance);
            Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Ended&&h.Calls.Count==1);
        });
        await Test("lost end response preserves identical sequence and body",async()=>{
            var h=new Handler((r,b,n)=>n==3?throw new HttpRequestException():Task.FromResult(n==1?Start():n==2?Update(1):Json(new{ok=true,ended=true})));
            var c=Make(h);await c.SendAsync(Lobby,false);Check((await c.SendAsync("",true)).Status==Client.Outcome.Retry);
            Check((await c.SendAsync("",true)).Status==Client.Outcome.Ended&&h.Calls[2].Body==h.Calls[3].Body);
        });
        await Test("production ignores local override and development accepts literal loopback only",()=>{
            Check(MatchmakingRelayEndpoint.Resolve("https://relay.example/","http://127.0.0.1:8791/",false)=="https://relay.example/");
            Check(MatchmakingRelayEndpoint.Resolve("https://relay.example/","http://127.0.0.1:8791/",true)=="http://127.0.0.1:8791/");
            foreach(var value in new[]{"http://relay.example/","https://relay.example/","http://localhost.evil/","http://127.0.0.1:8791/?token=x","none"})Check(MatchmakingRelayEndpoint.Resolve("https://relay.example/",value,true)=="");
            bool rejected=false;try{_=new Client(new HttpClient(),"http://127.0.0.1:8791/");}catch(ArgumentException){rejected=true;}Check(rejected);
            _=new Client(new HttpClient(),"http://127.0.0.1:8791/",allowLoopback:true);return Task.CompletedTask;
        });
        await Test("unrecognized 503 body stays bounded retry without exposing server text",async()=>{
            var h=new Handler((r,b,n)=>Task.FromResult(n==1?Start():Json(new{error="untrusted-response-placeholder"},503)));
            var c=Make(h);Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Retry);
        });
        await Test("headers and response streams both stop at the total request deadline",async()=>{
            var never=new TaskCompletionSource<HttpResponseMessage>();var header=new Handler((r,b,n)=>never.Task);var c=Make(header);var timer=System.Diagnostics.Stopwatch.StartNew();
            Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Retry&&timer.Elapsed<TimeSpan.FromSeconds(15));
            var body=new Handler((r,b,n)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StalledContent()}));c=Make(body);timer.Restart();
            Check((await c.SendAsync(Lobby,false)).Status==Client.Outcome.Retry&&timer.Elapsed<TimeSpan.FromSeconds(15));
        });
        if(args.Contains("--workerd")) await Test("C# client to local workerd: start, rotated update, end",async()=>{
            var data=JsonSerializer.Serialize(new{hostName="Host",roomCode="ABCDEF",state="Lobby",players=5,maxPlayers=15,progressPercent=0,region="Asia",map="Skeld",gameMode="スタンダード",shellVersion="2.1.0",threadComment=""});
            var c=new Client(new HttpClient(new LocalBridge()),"http://127.0.0.1:8791/",allowLoopback:true);
            Check((await c.SendAsync(data,false)).Status==Client.Outcome.Success);
            Check((await c.SendAsync("",true)).Status==Client.Outcome.Ended);
            Check(!c.HasSession);
        });
        Console.WriteLine($"C# matchmaking transport: {passed} passed, 0 failed.");
    }
    sealed class StalledContent : HttpContent
    {
        internal StalledContent(){Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");}
        protected override bool TryComputeLength(out long length){length=0;return false;}
        protected override Task SerializeToStreamAsync(Stream stream,TransportContext context)=>Task.CompletedTask;
        protected override Task<Stream> CreateContentReadStreamAsync()=>Task.FromResult<Stream>(new StalledStream());
    }
    sealed class StalledStream : Stream
    {
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken cancellation)=>new TaskCompletionSource<int>().Task;
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
    sealed class LocalBridge : HttpMessageHandler
    {
        readonly HttpClient local=new(new HttpClientHandler{AllowAutoRedirect=false});
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            using var relay=new HttpRequestMessage(request.Method,"http://127.0.0.1:8791"+request.RequestUri.AbsolutePath);
            relay.Content=new StringContent(await request.Content.ReadAsStringAsync(),Encoding.UTF8,"application/json");
            relay.Headers.TryAddWithoutValidation("CF-Connecting-IP","198.51.100.240");
            relay.Headers.Authorization=request.Headers.Authorization;
            return await local.SendAsync(relay,token);
        }
    }
    sealed class Handler : HttpMessageHandler
    {
        internal sealed record Call(string Path,string Body,string Bearer,bool SharedSecret);
        internal List<Call> Calls=new();
        readonly Func<HttpRequestMessage,string,int,Task<HttpResponseMessage>> respond;
        internal Handler(Func<HttpRequestMessage,string,int,Task<HttpResponseMessage>> respond){this.respond=respond;}
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var body=await request.Content.ReadAsStringAsync();Calls.Add(new(request.RequestUri.AbsolutePath,body,request.Headers.Authorization?.Parameter??"",request.Headers.Contains("X-Relay-Secret")));
            return await respond(request,body,Calls.Count);
        }
    }
}
