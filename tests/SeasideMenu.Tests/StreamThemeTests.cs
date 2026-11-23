using TownOfHost.Modules.MainMenu;
using TownOfHost;
using TownOfHost.Templates;
using UnityEngine;
using TMPro;
using Obj=UnityEngine.Object;

partial class Program
{
    static void StreamThemeTests()
    {
        Test("stream Japanese heading and back glyphs render in front of their plates",()=>
        {
            var f=StreamFixture();SeasideStreamView.Show(f.menu,new[]{("Paste",f.button)},f.labels);
            var heading=Obj.Objects.Single(g=>g.name=="StreamHeading").GetComponent<TextMeshPro>();
            var back=Obj.Objects.Single(g=>g.name=="StreamBack").GetComponentInChildren<TextMeshPro>(true);
            Check(heading.text=="配信・YouTube");
            Check(heading.GetComponentsInChildren<Renderer>(true).All(r=>r.enabled&&r.sortingOrder>4));
            Check(back.GetComponentsInChildren<Renderer>(true).All(r=>r.enabled&&r.sortingOrder>8));
            Check(heading.transform.Find("JapaneseFallback")!=null);
            SeasideStreamView.Close();f.view.Dispose();
        });
        Test("stream corner logo uses current play art without covering its heading",()=>
        {
            var f=StreamFixture();SeasideStreamView.Show(f.menu,new[]{("Paste",f.button)},f.labels);
            Check(Obj.Objects.Any(g=>g.name=="StreamShellLogo"));
            Check(!SeasideStreamLayout.Logo.Overlaps(SeasideStreamLayout.Title));
            SeasideStreamView.Close();f.view.Dispose();
        });
        Test("stream presentation preserves existing click and input events",()=>
        {
            var f=StreamFixture();int clicks=0,changes=0;
            f.button.Button.OnClick.AddListener((Action)(()=>clicks++));
            CreateStreameMenu.JoinWord.OnChange.AddListener((Action)(()=>changes++));
            SeasideStreamView.Show(f.menu,new[]{("Paste",f.button)},f.labels);
            Check(SeasideStreamView.Visible);f.button.Button.OnClick.Invoke();CreateStreameMenu.JoinWord.SetText("参加希望");
            Check(clicks==1&&changes==1&&CreateStreameMenu.JoinWord.text=="参加希望");
            SeasideStreamView.Close();f.view.Dispose();
        });
        Test("stream close restores borrowed parent geometry renderers and navigation",()=>
        {
            var f=StreamFixture();var parent=f.button.Button.transform.parent;var pos=f.button.Button.transform.localPosition;
            var nav=ControllerManager.Instance.CurrentUiState.SelectableUiElements;var collider=f.menu.playButton.GetComponent<BoxCollider2D>();
            SeasideStreamView.Show(f.menu,new[]{("Paste",f.button)},f.labels);Check(!collider.enabled);
            SeasideStreamView.Close();Check(collider.enabled&&f.button.Button.transform.parent==parent);
            Near(f.button.Button.transform.localPosition.x,pos.x);Check(ControllerManager.Instance.CurrentUiState.SelectableUiElements==nav);
            Check(!Obj.Objects.Any(g=>g.name=="ShellSeasideStream"||g.name=="StreamButton_Paste"));f.view.Dispose();
        });
        Test("stream reopening keeps a single presentation and no stale button plates",()=>
        {
            var f=StreamFixture();for(int i=0;i<12;i++)SeasideStreamView.Show(f.menu,new[]{("Paste",f.button)},f.labels);
            Check(Obj.Objects.Count(g=>g.name=="ShellSeasideStream")==1&&Obj.Objects.Count(g=>g.name=="StreamButton_Paste")==1);
            SeasideStreamView.Close();SeasideStreamView.Close();Check(!SeasideStreamView.Visible);f.view.Dispose();
        });
        Test("stream construction failure restores previously moved controls",()=>
        {
            var f=StreamFixture();var parent=f.button.Button.transform.parent;
            SeasideStreamView.Show(f.menu,new[]{("Paste",f.button),("Auth",(SimpleButton)null)},f.labels);
            Check(!SeasideStreamView.Visible&&f.button.Button.transform.parent==parent);f.view.Dispose();
        });
        foreach(var size in new[]{(1920,1080),(1536,864),(1024,768),(1280,1024),(3440,1440),(800,1280)})
            Test("stream page safe bounds and pointer conversion "+size,()=>
            {
                var fit=SeasideMenuLayout.Fit(size.Item1,size.Item2);
                foreach(var r in SeasideStreamLayout.Controls.Values.Append(SeasideStreamLayout.Title).Append(SeasideStreamLayout.Panel))
                {
                    Check(r.X>=0&&r.Y>=0&&r.X+r.Width<=SeasideMenuLayout.Width&&r.Y+r.Height<=SeasideMenuLayout.Height);
                    var p=fit.ToScreen(r.CenterX,r.CenterY);var a=fit.ToArt(p.x,p.y);Near(a.x,r.CenterX);Near(a.y,r.CenterY);
                }
            });
        Test("stream clickable controls never overlap input fields or other controls",()=>
        {
            var names=new[]{"Paste","Auth","ObsRoles","Comments","Reset","Obs","Terms","Privacy","Back","Join","Remove"};
            for(int i=0;i<names.Length;i++)for(int j=i+1;j<names.Length;j++)
                Check(!SeasideStreamLayout.Rect(names[i]).Overlaps(SeasideStreamLayout.Rect(names[j])));
        });
        Test("stream heading and body preserve Shell logo and version space",()=>
        {
            Check(!SeasideStreamLayout.Title.Overlaps(SeasideMenuLayout.VersionBox));
            Check(SeasideStreamLayout.Title.X>=490&&SeasideStreamLayout.Panel.Y>=280);
        });
    }
    static (MainMenuManager menu,SeasideMainMenuPatch.HomeView view,SimpleButton button,(string id,TextMeshPro text)[] labels) StreamFixture()
    {
        SeasideStreamView.Close();var f=Setup();
        CreateStreameMenu.JoinWord=new GameObject("JoinInput").AddComponent<TextBoxTMP>();
        CreateStreameMenu.RemoveWord=new GameObject("RemoveInput").AddComponent<TextBoxTMP>();
        CreateStreameMenu.JoinWord.transform.SetParent(f.menu.transform,false);CreateStreameMenu.RemoveWord.transform.SetParent(f.menu.transform,false);
        var b=new SimpleButton(f.menu.transform,"StreamPasteNative",new(1,2,0),Color.white,Color.white,()=>{},"設定");
        return(f.menu,f.view,b,Array.Empty<(string,TextMeshPro)>());
    }
}
namespace TownOfHost { static class CreateStreameMenu { public static TextBoxTMP JoinWord,RemoveWord; } }
