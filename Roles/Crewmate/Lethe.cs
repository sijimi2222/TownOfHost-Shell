using System.Collections.Generic;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Crewmate;

public sealed class Lethe : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Lethe),
            player => new Lethe(player),
            CustomRoles.Lethe,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            39990,
            SetupOptionItem,
            "Le",
            "#7f9aa8",
            (0, 7),
            from: From.TownOfHost_Shell
        );

    // ===== 設定 =====

    static OptionItem OptNeedTasks;
    static OptionItem OptRequiredTaskCount;
    static OptionItem OptHideUntilTasks;

    static OptionItem OptGiveSealer;
    static OptionItem OptGiveSecurer;
    static OptionItem OptGiveInfoPoor;
    static OptionItem OptGiveNotvoter;
    static OptionItem OptGiveSlowStarter;

    // 能力が解放されているか
    bool Awakened;

    public Lethe(PlayerControl player)
        : base(RoleInfo, player)
    {
        Awakened = !OptNeedTasks.GetBool();
    }

    private static void SetupOptionItem()
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

    // ===== レテがキルされた時 =====

    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        // タスク条件未達なら発動しない
        if (!Awakened)
            return;

        // 自殺系では発動しない
        if (info.IsSuicide || info.IsFakeSuicide)
            return;

        var (killer, target) = info.AttemptTuple;

        if (killer == null || target == null)
            return;

        // このレテ本人が対象の時だけ
        if (target.PlayerId != Player.PlayerId)
            return;

        // 自分自身によるキル扱いなら発動しない
        if (killer.PlayerId == target.PlayerId)
            return;

        // ONになっているデバフを候補に入れる
        var debuffs = new List<CustomRoles>();

        if (OptGiveSealer.GetBool())
            debuffs.Add(CustomRoles.Sealer);

        if (OptGiveSecurer.GetBool())
            debuffs.Add(CustomRoles.Securer);

        if (OptGiveInfoPoor.GetBool())
            debuffs.Add(CustomRoles.InfoPoor);

        if (OptGiveNotvoter.GetBool())
            debuffs.Add(CustomRoles.Notvoter);

        if (OptGiveSlowStarter.GetBool())
            debuffs.Add(CustomRoles.SlowStarter);

        // 全部OFFなら何もしない
        if (debuffs.Count == 0)
            return;

        // ONになっているデバフからランダムで1つ選ぶ
        var selectedDebuff =
            debuffs[IRandom.Instance.Next(0, debuffs.Count)];

        // キラーに選ばれたデバフを1つだけ付与
        killer.RpcSetCustomRole(selectedDebuff);
    }

    // ===== タスク完了 =====

    public override bool OnCompleteTask(uint taskid)
    {
        // タスク条件自体がOFFなら常に能力使用可能
        if (!OptNeedTasks.GetBool())
        {
            Awakened = true;
            return true;
        }

        // すでに解放済み
        if (Awakened)
            return true;

        // 指定タスク数を達成
        if (
            MyTaskState.HasCompletedEnoughCountOfTasks(
                OptRequiredTaskCount.GetInt()
            )
        )
        {
            Awakened = true;

            // 表示を更新
            Utils.RoleSendList.Add(Player.PlayerId);
        }

        return true;
    }

    // ===== 自覚前の表示 =====

    public override CustomRoles Misidentify()
    {
        if (
            OptNeedTasks.GetBool()
            && OptHideUntilTasks.GetBool()
            && !Awakened
        )
        {
            // 自覚前は普通のクルーメイトに見せる
            return CustomRoles.Crewmate;
        }

        return CustomRoles.NotAssigned;
    }
}