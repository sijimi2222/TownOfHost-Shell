using HarmonyLib;

namespace TownOfHost;

// Only lifecycle/UI hooks live here; host action checks also use the existing command handlers.
[HarmonyPatch]
static class NaturalDisastersPatch
{
    [HarmonyPostfix, HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    static void Tick(PlayerControl __instance)
    {
        if (__instance == PlayerControl.LocalPlayer && !__instance.notRealPlayer) NaturalDisasters.FixedUpdate();
    }

    [HarmonyPostfix, HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
    static void PlayerLeft() => NaturalDisasters.OnPlayerLeft();

    [HarmonyPrefix, HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
    static void RecordLeft([HarmonyArgument(0)] InnerNet.ClientData data)
    {
        if (data?.Character != null) NaturalDisasters.RecordSurvivalStop(data.Character.PlayerId, true);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerState), nameof(PlayerState.SetDead))]
    static void RecordDeath(byte ___PlayerId) => NaturalDisasters.RecordSurvivalStop(___PlayerId);

    [HarmonyPrefix, HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
    static void End() => NaturalDisasters.ResetWithReason("AmongUsClient.OnGameEnd");

    [HarmonyPostfix, HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]
    static void Disconnect() => NaturalDisasters.ResetWithReason("AmongUsClient.OnDisconnected");

    [HarmonyPostfix, HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
    static void Lobby() => NaturalDisasters.ResetWithReason("LobbyBehaviour.Start");

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
    static bool Meeting() => !NaturalDisasters.IsThisMode;

    [HarmonyPrefix, HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CloseDoorsOfType))]
    static bool Doors() => !NaturalDisasters.IsThisMode;

    [HarmonyPostfix, HarmonyPriority(Priority.Last), HarmonyPatch(typeof(TaskPanelBehaviour), nameof(TaskPanelBehaviour.SetTaskText))]
    static void TaskRoster(TaskPanelBehaviour __instance) => NaturalDisasters.AppendSurvivalRoster(__instance);
    [HarmonyPostfix, HarmonyPriority(Priority.Last), HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    static void Hud(HudManager __instance)
    {
        NaturalDisasters.AppendSurvivalRoster(__instance?.TaskPanel);
        if (!NaturalDisasters.IsThisMode || !GameStates.InGame || !GameStates.introDestroyed) return;
        __instance.KillButton?.ToggleVisible(false);
        __instance.ReportButton?.ToggleVisible(false);
        __instance.SabotageButton?.ToggleVisible(false);
        __instance.ImpostorVentButton?.ToggleVisible(false);
        __instance.Chat?.SetVisible(false);
    }
}
