using System.Text.Json;
using OneBase.AI.Llm;
using OneBase.AI.Tools.Data;
using OneBase.Application.DataSources;

namespace OneBase.AI.Grounding;

/// <summary>
/// Инструмент AI-сотрудника «источник истины»: кто ведёт показатель (Linko, таблица Google Sheets, OneBase, расчёт), открытые конфликты
/// источников по нему и — если переданы значения из разных источников — какое из них верно по правилу приоритета (SourceOfTruth.Resolve).
/// </summary>
public static class SourceOfTruthTool
{
    public const string ToolName = "get_source_of_truth";

    public static readonly LlmToolDefinition Definition = new(
        ToolName,
        "Кто источник истины показателя (Linko, Google Sheets, OneBase, расчёт) и есть ли открытые конфликты источников. Вызывай, когда " +
        "значения из разных источников расходятся или пользователь ссылается на другой источник: передай values — получишь верное по правилу " +
        "приоритета и конфликт. Без metric — вся карта источников.",
        JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                metric = new { type = "string", description = "Показатель: «факт продаж», «план РОП», «оплаты дилеров» или ключ (linko_sales, plan_rop…)" },
                values = new
                {
                    type = "array",
                    description = "Значения показателя из разных источников для сверки",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            source = new { type = "string", @enum = new[] { TruthOwners.Linko, TruthOwners.GoogleSheets, TruthOwners.OneBase, TruthOwners.Calculated } },
                            value = new { type = "number" },
                        },
                        required = new[] { "source", "value" },
                    },
                },
            },
        }));

    public static async Task<string> RunAsync(JsonElement arguments, SourceOfTruthService service, CancellationToken ct)
    {
        var map = await service.MapAsync(ct);
        var query = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("metric", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString() ?? ""
            : "";
        var values = new List<SourceValue>();
        if (arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("values", out var v) && v.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in v.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("source", out var s) && s.ValueKind == JsonValueKind.String
                    && item.TryGetProperty("value", out var n) && n.TryGetDecimal(out var value))
                {
                    values.Add(new SourceValue(s.GetString()!.Trim().ToLowerInvariant(), value));
                }
            }
        }

        if (query.Length == 0)
        {
            return JsonSerializer.Serialize(new { rules = SourceOfTruth.Rules, metrics = map }, DataTool.Json);
        }

        var metric = SourceOfTruthService.Find(map, query);
        if (metric is null)
        {
            return JsonSerializer.Serialize(new
            {
                rules = SourceOfTruth.Rules,
                found = false,
                note = $"Показатель «{query}» в карте источников не найден — источник истины для него не определён: значение не выбирается произвольно.",
                resolution = values.Count > 0 ? SourceOfTruth.Resolve(null, values) : null,
                metrics = map.Select(x => new { x.Key, x.Label, x.Owner }),
            }, DataTool.Json);
        }

        return JsonSerializer.Serialize(new
        {
            rules = SourceOfTruth.Rules,
            found = true,
            metric,
            ownerLabel = TruthOwners.Label(metric.Owner),
            resolution = values.Count > 0 ? SourceOfTruth.Resolve(metric.Owner, values) : null,
        }, DataTool.Json);
    }
}
