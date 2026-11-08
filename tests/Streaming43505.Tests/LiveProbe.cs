using System.Reflection;using System.Text.Json;using TownOfHost;
partial class Program{
static async Task RunLiveProbe(){
try{
 var url=Environment.GetEnvironmentVariable("TOHS_LIVE_PROBE_URL");
 if(!StreamerInfo.TrySetStreamURL(url)){Console.WriteLine("{\"stage\":\"input\",\"success\":false}");return;}
 int epoch=(int)typeof(StreamerInfo).GetField("epoch",flags).GetValue(null);
 var info=(StreamerInfo)Activator.CreateInstance(typeof(StreamerInfo),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{StreamSafety.VideoId(url),epoch},null);
 typeof(StreamerInfo).GetField("current",flags).SetValue(null,info);
 for(int attempt=0;attempt<3;attempt++){
  try{
   await info.FetchAsync();Drain();
   var session=(ChatData)typeof(StreamerInfo).GetField("chatData",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(info);
   bool displayed=StreamerInfo.RecentComments.Count>0&&FrameTextUnderTest.BuildText().Contains(StreamSafety.Plain(StreamerInfo.RecentComments.Last().Text,60));
   Console.WriteLine(JsonSerializer.Serialize(new{stage=attempt==0?"bootstrap":"poll",success=true,session_present=session!=null,received_comments=StreamerInfo.RecentComments.Count,formatter_contains_comment=displayed}));
   if(attempt<2)await Task.Delay(session?.PollDelayMilliseconds??3000);
  }catch(Exception error){var field=error.GetType().GetField("HttpStatus",BindingFlags.Instance|BindingFlags.NonPublic);Console.WriteLine(JsonSerializer.Serialize(new{stage=attempt==0?"bootstrap":"poll",success=false,http=field?.GetValue(error)??0,error_type=error is HttpRequestException?"transport":error is OperationCanceledException?"timeout":"response"}));break;}
 }
}finally{StreamerInfo.StopNetwork();}}
}
