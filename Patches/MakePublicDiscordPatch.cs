using System;

using System.IO;
using System.Collections.Generic;

using System.Net.Http;

using System.Text;

using System.Text.Json;

using System.Threading.Tasks;

using HarmonyLib;

using InnerNet;

using TownOfHost.Roles.Core;

using static TownOfHost.Utils;



namespace TownOfHost

{

    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]

    internal static class SetupDiscordMatchmakingButtonsPatch

    {

        [HarmonyPostfix]

        public static void Postfix(GameStartManager __instance)

        {

            DiscordMatchmakingRelayService.RefreshRecruitmentButtons(__instance);

        }

    }



    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.MakePublic))]

    internal static class MakePublicDiscordBotPatch

    {

        [HarmonyPrefix]

        [HarmonyPriority(Priority.Last)]

        public static bool Prefix(GameStartManager __instance)

        {

            DiscordMatchmakingRelayService.ToggleRecruitment(__instance);

            return false;

        }

    }



    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]

    internal static class UpdateDiscordMatchmakingRelayPatch

    {

        [HarmonyPostfix]

        public static void Postfix()

        {

            DiscordMatchmakingRelayService.Tick();

        }

    }



    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]

    internal static class DeleteDiscordMatchmakingOnExitPatch

    {

        [HarmonyPrefix]

        public static void Prefix()

        {

            DiscordMatchmakingRelayService.TryDelete("ExitGame");

        }

    }



    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]

    internal static class DeleteDiscordMatchmakingOnDisconnectPatch

    {

        [HarmonyPrefix]

        public static void Prefix()

        {

            DiscordMatchmakingRelayService.TryDelete("OnDisconnected");

        }

    }



    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.StartGame))]

    internal static class UpdateDiscordMatchmakingOnStartGamePatch

    {

        [HarmonyPostfix]

        public static void Postfix()

        {

            DiscordMatchmakingRelayService.RequestImmediateUpdate("StartGame");

        }

    }



    internal static class DiscordMatchmakingRelayService

    {

        private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };

        private static readonly object Sync = new();




        // Session objects keep late responses from modifying a newer recruitment.
        private sealed class Recruitment
        {
            public LobbySnapshot Lobby = new();
            public readonly Modules.MatchmakingRelayClient Relay = new(Client, RelayUrl, allowLoopback: Modules.MatchmakingRelayEndpoint.DevelopmentBuild);
            public long LastSentAtMs;
            public int ConsecutiveFailures;
            public long RetryAtMs;
            public string LastSnapshot = "";
            public string DeleteReason = "";
            public bool Closed;
            public bool UpsertPending;
            public bool DeletePending;
            public long RetryDeleteAtMs;
        }

        private sealed record LobbySnapshot(
            string HostName = "Unknown Host", string RoomCode = "", string State = "Unknown",
            int Players = 0, int MaxPlayers = 0, int ProgressPercent = 0,
            string Region = "Unknown", string Map = "Unknown", string GameMode = "Unknown",
            string ShellVersion = "", string ThreadComment = "");

        private static readonly List<Recruitment> Recruitments = new();
        private static Recruitment _current;
        private static Task _sendTail = Task.CompletedTask;
        private static bool _activeRecruitment;
        private static bool _forceUpdateRequested;
        private static long _nextUpdateAtMs;
        private const int UpdateIntervalMs = 6000;
        private static GameStartManager _buttons;
        private static bool _failureNotice;
        private static bool _maintenanceNotice;
        private static bool _refreshButtons;
        private static long _nextRecruitmentStartAtMs;
        private const int MaxConsecutiveFailures = 5;
        private const int RestartCooldownMs = 15000;
        private static string RelayUrl => Modules.MatchmakingRelayEndpoint.Resolve(Main.MatchmakingRelayUrl, Main.MatchmakingDevelopmentRelayUrl);
        private static bool RelayConfigured => !string.IsNullOrEmpty(RelayUrl);


        public static void ToggleRecruitment(GameStartManager gameStartManager)

        {

            if (!RelayConfigured)

            {

                var message = "現在マッチメイキング機能を使うことができません。";

                Logger.Info(message, nameof(DiscordMatchmakingRelayService));

                Logger.seeingame(message);

                return;

            }

            if (!CanSend()) return;
            _buttons = gameStartManager;
            bool enable;

            lock (Sync)

                enable = !_activeRecruitment;



            if (enable)

                enable = StartRecruitment();

            else

                TryDelete("RecruitmentDisabled");



            UpdateRecruitmentButtons(gameStartManager, enable);

            Logger.Info($"MODマッチメイキング募集: {(enable ? "ON" : "OFF")}（バニラ部屋は非公開のまま）", nameof(DiscordMatchmakingRelayService));

        }




        private static bool StartRecruitment()
        {
            try
            {
                if (!CanSend() || !RelayConfigured) return false;
                
                if (!TryCollectLobby(out var lobby)) return false;
                lock (Sync)
                {
                    if (_activeRecruitment) return true;
                    if (Recruitments.Count >= 4 || Environment.TickCount64 < _nextRecruitmentStartAtMs)
                    {
                        Logger.seeingame("募集を再開するまで少しお待ちください。");
                        return false;
                    }
                    _nextRecruitmentStartAtMs = Environment.TickCount64 + RestartCooldownMs;
                    _current = new Recruitment { Lobby = lobby };
                    Recruitments.Add(_current);
                    _activeRecruitment = true;
                    _forceUpdateRequested = false;
                    _nextUpdateAtMs = Environment.TickCount64 + UpdateIntervalMs;

                    SendUpsert(_current, lobby, "MakePublic");
                }
                return true;
            }
            catch (Exception)
            {
                Logger.Warn("募集処理を完了できませんでした。詳細情報は記録しません。", nameof(DiscordMatchmakingRelayService));
                return false;
            }
        }

        public static void Tick()
        {
            try
            {
                if (Main.IsAndroid()) return;
                if (!RelayConfigured) TryDelete("ConfigurationDisabled");
                if (_maintenanceNotice)
                {
                    _maintenanceNotice = false;
                    Logger.seeingame("現在マッチメイキング機能を使うことができません。");
                    UpdateRecruitmentButtons(_buttons, false);
                }
                if (_refreshButtons)
                {
                    _refreshButtons = false;
                    UpdateRecruitmentButtons(_buttons, _activeRecruitment);
                }
                if (_failureNotice)
                {
                    _failureNotice = false;
                    Logger.seeingame("募集サービスに接続できませんでした。時間をおいて募集を再開してください。");
                    UpdateRecruitmentButtons(_buttons, false);
                }
                var nowMs = Environment.TickCount64;
                lock (Sync)
                {
                    // Owned closed sessions retry even after leaving the room / losing host status.
                    foreach (var recruitment in Recruitments)
                        if (recruitment.Closed && !recruitment.DeletePending && nowMs >= recruitment.RetryDeleteAtMs)
                            QueueDelete(recruitment);
                    if (!_activeRecruitment) return;
                    if (!_forceUpdateRequested && nowMs < _nextUpdateAtMs) return;
                    _forceUpdateRequested = false;
                    _nextUpdateAtMs = nowMs + UpdateIntervalMs;
                }
                if (!CanSend())
                {
                    TryDelete("HostLost");
                    return;
                }
                if (!TryCollectLobby(out var lobby)) return;
                lock (Sync)
                {
                    if (_current == null || _current.Closed) return;
                    if (_current.Lobby.RoomCode != lobby.RoomCode)
                    {
                        TryDelete("RoomChanged");
                        return;
                    }
                    _current.Lobby = lobby;
                    var snapshot = BuildPayload(lobby);
                    if (_current.UpsertPending || nowMs < _current.RetryAtMs
                        || (snapshot == _current.LastSnapshot && nowMs - _current.LastSentAtMs < 120000)) return;
                    SendUpsert(_current, lobby, "Tick");
                }
            }
            catch (Exception)
            {
                Logger.Warn("募集処理を完了できませんでした。詳細情報は記録しません。", nameof(DiscordMatchmakingRelayService));
            }
        }

        public static void TryDelete(string reason)
        {
            if (Main.IsAndroid()) return;
            lock (Sync)
            {
                // Never derive a delete target from the room a guest happens to be in.
                if (_current == null || _current.Closed) return;
                var recruitment = _current;
                recruitment.Closed = true;
                recruitment.DeleteReason = reason ?? "";
                _current = null;
                _activeRecruitment = false;
                _forceUpdateRequested = false;
                QueueDelete(recruitment);
            }
        }

        // All queue operations are made under Sync. One outstanding upsert per session coalesces
        // repeated polls, and the task chain preserves enqueue order without blocking Unity.
        private static void SendUpsert(Recruitment recruitment, LobbySnapshot lobby, string reason)
        {
            if (recruitment.UpsertPending || recruitment.Closed) return;
            recruitment.UpsertPending = true;
            EnqueueSend(recruitment, lobby, "upsert", reason);
        }

        private static void QueueDelete(Recruitment recruitment)
        {
            if (recruitment.DeletePending) return;
            recruitment.DeletePending = true;
            EnqueueSend(recruitment, recruitment.Lobby with { State = "Closed", Players = 0, MaxPlayers = 0, ProgressPercent = 0 },
                "delete", recruitment.DeleteReason);
        }

        private static void EnqueueSend(Recruitment recruitment, LobbySnapshot lobby, string action, string reason)
        {
            var previous = _sendTail;
            _sendTail = Task.Run(async () =>
            {
                await previous.ConfigureAwait(false);
                await SendCore(recruitment, lobby, action, reason).ConfigureAwait(false);
            });
        }

        private static async Task SendCore(Recruitment recruitment, LobbySnapshot lobby, string action, string reason)
        {
            var end = action == "delete";
            var retrySeconds = 6;
            try
            {
                var result = await recruitment.Relay.SendAsync(end ? "" : BuildPayload(lobby), end).ConfigureAwait(false);
                retrySeconds = result.RetrySeconds;
                lock (Sync)
                {
                    if (result.Status == Modules.MatchmakingRelayClient.Outcome.Success)
                    {
                        recruitment.LastSnapshot = recruitment.Relay.LastAppliedLobby;
                        recruitment.LastSentAtMs = Environment.TickCount64;
                        recruitment.RetryAtMs = 0;
                        recruitment.ConsecutiveFailures = 0;
                        _refreshButtons = true;
                    }
                    else if (result.Status == Modules.MatchmakingRelayClient.Outcome.Retry)
                    {
                        recruitment.RetryAtMs = Environment.TickCount64 + retrySeconds * 1000L;
                        recruitment.ConsecutiveFailures++;
                        if (recruitment.ConsecutiveFailures >= MaxConsecutiveFailures)
                        {
                            recruitment.Closed = true;
                            if (end || !recruitment.Relay.HasSession) Recruitments.Remove(recruitment);
                            else recruitment.ConsecutiveFailures = 0; // separate bounded best-effort close budget
                            if (ReferenceEquals(_current, recruitment))
                            {
                                _current = null; _activeRecruitment = false; _failureNotice = true;
                            }
                        }
                    }
                    else
                    {
                        Recruitments.Remove(recruitment);
                        recruitment.Closed = true;
                        if (ReferenceEquals(_current, recruitment))
                        {
                            _current = null; _activeRecruitment = false;
                            if (result.Status == Modules.MatchmakingRelayClient.Outcome.Maintenance) _maintenanceNotice = true;
                            else _failureNotice = true;
                        }
                    }
                }
            }
            catch
            {
                // Do not log request URLs, capabilities, JSON bodies, or transport exceptions.
                retrySeconds = 60;
                lock (Sync) recruitment.RetryAtMs = Environment.TickCount64 + 60000;
            }
            finally
            {
                lock (Sync)
                {
                    if (!end) recruitment.UpsertPending = false;
                    else
                    {
                        recruitment.DeletePending = false;
                        recruitment.RetryDeleteAtMs = Environment.TickCount64 + retrySeconds * 1000L;
                    }
                }
            }
        }
        private static bool CanSend()

        {

            if (Main.IsAndroid()) return false;

            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return false;

            return true;

        }



        public static void RequestImmediateUpdate(string reason)

        {

            try

            {

                if (!CanSend()) return;

                lock (Sync)

                {

                    if (!_activeRecruitment) return;

                    _forceUpdateRequested = true;

                    _nextUpdateAtMs = 0;

                }

            }

            catch (Exception)

            {

                Logger.Warn("募集処理を完了できませんでした。詳細情報は記録しません。", nameof(DiscordMatchmakingRelayService));

            }

        }



        private static void UpdateRecruitmentButtons(GameStartManager gameStartManager, bool recruiting)

        {

            if (gameStartManager == null) return;

            var statusText = recruiting ? "公開中" : "非公開";
            if (recruiting) lock (Sync)
                if (_current == null || _current.LastSentAtMs == 0) statusText = "募集準備中";



            if (gameStartManager.HostPrivateButton != null)

            {

                gameStartManager.HostPrivateButton.buttonText.DestroyTranslator();

                gameStartManager.HostPrivateButton.buttonText.text = statusText;

            }



            if (gameStartManager.HostPublicButton != null)

            {

                gameStartManager.HostPublicButton.buttonText.DestroyTranslator();

                gameStartManager.HostPublicButton.buttonText.text = statusText;

            }



        }



        public static void RefreshRecruitmentButtons(GameStartManager gameStartManager)

        {

            



            bool recruiting;

            lock (Sync)

                recruiting = _activeRecruitment;



            UpdateRecruitmentButtons(gameStartManager, recruiting);

        }




        private static bool TryCollectLobby(out LobbySnapshot lobby)
        {
            // Unity / options / translation data is captured on the main thread, not in the HTTP task.
            var roomCode = GetCurrentRoomCode();
            var isInGame = (AmongUsClient.Instance != null && AmongUsClient.Instance.IsGameStarted) || GameStates.IsInGame;
            var region = ServerManager.Instance?.CurrentRegion?.Name;
            var map = "Unknown";
            if (Main.NormalOptions != null)
            {
                var id = Main.NormalOptions.MapId;
                if (id < Constants.MapNames.Length) map = Constants.MapNames[id];
            }
            var mode = Options.GameMode?.GetString().RemoveHtmlTags();
            var comment = TownOfHost.Modules.MatchmakingWordManager.GetCurrentWord() ?? "";
            if (comment.Length > TownOfHost.Modules.MatchmakingWordManager.MaxCommentLength)
                comment = comment[..TownOfHost.Modules.MatchmakingWordManager.MaxCommentLength];
            lobby = new LobbySnapshot(
                PlayerControl.LocalPlayer?.Data?.PlayerName ?? "Unknown Host", roomCode,
                isInGame ? "InGame" : (GameStates.IsLobby ? "Lobby" : "Unknown"),
                AmongUsClient.Instance?.allClients?.Count ?? 0, Main.NormalOptions?.MaxPlayers ?? 15,
                isInGame ? CalculateMatchProgressPercent() : 0,
                string.IsNullOrWhiteSpace(region) ? "Unknown" : region,
                string.IsNullOrWhiteSpace(map) ? "Unknown" : map,
                string.IsNullOrWhiteSpace(mode) ? "Unknown" : mode,
                NormalizeShellVersion(Main.PluginShowVersion), comment);
            return !string.IsNullOrWhiteSpace(roomCode);
        }

        private static string NormalizeShellVersion(string version) => (version ?? "").Trim().TrimStart('v', 'V').Trim();



        private static int CalculateMatchProgressPercent()

        {

            try

            {

                var totalTasks = GameData.Instance != null ? GameData.Instance.TotalTasks : 0;

                var completedTasks = GameData.Instance != null ? GameData.Instance.CompletedTasks : 0;

                var taskProgress = totalTasks > 0 ? (double)completedTasks / totalTasks : 0d;



                var totalPlayers = 0;

                var aliveCount = 0;

                var killerAlive = 0;

                var nonKillerAlive = 0;

                foreach (var pc in PlayerCatch.AllPlayerControls)

                {

                    if (pc == null) continue;

                    totalPlayers++;

                    if (!pc.IsAlive()) continue;

                    aliveCount++;

                    if (IsKillerAligned(pc)) killerAlive++;

                    else nonKillerAlive++;

                }

                var deathProgress = totalPlayers > 0 ? (double)(totalPlayers - aliveCount) / totalPlayers : 0d;



                var factionProgress = Math.Clamp((double)killerAlive / Math.Max(nonKillerAlive, 1), 0d, 1d);



                var overall = (taskProgress + deathProgress + factionProgress) / 3d;

                return (int)Math.Clamp(Math.Round(overall * 100d), 0d, 100d);

            }

            catch (Exception)

            {

                Logger.Warn("募集処理を完了できませんでした。詳細情報は記録しません。", nameof(DiscordMatchmakingRelayService));

                return 0;

            }

        }



        private static bool IsKillerAligned(PlayerControl pc)

        {

            return pc.GetCountTypes() switch

            {

                CountTypes.Impostor => true,

                CountTypes.Jackal => true,

                CountTypes.Remotekiller => true,

                CountTypes.GrimReaper => true,

                CountTypes.MilkyWay => true,

                CountTypes.Pavlov => true,

                CountTypes.StandMaster => true,

                CountTypes.Villain => true,

                _ => false,

            };

        }




        private static string BuildPayload(LobbySnapshot lobby)
        {
            return JsonSerializer.Serialize(new
            {
                hostName = LimitText(lobby.HostName, 128), roomCode = lobby.RoomCode,
                state = lobby.State, players = Math.Clamp(lobby.Players, 0, 15),
                maxPlayers = Math.Clamp(lobby.MaxPlayers, 1, 15),
                progressPercent = Math.Clamp(lobby.ProgressPercent, 0, 100),
                region = LimitText(lobby.Region, 128), map = LimitText(lobby.Map, 128),
                gameMode = LimitText(lobby.GameMode, 128), shellVersion = LimitText(lobby.ShellVersion, 32),
                threadComment = LimitText(lobby.ThreadComment, 1000)
            });
        }

        private static string LimitText(string value, int max)
        {
            value ??= "";
            // Prevent control characters and avoid splitting a UTF-16 surrogate pair at the limit.
            var clean = new StringBuilder();
            foreach (var c in value) if (!char.IsControl(c) || c == '\n' || c == '\t') clean.Append(c);
            if (clean.Length <= max) return clean.ToString();
            var length = char.IsHighSurrogate(clean[max - 1]) ? max - 1 : max;
            return clean.ToString(0, length);
        }
        private static string GetCurrentRoomCode()

        {

            try

            {

                return GameCode.IntToGameName(AmongUsClient.Instance.GameId);

            }

            catch

            {

                return "";

            }

        }



    }

}
