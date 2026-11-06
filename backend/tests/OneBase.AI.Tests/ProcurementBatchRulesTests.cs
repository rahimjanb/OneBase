using OneBase.Application.Procurement;

namespace OneBase.AI.Tests;

/// <summary>Правила партий без базы: годность по дате, срок по дате производства, проверка партии прихода, отбор FEFO, состояние срока.</summary>
public class ProcurementBatchRulesTests
{
    private static readonly DateOnly Day = new(2026, 11, 5);

    [Fact]
    public void A_batch_is_usable_through_its_expiry_day_and_expired_from_the_next()
    {
        Assert.True(ProcurementBatchRules.Usable(Day, Day));
        Assert.False(ProcurementBatchRules.Usable(Day, Day.AddDays(1)));
        Assert.Equal("expired", ProcurementBatchRules.ExpiryState(Day.AddDays(-1), Day, null));
        Assert.Equal("expiring", ProcurementBatchRules.ExpiryState(Day.AddDays(30), Day, null)); // 30 дней — по умолчанию
        Assert.Equal("ok", ProcurementBatchRules.ExpiryState(Day.AddDays(31), Day, null));
        Assert.Equal("expiring", ProcurementBatchRules.ExpiryState(Day.AddDays(60), Day, 90));
    }

    [Fact]
    public void Expiry_comes_from_the_batch_or_from_the_production_date_and_the_shelf_life()
    {
        Assert.Equal(Day.AddDays(10), ProcurementBatchRules.ExpiresOn(Day.AddDays(10), Day, 365)); // указанный срок важнее
        Assert.Equal(Day.AddDays(180), ProcurementBatchRules.ExpiresOn(null, Day, 180));
        Assert.Null(ProcurementBatchRules.ExpiresOn(null, Day, null));
        Assert.Null(ProcurementBatchRules.ExpiresOn(null, null, 180));
    }

    [Fact]
    public void A_received_batch_needs_a_quantity_a_known_expiry_and_sane_dates()
    {
        string? Check(decimal qty, DateOnly? expires, DateOnly? made = null, bool allowExpired = false) =>
            ProcurementBatchRules.Check("Мука", new ProcurementBatchInput("A-1", made, expires, qty), expires, Day, allowExpired);

        Assert.Null(Check(10, Day.AddDays(30), Day.AddDays(-5)));
        Assert.Contains("количество партии", Check(0, Day.AddDays(30)));
        Assert.Contains("количество партии", Check(1.0005m, Day.AddDays(30)));
        Assert.Contains("укажите срок годности", Check(10, null));
        Assert.Contains("позже сегодняшней", Check(10, Day.AddDays(30), Day.AddDays(1)));
        Assert.Contains("позже срока годности", Check(10, Day.AddDays(-2), Day.AddDays(-1)));
        Assert.Contains("просроченное на склад не принимается", Check(10, Day.AddDays(-1)));
        Assert.Null(Check(10, Day.AddDays(-1), allowExpired: true)); // ввод партий имеющегося остатка
    }

    [Fact]
    public void Fefo_takes_the_nearest_expiry_first_and_skips_expired_for_issue()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid(), old = Guid.NewGuid();
        ProcurementBatchStock[] stock =
        [
            new(b, "LOT-2", Day.AddDays(40), Day.AddDays(-3), 50),
            new(a, "LOT-1", Day.AddDays(5), Day.AddDays(-9), 20),
            new(c, "LOT-3", Day.AddDays(5), Day.AddDays(-1), 10), // тот же срок, пришла позже
            new(old, "LOT-0", Day.AddDays(-1), Day.AddDays(-30), 100),
        ];

        Assert.Equal([(a, 20m), (c, 10m), (b, 5m)], ProcurementBatchRules.Pick(stock, 35, Day, usableOnly: true));
        Assert.Null(ProcurementBatchRules.Pick(stock, 81, Day, usableOnly: true)); // годного 80
        Assert.Equal([(old, 100m), (a, 5m)], ProcurementBatchRules.Pick(stock, 105, Day, usableOnly: false)); // списание: просроченные первыми
        Assert.Equal("LOT-7 (A-1, до 05.11.2026)", ProcurementBatchRules.Label("LOT-7", "A-1", Day));
    }
}
