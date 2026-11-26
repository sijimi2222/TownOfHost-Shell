using System;
using System.Collections.Generic;
using UnityEngine;
using TownOfHost.Roles.Core;

namespace TownOfHost.Modules;

// ナイス/イビルハーミットの共通部品。
// ・照準: ノヴァと同じ仕組み(体をほぼ止め、歩こうとした向きで照準を回す。離れすぎたら元の位置へ戻す)
// ・空間探し: 指定した方向の「最小距離〜最大距離」で、一番近い「立てる場所」を探す
//   (立てる場所 = いずれかの部屋/通路の範囲内で、すぐそばに壁が無く、既知の床の点から壁を越えずにまっすぐ届く場所)
//   壁は厚みの両側に線があるだけなので、「近くに壁が無いか」だけでは厚い壁の中が通ってしまう(2026-11-26 報告)。
//   そこで天災モード(NaturalDisasters.IsReachableFloor)と同じく、床の点から届くかを確かめる。
internal sealed class HermitWarp
{
    private const float AimSpeed = 0.15f;
    private const float AimDeadZone = 0.01f;
    private const float AimLeash = 0.25f;
    private const float AimTurnSpeed = 90f;
    private const float SnapInterval = 0.15f;
    private const float NotifyInterval = 0.2f;
    private const float SearchStep = 0.1f;
    private const float Clearance = 0.2f;

    private readonly PlayerControl player;
    private readonly byte ownerId;
    public bool Loaded { get; private set; }
    private Vector2 aimHome, lastAimPos;
    private float angle, aimTarget, ignoreAimUntil, nextSnapAt, nextNotifyAt;
    private int lastShownAngle = int.MinValue;
    private float savedSpeed = -1f, forcedSpeed = -1f;

    public HermitWarp(PlayerControl player)
    {
        this.player = player;
        ownerId = player.PlayerId;
    }

    public int ShownAngle => ((Mathf.RoundToInt(angle) % 360) + 360) % 360;

    public string Arrow
    {
        get
        {
            string[] arrows = { "→", "↗", "↑", "↖", "←", "↙", "↓", "↘" };
            return arrows[Mathf.RoundToInt(ShownAngle / 45f) % 8];
        }
    }

    public bool OnLadder => player.onLadder || player.inMovingPlat || player.inVent
        || player.MyPhysics == null || player.MyPhysics.Animations.IsPlayingAnyLadderAnimation();

    // 装填: 向きを読み始める(初めの向きは体の向き)
    public bool Load()
    {
        if (Loaded || !player.IsAlive() || OnLadder) return false;
        Loaded = true;
        aimHome = player.transform.position;
        lastAimPos = aimHome;
        angle = aimTarget = player.cosmetics.FlipX ? 180f : 0f;
        ignoreAimUntil = 0f;
        lastShownAngle = int.MinValue;
        SetSpeed(AimSpeed);
        return true;
    }

    // 装填の取り消し(速さを元に戻す)
    public void Cancel()
    {
        Loaded = false;
        RestoreSpeed();
    }

    // 照準の更新。向きの表示を出し直す必要があれば true
    public bool UpdateAim()
    {
        if (!Loaded) return false;
        var pos = (Vector2)player.transform.position;
        if (Time.time < ignoreAimUntil) lastAimPos = pos;
        else
        {
            var delta = pos - lastAimPos;
            if (delta.magnitude >= AimDeadZone)
            {
                lastAimPos = pos;
                aimTarget = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                // 歩こうとしている間だけ、その向きへ少しずつ回る(ノヴァの自由モードと同じ)
                angle = Mathf.MoveTowardsAngle(angle, aimTarget, AimTurnSpeed * Time.fixedDeltaTime * 3f);
                angle = Mathf.DeltaAngle(0f, angle);
            }
            if (Vector2.Distance(pos, aimHome) > AimLeash && Time.time >= nextSnapAt)
            {
                nextSnapAt = Time.time + SnapInterval;
                player.RpcSnapToForced(aimHome);
                lastAimPos = aimHome;
                ignoreAimUntil = Time.time + 0.25f;
            }
        }
        if (ShownAngle != lastShownAngle && Time.time >= nextNotifyAt)
        {
            lastShownAngle = ShownAngle;
            nextNotifyAt = Time.time + NotifyInterval;
            return true;
        }
        return false;
    }

    // 発射: 空間があればテレポートして true、無ければ false(呼び出し側で自殺させる)
    public bool Fire(float minDistance, float maxDistance, bool wallOnly)
    {
        float shotAngle = angle;
        Cancel();
        var truePos = player.GetTruePosition();
        var offset = truePos - (Vector2)player.transform.position;
        var anchors = FloorAnchors(player);
        if (!TryFindSpace(truePos, shotAngle, minDistance, maxDistance, wallOnly, anchors, out var point)) return false;
        player.RpcSnapToForced(point - offset);
        return true;
    }

    public static bool TryFindSpace(Vector2 origin, float angleDeg, float minDistance, float maxDistance, bool wallOnly, List<Vector2> anchors, out Vector2 point)
    {
        point = default;
        if (ShipStatus.Instance == null) return false;
        var dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
        float start = Mathf.Max(SearchStep, minDistance);
        for (float d = start; d <= maxDistance + 0.0001f; d += SearchStep)
        {
            var p = origin + dir * d;
            if (wallOnly && !PhysicsHelpers.AnyNonTriggersBetween(origin, dir, d, Constants.ShipAndObjectsMask)) continue;
            if (!IsStandable(p) || !IsReachable(p, anchors)) continue;
            point = p;
            return true;
        }
        return false;
    }

    public static bool IsStandable(Vector2 p)
    {
        bool inRoom = false;
        foreach (var room in ShipStatus.Instance.AllRooms)
        {
            if (room?.roomArea != null && room.roomArea.OverlapPoint(p)) { inRoom = true; break; }
        }
        if (!inRoom) return false;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            if (PhysicsHelpers.AnyNonTriggersBetween(p, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), Clearance, Constants.ShipAndObjectsMask)) return false;
        }
        return true;
    }

    // 床だと分かっている点: マップのスポーン地点(ランダムスポーンの一覧)、ベント、自分以外の生存者の足元
    static List<Vector2> FloorAnchors(PlayerControl self)
    {
        var anchors = new List<Vector2>();
        RandomSpawn.SpawnMap map = Main.NormalOptions.MapId switch
        {
            0 => new RandomSpawn.SkeldSpawnMap(),
            1 => new RandomSpawn.MiraHQSpawnMap(),
            2 => new RandomSpawn.PolusSpawnMap(),
            4 => new RandomSpawn.AirshipSpawnMap(),
            5 => new RandomSpawn.FungleSpawnMap(),
            _ => null
        };
        if (map != null) anchors.AddRange(map.Positions.Values);
        if (ShipStatus.Instance?.AllVents != null)
            foreach (var vent in ShipStatus.Instance.AllVents)
                if (vent != null) anchors.Add(vent.transform.position);
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            if (pc != null && pc.PlayerId != self.PlayerId && !pc.inVent && !pc.onLadder && !pc.inMovingPlat) anchors.Add(pc.GetTruePosition());
        return anchors;
    }

    static bool IsReachable(Vector2 p, List<Vector2> anchors)
    {
        foreach (var anchor in anchors)
        {
            var delta = p - anchor;
            if (delta.sqrMagnitude < .04f) return true;
            if (!PhysicsHelpers.AnyNonTriggersBetween(anchor, delta.normalized, delta.magnitude, Constants.ShipAndObjectsMask)) return true;
        }
        return false;
    }

    private void SetSpeed(float speed)
    {
        if (savedSpeed < 0f) savedSpeed = Main.AllPlayerSpeed.TryGetValue(ownerId, out float old) ? old : 1f;
        forcedSpeed = speed;
        Main.AllPlayerSpeed[ownerId] = speed;
        player.MarkDirtySettings();
    }

    private void RestoreSpeed()
    {
        if (savedSpeed < 0f) return;
        float restore = savedSpeed, expected = forcedSpeed;
        savedSpeed = forcedSpeed = -1f;
        // 他の効果で速さが変わっていたら、上書きしない(ノヴァと同じ)
        if (Main.AllPlayerSpeed.TryGetValue(ownerId, out float now) && Mathf.Abs(now - expected) < .0001f)
        {
            Main.AllPlayerSpeed[ownerId] = restore;
            player.MarkDirtySettings();
        }
    }
}
