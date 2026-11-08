using System;
using System.Text;
using System.Linq;
using HarmonyLib;
using Hazel;
using InnerNet;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TownOfHost.Modules;
using TownOfHost.Roles.Impostor;
namespace TownOfHost.Development;

[HarmonyPatch]
internal static class ShellDebugTraffic
{
    [ThreadStatic]private static string category;
    private sealed class Scope : IDisposable
    {
        private readonly string previous;
        internal Scope(string next){previous=category;category=next;}
        public void Dispose(){category=previous;}
    }
    private sealed class Empty : IDisposable{public void Dispose(){}}
    private static readonly Empty empty=new();
    internal static bool RawHookInstalled=>Harmony.GetPatchInfo(AccessTools.Method(typeof(InnerNetClient),"Connection_DataSentRaw"))?.Postfixes.Any(p=>p.PatchMethod.DeclaringType==typeof(ShellDebugTraffic))==true;
    internal static bool ErrorHookInstalled=>Harmony.GetPatchInfo(AccessTools.Method(typeof(Connection),nameof(Connection.Send),new[]{typeof(MessageWriter)}))?.Postfixes.Any(p=>p.PatchMethod.DeclaringType==typeof(ShellDebugTraffic))==true;
    internal static IDisposable Begin(CustomNetObject owner,string phase)=>ShellDebugRuntime.Capture&&owner is NovaBeamTile?new Scope(phase):empty;
    internal static IDisposable Name(CustomNetObject owner,string name)
    {
        if(!ShellDebugRuntime.Capture||owner is not NovaBeamTile)return empty;
        ShellDebugRuntime.Traffic.Name(Encoding.UTF8.GetByteCount(name??""));
        return new Scope(string.IsNullOrEmpty(name)?"hide":name.Contains("<nobr>")?"beam":"guide");
    }
    [HarmonyPatch(typeof(InnerNetClient),"Connection_DataSentRaw"),HarmonyPostfix]
    private static void Raw([HarmonyArgument(1)]int length)
    {try{if(ShellDebugRuntime.Capture)ShellDebugRuntime.Traffic.Raw(length,ShellDebugRuntime.Now);}catch{} }
    [HarmonyPatch(typeof(InnerNetClient),nameof(InnerNetClient.SendOrDisconnect)),HarmonyPrefix,HarmonyPriority(-2000)]
    private static void CnoRequest([HarmonyArgument(0)]MessageWriter message,bool __runOriginal)
    {try{if(__runOriginal&&message!=null&&category!=null&&ShellDebugRuntime.Capture)ShellDebugRuntime.Traffic.Request(category,message.Length);}catch{} }
    [HarmonyPatch(typeof(Connection),nameof(Connection.Send)),HarmonyPostfix]
    private static void SendResult(SendErrors __result)
    {try{if(ShellDebugRuntime.Capture&&__result!=SendErrors.None)ShellDebugRuntime.Traffic.Error();}catch{} }
    [HarmonyPatch(typeof(Connection),nameof(Connection.Send)),HarmonyFinalizer]
    private static Exception SendException(Exception __exception)
    {try{if(ShellDebugRuntime.Capture&&__exception!=null)ShellDebugRuntime.Traffic.Error();}catch{}return __exception;}
}
