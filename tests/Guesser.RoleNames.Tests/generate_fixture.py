"""Manual extraction of reviewed production parsing methods, not a replacement implementation."""
from pathlib import Path
import hashlib, json, re, os
folder=Path(__file__).resolve().parent
root=Path(os.environ.get('TOHS_GUESS_SOURCE_ROOT',folder.parents[1]))
utils=(root/'Modules/Utils/UtilsRoleInfo.cs').read_text(encoding='utf-8-sig')
guess=(root/'Modules/GuessManager.cs').read_text(encoding='utf-8-sig')
guesser=(root/'Roles/AddOns/Common/Buff/Guesser.cs').read_text(encoding='utf-8-sig')
core=(root/'Roles/Core/CustomRoleManager.cs').read_text(encoding='utf-8-sig')
# These adjacent ranges include the complete switch, kana table and normalization methods.
parts={
 'aliases':utils[utils.index('        private static string FixRoleNameInput('):utils.index('        static readonly Dictionary<string, string> KanaRomajiMap')],
 'lookup':utils[utils.index('        static readonly Dictionary<string, string> KanaRomajiMap'):utils.index('        private static void ConcatCommands(')],
 'target':guess[guess.index('    private static bool CheckTargetRoles('):guess.index('    public static TextMeshPro NameText(this PlayerControl')],
 'message':guess[guess.index('    private static bool MsgToPlayerAndRole('):guess.index('    public static string ChangeNormal2Vanilla(')],
 'display':guess[guess.index('    public static string ChangeNormal2Vanilla('):guess.index('    public static bool GuessCountImp(')],
 'blocked':guesser[guesser.index('    static readonly CustomRoles[] TellerRoles'):guesser.index('    public static void SetupCustomOption(')],
}
# Selection markers occur on member boundaries, so bodies remain exactly as compiled in production.
out='// Generated from reviewed production methods. Regenerate manually; hashes are checked at runtime.\nusing System.Text.RegularExpressions;\nusing static TownOfHost.Translator;\nnamespace TownOfHost {\npublic static class UtilsRoleInfo {\n'+parts['aliases']+parts['lookup']+'}\npublic static class GuessManager {\n'+parts['target']+parts['message']+parts['display']+'\npublic static bool Parse(string text,out byte id,out CustomRoles role,out string error)=>MsgToPlayerAndRole(text,out id,out role,out error);\npublic static bool Wrong(PlayerControl target,CustomRoles role)=>CheckTargetRoles(target,role);\n}\npublic static class TellerGate { public static Option TellerCannotGuess;\n'+parts['blocked']+'}\n}\n'
(folder/'GeneratedParsing.cs').write_text(out,encoding='utf-8')
start=core.index('public enum CustomRoles');end=core.index('}',core.index('{',start))+1
(folder/'GeneratedRoles.cs').write_text('namespace TownOfHost;\n'+core[start:end]+'\n',encoding='utf-8')
sources=['Modules/Utils/UtilsRoleInfo.cs','Modules/GuessManager.cs','Roles/AddOns/Common/Buff/Guesser.cs','Roles/Core/CustomRoleManager.cs','Resources/string.csv']
(folder/'FixtureHashes.json').write_text(json.dumps({p:hashlib.sha256((root/p).read_text(encoding='utf-8-sig').encode()).hexdigest().upper() for p in sources},indent=2),encoding='utf-8')
