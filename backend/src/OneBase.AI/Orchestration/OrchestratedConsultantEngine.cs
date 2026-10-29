using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneBase.AI.Agents;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Grounding;
using OneBase.AI.Llm;
using OneBase.AI.Memory;
using OneBase.AI.Monitoring;
using OneBase.AI.Providers;
using OneBase.AI.Reports;
using OneBase.AI.Security;

namespace OneBase.AI.Orchestration;

/// <summary>
/// Главный AI-консультант: вопрос → маршрутизация → AI-сотрудники параллельно → единый управленческий ответ.
/// AI-сотрудники без доступа у пользователя не привлекаются; консультант прямо говорит, каких данных не видит.
/// </summary>
public sealed partial class OrchestratedConsultantEngine(
    AiAgentStore agents,
    IUserPermissions permissions,
    AiRouter router,
    AgentOrchestrator orchestrator,
    IAiGateway gateway,
    AiMemoryStore memory,
    ReportService reports,
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
        var available = departments.Where(a => a.RequiredPermission is null || own.Contains(a.RequiredPermission)).ToList();
        var forbidden = departments.Except(available).ToList();
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
            routing = Enforce(await router.RouteAsync(turn.Question, turn.History, available, context, ct, rememberedText), turn, correction, available);
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
        var results = runs.Select(r => r.Result).ToList();
        var evidence = Evidence(runs, turn, correction, rememberedText);
        var review = correction is null ? null : CorrectionGuard.Review(correction, evidence, turn.LastAnswerId);
        // Два системных сообщения: неизменные правила ответа (кэшируются у провайдера между вопросами) и данные этого вопроса.
        List<LlmMessage> messages =
        [
            new(LlmRole.System, ComposeRules(consultant)),
            new(LlmRole.System, ComposePrompt(results, forbidden, rememberedText, wantsReport ? created : null, turn.Channel, asksReport && !wantsReport, review)),
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
            MaxOutputTokens = turn.Channel?.MaxOutputTokens ?? consultant?.MaxOutputTokens,
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
    /// Data Retrieval: ответ о фактах — только по данным. Маршрутизатор не привлёк сотрудников, а вопрос — о данных компании (вопрос со словами
    /// отделов) — сотрудники по ключевым словам. Возражение к ответу — перепроверяют сотрудники той же области (по прошлому вопросу и ответу),
    /// а если область не определить, но в ответе были числа, — все доступные.
    /// </summary>
    internal static RoutingDecision Enforce(RoutingDecision routing, ConsultantTurn turn, CorrectionRequest? correction, IReadOnlyList<AgentConfig> available)
    {
        if (routing.Agents.Count > 0 || available.Count == 0)
        {
            return routing;
        }

        if (correction is not null)
        {
            var previousQuestion = turn.History.LastOrDefault(m => m.Role == LlmRole.User)?.Content ?? string.Empty;
            var agents = AiRouter.KeywordAgents($"{previousQuestion}\n{correction.PreviousAnswer}\n{turn.Question}", available).ToList();
            if (agents.Count == 0 && NumberClaims.Checkable(correction.PreviousAnswer).Count > 0)
            {
                agents = available.Select(a => a.Code).ToList();
            }

            return agents.Count == 0 ? routing : routing with { Agents = agents, Reason = "Пользователь оспаривает прошлый ответ — AI-сотрудники перепроверяют данные." };
        }

        if (AsksForData().IsMatch(turn.Question) && AiRouter.KeywordAgents(turn.Question, available) is { Count: > 0 } byWords)
        {
            return routing with { Agents = byWords, Reason = "Вопрос о данных компании — ответ только по данным OneBase." };
        }

        return routing;
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

    /// <summary>Вопрос, а не реплика: вопросительный знак или слово запроса данных.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"\?|сколько|какой|какая|какие|каков|почему|где|кто|когда|покажи|дай|сравни|посчитай|выведи|назови|nechta|qancha|qaysi|nima|qanday|ko['ʻ’‘]?rsat", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex AsksForData();

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
        sb.AppendLine(ConsultantPrompts.QualityRules);
        sb.AppendLine();
        sb.AppendLine("Каждое число ответа сверяется кодом с данными AI-сотрудников: число, которого нет в их результатах, будет помечено как не подтверждённое.");
        sb.AppendLine("Числа, которые сотрудник отметил как unverified, — не факт: не приводи их или скажи, что они не подтверждены данными.");
        return sb.ToString();
    }

    /// <summary>Данные этого вопроса для итогового ответа: дата, результаты сотрудников, память чата, отчёты, ограничения доступа.</summary>
    private string ComposePrompt(IReadOnlyList<AgentResult> results, IReadOnlyList<AgentConfig> forbidden, string? remembered,
        IReadOnlyList<ReportRef>? reportsCreated, ConsultantChannel? channel = null, bool reportsForbidden = false, CorrectionReview? review = null)
    {
        var today = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(5));
        var sb = new StringBuilder();
        sb.AppendLine($"Сегодня {today:dd.MM.yyyy} (Ташкент).");
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
            sb.AppendLine("К этому вопросу AI-сотрудники не привлекались: данных OneBase в этом ответе нет. Отвечай по истории чата и общим знаниям;");
            sb.AppendLine("если вопрос о показателях компании — скажи, что для ответа нужно уточнить вопрос (период, отдел), и не называй цифр компании.");
        }
        else
        {
            sb.AppendLine("Результаты AI-сотрудников (единственный источник данных компании для этого ответа):");
            sb.AppendLine(JsonSerializer.Serialize(results.Select(r => new
            {
                r.Agent,
                r.AgentName,
                r.Status,
                r.Summary,
                r.Findings,
                r.Metrics,
                r.Problems,
                r.Recommendations,
                DataSources = r.DataSources.Select(s => s.Period is null ? s.Title : $"{s.Title} ({s.Period})"),
                r.Error,
                r.Unverified,
            }), Json));
            sb.AppendLine();
            sb.AppendLine("""
                Сформируй единый ответ:
                1. Главное — 1–3 предложения с ключевыми цифрами.
                2. По отделам — только привлечённые; факты с цифрами и периодом, со ссылкой на источник.
                3. Взаимосвязи и основные причины (анализ), если данных достаточно.
                4. Рекомендации — приоритетные действия, основанные на фактах; отметь, что это рекомендации.
                Если сотрудник вернул no_data или ошибку — прямо скажи, каких данных нет. Не пересказывай JSON дословно.
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
                : "Пользователь просил отчёт Excel, но он не сформирован. Прямо скажи об этом и почему (нет данных, нет доступа или ошибка — по результатам сотрудников), " +
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
