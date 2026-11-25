using AmongUs.GameOptions;
using UnityEngine;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;

namespace TownOfHost.Roles.Neutral;

// ジャーマフォビア: 視界内に死体が写ると死亡し、生存していれば単独勝利する。
public sealed class Germaphobia : RoleBase, IAdditionalWinner, ISystemTypeUpdateHook
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Germaphobia),
            player => new Germaphobia(player),
            CustomRoles.Germaphobia,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            996000,
            SetupOptionItem,
            "gmp",
            "#7FD8E8",
            OptionSort: (0, -7),
            from: From.TownOfHost_Shell,
            isNewRole: true
        );

    public Germaphobia(PlayerControl player)
    : base(RoleInfo, player, () => HasTask.False)
    {
        checkTimer = 0f;
    }

    static OptionItem OptionCantFixSabotage;
    enum OptionName { GermaphobiaCantFixSabotage }

    // FindObjectsOfType を毎フレーム呼ばないよう、判定は一定間隔で行う。
    const float CheckInterval = 0.1f;
    float checkTimer;

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 15, rule: new(1, 50, 1));
        OptionCantFixSabotage = BooleanOptionItem.Create(RoleInfo, 10, OptionName.GermaphobiaCantFixSabotage, true, false);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !Is(player)) return;
        if (Player == null || Player.Data == null || Player.Data.Disconnected || !Player.IsAlive()) return;
        if (!GameStates.IsInTask || GameStates.Intro || GameStates.IsMeeting || GameStates.CalledMeeting) return;
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;

        checkTimer -= Time.fixedDeltaTime;
        if (checkTimer > 0f) return;
        checkTimer = CheckInterval;

        var body = FindVisibleBody();
        if (body == null) return;

        Logger.Info($"{Player.GetNameWithRole().RemoveHtmlTags()} が死体(Id:{body.ParentId})を視界に入れたため死亡", "Germaphobia");
        CustomRoleManager.OnCheckMurder(Player, Player, Player, Player,
            true, true, 99, CustomDeathReason.Suicide);
    }

    // 視界半径内にあり、視界を遮る壁(ShadowMask)が間に無い死体を探す。
    DeadBody FindVisibleBody()
    {
        if (ShipStatus.Instance == null) return null;
        float radius = ShipStatus.Instance.CalculateLightRadius(Player.Data);
        if (!float.IsFinite(radius) || radius <= 0f) return null;
        var from = Player.GetTruePosition();

        foreach (var body in Object.FindObjectsOfType<DeadBody>())
        {
            if (body == null || !body.gameObject.activeInHierarchy) continue;
            if (body.ParentId == Player.PlayerId) continue;
            // 掃除済み・捕食済みの死体は見えない扱い。
            if (MadCleaner.IsCleaned(body.ParentId) || Tiger.IsPredatedBody(body.ParentId)) continue;

            var to = body.TruePosition;
            if (Vector2.Distance(from, to) > radius) continue;
            if (PhysicsHelpers.AnythingBetween(from, to, Constants.ShadowMask, false)) continue;
            return body;
        }
        return null;
    }

    public bool CheckWin(ref CustomRoles winnerRole)
    {
        if (!Player.IsAlive()) return false;
        winnerRole = CustomRoles.Germaphobia;
        // 勝利優先度が現在の勝者より高ければ単独勝利、同じなら共に勝利、低ければ勝利しない。
        return CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Germaphobia, Player.PlayerId, true);
    }

    bool CanFixSabotage => !OptionCantFixSabotage.GetBool();
    bool ISystemTypeUpdateHook.UpdateReactorSystem(ReactorSystemType system, byte amount) => CanFixSabotage;
    bool ISystemTypeUpdateHook.UpdateHeliSabotageSystem(HeliSabotageSystem system, byte amount) => CanFixSabotage;
    bool ISystemTypeUpdateHook.UpdateLifeSuppSystem(LifeSuppSystemType system, byte amount) => CanFixSabotage;
    bool ISystemTypeUpdateHook.UpdateHqHudSystem(HqHudSystemType system, byte amount) => CanFixSabotage;
    bool ISystemTypeUpdateHook.UpdateSwitchSystem(SwitchSystem system, byte amount) => CanFixSabotage;
    bool ISystemTypeUpdateHook.UpdateHudOverrideSystem(HudOverrideSystemType system, byte amount) => CanFixSabotage;
}
