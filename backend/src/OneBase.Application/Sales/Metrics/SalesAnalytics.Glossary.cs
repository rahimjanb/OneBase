namespace OneBase.Application.Sales.Metrics;

/// <summary>Категория отчёта в справочнике: id (как у карточки категории), название, из настройки ли она и типы Linko внутри.</summary>
public sealed record CategoryGlossaryRow(string CategoryId, string Name, bool Configured, IReadOnlyList<string> LinkoTypes);

public sealed partial class SalesAnalytics
{
    /// <summary>Справочник категорий месяца — для поиска по названию (find_products).</summary>
    public IReadOnlyList<CategoryGlossaryRow> CategoryGlossary() => View("glossary", () => BuildGlossary(_cats, _d.Categories));

    /// <summary>
    /// Справочник категорий из карты категорий и типов Linko: категории отчёта (из настройки) с типами Linko внутри,
    /// затем типы вне настройки — каждый сам по себе. Тип с тем же названием, что категория, отдельно не повторяется.
    /// Статический, чтобы словарь для AI собирался из справочников без загрузки месяца продаж.
    /// </summary>
    public static IReadOnlyList<CategoryGlossaryRow> BuildGlossary(SalesCategories cats, IReadOnlyDictionary<long, string> linkoTypes) =>
        linkoTypes
            .GroupBy(t => cats.GroupOf(t.Key))
            .Select(g =>
            {
                var name = cats.NameOf(g.Key);
                var types = g.Select(t => t.Value)
                    .Where(t => !string.Equals(t, name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                return new CategoryGlossaryRow(g.Key?.ToString() ?? "none", name, SalesCategories.IsConfigured(g.Key), types);
            })
            .OrderByDescending(r => r.Configured)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Справочник товаров (SKU) Linko — все, включая не продававшиеся в этом месяце.</summary>
    public IReadOnlyCollection<ProductInfo> ProductCatalog => View("products", () => (IReadOnlyCollection<ProductInfo>)_d.Products.Values.ToList());
}
