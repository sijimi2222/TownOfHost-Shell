using TownOfHost;
using TownOfHost.Roles.Core;
using UnityEngine;
namespace UnityEngine {
 public partial struct Vector2 {
  public float magnitude=>MathF.Sqrt(sqrMagnitude);public static Vector2 zero=>new(0,0);public static Vector2 one=>new(1,1);
  public static Vector2 operator +(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y);public static Vector2 operator *(Vector2 a,float b)=>new(a.x*b,a.y*b);public static Vector2 operator /(Vector2 a,float b)=>new(a.x/b,a.y/b);
  public static float Dot(Vector2 a,Vector2 b)=>a.x*b.x+a.y*b.y;
 }
 public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}public static explicit operator Vector2(Vector2Int a)=>new(a.x,a.y);}
 public partial class Transform {public Vector3 lossyScale=new(1,1,1);public Vector3 TransformPoint(Vector3 p)=>new(position.x+p.x,position.y+p.y,position.z+p.z);}
 public static partial class Mathf {
  public const float Deg2Rad=MathF.PI/180f,Rad2Deg=180f/MathF.PI;
  public static float Abs(float x)=>MathF.Abs(x);public static float Sin(float x)=>MathF.Sin(x);public static float Cos(float x)=>MathF.Cos(x);public static float Atan2(float y,float x)=>MathF.Atan2(y,x);
  public static int CeilToInt(float x)=>(int)MathF.Ceiling(x);public static int FloorToInt(float x)=>(int)MathF.Floor(x);public static int RoundToInt(float x)=>(int)MathF.Round(x);
  public static float Lerp(float a,float b,float t)=>a+(b-a)*t;public static float DeltaAngle(float a,float b){float d=((b-a)%360+360)%360;return d>180?d-360:d;}
  public static float MoveTowardsAngle(float a,float b,float d){float delta=DeltaAngle(a,b);return a+Math.Clamp(delta,-d,d);}
 }
 public partial class Color {public static Color clear=new();}
 public static class ColorUtility {public static string ToHtmlStringRGB(Color c)=>"ABCDEF";}
 public class SpriteRenderer {public bool enabled=true;}
 public class Material {public bool HasProperty(string name)=>false;public float GetFloat(string name)=>0;}
 public static class Resources {public static object Load(string name,Type type)=>null;}
 public struct Bounds {public Vector3 center;}
}
namespace TMPro {
 public class TMP_Settings{public static string defaultFontAssetPath="Fonts & Materials/";}
 public class TMP_FontAsset {public Material material=new();public Dictionary<char,TMP_Character> characterLookupTable=new();public FaceInfo faceInfo=new();}
 public class TMP_Character {public Glyph glyph=new();}public class Glyph{public GlyphMetrics metrics=new();}
 public class GlyphMetrics{public float width,height,horizontalBearingX,horizontalBearingY,horizontalAdvance;}
 public class FaceInfo{public float scale=1,pointSize=1,ascentLine=1,descentLine;}
 public class TextMeshPro {public string text="";public Color color=new();public Transform transform=new();public Bounds textBounds=new();public Vector2 GetPreferredValues(string text)=>new(2.5f,2.5f);public void ForceMeshUpdate(bool a,bool b){}}
}
namespace Il2CppInterop.Runtime {public static class Il2CppType {public static Type Of<T>()=>typeof(T);}}
public static class ObjectCast {public static T TryCast<T>(this object x)where T:class=>x as T;}
public class Outfit {public string PlayerName="host",HatId="hat",SkinId="skin",PetId="pet",VisorId="visor";public int ColorId=2;}
public enum PlayerOutfitType{Default}
public partial class NetworkedPlayerInfo {public Dictionary<PlayerOutfitType,Outfit> Outfits=new(){{PlayerOutfitType.Default,new()}};public Outfit DefaultOutfit=>Outfits[PlayerOutfitType.Default];}
public class Cosmetics {public bool FlipX;public TMPro.TextMeshPro nameText=new(),colorBlindText=new();}
public partial class PlayerControl {
 public bool onLadder;public Cosmetics cosmetics=new();public bool IsTeammate(PlayerControl target)=>Role.IsImpostor()&&target.Role.IsImpostor();
 public T[] GetComponentsInChildren<T>(bool active)=>Array.Empty<T>();
}
namespace TownOfHost {
 public static partial class Palette{public static Color ImpostorRed=new();public static Color[] PlayerColors=Enumerable.Range(0,32).Select(_=>new Color()).ToArray();}
 public static class PhysicsHelpers {public static Func<Vector2,Vector2,float,int,bool> Block;public static bool AnyNonTriggersBetween(Vector2 a,Vector2 d,float length,int mask)=>Block?.Invoke(a,d,length,mask)??false;}
 public static partial class Constants{public const int ShipAndObjectsMask=1;}
 public static partial class Utils {public static int Flashes;public static void AllPlayerKillFlash()=>Flashes++;public static string ColorString(Color c,string text)=>text;}
 public static partial class RoleExt {public static bool IsMainRole(this CustomRoles role) => role < CustomRoles.NotAssigned || role == CustomRoles.Nova;}
}
namespace TownOfHost.Roles.Madmate{public static class Madpsycho{public static bool IsReflectionAttempt(MurderInfo info)=>false;}}
namespace Hazel {public partial class MessageReader {public float ReadSingle(){var data=new byte[4];for(int i=0;i<4;i++)data[i]=ReadByte();return BitConverter.ToSingle(data);}}}
namespace TownOfHost.Modules {
 public static class NetworkSafety {public const int NameByteLimit=800;}
 // Game/network substitute only. Native rendering is deliberately not simulated visually.
 public class CustomNetObject {
  public static List<CustomNetObject> AllObjects=new();static Queue<(CustomNetObject Item,Vector2 Pos)> pending=new();static bool spawning;
  public static bool ThrowAppearance,ThrowMove;public static int RawNameCalls;public static List<string> Names=new();public static List<Vector2> Moves=new();
  public PlayerControl PlayerControl;public Vector2 Position;protected virtual bool CanCreate=>true;protected virtual bool DelayPlayerIdReset=>false;
  protected void CreateNetObject(Vector2 p){pending.Enqueue((this,p));Process();}
  static void Process(){if(spawning||pending.Count==0)return;spawning=true;var(next,pos)=pending.Dequeue();_ =new LateTask(()=>{if(next.CanCreate){next.PlayerControl=new(){PlayerId=254};next.PlayerControl.transform.position=pos;next.PlayerControl.cosmetics.nameText.transform.position=pos;next.Position=pos;AllObjects.Add(next);next.OnCreated();}spawning=false;Process();},.4f);}
  protected void SetAppearance(int colorId,string skinId="",string hatId="",string petId="",string visorId="",string displayName=null){var o=global::PlayerControl.LocalPlayer.Data.DefaultOutfit;var old=(o.PlayerName,o.ColorId,o.HatId,o.SkinId,o.PetId,o.VisorId);o.PlayerName=displayName??o.PlayerName;o.ColorId=colorId;o.HatId=hatId;o.SkinId=skinId;o.PetId=petId;o.VisorId=visorId;if(ThrowAppearance)throw new InvalidOperationException("synthetic serialize failure");Names.Add(o.PlayerName);(o.PlayerName,o.ColorId,o.HatId,o.SkinId,o.PetId,o.VisorId)=old;}
  protected void SetName(string name){RawNameCalls++;throw new InvalidOperationException("Unsafe raw dummy name RPC");}
  protected void SnapToPosition(Vector2 pos){if(ThrowMove)throw new InvalidOperationException("synthetic move failure");Position=pos;PlayerControl.transform.position=pos;Moves.Add(pos);}
  public void Despawn(){AllObjects.Remove(this);PlayerControl=null;}
  protected virtual void OnCreated(){}protected virtual void OnFixedUpdate(){}public virtual void OnMeeting(){}
  public static void Tick(){foreach(var o in AllObjects.ToArray())o.OnFixedUpdate();}
  public static void Reset(){AllObjects.Clear();pending.Clear();spawning=false;ThrowAppearance=ThrowMove=false;RawNameCalls=0;Names.Clear();Moves.Clear();}
 }
}
