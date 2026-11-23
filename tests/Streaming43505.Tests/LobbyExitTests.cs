using System.Reflection;
using TownOfHost;
partial class Program {
 static void RunLobbyExitTests(){
  foreach(bool enabled in new[]{false,true})Test($"room exit cancels actual comment worker enabled={enabled} (synthetic)",()=>{
   ResetStream();var(info,source)=FakeStream();source.Hang=true;TownOfHost.Main.ShowStreamComments.Value=enabled;
   if(enabled){StreamerInfo.CommentFrameTick(.02f);Check(SpinWait.SpinUntil(()=>source.Calls==1,2000));}
   // Repeated cleanup paths: disconnect prefix, HUD destruction, then native disconnect callback.
   StreamerInfo.DisconnectInternal();StreamerInfo.StopNetwork();StreamerInfo.DisconnectInternal();WaitFetch();
   int calls=source.Calls;Queue(info,"obsolete","synthetic-channel","日本語 / English");Drain();
   TownOfHost.GameStates.IsLobby=false;TownOfHost.GameStates.InGame=false;
   for(int i=0;i<30;i++)StreamerInfo.CommentFrameTick(.02f);
   Check(source.Disposed&&calls==(enabled?1:0)&&source.Calls==calls);
   Check(StreamerInfo.RecentComments.Count==0&&StreamerInfo.Interests.Count==0&&!StreamerInfo.Interests.Accepting);
   Check(typeof(StreamerInfo).GetField("current",flags).GetValue(null)==null&&TownOfHost.Main.ShowStreamComments.Value==enabled);
  });
  foreach(bool enabled in new[]{false,true})Test($"twenty synthetic room recreations retain no old source enabled={enabled}",()=>{
   for(int n=0;n<20;n++){
    ResetStream();var(info,source)=FakeStream();source.Hang=true;TownOfHost.Main.ShowStreamComments.Value=enabled;
    if(enabled){StreamerInfo.CommentFrameTick(.02f);Check(SpinWait.SpinUntil(()=>source.Calls==1,2000));}
    StreamerInfo.DisconnectInternal();WaitFetch();Queue(info,"old-"+n,"synthetic-channel","old callback");Drain();
    Check(source.Disposed&&StreamerInfo.RecentComments.Count==0&&typeof(StreamerInfo).GetField("current",flags).GetValue(null)==null);
   }
  });
 }
}
