using System;using System.Collections.Generic;
namespace UnityEngine
{
 public class Object {
  public static T Instantiate<T>(T source,Transform parent) where T:Component,new(){var c=new T();c.transform.parent=parent;return c;}
  public static void Destroy(Object obj){if(obj is GameObject go){go.SetActive(false);go.Destroyed=true;go.transform.parent=null;}}
 }
 public class GameObject:Object {public string name;public bool Destroyed,activeSelf=true;public readonly RectTransform transform;public GameObject(){transform=new RectTransform(this);}public bool activeInHierarchy=>!Destroyed&&activeSelf&&(transform.parent?.gameObject.activeInHierarchy??true);public void SetActive(bool b)=>activeSelf=b;}
 public class Transform:Object {public GameObject gameObject;public Vector3 localPosition,localScale;private Transform p;public readonly List<Transform> children=new();public Transform(GameObject go){gameObject=go;}public Transform parent{get=>p;set{p?.children.Remove(this);p=value;p?.children.Add(this);}}}
 public class RectTransform:Transform{public Vector2 sizeDelta;public RectTransform(GameObject go):base(go){}}
 public class Component:Object{public readonly GameObject gameObject=new();public Transform transform=>gameObject.transform;public string name{get=>gameObject.name;set=>gameObject.name=value;}}
 public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}}
 public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public class SpriteRenderer:Component{}
}
namespace TMPro {public enum TextAlignmentOptions{Center,Left}public enum TextOverflowModes{Ellipsis}public class TextMeshPro:UnityEngine.Component{public string text="";public TextAlignmentOptions alignment;public bool enableWordWrapping;public TextOverflowModes overflowMode;public UnityEngine.RectTransform rectTransform=>gameObject.transform;}}
public class ButtonEvent{private Action action;public void AddListener(Action a)=>action+=a;public void Invoke()=>action?.Invoke();}
public class PassiveButton{public ButtonEvent OnClick=new();}
public class ToggleButtonBehaviour:UnityEngine.Component{public TMPro.TextMeshPro Text=new();private readonly PassiveButton passive=new();public T GetComponent<T>() where T:class=>passive as T;}
public class OptionsMenuBehaviour{public UnityEngine.SpriteRenderer Background=new();public ToggleButtonBehaviour DisableMouseMovement=new();}
namespace TownOfHost.Modules.ClientOptions{public static class ClientActionItem{public static UnityEngine.SpriteRenderer CustomBackground=new();}}
namespace TownOfHost{public static class Translator{public static string GetString(string key)=>key=="StreamInterestCount"?"参加希望者：{0}人（今回受付分）":key;}}
