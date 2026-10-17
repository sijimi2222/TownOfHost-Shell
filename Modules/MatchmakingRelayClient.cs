using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TownOfHost.Modules;

// Anonymous recruitment capabilities only. Never persisted, sent through game RPC, or logged.
internal sealed class MatchmakingRelayClient
{
    internal enum Outcome { Success, Retry, Ended, Rejected }
    internal sealed class Result
    {
        internal Outcome Status { get; }
        internal int RetrySeconds { get; }
        internal Result(Outcome status, int retrySeconds = 6) { Status = status; RetrySeconds = retrySeconds; }
    }
    private readonly HttpClient client;
    private readonly Uri origin;
    private readonly Func<long> clock;
    private readonly string requestId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private readonly SemaphoreSlim gate = new(1, 1);
    private string sessionId = "";
    private string token = "";
    private long sequence;
    private long leaseUntil;
    private string pendingBody;
    private string pendingLobby;
    internal string LastAppliedLobby { get; private set; } = "";
    private bool ended;
    internal bool HasSession => token.Length != 0;
    internal MatchmakingRelayClient(HttpClient client, string baseUrl, Func<long> clock = null)
    {
        this.client = client;
        this.clock = clock ?? (() => Environment.TickCount64);
        origin = new Uri(baseUrl, UriKind.Absolute);
        if (origin.Scheme != Uri.UriSchemeHttps || origin.UserInfo.Length != 0 || origin.Query.Length != 0 ||
            origin.Fragment.Length != 0 || origin.AbsolutePath != "/") throw new ArgumentException("Invalid relay endpoint.");
    }
    internal async Task<Result> SendAsync(string lobbyJson, bool end)
    {
        if (!await gate.WaitAsync(0).ConfigureAwait(false)) return new Result(Outcome.Retry);
        try
        {
            if (ended) return new Result(Outcome.Ended);
            if (token.Length == 0)
            {
                if (end) { ended = true; return new Result(Outcome.Ended); }
                var start = await Post("start", JsonSerializer.Serialize(new { request_id = requestId }), false).ConfigureAwait(false);
                if (start.Status != Outcome.Success) return new Result(start.Status, start.RetrySeconds);
                using var doc = JsonDocument.Parse(start.Body);
                var root = doc.RootElement;
                var id = root.GetProperty("session_id").GetString();
                var capability = root.GetProperty("session_token").GetString();
                var ttl = root.GetProperty("expires_in").GetInt32();
                if (!IsHex(id, 32) || !IsHex(capability, 64) || ttl < 1 || ttl > 600 ||
                    root.GetProperty("heartbeat_interval").GetInt32() != 120) return Reject();
                sessionId = id; token = capability;
                leaseUntil = clock() + ttl * 1000L;
            }
            if (clock() >= leaseUntil) return Reject(Outcome.Ended);
            // Resolve a potentially committed token rotation before ending the session.
            if (end && pendingBody != null)
            {
                var outstanding = await Post("update", pendingBody, true).ConfigureAwait(false);
                if (outstanding.Status == Outcome.Retry) return new Result(Outcome.Retry, outstanding.RetrySeconds);
                if (outstanding.Status != Outcome.Success) return Reject(outstanding.Status);
                var applied = ApplyUpdate(outstanding.Body);
                if (applied.Status != Outcome.Success) return applied;
            }
            string body;
            if (end)
            {
                // A confirmed update has a rotated capability. End uses the next sequence.
                sequence++; pendingBody = null;
                body = JsonSerializer.Serialize(new { session_id = sessionId, sequence });
            }
            else
            {
                if (pendingBody == null)
                {
                    pendingLobby = lobbyJson;
                    using var lobby = JsonDocument.Parse(lobbyJson);
                    sequence++;
                    pendingBody = JsonSerializer.Serialize(new { session_id = sessionId, sequence, lobby = lobby.RootElement });
                }
                body = pendingBody;
            }
            var reply = await Post(end ? "end" : "update", body, true).ConfigureAwait(false);
            if (reply.Status == Outcome.Ended || reply.Status == Outcome.Rejected) return Reject(reply.Status);
            if (reply.Status != Outcome.Success) return new Result(reply.Status, reply.RetrySeconds);
            using var response = JsonDocument.Parse(reply.Body);
            var result = response.RootElement;
            if (!result.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True) return Reject();
            if (end)
            {
                if (!result.TryGetProperty("ended", out var closed) || closed.ValueKind != JsonValueKind.True) return Reject();
                return Reject(Outcome.Ended);
            }
            return ApplyUpdate(reply.Body);
        }
        catch (HttpRequestException) { return new Result(Outcome.Retry, 60); }
        catch (OperationCanceledException) { return new Result(Outcome.Retry, 60); }
        catch (IOException) { return new Result(Outcome.Retry, 60); }
        catch { return Reject(); } // Never expose HTTP, JSON or exception contents.
        finally { gate.Release(); }
    }
    private Result ApplyUpdate(string body)
    {
        using var response = JsonDocument.Parse(body);
        var result = response.RootElement;
        var renewedToken = result.GetProperty("session_token").GetString();
        var confirmed = result.GetProperty("sequence").GetInt64();
        var seconds = result.GetProperty("expires_in").GetInt32();
        if (!result.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True ||
            !IsHex(renewedToken, 64) || confirmed != sequence || seconds < 1 || seconds > 600) return Reject();
        token = renewedToken;
        leaseUntil = clock() + seconds * 1000L;
        LastAppliedLobby = pendingLobby;
        pendingBody = null;
        return new Result(Outcome.Success);
    }
    private Result Reject(Outcome outcome = Outcome.Rejected)
    {
        token = ""; sessionId = ""; pendingBody = null; ended = true;
        return new Result(outcome);
    }
    private static bool IsHex(string value, int length)
    {
        if (value == null || value.Length != length) return false;
        foreach (var c in value) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
        return true;
    }
    private sealed class Reply
    {
        internal Outcome Status;
        internal string Body = "";
        internal int RetrySeconds = 6;
    }
    private async Task<Reply> Post(string action, string body, bool authenticated)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(origin, "v2/sessions/" + action));
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        if (authenticated) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        var status = (int)response.StatusCode;
        if (status == 429 || status >= 500)
        {
            var retry = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 60;
            return new Reply { Status = Outcome.Retry, RetrySeconds = (int)Math.Clamp(retry, 6, 86400) };
        }
        if (status == 410) return new Reply { Status = Outcome.Ended };
        if (!response.IsSuccessStatusCode) return new Reply { Status = Outcome.Rejected };
        if (response.Content.Headers.ContentLength > 8192 ||
            response.Content.Headers.ContentType?.MediaType != "application/json") return new Reply { Status = Outcome.Rejected };
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var content = new MemoryStream();
        var bytes = new byte[1024];
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var count = await stream.ReadAsync(bytes.AsMemory(0, bytes.Length), deadline.Token).ConfigureAwait(false);
            if (count == 0) break;
            if (content.Length + count > 8192) return new Reply { Status = Outcome.Rejected };
            content.Write(bytes, 0, count);
        }
        return new Reply { Status = Outcome.Success, Body = Encoding.UTF8.GetString(content.ToArray()) };
    }
}
