using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using UnityEngine;

static partial class Program
{
 sealed class FontSample
 {
  public string Name { get; set; }
  public int Object { get; set; }
  public float PointSize { get; set; }
  public float Width { get; set; }
  public float Height { get; set; }
  public float WideWidth { get; set; }
  public float WideHeight { get; set; }
  public float AsciiWidth { get; set; }
  public float AsciiHeight { get; set; }
 }
 static FontSample[] FontSamples([CallerFilePath] string source="") => JsonSerializer.Deserialize<FontSample[]>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(source),"BuiltinBeamFontMetrics.json")));
 static void FontFor(Nova n,FontSample sample,bool wide)
 {
  foreach(var tile in Get<List<NovaBeamTile>>(n,"tiles"))
  {
   var font=tile.PlayerControl.cosmetics.nameText.font;
   font.name=sample.Name;font.faceInfo.pointSize=sample.PointSize;
   font.characterLookupTable['-'].glyph.metrics.width=sample.Width;
   font.characterLookupTable['-'].glyph.metrics.height=sample.Height;
   font.characterLookupTable.Remove('\u2014');
   if(wide&&sample.WideWidth>0)font.characterLookupTable['\u2014']=new(){glyph=new(){metrics=new(){width=sample.WideWidth,height=sample.WideHeight,horizontalAdvance=sample.WideWidth}}};
  }
 }
 static int ArtGlyphs(string name)=>Regex.Matches(name,@"em>[-_—]").Count;
 static TMPro.TMP_FontAsset LoadedFont(FontSample sample,bool wide)
 {
  var font=new TMPro.TMP_FontAsset{name=sample.Name};font.faceInfo.pointSize=sample.PointSize;
  font.characterLookupTable['-']=new(){glyph=new(){metrics=new(){width=sample.Width,height=sample.Height}}};
  if(wide&&sample.WideWidth>0)font.characterLookupTable['\u2014']=new(){glyph=new(){metrics=new(){width=sample.WideWidth,height=sample.WideHeight}}};
  if(wide&&sample.AsciiWidth>0)font.characterLookupTable['_']=new(){glyph=new(){metrics=new(){width=sample.AsciiWidth,height=sample.AsciiHeight}}};
  return font;
 }
 static List<string> BeamNames(Nova n)=>Get<List<NovaBeamTile>>(n,"tiles").Where(t=>Get<NovaBeamTile.Style>(t,"style")==NovaBeamTile.Style.Beam).Select(t=>Get<string>(t,"beamArt")).ToList();
 static void FullInk(Nova n,float reach)
 {
  var samples=VisibleBeamSamples(n,true);Check(MathF.Abs(samples.Min(x=>x.along)-.5f)<.005f&&MathF.Abs(samples.Max(x=>x.along)-reach)<.005f,"legacy mesh endpoints");
  for(int i=2;i<samples.Count;i+=2)Check(samples[i].along<=samples[i-1].along+.005f,"legacy mesh has no gaps");
 }
 static void LegacyBeamTests()
 {
  foreach(string mode in new[]{"wide","narrow-only","outline-wide"})Test("legacy Resources order and dash remain unchanged "+mode,()=>{
   var samples=FontSamples();var narrow=samples.First(x=>x.Object==304);var wide=samples.First(x=>x.Name=="Barlow-ExtraBold SDF");
   var black=LoadedFont(narrow,mode=="wide");var extra=LoadedFont(wide,true);if(mode=="outline-wide")extra.material.OutlineWidth=.2f;
   Resources.Assets["Fonts & Materials/Barlow-Black SDF"]=black;if(mode!="narrow-only")Resources.Assets["Fonts & Materials/Barlow-ExtraBold SDF"]=extra;
   var n=N();Start(n);Lock(n);Fire(n);FullInk(n,ExpectedMapRange());var profile=Get<NovaBeamTile.RenderProfile>(n,"render");
   Eq(narrow.Name,Get<string>(profile,"smoothFont"));Eq(1,Resources.LoadCalls);Check(BeamNames(n).All(x=>!x.Contains("em>_")&&!x.Contains("em>—")));StopRole(n);AssertGuideHidden(n);
  });
  foreach(float angle in new[]{0f,37f,90f,135f,225f,315f})foreach(float width in new[]{.25f,5f})Test($"legacy registered font angle={angle} width={width}",()=>{
   var sample=FontSamples().First(x=>x.Object==304);Resources.Assets["Fonts & Materials/Barlow-Black SDF"]=LoadedFont(sample,true);TownOfHost.Main.NormalOptions.MapId=4;OptionItem.All[992912].Value=width;
   var n=N();Start(n);Set(n,"angle",angle);TMPro.TextMeshPro.GeometryPadding=sample.Height*.2f;Lock(n);Fire(n);FullInk(n,ExpectedMapRange());
   Eq(15,CustomNetObject.AllObjects.Count);Eq(1,Resources.LoadCalls);Check(BeamNames(n).All(name=>ArtGlyphs(name)<=12&&Encoding.UTF8.GetByteCount(name)<=800&&!name.Contains("em>_")));StopRole(n);AssertGuideHidden(n);
  });
  foreach(var font in FontSamples())Test("legacy built-in font preserves dash "+font.Name+"/"+font.Object,()=>{
   var n=N();Warm(n);FontFor(n,font,true);Press(n);Lock(n);Fire(n);FullInk(n,ExpectedMapRange());
   Check(BeamNames(n).All(x=>Encoding.UTF8.GetByteCount(x)<=800&&!x.Contains("<scale=")&&!x.Contains("em>—")));StopRole(n);AssertGuideHidden(n);
  });
  var black=FontSamples().First(x=>x.Object==301);
  foreach(byte map in new byte[]{1,4,5})foreach(float width in new[]{.25f,1.25f,5f})foreach(float angle in new[]{0f,45f,90f,225f})foreach(bool padding in new[]{false,true})
   Test($"legacy mesh map={map} width={width} angle={angle} sdf-padding={padding}",()=>{
    TownOfHost.Main.NormalOptions.MapId=map;OptionItem.All[992912].Value=width;
    var n=N();Warm(n);FontFor(n,black,true);TMPro.TextMeshPro.GeometryPadding=padding?black.Height*.2f:0;
    Start(n);Set(n,"angle",angle);Lock(n);Fire(n);FullInk(n,ExpectedMapRange());
    Eq(map==4?15:map==5?11:10,Get<List<NovaBeamTile>>(n,"tiles").Count);
    var names=BeamNames(n);Check(names.All(x=>ArtGlyphs(x)>=1&&ArtGlyphs(x)<=12&&Encoding.UTF8.GetByteCount(x)<=800&&!x.Contains("em>—")));
    int sent=CustomNetObject.VisualAttempts,layouts=TMPro.TextMeshPro.LayoutCalls;
    for(int i=0;i<15;i++)Tick(Time.time+.02f,n);Eq(sent,CustomNetObject.VisualAttempts);Eq(layouts,TMPro.TextMeshPro.LayoutCalls);
    Tick(Time.time+1.51f,n);AssertGuideHidden(n);n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
   });
  foreach(float length in new[]{10f,50f})foreach(float angle in new[]{0f,37f,90f,315f})Test($"legacy mesh calibration range={length} angle={angle}",()=>{
   var n=N();Warm(n);FontFor(n,black,true);TMPro.TextMeshPro.GeometryPadding=black.Height*.2f;Set(n,"angle",angle);Set(n,"reach",length);
   Check((bool)n.GetType().GetMethod("DrawBeam",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(n,null));CustomNetObject.Tick();Tick(Time.time+.02f,n);FullInk(n,length);StopRole(n);AssertGuideHidden(n);
  });
  foreach(string invalid in new[]{"missing","zero","nan","thin","extreme"})Test("unused wide glyph cannot alter legacy dash "+invalid,()=>{
   var n=N();Warm(n);FontFor(n,black,true);
   foreach(var tile in Get<List<NovaBeamTile>>(n,"tiles")){
    var font=tile.PlayerControl.cosmetics.nameText.font;
    if(invalid=="missing")font.characterLookupTable.Remove('\u2014');
    else {var metric=font.characterLookupTable['\u2014'].glyph.metrics;if(invalid=="zero")metric.height=0;if(invalid=="nan")metric.width=float.NaN;if(invalid=="thin")metric.width=1;if(invalid=="extreme")metric.width=10000;}
   }
   Start(n);Lock(n);Fire(n);FullInk(n,ExpectedMapRange());Check(BeamNames(n).All(x=>!x.Contains("em>—")));StopRole(n);AssertGuideHidden(n);
  });
  foreach(string ending in new[]{"cancel","meeting","end","death","destroy","send-error","draw-error"})Test("legacy Airship fifteen cleanup "+ending,()=>{
   TownOfHost.Main.NormalOptions.MapId=4;var n=N();Warm(n);FontFor(n,black,true);Start(n);
   if(ending!="cancel"){Lock(n);if(ending=="send-error")CustomNetObject.ThrowAppearance=true;if(ending=="draw-error")TMPro.TextMeshPro.ThrowLayout=true;Tick(Time.time+1.01f,n);Tick(Time.time+.02f,n);}
   switch(ending){case "meeting":n.OnStartMeeting();break;case "end":GameStates.IsInTask=false;Tick(Time.time+.02f,n);break;case "death":n.Player.Data.IsDead=true;n.OnDead(n.Player);break;case "destroy":n.OnDestroy();break;default:StopRole(n);break;}
   AssertGuideHidden(n);n.OnDestroy();Tick(Time.time+16,n);Eq(0,CustomNetObject.AllObjects.Count);
  });
  foreach(string fault in new[]{"missing-mesh","missing-glyph","nonlinear-middle","layout-throw"})Test("invalid legacy mesh cancels before damage "+fault,()=>{
   var n=N();Warm(n);FontFor(n,black,true);Start(n);var target=P(2,x:25);Lock(n);
   if(fault=="missing-mesh")TMPro.TextMeshPro.MissingMesh=true;if(fault=="missing-glyph")TMPro.TextMeshPro.MissingGlyph=true;if(fault=="nonlinear-middle")TMPro.TextMeshPro.NonlinearMiddle=true;if(fault=="layout-throw")TMPro.TextMeshPro.ThrowLayout=true;
   Tick(Time.time+1.01f,n);Check(!Active(n)&&target.IsAlive());AssertGuideHidden(n);
   int sends=CustomNetObject.VisualAttempts;for(int i=0;i<20;i++)Tick(Time.time+.02f,n);Eq(sends,CustomNetObject.VisualAttempts);
  });
  Test("legacy dash retains wall penetration defense and range50",()=>{
   var n=N();Warm(n);FontFor(n,black,true);Start(n);var a=P(2,x:49);var b=P(3,x:51);var c=P(4,x:25);c.Guarded=true;
   PhysicsHelpers.Block=(_,_,_,_)=>throw new Exception("no wall query expected");Lock(n);Fire(n);FullInk(n,ExpectedMapRange());Check(!a.IsAlive()&&b.IsAlive()&&c.IsAlive());StopRole(n);AssertGuideHidden(n);
  });
  Test("legacy beam repeats without replacing the fixed pool",()=>{
   var n=N();Warm(n);FontFor(n,black,true);var pool=CustomNetObject.AllObjects.ToArray();
   for(int i=0;i<3;i++){Start(n);Lock(n);Fire(n);FullInk(n,ExpectedMapRange());Tick(Time.time+1.51f,n);AssertGuideHidden(n);Check(pool.SequenceEqual(CustomNetObject.AllObjects));Tick(Get<float>(n,"readyAt")+.01f,n);}n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  Test("two legacy Nova instances retain independent profiles",()=>{
   var a=N(1);var b=N(2);Warm(a,b);FontFor(a,black,true);FontFor(b,black,false);Start(a);Start(b);Lock(a);Lock(b);Fire(a);Fire(b);FullInk(a,50);FullInk(b,50);
   Check(BeamNames(a).Concat(BeamNames(b)).All(x=>!x.Contains("em>—")));a.OnDestroy();Check(Get<bool>(b,"TilesReady"));b.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  Test("wide glyph availability cannot change restored wire names",()=>{
   var rows=new List<string[]>();
   foreach(bool wide in new[]{false,true}){Reset();var n=N();Warm(n);FontFor(n,black,wide);Start(n);Lock(n);Fire(n);FullInk(n,ExpectedMapRange());rows.Add(BeamNames(n).ToArray());n.OnDestroy();}
   Check(rows[0].SequenceEqual(rows[1]));
  });
  Test("legacy source preserves verified dash ordering and mesh algorithm",()=>{
   string s=Source("Roles/Impostor/Nova.cs");Check(s.Contains("private const char BarChar = '-';")&&!s.Contains("WideBeamCharacters")&&!s.Contains("SelectBeamCharacter")&&!s.Contains("ch.textElement?.glyph"));
   Check(s.IndexOf("\"Barlow-Black SDF\", \"Barlow-ExtraBold SDF\", \"Barlow-Bold SDF\", \"Barlow-Regular SDF\"")>=0);
  });
  Test("legacy transparent SDF padding remains a separately reported limitation",()=>{
   var n=N();Warm(n);FontFor(n,black,false);TMPro.TextMeshPro.GeometryPadding=black.Height*.2f;Start(n);Lock(n);Fire(n);FullInk(n,ExpectedMapRange());
   var tile=Get<List<NovaBeamTile>>(n,"tiles").Last(t=>Get<NovaBeamTile.Style>(t,"style")==NovaBeamTile.Style.Beam);var text=tile.PlayerControl.cosmetics.nameText;text.text=Get<string>(tile,"beamArt");text.ForceMeshUpdate(true,true);
   int last=text.textInfo.characterCount-2;var ch=text.textInfo.characterInfo[last];var pts=Enumerable.Range(0,4).Select(i=>text.transform.TransformPoint(text.textInfo.InkVertices[ch.vertexIndex+i])).ToArray();
   Check(pts.Max(x=>x.x)-n.Player.transform.position.x<49.995f,"mesh coverage must not be reported as exact opaque ink coverage");StopRole(n);AssertGuideHidden(n);
  });
  foreach(string ending in new[]{"finish","meeting","end","cancel"})Test("Airship fifteenth dummy renders and releases "+ending,()=>{
   TownOfHost.Main.NormalOptions.MapId=4;OptionItem.All[992912].Value=.25f;var n=N();Warm(n);
   foreach(var t in Get<List<NovaBeamTile>>(n,"tiles")){var m=t.PlayerControl.cosmetics.nameText.font.characterLookupTable['-'].glyph.metrics;m.width=.425f;m.height=.25f;}
   Start(n);Lock(n);Fire(n);FullInk(n,ExpectedMapRange());var tiles=Get<List<NovaBeamTile>>(n,"tiles");Eq(15,tiles.Count);Eq(NovaBeamTile.Style.Beam,Get<NovaBeamTile.Style>(tiles[14],"style"));
   Check(BeamNames(n).All(x=>Encoding.UTF8.GetByteCount(x)<=800&&ArtGlyphs(x)<=12));
   if(ending=="meeting")n.OnStartMeeting();else if(ending=="end"){GameStates.IsInTask=false;Tick(Time.time+.02f,n);}else if(ending=="cancel")StopRole(n);else Tick(Time.time+1.51f,n);
   AssertGuideHidden(n);n.OnDestroy();Tick(Time.time+20,n);Eq(0,CustomNetObject.AllObjects.Count);
  });
 }
 static float BeamInkEnd(Nova n)
 {
  float angle=Get<float>(n,"angle")*Mathf.Deg2Rad;var dir=new Vector2(MathF.Cos(angle),MathF.Sin(angle));var anchor=(Vector2)n.Player.transform.position;float end=float.NegativeInfinity;
  foreach(var tile in Get<List<NovaBeamTile>>(n,"tiles").Where(t=>Get<NovaBeamTile.Style>(t,"style")==NovaBeamTile.Style.Beam)){
   var text=tile.PlayerControl.cosmetics.nameText;text.text=Get<string>(tile,"beamArt");text.ForceMeshUpdate(true,true);var info=text.textInfo;
   for(int i=1;i<info.characterCount-1;i++)for(int v=0;v<4;v++)end=MathF.Max(end,Vector2.Dot((Vector2)text.transform.TransformPoint(info.InkVertices[info.characterInfo[i].vertexIndex+v])-anchor,dir));
   text.text="";
  }
  return end;
 }
 static void StretchRepro()
 {
  Reset();
  var n=N();Warm(n);var black=FontSamples().First(x=>x.Object==301);FontFor(n,black,false);TMPro.TextMeshPro.GeometryPadding=black.Height*.2f;Start(n);var target=P(2,x:50);Lock(n);Fire(n);
  float mesh=VisibleBeamSamples(n,true).Max(x=>x.along),visible=BeamInkEnd(n);
  Console.WriteLine("STRETCH_REPRO "+JsonSerializer.Serialize(new{visible_end=visible,mesh_end=mesh,known_legacy_padding_limit=true,attack_reach=Get<float>(n,"reach"),target_at_50_dead=!target.IsAlive(),pool=CustomNetObject.AllObjects.Count}));
  Environment.ExitCode=0;
 }
 static void StretchBench()
 {
  var rows=new List<object>();var black=FontSamples().First(x=>x.Object==304);
  foreach(byte map in new byte[]{1,4,5})foreach(float width in new[]{.25f,1.25f,5f})foreach(float angle in new[]{0f,45f})foreach(bool wide in new[]{false,true}){
   Reset();Resources.Assets["Fonts & Materials/Barlow-Black SDF"]=LoadedFont(black,wide);TownOfHost.Main.NormalOptions.MapId=map;OptionItem.All[992912].Value=width;var n=N();Warm(n);TMPro.TextMeshPro.GeometryPadding=black.Height*.2f;
   var pool=Get<List<NovaBeamTile>>(n,"tiles");float prepared=Time.time;CustomNetObject.NameEvents.Clear();CustomNetObject.MoveEvents.Clear();Sender.Packets.Clear();
   long allocation=GC.GetAllocatedBytesForCurrentThread();int probes=TMPro.TextMeshPro.LayoutCalls;
   Press(n);Set(n,"angle",angle);Lock(n);Fire(n);var names=BeamNames(n);float mesh=VisibleBeamSamples(n,true).Max(x=>x.along),max=BeamInkEnd(n);
   long managedAllocation=GC.GetAllocatedBytesForCurrentThread()-allocation;int layoutCalls=TMPro.TextMeshPro.LayoutCalls-probes;
   int peakName=names.Max(s=>Encoding.UTF8.GetByteCount(s));int symbols=names.Sum(ArtGlyphs);int active=names.Count;
   Tick(Time.time+1.51f,n);AssertGuideHidden(n);
   rows.Add(new{map,width,angle,wide,reach=Get<float>(n,"reach"),fired=Get<bool>(n,"TilesReady"),preferred=wide,uses_wide=names.Any(x=>x.Contains("em>_")),pool=pool.Count,active,symbols,coverage_max=max,mesh_coverage_max=mesh,prepared_at=prepared,name_utf8_bytes=CustomNetObject.NameEvents.Sum(x=>Encoding.UTF8.GetByteCount(x.name)),max_name_utf8_bytes=peakName,layout_calls=layoutCalls,managed_allocation_including_test_renderer=managedAllocation,role_packets=Sender.Packets.Count,names=CustomNetObject.NameEvents.Select(e=>new{time=e.time-prepared,index=e.index,name=e.name}).ToArray(),moves=CustomNetObject.MoveEvents.Select(e=>new{time=e.time-prepared,index=e.index,x=e.pos.x,y=e.pos.y}).ToArray()});
   n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  }
  Console.WriteLine("STRETCH_BENCH "+JsonSerializer.Serialize(rows));
 }
}
