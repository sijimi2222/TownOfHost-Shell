using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static TownOfHost.GameSettingMenuStartPatch;

namespace TownOfHost;

// Native UI adapters only; options and their values are never recreated by a mode change.
internal static class RoleSettingsDisplayUi
{
    private static PassiveButton Button;
    private static readonly Dictionary<GameOptionsMenu, Dictionary<string, CategoryHeaderMasked>> Headers = new();

    internal static void Reset()
    {
        if (Button != null) UnityEngine.Object.Destroy(Button.gameObject);
        Button = null;
        Headers.Clear();
        RoleSettingsDisplay.Reset();
    }

    internal static void Create(GameSettingMenu owner, PassiveButton lastTopButton)
    {
        Reset();
        if (lastTopButton == null) return;
        Button = UnityEngine.Object.Instantiate(owner.GameSettingsButton, lastTopButton.transform.parent);
        Button.gameObject.name = "RoleDisplayModeToggle";
        var aspect = Button.GetComponent<AspectPosition>();
        if (aspect != null) UnityEngine.Object.DestroyImmediate(aspect);
        Button.transform.localScale = Vector3.one;
        Button.transform.localPosition = lastTopButton.transform.localPosition + new Vector3(.762f, 0f, -.1f);
        foreach (var state in new[] { Button.inactiveSprites, Button.activeSprites, Button.selectedSprites })
        {
            var renderer = state.GetComponent<SpriteRenderer>();
            if (renderer == null) continue;
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localScale = Vector3.one;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(.68f, .68f);
        }
        foreach (var collider in Button.GetComponentsInChildren<BoxCollider2D>(true))
        {
            collider.size = new Vector2(.68f, .68f);
            collider.offset = Vector2.zero;
        }
        Button.OnClick = new();
        Button.OnMouseOver = new();
        Button.OnMouseOut = new();
        Button.OnClick.AddListener((Action)(() =>
        {
            if (!TryGetRoleList(out _, out var menu)) return;
            NumericOptionInput.Close(false);
            ToolTip.Hide();
            RoleSettingsDisplay.Toggle();
            var scroller = ModSettingsTab.scrollBar;
            scroller.velocity = Vector2.zero;
            var position = scroller.Inner.localPosition;
            scroller.Inner.localPosition = new Vector3(position.x, 0, position.z);
            scroller.ScrollRelative(Vector2.zero);
            GameOptionsMenuUpdatePatch.RefreshLayout(menu);
            Refresh();
        }));
        Button.OnMouseOver.AddListener((Action)(() => ToolTip.Show(Button,
            RoleSettingsDisplay.TargetName + "に切り替え", null)));
        Button.OnMouseOut.AddListener((Action)ToolTip.Hide);
        Button.SelectButton(false);
        var fontSource = ModSettingsButton != null ? ModSettingsButton.buttonText : null;
        if (sideTabButtons != null)
        {
            // These labels already display Japanese in this menu and carry its font fallbacks.
            foreach (var tabButton in sideTabButtons)
            {
                if (tabButton == null || tabButton.buttonText == null) continue;
                fontSource = tabButton.buttonText;
                break;
            }
        }
        ConfigureLabel(Button, fontSource);
        UpdateLabel(Button, true);
        Button.gameObject.SetActive(false);
    }

    private static bool TryGetRoleList(out TabGroup tab, out GameOptionsMenu menu)
    {
        tab = TabGroup.MainSettings;
        menu = null;
        if (ModSettingsTab == null || !ModSettingsTab.gameObject.activeInHierarchy
            || NowRoleTab != TownOfHost.Roles.Core.CustomRoles.NotAssigned || list == null) return false;
        if (OptionsMenuBehaviourStartPatch.Instance != null
            && OptionsMenuBehaviourStartPatch.Instance.gameObject.activeInHierarchy) return false;
        if ((HudManager.Instance?.Chat?.IsOpenOrOpening ?? false)
            || (HudManager.Instance?.TaskPanel?.open ?? false) || ShowFilter.IsShowFilter) return false;
        foreach (var pair in list)
        {
            if (!RoleSettingsDisplay.IsRoleList(pair.Key) || pair.Value == null
                || !pair.Value.gameObject.activeInHierarchy) continue;
            tab = pair.Key; menu = pair.Value; return true;
        }
        return false;
    }

    internal static void Refresh()
    {
        if (Button == null) return;
        bool wasVisible = Button.gameObject.activeSelf;
        bool visible = TryGetRoleList(out _, out _);
        Button.gameObject.SetActive(visible);
        UpdateLabel(Button, visible && !wasVisible);
    }

    // Reparent out of the cloned FontPlacer/state containers: their offsets and active state
    // belong to a wide vanilla tab, not to this square button.
    internal static void ConfigureLabel(PassiveButton button, TextMeshPro fontSource)
    {
        if (button == null || button.buttonText == null) return;
        var text = button.buttonText;
        text.DestroyTranslator();
        var aspect = text.GetComponent<AspectPosition>();
        if (aspect != null) UnityEngine.Object.DestroyImmediate(aspect);
        text.transform.SetParent(button.transform, false);
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(.6f, .6f);
        text.transform.localPosition = new Vector3(0f, 0f, -.2f);
        text.transform.localScale = Vector3.one;
        if (fontSource != null && fontSource.font != null)
        {
            text.font = fontSource.font;
            text.fontSharedMaterial = fontSource.fontSharedMaterial;
        }
        text.alignment = TextAlignmentOptions.Center;
        text.margin = Vector4.zero;
        text.color = Color.white;
        text.alpha = 1f;
        text.fontSize = text.fontSizeMax = 2.2f;
        text.fontSizeMin = .65f;
        text.enableAutoSizing = true;
        text.enableWordWrapping = false; // The target labels contain explicit line breaks.
        text.overflowMode = TextOverflowModes.Overflow;
        text.enabled = true;
        text.gameObject.SetActive(true);
        var mesh = text.GetComponent<MeshRenderer>();
        foreach (var state in new[] { button.inactiveSprites, button.activeSprites, button.selectedSprites })
        {
            var background = state.GetComponent<SpriteRenderer>();
            if (background == null) continue;
            text.sortingOrder = Math.Max(text.sortingOrder, background.sortingOrder + 10);
            if (mesh != null)
            {
                mesh.sortingLayerID = background.sortingLayerID;
                mesh.sortingOrder = text.sortingOrder;
            }
        }
    }

    internal static void UpdateLabel(PassiveButton button, bool forceMeshUpdate = false)
    {
        if (button == null || button.buttonText == null) return;
        var text = button.buttonText;
        string target = RoleSettingsDisplay.TargetButtonText;
        bool changed = text.text != target || !text.enabled || !text.gameObject.activeSelf;
        text.enabled = true;
        text.gameObject.SetActive(true);
        if (text.text != target) text.text = target;
        if (changed || forceMeshUpdate) text.ForceMeshUpdate(true);
    }

    internal static void HideHeaders(GameOptionsMenu menu)
    {
        if (!Headers.TryGetValue(menu, out var headers)) return;
        foreach (var header in headers.Values)
            if (header != null) header.gameObject.SetActive(false);
    }

    internal static void PlaceHeader(GameOptionsMenu menu, RoleSettingsDisplay.Origin origin, ref float offset)
    {
        if (!Headers.TryGetValue(menu, out var headers))
            Headers.Add(menu, headers = new());
        if (!headers.TryGetValue(origin.Key, out var header) || header == null)
        {
            var source = ModSettingsTab.transform.Find("Scroller/SliderInner/ChancesTab/CategoryHeaderMasked");
            if (source == null) return;
            var template = source.GetComponent<CategoryHeaderMasked>();
            header = UnityEngine.Object.Instantiate(template, menu.transform);
            header.name = "RoleOriginHeader_" + headers.Count;
            header.transform.localPosition = template.transform.localPosition + new Vector3(-.7789f, -.15f, -10f);
            header.Title.DestroyTranslator();
            header.Title.fontSize *= .8f;
            header.Title.fontSizeMax = header.Title.fontSize;
            header.Title.fontSizeMin = .6f;
            header.Title.enableAutoSizing = true;
            headers[origin.Key] = header;
        }
        header.Title.text = "<b>" + origin.Name + "</b>";
        offset -= .68f;
        var position = header.transform.localPosition;
        header.transform.localPosition = new Vector3(position.x, offset - 1.65f, position.z);
        header.gameObject.SetActive(true);
        offset -= .23f;
    }
}
