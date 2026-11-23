using TownOfHost;
partial class Program
{
    static YouTubeOAuthTokens HostTokens(string host, bool expired = false) => new(
        new YouTubeCredential("synthetic-access-" + host, false, DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 60)), "synthetic-refresh-" + host);

    static async Task ExternalBetaTests()
    {
        await TesterSetupTests();
        foreach (var code in new[] { YouTubeOAuthCode.AccessDenied, YouTubeOAuthCode.PolicyBlocked,
            YouTubeOAuthCode.InvalidClient, YouTubeOAuthCode.NetworkFailure, YouTubeOAuthCode.TimedOut,
            YouTubeOAuthCode.StateMismatch, YouTubeOAuthCode.MissingRefreshToken })
            await Test("failed relink preserves previous durable grant: " + code, async () =>
            {
                var old = HostTokens("previous"); var store = new Store { Data = old };
                var flow = new Flow { Pending = _ => Task.FromException<YouTubeOAuthTokens>(
                    new YouTubeApiException(YouTubeFailure.AuthenticationExpired, new(YouTubeOAuthStage.CallbackValidation, code))) };
                using (var auth = Service(store, flow: flow))
                {
                    await auth.BeginAsync(); Check(store.Data == old && store.Deletes == 0 && store.Saves == 0);
                    Check(auth.HasSavedLink && auth.Message.Contains("保持") && auth.LastFailure.Code == code);
                    Check(await auth.GetAsync(default) == old.Access && !auth.ConsumeRestart());
                }
                using var restored = Service(store); await restored.InitializeAsync();
                Check(restored.State == YouTubeAuthState.Linked && await restored.GetAsync(default) == old.Access);
            });
        await Test("cancel before relink commit keeps the original grant", async () =>
        {
            var old = HostTokens("old"); var store = new Store { Data = old };
            using var entered = new ManualResetEventSlim();
            var flow = new Flow { Pending = async ct => { entered.Set(); await Task.Delay(Timeout.Infinite, ct); return HostTokens("new"); } };
            using var auth = Service(store, flow: flow); var task = auth.BeginAsync();
            Check(entered.Wait(2000)); auth.CancelAuthorization(); await task;
            Check(auth.State == YouTubeAuthState.Cancelled && auth.HasSavedLink && store.Data == old && store.Deletes == 0 && store.Saves == 0);
        });
        await Test("successful account replacement publishes only the new durable grant", async () =>
        {
            var old = HostTokens("old"); var fresh = HostTokens("new"); var store = new Store { Data = old }; var exchange = new Exchange();
            using (var auth = Service(store, exchange, new Flow { Pending = _ => Task.FromResult(fresh) }))
            { await auth.BeginAsync(); Check(auth.HasSavedLink && store.Data == fresh && store.Saves == 1 && store.Deletes == 0 && exchange.Revokes == 0); }
            using var next = Service(store); await next.InitializeAsync(); Check(await next.GetAsync(default) == fresh.Access);
            // Do not revoke the previous refresh token here: Google revocation can invalidate the replacement grant too.
        });
        await Test("save failure during replacement stays fail closed without deleting the previous file", async () =>
        {
            var old = HostTokens("old"); var store = new Store { Data = old, SaveFail = true };
            using var auth = Service(store); await auth.BeginAsync();
            Check(auth.State == YouTubeAuthState.StorageError && !auth.HasSavedLink && store.Data == old && store.Deletes == 0 && store.Marker);
            await Deny(YouTubeFailure.CredentialStorageFailure, async () => await auth.GetAsync(default));
            using var next = Service(store); await next.InitializeAsync(); Check(next.State == YouTubeAuthState.StorageError);
        });
        await Test("unlink during pending replacement prevents grant resurrection", async () =>
        {
            var store = new Store { Data = HostTokens("old") }; using var entered = new ManualResetEventSlim();
            var flow = new Flow { Pending = async ct => { entered.Set(); await Task.Delay(Timeout.Infinite, ct); return HostTokens("new"); } };
            var exchange = new Exchange(); using var auth = Service(store, exchange, flow); var link = auth.BeginAsync();
            Check(entered.Wait(2000)); await auth.UnlinkAsync(); await link;
            Check(store.Data == null && !auth.HasSavedLink && store.Saves == 0 && exchange.Revokes == 1 && await auth.GetAsync(default) == null);
        });
        foreach (var stage in new[] { YouTubeOAuthStage.BrowserOpen, YouTubeOAuthStage.WaitingForRedirect,
            YouTubeOAuthStage.CallbackValidation, YouTubeOAuthStage.TokenExchange, YouTubeOAuthStage.TokenValidation, YouTubeOAuthStage.ProtectedSave })
            await Test("progress changes refresh the displayed stage: " + stage, async () =>
            {
                var source = new TaskCompletionSource<YouTubeOAuthTokens>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var auth = Service(new Store(), flow: new Flow { Pending = _ => source.Task });
                var task = auth.BeginAsync(); await Task.Delay(10); int revision = auth.Revision;
                auth.Trace(new(stage)); Check(auth.Revision > revision && auth.State == YouTubeAuthState.Authorizing && !string.IsNullOrEmpty(auth.Message));
                source.SetResult(HostTokens("new")); await task; Check(auth.State == YouTubeAuthState.Linked);
            });
        await Test("two hosts refresh distinct tokens and unlink locally independently", async () =>
        {
            var a = new Store { Data = HostTokens("a", true) }; var b = new Store { Data = HostTokens("b", true) };
            var ea = new Exchange { Pending = _ => Task.FromResult(HostTokens("a-new")) };
            var eb = new Exchange { Pending = _ => Task.FromResult(HostTokens("b-new")) };
            using var one = Service(a, ea); using var two = Service(b, eb);
            var result = await Task.WhenAll(one.GetAsync(default), two.GetAsync(default));
            Check(result[0].Secret != result[1].Secret && ea.Last["refresh_token"] != eb.Last["refresh_token"]);
            await one.UnlinkAsync(); Check(b.Data != null && two.HasSavedLink && eb.Revokes == 0 && b.Deletes == 0);
        });
        await Test("DPAPI stores in separate host directories restore without HTTP", async () =>
        {
            var a = new YouTubeProtectedTokenStore(Temp()); var b = new YouTubeProtectedTokenStore(Temp());
            try
            {
                a.Save(Id, HostTokens("a")); b.Save(Id, HostTokens("b")); var ea = new Exchange(); var eb = new Exchange();
                using var one = new YouTubeAuthService(new(Id), a, new Flow(), ea); using var two = new YouTubeAuthService(new(Id), b, new Flow(), eb);
                await Task.WhenAll(one.InitializeAsync(), two.InitializeAsync());
                Check((await one.GetAsync(default)).Secret != (await two.GetAsync(default)).Secret && ea.Refreshes == 0 && eb.Refreshes == 0);
                await one.UnlinkAsync(); Check(a.Load(Id) == null && b.Load(Id) != null);
            }
            finally { a.Delete(); b.Delete(); }
        });
        await Test("access denied explains uncertainty instead of asserting tester registration", () =>
        {
            var failure = new YouTubeOAuthDiagnostic(YouTubeOAuthStage.CallbackValidation, YouTubeOAuthCode.AccessDenied);
            Check(failure.UserMessage.Contains("未登録") && failure.UserMessage.Contains("考えられます") && !failure.SafeLog.Contains(Id)); return Task.CompletedTask;
        });
    }
}
