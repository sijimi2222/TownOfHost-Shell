using System.Text;
using TownOfHost.Roles.Core;
using static TownOfHost.Utils;
using static TownOfHost.Translator;
using static TownOfHost.UtilsRoleText;
namespace TownOfHost.Roles.Core {
public enum CustomRoles

{//Default

    Crewmate = 0,

    //Impostor(Vanilla)

    Impostor,

    Shapeshifter,

    Phantom,

    Viper,

    //Impostor

    Destroyer,

    BountyHunter,

    FireWorks,

    Mafia,

    SerialKiller,

    ShapeMaster,

    Sniper,

    Vampire,

    Witch,

    Warlock,

    Mare,

    Penguin,

    Puppeteer,

    TimeThief,

    EvilTracker,

    Stealth,

    NekoKabocha,

    EvilHacker,

    Insider,

    HadouHo,

    SelfBomber,

    Professional,

    DoubleKiller,

    Slugger,

    Chaser,

    Samurai,

    Rocket,

    EvilMoving,

    Teleporter,

    EvilTrapper,

    Minimalist,

    Evolver,

    Conjurer,

    Swooper,

    EvilCommander,

    Spider,

    //TOH-K

    Bomber,

    TeleportKiller,

    AntiReporter,

    Tairou,

    Evilgambler,

    Notifier,

    Magician,

    Decrescendo,

    Curser,

    Alien,

    AlienHijack,

    SpeedStar,

    EvilTeller,

    Limiter,

    ProgressKiller,

    Mole,

    EvilAddoer,

    Reloader,

    Jumper,

    EarnestWolf,

    Amnesiac,

    Camouflager,

    ConnectSaver,

    EvilSatellite,

    ProBowler,

    EvilMaker,

    Eraser,

    QuickKiller,

    CharismaStar,

    Ballooner,

    BorderKiller,

    ShapeKiller,

    Archer,

    Assassin,

    UnFortuner,

    //TOH-P

    EvilGuesser,

    TimeSleeper,

    EvilBlender,

    DummySpawner,

    Reverser,

    EvilSanta,

    EvilLinker,

    MassMurder,

    EvilStandMaster,

    BeginnerImpostor,

    VillainWolf,

    BlackVisioner,

    //DEBUG only Impostor

    //Madmate

    MadGuardian,

    Madmate,

    MadSnitch,

    MadAvenger,

    SKMadmate,

    //TOH-K

    MadJester,

    MadTeller,

    MadBait,

    MadReduced,

    MadWorker,

    MadTracker,

    MadChanger,

    MadSuicide,

    MadBetrayer,

    Nue,

    MadCount,

    MadHacker,

    MadSheriff,
    AddLight,
    AntiTeleporter,
    VentBeginner,
    Ventoman,
    Madpsycho,

    BlackCat,

    BlackMadMate,

    MadKaiser,

    MadWare,

    //DEBUG only Madmate

    //Crewmate(Vanilla)

    Engineer,

    Scientist,

    Tracker,

    Noisemaker,

    Detective,

    // 公式ジャッジ
    Judge,

    //Crewmate

    Bait,

    Lighter,

    Mayor,

    Arcana,

    President,

    Guardian,

    Typewriter,

    SabotageMaster,

    Sheriff,

    Snitch,

    SpeedBooster,

    Trapper,

    Dictator,

    Doctor,

    Seer,

    TimeManager,

    VillageChief,

    Santa,

    Rabbit,

    Nimrod,

    Sage,

    NiceTrapper,

    Moving,

    Astel,

    SatsumatoImo,

    SatsumatoImoC,

    SatsumatoImoM,

    NiceTeleporter,

    NiceRedRidingHood,

    MagicalGirl,

    Walkure,

    SuspiciousTeller,

    NiceNekomata,

    //TOH-K

    Gasp,

    VentMaster,

    ToiletFan,

    Bakery,

    FortuneTeller,

    TaskStar,

    PonkotuTeller,

    UltraStar,

    MeetingSheriff,

    GuardMaster,

    Balancer,

    ShrineMaiden,

    Comebacker,

    WhiteHacker,

    WolfBoy,

    NiceAddoer,

    InSender,

    Staff,

    Efficient,

    Psychic,

    SwitchSheriff,

    NiceLogger,

    NiceEraser,

    Android,

    King,

    AmateurTeller,

    Cakeshop,

    Snowman,

    Stolener,

    VentOpener,

    VentHunter,

    Walker,

    CandleLighter,

    Express,

    Inspector,

    Fortuner,

    AllArounder,

    Observer,

    Satellite,

    Merlin,

    //TOH-P

    NiceGuesser,

    Pukupuku,

    Medium,

    SheriffHadouHo,

    Hitchhiker,

    NiceLinker,

    StepDancer,

    Apprentice,

    Milkman,

    Police,

    NiceWorkaholic,

    Jailer,

    LegendStar,

    Nautilus,

    //TOH-Shell
    Lethe,

    //DEBUG only Crewmate

    Analyzer,

    //Neutral

    Arsonist,

    Egoist,

    Jester,

    FakeJester,

    AntiHero,

    Pharaoh,

    Opportunist,

    PlagueDoctor,

    SchrodingerCat,

    Terrorist,

    Executioner,

    Jackal,
    Hunter,

    DarkSheriff,

    RaccoonParent,

    RaccoonChild,

    JackalHadouHo,

    JackalSeer,

    Tama,

    Freeter,

    God,

    Tuna,

    Onmyoji,

    Zombie,

    Shikigami,

    Cupid,

    Oblivion,

    Eater,

    Autocrat,

    Spelunker,

    PavlovDog,

    PavlovOwner,

    PavlovDogImprint,

    Moira,

    PoisonedBakery,

    Monika,

    LoversBreaker,

    Suicider,

    PokerFace,

    Ogre,

    Lawyer,

    Pursuer,

    Duelist,

    Archenemy,

    Pirate,

    Gang,

    TheFirstLittlePig,

    TheSecondLittlePig,

    TheThirdLittlePig,

    //TOH-K

    Remotekiller,

    Chef,

    JackalMafia,

    CountKiller,

    GrimReaper,

    Madonna,

    Jackaldoll,

    Workaholic,

    Monochromer,

    DoppelGanger,

    MassMedia,

    Chameleon,

    Banker,

    BakeCat,

    Emptiness,

    JackalAlien,

    CurseMaker,

    PhantomThief,

    Fox,

    Turncoat,

    Vulture,

    SantaClaus,

    Missioneer,

    Strawdoll,

    Fool,

    JackalWolf,

    TaskPlayerB,

    //TOH-P

    StandMaster,

    Chatter,

    BatGirl,

    Shyboy,

    SmokeMaker,

    Stand,

    Villain,

    Scratcher,

    HappyJester,

    Connector,

    Sleeper,

    Null,

    UnHappyJester,

    //DEBUG only Neutral.

    //HideAndSeek

    HASFox,

    HASTroll,

    MMArcher,

    DummyHunterPlayer,

    //GM

    GM,



        //Combination

    Driver,

    Braid,

    Vega,

    Altair,

    Amateras,

    Ruler,

    Abuser,

    Victim,

    // リバーサルはクルー役職として扱う。組み合わせ役職の既存IDを変えず、

    // 属性の開始番号(500)直前にある未使用の499番を使用する。

    // Shell preserves existing role IDs; upstream Influencer=286 conflicts with StandMaster.
    Influencer = 496,

    // hamo beta additions: retain Shell IDs used by saved settings/RPCs.
    PuppeteerHadouHo = 312,
    Whiteshark = 313,
    HateKiller = 314,
    BlackSanta = 315,
    Hyde = 316,
    BoostLighter = 317,
    Jizo = 318,
    Fanatic = 319,
    Jekyll = 320,
    JekyllandHyde = 321,
    Dancer = 322,
    Dracula = 323,
    Mermaid = 324,
    kenzoku = 575,
    BountyGuesser = 576,
    Resulter = 325,
    Corexis = 326,
    Muter = 327,
    MadScream = 328,
    Maestro = 329,
    Tomorrow = 330,
    Repeater = 331,
    MadParasite = 332,
    Infect = 333,
    Infected = 334,
    HathorSekhmet = 335,
    Surrogate = 336,
    MadProxy = 337,
    Inheritance = 577,
    Survivor = 338,
    Mario = 339,
    Obolus = 340,
    HydeImp = 341,
    DarkHide = 342,
    Greedier = 343,
    Administer = 344,
    Tuner = 345,
    MadCleaner = 346,
    Chief = 347,
    Streamer = 348,
    Prankster = 349,
    // hamo v4.35 additions: preserve all previously published Shell IDs.
    Trauma = 350,
    Surmount = 351,
    Messenger = 352,
    Fugitive = 353,
    MemoryRestorer = 354,
    ArchVillain = 355,
    LonelyWolf = 356,
    Clockworker = 357,
    Abductor = 358,
    MadInvisible = 359,
    MadMagic = 360,
    Flatfish = 361,
    Lupin = 362,
    Daoshi = 363,
    Catalyst = 364,
    Conductor = 365,
    Sender = 366,
    MadSender = 367,
    Parity = 368,
    Experiencer = 369,
        StepBomber = 370,
        Locksmith = 371,
        Coroner = 372,
    Damocles = 373,
    Idol = 374,
    Marshall = 375,
    LessThan = 376,
    Eliminator = 382,
    // Added without changing any existing Shell role/add-on IDs.
    MadKind = 377,
    Framer = 378,
    NiceHacker = 379,
    Romeo = 380,
    Juliet = 381,
    Nova = 588,
    Arsene = 578,
    Kyonshi = 579,
    Surviving = 7010,
    Berserker = 7011,
    ToughGuy = 7012,
    Comorbidity = 7013,
    Murder = 7014,
    BetrayGuesser = 7015,

    NDPlayer = 497,

    Tiger = 498,

    Reversal = 499,

    // Sub-roll after 500

    NotAssigned = 500,

    LastImpostor,

    LastNeutral,

    Workhorse,

    Twins,

    OneWolf,

    Chain,

    //第三属性

    Lovers, RedLovers, YellowLovers, BlueLovers, GreenLovers, WhiteLovers, PurpleLovers,

    MadonnaLovers, CupidLovers, OneLove, Amanojaku, Faction,

    LastCrewmate,

    //AddMadmate,

    //バフ

    Guesser,

    Serial,

    Connecting,

    Watching,

    PlusVote,

    Tiebreaker,

    Autopsy,

    Revenger,

    Speeding,

    Damudo,

    Management,

    Opener,

    Powerful,

    Stack,

    //AntiTeleporter,

    Seeing,

    Lighting,

    Moon,

    Guarding,

    MagicHand,

    Absorb,

    Symbol,

    VoteTracker,

    //デバフ

    Surrender,

    Amnesia,

    Notvoter,

    Elector,

    NonReport,

    Transparent,

    Water,

    Clumsy,

    Slacker,

    SlowStarter,

    InfoPoor,

    News,

    Sunglasses,

    Stamina,

    Jumbo,

    Reporting,



    //GhostRoles



    //MadmateGhost



    DemonicCrusher,

    DemonicTracker,

    DemonicVenter,

    DemonicSupporter,

    //CrewMateGhost

    Ghostbuttoner,

    GhostNoiseSender,

    GhostReseter,

    GuardianAngel,

    GhostRumour,

    //Pko

    GhostFloodlight,

    GhostSaboteur,

    //NeutralGhost

    AsistingAngel,

    Securer,

    Sealer,

    Triplets,

    SilverBuzzer,

    // 氷鬼(IceOni)は要望により役職として削除済み(実装ファイルIceOni.csを削除)。

    // enumの並び(数値)がズレるとセーブデータ等に影響するため、値自体はここに残している。

    IceOni,

   
}
public enum CustomRoleTypes

{

    Crewmate,

    Impostor,

    Neutral,

    Madmate

}
}
namespace TownOfHost {
public enum CountTypes

    {

        OutOfGame,

        None,

        Crew,

        Impostor,

        Jackal,

        Hunter,

        Remotekiller,

        TaskPlayer,

        GrimReaper,

        Fox,

        MilkyWay,

        Pavlov,

        Eater,

        Victim,

        Monika,

        StandMaster,

        Villain,

        Daoshi

    }
public enum CombinationRoles

    {

        None,

        AssassinandMerlin,

        DriverandBraid,

        FoolandNue,

        VegaandAltair,

        AbuserandVictim,

        PokerFace,

        TheThreeLittlePigs,
        CatalystandConductor,
        TraumaandSurmount
    }
public enum TabGroup

    {

        MainSettings,

        ImpostorRoles,

        MadmateRoles,

        CrewmateRoles,

        NeutralRoles,

        Combinations,

        Addons,

        GhostRoles,

        // ===== TOWNOFHOST-SHELL SETTING配下のGAME/OTHERページ =====

        // MainSettingsタブに雑多に並んでいた「ゲームモード別設定/タスク勝利無効化」と

        // 「コマンド禁止/自動機能・タスクターン中チャット表示/BAN・KICK関連」を、

        // 役職タブと同じ仕組みの専用ページに独立させるために追加。

        // 末尾に追加しているため、既存のTabGroup値のインデックスには影響しない。

        Game,

        Other,

        // ===== OTHER2 =====

        // 元々「Other」ページ内に見出しラベルだけ"Other2"と表示させていた区画

        // (コマンド禁止/自動機能/BAN・KICK関連)を、実際に独立したタブとして分離。

        Other2

    }
public enum OptionFormat

    {

        None,

        Players,

        Seconds,

        Percent,

        Times,

        /// <summary>試合</summary>

        Games,

        /// <summary>x</summary>

        Multiplier,

        Votes,

        Pieces,

        day,

        Set,

        Turns

    }
public enum CustomGameMode

    {

        Standard, //= 0x01,

        HideAndSeek, //= 0x02,

        TaskBattle, //= 0x03,

        StandardHAS,//= 0x04

        SuddenDeath,//= 0x05

        MurderMystery,//= 0x06

        DummyBattleRoyale,//= 0x07

        DummyHunter,//= 0x08

        ShuffleRole,//= 0x09
        NaturalDisasters,
        Kedoro,

        All = int.MaxValue

    }
public enum CustomOptionTags

    {

        Standard,//スタンダードモード

        HideAndSeek,//かくれんぼモード

        TaskBattle,//タスクバトルMode

        SuddenDeath,//サドンデス(Sta)

        MurderMystery,

        DummyBattleRoyale,

        DummyHunter,

        ShuffleRole,

        StandardHAS,//役職入りかくれんぼ(Sta)

        Role,//役職設定

        GameOption,//ゲーム設定

        OtherOption,//その他の設定
        NaturalDisasters,
        Kedoro,



        All = int.MaxValue,

    }
public static class UtilsOption {
        public static string GetFrom(SimpleRoleInfo info, bool isblock = true) => GetFrom(info.From, info.RoleName, isblock);

        public static string GetFrom(From from, CustomRoles role = CustomRoles.NotAssigned, bool isblock = true)

        {

            string Fromtext = $"<#{(isblock ? "000000" : "ffffff")}>From:</color>";

            from = ShellRoleOrigins.Resolve(role, from);

            switch (from)

            {

                case From.None: Fromtext = ""; break;

                case From.AmongUs: Fromtext += "<#ff1919>Among Us</color>"; break;

                case From.TheOtherRoles: Fromtext += $"<#ff0000>TheOtherRoles</color>"; break;

                case From.TOR_GM_Edition: Fromtext += $"<#ff0000>TOR GM Edition</color>"; break;

                case From.TOR_GM_Haoming_Edition: Fromtext += $"<#ff0000>TOR GM Haoming</color>"; break;

                case From.SuperNewRoles: Fromtext += "<#ffa500>Super</color><#ff0000>New</color><#00ff00>Roles</color>"; break;

                case From.ExtremeRoles: Fromtext += $"<#d3d3d3>{from}</color>"; break;

                case From.NebulaontheShip: Fromtext += $"<#191970>{from}</color>"; break;

                case From.au_libhalt_net: Fromtext += $"<#ffc0cb>au libhalt net</color>"; break;

                case From.FoolersMod: Fromtext += $"<#006400>{from}</color>"; break;

                case From.SheriffMod: Fromtext += $"<#f8cd46>{from}</color>"; break;

                case From.Jester: Fromtext += $"<#ec62a5>{from}</color>"; break;

                case From.TownOfUs: Fromtext += $"<#daa520>{from}</color>"; break;

                case From.TownOfHost: Fromtext += $"<#00bfff>{from}</color>"; break;

                case From.TownOfHost_Y: Fromtext += $"<#dddd00>TownOfHost_Y</color>"; break;

                case From.TownOfHost_K: Fromtext += $"<#00bfff>TownOfHost_K</color>"; break;

                case From.TownOfHost_for_E: Fromtext += $"<#18e744>TownOfHost for E</color>"; break;

                case From.Speyrp: Fromtext = $"<#7fffbf>From:Yoran★</color>"; break;

                case From.TownOfHost_Enhanced: Fromtext += $"<#ffc0cb>TownOfHost Enhanced</color>"; break;

                case From.TownOfHost_Pko: Fromtext += $"<#FF9631>TownOfHost Pko</color>"; break;

                case From.RevolutionaryHostRoles: Fromtext += $"<#3cb371>RevolutionaryHostRoles</color>"; break;

                case From.Love_Couple_Mod: Fromtext += "<#ff6be4>Love Couple Mod</color>"; break;

                case From.TownOfHost_hamo: Fromtext += $"<{ShellRoleOrigins.HamoTextColor}>{(ShellRoleOrigins.HasOverride(role) ? ShellRoleOrigins.HamoDisplayName : "TownOfHost_hamo")}</color>"; break;

                case From.TownOfHost_Temp: Fromtext += $"<{ShellRoleOrigins.TempTextColor}>{ShellRoleOrigins.TempDisplayName}</color>"; break;

                case From.TownOfHost_Shell: Fromtext += $"<#aa00ff>{(role == CustomRoles.Tiger ? "Town Of Host Shell" : "TownOfHost_Shell")}</color>"; break;

            }

            if (role is CustomRoles.BoostLighter) Fromtext += "  <#000000>(<#eee5be>ライター</color>)</color>";

            if (role is CustomRoles.MadSuicide) Fromtext += "  <#000000>(<#ff1919>崇拝者</color>)</color>";

            return Fromtext;

        }

}
public static class BaselineUtilsOption {
public static string GetFrom(From from, CustomRoles role = CustomRoles.NotAssigned, bool isblock = true)

        {

            string Fromtext = $"<#{(isblock ? "000000" : "ffffff")}>From:</color>";

            switch (from)

            {

                case From.None: Fromtext = ""; break;

                case From.AmongUs: Fromtext += "<#ff1919>Among Us</color>"; break;

                case From.TheOtherRoles: Fromtext += $"<#ff0000>TheOtherRoles</color>"; break;

                case From.TOR_GM_Edition: Fromtext += $"<#ff0000>TOR GM Edition</color>"; break;

                case From.TOR_GM_Haoming_Edition: Fromtext += $"<#ff0000>TOR GM Haoming</color>"; break;

                case From.SuperNewRoles: Fromtext += "<#ffa500>Super</color><#ff0000>New</color><#00ff00>Roles</color>"; break;

                case From.ExtremeRoles: Fromtext += $"<#d3d3d3>{from}</color>"; break;

                case From.NebulaontheShip: Fromtext += $"<#191970>{from}</color>"; break;

                case From.au_libhalt_net: Fromtext += $"<#ffc0cb>au libhalt net</color>"; break;

                case From.FoolersMod: Fromtext += $"<#006400>{from}</color>"; break;

                case From.SheriffMod: Fromtext += $"<#f8cd46>{from}</color>"; break;

                case From.Jester: Fromtext += $"<#ec62a5>{from}</color>"; break;

                case From.TownOfUs: Fromtext += $"<#daa520>{from}</color>"; break;

                case From.TownOfHost: Fromtext += $"<#00bfff>{from}</color>"; break;

                case From.TownOfHost_Y: Fromtext += $"<#dddd00>TownOfHost_Y</color>"; break;

                case From.TownOfHost_K: Fromtext += $"<#00bfff>TownOfHost_K</color>"; break;

                case From.TownOfHost_for_E: Fromtext += $"<#18e744>TownOfHost for E</color>"; break;

                case From.Speyrp: Fromtext = $"<#7fffbf>From:Yoran★</color>"; break;

                case From.TownOfHost_Enhanced: Fromtext += $"<#ffc0cb>TownOfHost Enhanced</color>"; break;

                case From.TownOfHost_Pko: Fromtext += $"<#FF9631>TownOfHost Pko</color>"; break;

                case From.RevolutionaryHostRoles: Fromtext += $"<#3cb371>RevolutionaryHostRoles</color>"; break;

                case From.Love_Couple_Mod: Fromtext += "<#ff6be4>Love Couple Mod</color>"; break;

                case From.TownOfHost_hamo: Fromtext += $"<#fb85ff>TownOfHost_hamo</color>"; break;

                case From.TownOfHost_Shell: Fromtext += $"<#aa00ff>{(role == CustomRoles.Tiger ? "Town Of Host Shell" : "TownOfHost_Shell")}</color>"; break;

            }

            if (role is CustomRoles.BoostLighter) Fromtext += "  <#000000>(<#eee5be>ライター</color>)</color>";

            if (role is CustomRoles.MadSuicide) Fromtext += "  <#000000>(<#ff1919>崇拝者</color>)</color>";

            return Fromtext;

        }
}
public static class UtilsShowOption {
public static void ShowChildrenSettings(OptionItem o,ref StringBuilder b,bool Askesu=false,PlayerControl pc=null){}
public static string AddonInfo(CustomRoles role, string Mark = "", From from = From.None, PlayerControl pc = null)

        {

            var html = "\n<size=90%><line-height=1.8pic>";

            var fromRole = ShellRoleOrigins.HasOverride(role) ? role : CustomRoles.NotAssigned;
            var Fromtext = $"\n{UtilsOption.GetFrom(from, fromRole)}\n";

            var builder = new StringBuilder();

            var text = "";

            if (Mark != "") Mark = $" {Mark}";

            if (Options.CustomRoleSpawnChances.TryGetValue(role, out var op)) ShowChildrenSettings(op, ref builder, Askesu: true, pc: pc);

            if (builder.ToString().RemoveHtmlTags() != "") text = $"\n\n<size=45%>{builder}";

            if (UtilsOption.GetFrom(from, fromRole).RemoveHtmlTags() == "") Fromtext = "";



            var info = ColorString(GetRoleColor(role), GetString($"{role}") + Mark + html + GetString($"{role}Info"));

            if (Mark == "") info = $"<b>{info}</b>";



            return info + $"\n</b></color><size=57%>{Fromtext}<line-height=1.3pic>" + GetString($"{role}InfoLong") + text;

        }
public static string GetAddonsHelp(CustomRoles role)

        {

            if (!(role.IsAddOn() || role.IsGhostRole() || role.IsLovers() || role is CustomRoles.Amanojaku || role is CustomRoles.Twins || role is CustomRoles.Triplets)) return "";

            var text = "";

            var juncture = "<line-height=2.0pic><size=150%>";



            return (text += juncture) + (role switch

            {

                CustomRoles.Twins => AddonInfo(role, ""),

                CustomRoles.Triplets => AddonInfo(role, ""),

                //バフ

                CustomRoles.Guesser => AddonInfo(role, "∮", From.TheOtherRoles),

                CustomRoles.Serial => AddonInfo(role, "∂"),

                CustomRoles.BountyGuesser => AddonInfo(role, "؟"),

                CustomRoles.MagicHand => AddonInfo(role, "ж"),

                CustomRoles.Powerful => AddonInfo(role, "∠"),

                CustomRoles.Connecting => AddonInfo(role, "Ψ"),

                CustomRoles.Watching => AddonInfo(role, "∑", From.TOR_GM_Edition),

                CustomRoles.PlusVote => AddonInfo(role, "р", From.TownOfHost_Y),

                CustomRoles.Tiebreaker => AddonInfo(role, "т", From.TheOtherRoles),

                CustomRoles.Autopsy => AddonInfo(role, "Å", From.TownOfHost_Y),

                CustomRoles.Revenger => AddonInfo(role, "Я", From.TownOfHost_Y),

                CustomRoles.Speeding => AddonInfo(role, "∈"),

                CustomRoles.Guarding => AddonInfo(role, "ζ", From.TownOfHost_Y),

                CustomRoles.Absorb => AddonInfo(role, "Г"),

                CustomRoles.VoteTracker => AddonInfo(role, "ν"),

                CustomRoles.Management => AddonInfo(role, "θ", From.TownOfHost_Y),

                CustomRoles.Opener => AddonInfo(role, "п"),

                //CustomRoles.AntiTeleporter => AddonInfo(role, "t", From.RevolutionaryHostRoles),

                CustomRoles.Seeing => AddonInfo(role, "☯", From.TownOfHost_Y),

                CustomRoles.Lighting => AddonInfo(role, "＊", From.TownOfHost_Y),

                CustomRoles.Moon => AddonInfo(role, "э"),

                //デバフ

                CustomRoles.Amnesia => AddonInfo(role),

                CustomRoles.SlowStarter => AddonInfo(role, "Ｓs"),

                CustomRoles.Notvoter => AddonInfo(role, "Ｖ"),

                CustomRoles.Elector => AddonInfo(role, "Ｅ"),

                CustomRoles.InfoPoor => AddonInfo(role, "Ｉ", From.TownOfHost_Y),

                CustomRoles.NonReport => AddonInfo(role, "Ｒ", From.TownOfHost_Y),

                CustomRoles.Transparent => AddonInfo(role, "Ｔ"),

                CustomRoles.Water => AddonInfo(role, "Ｗ"),

                CustomRoles.Clumsy => AddonInfo(role, "Ｃ", From.TownOfHost_Y),

                CustomRoles.Slacker => AddonInfo(role, "ＳＬ"),

                CustomRoles.Stamina => AddonInfo(role, "ＳＴ"),

                CustomRoles.Jumbo => AddonInfo(role, "Ｊ"),

                CustomRoles.Securer => AddonInfo(role, "ＳＵ"),

                CustomRoles.Sealer => AddonInfo(role, "ＳＥ"),

                CustomRoles.Surrender => AddonInfo(role, "s", From.TownOfHost_hamo),

                CustomRoles.Reporting => AddonInfo(role, "Ⓡ", From.TownOfHost_hamo),

                CustomRoles.News => AddonInfo(role, "Ｎ"),

                CustomRoles.Sunglasses => AddonInfo(role, "Ｓ", From.TheOtherRoles),

                //第三属性

                CustomRoles.Amanojaku => AddonInfo(role),

                CustomRoles.Lovers or CustomRoles.RedLovers or CustomRoles.BlueLovers or CustomRoles.YellowLovers or CustomRoles.GreenLovers

                or CustomRoles.WhiteLovers or CustomRoles.PurpleLovers or CustomRoles.MadonnaLovers or CustomRoles.CupidLovers => AddonInfo(role, "♥", role != CustomRoles.Lovers ? From.None : From.Love_Couple_Mod),

                CustomRoles.OneLove => AddonInfo(role),

                //ラスト系

                CustomRoles.LastImpostor => AddonInfo(role, from: From.TownOfHost),

                CustomRoles.LastNeutral => AddonInfo(role),

                CustomRoles.LastCrewmate => AddonInfo(role, from: From.TownOfHost_hamo),

                CustomRoles.Workhorse => AddonInfo(role, from: From.TownOfHost),

                CustomRoles.OneWolf => AddonInfo(role),

                CustomRoles.Stack => AddonInfo(role),

                //幽霊役職

                CustomRoles.Ghostbuttoner => AddonInfo(role),

                CustomRoles.GhostFloodlight => AddonInfo(role),

                CustomRoles.GhostSaboteur => AddonInfo(role),

                CustomRoles.GhostNoiseSender => AddonInfo(role),

                CustomRoles.GhostReseter => AddonInfo(role),

                CustomRoles.GhostRumour => AddonInfo(role),

                CustomRoles.GuardianAngel => AddonInfo(role),

                CustomRoles.DemonicTracker => AddonInfo(role),

                CustomRoles.DemonicCrusher => AddonInfo(role),

                CustomRoles.DemonicSupporter => AddonInfo(role),

                CustomRoles.DemonicVenter => AddonInfo(role),

                CustomRoles.AsistingAngel => AddonInfo(role),
                CustomRoles.Inheritance => AddonInfo(role),



                _ => $"{role}-{role.GetRoleInfo()?.ConfigId ?? -100}...?(´・ω・｀)"
                    + (ShellRoleOrigins.HasOverride(role) ? $"\n{UtilsOption.GetFrom(From.None, role)}" : "")

            });

        }
}
public static class BaselineUtilsShowOption {
public static void ShowChildrenSettings(OptionItem o,ref StringBuilder b,bool Askesu=false,PlayerControl pc=null){}
public static string AddonInfo(CustomRoles role, string Mark = "", From from = From.None, PlayerControl pc = null)

        {

            var html = "\n<size=90%><line-height=1.8pic>";

            var Fromtext = $"\n{BaselineUtilsOption.GetFrom(from)}\n";

            var builder = new StringBuilder();

            var text = "";

            if (Mark != "") Mark = $" {Mark}";

            if (Options.CustomRoleSpawnChances.TryGetValue(role, out var op)) ShowChildrenSettings(op, ref builder, Askesu: true, pc: pc);

            if (builder.ToString().RemoveHtmlTags() != "") text = $"\n\n<size=45%>{builder}";

            if (BaselineUtilsOption.GetFrom(from).RemoveHtmlTags() == "") Fromtext = "";



            var info = ColorString(GetRoleColor(role), GetString($"{role}") + Mark + html + GetString($"{role}Info"));

            if (Mark == "") info = $"<b>{info}</b>";



            return info + $"\n</b></color><size=57%>{Fromtext}<line-height=1.3pic>" + GetString($"{role}InfoLong") + text;

        }
public static string GetAddonsHelp(CustomRoles role)

        {

            if (!(role.IsAddOn() || role.IsGhostRole() || role.IsLovers() || role is CustomRoles.Amanojaku || role is CustomRoles.Twins || role is CustomRoles.Triplets)) return "";

            var text = "";

            var juncture = "<line-height=2.0pic><size=150%>";



            return (text += juncture) + (role switch

            {

                CustomRoles.Twins => AddonInfo(role, ""),

                CustomRoles.Triplets => AddonInfo(role, ""),

                //バフ

                CustomRoles.Guesser => AddonInfo(role, "∮", From.TheOtherRoles),

                CustomRoles.Serial => AddonInfo(role, "∂"),

                CustomRoles.BountyGuesser => AddonInfo(role, "؟"),

                CustomRoles.MagicHand => AddonInfo(role, "ж"),

                CustomRoles.Powerful => AddonInfo(role, "∠"),

                CustomRoles.Connecting => AddonInfo(role, "Ψ"),

                CustomRoles.Watching => AddonInfo(role, "∑", From.TOR_GM_Edition),

                CustomRoles.PlusVote => AddonInfo(role, "р", From.TownOfHost_Y),

                CustomRoles.Tiebreaker => AddonInfo(role, "т", From.TheOtherRoles),

                CustomRoles.Autopsy => AddonInfo(role, "Å", From.TownOfHost_Y),

                CustomRoles.Revenger => AddonInfo(role, "Я", From.TownOfHost_Y),

                CustomRoles.Speeding => AddonInfo(role, "∈"),

                CustomRoles.Guarding => AddonInfo(role, "ζ", From.TownOfHost_Y),

                CustomRoles.Absorb => AddonInfo(role, "Г"),

                CustomRoles.VoteTracker => AddonInfo(role, "ν"),

                CustomRoles.Management => AddonInfo(role, "θ", From.TownOfHost_Y),

                CustomRoles.Opener => AddonInfo(role, "п"),

                //CustomRoles.AntiTeleporter => AddonInfo(role, "t", From.RevolutionaryHostRoles),

                CustomRoles.Seeing => AddonInfo(role, "☯", From.TownOfHost_Y),

                CustomRoles.Lighting => AddonInfo(role, "＊", From.TownOfHost_Y),

                CustomRoles.Moon => AddonInfo(role, "э"),

                //デバフ

                CustomRoles.Amnesia => AddonInfo(role),

                CustomRoles.SlowStarter => AddonInfo(role, "Ｓs"),

                CustomRoles.Notvoter => AddonInfo(role, "Ｖ"),

                CustomRoles.Elector => AddonInfo(role, "Ｅ"),

                CustomRoles.InfoPoor => AddonInfo(role, "Ｉ", From.TownOfHost_Y),

                CustomRoles.NonReport => AddonInfo(role, "Ｒ", From.TownOfHost_Y),

                CustomRoles.Transparent => AddonInfo(role, "Ｔ"),

                CustomRoles.Water => AddonInfo(role, "Ｗ"),

                CustomRoles.Clumsy => AddonInfo(role, "Ｃ", From.TownOfHost_Y),

                CustomRoles.Slacker => AddonInfo(role, "ＳＬ"),

                CustomRoles.Stamina => AddonInfo(role, "ＳＴ"),

                CustomRoles.Jumbo => AddonInfo(role, "Ｊ"),

                CustomRoles.Securer => AddonInfo(role, "ＳＵ"),

                CustomRoles.Sealer => AddonInfo(role, "ＳＥ"),

                CustomRoles.Surrender => AddonInfo(role, "s", From.TownOfHost_hamo),

                CustomRoles.Reporting => AddonInfo(role, "Ⓡ", From.TownOfHost_hamo),

                CustomRoles.News => AddonInfo(role, "Ｎ"),

                CustomRoles.Sunglasses => AddonInfo(role, "Ｓ", From.TheOtherRoles),

                //第三属性

                CustomRoles.Amanojaku => AddonInfo(role),

                CustomRoles.Lovers or CustomRoles.RedLovers or CustomRoles.BlueLovers or CustomRoles.YellowLovers or CustomRoles.GreenLovers

                or CustomRoles.WhiteLovers or CustomRoles.PurpleLovers or CustomRoles.MadonnaLovers or CustomRoles.CupidLovers => AddonInfo(role, "♥", role != CustomRoles.Lovers ? From.None : From.Love_Couple_Mod),

                CustomRoles.OneLove => AddonInfo(role),

                //ラスト系

                CustomRoles.LastImpostor => AddonInfo(role, from: From.TownOfHost),

                CustomRoles.LastNeutral => AddonInfo(role),

                CustomRoles.LastCrewmate => AddonInfo(role, from: From.TownOfHost_hamo),

                CustomRoles.Workhorse => AddonInfo(role, from: From.TownOfHost),

                CustomRoles.OneWolf => AddonInfo(role),

                CustomRoles.Stack => AddonInfo(role),

                //幽霊役職

                CustomRoles.Ghostbuttoner => AddonInfo(role),

                CustomRoles.GhostFloodlight => AddonInfo(role),

                CustomRoles.GhostSaboteur => AddonInfo(role),

                CustomRoles.GhostNoiseSender => AddonInfo(role),

                CustomRoles.GhostReseter => AddonInfo(role),

                CustomRoles.GhostRumour => AddonInfo(role),

                CustomRoles.GuardianAngel => AddonInfo(role),

                CustomRoles.DemonicTracker => AddonInfo(role),

                CustomRoles.DemonicCrusher => AddonInfo(role),

                CustomRoles.DemonicSupporter => AddonInfo(role),

                CustomRoles.DemonicVenter => AddonInfo(role),

                CustomRoles.AsistingAngel => AddonInfo(role),



                _ => $"{role}-{role.GetRoleInfo()?.ConfigId ?? -100}...?(´・ω・｀)"

            });

        }
}
public static partial class Options {
public static void SetupRoleOptions(SimpleRoleInfo info)
            => SetupRoleOptions(info.ConfigId, info.Tab, info.RoleName, info.AssignInfo.AssignCountRule,
                fromtext: UtilsOption.GetFrom(info), combination: info.Combination);
public static OptionItem SetupRoleOptions(int id, TabGroup tab, CustomRoles role, IntegerValueRule assignCountRule = null, CustomGameMode customGameMode = CustomGameMode.Standard, string fromtext = "", CombinationRoles combination = CombinationRoles.None, int defo = -1)

        {

            if ((role is CustomRoles.Phantom) || (combination != CombinationRoles.None && Combinations.Contains(combination))) return null;

            if (role.IsVanilla())

            {

                switch (role)

                {

                    case CustomRoles.Impostor: id = 10; break;

                    case CustomRoles.Shapeshifter: id = 30; break;

                    case CustomRoles.Phantom: id = 40; break;

                    case CustomRoles.Viper: id = 23050; break;

                    case CustomRoles.Crewmate: id = 11; break;

                    case CustomRoles.Engineer: id = 200; break;

                    case CustomRoles.Scientist: id = 250; break;

                    case CustomRoles.Tracker: id = 300; break;

                    case CustomRoles.Noisemaker: id = 350; break;

                    case CustomRoles.Detective: id = 23100; break;

                    case CustomRoles.Judge: id = 25100; break;
                    case CustomRoles.Influencer: id = 25150; break;

                }

            }

            assignCountRule ??= new(1, 15, 1);

            // Shell attribution wins over upstream/default option text for the selected roles only.
            if (ShellRoleOrigins.HasOverride(role)) fromtext = UtilsOption.GetFrom(From.None, role);

            var from = "<line-height=25%><size=25%>\n</size><size=60%><pos=10%></color> <b>" + fromtext + "</b></size>";



            var tag = CustomOptionTags.Role;

            if (role is CustomRoles.MMArcher) customGameMode = CustomGameMode.MurderMystery;

            switch (customGameMode)

            {

                case CustomGameMode.HideAndSeek: tag = CustomOptionTags.HideAndSeek; break;

                                case CustomGameMode.MurderMystery: tag = CustomOptionTags.MurderMystery; break;
            }

            CustomRoleManager.SortCustomRoles.Add(role);

            if (role.GetCombination() is not CustomRoles.NotAssigned) CustomRoleManager.SortCustomRoles.Add(role.GetCombination());

            var spawnOption = IntegerOptionItem.Create(id, combination == CombinationRoles.None ? role.ToString() : combination.ToString(), new(0, 100, 10), 0, tab, false, from)

                    .SetColorcode(UtilsRoleText.GetRoleColorCode(role))

                    .SetColor(UtilsRoleText.GetRoleColor(role, true))

                    .SetCustomRole(role)

                    .SetValueFormat(OptionFormat.Percent)

                    .SetHeader(true)

                    .SetEnabled(() => role is not CustomRoles.Crewmate and not CustomRoles.Impostor and not CustomRoles.Eater)

                    // ===== イーターのリストラ =====

                    // コード自体は残すが、ホストが選択できないよう設定画面から完全に非表示にする。

                    // (常に0%扱いになるので role.IsEnable() も false のままになり、

                    //  役職プールにも一切出てこなくなる)

                    .SetHidden(role == CustomRoles.NotAssigned || role == CustomRoles.Eater)

                    .SetTag(tag) as IntegerOptionItem;

            var forceHidden = role == CustomRoles.NotAssigned || role == CustomRoles.Eater;

            var hidevalue = forceHidden || role.IsCombinationRole() || role.IsLovers() || (assignCountRule.MaxValue == assignCountRule.MinValue);



            if (role is CustomRoles.Crewmate or CustomRoles.Impostor) return spawnOption;



            var countOption = IntegerOptionItem.Create(id + 1, "Maximum", assignCountRule, defo is -1 ? assignCountRule.Step : defo, tab, false, HideValue: hidevalue)

                .SetParent(spawnOption)

                .SetValueFormat(assignCountRule.MaxValue is 7 ? OptionFormat.Set : OptionFormat.Players)

                .SetEnabled(() => !SlotRoleAssign.IsSeted(role))

                .SetTag(tag)

                .SetHidden(hidevalue)

                .SetParentRole(role)

                .RegisterUpdateValueEvent((object obj, OptionItem.UpdateValueEventArgs args) => spawnOption.Refresh());



            if (combination != CombinationRoles.None) Combinations.Add(combination);

            CustomRoleSpawnChances.Add(role, spawnOption);

            CustomRoleCounts.Add(role, countOption);

            return spawnOption;

        }
}
internal static class SettingsGroups {
static CustomRoles RoleOf(OptionItem option)
    {
        // Follow existing parents too: nested settings need not repeat ParentRole.
        for (var current = option; current != null; current = current.Parent)
        {
            if (current.CustomRole != CustomRoles.NotAssigned) return current.CustomRole;
            if (current.ParentRole != CustomRoles.NotAssigned) return current.ParentRole;
        }
        return CustomRoles.NotAssigned;
    }
internal static bool IsShell(OptionItem option)
        => Hamo435RoleOrder.Contains(RoleOf(option))
            || RoleOf(option) is CustomRoles.Chief or CustomRoles.Tuner or CustomRoles.Lethe
                or CustomRoles.Destroyer or CustomRoles.MadCleaner
            || (CustomRoleManager.AllRolesInfo.TryGetValue(RoleOf(option), out var info)
                && info.From == From.TownOfHost_Shell);
internal static int Group(OptionItem option)
    {
        var role = RoleOf(option);
        if (role == CustomRoles.NotAssigned) return 3;
        if (IsShell(option)) return 1;
        return CustomRoleManager.AllRolesInfo.TryGetValue(role, out var info)
            && info.From == From.AmongUs ? 0 : 2;
    }
}
}
