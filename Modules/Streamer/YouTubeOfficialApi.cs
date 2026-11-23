using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TownOfHost;

internal enum YouTubeFailure { MissingCredentials, AuthenticationExpired, PermissionDenied, QuotaExceeded, NotLive, LiveEnded, ChatUnavailable, NotFound, InvalidResponse, TransportUnsupported, NetworkInterrupted, BufferOverflow, CredentialStorageFailure }
// Diagnostics contain fixed enums and HTTP status only: never URLs, bodies or credentials.
internal enum YouTubeOAuthStage { Unknown, Restore, Starting, BrowserOpen, WaitingForRedirect, CallbackValidation, TokenExchange, TokenValidation, ProtectedSave, ProtectedCleanup, Linked }
internal enum YouTubeOAuthCode { None, InvalidClient, InvalidRequest, InvalidGrant, UnauthorizedClient, AccessDenied, InvalidScope, RedirectMismatch, PolicyBlocked, RateLimited, ServerError, OtherGoogleError, StateMismatch, InvalidCallback, TimedOut, NetworkFailure, InvalidTokenResponse, ScopeMismatch, MissingRefreshToken, StorageFailure, Cancelled, UnexpectedFailure, ClientSecretRequired, ClientIdRequired, AuthorizationCodeRequired, RedirectUriRequired, GrantTypeRequired, CodeVerifierRequired }
internal sealed record YouTubeOAuthDiagnostic(YouTubeOAuthStage Stage, YouTubeOAuthCode Code = YouTubeOAuthCode.None, int HttpStatus = 0)
{
    internal string SafeLog => "YOUTUBE_OAUTH stage=" + (Enum.IsDefined(typeof(YouTubeOAuthStage), Stage) ? Stage : YouTubeOAuthStage.Unknown)
        + " code=" + (Enum.IsDefined(typeof(YouTubeOAuthCode), Code) ? Code : YouTubeOAuthCode.UnexpectedFailure)
        + " http=" + (HttpStatus >= 100 && HttpStatus <= 599 ? HttpStatus : 0);
    internal string UserMessage => (Code switch
    {
        YouTubeOAuthCode.InvalidClient => "GoogleがOAuthクライアントを拒否しました。CloudのDesktopクライアント設定を確認してください。",
        YouTubeOAuthCode.ClientSecretRequired => "GoogleがDesktopクライアントのclient_secretを要求しました。同じClient IDの設定をローカル保護取込ツールで保存し、再起動してください。",
        YouTubeOAuthCode.ClientIdRequired or YouTubeOAuthCode.AuthorizationCodeRequired or YouTubeOAuthCode.RedirectUriRequired or YouTubeOAuthCode.GrantTypeRequired or YouTubeOAuthCode.CodeVerifierRequired => "Googleが必須項目の不足を報告しました。送信検証と診断コードを確認してください。",
        YouTubeOAuthCode.InvalidRequest => "Googleがトークン交換要求を受け付けませんでした。診断コードを開発者へ確認してください。",
        YouTubeOAuthCode.InvalidGrant => "Googleが認証コードまたは更新許可を受け付けませんでした。コードの再利用・期限切れ・PKCE不一致などが考えられます。",
        YouTubeOAuthCode.AccessDenied => "Googleがアクセスを拒否しました。同意の拒否、Testingの未登録ユーザー、組織の制限などが考えられます。ブラウザーの案内を確認し、開発者にテストユーザー登録を確認してください。",
        YouTubeOAuthCode.UnauthorizedClient or YouTubeOAuthCode.PolicyBlocked => "Googleのクライアント設定または組織ポリシーで認証が許可されませんでした。",
        YouTubeOAuthCode.InvalidScope => "Googleが要求権限を受け付けませんでした。youtube.readonlyの設定を確認してください。",
        YouTubeOAuthCode.RedirectMismatch => "Googleがリダイレクト先を受け付けませんでした。Desktopクライアント種別を確認してください。",
        YouTubeOAuthCode.StateMismatch => "認証応答のstateが一致しないため、安全のため停止しました。",
        YouTubeOAuthCode.InvalidCallback => "認証応答の形式を検証できないため、安全のため停止しました。",
        YouTubeOAuthCode.TimedOut => "Google認証処理が時間切れになりました。通信状態と診断段階を確認してください。",
        YouTubeOAuthCode.NetworkFailure => "Google認証サーバーとの通信に失敗しました。接続状態を確認してください。",
        YouTubeOAuthCode.RateLimited => "Google認証のリクエストが制限されました。時間を置いて確認してください。",
        YouTubeOAuthCode.ServerError => "Google認証サーバー側のエラーです。時間を置いて確認してください。",
        YouTubeOAuthCode.ScopeMismatch => "要求と異なる権限が応答に含まれたため停止しました。権限の安全確認が必要です。",
        YouTubeOAuthCode.MissingRefreshToken => "認証応答に更新トークンがないため保存できません。診断コードを開発者へ確認してください。",
        YouTubeOAuthCode.InvalidTokenResponse => "Googleのトークン応答を検証できないため停止しました。",
        YouTubeOAuthCode.StorageFailure => "Windowsの認証情報の保存・復元に失敗しました。保存先の権限を確認してください。",
        YouTubeOAuthCode.Cancelled => "Google連携を中断しました（キャンセルまたは時間切れ）。",
        _ => "Google連携に失敗しました。診断段階とコードを確認してください。"
    }) + "\n" + SafeLog;
    internal static YouTubeOAuthCode GoogleCode(string code) => code switch
    {
        "invalid_client" => YouTubeOAuthCode.InvalidClient, "invalid_request" => YouTubeOAuthCode.InvalidRequest,
        "invalid_grant" => YouTubeOAuthCode.InvalidGrant, "unauthorized_client" => YouTubeOAuthCode.UnauthorizedClient,
        "access_denied" => YouTubeOAuthCode.AccessDenied, "invalid_scope" => YouTubeOAuthCode.InvalidScope,
        "redirect_uri_mismatch" => YouTubeOAuthCode.RedirectMismatch,
        "admin_policy_enforced" or "org_internal" => YouTubeOAuthCode.PolicyBlocked,
        _ => YouTubeOAuthCode.OtherGoogleError
    };
    internal static YouTubeOAuthDiagnostic FromGoogle(string json, int http)
    {
        var code = http == 429 ? YouTubeOAuthCode.RateLimited : http >= 500 ? YouTubeOAuthCode.ServerError : YouTubeOAuthCode.OtherGoogleError;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            {
                var known = GoogleCode(error.GetString());
                if (known != YouTubeOAuthCode.OtherGoogleError) code = known;
                // Match complete, fixed missing-parameter phrases only. Never retain or
                // emit free-form descriptions, parameter values, URLs or credentials.
                if (http == 400 && code == YouTubeOAuthCode.InvalidRequest && doc.RootElement.TryGetProperty("error_description", out var description) && description.ValueKind == JsonValueKind.String)
                    code = RequiredParameterCode(description.GetString());
            }
        }
        catch (JsonException) { }
        return new(YouTubeOAuthStage.TokenExchange, code, http);
    }
    private static YouTubeOAuthCode RequiredParameterCode(string description)
    {
        if (description == null || description.Length > 160) return YouTubeOAuthCode.InvalidRequest;
        string text = description.Trim();
        foreach (var entry in new[] {
            ("client_secret", YouTubeOAuthCode.ClientSecretRequired), ("client_id", YouTubeOAuthCode.ClientIdRequired),
            ("code", YouTubeOAuthCode.AuthorizationCodeRequired), ("redirect_uri", YouTubeOAuthCode.RedirectUriRequired),
            ("grant_type", YouTubeOAuthCode.GrantTypeRequired), ("code_verifier", YouTubeOAuthCode.CodeVerifierRequired) })
            if (text == entry.Item1 + " is missing." || text == "Missing required parameter: " + entry.Item1 || text == "Missing required parameter: " + entry.Item1 + ".") return entry.Item2;
        return YouTubeOAuthCode.InvalidRequest;
    }
    public override string ToString() => SafeLog;
}
internal sealed class YouTubeApiException : Exception
{
    internal YouTubeFailure Reason { get; }
    internal YouTubeOAuthDiagnostic OAuth { get; }
    internal bool Retryable => Reason == YouTubeFailure.NetworkInterrupted;
    internal YouTubeApiException(YouTubeFailure reason, YouTubeOAuthDiagnostic oauth = null) : base("YouTube official API: " + reason) { Reason = reason; OAuth = oauth; }
}
internal sealed class YouTubeCredential
{
    internal string Secret { get; }
    internal bool IsApiKey { get; }
    internal DateTimeOffset ExpiresAt { get; }
    internal bool Valid => !string.IsNullOrWhiteSpace(Secret) && Secret.Length <= 8192 && !Secret.Any(char.IsControl) && ExpiresAt > DateTimeOffset.UtcNow;
    internal YouTubeCredential(string secret, bool apiKey, DateTimeOffset expiresAt) { Secret = secret; IsApiKey = apiKey; ExpiresAt = expiresAt; }
    public override string ToString() => "YouTubeCredential[redacted]";
    internal void Apply(HttpRequestMessage request)
    {
        if (!Valid) throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired);
        if (IsApiKey) request.Headers.Add("x-goog-api-key", Secret);
        else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Secret);
    }
}
internal interface IYouTubeCredentialProvider { Task<YouTubeCredential> GetAsync(CancellationToken token); }
internal sealed class MissingYouTubeCredentials : IYouTubeCredentialProvider
{ public Task<YouTubeCredential> GetAsync(CancellationToken token) => Task.FromResult<YouTubeCredential>(null); }
internal sealed class MemoryYouTubeCredentials : IYouTubeCredentialProvider
{
    private readonly YouTubeCredential value;
    internal MemoryYouTubeCredentials(YouTubeCredential credential) => value = credential;
    public Task<YouTubeCredential> GetAsync(CancellationToken token) => Task.FromResult(value);
}
internal readonly record struct YouTubeChatBatch(IReadOnlyList<LiveStreamComment> Comments, string NextPageToken, bool Ended);
// A future, policy-approved backend can implement this interface. No relay exists here.
internal interface IYouTubeChatSource : IDisposable { Task RunAsync(string videoId, Action<YouTubeChatBatch> receive, CancellationToken token); }
internal static class YouTubeOfficialRuntime
{
    // No credentials, Cloud registration, browser or server is configured by default.
    internal static Func<IYouTubeChatSource> Factory = () => new OfficialYouTubeChatSource(new MissingYouTubeCredentials());
    internal static bool ParticipationPolicyReviewed { get; set; }
    internal static Action<YouTubeChatDiagnostic> Diagnostics;
    internal static void Trace(YouTubeChatDiagnostic value) { try { Diagnostics?.Invoke(value); } catch { } }
    internal static readonly string TermsUrl = "https://www.youtube.com/t/terms";
    internal static readonly string PrivacyUrl = "https://policies.google.com/privacy";
    internal static readonly string PermissionUrl = "https://myaccount.google.com/permissions";
    internal const string Attribution = "YouTube ライブチャット";
}
internal static class OfficialYouTubeJson
{
    internal static string LiveChatId(string json, string video)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) throw new YouTubeApiException(YouTubeFailure.InvalidResponse);
            foreach (var item in items.EnumerateArray())
            {
                if (Text(item, "id", 32) != video) continue;
                if (!item.TryGetProperty("liveStreamingDetails", out var details)) throw new YouTubeApiException(YouTubeFailure.NotLive);
                if (Text(details, "actualEndTime", 128).Length > 0) throw new YouTubeApiException(YouTubeFailure.LiveEnded);
                var chat = Text(details, "activeLiveChatId", 4096);
                if (chat.Length > 0) return chat;
                throw new YouTubeApiException(Text(details, "actualStartTime", 128).Length > 0 ? YouTubeFailure.ChatUnavailable : YouTubeFailure.NotLive);
            }
            throw new YouTubeApiException(YouTubeFailure.NotFound);
        }
        catch (JsonException) { throw new YouTubeApiException(YouTubeFailure.InvalidResponse); }
    }
    internal static string Text(JsonElement element, string key, int max)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String) return "";
        string text = value.GetString() ?? "";
        if (text.Length > max) throw new YouTubeApiException(YouTubeFailure.InvalidResponse);
        return text;
    }
    internal static YouTubeFailure Error(HttpStatusCode status, string body)
    {
        string reason = "";
        try { using var doc = JsonDocument.Parse(body); if (doc.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("errors", out var a) && a.ValueKind == JsonValueKind.Array && a.GetArrayLength() > 0) reason = Text(a[0], "reason", 128); } catch { }
        if (status == HttpStatusCode.Unauthorized || reason is "authError" or "keyInvalid") return YouTubeFailure.AuthenticationExpired;
        if (reason is "quotaExceeded" or "dailyLimitExceeded" or "rateLimitExceeded" || (int)status == 429) return YouTubeFailure.QuotaExceeded;
        if (reason == "liveChatEnded") return YouTubeFailure.LiveEnded;
        if (reason == "liveChatDisabled") return YouTubeFailure.ChatUnavailable;
        if (status == HttpStatusCode.Forbidden) return YouTubeFailure.PermissionDenied;
        if (status == HttpStatusCode.NotFound) return YouTubeFailure.NotFound;
        return (int)status >= 500 ? YouTubeFailure.NetworkInterrupted : YouTubeFailure.InvalidResponse;
    }
}
internal sealed class OfficialYouTubeChatSource : IYouTubeChatSource
{
    private readonly IYouTubeCredentialProvider credentials;
    private readonly HttpClient client;
    private string resume;
    private readonly TimeSpan callDuration, videoDuration, headerDuration;
    private readonly Action<YouTubeChatDiagnostic> diagnostics;
    internal OfficialYouTubeChatSource(IYouTubeCredentialProvider provider, HttpClient http = null, TimeSpan? renewalInterval = null,
        Action<YouTubeChatDiagnostic> progress = null, TimeSpan? videoTimeout = null, TimeSpan? headerTimeout = null)
    {
        callDuration = renewalInterval ?? TimeSpan.FromMinutes(20);
        if (callDuration < TimeSpan.FromMilliseconds(10) || callDuration > TimeSpan.FromMinutes(20)) throw new ArgumentOutOfRangeException(nameof(renewalInterval));
        videoDuration = videoTimeout ?? TimeSpan.FromSeconds(12); headerDuration = headerTimeout ?? TimeSpan.FromSeconds(15);
        if (videoDuration < TimeSpan.FromMilliseconds(10) || videoDuration > TimeSpan.FromSeconds(12)
            || headerDuration < TimeSpan.FromMilliseconds(10) || headerDuration > TimeSpan.FromSeconds(15)) throw new ArgumentOutOfRangeException("timeout");
        diagnostics = progress;
        credentials = provider ?? new MissingYouTubeCredentials();
        client = http ?? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.None }) { Timeout = Timeout.InfiniteTimeSpan };
    }
    private async Task<YouTubeCredential> Authorize(CancellationToken token)
    {
        var value = await credentials.GetAsync(token).ConfigureAwait(false);
        if (value == null) throw new YouTubeApiException(YouTubeFailure.MissingCredentials);
        if (!value.Valid) throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired);
        return value;
    }
    internal static async Task<string> ReadJson(HttpResponseMessage response, CancellationToken token)
    {
        const int limit = 2 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > limit) throw new YouTubeApiException(YouTubeFailure.InvalidResponse);
        using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var memory = new MemoryStream(); var buffer = new byte[8192];
        while (true) { int n = await input.ReadAsync(buffer.AsMemory(), token).AsTask().WaitAsync(token).ConfigureAwait(false); if (n == 0) break; if (memory.Length + n > limit) throw new YouTubeApiException(YouTubeFailure.InvalidResponse); memory.Write(buffer, 0, n); }
        return System.Text.Encoding.UTF8.GetString(memory.ToArray());
    }
    public async Task RunAsync(string videoId, Action<YouTubeChatBatch> receive, CancellationToken token)
    {
        var trace = new YouTubeChatTrace(diagnostics);
        try
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(videoId ?? "", @"\A[A-Za-z0-9_-]{11}\z")) throw new YouTubeApiException(YouTubeFailure.InvalidResponse);
            trace.At(YouTubeChatStage.Credential); trace.Emit(YouTubeChatCode.Started);
            var credential = await Authorize(token).ConfigureAwait(false);
            trace.Emit(YouTubeChatCode.Completed);
            string chat;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(videoDuration);
                try
                {
                    trace.At(YouTubeChatStage.VideosRequest); trace.Emit(YouTubeChatCode.Started);
                    using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/videos?part=liveStreamingDetails&id=" + Uri.EscapeDataString(videoId));
                    credential.Apply(request);
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
                    trace.HttpStatus = (int)response.StatusCode; trace.HttpMajor = response.Version.Major; trace.Emit(YouTubeChatCode.HeadersReceived);
                    trace.At(YouTubeChatStage.VideosResponse); trace.Emit(YouTubeChatCode.Started);
                    string json = await ReadJson(response, deadline.Token).ConfigureAwait(false);
                    trace.Emit(YouTubeChatCode.Completed);
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = new YouTubeApiException(OfficialYouTubeJson.Error(response.StatusCode, json));
                        trace.Fail(error, YouTubeChatCode.HttpRejected); throw error;
                    }
                    trace.At(YouTubeChatStage.ActiveLiveChatId); trace.Emit(YouTubeChatCode.Started);
                    chat = OfficialYouTubeJson.LiveChatId(json, videoId);
                    trace.Emit(YouTubeChatCode.Completed);
                }
                catch (OperationCanceledException error) when (!token.IsCancellationRequested && deadline.IsCancellationRequested)
                { trace.Fail(error, YouTubeChatCode.Timeout); throw; }
            }
            trace.HttpStatus = trace.HttpMajor = 0;
            trace.At(YouTubeChatStage.Credential); trace.Emit(YouTubeChatCode.Started);
            credential = await Authorize(token).ConfigureAwait(false);
            trace.Emit(YouTubeChatCode.Completed);
            using var call = CancellationTokenSource.CreateLinkedTokenSource(token);
            var remaining = credential.ExpiresAt - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired);
            call.CancelAfter(remaining < callDuration ? remaining : callDuration);
            using var rpc = new HttpRequestMessage(HttpMethod.Post, "https://youtube.googleapis.com/youtube.api.v3.V3DataLiveChatMessageService/StreamList") { Version = HttpVersion.Version20, VersionPolicy = HttpVersionPolicy.RequestVersionExact };
            credential.Apply(rpc); rpc.Headers.Add("te", "trailers"); rpc.Headers.Add("grpc-accept-encoding", "identity");
            rpc.Content = new ByteArrayContent(YouTubeGrpcProtocol.Frame(YouTubeGrpcProtocol.Request(chat, resume)));
            rpc.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            try
            {
                trace.At(YouTubeChatStage.StreamRequest); trace.Emit(YouTubeChatCode.Started);
                using var streamResponse = await SendHeaders(rpc, call.Token, trace).ConfigureAwait(false);
                trace.HttpStatus = (int)streamResponse.StatusCode; trace.HttpMajor = streamResponse.Version.Major;
                trace.Emit(YouTubeChatCode.Completed);
                trace.At(YouTubeChatStage.StreamHeaders); trace.Emit(YouTubeChatCode.HeadersReceived);
                if (!streamResponse.IsSuccessStatusCode)
                {
                    var error = new YouTubeApiException(OfficialYouTubeJson.Error(streamResponse.StatusCode, await ReadJson(streamResponse, call.Token).ConfigureAwait(false)));
                    trace.Fail(error, YouTubeChatCode.HttpRejected); throw error;
                }
                string mediaType = streamResponse.Content.Headers.ContentType?.MediaType;
                if (streamResponse.Version.Major != 2 || (mediaType != "application/grpc" && mediaType != "application/grpc+proto")) throw new YouTubeApiException(YouTubeFailure.TransportUnsupported);
                trace.Emit(YouTubeChatCode.Http2Established);
                bool trailersOnly = streamResponse.Headers.Contains("grpc-status");
                trace.At(YouTubeChatStage.GrpcStatus); CheckGrpcStatus(streamResponse.Headers, false, trace);
                using var inputStream = await streamResponse.Content.ReadAsStreamAsync(call.Token).ConfigureAwait(false);
                bool first = true;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    if (!credential.Valid) { trace.Emit(YouTubeChatCode.Renewal); return; }
                    trace.At(YouTubeChatStage.Receive); if (first) trace.Emit(YouTubeChatCode.Started);
                    byte[] payload = await YouTubeGrpcProtocol.ReadFrame(inputStream, call.Token).ConfigureAwait(false);
                    if (payload == null) break;
                    if (first) trace.Emit(YouTubeChatCode.Completed);
                    trace.At(YouTubeChatStage.Parse); if (first) trace.Emit(YouTubeChatCode.Started);
                    var batch = YouTubeGrpcProtocol.Response(payload);
                    if (first) trace.Emit(YouTubeChatCode.Completed);
                    first = false;
                    receive(batch);
                    if (!string.IsNullOrEmpty(batch.NextPageToken)) resume = batch.NextPageToken;
                    if (batch.Ended) throw new YouTubeApiException(YouTubeFailure.LiveEnded);
                }
                trace.At(YouTubeChatStage.GrpcStatus); CheckGrpcStatus(streamResponse.TrailingHeaders, !trailersOnly, trace);
                // A validated OK trailer ends this RPC successfully. The owner waits
                // before reopening with the last continuation token; this is not a failure.
                trace.Emit(YouTubeChatCode.EndOfStream);
                return;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested && call.IsCancellationRequested)
            { trace.Emit(YouTubeChatCode.Renewal); /* Existing planned credential/call renewal. */ }
        }
        catch (Exception error) { trace.Fail(error); throw; }
    }
    private async Task<HttpResponseMessage> SendHeaders(HttpRequestMessage request, CancellationToken token, YouTubeChatTrace trace)
    {
        using var headers = CancellationTokenSource.CreateLinkedTokenSource(token);
        headers.CancelAfter(headerDuration);
        try { return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headers.Token).ConfigureAwait(false); }
        catch (OperationCanceledException error) when (!token.IsCancellationRequested && headers.IsCancellationRequested)
        { trace.Fail(error, YouTubeChatCode.Timeout); throw; }
    }
    private static YouTubeFailure GrpcFailure(int code) => code switch
    { 16 => YouTubeFailure.AuthenticationExpired, 7 => YouTubeFailure.PermissionDenied, 8 => YouTubeFailure.QuotaExceeded, 9 => YouTubeFailure.ChatUnavailable, 5 => YouTubeFailure.NotFound, 14 => YouTubeFailure.NetworkInterrupted, _ => YouTubeFailure.InvalidResponse };
    private static void CheckGrpcStatus(HttpHeaders headers, bool required, YouTubeChatTrace trace)
    {
        if (!headers.TryGetValues("grpc-status", out var values))
        {
            if (required) { var error = new YouTubeApiException(YouTubeFailure.InvalidResponse); trace.Fail(error, YouTubeChatCode.MissingGrpcStatus); throw error; }
            return;
        }
        if (!int.TryParse(values.SingleOrDefault(), out int code)) throw new YouTubeApiException(YouTubeFailure.InvalidResponse);
        trace.GrpcStatus = code;
        if (code == 0) { trace.Emit(YouTubeChatCode.GrpcOk); return; }
        var rejected = new YouTubeApiException(GrpcFailure(code));
        trace.Fail(rejected, YouTubeChatCode.GrpcRejected); throw rejected;
    }
    public void Dispose() => client.Dispose();
}
