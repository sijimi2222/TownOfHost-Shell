using System;
using System.Linq;

namespace TownOfHost.Modules
{
    // 書式はChatCommandPatchのホスト/受信側双方を確認。案内のみでコマンドは実行しない。
    internal static class ShellAICommandHelp
    {
        internal sealed class CommandHelpEntry
        {
            public string Id { get; }
            public string[] Topics { get; }
            public string Usage { get; }
            public string Description { get; }
            public string Conditions { get; }
            public CommandHelpEntry(string id, string[] topics, string usage, string description, string conditions)
            {
                Id = id;
                Topics = topics;
                Usage = usage;
                Description = description;
                Conditions = conditions;
            }
            public string Reply => string.Join("\n", new[] { Description, Usage, Conditions }.Where(s => s.Length > 0));
        }

        // 詳細な対象を先に置く。単なる「秘匿」は個人/陣営の選択案内にする。
        private static readonly CommandHelpEntry[] Entries =
        {
            // ChatCommandPatch: /pm (local + OnReceiveChat)。args[1]=色、args.Skip(2)=本文。
            new("PrivateMessage", new[] { "個人", "個チャ", "一人だけ", "ひとりだけ", "特定の人", "pm" },
                "/cmd pm 色 メッセージ（例: /cmd pm 赤 こんにちは）",
                "個人秘匿は相手の色で指定します。相手と自分に表示します。",
                "個人メッセージ設定ON時。非ホストは生存中のみ。"),
            // ChatCommandPatch: /sc・/secretchat、GetHideSendText。死亡者も受信対象。
            new("ImpostorChat", new[] { "インポ", "赤陣営", "sc", "secretchat" },
                "/cmd sc メッセージ（別名: /cmd secretchat メッセージ）",
                "インポスター秘匿です。死亡者にも届きます。",
                "設定ON・ゲーム中・生存中のインポスター系/Egoistが対象。OneWolf・未自覚Amnesiacは不可。追放演出中などは送れない場合があります。"),
            // CoCommandOption.HandleAcoCommand / HandleAclCommand。
            new("AddonCoList", new[] { "属性co一覧", "属性coリスト", "属性co確認", "属性coを確認", "acl" },
                "/cmd acl", "属性CO一覧を確認できます。", "参加者も使用可能。ホスト設定で無効化される場合があります。"),
            new("AddonCo", new[] { "属性co", "アドオンco", "aco" },
                "/cmd aco 属性名", "属性をCOし、全員へ公開します。", "ゲーム中の生存者用。属性COがホスト設定で禁止されていない場合のみ。"),
            // CoCommandOption.HandleColistCommand / ChatCommandPatch /colist・/cl。
            new("CoList", new[] { "co一覧", "coリスト", "co確認", "coを確認", "誰がco", "colist" },
                "/cmd colist（別名: /cmd cl）", "CO一覧を確認できます。", "参加者も使用可能。ホスト設定で無効化される場合があります。"),
            // CoCommandOption.HandleCoCommand: 非ロビー・生存・OptionCommandCo=false。
            new("Co", new[] { "co", "カミングアウト" },
                "/cmd co 役職名（例: /cmd co シェリフ）",
                "指定した役職をCOし、全員へ公開します。",
                "ゲーム中の生存者用。CO禁止設定がOFFの場合のみ。通常役職名、ベント系・占い系に対応。"),
            // ChatCommandPatch /l・/lastresult + UtilsLog.ShowLastResult。
            new("LastResult", new[] { "前回の試合", "前の試合", "試合結果", "前回の結果", "リザルト", "lastresult" },
                "/cmd l（別名: /cmd lastresult）", "前回の試合結果を表示します。", "試合中は使用不可。参加者はホスト設定で制限されます。末尾に m または mo で単色表示。"),
            // ChatCommandPatch /kl・/killlog + UtilsLog.ShowKillLog。
            new("KillLog", new[] { "キルログ", "killlog" },
                "/cmd kl（別名: /cmd killlog）", "キルログを表示します。", "試合中は使用不可。参加者はホスト設定で制限されます。末尾に m または mo で単色表示。"),
            // GetRolesInfoは属性も解決する。/h aはホスト側でもlastimpostor限定なので一般案内には使わない。
            new("AddonHelp", new[] { "属性", "アドオン", "addon" },
                "/cmd h r 属性名（例: /cmd h r ラストインポスター）",
                "属性の説明は役職と同じ検索で確認できます。",
                "参加者も使用可能。ホストのコマンド禁止・設定非公開の影響を受けます。"),
            // /h mはホスト側のみ。OnReceiveChatの/hにはm分岐がない。
            new("ModeHelp", new[] { "モード" },
                "ホスト: /cmd h m モード名（例: /cmd h m tbm）",
                "モード説明はホスト側のみ対応。参加者はホストに確認してください。",
                "別名: h modes。確認済み引数: has / tbm / mm / nge / sbm / im / rmm / Sd（大文字小文字に注意）。"),
            // /miは会議開始情報の再表示であり、投票先の一覧ではない。
            new("VoteCheck", new[] { "投票" }, "",
                "会議の投票先を確認する専用コマンドは確認できませんでした。",
                "/cmd mi は会議開始時の情報を再表示するもので、投票先一覧ではありません。"),
            // ChatCommandPatch OnReceiveChat /mi・/MeeginInfo。
            new("MeetingInfo", new[] { "会議情報", "会議の情報", "会議開始", "会議通知" },
                "/cmd mi（過去の回は /cmd mi 数字）",
                "会議開始時に自分へ届いた情報を再表示します。",
                "引数なしはゲーム中のみ。過去の回は保存情報がある場合のみ。参加者はホスト設定で制限されます。"),
            new("SecretChatChoice", new[] { "秘匿" }, "",
                "個人宛ては /cmd pm 色 メッセージ、陣営宛ては /cmd sc メッセージです。",
                "それぞれ設定・役職などの条件があります。「個人秘匿」「インポスター秘匿」と質問すると詳しく案内します。")
        };

        private static readonly string[] Requests =
        {
            "見る", "見たい", "確認", "教えて", "やり方", "方法", "どう", "使い方", "説明", "能力",
            "コマンド", "送りたい", "送り方", "送る", "話したい", "したい", "一覧", "とは", "って何", "使える"
        };

        internal static CommandHelpEntry Find(string text)
        {
            bool request = Requests.Any(word => text.Contains(word, StringComparison.Ordinal));
            foreach (var entry in Entries)
            {
                bool topic = entry.Topics.Any(word => MatchesTopic(text, word));
                if (!topic || (!request && !entry.Topics.Contains(text))) continue;
                if (entry.Id == "PrivateMessage" && !new[] { "秘匿", "チャット", "個チャ", "メッセージ", "送", "話", "pm" }
                    .Any(word => MatchesTopic(text, word))) continue;
                // 「インポスターの説明」を陣営チャット扱いしない。
                if (entry.Id == "ImpostorChat" && !new[] { "秘匿", "チャット", "送", "話", "sc", "secretchat" }
                    .Any(word => MatchesTopic(text, word))) continue;
                // 実在する属性名の能力質問は従来の役職名照合へ回す。
                if (entry.Id == "AddonHelp" && !new[] { "属性", "アドオン", "addon" }.Contains(text)
                    && !text.StartsWith("属性", StringComparison.Ordinal) && !text.StartsWith("アドオン", StringComparison.Ordinal)) continue;
                return entry;
            }
            return null;
        }

        private static bool MatchesTopic(string text, string word)
        {
            if (!word.All(c => c is >= 'a' and <= 'z')) return text.Contains(word, StringComparison.Ordinal);
            // COをdiscordなどの別英単語から拾わない。
            for (int start = 0; start <= text.Length - word.Length; start++)
            {
                if (string.CompareOrdinal(text, start, word, 0, word.Length) != 0) continue;
                int end = start + word.Length;
                if ((start == 0 || !IsAsciiLetter(text[start - 1])) && (end == text.Length || !IsAsciiLetter(text[end]))) return true;
            }
            return false;
        }
        private static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z';
    }
}
