using Hazel;
namespace TownOfHost { public static class ColorAdapter {
public static void RpcChColor(this PlayerControl pc, PlayerControl seer, byte Color, bool NonSkin = false)

        {

            if (Options.firstturnmeeting && MeetingStates.FirstMeeting) return;

            if (GameStates.IsLobby) return;



            if (seer.PlayerId == PlayerControl.LocalPlayer.PlayerId)

            {
                pc.SetColor(Color);
                // ホスト自身の視点なら、自分の画面だけ変えて終わり。
                // (以前は return が下のコメントアウトの中にあったため、自分宛てのRPCが送られて全員の色が変わっていた)
                return;
                /*if (NonSkin)
                {

                    pc.SetSkin("", Color);

                    pc.SetHat("", Color);

                    pc.SetVisor("", Color);

                    return;

                }*/
            }
            if (seer.GetClientId() == -1) return; // 宛先が分からない時は送らない(全員に届いてしまうため)
            var sender = CustomRpcSender.Create("DChengeColor", SendOption.Reliable);
            sender.StartMessage(seer.GetClientId());



            sender.StartRpc(pc.NetId, RpcCalls.SetColor)

            .Write(pc.NetId)

            .Write(Color)

            .EndRpc();



            /*if (NonSkin)

            {

                sender.StartRpc(pc.NetId, RpcCalls.SetHatStr)

                .Write("")

                .Write(pc.GetNextRpcSequenceId(RpcCalls.SetHatStr))

                .EndRpc();

                sender.StartRpc(pc.NetId, RpcCalls.SetSkinStr)

                .Write("")

                .Write(pc.GetNextRpcSequenceId(RpcCalls.SetSkinStr))

                .EndRpc();

                sender.StartRpc(pc.NetId, RpcCalls.SetVisorStr)

                .Write("")

                .Write(pc.GetNextRpcSequenceId(RpcCalls.SetVisorStr))

                .EndRpc();

            }*/

            sender.EndMessage();

            sender.SendMessage();

        }
public static void RpcHideSkinAndPet(this PlayerControl pc, PlayerControl seer)

        {

            if (Options.firstturnmeeting && MeetingStates.FirstMeeting) return;

            if (GameStates.IsLobby) return;



            if (seer.PlayerId == PlayerControl.LocalPlayer.PlayerId)

            {

                pc.SetSkin("", pc.CurrentOutfit.ColorId);

                pc.SetHat("", pc.CurrentOutfit.ColorId);

                pc.SetVisor("", pc.CurrentOutfit.ColorId);

                pc.SetPet("", pc.CurrentOutfit.ColorId);

                return;
            }
            if (seer.GetClientId() == -1) return; // 宛先が分からない時は送らない(全員に届いてしまうため)
            var sender = CustomRpcSender.Create("DHideSkinAndPet", SendOption.Reliable);
            sender.StartMessage(seer.GetClientId());



            sender.StartRpc(pc.NetId, RpcCalls.SetHatStr)

            .Write("")

            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetHatStr))

            .EndRpc();

            sender.StartRpc(pc.NetId, RpcCalls.SetSkinStr)

            .Write("")

            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetSkinStr))

            .EndRpc();

            sender.StartRpc(pc.NetId, RpcCalls.SetVisorStr)

            .Write("")

            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetVisorStr))

            .EndRpc();

            sender.StartRpc(pc.NetId, RpcCalls.SetPetStr)

            .Write("")

            .Write(pc.GetNextRpcSequenceId(RpcCalls.SetPetStr))

            .EndRpc();



            sender.EndMessage();

            sender.SendMessage();

        }
} }