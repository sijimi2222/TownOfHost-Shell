using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using TownOfHost.Templates;
using UnityEngine;

namespace TownOfHost;

// UI prototype only: no release requests, downloads or DLL replacement.
[HarmonyPatch]
public static class ShellVersionPanel
{
    static GameObject panel;
    static MainMenuManager owner;
    static readonly List<Collider2D> blocked = new();
    static readonly List<SimpleButton> cards = new();
    static SimpleButton thumb;
    static int first;
    static bool dragging;
    static bool oldTint, oldLogo;
    static string[] versions;
    const int VisibleRows = 3;
    static readonly Color32 Background = new(20, 26, 42, 255);
    static readonly Color32 Card = new(40, 51, 73, 255);

    public static void Open(MainMenuManager menu)
    {
        if (panel != null && panel.activeSelf) return;
        owner = menu;
        oldTint = menu.screenTint.enabled;
        oldLogo = CredentialsPatch.TOHhmLogo != null && CredentialsPatch.TOHhmLogo.gameObject.activeSelf;
        blocked.Clear();
        foreach (var collider in menu.GetComponentsInChildren<Collider2D>())
        {
            if (!collider.enabled) continue;
            collider.enabled = false;
            blocked.Add(collider);
        }
        if (CredentialsPatch.TOHhmLogo != null) CredentialsPatch.TOHhmLogo.gameObject.SetActive(false);
        menu.screenTint.enabled = true;
        if (panel == null) Build(menu);
        first = 0;
        panel.SetActive(true);
        Refresh();
    }

    static SimpleButton Button(string name, Vector3 position, Vector2 size, string label, Color32 color, Action action)
    {
        var button = new SimpleButton(panel.transform, name, position, color, color, action, label);
        button.Scale = size;
        button.FontSize = 2.2f;
        return button;
    }

    static void Build(MainMenuManager menu)
    {
        panel = new GameObject("ShellVersionPanel");
        panel.transform.SetParent(menu.gameModeButtons.transform.parent, false);
        panel.transform.localPosition = new Vector3(0, 0, -20);
        panel.transform.localScale = new Vector3(0.15f, 0.15f, 1f);
        MainMenuManagerPatch.betaVersionMenu = panel;
        cards.Clear();
        versions = new[] { "v" + Main.PluginShowVersion, "v1.0.1", "v1.0.0", "v0.9.5", "v0.9.0" };
        Button("Background", new(0, 0, 0), new(7.7f, 6.4f), "", Background, () => { });
        Button("Close", new(-3.25f, 2.68f, -1), new(.6f, .55f), "×", Card, Close);
        var title = Button("Title", new(0, 2.68f, -1), new(5.7f, .55f), "バージョン切り替え", Background, () => { });
        title.FontSize = 3f;
        Button("Current", new(0, 1.82f, -1), new(6.55f, .55f), "現在のバージョン　v" + Main.PluginShowVersion, Card, () => { });
        var latest = Button("Latest", new(0, 1f, -1), new(6.55f, .55f), "現在公開されてる最新バージョン　v" + Main.PluginShowVersion + "（仮表示）", Card, () => { });
        latest.FontSize = 1.9f;
        // Only these three reusable slots scroll. Header objects never move.
        for (int i = 0; i < VisibleRows; i++)
        {
            int slot = i;
            cards.Add(Button("VersionCard" + i, new(-.2f, .04f - i * 1.02f, -1), new(6.1f, .82f), "", Card,
                () => OnVersionSelected(versions[first + slot])));
        }
        Button("ScrollTrack", new(3.22f, -.98f, -1), new(.22f, 2.86f), "", Card, () => { });
        thumb = Button("ScrollThumb", new(3.22f, -.408f, -2), new(.22f, 1.716f), "", new(0, 210, 165, 255), () => { });
        Button("ScrollUp", new(3.22f, .65f, -2), new(.4f, .35f), "▲", Card, () => Scroll(-1));
        Button("ScrollDown", new(3.22f, -2.61f, -2), new(.4f, .35f), "▼", Card, () => Scroll(1));
        var footer = Button("PrototypeNotice", new(0, -2.92f, -1), new(6.55f, .35f), "UIプレビュー：バージョン変更はまだ利用できません", Background, () => { });
        footer.FontSize = 1.6f;
    }

    // Future switching flow starts here. Intentionally does nothing in this prototype.
    static void OnVersionSelected(string version) { }

    static void Scroll(int delta)
    {
        first = Mathf.Clamp(first + delta, 0, versions.Length - VisibleRows);
        Refresh();
    }

    static void Refresh()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            string version = versions[first + i];
            bool current = version == "v" + Main.PluginShowVersion;
            cards[i].Label.text = version + (current ? "　　　　　　　適用中" : "　　　　　　　切り替え（準備中）");
            cards[i].NormalSprite.color = cards[i].HoverSprite.color = current ? new Color32(22, 110, 97, 255) : Card;
        }
        thumb.Button.transform.localPosition = new(3.22f, -.408f - first * .572f, -2);
    }

    public static void Close()
    {
        if (panel != null) panel.SetActive(false);
        dragging = false;
        foreach (var collider in blocked) if (collider != null) collider.enabled = true;
        blocked.Clear();
        if (owner != null) owner.screenTint.enabled = oldTint;
        if (CredentialsPatch.TOHhmLogo != null) CredentialsPatch.TOHhmLogo.gameObject.SetActive(oldLogo);
    }

    [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate)), HarmonyPostfix]
    static void Update()
    {
        if (panel == null || !panel.activeInHierarchy)
        {
            if (blocked.Count > 0) Close();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
        var camera = Camera.main;
        if (camera == null) return;
        var mouse = panel.transform.InverseTransformPoint(camera.ScreenToWorldPoint(Input.mousePosition));
        if (Mathf.Abs(mouse.x) < 3.5f && mouse.y < .65f && mouse.y > -2.61f)
        {
            if (Input.mouseScrollDelta.y != 0) Scroll(Input.mouseScrollDelta.y > 0 ? -1 : 1);
        }
        if (Input.GetMouseButtonDown(0) && Mathf.Abs(mouse.x - 3.22f) < .25f && mouse.y < .45f && mouse.y > -2.41f) dragging = true;
        if (!Input.GetMouseButton(0)) dragging = false;
        if (dragging)
        {
            first = Mathf.RoundToInt(Mathf.Clamp01((-.408f - mouse.y) / 1.144f) * (versions.Length - VisibleRows));
            Refresh();
        }
    }
}
