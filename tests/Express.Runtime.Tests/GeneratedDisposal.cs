// Actual production RoleBase.Dispose; source parity checked by Program.
namespace TownOfHost.Roles.Core;
public abstract partial class RoleBase
{
    public void Dispose()

    {

        OnDestroy();

        CustomRoleManager.AllActiveRoles.Remove(Player.PlayerId);

        Player = null;

    }
}
