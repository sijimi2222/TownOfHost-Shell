using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using TMPro;
using TownOfHost.Modules.MainMenu;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TownOfHost;

internal static partial class SeasideMainMenuPatch
{
    static CodeEntryView codeEntryView;
    [HarmonyPatch(typeof(EnterCodeManager),nameof(EnterCodeManager.OnEnable)),HarmonyPostfix,HarmonyPriority(Priority.Last)]
    static void CodeEntryOpened(EnterCodeManager __instance)
    {
        var owner=view?.Owner;
        if(owner==null || !MenuSceneAvailable(owner) || !view.NativeOnlineActionPending || !__instance.transform.IsChildOf(owner.transform))return;
        CloseCodeEntry();
        try { codeEntryView=new(owner,__instance);codeEntryView.Tick(); }
        catch { CloseCodeEntry();Logger.Warn("SEASIDE_CODE_ENTRY_NATIVE_FALLBACK","SeasideMenu"); }
    }
    [HarmonyPatch(typeof(EnterCodeManager),nameof(EnterCodeManager.OnDisable)),HarmonyPrefix]
    static void CodeEntryClosing(EnterCodeManager __instance)
    { if(codeEntryView?.Native==__instance)CloseCodeEntry(); }
    static void CloseCodeEntry() { var old=codeEntryView;codeEntryView=null;old?.Dispose(); }
    static void TickCodeEntry()
    {
        if(codeEntryView==null)return;
        try { if(!codeEntryView.Tick())CloseCodeEntry(); }
        catch { CloseCodeEntry();Logger.Warn("SEASIDE_CODE_ENTRY_NATIVE_FALLBACK","SeasideMenu"); }
    }

    // A presentation lease, not a second code-entry implementation. Native TextBoxTMP,
    // buttons, field visibility, controller navigation and callbacks are kept intact.
    internal sealed class CodeEntryView : IDisposable
    {
        internal readonly EnterCodeManager Native;
        readonly MainMenuManager owner;
        readonly GameObject root;
        readonly SpriteRenderer background,matte;
        readonly TextMeshPro version,friendLabel,officialVersion;
        readonly VersionShower nativeVersion;
        readonly List<Placement> placements=new();
        readonly Dictionary<Renderer,bool> hidden=new();
        readonly Dictionary<Collider2D,bool> blocked=new();
        readonly Dictionary<Behaviour,bool> anchors=new();
        readonly Dictionary<Renderer,int> orders=new();
        readonly Dictionary<PassiveUiElement,Collider2D> clickMasks=new();
        readonly Dictionary<SpriteRenderer,SpriteMaskInteraction> spriteMasks=new();
        readonly List<Texture2D> textures=new();
        readonly List<Sprite> sprites=new();
        readonly Dictionary<string,Sprite> artCache=new(StringComparer.Ordinal);
        readonly HashSet<string> missingArt=new(StringComparer.Ordinal);
        readonly List<(SpriteRenderer image,PassiveButton source)> buttonImages=new();
        readonly Dictionary<SpriteRenderer,(Vector3 position,int order)> stamps=new();
        long imageBytes,imagePixels;
        readonly List<Transform> keep=new();
        readonly FriendsListButton friends;
        readonly Sprite artwork;
        readonly bool ownsArtwork;
        bool disposed;
        sealed class Placement
        {
            internal Transform Transform;
            internal Vector3 Position,Scale;
            internal Quaternion Rotation;
            internal SeasideRect Rect;
            internal string Id;
            internal float Width,Height;
            internal TextMeshPro Text;
            internal Vector2 Size;
            internal float Font,Min,Max;
            internal bool Auto,Wrap;
            internal TextAlignmentOptions Alignment;
        }
        internal bool Visible=>root!=null&&root.activeInHierarchy;
        internal CodeEntryView(MainMenuManager owner,EnterCodeManager native)
        {
            this.owner=owner;Native=native;
            if(native.enterCodeField==null||native.BackButton==null||native.joinGamePassiveButton==null
                ||native.hostText==null||native.capacityText==null||native.serverText==null||native.chatText==null
                ||EnterCodeManagerPatch.ServerList==null||EnterCodeManagerPatch.ServerListDrop==null
                ||EnterCodeManagerPatch.vServerOnlyCheckBox==null||EnterCodeManagerPatch.RestoreButton==null
                ||EnterCodeManagerPatch.DownloadButton==null||EnterCodeManagerPatch.UnloadAndJoinButton==null)
                throw new InvalidOperationException("Incomplete native code-entry adapters");
            artwork=SeasideMenuLayers.TryLoad()?.Background;
            if(artwork==null)artwork=HomeView.LoadArtwork(out ownsArtwork);
            if(artwork==null)throw new InvalidOperationException("Missing Shell background");
            root=new GameObject("ShellSeasideCodeEntry");root.layer=owner.quitButton.gameObject.layer;root.transform.SetParent(owner.transform,false);
            root.SetActive(false);
            try
            {
                matte=HomeView.SpriteObject("CodeEntryLetterbox",root.transform,HomeView.SolidSprite(),Color.black,0);
                background=HomeView.SpriteObject("CodeEntryBackground",root.transform,artwork,Color.white,1);
                Box("CodeEntryPanelBorder",SeasideCodeEntryLayout.Panel,new Color32(251,241,221,255),2);
                var p=SeasideCodeEntryLayout.Panel;
                Box("CodeEntryPanel",new(p.X+8,p.Y+8,p.Width-16,p.Height-16),new Color32(42,32,79,245),3);
                if(OptionalArt("panel",p,4,false)!=null){root.transform.Find("CodeEntryPanelBorder").GetComponent<SpriteRenderer>().enabled=false;root.transform.Find("CodeEntryPanel").GetComponent<SpriteRenderer>().enabled=false;}
                Box("CodeEntryTitle",SeasideCodeEntryLayout.Title,new Color32(251,241,221,255),4);
                Label("CodeEntryHeading",SeasideCodeEntryLayout.Title,"コードを入力",4.2f,new Color32(30,24,45,255));
                version=Label("CodeEntryVersion",SeasideMenuLayout.VersionBox,"",3.1f,new Color32(40,30,50,255));
                nativeVersion=Object.FindObjectOfType<VersionShower>();
                officialVersion=Label("CodeEntryGameVersion",SeasideMenuLayout.GameVersionBox,"",2.4f,Color.white);
                officialVersion.alignment=TextAlignmentOptions.Left;
                friends=Object.FindObjectOfType<FriendsListButton>();
                friendLabel=Label("CodeEntryNotifications",SeasideMenuLayout.FriendsBox,"",2.5f,Color.white);
                OptionalArt("illustration",SeasideCodeEntryLayout.Rect("Illustration"),4,true);
                string[] rows={"Host","Capacity","Region","Chat"};string[] labels={"ホスト","最大人数","地域","チャットタイプ"};
                for(int i=0;i<rows.Length;i++){
                    var r=SeasideCodeEntryLayout.Rect(rows[i]);
                    Label("CodeEntryLabel"+rows[i],new(565,r.Y,270,r.Height),labels[i],2.6f,Color.white);
                    Box("CodeEntryFieldBorder"+rows[i],r,new Color32(145,137,173,255),4);
                    Box("CodeEntryField"+rows[i],new(r.X+3,r.Y+3,r.Width-6,r.Height-6),new Color32(35,29,58,255),5);
                }
                Label("CodeEntryServerOnlyLabel",new(505,752,315,45),"選択中のサーバーのみ検索する",1.8f,Color.white);
                OptionalArt("input-frame",SeasideCodeEntryLayout.Rect("Code"),6,false);
                Add(native.enterCodeField.transform,"Code");Add(native.BackButton.transform,"Back");
                Add(native.joinGamePassiveButton.transform,"Join");
                AddText(native.hostText,"Host");AddText(native.capacityText,"Capacity");
                AddText(native.serverText,"Region");AddText(native.chatText,"Chat");
                Add(EnterCodeManagerPatch.ServerList.transform,"Server");Add(EnterCodeManagerPatch.ServerListDrop.transform,"ServerDropdown");
                Add(EnterCodeManagerPatch.vServerOnlyCheckBox.transform,"ServerOnly");
                Add(EnterCodeManagerPatch.RestoreButton.transform,"Restore");Add(EnterCodeManagerPatch.DownloadButton.transform,"Download");
                Add(EnterCodeManagerPatch.UnloadAndJoinButton.transform,"Unload");
                if(EnterCodeManagerPatch.VersionText!=null)AddText(EnterCodeManagerPatch.VersionText,"Warning");
                if(native.loadingIcon!=null)Add(native.loadingIcon.transform,"Loading");
                if(native.enterCodeCheckmark!=null)Add(native.enterCodeCheckmark.transform,"Checkmark");
                foreach(string id in new[]{"Back","Join","Restore","Download","Unload"}){
                    string individual="button-"+id.ToLowerInvariant();
                    string file=Path.Combine(Paths.ConfigPath,"TOH-Shell","MainMenu","CodeEntry",individual+".png");
                    var image=OptionalArt(File.Exists(file)?individual:"button",SeasideCodeEntryLayout.Rect(id),6,false,id);
                    if(image==null)continue;
                    var source=placements.First(x=>x.Id==id).Transform.GetComponent<PassiveButton>();
                    if(source==null)continue;
                    buttonImages.Add((image,source));
                    foreach(var r in source.GetComponentsInChildren<SpriteRenderer>(true)){hidden[r]=r.enabled;r.enabled=false;}
                }
                foreach(var a in native.GetComponentsInChildren<AspectPosition>(true)){anchors[a]=a.enabled;a.enabled=false;}
                // Hide only obsolete code-screen art/labels. Never touch unrelated popup renderers.
                foreach(var r in native.GetComponentsInChildren<Renderer>(true))if(!Kept(r.transform)){hidden[r]=r.enabled;r.enabled=false;}
                // Checkbox's old long label is replaced independently, without touching its button/checkmark.
                foreach(var t in EnterCodeManagerPatch.vServerOnlyCheckBox.GetComponentsInChildren<TextMeshPro>(true))
                    foreach(var r in t.GetComponentsInChildren<Renderer>(true)){hidden[r]=r.enabled;r.enabled=false;}
                var protectedRoots=keep.Append(native.transform).ToArray();
                foreach(var b in owner.GetComponentsInChildren<PassiveButton>(true))
                    if(!protectedRoots.Any(t=>b.transform.IsChildOf(t)))foreach(var c in b.GetComponentsInChildren<Collider2D>(true))
                    { if(!blocked.ContainsKey(c)){blocked[c]=c.enabled;c.enabled=false;} }
                Logger.Info("SEASIDE_CODE_ENTRY_READY nativeControls=True","SeasideMenu");
            }
            catch { Dispose();throw; }
        }
        bool Kept(Transform t)=>keep.Any(k=>t.IsChildOf(k));
        void Add(Transform t,string id)
        {
            if(t==null)return;
            if(placements.Any(p=>p.Transform==t))return;
            var p=new Placement{Id=id,Transform=t,Position=t.localPosition,Scale=t.localScale,Rotation=t.rotation,Rect=SeasideCodeEntryLayout.Rect(id),Width=1,Height=1};
            // Collider.bounds is empty for inactive/disabled native fields (notably Join).
            // Use authored geometry so later activation never produces an enormous scale.
            var collider=t.GetComponentInChildren<BoxCollider2D>(true);
            var renderer=t.GetComponentInChildren<SpriteRenderer>(true);
            var geometry=t;var size=new Vector3(1,1,1);
            if(collider!=null&&collider.size.x>0&&collider.size.y>0){geometry=collider.transform;size=new(collider.size.x,collider.size.y,1);}
            else if(renderer!=null){geometry=renderer.transform;size=renderer.drawMode==SpriteDrawMode.Simple&&renderer.sprite!=null?renderer.sprite.bounds.size:new Vector3(renderer.size.x,renderer.size.y,1);}
            var scale=t.lossyScale;
            p.Width=Math.Max(.01f,Math.Abs(size.x*geometry.lossyScale.x)/Math.Max(.0001f,Math.Abs(scale.x)));
            p.Height=Math.Max(.01f,Math.Abs(size.y*geometry.lossyScale.y)/Math.Max(.0001f,Math.Abs(scale.y)));
            placements.Add(p);keep.Add(t);
            CaptureNativeGraphics(t);
        }
        void CaptureNativeGraphics(Transform t)
        {
            foreach(var r in t.GetComponentsInChildren<Renderer>(true))if(!orders.ContainsKey(r))orders[r]=r.sortingOrder;
            foreach(var b in t.GetComponentsInChildren<PassiveUiElement>(true))if(!clickMasks.ContainsKey(b))clickMasks[b]=b.ClickMask;
            foreach(var r in t.GetComponentsInChildren<SpriteRenderer>(true))if(!spriteMasks.ContainsKey(r))spriteMasks[r]=r.maskInteraction;
        }
        void AddText(TextMeshPro text,string id)
        {
            Add(text.transform,id);var p=placements.Last();p.Text=text;p.Size=text.rectTransform.sizeDelta;
            p.Font=text.fontSize;p.Min=text.fontSizeMin;p.Max=text.fontSizeMax;p.Auto=text.enableAutoSizing;p.Wrap=text.enableWordWrapping;p.Alignment=text.alignment;
        }
        SpriteRenderer Box(string name,SeasideRect r,Color color,int order)
        {
            var s=HomeView.SpriteObject(name,root.transform,HomeView.SolidSprite(),color,order);
            s.transform.localPosition=HomeView.At(r.CenterX,r.CenterY,-.02f-order*.002f);
            s.transform.localScale=new((float)r.Width/100,(float)r.Height/100,1);return s;
        }
        TextMeshPro Label(string name,SeasideRect r,string value,float size,Color color)
        {
            var text=Object.Instantiate(owner.quitButton.GetComponentInChildren<TextMeshPro>(true),root.transform);text.name=name;text.DestroyTranslator();
            foreach(var a in text.GetComponentsInChildren<AspectPosition>(true))a.enabled=false;
            text.transform.localPosition=HomeView.At(r.CenterX,r.CenterY,-.09f);text.transform.localScale=Vector3.one;
            text.rectTransform.sizeDelta=new((float)r.Width/100,(float)r.Height/100);text.fontSize=text.fontSizeMax=size;text.fontSizeMin=size*.6f;
            text.enableAutoSizing=true;text.enableWordWrapping=false;text.alignment=TextAlignmentOptions.Center;text.fontStyle=FontStyles.Normal;text.color=color;text.text=value;
            text.GetComponent<MeshRenderer>().sortingOrder=7;text.GetComponent<MeshRenderer>().enabled=true;text.gameObject.SetActive(true);return text;
        }
        SpriteRenderer OptionalArt(string name,SeasideRect r,int order,bool contain,string instance=null)
        {
            var renderer=HomeView.SpriteObject("CodeEntryArt_"+(instance??name),root.transform,HomeView.SolidSprite(),Color.white,order);renderer.enabled=false;
            Texture2D texture=null;
            try
            {
                if(missingArt.Contains(name))return null;
                if(!artCache.TryGetValue(name,out var sprite)){
                string path=Path.Combine(Paths.ConfigPath,"TOH-Shell","MainMenu","CodeEntry",name+".png");
                if(!File.Exists(path)){missingArt.Add(name);return null;}
                long length=new FileInfo(path).Length;
                if(length>16*1024*1024||length+imageBytes>32*1024*1024)throw new InvalidDataException();
                var bytes=File.ReadAllBytes(path);
                if(bytes.Length<24||bytes[0]!=137||bytes[1]!=80||bytes[2]!=78||bytes[3]!=71)throw new InvalidDataException();
                uint w=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)),h=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
                if(!SeasideCodeEntryLayout.ValidImageDimensions(w,h)||(long)w*h+imagePixels>16*1024*1024)throw new InvalidDataException();
                texture=new Texture2D(2,2,TextureFormat.ARGB32,false);
                if(!ImageConversion.LoadImage(texture,bytes)||!SeasideCodeEntryLayout.ValidImageDimensions((uint)texture.width,(uint)texture.height))throw new InvalidDataException();
                sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new(.5f,.5f),100);sprites.Add(sprite);textures.Add(texture);texture=null;
                artCache[name]=sprite;imageBytes+=length;imagePixels+=(long)w*h;
                }
                renderer.sprite=sprite;renderer.transform.localPosition=HomeView.At(r.CenterX,r.CenterY,-.02f-order*.002f);
                float x=(float)r.Width/sprite.texture.width,y=(float)r.Height/sprite.texture.height;
                renderer.transform.localScale=contain?new(Math.Min(x,y),Math.Min(x,y),1):new(x,y,1);renderer.enabled=true;return renderer;
            }
            catch {missingArt.Add(name);if(texture!=null)Object.Destroy(texture);Logger.Warn("SEASIDE_CODE_ENTRY_ART_UNAVAILABLE "+name,"SeasideMenu");return null;}
        }
        internal bool Tick()
        {
            if(disposed||Native==null||!Native.gameObject.activeInHierarchy||!MenuSceneAvailable(owner))return false;
            // Error/account/consent overlays keep their native ordering and input ownership.
            // Suspending does not reset the TextBox, lookup state, callbacks or native modal.
            if(PopupOpen()){RestoreNative();root.SetActive(false);return true;}
            var cam=Camera.main;if(cam==null||!cam.orthographic){root.SetActive(false);return true;}
            FitSurface(owner,root,background.transform,matte,cam);root.SetActive(true);
            foreach(var p in placements)
            {
                var t=p.Transform;if(t==null)continue;
                var parent=t.parent;var ps=parent==null?Vector3.one:parent.lossyScale;
                float k=p.Text!=null?1:Math.Min((float)p.Rect.Width/100/p.Width,(float)p.Rect.Height/100/p.Height);
                float scale=root.transform.lossyScale.x*k;
                t.localScale=new(scale/Math.Max(.0001f,Math.Abs(ps.x)),scale/Math.Max(.0001f,Math.Abs(ps.y)),1);
                t.position=root.transform.TransformPoint(HomeView.At(p.Rect.CenterX,p.Rect.CenterY,p.Rect==SeasideCodeEntryLayout.Rect("ServerDropdown")?-.55f:-.2f));t.rotation=root.transform.rotation;
                if(p.Text!=null){p.Text.rectTransform.sizeDelta=new((float)p.Rect.Width/100,(float)p.Rect.Height/100);p.Text.fontSize=p.Text.fontSizeMax=p.Rect.Height<=52?2.6f:1.8f;p.Text.fontSizeMin=1.2f;p.Text.enableAutoSizing=true;p.Text.enableWordWrapping=false;p.Text.alignment=TextAlignmentOptions.Center;}
            }
            // ServerDropdown pools option buttons on first open. Capture those late
            // controls without replacing their callbacks or controller navigation.
            var dropdown=EnterCodeManagerPatch.ServerListDrop;
            var dropdownTransform=dropdown==null?null:dropdown.transform;
            if(dropdown!=null&&dropdown.gameObject.activeInHierarchy)CaptureNativeGraphics(dropdownTransform);
            foreach(var pair in hidden)if(pair.Key!=null)pair.Key.enabled=false;
            foreach(var pair in anchors)if(pair.Key!=null)pair.Key.enabled=false;
            foreach(var pair in blocked)if(pair.Key!=null)pair.Key.enabled=false;
            foreach(var pair in orders)if(pair.Key!=null)pair.Key.sortingOrder=dropdownTransform!=null&&pair.Key.transform.IsChildOf(dropdownTransform)?30:20;
            foreach(var pair in clickMasks)if(pair.Key!=null)pair.Key.ClickMask=null;
            foreach(var pair in spriteMasks)if(pair.Key!=null)pair.Key.maskInteraction=SpriteMaskInteraction.None;
            foreach(var pair in buttonImages){
                var b=pair.source;pair.image.enabled=b!=null&&b.gameObject.activeInHierarchy;
                if(b!=null){
                    bool enabled=b.enabled&&b.GetComponentsInChildren<Collider2D>(true).Any(c=>c.enabled);
                    bool highlighted=(b.activeSprites!=null&&b.activeSprites.activeInHierarchy)||(b.selectedSprites!=null&&b.selectedSprites.activeInHierarchy);
                    pair.image.color=!enabled?new Color(.5f,.5f,.5f,1):highlighted?new Color(.9f,1,1,1):Color.white;
                }
            }
            version.text="TOH-Shell v"+Main.PluginShowVersion;
            officialVersion.text=nativeVersion?.text==null?"":nativeVersion.text.text;
            friendLabel.text="フレンド・通知"+(friends?.NotifCount==null?"":" "+friends.NotifCount.text);
            return true;
        }
        void RestoreNative()
        {
            foreach(var p in placements)if(p.Transform!=null){p.Transform.localPosition=p.Position;p.Transform.localScale=p.Scale;p.Transform.rotation=p.Rotation;if(p.Text!=null){p.Text.rectTransform.sizeDelta=p.Size;p.Text.fontSize=p.Font;p.Text.fontSizeMin=p.Min;p.Text.fontSizeMax=p.Max;p.Text.enableAutoSizing=p.Auto;p.Text.enableWordWrapping=p.Wrap;p.Text.alignment=p.Alignment;}}
            foreach(var p in hidden)if(p.Key!=null)p.Key.enabled=p.Value;
            foreach(var p in blocked)if(p.Key!=null)p.Key.enabled=p.Value;
            foreach(var p in anchors)if(p.Key!=null)p.Key.enabled=p.Value;
            foreach(var p in orders)if(p.Key!=null)p.Key.sortingOrder=p.Value;
            foreach(var p in clickMasks)if(p.Key!=null)p.Key.ClickMask=p.Value;
            foreach(var p in spriteMasks)if(p.Key!=null)p.Key.maskInteraction=p.Value;
            foreach(var p in stamps)if(p.Key!=null){p.Key.transform.position=p.Value.position;p.Key.sortingOrder=p.Value.order;}
        }
        internal void PositionStamp(SpriteRenderer stamp)
        {
            if(!Visible)return;
            if(!stamps.ContainsKey(stamp))stamps.Add(stamp,(stamp.transform.position,stamp.sortingOrder));
            stamp.transform.position=root.transform.TransformPoint(HomeView.At(1590,210,-.2f));stamp.sortingOrder=8;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;RestoreNative();
            if(root!=null)Object.Destroy(root);foreach(var s in sprites)Object.Destroy(s);foreach(var t in textures)Object.Destroy(t);
            if(ownsArtwork&&artwork!=null){Object.Destroy(artwork.texture);Object.Destroy(artwork);}
        }
    }
}
