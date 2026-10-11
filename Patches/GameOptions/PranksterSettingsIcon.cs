using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost;

// Settings-row decoration only. Role names, translations and gameplay displays stay unchanged.
internal static class PranksterSettingsIcon
{
    const string IconName = "PranksterSettingsPumpkin";

    internal static void Refresh(OptionItem option, StringOption row)
    {
        // Only the role's own row, never its child settings or other role-name displays.
        if (option.CustomRole != CustomRoles.Prankster || row == null
            || row.TitleText == null || row.LabelBackground == null) return;

        var title = row.TitleText;
        title.ForceMeshUpdate(true);
        var text = title.textInfo;
        float right = float.NegativeInfinity, top = float.NegativeInfinity, bottom = float.PositiveInfinity;
        for (int i = 0; i < text.characterCount; i++)
        {
            var character = text.characterInfo[i];
            if (character.character == '\n') break; // The From credit is on the next line.
            if (!character.isVisible) continue;
            right = Mathf.Max(right, character.topRight.x);
            top = Mathf.Max(top, character.topRight.y);
            bottom = Mathf.Min(bottom, character.bottomLeft.y);
        }
        if (float.IsNegativeInfinity(right)) return;

        var existing = title.transform.Find(IconName);
        var icon = existing == null ? null : existing.GetComponent<SpriteRenderer>();
        if (icon == null)
        {
            // Same embedded-image loader and SpriteRenderer approach as the settings NEW badge.
            var sprite = UtilsSprite.LoadSprite("TownOfHost.Resources.TOHhm.Label.PranksterPumpkin.png", 128f);
            if (sprite == null) return;
            var iconObject = new GameObject(IconName);
            iconObject.transform.SetParent(title.transform, false);
            iconObject.layer = row.LabelBackground.gameObject.layer;
            icon = iconObject.AddComponent<SpriteRenderer>();
            icon.sprite = sprite;
            icon.color = Color.white;
            icon.drawMode = SpriteDrawMode.Simple;
        }

        // Share the row's UI mask/material; no collider, button or event handler is added.
        icon.sharedMaterial = row.LabelBackground.sharedMaterial;
        icon.maskInteraction = row.LabelBackground.maskInteraction;
        icon.sortingLayerID = row.LabelBackground.sortingLayerID;
        icon.sortingOrder = row.LabelBackground.sortingOrder + 1;
        float height = Mathf.Clamp((top - bottom) * .85f, .18f, .28f);
        float scale = height / icon.sprite.bounds.size.y;
        icon.transform.localScale = Vector3.one * scale;
        icon.transform.localPosition = new Vector3(right + .05f + icon.sprite.bounds.size.x * scale * .5f,
            (top + bottom) * .5f, -.1f);
    }
}
