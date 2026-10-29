using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using TownOfHost.Modules.MainMenu;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfHost;

// A non-interactive surface exists before vanilla Start and stays until the real home is ready.
// No sleep, timeout, tween or replacement of the game's startup routine.
internal static partial class SeasideMainMenuPatch
{
    static LoadingView loading;
    static MainMenuManager earlyOwner;
    static bool navigationAway, presentationFailed;
    static Camera.CameraCallback beforeCamera, afterCamera;
    static readonly List<Component> startupPopups = new();

    static void ConfigureEarly()
    {
        if (beforeCamera != null) return;
        beforeCamera = (Action<Camera>)BeforeCamera;
        Camera.onPreCull += beforeCamera;
        afterCamera = (Action<Camera>)AfterFriendsCamera;
        Camera.onPostRender += afterCamera;
    }
    [HarmonyPatch(typeof(SplashManager), nameof(SplashManager.Start)), HarmonyPostfix]
    static void PreloadSeaside()
    {
        try
        {
            ConfigureEarly();
            if (SeasideMenuLayers.TryLoad() == null)
                UtilsSprite.GetOrLoadTexture(SeasideMenuLayout.EmbeddedAsset);
            Logger.Info("SEASIDE_PRELOAD frame=" + Time.frameCount, "SeasideMenu");
        }
        catch { Logger.Warn("SEASIDE_PRELOAD_DEFERRED", "SeasideMenu"); }
    }
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Awake)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
    static void EarlyAwake(MainMenuManager __instance) => PrepareEarly(__instance,"AWAKE");
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPrefix]
    static void EarlyStart(MainMenuManager __instance) => PrepareEarly(__instance,"START_PREFIX");
    static void PrepareEarly(MainMenuManager owner, string phase)
    {
        try
        {
            if (!MenuSceneAvailable(owner)) return;
            ConfigureEarly();
            if (earlyOwner != owner)
            {
                StopLoading();
                if (view != null && view.Owner != owner) { view.Dispose(); view = null; }
                earlyOwner = owner; navigationAway = presentationFailed = false; startupPopups.Clear();
            }
            if (presentationFailed) return;
            loading ??= new LoadingView(owner);
            Present(owner);
            Logger.Info("SEASIDE_" + phase + " frame=" + Time.frameCount + ";background=" + loading.Visible, "SeasideMenu");
        }
        catch { Logger.Warn("SEASIDE_EARLY_BACKGROUND_DEFERRED", "SeasideMenu"); }
    }
    static void BeforeCamera(Camera camera)
    {
        if (!MenuSceneAvailable(earlyOwner))
        {
            if (view != null || loading != null) ReleaseMenuPresentation();
            return;
        }
        if (camera == null || camera != Camera.main) return;
        try
        {
            // An already interactive home is driven by LateUpdate. Only cover pending transitions here.
            if (view == null || !view.Visible || view.OptionsBlocking || (loading != null && loading.Visible)) Present(earlyOwner);
            loading?.RecordFirstRender(camera);
            view?.BeforeFriendsRender(camera);
        }
        catch { FailSafe(); }
    }
    static void Present(MainMenuManager owner)
    {
        if (owner == null) return;
        if (!MenuSceneAvailable(owner))
        {
            if (view != null || loading != null) ReleaseMenuPresentation();
            return;
        }
        TickOptions(); // Options animate while their native modal owns input above the retained home.
        if (presentationFailed || PopupOpen())
        {
            loading?.Hide(); if (view != null && view.Owner == owner) view.Hide(); return;
        }
        if (view != null && view.Owner == owner && owner.finishStartup)
        {
            // Restore only the temporary progress ordering BEFORE HomeView takes its normal snapshot.
            // These synchronous operations do not yield a frame with the old menu visible.
            loading?.Hide(); view.Tick(true); return;
        }
        if (!navigationAway && loading != null && loading.Owner == owner)
        {
            view?.Hide(); loading.Tick();
        }
        else loading?.Hide();
    }
    static void StopLoading()
    {
        try { loading?.Dispose(); } finally { loading = null; earlyOwner = null; }
    }
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.ActivateMainMenuUI)), HarmonyPostfix]
    static void ActivateHome(MainMenuManager __instance) { navigationAway = false; Present(__instance); }

    // Mouse input is absorbed by a transparent full-viewport clone, while controller activation of
    // the original home buttons is rejected. No original collider/renderer/prototype is mutated.
    [HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.ReceiveClickUp))]
    [HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.ReceiveClickDown))]
    [HarmonyPrefix]
    static bool StartupClick(PassiveButton __instance)
    {
        view?.TraceMenuInput("click", __instance, null);
        return !OptionsCloseBlocks(__instance) && (loading == null || !loading.Blocks(__instance)) && !(view?.BlocksTransition(__instance) ?? false);
    }

    static bool StartupPopupOpen()
    {
        startupPopups.RemoveAll(p => p == null);
        return startupPopups.Any(p => p.gameObject.activeInHierarchy);
    }
    static void TrackStartupPopup(Component popup)
    {
        if (!OwnsMenuUi(popup)) return;
        if (!startupPopups.Contains(popup)) startupPopups.Add(popup);
        loading?.Hide(); view?.Hide();
    }
    [HarmonyPatch(typeof(GenericPopup), nameof(GenericPopup.Show)), HarmonyPrefix]
    static void GenericOpening(GenericPopup __instance) => TrackStartupPopup(__instance);
    [HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.Show))]
    [HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.DoShow))]
    [HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.ShowCustom))]
    [HarmonyPrefix]
    static void DisconnectOpening(DisconnectPopup __instance) => TrackStartupPopup(__instance);
    [HarmonyPatch(typeof(AdDataCollectScreen), nameof(AdDataCollectScreen.Show))]
    [HarmonyPatch(typeof(AdDataCollectScreen), nameof(AdDataCollectScreen.ForceShow))]
    [HarmonyPrefix]
    static void ConsentOpening(AdDataCollectScreen __instance) => TrackStartupPopup(__instance);
    [HarmonyPatch(typeof(GenericPopup), nameof(GenericPopup.Close))]
    [HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.Close))]
    [HarmonyPatch(typeof(AdDataCollectScreen), nameof(AdDataCollectScreen.Close))]
    [HarmonyPostfix]
    static void StartupPopupClosed() { Present(earlyOwner); }

    [HarmonyPatch(typeof(LoadingBarManager), nameof(LoadingBarManager.ToggleLoadingBar)), HarmonyPostfix]
    static void LoadingBarChanged() { if (loading != null && loading.Visible) loading.LiftProgress(); }

    internal sealed class LoadingView : IDisposable
    {
        internal readonly MainMenuManager Owner;
        readonly GameObject root;
        readonly SpriteRenderer image, matte;
        readonly Sprite artwork;
        readonly bool ownsArtwork;
        readonly PassiveButton shield;
        readonly BoxCollider2D shieldBox;
        readonly TextMeshPro status;
        readonly HashSet<PassiveButton> homeButtons = new();
        readonly Dictionary<Renderer,int> lifted = new();
        bool rendered, disposed;
        internal bool Visible => root != null && root.activeSelf;
        internal LoadingView(MainMenuManager owner)
        {
            Owner = owner;
            if (owner.quitButton == null) throw new InvalidOperationException("Menu prefab not ready");
            var layers = SeasideMenuLayers.TryLoad();
            artwork = layers?.Background;
            if (artwork == null) artwork = HomeView.LoadArtwork(out ownsArtwork);
            if (artwork == null) throw new InvalidOperationException("Missing loading artwork");
            root = new GameObject("ShellSeasideLoading"); root.SetActive(false);
            root.layer = owner.quitButton.gameObject.layer; root.transform.SetParent(owner.transform,false);
            try
            {
                matte = HomeView.SpriteObject("LoadingLetterbox",root.transform,HomeView.SolidSprite(),Color.black,200);
                image = HomeView.SpriteObject("LoadingArtwork",root.transform,artwork,Color.white,201);
                if (layers != null)
                    foreach (var layer in layers.Buttons.Values)
                    {
                        var button = HomeView.SpriteObject("LoadingButton_" + layer.Id, root.transform, layer.Sprite, Color.white, 202);
                        button.transform.localPosition = HomeView.At(layer.ImageRect.CenterX, layer.ImageRect.CenterY, -.05f);
                    }
                // Clone the complete vanilla button rather than relying on SimpleButton's later Start-time base.
                shield = Object.Instantiate(owner.quitButton,root.transform); shield.name = "SeasideLoadingInputShield";
                shield.transform.localPosition = new(0,0,-.2f); shield.transform.localScale = Vector3.one;
                foreach (var anchor in shield.GetComponentsInChildren<AspectPosition>(true)) { anchor.enabled = false; Object.Destroy(anchor); }
                foreach (var r in shield.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                foreach (var c in shield.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
                shieldBox = shield.gameObject.AddComponent<BoxCollider2D>();
                shield.OnClick = new(); shield.OnMouseOver = new(); shield.OnMouseOut = new();
                shield.Colliders = new Collider2D[] {shieldBox}; shield.ClickMask = null;
                shield.gameObject.SetActive(true);
                var font = owner.quitButton.GetComponentInChildren<TextMeshPro>(true);
                if (font != null)
                {
                    var panel = HomeView.SpriteObject("LoadingStatusPanel",root.transform,HomeView.SolidSprite(),new(.1f,.08f,.2f,.86f),203);
                    panel.transform.localPosition = HomeView.At(1100,565,-.12f); panel.transform.localScale = new(9,1,1);
                    status = Object.Instantiate(font,root.transform); status.name = "SeasideLoadingStatus"; status.DestroyTranslator();
                    var anchor = status.GetComponent<AspectPosition>(); if (anchor != null) { anchor.enabled=false; Object.Destroy(anchor); }
                    status.transform.localPosition = HomeView.At(1100,565,-.15f); status.transform.localScale = Vector3.one;
                    status.rectTransform.sizeDelta = new(8.8f,.9f); status.alignment=TextAlignmentOptions.Center; status.color=Color.white;
                    status.fontSize=status.fontSizeMax=2.6f; status.fontSizeMin=1.5f; status.enableAutoSizing=true; status.enableWordWrapping=true;
                    var renderer=status.GetComponent<MeshRenderer>(); renderer.enabled=true; renderer.sortingOrder=204;
                    status.text="起動準備中…"; status.gameObject.SetActive(true);
                }
                RefreshButtons();
            }
            catch { Object.Destroy(root); ReleaseArtwork(); throw; }
        }
        void RefreshButtons()
        {
            foreach (var b in new[]{Owner.playButton,Owner.inventoryButton,Owner.shopButton,Owner.newsButton,
                Owner.myAccountButton,Owner.settingsButton,Owner.creditsButton,Owner.quitButton}) if(b!=null)homeButtons.Add(b);
            if (CredentialsPatch.TOHhmLogo != null)
                foreach(var b in CredentialsPatch.TOHhmLogo.GetComponentsInChildren<PassiveButton>(true)) homeButtons.Add(b);
            var friends=Object.FindObjectOfType<FriendsListButton>();
            if (friends != null && friends.Button != null)
                foreach(var b in friends.Button.GetComponentsInChildren<PassiveButton>(true))homeButtons.Add(b);
        }
        internal bool Blocks(PassiveButton button) => Visible && (button==shield || homeButtons.Contains(button));
        internal void Tick()
        {
            var cam=Camera.main;
            if (disposed || !MenuSceneAvailable(Owner) || !Owner.gameObject.activeInHierarchy || cam==null || !cam.orthographic) {Hide();return;}
            FitSurface(Owner,root,image.transform,matte,cam);
            var scale=root.transform.localScale.x;
            shieldBox.size=new(cam.orthographicSize*2*cam.aspect/scale,cam.orthographicSize*2/scale);
            shieldBox.offset=new(0,0);
            RefreshButtons();
            if(!Visible) {root.SetActive(true);Logger.Info("SEASIDE_LOADING_VISIBLE frame="+Time.frameCount+";startup="+Owner.finishStartup,"SeasideMenu");}
            LiftProgress();
        }
        internal void RecordFirstRender(Camera camera)
        {
            if(rendered || !Visible)return;
            rendered=true;
            Logger.Info("SEASIDE_FIRST_MENU_CAMERA frame="+Time.frameCount+";startup="+Owner.finishStartup+";asset="+artwork.texture.width+"x"+artwork.texture.height,"SeasideMenu");
        }
        internal void Lift(Renderer renderer)
        {
            if(renderer==null || renderer.transform.IsChildOf(root.transform))return;
            if(!lifted.ContainsKey(renderer))lifted[renderer]=renderer.sortingOrder;
            renderer.sortingOrder=Math.Max(renderer.sortingOrder,210);
        }
        internal void LiftProgress()
        {
            var bar=Object.FindObjectOfType<LoadingBarManager>();
            if(bar?.loadingBar==null)return;
            foreach(var renderer in bar.loadingBar.GetComponentsInChildren<Renderer>())Lift(renderer);
        }
        internal void Hide()
        {
            if(root!=null)root.SetActive(false);
            if (MenuSceneAvailable(Owner))
                foreach(var pair in lifted)if(pair.Key!=null)pair.Key.sortingOrder=pair.Value;
            lifted.Clear();
        }
        void ReleaseArtwork()
        {
            if(ownsArtwork && artwork!=null){Object.Destroy(artwork.texture);Object.Destroy(artwork);}
        }
        public void Dispose(){if(disposed)return;disposed=true;Hide();if(root!=null)Object.Destroy(root);ReleaseArtwork();}
    }
}
