using System.Collections.Generic;
using System.Linq;

namespace TownOfHost.Modules;

// A Hunter owns only its own contribution. The ordinary blackout flag remains
// independent, so another Hunter/Stealth/Alien/kill-flash cannot be cleared here.
internal sealed class HunterVisionLeases
{
    readonly Dictionary<object, HashSet<byte>> owners = new();
    readonly Dictionary<byte, int> counts = new();
    internal bool Contains(byte player) => counts.TryGetValue(player, out int n) && n > 0;
    internal byte[] Replace(object owner, IEnumerable<byte> players)
    {
        var next = new HashSet<byte>(players);
        var previous = owners.TryGetValue(owner, out var old) ? old : new HashSet<byte>();
        var touched = previous.Union(next).ToArray();
        foreach (byte id in previous.Except(next)) Remove(id);
        foreach (byte id in next.Except(previous)) counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
        if (next.Count == 0) owners.Remove(owner); else owners[owner] = next;
        return touched;
    }
    internal byte[] Release(object owner) => Replace(owner, System.Array.Empty<byte>());
    void Remove(byte id)
    {
        if (!counts.TryGetValue(id, out int n)) return;
        if (n <= 1) counts.Remove(id); else counts[id] = n - 1;
    }
    internal void Clear() { owners.Clear(); counts.Clear(); }
}
