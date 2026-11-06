using System;
using System.Net;

namespace TownOfHost.Modules;

// Production releases cannot enable an arbitrary relay through a local configuration file.
internal static class MatchmakingRelayEndpoint
{
#if SHELL_MATCHMAKING_DEVELOPMENT
    internal const bool DevelopmentBuild = true;
#else
    internal const bool DevelopmentBuild = false;
#endif
    internal static bool IsValid(string value, bool allowLoopback = false)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/") return false;
        if (uri.Scheme == Uri.UriSchemeHttps) return true;
        return allowLoopback && uri.Scheme == Uri.UriSchemeHttp && IsLiteralLoopback(uri);
    }
    internal static bool IsLiteralLoopback(Uri uri) => IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip)
        && IPAddress.IsLoopback(ip);
    internal static string Resolve(string production, string development, bool isDevelopment = DevelopmentBuild)
    {
        if (!isDevelopment) return IsValid(production) ? production : "";
        return IsValid(development, true) && Uri.TryCreate(development, UriKind.Absolute, out var uri)
            && IsLiteralLoopback(uri) ? development : "";
    }
}
