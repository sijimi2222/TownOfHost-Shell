using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
namespace ShellDebugController.Shared;

public static class LocalPipe
{
    public const uint RejectRemoteClients=0x8, FirstInstance=0x80000, Overlapped=0x40000000;
    public const int RequestLimit=1024, ResponseLimit=65536;
    private static string Sid()
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Windows only");
        using var identity=WindowsIdentity.GetCurrent();return identity.User?.Value??throw new InvalidOperationException("No Windows user SID");
    }
    public static string Name(int pid)
    {
        if(pid<=0)throw new ArgumentOutOfRangeException(nameof(pid));
        string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Sid()))).Substring(0,16);
        return "toh-shell-debug-v1-"+hash+"-"+pid;
    }
    [StructLayout(LayoutKind.Sequential)]private struct SecurityAttributes{public int Length;public IntPtr Descriptor;public int Inherit;}
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string text,uint revision,out IntPtr descriptor,out uint size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafePipeHandle CreateNamedPipe(string name,uint openMode,uint pipeMode,uint instances,uint output,uint input,uint timeout,ref SecurityAttributes security);
    [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint pid);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe,out uint pid);
    public static NamedPipeServerStream CreateServer(string name)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Length>150||name.IndexOfAny(new[]{'\\','/',':'})>=0)throw new ArgumentException("Invalid local pipe name");
        string sddl="D:P(A;;GA;;;"+Sid()+")";
        if(!ConvertStringSecurityDescriptorToSecurityDescriptor(sddl,1,out var descriptor,out _))throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var security=new SecurityAttributes{Length=Marshal.SizeOf<SecurityAttributes>(),Descriptor=descriptor,Inherit=0};
            var handle=CreateNamedPipe(@"\\.\pipe\"+name,3|FirstInstance|Overlapped,RejectRemoteClients,1,ResponseLimit,RequestLimit,0,ref security);
            if(handle.IsInvalid){int error=Marshal.GetLastWin32Error();handle.Dispose();throw new Win32Exception(error);}
            try{return new NamedPipeServerStream(PipeDirection.InOut,true,false,handle);}catch{handle.Dispose();throw;}
        }
        finally{LocalFree(descriptor);}
    }
    public static int PeerProcess(PipeStream pipe,bool server)
    {
        bool ok=server?GetNamedPipeClientProcessId(pipe.SafePipeHandle,out var pid):GetNamedPipeServerProcessId(pipe.SafePipeHandle,out pid);
        if(!ok||pid==0||pid>int.MaxValue)throw new Win32Exception(Marshal.GetLastWin32Error());return (int)pid;
    }
    public static bool ControllerPeer(int pid)
    {
        try{using var peer=Process.GetProcessById(pid);using var self=Process.GetCurrentProcess();return peer.SessionId==self.SessionId&&peer.ProcessName.Equals("ShellDebugController",StringComparison.OrdinalIgnoreCase);}
        catch{return false;}
    }
    public static async Task WriteAsync<T>(Stream stream,T value,int limit,CancellationToken cancellation)
    {
        byte[] data=JsonSerializer.SerializeToUtf8Bytes(value);
        if(data.Length<1||data.Length>limit)throw new InvalidDataException("IPC frame exceeds bound");
        byte[] header=BitConverter.GetBytes(data.Length);await stream.WriteAsync(header,0,4,cancellation).ConfigureAwait(false);
        await stream.WriteAsync(data,0,data.Length,cancellation).ConfigureAwait(false);await stream.FlushAsync(cancellation).ConfigureAwait(false);
    }
    public static async Task<T> ReadAsync<T>(Stream stream,int limit,CancellationToken cancellation)
    {
        async Task Exact(byte[] buffer){int offset=0;while(offset<buffer.Length){int read=await stream.ReadAsync(buffer,offset,buffer.Length-offset,cancellation).ConfigureAwait(false);if(read==0)throw new EndOfStreamException();offset+=read;}}
        var header=new byte[4];await Exact(header).ConfigureAwait(false);int length=BitConverter.ToInt32(header,0);
        if(length<1||length>limit)throw new InvalidDataException("Invalid IPC frame size");
        var data=new byte[length];await Exact(data).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(data,new JsonSerializerOptions{MaxDepth=8})??throw new InvalidDataException("Empty IPC object");
    }
}
