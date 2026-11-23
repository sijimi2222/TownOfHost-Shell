using BepInEx.Configuration;using TownOfHost;using TownOfHost.Modules.ClientOptions;
class Program {
 static int passed;static void Check(bool b){if(!b)throw new Exception("assertion failed");}static void Test(string s,Action a){a();passed++;Console.WriteLine("PASS "+s);}
 static string root=Path.Combine(Path.GetTempPath(),"ShellWavePrefs-"+Guid.NewGuid().ToString("N"));
 static ConfigFile File(string n)=>new(Path.Combine(root,n+".cfg"),true);
 static void SetMain(ConfigFile f){TownOfHost.Main.EnableDiscordRichPresence=f.Bind("Client Options","Enable Discord Rich Presence",true);TownOfHost.Main.ShowRoomCode=f.Bind("Client Options","Show Room Code",false);TownOfHost.Main.DisableWaveAnimation=WaveAnimationPreferences.Bind(f);}
 static void CrossProcess(string name,bool value){var f=File(name);WaveAnimationPreferences.Bind(f).Value=value;var start=new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath){UseShellExecute=false,CreateNoWindow=true};start.ArgumentList.Add("--read");start.ArgumentList.Add(f.ConfigFilePath);start.ArgumentList.Add(value.ToString());using var p=System.Diagnostics.Process.Start(start);Check(p.WaitForExit(10000)&&p.ExitCode==0);}
 static void MainMethod(){
 Test("ON value survives a separate process restart",()=>CrossProcess("processOn",true));
 Test("OFF value survives a separate process restart",()=>CrossProcess("processOff",false));
 Test("real BepInEx configuration defaults to waves enabled",()=>Check(!WaveAnimationPreferences.Bind(File("default")).Value));
 Test("real configuration uses the exact client section and unique key",()=>{var x=WaveAnimationPreferences.Bind(File("keys"));Check(x.Definition.Section=="Client Options"&&x.Definition.Key=="Disable Wave Animation");});
 Test("real configuration writes ON and reloads it from disk",()=>{var f=File("on");WaveAnimationPreferences.Bind(f).Value=true;Check(WaveAnimationPreferences.Bind(File("on")).Value);});
 Test("real configuration writes OFF and reloads it from disk",()=>{var f=File("off");var x=WaveAnimationPreferences.Bind(f);x.Value=true;x.Value=false;Check(!WaveAnimationPreferences.Bind(File("off")).Value);});
 Test("existing configurations gain a default without changing prior settings",()=>{var f=File("upgrade");f.Bind("Client Options","Show Room Code",false).Value=true;f.Bind("Client Options","Enable Discord Rich Presence",true).Value=false;SetMain(File("upgrade"));Check(!TownOfHost.Main.DisableWaveAnimation.Value&&TownOfHost.Main.ShowRoomCode.Value&&!TownOfHost.Main.EnableDiscordRichPresence.Value);});
 Test("binding the same configuration again retains its value",()=>{var f=File("rebind");WaveAnimationPreferences.Bind(f).Value=true;Check(WaveAnimationPreferences.Bind(f).Value);});
 Test("configuration instances remain independent",()=>{WaveAnimationPreferences.Bind(File("independentA")).Value=true;Check(!WaveAnimationPreferences.Bind(File("independentB")).Value);});
 Test("null configuration is explicitly rejected",()=>{bool ok=false;try{WaveAnimationPreferences.Bind(null);}catch(ArgumentNullException){ok=true;}Check(ok);});
 Test("existing toggle implementation saves ON on click",()=>{SetMain(File("clickOn"));Registration.Clear();Registration.Build(new());ClientActionItem.Items.Last().Click();Check(WaveAnimationPreferences.Bind(File("clickOn")).Value);});
 Test("existing toggle implementation saves OFF on second click",()=>{SetMain(File("clickOff"));Registration.Clear();Registration.Build(new());var b=ClientActionItem.Items.Last();b.Click();b.Click();Check(!WaveAnimationPreferences.Bind(File("clickOff")).Value);});
 Test("toggle uses red OFF and green ON without changing other toggles",()=>{SetMain(File("colors"));Registration.Clear();Registration.Build(new());var b=ClientActionItem.Items.Last();Check(b.ToggleButton.Background.color.Id==0);b.Click();Check(b.ToggleButton.Background.color.Id==1&&b.ToggleButton.Rollover.Last.Id==1&&!TownOfHost.Main.ShowRoomCode.Value&&TownOfHost.Main.EnableDiscordRichPresence.Value);});
 Test("registration appends after both existing second-page entries",()=>{SetMain(File("registration"));Registration.Clear();Registration.Build(new());Check(ClientActionItem.Items.Select(x=>x.Name).SequenceEqual(new[]{"EnableDiscordRichPresence","ShowRoomCode","DisableWaveAnimation"}));Check(ClientActionItem.Items.All(x=>x.Page==1));});
 Test("reopening the options menu does not duplicate registered toggles",()=>{SetMain(File("repeat"));Registration.Clear();for(int i=0;i<10;i++)Registration.Build(new());Check(ClientActionItem.Items.Count==3);});
 Test("destroyed native toggle is recreated using the saved configuration",()=>{SetMain(File("destroy"));Registration.Clear();Registration.Build(new());var b=ClientActionItem.Items.Last();b.Click();b.ToggleButton=null;Registration.Build(new());Check(ClientActionItem.Items.Count==4&&ClientActionItem.Items.Last().Name=="DisableWaveAnimation"&&ClientActionItem.Items.Last().ToggleButton.Background.color.Id==1);});
 Test("reloading after external save updates the bound entry",()=>{var a=File("reload");var x=WaveAnimationPreferences.Bind(a);var b=File("reload");WaveAnimationPreferences.Bind(b).Value=true;a.Reload();Check(x.Value);});
 }
 static void Main(string[] args){if(args.Length==3&&args[0]=="--read"){var f=new ConfigFile(args[1],false);Environment.ExitCode=WaveAnimationPreferences.Bind(f).Value==bool.Parse(args[2])?0:1;return;}try{Directory.CreateDirectory(root);MainMethod();Console.WriteLine($"RESULT {passed} passed, 0 failed (real BepInEx configuration and actual toggle; native UI substitutes)");}finally{Directory.Delete(root,true);}}
}
