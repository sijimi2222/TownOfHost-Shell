using System;
using System.Collections.Generic;
namespace TownOfHost.Modules;
internal static class NovaBeamRules
{
    internal static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    internal static bool ValidState(bool c, bool w, bool b, float angle)
        => (c ? 1 : 0) + (w ? 1 : 0) + (b ? 1 : 0) <= 1 && Finite(angle) && Math.Abs(angle) <= 36000f;
    internal static float SnapAngle(float angle, int mode)
    {
        if (!Finite(angle)) return 0f;
        float normalized = ((angle + 180f) % 360f + 360f) % 360f - 180f;
        return mode == 1 ? (Math.Abs(normalized) <= 90f ? 0f : 180f)
            : mode == 2 ? MathF.Round(angle / 90f) * 90f : angle;
    }
    internal static bool Contains(float along, float side, float reach, float width, float start)
        => Finite(along) && Finite(side) && Finite(reach) && Finite(width)
            && along >= start && along <= reach && side <= width * .5f + .3f;
}
// Nova-only cosmetic budget. Never suppress unrelated mandatory Shell teleports.
internal static class NovaVisualTraffic
{
    static readonly Queue<float> times = new(); static int game;
    internal static bool Take(int session, float now)
    {
        if (!NovaBeamRules.Finite(now)) return false;
        if (session != game || times.Count > 0 && now < times.Peek()) { times.Clear(); game = session; }
        while (times.Count > 0 && now - times.Peek() >= 6f) times.Dequeue();
        if (times.Count >= 120) return false;
        times.Enqueue(now); return true;
    }
}
