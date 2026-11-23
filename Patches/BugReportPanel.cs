using System;
using System.Net;
using TMPro;
using TownOfHost.Modules;
using UnityEngine;
using UnityEngine.Events;

namespace TownOfHost;

public static partial class RoleGuideButtonPatch
{
    private static BugReportView bugReportView;
    private static bool bugReportSending;

    private sealed class BugReportView
    {
        internal HudManager Hud;
        internal GameObject Root;
        internal BugReportField Title, Description;
        internal TextMeshPro Status, AttachmentLabel, SubmitLabel;
        internal PassiveButton Submit;
        internal string Diagnostics;
        internal string AccessKey;
        internal Uri Endpoint;
        internal bool Attach, Submitted;
    }

    private sealed class BugReportField
    {
        private static readonly Color Background = new(0.17f, 0.2f, 0.25f);
        private static readonly Color FocusedBorder = new(0.33f, 0.75f, 1f);
        private readonly BugReportInputBuffer input;
        private static int inputFrame = -1;
        private static string compositionBeforeSwitch = "";
        private readonly TextMeshPro display;
        private readonly SpriteRenderer border, caret;
        private int lastCaret = int.MinValue;
        private float blinkStarted;
        private bool caretOnPage;

        private readonly bool multiline;
        internal bool Focused { get; private set; }
        internal bool Composing => Focused && (input.Composition.Length > 0 || !string.IsNullOrEmpty(Input.compositionString));

        internal BugReportField(GameObject parent, string name, float y, float height, int limit,
            bool multiline, Action unfocusOther)
        {
            this.multiline = multiline;
            input = new BugReportInputBuffer(limit, multiline);
            var frame = ReportSurface(parent, name, 0, y, 6.94f, height + 0.04f, Background, 62);
            border = frame.GetComponent<SpriteRenderer>();
            var field = ReportSurface(frame, "Background", 0f, 0f, 6.9f, height, Background, 63);
            display = ReportLabel(field, "Value", "", -3.3f, height * 0.5f - 0.055f, 6.6f, height - 0.09f, 1.18f,
                TextAlignmentOptions.TopLeft);
            display.enableWordWrapping = true;
            display.overflowMode = TextOverflowModes.Page;
            display.sortingOrder = 64;

            var line = new GameObject("Caret") { layer = 5 };
            line.transform.SetParent(display.transform, false);
            caret = line.AddComponent<SpriteRenderer>();
            caret.sprite = SquareSprite;
            caret.color = FocusedBorder;
            caret.sortingOrder = 65;
            caret.enabled = false;
            ReportClick(field, 6.9f, height, () =>
            {
                unfocusOther();
                Focus();
            });
        }

        internal string Text => input.Text;

        internal void Update()
        {
            if (display == null) return;
            if (Focused) ReadKeyboard();
            var (text, position) = BugReportInputText.Format(input.Text, input.Composition, input.Caret, Focused);
            if (position != lastCaret || display.text != text)
            {
                border.color = position >= 0 ? FocusedBorder : Background;
                blinkStarted = Time.unscaledTime;
                lastCaret = position;
                display.text = text;
                PositionCaret(position);
            }
            caret.enabled = caretOnPage && BugReportInputText.CaretVisible(position, Time.unscaledTime - blinkStarted);
        }

        private void PositionCaret(int position)
        {
            caretOnPage = false;
            if (position < 0)
            {
                display.pageToDisplay = 1;
                return;
            }

            // Measure the real glyph positions so blinking cannot reflow or hide the typed text.
            display.ForceMeshUpdate();
            var info = display.textInfo;
            if (info.characterCount == 0)
            {
                display.pageToDisplay = 1;
                SetCaret(0.01f, -0.11f, 0.20f);
                return;
            }

            var characters = info.characterInfo;
            int index = 0;
            while (index < info.characterCount && characters[index].index < position) index++;
            var character = characters[Math.Min(index, info.characterCount - 1)];
            display.pageToDisplay = character.pageNumber + 1;
            float x = index < info.characterCount ? character.origin : character.xAdvance;
            SetCaret(x, (character.ascender + character.descender) * 0.5f,
                Mathf.Max(0.17f, character.ascender - character.descender));
        }

        private void SetCaret(float x, float y, float height)
        {
            caret.transform.localPosition = new Vector3(x, y, -0.02f);
            caret.transform.localScale = new Vector3(0.021f, height, 1f);
            caretOnPage = true;
        }

        internal void Focus()
        {
            if (!Focused)
            {
                Focused = true;
                Input.imeCompositionMode = IMECompositionMode.On;
                input.MoveToEdge(true);
            }
            blinkStarted = Time.unscaledTime;
        }

        internal void Unfocus()
        {
            if (!Focused) return;
            ReadKeyboard(leave: true);
            Focused = false;
            compositionBeforeSwitch = Input.compositionString ?? "";
            Input.imeCompositionMode = IMECompositionMode.Off;
        }

        private void ReadKeyboard(bool leave = false)
        {
            var composition = Input.compositionString ?? "";
            if (composition == compositionBeforeSwitch)
                composition = "";
            else
                compositionBeforeSwitch = "";

            if (inputFrame == Time.frameCount)
            {
                if (leave) input.ApplyFrame("", composition, leave: true);
                return;
            }
            inputFrame = Time.frameCount;

            bool composing = input.Composition.Length > 0 || composition.Length > 0;
            bool cancel = composing && (Input.GetKeyDown(KeyCode.Escape)
                || (composition.Length == 0 && Input.GetKey(KeyCode.Backspace)));
            if (cancel && Input.GetKeyDown(KeyCode.Escape)) composition = "";
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            var keys = ctrl ? "" : Input.inputString ?? "";
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            input.ApplyFrame(keys, composition, cancel, leave, confirm: enter);
            if (leave || composing) return;

            if (ctrl && Input.GetKeyDown(KeyCode.V)) input.Insert(GUIUtility.systemCopyBuffer);
            if (ctrl && Input.GetKeyDown(KeyCode.C)) GUIUtility.systemCopyBuffer = input.Text;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) input.Move(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) input.Move(1);
            if (Input.GetKeyDown(KeyCode.Home)) input.MoveToEdge(false);
            if (Input.GetKeyDown(KeyCode.End)) input.MoveToEdge(true);
            foreach (var key in keys)
                if (key == '\b') input.Delete(backward: true);
            if (Input.GetKeyDown(KeyCode.Backspace) && !keys.Contains('\b')) input.Delete(backward: true);
            if (Input.GetKeyDown(KeyCode.Delete)) input.Delete(backward: false);
            if (multiline && enter && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                input.Insert("\n");
        }
    }

    private static void OpenBugReportPanel(HudManager hud)
    {
        if (bugReportView != null) return;
        ClosePanel();
        var root = new GameObject("ShellBugReportPanel") { layer = 5 };
        root.transform.SetParent(hud.transform, false);
        root.transform.localPosition = new Vector3(0f, 0f, -25f);
        var view = new BugReportView { Hud = hud, Root = root, Diagnostics = BugReportRoundLog.Snapshot() };
        bugReportView = view;
        var chatInput = hud.Chat?.freeChatField?.textArea;
        if (chatInput != null && chatInput.hasFocus) chatInput.LoseFocus();

        var config = Main.Instance.Config;
        view.AccessKey = config.Bind("Bug Reports", "AccessKey", "", "Private intake key supplied by the report operator.").Value;
        var endpoint = config.Bind("Bug Reports", "EndpointUrl", "", "HTTPS address of the Shell report intake.").Value;
        if (BugReportSubmission.TryConfigure(endpoint, view.AccessKey, out var uri))
            view.Endpoint = uri;

        ReportSurface(root, "Backdrop", 0, 0, 7.6f, 4.8f, new Color(0.08f, 0.09f, 0.12f), 61);
        ReportLabel(root, "Heading", "バグ報告", 0, 2.02f, 6.9f, 0.4f, 2.05f);
        ReportLabel(root, "Destination", "Shell公式Discordの「バグ報告」に新しい投稿を作成します。", 0, 1.62f, 7f, 0.34f, 1.23f);
        ReportLabel(root, "TitleLabel", "件名（80文字まで）", 0, 1.28f, 7f, 0.24f, 1.18f);
        view.Title = new BugReportField(root, "Title", 0.94f, 0.36f, BugReportSubmission.MaxTitle,
            multiline: false, () => view.Description.Unfocus());
        ReportLabel(root, "DescriptionLabel", "本文（1700文字まで）　発生したこと・再現手順など", 0, 0.60f, 7f, 0.26f, 1.18f);
        view.Description = new BugReportField(root, "Description", -0.05f, 1.0f, BugReportSubmission.MaxDescription,
            multiline: true, () => view.Title.Unfocus());
        ReportButton(root, "Attachment", -1.6f, -0.83f, 3.65f, "", () =>
        {
            if (view.Diagnostics == null || view.Submitted || bugReportSending) return;
            view.Attach = !view.Attach;
            UpdateAttachmentLabel(view);
        }, out view.AttachmentLabel);
        UpdateAttachmentLabel(view);
        ReportLabel(root, "AttachmentInfo", "ログには試合の状態と警告の発生箇所が含まれます。", 0, -1.18f, 7.1f, 0.3f, 1.08f);
        view.Status = ReportLabel(root, "Status", view.Endpoint == null
            ? "報告先は未設定です。送信はまだ利用できません。"
            : "Discordに公開される内容です。個人情報を本文に入れないでください。",
            0, -1.58f, 7.05f, 0.45f, 1.18f);
        ReportButton(root, "Close", -2.25f, -2.02f, 2.05f, "閉じる", CloseBugReportPanel, out _);
        view.Submit = ReportButton(root, "Submit", 1.87f, -2.02f, 3f, "バグを報告する",
            () => SendBugReport(view), out view.SubmitLabel);
        RefreshReportSubmit(view);
        BlockVanillaUi(root.transform);
        UpdateBugReportPanel();
    }

    private static void UpdateAttachmentLabel(BugReportView view)
    {
        view.AttachmentLabel.text = view.Diagnostics == null
            ? "試合ログなし"
            : view.Attach ? "☑ 直近の試合ログを添付" : "□ 直近の試合ログを添付";
    }

    internal static void UpdateBugReportPanel()
    {
        var view = bugReportView;
        if (view == null) return;
        if (view.Root == null || !HudManager.InstanceExists || HudManager.Instance != view.Hud
            || !IsBugReportLobby() || GameSettingMenu.Instance || GameplayHudVisibilityPatch.OptionsOpen(view.Hud)
            || (Input.GetKeyDown(KeyCode.Escape) && !view.Title.Composing && !view.Description.Composing))
        {
            CloseBugReportPanel();
            return;
        }
        var camera = view.Hud.UICamera;
        if (camera != null && camera.orthographic)
        {
            var scale = Mathf.Min(1f, 2f * camera.orthographicSize * camera.aspect / 8f,
                2f * camera.orthographicSize / 5.05f);
            view.Root.transform.localScale = new Vector3(scale, scale, 1f);
        }
        view.Title.Update();
        view.Description.Update();
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            var next = view.Title.Focused ? view.Description : view.Title;
            view.Title.Unfocus();
            view.Description.Unfocus();
            next.Focus();
        }
    }

    private static void CloseBugReportPanel()
    {
        var view = bugReportView;
        if (view == null) return;
        bugReportView = null;
        view.Title?.Unfocus();
        view.Description?.Unfocus();
        Input.imeCompositionMode = IMECompositionMode.Off;
        if (view.Root != null)
        {
            view.Root.SetActive(false);
            UnityEngine.Object.Destroy(view.Root);
        }
        RestoreVanillaUi();
    }

    private static void RefreshReportSubmit(BugReportView view)
    {
        view.Submit.enabled = view.Endpoint != null && !bugReportSending && !view.Submitted;
        view.SubmitLabel.color = view.Submit.enabled ? Color.white : new Color(0.57f, 0.60f, 0.64f);
    }

    private static void SendBugReport(BugReportView view)
    {
        if (bugReportSending || view.Submitted || view.Endpoint == null ||
            !IsBugReportLobby() || GameSettingMenu.Instance || GameplayHudVisibilityPatch.OptionsOpen(view.Hud))
            return;

        view.Title.Unfocus();
        view.Description.Unfocus();
        var title = view.Title.Text.Trim();
        var description = view.Description.Text.Trim();
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description)
            || title.Length > BugReportSubmission.MaxTitle || description.Length > BugReportSubmission.MaxDescription)
        {
            view.Status.text = "件名と本文を制限内で入力してください。";
            return;
        }
        bugReportSending = true;
        RefreshReportSubmit(view);
        view.Status.text = "送信中…";
        _ = DeliverAsync(view, title, description, Main.PluginShowVersion, view.Attach ? view.Diagnostics : null);
    }

    private static async System.Threading.Tasks.Task DeliverAsync(BugReportView view, string title, string description,
        string version, string diagnostics)
    {
        var result = await BugReportSubmission.SendAsync(view.Endpoint, view.AccessKey, title, description, version, diagnostics)
            .ConfigureAwait(false);
        MainThreadDispatcher.Enqueue(() =>
        {
            bugReportSending = false;
            var active = bugReportView;
            if (active == null || active.Root == null) return;
            if (active == view)
            {
                // A lost response may mean the post already exists. Keep this submission disabled.
                active.Submitted = result.Success || (int)result.Status == 0 || (int)result.Status >= 500;
                active.Status.text = result.Success ? $"投稿しました　ID {result.ThreadId}" : result.Status switch
                {
                    HttpStatusCode.Unauthorized => "受付キーが無効です。",
                    HttpStatusCode.TooManyRequests => "送信間隔を空けてから、もう一度お試しください。",
                    HttpStatusCode.Forbidden => "受付が許可されていません。",
                    HttpStatusCode.BadRequest => "入力内容が受付条件に合いません。",
                    _ => "投稿結果を確認できません。再送信する前にDiscordを確認してください。"
                };
            }
            RefreshReportSubmit(active);
        });
    }

    private static PassiveButton ReportButton(GameObject parent, string name, float x, float y, float width,
        string caption, Action click, out TextMeshPro label)
    {
        var button = ReportSurface(parent, name, x, y, width, 0.46f, new Color(0.23f, 0.33f, 0.46f), 62);
        label = ReportLabel(button, "Label", caption, 0, 0, width, 0.42f, 1.33f);
        return ReportClick(button, width, 0.46f, click);
    }

    private static PassiveButton ReportClick(GameObject obj, float width, float height, Action click)
    {
        var collider = obj.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(width, height);
        var button = obj.AddComponent<PassiveButton>();
        button.Colliders = new Collider2D[] { collider };
        button.OnClick = new();
        button.OnClick.AddListener((UnityAction)(() => click()));
        button.OnMouseOut = new();
        button.OnMouseOver = new();
        return button;
    }

    private static GameObject ReportSurface(GameObject parent, string name, float x, float y,
        float width, float height, Color color, int order)
    {
        var surface = new GameObject(name) { layer = 5 };
        surface.transform.SetParent(parent.transform, false);
        surface.transform.localPosition = new Vector3(x, y, -0.1f * (order - 61));
        var renderer = surface.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = new Vector2(width, height);
        renderer.color = color;
        renderer.sortingOrder = order;
        return surface;
    }

    private static TextMeshPro ReportLabel(GameObject parent, string name, string value, float x, float y,
        float width, float height, float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var label = MakeText(parent, name, new Vector3(x, y, -0.2f), value, fontSize, alignment,
            new Vector2(width, height));
        label.color = Color.white;
        label.richText = false;
        label.sortingOrder = 63;
        return label;
    }
}
