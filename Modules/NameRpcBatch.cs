using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using InnerNet;

namespace TownOfHost.Modules;

// Recipient-scoped adaptation of hamo 4.40.00. RPC format and unreliable delivery match RpcSetNamePrivate.
public static class NameRpcBatch
{
    private sealed class Entry
    {
        public uint NetId, DataNetId;
        public string Name;
        public bool HiddenOnModded;
    }
    private const int MaxBytesPerPacket = 900;
    private static readonly Dictionary<int, List<Entry>> queue = new();
    private static (int game, int host) session;
    private static bool flushing;
    public static void Clear() => queue.Clear();

    public static void Add(int clientId, uint netId, uint dataNetId, string name, bool dontShowOnModdedClient)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || !client.AmConnected || clientId < 0 || name == null) return;
        var current = (client.GameId, client.ClientId);
        if (session != current) { queue.Clear(); session = current; }
        if (!queue.TryGetValue(clientId, out var list)) queue[clientId] = list = new();
        var entry = list.FirstOrDefault(e => e.NetId == netId);
        if (entry == null) { entry = new Entry { NetId = netId }; list.Add(entry); }
        entry.DataNetId = dataNetId;
        entry.Name = NetworkSafety.ClampName(name);
        entry.HiddenOnModded = dontShowOnModdedClient;
    }

    public static void Flush()
    {
        if (flushing || queue.Count == 0) return;
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || !client.AmConnected || session != (client.GameId, client.ClientId))
        { queue.Clear(); return; }
        var pending = queue.ToArray();
        queue.Clear(); // A failed/partially sent batch is never replayed.
        var connected = new HashSet<int>();
        var objects = new Dictionary<uint, uint>();
        foreach (var player in PlayerCatch.AllPlayerControls)
        {
            if (player?.Data == null || player.Data.Disconnected) continue;
            int id = player.GetClientId();
            if (id >= 0) connected.Add(id);
            objects[player.NetId] = player.Data.NetId;
        }
        flushing = true;
        try
        {
            foreach (var (clientId, entries) in pending)
            {
                if (!connected.Contains(clientId)) continue;
                MessageWriter writer = null;
                void Send()
                {
                    if (writer == null) return;
                    try { writer.EndMessage(); client.SendOrDisconnect(writer); }
                    finally { writer.Recycle(); writer = null; }
                }
                try
                {
                    foreach (var entry in entries)
                    {
                        if (!objects.TryGetValue(entry.NetId, out var data) || data != entry.DataNetId) continue;
                        if (writer != null && writer.Length + System.Text.Encoding.UTF8.GetByteCount(entry.Name) + 24 > MaxBytesPerPacket) Send();
                        if (writer == null)
                        {
                            writer = MessageWriter.Get(SendOption.None);
                            writer.StartMessage(6);
                            writer.Write(client.GameId); writer.WritePacked(clientId);
                        }
                        writer.StartMessage(2); writer.WritePacked(entry.NetId); writer.Write((byte)RpcCalls.SetName);
                        writer.Write(entry.DataNetId); writer.Write(entry.Name); writer.Write(entry.HiddenOnModded); writer.EndMessage();
                    }
                    Send();
                }
                finally { writer?.Recycle(); }
            }
        }
        finally { flushing = false; }
    }

    private static void TryFlush()
    {
        try { Flush(); }
        catch (Exception e) { queue.Clear(); Logger.Warn($"Name batch interrupted ({e.GetType().Name}); not replayed", "NameRpcBatch"); }
    }
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendOrDisconnect)), HarmonyPriority(1000)]
    private static class KeepOrderPatch
    {
        public static void Prefix() { if (!flushing) TryFlush(); }
    }
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]
    private static class EndFramePatch
    {
        public static void Postfix() => TryFlush();
    }
}
