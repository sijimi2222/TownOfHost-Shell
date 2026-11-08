using System.Reflection;
using TownOfHost;
using UnityEngine;
using Obj=UnityEngine.Object;
partial class Program
{
 sealed class MenuReturnFixture
 {
  internal MainMenuManager Menu;internal SeasideMainMenuPatch.HomeView View;internal FriendsListUI Friends;
  internal ControllerUiElementsState HomeState,Overlay;internal string LastScreen;
  internal MenuReturnFixture(bool useFriends=false)
  {
   if(useFriends){var x=FriendFixture();Menu=x.menu;View=x.view;Friends=x.ui;NativeBlur(Friends);OpenFriend(x);Pump(View);CloseFriend(Friends);Pump(View);}
   else{var x=Setup();Menu=x.menu;View=x.view;BindHome(x);}
   HomeState=ControllerManager.Instance.CurrentUiState;
   Menu.inventoryButton.OnClick.AddListener(()=>Leave("Inventory"));Menu.shopButton.OnClick.AddListener(()=>Leave("Shop"));
  }
  void Leave(string screen){LastScreen=screen;Call("LeavingHome");Menu.Home=false;Overlay=new(){MenuName=screen,IsScene=false};Overlay.SelectableUiElements.Add(new GameObject("NativeScreenControl").AddComponent<PassiveButton>());ControllerManager.Instance.CurrentUiState=Overlay;}
  internal void Open(string id){LastScreen=null;Proxy(id).OnClick.Invoke();Pump(View);Check(LastScreen!=null&&!View.Visible);}
  internal void Return(bool late=true,bool newState=false)
  {
   var home=newState?new ControllerUiElementsState{MenuName=HomeState.MenuName,IsScene=true}:HomeState;
   if(newState){home.SelectableUiElements.Add(Menu.playButton);home.CurrentSelection=Menu.playButton;}
   if(!late)ControllerManager.Instance.CurrentUiState=home;
   Menu.Home=true;Menu.animating=false;CredentialsPatch.TOHhmLogo.gameObject.SetActive(true);Call("Reset",Menu);Pump(View,1);
   if(late)ControllerManager.Instance.CurrentUiState=home;
   HomeState=home;Pump(View,2);
  }
 }
 static void ReproduceNativeReturn(){var x=new MenuReturnFixture();x.Open("Inventory");x.Return(true,true);Proxy("News").OnMouseOver.Invoke();Pump(x.View,2);Check(Focus("News").enabled);Proxy("News").OnClick.Invoke();Pump(x.View);Check(x.Menu.newsButton.OnClick.Calls==1);x.View.Dispose();}
 static bool NavHas(PassiveButton button)=>ControllerManager.Instance.CurrentUiState.SelectableUiElements.Any(e=>e!=null&&e.GetInstanceID()==button.GetInstanceID());
 static void NativeMenuReturnTests()
 {
  foreach(string page in new[]{"Inventory","Shop"})foreach(bool late in new[]{false,true})foreach(bool fresh in new[]{false,true})foreach(bool friends in new[]{false,true})
   Test($"native return {page} late={late} fresh={fresh} friends={friends}",()=>{var x=new MenuReturnFixture(friends);x.Open(page);x.Return(late,fresh);Check(x.View.Visible&&NavHas(Proxy("Play"))&&!x.View.FriendsBlocking);Proxy("News").OnMouseOver.Invoke();Pump(x.View,2);Check(Focus("News").enabled);Proxy("News").OnClick.Invoke();Pump(x.View);Check(x.Menu.newsButton.OnClick.Calls==1);x.View.Dispose();});
  Test("native in-place selectable replacement is repaired after a return",()=>{var x=new MenuReturnFixture();x.Open("Inventory");x.Return();var state=ControllerManager.Instance.CurrentUiState;state.SelectableUiElements.Clear();state.SelectableUiElements.Add(x.Menu.playButton);Pump(x.View,1);Check(NavHas(Proxy("Play"))&&NavHas(Proxy("Friends")));x.View.Dispose();});
  Test("same-length native replacement is repaired using element identity",()=>{var x=new MenuReturnFixture();var state=ControllerManager.Instance.CurrentUiState;int n=state.SelectableUiElements.Count;state.SelectableUiElements=new();for(int i=0;i<n;i++)state.SelectableUiElements.Add(x.Menu.playButton);Pump(x.View,1);Check(NavHas(Proxy("Inventory"))&&state.SelectableUiElements.Count==n);x.View.Dispose();});
  Test("a new native home state is reclaimed without an extra Hide Show",()=>{var x=new MenuReturnFixture();var state=new ControllerUiElementsState{MenuName=x.HomeState.MenuName,IsScene=true};state.SelectableUiElements.Add(x.Menu.playButton);ControllerManager.Instance.CurrentUiState=state;Pump(x.View,1);Check(NavHas(Proxy("Play")));Proxy("Settings").OnClick.Invoke();Pump(x.View);Check(x.Menu.settingsButton.OnClick.Calls==1);x.View.Dispose();});
  Test("foreign modal is never adopted just because home artwork is visible",()=>{var x=new MenuReturnFixture();var modal=new ControllerUiElementsState{MenuName="AccountConfirmation",IsScene=false};var button=new GameObject("NativeModalControl").AddComponent<PassiveButton>();modal.SelectableUiElements.Add(button);modal.CurrentSelection=button;ControllerManager.Instance.CurrentUiState=modal;Proxy("Play").OnClick.Invoke();Pump(x.View,25);Check(modal.SelectableUiElements.Single()==button&&modal.CurrentSelection==button&&!x.View.Transitioning);x.View.Dispose();});
  Test("a same-name overlay is not mistaken for the native scene",()=>{var x=new MenuReturnFixture();var modal=new ControllerUiElementsState{MenuName=x.HomeState.MenuName,IsScene=false};var items=modal.SelectableUiElements;ControllerManager.Instance.CurrentUiState=modal;Pump(x.View);Check(modal.SelectableUiElements==items&&!NavHas(Proxy("Play")));x.View.Dispose();});
  Test("closing a foreign modal resumes the existing home lease",()=>{var x=new MenuReturnFixture();ControllerManager.Instance.CurrentUiState=new(){MenuName="Modal",IsScene=false};Pump(x.View,2);ControllerManager.Instance.CurrentUiState=x.HomeState;Pump(x.View,2);Proxy("News").OnClick.Invoke();Pump(x.View);Check(x.Menu.newsButton.OnClick.Calls==1);x.View.Dispose();});
  Test("late native setup hook refreshes home controls",()=>{var x=new MenuReturnFixture();var state=new ControllerUiElementsState{MenuName=x.HomeState.MenuName,IsScene=true};state.SelectableUiElements.Add(x.Menu.playButton);ControllerManager.Instance.CurrentUiState=state;Call("NativeMenuNavigationReady",x.Menu);Check(NavHas(Proxy("Inventory")));x.View.Dispose();});
  Test("native setup hook does not hijack friends navigation",()=>{var x=FriendFixture();NativeBlur(x.ui);OpenFriend(x);var state=ControllerManager.Instance.CurrentUiState;var items=state.SelectableUiElements;Call("NativeMenuNavigationReady",x.menu);Pump(x.view);Check(ControllerManager.Instance.CurrentUiState==state&&state.SelectableUiElements==items&&x.view.FriendsBlocking);x.view.Dispose();});
  Test("native setup hook outside menu is inert",()=>{var x=new MenuReturnFixture();GameScene();var state=new ControllerUiElementsState{MenuName="Game",IsScene=true};ControllerManager.Instance.CurrentUiState=state;var items=state.SelectableUiElements;Call("NativeMenuNavigationReady",x.Menu);Check(state.SelectableUiElements==items);x.View.Dispose();});
  Test("replaced native selectable list survives Hide instead of restoring stale snapshot",()=>{var x=new MenuReturnFixture();var state=x.HomeState;var native=new Il2CppSystem.Collections.Generic.List<UiElement>{x.Menu.settingsButton};state.SelectableUiElements=native;x.View.Hide();Check(state.SelectableUiElements==native);x.View.Dispose();});
  Test("missing controller waits without losing return recovery",()=>{var x=new MenuReturnFixture();x.Open("Shop");ControllerManager.Instance.CurrentUiState=null;x.Menu.Home=true;Call("Reset",x.Menu);Pump(x.View,2);ControllerManager.Instance.CurrentUiState=x.HomeState;Pump(x.View,2);Check(NavHas(Proxy("Play")));x.View.Dispose();});
  Test("native-disabled source is not force-enabled during navigation repair",()=>{var x=new MenuReturnFixture();x.Open("Inventory");x.Return(true,true);x.Menu.shopButton.enabled=false;Proxy("Shop").OnClick.Invoke();Pump(x.View);Check(!x.Menu.shopButton.enabled&&x.Menu.shopButton.OnClick.Calls==0);x.View.Dispose();});
  Test("forty alternating inventory shop returns preserve every input collider",()=>{var x=new MenuReturnFixture();for(int i=0;i<40;i++){x.Open(i%2==0?"Inventory":"Shop");x.Return(i%3==0,i%2==0);Check(x.View.Visible&&NavHas(Proxy("Play")));foreach(var t in TownOfHost.Modules.MainMenu.SeasideMenuLayout.Targets)Check(Proxy(t.Id).Colliders.All(c=>c.enabled));}x.View.Dispose();});
  Test("alternating native pages after a friend session retains typed Blur fix",()=>{var x=new MenuReturnFixture(true);var blur=x.Friends.transform.Find("Blur").GetComponent<SpriteRenderer>();for(int i=0;i<8;i++){x.Open(i%2==0?"Inventory":"Shop");x.Return(true,true);x.Friends.gameObject.SetActive(true);Proxy("Friends").OnClick.Invoke();Pump(x.View);Check(!blur.enabled&&x.View.FriendsBlocking);CloseFriend(x.Friends);Pump(x.View);Check(blur.enabled&&!x.View.FriendsBlocking&&NavHas(Proxy("Play")));}x.View.Dispose();});
  Test("canceled native transition returns to a usable home",()=>{var x=new MenuReturnFixture();x.Open("Shop");x.Return(false,false);Proxy("Inventory").OnClick.Invoke();Pump(x.View);Check(x.Menu.inventoryButton.OnClick.Calls==1);x.View.Dispose();});
  Test("ESC-style native reset permits a new click after a deferred pop",()=>{var x=new MenuReturnFixture();x.Open("Inventory");x.Menu.Home=true;Call("Reset",x.Menu);ControllerManager.Instance.CurrentUiState=x.HomeState;Call("Reset",x.Menu);Pump(x.View,2);Proxy("Shop").OnClick.Invoke();Pump(x.View);Check(x.Menu.shopButton.OnClick.Calls==1);x.View.Dispose();});
  foreach(bool scene in new[]{false,true})foreach(string name in new[]{"", "NativeHome"})
   Test($"native home identity survives replacement scene={scene} named={name.Length>0}",()=>{var x=Setup();var home=ControllerManager.Instance.CurrentUiState;home.IsScene=scene;home.MenuName=name;BindHome(x);var next=new ControllerUiElementsState{IsScene=scene,MenuName=name};next.SelectableUiElements.Add(x.menu.playButton);ControllerManager.Instance.CurrentUiState=next;Pump(x.view,2);Check(NavHas(Proxy("Inventory")));x.view.Dispose();});
  Test("native replacement does not restore a hidden proxy as native selection",()=>{var x=new MenuReturnFixture();var native=new Il2CppSystem.Collections.Generic.List<UiElement>{x.Menu.settingsButton};x.HomeState.SelectableUiElements=native;Pump(x.View,1);x.View.Hide();Check(x.HomeState.SelectableUiElements==native&&x.HomeState.CurrentSelection==x.Menu.settingsButton);x.View.Dispose();});
  Test("first click after a deferred native pop reconciles without another frame",()=>{var x=new MenuReturnFixture();x.Open("Inventory");x.Menu.Home=true;Call("Reset",x.Menu);ControllerManager.Instance.CurrentUiState=x.HomeState;Proxy("News").OnClick.Invoke();Pump(x.View);Check(x.Menu.newsButton.OnClick.Calls==1);x.View.Dispose();});
  Test("native input diagnostics stop at sixteen hit events",()=>{var x=new MenuReturnFixture();for(int i=0;i<80;i++)Call("NativeMenuHover",Proxy("News"),Proxy("News").Colliders[0]);Check(Logger.Infos.Count(s=>s.StartsWith("MENU_INPUT_HIT"))==16);x.View.Dispose();});
  Test("native input probe records chosen hit without arbitrary labels",()=>{var x=new MenuReturnFixture();var other=new GameObject("PRIVATE_DUMMY_NAME").AddComponent<PassiveButton>();var col=other.gameObject.AddComponent<BoxCollider2D>();Call("NativeMenuHover",other,col);Check(Logger.Infos.Any(s=>s.StartsWith("MENU_INPUT_HIT"))&&!string.Join("\n",Logger.Infos).Contains("PRIVATE_DUMMY"));x.View.Dispose();});
  Test("native input probe never disables a foreign raycast target",()=>{var x=new MenuReturnFixture();var other=new GameObject("NativeTransparentMask").AddComponent<PassiveButton>();var c=other.gameObject.AddComponent<BoxCollider2D>();Call("NativeMenuHover",other,c);Check(c.enabled&&other.enabled);x.View.Dispose();});
 }
}
