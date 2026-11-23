using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using TownOfHost;
class Program {
 static int passed,failed;
 static string Here([CallerFilePath]string file="")=>Path.GetDirectoryName(file);
 static string Root=>Environment.GetEnvironmentVariable("TOHS_GUESS_SOURCE_ROOT")??Path.GetFullPath(Path.Combine(Here(),"../.."));
 static void Check(bool ok,string why="assertion"){if(!ok)throw new Exception(why);}
 static void Test(string name,Action action){Translator.Language=2;PlayerCatch.AllPlayerControls.Clear();TellerGate.TellerCannotGuess=null;try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static void Load(){using var csv=new TextFieldParser(Path.Combine(Root,"Resources/string.csv"),Encoding.UTF8);csv.SetDelimiters(",");csv.HasFieldsEnclosedInQuotes=true;while(!csv.EndOfData){var row=csv.ReadFields();if(row?.Length>1)Translator.Rows[row[0]]=row;}}
 static Dictionary<string,int> Mappings(){var rows=new Dictionary<string,int>();foreach(int language in new[]{1,2}){Translator.Language=language;foreach(var role in Enum.GetValues<CustomRoles>())foreach(var input in new[]{role.ToString(),Translator.GetString(role.ToString())}.Distinct())rows[language+"|"+input]=UtilsRoleInfo.GetRoleByInputName(input,out var result,true)?(int)result:-1;}return rows;}
 static void Main(string[] args){Load();
 if(args.Contains("--capture-baseline")){File.WriteAllText(Path.Combine(Here(),"BaselineMappings.json"),JsonSerializer.Serialize(Mappings(),new JsonSerializerOptions{WriteIndented=true}));return;}
 Test("compiled slices match current production source",()=>{foreach(var (name,expected) in JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Here(),"FixtureHashes.json")))){string source=File.ReadAllText(Path.Combine(Root,name)).Replace("\r\n","\n");Check(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))==expected,name);}});
 foreach(var input in new[]{"かけだし占い師","駆け出し占い師","かけだしうらないし","カケダシウラナイシ","AmateurTeller","amateurteller","AMATEURTELLER"})Test("recognizes AmateurTeller: "+input,()=>{Check(UtilsRoleInfo.GetRoleByInputName(input,out var role,true)&&role==CustomRoles.AmateurTeller);});
 foreach(int language in new[]{1,2})foreach(string input in new[]{"かけだし占い師","AmateurTeller"})Test("language-independent official name "+language+" "+input,()=>{Translator.Language=language;Check(UtilsRoleInfo.GetRoleByInputName(input,out var role,true)&&role==CustomRoles.AmateurTeller);});
 foreach(string selector in new[]{"1","赤","red"})Test("shared guess command resolves target and role: "+selector,()=>{PlayerCatch.AllPlayerControls.Add(new(){PlayerId=1,Role=CustomRoles.AmateurTeller});Check(GuessManager.Parse("/cmd /bt "+selector+" かけだし占い師",out var id,out var role,out var error)&&id==1&&role==CustomRoles.AmateurTeller&&error=="");});
 Test("correct target match retains success",()=>Check(!GuessManager.Wrong(new(){Role=CustomRoles.AmateurTeller},CustomRoles.AmateurTeller)));
 Test("incorrect target match retains misfire",()=>Check(GuessManager.Wrong(new(){Role=CustomRoles.AmateurTeller},CustomRoles.Sheriff)));
 foreach(var role in new[]{CustomRoles.EvilGuesser,CustomRoles.NiceGuesser,CustomRoles.Nova,CustomRoles.Sheriff,CustomRoles.FortuneTeller,CustomRoles.SuspiciousTeller,CustomRoles.PonkotuTeller,CustomRoles.Destroyer,CustomRoles.Onmyoji})Test("existing role matching: "+role,()=>{Check(UtilsRoleInfo.GetRoleByInputName(Translator.GetString(role.ToString()),out var found,true)&&found==role);Check(!GuessManager.Wrong(new(){Role=role},found));});
 Test("teller restriction concerns shooter and does not block EvilGuesser",()=>{TellerGate.TellerCannotGuess=new(){Value=true};Check(TellerGate.IsBlockedTeller(CustomRoles.AmateurTeller)&&!TellerGate.IsBlockedTeller(CustomRoles.EvilGuesser));TellerGate.TellerCannotGuess.Value=false;Check(!TellerGate.IsBlockedTeller(CustomRoles.AmateurTeller));});
 Test("includeVanilla restriction is preserved",()=>{Check(!UtilsRoleInfo.GetRoleByInputName("Crewmate",out _,false));Check(UtilsRoleInfo.GetRoleByInputName("Crewmate",out var role,true)&&role==CustomRoles.Crewmate);});
 Test("dead target remains rejected",()=>{PlayerCatch.AllPlayerControls.Add(new(){PlayerId=1,Data=new(){IsDead=true}});Check(!GuessManager.Parse("/cmd /bt 1 かけだし占い師",out _,out _,out _));});
 Test("unknown role is not guessed",()=>Check(!UtilsRoleInfo.GetRoleByInputName("存在しない役職",out _,true)));
 Test("registered class and translation use AmateurTeller, not a duplicate role",()=>{Check(Enum.GetNames<CustomRoles>().Contains("AmateurTeller")&&!Enum.GetNames<CustomRoles>().Contains("BeginnerTeller"));Check(Translator.Rows["AmateurTeller"][1]=="AmateurTeller"&&Translator.Rows["AmateurTeller"][2]=="かけだし占い師");var source=File.ReadAllText(Path.Combine(Root,"Roles/Crewmate/AmateurTeller.cs"));Check(source.Contains("CustomRoles.AmateurTeller")&&source.Contains("30100")&&!source.Contains("override bool? CheckGuess"));});
 Test("all other role-name mappings retain baseline behavior",()=>{var baseline=JsonSerializer.Deserialize<Dictionary<string,int>>(File.ReadAllText(Path.Combine(Here(),"BaselineMappings.json")));var now=Mappings();foreach(var (key,value) in baseline){if(key.EndsWith("|かけだし占い師"))continue;Check(now.TryGetValue(key,out int actual)&&actual==value,"changed mapping: "+key);}});
 Console.WriteLine($"ROLE AmateurTeller={(int)CustomRoles.AmateurTeller}; optionBase=30100");
 Console.WriteLine($"RESULT {passed} passed, {failed} failed (compiled production parsing and match slices, synthetic players)");Environment.ExitCode=failed==0?0:1;
 }
}
