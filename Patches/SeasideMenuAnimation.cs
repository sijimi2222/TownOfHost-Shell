using System;
using System.Collections.Generic;
using System.Linq;
using TownOfHost.Modules.MainMenu;
using UnityEngine;

namespace TownOfHost;

internal static partial class SeasideMainMenuPatch
{
    internal sealed partial class HomeView
    {
        readonly SeasideMenuLayers layerPack;
        readonly Dictionary<string, MotionBinding> motionBindings = new(StringComparer.Ordinal);
        string hoveredId, pendingId;
        UiElement lastSelection;
        bool navigationFocus;
        double rearm;
        int animationFrame = -1;
        internal bool UsesSeparatedAssets => layerPack != null;
        sealed class MotionBinding
        {
            internal PassiveButton Button;
            internal Transform Pivot;
            internal SpriteRenderer Focus;
            internal readonly SeasideButtonMotion Motion = new();
        }
        void AttachLayer(PassiveButton button, SeasideTarget target)
        {
            if (layerPack == null) return;
            var layer = layerPack.Buttons[target.Id];
            var pivot = new GameObject("Motion_" + target.Id);
            pivot.layer = button.gameObject.layer; pivot.transform.SetParent(button.transform, false);
            pivot.transform.localPosition = new(0, 0, -.02f);
            var image = SpriteObject("ButtonArtwork_" + target.Id, pivot.transform, layer.Sprite, Color.white, 2);
            image.transform.localPosition = new((float)(layer.ImageRect.CenterX - target.Rect.CenterX) / 100,
                (float)(target.Rect.CenterY - layer.ImageRect.CenterY) / 100, 0);
            var focus = button.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.gameObject.name == "Focus");
            focus.transform.SetParent(pivot.transform, false);
            motionBindings.Add(target.Id, new() { Button = button, Pivot = pivot.transform, Focus = focus });
        }
        void SetButtonHover(string id, bool active)
        {
            if (active) { hoveredId = id; navigationFocus = false; }
            else if (hoveredId == id) hoveredId = null;
        }
        bool CanInvoke(string id)
        {
            if (!Visible || !OwnsCurrentNavigation || FriendsBlocking || OptionsBlocking || transition.Active || !MenuSceneAvailable(Owner) || !Owner.gameObject.activeInHierarchy || !Owner.finishStartup
                || Owner.animating || !Owner.IsMainMenuActive()) return false;
            if (SeasidePlayLayout.Ids.Contains(id)) return CanInvokePlay(id);
            return transition.Page == SeasidePage.Home && sources.TryGetValue(id, out var source) && source != null
                && source.enabled && source.gameObject.activeInHierarchy;
        }
        void RequestClick(string id, bool leavesHome)
        {
            ReconcileNavigation();
            if (!CanInvoke(id)) return;
            if (layerPack == null) { Invoke(id, leavesHome); return; }
            if (pendingId != null || rearm > 0 || !motionBindings.TryGetValue(id, out var binding)) return;
            if (binding.Motion.BeginPress()) pendingId = id;
        }
        static void ApplyPose(MotionBinding binding)
        {
            if (binding.Pivot == null) return;
            var p = binding.Motion.Pose;
            // Absolute poses from the neutral parent: no accumulated transform drift, collider stays fixed.
            binding.Pivot.localPosition = new(p.X / 100, p.Y / 100, -.02f);
            binding.Pivot.localRotation = Quaternion.Euler(0, 0, p.Rotation);
            binding.Pivot.localScale = new(p.ScaleX, p.ScaleY, 1);
        }
        void TickButtonAnimations()
        {
            if (layerPack == null || animationFrame == Time.frameCount) return;
            animationFrame = Time.frameCount;
            double dt = Time.unscaledDeltaTime;
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt < 0) return;
            rearm = Math.Max(0, rearm - Math.Min(dt, .1));
            var state = ControllerManager.Instance?.CurrentUiState;
            bool ownsNavigation = navState != null && state != null && state.Pointer == navState.Pointer;
            if (!ownsNavigation || (pendingId != null && !CanInvoke(pendingId)))
            {
                ResetButtonAnimations(); return;
            }
            var selected = state.CurrentSelection;
            if (selected != lastSelection)
            {
                if (hoveredId == null) navigationFocus = true;
                lastSelection = selected;
            }
            string focusId = hoveredId;
            if (focusId == null && (navigationFocus || Controller.currentTouchType == Controller.TouchType.Joystick))
                focusId = motionBindings.FirstOrDefault(p => p.Value.Button == selected).Key;
            string completed = null;
            foreach (var pair in motionBindings)
            {
                var binding = pair.Value;
                bool focus = pendingId == null && pair.Key == focusId && CanInvoke(pair.Key);
                if (binding.Focus != null) binding.Focus.enabled = focus;
                if (binding.Motion.Advance(dt, focus) && pair.Key == pendingId) completed = pair.Key;
                ApplyPose(binding);
            }
            if (completed == null) return;
            pendingId = null; rearm = .1;
            foreach (var binding in motionBindings.Values) { binding.Motion.Reset(); ApplyPose(binding); }
            // Clear state before calling the native event, which may hide or destroy the menu.
            if (CanInvoke(completed)) Invoke(completed, SeasideMenuLayout.Targets.FirstOrDefault(t => t.Id == completed).LeavesHome);
        }
        void ResetButtonAnimations()
        {
            hoveredId = pendingId = null; rearm = 0; navigationFocus = false; lastSelection = MenuSceneAvailable(Owner) ? ControllerManager.Instance?.CurrentUiState?.CurrentSelection : null; animationFrame = -1;
            foreach (var binding in motionBindings.Values)
            {
                binding.Motion.Reset(); if (binding.Focus != null) binding.Focus.enabled = false;
                if (binding.Pivot != null) ApplyPose(binding);
            }
        }
    }
}
