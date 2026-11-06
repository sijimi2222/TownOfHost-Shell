using System;

namespace TownOfHost.Modules;

internal static class HudHeaderLayout
{
    internal static float RequiredLeftShift(float textRight, float guideLeft)
    {
        if (float.IsNaN(textRight) || float.IsInfinity(textRight)
            || float.IsNaN(guideLeft) || float.IsInfinity(guideLeft)) return 0;
        return Math.Max(0, textRight - (guideLeft - .12f));
    }

    // Viewport-relative geometry preserves the gap across resolutions and camera aspect ratios.
    internal static bool TryRightOffset(float viewportWidth, float leftViewport, out float offset)
    {
        offset = 0;
        if (float.IsNaN(viewportWidth) || float.IsInfinity(viewportWidth) || viewportWidth <= 0
            || float.IsNaN(leftViewport) || float.IsInfinity(leftViewport) || leftViewport <= 0 || leftViewport >= 1)
            return false;
        offset = (1 - leftViewport) * viewportWidth + .12f;
        return true;
    }
}
