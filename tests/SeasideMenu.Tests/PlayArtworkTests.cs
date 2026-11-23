using System;
using System.Linq;
using TownOfHost;
using TownOfHost.Modules.MainMenu;
using UnityEngine;
using Obj = UnityEngine.Object;
partial class Program
{
    static void PlayArtworkTests()
    {
        Test("artist page keeps local on the left and online on the right",()=>{Check(SeasidePlayLayout.Local.CenterX<SeasidePlayLayout.Online.CenterX);});
        foreach(var id in new[]{"PlayLocal","PlayOnline"})
        {
            foreach(var size in new[]{(1000d,750d),(750d,1000d),(1920d,1080d),(1d,1d)})
                Test("illustration covers frame with centered source crop: "+id+size,()=>{
                    var box=SeasidePlayLayout.IllustrationBox(id);var image=SeasidePlayLayout.FitIllustration(id,size.Item1,size.Item2);
                    var crop=SeasidePlayLayout.CropIllustration(id,size.Item1,size.Item2);
                    Check(image==box);Near(crop.Width/crop.Height,box.Width/box.Height);
                    Near(crop.CenterX,size.Item1/2);Near(crop.CenterY,size.Item2/2);
                    Check(crop.X>=0&&crop.Y>=0&&crop.X+crop.Width<=size.Item1+1e-8&&crop.Y+crop.Height<=size.Item2+1e-8);
                    Near(image.Width/crop.Width,image.Height/crop.Height);Near(image.CenterX,box.CenterX);Near(image.CenterY,box.CenterY);
                    Check(image.X>=box.X-1e-8&&image.Y>=box.Y-1e-8&&image.X+image.Width<=box.X+box.Width+1e-8&&image.Y+image.Height<=box.Y+box.Height+1e-8);
                });
            Test("art remains clear of title and inside click area: "+id,()=>{var box=SeasidePlayLayout.IllustrationBox(id);var title=SeasidePlayLayout.Title(id);var hit=SeasidePlayLayout.Rect(id);Check(!box.Overlaps(title)&&box.X>=hit.X&&box.Y>=hit.Y&&box.X+box.Width<=hit.X+hit.Width&&box.Y+box.Height<=hit.Y+hit.Height);});
            Test("illustration has no collider and uses existing motion pivot: "+id,()=>{var x=Setup();var art=Obj.Objects.Single(g=>g.name=="PlayIllustration_"+id);Check(art.GetComponentsInChildren<Collider2D>(true).Length==0&&art.transform.parent==Motion(id));Check(art.GetComponent<SpriteRenderer>().sortingOrder==2);x.view.Dispose();});
        }
        foreach(var id in new[]{"PlayLocal","PlayOnline"})
        {
            Test("4:3 source retains its central subjects while filling the frame: "+id,()=>{var crop=SeasidePlayLayout.CropIllustration(id,1000,750);Near(crop.X,0);Near(crop.Width,1000);Check(crop.Y>100&&crop.Y<120&&crop.Height>520&&crop.Height<530);});
            Test("runtime crop sprite and uniform scale match cover calculation: "+id,()=>{var x=Setup();var art=Obj.Objects.Single(g=>g.name=="PlayIllustration_"+id).GetComponent<SpriteRenderer>();var crop=SeasidePlayLayout.CropIllustration(id,1000,750);Check(Math.Abs(art.sprite.rect.width-crop.Width)<1e-4&&Math.Abs(art.sprite.rect.height-crop.Height)<1e-4&&Math.Abs(art.sprite.rect.y-(750-crop.Y-crop.Height))<1e-4);Near(art.transform.localScale.x,art.transform.localScale.y);Check(art.sprite.vertices.Length==8&&art.sprite.triangles.All(t=>t<8)&&art.sprite.vertices.All(v=>v.x>=0&&v.y>=0&&v.x<=art.sprite.rect.width&&v.y<=art.sprite.rect.height));Near(art.sprite.vertices.Max(v=>v.x),art.sprite.rect.width);Near(art.sprite.vertices.Max(v=>v.y),art.sprite.rect.height);x.view.Dispose();});
        }
        Test("invalid illustration dimensions are rejected",()=>{foreach(double n in new[]{0d,-1d,double.NaN,double.PositiveInfinity}){bool rejected=false;try{SeasidePlayLayout.FitIllustration("PlayLocal",n,750);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected);}});
        Test("play background and pictures are inactive on Home, restored after native return",()=>{var x=Setup();x.view.Tick(true);var scene=Obj.Objects.Single(g=>g.name=="SeasidePlayArtwork");Check(!scene.activeInHierarchy);EnterPlay(x.view);Check(scene.activeInHierarchy);Proxy("PlayLocal").OnClick.Invoke();Pump(x.view);Check(!scene.activeInHierarchy);Check(x.view.ResumeNativePlay());x.view.Tick(true);Check(scene.activeInHierarchy);x.view.Dispose();});
        Test("reverse wave restores the original Home art without play-card residue",()=>{var x=Setup();EnterPlay(x.view);Proxy("PlayBack").OnClick.Invoke();Pump(x.view,160);Check(x.view.Page==SeasidePage.Home&&!Obj.Objects.Single(g=>g.name=="SeasidePlayArtwork").activeSelf);x.view.Dispose();});
        Test("baked titles are masked and re-rendered as textured moving shapes",()=>{var x=Setup();foreach(string id in SeasidePlayLayout.Ids){var title=Obj.Objects.Single(g=>g.name=="PlayTitle_"+id);var shadow=Obj.Objects.Single(g=>g.name=="PlayTitleShadow_"+id);Check(title.transform.parent==Motion(id)&&shadow.transform.parent.name=="SeasidePlayArtwork");var mesh=title.GetComponent<MeshFilter>().mesh;Check(mesh.uv.Length==6&&mesh.uv.All(v=>v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1));}x.view.Dispose();});
        Test("play illustrations and title share the existing hover and press animation",()=>{var x=Setup();EnterPlay(x.view);var b=Proxy("PlayLocal");b.OnMouseOver.Invoke();Pump(x.view,15);Check(Math.Abs(Motion("PlayLocal").localRotation.Z)>.01);b.OnClick.Invoke();Pump(x.view,6);Near(Motion("PlayLocal").localScale.y,.95);Near(Motion("PlayLocal").localRotation.Z,0);x.view.Dispose();});
        Test("missing single illustration leaves page actions usable",()=>{var x=Setup();x.view.Dispose();UtilsSprite.MissingAsset="TownOfHost.Resources.MainMenu.Play.local.png";var v=new SeasideMainMenuPatch.HomeView(x.menu);EnterPlay(v);Check(!Obj.Objects.Any(g=>g.name=="PlayIllustration_PlayLocal"));Proxy("PlayLocal").OnClick.Invoke();Pump(v);Check(x.menu.playLocalButton.OnClick.Calls==1);v.Dispose();});
        Test("missing page template falls back rather than covering native controls",()=>{var x=Setup();x.view.Dispose();UtilsSprite.MissingAsset="TownOfHost.Resources.MainMenu.Play.page.png";Call("Start",x.menu);Check(Home==null&&x.menu.playButton.GetComponent<BoxCollider2D>().enabled);});
        Test("dynamic version covers only the baked text, preserving the artist frame",()=>{var x=Setup();EnterPlay(x.view);var frame=Obj.Objects.Single(g=>g.name=="PlayDynamicVersionFrame").GetComponent<SpriteRenderer>();Near(frame.transform.localScale.x,3.5);Near(frame.transform.localScale.y,.46);Check(Text("ShellVersion").text=="TOH-Shell v2.2.0β"&&Text("GameVersion").text=="v19.8a (build num: 7492)");x.view.Dispose();});
        Test("artist page teardown leaves no visual objects",()=>{var x=Setup();EnterPlay(x.view);x.view.Dispose();Check(!Obj.Objects.Any(g=>g.name.StartsWith("PlayIllustration_")||g.name.StartsWith("PlayTitle_")||g.name=="SeasidePlayArtwork"));});
    }
}
