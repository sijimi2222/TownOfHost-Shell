using System; using System.Collections.Generic; using System.Linq;
namespace TownOfHost { public static class RoleCommandParsing { public static bool TryParseCommand(string text, out string[] args)
    {
        args = (text ?? "").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0 || !args[0].StartsWith("/")) return false;
        if (args.Length > 0 && args[0].Equals("/cmd", System.StringComparison.OrdinalIgnoreCase)) args = args.Skip(1).ToArray();
        if (args.Length == 0) return false;
        var command = args[0].TrimStart('/').ToLowerInvariant();
        if (command is not ("mes" or "mm" or "en")) return false;
        args[0] = "/" + command;
        return true;
    } } public static class OptionsBroadcast { public static List<TestOptionsSender> AllSenders=new(); public static void SendAllGameOptions()
        {
            AllSenders.RemoveAll(s => !s.AmValid());
            foreach (var sender in AllSenders)
            {
                try
                {
                    if (sender.IsDirty) sender.SendGameOptions();
                }
                catch (System.Exception e)
                {
                    Logger.Exception(e, "SendAllGameOptions");
                }
                sender.IsDirty = false;
            }
        } } public sealed class TestOptionsSender { public bool IsDirty=true; public bool Valid=true; public int Calls; public bool Throw; public bool AmValid()=>Valid; public void SendGameOptions(){Calls++;if(Throw)throw new Exception("simulated serialization error");} } }