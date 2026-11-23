using System;
using System.Linq;
using System.Collections.Generic;
using TownOfHost;
using TownOfHost.Modules;
using Hazel;

partial class Program
{
    static void CnoCleanupTests()
    {
        foreach (int people in Enumerable.Range(1, 15))
        {
            Test($"cleanup spawn failure rolls back allocated dummy players={people}", () =>
            {
                Peers(people - 1);
                var real = PlayerCatch.AllPlayerControls.ToArray();
                var d = new TrafficDummy { Pool = true };
                AmongUsClient.ThrowSend = true;
                d.Spawn();
                AmongUsClient.ThrowSend = false;
                Advance(3);
                Check(d.Failed == 1 && d.Created == 0);
                Check(d.PlayerControl != null && d.PlayerControl.gameObject.Destroyed);
                Check(!PlayerControl.AllPlayerControls.Contains(d.PlayerControl));
                Check(!CustomNetObject.AllObjects.Contains(d));
                Check(real.SequenceEqual(PlayerCatch.AllPlayerControls));
                Check(MessageWriter.Created.All(w => w.Recycles == 1));
                int sends = AmongUsClient.Sends;
                d.Despawn(); d.Despawn(); Advance(5);
                Check(AmongUsClient.Sends == sends && d.PlayerControl.gameObject.DestroyCalls == 1);
                var next = new TrafficDummy { Pool = true };
                next.Spawn(); Advance(2); Check(next.Created == 1); next.Despawn();
            });
            foreach (string loss in new[] { "null", "disconnected", "not-host", "ended", "not-joined", "replacement", "game-id" })
                Test($"cleanup retired session is local and idempotent players={people} loss={loss}", () =>
                {
                    Peers(people - 1);
                    var d = new TrafficDummy { Pool = true }; d.Spawn(); Advance(1);
                    var owner = AmongUsClient.Instance;
                    var pc = d.PlayerControl;
                    var real = new PlayerControl { PlayerId = 1 };
                    PlayerControl.AllPlayerControls.Add(real);
                    GameData.Instance = new();
                    var dummyInfo = new NetworkedPlayerInfo { PlayerId = 254, Object = pc };
                    var realInfo = new NetworkedPlayerInfo { PlayerId = 1, Object = real };
                    GameData.Instance.AllPlayers.Add(dummyInfo); GameData.Instance.AllPlayers.Add(realInfo);
                    if (loss == "null") AmongUsClient.Instance = null;
                    if (loss == "disconnected") owner.AmConnected = false;
                    if (loss == "not-host") owner.AmHost = false;
                    if (loss == "ended") owner.GameState = AmongUsClient.GameStates.Ended;
                    if (loss == "not-joined") owner.GameState = AmongUsClient.GameStates.NotJoined;
                    if (loss == "replacement") AmongUsClient.Instance = new() { GameState = AmongUsClient.GameStates.Started };
                    if (loss == "game-id") owner.GameId++;
                    int sends = AmongUsClient.Sends;
                    d.Despawn(); d.Despawn(); Advance(4);
                    Check(pc.gameObject.Destroyed && pc.gameObject.DestroyCalls == 1);
                    Check(!CustomNetObject.AllObjects.Contains(d) && !PlayerControl.AllPlayerControls.Contains(pc));
                    Check(AmongUsClient.Sends == sends);
                    Check(!real.gameObject.Destroyed && PlayerControl.AllPlayerControls.Contains(real));
                    Check(!GameData.Instance.AllPlayers.Contains(dummyInfo) && GameData.Instance.AllPlayers.Contains(realInfo));
                    Check(owner.Removed.Count == 1);
                    if (loss == "replacement") Check(AmongUsClient.Instance.Removed.Count == 0);
                    GameData.Instance = new();
                });
        }
        Test("cleanup disconnect entry invalidates cosmetics before other teardown", () =>
        {
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(SourcePath()), "../../Patches/PlayerJoinAndLeftPatch.cs"));
            var source = System.IO.File.ReadAllText(path);
            int first = source.IndexOf("class DisconnectInternalPatch", StringComparison.Ordinal);
            int reset = source.IndexOf("if (__instance == AmongUsClient.Instance) CustomNetObject.Reset();", first, StringComparison.Ordinal);
            int next = source.IndexOf("StreamerInfo.DisconnectInternal();", first, StringComparison.Ordinal);
            Check(reset > first && reset < next);
        });
        Test("cleanup queued cancellation cannot resurrect object", () =>
        {
            var a = new TrafficDummy { Pool = true }; a.Spawn();
            var b = new TrafficDummy { Pool = true }; b.Spawn(); b.Despawn();
            Advance(5); Check(b.PlayerControl == null && b.Created == 0); a.Despawn();
        });
        Test("cleanup pending callback after disconnect reclaims and does not transmit", () =>
        {
            var d = new TrafficDummy { Pool = true }; d.Spawn();
            var pc = d.PlayerControl; int sends = AmongUsClient.Sends;
            AmongUsClient.Instance.AmConnected = false; Advance(3);
            Check(d.Created == 0 && pc.gameObject.Destroyed && AmongUsClient.Sends == sends);
            Check(!CustomNetObject.AllObjects.Contains(d));
        });
        Test("cleanup reset cancels pending generation and delayed callbacks", () =>
        {
            var d = new TrafficDummy { Pool = true }; d.Spawn(); var pc = d.PlayerControl;
            var b = new TrafficDummy { Pool = true }; b.Spawn();
            CustomNetObject.Reset(); int sends = AmongUsClient.Sends;
            Advance(5); Check(pc.gameObject.Destroyed && b.PlayerControl == null);
            Check(d.Created == 0 && b.Created == 0 && AmongUsClient.Sends == sends);
        });
        Test("cleanup unregister exception still destroys and retires once", () =>
        {
            var d = new TrafficDummy { Pool = true }; d.Spawn(); Advance(2);
            var pc = d.PlayerControl; AmongUsClient.Instance.ThrowRemove = true;
            d.Despawn(); d.Despawn(); Check(pc.gameObject.DestroyCalls == 1);
            Check(!CustomNetObject.AllObjects.Contains(d) && AmongUsClient.Instance.Removed.Count == 1);
        });
        Test("cleanup appearance completion exception retires partial object", () =>
        {
            // Local appearance commit can fail after the normal spawn; failure must retire it.
            var d = new TrafficDummy { Pool = true }; d.Spawn();
            MessageWriter.ThrowSerialize = true; Advance(2); MessageWriter.ThrowSerialize = false;
            Check(d.Failed == 1 && d.PlayerControl.gameObject.Destroyed && !CustomNetObject.AllObjects.Contains(d));
        });
        Test("cleanup reentrant removal during initial send cannot register again", () =>
        {
            var d = new TrafficDummy { Pool = true };
            AmongUsClient.BeforeSend = () => { AmongUsClient.BeforeSend = null; d.Despawn(); };
            d.Spawn(); Advance(3);
            Check(d.Created == 0 && d.PlayerControl.gameObject.DestroyCalls == 1);
            Check(!CustomNetObject.AllObjects.Contains(d));
        });
        Test("cleanup recipient batching stops at transport loss", () =>
        {
            Peers(14); var d = new TrafficDummy { Pool = true }; d.Spawn();
            AmongUsClient.BeforeSend = () => AmongUsClient.Instance.AmConnected = false;
            int sends = AmongUsClient.Sends; Advance(2);
            Check(AmongUsClient.Sends == sends + 1 && d.Created == 0);
            Check(d.PlayerControl.gameObject.Destroyed && !CustomNetObject.AllObjects.Contains(d));
        });
        Test("cleanup transport loss during initial send cannot commit registration", () =>
        {
            AmongUsClient.BeforeSend = () => AmongUsClient.Instance.AmConnected = false;
            var d = new TrafficDummy { Pool = true }; d.Spawn(); Advance(2);
            Check(AmongUsClient.Sends == 1 && d.Created == 0);
            Check(d.PlayerControl.gameObject.Destroyed && !CustomNetObject.AllObjects.Contains(d));
        });
        Test("cleanup direct name and position updates reject ended connection", () =>
        {
            var d = new TrafficDummy { Pool = true }; d.Spawn(); Advance(2);
            AmongUsClient.Instance.GameState = AmongUsClient.GameStates.Ended;
            int sends = AmongUsClient.Sends;
            try { d.Visual("must not send"); } catch (InvalidOperationException) { }
            d.Move(new(1, 1)); d.Despawn(); Check(AmongUsClient.Sends == sends);
        });
        foreach (int people in Enumerable.Range(1, 15))
            Test($"cleanup multi owner shutdown bounds queued work players={people}", () =>
            {
                clock = 0; Peers(people - 1);
                var ds = Enumerable.Range(0, people * 4).Select(_ => new TrafficDummy { Pool = true }).ToArray();
                foreach (var d in ds) d.Spawn(); Advance(.15f);
                AmongUsClient.Instance.GameState = AmongUsClient.GameStates.Ended;
                int sends = AmongUsClient.Sends;
                CustomNetObject.FixedUpdate();
                foreach (var d in ds) d.Despawn();
                Advance(people * 5 + 10);
                Check(AmongUsClient.Sends == sends && CustomNetObject.AllObjects.Count == 0);
                Check(ds.All(d => d.PlayerControl == null || d.PlayerControl.gameObject.Destroyed));
                Check(ds.All(d => d.Created == 0));
            });
    }
}
