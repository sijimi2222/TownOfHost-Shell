using TownOfHost.Roles.Core; using TownOfHost.Roles.Madmate; namespace TownOfHost {
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
public enum CustomWinner

    {

        Draw = -1,

        Default = -2,

        None = -3,

        Impostor = CustomRoles.Impostor,

        Crewmate = CustomRoles.Crewmate,

        Tiger = CustomRoles.Tiger,
        Streamer = CustomRoles.Streamer,
        Prankster = CustomRoles.Prankster,
        LessThan = CustomRoles.LessThan,
        Romeo = CustomRoles.Romeo,
        Flatfish = CustomRoles.Flatfish,
        Lupin = CustomRoles.Lupin,
        Daoshi = CustomRoles.Daoshi,
        Catalyst = CustomRoles.Catalyst,
        Parity = CustomRoles.Parity,
        NDPlayer = CustomRoles.NDPlayer,

        Jester = CustomRoles.Jester,

        FakeJester = CustomRoles.FakeJester,

        AntiHero = CustomRoles.AntiHero,

        HappyJester = CustomRoles.HappyJester,

        PlagueDoctor = CustomRoles.PlagueDoctor,

        Terrorist = CustomRoles.Terrorist,

        Lovers = CustomRoles.Lovers,

        RedLovers = CustomRoles.RedLovers,

        YellowLovers = CustomRoles.YellowLovers,

        BlueLovers = CustomRoles.BlueLovers,

        GreenLovers = CustomRoles.GreenLovers,

        WhiteLovers = CustomRoles.WhiteLovers,

        PurpleLovers = CustomRoles.PurpleLovers,

        MadonnaLovers = CustomRoles.MadonnaLovers,

        CupidLovers = CustomRoles.CupidLovers,

        OneLove = CustomRoles.OneLove,

        Executioner = CustomRoles.Executioner,

        Arsonist = CustomRoles.Arsonist,

        Egoist = CustomRoles.Egoist,

        Jackal = CustomRoles.Jackal,
        Hunter = CustomRoles.Hunter,
        Ventoman = CustomRoles.Ventoman,

        Remotekiller = CustomRoles.Remotekiller,

        Chef = CustomRoles.Chef,

        Monochromer = CustomRoles.Monochromer,

        GrimReaper = CustomRoles.GrimReaper,

        CountKiller = CustomRoles.CountKiller,

        Workaholic = CustomRoles.Workaholic,

        MassMedia = CustomRoles.MassMedia,

        SantaClaus = CustomRoles.SantaClaus,

        DoppelGanger = CustomRoles.DoppelGanger,

        Vulture = CustomRoles.Vulture,

        CurseMaker = CustomRoles.CurseMaker,

        Fox = CustomRoles.Fox,

        PhantomThief = CustomRoles.PhantomThief,

        MilkyWay = CustomRoles.Vega,

        MadBetrayer = CustomRoles.MadBetrayer,

        Strawdoll = CustomRoles.Strawdoll,

        Missioneer = CustomRoles.Missioneer,

        God = CustomRoles.God,

        Tuna = CustomRoles.Tuna,

        DarkSheriff = CustomRoles.DarkSheriff,

        RaccoonParent = CustomRoles.RaccoonParent,

        Onmyoji = CustomRoles.Onmyoji,

        Zombie = CustomRoles.Zombie,

        Eater = CustomRoles.Eater,

        Spelunker = CustomRoles.Spelunker,

        Pavlov = CustomRoles.PavlovDog,

        Moira = CustomRoles.Moira,

        PoisonedBakery = CustomRoles.PoisonedBakery,

        Monika = CustomRoles.Monika,

        LoversBreaker = CustomRoles.LoversBreaker,

        Chatter = CustomRoles.Chatter,

        Suicider = CustomRoles.Suicider,

        BatGirl = CustomRoles.BatGirl,

        StandMaster = CustomRoles.StandMaster,

        Shyboy = CustomRoles.Shyboy,

        Villain = CustomRoles.Villain,

        Scratcher = CustomRoles.Scratcher,

        PokerFace = CustomRoles.PokerFace,

        Lawyer = CustomRoles.Lawyer,

        Pirate = CustomRoles.Pirate,

        Victim = CustomRoles.Victim,
        Dracula = CustomRoles.Dracula,
        Mermaid = CustomRoles.Mermaid,
        Muter = CustomRoles.Muter,
        Mario = CustomRoles.Mario,
        DarkHide = CustomRoles.DarkHide,



        Amateras = CustomRoles.Amateras,

        Ruler = CustomRoles.Ruler,



        HASTroll = CustomRoles.HASTroll,

        TaskPlayerB = CustomRoles.TaskPlayerB,

        Sleeper = CustomRoles.Sleeper,

        Null = CustomRoles.Null,

        Typewriter = CustomRoles.Typewriter,

        VillainWolf = CustomRoles.VillainWolf,

        SuddenDeathRed = 1000, SuddenDeathBlue = 1001, SuddenDeathYellow = 1002, SuddenDeathGreen = 1003, SuddenDeathPurple = 1004

    }
static class RealFactionHelpers {
public static bool IsImpostor(this CustomRoles role)

        {

            var roleInfo = role.GetRoleInfo();

            if (roleInfo != null)

                return roleInfo.CustomRoleType == CustomRoleTypes.Impostor;



            return false;

        }
public static bool IsMadmate(this CustomRoles role)

        {

            if (role is CustomRoles.MadBetrayer) return MadBetrayer.IsMadmate();

            var roleInfo = role.GetRoleInfo();

            if (roleInfo != null)

                return roleInfo.CustomRoleType == CustomRoleTypes.Madmate;

            return role == CustomRoles.SKMadmate;

        }
public static bool IsImpostorTeam(this CustomRoles role) => role.IsImpostor() || role.IsMadmate();
public static bool IsNeutral(this CustomRoles role)

        {

            if (role is CustomRoles.MadBetrayer) return MadBetrayer.IsMadmate() is false;

            var roleInfo = role.GetRoleInfo();

            if (roleInfo != null)

                return roleInfo.CustomRoleType == CustomRoleTypes.Neutral || role == CustomRoles.Jackaldoll;

            return role is CustomRoles.HASTroll or CustomRoles.HASFox;

        }
public static CustomRoleTypes GetCustomRoleTypes(this CustomRoles role)

        {

            CustomRoleTypes type = CustomRoleTypes.Crewmate;



            if (role is CustomRoles.MadBetrayer) return MadBetrayer.IsMadmate() ? type = CustomRoleTypes.Madmate : CustomRoleTypes.Neutral;

            var roleInfo = role.GetRoleInfo();

            if (roleInfo != null)

                return roleInfo.CustomRoleType;



            if (role.IsImpostor()) type = CustomRoleTypes.Impostor;

            if (role.IsNeutral()) type = CustomRoleTypes.Neutral;

            if (role.IsMadmate()) type = CustomRoleTypes.Madmate;

            return type;

        }
public static bool IsLovers(this CustomRoles roles, bool checkonelover = true)

        {

            if (roles is CustomRoles.OneLove && checkonelover) return true;

            return roles is

            CustomRoles.Lovers or

            CustomRoles.RedLovers or

            CustomRoles.YellowLovers or

            CustomRoles.BlueLovers or

            CustomRoles.GreenLovers or

            CustomRoles.WhiteLovers or

            CustomRoles.PurpleLovers or

            CustomRoles.MadonnaLovers or

            CustomRoles.CupidLovers;



        }
public static bool IsLovers(this PlayerControl pc, bool checkonelover = true)

        {

            return pc.Is(CustomRoles.Lovers) || pc.Is(CustomRoles.RedLovers) ||

            pc.Is(CustomRoles.YellowLovers) || pc.Is(CustomRoles.BlueLovers) ||

            pc.Is(CustomRoles.GreenLovers) || pc.Is(CustomRoles.WhiteLovers) ||

            pc.Is(CustomRoles.PurpleLovers) || pc.Is(CustomRoles.MadonnaLovers) ||

            pc.Is(CustomRoles.CupidLovers) ||

            (pc.Is(CustomRoles.OneLove) && checkonelover);

        }
}}