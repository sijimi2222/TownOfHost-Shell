using System.Text.Json;
using TownOfHost.Modules;
partial class Program {
 static void RangeReplay(string path){
  using var doc=JsonDocument.Parse(System.IO.File.ReadAllText(path));var rows=new List<object>();
  foreach(var input in doc.RootElement.EnumerateArray()){
   int pool=input.GetProperty("pool").GetInt32();Check(pool>=1&&pool<=17);
   var p=Prepare(pool,14,true);int prep=p.times.Count;double ready=p.times.LastOrDefault();int prepPeak=Peak(p.times);
   Check(prep==pool*4);var events=new List<(double time,int index,string name,float x,float y,bool move)>();
   foreach(var e in input.GetProperty("names").EnumerateArray())events.Add((e.GetProperty("time").GetDouble(),e.GetProperty("index").GetInt32(),e.GetProperty("name").GetString(),0,0,false));
   foreach(var e in input.GetProperty("moves").EnumerateArray())events.Add((e.GetProperty("time").GetDouble(),e.GetProperty("index").GetInt32(),null,e.GetProperty("x").GetSingle(),e.GetProperty("y").GetSingle(),true));
   double begin=clock;foreach(var e in events.OrderBy(e=>e.time).ThenBy(e=>(input.GetProperty("reach").GetSingle()==50||input.GetProperty("reach").GetSingle()==70)&&e.move?0:1)){
    Check(e.index>=0&&e.index<pool);if(begin+e.time>clock)Advance((float)(begin+e.time-clock));
    if(e.move)p.pool[e.index].Move(new(e.x,e.y));else p.pool[e.index].Visual(e.name);
   }
   Check(CustomNetObject.AllObjects.Count==pool);int total=p.times.Count;
   rows.Add(new{fired=input.GetProperty("fired").GetBoolean(),reach=input.GetProperty("reach").GetSingle(),width=input.GetProperty("width").GetSingle(),angle=input.GetProperty("angle").GetSingle(),preferred=input.GetProperty("preferred").GetBoolean(),participants=15,pool,prep_send_calls=prep,prep_completed_at=ready,prep_peak_10s=prepPeak,cosmetic_send_calls=total-prep,total_send_calls=total,peak_10s=Peak(p.times),name_utf8_bytes=input.GetProperty("name_utf8_bytes").GetInt32(),max_name_utf8_bytes=input.GetProperty("max_name_utf8_bytes").GetInt32(),active_beam_dummies=input.GetProperty("active").GetInt32(),role_packets=input.GetProperty("role_packets").GetInt32()});
  }
  Console.WriteLine("RANGE_REPLAY "+JsonSerializer.Serialize(rows));
 }
}
