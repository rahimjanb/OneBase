using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Audit;
using OneBase.Domain.Telegram;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>Категория для выбора: название из «Настроек продаж» и второе название («Помадка» — «Могуль»).</summary>
public sealed record CategoryCardOption(string Name, string? Alias);

/// <summary>Выбор категорий получателя; Categories = null — выбор по умолчанию.</summary>
public sealed record CategoryCardRecipientView(Guid Id, IReadOnlyList<string>? Categories);

/// <summary>
/// «Карточки категорий»: категории отчёта по порядку настройки, выбор по умолчанию (Defaults пусто — все категории), свой выбор получателей
/// и сколько категорий помещается в одну отправку (альбом Telegram).
/// </summary>
public sealed record CategoryCardsView(IReadOnlyList<CategoryCardOption> Categories, IReadOnlyList<string> Defaults, IReadOnlyList<CategoryCardRecipientView> Recipients,
    int Max, DateTimeOffset? UpdatedAt);

/// <summary>Выбор категорий: названия в нужном порядке; пусто или null — «все» (по умолчанию) или «как по умолчанию» (у получателя).</summary>
public sealed record CategoryCardsInput(IReadOnlyList<string>? Categories);

/// <summary>
/// «Настройки → Telegram → Автоматические отчёты → Карточки категорий»: какие категории уходят по умолчанию и свой выбор получателя. Сами
/// отправки — обычный конвейер автоотчётов (запуски category-day.* отчёта «11. Итоги дня по категориям», получатели, предпросмотр, тест, история).
/// </summary>
public sealed class CategoryCardService(IAppDbContext db, SalesOptions sales, IAuditLogger audit, TimeProvider clock)
{
    public async Task<CategoryCardsView> GetAsync(CancellationToken ct)
    {
        var settings = await db.TelegramReportSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var recipients = await db.TelegramReportSubscriptions.AsNoTracking().OrderBy(s => s.CreatedAt).Select(s => new { s.Id, s.Categories }).ToListAsync(ct);
        return new CategoryCardsView(
            Options(),
            Known(CategoryCardChoice.Parse(settings?.CategoryCards)),
            recipients.Select(r => new CategoryCardRecipientView(r.Id, r.Categories is null ? null : Known(CategoryCardChoice.Parse(r.Categories)))).ToList(),
            CategoryCardChoice.Max,
            settings?.UpdatedAt);
    }

    /// <summary>Выбор по умолчанию (пусто — все категории): уходит всем получателям без своего выбора.</summary>
    public async Task<CategoryCardsView> SaveDefaultsAsync(CategoryCardsInput input, Guid userId, CancellationToken ct)
    {
        var names = Validate(input.Categories);
        var settings = await db.TelegramReportSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new TelegramReportSettings();
            db.TelegramReportSettings.Add(settings);
        }

        settings.CategoryCards = CategoryCardChoice.Format(names);
        settings.UpdatedAt = clock.GetUtcNow();
        settings.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.category_cards", nameof(TelegramReportSettings), "1", new { Categories = names }, ct);
        return await GetAsync(ct);
    }

    /// <summary>Свой выбор получателя; пусто — снова по умолчанию.</summary>
    public async Task<CategoryCardsView> SaveRecipientAsync(Guid recipientId, CategoryCardsInput input, Guid userId, CancellationToken ct)
    {
        var names = Validate(input.Categories);
        var row = await db.TelegramReportSubscriptions.FirstOrDefaultAsync(s => s.Id == recipientId, ct) ?? throw new KeyNotFoundException("Получатель не найден.");
        row.Categories = CategoryCardChoice.Format(names);
        row.UpdatedById = userId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.reports.recipient_categories", nameof(TelegramReportSubscription), recipientId.ToString(),
            new { Categories = names }, ct);
        return await GetAsync(ct);
    }

    private List<CategoryCardOption> Options() => sales.Categories.Keys.Select(n => new CategoryCardOption(n, CategoryCardChoice.AliasOf(n))).ToList();

    /// <summary>Названия из настройки в её написании (регистр не важен); категории, которых в настройке больше нет, — отбрасываются.</summary>
    private List<string> Known(IEnumerable<string> names) =>
        names.Select(n => sales.Categories.Keys.FirstOrDefault(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase))).OfType<string>().Distinct().ToList();

    private List<string> Validate(IReadOnlyList<string>? input)
    {
        var names = (input ?? []).Select(n => n?.Trim() ?? "").Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var unknown = names.Where(n => !sales.Categories.Keys.Any(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase))).ToList();
        if (unknown.Count > 0)
        {
            throw new ReportValidationException($"Таких категорий нет в «Настройках продаж»: {string.Join(", ", unknown)}.");
        }

        if (names.Count > CategoryCardChoice.Max)
        {
            throw new ReportValidationException($"В одной отправке — не больше {CategoryCardChoice.Max} категорий (альбом Telegram).");
        }

        return Known(names);
    }
}
