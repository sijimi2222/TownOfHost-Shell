global using static TownOfHost.Translator;
using TownOfHost;
using TownOfHost.Modules;
using TownOfHost.Roles.Core;
using UnityEngine;
namespace UnityEngine {
 public partial struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public float sqrMagnitude=>x*x+y*y;public static float Distance(Vector2 a,Vector2 b)=>MathF.Sqrt((a-b).sqrMagnitude);public static Vector2 operator -(Vector2 a,Vector2 b)=>new(a.x-b.x,a.y-b.y);public static implicit operator Vector2(Vector3 v)=>new(v.x,v.y);public static implicit operator Vector3(Vector2 v)=>new(v.x,v.y,0);}
 public partial struct Vector3{public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}}
 public partial class Transform{public Vector3 position;}
 public class GraphicsBuffer{}
 public static partial class Mathf{public static float Min(float a,float b)=>MathF.Min(a,b);public static float Max(float a,float b)=>MathF.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static int Clamp(int v,int min,int max)=>Math.Clamp(v,min,max);}
 public static class Time{public static float time,deltaTime=.02f,fixedDeltaTime=.02f;}
 public class GameObject{public bool Active;public bool activeInHierarchy=>Active;public void SetActive(bool x)=>Active=x;}
}
namespace BepInEx{public class Dummy{}}
namespace BepInEx.Unity.IL2CPP{public class Dummy{}}
namespace BepInEx.Logging{public class ManualLogSource{public void LogError(Exception e){}}}
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Parameter)]public class HarmonyArgument:Attribute{public HarmonyArgument(int index){}}[AttributeUsage(AttributeTargets.Method)]public class HarmonyPriority:Attribute{public HarmonyPriority(int value){}}public static class Priority{public const int Last=0;}[AttributeUsage(AttributeTargets.Class|AttributeTargets.Method,AllowMultiple=true)]public class HarmonyPatch:Attribute{public HarmonyPatch(params object[] a){}}}
namespace MS.Internal.Xml.XPath{public class Dummy{}}
namespace Il2CppSystem.Xml.Schema{public class FacetsChecker{public class FacetsCompiler{}}}
namespace Hazel{
 public enum SendOption{Reliable,None}
 public class MessageWriter{
  public static List<MessageWriter> Created=new();public MemoryStream Buffer=new();readonly Stack<int> frames=new();public bool Recycled;public SendOption SendOption;
  public static MessageWriter Get(SendOption opt){var w=new MessageWriter{SendOption=opt};w.Write((byte)(opt==SendOption.Reliable?1:0));if(opt==SendOption.Reliable){w.Write((byte)0);w.Write((byte)0);}Created.Add(w);return w;}
  public void StartMessage(byte tag){frames.Push((int)Buffer.Position);Write((byte)0);Write((byte)0);Write(tag);}public void EndMessage(){int start=frames.Pop();long end=Buffer.Position;Buffer.Position=start;int len=(int)end-start-3;Write((byte)len);Write((byte)(len>>8));Buffer.Position=end;}
  public void Write(byte x)=>Buffer.WriteByte(x);public void Write(bool x)=>Write(x?(byte)1:(byte)0);public void Write(int x)=>Buffer.Write(BitConverter.GetBytes(x));public void Write(float x)=>Buffer.Write(BitConverter.GetBytes(x));public void Write(uint x)=>Buffer.Write(BitConverter.GetBytes(x));public void WritePacked(uint x)=>WritePacked((int)x);public void WritePacked(int x){uint v=(uint)x;while(v>=128){Write((byte)(v|128));v>>=7;}Write((byte)v);}public void Recycle()=>Recycled=true;public byte[] ToByteArray(bool header)=>Buffer.ToArray();
 }
 public partial class MessageReader{readonly Queue<byte> bytes;public int Position;public int Length;public MessageReader(params byte[] values){bytes=new(values);Length=values.Length;}public bool ReadBoolean()=>ReadByte()!=0;public byte ReadByte(){Position++;return bytes.Dequeue();}}
}
namespace AmongUs.GameOptions{public enum FloatOptionNames{KillCooldown}public enum RoleTypes{Crewmate,Engineer,Phantom,Impostor,Shapeshifter}public interface IGameOptions{}public static class AURoleOptions{public static float PhantomCooldown,ShapeshifterCooldown,EngineerCooldown,EngineerInVentMaxTime,ShapeshifterDuration;}public static class OptExt{public static void SetVision(this IGameOptions opt,bool x){}public static void SetFloat(this IGameOptions opt,FloatOptionNames name,float value){}}}
public partial class NetworkedPlayerInfo{public bool IsDead,Disconnected;}
public partial class Animations{public bool IsPlayingEnterVentAnimation()=>false;public bool IsPlayingAnyLadderAnimation()=>false;}
public class PlayerPhysics{public List<int> Boots=new();public bool ThrowOnBoot;public void RpcBootFromVent(int id){if(ThrowOnBoot)throw new InvalidOperationException("synthetic boot failure");Boots.Add(id);}public Animations Animations=new();}
public partial class PlayerControl{
 public static implicit operator bool(PlayerControl p)=>p is not null; public static PlayerControl LocalPlayer;public static List<PlayerControl> AllPlayerControls=>PlayerCatch.All;public byte PlayerId;public int OwnerId;public bool AmOwner,inVent,inMovingPlat,Guarded;public NetworkedPlayerInfo Data=new();public Transform transform=new();public PlayerPhysics MyPhysics=new();public CustomRoles Role;public float killTimer;public static uint NextNet;public uint NetId=++NextNet;public PlayerControl Target;public byte RealKillerId=255;public int Reports;public void SetRealKiller(PlayerControl killer)=>RealKillerId=killer.PlayerId;public PlayerControl GetKillTarget(bool only)=>Target;public void ReportDeadBody(NetworkedPlayerInfo info)=>Reports++;public void SetKillCooldown(float cd,bool delay=false)=>LastCooldown=cd;public int ResetCount,SettingsCount,Teleports;public float LastCooldown;
 public void FixedUpdate(){}public bool IsAlive()=>Data!=null&&!Data.IsDead&&!Data.Disconnected;public RoleBase GetRoleClass()=>CustomRoleManager.AllActiveRoles.GetValueOrDefault(PlayerId);
 public CustomRoles GetCustomRole()=>Role;public bool Is(CustomRoles r)=>Role==r;public bool Is(CustomRoleTypes r)=>r==CustomRoleTypes.Impostor&&Role==CustomRoles.DoubleKiller;
 public bool CanUseKillButton()=>Role!=CustomRoles.Madpsycho&&Role!=CustomRoles.Hunter;public string GetNameWithRole()=>"player-"+PlayerId;public Vector2 GetTruePosition()=>transform.position;
 public void RpcMurderPlayer(PlayerControl t){CustomRoleManager.DirectMurders++;if(!t.Guarded)t.Data.IsDead=true;}public void RpcSnapToForced(Vector2 v){transform.position=v;Teleports++;}public void RpcResetAbilityCooldown(bool Sync=false)=>ResetCount++;public void RpcProtectedMurderPlayer(){}public void ResetKillCooldown()=>killTimer=30;public void SetKillTimer(float x)=>killTimer=x;public void SyncSettings()=>SettingsCount++;public void MarkDirtySettings()=>SettingsCount++;public void SetKillCooldown(PlayerControl target=null,bool force=false)=>LastCooldown=30;public bool IsWinner(CustomWinner w)=>false;public bool IsLovers()=>false;
}
public class MeetingHud{public static MeetingHud Instance;}
public partial class AmongUsClient{public static AmongUsClient Instance=new();public bool AmHost=true;public int Ping=0,GameId=12;public List<byte[]> Packets=new();public void SendOrDisconnect(Hazel.MessageWriter w)=>Packets.Add(w.ToByteArray(true));}
public interface ISystemType{}
public enum SystemTypes{Ventilation=20}
public class SequenceBuffer<T>{}public class VentMoveInfo{}
public class VentilationSystem:ISystemType{public IntPtr Pointer=new(42);public enum Operation{StartCleaning,StopCleaning,Enter,Exit,Move,BootImpostors}public List<(byte Player,Operation Op,byte Vent)> Applied=new();public void PerformVentOp(byte playerId,Operation op,byte ventId,SequenceBuffer<VentMoveInfo> buffer){Applied.Add((playerId,op,ventId));if(op==Operation.Exit)PlayersInsideVents.Remove(playerId);else if(op==Operation.Move)PlayersInsideVents[playerId]=ventId;}public Dictionary<byte,byte> PlayersCleaningVents=new(),PlayersInsideVents=new();public void Serialize(){}}
public static class CastExt{public static T TryCast<T>(this ISystemType s) where T:class=>s as T;}
public partial class ShipStatus{public IntPtr Pointer=new(24);public static ShipStatus Instance;public Dictionary<SystemTypes,ISystemType> Systems=new();public Vent[] AllVents=Array.Empty<Vent>();public uint NetId=3;public void Start(){}}
public class Vent{public int Id;public Vent[] NearbyVents=Array.Empty<Vent>();public GameObject[] CleaningIndicators=Array.Empty<GameObject>();public void UpdateArrows(){}public bool TryMoveToVent(Vent v)=>true;}
public class GameOptionsManager{public static GameOptionsManager Instance=new();public NormalOptions CurrentGameOptions=new();}
public partial class NormalOptions{public byte MapId=1;public int KillDistance=1;}
namespace TownOfHost.Attributes{public class PluginModuleInitializerAttribute:Attribute{}public class GameModuleInitializerAttribute:Attribute{}}
namespace TownOfHost{
 public enum CustomRoles{//Default

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
    // hamo 4.40.04 additions: Shell's existing numeric IDs are immutable.
    Pusher = 383,
    Cleaner = 384,
    Gimlet = 385,
    Informant = 386,
    LifeSaver = 387,
    Bloodlust = 388,
    EvilButtoner = 389,
    Notice = 1073,
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

   
}public enum CustomRoleTypes{Crewmate,Madmate,Impostor,Neutral}public enum CustomDeathReason{Kill,Counter,Suicide,Evaporation,Hit,Fall}public enum CountTypes{Hunter}public enum GeneralOption{CanVent,TaskTrigger,KillCooldown,Cooldown,ImpostorVision,OptionCount}public enum OptionFormat{Seconds,Pieces,Times,Multiplier,Players}public enum CustomWinner{Hunter,Default,Crewmate,Informant}public enum GameOverReason{ImpostorByKill}public enum CustomGameMode{Standard,HideAndSeek}
 public static class Main{public static NormalOptions NormalOptions=new();public static float LagTime;public const float MinSpeed=.0001f;public static Dictionary<byte,float> AllPlayerSpeed=new();}
 public static partial class GameStates{public static bool IsInTask=true,IsMeeting,CalledMeeting,ExiledAnimate;}
 public static partial class Options{public static CustomGameMode CurrentGameMode;public static OptionItem MadCanSeeImpostor=new(){Value=1};public static bool IsStandardHAS,firstturnmeeting;public static float HideAndSeekKillDelayTimer;}
 public static class MeetingStates{public static bool FirstMeeting;}
 public static class GameModeManager{public static bool IsStandardClass()=>true;}
 public static class StringExt{public static string RemoveHtmlTags(this string x)=>x;}
 public class FloatRule{public float Min,Max,Step;public FloatRule(float a,float b,float c){Min=a;Max=b;Step=c;}}
 public class IntRule{public int Min,Max,Step;public IntRule(int a,int b,int c){Min=a;Max=b;Step=c;}}
 public partial class OptionItem{public float Value,Default;public int Id;public object Rule;public static List<int> Registrations=new();public static Dictionary<int,OptionItem> All=new();public float GetFloat()=>Value;public int GetInt()=>(int)Value;public int GetValue()=>(int)Value;public bool GetBool()=>Value!=0;public OptionItem SetValueFormat(OptionFormat f)=>this;}
 public class FloatOptionItem:OptionItem{public static FloatOptionItem Create(SimpleRoleInfo info,int id,object name,FloatRule rule,float def,bool single){var o=new FloatOptionItem{Value=def,Default=def,Id=info.ConfigId+id,Rule=rule};Registrations.Add(o.Id);All[o.Id]=o;return o;}}
 public static class IntegerOptionItem{public static OptionItem Create(SimpleRoleInfo info,int id,object name,IntRule rule,int def,bool single){var o=new OptionItem{Value=def,Default=def,Id=info.ConfigId+id,Rule=rule};OptionItem.Registrations.Add(o.Id);OptionItem.All[o.Id]=o;return o;}}
 public static partial class BooleanOptionItem{public static OptionItem Create(SimpleRoleInfo info,int id,object name,bool def,bool single){var o=new OptionItem{Value=def?1:0,Default=def?1:0,Id=info.ConfigId+id};OptionItem.Registrations.Add(o.Id);OptionItem.All[o.Id]=o;return o;}}
 public static class StringOptionItem{public static OptionItem Create(SimpleRoleInfo info,int id,object name,string[] choices,int def,bool single){var o=new OptionItem{Value=def,Default=def,Id=info.ConfigId+id};OptionItem.Registrations.Add(o.Id);OptionItem.All[o.Id]=o;return o;}}
 public static class OverrideTasksData{public static void Create(SimpleRoleInfo info,int id,(bool,int,int,int)? tasks=null){}}
 public static class SoloWinOption{public static void Create(SimpleRoleInfo info,int id,int defo){}}
 public static class Translator{public static string GetString(string key)=>key=="Rpsych"||key=="RMeetingKill"?"{0}/{1}":key;}
 public static class Logger{public static void Info(string m,string tag){}public static void Warn(string m,string tag){}}
 public static class PlayerCatch{public static List<PlayerControl> All=new();public static IEnumerable<PlayerControl> AllPlayerControls=>All;public static IEnumerable<PlayerControl> AllAlivePlayerControls=>All.Where(p=>p.IsAlive());public static NetworkedPlayerInfo GetPlayerInfoById(byte id)=>GetPlayerById(id)?.Data;public static PlayerControl GetPlayerById(byte id)=>All.FirstOrDefault(p=>p.PlayerId==id);public static PlayerControl GetPlayerControl(byte id)=>GetPlayerById(id);}
 public static partial class RoleExt{public static bool IsImpostor(this CustomRoles r)=>r==CustomRoles.DoubleKiller||r==CustomRoles.HateKiller||r==CustomRoles.Professional||r==CustomRoles.Nova;}
 public partial class PlayerState{static Dictionary<byte,PlayerState> All=new();public int NumberOfRemainingButtons=1;public byte PlayerId;public CustomDeathReason DeathReason;public int GetKillCount()=>0;public static PlayerState GetByPlayerId(byte id){if(!All.ContainsKey(id))All[id]=new(){PlayerId=id};return All[id];}public static void Reset()=>All.Clear();}
 public static partial class UtilsName{public static string GetPlayerColor(PlayerControl p,bool x)=>"p"+p.PlayerId;}
 public static partial class Utils{public const string AdditionalAliveWinnerMark="+";public static List<byte> Messages=new();public static void SendMessage(string message,byte id=255,string title="")=>Messages.Add(id);}
 public static class UtilsNotifyRoles{public static int Calls;public static void NotifyRoles(PlayerControl SpecifySeer=null,bool ForceLoop=false,bool OnlyMeName=false){Calls++;}}
 public static class SuddenDeathMode{public static bool NowSuddenDeathMode;}
 public static class RPC{public static int SoundsPlayed;public static void PlaySoundRPC(byte id,Sounds sound)=>SoundsPlayed++;}public enum Sounds{KillSound,SabotageAlarm}
 public class Achievement{public Achievement(SimpleRoleInfo role,int a,int b,int c,int d,bool e=false){}}
 public static class Achievements{public static void RpcCompleteAchievement(byte id,int a,Achievement b){}}
}
namespace TownOfHost.Modules{
 public class LateTask{public static List<(Action Action,float Delay,float At)> Jobs=new();public LateTask(Action action,float delay,string name="",bool log=false)=>Jobs.Add((action,delay,Time.time+delay));public static void At(float time){Time.time=time;foreach(var j in Jobs.Where(j=>j.At<=time).ToArray()){Jobs.Remove(j);j.Action();}}}
}
namespace TownOfHost.Roles.Core{
 public enum HasTask{False,ForRecompute}public enum From{SuperNewRoles,TownOfHost_Pko,TownOfHost_hamo,TownOfHost_K,UchuAddon,NebulaontheShip}
 public class TaskState{public int Completed;public bool HasCompletedEnoughCountOfTasks(int n)=>Completed>=n;}
 public class RoleAssignInfo{public IntRule AssignCountRule;public RoleAssignInfo(CustomRoles role,CustomRoleTypes team){}}
 public class SimpleRoleInfo{public int ConfigId;public Action Setup;public string RoleColorCode;public Color RoleColor=new();public From From;public CustomRoles RoleName;public Func<AmongUs.GameOptions.RoleTypes> BaseRoleType;public RoleAssignInfo AssignInfo;public bool IsDesyncImpostor;public static SimpleRoleInfo Create(Type type,Func<PlayerControl,RoleBase> make,CustomRoles role,Func<AmongUs.GameOptions.RoleTypes> baseRole,CustomRoleTypes team,int id,Action setup,string cmd,string colorCode="",(int,int)? OptionSort=null,bool isDesyncImpostor=false,CountTypes? countType=null,RoleAssignInfo assignInfo=null,From from=default,bool isNewRole=false,Func<string> Desc=null,Func<object> introSound=null)=>new(){From=from,RoleName=role,BaseRoleType=baseRole,ConfigId=id,Setup=setup,RoleColorCode=colorCode,AssignInfo=assignInfo,IsDesyncImpostor=isDesyncImpostor};}
 public class Sender:IDisposable{public Hazel.MessageWriter Writer=new();public static List<byte[]> Packets=new();public void Dispose(){Packets.Add(Writer.ToByteArray(false));}}
 public partial class MurderInfo{public PlayerControl AttemptKiller,AttemptTarget;public int KillPower=1,GuardPower;public bool DoKill=true;public (PlayerControl,PlayerControl) AttemptTuple=>(AttemptKiller,AttemptTarget);public bool IsFakeSuicide;}
 public abstract partial class RoleBase{public PlayerControl Player;public TaskState MyTaskState=new();public PlayerState MyState=>PlayerState.GetByPlayerId(Player.PlayerId);public Func<HasTask> TaskMode;protected static FloatRule OptionBaseCoolTime=new(0,180,2.5f);public RoleBase(SimpleRoleInfo info,PlayerControl player,Func<HasTask> hasTasks=null){Player=player;TaskMode=hasTasks;CustomRoleManager.AllActiveRoles.TryAdd(player.PlayerId,this);}protected Sender CreateSender(Hazel.SendOption opt=Hazel.SendOption.None){var x=new Sender();x.Writer.Write(Player.PlayerId);return x;}public virtual void Add(){}public virtual void OnDestroy(){}public virtual void OnDead(PlayerControl player){}public virtual bool OnCheckMurderAsTarget(MurderInfo info)=>true;public virtual bool OnCompleteTask(uint id)=>true;public virtual void ApplyGameOptions(AmongUs.GameOptions.IGameOptions opt){}public virtual bool OnEnterVent(PlayerPhysics p,int id)=>true;public virtual bool CanVentMoving(PlayerPhysics p,int id)=>true;public virtual bool CanClickUseVentButton=>true;public virtual void OnFixedUpdate(PlayerControl p){}public virtual void ReceiveRPC(Hazel.MessageReader r){}public virtual string GetProgressText(bool comms=false,bool GameLog=false)=>"";public virtual bool OverrideAbilityButton(out string text){text="";return false;}public virtual void OnStartMeeting(){}public virtual void OnReportDeadBody(PlayerControl p,NetworkedPlayerInfo n){}public virtual void AfterMeetingTasks(){}public virtual bool VotingResults(ref NetworkedPlayerInfo exiled,ref bool tie,Dictionary<byte,int> vote,byte[] most,bool clear)=>true;public virtual void CheckWinner(GameOverReason reason){}public virtual bool CheckShapeshift(PlayerControl target,ref bool animate)=>true;public virtual string GetAbilityButtonText()=>"";public virtual string GetLowerText(PlayerControl seer,PlayerControl seen=null,bool isForMeeting=false,bool isForHud=false)=>"";}
 public static partial class CustomRoleManager{
  public static Dictionary<byte,RoleBase> AllActiveRoles=new();public static bool Accept=true,Throw,Validate=true;public static int Attempts,DirectMurders;public static Action<MurderInfo> Observe;public static MurderInfo LastInfo;
  public static bool OnCheckMurder(PlayerControl killer,PlayerControl target,PlayerControl appearKiller,PlayerControl appearTarget,bool? force=false,bool? DontRoleAbility=false,int Killpower=1,CustomDeathReason deathReason=CustomDeathReason.Kill){Attempts++;if(Throw)throw new InvalidOperationException("synthetic");var info=new MurderInfo{AttemptKiller=killer,AttemptTarget=target,AppearanceKiller=appearKiller,AppearanceTarget=appearTarget,KillPower=Killpower,IsFakeSuicide=appearKiller.PlayerId==appearTarget.PlayerId};LastInfo=info;Observe?.Invoke(info);if(Validate&&!TownOfHost.CheckMurderPatch.CheckForInvalidMurdering(info,force==true))return false;if(DontRoleAbility!=true&&target.GetRoleClass() is RoleBase role&&!role.OnCheckMurderAsTarget(info))return false;if(!Accept||target.Guarded)return false;if(force==false&&killer.GetRoleClass() is Interfaces.IKiller k)k.OnCheckMurderAsKiller(info);if(!info.DoKill)return false;if(TownOfHost.Modules.DeadBodyEraser.TryCompleteNoBodyKill(info))return true;target.Data.IsDead=true;PlayerState.GetByPlayerId(target.PlayerId).DeathReason=deathReason;killer.killTimer=30;return true;}
 }
}
namespace TownOfHost.Roles.Core.Interfaces{public interface IKiller{bool CanKill=>true;bool IsKiller=>CanKill;void OnCheckMurderAsKiller(MurderInfo info){}void OnMurderPlayerAsKiller(MurderInfo info){}}public interface IImpostor:IKiller{}public interface ILNKiller:IKiller{}public interface IUsePhantomButton{public static Dictionary<byte,float> IPPlayerKillCooldown=new();bool IsPhantomRole=>true;bool IsresetAfterKill=>true;bool SyncAbilityCooldownWithKillCooldown=>false;void OnClick(ref bool AdjustKillCooldown,ref bool? ResetCooldown);}}
namespace TownOfHost.Roles.Impostor{public static class Ballooner{public static bool IsInExplosionRange(PlayerControl a,PlayerControl b,float range)=>Vector2.Distance(a.GetTruePosition(),b.GetTruePosition())<=range;}}

namespace TownOfHost.Roles.Crewmate{public class Dummy{}}
namespace TownOfHost{public static class RoleAddAddons{public static void Create(TownOfHost.Roles.Core.SimpleRoleInfo info,int id,bool NeutralKiller=false){}}}

namespace TownOfHost.Patches.ISystemType{public static class VentilationSystemUpdateSystemPatch{public static Dictionary<byte,byte> NowVentId=new();}}

namespace TownOfHost{public static class ByteExt{public static PlayerControl GetPlayerControl(this byte id)=>PlayerCatch.GetPlayerById(id);}public static partial class ReportDeadBodyPatch{public static Dictionary<byte,bool> IgnoreBodyids=new();}public static partial class CustomWinnerHolder{public static CustomWinner WinnerTeam=CustomWinner.Default;}}
namespace TownOfHost.Roles.Core.Interfaces{public interface ISchrodingerCatOwner{public enum TeamType{None,Crew,Impostor,Jackal}}}

