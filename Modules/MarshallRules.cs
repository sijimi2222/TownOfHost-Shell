using System;
using System.Collections.Generic;
using System.Linq;
namespace TownOfHost.Modules;

// Host-independent rules shared by the live role and executable tests. No RPC or Unity state.
internal sealed class MarshallUsage
{
    internal int TotalUsed { get; private set; }
    internal int MeetingUsed { get; private set; }
    internal void BeginMeeting() => MeetingUsed = 0;
    internal bool Available(int total, int perMeeting) => total != 0 && (total < 0 || TotalUsed < total)
        && (perMeeting < 0 || MeetingUsed < perMeeting);
    internal bool TrySpend(int total, int perMeeting, bool validTarget, bool alreadySuppressed, bool knownZeroPower)
    {
        if (!validTarget || alreadySuppressed || knownZeroPower || !Available(total, perMeeting)) return false;
        if (TotalUsed < int.MaxValue) TotalUsed++;
        if (MeetingUsed < int.MaxValue) MeetingUsed++;
        return true;
    }
}
internal static class MarshallVoteRules
{
    internal static int Adjust(int raw, bool suppressed) => suppressed ? raw <= 0 ? 0 : raw - 1 : raw;
    // A single shared preference wins. Conflicting Marshalls do not take a majority vote.
    internal static byte Preferred(IReadOnlyDictionary<byte, int> counts, IEnumerable<byte> ballots, byte none = 254)
    {
        if (counts == null || counts.Count == 0) return none;
        int max = counts.Values.Max();
        if (max <= 0) return none;
        var tied = counts.Where(p => p.Value == max).Select(p => p.Key).ToHashSet();
        if (tied.Count < 2) return none;
        var selected = ballots.Where(tied.Contains).Distinct().Take(2).ToArray();
        return selected.Length == 1 ? selected[0] : none;
    }
}
internal sealed class MarshallMeetingLedger
{
    internal bool Open { get; private set; }
    internal int Generation { get; private set; }
    readonly HashSet<byte> aliveAtStart = new();
    readonly HashSet<byte> reported = new();
    // Confirmed kill RPCs carry their logical meeting epoch through delayed execution.
    readonly Dictionary<byte, (int generation, bool duringMeeting)> queuedKills = new();
    internal readonly HashSet<byte> Suppressed = new();
    internal void Prepare()
    {
        Open = false; Generation++; aliveAtStart.Clear(); reported.Clear(); Suppressed.Clear();
    }
    internal void Begin(IEnumerable<byte> alive)
    {
        aliveAtStart.Clear(); aliveAtStart.UnionWith(alive); reported.Clear(); Suppressed.Clear(); Open = true;
    }
    internal void QueueKill(byte target, bool meetingNow)
        => queuedKills[target] = (Generation, Open && meetingNow);
    internal bool Death(byte target, bool meetingNow)
    {
        bool queued = queuedKills.Remove(target, out var cause);
        if (!Open || !meetingNow || !aliveAtStart.Contains(target) || reported.Contains(target)) return false;
        if (queued && (!cause.duringMeeting || cause.generation != Generation)) return false;
        reported.Add(target); return true;
    }
    internal void Close() { Open = false; Suppressed.Clear(); aliveAtStart.Clear(); reported.Clear(); }
    internal void Reset() { Close(); queuedKills.Clear(); Generation = 0; }
}
