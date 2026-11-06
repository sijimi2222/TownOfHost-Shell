namespace TownOfHost.Modules;

// hamo 4.40.02's nine undirected Mira HQ edges. No new IDs or packets.
internal static class MiraVentRules
{
    internal static bool IsBlocked(int from, int to)
    {
        if (from < 0 || to < 0 || from > 255 || to > 255 || from == to) return false;
        if (from > to) (from, to) = (to, from);
        return (from, to) is (6, 7) or (5, 6) or (4, 5) or (3, 4) or (3, 9)
            or (9, 10) or (10, 11) or (8, 10) or (1, 2);
    }
}
