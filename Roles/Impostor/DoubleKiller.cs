using System;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Madmate;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

// Selective 4.40.02 port: keep Shell's ordinary kill and limited secondary kill.
public sealed class DoubleKiller : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(DoubleKiller), p => new DoubleKiller(p), CustomRoles.DoubleKiller,
        () => RoleTypes.Phantom, CustomRoleTypes.Impostor, 3400, SetUpOptionItem,
        "dk", OptionSort: (3, 13), from: From.SuperNewRoles);
    static OptionItem OptionPhantomCooldown, OptionKillCooldown, OptionCanVent, OptionCanSabotage, OptionPhantomUsageCount;
    readonly float phantomCooldown, killCooldown;
    readonly bool canVent, canSabotage;
    int usedPhantomCount, generation, ventCooldownRevision;
    float gateRemaining;
    float abilityReadyAt = -1f, ventEnteredAt = -1f, cooldownOverride;
    bool disposed;
    public bool CanSubkill { get; private set; } = true;
    bool Current => !disposed && Player != null && ReferenceEquals(Player.GetRoleClass(), this);
    public DoubleKiller(PlayerControl player) : base(RoleInfo, player)
    {
        phantomCooldown = OptionPhantomCooldown.GetFloat();
        killCooldown = OptionKillCooldown.GetFloat();
        canVent = OptionCanVent.GetBool(); canSabotage = OptionCanSabotage.GetBool();
    }
    enum OptionName { DoubleKillerPhantomCooldown, DoubleKillerKillCooldown, DoubleKillerCanVent, DoubleKillerCanSabotage, DoubleKillerPhantomUsageCount }
    static void SetUpOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.DoubleKillerKillCooldown, new(0.5f, 60f, 0.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionPhantomCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.DoubleKillerPhantomCooldown, new(0.5f, 60f, 0.5f), 30f, false).SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 12, OptionName.DoubleKillerCanVent, true, false);
        OptionCanSabotage = BooleanOptionItem.Create(RoleInfo, 13, OptionName.DoubleKillerCanSabotage, true, false);
        OptionPhantomUsageCount = IntegerOptionItem.Create(RoleInfo, 14, OptionName.DoubleKillerPhantomUsageCount, new(1, 14, 1), 1, false).SetValueFormat(OptionFormat.Times);
    }
    public float CalculateKillCooldown() => killCooldown;
    public bool CanUseSabotageButton() => canSabotage;
    public bool CanUseImpostorVentButton() => canVent;
    public override bool CanClickUseVentButton => canVent;
    public override bool OnEnterVent(PlayerPhysics physics, int ventId) => canVent;
    public override bool CanVentMoving(PlayerPhysics physics, int ventId) => canVent;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        if (usedPhantomCount < OptionPhantomUsageCount.GetInt()) AURoleOptions.PhantomCooldown = cooldownOverride > 0f ? cooldownOverride : phantomCooldown;
    }
    bool IUsePhantomButton.IsPhantomRole => usedPhantomCount < OptionPhantomUsageCount.GetInt();
    bool IUsePhantomButton.IsresetAfterKill => false;
    bool IUsePhantomButton.SyncAbilityCooldownWithKillCooldown => false;
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (AmongUsClient.Instance?.AmHost != true || !Current) return;
        KeepCooldownInVent();
        if (CanSubkill) return;
        gateRemaining -= Time.fixedDeltaTime;
        if (gateRemaining <= 0) { CanSubkill = true; SendRPC(); }
    }
    void Invalidate()
    {
        generation++; ventCooldownRevision++; gateRemaining = 0; CanSubkill = true;
        abilityReadyAt = ventEnteredAt = -1f; cooldownOverride = 0;
    }
    public override void AfterMeetingTasks()
    {
        Invalidate();
        abilityReadyAt = Time.time + phantomCooldown;
    }
    void KeepCooldownInVent()
    {
        if (!Player.IsAlive() || !GameStates.IsInTask || GameStates.IsMeeting || GameStates.CalledMeeting
            || usedPhantomCount >= OptionPhantomUsageCount.GetInt()) return;
        if (abilityReadyAt < 0f) abilityReadyAt = Time.time + phantomCooldown;
        if (Player.inVent) { if (ventEnteredAt < 0f) ventEnteredAt = Time.time; return; }
        if (ventEnteredAt < 0f) return;
        float entered = ventEnteredAt; ventEnteredAt = -1f;
        if (abilityReadyAt <= entered) return;
        abilityReadyAt += Time.time - entered;
        float left = abilityReadyAt - Time.time;
        if (left <= .5f) return;
        cooldownOverride = left;
        Player.RpcResetAbilityCooldown(Sync: true);
        int captured = generation, revision = ++ventCooldownRevision;
        _ = new LateTask(() =>
        {
            if (!Current || captured != generation || revision != ventCooldownRevision || !Player.IsAlive() || GameStates.IsMeeting || GameStates.CalledMeeting) return;
            cooldownOverride = 0f; Player.SyncSettings();
        }, 1f, "DoubleKillerVentCooldown", true);
    }
    public override void OnDestroy() { Invalidate(); disposed = true; }
    public override void OnStartMeeting() => Invalidate();
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => Invalidate();
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false; ResetCooldown = false;
        if (AmongUsClient.Instance?.AmHost != true || !Current || !Player.IsAlive()
            || !GameStates.IsInTask || GameStates.IsMeeting || GameStates.CalledMeeting || !CanSubkill
            || usedPhantomCount >= OptionPhantomUsageCount.GetInt()) return;
        PlayerControl nearest = null;
        float minDist = Main.NormalOptions.KillDistance switch { 0 => 1f, 1 => 1.8f, _ => 2.5f };
        foreach (var target in PlayerCatch.AllAlivePlayerControls)
        {
            if (target.PlayerId == Player.PlayerId || target.Data.Disconnected) continue;
            if (target.GetCustomRole().IsImpostor() && !SuddenDeathMode.NowSuddenDeathMode) continue;
            float dist = Vector2.Distance(Player.GetTruePosition(), target.GetTruePosition());
            if (dist < minDist) { minDist = dist; nearest = target; }
        }
        if (nearest == null) return;
        usedPhantomCount++;
        abilityReadyAt = Time.time + phantomCooldown; cooldownOverride = 0f; ventCooldownRevision++;
        CanSubkill = false; gateRemaining = Mathf.Min(phantomCooldown, 1f); SendRPC();
        float savedKillTimer = Player.killTimer;
        Vector2 targetPos = nearest.transform.position;
        bool reflected = nearest.GetRoleClass() is Madpsycho psycho && psycho.TryPsycho(Player, 1);
        if (!reflected && CustomRoleManager.OnCheckMurder(Player, nearest, nearest, nearest, true, false, 1, CustomDeathReason.Kill))
        {
            RPC.PlaySoundRPC(Player.PlayerId, Sounds.KillSound);
            if (Player.IsAlive()) Player.RpcSnapToForced(targetPos);
        }
        if (Player.IsAlive()) Player.RpcResetAbilityCooldown(Sync: true);
        int captured = generation;
        // One delayed restoration per valid press, never one per FixedUpdate.
        _ = new LateTask(() =>
        {
            if (!Current || captured != generation || !Player.IsAlive() || !GameStates.IsInTask
                || GameStates.IsMeeting || GameStates.CalledMeeting) return;
            RestoreKillCooldown(savedKillTimer);
        }, 0.2f, "DoubleKillerRestoreCD", true);
    }
    void RestoreKillCooldown(float cooldown)
    {
        cooldown = Mathf.Max(cooldown, 0f);
        if (IUsePhantomButton.IPPlayerKillCooldown.ContainsKey(Player.PlayerId)) IUsePhantomButton.IPPlayerKillCooldown[Player.PlayerId] = 0;
        Player.RpcProtectedMurderPlayer(); Player.ResetKillCooldown(); Player.SetKillTimer(cooldown); Player.SyncSettings();
    }
    void SendRPC()
    {
        if (AmongUsClient.Instance?.AmHost != true || !Current) return;
        using var sender = CreateSender(); sender.Writer.Write(CanSubkill); sender.Writer.Write((byte)usedPhantomCount);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        if (AmongUsClient.Instance?.AmHost == true || disposed) return;
        CanSubkill = reader.ReadBoolean();
        // Accept the upstream one-byte readiness form as well as Shell's count.
        if (reader.Position < reader.Length) usedPhantomCount = Mathf.Clamp(reader.ReadByte(), 0, 14);
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        int remaining = Mathf.Max(0, OptionPhantomUsageCount.GetInt() - usedPhantomCount);
        return remaining <= 0 ? "" : $"<#ff0000>({remaining})</color>";
    }
    public override bool OverrideAbilityButton(out string text) { text = "DoubleKiller_Ability"; return true; }
}
