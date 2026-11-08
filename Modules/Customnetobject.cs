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
    private static readonly Queue<Action> VisualSpawnQueue = new();
    private static bool visualCoolingDown;
    private static bool IsSpawning = false;
    private static uint spawnGeneration;
    private static bool CurrentSpawn(uint generation, AmongUsClient client, int game)
        => generation == spawnGeneration && client != null && client == AmongUsClient.Instance
            && client.AmHost && client.GameId == game && client.GameState == AmongUsClient.GameStates.Started;

    private bool spawnAssignmentsComplete;
    protected int Id;
    public PlayerControl PlayerControl;
    public Vector2 Position;

    protected virtual bool IsDynamic => false;
    protected virtual bool CanCreate => true;
    protected virtual bool DelayPlayerIdReset => false;
    // Opt-in for Nova's large visual pool. Other CNO consumers keep their existing cadence/envelopes.
    protected virtual bool BatchSpawnAssignments => false;
    protected virtual float SpawnInterval => 0.4f;

    public void Despawn()
    {
#if SHELL_DEBUG_CONTROLLER
        using var debugTraffic = TownOfHost.Development.ShellDebugTraffic.Begin(this, "remove");
#endif
        var client = AmongUsClient.Instance;
        if (client == null || !client.AmHost) return;
        var capturedPC = PlayerControl;
        try
        {
            if (capturedPC != null)
            {
                MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
                try
                {
                    writer.StartMessage(5);
                    writer.Write(client.GameId);
                    writer.StartMessage(5);
                    writer.WritePacked(capturedPC.NetId);
                    writer.EndMessage();
                    writer.EndMessage();
                    client.SendOrDisconnect(writer);
                }
                finally { writer.Recycle(); }
            }
        }
        catch (Exception e) { Logger.Error(e.ToString(), "CNO.Despawn"); }
        finally
        {
            // A failed send cannot leave a host-side dummy updating after its owner has retired it.
            try { if (capturedPC != null) client.RemoveNetObject(capturedPC); }
            catch (Exception e) { Logger.Error(e.GetType().Name, "CNO.Despawn.Local"); }
            try { if (capturedPC != null) UnityEngine.Object.Destroy(capturedPC.gameObject); }
            catch (Exception e) { Logger.Error(e.GetType().Name, "CNO.Despawn.Destroy"); }
            AllObjects.Remove(this);
        }
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

    // Nova's visual transaction never edits the borrowed host outfit or invokes local
    // Shapeshift on a dummy whose CachedPlayerData aliases the host. Vanilla still receives
    // the same temporary-info -> Shapeshift -> original-info sequence.
    protected void SetVisualName(string name)
    {
#if SHELL_DEBUG_CONTROLLER
        using var debugTraffic = TownOfHost.Development.ShellDebugTraffic.Name(this, name);
#endif
        var client = AmongUsClient.Instance;
        var host = global::PlayerControl.LocalPlayer;
        var dummy = PlayerControl;
        var data = host == null ? null : host.Data;
        if (client == null || !client.AmHost || data == null || dummy == null)
            throw new InvalidOperationException("Visual owner is unavailable");
        var outfits = data.Outfits;
        var original = outfits[PlayerOutfitType.Default];
        if (original == null) throw new InvalidOperationException("Default outfit is unavailable");
        // Native PlayerOutfit.Serialize/Deserialize layout verified against the current v19s binary:
        // name, packed color, hat/pet/skin/visor/nameplate, five sequence bytes.
        // Deserialize performs native managed-reference stores; no generated raw string field setters.
        var visual = new NetworkedPlayerInfo.PlayerOutfit();
        var buffer = MessageWriter.Get(SendOption.Reliable);
        try
        {
            buffer.Write(name ?? ""); buffer.WritePacked(0);
            for (int i = 0; i < 5; i++) buffer.Write("");
            for (int i = 0; i < 5; i++) buffer.Write((byte)0);
            var reader = MessageReader.Get(buffer.ToByteArray(false));
            try { visual.Deserialize(reader); }
            finally { reader.Recycle(); }
        }
        finally { buffer.Recycle(); }
        using var sender = CustomRpcSender.Create("CNO.VisualName", SendOption.Reliable);
        var writer = sender.stream;
        sender.StartMessage();
        try
        {
            outfits[PlayerOutfitType.Default] = visual;
            writer.StartMessage(1); writer.WritePacked(data.NetId); data.Serialize(writer, false); writer.EndMessage();
            sender.StartRpc(dummy.NetId, RpcCalls.Shapeshift).WriteNetObject(host).Write(false).EndRpc();
        }
        finally
        {
            outfits[PlayerOutfitType.Default] = original;
            GC.KeepAlive(original); GC.KeepAlive(visual);
        }
        writer.StartMessage(1); writer.WritePacked(data.NetId); data.Serialize(writer, false); writer.EndMessage();
        sender.EndMessage(); sender.SendMessage();
        GC.KeepAlive(original); GC.KeepAlive(visual);
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
#if SHELL_DEBUG_CONTROLLER
        using var debugTraffic = TownOfHost.Development.ShellDebugTraffic.Begin(this, "move");
#endif
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
        var queue = BatchSpawnAssignments ? VisualSpawnQueue : SpawnQueue;
        queue.Enqueue(() => { if (CurrentSpawn(generation, client, game) && CanCreate) DoCreate(position); else { IsSpawning = false; ProcessQueue(); } });
        ProcessQueue();
    }

    private static void ProcessQueue()
    {
        if (IsSpawning) return;
        // Mandatory/legacy effects are never held behind Nova's cosmetic pacing interval.
        var queue = SpawnQueue.Count > 0 ? SpawnQueue : !visualCoolingDown ? VisualSpawnQueue : null;
        if (queue == null || queue.Count == 0) return;
        IsSpawning = true;
        var action = queue.Dequeue();
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
        VisualSpawnQueue.Clear();
        visualCoolingDown = false;
        IsSpawning = false;
    }

    private void DoCreate(Vector2 position)
    {
#if SHELL_DEBUG_CONTROLLER
        using var debugTraffic = TownOfHost.Development.ShellDebugTraffic.Begin(this, "spawn");
#endif
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
#if SHELL_DEBUG_CONTROLLER
            using var debugTraffic = TownOfHost.Development.ShellDebugTraffic.Begin(capturedSelf, "assignment");
#endif
            var localHost = PlayerControl.LocalPlayer;
            if (localHost == null || localHost.Data == null) return;
            CustomRpcSender sender = null;
            int pending = 0;
            try
            {
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.Disconnected || pc.AmOwner) continue;

                sender ??= CustomRpcSender.Create("CNO.AssignId", SendOption.Reliable);
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
                // Complete GameDataTo envelopes retain each recipient and the original RPC ordering.
                // At most eight small assignment envelopes per reliable packet (well below 1000 bytes).
                if (++pending >= (capturedSelf.BatchSpawnAssignments ? 8 : 1))
                {
                    sender.SendMessage(); sender.Dispose(); sender = null; pending = 0;
                }
            }

            if (pending > 0) sender.SendMessage();
            }
            finally { sender?.Dispose(); }

            capturedPC.CachedPlayerData = localHost.Data;
            capturedSelf.spawnAssignmentsComplete = true;
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
                else if (capturedSelf.BatchSpawnAssignments && !capturedSelf.spawnAssignmentsComplete)
                { capturedSelf.OnCreateFailed(); capturedSelf.Despawn(); }
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
                float pause = Math.Max(0f, capturedSelf.SpawnInterval - 0.4f);
                if (capturedSelf.BatchSpawnAssignments && pause > 0f)
                {
                    visualCoolingDown = true;
                    _ = new LateTask(() =>
                    {
                        if (!CurrentSpawn(generation, spawnClient, spawnGame)) return;
                        visualCoolingDown = false;
                        ProcessQueue();
                    }, pause, "CNO.SpawnNext", true);
                }
                IsSpawning = false;
                ProcessQueue();
            }
        }, 0.4f, "CNO.OnCreated", true);
    }

    protected virtual void OnCreated() { }
    protected virtual void OnCreateFailed() { }

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

