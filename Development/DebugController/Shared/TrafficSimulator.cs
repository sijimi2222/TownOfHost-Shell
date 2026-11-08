using System;
using System.Collections.Generic;
using System.Linq;
using TownOfHost.Modules;
namespace ShellDebugController.Shared;

public sealed class SimulationInput
{
    private int? customDummies;
    public bool IsFungle { get; set; }
    public int Dummies { get => customDummies ?? NovaVisualPoolRules.Count(IsFungle); set => customDummies=value; }
    public int BeamDummies { get; set; }=3;
    public int NameBytes { get; set; }=593;
    public int SpawnBytes { get; set; }=256;
    public int EmptyNameFrameBytes { get; set; }=256;
    public bool SpawnMeasured { get; set; }
    public bool EmptyNameMeasured { get; set; }
    public double ChargeSeconds { get; set; }=3;
    public double DelaySeconds { get; set; }=1;
}
public sealed class SimulatedPhase
{
    public string Phase { get; set; }="";
    public int Messages { get; set; }
    public long Bytes { get; set; }
}
public sealed class SimulationResult
{
    public SimulatedPhase[] Phases { get; set; }=Array.Empty<SimulatedPhase>();
    public long[] Seconds { get; set; }=Array.Empty<long>();
    public int PeakTenSeconds { get; set; }
    public int MaximumPacketBytes { get; set; }
    public string[] Warnings { get; set; }=Array.Empty<string>();
    public string Basis { get; set; }="";
}
public static class TrafficSimulator
{
    public const int Participants=15, RemoteParticipants=14, AssignmentBatch=8, PacketLimit=1000, NameLimit=800;
    // Exact framing fields from CNO.AssignId; five bytes for every packed int is
    // a conservative bound, not a measurement of a real transport datagram.
    public const int AssignmentEnvelopeUpperBytes=48, ReliableHeaderBytes=3, MoveUpperBytes=25;
    private static int PackedBytes(int n){int count=1;while((n>>=7)!=0)count++;return count;}
    public static SimulationResult Run(SimulationInput input)
    {
        if(input==null||input.Dummies<2||input.Dummies>17||input.BeamDummies<1||input.BeamDummies>=input.Dummies
            ||input.NameBytes<0||input.NameBytes>65535||input.SpawnBytes<1||input.SpawnBytes>65535
            ||input.EmptyNameFrameBytes<1||input.EmptyNameFrameBytes>65535
            ||!double.IsFinite(input.ChargeSeconds)||input.ChargeSeconds<0||input.ChargeSeconds>30
            ||!double.IsFinite(input.DelaySeconds)||input.DelaySeconds<0||input.DelaySeconds>30)throw new ArgumentOutOfRangeException(nameof(input));
        var events=new List<(double At,int Bytes,string Phase)>();
        void Add(double at,int size,string phase)=>events.Add((at,size,phase));
        for(int i=0;i<input.Dummies;i++)
        {
            Add(i,input.SpawnBytes,"準備");
            for(int remaining=RemoteParticipants;remaining>0;remaining-=AssignmentBatch)
                Add(i+.1,ReliableHeaderBytes+Math.Min(remaining,AssignmentBatch)*AssignmentEnvelopeUpperBytes,"準備");
            Add(i+.4,input.EmptyNameFrameBytes,"準備");
        }
        double fire=input.Dummies-.6+input.ChargeSeconds+input.DelaySeconds;
        int beamBytes=checked(input.EmptyNameFrameBytes+input.NameBytes+PackedBytes(input.NameBytes)-1);
        for(int i=0;i<input.BeamDummies;i++){Add(fire,MoveUpperBytes,"発射");Add(fire,beamBytes,"発射");}
        Add(fire,MoveUpperBytes,"発射");Add(fire,input.EmptyNameFrameBytes,"発射");
        for(int i=0;i<input.BeamDummies;i++){Add(fire+1.5,MoveUpperBytes,"回収");Add(fire+1.5,input.EmptyNameFrameBytes,"回収");}
        // Star is already hidden at firing; the normal reusable pool is parked,
        // not despawned after every shot. Meetings/end may despawn all separately.
        var warning=new List<string>();
        int maximum=events.Max(e=>e.Bytes),peak=events.Max(e=>events.Count(x=>x.At>=e.At&&x.At<e.At+10));
        if(input.NameBytes>NameLimit)warning.Add("表示名800B上限を超過");
        if(maximum>PacketLimit)warning.Add("1000Bを超える送信要求あり。自動分割できるとは仮定しません");
        if(peak>=100)warning.Add("10秒間に100回以上の送信要求が集中するモデルです");
        int cosmetics=events.Count(e=>e.Phase!="準備");if(cosmetics>120)warning.Add("Novaの6秒/120回の描画予算を超えるモデルです");
        if(!input.SpawnMeasured||!input.EmptyNameMeasured)warning.Add("未実測のペイロードは入力欄の仮定値を使用");
        warning.Add("準備=描画プール生成。チャージ/照準更新・他役職・通常同期・再送・UDPヘッダーは含まない。切断耐性は保証しません");
        int duration=(int)Math.Ceiling(events.Max(e=>e.At))+1;var seconds=new long[duration];foreach(var e in events)seconds[(int)e.At]++;
        return new SimulationResult{MaximumPacketBytes=maximum,PeakTenSeconds=peak,Seconds=seconds,Warnings=warning.ToArray(),
            Phases=events.GroupBy(e=>e.Phase).Select(g=>new SimulatedPhase{Phase=g.Key,Messages=g.Count(),Bytes=g.Sum(e=>(long)e.Bytes)}).ToArray(),
            Basis=$"推定専用: {(input.IsFungle?"The Fungle":"その他のマップ")} / プール{input.Dummies}体（照準含む、既定{NovaVisualPoolRules.Count(input.IsFungle)}）。15人/1 Nova、割当8人/包、生成1秒間隔。Spawn={(input.SpawnMeasured?"観測値":"仮定")}, 空名={(input.EmptyNameMeasured?"観測値":"仮定")}"};
    }
}
