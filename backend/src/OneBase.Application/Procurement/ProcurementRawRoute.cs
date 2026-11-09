using System.Text.Json;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>
/// Маршрут заявок «Закупка сырья» (чистые функции — проверяются тестами): шаги согласования по автору заявки — снабженец, аналитик или
/// финансист. Шаги настраиваются в «Настройки → Отделы → Снабжение» (ProcurementSettings.RawRouteSteps); по умолчанию — снабженец:
/// аналитик → финансист; аналитик: финансист; финансист: аналитик. После последнего шага заявка утверждена и уходит в снабжение.
/// </summary>
public static class ProcurementRawRoute
{
    /// <summary>Шаги: аналитик (роль «Аналитик»), финансист маршрута, директор маршрута — всегда или если сумма к закупке больше лимита.</summary>
    public const string Analyst = "analyst";
    public const string Finance = "finance";
    public const string Director = "director";
    public const string DirectorOverLimit = "director_limit";

    /// <summary>Авторы заявки: снабженец (право «Снабжение: обработка заявок»), аналитик, финансист.</summary>
    public const string SupplyAuthor = "supply";
    public const string AnalystAuthor = "analyst";
    public const string FinanceAuthor = "finance";

    public static readonly IReadOnlyList<string> Authors = [SupplyAuthor, AnalystAuthor, FinanceAuthor];
    public static readonly IReadOnlyList<string> Steps = [Analyst, Finance, Director, DirectorOverLimit];

    /// <summary>Маршрут по умолчанию (как решили 08.11.2026): без директора.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Default = new Dictionary<string, IReadOnlyList<string>>
    {
        [SupplyAuthor] = [Analyst, Finance],
        [AnalystAuthor] = [Finance],
        [FinanceAuthor] = [Analyst],
    };

    private static readonly JsonSerializerOptions Json = new();

    /// <summary>Маршрут из настроек; пусто или не читается — по умолчанию; нет строки автора — его строка по умолчанию.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Parse(string? json)
    {
        Dictionary<string, List<string>>? stored = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                stored = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, Json);
            }
            catch (JsonException)
            {
                stored = null;
            }
        }

        return Authors.ToDictionary(a => a, a => stored?.GetValueOrDefault(a) is { Count: > 0 } steps ? (IReadOnlyList<string>)steps : Default[a]);
    }

    public static string Serialize(IReadOnlyDictionary<string, IReadOnlyList<string>> route) =>
        JsonSerializer.Serialize(Authors.ToDictionary(a => a, a => route[a]), Json);

    /// <summary>
    /// Проверка маршрута из формы (текст ошибки или null): строки всех трёх авторов, в каждой хотя бы один шаг, шаги известные и без повторов,
    /// директор — один раз (всегда или если больше лимита); свою заявку автор не согласует — у аналитика нет шага «аналитик», у финансиста —
    /// «финансист».
    /// </summary>
    public static string? Check(IReadOnlyDictionary<string, IReadOnlyList<string>>? route)
    {
        if (route is null)
        {
            return "Укажите маршрут закупки сырья.";
        }

        foreach (var author in Authors)
        {
            var who = AuthorLabel(author);
            if (!route.TryGetValue(author, out var steps) || steps is not { Count: > 0 })
            {
                return $"Маршрут сырья: для заявок, которые создаёт {who}, нужен хотя бы один шаг согласования.";
            }

            if (steps.FirstOrDefault(s => !Steps.Contains(s)) is { } unknown)
            {
                return $"Маршрут сырья: неизвестный шаг «{unknown}».";
            }

            if (steps.Distinct().Count() != steps.Count || (steps.Contains(Director) && steps.Contains(DirectorOverLimit)))
            {
                return $"Маршрут сырья: для заявок, которые создаёт {who}, каждый шаг — один раз (директор — один раз: всегда или если сумма больше лимита).";
            }

            if ((author == AnalystAuthor && steps.Contains(Analyst)) || (author == FinanceAuthor && steps.Contains(Finance)))
            {
                return $"Маршрут сырья: заявку, которую создаёт {who}, он сам не согласует — уберите шаг «{StepLabel(author == AnalystAuthor ? Analyst : Finance)}».";
            }
        }

        return null;
    }

    /// <summary>
    /// Чьим маршрутом идёт заявка автора: финансист маршрута, затем аналитик, затем снабженец (автор в нескольких группах — первая подходящая).
    /// null — заявки на сырьё ему создавать нельзя.
    /// </summary>
    public static string? Author(ProcurementContext ctx) =>
        ctx.IsFinancier ? FinanceAuthor : ctx.IsAnalyst ? AnalystAuthor : ctx.Manages ? SupplyAuthor : null;

    public static ProcurementRequestStatus StatusOf(string step) => step switch
    {
        Analyst => ProcurementRequestStatus.AnalystApproval,
        Finance => ProcurementRequestStatus.FinanceApproval,
        _ => ProcurementRequestStatus.DirectorApproval,
    };

    /// <summary>Шаги заявки, с которыми её отправили (ProcurementRequest.RouteSteps).</summary>
    public static IReadOnlyList<string> Split(string? steps) =>
        string.IsNullOrWhiteSpace(steps) ? [] : steps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string Join(IReadOnlyList<string> steps) => string.Join(",", steps);

    /// <summary>
    /// Нужен ли шаг: «директор, если больше лимита» — только если сумма к закупке больше лимита; директор (оба вида) — не если автор сам
    /// директор маршрута (свою заявку не согласует).
    /// </summary>
    public static bool Needed(string step, decimal purchaseTotal, decimal limit, bool initiatorDirects) => step switch
    {
        DirectorOverLimit => purchaseTotal > limit && !initiatorDirects,
        Director => !initiatorDirects,
        _ => true,
    };

    /// <summary>
    /// Следующий шаг заявки: после текущего (from; null — первый шаг при отправке) — первый нужный (Needed); шагов больше нет — утверждена.
    /// Текущего статуса нет среди шагов — утверждена (так не бывает: решают только по шагам заявки).
    /// </summary>
    public static ProcurementRequestStatus Next(IReadOnlyList<string> steps, ProcurementRequestStatus? from, decimal purchaseTotal, decimal limit, bool initiatorDirects)
    {
        var start = 0;
        if (from is { } current)
        {
            var at = steps.ToList().FindIndex(s => StatusOf(s) == current);
            if (at < 0)
            {
                return ProcurementRequestStatus.Approved;
            }

            start = at + 1;
        }

        for (var i = start; i < steps.Count; i++)
        {
            if (Needed(steps[i], purchaseTotal, limit, initiatorDirects))
            {
                return StatusOf(steps[i]);
            }
        }

        return ProcurementRequestStatus.Approved;
    }

    /// <summary>«снабженец», «аналитик», «финансист» — для текстов ошибок.</summary>
    public static string AuthorLabel(string author) => author switch
    {
        AnalystAuthor => "аналитик",
        FinanceAuthor => "финансист",
        _ => "снабженец",
    };

    public static string StepLabel(string step) => step switch
    {
        Analyst => "аналитик",
        Finance => "финансист",
        Director => "директор",
        _ => "директор, если сумма больше лимита",
    };
}
