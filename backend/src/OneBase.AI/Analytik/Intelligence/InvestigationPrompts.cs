using System.Text;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Semantic;

namespace OneBase.AI.Analytik.Intelligence;

/// <summary>
/// Промпты ролей исследования. Системные части неизменны (кэш промпта): текущая дата, вопрос и состояние — только в сообщении пользователя.
/// </summary>
internal static class InvestigationPrompts
{
    private static readonly string Methods = string.Join("; ", ComputeCatalog.Methods.Select(m =>
        $"{m.Code} ({m.Title}{(m.NeedsDimension ? ", dimension" : "")}{(m.NeedsCompare ? ", compare_from/compare_to" : "")}{(m.Params.Length > 0 ? ", params: " + m.Params : "")})"));

    private static readonly string Metrics = string.Join(", ", SemanticCatalog.Metrics.Select(m => m.Code));
    private static readonly string Dimensions = string.Join(", ", SemanticCatalog.Dimensions.Select(d => d.Code));

    private const string HypothesisFormat = """
        Гипотеза: {"id":"h1","statement":"короткая проверяемая формулировка","test":{"method":"<метод>","metrics":["revenue"],"dimension":"region",
        "grain":"day","date_from":"YYYY-MM-DD","date_to":"YYYY-MM-DD","compare_from":"YYYY-MM-DD","compare_to":"YYYY-MM-DD",
        "filters":[{"dimension":"region","op":"in","values":["Хорезм"]}],"params":{"change_date":"YYYY-MM-DD","treatment":["Хорезм"]}},
        "expect":{"direction":"down","member":"Хорезм","effect":"price","min_share":0.5}}
        Методы проверки (analytik_compute): drivers — кто дал изменение (dimension, сравнение равных отрезков); pvm — цена / объём / ассортимент
        (expect.effect: price, volume, mix, new, lost); before_after — до и после даты события (params.change_date); did — разность разностей с
        контрольной группой (dimension, params.change_date, params.treatment, params.control); correlation, regression, lag — связь показателей
        (2+ metrics); trend; churn (ушедшие клиенты); mix (доли); plan_variance (план и факт); anomalies.
        expect.direction: up или down; member — участник (регион, группа, агент) для drivers, mix, plan_variance.
        """;

    public static readonly string Planner = $$"""
        Ты — Planner и Hypothesis Analyst раздела «Аналитик» OneBase (производство кондитерских изделий в Узбекистане). Ты не отвечаешь на вопрос —
        ты планируешь исследование, которое ответит на него по данным. Верни один JSON-объект без текста вокруг:
        {"goal":"что нужно выяснить","depth":"standard|deep","steps":[{"id":"s1","question":"конкретный вопрос к данным","analyst":"<код>",
        "depends_on":[],"purpose":"зачем шаг"}],"hypotheses":[...],"data_needed":["какие данные нужны"]}
        Шаги:
        - Подзадачи — конкретные вопросы к данным: что, за какой период, в каком разрезе. Число шагов — по сложности вопроса (2–3 для умеренного,
          больше — для межмодульного), но не больше предела из сообщения. Без лишних шагов: каждый шаг — отдельная работа аналитика.
        - analyst — код аналитика из списка в сообщении (только доступные). Шаг, которому нужны выводы другого, указывает его в depends_on —
          независимые шаги выполняются параллельно.
        Гипотезы:
        - Несколько альтернативных объяснений, а не одно; обязательно «нулевая» (обычные колебания, сезонность, эффект базы), если вопрос о причинах.
        - У каждой — проверка расчётом (test) и ожидание (expect). Статус гипотезы поставит код по расчёту, не ты.
        {{HypothesisFormat}}
        Показатели: {{Metrics}}. Измерения: {{Dimensions}}. Методы: {{Methods}}.
        Даты — YYYY-MM-DD; сравнивай равные отрезки (месяц с месяцем, неделю с неделей). Названия регионов, групп, агентов — как в вопросе.
        Не придумывай чисел и выводов. Нет доступа к данным отдела — не планируй шаги этого отдела.
        """;

    public static readonly string Replanner = $$"""
        Ты — Planner исследования в разделе «Аналитик» OneBase. Исследование уже идёт: в сообщении — цель, результаты шагов, статусы гипотез,
        противоречия и пробелы. Реши, нужны ли дополнительные шаги. Верни один JSON-объект:
        {"stop":true,"reason":"почему данных достаточно"} — если вопрос можно ответить; иначе
        {"stop":false,"reason":"что не хватает","steps":[...новые шаги, id r1, r2…],"hypotheses":[...новые гипотезы, id rh1…]}.
        Как дополнять:
        - шаг без данных (no_data, failed) — альтернативный источник: другой аналитик, другой разрез или срез (analytics_query), а не тот же вопрос;
        - противоречие — проверочный шаг, который выяснит, какое значение верно (источник истины, период, правило расчёта);
        - гипотеза «не доказана» — другой метод проверки (did с контрольной группой вместо before_after, lag вместо correlation), другой период;
        - не повторяй уже выполненные шаги и проверки; не больше пределов из сообщения.
        {{HypothesisFormat}}
        Показатели: {{Metrics}}. Измерения: {{Dimensions}}.
        """;

    public static readonly string Skeptic = $$"""
        Ты — Skeptic: независимый критик выводов исследования раздела «Аналитик» OneBase. Не соглашайся по умолчанию. По данным в сообщении найди:
        логические разрывы (вывод не следует из данных), непроверенные альтернативные объяснения (сезонность, эффект базы, смена ассортимента, цены,
        отставание загрузки данных, единичный крупный клиент), подмену причины корреляцией, неверное сравнение периодов. Верни один JSON-объект:
        {"objections":["возражение"],"alternatives":[гипотезы с test и expect, id k1…]} — не больше 3 альтернатив, только проверяемые расчётом.
        {{HypothesisFormat}}
        Показатели: {{Metrics}}. Измерения: {{Dimensions}}.
        """;

    public const string Reviewer = """
        Ты — независимый Reviewer аналитического заключения раздела «Аналитик» OneBase. Ты не автор ответа и не обязан с ним соглашаться. Сверь
        ответ с материалами исследования в сообщении и найди ошибки: неверно понят вопрос; не все части вопроса раскрыты; период ответа не тот,
        что в данных; неверные расчёты; логический скачок; причина без подтверждённой гипотезы; отклонённая гипотеза выдана за причину;
        противоречие или ограничение данных не названо; рекомендация не следует из данных. Стиль и формат не оценивай. Верни один JSON-объект:
        {"verdict":"approve|revise","issues":[{"kind":"understanding|completeness|period|calculation|logic|causality|contradiction|limitations|recommendation",
        "severity":"error|warning","text":"что не так и как исправить"}]}. verdict = revise — только при ошибках (severity error).
        """;

    public const string Advisor = """
        Это ответ по исследованию (план, шаги аналитиков, гипотезы с проверкой расчётом). Правила ответа (важнее общих):
        - Таблицу гипотез со статусами и ход исследования система добавит под ответом сама — не повторяй её; в тексте опирайся на статусы.
        - Причину называй только для гипотез со статусом «подтверждена» и классом «проверенная причинная гипотеза» или «статистически подтверждённая
          связь» — и называй класс словами. Класс «наблюдаемая зависимость» — «данные согласуются», а не «причина». Не доказанные — «гипотеза,
          требует проверки»; отклонённые — «не подтвердилось». Корреляция — не причина.
        - Противоречия — назови и скажи, какому значению верить и почему (владелец данных, период, правило расчёта) или что нужно проверить.
        - Ограничения данных — отдельным коротким разделом, если часть шагов не дала данных или гипотезы не доказаны.
        - Рекомендации (Decision Advisor) — конкретные действия с приоритетом, отделом и ожидаемым эффектом. Операции (заявки, закупки, изменения
          планов) не выполняются — их оформляет человек; предложи записать решение в «Аналитик → Решения», чтобы потом проверить результат.
        """;

    /// <summary>Сообщение планировщику: дата, вопрос, история, доступные аналитики, пределы, свежесть данных.</summary>
    public static string PlannerMessage(string question, string? history, IEnumerable<(string Code, string Name, string Description)> analysts, DateOnly today,
        int maxSteps, int maxHypotheses, string? readiness)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Сегодня {today:yyyy-MM-dd} (Ташкент). Пределы: шагов — не больше {maxSteps}, гипотез — не больше {maxHypotheses}.");
        sb.AppendLine("Доступные аналитики (код — кто):");
        foreach (var a in analysts)
        {
            sb.AppendLine($"- {a.Code} — {a.Name}: {a.Description}");
        }

        if (readiness is not null)
        {
            sb.AppendLine().AppendLine("Свежесть данных:").AppendLine(readiness);
        }

        if (history is not null)
        {
            sb.AppendLine().AppendLine("Ранее в чате:").AppendLine(history);
        }

        sb.AppendLine().AppendLine("Вопрос руководителя:").AppendLine(question);
        return sb.ToString();
    }

    /// <summary>Состояние исследования для перепланирования, скептика и ревизора: шаги, гипотезы, противоречия.</summary>
    public static string State(string goal, IReadOnlyList<StepOutcome> steps, IReadOnlyList<HypothesisVerdict> verdicts, IReadOnlyList<Contradiction> contradictions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Цель: {goal}");
        sb.AppendLine("Шаги:");
        foreach (var s in steps)
        {
            sb.AppendLine($"- {s.Step.Id} [{s.Step.Analyst}] {s.Step.Question} → {s.Status}: {Cut(s.Summary, 600)}");
            foreach (var f in s.Findings.Take(6))
            {
                sb.AppendLine($"    • {Cut(f, 240)}");
            }
        }

        sb.AppendLine("Гипотезы (статус поставлен расчётом):");
        foreach (var v in verdicts)
        {
            sb.AppendLine($"- {v.Hypothesis.Id} «{v.Hypothesis.Statement}» → {HypothesisStatuses.Title(v.Status)} ({EvidenceClasses.Title(v.EvidenceClass)}): " +
                          $"{string.Join(" ", v.Supporting.Concat(v.Refuting).Take(2))} {(v.Needed.Count > 0 ? "Не хватает: " + string.Join("; ", v.Needed.Take(2)) : "")}");
        }

        if (contradictions.Count > 0)
        {
            sb.AppendLine("Противоречия:");
            foreach (var c in contradictions)
            {
                sb.AppendLine($"- {c.Text}");
            }
        }

        return sb.ToString();
    }

    private static string Cut(string s, int max) => s.Length > max ? s[..max] + "…" : s;
}
