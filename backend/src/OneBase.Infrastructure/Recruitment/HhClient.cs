using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OneBase.Application.Recruitment.Hh;

namespace OneBase.Infrastructure.Recruitment;

/// <summary>
/// Клиент официального API HeadHunter (https://api.hh.ru/, сайт — host=hh.uz). Каждый запрос — с HH-User-Agent «OneBase/1.0 (почта)»
/// (обязателен по спецификации). Адреса резюме и действий берутся из ответов HH и принимаются только с хоста api.hh.ru. В копии для проверки
/// HttpClient закрыт обработчиком изоляции.
/// </summary>
internal sealed class HhClient(HttpClient http) : IHhApi
{
    public const string ApiHost = "api.hh.ru";
    public const string Site = "hh.uz";
    private static readonly Uri Api = new($"https://{ApiHost}/");

    public string AuthorizeUrl(string clientId, string redirectUri, string state) =>
        $"https://hh.ru/oauth/authorize?response_type=code&client_id={Uri.EscapeDataString(clientId)}&state={Uri.EscapeDataString(state)}"
        + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";

    public Task<HhTokens> ExchangeCodeAsync(HhApp app, string code, CancellationToken ct) =>
        TokenAsync(app, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = app.ClientId,
            ["client_secret"] = app.ClientSecret,
            ["redirect_uri"] = app.RedirectUri,
            ["code"] = code,
        }, ct);

    public Task<HhTokens> RefreshAsync(HhApp app, string refreshToken, CancellationToken ct) =>
        TokenAsync(app, new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken }, ct);

    private async Task<HhTokens> TokenAsync(HhApp app, Dictionary<string, string> form, CancellationToken ct)
    {
        using var request = Request(app, HttpMethod.Post, new Uri(Api, $"token?host={Site}"), null);
        request.Content = new FormUrlEncodedContent(form);
        using var doc = await SendAsync(request, ct);
        var root = doc.RootElement;
        var expires = root.TryGetProperty("expires_in", out var e) && e.TryGetInt64(out var seconds) ? seconds : 1209600;
        return new HhTokens(Str(root, "access_token") ?? throw new HhApiException("HH не вернул access_token.", 200),
            Str(root, "refresh_token") ?? string.Empty, DateTimeOffset.UtcNow.AddSeconds(expires));
    }

    public async Task<HhMe> MeAsync(HhApp app, string token, CancellationToken ct)
    {
        using var doc = await SendAsync(Request(app, HttpMethod.Get, Url("me"), token), ct);
        var employer = doc.RootElement.TryGetProperty("employer", out var e) && e.ValueKind == JsonValueKind.Object ? e : default;
        return employer.ValueKind == JsonValueKind.Object ? new HhMe(Str(employer, "id"), Str(employer, "name")) : new HhMe(null, null);
    }

    public async Task<HhPage<HhVacancyRef>> ActiveVacanciesAsync(HhApp app, string token, string employerId, int page, CancellationToken ct)
    {
        using var doc = await SendAsync(Request(app, HttpMethod.Get, Url($"employers/{Uri.EscapeDataString(employerId)}/vacancies/active", $"page={page}&per_page=50"), token), ct);
        return Page(doc.RootElement, item => new HhVacancyRef(Str(item, "id") ?? string.Empty, Str(item, "name") ?? "—",
            item.TryGetProperty("area", out var area) && area.ValueKind == JsonValueKind.Object ? Str(area, "name") : null));
    }

    public async Task<HhPage<HhNegotiationItem>> ResponsesAsync(HhApp app, string token, string vacancyId, int page, CancellationToken ct)
    {
        using var doc = await SendAsync(Request(app, HttpMethod.Get, Url("negotiations/response", $"vacancy_id={Uri.EscapeDataString(vacancyId)}&page={page}&per_page=50"), token), ct);
        return Page(doc.RootElement, item =>
        {
            var resume = item.TryGetProperty("resume", out var r) && r.ValueKind == JsonValueKind.Object ? r : default;
            var actions = new List<HhAction>();
            if (item.TryGetProperty("actions", out var a) && a.ValueKind == JsonValueKind.Array)
            {
                foreach (var action in a.EnumerateArray())
                {
                    if (Str(action, "id") is { } id && Str(action, "url") is { } url && Str(action, "method") is { } method && Trusted(url))
                    {
                        actions.Add(new HhAction(id, Str(action, "name") ?? id, method.ToUpperInvariant(), url));
                    }
                }
            }

            var has = resume.ValueKind == JsonValueKind.Object;
            string? salary = null;
            if (has && resume.TryGetProperty("salary", out var sal) && sal.ValueKind == JsonValueKind.Object && Str(sal, "amount") is { } amount)
            {
                salary = $"{amount} {Str(sal, "currency")}".Trim();
            }

            int? months = has && resume.TryGetProperty("total_experience", out var total) && total.ValueKind == JsonValueKind.Object && Int(total, "months") is > 0 and var m
                ? m
                : null;
            var site = has ? Str(resume, "alternate_url") : null;
            return new HhNegotiationItem(Str(item, "id") ?? string.Empty,
                item.TryGetProperty("state", out var s) && s.ValueKind == JsonValueKind.Object ? Str(s, "id") : null,
                Date(Str(item, "updated_at")),
                has ? Str(resume, "id") ?? string.Empty : string.Empty,
                has ? Str(resume, "url") : null,
                has ? Str(resume, "first_name") : null,
                has ? Str(resume, "last_name") : null,
                has ? Str(resume, "middle_name") : null,
                has ? Str(resume, "title") : null,
                actions,
                has && resume.TryGetProperty("area", out var area) && area.ValueKind == JsonValueKind.Object ? Str(area, "name") : null,
                salary,
                months,
                HhLinks.IsSite(site) ? site : null);
        });
    }

    public async Task<HhResume> ResumeAsync(HhApp app, string token, string resumeUrl, CancellationToken ct)
    {
        var uri = new Uri(resumeUrl);
        if (!Trusted(resumeUrl))
        {
            throw new HhApiException("Адрес резюме не с api.hh.ru — не запрашиваем.", 0);
        }

        var query = string.IsNullOrEmpty(uri.Query) ? $"?host={Site}" : $"{uri.Query}&host={Site}";
        using var doc = await SendAsync(Request(app, HttpMethod.Get, new Uri($"{uri.GetLeftPart(UriPartial.Path)}{query}"), token), ct);
        var root = doc.RootElement;
        var contacts = new List<HhContactItem>();
        if (root.TryGetProperty("contact", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in list.EnumerateArray())
            {
                var kind = Str(c, "kind") ?? (c.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.Object ? Str(t, "id") : null) ?? "other";
                var value = Str(c, "contact_value");
                if (value is null && c.TryGetProperty("value", out var v))
                {
                    value = v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind == JsonValueKind.Object ? Str(v, "formatted") ?? Str(v, "number") : null;
                }

                contacts.Add(new HhContactItem(kind, value, c.TryGetProperty("verified", out var ver) && ver.ValueKind == JsonValueKind.True));
            }
        }

        return new HhResume(Str(root, "id") ?? string.Empty, Str(root, "first_name"), Str(root, "last_name"), Str(root, "middle_name"), Str(root, "title"),
            root.TryGetProperty("area", out var area) && area.ValueKind == JsonValueKind.Object ? Str(area, "name") : null, contacts, ResumeText(root));
    }

    public async Task ExecuteActionAsync(HhApp app, string token, HhAction action, string? message, CancellationToken ct)
    {
        if (!Trusted(action.Url))
        {
            throw new HhApiException("Адрес действия не с api.hh.ru — не выполняем.", 0);
        }

        var uri = new Uri(action.Url);
        var url = new Uri($"{uri.GetLeftPart(UriPartial.Path)}{(string.IsNullOrEmpty(uri.Query) ? "?" : uri.Query + "&")}host={Site}");
        using var request = Request(app, new HttpMethod(action.Method), url, token);
        request.Content = new FormUrlEncodedContent(string.IsNullOrWhiteSpace(message) ? [] : new Dictionary<string, string> { ["message"] = message });
        using var _ = await SendAsync(request, ct);
    }

    // ---------- Общее ----------

    private static bool Trusted(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.Host == ApiHost;

    private static Uri Url(string path, string? query = null) => new(Api, $"{path}?{(query is null ? string.Empty : query + "&")}host={Site}");

    private static HttpRequestMessage Request(HhApp app, HttpMethod method, Uri uri, string? token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("HH-User-Agent", $"OneBase/1.0 ({app.ContactEmail})");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var _ = request;
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.IsSuccessStatusCode)
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        }

        string? type = null, value = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                type = Str(errors[0], "type");
                value = Str(errors[0], "value");
            }
            else
            {
                type = Str(doc.RootElement, "error");
                value = Str(doc.RootElement, "error_description");
            }
        }
        catch (JsonException)
        {
            // не JSON
        }

        var status = (int)response.StatusCode;
        throw new HhApiException(Explain(status, type, value), status, type, value);
    }

    private static string Explain(int status, string? type, string? value) => (type, value) switch
    {
        ("api_access_payment", _) => "HH: нужен платный доступ работодателя к откликам и резюме (api_access_payment).",
        ("resumes", "view_limit_exceeded") => "HH: исчерпан суточный лимит просмотров резюме.",
        ("resumes", "quota_exceeded") => "HH: у менеджера исчерпана квота просмотра резюме.",
        ("resumes", "no_available_service") => "HH: не хватает услуг для просмотра резюме.",
        ("resumes", "cant_view_contacts") => "HH: нет права смотреть контакты в резюме.",
        ("oauth", "token_expired") => "HH: срок токена истёк — обновляем.",
        ("oauth", "token_revoked") => "HH: доступ отозван — подключите HH заново.",
        ("oauth", _) => $"HH: ошибка авторизации ({value}) — подключите HH заново.",
        ("bad_user_agent", _) => "HH: не принят заголовок HH-User-Agent — укажите почту разработчика в настройках HH.",
        ("invalid_grant", _) or ("invalid_request", _) => $"HH: вход не выполнен ({value}).",
        ("captcha_required", _) => "HH просит пройти капчу — откройте HH в браузере под этим аккаунтом.",
        _ when status == 429 => "HH: слишком много запросов — повторим позже.",
        _ when status >= 500 => "HH временно недоступен — повторим позже.",
        _ => $"HH ответил ошибкой {status}{(type is null ? string.Empty : $" ({type}{(value is null ? string.Empty : $": {value}")})")}.",
    };

    private static HhPage<T> Page<T>(JsonElement root, Func<JsonElement, T> map)
    {
        var items = new List<T>();
        if (root.TryGetProperty("items", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            items.AddRange(list.EnumerateArray().Select(map));
        }

        return new HhPage<T>(items, Int(root, "page"), Math.Max(1, Int(root, "pages")));
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.GetRawText(), _ => null }
            : null;

    private static int Int(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;

    private static DateTimeOffset? Date(string? s) =>
        DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d.ToUniversalTime() : null; // HH присылает +0500, в базе — UTC

    /// <summary>Текст резюме для поиска и оценки: должность, опыт (компания, должность, период, описание), навыки, образование, языки.</summary>
    private static string ResumeText(JsonElement r)
    {
        var sb = new StringBuilder();
        if (Str(r, "title") is { } title)
        {
            sb.AppendLine($"Должность: {title}");
        }

        if (r.TryGetProperty("salary", out var salary) && salary.ValueKind == JsonValueKind.Object && Str(salary, "amount") is { } amount)
        {
            sb.AppendLine($"Желаемая зарплата: {amount} {Str(salary, "currency")}");
        }

        if (r.TryGetProperty("total_experience", out var total) && total.ValueKind == JsonValueKind.Object && Int(total, "months") is > 0 and var months)
        {
            sb.AppendLine($"Общий опыт: {months / 12} г. {months % 12} мес.");
        }

        if (r.TryGetProperty("experience", out var experience) && experience.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine("Опыт работы:");
            foreach (var x in experience.EnumerateArray())
            {
                sb.AppendLine($"- {Str(x, "position")} — {Str(x, "company")} ({Str(x, "start")} – {Str(x, "end") ?? "по настоящее время"})");
                if (Str(x, "description") is { } d)
                {
                    sb.AppendLine($"  {d}");
                }
            }
        }

        if (r.TryGetProperty("skill_set", out var skills) && skills.ValueKind == JsonValueKind.Array && skills.GetArrayLength() > 0)
        {
            sb.AppendLine($"Навыки: {string.Join(", ", skills.EnumerateArray().Where(s => s.ValueKind == JsonValueKind.String).Select(s => s.GetString()))}");
        }

        if (Str(r, "skills") is { } about)
        {
            sb.AppendLine($"О себе: {about}");
        }

        if (r.TryGetProperty("education", out var education) && education.ValueKind == JsonValueKind.Object
            && education.TryGetProperty("primary", out var primary) && primary.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in primary.EnumerateArray())
            {
                sb.AppendLine($"Образование: {Str(x, "name")} {Str(x, "result")} {Str(x, "year")}".TrimEnd());
            }
        }

        return sb.ToString().Trim();
    }
}
