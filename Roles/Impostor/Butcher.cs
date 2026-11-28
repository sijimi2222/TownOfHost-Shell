using AmongUs.GameOptions;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

public sealed class Butcher : RoleBase, IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(Butcher),
        player => new Butcher(player),
        CustomRoles.Butcher,
        () => RoleTypes.Impostor,
        CustomRoleTypes.Impostor,
        1000000,
        SetupOptionItem,
        "bch",
        "#FF1919",
        OptionSort: (0, -11),
        from: From.TownOfHost_Shell
    );

    private static OptionItem KillCooldownOption;
    private static OptionItem MovementLockOption;
    private readonly RoleAbilityContext context;
    private LateTask releaseTask;

    public Butcher(PlayerControl player) : base(RoleInfo, player)
    {
        context = new RoleAbilityContext(this);
    }

    internal bool IsMovementLocked => releaseTask != null;

    private static void SetupOptionItem()
    {
        KillCooldownOption = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0.5f, 180f, 0.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        MovementLockOption = FloatOptionItem.Create(RoleInfo, 11, "ButcherMovementLockTime", new(0.5f, 30f, 0.5f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public float CalculateKillCooldown() => KillCooldownOption.GetFloat();

    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        if (!context.IsCurrent || !Player.IsAlive() || !info.CanKill || !info.DoKill || info.IsSuicide
            || info.AttemptTarget == null || info.AppearanceTarget == null
            || info.AttemptTarget.PlayerId != info.AppearanceTarget.PlayerId
            || info.AttemptTarget.Data?.IsDead != true)
            return;

        releaseTask?.CallStop();
        releaseTask = new LateTask(ReleaseMovement, MovementLockOption.GetFloat(), "Butcher Release Movement");
        Player.MarkDirtySettings();
    }

    private void ReleaseMovement()
    {
        releaseTask = null;
        if (context.IsCurrent && GameStates.InGame && !GameStates.IsEnded)
            Player.MarkDirtySettings();
    }

    public override void OnDestroy()
    {
        if (releaseTask == null) return;
        releaseTask.CallStop();
        ReleaseMovement();
    }
}
