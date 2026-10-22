using System.Text;
using System.Text.RegularExpressions;
using OneBase.AI.Llm;

namespace OneBase.AI.Grounding;

/// <summary>Пользователь оспаривает прошлый ответ: тот ответ, его возражение и какое это возражение подряд (настаивает ли он).</summary>
public sealed record CorrectionRequest(string PreviousAnswer, string Argument, int Attempt);

/// <summary>
/// Итог перепроверки после возражения — вычислен по свежим данным, а не по словам модели. Verdict: confirmed — прошлый ответ подтверждён
/// данными, corrected — в нём были неподтверждённые числа (или данные подтвердили пользователя) — ответ исправляется, insufficient — данных
/// для проверки нет. Списки — числа прошлого ответа и пользователя: подтверждены данными или нет.
/// </summary>
public sealed record CorrectionReview(
    string Verdict,
    IReadOnlyList<string> PreviousConfirmed,
    IReadOnlyList<string> PreviousNotConfirmed,
    IReadOnlyList<string> UserConfirmed,
    IReadOnlyList<string> UserNotConfirmed,
    int Attempt,
    Guid? PreviousMessageId = null);

/// <summary>
/// Correction Guard: возражение пользователя («ты ошибся», «неправильно», «передумай», «xato») — не доказательство ошибки. Оно запускает
/// перепроверку по данным (AI-сотрудники заново получают данные с заданием перепроверить), а вывод меняется только если его не подтверждают
/// данные. Настойчивость (повторные возражения без новых данных) факты не меняет.
/// </summary>
public static partial class CorrectionGuard
{
    public const string Confirmed = "confirmed";
    public const string Corrected = "corrected";
    public const string Insufficient = "insufficient";

    [GeneratedRegex(@"(?:ты|вы)\s+(?:ошиб|не\s+прав|напутал|перепутал)|ошибаешь|ошибаетесь|ошибка\s+в\s+(?:ответе|расч|цифр)|неправильн|неверн|это\s+не\s+так|не\s+так[,.!]|" +
                    @"передумай|подумай\s+(?:ещё|еще|снова)|пересчитай|перепроверь|исправь|правильный\s+ответ|на\s+самом\s+деле|неправда|не\s+правда|" +
                    @"вр[её]шь|чушь|ерунд|не\s+верю|это\s+ложь|" +
                    @"xato|noto['ʻ’‘]?g['ʻ’‘]?ri|adashd|qayta\s+tekshir|to['ʻ’‘]?g['ʻ’‘]?ri\s+emas|yolg['ʻ’‘]?on", RegexOptions.IgnoreCase)]
    private static partial Regex Dispute();

    public static bool IsDispute(string? text) => !string.IsNullOrWhiteSpace(text) && Dispute().IsMatch(text);

    /// <summary>Возражение к последнему ответу консультанта; null — это не возражение или ответа ещё не было.</summary>
    public static CorrectionRequest? Detect(string question, IReadOnlyList<LlmMessage> history)
    {
        if (!IsDispute(question))
        {
            return null;
        }

        var previous = history.LastOrDefault(m => m.Role == LlmRole.Assistant);
        if (previous is null || string.IsNullOrWhiteSpace(previous.Content))
        {
            return null;
        }

        // Сколько возражений подряд: это и все предыдущие вопросы пользователя, пока они оспаривают ответ.
        var attempt = 1;
        foreach (var m in history.Reverse().Where(m => m.Role == LlmRole.User))
        {
            if (!IsDispute(m.Content))
            {
                break;
            }

            attempt++;
        }

        return new CorrectionRequest(previous.Content, question, attempt);
    }

    /// <summary>Перепроверка по свежим данным: числа прошлого ответа и пользователя — подтверждены или нет; вердикт — по ним.</summary>
    public static CorrectionReview Review(CorrectionRequest request, EvidenceSet evidence, Guid? previousMessageId = null)
    {
        var previous = NumberClaims.Checkable(request.PreviousAnswer);
        var user = NumberClaims.Find(request.Argument);
        var previousConfirmed = previous.Where(evidence.Supports).ToList();
        var previousNot = previous.Where(c => !evidence.Supports(c)).ToList();
        var userConfirmed = user.Where(evidence.Supports).ToList();
        var userNot = user.Where(c => !evidence.Supports(c)).ToList();

        // Данные подтвердили число пользователя, которого не было в прошлом ответе, — значит, в ответе было не то число.
        var userNew = userConfirmed.Any(u => !previousConfirmed.Any(p => EvidenceCheck.Same(p, u)));
        var verdict = evidence.IsEmpty || (previous.Count == 0 && user.Count == 0) ? Insufficient
            : previousNot.Count > 0 || userNew ? Corrected
            : previousConfirmed.Count > 0 ? Confirmed
            : Insufficient;
        return new CorrectionReview(verdict, Texts(previousConfirmed), Texts(previousNot), Texts(userConfirmed), Texts(userNot), request.Attempt,
            previousMessageId);
    }

    /// <summary>Добавка к задаче AI-сотрудника: перепроверить показатели прошлого ответа заново по данным.</summary>
    public static string AgentTask(CorrectionRequest request)
    {
        var numbers = NumberClaims.Checkable(request.PreviousAnswer).Select(c => c.Text).Distinct().Take(15).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("Пользователь оспаривает прошлый ответ консультанта. Возражение — не доказательство: перепроверь заново по данным инструментов");
        sb.AppendLine("те же показатели, период, регион и фильтры, что в прошлом ответе, и верни точные значения с источником и периодом.");
        if (numbers.Count > 0)
        {
            sb.AppendLine($"Числа прошлого ответа: {string.Join("; ", numbers)}.");
        }

        sb.AppendLine($"Слова пользователя: «{Cut(request.Argument, 400)}». Если он называет другое значение или источник — проверь и их, но не принимай без данных.");
        return sb.ToString();
    }

    /// <summary>Протокол для итогового ответа после возражения: что показала перепроверка и как отвечать.</summary>
    public static string ComposeBlock(CorrectionReview review)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ПОЛЬЗОВАТЕЛЬ ОСПАРИВАЕТ ПРОШЛЫЙ ОТВЕТ" + (review.Attempt > 1 ? $" (уже {review.Attempt}-й раз подряд)" : "") + ". AI-сотрудники перепроверили данные заново.");
        sb.AppendLine("Перепроверка по данным OneBase (посчитано кодом, не моделью):");
        sb.AppendLine($"- числа прошлого ответа, подтверждённые данными: {List(review.PreviousConfirmed)};");
        sb.AppendLine($"- числа прошлого ответа, НЕ подтверждённые данными: {List(review.PreviousNotConfirmed)};");
        sb.AppendLine($"- числа пользователя, подтверждённые данными: {List(review.UserConfirmed)};");
        sb.AppendLine($"- числа пользователя, НЕ подтверждённые данными: {List(review.UserNotConfirmed)}.");
        sb.AppendLine(review.Verdict switch
        {
            Confirmed => "Итог: прошлый ответ ПОДТВЕРЖДЁН данными. Не соглашайся с пользователем и не меняй факты: объясни, почему ответ остаётся верным, " +
                         "назови значение, источник и период. Значение пользователя, если оно не подтверждено, назови и прямо скажи, что текущими данными " +
                         "оно не подтверждается; предложи указать источник (отчёт, выгрузку) — его можно проверить.",
            Corrected => "Итог: в прошлом ответе ЕСТЬ ОШИБКА — исправь её: дай верные значения из данных и конкретно скажи, что было неверно (какое число, " +
                         "почему). Подтверждённое данными число пользователя признай; неподтверждённое — не принимай.",
            _ => "Итог: данных для проверки НЕДОСТАТОЧНО. Не выбирай сторону: скажи «Недостаточно данных для обоснованного вывода», каких данных не " +
                 "хватает, и попроси источник или уточнение (период, регион, отчёт).",
        });
        if (review.Attempt > 1)
        {
            sb.AppendLine("Повторное возражение без новых данных — не повод менять факты: настойчивость не доказательство.");
        }

        return sb.ToString();
    }

    private static IReadOnlyList<string> Texts(IEnumerable<NumberClaim> claims) => claims.Select(c => c.Text).Distinct().ToList();

    private static string List(IReadOnlyList<string> items) => items.Count == 0 ? "нет" : string.Join(", ", items);

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
