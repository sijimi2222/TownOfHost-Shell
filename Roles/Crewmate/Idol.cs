using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Attributes;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.AddOns.Common;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Roles.Crewmate;

// Adapted from the contributor-provided Idol.cs. Confirm permission and license
// conditions before publication; author credits are maintained by the project owner.
public sealed class Idol : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(Idol), player => new Idol(player), CustomRoles.Idol,
        () => RoleTypes.Crewmate, CustomRoleTypes.Crewmate, 40100, SetupOptionItem,
        "idol", "#F8ABA6", (5, 50), from: From.TownOfHost_Shell, isNewRole: true);

    static OptionItem OptionFanDuration, OptionTasksRequired, OptionCooldown, OptionBoostDuration, OptionSpeedUp;
    enum OptionName { IdolFanDuration, IdolTasksRequired, IdolCooldown, IdolBoostDuration, IdolSpeedUp }
    static readonly HashSet<Idol> instances = new();
    // IL2CPP Unity equality is native-object equality, while the CLR default
    // collection comparer uses managed wrapper identity. Settings senders and
    // player enumeration can hold different wrappers for the same native player.
    sealed class PlayerIdentityComparer : IEqualityComparer<PlayerControl>
    {
        public bool Equals(PlayerControl x, PlayerControl y) => x == y;
        public int GetHashCode(PlayerControl player) => ReferenceEquals(player, null) ? 0 : player.GetInstanceID();
    }
    static readonly IEqualityComparer<PlayerControl> playerIdentity = new PlayerIdentityComparer();
    readonly HashSet<PlayerControl> frozen = new(playerIdentity);
    sealed class Boost { internal float Remaining, Amount; }
    readonly Dictionary<PlayerControl, Boost> boosts = new(playerIdentity);
    readonly byte ownerId;
    bool fanServing, disposed, receivedStatus;
    float fanTimer, cooldownTimer, syncTimer;
    int lastUsedTaskCount, remoteTasksLeft;

    public Idol(PlayerControl player) : base(RoleInfo, player)
    {
        ownerId = player.PlayerId;
        cooldownTimer = OptionCooldown.GetFloat();
    }

    static void SetupOptionItem()
    {
        OptionFanDuration = FloatOptionItem.Create(RoleInfo, 10, OptionName.IdolFanDuration, new(1f, 10f, 0.5f), 3f, false).SetValueFormat(OptionFormat.Seconds);
        OptionTasksRequired = IntegerOptionItem.Create(RoleInfo, 11, OptionName.IdolTasksRequired, new(1, 10, 1), 3, false).SetValueFormat(OptionFormat.Pieces);
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 12, OptionName.IdolCooldown, new(10f, 40f, 5f), 20f, false).SetValueFormat(OptionFormat.Seconds);
        OptionBoostDuration = FloatOptionItem.Create(RoleInfo, 13, OptionName.IdolBoostDuration, new(5f, 20f, 2.5f), 10f, false).SetValueFormat(OptionFormat.Seconds);
        OptionSpeedUp = FloatOptionItem.Create(RoleInfo, 14, OptionName.IdolSpeedUp, new(0.25f, 3f, 0.25f), 0.75f, false);
    }

    bool IsCurrent => !disposed && Player != null && ReferenceEquals(Player.GetRoleClass(), this)
        && PlayerCatch.GetPlayerById(Player.PlayerId) == Player;
    static bool InTasks => GameStates.InGame && GameStates.IsInTask && !GameStates.Intro
        && !GameStates.IsMeeting && !GameStates.CalledMeeting;
    bool EffectOwnerValid => IsCurrent && Player.IsAlive() && Amnesia.CheckAbility(Player) && InTasks;
    int TasksLeft => Mathf.Max(0, OptionTasksRequired.GetInt() - Mathf.Max(0, MyTaskState.CompletedTasksCount - lastUsedTaskCount));

    public override void Add()
    {
        instances.Add(this);
        CustomRoleManager.OnFixedUpdateOthers.Add(TickAll);
        CustomRoleManager.OnMurderPlayerOthers.Add(AfterMurder);
        RegisterPet();
        Trace($"ADD host={AmongUsClient.Instance.AmHost} cooldown={cooldownTimer:F2}");
        if (AmongUsClient.Instance.AmHost) SyncState();
    }

    public override void OnSpawn(bool initialState = false) => RegisterPet();

    void RegisterPet()
    {
        if (AmongUsClient.Instance.AmHost && IsCurrent)
        {
            bool alreadyRegistered = PetActionManager.Handlers.TryGetValue(Player.PlayerId, out var action) && action == (Action)OnPet;
            PetActionManager.Register(Player.PlayerId, OnPet);
            if (!alreadyRegistered) Trace($"PET_REGISTER current={IsCurrent}");
        }
    }

    void UnregisterPet()
    {
        if (Player != null && PetActionManager.Handlers.TryGetValue(Player.PlayerId, out var action)
            && action == (Action)OnPet) PetActionManager.Unregister(Player.PlayerId);
    }

    public override void OnDestroy()
    {
        UnregisterPet();
        disposed = true;
        instances.Remove(this);
        EndAll(false, "ROLE_DESTROY");
        if (instances.Count == 0)
        {
            CustomRoleManager.OnFixedUpdateOthers.Remove(TickAll);
            CustomRoleManager.OnMurderPlayerOthers.Remove(AfterMurder);
        }
    }

    [GameModuleInitializer]
    public static void Reset()
    {
        foreach (var role in instances.ToArray())
        {
            role.UnregisterPet();
            role.EndAll(false, "RESET");
        }
        instances.Clear();
        CustomRoleManager.OnFixedUpdateOthers.Remove(TickAll);
        CustomRoleManager.OnMurderPlayerOthers.Remove(AfterMurder);
    }

    void OnPet()
    {
        string reason = PetRejectionReason();
        Trace($"PET completed={MyTaskState.CompletedTasksCount} total={MyTaskState.AllTasksCount} required={OptionTasksRequired.GetInt()} usedAt={lastUsedTaskCount} tasksLeft={TasksLeft} cooldown={cooldownTimer:F2} canUse={reason == "READY"} reason={reason}");
        if (reason != "READY") return;

        fanServing = true;
        fanTimer = OptionFanDuration.GetFloat();
        lastUsedTaskCount = MyTaskState.CompletedTasksCount;
        frozen.Clear();
        foreach (var target in PlayerCatch.AllAlivePlayerControls.ToArray())
        {
            string reasonTarget = target == Player ? "SELF" : !SamePlayer(target) ? "STALE_OR_DISCONNECTED"
                : IsRestricted(target) ? "RESTRICTED_MOVEMENT" : SightRejectionReason(target);
            Trace($"TARGET target={target.PlayerId} result={reasonTarget}");
            if (reasonTarget == "SELECTED") frozen.Add(target);
        }
        Refresh(Player);
        foreach (var target in frozen) Refresh(target);
        Trace($"FAN_START duration={fanTimer:F2} targets={frozen.Count}");
        Flush();
        TraceMovement(Player, "FREEZE");
        foreach (var target in frozen) TraceMovement(target, "FREEZE");
        SyncState();
    }

    string PetRejectionReason()
    {
        if (!AmongUsClient.Instance.AmHost) return "NOT_HOST";
        if (!IsCurrent) return "STALE_ROLE_OR_PLAYER";
        if (!Player.IsAlive()) return "DEAD";
        if (!Amnesia.CheckAbility(Player)) return "ABILITY_DISABLED";
        if (!InTasks) return "NOT_TASK_PHASE";
        if (fanServing) return "FAN_ACTIVE";
        if (cooldownTimer > 0f) return "COOLDOWN";
        if (TasksLeft > 0) return "TASKS";
        if (IsRestricted(Player)) return "RESTRICTED_MOVEMENT";
        return Player.GetPlayerState()?.CanMove != true ? "MOVEMENT_LOCKED" : "READY";
    }

    string SightRejectionReason(PlayerControl target)
    {
        if (ShipStatus.Instance == null) return "NO_SHIP";
        var from = Player.GetTruePosition();
        var to = target.GetTruePosition();
        float radius = ShipStatus.Instance.CalculateLightRadius(Player.Data);
        float distance = Vector2.Distance(from, to);
        Trace($"SIGHT target={target.PlayerId} distance={distance:F2} radius={radius:F2}");
        if (!float.IsFinite(radius) || radius < 0f) return "INVALID_RADIUS";
        if (distance > radius) return "OUT_OF_SIGHT";
        return PhysicsHelpers.AnythingBetween(from, to, Constants.ShadowMask, false) ? "WALL_BLOCKED" : "SELECTED";
    }

    static bool SamePlayer(PlayerControl player) => player != null && player.Data != null
        && !player.Data.Disconnected && PlayerCatch.GetPlayerById(player.PlayerId) == player;

    static bool IsRestricted(PlayerControl player)
        => player == null || player.MyPhysics == null || player.MyPhysics.Animations == null
            || player.onLadder || player.inMovingPlat || player.inVent || player.walkingToVent
            || player.MyPhysics.Animations.IsPlayingAnyLadderAnimation()
            || player.MyPhysics.Animations.IsPlayingEnterVentAnimation();

    // Called once per local FixedUpdate even if the role owner died, left, or lost
    // its own task-phase callback. Never rely on a delayed byte PlayerId lookup.
    static void TickAll(PlayerControl tickingPlayer)
    {
        if (tickingPlayer != PlayerControl.LocalPlayer) return;
        foreach (var role in instances.ToArray()) role.Tick();
    }

    void Tick()
    {
        float dt = Time.fixedDeltaTime;
        if (!float.IsFinite(dt) || dt <= 0f) return;
        if (!AmongUsClient.Instance.AmHost)
        {
            // Presentation interpolation only; clients cannot end or grant effects.
            if (InTasks && receivedStatus)
            {
                if (fanServing) fanTimer = Mathf.Max(0f, fanTimer - dt);
                else cooldownTimer = Mathf.Max(0f, cooldownTimer - dt);
            }
            return;
        }
        if (!EffectOwnerValid || (fanServing && IsRestricted(Player))) { EndAll(true, "OWNER_OR_PHASE_INVALID"); return; }
        bool changed = PruneTargets();
        List<PlayerControl> expired = null;
        foreach (var pair in boosts.ToArray())
        {
            pair.Value.Remaining -= dt;
            if (pair.Value.Remaining > 0f) continue;
            boosts.Remove(pair.Key);
            Trace($"BOOST_END target={pair.Key.PlayerId} reason=EXPIRED");
            (expired ??= new()).Add(pair.Key);
            Refresh(pair.Key);
            changed = true;
        }
        if (changed)
        {
            Flush();
            if (expired != null) foreach (var target in expired) TraceMovement(target, "RESTORE");
        }
        if (fanServing)
        {
            fanTimer = Mathf.Max(0f, fanTimer - dt);
            if (fanTimer <= 0f) { EndFanService(); return; }
        }
        else cooldownTimer = Mathf.Max(0f, cooldownTimer - dt);
        syncTimer -= dt;
        if (syncTimer <= 0f && (fanServing || cooldownTimer > 0f)) SyncState();
    }

    static void AfterMurder(MurderInfo info)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        foreach (var role in instances.ToArray())
        {
            if (!role.EffectOwnerValid) role.EndAll(true, "OWNER_DEATH_OR_INVALID");
            else if (role.PruneTargets()) Flush();
        }
    }

    bool PruneTargets()
    {
        bool changed = false;
        foreach (var target in frozen.ToArray())
        {
            if (SamePlayer(target) && target.IsAlive() && !IsRestricted(target)) continue;
            frozen.Remove(target);
            Trace($"FREEZE_END target={target.PlayerId} reason=TARGET_INVALID_OR_RESTRICTED");
            Refresh(target);
            changed = true;
        }
        foreach (var target in boosts.Keys.ToArray())
        {
            if (SamePlayer(target) && target.IsAlive()) continue;
            boosts.Remove(target);
            Trace($"BOOST_END target={target.PlayerId} reason=TARGET_INVALID");
            Refresh(target);
            changed = true;
        }
        return changed;
    }

    void EndFanService()
    {
        fanServing = false;
        fanTimer = 0f;
        cooldownTimer = OptionCooldown.GetFloat();
        Refresh(Player);
        foreach (var target in frozen)
        {
            if (SamePlayer(target) && target.IsAlive() && !IsRestricted(target))
            {
                // Refresh one contribution; multiple Idols use the strongest active
                // boost instead of accumulating additions into a shared base speed.
                boosts[target] = new Boost { Remaining = OptionBoostDuration.GetFloat(), Amount = OptionSpeedUp.GetFloat() };
            }
            Refresh(target);
        }
        var released = frozen.ToArray();
        frozen.Clear();
        Trace($"FAN_END targets={released.Length} boosts={boosts.Count} cooldown={cooldownTimer:F2}");
        Flush();
        TraceMovement(Player, "RESTORE");
        foreach (var target in released) TraceMovement(target, "BOOST_OR_RESTORE");
        SyncState();
    }

    void EndAll(bool notify, string reason)
    {
        bool hadFan = fanServing;
        bool changed = hadFan || frozen.Count > 0 || boosts.Count > 0;
        var affected = frozen.Concat(boosts.Keys).ToHashSet(playerIdentity);
        if (hadFan && Player != null) affected.Add(Player);
        fanServing = false;
        fanTimer = 0f;
        if (hadFan) cooldownTimer = OptionCooldown.GetFloat();
        frozen.Clear();
        boosts.Clear();
        if (!AmongUsClient.Instance.AmHost || !changed) return;
        Trace($"EFFECT_CLEAR reason={reason} targets={affected.Count}");
        foreach (var player in affected) Refresh(player);
        Flush();
        foreach (var player in affected) TraceMovement(player, "RESTORE");
        if (notify && IsCurrent) SyncState();
    }

    public override void OnStartMeeting()
    {
        if (AmongUsClient.Instance.AmHost) { EndAll(false, "MEETING"); SyncState(); }
    }

    public override bool OnCompleteTask(uint taskid)
    {
        if (AmongUsClient.Instance.AmHost && IsCurrent)
        {
            Trace($"TASK completed={MyTaskState.CompletedTasksCount} total={MyTaskState.AllTasksCount} required={OptionTasksRequired.GetInt()} tasksLeft={TasksLeft} cooldown={cooldownTimer:F2}");
            SyncState();
        }
        return true;
    }

    // Pure composition in each recipient's normal game options. Do not write
    // PlayerState.CanMove or Main.AllPlayerSpeed: unrelated effects own those.
    internal static float ApplyMovement(PlayerControl player, float speed)
    {
        if (instances.Count == 0 || !AmongUsClient.Instance.AmHost || !InTasks || !SamePlayer(player)
            || !player.IsAlive() || IsRestricted(player)) return speed;
        float addition = 0f;
        foreach (var role in instances)
        {
            if (!role.EffectOwnerValid) continue;
            if (role.fanServing && (role.Player == player || role.frozen.Contains(player))) return Main.MinSpeed;
            if (role.boosts.TryGetValue(player, out var boost) && boost.Remaining > 0f)
                addition = Mathf.Max(addition, boost.Amount);
        }
        // Existing roots have priority over boosts, including speed-based roots.
        if (addition <= 0f || speed <= Main.MinSpeed || player.GetPlayerState()?.CanMove != true) return speed;
        return Mathf.Clamp(speed + addition, Main.MinSpeed, 10f);
    }

    static void Refresh(PlayerControl player)
    {
        if (AmongUsClient.Instance.AmHost && SamePlayer(player)) player.MarkDirtySettings();
    }
    static void Flush()
    {
        if (AmongUsClient.Instance.AmHost) GameOptionsSender.SendAllGameOptions();
    }

    // Event-only diagnostics: session-local numeric IDs, no names or account IDs.
    void Trace(FormattableString message) => Logger.Info($"role=Idol player={ownerId} {FormattableString.Invariant(message)}", "Idol");

    void TraceMovement(PlayerControl target, string phase)
    {
        if (!SamePlayer(target)) return;
        // This is the host's prepared-options snapshot, NOT a client delivery ACK.
        try
        {
            var sender = GameOptionsSender.AllSenders.OfType<PlayerGameOptionsSender>()
                .FirstOrDefault(s => s.player == target);
            string prepared = sender?.cachedGameOptions == null ? "unavailable"
                : sender.cachedGameOptions.GetFloat(FloatOptionNames.PlayerSpeedMod).ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
            Trace($"MOVEMENT target={target.PlayerId} phase={phase} canMove={target.GetPlayerState()?.CanMove} preparedSpeed={prepared} delivery=UNCONFIRMED");
        }
        catch
        {
            // Diagnostics must never interrupt ability cleanup or expose exceptions.
            Trace($"MOVEMENT target={target.PlayerId} phase={phase} preparedSpeed=unavailable delivery=UNCONFIRMED");
        }
    }

    void SyncState()
    {
        if (!AmongUsClient.Instance.AmHost || !IsCurrent) return;
        syncTimer = 1f;
        using var sender = CreateSender(SendOption.Reliable);
        sender.Writer.Write(fanServing);
        sender.Writer.Write(fanTimer);
        sender.Writer.Write(cooldownTimer);
        sender.Writer.Write(TasksLeft);
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost || !IsCurrent) return;
        bool serving = reader.ReadBoolean();
        float fan = reader.ReadSingle(), cooldown = reader.ReadSingle();
        int left = reader.ReadInt32();
        if (!float.IsFinite(fan) || !float.IsFinite(cooldown) || fan < 0f || fan > 10f
            || cooldown < 0f || cooldown > 40f || left < 0 || left > 10) return;
        fanServing = serving;
        fanTimer = fan;
        cooldownTimer = cooldown;
        remoteTasksLeft = left;
        receivedStatus = true;
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null,
        bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seer != Player || seen != Player || !Player.IsAlive() || isForMeeting) return "";
        int left = !AmongUsClient.Instance.AmHost && receivedStatus ? remoteTasksLeft : TasksLeft;
        string text = fanServing ? string.Format(GetString("IdolFanActive"), fanTimer.ToString("F1"))
            : left > 0 ? string.Format(GetString("IdolTasksLeft"), left)
            : cooldownTimer > 0f ? string.Format(GetString("IdolPreparing"), cooldownTimer.ToString("F0"))
            : GetString("IdolReady");
        string colored = $"<color={RoleInfo.RoleColorCode}>{text}</color>";
        return isForHud ? colored : $"<size=60%>{colored}</size>";
    }
}

