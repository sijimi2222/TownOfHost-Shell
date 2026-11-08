// Verbatim property extraction from Modules/GameState.cs; game dependencies are substituted.
namespace TownOfHost;
public partial class PlayerState
{
private bool baseBlackOut;
        public bool IsBlackOut
        {
            get => baseBlackOut || TownOfHost.Roles.Neutral.Hunter.IsDarkened(PlayerId);
            set => baseBlackOut = value;
        }
}
