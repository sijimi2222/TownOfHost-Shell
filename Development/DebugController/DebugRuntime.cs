using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using InnerNet;
using UnityEngine;
using ShellDebugController.Shared;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Impostor;
[assembly: AssemblyMetadata("ShellDebugController", "development-only")]
namespace TownOfHost.Development;

[HarmonyPatch]
internal static class ShellDebugRuntime
{
    internal static readonly ControlLease Lease=new();
    internal static readonly TrafficLedger Traffic=new();
    private static readonly Stopwatch Clock=Stopwatch.StartNew();
    private static DebugPipeServer server;
    private static DebugSnapshot snapshot=new(){ProcessId=Environment.ProcessId};
    private static double nextSnapshot;
    private static int game;
    private static int epoch;
    private static bool hadCapture;
    private static int lastFrame=-1;
    internal static bool ZoomEnabled { get; private set; }
    internal static bool Capture { get; private set; }
    internal static double Now=>Clock.Elapsed.TotalSeconds;
    private static readonly FieldInfo Charging=AccessTools.Field(typeof(Nova),"charging"),Waiting=AccessTools.Field(typeof(Nova),"waiting"),Beaming=AccessTools.Field(typeof(Nova),"beaming"),TileStyle=AccessTools.Field(typeof(NovaBeamTile),"style");
    private static readonly FieldInfo Charge=AccessTools.Field(typeof(Nova),"chargeTime"),Delay=AccessTools.Field(typeof(Nova),"fireDelay");
    private static readonly PropertyInfo Ready=AccessTools.Property(typeof(Nova),"TilesReady");
    internal static DebugSnapshot Snapshot()=>Volatile.Read(ref snapshot);
    internal static void Tick()
    {
        if(!OperatingSystem.IsWindows())return;
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        if(server==null){server=new(Lease,Snapshot,()=>Now);server.Start();}
        try
        {
            var client=AmongUsClient.Instance;var player=PlayerControl.LocalPlayer;
            if(client!=null&&game!=client.GameId){Lease.Close();Traffic.Clear();game=client.GameId;unchecked{epoch++;}}
            var flags=Lease.Read(Now,epoch);
            bool task=GameStates.IsInGame&&GameStates.IsInTask&&!GameStates.IsMeeting&&!GameStates.CalledMeeting
                &&MeetingHud.Instance==null&&!GameStates.Intro&&!GameStates.ExiledAnimate&&client!=null&&client.GameState==AmongUsClient.GameStates.Started;
            bool valid=player!=null&&player.Data!=null&&player.Data.Role!=null&&!player.Data.Disconnected;
            bool pc=Constants.GetPlatformType() is Platforms.StandaloneSteamPC or Platforms.StandaloneEpicPC;
            var eligibility=new DebugEligibility(pc,client!=null&&client.AmHost,valid&&player.AmOwner,client!=null&&!client.IsGamePublic,
                client!=null&&client.NetworkMode==NetworkModes.OnlineGame,task,valid&&player.IsAlive(),valid&&player.CanMove,
                valid&&(player.inVent||player.onLadder||player.inMovingPlat||player.MyPhysics==null||player.MyPhysics.Animations.IsPlayingAnyLadderAnimation()));
            ZoomEnabled=flags.Debug&&flags.Zoom&&eligibility.LivingHost;
            Capture=flags.Debug&&(flags.Telemetry||flags.Simulation);
            if(hadCapture&&!Capture)Traffic.Clear();hadCapture=Capture;
            NoClipMovement.Update(player,client,flags.Debug&&flags.NoClip&&eligibility.Movement);
            if(!ZoomEnabled&&valid&&player.IsAlive())Zoom.Reset();
            if(Now<nextSnapshot)return;nextSnapshot=Now+.2;
            var value=new DebugSnapshot{Version=typeof(ShellDebugRuntime).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??typeof(ShellDebugRuntime).Assembly.GetName().Version?.ToString(3),Connected=true,ProcessId=Environment.ProcessId,SessionEpoch=epoch,Debug=flags.Debug,ZoomApplied=ZoomEnabled&&Options.UseZoom?.GetBool()==true,
                NoClipApplied=NoClipMovement.Active,
                Eligibility=!flags.Debug?"デバッグOFF":!eligibility.LivingHost?"生存中のPCホスト・非公開オンライン試合でのみ操作できます":flags.Zoom&&Options.UseZoom?.GetBool()!=true?"ゲーム内のズーム設定もONにしてください":"非公開テスト環境 / 操作可能",
                RawHookAvailable=ShellDebugTraffic.RawHookInstalled,
                ErrorHookAvailable=ShellDebugTraffic.ErrorHookInstalled};
            if(Capture)
            {
            value.MapId=Main.NormalOptions?.MapId;
            value.IsFungle=value.MapId.HasValue ? (MapNames)value.MapId.Value==MapNames.Fungle : null;
            value.NovaPoolTarget=value.IsFungle.HasValue ? NovaVisualPoolRules.Count(value.IsFungle.Value) : null;
            value.Participants=PlayerCatch.AllPlayerControls.Count(p=>p!=null&&p.PlayerId<254&&p.Data!=null&&!p.Data.Disconnected);
            var tiles=CustomNetObject.AllObjects.OfType<NovaBeamTile>().ToArray();value.NovaDummies=tiles.Length;
            var roles=CustomRoleManager.AllActiveRoles.Values.OfType<Nova>().ToArray();
            bool Is(FieldInfo field,Nova n)=>field?.GetValue(n) is true;
            value.NovaState=roles.Length==0?"Novaなし":roles.Any(n=>Is(Beaming,n))?"発射中":roles.Any(n=>Is(Waiting,n))?"発射待機":roles.Any(n=>Is(Charging,n))?"チャージ・照準":roles.Any(n=>Ready?.GetValue(n) is not true)?"描画準備中":"準備完了 / 再利用待機";
            int hidden=tiles.Count(t=>TileStyle?.GetValue(t) is NovaBeamTile.Style.Hidden);value.NovaState+=$" / 非表示・回収待機 {hidden}/{tiles.Length}";
            if(roles.Length>0){value.NovaChargeSeconds=Charge?.GetValue(roles[0]) is float c?c:3;value.NovaDelaySeconds=Delay?.GetValue(roles[0]) is float delay?delay:1;}
            Traffic.Fill(value,Now);
            }
            else value.NovaState="通信状態OFF";
            Volatile.Write(ref snapshot,value);
        }
        catch(Exception)
        {
            Lease.Close();Capture=ZoomEnabled=false;NoClipMovement.Restore();Zoom.Reset();
            Volatile.Write(ref snapshot,new DebugSnapshot{ProcessId=Environment.ProcessId,Eligibility="デバッグ状態の取得失敗。操作を解除しました"});
        }
    }
    internal static void Stop(){Lease.Close();Capture=ZoomEnabled=false;NoClipMovement.Restore();Zoom.Reset();server?.Dispose();server=null;}
    [HarmonyPatch(typeof(InnerNetClient),nameof(InnerNetClient.Update)),HarmonyPostfix]
    private static void ClientUpdate()=>Tick();
    [HarmonyPatch(typeof(HudManager),nameof(HudManager.Update)),HarmonyPrefix]
    private static void HudUpdate()=>Tick();
    [HarmonyPatch(typeof(MainMenuManager),nameof(MainMenuManager.Start)),HarmonyPostfix]
    private static void MenuStart(){NoClipMovement.Restore();Zoom.Reset();Tick();}
    [HarmonyPatch(typeof(InnerNetClient),nameof(InnerNetClient.OnDestroy)),HarmonyPrefix]
    private static void Destroy()=>Stop();
    [HarmonyPatch(typeof(MeetingHud),nameof(MeetingHud.Start)),HarmonyPrefix]
    private static void Meeting(){NoClipMovement.Restore();Zoom.Reset();}
    [HarmonyPatch(typeof(AmongUsClient),nameof(AmongUsClient.OnGameEnd)),HarmonyPrefix]
    private static void End(){NoClipMovement.Restore();Zoom.Reset();Lease.Close();}
}

