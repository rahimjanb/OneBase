using Microsoft.EntityFrameworkCore;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Recruitment;
using OneBase.Application.Abstractions;
using OneBase.Application.Agents;
using OneBase.Application.Field;
using OneBase.Domain.AI;
using OneBase.Domain.Recruitment;

namespace OneBase.AI.DialogAgents;

/// <summary>
/// «Проверка» агента в настройках: что он ответил бы на переписку, которую пишет сотрудник, — тот же промпт, те же ограничения, но ничего не
/// отправляется и не сохраняется (кроме учёта расходов AI: это платный вызов модели).
/// </summary>
public sealed class DialogAgentTester(IAppDbContext db, IAiGateway ai, TimeProvider clock)
{
    public const int MaxMessages = 30;

    public async Task<AgentTestResult> TestAsync(DialogAgent agent, AgentTestInput input, Guid userId, CancellationToken ct)
    {
        var list = (input.Messages ?? []).Where(m => !string.IsNullOrWhiteSpace(m.Text)).ToList();
        if (list.Count is 0 or > MaxMessages || list.Any(m => m.Text.Length > 2000 || m.Role is not ("user" or "agent")) || list[^1].Role != "user")
        {
            throw new FieldValidationException($"Переписка — до {MaxMessages} сообщений по 2000 символов; последнее — от собеседника.");
        }

        var last = list[^1].Text;
        var now = clock.GetUtcNow();
        var notes = new List<string>();
        if (agent.Mode == DialogAgentMode.Approval)
        {
            notes.Add("Режим «С проверкой»: в настоящем диалоге этот ответ ждал бы проверки сотрудником.");
        }

        if (agent.OffHours == "morning" && !DialogAgentRules.InWork(agent, now))
        {
            notes.Add($"Сейчас нерабочее время агента: ответ ушёл бы {DialogAgentRules.NextWorkStart(agent, now).ToOffset(DialogAgentRules.Tashkent):dd.MM HH:mm}.");
        }

        var recruiter = agent.Kind == DialogAgentKinds.Recruiter;
        if (recruiter)
        {
            notes.Add("В настоящем диалоге рекрутер сначала просит согласие на обработку данных — в проверке этот шаг пропущен.");
        }

        var phrase = DialogAgentRules.HandoffIn(last, agent.HandoffPhrases);
        if (phrase is not null || RecruiterRules.AsksForHuman(last))
        {
            return new AgentTestResult("handoff", [agent.HandoffReply ?? "Передаю ваш вопрос сотруднику — он ответит вам здесь."],
                phrase is null ? "Собеседник просит человека — передаётся сотруднику без модели." : $"Фраза «{phrase}» — сразу к сотруднику, без модели.", null, notes, null, 0, 0);
        }

        var answers = await db.DialogAgentAnswers.AsNoTracking().Where(a => a.AgentId == agent.Id).OrderBy(a => a.SortOrder).ToListAsync(ct);
        var messages = new List<LlmMessage>();
        IReadOnlyList<Vacancy> open = [];
        if (recruiter)
        {
            var vacancy = input.VacancyId is { } vid ? await db.Vacancies.AsNoTracking().Include(v => v.Department).FirstOrDefaultAsync(v => v.Id == vid, ct) : null;
            open = await RecruiterAgent.OpenVacanciesAsync(db, ct);
            notes.Add(open.Count == 0
                ? "Открытых вакансий в OneBase нет — агент скажет, что сейчас набора нет."
                : $"Агент знает открытые вакансии OneBase: {string.Join(", ", open.Take(8).Select(v => v.Code))}{(open.Count > 8 ? $" и ещё {open.Count - 8}" : string.Empty)}.");
            var candidate = new Candidate { FullName = "Тестовый кандидат" };
            var application = vacancy is null ? null : new CandidateApplication { CandidateId = candidate.Id, VacancyId = vacancy.Id, Vacancy = vacancy, Source = "test" };
            messages.Add(new LlmMessage(LlmRole.System, RecruiterAgent.SystemPrompt(agent, answers)));
            messages.Add(new LlmMessage(LlmRole.System, RecruiterAgent.Context(agent, candidate, application, open, null, now)));
        }
        else
        {
            messages.Add(new LlmMessage(LlmRole.System, AssistantAgent.SystemPrompt(agent, answers)));
            messages.Add(new LlmMessage(LlmRole.System, $"{DialogAgentPrompt.Now(agent, now)}\nСобеседник: проверка из настроек."));
        }

        messages.AddRange(list.Select(m => m.Role == "user"
            ? new LlmMessage(LlmRole.User, DialogAgentPrompt.Wrap(m.Text))
            : new LlmMessage(LlmRole.Assistant, m.Text)));

        AiCallResult result;
        try
        {
            result = await ai.CompleteAsync(messages, [], new AiCallOptions
            {
                Model = DialogAgentPrompt.Model(agent),
                JsonOutput = true,
                AllowToolCalls = false,
                MaxOutputTokens = 1200,
                CacheConversation = false,
                Context = new AiCallContext(userId, recruiter ? "recruiter" : "assistant", null, "agents.test"),
            }, ct);
        }
        catch (LlmNotConfiguredException ex)
        {
            throw new FieldValidationException(ex.Message);
        }
        catch (OneBase.AI.Providers.AiProviderException ex)
        {
            throw new FieldValidationException($"Модель не ответила: {ex.Message}");
        }

        string action;
        IReadOnlyList<string> raw;
        string? reason;
        if (recruiter && RecruiterAgent.Parse(result.Text) is { } r)
        {
            (action, raw, reason) = (r.Action, r.Messages, r.Reason);
            foreach (var (q, a) in r.Answers)
            {
                notes.Add($"Записал бы ответ на вопрос «{q}»: {a}");
            }

            if (RecruiterAgent.Chosen(r.Vacancy, open) is { } chosen && chosen.Id != input.VacancyId)
            {
                notes.Add($"Записал бы кандидата на вакансию {chosen.Code} «{chosen.Title}».");
            }

            if (r.Action == "propose_interview")
            {
                notes.Add(agent.CanScheduleInterviews
                    ? "Пригласил бы на собеседование — к сообщению добавились бы свободные варианты времени из календаря."
                    : "Собеседования назначает HR — диалог перешёл бы к сотруднику.");
            }
        }
        else if (!recruiter && AssistantAgent.Parse(result.Text) is { } d)
        {
            (action, raw, reason) = (d.Action, d.Messages, d.Reason);
        }
        else
        {
            return new AgentTestResult("error", [], "Ответ модели не по формату — в настоящем диалоге вопрос ушёл бы сотруднику.", null, notes,
                result.Model.ToString(), result.Usage.PromptTokens, result.Usage.OutputTokens);
        }

        var parts = DialogAgentRules.Parts(raw, agent.SplitMessages, agent.MaxReplyChars);
        var blocked = DialogAgentRules.ForbiddenIn(string.Join("\n\n", parts), agent.ForbiddenPhrases) is { } bad
            ? $"В ответе запрещённая фраза «{bad}» — он не был бы отправлен."
            : null;
        return new AgentTestResult(action, parts, action == "handoff" ? reason ?? "Агент передал бы вопрос сотруднику." : null, blocked, notes,
            result.Model.ToString(), result.Usage.PromptTokens, result.Usage.OutputTokens);
    }
}
