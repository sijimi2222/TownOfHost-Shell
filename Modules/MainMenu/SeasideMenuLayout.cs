using System;
using System.Collections.Generic;

namespace TownOfHost.Modules.MainMenu;

// Artwork pixels use a top-left origin. The same transform is used by the art and every hit area.
internal readonly record struct SeasideRect(double X, double Y, double Width, double Height)
{
    internal double CenterX => X + Width / 2;
    internal double CenterY => Y + Height / 2;
    internal bool Contains(double x, double y) => x >= X && x <= X + Width && y >= Y && y <= Y + Height;
    internal bool Overlaps(SeasideRect other) => X < other.X + other.Width && X + Width > other.X
        && Y < other.Y + other.Height && Y + Height > other.Y;
}
internal readonly record struct SeasideTarget(string Id, SeasideRect Rect, bool LeavesHome);
internal readonly record struct SeasideFit(double Scale, double Left, double Top)
{
    internal (double x, double y) ToScreen(double x, double y) => (Left + x * Scale, Top + y * Scale);
    internal (double x, double y) ToArt(double x, double y) => ((x - Left) / Scale, (y - Top) / Scale);
}
internal static class SeasideMenuLayout
{
    internal const double Width = 1672, Height = 941, PixelsPerUnit = 100;
    internal const string EmbeddedAsset = "TownOfHost.Resources.MainMenu.Seaside.png";
    internal static readonly IReadOnlyList<SeasideTarget> Targets = Array.AsReadOnly(new[]
    {
        new SeasideTarget("Play", new(18, 32, 570, 86), true),
        new SeasideTarget("Inventory", new(18, 132, 570, 85), true),
        new SeasideTarget("Shop", new(18, 232, 570, 84), true),
        new SeasideTarget("News", new(18, 333, 570, 85), true),
        new SeasideTarget("Account", new(18, 435, 570, 85), true),
        new SeasideTarget("Settings", new(18, 536, 570, 86), true),
        new SeasideTarget("Credits", new(18, 640, 570, 85), true),
        new SeasideTarget("Quit", new(18, 742, 570, 85), true),
        new SeasideTarget("Discord", new(630, 825, 167, 87), false),
        new SeasideTarget("YouTube", new(812, 825, 166, 87), false),
        new SeasideTarget("GitHub", new(993, 825, 165, 87), false),
        new SeasideTarget("Version", new(1181, 653, 471, 63), true),
        new SeasideTarget("Statistics", new(1181, 727, 471, 65), true),
        new SeasideTarget("Stream", new(1181, 803, 471, 70), true),
    });
    internal static readonly SeasideRect VersionBox = new(1200, 25, 420, 50);
    internal static readonly SeasideRect FriendsBox = new(1320, 98, 300, 46);
    // Below Quit, within the artwork's safe area; shares its aspect-preserving transform.
    internal static readonly SeasideRect GameVersionBox = new(38, 854, 550, 44);
    // The delivered logo PNG contains the unchanged 1672x941 composition at top-left,
    // with transparent right/bottom padding. Unity sprite rectangles have a bottom-left origin.
    internal static SeasideRect ArtworkPixels(int textureWidth, int textureHeight)
    {
        if (textureWidth == 1917 && textureHeight == 1038) return new(0, 97, Width, Height);
        if (textureWidth <= 0 || textureHeight <= 0
            || Math.Abs((double)textureWidth / textureHeight - Width / Height) >= .02)
            throw new ArgumentException("Unsupported seaside artwork composition");
        return new(0, 0, textureWidth, textureHeight);
    }
    internal static SeasideFit Fit(double viewportWidth, double viewportHeight)
    {
        if (!(viewportWidth > 0) || !(viewportHeight > 0)
            || double.IsInfinity(viewportWidth) || double.IsInfinity(viewportHeight))
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        double scale = Math.Min(viewportWidth / Width, viewportHeight / Height);
        return new(scale, (viewportWidth - Width * scale) / 2, (viewportHeight - Height * scale) / 2);
    }
    internal static (double x, double y) Local(double x, double y)
        => ((x - Width / 2) / PixelsPerUnit, (Height / 2 - y) / PixelsPerUnit);
}
