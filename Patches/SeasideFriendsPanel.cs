using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TownOfHost.Modules.MainMenu;
using UnityEngine;

namespace TownOfHost;

internal static partial class SeasideMainMenuPatch
{
    [HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.Open)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
    static void FriendsOpened(FriendsListUI __instance)
    {
        try { view?.OpenFriends(__instance); }
        catch { view?.CancelFriends(); Logger.Warn("SEASIDE_FRIENDS_PRESENTATION_UNAVAILABLE", "SeasideMenu"); }
    }
    [HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.Close)), HarmonyPrefix]
    static bool FriendsClosing(FriendsListUI __instance, bool __0)
        => !(view?.CloseFriends(__instance, __0) ?? false);

    [HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.OnDisable)), HarmonyPostfix]
    static void FriendsDisabled(FriendsListUI __instance)
    {
        if (view?.OwnsFriends(__instance) ?? false) view.ReleaseFriends(false);
    }

    internal sealed partial class HomeView
    {
        FriendsPresentation friendsPanel;
        readonly Dictionary<Collider2D, bool> friendsInputs = new();
        bool releasingFriends;
        internal bool FriendsBlocking => friendsPanel != null;
        internal bool OwnsFriends(FriendsListUI ui) => friendsPanel != null && friendsPanel.Ui == ui;
        static bool IsFriendsContent(Component component) => FriendsListUI.Instance != null && component != null
            && component.transform.IsChildOf(FriendsListUI.Instance.transform);

        // Colliders protect pointer input; this also protects native/controller activation of hidden home buttons.
        bool BlocksFriendsInput(PassiveButton button) => FriendsBlocking && button != null && !IsFriendsContent(button)
            && (button.transform.IsChildOf(root.transform) || button.transform.IsChildOf(Owner.transform)
                || blocked.Keys.Any(c => c != null && c.transform.IsChildOf(button.transform)));

        // The native Open still owns authentication, refreshing data, controller focus and tabs.
        internal void PrepareFriends()
        {
            if (disposed || !Visible || transition.Active) return;
            ResetButtonAnimations();
            // Friends/updater chips have focus sprites too, but no animated artwork binding.
            foreach (var button in proxies)
                foreach (var focus in button.GetComponentsInChildren<SpriteRenderer>(true))
                    if (focus.gameObject.name == "Focus") focus.enabled = false;
            friendsPanel?.Motion.Open();
        }
        internal void OpenFriends(FriendsListUI ui)
        {
            if (disposed || !Visible || transition.Active || !OwnsMenuUi(ui) || !ui.IsOpen || Camera.main == null || !Camera.main.orthographic) return;
            // Refuse an unexpected root encompassing the whole menu instead of moving the screen.
            if (Owner.transform.IsChildOf(ui.transform) || root.transform.IsChildOf(ui.transform)) return;
            if (OwnsFriends(ui)) { friendsPanel.Motion.Open(); return; }
            CancelFriends();
            PrepareFriends();
            CaptureFriendsAudit("native_open", ui);
            friendsPanel = new FriendsPresentation(ui);
            // The list can have been active at the first home draw. Release only its snapshots.
            foreach (var c in blocked.Keys.Where(IsFriendsContent).ToArray()) { if (c != null) c.enabled = blocked[c]; blocked.Remove(c); }
            foreach (var r in covered.Keys.Where(IsFriendsContent).ToArray()) { if (r != null) r.enabled = covered[r]; covered.Remove(r); }
            foreach (var c in root.GetComponentsInChildren<Collider2D>(true))
            { friendsInputs[c] = c.enabled; c.enabled = false; }
            friendsPanel.Draw(Camera.main);
            CaptureFriendsAudit("shell_applied", ui);
        }
        internal bool CloseFriends(FriendsListUI ui, bool nativeArgument)
        {
            if (releasingFriends || !OwnsFriends(ui) || !MenuSceneAvailable(Owner) || !ui.gameObject.activeInHierarchy) return false;
            if (nativeArgument) { ReleaseFriends(false); return false; } // Native force-close bypasses presentation.
            friendsPanel.CloseArgument = nativeArgument;
            friendsPanel.Motion.Close(); // Repeated native close requests do not reset the timeline.
            return true;
        }
        void TickFriends()
        {
            if (friendsPanel == null) return;
            if (!MenuSceneAvailable(Owner) || friendsPanel.Ui == null || !friendsPanel.Ui.IsOpen || !friendsPanel.Ui.gameObject.activeInHierarchy)
            { ReleaseFriends(false); return; }
            if (Camera.main == null || !Camera.main.orthographic) { CancelFriends(); return; }
            if (friendsPanel.Frame != Time.frameCount)
            {
                friendsPanel.Frame = Time.frameCount;
                friendsPanel.Motion.Advance(Time.unscaledDeltaTime);
            }
            friendsPanel.Draw(Camera.main);
            if (friendsPanel.Motion.Openness >= 1) CaptureFriendsAudit("settled", friendsPanel.Ui);
            if (friendsPanel.Motion.Finished) ReleaseFriends(true);
        }
        internal void CancelFriends() => ReleaseFriends(MenuSceneAvailable(Owner));
        internal void ReleaseFriends(bool closeNative)
        {
            var previous = friendsPanel;
            if (previous == null) return;
            friendsPanel = null; // Clear before Close/OnDisable callbacks can re-enter.
            previous.Restore();
            foreach (var pair in friendsInputs) if (pair.Key != null) pair.Key.enabled = pair.Value;
            friendsInputs.Clear();
            try
            {
                releasingFriends = true;
                if (closeNative && previous.Ui != null && previous.Ui.IsOpen) previous.Ui.Close(previous.CloseArgument);
            }
            finally
            {
                releasingFriends = false;
                CaptureFriendsAudit("released", previous.Ui);
                // Only reclaim our own navigation state, never an error/account modal's state.
                var current = ControllerManager.Instance?.CurrentUiState;
                if (!disposed && Visible && MenuSceneAvailable(Owner) && navState != null && current != null && current.Pointer == navState.Pointer)
                    RefreshNavigation();
            }
        }
        sealed class FriendsPresentation
        {
            internal readonly FriendsListUI Ui;
            internal readonly SeasidePanelSlide Motion = new();
            internal bool CloseArgument;
            internal int Frame = -1;
            // Native instance IDs, not CLR wrapper identities: IL2CPP may return fresh base wrappers each call.
            readonly Dictionary<int, (Renderer renderer, int order)> orders = new();
            readonly Dictionary<int, (SpriteRenderer renderer, bool enabled)> backdrops = new();
            internal SpriteRenderer NativeBackdrop => Ui == null ? null : Ui.transform.Find("Blur")?.GetComponent<SpriteRenderer>();
            internal void SuppressKnownBackdrop()
            {
                // Typed native lookup. `renderer is SpriteRenderer` on Renderer[] tests the CLR proxy, not its native class.
                var sprite = NativeBackdrop;
                if (sprite == null) return;
                int id = sprite.GetInstanceID();
                if (!backdrops.ContainsKey(id)) backdrops[id] = (sprite, sprite.enabled);
                sprite.enabled = false;
            }
            float restingX, writtenX;
            bool written;
            internal FriendsPresentation(FriendsListUI ui) { Ui = ui; restingX = ui.transform.localPosition.x; }
            internal void Draw(Camera camera)
            {
                var pos = Ui.transform.localPosition;
                // Respect native anchors/resizing. Subtract no accumulated offset: every pose is absolute.
                if (written && Math.Abs(pos.x - writtenX) > .0001f) restingX = pos.x;
                // Convert world viewport width into native parent's units; do not reparent or rescale.
                var parent = Ui.transform.parent;
                float unit = parent == null ? 1 : parent.TransformPoint(new(1,0,0)).x - parent.TransformPoint(new(0,0,0)).x;
                if (Math.Abs(unit) < .0001f) throw new InvalidOperationException("Unsupported native panel axis");
                writtenX = restingX + (float)Motion.Offset * (camera.orthographicSize * 2 * camera.aspect + 1) / unit;
                Ui.transform.localPosition = new(writtenX, pos.y, pos.z);
                written = true;
                SuppressKnownBackdrop();
                foreach (var renderer in Ui.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;
                    int id = renderer.GetInstanceID();
                    if (backdrops.ContainsKey(id)) continue;
                    if (!orders.ContainsKey(id)) orders[id] = (renderer, renderer.sortingOrder);
                    renderer.sortingOrder = orders[id].order + 20;
                }
            }
            internal void Restore()
            {
                if (Ui != null)
                {
                    var p = Ui.transform.localPosition;
                    // Do not overwrite a position newly assigned by the native layout after our last draw.
                    if (written && Math.Abs(p.x - writtenX) <= .0001f) Ui.transform.localPosition = new(restingX, p.y, p.z);
                }
                foreach (var entry in orders.Values) if (entry.renderer != null) entry.renderer.sortingOrder = entry.order;
                orders.Clear();
                foreach (var entry in backdrops.Values) if (entry.renderer != null) entry.renderer.enabled = entry.enabled;
                backdrops.Clear();
            }
        }
    }
}
