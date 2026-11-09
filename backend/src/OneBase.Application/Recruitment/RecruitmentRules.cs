using System.Text.RegularExpressions;
using OneBase.Domain.Recruitment;

namespace OneBase.Application.Recruitment;

/// <summary>Правила подбора без базы: нормализация контактов, этапы, срок хранения, ограничения файлов резюме.</summary>
public static partial class RecruitmentRules
{
    public const string PurposeRecruitment = "recruitment";

    public static readonly string[] Sources = ["manual", "hh", "telegram", "referral", "other"];
    public static readonly string[] ConsentChannels = ["written", "telegram", "hh", "email", "verbal"];
    public static readonly string[] EmploymentTypes = ["full", "part", "shift", "remote", "temporary"];
    public static readonly string[] Languages = ["ru", "uz"];

    public const long MaxResumeBytes = 20L * 1024 * 1024;
    public const int MaxResumesPerCandidate = 10;

    /// <summary>Текста резюме — не больше (для поиска и оценки хватает; огромный файл не раздувает базу).</summary>
    public const int MaxResumeText = 100_000;

    public static readonly HashSet<string> ResumeExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".docx", ".doc", ".rtf", ".odt", ".txt", ".jpg", ".jpeg", ".png", ".webp" };

    public const int MinRetentionDays = 1;
    public const int MaxRetentionDays = 365;

    /// <summary>Текст согласия по умолчанию (версия 1) — без названия компании; HR меняет его в настройках подбора.</summary>
    public const string DefaultConsentText =
        "Я согласен(на) на обработку моих персональных данных (ФИО, контакты, резюме, переписка) компанией для подбора на вакансии. "
        + "Данные хранятся, пока идёт подбор, и удаляются после его завершения в срок, указанный компанией. Согласие можно отозвать в любой момент — "
        + "тогда данные будут удалены.";

    public static bool IsClosed(ApplicationStage stage) => stage is ApplicationStage.Hired or ApplicationStage.Rejected or ApplicationStage.Withdrawn;

    /// <summary>
    /// С какого момента идёт срок хранения: null — есть открытый отклик (идёт подбор); иначе — позднее из последней активности и закрытия
    /// последнего отклика.
    /// </summary>
    public static DateTimeOffset? RetentionFrom(IEnumerable<(ApplicationStage Stage, DateTimeOffset? ClosedAt)> applications, DateTimeOffset lastActivity)
    {
        var anchor = lastActivity;
        foreach (var (stage, closedAt) in applications)
        {
            if (!IsClosed(stage))
            {
                return null;
            }

            if (closedAt is { } closed && closed > anchor)
            {
                anchor = closed;
            }
        }

        return anchor;
    }

    /// <summary>Когда данные будут удалены (null — идёт подбор).</summary>
    public static DateTimeOffset? DeleteAt(DateTimeOffset? retentionFrom, int retentionDays) => retentionFrom?.AddDays(retentionDays);

    /// <summary>
    /// Нормализованный контакт или null (не похож на контакт этого вида). Телефон — +998XXXXXXXXX (9 цифр без кода страны — номер Узбекистана)
    /// или +код и 8–15 цифр; e-mail — в нижнем регистре; Telegram ID — число; username — без @ и ссылки t.me, в нижнем регистре.
    /// </summary>
    public static string? Normalize(CandidateContactKind kind, string? raw)
    {
        var value = raw?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return null;
        }

        switch (kind)
        {
            case CandidateContactKind.Phone:
            {
                var plus = value.StartsWith('+');
                var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
                if (!plus && digits.Length == 9)
                {
                    return "+998" + digits;
                }

                if (!plus && digits.Length == 12 && digits.StartsWith("998", StringComparison.Ordinal))
                {
                    return "+" + digits;
                }

                return plus && digits.Length is >= 8 and <= 15 ? "+" + digits : null;
            }

            case CandidateContactKind.Email:
                value = value.ToLowerInvariant();
                return EmailPattern().IsMatch(value) && value.Length <= 200 ? value : null;

            case CandidateContactKind.Telegram:
                return long.TryParse(value, out var id) && id > 0 ? id.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;

            case CandidateContactKind.TelegramUsername:
                foreach (var prefix in new[] { "https://t.me/", "http://t.me/", "t.me/", "@" })
                {
                    if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        value = value[prefix.Length..];
                    }
                }

                value = value.TrimEnd('/').ToLowerInvariant();
                return UsernamePattern().IsMatch(value) ? value : null;

            default:
                return null;
        }
    }

    /// <summary>Понятное имя вида контакта (для ошибок и истории).</summary>
    public static string KindLabel(CandidateContactKind kind) => kind switch
    {
        CandidateContactKind.Phone => "телефон",
        CandidateContactKind.Email => "e-mail",
        CandidateContactKind.Telegram => "Telegram ID",
        CandidateContactKind.TelegramUsername => "Telegram username",
        _ => kind.ToString(),
    };

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex("^[a-z0-9_]{5,32}$")]
    private static partial Regex UsernamePattern();
}
