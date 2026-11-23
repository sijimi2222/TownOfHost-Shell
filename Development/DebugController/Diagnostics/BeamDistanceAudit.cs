using System;
using System.Collections.Generic;
using System.Linq;

namespace TownOfHost.Development;

// Observation only. No Unity, network, configuration or gameplay dependencies.
internal sealed class BeamDistanceAudit
{
    internal const int ShotsPerGame = 64, MessageCharacters = 32768;
    private readonly Dictionary<object, Shot> pending = new();
    private int game, admitted;
    private bool initialized;
    private long next;
    internal sealed class Shot
    {
        internal long Id;
        internal bool InsideFire = true, Planned, Observed, SawVisible;
        internal float FirstVisible;
        internal bool Observe(float now, bool visible)
        {
            if (Observed || !visible || !float.IsFinite(now)) return false;
            if (!SawVisible) { SawVisible = true; FirstVisible = now; return false; }
            if (now - FirstVisible < .12f) return false;
            Observed = true; return true;
        }
    }
    internal void Game(int id)
    {
        if (initialized && id == game) return;
        initialized = true; game = id; admitted = 0; pending.Clear();
    }
    internal Shot Begin(object owner, int id)
    {
        Game(id);
        if (owner == null || admitted >= ShotsPerGame) return null;
        var shot = new Shot { Id = ++next };
        pending[owner] = shot; admitted++; return shot;
    }
    internal Shot Find(object owner) => owner != null && pending.TryGetValue(owner, out var shot) ? shot : null;
    internal void End(object owner) { if (owner != null) pending.Remove(owner); }
    internal int Pending => pending.Count;
    internal static string UnusedReason(int total, int used, bool beaming, bool ready) => !ready ? "pool_not_ready_or_retired"
        : !beaming ? "not_firing_or_cancelled" : used < 0 || used > Math.Max(0, total - 1) ? "unknown_plan"
        : used < total - 1 ? "coverage_plan_complete_spare_slots_parked" : "all_beam_slots_used";
    internal static (float Min, float Max, int Gaps, bool Complete) Coverage(IEnumerable<(float Min, float Max)> ranges, float start, float end)
    {
        var valid = ranges.Where(x => float.IsFinite(x.Min) && float.IsFinite(x.Max) && x.Max >= x.Min).OrderBy(x => x.Min).ToArray();
        if (valid.Length == 0) return (float.NaN, float.NaN, 0, false);
        int gaps = 0; float covered = start;
        foreach (var interval in valid) { if (interval.Min > covered + .005f) gaps++; covered = Math.Max(covered, interval.Max); }
        return (valid[0].Min, valid.Max(x => x.Max), gaps, gaps == 0 && covered >= end - .005f);
    }
}
