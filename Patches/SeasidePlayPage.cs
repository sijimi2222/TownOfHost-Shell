using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using TownOfHost.Modules.MainMenu;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfHost;

internal static partial class SeasideMainMenuPatch
{
    static bool skipNativeReset;
    static MainMenuManager resumeNativeOwner;
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.ResetScreen)), HarmonyPrefix]
    static bool BeforeReset(MainMenuManager __instance)
    {
        skipNativeReset = view != null && view.Owner == __instance && view.ConsumeBack();
        return !skipNativeReset;
    }
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenGameModeMenu)), HarmonyPrefix]
    static bool NativePlayPage(MainMenuManager __instance)
    {
        if (view != null && view.Owner == __instance && view.AwaitingNativePlay)
        {
            resumeNativeOwner = __instance; return false;
        }
        LeavingHome(); return true;
    }
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.OpenGameModeMenu)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
    static void NativePlayPageFinished(MainMenuManager __instance)
    {
        if (resumeNativeOwner != __instance) return;
        resumeNativeOwner = null;
        // The existing OpenMenuPostfix hides the Shell logo even when the original method was skipped.
        // Resume only after it has run, then ResetScreen restores the vanilla home lifecycle correctly.
        try { if (view != null && view.Owner == __instance) view.ResumeNativePlay(); }
        catch { FailSafe(); }
    }
    internal sealed partial class HomeView
    {
        readonly SeasideTransition transition = new();
        readonly List<PassiveButton> playButtons = new();
        readonly List<Mesh> pageMeshes = new();
        readonly List<Sprite> pageIllustrationSprites = new();
        Material pageMaterial, pageArtworkMaterial;
        GameObject playArtwork;
        SeasideWaveView wave;
        int pageFrame = -1;
        bool awaitingNativePlay;
        internal bool AwaitingNativePlay => awaitingNativePlay && !disposed;
        internal SeasidePage Page => transition.Page;
        internal bool Transitioning => transition.Active;
        internal bool BlocksTransition(PassiveButton button) => Visible && (transition.Active || BlocksFriendsInput(button) || BlocksOptionsInput(button));
        void CreatePlayPage()
        {
            if (Owner.PlayOnlineButton == null || Owner.playLocalButton == null)
                throw new InvalidOperationException("Missing native play adapters");
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Menu shader unavailable");
            pageMaterial = new Material(shader);
            var template = UtilsSprite.LoadSprite("TownOfHost.Resources.MainMenu.Play.page.png", 100);
            if (template == null || template.texture.width != 1672 || template.texture.height != 941)
                throw new InvalidOperationException("Missing artist play page");
            pageArtworkMaterial = new Material(shader) { mainTexture = template.texture };
            playArtwork = new GameObject("SeasidePlayArtwork"); playArtwork.layer = root.layer;
            playArtwork.transform.SetParent(root.transform, false);
            playArtwork.transform.localPosition = new(0,0,-.01f);
            SpriteObject("PlayPageBackground", playArtwork.transform, template, Color.white, 1);
            // Cover only the reference's baked version text with its own cream face colour.
            // Keep the artist's border/background intact and the existing dynamic text above it.
            var header = new SeasideRect(1235,29,350,46);
            var frame = SpriteObject("PlayDynamicVersionFrame",playArtwork.transform,SolidSprite(),new Color32(251,241,221,255),2);
            frame.transform.localPosition = At(header.CenterX,header.CenterY,0);
            frame.transform.localScale = new((float)header.Width/100,(float)header.Height/100,1);
            foreach (string id in SeasidePlayLayout.Ids)
            {
                var rect = SeasidePlayLayout.Rect(id);
                var button = NewButton(id, rect, () => RequestClick(id, false), false);
                var pivot = new GameObject("Motion_" + id); pivot.layer = root.layer;
                pivot.transform.SetParent(button.transform, false); pivot.transform.localPosition = new(0,0,-.02f);
                var focus = button.GetComponentsInChildren<SpriteRenderer>(true).Single(r => r.gameObject.name == "Focus");
                focus.transform.SetParent(pivot.transform, false);
                // Opaque, fixed black underlay covers the reference's baked title before the
                // textured title moves. It reads as the frame's shadow, never a second label.
                MakeArtistTitle(playArtwork.transform, id, true);
                MakeArtistTitle(pivot.transform, id, false);
                if (id != "PlayBack")
                {
                    string file = id == "PlayLocal" ? "local.png" : "online.png";
                    var sprite = UtilsSprite.LoadSprite("TownOfHost.Resources.MainMenu.Play." + file,100);
                    if (sprite != null)
                    {
                        var imageRect = SeasidePlayLayout.FitIllustration(id,sprite.texture.width,sprite.texture.height);
                        var crop = SeasidePlayLayout.CropIllustration(id,sprite.texture.width,sprite.texture.height);
                        var visibleSprite = Sprite.Create(sprite.texture,
                            new Rect((float)crop.X,(float)(sprite.texture.height-crop.Y-crop.Height),(float)crop.Width,(float)crop.Height),new(.5f,.5f),100);
                        pageIllustrationSprites.Add(visibleSprite);
                        // Bevel the displayed sprite's corners inside the existing white frame.
                        // This clips only the visual, not the button's fixed click polygon.
                        // OverrideGeometry accepts rect-local PIXELS; Unity applies pivot/PPU.
                        float w=(float)crop.Width, h=(float)crop.Height;
                        float cut=(float)(24*crop.Width/imageRect.Width);
                        visibleSprite.OverrideGeometry(new Vector2[] {
                            new(cut,h),new(w-cut,h),new(w,h-cut),new(w,cut),
                            new(w-cut,0),new(cut,0),new(0,cut),new(0,h-cut)
                        },new ushort[] {0,1,2,0,2,3,0,3,4,0,4,5,0,5,6,0,6,7});
                        var image = SpriteObject("PlayIllustration_"+id,pivot.transform,visibleSprite,Color.white,2);
                        image.transform.localPosition = new((float)(imageRect.CenterX-rect.CenterX)/100,
                            (float)(rect.CenterY-imageRect.CenterY)/100,-.01f);
                        image.transform.localScale = new((float)(imageRect.Width/crop.Width),
                            (float)(imageRect.Height/crop.Height),1);
                    }
                    else Logger.Warn("SEASIDE_PLAY_ILLUSTRATION_UNAVAILABLE " + id,"SeasideMenu");
                }
                motionBindings.Add(id,new() { Button=button, Pivot=pivot.transform, Focus=focus });
                playButtons.Add(button); button.gameObject.SetActive(false);
            }
            playArtwork.SetActive(false);
        }
        void MakeArtistTitle(Transform parent, string id, bool shadow)
        {
            var rect = SeasidePlayLayout.Title(id); var hit = SeasidePlayLayout.Rect(id);
            double cut = rect.Height*.38;
            var pixels = new[] { (rect.X,rect.CenterY), (rect.X+cut,rect.Y), (rect.X+rect.Width-cut,rect.Y),
                (rect.X+rect.Width,rect.CenterY), (rect.X+rect.Width-cut,rect.Y+rect.Height), (rect.X+cut,rect.Y+rect.Height) };
            var mesh = new Mesh {
                vertices = pixels.Select(p => shadow ? At(p.Item1,p.Item2,0) : new Vector3((float)(p.Item1-hit.CenterX)/100,(float)(hit.CenterY-p.Item2)/100,0)).ToArray(),
                uv = pixels.Select(p => new Vector2((float)(p.Item1/1672),(float)(1-p.Item2/941))).ToArray(),
                triangles = new[] {0,1,2,0,2,3,0,3,4,0,4,5},
                colors = Enumerable.Repeat(shadow ? new Color(.04f,.03f,.05f) : Color.white,6).ToArray()
            };
            mesh.RecalculateBounds(); pageMeshes.Add(mesh);
            var go = new GameObject((shadow ? "PlayTitleShadow_" : "PlayTitle_")+id); go.layer = root.layer;
            go.transform.SetParent(parent,false); go.transform.localPosition = new(0,0,shadow ? -.005f : -.015f);
            go.AddComponent<MeshFilter>().mesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>(); renderer.material=shadow ? pageMaterial : pageArtworkMaterial;
            renderer.sortingOrder=2;
        }
        void ApplyPage()
        {
            bool home = transition.Page == SeasidePage.Home;
            if (playArtwork != null) playArtwork.SetActive(!home);
            foreach (var target in SeasideMenuLayout.Targets)
                proxies.First(b => b.gameObject.name == "Seaside_" + target.Id).gameObject.SetActive(home);
            foreach (var button in playButtons) button.gameObject.SetActive(!home);
            foreach (var entry in updateActions) entry.proxy.gameObject.SetActive(home && entry.source != null && entry.source.gameObject.activeInHierarchy);
            if (navState != null) RefreshNavigation();
        }
        bool CanInvokePlay(string id)
        {
            if (transition.Page != SeasidePage.Play || !SeasidePlayLayout.Ids.Contains(id)) return false;
            var source = id == "PlayOnline" ? Owner.PlayOnlineButton : id == "PlayLocal" ? Owner.playLocalButton : null;
            // The vanilla mode-choice panel stays hidden, but its existing events remain the authority.
            return id == "PlayBack" || (source != null && source.enabled && source.OnClick != null);
        }
        void BeginPage(SeasidePage page)
        {
            if (!transition.Begin(page)) return;
            if (Main.DisableWaveAnimation?.Value == true) { CompleteWithoutWave(); return; }
            ResetButtonAnimations();
            wave ??= new(root.transform, Owner.quitButton.GetComponentInChildren<TextMeshPro>(true));
            if (navState != null) RefreshNavigation();
            DrawWave();
        }
        void CompleteWithoutWave()
        {
            transition.Restore(transition.To);
            wave?.Hide();
            ResetButtonAnimations(); ApplyPage(); rearm = .1;
            var current = ControllerManager.Instance?.CurrentUiState;
            if (navState == null || current == null || current.Pointer != navState.Pointer) return;
            var first = proxies.FirstOrDefault(b => b.gameObject.activeSelf);
            if (first != null) ControllerManager.Instance?.SetCurrentSelected(first);
        }
        void DrawWave()
        {
            var camera=Camera.main;
            if (camera == null) return;
            double h=camera.orthographicSize*2/root.transform.localScale.x;
            wave?.Draw(transition,h*camera.aspect,h);
        }
        void TickPage()
        {
            if (pageFrame == Time.frameCount) return;
            pageFrame=Time.frameCount;
            if (transition.Active)
            {
                var current = ControllerManager.Instance?.CurrentUiState;
                if (navState == null || current == null || current.Pointer != navState.Pointer)
                {
                    CancelTransition(); ResetButtonAnimations(); return;
                }
                if (Main.DisableWaveAnimation?.Value == true) { CompleteWithoutWave(); return; }
                var previous=transition.Page;
                bool completed=transition.Advance(Time.unscaledDeltaTime);
                if (previous != transition.Page) ApplyPage(); // Only reached under the opaque water core.
                DrawWave();
                if (completed)
                {
                    ResetButtonAnimations(); ApplyPage();
                    var first=proxies.FirstOrDefault(b=>b.gameObject.activeSelf);
                    if (first != null && navState != null) ControllerManager.Instance?.SetCurrentSelected(first);
                }
                return;
            }
            if (transition.Page == SeasidePage.Play && Input.GetKeyDown(KeyCode.Escape)) ConsumeBack();
        }
        internal bool ConsumeBack()
        {
            if (!Visible) return false;
            if (transition.Active) return true;
            if (transition.Page != SeasidePage.Play) return false;
            RequestClick("PlayBack",false); return true;
        }
        internal bool ResumeNativePlay()
        {
            if (!awaitingNativePlay || disposed) return false;
            Hide(); awaitingNativePlay=false;
            // Let the vanilla reset restore its own navigation state before presenting our choice page.
            Owner.ResetScreen(); navigationAway=false; transition.Restore(SeasidePage.Play); ApplyPage(); Present(Owner); return true;
        }
        void InvokePlay(string id)
        {
            if (id == "Play") { BeginPage(SeasidePage.Play); return; }
            if (id == "PlayBack") { BeginPage(SeasidePage.Home); return; }
            var native = id == "PlayOnline" ? Owner.PlayOnlineButton : Owner.playLocalButton;
            if (native == null || !native.enabled || native.OnClick == null) return;
            Hide(); navigationAway=true; awaitingNativePlay=true;
            try { native.OnClick.Invoke(); }
            catch
            {
                awaitingNativePlay=false; navigationAway=false; Owner.ResetScreen(); transition.Reset(); ApplyPage();
                if (!PopupOpen()) Tick(true);
                Logger.Warn("SEASIDE_NATIVE_PLAY_ACTION_FAILED", "SeasideMenu");
            }
        }
        void CancelTransition()
        {
            bool active=transition.Active; transition.Cancel(); wave?.Hide(); pageFrame=-1;
            if (active && root != null) ApplyPage();
        }
        void DisposePlayPage()
        {
            wave?.Dispose(); wave=null;
            foreach (var mesh in pageMeshes) if (mesh != null) Object.Destroy(mesh);
            foreach (var sprite in pageIllustrationSprites) if (sprite != null) Object.Destroy(sprite);
            pageIllustrationSprites.Clear();
            if (pageMaterial != null) Object.Destroy(pageMaterial);
            if (pageArtworkMaterial != null) Object.Destroy(pageArtworkMaterial);
        }
    }
}
