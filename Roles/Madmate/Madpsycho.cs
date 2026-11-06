using System;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Madmate;

// hamo 4.40.02 behavior adapted to the existing Shell serialization identity.
// Keep the legacy type/enum spelling, ConfigId and detail IDs for saved settings.
public sealed class Madpsycho : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(Madpsycho), p => new Madpsycho(p), CustomRoles.Madpsycho,
        () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
        CustomRoleTypes.Madmate, 22800, SetupOptionItems, "mps", OptionSort: (2, 3));

    static OptionItem OptionCanVent;
    public static OptionItem OptionDeathReason, OptionTaskTrigger;
    public static readonly CustomDeathReason[] deathReasons = { CustomDeathReason.Kill, CustomDeathReason.Counter };
    enum OptionName { psychoDeathReason }
    bool reflecting, disposed;
    byte reflectingTarget;
    public Madpsycho(PlayerControl player) : base(RoleInfo, player, () => HasTask.ForRecompute) { }
    static void SetupOptionItems()
    {
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 9, GeneralOption.CanVent, false, false);
        OptionDeathReason = StringOptionItem.Create(RoleInfo, 10, OptionName.psychoDeathReason,
            deathReasons.Select(r => r.ToString()).ToArray(), 1, false);
        OptionTaskTrigger = IntegerOptionItem.Create(RoleInfo, 11, GeneralOption.TaskTrigger,
            new(0, 99, 1), 1, false).SetValueFormat(OptionFormat.Pieces);
        OverrideTasksData.Create(RoleInfo, 20);
    }
    public bool CanPsycho() => !disposed && MyTaskState.HasCompletedEnoughCountOfTasks(OptionTaskTrigger.GetInt());
    public override void OnDestroy() { disposed = true; reflecting = false; }
    public override bool OnCheckMurderAsTarget(MurderInfo info)
        => !TryPsycho(info.AttemptKiller, info.KillPower);

    // A narrow host-created ticket replaces upstream's blanket exception allowing
    // every MadPsycho to use the normal murder path despite having no kill button.
    internal static bool IsReflectionAttempt(MurderInfo info)
        => info?.AttemptKiller?.GetRoleClass() is Madpsycho role && !role.disposed && role.reflecting
            && info.AttemptTarget != null && role.reflectingTarget == info.AttemptTarget.PlayerId
            && info.KillPower == 10;

    public bool TryPsycho(PlayerControl killer, int power, bool meeting = false)
    {
        if (AmongUsClient.Instance?.AmHost != true || disposed || reflecting || Player == null || killer == null
            || !ReferenceEquals(Player.GetRoleClass(), this) || !Player.IsAlive() || !killer.IsAlive()
            || Player.Data.Disconnected || killer.Data.Disconnected || killer.PlayerId == Player.PlayerId
            || power >= 2 || !CanPsycho()) return false;
        if (meeting ? !(GameStates.IsMeeting || GameStates.CalledMeeting)
            : (!GameStates.IsInTask || GameStates.IsMeeting || GameStates.CalledMeeting)) return false;
        int choice = OptionDeathReason.GetValue();
        var reason = deathReasons[Math.Clamp(choice, 0, deathReasons.Length - 1)];
        var state = PlayerState.GetByPlayerId(killer.PlayerId);
        var previous = state.DeathReason;
        reflecting = true; reflectingTarget = killer.PlayerId;
        try
        {
            // The common task-phase murder validator deliberately rejects all
            // meeting murders. Keep the established meeting-role RPC path here.
            state.DeathReason = reason;
            if (meeting) Player.RpcMurderPlayer(killer);
            else CustomRoleManager.OnCheckMurder(Player, killer, Player, killer,
                force: true, Killpower: 10, deathReason: reason);
            if (killer.IsAlive()) state.DeathReason = previous;
            if (meeting)
            {
                bool died = !killer.IsAlive();
                string message = string.Format(GetString(died ? "Rpsych" : "RMeetingKill"),
                    UtilsName.GetPlayerColor(killer, true), UtilsName.GetPlayerColor(died ? Player : killer, true));
                foreach (var seer in PlayerCatch.AllPlayerControls.Where(p => !p.IsAlive()))
                    Utils.SendMessage(message, seer.PlayerId, GetString("RMSKillTitle"));
            }
            return true;
        }
        finally
        {
            if (killer.IsAlive()) state.DeathReason = previous;
            reflecting = false;
        }
    }
}
