using HarmonyLib;

namespace TownOfHost;

public static class ShellTextBoxPatch
{
    private static string currentInput = "";
    private static int currentIndex = 0;
    private static bool isChatInput = false;

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetText))]
    [HarmonyPrefix]
    public static void SetTextPrefix(
        TextBoxTMP __instance,
        [HarmonyArgument(0)] string input)
    {
        isChatInput =
            HudManager.InstanceExists &&
            HudManager.Instance.Chat != null &&
            HudManager.Instance.Chat.freeChatField.textArea == __instance;

        if (!isChatInput) return;

        currentInput = input ?? "";
        currentIndex = 0;
    }

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.SetText))]
    [HarmonyPostfix]
    public static void SetTextPostfix()
    {
        currentInput = "";
        currentIndex = 0;
        isChatInput = false;
    }

    [HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.IsCharAllowed))]
    [HarmonyPrefix]
    public static bool ValidateChatCharacter(
        [HarmonyArgument(0)] char c,
        ref bool __result)
    {
        if (!isChatInput)
            return true;

        char actualChar = c;

        if (currentIndex < currentInput.Length)
        {
            actualChar = currentInput[currentIndex];
            currentIndex++;
        }

        // 本物のBackspace・Enter・[ だけ拒否
        __result = actualChar is not ('\b' or '\r' or '[');
        return false;
    }
}