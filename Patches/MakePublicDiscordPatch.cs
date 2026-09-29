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

        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(3.0) };

        private static readonly object Sync = new();




        // Session objects keep late responses from modifying a newer recruitment.
        private sealed class Recruitment
        {
            public LobbySnapshot Lobby = new();
            public string SessionId = "";
            public string MessageId = "";
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
        private static readonly string PersistedStateFilePath = Path.Combine(Main.BaseDirectory, "discord_matchmaking_active.txt");
        private static bool _startupStaleCheckDone;
        private static bool RelayConfigured => !string.IsNullOrWhiteSpace(Main.MatchmakingRelayUrl)
            && !Main.MatchmakingRelayUrl.Equals("none", StringComparison.OrdinalIgnoreCase);


        public static void ToggleRecruitment(GameStartManager gameStartManager)

        {

            if (string.IsNullOrWhiteSpace(Main.MatchmakingRelayUrl) || Main.MatchmakingRelayUrl.Equals("none", StringComparison.OrdinalIgnoreCase))

            {

                var message = "現在マッチメイキング機能を使うことができません。";

                Logger.Info(message, nameof(DiscordMatchmakingRelayService));

                Logger.seeingame(message);

                return;

            }

            if (!CanSend()) return;



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
                RunStartupStaleCheckIfNeeded();
                if (!TryCollectLobby(out var lobby)) return false;
                lock (Sync)
                {
                    if (_activeRecruitment) return true;
                    _current = new Recruitment { Lobby = lobby, SessionId = Guid.NewGuid().ToString("N") };
                    Recruitments.Add(_current);
                    _activeRecruitment = true;
                    _forceUpdateRequested = false;
                    _nextUpdateAtMs = Environment.TickCount64 + UpdateIntervalMs;
                    // Save before the first request: even a lost HTTP response can leave a remote post.
                    PersistActiveState();
                    SendUpsert(_current, lobby, "MakePublic");
                }
                return true;
            }
            catch (Exception e)
            {
                Logger.Exception(e, nameof(DiscordMatchmakingRelayService));
                return false;
            }
        }

        public static void Tick()
        {
            try
            {
                if (Main.IsAndroid() || !RelayConfigured) return;
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
                    var snapshot = JsonSerializer.Serialize(lobby);
                    if (_current.UpsertPending || snapshot == _current.LastSnapshot) return;
                    SendUpsert(_current, lobby, "Tick");
                }
            }
            catch (Exception e)
            {
                Logger.Exception(e, nameof(DiscordMatchmakingRelayService));
            }
        }

        public static void TryDelete(string reason)
        {
            if (Main.IsAndroid() || !RelayConfigured) return;
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

        private static void RunStartupStaleCheckIfNeeded()
        {
            lock (Sync)
            {
                if (_startupStaleCheckDone || Main.IsAndroid() || !RelayConfigured) return;
                _startupStaleCheckDone = true;
                try
                {
                    if (!File.Exists(PersistedStateFilePath)) return;
                    var saved = File.ReadAllText(PersistedStateFilePath);
                    if (saved.TrimStart().StartsWith("[", StringComparison.Ordinal))
                    {
                        using var doc = JsonDocument.Parse(saved);
                        foreach (var entry in doc.RootElement.EnumerateArray())
                            RestoreStaleRecruitment(entry.GetProperty("roomCode").GetString(),
                                entry.GetProperty("messageId").GetString(), entry.GetProperty("sessionId").GetString());
                    }
                    else
                    {
                        // Compatibility with the old roomCode/messageId two-line save file.
                        var lines = saved.Replace("\r", "").Split('\n');
                        RestoreStaleRecruitment(lines.Length > 0 ? lines[0].Trim() : "",
                            lines.Length > 1 ? lines[1].Trim() : "", lines.Length > 2 ? lines[2].Trim() : "");
                    }
                    foreach (var recruitment in Recruitments)
                        if (recruitment.Closed) QueueDelete(recruitment);
                }
                catch (Exception e)
                {
                    Logger.Exception(e, nameof(DiscordMatchmakingRelayService));
                }
            }
        }

        private static void RestoreStaleRecruitment(string roomCode, string messageId, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(roomCode)
                || (string.IsNullOrWhiteSpace(messageId) && string.IsNullOrWhiteSpace(sessionId))) return;
            Recruitments.Add(new Recruitment
            {
                Lobby = new LobbySnapshot(RoomCode: roomCode), MessageId = messageId ?? "",
                SessionId = sessionId ?? "", Closed = true, DeleteReason = "StaleOnStartup"
            });
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
            try
            {
                if (!RelayConfigured) return;
                string messageId;
                lock (Sync) messageId = recruitment.MessageId; // Read after the preceding response.
                using var req = new HttpRequestMessage(HttpMethod.Post, Main.MatchmakingRelayUrl);
                var secret = Main.MatchmakingRelaySecret;
                if (!string.IsNullOrWhiteSpace(secret) && !secret.Equals("none", StringComparison.OrdinalIgnoreCase))
                    req.Headers.TryAddWithoutValidation("X-Relay-Secret", secret);
                req.Content = new StringContent(BuildPayload(action, lobby, messageId, recruitment.SessionId, reason),
                    Encoding.UTF8, "application/json");
                Logger.Info($"Relay send: action={action}, room={lobby.RoomCode}, session={recruitment.SessionId}, reason={reason}", nameof(DiscordMatchmakingRelayService));
                using var res = await Client.SendAsync(req).ConfigureAwait(false);
                var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!res.IsSuccessStatusCode)
                {
                    Logger.Warn($"Relay failed: HTTP {(int)res.StatusCode}; retry on next poll", nameof(DiscordMatchmakingRelayService));
                    return;
                }
                if (action == "upsert")
                {
                    var newMessageId = TryReadJsonString(body, "messageId");
                    if (string.IsNullOrWhiteSpace(newMessageId)
                        || (!string.IsNullOrEmpty(messageId) && messageId != newMessageId))
                    {
                        Logger.Warn("Relay returned missing/invalid/different messageId; retry on next poll", nameof(DiscordMatchmakingRelayService));
                        return;
                    }
                    lock (Sync)
                    {
                        recruitment.MessageId = newMessageId;
                        recruitment.LastSnapshot = JsonSerializer.Serialize(lobby);
                        PersistActiveState();
                    }
                }
                else
                {
                    // Legacy delete endpoints may return an empty 2xx body.
                    // Reject malformed JSON rather than discarding recovery data.
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        using var response = JsonDocument.Parse(body);
                    }
                    lock (Sync)
                    {
                        Recruitments.Remove(recruitment);
                        PersistActiveState();
                    }
                }
                Logger.Info($"Relay success: action={action}, room={lobby.RoomCode}, session={recruitment.SessionId}", nameof(DiscordMatchmakingRelayService));
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, nameof(DiscordMatchmakingRelayService));
            }
            finally
            {
                lock (Sync)
                {
                    if (action == "upsert") recruitment.UpsertPending = false;
                    else
                    {
                        recruitment.DeletePending = false;
                        recruitment.RetryDeleteAtMs = Environment.TickCount64 + UpdateIntervalMs;
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

            catch (Exception e)

            {

                Logger.Exception(e, nameof(DiscordMatchmakingRelayService));

            }

        }



        private static void UpdateRecruitmentButtons(GameStartManager gameStartManager, bool recruiting)

        {

            if (gameStartManager == null) return;

            var statusText = recruiting ? "公開中" : "非公開";



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

            RunStartupStaleCheckIfNeeded();



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

            catch (Exception e)

            {

                Logger.Exception(e, nameof(DiscordMatchmakingRelayService));

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




        private static string BuildPayload(string action, LobbySnapshot lobby, string messageId, string sessionId, string reason)
        {
            return JsonSerializer.Serialize(new
            {
                action,
                hostName = lobby.HostName,
                roomCode = lobby.RoomCode,
                state = lobby.State,
                stateLabel = GetStateLabel(lobby.State),
                players = lobby.Players,
                maxPlayers = lobby.MaxPlayers,
                progressPercent = lobby.ProgressPercent,
                content = BuildRecruitmentContent(lobby),
                messageId,
                reason,
                threadRequested = action == "upsert" && string.IsNullOrWhiteSpace(messageId) && !string.IsNullOrWhiteSpace(lobby.ThreadComment),
                threadComment = lobby.ThreadComment,
                mod = Main.ModName,
                modVersion = Main.PluginVersion,
                forkId = Main.ForkId,
                sentAtUtc = DateTime.UtcNow.ToString("o"),
                region = lobby.Region,
                map = lobby.Map,
                gameMode = lobby.GameMode,
                shellVersion = string.IsNullOrWhiteSpace(lobby.ShellVersion) ? NormalizeShellVersion(Main.PluginShowVersion) : lobby.ShellVersion,
                internalVersion = Main.PluginVersion,
                sessionId
            });
        }

        private static string BuildRecruitmentContent(LobbySnapshot lobby)
        {
            if (lobby.State == "Closed") return "🔴 募集終了";
            var status = lobby.State switch
            {
                "Lobby" => "🏠 状態 ロビー",
                "InGame" => "⚔️ 状態 試合中",
                _ => "🏠 状態 不明"
            };
            return $"🟢 募集中\n\n{lobby.RoomCode}\n{lobby.Players} / {lobby.MaxPlayers}\n\n"
                + $"🌏 リージョン {lobby.Region}\n🗺 マップ {lobby.Map}\n🎮 ステージ {lobby.GameMode}\n"
                + $"🔧 バージョン v{lobby.ShellVersion}\n{status}";
        }



        private static string GetStateLabel(string state)

        {

            if (string.IsNullOrWhiteSpace(state)) return "Unknown";



            return state switch

            {

                "Lobby" => "Lobby",

                "InGame" => "In Game",

                "Closed" => "Closed",

                _ => state

            };

        }



        private static string TryReadJsonString(string json, string propName)

        {

            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propName)) return "";

            try

            {

                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty(propName, out var elem)) return "";

                return elem.ValueKind == JsonValueKind.String ? elem.GetString() ?? "" : "";

            }

            catch

            {

                return "";

            }

        }




        // Called under Sync. Keep failed old deletes as well as the new current session;
        // a delayed old response must never erase the new session's recovery information.
        private static void PersistActiveState()
        {
            try
            {
                if (Recruitments.Count == 0)
                {
                    if (File.Exists(PersistedStateFilePath)) File.Delete(PersistedStateFilePath);
                    return;
                }
                var entries = new List<object>(Recruitments.Count);
                foreach (var recruitment in Recruitments)
                    entries.Add(new { roomCode = recruitment.Lobby.RoomCode, messageId = recruitment.MessageId, sessionId = recruitment.SessionId });
                Directory.CreateDirectory(Main.BaseDirectory);
                var temporaryPath = PersistedStateFilePath + ".tmp";
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries));
                File.Move(temporaryPath, PersistedStateFilePath, true);
            }
            catch (Exception e)
            {
                Logger.Exception(e, nameof(DiscordMatchmakingRelayService));
            }
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
