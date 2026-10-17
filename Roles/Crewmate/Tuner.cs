using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Attributes;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Crewmate;

public sealed class Tuner : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(Tuner), player => new Tuner(player), CustomRoles.Tuner,
        () => RoleTypes.Crewmate, CustomRoleTypes.Crewmate, 97000, SetupOptionItem,
        "tu", "#63C7B2", (0, 7), from: From.TownOfHost_Shell);

    enum OptionName { TunerRequiredTasks, TunerAddedSeconds, TunerUseLimit, TunerAnnounce, TunerStackEffects, TunerAffectSheriff }
    static OptionItem RequiredTasks, AddedSeconds, UseLimit, Announce, StackEffects, AffectSheriff;
    int remainingUses;
    bool reserved, exiledThisMeeting;
    static float activeAddition;
    static bool includeSheriff;
    static bool meetingFinished;
    // SetKillCooldown encodes FailedProtected cooldowns as twice the intended seconds.
    // Keep this metadata separate from the role's base cooldown and from the bonus.
    static readonly HashSet<byte> DoubledCooldowns = new();

    public Tuner(PlayerControl player) : base(RoleInfo, player)
        => remainingUses = UseLimit.GetInt();

    static void SetupOptionItem()
    {
        RequiredTasks = IntegerOptionItem.Create(RoleInfo, 10, OptionName.TunerRequiredTasks, new ClampedIntegerValueRule(1, 10, 1), 10, false)
            .SetValueFormat(OptionFormat.Pieces);
        AddedSeconds = FloatOptionItem.Create(RoleInfo, 11, OptionName.TunerAddedSeconds, new ClampedFloatValueRule(2.5f, 100f, 2.5f), 10f, false)
            .SetValueFormat(OptionFormat.Seconds);
        UseLimit = IntegerOptionItem.Create(RoleInfo, 12, OptionName.TunerUseLimit, new ClampedIntegerValueRule(1, 10, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
        Announce = BooleanOptionItem.Create(RoleInfo, 13, OptionName.TunerAnnounce, false, false);
        StackEffects = BooleanOptionItem.Create(RoleInfo, 14, OptionName.TunerStackEffects, false, false);
        AffectSheriff = BooleanOptionItem.Create(RoleInfo, 15, OptionName.TunerAffectSheriff, false, false);
    }

    [GameModuleInitializer]
    public static void Reset()
    {
        activeAddition = 0f;
        includeSheriff = false;
        meetingFinished = false;
        DoubledCooldowns.Clear();
    }

    // Called independently of living Tuner instances: death/role conversion must not
    // leave a previously activated modifier behind on the next meeting.
    public static void MeetingStarted()
    {
        bool hadEffect = activeAddition > 0f;
        Reset();
        if (hadEffect && AmongUsClient.Instance?.AmHost == true)
            UtilsOption.MarkEveryoneDirtySettings();
    }

    public override void OnStartMeeting()
    {
        reserved = false;
        exiledThisMeeting = false;
        if (AmongUsClient.Instance?.AmHost == true) SyncState();
    }

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (AmongUsClient.Instance?.AmHost != true || !GameStates.IsMeeting
            || voter == null || !Is(voter) || votedForId != Player.PlayerId
            || !Player.IsAlive() || !TownOfHost.Modules.SelfVoteManager.Canuseability()
            || MyTaskState.CompletedTasksCount < RequiredTasks.GetInt() || remainingUses <= 0)
            return true;
        if (!reserved)
        {
            reserved = true;
            SyncState();
            Utils.SendMessage(GetString("TunerReserved"), Player.PlayerId);
        }
        // Follow existing self-vote abilities: clear the command vote and allow a real vote.
        return false;
    }

    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (exiled?.PlayerId == Player.PlayerId) exiledThisMeeting = true;
    }

    // Invoked after every role's AfterMeetingTasks, with exile state already restored.
    public static void MeetingFinished()
    {
        if (AmongUsClient.Instance?.AmHost != true || meetingFinished) return;
        meetingFinished = true;
        int successes = 0;
        foreach (var role in CustomRoleManager.AllActiveRoles.Values)
        {
            if (role is not Tuner tuner || !tuner.reserved) continue;
            tuner.reserved = false;
            var player = tuner.Player;
            if (CustomWinnerHolder.WinnerTeam == CustomWinner.Default
                && player != null && player.Data != null && !player.Data.Disconnected
                && player.IsAlive() && !tuner.exiledThisMeeting && tuner.remainingUses > 0
                && !Main.AfterMeetingDeathPlayers.ContainsKey(player.PlayerId))
            {
                tuner.remainingUses--;
                successes++;
            }
            tuner.SyncState();
        }
        if (successes == 0) return;
        activeAddition = AddedSeconds.GetFloat() * (StackEffects.GetBool() ? successes : 1);
        includeSheriff = AffectSheriff.GetBool();
        UtilsOption.MarkEveryoneDirtySettings();
        if (Announce.GetBool()) Utils.SendMessage(GetString("TunerActivated"));
        Logger.Info($"Activated: successfulTuners={successes}, addedSeconds={activeAddition}", "Tuner");
    }

    public static void SetCooldownEncoding(PlayerControl player, bool doubled)
    {
        if (player == null) return;
        if (doubled && activeAddition > 0f) DoubledCooldowns.Add(player.PlayerId);
        else DoubledCooldowns.Remove(player.PlayerId);
    }

    public static void ApplyCooldown(PlayerControl player, IGameOptions options)
    {
        if (AmongUsClient.Instance?.AmHost != true || activeAddition <= 0f
            || !GameModeManager.IsStandardClass() || player == null || !player.IsAlive()
            || player.Data == null || player.Data.Disconnected
            || player.GetRoleClass() is not IKiller killer || !killer.UsesDirectKillCooldown
            || (!includeSheriff && player.GetRoleClass() is Sheriff)) return;
        // The base dictionary/options remain untouched. Ability/Phantom/Engineer cooldowns
        // are never modified, nor is the independent Phantom kill-tracking timer.
        float factor = DoubledCooldowns.Contains(player.PlayerId) ? 2f : 1f;
        options.SetFloat(FloatOptionNames.KillCooldown,
            options.GetFloat(FloatOptionNames.KillCooldown) + activeAddition * factor);
    }

    void SyncState()
    {
        using var sender = CreateSender(SendOption.Reliable);
        sender.Writer.Write(remainingUses);
        sender.Writer.Write(reserved);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        remainingUses = reader.ReadInt32();
        reserved = reader.ReadBoolean();
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => Utils.ColorString(RoleInfo.RoleColor, $"({remainingUses})");

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!isForMeeting || !Is(seer) || seen.PlayerId != Player.PlayerId || !Player.IsAlive()) return "";
        return GetString(reserved ? "TunerReserved" : "TunerMeetingHint");
    }
}
