using System;
using System.Text;
using HarmonyLib;
using Hazel;
using InnerNet;

namespace TownOfHost.Modules;

// Adapted from the supplied hamo 4.40.00 framing fix. Shell client RPCs and mandatory teleports are preserved.
public static class NetworkSafety
{
    public const int PacketLimit = 1000;
    public const int NameByteLimit = 800;
    private static bool splitting;
    private static int warnings;
    public static string ClampName(string name)
    {
        string result = NetworkPacketPlan.ClampName(name, NameByteLimit);
        if (name != result && ++warnings <= 20)
            Logger.Warn($"Name byte limit applied ({Encoding.UTF8.GetByteCount(name)} -> {Encoding.UTF8.GetByteCount(result)})", "NetworkSafety");
        return result;
    }
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendOrDisconnect)), HarmonyPriority(900)]
    private static class SplitPatch
    {
        public static bool Prefix(InnerNetClient __instance, [HarmonyArgument(0)] MessageWriter msg, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            if (splitting || msg == null || msg.Length <= PacketLimit) return true;
            // Shell AntiBlackout and explicit compound-RPC sections keep their existing transaction handling.
            if (TownOfHost.InnerNetClientPatch.DontTouch || AntiBlackout.IsCached) return true;
            try
            {
                var bytes = (byte[])msg.ToByteArray(true);
                int header = msg.SendOption == SendOption.Reliable ? 3 : 1;
                if (!NetworkPacketPlan.TrySplit(bytes, header, PacketLimit, out var bodies)) return true;
                // Build every writer before sending anything; never resend the original after a partial send.
                var writers = new System.Collections.Generic.List<MessageWriter>();
                try
                {
                    foreach (var body in bodies)
                    {
                        var writer = MessageWriter.Get(msg.SendOption); writers.Add(writer); writer.Write(body);
                    }
                }
                catch
                {
                    foreach (var writer in writers) writer.Recycle();
                    throw;
                }
                splitting = true;
                try
                {
                    foreach (var writer in writers) __instance.SendOrDisconnect(writer);
                }
                catch (Exception e)
                {
                    Logger.Warn($"Split send interrupted ({e.GetType().Name}); original not replayed", "NetworkSafety");
                }
                finally
                {
                    splitting = false;
                    foreach (var writer in writers) writer.Recycle();
                }
                return false;
            }
            catch (Exception e)
            {
                Logger.Warn($"Packet could not be safely split ({e.GetType().Name}); preserving original", "NetworkSafety");
                return true;
            }
        }
    }
}
