using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Neutral;

class Program
{
    static int passed, failed;
    static string Here([CallerFilePath] string file = "") => Path.GetDirectoryName(file);
    static string Root => Path.GetFullPath(Path.Combine(Here(), "../.."));
    static void Check(bool value, string message = "assertion") { if (!value) throw new Exception(message); }
    static void Test(string name, Action run)
    {
        PlayerCatch.Players.Clear(); TestEvents.Reset(); Utils.RoleSendList.Clear();
        NameColorManager.Colors.Clear(); TargetArrow.Links.Clear(); LateTask.Pending.Clear();
        SelfVoteManager.Active = false; StandMaster.EnableKillAbility = false; Opportunist.HasKillButton = false; Walkure.Reject = false;
        try { run(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + " " + e.Message); }
    }
    static PlayerControl P(byte id, CustomRoles role = CustomRoles.Crewmate, CustomRoleTypes faction = CustomRoleTypes.Neutral)
    { var p = new PlayerControl { PlayerId = id, Role = role, Faction = faction }; PlayerCatch.Players[id] = p; return p; }
    static Onmyoji N() { var n = new Onmyoji(P(1, CustomRoles.Onmyoji)); n.SetCreation(true); return n; }
    static string Extract(string source, string name)
    {
        var m = Regex.Match(source, @"^    (?:public override |public |private |static )*(?:bool|string|void) " + name + @"\(", RegexOptions.Multiline);
        Check(m.Success, "method missing " + name); int brace = source.IndexOf('{', m.Index + m.Length), depth = 0;
        for (int i = brace; i < source.Length; i++)
        { if (source[i] == '{') depth++; if (source[i] == '}' && --depth == 0) return source[m.Index..(i + 1)]; }
        throw new Exception("unbalanced method");
    }
    static readonly CustomRoles[] Added = {
        CustomRoles.Mermaid, CustomRoles.Hunter, CustomRoles.Tiger, CustomRoles.Monika, CustomRoles.Altair,
        CustomRoles.AntiHero, CustomRoles.Catalyst, CustomRoles.Conductor, CustomRoles.Fool, CustomRoles.HathorSekhmet,
        CustomRoles.JackalSeer, CustomRoles.Lupin, CustomRoles.LoversBreaker, CustomRoles.Missioneer,
        CustomRoles.Pharaoh, CustomRoles.RaccoonChild, CustomRoles.RaccoonParent
    };
    static void Main()
    {
        var source = File.ReadAllText(Path.Combine(Root, "Roles/Neutral/Onmyoji.cs")).Replace("\r\n", "\n");
        var fixture = File.ReadAllText(Path.Combine(Here(), "GeneratedTargeting.cs")).Replace("\r\n", "\n");
        var hashes = JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Here(), "FixtureHashes.json")));
        foreach (var pair in hashes) Test("compiled method equals production " + pair.Key, () => {
            var body = Extract(source, pair.Key); Check(body == Extract(fixture, pair.Key));
            Check(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))) == pair.Value);
        });
        Test("numeric role enum matches current production", () => {
            var text = File.ReadAllText(Path.Combine(Root, "Roles/Core/CustomRoleManager.cs")).Replace("\r\n", "\n");
            int first = text.IndexOf("public enum CustomRoles"), end = text.IndexOf('}', text.IndexOf('{', first));
            Check(File.ReadAllText(Path.Combine(Here(), "GeneratedRoles.cs")).Replace("\r\n", "\n").Contains(text[first..(end + 1)]));
        });
        Test("Victim fixture uses the same awakening criterion", () => {
            var victim = File.ReadAllText(Path.Combine(Root, "Roles/Neutral/Victim.cs"));
            Check(victim.Contains("public bool IsAwakened => mode == VictimMode.Awakened;"));
            Check(victim.Contains("public bool CanKill => mode == VictimMode.Awakened || performingForcedKill;"));
        });
        foreach (var role in Added)
        {
            Test("added killer and star " + role, () => { var n = N(); var p = P(2, role); Check(n.Killer(p) && n.Star(p) && !n.Valid(p)); Check(n.GetMark(n.Player, p).Contains("★")); });
            Test("added role cannot be nominated " + role, () => { var n = N(); n.SetNomination(true); var p = P(2, role); var info = new MurderInfo { AttemptKiller = n.Player, AttemptTarget = p }; n.OnCheckMurderAsKiller(info); Check(!info.DoKill && TestEvents.Conversions == 0 && p.Role == role); });
            Test("added role rejected by vote candidate " + role, () => { var n = N(); var p = P(2, role); Check(!n.CheckVoteAsVoter(1, n.Player)); Check(!n.CheckVoteAsVoter(2, n.Player)); Check(n.NextShikigamiCandidate == 255 && TestEvents.Conversions == 0); });
            Test("dead added role not marked or nominated " + role, () => { var n = N(); var p = P(2, role); p.Alive = false; Check(!n.Valid(p) && !n.Star(p) && n.GetMark(n.Player, p) == ""); });
            Test("upstream classification ignores current kill enable flag " + role, () => { var n = N(); var p = P(2, role); p.KillEnabled = false; Check(n.Killer(p)); });
        }
        foreach (var role in new[] { CustomRoles.CountKiller, CustomRoles.Jackal, CustomRoles.JackalHadouHo, CustomRoles.JackalMafia,
            CustomRoles.JackalAlien, CustomRoles.DoppelGanger, CustomRoles.GrimReaper, CustomRoles.Remotekiller, CustomRoles.Egoist,
            CustomRoles.Eater, CustomRoles.PavlovDog, CustomRoles.Sheriff, CustomRoles.SwitchSheriff, CustomRoles.WolfBoy,
            CustomRoles.JackalWolf, CustomRoles.Stand, CustomRoles.Ogre })
            Test("existing killer classification preserved " + role, () => { var n = N(); var p = P(2, role); Check(n.Killer(p) && n.Star(p) && !n.Valid(p)); });
        foreach (var role in new[] { CustomRoles.Impostor, CustomRoles.Nova, CustomRoles.Destroyer })
            Test("impostor faction including protected roles " + role, () => { var n = N(); var p = P(2, role, CustomRoleTypes.Impostor); Check(n.Killer(p) && !n.Valid(p)); });
        foreach (bool enabled in new[] { false, true })
        {
            Test("StandMaster option " + enabled, () => { var n = N(); StandMaster.EnableKillAbility = enabled; var p = P(2, CustomRoles.StandMaster); Check(n.Killer(p) == enabled && n.Valid(p) != enabled); });
            Test("Opportunist option " + enabled, () => { var n = N(); Opportunist.HasKillButton = enabled; var p = P(2, CustomRoles.Opportunist); Check(n.Killer(p) == enabled && n.Valid(p) != enabled); });
        }
        foreach (bool awake in new[] { false, true }) foreach (bool forced in new[] { false, true })
            Test($"Victim awake={awake} forced={forced}", () => { var n = N(); var p = P(2, CustomRoles.Victim); p.State = new Victim { IsAwakened = awake, PerformingForcedKill = forced }; Check(n.Killer(p) == awake && n.Valid(p) != awake && n.Star(p) == awake); });
        Test("Victim crewmate mode stays unmarked", () => { var n = N(); var p = P(2, CustomRoles.Victim); p.State = new Victim { IsAwakened = false }; Check(!n.Killer(p) && n.Valid(p)); });
        Test("Strawdoll excluded and may be converted", () => { var n = N(); var p = P(2, CustomRoles.Strawdoll); Check(!n.Killer(p) && n.Valid(p) && !n.Star(p)); n.SetNomination(true); n.OnCheckMurderAsKiller(new() { AttemptKiller = n.Player, AttemptTarget = p }); LateTask.Drain(); Check(p.Role == CustomRoles.Shikigami && ((Shikigami)p.State).Owner == 1); });
        Test("Eliminator remains outside upstream whitelist", () => { var n = N(); var p = P(2, CustomRoles.Eliminator); Check(!n.Killer(p) && n.Valid(p)); });
        Test("null target rejected by every gate", () => { var n = N(); Check(!n.Killer(null) && !n.Valid(null) && !n.Star(null)); });
        Test("self never becomes Shikigami", () => { var n = N(); Check(!n.Valid(n.Player)); });
        Test("PavlovOwner exception unchanged", () => { var n = N(); var p = P(2, CustomRoles.PavlovOwner); Check(n.Valid(p) && !n.Star(p)); });
        Test("ordinary crew conversion and delayed owner assignment", () => { var n = N(); var p = P(2); n.SetNomination(true); n.OnCheckMurderAsKiller(new() { AttemptKiller = n.Player, AttemptTarget = p }); Check(p.Role == CustomRoles.Shikigami && n.ShikigamiIds.SequenceEqual(new byte[] { 2 })); LateTask.Drain(); Check(((Shikigami)p.State).Owner == 1 && TestEvents.Conversions == 1); });
        Test("second conversion cannot consume another player", () => { var n = N(); n.SetNomination(true); var a = P(2); var b = P(3); n.OnCheckMurderAsKiller(new() { AttemptKiller = n.Player, AttemptTarget = a }); n.SetNomination(true); n.OnCheckMurderAsKiller(new() { AttemptKiller = n.Player, AttemptTarget = b }); Check(TestEvents.Conversions == 1 && b.Role == CustomRoles.Crewmate); });
        Test("disabled creation never converts", () => { var n = N(); n.SetCreation(false); var p = P(2); Check(n.CheckVoteAsVoter(2, n.Player)); Check(TestEvents.Conversions == 0); });
        Test("normal vote and skip mode still work", () => { var n = N(); P(2); Check(!n.CheckVoteAsVoter(1, n.Player)); Check(!n.CheckVoteAsVoter(2, n.Player) && n.NextShikigamiCandidate == 2); Check(!n.CheckVoteAsVoter(1, n.Player)); Check(!n.CheckVoteAsVoter(253, n.Player) && n.NextShikigamiCandidate == 255); });
        Test("other player's vote is untouched", () => { var n = N(); var p = P(2); Check(n.CheckVoteAsVoter(1, p)); });
        Test("Walkure veto still protects conversion", () => { var n = N(); var p = P(2); n.SetNomination(true); Walkure.Reject = true; n.OnCheckMurderAsKiller(new() { AttemptKiller = n.Player, AttemptTarget = p }); Check(p.Role == CustomRoles.Crewmate && TestEvents.Conversions == 0); });
        Test("star refresh updates dynamic Victim without stale color", () => { var n = N(); var p = P(2, CustomRoles.Victim); var victim = new Victim(); p.State = victim; n.Refresh(); Check(!NameColorManager.Colors.ContainsKey((1,2))); victim.IsAwakened = true; n.Refresh(); Check(NameColorManager.Colors.ContainsKey((1,2))); victim.IsAwakened = false; n.Refresh(); Check(!NameColorManager.Colors.ContainsKey((1,2))); });
        Test("independent Onmyoji nomination lists", () => { var a = N(); var b = new Onmyoji(P(3, CustomRoles.Onmyoji)); var p = P(2); a.SetNomination(true); a.OnCheckMurderAsKiller(new() { AttemptKiller = a.Player, AttemptTarget = p }); Check(a.ShikigamiIds.Count == 1 && b.ShikigamiIds.Count == 0); });
        Console.WriteLine($"RESULT {passed} passed, {failed} failed (compiled production targeting slices; synthetic game state)");
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }
}