using System;
using System.Collections.Generic;
using System.Text;

namespace TownOfHost.Modules;

// Protocol framing only. Does not change RPC IDs, payloads, destinations, or reliability.
internal static class NetworkPacketPlan
{
    internal static bool TrySplit(byte[] packet, int headerBytes, int limit, out List<byte[]> bodies)
    {
        var packets = new List<byte[]>();
        bodies = packets;
        if (packet == null || headerBytes is < 1 or > 3 || packet.Length <= headerBytes || limit <= headerBytes + 8) return false;
        var groups = new List<(byte tag, byte[] prefix, List<byte[]> children, byte[] raw)>();
        int position = headerBytes;
        while (position < packet.Length)
        {
            if (packet.Length - position < 3) return false;
            int length = packet[position] | packet[position + 1] << 8;
            byte tag = packet[position + 2];
            int start = position + 3, end = start + length;
            if (end > packet.Length) return false;
            if (tag is 5 or 6)
            {
                if (length < 4) return false;
                int child = start + 4;
                if (tag == 6)
                {
                    int count = 0;
                    byte value;
                    do
                    {
                        if (child >= end || ++count > 5) return false;
                        value = packet[child++];
                    } while ((value & 0x80) != 0);
                }
                var prefix = Copy(packet, start, child - start);
                var children = new List<byte[]>();
                while (child < end)
                {
                    if (end - child < 3) return false;
                    int size = 3 + packet[child] + (packet[child + 1] << 8);
                    if (size > end - child) return false;
                    children.Add(Copy(packet, child, size));
                    child += size;
                }
                // Empty GameData is retained exactly, never silently dropped.
                if (children.Count == 0) groups.Add((tag, null, null, Copy(packet, position, end - position)));
                else groups.Add((tag, prefix, children, null));
            }
            else groups.Add((tag, null, null, Copy(packet, position, end - position)));
            position = end;
        }
        var current = new List<byte>();
        void Flush()
        {
            if (current.Count == 0) return;
            packets.Add(current.ToArray());
            current.Clear();
        }
        foreach (var group in groups)
        {
            if (group.raw != null)
            {
                if (headerBytes + current.Count + group.raw.Length > limit) Flush();
                current.AddRange(group.raw);
                continue;
            }
            var pending = new List<byte>();
            foreach (var child in group.children)
            {
                int envelope = 3 + group.prefix.Length;
                if (pending.Count > 0 && headerBytes + current.Count + envelope + pending.Count + child.Length > limit)
                {
                    AppendEnvelope(current, group.tag, group.prefix, pending);
                    pending.Clear();
                    Flush();
                }
                if (pending.Count == 0 && headerBytes + current.Count + envelope + child.Length > limit) Flush();
                // An individual RPC is indivisible. Preserve it even if oversized.
                pending.AddRange(child);
            }
            if (pending.Count > 0) AppendEnvelope(current, group.tag, group.prefix, pending);
        }
        Flush();
        return bodies.Count > 1;
    }

    private static byte[] Copy(byte[] bytes, int start, int count)
    {
        var result = new byte[count];
        Array.Copy(bytes, start, result, 0, count);
        return result;
    }
    private static void AppendEnvelope(List<byte> output, byte tag, byte[] prefix, List<byte> payload)
    {
        int length = prefix.Length + payload.Count;
        output.Add((byte)length); output.Add((byte)(length >> 8)); output.Add(tag);
        output.AddRange(prefix); output.AddRange(payload);
    }

    internal static string ClampName(string name, int byteLimit = 800)
    {
        if (name == null || Encoding.UTF8.GetByteCount(name) <= byteLimit) return name;
        var result = new StringBuilder(); int size = 0;
        for (int i = 0; i < name.Length; i++)
        {
            int count = char.IsHighSurrogate(name[i]) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]) ? 2 : 1;
            string part = name.Substring(i, count); int bytes = Encoding.UTF8.GetByteCount(part);
            if (size + bytes > byteLimit) break;
            result.Append(part); size += bytes; i += count - 1;
        }
        string text = result.ToString(); int tagStart = text.LastIndexOf('<');
        return tagStart >= 0 && text.IndexOf('>', tagStart) < 0 ? text[..tagStart] : text;
    }
}
