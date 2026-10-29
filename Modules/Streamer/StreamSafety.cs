using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
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

internal readonly record struct LiveStreamComment(string Id, string Channel, string Name, string Text);

internal readonly record struct StreamBootstrapDiagnostic(bool Config, bool Renderer, bool Key, bool Version, bool Continuation);

internal static class StreamCommentParser
{
    private static readonly Regex ConfigStart = new(@"\bytcfg\s*\.\s*set\s*\(\s*", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex InitialStart = new(@"(?:\bytInitialData|window\s*\[\s*[""']ytInitialData[""']\s*\]|window\s*\.\s*ytInitialData)\s*=\s*", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static JsonNode Child(JsonNode node, string key) => node is JsonObject obj ? obj[key] : null;
    private static string Text(JsonNode node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";
    private static JsonObject ObjectAt(string html, int start)
    {
        while (start < html.Length && char.IsWhiteSpace(html[start])) start++;
        if (start >= html.Length || html[start] != '{') return null;
        bool quoted = false, escaped = false; int depth = 0;
        for (int i = start; i < html.Length; i++)
        {
            char c = html[i];
            if (quoted) { if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; continue; }
            if (c == '"') quoted = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0)
            {
                try { return JsonNode.Parse(html.Substring(start, i - start + 1)) as JsonObject; }
                catch (System.Text.Json.JsonException) { return null; }
            }
        }
        return null;
    }
    private static JsonObject InitialRenderer(string html)
    {
        foreach (Match m in InitialStart.Matches(html))
        {
            var root = ObjectAt(html, m.Index + m.Length);
            if (Child(Child(root, "contents"), "liveChatRenderer") is JsonObject chat) return chat;
        }
        return null;
    }

    internal static bool TryBootstrap(string html, out ChatData session, out IReadOnlyList<LiveStreamComment> comments, out StreamBootstrapDiagnostic diagnostic)
    {
        session = null; comments = Array.Empty<LiveStreamComment>();
        var config = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (Match m in ConfigStart.Matches(html))
        {
            var obj = ObjectAt(html, m.Index + m.Length);
            if (obj != null) foreach (var pair in obj) config[pair.Key] = pair.Value;
        }
        JsonNode Setting(string name) => config.TryGetValue(name, out var value) ? value : null;
        var client = Child(Setting("INNERTUBE_CONTEXT"), "client");
        string key = Text(Setting("INNERTUBE_API_KEY"));
        string version = Text(Child(client, "clientVersion"));
        if (version.Length == 0) version = Text(Setting("INNERTUBE_CLIENT_VERSION"));
        string visitor = Text(Child(client, "visitorData"));
        if (visitor.Length == 0) visitor = Text(Setting("VISITOR_DATA"));
        var chat = InitialRenderer(html);
        bool hasNext = TryContinuation(chat, out var token, out int delay);
        diagnostic = new(config.Count > 0, chat != null, key.Length is > 0 and <= 512, Regex.IsMatch(version, @"\A[A-Za-z0-9_.-]{1,128}\z"), hasNext);
        if (!diagnostic.Key || !diagnostic.Version || !diagnostic.Renderer || !diagnostic.Continuation) return false;
        session = new ChatData(key, token, visitor.Length <= 4096 ? visitor : "", version) { PollDelayMilliseconds = delay };
        comments = ParseActions(Child(chat, "actions"));
        return true;
    }

    internal static bool TryContinuation(JsonNode chat, out string token, out int delay)
    {
        token = ""; delay = 3000;
        if (Child(chat, "continuations") is not JsonArray entries) return false;
        foreach (var entry in entries.Take(16))
        foreach (var kind in new[] { "invalidationContinuationData", "timedContinuationData", "reloadContinuationData" })
        {
            var next = Child(entry, kind); var value = Text(Child(next, "continuation"));
            if (value.Length == 0 || value.Length > 4096) continue;
            token = value;
            if (Child(next, "timeoutMs") is JsonValue ms && ms.TryGetValue<int>(out int timeout)) delay = Math.Clamp(timeout, 3000, 60000);
            return true;
        }
        return false;
    }

    internal static IReadOnlyList<LiveStreamComment> ParseActions(JsonNode actions)
    {
        var result = new List<LiveStreamComment>();
        if (actions is not JsonArray array) return result;
        foreach (var action in array.Take(200))
        {
            var item = Child(Child(action, "addChatItemAction"), "item");
            var chat = Child(item, "liveChatTextMessageRenderer") ?? Child(item, "liveChatPaidMessageRenderer");
            if (chat is not JsonObject) continue;
            var text = new StringBuilder();
            if (Child(Child(chat, "message"), "runs") is JsonArray runs)
                foreach (var run in runs.Take(100))
                {
                    var fragment = Text(Child(run, "text"));
                    if (fragment.Length == 0 && Child(Child(run, "emoji"), "shortcuts") is JsonArray shortcuts && shortcuts.Count > 0) fragment = Text(shortcuts[0]);
                    text.Append(fragment); if (text.Length >= 2048) break;
                }
            if (text.Length == 0) text.Append(Text(Child(Child(chat, "message"), "simpleText")));
            var id = Text(Child(chat, "id"));
            if (text.Length == 0 || id.Length == 0 || id.Length > 256) continue;
            string channel = Text(Child(chat, "authorExternalChannelId"));
            if (channel.Length > 128) channel = ""; // Never merge distinct accounts through a truncated ID.
            string body = string.Concat(text.ToString().EnumerateRunes().Take(2048).Select(r => r.ToString()));
            result.Add(new LiveStreamComment(id, channel, StreamSafety.Plain(Text(Child(Child(chat, "authorName"), "simpleText")), 40), body));
        }
        return result;
    }

    internal static bool TryLiveChat(string json, out JsonObject chat)
    {
        chat = Child(Child(JsonNode.Parse(json), "continuationContents"), "liveChatContinuation") as JsonObject;
        return chat != null;
    }
    internal static IReadOnlyList<LiveStreamComment> ParseLive(string json)
        => TryLiveChat(json, out var chat) ? ParseActions(Child(chat, "actions")) : Array.Empty<LiveStreamComment>();

    internal static IReadOnlyList<LiveStreamComment> ParseInitial(string html)
        => ParseActions(Child(InitialRenderer(html), "actions"));
}
