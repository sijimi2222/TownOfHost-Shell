using System.Text.RegularExpressions;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
using UnityEngine;
static partial class Program {
 static List<(float along,float side)> VisibleBeamSamples(Nova n,bool smooth) {
  var profile=Get<NovaBeamTile.RenderProfile>(n,"render");
  float a=Get<float>(n,"angle")*Mathf.Deg2Rad;
  var dir=new Vector2(MathF.Cos(a),MathF.Sin(a));var perp=new Vector2(-dir.y,dir.x);
  var anchor=Get<Vector2>(n,"anchor");float tile=profile.TileWorldSize,pixel=tile/NovaBeamTile.Grid;
  var result=new List<(float,float)>();
  foreach(var t in Get<List<NovaBeamTile>>(n,"tiles")) {
   if(Get<NovaBeamTile.Style>(t,"style")!=NovaBeamTile.Style.Beam)continue;
   string art=Get<string>(t,"beamArt");Check(t.Committed,"visible tile must commit before attack");
   if(art.Contains("<nobr>")) {
    // Independent oracle: evaluate the submitted name via the TMP substitute's
    // native-unit mapping and final material mesh, not the production inverse.
    var text=t.PlayerControl.cosmetics.nameText;text.text=art;text.ForceMeshUpdate(true,true);
    var info=text.textInfo;
    for(int i=1;i<info.characterCount-1;i++){
     var ch=info.characterInfo[i];Check(ch.character=='-'&&ch.isVisible);var mesh=info.meshInfo[ch.materialReferenceIndex].vertices;
     var points=Enumerable.Range(0,4).Select(v=>(Vector2)text.transform.TransformPoint(mesh[ch.vertexIndex+v])-anchor).ToArray();
     var center=(points[0]+points[1]+points[2]+points[3])*.25f;
     float side=MathF.Abs(Vector2.Dot(center,perp));result.Add((points.Min(p=>Vector2.Dot(p,dir)),side));result.Add((points.Max(p=>Vector2.Dot(p,dir)),side));
    }
    text.text="";
   } else if(smooth) {
    var center=t.Position+profile.tileOffset+profile.barShift-anchor;float along=Vector2.Dot(center,dir),side=MathF.Abs(Vector2.Dot(center,perp));
    result.Add((along-profile.BarLength*.5f,side));result.Add((along+profile.BarLength*.5f,side));
   } else {
    var rows=Regex.Replace(art,"<[^>]*>","").Split('\n');var center=t.Position+profile.tileOffset-anchor;
    for(int row=0;row<rows.Length;row++)for(int col=0;col<rows[row].Length;col++)if(rows[row][col]=='■'){
     var point=center+new Vector2((col+.5f)*pixel-tile*.5f,tile*.5f-(row+.5f)*pixel);result.Add((Vector2.Dot(point,dir),MathF.Abs(Vector2.Dot(point,perp))));
    }
   }
  }
  Check(result.Count>0,"beam contains drawn samples");return result;
 }
 static void AssertGuideHidden(Nova n) {
  Check(!Active(n));foreach(var t in Get<List<NovaBeamTile>>(n,"tiles")) {
   Check(t.PlayerControl==null||t.PlayerControl.cosmetics.nameText.text=="","no star/beam text after stop");
   Check(t.PlayerControl==null||t.Position.x==50f&&t.Position.y==50f,"visual parked or removed");
  }
 }
 static void WallPenetrationTests() {
  foreach(bool smooth in new[]{false,true})foreach(float angle in new[]{0f,45f,90f,135f,180f,225f,270f,315f})foreach(int walls in new[]{0,1,3})
   Test($"penetrating draw/hit smooth={smooth} angle={angle} walls={walls}",()=>{
    var n=N();Start(n);Set(n,"angle",angle);var profile=Get<NovaBeamTile.RenderProfile>(n,"render");
    if(smooth){Set(profile,"smoothFont","synthetic");Set(profile,"barSize",2000);Set(profile,"<BarLength>k__BackingField",2f);}
    int probes=0;float[] barriers=walls==0?Array.Empty<float>():walls==1?new[]{2.2f}:new[]{1.2f,4.2f,8f};
    PhysicsHelpers.Block=(_,_,length,_)=>{probes++;return barriers.Any(w=>length>=w);};
    var dir=new Vector2(MathF.Cos(angle*Mathf.Deg2Rad),MathF.Sin(angle*Mathf.Deg2Rad));var perp=new Vector2(-dir.y,dir.x);
    PlayerControl At(byte id,float along,float side=0){var p=dir*along+perp*side;return P(id,x:p.x,y:p.y);}
    var hits=new[]{At(2,3),At(3,7),At(4,49.9f)};
    var misses=new[]{At(5,51f),At(6,4,1.5f),At(7,-1),At(8,.3f)};
    Lock(n);Fire(n);Eq(50f,Get<float>(n,"reach"));Eq(0,probes);
    Check(hits.All(p=>!p.IsAlive()),"all three targets including far/wall targets must be hit");
    Check(misses.All(p=>p.IsAlive()),"outside range/width/start must not be hit");Eq(3,CustomRoleManager.Attempts);
    var samples=VisibleBeamSamples(n,smooth);
    Check(samples.All(p=>p.along>=.495f&&p.along<=50.005f&&p.side<=.626f),"render geometry stays inside unchanged beam envelope");
    Check(samples.Min(p=>p.along)<1f&&samples.Max(p=>p.along)>49.4f,"render reaches near both beam endpoints");
    Tick(Time.time+.2f,n);Eq(3,CustomRoleManager.Attempts);Eq(0,probes);
    Tick(Time.time+1.51f,n);AssertGuideHidden(n);
   });
  foreach(string phase in new[]{"charging","waiting","beaming"})foreach(string end in new[]{"meeting","game-end","cancel"})
   Test($"penetrating visual cleanup {phase}/{end}",()=>{
    var n=N();Start(n);PhysicsHelpers.Block=(_,_,_,_)=>true;
    if(phase!="charging")Lock(n);if(phase=="beaming")Fire(n);
    if(end=="meeting")n.OnStartMeeting();else if(end=="game-end"){GameStates.IsInTask=false;Tick(Time.time+.01f,n);}else StopRole(n);
    AssertGuideHidden(n);if(end!="cancel")Eq(0,CustomNetObject.AllObjects.Count);
   });
  Test("two simultaneous penetrating beams keep state and cleanup independent",()=>{
   var a=N(1);var b=N(2);Warm(a,b);Press(a);Press(b);PhysicsHelpers.Block=(_,_,_,_)=>true;
   P(3,x:8);Tick(Time.time+3.01f,a,b);Press(a);Press(b);Tick(Time.time+1.01f,a,b);Tick(Time.time+.02f,a,b);
   Eq(50f,Get<float>(a,"reach"));Eq(50f,Get<float>(b,"reach"));Check(Get<bool>(a,"beaming")&&Get<bool>(b,"beaming"));
   a.OnDestroy();Check(Get<bool>(b,"beaming"));StopRole(b);AssertGuideHidden(b);
  });
  Test("three penetrating shots reuse pool and restore guide each time",()=>{
   var n=N();PhysicsHelpers.Block=(_,_,_,_)=>true;
   for(int i=0;i<3;i++){Start(n);Lock(n);Fire(n);Tick(Time.time+1.51f,n);AssertGuideHidden(n);Tick(Get<float>(n,"readyAt")+.01f,n);}Check(Get<bool>(n,"TilesReady"));
  });
  Test("penetrating render failure still cancels attack and clears guide",()=>{
   var n=N();Start(n);P(2,x:8);Lock(n);PhysicsHelpers.Block=(_,_,_,_)=>true;CustomNetObject.ThrowAppearance=true;
   Tick(Time.time+1.01f,n);Check(!Active(n)&&CustomRoleManager.Attempts==0);AssertGuideHidden(n);
  });
  Test("walls cannot change existing wire state names or move sequence",()=>{
   (string[] names,string[] moves,string[] states) Shot(bool walls){Reset();var n=N();Start(n);PhysicsHelpers.Block=(_,_,_,_)=>walls;Lock(n);Fire(n);return(CustomNetObject.Names.ToArray(),CustomNetObject.Moves.Select(p=>$"{p.x:R},{p.y:R}").ToArray(),Sender.Packets.Select(Convert.ToHexString).ToArray());}
   var a=Shot(false);var b=Shot(true);Check(a.names.SequenceEqual(b.names)&&a.moves.SequenceEqual(b.moves)&&a.states.SequenceEqual(b.states));Eq(0,CustomNetObject.RawNameCalls);
  });
  Test("penetrating Nova retains shared geometric rule and standard host attack path",()=>{
   var s=Source("Roles/Impostor/Nova.cs");Check(!s.Contains("WallDistance(")&&!s.Contains("AnyNonTriggersBetween")&&!s.Contains("Physics2D.Raycast"));
   Check(s.Contains("reach = BeamLength;")&&s.Contains("Contains(along, side, reach, beamSize, BeamStart)")&&s.Contains("OnCheckMurder(Player, target, target, target, true"));
  });
 }
}
