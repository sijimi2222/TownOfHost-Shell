using AmongUs.GameOptions;
using UnityEngine;
using TownOfHost.Modules;
using TownOfHost.Patches;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

// イビルハーミット: ペットで装填して方向を選び(ノヴァと同じ照準)、ファントムで発射する。
// 指定した方向に空間があれば一番近い空間へテレポートし、無ければ自殺する。
public sealed class EvilHermit : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(EvilHermit),
            player => new EvilHermit(player),
            CustomRoles.EvilHermit,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            998000,
            SetupOptionItem,
            "ehm",
            "#ff1919",
            OptionSort: (0, -10),
            from: From.TownOfHost_Shell,
            isNewRole: true
        );

    public EvilHermit(PlayerControl player) : base(RoleInfo, player)
    {
        killCooldown = OptionKillCooldown.GetFloat();
        usesLeft = OptionUseCount.GetInt();
        warp = new HermitWarp(player);
    }

    static OptionItem OptionKillCooldown, OptionAbilityCooldown, OptionUseCount, OptionMinDistance, OptionMaxDistance, OptionWallOnly;
    internal enum OptionName { HermitAbilityCooldown, HermitUseCount, HermitMinDistance, HermitMaxDistance, HermitWallOnly }

    readonly float killCooldown;
    readonly HermitWarp warp;
    // 0 は無制限
    int usesLeft;

    static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 180f, 1f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionAbilityCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.HermitAbilityCooldown, new(0f, 255f, 1f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionUseCount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.HermitUseCount, new(0, 99, 1), 0, false)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
        OptionMinDistance = FloatOptionItem.Create(RoleInfo, 13, OptionName.HermitMinDistance, new(0f, 10f, 0.5f), 1f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionMaxDistance = FloatOptionItem.Create(RoleInfo, 14, OptionName.HermitMaxDistance, new(0.5f, 30f, 0.5f), 6f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OptionWallOnly = BooleanOptionItem.Create(RoleInfo, 15, OptionName.HermitWallOnly, false, false);
    }

    bool Unlimited => OptionUseCount.GetInt() == 0;
    bool HasUses => Unlimited || usesLeft > 0;

    public float CalculateKillCooldown() => killCooldown;
    public override void ApplyGameOptions(IGameOptions opt) => AURoleOptions.PhantomCooldown = OptionAbilityCooldown.GetFloat();

    public override void Add() => PetActionManager.Register(Player.PlayerId, OnPet);
    public override void OnDestroy()
    {
        PetActionManager.Unregister(Player.PlayerId);
        warp.Cancel();
    }

    void OnPet()
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive() || !GameStates.IsInTask || GameStates.CalledMeeting) return;
        // もう一度撫でると装填を取り消す
        if (warp.Loaded) warp.Cancel();
        else if (!HasUses || !warp.Load()) return;
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    bool IUsePhantomButton.IsPhantomRole => true;
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive() || !warp.Loaded || !HasUses) return;
        if (warp.OnLadder) { warp.Cancel(); UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player); return; }
        ResetCooldown = true;
        if (!Unlimited) usesLeft--;
        Shoot(Player, warp, OptionMinDistance.GetFloat(), OptionMaxDistance.GetFloat(), OptionWallOnly.GetBool(), nameof(EvilHermit));
        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
    }

    // 発射の共通処理(ナイスハーミットも使う)。空間が無ければ自殺
    internal static void Shoot(PlayerControl player, HermitWarp warp, float min, float max, bool wallOnly, string tag)
    {
        int shotAngle = warp.ShownAngle;
        if (warp.Fire(min, Mathf.Max(min, max), wallOnly))
        {
            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} が{shotAngle}°の方向へテレポート", tag);
            return;
        }
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} が{shotAngle}°の方向に空間が無く自殺", tag);
        CustomRoleManager.OnCheckMurder(player, player, player, player, true, true, 99, CustomDeathReason.Suicide);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || player.PlayerId != Player.PlayerId || !warp.Loaded) return;
        if (!Player.IsAlive() || warp.OnLadder || GameStates.CalledMeeting)
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
        => LowerText(Player, warp, seer, seen, isForMeeting, HasUses, RoleInfo.RoleColorCode);

    internal static string LowerText(PlayerControl owner, HermitWarp warp, PlayerControl seer, PlayerControl seen, bool isForMeeting, bool hasUses, string color)
    {
        seen ??= seer;
        if (seer == null || seen != seer || seer.PlayerId != owner.PlayerId || isForMeeting || !owner.IsAlive()) return "";
        if (warp.Loaded)
            return $"<{color}>{string.Format(Translator.GetString("HermitLoaded"), warp.ShownAngle, warp.Arrow)}</color>";
        return $"<{color}>{Translator.GetString(hasUses ? "HermitControls" : "HermitNoUses")}</color>";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
        => Unlimited ? "" : Utils.ColorString(RoleInfo.RoleColor, $"({usesLeft})");

    public override string GetAbilityButtonText() => Translator.GetString("HermitFire");
}
