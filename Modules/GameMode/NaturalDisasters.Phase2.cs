using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using UnityEngine;

namespace TownOfHost;

public static partial class NaturalDisasters
{
    const float HazardRadius = 1.05f;
    enum DisasterKind { Meteor, Earthquake, SandStorm, VolcanoEruption, Sinkhole, Tornado, Tsunami }
    static readonly Dictionary<DisasterKind, OptionItem> weights = new();
    static OptionItem quakeDuration, quakeSpeed, sandDuration, sandVision, lavaDuration, lavaStep, holeDuration;
    // Each membership belongs to an object. Removing one source cannot remove another's effect.
    static readonly Dictionary<byte, HashSet<DisasterObject>> quakeSources = new();
    static readonly Dictionary<byte, HashSet<DisasterObject>> sandSources = new();
    static readonly Dictionary<byte, float> originalSpeeds = new();

    static void SetupPhase2Options()
    {
        SetupPhase3Options();
        int id = 220010;
        foreach (DisasterKind kind in Enum.GetValues(typeof(DisasterKind)))
            weights[kind] = IntegerOptionItem.Create(id++, "NDWeight" + kind, new(0, 100, 5), 50, TabGroup.MainSettings, false)
                .SetTag(CustomOptionTags.NaturalDisasters);
        quakeDuration = IntegerOptionItem.Create(220020, "NDQuakeDuration", new(1, 120, 1), 30, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Seconds).SetTag(CustomOptionTags.NaturalDisasters);
        quakeSpeed = FloatOptionItem.Create(220021, "NDQuakeSpeed", new(.05f, 2f, .05f), .1f, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Multiplier).SetTag(CustomOptionTags.NaturalDisasters);
        sandDuration = IntegerOptionItem.Create(220022, "NDSandDuration", new(1, 120, 1), 30, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Seconds).SetTag(CustomOptionTags.NaturalDisasters);
        sandVision = FloatOptionItem.Create(220023, "NDSandVision", new(0f, 1f, .05f), 0f, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Multiplier).SetTag(CustomOptionTags.NaturalDisasters);
        lavaDuration = IntegerOptionItem.Create(220024, "NDLavaDuration", new(1, 120, 1), 5, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Seconds).SetTag(CustomOptionTags.NaturalDisasters);
        lavaStep = FloatOptionItem.Create(220025, "NDLavaStep", new(.5f, 5f, .5f), 1f, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Seconds).SetTag(CustomOptionTags.NaturalDisasters);
        // EHR sinkholes persist indefinitely. Shell explicitly bounds their lifetime.
        holeDuration = IntegerOptionItem.Create(220026, "NDHoleDuration", new(1, 120, 1), 30, TabGroup.MainSettings, false)
            .SetValueFormat(OptionFormat.Seconds).SetTag(CustomOptionTags.NaturalDisasters);
    }

    static bool TrySelectDisaster(out DisasterKind selected)
    {
        selected = DisasterKind.Meteor;
        int total = weights.Values.Sum(option => option.GetInt());
        if (total <= 0) return false;
        int roll = IRandom.Instance.Next(total);
        foreach (var entry in weights)
        {
            roll -= entry.Value.GetInt();
            if (roll < 0) { selected = entry.Key; return true; }
        }
        return false;
    }

    static float GetDuration(DisasterKind kind) => kind switch
    {
        DisasterKind.Tornado => tornadoDuration.GetInt(),
        DisasterKind.Tsunami => float.PositiveInfinity,
        DisasterKind.Earthquake => quakeDuration.GetInt(),
        DisasterKind.SandStorm => sandDuration.GetInt(),
        DisasterKind.VolcanoEruption => 3f * lavaStep.GetFloat() + lavaDuration.GetInt(),
        DisasterKind.Sinkhole => holeDuration.GetInt(),
        _ => 5f
    };
    static float GetHazardRadius(DisasterObject source) => source.Kind == DisasterKind.VolcanoEruption
        ? (1.5f - (4 - source.LavaPhase) * .4f) * (HazardRadius / 1.5f) : HazardRadius;
    static CustomDeathReason GetDeathReason(DisasterKind kind) => kind switch
    {
        DisasterKind.VolcanoEruption => CustomDeathReason.Lava,
        DisasterKind.Sinkhole => CustomDeathReason.Sunken,
        _ => CustomDeathReason.Meteor
    };

    static void UpdateDisasterEffects(DisasterObject source, float elapsed)
    {
        if (source.Kind == DisasterKind.VolcanoEruption)
        {
            int phase = Mathf.Clamp(1 + Mathf.FloorToInt(elapsed / lavaStep.GetFloat()), 1, 4);
            if (phase != source.LavaPhase) { source.LavaPhase = phase; source.ShowImpact(); }
        }
        if (source.Kind != DisasterKind.Earthquake && source.Kind != DisasterKind.SandStorm) return;
        // Status effects need only 10 checks/sec; lethal hazards still check every physics tick.
        if (Time.fixedTime < source.NextEffectCheck) return;
        source.NextEffectCheck = Time.fixedTime + .1f;
        var memberships = source.Kind == DisasterKind.Earthquake ? quakeSources : sandSources;
        var inside = source.InsidePlayers;
        inside.Clear();
        var players = PlayerControl.AllPlayerControls;
        for (int i = 0; i < players.Count; i++)
        {
            var pc = players[i];
            if (pc == null || pc.PlayerId > 15 || pc.Data == null || pc.Data.Disconnected || pc.Data.IsDead
                || !pc.IsAlive() || pc.Is(CustomRoles.GM) || pc.IsTestBot() || pc.isDummy) continue;
            if ((pc.GetTruePosition() - source.Position).sqrMagnitude <= HazardRadius * HazardRadius)
                inside.Add(pc.PlayerId);
        }
        // Copy IDs into a reusable buffer before membership changes mutate the dictionary.
        var ids = source.EffectPlayers;
        ids.Clear();
        foreach (byte id in memberships.Keys) ids.Add(id);
        foreach (byte id in inside) if (!memberships.ContainsKey(id)) ids.Add(id);
        foreach (byte id in ids) SetEffectMembership(memberships, source, id, inside.Contains(id));
    }
    static void SetEffectMembership(Dictionary<byte, HashSet<DisasterObject>> memberships, DisasterObject source, byte id, bool inside)
    {
        if (!memberships.TryGetValue(id, out var sources))
        {
            if (!inside) return;
            sources = new();
            memberships[id] = sources;
        }
        int before = sources.Count;
        if (inside) sources.Add(source); else sources.Remove(source);
        int after = sources.Count;
        if (after == 0) memberships.Remove(id);
        if ((before == 0) == (after == 0)) return;
        if (ReferenceEquals(memberships, quakeSources))
        {
            if (after > 0)
            {
                float speed = Main.AllPlayerSpeed.TryGetValue(id, out float value) ? value : Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                originalSpeeds[id] = speed;
                Main.AllPlayerSpeed[id] = Mathf.Min(speed, quakeSpeed.GetFloat());
            }
            else if (originalSpeeds.Remove(id, out float original)) Main.AllPlayerSpeed[id] = original;
        }
        // GameOptions already handles host and vanilla recipients; no custom RPC required.
        Dirty(id);
    }

    static void Dirty(byte id)
    {
        var player = PlayerCatch.GetPlayerById(id);
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost && GameStates.InGame
            && !GameStates.IsEnded && player != null && player.Data != null && !player.Data.Disconnected)
            player.MarkDirtySettings();
    }

    static void RemoveEffectSource(DisasterObject source)
    {
        foreach (var memberships in new[] { quakeSources, sandSources })
            foreach (byte id in memberships.Keys.ToArray()) SetEffectMembership(memberships, source, id, false);
    }

    static void ClearAreaEffects()
    {
        var affected = quakeSources.Keys.Concat(sandSources.Keys).Distinct().ToArray();
        foreach (var pair in originalSpeeds) Main.AllPlayerSpeed[pair.Key] = pair.Value;
        quakeSources.Clear();
        sandSources.Clear();
        originalSpeeds.Clear();
        foreach (byte id in affected) Dirty(id);
    }

    public static void ApplyDisasterVision(byte id, IGameOptions opt)
    {
        if (!IsActive || !IsThisMode || !sandSources.TryGetValue(id, out var sources) || sources.Count == 0) return;
        opt.SetVision(false);
        opt.SetFloat(FloatOptionNames.CrewLightMod, sandVision.GetFloat());
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, sandVision.GetFloat());
    }

    static string GetDisasterSprite(DisasterKind kind, int phase)
    {
        if (kind == DisasterKind.VolcanoEruption)
        {
            string row = new('W', phase * 2);
            return "<size=120%><line-height=97%><cspace=0.16em><mark=#ff6200>"
                + string.Join("\n", Enumerable.Repeat(row, phase * 2)) + "</mark>";
        }
        return disasterSprites[kind];
    }
    // Rich-text sprites from the pinned EHR commit; only standard Shapeshift carries them.
    static readonly Dictionary<DisasterKind, string> disasterSprites = new()
    {
        [DisasterKind.Earthquake] = "<size=120%><line-height=97%><cspace=0.16em><mark=#000000>WW</mark><mark=#5e5e5e>W</mark><mark=#adadad>W</mark><#0000>WWWW</color>\n<mark=#5e5e5e>W</mark><mark=#000000>W</mark><mark=#5e5e5e>W</mark><mark=#adadad>WW</mark><#0000>WWW</color>\n<mark=#5e5e5e>W</mark><mark=#000000>WW</mark><mark=#5e5e5e>WW</mark><mark=#adadad>WW</mark><#0000>W</color>\n<mark=#adadad>W</mark><mark=#5e5e5e>W</mark><mark=#000000>WWW</mark><mark=#5e5e5e>WW</mark><mark=#adadad>W</mark>\n<#0000>W</color><mark=#adadad>W</mark><mark=#5e5e5e>WW</mark><mark=#000000>WWW</mark><mark=#5e5e5e>W</mark>\n<#0000>WW</color><mark=#adadad>WW</mark><mark=#5e5e5e>WW</mark><mark=#000000>WW</mark>\n<#0000>WWWW</color><mark=#adadad>WW</mark><mark=#5e5e5e>W</mark><mark=#000000>W</mark>\n<#0000>WWWWW</color><mark=#adadad>W</mark><mark=#5e5e5e>W</mark><mark=#000000>W",
        [DisasterKind.SandStorm] = "<size=120%><line-height=97%><cspace=0.16em><mark=#ffff99>WWWWWWWW\nWWWWWWWW\nWWWWWWWW\nWWWWWWWW\nWWWWWWWW\nWWWWWWWW\nWWWWWWWW\nWWWWWWWW",
        [DisasterKind.Sinkhole] = "<size=120%><line-height=97%><cspace=0.16em><mark=#7d7d7d>WWWWWWWW</mark>\n<mark=#545454>W</mark><mark=#424242>WWWWWW</mark><mark=#7d7d7d>W</mark>\n<mark=#7d7d7d>W</mark><mark=#424242>W</mark><mark=#000000>WWWW</mark><mark=#424242>W</mark><mark=#545454>W</mark>\n<mark=#545454>W</mark><mark=#424242>W</mark><mark=#000000>WWWW</mark><mark=#424242>W</mark><mark=#7d7d7d>W</mark>\n<mark=#7d7d7d>W</mark><mark=#424242>W</mark><mark=#000000>WWWW</mark><mark=#424242>W</mark><mark=#545454>W</mark>\n<mark=#545454>W</mark><mark=#424242>W</mark><mark=#000000>WWWW</mark><mark=#424242>W</mark><mark=#7d7d7d>W</mark>\n<mark=#7d7d7d>W</mark><mark=#424242>WWWWWW</mark><mark=#545454>W</mark>\n<mark=#7d7d7d>WWWWWWWW",
    };
}
