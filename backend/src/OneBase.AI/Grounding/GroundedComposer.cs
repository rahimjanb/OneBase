using System.Text;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Providers;

namespace OneBase.AI.Grounding;

/// <summary>
/// Проверка ответа для журнала и разбора в чате: сколько чисел проверено, какие не подтверждены, исправлялся ли ответ (Revised) и пришлось ли
/// пометить неподтверждённое прямо в тексте (Warned).
/// </summary>
public sealed record GroundingSummary(int Checked, int Supported, IReadOnlyList<string> Unsupported, IReadOnlyList<string> Quoted, bool Revised, bool Warned);

public sealed record GroundedAnswer(string Text, GroundingSummary Grounding, AiTokenUsage Usage, AiModelRef Model, bool UsedFallback);

/// <summary>
/// Итоговый ответ с проверкой по данным: модель пишет ответ, код сверяет каждое число с доказательствами (EvidenceCheck). Неподтверждённые числа —
/// одна попытка исправить ответ с указанием, какие именно; если и после неё число не подтверждено — ответ не выдаёт его за факт: в конце
/// пометка «не подтверждено данными OneBase». Сравнивается с данными, а не с тем, что хочет услышать пользователь.
/// </summary>
public sealed class GroundedComposer(IAiGateway gateway)
{
    /// <summary>quotable — оспоренный прошлый ответ: его числа ответ может привести только с оговоркой, что они неверны или не подтверждены.</summary>
    public async Task<GroundedAnswer> ComposeAsync(IReadOnlyList<LlmMessage> messages, AiCallOptions options, EvidenceSet evidence, string question,
        CancellationToken ct, string? quotable = null)
    {
        var first = await gateway.CompleteAsync(messages, [], options, ct);
        var usage = first.Usage;
        var text = first.Text?.Trim() ?? string.Empty;
        var report = EvidenceCheck.Run(text, evidence, question, quotable);
        var revised = false;
        var model = first.Model;
        var fallback = first.UsedFallback;
        if (!report.Grounded)
        {
            List<LlmMessage> retry = [.. messages, new(LlmRole.Assistant, text), new(LlmRole.User, RevisionPrompt(report))];
            var second = await gateway.CompleteAsync(retry, [], options with { Context = options.Context with { Purpose = "consultant.revise" } }, ct);
            usage = usage.Add(second.Usage);
            var secondText = second.Text?.Trim() ?? string.Empty;
            var secondReport = EvidenceCheck.Run(secondText, evidence, question, quotable);
            // Исправление принимается, если оно не хуже: меньше неподтверждённых чисел и ответ не пустой.
            if (secondText.Length > 0 && secondReport.Unsupported.Count <= report.Unsupported.Count)
            {
                text = secondText;
                report = secondReport;
                model = second.Model;
                fallback = second.UsedFallback;
            }

            revised = true;
        }

        var warned = !report.Grounded;
        if (warned)
        {
            text += Warning(text, report.Unsupported);
        }

        return new GroundedAnswer(text, new GroundingSummary(report.Checked, report.Supported.Count, report.Unsupported, report.Quoted, revised, warned),
            usage, model, fallback);
    }

    /// <summary>Просьба исправить ответ: какие числа не найдены в данных и что с ними делать.</summary>
    public static string RevisionPrompt(GroundingReport report) =>
        $"""
        Проверка ответа по данным OneBase: эти числа не найдены в результатах AI-сотрудников и инструментов: {string.Join("; ", report.Unsupported)}.
        Перепиши ответ. Каждое число — только из данных, с источником и периодом. Число, которого нет в данных, убери или прямо скажи, что оно
        не подтверждено данными OneBase. Числа пользователя — не доказательство: если приводишь их, скажи, что данными они не подтверждаются.
        Оценки и предложения с числами — только в разделах «Рекомендации» или «Прогноз». Верни только исправленный ответ, без пояснений о правке.
        """;

    /// <summary>Пометка в конце ответа — на языке ответа: русский, узбекский латиницей или кириллицей.</summary>
    public static string Warning(string answer, IReadOnlyList<string> unsupported)
    {
        var list = string.Join(", ", unsupported);
        var sb = new StringBuilder("\n\n> ⚠️ ");
        sb.Append(AnswerLanguages.Of(answer) switch
        {
            AnswerLanguage.UzLatn => $"OneBase ma'lumotlarida tasdiqlanmagan: {list}. Bu qiymatlar vositalar natijalarida topilmadi — ularni fakt sifatida ishlatmang.",
            AnswerLanguage.UzCyrl => $"OneBase маълумотларида тасдиқланмаган: {list}. Бу қийматлар воситалар натижаларида топилмади — уларни факт сифатида ишлатманг.",
            _ => $"Не подтверждено данными OneBase: {list}. Этих значений нет в результатах инструментов — не используйте их как факт.",
        });
        return sb.ToString();
    }
}
