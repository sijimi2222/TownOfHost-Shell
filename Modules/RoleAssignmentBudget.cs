using System;
using System.Collections.Generic;
using System.Linq;
using TownOfHost.Roles.Core;
namespace TownOfHost.Modules;

// The upstream guaranteed/capacity rule, separated from Unity for deterministic tests.
internal static class RoleAssignmentBudget
{
    internal static Dictionary<CustomRoleTypes,int> Others(int seats,
        IReadOnlyDictionary<CustomRoleTypes,int> minimum, IReadOnlyDictionary<CustomRoleTypes,int> maximum,
        IReadOnlyDictionary<CustomRoleTypes,int> capacity, IReadOnlyDictionary<CustomRoleTypes,int> guaranteed, Func<int,int> next)
    {
        var sure = new List<CustomRoleTypes>(); var ordinary = new List<CustomRoleTypes>();
        var extra = new List<CustomRoleTypes>(); var types = maximum.Keys.Where(t => t != CustomRoleTypes.Impostor).ToArray();
        seats = Math.Max(0,seats);
        foreach (var type in types)
        {
            int min = minimum.TryGetValue(type,out var lo) ? Math.Max(0,lo) : 0;
            int max = Math.Max(0,maximum[type]);
            int limit = Math.Min(Math.Max(min,max), capacity.TryGetValue(type,out var cap) ? Math.Max(0,cap) : 0);
            int certain = Math.Min(guaranteed.TryGetValue(type,out var count) ? Math.Max(0,count) : 0,limit);
            int floor = Math.Max(Math.Min(Math.Min(min,max),limit),certain);
            sure.AddRange(Enumerable.Repeat(type,certain));
            ordinary.AddRange(Enumerable.Repeat(type,floor-certain));
            extra.AddRange(Enumerable.Repeat(type,limit-floor));
        }
        while(sure.Count+ordinary.Count>seats && ordinary.Count>0) ordinary.RemoveAt(next(ordinary.Count));
        while(sure.Count>seats) sure.RemoveAt(next(sure.Count));
        ordinary.AddRange(sure);
        while(extra.Count>Math.Max(0,seats-ordinary.Count)) extra.RemoveAt(next(extra.Count));
        ordinary.AddRange(extra);
        return types.ToDictionary(t=>t,t=>ordinary.Count(x=>x==t));
    }
}
