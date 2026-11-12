using System.Text.Json;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Simulation;

namespace OneBase.AI.Analytik.Simulation;

/// <summary>AI недоступен (не настроен ключ или модель) — кнопка AI-анализа отвечает понятным текстом.</summary>
public sealed class AiUnavailableException(string message) : Exception(message);

/// <summary>
/// AI-интерпретация результата What-If — по кнопке, после расчёта (не на каждое движение ползунка: один платный вызов модели). Модель получает
/// только посчитанное движком (показатели, ограничения, риски, рекомендации, исходные данные, допущения) и пишет заключение для руководства;
/// новых чисел она не считает. Сохранённому сценарию анализ сохраняется (пересчёт его сбрасывает).
/// </summary>
public sealed class ScenarioAnalyst(IAiGateway gateway, ScenarioService scenarios)
{
    public const string Prompt = """
        Ты — аналитик сценариев What-If в разделе «Аналитик» OneBase (кондитерское производство в Узбекистане). Тебе дан результат расчёта движка
        симулятора по реальным данным компании. Напиши заключение для руководства на русском, не длиннее 350 слов, в разделах:
        **Основной вывод** — 1–2 предложения;
        **Что меняется** — главные показатели: база → сценарий, разница и %;
        **Ограничения** — что мешает (мощность, сырьё, сроки, деньги), с датами;
        **Риски**;
        **Рекомендации** — конкретные действия, по приоритету;
        **Исходные данные и допущения** — период, дата данных, что введено пользователем, чего нет в OneBase.
        Правила: каждое число — только из данных ниже, ничего не пересчитывай и не придумывай (ни цен, ни мощностей, ни зарплат). Показатель без
        данных так и называй: «не посчитано — нет …». Не путай запрошенный объём с проданным: рост спроса не равен росту выручки, если есть
        ограничения. Не обещай результат — это расчёт сценария.
        """;

    public async Task<string> ExplainAsync(AnalytikPrincipal p, Guid scenarioId, CancellationToken ct)
    {
        var view = await scenarios.GetAsync(p, scenarioId, ct);
        if (view.Result is not { Valid: true } result)
        {
            throw new ArgumentException("Сценарий не посчитан — AI-анализ недоступен.");
        }

        var text = await CompleteAsync(p, view.Name, view.Baseline, result, view.Parameters, ct);
        if (view.Own)
        {
            await scenarios.SaveAiAsync(p, scenarioId, text, ct);
        }

        return text;
    }

    public async Task<string> ExplainDraftAsync(AnalytikPrincipal p, RunInput input, CancellationToken ct)
    {
        var result = await scenarios.RunAsync(p, input, ct);
        if (!result.Valid)
        {
            throw new ArgumentException("Сценарий с ошибками в параметрах — AI-анализ недоступен.");
        }

        var baseline = await scenarios.BaselineByIdAsync(p, input.BaselineId, ct);
        return await CompleteAsync(p, "Черновик сценария", baseline, result, input.Parameters ?? [], ct);
    }

    private async Task<string> CompleteAsync(AnalytikPrincipal p, string name, BaselineView baseline, SimulationResult r, IReadOnlyList<ScenarioParameter> parameters,
        CancellationToken ct)
    {
        var readiness = await gateway.GetReadinessAsync(ct);
        if (!readiness.Ready)
        {
            throw new AiUnavailableException(readiness.Message ?? "AI не настроен.");
        }

        var call = await gateway.CompleteAsync([new LlmMessage(LlmRole.System, Prompt), new LlmMessage(LlmRole.User, Content(name, baseline, r, parameters))], [],
            new AiCallOptions
            {
                MaxOutputTokens = 1800, CacheConversation = false,
                Context = new AiCallContext(p.UserId, AnalytikAgents.Scenario, null, "analytik.scenario.explain"),
            }, ct);
        var text = call.Text?.Trim();
        return string.IsNullOrWhiteSpace(text) ? throw new AiUnavailableException("Модель не вернула текст — попробуйте ещё раз.") : text;
    }

    /// <summary>Данные для модели: сценарий, параметры с названиями, исходные данные, показатели, ограничения, риски, рекомендации, допущения.</summary>
    internal static string Content(string name, BaselineView baseline, SimulationResult r, IReadOnlyList<ScenarioParameter> parameters) =>
        JsonSerializer.Serialize(new
        {
            Scenario = name,
            BasePeriod = $"{baseline.From:dd.MM.yyyy} — {baseline.To:dd.MM.yyyy}",
            DataAsOf = baseline.AsOf.ToString("dd.MM.yyyy"),
            Horizon = $"{r.HorizonDays} дн.: {r.From:dd.MM.yyyy} — {r.To:dd.MM.yyyy}",
            Parameters = parameters.Select(x => new { ScenarioCatalog.Find(x.Code)?.Title, x.Value, ScenarioCatalog.Find(x.Code)?.Unit, x.Target }),
            r.Conclusion.Headline,
            Metrics = r.Metrics.Select(m => new { m.Title, m.Unit, m.Base, m.Scenario, m.Delta, m.DeltaPercent, m.Note }),
            r.Constraints,
            r.Risks,
            r.Conclusion.Recommendations,
            LimitedProducts = r.Products.Where(x => x.Limit is not null).Take(10).Select(x => new { x.Name, x.RequestedKg, x.SoldKg, x.Limit }),
            Shortages = r.Materials.Where(m => m.LostDays > 0 || m.OrderLate).Take(10)
                .Select(m => new { m.Name, m.Unit, m.ShortageDate, m.LostDays, m.OrderBy, m.LeadTimeDays, m.Purchase, m.PurchaseCost }),
            r.Expansion,
            r.Inputs,
            r.Conclusion.Assumptions,
            r.Conclusion.Sources,
            DataNotes = baseline.Notes,
        }, SimulationJson.Options);
}
