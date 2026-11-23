using TownOfHost;
using UnityEngine;
using NativeCorners=Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<UnityEngine.Vector3>;

partial class Program {
 static void ReproduceNativeCornerCopy(){
  GameStates.IsLobby=true;GameStates.IsInGame=GameStates.IsInTask=false;PlaceButtons();Layout();
  float restingX=CredentialsPatch.pingTrackerCredential.transform.localPosition.x;
  GameStates.IsLobby=false;GameStates.IsInGame=GameStates.IsInTask=true;
  H.UICamera.transform.localPosition=new(-12,1,-10);PlaceButtons();Layout();
  Near(CredentialsPatch.pingTrackerCredential.transform.localPosition.x,restingX+.78f);
 }
 static void LifecycleTests(){
  Test("managed corner array conversion really copies instead of returning native output",()=>{var managed=new Vector3[4];CredentialsPatch.pingTrackerCredential.transform.position=new(4,2,0);CredentialsPatch.pingTrackerCredential.rectTransform.GetWorldCorners(managed);Check(managed.All(x=>x==new Vector3(0,0,0)));});
  Test("native corner buffer receives real world coordinates",()=>{var native=new NativeCorners(4);CredentialsPatch.pingTrackerCredential.transform.position=new(4,2,0);CredentialsPatch.pingTrackerCredential.rectTransform.GetWorldCorners(native);Near(native[2].x,4);Near(native[2].y,2);});
  Test("lobby-to-game camera relocation cannot turn world origin into a huge left shift",ReproduceNativeCornerCopy);
  foreach(float aspect in new[]{4f/3,16f/9,21f/9,9f/16})
  foreach(float cameraX in new[]{-27f,-12f,-1f,0f,15f,41f})Test($"native output stays correct across game-start camera relocation {aspect} {cameraX}",()=>{
   H.UICamera.aspect=aspect;GameStates.IsLobby=true;GameStates.IsInGame=GameStates.IsInTask=false;PlaceButtons();Layout();
   GameStates.IsLobby=false;GameStates.IsInGame=GameStates.IsInTask=true;H.UICamera.transform.localPosition=new(cameraX,7,-10);PlaceButtons();Layout();
   var t=CredentialsPatch.pingTrackerCredential;var viewport=H.UICamera.WorldToViewportPoint(t.transform.position);
   // The unchanged native header pivot sits exactly .12 HUD units left of HELP.
   var edge=RoleGuideButtonPatch.guideFrameRenderer.bounds.min.x;
   Near(t.transform.position.x,edge-.12f);Near(viewport.y,1);
   float local=t.transform.localPosition.x;int n=CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments;
   for(int i=0;i<60;i++){H.UICamera.transform.localPosition=new(cameraX+i*.2f,-i*.1f,-10);Layout();Near(t.transform.localPosition.x,local);}
   Check(CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments==n);
  });
  Test("offset trace of 7.246 cannot be produced by discarded zero corners",()=>{
   PlaceButtons();Layout();float x=CredentialsPatch.pingTrackerCredential.transform.localPosition.x;
   float allowed=RoleGuideButtonPatch.guideFrameRenderer.bounds.min.x-.12f;
   H.UICamera.transform.localPosition=new(-allowed-7.246f,0,-10);CredentialsPatch.pingTrackerCredential.havePropertiesChanged=true;Layout();
   Near(CredentialsPatch.pingTrackerCredential.transform.localPosition.x,x);
  });
  Test("native corner buffer is reused through repeated warning changes",()=>{Layout();int n=NativeCorners.Allocations;for(int i=0;i<60;i++){CredentialsPatch.pingTrackerCredential.text="TownOfHost-Shell v2.2.3\n"+i;Layout();}Check(NativeCorners.Allocations==n);});
  Test("production holds the native output buffer itself",()=>{Layout();var f=typeof(CredentialsPatch).GetField("headerWorldCorners",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);Check(f.FieldType==typeof(NativeCorners)&&f.GetValue(null) is NativeCorners a&&a.Length==4);});
  Test("intro-to-game reanchors once even when camera geometry is unchanged",()=>{Layout();GameStates.Intro=true;Layout();int n=CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments;GameStates.Intro=false;Layout();Check(CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments>n);n=CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments;for(int i=0;i<30;i++)Layout();Check(CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments==n);});
  Test("HUD ancestor replacement invalidates anchor without duplicating the label",()=>{Layout();var text=CredentialsPatch.pingTrackerCredential;int n=CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments;var wrapper=new GameObject();wrapper.transform.parent=H.UICamera.transform;H.transform.parent=wrapper.transform;Layout();Check(ReferenceEquals(text,CredentialsPatch.pingTrackerCredential)&&text.transform.parent==H.transform&&CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments>n);Near(text.transform.position.x,RoleGuideButtonPatch.guideFrameRenderer.bounds.min.x-.12f);});
  Test("game end and lobby return reset phase without inheriting a stale correction",()=>{Layout();var text=CredentialsPatch.pingTrackerCredential;GameStates.IsInGame=GameStates.IsInTask=false;Layout();GameStates.IsLobby=true;H.UICamera.transform.localPosition=new(-20,7,-10);Layout();Check(ReferenceEquals(text,CredentialsPatch.pingTrackerCredential));Near(text.transform.position.x,RoleGuideButtonPatch.guideFrameRenderer.bounds.min.x-.12f);Near(text.transform.localPosition.y,H.UICamera.orthographicSize);});
  Test("HUD regenerated during game start adopts label without world-position preservation",()=>{
   Layout();var label=CredentialsPatch.pingTrackerCredential;var old=H;HudManager.Instance=new();InitializeHeaderFixture();PlaceButtons();H.UICamera.transform.localPosition=new(-14,5,-10);Layout();
   Check(ReferenceEquals(label,CredentialsPatch.pingTrackerCredential)&&label.transform.parent==H.transform&&label.transform.parent!=old.transform);
   Near(label.transform.position.x,RoleGuideButtonPatch.guideFrameRenderer.bounds.min.x-.12f);
  });
  Test("reparented header rebinds once to current HUD",()=>{Layout();var t=CredentialsPatch.pingTrackerCredential;t.transform.SetParent(new GameObject().transform,false);Layout();Check(t.transform.parent==H.transform);int n=t.transform.ParentChanges;for(int i=0;i<30;i++)Layout();Check(t.transform.ParentChanges==n);});
  Test("replaced camera updates header binding and native buffer output",()=>{Layout();H.UICamera=new Camera();H.transform.parent=H.UICamera.transform;H.UICamera.transform.localPosition=new(-20,8,-10);Layout();Check(CredentialsPatch.pingTrackerCredentialAspectPos.parentCam==H.UICamera);Near(CredentialsPatch.pingTrackerCredential.transform.position.x,RoleGuideButtonPatch.guideFrameRenderer.bounds.min.x-.12f);});
  Test("repeated rounds meetings and lobby return do not accumulate left corrections",()=>{
   for(int i=0;i<30;i++){
    GameStates.IsLobby=true;GameStates.IsMeeting=GameStates.IsInTask=GameStates.IsInGame=false;H.UICamera.transform.localPosition=new(0,0,-10);PlaceButtons();Layout();
    GameStates.IsLobby=false;GameStates.IsInGame=GameStates.IsInTask=true;H.UICamera.transform.localPosition=new(-12+i*.5f,3,-10);Layout();
    Near(CredentialsPatch.pingTrackerCredential.transform.position.x,RoleGuideButtonPatch.guideFrameRenderer.bounds.min.x-.12f);
    GameStates.IsMeeting=true;GameStates.IsInTask=false;Layout();GameStates.IsMeeting=false;GameStates.IsInTask=true;Layout();
    Near(CredentialsPatch.pingTrackerCredential.transform.position.x,RoleGuideButtonPatch.guideFrameRenderer.bounds.min.x-.12f);
   }
  });
 }
}
