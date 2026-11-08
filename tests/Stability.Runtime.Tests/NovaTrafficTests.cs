using System;using System.Linq;using System.Collections.Generic;using System.Reflection;using System.Text.Json;using TownOfHost;using TownOfHost.Modules;using Hazel;
partial class Program {
 class TrafficDummy:CustomNetObject {
  public bool Allowed=true;public bool Pool;public int Created,Failed;public void Spawn()=>CreateNetObject(new(50,50));
  public void Visual(string name)=>SetVisualName(name);public void Move(UnityEngine.Vector2 p)=>SnapToPosition(p);
  protected override bool CanCreate=>Allowed;
#if !BASELINE_TRAFFIC
  protected override void OnCreateFailed(){Failed++;}
  protected override bool BatchSpawnAssignments=>Pool;
  protected override float SpawnInterval=>Pool?1f:.4f;
#endif
  protected override void OnCreated(){Created++;SetAppearance(0,displayName:"");}
 }
 static double clock;
 static void Peers(int count){for(int i=1;i<=count;i++)PlayerCatch.AllPlayerControls.Add(new(){PlayerId=(byte)i,OwnerId=100+i});}
 static void Advance(float seconds){for(int i=0;i<(int)Math.Ceiling(seconds/.02f);i++){clock+=.02;LateTask.Update(.02f);}}
 static (List<double> times,List<string[]> packets,List<TrafficDummy> pool) Prepare(int tiles,int peers,bool pool){Reset();clock=0;Peers(peers);var ts=new List<double>();AmongUsClient.BeforeSend=()=>ts.Add(clock);var ds=new List<TrafficDummy>();for(int i=0;i<tiles;i++){var d=new TrafficDummy{Pool=pool};ds.Add(d);d.Spawn();}Advance(tiles*1.1f+1);Check(ds.All(d=>d.Created==1));return(ts,AmongUsClient.Packets.ToList(),ds);}
 static int Peak(List<double> ts,double window=10)=>ts.Count==0?0:ts.Max(t=>ts.Count(x=>x>=t&&x<t+window));
 static IEnumerable<string> Assignments(List<string[]> packets)=>packets.Where(p=>p.Contains("start:6")).SelectMany(p=>p);
 static void TrafficBench(){var rows=new List<object>();foreach(int peers in new[]{0,1,7,14})foreach(int tiles in new[]{10,17,34})foreach(bool batching in new[]{false,true}){var r=Prepare(tiles,peers,batching);rows.Add(new{tiles,peers,opt_in=batching,send_calls=r.times.Count,peak_ten_seconds=Peak(r.times),completed_at=r.times.LastOrDefault(),assignment_envelopes=r.packets.Sum(p=>p.Count(s=>s=="start:6")),assign_packets=r.packets.Count(p=>p.Contains("start:6"))});}Console.WriteLine(JsonSerializer.Serialize(rows));}
 static void NovaTrafficTests(){
 Test("Nova CNO opts into batching and one-second creation spacing",()=>{var path=System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(SourcePath()),"../../Roles/Impostor/Nova.cs"));var s=System.IO.File.ReadAllText(path);Check(s.Contains("protected override bool BatchSpawnAssignments => true;")&&s.Contains("protected override float SpawnInterval => 1f;"));});
 foreach(int peers in new[]{0,1,7,8,9,14,30})Test("Nova assignment targets and RPC bytes preserved for "+peers+" peers",()=>{var a=Prepare(1,peers,false);var b=Prepare(1,peers,true);Check(Assignments(a.packets).SequenceEqual(Assignments(b.packets)));Check(b.packets.Where(p=>p.Contains("start:6")).All(p=>p.Count(s=>s=="start:6")<=8));Check(b.packets.Sum(p=>p.Count(s=>s=="start:6"))==peers);});
 foreach(int count in new[]{10,17,34})Test("Nova preparation reduces packet peak and total for "+count+" tiles",()=>{var a=Prepare(count,14,false);var b=Prepare(count,14,true);Check(b.times.Count*4==a.times.Count);Check(Peak(b.times)<=44&&Peak(a.times)>100);Check(b.pool.Count==CustomNetObject.AllObjects.Count);});
 Test("Nova spacing holds shared queue until one second",()=>{Reset();var a=new TrafficDummy{Pool=true};var b=new TrafficDummy{Pool=true};a.Spawn();b.Spawn();Advance(.4f);Check(a.Created==1&&b.PlayerControl==null);Advance(.4f);Check(b.PlayerControl==null);Advance(.3f);Check(b.PlayerControl!=null);});
 Test("legacy consumers retain immediate next spawn after creation",()=>{Reset();var a=new TrafficDummy();var b=new TrafficDummy();a.Spawn();b.Spawn();Advance(.42f);Check(a.Created==1&&b.PlayerControl!=null);});
 Test("Nova meeting cancellation does not create queued pool",()=>{Reset();Peers(14);var a=new TrafficDummy{Pool=true};var b=new TrafficDummy{Pool=true};a.Spawn();b.Spawn();Advance(.42f);a.Allowed=b.Allowed=false;a.Despawn();b.Despawn();int n=AmongUsClient.Sends;Advance(2);Check(b.PlayerControl==null&&CustomNetObject.AllObjects.Count==0&&AmongUsClient.Sends==n);});
 Test("stale Nova pacing callback cannot unlock a new session queue",()=>{Reset();var a=new TrafficDummy{Pool=true};a.Spawn();Advance(.42f);var old=LateTask.Tasks.Single(t=>t.name=="CNO.SpawnNext");CustomNetObject.ResetSpawnState();var b=new TrafficDummy{Pool=true};var c=new TrafficDummy{Pool=true};b.Spawn();c.Spawn();old.Run(1);Check(c.PlayerControl==null);});
 Test("incomplete Nova assignment cannot commit a ready visual",()=>{Reset();Peers(14);var a=new TrafficDummy{Pool=true};a.Spawn();AmongUsClient.ThrowSend=true;Advance(.12f);AmongUsClient.ThrowSend=false;Advance(.4f);Check(a.Created==0&&a.Failed==1&&!CustomNetObject.AllObjects.Contains(a));});
 Test("host disappearance during assignment fails closed for Nova",()=>{Reset();var a=new TrafficDummy{Pool=true};a.Spawn();PlayerControl.LocalPlayer=null;Advance(.5f);Check(a.Created==0&&a.Failed==1&&!CustomNetObject.AllObjects.Contains(a));});
 Test("failed despawn still removes host dummy and destroys only its own object",()=>{Reset();var a=new TrafficDummy{Pool=true};var other=new TrafficDummy();a.Spawn();other.Spawn();Advance(2);var pc=a.PlayerControl;var keep=other.PlayerControl;AmongUsClient.ThrowSend=true;a.Despawn();Check(!CustomNetObject.AllObjects.Contains(a)&&CustomNetObject.AllObjects.Contains(other)&&pc.gameObject.Destroyed&&!keep.gameObject.Destroyed);Check(MessageWriter.Created.All(w=>w.Recycles==1));});
 Test("failed preparation and failed despawn cannot leave a local visual registered",()=>{Reset();Peers(14);var a=new TrafficDummy{Pool=true};a.Spawn();AmongUsClient.ThrowSend=true;Advance(.6f);Check(a.Created==0&&a.Failed==1&&!CustomNetObject.AllObjects.Contains(a)&&a.PlayerControl.gameObject.Destroyed);});
 Test("legacy dummy is not blocked by the Nova cosmetic cooldown",()=>{Reset();var a=new TrafficDummy{Pool=true};var b=new TrafficDummy{Pool=true};a.Spawn();b.Spawn();Advance(.42f);var legacy=new TrafficDummy();legacy.Spawn();Check(legacy.PlayerControl!=null&&b.PlayerControl==null);Advance(.4f);Check(legacy.Created==1&&b.PlayerControl==null);});
 Test("queued legacy effect precedes remaining Nova preparation",()=>{Reset();var a=new TrafficDummy{Pool=true};var b=new TrafficDummy{Pool=true};var legacy=new TrafficDummy();a.Spawn();b.Spawn();legacy.Spawn();Advance(.42f);Check(legacy.PlayerControl!=null&&b.PlayerControl==null);});
 Test("Nova pending batch writer is recycled after transport failure",()=>{Reset();Peers(14);var a=new TrafficDummy{Pool=true};a.Spawn();AmongUsClient.ThrowSend=true;Advance(.12f);Check(MessageWriter.Created.All(w=>w.Recycles==1));});
 Test("Nova batch skips disconnected peers without affecting remaining destinations",()=>{Reset();Peers(14);PlayerCatch.AllPlayerControls[1].Data.Disconnected=true;PlayerCatch.AllPlayerControls[5].Data=null;var a=new TrafficDummy{Pool=true};a.Spawn();Advance(.12f);Check(AmongUsClient.Packets.Sum(p=>p.Count(s=>s=="start:6"))==12);});
 }
 static string SourcePath([System.Runtime.CompilerServices.CallerFilePath]string f="")=>f;
}
