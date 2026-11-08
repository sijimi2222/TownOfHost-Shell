// Generated from the current product source for tests; re-run generate-logic-tests.py after changes.
using System;
using System.Collections.Generic;
using TownOfHost.Roles.Core;
using static TownOfHost.Modules.SelfVoteManager;
namespace TownOfHost.Roles.Core { public enum CustomRoles

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

   
} }
namespace TownOfHost { public static class CustomRolesHelper { public static bool IsAddOn(this CustomRoles roles)

        {

            return

                roles is

                //ラスト系

                CustomRoles.LastImpostor or

                CustomRoles.LastNeutral or

                CustomRoles.Workhorse or

                CustomRoles.OneWolf or

                CustomRoles.Stack or

                //バフ

                CustomRoles.Moon or

                CustomRoles.VoteTracker or

                CustomRoles.Guesser or

                CustomRoles.Speeding or

                CustomRoles.Guarding or

                CustomRoles.Watching or

                CustomRoles.Lighting or

                CustomRoles.Management or

                CustomRoles.Connecting or

                CustomRoles.Serial or
                CustomRoles.BountyGuesser or

                CustomRoles.PlusVote or

                CustomRoles.Opener or

                //CustomRoles.AntiTeleporter or

                CustomRoles.Seeing or

                CustomRoles.Revenger or

                CustomRoles.Autopsy or

                CustomRoles.Tiebreaker or

                CustomRoles.MagicHand or

                CustomRoles.Powerful or

                CustomRoles.Absorb or

                CustomRoles.Symbol or

                CustomRoles.LastCrewmate or

                CustomRoles.Inheritance or

                //デバフ

                CustomRoles.NonReport or

                CustomRoles.Notvoter or

                CustomRoles.Elector or

                CustomRoles.Water or

                CustomRoles.Slacker or

                CustomRoles.Stamina or

                CustomRoles.Jumbo or

                CustomRoles.Transparent or

                CustomRoles.Amnesia or

                CustomRoles.Clumsy or

                CustomRoles.SlowStarter or

                CustomRoles.InfoPoor or

                CustomRoles.News or

                CustomRoles.Sunglasses or

                CustomRoles.Securer or

                CustomRoles.Sealer or

                CustomRoles.Surrender or

                CustomRoles.Reporting or

                CustomRoles.SilverBuzzer or
                CustomRoles.Surviving or
                CustomRoles.Berserker or
                CustomRoles.ToughGuy or
                CustomRoles.Comorbidity or
                CustomRoles.Murder or
                CustomRoles.BetrayGuesser

                ;

        }
public static bool IsBuffAddon(this CustomRoles roles)

        {

            return roles is

                CustomRoles.Moon or

                CustomRoles.Guesser or

                CustomRoles.Speeding or

                CustomRoles.Guarding or

                CustomRoles.Watching or

                CustomRoles.Lighting or

                CustomRoles.Management or

                CustomRoles.Connecting or

                CustomRoles.Serial or
                CustomRoles.BountyGuesser or

                CustomRoles.PlusVote or

                CustomRoles.Opener or

                //CustomRoles.AntiTeleporter or

                CustomRoles.Seeing or

                CustomRoles.Revenger or

                CustomRoles.Autopsy or

                CustomRoles.Tiebreaker or

                CustomRoles.MagicHand or

                CustomRoles.Berserker or
                CustomRoles.ToughGuy or
                CustomRoles.Comorbidity or
                CustomRoles.Powerful or

                CustomRoles.Absorb or

                CustomRoles.Inheritance

                ;

        }
public static bool IsDebuffAddon(this CustomRoles roles)

        {

            return roles is

                CustomRoles.NonReport or

                CustomRoles.Notvoter or

                CustomRoles.Elector or

                CustomRoles.Water or

                CustomRoles.Slacker or

                CustomRoles.Stamina or

                CustomRoles.Jumbo or

                CustomRoles.Transparent or

                CustomRoles.Amnesia or

                CustomRoles.Clumsy or

                CustomRoles.SlowStarter or

                CustomRoles.InfoPoor or

                CustomRoles.News or

                CustomRoles.Sunglasses or

                CustomRoles.Securer or

                CustomRoles.Sealer or

                CustomRoles.Surrender or

                CustomRoles.Reporting or

                CustomRoles.SilverBuzzer or
                CustomRoles.Surviving or
                CustomRoles.Murder or
                CustomRoles.BetrayGuesser

                ;

        }
public static bool IsCombinationRole(this CustomRoles role) => role is

        CustomRoles.Assassin or CustomRoles.Merlin or

        CustomRoles.Driver or CustomRoles.Braid or

        CustomRoles.Vega or CustomRoles.Altair or

        CustomRoles.Abuser or CustomRoles.Victim or

        CustomRoles.Fool or CustomRoles.Nue or
        CustomRoles.Catalyst or CustomRoles.Conductor or
        CustomRoles.Trauma or CustomRoles.Surmount;
public static CustomRoles GetCombination(this CustomRoles role)

        {

            if (role.IsCombinationRole() is false) return CustomRoles.NotAssigned;



            switch (role)

            {

                case CustomRoles.Assassin: return CustomRoles.Merlin;

                case CustomRoles.Merlin: return CustomRoles.Assassin;

                case CustomRoles.Driver: return CustomRoles.Braid;

                case CustomRoles.Braid: return CustomRoles.Driver;

                case CustomRoles.Vega: return CustomRoles.Altair;

                case CustomRoles.Altair: return CustomRoles.Vega;

                case CustomRoles.Abuser: return CustomRoles.Victim;

                case CustomRoles.Victim: return CustomRoles.Abuser;

                case CustomRoles.Fool: return CustomRoles.Nue;

                case CustomRoles.Nue: return CustomRoles.Fool;
                case CustomRoles.Catalyst: return CustomRoles.Conductor;
                case CustomRoles.Conductor: return CustomRoles.Catalyst;
                case CustomRoles.Trauma: return CustomRoles.Surmount;
                case CustomRoles.Surmount: return CustomRoles.Trauma;

            }

            return CustomRoles.NotAssigned;

        }
public static bool IsGhostRole(this CustomRoles role)

        {

            return role is CustomRoles.GuardianAngel

                        or CustomRoles.Ghostbuttoner

                        or CustomRoles.GhostFloodlight

                        or CustomRoles.GhostSaboteur

                        or CustomRoles.GhostNoiseSender

                        or CustomRoles.GhostReseter

                        or CustomRoles.GhostRumour

                        or CustomRoles.DemonicTracker

                        or CustomRoles.DemonicVenter

                        or CustomRoles.DemonicCrusher

                        or CustomRoles.DemonicSupporter

                        or CustomRoles.AsistingAngel
                        or CustomRoles.Arsene
                        or CustomRoles.Kyonshi

                        ;

        } }
public static class Options { public static Dictionary<CustomRoles, FakeOption> CustomRoleCounts = new(); public static Dictionary<CustomRoles, FakeOption> CustomRoleSpawnChances = new(); public static int GetRoleCount(CustomRoles role)
        {
            // The secondary member is never independently rolled.
            role = role switch { CustomRoles.Surmount => CustomRoles.Trauma, CustomRoles.Conductor => CustomRoles.Catalyst, _ => role };

            return GetRoleChance(role) == 0 ? 0 : CustomRoleCounts.TryGetValue(role, out var option) ? option.GetInt() : 0;

        }
public static int GetRoleChance(CustomRoles role)
        {
            // The secondary member is never independently rolled.
            role = role switch { CustomRoles.Surmount => CustomRoles.Trauma, CustomRoles.Conductor => CustomRoles.Catalyst, _ => role };

            if (!Event.CheckRole(role)) return 0;



            if (CustomRoleSpawnChances.TryGetValue(role, out var option))

            {

                if (option.GetBool())

                    return option.GetInt();

            }

            return 0;

        } } }
namespace TownOfHost.Roles.Crewmate { public sealed class SenderState
{
    public int Used;
    byte pendingTarget = byte.MaxValue;
    public bool AwaitingMessage;

    public int Left(int max) => System.Math.Max(0, max - Used);
    public void Reset() { pendingTarget = byte.MaxValue; AwaitingMessage = false; }

    public bool CheckVote(PlayerControl me, byte votedForId, PlayerControl voter, int max)
    {
        if (!Canuseability() || voter.PlayerId != me.PlayerId || !me.IsAlive() || Left(max) <= 0) return true;
        if (!CheckSelfVoteMode(me, votedForId, out var status)) return true;
        if (status is VoteStatus.Self) Utils.SendMessage(GetString("SenderSelectMode"), me.PlayerId);
        if (status is VoteStatus.Skip) { Reset(); Utils.SendMessage(GetString("VoteSkillFin"), me.PlayerId); }
        if (status is VoteStatus.Vote)
        {
            var t = PlayerCatch.GetPlayerById(votedForId);
            if (t == null || !t.IsAlive() || t.PlayerId == me.PlayerId) Utils.SendMessage(GetString("SenderCantTarget"), me.PlayerId);
            else
            {
                pendingTarget = votedForId;
                AwaitingMessage = true;
                Utils.SendMessage(string.Format(GetString("SenderReady"), t.Data?.PlayerName), me.PlayerId);
            }
        }
        SetMode(me, status is VoteStatus.Self);
        return false;
    }

    public bool TryIntercept(PlayerControl me, string text, int max)
    {
        if (pendingTarget == byte.MaxValue || !GameStates.IsMeeting || !me.IsAlive() || string.IsNullOrWhiteSpace(text)) return false;
        if (text.TrimStart().StartsWith("/")) return false; // コマンドはそのまま
        var target = PlayerCatch.GetPlayerById(pendingTarget);
        Reset();
        if (target == null || !target.IsAlive() || Left(max) <= 0)
        {
            (me.GetRoleClass() as Sender)?.SendRPC();
            (me.GetRoleClass() as Madmate.MadSender)?.SendRPC();
            return false;
        }
        Used++;
        (me.GetRoleClass() as Sender)?.SendRPC();
        (me.GetRoleClass() as Madmate.MadSender)?.SendRPC();
        Utils.SendMessage(text, target.PlayerId, Utils.ColorString(UnityEngine.Color.cyan, GetString("SenderFrom")));
        Utils.SendMessage(string.Format(GetString("SenderSent"), target.Data?.PlayerName, Left(max)), me.PlayerId);
        Logger.Info($"センダー: {me.Data?.GetLogPlayerName()} → {target.Data?.GetLogPlayerName()} に秘匿を送信", "Sender");
        return true;
    }

    static string GetString(string key) => Translator.GetString(key);
} }
