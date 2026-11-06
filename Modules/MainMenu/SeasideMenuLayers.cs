using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using BepInEx;
using UnityEngine;

namespace TownOfHost.Modules.MainMenu;

internal sealed record SeasideLayerButton(string Id, SeasideRect ImageRect, Sprite Sprite);
internal sealed class SeasideMenuLayers
{
    internal const string ResourcePrefix = "TownOfHost.Resources.MainMenu.Layers.";
    internal Sprite Background { get; }
    internal IReadOnlyDictionary<string, SeasideLayerButton> Buttons { get; }
    static bool warned;
    SeasideMenuLayers(Sprite background, Dictionary<string, SeasideLayerButton> buttons)
        { Background = background; Buttons = buttons; }
    internal static SeasideMenuLayers TryLoad()
    {
        // Existing artist-provided flat replacements must never have another set of buttons laid over them.
        if (File.Exists(Path.Combine(Paths.ConfigPath, "TOH-Shell", "MainMenu", "background.png"))) return null;
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + "manifest.json");
            if (stream == null || stream.Length > 256 * 1024) return null;
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            if (root.GetProperty("canvas").GetProperty("width").GetInt32() != 1672
                || root.GetProperty("canvas").GetProperty("height").GetInt32() != 941) return null;
            var background = UtilsSprite.LoadSprite(ResourcePrefix + "background.png", 100);
            if (background == null || background.texture.width != 1672 || background.texture.height != 941) return null;
            var buttons = new Dictionary<string, SeasideLayerButton>(StringComparer.Ordinal);
            foreach (var item in root.GetProperty("buttons").EnumerateArray())
            {
                string id = item.GetProperty("id").GetString();
                var target = SeasideMenuLayout.Targets.Single(t => t.Id == id);
                var image = ReadRect(item.GetProperty("image_rect"));
                if (ReadRect(item.GetProperty("hit_rect")) != target.Rect || image.Width <= 0 || image.Height <= 0
                    || image.X < 0 || image.Y < 0 || image.X + image.Width > 1672 || image.Y + image.Height > 941)
                    throw new InvalidDataException();
                string file = item.GetProperty("file").GetString();
                if (string.IsNullOrEmpty(file) || !file.StartsWith("buttons/", StringComparison.Ordinal)
                    || file.Contains("..") || file.Contains('\\') || !file.EndsWith(".png", StringComparison.Ordinal))
                    throw new InvalidDataException();
                var sprite = UtilsSprite.LoadSprite(ResourcePrefix + file.Replace('/', '.'), 100);
                if (sprite == null || sprite.texture.width != image.Width || sprite.texture.height != image.Height)
                    throw new InvalidDataException();
                buttons.Add(id, new(id, image, sprite));
            }
            if (buttons.Count != 14 || SeasideMenuLayout.Targets.Any(t => !buttons.ContainsKey(t.Id))) throw new InvalidDataException();
            return new(background, buttons);
        }
        catch
        {
            if (!warned) { warned = true; Logger.Warn("SEASIDE_LAYERS_UNAVAILABLE_USING_STATIC_ARTWORK", "SeasideMenu"); }
            return null;
        }
    }
    static SeasideRect ReadRect(JsonElement r) => new(r.GetProperty("x").GetInt32(), r.GetProperty("y").GetInt32(),
        r.GetProperty("width").GetInt32(), r.GetProperty("height").GetInt32());
}
