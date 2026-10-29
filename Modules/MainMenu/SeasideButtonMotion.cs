using System;

namespace TownOfHost.Modules.MainMenu;

internal readonly record struct SeasideButtonPose(float X, float Y, float Rotation, float ScaleX, float ScaleY)
{
    internal static SeasideButtonPose Neutral => new(0, 0, 0, 1, 1);
}

// Pure state only. Time and the one-shot native action are owned by HomeView.
internal sealed class SeasideButtonMotion
{
    internal const double HoverPeriod = 1.1, SettleSeconds = .16, PressSeconds = .12, ReturnSeconds = .17;
    internal const double ClickSeconds = PressSeconds + ReturnSeconds;
    double hover, phase, press = -1;
    internal bool Pressing => press >= 0;
    internal SeasideButtonPose Pose { get; private set; } = SeasideButtonPose.Neutral;
    static double Smooth(double t) => t * t * (3 - 2 * t);
    internal bool BeginPress()
    {
        if (Pressing) return false;
        hover = phase = 0; press = 0; Pose = SeasideButtonPose.Neutral;
        return true;
    }
    internal bool Advance(double elapsed, bool selected)
    {
        if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed <= 0) return false;
        // A stall must not consume the whole visible feedback in a single render frame.
        double dt = Math.Min(elapsed, .1);
        if (Pressing)
        {
            press += dt;
            if (press + 1e-9 >= ClickSeconds) { Reset(); return true; }
            double amount = press <= PressSeconds ? Smooth(press / PressSeconds)
                : 1 - Smooth(Math.Min(1, (press - PressSeconds) / ReturnSeconds));
            Pose = new(0, (float)(-4 * amount), 0, (float)(1 - .03 * amount), (float)(1 - .05 * amount));
            return false;
        }
        hover = selected ? Math.Min(1, hover + dt / SettleSeconds) : Math.Max(0, hover - dt / SettleSeconds);
        if (hover == 0) { phase = 0; Pose = SeasideButtonPose.Neutral; return false; }
        phase = (phase + dt) % HoverPeriod;
        double wave = Math.Sin(phase * 2 * Math.PI / HoverPeriod) * Smooth(hover);
        Pose = new((float)(1.2 * wave), 0, (float)(.65 * wave), 1, 1);
        return false;
    }
    internal void Reset() { hover = phase = 0; press = -1; Pose = SeasideButtonPose.Neutral; }
}
