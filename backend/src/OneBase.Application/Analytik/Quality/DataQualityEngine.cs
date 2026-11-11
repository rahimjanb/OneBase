using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Catalog;
using OneBase.Application.Analytik.Readiness;
using OneBase.Application.Analytik.Risk;
using OneBase.Application.Security;
using OneBase.Domain.Production;

namespace OneBase.Application.Analytik.Quality;

/// <summary>
/// Data Quality Engine: свежесть источников (синхронизация Linko по сущностям, Verifix), открытые конфликты Linko и Google Sheets (их находит
/// существующий контроль источников — здесь они только поднимаются в риски), ссылочная целостность подтверждённых связей каталога (доля значений
/// без записи в целевой таблице), будущие даты, отрицательные остатки, дубли названий позиций, пробелы настроек, без которых часть аналитики
/// не считается (минимумы, сроки поставки, связь техкарты с товаром продаж). Противоречивые данные не объединяются: владелец показателя —
/// SourceOfTruth, здесь — только сигнал.
/// </summary>
public sealed class DataQualityEngine(IAppDbContext db, CatalogService catalog, IAnalytikSql sql, DataReadiness readiness, TimeProvider clock)
{
    public static readonly string[] Rules =
    [
        "quality.linko_sync", "quality.verifix_sync", "quality.source_conflicts", "quality.orphans", "quality.future_dates", "quality.negative_stock",
        "quality.duplicate_items", "quality.missing_minimums", "quality.missing_lead_time", "quality.recipe_product_link", "quality.hr_schedule",
        "quality.sales_plan",
    ];

    /// <summary>Находки качества данных и правила, которые удалось проверить (непроверенное правило не закрывает свою прежнюю находку).</summary>
    public async Task<(IReadOnlyList<InsightCandidate> Found, IReadOnlySet<string> Evaluated)> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var found = new List<InsightCandidate>();
        var failed = new HashSet<string>(StringComparer.Ordinal);

        // 1. Свежесть Linko: синхронизация каждые 30 минут — больше 3 часов без удачной — предупреждение, больше суток — критично. Одна находка
        //    на все отстающие сущности (а не по находке на каждую): иначе один сбой обмена дал бы десятки рисков и уведомлений.
        // Служебные строки обмена (открытая проблема контроля, время сверки) — не данные: их «ошибка» — это текст уведомления.
        ReadinessReport? report = null;
        try
        {
            report = await readiness.CheckAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failed.Add("quality.sales_plan"); // без готовности данных планы не проверены — прежняя находка не закрывается
        }

        var states = (await db.LinkoSyncStates.AsNoTracking().ToListAsync(ct)).Where(s => s.Entity is not ("alert" or "reconcile")).ToList();
        var stale = states
            .Select(s => (State: s, Age: s.LastSuccessAt is { } at ? now - at : (TimeSpan?)null))
            .Where(x => x.State.LastError is { Length: > 0 } || x.Age is null || x.Age > TimeSpan.FromHours(3))
            .OrderByDescending(x => x.Age ?? TimeSpan.MaxValue)
            .ToList();
        if (stale.Count > 0)
        {
            var oldest = stale.Select(x => x.State.LastSuccessAt).Where(x => x is not null).DefaultIfEmpty().Min();
            var errors = stale.Where(x => x.State.LastError is { Length: > 0 }).ToList();
            var critical = stale.Any(x => x.Age is null || x.Age > TimeSpan.FromHours(24));
            // Причина из подключения (не подключён, обмен выключен) — подтверждённая; что уже сделано — из контроля данных Аналитика.
            var link = report?.Link(SourceCodes.Linko);
            var sales = report?.Datasets.FirstOrDefault(d => d.Code == "sales");
            var linkCause = link is null ? null
                : !link.Connected ? link.Problem
                : !link.AutoSync ? "Автоматический обмен с Linko выключен — данные обновляются только кнопкой «Обновить»."
                : null;
            var done = sales?.Action;
            found.Add(new InsightCandidate(InsightKinds.Quality, "sales", "quality.linko_sync", "quality.linko_sync", critical ? Severities.Critical : Severities.Warning,
                $"Данные Linko не обновляются: {stale.Count} из {states.Count} сущностей",
                (oldest is { } o ? $"Самая старая удачная синхронизация — {o.ToOffset(TimeSpan.FromHours(5)):dd.MM.yyyy HH:mm}. " : "") +
                "Отстают: " + string.Join(", ", stale.Take(12).Select(x => x.State.Entity)) + (stale.Count > 12 ? $" и ещё {stale.Count - 12}" : "") + ".",
                Cause: linkCause ?? (errors.Count > 0 ? $"Ошибка последней попытки ({errors[0].State.Entity}): {Short(errors[0].State.LastError!, 300)}" : null),
                CauseConfirmed: linkCause is not null || errors.Count > 0,
                Hypotheses: linkCause is not null || errors.Count > 0 ? null : ["Сервер API с синхронизацией не запущен", "Нет связи с Linko"],
                Impact: stale.Count, ImpactUnit: "сущностей",
                Consequences: "Отчёты продаж и Аналитик показывают цифры на дату последней синхронизации; вопросы о более поздних днях AI-аналитик " +
                              "разбирает по последним загруженным данным.",
                Recommendations: done is null
                    ? ["Проверить «Настройки → Интеграции → Linko» и журнал синхронизации."]
                    : ["Проверить «Настройки → Интеграции → Linko» и журнал синхронизации.", $"Уже сделано: {done}"],
                Evidence: new { Entities = stale.Select(x => new { x.State.Entity, x.State.LastSuccessAt, x.State.LastError }) },
                Href: "/settings/data-sources", RequiredPermission: Permissions.SalesRead));
        }

        // 2. Свежесть Verifix.
        var lastHr = await db.HrSyncRuns.AsNoTracking().Where(r => r.Status == "Succeeded" || r.Status == "Partial")
            .MaxAsync(r => (DateTimeOffset?)r.FinishedAt, ct);
        var anyHr = await db.HrSyncRuns.AnyAsync(ct);
        if (anyHr && (lastHr is null || now - lastHr > TimeSpan.FromHours(6)))
        {
            found.Add(new InsightCandidate(InsightKinds.Quality, "hr", "quality.verifix_sync", "quality.verifix_sync", Severities.Warning,
                "Данные Verifix не обновляются",
                lastHr is { } h ? $"Последняя удачная синхронизация Verifix — {h.ToOffset(TimeSpan.FromHours(5)):dd.MM.yyyy HH:mm}." : "Удачной синхронизации Verifix нет.",
                Consequences: "Посещаемость и опоздания в HR и Аналитике неполные.", Recommendations: ["Проверить «HR → Настройки → Verifix»."],
                Href: "/hr", RequiredPermission: Permissions.HrRead));
        }

        // 2а. Планы продаж на текущий месяц не заведены (а в прошлом были): выполнение плана и его прогноз не считаются.
        if (report?.Datasets.FirstOrDefault(d => d.Code == "plans" && d.Status == ReadinessStatus.Missing) is { } plans)
        {
            found.Add(new InsightCandidate(InsightKinds.Quality, "sales", "quality.sales_plan", "quality.sales_plan", Severities.Warning,
                $"{plans.Title} нет в OneBase", plans.Reason ?? plans.Title,
                CauseConfirmed: true, Consequences: "Выполнение плана, прогноз выполнения и отставание регионов от плана в Аналитике и отчётах не считаются.",
                Recommendations: ["Завести планы месяца в «Продажи → Планы» или проверить загрузку планов из Linko."],
                Href: plans.Href, RequiredPermission: Permissions.SalesRead));
        }

        // 3. Открытые конфликты источников (Linko против Google Sheets и др.), которые требуют решения.
        var conflicts = await db.DataSourceConflicts.AsNoTracking().Where(c => c.ResolvedAt == null && c.NeedsAction)
            .GroupBy(c => c.Field).Select(g => new { Field = g.Key, Count = g.Count(), Sample = g.Select(c => c.Message).FirstOrDefault() }).ToListAsync(ct);
        foreach (var c in conflicts)
        {
            found.Add(new InsightCandidate(InsightKinds.Quality, "system", "quality.source_conflicts", $"quality.source_conflicts:{c.Field}", Severities.Warning,
                $"Конфликт источников данных: {c.Field} ({c.Count})", c.Sample ?? "Значения из разных источников расходятся.",
                Consequences: "Показатель берётся из источника-владельца; расхождение нужно разобрать, автоматически данные не объединяются.",
                Recommendations: ["Разобрать конфликт в «Настройки → Источники данных»."], Impact: c.Count, ImpactUnit: "конфликтов",
                Href: "/settings/data-sources", RequiredPermission: Permissions.IntegrationsManage));
        }

        // 4. Ссылочная целостность: подтверждённые связи без внешнего ключа, у которых часть значений не находится в целевой таблице.
        var model = await catalog.ModelAsync(ct);
        foreach (var r in model.Relations.Where(r => r.Kind != RelationKinds.ForeignKey && r.Status == RelationStatus.Confirmed && r.MatchRatio is < 0.98m
                     && r.Checked >= 20))
        {
            var missing = Math.Round((1 - r.MatchRatio!.Value) * 100, 1);
            var table = model.Find(r.From);
            found.Add(new InsightCandidate(InsightKinds.Quality, table?.Module ?? "other", "quality.orphans", $"quality.orphans:{r.From}.{r.FromColumn}",
                missing >= 10 ? Severities.Warning : Severities.Info,
                $"{missing}% значений {r.From}.{r.FromColumn} не найдены в {r.To}",
                $"Проверено {r.Checked} различных значений {r.FromColumn}: у {missing}% нет записи в {r.To}.{r.ToColumn}.",
                Hypotheses: ["Записи справочника удалены или ещё не синхронизированы", "Данные из старого периода (уволенные ТП, закрытые точки)"],
                Consequences: "Строки без справочника выпадают из разрезов по нему (например, продажи без ТП в справочнике).",
                Evidence: new { r.From, r.FromColumn, r.To, r.ToColumn, r.MatchRatio, r.Checked }, Href: "/analytik/data",
                RequiredPermission: table?.RequiredPermission));
        }

        // 5–7. Бизнес-правила по самим данным (запросы только на чтение).
        await CheckAsync(found, "quality.future_dates", "sales",
            """SELECT count(*) FROM "linko"."Orders" WHERE "CreatedDate" > (now() AT TIME ZONE 'Asia/Tashkent')::date + 1""",
            n => n > 0 ? new InsightCandidate(InsightKinds.Quality, "sales", "quality.future_dates", "quality.future_dates:orders", Severities.Warning,
                $"Заказы с датой в будущем: {n}", $"У {n} заказов Linko дата создания позже завтрашнего дня.",
                Hypotheses: ["Ошибка даты в Linko или часового пояса"], Consequences: "Такие заказы не попадают в отчёт текущего дня.", Impact: n, ImpactUnit: "заказов",
                RequiredPermission: Permissions.SalesRead) : null, ct);
        await CheckAsync(found, "quality.negative_stock", "procurement",
            """SELECT count(*) FROM "procurement"."StockBalances" WHERE "OnHand" < 0 OR "Reserved" < 0 OR "Reserved" > "OnHand" """,
            n => n > 0 ? new InsightCandidate(InsightKinds.Quality, "procurement", "quality.negative_stock", "quality.negative_stock", Severities.Critical,
                $"Остатки с нарушением правил: {n}", $"У {n} строк остатков отрицательный остаток или резерв больше остатка.",
                Recommendations: ["Провести инвентаризацию позиций и проверить журнал движений."], Impact: n, ImpactUnit: "строк",
                Href: "/procurement/stock", RequiredPermission: "procurement.read|procurement.manage|procurement.warehouse") : null, ct);
        await CheckAsync(found, "quality.duplicate_items", "procurement",
            """SELECT count(*) FROM (SELECT lower(btrim("Name")) FROM "procurement"."Items" WHERE "IsActive" GROUP BY 1 HAVING count(*) > 1) d""",
            n => n > 0 ? new InsightCandidate(InsightKinds.Quality, "procurement", "quality.duplicate_items", "quality.duplicate_items", Severities.Info,
                $"Повторяющиеся названия позиций: {n}", $"{n} названий сырья и материалов встречаются у нескольких активных позиций с разными артикулами.",
                Hypotheses: ["Одна позиция заведена дважды при импорте"], Consequences: "Остатки и расход одной позиции делятся между карточками — дефицит виден хуже.",
                Recommendations: ["Проверить дубли в каталоге склада и объединить или отключить лишние."], Impact: n, ImpactUnit: "названий",
                Href: "/procurement/stock", RequiredPermission: "procurement.read|procurement.manage|procurement.warehouse") : null, ct);

        // 8. Опоздания «на полсмены»: приход позже начала смены по графику больше чем на 4 часа. Обычно это не опоздание, а график, не
        //    совпадающий с фактической сменой (ночная, вторая смена), — такие дни раздувают минуты опозданий в HR и Аналитике.
        try
        {
            var late = await sql.QueryAsync(
                """
                SELECT count(*) FILTER (WHERE m > 240), count(*), coalesce(sum(m) FILTER (WHERE m > 240), 0), coalesce(sum(m), 0)
                FROM (SELECT EXTRACT(EPOCH FROM ("FirstIn" - "PlanBegin")) / 60 AS m
                      FROM "hr"."TimesheetDays"
                      WHERE "Date" >= (now() AT TIME ZONE 'Asia/Tashkent')::date - 30 AND "FirstIn" > "PlanBegin") d
                """, [], new SqlLimits(1, 10), ct);
            var row = late.Rows[0];
            long outliers = Convert.ToInt64(row[0] ?? 0), cases = Convert.ToInt64(row[1] ?? 0);
            decimal outlierMinutes = Convert.ToDecimal(row[2] ?? 0), minutes = Convert.ToDecimal(row[3] ?? 0);
            var share = minutes > 0 ? Math.Round(outlierMinutes / minutes * 100, 0) : 0;
            if (outliers >= 20 && share >= 30)
            {
                found.Add(new InsightCandidate(InsightKinds.Quality, "hr", "quality.hr_schedule", "quality.hr_schedule", Severities.Warning,
                    $"Опозданий больше 4 часов: {outliers} — {share:0}% всех минут опозданий",
                    $"За 30 дней по табелю Verifix {outliers} из {cases} опозданий — приход позже начала смены по графику больше чем на 4 часа; " +
                    $"на них приходится {share:0}% минут опозданий.",
                    Hypotheses: ["График сотрудника не совпадает с фактической сменой (ночная или вторая смена)", "Сотрудник пришёл на часть дня по договорённости"],
                    Consequences: "Минуты опозданий в HR и Аналитике завышены; сравнение подразделений по опозданиям искажено.",
                    Recommendations: ["Проверить графики сотрудников с такими днями в Verifix и назначить правильную смену."],
                    Impact: outliers, ImpactUnit: "дней", Evidence: new { Outliers = outliers, Cases = cases, OutlierMinutes = outlierMinutes, Minutes = minutes },
                    Href: "/hr", RequiredPermission: Permissions.HrRead));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failed.Add("quality.hr_schedule");
        }

        // 9–11. Пробелы настроек, без которых часть аналитики не считается.
        var items = await db.ProcurementItems.CountAsync(i => i.IsActive, ct);
        var withMin = await db.ProcurementItems.CountAsync(i => i.IsActive && i.MinQty > 0, ct);
        if (items > 0 && withMin < items / 2)
        {
            found.Add(new InsightCandidate(InsightKinds.Quality, "procurement", "quality.missing_minimums", "quality.missing_minimums", Severities.Info,
                $"Минимальные остатки заданы у {withMin} из {items} позиций",
                "Без минимума страховой запас считается только по разбросу расхода, а позиции «ниже минимума» не видны.",
                Recommendations: ["Задать минимумы по расходу: «Склад → Задать минимумы»."], Impact: items - withMin, ImpactUnit: "позиций",
                Href: "/procurement/stock", RequiredPermission: "procurement.read|procurement.manage|procurement.warehouse"));
        }

        var suppliers = await db.ProcurementSuppliers.CountAsync(s => s.IsActive, ct);
        var withLead = await db.ProcurementSuppliers.CountAsync(s => s.IsActive && s.LeadTimeDays != null, ct);
        if (suppliers > 0 && withLead < suppliers / 2)
        {
            found.Add(new InsightCandidate(InsightKinds.Quality, "procurement", "quality.missing_lead_time", "quality.missing_lead_time", Severities.Info,
                $"Срок поставки задан у {withLead} из {suppliers} поставщиков",
                "Без срока поставки дата заказа в плане закупок считается по сроку по умолчанию — это допущение.",
                Recommendations: ["Указать срок поставки в карточках основных поставщиков сырья."], Impact: suppliers - withLead, ImpactUnit: "поставщиков",
                Href: "/procurement/suppliers", RequiredPermission: "procurement.read|procurement.manage"));
        }

        var activeRecipes = await db.ProductionRecipes.CountAsync(r => r.Status == ProductionRecipeStatus.Active, ct);
        var linked = await db.ProductionRecipes.CountAsync(r => r.Status == ProductionRecipeStatus.Active && r.LinkoProductId != null, ct);
        if (activeRecipes > 0 && linked < activeRecipes)
        {
            found.Add(new InsightCandidate(InsightKinds.Quality, "production", "quality.recipe_product_link", "quality.recipe_product_link", Severities.Info,
                $"Действующих техкарт без товара продаж: {activeRecipes - linked} из {activeRecipes}",
                "Без связи техкарты с товаром Linko потребность в сырье от плана продаж не рассчитывается — только от заданий и истории расхода.",
                Recommendations: ["Указать товар Linko в карточке техкарты."], Impact: activeRecipes - linked, ImpactUnit: "техкарт",
                Href: "/production/technologist", RequiredPermission: "production.read|production.manage|production.recipes"));
        }

        return (found, Rules.Where(r => !failed.Contains(r)).ToHashSet(StringComparer.Ordinal));

        async Task CheckAsync(List<InsightCandidate> list, string rule, string module, string text, Func<long, InsightCandidate?> make, CancellationToken token)
        {
            try
            {
                var result = await sql.QueryAsync(text, [], new SqlLimits(1, 10), token);
                if (make(Convert.ToInt64(result.Rows[0][0] ?? 0)) is { } candidate)
                {
                    list.Add(candidate);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Проверка не выполнилась — правило не считается проверенным, прежняя находка остаётся.
                failed.Add(rule);
            }
        }
    }

    private static string Short(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
