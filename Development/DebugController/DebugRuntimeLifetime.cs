using System;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace TownOfHost.Development;

// v19s InnerNetClient.OnDestroy is a short branch/tail-call native method. A
// detour there executed its INT3 padding on title return in the captured game.
// Keep that original method untouched; release debug controls on the existing
// disconnect path and on application shutdown instead.
internal static class DebugRuntimeLifetime
{
    private static bool installed;
    internal static void Install()
    {
        if (installed) return;
        Application.quitting += new Action(ShellDebugRuntime.Stop);
        installed = true;
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.DisconnectInternal))]
    private static class DisconnectPatch
    {
        [HarmonyPostfix]
        private static void Postfix() => ShellDebugRuntime.Stop();
    }
}
