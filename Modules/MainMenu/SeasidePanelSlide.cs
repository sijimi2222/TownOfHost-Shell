using System;

namespace TownOfHost.Modules.MainMenu;

// Position only. Native panel dimensions, scale and anchoring remain the game's responsibility.
internal sealed class SeasidePanelSlide
{
    internal const double Duration = .32;
    internal double Openness { get; private set; }
    internal bool Closing { get; private set; }
    internal bool Finished => Closing && Openness <= 0;
    internal double Offset => 1 - Openness * Openness * (3 - 2 * Openness);
    internal void Close() => Closing = true;
    internal void Open() => Closing = false;
    internal void Advance(double delta)
    {
        if (double.IsNaN(delta) || double.IsInfinity(delta) || delta < 0) return;
        Openness = Math.Clamp(Openness + (Closing ? -1 : 1) * Math.Min(delta, .1) / Duration, 0, 1);
    }
}
