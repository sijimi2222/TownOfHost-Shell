using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Impostor;

namespace TownOfHost.Roles.Crewmate;

// ナイスハーミット: ベントに入るとタスクモード/テレポートモードを切り替える(切り替えの仕組みはポリスと同じ)。
// タスクはタスクモードの時だけできる。テレポートモードではペットで装填して方向を選び(ノヴァと同じ照準)、ファントムで発射する。
// 指定した方向に空間があれば一番近い空間へテレポートし、無ければ自殺する。
public sealed class NiceHermit : RoleBase, IKiller, IUsePhantomButton
{
    bool IKiller.UsesDirectKillCooldown => false;
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(NiceHermit),
            player => new NiceHermit(player),
            CustomRoles.NiceHermit,
            () => RoleTypes.Engineer,
            CustomRoleTypes.Crewmate,
            999000,
            SetupOptionItem,
            "nhm",
            "#8fb8de",
            (0, -1),
            true,
            introSound: () => GetIntroSound(RoleTypes.Crewmate),
            from: From.TownOfHost_Shell,
            isNewRole: true
        );

    public NiceHermit(PlayerControl player) : base(RoleInfo, player, () => HasTask.True)
    {
        usesLeft = OptionUseCount.GetInt();
        warp = new HermitWarp(player);
    }

    static OptionItem OptionAbilityCooldown, OptionUseCount, OptionMinDistance, OptionMaxDistance, OptionWallOnly;
    const float ModeSwitchCooldown = 1f;

    readonly HermitWarp warp;
    bool teleportMode;
    int usesLeft; // 0 は無制限

    static void SetupOptionItem()
    {
        OptionAbilityCooldown = FloatOptionItem.Create(RoleInfo, 10, EvilHermit.OptionName.HermitAbilityCooldown, new(0f, 255f, 1f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionUseCount = IntegerOptionItem.Create(RoleInfo, 11, EvilHermit.OptionName.HermitUseCount, new(0, 99, 1), 0, false)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
        OptionMinDistance = FloatOptionItem.Create(RoleInfo, 12, EvilHermit.OptionName.HermitMinDistance, new(0f, 10f, 0.5f), 1f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionMaxDistance = FloatOptionItem.Create(RoleInfo, 13, EvilHermit.OptionName.HermitMaxDistance, new(0.5f, 30f, 0.5f), 6f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionWallOnly = BooleanOptionItem.Create(RoleInfo, 14, EvilHermit.OptionName.HermitWallOnly, false, false);
    }

    bool Unlimited => OptionUseCount.GetInt() == 0;
    bool HasUses => Unlimited || usesLeft > 0;

    public float CalculateKillCooldown() => 999f;
    public bool CanUseKillButton() => false;
    public bool CanUseSabotageButton() => false;
    // テレポートモード(ファントム)ではインポスターのベントボタンで、タスクモード(エンジニア)ではエンジニアのベントで切り替える
    public bool CanUseImpostorVentButton() => teleportMode;
    public override bool CanTask() => !teleportMode;
    public override bool CanUseAbilityButton() => teleportMode;
    bool IUsePhantomButton.IsPhantomRole => teleportMode;
    bool IUsePhantomButton.IsresetAfterKill => false;
    public override RoleTypes? AfterMeetingRole => teleportMode ? RoleTypes.Phantom : RoleTypes.Engineer;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(false);
        if (teleportMode) AURoleOptions.PhantomCooldown = OptionAbilityCooldown.GetFloat();
        else
        {
            AURoleOptions.EngineerCooldown = ModeSwitchCooldown;
            AURoleOptions.EngineerInVentMaxTime = 0f;
        }
    }

    public override void Add()
    {
        teleportMode = false;
        PetActionManager.Register(Player.PlayerId, OnPet);
    }
    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
        warp.Cancel();
    }

    // ベントに入るとモードを切り替える(ベントには入らずに追い出される)
    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive()) return false;
        warp.Cancel();
        SwitchMode(!teleportMode);
        SendRpc();
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
        return false;
    }

    // ポリスの切り替えと同じ。テレポートモードの間は、自分からインポスターが仲間に見えないようにする
    void SwitchMode(bool toTeleport)
    {
        teleportMode = toTeleport;
        if (!Player.IsAlive()) return;
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            var role = pc.GetCustomRole();
            if (role.IsImpostor())
                pc.RpcSetRoleDesync(toTeleport ? RoleTypes.Scientist : role.GetRoleTypes(), Player.GetClientId());
            if (Is(pc))
                pc.RpcSetRoleDesync(toTeleport ? RoleTypes.Phantom : RoleTypes.Engineer, Player.GetClientId());
        }
        Player.MarkDirtySettings();
        _ = new LateTask(() =>
        {
            if (!Player.IsAlive() || teleportMode != toTeleport) return;
            if (toTeleport) AURoleOptions.PhantomCooldown = OptionAbilityCooldown.GetFloat();
            Player.RpcResetAbilityCooldown(Sync: true);
        }, 0.1f, "NiceHermit.ModeReset", true);
    }

    void OnPet()
    {
        if (!AmongUsClient.Instance.AmHost || !teleportMode || !Player.IsAlive() || !GameStates.IsInTask || GameStates.CalledMeeting) return;
        if (warp.Loaded) warp.Cancel();
        else if (!HasUses || !warp.Load()) return;
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
        if (!AmongUsClient.Instance.AmHost || !teleportMode || !Player.IsAlive() || !warp.Loaded || !HasUses) return;
        if (warp.OnLadder) { warp.Cancel(); UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player); return; }
        ResetCooldown = true;
        if (!Unlimited) usesLeft--;
        EvilHermit.Shoot(Player, warp, OptionMinDistance.GetFloat(), OptionMaxDistance.GetFloat(), OptionWallOnly.GetBool(), nameof(NiceHermit));
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || player.PlayerId != Player.PlayerId || !warp.Loaded) return;
        if (!Player.IsAlive() || !teleportMode || warp.OnLadder || GameStates.CalledMeeting)
        {
            warp.Cancel();
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
            return;
        }
        if (warp.UpdateAim()) UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    public override void OnStartMeeting() => warp.Cancel();
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => warp.Cancel();

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seer == null || seen != seer || seer.PlayerId != Player.PlayerId || isForMeeting || !Player.IsAlive()) return "";
        if (!teleportMode) return $"<{RoleInfo.RoleColorCode}>{Translator.GetString("NiceHermitTaskMode")}</color>";
        return EvilHermit.LowerText(Player, warp, seer, seen, isForMeeting, HasUses, RoleInfo.RoleColorCode);
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => Unlimited ? "" : Utils.ColorString(RoleInfo.RoleColor, $"({usesLeft})");

    public override string GetAbilityButtonText() => Translator.GetString("HermitFire");

    void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(teleportMode);
    }
    public override void ReceiveRPC(MessageReader reader) => teleportMode = reader.ReadBoolean();
}
