using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

public sealed class Gimlet : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Gimlet),
            player => new Gimlet(player),
            CustomRoles.Gimlet,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            994200,
            SetupOptionItem,
            "gml",
            "#FF1919",
            OptionSort: (0, -4),
            from: From.NebulaontheShip,
            isNewRole: true
        );

    public Gimlet(PlayerControl player) : base(RoleInfo, player)
    {
        context = new RoleAbilityContext(this);
        killCooldown = OptionKillCooldown.GetFloat();
        drillCooldown = OptionDrillCooldown.GetFloat();
        hitRadius = OptionHitRadius.GetFloat();
        speedRate = OptionSpeed.GetFloat();
        dashTime = OptionDashTime.GetFloat();
        friendlyFire = OptionFriendlyFire.GetBool();
        friction = OptionFriction.GetFloat();
    }

    readonly RoleAbilityContext context;
    bool disposed;
    bool CanAct => !disposed && context.CanAct;

    static OptionItem OptionKillCooldown;
    static OptionItem OptionDrillCooldown;
    static OptionItem OptionHitRadius;
    static OptionItem OptionSpeed;
    static OptionItem OptionDashTime;
    static OptionItem OptionFriendlyFire;
    static OptionItem OptionFriction;
    enum OptionName { GimletDrillCooldown, GimletHitRadius, GimletSpeed, GimletDashTime, GimletFriendlyFire, GimletFriction }

    readonly float killCooldown;
    readonly float drillCooldown;
    readonly float hitRadius;
    readonly float speedRate;
    readonly float dashTime;
    readonly bool friendlyFire;
    readonly float friction;

    const float SnapInterval = 0.1f;
    const float BaseWalkSpeed = 2.5f;

    bool dashing;
    float dashUntil;
    float nextSnapAt;
    float lastStepAt;
    Vector2 dashPos;
    Vector2 dashStart;
    Vector2 dir;
    TextDummy drill;
    float drillSpawnAt = -1f;
    const float DummySpawnDelay = 4f;
    Vector2 bodyOffset;
    float savedSpeed = -1f;

    readonly System.Collections.Generic.HashSet<uint> hitTargets = new();
    Vector2 lastPos;
    Vector2 lastMoveDir;
    float lastMoveAt = -99f;

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDrillCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.GimletDrillCooldown, new(2.5f, 180f, 2.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionHitRadius = FloatOptionItem.Create(RoleInfo, 12, OptionName.GimletHitRadius, new(0.25f, 2f, 0.25f), 0.5f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionSpeed = FloatOptionItem.Create(RoleInfo, 13, OptionName.GimletSpeed, new(0.5f, 3f, 0.25f), 1f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionDashTime = FloatOptionItem.Create(RoleInfo, 14, OptionName.GimletDashTime, new(0.5f, 5f, 0.25f), 1.5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionFriendlyFire = BooleanOptionItem.Create(RoleInfo, 15, OptionName.GimletFriendlyFire, false, false);
        OptionFriction = FloatOptionItem.Create(RoleInfo, 16, OptionName.GimletFriction, new(0f, 1f, 0.1f), 0.5f, false)
            .SetValueFormat(OptionFormat.Multiplier);
    }

    public float CalculateKillCooldown() => killCooldown;
    public override void ApplyGameOptions(IGameOptions opt) => AURoleOptions.PhantomCooldown = drillCooldown;

    bool IUsePhantomButton.IsPhantomRole => true;
    public override string GetAbilityButtonText() => GetString("GimletButton");

    float DashSpeed => BaseWalkSpeed * 2f * Main.NormalOptions.PlayerSpeedMod * speedRate;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = false;
        if (!CanAct || dashing) return;
        if (Player.onLadder || Player.inMovingPlat || Player.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return;
        ResetCooldown = true;
        dir = Time.time - lastMoveAt < 0.5f && lastMoveDir.sqrMagnitude > 0.5f
            ? lastMoveDir
            : (Player.cosmetics.FlipX ? Vector2.left : Vector2.right);
        hitTargets.Clear();
        dashing = true;
        dashUntil = Time.time + dashTime;
        dashPos = Player.GetTruePosition();
        dashStart = dashPos;
        bodyOffset = (Vector2)Player.transform.position - dashPos;
        lastStepAt = Time.time;
        nextSnapAt = 0f;
        if (savedSpeed < 0f) savedSpeed = Main.AllPlayerSpeed.TryGetValue(Player.PlayerId, out var currentSpeed) ? currentSpeed : Main.NormalOptions.PlayerSpeedMod;
        Main.AllPlayerSpeed[Player.PlayerId] = Main.MinSpeed;
        Player.MarkDirtySettings();
        Logger.Info($"{Player.GetNameWithRole().RemoveHtmlTags()} がドリルで突進しました 向き={dir}", "Gimlet");
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!context.IsCurrent || !Is(player)) return;
        if (!CanAct) { StopDash(); RemoveDrill(); return; }
        else if (drill == null && GameStates.IsInTask && !GameStates.IsMeeting)
        {
            if (drillSpawnAt < 0f) drillSpawnAt = Time.time + DummySpawnDelay;
            if (Time.time >= drillSpawnAt) drill = new TextDummy(context);
        }
        if (!dashing)
        {
            var pos = (Vector2)Player.transform.position;
            var delta = pos - lastPos;
            if (delta.magnitude > 0.02f)
            {
                lastMoveDir = delta.normalized;
                lastMoveAt = Time.time;
            }
            lastPos = pos;
            return;
        }
        if (!Player.IsAlive() || GameStates.IsMeeting || GameStates.CalledMeeting) { StopDash(); return; }
        if (Time.time < nextSnapAt) return;
        nextSnapAt = Time.time + SnapInterval;
        var step = DashSpeed * (Time.time - lastStepAt);
        lastStepAt = Time.time;
        var from = dashPos;
        var moved = Advance(step);
        HitPlayers(from, dashPos);
        Player.RpcSnapToForced(dashPos + bodyOffset);
        ShowDrill();
        if (!moved)
        {
            Logger.Info($"ドリルが壁に当たって止まりました 進んだ距離={Vector2.Distance(dashStart, dashPos):0.00}", "Gimlet");
            StopDash();
            return;
        }
        if (Time.time >= dashUntil)
        {
            Logger.Info($"ドリルの突進が終わりました 進んだ距離={Vector2.Distance(dashStart, dashPos):0.00}", "Gimlet");
            StopDash();
        }
    }

    void ShowDrill()
    {
        if (drill == null) return;
        var deg = Mathf.RoundToInt(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        drill.Show($"<size=300%><rotate={deg}><#9AA0A6>▶</color></rotate></size>");
        drill.MoveTo(dashPos + bodyOffset + dir * 0.7f + new Vector2(0f, 0.1f));
    }

    void RemoveDrill()
    {
        drill?.Remove();
        drill = null;
        drillSpawnAt = -1f;
    }

    public override void OnDestroy() { StopDash(); RemoveDrill(); disposed = true; }
    public override void OnStartMeeting() { StopDash(); RemoveDrill(); }
    public override void OnDead(PlayerControl player) { if (Is(player)) { StopDash(); RemoveDrill(); } }

    const float WallMargin = 0.3f;

    static bool Free(Vector2 from, Vector2 d, float len)
        => !PhysicsHelpers.AnyNonTriggersBetween(from, d, len + WallMargin, Constants.ShipOnlyMask);

    bool Advance(float step)
    {
        if (step <= 0f) return true;
        if (Free(dashPos, dir, step))
        {
            dashPos += dir * step;
            return true;
        }
        var horizontal = new Vector2(Mathf.Sign(dir.x), 0f);
        var vertical = new Vector2(0f, Mathf.Sign(dir.y));
        var axes = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y) ? new[] { horizontal, vertical } : new[] { vertical, horizontal };
        foreach (var axis in axes)
        {
            var along = Mathf.Abs(Vector2.Dot(dir, axis));
            if (along < 0.05f) continue;
            var headOn = Mathf.Sqrt(Mathf.Max(0f, 1f - along * along));
            if (headOn > friction) continue;
            if (!Free(dashPos, axis, step)) continue;
            dir = axis;
            dashPos += axis * step;
            return true;
        }
        for (var i = 3; i >= 1; i--)
        {
            var s = step * i / 4f;
            if (!Free(dashPos, dir, s)) continue;
            dashPos += dir * s;
            break;
        }
        return false;
    }

    void HitPlayers(Vector2 from, Vector2 to)
    {
        var seg = to - from;
        var len = seg.magnitude;
        foreach (var target in PlayerCatch.AllAlivePlayerControls.ToArray())
        {
            if (!CanAct) break;
            if (target == null || Is(target) || !target.IsAlive() || target.inVent) continue;
            if (!friendlyFire && Player.IsTeammate(target) && !SuddenDeathMode.NowSuddenDeathMode) continue;
            var p = target.GetTruePosition() - from;
            var t = len > 0.001f ? Mathf.Clamp(Vector2.Dot(p, seg / len), 0f, len) : 0f;
            var closest = len > 0.001f ? seg / len * t : Vector2.zero;
            if ((p - closest).magnitude > hitRadius || !hitTargets.Add(target.NetId)) continue;
            Logger.Info($"ドリルが {target.GetNameWithRole().RemoveHtmlTags()} に当たりました", "Gimlet");
            CustomRoleManager.OnCheckMurder(Player, target, target, target, true, deathReason: CustomDeathReason.Kill);
        }
    }

    void StopDash()
    {
        if (!dashing) return;
        dashing = false;
        if (savedSpeed >= 0f)
        {
            if (context.IsCurrent && Main.AllPlayerSpeed.TryGetValue(Player.PlayerId, out var currentSpeed) && Mathf.Abs(currentSpeed - Main.MinSpeed) < 0.00001f)
                Main.AllPlayerSpeed[Player.PlayerId] = savedSpeed;
            savedSpeed = -1f;
            Player.MarkDirtySettings();
        }
        lastPos = Player.transform.position;
        drill?.Park();
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        StopDash();
        RemoveDrill();
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting || !dashing || seer.PlayerId != Player.PlayerId || seen.PlayerId != Player.PlayerId) return "";
        return Utils.ColorString(RoleInfo.RoleColor, GetString("GimletDashing"));
    }

    void SendRPC()
    {
        if (!context.IsCurrent) return;
        using var sender = CreateSender();
        sender.Writer.Write(dashing);
    }
    public override void ReceiveRPC(MessageReader reader) => dashing = reader.ReadBoolean();
}
