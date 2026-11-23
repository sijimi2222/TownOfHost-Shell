using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;

namespace TownOfHost;

// No strings or exceptions are retained: the diagnostic schema cannot carry
// credentials, URLs, IDs, response bodies or chat text.
internal enum YouTubeChatStage { Unknown, Credential, VideosRequest, VideosResponse, ActiveLiveChatId, StreamRequest, StreamHeaders, GrpcStatus, Receive, Parse, Retry, Stop }
internal enum YouTubeChatCode { Unknown, Started, Completed, HeadersReceived, Http2Established, HttpRejected, UnsupportedTransport, GrpcOk, GrpcRejected, MissingGrpcStatus, InvalidPayload, ApiFailure, NetworkFailure, Timeout, Cancelled, Renewal, EndOfStream, RetryScheduled, RetryExhausted, TerminalFailure, IdleBackoff }
internal enum YouTubeChatExceptionKind { None, HttpRequest, HttpProtocol, Socket, TlsAuthentication, IO, Cancellation, Json, Api, NotSupported, InvalidOperation, Unexpected }
internal sealed record YouTubeChatDiagnostic(YouTubeChatStage Stage, YouTubeChatCode Code,
    int HttpStatus = 0, int GrpcStatus = -1, int HttpMajor = 0,
    YouTubeChatExceptionKind ExceptionKind = YouTubeChatExceptionKind.None,
    YouTubeChatExceptionKind Cause = YouTubeChatExceptionKind.None,
    long ElapsedMs = 0, long TotalMs = 0, int Attempt = 0, int RetryDelaySeconds = 0, YouTubeFailure? Failure = null, SocketError? SocketStatus = null)
{
    private static string Fixed<T>(T value) where T : struct, Enum => Enum.IsDefined(typeof(T), value) ? value.ToString() : "Unknown";
    private static long Time(long value) => Math.Clamp(value, 0, 86400000);
    internal string SafeLog => "YOUTUBE_CHAT stage=" + Fixed(Stage) + " code=" + Fixed(Code)
        + " http=" + (HttpStatus is >= 100 and <= 599 ? HttpStatus : 0)
        + " grpc=" + (GrpcStatus is >= 0 and <= 16 ? GrpcStatus.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unknown")
        + " httpMajor=" + (HttpMajor is >= 1 and <= 3 ? HttpMajor : 0)
        + " exception=" + Fixed(ExceptionKind) + " cause=" + Fixed(Cause)
        + " elapsedMs=" + Time(ElapsedMs) + " totalMs=" + Time(TotalMs)
        + " attempt=" + Math.Clamp(Attempt, 0, 4) + " retryDelaySec=" + Math.Clamp(RetryDelaySeconds, 0, 3600)
        + " reason=" + (Failure.HasValue ? Fixed(Failure.Value) : "None")
        + " socket=" + (SocketStatus.HasValue ? Fixed(SocketStatus.Value) : "None");
    internal static YouTubeChatExceptionKind Kind(Exception error) => error switch
    {
        null => YouTubeChatExceptionKind.None,
        YouTubeApiException => YouTubeChatExceptionKind.Api,
        OperationCanceledException => YouTubeChatExceptionKind.Cancellation,
        AuthenticationException => YouTubeChatExceptionKind.TlsAuthentication,
        SocketException => YouTubeChatExceptionKind.Socket,
        HttpRequestException => YouTubeChatExceptionKind.HttpRequest,
        JsonException => YouTubeChatExceptionKind.Json,
        NotSupportedException => YouTubeChatExceptionKind.NotSupported,
        InvalidOperationException => YouTubeChatExceptionKind.InvalidOperation,
        // HttpProtocolException is not a public .NET 6 contract on every runtime.
        _ when error.GetType().Name == "HttpProtocolException" => YouTubeChatExceptionKind.HttpProtocol,
        IOException => YouTubeChatExceptionKind.IO,
        _ => YouTubeChatExceptionKind.Unexpected
    };
    internal static YouTubeChatExceptionKind InnerKind(Exception error)
    {
        var result = YouTubeChatExceptionKind.None;
        for (int i = 0; i < 4 && error?.InnerException != null; i++) { error = error.InnerException; result = Kind(error); }
        return result;
    }
    internal static SocketError? SocketCode(Exception error)
    {
        for (int i = 0; i < 5 && error != null; i++, error = error.InnerException)
            if (error is SocketException socket) return socket.SocketErrorCode;
        return null;
    }
    internal static YouTubeFailure Reason(Exception error) => error is YouTubeApiException api ? api.Reason
        : error is HttpRequestException or IOException or OperationCanceledException ? YouTubeFailure.NetworkInterrupted : YouTubeFailure.InvalidResponse;
    public override string ToString() => SafeLog;
}
internal sealed class YouTubeChatTrace
{
    private readonly Action<YouTubeChatDiagnostic> sink;
    private readonly long started = Stopwatch.GetTimestamp();
    private long stageStarted;
    internal YouTubeChatStage Stage { get; private set; }
    internal int HttpStatus, HttpMajor, GrpcStatus = -1;
    internal bool FailureLogged { get; private set; }
    internal YouTubeChatTrace(Action<YouTubeChatDiagnostic> progress) { sink = progress ?? YouTubeOfficialRuntime.Trace; stageStarted = started; }
    internal void At(YouTubeChatStage stage) { Stage = stage; stageStarted = Stopwatch.GetTimestamp(); }
    internal void Emit(YouTubeChatCode code)
    { Write(new(Stage, code, HttpStatus, GrpcStatus, HttpMajor, ElapsedMs: Elapsed(stageStarted), TotalMs: Elapsed(started))); }
    internal void Fail(Exception error, YouTubeChatCode? code = null)
    {
        if (FailureLogged) return;
        FailureLogged = true;
        if (error is HttpRequestException http && http.StatusCode.HasValue) HttpStatus = (int)http.StatusCode.Value;
        code ??= error switch {
            OperationCanceledException => YouTubeChatCode.Cancelled,
            HttpRequestException or IOException => YouTubeChatCode.NetworkFailure,
            NotSupportedException => YouTubeChatCode.UnsupportedTransport,
            YouTubeApiException api when api.OAuth?.Code == YouTubeOAuthCode.TimedOut => YouTubeChatCode.Timeout,
            YouTubeApiException api when api.Reason == YouTubeFailure.InvalidResponse => YouTubeChatCode.InvalidPayload,
            YouTubeApiException api when api.Reason == YouTubeFailure.TransportUnsupported => YouTubeChatCode.UnsupportedTransport,
            _ => YouTubeChatCode.ApiFailure
        };
        Write(new(Stage, code.Value, HttpStatus, GrpcStatus, HttpMajor, YouTubeChatDiagnostic.Kind(error), YouTubeChatDiagnostic.InnerKind(error), Elapsed(stageStarted), Elapsed(started), Failure: YouTubeChatDiagnostic.Reason(error), SocketStatus: YouTubeChatDiagnostic.SocketCode(error)));
    }
    private void Write(YouTubeChatDiagnostic value) { try { sink?.Invoke(value); } catch { /* Diagnostics never affect transport or cancellation. */ } }
    internal static long Elapsed(long timestamp) => Math.Max(0, (Stopwatch.GetTimestamp() - timestamp) * 1000 / Stopwatch.Frequency);
}
