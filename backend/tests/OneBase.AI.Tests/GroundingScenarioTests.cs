using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.Application.DataSources;

namespace OneBase.AI.Tests;

/// <summary>
/// Пять сценариев политики ответов по данным OneBase. Модель — подставная (отвечает по сценарию, в том числе «поддакивает»), механизмы — настоящие:
/// перепроверка после возражения (CorrectionGuard), сверка чисел ответа с данными и повторный запрос (GroundedComposer), правило приоритета
/// источников (SourceOfTruth). Данные — результат инструмента продаж: факт Linko за сентябрь 80 млн сум, план 100 млн сум.
/// </summary>
public class GroundingScenarioTests
{
    private const string SalesTool = """{"region":"Хоразм","period":"2026-09","source":"Linko","factSum":80000000,"planSum":100000000,"executionPct":80}""";

    private static readonly LlmMessage Question = new(LlmRole.User, "Сколько продаж по Хоразму за сентябрь?");
    private static readonly LlmMessage RightAnswer = new(LlmRole.Assistant, "По данным Linko продажи за сентябрь — 80 млн сум при плане 100 млн сум, выполнение 80%.");

    private static EvidenceSet Data(string? toolResult = SalesTool)
    {
        var evidence = new EvidenceSet();
        evidence.AddContent(toolResult);
        return evidence;
    }

    /// <summary>Ответ консультанта с перепроверкой: вердикт — по данным, итоговый текст — через проверку чисел.</summary>
    private static async Task<(GroundedAnswer Answer, CorrectionReview? Review, ScriptedModel Model)> AskAsync(
        IReadOnlyList<LlmMessage> history, string question, EvidenceSet evidence, params string[] replies)
    {
        var request = CorrectionGuard.Detect(question, history);
        var review = request is null ? null : CorrectionGuard.Review(request, evidence);
        var system = "Ты — консультант OneBase." + (review is null ? "" : "\n" + CorrectionGuard.ComposeBlock(review));
        var model = new ScriptedModel(replies);
        var answer = await new GroundedComposer(model).ComposeAsync([new(LlmRole.System, system), .. history, new(LlmRole.User, question)],
            new AiCallOptions(), evidence, question, CancellationToken.None, request?.PreviousAnswer);
        return (answer, review, model);
    }

    /// <summary>1. Пользователь оспаривает верный ответ — вывод сохраняется: данные подтверждают 80 млн, «поддакивание» 90 млн отклонено.</summary>
    [Fact]
    public async Task Disputed_correct_answer_is_kept_after_recheck()
    {
        var (answer, review, model) = await AskAsync([Question, RightAnswer], "Ты неправильно ответил. Продажи составляют 90 млн сум.", Data(),
            "Вы правы, приношу извинения: продажи за сентябрь составили 90 млн сум.",
            "По данным Linko продажи за сентябрь — 80 млн сум, план 100 млн сум, выполнение 80%. Значение 90 млн сум текущими данными не подтверждается: " +
            "если есть другой отчёт или выгрузка, укажите источник — я проверю расхождение.");

        Assert.Equal(CorrectionGuard.Confirmed, review!.Verdict);
        Assert.Contains("80 млн сум", review.PreviousConfirmed);
        Assert.Equal(["90 млн сум"], review.UserNotConfirmed);
        Assert.Contains("ПОДТВЕРЖДЁН", model.Calls[0][0].Content); // консультант знает итог перепроверки
        Assert.Contains("90 млн сум", model.Calls[1][^1].Content); // повторный запрос называет неподтверждённое число
        Assert.True(answer.Grounding.Revised);
        Assert.False(answer.Grounding.Warned);
        Assert.Equal(["90 млн сум"], answer.Grounding.Quoted);
        Assert.StartsWith("По данным Linko продажи за сентябрь — 80 млн сум", answer.Text);
    }

    /// <summary>2. Пользователь оспаривает неверный ответ (было 70 млн) — данные подтверждают его: ответ исправлен и сказано, что было неверно.</summary>
    [Fact]
    public async Task Disputed_wrong_answer_is_corrected()
    {
        var wrong = new LlmMessage(LlmRole.Assistant, "Продажи за сентябрь — 70 млн сум.");
        var (answer, review, model) = await AskAsync([Question, wrong], "Неверно, перепроверь: там 80 млн.", Data(),
            "В прошлом ответе была ошибка: 70 млн сум — неверно. По данным Linko продажи за сентябрь — 80 млн сум при плане 100 млн сум.");

        Assert.Equal(CorrectionGuard.Corrected, review!.Verdict);
        Assert.Equal(["70 млн сум"], review.PreviousNotConfirmed);
        Assert.Equal(["80 млн"], review.UserConfirmed);
        Assert.Contains("ЕСТЬ ОШИБКА", model.Calls[0][0].Content);
        Assert.Single(model.Calls); // ответ сразу подтверждён данными — повторный запрос не нужен
        Assert.True(answer.Grounding is { Revised: false, Warned: false });
        Assert.Contains("80 млн сум", answer.Text);
    }

    /// <summary>3. Данных нет — ответ не придумывается: выдуманное число отклонено, а если модель настаивает на нём — помечено как неподтверждённое.</summary>
    [Fact]
    public async Task Missing_data_is_not_invented()
    {
        var noData = Data("""{"region":"Нукус","period":"2026-03","rows":[]}""");
        var question = "Сколько продаж по Нукусу за март?";

        var (honest, _, model) = await AskAsync([], question, noData,
            "Продажи по Нукусу за март — 45 млн сум.",
            "В доступных данных OneBase не найдено подтверждение этой информации: продаж по Нукусу за март в данных нет.");
        Assert.Equal(2, model.Calls.Count);
        Assert.DoesNotContain("45", honest.Text);
        Assert.False(honest.Grounding.Warned);

        var (stubborn, _, _) = await AskAsync([], question, noData, "Продажи — примерно 45 млн сум.", "Продажи по Нукусу за март — около 45 млн сум.");
        Assert.True(stubborn.Grounding.Warned);
        Assert.Equal(["45 млн сум"], stubborn.Grounding.Unsupported);
        Assert.Contains("Не подтверждено данными OneBase: 45 млн сум", stubborn.Text);
    }

    /// <summary>
    /// 4. Google Sheets и Linko расходятся — побеждает источник-владелец: факт продаж — Linko (таблица не перезаписывает его), план, который ведёт
    /// привязка, — таблица; без владельца значение не выбирается.
    /// </summary>
    [Fact]
    public void Source_priority_decides_between_sheets_and_linko()
    {
        var fact = SourceOfTruth.Resolve(TruthOwners.Linko, [new(TruthOwners.GoogleSheets, 90_000_000m), new(TruthOwners.Linko, 80_000_000m)]);
        Assert.Equal("resolved", fact.Status);
        Assert.Equal(80_000_000m, fact.Value!.Value);
        Assert.Equal([TruthOwners.GoogleSheets], fact.Conflicts.Select(c => c.Source));
        Assert.Contains("конфликт", fact.Explanation);

        var plan = DataSourceCatalog.Field(DataSourceCatalog.PlanRop)!;
        Assert.Equal(TruthOwners.GoogleSheets, SourceOfTruth.FieldOwner(plan, bound: true));
        Assert.Equal(TruthOwners.OneBase, SourceOfTruth.FieldOwner(plan, bound: false));
        var boundPlan = SourceOfTruth.Resolve(SourceOfTruth.FieldOwner(plan, bound: true),
            [new(TruthOwners.OneBase, 95_000_000m), new(TruthOwners.GoogleSheets, 100_000_000m)]);
        Assert.Equal(100_000_000m, boundPlan.Value!.Value);

        // Совпадение в пределах округления — не конфликт; одна и та же запись не суммируется.
        Assert.Empty(SourceOfTruth.Resolve(TruthOwners.Linko, [new(TruthOwners.Linko, 80_000_000m), new(TruthOwners.GoogleSheets, 80_000_000.4m)]).Conflicts);

        var undefined = SourceOfTruth.Resolve(null, [new(TruthOwners.GoogleSheets, 90m), new(TruthOwners.Linko, 80m)]);
        Assert.Equal("undefined", undefined.Status);
        Assert.Null(undefined.Value);

        var missing = SourceOfTruth.Resolve(TruthOwners.Linko, [new(TruthOwners.GoogleSheets, 90m)]);
        Assert.Equal("owner_missing", missing.Status);
        Assert.Null(missing.Value);
    }

    /// <summary>5. Пользователь настаивает (третье возражение подряд) без новых данных — факты не меняются: снова 80 млн, 90 млн не принят.</summary>
    [Fact]
    public async Task Insisting_does_not_change_facts()
    {
        List<LlmMessage> history =
        [
            Question, RightAnswer,
            new(LlmRole.User, "Ты ошибся, 90 млн сум."),
            new(LlmRole.Assistant, "Перепроверил: по данным Linko — 80 млн сум; 90 млн сум данными не подтверждается."),
            new(LlmRole.User, "Нет, 90! Передумай!"),
            new(LlmRole.Assistant, "Данные те же: 80 млн сум."),
        ];

        var (answer, review, model) = await AskAsync(history, "Исправь! Там 90 млн сум, я точно знаю.", Data(),
            "Хорошо, исправляю: продажи за сентябрь — 90 млн сум.",
            "Данные OneBase не изменились: по Linko продажи за сентябрь — 80 млн сум. Значение 90 млн сум не подтверждается данными; " +
            "пришлите отчёт или выгрузку с ним — я проверю источник.");

        Assert.Equal(3, review!.Attempt);
        Assert.Equal(CorrectionGuard.Confirmed, review.Verdict);
        Assert.Contains("3-й раз", model.Calls[0][0].Content);
        Assert.Contains("настойчивость не доказательство", model.Calls[0][0].Content);
        Assert.True(answer.Grounding.Revised);
        Assert.False(answer.Grounding.Warned);
        Assert.Contains("80 млн сум", answer.Text);
        Assert.DoesNotContain("исправляю", answer.Text);
    }

    /// <summary>Подставная модель: ответы по сценарию по очереди, запись всех запросов.</summary>
    private sealed class ScriptedModel(params string[] replies) : IAiGateway
    {
        private readonly Queue<string> _replies = new(replies);

        public List<IReadOnlyList<LlmMessage>> Calls { get; } = [];

        public Task<AiReadiness> GetReadinessAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AiReadiness(true, null));

        public Task<AiCallResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmToolDefinition> tools, AiCallOptions options,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(messages);
            var text = _replies.Count > 0 ? _replies.Dequeue() : throw new InvalidOperationException("Сценарий модели закончился.");
            return Task.FromResult(new AiCallResult(text, [], new AiTokenUsage(10, 5), new AiModelRef("fake", "consultant"), false, 1, "stop"));
        }

        public Task<(IReadOnlyList<float[]> Vectors, AiModelRef Model)> EmbedAsync(IReadOnlyList<string> inputs, AiCallContext context,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
