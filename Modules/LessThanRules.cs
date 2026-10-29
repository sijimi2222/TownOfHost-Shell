using System;
using System.Collections.Generic;

namespace TownOfHost.Roles.Neutral;

internal sealed class LessThanNumbers
{
    readonly Dictionary<byte, int> values = new();
    internal static bool InRange(int value) => value >= 1 && value <= 100;
    internal void Assign(byte id, Func<int> draw)
    {
        int value = draw();
        if (!InRange(value)) throw new ArgumentOutOfRangeException(nameof(draw));
        values[id] = value;
    }
    internal bool TryGet(byte id, out int value) => values.TryGetValue(id, out value);
    internal void Clear() => values.Clear();
    internal static int Compare(int own, int target)
    {
        if (!InRange(own) || !InRange(target)) throw new ArgumentOutOfRangeException();
        if (own == target) return 0;
        return own > target ? 1 : -1;
    }
}
internal sealed class LessThanRerollUsage
{
    int roundUses, meetingUses;
    internal void Reset() { roundUses = meetingUses = 0; }
    internal void BeginMeeting() => meetingUses = 0;
    internal int Remaining(int limit, bool perMeeting)
        => Math.Max(0, Math.Clamp(limit, 1, 10) - (perMeeting ? meetingUses : roundUses));
    internal bool TryUse(int limit, bool perMeeting)
    {
        if (Remaining(limit, perMeeting) == 0) return false;
        roundUses++; meetingUses++; return true;
    }
}
