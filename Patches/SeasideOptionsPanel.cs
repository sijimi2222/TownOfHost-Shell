using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TownOfHost.Modules.MainMenu;
using UnityEngine;

namespace TownOfHost;

internal static partial class SeasideMainMenuPatch
{
    static OptionsPresentation optionsPanel;
    static bool completingOptionsClose;

    internal static bool DefersOptionsClose(OptionsMenuBehaviour ui) => !completingOptionsClose
        && optionsPanel != null && optionsPanel.Ui == ui && optionsPanel.Motion.Closing;

    static bool ReuseOptionsOpen(OptionsMenuBehaviour ui)
    {
        if (completingOptionsClose || optionsPanel == null || optionsPanel.Ui != ui || !optionsPanel.Motion.Closing || !ui.IsOpen
            || !ui.gameObject.activeInHierarchy || !(view?.OwnsOptions(ui) ?? false)) return false;
        optionsPanel.Reopen(); // Reverse in place without another native overlay push.
        return true;
    }

    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Close)), HarmonyPrefix, HarmonyPriority(Priority.First)]
    static bool OptionsClosing(OptionsMenuBehaviour __instance)
    {
        if (completingOptionsClose || optionsPanel == null || optionsPanel.Ui != __instance
            || !MenuSceneAvailable(optionsPanel.Owner) || !optionsPanel.Owner.gameObject.activeInHierarchy
            || !(view?.OwnsOptions(__instance) ?? false) || !__instance.IsOpen || !__instance.gameObject.activeInHierarchy)
            return true;
        if (Camera.main == null || !Camera.main.orthographic) return true;
        try { optionsPanel.BeginClose(); return false; }
        catch { CancelOptionsSlide(); Logger.Warn("SEASIDE_OPTIONS_CLOSE_FALLBACK", "SeasideMenu"); return true; }
    }

    static bool OptionsCloseBlocks(PassiveButton button)
    {
        if (button == null || !MenuSceneAvailable(view?.Owner ?? earlyOwner)) return false;
        return (optionsPanel != null && optionsPanel.Motion.Closing && optionsPanel.Ui != null
            && button.transform.IsChildOf(optionsPanel.Ui.transform)) || (view?.BlocksOptionsRelease(button) ?? false);
    }

    static void CompleteOptionsClose(OptionsPresentation panel)
    {
        if (optionsPanel != panel || panel.Ui == null) return;
        var ui = panel.Ui;
        bool closed = false;
        try
        {
            completingOptionsClose = true;
            ui.Close(); // The original Close/overlay pop and existing cleanup run once, off-screen.
            closed = ui == null || !ui.IsOpen || !ui.gameObject.activeInHierarchy;
            if (closed && options == ui) FinishOptions(ui);
            if (!closed && optionsPanel == panel) panel.Reopen();
        }
        catch
        {
            if (optionsPanel == panel) panel.Reopen(); // Keep its foreground order while returning to a usable modal.
            Logger.Warn("SEASIDE_OPTIONS_CLOSE_CANCELLED", "SeasideMenu");
        }
        finally
        {
            completingOptionsClose = false;
            if (closed) view?.MarkOptionsReleased();
        }
    }

    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Open)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
    static void OptionsOpened(OptionsMenuBehaviour __instance)
    {
        var owner = view?.Owner;
        if (!MenuSceneAvailable(owner))
        {
            if (optionsPanel != null && optionsPanel.Ui == __instance) CancelOptionsSlide();
            return;
        }
        if (options != __instance || !OwnsMenuUi(__instance) || owner == null || !owner.finishStartup
            || !owner.gameObject.activeInHierarchy || !__instance.IsOpen || !__instance.gameObject.activeInHierarchy
            || Camera.main == null || !Camera.main.orthographic) return;
        // Move only the native dialog, never a shared ancestor containing the whole menu.
        if (owner.transform.IsChildOf(__instance.transform)) return;
        if (optionsPanel != null && optionsPanel.Ui == __instance) return;
        CancelOptionsSlide();
        try
        {
            optionsPanel = new OptionsPresentation(owner, __instance);
            optionsPanel.Draw(Camera.main); // Below the viewport before the first render of Open.
        }
        catch { CancelOptionsSlide(); Logger.Warn("SEASIDE_OPTIONS_SLIDE_UNAVAILABLE", "SeasideMenu"); }
    }

    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.OnDisable)), HarmonyPostfix]
    static void OptionsDisabled(OptionsMenuBehaviour __instance)
    {
        if (options == __instance) FinishOptions(__instance);
    }
    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.OnDestroy)), HarmonyPrefix]
    static void OptionsDestroyed(OptionsMenuBehaviour __instance) => OptionsDisabled(__instance);

    static void FinishOptions(OptionsMenuBehaviour ui)
    {
        if (options != ui) return;
        CancelOptionsSlide();
        view?.ReleaseOptionsInput();
        options = null;
        if (!MenuSceneAvailable(view?.Owner ?? earlyOwner)) { ReleaseMenuPresentation(); return; }
        Logger.Info("SEASIDE_OPTIONS_HOME_RESUMED", "SeasideMenu");
    }

    static void TickOptions()
    {
        if (options != null && (!options.IsOpen || !options.gameObject.activeInHierarchy)) FinishOptions(options);
        var panel = optionsPanel;
        if (panel == null) return;
        if (!MenuSceneAvailable(panel.Owner) || !panel.Owner.gameObject.activeInHierarchy
            || view == null || view.Owner != panel.Owner || options != panel.Ui || panel.Ui == null || !view.OwnsOptions(panel.Ui)
            || !panel.Ui.IsOpen || !panel.Ui.gameObject.activeInHierarchy)
        { CancelOptionsSlide(); return; }
        if (Camera.main == null || !Camera.main.orthographic)
        { if (panel.Motion.Closing) CompleteOptionsClose(panel); else CancelOptionsSlide(); return; }
        try
        {
            if (panel.Frame != Time.frameCount)
            {
                panel.Frame = Time.frameCount;
                panel.Motion.Advance(Time.unscaledDeltaTime);
            }
            panel.Draw(Camera.main);
            if (panel.Motion.Finished) CompleteOptionsClose(panel);
        }
        catch
        {
            if (panel.Motion.Closing) CompleteOptionsClose(panel); else CancelOptionsSlide();
            Logger.Warn("SEASIDE_OPTIONS_SLIDE_CANCELLED", "SeasideMenu");
        }
    }

    static void CancelOptionsSlide()
    {
        var previous = optionsPanel;
        optionsPanel = null;
        // Native destruction may invalidate the transform between callbacks. Never interrupt Close.
        try { previous?.Restore(); } catch { /* No live dialog remains to restore. */ }
    }

    // Position-only opening/closing; the native Close and modal cleanup run after the closing slide.
    sealed class OptionsPresentation
    {
        internal readonly MainMenuManager Owner;
        internal readonly OptionsMenuBehaviour Ui;
        internal readonly SeasidePanelSlide Motion = new();
        internal int Frame = Time.frameCount;
        float restingY, writtenY;
        bool written, settled;
        readonly Dictionary<int, (Renderer renderer, int order)> orders = new();
        readonly Dictionary<Collider2D, bool> closingInputs = new();

        internal OptionsPresentation(MainMenuManager owner, OptionsMenuBehaviour ui)
        { Owner = owner; Ui = ui; restingY = ui.transform.localPosition.y; }

        internal void BeginClose()
        {
            if (Motion.Closing) return;
            var y = Ui.transform.localPosition.y;
            if (!written || Math.Abs(y - writtenY) > .0001f) restingY = y;
            settled = false;
            Motion.Close();
            Logger.Info("SEASIDE_OPTIONS_CLOSE_BEGIN frame=" + Time.frameCount, "SeasideMenu");
            SuppressClosingInput();
        }
        internal void Reopen()
        {
            if (!Motion.Closing) return;
            Motion.Open(); settled = false;
            RestoreClosingInput();
        }
        void SuppressClosingInput()
        {
            foreach (var c in Ui.GetComponentsInChildren<Collider2D>(true))
            {
                if (!closingInputs.ContainsKey(c)) closingInputs[c] = c.enabled;
                c.enabled = false;
            }
        }
        void RestoreClosingInput()
        {
            foreach (var pair in closingInputs) if (pair.Key != null) pair.Key.enabled = pair.Value;
            closingInputs.Clear();
        }

        internal void Draw(Camera camera)
        {
            // Keep native options above the retained evening artwork, including late-created tab content.
            foreach (var renderer in Ui.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                int id = renderer.GetInstanceID();
                if (!orders.ContainsKey(id)) orders[id] = (renderer, renderer.sortingOrder);
                renderer.sortingOrder = orders[id].order + 20;
            }
            if (Motion.Closing) SuppressClosingInput();
            if (settled) return;
            var pos = Ui.transform.localPosition;
            // Adopt a native anchoring/resolution update; never accumulate our previous offset.
            if (written && Math.Abs(pos.y - writtenY) > .0001f) restingY = pos.y;
            if (!Motion.Closing && Motion.Openness >= 1)
            {
                RestorePosition();
                settled = true; // No further transform writes until a new native open lifecycle.
                return;
            }
            var parent = Ui.transform.parent;
            float unit = parent == null ? 1 : parent.TransformPoint(new(0, 1, 0)).y - parent.TransformPoint(new(0, 0, 0)).y;
            if (!float.IsFinite(unit) || Math.Abs(unit) < .0001f || !float.IsFinite(restingY)
                || !float.IsFinite(camera.orthographicSize) || camera.orthographicSize <= 0)
                throw new InvalidOperationException("Unsupported native options axis");
            writtenY = restingY - (float)Motion.Offset * (camera.orthographicSize * 2 + 1) / unit;
            Ui.transform.localPosition = new(pos.x, writtenY, pos.z);
            written = true;
        }

        internal void Restore()
        {
            try { RestorePosition(); }
            finally
            {
                RestoreClosingInput();
                foreach (var entry in orders.Values) if (entry.renderer != null) entry.renderer.sortingOrder = entry.order;
                orders.Clear();
            }
        }
        void RestorePosition()
        {
            if (written && Ui != null)
            {
                var pos = Ui.transform.localPosition;
                // A native layout update after our last draw belongs to the game, not to this adapter.
                if (Math.Abs(pos.y - writtenY) <= .0001f) Ui.transform.localPosition = new(pos.x, restingY, pos.z);
            }
            written = false;
        }
    }

    internal sealed partial class HomeView
    {
        OptionsMenuBehaviour optionsUi;
        readonly Dictionary<Collider2D, bool> optionsInputs = new();
        int optionsLifecycleDiagnostics;
        int optionsReleaseFrame = -1;
        internal bool BlocksOptionsRelease(PassiveButton button) => Visible && Time.frameCount == optionsReleaseFrame
            && button != null && button.transform.IsChildOf(root.transform);
        internal void MarkOptionsReleased()
        {
            optionsReleaseFrame = Time.frameCount;
            ResetButtonAnimations(); rearm = .1;
        }
        internal bool OptionsBlocking => optionsUi != null;
        internal bool OwnsOptions(OptionsMenuBehaviour ui) => optionsUi != null && optionsUi == ui;
        bool IsOptionsContent(Component component) => optionsUi != null && component != null
            && component.transform.IsChildOf(optionsUi.transform);
        bool BlocksOptionsInput(PassiveButton button) => OptionsBlocking && button != null && !IsOptionsContent(button)
            && (button.transform.IsChildOf(root.transform) || button.transform.IsChildOf(Owner.transform)
                || blocked.Keys.Any(c => c != null && c.transform.IsChildOf(button.transform)));

        internal bool PrepareOptions(OptionsMenuBehaviour ui)
        {
            if (disposed || !Visible || !OwnsMenuUi(ui) || Owner.transform.IsChildOf(ui.transform)) return false;
            if (OwnsOptions(ui)) return true;
            ReleaseOptionsInput();
            optionsUi = ui;
            CancelTransition();
            ResetButtonAnimations();
            foreach (var c in blocked.Keys.Where(IsOptionsContent).ToArray()) { if (c != null) c.enabled = blocked[c]; blocked.Remove(c); }
            foreach (var r in covered.Keys.Where(IsOptionsContent).ToArray()) { if (r != null) r.enabled = covered[r]; covered.Remove(r); }
            SuppressOptionsInput();
            foreach (var button in proxies)
                foreach (var focus in button.GetComponentsInChildren<SpriteRenderer>(true))
                    if (focus.gameObject.name == "Focus") focus.enabled = false;
            return true;
        }
        void SuppressOptionsInput()
        {
            foreach (var c in root.GetComponentsInChildren<Collider2D>(true))
                if (!IsOptionsContent(c))
                {
                    if (!optionsInputs.ContainsKey(c)) optionsInputs[c] = c.enabled;
                    c.enabled = false;
                }
        }
        internal void TraceOptionsLifecycle(string phase)
        {
            if (OptionsBlocking && optionsLifecycleDiagnostics++ < 8)
                Logger.Info("SEASIDE_OPTIONS_LIFECYCLE phase=" + phase + ";visible=" + Visible
                    + ";nativeOpen=" + optionsUi.IsOpen + ";popupKind=" + (BlockingPopup() ?? "none"), "SeasideMenu");
        }
        internal void ReleaseOptionsInput()
        {
            optionsUi = null;
            foreach (var pair in optionsInputs) if (pair.Key != null) pair.Key.enabled = pair.Value;
            optionsInputs.Clear();
        }
    }
}
