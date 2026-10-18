using System;

namespace TownOfHost;

// Development commands are authorized by the local UI entry point, never by an
// RPC's PlayerControl, FriendCode, PlayerId or a client-supplied origin flag.
internal static class DevelopmentCommandPolicy
{
    internal static bool TryGetRestrictedCommand(string text, out string command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.TrimStart().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].StartsWith("/", StringComparison.Ordinal)) return false;
        int index = parts[0].Equals("/cmd", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        if (index >= parts.Length) return false;
        var name = parts[index].TrimStart('/').ToLowerInvariant();
        if (name is not ("002" or "rev" or "revive" or "cr" or "debug" or "dgm" or "fps" or "wiop")) return false;
        command = name;
        return true;
    }

    internal static bool CanUseHostDevelopmentCommand(bool fromLocalInput, bool amHost,
        bool senderIsLocalHost, bool debugAuthenticated, bool optionEnabled)
        => fromLocalInput && amHost && senderIsLocalHost && debugAuthenticated && optionEnabled;
}
