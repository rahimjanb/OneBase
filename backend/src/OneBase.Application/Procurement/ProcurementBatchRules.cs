using OneBase.Application.Field;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>
/// Партия прихода из формы: номер партии производителя, дата производства, срок годности (или по сроку хранения позиции), количество.
/// ExpiryUnknown — «срок не указан»: только при вводе партий на имеющийся остаток, партия заблокирована до решения по качеству.
/// </summary>
public sealed record ProcurementBatchInput(string? SupplierBatchNo, DateOnly? ManufacturedOn, DateOnly? ExpiresOn, decimal Quantity, bool ExpiryUnknown = false);

/// <summary>Партия для отбора: остаток на складе, срок, когда пришла, номер, статус качества — по ним FEFO и годность.</summary>
public sealed record ProcurementBatchStock(Guid BatchId, string Number, DateOnly? ExpiresOn, DateOnly ReceivedOn, decimal OnHand,
    ProcurementBatchQuality Quality = ProcurementBatchQuality.Approved);

/// <summary>
/// Правила партий (чистые функции — проверяются тестами): годность (допущена по качеству, срок известен и не прошёл по Ташкенту), срок по
/// дате производства, проверка партии прихода, отбор FEFO — сначала партия с ближайшим сроком, при равном сроке — пришедшая раньше.
/// </summary>
public static class ProcurementBatchRules
{
    public const string Prefix = "LOT";

    /// <summary>Напоминание о сроке по умолчанию — за 30 дней.</summary>
    public const int DefaultWarningDays = 30;

    public const int MaxShelfLifeDays = 3650;

    public const int MaxBatchesPerLine = 50;

    /// <summary>Срок не прошёл: срок годности — сегодня или позже. Просрочена со следующего дня после срока.</summary>
    public static bool Usable(DateOnly expiresOn, DateOnly today) => expiresOn >= today;

    /// <summary>Можно выдать: допущена по качеству, срок известен и не прошёл.</summary>
    public static bool Usable(ProcurementBatchQuality quality, DateOnly? expiresOn, DateOnly today) =>
        quality == ProcurementBatchQuality.Approved && expiresOn is { } expires && Usable(expires, today);

    /// <summary>Почему не выдать: control — на контроле качества, blocked — забракована или заблокирована, expired — просрочена; годна — null.</summary>
    public static string? Unusable(ProcurementBatchQuality quality, DateOnly? expiresOn, DateOnly today) => quality switch
    {
        ProcurementBatchQuality.Pending => "control",
        ProcurementBatchQuality.Rejected or ProcurementBatchQuality.Blocked => "blocked",
        _ => expiresOn is { } expires && Usable(expires, today) ? null : "expired",
    };

    /// <summary>Состояние срока: expired — просрочена, expiring — истекает в ближайшие warningDays дней, ok, unknown — срок не указан.</summary>
    public static string ExpiryState(DateOnly? expiresOn, DateOnly today, int? warningDays) => expiresOn is not { } expires
        ? "unknown"
        : !Usable(expires, today) ? "expired" : expires.DayNumber - today.DayNumber <= (warningDays ?? DefaultWarningDays) ? "expiring" : "ok";

    /// <summary>Срок годности партии: указанный или дата производства + срок хранения позиции; не определить — null.</summary>
    public static DateOnly? ExpiresOn(DateOnly? expiresOn, DateOnly? manufacturedOn, int? shelfLifeDays) =>
        expiresOn ?? (manufacturedOn is { } made && shelfLifeDays is > 0 ? made.AddDays(shelfLifeDays.Value) : null);

    /// <summary>
    /// Проверка партии прихода: количество больше нуля (до трёх знаков), срок известен, производство не позже срока и не в будущем, срок не
    /// истёк (просроченное не принимается; allowExpired — ввод партий имеющегося остатка: он мог и просрочиться). allowUnknown — ввод партий:
    /// срок можно не указать («срок не указан»). Ошибка — текстом, иначе null.
    /// </summary>
    public static string? Check(string label, ProcurementBatchInput batch, DateOnly? expiresOn, DateOnly today, bool allowExpired = false, bool allowUnknown = false)
    {
        var no = string.IsNullOrWhiteSpace(batch.SupplierBatchNo) ? "" : $" (партия {batch.SupplierBatchNo.Trim()})";
        if (batch.Quantity <= 0 || batch.Quantity > ProcurementRules.MaxQuantity || decimal.Round(batch.Quantity, 3) != batch.Quantity)
        {
            return $"«{label}»{no}: количество партии — больше нуля, не больше трёх знаков после запятой.";
        }

        if (batch.SupplierBatchNo is { Length: > 64 })
        {
            return $"«{label}»: номер партии — до 64 символов.";
        }

        if (batch.ManufacturedOn is { } future && future > today)
        {
            return $"«{label}»{no}: дата производства {future:dd.MM.yyyy} — позже сегодняшней.";
        }

        if (batch.ExpiryUnknown)
        {
            return allowUnknown
                ? batch.ExpiresOn is null ? null : $"«{label}»{no}: срок годности указан — снимите «срок не указан»."
                : $"«{label}»{no}: «срок не указан» — только при вводе партий на имеющийся остаток.";
        }

        if (expiresOn is not { } expires)
        {
            return $"«{label}»{no}: укажите срок годности — или дату производства, если у позиции задан срок хранения.";
        }

        if (batch.ManufacturedOn is { } made && made > expires)
        {
            return $"«{label}»{no}: дата производства {made:dd.MM.yyyy} — позже срока годности.";
        }

        return allowExpired || Usable(expires, today) ? null : $"«{label}»{no}: срок годности истёк {expires:dd.MM.yyyy} — просроченное на склад не принимается.";
    }

    /// <summary>
    /// Отбор FEFO: сколько взять из каких партий, чтобы набрать quantity; usableOnly — только годные на today (допущенные и не просроченные).
    /// Не хватает — null (столько нет). Порядок: срок (без срока — в конце), дата прихода, номер.
    /// </summary>
    public static List<(Guid BatchId, decimal Quantity)>? Pick(IEnumerable<ProcurementBatchStock> batches, decimal quantity, DateOnly today, bool usableOnly)
    {
        var picks = new List<(Guid, decimal)>();
        var left = quantity;
        foreach (var b in Fefo(batches).Where(b => b.OnHand > 0 && (!usableOnly || Usable(b.Quality, b.ExpiresOn, today))))
        {
            if (left <= 0)
            {
                break;
            }

            var take = Math.Min(left, b.OnHand);
            picks.Add((b.BatchId, take));
            left -= take;
        }

        return left > 0 ? null : picks;
    }

    public static IOrderedEnumerable<ProcurementBatchStock> Fefo(IEnumerable<ProcurementBatchStock> batches) =>
        batches.OrderBy(b => b.ExpiresOn ?? DateOnly.MaxValue).ThenBy(b => b.ReceivedOn).ThenBy(b => b.Number, StringComparer.Ordinal);

    /// <summary>Подпись партии: «LOT-2026-00012 (A-1021, до 01.04.2027)»; без срока — «срок не указан».</summary>
    public static string Label(string number, string? supplierBatchNo, DateOnly? expiresOn) =>
        $"{number} ({(string.IsNullOrWhiteSpace(supplierBatchNo) ? "" : $"{supplierBatchNo}, ")}{(expiresOn is { } e ? $"до {e:dd.MM.yyyy}" : "срок не указан")})";

    /// <summary>Сегодня по Ташкенту для часов приложения (в тестах — подставные).</summary>
    public static DateOnly Today(TimeProvider clock) => FieldClock.LocalDate(clock.GetUtcNow());
}
