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