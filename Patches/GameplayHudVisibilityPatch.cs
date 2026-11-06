using HarmonyLib;
using UnityEngine;

namespace TownOfHost;

// Selectively incorporates hamo's chat/match-info correction, preserving Shell's HELP guide.
[HarmonyPatch]
internal static class GameplayHudVisibilityPatch
{
    internal static bool OptionsOpen(HudManager hud) => hud != null && hud.GameMenu != null && hud.GameMenu.IsOpen;

    internal static void Apply(HudManager hud)
    {
        if (hud == null || !hud.gameObject.activeInHierarchy || !GameStates.IsModHost) return;
        var player = PlayerControl.LocalPlayer;
        if (player == null || player.Data == null || player.Data.Disconnected) return;
        if (!GameStates.IsLobby && !GameStates.IsInGame) return;

        // Settings/HUD reactivation must not leave the native match-info entry in the task HUD.
        if (GameStates.IsInGame && !GameStates.IsLobby && hud.MatchInfoButton != null
            && hud.MatchInfoButton.gameObject.activeSelf)
            hud.MatchInfoButton.gameObject.SetActive(false);

        var chat = hud.Chat;
        var button = chat?.chatButton;
        if (button == null) return;
        bool ghost = GameStates.IsInGame && !GameStates.IsLobby && player.Data.IsDead;
        bool taskChat = GameStates.IsInGame && GameStates.IsInTask && !GameStates.IsMeeting
            && !player.Data.IsDead && !NaturalDisasters.IsThisMode
            && (Options.OptionGameChatSetting?.GetBool() ?? false);
        bool allowed = GameStates.IsLobby || GameStates.IsMeeting || ghost || taskChat;
        if (!allowed)
        {
            if (button.gameObject.activeSelf) button.gameObject.SetActive(false);
        }
        else if ((ghost || taskChat) && !OptionsOpen(hud) && !button.gameObject.activeSelf)
        {
            // Preserve ghost chat and explicitly enabled task/proximity chat, including after settings close.
            chat.SetVisible(true);
            button.gameObject.SetActive(true);
        }
    }

    [HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Close)), HarmonyPostfix]
    static void OptionsClosed(OptionsMenuBehaviour __instance)
    {
        if (!HudManager.InstanceExists) return;
        var hud = HudManager.Instance;
        if (hud == null || hud.GameMenu != __instance) return;
        Apply(hud);
        RoleGuideButtonPatch.UpdateTopRightButtonLayout();
        RoleGuideButtonPatch.UpdateButtonVisibility();
    }
}
