using System.Reflection;using System.Text.Json.Nodes;using TownOfHost;
partial class Program{
static JsonNode Snapshot()=>JsonNode.Parse((string)typeof(ObsOverlay).GetMethod("BuildState",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null));
static void SetupSnapshot(){PlayerControl.AllPlayerControls.Clear();TownOfHost.Main.AllPlayerNames.Clear();var local=new PlayerControl{PlayerId=0,Data=new(){PlayerName="<b>配信者</b>"},Role="HiddenHostRole"};PlayerControl.LocalPlayer=local;PlayerControl.AllPlayerControls.Add(local);PlayerControl.AllPlayerControls.Add(new(){PlayerId=1,Role="HiddenOtherRole"});TownOfHost.Main.ObsShowRoles.Value=0;GameStates.InGame=true;GameStates.IsLobby=false;}
static void RunSnapshots(){
Test("actual snapshot default hides all roles and individual tasks",()=>{SetupSnapshot();var s=Snapshot();Check(s["players"].AsArray().All(p=>p["role"].ToString()==""&&p["tasks"].ToString()==""));Check(!s.ToJsonString().Contains("HiddenHostRole")&&!s.ToJsonString().Contains("HiddenOtherRole"));});
Test("actual snapshot omits connection and private identity",()=>{var s=Snapshot().ToJsonString();foreach(var word in new[]{"FriendCode","Puid","synthetic-private","account_id","token","ClientId","PlayerId"})Check(!s.Contains(word));});
Test("actual snapshot strips TMP names",()=>Check(Snapshot()["players"][0]["name"].ToString()=="配信者"));
Test("actual dead-only setting hides while alive",()=>{TownOfHost.Main.ObsShowRoles.Value=1;Check(Snapshot()["players"][0]["role"].ToString()=="");});
Test("actual dead-only setting reveals after explicit optin and death",()=>{PlayerControl.LocalPlayer.Data.Dead=true;Check(Snapshot()["players"][0]["role"].ToString()=="HiddenHostRole");});
Test("actual lobby never reveals roles in spoiler mode",()=>{TownOfHost.Main.ObsShowRoles.Value=2;GameStates.InGame=false;GameStates.IsLobby=true;Check(Snapshot()["players"].AsArray().All(p=>p["role"].ToString()==""));});
Test("actual invalid color falls back",()=>{PlayerControl.AllPlayerControls[1].Data.DefaultOutfit.ColorId=999;Check(Snapshot()["players"][1]["color"].ToString()=="#888888");});
Test("actual snapshot excludes disconnected players",()=>{PlayerControl.AllPlayerControls[1].Data.Disconnected=true;Check(Snapshot()["players"].AsArray().Count==1);});
Test("actual snapshot clears outside room",()=>{GameStates.InGame=false;GameStates.IsLobby=false;Check(Snapshot().AsObject().Count==0);});
Test("actual missing local player clears snapshot",()=>{PlayerControl.LocalPlayer=null;GameStates.IsLobby=true;Check(Snapshot().AsObject().Count==0);});
}
}