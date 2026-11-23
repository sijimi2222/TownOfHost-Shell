using System;using System.Linq;using System.Collections.Generic;using System.Text;using TownOfHost.Modules;namespace TownOfHost{public static class OptionSerializer {
public static string GenerateModOptionsString()
    {
        var builder = new StringBuilder(1024);
        var options = OptionItem.AllOptions.Where(option => option is not PresetOptionItem).OrderBy(option => option.Id);
        var lastId = 0;
        foreach (var option in options)
        {
            var value = option.GetValue();
            // Recovered IDs must be explicit even at zero, so absence means an old preset.
            if (value == 0 && !ShellOptionIds.IsRecoveredOption(option.Id))
            {
                continue;
            }
            var idDelta = option.Id - lastId;
            builder.Append(idDelta == 1 ? "" : Base62.ToBase62(idDelta)).Append(',').Append(value == 1 ? "" : Base62.ToBase62(value)).Append('!');
            lastId = option.Id;
        }
        return builder.ToString();
    }
public static void LoadModOptionsString(string source)
    {
        var modOptions = source.Split('!', StringSplitOptions.RemoveEmptyEntries);
        var parsedModOptions = new Dictionary<int, int>(modOptions.Length);
        var lastId = 0;
        foreach (var modOption in modOptions)
        {
            var split = modOption.Split(',');
            var idDelta = split[0] == "" ? 1 : Base62.ToInt(split[0]);
            lastId += idDelta;
            var value = split[1] == "" ? 1 : Base62.ToInt(split[1]);
            parsedModOptions[lastId] = value;
        }
        foreach (var option in OptionItem.AllOptions)
        {
            if (option is PresetOptionItem)
            {
                continue;
            }
            var optionvalue = parsedModOptions.TryGetValue(option.Id, out var value) ? value
                : ShellOptionIds.IsRecoveredOption(option.Id) ? option.DefaultValue : 0;
            if (option.GetValue() == optionvalue) continue;
            option.SetValue(optionvalue, false);
        }
        OptionItem.SyncAllOptions();
    }
}}