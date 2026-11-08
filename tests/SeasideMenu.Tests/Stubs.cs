using System;using System.Linq;using System.Collections.Generic;
namespace BepInEx {public static class Paths {public static string ConfigPath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"ShellSeasideUnitTests-NoExternalAssets");}}
namespace BepInEx.Configuration {public class ConfigEntry<T>{public T Value;public ConfigEntry(T v){Value=v;}} public class ConfigFile {public bool LegacySeasideEnabled;public int BindCalls;public ConfigEntry<T> Bind<T>(string s,string k,T d,string desc){BindCalls++;return new(s=="Main Menu"&&k=="Seaside Menu"?(T)(object)LegacySeasideEnabled:d);}}}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.All,AllowMultiple=true)]public class HarmonyPatch:Attribute {public HarmonyPatch(){} public HarmonyPatch(Type t,string s){}}
 public class HarmonyPostfix:Attribute{} public class HarmonyPrefix:Attribute{} public class HarmonyPriority:Attribute {public HarmonyPriority(int i){}} public static class Priority {public const int Last=0,First=800;}
}
namespace Il2CppSystem.Collections.Generic {public class List<T>:System.Collections.Generic.List<T>{static int next;public IntPtr Pointer=(IntPtr)(++next);}}
namespace UnityEngine {
 public class Object {static int nextId;readonly int id=++nextId;public virtual int GetInstanceID()=>id;public static bool NativeRendererWrappers;public string name {get=>this is Component c?c.gameObject.name:n;set{if(this is Component c)c.gameObject.name=value;else n=value;}}string n;
 public static int ComponentQueries,ChildComponentQueries;
 public static readonly List<GameObject> Objects=new(); public static T FindObjectOfType<T>() where T:class=>FindObjectsOfType<T>().FirstOrDefault();
 public static T[] FindObjectsOfType<T>() where T:class=>Objects.Where(g=>g.activeInHierarchy).SelectMany(g=>g.Components).OfType<T>().ToArray();
 public static void Destroy(Object o){if(o is GameObject g){foreach(var child in g.transform.children.ToArray())Destroy(child.gameObject);g.transform.SetParent(null);g.SetActive(false);Objects.Remove(g);} else if(o is Component c)c.gameObject.Components.Remove(c);}
 public static T Instantiate<T>(T o,Transform parent) where T:Object {
  if(o is PassiveButton source){var g=new GameObject("LoadingClone");g.transform.SetParent(parent,false);var b=g.AddComponent<PassiveButton>();g.AddComponent<BoxCollider2D>();g.AddComponent<SpriteRenderer>();var f=new GameObject("Font");f.transform.SetParent(g.transform,false);f.AddComponent<TMPro.TextMeshPro>();f.AddComponent<MeshRenderer>();return b as T;}
  if(o is TMPro.TextMeshPro old){var g=new GameObject("Text_TMP");g.transform.SetParent(parent,false);var x=g.AddComponent<TMPro.TextMeshPro>();x.fontSize=old.fontSize;x.fontStyle=old.fontStyle;x.text=old.text;g.AddComponent<MeshRenderer>();return x as T;}
  throw new Exception("Unexpected clone");
 }}
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();public T[] GetComponentsInChildren<T>(bool include=false) where T:class=>gameObject.GetComponentsInChildren<T>(include);public T GetComponentInChildren<T>(bool include=false) where T:class=>GetComponentsInChildren<T>(include).FirstOrDefault();}
 public class Behaviour:Component {public virtual bool enabled {get;set;}=true;}
 public class GameObject:Object {public UnityEngine.SceneManagement.Scene OwnScene=UnityEngine.SceneManagement.SceneManager.Active;public UnityEngine.SceneManagement.Scene scene=>transform.parent==null?OwnScene:transform.parent.gameObject.scene;public readonly List<Component> Components=new();public Transform transform;public int layer;public bool activeSelf=true;public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
 public GameObject(string n){name=n;transform=new Transform{gameObject=this};Components.Add(transform);Objects.Add(this);}public void SetActive(bool a)=>activeSelf=a;
 public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);return c;}
 public T GetComponent<T>() where T:class{Object.ComponentQueries++;return Components.OfType<T>().FirstOrDefault();}
 public T GetComponentInChildren<T>(bool include=false) where T:class=>GetComponentsInChildren<T>(include).FirstOrDefault();
 public T[] GetComponentsInChildren<T>(bool include=false) where T:class {Object.ChildComponentQueries++;var result=new[]{this}.Concat(transform.Descendants().Select(t=>t.gameObject)).Where(g=>include||g.activeInHierarchy).SelectMany(g=>g.Components).OfType<T>().ToArray();if(typeof(T)==typeof(Renderer)&&Object.NativeRendererWrappers)return result.Cast<Renderer>().Select(r=>(T)(object)new RendererAlias(r)).ToArray();return result;}}
 public class Transform:Component {public Transform parent;public readonly List<Transform> children=new();public Vector3 localPosition,localScale=Vector3.one;public Quaternion rotation,localRotation;
 public Vector3 lossyScale=>parent==null?localScale:new(localScale.x*parent.lossyScale.x,localScale.y*parent.lossyScale.y,localScale.z*parent.lossyScale.z);
 public Vector3 position {get=>parent==null?localPosition:parent.TransformPoint(localPosition);set=>localPosition=parent==null?value:new((value.x-parent.position.x)/parent.localScale.x,(value.y-parent.position.y)/parent.localScale.y,(value.z-parent.position.z)/parent.localScale.z);}
 public void SetParent(Transform p,bool world=true){var pos=position;parent?.children.Remove(this);parent=p;p?.children.Add(this);if(world)position=pos;}
 public Transform Find(string path){var split=path.Split(new[]{'/'},2);var child=children.FirstOrDefault(c=>c.gameObject.name==split[0]);return split.Length==1?child:child?.Find(split[1]);}
 public IEnumerable<Transform> Descendants()=>children.SelectMany(c=>new[]{c}.Concat(c.Descendants())); public bool IsChildOf(Transform t)=>this==t||(parent!=null&&parent.IsChildOf(t));
 public Vector3 TransformPoint(Vector3 p)=>position+new Vector3(p.x*localScale.x,p.y*localScale.y,p.z*localScale.z);}
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 one=>new(1,1,1);public static Vector3 operator*(Vector3 a,float s)=>new(a.x*s,a.y*s,a.z*s);public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);}
 public struct Quaternion{public float Z;public static Quaternion Euler(float x,float y,float z)=>new(){Z=z};} public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new(1,1,1);public static Color black=>new(0,0,0);public static Color clear=>new(0,0,0,0);public static implicit operator Color(Color32 c)=>new(c.r/255f,c.g/255f,c.b/255f,c.a/255f);}
 public struct Color32 {public byte r,g,b,a;public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;}public static implicit operator Color32(Color c)=>new((byte)(c.r*255),(byte)(c.g*255),(byte)(c.b*255),(byte)(c.a*255));}
 public class Renderer:Behaviour {public virtual int sortingOrder{get;set;}public int sortingLayerID;public virtual Bounds bounds{get;set;}=new(){size=new(20,10,1)};public virtual Material sharedMaterial{get;set;}public bool isVisible=>enabled&&gameObject.activeInHierarchy;}
 public class RendererAlias:Renderer {readonly Renderer source;public RendererAlias(Renderer r){source=r;gameObject=r.gameObject;}public override int GetInstanceID()=>source.GetInstanceID();public override bool enabled {get=>source==null?true:source.enabled;set{if(source!=null)source.enabled=value;}}public override int sortingOrder {get=>source==null?0:source.sortingOrder;set{if(source!=null)source.sortingOrder=value;}}public override Material sharedMaterial{get=>source?.sharedMaterial;set{if(source!=null)source.sharedMaterial=value;}}public override Bounds bounds{get=>source==null?new():source.bounds;set{if(source!=null)source.bounds=value;}}}
 public class Shader:Object {public static Shader Find(string name)=>new();}
 public class Material:Object {public Texture2D mainTexture;public Shader shader;public readonly Dictionary<string,Color> Colors=new();public readonly Dictionary<string,float> Floats=new();public Material(Shader shader){this.shader=shader;}public bool HasProperty(string key)=>Colors.ContainsKey(key)||Floats.ContainsKey(key);public Color GetColor(string key)=>Colors[key];public float GetFloat(string key)=>Floats[key];}
 public class Mesh:Object {public Vector2[] uv;public Vector3[] vertices;public Color[] colors;public int[] triangles;public void MarkDynamic(){}public void Clear(){vertices=null;colors=null;triangles=null;}public void RecalculateBounds(){}}
 public class MeshFilter:Component {public Mesh mesh;}
 public enum SpriteDrawMode {Simple,Sliced,Tiled}public enum SpriteMaskInteraction {None,VisibleInsideMask,VisibleOutsideMask}
 public class MeshRenderer:Renderer {public Material material;} public class SpriteRenderer:Renderer {public Sprite sprite;public Color color;public SpriteDrawMode drawMode;public Vector2 size=new(1,1);public SpriteMaskInteraction maskInteraction;}
 public class Collider2D:Behaviour{public Bounds bounds=>new(){size=transform.lossyScale};} public class PolygonCollider2D:Collider2D {public Vector2[] points;}public class BoxCollider2D:Collider2D{public Vector2 size,offset;}
 public enum TextureFormat {ARGB32}public class Texture2D:Object {public int width,height;public Texture2D(int w,int h,TextureFormat f,bool m){width=w;height=h;}public void SetPixel(int x,int y,Color c){}public void Apply(){}}
 public struct Rect {public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}}
 public struct Bounds {public Vector3 size;}
 public class Sprite:Object {public Vector2[] vertices;public ushort[] triangles;public void OverrideGeometry(Vector2[] v,ushort[] t){vertices=v;triangles=t;}public Bounds bounds;public Rect rect;public Texture2D texture;public static Sprite Create(Texture2D t,Rect r,Vector2 p,float unit)=>new(){texture=t,rect=r,bounds=new(){size=new(r.width/unit,r.height/unit,1)}};}
 public static class ImageConversion {public static bool Success;public static bool LoadImage(Texture2D t,byte[] b){if(!Success)return false;t.width=(int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(16,4));t.height=(int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(20,4));return true;}}
 public class RectTransform {public Vector2 sizeDelta;}
 public class Camera:Behaviour {public class CameraCallback{Action<Camera> a;public static implicit operator CameraCallback(Action<Camera> action)=>new(){a=action};public static CameraCallback operator+(CameraCallback x,CameraCallback y)=>new(){a=x?.a+y?.a};public void Invoke(Camera c)=>a?.Invoke(c);}public static CameraCallback onPreCull,onPostRender;public Color backgroundColor;public float depth;public int clearFlags,cullingMask;public static Camera main;public bool orthographic=true;public float orthographicSize=3,aspect=16f/9;public Vector3 ViewportToWorldPoint(Vector3 p)=>new((p.x-.5f)*orthographicSize*2*aspect,(p.y-.5f)*orthographicSize*2,p.z);}
 public class CanvasGroup:Behaviour {public float alpha=1;public bool interactable=true,blocksRaycasts=true,ignoreParentGroups;}public class Canvas:Behaviour {public int renderMode,sortingOrder,sortingLayerID;}public class CanvasRenderer:Component {public Color color=Color.white;public Color GetColor()=>color;}
 public static class Application {public static string version="2026.10.13";}
 public static class Time {public static int frameCount;public static float unscaledDeltaTime=.02f;}
 public class AspectPosition:Behaviour{} public enum KeyCode {F8,Escape} public static class Input {public static bool F8,Escape;public static bool GetKeyDown(KeyCode c){if(c==KeyCode.Escape){var e=Escape;Escape=false;return e;}var k=F8;F8=false;return k;}}
}
namespace TMPro {public enum TextAlignmentOptions {Center,Left}public enum FontStyles {Normal=0,UpperCase=16,Italic=2}public class TextMeshPro:UnityEngine.Behaviour {public string text="";public FontStyles fontStyle;public float fontSize,fontSizeMax,fontSizeMin;public bool enableAutoSizing,enableWordWrapping;public TextAlignmentOptions alignment;public UnityEngine.Color color;public UnityEngine.RectTransform rectTransform=new();}}
public static class Controller {public enum TouchType {Joystick,Mouse,Touch}public static TouchType currentTouchType=TouchType.Mouse;}
public class Event {readonly List<Action> actions=new();public void AddListener(Action a)=>actions.Add(a);public int Calls;public void Invoke(){Calls++;foreach(var a in actions.ToArray())a();}}
public struct ControllerNavigation {public static ControllerNavigation defaultNavigation=>new();}
public class UiElement:UnityEngine.Behaviour {public ControllerNavigation ControllerNav;public Event OnMouseOver=new(),OnMouseOut=new();}
public class PassiveUiElement:UiElement {public UnityEngine.Collider2D[] Colliders;public UnityEngine.Collider2D ClickMask;}
public class PassiveButtonManager {public void HandleMouseOver(PassiveUiElement element,UnityEngine.Collider2D collider){}}
public class PassiveButton:PassiveUiElement {public UnityEngine.GameObject activeSprites,selectedSprites;public void ReceiveClickUp(){}public void ReceiveClickDown(){}public Event OnClick=new();}
public class ControllerUiElementsState {public string MenuName="";public bool IsScene;public IntPtr Pointer= (IntPtr)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(new object());public Il2CppSystem.Collections.Generic.List<UiElement> SelectableUiElements=new();public UiElement CurrentSelection;}
public class ControllerManager {public static ControllerManager Instance=new();public ControllerUiElementsState CurrentUiState=new(){MenuName="MainMenu",IsScene=true};public void SetCurrentSelected(UiElement e)=>CurrentUiState.CurrentSelection=e;}
public class MainMenuManager:UnityEngine.Component {
 public PassiveButton PlayOnlineButton,playLocalButton;public PassiveButton playButton,inventoryButton,shopButton,newsButton,myAccountButton,settingsButton,creditsButton,quitButton;
 public UnityEngine.SpriteRenderer screenTint;public UnityEngine.GameObject creditsScreen;public bool finishStartup=true,animating,Home=true;
 public Action ResetFailure;public bool IsMainMenuActive()=>Home;public void Awake(){}public void Start(){}public void LateUpdate(){}public void ResetScreen(){ResetFailure?.Invoke();Home=true;TownOfHost.CredentialsPatch.TOHhmLogo?.gameObject.SetActive(true);}public void SetUpControllerNav(){}public void OpenGameModeMenu(){}public void OpenCreateGame(){}public void DeactivateMainMenuUI(){}public void ActivateMainMenuUI(){}public void OpenAccountMenu(){}public void OpenCredits(){}public void OpenOnlineMenu(){}public void OpenEnterCodeMenu(){}public void TransitionToInventory(){}public void TransitionToShop(){}}
public class SplashManager:UnityEngine.Component {public void Start(){}}
public class GenericPopup:UnityEngine.Component {public void Show(){}public void Close(){}}
public class DisconnectPopup:UnityEngine.Component {public void Show(){}public void DoShow(){}public void ShowCustom(){}public void Close(){}}
public class AdDataCollectScreen:UnityEngine.Component {public void Show(){}public void ForceShow(){}public void Close(){}}
public class LoadingBarManager:UnityEngine.Component {public UnityEngine.Component loadingBar;public void ToggleLoadingBar(){}}
public class OptionsMenuBehaviour:UnityEngine.Component {public bool IsOpen;public int CloseCalls;public Action CloseAction;public Exception CloseFailure;public void Open(){}public void Close(){CloseCalls++;if(CloseFailure!=null)throw CloseFailure;IsOpen=false;gameObject.SetActive(false);CloseAction?.Invoke();}public void OnDisable(){}public void OnDestroy(){}}
public class AnnouncementPopUp:UnityEngine.Component {public void Show(){}public void OnEnable(){}}
public class FriendsListUI:UnityEngine.Component {public static FriendsListUI Instance;public bool IsOpen;public int CloseCalls;public Action CloseAction;public bool LastCloseArgument;public void Open(){IsOpen=true;} public void Close(bool value){CloseCalls++;LastCloseArgument=value;IsOpen=false;gameObject.SetActive(false);CloseAction?.Invoke();}public void OnDisable(){}}
public class FriendsListButton:UnityEngine.Component {public UnityEngine.GameObject Button;public TMPro.TextMeshPro NotifCount;}
public class AccountManager:UnityEngine.Component {public bool AccountTabActive;public void OpenAccountWindow(){}public void CloseAccountWindow(){}}
public class VersionShower:UnityEngine.Component {public TMPro.TextMeshPro text;}
public class ModManager {public UnityEngine.SpriteRenderer ModStamp;public void LateUpdate(){}}
namespace TownOfHost {
 public class ErrorText {public static ErrorText Instance;public TMPro.TextMeshPro Text;}
 public class Main {public static BepInEx.Configuration.ConfigEntry<bool> DisableWaveAnimation=new(false);public static Main Instance=new();public BepInEx.Configuration.ConfigFile Config=new();public static string PluginShowVersion="2.2.0β";}
 public static class Logger {public static int Warnings;public static bool FailAudit;public static readonly List<string> Infos=new();public static void Warn(string m,string c)=>Warnings++;public static void Info(string m,string c){if(c=="SeasideFriendsAudit"&&FailAudit)throw new Exception("diagnostic failure");Infos.Add(m);}}
 public static class CredentialsPatch {public static UnityEngine.SpriteRenderer TOHhmLogo;}
 public static class MainMenuManagerPatch {public static readonly Dictionary<string,PassiveButton> Sources=new();public static PassiveButton SeasideAction(string id)=>Sources.GetValueOrDefault(id);public static Templates.SimpleButton UpdateButton,UpdateButton2;}
 public static class UtilsSprite {
 public static bool Missing;public static string MissingAsset;
 static readonly Dictionary<string,UnityEngine.Sprite> cache=new();
 public static void Reset(){Missing=false;MissingAsset=null;cache.Clear();}
 public static UnityEngine.Texture2D GetOrLoadTexture(string id){
 if(Missing||id==MissingAsset)return null;int w=1917,h=1038;
 if(id.StartsWith("TownOfHost.Resources.MainMenu.Play.")){w=id.EndsWith("page.png")?1672:1000;h=id.EndsWith("page.png")?941:750;}
 else if(id.StartsWith(TownOfHost.Modules.MainMenu.SeasideMenuLayers.ResourcePrefix)){
  if(id.EndsWith("background.png")){w=1672;h=941;}
  else {using var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(TownOfHost.Modules.MainMenu.SeasideMenuLayers.ResourcePrefix+"manifest.json");using var doc=System.Text.Json.JsonDocument.Parse(stream);var a=doc.RootElement.GetProperty("buttons").EnumerateArray().Single(b=>id==TownOfHost.Modules.MainMenu.SeasideMenuLayers.ResourcePrefix+b.GetProperty("file").GetString().Replace('/','.')).GetProperty("image_rect");w=a.GetProperty("width").GetInt32();h=a.GetProperty("height").GetInt32();}}
 return new(w,h,UnityEngine.TextureFormat.ARGB32,false);}
 public static UnityEngine.Sprite LoadSprite(string id,float ppu){if(Missing||id==MissingAsset)return null;if(cache.TryGetValue(id,out var sprite))return sprite;var t=GetOrLoadTexture(id);if(t==null)return null;return cache[id]=UnityEngine.Sprite.Create(t,new(0,0,t.width,t.height),new(.5f,.5f),ppu);}
 }
 public static class Ext {public static void DestroyTranslator(this TMPro.TextMeshPro t){}}
}
namespace TownOfHost.Templates {public class SimpleButton {
 public PassiveButton Button;public SimpleButton(UnityEngine.Transform parent,string name,UnityEngine.Vector3 pos,UnityEngine.Color32 n,UnityEngine.Color32 h,Action action,string label,bool active=true){
 var g=new UnityEngine.GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=pos;Button=g.AddComponent<PassiveButton>();Button.OnClick.AddListener(action);g.AddComponent<UnityEngine.BoxCollider2D>();g.AddComponent<UnityEngine.SpriteRenderer>();
 var text=new UnityEngine.GameObject("Text_TMP");text.transform.SetParent(g.transform,false);text.AddComponent<TMPro.TextMeshPro>();text.AddComponent<UnityEngine.MeshRenderer>();g.SetActive(active);
 }} }

namespace UnityEngine.SceneManagement {
 public struct Scene {public int handle;public string name;public bool isLoaded;public bool IsValid()=>handle!=0;}
 public static class SceneManager {public static Scene Active=new(){handle=1,isLoaded=true};public static Scene GetActiveScene()=>Active;}
}
public static class HudManager {public static bool InstanceExists;}
public class PlayerControl {public static PlayerControl LocalPlayer;}

namespace UnityEngine.UI {public class Image:UnityEngine.Behaviour {public UnityEngine.Color color=UnityEngine.Color.white;public bool raycastTarget=true;public UnityEngine.CanvasRenderer canvasRenderer;public UnityEngine.Material material;}}
