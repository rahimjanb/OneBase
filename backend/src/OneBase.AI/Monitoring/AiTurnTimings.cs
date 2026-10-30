using System.Diagnostics;
using OneBase.AI.Gateway;

namespace OneBase.AI.Monitoring;

/// <summary>
/// Замер одного ответа консультанта: этапы (подготовка, маршрутизация, сотрудники, итоговый ответ, проверка, запись), каждый вызов модели
/// (ожидание очереди, время провайдера, попытка, резервная модель, токены) и каждый вызов инструмента (время, размер результата). Только
/// длительности, имена этапов, инструментов и моделей и размеры — без текста вопроса, ответа и данных. Текущий замер передаётся через
/// AsyncLocal — в параллельных сотрудников тоже; запись потокобезопасна. Сохраняется в AI-журнал (ai.AuditLogs.Timings).
/// </summary>
public sealed class AiTurnTimings
{
    private static readonly AsyncLocal<AiTurnTimings?> CurrentTurn = new();

    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly object _lock = new();
    private readonly List<AiStageTiming> _stages = [];
    private readonly List<AiLlmTiming> _calls = [];
    private readonly List<AiToolTiming> _tools = [];
    private readonly List<AiReviewStep> _reviews = [];
    private decimal _cost;

    /// <summary>Замер ответа, который сейчас идёт в этом потоке выполнения; null — вызов не из ответа консультанта.</summary>
    public static AiTurnTimings? Current => CurrentTurn.Value;

    /// <summary>Начинает замер ответа: всё, что вызывается дальше в этом потоке выполнения (и в запущенных из него задачах), пишется в него.</summary>
    public static AiTurnTimings Start()
    {
        var timings = new AiTurnTimings();
        CurrentTurn.Value = timings;
        return timings;
    }

    /// <summary>Ответ консультанта: общий идентификатор всех его вызовов моделей (ai.Usage.TurnId) и записи в AI-журнале.</summary>
    public Guid Id { get; } = Guid.CreateVersion7();

    /// <summary>Рассчитанная стоимость вызовов этого ответа, USD (по ценам моделей; без цены вызов в сумму не входит — см. CostIncomplete).</summary>
    public decimal CostUsd
    {
        get
        {
            lock (_lock)
            {
                return _cost;
            }
        }
    }

    /// <summary>У части вызовов нет цены модели — CostUsd меньше настоящей.</summary>
    public bool CostIncomplete { get; private set; }

    /// <summary>Маршрутизация моделей ответа: режим, сложность, выбор по правилам — заполняет консультант.</summary>
    public AiRoutingNote? Routing { get; set; }

    /// <summary>Вызов модели этого ответа записан в учёт со стоимостью (null — нет цены модели).</summary>
    public void AddCost(decimal? cost)
    {
        lock (_lock)
        {
            if (cost is { } c)
            {
                _cost += c;
            }
            else
            {
                CostIncomplete = true;
            }
        }
    }

    /// <summary>Шаг перепроверки: причина (AiReviewCauses), что сделано, какая модель (если был вызов).</summary>
    public void Review(string cause, string action, string? model = null, string? agent = null)
    {
        lock (_lock)
        {
            _reviews.Add(new AiReviewStep(cause, action, model, agent, Now));
        }
    }

    public IReadOnlyList<AiReviewStep> Reviews
    {
        get
        {
            lock (_lock)
            {
                return _reviews.ToList();
            }
        }
    }

    /// <summary>
    /// Отвязать текущий поток выполнения от ответа: фоновое сравнение моделей (Task.Run из ответа) не должно попадать в его стоимость
    /// и замер.
    /// </summary>
    public static void Detach() => CurrentTurn.Value = null;

    /// <summary>Время с начала ответа, мс.</summary>
    public long Now => _watch.ElapsedMilliseconds;

    /// <summary>Первый фрагмент итогового текста ушёл пользователю (потоковая отдача), мс от начала; null — текст пришёл целиком.</summary>
    public long? FirstTextMs { get; private set; }

    /// <summary>Этап: время — от вызова до Dispose.</summary>
    public IDisposable Stage(string name, string? agent = null) => new StageScope(this, name, agent, Now);

    public void Text()
    {
        lock (_lock)
        {
            FirstTextMs ??= Now;
        }
    }

    public void Llm(AiLlmTiming call)
    {
        lock (_lock)
        {
            _calls.Add(call);
        }
    }

    public void Tool(AiToolTiming tool)
    {
        lock (_lock)
        {
            _tools.Add(tool);
        }
    }

    public AiTimingsView View()
    {
        lock (_lock)
        {
            return new AiTimingsView(Now, FirstTextMs, _stages.OrderBy(s => s.StartMs).ToList(), _calls.OrderBy(c => c.StartMs).ToList(),
                _tools.OrderBy(t => t.StartMs).ToList(), _reviews.ToList());
        }
    }

    private void Add(AiStageTiming stage)
    {
        lock (_lock)
        {
            _stages.Add(stage);
        }
    }

    private sealed class StageScope(AiTurnTimings owner, string name, string? agent, long start) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done)
            {
                return;
            }

            _done = true;
            owner.Add(new AiStageTiming(name, agent, start, owner.Now - start));
        }
    }
}

/// <summary>Этап ответа: имя (prepare, routing, agent, compose…), сотрудник для этапов сотрудника, начало и длительность, мс.</summary>
public sealed record AiStageTiming(string Name, string? Agent, long StartMs, long Ms);

/// <summary>
/// Вызов модели (одна попытка): зачем, чей, какая модель; QueueMs — ожидание свободного места (лимит одновременных вызовов), ProviderMs —
/// ответ провайдера, RecordMs — запись учёта; Attempt — номер попытки этой модели, Fallback — резервная модель.
/// </summary>
public sealed record AiLlmTiming(
    string Purpose,
    string? Agent,
    string Model,
    long StartMs,
    long QueueMs,
    long ProviderMs,
    long RecordMs,
    int Attempt,
    bool Fallback,
    bool Success,
    int InputTokens,
    int OutputTokens,
    int CacheReadTokens,
    string? Reason = null,
    int CacheWriteTokens = 0);

/// <summary>Шаг перепроверки проблемного результата: причина, действие, модель вызова (если был), сотрудник, когда (мс от начала).</summary>
public sealed record AiReviewStep(string Cause, string Action, string? Model, string? Agent, long AtMs);

/// <summary>Причины перепроверки (AiReviewStep.Cause).</summary>
public static class AiReviewCauses
{
    public const string NoData = "no_data";
    public const string ToolFailed = "tool_failed";
    public const string WrongTool = "wrong_tool";
    public const string UnsupportedNumbers = "unsupported_numbers";
    public const string Incomplete = "incomplete";
    public const string Ambiguous = "ambiguous";
    public const string Budget = "budget";
    public const string Attempts = "attempts";
}

/// <summary>
/// Маршрутизация моделей ответа для журнала: режим (off | shadow | on), сложность и её признаки, модель сотрудников и итогового ответа
/// (фактически использованная и по правилам — в «Сравнении» они различаются) с причинами.
/// </summary>
public sealed record AiRoutingNote(
    string Mode,
    AnswerComplexity Complexity,
    string? AgentModel,
    string AgentReason,
    string? FinalModel,
    string FinalReason,
    string? PlannedAgentModel = null,
    string? PlannedFinalModel = null,
    string? PlannedFinalReason = null);

/// <summary>Вызов инструмента сотрудником: время, размер результата (символов) и обрезан ли он для модели.</summary>
public sealed record AiToolTiming(string Agent, string Tool, long StartMs, long Ms, int Chars, bool Success, bool Truncated);

/// <summary>Замер ответа для журнала: всего, первый фрагмент текста, этапы, вызовы модели и инструментов.</summary>
public sealed record AiTimingsView(
    long TotalMs,
    long? FirstTextMs,
    IReadOnlyList<AiStageTiming> Stages,
    IReadOnlyList<AiLlmTiming> Calls,
    IReadOnlyList<AiToolTiming> Tools,
    IReadOnlyList<AiReviewStep>? Reviews = null);

/// <summary>Короткая строка замера для журнала сервера: этапы и суммы по вызовам модели и инструментам — без текста.</summary>
public static class AiTimingsText
{
    public static string Summary(AiTimingsView view)
    {
        var stages = view.Stages
            .GroupBy(s => s.Agent is null ? s.Name : $"{s.Name}:{s.Agent}")
            .Select(g => $"{g.Key} {g.Sum(s => s.Ms)}");
        var parts = new List<string> { string.Join(", ", stages) };
        if (view.Calls.Count > 0)
        {
            parts.Add($"модель: вызовов {view.Calls.Count}, очередь {view.Calls.Sum(c => c.QueueMs)}, провайдер {view.Calls.Sum(c => c.ProviderMs)}, " +
                      $"учёт {view.Calls.Sum(c => c.RecordMs)}, повторов {view.Calls.Count(c => c.Attempt > 1)}, резерв {view.Calls.Count(c => c.Fallback)}");
        }

        if (view.Tools.Count > 0)
        {
            parts.Add($"инструменты: {view.Tools.Count}, {view.Tools.Sum(t => t.Ms)} мс, символов {view.Tools.Sum(t => (long)t.Chars)}");
        }

        if (view.FirstTextMs is { } first)
        {
            parts.Add($"первый текст {first}");
        }

        return string.Join("; ", parts);
    }
}
