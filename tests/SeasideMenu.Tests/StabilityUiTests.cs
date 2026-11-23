using TownOfHost;using UnityEngine;using Obj=UnityEngine.Object;using System.Reflection;
partial class Program {
 static void StabilityUiTests(){
  Test("closed server dropdown does not rescan component arrays every frame",()=>{OnlineAssetFolder();var x=CodeFixture();Obj.ChildComponentQueries=0;for(int i=0;i<100;i++)CodeSkin.Tick();Check(Obj.ChildComponentQueries==0);Call("CloseCodeEntry");x.view.Dispose();});
  Test("exhausted menu diagnostics do not construct another component snapshot",()=>{var x=Setup();BindHome(x);x.view.Tick(true);typeof(SeasideMainMenuPatch).GetField("diagnosticCount",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,12);Obj.ComponentQueries=0;for(int i=0;i<100;i++)Call("Stamp",new ModManager());Check(Obj.ComponentQueries==0);x.view.Dispose();});
 }
 static void BenchmarkStabilityUi(){
  OnlineAssetFolder();var x=CodeFixture();Obj.ChildComponentQueries=0;for(int i=0;i<100;i++)CodeSkin.Tick();int dropdown=Obj.ChildComponentQueries;Call("CloseCodeEntry");x.view.Dispose();
  var h=Setup();BindHome(h);h.view.Tick(true);typeof(SeasideMainMenuPatch).GetField("diagnosticCount",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,12);Obj.ComponentQueries=0;for(int i=0;i<100;i++)Call("Stamp",new ModManager());
  Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{frames=100,closed_dropdown_child_queries=dropdown,exhausted_diagnostic_component_queries=Obj.ComponentQueries}));h.view.Dispose();
 }
}
