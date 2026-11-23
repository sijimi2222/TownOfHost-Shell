using TownOfHost;
using TMPro;
using UnityEngine;
class Program {
 static int passed;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Test(string name,Action action){RoleSettingsDisplay.Reset();action();passed++;Console.WriteLine("PASS "+name);}
 static TextMeshPro Text(){var go=new GameObject();go.Add<MeshRenderer>();return go.Add<TextMeshPro>();}
 static (PassiveButton,TextMeshPro,Transform) Setup(){
  var go=new GameObject();var b=go.Add<PassiveButton>();b.buttonText=Text();
  var inherited=new GameObject();inherited.transform.localPosition=new Vector3(4,1,0);inherited.SetActive(false);
  b.buttonText.transform.SetParent(inherited.transform,false);b.buttonText.transform.localPosition=new Vector3(3,1,0);b.buttonText.transform.localScale=new Vector3(0,0,0);
  b.buttonText.color=new Color(0,0,0,0);b.buttonText.alpha=0;b.buttonText.fontSize=0;b.buttonText.font=new();b.buttonText.fontSharedMaterial=new();b.buttonText.gameObject.Add<AspectPosition>();
  int order=2;foreach(var state in new[]{b.inactiveSprites,b.activeSprites,b.selectedSprites}){var bg=state.Add<SpriteRenderer>();bg.sortingLayerID=7;bg.sortingOrder=order++;bg.size=new Vector2(.68f,.68f);}
  var source=Text();source.font=new();source.fontSharedMaterial=new();return(b,source,inherited.transform);
 }
 static string Plain(PassiveButton b)=>b.buttonText.text.Replace("\n","");
 static void Main(){
 Test("initial label states the MOD destination",()=>{var(b,font,_)=Setup();LabelAdapter.ConfigureLabel(b,font);LabelAdapter.UpdateLabel(b,true);Check(Plain(b)=="MOD順にする"&&b.buttonText.text.Split('\n').Length==2,"initial label");});
 Test("mode change states the gojuon destination",()=>{var(b,font,_)=Setup();LabelAdapter.ConfigureLabel(b,font);RoleSettingsDisplay.Toggle();LabelAdapter.UpdateLabel(b);Check(Plain(b)=="あいうえお順にする"&&b.buttonText.text.Split('\n').Length==2,"mod destination");});
 Test("second click restores the initial destination",()=>{var(b,font,_)=Setup();LabelAdapter.ConfigureLabel(b,font);RoleSettingsDisplay.Toggle();LabelAdapter.UpdateLabel(b);RoleSettingsDisplay.Toggle();LabelAdapter.UpdateLabel(b);Check(Plain(b)=="MOD順にする","roundtrip");});
 Test("menu reopen matches reset mode and destination",()=>{var(b,font,_)=Setup();RoleSettingsDisplay.Toggle();LabelAdapter.UpdateLabel(b);RoleSettingsDisplay.Reset();LabelAdapter.UpdateLabel(b,true);Check(Plain(b)=="MOD順にする","reopen");});
 Test("label escapes inherited offset and inactive FontPlacer",()=>{var(b,font,parent)=Setup();LabelAdapter.ConfigureLabel(b,font);Check(b.buttonText.transform.parent==b.transform&&b.buttonText.transform.parent!=parent,"inherited parent");Check(b.buttonText.transform.localPosition.Equals(new Vector3(0,0,-.2f)),"local position");});
 Test("inherited AspectPosition no longer overrides label layout",()=>{var(b,font,_)=Setup();var aspect=b.buttonText.GetComponent<AspectPosition>();LabelAdapter.ConfigureLabel(b,font);Check(aspect.Destroyed&&b.buttonText.GetComponent<AspectPosition>()==null,"aspect retained");Check(b.buttonText.TranslatorRemoved,"translator retained");});
 Test("Japanese UI font and shared material are reused",()=>{var(b,font,_)=Setup();LabelAdapter.ConfigureLabel(b,font);Check(ReferenceEquals(b.buttonText.font,font.font)&&ReferenceEquals(b.buttonText.fontSharedMaterial,font.fontSharedMaterial),"font source");});
 Test("missing font source preserves the existing font",()=>{var(b,_,_2)=Setup();var f=b.buttonText.font;var m=b.buttonText.fontSharedMaterial;LabelAdapter.ConfigureLabel(b,null);Check(ReferenceEquals(b.buttonText.font,f)&&ReferenceEquals(b.buttonText.fontSharedMaterial,m),"fallback font");});
 Test("white opaque text is visible over colored background",()=>{var(b,font,_)=Setup();LabelAdapter.ConfigureLabel(b,font);Check(b.buttonText.color.Equals(Color.white)&&b.buttonText.alpha==1,"color alpha");});
 Test("label anchors pivot rectangle and scale are centered",()=>{var(b,font,_)=Setup();LabelAdapter.ConfigureLabel(b,font);var rect=b.buttonText.rectTransform;Check(rect.anchorMin.Equals(new Vector2(.5f,.5f))&&rect.anchorMax.Equals(rect.anchorMin)&&rect.pivot.Equals(rect.anchorMin),"anchors");Check(rect.anchoredPosition.Equals(Vector2.zero)&&rect.sizeDelta.Equals(new Vector2(.6f,.6f)),"rect");Check(b.buttonText.transform.localScale.Equals(Vector3.one)&&b.buttonText.alignment==TextAlignmentOptions.Center,"scale alignment");});
 Test("mesh draws before every button state background",()=>{var(b,font,_)=Setup();LabelAdapter.ConfigureLabel(b,font);var mesh=b.buttonText.GetComponent<MeshRenderer>();foreach(var state in new[]{b.inactiveSprites,b.activeSprites,b.selectedSprites}){var bg=state.GetComponent<SpriteRenderer>();Check(mesh.sortingLayerID==bg.sortingLayerID&&mesh.sortingOrder>bg.sortingOrder,"draw order");}});
 Test("explicit lines fit a bounded auto-size region",()=>{var(b,font,_)=Setup();LabelAdapter.ConfigureLabel(b,font);Check(b.buttonText.enableAutoSizing&&b.buttonText.fontSizeMin==.65f&&b.buttonText.fontSizeMax==2.2f&&b.buttonText.fontSize==2.2f,"font bounds");Check(!b.buttonText.enableWordWrapping&&b.buttonText.overflowMode==TextOverflowModes.Overflow,"text layout");});
 Test("refresh reenables hidden disabled label",()=>{var(b,font,_)=Setup();LabelAdapter.ConfigureLabel(b,font);b.buttonText.enabled=false;b.buttonText.gameObject.SetActive(false);LabelAdapter.UpdateLabel(b);Check(b.buttonText.enabled&&b.buttonText.gameObject.activeSelf&&b.buttonText.MeshUpdates==1,"reshow");});
 Test("unchanged refresh avoids rebuilding the mesh every frame",()=>{var(b,font,_)=Setup();LabelAdapter.ConfigureLabel(b,font);LabelAdapter.UpdateLabel(b);for(int i=0;i<100;i++)LabelAdapter.UpdateLabel(b);Check(b.buttonText.MeshUpdates==1,"repeated mesh generation");});
 Test("reappearance can force a mesh refresh without changing mode",()=>{var(b,font,_)=Setup();LabelAdapter.ConfigureLabel(b,font);LabelAdapter.UpdateLabel(b);LabelAdapter.UpdateLabel(b,true);Check(b.buttonText.MeshUpdates==2&&b.buttonText.LastIgnoreActive,"forced refresh");});
 Test("configuration preserves backgrounds size and draw order",()=>{var(b,font,_)=Setup();var states=new[]{b.inactiveSprites,b.activeSprites,b.selectedSprites};var before=states.Select(x=>x.GetComponent<SpriteRenderer>()).Select(x=>(x.sortingLayerID,x.sortingOrder,x.size)).ToArray();LabelAdapter.ConfigureLabel(b,font);var after=states.Select(x=>x.GetComponent<SpriteRenderer>()).Select(x=>(x.sortingLayerID,x.sortingOrder,x.size)).ToArray();Check(before.SequenceEqual(after),"background changed");});
 Test("no label text uses numeric mode markers",()=>{foreach(var mode in new[]{0,1}){Check(RoleSettingsDisplay.TargetButtonText!="1"&&RoleSettingsDisplay.TargetButtonText!="2","numeric label");RoleSettingsDisplay.Toggle();}});
 Console.WriteLine($"RESULT {passed} passed, 0 failed");
 }
}
