using System; using System.Linq; using System.Collections.Generic;
namespace AmongUs.GameOptions { public enum RoleTypes{Impostor,Crewmate,Engineer,Scientist,Noisemaker,Tracker,Detective,Judge,GuardianAngel,SpiritGuide} }
namespace InnerNet { }
namespace UnityEngine { public static class Time { public static float fixedDeltaTime=.02f; } public static class Screen { public static bool fullScreen; } public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);} public class GameObject{ public bool activeInHierarchy=true; } }
public class TextBoxTMP { public UnityEngine.GameObject gameObject=new(); public bool isActiveAndEnabled=true; public int Calls; public void GiveFocus()=>Calls++; }
namespace Hazel { public enum SendOption{Reliable}  public class MessageReader { Queue<object> q;public MessageReader(IEnumerable<object> data){q=new(data);}public int ReadInt32()=>(int)q.Dequeue();public bool ReadBoolean()=>(bool)q.Dequeue();public float ReadSingle()=>(float)q.Dequeue(); } }
public class AmongUsClient { public static AmongUsClient Instance=new();public bool AmHost=true; }
public class NativeRole{public bool IsImpostor=true;}
public class NetworkedPlayerInfo {public NativeRole Role=new();public AmongUs.GameOptions.RoleTypes RoleType=AmongUs.GameOptions.RoleTypes.Impostor;public bool IsDead; public byte PlayerId;public bool Disconnected; public List<TaskInfo> Tasks=new(); }
public class TaskInfo { public uint Id; }
public class Outfit {public byte ColorId;}
public class PlayerControl {public static Dictionary<byte,PlayerControl> Players=new();public TaskView View=new(); public static PlayerControl LocalPlayer;public int ClientId=2;public byte Color;public Outfit CurrentOutfit=new();public void SetColor(byte c)=>Color=c;public void SetHat(string s,byte c){}public void SetSkin(string s,byte c){}public void SetVisor(string s,byte c){}public void SetPet(string s,byte c){}public int GetNextRpcSequenceId(TownOfHost.RpcCalls c)=>1; public byte PlayerId;public uint NetId=100;public bool Alive=true;public NetworkedPlayerInfo Data=new();public TownOfHost.Roles.Impostor.Experiencer Active;public float Cooldown;public int Exiles,Syncs;public List<TownOfHost.Roles.Core.CustomRoles> Addons=new(); }
namespace TownOfHost {
public enum GeneralOption{KillCooldown} public enum OptionFormat{Seconds} public enum OptionZeroNotation{Off} public enum Sounds{TaskComplete}
public record NumberRule(float Min,float Max,float Step);
public class OptionItem { public static Dictionary<int,OptionItem> Items=new(); public float Value; public int Id;public object Key; public OptionItem Parent; public Func<bool> Enabled;
 public int GetInt()=>(int)Value;public float GetFloat()=>Value;public OptionItem SetValueFormat(OptionFormat f)=>this;public OptionItem SetZeroNotation(OptionZeroNotation n)=>this;public OptionItem SetEnabled(Func<bool> f){Enabled=f;return this;}
 public static OptionItem Add(TownOfHost.Roles.Core.SimpleRoleInfo info,int offset,object key,float value,OptionItem parent){var o=new OptionItem{Id=info.ConfigId+offset,Key=key,Value=value,Parent=parent};Items.Add(o.Id,o);return o;}}
public class FloatOptionItem:OptionItem {public static OptionItem Create(TownOfHost.Roles.Core.SimpleRoleInfo i,int offset,object key,NumberRule r,float value,bool single,OptionItem parent=null)=>Add(i,offset,key,value,parent);}
public class IntegerOptionItem:OptionItem {public static OptionItem Create(TownOfHost.Roles.Core.SimpleRoleInfo i,int offset,object key,NumberRule r,int value,bool single,OptionItem parent=null)=>Add(i,offset,key,value,parent);}
public static class GameStates{public static bool IsMeeting=true,IsInTask=true,IsLobby,IsFreePlay;public static bool InGame=>!IsLobby;}
public static class Main{public static List<byte> DisableTaskPlayerList=new();public static Dictionary<byte,float> AllPlayerKillCooldown=new();}
public static class Utils{public static List<byte> RoleSendList=new();public static bool IsRestriction()=>false;public static List<(string Text,byte Target)> Messages=new(); public static void SendMessage(string text,byte id=255)=>Messages.Add((text,id));}
public static class RPC{public static void PlaySoundRPC(byte id,Sounds sound){} }
public static class Logger{public static void Info(string t,string tag){}public static void Error(string t,string tag){}public static void Warn(string t,string tag){} }
public static class Ext{ public static int GetClientId(this PlayerControl p)=>p.ClientId;public static TownOfHost.Roles.Impostor.Experiencer GetRoleClass(this PlayerControl p)=>p.Active;public static bool IsAlive(this PlayerControl p)=>p!=null&&p.Alive&&!p.Data.Disconnected;public static void SetKillCooldown(this PlayerControl p)=>p.Cooldown=p.Active.CalculateKillCooldown();public static void SyncSettings(this PlayerControl p)=>p.Syncs++;public static void RpcProtectedMurderPlayer(this PlayerControl p){}public static void RpcSetCustomRole(this PlayerControl p,TownOfHost.Roles.Core.CustomRoles role)=>p.Addons.Add(role);public static void RpcExileV3(this PlayerControl p){p.Alive=false;p.Exiles++;}}
}
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

   
} public enum CustomRoleTypes{Impostor,Crewmate,Madmate,Neutral}public enum From{TownOfHost_hamo}
public class SimpleRoleInfo {public Func<AmongUs.GameOptions.RoleTypes> BaseRoleType=()=>AmongUs.GameOptions.RoleTypes.Impostor;public bool IsDesyncImpostor;public int ConfigId;public Action Setup;public CustomRoles RoleName;public bool New;public static SimpleRoleInfo Create(Type t,Func<PlayerControl,TownOfHost.Roles.Impostor.Experiencer> factory,CustomRoles role,Func<AmongUs.GameOptions.RoleTypes> native,CustomRoleTypes camp,int id,Action options,string alias,(int,int)? OptionSort=null,From from=From.TownOfHost_hamo,bool isNewRole=false)=>new(){ConfigId=id,Setup=options,RoleName=role,New=isNewRole};}
public class TestWriter{public List<object> Values=new();public void Write(int v)=>Values.Add(v);public void Write(bool v)=>Values.Add(v);public void Write(float v)=>Values.Add(v);}
public class Sender:IDisposable{public TestWriter Writer=new();public void Dispose(){RoleBase.LastRpc=Writer.Values;}}
public enum HasTask{True,False,ForRecompute}
public class RoleBase {public HasTask HasTasks=HasTask.False;public bool CanTask()=>TownOfHost.UtilsTask.HasTasks(PlayerControl.LocalPlayer.Data,false);public PlayerControl Player;public static List<object> LastRpc;public RoleBase(SimpleRoleInfo i,PlayerControl p){Player=p;}public bool Is(PlayerControl p)=>p==Player;public static string GetString(string s)=>s;protected Sender CreateSender()=>new(); public enum DontReportreson{None,Impostor}
public virtual void Add(){}public virtual bool OnCompleteTask(uint id)=>true;public virtual void OnFixedUpdate(PlayerControl p){}public virtual void OnReportDeadBody(PlayerControl p,NetworkedPlayerInfo n){}public virtual void OnDead(PlayerControl p){}public virtual void OnDestroy(){}public virtual bool CancelReportDeadBody(PlayerControl p,NetworkedPlayerInfo n,ref DontReportreson r)=>false;public virtual void ReceiveRPC(Hazel.MessageReader r){}public virtual string GetProgressText(bool comms=false,bool GameLog=false)=>"";}
public class MurderInfo {public PlayerControl AttemptKiller,AttemptTarget;public PlayerControl AppearanceKiller=>AttemptKiller;public PlayerControl AppearanceTarget=>AttemptTarget;public (PlayerControl,PlayerControl) AttemptTuple=>(AttemptKiller,AttemptTarget);public bool IsSuicide=>AttemptKiller==AttemptTarget;public bool IsFakeSuicide;public bool CanKill=true,DoKill=true,Guard;public bool CheckHasGuard()=>Guard; public MurderInfo(PlayerControl k,PlayerControl t){AttemptKiller=k;AttemptTarget=t;}}
public static class CustomRoleManager{public static RoleBase GetByPlayerId(byte id)=>PlayerControl.Players.GetValueOrDefault(id)?.Active;public static void OnTaskCompleteOthers(PlayerControl p,bool b){}public static int Resolved,Deaths;public static bool OnCheckMurder(PlayerControl k,PlayerControl t,PlayerControl ak,PlayerControl at,bool force=false){Resolved++;OnMurderPlayer(k,t);return true;} public static void OnMurderPlayer(PlayerControl k,PlayerControl t){Deaths++;k.Active.OnMurderPlayerAsKiller(new(k,t));}}
}
namespace TownOfHost.Roles.Core.Interfaces{public interface IImpostor{} }

namespace TownOfHost {
public static class Options{public static bool firstturnmeeting;public static CustomGameMode CurrentGameMode=CustomGameMode.Standard;public static FakeOption GhostIgnoreTasks=new();}public static class MeetingStates{public static bool FirstMeeting;}
public enum RpcCalls{SetColor,SetHatStr,SetSkinStr,SetVisorStr,SetPetStr}
public class CustomRpcSender{public static List<int> Destinations=new();int destination;public static CustomRpcSender Create(string n,Hazel.SendOption s)=>new();public void StartMessage(int id)=>destination=id;public CustomRpcSender StartRpc(uint id,RpcCalls rpc)=>this;public CustomRpcSender Write(object o)=>this;public void EndRpc(){}public void EndMessage(){}public void SendMessage()=>Destinations.Add(destination);}
}

public class TaskView{public bool hasTasks;public int CompletedTasksCount,AllTasksCount=1;public bool IsTaskFinished=>false;public void Update(PlayerControl p){}public bool HasCompletedEnoughCountOfTasks(int i)=>false;}
public class StateView{public TownOfHost.Roles.Core.CustomRoles MainRole=TownOfHost.Roles.Core.CustomRoles.Experiencer,GhostRole=TownOfHost.Roles.Core.CustomRoles.NotAssigned;public List<TownOfHost.Roles.Core.CustomRoles> SubRoles=new();}
public class FakeConsole{public bool AllowImpostor;}
public class GameData{public static GameData Instance=new();public void RecomputeTaskCounts(){} }
public class RoleManager{public static RoleManager Instance=new();public void SetRole(PlayerControl p,AmongUs.GameOptions.RoleTypes r){} }
namespace TownOfHost {
public class FakeOption{public bool GetBool()=>false;public int GetInt()=>0;}
public enum CustomGameMode{Standard,TaskBattle,MurderMystery,HideAndSeek}
public static class PlayerState{public static Dictionary<byte,StateView> States=new();public static StateView GetByPlayerId(byte id)=>States.GetValueOrDefault(id);}
public static class PipelineExt{public static string GetNameWithRole(this PlayerControl p)=>"test";public static string RemoveHtmlTags(this string s)=>s;public static TaskView GetPlayerTaskState(this PlayerControl p)=>p.View;public static TownOfHost.Roles.Core.CustomRoles GetCustomRole(this PlayerControl p)=>TownOfHost.Roles.Core.CustomRoles.Experiencer;public static TownOfHost.Roles.Core.SimpleRoleInfo GetRoleInfo(this TownOfHost.Roles.Core.CustomRoles r)=>TownOfHost.Roles.Impostor.Experiencer.RoleInfo;public static bool Is(this PlayerControl p,TownOfHost.Roles.Core.CustomRoles r)=>p.GetCustomRole()==r;public static bool Is(this PlayerControl p,TownOfHost.Roles.Core.CustomRoleTypes r)=>r==TownOfHost.Roles.Core.CustomRoleTypes.Impostor;public static bool IsImpostor(this TownOfHost.Roles.Core.CustomRoles r)=>r==TownOfHost.Roles.Core.CustomRoles.Experiencer;public static bool IsCrewmate(this TownOfHost.Roles.Core.CustomRoles r)=>!r.IsImpostor();public static bool IsCrewmate(this AmongUs.GameOptions.RoleTypes r)=>r!=AmongUs.GameOptions.RoleTypes.Impostor;public static bool CanUseKillButton(this PlayerControl p)=>true;public static void RpcSetRoleDesync(this PlayerControl p,AmongUs.GameOptions.RoleTypes r,int id){}public static void SetKillCooldown(this PlayerControl p,float f,bool force,bool delay){}public static void RpcResetAbilityCooldown(this PlayerControl p,bool Sync){}public static void RpcResetAbilityCooldown(this PlayerControl p){} }
public static class TaskBattle{public static bool TaskBattleCompleteTask(PlayerControl p,TaskView v)=>true;}
public static class MurderMystery{public static void OnCompleteTask(PlayerControl p){} }
public static class UtilsNotifyRoles{public static int Calls;public static void NotifyRoles()=>Calls++;}
public static class UtilsGameLog{public static void AddGameLog(string a,string b){} }
public static class UtilsName{public static string GetPlayerColor(PlayerControl p,bool b)=>"test";}
public static class RemoveDisableDevicesPatch{public static void UpdateDisableDevices(){} }
public class LateTask{public LateTask(Action a,float delay,string n){}}

}
namespace TownOfHost.Roles.Impostor{public class Clockworker:TownOfHost.Roles.Core.RoleBase{public bool InTaskMode;public Clockworker():base(null,null){} }}
namespace TownOfHost.Roles.AddOns.Common{public static class Amnesia{public static bool Allow=true;public static bool CheckAbility(PlayerControl p)=>Allow;public static TownOfHost.FakeOption OptionCanRealizeTask=new(),OptionRealizeTaskCount=new();public static void RemoveAmnesia(byte id){} }public static class Workhorse{public static bool OnCompleteTask(PlayerControl p)=>true;}}

namespace TownOfHost{public static class Translator{public static string GetString(string s)=>s;}public static class Lovers{public static List<byte> HaveLoverDontTaskPlayers=new();}}
