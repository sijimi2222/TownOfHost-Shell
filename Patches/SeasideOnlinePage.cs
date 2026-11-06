using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using HarmonyLib;
using TMPro;
using TownOfHost.Modules.MainMenu;
using UnityEngine;
using Object=UnityEngine.Object;

namespace TownOfHost;
internal static partial class SeasideMainMenuPatch
{
    // Run after native authorization, initialization and Shell's existing online-button setup.
    [HarmonyPatch(typeof(MainMenuManager),nameof(MainMenuManager.OpenOnlineMenu)),HarmonyPostfix,HarmonyPriority(Priority.Last)]
    static void OnlineMenuReady(MainMenuManager __instance)
    {
        if(view?.Owner!=__instance || !MenuSceneAvailable(__instance) || PopupOpen())return;
        try { view.TryOpenOnlinePage(); } catch { FailSafe(); }
    }
    internal sealed partial class HomeView
    {
        const string OnlineScalerPath="MainUI/AspectScaler/RightPanel/MaskedBlackScreen/OnlineButtons/AspectSize/Scaler";
        GameObject onlineArtwork;
        bool onlinePage,returnToOnline,createOnlineAllowed,codeOnlineAllowed,nativeOnlineActionPending;
        PassiveButton createOnlineSource,codeOnlineSource;
        readonly List<PassiveButton> onlineButtons=new();
        readonly List<Texture2D> onlineTextures=new();
        internal bool OnlinePage => onlinePage;
        internal bool NativeOnlineActionPending => nativeOnlineActionPending;
        void CompleteOnlineHandoff(string reason)
        {
            if(!nativeOnlineActionPending)return;
            nativeOnlineActionPending=false;
            Logger.Info("SEASIDE_ONLINE_HANDOFF_END reason="+reason,"SeasideMenu");
        }
        void CreateOnlinePage()
        {
            onlineArtwork=new GameObject("SeasideOnlineArtwork");onlineArtwork.layer=root.layer;
            onlineArtwork.transform.SetParent(root.transform,false);onlineArtwork.SetActive(false);
            OnlineShape(onlineArtwork.transform,"OnlineHeading",SeasideOnlineLayout.Title,new(0,0,1672,941),new Color32(251,241,221,255),3);
            OnlineText(onlineArtwork.transform,"OnlineHeadingText",SeasideOnlineLayout.Title,new(0,0,1672,941),"オンライン",4.5f);
            foreach(string id in SeasideOnlineLayout.Ids)
            {
                var card=SeasideOnlineLayout.Rect(id);
                var b=NewButton(id,card,()=>RequestClick(id,false),false);
                float w=(float)card.Width/100,h=(float)card.Height/100,c=.25f;
                b.GetComponent<PolygonCollider2D>().points=new Vector2[]{new(-w/2+c,h/2),new(w/2-c,h/2),new(w/2,h/2-c),new(w/2,-h/2+c),new(w/2-c,-h/2),new(-w/2+c,-h/2),new(-w/2,-h/2+c),new(-w/2,h/2-c)};
                var pivot=new GameObject("Motion_"+id);pivot.layer=root.layer;pivot.transform.SetParent(b.transform,false);
                var focus=b.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.gameObject.name=="Focus");
                focus.transform.SetParent(pivot.transform,false);focus.sortingOrder=7;
                OnlineShape(pivot.transform,id+"Border",card,card,new Color32(24,20,36,255),2);
                var inner=new SeasideRect(card.X+5,card.Y+5,card.Width-10,card.Height-10);
                OnlineShape(pivot.transform,id+"Face",inner,card,new Color32(251,241,221,255),3);
                if(id!="OnlineBack")
                {
                    OnlineShape(pivot.transform,id+"ImagePlaceholder",SeasideOnlineLayout.ImageBox(id),card,new Color32(139,134,157,255),4);
                    AddOnlineImage(pivot.transform,id,card);
                    OnlineShape(pivot.transform,id+"Label",SeasideOnlineLayout.Label(id),card,new Color32(251,241,221,255),5);
                }
                OnlineText(pivot.transform,id+"Text",SeasideOnlineLayout.Label(id),card,
                    id=="OnlineCreate"?"ゲーム作成":id=="OnlineCode"?"コードを入力":"戻る",3.7f);
                motionBindings.Add(id,new(){Button=b,Pivot=pivot.transform,Focus=focus});onlineButtons.Add(b);b.gameObject.SetActive(false);
            }
        }
        void OnlineShape(Transform parent,string name,SeasideRect rect,SeasideRect origin,Color color,int order)
        {
            double cut=Math.Min(25,rect.Height*.3);
            var ps=new[]{(rect.X+cut,rect.Y),(rect.X+rect.Width-cut,rect.Y),(rect.X+rect.Width,rect.Y+cut),
                (rect.X+rect.Width,rect.Y+rect.Height-cut),(rect.X+rect.Width-cut,rect.Y+rect.Height),
                (rect.X+cut,rect.Y+rect.Height),(rect.X,rect.Y+rect.Height-cut),(rect.X,rect.Y+cut)};
            var mesh=new Mesh {vertices=ps.Select(p=>new Vector3((float)(p.Item1-origin.CenterX)/100,(float)(origin.CenterY-p.Item2)/100,0)).ToArray(),
                triangles=new[]{0,1,2,0,2,3,0,3,4,0,4,5,0,5,6,0,6,7},colors=Enumerable.Repeat(color,8).ToArray()};
            mesh.RecalculateBounds();pageMeshes.Add(mesh);var go=new GameObject(name);go.layer=root.layer;go.transform.SetParent(parent,false);
            go.transform.localPosition=new(0,0,-.02f-order*.001f);go.AddComponent<MeshFilter>().mesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.material=pageMaterial;r.sortingOrder=order;
        }
        void OnlineText(Transform parent,string name,SeasideRect rect,SeasideRect origin,string label,float size)
        {
            var t=NewText(name,rect,size,new Color32(30,24,45,255));t.transform.SetParent(parent,false);
            t.transform.localPosition=new((float)(rect.CenterX-origin.CenterX)/100,(float)(origin.CenterY-rect.CenterY)/100,-.06f);
            t.GetComponent<MeshRenderer>().sortingOrder=6;t.text=label;
        }
        sealed class OnlineImageOptions { public OnlineImageOptions(){} public double Zoom{get;set;}=1;public double OffsetX{get;set;}public double OffsetY{get;set;} }
        void AddOnlineImage(Transform parent,string id,SeasideRect card)
        {
            string basename=id=="OnlineCreate"?"create":"join";
            string folder=Path.Combine(Paths.ConfigPath,"TOH-Shell","MainMenu","Online");
            var box=SeasideOnlineLayout.ImageBox(id);Sprite sprite=null;Texture2D texture=null;
            // A named renderer exists even without an image; it never owns a collider/event.
            var renderer=SpriteObject(id+"Artwork",parent,SolidSprite(),Color.white,5);renderer.enabled=false;
            try
            {
                string file=Path.Combine(folder,basename+".png");
                if(File.Exists(file))
                {
                    var info=new FileInfo(file);if(info.Length<=0||info.Length>16*1024*1024)throw new InvalidDataException();
                    var bytes=File.ReadAllBytes(file);
                    if(bytes.Length<24||bytes[0]!=137||bytes[1]!=80||bytes[2]!=78||bytes[3]!=71)throw new InvalidDataException();
                    uint w=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)),h=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
                    if(w==0||h==0||w>4096||h>4096)throw new InvalidDataException();
                    texture=new Texture2D(2,2,TextureFormat.ARGB32,false);
                    if(!ImageConversion.LoadImage(texture,bytes)||texture.width>4096||texture.height>4096)throw new InvalidDataException();
                }
                else return; // Deliberate blank placeholder until artist delivery.
                var settings=new OnlineImageOptions();string config=Path.Combine(folder,basename+".json");
                if(File.Exists(config))try { if(new FileInfo(config).Length<=4096)settings=JsonSerializer.Deserialize<OnlineImageOptions>(File.ReadAllText(config))??settings; }catch { /* defaults */ }
                var crop=SeasideOnlineLayout.Crop(id,texture.width,texture.height,settings.Zoom,settings.OffsetX,settings.OffsetY);
                sprite=Sprite.Create(texture,new Rect((float)crop.X,(float)(texture.height-crop.Y-crop.Height),(float)crop.Width,(float)crop.Height),new(.5f,.5f),100);
                pageIllustrationSprites.Add(sprite);onlineTextures.Add(texture);texture=null;
                renderer.sprite=sprite;renderer.transform.localPosition=new((float)(box.CenterX-card.CenterX)/100,(float)(card.CenterY-box.CenterY)/100,-.04f);
                renderer.transform.localScale=new((float)(box.Width/crop.Width),(float)(box.Height/crop.Height),1);renderer.enabled=true;
            }
            catch { if(texture!=null)Object.Destroy(texture);Logger.Warn("SEASIDE_ONLINE_ARTWORK_UNAVAILABLE "+id,"SeasideMenu"); }
        }
        internal bool TryOpenOnlinePage()
        {
            if(disposed || !MenuSceneAvailable(Owner) || PopupOpen())return false;
            var scaler=Owner.transform.Find(OnlineScalerPath);
            if(scaler==null || !scaler.gameObject.activeInHierarchy)return false;
            var create=scaler.Find("Create Lobby Button")?.GetComponent<PassiveButton>();
            var code=scaler.Find("Enter Code Button")?.GetComponent<PassiveButton>();
            if(create?.OnClick==null || code?.OnClick==null)return false; // Native fallback on unknown prefab.
            createOnlineSource=create;codeOnlineSource=code;
            createOnlineAllowed=create.gameObject.activeInHierarchy;codeOnlineAllowed=code.gameObject.activeInHierarchy;
            // Same reset adapter already used by Shell's play page. The native online flow has
            // completed its authorization/setup; preserve its actual button events, then cover its presentation.
            Hide();CompleteOnlineHandoff("online-return");Owner.ResetScreen();awaitingNativePlay=false;returnToOnline=false;onlinePage=true;
            navigationAway=false;transition.Restore(SeasidePage.Play);ApplyPage();Present(Owner);return true;
        }
        void ApplyOnlinePage()
        {
            if(onlineArtwork!=null)onlineArtwork.SetActive(onlinePage);
            foreach(var b in onlineButtons)b.gameObject.SetActive(onlinePage);
        }
        bool CanInvokeOnline(string id)
        {
            if(nativeOnlineActionPending || !onlinePage || !SeasideOnlineLayout.Ids.Contains(id))return false;
            var source=id=="OnlineCreate"?createOnlineSource:codeOnlineSource;
            return id=="OnlineBack" || ((id=="OnlineCreate"?createOnlineAllowed:codeOnlineAllowed) && source!=null && source.enabled && source.OnClick!=null);
        }
        void InvokeOnline(string id)
        {
            if(!CanInvokeOnline(id))return;
            if(id=="OnlineBack") { onlinePage=false;returnToOnline=false;BeginPage(SeasidePage.Home);return; }
            var source=id=="OnlineCreate"?createOnlineSource:codeOnlineSource;
            // CreateGameScreen is an overlay: native MainUI can remain active throughout.
            // Keep its rendering/input ownership until a native return, not merely until an
            // animation or ActivateMainMenuUI callback completes. Enter-code also calls
            // ResetScreen while opening; that call is not a return to Shell.
            nativeOnlineActionPending=true;
            Hide();navigationAway=true;awaitingNativePlay=true;returnToOnline=true;
            Logger.Info("SEASIDE_ONLINE_HANDOFF_BEGIN action="+id,"SeasideMenu");
            try { source.OnClick.Invoke(); }
            catch { ResumeNativePlay();Logger.Warn("SEASIDE_ONLINE_ACTION_FAILED","SeasideMenu"); }
        }
    }
}
