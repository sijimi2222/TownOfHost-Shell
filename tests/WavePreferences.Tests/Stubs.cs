using System;using System.Collections.Generic;using BepInEx.Configuration;
public class OptionsMenuBehaviour {}
namespace UnityEngine {public struct Color {public int Id;public static Color red=>new(){Id=0};public static Color green=>new(){Id=1};}}
namespace TownOfHost.Modules.ClientOptions {
 public class ClientActionItem {public static List<ClientActionItem> Items=new();public string Name;public int? Page;public Action OnClickAction;public Toggle ToggleButton=new();public ClientActionItem(string n,OptionsMenuBehaviour o,bool tooltip,int? page){Name=n;Page=page;Items.Add(this);}public void Click()=>OnClickAction?.Invoke();}
 public class Toggle {public Background Background=new();public Rollover Rollover=new();}public class Background{public UnityEngine.Color color;}public class Rollover{public UnityEngine.Color Last;public void ChangeOutColor(UnityEngine.Color c){Last=c;}}
}
namespace TownOfHost {public static class Main{public static bool IsAndroid()=>false;public static ConfigEntry<bool> EnableDiscordRichPresence,ShowRoomCode,DisableWaveAnimation;}}
