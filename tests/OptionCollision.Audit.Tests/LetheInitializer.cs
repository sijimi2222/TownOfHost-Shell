using System;using TownOfHost;using TownOfHost.Roles.Core;namespace AuditDefs{public static class Lethe {public static SimpleRoleInfo RoleInfo;
public static OptionItem OptNeedTasks;
public static OptionItem OptRequiredTaskCount;
public static OptionItem OptHideUntilTasks;
public static OptionItem OptGiveSealer;
public static OptionItem OptGiveSecurer;
public static OptionItem OptGiveInfoPoor;
public static OptionItem OptGiveNotvoter;
public static OptionItem OptGiveSlowStarter;

public static void Initialize()
    {
        // タスク完了まで能力を使えない
        OptNeedTasks = BooleanOptionItem.Create(
            RoleInfo,
            10,
            GeneralOption.TaskAwakening,
            true,
            false
        ).SetOptionName(() => "タスクを完了させるまで能力が発動できない");

        // 必要タスク数 1～10
        OptRequiredTaskCount = IntegerOptionItem.Create(
            RoleInfo,
            11,
            GeneralOption.AwakeningTaskcount,
            new(1, 10, 1),
            1,
            false,
            OptNeedTasks
        ).SetOptionName(() => "発動までのタスク数");

        // タスク完了まで自覚できない
        OptHideUntilTasks = BooleanOptionItem.Create(
            RoleInfo,
            12,
            GeneralOption.TaskAwakening,
            true,
            false,
            OptNeedTasks
        ).SetOptionName(() => "タスクを完了させるまで自覚できない");

        // シーラー
        OptGiveSealer = BooleanOptionItem.Create(
            RoleInfo,
            13,
            GeneralOption.TaskAwakening,
            true,
            false
        ).SetOptionName(() => "対象にシーラーを付与する");

        // セキュアラー
        OptGiveSecurer = BooleanOptionItem.Create(
            RoleInfo,
            14,
            GeneralOption.TaskAwakening,
            true,
            false
        ).SetOptionName(() => "対象にセキュアラーを付与する");

        // インフォプアー
        OptGiveInfoPoor = BooleanOptionItem.Create(
            RoleInfo,
            15,
            GeneralOption.TaskAwakening,
            true,
            false
        ).SetOptionName(() => "対象にインフォプアーを付与する");

        // ノットヴォウター
        OptGiveNotvoter = BooleanOptionItem.Create(
            RoleInfo,
            16,
            GeneralOption.TaskAwakening,
            true,
            false
        ).SetOptionName(() => "対象にノットヴォウターを付与する");

        // スロースターター
        OptGiveSlowStarter = BooleanOptionItem.Create(
            RoleInfo,
            17,
            GeneralOption.TaskAwakening,
            true,
            false
        ).SetOptionName(() => "対象にスロースターターを付与する");
    }
}}