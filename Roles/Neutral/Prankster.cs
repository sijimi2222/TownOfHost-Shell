using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Prankster : RoleBase, ISelfVoter, IAdditionalWinner, ISystemTypeUpdateHook
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(Prankster), player => new Prankster(player), CustomRoles.Prankster,
        () => RoleTypes.Crewmate, CustomRoleTypes.Neutral, 97400, SetupOptionItem,
        "prank", "#C86A18", (6, 4), from: From.TownOfHost_Shell);

    enum OptionName { PranksterRequiredCandy, PranksterCantFixSabotage, PranksterAdditionalWin }
    static OptionItem RequiredCandy, CantFixSabotage, AdditionalWin;
    const string CurseMark = "<color=#C86A18>✦</color>";
    static MeetingHud jackMeeting;
    static byte jackId = byte.MaxValue;
    static float meetingRefreshTimer;
    static bool meetingNamesDirty;
    readonly CandyState state = new();
    bool winTriggered;

    public Prankster(PlayerControl player) : base(RoleInfo, player)
        => CustomRoleManager.MarkOthers.Add(GetCurseMark);

    static void SetupOptionItem()
    {
        // Register with the existing priority resolver, fixed at 15 and hidden from the options UI.
        SoloWinOption.Create(RoleInfo, 9, show: () => false, defo: 15,
            rule: new ClampedIntegerValueRule(15, 15, 1));
        RequiredCandy = IntegerOptionItem.Create(RoleInfo, 10, OptionName.PranksterRequiredCandy,
            new ClampedIntegerValueRule(1, 15, 1), 5, false);
        CantFixSabotage = BooleanOptionItem.Create(RoleInfo, 11, OptionName.PranksterCantFixSabotage, true, false);
        AdditionalWin = BooleanOptionItem.Create(RoleInfo, 12, OptionName.PranksterAdditionalWin, false, false);
    }

    // Per-role state: a refusal in meeting N expires after meeting N+1, not meeting N.
    internal sealed class CandyState
    {
        public readonly HashSet<byte> Requested = new();
        public readonly Dictionary<byte, int> Curses = new();
        public int Candy, Meeting;
        public bool Used, Waiting, MeetingOpen, WinFlag, Revealed;
        public byte PendingTarget = byte.MaxValue;
        public void BeginMeeting()
        {
            Meeting++;
            MeetingOpen = true;
            Used = false;
            ClearPending();
        }
        public bool Request(byte target, bool isJack)
        {
            if (!MeetingOpen || Used || !Requested.Add(target)) return false;
            Used = true;
            Waiting = !isJack;
            PendingTarget = Waiting ? target : byte.MaxValue;
            return true;
        }
        public bool Answer(byte target, int answer, int required)
        {
            if (!MeetingOpen || !Waiting || target != PendingTarget || (answer != 1 && answer != 2)) return false;
            ClearPending(); // Consume before sending any messages or evaluating a winner.
            if (answer == 1) { Candy++; WinFlag |= Candy >= required; }
            else Curses[target] = Meeting + 1;
            return true;
        }
        public void ClearPending() { Waiting = false; PendingTarget = byte.MaxValue; }
        public void Cancel() { ClearPending(); Curses.Clear(); MeetingOpen = false; }
    }

    [Attributes.GameModuleInitializer]
    public static void ResetJack()
    {
        jackId = byte.MaxValue;
        jackMeeting = null;
        meetingRefreshTimer = 0f;
        meetingNamesDirty = false;
    }

    static bool IsParticipant(PlayerControl player) => player != null && player.Data != null
        && player.PlayerId <= 15 && !player.Data.Disconnected
        && !player.isDummy && !player.notRealPlayer && !player.IsTestBot();
    static bool IsLivingPlayer(PlayerControl player) => IsParticipant(player)
        && player.IsAlive() && !player.Is(CustomRoles.GM);
    static bool CanAnswerNow => GameStates.IsMeeting && !GameStates.ExiledAnimate
        && !GameStates.IsOutro && CustomWinnerHolder.WinnerTeam == CustomWinner.Default;

    public override void OnStartMeeting()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        state.BeginMeeting();
        SelfVoteManager.SetMode(Player, false);
        if (jackMeeting != MeetingHud.Instance)
        {
            jackMeeting = MeetingHud.Instance;
            var candidates = new List<byte>();
            foreach (var player in PlayerCatch.AllPlayerControls)
                if (IsLivingPlayer(player)) candidates.Add(player.PlayerId);
            jackId = candidates.Count == 0 ? byte.MaxValue : candidates[IRandom.Instance.Next(candidates.Count)];
        }
        if (!IsLivingPlayer(Player)) state.Cancel();
        SyncState();
        // MeetingAddMessage is aggregated into a public notice; this hint is private.
        if (IsLivingPlayer(Player) && SelfVoteManager.Canuseability())
            SendNotice(Player, GetString("PranksterMeetingHint"));
    }

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!AmongUsClient.Instance.AmHost || voter == null || !Is(voter) || !IsLivingPlayer(Player)
            || !CanAnswerNow || !state.MeetingOpen || state.Used || !SelfVoteManager.Canuseability()) return true;
        bool selecting = SelfVoteManager.CheckSelfVoteMode(Player, votedForId, out var status);
        if (status == SelfVoteManager.VoteStatus.Self)
        {
            SendNotice(Player, GetString(selecting ? "PranksterSelectTarget" : "VoteSkillFin"));
            return false;
        }
        if (!selecting) return true;
        if (status == SelfVoteManager.VoteStatus.Skip)
        {
            SelfVoteManager.SetMode(Player, false);
            SendNotice(Player, GetString("VoteSkillFin"));
        }
        else
        {
            var target = PlayerCatch.GetPlayerById(votedForId);
            if (!IsLivingPlayer(target)) return false;
            if (state.Requested.Contains(votedForId))
                SendNotice(Player, GetString("PranksterAlreadyAsked"));
            else if (HasPendingRequest(votedForId))
                SendNotice(Player, GetString("PranksterTargetBusy"));
            else if (state.Request(votedForId, votedForId == jackId))
            {
                SelfVoteManager.SetMode(Player, false);
                if (votedForId == jackId) state.Revealed = true;
                SyncState();
                if (votedForId == jackId)
                {
                    RevealToEveryone();
                }
                else SendNotice(target, GetString("PranksterCandyPrompt"));
            }
        }
        return false; // Self votes, target selection and cancellation are never ordinary votes.
    }

    public static bool IsRevealed(PlayerControl player)
        => player != null && CustomRoleManager.AllActiveRoles.TryGetValue(player.PlayerId, out var role)
            && role is Prankster prankster && prankster.state.Revealed;

    void RevealToEveryone()
    {
        SendNotice(Player, GetString("PranksterJackCaught"));
        string message = string.Format(GetString("PranksterJackReveal"), Player.GetRealName(isMeeting: true));
        foreach (var participant in PlayerCatch.AllPlayerControls)
        {
            if (!IsParticipant(participant)) continue; // Include dead players and GM; exclude dummy/disconnected.
            NameColorManager.Add(participant.PlayerId, Player.PlayerId, RoleInfo.RoleColorCode);
            SendNotice(participant, message);
        }
    }

    // Reuse ChatManager's standard SetName -> SendChat -> SetName pattern, addressed to the recipient.
    // Speaking as the recipient also avoids the meeting's per-client alive/dead chat masking.
    static void SendNotice(PlayerControl recipient, string message)
    {
        if (!AmongUsClient.Instance.AmHost || !IsParticipant(recipient)) return;
        string title = $"<{Main.ModColor}>{GetString("DefaultSystemMessageTitle")}</color>";
        string body = "<align=\"left\"><size=70%>" + message + "</size>";
        if (recipient == PlayerControl.LocalPlayer)
        {
            var chat = HudManager.Instance?.Chat;
            if (chat == null) return;
            string originalName = recipient.Data.PlayerName;
            recipient.SetName(title);
            try { chat.AddChat(recipient, body); }
            finally { recipient.SetName(originalName); }
            return;
        }
        int clientId = recipient.GetClientId();
        if (clientId < 0) return; // Never let an unavailable private recipient become a broadcast.
        string restoredName = recipient.GetRealName(isMeeting: true).ApplyNameColorData(recipient, recipient, true);
        if (GameStates.IsMeeting)
            restoredName += TownOfHost.Roles.Crewmate.Chief.MeetingMark(recipient, recipient)
                + GetCurseMark(recipient, recipient, true);
        else if (Main.LastNotifyNames.TryGetValue((recipient.PlayerId, recipient.PlayerId), out var lastName))
            restoredName = lastName;
        var sender = CustomRpcSender.Create("PranksterNotice", SendOption.Reliable);
        sender.StartMessage(clientId);
        sender.StartRpc(recipient.NetId, RpcCalls.SetName).Write(recipient.Data.NetId).Write(title).EndRpc();
        sender.StartRpc(recipient.NetId, RpcCalls.SendChat).Write(body).EndRpc();
        sender.StartRpc(recipient.NetId, RpcCalls.SetName).Write(recipient.Data.NetId).Write(restoredName).EndRpc();
        sender.EndMessage();
        sender.SendMessage();
    }

    static bool HasPendingRequest(byte target)
    {
        foreach (var role in CustomRoleManager.AllActiveRoles.Values)
            if (role is Prankster prankster && prankster.state.Waiting
                && prankster.state.PendingTarget == target && IsLivingPlayer(prankster.Player)) return true;
        return false;
    }

    public static bool TryParseCandyCommand(string text, out int answer)
    {
        answer = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var args = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length < 2 || !args[0].Equals("/cmd", StringComparison.OrdinalIgnoreCase)
            || !args[1].TrimStart('/').Equals("candy", StringComparison.OrdinalIgnoreCase)) return false;
        if (args.Length == 3 && (args[2] == "1" || args[2] == "2")) answer = args[2] == "1" ? 1 : 2;
        return true;
    }

    public static bool TryHandleCandyCommand(PlayerControl player, string text)
    {
        if (!TryParseCandyCommand(text, out int answer)) return false;
        if (!AmongUsClient.Instance.AmHost || player == null) return true;
        if (answer == 0) { SendNotice(player, GetString("PranksterCandyUsage")); return true; }
        if (CanAnswerNow && IsLivingPlayer(player))
        {
            foreach (var role in CustomRoleManager.AllActiveRoles.Values)
            {
                if (role is not Prankster prankster || !IsLivingPlayer(prankster.Player)) continue;
                if (!prankster.state.Answer(player.PlayerId, answer, RequiredCandy.GetInt())) continue;
                prankster.SyncState();
                if (answer == 1)
                {
                    SendNotice(prankster.Player, string.Format(GetString("PranksterCandyReceived"), prankster.state.Candy,
                        RequiredCandy.GetInt()));
                    prankster.TrySoloWin();
                }
                else SendNotice(player, GetString("PranksterCursed"));
                return true;
            }
        }
        SendNotice(player, GetString("PranksterNoRequest"));
        return true;
    }

    void TrySoloWin()
    {
        if (!AmongUsClient.Instance.AmHost || AdditionalWin.GetBool() || !state.WinFlag
            || !IsLivingPlayer(Player) || winTriggered || CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;
        if (!CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Prankster, Player.PlayerId, AddWin: false)) return;
        winTriggered = true;
        CustomWinnerHolder.NeutralWinnerIds.Add(Player.PlayerId);
        if (GameManager.Instance != null) GameManager.Instance.LogicFlow.CheckEndCriteria();
    }

    public bool CheckWin(ref CustomRoles winnerRole)
        => AdditionalWin.GetBool() && state.WinFlag && IsLivingPlayer(Player);

    // Sabotage wins and later winner takeovers can bypass/reset the ordinary additional-winner pass.
    public static void ApplyAdditionalWinners()
    {
        if (AdditionalWin?.GetBool() != true
            || CustomWinnerHolder.WinnerTeam is CustomWinner.Default or CustomWinner.Draw or CustomWinner.None) return;
        foreach (var role in CustomRoleManager.AllActiveRoles.Values)
        {
            if (role is not Prankster prankster) continue;
            var winnerRole = CustomRoles.Prankster;
            if (!prankster.CheckWin(ref winnerRole)) continue;
            CustomWinnerHolder.WinnerIds.Add(prankster.Player.PlayerId);
            CustomWinnerHolder.AdditionalWinnerRoles.Add(winnerRole);
        }
    }
    public override string GetProgressText(bool comms = false, bool gamelog = false)
        => Utils.ColorString(RoleInfo.RoleColor, $"({state.Candy}/{RequiredCandy.GetInt()})");

    // Called after exile death is committed and existing roles have queued their meeting deaths.
    public static void MeetingEnded()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        foreach (var role in CustomRoleManager.AllActiveRoles.Values)
        {
            if (role is not Prankster prankster || !prankster.state.MeetingOpen) continue;
            prankster.FinishMeeting();
        }
        jackMeeting = null;
        jackId = byte.MaxValue;
    }
    void FinishMeeting()
    {
        if (!state.MeetingOpen) return;
        SelfVoteManager.SetMode(Player, false);
        if (!IsLivingPlayer(Player) || Main.AfterMeetingDeathPlayers.ContainsKey(Player.PlayerId)) state.Cancel();
        else
        {
            // No reply is a refusal, once only, with the same next-meeting deadline as /cmd candy 2.
            if (state.Waiting)
            {
                var target = PlayerCatch.GetPlayerById(state.PendingTarget);
                if (IsLivingPlayer(target) && !Main.AfterMeetingDeathPlayers.ContainsKey(target.PlayerId)
                    && state.Answer(target.PlayerId, 2, RequiredCandy.GetInt()))
                    SendNotice(target, GetString("PranksterCursed"));
            }
            var expired = new List<byte>();
            foreach (var curse in state.Curses)
            {
                var target = PlayerCatch.GetPlayerById(curse.Key);
                if (!IsLivingPlayer(target)) { expired.Add(curse.Key); continue; }
                if (curse.Value > state.Meeting) continue;
                expired.Add(curse.Key);
                if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default
                    || Main.AfterMeetingDeathPlayers.ContainsKey(curse.Key)) continue;
                target.SetRealKiller(Player);
                MeetingHudPatch.TryAddAfterMeetingDeathPlayers(CustomDeathReason.Spell, curse.Key);
                Logger.Info($"Curse expired: owner={Player.PlayerId}, target={curse.Key}, meeting={state.Meeting}", "Prankster");
            }
            foreach (byte id in expired) state.Curses.Remove(id);
        }
        state.ClearPending();
        state.MeetingOpen = false;
        SyncState();
    }

    public static string GetCurseMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!IsLivingPlayer(seen)) return "";
        foreach (var role in CustomRoleManager.AllActiveRoles.Values)
            if (role is Prankster prankster && IsLivingPlayer(prankster.Player)
                && prankster.state.Curses.ContainsKey(seen.PlayerId)) return CurseMark;
        return "";
    }

    void CancelIfUnavailable(PlayerControl unavailable)
    {
        if (unavailable == null) return;
        bool changed = false;
        if (Is(unavailable))
        {
            changed = state.Waiting || state.Curses.Count > 0 || state.MeetingOpen;
            state.Cancel();
            SelfVoteManager.SetMode(Player, false);
        }
        else
        {
            changed = state.Curses.Remove(unavailable.PlayerId);
            if (state.Waiting && state.PendingTarget == unavailable.PlayerId) { state.ClearPending(); changed = true; }
        }
        if (changed && AmongUsClient.Instance.AmHost) SyncState();
    }
    public override void OnDead(PlayerControl player) => CancelIfUnavailable(player);
    public override void OnLeftPlayer(PlayerControl player) => CancelIfUnavailable(player);
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (exiled?.PlayerId == Player.PlayerId) CancelIfUnavailable(Player);
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (AmongUsClient.Instance.AmHost && !IsLivingPlayer(Player)) CancelIfUnavailable(Player);
    }

    void SyncState()
    {
        using (var sender = CreateSender(SendOption.Reliable))
        {
            sender.Writer.WritePacked(state.Candy);
            sender.Writer.Write(state.Used);
            sender.Writer.Write(state.Waiting);
            sender.Writer.Write(state.PendingTarget);
            sender.Writer.Write(state.WinFlag);
            sender.Writer.Write(state.Revealed);
            sender.Writer.WritePacked(state.Meeting);
            sender.Writer.Write(state.MeetingOpen);
            sender.Writer.WritePacked(state.Requested.Count);
            foreach (byte id in state.Requested) sender.Writer.Write(id);
            sender.Writer.WritePacked(state.Curses.Count);
            foreach (var curse in state.Curses) { sender.Writer.Write(curse.Key); sender.Writer.WritePacked(curse.Value); }
        }
        RefreshMeetingMarks();
        if (GameStates.IsMeeting)
        {
            meetingNamesDirty = true;
            FlushMeetingNames();
        }
        else UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        state.Candy = reader.ReadPackedInt32();
        state.Used = reader.ReadBoolean(); state.Waiting = reader.ReadBoolean(); state.PendingTarget = reader.ReadByte();
        state.WinFlag = reader.ReadBoolean(); state.Revealed = reader.ReadBoolean();
        state.Meeting = reader.ReadPackedInt32(); state.MeetingOpen = reader.ReadBoolean();
        state.Requested.Clear();
        int count = reader.ReadPackedInt32();
        for (int i = 0; i < count; i++) state.Requested.Add(reader.ReadByte());
        state.Curses.Clear(); count = reader.ReadPackedInt32();
        for (int i = 0; i < count; i++) state.Curses.Add(reader.ReadByte(), reader.ReadPackedInt32());
        RefreshMeetingMarks();
    }

    static void RefreshMeetingMarks()
    {
        var hud = MeetingHud.Instance;
        if (hud == null || !GameStates.IsMeeting) return;
        foreach (var area in hud.playerStates)
        {
            if (area == null || area.NameText == null) continue;
            // Only remove our exact mark; keep name colours and other roles' marks intact.
            var target = PlayerCatch.GetPlayerById(area.PlayerId);
            string text = area.NameText.text.Replace(CurseMark, "");
            if (IsRevealed(target))
            {
                string name = target.GetRealName(isMeeting: true).RemoveColorTags();
                string coloredName = Utils.ColorString(RoleInfo.RoleColor, name);
                int index = name.Length == 0 ? -1 : text.IndexOf(name, StringComparison.Ordinal);
                if (index >= 0 && !text.Contains(coloredName))
                    text = text.Remove(index, name.Length).Insert(index, coloredName);
            }
            text += GetCurseMark(PlayerControl.LocalPlayer, target, true);
            if (area.NameText.text != text) area.NameText.text = text;
        }
    }
    static void FlushMeetingNames()
    {
        if (!meetingNamesDirty || !AmongUsClient.Instance.AmHost || ChatUpdatePatch.BlockSendName) return;
        NameColorManager.RpcMeetingColorName();
        meetingNamesDirty = false;
    }
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    class MeetingStatePatch
    {
        static void Postfix()
        {
            bool present = false;
            foreach (var role in CustomRoleManager.AllActiveRoles.Values)
                if (role is Prankster) { present = true; break; }
            if (!present && !meetingNamesDirty) return;
            meetingRefreshTimer += Time.deltaTime;
            if (meetingRefreshTimer < .25f) return;
            meetingRefreshTimer = 0f;
            // Meeting kills using exile/SetDead may bypass OnDead. Clear them without any per-frame RPC.
            if (AmongUsClient.Instance.AmHost)
                foreach (var role in CustomRoleManager.AllActiveRoles.Values)
                    if (role is Prankster prankster)
                    {
                        if (!IsLivingPlayer(prankster.Player)) prankster.CancelIfUnavailable(prankster.Player);
                        else if (prankster.state.Waiting && !IsLivingPlayer(PlayerCatch.GetPlayerById(prankster.state.PendingTarget)))
                        {
                            prankster.state.ClearPending();
                            prankster.SyncState();
                        }
                    }
            RefreshMeetingMarks();
            FlushMeetingNames();
        }
    }

    bool ISystemTypeUpdateHook.UpdateReactorSystem(ReactorSystemType system, byte amount) => !CantFixSabotage.GetBool();
    bool ISystemTypeUpdateHook.UpdateHeliSabotageSystem(HeliSabotageSystem system, byte amount) => !CantFixSabotage.GetBool();
    bool ISystemTypeUpdateHook.UpdateLifeSuppSystem(LifeSuppSystemType system, byte amount) => !CantFixSabotage.GetBool();
    bool ISystemTypeUpdateHook.UpdateHqHudSystem(HqHudSystemType system, byte amount) => !CantFixSabotage.GetBool();
    bool ISystemTypeUpdateHook.UpdateSwitchSystem(SwitchSystem system, byte amount) => !CantFixSabotage.GetBool();
    bool ISystemTypeUpdateHook.UpdateHudOverrideSystem(HudOverrideSystemType system, byte amount) => !CantFixSabotage.GetBool();

    public override void OnDestroy()
    {
        state.Cancel();
        state.Requested.Clear();
        bool otherPrankster = false;
        foreach (var role in CustomRoleManager.AllActiveRoles.Values)
            if (role is Prankster && role != this) { otherPrankster = true; break; }
        if (!otherPrankster) { CustomRoleManager.MarkOthers.Remove(GetCurseMark); ResetJack(); }
        RefreshMeetingMarks();
        if (AmongUsClient.Instance.AmHost && GameStates.IsMeeting)
        {
            meetingNamesDirty = true;
            FlushMeetingNames();
        }
    }
}
