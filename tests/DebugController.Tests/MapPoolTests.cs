using System.Text.Json;
using ShellDebugController.Shared;
internal static partial class Program {
 static void MapPoolTests(){
  Test("Airship simulator uses fifteen including star",()=>{Check(new SimulationInput{IsAirship=true}.Dummies==15);Check(new SimulationInput{IsFungle=true}.Dummies==11&&new SimulationInput().Dummies==10);});
  Test("Airship fifteen adds twenty paced preparation sends",()=>{var a=TrafficSimulator.Run(new());var b=TrafficSimulator.Run(new(){IsAirship=true});Check(a.Phases[0].Messages==40&&b.Phases[0].Messages==60);Check(b.Phases[0].Bytes-a.Phases[0].Bytes==5950);Check(b.Phases[1].Bytes==a.Phases[1].Bytes&&b.Phases[2].Bytes==a.Phases[2].Bytes);Check(b.Seconds.Length==a.Seconds.Length+5&&b.Seconds.Max()<=a.Seconds.Max());});
  Test("Airship estimate size bounds and explicit map label",()=>{var r=TrafficSimulator.Run(new(){IsAirship=true});Check(r.MaximumPacketBytes<=1000&&r.PeakTenSeconds<100&&r.Basis.Contains("推定専用")&&r.Basis.Contains("The Airship")&&r.Basis.Contains("15体"));});
  Test("Airship name byte guard retained",()=>{Check(!TrafficSimulator.Run(new(){IsAirship=true,NameBytes=800}).Warnings.Any(x=>x.Contains("表示名800")));Check(TrafficSimulator.Run(new(){IsAirship=true,NameBytes=801}).Warnings.Any(x=>x.Contains("表示名800")));});
  Test("Airship simulation toggles back to unchanged normal and Fungle",()=>{var x=new SimulationInput{IsAirship=true};Check(x.Dummies==15);x.IsAirship=false;Check(x.Dummies==10);x.IsFungle=true;Check(x.Dummies==11);});
  Test("Airship manual stress override is preserved",()=>{var x=new SimulationInput{IsAirship=true,Dummies=17};Check(x.Dummies==17);x.IsAirship=false;Check(x.Dummies==17);});
  Test("Airship telemetry map and target roundtrip without protocol change",()=>{var x=JsonSerializer.Deserialize<DebugSnapshot>(JsonSerializer.Serialize(new DebugSnapshot{MapId=4,IsFungle=false,NovaPoolTarget=15}));Check(x.MapId==4&&x.IsFungle==false&&x.NovaPoolTarget==15);});
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
