using UnityEngine;

namespace TownOfHost;

// An informational row only: no Option ID, saved value, callback or synchronization.
internal static class PortSourceSettingsRow
{
    const string RowName = "PortSourceInformation";

    internal static void Hide(GameOptionsMenu menu)
    {
        var row = menu.transform.Find(RowName);
        if (row != null) row.gameObject.SetActive(false);
    }

    internal static void Place(GameOptionsMenu menu, StringOption template, ref float offset)
    {
        var existing = menu.transform.Find(RowName);
        StringOption row;
        if (existing == null)
        {
            row = Object.Instantiate(template, menu.transform);
            row.name = RowName;
            row.enabled = false;
            row.PlusBtn.gameObject.SetActive(false);
            row.MinusBtn.gameObject.SetActive(false);
            foreach (var button in row.GetComponentsInChildren<PassiveButton>(true))
            {
                button.OnClick = new();
                button.OnMouseOver = new();
                button.OnMouseOut = new();
                button.enabled = false;
            }
            foreach (var collider in row.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            row.TitleText.DestroyTranslator();
            row.ValueText.DestroyTranslator();
            row.ValueText.fontSizeMax = row.ValueText.fontSize;
            row.ValueText.fontSizeMin = row.ValueText.fontSize * 0.6f;
            row.ValueText.enableAutoSizing = true;
        }
        else row = existing.GetComponent<StringOption>();
        row.TitleText.text = "<b>" + Translator.GetString("PortSourceLabel") + "</b>";
        row.ValueText.text = "Endless Host Roles (EHR)";
        offset -= 0.68f;
        var position = template.transform.localPosition;
        row.transform.localPosition = new Vector3(position.x, offset - 1.5f, position.z);
        row.gameObject.SetActive(true);
    }
}

