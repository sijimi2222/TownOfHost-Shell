using System.Text;
using System.Text.Json;
using TownOfHost;
using TownOfHost.Development;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using UnityEngine;
static partial class Program
{
 static float ExpectedMapRange()=>TownOfHost.Main.NormalOptions?.MapId==4?70f:50f;
 static void AirshipRangeTests()
 {
  Test("Airship alone selects range70 across all 256 map IDs",()=>{var n=N();for(int id=0;id<=255;id++){TownOfHost.Main.NormalOptions.MapId=(byte)id;Eq(id==4?70f:50f,Get<float>(n,"BeamRange"));}n.OnDestroy();});
  Test("missing map retains ordinary range50",()=>{var n=N();TownOfHost.Main.NormalOptions=null;Eq(50f,Get<float>(n,"BeamRange"));n.OnDestroy();});
  foreach(byte map in new byte[]{0,1,2,3,4,5,6,255})Test("selected range actual fire "+map,()=>{
   TownOfHost.Main.NormalOptions.MapId=map;var n=N();Start(n);Lock(n);Fire(n);Eq(map==4?70f:50f,Get<float>(n,"reach"));FullInk(n,ExpectedMapRange());StopRole(n);AssertGuideHidden(n);
  });
  foreach(float angle in new[]{0f,45f,90f,135f,180f,225f,270f,315f})foreach(float width in new[]{.25f,1.25f,5f})foreach(bool bundled in new[]{false,true})
   Test($"Airship70 continuous mesh and damage {angle}/{width}/bundled={bundled}",()=>{
    TownOfHost.Main.NormalOptions.MapId=4;OptionItem.All[992912].Value=width;
    if(bundled){var sample=FontSamples().First(x=>x.Object==304);Resources.Assets["Fonts & Materials/Barlow-Black SDF"]=LoadedFont(sample,false);TMPro.TextMeshPro.GeometryPadding=sample.Height*.2f;}
    var n=N();Start(n);Set(n,"angle",angle);var dir=new Vector2(MathF.Cos(angle*Mathf.Deg2Rad),MathF.Sin(angle*Mathf.Deg2Rad));var perp=new Vector2(-dir.y,dir.x);
    PlayerControl At(byte id,float along,float side=0){var p=dir*along+perp*side;return P(id,x:p.x,y:p.y);}
    var hits=new[]{At(2,49),At(3,51),At(4,69.9f),At(5,70)};var beyond=At(6,70.01f);var side=At(7,30,width*.5f+.31f);var guard=At(8,65);guard.Guarded=true;
    PhysicsHelpers.Block=(_,_,_,_)=>throw new Exception("walls and doors must not be probed");Lock(n);Fire(n);Eq(70f,Get<float>(n,"reach"));FullInk(n,70);
    Check(hits.All(p=>!p.IsAlive())&&beyond.IsAlive()&&side.IsAlive()&&guard.IsAlive());Eq(5,CustomRoleManager.Attempts);
    var samples=VisibleBeamSamples(n,true);var intervals=Enumerable.Range(0,samples.Count/2).Select(i=>(samples[2*i].along,samples[2*i+1].along));var observed=BeamDistanceAudit.Coverage(intervals,.5f,70);
    Check(observed.Complete&&observed.Gaps==0&&MathF.Abs(observed.Max-70)<.005f);
    Check(BeamNames(n).All(s=>Encoding.UTF8.GetByteCount(s)<=800&&ArtGlyphs(s)<=12&&!s.Contains("em>_")));Eq(15,CustomNetObject.AllObjects.Count);
    int updates=CustomNetObject.VisualAttempts;for(int i=0;i<20;i++)Tick(Time.time+.02f,n);Eq(updates,CustomNetObject.VisualAttempts);Eq(5,CustomRoleManager.Attempts);
    Tick(Time.time+1.51f,n);AssertGuideHidden(n);n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
   });
  foreach(float angle in new[]{0f,90f,180f,270f})foreach(var origin in new[]{new Vector2(41,19),new Vector2(-27,-18)})Test($"Airship70 off-map endpoints retain encodable dummy anchors {origin.x}/{origin.y}/{angle}",()=>{
   TownOfHost.Main.NormalOptions.MapId=4;var n=N();n.Player.transform.position=origin;Start(n);Set(n,"angle",angle);Lock(n);Fire(n);FullInk(n,70);
   Check(CustomNetObject.Moves.All(p=>MathF.Abs(p.x)<=50&&MathF.Abs(p.y)<=50));StopRole(n);AssertGuideHidden(n);n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  foreach(string ending in new[]{"finish","cancel","meeting","end","death","destroy","send-failure","layout-failure"})Test("Airship70 cleanup and bounded pool on "+ending,()=>{
   TownOfHost.Main.NormalOptions.MapId=4;var n=N();Start(n);var pool=CustomNetObject.AllObjects.ToArray();Lock(n);
   if(ending=="send-failure")CustomNetObject.ThrowAppearance=true;if(ending=="layout-failure")TMPro.TextMeshPro.ThrowLayout=true;
   Tick(Time.time+1.01f,n);Tick(Time.time+.02f,n);
   switch(ending){case "meeting":n.OnStartMeeting();break;case "end":GameStates.IsInTask=false;Tick(Time.time+.02f,n);break;case "death":n.Player.Data.IsDead=true;n.OnDead(n.Player);break;case "destroy":n.OnDestroy();break;case "cancel":StopRole(n);break;default:Tick(Time.time+1.51f,n);break;}
   AssertGuideHidden(n);Check(CustomNetObject.AllObjects.Count<=15);n.OnDestroy();Tick(Time.time+30,n);Eq(0,CustomNetObject.AllObjects.Count);
  });
  Test("Airship70 repeated shots reuse all fifteen and meeting regenerates once",()=>{
   TownOfHost.Main.NormalOptions.MapId=4;var n=N();Warm(n);var first=CustomNetObject.AllObjects.ToArray();
   for(int i=0;i<3;i++){Start(n);Lock(n);Fire(n);FullInk(n,70);Tick(Time.time+1.51f,n);AssertGuideHidden(n);Check(first.SequenceEqual(CustomNetObject.AllObjects));Tick(Get<float>(n,"readyAt")+.01f,n);}
   n.OnStartMeeting();Eq(0,CustomNetObject.AllObjects.Count);n.AfterMeetingTasks();Warm(n);Eq(15,CustomNetObject.AllObjects.Count);Check(!first.Intersect(CustomNetObject.AllObjects).Any());n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  Test("two Airship70 Nova owners retain independent state and cleanup",()=>{
   TownOfHost.Main.NormalOptions.MapId=4;var a=N(1);var b=N(2);Warm(a,b);Start(a);Start(b);Lock(a);Lock(b);Fire(a);Fire(b);FullInk(a,70);FullInk(b,70);Eq(30,CustomNetObject.AllObjects.Count);
   a.OnDestroy();Eq(15,CustomNetObject.AllObjects.Count);Check(Get<bool>(b,"beaming"));StopRole(b);AssertGuideHidden(b);b.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  Test("firing snapshots selected map range until the next shot",()=>{
   TownOfHost.Main.NormalOptions.MapId=4;var n=N();Start(n);Lock(n);Fire(n);TownOfHost.Main.NormalOptions.MapId=0;Tick(Time.time+.02f,n);Eq(70f,Get<float>(n,"reach"));StopRole(n);n.OnDestroy();
  });
  Test("next normal match cannot inherit Airship70 reach",()=>{TownOfHost.Main.NormalOptions.MapId=4;var a=N();Start(a);Lock(a);Fire(a);a.OnDestroy();Reset();var b=N();Start(b);Lock(b);Fire(b);Eq(50f,Get<float>(b,"reach"));b.OnDestroy();});
  Test("impossible narrow measured glyph cancels Airship70 before damage",()=>{
   TownOfHost.Main.NormalOptions.MapId=4;OptionItem.All[992912].Value=.25f;var n=N();Warm(n);foreach(var t in Get<List<NovaBeamTile>>(n,"tiles")){var m=t.PlayerControl.cosmetics.nameText.font.characterLookupTable['-'].glyph.metrics;m.width=.3f;m.height=.25f;}
   Start(n);var target=P(2,x:60);Lock(n);Tick(Time.time+1.01f,n);Check(!Active(n)&&target.IsAlive());AssertGuideHidden(n);n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  Test("Airship range selection preserves role state wire bytes",()=>{
   var rows=new List<string[]>();foreach(byte map in new byte[]{0,4}){Reset();TownOfHost.Main.NormalOptions.MapId=map;var n=N();Warm(n);Sender.Packets.Clear();Press(n);Lock(n);Fire(n);rows.Add(Sender.Packets.Select(Convert.ToHexString).ToArray());n.OnDestroy();}Check(rows[0].SequenceEqual(rows[1]));
  });
  Test("distance observer uses shared reach for both endpoints",()=>{var s=Source("Development/DebugController/Diagnostics/NovaDistanceDiagnostics.cs");Check(s.Contains("Read<float>(nova, \"reach\")")&&s.Contains("plannedDrawEnd = Vector(hitEnd), hitStart = Vector(hitStart), hitEnd = Vector(hitEnd)"));});
 }
 static void AirshipDiagnostic()
 {
  var rows=new List<object>();
  foreach(var(ox,oy,angle) in new[]{(39.097f,-1.559f,171f),(39.047f,-1.296f,180f),(39.047f,-2.130f,90f)}){
   Reset();TownOfHost.Main.NormalOptions.MapId=4;var f=FontSamples().First(x=>x.Object==304);Resources.Assets["Fonts & Materials/Barlow-Black SDF"]=LoadedFont(f,false);TMPro.TextMeshPro.GeometryPadding=f.Height*.2f;
   var n=N();n.Player.transform.position=new Vector2(ox,oy);Start(n);Set(n,"angle",angle);Lock(n);Fire(n);var points=VisibleBeamSamples(n,true);var c=BeamDistanceAudit.Coverage(Enumerable.Range(0,points.Count/2).Select(i=>(points[2*i].along,points[2*i+1].along)),.5f,70);Check(c.Complete&&c.Gaps==0);var dir=new Vector2(MathF.Cos(angle*Mathf.Deg2Rad),MathF.Sin(angle*Mathf.Deg2Rad));var end=new Vector2(ox,oy)+dir*Get<float>(n,"reach");
   rows.Add(new{kind="SIMULATED_NovaDistance_NOT_REAL_GAME_LOG",mapId=4,anchor=new{x=ox,y=oy},angle,reach=Get<float>(n,"reach"),plannedDrawEnd=new{x=end.x,y=end.y},hitEnd=new{x=end.x,y=end.y},observedMeshMin=c.Min,observedMeshMax=c.Max,observedMeshGaps=c.Gaps,observedMeshComplete=c.Complete,pool=CustomNetObject.AllObjects.Count,beamUsed=BeamNames(n).Count});StopRole(n);AssertGuideHidden(n);n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  }
  Console.WriteLine("AIRSHIP_DIAGNOSTIC "+JsonSerializer.Serialize(rows));
 }
}
