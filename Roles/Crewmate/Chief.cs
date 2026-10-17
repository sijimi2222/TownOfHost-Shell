using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;

namespace TownOfHost.Roles.Crewmate;

public sealed class Chief : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(Chief), player => new Chief(player), CustomRoles.Chief,
        () => RoleTypes.Engineer, CustomRoleTypes.Crewmate, 97200, SetupOptionItem,
        "chf", "#4169E1", (1, 10), from: From.TownOfHost_Shell);
    enum OptionName { ChiefTargetMark, ChiefCooldown, ChiefRequiredTasks, ChiefTargetLimit }
    static OptionItem TargetMark, Cooldown, RequiredTasks, TargetLimit;
    static readonly HashSet<Chief> instances = new();
    static readonly Dictionary<byte, string> meetingMarks = new();
    static MeetingHud markedMeeting;
    readonly HashSet<byte> targets = new();
    bool selecting, taskPhaseReady;
    float cooldownLeft;

    public Chief(PlayerControl player) : base(RoleInfo, player)
    {
        instances.Add(this);
        PetActionManager.Register(player.PlayerId, OnPet);
        CustomRoleManager.MarkOthers.Add(TargetMarkForViewer);
    }
    static void SetupOptionItem()
    {
        TargetMark = BooleanOptionItem.Create(RoleInfo, 10, OptionName.ChiefTargetMark, false, false);
        Cooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.ChiefCooldown, new ClampedFloatValueRule(10f, 40f, 2.5f), 20f, false).SetValueFormat(OptionFormat.Seconds);
        RequiredTasks = IntegerOptionItem.Create(RoleInfo, 12, OptionName.ChiefRequiredTasks, new ClampedIntegerValueRule(1, 10, 1), 5, false).SetValueFormat(OptionFormat.Pieces);
        TargetLimit = IntegerOptionItem.Create(RoleInfo, 13, OptionName.ChiefTargetLimit, new ClampedIntegerValueRule(1, 15, 1), 5, false).SetValueFormat(OptionFormat.Players);
    }
    public override bool CanClickUseVentButton => false;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => false;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = 255f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }
    public override void OnSpawn(bool initialState = false) => PetActionManager.Register(Player.PlayerId, OnPet);

    static bool IsLivingPlayer(PlayerControl pc)
        => pc != null && pc.Data != null && !pc.Data.Disconnected && pc.IsAlive() && !pc.Is(CustomRoles.GM);

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsMeeting || voter == null || !Is(voter)
            || !IsLivingPlayer(Player) || !SelfVoteManager.Canuseability()) return true;
        if (votedForId == Player.PlayerId)
        {
            selecting = !selecting;
            SyncState();
            Utils.SendMessage(GetString(selecting ? "ChiefSelectionOn" : "ChiefSelectionOff"), Player.PlayerId);
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
            return false; // Existing CastVotePatch clears the ability vote for this voter.
        }
        if (!selecting) return true;
        if (votedForId == MeetingVoteManager.Skip)
        {
            selecting = false;
            SyncState();
            return true; // Skip remains a normal vote, even while selecting.
        }
        var target = PlayerCatch.GetPlayerById(votedForId);
        if (IsLivingPlayer(target) && targets.Count < TargetLimit.GetInt() && targets.Add(votedForId))
        {
            SyncState();
            UtilsNotifyRoles.NotifyRoles();
        }
        // Duplicates, invalid targets and full selection never become accidental votes.
        return false;
    }
    public override void OnStartMeeting()
    {
        targets.Clear();
        selecting = false;
        taskPhaseReady = false;
        if (AmongUsClient.Instance.AmHost) SyncState();
    }
    public override void AfterMeetingTasks()
    {
        selecting = false;
        taskPhaseReady = IsLivingPlayer(Player);
        if (!taskPhaseReady) targets.Clear();
        if (AmongUsClient.Instance.AmHost) SyncState();
    }
    public override void OnExileWrapUp(NetworkedPlayerInfo exiled, ref bool DecidedWinner)
    {
        if (exiled?.PlayerId == Player.PlayerId) ClearSelection();
    }
    void ClearSelection()
    {
        targets.Clear();
        selecting = taskPhaseReady = false;
        if (AmongUsClient.Instance.AmHost)
        {
            SyncState();
            UtilsNotifyRoles.NotifyRoles();
        }
    }
    public override void OnDead(PlayerControl player) { if (Is(player)) ClearSelection(); }
    public override void OnLeftPlayer(PlayerControl player) { if (player != null && Is(player)) ClearSelection(); }

    // Follow NiceTeleporter's conservative movement exclusions, with moveable/NetTransform
    // checks for zipline and other scripted movement. Skip rather than forcibly interrupt.
    static bool CanTeleport(PlayerControl pc)
    {
        if (!IsLivingPlayer(pc) || !pc.moveable || pc.NetTransform == null || pc.MyPhysics == null
            || pc.MyPhysics.Animations == null || pc.inVent || pc.walkingToVent || pc.onLadder || pc.inMovingPlat
            || pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()
            || pc.MyPhysics.Animations.IsPlayingEnterVentAnimation()) return false;
        if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship
            && Vector2.Distance(pc.GetTruePosition(), new Vector2(7.76f, 8.56f)) <= 1.9f) return false;
        if (pc.GetRoleClass() is HadouHo hh && (hh.IsCharging || hh.ShowBeamMark)) return false;
        if (pc.GetRoleClass() is JackalHadouHo jhh && (jhh.IsCharging || jhh.IsSuperCharging || jhh.ShowBeamMark)) return false;
        if (pc.GetRoleClass() is SheriffHadouHo shh && (shh.IsCharging || shh.ShowBeamMark)) return false;
        return true;
    }
    void OnPet()
    {
        if (!AmongUsClient.Instance.AmHost || CustomWinnerHolder.WinnerTeam != CustomWinner.Default
            || !GameStates.IsInTask || GameStates.CalledMeeting || !taskPhaseReady || cooldownLeft > 0f
            || MyTaskState.CompletedTasksCount < RequiredTasks.GetInt() || !CanTeleport(Player)) return;
        // Capture the transform position once, as existing teleport roles do. No random
        // offset that could place a target across a wall or outside the map.
        Vector2 destination = Player.transform.position;
        int moved = 0;
        foreach (byte id in targets)
        {
            var target = PlayerCatch.GetPlayerById(id);
            if (id == Player.PlayerId || !CanTeleport(target)) continue;
            target.RpcSnapToForced(destination, SendOption.Reliable);
            moved++;
        }
        if (moved == 0) return;
        cooldownLeft = Cooldown.GetFloat();
        SyncState();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        Logger.Info($"chief={Player.PlayerId}, moved={moved}", "Chief");
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive() || cooldownLeft <= 0f) return;
        int oldSeconds = Mathf.CeilToInt(cooldownLeft);
        cooldownLeft = Mathf.Max(0f, cooldownLeft - Time.fixedDeltaTime);
        if (oldSeconds != Mathf.CeilToInt(cooldownLeft))
        {
            SyncState();
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        }
    }
    string TargetMarkForViewer(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!IsLivingPlayer(Player) || seer == null || seen == null || !targets.Contains(seen.PlayerId)) return "";
        return Is(seer) || (TargetMark.GetBool() && seer.PlayerId == seen.PlayerId)
            ? "<color=#4169E1>★</color>" : "";
    }
    // Meeting names bypass task-phase NotifyRoles. Only append/remove our own suffix.
    public static string MeetingMark(PlayerControl seer, PlayerControl seen)
    {
        string result = "";
        foreach (var chief in instances) result += chief.TargetMarkForViewer(seer, seen, true);
        return result;
    }
    static void RefreshMeetingMarks()
    {
        var hud = MeetingHud.Instance;
        if (hud == null || !GameStates.IsMeeting) return;
        if (markedMeeting != hud)
        {
            markedMeeting = hud;
            meetingMarks.Clear();
        }
        foreach (var area in hud.playerStates)
        {
            if (area == null || area.NameText == null) continue;
            string text = area.NameText.text;
            if (meetingMarks.TryGetValue(area.PlayerId, out var previous)
                && previous.Length > 0 && text.EndsWith(previous, System.StringComparison.Ordinal))
                text = text.Substring(0, text.Length - previous.Length);
            string mark = MeetingMark(PlayerControl.LocalPlayer, PlayerCatch.GetPlayerById(area.PlayerId));
            area.NameText.text = text + mark;
            meetingMarks[area.PlayerId] = mark;
        }
        if (AmongUsClient.Instance.AmHost) NameColorManager.RpcMeetingColorName();
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seer == null || seen == null || !Is(seer) || seen.PlayerId != Player.PlayerId || !Player.IsAlive()) return "";
        if (isForMeeting) return string.Format(GetString(selecting ? "ChiefSelecting" : "ChiefMeetingHint"), targets.Count, TargetLimit.GetInt());
        if (MyTaskState.CompletedTasksCount < RequiredTasks.GetInt()) return string.Format(GetString("ChiefTasksLeft"), MyTaskState.CompletedTasksCount, RequiredTasks.GetInt());
        return cooldownLeft > 0f ? string.Format(GetString("ChiefCooldownLeft"), Mathf.CeilToInt(cooldownLeft)) : string.Format(GetString("ChiefReady"), targets.Count);
    }
    void SyncState()
    {
        RefreshMeetingMarks();
        using var sender = CreateSender(SendOption.Reliable);
        sender.Writer.Write(selecting);
        sender.Writer.Write(taskPhaseReady);
        sender.Writer.Write(cooldownLeft);
        sender.Writer.WritePacked(targets.Count);
        foreach (byte id in targets) sender.Writer.Write(id);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        selecting = reader.ReadBoolean();
        taskPhaseReady = reader.ReadBoolean();
        cooldownLeft = reader.ReadSingle();
        targets.Clear();
        int count = reader.ReadPackedInt32();
        for (int i = 0; i < count; i++) targets.Add(reader.ReadByte());
        RefreshMeetingMarks();
    }
    public override void OnDestroy()
    {
        instances.Remove(this);
        if (instances.Count == 0)
        {
            meetingMarks.Clear();
            markedMeeting = null;
        }
        PetActionManager.Unregister(Player.PlayerId);
        CustomRoleManager.MarkOthers.Remove(TargetMarkForViewer);
        targets.Clear();
        selecting = taskPhaseReady = false;
        cooldownLeft = 0f;
    }
}
