using System;using TownOfHost;using TownOfHost.Roles.Core;namespace AuditDefs{public static class ShrineMaiden {public static SimpleRoleInfo RoleInfo;
public static OptionItem OptAwakening,OptionMaximum,OptionVoteMode,Optioncantaskcount,Option1MeetingMaximum;
enum Option

    {

        TellMaximum,

        AbilityVotemode,

    }
public static void Initialize()

    {

        OptionMaximum = IntegerOptionItem.Create(RoleInfo, 11, Option.TellMaximum, new(1, 99, 1), 1, false)

            .SetValueFormat(OptionFormat.Times);

        OptionVoteMode = StringOptionItem.Create(RoleInfo, 12, Option.AbilityVotemode, EnumHelper.GetAllNames<AbilityVoteMode>(), 1, false);

        Optioncantaskcount = IntegerOptionItem.Create(RoleInfo, 14, GeneralOption.cantaskcount, new(0, 99, 1), 5, false);

        Option1MeetingMaximum = IntegerOptionItem.Create(RoleInfo, 15, GeneralOption.MeetingMaxTime, new(0, 99, 1), 0, false)

            .SetValueFormat(OptionFormat.Times);

        OptAwakening = BooleanOptionItem.Create(RoleInfo, 16, GeneralOption.AbilityAwakening, false, false);

        SaruWarningOption.AddTo(RoleInfo);

    }
}}