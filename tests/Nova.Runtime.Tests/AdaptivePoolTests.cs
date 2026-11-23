using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using UnityEngine;
static partial class Program {
 static void ReferenceFont(){var font=FontSamples().First(x=>x.Object==304);Resources.Assets["Fonts & Materials/Barlow-Black SDF"]=LoadedFont(font,false);TMPro.TextMeshPro.GeometryPadding=font.Height*.2f;}
 static void AdaptivePoolTests([CallerFilePath]string file=""){
  Test("IsInTask true during intro cannot queue any Nova dummy",()=>{
   TownOfHost.Main.NormalOptions.MapId=4;GameStates.IsInTask=true;GameStates.Intro=true;GameStates.AfterIntro=false;var n=N();Tick(0,n);Tick(60,n);Eq(0,CustomNetObject.AllObjects.Count);Check(!Get<bool>(n,"tilesSpawned"));Check(!Press(n));n.OnDestroy();
  });
  Test("ended intro flag alone is insufficient before AfterIntro",()=>{
   GameStates.Intro=false;GameStates.AfterIntro=false;var n=N();Tick(0,n);Tick(60,n);Eq(0,CustomNetObject.AllObjects.Count);n.OnDestroy();
  });
  foreach(int people in new[]{1,6,10,12,15})Test("Airship waits for last live participant before delay "+people,()=>{
   ReferenceFont();TownOfHost.Main.NormalOptions.MapId=4;PlayerCatch.All.Clear();var host=P(0,CustomRoles.Nova);PlayerControl.LocalPlayer=host;var n=new Nova(host);for(byte i=1;i<people;i++)P(i);Eq(people,PlayerCatch.All.Count);var late=PlayerCatch.All.Last();PlayerState.GetByPlayerId(late.PlayerId).HasSpawned=false;
   Tick(0,n);Tick(60,n);Eq(0,CustomNetObject.AllObjects.Count);Check(Get<float>(n,"tileSpawnAt")<0);PlayerState.GetByPlayerId(late.PlayerId).HasSpawned=true;Tick(61,n);Tick(64.99f,n);Eq(0,CustomNetObject.AllObjects.Count);Tick(65.01f,n);Tick(65.42f,n);Check(CustomNetObject.AllObjects.Count>=1);Warm(n);Eq(4,CustomNetObject.AllObjects.Count);n.OnDestroy();
  });
  foreach(string skipped in new[]{"dead","disconnected","dummy"})Test("Airship startup ignores non-participating spawn marker "+skipped,()=>{
   TownOfHost.Main.NormalOptions.MapId=4;var n=N();var p=P(skipped=="dummy"?(byte)254:(byte)2);PlayerState.GetByPlayerId(p.PlayerId).HasSpawned=false;if(skipped=="dead")p.Data.IsDead=true;if(skipped=="disconnected")p.Data.Disconnected=true;Warm(n);AssertPool(n);n.OnDestroy();
  });
  Test("Airship incomplete live participant data blocks spawn safely",()=>{TownOfHost.Main.NormalOptions.MapId=4;var n=N();P(2).Data=null;Tick(0,n);Tick(60,n);Eq(0,CustomNetObject.AllObjects.Count);n.OnDestroy();});
  Test("other map startup never depends on Airship-only spawn markers",()=>{var n=N();PlayerState.GetByPlayerId(n.Player.PlayerId).HasSpawned=false;Warm(n);AssertPool(n);n.OnDestroy();});
  using(var old=JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(file),"PoolBaseline.json")))){
   foreach(var row in old.RootElement.GetProperty("rows").EnumerateArray()){
    byte map=row.GetProperty("map").GetByte();float width=row.GetProperty("width").GetSingle(),angle=row.GetProperty("angle").GetSingle();var names=row.GetProperty("names").EnumerateArray().Select(x=>x.GetString()).ToArray();
    Test($"original final wire names unchanged {map}/{width}/{angle}",()=>{TownOfHost.Main.NormalOptions.MapId=map;OptionItem.All[992912].Value=width;var n=N();Start(n);Set(n,"angle",angle);Lock(n);Fire(n);Check(names.SequenceEqual(BeamNames(n)),"original glyph positions/density/bytes changed");FullInk(n,ExpectedMapRange());n.OnDestroy();});
   }
  }
  foreach(byte map in new byte[]{0,1,2,4,5})Test("reference standard width actual minimum including star "+map,()=>{ReferenceFont();TownOfHost.Main.NormalOptions.MapId=map;var n=N();Start(n);Eq(map==4?4:3,CustomNetObject.AllObjects.Count);Lock(n);Fire(n);Eq(CustomNetObject.AllObjects.Count-1,BeamNames(n).Count);FullInk(n,ExpectedMapRange());n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);});
  foreach(byte map in new byte[]{1,4,5})foreach(float width in Enumerable.Range(1,20).Select(x=>x*.25f))foreach(float angle in new[]{0f,17f,45f,90f,137f,225f})Test($"adaptive all widths full mesh {map}/{width}/{angle}",()=>{
   ReferenceFont();TownOfHost.Main.NormalOptions.MapId=map;OptionItem.All[992912].Value=width;var n=N();Start(n);Set(n,"angle",angle);Lock(n);Fire(n);FullInk(n,ExpectedMapRange());AssertPool(n);
   Check(BeamNames(n).All(s=>ArtGlyphs(s)<=12&&Encoding.UTF8.GetByteCount(s)<=800));Tick(Time.time+1.51f,n);AssertGuideHidden(n);n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  foreach(var font in FontSamples())foreach(float width in Enumerable.Range(1,20).Select(x=>x*.25f))foreach(byte map in new byte[]{1,4})Test($"registered font before measurement {font.Object}/{width}/{map}",()=>{
   var asset=LoadedFont(font,false);Resources.Assets["Fonts & Materials/Barlow-Black SDF"]=asset;TownOfHost.Main.NormalOptions.MapId=map;OptionItem.All[992912].Value=width;var n=N();Start(n);Set(n,"angle",37f);Lock(n);Fire(n);FullInk(n,ExpectedMapRange());AssertPool(n);Check(BeamNames(n).All(s=>ArtGlyphs(s)<=12&&Encoding.UTF8.GetByteCount(s)<=800));n.OnDestroy();
  });
  foreach(int players in new[]{1,6,10,12,15})foreach(byte map in new byte[]{1,4,5})foreach(bool allNova in new[]{false,true})Test($"synthetic players and independent pools {players}/{map}/{allNova}",()=>{
   ReferenceFont();TownOfHost.Main.NormalOptions.MapId=map;PlayerCatch.All.Clear();var roles=new List<Nova>();
   for(byte i=0;i<players;i++){var p=P(i,allNova||i==0?CustomRoles.Nova:CustomRoles.Crewmate);if(i==0)PlayerControl.LocalPlayer=p;if(allNova||i==0)roles.Add(new Nova(p));}
   Warm(roles.ToArray());Eq(players,PlayerCatch.All.Count);int total=roles.Sum(n=>Get<List<NovaBeamTile>>(n,"tiles").Count);Eq((map==4?4:3)*roles.Count,total);Eq(total,CustomNetObject.AllObjects.Count);Check(roles.All(n=>Get<bool>(n,"TilesReady")));
   GameStates.IsMeeting=true;foreach(var n in roles)n.OnStartMeeting();Eq(0,CustomNetObject.AllObjects.Count);Tick(Time.time+200,roles.ToArray());Eq(0,CustomNetObject.AllObjects.Count);GameStates.IsMeeting=false;foreach(var n in roles)n.AfterMeetingTasks();Warm(roles.ToArray());Eq(total,CustomNetObject.AllObjects.Count);
   foreach(var n in roles)n.OnDestroy();Tick(Time.time+200);Eq(0,CustomNetObject.AllObjects.Count);Eq(players,PlayerCatch.All.Count);
  });
  Test("only one mesh preflight angle runs per tick across owners",()=>{
   var roles=new[]{N(1),N(2),N(3)};Tick(0,roles);Tick(4.01f,roles);int previous=0;for(int i=0;i<120&&!roles.All(n=>Get<bool>(n,"TilesReady"));i++){Tick(Time.time+.02f,roles);int probes=roles.Sum(n=>Get<int>(n,"poolProbe"));Check(probes-previous<=1);previous=probes;}Eq(roles.Length,previous);foreach(var n in roles)n.OnDestroy();
  });
  Test("preflight never publishes calibration names and no spare beam bodies",()=>{
   ReferenceFont();var n=N();Warm(n);Eq(3,CustomNetObject.AllObjects.Count);Check(CustomNetObject.NameEvents.All(e=>e.name==""));Eq(0,CustomRoleManager.Attempts);n.OnDestroy();
  });
  Test("preflight failure is bounded and retires star without retries",()=>{
   var n=N();Tick(0,n);Tick(4.01f,n);TMPro.TextMeshPro.ThrowLayout=true;Tick(4.42f,n);Tick(4.5f,n);Check(Get<bool>(n,"poolPlanFailed"));Eq(0,CustomNetObject.AllObjects.Count);int calls=TMPro.TextMeshPro.LayoutCalls;
   for(int i=0;i<100;i++)Tick(Time.time+.1f,n);Eq(calls,TMPro.TextMeshPro.LayoutCalls);Check(!Press(n)&&CustomRoleManager.Attempts==0);n.OnDestroy();
  });
  foreach(string stop in new[]{"meeting","death","end","destroy"})Test("growth queued then cancelled without late visible resurrection "+stop,()=>{
   var n=N();Start(n);TMPro.TextMeshPro.GlyphLimit=4;Lock(n);Tick(Time.time+1.01f,n);Check(Get<List<string>>(n,"pendingBeamNames")!=null);Eq(0,CustomRoleManager.Attempts);Check(!Get<bool>(n,"beamVisible"));
   if(stop=="meeting")n.OnStartMeeting();if(stop=="death"){n.Player.Data.IsDead=true;n.OnDead(n.Player);}if(stop=="end"){GameStates.IsInTask=false;Tick(Time.time+.02f,n);}if(stop=="destroy")n.OnDestroy();Tick(Time.time+35,n);AssertGuideHidden(n);n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  Test("late glyph split grows once, waits for visible commit and reuses",()=>{
   var n=N();Start(n);TMPro.TextMeshPro.GlyphLimit=4;var target=P(2,x:25);Lock(n);Tick(Time.time+1.01f,n);Check(target.IsAlive()&&!Get<bool>(n,"beamVisible"));int queued=Get<List<NovaBeamTile>>(n,"tiles").Count;Check(queued<=10);Tick(Time.time+.02f,n);Eq(queued,Get<List<NovaBeamTile>>(n,"tiles").Count);CompletePending(n);Check(!target.IsAlive());Tick(Time.time+1.51f,n);AssertGuideHidden(n);var pool=CustomNetObject.AllObjects.ToArray();Tick(Get<float>(n,"readyAt")+.01f,n);Start(n);Lock(n);Fire(n);Check(pool.SequenceEqual(CustomNetObject.AllObjects));n.OnDestroy();
  });
  Test("growth preparation timeout never damages or retries a shot",()=>{
   var n=N();Start(n);TMPro.TextMeshPro.GlyphLimit=4;Lock(n);Tick(Time.time+1.01f,n);Set(n,"visualDeadline",Time.time);Tick(Time.time+.02f,n);Check(!Active(n));Eq(0,CustomRoleManager.Attempts);Tick(Time.time+40,n);Check(!Active(n));AssertGuideHidden(n);n.OnDestroy();
  });
 }
 static void PoolBench(){
  var rows=new List<object>();foreach(byte map in new byte[]{1,4,5})foreach(float width in new[]{.25f,.5f,1.25f,2.5f,5f}){
   Reset();ReferenceFont();TownOfHost.Main.NormalOptions.MapId=map;OptionItem.All[992912].Value=width;long before=GC.GetAllocatedBytesForCurrentThread();var sw=System.Diagnostics.Stopwatch.StartNew();var n=N();Warm(n);sw.Stop();float prepared=Time.time;long managed=GC.GetAllocatedBytesForCurrentThread()-before;
   int pool=CustomNetObject.AllObjects.Count;Press(n);Lock(n);Fire(n);var names=BeamNames(n);int bytes=names.Max(Encoding.UTF8.GetByteCount),used=names.Count;FullInk(n,ExpectedMapRange());Tick(Time.time+1.51f,n);AssertGuideHidden(n);
   rows.Add(new{map,width,pool,active=used,legacy=NovaVisualPoolRules.Count(map==5,map==4),maxNameBytes=bytes,preparedSimulatedAt=prepared,preparationCpuMs=sw.Elapsed.TotalMilliseconds,managedBytesIncludingFakeTmpAndRegex=managed});n.OnDestroy();
  }Console.WriteLine("POOL_BENCH "+JsonSerializer.Serialize(rows));
 }
}
