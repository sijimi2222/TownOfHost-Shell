namespace TownOfHost.Modules;

/// <summary>Retired YouTube-only feature. Keep inert entry points for old callers/settings.</summary>
public static class YouTubeMapVote
{
    // Option ID 1400171 remains reserved; persisted true values have no effect.
    public static OptionItem Enabled;
    public static bool IsOn => false;
    public static int Version => 0;
    public static void OnComment(string voterId, string text) { }
    public static void Reset() { }
    public static string Summary() => string.Empty;
    public static void ApplyAtGameStart() { }
}
