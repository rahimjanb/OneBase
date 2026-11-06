using OneBase.Application.Procurement;
using OneBase.Domain.Procurement;

namespace OneBase.AI.Tests;

/// <summary>Маршрут сырья и единицы закупки — чистые функции: шаги маршрута, переходы, пересчёт единиц.</summary>
public class ProcurementRawRouteRulesTests
{
    [Fact]
    public void Raw_route_starts_at_the_supply_head_and_always_reaches_the_director()
    {
        Assert.Equal(ProcurementRequestStatus.SupplyApproval, ProcurementRules.FirstStep(false, true, true, raw: true));
        Assert.Equal(ProcurementRequestStatus.DirectorApproval, ProcurementRules.FirstStep(false, true, true, raw: true, initiatorSupply: true));
        Assert.Equal(ProcurementRequestStatus.HeadApproval, ProcurementRules.FirstStep(false, true, false, raw: true)); // закупки нет — прежний маршрут
        Assert.Equal(ProcurementRequestStatus.HeadApproval, ProcurementRules.FirstStep(false, true, true));
        Assert.Equal(ProcurementRequestStatus.DirectorApproval, ProcurementRules.NextStep(ProcurementRequestStatus.SupplyApproval, true, 1, 5_000_000));
        Assert.Equal(ProcurementRequestStatus.Approved, ProcurementRules.NextStep(ProcurementRequestStatus.DirectorApproval, true, 1, 5_000_000));
        Assert.Equal(ProcurementRequestStatus.SupplyApproval, ProcurementRules.ReapprovalStep(true));
        Assert.Equal(ProcurementRequestStatus.FinanceApproval, ProcurementRules.ReapprovalStep(false));

        // Шаг не пропустить: из «руководителя снабжения» — только решение согласующего; утвердить сразу нельзя без него.
        Assert.True(ProcurementRules.CanTransition(ProcurementRequestStatus.SupplyApproval, ProcurementRequestStatus.Approved, false, false, decides: true));
        Assert.False(ProcurementRules.CanTransition(ProcurementRequestStatus.SupplyApproval, ProcurementRequestStatus.Approved, true, true, decides: false));
        Assert.False(ProcurementRules.CanTransition(ProcurementRequestStatus.SupplyApproval, ProcurementRequestStatus.DirectorApproval, true, true, decides: true));
        Assert.True(ProcurementRules.CanTransition(ProcurementRequestStatus.SupplyApproval, ProcurementRequestStatus.Cancelled, true, false));
    }

    [Fact]
    public void Purchase_units_convert_to_the_card_unit_without_silent_rounding()
    {
        (string, decimal)[] units = [("упак", 25m), ("коробка", 0.333m)];
        Assert.Equal(1m, ProcurementUnitRules.Factor("кг", "кг", units));
        Assert.Equal(1m, ProcurementUnitRules.Factor("кг", null, units));
        Assert.Equal(25m, ProcurementUnitRules.Factor("кг", "упак", units));
        Assert.Null(ProcurementUnitRules.Factor("кг", "мешок", units));
        Assert.Equal(75m, ProcurementUnitRules.ToBase(3, 25));
        Assert.Null(ProcurementUnitRules.ToBase(0.5m, 0.333m));
        Assert.Equal(10_000m, ProcurementUnitRules.CostToBase(250_000, 25));
        Assert.Equal(1_001.00m, ProcurementUnitRules.CostToBase(333.333m, 0.333m));
        Assert.Null(ProcurementUnitRules.Check("кг", [new("упак", 25)], ["упак", "кг"]));
        Assert.NotNull(ProcurementUnitRules.Check("кг", [new("упак", 0)], ["упак", "кг"]));
        Assert.NotNull(ProcurementUnitRules.Check("кг", [new("кг", 2)], ["упак", "кг"]));
        Assert.NotNull(ProcurementUnitRules.Check("кг", [new("ящик", 2)], ["упак", "кг"]));
        Assert.NotNull(ProcurementUnitRules.Check("кг", [new("упак", 1.1234567m)], ["упак", "кг"]));
    }
}
