using TownOfHost;using Csv;using System.Reflection;
class Program {
 static int passed,failed;static void Check(bool value,string reason="assertion"){if(!value)throw new Exception(reason);}static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static Stream Resource()=>Assembly.GetExecutingAssembly().GetManifestResourceStream("TownOfHost.Resources.string.csv");
 static int Main(){
  Test("real translator initialization",()=>{Translator.Init();Check(Translator.translateMaps.Count>4000);Check(Translator.achievementMaps.Count>0);});
  string[] keys={"Nova","NovaInfo","NovaInfoLong","NovaFireMode","NovaFireModeFree","NovaFireModeLeftRight","NovaFireModeFourWay","NovaFireCooldown","NovaBeamSize","NovaChargeTime","NovaFireDelay","NovaCharging","NovaCharged","NovaWaiting","NovaBeaming","NovaControls","NovaPreparing","NovaVisualUnavailable","NovaFire"};
  foreach(string key in keys){
   foreach(var lang in new[]{SupportedLangs.English,SupportedLangs.Japanese})Test(key+" "+lang,()=>{var text=Translator.GetString(key,lang);Check(text.Length>0&&!text.Contains("INVALID")&&!text.StartsWith("*"));});
   Test(key+" other language fallback",()=>{foreach(var lang in Enum.GetValues<SupportedLangs>())if(lang is not SupportedLangs.English and not SupportedLangs.Japanese)Check(Translator.GetString(key,lang)=="*"+Translator.GetString(key,SupportedLangs.English));});
   Test(key+" CSV header alignment",()=>{using var input=Resource();var rows=CsvReader.ReadFromStream(input,new CsvOptions{HeaderMode=HeaderMode.HeaderPresent}).Where(r=>r.Values[0]==key).ToArray();Check(rows.Length==1);Check(rows[0].ColumnCount==8&&rows[0].Headers.Length==8);});
  }
  Test("Japanese role name and intro",()=>{Check(Translator.GetString("Nova",SupportedLangs.Japanese)=="ノヴァ");Check(Translator.GetString("NovaInfo",SupportedLangs.Japanese)=="星の産声");});
  foreach(var lang in new[]{SupportedLangs.English,SupportedLangs.Japanese})Test("charge formatting "+lang,()=>{var value=string.Format(Translator.GetString("NovaCharging",lang),1.5f,90);Check(value.Contains("1.5")&&value.Contains("90"));});
  foreach(var lang in new[]{SupportedLangs.English,SupportedLangs.Japanese})Test("delay formatting "+lang,()=>Check(string.Format(Translator.GetString("NovaWaiting",lang),.5f).Contains("0.5")));
  Test("description uses official Phantom controls",()=>{var ja=Translator.GetString("NovaInfoLong",SupportedLangs.Japanese);Check(ja.Contains("ファントム")&&ja.Contains("もう一度")&&!ja.Contains("ペット"));});
  Test("all translation rows match header and are unique",()=>{using var input=Resource();var rows=CsvReader.ReadFromStream(input,new CsvOptions{HeaderMode=HeaderMode.HeaderPresent}).Where(r=>!r.Values[0].StartsWith("#")).ToArray();Check(rows.All(r=>r.ColumnCount==8));Check(rows.Select(r=>r.Values[0]).Distinct().Count()==rows.Length);});
  Test("no unexpected CSV parse warning",()=>Check(Logger.Warnings.All(w=>w.Contains("翻訳用CSVに重複があります"))));
  Console.WriteLine($"NOVA_LOCALIZATION_RESULT {passed} passed, {failed} failed");return failed==0?0:1;
 }
}