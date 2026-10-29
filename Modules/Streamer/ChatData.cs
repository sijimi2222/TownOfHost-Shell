using System.Text.Json;
namespace TownOfHost;
/// YouTube comment integration originally adapted from TownOfHost For E.
internal class ChatData
{
    public readonly string key, visitorData, clientVersion;
    public string continuation { get; private set; }
    internal int PollDelayMilliseconds { get; set; } = 3000;
    public ChatData(string key, string content, string visitorData, string clientVersion)
    { this.key = key; continuation = content; this.visitorData = visitorData; this.clientVersion = clientVersion; }
    public bool TryUpdateContinuation(string json)
    {
        if (!StreamCommentParser.TryLiveChat(json, out var chat) || !StreamCommentParser.TryContinuation(chat, out var token, out var delay)) return false;
        continuation = token; PollDelayMilliseconds = delay; return true;
    }
    public void UpdateContinuation(string json) => TryUpdateContinuation(json);
    public string Build() => JsonSerializer.Serialize(new { context = new { client = new { visitorData, clientName = "WEB", clientVersion, hl = "ja", gl = "JP" } }, continuation });
}
