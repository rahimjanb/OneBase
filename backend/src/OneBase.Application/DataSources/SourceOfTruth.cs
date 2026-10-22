using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;

namespace OneBase.Application.DataSources;

/// <summary>Владельцы данных (источник истины показателя).</summary>
public static class TruthOwners
{
    public const string Linko = "linko";
    public const string GoogleSheets = "google_sheets";
    public const string OneBase = "onebase";
    public const string Calculated = "calculated";

    public static string Label(string owner) => owner switch
    {
        Linko => "Linko",
        GoogleSheets => "Google Sheets",
        OneBase => "OneBase",
        Calculated => "расчёт OneBase",
        _ => owner,
    };
}

/// <summary>Показатель и его источник истины: Owner — кто ведёт данные, Source — откуда именно (таблица, раздел), Detail — что входит.</summary>
public sealed record TruthMetric(string Key, string Label, string Owner, string Source, string Detail, int OpenConflicts = 0);

/// <summary>Значение показателя из одного источника — кандидат при сверке.</summary>
public sealed record SourceValue(string Source, decimal Value, string? Note = null);

/// <summary>
/// Итог сверки значений из разных источников. Status: resolved — победило значение источника-владельца (Conflicts — расходящиеся значения
/// других источников); owner_missing — владелец известен, но его значения среди кандидатов нет: ничего не выбрано; undefined — источник
/// истины не определён: значение произвольно не выбирается.
/// </summary>
public sealed record TruthResolution(string Status, string? Owner, SourceValue? Value, IReadOnlyList<SourceValue> Conflicts, string Explanation);

/// <summary>
/// Источник истины показателей OneBase. Факт (продажи, возвраты, визиты, остатки, перемещения, оплаты точек, планы ТП, акции) — Linko;
/// поля OneBase (планы регионов, дилер и СВР региона, P&L, реестры взаиморасчётов) — включённая привязка Google Sheets или сам OneBase;
/// выполнение плана и показатели P&L — расчёт при показе. Google Sheets не перезаписывает показатели Linko; «новее — значит вернее» не
/// действует: побеждает источник-владелец.
/// </summary>
public static class SourceOfTruth
{
    /// <summary>Группы данных Linko и сущности синхронизации Linko, по которым видно, когда они обновлены.</summary>
    public static readonly IReadOnlyList<(string Key, string Label, string Detail, string[] Entities)> LinkoGroups =
    [
        ("linko_sales", "Факт продаж и возвратов", "заказы и возвраты: вторичка, ассортимент, SKU, прямые клиенты первички", ["orders", "order_returns"]),
        ("linko_visits", "Визиты", "визиты агентов, конверсия визита, проблемные агенты", ["visits"]),
        ("linko_outlets", "Точки, агенты, товары", "справочники Linko: торговые точки, пользователи, товары и категории", ["markets", "users", "products"]),
        ("linko_stock", "Остатки и перемещения", "остатки складов, аутсток, перемещения завод → дилеры (первичка)", ["product_balances", "stock_transfers"]),
        ("linko_payments", "Оплаты", "платежи клиентов", ["payments"]),
        ("linko_kpi", "Планы ТП", "планы агентов из API планов Linko", ["kpi_plans"]),
    ];

    /// <summary>Показатели, которые OneBase считает при показе и не хранит.</summary>
    public static readonly IReadOnlyList<TruthMetric> Calculated =
    [
        new("kpi", "Выполнение плана, отклонение, прогноз", TruthOwners.Calculated, "Расчёт OneBase",
            "считается при показе из плана (OneBase или Google Sheets) и факта Linko — не хранится, источники его не пишут"),
        new("pnl_calc", "P&L: валовая прибыль, EBIT, рентабельность", TruthOwners.Calculated, "Расчёт OneBase", "считается из строк P&L дилеров"),
        new("settlements", "Акт сверки и DSO: остатки, долги, просрочка", TruthOwners.Calculated, "Расчёт OneBase",
            "отгрузки и возвраты — Linko, оплаты, расходы, сроки и остатки акта — реестры Google Sheets; остаток и долг считаются, не хранятся"),
    ];

    /// <summary>Правила приоритета — коротко, для AI и подсказок.</summary>
    public const string Rules =
        "Источник истины назначен каждому показателю: факт продаж, возвратов, визитов, остатков, перемещений, оплат точек, планы ТП и акции — Linko; " +
        "поле OneBase, которое ведёт включённая привязка Google Sheets, — эта таблица, иначе — OneBase; выполнение плана и P&L — расчёт OneBase. " +
        "Google Sheets не перезаписывает показатели Linko; одни и те же записи из разных источников не суммируются; новее — не значит вернее: " +
        "при расхождении верно значение источника-владельца, остальные — конфликт, о котором нужно сказать. Если владелец не определён — значение " +
        "не выбирается произвольно.";

    /// <summary>
    /// Сверка значений одного показателя из разных источников: побеждает источник-владелец (owner); значения других источников, отличные
    /// от него больше tolerance, — конфликты. Владелец не задан — undefined, нет его значения — owner_missing: в обоих случаях ничего не выбрано.
    /// </summary>
    public static TruthResolution Resolve(string? owner, IReadOnlyList<SourceValue> candidates, decimal tolerance = 0.5m)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            return new TruthResolution("undefined", null, null, candidates.ToList(),
                "Источник истины для показателя не определён — значение не выбирается произвольно: нужно назначить владельца показателя.");
        }

        var own = candidates.FirstOrDefault(c => string.Equals(c.Source, owner, StringComparison.OrdinalIgnoreCase));
        if (own is null)
        {
            return new TruthResolution("owner_missing", owner, null, candidates.ToList(),
                $"Значения источника-владельца ({TruthOwners.Label(owner)}) нет — значения других источников его не заменяют.");
        }

        var conflicts = candidates.Where(c => !ReferenceEquals(c, own) && Math.Abs(c.Value - own.Value) > tolerance).ToList();
        return new TruthResolution("resolved", owner, own, conflicts, conflicts.Count == 0
            ? $"Значение источника-владельца ({TruthOwners.Label(owner)}); другие источники с ним совпадают."
            : $"Верно значение источника-владельца ({TruthOwners.Label(owner)}); расходятся: " +
              string.Join(", ", conflicts.Select(c => $"{TruthOwners.Label(c.Source)} — {c.Value:0.##}")) + " — это конфликт источников.");
    }

    /// <summary>Владелец поля OneBase: включённая привязка Google Sheets (bound), иначе OneBase; реестры взаиморасчётов — только Google Sheets.</summary>
    public static string FieldOwner(DataSourceField field, bool bound) =>
        bound || field.Kind is "ledger" or "terms" or "balance" ? TruthOwners.GoogleSheets : TruthOwners.OneBase;
}

/// <summary>Карта источников истины на сейчас: показатели Linko, поля OneBase с их владельцем (по привязкам) и открытыми конфликтами, расчёты.</summary>
public sealed class SourceOfTruthService(IAppDbContext db, FieldOwnership ownership)
{
    public async Task<IReadOnlyList<TruthMetric>> MapAsync(CancellationToken ct = default)
    {
        var owners = await ownership.OwnersAsync(ct);
        var conflicts = await db.DataSourceConflicts.AsNoTracking()
            .Where(c => c.ResolvedAt == null && c.NeedsAction)
            .GroupBy(c => c.Field)
            .Select(g => new { Field = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Field, x => x.Count, ct);
        var rows = SourceOfTruth.LinkoGroups.Select(g => new TruthMetric(g.Key, g.Label, TruthOwners.Linko, "Linko", g.Detail)).ToList();
        foreach (var field in DataSourceCatalog.Fields)
        {
            var bound = owners.TryGetValue(field.Key, out var owner);
            var source = bound ? $"Google Sheets «{owner!.Connection}» ({DataSourceRules.SheetsLabel(owner.Sheets)})"
                : SourceOfTruth.FieldOwner(field, false) == TruthOwners.GoogleSheets ? "Google Sheets (не привязано — данных нет)" : "OneBase";
            rows.Add(new TruthMetric(field.Key, field.Label, SourceOfTruth.FieldOwner(field, bound), source, field.Description,
                conflicts.GetValueOrDefault(field.Key)));
        }

        rows.AddRange(SourceOfTruth.Calculated);
        return rows;
    }

    /// <summary>Показатель по ключу или словам названия («план РОП», «оплаты дилеров»); null — не найден.</summary>
    public static TruthMetric? Find(IReadOnlyList<TruthMetric> map, string query)
    {
        var q = query.Trim().ToLowerInvariant();
        if (q.Length == 0)
        {
            return null;
        }

        // Основы слов (до 6 букв): «оплаты дилеров» найдёт «Оплаты дилеров», «планы РОП» — «План РОП».
        var stems = q.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .Select(w => w.Length > 4 ? w[..^1] : w)
            .Select(w => w[..Math.Min(w.Length, 6)])
            .ToList();
        return map.FirstOrDefault(m => m.Key == q)
               ?? map.FirstOrDefault(m => m.Label.ToLowerInvariant().Contains(q))
               ?? (stems.Count == 0 ? null : map.FirstOrDefault(m => stems.All(s => m.Label.ToLowerInvariant().Contains(s))));
    }
}
