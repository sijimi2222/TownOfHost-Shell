using System;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace TownOfHost
{
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class Zoom
    {
        public static int size = 3;
        private static int last;
        private static bool initialized, changed;
        private static Camera uiCamera, worldCamera;
        private static float normalUi, normalWorld, written;

        public static void Postfix()
        {
            var hud = HudManager.Instance;
            var player = PlayerControl.LocalPlayer;
            var client = AmongUsClient.Instance;
            if (hud == null || hud.UICamera == null || player == null || player.Data == null
                || player.Data.Role == null || player.Data.Disconnected || client == null)
            { Reset(); return; }

            bool task = GameStates.IsInGame && GameStates.IsInTask
                && client.GameState == AmongUsClient.GameStates.Started
                && !GameStates.IsMeeting && !GameStates.CalledMeeting && MeetingHud.Instance == null
                && !GameStates.Intro && !GameStates.ExiledAnimate;
            bool dead = !player.IsAlive();
#if SHELL_DEBUG_CONTROLLER
            bool pc = Constants.GetPlatformType() is Platforms.StandaloneSteamPC or Platforms.StandaloneEpicPC;
            bool aliveDebug = !dead && pc && client.AmHost && player.AmOwner
                && client.NetworkMode == NetworkModes.OnlineGame && !client.IsGamePublic
                && Development.ShellDebugRuntime.ZoomEnabled;
#endif
            // The existing option remains required. Development living zoom also needs the controller lease.
            // Freeplay/Alt must not bypass it or
            // the private-room requirement for living players. Ordinary dead-client zoom remains available.
            bool freeplayDead = dead && GameStates.IsFreePlay && Input.GetKey(KeyCode.LeftAlt);
            bool allowed = Options.UseZoom?.GetBool() == true && task
                && (
#if SHELL_DEBUG_CONTROLLER
                    aliveDebug ||
#endif
                    dead && (!player.IsGhostRole() || freeplayDead));
            if (!allowed) { Reset(); return; }

            var mainCamera = Camera.main;
            if (initialized && (uiCamera != hud.UICamera || worldCamera != mainCamera)) Reset();
            if (!initialized)
            {
                if (!Valid(hud.UICamera) || mainCamera != null && !Valid(mainCamera)) return;
                uiCamera = hud.UICamera; worldCamera = mainCamera;
                normalUi = uiCamera.orthographicSize;
                normalWorld = worldCamera != null ? worldCamera.orthographicSize : normalUi;
                size = last = Mathf.Max(2, Mathf.RoundToInt(normalUi));
                initialized = true;
            }
            // Chat/settings freeze wheel input, but eligibility loss above still restores the camera.
            if (!player.CanMove) return;
            if (Input.GetKeyDown(KeyCode.LeftShift)) { Reset(); return; }
            if (Input.mouseScrollDelta.y < 0 && size < int.MaxValue) size++;
            if (Input.mouseScrollDelta.y > 0 && size > 2) size--;
            if (last == size) return;

            written = size; changed = true;
            uiCamera.orthographicSize = size;
            if (worldCamera != null) worldCamera.orthographicSize = size;
            last = size;
            RefreshLayout();
        }

        private static bool Valid(Camera c) => c != null && float.IsFinite(c.orthographicSize) && c.orthographicSize > 0;

        internal static void Reset()
        {
            var ui = uiCamera; var world = worldCamera;
            float uiDefault = normalUi, worldDefault = normalWorld, ownSize = written;
            bool restore = initialized && changed;
            uiCamera = worldCamera = null;
            initialized = changed = false;
            size = 3; last = 0;
            if (!restore) return;
            bool restored = false;
            // Only restore this zoom's own writes, never a replacement menu camera or another effect's size.
            if (ui != null && Mathf.Approximately(ui.orthographicSize, ownSize))
            { ui.orthographicSize = uiDefault; restored = true; }
            if (world != null && Mathf.Approximately(world.orthographicSize, ownSize))
            { world.orthographicSize = worldDefault; restored = true; }
            if (restored) RefreshLayout();
        }

        private static void RefreshLayout()
        {
            if (Screen.height <= 0) return;
            try { ResolutionManager.ResolutionChanged?.Invoke((float)Screen.width / Screen.height, Screen.width, Screen.height, Screen.fullScreen); }
            catch (Exception e) { Logger.Warn("Zoom layout refresh failed: " + e.GetType().Name, "Zoom"); }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HudManager), nameof(HudManager.OnDestroy))]
        internal static void HudDestroyed() => Reset();

        [HarmonyPrefix, HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
        internal static void GameEnded() => Reset();

        [HarmonyPrefix, HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
        internal static void MainMenuOpened() => Reset();
    }
}
