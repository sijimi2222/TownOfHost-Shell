using System.Collections.Generic;
using AmongUs.GameOptions;
using TownOfHost.Modules;

using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Crewmate;

public sealed class Express : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Express),
            player => new Express(player),
            CustomRoles.Express,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            31400,
            SetupOptionItem,
            "exp",
            "#00ffff",
            (4, 3),
            from: From.TownOfHost_Y
        );

    public Express(PlayerControl player)
        : base(RoleInfo, player)
    {
        speed = OptionSpeed.GetFloat();
    }

    private static OptionItem OptionSpeed;

    enum OptionName
    {
        ExpressSpeed
    }

    private readonly float speed;
    private Dictionary<byte, float> appliedSpeeds;
    private byte appliedPlayerId;

    private static void SetupOptionItem()
    {
        OptionSpeed = FloatOptionItem.Create(RoleInfo, 10, OptionName.ExpressSpeed, new(1.5f, 10f, 0.25f), 3.0f, false)
            .SetValueFormat(OptionFormat.Multiplier);
    }

    // Keep the existing additive speed, once per role instance.
    public override void Add()
    {
        if (appliedSpeeds != null) return;

        var speeds = Main.AllPlayerSpeed;
        var playerId = Player.PlayerId;
        speeds[playerId] += speed;
        appliedSpeeds = speeds;
        appliedPlayerId = playerId;
    }

    public override void OnDestroy()
    {
        var speeds = appliedSpeeds;
        appliedSpeeds = null; // Repeated cleanup cannot remove the contribution twice.

        // Do not touch a replacement match dictionary or dereference a destroyed player.
        if (speeds == null || !ReferenceEquals(speeds, Main.AllPlayerSpeed)
            || !speeds.TryGetValue(appliedPlayerId, out var currentSpeed)) return;

        speeds[appliedPlayerId] = currentSpeed - speed;
        // Queue the existing options sync; never send a new RPC during teardown.
        PlayerGameOptionsSender.SetDirty(appliedPlayerId);
    }
}
