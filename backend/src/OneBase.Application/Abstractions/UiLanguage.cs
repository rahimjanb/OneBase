namespace OneBase.Application.Abstractions;

/// <summary>
/// Язык интерфейса пользователя на время запроса: «ru», «uz» (узбекский латиницей) или «uz-Cyrl» (узбекский кириллицей) — кнопка RU / UZ / ЎЗ;
/// фронтенд шлёт его в Accept-Language. Ставит middleware API; фоновая работа, начатая из запроса (ответ консультанта дописывается и после
/// закрытия вкладки), наследует значение. Вне запроса — русский.
/// </summary>
public static class UiLanguage
{
    public const string Russian = "ru";
    public const string UzbekLatin = "uz";
    public const string UzbekCyrillicCode = "uz-Cyrl";

    private static readonly AsyncLocal<string?> CurrentValue = new();

    public static string Current => CurrentValue.Value ?? Russian;

    /// <summary>Узбекский — латиницей или кириллицей.</summary>
    public static bool IsUzbek => Current is UzbekLatin or UzbekCyrillicCode;

    public static bool IsCyrillic => Current == UzbekCyrillicCode;

    public static void Set(string? acceptLanguage) => CurrentValue.Value = Parse(acceptLanguage);

    /// <summary>«uz-Cyrl» — узбекский кириллицей; «uz», «uz-UZ», «uz-Latn» — латиницей; всё остальное — русский.</summary>
    public static string Parse(string? acceptLanguage)
    {
        var value = acceptLanguage?.TrimStart() ?? "";
        return value.StartsWith("uz-cyrl", StringComparison.OrdinalIgnoreCase) ? UzbekCyrillicCode
            : value.StartsWith("uz", StringComparison.OrdinalIgnoreCase) ? UzbekLatin
            : Russian;
    }

    /// <summary>Язык отчёта из настроек и запросов: «ru», «uz», «uz-Cyrl» (без учёта регистра); иное — null.</summary>
    public static string? Normalize(string? language) => language?.Trim().ToLowerInvariant() switch
    {
        "ru" => Russian,
        "uz" or "uz-latn" => UzbekLatin,
        "uz-cyrl" => UzbekCyrillicCode,
        _ => null,
    };

    /// <summary>Узбекский текст (написан латиницей) на языке интерфейса: кириллицей — переведённый, иначе как есть.</summary>
    public static string Uzbek(string latin) => IsCyrillic ? UzbekCyrillic.Convert(latin) : latin;

    /// <summary>Для инструкций модели: на каком языке отвечать, если вопрос не подсказывает другой.</summary>
    public static string DefaultAnswerLanguage => IsCyrillic ? "узбекский (кириллица, как в интерфейсе)" : IsUzbek ? "узбекский (латиница, как в интерфейсе)" : "русский";
}
