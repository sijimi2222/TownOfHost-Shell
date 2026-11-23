using UnityEngine;
using TMPro;
namespace UnityEngine {
 public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public static Vector2 zero=>new(0,0); }
 public struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} public static Vector3 one=>new(1,1,1); }
 public struct Vector4 { public float x,y,z,w; public static Vector4 zero=>new(); }
 public class Object { public bool Destroyed; public static void DestroyImmediate(Object o){o.Destroyed=true;} }
 public class GameObject:Object {
  public bool activeSelf=true;
  public Transform transform;
  readonly Dictionary<Type,Component> components=new();
  public GameObject(){transform=new Transform{gameObject=this};}
  public void SetActive(bool active){activeSelf=active;}
  public T Add<T>() where T:Component,new(){var c=new T{gameObject=this};components[typeof(T)]=c;return c;}
  public T GetComponent<T>() where T:Component { return components.TryGetValue(typeof(T),out var c)&&!c.Destroyed?(T)c:null; }
 }
 public class Component:Object { public GameObject gameObject; public Transform transform=>gameObject.transform; public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>(); }
 public class Transform:Object {public GameObject gameObject;public Transform parent;public Vector3 localPosition,localScale;public void SetParent(Transform p,bool worldPositionStays){parent=p;} }
 public class RectTransform {public Vector2 anchorMin,anchorMax,pivot,anchoredPosition,sizeDelta;}
 public class MeshRenderer:Component {public int sortingLayerID,sortingOrder;}
 public class SpriteRenderer:Component {public int sortingLayerID,sortingOrder;public Vector2 size;}
}
namespace TMPro {
 public enum TextAlignmentOptions{Left,Center}
 public enum TextOverflowModes{Truncate,Overflow}
 public class TMP_FontAsset {}
 public class TextMeshPro:Component {
  public RectTransform rectTransform=new(); public TMP_FontAsset font;public object fontSharedMaterial;
  public TextAlignmentOptions alignment;public TextOverflowModes overflowMode;public Vector4 margin;
  public Color color; public float alpha,fontSize,fontSizeMax,fontSizeMin;public int sortingOrder;
  public bool enableAutoSizing,enableWordWrapping,enabled=true,TranslatorRemoved;public string text="";
  public int MeshUpdates;public bool LastIgnoreActive;
  public void ForceMeshUpdate(bool ignoreActiveState){MeshUpdates++;LastIgnoreActive=ignoreActiveState;}
 }
}
public class AspectPosition:Component {}
public class PassiveButton:Component {
 public TextMeshPro buttonText;
 public GameObject inactiveSprites=new(),activeSprites=new(),selectedSprites=new();
}
namespace TownOfHost { public static class TestLabelExtensions {public static void DestroyTranslator(this TextMeshPro text){text.TranslatorRemoved=true;}} }
