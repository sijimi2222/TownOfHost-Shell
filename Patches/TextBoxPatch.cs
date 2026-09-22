using HarmonyLib;
using UnityEngine;

namespace TownOfHost;

public static class ShellTextBoxPatch
{
    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.IsCharAllowed))]
    [HarmonyPrefix]
    public static bool ValidateChatCharacter(
        [HarmonyArgument(0)] char c,
        ref bool __result)
    {
        if (!string.IsNullOrEmpty(Input.compositionString))
        {
            __result = true;
            return false;
        }

        __result = c is not ('\b' or '\r' or '[');
        return false;
    }
}