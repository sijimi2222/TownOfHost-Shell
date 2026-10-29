using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TownOfHost.Modules;
using TownOfHost.Patches.ISystemType;
using UnityEngine;

namespace TownOfHost.Patches;

// Host-authoritative alternative to forging per-client cleaning dictionaries.
// A denied remote hop consumes the game's native sequence normally as an Exit,
// then uses the existing vanilla BootFromVent RPC. It does not invent cleaners,
// hand-write Serialize payloads, or keep retrying a packet every FixedUpdate.
internal static class MiraVentLock
{
    static bool? apiAvailable;
    static int session;
    static IntPtr shipPointer;
    static readonly System.Collections.Generic.Dictionary<byte, float> nextCorrection = new();
    internal readonly struct Correction
    {
        internal readonly byte Player, Source;
        internal readonly int Game;
        internal readonly IntPtr Ship;
        internal Correction(byte player, byte source, int game, IntPtr ship)
        { Player = player; Source = source; Game = game; Ship = ship; }
    }
    internal static bool IsCompatible(MethodInfo method)
    {
        if (method == null || method.IsStatic || method.ReturnType != typeof(void)) return false;
        var p = method.GetParameters();
        return p.Length == 4 && p[0].ParameterType == typeof(byte)
            && p[1].ParameterType == typeof(VentilationSystem.Operation) && p[2].ParameterType == typeof(byte)
            && p[3].ParameterType.Name.StartsWith("SequenceBuffer`1", StringComparison.Ordinal);
    }
    internal static bool ApiAvailable
    {
        get
        {
            if (!apiAvailable.HasValue)
            {
                var methods = typeof(VentilationSystem).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                apiAvailable = methods.Count(m => m.Name == "PerformVentOp" && IsCompatible(m)) == 1;
                if (!apiAvailable.Value) Logger.Warn("Mira vent lock disabled: native operation signature is unavailable.", "MiraVentLock");
            }
            return apiAvailable.Value;
        }
    }
    internal static bool Active => ApiAvailable && AmongUsClient.Instance?.AmHost == true
        && GameStates.IsInTask && !GameStates.IsMeeting && !GameStates.CalledMeeting
        && Options.CurrentGameMode == CustomGameMode.Standard && ShipStatus.Instance != null
        && GameOptionsManager.Instance?.CurrentGameOptions?.MapId == 1;

    internal static void Reset()
    {
        nextCorrection.Clear(); session = 0; shipPointer = IntPtr.Zero;
    }
    internal static bool TryCorrect(VentilationSystem system, byte playerId, ref VentilationSystem.Operation op,
        ref byte destination, out Correction? correction)
    {
        correction = null;
        if (!Active || system == null || op != VentilationSystem.Operation.Move) return false;
        var ship = ShipStatus.Instance;
        if (session != AmongUsClient.Instance.GameId || shipPointer != ship.Pointer)
        { Reset(); session = AmongUsClient.Instance.GameId; shipPointer = ship.Pointer; }
        if (ship.Systems == null || !ship.Systems.TryGetValue(SystemTypes.Ventilation, out var raw)
            || raw?.TryCast<VentilationSystem>()?.Pointer != system.Pointer) return false;
        var player = PlayerCatch.GetPlayerById(playerId);
        if (player == null || !player.IsAlive() || player.Data == null || player.Data.Disconnected
            || system.PlayersInsideVents == null || !system.PlayersInsideVents.TryGetValue(playerId, out byte source)
            || !MiraVentRules.IsBlocked(source, destination) || ship.AllVents == null) return false;
        var from = ship.AllVents.FirstOrDefault(v => v != null && v.Id == source);
        byte requested = destination;
        if (from?.NearbyVents == null || !from.NearbyVents.Any(v => v != null && v.Id == requested)) return false;
        // Do not suppress UpdateSystem/its sequence buffer. An ordinary Exit
        // leaves the host's real dictionaries coherent for every client.
        op = VentilationSystem.Operation.Exit;
        destination = source;
        correction = new Correction(playerId, source, session, shipPointer);
        return true;
    }
    internal static void Complete(Correction? correction)
    {
        if (!correction.HasValue || !Active) return;
        var c = correction.Value;
        if (c.Game != AmongUsClient.Instance.GameId || c.Ship != ShipStatus.Instance.Pointer) return;
        var player = PlayerCatch.GetPlayerById(c.Player);
        if (player == null || !player.IsAlive() || player.Data.Disconnected || player.MyPhysics == null) return;
        if (VentilationSystemUpdateSystemPatch.NowVentId != null) VentilationSystemUpdateSystemPatch.NowVentId[c.Player] = c.Source;
        float now = Time.time;
        if (nextCorrection.TryGetValue(c.Player, out float next) && now < next) return;
        nextCorrection[c.Player] = now + .25f;
        // Existing native RPC: no custom reader/writer, new RPC ID, or fake ID.
        player.MyPhysics.RpcBootFromVent(c.Source);
    }
    internal static bool LocalBlocked(Vent from, Vent to) => Active && from != null && to != null
        && PlayerControl.LocalPlayer?.IsAlive() == true && PlayerControl.LocalPlayer.inVent
        && MiraVentRules.IsBlocked(from.Id, to.Id);

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
    static class StartPatch { static void Postfix() => Reset(); }

    [HarmonyPatch(typeof(VentilationSystem), "PerformVentOp")]
    internal static class NativeOperationPatch
    {
        static bool Prepare() => ApiAvailable;
        internal static void Prefix(VentilationSystem __instance, [HarmonyArgument(0)] byte playerId,
            [HarmonyArgument(1)] ref VentilationSystem.Operation op, [HarmonyArgument(2)] ref byte ventId,
            out Correction? __state, bool __runOriginal)
        {
            __state = null;
            if (!__runOriginal) return;
            try { TryCorrect(__instance, playerId, ref op, ref ventId, out __state); }
            catch (Exception) { Logger.Warn("Mira vent operation check failed; native handling retained.", "MiraVentLock"); }
        }
        internal static void Postfix(Correction? __state, bool __runOriginal)
        {
            try { if (__runOriginal) Complete(__state); }
            catch (Exception) { Logger.Warn("Mira vent exit correction failed; no automatic replay.", "MiraVentLock"); }
        }
    }
    [HarmonyPatch(typeof(Vent), nameof(Vent.TryMoveToVent))]
    internal static class LocalMovePatch
    {
        [HarmonyPriority(Priority.Last)]
        internal static bool Prefix(Vent __instance, [HarmonyArgument(0)] Vent otherVent, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            if (!LocalBlocked(__instance, otherVent)) return true;
            __result = false; return false;
        }
    }
    [HarmonyPatch(typeof(Vent), nameof(Vent.UpdateArrows))]
    static class ArrowPatch
    {
        static void Postfix(Vent __instance, bool __runOriginal)
        {
            if (!__runOriginal || !Active || __instance == null || PlayerControl.LocalPlayer?.inVent != true) return;
            var nearby = __instance.NearbyVents; var marks = __instance.CleaningIndicators;
            if (nearby == null || marks == null) return;
            for (int i = 0; i < nearby.Length && i < marks.Length; i++)
                if (nearby[i] != null && marks[i] != null && MiraVentRules.IsBlocked(__instance.Id, nearby[i].Id)) marks[i].SetActive(true);
        }
    }
}
