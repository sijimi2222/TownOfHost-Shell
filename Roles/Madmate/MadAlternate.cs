using AmongUs.GameOptions;
using TownOfHost.Modules;
using TownOfHost.Roles.AddOns.Common;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Options;

namespace TownOfHost.Roles.Madmate;

public sealed class MadAlternate : RoleBase, ISelfVoter, IKillFlashSeeable, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(MadAlternate), player => new MadAlternate(player), CustomRoles.MadAlternate,
        () => RoleTypes.Engineer, CustomRoleTypes.Madmate, 995000, SetupOptionItem,
        "malt", "#FF1919", (0, -1),
        introSound: () => GetIntroSound(RoleTypes.Impostor), from: From.TownOfHost_Shell, isNewRole: true);

    private static OptionItem optionCanVent;
    private bool selecting;
    private byte? protectedPlayerId;

    public MadAlternate(PlayerControl player) : base(RoleInfo, player, () => HasTask.ForRecompute) { }

    private static void SetupOptionItem()
    {
        optionCanVent = BooleanOptionItem.Create(RoleInfo, 10, GeneralOption.CanVent, false, false);
        OverrideTasksData.Create(RoleInfo, 20, tasks: (true, 3, 3, 3), taskCountRule: new ClampedIntegerValueRule(0, 10, 1));
    }

    public override bool CanClickUseVentButton => optionCanVent.GetBool();
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => optionCanVent.GetBool();

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = optionCanVent.GetBool() ? MadmateVentCooldown.GetFloat() : 255f;
        AURoleOptions.EngineerInVentMaxTime = MadmateVentMaxTime.GetFloat();
    }

    public bool? CheckKillFlash(MurderInfo info) => MadmateCanSeeKillFlash.GetBool();
    public bool? CheckSeeDeathReason(PlayerControl seen) => MadmateCanSeeDeathReason.GetBool();
    public override CustomRoles TellResults(PlayerControl player) => MadTellOpt();
    bool ISelfVoter.CanUseVoted() => false;

    public override void OnStartMeeting()
    {
        selecting = false;
        protectedPlayerId = null;
    }

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Is(voter) || !Player.IsAlive() || !IsTaskFinished
            || !SelfVoteManager.Canuseability() || protectedPlayerId.HasValue)
            return true;

        if (votedForId == Player.PlayerId)
        {
            selecting = !selecting;
            Utils.SendMessage(GetString(selecting ? "MadAlternate.Select" : "MadAlternate.Cancel"), Player.PlayerId);
            return false;
        }

        if (!selecting) return true;
        if (votedForId == MeetingVoteManager.Skip)
        {
            selecting = false;
            Utils.SendMessage(GetString("MadAlternate.Cancel"), Player.PlayerId);
            return false;
        }

        var target = PlayerCatch.GetPlayerById(votedForId);
        if (target == null || target.Data == null || target.Data.Disconnected || !target.IsAlive())
            return false;

        selecting = false;
        protectedPlayerId = votedForId;
        Utils.SendMessage(string.Format(GetString("MadAlternate.Selected"), UtilsName.GetPlayerColor(target, true)), Player.PlayerId);
        return false;
    }

    internal static bool TrySacrificeFor(PlayerControl target)
    {
        // A vote selects the target for the task phase following that meeting.
        if (AmongUsClient.Instance?.AmHost != true || !GameStates.IsInTask || GameStates.CalledMeeting)
            return false;

        foreach (var player in PlayerCatch.AllAlivePlayerControls)
        {
            if (player.PlayerId == target.PlayerId || player.Data == null || player.Data.Disconnected
                || player.GetRoleClass() is not MadAlternate alternate || alternate.protectedPlayerId != target.PlayerId
                || !Amnesia.CheckAbility(player))
                continue;

            var state = player.GetPlayerState();
            var previousReason = state.DeathReason;
            // Consume the selection before the synchronous murder callbacks fire.
            alternate.protectedPlayerId = null;
            state.DeathReason = CustomDeathReason.Sacrifice;
            player.RpcMurderPlayerV2(player, MurderResultFlags.Succeeded);
            if (player.IsAlive())
            {
                state.DeathReason = previousReason;
                alternate.protectedPlayerId = target.PlayerId;
                continue;
            }

            UtilsGameLog.AddGameLog(nameof(MadAlternate),
                string.Format(GetString("MadAlternate.Sacrifice"), UtilsName.GetPlayerColor(player, true), UtilsName.GetPlayerColor(target, true)));
            return true;
        }

        return false;
    }
}

