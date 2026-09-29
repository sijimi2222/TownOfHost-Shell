using UnityEngine;

namespace TownOfHost;

public static partial class NaturalDisasters
{
    static bool IsUncounted(DisasterKind kind) => excludePersistent.GetBool()
        && (kind == DisasterKind.Sinkhole || kind == DisasterKind.BuildingCollapse);

    static int CountDisasterSlots()
    {
        int count = 0;
        foreach (var disaster in disasters)
            if (!disaster.Cancelled && !IsUncounted(disaster.Kind)) count++;
        return count;
    }
    static bool IsRoomReserved(PlainShipRoom room)
    {
        foreach (var disaster in disasters)
            if (disaster.UsesRoom(room)) return true;
        return false;
    }
    // Do not save/restore a safe position inside another reserved or collapsed room.
    static bool IsUnsafeCollapsePosition(Vector2 position)
    {
        foreach (var disaster in disasters)
            if (disaster.ContainsReservedRoom(position)) return true;
        return false;
    }
}
