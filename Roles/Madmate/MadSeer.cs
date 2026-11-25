using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Options;

namespace TownOfHost.Roles.Madmate;

// マッドシーア: 他人に間違ったキルフラッシュを見せて混乱を生むマッドメイト。
// ・間違った兆候: ベントに入ると、自分以外の生存者全員にキルフラッシュを見せる(本人はすぐベントから追い出される)。
// ・正しい兆候: 誰かが死亡すると、自身がキルフラッシュを見る。
// 通信妨害中はベントに入れない(フラッシュも起きない)。
public sealed class MadSeer : RoleBase, IKillFlashSeeable
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(MadSeer), player => new MadSeer(player), CustomRoles.MadSeer,
        () => RoleTypes.Engineer, CustomRoleTypes.Madmate, 997000, SetupOptionItem,
        "msee", "#FF1919", (0, -2),
        introSound: () => GetIntroSound(RoleTypes.Impostor), from: From.TownOfHost_Shell, isNewRole: true);

    private static OptionItem optionVentCooldown;
    private static OptionItem optionCanSeeKillFlashInComms;

    enum OptionName { VentCooldown, MadSeerCanSeeKillFlashInComms }

    public MadSeer(PlayerControl player) : base(RoleInfo, player, () => HasTask.False) { }

    private static void SetupOptionItem()
    {
        optionVentCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.VentCooldown, new(0.5f, 180f, 0.5f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        optionCanSeeKillFlashInComms = BooleanOptionItem.Create(RoleInfo, 11, OptionName.MadSeerCanSeeKillFlashInComms, false, false);
    }

    private static bool CommsActive => Utils.IsActive(SystemTypes.Comms);

    // 通信妨害中はベントボタン自体を押せない(MODクライアント向けの表示。実際の判定は OnEnterVent)
    public override bool CanClickUseVentButton => !CommsActive;

    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        // 通信妨害中はベントに入れない(間違った兆候も起きない)
        if (CommsActive) return false;
        if (!GameStates.IsInTask || GameStates.CalledMeeting) return false;

        // 間違った兆候: 自分以外の生存者全員にキルフラッシュを見せる
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (pc.PlayerId == Player.PlayerId) continue;
            pc.KillFlash();
        }
        Logger.Info($"{Player.Data?.GetLogPlayerName()}がベントに入り、偽のキルフラッシュを発生させました", "MadSeer");

        // この役職はベントに入っても即座に追い出される
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = optionVentCooldown.GetFloat();
    }

    // 正しい兆候: 誰かが死亡すると自身がキルフラッシュを見る(通信妨害中は設定次第)
    public bool? CheckKillFlash(MurderInfo info) => !CommsActive || optionCanSeeKillFlashInComms.GetBool();

    public override CustomRoles TellResults(PlayerControl player) => MadTellOpt();
}
