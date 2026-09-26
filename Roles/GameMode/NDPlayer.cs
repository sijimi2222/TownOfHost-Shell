using AmongUs.GameOptions;
using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.GameMode;

public sealed class NDPlayer : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(NDPlayer), pc => new NDPlayer(pc), CustomRoles.NDPlayer,
        () => RoleTypes.Crewmate, CustomRoleTypes.Crewmate, 69950, null, "ndplayer", "#03fc4a",
        assignInfo: new RoleAssignInfo(CustomRoles.NDPlayer, CustomRoleTypes.Crewmate)
        { IsInitiallyAssignableCallBack = () => false });

    public NDPlayer(PlayerControl player) : base(RoleInfo, player, () => HasTask.False) { }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(true);
        opt.SetFloat(FloatOptionNames.CrewLightMod, 1.3f);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, 1.3f);
        NaturalDisasters.ApplyDisasterVision(Player.PlayerId, opt);
    }
}
