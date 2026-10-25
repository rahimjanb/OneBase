using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OneBase.Infrastructure.Hr;

/// <summary>
/// Форматы Verifix по документации: даты «dd.mm.yyyy», время «dd.mm.yyyy hh24:mi:ss» — во временной зоне организации и без смещения
/// (track$list: «Время отметки во временной зоне организации»). Числа в примерах ответов — строками ("165"), поэтому разбор чисел
/// принимает и строку, и число.
/// </summary>
public static class VerifixFormats
{
    public const string DateFormat = "dd.MM.yyyy";
    public const string DateTimeFormat = "dd.MM.yyyy HH:mm:ss";

    private static readonly string[] DateTimeFormats = [DateTimeFormat, "dd.MM.yyyy HH:mm", DateFormat, "yyyy-MM-dd HH:mm:ss"];

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static string Date(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string DateTime(DateTime local) => local.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    public static DateOnly? ParseDate(string? text) =>
        !string.IsNullOrWhiteSpace(text) && System.DateTime.TryParseExact(text.Trim(), DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? DateOnly.FromDateTime(value)
            : null;

    /// <summary>Локальное время организации (Kind = Unspecified). Дата без времени — полночь (так приходят запросы на целые дни).</summary>
    public static DateTime? ParseLocal(string? text) =>
        !string.IsNullOrWhiteSpace(text) && System.DateTime.TryParseExact(text.Trim(), DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? System.DateTime.SpecifyKind(value, DateTimeKind.Unspecified)
            : null;

    /// <summary>Часовой пояс организации; неизвестный идентификатор — Ташкент (UTC+5, без перехода на летнее время).</summary>
    public static TimeZoneInfo Zone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
            }
        }

        return Tashkent;
    }

    public static readonly TimeZoneInfo Tashkent = TashkentZone();

    private static TimeZoneInfo TashkentZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("Asia/Tashkent", TimeSpan.FromHours(5), "Tashkent", "Tashkent");
        }
    }

    /// <summary>Локальное время организации → момент UTC.</summary>
    public static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = System.DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = zone.IsInvalidTime(unspecified) ? zone.BaseUtcOffset : zone.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset).ToUniversalTime();
    }

    /// <summary>Момент → локальное время организации.</summary>
    public static DateTime ToLocal(DateTimeOffset moment, TimeZoneInfo zone) =>
        System.DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(moment, zone).DateTime, DateTimeKind.Unspecified);
}
