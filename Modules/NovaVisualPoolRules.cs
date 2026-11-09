using System;
namespace TownOfHost.Modules;

// Total per Nova, including the star/guide. Shared by gameplay and the offline
// estimator; no controller/IPC dependency may enter the public game assembly.
internal static class NovaVisualPoolRules
{
    internal const int NormalTotal = 10, FungleTotal = 11, AirshipTotal = 15;
    internal static int Count(bool isFungle, bool isAirship = false) => isAirship ? AirshipTotal : isFungle ? FungleTotal : NormalTotal;
    internal static int GlyphCount(float span, float bar, int capacity)
    {
        if (!float.IsFinite(span) || !float.IsFinite(bar) || span <= 0 || bar <= 0 || bar > span || capacity < 1) return 0;
        // Keep the prior 30% overlap when it fits. With a fixed body pool, reduce
        // overlap only if every point is still covered by real measured quads.
        double minimum = Math.Ceiling((double)span / bar);
        if (minimum > capacity) return 0;
        double preferred = Math.Ceiling((span - bar) / (bar * .7f)) + 1;
        return (int)Math.Min(capacity, preferred);
    }
}
