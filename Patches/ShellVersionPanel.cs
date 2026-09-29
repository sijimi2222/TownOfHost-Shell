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

    // ホバー時
    static readonly Color32 HoverCard = new(110, 135, 180, 255);

    // 適用中
    static readonly Color32 CurrentCard = new(22, 110, 97, 255);
    static readonly Color32 CurrentHover = new(45, 190, 160, 255);

    // 白枠
    static readonly Color32 WhiteBorder = new(255, 255, 255, 255);

    public static void Open(MainMenuManager menu)
    {
        if (panel != null && panel.activeSelf) return;

        owner = menu;

        oldTint = menu.screenTint.enabled;

        oldLogo =
            CredentialsPatch.TOHhmLogo != null &&
            CredentialsPatch.TOHhmLogo.gameObject.activeSelf;

        blocked.Clear();

        foreach (var collider in menu.GetComponentsInChildren<Collider2D>())
        {
            if (!collider.enabled) continue;

            collider.enabled = false;
            blocked.Add(collider);
        }

        if (CredentialsPatch.TOHhmLogo != null)
            CredentialsPatch.TOHhmLogo.gameObject.SetActive(false);

        menu.screenTint.enabled = true;

        if (panel == null)
            Build(menu);

        first = 0;

        panel.SetActive(true);

        Refresh();
    }

    static SimpleButton Button(
        string name,
        Vector3 position,
        Vector2 size,
        string label,
        Color32 color,
        Action action,
        bool whiteBorder = false,
        float borderThickness = 0.06f)
    {
        // ----------------------------
        // 白枠
        // ----------------------------
        if (whiteBorder)
        {
            var borderButton = new SimpleButton(
                panel.transform,
                name + "_Border",

                // 本体より少し後ろ
                new Vector3(
                    position.x,
                    position.y,
                    position.z + 0.05f),

                WhiteBorder,
                WhiteBorder,

                () => { },

                "");

            // 本体より少し大きくする
            borderButton.Scale = new Vector2(
                size.x + borderThickness,
                size.y + borderThickness);

            // 白枠側はクリックできないようにする
            foreach (var collider in
                borderButton.Button.GetComponentsInChildren<Collider2D>())
            {
                collider.enabled = false;
            }
        }

        // ----------------------------
        // 本体
        // ----------------------------
        var button = new SimpleButton(
            panel.transform,
            name,
            position,
            color,
            color,
            action,
            label);

        button.Scale = size;
        button.FontSize = 2.2f;

        return button;
    }

    static void Build(MainMenuManager menu)
    {
        panel = new GameObject("ShellVersionPanel");

        panel.transform.SetParent(
            menu.gameModeButtons.transform.parent,
            false);

        panel.transform.localPosition =
            new Vector3(0, 0, -20);

        panel.transform.localScale =
            new Vector3(0.15f, 0.15f, 1f);

        MainMenuManagerPatch.betaVersionMenu = panel;

        cards.Clear();

        versions = new[]
        {
            "v" + Main.PluginShowVersion,
            "v1.0.1",
            "v1.0.0",
            "v0.9.5",
            "v0.9.0"
        };

        // ==================================================
        // 大きい背景
        // ==================================================

        Button(
            "Background",
            new(0, 0, 0),
            new(7.1f, 7.2f),
            "",
            Background,
            () => { });

        // ==================================================
        // × ボタン
        // ==================================================

        var close = Button(
            "Close",
            new(-2.5f, 2.49f, -2),
            new(.96f, .88f),
            "×",
            Card,
            Close,
            true,
            0.08f);

        // 今の大きさを維持
        close.FontSize = 9f;

        // マウスを乗せたら明るく
        close.NormalSprite.color = Card;
        close.HoverSprite.color = HoverCard;

        // ==================================================
        // タイトル
        // ==================================================

        var title = Button(
            "Title",
            new(0, 2.68f, -1),
            new(5.7f, .55f),
            "バージョン切り替え",
            Background,
            () => { },
            true);

        title.FontSize = 3f;

        // ==================================================
        // 現在のバージョン
        // ==================================================

        Button(
            "Current",
            new(0, 1.82f, -1),
            new(6.55f, .55f),
            "現在のバージョン　v" + Main.PluginShowVersion,
            Card,
            () => { },
            true);

        // ==================================================
        // 最新バージョン
        // ==================================================

        var latest = Button(
            "Latest",
            new(0, 1f, -1),
            new(6.55f, .55f),

            "現在公開されてる最新バージョン　v"
            + Main.PluginShowVersion
            + "（仮表示）",

            Card,
            () => { },
            true);

        latest.FontSize = 1.9f;

        // ==================================================
        // バージョン一覧
        // ==================================================

        for (int i = 0; i < VisibleRows; i++)
        {
            int slot = i;

            var card = Button(
                "VersionCard" + i,

                new(
                    -.2f,
                    .04f - i * 1.02f,
                    -1),

                new(6.1f, .82f),

                "",

                Card,

                () =>
                    OnVersionSelected(
                        versions[first + slot]),

                true,
                0.07f);

            cards.Add(card);
        }

        // ==================================================
        // スクロールバー
        // ==================================================

        Button(
            "ScrollTrack",
            new(3.22f, -.98f, -1),
            new(.22f, 2.86f),
            "",
            Card,
            () => { },
            true,
            0.04f);

        thumb = Button(
            "ScrollThumb",
            new(3.22f, -.408f, -2),
            new(.22f, 1.716f),
            "",
            new(0, 210, 165, 255),
            () => { },
            true,
            0.04f);

        // ==================================================
        // 上スクロール
        // ==================================================

        var scrollUp = Button(
            "ScrollUp",
            new(3.22f, .65f, -2),
            new(.4f, .35f),
            "▲",
            Card,
            () => Scroll(-1),
            true,
            0.05f);

        scrollUp.NormalSprite.color = Card;
        scrollUp.HoverSprite.color = HoverCard;

        // ==================================================
        // 下スクロール
        // ==================================================

        var scrollDown = Button(
            "ScrollDown",
            new(3.22f, -2.61f, -2),
            new(.4f, .35f),
            "▼",
            Card,
            () => Scroll(1),
            true,
            0.05f);

        scrollDown.NormalSprite.color = Card;
        scrollDown.HoverSprite.color = HoverCard;

        // ==================================================
        // 下の注意書き
        // ==================================================

        var footer = Button(
            "PrototypeNotice",
            new(0, -2.92f, -1),
            new(6.55f, .35f),
            "UIプレビュー：バージョン変更はまだ利用できません",
            Background,
            () => { },
            true,
            0.05f);

        footer.FontSize = 1.6f;
    }

    // ======================================================
    // 将来ここにバージョン切り替え処理を追加
    // ======================================================

    static void OnVersionSelected(string version)
    {
    }

    static void Scroll(int delta)
    {
        first = Mathf.Clamp(
            first + delta,
            0,
            versions.Length - VisibleRows);

        Refresh();
    }

    static void Refresh()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            string version =
                versions[first + i];

            bool current =
                version ==
                "v" + Main.PluginShowVersion;

            cards[i].Label.text =
                version +
                (current
                    ? "　　　　　　　適用中"
                    : "　　　　　　　切り替え（準備中）");

            if (current)
            {
                // 適用中
                cards[i].NormalSprite.color =
                    CurrentCard;

                cards[i].HoverSprite.color =
                    CurrentHover;
            }
            else
            {
                // 通常
                cards[i].NormalSprite.color =
                    Card;

                cards[i].HoverSprite.color =
                    HoverCard;
            }
        }

        thumb.Button.transform.localPosition =
            new Vector3(
                3.22f,
                -.408f - first * .572f,
                -2);
    }

    public static void Close()
    {
        if (panel != null)
            panel.SetActive(false);

        dragging = false;

        foreach (var collider in blocked)
        {
            if (collider != null)
                collider.enabled = true;
        }

        blocked.Clear();

        if (owner != null)
            owner.screenTint.enabled = oldTint;

        if (CredentialsPatch.TOHhmLogo != null)
        {
            CredentialsPatch.TOHhmLogo
                .gameObject
                .SetActive(oldLogo);
        }
    }

    [HarmonyPatch(
        typeof(ModManager),
        nameof(ModManager.LateUpdate))]
    [HarmonyPostfix]
    static void Update()
    {
        if (panel == null ||
            !panel.activeInHierarchy)
        {
            if (blocked.Count > 0)
                Close();

            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        var camera = Camera.main;

        if (camera == null)
            return;

        var mouse =
            panel.transform
                .InverseTransformPoint(
                    camera.ScreenToWorldPoint(
                        Input.mousePosition));

        // マウスホイール
        if (Mathf.Abs(mouse.x) < 3.5f &&
            mouse.y < .65f &&
            mouse.y > -2.61f)
        {
            if (Input.mouseScrollDelta.y != 0)
            {
                Scroll(
                    Input.mouseScrollDelta.y > 0
                        ? -1
                        : 1);
            }
        }

        // スクロールバーをドラッグ開始
        if (Input.GetMouseButtonDown(0) &&
            Mathf.Abs(mouse.x - 3.22f) < .25f &&
            mouse.y < .45f &&
            mouse.y > -2.41f)
        {
            dragging = true;
        }

        // 離したら終了
        if (!Input.GetMouseButton(0))
        {
            dragging = false;
        }

        // スクロールバー移動
        if (dragging)
        {
            first =
                Mathf.RoundToInt(
                    Mathf.Clamp01(
                        (-.408f - mouse.y)
                        / 1.144f)
                    *
                    (versions.Length
                     - VisibleRows));

            Refresh();
        }
    }
}