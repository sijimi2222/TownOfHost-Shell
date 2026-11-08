using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using ShellDebugController.Shared;
using TownOfHost.Development;
internal static partial class Program
{
 [DllImport("advapi32.dll",SetLastError=true)]static extern uint GetSecurityInfo(SafePipeHandle handle,int objectType,uint info,out IntPtr owner,out IntPtr group,out IntPtr dacl,out IntPtr sacl,out IntPtr descriptor);
 [DllImport("advapi32.dll")]static extern uint GetSecurityDescriptorLength(IntPtr descriptor);
 [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr descriptor);
 static void ServerTests()
 {
  Test("OS pipe DACL grants only current Windows SID",()=>{
   if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
   using var pipe=LocalPipe.CreateServer(LocalPipe.Name(Environment.ProcessId)+"-acl");
   Check(GetSecurityInfo(pipe.SafePipeHandle,6,4,out _,out _,out _,out _,out var descriptor)==0);
   try{var bytes=new byte[GetSecurityDescriptorLength(descriptor)];Marshal.Copy(descriptor,bytes,0,bytes.Length);var sd=new RawSecurityDescriptor(bytes,0);using var identity=WindowsIdentity.GetCurrent();Check(sd.DiscretionaryAcl.Count==1);var ace=(CommonAce)sd.DiscretionaryAcl[0];Check(ace.SecurityIdentifier==identity.User&&ace.AceQualifier==AceQualifier.AccessAllowed);}
   finally{LocalFree(descriptor);}
  });
  Test("real pipe server command OFF and reconnect clear lease",()=>{
   var clock=Stopwatch.StartNew();var lease=new ControlLease();string name=LocalPipe.Name(Environment.ProcessId)+"-flow";
   using var server=new DebugPipeServer(lease,()=>new DebugSnapshot{ProcessId=Environment.ProcessId,Debug=lease.Read(clock.Elapsed.TotalSeconds).Debug},()=>clock.Elapsed.TotalSeconds,p=>p==Environment.ProcessId,name);server.Start();
   NamedPipeClientStream Connect(){var client=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);client.Connect(1500);return client;}
   using(var client=Connect()){
    using var deadline=new CancellationTokenSource(1500);LocalPipe.WriteAsync(client,On(),1024,deadline.Token).GetAwaiter().GetResult();Check(LocalPipe.ReadAsync<DebugSnapshot>(client,65536,deadline.Token).GetAwaiter().GetResult().Debug);
    Thread.Sleep(120);LocalPipe.WriteAsync(client,new ControlRequest{Sequence=2},1024,deadline.Token).GetAwaiter().GetResult();Check(!LocalPipe.ReadAsync<DebugSnapshot>(client,65536,deadline.Token).GetAwaiter().GetResult().Debug);
   }
   Check(SpinWait.SpinUntil(()=>!lease.Read(clock.Elapsed.TotalSeconds).Debug,1500));
   using(var client=Connect()){using var deadline=new CancellationTokenSource(1500);LocalPipe.WriteAsync(client,new ControlRequest{Sequence=1},1024,deadline.Token).GetAwaiter().GetResult();Check(!LocalPipe.ReadAsync<DebugSnapshot>(client,65536,deadline.Token).GetAwaiter().GetResult().Debug);}
  });
  Test("real pipe server rejects unauthorized local PID",()=>{
   var clock=Stopwatch.StartNew();var lease=new ControlLease();string name=LocalPipe.Name(Environment.ProcessId)+"-unauthorized";
   using var server=new DebugPipeServer(lease,()=>new(),()=>clock.Elapsed.TotalSeconds,_=>false,name);server.Start();using var client=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);client.Connect(1500);using var deadline=new CancellationTokenSource(1500);
   try{LocalPipe.ReadAsync<DebugSnapshot>(client,65536,deadline.Token).GetAwaiter().GetResult();throw new Exception("unauthorized peer accepted");}catch(EndOfStreamException){}Check(!lease.Read(clock.Elapsed.TotalSeconds).Debug);
  });
  Test("real pipe malformed command loses lease",()=>{
   var clock=Stopwatch.StartNew();var lease=new ControlLease();string name=LocalPipe.Name(Environment.ProcessId)+"-malformed";
   using var server=new DebugPipeServer(lease,()=>new(),()=>clock.Elapsed.TotalSeconds,_=>true,name);server.Start();using var client=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);client.Connect(1500);client.Write(BitConverter.GetBytes(int.MaxValue));client.Flush();using var deadline=new CancellationTokenSource(1500);
   try{LocalPipe.ReadAsync<DebugSnapshot>(client,65536,deadline.Token).GetAwaiter().GetResult();throw new Exception("oversized accepted");}catch(EndOfStreamException){}Check(!lease.Read(clock.Elapsed.TotalSeconds).Debug);
  });
  Test("real pipe silent client times out without renewing permission",()=>{
   var clock=Stopwatch.StartNew();var lease=new ControlLease();string name=LocalPipe.Name(Environment.ProcessId)+"-timeout";
   using var server=new DebugPipeServer(lease,()=>new DebugSnapshot{Debug=lease.Read(clock.Elapsed.TotalSeconds).Debug},()=>clock.Elapsed.TotalSeconds,_=>true,name);server.Start();using var client=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous);client.Connect(1500);using var deadline=new CancellationTokenSource(1500);
   LocalPipe.WriteAsync(client,On(),1024,deadline.Token).GetAwaiter().GetResult();Check(LocalPipe.ReadAsync<DebugSnapshot>(client,65536,deadline.Token).GetAwaiter().GetResult().Debug);
   Check(SpinWait.SpinUntil(()=>!lease.Read(clock.Elapsed.TotalSeconds).Debug,2700));
  });
 }
}
