using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TownOfHost;

// Pure display/input rules: no Unity objects, credentials or network access.
internal static class StreamSafety
{
    internal static string VideoId(string text)
    {
        if (text == null || text.Length > 2048 || !Uri.TryCreate(text.Trim(), UriKind.Absolute, out var u)
            || u.Scheme != Uri.UriSchemeHttps || !u.IsDefaultPort || u.UserInfo.Length != 0) return "";
        var host = u.IdnHost.ToLowerInvariant();
        var segments = u.AbsolutePath.Trim('/').Split('/');
        string id = "";
        if (host == "youtu.be" && segments.Length == 1) id = segments[0];
        else if (host is "youtube.com" or "www.youtube.com" or "m.youtube.com")
        {
            if (segments.Length == 2 && segments[0] is "live" or "shorts" or "embed") id = segments[1];
            else if (segments.Length == 1 && segments[0] == "watch")
            {
                var values = u.Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2))
                    .Where(x => x.Length == 2 && x[0] == "v").ToArray();
                if (values.Length == 1) id = Uri.UnescapeDataString(values[0][1]);
            }
        }
        return Regex.IsMatch(id, @"\A[A-Za-z0-9_-]{11}\z") ? id : "";
    }

    internal static string Plain(string value, int maxElements = 80)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var b = new StringBuilder(); int count = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (++count > maxElements) break;
            if (Rune.IsControl(rune)) continue;
            b.Append(rune.Value == '<' ? "＜" : rune.Value == '>' ? "＞" : rune.ToString());
        }
        return b.ToString().Trim();
    }

    internal static bool RevealRoles(int mode, bool inGame, bool localDead)
        => inGame && (mode == 2 || mode == 1 && localDead);

    internal static string Color(string value)
        => value != null && Regex.IsMatch(value, @"\A#[0-9a-fA-F]{6}\z") ? value : "#ffffff";
}

internal readonly record struct LiveStreamComment(string Id, string Channel, string Name, string Text, System.DateTimeOffset? PublishedAt = null);
