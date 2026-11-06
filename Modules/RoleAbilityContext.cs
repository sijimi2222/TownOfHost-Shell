using System;
using TownOfHost.Roles.Core;

namespace TownOfHost.Modules;

// Lifetime validation for the newly imported abilities only. Never infer Unity identity
// from managed wrapper identity; roles themselves are ordinary managed instances.
internal sealed class RoleAbilityContext
{
    readonly RoleBase owner;
    readonly int gameId;
    readonly uint netId;
    public RoleAbilityContext(RoleBase role)
    { owner = role; gameId = AmongUsClient.Instance.GameId; netId = role.Player.NetId; }
    public bool IsCurrent => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
        && AmongUsClient.Instance.GameId == gameId && owner.Player != null
        && IsSamePlayer(owner.Player, netId) && ReferenceEquals(owner.Player.GetRoleClass(), owner);
    public bool CanAct => IsCurrent && owner.Player.IsAlive() && GameStates.InGame
        && GameStates.IsInTask && !GameStates.Intro && !GameStates.IsMeeting
        && !GameStates.CalledMeeting && !GameStates.IsEnded
        && CustomWinnerHolder.WinnerTeam == CustomWinner.Default;
    public static bool IsSamePlayer(PlayerControl player, uint expectedNetId)
    {
        if (player == null || player.Data == null || player.Data.Disconnected || player.NetId != expectedNetId) return false;
        var current = PlayerCatch.GetPlayerById(player.PlayerId);
        return current != null && current.NetId == expectedNetId;
    }
}
