using System.Collections.Generic;
using UnityEngine;

namespace TownOfHost;

public static partial class NaturalDisasters
{
    static readonly List<Vector2> mapFloorAnchors = new();
    public static bool SupportsMap(byte map) => map is 0 or 1 or 2 or 4 or 5;

    static void InitializeMap()
    {
        mapFloorAnchors.Clear();
        RandomSpawn.SpawnMap map = Main.NormalOptions.MapId switch
        {
            0 => new RandomSpawn.SkeldSpawnMap(),
            1 => new RandomSpawn.MiraHQSpawnMap(),
            2 => new RandomSpawn.PolusSpawnMap(),
            4 => new RandomSpawn.AirshipSpawnMap(),
            5 => new RandomSpawn.FungleSpawnMap(),
            _ => null
        };
        if (map != null) mapFloorAnchors.AddRange(map.Positions.Values);
    }
    static bool HasFloorClearance(Vector2 point)
    {
        // The spawn anchors establish reachability; these short rays reject wall-adjacent points.
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            if (PhysicsHelpers.AnyNonTriggersBetween(point, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), .2f, Constants.ShipAndObjectsMask)) return false;
        }
        return true;
    }
    static bool IsReachableFloor(Vector2 point)
    {
        if (!HasFloorClearance(point)) return false;
        foreach (var anchor in mapFloorAnchors)
        {
            Vector2 delta = point - anchor;
            if (delta.sqrMagnitude < .04f) return true;
            if (!PhysicsHelpers.AnyNonTriggersBetween(anchor, delta.normalized, delta.magnitude, Constants.ShipAndObjectsMask)) return true;
        }
        return false;
    }
    static bool TryDisasterPosition(PlayerControl[] players, out Vector2 point)
    {
        point = default;
        // Keep the established player-centered warning, but exclude transit/unsafe positions.
        // With no survivors, skip player sampling and use the existing map-floor fallback.
        int first = players.Length > 0 ? IRandom.Instance.Next(players.Length) : 0;
        for (int i = 0; i < players.Length; i++)
        {
            var pc = players[(first + i) % players.Length];
            if (pc.inVent || pc.onLadder || pc.inMovingPlat || pc.Collider == null || !pc.Collider.enabled) continue;
            point = pc.GetTruePosition();
            if (IsReachableFloor(point)) return true;
        }
        // Only existing map-specific spawn points; never sample an enclosing rectangle.
        if (mapFloorAnchors.Count == 0) return false;
        first = IRandom.Instance.Next(mapFloorAnchors.Count);
        for (int i = 0; i < mapFloorAnchors.Count; i++)
        {
            point = mapFloorAnchors[(first + i) % mapFloorAnchors.Count];
            if (HasFloorClearance(point)) return true;
        }
        return false;
    }
    static bool TryRoomFloor(PlainShipRoom room, out Vector2 point)
    {
        point = default;
        if (room == null || room.roomArea == null || !room.roomArea.enabled || !room.gameObject.activeInHierarchy
            || room.RoomId is SystemTypes.Hallway or SystemTypes.Outside or SystemTypes.Decontamination2 or SystemTypes.Decontamination3) return false;
        // Require an actual usable floor point, not the center of a room's bounding rectangle.
        foreach (var anchor in mapFloorAnchors)
        {
            if (!room.roomArea.OverlapPoint(anchor) || !HasFloorClearance(anchor)) continue;
            point = anchor;
            return true;
        }
        foreach (var pc in PlayerControl.AllPlayerControls)
        {
            if (!IsRealSurvivor(pc) || pc.Data.IsDead || pc.inVent || pc.onLadder || pc.inMovingPlat || pc.Collider == null || !pc.Collider.enabled) continue;
            Vector2 pos = pc.GetTruePosition();
            if (!room.roomArea.OverlapPoint(pos) || !IsReachableFloor(pos)) continue;
            point = pos;
            return true;
        }
        return false;
    }
    static Rect MapMovementBounds(Vector2 origin)
    {
        float left = origin.x, right = origin.x, bottom = origin.y, top = origin.y;
        foreach (var point in mapFloorAnchors)
        {
            left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x);
            bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y);
        }
        foreach (var room in ShipStatus.Instance.AllRooms)
        {
            if (room == null || room.roomArea == null || !room.roomArea.enabled) continue;
            var bounds = room.roomArea.bounds;
            left = Mathf.Min(left, bounds.min.x); right = Mathf.Max(right, bounds.max.x);
            bottom = Mathf.Min(bottom, bounds.min.y); top = Mathf.Max(top, bounds.max.y);
        }
        return Rect.MinMaxRect(left - 2, bottom - 2, right + 2, top + 2);
    }
}
