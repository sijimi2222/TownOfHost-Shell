using System.Text.Json;
using ShellDebugController.Shared;
internal static partial class Program {
 static void MapPoolTests(){
  Test("map-aware simulator default ten and eleven",()=>{Check(new SimulationInput().Dummies==10);Check(new SimulationInput{IsFungle=true}.Dummies==11);});
  Test("simulation map changes update defaults without frozen count",()=>{var i=new SimulationInput();i.IsFungle=true;Check(i.Dummies==11);i.IsFungle=false;Check(i.Dummies==10);});
  Test("manual stress count remains explicitly configurable",()=>{var i=new SimulationInput{Dummies=17,IsFungle=true};Check(i.Dummies==17);i.IsFungle=false;Check(i.Dummies==17);});
  Test("Fungle adds four paced preparation sends",()=>{var a=TrafficSimulator.Run(new());var b=TrafficSimulator.Run(new(){IsFungle=true});Check(a.Phases[0].Messages==40&&b.Phases[0].Messages==44);Check(a.Phases[1].Messages==b.Phases[1].Messages&&a.Phases[2].Messages==b.Phases[2].Messages);Check(b.Seconds.Length==a.Seconds.Length+1);Check(b.Seconds.Max()<=a.Seconds.Max());});
  Test("Fungle preparation byte delta uses same payload assumptions",()=>{var a=TrafficSimulator.Run(new());var b=TrafficSimulator.Run(new(){IsFungle=true});Check(b.Phases[0].Bytes-a.Phases[0].Bytes==1190);Check(b.MaximumPacketBytes==a.MaximumPacketBytes);Check(b.Basis.Contains("推定専用")&&b.Basis.Contains("The Fungle")&&b.Basis.Contains("11体"));});
  Test("Fungle name size bound still enforced",()=>{Check(!TrafficSimulator.Run(new(){IsFungle=true,NameBytes=800}).Warnings.Any(x=>x.Contains("表示名800")));Check(TrafficSimulator.Run(new(){IsFungle=true,NameBytes=801}).Warnings.Any(x=>x.Contains("表示名800")));});
  Test("eleven pool modeled without spurious packet or rate breach",()=>{var r=TrafficSimulator.Run(new(){IsFungle=true});Check(r.PeakTenSeconds<100&&r.MaximumPacketBytes<=1000);});
  Test("legacy telemetry absent map is unknown not assumed Fungle",()=>{var s=JsonSerializer.Deserialize<DebugSnapshot>("{}");Check(s.MapId==null&&s.IsFungle==null&&s.NovaPoolTarget==null);});
  Test("Fungle telemetry map count roundtrips",()=>{var s=JsonSerializer.Deserialize<DebugSnapshot>(JsonSerializer.Serialize(new DebugSnapshot{MapId=5,IsFungle=true,NovaPoolTarget=11}));Check(s.MapId==5&&s.IsFungle==true&&s.NovaPoolTarget==11);});
 }
}
