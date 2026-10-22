using System.Text;
using System.Text.Json;
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
/// одна попытка пересчитать (calculate) или убрать, оставшиеся — в Unverified результата.
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
    TimeProvider clock)
{
    private const int MaxSteps = 8;

    /// <summary>Сколько символов результата инструмента уходит модели — большие таблицы режутся.</summary>
    private const int MaxToolResultChars = 16_000;

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
            return Failed(agent.Code, agent.Name, "forbidden", "У вас нет доступа к данным этого отдела.");
        }

        // Только инструменты данных: взаимодействие отделов идёт через главного консультанта, а не делегированием.
        var tools = (await executor.GetAllowedToolsAsync(agent.Code, task.UserId, ct)).Where(t => t.Source is not null).ToList();

        // Пока в базе знаний нет документов, доступных пользователю, поиск по ней не предлагается: он всегда пуст,
        // а каждый вызов — лишний круг к модели. С первым проиндексированным документом поиск включается сам.
        if (tools.Any(t => t.Source == KnowledgeSources.Documents) && (await knowledge.AllowedDocumentsAsync(task.UserId, ct)).Count == 0)
        {
            tools = tools.Where(t => t.Source != KnowledgeSources.Documents).ToList();
        }
        var definitions = tools.Select(t => new LlmToolDefinition(t.Name, t.Description, t.InputSchema)).ToList();

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
        }

        // Словарь названий компании — только сотрудникам с данными продаж: иначе «помадка» в вопросе для модели просто незнакомое слово.
        var glossaryText = tools.Any(t => t.Source?.StartsWith("sales.", StringComparison.Ordinal) == true) ? await glossary.TextAsync(ct) : null;
        var messages = new List<LlmMessage>
        {
            new(LlmRole.System, SystemPrompt(agent, tools, glossaryText, reportTool is not null)),
            new(LlmRole.User, task.Context is null ? task.Task : $"{task.Task}\n\nКонтекст от главного консультанта:\n{task.Context}"),
        };

        var context = new ToolContext(agent.Code, task.UserId, 0, task.ConversationId);
        var usage = AiTokenUsage.None;
        var toolsUsed = new List<string>();
        var sources = new List<DataSource>();
        var evidence = new EvidenceSet();
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
                        инструментом данных; чего нет в данных — убери или напиши, что данных нет. Затем верни исправленный итоговый JSON.
                        """));
                    continue;
                }

                return new AgentRun(check.Grounded ? result : result with { Unverified = check.Unsupported }, usage, model, evidence);
            }

            messages.Add(response.Reply);
            foreach (var call in response.ToolCalls)
            {
                var isReport = call.Name == ReportSpec.ToolName;
                var title = isReport ? ReportSpec.ToolTitle
                    : call.Name == Calculator.ToolName ? "Точный расчёт"
                    : call.Name == SourceOfTruthTool.ToolName ? "Источник истины"
                    : tools.FirstOrDefault(t => t.Name == call.Name)?.Title ?? call.Name;
                if (progress is not null)
                {
                    await progress(new ConsultantProgress("agent", agent.Code, agent.Name, ProgressStatus.Running, title));
                }

                ToolResult result;
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
                }
                else if (!isReport)
                {
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

                var content = result.Content.Length > MaxToolResultChars
                    ? result.Content[..MaxToolResultChars] + "\n…(обрезано: запросите данные уже, с фильтрами)"
                    : result.Content;
                messages.Add(new LlmMessage(LlmRole.Tool, result.Success ? content : $"ОШИБКА: {content}", ToolCallId: call.Id));
            }
        }

        return new AgentRun(new AgentResult(agent.Code, agent.Name, "failed", "Превышен лимит шагов агента.", [], [], [], [], sources, toolsUsed,
            "Агент не завершил анализ за отведённое число шагов."), usage, model, evidence);
    }

    /// <summary>Утверждения итога сотрудника для проверки по данным: вывод, факты, показатели и проблемы (рекомендации — предложения, не факты).</summary>
    internal static string ResultText(AgentResult r) =>
        string.Join("\n", new[] { r.Summary }
            .Concat(r.Findings)
            .Concat(r.Metrics.Select(m => $"{m.Name}: {m.Value} {m.Unit}".Trim()))
            .Concat(r.Problems));

    private string SystemPrompt(AgentConfig agent, IReadOnlyList<ITool> tools, string? glossaryText, bool report = false)
    {
        var today = clock.GetUtcNow().ToOffset(CompanyOffset);
        var sb = new StringBuilder();
        sb.AppendLine(agent.Prompt);
        sb.AppendLine();
        sb.AppendLine($"Сегодня {today:dd.MM.yyyy}, {today:HH:mm} по Ташкенту. Компания работает в Узбекистане, суммы — в сумах (UZS), вес — в кг.");
        sb.AppendLine(tools.Count == 0
            ? "Инструментов с данными OneBase у тебя сейчас нет — значит, данных для ответа нет."
            : $"Бери цифры только из результатов инструментов. Вызывай инструменты столько раз, сколько нужно, с точными фильтрами (период, регион). " +
              $"Любое число, которого нет в результатах (сумма, разница, доля, рост, среднее), считай инструментом {Calculator.ToolName} — каждое число итога " +
              $"сверяется с данными. Если значения из разных источников расходятся или пользователь ссылается на другой источник — вызови " +
              $"{SourceOfTruthTool.ToolName}: верно значение источника-владельца, о расхождении скажи.");
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
        sb.AppendLine(ConsultantPrompts.QualityRules);
        sb.AppendLine();
        sb.AppendLine($$"""
            Когда данных достаточно, верни итог ОДНИМ JSON-объектом (без Markdown и текста вокруг):
            {"agent":"{{agent.Code}}","status":"completed | partial | no_data","summary":"2–3 предложения — главный вывод",
             "findings":["факт с цифрой, единицей и периодом"],"metrics":[{"name":"...","value":"...","unit":"..."}],
             "problems":["проблема и её масштаб"],"recommendations":["конкретное действие на основе фактов"],
             "data_sources":[{"title":"источник","period":"период"}]}
            status = "no_data", если нужных данных в OneBase нет; "partial" — если есть только часть.
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

    private static AgentRun Failed(string code, string name, string status, string message) =>
        new(new AgentResult(code, name, status, message, [], [], [], [], [], [], message), AiTokenUsage.None, null);
}
