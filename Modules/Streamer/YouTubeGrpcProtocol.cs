using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TownOfHost;

// Narrow, bounded protobuf codec for Google's published stream_list.proto.
// Field numbers: https://developers.google.com/youtube/data/live/streaming-live-chat
// HTTP/2/gRPC framing: https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md
// No dynamic code generation, native gRPC library, Unity access or private endpoints.
internal static class YouTubeGrpcProtocol
{
    internal const int MaxFrameBytes = 2 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static byte[] Request(string chat, string page)
    {
        using var b = new MemoryStream();
        String(b, 1, chat); String(b, 2, "ja");
        if (!string.IsNullOrEmpty(page)) String(b, 99, page);
        String(b, 100, "id"); String(b, 100, "snippet"); String(b, 100, "authorDetails");
        return b.ToArray();
    }
    internal static void Varint(Stream stream, ulong number)
    { while (number >= 128) { stream.WriteByte((byte)(number | 128)); number >>= 7; } stream.WriteByte((byte)number); }
    internal static void String(Stream stream, int field, string text) => Bytes(stream, field, Utf8.GetBytes(text ?? ""));
    internal static void Bytes(Stream stream, int field, byte[] data)
    { Varint(stream, (ulong)(field << 3 | 2)); Varint(stream, (ulong)data.Length); stream.Write(data, 0, data.Length); }
    internal static byte[] Frame(byte[] payload)
    {
        if (payload.Length > MaxFrameBytes) throw Invalid();
        var frame = new byte[payload.Length + 5]; int n = payload.Length;
        frame[1] = (byte)(n >> 24); frame[2] = (byte)(n >> 16); frame[3] = (byte)(n >> 8); frame[4] = (byte)n;
        Buffer.BlockCopy(payload, 0, frame, 5, n); return frame;
    }
    internal static async Task<byte[]> ReadFrame(Stream input, CancellationToken token)
    {
        var header = new byte[5]; int n = await input.ReadAsync(header.AsMemory(0, 1), token).AsTask().WaitAsync(token).ConfigureAwait(false);
        if (n == 0) return null;
        await Exact(input, header.AsMemory(1), token).ConfigureAwait(false);
        if (header[0] != 0) throw new YouTubeApiException(YouTubeFailure.TransportUnsupported);
        uint size = (uint)header[1] << 24 | (uint)header[2] << 16 | (uint)header[3] << 8 | header[4];
        if (size > MaxFrameBytes) throw Invalid();
        var bytes = new byte[(int)size]; await Exact(input, bytes, token).ConfigureAwait(false); return bytes;
    }
    private static async Task Exact(Stream input, Memory<byte> bytes, CancellationToken token)
    {
        while (bytes.Length > 0) { int n = await input.ReadAsync(bytes, token).AsTask().WaitAsync(token).ConfigureAwait(false); if (n == 0) throw Invalid(); bytes = bytes[n..]; }
    }
    internal static YouTubeChatBatch Response(byte[] data)
    {
        if (data.Length > MaxFrameBytes) throw Invalid();
        var messages = new List<LiveStreamComment>(); string next = ""; bool ended = false;
        foreach (var f in Fields(data))
        {
            if (f.Number == 2) { ended = Text(f, 128).Length > 0; }
            else if (f.Number == 100602) next = Text(f, 32768);
            else if (f.Number == 1007)
            {
                if (f.Wire != 2) throw Invalid();
                if (messages.Count >= 2000) throw Invalid();
                var item = Message(f.Data, out bool itemEnded); ended |= itemEnded;
                if (item.HasValue) messages.Add(item.Value);
            }
        }
        return new(messages, next, ended);
    }
    private static LiveStreamComment? Message(ReadOnlyMemory<byte> bytes, out bool ended)
    {
        string id = "", channel = "", name = "", text = "", timestamp = ""; int type = 0; ended = false;
        foreach (var f in Fields(bytes))
        {
            if (f.Number == 101) id = Text(f, 256);
            else if (f.Number == 3)
            {
                if (f.Wire != 2) throw Invalid();
                foreach (var a in Fields(f.Data))
                { if (a.Number == 10101) channel = Text(a, 128); else if (a.Number == 103) name = Text(a, 1024); }
            }
            else if (f.Number == 2)
            {
                if (f.Wire != 2) throw Invalid();
                foreach (var s in Fields(f.Data))
                {
                    if (s.Number == 1) { if (s.Wire != 0 || s.Value > int.MaxValue) throw Invalid(); type = (int)s.Value; }
                    else if (s.Number == 301 && channel.Length == 0) channel = Text(s, 128);
                    else if (s.Number == 4) timestamp = Text(s, 128);
                    else if (s.Number == 16) text = Text(s, 16384);
                    else if (s.Number == 19) { if (s.Wire != 2) throw Invalid(); foreach (var t in Fields(s.Data)) if (t.Number == 1) text = Text(t, 16384); }
                }
            }
        }
        ended = type == 4;
        // Silent events and unknown/system-only variants never become participation commands.
        if (type != 1 || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(text)) return null;
        DateTimeOffset? published = DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at.ToUniversalTime() : null;
        return new LiveStreamComment(id, channel, StreamSafety.Plain(name, 80), StreamSafety.Plain(text, 2048), published);
    }
    private readonly record struct Field(int Number, int Wire, ulong Value, ReadOnlyMemory<byte> Data);
    private static IEnumerable<Field> Fields(ReadOnlyMemory<byte> bytes)
    {
        int at = 0, count = 0;
        while (at < bytes.Length)
        {
            if (++count > 20000) throw Invalid();
            ulong tag = Read(bytes, ref at); int wire = (int)(tag & 7); ulong number = tag >> 3;
            if (number == 0 || number > 536870911) throw Invalid();
            ulong value = 0; ReadOnlyMemory<byte> block = default;
            if (wire == 0) value = Read(bytes, ref at);
            else if (wire is 1 or 5) { int n = wire == 1 ? 8 : 4; if (n > bytes.Length - at) throw Invalid(); at += n; }
            else if (wire == 2) { ulong n = Read(bytes, ref at); if (n > (ulong)(bytes.Length - at)) throw Invalid(); block = bytes.Slice(at, (int)n); at += (int)n; }
            else throw Invalid();
            yield return new Field((int)number, wire, value, block);
        }
    }
    private static ulong Read(ReadOnlyMemory<byte> data, ref int at)
    {
        ulong value = 0;
        for (int shift = 0; shift <= 63; shift += 7)
        {
            if (at >= data.Length) throw Invalid(); byte b = data.Span[at++];
            if (shift == 63 && b > 1) throw Invalid();
            value |= (ulong)(b & 127) << shift; if (b < 128) return value;
        }
        throw Invalid();
    }
    private static string Text(Field field, int max)
    {
        if (field.Wire != 2 || field.Data.Length > max * 4) throw Invalid();
        try { string s = Utf8.GetString(field.Data.Span); if (s.Length > max) throw Invalid(); return s; }
        catch (DecoderFallbackException) { throw Invalid(); }
    }
    private static YouTubeApiException Invalid() => new(YouTubeFailure.InvalidResponse);
}