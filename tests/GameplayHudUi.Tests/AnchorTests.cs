using System.Reflection;
using TownOfHost;
using UnityEngine;

partial class Program {
 static void InitializeHeaderFixture(){
  GameStates.Intro=false;
  H.transform.parent=H.UICamera.transform;
  H.SettingsButton.transform.parent=H.transform;H.Chat.chatButton.transform.parent=H.transform;
  RoleGuideButtonPatch._btnRenderer.transform.parent=H.transform;
  var labels=new GameObject();labels.transform.parent=H.transform;
  CredentialsPatch.pingTrackerCredential.transform.parent=labels.transform;
  foreach(var name in new[]{"headerLayoutValid","headerMeshPending","headerLastHasGuide"}){
   var f=typeof(CredentialsPatch).GetField(name,BindingFlags.Static|BindingFlags.NonPublic);f?.SetValue(null,false);
  }
 }
 static void PlaceButtons(){float x=H.UICamera.orthographicSize*H.UICamera.aspect-.5f;H.SettingsButton.transform.localPosition=new(x,2.5f,-1);H.Chat.chatButton.transform.localPosition=new(x-.78f,2.5f,-1);}
 static void Layout(){RoleGuideButtonPatch.UpdateTopRightButtonLayout();CredentialsPatch.UpdateCredentialLayout();LateHeader();}
 static void AnchorDriftReproduction(){
  Layout();float x=CredentialsPatch.pingTrackerCredential.transform.localPosition.x;
  // Model a world-bounds sample taken on a different camera-update boundary.
  // The old one-way clamp retains its shift after the transient sample disappears.
  RoleGuideButtonPatch.guideFrameRenderer.StaleWorldDelta=new(-5,0,0);LateHeader();
  RoleGuideButtonPatch.guideFrameRenderer.StaleWorldDelta=new(0,0,0);LateHeader();
  Near(CredentialsPatch.pingTrackerCredential.transform.localPosition.x,x);
 }
 static void AnchorTests(){
  Test("transient world-bounds mismatch cannot accumulate a permanent header displacement",AnchorDriftReproduction);
  foreach(float aspect in new[]{4f/3,16f/9,21f/9,9f/16})
  foreach(string scene in new[]{"lobby","tasks","meeting"})
  foreach(float direction in new[]{-1f,1f})Test($"HUD anchor is invariant under camera pan {scene} {aspect} {direction}",()=>{
   H.UICamera.aspect=aspect;GameStates.IsLobby=scene=="lobby";GameStates.IsMeeting=scene=="meeting";GameStates.IsInTask=scene=="tasks";
   PlaceButtons();Layout();var text=CredentialsPatch.pingTrackerCredential;var local=text.transform.localPosition;
   var screen=H.UICamera.WorldToViewportPoint(text.transform.position);int updates=text.MeshUpdates,adjust=CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments;
   for(int i=0;i<120;i++){
    H.UICamera.transform.localPosition=new(direction*i*.7f,(i%11)-5,-10);
    Layout();Near(text.transform.localPosition.x,local.x);Near(text.transform.localPosition.y,local.y);
    var after=H.UICamera.WorldToViewportPoint(text.transform.position);Near(after.x,screen.x);Near(after.y,screen.y);
   }
   Check(text.transform.PositionWrites==0&&text.MeshUpdates==updates&&CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments==adjust);
  });
  Test("credential uses the same HUD parent and explicit UI camera",()=>{Layout();Check(CredentialsPatch.pingTrackerCredential.transform.parent==H.transform&&CredentialsPatch.pingTrackerCredentialAspectPos.parentCam==H.UICamera&&!CredentialsPatch.pingTrackerCredentialAspectPos.updateAlways);});
  Test("world-space setter honours a translated scaled parent",()=>{var root=new GameObject();root.transform.localPosition=new(20,-5,0);root.transform.localScale=new(2,3,1);var child=new GameObject();child.transform.parent=root.transform;child.transform.position=new(26,1,0);Near(child.transform.localPosition.x,3);Near(child.transform.localPosition.y,2);Near(child.transform.position.x,26);});
  Test("stable header needs no per-frame mesh rebuild or anchor write",()=>{Layout();var t=CredentialsPatch.pingTrackerCredential;int n=t.MeshUpdates,a=CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments;for(int i=0;i<300;i++)Layout();Check(t.MeshUpdates==n&&CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments==a);});
  Test("longer text rebuilds once and restores the HELP gap",()=>{Layout();var t=CredentialsPatch.pingTrackerCredential;int n=t.MeshUpdates;t.text+="\nextra warning";t.PendingInkOverflow=1;Layout();Check(t.MeshUpdates==n+1&&InkRight()<RoleGuideButtonPatch.guideFrameRenderer.bounds.min.x);for(int i=0;i<20;i++)Layout();Check(t.MeshUpdates==n+1);});
  Test("shorter text returns to its absolute anchor rather than retaining a left shift",()=>{var t=CredentialsPatch.pingTrackerCredential;t.PendingInkOverflow=1.2f;Layout();float x=t.transform.localPosition.x;t.text="TownOfHost-Shell v2.2.3";t.PendingInkOverflow=0;Layout();Check(t.transform.localPosition.x>x+1.1f);});
  Test("same text with changed TMP properties invalidates the geometry cache",()=>{Layout();var t=CredentialsPatch.pingTrackerCredential;int n=t.MeshUpdates;t.havePropertiesChanged=true;t.PendingInkOverflow=1;LateHeader();Check(t.MeshUpdates==n+1&&InkRight()<RoleGuideButtonPatch.guideFrameRenderer.bounds.min.x);});
  Test("native resolution event preserves the stored overhang correction",()=>{var t=CredentialsPatch.pingTrackerCredential;t.PendingInkOverflow=1;Layout();float x=t.transform.localPosition.x;CredentialsPatch.pingTrackerCredentialAspectPos.AdjustPosition();NativeAdjusted(CredentialsPatch.pingTrackerCredentialAspectPos);Near(t.transform.localPosition.x,x);});
  foreach(float aspect in new[]{4f/3,16f/9,21f/9,9f/16})Test("resizing updates one anchor without changing text scale "+aspect,()=>{Layout();var t=CredentialsPatch.pingTrackerCredential;var scale=t.transform.localScale;H.UICamera.aspect=aspect;PlaceButtons();Layout();Check(t.transform.localScale==scale&&InkRight()<RoleGuideButtonPatch.guideFrameRenderer.bounds.min.x);int n=t.MeshUpdates;for(int i=0;i<20;i++)Layout();Check(t.MeshUpdates==n);});
  Test("meeting and task transitions reanchor without accumulating offsets",()=>{Layout();float task=CredentialsPatch.pingTrackerCredential.transform.localPosition.x;for(int i=0;i<40;i++){GameStates.IsMeeting=true;GameStates.IsInTask=false;Layout();GameStates.IsMeeting=false;GameStates.IsInTask=true;Layout();Near(CredentialsPatch.pingTrackerCredential.transform.localPosition.x,task);}});
  Test("replacement HUD adopts the same label once without duplication",()=>{Layout();var text=CredentialsPatch.pingTrackerCredential;var old=H;HudManager.Instance=new();InitializeHeaderFixture();PlaceButtons();Layout();Check(ReferenceEquals(text,CredentialsPatch.pingTrackerCredential)&&text.transform.parent==H.transform&&text.transform.parent!=old.transform);int n=text.transform.ParentChanges;for(int i=0;i<50;i++)Layout();Check(text.transform.ParentChanges==n);});
  Test("no active HUD after scene exit is safe",()=>{Layout();HudManager.Instance=null;LateHeader();Clamp();});
  Test("temporarily missing UI camera is safe and can recover",()=>{Layout();var camera=H.UICamera;H.UICamera=null;LateHeader();H.UICamera=camera;Layout();Check(CredentialsPatch.pingTrackerCredentialAspectPos.parentCam==camera);});
  Test("HUD hide and show retains its native anchor",()=>{Layout();float x=CredentialsPatch.pingTrackerCredential.transform.localPosition.x;H.gameObject.SetActive(false);LateHeader();H.gameObject.SetActive(true);Layout();Near(CredentialsPatch.pingTrackerCredential.transform.localPosition.x,x);});
  Test("both version and red warning remain one unchanged TMP object",()=>{var t=CredentialsPatch.pingTrackerCredential;string content=t.text;Layout();H.UICamera.transform.localPosition=new(-24,10,0);Layout();Check(ReferenceEquals(t,CredentialsPatch.pingTrackerCredential)&&t.text==content&&content.Contains("ゲームを終了しない"));});
  Test("unavailable aspect component does not interrupt HUD",()=>{CredentialsPatch.pingTrackerCredentialAspectPos=null;LateHeader();});
 }
}
