using System.Linq;using System.Text;using TownOfHost;
internal static class FrameTextUnderTest {const int ShowCount=8,MaxTextLength=60;static string Clean(string s)=>StreamSafety.Plain(s,80);
    internal static string BuildText()
    {
        (string Name, string Text)[] items;
        lock (StreamerInfo.RecentComments)
            items = StreamerInfo.RecentComments.Skip(System.Math.Max(0, StreamerInfo.RecentComments.Count - ShowCount)).ToArray();
        if (items.Length == 0) return StreamerInfo.StatusMessage;
        var lf = ((char)10).ToString();
        var sb = new StringBuilder();
        if (StreamerInfo.HasConnectionError) sb.Append(StreamerInfo.StatusMessage).Append(lf);
        foreach (var (name, body) in items)
        {
            if (sb.Length > 0) sb.Append(lf);
            var t = Clean(body);
            if (t.Length > MaxTextLength) t = StreamSafety.Plain(t, MaxTextLength) + "…";
            sb.Append("<color=#ffd36e>").Append(Clean(name)).Append("</color>: ").Append(t);
        }
        return sb.ToString();
    }


}