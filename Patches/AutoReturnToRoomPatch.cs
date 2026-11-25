using HarmonyLib;
using TMPro;
using UnityEngine;
using static TownOfHost.Translator;

namespace TownOfHost.Patches
{
    // 試合終了後、自動で部屋(ロビー)に戻る機能。
    // EKN(EndKnot, GPL v3)の AutoPlayAgain(EndGameManagerPatch)を参考にした作り:
    //   結果画面のボタン表示(EndGameManager.ShowButtons)の後、画面上にカウントダウンを表示し、
    //   0秒になったら結果画面自身が持つ EndGameNavigation(EndGameManager.Navigation)の NextGame() を呼ぶ。
    //   NextGame() が呼ばれたら(自動・手動どちらでも)カウントダウン表示を片付ける。
    // 設定は従来どおり「自動機能」内の「試合終了後自動で部屋に戻る」(OptionAutoReturnRoom)と、
    // その子の「GMの場合のみ自動で部屋に戻る」を使う。
    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.ShowButtons))]
    public static class AutoReturnToRoomPatch
    {
        /// <summary>部屋に戻るまでの秒数(EKN の既定値と同じ)</summary>
        public const int CountdownSeconds = 10;

        private static GameObject countdownText;
        private static EndGameManager scheduledOwner;
        private static uint scheduleSequence;

        public static void Postfix(EndGameManager __instance)
        {
            var client = AmongUsClient.Instance;
            if (client == null || !client.AmHost || !__instance) return;

            // 定期立て直し(N試合ごと)が今回の試合で予約されている場合、そちらを優先し
            // 通常の「同じ部屋でもう一度」は行わない
            if (PeriodicAutoRehostPatch.IsScheduledForThisGame()) return;

            // 自動戻り設定がOFFなら終了
            if (!Options.OptionAutoReturnRoom.GetBool()) return;
            if (Options.OptionAutoReturnRoomGM.GetBool() && !Options.EnableGM.GetBool()) return;

            // 同じ結果画面で ShowButtons が複数回呼ばれても、カウントダウンは1つだけ
            if (scheduledOwner == __instance) return;

            Cleanup();
            scheduledOwner = __instance;
            uint sequence = unchecked(++scheduleSequence);
            int gameId = client.GameId;
            Logger.Info($"{CountdownSeconds}秒後に自動で部屋に戻ります", "AutoReturnToRoom");

            _ = new LateTask(() => Tick(__instance, client, gameId, sequence, CountdownSeconds), 0.5f, "AutoReturnToRoom", true);
        }

        private static void Tick(EndGameManager manager, AmongUsClient scheduledClient, int gameId, uint sequence, int seconds)
        {
            if (sequence != scheduleSequence) return; // 片付け済み・別の予約に置き換わった

            // 結果画面がもう無い、ホストでなくなった、別の試合になった等の場合は中止
            var client = AmongUsClient.Instance;
            if (!manager || !manager.gameObject.activeInHierarchy || client == null || client != scheduledClient || !client.AmHost
                || client.GameId != gameId || client.GameState != AmongUsClient.GameStates.Ended)
            {
                Cleanup();
                return;
            }

            var navigation = manager.Navigation;
            if (navigation == null)
            {
                Cleanup();
                return;
            }

            ShowCountdown(seconds);

            if (seconds > 0)
            {
                _ = new LateTask(() => Tick(manager, scheduledClient, gameId, sequence, seconds - 1), 1f, "AutoReturnToRoom", true);
                return;
            }

            Cleanup();
            try
            {
                navigation.NextGame();
            }
            catch (System.Exception ex)
            {
                // NextGame() はバニラ側の実装のため、内部で失敗した場合にこちらから安全に復旧させる手段が無い。
                // 部屋ごと切断させるのは参加者全員に影響するため行わず、失敗をログに残すだけにする。
                Logger.Error($"EndGameNavigation.NextGame()が失敗しました: {ex}", "AutoReturnToRoom");
            }
        }

        private static void ShowCountdown(int seconds)
        {
            if (countdownText == null)
            {
                countdownText = new GameObject("AutoReturnRoomCountdown");
                countdownText.transform.position = new Vector3(0f, 2.5f, 10f);
                var tmp = countdownText.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 3f;
            }
            var text = countdownText.GetComponent<TextMeshPro>();
            if (text != null) text.text = string.Format(GetString("AutoReturnRoomCountdown"), seconds);
        }

        /// <summary>カウントダウンを止め、表示を消す</summary>
        public static void Cleanup()
        {
            unchecked { scheduleSequence++; }
            scheduledOwner = null;
            if (countdownText == null) return;
            UnityEngine.Object.Destroy(countdownText);
            countdownText = null;
        }
    }

    // 手動で「もう一度遊ぶ」が押された場合も、カウントダウン表示を片付ける(EKN と同じ)
    [HarmonyPatch(typeof(EndGameNavigation), nameof(EndGameNavigation.NextGame))]
    internal static class AutoReturnToRoomNextGamePatch
    {
        public static void Postfix() => AutoReturnToRoomPatch.Cleanup();
    }
}
