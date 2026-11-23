// Generated from production Onmyoji.cs. Regenerate with generate_fixture.py after reviewed source changes.
using TownOfHost;
using static TownOfHost.PlayerCatch;
using static TownOfHost.Utils;
using static TownOfHost.Translator;
using static TownOfHost.Modules.SelfVoteManager;
namespace TownOfHost.Roles.Neutral;
public partial class Onmyoji : TestRoleBase {
    bool IsOnmyojiKillerTarget(PlayerControl target)
    {
        if (target == null) return false;

        if (target.Is(CustomRoleTypes.Impostor)) return true;
        if (target.Is(CustomRoles.StandMaster) && StandMaster.EnableKillAbility) return true;
        if (target.Is(CustomRoles.Opportunist) && Opportunist.HasKillButton) return true;
        if (target.GetRoleClass() is Victim vic) return vic.IsAwakened;

        return target.GetCustomRole() is
            CustomRoles.CountKiller or
            CustomRoles.Jackal or CustomRoles.JackalHadouHo or CustomRoles.JackalMafia or CustomRoles.JackalAlien or
            CustomRoles.DoppelGanger or CustomRoles.GrimReaper or CustomRoles.Remotekiller or
            CustomRoles.Egoist or CustomRoles.Eater or CustomRoles.PavlovDog or
            CustomRoles.Sheriff or CustomRoles.SwitchSheriff or CustomRoles.WolfBoy or
            CustomRoles.JackalWolf or CustomRoles.Stand or CustomRoles.Ogre or
            CustomRoles.Mermaid or CustomRoles.Hunter or CustomRoles.Tiger or CustomRoles.Monika or
            CustomRoles.Altair or CustomRoles.AntiHero or CustomRoles.Catalyst or CustomRoles.Conductor or
            CustomRoles.Fool or CustomRoles.HathorSekhmet or CustomRoles.JackalSeer or CustomRoles.Lupin or
            CustomRoles.LoversBreaker or CustomRoles.Missioneer or CustomRoles.Pharaoh or CustomRoles.RaccoonChild or
            CustomRoles.RaccoonParent
            ;

    }

    bool IsStarReadingTarget(PlayerControl target)

    {

        if (target == null || !target.IsAlive()) return false;

        if (target.Is(CustomRoles.PavlovOwner)) return false;

        return IsOnmyojiKillerTarget(target);

    }

    bool IsValidShikigamiTarget(PlayerControl target)

    {

        if (target == null || !target.IsAlive()) return false;

        if (target.PlayerId == Player.PlayerId) return false;

        if (target.Is(CustomRoles.PavlovOwner)) return true;

        if (IsOnmyojiKillerTarget(target)) return false;

        return true;

    }

    public void OnCheckMurderAsKiller(MurderInfo info)

    {

        if (!Is(info.AttemptKiller) || info.IsSuicide) return;

        info.DoKill = false;



        if (!nominateMode || ShikigamiIds.Count >= 1) return;



        (_, var target) = info.AttemptTuple;

        if (!IsValidShikigamiTarget(target)) return;



        nominateMode = false;

        ApplyModeDesync(false);

        AddShikigami(target);

        UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);

    }

    void AddShikigami(PlayerControl target)

    {

        if (ShikigamiIds.Count >= 1) return;

        if (!IsValidShikigamiTarget(target)) return;

        if (Walkure.TryRejectRoleChange(Player, target, Walkure.RoleChangeSource.Jackal)) return;



        ShikigamiIds.Add(target.PlayerId);

        TargetArrow.Add(Player.PlayerId, target.PlayerId);



        if (!RoleSendList.Contains(target.PlayerId))

            RoleSendList.Add(target.PlayerId);



        target.RpcSetCustomRole(CustomRoles.Shikigami, log: null);

        _ = new LateTask(() =>

        {

            if (target.GetRoleClass() is Shikigami sk)

                sk.SetOwner(Player.PlayerId);

        }, 0.1f, "Onmyoji.SetOwner", true);



        NameColorManager.Add(Player.PlayerId, target.PlayerId, "#9b59b6");

        createCooldownTimer = OptionCreateShikigamiCooldown.GetFloat();



        SendRPC();

        _ = new LateTask(() => UtilsNotifyRoles.NotifyRoles(), 0.2f, "Onmyoji.Shikigami", true);

    }

    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)

    {

        if (!Is(voter)) return true;

        if (!Player.IsAlive() || !OptionCanCreateShikigami.GetBool() || ShikigamiIds.Count >= 1) return true;



        if (CheckSelfVoteMode(Player, votedForId, out var status))

        {

            if (status is VoteStatus.Self)

            {

                NextShikigamiCandidate = byte.MaxValue;

                SendMessage(

                    "<color=#9b59b6>【式神候補選択モード】</color>\n" +

                    "誰かに投票 → 近接指名の候補に設定\n" +

                    "スキップ → キャンセル\n" +

                    "（ペットでモード切替→キルボタンでも指名可）",

                    Player.PlayerId);

                SetMode(Player, true);

                SendRPC();

                return false;

            }

            if (status is VoteStatus.Skip)

            {

                NextShikigamiCandidate = byte.MaxValue;

                SendMessage(GetString("VoteSkillFin"), Player.PlayerId);

                SetMode(Player, false);

                SendRPC();

                return false;

            }

            if (status is VoteStatus.Vote)

            {

                var target = GetPlayerById(votedForId);

                if (target == null || !target.IsAlive() || votedForId == Player.PlayerId)

                {

                    SendMessage("<color=#9b59b6>その相手は式神にできません。</color>", Player.PlayerId);

                    SetMode(Player, false);

                    return false;

                }

                if (!IsValidShikigamiTarget(target))

                {

                    SendMessage("<color=#9b59b6>キル能力を持たない相手のみ式神にできます。</color>", Player.PlayerId);

                    SetMode(Player, false);

                    return false;

                }

                NextShikigamiCandidate = votedForId;

                nearTimer = 0f;

                SendMessage(

                    $"<color=#9b59b6>【式神候補設定】</color>\n" +

                    $"{UtilsName.GetPlayerColor(target, true)} を候補に設定しました。\n" +

                    $"次ターン、1.5秒近づいて式神作成！",

                    Player.PlayerId);

                SetMode(Player, false);

                SendRPC();

                return false;

            }

        }

        return true;

    }

    void RefreshStarReadingTargets()

    {

        NameColorManager.RemoveAll(Player.PlayerId);

        if (!Player.IsAlive()) return;



        foreach (var pc in AllPlayerControls)

        {

            if (pc == null || pc.PlayerId == Player.PlayerId || !pc.IsAlive()) continue;

            if (!IsStarReadingTarget(pc)) continue;

            NameColorManager.Add(Player.PlayerId, pc.PlayerId, UtilsRoleText.GetRoleColorCode(pc.GetCustomRole()));

        }

        foreach (var id in ShikigamiIds)

            NameColorManager.Add(Player.PlayerId, id, "#9b59b6");

    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)

    {

        seen ??= seer;

        if (!Is(seer) || !Player.IsAlive()) return "";



        if (seer.PlayerId != seen.PlayerId)

        {

            if (!IsStarReadingTarget(seen)) return "";

            return $" <color={UtilsRoleText.GetRoleColorCode(seen.GetCustomRole())}>★</color>";

        }

        if (isForMeeting) return "";



        var arrows = "";

        foreach (var id in ShikigamiIds)

        {

            var sk = GetPlayerById(id);

            if (sk == null || !sk.IsAlive()) continue;

            arrows += TargetArrow.GetArrows(seer, id);

        }

        return arrows != "" ? $"<color=#9b59b6>{arrows}</color>" : "";

    }
}
