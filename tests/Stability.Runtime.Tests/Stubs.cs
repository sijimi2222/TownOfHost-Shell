using System;using System.Collections.Generic;using System.Linq;
namespace HarmonyLib {[AttributeUsage(AttributeTargets.All,AllowMultiple=true)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string n){}public HarmonyPatch(Type t){}public HarmonyPatch(string n){}}public class HarmonyPostfix:Attribute{}}
namespace AmongUs.GameOptions {public enum RoleTypes {Crewmate,Impostor}}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays {public class Il2CppStructArray<T>:List<T>{}}
namespace UnityEngine {
 public class Object {public string name="synthetic";public bool Destroyed;public static implicit operator bool(Object o)=>o!=null&&!o.Destroyed;public static void Destroy(Object o){o.Destroyed=true;}public static T Instantiate<T>(T p,Vector2 v,Quaternion q) where T:PlayerControl=>new PlayerControl() as T;}
 public class GameObject:Object {public bool activeInHierarchy=true;public Transform transform=new();public T GetComponent<T>()where T:new()=>new();public void SetActive(bool b)=>activeInHierarchy=b;}
 public class Transform:Object {public Vector3 position;public GameObject gameObject=>new();public Transform FindChild(string n)=>new();}
 public struct Quaternion {public static Quaternion identity=>new();}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}public static implicit operator Vector2(Vector3 p)=>new(p.x,p.y);}
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>new();public static float Distance(Vector2 a,Vector2 b)=>MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}
 public struct Color {public static Color clear=>new();}
}
namespace InnerNet {public class InnerNetObject:UnityEngine.Object{public uint NetId=100;}}
namespace Hazel {
 public enum SendOption {None,Reliable}
 public class MessageReader {public static List<MessageReader> Created=new();public List<string> Trace;public int Recycles;public static MessageReader Get(byte[] bytes){var r=new MessageReader{Trace=System.Text.Json.JsonSerializer.Deserialize<List<string>>(System.Text.Encoding.UTF8.GetString(bytes))};Created.Add(r);return r;}public void Recycle(){if(++Recycles>1)throw new Exception("reader double recycle");}}
 public class MessageWriter {public byte[] ToByteArray(bool header)=>System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(Trace));public static List<MessageWriter> Created=new();public static bool ThrowSerialize;public int Recycles;public List<string> Trace=new();public static MessageWriter Get(SendOption o){var w=new MessageWriter();Created.Add(w);return w;}public void Recycle(){if(++Recycles>1)throw new Exception("double recycle");}public void StartMessage(byte b)=>Trace.Add("start:"+b);public void EndMessage()=>Trace.Add("end");public void Write<T>(T v)=>Trace.Add("write:"+typeof(T).Name+":"+v);public void Write(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> b,int o,int n)=>Trace.Add("bytes");public void WritePacked<T>(T v)=>Trace.Add("packed:"+v);public void WriteBytesAndSize(object b)=>Trace.Add("byteSize");public void WriteNetObject(InnerNet.InnerNetObject o)=>Trace.Add("net:"+o.NetId);}
}
public enum RpcCalls:byte {SetName=6,Shapeshift=46,MurderPlayer=12,SetRole=44,SnapTo=21}
public enum MurderResultFlags {FailedError}
public enum SpawnFlags:byte {None}
public enum PlayerOutfitType {Default}
public class Outfit {public bool ThrowNameRead;public int NameReads,NameWrites;public string RawName="original";public string PlayerName{get{NameReads++;if(ThrowNameRead)throw new OutOfMemoryException("synthetic invalid native string");return RawName;}set{NameWrites++;RawName=value;}}public string HatId="hat",SkinId="skin",PetId="pet",VisorId="visor";public int ColorId=7;}
public class NetworkedPlayerInfo:InnerNet.InnerNetObject {public class PlayerOutfit:Outfit{public static bool ThrowDeserialize;public void Deserialize(Hazel.MessageReader r){if(ThrowDeserialize)throw new OutOfMemoryException("synthetic native deserialize");if(r.Trace.Count!=12||r.Trace[1]!="packed:0"||r.Trace.Skip(7).Any(x=>x!="write:Byte:0"))throw new Exception("invalid native outfit layout");RawName=r.Trace[0].Substring("write:String:".Length);ColorId=0;HatId=PetId=SkinId=VisorId="";}}public Dictionary<PlayerOutfitType,PlayerOutfit> Outfits=new(){{PlayerOutfitType.Default,new()}};public byte PlayerId;public bool Disconnected;public PlayerControl Object;public int SerializeCalls,ThrowOnSerializeCall;public void Serialize(Hazel.MessageWriter w,bool initial){if(++SerializeCalls==ThrowOnSerializeCall)throw new InvalidOperationException("synthetic restore serialization");if(Hazel.MessageWriter.ThrowSerialize)throw new InvalidOperationException("synthetic serialization");w.Trace.Add("outfit:"+Outfits[PlayerOutfitType.Default].RawName);}}
public class CosmeticText {public string text;public UnityEngine.Color color;}
public class Cosmetics {public CosmeticText nameText=new(),colorBlindText=new();}
public class NetTransform:InnerNet.InnerNetObject {public ushort lastSequenceId;public void SnapTo(UnityEngine.Vector2 p){}}
public class PlayerControl:InnerNet.InnerNetObject {public static PlayerControl LocalPlayer;public static List<PlayerControl> AllPlayerControls=new();public byte PlayerId;public bool isNew,notRealPlayer,AmOwner,Visible;public int OwnerId=1;public NetworkedPlayerInfo Data=new(),CachedPlayerData;public NetTransform NetTransform=new();public UnityEngine.GameObject gameObject=new();public UnityEngine.Transform transform=>gameObject.transform;public Cosmetics cosmetics=new();public UnityEngine.Vector2 GetTruePosition()=>new();public int Shapeshifts;public void Shapeshift(PlayerControl p,bool b){Shapeshifts++;} }
public class GameData {public static GameData Instance=new();public List<NetworkedPlayerInfo> AllPlayers=new();}
public class AmongUsClient:UnityEngine.Object {public enum GameStates{NotJoined,Joined,Started,Ended}public static AmongUsClient Instance;public bool AmHost=true,AmConnected=true;public int GameId=7;public uint NetIdCnt=20;public GameStates GameState=GameStates.Ended;public PlayerControl PlayerPrefab=new();public UnityEngine.AddressableAssets.AssetReference[] ShipPrefabs=Array.Empty<UnityEngine.AddressableAssets.AssetReference>();public void Awake(){}public void OnGameJoined(){}public void StartCoroutine(System.Collections.IEnumerator e){}public static Action BeforeSend;public static bool ThrowSend;public static int Sends;public static List<string[]> Packets=new();public void SendOrDisconnect(Hazel.MessageWriter w){Sends++;BeforeSend?.Invoke();if(ThrowSend)throw new InvalidOperationException("synthetic send");Packets.Add(w.Trace.ToArray());}public void RemoveNetObject(InnerNet.InnerNetObject o){}public AmongUs.InnerNet.GameDataMessages.SpawnGameDataMessage CreateSpawnMessage(PlayerControl p,int id,SpawnFlags f)=>new();public Hazel.MessageWriter StartRpcImmediately(uint n,byte rpc,Hazel.SendOption o)=>Hazel.MessageWriter.Get(o);public void FinishRpcImmediately(Hazel.MessageWriter w){SendOrDisconnect(w);w.Recycle();}}
namespace AmongUs.InnerNet.GameDataMessages {public class SpawnGameDataMessage {public void SerializeValues(Hazel.MessageWriter w)=>w.Trace.Add("spawn");}}
public class EndGameManager:UnityEngine.Object {public UnityEngine.GameObject gameObject=new();public void ShowButtons(){}}
public class EndGameNavigation {public int Calls;public void NextGame(){Calls++;}}
public class DestroyableSingleton<T> where T:new(){public static bool InstanceExists=true;public static int Gets;public static T Value=new();public static T Instance{get{Gets++;return Value;}}}
namespace TownOfHost {
 public static class Logger {public static List<string> Errors=new();public static void Error(string s,string c,bool b=true)=>Errors.Add(c);public static void Warn(string s,string c){}public static void Info(string s,string c){}}
 public static class Main {public static bool DebugVersion,SetRoleOverride;public static Toggle PreloadMapAssets=new();}
 public static class GameStates {public static bool IsLobby,IsEnded;}
 public class Toggle{public bool Value;public bool GetBool()=>Value;}
 public static class Options {public static Toggle OptionAutoReturnRoom=new(){Value=true},OptionAutoReturnRoomGM=new(),EnableGM=new();}
 public static class GameModeManager {public static bool IsStandardClass()=>true;}
 public static class ExtendedPlayerControl {public const int SuccessFlags=1;}
 public static class PlayerCatch {public static List<PlayerControl> AllPlayerControls=new();}
 public static class NetHelpers {public static void WriteVector2(UnityEngine.Vector2 p,Hazel.MessageWriter w){w.Write(p.x);w.Write(p.y);}}
}
namespace TownOfHost.Patches {public static class PeriodicAutoRehostPatch {public static bool Scheduled;public static bool IsScheduledForThisGame()=>Scheduled;}}




