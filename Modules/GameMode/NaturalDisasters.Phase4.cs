using System.Collections.Generic;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost;

// EHR 56d510116d640fb63ee5192e71fd2f5377772840: room exclusion and safe-position return.
public static partial class NaturalDisasters
{
    public static string CollapseWarning(byte playerId) => IsActive && IsThisMode && meteor != null
        ? meteor.GetCollapseWarning(playerId) : "";
    static OptionItem thunderDuration, lightningInterval, collapseDuration;
    static void SetupPhase4Options()
    {
// Thunderstorm temporarily disabled
//         thunderDuration = IntegerOptionItem.Create(220040, "NDThunderDuration", new(1, 120, 1), 20, TabGroup.MainSettings, false)
//             .SetValueFormat(OptionFormat.Seconds).SetTag(CustomOptionTags.NaturalDisasters);
//         lightningInterval = IntegerOptionItem.Create(220041, "NDLightningInterval", new(1, 20, 1), 2, TabGroup.MainSettings, false)
//             .SetValueFormat(OptionFormat.Seconds).SetTag(CustomOptionTags.NaturalDisasters);
        // EHR keeps collapsed rooms indefinitely. Shell's bounded lifetime allows cleanup/reopening.
        collapseDuration = new DisasterDurationOption(220042, "NDCollapseDuration", 30)
            .SetValueFormat(OptionFormat.Seconds).SetTag(CustomOptionTags.NaturalDisasters);
    }

    static bool TryChooseCollapseRoom(out PlainShipRoom selected, out Vector2 position)
    {
        selected = null;
        position = Vector2.zero;
        int count = 0;
        foreach (var room in ShipStatus.Instance.AllRooms)
        {
            if (room == null || room.roomArea == null || room.RoomId is SystemTypes.Hallway or SystemTypes.Outside
                or SystemTypes.Decontamination2 or SystemTypes.Decontamination3) continue;
            if (IRandom.Instance.Next(++count) == 0) selected = room;
        }
        if (selected == null) return false;
        position = selected.transform.position;
        if (selected.roomArea.OverlapPoint(position)) return true;
        var bounds = selected.roomArea.bounds;
        position = bounds.center;
        if (selected.roomArea.OverlapPoint(position)) return true;
        // Bounded sampling handles non-rectangular room colliders without an unbounded search.
        for (int i = 0; i < 32; i++)
        {
            position = new Vector2(UnityEngine.Random.Range(bounds.min.x, bounds.max.x), UnityEngine.Random.Range(bounds.min.y, bounds.max.y));
            if (selected.roomArea.OverlapPoint(position)) return true;
        }
        return false;
    }

    sealed partial class DisasterObject
    {
        public bool IsPhase4Disaster => Kind == DisasterKind.Thunderstorm || Kind == DisasterKind.BuildingCollapse;
        PlainShipRoom collapseRoom;
        bool collapseActive, lightningVisible;
        float nextLightning, hideLightningAt, nextRoomCheck, nextSafeRecord;
        readonly List<Vector2> lightningAnchors = new();
        readonly Dictionary<byte, int> collapseWarnings = new();
        readonly Dictionary<byte, SafePlace> safePlaces = new();
        readonly Dictionary<byte, float> nextReturn = new();
        readonly struct SafePlace
        {
            public readonly Vector2 Feet, Snap;
            public SafePlace(PlayerControl pc) { Feet = pc.GetTruePosition(); Snap = pc.transform.position; }
        }
        bool IsCollapsedRoomPoint(Vector2 pos) => collapseRoom != null && collapseRoom.roomArea != null
            && collapseRoom.roomArea.OverlapPoint(pos);
        string CollapseRoomLabel() => Kind == DisasterKind.BuildingCollapse && collapseRoom != null
            ? "\n" + Translator.GetString(collapseRoom.RoomId.ToString()) : "";

        public string GetCollapseWarning(byte id) => !cancelled && !collapseActive
            && collapseWarnings.TryGetValue(id, out int seconds)
            ? "\n" + string.Format(Translator.GetString("NDCollapseWarning"), seconds) : "";

        void UpdateCollapseWarning(PlayerControl pc, int seconds)
        {
            collapseWarnings.TryGetValue(pc.PlayerId, out int previous);
            if (previous == seconds) return;
            if (seconds > 0) collapseWarnings[pc.PlayerId] = seconds;
            else collapseWarnings.Remove(pc.PlayerId);
            if (pc.Data != null && !pc.Data.Disconnected && GameStates.InGame)
                UtilsNotifyRoles.NotifyRoles(NoCache: true, OnlyMeName: true, SpecifySeer: pc);
        }

        void ClearCollapseWarnings()
        {
            var players = PlayerControl.AllPlayerControls;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null) UpdateCollapseWarning(players[i], 0);
            collapseWarnings.Clear();
        }

        bool TryLightningPosition(out Vector2 hit)
        {
            hit = default;
            if (lightningAnchors.Count < 2) return false;
            // Existing Skeld spawn points are trusted floor anchors. Sample their connecting
            // segments, never a map bounding box; reject walls/doors and roofed rooms.
            for (int attempt = 0; attempt < 96; attempt++)
            {
                Vector2 anchor = lightningAnchors[IRandom.Instance.Next(lightningAnchors.Count)];
                Vector2 other = lightningAnchors[IRandom.Instance.Next(lightningAnchors.Count)];
                hit = Vector2.Lerp(anchor, other, UnityEngine.Random.value);
                Vector2 delta = hit - anchor;
                if (delta.sqrMagnitude < .01f || hit.GetPlainShipRoom() != null) continue;
                if (PhysicsHelpers.AnyNonTriggersBetween(anchor, delta.normalized, delta.magnitude, Constants.ShipAndObjectsMask)) continue;
                Vector2 side = new Vector2(-delta.y, delta.x).normalized * .2f;
                if (PhysicsHelpers.AnyNonTriggersBetween(anchor + side, delta.normalized, delta.magnitude, Constants.ShipAndObjectsMask)
                    || PhysicsHelpers.AnyNonTriggersBetween(anchor - side, delta.normalized, delta.magnitude, Constants.ShipAndObjectsMask)) continue;
                // Check a small clearance around the point, including corners beside the path.
                bool clear = true;
                for (int direction = 0; direction < 8; direction++)
                {
                    float angle = direction * Mathf.PI / 4f;
                    if (!PhysicsHelpers.AnyNonTriggersBetween(hit, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), .3f, Constants.ShipAndObjectsMask)) continue;
                    clear = false;
                    break;
                }
                if (clear) return true;
            }
            return false;
        }

        public void RecordSafePositions()
        {
            if (Kind != DisasterKind.BuildingCollapse || cancelled || Time.fixedTime < nextSafeRecord) return;
            nextSafeRecord = Time.fixedTime + .1f;
            var players = PlayerControl.AllPlayerControls;
            for (int i = 0; i < players.Count; i++)
            {
                var pc = players[i];
                if (pc != null && pc.PlayerId < 254)
                    UpdateCollapseWarning(pc, !collapseActive && Ready && IsLivingParticipant(pc)
                        && IsCollapsedRoomPoint(pc.GetTruePosition()) ? Mathf.Max(0, Mathf.CeilToInt(remaining)) : 0);
                if (!IsLivingParticipant(pc) || pc.inVent || pc.onLadder || pc.inMovingPlat) continue;
                if (!IsCollapsedRoomPoint(pc.GetTruePosition())) safePlaces[pc.PlayerId] = new SafePlace(pc);
            }
        }

        public void BeginPhase4()
        {
// Thunderstorm temporarily disabled
//             if (Kind == DisasterKind.Thunderstorm)
//             {
//                 lightningAnchors.AddRange(new RandomSpawn.SkeldSpawnMap().Positions.Values);
//                 nextLightning = Time.fixedTime; // EHR attempts its first strike on activation.
//             }
            // Thunderstorm temporarily disabled: restore else-if when re-enabling.
            // else if (Kind == DisasterKind.BuildingCollapse && collapseRoom != null)
            if (Kind == DisasterKind.BuildingCollapse && collapseRoom != null)
            {
                collapseActive = true;
                ClearCollapseWarnings();
                DebugLog($"BuildingCollapse CollapsedRoom room={collapseRoom.RoomId}");
                ResolvingImpact = true;
                try
                {
                    var players = PlayerControl.AllPlayerControls;
                    for (int i = 0; i < players.Count; i++)
                    {
                        var pc = players[i];
                        if (IsLivingParticipant(pc) && IsCollapsedRoomPoint(pc.GetTruePosition()))
                            KillPhase4(pc, CustomDeathReason.Collapsed, "CollapsedKill");
                    }
                }
                finally { ResolvingImpact = false; }
            }
        }

        public void TickPhase4()
        {
            if (cancelled || !Ready || PlayerControl == null) return;
// Thunderstorm temporarily disabled
//             if (Kind == DisasterKind.Thunderstorm)
//             {
//                 if (lightningVisible && Time.fixedTime >= hideLightningAt) { Show(""); lightningVisible = false; }
//                 if (Time.fixedTime < nextLightning) return;
//                 nextLightning = Time.fixedTime + lightningInterval.GetInt();
//                 if (!TryLightningPosition(out Vector2 hit))
//                 {
//                     DebugLog("Thunderstorm LightningSkipped: no reachable unroofed point");
//                     return;
//                 }
//                 SnapToPosition(hit);
//                 Show(LightningSprite);
//                 lightningVisible = true;
//                 hideLightningAt = Time.fixedTime + .35f;
//                 DebugLog($"Thunderstorm LightningSpawned position={hit}");
//                 ResolvingImpact = true;
//                 try
//                 {
//                     var players = PlayerControl.AllPlayerControls;
//                     for (int i = 0; i < players.Count; i++)
//                     {
//                         var pc = players[i];
//                         if (IsLivingParticipant(pc) && (pc.GetTruePosition() - hit).sqrMagnitude <= .75f * .75f)
//                             KillPhase4(pc, CustomDeathReason.Lightning, "LightningKill");
//                     }
//                 }
//                 finally { ResolvingImpact = false; }
//                 return;
//             }
            if (!collapseActive || Time.fixedTime < nextRoomCheck) return;
            nextRoomCheck = Time.fixedTime + .2f;
            ResolvingImpact = true;
            try
            {
                var players = PlayerControl.AllPlayerControls;
                for (int i = 0; i < players.Count; i++)
                {
                    var pc = players[i];
                    if (!IsLivingParticipant(pc) || pc.onLadder || pc.inMovingPlat || pc.inVent) continue;
                    if (!IsCollapsedRoomPoint(pc.GetTruePosition())) continue;
                    if (nextReturn.TryGetValue(pc.PlayerId, out float due) && Time.fixedTime < due) continue;
                    nextReturn[pc.PlayerId] = Time.fixedTime + .5f;
                    if (safePlaces.TryGetValue(pc.PlayerId, out var safe) && !IsCollapsedRoomPoint(safe.Feet))
                    {
                        pc.RpcSnapToForced(safe.Snap);
                        DebugLog($"BuildingCollapse PlayerReturned PlayerId={pc.PlayerId}, room={collapseRoom.RoomId}");
                    }
                    else KillPhase4(pc, CustomDeathReason.Collapsed, "CollapsedKill");
                }
            }
            finally { ResolvingImpact = false; }
        }
        void KillPhase4(PlayerControl pc, CustomDeathReason reason, string action)
        {
            DebugLog($"{Kind} {action} PlayerId={pc.PlayerId}, position={pc.GetTruePosition()}, distance={Vector2.Distance(pc.GetTruePosition(), Position):F2}");
            CustomRoleManager.OnCheckMurder(pc, pc, pc, pc, true, true, 99, reason);
        }
        string Phase4Sprite() => Kind == DisasterKind.Thunderstorm ? ""
            : "<size=120%><color=#ff4444>×</color></size>\n" + Translator.GetString("NDCollapsedRoom") + CollapseRoomLabel();
        void ResetPhase4()
        {
            ClearCollapseWarnings();
            collapseRoom = null;
            collapseActive = lightningVisible = false;
            nextLightning = hideLightningAt = nextRoomCheck = nextSafeRecord = 0f;
            lightningAnchors.Clear();
            safePlaces.Clear();
            nextReturn.Clear();
        }
        const string LightningSprite = "<size=170%><line-height=97%><cspace=0.16em><mark=#ffff99>WWWW</mark>\n<mark=#ffff99>W</mark><mark=#ffffff>WW</mark><mark=#ffff99>W</mark>\n<mark=#ffff99>W</mark><mark=#ffffff>WW</mark><mark=#ffff99>W</mark>\n<mark=#ffff99>WWWW</mark>";
    }
}
