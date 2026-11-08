using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using UnityEngine;
static partial class Program {
 static void LogLayout(Nova n) {
  // Reconstruct the recorded profile. Actual native unit scale was NOT logged;
  // independent X/Y scales below are a deliberate mismatch/fault model.
  var p=Get<NovaBeamTile.RenderProfile>(n,"render");Set(p,"smoothFont","Barlow-Black SDF");Set(p,"barSize",4484);Set(p,"emAt100",.162f);Set(p,"<BarLength>k__BackingField",3.97f);
  TMPro.TextMeshPro.UnitsX=.4f;TMPro.TextMeshPro.UnitsY=.22f;
  foreach(var t in Get<List<NovaBeamTile>>(n,"tiles"))t.PlayerControl.cosmetics.nameText.transform.lossyScale=new(.648f,.648f,1);
 }
 static void LayoutRepro() {
  foreach(float reach in new[]{10f,50f}) {
   Reset();var probe=N();Start(probe);LogLayout(probe);TMPro.TextMeshPro.UnitsX=.25075f;TMPro.TextMeshPro.UnitsY=.25f;Set(probe,"reach",reach);
   bool drawable=(bool)probe.GetType().GetMethod("DrawBeam",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(probe,null);
   Console.WriteLine("UNIT_ERROR_REPRO "+JsonSerializer.Serialize(new{reach,relative_unit_error=.003,drawable}));
  }
  Reset();var n=N(0);Start(n);LogLayout(n);Lock(n);Tick(Time.time+1.01f,n);Tick(Time.time+.02f,n);
  bool fired=Get<bool>(n,"beaming")&&Get<bool>(n,"beamVisible");
  Console.WriteLine("LAYOUT_REPRO "+JsonSerializer.Serialize(new{fired,ready=Get<bool>(n,"TilesReady"),tiles=Get<List<NovaBeamTile>>(n,"tiles").Count,hits=CustomRoleManager.Attempts,oom=false}));
  Environment.ExitCode=fired?0:1;
 }
 static void LayoutTests() {
  foreach(float reach in new[]{10f,50f})Test("0.3 percent native unit mismatch corrected at reach "+reach,()=>{
   var n=N();Start(n);LogLayout(n);TMPro.TextMeshPro.UnitsX=.25075f;TMPro.TextMeshPro.UnitsY=.25f;Set(n,"reach",reach);
   Check((bool)n.GetType().GetMethod("DrawBeam",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,null));CustomNetObject.Tick();Tick(Time.time+.02f,n);
   var samples=VisibleBeamSamples(n,true);Check(MathF.Abs(samples.Max(p=>p.along)-reach)<.005f);StopRole(n);AssertGuideHidden(n);
  });
  Test("recorded font profile survives independent native font-unit scale",()=>{var n=N(0);Start(n);LogLayout(n);Lock(n);Fire(n);var s=VisibleBeamSamples(n,true);Check(s.Max(p=>p.along)>49.995f);Tick(Time.time+1.51f,n);AssertGuideHidden(n);});
  foreach(float scale in new[]{.6f,1f,1.4f})foreach(float angle in new[]{0f,37f,90f,135f,225f,315f})foreach(float reach in new[]{10f,50f})
   Test($"native final mesh independently covers reach={reach} scale={scale} angle={angle}",()=>{
    var n=N();Start(n);TMPro.TextMeshPro.UnitsX=.39f;TMPro.TextMeshPro.UnitsY=.18f;
    foreach(var t in Get<List<NovaBeamTile>>(n,"tiles"))t.PlayerControl.cosmetics.nameText.transform.lossyScale=new(scale,scale,1);
    Set(n,"angle",angle);Set(n,"reach",reach);Check((bool)n.GetType().GetMethod("DrawBeam",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,null));
    CustomNetObject.Tick();Tick(Time.time+.02f,n);
    var samples=VisibleBeamSamples(n,true);Check(samples.Min(p=>p.along)>=.495f&&samples.Max(p=>p.along)>=reach-.005f&&samples.Max(p=>p.along)<=reach+.005f);
    for(int i=2;i<samples.Count;i+=2)Check(samples[i].along<=samples[i-1].along+.005f,"full beam has no gaps");Check(samples.All(p=>p.side<.04f));
    StopRole(n);AssertGuideHidden(n);
   });
  foreach(float bias in new[]{1.2f,1.62f,3.8f})Test("preferred-width estimate differs from native coordinate units "+bias,()=>{
   TMPro.TextMeshPro.PreferredWidth=bias;var n=N();Start(n);Lock(n);Fire(n);Check(VisibleBeamSamples(n,true).Max(p=>p.along)>49.995f);
  });
  Test("final mesh works when character vertex work buffers are stale",()=>{var n=N();Start(n);TMPro.TextMeshPro.StaleWorkingVertices=true;Lock(n);Fire(n);Check(VisibleBeamSamples(n,true).Max(p=>p.along)>49.995f);});
  foreach(int limit in new[]{3,4,6,8,10,12})Test("bounded local split accommodates native per-name glyph limit "+limit,()=>{
   var n=N();Start(n);TMPro.TextMeshPro.GlyphLimit=limit;Lock(n);Fire(n);Check(VisibleBeamSamples(n,true).Max(p=>p.along)>49.995f);
   var art=Get<List<NovaBeamTile>>(n,"tiles").Where(t=>Get<NovaBeamTile.Style>(t,"style")==NovaBeamTile.Style.Beam).Select(t=>Get<string>(t,"beamArt")).ToArray();Check(art.All(s=>Regex.Matches(s,@"em>-").Count<=limit&&Encoding.UTF8.GetByteCount(s)<=800));Check(art.Length<=9);
  });
  foreach(string fault in new[]{"missing-final-mesh","singular-units","nonlinear-multiple-glyphs","glyph-capacity","bad-mesh-oom"})Test("no invisible damage or repeated retry on "+fault,()=>{
   var n=N();Start(n);var p=P(2,x:25);Lock(n);
   if(fault=="missing-final-mesh")TMPro.TextMeshPro.MissingMesh=true;
   if(fault=="singular-units")TMPro.TextMeshPro.DegenerateAxis=true;
   if(fault=="nonlinear-multiple-glyphs")TMPro.TextMeshPro.NonlinearMiddle=true;
   if(fault=="glyph-capacity")TMPro.TextMeshPro.GlyphLimit=1;
   if(fault=="bad-mesh-oom")TMPro.TextMeshPro.ThrowLayout=true;
   Tick(Time.time+1.01f,n);Check(!Active(n)&&p.IsAlive()&&CustomRoleManager.Attempts==0);AssertGuideHidden(n);
   int sends=CustomNetObject.VisualAttempts;for(int i=0;i<20;i++)Tick(Time.time+.02f,n);Eq(sends,CustomNetObject.VisualAttempts);
  });
  Test("native probes never transmit their calibration strings",()=>{
   var n=N();Start(n);Lock(n);CustomNetObject.NameEvents.Clear();Fire(n);
   var final=Get<List<NovaBeamTile>>(n,"tiles").Select(t=>Get<string>(t,"beamArt")).ToHashSet();
   Check(CustomNetObject.NameEvents.Where(e=>e.name.Contains("<nobr>")).All(e=>final.Contains(e.name)));Eq(0,CustomNetObject.RawNameCalls);
  });
  Test("mesh layout does not alter shared RPC geometry or cleanup code",()=>{
   string s=Source("Roles/Impostor/Nova.cs");Check(s.Contains("info.meshInfo[ch.materialReferenceIndex].vertices")&&s.Contains("NOVA_DRAW_ABORT reason=")&&s.Contains("NOVA_DRAW_PLAN reach="));
   Check(s.Contains("Encoding.UTF8.GetByteCount(name) > NetworkSafety.NameByteLimit")&&s.Contains("covered < reach - .005f")&&s.Contains("error > .04f"));
  });
 }
 static void LayoutBench() {
  var rows=new List<object>();
  foreach(float length in new[]{10f,50f})foreach(float width in new[]{.25f,1.25f,5f})foreach(float angle in new[]{0f,45f}) {
   Reset();OptionItem.All[992912].Value=width;var n=N();Warm(n);float prepared=Time.time;int pool=Get<List<NovaBeamTile>>(n,"tiles").Count;
   CustomNetObject.NameEvents.Clear();CustomNetObject.MoveEvents.Clear();Sender.Packets.Clear();Press(n);Set(n,"angle",angle);Lock(n);
   // Execute the production draw path for both ranges without changing its
   // production maximum. Commit/stop uses the normal tile lifecycle.
   Set(n,"reach",length);bool fired=(bool)n.GetType().GetMethod("DrawBeam",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(n,null);CustomNetObject.Tick();Tick(Time.time+.02f,n);
   int active=Get<List<NovaBeamTile>>(n,"tiles").Count(t=>Get<NovaBeamTile.Style>(t,"style")==NovaBeamTile.Style.Beam);
   StopRole(n);AssertGuideHidden(n);
   rows.Add(new{reach=length,width,angle,preferred=false,fired,pool,active,prepared_at=prepared,role_packets=Sender.Packets.Count,name_updates=CustomNetObject.NameEvents.Count,move_updates=CustomNetObject.MoveEvents.Count,name_utf8_bytes=CustomNetObject.NameEvents.Sum(e=>Encoding.UTF8.GetByteCount(e.name)),max_name_utf8_bytes=CustomNetObject.NameEvents.Max(e=>Encoding.UTF8.GetByteCount(e.name)),names=CustomNetObject.NameEvents.Select(e=>new{time=e.time-prepared,index=e.index,name=e.name}).ToArray(),moves=CustomNetObject.MoveEvents.Select(e=>new{time=e.time-prepared,index=e.index,x=e.pos.x,y=e.pos.y}).ToArray()});
  }
  Console.WriteLine("RANGE_BENCH "+JsonSerializer.Serialize(rows));
 }
}
