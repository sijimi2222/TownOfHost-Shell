using AmongUs.GameOptions;
using UnityEngine;
using TownOfHost.Modules;

namespace TownOfHost.Roles.Core;

public sealed class PetModeSwitch
{
    private readonly PlayerControl player;
    private readonly RoleBase owner;
    public bool TaskMode { get; private set; }

    public PetModeSwitch(RoleBase owner) { this.owner = owner; player = owner.Player; }
    public void Dispose() => PrivateRoleStateSync.Unregister(owner);

    public static bool Enabled => Roles.Crewmate.Sheriff.StartInTaskMode?.GetBool() == true;

    public void Start()
    {
        PrivateRoleStateSync.Register(owner, reader => TaskMode = reader.ReadBoolean());
        if (!AmongUsClient.Instance.AmHost || !Enabled) { TaskMode = Enabled; return; }
        Set(true);
    }

    public bool Toggle()
    {
        if (!AmongUsClient.Instance.AmHost || !Enabled || player == null || !ReferenceEquals(player.GetRoleClass(), owner) || !player.IsAlive() || !GameStates.IsInTask || GameStates.CalledMeeting) return false;
        Set(!TaskMode);
        return true;
    }

    public void Set(bool taskMode)
    {
        if (player == null || !ReferenceEquals(player.GetRoleClass(), owner)) return;
        TaskMode = taskMode;
        if (!AmongUsClient.Instance.AmHost || player == null || !player.IsAlive()) return;
        var role = taskMode ? RoleTypes.Crewmate : RoleTypes.Impostor;
        if (player == PlayerControl.LocalPlayer) RoleManager.Instance.SetRole(PlayerControl.LocalPlayer, role);
        else
        {
            var clientId = player.GetClientId();
            if (clientId != -1) player.RpcSetRoleDesync(role, clientId);
        }
        PrivateRoleStateSync.Send(owner, writer => writer.Write(TaskMode));
        if (!taskMode) player.SetKillCooldown(delay: true);
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: player);
    }

    public string ModeText()
        => !Enabled ? "" : Utils.ColorString(Color.yellow, " [" + Translator.GetString(TaskMode ? "PetModeSwitchTask" : "PetModeSwitchCombat") + "]");
}
