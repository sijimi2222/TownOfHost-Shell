using System;

namespace TownOfHost.Modules;

// Shared budget ONLY for new TextDummy visuals. Existing Nova/Destroyer traffic is unchanged.
internal sealed class RoleVisualBudget
{
    int game = int.MinValue;
    float lastTime;
    float tokens;
    readonly float rate, capacity;
    public RoleVisualBudget(float perSecond, float burst) { rate = perSecond; capacity = burst; }
    public bool Take(int gameId, float now)
    {
        if (!float.IsFinite(now)) return false;
        if (game != gameId || now < lastTime) { game = gameId; tokens = capacity; lastTime = now; }
        tokens = Math.Min(capacity, tokens + Math.Max(0f, now - lastTime) * rate);
        lastTime = now;
        if (tokens < 1f) return false;
        tokens -= 1f;
        return true;
    }
}
