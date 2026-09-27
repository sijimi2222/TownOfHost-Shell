using System;
using System.Linq;
using System.Collections.Generic;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost;

// Skeld disasters. Based on EHR 56d510116d640fb63ee5192e71fd2f5377772840.
public static partial class NaturalDisasters
{
    public static bool IsThisMode => Options.GameMode != null && Options.CurrentGameMode == CustomGameMode.NaturalDisasters;
    public static bool IsActive { get; private set; }
    public static bool ResolvingImpact { get; private set; }
    static OptionItem interval, warningTime;
    static DisasterObject meteor;
    static float remaining;
    static bool impacted;
    static bool started;
    static int lastCountdown;
    static int generation;
    static float spawnWait;
    // Match the actual registered Meteor object, not every dummy or player without role data.
    public static bool IsDisasterDisplay(PlayerControl player) => player != null && player.PlayerId == 254
        && CustomNetObject.AllObjects.Any(obj => obj is DisasterObject && obj.PlayerControl == player);
    // Temporary Phase 1 diagnostics: state transitions and at most one timer line per second.
    static string lastWaitReason;
    static bool updateLogged;
    static float nextTimerLog;
    static void DebugLog(string message) => Logger.Info(message, "ND");
    static void WaitFor(string reason)
    {
        if (lastWaitReason == reason) return;
        lastWaitReason = reason;
        DebugLog($"Return reason = {reason}; active={IsActive}, mode={IsThisMode}, InGame={GameStates.InGame}, introDestroyed={GameStates.introDestroyed}, task={GameStates.IsInTask}");
    }
    static void LogTimer(string phase, float value)
    {
        if (Time.realtimeSinceStartup < nextTimerLog) return;
        nextTimerLog = Time.realtimeSinceStartup + 1f;
        DebugLog($"Timer = {value:F2}; phase={phase}");
    }

    public static void SetupOptionItem()
    {
        ObjectOptionitem.Create(1_000_220, "NaturalDisasters", true, null, TabGroup.MainSettings)
            .SetColorcode("#03fc4a").SetTag(CustomOptionTags.NaturalDisasters);
        SetupPhase2Options();
        interval = FloatOptionItem.Create(220000, "NDMeteorInterval", new(0.5f, 20f, 0.5f), 2f, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Seconds).SetTag(CustomOptionTags.NaturalDisasters);
        warningTime = IntegerOptionItem.Create(220001, "NDMeteorWarning", new(1, 30, 1), 5, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Seconds).SetTag(CustomOptionTags.NaturalDisasters);
    }

    [Attributes.GameModuleInitializer]
    public static void Reset() => ResetWithReason("GameModuleInitializer");

    public static void ResetWithReason(string reason)
    {
        if (IsThisMode || IsActive || meteor != null)
            DebugLog($"Cancel reason = {reason}; active={IsActive}, started={started}, InGame={GameStates.InGame}, introDestroyed={GameStates.introDestroyed}");
        IsActive = false;
        started = false;
        ResolvingImpact = false;
        generation++;
        meteor?.Cancel(reason);
        meteor = null;
        ClearAreaEffects();
        remaining = 0;
        impacted = false;
        lastWaitReason = null;
        updateLogged = false;
        nextTimerLog = 0f;
    }

    public static void OnGameStart()
    {
        ResetWithReason("OnGameStart initialization");
        IsActive = IsThisMode && Main.NormalOptions.MapId == 0;
        if (IsActive) Main.DontGameSet = Options.NoGameEnd.GetBool();
        // Wait until the intro is gone; this countdown is advanced only during the task phase.
        remaining = 5f;
        DebugLog($"OnGameStart active={IsActive}, mode={IsThisMode}, map={Main.NormalOptions.MapId}, InGame={GameStates.InGame}, interval={interval.GetFloat()}, warning={warningTime.GetInt()}");
    }

    public static PlayerControl[] LivingPlayers() => PlayerCatch.AllPlayerControls
        .Where(pc => pc != null && pc.Data != null && !pc.Data.Disconnected && !pc.Data.IsDead
            && pc.IsAlive() && !pc.Is(CustomRoles.GM) && !pc.IsTestBot() && !pc.isDummy && pc.PlayerId < 254)
        .ToArray();

    public static void OnPlayerLeft() => CancelCurrentMeteor("PlayerLeft");

    static void CancelCurrentMeteor(string reason)
    {
        if (!IsActive) return;
        DebugLog($"Cancel reason = {reason}");
        // Discard the pending warning too; never keep a disconnected player as a target.
        generation++;
        meteor?.Cancel(reason);
        meteor = null;
        impacted = false;
        remaining = interval.GetFloat();
    }

    public static void FixedUpdate()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        if (!IsActive)
        {
            if (IsThisMode && !GameStates.IsLobby && !GameStates.IsEnded) WaitFor("IsActive=false");
            return;
        }
        if (!updateLogged)
        {
            updateLogged = true;
            DebugLog($"FixedUpdate active; mode={IsThisMode}, InGame={GameStates.InGame}, introDestroyed={GameStates.introDestroyed}");
        }
        // Role selection arms the mode BEFORE IntroPatch sets InGame=true.
        // Pre-intro Joined/InGame=false means waiting, not cancellation of the new round.
        if (!IsThisMode || GameStates.IsEnded || (started && (GameStates.IsLobby || !GameStates.InGame)))
        {
            ResetWithReason($"Lifecycle: mode={IsThisMode}, lobby={GameStates.IsLobby}, ended={GameStates.IsEnded}, InGame={GameStates.InGame}, started={started}");
            return;
        }
        if (!GameStates.InGame || GameStates.IsLobby || !GameStates.introDestroyed)
        { WaitFor("WaitingForIntro"); return; }
        if (!GameStates.IsInTask || GameStates.IsMeeting || ExileController.Instance)
        { if (meteor != null) CancelCurrentMeteor("Task phase interrupted"); WaitFor($"TaskPhaseGate: task={GameStates.IsInTask}, meeting={GameStates.IsMeeting}, exile={ExileController.Instance != null}"); return; }
        if (lastWaitReason == "WaitingForIntro" || lastWaitReason?.StartsWith("TaskPhaseGate:") == true)
            lastWaitReason = null;
        started = true;
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) { ResetWithReason($"Winner={CustomWinnerHolder.WinnerTeam}"); return; }
        if (meteor == null)
        {
            remaining -= Time.fixedDeltaTime;
            LogTimer("BeforeMeteor", remaining);
            if (remaining > 0f) return;
            var players = LivingPlayers();
            if (players.Length < 2) { WaitFor($"LivingPlayers={players.Length}; need at least 2"); return; }
            if (!TrySelectDisaster(out var kind)) { WaitFor("No enabled disasters"); remaining = interval.GetFloat(); return; }
            // Snapshot a living player's position, then allow everyone to escape during the warning.
            Vector2 position = players[IRandom.Instance.Next(players.Length)].GetTruePosition();
            impacted = false;
            lastCountdown = kind == DisasterKind.Thunderstorm ? 0 : warningTime.GetInt();
            remaining = lastCountdown;
            spawnWait = 0f;
            DebugLog($"{kind} Start position={position}, living={players.Length}, generation={generation}");
            PlainShipRoom collapseRoom = null;
            if (kind == DisasterKind.BuildingCollapse && !TryChooseCollapseRoom(out collapseRoom, out position))
            { WaitFor("No suitable collapse room"); remaining = interval.GetFloat(); return; }
            meteor = new DisasterObject(position, generation, lastCountdown, kind, collapseRoom);
            return;
        }
        // The spawn queue is asynchronous. Never kill before OnCreated displayed the warning.
        if (!meteor.Ready)
        {
            spawnWait += Time.fixedDeltaTime;
            LogTimer("WaitingForCustomNetObject", spawnWait);
            if (spawnWait >= 10f)
            {
                CancelCurrentMeteor("CustomNetObject not ready after 10 seconds; no damage");
            }
            return;
        }
        meteor.RecordSafePositions();
        remaining -= Time.fixedDeltaTime;
        LogTimer(impacted ? "ImpactVisual" : "Warning", remaining);
        if (!impacted)
        {
            int countdown = Mathf.Max(0, Mathf.CeilToInt(remaining));
            if (countdown != lastCountdown && countdown > 0)
            {
                lastCountdown = countdown;
                meteor.ShowWarning(countdown);
            }
            if (remaining > 0f) return;
            impacted = true;
            meteor.BeginMovement();
            meteor.BeginPhase4();
            meteor.ShowImpact();
            DebugLog($"{meteor.Kind} Activated / Impact position={meteor.Position}");
            remaining = GetDuration(meteor.Kind); // Hazard remains active for the visible lifetime.
        }
        else if (remaining <= 0f)
        {
            meteor.Cancel("Impact visual expired");
            meteor = null;
            remaining = interval.GetFloat();
            return;
        }
        // Check on impact and every active impact tick, including players who enter later.
        // Snapshot living victims and suppress the end predicate until this batch resolves.
        if (meteor.IsPhase4Disaster) { meteor.TickPhase4(); return; }
        if (meteor.IsMovingDisaster)
        {
            if (meteor.TickMovement()) CancelCurrentMeteor("Moving disaster left map bounds");
            return;
        }
        UpdateDisasterEffects(meteor, GetDuration(meteor.Kind) - remaining);
        if (meteor.Kind == DisasterKind.Earthquake || meteor.Kind == DisasterKind.SandStorm) return;
        float radius = GetHazardRadius(meteor);
        var victims = LivingPlayers().Where(pc => Vector2.Distance(pc.GetTruePosition(), meteor.Position) <= radius).ToArray();
        ResolvingImpact = true;
        try
        {
            foreach (var pc in victims)
            {
                DebugLog($"{meteor.Kind} Victim PlayerId={pc.PlayerId}, distance={Vector2.Distance(pc.GetTruePosition(), meteor.Position):F2}, radius={radius:F2}");
                CustomRoleManager.OnCheckMurder(pc, pc, pc, pc, true, true, 99, GetDeathReason(meteor.Kind));
            }
        }
        finally { ResolvingImpact = false; }
    }

    public sealed class NaturalDisastersGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            if (Options.NoGameEnd.GetBool()) return false; // Leave manual Draw termination to GameEndChecker.
            if (!IsActive || !started || ResolvingImpact || CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return false;
            var alive = LivingPlayers();
            if (alive.Length > 1) return false;
            // A dedicated winner avoids the common Crewmate branch adding every dead crew member.
            CustomWinnerHolder.ResetAndSetWinner(alive.Length == 0 ? CustomWinner.None : CustomWinner.NDPlayer);
            if (alive.Length == 1) CustomWinnerHolder.WinnerIds.Add(alive[0].PlayerId);
            ResetWithReason($"GameEnd living={alive.Length}");
            return true;
        }
    }

    sealed partial class DisasterObject : CustomNetObject
    {
        public readonly DisasterKind Kind;
        public int LavaPhase = 1;
        public float NextEffectCheck;
        public readonly HashSet<byte> InsidePlayers = new();
        public readonly List<byte> EffectPlayers = new(16);
        readonly int token;
        readonly int warning;
        bool cancelled;
        public bool Ready { get; private set; }
        string lastCreateFailure;
        protected override bool CanCreate
        {
            get
            {
                string reason = cancelled ? "Object cancelled" : token != generation ? "Generation changed"
                    : !IsActive ? "Mode inactive" : !IsThisMode ? "Mode changed"
                    : !GameStates.InGame ? "InGame=false" : GameStates.IsEnded ? "Game ended"
                    : GameStates.IsLobby ? "Lobby" : null;
                if (reason != null && reason != lastCreateFailure)
                    DebugLog($"Cancel reason = CanCreate: {reason}; token={token}, generation={generation}");
                lastCreateFailure = reason;
                return reason == null;
            }
        }

        public DisasterObject(Vector2 position, int token, int warning, DisasterKind kind, PlainShipRoom room = null)
        {
            Kind = kind;
            collapseRoom = room;
            this.token = token;
            this.warning = warning;
            Position = position;
            DebugLog($"CreateWarning position={position}, seconds={warning}, generation={token}");
            CreateNetObject(position);
        }

        protected override void OnCreated()
        {
            if (!CanCreate) { Cancel("OnCreated rejected"); return; }
            // EHR hides the host's dummy body while keeping its name renderer visible.
            if (PlayerControl.cosmetics?.currentBodySprite?.BodySprite != null)
                PlayerControl.cosmetics.currentBodySprite.BodySprite.color = Color.clear;
            ShowWarning(warning);
            SnapToPosition(Position); // Standard reliable SnapTo, including the local host.
            Ready = true;
            DebugLog($"{Kind} WarningCreated NetId={PlayerControl.NetId}, position={Position}");
        }

        public void ShowWarning(int seconds) => Show(Kind == DisasterKind.Thunderstorm ? "" : $"<size=250%>{seconds}</size>\n{Translator.GetString(Kind == DisasterKind.Meteor ? "DeathReason.Meteor" : "ND" + Kind)}{CollapseRoomLabel()}");
        // Meteor rich-text sprite from the EHR commit cited at the top of this file.
        public void ShowImpact() => Show(IsPhase4Disaster ? Phase4Sprite() : IsMovingDisaster ? MovingSprite() : Kind != DisasterKind.Meteor ? GetDisasterSprite(Kind, LavaPhase) : "<size=120%><line-height=97%><cspace=0.16em><#0000>WWW</color><mark=#fff700>WW</mark><#0000>WWW\nWW</color><mark=#fff700>W</mark><mark=#ffae00>WW</mark><mark=#fff700>W</mark><#0000>WW\nW</color><mark=#fff700>W</mark><mark=#ffae00>W</mark><mark=#ff6f00>WW</mark><mark=#ffae00>W</mark><mark=#fff700>W</mark><#0000>W</color>\n<mark=#fff700>W</mark><mark=#ffae00>W</mark><mark=#ff6f00>W</mark><mark=#ff1100>WW</mark><mark=#ff6f00>W</mark><mark=#ffae00>W</mark><mark=#fff700>W</mark>\n<mark=#fff700>W</mark><mark=#ffae00>W</mark><mark=#ff6f00>W</mark><mark=#ff1100>WW</mark><mark=#ff6f00>W</mark><mark=#ffae00>W</mark><mark=#fff700>W</mark>\n<#0000>W</color><mark=#fff700>W</mark><mark=#ffae00>W</mark><mark=#ff6f00>WW</mark><mark=#ffae00>W</mark><mark=#fff700>W</mark><#0000>W\nWW</color><mark=#fff700>W</mark><mark=#ffae00>WW</mark><mark=#fff700>W</mark><#0000>WW\nWWW</color><mark=#fff700>WW</mark><#0000>WWW");

        void Show(string text)
        {
            if (!CanCreate) return;
            if (PlayerControl == null) { DebugLog("Return reason = Show: dummy PlayerControl=null"); return; }
            // Never use SetName here: the dummy's CachedPlayerData is the host's data.
            // Snapshot the rich-text name through Shapeshift, then restore the host outfit.
            SetAppearance(0, displayName: "<size=14><br></size>" + text);
            if (PlayerControl.cosmetics?.currentBodySprite?.BodySprite != null)
                PlayerControl.cosmetics.currentBodySprite.BodySprite.color = Color.clear;
        }

        public void Cancel(string reason)
        {
            if (cancelled) return; // Lifecycle hooks may cancel the same object more than once.
            DebugLog($"Cancel reason = {reason}; dummy NetId={PlayerControl?.NetId}, ready={Ready}");
            if (!cancelled) DebugLog($"{Kind} Expired reason={reason}");
            RemoveEffectSource(this);
            cancelled = true;
            Ready = false;
            ResetMovement();
            ResetPhase4();
            // A reset can happen before the base class's delayed OnCreated removes this entry.
            var players = GameData.Instance?.AllPlayers;
            if (players != null && PlayerControl != null)
                for (int i = players.Count - 1; i >= 0; i--)
                    if (players[i] != null && players[i].PlayerId == 254
                        && players[i].Object?.NetId == PlayerControl.NetId)
                        players.RemoveAt(i);
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost) Despawn();
            // Despawn already destroys the host object. Only disconnected/non-host cleanup needs Destroy here.
            AllObjects.Remove(this);
            if (PlayerControl != null && (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost))
                UnityEngine.Object.Destroy(PlayerControl.gameObject);
            InsidePlayers.Clear();
            EffectPlayers.Clear();
            PlayerControl = null;
        }

        public override void OnMeeting() => Cancel("OnMeeting");
    }
}
