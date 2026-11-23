using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.AI.Tools;
using OneBase.AI.Tools.Data;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Catalog;
using OneBase.Application.Analytik.Query;

namespace OneBase.AI.Analytik;

/// <summary>
/// analytik_sql — свободный запрос на чтение к базе OneBase: один SELECT/WITH с CTE, оконными функциями (lag, lead, row_number…) и
/// соединениями. Нужен для вопросов, которых нет ни в готовых инструментах, ни в типизированном плане: «что было после каждого заказа»,
/// интервалы между событиями, ранги и доли внутри групп, связь размера заказа и следующих визитов. Проверки — SqlQueryService (каталог,
/// права, скрытые колонки, белый список функций, только чтение, таймаут, журнал). describe — колонки таблиц из каталога (у команды
/// консультанта нет analytik_describe_table).
/// </summary>
public sealed class SqlTool(QueryEngine engine, SqlQueryService queries, CatalogService catalog, AnalytikOptions options) : AnalytikTool(engine)
{
    public const string ToolName = "analytik_sql";
    private const int DescribeTables = 8;

    public override string Name => ToolName;
    public override string Title => "Свободный запрос к базе (только чтение)";
    public override int Level => ToolLevels.Analyze;

    // Источник «Статистика и исследования» есть и у команды консультанта, и у аналитиков: оба получают инструмент при запуске.
    public override string? Source => KnowledgeSources.SalesStats;

    public override string Description =>
        "Свободный запрос PostgreSQL на чтение к базе OneBase: один SELECT или WITH … SELECT — CTE, оконные функции (lag, lead, row_number, rank, " +
        "sum() over …), соединения, подзапросы, percentile_cont, corr, date_trunc. Для того, чего нет в готовых инструментах и в analytik_query: " +
        "цепочки по времени (что было после каждого заказа, сколько визитов и дней до следующего), интервалы между событиями, сравнение строки с " +
        "предыдущей, ранги и доли внутри групп, медианы, связь двух показателей по точкам или ТП. Таблицы — schema.\"Table\", колонки — \"Column\" " +
        "в двойных кавычках (регистр важен). Главные таблицы продаж: " +
        "linko.\"Orders\" (\"Id\", \"CreatedDate\" — день ввода заказа, \"AcceptedDate\" — день приёмки (выручка), \"Status\" — delivered, given = продажа; " +
        "not_delivered, cancelled — нет, \"AgentId\" — ТП, \"MarketId\" — точка, \"BranchId\", \"BranchName\", \"TotalPrice\" — сумма, \"TotalWeight\" — кг, " +
        "\"Currency\"); linko.\"OrderLines\" (\"OrderId\", \"ProductId\", \"Amount\", \"Price\", \"TotalPrice\", \"TotalWeight\", \"ReturnAmount\"); " +
        "linko.\"Visits\" (\"Day\" — день, \"Date\" — отметка, \"Status\" — done выполнен / pending, \"IsInPlan\" — по маршруту, \"MarketId\", \"UserId\" — ТП); " +
        "linko.\"Markets\" (\"Id\", \"Name\", \"MarketTypeName\", \"BranchId\", \"BranchName\", \"Address\"); linko.\"Users\" — ТП (\"Id\", \"FirstName\", " +
        "\"SecondName\", \"IsActive\", \"JobName\"); linko.\"Products\" (\"Id\", \"Name\", \"TypeId\"); linko.\"ProductTypes\" (\"Id\", \"Name\", \"ParentId\" — " +
        "товарные группы); linko.\"OrderReturns\", linko.\"OrderReturnLines\" — возвраты; linko.\"Payments\" — оплаты; sales.\"Regions\" (\"Id\", \"Name\", " +
        "\"LinkoBranchId\" — филиал Linko региона); sales.\"RegionPlans\" (\"RegionId\", \"Year\", \"Month\", \"CategoryId\", \"PlanKg\"). Продажи вторички — " +
        "статусы delivered и given и без филиалов «Завод» и «К К Мерч» (lower(\"BranchName\") not in ('завод', 'к к мерч')). Другие таблицы и колонки — " +
        "describe: [\"schema.Table\"] (или analytik_find_tables). Секреты, персональные данные и закрытые таблицы недоступны; доступ — по правам " +
        "пользователя; данные продаж — только при доступе ко всей организации.";

    public override string? UsageHint =>
        $"Вопрос, на который нет готового инструмента (или готовый «не связывает» нужное), — не отвечай «невозможно» и «не рассчитано»: посчитай " +
        $"{ToolName} по сырым таблицам (заказы, строки, визиты, точки, оплаты). Пиши один запрос с CTE и оконными функциями, всегда ограничивай " +
        "период условием по дате; подсчёт по событиям пары — оконными функциями, не подзапросом на каждую строку. Ошибку исправь по её тексту и " +
        "повтори: отказ по скрытой колонке — убери колонку (имя ТП — FirstName, SecondName), таймаут — упрости. Это не «нет прав» и не «сбой " +
        "источника» — так не пиши. Официальные итоги (выручка, кг, план, АКБ за период) — готовыми " +
        "инструментами продаж: свой запрос их не заменяет. Отвечай по строкам результата и назови правило подсчёта (что считалось визитом, заказом, " +
        "периодом).";

    /// <summary>Запрос исправляют по тексту ошибки PostgreSQL или проверки — попыток больше, чем у инструмента с готовыми аргументами.</summary>
    public override int MaxFailures => 6;

    public override JsonElement InputSchema { get; } = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["sql"] = new
            {
                type = "string",
                description = "Один SELECT или WITH … SELECT (PostgreSQL 16), без «;» в середине и без $-параметров. Пример: WITH o AS (SELECT \"MarketId\", " +
                              "\"CreatedDate\" d, sum(\"TotalWeight\") kg FROM linko.\"Orders\" WHERE \"Status\" IN ('delivered','given') AND \"CreatedDate\" " +
                              ">= '2026-08-01' GROUP BY 1, 2) SELECT \"MarketId\", d, kg, d - lag(d) OVER (PARTITION BY \"MarketId\" ORDER BY d) AS days_since FROM o",
            },
            ["purpose"] = new { type = "string", description = "Что считается (одной фразой) — для журнала запросов." },
            ["max_rows"] = new { type = "integer", description = "Строк в ответе (по умолчанию 200, до предела Аналитика). Агрегируй в запросе, а не выгружай сырые строки." },
            ["describe"] = new
            {
                type = "array",
                items = new { type = "string" },
                description = $"Вместо запроса — колонки таблиц (до {DescribeTables}): [\"linko.Orders\", \"sales.Regions\"].",
            },
        },
    });

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        if (args.Element("describe") is { ValueKind: JsonValueKind.Array } describe && describe.GetArrayLength() > 0)
        {
            return await DescribeAsync(context, describe.EnumerateArray().Select(x => x.GetString() ?? "").Take(DescribeTables).ToList(), ct);
        }

        var sql = args.Str("sql") ?? throw new ToolArgumentException("Нужен sql — один SELECT или WITH, либо describe — список таблиц.");
        var rows = args.All || context.Report ? options.MaxRows : args.Int("max_rows", 1, Math.Max(1, options.MaxRows));
        var outcome = await queries.RunAsync(sql, rows, Caller(context), ct);
        if (!outcome.Ok)
        {
            // Нет права на таблицу — ограничение ответа; остальное (скрытая колонка, функция, тип, тяжесть, таймаут) исправляется в запросе.
            return ToolResult.Failure(Name, outcome.Access ? ToolFailures.Forbidden : ToolFailures.InvalidArguments, outcome.Error!);
        }

        var r = outcome.Result!;
        return Data(new
        {
            r.QueryId,
            Columns = r.Columns.Select(c => new { c.Name, c.Type }),
            r.Rows,
            RowCount = r.Rows.Count,
            r.HasMore,
            r.Tables,
            Note = r.HasMore
                ? $"Показаны первые {r.Rows.Count} строк (has_more): сгруппируйте в запросе (итоги, распределение, топ-N с ORDER BY … LIMIT) — не делайте выводов по части строк."
                : null,
        }, new DataSource($"OneBase → свободный запрос: {string.Join(", ", r.Tables)}", r.At.ToOffset(TimeSpan.FromHours(5)).ToString("dd.MM.yyyy HH:mm"),
            $"/analytik/history?query={r.QueryId}"));
    }

    private async Task<ToolResult> DescribeAsync(ToolContext context, IReadOnlyList<string> names, CancellationToken ct)
    {
        var principal = await PrincipalAsync(context, ct);
        var model = await catalog.ModelAsync(ct);
        var tables = names.Select<string, object>(name =>
        {
            if (model.Find(name) is not { } t)
            {
                var hint = model.Tables.Where(x => x.Name.Contains(name.Split('.').Last().Trim('"'), StringComparison.OrdinalIgnoreCase))
                    .Take(5).Select(x => x.FullName).ToList();
                return new { Table = name, Error = "Таблицы нет в каталоге.", Similar = hint };
            }

            var decision = AnalytikAccess.Decide(t, principal);
            if (!decision.Allowed)
            {
                return new { Table = t.FullName, Error = decision.Message };
            }

            return new
            {
                Table = t.FullName,
                t.Title,
                t.Description,
                Rows = t.Rows,
                t.DateColumn,
                RowsLimitedToYourArea = decision.Scope.Count > 0,
                Columns = t.Columns.Where(c => c.Visible).Select(c => new { c.Name, c.DataType, c.Title, c.Description }),
                Hidden = t.Columns.Where(c => !c.Visible).Select(c => c.Name),
                Links = model.Relations.Where(x => x.Joinable && (x.From == t.FullName || x.To == t.FullName)).Take(20)
                    .Select(x => $"{x.From}.{x.FromColumn} → {x.To}.{x.ToColumn}"),
            };
        }).ToList();
        return Data(new { Tables = tables, Hint = $"Пишите запрос: {ToolName} с sql. Колонки — в двойных кавычках, как здесь." });
    }
}
