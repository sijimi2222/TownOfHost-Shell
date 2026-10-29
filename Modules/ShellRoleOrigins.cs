using System.Collections.Generic;
using TownOfHost.Roles.Core;

namespace TownOfHost;

/// <summary>
/// Shell-owned attribution overrides. Keep these separate from upstream role definitions.
/// This metadata does not select settings groups, order, IDs or gameplay behavior.
/// </summary>
internal static class ShellRoleOrigins
{
    internal const string HamoDisplayName = "TownOfHost-hamo";
    internal const string TempDisplayName = "TownOfHost_Temp";
    // Attribution colors only; never change a role's own color.
    internal const string HamoTextColor = "#fb85ff";
    internal const string TempTextColor = "#ffeb99";

    // Only the explicitly requested roles/addons and newly imported hamo roles belong here.
    private static readonly HashSet<CustomRoles> HamoRoles = new()
    {
        CustomRoles.Catalyst,
        CustomRoles.Conductor,
        CustomRoles.Trauma,
        CustomRoles.Surmount,
        CustomRoles.MemoryRestorer,
        CustomRoles.Sender,
        CustomRoles.Fugitive,
        CustomRoles.Messenger,
        CustomRoles.ArchVillain,
        CustomRoles.LonelyWolf,
        CustomRoles.Clockworker,
        CustomRoles.Abductor,
        CustomRoles.MadInvisible,
        CustomRoles.MadMagic,
        CustomRoles.MadSender,
        CustomRoles.Parity,
        CustomRoles.Lupin,
        CustomRoles.Daoshi,
        CustomRoles.Flatfish,
        CustomRoles.Surviving,
        CustomRoles.Berserker,
        CustomRoles.ToughGuy,
        CustomRoles.Comorbidity,
        CustomRoles.Murder,
        CustomRoles.BetrayGuesser,
        CustomRoles.Experiencer,
    };

    private static readonly HashSet<CustomRoles> TempRoles = new()
    {
        CustomRoles.StepBomber,
        CustomRoles.Locksmith,
        CustomRoles.Coroner,
    };

    internal static bool HasOverride(CustomRoles role) => HamoRoles.Contains(role) || TempRoles.Contains(role);

    internal static From Resolve(CustomRoles role, From original)
        => TempRoles.Contains(role) ? From.TownOfHost_Temp
            : HamoRoles.Contains(role) ? From.TownOfHost_hamo : original;
}
