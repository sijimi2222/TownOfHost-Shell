using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;

static partial class Program
{
    private static void IdleBench()
    {
        var canCreate = typeof(TownOfHost.Roles.Impostor.NovaBeamTile)
            .GetProperty("CanCreate", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetGetMethod(true).CreateDelegate<Func<TownOfHost.Roles.Impostor.NovaBeamTile, bool>>();
        var rows = new List<object>();
        foreach (int owners in new[] { 1, 6, 15 })
        foreach (float width in new[] { 0.25f, 1.25f })
        {
            Reset();
            TownOfHost.Main.NormalOptions.MapId = 4;
            TownOfHost.OptionItem.All[992912].Value = width;
            var roles = Enumerable.Range(1, owners).Select(id => N((byte)id)).ToArray();
            Warm(roles);
            var tiles = roles.SelectMany(n => Get<List<TownOfHost.Roles.Impostor.NovaBeamTile>>(n, "tiles")).ToArray();
            void Run(int iterations)
            {
                for (int i = 0; i < iterations; i++)
                {
                    UnityEngine.Time.time += 0.02f;
                    foreach (var role in roles) role.OnFixedUpdate(role.Player);
                    foreach (var tile in tiles) Check(canCreate(tile), "idle visual ownership changed");
                }
            }
            Run(1000);
            int packets = Sender.Packets.Count;
            int names = CustomNetObject.NameEvents.Count;
            var timer = new Stopwatch();
            long before = GC.GetAllocatedBytesForCurrentThread();
            timer.Start();
            Run(10000);
            timer.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(Sender.Packets.Count == packets && CustomNetObject.NameEvents.Count == names
                && CustomRoleManager.Attempts == 0, "idle tick produced traffic or damage");
            rows.Add(new { owners, width, tiles = tiles.Length, ticks = 10000, allocated, milliseconds = timer.Elapsed.TotalMilliseconds });
            foreach (var role in roles) role.OnDestroy();
        }
        Console.WriteLine(JsonSerializer.Serialize(rows));
    }
}
