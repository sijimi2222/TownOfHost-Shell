using System.Text.Json;
using System.Text.RegularExpressions;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using UnityEngine;
static partial class Program {
 static void RangeTests(){
  foreach(float width in new[]{.25f,.5f,1.25f,2.5f,5f})foreach(float angle in new[]{0f,30f,45f,90f,135f,180f,225f,270f,315f})
   Test($"range50 coverage width={width} angle={angle}",()=>{
    OptionItem.All[992912].Value=width;var n=N();Start(n);Set(n,"angle",angle);
    var dir=new Vector2(MathF.Cos(angle*Mathf.Deg2Rad),MathF.Sin(angle*Mathf.Deg2Rad));var perp=new Vector2(-dir.y,dir.x);
    PlayerControl At(byte id,float d,float side=0){var p=dir*d+perp*side;return P(id,x:p.x,y:p.y);}
    var hits=new[]{At(2,10),At(3,25),At(4,49),At(5,50)};var miss=At(6,51);var side=At(7,25,width*.5f+.31f);
    int probes=0;PhysicsHelpers.Block=(_,_,_,_)=>{probes++;return true;};Lock(n);Fire(n);
    Check(hits.All(p=>!p.IsAlive())&&miss.IsAlive()&&side.IsAlive());Eq(4,CustomRoleManager.Attempts);Eq(0,probes);Eq(50f,Get<float>(n,"reach"));
    var samples=VisibleBeamSamples(n,true).OrderBy(p=>p.along).ToArray();
    Check(samples[0].along>=.495f&&samples[0].along<=.505f&&samples[^1].along>=49.995f&&samples[^1].along<=50.005f);
    var intervals=samples.Chunk(2).ToArray(); // Full continuity is checked in glyph order below.
    var raw=VisibleBeamSamples(n,true);for(int i=2;i<raw.Count;i+=2)Check(raw[i].along<=raw[i-1].along+.005f,"gap between glyphs");
    Check(samples.All(p=>p.side<=.005f));Check(CustomNetObject.AllObjects.Count<=17);Check(CustomNetObject.Names.All(x=>System.Text.Encoding.UTF8.GetByteCount(x)<=800));
    Check(Get<List<NovaBeamTile>>(n,"tiles").Where(t=>Get<NovaBeamTile.Style>(t,"style")==NovaBeamTile.Style.Beam).All(t=>Vector2.Distance(t.Position,Get<Vector2>(n,"anchor"))<.02f));
    int names=CustomNetObject.VisualAttempts,moves=CustomNetObject.Moves.Count;for(int i=0;i<10;i++)Tick(Time.time+.02f,n);Eq(names,CustomNetObject.VisualAttempts);Eq(moves,CustomNetObject.Moves.Count);
    Tick(Time.time+1.51f,n);AssertGuideHidden(n);
   });
  foreach(var origin in new[]{new Vector2(45,45),new Vector2(-45,-45),new Vector2(40,-40)})foreach(float angle in new[]{0f,45f,180f,270f})
   Test($"out-of-map artwork keeps body in network range {origin.x},{origin.y}/{angle}",()=>{
    var n=N();n.Player.transform.position=origin;Start(n);Set(n,"angle",angle);Lock(n);Fire(n);
    Check(CustomNetObject.MoveEvents.All(e=>Math.Abs(e.pos.x)<=50&&Math.Abs(e.pos.y)<=50));
    Check(VisibleBeamSamples(n,true).Max(p=>p.along)>49.99f);Tick(Time.time+1.51f,n);AssertGuideHidden(n);
   });
  foreach(string fault in new[]{"layout-oom","far-mismatch","missing-glyph","missing-font","invalid-anchor","over-budget-name"})
   Test("range50 fails closed on "+fault,()=>{
    var n=N();Start(n);Lock(n);var profile=Get<NovaBeamTile.RenderProfile>(n,"render");
    if(fault=="layout-oom")TMPro.TextMeshPro.ThrowLayout=true;
    if(fault=="far-mismatch")TMPro.TextMeshPro.WrongFar=true;
    if(fault=="missing-glyph")TMPro.TextMeshPro.MissingGlyph=true;
    if(fault=="missing-font")foreach(var t in Get<List<NovaBeamTile>>(n,"tiles"))t.PlayerControl.cosmetics.nameText.font=null;
    if(fault=="invalid-anchor")Set(n,"anchor",new Vector2(51,0));
    if(fault=="over-budget-name"){Set(profile,"smoothFont",new string('x',900));Set(profile,"barSize",2000);}
    Tick(Time.time+1.01f,n);Check(!Active(n)&&CustomRoleManager.Attempts==0);AssertGuideHidden(n);
    int attempts=CustomNetObject.VisualAttempts;for(int i=0;i<20;i++)Tick(Time.time+.02f,n);Eq(attempts,CustomNetObject.VisualAttempts);
   });
  Test("packed name is published after its body position",()=>{
   var n=N();Start(n);Lock(n);CustomNetObject.EventOrder.Clear();Fire(n);var pool=Get<List<NovaBeamTile>>(n,"tiles");
   for(int i=1;i<pool.Count;i++)if(Get<NovaBeamTile.Style>(pool[i],"style")==NovaBeamTile.Style.Beam){int moved=CustomNetObject.EventOrder.IndexOf("M:"+i),named=CustomNetObject.EventOrder.IndexOf("N:"+i);Check(moved>=0&&named>moved);}
  });
  Test("exhausted movement budget cannot publish packed art at parked coordinates",()=>{
   var n=N();Start(n);var target=P(2,x:25);Lock(n);while(NovaVisualTraffic.Take(AmongUsClient.Instance.GameId,Time.time)){}CustomNetObject.Names.Clear();
   Tick(Time.time+1.01f,n);Tick(Time.time+.02f,n);Check(CustomNetObject.Names.All(x=>!x.Contains("<nobr>"))&&target.IsAlive()&&CustomRoleManager.Attempts==0);
   Tick(Time.time+2.01f,n);Check(!Active(n));Tick(Time.time+6.01f,n);AssertGuideHidden(n);
  });
  Test("range50 boundary tolerance only covers floating-point projection",()=>{Check(NovaBeamRules.Contains(50.000004f,0,50,1.25f,.5f));Check(!NovaBeamRules.Contains(50.001f,0,50,1.25f,.5f));Check(!NovaBeamRules.Contains(51,0,50,1.25f,.5f));});
  Test("packed glyphs use fixed object and byte limits",()=>{
   var n=N();Start(n);Lock(n);Fire(n);var tiles=Get<List<NovaBeamTile>>(n,"tiles");Check(tiles.Count<=17);
   foreach(var t in tiles.Where(t=>Get<NovaBeamTile.Style>(t,"style")==NovaBeamTile.Style.Beam)){
    var art=Get<string>(t,"beamArt");int glyphs=Regex.Matches(art,@"em>-").Count;Check(glyphs>=1&&glyphs<=12);Check(art.Contains("<nobr>")&&art.Contains("<alpha=#00>"));
   }
  });
 }
 static void RangeBench(){
  var rows=new List<object>();
  foreach(float width in new[]{.25f,1.25f,5f})foreach(float angle in new[]{0f,45f})foreach(bool preferred in new[]{false,true}){
   Reset();OptionItem.All[992912].Value=width;var n=N();var profile=Get<NovaBeamTile.RenderProfile>(n,"render");
   if(preferred){Set(profile,"smoothFont","synthetic");Set(profile,"barSize",(int)MathF.Round(width*1600));Set(profile,"<BarLength>k__BackingField",width*2.272f*1.4f);}
   Warm(n);float prepared=Time.time;int pool=Get<List<NovaBeamTile>>(n,"tiles").Count;CustomNetObject.NameEvents.Clear();CustomNetObject.MoveEvents.Clear();CustomNetObject.Names.Clear();CustomNetObject.Moves.Clear();Sender.Packets.Clear();
   Press(n);Set(n,"angle",angle);Lock(n);Tick(Time.time+1.01f,n);Tick(Time.time+.02f,n);bool fired=Get<bool>(n,"beaming")&&Get<bool>(n,"beamVisible");int active=Get<List<NovaBeamTile>>(n,"tiles").Count(t=>Get<NovaBeamTile.Style>(t,"style")==NovaBeamTile.Style.Beam);
   Tick(Time.time+1.51f,n);AssertGuideHidden(n);
   rows.Add(new{width,angle,preferred,fired,reach=Get<float>(n,"reach"),pool,active,prepared_at=prepared,name_updates=CustomNetObject.NameEvents.Count,move_updates=CustomNetObject.MoveEvents.Count,name_utf8_bytes=CustomNetObject.NameEvents.Sum(e=>System.Text.Encoding.UTF8.GetByteCount(e.name)),max_name_utf8_bytes=CustomNetObject.NameEvents.Max(e=>System.Text.Encoding.UTF8.GetByteCount(e.name)),role_packets=Sender.Packets.Count,names=CustomNetObject.NameEvents.Select(e=>new{time=e.time-prepared,index=e.index,name=e.name}).ToArray(),moves=CustomNetObject.MoveEvents.Select(e=>new{time=e.time-prepared,index=e.index,x=e.pos.x,y=e.pos.y}).ToArray()});
  }
  Console.WriteLine("RANGE_BENCH "+JsonSerializer.Serialize(rows));
 }
}
