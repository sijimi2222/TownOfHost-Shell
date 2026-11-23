using System;using System.Linq;using Hazel;using AmongUs.GameOptions;using static TownOfHost.PlayerCatch;namespace TownOfHost { public static partial class ChatCommands {public static bool CanUseLocalDevelopmentCommand(PlayerControl player, bool fromLocalInput, OptionItem option)
        {
            return DevelopmentCommandPolicy.CanUseHostDevelopmentCommand(
                fromLocalInput,
                AmongUsClient.Instance?.AmHost == true,
                player != null && player == PlayerControl.LocalPlayer && player.AmOwner
                    && player.Data != null && !player.Data.Disconnected,
                DebugModeManager.AmDebugger,
                option?.GetBool() == true);
        }
public static bool CanUseReviveCommand(PlayerControl player, bool fromLocalInput = false)
            => CanUseLocalDevelopmentCommand(player, fromLocalInput, DebugModeManager.EnableDebugMode);
public static bool CanUseChangeRoleCommand(PlayerControl player, bool fromLocalInput = false)
            => CanUseLocalDevelopmentCommand(player, fromLocalInput, DebugModeManager.EnableTOHhmDebugMode);
public static bool TryHandleRestrictedLocalCommand(string text)
        {
            if (!DevelopmentCommandPolicy.TryGetRestrictedCommand(text, out var command)) return false;
            bool allowed = command is "rev" or "revive"
                ? CanUseReviveCommand(PlayerControl.LocalPlayer, fromLocalInput: true)
                : command != "002" && CanUseChangeRoleCommand(PlayerControl.LocalPlayer, fromLocalInput: true);
            if (allowed) return false;
            var localPlayer = PlayerControl.LocalPlayer;
            var chat = HudManager.Instance?.Chat;
            if (localPlayer != null && chat != null)
                chat.AddChat(localPlayer, command == "002"
                    ? "この公開版ではプレイヤー識別情報の収集・外部送信は利用できません。"
                    : "この開発用コマンドは、デバッグ認証と設定を有効にしたホスト本人だけがローカル入力で使用できます。");
            return true;
        }
public static void ExecuteInGameRoleChange(PlayerControl sender, string[] args, bool fromLocalInput = false)

        {

            if (!CanUseChangeRoleCommand(sender, fromLocalInput))

            {

                Logger.Warn("Denied development command /cmd cr", "ChatCommand");

                return;

            }



            if (!GameStates.InGame || args.Length < 2) return;



            var target = sender;

            if (args.Length >= 3 && byte.TryParse(args[2], out var playerId))

                target = GetPlayerById(playerId) ?? sender;



            if (!GetRoleByInputName(args[1], out var role, true)) return;



            NameColorManager.RemoveAll(target.PlayerId);

            target.RpcSetCustomRole(role, true, true);

            RPC.RpcSyncAllNetworkedPlayer();

            Logger.Info($"/cmd cr: {sender.GetNameWithRole().RemoveHtmlTags()} changed {target.GetNameWithRole().RemoveHtmlTags()} to {role}", "ChatCommand");

        }
public static void ExecuteReviveCommand(PlayerControl sender, string[] args, bool fromLocalInput = false)

        {

            if (!CanUseReviveCommand(sender, fromLocalInput))

            {

                Logger.Warn("Denied development command /cmd rev", "ChatCommand");

                return;

            }



            var target = sender;

            if (args.Length >= 2 && byte.TryParse(args[1], out var playerId))

                target = GetPlayerById(playerId) ?? sender;



            target.Revive();

            target.RpcSetRole(RoleTypes.Crewmate, true);

            target.Data.IsDead = false;



            if (GameStates.InGame)

            {

                var state = PlayerState.GetByPlayerId(target.PlayerId);

                if (state != null)

                {

                    state.IsDead = false;

                    state.DeathReason = CustomDeathReason.etc;

                    target.RpcSetRole(state.MainRole.GetRoleTypes(), true);

                }

            }



            RPC.RpcSyncAllNetworkedPlayer();

            Logger.Info($"/cmd rev: {sender.GetNameWithRole().RemoveHtmlTags()} revived {target.GetNameWithRole().RemoveHtmlTags()}", "ChatCommand");

        }
public static bool StartsWithCmdPrefix(string text)

        {

            if (string.IsNullOrWhiteSpace(text)) return false;



            var commandText = text.TrimStart();

            return commandText.StartsWith("/cmd", StringComparison.OrdinalIgnoreCase)

                && (commandText.Length == 4 || char.IsWhiteSpace(commandText[4]));

        }
public static bool NormalizeLegacyCommandInput(ref string text)

        {

            if (string.IsNullOrWhiteSpace(text)) return false;



            var commandText = text.TrimStart();

            if (!commandText.StartsWith("/", StringComparison.Ordinal)) return false;



            if (StartsWithCmdPrefix(commandText))

            {

                var rest = commandText.Length > 4 ? commandText.Substring(4).TrimStart() : "";

                text = rest.Length == 0 ? "/cmd" : $"/cmd {rest}";

                return false;

            }



            var legacyCommand = commandText.Substring(1).TrimStart();

            if (legacyCommand.Length == 0) return false;



            text = $"/cmd {legacyCommand}";

            return true;

        }
public static void RequestCommandProcessingFromHost(string text)

        {

            if (string.IsNullOrWhiteSpace(text) || AmongUsClient.Instance == null || PlayerControl.LocalPlayer == null) return;
            if (DevelopmentCommandPolicy.TryGetRestrictedCommand(text, out _)) return;

            if (AmongUsClient.Instance.AmHost) return;



            var writer = AmongUsClient.Instance.StartRpcImmediately(

                PlayerControl.LocalPlayer.NetId,

                (byte)CustomRPC.RequestCommandProcessing,

                SendOption.Reliable,

                AmongUsClient.Instance.HostId);

            writer.Write(text);

            AmongUsClient.Instance.FinishRpcImmediately(writer);

        }
public static bool TrySendCmdlessCommandDirectly(ChatController chatController)

        {

            if (chatController == null || AmongUsClient.Instance == null || AmongUsClient.Instance.AmHost

                || PlayerControl.LocalPlayer == null || chatController.quickChatField.Visible) return false;



            var textArea = chatController.freeChatField?.textArea;

            if (textArea == null || string.IsNullOrWhiteSpace(textArea.text)) return false;



            var rawText = textArea.text;
            if (TryHandleRestrictedLocalCommand(rawText))
            {
                textArea.Clear();
                return true;
            }


            var trimmedText = rawText.TrimStart();

            if (!trimmedText.StartsWith("/") || StartsWithCmdPrefix(trimmedText)) return false;



            NormalizeLegacyCommandInput(ref rawText);

            if (!StartsWithCmdPrefix(rawText)) return false;



            RequestCommandProcessingFromHost(rawText);

            chatController.timeSinceLastMessage = 3f;

            textArea.Clear();

            return true;

        }
public static bool ReachedNormal; public static void ReceivedGate(PlayerControl player,string text,out bool canceled,bool Isclient=false){

            



            if (player != null)

            {

                var tag = !player.Data.IsDead ? "SendChatAlive" : "SendChatDead";

            }



            canceled = false;
            // Received chat/RPC is never evidence of local host input, even if its
            // PlayerControl or claimed owner points at the host.
            if (DevelopmentCommandPolicy.TryGetRestrictedCommand(text, out _))
            {
                canceled = true;
                return;
            }


ReachedNormal=true;}
public static bool LocalGate(ChatController __instance){            var text = __instance.freeChatField.textArea.text;
            if (TryHandleRestrictedLocalCommand(text))
            {
                __instance.freeChatField.textArea.Clear();
                return false;
            }


return true;} }}