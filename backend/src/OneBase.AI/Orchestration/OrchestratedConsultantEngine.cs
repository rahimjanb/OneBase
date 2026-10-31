using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Knowledge;
using OneBase.AI.Llm;
using OneBase.AI.Memory;
using OneBase.AI.Monitoring;
using OneBase.AI.Providers;
using OneBase.AI.Reports;
using OneBase.AI.Security;
using OneBase.AI.Tools;

namespace OneBase.AI.Orchestration;

/// <summary>
/// Главный AI-консультант: вопрос → маршрутизация → AI-сотрудники параллельно → единый управленческий ответ.
/// AI-сотрудники без доступа у пользователя не привлекаются — ни без права на отдел, ни без единого инструмента данных для этого пользователя;
/// консультант прямо говорит, каких данных не видит («нет доступа», «источник не подключён»), а не «данных нет».
/// </summary>
public sealed partial class OrchestratedConsultantEngine(
    AiAgentStore agents,
    IUserPermissions permissions,
    AiRouter router,
    AgentOrchestrator orchestrator,
    IAiGateway gateway,
    AiMemoryStore memory,
    ReportService reports,
    ToolExecutor executor,
    KnowledgeService knowledge,
    IToolRegistry tools,
    TimeProvider clock,
    ILogger<OrchestratedConsultantEngine> logger,
    IAiSettingsSource aiSettings,
    IModelComparisons? comparisons = null) : IConsultantEngine
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<ConsultantAnswer> AnswerAsync(ConsultantTurn turn, Func<ConsultantProgress, Task> progress, CancellationToken ct)
    {
        var startedAt = clock.GetUtcNow();
        var turnTimings = AiTurnTimings.Current;
        var contextStage = turnTimings?.Stage("context");
        // Отчёт Excel — только когда пользователь сам его просит: иначе инструмента отчёта у сотрудников нет. Канал может отчёты запретить.
        var asksReport = ReportIntent.Requested(turn.Question);
        var wantsReport = asksReport && (turn.Channel?.AllowReports ?? true);
        var all = await agents.ListAsync(ct);
        var consultant = all.FirstOrDefault(a => a.IsConsultant);
        if (consultant is { Enabled: false })
        {
            throw new LlmNotConfiguredException("Главный консультант выключен администратором в «Настройки → AI → Агенты».");
        }

        var own = await permissions.GetAsync(turn.UserId, ct);
        var departments = all.Where(a => !a.IsConsultant && a.Enabled).ToList();
        var permitted = departments.Where(a => a.RequiredPermission is null || own.Contains(a.RequiredPermission)).ToList();
        // Сотрудник без единого инструмента данных для пользователя не запускается: иначе он ответил бы «данных нет» вместо «нет доступа».
        var access = await DataAccessAsync(permitted, turn.UserId, ct);
        var available = permitted.Where(a => access.Sources[a.Code].Count > 0).ToList();
        var unconnected = permitted.Where(a => access.Granted[a.Code].Count > 0 && access.Sources[a.Code].Count == 0).ToList();
        var forbidden = departments.Except(available).Except(unconnected).ToList();
        var context = new AiCallContext(turn.UserId, AgentDefaults.Consultant, turn.ConversationId, turn.Channel?.Name ?? "consultant");

        // Аналитическая память чата: что сотрудники уже выяснили (только доступные пользователю сейчас).
        var names = all.ToDictionary(a => a.Code, a => a.Name);
        var remembered = await memory.ConversationAsync(turn.UserId, turn.ConversationId, available.Select(a => a.Code).ToList(), 8, ct);
        var rememberedText = remembered.Count == 0 ? null : AiMemoryStore.Format(remembered, names);
        if (remembered.Count > 0)
        {
            await progress(new ConsultantProgress("memory", null, null, ProgressStatus.Done, $"прошлых анализов в чате: {remembered.Count}"));
        }

        // Correction Guard: вопрос оспаривает прошлый ответ — это не доказательство, а повод перепроверить данные заново.
        var correction = CorrectionGuard.Detect(turn.Question, turn.History);

        contextStage?.Dispose();
        await progress(new ConsultantProgress("routing", null, null, ProgressStatus.Running));
        RoutingDecision routing;
        using (turnTimings?.Stage("routing"))
        {
            routing = Enforce(await router.RouteAsync(turn.Question, turn.History, available, context, ct, rememberedText), turn, correction, available,
                await router.SalesWordsAsync(available, ct), access.Sources, [.. forbidden, .. unconnected]);
        }

        await progress(new ConsultantProgress("routing", null, null, ProgressStatus.Done,
            routing.Agents.Count == 0 ? "без AI-сотрудников" : string.Join(", ", routing.Agents.Select(c => available.First(a => a.Code == c).Name))));
        if (correction is not null)
        {
            var recheck = CorrectionGuard.AgentTask(correction);
            routing = routing with
            {
                Tasks = routing.Agents.ToDictionary(code => code, code => $"{routing.Tasks.GetValueOrDefault(code) ?? turn.Question}\n\n{recheck}"),
            };
        }

        var chosen = available.Where(a => routing.Agents.Contains(a.Code)).ToList();
        var contexts = new Dictionary<string, string?>();
        var memoryUsed = remembered.Count;
        using (turnTimings?.Stage("agent.memory"))
        {
            foreach (var a in chosen)
            {
                var earlier = await memory.AgentAsync(turn.UserId, a.Code, turn.ConversationId, 3, ct);
                memoryUsed += earlier.Count;
                contexts[a.Code] = AgentContext(turn.History, remembered.Where(m => m.AgentCode == a.Code).ToList(), earlier);
            }
        }

        // Маршрутизация моделей (сотрудники, итоговый ответ, перепроверка) и общий план перепроверки ответа (review): повторы сотрудников и
        // исправления итога — из одного счётчика; новая попытка — только пока вызовы ответа стоят меньше 75 % бюджета вопроса.
        var aiConfig = await aiSettings.GetSettingsAsync(ct);
        var modelRouting = aiConfig.ModelRouting;
        var reviewPlan = modelRouting.On && modelRouting.Review
            ? new ReviewPlan(modelRouting.ReviewMaxAttempts,
                () => modelRouting.QuestionBudgetUsd is not { } limit || turnTimings is null || turnTimings.CostUsd < limit * 0.75m)
            : null;

        var agentsStage = turnTimings?.Stage("agents");
        var (runs, agentUsage) = chosen.Count == 0
            ? ([], AiTokenUsage.None)
            : await orchestrator.RunAsync(chosen, routing.Tasks, turn.Question, contexts, turn.UserId, turn.ConversationId, progress, ct, wantsReport, turn.Channel?.AgentModel,
                reviewPlan);
        agentsStage?.Dispose();
        var created = wantsReport ? await reports.CreatedAsync(turn.UserId, turn.ConversationId, startedAt, ct) : [];

        await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Running));
        var results = runs.Select(r => r.Result).Concat(Denied(routing.Denied, all, access.Granted, unconnected)).ToList();
        var evidence = Evidence(runs, turn, correction, rememberedText);
        var review = correction is null ? null : CorrectionGuard.Review(correction, evidence, turn.LastAnswerId);
        // Два системных сообщения: неизменные правила ответа (кэшируются у провайдера между вопросами) и данные этого вопроса.
        List<LlmMessage> messages =
        [
            new(LlmRole.System, ComposeRules(consultant)),
            new(LlmRole.System, ComposePrompt(results, forbidden, rememberedText, wantsReport ? created : null, turn.Channel, asksReport && !wantsReport, review,
                AnswerLanguages.Of(turn.Question))),
            .. turn.History,
            new(LlmRole.User, turn.Question),
        ];
        // Модель итогового ответа: канала, консультанта в «Агентах», по сложности вопроса (dynamic) или основная. Сложность — правилами по
        // уже полученным данным, без отдельного вызова модели.
        var toolErrors = runs.Sum(r => r.ToolErrors?.Count ?? 0);
        var complexity = AnswerComplexityRules.Assess(turn.Question, turn.History, runs, routing, correction is not null, toolErrors);
        var finalChoice = AiModelRouting.Final(aiConfig, turn.Channel?.Model, consultant?.Model, complexity);
        if (reviewPlan is not null)
        {
            reviewPlan.Escalation = AiModelRouting.Escalation(modelRouting, finalChoice.Model ?? aiConfig.Primary);
        }

        // Ответ сверяется с данными: неподтверждённые числа — одна попытка исправить, затем пометка в тексте (review — см. GroundedComposer).
        var composeStage = turnTimings?.Stage("compose");
        var answer = await new GroundedComposer(gateway).ComposeAsync(messages, new AiCallOptions
        {
            Model = finalChoice.Model,
            ModelReason = finalChoice.Reason,
            Temperature = consultant?.Temperature,
            // Лимит канала или консультанта из «Агентов»; не задан — не меньше, чем у сотрудников: подробный ответ по нескольким разрезам
            // (регионы, ассортимент, визиты) занимает 3,5 тыс. токенов и упирался в общий лимит 4096.
            MaxOutputTokens = turn.Channel?.MaxOutputTokens ?? consultant?.MaxOutputTokens ?? Math.Max(aiConfig.MaxOutputTokens, AgentRunner.AgentOutputTokens),
            // Данные и история — свои у каждого вопроса: кэшируются только правила, запись хвоста в кэш окупилась бы лишь при перепроверке.
            CacheConversation = false,
            Context = context with { Purpose = "consultant.answer" },
        }, evidence, turn.Question, ct, correction?.PreviousAnswer, turn.Draft, reviewPlan);
        composeStage?.Dispose();
        RecordRouting(turn, turnTimings, modelRouting, complexity, runs, finalChoice, answer);
        await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Done,
            answer.Grounding.Checked == 0 ? null : $"чисел сверено с данными: {answer.Grounding.Checked}"));

        // Память — вспомогательная: её сбой не должен отнимать у пользователя готовый ответ.
        try
        {
            using var memoryStage = turnTimings?.Stage("memory.save");
            await memory.SaveAsync(turn.UserId, turn.ConversationId, routing.Tasks, turn.Question, results, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Консультант: не удалось сохранить память чата {ConversationId}", turn.ConversationId);
        }

        var sources = new List<DataSource>();
        foreach (var s in results.SelectMany(r => r.DataSources))
        {
            if (!sources.Any(x => x.Title == s.Title && x.Period == s.Period))
            {
                sources.Add(s);
            }
        }

        return new ConsultantAnswer(
            answer.Text,
            new ConsultantDetails(results, sources, answer.Model.ToString(), answer.UsedFallback, routing.Reason.Length > 0 ? routing.Reason : null, memoryUsed,
                created.Count > 0 ? created : null, answer.Grounding, review),
            routing.Usage.Add(agentUsage).Add(answer.Usage));
    }

    /// <summary>
    /// Маршрутизация моделей ответа — в замер (AI-журнал): режим, сложность, модели и причины. «Сравнение»: что выбрали бы правила, и запись в
    /// журнал сравнения с прогнозом стоимости — те же токены по ценам выбранной правилами модели (оценка: у другой модели токены были бы
    /// другими). Второго ответа модели нет — сравнение ничего не тратит.
    /// </summary>
    private void RecordRouting(ConsultantTurn turn, AiTurnTimings? timings, AiModelRoutingSettings routingSettings, AnswerComplexity complexity,
        IReadOnlyList<AgentRun> runs, AiModelChoice finalChoice, GroundedAnswer answer)
    {
        if (timings is null)
        {
            return;
        }

        var agentModel = runs.Select(r => r.Model).OfType<string>().FirstOrDefault();
        var plannedAgent = AiModelRouting.PlannedAgent(routingSettings);
        var plannedFinal = AiModelRouting.PlannedFinal(routingSettings, complexity);
        timings.Routing = new AiRoutingNote(routingSettings.Mode, complexity, agentModel, runs.Select(r => r.ModelReason).OfType<string>().FirstOrDefault() ?? "сотрудники не привлекались",
            answer.Model.ToString(), finalChoice.Reason, plannedAgent.Model?.ToString(), plannedFinal.Model?.ToString(), plannedFinal.Reason);
        if (!routingSettings.Shadow || comparisons is null)
        {
            return;
        }

        var calls = timings.View().Calls.Where(c => c.Success).ToList();
        AiTokenUsage Sum(Func<AiLlmTiming, bool> which) => calls.Where(which)
            .Aggregate(AiTokenUsage.None, (sum, c) => sum.Add(new AiTokenUsage(c.InputTokens, c.OutputTokens, c.CacheReadTokens, c.CacheWriteTokens)));
        var expected = $"сложность {complexity.Score}: {(complexity.Signals.Count == 0 ? "простой вопрос" : string.Join(", ", complexity.Signals))}";
        _ = Task.Run(async () =>
        {
            AiTurnTimings.Detach();
            try
            {
                if (agentModel is not null && plannedAgent.Model is { } planned)
                {
                    var usage = Sum(c => c.Purpose == "agent");
                    await comparisons.RecordAsync(AiTaskCategory.Answer, "shadow", "agent", turn.Question, expected,
                        new ComparisonSide(agentModel, agentModel, true, usage, calls.Where(c => c.Purpose == "agent").Sum(c => c.ProviderMs)),
                        new ComparisonSide(planned.ToString(), planned.ToString(), true, usage, 0), agentModel == planned.ToString() ? 1 : 0, null, null);
                }

                if (plannedFinal.Model is { } final)
                {
                    var usage = Sum(c => c.Purpose is "consultant.answer" or "consultant.revise" or "consultant.review");
                    await comparisons.RecordAsync(AiTaskCategory.Answer, "shadow", "final", turn.Question, expected,
                        new ComparisonSide(answer.Model.ToString(), answer.Model.ToString(), true, usage,
                            calls.Where(c => c.Purpose.StartsWith("consultant.", StringComparison.Ordinal) && c.Purpose != "consultant.route").Sum(c => c.ProviderMs)),
                        new ComparisonSide(final.ToString(), final.ToString(), true, usage, 0), answer.Model == final ? 1 : 0, null, null);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Консультант: сравнение маршрутизации моделей не записано");
            }
        });
    }

    /// <summary>
    /// Data Retrieval: ответ о фактах — только по данным. Без AI-сотрудников остаётся только разговор: приветствие, благодарность, прощание,
    /// вопрос о возможностях консультанта. Вопрос о данных, по которому маршрутизатор не привлёк сотрудников, получает их по словам вопроса
    /// (русский и узбекский), словарю продаж (salesWords) и источникам инструментов, доступным пользователю (sources); уточнение без своих слов
    /// («А по Самарканду?») — по прошлому вопросу, с его условиями в задаче. Область, которую пользователю не открыть (unavailable — сотрудники
    /// без прав или без подключённых источников), попадает в Denied: консультант скажет «нет доступа», а не ответит другим отделом. Область не
    /// определилась — один сотрудник с наибольшим доступом к данным; вопрос о компании в целом — все доступные. Возражение к ответу —
    /// перепроверяют сотрудники той же области (по прошлому вопросу и ответу), а если область не определить, но в ответе были числа, — все.
    /// </summary>
    internal static RoutingDecision Enforce(RoutingDecision routing, ConsultantTurn turn, CorrectionRequest? correction, IReadOnlyList<AgentConfig> available,
        IReadOnlyList<string>? salesWords = null, IReadOnlyDictionary<string, IReadOnlySet<string>>? sources = null, IReadOnlyList<AgentConfig>? unavailable = null)
    {
        if (routing.Agents.Count > 0 || (available.Count == 0 && unavailable is not { Count: > 0 }))
        {
            return routing;
        }

        var previous = turn.History.LastOrDefault(m => m.Role == LlmRole.User)?.Content;
        if (correction is not null)
        {
            if (available.Count == 0)
            {
                return routing;
            }

            var agents = AiRouter.KeywordAgents($"{previous}\n{correction.PreviousAnswer}\n{turn.Question}", available, salesWords).ToList();
            if (agents.Count == 0 && NumberClaims.Checkable(correction.PreviousAnswer).Count > 0)
            {
                agents = available.Select(a => a.Code).ToList();
            }

            return agents.Count == 0 ? routing : routing with { Agents = agents, Reason = "Пользователь оспаривает прошлый ответ — AI-сотрудники перепроверяют данные." };
        }

        var followUp = previous is not null && RouteIntent.IsFollowUp(turn.Question);
        if (!followUp && RouteIntent.IsSmallTalk(turn.Question))
        {
            return routing;
        }

        // Область — по вопросу; уточнение без своих слов — по прошлому вопросу.
        var text = turn.Question;
        var (chosen, denied) = RouteIntent.Resolve(text, available, salesWords, sources);
        var inherited = false;
        if (chosen.Count == 0 && denied.Count == 0 && previous is not null)
        {
            (chosen, denied) = RouteIntent.Resolve(previous, available, salesWords, sources);
            inherited = chosen.Count > 0 || denied.Count > 0;
            text = inherited ? previous : text;
        }

        // Отдел вопроса пользователю недоступен целиком — «нет доступа», а не ответ другого отдела.
        var deniedAgents = chosen.Count == 0 && unavailable is { Count: > 0 }
            ? AiRouter.KeywordAgents(text, unavailable, salesWords).Select(code => new DeniedArea(code, null, [])).ToList()
            : [];
        List<DeniedArea> allDenied = [.. denied, .. deniedAgents];
        if (chosen.Count == 0 && allDenied.Count == 0)
        {
            chosen = RouteIntent.IsCompanyWide(text) ? available.Select(a => a.Code).ToList()
                : RouteIntent.DefaultAgent(available, sources) is { } fallback ? [fallback]
                : [];
        }

        if (chosen.Count == 0 && allDenied.Count == 0)
        {
            return routing;
        }

        var tasks = followUp || inherited
            ? chosen.ToDictionary(code => code, _ => $"{previous}\nУточнение: {turn.Question}")
            : new Dictionary<string, string>();
        var reason = chosen.Count == 0 ? "Данные вопроса пользователю недоступны — AI-сотрудники не привлекаются."
            : followUp || inherited ? "Уточнение к прошлому вопросу — AI-сотрудники той же области."
            : "Вопрос о данных компании — ответ только по данным OneBase.";
        return routing with { Agents = chosen, Tasks = tasks, Reason = reason, Denied = allDenied.Count > 0 ? allDenied : null };
    }

    /// <summary>
    /// Доступ пользователя к данным сотрудников: Granted — источники инструментов, на которые у него есть права; Sources — из них те, где есть
    /// данные (база знаний — только при доступных ему документах, как в AgentRunner).
    /// </summary>
    private async Task<(IReadOnlyDictionary<string, IReadOnlySet<string>> Granted, IReadOnlyDictionary<string, IReadOnlySet<string>> Sources)> DataAccessAsync(
        IReadOnlyList<AgentConfig> agents, Guid userId, CancellationToken ct)
    {
        var granted = await executor.DataSourcesAsync(agents.Select(a => a.Code).ToList(), userId, ct);
        var documents = !granted.Values.Any(s => s.Contains(KnowledgeSources.Documents)) || (await knowledge.AllowedDocumentsAsync(userId, ct)).Count > 0;
        var sources = granted.ToDictionary(p => p.Key, IReadOnlySet<string> (p) => documents
            ? p.Value
            : p.Value.Where(s => s != KnowledgeSources.Documents).ToHashSet(StringComparer.Ordinal));
        return (granted, sources);
    }

    /// <summary>
    /// Итоги по областям, которые пользователю не открыть: forbidden (нет прав) или no_data «источник не подключён» (права есть, а данных в
    /// источнике нет — например, ни одного доступного документа). Модель для них не вызывается.
    /// </summary>
    internal static IEnumerable<AgentResult> Denied(IReadOnlyList<DeniedArea>? denied, IReadOnlyList<AgentConfig> all,
        IReadOnlyDictionary<string, IReadOnlySet<string>> granted, IReadOnlyList<AgentConfig> unconnected)
    {
        foreach (var d in denied ?? [])
        {
            var name = all.FirstOrDefault(a => a.Code == d.Agent)?.Name ?? d.Agent;
            var notConnected = d.Title is null
                ? unconnected.Any(a => a.Code == d.Agent)
                : d.Sources.Any(source => granted.Values.Any(g => g.Contains(source)));
            var what = d.Title ?? "данные отдела";
            var (status, reason, message) = notConnected
                ? ("no_data", "source_not_connected", $"Источник не подключён: {what} — в нём нет данных, доступных пользователю.")
                : ("forbidden", "no_access", $"Нет доступа: у пользователя нет прав на {(d.Title is null ? "данные этого отдела" : $"данные «{d.Title}»")}.");
            yield return new AgentResult(d.Agent, d.Title is null ? name : $"{name} · {d.Title}", status, message, [], [], [], [], [], [], message, Reason: reason);
        }
    }

    /// <summary>
    /// Доказательства ответа: числа из результатов инструментов и расчётов всех сотрудников, производные от их подтверждённых показателей,
    /// выводы прошлых анализов (память) и — если сотрудники не привлекались — прошлые ответы чата. При возражении — только свежие данные:
    /// прошлый ответ и есть то, что проверяется.
    /// </summary>
    private static EvidenceSet Evidence(IReadOnlyList<AgentRun> runs, ConsultantTurn turn, CorrectionRequest? correction, string? remembered)
    {
        var evidence = new EvidenceSet();
        foreach (var run in runs)
        {
            if (run.Evidence is { } e)
            {
                evidence.AddAll(e);
            }
        }

        var keys = runs.SelectMany(r => NumberClaims.Find(AgentRunner.ResultText(r.Result)))
            .Where(evidence.Supports)
            .Select(c => c.Value)
            .ToList();
        evidence.AddDerived(keys);
        if (correction is null)
        {
            evidence.AddText(remembered);
            if (runs.Count == 0)
            {
                foreach (var m in turn.History.Where(m => m.Role == LlmRole.Assistant))
                {
                    evidence.AddText(m.Content);
                }
            }
        }

        return evidence;
    }

    /// <summary>
    /// Контекст сотрудника: о чём шла речь в чате, что он уже выяснил в этом чате и его недавние выводы в других чатах
    /// (с пометкой, что цифры могли устареть — актуальные берутся только из инструментов).
    /// </summary>
    private static string? AgentContext(IReadOnlyList<LlmMessage> history, IReadOnlyList<MemoryNote> inChat, IReadOnlyList<MemoryNote> earlier)
    {
        var sb = new StringBuilder();
        if (history.Count > 0)
        {
            sb.AppendLine("Ранее в чате:");
            foreach (var m in history.TakeLast(4))
            {
                var text = m.Content.Length > 400 ? m.Content[..400] + "…" : m.Content;
                sb.AppendLine($"{(m.Role == LlmRole.User ? "Вопрос" : "Ответ")}: {text}");
            }
        }

        if (inChat.Count > 0)
        {
            sb.AppendLine("Ты уже анализировал в этом чате:");
            sb.Append(AiMemoryStore.Format(inChat));
        }

        if (earlier.Count > 0)
        {
            sb.AppendLine("Твои выводы из других чатов (могли устареть — цифры бери только из инструментов):");
            sb.Append(AiMemoryStore.Format(earlier));
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>Неизменная часть инструкций итогового ответа — одинакова для всех вопросов, поэтому читается из кэша промпта провайдера.</summary>
    internal static string ComposeRules(AgentConfig? consultant)
    {
        var sb = new StringBuilder();
        sb.AppendLine(consultant?.Prompt ?? AgentDefaults.Director.Prompt);
        sb.AppendLine();
        sb.AppendLine("Суммы — в сумах, вес — в кг.");
        sb.AppendLine();
        sb.AppendLine(ConsultantPrompts.GroundingPolicy);
        sb.AppendLine();
        sb.AppendLine("Каждое число ответа сверяется кодом с данными AI-сотрудников: число, которого нет в их результатах, будет помечено как не подтверждённое.");
        sb.AppendLine("Числа, которые сотрудник отметил как unverified, — не факт: не приводи их или скажи, что они не подтверждены данными.");
        sb.AppendLine();
        // Правила консультанта — последними: они важнее промпта выше, даже если администратор задал свой.
        sb.AppendLine(ConsultantPrompts.ConsultantRules);
        return sb.ToString();
    }

    /// <summary>
    /// Итоги сотрудников для итогового ответа: у каждого — outcome (одно из восьми состояний AnswerOutcome), готовая фраза для пользователя на
    /// языке вопроса (message), безопасное пояснение (note) и ограничения (limitations). Факты и цифры — только у completed и partial; у сбоя,
    /// «нет прав», «не подключено» и «период» текст модели сотрудника не передаётся (он мог сказать «данных нет»), как и текст ошибки
    /// (исключения, адреса, SQL). title — название инструмента для ограничений (по коду; нет — «источник данных»).
    /// </summary>
    internal static string ComposeData(IReadOnlyList<AgentResult> results, Func<string, string?>? title = null, AnswerLanguage language = AnswerLanguage.Ru) =>
        JsonSerializer.Serialize(results.Select(r =>
        {
            var outcome = AnswerOutcome.Of(r);
            var data = outcome is AnswerOutcome.Completed or AnswerOutcome.Partial;
            var limitations = AnswerOutcome.Limitations(r, title);
            return new
            {
                r.Agent,
                r.AgentName,
                Outcome = outcome,
                Message = AnswerOutcome.Phrase(outcome, language),
                Summary = data || outcome is AnswerOutcome.NotFound or AnswerOutcome.NotInOneBase ? r.Summary : null,
                Note = AnswerOutcome.Note(r, outcome),
                Findings = data ? r.Findings : [],
                Metrics = data ? r.Metrics : [],
                Problems = data ? r.Problems : [],
                Recommendations = data ? r.Recommendations : [],
                DataSources = r.DataSources.Select(s => s.Period is null ? s.Title : $"{s.Title} ({s.Period})"),
                Limitations = limitations.Count > 0 ? limitations : null,
                r.Unverified,
            };
        }), Json);

    /// <summary>Данные этого вопроса для итогового ответа: дата, результаты сотрудников, память чата, отчёты, ограничения доступа.</summary>
    private string ComposePrompt(IReadOnlyList<AgentResult> results, IReadOnlyList<AgentConfig> forbidden, string? remembered,
        IReadOnlyList<ReportRef>? reportsCreated, ConsultantChannel? channel = null, bool reportsForbidden = false, CorrectionReview? review = null,
        AnswerLanguage language = AnswerLanguage.Ru)
    {
        var today = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(5));
        var sb = new StringBuilder();
        sb.AppendLine($"Сегодня {today:dd.MM.yyyy} (Ташкент).");
        sb.AppendLine($"Язык вопроса — {AnswerLanguages.Label(language)}: отвечай на нём.");
        sb.AppendLine();
        if (review is not null)
        {
            sb.AppendLine(CorrectionGuard.ComposeBlock(review));
            sb.AppendLine();
        }

        if (channel?.Instructions is { Length: > 0 } instructions)
        {
            sb.AppendLine(instructions);
            sb.AppendLine();
        }

        if (reportsForbidden)
        {
            sb.AppendLine("Пользователь просит отчёт Excel, но в этом канале отчёты ему не разрешены. Скажи об этом коротко и ответь текстом.");
            sb.AppendLine();
        }

        if (results.Count == 0)
        {
            sb.AppendLine("К этому вопросу AI-сотрудники не привлекались: данные OneBase не запрашивались. Отвечай по истории чата и общим знаниям;");
            sb.AppendLine("если вопрос о показателях компании — скажи, что для ответа нужно уточнить вопрос (период, отдел), и не называй цифр компании.");
            sb.AppendLine("Не утверждай, что данных нет: поиск не выполнялся.");
        }
        else
        {
            sb.AppendLine("Результаты AI-сотрудников (единственный источник данных компании для этого ответа; состояние каждого — в поле outcome):");
            sb.AppendLine(ComposeData(results, name => tools.Find(name)?.Title, language));
            sb.AppendLine();
            sb.AppendLine("""
                Сформируй единый ответ:
                1. Главное — 1–3 предложения с ключевыми цифрами.
                2. По отделам — только привлечённые; факты с цифрами и периодом, со ссылкой на источник.
                3. Взаимосвязи и основные причины (анализ), если данных достаточно.
                4. Рекомендации — приоритетные действия, основанные на фактах; отметь, что это рекомендации.
                Сотрудников без данных называй по их outcome — по правилам итогового ответа (note — готовое пояснение). Не пересказывай JSON дословно.
                """);
        }

        if (remembered is not null)
        {
            sb.AppendLine();
            sb.AppendLine("Что AI-сотрудники выяснили раньше в этом чате (для сравнения и уточняющих вопросов):");
            sb.AppendLine(remembered);
        }

        if (reportsCreated is not null)
        {
            sb.AppendLine();
            sb.AppendLine(reportsCreated.Count > 0
                ? $"Пользователь просил отчёт. Сформированы отчёты Excel: {string.Join("; ", reportsCreated.Select(r => $"«{r.Title}» (листов {r.SheetCount}, строк {r.RowCount})"))}. " +
                  (channel is null
                      ? "Они показаны под ответом: предпросмотр и кнопка «Скачать Excel». "
                      : "Файлы Excel будут отправлены следом за ответом. ") +
                  "Коротко скажи, что в отчёте, и назови главные цифры; таблицы целиком не повторяй."
                : "Пользователь просил отчёт Excel, но он не сформирован. Прямо скажи об этом и почему — по outcome сотрудников (нет прав, источник не подключён, сбой, не найдено), " +
                  "и предложи уточнить запрос: какие показатели, период, регион.");
        }

        if (forbidden.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"У пользователя нет доступа к данным: {string.Join(", ", forbidden.Select(f => $"{f.Name} ({f.Role})"))}. " +
                "Не раскрывай и не оценивай эти данные; если вопрос о них — скажи, что у пользователя нет доступа, и предложи обратиться к администратору.");
        }

        return sb.ToString();
    }
}
