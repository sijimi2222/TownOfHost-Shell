using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using TownOfHost.Development;
partial class Program
{
    static void RunDebugLifetimeTests()
    {
        Test("debug lifecycle never detours the crashing short native OnDestroy", () =>
        {
            string source = File.ReadAllText(Path.Combine(Root(),"Development/DebugController/DebugRuntime.cs"));
            Check(!Regex.IsMatch(source,@"HarmonyPatch\(typeof\(InnerNetClient\),\s*nameof\(InnerNetClient.OnDestroy\)"));
            foreach (var type in typeof(DebugRuntimeLifetime).GetNestedTypes(BindingFlags.NonPublic))
            { var target = type.GetCustomAttribute<HarmonyPatch>(); if (target != null) Check(target.Method != "OnDestroy"); }
        });
        Test("debug cleanup uses the existing disconnect method as a postfix", () =>
        {
            var type = typeof(DebugRuntimeLifetime).GetNestedType("DisconnectPatch",BindingFlags.NonPublic);
            var target = type.GetCustomAttribute<HarmonyPatch>();
            Check(target.Target == typeof(InnerNet.InnerNetClient) && target.Method == "DisconnectInternal");
            var hook = type.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic);
            Check(hook.GetCustomAttribute<HarmonyPostfix>() != null);
            int before = ShellDebugRuntime.Stops; hook.Invoke(null,null); Check(ShellDebugRuntime.Stops == before+1);
        });
        Test("application quit hook installs once across repeated menu and lobby lifetimes", () =>
        {
            int before = UnityEngine.Application.Subscriptions;
            for(int i=0;i<1000;i++) DebugRuntimeLifetime.Install();
            Check(UnityEngine.Application.Subscriptions == before+1);
            int stops = ShellDebugRuntime.Stops; UnityEngine.Application.Quit(); Check(ShellDebugRuntime.Stops == stops+1);
        });
        Test("repeated disconnect adapters keep requesting cleanup without duplicate registration", () =>
        {
            var hook = typeof(DebugRuntimeLifetime).GetNestedType("DisconnectPatch",BindingFlags.NonPublic).GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic);
            int subscriptions = UnityEngine.Application.Subscriptions, stops = ShellDebugRuntime.Stops;
            for(int i=0;i<50;i++){ DebugRuntimeLifetime.Install(); hook.Invoke(null,null); }
            Check(ShellDebugRuntime.Stops == stops+50 && UnityEngine.Application.Subscriptions == subscriptions);
        });
    }
}
