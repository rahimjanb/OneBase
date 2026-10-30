using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Knowledge;
using OneBase.AI.Llm;
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

/// <summary>Evidence — числа из результатов инструментов и точных расчётов сотрудника: доказательства для проверки ответа консультанта.</summary>
public sealed record AgentRun(AgentResult Result, AiTokenUsage Usage, string? Model, EvidenceSet? Evidence = null);

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
    ILogger<AgentRunner> logger)
{
    private const int MaxSteps = 8;

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
        var reported = false;
        string? model = null;
        var options = new AiCallOptions
        {
            Model = task.Model ?? agent.Model,
            Temperature = agent.Temperature,
            MaxOutputTokens = agent.MaxOutputTokens,
            JsonOutput = true,
            Context = new AiCallContext(task.UserId, agent.Code, task.ConversationId, "agent"),
        };

        try
        {
            for (var step = 0; step < MaxSteps; step++)
            {
                // На последнем шаге инструменты вызывать нельзя — агент должен подвести итог по тому, что уже получил. Сам список инструментов
                // не меняется: они есть в истории диалога, а у Claude смена набора инструментов посреди диалога сбрасывает его рассуждения.
                var last = step == MaxSteps - 1;
                var response = await gateway.CompleteAsync(messages, definitions, last ? options with { AllowToolCalls = false } : options, ct);
                usage = usage.Add(response.Usage);
                model = response.Model.ToString();

                if (response.ToolCalls.Count == 0)
                {
                    var result = Parse(agent, response.Text, toolsUsed, sources);
                    var check = EvidenceCheck.Run(ResultText(result), evidence, task.Task);
                    if (!check.Grounded && !revised && !last && tools.Count > 0)
                    {
                        // Evidence Check сотрудника: числа, которых нет в результатах инструментов, — пересчитать (calculate), получить или убрать.
                        revised = true;
                        messages.Add(response.Reply);
                        messages.Add(new LlmMessage(LlmRole.User, $"""
                            Проверка итога по данным: эти числа не найдены в результатах инструментов: {string.Join("; ", check.Unsupported)}.
                            Производное число (сумма, разница, доля, рост, среднее) посчитай инструментом {Calculator.ToolName}; недостающее — получи
                            инструментом данных; чего нет в данных — убери или пометь как неподтверждённое. Затем верни исправленный итоговый JSON.
                            """));
                        continue;
                    }

                    var final = check.Grounded ? result : result with { Unverified = check.Unsupported };
                    return new AgentRun(Classify(final, collected.Count, issues, attempts), usage, model, evidence);
                }

                messages.Add(response.Reply);
                foreach (var call in response.ToolCalls)
                {
                    var isReport = call.Name == ReportSpec.ToolName;
                    var title = isReport ? ReportSpec.ToolTitle
                        : call.Name == Calculator.ToolName ? "Точный расчёт"
                        : call.Name == SourceOfTruthTool.ToolName ? "Источник истины"
                        : call.Name == ToolResultPager.ToolName ? "Следующие строки результата"
                        : tools.FirstOrDefault(t => t.Name == call.Name)?.Title ?? call.Name;
                    if (progress is not null)
                    {
                        await progress(new ConsultantProgress("agent", agent.Code, agent.Name, ProgressStatus.Running, title));
                    }

                    ToolResult result;
                    var isData = false;
                    try
                    {
                        if (call.Name == Calculator.ToolName && tools.Count > 0)
                        {
                            var (calculated, value) = Calculator.Run(call.Arguments);
                            if (value is { } v)
                            {
                                evidence.Add(v);
                            }

                            result = value is null ? ToolResult.Error(calculated) : ToolResult.Ok(calculated);
                        }
                        else if (call.Name == SourceOfTruthTool.ToolName && tools.Count > 0)
                        {
                            var map = await SourceOfTruthTool.RunAsync(call.Arguments, truth, ct);
                            evidence.AddContent(map);
                            result = ToolResult.Ok(map);
                            // Источник истины нашёл, что таблица не привязана или не загружалась: это «источник не подключён», а не «не искали».
                            if (SourceIssue(map) is { } sourceIssue)
                            {
                                issues.Add(sourceIssue);
                            }
                        }
                        else if (call.Name == ToolResultPager.ToolName && tools.Count > 0)
                        {
                            result = pager.Page(call.Arguments, Budget(used));
                        }
                        else if (!isReport && pager.TryRepeat(ToolResultPager.CallKey(call.Name, call.Arguments), out _, out var earlier))
                        {
                            // Тот же вызов с теми же аргументами уже был: данные не запрашиваются заново — модель берёт их из прошлого результата.
                            result = ToolResult.Ok(JsonSerializer.Serialize(new
                            {
                                repeat = true,
                                result_id = earlier,
                                message = "Этот вызов с теми же аргументами уже выполнен в этом анализе — его результат выше. Используй его" +
                                          (earlier is null ? "." : $"; следующие строки — {ToolResultPager.ToolName}(result_id = {earlier})."),
                            }));
                        }
                        else if (!isReport)
                        {
                            isData = true;
                            attempts++;
                            result = await executor.ExecuteAsync(context, call.Name, call.Arguments, ct);
                            if (result.Success)
                            {
                                evidence.AddContent(result.Content);
                            }
                        }
                        else if (reportTool is null || reported)
                        {
                            result = ToolResult.Error(reportTool is null
                                ? "Отчёт Excel формируется только по просьбе пользователя — ответь текстом."
                                : "Отчёт уже сформирован — второй не нужен. Подведи итог.");
                        }
                        else
                        {
                            (result, _) = await reports.CreateAsync(context, call.Arguments, ct);
                            reported = result.Success;
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
                    {
                        // Расчёт, источник истины и отчёт — не через ToolExecutor: их сбой тоже не должен останавливать сотрудника.
                        logger.LogError(ex, "AI: {Tool} сотрудника {Agent} завершился ошибкой", call.Name, agent.Code);
                        result = ToolResult.Failure(call.Name, ToolFailures.KindOf(ex), ToolFailures.SafeMessage(ex));
                    }

                    if (isData && result.Success)
                    {
                        collected.Add((title, result.Content));
                    }
                    else if (isData && (result.ErrorKind ?? (ToolFailures.IsOutOfRange(result.Content) ? ToolFailures.OutOfRange : null)) is { } kind
                             && kind != ToolFailures.InvalidArguments)
                    {
                        // Ошибка аргументов об ограничении периода («Период — не больше 62 дней») — тоже итог: период вне доступного.
                        issues.Add(new ToolIssue(call.Name, kind, ToolMessage(result.Content)));
                    }

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
            logger.LogWarning(ex, "AI: сотрудник {Agent} прерван после {Count} результатов инструментов — итог по полученным данным", agent.Code, collected.Count);
            var reason = ex is AiProviderException or LlmNotConfiguredException ? ex.Message : "внутренняя ошибка OneBase";
            return new AgentRun(Salvage(agent, $"Анализ прерван: {reason}", collected, sources, toolsUsed, issues), usage, model, evidence);
        }

        return collected.Count > 0
            ? new AgentRun(Salvage(agent, "Агент не завершил анализ за отведённое число шагов.", collected, sources, toolsUsed, issues), usage, model, evidence)
            : new AgentRun(new AgentResult(agent.Code, agent.Name, "failed", "Превышен лимит шагов агента.", [], [], [], [], sources, toolsUsed,
                "Агент не завершил анализ за отведённое число шагов.", Reason: "interrupted", ToolErrors: issues.Count > 0 ? issues : null), usage, model, evidence);
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
            status: "completed" — данные найдены; "partial" — часть данных или часть источников не ответила; "no_data" — поиск инструментами
            выполнен, а нужного в данных нет (в summary — что и где проверено). Сбой инструмента и «нет доступа» — не no_data: напиши о них в summary.
            """);
        return sb.ToString();
    }

    internal static AgentResult Parse(AgentConfig agent, string? text, IReadOnlyList<string> toolsUsed, IReadOnlyList<DataSource> toolSources)
    {
        var json = JsonText.ExtractObject(text);
        if (json is null)
        {
            return new AgentResult(agent.Code, agent.Name, "partial", text?.Trim() ?? string.Empty, [], [], [], [], toolSources, toolsUsed);
        }

        var root = json.Value;
        var status = Str(root, "status")?.Trim().ToLowerInvariant() switch
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
            toolsUsed);
    }

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
