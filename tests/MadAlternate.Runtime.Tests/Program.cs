using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.AddOns.Common;
using TownOfHost.Roles.Madmate;

var protector = new PlayerControl(0);
var target = new PlayerControl(1);
var other = new PlayerControl(2);
var role = new MadAlternate(protector);
protector.Role = role;
PlayerCatch.Players.AddRange([protector, target, other]);
MadAlternate.RoleInfo.Setup();

int passed = 0;
void Check(bool result, string label)
{
    if (!result) throw new InvalidOperationException(label);
    passed++;
    Console.WriteLine($"PASS {label}");
}

void SelectTarget()
{
    GameStates.IsInTask = false;
    role.OnStartMeeting();
    Check(!role.CheckVoteAsVoter(0, protector), "self vote opens selection without counting");
    Check(!role.CheckVoteAsVoter(1, protector), "selection vote does not count");
    GameStates.IsInTask = true;
}

Check(MadAlternate.RoleInfo.BaseRole() == AmongUs.GameOptions.RoleTypes.Engineer, "Engineer base role");
Check(OverrideTasksData.Settings == (true, 3, 3, 3), "task override defaults");
var taskRule = OverrideTasksData.TaskCountRule;
Check(taskRule.MinValue == 0 && taskRule.MaxValue == 10 && taskRule.Step == 1,
    "all task counts accept 0 through 10 in steps of 1");
Check(taskRule.GetValueByIndex(3) == 3 && taskRule.GetValueByIndex(10) == 10,
    "saved task counts within range keep their values");
Check(taskRule.GetValueByIndex(11) == 10 && taskRule.GetValueByIndex(99) == 10,
    "saved task counts above 10 are limited to the new maximum");
Check(!role.CanClickUseVentButton && !role.OnEnterVent(null, 0), "vent off by default");
BooleanOptionItem.Last.Enabled = true;
Check(role.CanClickUseVentButton && role.OnEnterVent(null, 0), "vent on with option");

Check(role.CheckVoteAsVoter(0, protector), "unfinished tasks allow normal voting");
protector.TasksFinished = true;
Check(role.CheckVoteAsVoter(0, other), "other players' votes unaffected");
Check(role.CheckVoteAsVoter(1, protector), "ordinary target vote without selection counts");
SelfVoteManager.Enabled = false;
Check(role.CheckVoteAsVoter(0, protector), "global meeting restrictions respected");
SelfVoteManager.Enabled = true;
Check(!role.CheckVoteAsVoter(0, protector) && !role.CheckVoteAsVoter(0, protector)
    && role.CheckVoteAsVoter(1, protector), "second self vote cancels");
Check(!role.CheckVoteAsVoter(0, protector) && !role.CheckVoteAsVoter(MeetingVoteManager.Skip, protector)
    && role.CheckVoteAsVoter(1, protector), "skip cancels");
Check(!role.CheckVoteAsVoter(0, protector) && !role.CheckVoteAsVoter(15, protector),
    "missing target remains unselected");
target.Data.Disconnected = true;
Check(!role.CheckVoteAsVoter(1, protector), "disconnected target rejected");
target.Data.Disconnected = false;
SelectTarget();
Check(role.CheckVoteAsVoter(2, protector), "regular vote can be cast after selection");
Check(!MadAlternate.TrySacrificeFor(other), "other player unprotected");
GameStates.CalledMeeting = true;
Check(!MadAlternate.TrySacrificeFor(target), "no protection during a meeting");
GameStates.CalledMeeting = false;
AmongUsClient.Instance.AmHost = false;
Check(!MadAlternate.TrySacrificeFor(target), "non-host cannot sacrifice");
AmongUsClient.Instance.AmHost = true;
Amnesia.AbilityEnabled = false;
Check(!MadAlternate.TrySacrificeFor(target), "disabled ability does not sacrifice");
Amnesia.AbilityEnabled = true;
protector.Data.Disconnected = true;
Check(!MadAlternate.TrySacrificeFor(target), "disconnected protector cannot sacrifice");
protector.Data.Disconnected = false;
protector.AllowDeath = false;
Check(!MadAlternate.TrySacrificeFor(target) && protector.State.DeathReason == CustomDeathReason.etc,
    "failed self kill does not block attack or leave an incorrect death reason");
protector.AllowDeath = true;
protector.BeforeMurder = () => Check(!MadAlternate.TrySacrificeFor(target), "selection consumed before callback");
Check(MadAlternate.TrySacrificeFor(target) && !protector.IsAlive() && target.IsAlive()
    && protector.State.DeathReason == CustomDeathReason.Sacrifice, "target survives and protector dies with Sacrifice");
Check(!MadAlternate.TrySacrificeFor(target), "sacrifice cannot repeat");

protector.Alive = true;
protector.BeforeMurder = null;
SelectTarget();
role.OnStartMeeting();
Check(!MadAlternate.TrySacrificeFor(target), "protection expires at next meeting");
SelectTarget();
protector.Role = null;
Check(!MadAlternate.TrySacrificeFor(target), "changing role clears protection");
Console.WriteLine($"RESULT {passed} passed");

