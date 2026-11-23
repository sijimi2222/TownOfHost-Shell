using System.Reflection;
using System.Runtime.CompilerServices;
using TownOfHost;
using UnityEngine;
using Obj = UnityEngine.Object;

partial class Program
{
    static void ReproduceOptionsBackground()
    {
        var x = SettingsSlideFixture();
        OpenSettingsSlide(x.ui); PumpSettings(x.menu, 1);
        Check(x.view.Visible);
        Check(!x.menu.playButton.GetComponent<SpriteRenderer>().enabled);
        Check(!Proxy("Play").Colliders[0].enabled);
        CloseSettingsSlide(x.ui); PumpSettings(x.menu, 1);
        Check(x.view.Visible && Proxy("Play").Colliders[0].enabled);
        x.view.Dispose();
    }

    static string SourceMember(string declaration, [CallerFilePath] string file = "")
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file), "../.."));
        var source = File.ReadAllText(Path.Combine(root, "Modules/MatchmakingWordManager.cs"));
        int start = source.IndexOf(declaration, StringComparison.Ordinal);
        Check(start >= 0);
        int brace = source.IndexOf('{', start), depth = 1, end = brace + 1;
        for (; depth > 0; end++) { if (source[end] == '{') depth++; if (source[end] == '}') depth--; }
        return source.Substring(brace + 1, end - brace - 2);
    }
    static void OptionsBackdropTests()
    {
        Test("settings backdrop reproduction remains evening before and after close", ReproduceOptionsBackground);
        Test("normal Settings proxy uses native handler once without revealing legacy UI", () => {
            var x=SettingsSlideFixture(); x.menu.settingsButton.OnClick.AddListener((Action)(()=>OpenSettingsSlide(x.ui)));
            Proxy("Settings").OnClick.Invoke(); Pump(x.view,18); PumpSettings(x.menu,1);
            Check(x.menu.settingsButton.OnClick.Calls==1&&x.ui.IsOpen&&x.view.Visible&&x.view.OptionsBlocking);
            Check(!x.menu.playButton.GetComponent<SpriteRenderer>().enabled);
            CloseSettingsSlide(x.ui); PumpSettings(x.menu,1); Check(x.view.Visible&&!x.view.OptionsBlocking); x.view.Dispose();
        });
        Test("evening artwork colour, scale and active state stay fixed behind settings", () => {
            var x=SettingsSlideFixture();var root=Obj.Objects.Single(g=>g.name=="ShellSeasideHome");var pose=root.transform.localScale;
            var before=root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name!="Focus").Select(r=>(r,r.color,r.enabled,r.sprite)).ToArray();
            OpenSettingsSlide(x.ui);PumpSettings(x.menu);Check(root.activeSelf);
            foreach(var item in before){Check(item.r.enabled==item.enabled&&item.r.sprite==item.sprite);Near(item.r.color.a,item.color.a);Near(item.r.color.r,item.color.r);Near(item.r.color.g,item.color.g);Near(item.r.color.b,item.color.b);}
            Near(root.transform.localScale.x,pose.x);Near(root.transform.localScale.y,pose.y);CloseSettingsSlide(x.ui);x.view.Dispose();
        });
        foreach(var target in TownOfHost.Modules.MainMenu.SeasideMenuLayout.Targets)
        Test("settings block pointer and controller invocation of "+target.Id,()=>{
            var x=SettingsSlideFixture();OpenSettingsSlide(x.ui);var b=Proxy(target.Id);
            Check(!b.Colliders[0].enabled&&!(bool)Call("StartupClick",b));
            b.OnMouseOver.Invoke();b.OnClick.Invoke();PumpSettings(x.menu);
            var source=target.Id switch {"Play"=>x.menu.playButton,"Inventory"=>x.menu.inventoryButton,"Shop"=>x.menu.shopButton,"News"=>x.menu.newsButton,"Account"=>x.menu.myAccountButton,"Settings"=>x.menu.settingsButton,"Credits"=>x.menu.creditsButton,"Quit"=>x.menu.quitButton,_=>MainMenuManagerPatch.Sources[target.Id]};
            Check(source.OnClick.Calls==0&&x.ui.IsOpen&&x.view.Visible);Neutral(target.Id);
            CloseSettingsSlide(x.ui);Check(b.Colliders[0].enabled&&(bool)Call("StartupClick",b));x.view.Dispose();
        });
        Test("settings retain native pointer targets while home is blocked",()=>{var x=SettingsSlideFixture();OpenSettingsSlide(x.ui);PumpSettings(x.menu);Check(x.row.Colliders[0].enabled&&(bool)Call("StartupClick",x.row)&&x.row.GetComponent<SpriteRenderer>().enabled);x.view.Dispose();});
        Test("settings preserve native controller overlay and selection",()=>{var x=SettingsSlideFixture();OpenSettingsSlide(x.ui);var overlay=new ControllerUiElementsState(){MenuName="Options",IsScene=false};overlay.SelectableUiElements.Add(x.row);overlay.CurrentSelection=x.row;ControllerManager.Instance.CurrentUiState=overlay;PumpSettings(x.menu);Check(ControllerManager.Instance.CurrentUiState==overlay&&overlay.SelectableUiElements.Count==1&&overlay.CurrentSelection==x.row);CloseSettingsSlide(x.ui);x.view.Dispose();});
        Test("options render above retained home and restore native orders on close",()=>{var x=SettingsSlideFixture();var r=x.row.GetComponent<SpriteRenderer>();OpenSettingsSlide(x.ui);PumpSettings(x.menu,25);Check(r.sortingOrder==27&&x.view.Visible);CloseSettingsSlide(x.ui);Check(r.sortingOrder==7);x.view.Dispose();});
        Test("late-created options tab keeps foreground order after slide finishes",()=>{var x=SettingsSlideFixture();OpenSettingsSlide(x.ui);PumpSettings(x.menu);var r=new GameObject("TabContent").AddComponent<MeshRenderer>();r.sortingOrder=5;r.transform.SetParent(x.ui.transform,false);PumpSettings(x.menu,1);Check(r.enabled&&r.sortingOrder==25);CloseSettingsSlide(x.ui);Check(r.sortingOrder==5);x.view.Dispose();});
        Test("OnDisable alone releases modal input before a future normal close",()=>{var x=SettingsSlideFixture();OpenSettingsSlide(x.ui);x.ui.IsOpen=false;x.ui.gameObject.SetActive(false);Call("OptionsDisabled",x.ui);Check(x.view.Visible&&!x.view.OptionsBlocking&&Proxy("Play").Colliders[0].enabled);Call("CloseOptions",x.ui);PumpSettings(x.menu,1);Check(x.view.Visible);x.view.Dispose();});
        Test("destroyed settings release home input without Close",()=>{var x=SettingsSlideFixture();OpenSettingsSlide(x.ui);Call("OptionsDestroyed",x.ui);Check(!x.view.OptionsBlocking&&Proxy("Play").Colliders[0].enabled);x.view.Dispose();});
        Test("missing close callback is recovered from inactive native dialog",()=>{var x=SettingsSlideFixture();OpenSettingsSlide(x.ui);x.ui.IsOpen=false;x.ui.gameObject.SetActive(false);PumpSettings(x.menu,1);Check(x.view.Visible&&!x.view.OptionsBlocking&&Proxy("Play").Colliders[0].enabled);x.view.Dispose();});
        Test("native controller pop after OnDisable rebinds home without stale modal navigation",()=>{var x=SettingsSlideFixture();var home=ControllerManager.Instance.CurrentUiState;OpenSettingsSlide(x.ui);var overlay=new ControllerUiElementsState(){MenuName="Options",IsScene=false};overlay.CurrentSelection=x.row;ControllerManager.Instance.CurrentUiState=overlay;x.ui.IsOpen=false;x.ui.gameObject.SetActive(false);Call("OptionsDisabled",x.ui);ControllerManager.Instance.CurrentUiState=home;Call("CloseOptions",x.ui);PumpSettings(x.menu,1);Check(x.view.Visible&&x.view.OwnsCurrentNavigation&&(bool)Call("StartupClick",Proxy("Play")));x.view.Dispose();});
        Test("pre-disabled proxy collider is not accidentally enabled by modal cleanup",()=>{var x=SettingsSlideFixture();var b=Proxy("Play");b.Colliders[0].enabled=false;OpenSettingsSlide(x.ui);CloseSettingsSlide(x.ui);Check(!b.Colliders[0].enabled);x.view.Dispose();});
        Test("twenty settings cycles preserve hidden native home and restore proxies",()=>{var x=SettingsSlideFixture();for(int i=0;i<20;i++){OpenSettingsSlide(x.ui);PumpSettings(x.menu,3);Check(x.view.Visible&&!x.menu.playButton.GetComponent<SpriteRenderer>().enabled);CloseSettingsSlide(x.ui);PumpSettings(x.menu,1);Check(x.view.Visible&&Proxy("Play").Colliders[0].enabled);}x.view.Dispose();});
        Test("settings departure still releases snapshots without replaying menu in gameplay",()=>{var x=SettingsSlideFixture();OpenSettingsSlide(x.ui);GameScene();Call("OptionsDisabled",x.ui);Call("Tick",x.menu);Check(Home==null&&!x.view.Visible&&!x.menu.playButton.GetComponent<SpriteRenderer>().enabled);});
        Test("settings errors stay on top and home returns only after error closes",()=>{var x=SettingsSlideFixture();OpenSettingsSlide(x.ui);var popup=new GameObject("RequiredError").AddComponent<GenericPopup>();Call("GenericOpening",popup);PumpSettings(x.menu,1);Check(!x.view.Visible&&popup.gameObject.activeSelf);CloseSettingsSlide(x.ui);Check(!x.view.Visible);popup.gameObject.SetActive(false);Call("StartupPopupClosed");PumpSettings(x.menu,1);Check(x.view.Visible);x.view.Dispose();});
        Test("options native children under owner are never covered as legacy home",()=>{var x=SettingsSlideFixture();x.ui.transform.SetParent(x.menu.transform,false);OpenSettingsSlide(x.ui);PumpSettings(x.menu);Check(x.row.GetComponent<SpriteRenderer>().enabled&&x.row.Colliders[0].enabled);CloseSettingsSlide(x.ui);x.view.Dispose();});
        Test("unrelated Close cannot release a currently displayed settings modal",()=>{var x=SettingsSlideFixture();OpenSettingsSlide(x.ui);Call("CloseOptions",Settings());Check(x.view.OptionsBlocking&&!Proxy("Play").Colliders[0].enabled);CloseSettingsSlide(x.ui);x.view.Dispose();});
        Test("no HUD exists contract: settings close checks existence before chat restoration or scheduling",()=>{var s=SourceMember("public static void HideEditor()");var guard=s.IndexOf("if (!HudManager.InstanceExists)");Check(guard>=0&&guard<s.IndexOf("TryRestoreChatUiNow();")&&guard<s.IndexOf("ScheduleChatUiRefresh(8f, 0f);"));});
        Test("no HUD exists contract: chat restoration never calls singleton getter before existence check",()=>{var s=SourceMember("private static void TryRestoreChatUiNow()");Check(s.IndexOf("if (!HudManager.InstanceExists) return;")>=0&&s.IndexOf("if (!HudManager.InstanceExists) return;")<s.IndexOf("DestroyableSingleton<HudManager>.Instance"));});
        Test("ordinary options close still closes the matchmaking text editor",()=>{var s=SourceMember("public static void HideEditor()");Check(s.IndexOf("popup.gameObject.SetActive(false)")<s.IndexOf("if (!HudManager.InstanceExists)"));});
    }
}
