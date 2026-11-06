using System;
using System.Collections.Generic;

namespace TownOfHost.Modules.MainMenu;

// The reference art uses the same logical canvas as the Shell home. Native controls
// remain independent of these rectangles and of all optional PNGs.
internal static class SeasideCodeEntryLayout
{
    internal static readonly SeasideRect Title=new(560,165,735,100);
    internal static readonly SeasideRect Panel=new(390,285,1170,560);
    internal static readonly IReadOnlyDictionary<string,SeasideRect> Controls=new Dictionary<string,SeasideRect>
    {
        ["Back"]=new(420,315,195,60),["Code"]=new(700,340,600,90),
        ["Host"]=new(850,460,410,52),["Capacity"]=new(850,525,410,52),
        ["Region"]=new(850,590,410,52),["Chat"]=new(850,655,410,52),
        ["Server"]=new(1305,590,225,52),["ServerDropdown"]=new(1110,350,410,330),
        ["ServerOnly"]=new(450,755,38,38),["Join"]=new(835,740,440,78),
        ["Unload"]=new(1330,700,205,35),["Download"]=new(1330,742,205,35),
        ["Restore"]=new(1330,784,205,35),["Warning"]=new(680,850,830,50),
        ["Loading"]=new(1310,350,48,48),["Checkmark"]=new(1325,400,30,30),
        ["Illustration"]=new(35,445,325,380)
    };
    internal static SeasideRect Rect(string id)=>Controls[id];
    internal static readonly string[] ArtworkNames={"panel","illustration","input-frame","button","button-back","button-join","button-restore","button-download","button-unload"};
    internal static bool ValidImageDimensions(uint width,uint height)=>width>0&&height>0&&width<=4096&&height<=4096;
}
