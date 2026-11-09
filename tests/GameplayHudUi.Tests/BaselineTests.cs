using TownOfHost;
using UnityEngine;

partial class Program {
 static void ReproduceInheritedPingOffset(){
  // Read from the current game's PingTracker constructor, not a guessed UI coordinate.
  CredentialsPatch.pingTrackerCredentialAspectPos.DistanceFromEdge=new(-.21f,.65f,0);
  Layout();Near(CredentialsPatch.pingTrackerCredential.transform.localPosition.y,H.UICamera.orthographicSize);
  Near(CredentialsPatch.pingTrackerCredentialAspectPos.DistanceFromEdge.y,0);
 }
 static void BaselineTests(){
  Test("native lobby PingTracker gap reproducer returns to the original Shell top anchor",ReproduceInheritedPingOffset);
  foreach(float gap in new[]{.65f,.32f})
  foreach(float aspect in new[]{4f/3,16f/9,21f/9,9f/16})
  foreach(string state in new[]{"lobby","tasks","meeting"})Test($"Shell vertical baseline ignores inherited PingTracker gap {gap} {aspect} {state}",()=>{
   H.UICamera.aspect=aspect;GameStates.IsLobby=state=="lobby";GameStates.IsMeeting=state=="meeting";GameStates.IsInTask=state=="tasks";
   PlaceButtons();CredentialsPatch.pingTrackerCredentialAspectPos.DistanceFromEdge=new(-.21f,gap,0);Layout();
   var t=CredentialsPatch.pingTrackerCredential;var p=H.UICamera.WorldToViewportPoint(t.transform.position);
   Near(p.y,1);Near(t.transform.localPosition.y,H.UICamera.orthographicSize);Near(CredentialsPatch.pingTrackerCredentialAspectPos.DistanceFromEdge.y,0);
   Check(InkRight()<RoleGuideButtonPatch.guideFrameRenderer.bounds.min.x);
   float x=t.transform.localPosition.x;int adjustments=CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments;
   for(int i=0;i<40;i++){H.UICamera.transform.localPosition=new(i*.25f,-i*.125f,0);Layout();Near(t.transform.localPosition.x,x);Near(H.UICamera.WorldToViewportPoint(t.transform.position).y,1);}
   Check(CredentialsPatch.pingTrackerCredentialAspectPos.Adjustments==adjustments);
  });
  Test("native anchor callbacks cannot restore the lobby gap",()=>{Layout();for(int i=0;i<20;i++){CredentialsPatch.pingTrackerCredentialAspectPos.DistanceFromEdge=new(-.21f,.65f,0);CredentialsPatch.pingTrackerCredentialAspectPos.AdjustPosition();NativeAdjusted(CredentialsPatch.pingTrackerCredentialAspectPos);LateHeader();Near(CredentialsPatch.pingTrackerCredential.transform.localPosition.y,H.UICamera.orthographicSize);}});
  Test("foreign template drawing depth is not inherited by the Shell header",()=>{CredentialsPatch.pingTrackerCredentialAspectPos.DistanceFromEdge=new(4,.65f,-350);Layout();Near(CredentialsPatch.pingTrackerCredential.transform.localPosition.z,0);});
  Test("warning line toggles do not change the vertical baseline",()=>{var t=CredentialsPatch.pingTrackerCredential;foreach(var text in new[]{"TownOfHost-Shell v2.2.3","TownOfHost-Shell v2.2.3\nゲームを終了しない","TownOfHost-Shell v2.2.3"}){t.text=text;Layout();Near(t.transform.localPosition.y,H.UICamera.orthographicSize);Check(t.text==text);}});
  Test("nonzero inherited gap is fixed even while HELP is not yet created",()=>{RoleGuideButtonPatch.guideFrameRenderer=null;CredentialsPatch.pingTrackerCredentialAspectPos.DistanceFromEdge=new(-.21f,.65f,0);Layout();Near(CredentialsPatch.pingTrackerCredential.transform.localPosition.y,H.UICamera.orthographicSize);});
  Test("absolute overhang correction is stable after several native gap resets",()=>{var t=CredentialsPatch.pingTrackerCredential;t.PendingInkOverflow=1.1f;Layout();float x=t.transform.localPosition.x;for(int i=0;i<50;i++){CredentialsPatch.pingTrackerCredentialAspectPos.DistanceFromEdge=new(-.21f,i%2==0?.65f:.32f,0);Layout();Near(t.transform.localPosition.x,x);Near(t.transform.localPosition.y,H.UICamera.orthographicSize);}});
 }
}
