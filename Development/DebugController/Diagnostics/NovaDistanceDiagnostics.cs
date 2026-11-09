using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyLib;
using TownOfHost.Modules;
using TownOfHost.Roles.Impostor;
using UnityEngine;

namespace TownOfHost.Development;

// This whole directory is excluded from public Release by TownOfHost.csproj.
// These observers never change text, material, transforms, camera or RPC state.
[HarmonyPatch]
internal static class NovaDistanceDiagnostics
{
    internal const string Revision = "nova-distance-diag-v1";
    internal static bool Enabled = true; // Disable/compile out when the audit is finished.
    private static readonly BeamDistanceAudit Audit = new();
    private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<string, FieldInfo> NovaFields = typeof(Nova).GetFields(Fields).GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First());
    private static readonly Dictionary<string, FieldInfo> TileFields = typeof(NovaBeamTile).GetFields(Fields).GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First());
    private static bool identity;
    private static readonly Regex Glyphs = new(@"em>-", RegexOptions.CultureInvariant);
    private static T Read<T>(object value, string field)
    {
        var table = value is Nova ? NovaFields : TileFields;
        return table.TryGetValue(field, out var member) && member.GetValue(value) is T result ? result : default;
    }
    private static bool Host => Enabled && Logger.isEnable && AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
    private static string Number(float n) => float.IsFinite(n) ? n.ToString("F3", CultureInfo.InvariantCulture) : "unknown";
    private static string Vector(Vector3 p) => Number(p.x) + "," + Number(p.y) + "," + Number(p.z);
    private static string Vector(Vector2 p) => Number(p.x) + "," + Number(p.y);
    private static string BoundsValue(Bounds b) => Vector(b.min) + ":" + Vector(b.max);
    private static string Property(object value, string name)
    {
        try
        {
            var result = value?.GetType().GetProperty(name)?.GetValue(value);
            if (result == null) return "unknown";
            string text = Convert.ToString(result, CultureInfo.InvariantCulture);
            return text.Length <= 160 ? text : "value_too_long";
        }
        catch (Exception e) { return "unavailable_" + e.GetType().Name; }
    }
    private static void Emit(long shot, string stage, object value)
    {
        try
        {
            string payload = JsonSerializer.Serialize(value);
            if (payload.Length > BeamDistanceAudit.MessageCharacters)
                payload = JsonSerializer.Serialize(new { revision = Revision, shot, stage, detail = "payload_limit", characters = payload.Length });
            Logger.Info(payload, "NovaDistance");
        }
        catch { /* Diagnostics cannot interrupt the role or its cleanup. */ }
    }
    private static void Identity()
    {
        if (identity) return;
        identity = true;
        string hash = "unavailable";
        var assembly = typeof(NovaDistanceDiagnostics).Assembly;
        try { using var file = File.OpenRead(assembly.Location); using var sha = SHA256.Create(); hash = Convert.ToHexString(sha.ComputeHash(file)); } catch { }
        Emit(0, "identity", new { revision = Revision, stage = "identity", configuration = "ShellDevelopment", version = Main.BepInExPluginVersion,
            onDiskSha256 = hash, mvid = assembly.ManifestModule.ModuleVersionId.ToString(), marker = "ShellDebugController=development-only",
            maxShotsPerGame = BeamDistanceAudit.ShotsPerGame, maxMessageCharacters = BeamDistanceAudit.MessageCharacters,
            scope = "host_local_observation_only_not_remote_pixel_proof" });
    }
    [HarmonyPatch(typeof(Nova), "Fire"), HarmonyPrefix]
    private static void FireBefore(Nova __instance)
    {
        try { if (Host) { Identity(); Audit.Begin(__instance, AmongUsClient.Instance.GameId); } } catch { }
    }
    [HarmonyPatch(typeof(Nova), "Fire"), HarmonyPostfix]
    private static void FireAfter(Nova __instance)
    {
        try
        {
            var shot = Audit.Find(__instance); if (shot == null) return;
            shot.InsideFire = false; shot.Planned = true;
            Capture(__instance, shot, "plan", false);
            if (!Read<bool>(__instance, "beaming")) Audit.End(__instance);
        }
        catch { Audit.End(__instance); }
    }
    [HarmonyPatch(typeof(Nova), "Fire"), HarmonyFinalizer]
    private static Exception FireFailed(Nova __instance, Exception __exception)
    {
        if (__exception == null) return null;
        try
        {
            var shot = Audit.Find(__instance);
            if (shot != null) Emit(shot.Id, "role_exception", new { revision = Revision, shot = shot.Id, stage = "role_exception", error = __exception.GetType().Name });
            Audit.End(__instance);
        }
        catch { }
        return __exception; // Preserve the original game's exception semantics.
    }
    [HarmonyPatch(typeof(Nova), nameof(Nova.OnFixedUpdate)), HarmonyPostfix]
    private static void Updated(Nova __instance, [HarmonyArgument(0)] PlayerControl player)
    {
        try
        {
            if (!Host || player == null || player.PlayerId != Read<byte>(__instance, "ownerId")) return;
            Identity(); Audit.Game(AmongUsClient.Instance.GameId);
            var shot = Audit.Find(__instance); if (shot == null || !shot.Planned) return;
            if (shot.Observe(Time.time, Read<bool>(__instance, "beamVisible"))) Capture(__instance, shot, "render", true);
        }
        catch { Audit.End(__instance); }
    }
    [HarmonyPatch(typeof(Nova), "Stop"), HarmonyPostfix]
    private static void Stopped(Nova __instance) => Ended(__instance);
    [HarmonyPatch(typeof(Nova), nameof(Nova.OnDestroy)), HarmonyPostfix]
    private static void Destroyed(Nova __instance) => Ended(__instance);
    [HarmonyPatch(typeof(Nova), nameof(Nova.OnStartMeeting)), HarmonyPostfix]
    private static void Meeting(Nova __instance) => Ended(__instance);
    private static void Ended(Nova __instance)
    {
        try
        {
            var shot = Audit.Find(__instance); if (shot == null || shot.InsideFire) return;
            Capture(__instance, shot, "cleanup", false); Audit.End(__instance);
        }
        catch { Audit.End(__instance); }
    }
    private static object CameraState(Camera camera, Vector2 start, Vector2 end)
    {
        if (camera == null) return new { available = false };
        try { return new { available = true, position = Vector(camera.transform.position), size = Number(camera.orthographicSize), aspect = Number(camera.aspect),
            near = Number(camera.nearClipPlane), far = Number(camera.farClipPlane), cullingMask = camera.cullingMask,
            startViewport = Vector(camera.WorldToViewportPoint(new Vector3(start.x, start.y, 0))), endViewport = Vector(camera.WorldToViewportPoint(new Vector3(end.x, end.y, 0))) }; }
        catch (Exception e) { return new { available = false, error = e.GetType().Name }; }
    }
    private static object MaterialState(Material material)
    {
        if (material == null) return new { available = false };
        var values = new Dictionary<string, string>();
        foreach (string key in new[] { "_StencilComp", "_Stencil", "_StencilOp", "_StencilReadMask", "_StencilWriteMask", "_ColorMask", "_CullMode", "_ZTestMode" })
            if (material.HasProperty(key)) values[key] = Number(material.GetFloat(key));
        foreach (string key in new[] { "_ClipRect", "_MaskCoord" }) if (material.HasProperty(key)) values[key] = material.GetVector(key).ToString();
        return new { available = true, shader = material.shader == null ? "unknown" : material.shader.name,
            maskHard = material.IsKeywordEnabled("MASK_HARD"), maskSoft = material.IsKeywordEnabled("MASK_SOFT"), uiClipRect = material.IsKeywordEnabled("UNITY_UI_CLIP_RECT"), values };
    }
    private static object TextState(NovaBeamTile tile, Vector2 anchor, Vector2 direction, List<(float Min, float Max)> meshRanges)
    {
        try
        {
            var text = tile.PlayerControl?.cosmetics?.nameText;
            if (text == null) return new { available = false };
            string currentText = text.text ?? "";
            bool matchesPlan = string.Equals(currentText, Read<string>(tile, "beamArt"), StringComparison.Ordinal);
            var info = text.textInfo; float min = float.MaxValue, max = float.MinValue, inkMin = float.MaxValue, inkMax = float.MinValue; int visible = 0;
            if (info != null && info.characterInfo != null && info.meshInfo != null)
                for (int i = 0; i < Math.Min(info.characterCount, Math.Min(info.characterInfo.Length, 16)); i++)
                {
                    var ch = info.characterInfo[i]; if (ch.character != '-' || !ch.isVisible) continue;
                    if (ch.materialReferenceIndex < 0 || ch.materialReferenceIndex >= info.meshInfo.Length) continue;
                    var vertices = info.meshInfo[ch.materialReferenceIndex].vertices;
                    if (vertices == null || ch.vertexIndex < 0 || ch.vertexIndex > vertices.Length - 4) continue;
                    float lo = float.MaxValue, hi = float.MinValue;
                    for (int v = 0; v < 4; v++) { float d = Vector2.Dot((Vector2)text.transform.TransformPoint(vertices[ch.vertexIndex + v]) - anchor, direction); lo = Mathf.Min(lo, d); hi = Mathf.Max(hi, d); }
                    meshRanges.Add((lo, hi)); min = Mathf.Min(min, lo); max = Mathf.Max(max, hi); visible++;
                    // Metric-derived ink bounds are estimates; shader pixels can differ.
                    var glyph = ch.textElement?.glyph;
                    if (glyph == null || !float.IsFinite(ch.scale) || ch.scale <= 0) continue;
                    var bl = (Vector2)vertices[ch.vertexIndex]; var tl = (Vector2)vertices[ch.vertexIndex + 1]; var tr = (Vector2)vertices[ch.vertexIndex + 2]; var br = (Vector2)vertices[ch.vertexIndex + 3];
                    var center = (bl + tl + tr + br) * .25f; var right = (tr + br - tl - bl) * .25f; var up = (tl + tr - bl - br) * .25f;
                    if (right.magnitude <= 0 || up.magnitude <= 0) continue;
                    right *= glyph.metrics.width * ch.scale / (2 * right.magnitude); up *= glyph.metrics.height * ch.scale / (2 * up.magnitude);
                    foreach (var p in new[] { center - right - up, center - right + up, center + right + up, center + right - up })
                    { float d = Vector2.Dot((Vector2)text.transform.TransformPoint(p) - anchor, direction); inkMin = Mathf.Min(inkMin, d); inkMax = Mathf.Max(inkMax, d); }
                }
            var renderer = text.GetComponent<MeshRenderer>();
            var renderers = new List<object>();
            foreach (var child in text.GetComponentsInChildren<MeshRenderer>(true).Take(4))
                renderers.Add(new { enabled = child.enabled, visible = child.isVisible, active = child.gameObject.activeInHierarchy,
                    bounds = BoundsValue(child.bounds), material = MaterialState(child.sharedMaterial) });
            var properties = new Dictionary<string, string>();
            foreach (string property in new[] { "enableAutoSizing", "fontSize", "fontSizeMin", "fontSizeMax", "overflowMode", "isTextOverflowing", "isTextTruncated", "maxVisibleCharacters", "maxVisibleLines", "firstVisibleCharacter", "pageToDisplay", "enableWordWrapping", "margin", "havePropertiesChanged" }) properties[property] = Property(text, property);
            return new { available = true, matchesPlannedText = matchesPlan, currentTextBytes = currentText.Length <= 4096 ? Encoding.UTF8.GetByteCount(currentText) : -1,
                active = text.gameObject.activeInHierarchy, enabled = text.enabled, layer = text.gameObject.layer,
                position = Vector(text.transform.position), scale = Vector(text.transform.lossyScale), rect = text.rectTransform.rect.ToString(),
                characterCount = info?.characterCount ?? -1, materialCount = info?.materialCount ?? -1, childRenderers = renderers,
                visibleDashCount = visible, meshMin = visible > 0 ? Number(min) : "unknown", meshMax = visible > 0 ? Number(max) : "unknown",
                estimatedInkMin = inkMin < float.MaxValue ? Number(inkMin) : "unknown", estimatedInkMax = inkMax > float.MinValue ? Number(inkMax) : "unknown", properties,
                renderer = renderer == null ? null : new { enabled = renderer.enabled, isVisible = renderer.isVisible, forceRenderingOff = renderer.forceRenderingOff, bounds = BoundsValue(renderer.bounds), material = MaterialState(renderer.sharedMaterial) } };
        }
        catch (Exception e) { return new { available = false, error = e.GetType().Name }; }
    }
    private static void Capture(Nova nova, BeamDistanceAudit.Shot shot, string stage, bool details)
    {
        var tiles = Read<List<NovaBeamTile>>(nova, "tiles") ?? new List<NovaBeamTile>();
        var anchor = Read<Vector2>(nova, "anchor"); float angle = Read<float>(nova, "angle"), reach = Read<float>(nova, "reach");
        var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)); var hitStart = anchor + direction * .5f; var hitEnd = anchor + direction * reach;
        bool beaming = Read<bool>(nova, "beaming"); int used = 0, glyphs = 0, committed = 0;
        var rows = new List<object>(); var ranges = new List<(float Min, float Max)>();
        for (int i = 0; i < Math.Min(tiles.Count, 17); i++)
        {
            var tile = tiles[i]; var style = Read<NovaBeamTile.Style>(tile, "style"); string art = Read<string>(tile, "beamArt"); bool beam = style == NovaBeamTile.Style.Beam;
            int bars = beam && art != null ? Glyphs.Matches(art).Count : 0; if (beam) { used++; glyphs += bars; if (tile.Committed) committed++; }
            try { rows.Add(new { index = i, purpose = i == 0 ? "star" : "beam_slot", style = style.ToString(), ready = tile.Ready, failed = tile.Failed,
                netId = tile.PlayerControl == null ? 0u : tile.PlayerControl.NetId, position = Vector(tile.Position), wanted = Vector(Read<Vector2>(tile, "wanted")), glyphs = bars,
                nameBytes = beam && art != null ? Encoding.UTF8.GetByteCount(art) : 0, perNameGlyphLimitReached = bars >= NovaBeamTile.RenderProfile.MaxGlyphsPerTile,
                text = details && beam ? TextState(tile, anchor, direction, ranges) : null }); }
            catch (Exception e) { rows.Add(new { index = i, error = e.GetType().Name }); }
        }
        var measured = BeamDistanceAudit.Coverage(ranges, .5f, reach);
        var render = Read<NovaBeamTile.RenderProfile>(nova, "render"); int capacity = Math.Max(0, tiles.Count - 1) * NovaBeamTile.RenderProfile.MaxGlyphsPerTile;
        Emit(shot.Id, stage, new { revision = Revision, shot = shot.Id, stage, mapId = Main.NormalOptions == null ? -1 : (int)Main.NormalOptions.MapId,
            owner = Read<byte>(nova, "ownerId"), poolTotal = tiles.Count, beamSlots = Math.Max(0, tiles.Count - 1), beamUsed = used, committedBeam = committed,
            unusedBeamSlots = Math.Max(0, tiles.Count - 1 - used), unusedReason = BeamDistanceAudit.UnusedReason(tiles.Count, used, beaming, tiles.All(t => t.Ready)),
            glyphs, glyphCapacity = capacity, maxPackedGlyphs = NovaBeamTile.RenderProfile.MaxPackedGlyphs, nameByteLimit = NetworkSafety.NameByteLimit,
            poolCapacityReached = used > 0 && used >= tiles.Count - 1, glyphCapacityReached = glyphs > 0 && glyphs >= Math.Min(capacity, NovaBeamTile.RenderProfile.MaxPackedGlyphs),
            beaming, beamVisible = Read<bool>(nova, "beamVisible"), anchor = Vector(anchor), angle = Number(angle), packedBar = render == null ? "unknown" : Number(render.PackedBarLength),
            plannedDrawStart = Vector(hitStart), plannedDrawEnd = Vector(hitEnd), hitStart = Vector(hitStart), hitEnd = Vector(hitEnd), reach = Number(reach),
            observedMeshMin = details ? Number(measured.Min) : "not_sampled", observedMeshMax = details ? Number(measured.Max) : "not_sampled", observedMeshGaps = details ? measured.Gaps : -1,
            observedMeshComplete = details && measured.Complete, cancelledReason = Read<string>(nova, "drawFailure"),
            worldCamera = details ? CameraState(Camera.main, hitStart, hitEnd) : null, hudCamera = details ? CameraState(HudManager.Instance?.UICamera, hitStart, hitEnd) : null,
            rows, warning = "Host mesh and font metrics are observations, not GPU pixels or remote Vanilla proof; no ForceMeshUpdate or state writes performed." });
    }
}
