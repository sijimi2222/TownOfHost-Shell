using System;
using UnityEngine;
using TMPro;
namespace TownOfHost { internal static class LabelAdapter {
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
} }
