using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using InnerNet;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost.Development;

// Compiled only in ShellDevelopment by the existing project Compile Remove rule.
// Observes lifecycle boundaries; never suppresses exceptions, sends packets, or changes teardown order.
internal enum ExitStage
{
    ExitRequested, ExitReturned, DisconnectStarted, DisconnectReturned,
    ObjectsResetStarted, ObjectsResetReturned, CommentsStopStarted, CommentsStopReturned,
    RolesDisposeStarted, RolesDisposeReturned, HudDestroyStarted, HudDestroyReturned,
    SceneChangeStarted, SceneChangeReturned, MenuStartStarted, MenuStartReturned,
    MenuTickReturned, TrackingComplete, LifecycleException, MenuQuitInvocation, ApplicationQuitting,
    ManagedUnhandled,
    VersionStartStarted, VersionStartReturned, VersionPostfixStarted, VersionPostfixReturned,
    VersionTemplateStarted, VersionTemplateReturned, VersionCredentialsStarted, VersionCredentialsReturned,
    VersionErrorTextStarted, VersionErrorTextReturned, VersionSupportCheckStarted, VersionSupportCheckReturned,
    VersionEventTextStarted, VersionEventTextReturned, VersionStatisticsStarted, VersionStatisticsReturned,
    VersionStreamTextStarted, VersionStreamTextReturned, MenuTickStarted,
    ModTickStarted, ModTickReturned, ModTasksStarted, ModTasksReturned,
    ModMurderStarted, ModMurderReturned, ModStampStarted, ModStampReturned,
    HeaderLayoutStarted, HeaderLayoutReturned, ExitCallObserved
}

internal sealed class SessionExitTrace
{
    internal const int MaximumLines = 64;
    readonly HashSet<ExitStage> emitted = new();
    readonly HashSet<ExitStage> repeats = new();
    readonly object gate = new();
    readonly Action<string> write;
    readonly Func<long> milliseconds;
    long began;
    int ticks, lines;
    internal int Sequence { get; private set; }
    internal bool Active { get; private set; }
    internal SessionExitTrace(Action<string> write, Func<long> milliseconds)
    { this.write = write; this.milliseconds = milliseconds; }
    internal void Begin(int reason)
    {
        lock (gate)
        {
        // ExitGame can call DisconnectInternal synchronously. Keep one sequence for both.
        if (Active) return;
        Sequence++; Active = true; began = milliseconds(); emitted.Clear(); repeats.Clear(); ticks = lines = 0;
        Emit(ExitStage.ExitRequested, reason: reason);
        }
    }
    internal void Emit(ExitStage stage, Exception error = null, bool afterCompletion = false, int reason = -1)
    {
        lock (gate)
        {
        if ((!Active && !afterCompletion) || Sequence == 0 || lines >= MaximumLines) return;
        bool repeat = !emitted.Add(stage);
        // Show the first repeat of important entry boundaries, not an unbounded count per frame.
        // One old log line per stage could otherwise conceal duplicate teardown or VersionShower starts.
        if (repeat && (!RepeatBoundary(stage) || !repeats.Add(stage))) return;
        lines++;
        string kind = error switch
        {
            null => "None", NullReferenceException => "NullReference", ObjectDisposedException => "Disposed",
            OperationCanceledException => "Cancelled", ArgumentException => "Argument",
            InvalidOperationException => "InvalidOperation", _ => "Other"
        };
        try { write($"SESSION_EXIT_V1 seq={Sequence} stage={stage} elapsedMs={Math.Max(0, milliseconds() - began)} reason={reason} exception={kind}" + (repeat ? " repeat=2plus" : "")); }
        catch { /* Diagnostics must not change the observed lifecycle, even if the log is unavailable. */ }
        }
    }
    static bool RepeatBoundary(ExitStage stage) => stage is ExitStage.ExitCallObserved or ExitStage.DisconnectStarted
        or ExitStage.ObjectsResetStarted or ExitStage.CommentsStopStarted or ExitStage.RolesDisposeStarted
        or ExitStage.HudDestroyStarted or ExitStage.VersionStartStarted;
    internal void MenuTick()
    {
        lock (gate)
        {
        if (!Active) return;
        Emit(ExitStage.MenuTickReturned);
        if (++ticks < 2) return;
        Emit(ExitStage.TrackingComplete); Active = false;
        }
    }
}

internal static class SessionExitDiagnostics
{
    internal const string Marker = "SESSION_EXIT_V1";
    internal const string ProbeRevision = "title-frame-v2";
    static readonly SessionExitTrace trace = new(Write, () => Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000));
    static bool installed;
    static void Write(string message)
    {
        // Managed counters only: no native object/property access while objects are being destroyed.
        Logger.Info(message + $" roles={CustomRoleManager.AllActiveRoles.Count} objects={CustomNetObject.AllObjects.Count} chatStatus={(int)StreamerInfo.Status} probe={ProbeRevision}", "SessionExit");
    }
    internal static void Mark(ExitStage stage) => trace.Emit(stage);
    internal static void Install()
    {
        if (installed) return;
        installed = true;
        Application.quitting += new Action(OnQuitting);
        AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
    }
    static void OnQuitting() => trace.Emit(ExitStage.ApplicationQuitting, afterCompletion: true);
    static void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
        => trace.Emit(ExitStage.ManagedUnhandled, args.ExceptionObject as Exception, afterCompletion: true);

    static Exception Fault(Exception error)
    { if (error != null) trace.Emit(ExitStage.LifecycleException, error); return error; }

    // One explicit target per patch class, including its finalizer. Do not combine
    // multiple HarmonyPatch attributes as if they represented a list of targets.
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
    static class ExitPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)] static void Prefix(DisconnectReasons reason) { Install(); trace.Begin((int)reason); trace.Emit(ExitStage.ExitCallObserved); }
        [HarmonyPostfix, HarmonyPriority(Priority.Last)] static void Postfix() => trace.Emit(ExitStage.ExitReturned);
        [HarmonyFinalizer] static Exception Finalizer(Exception __exception) => Fault(__exception);
    }
    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.DisconnectInternal))]
    static class DisconnectPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)] static void Prefix(DisconnectReasons reason) { Install(); trace.Begin((int)reason); trace.Emit(ExitStage.DisconnectStarted); }
        [HarmonyPostfix, HarmonyPriority(Priority.Last)] static void Postfix() => trace.Emit(ExitStage.DisconnectReturned);
        [HarmonyFinalizer] static Exception Finalizer(Exception __exception) => Fault(__exception);
    }
    [HarmonyPatch(typeof(CustomNetObject), nameof(CustomNetObject.Reset))]
    static class ObjectsPatch
    {
        [HarmonyPrefix] static void Prefix() => trace.Emit(ExitStage.ObjectsResetStarted);
        [HarmonyPostfix] static void Postfix() => trace.Emit(ExitStage.ObjectsResetReturned);
        [HarmonyFinalizer] static Exception Finalizer(Exception __exception) => Fault(__exception);
    }
    [HarmonyPatch(typeof(StreamerInfo), nameof(StreamerInfo.StopNetwork))]
    static class CommentsPatch
    {
        [HarmonyPrefix] static void Prefix() => trace.Emit(ExitStage.CommentsStopStarted);
        [HarmonyPostfix] static void Postfix() => trace.Emit(ExitStage.CommentsStopReturned);
        [HarmonyFinalizer] static Exception Finalizer(Exception __exception) => Fault(__exception);
    }
    [HarmonyPatch(typeof(CustomRoleManager), nameof(CustomRoleManager.Dispose))]
    static class RolesPatch
    {
        [HarmonyPrefix] static void Prefix() => trace.Emit(ExitStage.RolesDisposeStarted);
        [HarmonyPostfix] static void Postfix() => trace.Emit(ExitStage.RolesDisposeReturned);
        [HarmonyFinalizer] static Exception Finalizer(Exception __exception) => Fault(__exception);
    }
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.OnDestroy))]
    static class HudPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)] static void Prefix() => trace.Emit(ExitStage.HudDestroyStarted);
        [HarmonyPostfix, HarmonyPriority(Priority.Last)] static void Postfix() => trace.Emit(ExitStage.HudDestroyReturned);
        [HarmonyFinalizer] static Exception Finalizer(Exception __exception) => Fault(__exception);
    }
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnActiveSceneChange))]
    static class ScenePatch
    {
        [HarmonyPrefix] static void Prefix() => trace.Emit(ExitStage.SceneChangeStarted);
        [HarmonyPostfix] static void Postfix() => trace.Emit(ExitStage.SceneChangeReturned);
        [HarmonyFinalizer] static Exception Finalizer(Exception __exception) => Fault(__exception);
    }
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    static class MenuPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)] static void Prefix() { Install(); trace.Emit(ExitStage.MenuStartStarted); }
        [HarmonyPostfix, HarmonyPriority(Priority.Last)] static void Postfix() => trace.Emit(ExitStage.MenuStartReturned);
        [HarmonyFinalizer] static Exception Finalizer(Exception __exception) => Fault(__exception);
    }
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.LateUpdate))]
    static class MenuTickPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)] static void Prefix() => trace.Emit(ExitStage.MenuTickStarted);
        [HarmonyPostfix, HarmonyPriority(Priority.Last)] static void Postfix() => trace.MenuTick();
        [HarmonyFinalizer] static Exception Finalizer(Exception __exception) => Fault(__exception);
    }
    [HarmonyPatch(typeof(SeasideMainMenuPatch.HomeView), "Invoke")]
    static class MenuActionPatch
    {
        [HarmonyPrefix] static void Prefix(string id) { if (id == "Quit") trace.Emit(ExitStage.MenuQuitInvocation, afterCompletion: true); }
    }
    [HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
    static class VersionPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)] static void Prefix() => trace.Emit(ExitStage.VersionStartStarted);
        [HarmonyPostfix, HarmonyPriority(Priority.Last)] static void Postfix() => trace.Emit(ExitStage.VersionStartReturned);
        [HarmonyFinalizer] static Exception Finalizer(Exception __exception) => Fault(__exception);
    }
    [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
    static class ModTickPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)] static void Prefix() => trace.Emit(ExitStage.ModTickStarted);
        [HarmonyPostfix, HarmonyPriority(Priority.Last)] static void Postfix() => trace.Emit(ExitStage.ModTickReturned);
        [HarmonyFinalizer] static Exception Finalizer(Exception __exception) => Fault(__exception);
    }
}
