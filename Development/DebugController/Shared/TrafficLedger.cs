using System;
using System.Linq;
using System.Collections.Generic;
namespace ShellDebugController.Shared;

// Fixed-size aggregates: never retains packets, player names, room codes or tokens.
public sealed class TrafficLedger
{
    public static readonly string[] Categories = { "spawn", "assignment", "hide", "beam", "guide", "move", "remove" };
    private readonly object sync = new();
    private readonly long[] seconds = Enumerable.Repeat(long.MinValue, 60).ToArray();
    private readonly long[] counts = new long[60];
    private readonly PhaseTraffic[] phases = Categories.Select(p => new PhaseTraffic { Phase=p }).ToArray();
    private long count, bytes, errors;
    private int nameBytes;
    private readonly Queue<double> recentTimes=new();
    private double truncatedUntil;
    public const int MaximumRecentEvents=16384;
    private static long Add(long value, long addition) => addition > long.MaxValue - value ? long.MaxValue : value + addition;
    public void Raw(int length, double now)
    {
        if (length < 0 || !double.IsFinite(now) || now < 0) return;
        lock (sync)
        {
            count=Add(count,1); bytes=Add(bytes,length);
            while(recentTimes.Count>0&&now-recentTimes.Peek()>=10)recentTimes.Dequeue();
            if(recentTimes.Count>=MaximumRecentEvents){recentTimes.Dequeue();truncatedUntil=now+10;}
            recentTimes.Enqueue(now);
            long second=(long)now; int slot=(int)(second%60);
            if(seconds[slot]!=second){seconds[slot]=second;counts[slot]=0;}
            counts[slot]=Add(counts[slot],1);
        }
    }
    public void Error() { lock(sync) errors=Add(errors,1); }
    public void Request(string category, int length)
    {
        if(length<0)return;
        lock(sync)
        {
            var phase=phases.FirstOrDefault(p=>p.Phase==category);if(phase==null)return;
            phase.Messages=Add(phase.Messages,1);phase.Bytes=Add(phase.Bytes,length);phase.MaximumBytes=Math.Max(phase.MaximumBytes,length);
        }
    }
    public void Name(int length) { if(length>=0)lock(sync)nameBytes=Math.Max(nameBytes,length); }
    public void Fill(DebugSnapshot snapshot, double now)
    {
        lock(sync)
        {
            long second=(long)Math.Max(0,now);var recent=new long[60];
            for(int i=0;i<60;i++){long wanted=second-59+i;int slot=(int)((wanted%60+60)%60);if(seconds[slot]==wanted)recent[i]=counts[slot];}
            snapshot.RawSendCount=count;snapshot.RawSendBytes=bytes;snapshot.SendErrors=errors;snapshot.RecentSeconds=recent;
            while(recentTimes.Count>0&&now-recentTimes.Peek()>=10)recentTimes.Dequeue();
            snapshot.LastTenSeconds=recentTimes.Count;snapshot.TenSecondWindowTruncated=now<truncatedUntil;
            snapshot.MaximumNameBytes=nameBytes;
            snapshot.NovaTraffic=phases.Select(p=>new PhaseTraffic{Phase=p.Phase,Messages=p.Messages,Bytes=p.Bytes,MaximumBytes=p.MaximumBytes}).ToArray();
            snapshot.ObservedEmptyNameBytes=phases[2].MaximumBytes;snapshot.ObservedSpawnBytes=phases[0].MaximumBytes;
        }
    }
    public void Clear()
    {
        lock(sync){count=bytes=errors=0;nameBytes=0;recentTimes.Clear();truncatedUntil=0;Array.Fill(seconds,long.MinValue);Array.Clear(counts,0,counts.Length);foreach(var p in phases){p.Messages=p.Bytes=0;p.MaximumBytes=0;}}
    }
}
