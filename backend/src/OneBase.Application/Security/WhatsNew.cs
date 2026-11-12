using System.Text.RegularExpressions;

namespace OneBase.Application.Security;

/// <summary>
/// Окно «Что нового» после обновления: версия релиза — дата «2026-11-13» (содержимое окна — в интерфейсе, frontend/src/lib/whats-new.ts).
/// Сервер хранит у пользователя последнюю закрытую версию: окно каждой версии показывается один раз на всех устройствах.
/// </summary>
public static partial class WhatsNew
{
    [GeneratedRegex("^[0-9]{4}-[0-9]{2}-[0-9]{2}(\\.[0-9]{1,2})?$")]
    private static partial Regex VersionFormat();

    /// <summary>Версия «2026-11-13» (или «2026-11-13.2» — второе обновление за день); иное — null.</summary>
    public static string? Normalize(string? version) =>
        version?.Trim() is { Length: > 0 and <= 32 } v && VersionFormat().IsMatch(v) ? v : null;

    /// <summary>
    /// Что записать после закрытия окна: новую версию, если она позже записанной; закрытое повторно старое окно (Профиль → «Что нового» со
    /// старой вкладки) отметку не откатывает.
    /// </summary>
    public static string Next(string? current, string version) => current is null || Compare(version, current) > 0 ? version : current;

    /// <summary>По дню, затем по номеру за день: «2026-11-13.2» после «2026-11-13», «2026-11-13.10» после «2026-11-13.9».</summary>
    public static int Compare(string a, string b)
    {
        var (dayA, partA) = Split(a);
        var (dayB, partB) = Split(b);
        var byDay = string.CompareOrdinal(dayA, dayB);
        return byDay != 0 ? byDay : partA.CompareTo(partB);
    }

    private static (string Day, int Part) Split(string version)
    {
        var dot = version.IndexOf('.');
        return dot < 0 ? (version, 0) : (version[..dot], int.TryParse(version[(dot + 1)..], out var part) ? part : 0);
    }
}
