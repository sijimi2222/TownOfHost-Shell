using System;
using System.Collections.Generic;
using System.Linq;
using TownOfHost.Roles.Core;

namespace TownOfHost
{
    static class PlayerOutfitExtension
    {
        public static NetworkedPlayerInfo.PlayerOutfit Set(this NetworkedPlayerInfo.PlayerOutfit instance, string playerName, int colorId, string hatId, string skinId, string visorId, string petId)
        {
            instance.PlayerName = playerName;
            instance.ColorId = colorId;
            instance.HatId = hatId;
            instance.SkinId = skinId;
            instance.VisorId = visorId;
            instance.PetId = petId;
            return instance;
        }
        public static bool Compare(this NetworkedPlayerInfo.PlayerOutfit instance, NetworkedPlayerInfo.PlayerOutfit targetOutfit)
        {
            if (instance == null || targetOutfit == null) return false;
            try
            {
                return instance.ColorId == targetOutfit.ColorId &&
                        instance.HatId == targetOutfit.HatId &&
                        instance.SkinId == targetOutfit.SkinId &&
                        instance.VisorId == targetOutfit.VisorId &&
                        instance.PetId == targetOutfit.PetId;
            }
            catch (Exception error)
            {
                // Legacy full-outfit comparisons are best effort. Never treat an
                // unreadable native string as an equal outfit or retry/log a stack.
                Camouflage.WarnOnce(byte.MaxValue, "compare", "native_outfit", error);
                return false;
            }

        }
        public static string GetString(this NetworkedPlayerInfo.PlayerOutfit instance)
        {
            return $"{instance.PlayerName} Color:{instance.ColorId} {instance.HatId} {instance.SkinId} {instance.VisorId} {instance.PetId}";
        }
    }
    public static class Camouflage
    {
        private const int CamouflageColor = 15;
        // The current Shell restoration sends SetColor only. Keep the required
        // value in managed memory; cached IL2CPP string wrappers are not needed.
        private static readonly Dictionary<byte, int> restoreColors = new();
        private static readonly HashSet<string> reportedFaults = new();
        private static readonly HashSet<byte> endRestoreAttempted = new();

        internal static void WarnOnce(byte id, string phase, string stage, Exception error = null)
        {
            if (!reportedFaults.Add(id + ":" + phase + ":" + stage)) return;
            Logger.Warn($"CAMOUFLAGE_RECOVERY playerId={id} phase={phase} stage={stage} error={error?.GetType().Name ?? "unavailable"}; cosmetic fallback/skip, gameplay continues", "CamouflageRecovery");
        }

        private static bool ValidColor(int color) => color >= 0 && color < Palette.PlayerColors.Length;

        public static void RememberColor(byte id, int color)
        {
            if (id >= 254) return;
            if (ValidColor(color)) restoreColors[id] = color;
            else WarnOnce(id, "capture", "invalid_color");
        }

        public static bool IsCamouflage;
        public static Dictionary<byte, NetworkedPlayerInfo.PlayerOutfit> PlayerSkins = new();

        public static void Init()
        {
            IsCamouflage = false;
            PlayerSkins.Clear();
            restoreColors.Clear();
            reportedFaults.Clear();
            endRestoreAttempted.Clear();
            ventplayr.Clear();
        }
        public static void CheckCamouflage()
        {
            if (!(AmongUsClient.Instance.AmHost && Options.CommsCamouflage.GetBool())) return;

            var oldIsCamouflage = IsCamouflage;

            IsCamouflage = false;//Utils.IsActive(SystemTypes.Comms);

            if (oldIsCamouflage != IsCamouflage)
            {
                foreach (var pc in PlayerCatch.AllPlayerControls)
                {
                    RpcSetSkin(pc);
                    // The code is intended to remove pets at dead players to combat a vanilla bug
                    if (!IsCamouflage && !pc.IsAlive())
                    {
                        return;
                        //pc.RpcSetPet("");
                    }
                }
                UtilsNotifyRoles.NotifyRoles(NoCache: true);
                if (!IsCamouflage)
                {
                    foreach (var role in CustomRoleManager.AllActiveRoles.Values)
                    {
                        role.ChangeColor();
                    }
                }
            }
        }
        public static List<byte> ventplayr = new();
        public static void RpcSetSkin(PlayerControl target, bool ForceRevert = false, bool RevertToDefault = false, bool? force = false)
            => TrySetSkin(target, ForceRevert, RevertToDefault, force, "rpc");

        // Preserve normal/full cosmetic semantics elsewhere. This method restores
        // exactly the Color that Shell currently sends; no PetId/string comparison.
        private static bool TrySetSkin(PlayerControl target, bool ForceRevert, bool RevertToDefault, bool? force, string phase)
        {
            byte playerId = byte.MaxValue;
            int? previousColor = null;
            bool localChangeAttempted = false;
            bool submissionStarted = false;
            try
            {
                var client = AmongUsClient.Instance;
                if (client == null || ((!client.AmHost && !(Options.CommsCamouflage.GetBool() || (force is null or true)))
                    || GameStates.IsLobby || target == null)) return false;
                if (Options.CurrentGameMode != CustomGameMode.Standard) return false;
                playerId = target.PlayerId;
                if (playerId >= 254) return false;
                var data = target.Data;
                if (data == null || data.Disconnected) return false;
                if (IsCamouflage && force is true && PlayerState.GetByPlayerId(playerId)?.IsDead == true) return false;
                if (target.inVent && force is not null)
                {
                    if (!ventplayr.Contains(playerId)) ventplayr.Add(playerId);
                    return false;
                }
                try
                {
                    var current = data.DefaultOutfit;
                    if (current != null && ValidColor(current.ColorId)) previousColor = current.ColorId;
                }
                catch (Exception error) { WarnOnce(playerId, phase, "current_color", error); }

                int color = CamouflageColor;
                if (!IsCamouflage || ForceRevert || force is true)
                {
                    byte lookupId = playerId;
                    if (Main.CheckShapeshift.TryGetValue(playerId, out var shifting) && shifting && !RevertToDefault && force is not null)
                    {
                        if (Main.ShapeshiftTarget.TryGetValue(playerId, out var shiftedId)) lookupId = shiftedId;
                        else WarnOnce(playerId, phase, "missing_shapeshift_target");
                    }
                    if (!TrySavedColor(lookupId, phase, out color))
                    {
                        if (lookupId != playerId && TrySavedColor(playerId, phase, out color)) { }
                        else if (previousColor.HasValue) color = previousColor.Value;
                        else { WarnOnce(playerId, phase, "no_safe_color"); return false; }
                        WarnOnce(playerId, phase, "fallback_color");
                    }
                }
                if (!ValidColor(color)) { WarnOnce(playerId, phase, "invalid_restore_color"); return false; }
                if (force is false && previousColor == color) return true;
                uint playerNetId = target.NetId, dataNetId = data.NetId;
                using (var sender = CustomRpcSender.Create(name: $"Camouflage.RpcSetSkin(playerId={playerId})"))
                {
                    localChangeAttempted = true;
                    target.SetColor(color);
                    sender.AutoStartRpc(playerNetId, (byte)RpcCalls.SetColor)
                        .Write(dataNetId).Write(color).EndRpc();
                    submissionStarted = true;
                    sender.SendMessage();
                }
                ventplayr.Remove(playerId);
                return true;
            }
            catch (Exception error)
            {
                // A packet may already be delivered when SendMessage throws (for
                // example its post-send callback). Roll back only before submission;
                // never send a speculative compensating packet or retry at game end.
                if (localChangeAttempted && !submissionStarted && previousColor.HasValue)
                {
                    try { target.SetColor(previousColor.Value); }
                    catch (Exception rollback) { WarnOnce(playerId, phase, "local_rollback", rollback); }
                }
                WarnOnce(playerId, phase, submissionStarted ? "send_outcome_unknown" : "restore_failed", error);
                return false;
            }
        }

        private static bool TrySavedColor(byte id, string phase, out int color)
        {
            if (restoreColors.TryGetValue(id, out color) && ValidColor(color)) return true;
            // Compatibility fallback for a caller that populated the legacy cache.
            // Only a primitive is read; never dereference its native cosmetic strings.
            try
            {
                if (PlayerSkins.TryGetValue(id, out var outfit) && outfit != null && ValidColor(outfit.ColorId))
                { color = outfit.ColorId; RememberColor(id, color); return true; }
            }
            catch (Exception error) { WarnOnce(id, phase, "cached_color", error); }
            color = -1;
            return false;
        }

        public static void RestoreAllForGameEnd() => RestorePlayers(true);
        public static void RestoreAllForMeeting() => RestorePlayers(false);

        private static void RestorePlayers(bool gameEnd)
        {
            string phase = gameEnd ? "endgame" : "meeting";
            PlayerControl[] players;
            try { players = PlayerCatch.AllPlayerControls.ToArray(); }
            catch (Exception error) { WarnOnce(byte.MaxValue, phase, "player_snapshot", error); return; }
            foreach (var player in players)
            {
                byte id = byte.MaxValue;
                try
                {
                    if (player == null) continue;
                    id = player.PlayerId;
                    if (id >= 254 || (gameEnd && !endRestoreAttempted.Add(id))) continue;
                    if (!gameEnd && player.shapeshifting) player.RpcShapeshift(player, false);
                    TrySetSkin(player, gameEnd, true, gameEnd ? (bool?)false : true, phase);
                }
                catch (Exception error) { WarnOnce(id, phase, "player_failed", error); }
            }
        }
    }
}
