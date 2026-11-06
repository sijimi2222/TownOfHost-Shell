using System;
using System.Linq;
using System.Text.Json;
using HarmonyLib;
using UnityEngine;

namespace TownOfHost;

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class ObsOverlay
{
    public const int Port=LoopbackOverlayServer.DefaultPort;
    public static string Url=>$"http://localhost:{Port}/";
    private static readonly LoopbackOverlayServer Server=new();
    private static float nextUpdateAt;
    private static bool quitHook;
    public static string RoleModeLabel(int mode)=>mode switch {1=>"死亡後のみ(役職・タスク公開)",2=>"常時公開(ネタバレ注意)",_=>"非公開"};
    public static void ApplySetting()
    {
        if(Main.ObsOverlay?.Value!=true){Stop();return;}
        if(!Server.Start()){Main.ObsOverlay.Value=false;Logger.Warn("OBS表示を開始できませんでした。ポートの使用状況を確認してください。","ObsOverlay");return;}
        if(!quitHook){Application.quitting+=(Action)Stop;quitHook=true;}
        nextUpdateAt=0;Server.Publish("{}");
    }
    public static void Stop(){Server.Stop();nextUpdateAt=0;}
    public static void Postfix()
    {
        if(Main.ObsOverlay?.Value!=true){if(Server.Running)Stop();return;}
        if(!Server.Running)ApplySetting();
        if(Time.unscaledTime<nextUpdateAt)return;nextUpdateAt=Time.unscaledTime+0.5f;
        try{Server.Publish(BuildState());}catch{Server.Publish("{}");}
    }
    private static string BuildState()
    {
        var local=PlayerControl.LocalPlayer;
        bool inGame=GameStates.InGame;
        if(local==null || (!GameStates.IsLobby&&!inGame))return "{}";
        bool reveal=StreamSafety.RevealRoles(Main.ObsShowRoles.Value,inGame,!local.IsAlive());
        var players=PlayerControl.AllPlayerControls.ToArray().Where(p=>p!=null&&p.Data!=null&&!p.Data.Disconnected).OrderBy(p=>p.PlayerId).Take(30).Select(p=>
        {
            string role="",roleColor="#ffffff",tasks="";
            if(reveal)
            {
                var r=p.GetCustomRole();role=StreamSafety.Plain(UtilsRoleText.GetRoleName(r).RemoveHtmlTags());roleColor=StreamSafety.Color(UtilsRoleText.GetRoleColorCode(r));
                var t=p.GetPlayerTaskState();if(t!=null&&t.hasTasks&&t.AllTasksCount>0)tasks=$"{t.CompletedTasksCount}/{t.AllTasksCount}";
            }
            var c=p.Data.DefaultOutfit?.ColorId??0;
            return new {name=StreamSafety.Plain((Main.AllPlayerNames.TryGetValue(p.PlayerId,out var n)?n:p.Data.PlayerName).RemoveHtmlTags(),40),color=c>=0&&c<Palette.PlayerColors.Length?"#"+ColorUtility.ToHtmlStringRGB(Palette.PlayerColors[c]):"#888888",dead=inGame&&!p.IsAlive(),tasks,role,roleColor};
        }).ToArray();
        return JsonSerializer.Serialize(new {phase=GameStates.IsLobby?"ロビー":GameStates.IsMeeting?"会議中":"タスク中",map=inGame?((MapNames)Main.NormalOptions.MapId).ToString():"",alive=players.Count(p=>!p.dead),total=players.Length,players});
    }
    [HarmonyPatch(typeof(HudManager),nameof(HudManager.OnDestroy)),HarmonyPrefix]
    private static void OnHudDestroyed(){Stop();StreamerInfo.StopNetwork();}
    [HarmonyPatch(typeof(AmongUsClient),nameof(AmongUsClient.OnDisconnected)),HarmonyPrefix]
    private static void OnDisconnected(){Stop();StreamerInfo.StopNetwork();TownOfHost.Modules.YouTubeMapVote.Reset();}
}
