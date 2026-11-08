using UnityEngine;
namespace HarmonyLib {[AttributeUsage(AttributeTargets.All,AllowMultiple=true)] public class HarmonyPatch:Attribute {public HarmonyPatch(){}public HarmonyPatch(Type t,string m){}}public class HarmonyPostfix:Attribute{}}
namespace UnityEngine {
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new(1,1,1);}
 public class GameObject {public bool activeSelf=true;public GameObject parent;public bool activeInHierarchy=>activeSelf&&(parent==null||parent.activeInHierarchy);public Transform transform;public int Changes;public GameObject(){transform=new(this);}public void SetActive(bool v){activeSelf=v;Changes++;}}
 public class Transform {public GameObject gameObject;public Transform parent;public Vector3 localPosition,localScale=Vector3.one;public Transform(GameObject g){gameObject=g;}public Vector3 position {get=>parent==null?localPosition:new(parent.position.x+localPosition.x,parent.position.y+localPosition.y,parent.position.z+localPosition.z);set=>localPosition=value;}public Vector3 InverseTransformPoint(Vector3 p)=>new(p.x-position.x,p.y-position.y,p.z-position.z);public Vector3 TransformPoint(Vector3 p)=>new(position.x+p.x*localScale.x,position.y+p.y*localScale.y,position.z+p.z*localScale.z);}
 public class Component {public GameObject gameObject=new();public Transform transform=>gameObject.transform;}
 public class SpriteRenderer:Component {public bool enabled=true;public float Width=1;public Bounds bounds=>new(){min=new(transform.position.x-Width/2,transform.position.y-.5f,transform.position.z),center=transform.position};}
 public struct Bounds {public Vector3 min,center;public Vector3 max=>new(2*center.x-min.x,2*center.y-min.y,2*center.z-min.z);}
 public class Camera {public bool orthographic=true;public float orthographicSize=3,aspect=16f/9;public Vector3 WorldToViewportPoint(Vector3 p)=>new(.5f+p.x/(2*orthographicSize*aspect),.5f+p.y/(2*orthographicSize),1);}
 public static class Mathf {public static float Abs(float x)=>Math.Abs(x);public static float Max(float a,float b)=>Math.Max(a,b);}
}
public class AspectPosition {public UnityEngine.Vector3 DistanceFromEdge;public int Adjustments;public float RightEdge,NativeAnchorBias;public static implicit operator bool(AspectPosition p)=>p!=null;public void AdjustPosition(){Adjustments++;RightEdge=HudManager.Instance.UICamera.orthographicSize*HudManager.Instance.UICamera.aspect-DistanceFromEdge.x;if(TownOfHost.CredentialsPatch.pingTrackerCredential!=null){var t=TownOfHost.CredentialsPatch.pingTrackerCredential.transform;var p=t.position;t.position=new(RightEdge+NativeAnchorBias,p.y,p.z);}}}
public class Button:UnityEngine.Component {}
public class ChatController:UnityEngine.Component {public Button chatButton=new();public int VisibleCalls;public bool IsOpenOrOpening;public void SetVisible(bool v){VisibleCalls++;gameObject.SetActive(v);chatButton.gameObject.SetActive(v);}}
public class OptionsMenuBehaviour:UnityEngine.Component {public bool IsOpen;public void Close(){IsOpen=false;}}
public class HudManager:UnityEngine.Component {public static HudManager Instance;public static bool InstanceExists=>Instance!=null;public Button SettingsButton=new(),MatchInfoButton=new();public ChatController Chat=new();public Camera UICamera=new();public OptionsMenuBehaviour GameMenu=new();}
public class DestroyableSingleton<T> {public static bool InstanceExists=>HudManager.InstanceExists;public static HudManager Instance=>HudManager.Instance;}
public class PlayerData {public bool IsDead,Disconnected;}
public class PlayerControl {public static PlayerControl LocalPlayer;public PlayerData Data=new();}
public static class GameSettingMenu {public static bool Instance;}
namespace TownOfHost {
 public static class GameStates {public static bool IsModHost=true,IsLobby,IsMeeting,IsInGame,IsInTask;}
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
 public class TextMeshPro:UnityEngine.Component {public float InkOverflow,PendingInkOverflow=float.NaN;public bool FailMesh;public int MeshUpdates;public Action DuringMeshUpdate;public void ForceMeshUpdate(){MeshUpdates++;DuringMeshUpdate?.Invoke();if(FailMesh)throw new Exception("synthetic unavailable font");if(!float.IsNaN(PendingInkOverflow)){InkOverflow=PendingInkOverflow;PendingInkOverflow=float.NaN;}}public UnityEngine.Bounds textBounds=>new(){min=new(-4+rectTransform.RightBias,-1,0),center=new((-4+2*rectTransform.RightBias+InkOverflow)/2,-.5f,0)};public Rect rectTransform;public TextMeshPro(){rectTransform=new(this);}public class Rect {readonly TextMeshPro owner;public float RightBias;public Rect(TextMeshPro o){owner=o;}public void GetWorldCorners(UnityEngine.Vector3[] corners){var p=owner.transform.position;corners[0]=new(p.x-4+RightBias,p.y-1,p.z);corners[1]=new(p.x-4+RightBias,p.y,p.z);corners[2]=new(p.x+RightBias,p.y,p.z);corners[3]=new(p.x+RightBias,p.y-1,p.z);}}}
}

namespace TownOfHost {public static class Logger {public static System.Collections.Generic.List<string> Lines=new();public static void Info(string s,string c)=>Lines.Add(s);public static void Warn(string s,string c)=>Lines.Add(s);}}
