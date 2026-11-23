using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TownOfHost;

// Display integration originally adapted from TownOfHostForE. Acquisition now uses
// only the official API source; no page parser or private-API fallback remains.
class StreamerInfo
{
    public static string StreamURL = "";
    private static string streamkey = "";
    public static float timer;
    internal static readonly StreamInterestList Interests = new();
    public static readonly List<(string Name, string Text)> RecentComments = new();
    public static int RecentCommentsVersion;
    public static bool HasStream => !string.IsNullOrEmpty(streamkey);
    private static StreamerInfo current;
    private static int epoch, fetching;
    private static bool savedLoaded, quitHook;
    private static readonly ConcurrentQueue<(int Epoch, LiveStreamComment Comment, bool Live, int InterestGeneration)> received = new();
    private static readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private static readonly Queue<string> seenOrder = new();
    private readonly IYouTubeChatSource source;
    private readonly CancellationTokenSource cancellation = new();
    private readonly string video;
    private readonly int generation;
    private readonly DateTimeOffset started = DateTimeOffset.UtcNow;
    private int terminal, overflow, erasePersonalData, idleCompletions;
    private bool historyReceived;
    private string continuationToken, latestCommentId;
    internal enum ConnectionStatus { Paused, Connecting, Waiting, Receiving, NetworkError, MissingCredentials, AuthenticationExpired, PermissionDenied, QuotaExceeded, NotLive, LiveEnded, ChatUnavailable, NotFound, InvalidResponse, TransportUnsupported, BufferOverflow, CredentialStorageFailure }
    private static readonly object statusGate = new();
    private static int connectionStatus, statusVersion;
    internal static int StatusVersion => Volatile.Read(ref statusVersion);
    internal static ConnectionStatus Status => (ConnectionStatus)Volatile.Read(ref connectionStatus);
    internal static bool HasConnectionError => Status is not (ConnectionStatus.Paused or ConnectionStatus.Waiting or ConnectionStatus.Receiving or ConnectionStatus.Connecting);
    internal static string StatusMessage => Status switch
    {
        ConnectionStatus.Connecting => "YouTube公式APIへ接続しています…",
        ConnectionStatus.Waiting or ConnectionStatus.Receiving => "接続済み。YouTubeのコメントを待っています…",
        ConnectionStatus.NetworkError => "YouTubeとの通信が切れました（回数を制限して再試行）。",
        ConnectionStatus.MissingCredentials => "YouTube公式APIの初期認証が未設定のため停止しています。",
        ConnectionStatus.CredentialStorageFailure => "認証情報を安全に保存・復元できないため停止しました。Google連携画面を確認してください。",
        ConnectionStatus.AuthenticationExpired => "Google認証が失効しました。再認証が必要です。",
        ConnectionStatus.PermissionDenied => "YouTube APIへのアクセスが許可されていません。",
        ConnectionStatus.QuotaExceeded => "YouTube APIの利用上限に達したため停止しました。",
        ConnectionStatus.NotLive => "この動画は現在ライブ配信中ではありません。",
        ConnectionStatus.LiveEnded => "YouTubeライブ配信は終了しました。",
        ConnectionStatus.ChatUnavailable => "ライブチャットが無効、終了済み、または取得できません。",
        ConnectionStatus.NotFound => "指定されたライブ配信を取得できません。",
        ConnectionStatus.TransportUnsupported => "この環境ではYouTubeのHTTP/2ストリーミングを利用できません。",
        ConnectionStatus.BufferOverflow => "コメントの受信上限を超えたため安全に停止しました。",
        ConnectionStatus.InvalidResponse => "YouTube公式APIの応答を読み取れないため停止しました。",
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
    public static string ExtractVideoId(string url) => StreamSafety.VideoId(url);
    public static bool TrySetStreamURL(string url)
    {
        string id = ExtractVideoId(url); if (id.Length == 0) return false;
        string canonical = "https://www.youtube.com/watch?v=" + id;
        if (id != streamkey)
        {
            StopNetwork(); StreamURL = canonical; streamkey = id; Interests.Clear(); ClearDisplay();
        }
        else StreamURL = canonical;
        Main.StreamURLConfig.Value = canonical; return true;
    }
    public static void SetstreamKey() { if (!string.IsNullOrWhiteSpace(StreamURL)) TrySetStreamURL(StreamURL); }
    public static void LoadSavedURL()
    {
        YouTubeAuthRuntime.Tick();
        if (!quitHook) { Application.quitting += (Action)DisconnectInternal; quitHook = true; }
        if (savedLoaded) return; savedLoaded = true;
        var saved = Main.StreamURLConfig?.Value; if (!string.IsNullOrEmpty(saved) && !HasStream) TrySetStreamURL(saved);
    }
    private static void ClearDisplay()
    {
        seen.Clear(); seenOrder.Clear();
        lock (RecentComments) { RecentComments.Clear(); RecentCommentsVersion++; }
    }
    public static void StopNetwork()
    {
        Interests.Stop();
        lock (statusGate)
        {
            Interlocked.Increment(ref epoch); Volatile.Write(ref connectionStatus, (int)ConnectionStatus.Paused); Interlocked.Increment(ref statusVersion);
        }
        var info = current; current = null;
        if (info != null)
        {
            info.cancellation.Cancel();
            try { info.source.Dispose(); } catch { /* Best-effort disposal; cancellation remains authoritative. */ }
        }
        while (received.TryDequeue(out _)) { }
        ClearDisplay();
    }
    public static void CommentFrameTick(float deltaTime)
    {
        LoadSavedURL();
        if (AmongUsClient.Instance?.AmHost != true || !YouTubeOfficialRuntime.ParticipationPolicyReviewed) Interests.Stop();
        bool wanted = Main.ShowStreamComments?.Value == true;
        if (!wanted || !(GameStates.IsLobby || GameStates.InGame))
        { if (current != null) StopNetwork(); return; }
        // Restore credentials/create HTTP services only when comments are requested in-game.
        YouTubeAuthRuntime.EnsureInitialized();
        DrainComments(); TryStartFetch();
    }
    internal static int NormalWaitSeconds(bool progressed, bool canResume, ref int consecutiveIdle)
    {
        if (progressed && canResume) { consecutiveIdle = 0; return 5; }
        // Quiet/empty streams remain usable, but cannot spin or repeatedly fetch
        // an initial page at full speed. State and delay are both bounded.
        consecutiveIdle = Math.Min(Math.Max(consecutiveIdle, 0), 4) + 1;
        return Math.Min(60, 5 << (consecutiveIdle - 1));
    }
    private static void TryStartFetch()
    {
        if (!HasStream || (current != null && Volatile.Read(ref current.terminal) == 1)) return;
        if (Interlocked.CompareExchange(ref fetching, 1, 0) != 0) return;
        current ??= new StreamerInfo(streamkey, epoch); var info = current;
        _ = Task.Run(async () =>
        {
            int failures = 0;
            bool stopLogged = false;
            long attemptStarted = Stopwatch.GetTimestamp();
            try
            {
                while (!info.cancellation.IsCancellationRequested)
                {
                    attemptStarted = Stopwatch.GetTimestamp();
                    string priorToken = info.continuationToken, priorComment = info.latestCommentId;
                    try
                    {
                        await info.FetchAsync().ConfigureAwait(false);
                        bool progressed = !string.Equals(priorToken, info.continuationToken, StringComparison.Ordinal)
                            || !string.Equals(priorComment, info.latestCommentId, StringComparison.Ordinal);
                        bool canResume = !string.IsNullOrEmpty(info.continuationToken);
                        int normalWait = NormalWaitSeconds(progressed, canResume, ref info.idleCompletions);
                        info.SetStatus(ConnectionStatus.Waiting);
                        YouTubeOfficialRuntime.Trace(new(YouTubeChatStage.Retry, progressed && canResume ? YouTubeChatCode.Renewal : YouTubeChatCode.IdleBackoff,
                            ElapsedMs: YouTubeChatTrace.Elapsed(attemptStarted), Attempt: failures + 1, RetryDelaySeconds: normalWait));
                        failures = 0; // Successful EOF/renewal never consumes network retries.
                        await Task.Delay(TimeSpan.FromSeconds(normalWait), info.cancellation.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (info.cancellation.IsCancellationRequested) { break; }
                    catch (Exception error)
                    {
                        if (info.generation != Volatile.Read(ref epoch)) break;
                        var reason = error is YouTubeApiException api ? api.Reason
                            : error is HttpRequestException or IOException or OperationCanceledException ? YouTubeFailure.NetworkInterrupted : YouTubeFailure.InvalidResponse;
                        if (Volatile.Read(ref info.overflow) != 0) reason = YouTubeFailure.BufferOverflow;
                        if (reason is YouTubeFailure.AuthenticationExpired or YouTubeFailure.CredentialStorageFailure) Interlocked.Exchange(ref info.erasePersonalData, 1);
                        info.SetStatus(reason == YouTubeFailure.NetworkInterrupted ? ConnectionStatus.NetworkError
                            : Enum.TryParse(reason.ToString(), out ConnectionStatus status) ? status : ConnectionStatus.InvalidResponse);
                        // Never include exception text, URLs, IDs, tokens or comments in diagnostics.
                        Logger.Warn($"YouTube official API stopped reason={reason}; attempt={failures + 1}", "StreamerInfo");
                        if (reason != YouTubeFailure.NetworkInterrupted || ++failures >= 4)
                        {
                            YouTubeOfficialRuntime.Trace(new(YouTubeChatStage.Stop, reason == YouTubeFailure.NetworkInterrupted ? YouTubeChatCode.RetryExhausted : YouTubeChatCode.TerminalFailure,
                                ExceptionKind: YouTubeChatDiagnostic.Kind(error), Cause: YouTubeChatDiagnostic.InnerKind(error), ElapsedMs: YouTubeChatTrace.Elapsed(attemptStarted),
                                Attempt: reason == YouTubeFailure.NetworkInterrupted ? failures : failures + 1, Failure: reason, SocketStatus: YouTubeChatDiagnostic.SocketCode(error)));
                            stopLogged = true; break;
                        }
                        int waitSeconds = 5 * (1 << (failures - 1));
                        YouTubeOfficialRuntime.Trace(new(YouTubeChatStage.Retry, YouTubeChatCode.RetryScheduled,
                            ExceptionKind: YouTubeChatDiagnostic.Kind(error), Cause: YouTubeChatDiagnostic.InnerKind(error), ElapsedMs: YouTubeChatTrace.Elapsed(attemptStarted),
                            Attempt: failures, RetryDelaySeconds: waitSeconds, Failure: reason, SocketStatus: YouTubeChatDiagnostic.SocketCode(error)));
                        await Task.Delay(TimeSpan.FromSeconds(waitSeconds), info.cancellation.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (info.cancellation.IsCancellationRequested) { }
            finally
            {
                if (!stopLogged) YouTubeOfficialRuntime.Trace(new(YouTubeChatStage.Stop, YouTubeChatCode.Cancelled,
                    ElapsedMs: YouTubeChatTrace.Elapsed(attemptStarted), Attempt: failures + 1));
                Interlocked.Exchange(ref info.terminal, 1);
                try { info.source.Dispose(); } catch { }
                Interlocked.Exchange(ref fetching, 0);
            }
        });
    }
    private StreamerInfo(string id, int gen) { video = id; generation = gen; source = YouTubeOfficialRuntime.Factory(); }
    private void Queue(IEnumerable<LiveStreamComment> comments, bool live) => QueueForReception(comments, live, Interests.Generation);
    private void QueueForReception(IEnumerable<LiveStreamComment> comments, bool live, int interestGeneration)
    {
        foreach (var item in comments)
        {
            // Pair the epoch check and enqueue with StopNetwork's invalidation.
            // A producer cannot append personal data after the stop-time queue clear.
            lock (statusGate)
            {
                if (generation != Volatile.Read(ref epoch) || cancellation.IsCancellationRequested) return;
                if (received.Count >= 512) { Interlocked.Exchange(ref overflow, 1); throw new YouTubeApiException(YouTubeFailure.BufferOverflow); }
                received.Enqueue((generation, item, live, interestGeneration));
            }
        }
    }
    public async Task FetchAsync()
    {
        SetStatus(ConnectionStatus.Connecting);
        bool allowHistory = !historyReceived;
        await source.RunAsync(video, batch =>
        {
            if (generation != Volatile.Read(ref epoch) || cancellation.IsCancellationRequested) return;
            int interestGeneration = Interests.Generation;
            if (!string.IsNullOrEmpty(batch.NextPageToken)) continuationToken = batch.NextPageToken;
            if (batch.Comments.Count > 0) { historyReceived = true; latestCommentId = batch.Comments[batch.Comments.Count - 1].Id; }
            for (int index = 0; index < batch.Comments.Count; index++)
            {
                var comment = batch.Comments[index];
                // streamList may send historical messages in several responses. A first-batch
                // flag cannot protect intake; require a publish time after this connection.
                bool live = comment.PublishedAt.HasValue && comment.PublishedAt.Value >= started;
                // A large historical response only needs its latest display window.
                // Never drop a newly published participation message this way.
                if (!live && (!allowHistory || index < batch.Comments.Count - 30)) continue;
                QueueForReception(new[] { comment }, live, interestGeneration);
            }
            SetStatus(ConnectionStatus.Waiting);
            if (batch.Ended) throw new YouTubeApiException(YouTubeFailure.LiveEnded);
        }, cancellation.Token).ConfigureAwait(false);
    }
    private static void DrainComments()
    {
        if (current != null && Interlocked.Exchange(ref current.erasePersonalData, 0) != 0)
        {
            while (received.TryDequeue(out _)) { }
            Interests.Clear(); ClearDisplay(); return;
        }
        if (current != null && Volatile.Read(ref current.terminal) == 1) Interests.Stop();
        int budget = 200;
        while (budget-- > 0 && received.TryDequeue(out var item))
        {
            if (item.Epoch != epoch || !seen.Add(item.Comment.Id)) continue;
            seenOrder.Enqueue(item.Comment.Id); while (seenOrder.Count > 512) seen.Remove(seenOrder.Dequeue());
            lock (RecentComments)
            {
                RecentComments.Add((StreamSafety.Plain(item.Comment.Name, 80), StreamSafety.Plain(item.Comment.Text, 2048)));
                while (RecentComments.Count > 30) RecentComments.RemoveAt(0); RecentCommentsVersion++;
            }
            if (!HasConnectionError) current?.SetStatus(ConnectionStatus.Receiving);
            if (!item.Live || AmongUsClient.Instance?.AmHost != true || !YouTubeOfficialRuntime.ParticipationPolicyReviewed) continue;
            Interests.OnComment(item.Comment, item.Live, item.InterestGeneration, Main.JoinWord?.Value, Main.RemoveWord?.Value);
        }
        if (current != null && Volatile.Read(ref current.terminal) == 1) Interests.Stop();
    }
    public static void JoinGame() { if (AmongUsClient.Instance?.AmHost == true) { timer = 0; Interests.Clear(); } }
    public static void DisconnectInternal() { StopNetwork(); Interests.Clear(); }
    internal static void StartInterestReception()
    { if (AmongUsClient.Instance?.AmHost == true && HasStream && YouTubeOfficialRuntime.ParticipationPolicyReviewed) Interests.Start(); }
    internal static void StopInterestReception() => Interests.Stop();
    internal static void ResetInterestList() => Interests.Reset();
    public static void FixUpdate() { CommentFrameTick(Time.unscaledDeltaTime); }
}
