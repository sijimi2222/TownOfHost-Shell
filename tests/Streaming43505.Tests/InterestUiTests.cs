using System.Reflection;using TownOfHost;using UnityEngine;
partial class Program{
static T UiField<T>(string name)=> (T)typeof(StreamerHopeMenu).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
static void Click(string name){var b=UiField<ToggleButtonBehaviour>(name);Check(b.gameObject.activeInHierarchy,"button inactive "+name);b.GetComponent<PassiveButton>().OnClick.Invoke();}
static string[] UiNames()=>UiField<List<TMPro.TextMeshPro>>("rows").Select(x=>x.text).ToArray();
static void RunInterestUi(){
var menu=new OptionsMenuBehaviour();TownOfHost.Modules.ClientOptions.ClientActionItem.CustomBackground.transform.parent=new GameObject().transform;
Test("actual interest UI initially hidden",()=>{ResetStream();StreamerHopeMenu.Init(menu);Check(!StreamerHopeMenu.Popup.gameObject.activeSelf);});
Test("actual UI shows count without game identities",()=>{StreamerHopeMenu.Show(menu);Check(StreamerHopeMenu.TitleText.text.Contains("0人")&&UiNames().Length==0);});
Test("actual reception button starts and stops without HTTP",()=>{Click("receptionButton");Check(StreamerInfo.Interests.Accepting);Click("receptionButton");Check(!StreamerInfo.Interests.Accepting);});
Test("actual UI renders only YouTube names",()=>{Click("receptionButton");var(i,h)=FakeStream();Queue(i,"ui1","private-channel","join: NotAGameIdentity",name:"YouTube Name");Drain();StreamerHopeMenu.Tick();Check(UiNames().SequenceEqual(new[]{"YouTube Name"})&&StreamerHopeMenu.TitleText.text.Contains("1人"));StreamerInfo.StopNetwork();});
Test("repeat show does not duplicate rows",()=>{for(int i=0;i<4;i++)StreamerHopeMenu.Show(menu);Check(UiNames().Length==1&&StreamerHopeMenu.Popup.transform.children.Count(x=>x.gameObject.name=="InterestDisplayName"&&x.gameObject.activeSelf)==1);});
Test("actual list reset distinct from comment-position reset",()=>{Click("resetButton");Check(StreamerInfo.Interests.Count==0&&UiNames().Length==0&&StreamerHopeMenu.TitleText.text.Contains("0人"));});
Test("all 200 names accessible without overlapping rows",()=>{StreamerInfo.StartInterestReception();for(int n=0;n<200;n++)Apply(StreamerInfo.Interests,Interest("u"+n,"Viewer "+n));StreamerHopeMenu.Tick();var all=new List<string>(UiNames());for(int p=1;p<7;p++){Click("nextButton");all.AddRange(UiNames());}Check(all.Count==200&&all.Distinct().Count()==200&&StreamerHopeMenu.TitleText.text.Contains("200人"));Check(!UiField<ToggleButtonBehaviour>("nextButton").gameObject.activeSelf);});
Test("actual previous page and clear after page 7 clamp safely",()=>{Click("previousButton");Check(UiNames().Length==30);Click("resetButton");Check(UiNames().Length==0&&!UiField<ToggleButtonBehaviour>("previousButton").gameObject.activeSelf);});
Test("actual UI updates after name changes",()=>{Apply(StreamerInfo.Interests,Interest("rename","Before"));StreamerHopeMenu.Tick();Apply(StreamerInfo.Interests,Interest("rename","After"));StreamerHopeMenu.Tick();Check(UiNames().SequenceEqual(new[]{"After"})&&StreamerHopeMenu.TitleText.text.Contains("1人"));});
Test("hidden UI clears rows and can reopen",()=>{StreamerHopeMenu.Hide();Check(UiNames().Length==0&&!StreamerHopeMenu.Popup.gameObject.activeSelf);StreamerHopeMenu.Show(menu);Check(UiNames().SequenceEqual(new[]{"After"}));});
Test("nonhost cannot operate host intake controls",()=>{AmongUsClient.Instance.AmHost=false;StreamerHopeMenu.Tick();Check(!UiField<ToggleButtonBehaviour>("receptionButton").gameObject.activeSelf&&!UiField<ToggleButtonBehaviour>("resetButton").gameObject.activeSelf);AmongUsClient.Instance.AmHost=true;StreamerHopeMenu.Tick();});
Test("repeated initialization disposes previous panel",()=>{var old=StreamerHopeMenu.Popup;StreamerHopeMenu.Init(menu);Check(old.gameObject.Destroyed&&!StreamerHopeMenu.Popup.gameObject.activeSelf);});
StreamerHopeMenu.Hide();StreamerInfo.DisconnectInternal();
}
}
