using System;
using System.Collections.Generic;
using System.Text.Json;
namespace TownOfHost.Modules;

// Avoid the managed/IL2CPP Newtonsoft assembly's incompatible JToken indexer signatures.
internal static class ShellJsonData
{
    internal sealed record News(int Number, string Title, string Subtitle, string Short, string Body, string Date);
    private static string Text(JsonElement value, string key) => value.TryGetProperty(key, out var item)
        && item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : "";
    internal static bool TryReadBeta(string json, out string name, out string url, out string body)
    {
        name = url = body = "";
        if (json == null || json.Length > 2 * 1024 * 1024) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            string title = Text(root, "name"), download = Text(root, "url");
            if (title.Length == 0 || download.Length == 0) return false;
            name = title; url = download; body = Text(root, "body"); return true;
        }
        catch (JsonException) { return false; }
    }
    internal static bool TryReadNews(string json, out List<News> news)
    {
        news = new();
        if (json == null || json.Length > 2 * 1024 * 1024) return false;
        try
        {
            using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("News", out var items)
                || items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 256) return false;
            var numbers = new HashSet<int>();
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("Number", out var number)) continue;
                int id;
                if (number.ValueKind == JsonValueKind.Number) { if (!number.TryGetInt32(out id)) continue; }
                else if (number.ValueKind == JsonValueKind.String) { if (!int.TryParse(number.GetString(), out id)) continue; }
                else continue;
                if (!numbers.Add(id)) continue;
                news.Add(new(id, Text(item,"Title"), Text(item,"Subtitle"), Text(item,"Short"), Text(item,"Body"), Text(item,"Date")));
            }
            return true;
        }
        catch (JsonException) { news.Clear(); return false; }
    }
    internal sealed record Blocked(string Code, string AddedMod, string ReasonCode, string ReasonTitle, string ReasonDescription, string EndBanTime, bool IsPUID);
    internal static bool TryReadBlacklist(string json, out List<Blocked> records)
    {
        records = new();
        if (json == null || json.Length > 32 * 1024 * 1024) return false;
        try
        {
            using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            foreach (var (key, identifier, puid) in new[] { ("blockedPlayers","FriendCode",false), ("blockedPlayersPUID","PUID",true) })
            {
                if (!root.TryGetProperty(key,out var list) || list.ValueKind != JsonValueKind.Array) { records.Clear(); return false; }
                foreach (var item in list.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) { records.Clear(); return false; }
                    var reason = item.TryGetProperty("Reason",out var value) && value.ValueKind == JsonValueKind.Object ? value : default;
                    string Reason(string field) => reason.ValueKind == JsonValueKind.Object ? Text(reason,field) : "";
                    records.Add(new(Text(item,identifier),Text(item,"AddedMod"),Reason("Code"),Reason("Title"),Reason("Description"),Text(item,"EndBanTime"),puid));
                }
            }
            return true;
        }
        catch (JsonException) { records.Clear(); return false; }
    }
}
