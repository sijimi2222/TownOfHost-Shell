using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TownOfHost.Modules;

// Managed parsing only: no Newtonsoft JToken indexers, network access or Unity objects.
internal static class ShellReleaseMetadata
{
    internal sealed record Latest(Version Version, string Title, string Body, string DownloadUrl);
    internal static Version ParseVersion(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var match = Regex.Match(tag.Trim(), @"\A[vVsS]?(\d+\.\d+\.\d+(?:\.\d+)?)(?:[-β].*)?\z");
        return match.Success && Version.TryParse(match.Groups[1].Value, out var version) ? version : null;
    }
    internal static bool TryRead(string json, string platform, out Latest latest)
    {
        latest = null;
        if (json == null || json.Length > 2 * 1024 * 1024) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            static string Text(JsonElement value, string key) => value.TryGetProperty(key, out var field)
                && field.ValueKind == JsonValueKind.String ? field.GetString() ?? "" : "";
            string tag = Text(root, "tag_name");
            Version version = ParseVersion(tag);
            if (version == null || !root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return false;
            string preferred = platform == "Steam" ? "TownOfHost-Shell_Steam.dll" : platform == "Epic" ? "TownOfHost-Shell_Epic.dll" : "";
            string generic = null, specific = null;
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.ValueKind != JsonValueKind.Object) continue;
                string name = Text(asset, "name"), url = Text(asset, "browser_download_url");
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0) continue;
                // The updater stays bound to Shell's release repository.
                if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                    || !uri.AbsolutePath.StartsWith("/sijimi2222/TownOfHost-Shell/releases/download/", StringComparison.OrdinalIgnoreCase)) continue;
                if (name == "TownOfHost-Shell.dll") generic = url;
                if (name == preferred) specific = url;
            }
            string selected = specific ?? generic;
            if (selected == null) return false;
            latest = new Latest(version, "Ver. " + tag.TrimStart('v', 'V', 'S', 's'), Text(root, "body"), selected);
            return true;
        }
        catch (JsonException) { return false; }
    }
}
