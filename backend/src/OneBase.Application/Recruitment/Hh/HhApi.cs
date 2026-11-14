namespace OneBase.Application.Recruitment.Hh;

/// <summary>Приложение HH (dev.hh.ru): client_id, client_secret, redirect_uri и почта разработчика для HH-User-Agent.</summary>
public sealed record HhApp(string ClientId, string ClientSecret, string RedirectUri, string ContactEmail);

public sealed record HhTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

public sealed record HhMe(string? EmployerId, string? EmployerName);

public sealed record HhVacancyRef(string Id, string Name, string? Area);

/// <summary>Действие по отклику из ответа HH (actions[]): выполняется запросом по url и method, которые отдал HH.</summary>
public sealed record HhAction(string Id, string Name, string Method, string Url);

/// <summary>
/// Отклик из списка (GET /negotiations/response — бесплатно): краткое резюме из ответа — имя, должность, город, зарплата, стаж, ссылка на сайт.
/// Пол, возраст и фото не берутся: на оценку они влиять не должны. Полное резюме (просмотр расходует лимит HH) — не загружается.
/// </summary>
public sealed record HhNegotiationItem(string Id, string? State, DateTimeOffset? UpdatedAt, string ResumeId, string? ResumeUrl, string? FirstName, string? LastName,
    string? MiddleName, string? Title, IReadOnlyList<HhAction> Actions, string? Area = null, string? Salary = null, int? ExperienceMonths = null,
    string? AlternateUrl = null);

public sealed record HhContactItem(string Kind, string? Value, bool Verified);

public sealed record HhResume(string Id, string? FirstName, string? LastName, string? MiddleName, string? Title, string? Area, IReadOnlyList<HhContactItem> Contacts,
    string Text);

public sealed record HhPage<T>(IReadOnlyList<T> Items, int Page, int Pages);

/// <summary>Ошибка API HH: статус и тип из ответа (errors[].type / value или error / error_description), текст — для HR.</summary>
public sealed class HhApiException(string message, int status, string? type = null, string? value = null) : Exception(message)
{
    public int Status { get; } = status;
    public string? Type { get; } = type;
    public string? Value { get; } = value;

    public bool TokenExpired => Type == "oauth" && Value == "token_expired";
}

/// <summary>
/// Официальный API HeadHunter (api.hh.ru, сайт hh.uz — параметр host). Только методы из спецификации
/// https://api.hh.ru/openapi/specification/public: вход OAuth (hh.ru/oauth/authorize, POST /token), /me, опубликованные вакансии
/// работодателя, отклики коллекции «response», просмотр резюме, действие по отклику из actions[]. Переписка в откликах в API устарела — не
/// используется. Каждый вызов идёт через Quota Guard (<see cref="HhOperations"/>): просмотр резюме расходует лимит HH и заблокирован.
/// </summary>
public interface IHhApi
{
    string AuthorizeUrl(string clientId, string redirectUri, string state);

    Task<HhTokens> ExchangeCodeAsync(HhApp app, string code, CancellationToken ct);

    Task<HhTokens> RefreshAsync(HhApp app, string refreshToken, CancellationToken ct);

    Task<HhMe> MeAsync(HhApp app, string token, CancellationToken ct);

    Task<HhPage<HhVacancyRef>> ActiveVacanciesAsync(HhApp app, string token, string employerId, int page, CancellationToken ct);

    Task<HhPage<HhNegotiationItem>> ResponsesAsync(HhApp app, string token, string vacancyId, int page, CancellationToken ct);

    Task<HhResume> ResumeAsync(HhApp app, string token, string resumeUrl, CancellationToken ct);

    Task ExecuteActionAsync(HhApp app, string token, HhAction action, string? message, CancellationToken ct);
}

/// <summary>Ссылки на сайт HH: резюме кандидата открывает HR сам (в своём аккаунте), OneBase их не загружает.</summary>
public static partial class HhLinks
{
    /// <summary>Адрес сайта HH (https, hh.uz или hh.ru и их поддомены).</summary>
    public static bool IsSite(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps
        && (u.Host is "hh.uz" or "hh.ru" || u.Host.EndsWith(".hh.uz", StringComparison.Ordinal) || u.Host.EndsWith(".hh.ru", StringComparison.Ordinal));

    /// <summary>Резюме на hh.uz по его ID.</summary>
    public static string Resume(string resumeId) => $"https://hh.uz/resume/{Uri.EscapeDataString(resumeId)}";

    /// <summary>ID резюме из ссылки на профиль (hh.uz/resume/…, hh.ru/resume/…); не ссылка HH — null.</summary>
    public static string? ResumeId(string? url)
    {
        if (!IsSite(url?.Trim()))
        {
            return null;
        }

        var m = ResumePath().Match(new Uri(url!.Trim()).AbsolutePath);
        return m.Success ? m.Groups[1].Value : null;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^/resume/([0-9a-fA-F]{20,64})/?$")]
    private static partial System.Text.RegularExpressions.Regex ResumePath();
}
