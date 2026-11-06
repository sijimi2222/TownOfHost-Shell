// Adapted from the supplied TOH-hamo main Nova (GNU GPL v3; see LICENSE).
// Upstream SHA-256: fdb3a6b244a88662e9c1dd29820817088742b404609a27744fd60be7f788f465
using System;
using System.Collections.Generic;
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
    private const float BeamLength = 10f;
    // 今回のビームが届く長さ(壁に当たったらそこまで)
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
    private const int MaxTiles = 16;

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
        var owner = PlayerCatch.GetPlayerById(ownerId);
        if (owner != null && owner.NetId == ownerNetId
            && Main.AllPlayerSpeed.TryGetValue(ownerId, out float now) && Mathf.Abs(now - forcedSpeed) < .0001f)
        {
            Main.AllPlayerSpeed[ownerId] = savedSpeed;
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
        // The shared queue serializes creation; include the short-wall fallback.
        if (tilesSpawned && restSpawnAt >= 0f && (render.Measured || Time.time >= restSpawnAt))
        {
            restSpawnAt = -1f;
            for (var i = tiles.Count; i < TileCount(); i++) tiles.Add(new NovaBeamTile(render, () => Phase, gameId));
        }
        if (lastVisualStatus != VisualStatus)
        {
            lastVisualStatus = VisualStatus; SendRpc(); UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
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

    // ビームを描くのに必要なダミーの数(マス目の絵 1 枚が受け持つ広さから決める)
    private int TileCount()
    {
        float tile = render.TileWorldSize;
        int grid = Mathf.Clamp(Mathf.CeilToInt((BeamLength + beamSize) / tile * 1.42f) + 3, 4, MaxTiles);
        if (!render.Smooth) return grid + 1;
        float bar = render.BarLength, span = BeamLength - BeamStart;
        int bars = span <= bar ? 1 : Mathf.CeilToInt((span - bar) / (bar * .7f)) + 1;
        return Mathf.Max(grid, Mathf.Clamp(bars, 1, MaxTiles)) + 1;
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
        SendRpc();
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
        if (fireDelay <= 0f) Fire();
    }

    private void Stop()
    {
        charging = false;
        waiting = false;
        beaming = false; beamVisible = false; attempted.Clear();
        RestoreSpeed();
        foreach (var tile in tiles) tile.Park();
        SendRpc();
        UtilsNotifyRoles.NotifyRoles(ForceLoop: true);
    }

    private void Fire()
    {
        if (!Phase || !TilesReady) { Stop(); return; }
        charging = waiting = false; beaming = true; beamVisible = false;
        attempted.Clear(); visualDeadline = Time.time + 2f;
        SetSpeed(Main.MinSpeed);
        reach = WallDistance(anchor, Dir);
        if (reach <= BeamStart || !DrawBeam())
        {
            Logger.Warn("Beam has no safe drawable coverage; attack cancelled.", "Nova"); Stop(); return;
        }
        if (tiles.Count > 0) tiles[0].Park();
        SendRpc(); UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
    }

    // 撃つ向きに、壁(扉も)がどこにあるかを少しずつ確かめる。壁より先には届かない
    private static float WallDistance(Vector2 from, Vector2 dir)
    {
        const float step = 0.25f;
        for (var t = step; t <= BeamLength; t += step)
        {
            if (PhysicsHelpers.AnyNonTriggersBetween(from, dir, t, Constants.ShipAndObjectsMask))
                return Mathf.Max(0f, t - step);
        }
        return BeamLength;
    }

    // ビームの帯を、マス目(1 マスはビームの太さの 1/4 くらい)で塗り、マス目の絵を何枚かのダミーに分けて貼る。
    // 絵どうしは同じマス目の上に並ぶので、つなぎ目がずれず、角度があっても段差がマス目 1 つぶんだけになる。
    private bool DrawBeam()
    {
        if (render.Smooth && reach - BeamStart >= render.BarLength) return DrawSmoothBeam();
        var dir = Dir;
        var perp = new Vector2(-dir.y, dir.x);
        var tile = render.TileWorldSize;
        var half = beamSize * 0.5f;
        var used = new List<Vector2Int>();
        for (var t = 0f; t <= reach + tile; t += tile * 0.25f)
        {
            for (var k = -1; k <= 1; k++)
            {
                var p = dir * Mathf.Min(t, reach) + perp * (half * k);
                var cell = new Vector2Int(Mathf.FloorToInt(p.x / tile), Mathf.FloorToInt(p.y / tile));
                if (!used.Contains(cell)) used.Add(cell);
            }
        }
        var artTiles = used.Select(cell => (cell, art: render.BuildTile(cell, tile, dir, perp, half, BeamStart, reach)))
            .Where(x => x.art != null).ToArray();
        if (artTiles.Length == 0 || artTiles.Length > tiles.Count - 1) return false;
        var index = 1;
        foreach (var sample in artTiles.OrderBy(c => Vector2.Dot(((Vector2)c.cell + Vector2.one * 0.5f) * tile, dir)))
        {
            tiles[index].ShowBeam(sample.art);
            tiles[index].MoveTo(anchor + ((Vector2)sample.cell + Vector2.one * 0.5f) * tile);
            index++;
        }
        for (; index < tiles.Count; index++) tiles[index].Park();
        return true;
    }

    // なめらかなビーム: ふちの無いフォントの「-」をビームの向きに回し、少しずつ重ねて並べる
    private bool DrawSmoothBeam()
    {
        var dir = Dir;
        var bar = render.BarLength;
        var span = reach - BeamStart;
        var count = span <= bar ? 1 : Mathf.CeilToInt((span - bar) / (bar * 0.7f)) + 1;
        if (count > tiles.Count - 1) return false;
        var first = BeamStart + bar * 0.5f;
        var last = Mathf.Max(first, reach - bar * 0.5f);
        var art = render.BarName(angle);
        for (var i = 0; i < count; i++)
        {
            var t = count == 1 ? first : Mathf.Lerp(first, last, i / (float)(count - 1));
            tiles[i + 1].ShowBeam(art, true);
            tiles[i + 1].MoveTo(anchor + dir * t);
        }
        for (var i = count + 1; i < tiles.Count; i++) tiles[i].Park();
        return count > 0;
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
        foreach (var tile in tiles) tile.Remove();
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
        Logger.Info("ふちの無いフォントが見つからないので、ビームはマス目で描きます", "Nova");
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
    public bool Committed => Ready && Vector2.Distance(Position, wanted) < .02f;
    protected override bool CanCreate => !removed && ownerCurrent() && GameStates.IsInTask
        && !GameStates.CalledMeeting && !GameStates.IsMeeting;
    private bool created;
    private bool removed;
    private Style style = Style.Hidden;
    private string shownName;
    private string beamArt;
    private bool beamSmooth;

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

    protected override void OnCreated()
    {
        if (removed) { Despawn(); return; }
        created = true;
        if (!profile.Measured) profile.Measure(PlayerControl, GuideColor);
        SendTileName("");
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
    void SendTileName(string name)
    {
        if (Encoding.UTF8.GetByteCount(name) > NetworkSafety.NameByteLimit)
        {
            failed = true; Logger.Warn("Nova visual exceeds the safe name budget; attack disabled.", "Nova"); return;
        }
        var host = global::PlayerControl.LocalPlayer;
        if (!CanCreate || host?.Data == null || PlayerControl == null) { failed = true; return; }
        var outfit = host.Data.Outfits[PlayerOutfitType.Default];
        var oldName = outfit.PlayerName; var oldColor = outfit.ColorId;
        var oldHat = outfit.HatId; var oldSkin = outfit.SkinId; var oldPet = outfit.PetId; var oldVisor = outfit.VisorId;
        try { SetAppearance(0, "", "", "", "", displayName: name); }
        catch (Exception e) { failed = true; Logger.Warn("Nova visual failed: " + e.GetType().Name, "Nova"); }
        finally
        {
            outfit.PlayerName = oldName; outfit.ColorId = oldColor; outfit.HatId = oldHat;
            outfit.SkinId = oldSkin; outfit.PetId = oldPet; outfit.VisorId = oldVisor;
        }
        if (!failed && PlayerControl.cosmetics?.nameText != null) PlayerControl.cosmetics.nameText.text = name;
    }
    void CommitPosition()
    {
        if (Ready && CanCreate && Vector2.Distance(Position, wanted) >= .01f && NovaVisualTraffic.Take(ownerGame, Time.time))
        {
            try { SnapToPosition(wanted); }
            catch (Exception e) { failed = true; Logger.Warn("Nova visual move failed: " + e.GetType().Name, "Nova"); }
        }
    }
    protected override void OnFixedUpdate()
    {
        if (removed) return;
        if (!CanCreate) { Remove(); return; }
        CommitPosition();
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

    public void ShowBeam(string art, bool smooth = false)
    {
        beamSmooth = smooth;
        style = Style.Beam;
        beamArt = art;
        if (created && !removed) Apply();
    }

    private void Apply()
    {
        var name = style switch
        {
            Style.Guide => GuideName(false, GuideColor),
            Style.Locked => GuideName(true, GuideColor),
            Style.Beam => beamArt ?? "",
            _ => "",
        };
        if (name == shownName) return;
        SendTileName(name);
        if (!failed) shownName = name;
        HideBody();
    }

    // 絵の真ん中が、point に来るように置く
    public void MoveTo(Vector2 point)
    {
        var offset = style != Style.Beam ? profile.guideOffset : beamSmooth ? profile.tileOffset + profile.barShift : profile.tileOffset;
        wanted = point - offset;
        if (!created || removed) return;
        if (Vector2.Distance(Position, wanted) < 0.01f) return;
        CommitPosition();
    }

    public void Park()
    {
        style = Style.Hidden;
        wanted = ParkPos;
        if (!created || removed) return;
        Apply();
        // もう外にある時は送らない(通信を減らす)
        if (Vector2.Distance(Position, ParkPos) < 0.02f) return;
        CommitPosition();
    }

    public void Remove()
    {
        if (removed) return;
        removed = true;
        if (PlayerControl != null) Despawn();
    }

    // 会議では消さずに、マップの外へ戻すだけ(作り直すと時間がかかるため)
    public override void OnMeeting() => Park();
}
