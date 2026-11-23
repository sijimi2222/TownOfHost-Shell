// Extracted by RefreshIntegrationFixtures.py; do not edit.
using TownOfHost.Patches;namespace TownOfHost;internal static class RealPetDispatch{
internal static void OnPetUse(PlayerControl pc)
    {
        if (pc == null || !pc.IsAlive()) return;
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsLobby || GameStates.IsMeeting) return;

        if (pc.inVent || pc.inMovingPlat || pc.onLadder || pc.walkingToVent) return;
        if (pc.MyPhysics.Animations.IsPlayingEnterVentAnimation()) return;
        if (pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation()) return;

        if (PetActionManager.Handlers.TryGetValue(pc.PlayerId, out var handler))
        {
            handler.Invoke();
            Logger.Info($"{pc.Data?.GetLogPlayerName()} のOnPet実行", "PetActionPatch");
        }
    }
}
