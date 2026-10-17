using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.Application.DataSources;
using OneBase.Domain.AI;

namespace OneBase.AI.DataSources;

/// <summary>
/// Сопоставление столбцов Google Sheets с полями OneBase моделью ИИ проекта — через AI Gateway (основная модель из «Настройки → AI»,
/// резервная при сбое, учёт токенов). Модель видит заголовки, примеры значений, допустимые цели раздела, категории и правила владения
/// и отвечает JSON. Ответ проверяет SheetMapping.Validate: модель только предлагает — ничего не пишет и полей не создаёт.
/// </summary>
internal sealed class SheetMappingAdvisor(IAiGateway gateway) : ISheetMappingAdvisor
{
    internal const string Prompt = """
        Ты сопоставляешь столбцы листа Google Sheets с полями OneBase — системы продаж кондитерской компании (Узбекистан, регионы и дилеры).
        На входе JSON: таблица и лист, строка заголовков, столбцы (буква, заголовок, примеры значений), targets — допустимые цели раздела
        в виде ключей «поле.роль» с описаниями, categories — категории товаров OneBase, blocked — поля, которые уже ведёт другая привязка,
        linkoOwns — данные, которые ведёт Linko, existing — текущее сопоставление привязки, today — сегодняшняя дата.

        Правила:
        1. targetField — только ключ из targets, символ в символ. Подходящей цели нет — targetField null. Новых полей не придумывай.
        2. Данные из linkoOwns (факт продаж, факт, выполнение, отгрузки, остатки, визиты) из таблицы не берутся: такой столбец — null с
           предупреждением. План и факт — разные показатели: столбец факта нельзя сопоставлять с планом.
        3. Поля из blocked не предлагай: их ведёт другая привязка (targetField null и предупреждение).
        4. Одной роли поля — один столбец. Исключение — роль category_value: каждый столбец-категория широкой таблицы — отдельная запись
           с category — точным name категории из categories. В листе категория может называться синонимом из aliases (тип товара
           Linko или основа слова: «могул» — это «Помадка», «трубоч» — «Трубочки»): тогда category — name этой категории.
        5. Один столбец может служить нескольким полям (например, «Регион» — и для плана, и для дилера): тогда несколько записей.
        6. transformType: "tons_to_kg" — значения в тоннах; "month_text" — месяц текстом; "date" — дата; иначе "none".
        7. fields[].monthSource для полей плана и P&L: "column" — есть столбец месяца; "sheet" — месяц в названиях листов
           («Сентябрь 2026»); "current" — лист всегда про текущий месяц, месяца в данных нет; "fixed" — один конкретный месяц (year,
           month), например из названия таблицы или листа. Не уверен — не указывай поле в fields.
        8. confidence — честно, от 0 до 1: заголовок и значения однозначно подтверждают — выше 0.85; есть сомнения — 0.5–0.85 и
           requiresConfirmation true; догадка — ниже 0.5.
        9. reason и warnings — коротко, по-русски. Столбцы без подходящей цели тоже перечисли (targetField null).

        Ответ — только JSON, без пояснений вокруг:
        {"suggestions":[{"sourceColumn":"B","targetField":"plan_primary.value","confidence":0.93,"reason":"…","transformType":"none",
          "category":null,"requiresConfirmation":false,"warnings":[]}],
         "fields":[{"field":"plan_primary","monthSource":"column","year":null,"month":null}],
         "notes":["…"]}
        """;

    /// <summary>Что ведёт Linko — модель не должна брать это из таблицы.</summary>
    private static readonly string[] LinkoOwns =
    [
        "факт продаж: заказы и возвраты (кг, сумма) — вторичка, ассортимент, SKU, прямые клиенты первички",
        "визиты агентов, торговые точки, агенты",
        "остатки складов, аутсток, перемещения завод → дилеры (факт первички)",
        "планы ТП (агентов) — API планов Linko",
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<RawMapping> AdviseAsync(SheetMappingRequest request, Guid userId, CancellationToken ct = default)
    {
        var readiness = await gateway.GetReadinessAsync(ct);
        if (!readiness.Ready)
        {
            throw new SheetMappingException(readiness.Message ?? "AI не настроен в «Настройки → AI».");
        }

        AiCallResult result;
        try
        {
            result = await gateway.CompleteAsync(
                [new LlmMessage(LlmRole.System, Prompt), new LlmMessage(LlmRole.User, Request(request))],
                [],
                new AiCallOptions
                {
                    JsonOutput = true, MaxOutputTokens = 6000, ReasoningEffort = AiReasoning.Medium,
                    Context = new AiCallContext(userId, Purpose: "datasources.automap"),
                },
                ct);
        }
        catch (LlmNotConfiguredException ex)
        {
            throw new SheetMappingException(ex.Message);
        }
        catch (AiProviderException ex)
        {
            throw new SheetMappingException(ex.Message);
        }

        var json = JsonText.ExtractObject(result.Text)
                   ?? throw new SheetMappingException("ИИ ответил не в формате JSON — повторите или сопоставьте столбцы вручную.");
        return Parse(json, $"{result.Model.Provider}/{result.Model.Model}");
    }

    /// <summary>Сообщение модели: всё, что нужно для сопоставления, одним JSON.</summary>
    internal static string Request(SheetMappingRequest r) => JsonSerializer.Serialize(new
    {
        spreadsheet = r.Spreadsheet,
        sheet = r.Sheet,
        sheets = r.Sheets.Take(60),
        headerRow = r.HeaderRow,
        today = r.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        section = r.SectionName,
        columns = r.Columns.Select(c => new { letter = c.Letter, header = c.Header, samples = c.Samples }),
        targets = r.Targets.Select(t => new { key = t.Key, field = t.FieldLabel, role = t.RoleLabel, description = t.Description, required = t.Required }),
        categories = r.Categories.Select(c => new { name = c.Name, aliases = c.Aliases }),
        blocked = r.Blocked.Select(b => new { field = b.Key, rule = b.Value }),
        linkoOwns = LinkoOwns,
        existing = r.Existing.Select(f => new { field = f.Field, plan = f.Plan, profile = f.Profile, pnl = f.Pnl, monthFromSheet = f.MonthFromSheet, currentMonth = f.CurrentMonth }),
    }, Json);

    /// <summary>Разбор ответа модели; без списка suggestions — ошибка (ничего не выдумываем за модель).</summary>
    internal static RawMapping Parse(JsonElement json, string model)
    {
        if (!json.TryGetProperty("suggestions", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            throw new SheetMappingException("ИИ не вернул сопоставление столбцов — повторите или сопоставьте вручную.");
        }

        var suggestions = list.EnumerateArray()
            .Where(s => s.ValueKind == JsonValueKind.Object)
            .Select(s => new RawSuggestion(
                Text(s, "sourceColumn"), Text(s, "targetField"), Number(s, "confidence"), Text(s, "reason"), Text(s, "transformType"), Text(s, "category"),
                s.TryGetProperty("requiresConfirmation", out var rc) && rc.ValueKind == JsonValueKind.True,
                JsonText.Array(s, "warnings").Where(w => w.ValueKind == JsonValueKind.String).Select(w => w.GetString()!).ToList()))
            .ToList();
        var fields = JsonText.Array(json, "fields")
            .Where(f => f.ValueKind == JsonValueKind.Object)
            .Select(f => new RawFieldSetting(Text(f, "field"), Text(f, "monthSource"), (int?)Number(f, "year"), (int?)Number(f, "month")))
            .ToList();
        var notes = JsonText.Array(json, "notes").Where(n => n.ValueKind == JsonValueKind.String).Select(n => n.GetString()!).ToList();
        return new RawMapping(suggestions, fields, notes, model);
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        } : null;

    private static double? Number(JsonElement e, string name) =>
        !e.TryGetProperty(name, out var v) ? null
        : v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) ? d
        : v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString()?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p
        : null;
}
