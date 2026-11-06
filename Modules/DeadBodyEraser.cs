using System.Collections.Generic;
using Hazel;
using System.Linq;
using UnityEngine;
using TownOfHost.Attributes;
using TownOfHost.Roles.Core;

namespace TownOfHost.Modules;

// Selected from hamo 4.40.04: no-body attacks only. This is NOT an RPC capable of
// removing an already existing ordinary corpse on Vanilla. MadCleaner is untouched.
internal static class DeadBodyEraser
{
    private static readonly HashSet<(uint Killer, uint Target)> pending = new();
    private static readonly Dictionary<byte, DeadBody> unreportable = new();
    [GameModuleInitializer]
    public static void Init() { pending.Clear(); unreportable.Clear(); }
    public static bool IsUnreportable(byte id)
    {
        if (!unreportable.TryGetValue(id, out var cleaned) || cleaned == null || !cleaned.gameObject.activeInHierarchy) return false;
        // Reports carry only PlayerId. A fresh, reportable corpse after a revival must
        // not inherit an old corpse's lock, even if the former body still exists.
        return !Object.FindObjectsOfType<DeadBody>().Any(b => b != null && b.ParentId == id
            && b.gameObject.activeInHierarchy && !b.Reported);
    }
    public static DeadBody FindNearestBody(Vector2 from, float range)
    {
        return Object.FindObjectsOfType<DeadBody>()
            .Where(b => b != null && !b.Reported && b.gameObject.activeInHierarchy && !IsUnreportable(b.ParentId)
                && !Roles.Madmate.MadCleaner.IsCleaned(b.ParentId))
            .Where(b => Vector2.Distance(from, b.TruePosition) <= range)
            .Where(b => !PhysicsHelpers.AnyNonTriggersBetween(from, (b.TruePosition - from).normalized,
                Vector2.Distance(from, b.TruePosition), Constants.ShipAndObjectsMask))
            .OrderBy(b => Vector2.Distance(from, b.TruePosition)).FirstOrDefault();
    }
    public static bool MarkUnreportable(DeadBody body)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || GameStates.CalledMeeting
            || body == null || body.Reported || !body.gameObject.activeInHierarchy) return false;
        unreportable[body.ParentId] = body;
        body.Reported = true;
        // No local Destroy, cover dummy, unsupported cleanup RPC, or change to the victim's life state.
        return true;
    }
    public static void MarkLocalUnreportable(byte id)
    {
        foreach (var body in Object.FindObjectsOfType<DeadBody>())
            if (body != null && body.ParentId == id && body.gameObject.activeInHierarchy)
            { unreportable[id] = body; body.Reported = true; }
    }
    public static bool KillWithoutBody(PlayerControl killer, PlayerControl target, CustomDeathReason reason, bool force = true)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || killer == null || target == null
            || !killer.IsAlive() || !target.IsAlive() || !GameStates.IsInTask || GameStates.CalledMeeting) return false;
        var key = (killer.NetId, target.NetId);
        if (!pending.Add(key)) return false;
        try { return CustomRoleManager.OnCheckMurder(killer, target, killer, target, force, deathReason: reason); }
        finally { pending.Remove(key); }
    }
    // Called only after the existing murder/defense pipeline has accepted the hit.
    public static bool TryCompleteNoBodyKill(MurderInfo info)
    {
        if (!pending.Remove((info.AttemptKiller.NetId, info.AttemptTarget.NetId))) return false;
        var target = info.AppearanceTarget;
        // A role redirect may select a different player: preserve its ordinary kill path.
        if (target == null || target.NetId != info.AttemptTarget.NetId || !target.IsAlive()) return false;
        target.Exiled();
        var writer = AmongUsClient.Instance.StartRpcImmediately(target.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable, -1);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        CustomRoleManager.OnMurderPlayer(info.AppearanceKiller, target);
        return true;
    }
}
