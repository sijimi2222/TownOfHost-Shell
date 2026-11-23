using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using TMPro;
using UnityEngine;
using TownOfHost.Templates;
using Object = UnityEngine.Object;

namespace TownOfHost;

internal static class YouTubeAuthRuntime
{
    private static YouTubeAuthService service;
    private static bool initialized;
    private static readonly object requestGate = new();
    private static CancellationTokenSource request;
    internal static bool RequestPending => Volatile.Read(ref request) != null;
    internal static string ClientConfigurationPath => Path.Combine(BepInEx.Paths.ConfigPath, "TOH-Shell", "YouTube", "oauth-client.json");
    internal static string CredentialDirectory
    {
        get
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(profile) || !Path.IsPathFullyQualified(profile)) throw new YouTubeCredentialStorageException();
            return Path.Combine(profile, "TOH-Shell", "YouTube");
        }
    }
    private sealed class UnavailableStore : IYouTubeTokenStore
    {
        public YouTubeOAuthTokens Load(string id) => throw new YouTubeCredentialStorageException();
        public void Save(string id, YouTubeOAuthTokens tokens) => throw new YouTubeCredentialStorageException();
        public void Delete() => throw new YouTubeCredentialStorageException();
    }
    internal static YouTubeAuthService Service { get { EnsureInitialized(); return service; } }
    private static YouTubeAuthService Create()
    {
        YouTubeOAuthSettings settings = null;
        try { settings = YouTubeOAuthSettings.Load(ClientConfigurationPath); } catch { /* Fixed status below; never log JSON or credentials. */ }
        YouTubeAuthService result = null;
        void Progress(YouTubeOAuthDiagnostic value) => result?.Trace(value);
        var exchange = new YouTubeDesktopOAuth(progress: Progress);
        var flow = new YouTubeLoopbackOAuth(exchange, uri => { using var process = Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true }); }, Progress);
        IYouTubeTokenStore storage;
        try { storage = new YouTubeProtectedTokenStore(CredentialDirectory); } catch { storage = new UnavailableStore(); }
        // Write directly to BepInEx: the shared Logger may access in-game Unity UI
        // when its optional display flag is enabled, while OAuth runs on a worker.
        result = new(settings, storage, flow, exchange, value => Main.Logger?.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss:ff") + "][YouTubeAuth] " + value.SafeLog));
        return result;
    }
    internal static void EnsureInitialized()
    {
        if (initialized) return;
        service = Create(); initialized = true; Observe(service.InitializeAsync());
        YouTubeOfficialRuntime.Factory = () => new OfficialYouTubeChatSource(Service);
        YouTubeOfficialRuntime.Diagnostics = value => Main.Logger?.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss:ff") + "][YouTubeChat] " + value.SafeLog);
        Application.quitting += (Action)(() => { Cancel(); service?.Dispose(); });
    }
    internal static void Tick()
    {
        // OFF/hidden startup must not touch credentials or construct HTTP services.
        if (!initialized) return;
        if (service.ConsumeRestart()) StreamerInfo.StopNetwork();
    }
    internal static void Begin()
    {
        EnsureInitialized(); if (service.IsBusy) return;
        CancellationTokenSource pending;
        lock (requestGate) { if (request != null) return; request = pending = new(); }
        StreamerInfo.DisconnectInternal();
        // Reload configuration only from the unconfigured state. A configured service
        // owns all refresh/write operations under one gate, including re-linking.
        if (service.State == YouTubeAuthState.NeedsConfiguration) { service.Dispose(); service = Create(); }
        Observe(BeginRequest(service, pending));
    }
    private static async Task BeginRequest(YouTubeAuthService target, CancellationTokenSource pending)
    {
        try { await Task.Run(() => target.BeginAsync(pending.Token)).ConfigureAwait(false); }
        finally
        {
            lock (requestGate) { if (ReferenceEquals(request, pending)) request = null; }
            pending.Dispose();
        }
    }
    internal static void Cancel()
    {
        lock (requestGate) request?.Cancel();
        service?.CancelAuthorization();
    }
    internal static void Unlink()
    {
        Cancel(); StreamerInfo.DisconnectInternal(); var target = Service; Observe(Task.Run(target.UnlinkAsync));
    }
    private static async void Observe(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch { Logger.Warn("YouTube連携処理を停止しました。連携画面の状態を確認してください。", "YouTubeAuth"); }
    }
}
internal static class YouTubeAuthMenu
{
    private static GameObject root;
    private static TextMeshPro status;
    private static YouTubeAuthService shownService;
    private static SimpleButton begin, unlink, cancel;
    private static bool shownPending;
    private static int revision = -1, streamRevision = -1;
    private static bool initializationWarningLogged;
    private static bool ControlsReady => status != null
        && !SimpleButton.IsNullOrDestroyed(begin) && begin.Label != null
        && !SimpleButton.IsNullOrDestroyed(unlink) && unlink.Label != null
        && !SimpleButton.IsNullOrDestroyed(cancel) && cancel.Label != null;
    internal static bool Initialize(MainMenuManager menu)
    {
        if (menu == null || menu.screenTint == null || menu.quitButton == null) return false;
        if (root != null && root.transform.parent == menu.screenTint.transform && ControlsReady) return true;
        DiscardView();
        try
        {
            // VersionShower.Start may not have seeded TMPTemplate yet. Use the ready
            // menu prefab as a valid donor, only when the user opens this page.
            var font = menu.quitButton.GetComponentInChildren<TextMeshPro>(true);
            if (font == null) { WarnInitializationOnce(); return false; }
            TMPTemplate.SetBase(font);
            root = new GameObject("ShellYouTubeAuth");
            root.SetActive(false); // Keep partial construction out of LateUpdate/input.
            root.layer = menu.screenTint.gameObject.layer;
            root.transform.SetParent(menu.screenTint.transform, false);
            Text("YouTubeAuthTitle", "Google / YouTube 連携", new(0, 2.45f, -6), 2.8f, new(10, 0.7f));
            status = Text("YouTubeAuthStatus", "", new(0, 1.35f, -6), 1.8f, new(10, 1.2f));
            begin = Button("GoogleLinkStart", "Google連携開始", new(-2.1f, 0.25f, -7), () => YouTubeAuthRuntime.Begin());
            unlink = Button("GoogleLinkRemove", "連携解除", new(2.1f, 0.25f, -7), () => YouTubeAuthRuntime.Unlink());
            cancel = Button("GoogleLinkCancel", "認証をキャンセル", new(0, -0.5f, -7), () => YouTubeAuthRuntime.Cancel());
            Button("GooglePermissions", "Google権限管理", new(0, -1.15f, -7), () => Application.OpenURL(YouTubeOfficialRuntime.PermissionUrl));
            Text("YouTubeAuthDetails", "自分のGoogleアカウントで認証します。一般プレイヤーは認証不要です。\n権限はYouTube読取専用。Testing環境は通常7日で再認証が必要です。\n連携情報は本人のWindowsユーザー用DPAPIで保護します。\n再連携の失敗・中断では旧連携を保持し、解除時は端末のトークンを削除します。\nGoogle側の解除は同アカウント・同プロジェクトの他の連携にも影響します。\n外部テストは設定提供の許可待ち。参加希望受付も規約確認待ちです。", new(0, -1.8f, -6), 1.0f, new(10, 1.1f));
            Button("GoogleLinkBack", "戻る", new(0, -2.55f, -7), () => { Hide(); CreateStreameMenu.ShowMenu(); });
            initializationWarningLogged = false;
            return true;
        }
        catch (Exception)
        {
            DiscardView();
            WarnInitializationOnce();
            return false;
        }
    }
    private static void WarnInitializationOnce()
    {
        if (initializationWarningLogged) return;
        initializationWarningLogged = true;
        Logger.Warn("YouTube連携画面を生成できませんでした。元の画面を維持します。", "YouTubeAuth");
    }
    private static void DiscardView()
    {
        if (root != null) { root.SetActive(false); Object.Destroy(root); }
        root = null; status = null; begin = unlink = cancel = null; shownService = null;
        revision = streamRevision = -1;
    }
    private static TextMeshPro Text(string name, string value, Vector3 position, float size, Vector2 area)
    {
        var text = TMPTemplate.Create(name, value, Color.white, size, TextAlignmentOptions.Center, true, root.transform);
        text.transform.localPosition = position; text.rectTransform.sizeDelta = area; text.enableWordWrapping = true; return text;
    }
    private static SimpleButton Button(string name, string label, Vector3 position, Action action)
    {
        var button = MainMenuManagerPatch.CreateButton(name, position, new(80, 80, 160, 255), new(110, 110, 200, 255), action, label, scale: new(3.2f, 0.45f), transform: root.transform);
        button.FontSize = 2f; return button;
    }
    internal static void Show(MainMenuManager menu)
    {
        if (!Initialize(menu)) return;
        CreateStreameMenu.CloseMenu(); menu.screenTint.enabled = true; root.SetActive(true); revision = -1; Refresh();
    }
    internal static void Hide()
    {
        if (root == null || !root.activeSelf) return;
        YouTubeAuthRuntime.Cancel(); root.SetActive(false);
    }
    internal static void Tick()
    {
        if (root == null || !root.activeInHierarchy) return;
        if (!ControlsReady) { DiscardView(); WarnInitializationOnce(); return; }
        YouTubeAuthRuntime.Tick(); var service = YouTubeAuthRuntime.Service;
        if (shownPending != YouTubeAuthRuntime.RequestPending || !ReferenceEquals(shownService, service) || revision != service.Revision || streamRevision != StreamerInfo.StatusVersion) Refresh();
        if (Input.GetKeyDown(KeyCode.Escape)) { Hide(); CreateStreameMenu.ShowMenu(); }
    }
    private static void Refresh()
    {
        if (!ControlsReady) return;
        var service = YouTubeAuthRuntime.Service;
        status.text = service.Message;
        if (StreamerInfo.HasConnectionError) status.text += "\n" + StreamerInfo.StatusMessage;
        begin.Label.text = service.HasSavedLink ? "Googleを再連携" : "Google連携開始";
        begin.Button.gameObject.SetActive(!service.IsBusy && !YouTubeAuthRuntime.RequestPending); unlink.Button.gameObject.SetActive(service.State != YouTubeAuthState.Restoring);
        cancel.Button.gameObject.SetActive(YouTubeAuthRuntime.RequestPending || service.State == YouTubeAuthState.Authorizing);
        shownPending = YouTubeAuthRuntime.RequestPending; shownService = service; revision = service.Revision; streamRevision = StreamerInfo.StatusVersion;
    }
}
[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate))]
internal static class YouTubeAuthMenuUpdatePatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)] private static void Postfix() => YouTubeAuthMenu.Tick();
}
