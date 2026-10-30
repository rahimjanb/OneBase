using System.Text.Json;
using System.Text.RegularExpressions;
using OneBase.AI.Consultant;

namespace OneBase.AI.Tests;

/// <summary>
/// Структура ответа консультанта для экрана: разделы «Главное», «Факты», «Отклонения», «Рекомендации» (по-русски и по-узбекски) — блоки
/// отчёта; цвет изменения — по смыслу показателя; плитки и полоса плана — из тех же цифр; простой ответ — Markdown; ни одна цифра не
/// теряется и не появляется из ниоткуда; контекст экрана — строкой к вопросу.
/// </summary>
public class AnswerStructureTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 30, 10, 36, 0, TimeSpan.FromHours(5));

    private const string SalesReport = """
        Вторичка за октябрь выполнила 76,4% плана РОП. По прогнозу месяц закроется на 80,2% — разрыв около 96 тыс. кг. Рост в большинстве регионов скрыт падением Ташкента и выпадением двух крупных клиентов.

        ## Факты
        - **Объём вторички:** 371 148 кг (−2,3% к полному сентябрю, +5,2% к тем же дням)
        - **Выручка:** 13 446 260 699 сум (−3,8% к полному сентябрю, +3,6% к тем же дням)
        - **План РОП:** 485 645 кг
        - **Прогноз на 31.10:** 389 321 кг
        - **Активные точки (АКБ):** 12 308 (−7,3% к сентябрю)
        - **Страйк:** 54,8% (−15,2 п.п. до цели 70%)
        - **Экспорт и опт («Завод»):** 91 285 кг (−19,0% к 118 161 кг)
        - Рост в большинстве регионов: Самарканд +12%, Бухара +8%

        ## Ключевые отклонения
        - **Урикзор и Сеть** — в сентябре 14 800 кг и 558,9 млн сум, в октябре закупок нет
        - **Ташкент** — основное падение среди регионов, −11,4% к сентябрю
        - **Активная клиентская база** — минус 970 точек к сентябрю
        - **Страйк** — 54,8% при цели 70%

        ## Анализ
        Снижение АКБ связано с тем, что агенты Ташкента сократили маршруты.

        ## Рекомендации
        1. **Вернуть Урикзор и Сеть** — встреча с закупщиками, условия отгрузки на ноябрь (ответственный: РОП)
        2. **Разобрать Ташкент по агентам** — агенты с падением больше 15%, план восстановления до 5.11 (ответственный: супервайзер Ташкента)
        3. **Поднять страйк** — фокус-SKU в маршрутах, мотивация за 5+ SKU в заказе
        """;

    private static ConsultantDetails Details(string agent = "sales", params DataSource[] sources) => new(
        [
            new AgentResult(agent, "Sales AI", "completed", "итог", ["ф1", "ф2", "ф3"], [new Metric("Факт", "371 148", "кг")], ["п1"], ["р1", "р2"], [], ["get_sales"]),
            new AgentResult("marketing", "Marketing AI", "forbidden", "нет доступа", [], [], [], [], [], []),
        ],
        sources, "claude-haiku-5-5", false);

    private static T Single<T>(StructuredAnswer a) where T : AnswerBlock => Assert.Single(a.Blocks.OfType<T>());

    /// <summary>JSON без экранирования кириллицы (\uXXXX) — чтобы искать в нём числа ответа.</summary>
    private static readonly JsonSerializerOptions Readable = new(ConsultantChatService.Json) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [Fact]
    public void Sales_report_becomes_summary_with_progress_kpis_facts_deviations_and_recommendations_in_order()
    {
        var a = AnswerStructure.Build(SalesReport, Details(), 3300, At);

        Assert.True(a.Report);
        Assert.Equal(["summary", "kpis", "facts", "deviations", "markdown", "recommendations"], a.Blocks.Select(TypeOf));

        var summary = Single<SummaryBlock>(a);
        Assert.StartsWith("Вторичка за октябрь выполнила 76,4% плана РОП.", summary.Text);
        Assert.Equal(new AnswerProgress("Объём вторички", 371_148, 485_645, 389_321, "кг", "Прогноз на 31.10"), summary.Progress);

        // Плитки — главные показатели из фактов, большие суммы короче, изменение и его цвет — как у факта.
        var kpis = Single<KpisBlock>(a).Items;
        Assert.Equal(["Объём вторички", "Выручка", "Активные точки (АКБ)", "Страйк"], kpis.Select(k => k.Label));
        Assert.Equal(("13,45 млрд сум", "−3,8% к полному сентябрю", "bad"), (kpis[1].Value, kpis[1].Delta, kpis[1].Tone));
        Assert.Equal(("54,8%", "−15,2 п.п. до цели 70%", "warn"), (kpis[3].Value, kpis[3].Delta, kpis[3].Tone));

        var rows = Single<FactsBlock>(a).Rows;
        var volume = rows[0];
        Assert.Equal(("Объём вторички", "371 148 кг"), (volume.Label, volume.Value));
        Assert.Equal([new FactDelta("−2,3%", "к полному сентябрю", "bad"), new FactDelta("+5,2%", "к тем же дням", "good")], volume.Deltas);
        Assert.Equal(new FactDelta("−19,0%", "к 118 161 кг", "bad"), rows.Single(r => r.Label.StartsWith("Экспорт")).Deltas.Single());
        Assert.Equal("91 285 кг", rows.Single(r => r.Label.StartsWith("Экспорт")).Value);
        // Изменения разных регионов не отрываются от названий: значение — текстом целиком.
        var growth = rows.Single(r => r.Label == "Рост в большинстве регионов");
        Assert.Equal(("Самарканд +12%, Бухара +8%", 0), (growth.Value, growth.Deltas.Count));

        var deviations = Single<DeviationsBlock>(a).Items;
        Assert.Equal(["Урикзор и Сеть", "Ташкент", "Активная клиентская база", "Страйк"], deviations.Select(d => d.Title));
        Assert.Equal(["high", "high", "medium", "medium"], deviations.Select(d => d.Severity));
        Assert.Equal("в сентябре 14 800 кг и 558,9 млн сум, в октябре закупок нет", deviations[0].Text);

        Assert.Equal("## Анализ\n\nСнижение АКБ связано с тем, что агенты Ташкента сократили маршруты.", Assert.Single(a.Blocks.OfType<MarkdownBlock>()).Text);

        var recommendations = Single<RecommendationsBlock>(a);
        Assert.Equal(3, recommendations.Total);
        Assert.Equal(new RecommendationItem("Вернуть Урикзор и Сеть", "встреча с закупщиками, условия отгрузки на ноябрь", "РОП", null, "sales"), recommendations.Items[0]);
        Assert.Equal(("супервайзер Ташкента", "2026-11-05", "sales"), (recommendations.Items[1].Owner, recommendations.Items[1].DueDate, recommendations.Items[1].Department));
        // Без ответственного — отдел ответившего AI-сотрудника.
        Assert.Equal((null, "sales"), (recommendations.Items[2].Owner, recommendations.Items[2].Department));

        Assert.Equal(["Подробнее: Урикзор и Сеть", "Подробнее: Ташкент", "Покажи по регионам", "Сравни с прошлым годом"], a.FollowUps);
    }

    [Fact]
    public void Meta_counts_departments_facts_recommendations_and_takes_the_latest_data_date()
    {
        var details = Details("sales", new DataSource("OneBase → Продажи", "октябрь 2026, данные по 29.10.2026"), new DataSource("OneBase → Склад", "на 30.10.2026"));

        var meta = AnswerStructure.Build(SalesReport, details, 3300, At).Meta;

        // Сотрудник без доступа не считается подразделением ответа.
        Assert.Equal(new AnswerMeta(1, 4, 2, "claude-haiku-5-5", 3300, "2026-10-30"), meta);
    }

    [Fact]
    public void Facts_table_with_change_columns_gets_captions_from_headers_and_tones_by_meaning()
    {
        const string text = """
            Октябрь в целом слабее сентября: выручка снизилась, а просроченная дебиторка выросла. Ниже — ключевые цифры месяца и что с ними делать дальше.

            ### Факты
            | Показатель | Октябрь | Изменение к сентябрю |
            |---|---|---|
            | Выручка | 13 446 260 699 сум | −3,8% |
            | Объём, кг | 371 148 | +5,2% |
            | Просроченная дебиторка | 1 200 000 000 сум | +12,0% |
            | Опоздания | 41 | −8% |

            ### Рекомендации
            - Взыскать долг с трёх крупнейших дилеров
            """;

        var a = AnswerStructure.Build(text, null, 1000, At);

        var rows = Single<FactsBlock>(a).Rows;
        Assert.Equal(["Выручка", "Объём, кг", "Просроченная дебиторка", "Опоздания"], rows.Select(r => r.Label));
        Assert.Equal(["bad", "good", "bad", "good"], rows.Select(r => r.Deltas.Single().Tone)); // рост просрочки — плохо, меньше опозданий — хорошо
        Assert.All(rows, r => Assert.Equal("к сентябрю", r.Deltas.Single().Caption));
        Assert.Equal("13 446 260 699 сум", rows[0].Value);
        Assert.Equal(new RecommendationItem("Взыскать долг с трёх крупнейших дилеров", null, null, null, null), Single<RecommendationsBlock>(a).Items.Single());
    }

    [Fact]
    public void Comparison_table_stays_a_markdown_table()
    {
        const string text = """
            Регионы по выполнению плана за октябрь: три региона отстают, остальные идут по плану или выше. Подробности — в таблице.

            ## Факты
            | Регион | Факт, кг | План, кг | Выполнение |
            |---|---|---|---|
            | Ташкент | 80 000 | 120 000 | 66,7% |
            | Самарканд | 60 000 | 58 000 | 103,4% |
            """;

        var a = AnswerStructure.Build(text, null, 1000, At);

        Assert.True(a.Report);
        Assert.Empty(a.Blocks.OfType<FactsBlock>());
        Assert.Contains(a.Blocks.OfType<MarkdownBlock>(), b => b.Text.Contains("| Ташкент | 80 000 | 120 000 | 66,7% |"));
    }

    [Theory]
    [InlineData("Здравствуйте! Чем могу помочь по данным OneBase?")]
    [InlineData("Модель вернула пустой ответ.")]
    public void Short_answers_are_plain_markdown(string text)
    {
        var a = AnswerStructure.Build(text, null, 900, At);

        Assert.False(a.Report);
        Assert.Equal(text, Assert.IsType<MarkdownBlock>(Assert.Single(a.Blocks)).Text);
        Assert.Empty(a.FollowUps);
    }

    [Fact]
    public void Long_answer_without_known_sections_is_plain_markdown()
    {
        var text = string.Join("\n\n", Enumerable.Repeat("Продажи в октябре в целом стабильны, отклонения небольшие и объясняются сезонностью спроса.", 6));

        var a = AnswerStructure.Build(text, null, 900, At);

        Assert.False(a.Report);
        Assert.Equal(text, Assert.IsType<MarkdownBlock>(Assert.Single(a.Blocks)).Text);
    }

    [Fact]
    public void Answer_title_heading_is_kept_and_its_first_paragraph_is_the_summary()
    {
        const string text = """
            # Состояние продаж за октябрь

            Октябрь идёт ниже плана: 76,4% выполнения при прогнозе 80,2% к концу месяца, главный вклад в отставание — Ташкент.

            **Факты:**
            - Выручка: 13 446 260 699 сум
            - Объём: 371 148 кг

            **Что сделать:**
            - Разобрать Ташкент по агентам
            """;

        var a = AnswerStructure.Build(text, null, 900, At);

        Assert.Equal(["markdown", "summary", "kpis", "facts", "recommendations"], a.Blocks.Select(TypeOf));
        Assert.Equal("# Состояние продаж за октябрь", ((MarkdownBlock)a.Blocks[0]).Text);
        Assert.StartsWith("Октябрь идёт ниже плана", Single<SummaryBlock>(a).Text);
        Assert.Null(Single<SummaryBlock>(a).Progress);
    }

    [Fact]
    public void Uzbek_answer_sections_and_follow_ups_are_in_uzbek()
    {
        const string text = """
            Oktyabr oyida sotuv rejasi 76,4% bajarildi, prognoz bo'yicha oy 80,2% bilan yopiladi. Asosiy pasayish Toshkentda.

            ## Faktlar
            - **Tushum:** 13 446 260 699 so'm
            - **Hajm:** 371 148 kg (+5,2% shu kunlarga nisbatan)

            ## Muammolar
            - **Toshkent** — sotuv tushib ketdi

            ## Tavsiyalar
            1. **Toshkentni agentlar bo'yicha tahlil qilish** — pasayish 15% dan katta agentlar
            """;

        var a = AnswerStructure.Build(text, Details(), 900, At);

        Assert.True(a.Report);
        Assert.Equal(["Tushum", "Hajm"], Single<FactsBlock>(a).Rows.Select(r => r.Label));
        Assert.Equal("13,45 mlrd so'm", Single<KpisBlock>(a).Items[0].Value);
        Assert.Equal("high", Single<DeviationsBlock>(a).Items.Single().Severity);
        Assert.Equal(["Batafsil: Toshkent", "Hududlar bo'yicha ko'rsat", "O'tgan yil bilan solishtir"], a.FollowUps);
    }

    [Fact]
    public void No_number_of_the_answer_is_lost_and_none_is_invented()
    {
        var a = AnswerStructure.Build(SalesReport, Details(), 3300, At);
        var json = JsonSerializer.Serialize(a, Readable);

        // Каждое число ответа есть в блоках (плитки сокращают суммы, но факт остаётся в таблице фактов).
        foreach (Match m in Regex.Matches(SalesReport, @"\d[\d ]*(?:,\d+)?"))
        {
            Assert.Contains(m.Value.Trim(), json);
        }

        // В блоках нет чисел, которых нет в ответе, кроме сокращённых сумм плиток и полосы плана (те же числа без пробелов).
        var source = SalesReport.Replace(" ", "");
        foreach (var block in a.Blocks.Where(b => b is not KpisBlock and not SummaryBlock))
        {
            // Срок рекомендации — дата ISO из «до 5.11»: её числа не в тексте ответа.
            var serialized = Regex.Replace(JsonSerializer.Serialize(block, Readable), @"\d{4}-\d{2}-\d{2}", "");
            foreach (Match m in Regex.Matches(serialized, @"\d[\d ]*(?:,\d+)?"))
            {
                Assert.Contains(m.Value.Replace(" ", ""), source);
            }
        }
    }

    [Fact]
    public void Blocks_serialize_with_a_type_field_for_the_screen()
    {
        var json = JsonSerializer.Serialize(AnswerStructure.Build(SalesReport, Details(), 3300, At), ConsultantChatService.Json);
        var root = JsonDocument.Parse(json).RootElement;

        Assert.True(root.GetProperty("report").GetBoolean());
        Assert.Equal("summary", root.GetProperty("blocks")[0].GetProperty("type").GetString());
        Assert.Equal(485_645, root.GetProperty("blocks")[0].GetProperty("progress").GetProperty("plan").GetDecimal());
        Assert.Equal("good", root.GetProperty("blocks")[2].GetProperty("rows")[0].GetProperty("deltas")[1].GetProperty("tone").GetString());
        Assert.Equal(4, root.GetProperty("followUps").GetArrayLength());
        Assert.Equal(3300, root.GetProperty("meta").GetProperty("latencyMs").GetInt64());
    }

    [Fact]
    public void Speech_of_a_report_reads_the_summary_and_recommendations_only()
    {
        var a = AnswerStructure.Build(SalesReport, Details(), 3300, At);

        var speech = AnswerStructure.SpeechText(a, SalesReport);

        Assert.StartsWith("Вторичка за октябрь выполнила 76,4% плана РОП.", speech);
        Assert.Contains("Что сделать в первую очередь:\n1. Вернуть Урикзор и Сеть. встреча с закупщиками, условия отгрузки на ноябрь.", speech.Replace("\r", ""));
        Assert.DoesNotContain("13 446 260 699", speech); // таблицы фактов не зачитываются
        Assert.Equal("Привет!", AnswerStructure.SpeechText(AnswerStructure.Build("Привет!", null, 1, At), "Привет!"));
    }

    [Theory]
    [InlineData("+5,2%", null, "Выручка", "good")]
    [InlineData("−3,8%", "к сентябрю", "Выручка", "bad")]
    [InlineData("+12%", null, "Просроченная дебиторка", "bad")]
    [InlineData("−8%", null, "Опоздания", "good")]
    [InlineData("−15,2 п.п.", "до цели 70%", "Страйк", "warn")]
    [InlineData("−2%", "к плану", "Объём", "warn")]
    [InlineData("+0,0%", null, "Выручка", "neutral")]
    public void Tone_follows_the_meaning_of_the_metric(string delta, string? caption, string label, string tone) =>
        Assert.Equal(tone, AnswerStructure.Tone(delta, caption, label));

    [Theory]
    [InlineData("13 446 260 699 сум", "13,45 млрд сум")]
    [InlineData("558 900 000 сум", "558,9 млн сум")]
    [InlineData("371 148 кг", "371 148 кг")]
    [InlineData("54,8%", "54,8%")]
    [InlineData("2 000 000 000 so'm", "2 mlrd so'm")]
    public void Large_amounts_are_compact_on_tiles(string value, string compact) => Assert.Equal(compact, AnswerStructure.Compact(value));

    [Fact]
    public void Screen_context_is_added_to_the_question_for_the_consultant_only()
    {
        Assert.Equal("Как продажи?", ConsultantChatService.WithContext("Как продажи?", null));
        Assert.Equal("Как продажи?", ConsultantChatService.WithContext("Как продажи?", new ChatContext(null, null, null)));
        Assert.Equal(
            "Как продажи?\n\n(Контекст экрана, если в вопросе не сказано иное: период — октябрь 2026 (01.10.2026 — 31.10.2026); отдел — Продажи.)",
            ConsultantChatService.WithContext("Как продажи?", new ChatContext("2026-10", "sales", "Продажи")));
        Assert.Equal(
            "Сколько опозданий?\n\n(Контекст экрана, если в вопросе не сказано иное: период — февраль 2028 (01.02.2028 — 29.02.2028).)",
            ConsultantChatService.WithContext("Сколько опозданий?", new ChatContext("2028-02", null, null)));
        // Неверный период не подставляется.
        Assert.Equal("Как продажи?", ConsultantChatService.WithContext("Как продажи?", new ChatContext("октябрь", null, null)));
    }

    private static string TypeOf(AnswerBlock b) => b switch
    {
        SummaryBlock => "summary",
        KpisBlock => "kpis",
        FactsBlock => "facts",
        DeviationsBlock => "deviations",
        RecommendationsBlock => "recommendations",
        _ => "markdown",
    };
}
