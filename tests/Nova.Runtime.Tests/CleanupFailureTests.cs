using System;
using System.Linq;
using System.Collections.Generic;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Core;
using UnityEngine;

static partial class Program
{
    static void CleanupFailureTests()
    {
        foreach (string loss in new[] { "null", "disconnected", "ended", "logical-end", "not-host", "replacement" })
            foreach (string phase in new[] { "preparing", "charging", "beaming" })
                Test($"Nova session retirement loss={loss} phase={phase}", () =>
                {
                    var n = N();
                    if (phase == "preparing") { Tick(0, n); Tick(4.42f, n); }
                    else { Start(n); if (phase == "beaming") { Lock(n); Fire(n); } }
                    var owner = AmongUsClient.Instance;
                    if (loss == "null") AmongUsClient.Instance = null;
                    if (loss == "disconnected") owner.AmConnected = false;
                    if (loss == "ended") owner.GameState = AmongUsClient.GameStates.Ended;
                    if (loss == "logical-end") GameStates.IsEnded = true;
                    if (loss == "not-host") owner.AmHost = false;
                    if (loss == "replacement") AmongUsClient.Instance = new() { GameId = owner.GameId };
                    int sent = Sender.Packets.Count;
                    Tick(Time.time + .02f, n); Tick(Time.time + 60f, n);
                    Check(!Active(n) && CustomNetObject.AllObjects.Count == 0);
                    Eq(sent, Sender.Packets.Count);
                    n.OnDestroy(); n.OnDestroy(); Eq(sent, Sender.Packets.Count);
                });
        Test("Nova pool growth deadline retires the whole pool without retry", () =>
        {
            var n = N(); Warm(n);
            Set(n, "beaming", true); Set(n, "pendingBeamNames", new List<string> { "not published" });
            Set(n, "visualDeadline", Time.time);
            Tick(Time.time + .02f, n);
            Check(!Active(n) && Get<bool>(n, "poolPlanFailed"));
            Eq(0, CustomNetObject.AllObjects.Count); Eq(0, CustomRoleManager.Attempts);
            Tick(Time.time + 60f, n); Eq(0, CustomNetObject.AllObjects.Count);
            n.OnStartMeeting(); n.AfterMeetingTasks(); Warm(n); n.OnDestroy();
        });
        Test("Nova render commit timeout retires visible and queued objects", () =>
        {
            var n = N(); Start(n); Lock(n); Fire(n);
            var tile = Get<List<NovaBeamTile>>(n, "tiles")[1];
            Set(tile, "shownName", "not committed"); Set(n, "visualDeadline", Time.time);
            Tick(Time.time + .02f, n);
            Check(!Active(n) && Get<bool>(n, "poolPlanFailed"));
            Eq(0, CustomNetObject.AllObjects.Count);
            Tick(Time.time + 60f, n); Eq(0, CustomNetObject.AllObjects.Count); n.OnDestroy();
        });
        foreach (int people in Enumerable.Range(2, 14))
            Test($"Nova one failed owner cancels own queued pool only people={people}", () =>
            {
                ReferenceFont(); TownOfHost.Main.NormalOptions.MapId = 4;
                PlayerCatch.All.Clear();
                var roles = Enumerable.Range(0, people).Select(i => new Nova(P((byte)i, CustomRoles.Nova))).ToArray();
                PlayerControl.LocalPlayer = roles[0].Player; var n = roles[0]; var other = roles[1];
                Warm(roles);
                var own = Get<List<NovaBeamTile>>(n, "tiles").ToArray();
                var keep = Get<List<NovaBeamTile>>(other, "tiles").ToArray();
                typeof(NovaBeamTile).GetMethod("OnCreateFailed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(own[0], null);
                Tick(Time.time + .02f, roles); Tick(Time.time + 30f, roles);
                Check(own.All(t => !CustomNetObject.AllObjects.Contains(t)));
                Check(keep.All(t => CustomNetObject.AllObjects.Contains(t)) && Get<bool>(other, "TilesReady"));
                Check(!Active(n) && Get<bool>(n, "poolPlanFailed"));
                Check(!Press(n)); foreach (var role in roles) role.OnDestroy();
            });
    }
}
