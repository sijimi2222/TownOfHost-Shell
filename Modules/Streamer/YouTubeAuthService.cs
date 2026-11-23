using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TownOfHost;

internal sealed class YouTubeOAuthSettings
{
    internal string ClientId { get; }
    private readonly string clientSecret;
    internal static bool ValidClientSecret(string value) => value != null && value.Length is > 0 and <= 4096 && value.All(c => c >= '!' && c <= '~');
    internal YouTubeOAuthSettings(string clientId, string desktopClientSecret = null)
    {
        if (!Regex.IsMatch(clientId ?? "", @"\A[A-Za-z0-9._-]{1,960}\.apps\.googleusercontent\.com\z")) throw new ArgumentException("Invalid desktop OAuth client configuration");
        if (desktopClientSecret != null && !ValidClientSecret(desktopClientSecret)) throw new ArgumentException("Invalid desktop OAuth client configuration");
        ClientId = clientId; clientSecret = desktopClientSecret;
    }
    internal void ApplyClientSecret(Dictionary<string, string> form)
    {
        if (form == null || !form.TryGetValue("client_id", out var id) || id != ClientId || form.ContainsKey("client_secret")
            || !form.TryGetValue("grant_type", out var grant) || grant is not ("authorization_code" or "refresh_token"))
            throw new YouTubeApiException(YouTubeFailure.MissingCredentials);
        // Explicit local configuration only; never retry a consumed authorization code.
        if (clientSecret != null) form["client_secret"] = clientSecret;
    }
    internal static YouTubeOAuthSettings Load(string path, string credentialDirectory = null)
    {
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 4096) throw new ArgumentException("Invalid desktop OAuth client configuration");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path), new JsonDocumentOptions { MaxDepth = 4 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1 || root.EnumerateObject().Any(p => p.Name != "client_id")) throw new ArgumentException("Only a desktop client ID may be configured");
        var id = OfficialYouTubeJson.Text(root, "client_id", 1024);
        var validated = new YouTubeOAuthSettings(id);
        credentialDirectory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TOH-Shell", "YouTube");
        return new(validated.ClientId, new YouTubeProtectedClientSecret(credentialDirectory).Load(validated.ClientId));
    }
    public override string ToString() => "YouTubeOAuthSettings[configured]";
}
internal interface IYouTubeAuthorizationFlow
{ Task<YouTubeOAuthTokens> AuthorizeAsync(YouTubeOAuthSettings settings, CancellationToken token); }
internal interface IYouTubeTokenExchange : IDisposable
{
    Task<YouTubeOAuthTokens> ExchangeAsync(Dictionary<string, string> form, CancellationToken token, string previousRefresh = null);
    Task RevokeAsync(string token, CancellationToken cancellation);
}
internal enum YouTubeAuthState { NeedsConfiguration, Restoring, Unlinked, Authorizing, Linked, RefreshRequired, Refreshing, Cancelled, AuthenticationError, Expired, StorageError, UnlinkedRemotePending, NetworkError, RateLimited }
internal sealed class YouTubeAuthService : IYouTubeCredentialProvider, IDisposable
{
    private readonly YouTubeOAuthSettings settings;
    private readonly IYouTubeTokenStore store;
    private readonly IYouTubeAuthorizationFlow flow;
    private readonly IYouTubeTokenExchange exchange;
    private readonly SemaphoreSlim gate = new(1);
    private readonly object stateGate = new();
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource authorization;
    private YouTubeOAuthTokens tokens;
    private Task initialization;
    private int state, revision, disposed, restart, phase;
    private YouTubeOAuthDiagnostic lastFailure;
    private readonly Action<YouTubeOAuthDiagnostic> diagnostic;
    internal YouTubeOAuthDiagnostic LastFailure => Volatile.Read(ref lastFailure);
    internal void Trace(YouTubeOAuthDiagnostic value)
    {
        if (Interlocked.Exchange(ref phase, (int)value.Stage) != (int)value.Stage) Interlocked.Increment(ref revision);
        try { diagnostic?.Invoke(value); } catch { /* Diagnostic sinks cannot affect credentials. */ }
    }
    private void Failure(YouTubeOAuthDiagnostic value)
    { Volatile.Write(ref lastFailure, value); Trace(value); }
    private YouTubeOAuthDiagnostic FailureAt(YouTubeOAuthCode code) => new((YouTubeOAuthStage)Volatile.Read(ref phase), code);
    internal YouTubeAuthState State => (YouTubeAuthState)Volatile.Read(ref state);
    internal int Revision => Volatile.Read(ref revision);
    internal bool HasSavedLink => Volatile.Read(ref tokens) != null && State != YouTubeAuthState.StorageError;
    internal bool IsBusy => State is YouTubeAuthState.Restoring or YouTubeAuthState.Authorizing or YouTubeAuthState.Refreshing;
    internal bool ConsumeRestart() => Interlocked.Exchange(ref restart, 0) != 0;
    private string AuthorizationProgress => (YouTubeOAuthStage)Volatile.Read(ref phase) switch
    {
        YouTubeOAuthStage.WaitingForRedirect or YouTubeOAuthStage.BrowserOpen => "ブラウザーで自分のGoogleアカウントを選び、読取権限に同意してください。応答を待っています…",
        YouTubeOAuthStage.CallbackValidation => "Googleからの応答とstateを検証しています…",
        YouTubeOAuthStage.TokenExchange => "Googleと認証情報を交換しています…",
        YouTubeOAuthStage.TokenValidation => "読取権限と認証情報を検証しています…",
        YouTubeOAuthStage.ProtectedSave => "WindowsのDPAPIで連携情報を保護保存しています…",
        _ => "Google連携を準備しています。パスワードはMODに入力しません。"
    };
    internal string Message => (State switch
    {
        YouTubeAuthState.NeedsConfiguration => "開発者が許可を確認した初期設定が必要です。テスター用設定手順を確認してください。",
        YouTubeAuthState.Restoring => "保護された連携情報を読み込んでいます…",
        YouTubeAuthState.Authorizing => AuthorizationProgress,
        YouTubeAuthState.Linked => "Google連携情報あり。URL入力で利用できます（接続時に有効性を確認）。",
        YouTubeAuthState.RefreshRequired => "連携情報は保存済みです。コメント取得時に認証を更新します。",
        YouTubeAuthState.Refreshing => "Google認証を更新しています…",
        YouTubeAuthState.Cancelled => LastFailure?.UserMessage ?? "Google連携を中断しました（キャンセルまたは時間切れ）。",
        YouTubeAuthState.Expired => LastFailure?.UserMessage ?? "認証が失効しました。Google連携をやり直してください。",
        YouTubeAuthState.NetworkError => LastFailure?.UserMessage ?? "Google認証サーバーへ接続できません。保存済みの連携は保持しています。",
        YouTubeAuthState.RateLimited => LastFailure?.UserMessage ?? "Google認証のリクエストが制限されました。時間を置いて確認してください。",
        YouTubeAuthState.StorageError => LastFailure?.UserMessage ?? "認証情報の保存・復元・削除に失敗したため停止しました。保存先の権限を確認してください。",
        YouTubeAuthState.AuthenticationError => LastFailure?.UserMessage ?? "Google連携に失敗しました。設定と同意を確認し、再度連携してください。",
        YouTubeAuthState.UnlinkedRemotePending => "端末の連携情報は削除しました。Google側の解除は権限管理画面で確認してください。",
        _ => "この端末はGoogle未連携です。初回のみブラウザー認証が必要です。"
    }) + (HasSavedLink && State is YouTubeAuthState.Cancelled or YouTubeAuthState.AuthenticationError or YouTubeAuthState.NetworkError or YouTubeAuthState.RateLimited
        ? "\n再連携前の保存情報は保持しています。有効性は次の接続時に確認します。" : "");
    internal YouTubeAuthService(YouTubeOAuthSettings configuration, IYouTubeTokenStore tokenStore, IYouTubeAuthorizationFlow authorizationFlow, IYouTubeTokenExchange tokenExchange, Action<YouTubeOAuthDiagnostic> progress = null)
    { settings = configuration; store = tokenStore; flow = authorizationFlow; exchange = tokenExchange; diagnostic = progress; state = (int)(settings == null ? YouTubeAuthState.NeedsConfiguration : YouTubeAuthState.Restoring); }
    private void Set(YouTubeAuthState value) { Volatile.Write(ref state, (int)value); Interlocked.Increment(ref revision); }
    internal Task InitializeAsync()
    {
        lock (stateGate) return initialization ??= Task.Run(async () =>
        {
            await gate.WaitAsync(lifetime.Token).ConfigureAwait(false);
            try
            {
                if (settings == null) { Set(YouTubeAuthState.NeedsConfiguration); return; }
                Trace(new(YouTubeOAuthStage.Restore));
                var restored = store.Load(settings.ClientId); lifetime.Token.ThrowIfCancellationRequested(); tokens = restored;
                Set(tokens == null ? YouTubeAuthState.Unlinked : tokens.Access.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1) ? YouTubeAuthState.Linked : YouTubeAuthState.RefreshRequired);
                // No token refresh or API request on startup or while ShowComments is OFF.
            }
            catch (YouTubeCredentialStorageException) { tokens = null; Failure(new(YouTubeOAuthStage.Restore, YouTubeOAuthCode.StorageFailure)); Set(YouTubeAuthState.StorageError); }
            finally { gate.Release(); }
        });
    }
    internal void CancelAuthorization() { lock (stateGate) authorization?.Cancel(); }
    private bool DeleteSaved()
    {
        tokens = null;
        try { store.Delete(); return true; } catch { Failure(new(YouTubeOAuthStage.ProtectedCleanup, YouTubeOAuthCode.StorageFailure)); Set(YouTubeAuthState.StorageError); return false; }
    }
    internal async Task BeginAsync(CancellationToken cancellation = default)
    {
        await InitializeAsync().WaitAsync(cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        if (Volatile.Read(ref disposed) != 0 || settings == null || State == YouTubeAuthState.Authorizing) return;
        if (!await gate.WaitAsync(0).ConfigureAwait(false)) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellation); operation.CancelAfter(TimeSpan.FromMinutes(10));
        lock (stateGate) authorization = operation;
        try
        {
            Volatile.Write(ref lastFailure, null); Trace(new(YouTubeOAuthStage.Starting));
            Set(YouTubeAuthState.Authorizing);
            var authorized = await flow.AuthorizeAsync(settings, operation.Token).ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            if (authorized?.Access?.Valid != true) throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired, new(YouTubeOAuthStage.TokenValidation, YouTubeOAuthCode.InvalidTokenResponse));
            if (string.IsNullOrWhiteSpace(authorized.RefreshToken)) throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired, new(YouTubeOAuthStage.TokenValidation, YouTubeOAuthCode.MissingRefreshToken));
            if (authorized.RefreshToken.Length > 8192 || YouTubeProtectedTokenStore.HasControl(authorized.RefreshToken)) throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired, new(YouTubeOAuthStage.TokenValidation, YouTubeOAuthCode.InvalidTokenResponse));
            lock (stateGate)
            {
                operation.Token.ThrowIfCancellationRequested();
                Trace(new(YouTubeOAuthStage.ProtectedSave));
                // Commit boundary: cancellation/failed consent before this point keeps the
                // previous grant. A successful durable save wins over a later cancellation.
                store.Save(settings.ClientId, authorized);
                tokens = authorized; Set(YouTubeAuthState.Linked); Interlocked.Exchange(ref restart, 1);
            }
            Trace(new(YouTubeOAuthStage.Linked));
        }
        catch (OperationCanceledException) { Failure(FailureAt(YouTubeOAuthCode.Cancelled)); Set(YouTubeAuthState.Cancelled); }
        catch (YouTubeCredentialStorageException) { tokens = null; Failure(FailureAt(YouTubeOAuthCode.StorageFailure)); Set(YouTubeAuthState.StorageError); }
        catch (YouTubeApiException error)
        {
            Failure(error.OAuth ?? FailureAt(YouTubeOAuthCode.UnexpectedFailure));
            Set(error.Reason == YouTubeFailure.QuotaExceeded ? YouTubeAuthState.RateLimited : error.Reason == YouTubeFailure.NetworkInterrupted ? YouTubeAuthState.NetworkError : YouTubeAuthState.AuthenticationError);
        }
        catch (Exception error) when (error is HttpRequestException or IOException)
        { Failure(FailureAt(YouTubeOAuthCode.NetworkFailure)); Set(YouTubeAuthState.NetworkError); }
        catch { Failure(FailureAt(YouTubeOAuthCode.UnexpectedFailure)); Set(YouTubeAuthState.AuthenticationError); }
        finally { lock (stateGate) { if (authorization == operation) authorization = null; } gate.Release(); }
    }
    public async Task<YouTubeCredential> GetAsync(CancellationToken cancellation)
    {
        await InitializeAsync().WaitAsync(cancellation).ConfigureAwait(false);
        if (settings == null) return null;
        if (State == YouTubeAuthState.Authorizing) throw new YouTubeApiException(YouTubeFailure.MissingCredentials);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellation, lifetime.Token);
        await gate.WaitAsync(operation.Token).ConfigureAwait(false);
        try
        {
            if (State == YouTubeAuthState.StorageError) throw new YouTubeApiException(YouTubeFailure.CredentialStorageFailure);
            if (tokens == null) return null;
            if (tokens.Access.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1) && tokens.Access.Valid) return tokens.Access;
            Set(YouTubeAuthState.Refreshing);
            var form = new Dictionary<string, string> { ["client_id"] = settings.ClientId, ["grant_type"] = "refresh_token", ["refresh_token"] = tokens.RefreshToken };
            settings.ApplyClientSecret(form);
            var renewed = await exchange.ExchangeAsync(form, operation.Token, tokens.RefreshToken).ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested(); store.Save(settings.ClientId, renewed);
            tokens = renewed; Set(YouTubeAuthState.Linked); return tokens.Access;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        { if (tokens != null) Set(tokens.Access.Valid ? YouTubeAuthState.Linked : YouTubeAuthState.RefreshRequired); throw; }
        catch (YouTubeCredentialStorageException) { Failure(new(YouTubeOAuthStage.ProtectedSave, YouTubeOAuthCode.StorageFailure)); DeleteSaved(); Set(YouTubeAuthState.StorageError); throw new YouTubeApiException(YouTubeFailure.CredentialStorageFailure); }
        catch (YouTubeApiException error) when (error.Reason == YouTubeFailure.CredentialStorageFailure) { throw; }
        catch (YouTubeApiException error) when (error.Reason is YouTubeFailure.NetworkInterrupted or YouTubeFailure.QuotaExceeded or YouTubeFailure.InvalidResponse)
        { Failure(error.OAuth ?? FailureAt(error.Reason == YouTubeFailure.QuotaExceeded ? YouTubeOAuthCode.RateLimited : error.Reason == YouTubeFailure.NetworkInterrupted ? YouTubeOAuthCode.NetworkFailure : YouTubeOAuthCode.InvalidTokenResponse)); Set(error.Reason == YouTubeFailure.QuotaExceeded ? YouTubeAuthState.RateLimited : error.Reason == YouTubeFailure.NetworkInterrupted ? YouTubeAuthState.NetworkError : YouTubeAuthState.AuthenticationError); throw; }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException)
        { Failure(FailureAt(error is OperationCanceledException ? YouTubeOAuthCode.TimedOut : YouTubeOAuthCode.NetworkFailure)); Set(YouTubeAuthState.NetworkError); throw new YouTubeApiException(YouTubeFailure.NetworkInterrupted); }
        catch (Exception error)
        {
            Failure((error as YouTubeApiException)?.OAuth ?? FailureAt(YouTubeOAuthCode.UnexpectedFailure));
            if (!DeleteSaved()) throw new YouTubeApiException(YouTubeFailure.CredentialStorageFailure);
            Set(YouTubeAuthState.Expired); throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired);
        }
        finally { gate.Release(); }
    }
    internal async Task UnlinkAsync()
    {
        CancelAuthorization(); await InitializeAsync().ConfigureAwait(false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        await gate.WaitAsync(deadline.Token).ConfigureAwait(false);
        try
        {
            bool remoteUnknown = settings == null || State is YouTubeAuthState.StorageError or YouTubeAuthState.UnlinkedRemotePending;
            string revoke = tokens?.RefreshToken;
            if (!DeleteSaved()) return;
            Set(YouTubeAuthState.Unlinked);
            if (string.IsNullOrEmpty(revoke)) { if (remoteUnknown) Set(YouTubeAuthState.UnlinkedRemotePending); return; }
            try { await exchange.RevokeAsync(revoke, deadline.Token).ConfigureAwait(false); }
            catch { Set(YouTubeAuthState.UnlinkedRemotePending); }
        }
        finally { gate.Release(); }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel(); CancelAuthorization(); lock (stateGate) tokens = null; exchange.Dispose();
    }
    public override string ToString() => "YouTubeAuthService[" + State + "]";
}
