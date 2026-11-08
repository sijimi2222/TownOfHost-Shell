using TownOfHost;using UnityEngine;
partial class Program {
 static (PassiveButton button, ControllerUiElementsState state) OpenNativeOnlineModal(MainMenuManager menu,string action,bool activateHome) {
  Call("LeavingHome");
  if(action=="OnlineCode"){menu.ResetScreen();Call("Reset",menu);}
  var panel=new GameObject(action=="OnlineCreate"?"CreateGameScreen":"EnterCodeScreen");panel.transform.SetParent(menu.transform,false);
  var b=new TownOfHost.Templates.SimpleButton(panel.transform,"NativeModalButton",new(),Color.white,Color.white,()=>{},"native").Button;
  // Native create/code overlays coexist with an active underlying MainUI. IsMainMenuActive
  // therefore does not mean that Shell owns the currently selected controller menu.
  menu.Home=true;
  if(activateHome)Call("ActivateHome",menu);
  var state=new ControllerUiElementsState{MenuName=panel.name,IsScene=false,CurrentSelection=b};state.SelectableUiElements.Add(b);
  ControllerManager.Instance.CurrentUiState=state;
  return(b,state);
 }
 static void ReproduceOnlineHandoff() {
  var x=OnlineFixture();PassiveButton b=null;
  x.create.OnClick.AddListener((Action)(()=>{b=OpenNativeOnlineModal(x.menu,"OnlineCreate",false).button;}));
  Proxy("OnlineCreate").OnClick.Invoke();Pump(x.view,30);x.view.Tick(true);
  Console.WriteLine("REPRO sourceCalls="+x.create.OnClick.Calls+";ShellVisible="+x.view.Visible+";nativeInput="+b.GetComponent<BoxCollider2D>().enabled);
  Check(!x.view.Visible&&b.GetComponent<BoxCollider2D>().enabled&&b.GetComponent<SpriteRenderer>().enabled);x.view.Dispose();
 }
 static void OnlineHandoffTests() {
  foreach(string action in new[]{"OnlineCreate","OnlineCode"})foreach(bool activate in new[]{false,true}) {
   Test("native modal retains rendering/input through home callbacks: "+action+activate,()=>{
    var x=OnlineFixture();PassiveButton b=null;ControllerUiElementsState state=null;
    var source=action=="OnlineCreate"?x.create:x.code;
    source.OnClick.AddListener((Action)(()=>{var opened=OpenNativeOnlineModal(x.menu,action,activate);b=opened.button;state=opened.state;}));
    Proxy(action).OnClick.Invoke();Pump(x.view,30);
    for(int i=0;i<100;i++){x.view.Tick(true);Call("ActivateHome",x.menu);}
    Check(source.OnClick.Calls==1&&!x.view.Visible&&b.GetComponent<BoxCollider2D>().enabled&&b.GetComponent<SpriteRenderer>().enabled);
    Check(ControllerManager.Instance.CurrentUiState==state&&state.CurrentSelection==b&&state.SelectableUiElements.Count==1);
    Proxy(action).OnClick.Invoke();Check(source.OnClick.Calls==1);x.view.Dispose();
   });
  }
  foreach(string action in new[]{"OnlineCreate","OnlineCode"})Test("native return and repeated online actions: "+action,()=>{
   var x=OnlineFixture();var home=ControllerManager.Instance.CurrentUiState;
   var source=action=="OnlineCreate"?x.create:x.code;
   source.OnClick.AddListener((Action)(()=>OpenNativeOnlineModal(x.menu,action,true)));
   for(int i=0;i<10;i++){
    Proxy(action).OnClick.Invoke();Pump(x.view,30);x.view.Tick(true);Check(!x.view.Visible);
    Check(!(bool)Call("NativePlayPage",x.menu));Call("NativePlayPageFinished",x.menu);
    ControllerManager.Instance.CurrentUiState=home;Pump(x.view,3);
    Check(x.view.Visible&&x.view.OnlinePage&&Proxy(action).GetComponent<PolygonCollider2D>().enabled);
   }
   Check(source.OnClick.Calls==10);x.view.Dispose();
  });
  foreach(string action in new[]{"OnlineCreate","OnlineCode"})Test("explicit native online return releases handoff: "+action,()=>{
   var x=OnlineFixture();(action=="OnlineCreate"?x.create:x.code).OnClick.AddListener((Action)(()=>OpenNativeOnlineModal(x.menu,action,false)));
   Proxy(action).OnClick.Invoke();Pump(x.view,30);x.view.Tick(true);Check(!x.view.Visible);
   OnlineNativePath(x.menu).gameObject.SetActive(true);Call("OnlineMenuReady",x.menu);
   Check(x.view.Visible&&x.view.OnlinePage);x.view.Dispose();
  });
  Test("native reset during online initialization preserves handoff until real return",()=>{
   var x=OnlineFixture();x.create.OnClick.AddListener((Action)(()=>OpenNativeOnlineModal(x.menu,"OnlineCreate",false)));
   Proxy("OnlineCreate").OnClick.Invoke();Pump(x.view,30);Call("Reset",x.menu);x.view.Tick(true);
   Check(!x.view.Visible&&x.view.NativeOnlineActionPending);Check(x.view.ResumeNativePlay());
   Check(x.view.Visible&&x.view.OnlinePage&&!x.view.NativeOnlineActionPending);x.view.Dispose();
  });
  foreach(string action in new[]{"OnlineCreate","OnlineCode"})Test("native exception releases handoff: "+action,()=>{
   var x=OnlineFixture();(action=="OnlineCreate"?x.create:x.code).OnClick.AddListener((Action)(()=>throw new InvalidOperationException("synthetic")));
   Proxy(action).OnClick.Invoke();Pump(x.view,30);Check(x.view.Visible&&x.view.OnlinePage);x.view.Tick(true);Check(x.view.Visible);x.view.Dispose();
  });
  Test("pending online handoff never restores UI in gameplay",()=>{
   var x=OnlineFixture();x.create.OnClick.AddListener((Action)(()=>OpenNativeOnlineModal(x.menu,"OnlineCreate",false)));
   Proxy("OnlineCreate").OnClick.Invoke();Pump(x.view,30);GameScene();Call("ActivateHome",x.menu);Check(!x.view.Visible);x.view.Dispose();
  });
  foreach(string action in new[]{"OnlineCreate","OnlineCode"})Test("stale direct action cannot duplicate native handoff: "+action,()=>{
   var x=OnlineFixture();var source=action=="OnlineCreate"?x.create:x.code;
   source.OnClick.AddListener((Action)(()=>OpenNativeOnlineModal(x.menu,action,false)));
   Proxy(action).OnClick.Invoke();Pump(x.view,30);
   var invoke=typeof(SeasideMainMenuPatch.HomeView).GetMethod("InvokeOnline",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
   invoke.Invoke(x.view,new object[]{action});invoke.Invoke(x.view,new object[]{"OnlineBack"});
   Check(source.OnClick.Calls==1&&x.view.NativeOnlineActionPending&&!x.view.Visible);x.view.Dispose();
  });
  Test("native modal button is not blocked by Shell transition guard",()=>{
   var x=OnlineFixture();PassiveButton b=null;x.create.OnClick.AddListener((Action)(()=>b=OpenNativeOnlineModal(x.menu,"OnlineCreate",false).button));
   Proxy("OnlineCreate").OnClick.Invoke();Pump(x.view,30);Check((bool)Call("StartupClick",b));x.view.Dispose();
  });
 }
}
