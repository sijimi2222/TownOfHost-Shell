using System;using TownOfHost;using TownOfHost.Roles.Core;namespace AuditDefs{public static class Destroyer {public static SimpleRoleInfo RoleInfo;
public static OptionItem OptKillCooldown;
public static OptionItem OptShowKillFlash;
public static OptionItem OptCrushTime;
public static OverrideKilldistance KillDistanceOption;

    private sealed class DestroyerCooldownRule : FloatValueRule
    {
        public DestroyerCooldownRule() : base(20f, 50f, 1f) { }
        public override int RepeatIndex(int value)
            => System.Math.Clamp(value, 0, (int)((MaxValue - MinValue) / Step));
    }
public static void Initialize()
    {
        OptKillCooldown = FloatOptionItem.Create(
    RoleInfo,
    10,
    GeneralOption.KillCooldown,
    new DestroyerCooldownRule(),
    40f,
    false
).SetValueFormat(OptionFormat.Seconds)
 .SetOptionName(() => "キルクール");

        OptShowKillFlash = BooleanOptionItem.Create(
    RoleInfo,
    11,
    GeneralOption.TaskAwakening,
    true,
    false
).SetOptionName(() => "押し潰しが成功した場合キルフラッシュを鳴らす");

        OverrideKilldistance.Create(RoleInfo, 12);

        OptCrushTime = FloatOptionItem.Create(
    RoleInfo,
    13,
    GeneralOption.TaskAwakening,
    new(3f, 15f, 1f),
    5f,
    false
).SetValueFormat(OptionFormat.Seconds)
 .SetOptionName(() => "押し潰すのに必要な時間");


    }
}}