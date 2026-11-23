using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using TownOfHost.Modules.ClientOptions;
using Object = UnityEngine.Object;

namespace TownOfHost;

// Presentation only: the list has no connection to game admission or player identities.
public static class StreamerHopeMenu
{
    public static SpriteRenderer Popup { get; private set; }
    public static TextMeshPro TitleText { get; private set; }
    private static TextMeshPro statusText, pageText, rowTemplate;
    private static ToggleButtonBehaviour receptionButton, resetButton, previousButton, nextButton;
    private static readonly List<TextMeshPro> rows = new();
    private static int page, shownVersion = -1;
    private static bool shownHost, shownComments;

    public static void Init(OptionsMenuBehaviour menu)
    {
        Hide();
        if (Popup != null) Object.Destroy(Popup.gameObject);
        Popup = Object.Instantiate(menu.Background, ClientActionItem.CustomBackground.transform.parent);
        Popup.name = "StreamHopePopup";
        Popup.transform.localPosition = new(0f, 0f, -5f);
        Popup.transform.localScale = new(1.7f, 0.9f, 1f);
        rowTemplate = menu.DisableMouseMovement.Text;
        TitleText = CreateText("TitleText", new(0, 2.4f, -1), new(0.7f, 1.3f, 1));
        statusText = CreateText("InterestReceptionStatus", new(0, 1.98f, -1), new(0.5f, 1f, 1));
        pageText = CreateText("InterestPage", new(0, -1.8f, -1), new(0.5f, 1f, 1));
        CreateButton(menu, "Back", "ED.Back", new(2.2f, -2.4f, -2), Hide);
        receptionButton = CreateButton(menu, "InterestReception", "StreamInterestStart", new(-2.15f, -2.4f, -2), () =>
        {
            if (AmongUsClient.Instance?.AmHost != true) return;
            if (StreamerInfo.Interests.Accepting) StreamerInfo.StopInterestReception(); else StreamerInfo.StartInterestReception();
            Refresh();
        });
        resetButton = CreateButton(menu, "ResetInterestList", "StreamInterestReset", new(-0.65f, -2.4f, -2), () =>
        {
            if (AmongUsClient.Instance?.AmHost != true) return;
            StreamerInfo.ResetInterestList(); page = 0; Refresh();
        });
        previousButton = CreateButton(menu, "PreviousInterestPage", "StreamInterestPrevious", new(-1.5f, -1.8f, -2), () => { page--; Refresh(); });
        nextButton = CreateButton(menu, "NextInterestPage", "StreamInterestNext", new(1.5f, -1.8f, -2), () => { page++; Refresh(); });
        Popup.gameObject.SetActive(false);
    }
    private static TextMeshPro CreateText(string name, Vector3 position, Vector3 scale)
    {
        var text = Object.Instantiate(rowTemplate, Popup.transform);
        text.name = name; text.transform.localPosition = position; text.transform.localScale = scale;
        text.alignment = TextAlignmentOptions.Center; text.gameObject.SetActive(true); return text;
    }
    private static ToggleButtonBehaviour CreateButton(OptionsMenuBehaviour menu, string name, string key, Vector3 position, Action action)
    {
        var button = Object.Instantiate(menu.DisableMouseMovement, Popup.transform);
        button.name = name; button.transform.localPosition = position; button.transform.localScale = new(0.3f, 0.7f, 1);
        button.Text.text = Translator.GetString(key);
        var passive = button.GetComponent<PassiveButton>(); passive.OnClick = new(); passive.OnClick.AddListener(action);
        button.gameObject.SetActive(true); return button;
    }
    public static void Show(OptionsMenuBehaviour menu)
    {
        if (Popup == null) Init(menu);
        rowTemplate = menu.DisableMouseMovement.Text;
        page = 0; Popup.gameObject.SetActive(true); Refresh();
    }
    public static void Tick()
    {
        if (Popup == null || !Popup.gameObject.activeInHierarchy) return;
        if (shownVersion != StreamerInfo.Interests.Version || shownHost != (AmongUsClient.Instance?.AmHost == true)
            || shownComments != (Main.ShowStreamComments?.Value == true)) Refresh();
    }
    private static void Refresh()
    {
        if (Popup == null || rowTemplate == null) return;
        ClearRows();
        var interests = StreamerInfo.Interests;
        var names = interests.Snapshot();
        int pages = StreamInterestList.PageCount(names.Length);
        page = Math.Clamp(page, 0, pages - 1);
        TitleText.text = string.Format(Translator.GetString("StreamInterestCount"), names.Length);
        statusText.text = Translator.GetString(interests.Accepting ? "StreamInterestOpen" : "StreamInterestClosed");
        if (!YouTubeOfficialRuntime.ParticipationPolicyReviewed) statusText.text += " / 規約確認待ち（受付停止）";
        if (interests.Overflowed) statusText.text += " / " + Translator.GetString("StreamInterestLimit");
        if (!StreamerInfo.HasStream) statusText.text += " / " + Translator.GetString("StreamInterestNeedsURL");
        else if (Main.ShowStreamComments?.Value != true) statusText.text += " / " + Translator.GetString("StreamInterestNeedsComments");
        receptionButton.Text.text = Translator.GetString(interests.Accepting ? "StreamInterestStop" : "StreamInterestStart");
        bool host = AmongUsClient.Instance?.AmHost == true;
        receptionButton.gameObject.SetActive(host); resetButton.gameObject.SetActive(host);
        previousButton.gameObject.SetActive(page > 0); nextButton.gameObject.SetActive(page + 1 < pages);
        pageText.text = pages > 1 ? $"{page + 1} / {pages}" : "";
        var visible = StreamInterestList.Page(names, page);
        for (int i = 0; i < visible.Length; i++)
        {
            var text = CreateText("InterestDisplayName", new(i < 15 ? -1.35f : 1.35f, 1.58f - i % 15 * 0.22f, -2), new(0.5f, 1, 1));
            text.alignment = TextAlignmentOptions.Left; text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis; text.rectTransform.sizeDelta = new(4.8f, 0.3f);
            text.text = visible[i]; rows.Add(text);
        }
        shownVersion = interests.Version; shownHost = host; shownComments = Main.ShowStreamComments?.Value == true;
    }
    private static void ClearRows()
    {
        foreach (var row in rows) if (row != null) { row.gameObject.SetActive(false); Object.Destroy(row.gameObject); }
        rows.Clear();
    }
    public static void Hide()
    {
        ClearRows(); shownVersion = -1;
        if (Popup != null) Popup.gameObject.SetActive(false);
    }
}
