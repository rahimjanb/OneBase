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
    public void Raw_request_route_goes_by_author_and_skips_only_the_director_steps()
    {
        // По умолчанию: снабженец — аналитик → финансист; аналитик — финансист; финансист — аналитик; директора нет.
        var route = ProcurementRawRoute.Parse(null);
        Assert.Equal(["analyst", "finance"], route[ProcurementRawRoute.SupplyAuthor]);
        Assert.Equal(["finance"], route[ProcurementRawRoute.AnalystAuthor]);
        Assert.Equal(["analyst"], route[ProcurementRawRoute.FinanceAuthor]);
        Assert.Equal(route[ProcurementRawRoute.SupplyAuthor], ProcurementRawRoute.Parse("не json")[ProcurementRawRoute.SupplyAuthor]);
        Assert.Equal(["director"], ProcurementRawRoute.Parse(ProcurementRawRoute.Serialize(new Dictionary<string, IReadOnlyList<string>>(route)
            { [ProcurementRawRoute.FinanceAuthor] = ["director"] }))[ProcurementRawRoute.FinanceAuthor]);

        // Шаги: после аналитика — финансист, после финансиста — утверждена; «директор, если больше лимита» — только сверх лимита.
        string[] steps = ["analyst", "finance", "director_limit"];
        Assert.Equal(ProcurementRequestStatus.AnalystApproval, ProcurementRawRoute.Next(steps, null, 10, 100, false));
        Assert.Equal(ProcurementRequestStatus.FinanceApproval, ProcurementRawRoute.Next(steps, ProcurementRequestStatus.AnalystApproval, 10, 100, false));
        Assert.Equal(ProcurementRequestStatus.Approved, ProcurementRawRoute.Next(steps, ProcurementRequestStatus.FinanceApproval, 100, 100, false));
        Assert.Equal(ProcurementRequestStatus.DirectorApproval, ProcurementRawRoute.Next(steps, ProcurementRequestStatus.FinanceApproval, 101, 100, false));
        Assert.Equal(ProcurementRequestStatus.Approved, ProcurementRawRoute.Next(steps, ProcurementRequestStatus.FinanceApproval, 101, 100, initiatorDirects: true));
        Assert.Equal(ProcurementRequestStatus.Approved, ProcurementRawRoute.Next(["director_limit"], null, 1, 100, false)); // нечего согласовывать — утверждена
        Assert.Equal(ProcurementRequestStatus.DirectorApproval, ProcurementRawRoute.Next(["finance", "director"], ProcurementRequestStatus.FinanceApproval, 1, 100, false));

        // Автор: финансист, затем аналитик, затем снабженец; остальным заявку на сырьё не создать.
        ProcurementContext Ctx(bool finance = false, bool analyst = false, bool manages = false) =>
            new(Guid.NewGuid(), manages ? new HashSet<string> { OneBase.Application.Security.Permissions.ProcurementManage } : new HashSet<string>(), null,
                new HashSet<Guid>(), IsFinancier: finance, IsAnalyst: analyst);
        Assert.Equal("finance", ProcurementRawRoute.Author(Ctx(finance: true, analyst: true, manages: true)));
        Assert.Equal("analyst", ProcurementRawRoute.Author(Ctx(analyst: true, manages: true)));
        Assert.Equal("supply", ProcurementRawRoute.Author(Ctx(manages: true)));
        Assert.Null(ProcurementRawRoute.Author(Ctx()));
        Assert.False(Ctx().CanCreateRaw);

        // Аналитик решает шаг аналитика — но не по своей заявке.
        var analystCtx = Ctx(analyst: true);
        var request = new ProcurementRequest { Number = "PR-1", Title = "Мука", Status = ProcurementRequestStatus.AnalystApproval, InitiatorId = Guid.NewGuid() };
        Assert.True(ProcurementRules.Decides(analystCtx, request));
        var own = new ProcurementRequest { Number = "PR-2", Title = "Мука", Status = ProcurementRequestStatus.AnalystApproval, InitiatorId = analystCtx.UserId };
        Assert.False(ProcurementRules.Decides(analystCtx, own));
        Assert.True(ProcurementRules.CanTransition(ProcurementRequestStatus.AnalystApproval, ProcurementRequestStatus.Approved, false, false, decides: true));
        Assert.True(ProcurementRules.CanTransition(ProcurementRequestStatus.AnalystApproval, ProcurementRequestStatus.Cancelled, true, false));
        Assert.Equal(ProcurementApprovalStages.Analyst, ProcurementRules.Stage(ProcurementRequestStatus.AnalystApproval));
    }

    [Theory]
    [InlineData("supply", new string[0], "хотя бы один шаг")]
    [InlineData("supply", new[] { "analyst", "boss" }, "неизвестный шаг")]
    [InlineData("supply", new[] { "analyst", "analyst" }, "каждый шаг — один раз")]
    [InlineData("supply", new[] { "director", "director_limit" }, "каждый шаг — один раз")]
    [InlineData("analyst", new[] { "analyst", "finance" }, "сам не согласует")]
    [InlineData("finance", new[] { "finance" }, "сам не согласует")]
    public void Raw_route_settings_reject_empty_unknown_repeated_and_own_steps(string author, string[] steps, string error)
    {
        var route = new Dictionary<string, IReadOnlyList<string>>(ProcurementRawRoute.Default) { [author] = steps };
        Assert.Contains(error, ProcurementRawRoute.Check(route));
        Assert.Null(ProcurementRawRoute.Check(ProcurementRawRoute.Default));
        Assert.Null(ProcurementRawRoute.Check(new Dictionary<string, IReadOnlyList<string>>(ProcurementRawRoute.Default)
            { [ProcurementRawRoute.SupplyAuthor] = ["analyst", "finance", "director_limit"] }));
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
