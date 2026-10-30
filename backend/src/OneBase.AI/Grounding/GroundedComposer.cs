using System.Text;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Monitoring;
using OneBase.AI.Providers;

namespace OneBase.AI.Grounding;

/// <summary>
/// Проверка ответа для журнала и разбора в чате: сколько чисел проверено, какие не подтверждены, исправлялся ли ответ (Revised) и пришлось ли
/// пометить неподтверждённое прямо в тексте (Warned). Revisions — сколько было исправлений; EscalatedTo — модель, на которую перешла
/// перепроверка (review); Causes — причины перепроверки (AiReviewCauses).
/// </summary>
public sealed record GroundingSummary(int Checked, int Supported, IReadOnlyList<string> Unsupported, IReadOnlyList<string> Quoted, bool Revised, bool Warned,
    int Revisions = 0, string? EscalatedTo = null, IReadOnlyList<string>? Causes = null);

public sealed record GroundedAnswer(string Text, GroundingSummary Grounding, AiTokenUsage Usage, AiModelRef Model, bool UsedFallback);

/// <summary>
/// Итоговый ответ с проверкой по данным: модель пишет ответ, код сверяет каждое число с доказательствами (EvidenceCheck). Неподтверждённые числа —
/// одна попытка исправить ответ с указанием, какие именно; если и после неё число не подтверждено — ответ не выдаёт его за факт: в конце
/// пометка «не подтверждено данными OneBase». Сравнивается с данными, а не с тем, что хочет услышать пользователь.
/// С планом перепроверки (review): причина — неподтверждённые числа или оборванный ответ; первое исправление — той же моделью, следующее —
/// моделью сложных ответов (один переход и только вверх); попытки — из общего счётчика ответа, на каждую нужен бюджет вопроса. Исправление
/// принимается, только если оно не хуже; проверка чисел остаётся у каждой попытки.
/// </summary>
public sealed class GroundedComposer(IAiGateway gateway)
{
    /// <summary>
    /// quotable — оспоренный прошлый ответ: его числа ответ может привести только с оговоркой, что они неверны или не подтверждены. draft — черновик
    /// по мере генерации (ConsultantDraft): текст, затем «проверяется»; при исправлении — «переписывается» и новый текст. Итог — только результат.
    /// review — план перепроверки (null — как раньше: одно исправление той же моделью).
    /// </summary>
    public async Task<GroundedAnswer> ComposeAsync(IReadOnlyList<LlmMessage> messages, AiCallOptions options, EvidenceSet evidence, string question,
        CancellationToken ct, string? quotable = null, Func<ConsultantDraft, Task>? draft = null, ReviewPlan? review = null)
    {
        if (draft is not null)
        {
            options = options with { Stream = new DraftStream(draft) };
        }

        var first = await gateway.CompleteAsync(messages, [], options, ct);
        if (draft is not null)
        {
            await draft(new ConsultantDraft(ConsultantDraft.Checking));
        }

        var usage = first.Usage;
        var text = first.Text?.Trim() ?? string.Empty;
        var report = Check(text, evidence, question, quotable);
        var incomplete = review is not null && Incomplete(first.StopReason);
        var model = first.Model;
        var fallback = first.UsedFallback;
        var revisions = 0;
        string? escalatedTo = null;
        var causes = new List<string>();
        var turn = AiTurnTimings.Current;

        while (!report.Grounded || incomplete)
        {
            var cause = incomplete ? AiReviewCauses.Incomplete : AiReviewCauses.UnsupportedNumbers;
            if (!causes.Contains(cause))
            {
                causes.Add(cause);
            }

            // Без плана — как раньше: одно исправление той же моделью. С планом — попытки, бюджет и один переход вверх.
            var attempt = options;
            var purpose = "consultant.revise";
            if (review is null)
            {
                if (revisions > 0)
                {
                    break;
                }
            }
            else
            {
                if (!review.BudgetLeft)
                {
                    turn?.Review(AiReviewCauses.Budget, "исправление ответа пропущено: бюджет вопроса на исходе");
                    break;
                }

                if (revisions > 0)
                {
                    // Та же модель уже исправляла — второй раз её не зовём: переход на модель сложных ответов или конец.
                    if (!review.TryEscalate())
                    {
                        break;
                    }

                    attempt = options with { Model = review.Escalation!.Model, ModelReason = review.Escalation.Reason, Fallback = null };
                    purpose = "consultant.review";
                    escalatedTo = review.Escalation.Model?.ToString();
                }

                if (!review.TryTake())
                {
                    turn?.Review(AiReviewCauses.Attempts, "исправление ответа пропущено: попытки перепроверки исчерпаны");
                    break;
                }
            }

            List<LlmMessage> retry = [.. messages, new(LlmRole.Assistant, text), new(LlmRole.User, incomplete ? IncompletePrompt : RevisionPrompt(report))];
            if (draft is not null)
            {
                await draft(new ConsultantDraft(ConsultantDraft.Revising)); // показанный черновик с неподтверждёнными числами убирается
            }

            var second = await gateway.CompleteAsync(retry, [], attempt with { Context = attempt.Context with { Purpose = purpose } }, ct);
            if (draft is not null)
            {
                await draft(new ConsultantDraft(ConsultantDraft.Checking));
            }

            revisions++;
            usage = usage.Add(second.Usage);
            var secondText = second.Text?.Trim() ?? string.Empty;
            var secondReport = Check(secondText, evidence, question, quotable);
            var secondIncomplete = review is not null && Incomplete(second.StopReason);
            turn?.Review(cause, revisions == 1 && purpose == "consultant.revise" ? "исправление той же моделью" : "исправление моделью сложных ответов",
                second.Model.ToString());

            // Исправление принимается, если оно не хуже: меньше неподтверждённых чисел и ответ не пустой (оборванный — только если и тот оборван).
            if (secondText.Length > 0 && secondReport.Unsupported.Count <= report.Unsupported.Count && (!secondIncomplete || incomplete))
            {
                text = secondText;
                report = secondReport;
                model = second.Model;
                fallback = second.UsedFallback;
                incomplete = secondIncomplete;
            }
        }

        var warned = !report.Grounded;
        if (warned)
        {
            text += Warning(text, report.Unsupported);
        }

        return new GroundedAnswer(text, new GroundingSummary(report.Checked, report.Supported.Count, report.Unsupported, report.Quoted, revisions > 0, warned,
            revisions, escalatedTo, causes.Count == 0 ? null : causes), usage, model, fallback);
    }

    private static GroundingReport Check(string text, EvidenceSet evidence, string question, string? quotable)
    {
        using (AiTurnTimings.Current?.Stage("evidence"))
        {
            return EvidenceCheck.Run(text, evidence, question, quotable);
        }
    }

    /// <summary>Ответ оборван лимитом длины — неполон.</summary>
    private static bool Incomplete(string? stopReason) => stopReason is "max_tokens" or "length";

    /// <summary>Просьба переписать оборванный ответ.</summary>
    public const string IncompletePrompt =
        "Ответ оборвался на середине (лимит длины). Напиши его заново целиком, но короче: главное, цифры по отделам с источниками и периодом, " +
        "рекомендации. Числа — только из данных AI-сотрудников. Верни только ответ.";

    /// <summary>Текст модели — в черновик; сброс попытки (повтор, резервная модель) — сброс черновика.</summary>
    private sealed class DraftStream(Func<ConsultantDraft, Task> draft) : IAiTextStream
    {
        public Task TextAsync(string text) => draft(new ConsultantDraft(ConsultantDraft.TextKind, text));

        public Task ResetAsync() => draft(new ConsultantDraft(ConsultantDraft.Reset));
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
