using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Grounding;
using OneBase.AI.Llm;
using OneBase.AI.Orchestration;
using OneBase.AI.Providers;
using OneBase.Application.DataSources;

namespace OneBase.AI.Tests;

/// <summary>Механизмы ответа по данным: числа в тексте, доказательства, Evidence Check, точный расчёт, распознавание возражения, источник истины.</summary>
public class GroundingTests
{
    private static EvidenceSet Evidence(string json)
    {
        var e = new EvidenceSet();
        e.AddContent(json);
        return e;
    }

    /// <summary>Утверждения — суммы, проценты, вес; даты, время, год, «15-е», номера пунктов и малые целые без единицы — нет.</summary>
    [Fact]
    public void Numbers_in_text_are_claims_dates_years_and_list_numbers_are_not()
    {
        var claims = NumberClaims.Find("""
            1. Продажи за сентябрь 2026 (по 05.10, на 14:30, с 15-е): 1,2 млрд сум, рост 12,5%, 235 211 кг, 3 региона.
            2. Дилер «Помадка 2.5 кг» — 48 точек.
            """);

        Assert.Equal(["1,2 млрд сум", "12,5%", "235 211 кг", "2.5 кг", "48"], claims.Select(c => c.Text));
        var billion = claims[0];
        Assert.Equal(1_200_000_000m, billion.Value);
        Assert.Equal(50_000_000m, billion.Tolerance); // показан один знак после запятой: 1,15–1,25 млрд
        Assert.True(claims[1].Percent);
        Assert.Equal(235_211m, claims[2].Value);
    }

    /// <summary>Разделы «Рекомендации» и «Прогноз» — предложения и оценки, а не факты: их числа не проверяются.</summary>
    [Fact]
    public void Recommendation_and_forecast_sections_are_not_checked()
    {
        var claims = NumberClaims.Checkable("""
            **Факты**
            Продажи — 80 млн сум.
            **Рекомендации**
            Поднять АКБ на 15% и выйти на 120 млн сум.
            ### Прогноз
            К концу месяца — около 95 млн сум.
            """);

        Assert.Equal(["80 млн сум"], claims.Select(c => c.Text));
    }

    /// <summary>Доказательства — числа JSON инструмента, числа в строках, длины списков; доли — и в процентах; округление автора допускается.</summary>
    [Fact]
    public void Evidence_supports_rounded_numbers_shares_as_percent_and_list_lengths()
    {
        var e = Evidence("""{"revenue":1234567890.4,"growth":0.125,"name":"Помадка 230 гр","regions":[{"n":"a"},{"n":"b"},{"n":"c"},{"n":"d"},{"n":"e"},{"n":"f"},{"n":"g"},{"n":"h"},{"n":"i"},{"n":"j"},{"n":"k"},{"n":"l"}]}""");

        bool Supported(string text) => NumberClaims.Find(text, keepSmall: true).All(e.Supports);
        Assert.True(Supported("1,2 млрд сум"));
        Assert.True(Supported("1 234 567 890 сум"));
        Assert.True(Supported("12,5%"));
        Assert.True(Supported("230 гр"));
        Assert.True(Supported("12 регионов"));
        Assert.False(Supported("1,3 млрд сум"));
        Assert.False(Supported("90 млн сум"));
    }

    /// <summary>Число пользователя — не доказательство: с оговоркой («не подтверждается») — цитата, без неё — неподтверждённый факт.</summary>
    [Fact]
    public void User_number_passes_only_with_a_reservation()
    {
        var e = Evidence("""{"factSum":80000000}""");
        const string question = "Ты ошибся, продажи 90 млн сум.";

        var refuted = EvidenceCheck.Run("По данным Linko продажи — 80 млн сум. Значение 90 млн сум текущими данными не подтверждается.", e, question);
        Assert.True(refuted.Grounded);
        Assert.Equal(["80 млн сум"], refuted.Supported);
        Assert.Equal(["90 млн сум"], refuted.Quoted);

        var agreed = EvidenceCheck.Run("Вы правы, продажи составили 90 млн сум.", e, question);
        Assert.False(agreed.Grounded);
        Assert.Equal(["90 млн сум"], agreed.Unsupported);
    }

    /// <summary>Точный расчёт: выражение считает код (скобки, проценты, запятая), ошибки — понятным текстом, не исключением наружу.</summary>
    [Fact]
    public void Calculator_evaluates_expressions_and_reports_errors()
    {
        Assert.Equal(14.285714m, Calculator.Evaluate("(80-70)/70*100"));
        Assert.Equal(3m, Calculator.Evaluate("1,5 × 2"));
        Assert.Equal(100m, Calculator.Evaluate("50% * 200"));
        Assert.Equal(-5m, Calculator.Evaluate("-(2+3)"));
        Assert.Throws<FormatException>(() => Calculator.Evaluate("1/0"));
        Assert.Throws<FormatException>(() => Calculator.Evaluate("2+"));
        Assert.Throws<FormatException>(() => Calculator.Evaluate("продажи*2"));

        var (content, value) = Calculator.Run(System.Text.Json.JsonSerializer.SerializeToElement(new { expression = "80000000/100000000*100", label = "выполнение, %" }));
        Assert.Equal(80m, value);
        Assert.Contains("\"result\":80", content);
        Assert.Null(Calculator.Run(System.Text.Json.JsonSerializer.SerializeToElement(new { expression = "(" })).Value);
    }

    /// <summary>Возражение — по-русски и по-узбекски, только к уже данному ответу; настойчивость считается (какое возражение подряд).</summary>
    [Fact]
    public void Disputes_are_detected_and_counted()
    {
        List<LlmMessage> history =
        [
            new(LlmRole.User, "Сколько продаж в сентябре?"),
            new(LlmRole.Assistant, "Продажи за сентябрь — 80 млн сум."),
            new(LlmRole.User, "Ты ошибся, там 90 млн"),
            new(LlmRole.Assistant, "Перепроверил: 80 млн сум."),
        ];

        Assert.True(CorrectionGuard.IsDispute("Это неправильно"));
        Assert.True(CorrectionGuard.IsDispute("Javob xato, qayta tekshir"));
        Assert.True(CorrectionGuard.IsDispute("Передумай"));
        Assert.False(CorrectionGuard.IsDispute("А по Самарканду?"));
        Assert.Null(CorrectionGuard.Detect("Ты ошибся", [])); // ответа ещё не было
        Assert.Null(CorrectionGuard.Detect("Спасибо", history));

        var request = CorrectionGuard.Detect("Нет, исправь! Там 90 млн", history)!;
        Assert.Equal(2, request.Attempt);
        Assert.Equal("Перепроверил: 80 млн сум.", request.PreviousAnswer);
    }

    /// <summary>Поиск показателя в карте источников: по ключу и по словам названия; не найден — null (источник не выбирается наугад).</summary>
    [Fact]
    public void Metrics_are_found_by_key_and_words()
    {
        List<TruthMetric> map =
        [
            new("linko_sales", "Факт продаж и возвратов", TruthOwners.Linko, "Linko", ""),
            new("plan_rop", "План РОП", TruthOwners.GoogleSheets, "Google Sheets «План»", ""),
            new("dealer_payments", "Оплаты дилеров", TruthOwners.GoogleSheets, "Google Sheets", ""),
        ];

        Assert.Equal("plan_rop", SourceOfTruthService.Find(map, "plan_rop")?.Key);
        Assert.Equal("plan_rop", SourceOfTruthService.Find(map, "планы РОП")?.Key);
        Assert.Equal("dealer_payments", SourceOfTruthService.Find(map, "оплаты дилеров")?.Key);
        Assert.Equal("linko_sales", SourceOfTruthService.Find(map, "факт продаж")?.Key);
        Assert.Null(SourceOfTruthService.Find(map, "бюджет маркетинга"));
    }

    private static AgentConfig Agent(string code) => new(code, code + " AI", "роль", "описание", "prompt", true, null, null, null, true, 1, code, null, []);

    /// <summary>
    /// Data Retrieval: маршрутизатор не привлёк сотрудников, а вопрос — о данных: сотрудники по словам вопроса. Возражение — перепроверяют сотрудники
    /// области прошлого вопроса; реплика без вопроса о данных — без сотрудников.
    /// </summary>
    [Fact]
    public void Questions_about_data_and_disputes_always_reach_the_data()
    {
        IReadOnlyList<AgentConfig> available = [Agent("sales"), Agent("finance"), Agent("supply")];
        var empty = new RoutingDecision([], new Dictionary<string, string>(), "", false, AiTokenUsage.None);
        List<LlmMessage> history = [new(LlmRole.User, "Какие остатки на складе Хоразма?"), new(LlmRole.Assistant, "Остаток — 12 400 кг.")];
        ConsultantTurn Turn(string q, IReadOnlyList<LlmMessage>? h = null) => new(Guid.NewGuid(), Guid.NewGuid(), q, h ?? []);

        Assert.Equal(["sales"], OrchestratedConsultantEngine.Enforce(empty, Turn("Сколько продаж в сентябре?"), null, available).Agents);
        Assert.Empty(OrchestratedConsultantEngine.Enforce(empty, Turn("Спасибо, план понятен"), null, available).Agents);

        var dispute = Turn("Ты ошибся", history);
        var request = CorrectionGuard.Detect(dispute.Question, dispute.History);
        Assert.Equal(["supply"], OrchestratedConsultantEngine.Enforce(empty, dispute, request, available).Agents);
    }
}
