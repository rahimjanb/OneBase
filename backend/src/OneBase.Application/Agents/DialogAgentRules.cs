using System.Globalization;
using System.Text.RegularExpressions;
using OneBase.Domain.AI;

namespace OneBase.Application.Agents;

/// <summary>
/// Правила ИИ-агентов отделов без модели: ограничения ответа (запрещённые фразы, длина, без разметки), «сразу к человеку», деление ответа на
/// короткие сообщения, рабочее время.
/// </summary>
public static partial class DialogAgentRules
{
    public static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    public static readonly IReadOnlyList<string> Tones = ["friendly", "neutral", "formal"];
    public static readonly IReadOnlyList<string> Languages = ["auto", "ru", "uz"];
    public static readonly IReadOnlyList<string> Lengths = ["short", "normal", "detailed"];

    /// <summary>Сообщений в одном ответе — не больше (люди пишут 1–3 коротких).</summary>
    public const int MaxMessages = 3;

    public const int MinReplyChars = 100;
    public const int MaxReplyCharsLimit = 3000;

    /// <summary>Непустые строки списка (запрещённые фразы, вопросы), без повторов.</summary>
    public static IReadOnlyList<string> Lines(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split('\n').Select(l => l.Trim().TrimStart('-', '•', '*').Trim()).Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Первая запрещённая фраза, которая есть в ответе (без учёта регистра и ё/е); null — нет.</summary>
    public static string? ForbiddenIn(string reply, string? phrases)
    {
        var text = Fold(reply);
        return Lines(phrases).FirstOrDefault(p => Fold(p) is { Length: > 0 } f && text.Contains(f, StringComparison.Ordinal));
    }

    /// <summary>Фраза «сразу к человеку» в сообщении собеседника; null — нет.</summary>
    public static string? HandoffIn(string message, string? phrases) => ForbiddenIn(message, phrases);

    private static string Fold(string text) => text.ToLowerInvariant().Replace('ё', 'е').Trim();

    /// <summary>Текст для Telegram без разметки: модель иногда пишет **жирный**, # заголовки и ``` — в обычном сообщении это мусор.</summary>
    public static string Plain(string text)
    {
        var t = text.Replace("\r\n", "\n").Replace("```", string.Empty).Replace("**", string.Empty).Replace("__", string.Empty);
        t = HeadingPattern().Replace(t, string.Empty);
        t = BlankLinesPattern().Replace(t, "\n\n");
        return t.Trim();
    }

    /// <summary>Обрезать до max символов по границе предложения (или слова), без оборванного слова.</summary>
    public static string Clip(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        var cut = text[..max];
        var sentence = cut.LastIndexOfAny(['.', '!', '?', '\n']);
        if (sentence >= max / 2)
        {
            return cut[..(sentence + 1)].Trim();
        }

        var space = cut.LastIndexOf(' ');
        return (space > 0 ? cut[..space] : cut).TrimEnd(',', ';', ':', ' ') + "…";
    }

    /// <summary>
    /// Сообщения ответа: модель дала несколько — до трёх; одно с абзацами при «несколько коротких сообщений» — по абзацам (до трёх). Всё вместе —
    /// не длиннее maxChars. Без деления — одним сообщением.
    /// </summary>
    public static IReadOnlyList<string> Parts(IEnumerable<string> messages, bool split, int maxChars)
    {
        var list = messages.Select(Plain).Where(m => m.Length > 0).ToList();
        if (list.Count == 0)
        {
            return [];
        }

        if (!split)
        {
            return [Clip(string.Join("\n\n", list), maxChars)];
        }

        if (list.Count == 1)
        {
            list = list[0].Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        if (list.Count > MaxMessages)
        {
            list = [.. list.Take(MaxMessages - 1), string.Join("\n\n", list.Skip(MaxMessages - 1))];
        }

        var parts = new List<string>();
        var left = maxChars;
        foreach (var part in list)
        {
            if (left < 20)
            {
                break;
            }

            var clipped = Clip(part, left);
            parts.Add(clipped);
            left -= clipped.Length;
        }

        return parts;
    }

    /// <summary>Рабочие дни агента (1 — пн … 7 — вс).</summary>
    public static IReadOnlySet<int> Days(string? days) =>
        (days ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(d => int.TryParse(d, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0).Where(n => n is >= 1 and <= 7).ToHashSet();

    private static int IsoDay(DateTimeOffset local) => local.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek;

    /// <summary>Рабочее ли время агента в этот момент (Ташкент). Без часов — круглосуточно (с учётом дней).</summary>
    public static bool InWork(DialogAgent agent, DateTimeOffset at)
    {
        var local = at.ToOffset(Tashkent);
        var days = Days(agent.WorkDays);
        if (days.Count > 0 && !days.Contains(IsoDay(local)))
        {
            return false;
        }

        if (agent.WorkFrom is not { } from || agent.WorkTo is not { } to)
        {
            return true;
        }

        var time = TimeOnly.FromDateTime(local.DateTime);
        return from <= to ? time >= from && time < to : time >= from || time < to;
    }

    [GeneratedRegex(@"(?m)^\s{0,3}#{1,6}\s+")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLinesPattern();
}
