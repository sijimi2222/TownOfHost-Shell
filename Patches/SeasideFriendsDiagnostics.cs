using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TownOfHost;

internal static partial class SeasideMainMenuPatch
{
    static void AfterFriendsCamera(Camera camera)
    {
        try { if (camera != null && camera == Camera.main && MenuSceneAvailable(view?.Owner)) view?.AfterFriendsRender(); }
        catch { /* Read-only post-render diagnostics must never interrupt the game render loop. */ }
    }
    internal sealed partial class HomeView
    {
        FriendsRenderAudit friendsAudit;
        int friendsAuditSessions, friendsAuditRenderFrame = -1;
        float friendsAuditElapsed;
        internal void BeginFriendsAudit()
        {
            if (disposed || !Visible || FriendsBlocking || !MenuSceneAvailable(Owner)) return;
            friendsAuditElapsed = 0; friendsAuditRenderFrame = -1;
            friendsAudit = ++friendsAuditSessions <= 2 ? new FriendsRenderAudit(this, friendsAuditSessions) : null;
            CaptureFriendsAudit("before_open", FriendsListUI.Instance);
        }
        void CaptureFriendsAudit(string phase, FriendsListUI ui) => friendsAudit?.Capture(phase, ui);
        internal void BeforeFriendsRender(Camera camera)
        {
            if (camera != Camera.main || disposed || !Visible || !MenuSceneAvailable(Owner) || friendsPanel == null) return;
            CaptureFriendsAudit("before_cull", friendsPanel.Ui);
            // Same known backdrop only. Do not guess/hide other full-screen renderers.
            friendsPanel.SuppressKnownBackdrop();
            CaptureFriendsAudit("cull_guard", friendsPanel.Ui);
        }
        internal void AfterFriendsRender()
        {
            if (disposed || !Visible || friendsPanel == null || !MenuSceneAvailable(Owner)) return;
            if (friendsAudit == null) return;
            if (friendsAuditRenderFrame != Time.frameCount)
            {
                friendsAuditRenderFrame = Time.frameCount;
                float dt = Time.unscaledDeltaTime;
                if (!float.IsNaN(dt) && !float.IsInfinity(dt) && dt > 0) friendsAuditElapsed += Math.Min(dt, .1f);
            }
            if (friendsAuditElapsed >= 1) CaptureFriendsAudit("stable_render", friendsPanel.Ui);
            CaptureFriendsAudit(friendsPanel.Motion.Openness >= 1 ? "settled_render" : "after_render", friendsPanel.Ui);
            if (friendsPanel.NativeBackdrop != null && friendsPanel.NativeBackdrop.enabled)
                CaptureFriendsAudit("late_reenabled", friendsPanel.Ui);
        }
        sealed class FriendsRenderAudit
        {
            const int MaxLines = 128;
            readonly HomeView home;
            readonly HashSet<string> phases = new(StringComparer.Ordinal);
            readonly Dictionary<string, string> previous = new(StringComparer.Ordinal);
            int lines;
            internal FriendsRenderAudit(HomeView home, int session)
            {
                this.home = home;
                Log("FRIENDS_RENDER_AUDIT_V3 session=" + session);
            }
            static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
            static string V(Vector3 v) => F(v.x) + "," + F(v.y) + "," + F(v.z);
            static string C(Color c) => F(c.r) + "," + F(c.g) + "," + F(c.b) + "," + F(c.a);
            static string Label(Component c)
            {
                if (c == null) return "none";
                // Never print arbitrary GameObject names, TMP text, account/friend data or paths.
                return c.gameObject.name switch {
                    "Blur" or "Menu" or "ClickToClose" or "Background" or "Backdrop" or "Tint" or "ScreenTint"
                    or "ShellSeasideHome" or "Artwork" or "Art" or "Letterbox" or "baseColor"
                    or "FullScreen" or "DarkenBackground" or "BackgroundCloseBlocker" or "CloseBlocker" or "ClickBlocker"
                    or "IgnoreClicks" or "RightDarkener" or "LeftDarkener" or "BgBlocker" or "FriendsList"
                    or "FriendsListManager" or "PhoneUI" or "FriendsListMask" or "ScreenMask" or "Fill" => c.gameObject.name,
                    _ => "other"
                };
            }
            static string SafePath(Transform value)
            {
                var parts = new List<string>();
                for (int i = 0; value != null && i < 6; i++, value = value.parent) parts.Add(Label(value) + "#" + value.GetInstanceID());
                parts.Reverse(); return string.Join("/", parts);
            }
            static string MaterialState(Material m)
            {
                if (m == null) return "none";
                var shader = m.shader;
                string kind = shader == null ? "none" : shader.name switch {
                    "Sprites/Default" or "UI/Default" or "Custom/Blur" or "UI/Blur" or "Sprites/Blur"
                    or "Unlit/Transparent" or "TextMeshPro/Mobile/Distance Field" => shader.name,
                    _ => "other"
                };
                string result = "id=" + m.GetInstanceID() + ";shader=" + kind + ";shaderId=" + (shader == null ? 0 : shader.GetInstanceID());
                foreach (string key in new[] { "_Color", "_TintColor" })
                    if (m.HasProperty(key)) result += ";" + key + "=" + C(m.GetColor(key));
                foreach (string key in new[] { "_Alpha", "_Saturation", "_Intensity" })
                    if (m.HasProperty(key)) result += ";" + key + "=" + F(m.GetFloat(key));
                return result;
            }
            void Log(string value)
            {
                try
                {
                    if (lines++ < MaxLines) Logger.Info(value, "SeasideFriendsAudit");
                    else if (lines == MaxLines + 1) Logger.Info("AUDIT_CAPPED", "SeasideFriendsAudit");
                }
                catch { /* Logging failure must never alter native input or the menu. */ }
            }
            bool Scope(Component component)
            {
                if (component == null) return false;
                var scene = component.gameObject.scene;
                return scene.IsValid() && scene.isLoaded && (scene.handle == home.Owner.gameObject.scene.handle || scene.name == "DontDestroyOnLoad");
            }
            void Row(string phase, string key, string value)
            {
                if (previous.TryGetValue(key, out var old) && old == value) return;
                previous[key] = value; Log("STATE phase=" + phase + ";key=" + key + ";" + value);
            }
            internal void Capture(string phase, FriendsListUI ui)
            {
                if (!MenuSceneAvailable(home.Owner) || !phases.Add(phase)) return;
                try
                {
                    var camera = Camera.main;
                    var backdrop = ui == null ? null : ui.transform.Find("Blur")?.GetComponent<SpriteRenderer>();
                    Log("PHASE " + phase + ";frame=" + Time.frameCount + ";ui=" + (ui == null ? 0 : ui.GetInstanceID())
                        + ";typedBlur=" + (backdrop == null ? 0 : backdrop.GetInstanceID()) + ";blurEnabled=" + (backdrop != null && backdrop.enabled)
                        + ";uiOpen=" + (ui != null && ui.IsOpen) + ";uiActive=" + (ui != null && ui.gameObject.activeInHierarchy)
                        + ";blocked=" + home.FriendsBlocking + ";homeColliders=" + home.root.GetComponentsInChildren<Collider2D>(true).Count(c => c.enabled));
                    if (camera != null) Row(phase,"camera","ortho=" + camera.orthographic + ";size=" + F(camera.orthographicSize) + ";aspect=" + F(camera.aspect)
                        + ";background=" + C(camera.backgroundColor) + ";clear=" + (int)camera.clearFlags + ";mask=" + camera.cullingMask);
                    var renderers = new List<Renderer>();
                    if (home.Owner.screenTint != null) renderers.Add(home.Owner.screenTint);
                    if (home.art != null) renderers.Add(home.art.GetComponent<Renderer>());
                    if (home.matte != null) renderers.Add(home.matte);
                    if (ui != null) renderers.AddRange(ui.GetComponentsInChildren<Renderer>(true).OrderBy(r => r.transform.parent == ui.transform ? 0 : 1));
                    renderers.AddRange(Object.FindObjectsOfType<Renderer>());
                    var ids = new HashSet<int>(); int candidates = 0;
                    foreach (var r in renderers)
                    {
                        if (!Scope(r) || !ids.Add(r.GetInstanceID())) continue;
                        var bounds = r.bounds;
                        bool direct = ui != null && r.transform.parent == ui.transform;
                        bool tint = home.Owner.screenTint != null && r.GetInstanceID() == home.Owner.screenTint.GetInstanceID();
                        bool large = camera != null && bounds.size.x >= camera.orthographicSize * camera.aspect && bounds.size.y >= camera.orthographicSize;
                        if (!direct && !tint && !large) continue;
                        if (candidates++ >= 24) break;
                        var sprite = r.GetComponent<SpriteRenderer>();
                        bool nativeSprite = sprite != null && sprite.GetInstanceID() == r.GetInstanceID();
                        string area = ui != null && r.transform.IsChildOf(ui.transform) ? "friends" : r.transform.IsChildOf(home.root.transform) ? "seaside" : "menu";
                        Row(phase,"R" + r.GetInstanceID(),"area=" + area + ";node=" + Label(r) + ";path=" + SafePath(r.transform) + ";parent=" + (r.transform.parent == null ? 0 : r.transform.parent.GetInstanceID())
                            + ";clrSprite=" + (r is SpriteRenderer) + ";nativeSprite=" + nativeSprite + ";enabled=" + r.enabled + ";active=" + r.gameObject.activeInHierarchy
                            + ";self=" + r.gameObject.activeSelf + ";order=" + r.sortingOrder + ";layer=" + r.sortingLayerID + ";objectLayer=" + r.gameObject.layer + ";pos=" + V(r.transform.position) + ";size=" + V(bounds.size)
                            + ";spriteColor=" + (nativeSprite ? C(sprite.color) : "none") + ";mat=" + MaterialState(r.sharedMaterial));
                    }
                    foreach (var other in Object.FindObjectsOfType<Camera>().Where(Scope).Take(4))
                        Row(phase,"K" + other.GetInstanceID(),"enabled=" + other.enabled + ";depth=" + F(other.depth)
                            + ";mask=" + other.cullingMask + ";clear=" + (int)other.clearFlags + ";background=" + C(other.backgroundColor));
                    foreach (var group in Object.FindObjectsOfType<CanvasGroup>().Where(Scope).Take(8))
                        Row(phase,"G" + group.GetInstanceID(),"path=" + SafePath(group.transform) + ";alpha=" + F(group.alpha) + ";ignoreParents=" + group.ignoreParentGroups + ";interact=" + group.interactable + ";raycast=" + group.blocksRaycasts);
                    foreach (var canvas in Object.FindObjectsOfType<Canvas>().Where(Scope).Take(8))
                        Row(phase,"C" + canvas.GetInstanceID(),"path=" + SafePath(canvas.transform) + ";enabled=" + canvas.enabled + ";mode=" + (int)canvas.renderMode + ";order=" + canvas.sortingOrder + ";layer=" + canvas.sortingLayerID);
                    foreach (var image in Object.FindObjectsOfType<Image>().Where(Scope).Take(8))
                        Row(phase,"I" + image.GetInstanceID(),"node=" + Label(image) + ";path=" + SafePath(image.transform) + ";enabled=" + image.enabled + ";color=" + C(image.color)
                            + ";raycast=" + image.raycastTarget + ";canvasColor=" + (image.canvasRenderer == null ? "none" : C(image.canvasRenderer.GetColor()))
                            + ";mat=" + MaterialState(image.material));
                }
                catch { Log("AUDIT_READ_FAILED phase=" + phase); } // Diagnostic failures never change the menu/fallback state.
            }
        }
    }
}
