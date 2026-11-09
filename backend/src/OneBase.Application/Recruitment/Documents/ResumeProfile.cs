using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OneBase.Domain.Recruitment;

namespace OneBase.Application.Recruitment.Documents;

/// <summary>
/// Поле резюме: значение, цитата из текста (доказательство), основание — stated (сказано в резюме) или inferred (выведено из контекста, например
/// стаж по датам), проверено ли — цитата найдена в тексте, текст прочитан уверенно и поле сказано прямо. Непроверенное — подсказка, а не факт.
/// </summary>
public sealed record ProfileField(string? Value, string? Evidence = null, string Basis = "stated", bool Verified = false);

public sealed record ProfileExperience(string? Company, string? Position, string? From, string? To, string? Description, string? Evidence = null, bool Verified = false);

public sealed record ProfileEducation(string? Institution, string? Degree, string? Year, string? Evidence = null, bool Verified = false);

/// <summary>Разбор резюме: поля из задания; Quality — как прочитан текст (good, partial, poor — OCR с неразборчивыми местами); Summary — сводка AI.</summary>
public sealed record ResumeProfile(
    ProfileField? FullName,
    ProfileField? Phone,
    ProfileField? Email,
    ProfileField? Location,
    ProfileField? ProfessionalTitle,
    ProfileField? ExperienceYears,
    IReadOnlyList<ProfileExperience> WorkExperience,
    IReadOnlyList<ProfileEducation> Education,
    IReadOnlyList<ProfileField> Skills,
    IReadOnlyList<ProfileField> Languages,
    IReadOnlyList<ProfileField> Certificates,
    ProfileField? SalaryExpectation,
    ProfileField? AdditionalInformation,
    string? Summary,
    string Quality = "good",
    string? Model = null,
    IReadOnlyList<string>? ExtraPhones = null,
    IReadOnlyList<string>? ExtraEmails = null)
{
    public static readonly ResumeProfile Empty = new(null, null, null, null, null, null, [], [], [], [], [], null, null, null);
}

/// <summary>Разбор резюме моделью AI (реализация — OneBase.AI; платный вызов «hr.resume.analysis»). Нет модели — разбор без AI (только контакты).</summary>
public interface IResumeAnalyzer
{
    Task<ResumeProfile> AnalyzeAsync(string text, string? vacancy, CancellationToken ct);
}

/// <summary>Модель не настроена или выключена — разбор без AI: только контакты из текста.</summary>
public sealed class ResumeAnalyzerUnavailableException(string message) : Exception(message);

/// <summary>
/// Проверка разбора на сервере: цитата поля должна быть в тексте резюме (без учёта регистра, пробелов и кавычек); выведенное поле не бывает
/// «проверенным»; текст, прочитанный с неразборчивыми местами (poor), — всё непроверенное. Телефоны и почта — дополнительно регулярными
/// выражениями прямо из текста (они точны и без AI).
/// </summary>
public static partial class ResumeProfiles
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Как прочитан текст: OCR с несколькими «[неразборчиво]» — partial, со многими — poor.</summary>
    public static string Quality(string text, bool byAi)
    {
        if (!byAi)
        {
            return "good";
        }

        var unreadable = Regex.Count(text, @"\[неразборчиво\]|\[unreadable\]|\[o'qib bo'lmaydi\]", RegexOptions.IgnoreCase);
        return unreadable == 0 ? "good" : unreadable <= 3 ? "partial" : "poor";
    }

    public static ResumeProfile Verify(ResumeProfile p, string text, string quality)
    {
        var haystack = Normalize(text);
        ProfileField? F(ProfileField? f) => f is null || string.IsNullOrWhiteSpace(f.Value) ? null
            : f with { Verified = quality != "poor" && f.Basis != "inferred" && Found(haystack, f.Evidence ?? f.Value) };
        var phones = Phones(text);
        var emails = Emails(text);
        var phone = F(p.Phone);
        if (phone?.Value is { } pv)
        {
            phone = RecruitmentRules.Normalize(CandidateContactKind.Phone, pv) is { } normalized
                ? phone with { Value = normalized, Verified = phone.Verified || phones.Contains(normalized) }
                : null;
        }

        phone ??= phones.FirstOrDefault() is { } first ? new ProfileField(first, first, "stated", quality != "poor") : null;
        var email = F(p.Email);
        if (email?.Value is { } ev)
        {
            email = RecruitmentRules.Normalize(CandidateContactKind.Email, ev) is { } normalized
                ? email with { Value = normalized, Verified = email.Verified || emails.Contains(normalized) }
                : null;
        }

        email ??= emails.FirstOrDefault() is { } firstEmail ? new ProfileField(firstEmail, firstEmail, "stated", quality != "poor") : null;
        return p with
        {
            FullName = F(p.FullName),
            Phone = phone,
            Email = email,
            Location = F(p.Location),
            ProfessionalTitle = F(p.ProfessionalTitle),
            ExperienceYears = F(p.ExperienceYears),
            SalaryExpectation = F(p.SalaryExpectation),
            AdditionalInformation = F(p.AdditionalInformation),
            Skills = p.Skills.Select(F).OfType<ProfileField>().Take(40).ToList(),
            Languages = p.Languages.Select(F).OfType<ProfileField>().Take(10).ToList(),
            Certificates = p.Certificates.Select(F).OfType<ProfileField>().Take(20).ToList(),
            WorkExperience = p.WorkExperience.Take(20).Select(w => w with
            {
                Verified = quality != "poor" && Found(haystack, w.Evidence ?? w.Company ?? w.Position),
            }).ToList(),
            Education = p.Education.Take(10).Select(e => e with { Verified = quality != "poor" && Found(haystack, e.Evidence ?? e.Institution) }).ToList(),
            Quality = quality,
            ExtraPhones = phones.Where(x => x != phone?.Value).Take(5).ToList(),
            ExtraEmails = emails.Where(x => x != email?.Value).Take(5).ToList(),
        };
    }

    /// <summary>Разбор без AI — только контакты из текста (точные).</summary>
    public static ResumeProfile ContactsOnly(string text, string quality) => Verify(ResumeProfile.Empty, text, quality);

    private static bool Found(string haystack, string? evidence)
    {
        var needle = Normalize(evidence ?? string.Empty);
        return needle.Length >= 2 && haystack.Contains(needle, StringComparison.Ordinal);
    }

    /// <summary>Для сравнения: нижний регистр, без пробелов, кавычек, тире и апострофов разных видов.</summary>
    internal static string Normalize(string s) =>
        Ignored().Replace(s.ToLowerInvariant().Replace('ё', 'е'), string.Empty);

    public static IReadOnlyList<string> Phones(string text) =>
        PhoneLike().Matches(text).Select(m => RecruitmentRules.Normalize(CandidateContactKind.Phone, m.Value)).OfType<string>().Distinct().ToList();

    public static IReadOnlyList<string> Emails(string text) =>
        EmailLike().Matches(text).Select(m => RecruitmentRules.Normalize(CandidateContactKind.Email, m.Value)).OfType<string>().Distinct().ToList();

    public static string Serialize(ResumeProfile p) => JsonSerializer.Serialize(p, Json);

    public static ResumeProfile? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ResumeProfile>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"[\s""'«»“”„‘’ʻʼ`´\-–—_.,;:()\[\]]+")]
    private static partial Regex Ignored();

    [GeneratedRegex(@"(?<![\d+])(?:\+?998|8)?[\s\-()]*\d{2}[\s\-()]*\d{3}[\s\-]*\d{2}[\s\-]*\d{2}(?!\d)")]
    private static partial Regex PhoneLike();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailLike();
}
