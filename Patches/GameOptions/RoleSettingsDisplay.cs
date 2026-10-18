using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TownOfHost.Roles.Core;

namespace TownOfHost;

// Local presentation state only. Never create options, save values or send RPCs here.
internal static class RoleSettingsDisplay
{
    internal enum Mode { Gojuon, Mod }
    internal static Mode CurrentMode { get; private set; } = Mode.Gojuon;
    internal static string TargetButtonText => CurrentMode == Mode.Gojuon ? "MOD順\nにする" : "あいうえお\n順にする";
    internal static string TargetName => CurrentMode == Mode.Gojuon ? "MOD順" : "あいうえお順";

    internal sealed class Origin
    {
        internal readonly string Key, Name;
        internal readonly bool Unset;
        internal Origin(string key, string name, bool unset = false) { Key = key; Name = name; Unset = unset; }
    }
    private static readonly Origin Unknown = new("unset", "出典未設定", true);
    private sealed class Block
    {
        internal CustomRoles Role;
        internal Origin Origin;
        internal readonly List<OptionItem> Options = new();
    }
    private sealed class Plan
    {
        internal int Count;
        internal readonly List<OptionItem> Options = new();
        internal readonly Dictionary<OptionItem, Origin> Origins = new();
        internal readonly Dictionary<OptionItem, Origin> Headers = new();
    }
    private static readonly Dictionary<TabGroup, Plan> Plans = new();
    private static readonly HashSet<(Mode Mode, TabGroup Tab, string Key)> Collapsed = new();

    // Pure presentation state: no option IDs, persisted values, or RPCs.
    internal static Origin HeaderFor(TabGroup tab, OptionItem option)
    {
        if (!IsRoleList(tab) || RootRole(option) == CustomRoles.NotAssigned) return null;
        var plan = GetPlan(tab);
        if (plan.Headers.TryGetValue(option, out var cached)) return cached;
        var header = CreateHeader(tab, option);
        plan.Headers[option] = header;
        return header;
    }
    private static Origin CreateHeader(TabGroup tab, OptionItem option)
    {
        if (CurrentMode == Mode.Mod) return SectionFor(tab, option);
        var reading = RoleSettingsReadings.Key(RootRole(option));
        if (reading.Length == 0) return new("kana:unknown", "読み未設定");
        const string starts = "あかさたなはまやらわ";
        var c = reading[0];
        if (c >= 'あ' && c <= 'ん')
        {
            int index = 0;
            while (index + 1 < starts.Length && c >= starts[index + 1]) index++;
            return new("kana:" + starts[index], starts[index] + "行");
        }
        // Keep the existing lexical order even for names without a kana reading.
        return new("initial:" + c, c.ToString());
    }
    internal static bool IsCollapsed(TabGroup tab, Origin header)
        => header != null && Collapsed.Contains((CurrentMode, tab, header.Key));
    internal static bool AnyCollapsed(TabGroup tab)
        => DisplayOptions(tab).Select(option => HeaderFor(tab, option)).Any(header => IsCollapsed(tab, header));
    internal static void ToggleHeader(TabGroup tab, Origin header)
    {
        if (header == null) return;
        var key = (CurrentMode, tab, header.Key);
        if (!Collapsed.Remove(key)) Collapsed.Add(key);
    }
    internal static void SetAllCollapsed(TabGroup tab, bool collapsed)
    {
        foreach (var header in DisplayOptions(tab).Select(option => HeaderFor(tab, option)).Where(header => header != null))
        {
            var key = (CurrentMode, tab, header.Key);
            if (collapsed) Collapsed.Add(key); else Collapsed.Remove(key);
        }
    }

    internal static bool IsRoleList(TabGroup tab) => tab is TabGroup.ImpostorRoles
        or TabGroup.MadmateRoles or TabGroup.CrewmateRoles or TabGroup.NeutralRoles
        or TabGroup.Combinations or TabGroup.Addons or TabGroup.GhostRoles;

    // This old section divider no longer encloses a contiguous set after either display sort.
    // Hide only its decoration; keep the option and all underlying role/settings values.
    internal static bool IsLegacyGroupHeader(OptionItem option)
        => option is ObjectOptionitem { IsHedderObject: true } && option.Name == "Group-Addon";

    internal static void Reset() { CurrentMode = Mode.Gojuon; Plans.Clear(); Collapsed.Clear(); }
    internal static void Toggle() { CurrentMode = CurrentMode == Mode.Gojuon ? Mode.Mod : Mode.Gojuon; Plans.Clear(); }

    internal static CustomRoles RootRole(OptionItem option)
    {
        var root = option;
        while (root.Parent != null) root = root.Parent;
        var role = ShellRoleSettingsGroup.RoleOf(root);
        return role == CustomRoles.NotAssigned ? ShellRoleSettingsGroup.RoleOf(option) : role;
    }

    private static string Credit(string text)
    {
        text = Regex.Replace(text ?? "", "<[^>]*>", "");
        var match = Regex.Match(text, @"(?:^|\s)From\s*:\s*([^\r\n]*)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }
    private static string OriginName(From origin)
        => origin == From.TownOfHost_hamo ? ShellRoleOrigins.HamoDisplayName : Credit(UtilsOption.GetFrom(origin));
    private static string AliasKey(string name) => Regex.Replace(name, @"[\s_\-]+", "").ToLowerInvariant();

    // Explicit role metadata wins. Literal credits are read only when there is no typed origin.
    // A missing source remains unknown; neither faction nor role name is used to infer it.
    internal static Origin OriginFor(CustomRoles role, OptionItem root = null)
    {
        var from = ShellRoleOrigins.Resolve(role,
            CustomRoleManager.AllRolesInfo.TryGetValue(role, out var info) ? info.From : From.None);
        if (from != From.None) return new("from:" + from, OriginName(from));
        string name = Credit(root?.Fromtext);
        if (name.Length == 0) return Unknown;
        foreach (From candidate in Enum.GetValues(typeof(From)))
        {
            if (candidate != From.None && AliasKey(name) == AliasKey(OriginName(candidate)))
                return new("from:" + candidate, OriginName(candidate));
        }
        return new("literal:" + name, name);
    }

    // A pair occupies one existing settings block. Mixed sources get an explicit combined header.
    internal static Origin BlockOrigin(CustomRoles role, OptionItem root)
    {
        if (!CustomRoleManager.AllRolesInfo.TryGetValue(role, out var info)
            || info.Combination == CombinationRoles.None) return OriginFor(role, root);
        var sources = CustomRoleManager.AllRolesInfo.Values
            .Where(member => member.Combination == info.Combination)
            .Select(member => OriginFor(member.RoleName, member.RoleName == role ? root : null))
            .GroupBy(origin => origin.Key).Select(group => group.First())
            .OrderBy(origin => origin.Unset).ThenBy(origin => origin.Name, StringComparer.Ordinal).ToList();
        return sources.Count == 1 ? sources[0] : sources.Count == 0 ? OriginFor(role, root)
            : new("mixed:" + string.Join("|", sources.Select(origin => origin.Key)),
                string.Join(" / ", sources.Select(origin => origin.Name)));
    }

    // Promote only these existing source groups; preserve the previous tie-breakers for all others.
    // Mixed-source combination groups stay independent and are never attributed to one member.
    private static int ModGroupPriority(Origin origin) => origin.Key switch
    {
        "from:" + nameof(From.TownOfHost_Shell) => 0,
        "from:" + nameof(From.TownOfHost_hamo) => 1,
        "from:" + nameof(From.TownOfHost_Pko) => 2,
        "from:" + nameof(From.TownOfHost_K) => 3,
        "from:" + nameof(From.TownOfHost_Y) => 4,
        _ => 5
    };

    private static Plan GetPlan(TabGroup tab)
    {
        if (Plans.TryGetValue(tab, out var cached) && cached.Count == OptionItem.AllOptions.Count) return cached;
        var plan = new Plan { Count = OptionItem.AllOptions.Count };
        var options = OptionItem.AllOptions.Where(option => option.Tab == tab).ToList();
        if (!IsRoleList(tab)) { plan.Options.AddRange(options); return plan; }
        var blocks = new Dictionary<CustomRoles, Block>();
        var shared = new List<OptionItem>();
        foreach (var option in options)
        {
            var role = RootRole(option);
            if (role == CustomRoles.NotAssigned) { shared.Add(option); continue; }
            if (!blocks.TryGetValue(role, out var block))
            {
                var root = option;
                while (root.Parent != null) root = root.Parent;
                block = new Block { Role = role, Origin = BlockOrigin(role, root) };
                blocks.Add(role, block);
            }
            block.Options.Add(option);
            plan.Origins.Add(option, block.Origin);
        }
        IEnumerable<Block> ordered = CurrentMode == Mode.Mod
            ? blocks.Values.OrderBy(block => block.Origin.Unset)
                .ThenBy(block => ModGroupPriority(block.Origin))
                .ThenBy(block => block.Origin.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(block => block.Origin.Key, StringComparer.Ordinal)
                .ThenBy(block => RoleSettingsReadings.Key(block.Role), StringComparer.Ordinal)
            : blocks.Values.OrderBy(block => RoleSettingsReadings.Key(block.Role), StringComparer.Ordinal);
        foreach (var block in ordered) plan.Options.AddRange(block.Options);
        plan.Options.AddRange(shared); // Retain common faction settings after the role blocks.
        Plans[tab] = plan;
        return plan;
    }

    internal static IEnumerable<OptionItem> DisplayOptions(TabGroup tab) => GetPlan(tab).Options;
    internal static Origin SectionFor(TabGroup tab, OptionItem option)
        => GetPlan(tab).Origins.TryGetValue(option, out var origin) ? origin : null;
}
