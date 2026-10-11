using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Attributes;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Neutral;

namespace TownOfHost.Roles.Madmate;

public sealed class MadCleaner : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(MadCleaner), player => new MadCleaner(player), CustomRoles.MadCleaner,
        () => RoleTypes.Engineer, CustomRoleTypes.Madmate, 97100, SetupOptionItem,
        "mcl", "#FF0000", (1, 10), from: From.TownOfHost_Shell);

    enum OptionName { MadCleanerCooldown, MadCleanerArrows, MadCleanerVent, MadCleanerSeeImpostors, MadCleanerRequiredCount }
    static OptionItem Cooldown, ShowArrows, CanVent, SeeImpostors, RequiredCount;
    // Same default close-body distance as Evolver; no extra range option.
    const float CleaningRange = 1.5f;
    static readonly HashSet<byte> CleanedBodies = new();
    readonly Dictionary<byte, Vector2> bodyPositions = new();
    readonly Dictionary<byte, Vector2> nextPositions = new();
    readonly List<byte> removedIds = new();
    public int CleanCount { get; private set; }
    bool revealedToImpostors;
    float cooldownLeft, scanTimer;

    public MadCleaner(PlayerControl player) : base(RoleInfo, player)
        => PetActionManager.Register(player.PlayerId, OnPet);

    static void SetupOptionItem()
    {
        Cooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.MadCleanerCooldown, new ClampedFloatValueRule(10f, 40f, 5f), 20f, false).SetValueFormat(OptionFormat.Seconds);
        ShowArrows = BooleanOptionItem.Create(RoleInfo, 11, OptionName.MadCleanerArrows, true, false);
        CanVent = BooleanOptionItem.Create(RoleInfo, 12, OptionName.MadCleanerVent, false, false);
        SeeImpostors = BooleanOptionItem.Create(RoleInfo, 13, OptionName.MadCleanerSeeImpostors, true, false);
        RequiredCount = IntegerOptionItem.Create(RoleInfo, 14, OptionName.MadCleanerRequiredCount, new ClampedIntegerValueRule(1, 5, 1), 2, false).SetValueFormat(OptionFormat.Pieces);
    }

    [GameModuleInitializer]
    public static void ResetCleanedBodies() => CleanedBodies.Clear();
    public static bool IsCleaned(byte id) => CleanedBodies.Contains(id);

    public static bool KnowsAlly(PlayerControl seer, PlayerControl target)
    {
        if (seer == null || target == null || seer == target) return false;
        if (seer.GetRoleClass() is MadCleaner && SeeImpostors.GetBool() && target.Is(CustomRoleTypes.Impostor)) return true;
        return seer.Is(CustomRoleTypes.Impostor) && target.GetRoleClass() is MadCleaner cleaner && cleaner.revealedToImpostors;
    }

    public override bool CanClickUseVentButton => CanVent.GetBool();
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => CanVent.GetBool();
    public override void ApplyGameOptions(IGameOptions opt)
    {
        // The role remains Engineer. Host-side OnEnterVent also enforces the OFF state.
        if (!CanVent.GetBool()) AURoleOptions.EngineerCooldown = 255f;
    }
    public override void OnSpawn(bool initialState = false)
    {
        PetActionManager.Register(Player.PlayerId, OnPet);
        if (AmongUsClient.Instance.AmHost) SyncState();
    }

    static bool IsAvailable(DeadBody body)
    {
        if (body == null || body.Reported || !body.gameObject.activeInHierarchy) return false;
        byte id = body.ParentId;
        if (IsCleaned(id) || Tiger.IsPredatedBody(id) || Vulture.IsEatenBody(id)
            || Eater.IsEatenBody(id) || Evolver.IsEatenBody(id)) return false;
        var target = PlayerCatch.GetPlayerById(id);
        return target != null && target.Data != null && target.Data.IsDead && !target.Data.Disconnected;
    }

    void OnPet()
    {
        if (!AmongUsClient.Instance.AmHost || CustomWinnerHolder.WinnerTeam != CustomWinner.Default || !GameStates.IsInTask || GameStates.CalledMeeting
            || !Player.IsAlive() || Player.Data == null || Player.Data.Disconnected || cooldownLeft > 0f
            || Player.inVent || Player.walkingToVent || Player.onLadder || Player.inMovingPlat) return;
        DeadBody nearest = null;
        float bestDistance = CleaningRange;
        Vector2 position = Player.GetTruePosition();
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            if (!IsAvailable(body)) continue;
            float distance = Vector2.Distance(position, body.TruePosition);
            if (distance > bestDistance || PhysicsHelpers.AnythingBetween(Player.Collider, position, body.TruePosition, Constants.ShipOnlyMask, false)) continue;
            nearest = body;
            bestDistance = distance;
        }
        // Revalidate immediately before committing. Unity host callbacks run serially;
        // an accepted report already leaves the task phase before another pet is handled.
        if (!IsAvailable(nearest) || !GameStates.IsInTask || GameStates.CalledMeeting) return;
        if (!CleanedBodies.Add(nearest.ParentId)) return;
        nearest.Reported = true;
        CleanCount++;
        if (CleanCount >= RequiredCount.GetInt()) revealedToImpostors = true;
        cooldownLeft = Cooldown.GetFloat();
        // Approved specification: keep the visible body, reject all later reports.
        // No local Destroy or unsupported Vanilla body-deletion RPC.
        foreach (var role in CustomRoleManager.AllActiveRoles.Values)
            if (role is MadCleaner cleaner && cleaner.RefreshArrows()) cleaner.SyncState();
        SyncState();
        UtilsNotifyRoles.NotifyRoles();
        UtilsGameLog.AddGameLog("MadCleaner", string.Format(GetString("MadCleanerCleanLog"), UtilsName.GetPlayerColor(Player), nearest.ParentId));
        Logger.Info($"Cleaned body={nearest.ParentId}, cleaner={Player.PlayerId}, count={CleanCount}", "MadCleaner");
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!Player.IsAlive() || Player.Data == null || Player.Data.Disconnected)
        {
            if (ClearArrows()) SyncState();
            return;
        }
        int previous = Mathf.CeilToInt(cooldownLeft);
        cooldownLeft = Mathf.Max(0f, cooldownLeft - Time.fixedDeltaTime);
        bool changed = previous != Mathf.CeilToInt(cooldownLeft);
        scanTimer -= Time.fixedDeltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = 0.5f;
            changed |= RefreshArrows();
        }
        if (changed)
        {
            SyncState();
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        }
    }

    bool RefreshArrows()
    {
        nextPositions.Clear();
        if (ShowArrows.GetBool() && Player.IsAlive() && Player.Data != null && !Player.Data.Disconnected && GameStates.IsInTask && !GameStates.CalledMeeting)
            foreach (var body in Object.FindObjectsOfType<DeadBody>())
                if (IsAvailable(body)) nextPositions[body.ParentId] = body.TruePosition;
        bool changed = false;
        removedIds.Clear();
        foreach (var old in bodyPositions)
            if (!nextPositions.TryGetValue(old.Key, out var pos) || pos != old.Value) removedIds.Add(old.Key);
        foreach (byte id in removedIds)
        {
            GetArrow.Remove(Player.PlayerId, bodyPositions[id]);
            bodyPositions.Remove(id);
            changed = true;
        }
        foreach (var current in nextPositions)
            if (bodyPositions.TryAdd(current.Key, current.Value))
            {
                GetArrow.Add(Player.PlayerId, current.Value);
                changed = true;
            }
        return changed;
    }

    bool ClearArrows()
    {
        bool changed = bodyPositions.Count != 0;
        foreach (var pos in bodyPositions.Values) GetArrow.Remove(Player.PlayerId, pos);
        bodyPositions.Clear();
        nextPositions.Clear();
        removedIds.Clear();
        return changed;
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) { ClearArrows(); SyncState(); }
    public override void OnStartMeeting() { ClearArrows(); if (AmongUsClient.Instance.AmHost) SyncState(); }
    public override void OnMurderPlayerAsTarget(MurderInfo info) { ClearArrows(); if (AmongUsClient.Instance.AmHost) SyncState(); }
    public override void OnDestroy() { ClearArrows(); PetActionManager.Unregister(Player.PlayerId); }

    void SyncState()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender(SendOption.Reliable);
        sender.Writer.Write(CleanCount);
        sender.Writer.Write(revealedToImpostors);
        sender.Writer.Write(cooldownLeft);
        sender.Writer.WritePacked(CleanedBodies.Count);
        foreach (byte id in CleanedBodies) sender.Writer.Write(id);
        sender.Writer.WritePacked(bodyPositions.Count);
        foreach (var body in bodyPositions)
        {
            sender.Writer.Write(body.Key);
            NetHelpers.WriteVector2(body.Value, sender.Writer);
        }
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        CleanCount = reader.ReadInt32();
        revealedToImpostors = reader.ReadBoolean();
        cooldownLeft = reader.ReadSingle();
        CleanedBodies.Clear();
        int count = reader.ReadPackedInt32();
        for (int i = 0; i < count; i++) CleanedBodies.Add(reader.ReadByte());
        ClearArrows();
        count = reader.ReadPackedInt32();
        for (int i = 0; i < count; i++)
        {
            byte id = reader.ReadByte();
            Vector2 pos = NetHelpers.ReadVector2(reader);
            bodyPositions[id] = pos;
            GetArrow.Add(Player.PlayerId, pos);
        }
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => Utils.ColorString(RoleInfo.RoleColor, $"({CleanCount}/{RequiredCount.GetInt()})");
    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (!Is(seer) || seen.PlayerId != Player.PlayerId || isForMeeting || !Player.IsAlive() || !ShowArrows.GetBool()) return "";
        var text = new StringBuilder();
        foreach (var pos in bodyPositions.Values) text.Append(GetArrow.GetArrows(seer, pos));
        return Utils.ColorString(RoleInfo.RoleColor, text.ToString());
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Is(seer) || seen.PlayerId != Player.PlayerId || isForMeeting || !Player.IsAlive()) return "";
        return cooldownLeft > 0f ? string.Format(GetString("MadCleanerCooldownLeft"), Mathf.CeilToInt(cooldownLeft)) : GetString("MadCleanerReady");
    }
}

