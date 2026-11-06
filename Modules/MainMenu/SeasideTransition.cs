using System;

namespace TownOfHost.Modules.MainMenu;

internal enum SeasidePage { Home, Play }
internal readonly record struct SeasideLetterPose(double X, double Y, double Rotation, double Alpha);
internal readonly record struct SeasideWaveSpan(double Left, double Right);

// Pure timeline shared by the Unity presenter and the offline preview. No scene/RPC side effects.
internal sealed class SeasideTransition
{
    internal const double Duration = 2.6, EnterEnd = .46, SwitchAt = .50, AssembleEnd = 1.7, HoldEnd = 2.3;
    internal const string Word = "TOH-Shell";
    internal SeasidePage Page { get; private set; }
    internal SeasidePage From { get; private set; }
    internal SeasidePage To { get; private set; }
    internal double Time { get; private set; }
    internal bool Active { get; private set; }
    internal bool Reverse => To == SeasidePage.Home;
    internal bool FullyCovered => Active && Time >= EnterEnd && Time <= HoldEnd;
    internal bool Begin(SeasidePage next)
    {
        if (Active || Page == next) return false;
        From = Page; To = next; Time = 0; Active = true; return true;
    }
    internal bool Advance(double dt)
    {
        if (!Active || !double.IsFinite(dt) || dt <= 0) return false;
        Time = Math.Min(Duration, Time + dt);
        if (Time >= SwitchAt) Page = To;
        if (Time + 1e-9 < Duration) return false;
        Time = Duration; Active = false; Page = To; return true;
    }
    internal void Cancel()
    {
        if (Active) Page = From;
        Active = false; Time = 0; From = To = Page;
    }
    internal void Restore(SeasidePage page) { Reset(); Page = From = To = page; }
    internal void Reset() { Active = false; Time = 0; Page = From = To = SeasidePage.Home; }
    internal static double Clamp(double t) => Math.Max(0, Math.Min(1, t));
    internal static double Smooth(double t) { t = Clamp(t); return t * t * (3 - 2 * t); }
    // Rounded crests echo the reference's scalloped/pointed leading edge, without a rectangular wipe.
    internal SeasideWaveSpan Span(double y)
    {
        double ripple = .027 * Math.Sin(y * 13 * Math.PI + Time * 3.2)
            + .009 * Math.Sin(y * 27 * Math.PI - Time * 2.1);
        double left = -1, right = 2;
        if (Time < EnterEnd) right = -.10 + 1.22 * Smooth(Time / EnterEnd) + ripple;
        else if (Time > HoldEnd) left = -.11 + 1.23 * Smooth((Time - HoldEnd) / (Duration - HoldEnd)) + ripple;
        if (Reverse) return new(1 - right, 1 - left);
        return new(left, right);
    }
    // Approximate italic glyph advances keep the assembled word compact instead of letter-spaced.
    static readonly double[] LetterWidths = { .611, .778, .722, .333, .667, .556, .556, .222, .222 };
    internal static double TargetX(int index)
    {
        double total = 0, preceding = 0;
        for (int i = 0; i < LetterWidths.Length; i++) { total += LetterWidths[i]; if (i < index) preceding += LetterWidths[i]; }
        return .31 + .38 * (preceding + LetterWidths[index] / 2) / total;
    }
    internal SeasideLetterPose Letter(int index)
    {
        if (index < 0 || index >= Word.Length) throw new ArgumentOutOfRangeException(nameof(index));
        double targetX = TargetX(index);
        double startX = .14 + index * .095;
        double startY = index % 2 == 0 ? .29 + .025 * (index % 3) : .70 - .02 * (index % 3);
        double amount = Smooth((Time - .18 - index % 3 * .045) / (AssembleEnd - .18 - index % 3 * .045));
        double drift = (1 - amount) * .008 * Math.Sin(Time * 4 + index * 1.8);
        double exit = Time > HoldEnd ? 1.2 * Smooth((Time - HoldEnd) / (Duration - HoldEnd)) * (Reverse ? -1 : 1) : 0;
        return new(startX + (targetX - startX) * amount + drift + exit,
            startY + (.5 - startY) * amount + drift * .6,
            (1 - amount) * ((index % 2 == 0 ? -12 : 15) + 3 * Math.Sin(Time * 3 + index)),
            Time < .34 ? 0 : Clamp((Time - .34) / .28));
    }
}

internal static class SeasidePlayLayout
{
    // Artist page uses the same 1672 x 941 logical canvas as the home page.
    internal static readonly SeasideRect Online = new(936, 247, 622, 482);
    internal static readonly SeasideRect Local = new(112, 247, 624, 482);
    internal static readonly SeasideRect Back = new(622, 822, 428, 82);
    internal static readonly string[] Ids = { "PlayOnline", "PlayLocal", "PlayBack" };
    internal static SeasideRect Rect(string id) => id switch
        { "PlayOnline" => Online, "PlayLocal" => Local, "PlayBack" => Back, _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    internal static SeasideRect Title(string id) => id switch
        { "PlayOnline" => new(936,247,622,122), "PlayLocal" => new(112,247,624,122), "PlayBack" => Back, _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    internal static SeasideRect IllustrationBox(string id) => id switch
        { "PlayOnline" => new(954,404,586,308), "PlayLocal" => new(126,404,586,308), _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    internal static SeasideRect FitIllustration(string id, double width, double height)
    {
        CropIllustration(id,width,height); // Validate before presenting the unchanged frame rectangle.
        return IllustrationBox(id);
    }
    // Source pixels (top-left origin). Cover uniformly scales then crops symmetrically;
    // no source PNG is edited, and the destination never grows outside the artwork frame.
    internal static SeasideRect CropIllustration(string id, double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        var box = IllustrationBox(id);
        double scale = Math.Max(box.Width / width, box.Height / height);
        double w = Math.Min(width,box.Width / scale), h = Math.Min(height,box.Height / scale);
        return new((width-w)/2,(height-h)/2,w,h);
    }
}
