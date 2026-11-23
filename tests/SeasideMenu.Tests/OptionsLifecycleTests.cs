using System.Reflection;
using TownOfHost;
using UnityEngine;
using UnityEngine.SceneManagement;
using Obj=UnityEngine.Object;

partial class Program
{
 static void Field(string name,object value)=>typeof(SeasideMainMenuPatch).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,value);
 static OptionsMenuBehaviour Settings()=>new GameObject("Options").AddComponent<OptionsMenuBehaviour>();
 static void BindHome((MainMenuManager menu,SeasideMainMenuPatch.HomeView view) x){Field("view",x.view);Field("earlyOwner",x.menu);x.view.Tick(true);}
 static void GameScene(){HudManager.InstanceExists=true;PlayerControl.LocalPlayer=new();SceneManager.Active=new(){handle=2,isLoaded=true};}
 static void ReproduceStaleOptions()
 {
  var x=Setup();BindHome(x);
  var source=x.menu.playButton.GetComponent<SpriteRenderer>();Check(!source.enabled);
  GameScene();var opts=Settings();
  Call("OpenOptions",opts);
  // Stale view belongs to a scene the game has left. An options open must not replay its old snapshot.
  Check(!source.enabled);
  Check(typeof(SeasideMainMenuPatch).GetField("options",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)==null);

 }
 static void OptionsLifecycleTests()
 {
  Test("settings stale scene reproduction no longer replays menu snapshots",ReproduceStaleOptions);
  Test("home settings retain artwork while pausing background input",()=>{var x=Setup();BindHome(x);var o=Settings();o.IsOpen=true;Call("OpenOptions",o);Check(x.view.Visible&&x.view.OptionsBlocking);Call("Tick",x.menu);Check(x.view.Visible&&x.view.OptionsBlocking);o.IsOpen=false;Call("CloseOptions",o);Check(x.view.Visible);x.view.Dispose();});
  Test("home repeated settings do not duplicate artwork or buttons",()=>{var x=Setup();BindHome(x);var o=Settings();int count=Obj.Objects.Count;for(int i=0;i<20;i++){o.IsOpen=true;Call("OpenOptions",o);o.IsOpen=false;Call("CloseOptions",o);}Check(x.view.Visible&&Obj.Objects.Count==count);Check(Obj.Objects.Count(g=>g.name=="ShellSeasideHome")==1);x.view.Dispose();});
  foreach(var scenario in new[]{"lobby","task","meeting","outro"})Test("game settings untouched in "+scenario,()=>{var x=Setup();BindHome(x);GameScene();var o=Settings();var state=new ControllerUiElementsState();var selected=new GameObject("GameHelp").AddComponent<PassiveButton>();state.CurrentSelection=selected;state.SelectableUiElements.Add(selected);ControllerManager.Instance.CurrentUiState=state;var collider=selected.gameObject.AddComponent<BoxCollider2D>();var r=selected.gameObject.AddComponent<SpriteRenderer>();var p=selected.transform.position;var scale=selected.transform.localScale;o.IsOpen=true;Call("OpenOptions",o);o.IsOpen=false;Call("CloseOptions",o);Check(ControllerManager.Instance.CurrentUiState==state&&state.CurrentSelection==selected&&state.SelectableUiElements.Count==1);Check(r.enabled&&collider.enabled);Near(selected.transform.position.x,p.x);Near(selected.transform.position.y,p.y);Near(selected.transform.localScale.x,scale.x);Check(typeof(SeasideMainMenuPatch).GetField("options",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)==null);});
  Test("game HUD alone blocks menu without relying on cached game-state flags",()=>{var x=Setup();HudManager.InstanceExists=true;x.view.Tick(true);Check(!x.view.Visible);});
  Test("local player alone blocks menu even before HUD construction",()=>{var x=Setup();PlayerControl.LocalPlayer=new();x.view.Tick(true);Check(!x.view.Visible);});
  Test("inactive owner never displays home",()=>{var x=Setup();x.menu.gameObject.SetActive(false);x.view.Tick(true);Check(!x.view.Visible);x.menu.gameObject.SetActive(true);x.view.Tick(true);Check(x.view.Visible);x.view.Dispose();});
  Test("invalid scene cannot display or initialize menu",()=>{var x=Setup();x.menu.gameObject.OwnScene=new(){handle=0,isLoaded=false};Call("Start",x.menu);x.view.Tick(true);Check(!x.view.Visible&&Home==null);});
  Test("unloaded scene cannot display menu",()=>{var x=Setup();x.menu.gameObject.OwnScene=new(){handle=1,isLoaded=false};x.view.Tick(true);Check(!x.view.Visible);});
  Test("foreign scene options do not pause valid home",()=>{var x=Setup();BindHome(x);var o=Settings();o.gameObject.OwnScene=new(){handle=7,isLoaded=true};Call("OpenOptions",o);Check(x.view.Visible);x.view.Dispose();});
  Test("closing unrelated options does not resume home",()=>{var x=Setup();BindHome(x);var o=Settings();o.IsOpen=true;Call("OpenOptions",o);Call("CloseOptions",Settings());Check(x.view.Visible&&x.view.OptionsBlocking);o.IsOpen=false;Call("CloseOptions",o);Check(x.view.Visible);x.view.Dispose();});
  Test("close after scene change cannot restore native main menu controls",()=>{var x=Setup();BindHome(x);var o=Settings();o.IsOpen=true;Call("OpenOptions",o);GameScene();o.IsOpen=false;Call("CloseOptions",o);Check(!x.view.Visible);});
  Test("departed view discards blocked collider snapshot",()=>{var x=Setup();BindHome(x);var c=x.menu.playButton.GetComponent<BoxCollider2D>();Check(!c.enabled);GameScene();x.view.Hide();Check(!c.enabled);});
  Test("departed view discards covered renderer snapshot",()=>{var x=Setup();BindHome(x);var r=x.menu.playButton.GetComponent<SpriteRenderer>();GameScene();x.view.Hide();Check(!r.enabled);});
  Test("departed view discards stamp transform snapshot",()=>{var x=Setup();BindHome(x);var stamp=new GameObject("Stamp").AddComponent<SpriteRenderer>();x.view.PositionStamp(stamp);stamp.transform.position=new(21,22,23);stamp.sortingOrder=84;GameScene();x.view.Hide();Near(stamp.transform.position.x,21);Near(stamp.transform.position.y,22);Check(stamp.sortingOrder==84);});
  Test("departed view leaves old navigation snapshot untouched",()=>{var x=Setup();BindHome(x);var state=ControllerManager.Instance.CurrentUiState;var list=state.SelectableUiElements;GameScene();x.view.Hide();Check(state.SelectableUiElements==list);});
  Test("departed loading discards sorting snapshot",()=>{var x=Setup();x.view.Dispose();x.menu.finishStartup=false;Call("EarlyAwake",x.menu);var r=new GameObject("Progress").AddComponent<SpriteRenderer>();r.sortingOrder=7;Loading.Lift(r);Check(r.sortingOrder==210);r.sortingOrder=93;GameScene();Loading.Hide();Check(r.sortingOrder==93);});
  foreach(var hook in new[]{"BeforeCamera","Stamp"})Test("scene exit releases all menu handles via "+hook,()=>{var x=Setup();x.view.Dispose();Call("EarlyAwake",x.menu);Call("Start",x.menu);Check(Home!=null&&Loading!=null);GameScene();if(hook=="BeforeCamera")Call(hook,Camera.main);else Call(hook,new ModManager());Check(Home==null&&Loading==null);Check(typeof(SeasideMainMenuPatch).GetField("options",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)==null);});
  Test("game scene cannot run menu start or early loading",()=>{var x=Setup();x.view.Dispose();GameScene();Call("EarlyAwake",x.menu);Call("Start",x.menu);Check(Home==null&&Loading==null);});
  Test("game scene camera callbacks do not recreate old menu",()=>{var x=Setup();x.view.Dispose();Call("EarlyAwake",x.menu);Call("Start",x.menu);GameScene();for(int i=0;i<20;i++){Call("BeforeCamera",Camera.main);Call("Stamp",new ModManager());}Check(Home==null&&Loading==null);});
  foreach(var hook in new[]{"FriendsOpening","AccountWindowOpened","AccountWindowClosed","StartupPopupClosed"})Test("global popup hook outside menu is inert: "+hook,()=>{var x=Setup();BindHome(x);GameScene();var r=x.menu.playButton.GetComponent<SpriteRenderer>();Call(hook);Check(!r.enabled);});
  foreach(var hook in new[]{"OpenNews","NewsEnabled"})Test("news callback outside menu is inert: "+hook,()=>{var x=Setup();BindHome(x);GameScene();Call(hook,new GameObject("News").AddComponent<AnnouncementPopUp>());Check(!x.menu.playButton.GetComponent<SpriteRenderer>().enabled);});
  Test("game popup does not restore stale menu snapshots",()=>{var x=Setup();BindHome(x);GameScene();Call("GenericOpening",new GameObject("GameError").AddComponent<GenericPopup>());Check(!x.menu.playButton.GetComponent<SpriteRenderer>().enabled);});
  Test("returning to a new real home recreates one interactive view",()=>{var x=Setup();x.view.Dispose();Call("EarlyAwake",x.menu);Call("Start",x.menu);GameScene();Call("BeforeCamera",Camera.main);HudManager.InstanceExists=false;PlayerControl.LocalPlayer=null;var y=Setup();y.view.Dispose();Call("EarlyAwake",y.menu);Call("Start",y.menu);Call("Tick",y.menu);Check(Home.Visible);Check(Obj.Objects.Count(g=>g.name=="ShellSeasideHome")==1);Home.Dispose();Call("StopLoading");});
  Test("settings during press cancels action without changing its target",()=>{var x=Setup();BindHome(x);Proxy("Inventory").OnClick.Invoke();Pump(x.view,3);var o=Settings();o.IsOpen=true;Call("OpenOptions",o);o.IsOpen=false;Call("CloseOptions",o);Pump(x.view,20);Check(x.menu.inventoryButton.OnClick.Calls==0&&x.view.Visible);x.view.Dispose();});
  Test("settings during wave cancels transition and restores current page",()=>{var x=Setup();BindHome(x);Proxy("Play").OnClick.Invoke();Pump(x.view,20);Check(x.view.Transitioning);var o=Settings();o.IsOpen=true;Call("OpenOptions",o);Check(!x.view.Transitioning&&x.view.Visible&&x.view.OptionsBlocking);o.IsOpen=false;Call("CloseOptions",o);Check(x.view.Visible&&!x.view.Transitioning);x.view.Dispose();});
  Test("persistent native options preserve home with input suspended",()=>{var x=Setup();BindHome(x);var o=Settings();o.gameObject.OwnScene=new(){handle=99,name="DontDestroyOnLoad",isLoaded=true};o.IsOpen=true;Call("OpenOptions",o);Check(x.view.Visible&&x.view.OptionsBlocking);o.IsOpen=false;Call("CloseOptions",o);Check(x.view.Visible);x.view.Dispose();});
  Test("persistent native error remains above home",()=>{var x=Setup();BindHome(x);var p=new GameObject("PersistentError").AddComponent<GenericPopup>();p.gameObject.OwnScene=new(){handle=99,name="DontDestroyOnLoad",isLoaded=true};Call("GenericOpening",p);Check(!x.view.Visible);Call("Tick",x.menu);Check(!x.view.Visible);p.gameObject.SetActive(false);Call("StartupPopupClosed");Check(x.view.Visible);x.view.Dispose();});
  Test("persistent options during game cannot touch old home",()=>{var x=Setup();BindHome(x);GameScene();var o=Settings();o.gameObject.OwnScene=new(){handle=99,name="DontDestroyOnLoad",isLoaded=true};Call("OpenOptions",o);Check(!x.menu.playButton.GetComponent<SpriteRenderer>().enabled);});
  Test("invalid persistent popup scene is rejected",()=>{var x=Setup();BindHome(x);var o=Settings();o.gameObject.OwnScene=new(){handle=99,name="DontDestroyOnLoad",isLoaded=false};Call("OpenOptions",o);Check(x.view.Visible);x.view.Dispose();});
 }
}
