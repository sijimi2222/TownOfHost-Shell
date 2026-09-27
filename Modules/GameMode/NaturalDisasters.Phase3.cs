using System;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost;

// Movement and rich-text sprites based on EHR 56d510116d640fb63ee5192e71fd2f5377772840.
public static partial class NaturalDisasters
{
    static OptionItem tornadoDuration, tornadoSpeed, tornadoWalls, tornadoTurn, tsunamiSpeed;
    static void SetupPhase3Options()
    {
        tornadoDuration = IntegerOptionItem.Create(220030, "NDTornadoDuration", new(1, 120, 1), 20, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Seconds).SetTag(CustomOptionTags.NaturalDisasters);
        tornadoSpeed = FloatOptionItem.Create(220031, "NDTornadoSpeed", new(0f, 10f, .25f), 2f, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Multiplier).SetTag(CustomOptionTags.NaturalDisasters);
        tornadoWalls = BooleanOptionItem.Create(220032, "NDTornadoWalls", true, TabGroup.MainSettings, false)
            .SetTag(CustomOptionTags.NaturalDisasters);
        tornadoTurn = IntegerOptionItem.Create(220033, "NDTornadoTurn", new(1, 30, 1), 3, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Seconds).SetTag(CustomOptionTags.NaturalDisasters);
        tsunamiSpeed = FloatOptionItem.Create(220034, "NDTsunamiSpeed", new(.25f, 10f, .25f), 2.5f, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Multiplier).SetTag(CustomOptionTags.NaturalDisasters);
    }

    sealed partial class DisasterObject
    {
        public bool IsMovingDisaster => Kind == DisasterKind.Tornado || Kind == DisasterKind.Tsunami;
        static readonly Vector2[] WaveDirections = { Vector2.right, Vector2.left, Vector2.down, Vector2.up };
        Vector2 direction;
        Rect movementBounds;
        float movementTimer, pullTimer, nextTurn, nextBounce, nextDirectionLog;
        int waveDirection;
        bool movementStarted;

        static bool IsLivingParticipant(PlayerControl pc) => pc != null && pc.PlayerId <= 15 && pc.Data != null
            && !pc.Data.Disconnected && !pc.Data.IsDead && pc.IsAlive() && !pc.Is(CustomRoles.GM)
            && !pc.IsTestBot() && !pc.isDummy && !pc.notRealPlayer;

        public void BeginMovement()
        {
            if (!IsMovingDisaster) return;
            // EHR bounds room/spawn centers plus a five-unit margin. Only calculated once.
            float left = Position.x, right = Position.x, bottom = Position.y, top = Position.y;
            foreach (var room in ShipStatus.Instance.AllRooms)
            {
                if (room == null) continue;
                Vector2 pos = room.transform.position;
                left = Mathf.Min(left, pos.x); right = Mathf.Max(right, pos.x);
                bottom = Mathf.Min(bottom, pos.y); top = Mathf.Max(top, pos.y);
            }
            movementBounds = Rect.MinMaxRect(left - 5, bottom - 5, right + 5, top + 5);
            movementStarted = true;
            if (Kind == DisasterKind.Tornado) ChangeDirection(false);
            else
            {
                int[] counts = new int[4]; // Activation only, never allocated in the update loop.
                var players = PlayerControl.AllPlayerControls;
                for (int i = 0; i < players.Count; i++)
                {
                    var pc = players[i];
                    if (!IsLivingParticipant(pc)) continue;
                    Vector2 delta = pc.GetTruePosition() - Position;
                    if (Mathf.Abs(delta.y) <= 1.5f) { if (delta.x > 0) counts[0]++; else if (delta.x < 0) counts[1]++; }
                    if (Mathf.Abs(delta.x) <= 1.5f) { if (delta.y < 0) counts[2]++; else if (delta.y > 0) counts[3]++; }
                }
                waveDirection = 0;
                for (int i = 1; i < 4; i++) if (counts[i] > counts[waveDirection]) waveDirection = i;
                if (counts[waveDirection] == 0) waveDirection = IRandom.Instance.Next(4);
                direction = WaveDirections[waveDirection];
            }
        }

        void ChangeDirection(bool blocked)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (!blocked)
            {
                float closest = float.MaxValue;
                var players = PlayerControl.AllPlayerControls;
                for (int i = 0; i < players.Count; i++)
                {
                    var pc = players[i];
                    if (!IsLivingParticipant(pc)) continue;
                    Vector2 delta = pc.GetTruePosition() - Position;
                    float distance = delta.sqrMagnitude;
                    if (distance > .0001f && distance < closest) { closest = distance; direction = delta.normalized; }
                }
            }
            nextTurn = Time.fixedTime + tornadoTurn.GetInt();
            if (Time.fixedTime >= nextDirectionLog)
            {
                DebugLog($"Tornado DirectionChanged reason={(blocked ? "WallOrBoundary" : "Interval")} direction={direction}");
                nextDirectionLog = Time.fixedTime + 1f;
            }
        }

        public bool TickMovement()
        {
            if (cancelled || !Ready || !movementStarted || PlayerControl == null) return true;
            movementTimer += Time.fixedDeltaTime;
            pullTimer += Time.fixedDeltaTime;
            Vector2 previous = Position;
            // One standard position RPC at most per 50 ms, no catch-up RPC bursts after a stall.
            if (movementTimer >= .05f)
            {
                float dt = Mathf.Min(movementTimer, .1f);
                movementTimer = 0f;
                if (Kind == DisasterKind.Tornado && Time.fixedTime >= nextTurn) ChangeDirection(false);
                float speed = Kind == DisasterKind.Tornado ? tornadoSpeed.GetFloat() : tsunamiSpeed.GetFloat();
                Vector2 next = Position + direction * (speed * dt);
                bool outside = !movementBounds.Contains(next);
                bool wall = Kind == DisasterKind.Tornado && !tornadoWalls.GetBool()
                    && PhysicsHelpers.AnythingBetween(PlayerControl.Collider, Position, next + direction * 2f, Constants.ShipOnlyMask, false);
                if (outside && Kind == DisasterKind.Tsunami) return true;
                if (outside || wall)
                {
                    if (Time.fixedTime >= nextBounce) { ChangeDirection(true); nextBounce = Time.fixedTime + .25f; }
                }
                else if ((next - Position).sqrMagnitude > .000001f)
                {
                    Position = next;
                    PlayerControl.RpcSnapToForced(next);
                }
            }
            bool pull = pullTimer >= .1f;
            if (pull) pullTimer = 0f;
            var all = PlayerControl.AllPlayerControls;
            ResolvingImpact = true;
            try
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var pc = all[i];
                    if (!IsLivingParticipant(pc)) continue;
                    Vector2 pos = pc.GetTruePosition();
                    Vector2 delta = pos - Position;
                    float distanceSq = delta.sqrMagnitude;
                    // Sweep the center between transmitted positions to prevent high-speed tunnelling.
                    Vector2 travel = Position - previous;
                    float t = travel.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(pos - previous, travel) / travel.sqrMagnitude) : 1f;
                    Vector2 nearest = previous + travel * t;
                    bool lethal = Kind == DisasterKind.Tornado
                        ? (pos - nearest).sqrMagnitude <= .65f * .65f
                        : ((distanceSq <= 1.05f * 1.05f && Vector2.Dot(delta, direction) >= 0f)
                            || ((pos - nearest).sqrMagnitude <= 1.05f * 1.05f && Vector2.Dot(pos - previous, direction) >= 0f));
                    if (lethal)
                    {
                        DebugLog($"{Kind} Death PlayerId={pc.PlayerId}, distance={Mathf.Sqrt(distanceSq):F2}");
                        CustomRoleManager.OnCheckMurder(pc, pc, pc, pc, true, true, 99,
                            Kind == DisasterKind.Tornado ? CustomDeathReason.Tornado : CustomDeathReason.Drowned);
                        continue;
                    }
                    if (Kind != DisasterKind.Tornado || !pull || distanceSq > 1.05f * 1.05f
                        || pc.inVent || pc.onLadder || pc.inMovingPlat) continue;
                    Vector2 toward = -delta.normalized;
                    // 1.5x the original pull step, still capped at 10 Hz; never teleport to the eye.
                    if (!PhysicsHelpers.AnyNonTriggersBetween(pos, toward, .225f, Constants.ShipOnlyMask))
                        pc.RpcSnapToForced(pos + toward * Mathf.Min(.225f, Mathf.Sqrt(distanceSq)));
                }
            }
            finally { ResolvingImpact = false; }
            return false;
        }

        void ResetMovement()
        {
            movementStarted = false;
            direction = Vector2.zero;
            movementBounds = default;
            movementTimer = pullTimer = nextTurn = nextBounce = nextDirectionLog = 0f;
            waveDirection = 0;
        }
        string MovingSprite() => Kind == DisasterKind.Tornado ? TornadoSprite : TsunamiSprites[waveDirection];
        const string TornadoSprite = "<size=120%><line-height=97%><cspace=0.16em><#0000>WW</color><mark=#dbdbdb>WWWW</mark><#0000>WW\nW</color><mark=#dbdbdb>W</mark><mark=#b0b0b0>WWWW</mark><mark=#dbdbdb>W</mark><#0000>W</color>\n<mark=#dbdbdb>W</mark><mark=#b0b0b0>WW</mark><mark=#828282>WW</mark><mark=#b0b0b0>WW</mark><mark=#dbdbdb>W</mark>\n<mark=#dbdbdb>W</mark><mark=#b0b0b0>W</mark><mark=#828282>W</mark><mark=#474747>WW</mark><mark=#828282>W</mark><mark=#b0b0b0>W</mark><mark=#dbdbdb>W</mark>\n<mark=#dbdbdb>W</mark><mark=#b0b0b0>W</mark><mark=#828282>W</mark><mark=#474747>WW</mark><mark=#828282>W</mark><mark=#b0b0b0>W</mark><mark=#dbdbdb>W</mark>\n<mark=#dbdbdb>W</mark><mark=#b0b0b0>WW</mark><mark=#828282>WW</mark><mark=#b0b0b0>WW</mark><mark=#dbdbdb>W</mark>\n<#0000>W</color><mark=#dbdbdb>W</mark><mark=#b0b0b0>WWWW</mark><mark=#dbdbdb>W</mark><#0000>W\nWW</color><mark=#dbdbdb>WWWW</mark><#0000>WW";
        static readonly string[] TsunamiSprites = {
            "<size=120%><line-height=97%><cspace=0.16em><mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW</mark>\n<mark=#0073ff>W</mark><mark=#0095ff>W</mark><mark=#00ccff>W</mark><mark=#9ce8ff>WWWWW",
            "<size=120%><line-height=97%><cspace=0.16em><mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W</mark>\n<mark=#9ce8ff>WWWWW</mark><mark=#00ccff>W</mark><mark=#0095ff>W</mark><mark=#0073ff>W",
            "<size=120%><line-height=97%><cspace=0.16em><mark=#003cff>WWWWWWWW</mark>\n<mark=#006aff>WWWWWWWW</mark>\n<mark=#00bbff>WWWWWWWW</mark>\n<mark=#b8e1ff>WWWWWWWW</mark>\n<mark=#b8e1ff>WWWWWWWW</mark>\n<mark=#b8e1ff>WWWWWWWW</mark>\n<mark=#b8e1ff>WWWWWWWW</mark>\n<mark=#b8e1ff>WWWWWWWW",
            "<size=120%><line-height=97%><cspace=0.16em><mark=#c2e2ff>WWWWWWWW</mark>\n<mark=#c2e2ff>WWWWWWWW</mark>\n<mark=#c2e2ff>WWWWWWWW</mark>\n<mark=#c2e2ff>WWWWWWWW</mark>\n<mark=#c2e2ff>WWWWWWWW</mark>\n<mark=#00bfff>WWWWWWWW</mark>\n<mark=#007bff>WWWWWWWW</mark>\n<mark=#0033ff>WWWWWWWW",
        };
    }
}
