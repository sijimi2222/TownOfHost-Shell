// Adapted from the supplied TOH-hamo main Nova (GNU GPL v3; see LICENSE).
// Upstream SHA-256: fdb3a6b244a88662e9c1dd29820817088742b404609a27744fd60be7f788f465
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

// ノヴァ: 一直線上にビームを放つインポスター(星砲)。波動砲を参考にした作り。
// ファントムのボタンでチャージを始める(チャージ中とビーム中はその場から動けない)。
// チャージ中に歩こうとすると、照準(★)がその方向へ少しずつ回る。チャージが終わった後、もう一度ボタンを押すと向きが決まり、
// 少し待ってからビームが出る。
// ビームは、細かいマス目で描いた絵をダミーに貼って並べるので、どの角度でもなめらかに見え、MOD を入れていない人にも見える。
// ダミーを動かす通信(テレポート)は多すぎるとサーバーにキックされるので、照準はゆっくり・向きが変わった時だけ動かす。
public sealed class Nova : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Nova),
            player => new Nova(player),
            CustomRoles.Nova,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            992900,
            SetupOptionItem,
            "nv",
            OptionSort: (3, 13),
            from: From.UchuAddon,
            isNewRole: true
        );

    public Nova(PlayerControl player) : base(RoleInfo, player)
    {
        mode = (FireMode)OptionFireMode.GetValue();
        killCooldown = OptionKillCooldown.GetFloat();
        fireCooldown = OptionFireCooldown.GetFloat();
        beamSize = OptionBeamSize.GetFloat();
        chargeTime = OptionChargeTime.GetFloat();
        fireDelay = OptionFireDelay.GetFloat();
        ownerId = player.PlayerId; ownerNetId = player.NetId;
        ownerRoot = player.GetRoleClass();
        gameId = AmongUsClient.Instance?.GameId ?? 0;
        render = new NovaBeamTile.RenderProfile(beamSize);
    }

    readonly byte ownerId;
    readonly uint ownerNetId;
    readonly int gameId;
    readonly RoleBase ownerRoot;
    readonly NovaBeamTile.RenderProfile render;
    readonly HashSet<uint> attempted = new();
    bool disposed, beamVisible;
    float readyAt, visualDeadline, nextAngleRpc;
    float forcedSpeed = -1f;
    byte remoteVisualStatus;
    int lastVisualStatus = -1;
    bool Current => !disposed && Player != null && Player.NetId == ownerNetId
        && AmongUsClient.Instance != null && AmongUsClient.Instance.GameId == gameId
        && ReferenceEquals(Player.GetRoleClass(), ownerRoot);
    bool Phase => Current && Player.IsAlive() && Player.Data != null && !Player.Data.Disconnected
        && GameStates.IsInTask && !GameStates.IsMeeting && !GameStates.CalledMeeting && MeetingHud.Instance == null;
    bool TilesReady => tilesSpawned && restSpawnAt < 0f && tiles.Count >= TileCount()
        && tiles.All(t => t.Ready);
    bool RenderingCommitted => tiles.All(t => t.Committed);
    byte VisualStatus => tiles.Any(t => t.Failed) ? (byte)2 : TilesReady && Phase ? (byte)1 : (byte)0;

    public enum FireMode { Free, LeftRight, FourWay }
    private static readonly string[] FireModeKeys = { "NovaFireModeFree", "NovaFireModeLeftRight", "NovaFireModeFourWay" };

    private static OptionItem OptionFireMode;
    private static OptionItem OptionKillCooldown;
    private static OptionItem OptionFireCooldown;
    private static OptionItem OptionBeamSize;
    private static OptionItem OptionChargeTime;
    private static OptionItem OptionFireDelay;
    enum OptionName { NovaFireMode, NovaFireCooldown, NovaBeamSize, NovaChargeTime, NovaFireDelay }

    private readonly FireMode mode;
    private readonly float killCooldown;
    private readonly float fireCooldown;
    private readonly float beamSize;
    private readonly float chargeTime;
    private readonly float fireDelay;

    private const float BeamTime = 1.5f;
    private const float BeamLength = 50f;
    private const float AirshipBeamLength = 70f;
    private float BeamRange => Main.NormalOptions?.MapId == (byte)MapNames.Airship ? AirshipBeamLength : BeamLength;
    // 描画と命中判定で共有する最大射程。壁・扉・命中対象では短縮しない。
    private float reach = BeamLength;
    // ビームが自分の体から少し前で始まるように
    private const float BeamStart = 0.5f;
    // チャージ中はほんの少しだけ歩けるようにして、歩こうとした向きを読む
    private const float AimSpeed = 0.15f;
    private const float AimDeadZone = 0.01f;
    // 体がずれると★の円もずれて見えるので、少しずれたらすぐ戻す
    private const float AimLeash = 0.25f;
    // 照準が回る速さ(度/秒)。ちょっとだけ歩こうとすると、ちょっとだけ回るので、細かく合わせられる
    private const float AimTurnSpeed = 90f;
    private const float GuideDistance = 1.3f;
    private const float GuideInterval = 0.1f;
    // 照準を動かし直すのは、向きがこれ以上変わった時だけ
    private const float GuideAngleStep = 1.5f;
    // チャージが終わってから、この秒数ボタンを押さなかったら自動で撃つ(ずっと止まったままにならないように)
    private const float MaxHoldTime = 10f;

    private bool charging;
    // 向きを決めてから、ビームが出るまでの待ち
    private bool waiting;
    private float fireAt;
    // 発射を待つ間は、サボタージュの警報を何度も鳴らす
    private const float AlarmInterval = 0.3f;
    private float nextAlarmAt;
    private bool beaming;
    private float chargeUntil;
    private float beamUntil;
    // チャージを始めた場所(ビームはここから撃つ)
    private Vector2 anchor;
    // 照準の★は、今の体の真ん中を中心に回す(足元を中心にすると、下の方へ向けた時だけ遠く見えていた)
    private Vector2 guideBody;
    // 向きを読むために、前に見た場所を覚えておく(はじめの場所からではなく、直前からの動きで向きを決める)
    private Vector2 lastAimPos;
    // 照準を読むための、チャージを始めた時の体の位置(戻す時はここへ戻す)
    private Vector2 aimHome;
    // 戻した直後は、戻す前の位置の通信が遅れて届くことがあるので、少しの間は向きを読まない
    private float ignoreAimUntil;
    // 今の照準の角度(度)と、歩こうとしている向き。0 が右、反時計回り
    private float angle;
    private float aimTarget;
    private float guideAngle;
    private float nextGuideAt;
    private float savedSpeed = -1f;
    private int lastShownSec = -1;
    private int lastShownAngle = int.MinValue;

    // 0 番は照準、残りはビームの絵を貼るダミー
    private readonly List<NovaBeamTile> tiles = new();
    private bool tilesSpawned;
    // ダミーを出すのは、タスクが始まって少したってから(1ターン目の会議などと重なると、会議の開始で暗転することがあった)
    private const float TileSpawnDelay = 4f;
    private float tileSpawnAt = -1f;
    // 残りのダミーは、照準のダミーで大きさを測り終えてから、必要な数だけ出す(測れない時は少し待ってから)
    private float restSpawnAt = -1f;

    private static void SetupOptionItem()
    {
        OptionFireMode = StringOptionItem.Create(RoleInfo, 10, OptionName.NovaFireMode, FireModeKeys, 0, false);
        OptionFireCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.NovaFireCooldown, new(2.5f, 180f, 2.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionBeamSize = FloatOptionItem.Create(RoleInfo, 12, OptionName.NovaBeamSize, new(0.25f, 5f, 0.25f), 1.25f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionChargeTime = FloatOptionItem.Create(RoleInfo, 13, OptionName.NovaChargeTime, new(0.5f, 10f, 0.5f), 3f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionFireDelay = FloatOptionItem.Create(RoleInfo, 15, OptionName.NovaFireDelay, new(0f, 5f, 0.25f), 1f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 14, GeneralOption.KillCooldown, new(0f, 180f, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public float CalculateKillCooldown() => killCooldown;
    public override void ApplyGameOptions(IGameOptions opt) => AURoleOptions.PhantomCooldown = fireCooldown;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => !(charging || waiting || beaming);

    bool IUsePhantomButton.IsPhantomRole => true;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        // チャージ中にもう一度押せるように、始めた時はクールダウンを入れない
        ResetCooldown = false;
        if (AmongUsClient.Instance?.AmHost != true || !Phase || waiting || beaming) return;
        if (!charging && (Time.time < readyAt || !TilesReady || Player.inVent)) return;
        if (charging)
        {
            // 2回目: チャージが終わっていれば、向きを決めて発射の待ちに入る
            if (Time.time < chargeUntil) return;
            ResetCooldown = true;
            StartWait();
            return;
        }
        // 梯子・ジップライン・移動床の上では使えない(照準を戻す瞬間移動で、体がプルプル震えていた)
        if (OnLadder()) return;
        Logger.Info($"NOVA_CHARGE playerId={ownerId}", "NovaTraffic");
        charging = true;
        chargeUntil = Time.time + chargeTime;
        anchor = Player.GetTruePosition();
        aimHome = Player.transform.position;
        lastAimPos = aimHome;
        ignoreAimUntil = 0f;
        angle = Snap(Player.cosmetics.FlipX ? 180f : 0f);
        aimTarget = angle;
        lastShownSec = -1;
        lastShownAngle = int.MinValue;
        nextGuideAt = 0f;
        guideAngle = float.NaN;
        SetSpeed(AimSpeed);
        ShowGuide(NovaBeamTile.Style.Guide);
        if (!TilesReady) { Stop(); return; }
        SendRpc();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
    }

    private bool OnLadder() => Player.onLadder || Player.inMovingPlat
        || Player.MyPhysics == null || Player.MyPhysics.Animations.IsPlayingAnyLadderAnimation();
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => !(charging || waiting || beaming);

    // チャージ中はほぼ止める(歩こうとした向きだけ読めるくらい)。ビーム中は完全に止める
    private void SetSpeed(float speed)
    {
        if (savedSpeed < 0f) savedSpeed = Main.AllPlayerSpeed.TryGetValue(ownerId, out float old) ? old : 1f;
        forcedSpeed = speed;
        Main.AllPlayerSpeed[ownerId] = speed;
        Player.MarkDirtySettings();
    }

    private void RestoreSpeed()
    {
        if (savedSpeed < 0f) return;
        float restore = savedSpeed, expected = forcedSpeed;
        savedSpeed = forcedSpeed = -1f;
        var owner = PlayerCatch.GetPlayerById(ownerId);
        if (owner != null && owner.NetId == ownerNetId
            && Main.AllPlayerSpeed.TryGetValue(ownerId, out float now) && Mathf.Abs(now - expected) < .0001f)
        {
            Main.AllPlayerSpeed[ownerId] = restore;
            owner.MarkDirtySettings();
        }
        savedSpeed = forcedSpeed = -1f;
    }

    // 歩こうとした向きを読み、照準をその向きへ少しずつ回す。はじめの場所から離れすぎたら戻す
    private void ReadAim()
    {
        var pos = (Vector2)Player.transform.position;
        if (Time.time < ignoreAimUntil)
        {
            lastAimPos = pos;
            return;
        }
        if (!NovaBeamRules.Finite(pos.x) || !NovaBeamRules.Finite(pos.y)) { Stop(); return; }
        var delta = pos - lastAimPos;
        var moving = delta.magnitude >= AimDeadZone;
        if (moving)
        {
            lastAimPos = pos;
            aimTarget = Snap(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
        if (mode == FireMode.Free)
        {
            // 歩こうとしている間だけ回る(手を離すとその場で止まる)
            if (moving) angle = Mathf.MoveTowardsAngle(angle, aimTarget, AimTurnSpeed * Time.fixedDeltaTime * 3f);
        }
        else angle = aimTarget;
        angle = Mathf.DeltaAngle(0f, angle);
        if (Vector2.Distance(pos, aimHome) > AimLeash)
        {
            if (NovaVisualTraffic.Take(gameId, Time.time)) Player.RpcSnapToForced(aimHome);
            lastAimPos = aimHome;
            ignoreAimUntil = Time.time + 0.25f;
        }
    }

    // 発射モードに合わせて、向きをそろえる
    private float Snap(float deg) => NovaBeamRules.SnapAngle(deg, (int)mode);

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !Current || player == null || player.PlayerId != ownerId) return;
        if (!Phase)
        {
            if (charging || waiting || beaming) Stop();
            if (tilesSpawned) RemoveTiles();
            return;
        }
        if (!Player.IsAlive())
        {
            if (charging || waiting || beaming) Stop();
            if (tilesSpawned) RemoveTiles();
            return;
        }
        if (GameStates.IsMeeting || GameStates.CalledMeeting)
        {
            if (charging || waiting || beaming) Stop();
            return;
        }
        // ビーム用のダミーは、出すのに時間がかかるので、最初に作って見えない所へ置いておく
        if (!tilesSpawned && GameStates.IsInTask)
        {
            if (tileSpawnAt < 0f) tileSpawnAt = Time.time + TileSpawnDelay;
            if (Time.time < tileSpawnAt) return;
            tilesSpawned = true;

            tiles.Add(new NovaBeamTile(render, () => Phase, gameId));
            restSpawnAt = Time.time + 2f;
        }
        // The shared queue serializes creation; include the grid-rendering fallback.
        if (tilesSpawned && restSpawnAt >= 0f && (render.Measured || Time.time >= restSpawnAt))
        {
            restSpawnAt = -1f;
            for (var i = tiles.Count; i < TileCount(); i++) tiles.Add(new NovaBeamTile(render, () => Phase, gameId));
            Logger.Info($"NOVA_PREPARE_QUEUED playerId={ownerId} tiles={tiles.Count} (visual preparation, not firing)", "NovaTraffic");
        }
        if (lastVisualStatus != VisualStatus)
        {
            lastVisualStatus = VisualStatus; SendRpc(); UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
            Logger.Info($"NOVA_VISUAL_STATUS playerId={ownerId} status={lastVisualStatus} tiles={tiles.Count}", "NovaTraffic");
        }
        if ((charging || waiting || beaming) && !TilesReady) { Stop(); return; }
        // チャージ中・発射待ちに梯子などへ乗ったら、取り消す
        if ((charging || waiting) && OnLadder())
        {
            Logger.Info($"{Player.GetNameWithRole().RemoveHtmlTags()} が梯子などに乗ったので、ビームを取り消しました", "Nova");
            Stop();
            return;
        }
        if (charging)
        {
            ReadAim();
            if (!charging) return;
            var turned = !(Mathf.Abs(Mathf.DeltaAngle(guideAngle, angle)) < GuideAngleStep);
            var bodyMoved = Vector2.Distance(guideBody, Player.transform.position) > 0.06f;
            if (Time.time >= nextGuideAt && (turned || bodyMoved))
            {
                nextGuideAt = Time.time + GuideInterval;
                ShowGuide(NovaBeamTile.Style.Guide);
                if (Time.time >= nextAngleRpc) { SendRpc(); nextAngleRpc = Time.time + .2f; }
            }
            if (Time.time >= chargeUntil + MaxHoldTime) { Player.RpcResetAbilityCooldown(); StartWait(); return; }
            var sec = Mathf.CeilToInt(Mathf.Max(0f, chargeUntil - Time.time));
            var shownAngle = ShownAngle();
            if (sec != lastShownSec || (shownAngle != lastShownAngle && Time.time >= nextGuideAt - GuideInterval * 0.5f))
            {
                lastShownSec = sec;
                lastShownAngle = shownAngle;
                UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
            }
        }
        else if (waiting)
        {
            if (Time.time >= fireAt) { Fire(); return; }
            if (Time.time >= nextAlarmAt)
            {
                nextAlarmAt = Time.time + AlarmInterval;
                RPC.PlaySoundRPC(byte.MaxValue, Sounds.SabotageAlarm);
            }
        }
        else if (beaming)
        {
            if (!RenderingCommitted)
            {
                if (Time.time >= visualDeadline) { Logger.Warn("Beam visuals not committed; attack cancelled.", "Nova"); Stop(); }
                return;
            }
            if (!beamVisible) { beamVisible = true; beamUntil = Time.time + BeamTime; Utils.AllPlayerKillFlash(); }
            if (Time.time >= beamUntil) { Stop(); return; }
            ApplyBeamHit();
        }
    }

    // One star plus the reusable drawing pool: Airship 15, Fungle 11, others 10.
    // Use the same actual selected-map identity as existing Shell role code.
    private int TileCount()
    {
        var map = Main.NormalOptions?.MapId;
        return NovaVisualPoolRules.Count(map == (byte)MapNames.Fungle, map == (byte)MapNames.Airship);
    }

    private Vector2 Dir => new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    private int ShownAngle() => ((Mathf.RoundToInt(angle) % 360) + 360) % 360;

    // 照準(★)を、今の向きに置く
    private void ShowGuide(NovaBeamTile.Style style)
    {
        guideAngle = angle;
        if (tiles.Count == 0) return;
        int colorId = Player.Data.DefaultOutfit.ColorId;
        tiles[0].GuideColor = colorId >= 0 && colorId < Palette.PlayerColors.Length
            ? "#" + ColorUtility.ToHtmlStringRGB(Palette.PlayerColors[colorId]) : "#FFFFFF";
        tiles[0].SetStyle(style);
        guideBody = Player.transform.position;
        tiles[0].MoveTo(guideBody + Dir * GuideDistance);
    }

    // 向きを決めた: その場で完全に止まり、照準の★を赤くして、少し待ってから撃つ
    private void StartWait()
    {
        charging = false;
        waiting = true;
        readyAt = Time.time + fireCooldown;
        fireAt = Time.time + fireDelay;
        nextAlarmAt = 0f;
        SetSpeed(Main.MinSpeed);
        ShowGuide(NovaBeamTile.Style.Locked);
        if (!TilesReady) { Stop(); return; }
        SendRpc();
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
        if (fireDelay <= 0f) Fire();
    }

    private void Stop()
    {
        charging = false;
        waiting = false;
        beaming = false; beamVisible = false; attempted.Clear();
        try { RestoreSpeed(); } catch (Exception e) { Logger.Warn("NOVA_SPEED_RESTORE_FAILED " + e.GetType().Name, "NovaTraffic"); }
        foreach (var tile in tiles)
        {
            try { tile.Park(); }
            catch (Exception e) { Logger.Warn("NOVA_CLEANUP_FAILED " + e.GetType().Name, "NovaTraffic"); try { tile.Remove(); } catch { } }
        }
        try { SendRpc(); } catch (Exception e) { Logger.Warn("NOVA_STOP_SYNC_FAILED " + e.GetType().Name, "NovaTraffic"); }
        try { UtilsNotifyRoles.NotifyRoles(ForceLoop: true); } catch (Exception e) { Logger.Warn("NOVA_STOP_NOTIFY_FAILED " + e.GetType().Name, "NovaTraffic"); }
    }

    private string drawFailure;
    private bool RejectDraw(string reason) { drawFailure = reason; return false; }

    private void Fire()
    {
        Logger.Info($"NOVA_FIRE_REQUEST playerId={ownerId}", "NovaTraffic");
        if (!Phase || !TilesReady) { Stop(); return; }
        charging = waiting = false; beaming = true; beamVisible = false;
        attempted.Clear(); visualDeadline = Time.time + 2f;
        SetSpeed(Main.MinSpeed);
        bool drawable = false; drawFailure = null;
        try { reach = BeamRange; drawable = reach > BeamStart && DrawBeam(); }
        catch (Exception e) { drawFailure = "exception_" + e.GetType().Name; Logger.Warn("NOVA_DRAW_FAILED " + e.GetType().Name, "NovaTraffic"); }
        if (!drawable || !TilesReady)
        {
            string reason = !TilesReady ? "tiles_not_ready" : drawFailure ?? render.LayoutFailure ?? "coverage_unknown";
            Logger.Warn($"NOVA_DRAW_ABORT reason={reason} ready={TilesReady} slots={tiles.Count - 1} reach={reach:F2}", "NovaTraffic");
            Logger.Warn("Beam has no safe drawable coverage; attack cancelled.", "Nova"); Stop(); return;
        }
        if (tiles.Count > 0) tiles[0].Park();
        SendRpc(); UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    // Pack several unscaled glyphs into each existing dummy. The bodies remain
    // at the real firing position: standard NetHelpers coordinates are only +/-50.
    private bool DrawBeam()
    {
        if (!NovaBeamRules.Finite(anchor.x) || !NovaBeamRules.Finite(anchor.y)
            || Mathf.Abs(anchor.x) > 50f || Mathf.Abs(anchor.y) > 50f || tiles.Count < 2) return RejectDraw("invalid_anchor_or_pool");
        return render.PreparePacked(tiles[0].PlayerControl, angle, reach) && DrawSmoothBeam();
    }

    private bool DrawSmoothBeam()
    {
        float bar = render.PackedBarLength, span = reach - BeamStart;
        if (!NovaBeamRules.Finite(bar) || bar <= 0f || bar > span) return RejectDraw("invalid_glyph_length");
        int slots = tiles.Count - 1;
        int count = NovaVisualPoolRules.GlyphCount(span, bar, slots * NovaBeamTile.RenderProfile.MaxGlyphsPerTile);
        if (count < 1 || count > NovaBeamTile.RenderProfile.MaxPackedGlyphs
            || count > slots * NovaBeamTile.RenderProfile.MaxGlyphsPerTile) return RejectDraw($"glyph_capacity count={count}");
        float first = BeamStart + bar * .5f, last = reach - bar * .5f;
        var allPoints = new List<Vector2>(count);
        for (int i = 0; i < count; i++)
            allPoints.Add(Dir * (count == 1 ? first : Mathf.Lerp(first, last, i / (float)(count - 1))));
        List<string> names = null;
        string failure = "no_plan";
        // A font/platform can need fewer than twelve glyphs per name. Retry only
        // local, bounded layout work; never create extra bodies or send a probe.
        int minimum = Mathf.CeilToInt(count / (float)slots);
        for (int perTile = NovaBeamTile.RenderProfile.MaxGlyphsPerTile; perTile >= minimum; perTile--)
        {
            var candidate = new List<string>();
            var coverage = new List<(float Min, float Max)>();
            bool valid = true;
            for (int offset = 0; offset < count; offset += perTile)
            {
                var points = allPoints.GetRange(offset, Math.Min(perTile, count - offset));
                string name = render.PackedName(angle, points, Vector2.zero);
                if (Encoding.UTF8.GetByteCount(name) > NetworkSafety.NameByteLimit)
                { failure = "name_bytes"; valid = false; break; }
                if (!render.ValidatePackedName(tiles[0].PlayerControl, name, points, angle, out var intervals))
                { failure = render.LayoutFailure; valid = false; break; }
                coverage.AddRange(intervals); candidate.Add(name);
            }
            if (!valid) continue;
            float covered = BeamStart;
            foreach (var interval in coverage)
            {
                if (interval.Min > covered + .005f) { valid = false; failure = "coverage_gap"; break; }
                covered = Mathf.Max(covered, interval.Max);
            }
            if (!valid || covered < reach - .005f) { failure = valid ? "coverage_far_end" : failure; continue; }
            names = candidate; break;
        }
        // Every final name, glyph center and interval is checked before publishing.
        if (names == null || names.Count == 0 || names.Count > slots) return RejectDraw(failure);
        Logger.Info($"NOVA_DRAW_PLAN reach={reach:F2} glyphs={count} chunks={names.Count} maxBytes={names.Max(n => Encoding.UTF8.GetByteCount(n))} bar={bar:F3}", "NovaTraffic");
        for (int i = 0; i < names.Count; i++)
        {
            tiles[i + 1].MoveTo(anchor, true);
            tiles[i + 1].ShowBeam(names[i], true, true);
        }
        for (int i = names.Count + 1; i < tiles.Count; i++) tiles[i].Park();
        return true;
    }

    // ビームの線の上にいる人をキルする(仲間は巻き込まない)。
    // ジャッカルウルフなど、インポスター以外がノヴァの力を持っている時は、インポスターも当たる
    private void ApplyBeamHit()
    {
        if (!Phase || !beamVisible) return;
        var dir = Dir;
        foreach (var target in PlayerCatch.AllAlivePlayerControls.ToArray())
        {
            if (!Phase) { Stop(); return; }
            if (target == null || target.PlayerId == ownerId || target.PlayerId >= 254
                || !target.IsAlive() || target.Data == null || target.Data.Disconnected) continue;
            if (Player.IsTeammate(target) && !SuddenDeathMode.NowSuddenDeathMode) continue;
            var to = target.GetTruePosition() - anchor;
            float along = Vector2.Dot(to, dir), side = Mathf.Abs(Vector2.Dot(to, new Vector2(-dir.y, dir.x)));
            if (!NovaBeamRules.Contains(along, side, reach, beamSize, BeamStart) || !attempted.Add(target.NetId)) continue;
            // Preserve upstream's area-attack path; Shell target defenses/reflection remain active.
            try { CustomRoleManager.OnCheckMurder(Player, target, target, target, true, deathReason: CustomDeathReason.Evaporation); }
            catch (Exception e)
            {
                Logger.Warn("Nova attack failed: " + e.GetType().Name, "Nova"); Stop(); return;
            }
        }
    }

    private void RemoveTiles()
    {
        foreach (var tile in tiles) { try { tile.Remove(); } catch (Exception e) { Logger.Warn("NOVA_REMOVE_FAILED " + e.GetType().Name, "NovaTraffic"); } }
        tiles.Clear(); tilesSpawned = false; tileSpawnAt = restSpawnAt = -1f;
    }

    public override void OnDestroy() { Stop(); RemoveTiles(); disposed = true; }
    public override void OnDead(PlayerControl player) { if (player?.PlayerId == ownerId) { Stop(); RemoveTiles(); } }
    public override void OnStartMeeting() { Stop(); RemoveTiles(); }
    public override void AfterMeetingTasks() { Stop(); RemoveTiles(); readyAt = Time.time + fireCooldown; }

    // 会議が始まる前にダミーを消し、会議の後にまた出す
    // (ダミーを残したまま会議に入ると、MOD を入れていない人の画面が会議の最初で暗転していた)
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (charging || waiting || beaming) Stop();
        RemoveTiles();
        tilesSpawned = false;
        tileSpawnAt = -1f;
        restSpawnAt = -1f;
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!Current || seer == null || seen == null || seer.PlayerId != ownerId || seen.PlayerId != ownerId || isForMeeting || !Player.IsAlive()) return "";
        if (charging)
        {
            var left = Mathf.Max(0f, chargeUntil - Time.time);
            return Utils.ColorString(Palette.ImpostorRed, left > 0f
                ? string.Format(GetString("NovaCharging"), left, ShownAngle())
                : string.Format(GetString("NovaCharged"), ShownAngle()));
        }
        if (waiting) return Utils.ColorString(Palette.ImpostorRed, string.Format(GetString("NovaWaiting"), Mathf.Max(0f, fireAt - Time.time)));
        if (beaming) return Utils.ColorString(Palette.ImpostorRed, GetString("NovaBeaming"));
        byte status = AmongUsClient.Instance?.AmHost == true ? VisualStatus : remoteVisualStatus;
        return Utils.ColorString(RoleInfo.RoleColor, GetString(status == 2 ? "NovaVisualUnavailable" : status == 1 ? "NovaControls" : "NovaPreparing"));
    }

    public override string GetAbilityButtonText() => GetString("NovaFire");

    private void SendRpc()
    {
        if (AmongUsClient.Instance?.AmHost != true || !Current) return;
        using var sender = CreateSender(SendOption.Reliable);
        sender.Writer.Write(charging);
        sender.Writer.Write(waiting);
        sender.Writer.Write(beaming);
        sender.Writer.Write(angle);
        // Optional extension after the upstream seven-byte state. Legacy state
        // readers can ignore it; this build also accepts a legacy seven-byte state.
        sender.Writer.Write(VisualStatus);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        if (AmongUsClient.Instance?.AmHost != false || !Current || reader == null || reader.Length - reader.Position < 7) return;
        bool c = reader.ReadBoolean(), w = reader.ReadBoolean(), b = reader.ReadBoolean();
        float a = reader.ReadSingle();
        byte status = reader.Position < reader.Length ? reader.ReadByte() : (byte)1;
        if (!NovaBeamRules.ValidState(c, w, b, a) || status > 2) return;
        charging = c; waiting = w; beaming = b; angle = a;
        remoteVisualStatus = status;
    }
}

// ノヴァのビームの絵を貼るダミー(0 番は照準の★)。使わない時は、マップの外へ置いておく。
// 文字そのものは透明にして、後ろの塗り(mark)だけでマス目を塗る(文字だと黒いふちが付いて、つなぎ目が見えてしまうため)。
public sealed class NovaBeamTile : CustomNetObject
{
    public enum Style { Hidden, Guide, Locked, Beam }

    // 絵 1 枚のマス目の数(縦横)。名前の長さの上限に収まる大きさにしている
    public const int Grid = 10;
    private const string BeamColor = "#7FEFFFFF";
    private const string Cell = "■";
    // Transparent padding still advances exactly 1em under <mspace>, but does
    // not spend three UTF-8 bytes per invisible square in the network name.
    private const string BlankCell = ".";
    private const string TilePrefix = "<line-height=1em><mspace=1em><alpha=#00>";
    // 塗りの濃さは、mark を始めた時の文字の濃さまでしか出ないので、mark の前だけ文字を不透明に戻す
    private const string MarkOpen = "<alpha=#FF><mark=" + BeamColor + "><alpha=#00>";

    private static readonly Vector2 ParkPos = new(50f, 50f);

    // 1 マスの大きさ(ワールドの長さ)と、それを出す文字の大きさ
    // 文字 1 文字(1em)が、100% の時にワールドでどれくらいの長さか。ホストの画面で測って決める(測れない時の目安)
    // 名前の文字は体の上に出るので、絵の真ん中が体からどれだけずれているか(これも測って決める)


    public sealed class RenderProfile
    {
        float pixel = .3f; int sizePercent = 125; float emAt100 = .25f;
        internal Vector2 tileOffset = new(0f, .6f), guideOffset = new(0f, .6f), barShift;
        bool measured; string smoothFont; int barSize = 100; float setupBeamSize;
        public float TileWorldSize => pixel * Grid;
        public bool Smooth => smoothFont != null;
        public bool Measured => measured;
        public float BarLength { get; private set; } = 1f;
        public RenderProfile(float size) => SetupPixel(size);
        public string BarName(float angle)
    {
        var rot = Mathf.RoundToInt(Mathf.DeltaAngle(0f, angle));
        return $"<font=\"{smoothFont}\"><size={barSize}%><rotate={rot}><#7FEFFF>{BarChar}</color></rotate></size></font>";
    }
        private void FindSmoothFont()
    {
        var basePath = "Fonts & Materials/";
        try
        {
            var path = TMPro.TMP_Settings.defaultFontAssetPath;
            if (!string.IsNullOrEmpty(path)) basePath = path;
        }
        catch { }
        foreach (var name in SmoothFonts)
        {
            try
            {
                var font = Resources.Load(basePath + name, Il2CppInterop.Runtime.Il2CppType.Of<TMPro.TMP_FontAsset>())?.TryCast<TMPro.TMP_FontAsset>();
                if (font == null) continue;
                var mat = font.material;
                var outline = mat != null && mat.HasProperty("_OutlineWidth") ? mat.GetFloat("_OutlineWidth") : 0f;
                if (outline > 0.01f) continue;
                if (!font.characterLookupTable.TryGetValue(BarChar, out var ch) || ch?.glyph == null) continue;
                var m = ch.glyph.metrics;
                var face = font.faceInfo;
                // フォントの数値を、1em あたりの長さに直す
                var unit = face.scale / face.pointSize;
                var width = m.width * unit;
                var height = m.height * unit;
                if (width <= 0f || height <= 0f) continue;
                // 「-」の高さがビームの太さになる大きさ
                barSize = Mathf.Clamp(Mathf.RoundToInt(setupBeamSize / height / emAt100 * 100f), 50, 20000);
                var em = emAt100 * barSize / 100f;
                BarLength = width * em * BarLengthFix;
                // 文字の真ん中が、名前の行の真ん中からどれだけずれるか(文字は自分の真ん中で回るので、回しても同じ)
                var centerX = (m.horizontalBearingX + m.width * 0.5f - m.horizontalAdvance * 0.5f) * unit;
                var centerY = (m.horizontalBearingY - m.height * 0.5f) * unit - (face.ascentLine + face.descentLine) * 0.5f * unit;
                barShift = new Vector2(centerX, centerY) * em - new Vector2(0f, BarRaise);
                smoothFont = name;
                Logger.Info($"なめらかなビーム: {name} 文字={barSize}% 長さ={BarLength:F2} 文字のずれ={barShift}", "Nova");
                return;
            }
            catch (System.Exception e) { Logger.Warn($"{name} を使えませんでした: {e.Message}", "Nova"); }
        }
        Logger.Info("優先ビームフォントを読み込めませんでした。既定フォントを検査します", "Nova");
    }
        public void SetupPixel(float beamSize)
    {
        setupBeamSize = beamSize;
        // At 0.12 the thin beam crosses >16 world-grid cells at tile borders.
        // Keep the upstream dummy cap without truncating the visible far end.
        pixel = Mathf.Max(0.18f, beamSize / 4f);
        sizePercent = Mathf.Clamp(Mathf.RoundToInt(pixel / emAt100 * 100f), 20, 2000);
    }
        internal void Measure(PlayerControl player, string guideColor)
    {
        try
        {
            var text = player.cosmetics.nameText;
            if (text == null) return;
            var scale = Mathf.Abs(text.transform.lossyScale.x);
            var row = string.Concat(Enumerable.Repeat(Cell, Grid));
            var width = text.GetPreferredValues($"{TilePrefix}{row}").x * scale;
            if (width > 0.01f) emAt100 = width / Grid;
            sizePercent = Mathf.Clamp(Mathf.RoundToInt(pixel / emAt100 * 100f), 20, 2000);
            pixel = emAt100 * sizePercent / 100f;

            var block = new StringBuilder($"<size={sizePercent}%>{TilePrefix}");
            for (var r = 0; r < Grid; r++) block.Append(row).Append(r < Grid - 1 ? "\n" : "");
            tileOffset = CenterOf(player, text, block.Append("</size>").ToString());
            guideOffset = CenterOf(player, text, GuideName(false, guideColor));
            measured = true;
            FindSmoothFont();
            Logger.Info($"ビームのマス目: 1em={emAt100:F3} 1マス={pixel:F3} 文字={sizePercent}% ずれ={tileOffset} 照準のずれ={guideOffset}", "Nova");
        }
        catch (System.Exception e) { Logger.Warn($"ビームの大きさを測れませんでした: {e.Message}", "Nova"); }
        finally
        {
            try { player.cosmetics.nameText.text = ""; } catch { }
        }
    }
        private Vector2 CenterOf(PlayerControl player, TMPro.TextMeshPro text, string value)
    {
        text.text = value;
        // 文字が見えていない時でも、大きさを計算させる
        text.ForceMeshUpdate(true, true);
        var center = text.transform.TransformPoint(text.textBounds.center);
        return (Vector2)center - (Vector2)player.transform.position;
    }
        public const int MaxPackedGlyphs = 192;
        public const int MaxGlyphsPerTile = 12;
        private const float FrameWorld = 128f;
        public Vector2 PackedCorrection { get; private set; }
        public float PackedBarLength { get; private set; }
        private float PackedEm => emAt100 * barSize / 100f;

        private bool UseDefaultFont(TMPro.TMP_FontAsset font)
        {
            if (font == null || !font.characterLookupTable.TryGetValue(BarChar, out var ch) || ch?.glyph == null) return false;
            var mat = font.material;
            if (mat != null && mat.HasProperty("_OutlineWidth") && mat.GetFloat("_OutlineWidth") > .01f) return false;
            var m = ch.glyph.metrics; var f = font.faceInfo;
            float unit = f.scale / f.pointSize, height = m.height * unit, width = m.width * unit;
            if (!NovaBeamRules.Finite(height) || !NovaBeamRules.Finite(width) || height <= 0f || width <= 0f) return false;
            string name = font.name;
            if (string.IsNullOrEmpty(name) || name.Length > 64 || name.IndexOfAny(new[] { '<', '>', '\"', '\r', '\n' }) >= 0) return false;
            barSize = Mathf.Clamp(Mathf.RoundToInt(setupBeamSize / height / emAt100 * 100f), 50, 20000);
            BarLength = width * PackedEm * BarLengthFix;
            smoothFont = name;
            return true;
        }

        // Preferred text width is NOT the native <pos>/<voffset> font-unit
        // scale. Measure the actual final mesh and invert its two-axis mapping.
        private Vector2 layoutOrigin, layoutX, layoutY;
        private float layoutDet, frameUnits;
        public string LayoutFailure { get; private set; }
        private bool LayoutReject(string reason) { LayoutFailure = reason; return false; }
        private static string Number(float value) => value.ToString("0.#####", CultureInfo.InvariantCulture);
        private Vector2 ToLayout(Vector2 point)
        {
            var p = point - layoutOrigin;
            return new Vector2((p.x * layoutY.y - p.y * layoutY.x) / layoutDet,
                (layoutX.x * p.y - layoutX.y * p.x) / layoutDet);
        }
        private static void PlaceRaw(StringBuilder sb, Vector2 point)
        {
            if (!NovaBeamRules.Finite(point.x) || !NovaBeamRules.Finite(point.y)) throw new InvalidOperationException("Invalid beam point");
            sb.Append("<pos=").Append(Number(point.x)).Append("em><voffset=")
                .Append(Number(point.y)).Append("em>");
        }
        private string RawPackedName(float angle, IReadOnlyList<Vector2> points)
        {
            var sb = new StringBuilder($"<font=\"{smoothFont}\"><size={barSize}%><nobr><alpha=#00>");
            PlaceRaw(sb, new Vector2(-frameUnits, -frameUnits)); sb.Append('.');
            sb.Append("<alpha=#FF><rotate=").Append(Mathf.RoundToInt(Mathf.DeltaAngle(0f, angle))).Append("><#7FEFFF>");
            foreach (var point in points) { PlaceRaw(sb, point); sb.Append(BarChar); }
            sb.Append("</color></rotate><alpha=#00>");
            PlaceRaw(sb, new Vector2(frameUnits, frameUnits)); sb.Append(".</voffset></nobr></size></font>");
            return sb.ToString();
        }
        public string PackedName(float angle, IReadOnlyList<Vector2> points, Vector2 correction)
        {
            if (points.Count < 1 || points.Count > MaxGlyphsPerTile || !NovaBeamRules.Finite(layoutDet) || Mathf.Abs(layoutDet) < .000001f)
                throw new InvalidOperationException("Invalid packed beam");
            return RawPackedName(angle, points.Select(p => ToLayout(p + correction)).ToArray());
        }
        private static Vector2[] GlyphVertices(PlayerControl player, TMPro.TextMeshPro text, int index, int count)
        {
            var info = text.textInfo;
            if (info == null || info.characterCount != count + 2 || info.characterInfo == null || info.characterInfo.Length < count + 2)
                throw new InvalidOperationException("Packed character count unavailable");
            var ch = info.characterInfo[index + 1];
            if (ch.character != BarChar || !ch.isVisible || info.meshInfo == null
                || ch.materialReferenceIndex < 0 || ch.materialReferenceIndex >= info.meshInfo.Length)
                throw new InvalidOperationException("Packed glyph unavailable");
            // characterInfo.vertex_* can be intermediate layout buffers. The
            // material mesh holds the quad actually submitted for rendering.
            var vertices = info.meshInfo[ch.materialReferenceIndex].vertices;
            if (vertices == null || ch.vertexIndex < 0 || ch.vertexIndex > vertices.Length - 4)
                throw new InvalidOperationException("Packed mesh unavailable");
            var result = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                result[i] = (Vector2)text.transform.TransformPoint(vertices[ch.vertexIndex + i]) - (Vector2)player.transform.position;
                if (!NovaBeamRules.Finite(result[i].x) || !NovaBeamRules.Finite(result[i].y)) throw new InvalidOperationException("Invalid packed mesh");
            }
            return result;
        }
        private static Vector2 Center(Vector2[] vertices) => (vertices[0] + vertices[1] + vertices[2] + vertices[3]) * .25f;
        private Vector2[] Probe(PlayerControl player, TMPro.TextMeshPro text, float angle, Vector2 raw)
        {
            text.text = RawPackedName(angle, new[] { raw }); text.ForceMeshUpdate(true, true);
            return GlyphVertices(player, text, 0, 1);
        }
        public bool ValidatePackedName(PlayerControl player, string name, IReadOnlyList<Vector2> points, float angle,
            out List<(float Min, float Max)> intervals)
        {
            intervals = new List<(float Min, float Max)>(points.Count);
            var text = player?.cosmetics?.nameText;
            if (text == null) return LayoutReject("missing_text");
            try
            {
                text.text = name; text.ForceMeshUpdate(true, true);
                var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                for (int i = 0; i < points.Count; i++)
                {
                    var vertices = GlyphVertices(player, text, i, points.Count);
                    float error = Vector2.Distance(Center(vertices), points[i]);
                    if (error > .04f) return LayoutReject($"glyph_position index={i} error={Number(error)}");
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (var vertex in vertices) { float along = Vector2.Dot(vertex, dir); lo = Mathf.Min(lo, along); hi = Mathf.Max(hi, along); }
                    if (hi - lo < .001f) return LayoutReject("empty_glyph_mesh");
                    intervals.Add((lo, hi));
                }
                LayoutFailure = null;
                return true;
            }
            catch (Exception e) { return LayoutReject("mesh_" + e.GetType().Name); }
            finally { try { text.text = ""; } catch { } }
        }
        public bool PreparePacked(PlayerControl player, float angle, float length)
        {
            LayoutFailure = null; PackedCorrection = Vector2.zero; PackedBarLength = 0f;
            var text = player?.cosmetics?.nameText;
            if (text == null) return LayoutReject("missing_text");
            try
            {
                if (!Smooth && !UseDefaultFont(text.font)) return LayoutReject("missing_font");
                if (!NovaBeamRules.Finite(PackedEm) || PackedEm <= 0f) return LayoutReject("invalid_font_scale");
                frameUnits = FrameWorld / PackedEm;
                var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    var vertices = Probe(player, text, angle, Vector2.zero);
                    layoutOrigin = Center(vertices);
                    layoutX = Center(Probe(player, text, angle, new Vector2(1f, 0f))) - layoutOrigin;
                    layoutY = Center(Probe(player, text, angle, new Vector2(0f, 1f))) - layoutOrigin;
                    layoutDet = layoutX.x * layoutY.y - layoutX.y * layoutY.x;
                    if (!NovaBeamRules.Finite(layoutDet) || Mathf.Abs(layoutDet) < .000001f) return LayoutReject("singular_layout");
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (var vertex in vertices) { float along = Vector2.Dot(vertex, dir); lo = Mathf.Min(lo, along); hi = Mathf.Max(hi, along); }
                    PackedBarLength = hi - lo;
                    if (!NovaBeamRules.Finite(PackedBarLength) || PackedBarLength < .001f) return LayoutReject("invalid_glyph_length");
                    var near = ToLayout(Vector2.zero); var far = ToLayout(dir * length);
                    float extent = Mathf.Max(Mathf.Max(Mathf.Abs(near.x), Mathf.Abs(near.y)), Mathf.Max(Mathf.Abs(far.x), Mathf.Abs(far.y)));
                    if (!NovaBeamRules.Finite(extent) || extent > 100000f) return LayoutReject("layout_extent");
                    if (extent <= frameUnits * .8f) break;
                    if (attempt == 1) return LayoutReject("layout_frame");
                    frameUnits = extent * 2f + 2f;
                }
                // Validate the native conversion at the origin, middle and far
                // end; the final multi-glyph names are validated separately.
                foreach (float distance in new[] { 0f, length * .5f, length })
                {
                    var points = new[] { dir * distance };
                    if (!ValidatePackedName(player, PackedName(angle, points, Vector2.zero), points, angle, out _)) return false;
                }
                return true;
            }
            catch (Exception e) { return LayoutReject("prepare_" + e.GetType().Name); }
            finally { try { text.text = ""; } catch { } }
        }
        public string BuildTile(Vector2Int cell, float tile, Vector2 dir, Vector2 perp, float half, float start, float length)
    {
        var sb = new StringBuilder($"<size={sizePercent}%>{TilePrefix}");
        var any = false;
        for (var r = 0; r < Grid; r++)
        {
            int first = -1, last = -1;
            for (var c = 0; c < Grid; c++)
            {
                var p = new Vector2(cell.x * tile + (c + 0.5f) * pixel, cell.y * tile + tile - (r + 0.5f) * pixel);
                var along = Vector2.Dot(p, dir);
                if (along < start || along > length) continue;
                if (Mathf.Abs(Vector2.Dot(p, perp)) > half) continue;
                if (first < 0) first = c;
                last = c;
            }
            if (first < 0) sb.Append(string.Concat(Enumerable.Repeat(BlankCell, Grid)));
            else
            {
                any = true;
                sb.Append(string.Concat(Enumerable.Repeat(BlankCell, first)));
                sb.Append(MarkOpen).Append(string.Concat(Enumerable.Repeat(Cell, last - first + 1))).Append("</mark>");
                sb.Append(string.Concat(Enumerable.Repeat(BlankCell, Grid - 1 - last)));
            }
            if (r < Grid - 1) sb.Append('\n');
        }
        return any ? sb.Append("</size>").ToString() : null;
    }
    }
    readonly RenderProfile profile;
    readonly Func<bool> ownerCurrent;
    readonly int ownerGame;
    bool failed;
    public bool Failed => failed;
    public bool Ready => created && !removed && !failed && PlayerControl != null;
    public bool Committed => Ready && Vector2.Distance(Position, wanted) < .02f
        && (style != Style.Beam || !beamPacked || shownName == beamArt);
    protected override bool BatchSpawnAssignments => true;
    protected override float SpawnInterval => 1f;
    protected override bool CanCreate => !removed && ownerCurrent() && GameStates.IsInTask
        && !GameStates.CalledMeeting && !GameStates.IsMeeting;
    private bool created;
    private bool removed;
    private Style style = Style.Hidden;
    private string shownName;
    private string beamArt;
    private bool beamSmooth, beamPacked;

    // ===== なめらかなビーム用 =====
    // ゲームに入っている、ふち(黒い外枠)の無いフォントを探して、その「-」を回して使う。見つからない時はマス目で描く
    private const char BarChar = '-';
    private static readonly string[] SmoothFonts = { "Barlow-Black SDF", "Barlow-ExtraBold SDF", "Barlow-Bold SDF", "Barlow-Regular SDF" };
    // 実際の画面(録画)で測った補正。棒は計算より 1.4 倍くらい長く見え、0.15 くらい下に出ていた
    private const float BarLengthFix = 1.4f;
    private const float BarRaise = 0.15f;

    

    
    private Vector2 wanted = ParkPos;

    public NovaBeamTile(RenderProfile render, Func<bool> current, int game)
    { profile = render; ownerCurrent = current; ownerGame = game; CreateNetObject(ParkPos); }

    // ビームの太さに合わせて、1 マスの大きさを決める
    

    // 照準は、波動砲の足元の星と同じ★。向きを決めた後は赤くなる
    public string GuideColor = "#FFFFFF";
    private static string GuideName(bool locked, string color) => locked
        ? "<size=500%><color=#FF2040>★</color></size>"
        : $"<size=500%><color={color}>★</color></size>";

    protected override void OnCreateFailed()
    { failed = true; Logger.Warn("NOVA_PREPARE_FAILED assignment batch incomplete; attack disabled", "NovaTraffic"); }

    protected override void OnCreated()
    {
        if (removed) { Despawn(); return; }
        created = true;
        try { if (!profile.Measured) profile.Measure(PlayerControl, GuideColor); }
        catch (Exception e) { FailVisual(e.GetType().Name); return; }
        if (!SendTileName("")) return;
        shownName = "";
        CommitPosition();
        Apply();
        _ = new LateTask(() => { if (CanCreate && Ready) HideBody(); }, 0.3f, "NovaBeamTile.HideBody", true);
    }

    // ホストの画面で、実際に文字を置いて大きさとずれを測る(どの人の画面でも同じ文字なので、ここで測れば全員に合う)
    

    

    // マス目の絵を作る。マスの真ん中がビームの帯の中にあれば塗る。何も塗らない絵は null
    

    // ホストの画面では、ダミーの体が見えてしまうので、名前の文字以外を消す
    // Use Shell's existing appearance transaction, not a SetName RPC referring
    // to nonexistent dummy GameData. Always restore the host outfit on failure.
    bool SendTileName(string name)
    {
        if (failed || removed) return false;
        try
        {
            if (Encoding.UTF8.GetByteCount(name) > NetworkSafety.NameByteLimit)
                throw new InvalidOperationException("Visual name exceeds budget");
            if (!CanCreate || PlayerControl == null) throw new InvalidOperationException("Visual owner retired");
            SetVisualName(name);
            if (PlayerControl.cosmetics?.nameText != null) PlayerControl.cosmetics.nameText.text = name;
            return true;
        }
        catch (Exception e) { FailVisual(e.GetType().Name); return false; }
    }
    void FailVisual(string reason)
    {
        if (failed) return;
        failed = true;
        Logger.Warn("NOVA_VISUAL_FAILED " + reason + "; visual retired, attack disabled", "NovaTraffic");
        // Despawn does not read outfits or update display names, unlike Park/Apply.
        Remove();
    }
    void CommitPosition()
    {
        if (Ready && CanCreate && Vector2.Distance(Position, wanted) >= .01f && NovaVisualTraffic.Take(ownerGame, Time.time))
        {
            try { SnapToPosition(wanted); }
            catch (Exception e) { FailVisual(e.GetType().Name); }
        }
    }
    protected override void OnFixedUpdate()
    {
        if (removed) return;
        if (failed || !CanCreate) { Remove(); return; }
        CommitPosition();
        if (style == Style.Beam && beamPacked) Apply();
    }
    private void HideBody()
    {
        if (PlayerControl == null) return;
        try
        {
            foreach (var r in PlayerControl.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = false;
            PlayerControl.cosmetics.colorBlindText.color = Color.clear;
        }
        catch { }
    }

    public void SetStyle(Style next)
    {
        style = next;
        if (created && !removed) Apply();
    }

    public void ShowBeam(string art, bool smooth = false, bool packed = false)
    {
        beamSmooth = smooth; beamPacked = packed;
        style = Style.Beam;
        beamArt = art;
        if (created && !removed) Apply();
    }

    private void Apply()
    {
        // Keep packed art hidden until its standard position RPC is committed.
        if (style == Style.Beam && beamPacked && Vector2.Distance(Position, wanted) >= .02f) return;
        var name = style switch
        {
            Style.Guide => GuideName(false, GuideColor),
            Style.Locked => GuideName(true, GuideColor),
            Style.Beam => beamArt ?? "",
            _ => "",
        };
        if (name == shownName) return;
        if (SendTileName(name)) shownName = name;
        HideBody();
    }

    // 絵の真ん中が、point に来るように置く
    public void MoveTo(Vector2 point, bool packed = false)
    {
        if (packed) beamPacked = true;
        var offset = packed ? Vector2.zero : style != Style.Beam ? profile.guideOffset : beamPacked ? Vector2.zero : beamSmooth ? profile.tileOffset + profile.barShift : profile.tileOffset;
        wanted = point - offset;
        if (!created || removed) return;
        if (Vector2.Distance(Position, wanted) < 0.01f) return;
        CommitPosition();
    }

    public void Park()
    {
        style = Style.Hidden;
        wanted = ParkPos;
        if (failed || !CanCreate) { Remove(); return; }
        if (!created || removed) return;
        Apply();
        if (failed || removed) return;
        // もう外にある時は送らない(通信を減らす)
        if (Vector2.Distance(Position, ParkPos) < 0.02f) return;
        CommitPosition();
    }

    public void Remove()
    {
        if (removed) return;
        removed = true;
        try { if (PlayerControl?.cosmetics?.nameText != null) PlayerControl.cosmetics.nameText.text = ""; } catch { }
        try { if (PlayerControl != null) Despawn(); }
        finally { AllObjects.Remove(this); }
    }

    // 会議では消さずに、マップの外へ戻すだけ(作り直すと時間がかかるため)
    public override void OnMeeting() => Park();
}
