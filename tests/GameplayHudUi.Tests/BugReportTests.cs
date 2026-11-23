using System.Reflection;
using System.Net;
using System.Text.Json;
using TownOfHost;
using TownOfHost.Modules;
using UnityEngine;

partial class Program
{
    private static void PrepareBugReport()
    {
        AmongUsClient.Instance = new();
        GameStates.IsLobby = true;
        GameStates.IsInTask = GameStates.IsInGame = GameStates.IsFreePlay = false;
        RoleGuideButtonPatch.BugReportPanelOpenedFor = null;

        var button = new GameObject();
        button.transform.parent = RoleGuideButtonPatch._btnRenderer.transform;
        button.transform.localPosition = new Vector3(-0.9f, 0f, 0f);
        button.SetActive(false);

        var frame = new SpriteRenderer();
        frame.transform.parent = button.transform;
        RoleGuideButtonPatch.bugReportFrameRenderer = frame;
    }

    private static bool BugReportVisible => RoleGuideButtonPatch.bugReportFrameRenderer.gameObject.activeInHierarchy;

    private static void AttemptBugReport()
    {
        typeof(RoleGuideButtonPatch).GetMethod("OpenBugReport", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { H });
    }

    private static void BugReportTests()
    {
        Test("IME keeps テスト when Enter advances to the next composition segment 用", () =>
        {
            var field = new BugReportInputBuffer(80, false);
            field.ApplyFrame("", "テスト", confirm: true);
            field.ApplyFrame("", "用");
            Check(field.Text == "テスト" && field.Composition == "用");
            field.ApplyFrame("", "", confirm: true);
            Check(field.Text == "テスト用" && field.Composition == "");
        });

        Test("changing report fields keeps the last visible IME text in the original field", () =>
        {
            var title = new BugReportInputBuffer(80, false);
            var description = new BugReportInputBuffer(1700, true);
            title.ApplyFrame("", "テスト用");
            title.ApplyFrame("", "テスト用", leave: true);
            description.ApplyFrame("本文", "");
            Check(title.Text == "テスト用" && title.Composition == "" && description.Text == "本文");
        });

        Test("IME uses the committed candidate when Windows delivers a Unicode result", () =>
        {
            var field = new BugReportInputBuffer(80, false);
            field.ApplyFrame("", "てすと", confirm: true);
            field.ApplyFrame("テスト", "");
            field.ApplyFrame("用", "");
            Check(field.Text == "テスト用");
        });

        Test("a repeated preview or delayed IME result does not duplicate the text", () =>
        {
            var field = new BugReportInputBuffer(80, false);
            field.ApplyFrame("", "テスト", confirm: true);
            field.ApplyFrame("", "テスト");
            field.ApplyFrame("", "テスト");
            field.ApplyFrame("テスト", "");
            Check(field.Text == "テスト" && field.Composition == "");
        });

        Test("cancelled IME text is discarded while committed text and editing still work", () =>
        {
            var field = new BugReportInputBuffer(80, true);
            field.Insert("本🐛");
            field.ApplyFrame("", "削除");
            field.ApplyFrame("", "", cancel: true);
            field.Delete(backward: true);
            field.Insert("\n文");
            Check(field.Text == "本\n文" && field.Caret == 3 && field.Composition == "");
        });

        Test("report input keeps Japanese composition visible at the cursor", () =>
        {
            Check(BugReportInputText.Format("再手順", "げん", 1, true) == ("再げん手順", 3));
            Check(BugReportInputText.Format("再現手順", "", 2, true) == ("再現手順", 2));
        });

        Test("report input keeps the committed draft visible after leaving a field", () =>
        {
            Check(BugReportInputText.Format("件名", "未確定", 2, false) == ("件名", -1));
            Check(!BugReportInputText.CaretVisible(-1, 0));
        });

        Test("report caret blinks without modifying input or losing the empty-field cursor", () =>
        {
            Check(BugReportInputText.Format(null, null, -4, true) == ("", 0));
            Check(BugReportInputText.CaretVisible(0, 0.49f));
            Check(!BugReportInputText.CaretVisible(0, 0.5f));
            Check(BugReportInputText.CaretVisible(0, 1f));
        });

        Test("report input preserves pasted text and does not split a surrogate pair", () =>
        {
            const string text = "例🐛<color=red>";
            Check(BugReportInputText.Format(text, "追記", 2, true) == ("例追記🐛<color=red>", 3));
            var longText = new string('あ', 1690) + "\n最終行";
            Check(BugReportInputText.Format(longText, null, int.MaxValue, true) == (longText, longText.Length));
        });

        Test("BUG appears in lobby and opens its report form only on explicit click", () =>
        {
            PrepareBugReport();
            RoleGuideButtonPatch.UpdateButtonVisibility();
            Check(BugReportVisible && RoleGuideButtonPatch.BugReportPanelOpenedFor == null);
            AttemptBugReport();
            Check(RoleGuideButtonPatch.BugReportPanelOpenedFor == H);
        });

        Test("BUG hides when play begins and ignores a stale click", () =>
        {
            PrepareBugReport();
            RoleGuideButtonPatch.UpdateButtonVisibility();
            GameStates.IsInGame = true;
            GameStates.IsLobby = false;
            AttemptBugReport();
            RoleGuideButtonPatch.UpdateButtonVisibility();
            Check(!BugReportVisible && RoleGuideButtonPatch.BugReportPanelOpenedFor == null);
        });

        Test("BUG hides when either settings screen opens and returns on close", () =>
        {
            PrepareBugReport();
            H.GameMenu.IsOpen = true;
            RoleGuideButtonPatch.UpdateButtonVisibility();
            AttemptBugReport();
            Check(!BugReportVisible);
            H.GameMenu.IsOpen = false;
            GameSettingMenu.Instance = true;
            RoleGuideButtonPatch.UpdateButtonVisibility();
            AttemptBugReport();
            Check(!BugReportVisible && RoleGuideButtonPatch.BugReportPanelOpenedFor == null);
            GameSettingMenu.Instance = false;
            RoleGuideButtonPatch.UpdateButtonVisibility();
            Check(BugReportVisible);
        });

        Test("BUG hides in Freeplay and after losing the client", () =>
        {
            PrepareBugReport();
            GameStates.IsFreePlay = true;
            RoleGuideButtonPatch.UpdateButtonVisibility();
            AttemptBugReport();
            Check(!BugReportVisible);
            GameStates.IsFreePlay = false;
            AmongUsClient.Instance = null;
            RoleGuideButtonPatch.UpdateButtonVisibility();
            AttemptBugReport();
            Check(!BugReportVisible && RoleGuideButtonPatch.BugReportPanelOpenedFor == null);
        });

        Test("header uses BUG frame when visible and returns to HELP frame when hidden", () =>
        {
            PrepareBugReport();
            Check(RoleGuideButtonPatch.TryGetHeaderLeftEdge(H.transform, out float guideLeft));
            RoleGuideButtonPatch.UpdateButtonVisibility();
            Check(RoleGuideButtonPatch.TryGetHeaderLeftEdge(H.transform, out float bugLeft));
            Near(bugLeft, guideLeft - 0.9f);
            GameStates.IsLobby = false;
            RoleGuideButtonPatch.UpdateButtonVisibility();
            Check(RoleGuideButtonPatch.TryGetHeaderLeftEdge(H.transform, out float restoredLeft));
            Near(restoredLeft, guideLeft);
        });

        Test("round diagnostics are bounded, exclude full paths and stop immediately on round end", () =>
        {
            BugReportRoundLog.Begin();
            for (int i = 0; i < 165; i++)
                BugReportRoundLog.RecordWarning(BepInEx.Logging.LogLevel.Warning, @"C:\private-test\Fixture.cs", 42);
            BugReportRoundLog.Finish("round_ended");
            var lastRound = BugReportRoundLog.Snapshot();
            Check(lastRound.Split('\n').Length == 160 && !lastRound.Contains("private-test"));
            Check(lastRound.Contains("Warning Fixture.cs:42") && lastRound.EndsWith("round_ended"));
            BugReportRoundLog.RecordWarning(BepInEx.Logging.LogLevel.Error, "After.cs", 3);
            Check(lastRound == BugReportRoundLog.Snapshot());
            BugReportRoundLog.Begin();
            Check(!BugReportRoundLog.Snapshot().Contains("Fixture.cs"));
            BugReportRoundLog.Finish("left_game");
        });

        Test("report relay settings require exact HTTPS route and a valid access key", () =>
        {
            var key = new string('a', 32);
            Check(BugReportSubmission.TryConfigure("https://example.invalid/v1/bug-reports", key, out _));
            Check(!BugReportSubmission.TryConfigure("http://example.invalid/v1/bug-reports", key, out _));
            Check(!BugReportSubmission.TryConfigure("https://example.invalid/v1/bug-reports?x=y", key, out _));
            Check(!BugReportSubmission.TryConfigure("https://example.invalid/v1/bug-reports", "placeholder", out _));
        });

        Test("report client preserves Japanese input and omits diagnostics when unticked", () =>
        {
            var handler = new FakeReportHandler(HttpStatusCode.Created, "{\"threadId\":\"123456789123456789\"}");
            using var client = new HttpClient(handler);
            var uri = new Uri("https://example.invalid/v1/bug-reports");
            var result = BugReportSubmission.SendAsync(uri, "fixture", "件名", "本文", "2.2.4", null, client)
                .GetAwaiter().GetResult();
            Check(result.Success && handler.CallCount == 1);
            using var body = JsonDocument.Parse(handler.Body);
            Check(body.RootElement.GetProperty("description").GetString() == "本文");
            Check(body.RootElement.GetProperty("diagnostics").ValueKind == JsonValueKind.Null);
        });

        Test("report client does not automatically retry 429 or an unconfirmed response", () =>
        {
            var uri = new Uri("https://example.invalid/v1/bug-reports");
            foreach (var (status, body) in new[] { (HttpStatusCode.TooManyRequests, "{}"), (HttpStatusCode.Created, "{}") })
            {
                var handler = new FakeReportHandler(status, body);
                using var client = new HttpClient(handler);
                var result = BugReportSubmission.SendAsync(uri, "fixture", "title", "description", "2.2.4", "diagnostics", client)
                    .GetAwaiter().GetResult();
                Check(!result.Success && handler.CallCount == 1);
                using var request = JsonDocument.Parse(handler.Body);
                Check(request.RootElement.GetProperty("diagnostics").GetString() == "diagnostics");
            }
        });
    }

    private sealed class FakeReportHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        internal int CallCount { get; private set; }
        internal string Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            CallCount++;
            Check(request.Method == HttpMethod.Post && request.Headers.Authorization?.Scheme == "Bearer");
            Body = await request.Content.ReadAsStringAsync(cancellation);
            return new HttpResponseMessage(status) { Content = new StringContent(response) };
        }
    }
}
