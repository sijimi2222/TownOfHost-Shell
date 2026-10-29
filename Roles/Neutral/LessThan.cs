using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Attributes;
using TownOfHost.Modules;
using TownOfHost.Roles.AddOns.Common;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Neutral;

// Independently implemented from the project owner's LessThan specification.
public sealed class LessThan : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(LessThan), p => new LessThan(p), CustomRoles.LessThan, () => RoleTypes.Impostor,
        CustomRoleTypes.Neutral, 40300, SetupOptionItem, "lst", "#D4A5FF", (5, 48),
        isDesyncImpostor: true, countType: CountTypes.LessThan,
        from: From.TownOfHost_Shell, isNewRole: true);
    enum OptionName { LessThanShowNumber, LessThanReroll, LessThanRerollUses,
        LessThanResetEachMeeting, LessThanRerollOthers }
    static OptionItem KillCooldown, ShowNumber, CanVent, Reroll, RerollUses, ResetEachMeeting, RerollOthers;
    static readonly Dictionary<byte, PlayerControl> numberOwners = new();
    static readonly LessThanNumbers numbers = new();
    static readonly HashSet<LessThan> instances = new();
    static byte soloWinner = byte.MaxValue;
    readonly LessThanRerollUsage usage = new();
    MeetingHud currentMeeting;
    bool meetingOpen, acceptedSelfVote, disposed, resolvingSuicide, displayReady, lastShowNumber, pendingMeetingName;
    int privateDisplayNumber;

    public LessThan(PlayerControl player) : base(RoleInfo, player, () => HasTask.False) { }
    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 15, rule: new ClampedIntegerValueRule(1, 30, 1))
            .OptionWin.SetOptionName(() => GetString("LessThanWinPriority"));
        KillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown,
            new ClampedFloatValueRule(2.5f, 100f, 2.5f), 20f, false).SetValueFormat(OptionFormat.Seconds);
        ShowNumber = BooleanOptionItem.Create(RoleInfo, 11, OptionName.LessThanShowNumber, true, false);
        CanVent = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.CanVent, true, false)
            .SetOptionName(() => GetString("LessThanCanVent"));
        Reroll = BooleanOptionItem.Create(RoleInfo, 13, OptionName.LessThanReroll, true, false);
        RerollUses = IntegerOptionItem.Create(RoleInfo, 14, OptionName.LessThanRerollUses,
            new ClampedIntegerValueRule(1, 10, 1), 1, false, parent: Reroll).SetValueFormat(OptionFormat.Times);
        ResetEachMeeting = BooleanOptionItem.Create(RoleInfo, 15, OptionName.LessThanResetEachMeeting, true, false, parent: Reroll);
        RerollOthers = BooleanOptionItem.Create(RoleInfo, 16, OptionName.LessThanRerollOthers, false, false, parent: Reroll);
    }
    bool IsCurrent => !disposed && Player != null && ReferenceEquals(Player.GetRoleClass(), this)
        && PlayerCatch.GetPlayerById(Player.PlayerId) == Player;
    static bool Participant(PlayerControl p) => p != null && p.Data != null && !p.Data.Disconnected
        && !p.isDummy && !p.notRealPlayer && !p.IsTestBot();
    static bool Living(PlayerControl p) => Participant(p) && p.IsAlive() && !p.Is(CustomRoles.GM);
    bool CanAct => AmongUsClient.Instance.AmHost && IsCurrent && Living(Player) && GameStates.InGame
        && GameStates.IsInTask && !GameStates.Intro && !GameStates.IsMeeting && !GameStates.CalledMeeting
        && !GameStates.IsOutro && CustomWinnerHolder.WinnerTeam == CustomWinner.Default;

    public override void Add()
    {
        instances.Add(this);
        if (AmongUsClient.Instance.AmHost) { EnsureNumbers(); RefreshPrivateDisplays(); }
    }
    public override void OnSpawn(bool initialState = false)
    {
        if (AmongUsClient.Instance.AmHost && IsCurrent) { EnsureNumbers(); RefreshPrivateDisplays(); }
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        // Send once when actual play starts, after remote role construction; never
        // broadcast the ledger, and do not poll or resend every physics frame.
        if (!AmongUsClient.Instance.AmHost || !IsCurrent || !GameStates.InGame
            || GameStates.Intro || GameStates.IsOutro) return;
        bool show = ShowNumber.GetBool();
        if (displayReady && lastShowNumber == show) return;
        displayReady = true; lastShowNumber = show;
        SendPrivateDisplay();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }
    static void EnsureNumbers()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        foreach (var player in PlayerCatch.AllPlayerControls)
        {
            if (!Participant(player)) continue;
            if (numberOwners.TryGetValue(player.PlayerId, out var old) && old == player) continue;
            numberOwners[player.PlayerId] = player;
            numbers.Assign(player.PlayerId, Draw);
        }
    }
    static int Draw() => IRandom.Instance.Next(1, 101);
    internal static bool TryGetNumber(PlayerControl player, out int number)
    {
        number = 0;
        return AmongUsClient.Instance.AmHost && Participant(player)
            && numberOwners.TryGetValue(player.PlayerId, out var owner) && owner == player
            && numbers.TryGet(player.PlayerId, out number);
    }
    [GameModuleInitializer]
    public static void Reset()
    {
        foreach (var role in instances) { role.privateDisplayNumber = 0; role.meetingOpen = false; role.acceptedSelfVote = false; role.usage.Reset(); }
        instances.Clear();
        numbers.Clear(); numberOwners.Clear(); soloWinner = byte.MaxValue;
    }
    public override void OnDestroy()
    {
        disposed = true; meetingOpen = false; privateDisplayNumber = 0;
        instances.Remove(this);
        if (instances.Count == 0) Reset();
    }
    // Called when the host starts the game-end sequence, after winner arbitration.
    internal static void EndRound() => Reset();

    public float CalculateKillCooldown() => KillCooldown.GetFloat();
    public bool CanUseKillButton() => IsCurrent && Player.IsAlive();
    public bool CanUseImpostorVentButton() => CanVent.GetBool();
    public bool CanUseSabotageButton() => false;
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        // Forced suicide uses the existing force=true path, which skips this callback.
        if (!CanAct || info.AttemptKiller != Player || !Living(info.AttemptTarget)
            || info.AttemptTarget == Player || resolvingSuicide)
        { info.DoKill = false; return; }
        // Target callbacks and shared guards have already run. GuardPower is
        // finalized just after this callback; do not bypass a pending guard here.
        if (!info.DoKill || !info.CanKill || info.KillPower <= info.GuardPower) return;
        if (!TryGetNumber(Player, out int mine) || !TryGetNumber(info.AttemptTarget, out int theirs))
        { info.DoKill = false; return; }
        int comparison = LessThanNumbers.Compare(mine, theirs);
        if (comparison > 0) return; // Keep the original target and normal murder pipeline.
        info.DoKill = false;
        if (comparison == 0)
        {
            Player.SetKillCooldown(delay: false);
            return;
        }
        resolvingSuicide = true;
        try
        {
            CustomRoleManager.OnCheckMurder(Player, Player, Player, Player,
                true, true, 99, CustomDeathReason.Suicide);
        }
        finally { resolvingSuicide = false; }
    }

    public override void OnStartMeeting()
    {
        if (!AmongUsClient.Instance.AmHost || !IsCurrent || MeetingHud.Instance == null) return;
        if (meetingOpen && currentMeeting == MeetingHud.Instance) return;
        currentMeeting = MeetingHud.Instance; meetingOpen = true; acceptedSelfVote = false;
        usage.BeginMeeting(); EnsureNumbers(); RefreshPrivateDisplays();
    }
    public override void AfterMeetingTasks() { meetingOpen = false; pendingMeetingName = false; currentMeeting = null; }
    // This is invoked only after the existing vote manager has accepted a vote.
    // No vote cancellation, alternate self-vote UI, or ISelfVoter interception.
    internal static void OnAcceptedVote(byte voter, byte originalTarget, byte finalTarget, bool intentional)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsMeeting || GameStates.ExiledAnimate
            || GameStates.IsOutro || CustomWinnerHolder.WinnerTeam != CustomWinner.Default
            || !intentional || originalTarget != voter || finalTarget != voter) return;
        if (PlayerCatch.GetPlayerById(voter)?.GetRoleClass() is not LessThan role
            || !role.IsCurrent || !Living(role.Player) || !role.meetingOpen
            || role.currentMeeting != MeetingHud.Instance || role.acceptedSelfVote) return;
        // Consume the event before eligibility checks; replaying this accepted vote
        // after a setting change must not turn it into a new self-vote.
        role.acceptedSelfVote = true;
        if (!Reroll.GetBool() || !SelfVoteManager.Canuseability() || !Amnesia.CheckAbility(role.Player)
            || !role.usage.TryUse(RerollUses.GetInt(), ResetEachMeeting.GetBool())) return;
        EnsureNumbers();
        foreach (var player in PlayerCatch.AllAlivePlayerControls)
        {
            if (!Participant(player) || (!RerollOthers.GetBool() && player != role.Player)) continue;
            if (TryGetNumber(player, out _)) numbers.Assign(player.PlayerId, Draw);
        }
        RefreshPrivateDisplays();
    }

    static void RefreshPrivateDisplays()
    {
        foreach (var role in instances.ToArray())
        {
            if (!role.IsCurrent) continue;
            role.SendPrivateDisplay();
            role.RefreshLocalMeetingNumber();
            if (GameStates.IsMeeting) { role.pendingMeetingName = true; role.FlushMeetingName(); }
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: role.Player);
        }
    }
    void SendPrivateDisplay()
    {
        if (!AmongUsClient.Instance.AmHost || !IsCurrent) return;
        if (Player == PlayerControl.LocalPlayer || !Player.IsModClient()) return;
        int client = Player.GetClientId();
        if (client < 0) return; // Never fall back to a broadcast for private data.
        bool visible = ShowNumber.GetBool() && TryGetNumber(Player, out _);
        // The general RoleRPCSender broadcasts, so deliberately target this owner.
        var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId,
            (byte)CustomRPC.CustomRoleSync, SendOption.Reliable, client);
        writer.Write(Player.PlayerId);
        writer.Write(visible);
        if (visible && TryGetNumber(Player, out int number)) writer.Write(number);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost || !IsCurrent || Player != PlayerControl.LocalPlayer) return;
        bool visible = reader.ReadBoolean();
        int value = visible ? reader.ReadInt32() : 0;
        if (visible && !LessThanNumbers.InRange(value)) return;
        privateDisplayNumber = value; // Presentation only; never consulted by host logic.
        RefreshLocalMeetingNumber();
    }
    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!IsCurrent || seer != Player || seen != Player || !ShowNumber.GetBool()
            || !GameStates.InGame || GameStates.Intro || GameStates.IsOutro) return "";
        int value = privateDisplayNumber;
        if (AmongUsClient.Instance.AmHost && !TryGetNumber(Player, out value)) return "";
        return LessThanNumbers.InRange(value) ? $" <link=\"LessThanSelfNumber\"><color={RoleInfo.RoleColorCode}>({value})</color></link>" : "";
    }

    internal static string PrivateMeetingMark(PlayerControl seer, PlayerControl seen)
        => seer != null && seer.GetRoleClass() is LessThan role ? role.GetMark(seer, seen, true) : "";
    internal static string ReplaceMeetingNumber(string text, string mark)
        => Regex.Replace(text ?? "", @" <link=""LessThanSelfNumber""><color=#[0-9A-Fa-f]{6}>\(\d{1,3}\)</color></link>", "") + mark;
    void RefreshLocalMeetingNumber()
    {
        if (!IsCurrent || Player != PlayerControl.LocalPlayer || !GameStates.IsMeeting || MeetingHud.Instance == null) return;
        foreach (var area in MeetingHud.Instance.playerStates)
        {
            if (area == null || area.NameText == null || area.PlayerId != Player.PlayerId) continue;
            string next = ReplaceMeetingNumber(area.NameText.text, GetMark(Player, Player, true));
            if (area.NameText.text != next) area.NameText.text = next;
        }
    }
    void FlushMeetingName()
    {
        if (!pendingMeetingName || !AmongUsClient.Instance.AmHost || !IsCurrent || !meetingOpen
            || !GameStates.IsMeeting || ChatUpdatePatch.BlockSendName) return;
        if (!Player.IsModClient() && Player != PlayerControl.LocalPlayer && Player.GetClientId() >= 0)
            NameColorManager.RpcMeetingColorName(Player, Player);
        pendingMeetingName = false;
    }
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    class MeetingDisplayPatch
    {
        static void Postfix()
        {
            // Deferred only while the existing meeting/chat startup blocks renames.
            if (!AmongUsClient.Instance.AmHost || ChatUpdatePatch.BlockSendName) return;
            foreach (var role in instances)
                if (role.pendingMeetingName) role.FlushMeetingName();
        }
    }

    static bool OtherKillingSide(PlayerControl p)
    {
        if (p.GetRoleClass() is LessThan) return false;
        if (p.GetCustomRole().IsImpostor() || p.IsNeutralKiller()) return true;
        if (p.GetCountTypes() is CountTypes.Impostor or CountTypes.Jackal or CountTypes.Remotekiller
            or CountTypes.GrimReaper or CountTypes.MilkyWay or CountTypes.Pavlov or CountTypes.Eater
            or CountTypes.Victim or CountTypes.Monika or CountTypes.StandMaster or CountTypes.Villain
            or CountTypes.Daoshi or CountTypes.Hunter) return true;
        return p.GetCustomRole().IsNeutral() && p.GetRoleClass() is IKiller killer && killer.IsKiller;
    }
    public static bool TryChooseWinner()
    {
        if (instances.Count == 0 || !AmongUsClient.Instance.AmHost || !GameStates.InGame || GameStates.Intro || GameStates.IsOutro
            || CustomWinnerHolder.WinnerTeam is CustomWinner.Draw or CustomWinner.None) return false;
        var alive = PlayerCatch.AllAlivePlayerControls.Where(Living).ToArray();
        var candidates = alive.Where(p => p.GetRoleClass() is LessThan r && r.IsCurrent).ToArray();
        if (candidates.Length == 0 || candidates.Length < alive.Length - candidates.Length
            || alive.Any(OtherKillingSide)) return false;
        var candidate = candidates.Where(p => TryGetNumber(p, out _))
            .OrderByDescending(p => { TryGetNumber(p, out int n); return n; }).ThenBy(p => p.PlayerId).FirstOrDefault();
        if (candidate == null || candidates.Any(p => !TryGetNumber(p, out _))) return false;
        if (CustomWinnerHolder.WinnerTeam == CustomWinner.LessThan) return true;
        if (!CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.LessThan, candidate.PlayerId, AddWin: false)) return false;
        soloWinner = candidate.PlayerId;
        CustomWinnerHolder.NeutralWinnerIds.Add(soloWinner);
        return true;
    }
    internal static void FinalizeSoloWinner()
    {
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.LessThan || soloWinner == byte.MaxValue) return;
        // Never add the entire role to WinnerRoles: exactly one player wins.
        CustomWinnerHolder.WinnerIds.Clear(); CustomWinnerHolder.WinnerIds.Add(soloWinner);
        CustomWinnerHolder.WinnerRoles.Clear(); CustomWinnerHolder.AdditionalWinnerRoles.Clear();
        CustomWinnerHolder.NeutralWinnerIds.Clear(); CustomWinnerHolder.NeutralWinnerIds.Add(soloWinner);
        CustomWinnerHolder.CantWinPlayerIds.Remove(soloWinner);
        CustomWinnerHolder.ForceEveryoneWinsText = false;
    }
}
