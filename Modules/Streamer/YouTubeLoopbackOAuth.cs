using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TownOfHost;

// Opened only by an explicit Google-link action, never by startup/restoration.
internal sealed class YouTubeLoopbackOAuth : IYouTubeAuthorizationFlow
{
    private readonly IYouTubeTokenExchange exchange;
    private readonly Action<Uri> openBrowser;
    private readonly Action<YouTubeOAuthDiagnostic> diagnostic;
    internal YouTubeLoopbackOAuth(IYouTubeTokenExchange tokenExchange, Action<Uri> browser, Action<YouTubeOAuthDiagnostic> progress = null)
    { exchange = tokenExchange; openBrowser = browser; diagnostic = progress; }
    private void Trace(YouTubeOAuthDiagnostic value) { try { diagnostic?.Invoke(value); } catch { } }
    public async Task<YouTubeOAuthTokens> AuthorizeAsync(YouTubeOAuthSettings settings, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromMinutes(10));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.ExclusiveAddressUse = true;
        listener.Start(4);
        using var stop = deadline.Token.Register(() => listener.Stop());
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var redirect = new Uri("http://127.0.0.1:" + port + "/oauth2/callback"); var session = new YouTubePkceSession(settings.ClientId, redirect);
            // Caller provides a system-browser launcher; no embedded web view or password input.
            Trace(new(YouTubeOAuthStage.BrowserOpen));
            openBrowser(session.AuthorizationUri);
            Trace(new(YouTubeOAuthStage.WaitingForRedirect));
            for (int requests = 0; requests < 16; requests++)
            {
                using var connection = await listener.AcceptTcpClientAsync().WaitAsync(deadline.Token).ConfigureAwait(false);
                if (connection.Client.RemoteEndPoint is not IPEndPoint peer || !IPAddress.IsLoopback(peer.Address)) continue;
                using var requestTime = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token); requestTime.CancelAfter(TimeSpan.FromSeconds(3));
                using var stream = connection.GetStream();
                string target;
                try { target = await ReadRequest(stream, port, requestTime.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!deadline.IsCancellationRequested) { continue; }
                catch { await Reply(stream, false, requestTime.Token).ConfigureAwait(false); continue; }
                if (target == null) { await Reply(stream, false, requestTime.Token).ConfigureAwait(false); continue; }
                Trace(new(YouTubeOAuthStage.CallbackValidation));
                System.Collections.Generic.Dictionary<string, string> form;
                try { form = session.Complete(new Uri("http://127.0.0.1:" + port + target)); }
                catch (YouTubeApiException) { await Reply(stream, false, requestTime.Token).ConfigureAwait(false); throw; }
                settings.ApplyClientSecret(form);
                await Reply(stream, true, requestTime.Token).ConfigureAwait(false);
                // The code/state/verifier never appear in a response, URL log or exception message.
                Trace(new(YouTubeOAuthStage.TokenExchange));
                return await exchange.ExchangeAsync(form, deadline.Token).ConfigureAwait(false);
            }
            throw new YouTubeApiException(YouTubeFailure.AuthenticationExpired, new(YouTubeOAuthStage.CallbackValidation, YouTubeOAuthCode.InvalidCallback));
        }
        catch (Exception e) when (deadline.IsCancellationRequested && e is SocketException or ObjectDisposedException or OperationCanceledException) { throw new OperationCanceledException(deadline.Token); }
        finally { listener.Stop(); }
    }
    private static async Task<string> ReadRequest(Stream stream, int port, CancellationToken token)
    {
        var bytes = new byte[8192]; int size = 0;
        while (size < bytes.Length)
        {
            int n = await stream.ReadAsync(bytes.AsMemory(size, 1), token).AsTask().WaitAsync(token).ConfigureAwait(false); if (n == 0) return null; size += n;
            if (size >= 4 && bytes[size - 4] == 13 && bytes[size - 3] == 10 && bytes[size - 2] == 13 && bytes[size - 1] == 10) break;
        }
        if (size == bytes.Length) return null;
        for (int i = 0; i < size; i++) if (bytes[i] > 127 || bytes[i] == 0) return null;
        string[] lines = Encoding.ASCII.GetString(bytes, 0, size).Split("\r\n", StringSplitOptions.None);
        string[] first = lines[0].Split(' ');
        if (first.Length != 3 || first[0] != "GET" || first[2] != "HTTP/1.1" || !first[1].StartsWith("/oauth2/callback?", StringComparison.Ordinal) || first[1].Length > 7000 || first[1].Contains('#')) return null;
        string host = null;
        for (int i = 1; i < lines.Length && lines[i].Length != 0; i++)
        {
            int split = lines[i].IndexOf(':'); if (split <= 0) return null;
            string name = lines[i][..split], value = lines[i][(split + 1)..].Trim();
            if (name.Equals("Host", StringComparison.OrdinalIgnoreCase)) { if (host != null) return null; host = value; }
            if (name.Equals("Origin", StringComparison.OrdinalIgnoreCase) || name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) return null;
            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) && value != "0") return null;
        }
        if (host != "127.0.0.1:" + port) return null;
        return first[1];
    }
    private static async Task Reply(Stream stream, bool accepted, CancellationToken token)
    {
        // This confirms receipt only; token exchange/save may still fail in the MOD.
        string message = accepted ? "認証応答を受信しました。MODのGoogle連携画面で結果を確認してください。" : "この認証応答は受け付けられません。MODから連携をやり直してください。";
        byte[] body = Encoding.UTF8.GetBytes(message);
        byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + (accepted ? "200 OK" : "400 Bad Request") + "\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'none'\r\nReferrer-Policy: no-referrer\r\nConnection: close\r\nContent-Length: " + body.Length + "\r\n\r\n");
        await stream.WriteAsync(header, token).ConfigureAwait(false); await stream.WriteAsync(body, token).ConfigureAwait(false);
    }
}
