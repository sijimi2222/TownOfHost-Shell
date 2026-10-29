using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Attributes;
using TownOfHost.Modules;
using TownOfHost.Roles.AddOns.Common;
using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Crewmate;

// Independently implemented from the project owner's specification.
public sealed class Marshall : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(Marshall), p => new Marshall(p), CustomRoles.Marshall, () => RoleTypes.Crewmate,
        CustomRoleTypes.Crewmate, 40200, SetupOptionItem, "msh", "#6EFFC7", (5, 49),
        from: From.TownOfHost_Shell, isNewRole: true);
    static OptionItem AdditionalButtons, TotalLimit, MeetingLimit;
    enum OptionName { MarshallAdditionalButtons, MarshallTotalUses, MarshallMeetingUses }
    internal static readonly MarshallMeetingLedger Meeting = new();
    static readonly HashSet<Marshall> instances = new();
    readonly MarshallUsage usage = new();
    bool disposed, additionalButtonsApplied;
    int remoteTotal, remoteMeeting;
    public Marshall(PlayerControl player) : base(RoleInfo, player) { }

    // Existing counters use zero for infinity. Total zero must instead mean disabled;
    // only this new role's total selector adds -1, formatted with the existing infinity glyph.
    sealed class TotalUseOption : IntegerOptionItem
    {
        internal TotalUseOption() : base(RoleInfo.ConfigId + 11, nameof(OptionName.MarshallTotalUses), 5,
            RoleInfo.Tab, false, new IntegerValueRule(-1, 99, 1)) { }
        public override string GetString() => GetInt() < 0 ? "∞" : ApplyFormat(GetInt().ToString());
        public override string GetValueString(bool coloroff) => GetInt() < 0 ? "∞" : ApplyFormat(GetInt().ToString(), coloroff);
    }
    static void SetupOptionItem()
    {
        AdditionalButtons = IntegerOptionItem.Create(RoleInfo, 10, OptionName.MarshallAdditionalButtons,
            new(0, 99, 1), 2, false).SetValueFormat(OptionFormat.Times);
        TotalLimit = new TotalUseOption();
        TotalLimit.SetParent(RoleInfo.RoleOption).SetParentRole(CustomRoles.Marshall).SetValueFormat(OptionFormat.Times);
        MeetingLimit = IntegerOptionItem.Create(RoleInfo, 12, OptionName.MarshallMeetingUses,
            new(0, 99, 1), 1, false).SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
    }
    static int Total => TotalLimit.GetInt();
    static int PerMeeting => MeetingLimit.GetInt() == 0 ? -1 : MeetingLimit.GetInt();
    static bool Host => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
    static bool Living(PlayerControl p) => p != null && p.Data != null && !p.Data.Disconnected && p.IsAlive();
    bool Current => !disposed && Living(Player) && ReferenceEquals(Player.GetRoleClass(), this);

    [GameModuleInitializer] public static void ResetMeetingState() { Meeting.Reset(); instances.Clear(); }
    public override void Add()
    {
        instances.Add(this);
        if (!Host) return;
        var state = Player.GetPlayerState();
        // Respect the game's global zero-button prohibition; all report/map/sabotage guards stay intact.
        if (!additionalButtonsApplied && state != null)
        {
            if (Main.NormalOptions.NumEmergencyMeetings > 0)
                state.NumberOfRemainingButtons += AdditionalButtons.GetInt();
            additionalButtonsApplied = true;
        }
        SelfVoteManager.AddSelfVotes(Player);
        Player.MarkDirtySettings();
        SyncUsage();
    }
    public override void OnDestroy()
    {
        disposed = true;
        bool last = instances.Remove(this) && instances.Count == 0;
        if (Host) { SelfVoteManager.SetMode(Player, false); if (last) Meeting.Reset(); }
    }
    public override void OnStartMeeting()
    {
        if (!Host) return;
        usage.BeginMeeting(); SelfVoteManager.SetMode(Player, false); SyncUsage();
        if (Current && usage.Available(Total, PerMeeting) && SelfVoteManager.Canuseability())
            Notice(Player, GetString("MarshallMeetingHint"));
    }
    public override void AfterMeetingTasks() { if (Host) SelfVoteManager.SetMode(Player, false); }
    public override void OnDead(PlayerControl player)
    {
        if (Host && player != null && player.PlayerId == Player.PlayerId) SelfVoteManager.SetMode(Player, false);
    }
    public override void OnLeftPlayer(PlayerControl player)
    {
        if (!Host || player == null) return;
        Meeting.Suppressed.Remove(player.PlayerId);
        if (player.PlayerId == Player.PlayerId) SelfVoteManager.SetMode(Player, false);
    }
    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Host || voter == null || !Is(voter) || !Current || !Meeting.Open
            || !GameStates.IsMeeting || GameStates.ExiledAnimate || !SelfVoteManager.Canuseability()) return true;
        if (!usage.Available(Total, PerMeeting)) { SelfVoteManager.SetMode(Player, false); return true; }
        bool selecting = SelfVoteManager.CheckSelfVoteMode(Player, votedForId, out var status);
        if (status == SelfVoteManager.VoteStatus.Self)
        {
            Notice(Player, GetString(selecting ? "MarshallSelectTarget" : "VoteSkillFin")); return false;
        }
        if (!selecting) return true;
        if (status == SelfVoteManager.VoteStatus.Skip)
        {
            SelfVoteManager.SetMode(Player, false); Notice(Player, GetString("VoteSkillFin")); return false;
        }
        var target = PlayerCatch.GetPlayerById(votedForId);
        bool valid = Living(target) && target.PlayerId != Player.PlayerId;
        bool knownZero = valid && MeetingVoteManager.Instance != null
            && MeetingVoteManager.Instance.AllVotes.TryGetValue(votedForId, out var ballot)
            && ballot.HasVoted && ballot.NumVotes <= 0;
        if (!usage.TrySpend(Total, PerMeeting, valid, Meeting.Suppressed.Contains(votedForId), knownZero))
        {
            Notice(Player, GetString("MarshallInvalidTarget")); return false;
        }
        Meeting.Suppressed.Add(votedForId);
        SelfVoteManager.SetMode(Player, false); SyncUsage();
        Notice(Player, GetString("MarshallApplied"));
        return false; // Ability inputs are never cast votes, including on Vanilla clients.
    }
    internal static int AdjustPower(byte voter, int raw)
        => MarshallVoteRules.Adjust(raw, Meeting.Open && Meeting.Suppressed.Contains(voter));
    internal static byte Preferred(IReadOnlyDictionary<byte, int> counts)
    {
        if (!Host || !Meeting.Open || MeetingVoteManager.Instance == null) return MeetingVoteManager.NoVote;
        var ballots = new List<byte>();
        foreach (var role in CustomRoleManager.AllActiveRoles.Values.OfType<Marshall>())
        {
            if (!role.Current || !Amnesia.CheckAbility(role.Player)) continue;
            if (!MeetingVoteManager.Instance.AllVotes.TryGetValue(role.Player.PlayerId, out var vote)
                || !vote.Intentional || vote.VotedFor == MeetingVoteManager.NoVote
                || AdjustPower(vote.Voter, vote.NumVotes) <= 0) continue;
            if (vote.VotedFor != MeetingVoteManager.Skip && !Living(PlayerCatch.GetPlayerById(vote.VotedFor))) continue;
            ballots.Add(vote.VotedFor);
        }
        return MarshallVoteRules.Preferred(counts, ballots);
    }
    internal static void PrepareMeeting() { if (Host) Meeting.Prepare(); }
    internal static void OpenMeeting()
    {
        if (Host) Meeting.Begin(PlayerCatch.AllAlivePlayerControls.Select(p => p.PlayerId));
    }
    internal static void CloseMeeting() { if (Host) Meeting.Close(); }
    internal static void RecordConfirmedKill(byte target)
    {
        if (Host) Meeting.QueueKill(target, GameStates.IsMeeting && !GameStates.ExiledAnimate);
    }
    internal static void ReportMeetingDeath(byte targetId)
    {
        if (!Host || !Meeting.Death(targetId, GameStates.IsMeeting && !GameStates.ExiledAnimate)) return;
        var target = PlayerCatch.GetPlayerById(targetId);
        string unknown = GetString("MarshallUnknown"), name = unknown, role = unknown, reason = unknown;
        try
        {
            name = target?.Data?.PlayerName?.RemoveHtmlTags() ?? unknown;
            var state = PlayerState.GetByPlayerId(targetId);
            if (state != null)
            {
                role = GetString(state.MainRole.ToString());
                if (state.DeathReason != CustomDeathReason.etc) reason = Utils.GetDeathReason(state.DeathReason);
            }
        }
        catch { /* A missing role/reason must never interrupt the death or meeting pipeline. */ }
        if (role.StartsWith("<INVALID:")) role = unknown;
        if (reason.StartsWith("<INVALID:")) reason = unknown;
        string message;
        try { message = string.Format(GetString("MarshallDeathReport"), name, role, reason); }
        catch { message = unknown; }
        foreach (var marshal in CustomRoleManager.AllActiveRoles.Values.OfType<Marshall>().ToArray())
            if (marshal.Current && Amnesia.CheckAbility(marshal.Player)) Notice(marshal.Player, message);
    }
    static void Notice(PlayerControl recipient, string message)
    {
        if (!Host || !Living(recipient)) return;
        try
        {
            string title = "<#6EFFC7>" + GetString("Marshall") + "</color>";
            if (recipient == PlayerControl.LocalPlayer)
            {
                var chat = HudManager.Instance?.Chat;
                if (chat == null) return;
                string original = recipient.Data.PlayerName;
                recipient.SetName(title);
                try { chat.AddChat(recipient, message); } finally { recipient.SetName(original); }
            }
            else if (recipient.GetClientId() >= 0) Utils.SendMessage(message, recipient.PlayerId, title);
        }
        catch { /* Private chat failure must not interrupt voting or death processing. */ }
        // Unknown ClientId is a private-message failure, never a broadcast fallback.
    }
    void SyncUsage()
    {
        if (!Host) return;
        using var sender = CreateSender(SendOption.Reliable);
        sender.Writer.WritePacked(usage.TotalUsed); sender.Writer.WritePacked(usage.MeetingUsed);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        if (Host) return; // Remote clients cannot mutate the authoritative counters.
        remoteTotal = Math.Max(0, reader.ReadPackedInt32()); remoteMeeting = Math.Max(0, reader.ReadPackedInt32());
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        int totalUsed = Host ? usage.TotalUsed : remoteTotal, meetingUsed = Host ? usage.MeetingUsed : remoteMeeting;
        string leftTotal = Total < 0 ? "∞" : Math.Max(0, Total - totalUsed).ToString();
        string leftMeeting = PerMeeting < 0 ? "∞" : Math.Max(0, PerMeeting - meetingUsed).ToString();
        return " <#6EFFC7>(" + leftTotal + "/" + leftMeeting + ")</color>";
    }
}
