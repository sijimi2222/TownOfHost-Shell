using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

// Adapted from the contributor-provided Damocles.cs with the contributor's permission.
// Attribution name and GPL-3.0 publication conditions must be confirmed before release.
public sealed class Damocles : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Damocles),
            player => new Damocles(player),
            CustomRoles.Damocles,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            39900,
            SetupOptionItem,
            "dmc",
            from: From.TownOfHost_Shell,
            isNewRole: true
        );

    static OptionItem OptionNormalKillCooldown;
    static OptionItem OptionKingKillCooldown;
    static OptionItem OptionStartChance;
    static OptionItem OptionChanceUp;

    enum OptionName
    {
        DamoclesNormalKillCooldown,
        DamoclesKingKillCooldown,
        DamoclesStartChance,
        DamoclesChanceUp,
    }

    bool kingMode;
    float deathChance;
    bool disposed;
    bool suicidePending;
    int lifecycleVersion;
    int modeVersion;
    // A duplicate successful-Murder notification must not roll twice for the same death.
    // Revived targets are released from this set by OnFixedUpdate.
    readonly HashSet<PlayerControl> processedVictims = new();

    public Damocles(PlayerControl player) : base(RoleInfo, player)
    {
        kingMode = false;
        deathChance = Mathf.Clamp(OptionStartChance.GetFloat(), 0f, 100f);
    }

    static void SetupOptionItem()
    {
        OptionNormalKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.DamoclesNormalKillCooldown, new(2.5f, 100f, 2.5f), 40f, false).SetValueFormat(OptionFormat.Seconds);
        OptionKingKillCooldown = FloatOptionItem.Create(RoleInfo, 11, OptionName.DamoclesKingKillCooldown, new(2.5f, 100f, 2.5f), 20f, false).SetValueFormat(OptionFormat.Seconds);
        OptionStartChance = FloatOptionItem.Create(RoleInfo, 12, OptionName.DamoclesStartChance, new(0f, 100f, 0.5f), 5f, false).SetValueFormat(OptionFormat.Percent);
        OptionChanceUp = FloatOptionItem.Create(RoleInfo, 13, OptionName.DamoclesChanceUp, new(0.5f, 100f, 0.5f), 15f, false).SetValueFormat(OptionFormat.Percent);
    }

    public float CalculateKillCooldown() => kingMode ? OptionKingKillCooldown.GetFloat() : OptionNormalKillCooldown.GetFloat();
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => true;

    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = 1f;
    }

    bool IsCurrentRole => !disposed && Player != null
        && ReferenceEquals(Player.GetRoleClass(), this)
        && PlayerCatch.GetPlayerById(Player.PlayerId) == Player;

    bool CanAct => AmongUsClient.Instance.AmHost && IsCurrentRole && Player.IsAlive()
        && GameStates.InGame && GameStates.IsInTask && !GameStates.IsMeeting
        && !GameStates.CalledMeeting;

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = null;
        if (!CanAct) return;
        ResetCooldown = true;

        kingMode = !kingMode;
        var mode = ++modeVersion;
        var lifecycle = lifecycleVersion;
        SendRPC();

        // Wait for the common Phantom role reset before applying the new kill cooldown.
        _ = new LateTask(() =>
        {
            if (!CanAct || lifecycle != lifecycleVersion || mode != modeVersion) return;
            Player.ResetKillCooldown();
            // The outer delay has already allowed Phantom to reset. Send the protected
            // cooldown sync now, rather than leaving another delayed Murder RPC behind.
            Player.SetKillCooldown(delay: false);
            Player.SyncSettings();
            UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
        }, 0.2f, "Damocles ModeChange", true);
    }

    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        if (!CanAct || !kingMode || info.IsSuicide || !info.DoKill || !info.CanKill) return;
        var (killer, target) = info.AppearanceTuple;
        if (killer != Player || info.AttemptKiller != Player || target == null
            || target.Data == null || !target.Data.IsDead) return;
        processedVictims.RemoveWhere(v => v == null || v.Data == null || !v.Data.IsDead);
        if (!processedVictims.Add(target)) return;

        // The first kill uses the initial chance; increase only after this roll.
        bool die = UnityEngine.Random.Range(0f, 100f) < deathChance || deathChance >= 100f;
        deathChance = Mathf.Clamp(deathChance + OptionChanceUp.GetFloat(), 0f, 100f);
        SendRPC();
        UtilsNotifyRoles.NotifyRoles(SpecifySeer: Player);
        if (!die || suicidePending) return;

        suicidePending = true;
        var lifecycle = lifecycleVersion;
        _ = new LateTask(() =>
        {
            if (lifecycle != lifecycleVersion) return;
            suicidePending = false;
            if (!CanAct) return;
            CustomRoleManager.OnCheckMurder(Player, Player, Player, Player,
                true, true, 99, CustomDeathReason.Suicide);
        }, 0.2f, "Damocles Suicide", true);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (AmongUsClient.Instance.AmHost && IsCurrentRole)
            processedVictims.RemoveWhere(v => v == null || v.Data == null || !v.Data.IsDead);
    }

    public override void OnStartMeeting()
    {
        lifecycleVersion++;
        modeVersion++;
        suicidePending = false;
    }

    public override void OnDestroy()
    {
        disposed = true;
        lifecycleVersion++;
        modeVersion++;
        suicidePending = false;
        processedVictims.Clear();
    }

    public override string GetProgressText(bool comms = false, bool gamelog = false)
        => kingMode
            ? Utils.ColorString(Palette.ImpostorRed, $"({GetString("DamoclesKingMode")} {deathChance:0.#}%)")
            : "";

    void SendRPC()
    {
        if (!AmongUsClient.Instance.AmHost || !IsCurrentRole) return;
        using var sender = CreateSender(SendOption.Reliable);
        sender.Writer.Write(kingMode);
        sender.Writer.Write(deathChance);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        // Authority remains on the host. Client-originated state cannot change its mode.
        if (AmongUsClient.Instance.AmHost || !IsCurrentRole) return;
        bool mode = reader.ReadBoolean();
        float chance = reader.ReadSingle();
        if (!float.IsFinite(chance) || chance < 0f || chance > 100f) return;
        kingMode = mode;
        deathChance = chance;
    }
}

