using System;
using System.IO;

namespace TownOfHost.Modules;

internal static class StatisticsStore
{
    // Never empty an existing store during startup. Legacy migration is non-destructive.
    internal static void EnsureExists(string path, string legacy)
    {
        if (File.Exists(path)) return;
        string parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        if (!string.IsNullOrEmpty(legacy) && File.Exists(legacy))
        {
            File.Copy(legacy, path, overwrite: false);
            return;
        }
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
    }
}
