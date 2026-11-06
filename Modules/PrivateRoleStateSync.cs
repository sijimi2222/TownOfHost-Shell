using System;
using System.Collections.Generic;
using Hazel;
using TownOfHost.Roles.Core;

namespace TownOfHost.Modules;

// Optional, owner-only state inside the existing role-sync RPC. Legacy role payloads are unchanged.
internal static class PrivateRoleStateSync
{
    internal const int Marker = 0x53484D31;
    private sealed record Endpoint(RoleBase Owner, Action<MessageReader> Apply);
    private static readonly Dictionary<byte, Endpoint> endpoints = new();
    internal static void Register(RoleBase owner, Action<MessageReader> apply)
    {
        if (owner?.Player != null) endpoints[owner.Player.PlayerId] = new(owner, apply);
    }
    internal static void Unregister(RoleBase owner)
    {
        if (owner?.Player != null && endpoints.TryGetValue(owner.Player.PlayerId, out var current)
            && ReferenceEquals(current.Owner, owner)) endpoints.Remove(owner.Player.PlayerId);
    }
    internal static void Send(RoleBase owner, Action<MessageWriter> write)
    {
        var player = owner?.Player;
        if (AmongUsClient.Instance?.AmHost != true || player == null || player == PlayerControl.LocalPlayer
            || !player.IsModClient() || !ReferenceEquals(player.GetRoleClass(), owner)) return;
        int client = player.GetClientId();
        if (client < 0 || PlayerControl.LocalPlayer == null) return;
        var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId,
            (byte)CustomRPC.CustomRoleSync, SendOption.Reliable, client);
        writer.Write(player.PlayerId); writer.Write(Marker); write(writer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    internal static bool TryRead(RoleBase owner, MessageReader reader, bool trustedHost)
    {
        if (reader.Length - reader.Position < 4) return false;
        int position = reader.Position;
        if (reader.ReadInt32() != Marker) { reader.Position = position; return false; }
        if (!trustedHost || AmongUsClient.Instance?.AmHost == true || owner?.Player != PlayerControl.LocalPlayer) return true;
        if (!endpoints.TryGetValue(owner.Player.PlayerId, out var endpoint) || !ReferenceEquals(endpoint.Owner, owner)) return true;
        try { endpoint.Apply(reader); }
        catch (Exception e) { Logger.Warn($"Private role state rejected ({e.GetType().Name})", "RoleSync"); }
        return true;
    }
}
