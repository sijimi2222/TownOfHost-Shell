using System.Diagnostics;
using System.Text.Json;
using TownOfHost;
partial class Program
{
    static async Task<(int Exit, string Text)> RunSetup(string game, string json, string approval, string credentials)
    {
        string root = Environment.GetEnvironmentVariable("TOHS_VERSION_SOURCE") ?? throw new Exception("source root required");
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-File", Path.Combine(root,"tools/YouTubeBeta/Setup-YouTubeTester.ps1"),
            "-GoogleClientJson", json, "-GameDirectory", game, "-ApprovalRecord", approval, "-CredentialDirectory", credentials }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)); await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await stdout + await stderr);
    }
    static (string Root, string Game, string Json, string Approval, string Credentials, string Config) SetupFixture()
    {
        string root = Temp(), game = Path.Combine(root,"game"); Directory.CreateDirectory(Path.Combine(game,"BepInEx/plugins"));
        File.WriteAllText(Path.Combine(game,"Among Us.exe"),"synthetic fixture only; never executed");
        string json = Path.Combine(root,"desktop.json"), approval = Path.Combine(root,"permission.json"), credentials = Path.Combine(root,"credentials");
        File.WriteAllText(json,JsonSerializer.Serialize(new { installed = new { client_id = Id, client_secret = "synthetic-desktop-secret-private" } }));
        File.WriteAllText(approval,JsonSerializer.Serialize(new { purpose = "youtube-desktop-credential-provisioning", decision = "approved", reference = "synthetic fixture - not real permission" }));
        return (root,game,json,approval,credentials,Path.Combine(game,"BepInEx/config/TOH-Shell/YouTube/oauth-client.json"));
    }
    static async Task TesterSetupTests()
    {
        await Test("offline tester setup needs no JSON editing and protects its secret", async () =>
        {
            var f = SetupFixture(); var result = await RunSetup(f.Game,f.Json,f.Approval,f.Credentials);
            Check(result.Exit == 0 && File.Exists(f.Config), result.Text);
            Check(!result.Text.Contains(Id) && !result.Text.Contains("synthetic-desktop-secret-private"));
            Check(!File.ReadAllText(f.Config).Contains("secret") && File.ReadAllText(f.Config).Contains(Id));
            Check(YouTubeOAuthSettings.Load(f.Config,f.Credentials) != null && !File.Exists(Path.Combine(f.Credentials,"oauth.dpapi")));
        });
        await Test("tester setup preserves existing grants and unrelated game settings", async () =>
        {
            var f = SetupFixture(); Directory.CreateDirectory(Path.GetDirectoryName(f.Config)); File.WriteAllText(f.Config,JsonSerializer.Serialize(new {client_id=Id}));
            string cfg = Path.Combine(f.Game,"BepInEx/config/untouched.cfg"); File.WriteAllText(cfg,"ShowComments = false");
            var store = new YouTubeProtectedTokenStore(f.Credentials); store.Save(Id,HostTokens("existing"));
            byte[] before = File.ReadAllBytes(Path.Combine(f.Credentials,"oauth.dpapi"));
            var result = await RunSetup(f.Game,f.Json,f.Approval,f.Credentials); Check(result.Exit == 0, result.Text);
            Check(before.SequenceEqual(File.ReadAllBytes(Path.Combine(f.Credentials,"oauth.dpapi"))) && File.ReadAllText(cfg)=="ShowComments = false");
            Check(store.Load(Id).RefreshToken=="synthetic-refresh-existing"); store.Delete();
        });
        await Test("tester setup refuses a different configured client without mutation", async () =>
        {
            var f = SetupFixture(); Directory.CreateDirectory(Path.GetDirectoryName(f.Config)); File.WriteAllText(f.Config,"{\"client_id\":\"synthetic-other.apps.googleusercontent.com\"}");
            string before = File.ReadAllText(f.Config); var result = await RunSetup(f.Game,f.Json,f.Approval,f.Credentials);
            Check(result.Exit != 0 && result.Text.Contains("stage=Validate") && File.ReadAllText(f.Config)==before && !Directory.Exists(f.Credentials));
            Check(!result.Text.Contains(Id) && !result.Text.Contains("synthetic-other"));
        });
        await Test("tester setup refuses unapproved provisioning before reading credentials", async () =>
        {
            var f = SetupFixture(); File.Delete(f.Approval); File.Delete(f.Json);
            var result = await RunSetup(f.Game,f.Json,f.Approval,f.Credentials);
            Check(result.Exit != 0 && result.Text.Contains("stage=Approval") && !File.Exists(f.Config) && !Directory.Exists(f.Credentials));
        });
    }
}
