using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Streamer : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(Streamer), player => new Streamer(player), CustomRoles.Streamer,
        () => RoleTypes.Crewmate, CustomRoleTypes.Neutral, 97300, SetupOptionItem,
        "str", "#FF7F50", (6, 3), from: From.TownOfHost_Shell);
    enum OptionName { StreamerArrivalTime, StreamerExposureTime, StreamerCarryExposure, StreamerSoloWin }
    static OptionItem ArrivalTime, ExposureTime, CarryExposure, SoloWin;
    readonly RecordingState recording = new();
    PlainShipRoom room;
    SystemTypes roomId, previousRoom;
    bool hasRoom, hasPreviousRoom, active, pending = true;

    public Streamer(PlayerControl player) : base(RoleInfo, player) { }
    static void SetupOptionItem()
    {
        ArrivalTime = FloatOptionItem.Create(RoleInfo, 10, OptionName.StreamerArrivalTime,
            new ClampedFloatValueRule(20f, 60f, 2.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        ExposureTime = FloatOptionItem.Create(RoleInfo, 11, OptionName.StreamerExposureTime,
            new ClampedFloatValueRule(2.5f, 60f, 2.5f), 25f, false).SetValueFormat(OptionFormat.Seconds);
        CarryExposure = BooleanOptionItem.Create(RoleInfo, 12, OptionName.StreamerCarryExposure, false, false);
        SoloWin = BooleanOptionItem.Create(RoleInfo, 13, OptionName.StreamerSoloWin, false, false);
    }

    // Independent per Streamer. Pauses never erase exposure; only BeginTurn may reset it.
    internal sealed class RecordingState
    {
        public bool Arrived;
        public float ArrivalLeft, Exposure;
        public void BeginTurn(float arrival, bool carry)
        {
            Arrived = false;
            ArrivalLeft = arrival;
            if (!carry) Exposure = 0f;
        }
        public bool Tick(float elapsed, bool inside, bool someone, float exposureLimit)
        {
            if (!Arrived)
            {
                if (inside) { Arrived = true; ArrivalLeft = 0f; }
                else { ArrivalLeft = System.Math.Max(0f, ArrivalLeft - elapsed); return ArrivalLeft <= 0f; }
            }
            if (inside && someone) Exposure += elapsed;
            return Exposure >= exposureLimit;
        }
    }

    static bool RealAlive(PlayerControl pc) => pc != null && pc.Data != null && pc.PlayerId <= 15
        && !pc.Data.Disconnected && pc.IsAlive() && !pc.Is(CustomRoles.GM)
        && !pc.IsTestBot() && !pc.isDummy && !pc.notRealPlayer;
    static bool OnFloor(PlayerControl pc) => !pc.inVent && !pc.walkingToVent && !pc.onLadder
        && !pc.inMovingPlat && pc.Collider != null && pc.Collider.enabled;
    bool InRoom(PlayerControl pc) => room != null && room.roomArea != null && OnFloor(pc)
        && room.roomArea.OverlapPoint(pc.GetTruePosition());

    bool ChooseRoom()
    {
        if (ShipStatus.Instance == null) return false;
        RandomSpawn.SpawnMap map = Main.NormalOptions.MapId switch
        {
            0 => new RandomSpawn.SkeldSpawnMap(), 1 => new RandomSpawn.MiraHQSpawnMap(),
            2 => new RandomSpawn.PolusSpawnMap(), 4 => new RandomSpawn.AirshipSpawnMap(),
            5 => new RandomSpawn.FungleSpawnMap(), _ => null
        };
        if (map == null) return false;
        var candidates = new List<PlainShipRoom>();
        var ids = new HashSet<SystemTypes>();
        // Like the disaster room check, require a real room containing a known map floor anchor.
        foreach (var candidate in ShipStatus.Instance.AllRooms)
        {
            if (candidate == null || candidate.roomArea == null || !candidate.roomArea.enabled
                || !candidate.gameObject.activeInHierarchy || ids.Contains(candidate.RoomId)
                || candidate.RoomId is SystemTypes.Hallway or SystemTypes.Outside
                    or SystemTypes.Decontamination2 or SystemTypes.Decontamination3) continue;
            foreach (Vector2 point in map.Positions.Values)
            {
                if (!candidate.roomArea.OverlapPoint(point)) continue;
                bool clear = true;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    if (!PhysicsHelpers.AnyNonTriggersBetween(point, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                        .2f, Constants.ShipAndObjectsMask)) continue;
                    clear = false;
                    break;
                }
                if (!clear) continue;
                candidates.Add(candidate);
                ids.Add(candidate.RoomId);
                break;
            }
        }
        if (candidates.Count > 1 && hasPreviousRoom) candidates.RemoveAll(r => r.RoomId == previousRoom);
        if (candidates.Count == 0) return false;
        room = candidates[IRandom.Instance.Next(candidates.Count)];
        roomId = previousRoom = room.RoomId;
        hasPreviousRoom = hasRoom = true;
        return true;
    }
    void BeginTurn()
    {
        pending = false;
        if (!ChooseRoom())
        {
            active = hasRoom = false;
            Logger.Warn($"No safe room for player={Player.PlayerId}, map={Main.NormalOptions.MapId}; turn paused", "Streamer");
            SyncState();
            return;
        }
        recording.BeginTurn(ArrivalTime.GetFloat(), CarryExposure.GetBool());
        active = true;
        SyncState();
    }
    public override void AfterMeetingTasks() => pending = true;
    public override void OnStartMeeting()
    {
        active = pending = hasRoom = false;
        room = null;
        if (AmongUsClient.Instance.AmHost) SyncState();
    }
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !RealAlive(Player) || !GameStates.IsInTask
            || GameStates.Intro || GameStates.CalledMeeting || GameStates.IsMeeting
            || CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;
        // Wait for spawn selection/scripted movement; the arrival timer starts on the task floor.
        if (pending && Player.moveable && OnFloor(Player)) BeginTurn();
        if (!active) return;
        bool inside = InRoom(Player), someone = false;
        if (inside)
        {
            // Existing medium vanilla kill distance; no new distance option or global mutation.
            float range = NormalGameOptionsV12.KillDistances[1];
            foreach (var other in PlayerCatch.AllPlayerControls)
            {
                if (!RealAlive(other) || other.PlayerId == Player.PlayerId || !InRoom(other)) continue;
                Vector2 delta = other.GetTruePosition() - Player.GetTruePosition();
                if (delta.sqrMagnitude > range * range) continue;
                if (delta.sqrMagnitude > .0001f && PhysicsHelpers.AnyNonTriggersBetween(Player.GetTruePosition(),
                    delta.normalized, delta.magnitude, Constants.ShipAndObjectsMask)) continue;
                someone = true;
                break; // Count real seconds, never multiply by the number of people.
            }
        }
        bool wasArrived = recording.Arrived;
        int beforeArrival = Mathf.CeilToInt(recording.ArrivalLeft), beforeExposure = Mathf.FloorToInt(recording.Exposure);
        bool die = recording.Tick(Time.fixedDeltaTime, inside, someone, ExposureTime.GetFloat());
        if (die)
        {
            active = pending = false;
            SyncState();
            CustomRoleManager.OnCheckMurder(Player, Player, Player, Player, true, true, 99, CustomDeathReason.Suicide);
            return;
        }
        if (wasArrived != recording.Arrived || beforeArrival != Mathf.CeilToInt(recording.ArrivalLeft)
            || beforeExposure != Mathf.FloorToInt(recording.Exposure)) SyncState();
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (!active || !hasRoom || isForMeeting || !RealAlive(Player) || seer == null || seen == null
            || seer.PlayerId != Player.PlayerId || seen.PlayerId != Player.PlayerId) return "";
        string location = GetString(roomId.ToString());
        return recording.Arrived
            ? string.Format(GetString("StreamerRecording"), location, Mathf.FloorToInt(recording.Exposure), ExposureTime.GetFloat())
            : string.Format(GetString("StreamerTravel"), location, Mathf.CeilToInt(recording.ArrivalLeft));
    }
    void SyncState()
    {
        using var sender = CreateSender(SendOption.Reliable);
        sender.Writer.Write(active);
        sender.Writer.Write(hasRoom);
        sender.Writer.Write((byte)roomId);
        sender.Writer.Write(recording.Arrived);
        sender.Writer.Write(recording.ArrivalLeft);
        sender.Writer.Write(recording.Exposure);
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        active = reader.ReadBoolean(); hasRoom = reader.ReadBoolean(); roomId = (SystemTypes)reader.ReadByte();
        recording.Arrived = reader.ReadBoolean(); recording.ArrivalLeft = reader.ReadSingle(); recording.Exposure = reader.ReadSingle();
    }
    void Stop()
    {
        active = pending = hasRoom = false;
        room = null;
        if (AmongUsClient.Instance.AmHost) SyncState();
    }
    public override void OnDead(PlayerControl player) { if (Is(player)) Stop(); }
    public override void OnLeftPlayer(PlayerControl player) { if (player != null && Is(player)) Stop(); }
    public override void OnDestroy()
    {
        active = pending = hasRoom = hasPreviousRoom = false;
        room = null;
        recording.BeginTurn(0f, false);
    }

    // Resolve once after the existing winner pipeline, never trigger game end ourselves.
    public static void ApplyEndWinners()
    {
        if (CustomWinnerHolder.WinnerTeam is CustomWinner.Default or CustomWinner.Draw or CustomWinner.None) return;
        var survivors = new List<byte>();
        var dead = new List<byte>();
        foreach (var role in CustomRoleManager.AllActiveRoles.Values)
        {
            if (role is not Streamer streamer) continue;
            if (RealAlive(streamer.Player)) survivors.Add(streamer.Player.PlayerId);
            else dead.Add(streamer.Player.PlayerId);
        }
        if (survivors.Count == 0 && dead.Count == 0) return;
        if (survivors.Count > 0 && SoloWin.GetBool())
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Streamer);
        // Never award all members through WinnerRoles: dead/disconnected Streamers must lose.
        CustomWinnerHolder.WinnerRoles.Remove(CustomRoles.Streamer);
        foreach (byte id in dead)
        {
            CustomWinnerHolder.WinnerIds.Remove(id);
            CustomWinnerHolder.CantWinPlayerIds.Add(id);
        }
        if (survivors.Count == 0) return;
        foreach (byte id in survivors)
        {
            CustomWinnerHolder.CantWinPlayerIds.Remove(id);
            CustomWinnerHolder.WinnerIds.Add(id);
        }
        if (!SoloWin.GetBool()) CustomWinnerHolder.AdditionalWinnerRoles.Add(CustomRoles.Streamer);
    }
}
