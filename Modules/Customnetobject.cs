using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using InnerNet;
using UnityEngine;

namespace TownOfHost.Modules;

public class CustomNetObject
{
    public static readonly List<CustomNetObject> AllObjects = new();
    private static int MaxId = -1;

    private static readonly Queue<Action> SpawnQueue = new();
    private static bool IsSpawning = false;
    private static uint spawnGeneration;
    private static bool CurrentSpawn(uint generation, AmongUsClient client, int game)
        => generation == spawnGeneration && client != null && client == AmongUsClient.Instance
            && client.AmHost && client.GameId == game && client.GameState == AmongUsClient.GameStates.Started;

    protected int Id;
    public PlayerControl PlayerControl;
    public Vector2 Position;

    protected virtual bool IsDynamic => false;
    protected virtual bool CanCreate => true;
    protected virtual bool DelayPlayerIdReset => false;

    public void Despawn()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

        try
        {
            if (PlayerControl != null)
            {
                MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
                try
                {
                writer.StartMessage(5);
                writer.Write(AmongUsClient.Instance.GameId);
                writer.StartMessage(5);
                writer.WritePacked(PlayerControl.NetId);
                writer.EndMessage();
                writer.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(writer);
                }
                finally { writer.Recycle(); }

                AmongUsClient.Instance.RemoveNetObject(PlayerControl);
                UnityEngine.Object.Destroy(PlayerControl.gameObject);
            }
            AllObjects.Remove(this);
        }
        catch (Exception e) { Logger.Error(e.ToString(), "CNO.Despawn"); }
    }

    protected void Hide(PlayerControl player)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;

        if (player.AmOwner)
        {
            var hiddenPlayer = PlayerControl;
            var client = AmongUsClient.Instance;
            int game = client.GameId;
            uint generation = spawnGeneration;
            _ = new LateTask(() =>
            {
                if (!CurrentSpawn(generation, client, game) || !hiddenPlayer || PlayerControl != hiddenPlayer) return;
                try { hiddenPlayer.transform.FindChild("Names").FindChild("NameText_TMP").gameObject.SetActive(false); }
                catch { }
                hiddenPlayer.Visible = false;
            }, 0.1f, "CNO.Hide.Local", true);
            return;
        }

        MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
        try
        {
        writer.StartMessage(6);
        writer.Write(AmongUsClient.Instance.GameId);
        writer.WritePacked(player.OwnerId);
        writer.StartMessage(5);
        writer.WritePacked(PlayerControl.NetId);
        writer.EndMessage();
        writer.EndMessage();
        AmongUsClient.Instance.SendOrDisconnect(writer);
        }
        finally { writer.Recycle(); }
    }

    protected virtual void OnFixedUpdate()
    {
        if (!IsDynamic) return;
        try
        {
            if (!AmongUsClient.Instance.AmHost) return;
            ushort num = (ushort)(PlayerControl.NetTransform.lastSequenceId + 2U);
            MessageWriter mw = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.NetTransform.NetId, 21, SendOption.None);
            NetHelpers.WriteVector2(Position, mw);
            mw.Write(num);
            AmongUsClient.Instance.FinishRpcImmediately(mw);
        }
        catch { }
    }

    protected void SetAppearance(int colorId, string skinId = "", string hatId = "", string petId = "", string visorId = "", string displayName = null)
    {
        if (PlayerControl == null) return;

        var capturedPC = PlayerControl;
        var outfit = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default];
        string origName = outfit.PlayerName;
        int origColor = outfit.ColorId;
        string origHat = outfit.HatId;
        string origSkin = outfit.SkinId;
        string origPet = outfit.PetId;
        string origVisor = outfit.VisorId;

        using var sender = CustomRpcSender.Create("CNO.SetAppearance", SendOption.Reliable);
        MessageWriter writer = sender.stream;
        sender.StartMessage();

        try
        {
            outfit.PlayerName = displayName ?? origName;
            outfit.ColorId = colorId;
            outfit.HatId = hatId ?? "";
            outfit.SkinId = skinId ?? "";
            outfit.PetId = petId ?? "";
            outfit.VisorId = visorId ?? "";

        writer.StartMessage(1);
        writer.WritePacked(PlayerControl.LocalPlayer.Data.NetId);
        PlayerControl.LocalPlayer.Data.Serialize(writer, false);
        writer.EndMessage();

        try { capturedPC.Shapeshift(PlayerControl.LocalPlayer, false); }
        catch (Exception e) { Logger.Error(e.ToString(), "CNO.SetAppearance.Shapeshift"); }

        sender.StartRpc(capturedPC.NetId, RpcCalls.Shapeshift)
            .WriteNetObject(PlayerControl.LocalPlayer)
            .Write(false)
            .EndRpc();
        }
        finally
        {
            outfit.PlayerName = origName;
            outfit.ColorId = origColor;
            outfit.HatId = origHat;
            outfit.SkinId = origSkin;
            outfit.PetId = origPet;
            outfit.VisorId = origVisor;
        }

        writer.StartMessage(1);
        writer.WritePacked(PlayerControl.LocalPlayer.Data.NetId);
        PlayerControl.LocalPlayer.Data.Serialize(writer, false);
        writer.EndMessage();

        sender.EndMessage();
        sender.SendMessage();
    }

    protected void SetName(string name)
    {
        if (PlayerControl == null) return;

        if (PlayerControl.cosmetics?.nameText != null)
            PlayerControl.cosmetics.nameText.text = name;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
            PlayerControl.NetId, (byte)RpcCalls.SetName, SendOption.Reliable);
        writer.Write(PlayerControl.Data.NetId);
        writer.Write(name);
        writer.Write(false);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    protected void SnapToPosition(Vector2 position)
    {
        if (PlayerControl == null) return;
        Position = position;

        try { PlayerControl.NetTransform.SnapTo(position); }
        catch { }

        ushort sid = (ushort)(PlayerControl.NetTransform.lastSequenceId + 100U);
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
            PlayerControl.NetTransform.NetId, (byte)RpcCalls.SnapTo, SendOption.Reliable);
        NetHelpers.WriteVector2(position, writer);
        writer.Write(sid);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    protected void CreateNetObject(Vector2 position)
    {
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost || client.GameState != AmongUsClient.GameStates.Started) return;

        uint generation = spawnGeneration;
        int game = client.GameId;
        SpawnQueue.Enqueue(() => { if (CurrentSpawn(generation, client, game) && CanCreate) DoCreate(position); else { IsSpawning = false; ProcessQueue(); } });
        ProcessQueue();
    }

    private static void ProcessQueue()
    {
        if (IsSpawning || SpawnQueue.Count == 0) return;
        IsSpawning = true;
        var action = SpawnQueue.Dequeue();
        try
        {
            action();
        }
        catch (Exception e)
        {
            Logger.Error(e.ToString(), "CNO.Spawn");
            IsSpawning = false;
            ProcessQueue();
        }
    }

    public static void ResetSpawnState()
    {
        unchecked { spawnGeneration++; }
        SpawnQueue.Clear();
        IsSpawning = false;
    }

    private void DoCreate(Vector2 position)
    {
        var spawnClient = AmongUsClient.Instance;
        int spawnGame = spawnClient.GameId;
        uint generation = spawnGeneration;
        PlayerControl = UnityEngine.Object.Instantiate(
            AmongUsClient.Instance.PlayerPrefab, Vector2.zero, Quaternion.identity);
        PlayerControl.PlayerId = 254;
        PlayerControl.isNew = false;
        PlayerControl.notRealPlayer = true;

        try { PlayerControl.NetTransform.SnapTo(new Vector2(50f, 50f)); }
        catch { }

        AmongUsClient.Instance.NetIdCnt += 1U;

        MessageWriter msg = MessageWriter.Get(SendOption.Reliable);
        try
        {
        msg.StartMessage(5);
        msg.Write(AmongUsClient.Instance.GameId);
        msg.StartMessage(4);
        SpawnGameDataMessage item = AmongUsClient.Instance.CreateSpawnMessage(PlayerControl, -2, SpawnFlags.None);
        item.SerializeValues(msg);
        msg.EndMessage();

        for (uint i = 1; i <= 3; ++i)
        {
            msg.StartMessage(4);
            msg.WritePacked(2U);
            msg.WritePacked(-2);
            msg.Write((byte)SpawnFlags.None);
            msg.WritePacked(1);
            msg.WritePacked(AmongUsClient.Instance.NetIdCnt - i);
            msg.StartMessage(1);
            msg.EndMessage();
            msg.EndMessage();
        }

        msg.EndMessage();
        AmongUsClient.Instance.SendOrDisconnect(msg);
        }
        finally { msg.Recycle(); }

        if (PlayerControl.AllPlayerControls.Contains(PlayerControl))
            PlayerControl.AllPlayerControls.Remove(PlayerControl);

        PlayerControl.cosmetics.colorBlindText.color = Color.clear;

        Position = position;
        ++MaxId;
        Id = MaxId;
        if (MaxId == int.MaxValue) MaxId = -1;

        AllObjects.Add(this);

        var capturedPC = PlayerControl;
        var capturedSelf = this;

        _ = new LateTask(() =>
        {
            if (!CurrentSpawn(generation, spawnClient, spawnGame) || !capturedPC || !capturedSelf.CanCreate) return;
            var localHost = PlayerControl.LocalPlayer;
            if (localHost == null || localHost.Data == null) return;
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.Disconnected || pc.AmOwner) continue;

                var sender = CustomRpcSender.Create("CNO.AssignId", SendOption.Reliable);
                MessageWriter writer = sender.stream;
                sender.StartMessage(pc.OwnerId);

                writer.StartMessage(1);
                writer.WritePacked(capturedPC.NetId);
                writer.Write(pc.PlayerId);
                writer.EndMessage();

                sender.StartRpc(capturedPC.NetId, RpcCalls.MurderPlayer)
                    .WriteNetObject(capturedPC)
                    .Write((int)MurderResultFlags.FailedError)
                    .EndRpc();

                if (!capturedSelf.DelayPlayerIdReset)
                {
                    writer.StartMessage(1);
                    writer.WritePacked(capturedPC.NetId);
                    writer.Write((byte)254);
                    writer.EndMessage();
                }

                sender.EndMessage();
                sender.SendMessage();
            }

            capturedPC.CachedPlayerData = localHost.Data;
        }, 0.1f, "CNO.AssignId", true);

        if (DelayPlayerIdReset)
        {
            _ = new LateTask(() =>
            {
                if (!CurrentSpawn(generation, spawnClient, spawnGame) || !capturedPC || !AllObjects.Contains(capturedSelf) || !capturedSelf.CanCreate) return;
                foreach (var pc in PlayerCatch.AllPlayerControls)
                {
                    if (pc == null || pc.AmOwner || pc.Data == null || pc.Data.Disconnected) continue;
                    var sender = CustomRpcSender.Create("CNO.Text.ResetId", SendOption.Reliable);
                    sender.StartMessage(pc.OwnerId);
                    sender.stream.StartMessage(1);
                    sender.stream.WritePacked(capturedPC.NetId);
                    sender.stream.Write((byte)254);
                    sender.stream.EndMessage();
                    sender.EndMessage(); sender.SendMessage();
                }
            }, 1.1f, "CNO.Text.ResetId", true);
        }

        _ = new LateTask(() =>
        {
            // A retired session must never release another session's in-flight spawn.
            if (!CurrentSpawn(generation, spawnClient, spawnGame)) return;
            try
            {
                if (!capturedPC || !capturedSelf.CanCreate) capturedSelf.Despawn();
                else capturedSelf.OnCreated();
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString(), "CNO.OnCreated");
            }

            // ★ GameData.AllPlayers からも除去（会議でのUI崩れを防ぐ）
            try
            {
                var allPlayers = GameData.Instance?.AllPlayers;
                if (allPlayers != null && capturedPC != null)
                {
                    for (int i = allPlayers.Count - 1; i >= 0; i--)
                    {
                        var info = allPlayers[i];
                        if (info != null && info.PlayerId == 254
                            && info.Object?.NetId == capturedPC.NetId)
                        {
                            allPlayers.RemoveAt(i);
                            Logger.Info($"CNO: GameData.AllPlayersからダミーを除去 NetId={capturedPC.NetId}", "CustomNetObject");
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"CNO.RemoveFromGameData: {e}", "CustomNetObject");
            }

            if (CurrentSpawn(generation, spawnClient, spawnGame))
            {
                IsSpawning = false;
                ProcessQueue();
            }
        }, 0.4f, "CNO.OnCreated", true);
    }

    protected virtual void OnCreated() { }

    public virtual void OnMeeting()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        Despawn();
    }

    public static void FixedUpdate()
    {
        foreach (var cno in AllObjects.ToArray())
            cno?.OnFixedUpdate();
    }

    public static CustomNetObject Get(int id)
        => AllObjects.FirstOrDefault(x => x.Id == id);

    public static CustomNetObject GetKillableTarget(PlayerControl killer, float range = 1.5f)
    {
        if (killer == null) return null;
        var pos = killer.GetTruePosition();
        return AllObjects
            .Where(o => o is IKillableDummy)
            .OrderBy(o => Vector2.Distance(pos, o.Position))
            .FirstOrDefault(o => Vector2.Distance(pos, o.Position) <= range);
    }

    public static void Reset()
    {
        try
        {
            ResetSpawnState();
            foreach (var obj in AllObjects.ToArray())
                obj?.Despawn();
            AllObjects.Clear();
        }
        catch (Exception e) { Logger.Error(e.ToString(), "CNO.Reset"); }
    }
}

public interface IKillableDummy
{
    void OnKilled(PlayerControl killer);
}

