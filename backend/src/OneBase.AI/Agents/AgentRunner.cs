using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Knowledge;
using OneBase.AI.Llm;
using OneBase.AI.Monitoring;
using OneBase.AI.Providers;
using OneBase.AI.Reports;
using OneBase.AI.Security;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.DataSources;

namespace OneBase.AI.Agents;

/// <summary>
/// Задача AI-сотруднику: что выяснить и (от оркестратора) что уже известно от других сотрудников. Report — пользователь сам попросил
/// отчёт Excel (ReportIntent): только тогда сотруднику даётся инструмент create_report.
/// </summary>
/// <summary>Model — модель для этого запуска вместо модели сотрудника (канал попросил другую, например Claude для генерации отчёта).</summary>
public sealed record AgentTask(string AgentCode, string Task, Guid UserId, Guid? ConversationId = null, string? Context = null, bool Report = false, AiModelRef? Model = null);

/// <summary>
/// Ai:Agents:MaxParallelTools — сколько вызовов инструментов одного шага сотрудника идут одновременно (каждый в своей области DI);
/// 1 — по очереди, как раньше.
/// </summary>
public sealed record AgentRunnerOptions(int MaxParallelTools = 4);

/// <summary>Evidence — числа из результатов инструментов и точных расчётов сотрудника: доказательства для проверки ответа консультанта.</summary>
/// <summary>
/// ToolCalls — вызовов инструментов данных (без точного расчёта), Calculations — точных расчётов, ToolErrors — инструменты, вернувшие ошибку
/// (для разбора причин перепроверки и сложности ответа). ModelReason — почему сотрудник работал этой моделью.
/// </summary>
public sealed record AgentRun(AgentResult Result, AiTokenUsage Usage, string? Model, EvidenceSet? Evidence = null, int ToolCalls = 0, int Calculations = 0,
    IReadOnlyList<AgentToolError>? ToolErrors = null, string? ModelReason = null);

/// <summary>
/// Ошибка инструмента в работе сотрудника: Kind — failed (инструмент упал и после повтора), denied (нет такого инструмента, гранта или доступа
/// — модель выбрала неподходящий), error (инструмент отказал: аргументы, нет данных под фильтр).
/// </summary>
public sealed record AgentToolError(string Tool, string Kind, string Message)
{
    public const string Failed = "failed";
    public const string Denied = "denied";
    public const string Error = "error";
}

/// <summary>
/// Выполняет задачу одного AI-сотрудника: цикл вызова инструментов (данные OneBase) и структурированный итог
/// agent/status/findings/metrics/problems/recommendations/data_sources. Числа итога сверяются с результатами инструментов: неподтверждённые —
/// одна попытка пересчитать (calculate) или убрать, оставшиеся — в Unverified результата. Сбой инструмента не останавливает сотрудника (модель
/// получает структурный сбой и пробует другой путь); сбой модели после полученных данных — итог partial с этими данными, а не пустая ошибка.
/// Статус no_data — только когда данные действительно не найдены: сбой инструментов — failed, нет прав — forbidden (см. Classify).
/// </summary>
public sealed class AgentRunner(
    AiAgentStore agents,
    IAiGateway gateway,
    ToolExecutor executor,
    IUserPermissions permissions,
    KnowledgeService knowledge,
    SalesGlossary glossary,
    ReportService reports,
    SourceOfTruthService truth,
    TimeProvider clock,
    IServiceScopeFactory scopes,
    AgentRunnerOptions runnerOptions,
    IAiSettingsSource aiSettings,
    ILogger<AgentRunner>? logger = null)
{
    private const int MaxSteps = 8;

    /// <summary>
    /// Лимит длины ответа сотрудника, если в «Агентах» он не задан. Итог сотрудника — JSON с выводом, находками, показателями, проблемами и
    /// рекомендациями: на широкий вопрос («сравни октябрь с сентябрём по регионам, ассортименту и визитам») он занимает 3,5 тыс. токенов,
    /// а у reasoning-моделей в лимит входят и рассуждения. Общий лимит ответа (по умолчанию 4096) обрезал такой JSON — итог не разбирался,
    /// и сотрудник выходил «частично, без выводов». Платится только написанное, поэтому запас лимита ничего не стоит.
    /// </summary>
    internal const int AgentOutputTokens = 8192;

    /// <summary>Потолок лимита для повтора обрезанного итога («то же, короче»).</summary>
    internal const int MaxAgentOutputTokens = 32_000;

    /// <summary>Итог обрезан лимитом длины ответа: JSON недописан.</summary>
    internal static bool Truncated(string? stopReason) => stopReason is "max_tokens" or "length";

    private const string ShorterFinal =
        "Итог не поместился в лимит длины ответа и обрезан. Верни итоговый JSON заново и компактнее: summary — 2–3 предложения, findings — до 10 " +
        "главных фактов с цифрами, metrics — до 12, problems и recommendations — до 5, без повторов. Инструменты не вызывай — только данные, " +
        "уже полученные выше.";

    private const string AsJson =
        "Итог должен быть одним JSON-объектом по схеме из инструкции (agent, status, summary, findings, metrics, problems, recommendations, " +
        "data_sources) без текста вокруг. Верни его по уже полученным данным; инструменты не вызывай.";

    private const string FinishAfterFailure =
        "Связь с моделью на прошлом шаге прервалась. Новых инструментов не вызывай: подведи итог по данным, уже полученным выше, и верни итоговый " +
        "JSON. Что не успел получить — перечисли в summary как «не получено» (а не «данных нет») и верни status partial.";

    /// <summary>Сколько символов одного результата инструмента попадает в итог partial после сбоя, и сколько всего.</summary>
    private const int SalvageItemChars = 3_000;
    private const int SalvageTotalChars = 12_000;

    /// <summary>Сколько символов результата инструмента уходит модели — большие таблицы режутся (карточка ТП или точки целиком помещается).</summary>
    private const int MaxToolResultChars = 40_000;

    /// <summary>
    /// Сколько символов результатов инструментов помещается в один анализ сотрудника (контекст модели): каждый следующий результат получает
    /// не больше остатка, но не меньше MinToolResultChars. Большие результаты не обрезаются, а урезаются по строкам списков (ToolResultPager).
    /// </summary>
    private const int ContextBudgetChars = 200_000;

    private const int MinToolResultChars = 8_000;

    /// <summary>Узбекистан — UTC+5 без перехода на летнее время.</summary>
    private static readonly TimeSpan CompanyOffset = TimeSpan.FromHours(5);

    public async Task<AgentRun> RunAsync(AgentTask task, Func<ConsultantProgress, Task>? progress, CancellationToken ct)
    {
        var turn = AiTurnTimings.Current;
        var setup = turn?.Stage("agent.setup", task.AgentCode);
        var agent = await agents.GetAsync(task.AgentCode, ct);
        if (agent is null)
        {
            return Failed(task.AgentCode, task.AgentCode, "unknown", $"AI-сотрудника «{task.AgentCode}» нет.");
        }

        if (!agent.Enabled)
        {
            return Failed(agent.Code, agent.Name, "disabled", "AI-сотрудник выключен в настройках AI.");
        }

        var userPermissions = await permissions.GetAsync(task.UserId, ct);
        if (agent.RequiredPermission is { } required && !userPermissions.Contains(required))
        {
            return Failed(agent.Code, agent.Name, "forbidden", "У вас нет доступа к данным этого отдела.", "no_access");
        }

        // Только инструменты данных: взаимодействие отделов идёт через главного консультанта, а не делегированием.
        var permitted = (await executor.GetAllowedToolsAsync(agent.Code, task.UserId, ct)).Where(t => t.Source is not null).ToList();
        var tools = permitted;

        // Пока в базе знаний нет документов, доступных пользователю, поиск по ней не предлагается: он всегда пуст,
        // а каждый вызов — лишний круг к модели. С первым проиндексированным документом поиск включается сам.
        if (tools.Any(t => t.Source == KnowledgeSources.Documents) && (await knowledge.AllowedDocumentsAsync(task.UserId, ct)).Count == 0)
        {
            tools = tools.Where(t => t.Source != KnowledgeSources.Documents).ToList();
        }

        // Без инструментов данных сотруднику нечем ответить: это «нет доступа» (или источник не подключён), а не «данных нет» — модель не вызывается.
        if (tools.Count == 0)
        {
            return permitted.Count == 0
                ? Failed(agent.Code, agent.Name, "forbidden", "Нет доступа: у пользователя нет прав ни на один источник данных этого сотрудника.", "no_access")
                : Failed(agent.Code, agent.Name, "no_data", "Источник не подключён: в базе знаний нет документов, доступных пользователю.", "source_not_connected");
        }
        var definitions = tools.Select(t => new LlmToolDefinition(t.Name, t.Description, t is DataTool ? WithAllRows(t.InputSchema) : t.InputSchema)).ToList();

        // Отчёт Excel — только по просьбе пользователя и только при инструментах данных: один отчёт за задачу.
        var reportTool = task.Report && tools.Count > 0 ? new LlmToolDefinition(ReportSpec.ToolName, ReportSpec.Description, ReportSpec.InputSchema) : null;
        if (reportTool is not null)
        {
            definitions.Add(reportTool);
        }

        // Точный расчёт и источник истины — у каждого сотрудника с данными: производные числа считает код, расхождения источников решает правило.
        if (tools.Count > 0)
        {
            definitions.Add(Calculator.Definition);
            definitions.Add(SourceOfTruthTool.Definition);
            // Следующие строки больших результатов — из уже полученного снимка, без нового запроса к данным.
            definitions.Add(ToolResultPager.Definition);
        }

        // Словарь названий компании — только сотрудникам с данными продаж: иначе «помадка» в вопросе для модели просто незнакомое слово.
        var glossaryText = tools.Any(t => t.Source?.StartsWith("sales.", StringComparison.Ordinal) == true) ? await glossary.TextAsync(ct) : null;
        var messages = new List<LlmMessage>
        {
            new(LlmRole.System, SystemPrompt(agent, tools, glossaryText, reportTool is not null)),
            new(LlmRole.User, UserMessage(task, clock.GetUtcNow().ToOffset(CompanyOffset))),
        };

        var context = new ToolContext(agent.Code, task.UserId, 0, task.ConversationId);
        setup?.Dispose();
        var usage = AiTokenUsage.None;
        var toolsUsed = new List<string>();
        var sources = new List<DataSource>();
        var evidence = new EvidenceSet();
        // Что инструменты данных уже вернули (для итога partial при сбое) и какие инструменты данных не дали.
        var collected = new List<(string Title, string Content)>();
        var issues = new List<ToolIssue>();
        // Сколько раз сотрудник обращался к инструментам данных: «не найдено» — только после поиска, без него — «не представлено в OneBase».
        var attempts = 0;
        // Большие результаты — страницами из сохранённого снимка; used — сколько символов результатов уже в контексте модели.
        var pager = new ToolResultPager();
        var used = 0;
        var revised = false;
        var shortened = false;
        var reformatted = false;
        var reported = false;
        string? model = null;
        var settings = await aiSettings.GetSettingsAsync(ct);
        // Модель: канала, заданная сотруднику в «Агентах», по правилам маршрутизации моделей (модель сотрудников) или основная.
        var choice = AiModelRouting.Agent(settings, task.Model, agent.Model);
        // Review: упавший инструмент — один повтор без вызова модели, в новой области DI (сбой мог оставить DbContext в плохом состоянии).
        var review = settings.ModelRouting.On && settings.ModelRouting.Review;
        var toolCalls = 0;
        var calculations = 0;
        var toolErrors = new List<AgentToolError>();
        var options = new AiCallOptions
        {
            Model = choice.Model,
            ModelReason = choice.Reason,
            Temperature = agent.Temperature,
            // Лимит сотрудника из «Агентов»; не задан — не меньше AgentOutputTokens (общий лимит ответа обрезал итоговый JSON).
            MaxOutputTokens = agent.MaxOutputTokens ?? Math.Max(settings.MaxOutputTokens, AgentOutputTokens),
            JsonOutput = true,
            Context = new AiCallContext(task.UserId, agent.Code, task.ConversationId, "agent"),
        };

        // Шагов — MaxSteps; проверка итога по данным, пришедшая поздно, получает ещё до двух шагов (расчёт и итог), а не теряет числа.
        var steps = MaxSteps;
        try
        {
            for (var step = 0; step < steps; step++)
            {
                // На последнем шаге инструменты вызывать нельзя — агент должен подвести итог по тому, что уже получил. Сам список инструментов
                // не меняется: они есть в истории диалога, а у Claude смена набора инструментов посреди диалога сбрасывает его рассуждения.
                var last = step == steps - 1;
                var response = await gateway.CompleteAsync(messages, definitions, last ? options with { AllowToolCalls = false } : options, ct);
                usage = usage.Add(response.Usage);
                model = response.Model.ToString();

                if (response.ToolCalls.Count == 0 && Truncated(response.StopReason) && !shortened)
                {
                    // Итог обрезан лимитом длины: JSON не разобрать — «частично, без выводов». Одна попытка: тот же итог короче и с запасом по длине
                    // (вне счёта шагов: инструменты уже не нужны).
                    shortened = true;
                    logger?.LogWarning("AI: итог сотрудника {Agent} обрезан лимитом длины ответа ({Limit}) — прошу короче", agent.Code, options.MaxOutputTokens);
                    turn?.Review(AiReviewCauses.Incomplete, "итог сотрудника обрезан лимитом длины ответа — повтор короче", response.Model.ToString(), agent.Code);
                    messages.Add(response.Reply);
                    messages.Add(new LlmMessage(LlmRole.User, ShorterFinal));
                    response = await gateway.CompleteAsync(messages, definitions, options with
                    {
                        AllowToolCalls = false,
                        MaxOutputTokens = Math.Min((options.MaxOutputTokens ?? AgentOutputTokens) * 2, MaxAgentOutputTokens),
                    }, ct);
                    usage = usage.Add(response.Usage);
                    model = response.Model.ToString();
                }

                if (response.ToolCalls.Count == 0 && !reformatted && tools.Count > 0 && JsonText.ExtractObject(response.Text) is null
                    && JsonText.CloseTruncated(response.Text) is null)
                {
                    // Итог текстом, а не JSON: разобрать его нельзя — вышел бы «частично, без выводов». Одна просьба вернуть JSON по уже полученным данным.
                    reformatted = true;
                    logger?.LogWarning("AI: итог сотрудника {Agent} — не JSON, прошу вернуть JSON", agent.Code);
                    turn?.Review(AiReviewCauses.Incomplete, "итог сотрудника не в JSON — повтор в нужном виде", response.Model.ToString(), agent.Code);
                    messages.Add(response.Reply);
                    messages.Add(new LlmMessage(LlmRole.User, AsJson));
                    response = await gateway.CompleteAsync(messages, definitions, options with { AllowToolCalls = false }, ct);
                    usage = usage.Add(response.Usage);
                    model = response.Model.ToString();
                }

                if (response.ToolCalls.Count == 0)
                {
                    var result = Parse(agent, response.Text, toolsUsed, sources);
                    var check = EvidenceCheck.Run(ResultText(result), evidence, task.Task);
                    if (!check.Grounded && !revised && tools.Count > 0)
                    {
                        // Evidence Check сотрудника: числа, которых нет в результатах инструментов, — пересчитать (calculate), получить или убрать.
                        // Итог пришёл на последних шагах — ещё шаг на расчёт и шаг на итог (один раз: revised).
                        revised = true;
                        steps = Math.Max(steps, step + 3);
                        messages.Add(response.Reply);
                        messages.Add(new LlmMessage(LlmRole.User, $"""
                            Проверка итога по данным: эти числа не найдены в результатах инструментов: {string.Join("; ", check.Unsupported)}.
                            Производное число (сумма, разница, доля, рост, среднее) посчитай инструментом {Calculator.ToolName}; недостающее — получи
                            инструментом данных; чего нет в данных — убери или пометь как неподтверждённое. Затем верни исправленный итоговый JSON.
                            """));
                        continue;
                    }

                    var final = Settle(check.Grounded ? result : result with { Unverified = check.Unsupported }, issues);
                    return new AgentRun(Classify(final, collected.Count, issues, attempts), usage, model, evidence, toolCalls, calculations, toolErrors, choice.Reason);
                }

                messages.Add(response.Reply);
                var calls = response.ToolCalls;
                var titles = new string[calls.Count];
                for (var i = 0; i < calls.Count; i++)
                {
                    var call = calls[i];
                    titles[i] = call.Name == ReportSpec.ToolName ? ReportSpec.ToolTitle
                        : call.Name == Calculator.ToolName ? "Точный расчёт"
                        : call.Name == SourceOfTruthTool.ToolName ? "Источник истины"
                        : call.Name == ToolResultPager.ToolName ? "Следующие строки результата"
                        : tools.FirstOrDefault(t => t.Name == call.Name)?.Title ?? call.Name;
                    if (progress is not null)
                    {
                        await progress(new ConsultantProgress("agent", agent.Code, agent.Name, ProgressStatus.Running, titles[i]));
                    }
                }

                // Вызовы одного шага модель запросила, не видя результатов друг друга, — по данным они независимы. Расчёт, следующие строки
                // сохранённого результата (get_more_rows) и повтор уже выполненного вызова — сразу, без БД и без нового запроса к данным; инструменты
                // данных и источник истины — параллельно, каждый в своей области DI (свой DbContext, права, гранты и аудит в ToolExecutor); отчёт
                // Excel меняет состояние (один на задачу) — после остальных, по порядку. Один такой вызов — как раньше, в области сотрудника.
                var outcomes = new ToolOutcome[calls.Count];
                var io = new List<int>();
                var keys = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < calls.Count; i++)
                {
                    var call = calls[i];
                    var start = turn?.Now ?? 0;
                    if (call.Name == ReportSpec.ToolName)
                    {
                        continue;
                    }

                    if (call.Name == Calculator.ToolName && tools.Count > 0)
                    {
                        var (calculated, value) = Calculator.Run(call.Arguments);
                        outcomes[i] = new ToolOutcome(value is null ? ToolResult.Error(calculated) : ToolResult.Ok(calculated), value, start, 0);
                    }
                    else if (call.Name == ToolResultPager.ToolName && tools.Count > 0)
                    {
                        outcomes[i] = new ToolOutcome(pager.Page(call.Arguments, Budget(used)), null, start, 0);
                    }
                    else if (call.Name != SourceOfTruthTool.ToolName && ToolResultPager.CallKey(call.Name, call.Arguments) is var key
                             && (pager.TryRepeat(key, out _, out var earlier) || !keys.Add(key)))
                    {
                        // Тот же вызов с теми же аргументами уже был (в этом анализе или в этом же шаге): данные не запрашиваются заново.
                        outcomes[i] = new ToolOutcome(Repeated(earlier), null, start, 0);
                    }
                    else
                    {
                        io.Add(i);
                    }
                }

                if (io.Count > 1 && runnerOptions.MaxParallelTools > 1)
                {
                    var parallel = await ToolBatch.RunAsync(io.Count, runnerOptions.MaxParallelTools, async (k, token) =>
                    {
                        using var scope = scopes.CreateScope();
                        return await RunToolAsync(calls[io[k]], context, tools.Count > 0, scope.ServiceProvider.GetRequiredService<ToolExecutor>(),
                            scope.ServiceProvider.GetRequiredService<SourceOfTruthService>(), turn, review, token);
                    }, ct);
                    for (var k = 0; k < io.Count; k++)
                    {
                        outcomes[io[k]] = parallel[k];
                    }
                }
                else
                {
                    foreach (var i in io)
                    {
                        outcomes[i] = await RunToolAsync(calls[i], context, tools.Count > 0, executor, truth, turn, review, ct);
                    }
                }

                for (var i = 0; i < calls.Count; i++)
                {
                    if (calls[i].Name != ReportSpec.ToolName)
                    {
                        continue;
                    }

                    var start = turn?.Now ?? 0;
                    var watch = Stopwatch.StartNew();
                    ToolResult report;
                    try
                    {
                        if (reportTool is null || reported)
                        {
                            report = ToolResult.Error(reportTool is null
                                ? "Отчёт Excel формируется только по просьбе пользователя — ответь текстом."
                                : "Отчёт уже сформирован — второй не нужен. Подведи итог.");
                        }
                        else
                        {
                            (report, _) = await reports.CreateAsync(context, calls[i].Arguments, ct);
                            reported = report.Success;
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
                    {
                        // Отчёт — не через ToolExecutor: его сбой тоже не должен останавливать сотрудника.
                        logger?.LogError(ex, "AI: {Tool} сотрудника {Agent} завершился ошибкой", calls[i].Name, agent.Code);
                        report = ToolResult.Failure(calls[i].Name, ToolFailures.KindOf(ex), ToolFailures.SafeMessage(ex));
                    }

                    outcomes[i] = new ToolOutcome(report, null, start, watch.ElapsedMilliseconds);
                }

                var executed = io.ToHashSet();
                for (var i = 0; i < calls.Count; i++)
                {
                    var call = calls[i];
                    var (result, value, toolStart, toolMs) = outcomes[i];
                    var isData = executed.Contains(i) && call.Name != SourceOfTruthTool.ToolName;
                    if (call.Name == Calculator.ToolName && tools.Count > 0)
                    {
                        calculations++;
                        if (value is { } v)
                        {
                            evidence.Add(v);
                        }
                    }
                    else if (executed.Contains(i))
                    {
                        toolCalls++;
                        if (result.Success)
                        {
                            evidence.AddContent(result.Content); // данные инструмента и карта источника истины
                            // Источник истины нашёл, что таблица не привязана или не загружалась: это «источник не подключён», а не «не искали».
                            if (call.Name == SourceOfTruthTool.ToolName && SourceIssue(result.Content) is { } sourceIssue)
                            {
                                issues.Add(sourceIssue);
                            }
                        }
                        else
                        {
                            toolErrors.Add(new AgentToolError(call.Name, ErrorKindOf(result), result.Content.Length > 200 ? result.Content[..200] : result.Content));
                        }
                    }

                    if (isData)
                    {
                        attempts++;
                        if (result.Success)
                        {
                            collected.Add((titles[i], result.Content));
                        }
                        else if ((result.ErrorKind ?? (ToolFailures.IsOutOfRange(result.Content) ? ToolFailures.OutOfRange : null)) is { } kind
                                 && kind != ToolFailures.InvalidArguments)
                        {
                            // Ошибка аргументов об ограничении периода («Период — не больше 62 дней») — тоже итог: период вне доступного.
                            issues.Add(new ToolIssue(call.Name, kind, ToolMessage(result.Content)));
                        }
                    }

                    turn?.Tool(new AiToolTiming(agent.Code, call.Name, toolStart, toolMs, result.Content.Length, result.Success,
                        result.Content.Length > MaxToolResultChars));
                    if (!toolsUsed.Contains(call.Name))
                    {
                        toolsUsed.Add(call.Name);
                    }

                    foreach (var s in result.Sources ?? [])
                    {
                        if (!sources.Contains(s))
                        {
                            sources.Add(s);
                        }
                    }

                    // Не обрезается посередине: большой результат — валидный JSON с первыми строками списков и «_pagination»; остальное — get_more_rows.
                    var stored = pager.Stored;
                    var content = pager.Fit(result.Content, Budget(used), out _);
                    if (isData && result.Success)
                    {
                        pager.Remember(ToolResultPager.CallKey(call.Name, call.Arguments), result.Content, pager.Stored > stored ? pager.LastResultId : null);
                    }

                    used += content.Length;
                    messages.Add(new LlmMessage(LlmRole.Tool, result.Success ? content : $"ОШИБКА: {content}", ToolCallId: call.Id));
                }
            }
        }
        catch (Exception ex) when (collected.Count > 0 && !(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            // Модель или служба упали после того, как инструменты уже дали данные: итог partial с этими данными, а не пустая ошибка.
            logger?.LogWarning(ex, "AI: сотрудник {Agent} прерван после {Count} результатов инструментов — итог по полученным данным", agent.Code, collected.Count);
            var reason = ex is AiProviderException or LlmNotConfiguredException ? ex.Message : "внутренняя ошибка OneBase";

            // Сбой модели (перегрузка, таймаут) — ещё одна попытка подвести итог по уже полученным данным, без инструментов и без повтора внутри
            // шлюза: иначе вместо выводов — сырые куски результатов. Бюджет вопроса исчерпан или модель не настроена (LlmNotConfiguredException) —
            // новый вызов бессмыслен.
            if (ex is AiProviderException)
            {
                try
                {
                    var finish = await gateway.CompleteAsync([.. messages, new LlmMessage(LlmRole.User, FinishAfterFailure)], definitions,
                        options with { AllowToolCalls = false, RetrySameModel = false }, ct);
                    usage = usage.Add(finish.Usage);
                    model = finish.Model.ToString();
                    if (finish.ToolCalls.Count == 0 && (JsonText.ExtractObject(finish.Text) is not null || JsonText.CloseTruncated(finish.Text) is not null))
                    {
                        var result = Parse(agent, finish.Text, toolsUsed, sources);
                        var check = EvidenceCheck.Run(ResultText(result), evidence, task.Task);
                        var final = check.Grounded ? result : result with { Unverified = check.Unsupported };
                        turn?.Review(AiReviewCauses.Incomplete, "сбой модели — итог подведён по уже полученным данным", model, agent.Code);
                        return new AgentRun(Classify(final, collected.Count, issues, attempts), usage, model, evidence, toolCalls, calculations, toolErrors,
                            choice.Reason);
                    }
                }
                catch (Exception again) when (!(again is OperationCanceledException && ct.IsCancellationRequested))
                {
                    logger?.LogWarning(again, "AI: сотрудник {Agent} — итог после сбоя тоже не получен", agent.Code);
                }
            }
            return new AgentRun(Salvage(agent, $"Анализ прерван: {reason}", collected, sources, toolsUsed, issues), usage, model, evidence, toolCalls, calculations,
                toolErrors, choice.Reason);
        }

        return collected.Count > 0
            ? new AgentRun(Salvage(agent, "Агент не завершил анализ за отведённое число шагов.", collected, sources, toolsUsed, issues), usage, model, evidence,
                toolCalls, calculations, toolErrors, choice.Reason)
            : new AgentRun(new AgentResult(agent.Code, agent.Name, "failed", "Превышен лимит шагов агента.", [], [], [], [], sources, toolsUsed,
                "Агент не завершил анализ за отведённое число шагов.", Reason: "interrupted", ToolErrors: issues.Count > 0 ? issues : null), usage, model, evidence,
                toolCalls, calculations, toolErrors, choice.Reason);
    }

    /// <summary>Повтор вызова: результат уже есть выше (earlier — его result_id, если он сохранён страницами).</summary>
    private static ToolResult Repeated(string? earlier) => ToolResult.Ok(JsonSerializer.Serialize(new
    {
        repeat = true,
        result_id = earlier,
        message = "Этот вызов с теми же аргументами уже выполнен в этом анализе — его результат выше. Используй его" +
                  (earlier is null ? "." : $"; следующие строки — {ToolResultPager.ToolName}(result_id = {earlier})."),
    }));

    /// <summary>Что за ошибка инструмента: упал (после повтора), выбран неподходящий (нет инструмента, гранта, доступа) или отказ инструмента.</summary>
    internal static string ErrorKind(string message) =>
        message.StartsWith(ToolFailurePrefix, StringComparison.Ordinal) ? AgentToolError.Failed
        : message.Contains("не найден", StringComparison.Ordinal) && message.StartsWith("Инструмент", StringComparison.Ordinal)
          || message.Contains("нет доступа к инструменту", StringComparison.Ordinal)
          || message.Contains("закрыт для агента", StringComparison.Ordinal) ? AgentToolError.Denied
        : AgentToolError.Error;

    /// <summary>
    /// Вид ошибки инструмента для разбора перепроверки — по структурному виду сбоя (ToolFailures): сбой источника — failed; инструмента нет
    /// или он не подключён, нет доступа — denied (модель выбрала неподходящий); остальное (аргументы, период, «не найдено») — error.
    /// </summary>
    internal static string ErrorKindOf(ToolResult result) => result.ErrorKind switch
    {
        ToolFailures.ToolError => AgentToolError.Failed,
        ToolFailures.NotConnected or ToolFailures.Forbidden => AgentToolError.Denied,
        null => ErrorKind(result.Content),
        _ => AgentToolError.Error,
    };

    private const string ToolFailurePrefix = "Инструмент завершился ошибкой";

    /// <summary>Результат вызова инструмента шага: Value — число точного расчёта (доказательство), начало и длительность — для замера.</summary>
    private sealed record ToolOutcome(ToolResult Result, decimal? Value, long StartMs, long Ms);

    /// <summary>
    /// Вызов инструмента данных или источника истины — через переданные сервисы (области сотрудника или своей области вызова). Сбой — структурный
    /// результат (ToolFailures), а не исключение: сотрудник продолжает с другими инструментами. review: сбой источника (tool_error) — один повтор
    /// без вызова модели в новой области DI; не удался и он — сбой инструмента для модели (данные этого инструмента недоступны).
    /// </summary>
    private async Task<ToolOutcome> RunToolAsync(LlmToolCall call, ToolContext context, bool hasData, ToolExecutor executor, SourceOfTruthService truth,
        AiTurnTimings? turn, bool review, CancellationToken ct)
    {
        var start = turn?.Now ?? 0;
        var watch = Stopwatch.StartNew();
        async Task<ToolResult> Run(ToolExecutor ex, SourceOfTruthService tr)
        {
            try
            {
                return call.Name == SourceOfTruthTool.ToolName && hasData
                    ? ToolResult.Ok(await SourceOfTruthTool.RunAsync(call.Arguments, tr, ct))
                    : await ex.ExecuteAsync(context, call.Name, call.Arguments, ct);
            }
            catch (Exception e) when (!(e is OperationCanceledException && ct.IsCancellationRequested))
            {
                // Источник истины — не через ToolExecutor (тот сам превращает сбой инструмента в структурный): его сбой тоже не останавливает сотрудника.
                logger?.LogError(e, "AI: {Tool} сотрудника {Agent} завершился ошибкой", call.Name, context.AgentCode);
                return ToolResult.Failure(call.Name, ToolFailures.KindOf(e), ToolFailures.SafeMessage(e));
            }
        }

        var result = await Run(executor, truth);
        if (!review || result.ErrorKind != ToolFailures.ToolError)
        {
            return new ToolOutcome(result, null, start, watch.ElapsedMilliseconds);
        }

        logger?.LogWarning("AI: инструмент {Tool} сотрудника {Agent} упал — повтор без вызова модели", call.Name, context.AgentCode);
        using var scope = scopes.CreateScope();
        var again = await Run(scope.ServiceProvider.GetRequiredService<ToolExecutor>(), scope.ServiceProvider.GetRequiredService<SourceOfTruthService>());
        turn?.Review(AiReviewCauses.ToolFailed,
            again.Success ? $"{call.Name}: повтор без вызова модели — удался" : $"{call.Name}: упал и при повторе — данные инструмента недоступны", null, context.AgentCode);
        return new ToolOutcome(again, null, start, watch.ElapsedMilliseconds);
    }

    /// <summary>Бюджет следующего результата инструмента: остаток контекста анализа, от MinToolResultChars до MaxToolResultChars.</summary>
    internal static int Budget(int used) => Math.Clamp(ContextBudgetChars - used, MinToolResultChars, MaxToolResultChars);

    /// <summary>Схема инструмента данных с аргументом all_rows: списки целиком (большие — страницами get_more_rows), а не первые N.</summary>
    internal static JsonElement WithAllRows(JsonElement schema)
    {
        if (System.Text.Json.Nodes.JsonNode.Parse(schema.GetRawText()) is not System.Text.Json.Nodes.JsonObject root)
        {
            return schema;
        }

        if (root["properties"] is not System.Text.Json.Nodes.JsonObject properties)
        {
            properties = new System.Text.Json.Nodes.JsonObject();
            root["properties"] = properties;
        }

        properties[DataTool.AllRowsArg] ??= new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = "boolean",
            ["description"] = "true — списки целиком, а не первые строки (большие придут страницами: get_more_rows). Для сумм и количества не нужно — бери итоги.",
        };
        return JsonSerializer.SerializeToElement(root);
    }

    /// <summary>Утверждения итога сотрудника для проверки по данным: вывод, факты, показатели и проблемы (рекомендации — предложения, не факты).</summary>
    internal static string ResultText(AgentResult r) =>
        string.Join("\n", new[] { r.Summary }
            .Concat(r.Findings)
            .Concat(r.Metrics.Select(m => $"{m.Name}: {m.Value} {m.Unit}".Trim()))
            .Concat(r.Problems));

    /// <summary>
    /// Первое сообщение пользователя: дата и время — здесь, а не в системном промпте. Системный промпт с инструментами одинаков от вызова
    /// к вызову и читается из кэша провайдера; строка с текущей минутой в его начале делала бы каждый запрос уникальным.
    /// </summary>
    internal static string UserMessage(AgentTask task, DateTimeOffset now)
    {
        var text = $"Сегодня {now:dd.MM.yyyy}, {now:HH:mm} по Ташкенту.\n\n{task.Task}";
        return task.Context is null ? text : $"{text}\n\nКонтекст от главного консультанта:\n{task.Context}";
    }

    /// <summary>Системный промпт сотрудника — без даты и других меняющихся значений (см. UserMessage), чтобы кэшироваться у провайдера.</summary>
    internal static string SystemPrompt(AgentConfig agent, IReadOnlyList<ITool> tools, string? glossaryText, bool report = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine(agent.Prompt);
        sb.AppendLine();
        sb.AppendLine("Компания работает в Узбекистане, суммы — в сумах (UZS), вес — в кг. Текущая дата и время — в сообщении пользователя.");
        sb.AppendLine(tools.Count == 0
            ? "Инструментов с данными OneBase у тебя сейчас нет — это ограничение доступа, а не отсутствие данных: так и напиши в summary."
            : $"Бери цифры только из результатов инструментов. Вызывай инструменты столько раз, сколько нужно, с точными фильтрами (период, регион). " +
              $"Любое число, которого нет в результатах (сумма, разница, доля, рост, среднее), считай инструментом {Calculator.ToolName} — каждое число итога " +
              $"сверяется с данными. Если значения из разных источников расходятся или пользователь ссылается на другой источник — вызови " +
              $"{SourceOfTruthTool.ToolName}: верно значение источника-владельца, о расхождении скажи. " +
              "Не отвечай «нет данных» или «нет доступа», пока не дошёл до отдельных записей инструментами детализации (карточка ТП, визиты по точкам, " +
              "карточка точки, заказы, KPI, акции, акт сверки, DSO): сводка без нужного разреза — повод спуститься глубже, а не остановиться. " +
              "Разрез «по маршруту / вне маршрута / без визита» (визиты, визиты с заказом, заказы и их сумма, выручка, кг, АКБ) есть у республики, " +
              "направления, региона с каждым ТП и одного ТП за любой отрезок дней месяца — get_visits; продажи за день, неделю или «с N по M» " +
              "внутри одного календарного месяца — get_sales_days; период через границу месяца, несколько месяцев, квартал, полугодие или год " +
              "(до 366 дней) — get_sales_period одним вызовом, и сравнение периодов — тоже get_sales_period: разница и % в его результате " +
              "посчитаны кодом. Даты пересекают границу месяца — сразу get_sales_period, без пробного get_sales_days; квартал или несколько " +
              "месяцев не разбивай на вызовы get_sales_days. " +
              "Не перечисляй пользователю, каких данных «нет»: сначала вызови инструменты и дай цифры; если разреза действительно " +
              "нет ни в одном инструменте — одной фразой скажи, какого, и дай ближайшие данные.");
        foreach (var hint in tools.OfType<DataTool>().Select(t => t.UsageHint).OfType<string>())
        {
            sb.AppendLine(hint);
        }

        if (glossaryText is not null)
        {
            sb.AppendLine();
            sb.AppendLine(glossaryText);
        }

        if (report)
        {
            sb.AppendLine();
            sb.AppendLine($"""
                Пользователь попросил отчёт Excel. Порядок: 1) получи нужные данные инструментами (период и фильтры — как в вопросе);
                2) ОДИН раз вызови {ReportSpec.ToolName}: листы с source — тот же инструмент и аргументы, path к массиву в его результате,
                колонки с понятными русскими заголовками и форматами (kg, money, percent для полей …Pct, integer, text); итоговая строка (total) —
                где суммы имеют смысл. Цифры backend возьмёт из данных сам, списки при этом полные. 3) Верни итоговый JSON; в summary скажи, что
                отчёт сформирован и что в нём. Если данных для отчёта нет — не создавай его и верни status no_data.
                """);
        }

        sb.AppendLine();
        sb.AppendLine(ConsultantPrompts.AgentRules);
        sb.AppendLine();
        sb.AppendLine($$"""
            Когда данных достаточно, верни итог ОДНИМ JSON-объектом (без Markdown и текста вокруг):
            {"agent":"{{agent.Code}}","status":"completed | partial | no_data","summary":"2–3 предложения — главный вывод",
             "findings":["факт с цифрой, единицей и периодом"],"metrics":[{"name":"...","value":"...","unit":"..."}],
             "problems":["проблема и её масштаб"],"recommendations":["конкретное действие на основе фактов"],
             "data_sources":[{"title":"источник","period":"период"}]}
            status: "completed" — данные найдены; "partial" — часть нужных данных получить не удалось (инструмент не ответил, нет доступа к части
            источников); "no_data" — поиск инструментами выполнен, а нужного в данных нет (в summary — что и где проверено). Сбой инструмента и «нет
            доступа» — не no_data: напиши о них в summary. Идущий месяц или сегодняшний день с данными по дату последней синхронизации — не partial,
            а completed: дату данных («по 30.10») укажи в summary. Разницы, доли и рост по регионам, ТП и категориям бери из результатов
            инструментов, где сравнение уже посчитано кодом, а не считай по одной.
            """);
        return sb.ToString();
    }

    internal static AgentResult Parse(AgentConfig agent, string? text, IReadOnlyList<string> toolsUsed, IReadOnlyList<DataSource> toolSources)
    {
        var json = JsonText.ExtractObject(text);
        // JSON обрезан (лимит длины ответа): целые поля — summary, дописанные находки и показатели — сохраняются, итог — partial (truncated).
        var truncated = false;
        if (json is null && JsonText.CloseTruncated(text) is { } closed)
        {
            json = JsonText.ExtractObject(closed);
            truncated = json is not null;
        }

        if (json is null)
        {
            return new AgentResult(agent.Code, agent.Name, "partial", text?.Trim() ?? string.Empty, [], [], [], [], toolSources, toolsUsed, Reason: "unparsed");
        }

        var root = json.Value;
        var status = truncated ? "partial" : Str(root, "status")?.Trim().ToLowerInvariant() switch
        {
            "no_data" => "no_data",
            "partial" => "partial",
            _ => "completed",
        };

        var sources = toolSources.ToList();
        foreach (var s in Array(root, "data_sources"))
        {
            var source = s.ValueKind == JsonValueKind.String
                ? new DataSource(s.GetString() ?? string.Empty)
                : new DataSource(Str(s, "title") ?? string.Empty, Str(s, "period"));
            if (source.Title.Length > 0 && !sources.Any(x => x.Title == source.Title && x.Period == source.Period))
            {
                sources.Add(source);
            }
        }

        return new AgentResult(
            agent.Code,
            agent.Name,
            status,
            Str(root, "summary") ?? string.Empty,
            Strings(root, "findings"),
            Array(root, "metrics")
                .Select(m => m.ValueKind == JsonValueKind.Object
                    ? new Metric(Str(m, "name") ?? string.Empty, Str(m, "value") ?? Raw(m, "value") ?? string.Empty, Str(m, "unit"))
                    : new Metric(m.ToString(), string.Empty))
                .Where(m => m.Name.Length > 0)
                .ToList(),
            Strings(root, "problems"),
            Strings(root, "recommendations"),
            sources,
            toolsUsed,
            Reason: truncated ? "truncated" : null);
    }

    /// <summary>
    /// Статус итога — по тому, что на самом деле произошло, а не по осторожности модели. partial без сбоев инструментов, неподтверждённых чисел,
    /// обрезки, прерывания и текста вне JSON — completed: идущий месяц или день с данными по дату последней синхронизации Linko — обычное
    /// состояние, а не «ответ неполный» (с ним каждый ответ о текущем месяце начинался с «Ответ неполный»). Дата данных остаётся в summary.
    /// </summary>
    internal static AgentResult Settle(AgentResult r, IReadOnlyList<ToolIssue> issues) =>
        r.Status == "partial" && r.Reason is null && issues.Count == 0 && r.Unverified is not { Count: > 0 } ? r with { Status = "completed" } : r;

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? Raw(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.Number ? v.GetRawText() : null;

    private static IEnumerable<JsonElement> Array(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];

    private static IReadOnlyList<string> Strings(JsonElement e, string name) =>
        Array(e, name).Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? string.Empty : x.ToString()).Where(x => x.Length > 0).ToList();

    private static AgentRun Failed(string code, string name, string status, string message, string? reason = null) =>
        new(new AgentResult(code, name, status, message, [], [], [], [], [], [], message, Reason: reason), AiTokenUsage.None, null);

    /// <summary>
    /// Статус итога по тому, что на самом деле произошло с инструментами данных. dataResults — сколько их вызовов вернули данные, attempts — сколько
    /// было вызовов вообще (по умолчанию — данные и сбои). Ни одного результата и сбой инструмента — failed (tool_error): «данных нет» здесь было
    /// бы неправдой, какой бы статус ни назвала модель. Для no_data без данных: все отказы — «нет доступа» — forbidden; источник не подключён —
    /// source_not_connected; период вне доступного — out_of_range; инструменты данных не вызывались — not_in_onebase (сотрудник не искал: такой
    /// информации нет среди его источников); иначе — not_found (искал и не нашёл). Сбои инструментов при полученных данных — в ToolErrors.
    /// </summary>
    internal static AgentResult Classify(AgentResult result, int dataResults, IReadOnlyList<ToolIssue> issues, int? attempts = null)
    {
        var withIssues = issues.Count > 0 ? result with { ToolErrors = issues.ToList() } : result;
        if (dataResults == 0 && issues.Any(i => i.Kind == ToolFailures.ToolError))
        {
            var failed = string.Join(", ", issues.Where(i => i.Kind == ToolFailures.ToolError).Select(i => i.Tool).Distinct());
            return withIssues with
            {
                Status = "failed",
                Reason = "tool_error",
                Error = $"Инструменты завершились ошибкой ({failed}) — данные не получены. Это сбой источника, а не отсутствие данных.",
            };
        }

        if (result.Status != "no_data")
        {
            return withIssues;
        }

        if (dataResults == 0 && issues.Count > 0 && issues.All(i => i.Kind == ToolFailures.Forbidden))
        {
            return withIssues with { Status = "forbidden", Reason = "no_access", Error = "Нет доступа: у пользователя нет прав на нужные данные." };
        }

        if ((dataResults == 0 && issues.Any(i => i.Kind is ToolFailures.NotConnected or ToolFailures.Forbidden))
            || issues.Any(i => i.Kind == ToolFailures.NotConnected && i.Tool == SourceOfTruthTool.ToolName))
        {
            return withIssues with { Reason = "source_not_connected", Error = result.Error ?? "Нужный источник данных не подключён или закрыт для этого сотрудника." };
        }

        if (dataResults == 0 && issues.FirstOrDefault(i => i.Kind == ToolFailures.OutOfRange) is { } range)
        {
            return withIssues with { Reason = "out_of_range", Error = range.Message };
        }

        if ((attempts ?? dataResults + issues.Count) == 0)
        {
            return withIssues with { Reason = "not_in_onebase" };
        }

        return withIssues with { Reason = "not_found" };
    }

    /// <summary>
    /// Итог partial после сбоя: что уже вернули инструменты данных (коротко), их источники и сбои. Числа этих данных уже в доказательствах
    /// сотрудника — консультант может на них опереться.
    /// </summary>
    internal static AgentResult Salvage(AgentConfig agent, string reason, IReadOnlyList<(string Title, string Content)> collected, IReadOnlyList<DataSource> sources,
        IReadOnlyList<string> toolsUsed, IReadOnlyList<ToolIssue> issues)
    {
        var findings = new List<string>();
        var left = SalvageTotalChars;
        foreach (var (title, content) in collected)
        {
            if (left <= 0)
            {
                break;
            }

            var size = Math.Min(Math.Min(SalvageItemChars, left), content.Length);
            findings.Add($"{title}: {content[..size]}{(size < content.Length ? "…" : "")}");
            left -= size;
        }

        return new AgentResult(agent.Code, agent.Name, "partial", $"{reason}. Ниже — данные, которые инструменты успели вернуть.", findings, [], [], [],
            sources, toolsUsed, reason, Reason: "interrupted", ToolErrors: issues.Count > 0 ? issues.ToList() : null);
    }

    /// <summary>
    /// Источник показателя не подключён по ответу get_source_of_truth: показатель найден, а его freshness — «таблица не привязана» (not_bound) или
    /// «ещё не загружалось» (never), или последняя загрузка с ошибкой без единой удачной. null — источник в порядке или показатель не назван.
    /// </summary>
    internal static ToolIssue? SourceIssue(string map)
    {
        try
        {
            using var doc = JsonDocument.Parse(map);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("found", out var found) || found.ValueKind != JsonValueKind.True
                || !root.TryGetProperty("freshness", out var f) || f.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var status = f.TryGetProperty("status", out var st) ? st.GetString() : null;
            var updated = f.TryGetProperty("updatedAt", out var u) && u.ValueKind == JsonValueKind.String;
            if (status is "not_bound" or "never" || (status == "error" && !updated))
            {
                var label = f.TryGetProperty("label", out var l) ? l.GetString() : null;
                var source = f.TryGetProperty("source", out var src) ? src.GetString() : null;
                var what = status switch
                {
                    "not_bound" => "таблица не привязана",
                    "never" => "ещё не загружалось",
                    _ => "загрузка не удалась",
                };
                return new ToolIssue(SourceOfTruthTool.ToolName, ToolFailures.NotConnected, $"{label ?? "Показатель"}: {source ?? "источник"} — {what}.");
            }
        }
        catch (JsonException)
        {
            // не JSON — источник не определён
        }

        return null;
    }

    /// <summary>Текст сбоя из структурного ответа инструмента (ToolResult.Failure) или сам ответ.</summary>
    private static string ToolMessage(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m) && m.GetString() is { } text)
            {
                return text;
            }
        }
        catch (JsonException)
        {
            // не JSON — текст как есть
        }

        return content.Length <= 300 ? content : content[..300] + "…";
    }
}
