using System.Diagnostics;
using System.IO.Pipes;
using ShellDebugController.Shared;
namespace ShellDebugController;
internal sealed class ControllerConnection : IDisposable
{
    private readonly CancellationTokenSource stop=new();
    private readonly Func<ControlRequest> state;
    private readonly Action<DebugSnapshot,string> receive;
    private Task task;
    internal ControllerConnection(Func<ControlRequest> state,Action<DebugSnapshot,string> receive){this.state=state;this.receive=receive;}
    internal void Start()=>task=Task.Run(Run);
    private async Task Run()
    {
        while(!stop.IsCancellationRequested)
        {
            try
            {
                var processes=Process.GetProcessesByName("Among Us").Concat(Process.GetProcessesByName("AmongUs")).ToArray();
                int pid=0;
                try
                {
                    if(processes.Length!=1){receive(null,processes.Length==0?"Among Us: 未起動":"Among Usが複数あります。接続を停止しました");}
                    else pid=processes[0].Id;
                }
                finally{foreach(var process in processes)process.Dispose();}
                if(pid==0){await Task.Delay(1500,stop.Token).ConfigureAwait(false);continue;}
                using var pipe=new NamedPipeClientStream(".",LocalPipe.Name(pid),PipeDirection.InOut,PipeOptions.Asynchronous);
                await pipe.ConnectAsync(700,stop.Token).ConfigureAwait(false);
                if(LocalPipe.PeerProcess(pipe,false)!=pid)throw new InvalidDataException("Server process identity mismatch");
                long sequence=0;
                while(!stop.IsCancellationRequested&&pipe.IsConnected)
                {
                    using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);deadline.CancelAfter(1800);
                    var request=state().Sanitized();request.Sequence=++sequence;
                    await LocalPipe.WriteAsync(pipe,request,LocalPipe.RequestLimit,deadline.Token).ConfigureAwait(false);
                    var snapshot=await LocalPipe.ReadAsync<DebugSnapshot>(pipe,LocalPipe.ResponseLimit,deadline.Token).ConfigureAwait(false);
                    if(snapshot.Protocol!=1||snapshot.ProcessId!=pid||snapshot.Build!="ShellDevelopment")throw new InvalidDataException("Not a supported development MOD");
                    receive(snapshot,"Among Us PID "+pid+" / TOH-Shell "+snapshot.Version+" 開発版 接続済み");
                    await Task.Delay(250,stop.Token).ConfigureAwait(false);
                }
            }
            catch(OperationCanceledException)when(stop.IsCancellationRequested){break;}
            catch(Exception){receive(null,"開発版に未接続 / 通信終了（全操作OFF）");}
            try{await Task.Delay(1500,stop.Token).ConfigureAwait(false);}catch(OperationCanceledException){break;}
        }
    }
    public void Dispose(){stop.Cancel();}
}
