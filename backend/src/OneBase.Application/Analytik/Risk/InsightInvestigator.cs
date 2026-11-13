using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneBase.Application.Analytik.Compute;
using OneBase.Domain.Analytik;

namespace OneBase.Application.Analytik.Risk;

/// <summary>
/// Proactive Intelligence: авторасследование новой или выросшей находки — кодом, без модели и платных вызовов. Отклонение продаж или оплат дня
/// раскладывается движком расчётов против того же дня неделей раньше: вклад регионов, товарных групп и агентов, цена / объём / ассортимент.
/// Итог — в находке: уверенность, класс вывода (наблюдаемая зависимость — вклад, а не доказанная причина), расследование (вклад, расчёты,
/// вывод). Остальные правила уже считают причину своими данными: уверенность — по тому, подтверждена ли причина.
/// </summary>
public sealed class InsightInvestigator(ComputeService compute, ILogger<InsightInvestigator> logger)
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Расследовать находку; null — правило без расследования данными (только уверенность по подтверждённой причине).</summary>
    public async Task<string?> InvestigateAsync(AnalytikInsight insight, DateTimeOffset now, CancellationToken ct)
    {
        insight.InvestigatedAt = now;
        var day = Day(insight.Evidence);
        var metric = insight.Rule switch
        {
            "sales.daily_anomaly" => "revenue",
            "finance.payments_anomaly" => "payments",
            _ => null,
        };
        if (metric is null || day is null)
        {
            insight.Confidence = insight.CauseConfirmed ? 0.8m : insight.Hypotheses is null ? 0.5m : 0.3m;
            insight.EvidenceClass = insight.CauseConfirmed ? EvidenceClasses.Observed : EvidenceClasses.Assumption;
            insight.Investigation = JsonSerializer.Serialize(new
            {
                method = "rule",
                conclusion = insight.CauseConfirmed ? "Причина подтверждена данными правила." : "Причина не установлена данными — гипотезы требуют проверки.",
            }, Json);
            return null;
        }

        var compare = day.Value.AddDays(-7);
        var context = new ComputeContext(null, "proactive");
        var dims = metric == "payments" ? new[] { "region", "agent" } : new[] { "region", "product_group", "agent" };
        var drivers = new List<(string Dimension, ComputeOutcome Outcome)>();
        foreach (var dim in dims)
        {
            try
            {
                drivers.Add((dim, await compute.RunAsync(new ComputeRequest
                {
                    Method = "drivers",
                    Data = new ComputeData { Metrics = [metric], Dimension = dim, From = day, To = day, CompareFrom = compare, CompareTo = compare },
                }, context, ct)));
            }
            catch (Exception ex) when (ex is ComputeException or UnauthorizedAccessException)
            {
                logger.LogInformation("Аналитик: вклад {Dimension} для находки {Key} не посчитан: {Error}", dim, insight.Key, ex.Message);
            }
        }

        ComputeOutcome? pvm = null;
        if (metric == "revenue")
        {
            try
            {
                pvm = await compute.RunAsync(new ComputeRequest
                {
                    Method = "pvm",
                    Data = new ComputeData { From = day, To = day, CompareFrom = compare, CompareTo = compare },
                }, context, ct);
            }
            catch (ComputeException ex)
            {
                logger.LogInformation("Аналитик: PVM для находки {Key} не посчитан: {Error}", insight.Key, ex.Message);
            }
        }

        var tops = new List<object>();
        var lines = new List<string>();
        double? concentration = null;
        foreach (var (dim, o) in drivers.Where(d => d.Outcome.Status == "ok" && d.Outcome.Result is not null))
        {
            var r = JsonSerializer.SerializeToElement(o.Result, ComputeService.JsonOptions);
            var delta = r.GetProperty("delta").GetDouble();
            var items = r.GetProperty("items").EnumerateArray()
                .Where(i => Math.Sign(i.GetProperty("delta").GetDouble()) == Math.Sign(delta) && delta != 0).Take(3)
                .Select(i => new { key = i.GetProperty("key").GetString(), delta = i.GetProperty("delta").GetDouble(), share = i.GetProperty("shareOfGross").GetDouble() })
                .ToList();
            tops.Add(new { dimension = dim, total = delta, computationId = o.Id, items });
            if (items.Count > 0)
            {
                lines.Add($"{Title(dim)}: {string.Join(", ", items.Select(i => $"{i.key} {i.delta.ToString("N0", Ru)} ({(i.share * 100).ToString("N0", Ru)} %)"))}");
                if (dim == "region")
                {
                    concentration = items[0].share;
                }
            }
        }

        string? pvmLine = null;
        if (pvm is { Status: "ok", Result: not null })
        {
            var p = JsonSerializer.SerializeToElement(pvm.Result, ComputeService.JsonOptions).GetProperty("pvm");
            var effects = new[] { ("цена", p.GetProperty("price").GetDouble()), ("объём", p.GetProperty("volume").GetDouble()), ("ассортимент", p.GetProperty("mix").GetDouble()) };
            var main = effects.MaxBy(e => Math.Abs(e.Item2));
            pvmLine = $"главный эффект — {main.Item1} ({main.Item2.ToString("N0", Ru)} сум)";
        }

        var complete = drivers.Count > 0 && drivers.All(d => d.Outcome.Limitations.All(l => !l.Contains("неполный", StringComparison.Ordinal)));
        // Уверенность во вкладе: чем сильнее отклонение сосредоточено в одном регионе, тем увереннее «где»; данные неполные — ниже.
        var confidence = concentration is { } c ? 0.4 + 0.4 * Math.Min(1, c) : 0.3;
        if (!complete)
        {
            confidence -= 0.15;
        }

        insight.Confidence = Math.Round((decimal)Math.Clamp(confidence, 0.1, 0.9), 2);
        insight.EvidenceClass = EvidenceClasses.Observed;
        var conclusion = lines.Count == 0
            ? "Разложить отклонение не удалось: данных дня или дня сравнения нет."
            : $"Сравнение с {compare:dd.MM}: {string.Join("; ", lines)}{(pvmLine is null ? "" : "; " + pvmLine)}. Это вклад в отклонение, а не доказанная причина.";
        insight.Investigation = JsonSerializer.Serialize(new
        {
            method = "drivers+pvm", day, compare, drivers = tops, pvm = pvm?.Id, conclusion, complete,
        }, Json);
        return conclusion;
    }

    private static string Title(string dim) => dim switch
    {
        "region" => "регионы",
        "product_group" => "товарные группы",
        "agent" => "агенты",
        _ => dim,
    };

    private static DateOnly? Day(string? evidence)
    {
        if (evidence is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(evidence);
            return doc.RootElement.TryGetProperty("day", out var d) && DateOnly.TryParse(d.GetString(), CultureInfo.InvariantCulture, out var day) ? day : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
