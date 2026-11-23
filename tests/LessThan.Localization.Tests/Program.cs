using System.Reflection;
using Csv;
using TownOfHost;
class Program {
 static int passed,failed;
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static Stream Resource(string name="string")=>Assembly.GetExecutingAssembly().GetManifestResourceStream($"TownOfHost.Resources.{name}.csv");
 static int Main(){
  Test("production Translator.Init loads embedded language and achievement resources",()=>{Translator.Init();Check(Translator.translateMaps.Count>4000,"language initialization");Check(Translator.achievementMaps.Count>0,"achievement initialization");});
  var expected=new Dictionary<string,string>{
   ["LessThan"]="レスザン",["LessThanInfo"]="自分より小さいものを貪れ",["LessThanInfoLong"]="全員に1～100の数字が割り当てられる。",
   ["LessThanShowNumber"]="自分の数字を名前の横に表示",["LessThanReroll"]="再抽選",["LessThanRerollUses"]="再抽選の使用回数",
   ["LessThanResetEachMeeting"]="会議ごとに使用回数をリセット",["LessThanRerollOthers"]="他の人にも再抽選を適用する",["LessThanWinPriority"]="勝利優先度",["LessThanCanVent"]="ベントに入れる"};
  foreach(var (key,value) in expected){
   Test(key+" Japanese through real Translator",()=>Check(Translator.GetString(key,SupportedLangs.Japanese).StartsWith(value),"Japanese lookup failed"));
   Test(key+" English through real Translator",()=>{string text=Translator.GetString(key,SupportedLangs.English);Check(!text.Contains("INVALID")&&!text.StartsWith("*")&&text.Length>0,"English lookup failed");});
   Test(key+" all supported language fallbacks",()=>{foreach(var lang in Enum.GetValues<SupportedLangs>()){var text=Translator.GetString(key,lang);Check(!text.Contains("INVALID")&&text.Length>0,"invalid fallback: "+lang);if(lang is not SupportedLangs.English and not SupportedLangs.Japanese)Check(text=="*"+Translator.GetString(key,SupportedLangs.English),"English fallback: "+lang);}});
   Test(key+" exactly one header-aligned bilingual CSV row",()=>{using var stream=Resource();var rows=CsvReader.ReadFromStream(stream,new CsvOptions{HeaderMode=HeaderMode.HeaderPresent,AllowNewLineInEnclosedFieldValues=false}).Where(x=>x.Values[0]==key).ToArray();Check(rows.Length==1,"missing/duplicate key");var row=rows[0];Check(row.ColumnCount==row.Headers.Length,"column count differs from language header");Check(row.Values[1].Length>0&&row.Values[2].Length>0,"missing English/Japanese");});
  }
  Test("no CSV row exceeds the actual language header",()=>{using var stream=Resource();foreach(var row in CsvReader.ReadFromStream(stream,new CsvOptions{HeaderMode=HeaderMode.HeaderPresent,AllowNewLineInEnclosedFieldValues=false}))Check(row.Values[0].StartsWith("#") || row.ColumnCount==row.Headers.Length,"translation column count at "+row.Values[0]);});
  Test("translation CSV has no duplicate keys",()=>{using var stream=Resource();var duplicates=CsvReader.ReadFromStream(stream,new CsvOptions{HeaderMode=HeaderMode.HeaderPresent}).Where(x=>!x.Values[0].StartsWith("#")).GroupBy(x=>x.Values[0]).Where(g=>g.Count()>1).ToArray();Check(duplicates.Length==0,"duplicate translation keys");});
  Test("Translator initialization has no parse exceptions",()=>Check(Logger.Warnings.All(w=>w.Contains("翻訳用CSVに重複があります")),"unexpected parser warning"));
  Test("real GetRoleString Japanese name used by role list and winner display",()=>{TranslationController.Instance.currentLanguage.languageID=SupportedLangs.Japanese;TownOfHost.Main.JapaneseRoleName.Value=true;Check(Translator.GetRoleString("LessThan")=="レスザン","role name");});
  Test("real GetRoleString respects existing English role-name preference",()=>{TownOfHost.Main.JapaneseRoleName.Value=false;try{Check(Translator.GetRoleString("LessThan")=="LessThan","role-name preference");}finally{TownOfHost.Main.JapaneseRoleName.Value=true;}});
  Test("real GetString fallback before native controller initialization",()=>{TranslationController.InstanceExists=false;try{Check(Translator.GetString("LessThan")=="LessThan","startup English fallback");}finally{TranslationController.InstanceExists=true;}});
  Test("force-Japanese preference still works",()=>{TownOfHost.Main.ForceJapanese.Value=true;TranslationController.Instance.currentLanguage.languageID=SupportedLangs.English;try{Check(Translator.GetString("LessThan")=="レスザン","force Japanese");}finally{TownOfHost.Main.ForceJapanese.Value=false;TranslationController.Instance.currentLanguage.languageID=SupportedLangs.Japanese;}});
  Test("legacy solo-win label substitutes translated role without INVALID",()=>{var label=Translator.GetString("SoloWinOption",new Dictionary<string,string>{{"%role%",Translator.GetRoleString("LessThan")}});Check(label=="レスザンの勝利優先順位"&&!label.Contains("INVALID"),"embedded role name");});
  Test("existing kill cooldown translation is reused",()=>Check(Translator.GetString("KillCooldown",SupportedLangs.Japanese)=="キルクール","shared kill cooldown"));
  Test("existing shared vent translation is not changed for other roles",()=>Check(Translator.GetString("CanVent",SupportedLangs.Japanese)=="ベントを使える","shared vent translation"));
  Test("role intro and description keys follow existing role-description convention",()=>{Check(Translator.GetString("LessThan"+"Info")=="自分より小さいものを貪れ","intro");Check(Translator.GetString("LessThan"+"InfoLong").Contains("PlayerId"),"complete role description");});
  foreach(var role in new[]{"Idol","Damocles","Marshall","Prankster","StepBomber","Locksmith","Coroner"})Test(role+" name intro description remain bilingual",()=>{foreach(var suffix in new[]{"","Info","InfoLong"})foreach(var lang in new[]{SupportedLangs.Japanese,SupportedLangs.English}){var value=Translator.GetString(role+suffix,lang);Check(value.Length>0&&!value.Contains("INVALID"),"existing role translation "+role+suffix);}});
  foreach(var role in new[]{"MadKind","Framer","NiceHacker","Romeo","Juliet"}) Test(role+" all descriptions bilingual through real Translator",()=>{foreach(var suffix in new[]{"","Info","InfoLong"})foreach(var lang in new[]{SupportedLangs.English,SupportedLangs.Japanese}){var value=Translator.GetString(role+suffix,lang);Check(value.Length>0&&!value.Contains("INVALID")&&!value.StartsWith("*"),role+suffix+" translation");}});
  foreach(var key in new[]{"TaskTurnFortune","TaskTurnFortuneNoMeeting","TaskTurnFortuneTime","TaskTurnFortuneDistance","TaskTurnFortuneCooldown","TaskTurnFortuneDivine","PetModeSwitchTask","PetModeSwitchCombat","MadKindCrewSide","EvilTellerMeetingMode","EvilTellerNeedKills","AmnesiacRealizedKillCooldown","ReversalCanUseVent","AntiHeroNoKillCrewWin","PharaohMarkOnlyPharaoh","DaoshiCantKyonshiOwnKill","DaoshiCantKyonshiOwnKillNotice","VegaRendezvousCooldown","VegaCanUseVent"}) Test(key+" correct language columns and fallback",()=>{var en=Translator.GetString(key,SupportedLangs.English);var ja=Translator.GetString(key,SupportedLangs.Japanese);Check(en.Length>0&&ja.Length>0&&!en.Contains("INVALID")&&!ja.Contains("INVALID")&&!ja.StartsWith("*"),"bilingual key");});
  Console.WriteLine($"Results: {passed} passed, {failed} failed");return failed==0?0:1;
 }
}
