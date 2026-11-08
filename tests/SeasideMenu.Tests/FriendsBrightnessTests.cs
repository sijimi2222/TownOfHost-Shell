using System.Reflection;
using TownOfHost;
using UnityEngine;
using Obj=UnityEngine.Object;

partial class Program
{
 static SpriteRenderer NativeBlur(FriendsListUI ui,bool enabled=true)
 {
  var g=new GameObject("Blur");g.transform.SetParent(ui.transform,false);g.transform.localScale=new(21.54566f,9.641196f,1);
  var r=g.AddComponent<SpriteRenderer>();r.color=new(.3f,.3f,.3f,.3f);r.enabled=enabled;r.sortingOrder=0;return r;
 }
 static SpriteRenderer Focus(string id)=>Proxy(id).GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name=="Focus");
 static void ReproduceFriendsDimming()
 {
  var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);
  Check(x.view.Visible&&!blur.enabled); // Before fix: native Blur is enabled and promoted above artwork.
  x.view.Dispose();
 }
 static void FriendsBrightnessTests()
 {
  Test("native direct Blur is suppressed at the first Open draw",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);Check(!blur.enabled&&blur.gameObject.activeSelf&&blur.sortingOrder==0&&x.view.Visible);x.view.Dispose();});
  Test("native backdrop enabled state is restored after normal close",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);Pump(x.view);CloseFriend(x.ui);Pump(x.view);Check(blur.enabled&&!x.view.FriendsBlocking);x.view.Dispose();});
  Test("pre-disabled backdrop remains disabled after close",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui,false);OpenFriend(x);Pump(x.view);CloseFriend(x.ui);Pump(x.view);Check(!blur.enabled);x.view.Dispose();});
  Test("native Blur enabled again by native Update is re-suppressed",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);for(int i=0;i<15;i++){blur.enabled=true;Time.frameCount++;x.view.Tick(true);Check(!blur.enabled);}x.view.Dispose();Check(blur.enabled);});
  Test("late-created direct backdrop is captured without modifying the panel",()=>{var x=FriendFixture();OpenFriend(x);Pump(x.view,3);var blur=NativeBlur(x.ui);Pump(x.view,1);Check(!blur.enabled&&x.renderer.enabled);x.view.Dispose();Check(blur.enabled);});
  Test("Menu descendant named Blur is preserved as panel content",()=>{var x=FriendFixture();var menu=new GameObject("Menu");menu.transform.SetParent(x.ui.transform,false);var r=new GameObject("Blur").AddComponent<SpriteRenderer>();r.transform.SetParent(menu.transform,false);OpenFriend(x);Pump(x.view);Check(r.enabled&&r.sortingOrder==20);x.view.Dispose();});
  Test("unrelated modal Blur remains visible",()=>{var x=FriendFixture();var other=new GameObject("ImportantConfirmation");var r=new GameObject("Blur").AddComponent<SpriteRenderer>();r.transform.SetParent(other.transform,false);r.sortingOrder=99;OpenFriend(x);Pump(x.view);Check(r.enabled&&r.sortingOrder==99);x.view.Dispose();});
  Test("non-sprite child named Blur is not mistaken for native backdrop",()=>{var x=FriendFixture();var r=new GameObject("Blur").AddComponent<MeshRenderer>();r.transform.SetParent(x.ui.transform,false);OpenFriend(x);Check(r.enabled&&r.sortingOrder==20);x.view.Dispose();});
  Test("native click-to-close collider remains active without visible dimming",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);var b=new GameObject("ClickToClose").AddComponent<PassiveButton>();b.transform.SetParent(x.ui.transform,false);var c=b.gameObject.AddComponent<BoxCollider2D>();OpenFriend(x);Pump(x.view);Check(!blur.enabled&&c.enabled&&(bool)Call("StartupClick",b));CloseFriend(x.ui);Pump(x.view);Check(x.ui.CloseCalls==1);x.view.Dispose();});
  Test("Shell artwork colours and enabled states are unchanged behind panel",()=>{var x=FriendFixture();var root=Obj.Objects.Single(g=>g.name=="ShellSeasideHome");var items=root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name!="Focus").Select(r=>(r,r.color,r.enabled,r.sprite)).ToArray();var blur=NativeBlur(x.ui);OpenFriend(x);Pump(x.view);foreach(var item in items){Near(item.r.color.r,item.color.r);Near(item.r.color.g,item.color.g);Near(item.r.color.b,item.color.b);Near(item.r.color.a,item.color.a);Check(item.r.enabled==item.enabled&&item.r.sprite==item.sprite);}Check(!blur.enabled);x.view.Dispose();});
  foreach(var target in TownOfHost.Modules.MainMenu.SeasideMenuLayout.Targets)
  {
   string id=target.Id;
   Test("background hover and direct click do not react while friends open: "+id,()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);var focus=Focus(id);Proxy(id).OnMouseOver.Invoke();Proxy(id).OnClick.Invoke();Pump(x.view);Check(!focus.enabled&&x.view.Visible&&x.view.FriendsBlocking&&!x.view.Transitioning);Neutral(id);Check(!(bool)Call("StartupClick",Proxy(id)));x.view.Dispose();});
  }
  Test("Friends entry hover highlight is cleared before native Open",()=>{var x=FriendFixture();Proxy("Friends").OnMouseOver.Invoke();Check(Focus("Friends").enabled);OpenFriend(x);Check(!Focus("Friends").enabled);Proxy("Friends").OnMouseOver.Invoke();Check(!Focus("Friends").enabled);x.view.Dispose();});
  Test("pending hover and press reset before opening native panel",()=>{var x=FriendFixture();Proxy("Play").OnMouseOver.Invoke();Proxy("Play").OnClick.Invoke();Pump(x.view,2);OpenFriend(x);Pump(x.view);Check(!Focus("Play").enabled&&!x.view.Transitioning);Neutral("Play");x.view.Dispose();});
  Test("hidden original home buttons cannot receive keyboard click while panel is open",()=>{var x=FriendFixture();OpenFriend(x);foreach(var b in new[]{x.menu.playButton,x.menu.settingsButton,x.menu.quitButton})Check(!(bool)Call("StartupClick",b));Check((bool)Call("StartupClick",x.row));x.view.Dispose();});
  Test("persistent hidden native friend entry cannot acquire keyboard click",()=>{var x=FriendFixture();var entry=Obj.Objects.Single(g=>g.name=="FriendOriginal").GetComponent<PassiveButton>();entry.transform.SetParent(null,true);OpenFriend(x);Check(!(bool)Call("StartupClick",entry));x.view.Dispose();});
  Test("same-state controller highlighting cannot animate background selection",()=>{var x=FriendFixture(false);OpenFriend(x);x.home.CurrentSelection=Proxy("Play");Proxy("Play").OnMouseOver.Invoke();Controller.currentTouchType=Controller.TouchType.Joystick;Pump(x.view);Check(!Focus("Play").enabled);Neutral("Play");Check(!(bool)Call("StartupClick",Proxy("Play")));x.view.Dispose();});
  Test("normal hover and clicks recover after closing list",()=>{var x=FriendFixture();OpenFriend(x);Pump(x.view);CloseFriend(x.ui);Pump(x.view);Proxy("Play").OnMouseOver.Invoke();Check(Focus("Play").enabled&&(bool)Call("StartupClick",Proxy("Play")));Proxy("Play").OnClick.Invoke();Pump(x.view);Check(x.view.Transitioning);x.view.Dispose();});
  Test("input remains blocked during exit even without a dimmer",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);Pump(x.view);CloseFriend(x.ui);for(int i=0;i<15;i++){Time.frameCount++;x.view.Tick(true);Check(x.view.FriendsBlocking&&!blur.enabled);Proxy("Play").OnMouseOver.Invoke();Check(!Focus("Play").enabled&&!(bool)Call("StartupClick",Proxy("Play")));}Pump(x.view,3);Check(!x.view.FriendsBlocking&&blur.enabled);x.view.Dispose();});
  Test("reversing close keeps backdrop suppressed and does not restore home input early",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);Pump(x.view);CloseFriend(x.ui);Pump(x.view,4);Check(!(bool)Call("FriendsOpening"));Call("FriendsOpened",x.ui);Pump(x.view);Check(!blur.enabled&&x.ui.CloseCalls==0&&x.view.FriendsBlocking);x.view.Dispose();Check(blur.enabled);});
  Test("repeated open-close restores visual and input snapshots without drift",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);for(int i=0;i<30;i++){x.ui.gameObject.SetActive(true);OpenFriend(x);Pump(x.view);Check(!blur.enabled);CloseFriend(x.ui);Pump(x.view);Check(blur.enabled&&Proxy("Play").Colliders[0].enabled);Near(x.ui.transform.position.x,3.1);}x.view.Dispose();});
  foreach(float aspect in new[]{4f/3,16f/9,21f/9})Test("resolution changes preserve clear background and native panel scale: "+aspect,()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);Camera.main.aspect=aspect;Pump(x.view);Check(!blur.enabled&&x.view.Visible);Near(x.ui.transform.localScale.x,1);Near(x.ui.transform.localScale.y,1);x.view.Dispose();});
  Test("native ESC-equivalent Close request restores input after reverse slide",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);Pump(x.view);CloseFriend(x.ui);Pump(x.view);Check(x.ui.CloseCalls==1&&blur.enabled&&(bool)Call("StartupClick",Proxy("Play")));x.view.Dispose();});
  Test("force-close restores the original backdrop immediately",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);CloseFriend(x.ui,true);Check(blur.enabled&&!x.view.FriendsBlocking&&x.ui.CloseCalls==1);x.view.Dispose();});
  Test("native disable restores dimmer without calling native close again",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);x.ui.IsOpen=false;x.ui.gameObject.SetActive(false);Call("FriendsDisabled",x.ui);Check(blur.enabled&&x.ui.CloseCalls==0&&!x.view.FriendsBlocking);x.view.Dispose();});
  Test("scene change restores only owned backdrop and does not overwrite game controller state",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);GameScene();var state=new ControllerUiElementsState();ControllerManager.Instance.CurrentUiState=state;Call("Tick",x.menu);Check(blur.enabled&&!x.view.FriendsBlocking&&ControllerManager.Instance.CurrentUiState==state&&x.ui.CloseCalls==0);});
  Test("important popup remains accessible and restores backdrop state",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);var dialog=new GameObject("ImportantModal").AddComponent<GenericPopup>();Call("GenericOpening",dialog);Call("Tick",x.menu);Check(blur.enabled&&!x.view.Visible&&!x.view.FriendsBlocking);x.view.Dispose();});
  Test("dispose during entrance restores backdrop exactly once",()=>{var x=FriendFixture();var blur=NativeBlur(x.ui);OpenFriend(x);Pump(x.view,2);x.view.Dispose();x.view.Dispose();Check(blur.enabled&&x.ui.CloseCalls==1);});
  Test("missing direct Blur leaves standard list functional",()=>{var x=FriendFixture();OpenFriend(x);Pump(x.view);Check(x.view.Visible&&x.renderer.enabled&&x.row.Colliders[0].enabled);x.view.Dispose();});
 }
}
