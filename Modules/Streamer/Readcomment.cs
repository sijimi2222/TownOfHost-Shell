using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TownOfHost;

// YouTube acquisition is opt-in. Only immutable comments cross the worker/main
// thread boundary; game settings, join queues and map votes stay on the main thread.
class StreamerInfo
{
    public static string StreamURL="";
    private static string streamkey="";
    public static float timer;
    public static Dictionary<int,HopeInfo> Hopeplayers=new();
    public static List<HopeInfo> JoindHopeplayers=new();
    public static int number;
    public static readonly List<(string Name,string Text)> RecentComments=new();
    public static int RecentCommentsVersion;
    public static bool HasStream=>!string.IsNullOrEmpty(streamkey);
    private static StreamerInfo current;
    private static int epoch,fetching;
    private static float lastFetchAt=-999f;
    private static bool savedLoaded,quitHook;
    private static readonly ConcurrentQueue<(int Epoch,LiveStreamComment Comment,bool Live)> received=new();
    private static readonly HashSet<string> seen=new(StringComparer.Ordinal);
    private static readonly Queue<string> seenOrder=new();
    private readonly HttpClient client;
    private readonly CancellationTokenSource cancellation=new();
    private readonly string video;
    private readonly int generation;
    private ChatData chatData;
    private static DateTime nextAttemptAt=DateTime.MinValue,lastErrorAt=DateTime.MinValue;
    private static bool voteWasEnabled;
    internal enum ConnectionStatus { Paused, Connecting, Waiting, Receiving, NetworkError, HttpError, InvalidPage, InvalidResponse }
    private static readonly object statusGate = new();
    private static int connectionStatus, statusVersion;
    internal static int StatusVersion => Volatile.Read(ref statusVersion);
    internal static ConnectionStatus Status => (ConnectionStatus)Volatile.Read(ref connectionStatus);
    internal static bool HasConnectionError => Status is ConnectionStatus.NetworkError or ConnectionStatus.HttpError or ConnectionStatus.InvalidPage or ConnectionStatus.InvalidResponse;
    internal static string StatusMessage => Status switch
    {
        ConnectionStatus.Connecting => "YouTubeチャットへ接続しています…",
        ConnectionStatus.Waiting or ConnectionStatus.Receiving => "接続済み。YouTubeのコメントを待っています…",
        ConnectionStatus.NetworkError => "YouTubeへ接続できません。通信環境を確認してください（再試行中）。",
        ConnectionStatus.HttpError => "YouTubeからコメントを取得できませんでした（HTTPエラー・再試行中）。",
        ConnectionStatus.InvalidPage => "チャット情報を取得できません。公開状態・URL・YouTube接続を確認してください（再試行中）。",
        ConnectionStatus.InvalidResponse => "YouTubeの応答を読み取れませんでした（再試行中）。",
        _ => "YouTubeコメント取得は停止中です。"
    };
    private void SetStatus(ConnectionStatus value)
    {
        lock (statusGate)
        {
            if (generation != Volatile.Read(ref epoch) || connectionStatus == (int)value) return;
            Volatile.Write(ref connectionStatus, (int)value); Interlocked.Increment(ref statusVersion);
        }
    }
    private enum FetchStage { Bootstrap, Poll }
    private FetchStage fetchStage;
    private bool loggedPollSuccess;
    private sealed class FetchFailure : Exception
    {
        internal readonly ConnectionStatus Status; internal readonly int HttpStatus;
        internal FetchFailure(ConnectionStatus status, int httpStatus = 0) : base("YouTube comment retrieval failed") { Status = status; HttpStatus = httpStatus; }
    }


    public static string ExtractVideoId(string url)=>StreamSafety.VideoId(url);
    public static bool TrySetStreamURL(string url)
    {
        string id=ExtractVideoId(url);if(id.Length==0)return false;
        string canonical="https://www.youtube.com/watch?v="+id;
        if(id!=streamkey)
        {
            StopNetwork();StreamURL=canonical;streamkey=id;Hopeplayers.Clear();JoindHopeplayers.Clear();seen.Clear();seenOrder.Clear();
            lock(RecentComments){RecentComments.Clear();RecentCommentsVersion++;}
            TownOfHost.Modules.YouTubeMapVote.Reset();
        }
        else StreamURL=canonical;
        Main.StreamURLConfig.Value=canonical;return true;
    }
    public static void SetstreamKey(){if(!string.IsNullOrWhiteSpace(StreamURL))TrySetStreamURL(StreamURL);}
    public static void LoadSavedURL()
    {
        if(!quitHook){Application.quitting+=(Action)StopNetwork;quitHook=true;}
        if(savedLoaded)return;savedLoaded=true;
        var saved=Main.StreamURLConfig?.Value;if(!string.IsNullOrEmpty(saved)&&!HasStream)TrySetStreamURL(saved);
    }
    public static void StopNetwork()
    {
        lock (statusGate)
        {
            Interlocked.Increment(ref epoch); Volatile.Write(ref connectionStatus, (int)ConnectionStatus.Paused); Interlocked.Increment(ref statusVersion);
        }
        var info=current;current=null;
        try{info?.cancellation.Cancel();info?.client.Dispose();}catch{}
        while(received.TryDequeue(out _)){}
        lastFetchAt=-999f;nextAttemptAt=DateTime.MinValue;
    }
    public static void CommentFrameTick(float deltaTime)
    {
        LoadSavedURL();DrainComments();
        bool host=AmongUsClient.Instance?.AmHost==true;
        bool vote=host&&TownOfHost.Modules.YouTubeMapVote.IsOn;
        if(voteWasEnabled&&!vote)TownOfHost.Modules.YouTubeMapVote.Reset();voteWasEnabled=vote;
        bool wanted=Main.ShowStreamComments?.Value==true || vote || host&&TownOfHost.Modules.ExtraFeatureOptions.JoinByCommentOnly?.GetBool()==true;
        if(!wanted || !(GameStates.IsLobby||GameStates.InGame))
        {if(current!=null)StopNetwork();return;}
        TryStartFetch();
    }
    private static void TryStartFetch()
    {
        if(!HasStream||Time.unscaledTime-lastFetchAt<3f||DateTime.UtcNow<nextAttemptAt)return;
        if(Interlocked.CompareExchange(ref fetching,1,0)!=0)return;
        lastFetchAt=Time.unscaledTime;
        current??=new StreamerInfo(streamkey,epoch);var info=current;
        _=Task.Run(async()=>
        {
            try
            {
                await info.FetchAsync().ConfigureAwait(false);
                if (info.generation == Volatile.Read(ref epoch)) nextAttemptAt = DateTime.UtcNow.AddMilliseconds(info.chatData?.PollDelayMilliseconds ?? 3000);
            }
            catch (OperationCanceledException) when (info.cancellation.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (info.generation == Volatile.Read(ref epoch))
                {
                    info.chatData = null; // Expired continuation or a bad response must re-bootstrap.
                    nextAttemptAt = DateTime.UtcNow.AddSeconds(30);
                    var status = error is FetchFailure failure ? failure.Status
                        : error is System.Text.Json.JsonException ? ConnectionStatus.InvalidResponse
                        : error is HttpRequestException or OperationCanceledException or IOException ? ConnectionStatus.NetworkError : ConnectionStatus.InvalidResponse;
                    info.SetStatus(status);
                    if (DateTime.UtcNow - lastErrorAt > TimeSpan.FromSeconds(30))
                    {
                        lastErrorAt = DateTime.UtcNow;
                        int http = error is FetchFailure f ? f.HttpStatus : 0;
                        // Never log exception messages, response bodies, URLs, keys, IDs or comment text.
                        Logger.Warn($"YouTube取得失敗 stage={info.fetchStage} reason={status} http={http}; 30秒後に再試行します。", "StreamerInfo");
                    }
                }
            }
            finally{Interlocked.Exchange(ref fetching,0);}
        });
    }
    private StreamerInfo(string id,int gen)
    {
        video=id;generation=gen;client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(12)};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ja,en;q=0.8");
    }
    private async Task<string> ReadLimited(HttpResponseMessage response, CancellationToken token)
    {
        if (!response.IsSuccessStatusCode) throw new FetchFailure(ConnectionStatus.HttpError, (int)response.StatusCode);
        const int max=2*1024*1024;
        if(response.Content.Headers.ContentLength>max)throw new FetchFailure(ConnectionStatus.InvalidResponse);
        using var input=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var output=new MemoryStream();byte[] buffer=new byte[8192];
        while(true){int size=await input.ReadAsync(buffer.AsMemory(),token).ConfigureAwait(false);if(size==0)break;if(output.Length+size>max)throw new FetchFailure(ConnectionStatus.InvalidResponse);output.Write(buffer,0,size);}
        return Encoding.UTF8.GetString(output.ToArray());
    }
    private void Queue(IEnumerable<LiveStreamComment> comments,bool live)
    {
        foreach(var item in comments){if(generation!=Volatile.Read(ref epoch)||received.Count>=512)return;received.Enqueue((generation,item,live));}
    }
    public async Task FetchAsync()
    {
        // ResponseHeadersRead does not time out body reads: bound the complete operation.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(12)); var token = deadline.Token;
        if (chatData == null)
        {
            fetchStage = FetchStage.Bootstrap; SetStatus(ConnectionStatus.Connecting);
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.youtube.com/live_chat?is_popout=1&v=" + video);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            string html = await ReadLimited(response, token).ConfigureAwait(false);
            bool ready = StreamCommentParser.TryBootstrap(html, out var session, out var comments, out var flags);
            if (generation == Volatile.Read(ref epoch))
                Logger.Info($"YouTube bootstrap http=200 config={flags.Config} renderer={flags.Renderer} key_present={flags.Key} version_present={flags.Version} continuation_present={flags.Continuation} initial_messages={comments.Count}", "StreamerInfo");
            if (!ready) throw new FetchFailure(ConnectionStatus.InvalidPage);
            chatData = session; Queue(comments, false); SetStatus(ConnectionStatus.Waiting); return;
        }
        fetchStage = FetchStage.Poll;
        using var post = new HttpRequestMessage(HttpMethod.Post, "https://www.youtube.com/youtubei/v1/live_chat/get_live_chat?key=" + Uri.EscapeDataString(chatData.key));
        post.Headers.TryAddWithoutValidation("Origin", "https://www.youtube.com");
        post.Headers.TryAddWithoutValidation("X-Youtube-Client-Name", "1");
        post.Headers.TryAddWithoutValidation("X-Youtube-Client-Version", chatData.clientVersion);
        post.Content = new StringContent(chatData.Build(), Encoding.UTF8, "application/json");
        using var result = await client.SendAsync(post, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        string json = await ReadLimited(result, token).ConfigureAwait(false);
        if (!StreamCommentParser.TryLiveChat(json, out var chat)) throw new FetchFailure(ConnectionStatus.InvalidResponse);
        var live = StreamCommentParser.ParseActions(chat["actions"]); Queue(live, true);
        if (!chatData.TryUpdateContinuation(json)) chatData = null;
        SetStatus(ConnectionStatus.Waiting);
        if (!loggedPollSuccess && generation == Volatile.Read(ref epoch))
        {
            loggedPollSuccess = true;
            Logger.Info($"YouTube poll http=200 valid_chat=True messages={live.Count}; 描画キューへ追加しました。", "StreamerInfo");
        }
    }
    private static void DrainComments()
    {
        int budget=200;
        while(budget-->0&&received.TryDequeue(out var item))
        {
            if(item.Epoch!=epoch||!seen.Add(item.Comment.Id))continue;
            seenOrder.Enqueue(item.Comment.Id);while(seenOrder.Count>512)seen.Remove(seenOrder.Dequeue());
            lock(RecentComments){RecentComments.Add((item.Comment.Name,item.Comment.Text));while(RecentComments.Count>30)RecentComments.RemoveAt(0);RecentCommentsVersion++;}
            current?.SetStatus(ConnectionStatus.Receiving);
            if(!item.Live||AmongUsClient.Instance?.AmHost!=true)continue;
            TownOfHost.Modules.YouTubeMapVote.OnComment(item.Comment.Channel,item.Comment.Text);
            string remove=Main.RemoveWord?.Value;
            if(!string.IsNullOrWhiteSpace(remove)&&item.Comment.Text.Contains(remove))
            {foreach(var k in Hopeplayers.Where(p=>p.Value.AccountID==item.Comment.Channel).Select(p=>p.Key).ToArray())Hopeplayers.Remove(k);continue;}
            var hope=HopeInfo.TryCrateHopeInfo(item.Comment.Text,item.Comment.Channel,item.Comment.Name);if(hope==null)continue;
            var match=Hopeplayers.FirstOrDefault(x=>x.Value.AccountID==hope.AccountID);
            if(match.Value!=null){hope.SetId(match.Key);Hopeplayers[match.Key]=hope;continue;}
            if(Hopeplayers.Count>=200||JoindHopeplayers.Any(x=>x.AccountID==hope.AccountID))continue;
            hope.SetId(number++);Hopeplayers.Add(hope.Id,hope);
        }
    }
    public static void JoinGame(){if(AmongUsClient.Instance?.AmHost==true){timer=0;TownOfHost.Modules.YouTubeMapVote.Reset();}}
    public static void DisconnectInternal(){StopNetwork();JoindHopeplayers.Clear();TownOfHost.Modules.YouTubeMapVote.Reset();}
    public static bool JoinPlayer(InnerNet.ClientData player)
    {
        if(AmongUsClient.Instance?.AmHost!=true||TownOfHost.Modules.ExtraFeatureOptions.JoinByCommentOnly?.GetBool()!=true||!HasStream)return true;
        return player!=null&&(Hopeplayers.Values.Any(i=>i.PlayerName==player.PlayerName)||JoindHopeplayers.Any(i=>i.PlayerName==player.PlayerName));
    }
    public static void LeftPlayer(NetworkedPlayerInfo info){if(info!=null&&AmongUsClient.Instance?.AmHost==true)JoindHopeplayers.RemoveAll(i=>i.PlayerName==info.PlayerName);}
    public static void ChangeList(List<string> names)
    {
        if(AmongUsClient.Instance?.AmHost!=true)return;
        JoindHopeplayers.Clear();foreach(var pair in Hopeplayers.Where(x=>names.Contains(x.Value.PlayerName)).ToArray()){JoindHopeplayers.Add(pair.Value);Hopeplayers.Remove(pair.Key);}
    }
    public static void FixUpdate(){CommentFrameTick(Time.unscaledDeltaTime);}
}
