using System;using System.Linq;using System.Collections.Generic;using System.Text.RegularExpressions;using static TownOfHost.Translator;namespace TownOfHost {public static partial class Utils {static readonly Regex UnderlineRegex=new(@"<u>(.*?)</u>", RegexOptions.Singleline|RegexOptions.Compiled);public static void SendMessage(string text, byte sendTo = byte.MaxValue, string title = "", bool checkl = true, bool isTowSend = false, bool setsize = false, bool useChatBody = false)

        {

            if (!AmongUsClient.Instance.AmHost) return;

            if (text.RemoveHtmlTags() == "") return;

            // NonTitleは見出しを完全に空にする内部指定。歓迎文などで既定の

            // 「システムメッセージ」を出したくない場合にも、通常時・制限時共通で使える。

            if (title == "NonTitle") title = "";

            else if (title == "") title = $"<{Main.ModColor}>" + GetString($"DefaultSystemMessageTitle") + "</color>";



            var towsend = "";

            if (checkl && text.Length > 300)

            {

                var sendtext = "";

                var alltext = text.Split("\n");

                (string size, string color, string hi, string b) tag = ("", "", "", "");

                var sizeTags = new Stack<string>();

                var colorTags = new Stack<string>();

                var lineHeightTags = new Stack<string>();

                var boldTags = new Stack<string>();

                var nextLineIndex = 0;

                var lineCount = 0;

                while (nextLineIndex < alltext.Length)

                {

                    var line = alltext[nextLineIndex];

                    var additionalLength = line.Length + 1;

                    if (lineCount > 0 && (sendtext.Length + additionalLength > 280 || lineCount >= 10))

                        break;



                    sendtext += $"{line}\n";

                    lineCount++;

                    nextLineIndex++;



                    var tagtex = line.RemoveText().RemoveDeltext("　").RemoveDeltext(" ").Split("<");

                    foreach (var tagtext in tagtex)

                    {

                        if (tagtext == "") continue;

                        switch (tagtext.Substring(0, 1))

                        {

                            case "s":

                                if (!tagtext.Contains("sub"))

                                {

                                    tag.size = $"<{tagtext.Split(">")[0]}>";

                                    sizeTags.Push(tag.size);

                                }

                                break;

                            case "c":

                            case "#":

                                tag.color = $"<{tagtext.Split(">")[0]}>";

                                colorTags.Push(tag.color);

                                break;

                            case "l":

                                tag.hi = $"<{tagtext.Split(">")[0]}>";

                                lineHeightTags.Push(tag.hi);

                                break;

                            case "b":

                                tag.b = $"<{tagtext.Split(">")[0]}>";

                                boldTags.Push(tag.b);

                                break;

                            case "/":

                                switch (tagtext.Substring(1, 1))

                                {

                                    case "s":

                                        if (!tagtext.Contains("sub"))

                                        {

                                            if (sizeTags.Count > 0) sizeTags.Pop();

                                            tag.size = sizeTags.Count > 0 ? sizeTags.Peek() : "";

                                        }

                                        break;

                                    case "c":

                                        if (colorTags.Count > 0) colorTags.Pop();

                                        tag.color = colorTags.Count > 0 ? colorTags.Peek() : "";

                                        break;

                                    case "l":

                                        if (lineHeightTags.Count > 0) lineHeightTags.Pop();

                                        tag.hi = lineHeightTags.Count > 0 ? lineHeightTags.Peek() : "";

                                        break;

                                    case "b":

                                        if (boldTags.Count > 0) boldTags.Pop();

                                        tag.b = boldTags.Count > 0 ? boldTags.Peek() : "";

                                        break;

                                }

                                break;

                        }

                    }

                }

                var send = string.Join("\n", alltext.Skip(nextLineIndex));

                if (tag.color == "<#ffffff>") tag.color = "";

                towsend = $"{tag.b}{tag.hi}{tag.color}{tag.size}{send}";

                text = sendtext;

            }

            if (text.RemoveHtmlTags().StartsWith("\n") || text.RemoveHtmlTags().StartsWith("\r"))

            {

                var alltext = text.Split("\n");

                bool first = true;

                text = "";

                alltext.Do(t =>

                {

                    if (first)

                    {

                        text += t.RemoveDeltext("\n").RemoveDeltext("\r");

                        first = false;

                        return;

                    }

                    text += t + "\n";

                });

            }

            var fir = "<align=\"left\">";

            text = text.RemoveDeltext("color=#", "#").RemoveDeltext("FF>", ">");

            title = title.RemoveDeltext("color=#", "#").RemoveDeltext("FF>", ">");

            if (setsize is false) text = $"<size=70%>{(IsRestriction() ? "<#ffffff>" : "")}{text}";

            if (IsRestriction() && !useChatBody)

            {

                var sendtext = text;

                if (sendtext.RemoveHtmlTags().RemoveDeltext("\n") != "")

                {

                    if (!VersionInfoManager.GetCustomFlag(1))

                        sendtext = UnderlineRegex.Replace(text, m => $"<u><line-height=1.5em>{m.Groups[1].Value}</line-height></u>");

                    if (isTowSend && title == "") Main.MessagesToSend.Add(($" ", sendTo, $"{fir}{sendtext}"));

                    else Main.MessagesToSend.Add(($" ", sendTo, $"{fir}{title}\n{sendtext}"));

                }

            }

            else

                Main.MessagesToSend.Add(($"{fir}{text}", sendTo, $"{fir}{title}"));

            if (towsend is not "")

            {

                SendMessage(towsend, sendTo, IsRestriction() && !useChatBody ? "NonTitle" : title, true, isTowSend: true, setsize: useChatBody && setsize, useChatBody: useChatBody);

            }

        }}}