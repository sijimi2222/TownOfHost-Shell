using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;

namespace TownOfHost.Modules;

[HarmonyPatch]
internal static class BugReportRoundLog
{
    private const int Capacity = 160;
    private static readonly object Gate = new();
    private static readonly Queue<string> Entries = new();
    private static long roundStart;
    private static bool recording;

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start)), HarmonyPrefix]
    private static void OnRoundStart()
    {
        if (AmongUsClient.Instance == null || GameStates.IsFreePlay) return;
        Begin();
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start)), HarmonyPostfix]
    private static void OnMeeting() => Record("meeting_started");

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd)), HarmonyPostfix]
    private static void OnRoundEnd() => Finish("round_ended");

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame)), HarmonyPrefix]
    private static void OnExit() => Finish("left_game");

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined)), HarmonyPostfix]
    private static void OnJoin() => Finish("returned_to_lobby");

    internal static void Begin()
    {
        lock (Gate)
        {
            Entries.Clear();
            roundStart = Stopwatch.GetTimestamp();
            recording = true;
            Append("round_started");
        }
    }

    internal static void Finish(string eventName)
    {
        lock (Gate)
        {
            if (!recording) return;
            Append(eventName);
            recording = false;
        }
    }

    private static void Record(string eventName)
    {
        lock (Gate)
            if (recording) Append(eventName);
    }

    internal static void RecordWarning(LogLevel level, string sourcePath, int line)
    {
        lock (Gate)
        {
            if (!recording) return;
            // Caller metadata only. Never forward the original log text or a local path.
            var source = Path.GetFileName(sourcePath);
            if (string.IsNullOrEmpty(source) || source.Length > 60 ||
                !source.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_' or '-'))
                source = "unknown";
            Append($"{level} {source}:{line}");
        }
    }

    internal static string Snapshot()
    {
        lock (Gate)
            return Entries.Count == 0 ? null : string.Join("\n", Entries);
    }

    private static void Append(string entry)
    {
        if (Entries.Count == Capacity) Entries.Dequeue();
        double seconds = (Stopwatch.GetTimestamp() - roundStart) / (double)Stopwatch.Frequency;
        Entries.Enqueue($"{seconds.ToString("0.000", CultureInfo.InvariantCulture)}s {entry}");
    }
}
