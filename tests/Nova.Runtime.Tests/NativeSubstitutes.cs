using TownOfHost;
using TownOfHost.Roles.Core;
using UnityEngine;
namespace UnityEngine {
 public partial struct Vector3 {public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);}
 public partial struct Vector2 {
  public float magnitude=>MathF.Sqrt(sqrMagnitude);public static Vector2 zero=>new(0,0);public static Vector2 one=>new(1,1);
  public static Vector2 operator +(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y);public static Vector2 operator *(Vector2 a,float b)=>new(a.x*b,a.y*b);public static Vector2 operator /(Vector2 a,float b)=>new(a.x/b,a.y/b);
  public static float Dot(Vector2 a,Vector2 b)=>a.x*b.x+a.y*b.y;
 }
 public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}public static explicit operator Vector2(Vector2Int a)=>new(a.x,a.y);}
 public partial class Transform {public Vector3 lossyScale=new(1,1,1);public Vector3 TransformPoint(Vector3 p)=>new(position.x+p.x*lossyScale.x,position.y+p.y*lossyScale.y,position.z+p.z*lossyScale.z);}
 public static partial class Mathf {
  public const float Deg2Rad=MathF.PI/180f,Rad2Deg=180f/MathF.PI;
  public static float Abs(float x)=>MathF.Abs(x);public static float Sin(float x)=>MathF.Sin(x);public static float Cos(float x)=>MathF.Cos(x);public static float Atan2(float y,float x)=>MathF.Atan2(y,x);
  public static int CeilToInt(float x)=>(int)MathF.Ceiling(x);public static int FloorToInt(float x)=>(int)MathF.Floor(x);public static int RoundToInt(float x)=>(int)MathF.Round(x);
  public static float Lerp(float a,float b,float t)=>a+(b-a)*t;public static float DeltaAngle(float a,float b){float d=((b-a)%360+360)%360;return d>180?d-360:d;}
  public static float MoveTowardsAngle(float a,float b,float d){float delta=DeltaAngle(a,b);return a+Math.Clamp(delta,-d,d);}
 }
 public class Color {public static Color clear=new();}
 public static class ColorUtility {public static string ToHtmlStringRGB(Color c)=>"ABCDEF";}
 public class SpriteRenderer {public bool enabled=true;}
 public class Material {public float OutlineWidth;public bool HasProperty(string name)=>name=="_OutlineWidth";public float GetFloat(string name)=>OutlineWidth;}
 public static class Resources {public static int LoadCalls;public static Dictionary<string,object> Assets=new();public static object Load(string name,Type type){LoadCalls++;return Assets.GetValueOrDefault(name);}}
 public struct Bounds {public Vector3 center;}
}
namespace TMPro {
 public class TMP_Settings{public static string defaultFontAssetPath="Fonts & Materials/";}
 public class TMP_FontAsset {public string name="Barlow-Black SDF";public Material material=new();public Dictionary<char,TMP_Character> characterLookupTable=new();public FaceInfo faceInfo=new();}
 public class TMP_Character {public Glyph glyph=new();}public class Glyph{public GlyphMetrics metrics=new();}
 public class GlyphMetrics{public float width,height,horizontalBearingX,horizontalBearingY,horizontalAdvance;}
 public class FaceInfo{public float scale=1,pointSize=1,ascentLine=1,descentLine;}
 public class TMP_TextInfo {public int characterCount;public TMP_CharacterInfo[] characterInfo;public TMP_MeshInfo[] meshInfo;public Vector3[] InkVertices;}
 public struct TMP_MeshInfo {public Vector3[] vertices;}
 public struct TMP_Vertex {public Vector3 position;}
 public struct TMP_CharacterInfo {public float scale;public TMP_Character textElement;public char character;public bool isVisible;public int vertexIndex,materialReferenceIndex;public TMP_Vertex vertex_BL,vertex_TL,vertex_TR,vertex_BR;}
 public class TextMeshPro {
  public static bool ThrowLayout,WrongFar,MissingGlyph,StaleWorkingVertices,MissingMesh,NonlinearMiddle,DegenerateAxis;
  public static string InkFault="";public static bool WideGlyphAvailable;public static float GeometryPadding;public static int LayoutCalls;
  public static float UnitsX=.25f,UnitsY=.25f,PreferredWidth=2.5f;public static int GlyphLimit=12;
  public static void ResetLayout(){Resources.Assets.Clear();Resources.LoadCalls=0;InkFault="";WideGlyphAvailable=false;GeometryPadding=0;LayoutCalls=0;ThrowLayout=WrongFar=MissingGlyph=StaleWorkingVertices=MissingMesh=NonlinearMiddle=DegenerateAxis=false;UnitsX=UnitsY=.25f;PreferredWidth=2.5f;GlyphLimit=12;}
  public string text="";public Color color=new();public Transform transform=new();public Bounds textBounds=new();public TMP_TextInfo textInfo=new();
  public TMP_FontAsset font=MakeFont();
  static TMP_FontAsset MakeFont(){var f=new TMP_FontAsset();f.characterLookupTable['-']=new(){glyph=new(){metrics=new(){width=.568f,height=.25f,horizontalAdvance=.6f,horizontalBearingY=.25f}}};if(WideGlyphAvailable)f.characterLookupTable['\u2014']=new(){glyph=new(){metrics=new(){width=.568f*(51.953125f/32.625f),height=.25f,horizontalAdvance=.99f,horizontalBearingY=.25f}}};return f;}
  public Vector2 GetPreferredValues(string value)=>new(PreferredWidth,2.5f);
  public void ForceMeshUpdate(bool a,bool b){LayoutCalls++;
   if(!text.Contains("<nobr>"))return;
   if(ThrowLayout)throw new OutOfMemoryException("synthetic native layout");
   var activeFont=font;
   float em=.25f,size=1f,x=0,y=0,angle=0;bool visible=true;var chars=new List<TMP_CharacterInfo>();var vertices=new List<Vector3>();var ink=new List<Vector3>();int bars=System.Text.RegularExpressions.Regex.Matches(text,@"em>[-_—]").Count;
   foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text,"<[^>]*>|[^<]")){
    string token=m.Value;
    float Value(string prefix,string suffix)=>float.Parse(token.Substring(prefix.Length,token.Length-prefix.Length-suffix.Length),System.Globalization.CultureInfo.InvariantCulture);
    if(token.StartsWith("<font=")){var assetName=token.Substring(6,token.Length-7).Trim('"');activeFont=Resources.Assets.GetValueOrDefault("Fonts & Materials/"+assetName) as TMP_FontAsset??font;continue;}
    if(token.StartsWith("<size=")){size=Value("<size=","%>")/100f;em=.25f*size;continue;}
    if(token.StartsWith("<pos=")){x=Value("<pos=","em>")*UnitsX*size;continue;}
    if(token.StartsWith("<voffset=")){y=DegenerateAxis?0:Value("<voffset=","em>")*UnitsY*size;continue;}
    if(token.StartsWith("<rotate=")){angle=Value("<rotate=",">")*Mathf.Deg2Rad;continue;}
    if(token=="</rotate>"){angle=0;continue;}
    if(token.StartsWith("<alpha=")){visible=token!="<alpha=#00>";continue;}
    if(token.StartsWith("<"))continue;
    float cx=x+.01f,cy=y+.7f;if(WrongFar&&MathF.Abs(x)+MathF.Abs(y)>20){cx=x*.5f+.01f;cy=y*.5f+.7f;}
    if(NonlinearMiddle&&(token=="-"||token=="_"||token=="—")&&bars>1)cx+=.2f;
    Vector3 Vertex(float dx,float dy)=>new(cx+dx*MathF.Cos(angle)-dy*MathF.Sin(angle),cy+dx*MathF.Sin(angle)+dy*MathF.Cos(angle));
    var element=activeFont!=null&&activeFont.characterLookupTable.TryGetValue(token[0],out var known)?known:new TMP_Character{glyph=new(){metrics=new(){width=.568f,height=.25f}}};
    float elementScale=em*activeFont.faceInfo.scale/activeFont.faceInfo.pointSize;
    float iw=element.glyph.metrics.width*elementScale*.5f,ih=element.glyph.metrics.height*elementScale*.5f;
    ink.AddRange(new[]{Vertex(-iw,-ih),Vertex(-iw,ih),Vertex(iw,ih),Vertex(iw,-ih)});
    float w=iw+GeometryPadding*elementScale,h=ih+GeometryPadding*elementScale;
    int vi=vertices.Count;var bl=Vertex(-w,-h);var tl=Vertex(-w,h);var tr=Vertex(w,h);var br=Vertex(w,-h);vertices.AddRange(new[]{bl,tl,tr,br});
    chars.Add(new(){scale=InkFault=="nan-scale"?float.NaN:InkFault=="negative-scale"?-1:InkFault=="oversize-metrics"?elementScale*3:elementScale,textElement=InkFault=="missing-metrics"?null:element,character=MissingGlyph?'?':token[0],isVisible=visible&&bars<=GlyphLimit,vertexIndex=vi,materialReferenceIndex=0,
     vertex_BL=new(){position=StaleWorkingVertices?default:bl},vertex_TL=new(){position=StaleWorkingVertices?default:tl},vertex_TR=new(){position=StaleWorkingVertices?default:tr},vertex_BR=new(){position=StaleWorkingVertices?default:br}});
   }
   textInfo=new(){InkVertices=ink.ToArray(),characterCount=chars.Count,characterInfo=chars.ToArray(),meshInfo=MissingMesh?null:new[]{new TMP_MeshInfo{vertices=vertices.ToArray()}}};
  }
 }

}
namespace Il2CppInterop.Runtime {public static class Il2CppType {public static Type Of<T>()=>typeof(T);}}
public static class ObjectCast {public static T TryCast<T>(this object x)where T:class=>x as T;}
public class Outfit {public bool ThrowNameRead;public int NameReads;public string RawName="host";public string PlayerName{get{NameReads++;if(ThrowNameRead)throw new OutOfMemoryException("synthetic native string");return RawName;}set{RawName=value;}}public string HatId="hat",SkinId="skin",PetId="pet",VisorId="visor";public int ColorId=2;}
public enum PlayerOutfitType{Default}
public partial class NetworkedPlayerInfo {public Dictionary<PlayerOutfitType,Outfit> Outfits=new(){{PlayerOutfitType.Default,new()}};public Outfit DefaultOutfit=>Outfits[PlayerOutfitType.Default];}
public class Cosmetics {public bool FlipX;public TMPro.TextMeshPro nameText=new(),colorBlindText=new();}
public partial class PlayerControl {
 public bool onLadder;public Cosmetics cosmetics=new();public bool IsTeammate(PlayerControl target)=>Role.IsImpostor()&&target.Role.IsImpostor();
 public T[] GetComponentsInChildren<T>(bool active)=>Array.Empty<T>();
}
namespace TownOfHost {
 public static class Palette{public static Color ImpostorRed=new();public static Color[] PlayerColors=Enumerable.Range(0,32).Select(_=>new Color()).ToArray();}
 public static class PhysicsHelpers {public static Func<Vector2,Vector2,float,int,bool> Block;public static bool AnyNonTriggersBetween(Vector2 a,Vector2 d,float length,int mask)=>Block?.Invoke(a,d,length,mask)??false;}
 public static class Constants{public const int ShipAndObjectsMask=1;}
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
  public static bool ThrowAppearance,ThrowMove;public static int RawNameCalls,VisualAttempts;public static List<string> Names=new();public static List<Vector2> Moves=new();public static List<string> EventOrder=new();public static List<(float time,int index,string name)> NameEvents=new();public static List<(float time,int index,Vector2 pos)> MoveEvents=new();
  public PlayerControl PlayerControl;public Vector2 Position;protected virtual bool CanCreate=>true;protected virtual bool BatchSpawnAssignments=>false;protected virtual float SpawnInterval=>.4f;
  protected void CreateNetObject(Vector2 p){pending.Enqueue((this,p));Process();}
  static void Process(){if(spawning||pending.Count==0)return;spawning=true;var(next,pos)=pending.Dequeue();_ =new LateTask(()=>{if(next.CanCreate){next.PlayerControl=new(){PlayerId=254};next.PlayerControl.transform.position=pos;next.PlayerControl.cosmetics.nameText.transform.position=pos;next.Position=pos;AllObjects.Add(next);next.OnCreated();}spawning=false;Process();},.4f);}
  protected void SetAppearance(int colorId,string skinId="",string hatId="",string petId="",string visorId="",string displayName=null){var o=global::PlayerControl.LocalPlayer.Data.DefaultOutfit;var old=(o.PlayerName,o.ColorId,o.HatId,o.SkinId,o.PetId,o.VisorId);o.PlayerName=displayName??o.PlayerName;o.ColorId=colorId;o.HatId=hatId;o.SkinId=skinId;o.PetId=petId;o.VisorId=visorId;if(ThrowAppearance)throw new InvalidOperationException("synthetic serialize failure");Names.Add(o.PlayerName);(o.PlayerName,o.ColorId,o.HatId,o.SkinId,o.PetId,o.VisorId)=old;}
  protected void SetVisualName(string name){VisualAttempts++;if(ThrowAppearance)throw new OutOfMemoryException("synthetic native visual failure");Names.Add(name);NameEvents.Add((Time.time,AllObjects.IndexOf(this),name));EventOrder.Add("N:"+AllObjects.IndexOf(this));}
  protected void SetName(string name){RawNameCalls++;throw new InvalidOperationException("Unsafe raw dummy name RPC");}
  protected void SnapToPosition(Vector2 pos){if(ThrowMove)throw new InvalidOperationException("synthetic move failure");Position=pos;PlayerControl.transform.position=pos;PlayerControl.cosmetics.nameText.transform.position=pos;Moves.Add(pos);MoveEvents.Add((Time.time,AllObjects.IndexOf(this),pos));EventOrder.Add("M:"+AllObjects.IndexOf(this));}
  public void Despawn(){AllObjects.Remove(this);PlayerControl=null;}
  protected virtual void OnCreated(){}protected virtual void OnCreateFailed(){}protected virtual void OnFixedUpdate(){}public virtual void OnMeeting(){}
  public static void Tick(){foreach(var o in AllObjects.ToArray())o.OnFixedUpdate();}
  public static void Reset(){AllObjects.Clear();pending.Clear();spawning=false;ThrowAppearance=ThrowMove=false;RawNameCalls=VisualAttempts=0;Names.Clear();Moves.Clear();NameEvents.Clear();MoveEvents.Clear();EventOrder.Clear();}
 }
}

public enum MapNames : byte { Skeld=0, Mira=1, Polus=2, Dleks=3, Airship=4, Fungle=5 }
