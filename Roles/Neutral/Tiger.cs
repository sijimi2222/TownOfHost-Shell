using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Tiger : RoleBase, IKiller, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(Tiger), player => new Tiger(player), CustomRoles.Tiger,
        () => RoleTypes.Phantom, CustomRoleTypes.Neutral, 69900, SetupOptionItem,
        "tiger", "#FFD400", (1, 30), true,
        from: From.TownOfHost_Shell,
        assignInfo: new RoleAssignInfo(CustomRoles.Tiger, CustomRoleTypes.Neutral)
        { AssignCountRule = new(1, 1, 1) });

    static OptionItem KillCooldown, AbilityCooldown, Duration, Speed, Vision;
    // 死亡情報は残し、捕食された死体だけを検知・対象選択から除外する。
    static readonly HashSet<byte> PredatedBodyIds = new();
    public static bool IsPredatedBody(byte playerId) => PredatedBodyIds.Contains(playerId);
    public static void ResetPredatedBodies() => PredatedBodyIds.Clear();

    public static void RecordDeath(MurderInfo info)
    {
        byte id = info.AttemptTarget.PlayerId;
        bool changed = info.TigerPredation ? PredatedBodyIds.Add(id) : PredatedBodyIds.Remove(id);
        if (!changed) return;
        // キル成功後、各役職が死体の矢印を登録するより先に記録する。
        // 蘇生後の通常キルでは同じPlayerIdの除外を解除する。
        var tiger = info.AttemptKiller.GetRoleClass() as Tiger
            ?? CustomRoleManager.AllActiveRoles.Values.OfType<Tiger>().FirstOrDefault();
        if (tiger == null) return;
        using var sender = tiger.CreateSender(SendOption.Reliable);
        sender.Writer.Write(id);
        sender.Writer.Write(info.TigerPredation);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        byte id = reader.ReadByte();
        if (reader.ReadBoolean()) PredatedBodyIds.Add(id);
        else PredatedBodyIds.Remove(id);
    }
    enum OptionName { TigerKillCooldown, TigerAbilityCooldown, TigerDuration, TigerSpeed, TigerVision }
    bool hunting;
    float remaining;
    float abilityRemaining;
    bool sawOtherKiller;
    bool hasKilled;
    int generation;

    public Tiger(PlayerControl player) : base(RoleInfo, player, () => HasTask.False)
    {
        abilityRemaining = AbilityCooldown.GetFloat();
    }

    static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 10, rule: new(1, 30, 1));
        KillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.TigerKillCooldown, new(20f, 60f, 2.5f), 40f, false).SetValueFormat(OptionFormat.Seconds);
        AbilityCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.TigerAbilityCooldown, new(10f, 40f, 2.5f), 20f, false).SetValueFormat(OptionFormat.Seconds);
        Duration = FloatOptionItem.Create(RoleInfo, 12, OptionName.TigerDuration, new(1f, 8f, 0.5f), 3f, false).SetValueFormat(OptionFormat.Seconds);
        // Hunterの速度倍率の範囲・刻みを流用。
        Speed = FloatOptionItem.Create(RoleInfo, 13, OptionName.TigerSpeed, new(1f, 10f, 0.25f), 1.75f, false).SetValueFormat(OptionFormat.Multiplier);
        Vision = FloatOptionItem.Create(RoleInfo, 14, OptionName.TigerVision, new(0.01f, 0.50f, 0.01f), 0.50f, false).SetValueFormat(OptionFormat.Multiplier);
    }

    public bool CanUseKillButton() => Player.IsAlive();
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public float CalculateKillCooldown() => KillCooldown.GetFloat();
    public bool IsresetAfterKill => false;
    public override bool CanUseAbilityButton() => Player.IsAlive() && !hunting;
    public float SpeedMultiplier => hunting && Player.IsAlive() ? Speed.GetFloat() : 1f;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(true);
        AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
    }

    // 個別設定の最後で適用するため、他役職や共通の基本視界を変更しない。
    public void ApplyPredationVision(IGameOptions opt)
    {
        if (!hunting || !Player.IsAlive()) return;
        opt.SetFloat(FloatOptionNames.CrewLightMod, opt.GetFloat(FloatOptionNames.CrewLightMod) * Vision.GetFloat());
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, opt.GetFloat(FloatOptionNames.ImpostorLightMod) * Vision.GetFloat());
    }

    public void OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        if (!AmongUsClient.Instance.AmHost || !Player.IsAlive() || !GameStates.IsInTask
            || GameStates.IsMeeting || hunting || abilityRemaining > 0f) return;
        hunting = true;
        remaining = Duration.GetFloat();
        abilityRemaining = AbilityCooldown.GetFloat();
        int token = ++generation;
        AdjustKillCooldown = false;
        ResetCooldown = true;
        Player.SetKillCooldown(0.005f, force: true, delay: false);
        // Vanilla向けPhantom復帰のSetRoleの後にも解除を反映する。
        _ = new LateTask(() =>
        {
            if (Player != null && hunting && generation == token && Player.IsAlive()
                && GameStates.IsInTask && !GameStates.IsMeeting)
                Player.SetKillCooldown(0.005f, force: true, delay: false);
        }, 0.1f, "TigerReadyToKill", true);
        Player.SyncSettings();
    }

    void EndPredation(bool sync = true)
    {
        bool changed = hunting;
        hunting = false;
        remaining = 0f;
        generation++;
        if (changed && sync && AmongUsClient.Instance.AmHost && Player != null && Player.Data != null && !Player.Data.Disconnected)
        {
            Player.MarkDirtySettings();
            if (GameStates.IsInGame) Player.SyncSettings();
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Player == null || Player.Data == null || Player.Data.Disconnected || !Player.IsAlive()
            || !GameStates.IsInGame || GameStates.IsMeeting)
        {
            EndPredation();
            return;
        }
        if (!GameStates.IsInTask || GameStates.Intro) return;
        sawOtherKiller |= HasOtherKiller();
        abilityRemaining = Mathf.Max(0f, abilityRemaining - Time.fixedDeltaTime);
        if (!hunting) return;
        remaining -= Time.fixedDeltaTime;
        if (remaining > 0f) return;
        EndPredation();
        CustomRoleManager.OnCheckMurder(Player, Player, Player, Player,
            true, true, 99, CustomDeathReason.Suicide);
    }

    public override void OnStartMeeting()
    {
        EndPredation();
        abilityRemaining = AbilityCooldown.GetFloat();
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => EndPredation();
    public override void OnDead(PlayerControl player) { if (Is(player)) EndPredation(); }
    public override void OnLeftPlayer(PlayerControl player) { if (Is(player)) EndPredation(false); }
    public override void OnDestroy() => EndPredation(false);
    public override void CheckWinner(GameOverReason reason) => EndPredation(false);

    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        if (info.IsSuicide || !Is(info.AttemptKiller)) return;
        hasKilled = true;
        EndPredation();
        Player.SetKillCooldown(KillCooldown.GetFloat(), force: true, delay: false);
    }

    // 通常の防御判定がすべて終わった時だけ呼ぶ。MurderInfoの実キラー/対象は維持。
    public bool PreparePredationKill(MurderInfo info)
    {
        if (!hunting || info.IsSuicide || info.IsFakeSuicide || !Is(info.AttemptKiller)
            || info.AttemptTarget.IsProtected()) return false;
        info.TigerPredation = true;
        info.TigerKillRoom = info.AttemptTarget.GetShipRoomName();
        info.AppearanceKiller = info.AttemptTarget;
        info.AppearanceTarget = info.AttemptTarget;
        return true;
    }

    public static void SendPredationKill(MurderInfo info)
    {
        var target = info.AttemptTarget;
        var origin = target.GetTruePosition();
        var netId = target.NetId;
        // Magicianと同じマップ外位置。ReliableのSnapTo→Murderの順を維持する。
        target.RpcSnapToForced(new Vector2(9999f, 9999f), SendOption.Reliable);
        var sender = CustomRpcSender.Create("TigerPredationKill", SendOption.Reliable);
        sender.RpcMurderPlayer(target, target);
        sender.SendMessage();
        if (AmongUsClient.Instance.AmClient) target.MurderPlayer(target, ExtendedPlayerControl.SuccessFlags);
        // Native側でもキル失敗になった場合は、対象をマップ外に置き去りにしない。
        if (!target.Data.IsDead)
        {
            CustomRoleManager.CheckMurderInfos.Remove(target.PlayerId);
            target.RpcSnapToForced(origin, SendOption.Reliable);
            return;
        }
        // 死体は移動せず、キル演出後に死亡した本人のゴーストだけ元の位置へ戻す。
        _ = new LateTask(() =>
        {
            if (GameStates.IsInGame && !GameStates.IsMeeting && target != null && target.NetId == netId
                && target.Data != null && target.Data.IsDead && !target.Data.Disconnected)
                target.RpcSnapToForced(origin, SendOption.Reliable);
        }, 2f, "TigerRestoreGhost", true);
    }

    bool HasOtherKiller() => PlayerCatch.AllAlivePlayerControls.Any(pc => pc.PlayerId != Player.PlayerId
        && pc.Data != null && !pc.Data.Disconnected && pc.CanUseKillButton());

    bool CanWin => Player != null && Player.IsAlive() && !Player.Data.Disconnected
        && !GameStates.Intro && (sawOtherKiller || hasKilled) && !HasOtherKiller();

    public static bool HasLivingTiger() => PlayerCatch.AllAlivePlayerControls.Any(pc => pc.Is(CustomRoles.Tiger));

    public static bool TryChooseWinner()
    {
        foreach (var pc in PlayerCatch.AllAlivePlayerControls.OrderBy(pc => pc.PlayerId))
        {
            if (pc.GetRoleClass() is not Tiger tiger || !tiger.CanWin) continue;
            if (!CustomWinnerHolder.ResetAndSetAndChWinner(CustomWinner.Tiger, pc.PlayerId, AddWin: false)) continue;
            CustomWinnerHolder.NeutralWinnerIds.Add(pc.PlayerId);
            return true;
        }
        return false;
    }
}
