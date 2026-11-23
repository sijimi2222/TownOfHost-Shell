using System;using System.Linq;using System.Collections.Generic;namespace TownOfHost.Modules.ChatManager { public class ChatManager { public static bool IsForceSend;
private static bool TryGetMessageRecipient(PlayerControl recipient, out int clientId)
        {
            clientId = -1;
            if (recipient == null || recipient.Data == null || recipient.Data.Disconnected) return false;
            clientId = recipient.GetClientId();
            return clientId >= 0;
        }
public static void IntaskCheckSendMessage(PlayerControl player)
        {
            if (PlayerControl.LocalPlayer.IsAlive() && !ChatUpdatePatch.DoBlockChat)
            {
                if (Main.MessagesToSend.Count(x => x.Item2 is not byte.MaxValue) > 0)
                {
                    if (Utils.IsRestriction())
                    {
                        SendMessageInGame(null);
                        return;
                    }
                    (string msg, byte sendTo, string title) = Main.MessagesToSend.FirstOrDefault(x => x.Item2 is not byte.MaxValue);
                    if (sendTo is not byte.MaxValue && Main.MegCount < 50)
                    {
                        Main.MessagesToSend.Remove((msg, sendTo, title));
                        var sendpc = PlayerCatch.GetPlayerById(sendTo);
                        if (!TryGetMessageRecipient(sendpc, out int clientId)) return;
                        if (sendpc != null)
                        {
                            var name = sendpc.Data.GetLogPlayerName();
                            var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
                            writer.StartMessage(clientId);
                            writer.StartRpc(sendpc.NetId, (byte)RpcCalls.SetName)
                                .Write(player.Data.NetId)
                                .Write(title)
                                .EndRpc();
                            writer.StartRpc(sendpc.NetId, (byte)RpcCalls.SendChat)
                                .Write(msg)
                                .EndRpc();
                            writer.EndMessage();
                            writer.SendMessage();
                            UtilsNotifyRoles.NotifyRoles(true, false, true, [sendpc]);
                            if (Options.ExRpcWeightR.GetBool()) Main.MegCount++;
                        }
                    }
                }
            }
        }
public static void SendMessageInGame(ChatController chatController)
        {
            PlayerControl senderplayer = PlayerCatch.AllAlivePlayerControls.OrderBy(x => x.PlayerId).FirstOrDefault();
            if (senderplayer == null) return;

            (string msg, byte sendTo, string title) = Main.MessagesToSend[0];
            var name = senderplayer.Data.GetLogPlayerName();
            var SendToPlayerControl = PlayerCatch.GetPlayerById(sendTo);
            int clientId = -1;
            if (sendTo != byte.MaxValue && !TryGetMessageRecipient(SendToPlayerControl, out clientId))
            {
                Main.MessagesToSend.RemoveAt(0);
                return;
            }
            if (msg == "") msg = ".";

            if (Options.ExRpcWeightR.GetBool()) Main.MegCount++;
            // バニラ鯖使用下の状況
            if (Utils.IsRestriction())
            {
                //ホスト死亡時
                if (PlayerControl.LocalPlayer.IsAlive() is false)
                {
                    //苦肉の応急手当。一瞬だけホスト復活
                    Main.MessagesToSend.RemoveAt(0);
                    var Ischatopen = HudManager.Instance?.Chat?.IsOpenOrOpening ?? false;
                    senderplayer = PlayerControl.LocalPlayer;
                    name = senderplayer.Data.GetLogPlayerName();
                    // ホスト視点でのチャット送信
                    if (sendTo == byte.MaxValue || sendTo == senderplayer.PlayerId)
                    {
                        senderplayer.SetName(title);
                        DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderplayer, msg);
                        senderplayer.SetName(name);
                        if (chatController is not null)
                            chatController.timeSinceLastMessage = sendTo is byte.MaxValue ? 0 : Main.MessageWait.Value - 0.2f;
                        if (sendTo == senderplayer.PlayerId) return;
                    }
                    IsForceSend = true;
                    foreach (var seer in PlayerCatch.AllPlayerControls)
                    {
                        if (seer.PlayerId == PlayerControl.LocalPlayer.PlayerId) continue;
                        if (seer.PlayerId == sendTo || sendTo is byte.MaxValue)
                        {
                            if (!TryGetMessageRecipient(seer, out int seerclientid)) continue;

                            GameDataSerializePatch.SerializeMessageCount++;
                            var Nwriter = CustomRpcSender.Create("MessagesToSend", SendOption.None);
                            Nwriter.StartMessage(seerclientid);
                            if (seer.IsAlive())
                            {
                                senderplayer.Data.IsDead = false;
                                Nwriter.Write((writer) =>
                                {
                                    writer.StartMessage(1); //0x01 Data
                                    {
                                        writer.WritePacked(senderplayer.Data.NetId);
                                        senderplayer.Data.Serialize(writer, false);
                                    }
                                    writer.EndMessage();
                                }, true);
                            }
                            Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SetName)
                            .Write(senderplayer.Data.NetId)
                            .Write(title)
                            .EndRpc();
                            if (ChatControllerUpdatePatch.IsQuickChatOnly)
                            {
                                Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SendQuickChat)
                                .Write((byte)QuickChatPhraseType.SimplePhrase)
                                .Write((uint)StringNames.QCGG)
                                .EndRpc();
                            }
                            else
                            {
                                Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SendChat)
                                .Write(msg)
                                .EndRpc();
                            }
                            if (seer.IsAlive())
                            {
                                senderplayer.Data.IsDead = true;
                                Nwriter.Write((writer) =>
                                {
                                    writer.StartMessage(1); //0x01 Data
                                    {
                                        writer.WritePacked(senderplayer.Data.NetId);
                                        senderplayer.Data.Serialize(writer, false);
                                    }
                                    writer.EndMessage();
                                }, true);
                            }
                            GameDataSerializePatch.SerializeMessageCount--;
                            Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SetName)
                            .Write(senderplayer.Data.NetId)
                            .Write(name)
                            .EndRpc();
                            Nwriter.EndMessage();
                            Nwriter.SendMessage();
                        }
                    }
                    senderplayer.Die(DeathReason.Kill, false);
                    IsForceSend = false;
                    // 会議中は、送り終わったらホストの名前(色付き)を送り直す(生きている時と同じ)
                    if (GameStates.CalledMeeting && Main.MessagesToSend.Count < 1)
                    {
                        var host = senderplayer;
                        _ = new LateTask(() =>
                        {
                            NameColorManager.RpcMeetingColorName(host);
                            ChatUpdatePatch.DoBlockChat = false;
                        }, Main.LagTime, "Setname", true);
                    }
                    if (chatController is not null)
                        chatController.timeSinceLastMessage = sendTo is byte.MaxValue ? 0 : Main.MessageWait.Value - 0.2f;
                    return;
                }
                else//ホスト生存時はホストに喋らせる
                {
                    senderplayer = PlayerControl.LocalPlayer;
                    name = senderplayer.Data.GetLogPlayerName();
                    Main.MessagesToSend.RemoveAt(0);
                    // ホスト視点でのチャット送信
                    if (sendTo == byte.MaxValue)
                    {
                        senderplayer.SetName(title);
                        DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderplayer, msg);
                        senderplayer.SetName(name);
                        if (chatController is not null)
                            chatController.timeSinceLastMessage = 0;
                    }
                    var Nwriter = CustomRpcSender.Create("MessagesToSend", SendOption.None);
                    Nwriter.StartMessage(clientId);
                    Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SetName)
                    .Write(senderplayer.Data.NetId)
                    .Write(title)
                    .EndRpc();
                    if (ChatControllerUpdatePatch.IsQuickChatOnly)
                    {
                        Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SendQuickChat)
                        .Write((byte)QuickChatPhraseType.SimplePhrase)
                        .Write((uint)StringNames.QCGG)
                        .EndRpc();
                    }
                    else
                    {
                        Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SendChat)
                        .Write(msg)
                        .EndRpc();
                    }
                    Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SetName)
                    .Write(senderplayer.Data.NetId)
                    .Write(senderplayer.Data.GetLogPlayerName())
                    .EndRpc();
                    Nwriter.EndMessage();
                    Nwriter.SendMessage();
                    if (GameStates.CalledMeeting && Main.MessagesToSend.Count < 1)
                    {
                        _ = new LateTask(() =>
                        {
                            NameColorManager.RpcMeetingColorName(senderplayer);
                            ChatUpdatePatch.DoBlockChat = false;
                        }, Main.LagTime, "Setname", true);
                    }
                    if (chatController is not null)
                        chatController.timeSinceLastMessage = sendTo is byte.MaxValue ? 0 : Main.MessageWait.Value - 0.2f;
                    return;
                }
            }

            // タスク中で送信者が生きてて全員に表示 => 個別送信に切り替え、名前をその人視点の者に戻す
            if (GameStates.CalledMeeting is false && senderplayer.IsAlive() && sendTo == byte.MaxValue)
            {
                Main.MessagesToSend.RemoveAt(0);

                foreach (var seer in PlayerCatch.AllPlayerControls)
                {
                    if (!TryGetMessageRecipient(seer, out int seerclientid)) continue;
                    string playername = seer.GetRealName(isMeeting: true);
                    playername = playername.ApplyNameColorData(seer, seer, true);
                    if (Main.LastNotifyNames.TryGetValue((seer.PlayerId, seer.PlayerId), out var lastname))
                    {
                        playername = lastname;
                    }

                    var Nwriter = CustomRpcSender.Create("MessagesToSend", SendOption.None);
                    Nwriter.StartMessage(seerclientid);
                    Nwriter.StartRpc(seer.NetId, (byte)RpcCalls.SetName)
                    .Write(seer.Data.NetId)
                    .Write(title)
                    .EndRpc();
                    Nwriter.StartRpc(seer.NetId, (byte)RpcCalls.SendChat)
                    .Write(msg)
                    .EndRpc();
                    Nwriter.StartRpc(seer.NetId, (byte)RpcCalls.SetName)
                    .Write(seer.Data.NetId)
                    .Write(playername)
                    .EndRpc();
                    Nwriter.EndMessage();
                    Nwriter.SendMessage();
                    UtilsNotifyRoles.NotifyRoles();
                }
                if (chatController is not null)
                    chatController.timeSinceLastMessage = sendTo is byte.MaxValue ? 0 : Main.MessageWait.Value - 0.2f;
                return;
            }

            if (Options.ExHideChatCommand.GetBool() is false ||//秘匿Off
                    (senderplayer.PlayerId == 0 && sendTo == byte.MaxValue))//秘匿Onだけど、Snedplayrがホストかつ全員に送信
            {
                Main.MessagesToSend.RemoveAt(0);
                // ホスト視点でのチャット送信
                if (sendTo == byte.MaxValue)
                {
                    senderplayer.SetName(title);
                    DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderplayer, msg);
                    senderplayer.SetName(name);
                    if (chatController is not null)
                        chatController.timeSinceLastMessage = 0;
                }
                var Nwriter = CustomRpcSender.Create("MessagesToSend", SendOption.None);
                Nwriter.StartMessage(clientId);
                Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SetName)
                .Write(senderplayer.Data.NetId)
                .Write(title)
                .EndRpc();
                Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SendChat)
                .Write(msg)
                .EndRpc();
                Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SetName)
                .Write(senderplayer.Data.NetId)
                .Write(senderplayer.Data.GetLogPlayerName())
                .EndRpc();
                Nwriter.EndMessage();
                Nwriter.SendMessage();
                if (GameStates.CalledMeeting && Main.MessagesToSend.Count < 1)
                {
                    _ = new LateTask(() =>
                    {
                        NameColorManager.RpcMeetingColorName(senderplayer);
                        ChatUpdatePatch.DoBlockChat = false;
                    }, Main.LagTime, "Setname", true);
                }
                if (chatController is not null)
                    chatController.timeSinceLastMessage = sendTo is byte.MaxValue ? 0 : Main.MessageWait.Value - 0.2f;
                return;
            }
            if (Options.ExHideChatCommand.GetBool())
            {
                Main.MessagesToSend.RemoveAt(0);
                List<PlayerControl> Seers = new();
                Seers = sendTo == byte.MaxValue ? PlayerCatch.AllPlayerControls.ToList() : [SendToPlayerControl];

                foreach (var seer in Seers)
                {
                    if (!TryGetMessageRecipient(seer, out int seerclientid)) continue;
                    string playername = seer.GetRealName(isMeeting: true);
                    if (!GameStates.IsMeeting && Main.LastNotifyNames.TryGetValue((seer.PlayerId, seer.PlayerId), out var lastname))
                    {
                        playername = lastname;
                    }

                    var Nwriter = CustomRpcSender.Create("MessagesToSend", SendOption.None);
                    Nwriter.StartMessage(seerclientid);
                    Nwriter.StartRpc(seer.NetId, (byte)RpcCalls.SetName)
                    .Write(seer.Data.NetId)
                    .Write(title)
                    .EndRpc();
                    Nwriter.StartRpc(seer.NetId, (byte)RpcCalls.SendChat)
                    .Write(msg)
                    .EndRpc();
                    Nwriter.StartRpc(seer.NetId, (byte)RpcCalls.SetName)
                    .Write(seer.Data.NetId)
                    .Write(playername)
                    .EndRpc();
                    Nwriter.EndMessage();
                    Nwriter.SendMessage();

                }
                if (chatController is not null)
                    chatController.timeSinceLastMessage = sendTo is byte.MaxValue ? 0 : Main.MessageWait.Value - 0.2f;
            }
        }
public static void SendmessageInLobby(ChatController chatController)
        {
            PlayerControl senderplayer = PlayerCatch.AllAlivePlayerControls.Where(x => x.name is not "Player(Clone)").OrderBy(x => x.PlayerId).FirstOrDefault();
            if (senderplayer == null)
            {
                if (PlayerControl.LocalPlayer is not null)
                {
                    senderplayer = PlayerControl.LocalPlayer;
                }
                else
                { return; }
            }
            (string msg, byte sendTo, string title) = Main.MessagesToSend[0];
            var name = senderplayer.Data.GetLogPlayerName();
            var SendToPlayerControl = PlayerCatch.GetPlayerById(sendTo);
            int clientId = -1;
            if (sendTo != byte.MaxValue && !TryGetMessageRecipient(SendToPlayerControl, out clientId))
            {
                Main.MessagesToSend.RemoveAt(0);
                return;
            }

            if (title.RemoveHtmlTags() == title) // ホストが送信した場合、
            {
                senderplayer = PlayerControl.LocalPlayer;
                name = PlayerControl.LocalPlayer.Data.GetLogPlayerName();
                if (senderplayer == null) senderplayer = PlayerCatch.AllAlivePlayerControls.OrderBy(x => x.PlayerId).FirstOrDefault();
            }
            if (senderplayer == PlayerControl.LocalPlayer)
                _ = new LateTask(() =>
                { if (PlayerControl.LocalPlayer is not null) Utils.ApplySuffix(null, true); }, 0.24f, "", true);

            if (Utils.IsRestriction() && senderplayer.PlayerId != PlayerControl.LocalPlayer.PlayerId)
                return;

            Main.MessagesToSend.RemoveAt(0);
            if (Options.ExRpcWeightR.GetBool()) Main.MegCount++;

            // ホスト視点でのチャット送信
            if (clientId is -1)
            {
                senderplayer.SetName(title);
                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderplayer, msg);
                senderplayer.SetName(name);
            }
            var Nwriter = CustomRpcSender.Create("MessagesToSend", SendOption.None);
            Nwriter.StartMessage(clientId);
            Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SetName)
            .Write(senderplayer.Data.NetId)
            .Write(title)
            .EndRpc();
            if (ChatControllerUpdatePatch.IsQuickChatOnly)
            {
                Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SendQuickChat)
                .Write((byte)QuickChatPhraseType.SimplePhrase)
                .Write((uint)StringNames.QCGG)
                .EndRpc();
            }
            else
            {
                Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SendChat)
                .Write(msg)
                .EndRpc();
            }
            Nwriter.StartRpc(senderplayer.NetId, (byte)RpcCalls.SetName)
            .Write(senderplayer.Data.NetId)
            .Write(name)
            .EndRpc();
            Nwriter.EndMessage();
            Nwriter.SendMessage();
            if (title.RemoveHtmlTags() != title)
            {
                chatController.timeSinceLastMessage = sendTo is byte.MaxValue ? 0 : Main.MessageWait.Value - 0.2f;
            }
            if (Main.MessagesToSend.Count < 1)
                _ = new LateTask(() =>
                {
                    if (senderplayer.Data.PlayerName != name && !name.Contains("<"))
                    {
                        senderplayer.RpcSetName(name);
                    }
                }, 0.25f, "checkname", true);
        }
}
}
