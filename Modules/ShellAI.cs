using System;
using System.Linq;
using System.Text;
using TownOfHost.Roles.Core;

namespace TownOfHost.Modules
{
    // 外部通信を行わない、チャットコマンドへのルールベース案内。
    internal static class ShellAI
    {
        internal enum Intent { Greeting, MyRole, RoleList, CurrentSettings, CommandList, CommandHelp, RoleHelp, Unknown }

        internal readonly struct Detection
        {
            public Intent Intent { get; }
            public CustomRoles? Role { get; }
            public bool Clarify { get; }
            public ShellAICommandHelp.CommandHelpEntry Help { get; }
            public Detection(Intent intent, CustomRoles? role = null, bool clarify = false, ShellAICommandHelp.CommandHelpEntry help = null)
            {
                Intent = intent;
                Role = role;
                Clarify = clarify;
                Help = help;
            }
        }

        public static bool TryParseCommand(string text, out string question)
        {
            question = "";
            var command = (text ?? "").Trim();
            if (HasCommandPrefix(command, "/cmd"))
                command = "/" + command.Substring(4).TrimStart().TrimStart('/');
            if (!HasCommandPrefix(command, "/ai")) return false;
            question = command.Substring(3).Trim();
            return true;
        }

        private static bool HasCommandPrefix(string text, string prefix)
            => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && (text.Length == prefix.Length || char.IsWhiteSpace(text[prefix.Length]));

        internal static string Normalize(string text)
        {
            text = (text ?? "").RemoveHtmlTags().Trim();
            // 不正なUnicode入力でもチャット処理を例外で止めない。
            try { text = text.Normalize(NormalizationForm.FormKC); }
            catch (ArgumentException) { return ""; }
            return string.Concat(text.Where(c => !char.IsWhiteSpace(c)
                    && !char.IsPunctuation(c) && !char.IsControl(c)))
                .ToLowerInvariant().Replace("おしえて", "教えて")
                .Replace("みたい", "見たい").Replace("しりたい", "知りたい");
        }

        private static bool HasAny(string text, params string[] cues)
            => cues.Any(cue => text.Contains(cue, StringComparison.Ordinal));

        internal static Detection Classify(string question)
        {
            if ((question?.Length ?? 0) > 300) return new Detection(Intent.Unknown);
            var text = Normalize(question);
            if (text.Length == 0) return new Detection(Intent.Unknown);

            // 挨拶の次に具体的なコマンド用途を判定。一般案内より個人/陣営などの対象を優先。
            if (text is "こんにちは" or "こん" or "やあ" or "おはよう" or "おはよ"
                or "おはようございます" or "こんばんは" or "よろしく" or "よろしくお願いします"
                or "hello" or "hi") return new Detection(Intent.Greeting);

            var commandHelp = ShellAICommandHelp.Find(text);
            if (commandHelp != null) return new Detection(Intent.CommandHelp, help: commandHelp);

            bool roleTopic = HasAny(text, "役職", "何役");
            bool self = HasAny(text, "自分", "自役職", "私の", "僕の", "俺の", "何役");
            bool check = HasAny(text, "見る", "見たい", "見れる", "確認", "知りたい", "どこ", "何", "教えて");
            // 主語省略の定型だけを補完。「役職教えて」はここに含めない。
            bool implicitSelf = text is "役職確認したい" or "役職を確認したい" or "役職を見る方法"
                or "役職説明見たい" or "役職説明見る方法" or "役職の説明を見る方法" or "役職説明を見る方法"
                or "役職見る方法" or "役職見たい" or "役職を見たい" or "役職確認";
            if (roleTopic && ((self && check) || implicitSelf)) return new Detection(Intent.MyRole);

            if (roleTopic && HasAny(text, "一覧", "全部", "どんな役職がある", "使える役職", "役職の種類", "有効な役職"))
                return new Detection(Intent.RoleList);
            if (text.Contains("設定", StringComparison.Ordinal)
                && HasAny(text, "今", "現在", "部屋", "ゲーム", "見たい", "見る", "確認", "どんな", "教えて", "知りたい"))
                return new Detection(Intent.CurrentSettings);
            if ((text.Contains("コマンド", StringComparison.Ordinal)
                    && HasAny(text, "一覧", "教えて", "何", "使える", "確認", "見たい", "知りたい"))
                || (text.Contains("操作方法", StringComparison.Ordinal) && HasAny(text, "教えて", "知りたい", "確認")))
                return new Detection(Intent.CommandList);

            // 役職名を文末表現から推測せず、既存の翻訳名・内部名を文中で照合する。
            // 長い名前を優先し、派生役職を短い基本役職名と取り違えない。
            var match = Enum.GetValues<CustomRoles>()
                .Where(role => role != CustomRoles.NotAssigned)
                .SelectMany(role => new[] { UtilsRoleText.GetRoleName(role), role.ToString() }
                    .Select(name => new { Role = role, Name = Normalize(name) }))
                .Where(entry => entry.Name.Length > 0 && ContainsRoleName(text, entry.Name))
                .OrderByDescending(entry => entry.Name.Length)
                .FirstOrDefault();
            if (match != null) return new Detection(Intent.RoleHelp, match.Role);

            // 役職名単独の別名・ローマ字は既存の/h rと同じ名前解決を使う。
            if (UtilsRoleInfo.GetRoleByInputName(text, out var resolved, true) && resolved != CustomRoles.NotAssigned)
                return new Detection(Intent.RoleHelp, resolved);
            return new Detection(Intent.Unknown, clarify: text is "役職" or "役職は" or "役職教えて" or "役職を教えて");
        }

        private static bool ContainsRoleName(string question, string name)
        {
            int start = 0;
            while (start <= question.Length - name.Length)
            {
                int index = question.IndexOf(name, start, StringComparison.Ordinal);
                if (index < 0) return false;
                // 英字の短い内部名が別の英単語に偶然含まれた場合は除外する。
                bool asciiName = name.All(c => c is >= 'a' and <= 'z');
                int end = index + name.Length;
                if (!asciiName || ((index == 0 || !IsAsciiLetter(question[index - 1]))
                    && (end == question.Length || !IsAsciiLetter(question[end])))) return true;
                start = index + 1;
            }
            return false;
        }

        private static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z';

        public static string GetReply(string question)
        {
            var result = Classify(question);
            var roleName = result.Role.HasValue ? UtilsRoleText.GetRoleName(result.Role.Value).RemoveHtmlTags() : null;
            // 1質問につき3行。改行・制御文字はログに持ち込まない。
            var logQuestion = string.Concat((question ?? "").Take(300).Select(c => char.IsControl(c) ? ' ' : c));
            Logger.Info($"Question={logQuestion}", "ShellAI");
            Logger.Info($"Intent={result.Intent}", "ShellAI");
            Logger.Info($"Role={roleName ?? "None"}", "ShellAI");
            if (string.IsNullOrWhiteSpace(question)) return "/ai の後に質問を入力してください。";
            if (result.Help != null) return result.Help.Reply;
            if (result.Clarify) return "何について知りたいですか？\n自分の役職は /m、\n役職一覧は /n r で確認できます。非ホストは /cmd n r I のようにカテゴリ指定が必要です。I=インポスター M=マッド C=クルー N=中立 A=属性 G=ゴースト。";
            return result.Intent switch
            {
                Intent.Greeting => "こんにちは！TownOfHost-Shellの案内AIです。",
                Intent.MyRole => "自分の役職は /m で確認できます。",
                Intent.RoleList => "役職一覧は /n r で確認できます。非ホストは /cmd n r I のようにカテゴリ指定が必要です。I=インポスター M=マッド C=クルー N=中立 A=属性 G=ゴースト。",
                Intent.CurrentSettings => "現在の設定は /n で確認できます。",
                Intent.CommandList => "コマンド一覧は /h で確認できます。",
                Intent.RoleHelp => $"{roleName}については /h r {roleName} で確認できます。",
                _ => "その質問にはまだうまく答えられません。\n役職・設定・コマンドについて質問してみてください。"
            };
        }
    }
}
