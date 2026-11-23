using System.Text.Json;
using ShellDebugController.Shared;
using TownOfHost.Modules;
internal static partial class Program {
 static void MapPoolTests(){
  Test("Airship nominal measured-font estimate includes star",()=>{Check(new SimulationInput{IsAirship=true}.Dummies==4);Check(new SimulationInput{IsFungle=true}.Dummies==3&&new SimulationInput().Dummies==3);});
  Test("Airship nominal pool adds only four paced preparation sends",()=>{var a=TrafficSimulator.Run(new());var b=TrafficSimulator.Run(new(){IsAirship=true});Check(a.Phases[0].Messages==12&&b.Phases[0].Messages==16);Check(b.Phases[0].Bytes-a.Phases[0].Bytes==1190);Check(a.Phases[1].Messages==6&&b.Phases[1].Messages==8);});
  Test("Airship estimate size bounds and explicit map label",()=>{var r=TrafficSimulator.Run(new(){IsAirship=true});Check(r.MaximumPacketBytes<=1000&&r.PeakTenSeconds<100&&r.Basis.Contains("推定専用")&&r.Basis.Contains("The Airship")&&r.Basis.Contains("4体"));});
  Test("Airship name byte guard retained",()=>{Check(!TrafficSimulator.Run(new(){IsAirship=true,NameBytes=800}).Warnings.Any(x=>x.Contains("表示名800")));Check(TrafficSimulator.Run(new(){IsAirship=true,NameBytes=801}).Warnings.Any(x=>x.Contains("表示名800")));});
  Test("Airship simulation toggles to dynamic nominal normal and Fungle",()=>{var x=new SimulationInput{IsAirship=true};Check(x.Dummies==4);x.IsAirship=false;Check(x.Dummies==3);x.IsFungle=true;Check(x.Dummies==3);});
  Test("Airship manual stress override is preserved",()=>{var x=new SimulationInput{IsAirship=true,Dummies=17};Check(x.Dummies==17);x.IsAirship=false;Check(x.Dummies==17);});
  Test("Airship measured target roundtrip without protocol change",()=>{var x=JsonSerializer.Deserialize<DebugSnapshot>(JsonSerializer.Serialize(new DebugSnapshot{MapId=4,IsFungle=false,NovaPoolTarget=4}));Check(x.MapId==4&&x.IsFungle==false&&x.NovaPoolTarget==4);});
  Test("nominal other maps have identical range50 estimates",()=>{Check(new SimulationInput().Dummies==3);Check(new SimulationInput{IsFungle=true}.Dummies==3);});
  Test("simulation map changes update estimates without frozen count",()=>{var i=new SimulationInput();i.IsAirship=true;Check(i.Dummies==4);i.IsAirship=false;Check(i.Dummies==3);});
  Test("manual stress count remains explicitly configurable",()=>{var i=new SimulationInput{Dummies=17,IsFungle=true};Check(i.Dummies==17);i.IsFungle=false;Check(i.Dummies==17);});
  Test("Fungle no longer generates an unnecessary reserve",()=>{var a=TrafficSimulator.Run(new());var b=TrafficSimulator.Run(new(){IsFungle=true});Check(a.Phases[0].Messages==12&&b.Phases[0].Messages==12);Check(a.Phases.Select(x=>x.Bytes).SequenceEqual(b.Phases.Select(x=>x.Bytes)));});
  Test("Fungle assumptions and nominal label remain explicit",()=>{var b=TrafficSimulator.Run(new(){IsFungle=true});Check(b.Basis.Contains("推定専用")&&b.Basis.Contains("The Fungle")&&b.Basis.Contains("参考字体")&&b.Basis.Contains("3体"));});
  Test("Fungle name size bound still enforced",()=>{Check(!TrafficSimulator.Run(new(){IsFungle=true,NameBytes=800}).Warnings.Any(x=>x.Contains("表示名800")));Check(TrafficSimulator.Run(new(){IsFungle=true,NameBytes=801}).Warnings.Any(x=>x.Contains("表示名800")));});
  Test("optimized Fungle modeled without packet or rate breach",()=>{var r=TrafficSimulator.Run(new(){IsFungle=true});Check(r.PeakTenSeconds<100&&r.MaximumPacketBytes<=1000);});
  Test("legacy telemetry absent map is unknown",()=>{var s=JsonSerializer.Deserialize<DebugSnapshot>("{}");Check(s.MapId==null&&s.IsFungle==null&&s.NovaPoolTarget==null);});
  Test("Fungle measured telemetry count roundtrips",()=>{var s=JsonSerializer.Deserialize<DebugSnapshot>(JsonSerializer.Serialize(new DebugSnapshot{MapId=5,IsFungle=true,NovaPoolTarget=3}));Check(s.MapId==5&&s.IsFungle==true&&s.NovaPoolTarget==3);});
  foreach(int participants in new[]{1,6,10,12,15})foreach(int count in new[]{3,4,10,11,15})Test($"participant-aware estimate {participants}/{count}",()=>{var r=TrafficSimulator.Run(new(){Participants=participants,Dummies=count});int per=2+(int)Math.Ceiling((participants-1)/8d);Check(r.Phases[0].Messages==count*per);Check(r.Basis.Contains(participants+"人/1 Nova"));Check(r.MaximumPacketBytes<=1000);});
  foreach(float width in Enumerable.Range(1,20).Select(x=>x*.25f))foreach(bool air in new[]{false,true})Test($"nominal width estimate {width}/{air}",()=>{int count=NovaVisualPoolRules.EstimateTotal(false,air,width);Check(count>=2&&count<=NovaVisualPoolRules.Count(false,air));});
  foreach(int invalid in new[]{0,16})Test("participant model rejects invalid "+invalid,()=>{try{TrafficSimulator.Run(new(){Participants=invalid});throw new Exception("accepted");}catch(ArgumentOutOfRangeException){}});
 }
}
