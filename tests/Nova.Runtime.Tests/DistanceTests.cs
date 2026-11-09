using TownOfHost;
using TownOfHost.Development;
using TownOfHost.Modules;
using TownOfHost.Roles.Impostor;
using UnityEngine;
static partial class Program
{
 static void DistanceTests()
 {
  Test("distance shot log budget is bounded without growing pending roles",()=>{
   var a=new BeamDistanceAudit();var owner=new object();for(int i=0;i<BeamDistanceAudit.ShotsPerGame;i++){Check(a.Begin(owner,1)!=null);a.End(owner);}Eq(0,a.Pending);Check(a.Begin(owner,1)==null);Eq(0,a.Pending);
  });
  Test("distance log budget resets at the next game",()=>{var a=new BeamDistanceAudit();for(int i=0;i<BeamDistanceAudit.ShotsPerGame;i++)a.Begin(new object(),1);a.Game(2);Eq(0,a.Pending);Check(a.Begin(new object(),2)!=null);});
  Test("multiple Nova diagnostics never share shot identity or lifecycle",()=>{var a=new BeamDistanceAudit();var x=new object();var y=new object();var p=a.Begin(x,1);var q=a.Begin(y,1);Check(p.Id!=q.Id);a.End(x);Check(a.Find(x)==null&&ReferenceEquals(q,a.Find(y)));a.End(y);Eq(0,a.Pending);});
  Test("shot snapshot occurs once after the visible state settles",()=>{var a=new BeamDistanceAudit();var s=a.Begin(new object(),1);Check(s.InsideFire&&!s.Planned);s.InsideFire=false;s.Planned=true;Check(s.Planned&&!s.InsideFire);Check(!s.Observe(0,false));Check(!s.Observe(1,true));Check(!s.Observe(1.1f,true));Check(s.Observe(1.13f,true));for(int i=0;i<100;i++)Check(!s.Observe(2+i,true));});
  foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})Test("invalid observation clock does not consume diagnostic "+value,()=>{var s=new BeamDistanceAudit().Begin(new object(),1);Check(!s.Observe(value,true));Check(!s.Observe(1,true));Check(s.Observe(1.13f,true));});
  Test("cancelled pending shot cannot reappear on an update",()=>{var a=new BeamDistanceAudit();var o=new object();a.Begin(o,1);a.End(o);Check(a.Find(o)==null);a.Game(2);Check(a.Find(o)==null);});
  foreach(int total in new[]{10,11,15})Test("distance audit identifies spare slots in selected map pool "+total,()=>{Eq("coverage_plan_complete_spare_slots_parked",BeamDistanceAudit.UnusedReason(total,2,true,true));Eq("all_beam_slots_used",BeamDistanceAudit.UnusedReason(total,total-1,true,true));});
  Test("failed pool cannot be misreported as deliberately unused",()=>{Eq("pool_not_ready_or_retired",BeamDistanceAudit.UnusedReason(15,0,true,false));Eq("not_firing_or_cancelled",BeamDistanceAudit.UnusedReason(15,0,false,true));});
  Test("coverage observes mesh gap without changing hit rules",()=>{var c=BeamDistanceAudit.Coverage(new[]{(.5f,20f),(21f,50f)},.5f,50);Eq(1,c.Gaps);Check(!c.Complete);Check(NovaBeamRules.Contains(25,0,50,1.25f,.5f));});
  Test("coverage observes clipped far end",()=>{var c=BeamDistanceAudit.Coverage(new[]{(.5f,25f),(24f,40f)},.5f,50);Eq(40f,c.Max);Check(!c.Complete);});
  Test("coverage handles unordered overlapping complete segments",()=>{var c=BeamDistanceAudit.Coverage(new[]{(25f,50f),(.5f,26f)},.5f,50);Check(c.Complete&&c.Gaps==0);});
  Test("missing mesh is unknown instead of a fabricated 50m observation",()=>{var c=BeamDistanceAudit.Coverage(Array.Empty<(float,float)>(),.5f,50);Check(float.IsNaN(c.Max)&&!c.Complete);});
  foreach(int pool in new[]{10,11,12,15})Test("actual log glyph width needs twenty glyphs regardless of pool "+pool,()=>{Eq(20,NovaVisualPoolRules.GlyphCount(49.5f,3.589f,(pool-1)*12));});
  foreach(byte map in new byte[]{0,1,2,4,5})Test("linked Nova bundled-font model uses only necessary beam dummies "+map,()=>{
   var font=FontSamples().First(x=>x.Object==304);Resources.Assets["Fonts & Materials/Barlow-Black SDF"]=LoadedFont(font,false);TMPro.TextMeshPro.GeometryPadding=font.Height*.2f;
   TownOfHost.Main.NormalOptions.MapId=map;var n=N();Start(n);Lock(n);Fire(n);var tiles=Get<List<NovaBeamTile>>(n,"tiles");
   int used=tiles.Count(t=>Get<NovaBeamTile.Style>(t,"style")==NovaBeamTile.Style.Beam);Eq(map==4?15:map==5?11:10,tiles.Count);Eq(map==4?3:2,used);
   Check(tiles.Skip(used+1).All(t=>Get<NovaBeamTile.Style>(t,"style")==NovaBeamTile.Style.Hidden));FullInk(n,ExpectedMapRange());StopRole(n);AssertGuideHidden(n);
  });
  Test("larger pool cannot extend a completed range50 render plan",()=>{
   var rows=new List<string[]>();foreach(byte map in new byte[]{1,4}){Reset();TownOfHost.Main.NormalOptions.MapId=map;var n=N();Warm(n);Set(n,"reach",50f);Check((bool)typeof(Nova).GetMethod("DrawBeam",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,null));CustomNetObject.Tick();rows.Add(BeamNames(n).ToArray());n.OnDestroy();}Check(rows[0].SequenceEqual(rows[1]));
  });
  Test("diagnostic bridge cannot mutate render state or send a probe",()=>{
   string s=Source("Development/DebugController/Diagnostics/NovaDistanceDiagnostics.cs");
   foreach(string forbidden in new[]{".ForceMeshUpdate(",".ShowBeam(",".MoveTo(",".Park(",".SetActive(",".SetFloat(",".SetVector(",".StartRpc", "File.Write", "File.Append"})Check(!s.Contains(forbidden));
   Check(s.Contains("text.textInfo")&&s.Contains("renderer.sharedMaterial")&&s.Contains("onDiskSha256 = hash")&&s.Contains("observedMeshComplete")&&s.Contains("unusedBeamSlots"));
  });
 }
}
