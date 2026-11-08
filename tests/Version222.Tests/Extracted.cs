using System;
static class ThisAssembly {public static class Git {public const string Commit="same-commit",Branch="same-branch";}}
static class Main {public const string BepInExPluginVersion = "2.2.3";
public const string PluginVersion = "4.00.40.00";
public const string PluginShowVersion = "2.2.3";
public const string PluginReleaseVersion = "2.2.3";public static string VersionCheckTag => $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})/Shell-{BepInExPluginVersion}";public static readonly Version ShellVersion=Version.Parse(BepInExPluginVersion);public static Version version=Version.Parse(PluginVersion);public static string ForkId="Shell";public static Dictionary<byte,Peer> playerVersion=new();}
record Peer(Version version,string tag,string forkId);
static class Gate {public static bool MatchVersions(byte playerId, bool acceptVanilla = false)
            {
                if (!Main.playerVersion.TryGetValue(playerId, out var version)) return acceptVanilla;
                return Main.ForkId == version.forkId
                    && Main.version.CompareTo(version.version) == 0
                    && version.tag == Main.VersionCheckTag;
            }}