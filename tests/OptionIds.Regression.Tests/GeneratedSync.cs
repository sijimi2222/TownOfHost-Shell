using System;using Hazel;namespace TownOfHost{public static partial class RPC{
public static void SyncCustomSettingsRPC()

        {

            if (!AmongUsClient.Instance.AmHost) return;

            if (!PlayerCatch.AnyModClient()) return;



            int count = 0;

            MessageWriter writer = null;



            foreach (OptionItem co in OptionItem.AllOptions)

            {

                if (co is AssignOptionItem assignOptionItem)

                {

                    assignOptionItem.SendRpc(true);

                }

                if (count == 0 || count % 500 == 0)

                {

                    if (writer != null) AmongUsClient.Instance.FinishRpcImmediately(writer);

                    writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncCustomSettings, SendOption.Reliable, -1);

                    writer.WritePacked(-1);

                    writer.WritePacked(count);

                    writer.WritePacked(Math.Min(OptionItem.AllOptions.Count, count + 500));

                }

                writer.WritePacked(co.GetValue());

                count++;

            }

            AmongUsClient.Instance.FinishRpcImmediately(writer);

        }
public static void SyncCustomSettingsRPC(OptionItem item)

        {

            if (!AmongUsClient.Instance.AmHost || !PlayerCatch.AnyModClient()) return;

            if (item is AssignOptionItem) return;



            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncCustomSettings, SendOption.Reliable, -1);

            writer.WritePacked(item.Id);

            writer.WritePacked(item.GetValue());

            AmongUsClient.Instance.FinishRpcImmediately(writer);

        }
public static void Receive(Hazel.MessageReader reader){

                    int optionId = reader.ReadPackedInt32();

                    if (optionId >= 0 && OptionItem.FastOptions.TryGetValue(optionId, out var optionSync))

                    {

                        optionSync.SetValue(reader.ReadPackedInt32());

                        return;

                    }

                    int indexId = reader.ReadPackedInt32();

                    int maxId = reader.ReadPackedInt32();

                    for (var i = indexId; i < maxId; i++)

                        OptionItem.AllOptions[i].SetValue(reader.ReadPackedInt32());

                    return;

                }
}}