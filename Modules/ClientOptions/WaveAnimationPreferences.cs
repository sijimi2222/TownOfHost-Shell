using System;
using BepInEx.Configuration;

namespace TownOfHost.Modules.ClientOptions;

internal static class WaveAnimationPreferences
{
    internal const string Section = "Client Options";
    internal const string Key = "Disable Wave Animation";
    internal static ConfigEntry<bool> Bind(ConfigFile config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        return config.Bind(Section, Key, false, "Skip the seaside wave screen transition. Button feedback remains enabled.");
    }
}
