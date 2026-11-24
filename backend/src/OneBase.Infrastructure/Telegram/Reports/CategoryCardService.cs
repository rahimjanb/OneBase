using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Audit;
using OneBase.Domain.Telegram;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>Товар категории для выбора: id товара Linko, название и код.</summary>
public sealed record CategoryCardProduct(long Id, string Name, string? Code);

/// <summary>Категория для выбора: название из «Настроек продаж», второе название («Помадка» — «Могуль») и её товары из справочника Linko.</summary>
public sealed record CategoryCardOption(string Name, string? Alias, IReadOnlyList<CategoryCardProduct> Products);

/// <summary>Выбор категорий получателя; Categories = null — выбор по умолчанию; Products — выбранные товары категорий (нет категории — вся).</summary>
public sealed record CategoryCardRecipientView(Guid Id, IReadOnlyList<string>? Categories, IReadOnlyDictionary<string, IReadOnlyList<long>>? Products = null);

/// <summary>
/// «Карточки категорий»: категории отчёта по порядку настройки с их товарами, выбор по умолчанию (Defaults пусто — все категории;
/// DefaultProducts — выбранные товары категорий), свой выбор получателей и сколько категорий помещается в одну отправку (альбом Telegram).
/// </summary>
public sealed record CategoryCardsView(IReadOnlyList<CategoryCardOption> Categories, IReadOnlyList<string> Defaults, IReadOnlyList<CategoryCardRecipientView> Recipients,
    int Max, DateTimeOffset? UpdatedAt, IReadOnlyDictionary<string, IReadOnlyList<long>>? DefaultProducts = null);

/// <summary>
/// Выбор категорий: названия в нужном порядке; пусто или null — «все» (по умолчанию) или «как по умолчанию» (у получателя). Products —
/// товары выбранных категорий (название → id товаров Linko); категории нет или список пуст — вся категория.
/// </summary>
public sealed record CategoryCardsInput(IReadOnlyList<string>? Categories, IReadOnlyDictionary<string, long[]>? Products = null);

/// <summary>
/// «Настройки → Telegram → Автоматические отчёты → Карточки категорий»: какие категории (и какие их товары) уходят по умолчанию и свой выбор
/// получателя. Сами отправки — обычный конвейер автоотчётов (запуски category-day.* отчёта «11. Итоги дня по категориям», получатели,
/// предпросмотр, тест, история).
/// </summary>
public sealed class CategoryCardService(IAppDbContext db, SalesOptions sales, IAuditLogger audit, TimeProvider clock)
{
    public async Task<CategoryCardsView> GetAsync(CancellationToken ct)
    {
        var settings = await db.TelegramReportSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var recipients = await db.TelegramReportSubscriptions.AsNoTracking().OrderBy(s => s.CreatedAt).Select(s => new { s.Id, s.Categories }).ToListAsync(ct);
        var defaults = Known(CategoryCardChoice.Parse(settings?.CategoryCards));
        return new CategoryCardsView(
            await OptionsAsync(ct),
            defaults.Select(p => p.Category).ToList(),
            recipients.Select(r => Recipient(r.Id, r.Categories)).ToList(),
            CategoryCardChoice.Max,
            settings?.UpdatedAt,
            ProductsOf(defaults));
    }

    /// <summary>Выбор по умолчанию (пусто — все категории): уходит всем получателям без своего выбора.</summary>
    public async Task<CategoryCardsView> SaveDefaultsAsync(CategoryCardsInput input, Guid userId, CancellationToken ct)
    {
        var picks = await ValidateAsync(input, ct);
        var settings = await db.TelegramReportSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new TelegramReportSettings();
            db.TelegramReportSettings.Add(settings);
        }

        settings.CategoryCards = CategoryCardChoice.Format(picks);
        settings.UpdatedAt = clock.GetUtcNow();
        settings.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.category_cards", nameof(TelegramReportSettings), "1",
            new { Categories = picks.Select(p => p.Category).ToList(), Products = ProductsOf(picks) }, ct);
        return await GetAsync(ct);
    }

    /// <summary>Свой выбор получателя; пусто — снова по умолчанию.</summary>
    public async Task<CategoryCardsView> SaveRecipientAsync(Guid recipientId, CategoryCardsInput input, Guid userId, CancellationToken ct)
    {
        var picks = await ValidateAsync(input, ct);
        var row = await db.TelegramReportSubscriptions.FirstOrDefaultAsync(s => s.Id == recipientId, ct) ?? throw new KeyNotFoundException("Получатель не найден.");
        row.Categories = CategoryCardChoice.Format(picks);
        row.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.recipient_categories", nameof(TelegramReportSubscription), recipientId.ToString(),
            new { Categories = picks.Select(p => p.Category).ToList(), Products = ProductsOf(picks) }, ct);
        return await GetAsync(ct);
    }

    /// <summary>Выбор предпросмотра: категории и их товары (название категории без учёта регистра); без проверки — построитель берёт только товары категории.</summary>
    public static IReadOnlyList<CategoryCardPick> Picks(IReadOnlyList<string> categories, IReadOnlyDictionary<string, long[]>? products) =>
        CategoryCardChoice.Normalize(categories.Select(c => new CategoryCardPick(c ?? "",
            products?.FirstOrDefault(p => string.Equals(p.Key?.Trim(), c?.Trim(), StringComparison.OrdinalIgnoreCase)).Value)));

    /// <summary>Выбор получателя: null — по умолчанию; иначе — его категории и выбранные товары.</summary>
    private CategoryCardRecipientView Recipient(Guid id, string? stored)
    {
        if (stored is null)
        {
            return new CategoryCardRecipientView(id, null);
        }

        var own = Known(CategoryCardChoice.Parse(stored));
        return new CategoryCardRecipientView(id, own.Select(p => p.Category).ToList(), ProductsOf(own));
    }

    /// <summary>Выбранные товары по категориям (только категории с выбором товаров).</summary>
    private static Dictionary<string, IReadOnlyList<long>> ProductsOf(IEnumerable<CategoryCardPick> picks) =>
        picks.Where(p => !p.Whole).ToDictionary(p => p.Category, p => p.Products!);

    /// <summary>
    /// Категории отчёта по порядку настройки и их товары из справочника Linko: тип товара → категория, как в «Продажах» (подтип — в категорию
    /// ближайшего настроенного родителя). Товары — по коду (числом), затем по названию.
    /// </summary>
    private async Task<List<CategoryCardOption>> OptionsAsync(CancellationToken ct)
    {
        var map = SalesCategories.Build(sales.Categories, await LinkoTypeTree.LoadAsync(db, ct));
        var products = await db.LinkoProducts.AsNoTracking().Select(p => new { p.Id, p.Name, p.Code, p.TypeId }).ToListAsync(ct);
        return sales.Categories.Keys
            .Select(name => (Name: name, Group: map.GroupByName(name)))
            .Select(c => new CategoryCardOption(c.Name, CategoryCardChoice.AliasOf(c.Name), products
                .Where(p => c.Group is not null && map.GroupOf(p.TypeId) == c.Group)
                .Select(p => new CategoryCardProduct(p.Id, p.Name.Trim(), string.IsNullOrWhiteSpace(p.Code) ? null : p.Code.Trim()))
                .OrderBy(p => long.TryParse(p.Code, out var n) ? n : long.MaxValue)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .ToList()))
            .ToList();
    }

    /// <summary>Категории из настройки в её написании (регистр не важен); категории, которых в настройке больше нет, — отбрасываются.</summary>
    private List<CategoryCardPick> Known(IEnumerable<CategoryCardPick> picks) =>
        picks
            .Select(p => sales.Categories.Keys.FirstOrDefault(k => string.Equals(k, p.Category, StringComparison.OrdinalIgnoreCase)) is { } name ? p with { Category = name } : null)
            .OfType<CategoryCardPick>()
            .DistinctBy(p => p.Category)
            .ToList();

    /// <summary>
    /// Проверка выбора: категории — из «Настроек продаж», не больше Max; товары — только товары своей категории (выбраны все — вся категория,
    /// чтобы новые товары тоже попадали); товары невыбранных категорий не сохраняются.
    /// </summary>
    private async Task<List<CategoryCardPick>> ValidateAsync(CategoryCardsInput input, CancellationToken ct)
    {
        var names = (input.Categories ?? []).Select(n => n?.Trim() ?? "").Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var unknown = names.Where(n => !sales.Categories.Keys.Any(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase))).ToList();
        if (unknown.Count > 0)
        {
            throw new ReportValidationException($"Таких категорий нет в «Настройках продаж»: {string.Join(", ", unknown)}.");
        }

        if (names.Count > CategoryCardChoice.Max)
        {
            throw new ReportValidationException($"В одной отправке — не больше {CategoryCardChoice.Max} категорий (альбом Telegram).");
        }

        var picks = Known(Picks(names, input.Products));
        if (picks.Any(p => !p.Whole))
        {
            var options = (await OptionsAsync(ct)).ToDictionary(o => o.Name, o => o.Products.Select(x => x.Id).ToHashSet());
            for (var i = 0; i < picks.Count; i++)
            {
                if (picks[i].Whole)
                {
                    continue;
                }

                var all = options[picks[i].Category];
                var foreign = picks[i].Products!.Where(id => !all.Contains(id)).ToList();
                if (foreign.Count > 0)
                {
                    throw new ReportValidationException($"Товары не из категории «{picks[i].Category}»: {string.Join(", ", foreign)}.");
                }

                if (all.SetEquals(picks[i].Products!))
                {
                    picks[i] = picks[i] with { Products = null };
                }
            }
        }

        if (CategoryCardChoice.Format(picks) is { Length: > CategoryCardChoice.StoredMax })
        {
            throw new ReportValidationException("Выбрано слишком много отдельных товаров — оставьте у части категорий «все товары».");
        }

        return picks;
    }
}
