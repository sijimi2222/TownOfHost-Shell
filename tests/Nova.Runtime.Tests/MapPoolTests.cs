using System.Text;
using System.Text.Json;
using TownOfHost;
using TownOfHost.Roles.Core;
using TownOfHost.Modules;
using TownOfHost.Roles.Impostor;
using UnityEngine;
static partial class Program {
 static void MapPoolTests(){
  foreach(byte map in new byte[]{0,1,2,3,4,5,6,255})Test("selected map pool "+map,()=>{
   TownOfHost.Main.NormalOptions.MapId=map;var n=N();Warm(n);var tiles=Get<List<NovaBeamTile>>(n,"tiles");Eq(map==5?11:10,tiles.Count);
   Check(tiles.All(t=>t.PlayerControl!=null&&!t.Failed));n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  foreach(byte map in new byte[]{1,5})foreach(float width in new[]{.25f,.5f,1.25f,2.5f,5f})Test($"fixed map pool has full coverage {map}/{width}",()=>{
   TownOfHost.Main.NormalOptions.MapId=map;OptionItem.All[992912].Value=width;var n=N();Start(n);Set(n,"angle",45f);Lock(n);Fire(n);
   Eq(map==5?11:10,CustomNetObject.AllObjects.Count);var raw=VisibleBeamSamples(n,true);
   Check(raw.Min(p=>p.along)<.505f&&raw.Max(p=>p.along)>49.995f);for(int i=2;i<raw.Count;i+=2)Check(raw[i].along<=raw[i-1].along+.005f,"coverage gap");
   Check(CustomNetObject.Names.All(x=>Encoding.UTF8.GetByteCount(x)<=800));
   if(map==5&&width==.25f)Eq(NovaBeamTile.Style.Beam,Get<NovaBeamTile.Style>(Get<List<NovaBeamTile>>(n,"tiles")[10],"style"));
   Tick(Time.time+1.51f,n);AssertGuideHidden(n);n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  foreach(string end in new[]{"charge-cancel","meeting","end","destroy","death","draw-failure","send-failure"})Test("Fungle eleventh cleanup "+end,()=>{
   TownOfHost.Main.NormalOptions.MapId=5;OptionItem.All[992912].Value=.25f;var n=N();Start(n);Eq(11,CustomNetObject.AllObjects.Count);
   if(end!="charge-cancel"){Lock(n);if(end=="draw-failure")TMPro.TextMeshPro.ThrowLayout=true;if(end=="send-failure")CustomNetObject.ThrowAppearance=true;Tick(Time.time+1.01f,n);Tick(Time.time+.02f,n);}
   switch(end){case "meeting":n.OnStartMeeting();break;case "end":GameStates.IsInTask=false;Tick(Time.time+.02f,n);break;case "destroy":n.OnDestroy();break;case "death":n.Player.Data.IsDead=true;n.OnDead(n.Player);break;default:StopRole(n);break;}
   Check(!Active(n));AssertGuideHidden(n);n.OnDestroy();Tick(Time.time+12,n);Eq(0,CustomNetObject.AllObjects.Count);
  });
  Test("Fungle repeated shots reuse all eleven without growth",()=>{
   TownOfHost.Main.NormalOptions.MapId=5;OptionItem.All[992912].Value=.25f;var n=N();Warm(n);var initial=CustomNetObject.AllObjects.ToArray();
   for(int i=0;i<3;i++){Start(n);Lock(n);Fire(n);Tick(Time.time+1.51f,n);AssertGuideHidden(n);Check(initial.SequenceEqual(CustomNetObject.AllObjects));Tick(Get<float>(n,"readyAt")+.01f,n);}n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  Test("Fungle meeting regenerates exactly eleven new valid tiles",()=>{
   TownOfHost.Main.NormalOptions.MapId=5;var n=N();Warm(n);var old=CustomNetObject.AllObjects.ToArray();n.OnStartMeeting();Eq(0,CustomNetObject.AllObjects.Count);n.AfterMeetingTasks();Warm(n);Eq(11,CustomNetObject.AllObjects.Count);Check(!old.Intersect(CustomNetObject.AllObjects).Any());n.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  Test("two Fungle Nova pools isolate eleventh ownership",()=>{
   TownOfHost.Main.NormalOptions.MapId=5;var a=N(1);var b=N(2);Warm(a,b);Eq(22,CustomNetObject.AllObjects.Count);a.OnDestroy();Eq(11,CustomNetObject.AllObjects.Count);Check(Get<bool>(b,"TilesReady"));Start(b);Lock(b);Fire(b);Tick(Time.time+1.51f,b);AssertGuideHidden(b);b.OnDestroy();Eq(0,CustomNetObject.AllObjects.Count);
  });
  Test("Fungle cancel while queued never resurrects eleventh",()=>{TownOfHost.Main.NormalOptions.MapId=5;var n=N();Tick(0,n);Tick(4.01f,n);Tick(4.42f,n);n.OnDestroy();Tick(40,n);Eq(0,CustomNetObject.AllObjects.Count);});
  foreach(float bar in new[]{0f,float.NaN,float.PositiveInfinity,.001f})Test("fixed pool impossible coverage aborts "+bar,()=>Eq(0,NovaVisualPoolRules.GlyphCount(49.5f,bar,108)));
 }
 static void MapBench(){
  var rows=new List<object>();
  foreach(byte map in new byte[]{1,5})foreach(float width in new[]{.25f,1.25f,5f})foreach(float angle in new[]{0f,45f}){
   Reset();TownOfHost.Main.NormalOptions.MapId=map;OptionItem.All[992912].Value=width;var n=N();Warm(n);float prepared=Time.time;var tiles=Get<List<NovaBeamTile>>(n,"tiles");int pool=tiles.Count;
   CustomNetObject.NameEvents.Clear();CustomNetObject.MoveEvents.Clear();Sender.Packets.Clear();
   Press(n);Set(n,"angle",angle);Lock(n);Tick(Time.time+1.01f,n);Tick(Time.time+.02f,n);
   bool fired=Get<bool>(n,"beaming")&&Get<bool>(n,"beamVisible");int active=tiles.Count(t=>Get<NovaBeamTile.Style>(t,"style")==NovaBeamTile.Style.Beam);
   float min=0,max=0;if(fired){var points=VisibleBeamSamples(n,true);min=points.Min(x=>x.along);max=points.Max(x=>x.along);}
   Tick(Time.time+1.51f,n);AssertGuideHidden(n);
   rows.Add(new{map,width,angle,preferred=false,fired,reach=Get<float>(n,"reach"),pool,active,prepared_at=prepared,coverage_min=min,coverage_max=max,name_utf8_bytes=CustomNetObject.NameEvents.Sum(e=>Encoding.UTF8.GetByteCount(e.name)),max_name_utf8_bytes=CustomNetObject.NameEvents.Max(e=>Encoding.UTF8.GetByteCount(e.name)),role_packets=Sender.Packets.Count,names=CustomNetObject.NameEvents.Select(e=>new{time=e.time-prepared,index=e.index,name=e.name}).ToArray(),moves=CustomNetObject.MoveEvents.Select(e=>new{time=e.time-prepared,index=e.index,x=e.pos.x,y=e.pos.y}).ToArray()});
   n.OnDestroy();Check(CustomNetObject.AllObjects.Count==0);
  }
  Console.WriteLine("MAP_BENCH "+JsonSerializer.Serialize(rows));
 }
}
