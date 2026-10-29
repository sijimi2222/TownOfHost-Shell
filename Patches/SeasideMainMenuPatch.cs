using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using TMPro;
using TownOfHost.Modules.MainMenu;
using TownOfHost.Templates;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TownOfHost;

// Layered home artwork with local visual feedback. Native actions, navigation and network behaviour are preserved.
[HarmonyPatch]
internal static partial class SeasideMainMenuPatch
{
    static HomeView view;
    static OptionsMenuBehaviour options;
    static AnnouncementPopUp news;
    static bool menuTickLogged, managerTickLogged, accountWindowOpen;
    static string lastDiagnostic;
    static int diagnosticCount;

    // A managed HomeView can outlive its destroyed Unity owner. Never replay its snapshots in a game scene.
    static bool MenuSceneAvailable(MainMenuManager owner)
    {
        if (owner == null || HudManager.InstanceExists || PlayerControl.LocalPlayer != null) return false;
        var scene = owner.gameObject.scene;
        return scene.IsValid() && scene.isLoaded && scene.handle == SceneManager.GetActiveScene().handle;
    }
    static bool OwnsMenuUi(Component popup)
    {
        var owner = view?.Owner ?? earlyOwner;
        if (popup == null || !MenuSceneAvailable(owner)) return false;
        var scene = popup.gameObject.scene;
        // Native options/error services can own persistent popups outside the menu scene.
        return scene.IsValid() && scene.isLoaded
            && (scene.handle == owner.gameObject.scene.handle || scene.name == "DontDestroyOnLoad");
    }
    static void ReleaseMenuPresentation()
    {
        CancelOptionsSlide();
        // Clear shared handles first: destruction can synchronously invoke other patched UI callbacks.
        var previous = view; view = null;
        options = null; news = null; accountWindowOpen = false;
        resumeNativeOwner = null; skipNativeReset = false; startupPopups.Clear(); navigationAway = true;
        try { previous?.Dispose(); }
        finally { StopLoading(); }
    }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
    static void Start(MainMenuManager __instance)
    {
        if (!MenuSceneAvailable(__instance)) return;
        CancelOptionsSlide();
        view?.Dispose(); view = null;
        skipNativeReset = false; resumeNativeOwner = null;
        menuTickLogged = managerTickLogged = accountWindowOpen = false; lastDiagnostic = null; diagnosticCount = 0;
        // Standard menu: the obsolete opt-out key is deliberately neither bound nor read.
        Logger.Info("SEASIDE_STANDARD_MENU START", "SeasideMenu");
        try { view = new HomeView(__instance); Logger.Info("MENU_VIEW_BUILT", "SeasideMenu"); Present(__instance); }
        catch { FailSafe(); }
    }
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
    static void Tick(MainMenuManager __instance)
    {
        if (!menuTickLogged) { menuTickLogged = true; Logger.Info("NATIVE_MENU_TICK", "SeasideMenu"); }
        try
        {
            if (view != null && view.Owner != __instance) return;
            Present(__instance);
        }
        catch { FailSafe(); }
    }
    static bool PopupOpen() => BlockingPopup() != null;
    static string BlockingPopup()
    {
        if (StartupPopupOpen()) return "startup";
        if (ErrorText.Instance != null && ErrorText.Instance.Text != null && ErrorText.Instance.Text.enabled
            && ErrorText.Instance.Text.gameObject.activeInHierarchy) return "error";
        if (accountWindowOpen) return "account";
        if (options != null && options.IsOpen && !(view?.OwnsOptions(options) ?? false)) return "unowned-options";
        if (news != null && news.gameObject.activeInHierarchy) return "news";
        if (FriendsListUI.Instance != null && FriendsListUI.Instance.IsOpen && !(view?.OwnsFriends(FriendsListUI.Instance) ?? false)) return "unowned-friends";
        return null;
    }

    // Restore underlying controls before their original handlers construct/open a modal or another screen.
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenCreateGame))]
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenAccountMenu))]
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenCredits))]
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenOnlineMenu))]
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenEnterCodeMenu))]
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.TransitionToInventory))]
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.TransitionToShop))]
    [HarmonyPrefix]
    static void LeavingHome() { navigationAway = true; loading?.Hide(); view?.ReleaseOptionsInput(); view?.Hide(); }

    // Native options may deactivate their underlying menu without leaving the current scene.
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.DeactivateMainMenuUI)), HarmonyPrefix]
    static void DeactivatingHome()
    {
        if (view?.OptionsBlocking ?? false) { view.TraceOptionsLifecycle("native_deactivate"); return; }
        LeavingHome();
    }

    [HarmonyPatch(typeof(FriendsListUI), nameof(FriendsListUI.Open)), HarmonyPrefix]
    static bool FriendsOpening()
    {
        if (!MenuSceneAvailable(view?.Owner ?? earlyOwner)) return true;
        loading?.Hide();
        bool alreadyOpen = view?.OwnsFriends(FriendsListUI.Instance) ?? false;
        if (!alreadyOpen) view?.BeginFriendsAudit();
        view?.PrepareFriends();
        return !alreadyOpen; // Reverse our slide without pushing another native controller/menu state.
    }

    // AccountTabActive means that the persistent account tab is available even on the home screen.
    // Only an explicitly opened account window suspends the seaside page.
    [HarmonyPatch(typeof(AccountManager), nameof(AccountManager.OpenAccountWindow)), HarmonyPrefix]
    static void AccountWindowOpened() { if (!MenuSceneAvailable(view?.Owner ?? earlyOwner)) return; accountWindowOpen = true; loading?.Hide(); view?.Hide(); }
    [HarmonyPatch(typeof(AccountManager), nameof(AccountManager.CloseAccountWindow)), HarmonyPostfix]
    static void AccountWindowClosed() { if (!MenuSceneAvailable(view?.Owner ?? earlyOwner)) return; accountWindowOpen = false; Present(view?.Owner ?? earlyOwner); }

    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Open)), HarmonyPrefix]
    static bool OpenOptions(OptionsMenuBehaviour __instance)
    {
        if (!OwnsMenuUi(__instance))
        {
            Logger.Info("SEASIDE_OPTIONS_OUTSIDE_MENU_IGNORED", "SeasideMenu");
            return true; // Leave the game's own options/HUD lifecycle entirely in charge.
        }
        if (ReuseOptionsOpen(__instance)) return false;
        options = __instance; loading?.Hide();
        bool backdrop = view?.PrepareOptions(__instance) ?? false;
        if (!backdrop) view?.Hide();
        Logger.Info(backdrop ? "SEASIDE_OPTIONS_BACKDROP_KEPT" : "SEASIDE_OPTIONS_HOME_SUSPENDED", "SeasideMenu");
        return true;
    }
    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Close)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
    static void CloseOptions(OptionsMenuBehaviour __instance)
    {
        if (options != __instance || DefersOptionsClose(__instance)) return;
        FinishOptions(__instance);
        if (OwnsMenuUi(__instance)) Present(view?.Owner ?? earlyOwner);
    }
    [HarmonyPatch(typeof(AnnouncementPopUp), nameof(AnnouncementPopUp.Show)), HarmonyPrefix]
    static void OpenNews(AnnouncementPopUp __instance) { if (!OwnsMenuUi(__instance)) return; news = __instance; loading?.Hide(); view?.Hide(); }
    [HarmonyPatch(typeof(AnnouncementPopUp), nameof(AnnouncementPopUp.OnEnable)), HarmonyPostfix]
    static void NewsEnabled(AnnouncementPopUp __instance) { if (!OwnsMenuUi(__instance)) return; news = __instance; loading?.Hide(); view?.Hide(); }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.ResetScreen)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
    static void Reset(MainMenuManager __instance)
    {
        if (skipNativeReset) { skipNativeReset = false; return; }
        navigationAway = false;
        if (view != null && view.Owner == __instance) view.Reset();
        Present(__instance);
    }
    [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
    static void Stamp(ModManager __instance)
    {
        var owner = view?.Owner ?? earlyOwner;
        if (!MenuSceneAvailable(owner))
        {
            if (view != null || loading != null) ReleaseMenuPresentation();
            return;
        }
        if (loading != null && loading.Visible && __instance.ModStamp != null) loading.Lift(__instance.ModStamp);
        if (view == null) return;
        if (view.Owner == null) { view.Dispose(); view = null; return; }
        if (!managerTickLogged) { managerTickLogged = true; Logger.Info("MOD_MANAGER_TICK", "SeasideMenu"); }
        try
        {
            string state = view.Diagnostic() + ";popup=" + PopupOpen() + ";popupKind=" + (BlockingPopup() ?? "none") + ";optionsOwned=" + view.OptionsBlocking;
            if (diagnosticCount < 12 && state != lastDiagnostic)
            {
                lastDiagnostic = state; diagnosticCount++; Logger.Info(state, "SeasideMenu");
            }
            if (view.Visible && __instance.ModStamp != null) view.PositionStamp(__instance.ModStamp);
        }
        catch { if (diagnosticCount++ < 12) Logger.Warn("MENU_DIAGNOSTIC_UNAVAILABLE", "SeasideMenu"); }
    }
    static void FailSafe()
    {
        CancelOptionsSlide();
        try { view?.Dispose(); } catch { /* Best-effort scene teardown. */ } finally { view = null; }
        // Do not log exception payloads, account details, paths or button text.
        StopLoading(); presentationFailed = true;
        Logger.Warn("Seaside menu could not be displayed. Restored the original menu.", "SeasideMenu");
    }

    internal static void FitSurface(MainMenuManager owner, GameObject surface, Transform image, SpriteRenderer letterbox, Camera camera)
    {
            float height = camera.orthographicSize*2, width = height*camera.aspect;
            float scale = (float)SeasideMenuLayout.Fit(width,height).Scale*100;
            var point = camera.ViewportToWorldPoint(new Vector3(.5f,.5f,0));
            surface.transform.position = new(point.x,point.y,owner.quitButton.transform.position.z-.25f);
            surface.transform.rotation = camera.transform.rotation; surface.transform.localScale = Vector3.one*scale;
            // Different resolution artwork retains normalized logical coordinates and never stretches its own aspect ratio.
            var size = image.GetComponent<SpriteRenderer>().sprite.bounds.size;
            float imageScale = Math.Min((float)SeasideMenuLayout.Width/100/size.x,(float)SeasideMenuLayout.Height/100/size.y);
            image.localScale = new(imageScale,imageScale,1);
            letterbox.transform.localPosition = new(0,0,.1f); letterbox.transform.localScale = new(width/scale,height/scale,1);
    }

    internal sealed partial class HomeView : IDisposable
    {
        internal readonly MainMenuManager Owner;
        readonly GameObject root;
        readonly Transform art;
        readonly SpriteRenderer matte;
        readonly List<PassiveButton> proxies = new();
        readonly Dictionary<string, PassiveButton> sources = new(StringComparer.Ordinal);
        readonly Dictionary<Collider2D, bool> blocked = new();
        readonly Dictionary<Renderer, bool> covered = new();
        readonly List<(PassiveButton proxy, PassiveButton source)> updateActions = new();
        Sprite artwork;
        bool ownsArtwork;
        readonly TextMeshPro version, gameVersion, friendsText;
        readonly FriendsListButton friends;
        readonly AccountManager account;
        VersionShower originalVersion;
        readonly List<(SpriteRenderer renderer, Vector3 position, Vector3 scale, int order)> stamps = new();
        bool disposed;
        internal bool Visible => root != null && root.activeSelf;

        internal HomeView(MainMenuManager owner)
        {
            Owner = owner;
            // Validate all feature adapters before covering any original controls.
            sources.Add("Play", owner.playButton); sources.Add("Inventory", owner.inventoryButton);
            sources.Add("Shop", owner.shopButton); sources.Add("News", owner.newsButton);
            sources.Add("Account", owner.myAccountButton); sources.Add("Settings", owner.settingsButton);
            sources.Add("Credits", owner.creditsButton); sources.Add("Quit", owner.quitButton);
            foreach (var target in SeasideMenuLayout.Targets.Skip(8)) sources.Add(target.Id, MainMenuManagerPatch.SeasideAction(target.Id));
            if (sources.Count != 14 || sources.Values.Any(b => b == null || b.OnClick == null))
                throw new InvalidOperationException("Incomplete main menu adapters");
            friends = Object.FindObjectOfType<FriendsListButton>();
            account = Object.FindObjectOfType<AccountManager>();
            originalVersion = Object.FindObjectOfType<VersionShower>();
            layerPack = SeasideMenuLayers.TryLoad();
            Sprite background = artwork = layerPack?.Background;
            if (background == null) background = artwork = LoadArtwork(out ownsArtwork);
            if (background == null) throw new InvalidOperationException("Missing main menu artwork");

            root = new GameObject("ShellSeasideHome");
            root.SetActive(false); root.layer = owner.quitButton.gameObject.layer;
            root.transform.SetParent(owner.transform, false);
            try
            {
                matte = SpriteObject("Letterbox", root.transform, SolidSprite(), Color.black, 0);
                var image = SpriteObject("Artwork", root.transform, background, Color.white, 1);
                art = image.transform;
                foreach (var target in SeasideMenuLayout.Targets)
                {
                    string id = target.Id;
                    var button = NewButton(id, target.Rect, () => RequestClick(id, target.LeavesHome), false);
                    AttachLayer(button, target);
                }
                // Preserve the updater entry points when an actual update is offered by the existing updater.
                AddUpdater(MainMenuManagerPatch.UpdateButton?.Button, "Update", new(900, 98, 260, 46), "アップデート");
                AddUpdater(MainMenuManagerPatch.UpdateButton2?.Button, "UpdateDetails", new(900, 150, 260, 46), "更新情報");
                CreatePlayPage();
                version = NewText("ShellVersion", SeasideMenuLayout.VersionBox, 3.1f, new Color32(40, 30, 50, 255));
                gameVersion = NewText("GameVersion", SeasideMenuLayout.GameVersionBox, 2.8f, Color.white);
                // The Quit prototype may use uppercase; preserve vanilla version casing verbatim.
                gameVersion.fontStyle = FontStyles.Normal;
                gameVersion.alignment = TextAlignmentOptions.Left;
                // Preserve access to the original friends UI and its live unread count without exposing account identifiers.
                var friendsButton = friends == null ? null : friends.Button.GetComponentInChildren<PassiveButton>(true);
                if (friendsButton != null)
                {
                    NewButton("Friends", SeasideMenuLayout.FriendsBox, () => { if (FriendsBlocking) return; friendsButton.OnClick.Invoke(); if (FriendsListUI.Instance == null || !FriendsListUI.Instance.IsOpen) Hide(); }, true);
                    friendsText = NewText("FriendsLabel", SeasideMenuLayout.FriendsBox, 2.5f, Color.white);
                }
            }
            catch { DisposePlayPage(); Object.Destroy(root); ReleaseArtwork(); throw; }
        }
        internal static Sprite LoadArtwork(out bool owned)
        {
            owned = false;
            // Optional artist replacement; use the same composition/normalized positions. Never download an image.
            string path = Path.Combine(Paths.ConfigPath, "TOH-Shell", "MainMenu", "background.png");
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                if (info.Length > 0 && info.Length <= 16 * 1024 * 1024)
                {
                    Texture2D texture = null;
                    try
                    {
                        texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                        if (ImageConversion.LoadImage(texture, File.ReadAllBytes(path)) && texture.width <= 8192 && texture.height <= 8192)
                        {
                            var area = SeasideMenuLayout.ArtworkPixels(texture.width, texture.height);
                            var sprite = Sprite.Create(texture, new Rect((float)area.X, (float)area.Y, (float)area.Width, (float)area.Height), new(.5f, .5f), 100);
                            owned = true;
                            return sprite;
                        }
                    }
                    catch { /* Invalid optional replacement falls back to the embedded original. */ }
                    if (texture != null) Object.Destroy(texture);
                }
            }
            if (embeddedArtwork != null) return embeddedArtwork;
            var embeddedTexture = UtilsSprite.GetOrLoadTexture(SeasideMenuLayout.EmbeddedAsset);
            if (embeddedTexture == null) return null;
            var pixels = SeasideMenuLayout.ArtworkPixels(embeddedTexture.width, embeddedTexture.height);
            return embeddedArtwork = Sprite.Create(embeddedTexture,
                new Rect((float)pixels.X, (float)pixels.Y, (float)pixels.Width, (float)pixels.Height), new(.5f, .5f), 100);
        }
        static Sprite embeddedArtwork;
        static Sprite white;
        internal static Sprite SolidSprite()
        {
            if (white != null) return white;
            var t = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            t.SetPixel(0, 0, Color.white); t.Apply();
            return white = Sprite.Create(t, new Rect(0, 0, 1, 1), new(.5f, .5f), 1);
        }
        internal static SpriteRenderer SpriteObject(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name); go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = sprite; r.color = color; r.sortingOrder = order;
            return r;
        }
        internal static Vector3 At(double x, double y, float z = -.1f)
        {
            var p = SeasideMenuLayout.Local(x, y); return new((float)p.x, (float)p.y, z);
        }
        PassiveButton NewButton(string id, SeasideRect rect, Action action, bool visibleChip)
        {
            var simple = new SimpleButton(root.transform, "Seaside_" + id, At(rect.CenterX, rect.CenterY),
                Color.clear, Color.clear, () => { if (!FriendsBlocking && !OptionsBlocking) action(); }, "", false);
            var b = simple.Button;
            b.transform.localScale = Vector3.one;
            foreach (var anchor in b.GetComponentsInChildren<AspectPosition>(true)) { anchor.enabled = false; Object.Destroy(anchor); }
            foreach (var r in b.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var c in b.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
            b.OnMouseOver = new(); b.OnMouseOut = new();
            // A separate polygon avoids the slanted corners of adjacent drawn buttons.
            var shape = b.gameObject.AddComponent<PolygonCollider2D>();
            float w = (float)(rect.Width / 100), h = (float)(rect.Height / 100), bevel = Math.Min(.36f, w * .07f);
            shape.points = new Vector2[] { new(-w/2,0), new(-w/2+bevel,h/2), new(w/2-bevel,h/2), new(w/2,0), new(w/2-bevel,-h/2), new(-w/2+bevel,-h/2) };
            b.Colliders = new Collider2D[] { shape }; b.ClickMask = null;
            b.ControllerNav = ControllerNavigation.defaultNavigation;
            var focus = SpriteObject("Focus", b.transform, SolidSprite(), new Color(1,1,1,.15f), 3);
            focus.transform.localPosition = new(0,0,-.01f); focus.transform.localScale = new(w*.86f,h*.75f,1);
            focus.enabled = false;
            b.OnMouseOver.AddListener((Action)(() => {
                // Controller highlights can invoke hover events without a collider raycast.
                if (FriendsBlocking || OptionsBlocking) { focus.enabled = false; return; }
                SetButtonHover(id, true); focus.enabled = true;
            }));
            b.OnMouseOut.AddListener((Action)(() => { SetButtonHover(id, false); focus.enabled = false; }));
            if (visibleChip)
            {
                var chip = SpriteObject("FriendsChip", b.transform, SolidSprite(), new Color(.12f,.1f,.22f,.8f), 2);
                chip.transform.localPosition = new(0,0,.01f); chip.transform.localScale = new(w,h,1);
            }
            b.gameObject.SetActive(true); proxies.Add(b); return b;
        }
        void AddUpdater(PassiveButton source, string id, SeasideRect rect, string label)
        {
            if (source == null) return;
            var proxy = NewButton(id, rect, () => { Hide(); source.OnClick.Invoke(); }, true);
            var text = NewText(id + "Label", rect, 2.5f, Color.white);
            text.text = label;
            text.transform.SetParent(proxy.transform, true);
            proxy.gameObject.SetActive(source.gameObject.activeInHierarchy);
            updateActions.Add((proxy,source));
        }
        TextMeshPro NewText(string id, SeasideRect rect, float font, Color color)
        {
            var source = Owner.quitButton.GetComponentInChildren<TextMeshPro>(true);
            if (source == null) throw new InvalidOperationException("Missing menu font");
            var text = Object.Instantiate(source, root.transform); text.name = id; text.DestroyTranslator();
            var aspect = text.GetComponent<AspectPosition>();
            if (aspect != null) { aspect.enabled = false; Object.Destroy(aspect); }
            text.transform.localPosition = At(rect.CenterX,rect.CenterY,-.15f); text.transform.localScale = Vector3.one;
            text.rectTransform.sizeDelta = new((float)rect.Width/100,(float)rect.Height/100);
            text.alignment = TextAlignmentOptions.Center; text.color = color;
            text.fontSize = text.fontSizeMax = font; text.fontSizeMin = font*.6f;
            text.enableAutoSizing = true; text.enableWordWrapping = false; text.text = "";
            text.GetComponent<MeshRenderer>().sortingOrder = 4;
            text.GetComponent<MeshRenderer>().enabled = true;
            text.gameObject.SetActive(true); return text;
        }
        void Invoke(string id, bool leavesHome)
        {
            if (layerPack != null && (id == "Play" || SeasidePlayLayout.Ids.Contains(id))) { if (CanInvoke(id)) InvokePlay(id); return; }
            if (!Visible || !sources.TryGetValue(id, out var source) || source == null || !source.enabled
                || !source.gameObject.activeInHierarchy) return;
            if (leavesHome && id != "Settings") Hide();
            source.OnClick.Invoke();
        }
        internal string Diagnostic()
        {
            var camera = Camera.main;
            return "MENU_STATE visible=" + Visible + ";startup=" + Owner.finishStartup + ";animating=" + Owner.animating
                + ";nativeHome=" + Owner.IsMainMenuActive() + ";logo=" + (CredentialsPatch.TOHhmLogo != null && CredentialsPatch.TOHhmLogo.gameObject.activeInHierarchy)
                + ";tint=" + Owner.screenTint.enabled + ";credits=" + (Owner.creditsScreen != null && Owner.creditsScreen.activeInHierarchy)
                + ";account=" + (account != null && account.AccountTabActive) + ";camera=" + (camera != null)
                + ";ortho=" + (camera != null && camera.orthographic)
                + ";layers=" + UsesSeparatedAssets + ";asset=" + artwork.texture.width + "x" + artwork.texture.height
                + ";artActive=" + art.gameObject.activeInHierarchy + ";renderer=" + art.GetComponent<SpriteRenderer>().enabled
                + ";renderVisible=" + art.GetComponent<SpriteRenderer>().isVisible;
        }
        internal void Tick(bool allowDisplay)
        {
            if (optionsInputs.Count > 0 && (optionsUi == null || !optionsUi.IsOpen || !optionsUi.gameObject.activeInHierarchy)) ReleaseOptionsInput();
            TickFriends();
            bool ready = allowDisplay && !disposed && MenuSceneAvailable(Owner) && Owner.gameObject.activeInHierarchy && Owner.finishStartup && (FriendsBlocking || OptionsBlocking || (!Owner.animating && Owner.IsMainMenuActive()
                && CredentialsPatch.TOHhmLogo != null && CredentialsPatch.TOHhmLogo.gameObject.activeInHierarchy
                && !Owner.screenTint.enabled)) && (Owner.creditsScreen == null || !Owner.creditsScreen.activeInHierarchy);
            if (!ready) { Hide(); return; }
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) { Hide(); return; }
            Fit(cam);
            if (!Visible) Show();
            bool navigationChanged = false;
            foreach (var entry in updateActions)
            {
                bool show = transition.Page == SeasidePage.Home && !transition.Active && entry.source != null && entry.source.gameObject.activeInHierarchy;
                if (show)
                {
                    // Updater availability can arrive asynchronously after Show. Never leave its old hit area alive.
                    foreach (var c in entry.source.GetComponentsInChildren<Collider2D>())
                        if (c.enabled && !blocked.ContainsKey(c)) { blocked[c] = true; c.enabled = false; }
                    Cover(entry.source.GetComponentsInChildren<Renderer>());
                }
                navigationChanged |= entry.proxy.gameObject.activeSelf != show;
                entry.proxy.gameObject.SetActive(show);
            }
            if (navigationChanged && navState != null && !FriendsBlocking && !OptionsBlocking) RefreshNavigation();
            version.text = "TOH-Shell v" + Main.PluginShowVersion;
            UpdateGameVersion();
            if (friendsText != null) friendsText.text = "フレンド・通知" + (friends.NotifCount == null ? "" : " " + friends.NotifCount.text);
            // Do not allow the unseen legacy controls to acquire focus between frames.
            foreach (var pair in blocked) if (pair.Key != null) pair.Key.enabled = false;
            foreach (var pair in covered) if (pair.Key != null) pair.Key.enabled = false;
            if (OptionsBlocking) SuppressOptionsInput();
            if (!FriendsBlocking && !OptionsBlocking)
            {
                ReconcileNavigation();
                TickPage();
                if (!transition.Active) TickButtonAnimations();
            }
        }
        void UpdateGameVersion()
        {
            if (originalVersion == null) originalVersion = Object.FindObjectOfType<VersionShower>();
            // Reuse the actual vanilla menu label, including its version suffix and build number.
            // Before its widget is ready, show nothing rather than inventing another version format.
            gameVersion.text = originalVersion != null && originalVersion.text != null ? originalVersion.text.text : "";
            // Hide only the original version text, not error/notice objects sharing its parent.
            if (originalVersion != null && originalVersion.text != null)
                Cover(originalVersion.text.GetComponentsInChildren<Renderer>());
        }
        void Fit(Camera camera) => FitSurface(Owner,root,art,matte,camera);
        void Show()
        {
            if (Visible) return;
            blocked.Clear(); covered.Clear();
            foreach (var renderer in Owner.GetComponentsInChildren<Renderer>())
                if (renderer.enabled && !renderer.transform.IsChildOf(root.transform) && !IsFriendsContent(renderer) && !IsOptionsContent(renderer)) { covered[renderer] = true; renderer.enabled = false; }
            // Include persistent account/friends entry buttons so hidden legacy hit areas cannot intercept clicks.
            foreach (var button in Object.FindObjectsOfType<PassiveButton>())
                if (!button.transform.IsChildOf(root.transform) && !IsFriendsContent(button) && !IsOptionsContent(button))
                    foreach (var c in button.GetComponentsInChildren<Collider2D>())
                        if (c.enabled && !blocked.ContainsKey(c)) { blocked[c] = true; c.enabled = false; }
            if (friends != null) Cover(friends.GetComponentsInChildren<Renderer>());
            foreach (var credential in Object.FindObjectsOfType<TextMeshPro>())
                if (credential.gameObject.name == "TOHCredentialsText") Cover(credential.GetComponentsInChildren<Renderer>());
            root.SetActive(true); RegisterNavigation();
        }
        void Cover(IEnumerable<Renderer> renderers)
        {
            foreach (var r in renderers)
                if (r != null && r.enabled && !r.transform.IsChildOf(root.transform) && !IsFriendsContent(r) && !IsOptionsContent(r)) { covered[r] = true; r.enabled = false; }
        }
        internal void Hide()
        {
            // On scene teardown, old native navigation/snapshots are no longer ours to restore.
            CancelFriends();
            bool restore = MenuSceneAvailable(Owner);
            // Visibility is not modal ownership. A native reset/temporary hide can run during Open.
            // Keep the lease until native Close/Disable/Destroy or an actual scene departure.
            if (disposed || !restore) ReleaseOptionsInput();
            else TraceOptionsLifecycle("temporary_hide");
            if (!restore)
            {
                blocked.Clear(); covered.Clear(); stamps.Clear();
                ClearNavigationLease();
            }
            CancelTransition(); ResetButtonAnimations();
            if (root == null || !root.activeSelf) return;
            root.SetActive(false);
            foreach (var item in blocked) if (item.Key != null) item.Key.enabled = item.Value;
            foreach (var item in covered) if (item.Key != null) item.Key.enabled = item.Value;
            blocked.Clear(); covered.Clear();
            foreach (var s in stamps) if (s.renderer != null) { s.renderer.transform.position = s.position; s.renderer.transform.localScale = s.scale; s.renderer.sortingOrder = s.order; }
            stamps.Clear();
            ReleaseNavigationLease();
        }
        internal void PositionStamp(SpriteRenderer stamp)
        {
            if (!stamps.Any(s => s.renderer == stamp)) stamps.Add((stamp,stamp.transform.position,stamp.transform.localScale,stamp.sortingOrder));
            stamp.transform.position = root.transform.TransformPoint(At(1590,210,-.2f));
            stamp.sortingOrder = 6;
        }
        internal void Reset() { TraceOptionsLifecycle("native_reset"); Hide(); awaitingNativePlay=false; transition.Reset(); ApplyPage(); }
        void ReleaseArtwork()
        {
            if (!ownsArtwork || artwork == null) return;
            Object.Destroy(artwork.texture); Object.Destroy(artwork); artwork = null; ownsArtwork = false;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; Hide(); DisposePlayPage();
            if (root != null) Object.Destroy(root);
            ReleaseArtwork();
        }
    }
}
