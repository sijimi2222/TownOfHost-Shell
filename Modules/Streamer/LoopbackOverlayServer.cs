using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TownOfHost;

// Read-only, bounded, loopback-only transport. No game objects on this thread.
internal sealed class LoopbackOverlayServer : IDisposable
{
    internal const int DefaultPort = 52525;
    private readonly object gate = new();
    private TcpListener listener;
    private TcpClient active;
    private Thread thread;
    private string snapshot = "{}";
    private long updatedAt;
    internal int Port { get; private set; }
    internal bool Running { get { lock(gate) return listener != null; } }

    internal bool Start(int port = DefaultPort)
    {
        lock(gate)
        {
            if (listener != null) return true;
            try
            {
                var l = new TcpListener(IPAddress.Loopback, port);
                listener=l; l.Server.ExclusiveAddressUse = true; l.Start(8);
                listener=l; Port=((IPEndPoint)l.LocalEndpoint).Port;
                snapshot="{}"; updatedAt=Stopwatch.GetTimestamp();
                thread=new Thread(()=>Serve(l)) { IsBackground=true, Name="TOH-Shell OBS" };
                thread.Start(); return true;
            }
            catch { try { listener?.Stop(); } catch { } listener=null; return false; }
        }
    }

    internal void Publish(string json)
    {
        if (json == null || Encoding.UTF8.GetByteCount(json)>65536) json="{}";
        lock(gate) { snapshot=json; updatedAt=Stopwatch.GetTimestamp(); }
    }

    internal void Stop()
    {
        Thread worker;
        lock(gate)
        {
            var l=listener; listener=null; snapshot="{}";
            try { active?.Dispose(); } catch { }
            active=null; try { l?.Stop(); } catch { }
            worker=thread; thread=null;
        }
        if(worker!=null && worker!=Thread.CurrentThread) worker.Join(1500);
    }
    public void Dispose()=>Stop();

    private void Serve(TcpListener l)
    {
        while(true)
        {
            lock(gate) { if(listener!=l) break; }
            try
            {
                using var client=l.AcceptTcpClient();
                client.ReceiveTimeout=750;client.SendTimeout=750;
                lock(gate) { if(listener!=l) break; active=client; }
                try { Respond(client); } finally { lock(gate) { if(active==client) active=null; } }
            }
            catch(SocketException) { lock(gate) { if(listener!=l) break; } }
            catch(ObjectDisposedException) { break; }
            catch { lock(gate) { if(listener!=l) break; } }
        }
    }

    private void Respond(TcpClient client)
    {
        if(client.Client.RemoteEndPoint is not IPEndPoint ep || !IPAddress.IsLoopback(ep.Address)) return;
        using var stream=client.GetStream();
        var buffer=new byte[8192];int count=0;string request="";var deadline=Stopwatch.StartNew();
        while(count<buffer.Length && deadline.ElapsedMilliseconds<2000)
        {
            int n=stream.Read(buffer,count,buffer.Length-count);if(n==0)return;count+=n;
            request=Encoding.ASCII.GetString(buffer,0,count);
            if(request.Contains("\r\n\r\n",StringComparison.Ordinal))break;
        }
        if(!request.Contains("\r\n\r\n",StringComparison.Ordinal)){Send(stream,431,"text/plain","Request rejected");return;}
        var lines=request.Split(new[]{"\r\n"},StringSplitOptions.None);var first=lines[0].Split(' ');
        if(first.Length!=3 || first[0]!="GET" || first[2] is not ("HTTP/1.1" or "HTTP/1.0")){Send(stream,405,"text/plain","Method rejected");return;}
        var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        for(int i=1;i<lines.Length && lines[i].Length>0;i++)
        {
            int p=lines[i].IndexOf(':');if(p<=0){Send(stream,400,"text/plain","Request rejected");return;}
            var key=lines[i][..p].Trim();if(!headers.TryAdd(key,lines[i][(p+1)..].Trim())){Send(stream,400,"text/plain","Request rejected");return;}
        }
        bool AllowedHost(string host)=>host.Equals("localhost:"+Port,StringComparison.OrdinalIgnoreCase) || host=="127.0.0.1:"+Port;
        if(!headers.TryGetValue("Host",out var host)||!AllowedHost(host)
            || headers.ContainsKey("Transfer-Encoding") || headers.TryGetValue("Content-Length",out var len) && len!="0"
            || headers.TryGetValue("Origin",out var origin) && origin!="http://localhost:"+Port && origin!="http://127.0.0.1:"+Port
            || headers.TryGetValue("Sec-Fetch-Site",out var site) && site.Equals("cross-site",StringComparison.OrdinalIgnoreCase))
        {Send(stream,403,"text/plain","Request rejected");return;}
        if(first[1]=="/")Send(stream,200,"text/html",ObsOverlayPage.Html);
        else if(first[1]=="/state")
        {
            string body;lock(gate)body=(Stopwatch.GetTimestamp()-updatedAt)/(double)Stopwatch.Frequency<=2? snapshot:"{}";
            Send(stream,200,"application/json",body);
        }
        else Send(stream,404,"text/plain","Not found");
    }

    private static void Send(Stream stream,int status,string type,string body)
    {
        var data=Encoding.UTF8.GetBytes(body);
        var head=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Response\r\nContent-Type: {type}; charset=utf-8\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'\r\nContent-Length: {data.Length}\r\nConnection: close\r\n\r\n");
        stream.Write(head,0,head.Length);stream.Write(data,0,data.Length);
    }
}

internal static class ObsOverlayPage
{
    internal const string Html=@"<!doctype html><html lang=""ja""><head><meta charset=""utf-8""><title>TOH-Shell OBS</title><style>
body{margin:0;background:transparent;font-family:'Yu Gothic UI',Meiryo,sans-serif;color:#fff}#box{display:inline-block;padding:12px;background:rgba(0,0,0,.55);border-radius:10px;min-width:260px}.p{display:flex;gap:8px;align-items:center;margin:3px 0}.dead{opacity:.45;text-decoration:line-through}.dot{width:14px;height:14px;border-radius:50%}.role{font-size:13px}.tasks{font-size:12px;margin-left:auto}
</style></head><body><div id=""box""><strong id=""head"">TOH-Shell</strong><div id=""list""></div></div><script>
function color(s){return /^#[0-9a-f]{6}$/i.test(s)?s:'#ffffff'}
async function tick(){try{const r=await fetch('/state',{cache:'no-store'});if(!r.ok)throw Error();const s=await r.json();document.getElementById('head').textContent=(s.phase||'TOH-Shell')+(s.map?' / '+s.map:'')+(s.total?' / 生存 '+s.alive+'/'+s.total:'');const list=document.getElementById('list');list.replaceChildren();for(const p of s.players||[]){const row=document.createElement('div');row.className='p'+(p.dead?' dead':'');const dot=document.createElement('span');dot.className='dot';dot.style.backgroundColor=color(p.color);row.append(dot);const name=document.createElement('span');name.textContent=p.name||'';row.append(name);for(const [key,cls] of [['role','role'],['tasks','tasks']])if(p[key]){const t=document.createElement('span');t.className=cls;t.textContent=p[key];if(key==='role')t.style.color=color(p.roleColor);row.append(t)}list.append(row)}}catch{document.getElementById('head').textContent='TOH-Shell 接続待ち';document.getElementById('list').replaceChildren()}finally{setTimeout(tick,500)}}tick();
</script></body></html>";
}
