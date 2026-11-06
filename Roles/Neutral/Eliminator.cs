using System;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Attributes;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Neutral;

// Numeric identity and saved settings remain stable across the formal-name change.
public sealed class Eliminator : RoleBase, IKiller, IAdditionalWinner
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(Eliminator), p => new Eliminator(p), CustomRoles.Eliminator,
        () => RoleTypes.Impostor, CustomRoleTypes.Neutral, 993000, SetupOptionItem,
        "eliminator", "#808080", (0, -1), isDesyncImpostor: true,
        from: From.TownOfHost_Shell, isNewRole: true);
    enum OptionName { EliminatorRequiredKills, EliminatorMustSurvive }
    static OptionItem KillCooldown, RequiredKills, MustSurvive;
    // The ordinary additional-winner pass runs before several winner takeovers.
    // Opt in only at the final additive pass; never influence those earlier decisions.
    static bool finalWinnerPass;
    MurderInfo pendingAttempt;
    uint pendingVictimNetId;
    bool disposed, resolvingMisfire, roundClosed;
    internal int SuccessfulCrewKills { get; private set; }

    public Eliminator(PlayerControl player) : base(RoleInfo, player, () => HasTask.False) { }
    static void SetupOptionItem()
    {
        KillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
            new ClampedFloatValueRule(2.5f, 100f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        RequiredKills = IntegerOptionItem.Create(RoleInfo, 11, OptionName.EliminatorRequiredKills,
            new ClampedIntegerValueRule(1, 15, 1), 2, false).SetValueFormat(OptionFormat.Players);
        MustSurvive = BooleanOptionItem.Create(RoleInfo, 12, OptionName.EliminatorMustSurvive, false, false);
    }
    bool IsCurrent => !disposed && !roundClosed && Player != null
        && ReferenceEquals(Player.GetRoleClass(), this);
    static bool Participant(PlayerControl player) => player != null && player.Data != null
        && !player.Data.Disconnected && !player.isDummy && !player.notRealPlayer && !player.IsTestBot()
        && !player.Is(CustomRoles.GM);
    bool CanAct => AmongUsClient.Instance.AmHost && IsCurrent && Participant(Player) && Player.IsAlive()
        && GameStates.InGame && GameStates.IsInTask && !GameStates.Intro && !GameStates.IsMeeting
        && !GameStates.CalledMeeting && !GameStates.IsOutro && CustomWinnerHolder.WinnerTeam == CustomWinner.Default;
    internal static bool IsCrewTarget(PlayerControl target)
    {
        if (!Participant(target)) return false;
        var role = target.GetCustomRole();
        // CountTypes is a population counter, not an alignment (some neutrals count as Crew).
        // Use the custom faction, then exclude faction-changing subroles used by crew-win filtering.
        return role.GetCustomRoleTypes() == CustomRoleTypes.Crewmate && !role.IsImpostorTeam() && !role.IsNeutral()
            && !target.Is(CustomRoles.SKMadmate) && !target.Is(CustomRoles.Jackaldoll)
            && !target.Is(CustomRoles.Tama) && !target.Is(CustomRoles.Amanojaku)
            && !target.IsLovers() && !target.Is(CustomRoles.Faction)
            && !target.Is(CustomRoles.Twins) && !target.Is(CustomRoles.Triplets);
    }
    public float CalculateKillCooldown() => KillCooldown.GetFloat();
    public bool CanUseKillButton() => IsCurrent && Player.IsAlive();
    public bool CanUseImpostorVentButton() => false;
    public bool CanUseSabotageButton() => false;
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        pendingAttempt = null;
        if (info == null) return;
        if (!CanAct || resolvingMisfire || info.AttemptKiller != Player || info.IsSuicide
            || !Participant(info.AttemptTarget) || !info.AttemptTarget.IsAlive())
        { info.DoKill = false; return; }
        if (IsCrewTarget(info.AttemptTarget))
        {
            if (info.DoKill && info.CanKill && !info.IsGuard && info.KillPower > info.GuardPower)
            {
                pendingAttempt = info;
                pendingVictimNetId = info.AttemptTarget.NetId;
            }
            return; // Leave all normal shields/guards and host murder synchronization intact.
        }
        info.DoKill = false; // Cancel the original target before invoking the forced self-murder path.
        resolvingMisfire = true;
        try
        {
            CustomRoleManager.OnCheckMurder(Player, Player, Player, Player,
                true, true, 99, CustomDeathReason.Misfire);
        }
        finally { resolvingMisfire = false; }
    }
    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        if (!AmongUsClient.Instance.AmHost || !IsCurrent || info == null || !ReferenceEquals(info, pendingAttempt)) return;
        pendingAttempt = null; // Consume before any further callback: the same death can never count twice.
        var target = info.AttemptTarget;
        if (info.AttemptKiller != Player || info.IsSuicide || !info.DoKill || !info.CanKill
            || target == null || target.Data == null || target.Data.Disconnected || !target.Data.IsDead
            || target.NetId != pendingVictimNetId || info.AppearanceTarget != target) return;
        SuccessfulCrewKills++;
    }
    public override void OnStartMeeting() => pendingAttempt = null; // Keep the cumulative successful kills.
    public override void OnDestroy() { disposed = true; ClearRound(); }
    void ClearRound() { pendingAttempt = null; SuccessfulCrewKills = 0; resolvingMisfire = false; }
    [GameModuleInitializer]
    public static void Reset()
    {
        finalWinnerPass = false;
        foreach (var role in CustomRoleManager.AllActiveRoles.Values.OfType<Eliminator>())
        { role.ClearRound(); role.roundClosed = false; }
    }
    internal static void EndRound()
    {
        finalWinnerPass = false;
        foreach (var role in CustomRoleManager.AllActiveRoles.Values.OfType<Eliminator>())
        { role.ClearRound(); role.roundClosed = true; }
    }
    internal static bool EligibleWinnerTeam(CustomWinner winner)
    {
        if (winner == CustomWinner.Impostor) return true;
        if (winner is CustomWinner.Default or CustomWinner.None or CustomWinner.Draw or CustomWinner.Crewmate) return false;
        if (!Enum.IsDefined(typeof(CustomRoles), (int)winner)) return false;
        var role = (CustomRoles)winner;
        return role.IsNeutral() || role.IsLovers();
    }
    public bool CheckWin(ref CustomRoles winnerRole)
    {
        if (!finalWinnerPass || !AmongUsClient.Instance.AmHost || !IsCurrent || !Participant(Player)
            || !EligibleWinnerTeam(CustomWinnerHolder.WinnerTeam)
            || SuccessfulCrewKills < RequiredKills.GetInt()
            || (MustSurvive.GetBool() && !Player.IsAlive())
            || CustomWinnerHolder.CantWinPlayerIds.Contains(Player.PlayerId)) return false;
        winnerRole = CustomRoles.Eliminator;
        return true;
    }
    internal static void ApplyAdditionalWinners()
    {
        if (!AmongUsClient.Instance.AmHost || finalWinnerPass) return;
        finalWinnerPass = true;
        try
        {
            foreach (var role in CustomRoleManager.AllActiveRoles.Values.OfType<Eliminator>())
            {
                var winnerRole = CustomRoles.Eliminator;
                if (!role.CheckWin(ref winnerRole)) continue;
                CustomWinnerHolder.WinnerIds.Add(role.Player.PlayerId);
                CustomWinnerHolder.AdditionalWinnerRoles.Add(winnerRole);
            }
        }
        finally { finalWinnerPass = false; }
    }
}
