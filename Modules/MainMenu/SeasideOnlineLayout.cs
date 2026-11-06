using System;
namespace TownOfHost.Modules.MainMenu;

internal static class SeasideOnlineLayout
{
    internal static readonly string[] Ids = { "OnlineCreate", "OnlineCode", "OnlineBack" };
    internal static readonly SeasideRect Title = new(500, 154, 674, 84);
    internal static SeasideRect Rect(string id) => id switch {
        "OnlineCreate" => SeasidePlayLayout.Local, "OnlineCode" => SeasidePlayLayout.Online,
        "OnlineBack" => SeasidePlayLayout.Back, _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    internal static SeasideRect ImageBox(string id) {
        var r=Rect(id); if(id=="OnlineBack") throw new ArgumentOutOfRangeException(nameof(id));
        return new(r.X+18,r.Y+18,r.Width-36,r.Height-118);
    }
    internal static SeasideRect Label(string id) {
        var r=Rect(id);return id=="OnlineBack"?r:new(r.X+54,r.Y+r.Height-84,r.Width-108,78);
    }
    // Cover crop is visual-only. The complete fixed card remains the click target.
    internal static SeasideRect Crop(string id,double width,double height,double zoom=1,double offsetX=0,double offsetY=0) {
        if(!double.IsFinite(width)||!double.IsFinite(height)||width<=0||height<=0)throw new ArgumentOutOfRangeException(nameof(width));
        var box=ImageBox(id);zoom=double.IsFinite(zoom)?Math.Clamp(zoom,1,3):1;
        offsetX=double.IsFinite(offsetX)?Math.Clamp(offsetX,-1,1):0;offsetY=double.IsFinite(offsetY)?Math.Clamp(offsetY,-1,1):0;
        double scale=Math.Max(box.Width/width,box.Height/height)*zoom,w=Math.Min(width,box.Width/scale),h=Math.Min(height,box.Height/scale);
        return new((width-w)*(offsetX+1)/2,(height-h)*(offsetY+1)/2,w,h);
    }
}
