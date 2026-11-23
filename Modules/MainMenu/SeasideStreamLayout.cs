using System.Collections.Generic;

namespace TownOfHost.Modules.MainMenu;

internal static class SeasideStreamLayout
{
    internal static readonly SeasideRect Panel = new(155, 285, 1360, 490);
    internal static readonly SeasideRect Title = new(500, 170, 685, 90);
    internal static readonly SeasideRect Logo = new(0, 0, 490, 275);
    internal static readonly IReadOnlyDictionary<string, SeasideRect> Controls = new Dictionary<string, SeasideRect>
    {
        ["UrlTitle"] = new(200, 308, 850, 42),
        ["Url"] = new(200, 358, 900, 50),
        ["Paste"] = new(1140, 350, 310, 60),
        ["JoinTitle"] = new(200, 433, 580, 38),
        ["Join"] = new(200, 481, 580, 55),
        ["RemoveTitle"] = new(200, 557, 580, 38),
        ["Remove"] = new(200, 605, 580, 55),
        ["Auth"] = new(875, 466, 575, 64),
        ["ObsRoles"] = new(875, 591, 575, 64),
        ["Comments"] = new(200, 697, 340, 56),
        ["Reset"] = new(570, 697, 440, 56),
        ["Obs"] = new(1040, 697, 410, 56),
        ["Notice"] = new(175, 785, 1320, 43),
        ["Terms"] = new(200, 849, 340, 49),
        ["Privacy"] = new(570, 849, 440, 49),
        ["Back"] = new(1100, 842, 350, 64),
    };
    internal static SeasideRect Rect(string id) => Controls[id];
}
