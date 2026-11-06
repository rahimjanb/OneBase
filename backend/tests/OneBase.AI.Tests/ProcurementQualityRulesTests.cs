using OneBase.Application.Procurement;
using OneBase.Domain.Procurement;

namespace OneBase.AI.Tests;

/// <summary>Правила контроля качества и годности партии — чистые функции: норма показателя, шаги проверки, годность с учётом качества, FEFO без срока.</summary>
public class ProcurementQualityRulesTests
{
    private static readonly DateOnly Day = new(2026, 11, 5);

    [Fact]
    public void A_value_passes_when_it_is_within_the_norm_including_the_bounds()
    {
        Assert.True(ProcurementQualityRules.Passed(null, 14m, 14m));
        Assert.False(ProcurementQualityRules.Passed(null, 14m, 14.01m));
        Assert.True(ProcurementQualityRules.Passed(10m, 12m, 10m));
        Assert.False(ProcurementQualityRules.Passed(10m, 12m, 9.99m));
        Assert.True(ProcurementQualityRules.Passed(null, null, -5m));
    }

    [Fact]
    public void Check_goes_documents_then_lab_then_decision_and_bad_documents_go_straight_to_decision()
    {
        Assert.Equal(ProcurementQualityCheckStatus.Documents, ProcurementQualityRules.Next(ProcurementQualityControl.Lab, null, null));
        Assert.Equal(ProcurementQualityCheckStatus.Lab, ProcurementQualityRules.Next(ProcurementQualityControl.Lab, true, null));
        Assert.Equal(ProcurementQualityCheckStatus.Decision, ProcurementQualityRules.Next(ProcurementQualityControl.Lab, true, false));
        Assert.Equal(ProcurementQualityCheckStatus.Decision, ProcurementQualityRules.Next(ProcurementQualityControl.Lab, false, null));
        Assert.Equal(ProcurementQualityCheckStatus.Decision, ProcurementQualityRules.Next(ProcurementQualityControl.Documents, true, null));

        Assert.False(ProcurementQualityRules.LabAllowed(ProcurementQualityControl.Documents, ProcurementQualityCheckStatus.Decision, true));
        Assert.False(ProcurementQualityRules.LabAllowed(ProcurementQualityControl.Lab, ProcurementQualityCheckStatus.Decision, false));
        Assert.True(ProcurementQualityRules.LabAllowed(ProcurementQualityControl.Lab, ProcurementQualityCheckStatus.Lab, true));
        Assert.False(ProcurementQualityRules.LabAllowed(ProcurementQualityControl.Lab, ProcurementQualityCheckStatus.Decided, true));

        Assert.True(ProcurementQualityRules.CanApprove(ProcurementQualityCheckStatus.Decision));
        Assert.False(ProcurementQualityRules.CanApprove(ProcurementQualityCheckStatus.Lab));
        Assert.True(ProcurementQualityRules.Deviates(true, false));
        Assert.False(ProcurementQualityRules.Deviates(true, null));
    }

    [Fact]
    public void Only_an_approved_batch_with_a_known_future_expiry_is_usable()
    {
        Assert.True(ProcurementBatchRules.Usable(ProcurementBatchQuality.Approved, Day, Day));
        Assert.False(ProcurementBatchRules.Usable(ProcurementBatchQuality.Approved, Day.AddDays(-1), Day));
        Assert.False(ProcurementBatchRules.Usable(ProcurementBatchQuality.Approved, null, Day));
        Assert.False(ProcurementBatchRules.Usable(ProcurementBatchQuality.Pending, Day.AddDays(30), Day));
        Assert.Equal("control", ProcurementBatchRules.Unusable(ProcurementBatchQuality.Pending, Day.AddDays(30), Day));
        Assert.Equal("blocked", ProcurementBatchRules.Unusable(ProcurementBatchQuality.Rejected, Day.AddDays(30), Day));
        Assert.Equal("blocked", ProcurementBatchRules.Unusable(ProcurementBatchQuality.Blocked, null, Day));
        Assert.Equal("expired", ProcurementBatchRules.Unusable(ProcurementBatchQuality.Approved, Day.AddDays(-1), Day));
        Assert.Null(ProcurementBatchRules.Unusable(ProcurementBatchQuality.Approved, Day, Day));
        Assert.Equal("unknown", ProcurementBatchRules.ExpiryState(null, Day, 30));
        Assert.Equal("LOT-2026-00001 (A-1, срок не указан)", ProcurementBatchRules.Label("LOT-2026-00001", "A-1", null));
    }

    [Fact]
    public void Fefo_takes_only_usable_batches_and_puts_unknown_expiry_last()
    {
        var pending = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        var good = Guid.NewGuid();
        ProcurementBatchStock[] stock =
        [
            new(unknown, "LOT-3", null, Day.AddDays(-30), 10, ProcurementBatchQuality.Blocked),
            new(pending, "LOT-1", Day.AddDays(5), Day, 10, ProcurementBatchQuality.Pending),
            new(good, "LOT-2", Day.AddDays(40), Day, 10),
        ];
        Assert.Equal([(good, 10m)], ProcurementBatchRules.Pick(stock, 10, Day, usableOnly: true)!);
        Assert.Null(ProcurementBatchRules.Pick(stock, 11, Day, usableOnly: true));
        Assert.Equal([pending, good, unknown], ProcurementBatchRules.Fefo(stock).Select(b => b.BatchId));

        // «Срок не указан» — только при вводе партий.
        var entry = new ProcurementBatchInput("A-1", Day.AddDays(-10), null, 5, ExpiryUnknown: true);
        Assert.Null(ProcurementBatchRules.Check("Мука", entry, null, Day, allowExpired: true, allowUnknown: true));
        Assert.Contains("только при вводе партий", ProcurementBatchRules.Check("Мука", entry, null, Day)!);
    }
}
