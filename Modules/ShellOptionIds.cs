using System;
using System.Collections.Generic;
using TownOfHost.Roles.Core;

namespace TownOfHost;

// Recovery IDs for options which never entered AllOptions because their legacy IDs collided.
// ConfigId remains the role/preset reference identity; it is not changed by this mapping.
internal static class ShellOptionIds
{
    internal const int LetheMaximum = 1_800_000;
    internal const int LetheRequiredTaskCount = 1_800_001;
    internal const int LetheHideUntilTasks = 1_800_002;
    internal const int LetheGiveSealer = 1_800_003;
    internal const int LetheGiveSecurer = 1_800_004;
    internal const int ShrineMaidenSpawn = 1_800_005;
    internal const int ShrineMaidenMaximum = 1_800_006;

    internal static IReadOnlyList<int> RecoveredOptionIds { get; } = Array.AsReadOnly(new[]
    {
        LetheMaximum, LetheRequiredTaskCount, LetheHideUntilTasks, LetheGiveSealer,
        LetheGiveSecurer, ShrineMaidenSpawn, ShrineMaidenMaximum
    });

    internal static bool IsRecoveredOption(int id) =>
        id is >= LetheMaximum and <= ShrineMaidenMaximum;

    internal static int SpawnChanceId(CustomRoles role, int legacyId) =>
        role == CustomRoles.ShrineMaiden ? ShrineMaidenSpawn : legacyId;

    internal static int MaximumId(CustomRoles role, int legacyId) => role switch
    {
        CustomRoles.Lethe => LetheMaximum,
        CustomRoles.ShrineMaiden => ShrineMaidenMaximum,
        _ => legacyId
    };
}
