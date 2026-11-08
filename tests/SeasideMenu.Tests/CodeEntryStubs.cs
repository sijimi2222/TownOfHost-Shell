using TMPro;using UnityEngine;
public class TextBoxTMP:Component {
 public string text="";public bool hasFocus,AllowPaste=true;public Event OnChange=new(),OnEnter=new();public Collider2D[] colliders;
 public void GiveFocus()=>hasFocus=true;public void LoseFocus()=>hasFocus=false;
 public void SetText(string value){text=value;OnChange.Invoke();}
}
public class EnterCodeManager:Component {
 public TextBoxTMP enterCodeField;public GameObject enterCodeCheckmark,loadingIcon;
 public TextMeshPro hostText,capacityText,serverText,chatText;public PassiveButton joinGamePassiveButton;public UiElement BackButton;
 public void OnEnable(){}public void OnDisable(){}
}
public class ServerDropdown:Component {}
public class Checkbox:Component {}
namespace TownOfHost {
 public static class EnterCodeManagerPatch {
  public static PassiveButton RestoreButton,DownloadButton,UnloadAndJoinButton,ServerList;
  public static TextMeshPro VersionText;public static ServerDropdown ServerListDrop;public static Checkbox vServerOnlyCheckBox;
 }
}
