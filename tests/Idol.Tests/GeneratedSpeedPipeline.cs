// Extracted by RefreshIntegrationFixtures.py; do not edit.
using TownOfHost.Roles.Core;using TownOfHost.Roles.AddOns.Common;using Mathf=UnityEngine.Mathf;namespace TownOfHost;internal static class RealSpeedPipeline{internal static float Build(PlayerControl player){var state=player.GetPlayerState();bool HasRoleAddon=false;var data=new RoleAddonStub();AURoleOptions.PlayerSpeedMod=1f;
if (Main.AllPlayerSpeed.TryGetValue(player.PlayerId, out var speed))

            {

                if (0.25f <= speed)

                {

                    var addspeed = player.Is(CustomRoles.Speeding) ? Speeding.Speed : 0;

                    if (HasRoleAddon) addspeed = data.GiveSpeeding.GetBool() ? data.Speed.GetFloat() : addspeed;

                    speed += addspeed;

                }

                if (player.GetRoleClass() is TownOfHost.Roles.Neutral.Tiger tiger) speed *= tiger.SpeedMultiplier;
                speed = TownOfHost.Roles.Crewmate.Idol.ApplyMovement(player, speed);
                if (state.CanMove is false) speed = Main.MinSpeed;

                AURoleOptions.PlayerSpeedMod = Mathf.Clamp(speed, Main.MinSpeed, 10f);

            }
return AURoleOptions.PlayerSpeedMod;}}
