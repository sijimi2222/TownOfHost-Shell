using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TownOfHost.Roles.Core;

namespace TownOfHost;

public static partial class NaturalDisasters
{
    sealed class SurvivalEntry
    {
        public byte Id;
        public string Name;
        public float Seconds;
        public bool Stopped, Disconnected, Survived;
    }
    static readonly Dictionary<byte, SurvivalEntry> survival = new();
    static readonly List<SurvivalEntry> lastSurvival = new();
    static readonly List<byte> survivalOrder = new();
    static string rosterText = "", renderedRoster = "";
    static float nextRosterRefresh;
    static bool trackingSurvival;
    static float survivalStart;
    public static bool HasSurvivalResults => lastSurvival.Count > 0;

    static void BeginSurvival()
    {
        survival.Clear();
        survivalOrder.Clear();
        rosterText = "";
        nextRosterRefresh = 0f;
        lastSurvival.Clear();
        if (!IsActive || !AmongUsClient.Instance.AmHost) return;
        survivalStart = Time.realtimeSinceStartup;
        trackingSurvival = true;
        foreach (var pc in PlayerControl.AllPlayerControls)
        {
            if (!IsRealSurvivor(pc)) continue;
            string name = Main.AllPlayerNames.TryGetValue(pc.PlayerId, out var original) ? original : pc.GetRealName();
            survivalOrder.Add(pc.PlayerId);
            survival[pc.PlayerId] = new SurvivalEntry { Id = pc.PlayerId, Name = name.RemoveHtmlTags().Replace("\n", " ").Replace("\r", " ") };
        }
    }
    static bool IsRealSurvivor(PlayerControl pc) => pc != null && pc.PlayerId <= 15 && pc.Data != null
        && !pc.Data.Disconnected && !pc.Is(CustomRoles.GM) && !pc.IsTestBot() && !pc.isDummy && !pc.notRealPlayer;

    public static void RecordSurvivalStop(byte id, bool disconnected = false)
    {
        if (!trackingSurvival || !survival.TryGetValue(id, out var entry)) return;
        if ((disconnected && !entry.Disconnected) || !entry.Stopped) nextRosterRefresh = 0f;
        entry.Disconnected |= disconnected;
        if (entry.Stopped) return;
        entry.Seconds = Mathf.Max(0f, Time.realtimeSinceStartup - survivalStart);
        entry.Stopped = true;
    }
    static void TickSurvival()
    {
        if (!trackingSurvival) return;
        foreach (var entry in survival.Values)
        {
            var pc = PlayerCatch.GetPlayerById(entry.Id);
            if (pc == null || pc.Data == null || pc.Data.Disconnected) RecordSurvivalStop(entry.Id, true);
            else if (pc.Data.IsDead || !pc.IsAlive()) RecordSurvivalStop(entry.Id);
            else if (!entry.Stopped) entry.Seconds = Mathf.Max(0f, Time.realtimeSinceStartup - survivalStart);
        }
        RefreshRoster();
    }
    static void RefreshRoster()
    {
        if (Time.realtimeSinceStartup < nextRosterRefresh) return;
        nextRosterRefresh = Time.realtimeSinceStartup + 1f;
        var text = new StringBuilder("\n\n" + Translator.GetString("NDLivingMembers"));
        foreach (byte id in survivalOrder)
        {
            if (!survival.TryGetValue(id, out var entry)) continue;
            text.Append("\n<color=").Append(entry.Stopped ? "#FF4040" : "#00FF00").Append('>')
                .Append(entry.Name).Append("　").Append(FormatSurvivalTime(entry.Seconds));
            if (entry.Disconnected) text.Append(" (").Append(Translator.GetString("NDDisconnected")).Append(')');
            text.Append("</color>");
        }
        rosterText = text.ToString();
    }
    // Append only our suffix. Preserve the role description and ordinary task text above it.
    public static void AppendSurvivalRoster(TaskPanelBehaviour panel)
    {
        if (panel == null || panel.taskText == null) return;
        string suffix = IsActive && IsThisMode && trackingSurvival && GameStates.InGame
            && GameStates.introDestroyed && !GameStates.IsLobby ? rosterText : "";
        string text = panel.taskText.text;
        if (renderedRoster == suffix && (suffix.Length == 0 || text.EndsWith(suffix, StringComparison.Ordinal))) return;
        if (renderedRoster.Length > 0 && text.EndsWith(renderedRoster, StringComparison.Ordinal))
            text = text.Substring(0, text.Length - renderedRoster.Length);
        panel.taskText.text = text + suffix;
        renderedRoster = suffix;
    }
    // Called before the end-game ghost-role conversion; that conversion is not a real death.
    public static void FinishSurvival()
    {
        if (!trackingSurvival) return;
        TickSurvival();
        lastSurvival.Clear();
        foreach (var entry in survival.Values)
        {
            entry.Survived = !entry.Stopped && !entry.Disconnected;
            lastSurvival.Add(entry);
        }
        lastSurvival.Sort((a, b) => { int order = b.Seconds.CompareTo(a.Seconds); return order != 0 ? order : a.Id.CompareTo(b.Id); });
        trackingSurvival = false;
        survival.Clear();
        survivalOrder.Clear();
        rosterText = "";
        nextRosterRefresh = 0f;
        survivalStart = 0f;
    }
    static void ResetSurvival(string reason)
    {
        FinishSurvival();
        // Preserve only the completed result for the outro and existing /lastresult command.
        if (reason == "GameModuleInitializer" || reason == "OnGameStart initialization" || reason == "AmongUsClient.OnDisconnected")
            lastSurvival.Clear();
        survival.Clear();
        survivalOrder.Clear();
        rosterText = "";
        nextRosterRefresh = 0f;
        trackingSurvival = false;
        survivalStart = 0f;
    }
    public static string FormatSurvivalTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return string.Format(Translator.GetString("NDSurvivalTime"), total / 60, total % 60);
    }
    public static string SurvivalResultsText()
    {
        var text = new StringBuilder(Translator.GetString("NDResults"));
        var survivors = lastSurvival.Where(e => e.Survived && !e.Disconnected).Select(e => e.Name);
        text.Append('\n').Append(Translator.GetString("NDSurvivors")).Append(": ")
            .Append(string.Join(", ", survivors.DefaultIfEmpty(Translator.GetString("NDNoSurvivors"))));
        for (int i = 0; i < lastSurvival.Count; i++)
        {
            var entry = lastSurvival[i];
            text.Append('\n').Append(string.Format(Translator.GetString("NDSurvivalRank"), i + 1, entry.Name, FormatSurvivalTime(entry.Seconds)));
            if (entry.Disconnected) text.Append(" (").Append(Translator.GetString("NDDisconnected")).Append(')');
        }
        return text.ToString();
    }
    public static string PersonalSurvivalResult(byte id)
    {
        var entry = lastSurvival.Find(e => e.Id == id);
        return entry == null ? "" : string.Format(Translator.GetString("NDYourSurvival"), FormatSurvivalTime(entry.Seconds));
    }
    public static void SendSurvivalResults(byte recipient = byte.MaxValue)
    {
        if (!HasSurvivalResults || !AmongUsClient.Instance.AmHost || AmongUsClient.Instance.IsGameStarted) return;
        var chunk = new StringBuilder();
        foreach (string line in SurvivalResultsText().Split('\n'))
        {
            if (chunk.Length + line.Length > 450 && chunk.Length > 0)
            { Utils.SendMessage(chunk.ToString(), recipient); chunk.Clear(); }
            chunk.AppendLine(line);
        }
        if (chunk.Length > 0) Utils.SendMessage(chunk.ToString(), recipient);
    }
}
