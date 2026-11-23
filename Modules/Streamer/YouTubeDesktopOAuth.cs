using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TownOfHost;

// PKCE/token primitives. Explicit browser flow and DPAPI persistence are separate
// modules; construction never authenticates. See docs/development/youtube-oauth-user-guide.md.
internal sealed class YouTubePkceSession
{
    internal const string Scope = "https://www.googleapis.com/auth/youtube.readonly";
    internal string ClientId { get; }
    internal Uri Redirect { get; }
    private readonly string verifier = Base64(RandomNumberGenerator.GetBytes(32));
    private readonly string state = Base64(RandomNumberGenerator.GetBytes(32));
    private readonly DateTimeOffset expires = DateTimeOffset.UtcNow.AddMinutes(10);
    private int consumed;
    internal YouTubePkceSession(string clientId, Uri redirect)
    {
        if (string.IsNullOrWhiteSpace(clientId) || clientId.Length > 1024 || clientId.Any(char.IsControl)) throw new ArgumentException("Invalid OAuth client configuration");
        if (redirect == null || redirect.Scheme != "http" || redirect.Host != "127.0.0.1" || redirect.Port <= 0 || redirect.IsDefaultPort || redirect.UserInfo.Length != 0 || redirect.Query.Length != 0 || redirect.Fragment.Length != 0) throw new ArgumentException("OAuth redirect must use a dedicated loopback port");
        ClientId = clientId; Redirect = redirect;
    }
    internal Uri AuthorizationUri
    {
        get
        {
            if (expires <= DateTimeOffset.UtcNow || Volatile.Read(ref consumed) != 0) throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired);
            var challenge = Base64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            return new Uri("https://accounts.google.com/o/oauth2/v2/auth?" + string.Join("&", new Dictionary<string, string> {
                ["client_id"] = ClientId, ["redirect_uri"] = Redirect.AbsoluteUri, ["response_type"] = "code", ["scope"] = Scope,
                ["code_challenge_method"] = "S256", ["code_challenge"] = challenge, ["state"] = state, ["access_type"] = "offline", ["prompt"] = "consent select_account"
            }.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value))));
        }
    }
    internal Dictionary<string, string> Complete(Uri callback)
    {
        YouTubeApiException Invalid(YouTubeOAuthCode code) => new(YouTubeFailure.AuthenticationExpired, new(YouTubeOAuthStage.CallbackValidation, code));
        if (callback == null || callback.GetLeftPart(UriPartial.Path) != Redirect.GetLeftPart(UriPartial.Path) || callback.Fragment.Length != 0 || callback.UserInfo.Length != 0) throw Invalid(YouTubeOAuthCode.InvalidCallback);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        // OAuth callback parameters use form encoding: raw + means space; %2B stays +.
        foreach (string part in callback.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2); var key = Uri.UnescapeDataString(pair[0].Replace("+", " "));
            if (!values.TryAdd(key, pair.Length == 2 ? Uri.UnescapeDataString(pair[1].Replace("+", " ")) : "")) throw Invalid(YouTubeOAuthCode.InvalidCallback);
        }
        if (expires <= DateTimeOffset.UtcNow) throw Invalid(YouTubeOAuthCode.TimedOut);
        if (!values.TryGetValue("state", out string actual) || actual.Length != state.Length || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(state))) throw Invalid(YouTubeOAuthCode.StateMismatch);
        if (Interlocked.Exchange(ref consumed, 1) != 0) throw Invalid(YouTubeOAuthCode.InvalidCallback);
        if (values.TryGetValue("error", out var error)) throw Invalid(YouTubeOAuthDiagnostic.GoogleCode(error));
        if (!values.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code) || code.Length > 8192) throw Invalid(YouTubeOAuthCode.InvalidCallback);
        return new() { ["client_id"] = ClientId, ["redirect_uri"] = Redirect.AbsoluteUri, ["grant_type"] = "authorization_code", ["code"] = code, ["code_verifier"] = verifier };
    }
    private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public override string ToString() => "YouTubePkceSession[redacted]";
}
internal sealed class YouTubeOAuthTokens
{
    internal YouTubeCredential Access { get; }
    internal string RefreshToken { get; }
    internal YouTubeOAuthTokens(YouTubeCredential access, string refresh) { Access = access; RefreshToken = refresh; }
    public override string ToString() => "YouTubeOAuthTokens[redacted]";
}
internal sealed class YouTubeDesktopOAuth : IYouTubeTokenExchange
{
    private readonly HttpClient client;
    private readonly Action<YouTubeOAuthDiagnostic> diagnostic;
    internal YouTubeDesktopOAuth(HttpClient http = null, Action<YouTubeOAuthDiagnostic> progress = null)
    { client = http ?? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(15) }; diagnostic = progress; }
    private void Trace(YouTubeOAuthDiagnostic value) { try { diagnostic?.Invoke(value); } catch { /* Diagnostics must never affect authentication. */ } }
    // Invoke only after a caller deliberately configures a Desktop client and obtains consent.
    public async Task<YouTubeOAuthTokens> ExchangeAsync(Dictionary<string, string> form, CancellationToken token, string previousRefresh = null)
    {
        if (form == null || !form.TryGetValue("client_id", out var id) || string.IsNullOrWhiteSpace(id)
            || !form.TryGetValue("grant_type", out var grant) || grant is not ("authorization_code" or "refresh_token")) throw new YouTubeApiException(YouTubeFailure.MissingCredentials);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        Trace(new(YouTubeOAuthStage.TokenExchange));
        int http = 0;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token") { Content = new FormUrlEncodedContent(form) };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            http = (int)response.StatusCode; Trace(new(YouTubeOAuthStage.TokenExchange, HttpStatus: http));
            var json = await OfficialYouTubeChatSource.ReadJson(response, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new YouTubeApiException(http == 429 ? YouTubeFailure.QuotaExceeded : http >= 500 ? YouTubeFailure.NetworkInterrupted : YouTubeFailure.AuthenticationExpired, YouTubeOAuthDiagnostic.FromGoogle(json, http));
            Trace(new(YouTubeOAuthStage.TokenValidation, HttpStatus: http));
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 }); var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new YouTubeApiException(YouTubeFailure.InvalidResponse, new(YouTubeOAuthStage.TokenValidation, YouTubeOAuthCode.InvalidTokenResponse, http));
            string access = OfficialYouTubeJson.Text(root, "access_token", 8192), refresh = OfficialYouTubeJson.Text(root, "refresh_token", 8192);
            string scope = OfficialYouTubeJson.Text(root, "scope", 2048);
            // A refresh response may omit scope, retaining the originally verified readonly grant.
            if ((scope.Length == 0 && grant != "refresh_token") || (scope.Length != 0 && scope != YouTubePkceSession.Scope))
                throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired, new(YouTubeOAuthStage.TokenValidation, YouTubeOAuthCode.ScopeMismatch, http));
            if (OfficialYouTubeJson.Text(root, "token_type", 32) != "Bearer" || !root.TryGetProperty("expires_in", out var seconds) || !seconds.TryGetInt32(out int duration) || duration < 1 || duration > 86400)
                throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired, new(YouTubeOAuthStage.TokenValidation, YouTubeOAuthCode.InvalidTokenResponse, http));
            var credential = new YouTubeCredential(access, false, DateTimeOffset.UtcNow.AddSeconds(duration));
            if (!credential.Valid) throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired, new(YouTubeOAuthStage.TokenValidation, YouTubeOAuthCode.InvalidTokenResponse, http));
            return new(credential, refresh.Length > 0 ? refresh : previousRefresh);
        }
        catch (JsonException) { throw new YouTubeApiException(YouTubeFailure.InvalidResponse, new(YouTubeOAuthStage.TokenValidation, YouTubeOAuthCode.InvalidTokenResponse, http)); }
        catch (YouTubeApiException error) when (error.OAuth == null)
        { throw new YouTubeApiException(error.Reason, new(YouTubeOAuthStage.TokenExchange, error.Reason == YouTubeFailure.NetworkInterrupted ? YouTubeOAuthCode.NetworkFailure : YouTubeOAuthCode.InvalidTokenResponse, http)); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new YouTubeApiException(YouTubeFailure.NetworkInterrupted, new(YouTubeOAuthStage.TokenExchange, YouTubeOAuthCode.TimedOut, http)); }
        catch (Exception error) when (error is HttpRequestException or IOException)
        { throw new YouTubeApiException(YouTubeFailure.NetworkInterrupted, new(YouTubeOAuthStage.TokenExchange, YouTubeOAuthCode.NetworkFailure, http)); }
    }
    public async Task RevokeAsync(string token, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 8192 || YouTubeProtectedTokenStore.HasControl(token)) throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromSeconds(12));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/revoke") { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token }) };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired);
    }
    public void Dispose() => client.Dispose();
}
// In-memory provider retained for isolated callers/tests. Production uses
// YouTubeAuthService with explicit authorization and DPAPI-backed persistence.
internal sealed class YouTubeOAuthMemoryProvider : IYouTubeCredentialProvider, IDisposable
{
    private readonly string clientId, clientSecret;
    private readonly YouTubeDesktopOAuth oauth;
    private readonly SemaphoreSlim gate = new(1);
    private YouTubeOAuthTokens tokens;
    internal YouTubeOAuthMemoryProvider(string id, YouTubeOAuthTokens initial, YouTubeDesktopOAuth exchange, string desktopClientSecret = null)
    { clientId = id; tokens = initial; oauth = exchange; clientSecret = desktopClientSecret; }
    public async Task<YouTubeCredential> GetAsync(CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (tokens?.Access?.Valid == true && tokens.Access.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1)) return tokens.Access;
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(tokens?.RefreshToken) || oauth == null) throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired);
            var form = new Dictionary<string, string> { ["client_id"] = clientId, ["grant_type"] = "refresh_token", ["refresh_token"] = tokens.RefreshToken };
            if (!string.IsNullOrWhiteSpace(clientSecret)) form["client_secret"] = clientSecret;
            try { tokens = await oauth.ExchangeAsync(form, token, tokens.RefreshToken).ConfigureAwait(false); }
            catch { tokens = null; throw; } // Revocation never falls back to a key or another source.
            return tokens.Access;
        }
        finally { gate.Release(); }
    }
    public void Dispose() { tokens = null; oauth?.Dispose(); }
}
