// Extracted production fragments. Native/game collaborators are test substitutes.
using TownOfHost;using TownOfHost.Roles.Core;using TownOfHost.Roles.Core.Interfaces;using AmongUs.GameOptions;
namespace RegressionFragments;
public class SchrodingerCat:RoleBase { public ISchrodingerCatOwner.TeamType Team;public int Sets;public SchrodingerCat(PlayerControl p):base(new(),p){}public void RpcSetTeam(ISchrodingerCatOwner.TeamType team){Team=team;Sets++;} }
public class BakeCat:RoleBase {public ISchrodingerCatOwner.TeamType Team;public int Sets;public BakeCat(PlayerControl p):base(new(),p){}public void RpcSetTeam(ISchrodingerCatOwner.TeamType team){Team=team;Sets++;}}
public static class MemoryRestoreFragment {public static void Apply(RoleBase targetRole){
switch (targetRole)
        {
            case SchrodingerCat cat when cat.Team == ISchrodingerCatOwner.TeamType.None: cat.RpcSetTeam(ISchrodingerCatOwner.TeamType.Crew); break; // クルーとして陣営変化
            case BakeCat bake when bake.Team == ISchrodingerCatOwner.TeamType.None: bake.RpcSetTeam(ISchrodingerCatOwner.TeamType.Crew); break;     // 自覚&クルーとして陣営変化
        }
}}
public static class MadVisibilityFragment{public static OptionItem OptionCanSeeImpostor=new(){Value=1};static bool UsesMadmateCommonSettings(CustomRoles role)=>true;
public static bool CanSeeImpostorNameColor(CustomRoles role)
    {
        // Mad Cleaner uses its own dynamic ally visibility, not the common permanent colors.
        if (role is CustomRoles.MadCleaner or CustomRoles.MadKind) return false;
        if (role is CustomRoles.SatsumatoImoM)
        {
            return OptionCanSeeImpostor?.GetBool() ?? false;
        }

        return UsesMadmateCommonSettings(role) && Options.MadCanSeeImpostor.GetBool();
    }
}
public enum AssassinMeeting{WaitMeeting,EndMeeting}
public static class AssassinGuardFragment {public static AssassinMeeting NowState=AssassinMeeting.WaitMeeting;public static RoleBase AddRole;public static int Calls;public static void OnFixedUpdate(PlayerControl player){
if (player == null || NowState is not AssassinMeeting.WaitMeeting || player.IsAlive() || GameStates.IsMeeting
            || GameStates.CalledMeeting || GameStates.ExiledAnimate || !GameStates.IsInTask
            || CustomWinnerHolder.WinnerTeam is not CustomWinner.Default)
        {
            AddRole?.OnFixedUpdate(player);
            return;
        }
Calls++;}}
public static class ParasiteFragment{internal static bool IsLastLivingImpostor(PlayerControl killer)
        => killer != null && killer.GetCustomRole().IsImpostor()
            && !PlayerCatch.AllAlivePlayerControls.Any(p => p.PlayerId != killer.PlayerId && p.GetCustomRole().IsImpostor());}
public static class KillerCreditFragment{public static void Apply(PlayerControl dummykiller,PlayerControl killer,PlayerControl target){            if (dummykiller.PlayerId != killer.PlayerId) target.SetRealKiller(killer);}}
public class RaccoonParent:RoleBase{ public byte GrabbedTargetId=255;bool reportKillAttempted;public static bool ChildKillOnMeetingIfGrabbing=true; public static OptionItem OptionKillOnMeetingIfGrabbing=new(){Value=1}; public RaccoonParent(PlayerControl p):base(new(),p){}public void ResetMeeting()=>reportKillAttempted=false;
public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (AmongUsClient.Instance?.AmHost != true || reportKillAttempted || !Player.IsAlive()
            || GameStates.IsMeeting || MeetingHud.Instance != null || GrabbedTargetId == byte.MaxValue || !OptionKillOnMeetingIfGrabbing.GetBool()) return;
        var grabbed = GrabbedTargetId.GetPlayerControl();
        if (grabbed == null || !grabbed.IsAlive() || grabbed.Data.Disconnected) return;
        reportKillAttempted = true;
        CustomRoleManager.OnCheckMurder(Player, grabbed, grabbed, grabbed, true, false, 1, CustomDeathReason.Kill);
    }
}
public class RaccoonChild:RoleBase{ public byte GrabbedTargetId=255;bool reportKillAttempted;public static bool ChildKillOnMeetingIfGrabbing=true; public static OptionItem OptionKillOnMeetingIfGrabbing=new(){Value=1}; public RaccoonChild(PlayerControl p):base(new(),p){}public void ResetMeeting()=>reportKillAttempted=false;
public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (AmongUsClient.Instance?.AmHost != true || reportKillAttempted || !Player.IsAlive()
            || GameStates.IsMeeting || MeetingHud.Instance != null || GrabbedTargetId == byte.MaxValue || !RaccoonParent.ChildKillOnMeetingIfGrabbing) return;
        var grabbed = GrabbedTargetId.GetPlayerControl();
        if (grabbed == null || !grabbed.IsAlive() || grabbed.Data.Disconnected) return;
        reportKillAttempted = true;
        CustomRoleManager.OnCheckMurder(Player, grabbed, grabbed, grabbed, true, false, 1, CustomDeathReason.Kill);
    }
}
public class Turncoat:RoleBase {public static OptionItem OptionCanShapeShift=new(){Value=1};static void SetupOptionItem(){}public Turncoat(PlayerControl p):base(new(),p){}public static SimpleRoleInfo CreateInfo()=>SimpleRoleInfo.Create(
            typeof(Turncoat),
            player => new Turncoat(player),
            CustomRoles.Turncoat,
            () => OptionCanShapeShift.GetBool() ? RoleTypes.Shapeshifter : RoleTypes.Crewmate,
            CustomRoleTypes.Neutral,
            55200,
            SetupOptionItem,
            "Tu",
            "#371a1a",
            (7, 1),
            isDesyncImpostor: true,
            from: From.TownOfHost_K
        );}
