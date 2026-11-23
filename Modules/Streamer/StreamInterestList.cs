using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TownOfHost;

// Main-thread owned, display-only state. No game-player identity or admission decisions.
internal sealed class StreamInterestList
{
    internal const int Capacity = 200;
    internal const int PageSize = 30;
    private readonly Dictionary<string, (string Name, long Order)> entries = new(StringComparer.Ordinal);
    private long nextOrder;
    private int generation;
    private long receptionStartedTicks;
    internal int Generation => Volatile.Read(ref generation);
    internal int Version { get; private set; }
    internal bool Accepting { get; private set; }
    internal bool Overflowed { get; private set; }
    internal int Count => entries.Count;

    internal void Start()
    {
        if (Accepting) return;
        receptionStartedTicks = DateTimeOffset.UtcNow.UtcTicks;
        Accepting = true; Interlocked.Increment(ref generation); Version++;
    }
    internal void Stop()
    {
        if (!Accepting) return;
        Accepting = false; Interlocked.Increment(ref generation); Version++;
    }
    internal void Reset()
    {
        Interlocked.Increment(ref generation);
        receptionStartedTicks = DateTimeOffset.UtcNow.UtcTicks;
        entries.Clear(); nextOrder = 0; Overflowed = false; Version++;
    }
    internal void Clear() { Stop(); Reset(); }

    internal void OnComment(LiveStreamComment comment, bool live, int expectedGeneration, string joinWord, string removeWord)
    {
        if (!Accepting || !live || expectedGeneration != Generation || string.IsNullOrWhiteSpace(comment.Channel) || comment.Channel.Length > 128) return;
        if (comment.PublishedAt.HasValue && comment.PublishedAt.Value.UtcTicks < receptionStartedTicks) return;
        var body = comment.Text;
        if (string.IsNullOrEmpty(body) || body.Length > 2048) return;
        if (!string.IsNullOrWhiteSpace(removeWord) && body.Contains(removeWord, StringComparison.Ordinal))
        {
            if (entries.Remove(comment.Channel)) Version++;
            return;
        }
        if (string.IsNullOrWhiteSpace(joinWord) || !body.Contains(joinWord, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(comment.Name)) return;
        var name = StreamSafety.Plain(comment.Name, 80);
        if (string.IsNullOrWhiteSpace(name)) return;
        if (entries.TryGetValue(comment.Channel, out var old))
        {
            if (old.Name != name) { entries[comment.Channel] = (name, old.Order); Version++; }
            return;
        }
        if (entries.Count >= Capacity)
        {
            if (!Overflowed) { Overflowed = true; Version++; }
            return;
        }
        entries.Add(comment.Channel, (name, nextOrder++)); Version++;
    }
    internal string[] Snapshot() => entries.Values.OrderBy(x => x.Order).Select(x => x.Name).ToArray();
    internal static int PageCount(int count) => Math.Max(1, (count + PageSize - 1) / PageSize);
    internal static string[] Page(string[] names, int requestedPage) => names.Skip(Math.Clamp(requestedPage, 0, PageCount(names.Length) - 1) * PageSize).Take(PageSize).ToArray();
}
