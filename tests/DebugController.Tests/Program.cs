using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ShellDebugController.Shared;
using TownOfHost.Development;
using UnityEngine;
internal static partial class Program
{
 static int passed,failed;
 static void Check(bool value,string why="assertion"){if(!value)throw new Exception(why);}
 static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+" "+e);}}
 static void Setup(){NoClipMovement.Restore();AmongUsClient.Instance=new();TownOfHost.GameStates.IsInTask=true;TownOfHost.GameStates.IsMeeting=TownOfHost.GameStates.CalledMeeting=false;MeetingHud.Instance=null;Physics2D.Results=_=>Array.Empty<Collider2D>();}
 static ControlRequest On(long seq=1)=>new(){Sequence=seq,Debug=true,Zoom=true,NoClip=true,Telemetry=true,Simulation=true};
 static void Main(string[] args)
 {
  for(int mask=0;mask<512;mask++){int m=mask;Test("eligibility matrix "+m,()=>{bool B(int bit)=>(m&(1<<bit))!=0;var p=new DebugEligibility(B(0),B(1),B(2),B(3),B(4),B(5),B(6),B(7),B(8));bool living=(m&127)==127;Check(p.LivingHost==living);Check(p.Movement==(living&&B(7)&&!B(8)));});}
  for(int mask=0;mask<32;mask++){int m=mask;Test("master sanitizes all flags "+m,()=>{bool B(int b)=>(m&(1<<b))!=0;var r=new ControlRequest{Debug=B(0),Zoom=B(1),NoClip=B(2),Telemetry=B(3),Simulation=B(4)}.Sanitized();Check(r.Zoom==(B(0)&&B(1))&&r.NoClip==(B(0)&&B(2))&&r.Telemetry==(B(0)&&B(3))&&r.Simulation==(B(0)&&B(4)));});}
  Test("lease default off",()=>Check(!new ControlLease().Read(0).Debug));
  Test("lease expiry exact boundary",()=>{var l=new ControlLease();Check(l.Accept(On(),10));Check(l.Read(12.999).NoClip);Check(!l.Read(13).Debug);});
  Test("disconnect closes every permission",()=>{var l=new ControlLease();l.Accept(On(),0);l.Close();Check(!l.Read(.1).Debug);});
  Test("old sequence rejected without renewing lease",()=>{var l=new ControlLease();l.Accept(On(2),0);Check(!l.Accept(On(1),2.9));Check(!l.Read(3).Debug);});
  Test("duplicate cannot modify flags",()=>{var l=new ControlLease();l.Accept(new(){Sequence=2},0);Check(!l.Accept(On(2),.1)&&!l.Read(.2).Debug);});
  Test("wrong protocol rejected",()=>{var l=new ControlLease();var r=On();r.Protocol=2;Check(!l.Accept(r,0));});
  Test("stale session epoch denied",()=>{var l=new ControlLease();l.Accept(On(),0);Check(l.Read(.1,0).NoClip&&!l.Read(.1,1).NoClip);});
  foreach(double now in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity,-1}){double n=now;Test("invalid lease clock "+n,()=>{var l=new ControlLease();Check(!l.Accept(On(),n));l.Accept(On(),0);Check(!l.Read(n).Debug);});}
  Test("backwards lease clock fails closed",()=>{var l=new ControlLease();l.Accept(On(),10);Check(!l.Read(9).Debug);});
  Test("raw counters use observed lengths",()=>{var l=new TrafficLedger();l.Raw(100,.2);l.Raw(220,9.9);l.Error();var s=new DebugSnapshot();l.Fill(s,10.1);Check(s.RawSendCount==2&&s.RawSendBytes==320&&s.LastTenSeconds==2&&s.SendErrors==1);l.Fill(s,10.2);Check(s.LastTenSeconds==1);});
  Test("raw window expires without new traffic",()=>{var l=new TrafficLedger();l.Raw(10,0);var s=new DebugSnapshot();l.Fill(s,10);Check(s.LastTenSeconds==0&&s.RawSendCount==1);});
  Test("raw queue bounded and overflow labeled",()=>{var l=new TrafficLedger();for(int i=0;i<TrafficLedger.MaximumRecentEvents+20;i++)l.Raw(1,1);var s=new DebugSnapshot();l.Fill(s,1.1);Check(s.LastTenSeconds==TrafficLedger.MaximumRecentEvents&&s.TenSecondWindowTruncated&&s.RawSendCount==TrafficLedger.MaximumRecentEvents+20);l.Fill(s,12);Check(!s.TenSecondWindowTruncated&&s.LastTenSeconds==0);});
  Test("negative lengths ignored",()=>{var l=new TrafficLedger();l.Raw(-1,1);l.Request("beam",-1);var s=new DebugSnapshot();l.Fill(s,2);Check(s.RawSendCount==0&&s.NovaTraffic.All(p=>p.Messages==0));});
  Test("phase bytes and name bytes remain separate",()=>{var l=new TrafficLedger();l.Request("spawn",123);l.Request("hide",222);l.Request("beam",800);l.Name(593);var s=new DebugSnapshot();l.Fill(s,1);Check(s.ObservedSpawnBytes==123&&s.ObservedEmptyNameBytes==222&&s.MaximumNameBytes==593&&s.RawSendCount==0);});
  Test("unknown phase cannot allocate state",()=>{var l=new TrafficLedger();for(int i=0;i<1000;i++)l.Request("untrusted-"+i,10);var s=new DebugSnapshot();l.Fill(s,1);Check(s.NovaTraffic.Length==7&&s.NovaTraffic.All(p=>p.Messages==0));});
  Test("ledger clears without retaining payloads",()=>{var l=new TrafficLedger();l.Raw(4,0);l.Name(50);l.Clear();var s=new DebugSnapshot();l.Fill(s,1);Check(s.RawSendCount==0&&s.MaximumNameBytes==0&&s.RecentSeconds.All(x=>x==0));});
  foreach(int pool in new[]{2,10,17})foreach(int beam in new[]{1,2,5}){int p=pool,b=beam;if(b>=p)continue;Test($"fifteen-player model {p}/{b}",()=>{var r=TrafficSimulator.Run(new(){Dummies=p,BeamDummies=b});Check(r.Phases[0].Messages==p*4&&r.Phases[1].Messages==b*2+2&&r.Phases[2].Messages==b*2);Check(r.Phases.Sum(x=>x.Bytes)>0&&r.Seconds.Sum()==r.Phases.Sum(x=>x.Messages));});}
  Test("15-player assignment batches preserved",()=>Check((int)Math.Ceiling(TrafficSimulator.RemoteParticipants/(double)TrafficSimulator.AssignmentBatch)==2));
  Test("800 byte name boundary",()=>{Check(!TrafficSimulator.Run(new(){NameBytes=800}).Warnings.Any(x=>x.Contains("表示名800")));Check(TrafficSimulator.Run(new(){NameBytes=801}).Warnings.Any(x=>x.Contains("表示名800")));});
  Test("oversized packet not assumed split",()=>Check(TrafficSimulator.Run(new(){SpawnBytes=1001}).Warnings.Any(x=>x.Contains("1000B"))));
  Test("measured and assumed origins explicitly different",()=>{var a=TrafficSimulator.Run(new());var b=TrafficSimulator.Run(new(){SpawnMeasured=true,EmptyNameMeasured=true});Check(a.Warnings.Any(x=>x.Contains("未実測"))&&!b.Warnings.Any(x=>x.Contains("未実測")));});
  Test("model invalid counts rejected",()=>{try{TrafficSimulator.Run(new(){Dummies=1});throw new Exception("accepted");}catch(ArgumentOutOfRangeException){}});
  NoClipTests();FramingTests();MapPoolTests();
  if(args.Contains("--windows-pipe")){PipeTests();ServerTests();}
  Console.WriteLine($"RESULT {passed} passed, {failed} failed");Environment.ExitCode=failed==0?0:1;
 }
 static void NoClipTests()
 {
  foreach(string state in new[]{"public","guest","not-owner","dead","local","ended","meeting","called-meeting","vent","ladder","platform","frozen","off"}){string key=state;Test("noclip rejects "+key,()=>{Setup();var p=new PlayerControl();var c=AmongUsClient.Instance;switch(key){case "public":c.IsGamePublic=true;break;case "guest":c.AmHost=false;break;case "not-owner":p.AmOwner=false;break;case "dead":p.Alive=false;break;case "local":c.NetworkMode=NetworkModes.LocalGame;break;case "ended":c.GameState=AmongUsClient.GameStates.Ended;break;case "meeting":TownOfHost.GameStates.IsMeeting=true;break;case "called-meeting":TownOfHost.GameStates.CalledMeeting=true;break;case "vent":p.inVent=true;break;case "ladder":p.onLadder=true;break;case "platform":p.inMovingPlat=true;break;case "frozen":p.CanMove=false;break;}NoClipMovement.Update(p,c,key!="off");Check(p.Collider.enabled&&!NoClipMovement.Active);});}
  Test("noclip only modifies local collider",()=>{Setup();var p=new PlayerControl();var other=new PlayerControl();NoClipMovement.Update(p,AmongUsClient.Instance,true);Check(!p.Collider.enabled&&other.Collider.enabled);NoClipMovement.Restore();Check(p.Collider.enabled);});
  Test("movement stop at clear point keeps position",()=>{Setup();var p=new PlayerControl();NoClipMovement.Update(p,AmongUsClient.Instance,true);p.Position=new(3,0);NoClipMovement.Update(p,AmongUsClient.Instance,true);p.MyPhysics.body.velocity=new();NoClipMovement.Update(p,AmongUsClient.Instance,true);Check(p.Collider.enabled&&p.Position.x==3&&p.NetTransform.RpcCalls==0);});
  Test("stop inside wall returns last safe point",()=>{Setup();var p=new PlayerControl();var wall=new Collider2D();Physics2D.Results=v=>v.x>1?new[]{wall}:Array.Empty<Collider2D>();NoClipMovement.Update(p,AmongUsClient.Instance,true);p.Position=new(2,0);NoClipMovement.Update(p,AmongUsClient.Instance,true);p.MyPhysics.body.velocity=new();NoClipMovement.Update(p,AmongUsClient.Instance,true);Check(p.Collider.enabled&&p.Position.x==0&&p.NetTransform.RpcCalls==1);});
  Test("failed correction still restores collider",()=>{Setup();var p=new PlayerControl();NoClipMovement.Update(p,AmongUsClient.Instance,true);Physics2D.Results=_=>new[]{new Collider2D()};p.Position=new(2,0);p.NetTransform.ThrowRpc=true;NoClipMovement.Restore();Check(p.Collider.enabled&&p.Position.x==0&&p.NetTransform.LocalCalls==1);});
  Test("never captures a collider disabled by another feature",()=>{Setup();var p=new PlayerControl();p.Collider.enabled=false;NoClipMovement.Update(p,AmongUsClient.Instance,true);NoClipMovement.Restore();Check(!p.Collider.enabled);});
  Test("query ignores own collider and triggers",()=>{Setup();var p=new PlayerControl();Physics2D.Results=_=>new[]{p.Collider,new Collider2D{isTrigger=true}};NoClipMovement.Update(p,AmongUsClient.Instance,true);Check(!p.Collider.enabled);NoClipMovement.Restore();});
  Test("query overflow refuses movement",()=>{Setup();var p=new PlayerControl();Physics2D.Results=_=>Enumerable.Range(0,40).Select(_=>new Collider2D()).ToArray();NoClipMovement.Update(p,AmongUsClient.Instance,true);Check(p.Collider.enabled);});
  Test("changed session never sends old corrective RPC",()=>{Setup();var p=new PlayerControl();NoClipMovement.Update(p,AmongUsClient.Instance,true);Physics2D.Results=_=>new[]{new Collider2D()};AmongUsClient.Instance.GameId=2;NoClipMovement.Update(p,AmongUsClient.Instance,false);Check(p.Collider.enabled&&p.NetTransform.RpcCalls==0);});
  Test("meeting restores active collision",()=>{Setup();var p=new PlayerControl();NoClipMovement.Update(p,AmongUsClient.Instance,true);TownOfHost.GameStates.IsMeeting=true;NoClipMovement.Update(p,AmongUsClient.Instance,true);Check(p.Collider.enabled&&!NoClipMovement.Active);});
  Test("end restores without old-room traffic",()=>{Setup();var p=new PlayerControl();NoClipMovement.Update(p,AmongUsClient.Instance,true);AmongUsClient.Instance.GameState=AmongUsClient.GameStates.Ended;NoClipMovement.Update(p,AmongUsClient.Instance,true);Check(p.Collider.enabled&&p.NetTransform.RpcCalls==0);});
  Test("nonfinite position cannot acquire movement",()=>{Setup();var p=new PlayerControl{Position=new(float.NaN,0)};NoClipMovement.Update(p,AmongUsClient.Instance,true);Check(p.Collider.enabled);});
 }
 static void FramingTests()
 {
  Test("bounded command frame roundtrip",()=>{using var s=new MemoryStream();LocalPipe.WriteAsync(s,On(),1024,default).GetAwaiter().GetResult();s.Position=0;Check(LocalPipe.ReadAsync<ControlRequest>(s,1024,default).GetAwaiter().GetResult().NoClip);});
  foreach(int length in new[]{-1,0,1025,int.MaxValue}){int n=length;Test("malicious frame length rejected "+n,()=>{using var s=new MemoryStream(BitConverter.GetBytes(n));try{LocalPipe.ReadAsync<ControlRequest>(s,1024,default).GetAwaiter().GetResult();throw new Exception("accepted");}catch(InvalidDataException){}});}
  Test("truncated frame rejected",()=>{using var s=new MemoryStream(new byte[]{10,0,0,0,1});try{LocalPipe.ReadAsync<ControlRequest>(s,1024,default).GetAwaiter().GetResult();throw new Exception("accepted");}catch(EndOfStreamException){}});
  Test("invalid JSON rejected",()=>{using var s=new MemoryStream(new byte[]{1,0,0,0,0xff});try{LocalPipe.ReadAsync<ControlRequest>(s,1024,default).GetAwaiter().GetResult();throw new Exception("accepted");}catch(JsonException){}});
  Test("remote and path traversal pipe names rejected",()=>{foreach(var name in new[]{@"\\host\pipe\x","../x","a:b"})try{using var p=LocalPipe.CreateServer(name);throw new Exception("accepted");}catch(ArgumentException){}});
 }
 static void PipeTests()
 {
  Test("OS named pipe authenticates both local PIDs and roundtrips",()=>{
   string name=LocalPipe.Name(Environment.ProcessId)+"-test";using var server=LocalPipe.CreateServer(name);using var client=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);
   using var timeout=new CancellationTokenSource(3000);var pending=server.WaitForConnectionAsync(timeout.Token);client.ConnectAsync(1000,timeout.Token).GetAwaiter().GetResult();pending.GetAwaiter().GetResult();Check(LocalPipe.PeerProcess(client,false)==Environment.ProcessId&&LocalPipe.PeerProcess(server,true)==Environment.ProcessId);
   LocalPipe.WriteAsync(client,On(),1024,timeout.Token).GetAwaiter().GetResult();Check(LocalPipe.ReadAsync<ControlRequest>(server,1024,timeout.Token).GetAwaiter().GetResult().Debug);
  });
  Test("OS first-instance flag prevents duplicate pipe owner",()=>{string name=LocalPipe.Name(Environment.ProcessId)+"-exclusive";using var server=LocalPipe.CreateServer(name);try{using var other=LocalPipe.CreateServer(name);throw new Exception("duplicate accepted");}catch(System.ComponentModel.Win32Exception){}});
  Test("non-controller executable rejected as controller peer",()=>Check(!LocalPipe.ControllerPeer(Environment.ProcessId)));
 }
}
