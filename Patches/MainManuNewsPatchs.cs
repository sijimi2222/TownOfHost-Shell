//TOH_Yを参考にさせて貰いました ありがとうございます
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data.Player;
using Assets.InnerNet;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Newtonsoft.Json.Linq;
using TownOfHost;
using UnityEngine.Networking;

[HarmonyPatch]
public class ModNewsHistory
{
    public static List<ModNews> AllModNews = new();
    public static List<ModNews> JsonAndAllModNews = new();
    public static void Init()
    {
        {
            //リンクはこうやるらしい。<nobr><link=\"URL\">Text</nobr></link>
            /*　　テンプレート
            {
                var news = new ModNews
                {
                    Number = 100002,
                    Title = "text",
                    SubTitle = "<color=#FF9631>Town Of host-shell v3.23.11.39</color>",
                    ShortTitle = "<color=#FF9631>●TOH-Shell v3.23.11.39</color>",
                    Text = "text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text\n"
                    + "・text"
                    ,
                    Date = "2026-4-20T00:00:00Z"
                };
                AllModNews.Add(news);
            }*/
            {
                var news = new ModNews
                {
                    Number = 100001,
                    Title = "TOH-Shell v1.0.0 リリース！",
                    SubTitle = "<color=#b85fff>TownOfHost-Shell v1.0.0</color>",
                    ShortTitle = "<color=#b85fff>●TOH-Shell v1.0.0</color>",
                    Text = "バグ大量だけどとりあえずリリースしたぜ！",
                    Date = "2026-09-03"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100002,
                    Title = "TOH-Shell v1.1.0 リリース！",
                    SubTitle = "<color=#b85fff>TownOfHost-Shell v1.1.0</color>",
                    ShortTitle = "<color=#b85fff>●TOH-Shell v1.1.0</color>",
                    Text = "新役職「Tiger」を追加！\n\n"
                        + "捕食モード中は移動速度が上昇し、\n"
                        + "キルクールが即座に解除されます。\n\n"
                        + "その間にキルすると死体を残さず倒せますが、\n"
                        + "時間内にキルできなかった場合は自滅します。\n\n"
                        + "その他、Tiger関連の修正や細かい不具合修正を行いました！",
                    Date = "2026-09-24"
                };
                AllModNews.Add(news);
            }
            {
                var news = new ModNews
                {
                    Number = 100003,
                    Title = "TOH-Shell v2.0.0 リリース！",
                    SubTitle = "<color=#b85fff>TownOfHost-Shell v2.0.0</color>",
                    ShortTitle = "<color=#b85fff>●TOH-Shell v2.0.0</color>",
                    Text = "TOH-Shell v2.0.0をリリースしました！\n"
                        + "\n"
                        + "災害モードの完成、AIモード切り替え、Among Us最新版への対応など、大きな変更を行っています。\n"
                        + "\n"
                        + "【Natural Disasters】\n"
                        + "・災害モードを大幅更新し、各マップの災害処理を調整\n"
                        + "・生存時間を記録し、死亡後は時間を固定\n"
                        + "・ゲーム終了時に生存時間を確認できるよう改善\n"
                        + "・「ゲームを終了しない」設定で、最後の1人や全滅後もホストが終了するまで続行可能\n"
                        + "※ Thunderstormは現在調整中のため無効化しています。\n"
                        + "\n"
                        + "【AIモード】\n"
                        + "「AIモードを有効にする」設定を追加しました。\n"
                        + "ON：従来のAI機能を使用でき、会議中にAIコマンド案内を表示します。\n"
                        + "OFF：AI機能とAIリクエスト送信を停止し、会議中は元のゲッサーコマンド案内へ戻ります。\n"
                        + "\n"
                        + "【マッチメイキング】\n"
                        + "Shell独自のDiscordマッチメイキング機能を更新しました。\n"
                        + "部屋情報として人数、リージョン、マップ、ゲームモード、Shellバージョン、ロビー／試合中の状態を表示します。\n"
                        + "募集終了時はDiscord上の募集メッセージも削除されます。\n"
                        + "\n"
                        + "【Among Us最新版対応】\n"
                        + "Among Us 2026.9.29アップデートへ対応しました。\n"
                        + "・x64環境、GameOptions V12へ対応\n"
                        + "・hamo β 4.00.32.00の互換性変更を統合\n"
                        + "・SpiritGuide関連へ対応\n"
                        + "\n"
                        + "【役職整理】\n"
                        + "SnowmanとAndroidを現在の役職一覧からリストラしました。\n"
                        + "役職コードは残していますが、通常の設定・役職一覧・抽選対象には登録されません。\n"
                        + "\n"
                        + "【その他】\n"
                        + "・会議関連処理の改善\n"
                        + "・Shell独自機能の互換性調整\n"
                        + "・最新Among Us環境向けの内部修正\n"
                        + "・各種不具合修正\n"
                        + "\n"
                        + "不具合を見つけた場合は報告をお願いします。",
                    Date = "2026-10-02"
                };
                AllModNews.Add(news);
            }
            AnnouncementPopUp.UpdateState = AnnouncementPopUp.AnnounceState.NotStarted;
        }
    }
    //ここもTownOfHost_Y様を参考に..!
    public const string ModNewsURL = "";
    static bool downloaded = false;

    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start)), HarmonyPostfix]
    public static void StartPostfix(MainMenuManager __instance)
    {
        static IEnumerator FetchModNews()
        {
            if (downloaded)
            {
                yield break;
            }
            // ===== 空URL/例外への対策 =====
            // ModNewsURLが空文字のままだと、UnityWebRequest.Get("")や後続のJSON解析が
            // 例外を投げてMainMenuManager.Startの実行タイミングに悪影響を与える可能性がある
            // (実際にログでMissingMethodExceptionが確認されている)。
            // URLが未設定の場合はそもそも何もしないようにする。
            if (string.IsNullOrWhiteSpace(ModNewsURL))
            {
                yield break;
            }
            downloaded = true;
            var request = UnityWebRequest.Get(ModNewsURL);
            yield return request.SendWebRequest();
            if (request.isNetworkError || request.isHttpError)
            {
                downloaded = false;
                TownOfHost.Logger.Info("ModNews Error Fetch:" + request.responseCode.ToString(), "ModNews");
                yield break;
            }
            JObject json;
            try
            {
                json = JObject.Parse(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                // JSON解析に失敗しても、ここで完結させて他の処理に影響を与えないようにする。
                downloaded = false;
                TownOfHost.Logger.Info("ModNews Parse Error:" + e.Message, "ModNews");
                yield break;
            }
            for (var news = json["News"].First; news != null; news = news.Next)
            {
                JsonModNews n = new(
                    int.Parse(news["Number"].ToString()), news["Title"]?.ToString(), news["Subtitle"]?.ToString(), news["Short"]?.ToString(),
                    news["Body"]?.ToString(), news["Date"]?.ToString());
            }
        }
        __instance.StartCoroutine(FetchModNews().WrapToIl2Cpp());
    }

    private static DateTime ParseDateSafe(string date)
    {
        if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var result))
            return result;
        return DateTime.MinValue;
    }

    [HarmonyPatch(typeof(PlayerAnnouncementData), nameof(PlayerAnnouncementData.SetAnnouncements)), HarmonyPrefix]
    public static bool SetModAnnouncements(PlayerAnnouncementData __instance, [HarmonyArgument(0)] ref Il2CppReferenceArray<Announcement> aRange)
    {
        if (AllModNews.Count < 1)
        {
            Init();
            AllModNews.Do(n => JsonAndAllModNews.Add(n));
            JsonAndAllModNews.Sort((a1, a2) => { return DateTime.Compare(ParseDateSafe(a2.Date), ParseDateSafe(a1.Date)); });
        }

        List<Announcement> FinalAllNews = new();
        JsonAndAllModNews.Do(n => FinalAllNews.Add(n.ToAnnouncement()));
        foreach (var news in aRange)
        {
            if (!JsonAndAllModNews.Any(x => x.Number == news.Number))
                FinalAllNews.Add(news);
        }
        FinalAllNews.Sort((a1, a2) => { return DateTime.Compare(ParseDateSafe(a2.Date), ParseDateSafe(a1.Date)); });

        aRange = new(FinalAllNews.Count);
        for (int i = 0; i < FinalAllNews.Count; i++)
            aRange[i] = FinalAllNews[i];

        return true;
    }
}