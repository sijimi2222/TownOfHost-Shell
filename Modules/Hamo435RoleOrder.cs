using System.Collections.Generic;
using TownOfHost.Roles.Core;
namespace TownOfHost;

// Import order is presentation metadata. Never renumber roles/options to reorder the UI.
internal static class Hamo435RoleOrder
{
    internal static readonly Dictionary<CustomRoles, int> Priority = new()
    {
        [CustomRoles.Trauma] = -100,
        [CustomRoles.Surmount] = -101,
        [CustomRoles.Messenger] = -102,
        [CustomRoles.Fugitive] = -103,
        [CustomRoles.MemoryRestorer] = -104,
        [CustomRoles.ArchVillain] = -105,
        [CustomRoles.LonelyWolf] = -106,
        [CustomRoles.Clockworker] = -107,
        [CustomRoles.Abductor] = -108,
        [CustomRoles.MadInvisible] = -109,
        [CustomRoles.MadMagic] = -110,
        [CustomRoles.Flatfish] = -111,
        [CustomRoles.Lupin] = -112,
        [CustomRoles.Daoshi] = -113,
        [CustomRoles.Catalyst] = -114,
        [CustomRoles.Conductor] = -115,
        [CustomRoles.Sender] = -116,
        [CustomRoles.MadSender] = -117,
        [CustomRoles.Parity] = -118,
        [CustomRoles.Conjurer] = -119,
        [CustomRoles.Rocket] = -120,
        [CustomRoles.Surviving] = -121,
        [CustomRoles.Berserker] = -122,
        [CustomRoles.ToughGuy] = -123,
        [CustomRoles.Comorbidity] = -124,
        [CustomRoles.Murder] = -125,
        [CustomRoles.BetrayGuesser] = -126,
    };
    internal static bool Contains(CustomRoles role) => Priority.ContainsKey(role);
}
