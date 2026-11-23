using TownOfHost.Modules.ClientOptions; namespace TownOfHost; static class Registration {static ClientActionItem EnableDiscordRichPresence,ShowRoomCode,DisableWaveAnimation; public static void Clear(){EnableDiscordRichPresence=ShowRoomCode=DisableWaveAnimation=null;ClientActionItem.Items.Clear();} public static void Build(OptionsMenuBehaviour __instance){if ((EnableDiscordRichPresence == null || EnableDiscordRichPresence.ToggleButton == null) && !Main.IsAndroid())

            {

                EnableDiscordRichPresence = ClientOptionItem.Create("EnableDiscordRichPresence", Main.EnableDiscordRichPresence, __instance, showTooltip: true, page: 1);

            }
if (ShowRoomCode == null || ShowRoomCode.ToggleButton == null)

            {

                ShowRoomCode = ClientOptionItem.Create("ShowRoomCode", Main.ShowRoomCode, __instance, page: 1);

            }
if (DisableWaveAnimation == null || DisableWaveAnimation.ToggleButton == null)
            {
                DisableWaveAnimation = ClientOptionItem.Create("DisableWaveAnimation", Main.DisableWaveAnimation, __instance, page: 1);
            }}}