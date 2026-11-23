using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ShellDebugController.Shared;
namespace TownOfHost.Development;

// Fixed schema, one authenticated local peer, bounded frames and IO deadlines.
internal sealed class DebugPipeServer : IDisposable
{
    private readonly CancellationTokenSource stop=new();
    private readonly ControlLease lease;
    private readonly Func<DebugSnapshot> snapshot;
    private readonly Func<double> clock;
    private readonly Func<int,bool> authorize;
    private readonly string name;
    private Task worker;
    internal DebugPipeServer(ControlLease lease,Func<DebugSnapshot> snapshot,Func<double> clock,Func<int,bool> authorize=null,string name=null)
    {this.lease=lease;this.snapshot=snapshot;this.clock=clock;this.authorize=authorize??LocalPipe.ControllerPeer;this.name=name??LocalPipe.Name(Environment.ProcessId);}
    internal void Start()=>worker=Task.Run(Run);
    private async Task Run()
    {
        // Name collision or missing OS protection fails closed. No TCP fallback.
        try
        {
            using var pipe=LocalPipe.CreateServer(name);
            while(!stop.IsCancellationRequested)
            {
                await pipe.WaitForConnectionAsync(stop.Token).ConfigureAwait(false);
                try
                {
                    if(!authorize(LocalPipe.PeerProcess(pipe,true)))continue;
                    lease.Open();double last=double.NegativeInfinity;
                    while(!stop.IsCancellationRequested&&pipe.IsConnected)
                    {
                        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);deadline.CancelAfter(2000);
                        var request=await LocalPipe.ReadAsync<ControlRequest>(pipe,LocalPipe.RequestLimit,deadline.Token).ConfigureAwait(false);
                        double now=clock();if(now-last<.1||!lease.Accept(request,now))break;last=now;
                        await LocalPipe.WriteAsync(pipe,snapshot(),LocalPipe.ResponseLimit,deadline.Token).ConfigureAwait(false);
                    }
                }
                catch(Exception) when(!stop.IsCancellationRequested) { /* A failed peer only loses its lease. No payload logging. */ }
                finally { lease.Close();if(pipe.IsConnected)pipe.Disconnect(); }
            }
        }
        catch(Exception){lease.Close();}
    }
    public void Dispose(){lease.Close();stop.Cancel();}
}
