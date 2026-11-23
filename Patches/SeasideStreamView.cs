using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using TownOfHost.Modules.MainMenu;
using TownOfHost.Templates;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfHost;

// Presentation only: keep the existing Stream buttons, TextBoxTMPs and their callbacks.
[HarmonyPatch]
internal sealed class SeasideStreamView : IDisposable
{
    static SeasideStreamView current;
    readonly MainMenuManager owner;
    readonly GameObject root;
    readonly SpriteRenderer background, matte;
    readonly Sprite artwork;
    readonly bool ownsArtwork;
    readonly TextMeshPro version;
    readonly List<Action> restore = new();
    readonly List<Mesh> meshes = new();
    readonly List<Sprite> viewSprites = new();
    readonly List<GameObject> buttonPlates = new();
    readonly Dictionary<Renderer, bool> hidden = new();
    readonly Dictionary<Collider2D, bool> blocked = new();
    readonly Material material;
    readonly List<PassiveButton> buttons = new();
    ControllerUiElementsState navigation;
    Il2CppSystem.Collections.Generic.List<UiElement> previousNavigation, installedNavigation;
    UiElement previousSelection;
    bool disposed;
    static readonly Color Ink = new Color32(30, 24, 45, 255);
    static readonly Color Cream = new Color32(251, 241, 221, 255);
    internal static bool Visible => current?.root != null && current.root.activeSelf;

    internal static void Show(MainMenuManager owner, (string id, SimpleButton button)[] controls,
        (string id, TextMeshPro text)[] labels)
    {
        Close();
        try { current = new(owner, controls, labels); }
        catch { Close(); Logger.Warn("SEASIDE_STREAM_NATIVE_FALLBACK", "SeasideMenu"); }
    }
    internal static void Close() { var old = current; current = null; old?.Dispose(); }

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
    static void Tick(MainMenuManager __instance)
    {
        if (current == null || current.owner != __instance) return;
        if (PlayerControl.LocalPlayer != null || HudManager.InstanceExists) { Close(); return; }
        if (Input.GetKeyDown(KeyCode.Escape)) { current.owner.ResetScreen(); return; }
        try { current.Fit(); }
        catch { Close(); Logger.Warn("SEASIDE_STREAM_NATIVE_FALLBACK", "SeasideMenu"); }
    }
    SeasideStreamView(MainMenuManager owner, (string id, SimpleButton button)[] controls,
        (string id, TextMeshPro text)[] labels)
    {
        this.owner = owner;
        artwork = SeasideMenuLayers.TryLoad()?.Background;
        if (artwork == null) artwork = SeasideMainMenuPatch.HomeView.LoadArtwork(out ownsArtwork);
        if (artwork == null) throw new InvalidOperationException("Missing Shell background");
        root = new GameObject("ShellSeasideStream"); root.layer = owner.quitButton.gameObject.layer;
        root.transform.SetParent(owner.transform, false); root.SetActive(false);
        try
        {
            material = new Material(Shader.Find("Sprites/Default"));
            matte = SeasideMainMenuPatch.HomeView.SpriteObject("StreamLetterbox", root.transform, SeasideMainMenuPatch.HomeView.SolidSprite(), Color.black, 0);
            background = SeasideMainMenuPatch.HomeView.SpriteObject("StreamBackground", root.transform, artwork, Color.white, 1);
            AddLogo();
            Shape("StreamPanelOutline", SeasideStreamLayout.Panel, Ink, 2);
            var p = SeasideStreamLayout.Panel;
            Shape("StreamPanelBorder", new(p.X+5,p.Y+5,p.Width-10,p.Height-10), Cream, 3);
            Shape("StreamPanel", new(p.X+13,p.Y+13,p.Width-26,p.Height-26), new Color32(42,32,79,245), 4);
            Shape("StreamHeadingPlate", SeasideStreamLayout.Title, Cream, 4);
            Label("StreamHeading", SeasideStreamLayout.Title, "配信・YouTube", 4.1f, Ink);
            version = Label("StreamShellVersion", SeasideMenuLayout.VersionBox, "", 3.1f, Ink);
            Label("StreamNotifications", SeasideMenuLayout.FriendsBox, "配信設定", 2.3f, Color.white);
            foreach (var c in controls) AddButton(c.id, c.button);
            AddInput("Join", CreateStreameMenu.JoinWord);
            AddInput("Remove", CreateStreameMenu.RemoveWord);
            foreach (var l in labels) AddLabel(l.id, l.text);
            var back = new SimpleButton(root.transform, "StreamBack", Vector3.zero, Color.clear, Color.clear,
                () => owner.ResetScreen(), "戻る");
            AddButton("Back", back, false);
            Label("StreamVanillaVersion", new(38, 910, 650, 25), Application.version, 1.7f, Color.white).alignment = TextAlignmentOptions.Left;
            // Capture only currently active legacy controls. Later native modals retain their own input.
            foreach (var r in owner.GetComponentsInChildren<Renderer>())
                if (!r.transform.IsChildOf(root.transform)) { hidden[r] = r.enabled; r.enabled = false; }
            foreach (var b in Object.FindObjectsOfType<PassiveButton>())
                if (!b.transform.IsChildOf(root.transform)) foreach (var c in b.GetComponentsInChildren<Collider2D>())
                    if (!blocked.ContainsKey(c)) { blocked[c] = c.enabled; c.enabled = false; }
            root.SetActive(true); Fit(); BindNavigation();
            Logger.Info("SEASIDE_STREAM_READY existingCallbacks=True", "SeasideMenu");
        }
        catch { Dispose(); throw; }
    }
    void Move(Transform t, SeasideRect rect)
    {
        var parent = t.parent; var pos = t.localPosition; var scale = t.localScale; var rotation = t.localRotation;
        restore.Add(() => { if (t != null) { t.SetParent(parent, false); t.localPosition = pos; t.localScale = scale; t.localRotation = rotation; } });
        foreach (var a in t.GetComponentsInChildren<AspectPosition>(true))
        { bool enabled = a.enabled; restore.Add(() => { if (a != null) a.enabled = enabled; }); a.enabled = false; }
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        { int order = r.sortingOrder; restore.Add(() => { if (r != null) r.sortingOrder = order; }); r.sortingOrder = 10; }
        foreach (var s in t.GetComponentsInChildren<SpriteRenderer>(true))
        { var mask = s.maskInteraction; restore.Add(() => { if (s != null) s.maskInteraction = mask; }); s.maskInteraction = SpriteMaskInteraction.None; }
        foreach (var b in t.GetComponentsInChildren<PassiveUiElement>(true))
        { var mask = b.ClickMask; restore.Add(() => { if (b != null) b.ClickMask = mask; }); b.ClickMask = null; }
        t.SetParent(root.transform, false); t.localPosition = SeasideMainMenuPatch.HomeView.At(rect.CenterX, rect.CenterY, -.2f);
        t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
    }
    void AddButton(string id, SimpleButton simple, bool borrowed = true)
    {
        if (SimpleButton.IsNullOrDestroyed(simple)) throw new InvalidOperationException("Missing stream control");
        var rect = SeasideStreamLayout.Rect(id); var b = simple.Button;
        if (borrowed) Move(b.transform, rect);
        else b.transform.localPosition = SeasideMainMenuPatch.HomeView.At(rect.CenterX, rect.CenterY, -.2f);
        var oldScale = simple.Scale; var oldFont = simple.FontSize; var oldColor = simple.Label.color;
        restore.Add(() => { if (b != null) { simple.Scale = oldScale; simple.FontSize = oldFont; simple.Label.color = oldColor; } });
        simple.Scale = new((float)rect.Width/100, (float)rect.Height/100); simple.FontSize = id == "Back" ? 3.3f : 2.3f;
        simple.Label.color = Ink;
        // TMP creates fallback-font submeshes lazily. Generate them before setting
        // their order so Japanese glyphs cannot remain behind the new button plate.
        simple.Label.ForceMeshUpdate(true, true);
        foreach(var r in simple.Label.GetComponentsInChildren<Renderer>(true))
        { int order=r.sortingOrder;restore.Add(()=>{if(r!=null)r.sortingOrder=order;});r.sortingOrder=10; }
        foreach (var r in b.GetComponentsInChildren<SpriteRenderer>(true))
        { bool enabled = r.enabled; restore.Add(() => { if (r != null) r.enabled = enabled; }); r.enabled = false; }
        var oldOver = b.OnMouseOver; var oldOut = b.OnMouseOut;
        restore.Add(() => { if (b != null) { b.OnMouseOver = oldOver; b.OnMouseOut = oldOut; } });
        var face = Shape("StreamButton_"+id, rect, Cream, 8);
        var focus = Shape("StreamButtonFocus_"+id, rect, new Color(1,.75f,1,.25f), 9);
        focus.enabled = false;
        b.OnMouseOver = new(); b.OnMouseOut = new();
        b.OnMouseOver.AddListener((Action)(() => { if (focus != null) focus.enabled = true; }));
        b.OnMouseOut.AddListener((Action)(() => { if (focus != null) focus.enabled = false; }));
        // This renderer follows the existing button: hiding a control also removes its new plate.
        face.transform.SetParent(b.transform, true);
        focus.transform.SetParent(b.transform, true);
        buttonPlates.Add(face.gameObject); buttonPlates.Add(focus.gameObject);
        buttons.Add(b);
    }
    void AddInput(string id, TextBoxTMP box)
    {
        if (box == null) throw new InvalidOperationException("Missing stream input");
        var rect = SeasideStreamLayout.Rect(id); var collider = box.GetComponentInChildren<BoxCollider2D>(true);
        float w = collider != null ? Math.Abs(collider.size.x * collider.transform.lossyScale.x / box.transform.lossyScale.x) : 4;
        float h = collider != null ? Math.Abs(collider.size.y * collider.transform.lossyScale.y / box.transform.lossyScale.y) : .5f;
        Move(box.transform, rect);
        float k = Math.Min((float)rect.Width/100/Math.Max(.01f,w), (float)rect.Height/100/Math.Max(.01f,h));
        box.transform.localScale = Vector3.one * k;
    }
    void AddLabel(string id, TextMeshPro text)
    {
        if (text == null) throw new InvalidOperationException("Missing stream label");
        var rect = SeasideStreamLayout.Rect(id);
        var size = text.rectTransform.sizeDelta; var color = text.color; var align = text.alignment;
        float font = text.fontSize, min = text.fontSizeMin, max = text.fontSizeMax;
        bool auto = text.enableAutoSizing, wrap = text.enableWordWrapping;
        restore.Add(() => { if (text != null) { text.rectTransform.sizeDelta=size; text.color=color; text.alignment=align; text.fontSize=font; text.fontSizeMin=min; text.fontSizeMax=max; text.enableAutoSizing=auto; text.enableWordWrapping=wrap; } });
        Move(text.transform, rect);
        text.rectTransform.sizeDelta = new((float)rect.Width/100, (float)rect.Height/100);
        text.color=Color.white; text.fontSize=text.fontSizeMax=id=="Notice"?1.35f:2.6f; text.fontSizeMin=id=="Notice"?1.0f:1.5f;
        text.enableAutoSizing=true; text.enableWordWrapping=id=="Notice"; text.alignment=id=="Notice"?TextAlignmentOptions.Center:TextAlignmentOptions.Left;
    }
    MeshRenderer Shape(string name, SeasideRect r, Color color, int order)
    {
        float x=(float)r.Width/200, y=(float)r.Height/200, c=Math.Min(.25f,y*.55f);
        var mesh = new Mesh { vertices=new[]{new Vector3(-x+c,y,0),new Vector3(x-c,y,0),new Vector3(x,y-c,0),new Vector3(x,-y+c,0),new Vector3(x-c,-y,0),new Vector3(-x+c,-y,0),new Vector3(-x,-y+c,0),new Vector3(-x,y-c,0)},
            triangles=new[]{0,1,2,0,2,3,0,3,4,0,4,5,0,5,6,0,6,7}, colors=Enumerable.Repeat(color,8).ToArray() };
        mesh.RecalculateBounds(); meshes.Add(mesh);
        var go=new GameObject(name); go.layer=root.layer; go.transform.SetParent(root.transform,false);
        go.transform.localPosition=SeasideMainMenuPatch.HomeView.At(r.CenterX,r.CenterY,-.02f-order*.002f);
        go.AddComponent<MeshFilter>().mesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>(); renderer.material=material; renderer.sortingOrder=order; return renderer;
    }
    TextMeshPro Label(string name, SeasideRect r, string value, float font, Color color)
    {
        var t=Object.Instantiate(owner.quitButton.GetComponentInChildren<TextMeshPro>(true),root.transform); t.name=name; t.DestroyTranslator();
        foreach(var a in t.GetComponentsInChildren<AspectPosition>(true))a.enabled=false;
        t.transform.localPosition=SeasideMainMenuPatch.HomeView.At(r.CenterX,r.CenterY,-.15f);t.transform.localScale=Vector3.one;
        t.rectTransform.sizeDelta=new((float)r.Width/100,(float)r.Height/100);t.fontSize=t.fontSizeMax=font;t.fontSizeMin=font*.65f;
        t.enableAutoSizing=true;t.enableWordWrapping=false;t.fontStyle=FontStyles.Normal;t.color=color;t.alignment=TextAlignmentOptions.Center;t.text=value;
        t.gameObject.SetActive(true);t.ForceMeshUpdate(true,true);
        foreach(var renderer in t.GetComponentsInChildren<Renderer>(true)){renderer.sortingOrder=11;renderer.enabled=true;}
        return t;
    }
    void AddLogo()
    {
        // Reuse the current artist-provided play-page logo, including its matching
        // background. The plain layered home backdrop contains no corner logo.
        var page=UtilsSprite.LoadSprite("TownOfHost.Resources.MainMenu.Play.page.png",100);
        if(page==null||page.texture.width!=1672||page.texture.height!=941)return;
        var r=SeasideStreamLayout.Logo;
        var sprite=Sprite.Create(page.texture,new Rect((float)r.X,(float)(941-r.Y-r.Height),(float)r.Width,(float)r.Height),new(.5f,.5f),100);
        viewSprites.Add(sprite);
        var image=SeasideMainMenuPatch.HomeView.SpriteObject("StreamShellLogo",root.transform,sprite,Color.white,2);
        image.transform.localPosition=SeasideMainMenuPatch.HomeView.At(r.CenterX,r.CenterY,-.025f);
    }
    void Fit()
    {
        var camera=Camera.main;if(camera==null||!camera.orthographic)return;
        SeasideMainMenuPatch.FitSurface(owner,root,background.transform,matte,camera);
        version.text="TOH-Shell v"+Main.PluginShowVersion;
        foreach(var pair in hidden)if(pair.Key!=null)pair.Key.enabled=false;
        foreach(var pair in blocked)if(pair.Key!=null)pair.Key.enabled=false;
    }
    void BindNavigation()
    {
        navigation=ControllerManager.Instance?.CurrentUiState;if(navigation==null)return;
        previousNavigation=navigation.SelectableUiElements;previousSelection=navigation.CurrentSelection;
        installedNavigation=new();foreach(var b in buttons)installedNavigation.Add(b);
        foreach(var input in new[]{CreateStreameMenu.JoinWord,CreateStreameMenu.RemoveWord})
        {
            var selectable=input.GetComponent<PassiveButton>();
            if(selectable!=null)installedNavigation.Add(selectable);
        }
        navigation.SelectableUiElements=installedNavigation;
        if(buttons.Count>0)ControllerManager.Instance.SetCurrentSelected(buttons[0]);
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        foreach(var plate in buttonPlates)if(plate!=null){plate.SetActive(false);Object.Destroy(plate);}
        if(navigation?.SelectableUiElements!=null&&installedNavigation!=null&&navigation.SelectableUiElements.Pointer==installedNavigation.Pointer)
        {
            navigation.SelectableUiElements=previousNavigation;
            if(ControllerManager.Instance?.CurrentUiState?.Pointer==navigation.Pointer&&previousSelection!=null)ControllerManager.Instance.SetCurrentSelected(previousSelection);
        }
        for(int i=restore.Count-1;i>=0;i--)try{restore[i]();}catch{/* Scene teardown: never touch another view's ownership. */}
        foreach(var pair in hidden)if(pair.Key!=null)pair.Key.enabled=pair.Value;
        foreach(var pair in blocked)if(pair.Key!=null)pair.Key.enabled=pair.Value;
        if(root!=null){root.SetActive(false);Object.Destroy(root);}
        foreach(var mesh in meshes)Object.Destroy(mesh);
        foreach(var sprite in viewSprites)Object.Destroy(sprite);
        if(material!=null)Object.Destroy(material);
        if(ownsArtwork&&artwork!=null){Object.Destroy(artwork.texture);Object.Destroy(artwork);}
    }
}
