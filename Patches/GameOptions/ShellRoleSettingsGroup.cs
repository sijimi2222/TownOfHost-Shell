using System.Collections.Generic;
using System.Linq;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost;

// Presentation only: do not reorder AllOptions, serialized values or RoleInfo.
internal static class ShellRoleSettingsGroup
{
    const string HeaderName = "ShellRolesSubheader";
    const string OtherHeaderName = "OtherRolesSubheader";
    internal static bool IsRoleTab(TabGroup tab) => tab is TabGroup.CrewmateRoles
        or TabGroup.ImpostorRoles or TabGroup.MadmateRoles or TabGroup.NeutralRoles;

    static CustomRoles RoleOf(OptionItem option)
    {
        // Follow existing parents too: nested settings need not repeat ParentRole.
        for (var current = option; current != null; current = current.Parent)
        {
            if (current.CustomRole != CustomRoles.NotAssigned) return current.CustomRole;
            if (current.ParentRole != CustomRoles.NotAssigned) return current.ParentRole;
        }
        return CustomRoles.NotAssigned;
    }

    // Destroyer is an explicitly requested display-only exception; preserve its Nebula From.
    internal static bool IsShell(OptionItem option)
        => RoleOf(option) is CustomRoles.Chief or CustomRoles.Tuner or CustomRoles.Lethe
                or CustomRoles.Destroyer or CustomRoles.MadCleaner
            || (CustomRoleManager.AllRolesInfo.TryGetValue(RoleOf(option), out var info)
                && info.From == From.TownOfHost_Shell);

    // Shared faction settings were registered after the role list; keep that trailing block.
    internal static int Group(OptionItem option)
    {
        var role = RoleOf(option);
        if (role == CustomRoles.NotAssigned) return 3;
        if (IsShell(option)) return 1;
        return CustomRoleManager.AllRolesInfo.TryGetValue(role, out var info)
            && info.From == From.AmongUs ? 0 : 2;
    }

    static int Priority(OptionItem option) => RoleOf(option) switch
    {
        CustomRoles.Chief => 0,
        CustomRoles.Tuner => 1,
        CustomRoles.Lethe => 2,
        CustomRoles.Destroyer or CustomRoles.MadCleaner => 0,
        CustomRoles.Prankster => 0,
        CustomRoles.Streamer => 1,
        CustomRoles.Tiger => 2,
        _ => 3
    };

    internal static IEnumerable<OptionItem> DisplayOptions(TabGroup tab)
    {
        var options = OptionItem.AllOptions.Where(option => option.Tab == tab);
        // Stable ordering preserves existing order inside Vanilla/Other and each role's child rows.
        return IsRoleTab(tab)
            ? options.OrderBy(Group)
                .ThenBy(option => IsShell(option) ? Priority(option) : 0)
            : options;
    }

    internal static void HideHeader(GameOptionsMenu menu)
    {
        var existing = menu.transform.Find(HeaderName);
        if (existing != null) existing.gameObject.SetActive(false);
        var other = menu.transform.Find(OtherHeaderName);
        if (other != null) other.gameObject.SetActive(false);
    }

    internal static void PlaceHeader(GameOptionsMenu menu, ref float offset, int group)
    {
        string headerName = group == 1 ? HeaderName : OtherHeaderName;
        var existing = menu.transform.Find(headerName);
        CategoryHeaderMasked header;
        if (existing == null)
        {
            var template = GameSettingMenuStartPatch.ModSettingsTab.transform
                .Find("Scroller/SliderInner/ChancesTab/CategoryHeaderMasked")
                .GetComponent<CategoryHeaderMasked>();
            header = Object.Instantiate(template, menu.transform);
            header.name = headerName;
            // Same coordinates as the existing header wrapper, without cloning its other children.
            header.transform.localPosition = template.transform.localPosition + new Vector3(-0.7789f, -0.15f, -10f);
            header.Title.DestroyTranslator();
            header.Title.fontSize *= 0.8f;
        }
        else header = existing.GetComponent<CategoryHeaderMasked>();
        header.Title.text = "<b>" + Translator.GetString(headerName) + "</b>";
        offset -= 0.68f;
        var position = header.transform.localPosition;
        header.transform.localPosition = new Vector3(position.x, offset - 1.65f, position.z);
        header.gameObject.SetActive(true);
        offset -= 0.23f;
    }
}
