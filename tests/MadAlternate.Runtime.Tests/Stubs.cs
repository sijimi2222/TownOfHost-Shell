using AmongUs.GameOptions;
using TownOfHost.Roles.Core;

namespace AmongUs.GameOptions
{
    public interface IGameOptions { }
    public enum RoleTypes { Engineer, Impostor }
}

namespace TownOfHost
{
    public enum CustomDeathReason { etc = -1, Sacrifice = 39 }
    public class PlayerState { public CustomDeathReason DeathReason = CustomDeathReason.etc; }
    public class PlayerData { public bool Disconnected; }
    public class PlayerPhysics { }
    public enum MurderResultFlags { Succeeded }
    public class PlayerControl(byte id)
    {
        public byte PlayerId = id;
        public bool Alive = true;
        public bool TasksFinished;
        public bool AllowDeath = true;
        public Action BeforeMurder;
        public PlayerData Data = new();
        public RoleBase Role;
        public PlayerState State = new();
        public bool IsAlive() => Alive;
        public RoleBase GetRoleClass() => Role;
        public PlayerState GetPlayerState() => State;
        public void RpcMurderPlayerV2(PlayerControl target, MurderResultFlags resultFlags)
        {
            BeforeMurder?.Invoke();
            if (AllowDeath) target.Alive = false;
        }
    }
    public class AmongUsClient
    {
        public static AmongUsClient Instance = new();
        public bool AmHost = true;
    }
    public static class GameStates { public static bool IsInTask, CalledMeeting; }
    public static class PlayerCatch
    {
        public static List<PlayerControl> Players = [];
        public static IEnumerable<PlayerControl> AllAlivePlayerControls => Players.Where(p => p.IsAlive()).ToArray();
        public static PlayerControl GetPlayerById(byte id) => Players.Find(p => p.PlayerId == id);
    }
    public class OptionItem
    {
        public bool Enabled;
        public bool GetBool() => Enabled;
        public float GetFloat() => 0;
    }
    public static class BooleanOptionItem
    {
        public static OptionItem Last;
        public static OptionItem Create(SimpleRoleInfo role, int id, Options.GeneralOption option, bool enabled, bool single)
            => Last = new() { Enabled = enabled };
    }
    public static class OverrideTasksData
    {
        public static (bool, int, int, int) Settings;
        public static IntegerValueRule TaskCountRule;
        public static void Create(SimpleRoleInfo info, int id, (bool, int, int, int) tasks, IntegerValueRule taskCountRule)
        {
            Settings = tasks;
            TaskCountRule = taskCountRule;
        }
    }
    public static class Options
    {
        public enum GeneralOption { CanVent }
        public static OptionItem MadmateVentCooldown = new(), MadmateVentMaxTime = new(),
            MadmateCanSeeKillFlash = new(), MadmateCanSeeDeathReason = new();
        public static CustomRoles MadTellOpt() => CustomRoles.MadAlternate;
    }
    public static class AURoleOptions { public static float EngineerCooldown, EngineerInVentMaxTime; }
    public static class Utils { public static void SendMessage(string message, byte id) { } }
    public static class UtilsName { public static string GetPlayerColor(PlayerControl p, bool real) => p.PlayerId.ToString(); }
}
namespace TownOfHost.Modules
{
    public static class SelfVoteManager
    {
        public static bool Enabled = true;
        public static bool Canuseability() => Enabled;
    }
    public static class MeetingVoteManager { public const byte Skip = 253; }
    public static class UtilsGameLog { public static void AddGameLog(string tag, string message) { } }
}
namespace TownOfHost.Roles.AddOns.Common
{
    public static class Amnesia
    {
        public static bool AbilityEnabled = true;
        public static bool CheckAbility(PlayerControl p) => AbilityEnabled;
    }
}
namespace TownOfHost.Roles.Core
{
    public enum CustomRoles { MadAlternate }
    public enum CustomRoleTypes { Madmate }
    public enum HasTask { ForRecompute }
    public enum From { TownOfHost_Shell }
    public class SimpleRoleInfo(Func<RoleTypes> baseRole, Action setup)
    {
        public Func<RoleTypes> BaseRole = baseRole;
        public Action Setup = setup;
        public static SimpleRoleInfo Create(Type type, Func<PlayerControl, RoleBase> factory, CustomRoles role,
            Func<RoleTypes> baseRole, CustomRoleTypes side, int id, Action setup, string alias, string color,
            (int, int) order, Func<object> introSound, From from, bool isNewRole) => new(baseRole, setup);
    }
    public class MurderInfo { }
    public class RoleBase(SimpleRoleInfo _, PlayerControl player, Func<HasTask> __)
    {
        protected PlayerControl Player = player;
        protected bool IsTaskFinished => Player.TasksFinished;
        protected bool Is(PlayerControl voter) => voter == Player;
        protected static object GetIntroSound(RoleTypes role) => null;
        protected static string GetString(string key) => key switch
        {
            "MadAlternate.Selected" => "{0}",
            "MadAlternate.Sacrifice" => "{0} {1}",
            _ => key
        };
        public virtual bool CanClickUseVentButton => true;
        public virtual bool OnEnterVent(PlayerPhysics physics, int ventId) => true;
        public virtual void ApplyGameOptions(IGameOptions opt) { }
        public virtual CustomRoles TellResults(PlayerControl player) => default;
        public virtual void OnStartMeeting() { }
        public virtual bool CheckVoteAsVoter(byte votedForId, PlayerControl voter) => true;
    }
}
namespace TownOfHost.Roles.Core.Interfaces
{
    public interface ISelfVoter { bool CanUseVoted(); }
    public interface IKillFlashSeeable { bool? CheckKillFlash(MurderInfo info); }
    public interface IDeathReasonSeeable { bool? CheckSeeDeathReason(PlayerControl seen); }
}

