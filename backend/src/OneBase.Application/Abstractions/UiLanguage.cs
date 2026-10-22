namespace OneBase.Application.Abstractions;

/// <summary>
/// Язык интерфейса пользователя на время запроса: «ru» или «uz» (кнопка RU/UZ; фронтенд шлёт его в Accept-Language).
/// Ставит middleware API; фоновая работа, начатая из запроса (ответ консультанта дописывается и после закрытия вкладки),
/// наследует значение. Вне запроса — русский.
/// </summary>
public static class UiLanguage
{
    private static readonly AsyncLocal<string?> CurrentValue = new();

    public static string Current => CurrentValue.Value ?? "ru";

    public static bool IsUzbek => Current == "uz";

    public static void Set(string? acceptLanguage) => CurrentValue.Value = Parse(acceptLanguage);

    /// <summary>«uz», «uz-UZ», «uz-Latn» — узбекский; всё остальное — русский.</summary>
    public static string Parse(string? acceptLanguage) =>
        acceptLanguage?.TrimStart().StartsWith("uz", StringComparison.OrdinalIgnoreCase) == true ? "uz" : "ru";

    /// <summary>Для инструкций модели: на каком языке отвечать, если вопрос не подсказывает другой.</summary>
    public static string DefaultAnswerLanguage => IsUzbek ? "узбекский (латиница, как в интерфейсе)" : "русский";
}
