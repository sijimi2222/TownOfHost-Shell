using UnityEngine;
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.All,AllowMultiple=true)] public class HarmonyPatch:Attribute {public HarmonyPatch(){}public HarmonyPatch(Type t,string m){}}
 public class HarmonyPostfix:Attribute{}
}
namespace UnityEngine {
 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new(1,1,1);
  public static bool operator ==(Vector3 a,Vector3 b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;public static bool operator !=(Vector3 a,Vector3 b)=>!(a==b);
  public override bool Equals(object o)=>o is Vector3 v&&this==v;public override int GetHashCode()=>HashCode.Combine(x,y,z);
 }
 public class GameObject {
  public bool activeSelf=true;public GameObject parent;public bool activeInHierarchy=>activeSelf&&(parent==null||parent.activeInHierarchy)&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
  public Transform transform;public int Changes;public GameObject(){transform=new(this);}public void SetActive(bool v){activeSelf=v;Changes++;}
 }
 public class Transform {
  public GameObject gameObject;public Transform parent;public Vector3 localPosition,localScale=Vector3.one;public int PositionWrites,ParentChanges;
  public Transform(GameObject g){gameObject=g;}
  public Vector3 position {get=>parent==null?localPosition:parent.TransformPoint(localPosition);set{PositionWrites++;localPosition=parent==null?value:parent.InverseTransformPoint(value);}}
  public Vector3 lossyScale {get{var p=parent==null?Vector3.one:parent.lossyScale;return new(p.x*localScale.x,p.y*localScale.y,p.z*localScale.z);}}
  public Vector3 InverseTransformPoint(Vector3 p){var q=parent==null?p:parent.InverseTransformPoint(p);return new((q.x-localPosition.x)/localScale.x,(q.y-localPosition.y)/localScale.y,(q.z-localPosition.z)/localScale.z);}
  public Vector3 TransformPoint(Vector3 p){var q=new Vector3(localPosition.x+p.x*localScale.x,localPosition.y+p.y*localScale.y,localPosition.z+p.z*localScale.z);return parent==null?q:parent.TransformPoint(q);}
  public void SetParent(Transform p,bool worldPositionStays){var before=position;parent=p;ParentChanges++;if(worldPositionStays)position=before;}
 }
 public class Component {public GameObject gameObject=new();public Transform transform=>gameObject.transform;}
 public class SpriteRenderer:Component {
  public bool enabled=true;public float Width=1;public Vector3 StaleWorldDelta;
  public Bounds localBounds=>new(){min=new(-Width/2,-.5f,0),center=new(0,0,0)};
  public Bounds bounds {get{var min=transform.TransformPoint(localBounds.min);var c=transform.position;return new(){min=new(min.x+StaleWorldDelta.x,min.y+StaleWorldDelta.y,min.z),center=new(c.x+StaleWorldDelta.x,c.y+StaleWorldDelta.y,c.z)};}}
 }
 public struct Bounds {public Vector3 min,center;public Vector3 max=>new(2*center.x-min.x,2*center.y-min.y,2*center.z-min.z);}
 public class Camera:Component {
  public bool orthographic=true;public float orthographicSize=3,aspect=16f/9;
  public Vector3 WorldToViewportPoint(Vector3 p){var q=transform.InverseTransformPoint(p);return new(.5f+q.x/(2*orthographicSize*aspect),.5f+q.y/(2*orthographicSize),1);}
 }
 public static class Mathf {public static float Abs(float x)=>Math.Abs(x);public static float Max(float a,float b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);}
}
public class AspectPosition {
 public enum EdgeAlignments {RightTop}
 public UnityEngine.Vector3 DistanceFromEdge;public int Adjustments;public float RightEdge,NativeAnchorBias;
 public UnityEngine.Camera parentCam;public bool updateAlways;public EdgeAlignments Alignment;
 public static implicit operator bool(AspectPosition p)=>p!=null;
 public void AdjustPosition(){
  Adjustments++;var cam=parentCam??HudManager.Instance.UICamera;RightEdge=cam.orthographicSize*cam.aspect-DistanceFromEdge.x;
  if(TownOfHost.CredentialsPatch.pingTrackerCredential!=null)TownOfHost.CredentialsPatch.pingTrackerCredential.transform.localPosition=new(RightEdge+NativeAnchorBias,cam.orthographicSize-DistanceFromEdge.y,DistanceFromEdge.z);
 }
}
public class Button:UnityEngine.Component {}
public class ChatController:UnityEngine.Component {public Button chatButton=new();public int VisibleCalls;public bool IsOpenOrOpening;public void SetVisible(bool v){VisibleCalls++;gameObject.SetActive(v);chatButton.gameObject.SetActive(v);}}
public class OptionsMenuBehaviour:UnityEngine.Component {public bool IsOpen;public void Close(){IsOpen=false;}}
public class HudManager:UnityEngine.Component {public static HudManager Instance;public static bool InstanceExists=>Instance!=null;public Button SettingsButton=new(),MatchInfoButton=new();public ChatController Chat=new();public Camera UICamera=new();public OptionsMenuBehaviour GameMenu=new();}
public class DestroyableSingleton<T> {public static bool InstanceExists=>HudManager.InstanceExists;public static HudManager Instance=>HudManager.Instance;}
public class PlayerData {public bool IsDead,Disconnected;}
public class PlayerControl {public static PlayerControl LocalPlayer;public PlayerData Data=new();}
public static class GameSettingMenu {public static bool Instance;}
namespace TownOfHost {
 public static class GameStates {public static bool IsModHost=true,IsLobby,IsMeeting,IsInGame,IsInTask,Intro;}
 public class OptionItem {public bool Value;public bool GetBool()=>Value;}
 public static class Options {public static OptionItem OptionGameChatSetting=new();}
 public static class NaturalDisasters {public static bool IsThisMode;}
 public static partial class RoleGuideButtonPatch {
  public static SpriteRenderer _btnRenderer,guideFrameRenderer;public static bool isPanelOpen;public static int CloseCalls;
  const float GuideFrameGap=.42f;public static bool HasGuideButton=>_btnRenderer!=null&&_btnRenderer.gameObject.activeInHierarchy;
  static bool ShouldShowTopRightChatButton()=>GameStates.IsLobby||GameStates.IsMeeting;
  public static void ClosePanel(){CloseCalls++;isPanelOpen=false;}
 }
 public static partial class CredentialsPatch {public static AspectPosition pingTrackerCredentialAspectPos;public static TMPro.TextMeshPro pingTrackerCredential;}
}
namespace TMPro {
 public class TextMeshPro:UnityEngine.Component {
  public string text="TownOfHost-Shell v2.2.3\nゲームを終了しない";
  public float InkOverflow,PendingInkOverflow=float.NaN;public bool FailMesh,havePropertiesChanged;public int MeshUpdates;public Action DuringMeshUpdate;
  public void ForceMeshUpdate(){MeshUpdates++;DuringMeshUpdate?.Invoke();if(FailMesh)throw new Exception("synthetic unavailable font");if(!float.IsNaN(PendingInkOverflow)){InkOverflow=PendingInkOverflow;PendingInkOverflow=float.NaN;}havePropertiesChanged=false;}
  public UnityEngine.Bounds textBounds=>new(){min=new(-4+rectTransform.RightBias,-1,0),center=new((-4+2*rectTransform.RightBias+InkOverflow)/2,-.5f,0)};
  public Rect rectTransform;public TextMeshPro(){rectTransform=new(this);}
  public class Rect {
   readonly TextMeshPro owner;public float RightBias;public Rect(TextMeshPro o){owner=o;}
   public void GetWorldCorners(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<UnityEngine.Vector3> a){var t=owner.transform;a[0]=t.TransformPoint(new(-4+RightBias,-1,0));a[1]=t.TransformPoint(new(-4+RightBias,0,0));a[2]=t.TransformPoint(new(RightBias,0,0));a[3]=t.TransformPoint(new(RightBias,-1,0));}
  }
 }
}
namespace TownOfHost {public static class Logger {public static System.Collections.Generic.List<string> Lines=new();public static void Info(string s,string c)=>Lines.Add(s);public static void Warn(string s,string c)=>Lines.Add(s);}}

// Match the actual IL2CPP interop API: implicit conversion allocates a COPY;
// native writes do not flow back into the original managed T[].
namespace Il2CppInterop.Runtime.InteropTypes.Arrays {
 public sealed class Il2CppStructArray<T>:IEnumerable<T> where T:struct {
  readonly T[] items;public static int Allocations;
  public Il2CppStructArray(long length){items=new T[length];Allocations++;}
  public Il2CppStructArray(T[] source){items=(T[])source.Clone();Allocations++;}
  public static implicit operator Il2CppStructArray<T>(T[] a)=>a==null?null:new(a);
  public T this[int i]{get=>items[i];set=>items[i]=value;}
  public int Length=>items.Length;
  public IEnumerator<T> GetEnumerator()=>((IEnumerable<T>)items).GetEnumerator();
  System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>items.GetEnumerator();
 }
}
