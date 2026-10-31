using OneBase.AI.Tools;

namespace OneBase.AI.Consultant;

/// <summary>
/// Итог AI-сотрудника для итогового ответа консультанта — ровно одно состояние из восьми, без смешения: «данные найдены», «частично», «нет прав»,
/// «источник не подключён», «технический сбой», «не найдено после проверки», «период вне доступного», «не представлено в OneBase».
/// Выводится кодом из статуса и причины (AgentResult.Status, Reason), а не из текста модели: сбой источника нельзя пересказать как «данных нет».
/// </summary>
public static class AnswerOutcome
{
    /// <summary>Данные найдены — ответ по ним.</summary>
    public const string Completed = "completed";

    /// <summary>Часть данных — ответ по подтверждённой части с ограничениями.</summary>
    public const string Partial = "partial";

    /// <summary>У пользователя нет прав на эти данные.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>Источник не подключён или в нём нет загруженных данных.</summary>
    public const string SourceNotLoaded = "source_not_loaded";

    /// <summary>Технический сбой источника — данные не получены.</summary>
    public const string ToolError = "tool_error";

    /// <summary>Данные не найдены после проверки доступных источников.</summary>
    public const string NotFound = "not_found";

    /// <summary>Запрошенный период вне доступного (предел длины, месяцы без данных).</summary>
    public const string OutOfRange = "out_of_range";

    /// <summary>Такая информация не представлена в источниках OneBase — сотрудник её не искал.</summary>
    public const string NotInOneBase = "not_in_onebase";

    /// <summary>Состояние: код, смысл, правило формулировки и готовые фразы для пользователя — русский, узбекский латиница и кириллица.</summary>
    public sealed record State(string Code, string Meaning, string Rule, string Ru, string UzLatn, string UzCyrl);

    /// <summary>Восемь состояний — в правилах консультанта (ConsultantPrompts.ConsultantRules) и в данных итогового ответа (message).</summary>
    public static readonly IReadOnlyList<State> States =
    [
        new(Completed, "данные найдены.", "Ответь по найденным данным.",
            "По данным OneBase:", "OneBase ma'lumotlariga ko'ra:", "OneBase маълумотларига кўра:"),
        new(Partial, "данные получены частично.",
            "Ответь по подтверждённой части и прямо назови ограничения: что не удалось получить (note, limitations). Чего нет в его результатах — " +
            "«не получено», а не «данных нет»: данные могут быть в OneBase, сотрудник их не успел получить.",
            "По доступным данным (ответ неполный):", "Mavjud ma'lumotlarga ko'ra (javob to'liq emas):", "Мавжуд маълумотларга кўра (жавоб тўлиқ эмас):"),
        new(Forbidden, "у пользователя нет прав на эти данные.", "Так и скажи и предложи обратиться к администратору. Не пиши, что данных нет.",
            "У вас нет доступа к этим данным — обратитесь к администратору OneBase.",
            "Bu ma'lumotlarga ruxsatingiz yo'q — OneBase administratoriga murojaat qiling.",
            "Бу маълумотларга рухсатингиз йўқ — OneBase администраторига мурожаат қилинг."),
        new(SourceNotLoaded, "источник не подключён или в нём нет загруженных данных.", "Назови источник. Не пиши, что данных в компании нет.",
            "Источник этих данных не подключён или ещё не загружен.",
            "Bu ma'lumotlar manbasi ulanmagan yoki hali yuklanmagan.",
            "Бу маълумотлар манбаси уланмаган ёки ҳали юкланмаган."),
        new(ToolError, "технический сбой источника — данные не получены.", "Не пиши «данных нет» и не называй причин сбоя; предложи повторить позже.",
            "Не удалось получить данные из-за технического сбоя источника — повторите запрос позже.",
            "Manbadagi texnik nosozlik tufayli ma'lumotlarni olib bo'lmadi — keyinroq qayta urinib ko'ring.",
            "Манбадаги техник носозлик туфайли маълумотларни олиб бўлмади — кейинроқ қайта уриниб кўринг."),
        new(NotFound, "данные не найдены после проверки доступных источников.", "Скажи, что и где проверено; это честный отказ.",
            "В доступных источниках OneBase это не найдено.",
            "OneBase'ning mavjud manbalarida bu topilmadi.",
            "OneBase'нинг мавжуд манбаларида бу топилмади."),
        new(OutOfRange, "запрошенный период вне доступного.", "Объясни ограничение (note) и предложи доступный период.",
            "За запрошенный период данные недоступны — выберите период в пределах доступного.",
            "So'ralgan davr uchun ma'lumotlar mavjud emas — mavjud davr ichida tanlang.",
            "Сўралган давр учун маълумотлар мавжуд эмас — мавжуд давр ичида танланг."),
        new(NotInOneBase, "такая информация не представлена в источниках OneBase; поиск по ней не выполнялся.",
            "Скажи об этом и назови, какие данные по теме в OneBase есть. Не утверждай, что поиск выполнялся.",
            "Такая информация не представлена в источниках OneBase.",
            "Bunday ma'lumot OneBase manbalarida mavjud emas.",
            "Бундай маълумот OneBase манбаларида мавжуд эмас."),
    ];

    /// <summary>Готовая фраза состояния для пользователя на языке вопроса; другой язык — русская фраза, модель переведёт её на язык вопроса.</summary>
    public static string Phrase(string outcome, AnswerLanguage language)
    {
        var state = States.First(s => s.Code == outcome);
        return language switch
        {
            AnswerLanguage.UzLatn => state.UzLatn,
            AnswerLanguage.UzCyrl => state.UzCyrl,
            _ => state.Ru,
        };
    }

    /// <summary>Состояние итога: completed/partial — как есть; forbidden; failed и прочие сбои — tool_error; no_data — по причине.</summary>
    public static string Of(AgentResult r) => r.Status switch
    {
        "completed" => Completed,
        "partial" => Partial,
        "forbidden" => Forbidden,
        "disabled" => SourceNotLoaded,
        "no_data" => r.Reason switch
        {
            "source_not_connected" or "source_not_loaded" => SourceNotLoaded,
            "out_of_range" => OutOfRange,
            "not_in_onebase" => NotInOneBase,
            "no_access" => Forbidden,
            _ => NotFound,
        },
        _ => ToolError,
    };

    /// <summary>
    /// Пояснение для итогового ответа — без внутренних подробностей: текст сбоя модели или инструмента (исключения, адреса, SQL) пользователю не
    /// показывается. Для «нет прав», «не подключено» и «период» — текст OneBase (его пишет сам OneBase, он для людей).
    /// </summary>
    public static string? Note(AgentResult r, string outcome) => outcome switch
    {
        ToolError => "Технический сбой источника данных — данные не получены; запрос можно повторить позже.",
        Forbidden => r.Error ?? "У пользователя нет прав на эти данные.",
        SourceNotLoaded => r.Error ?? "Источник данных не подключён или в нём нет загруженных данных.",
        OutOfRange => r.Error is { } e && ToolFailures.IsOutOfRange(e) ? $"Запрошенный период вне доступного: {e}" : "Запрошенный период вне доступного.",
        NotFound => "Данные не найдены после проверки доступных источников.",
        NotInOneBase => "Такая информация не представлена в доступных источниках OneBase (поиск по ней не выполнялся).",
        Partial when r.Reason == "interrupted" => "Анализ прерван техническим сбоем — ниже только то, что успели получить.",
        Partial when r.Reason == "truncated" => "Итог сотрудника не уместился в лимит длины ответа — ниже только то, что он успел написать.",
        Partial when r.ToolErrors is { Count: > 0 } => "Часть источников не ответила — вывод неполный.",
        Partial => "Данные получены частично — вывод неполный.",
        _ => null,
    };

    /// <summary>Ограничения итога: какие источники не ответили и почему (по человеческому названию инструмента).</summary>
    public static IReadOnlyList<string> Limitations(AgentResult r, Func<string, string?>? title = null) =>
        (r.ToolErrors ?? [])
        .Where(e => e.Kind is ToolFailures.ToolError or ToolFailures.Forbidden or ToolFailures.NotConnected)
        .Select(e => $"{title?.Invoke(e.Tool) ?? "источник данных"}: {e.Kind switch
        {
            ToolFailures.Forbidden => "нет прав",
            ToolFailures.NotConnected => "источник не подключён",
            _ => "технический сбой",
        }}")
        .Distinct()
        .ToList();
}
