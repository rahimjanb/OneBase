using System.Text.RegularExpressions;

namespace OneBase.Application.Analytik.Catalog;

/// <summary>Найденная таблица: оценка совпадения и почему она найдена.</summary>
public sealed record CatalogHit(CatalogTableInfo Table, double Score, IReadOnlyList<string> Why);

/// <summary>
/// Поиск по каталогу словами вопроса: название и описание таблицы, имена и подписи колонок, словарь бизнес-слов OneBase (русский, узбекский
/// латиницей и кириллицей, английский) → таблицы. Не требует от пользователя знать имена таблиц.
/// </summary>
public static partial class CatalogSearch
{
    /// <summary>Основа слова → таблицы, которые оно обозначает.</summary>
    private static readonly (string[] Stems, string[] Tables)[] Synonyms =
    [
        (["клиент", "точк", "магазин", "customer", "market", "mijoz", "мижоз", "do'kon", "дўкон", "акб"], ["linko.Markets"]),
        (["заказ", "order", "buyurtma", "буюртма"], ["linko.Orders", "linko.OrderLines"]),
        (["продаж", "выручк", "вторичк", "sales", "revenue", "savdo", "sotuv", "савдо", "сотув", "оборот"], ["linko.Orders", "linko.OrderLines", "linko.OrderReturns"]),
        (["возврат", "return", "qaytar", "қайтар"], ["linko.OrderReturns", "linko.OrderReturnLines"]),
        (["визит", "visit", "страйк", "маршрут", "tashrif", "ташриф"], ["linko.Visits"]),
        (["товар", "продукт", "sku", "артикул", "product", "mahsulot", "маҳсулот", "ассортимент"], ["linko.Products", "linko.ProductTypes"]),
        (["категор", "помадк", "кекс", "трубоч", "печень", "бамбук", "шоколад", "могул"], ["linko.ProductTypes", "linko.Products"]),
        (["агент", "торгов", "супервайзер", "agent"], ["linko.Users"]),
        (["регион", "филиал", "област", "направлен", "region", "hudud", "ҳудуд", "viloyat", "вилоят"], ["sales.Regions", "sales.Directions"]),
        (["план", "plan", "reja", "режа"], ["sales.RegionPlans", "sales.FactoryPlans", "linko.KpiPlans", "sales.StaffPlans", "production.Orders"]),
        (["первичк", "отгрузк", "перемещ", "дилер", "shipment"], ["linko.StockTransfers", "linko.StockTransferLines"]),
        (["оплат", "платеж", "платёж", "payment", "to'lov", "тўлов"], ["linko.Payments"]),
        (["долг", "дебитор", "dso", "сверк", "qarz", "қарз"], ["sales.DealerLedger", "sales.DealerTerms"]),
        (["прибыл", "pnl", "p&l", "марж", "себестоим", "foyda", "фойда"], ["sales.PnlValues"]),
        (["сырь", "материал", "номенклатур", "xomashyo", "хомашё", "ингредиент"], ["procurement.Items", "procurement.StockBalances"]),
        (["остат", "склад", "запас", "наличи", "stock", "inventory", "ombor", "омбор", "qoldiq", "қолдиқ"], ["procurement.StockBalances", "linko.ProductBalances", "procurement.Items"]),
        (["движен", "расход", "списан", "приход", "выдач", "movement"], ["procurement.StockMovements", "procurement.StockDocuments"]),
        (["парти", "срок годн", "batch"], ["procurement.Batches"]),
        (["поставщик", "supplier", "yetkazib", "етказиб"], ["procurement.Suppliers"]),
        (["закуп", "заявк", "purchase", "xarid", "харид", "ta'minot", "таъминот", "снабжен"], ["procurement.Requests", "procurement.RequestItems", "procurement.PurchaseOrders"]),
        (["тендер", "tender"], ["procurement.Tenders"]),
        (["приезд", "машин", "приёмк", "приемк", "качеств"], ["procurement.Arrivals", "procurement.QualityChecks"]),
        (["техкарт", "рецепт", "рецептур", "норм", "recipe", "retsept"], ["production.Recipes", "production.RecipeLines"]),
        (["производ", "выпуск", "задани", "production", "ishlab", "ишлаб"], ["production.Orders", "production.Shifts"]),
        (["смен", "shift", "smena"], ["production.Shifts"]),
        (["брак", "defect", "nuqson", "нуқсон"], ["production.ShiftDefects", "production.DefectTypes"]),
        (["сотрудник", "персонал", "работник", "employee", "xodim", "ходим", "штат"], ["hr.Employees"]),
        (["опоздан", "табел", "посещ", "отсутств", "рабоч", "kechik", "кечик", "davomat", "давомат"], ["hr.TimesheetDays", "hr.AttendanceEvents"]),
        (["подразделен", "должност", "division"], ["hr.References"]),
        (["кандидат", "ваканс", "подбор", "nomzod", "номзод"], ["recruitment.Candidates", "recruitment.Vacancies", "recruitment.Applications"]),
        (["задач", "task", "vazifa", "вазифа", "проект"], ["work.Tasks", "work.Projects"]),
        (["акци", "скидк", "promo"], ["linko.Promotions", "linko.PromotionProducts"]),
        (["цен", "прайс", "price", "narx", "нарх"], ["linko.PriceListItems", "procurement.StockMovements", "procurement.PurchaseOrderItems"]),
        (["пользовател", "user", "роль"], ["public.Users", "public.Roles"]),
        (["отдел", "department", "bo'lim", "бўлим"], ["public.Departments"]),
        (["ai", "модел", "токен", "стоимост"], ["ai.Usage"]),
        (["находк", "риск", "alert"], ["ai.Alerts", "analytik.Insights"]),
    ];

    [GeneratedRegex(@"[\p{L}\p{N}'&]+")]
    private static partial Regex Token();

    public static IReadOnlyList<string> Tokens(string text) =>
        Token().Matches(text.ToLowerInvariant()).Select(m => m.Value.Trim('\'')).Where(w => w.Length >= 2).Distinct().ToList();

    public static IReadOnlyList<CatalogHit> Search(CatalogModel model, string text, string? module = null, int limit = 15, bool includeClosed = false)
    {
        var tokens = Tokens(text);
        if (tokens.Count == 0)
        {
            return [];
        }

        var boosts = new Dictionary<string, (double Score, string Why)>(StringComparer.Ordinal);
        foreach (var (stems, tables) in Synonyms)
        {
            var stem = stems.FirstOrDefault(s => tokens.Any(t => t.StartsWith(s, StringComparison.Ordinal) || (s.Length >= 4 && t.Contains(s, StringComparison.Ordinal))));
            if (stem is null)
            {
                continue;
            }

            for (var i = 0; i < tables.Length; i++)
            {
                var add = 6.0 - Math.Min(i, 3);
                boosts[tables[i]] = boosts.TryGetValue(tables[i], out var b) ? (b.Score + add, b.Why) : (add, $"слово «{stem}»");
            }
        }

        var hits = new List<CatalogHit>();
        foreach (var t in model.Tables)
        {
            if ((module is not null && t.Module != module) || (!includeClosed && t.Closed))
            {
                continue;
            }

            var why = new List<string>();
            double score = 0;
            if (boosts.TryGetValue(t.FullName, out var boost))
            {
                score += boost.Score;
                why.Add(boost.Why);
            }

            var name = CatalogRules.Words(t.Name);
            var title = (t.Title ?? "").ToLowerInvariant();
            var description = (t.Description ?? "").ToLowerInvariant();
            foreach (var token in tokens)
            {
                var stem = Stem(token);
                if (title.Contains(stem, StringComparison.Ordinal))
                {
                    score += 5;
                    why.Add($"название: {t.Title}");
                }
                else if (name.Contains(stem, StringComparison.Ordinal) || t.FullName.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    score += 4;
                    why.Add($"имя: {t.FullName}");
                }
                else if (description.Contains(stem, StringComparison.Ordinal))
                {
                    score += 1.5;
                }

                var column = t.Columns.FirstOrDefault(c => (c.Title ?? "").Contains(stem, StringComparison.OrdinalIgnoreCase)
                    || CatalogRules.Words(c.Name).Contains(stem, StringComparison.Ordinal));
                if (column is not null)
                {
                    score += 2;
                    why.Add($"колонка {column.Name}");
                }
            }

            if (score > 0)
            {
                // Таблицы с данными — выше пустых при прочих равных.
                score += t.Rows > 0 ? Math.Min(Math.Log10(t.Rows + 1) / 4, 1.5) : -1;
                hits.Add(new CatalogHit(t, Math.Round(score, 2), why.Distinct().Take(4).ToList()));
            }
        }

        return hits.OrderByDescending(h => h.Score).ThenBy(h => h.Table.FullName, StringComparer.Ordinal).Take(limit).ToList();
    }

    /// <summary>Показатели компании, подходящие к словам вопроса.</summary>
    public static IReadOnlyList<MetricKnowledge> Metrics(string text, int limit = 5)
    {
        var tokens = Tokens(text).Select(Stem).ToList();
        return CatalogKnowledge.Metrics
            .Select(m => (Metric: m, Score: tokens.Count(t => m.Title.Contains(t, StringComparison.OrdinalIgnoreCase)
                || m.Definition.Contains(t, StringComparison.OrdinalIgnoreCase) || m.Code.Contains(t, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .Select(x => x.Metric)
            .ToList();
    }

    /// <summary>Грубая основа слова: русские окончания отбрасываются (до 5 букв минимум).</summary>
    internal static string Stem(string token) => token.Length > 6 ? token[..Math.Max(5, token.Length - 2)] : token;
}
