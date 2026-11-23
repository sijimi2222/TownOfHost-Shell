using System;

namespace TownOfHost.Modules;

internal static class BugReportInputText
{
    internal static (string Text, int Caret) Format(string committed, string composition, int caret, bool focused)
    {
        committed ??= "";
        if (!focused) return (committed, -1);

        caret = Math.Clamp(caret, 0, committed.Length);
        if (caret > 0 && caret < committed.Length && char.IsSurrogatePair(committed[caret - 1], committed[caret]))
            caret--;

        composition ??= "";
        return (committed.Insert(caret, composition), caret + composition.Length);
    }

    internal static bool CaretVisible(int caret, float elapsed)
        => caret >= 0 && elapsed % 1f < 0.5f;
}

internal sealed class BugReportInputBuffer(int limit, bool multiline)
{
    internal string Text { get; private set; } = "";
    internal int Caret { get; private set; }
    internal string Composition { get; private set; } = "";
    private string lastRecovered = "";
    private string acceptedPreview = "";
    private bool confirmPending;

    internal void ApplyFrame(string input, string composition, bool cancel = false, bool leave = false, bool confirm = false)
    {
        input = RemoveControls(input, false);
        composition ??= "";
        if (composition == acceptedPreview)
            composition = "";
        else
            acceptedPreview = "";

        if (lastRecovered.Length > 0 && input.StartsWith(lastRecovered, StringComparison.Ordinal))
        {
            input = input[lastRecovered.Length..];
            lastRecovered = "";
        }
        else if (composition.Length > 0 || input.Length > 0)
            lastRecovered = "";

        if (cancel)
        {
            Composition = composition;
            confirmPending = false;
            return;
        }

        bool deliveredUnicode = false;
        foreach (var c in input)
            if (c > 127) deliveredUnicode = true;

        var pending = confirmPending && Composition.Length > 0 ? Composition
            : composition.Length > 0 ? composition : Composition;
        if (pending.Length > 0 && (composition.Length == 0 || leave || confirmPending) && !deliveredUnicode)
        {
            // Legacy Input can clear its IME preview without returning the committed Unicode.
            Insert(pending);
            lastRecovered = pending;
            if (pending == composition)
            {
                acceptedPreview = composition;
                composition = "";
            }
            else if (leave && composition.Length > 0)
                Insert(composition);
        }

        if (composition.Length == 0 || leave || deliveredUnicode)
            Insert(input);

        if (deliveredUnicode && composition == input)
        {
            acceptedPreview = composition;
            composition = "";
        }
        Composition = leave ? "" : composition;
        confirmPending = confirm && !deliveredUnicode && Composition.Length > 0;
    }

    internal void Insert(string value)
    {
        value = RemoveControls(value, multiline);
        int count = Math.Min(value.Length, limit - Text.Length);
        if (count <= 0) return;
        if (char.IsHighSurrogate(value[count - 1])) count--;
        Text = Text.Insert(Caret, value[..count]);
        Caret += count;
    }

    internal void Move(int direction)
    {
        Caret = Math.Clamp(Caret + direction, 0, Text.Length);
        if (Caret > 0 && Caret < Text.Length && char.IsLowSurrogate(Text[Caret]))
            Caret += direction;
    }

    internal void MoveToEdge(bool end) => Caret = end ? Text.Length : 0;

    internal void Delete(bool backward)
    {
        int oldCaret = Caret;
        Move(backward ? -1 : 1);
        var start = Math.Min(oldCaret, Caret);
        Text = Text.Remove(start, Math.Abs(Caret - oldCaret));
        Caret = start;
    }

    private static string RemoveControls(string value, bool allowNewline)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var result = new System.Text.StringBuilder(value.Length);
        foreach (var c in value.Replace("\r\n", "\n").Replace('\r', '\n'))
        {
            if (c == '\n' && allowNewline) result.Append(c);
            else if (c is '\n' or '\t' && allowNewline) result.Append(' ');
            else if (!char.IsControl(c)) result.Append(c);
        }
        return result.ToString();
    }
}
