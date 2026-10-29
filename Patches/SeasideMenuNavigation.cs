using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace TownOfHost;

internal static partial class SeasideMainMenuPatch
{
    // ResetScreen can precede the native inventory/shop overlay pop. Bind after native setup,
    // and reconcile again on home ticks because its coroutine can replace the state afterwards.
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.SetUpControllerNav)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
    static void NativeMenuNavigationReady(MainMenuManager __instance)
    {
        if (view?.Owner == __instance && MenuSceneAvailable(__instance)) view.ReconcileNavigation();
    }
    [HarmonyPatch(typeof(PassiveButtonManager), nameof(PassiveButtonManager.HandleMouseOver)), HarmonyPrefix]
    static void NativeMenuHover(PassiveUiElement __0, Collider2D __1) => view?.TraceMenuInput("hover", __0, __1);

    internal sealed partial class HomeView
    {
        ControllerUiElementsState navState, homeState;
        Il2CppSystem.Collections.Generic.List<UiElement> oldSelectables, installedSelectables;
        UiElement oldSelection;
        int[] installedIds = Array.Empty<int>();
        string homeMenuName;
        bool homeIsScene;
        int navigationDiagnostics, inputDiagnostics;
        internal bool OwnsCurrentNavigation => navState != null && ControllerManager.Instance?.CurrentUiState != null
            && navState.Pointer == ControllerManager.Instance.CurrentUiState.Pointer;

        bool IsHomeState(ControllerUiElementsState state)
        {
            if (state == null) return false;
            if (homeState != null)
            {
                if (state.Pointer == homeState.Pointer) return state.MenuName == homeMenuName && state.IsScene == homeIsScene;
                // Same-name overlays must never be mistaken for a scene, even while home art is visible.
                return state.IsScene == homeIsScene && state.MenuName == homeMenuName
                    && ((homeIsScene && !string.IsNullOrEmpty(homeMenuName)) || ContainsNativeHomeControl(state));
            }
            // Initial ownership is established only from a native scene or actual home controls.
            return state.IsScene || ContainsNativeHomeControl(state);
        }
        bool ContainsNativeHomeControl(ControllerUiElementsState state)
        {
            var items = state.SelectableUiElements;
            if (items == null) return false;
            foreach (var item in items)
                if (item != null && sources.Values.Any(b => b != null && b.GetInstanceID() == item.GetInstanceID())) return true;
            return false;
        }
        bool HasInstalledNavigation()
        {
            var items = navState?.SelectableUiElements;
            // Native wrappers are not stable CLR identities. Check native list and element identities.
            if (items == null || installedSelectables == null || items.Pointer != installedSelectables.Pointer || items.Count != installedIds.Length) return false;
            for (int i = 0; i < items.Count; i++)
                if (items[i] == null || items[i].GetInstanceID() != installedIds[i]) return false;
            return true;
        }
        void RegisterNavigation() => ReconcileNavigation();
        internal void ReconcileNavigation()
        {
            if (!Visible || disposed || FriendsBlocking || OptionsBlocking || transition.Active || PopupOpen() || !MenuSceneAvailable(Owner)
                || !Owner.finishStartup || Owner.animating || !Owner.IsMainMenuActive()) return;
            var controller = ControllerManager.Instance;
            var current = controller?.CurrentUiState;
            if (!IsHomeState(current))
            {
                if (navigationDiagnostics++ < 12) Logger.Info("MENU_NAV_WAIT nativeHome=True;foreignOrMissingState=True", "SeasideInput");
                return;
            }
            if (OwnsCurrentNavigation && HasInstalledNavigation()) return;
            bool replaced = navState != null;
            ReleaseNavigationLease();
            homeState = current; homeMenuName = current.MenuName; homeIsScene = current.IsScene;
            navState = current; oldSelectables = current.SelectableUiElements; oldSelection = current.CurrentSelection;
            if (oldSelection != null && oldSelection.transform.IsChildOf(root.transform))
            {
                oldSelection = null;
                if (oldSelectables != null) foreach (var item in oldSelectables)
                    if (item != null && !item.transform.IsChildOf(root.transform)) { oldSelection = item; break; }
            }
            RefreshNavigation();
            var first = proxies.FirstOrDefault(b => b != null && b.gameObject.activeSelf);
            if (first != null) controller.SetCurrentSelected(first);
            lastSelection = current.CurrentSelection;
            if (navigationDiagnostics++ < 12) Logger.Info("MENU_NAV_BOUND replaced=" + replaced + ";proxyCount=" + installedIds.Length, "SeasideInput");
        }
        void RefreshNavigation()
        {
            if (navState == null) return;
            var items = new Il2CppSystem.Collections.Generic.List<UiElement>();
            var ids = new System.Collections.Generic.List<int>();
            foreach (var button in proxies) if (!transition.Active && button.gameObject.activeSelf) { items.Add(button); ids.Add(button.GetInstanceID()); }
            navState.SelectableUiElements = installedSelectables = items;
            installedIds = ids.ToArray();
        }
        void ReleaseNavigationLease()
        {
            // Do not overwrite a list replaced by native UI after our last binding.
            if (navState != null && HasInstalledNavigation())
            {
                navState.SelectableUiElements = oldSelectables;
                if (OwnsCurrentNavigation && oldSelection != null) ControllerManager.Instance.SetCurrentSelected(oldSelection);
            }
            ClearNavigationLease();
        }
        void ClearNavigationLease()
        {
            navState = null; oldSelectables = installedSelectables = null; oldSelection = null;
            installedIds = Array.Empty<int>();
        }
        internal void TraceMenuInput(string phase, PassiveUiElement hit, Collider2D collider)
        {
            // Event-driven and capped for the whole view lifetime. No text, account data or arbitrary object names.
            if (!Visible || !MenuSceneAvailable(Owner) || inputDiagnostics >= 16 || hit == null) return;
            inputDiagnostics++;
            string kind = hit.transform.IsChildOf(root.transform) ? "seaside" : IsFriendsContent(hit) ? "friends" : "nativeOther";
            Logger.Info("MENU_INPUT_HIT phase=" + phase + ";kind=" + kind + ";id=" + hit.GetInstanceID()
                + ";active=" + hit.gameObject.activeInHierarchy + ";enabled=" + hit.enabled
                + ";collider=" + (collider != null && collider.enabled) + ";homeNavigation=" + OwnsCurrentNavigation
                + ";friendsBlocking=" + FriendsBlocking, "SeasideInput");
        }
    }
}
