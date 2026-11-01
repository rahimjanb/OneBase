using OneBase.AI.Agents;
using OneBase.AI.Knowledge;
using OneBase.AI.Orchestration;

namespace OneBase.AI.Tests;

/// <summary>
/// Маршрут вопросов снабжения без модели: «заказ поставщику», «закупочная цена», «заявка на закупку», «просрочка поставки», «коммерческое
/// предложение» ведут к Supply AI; «заказ» со словами снабжения — не область заказов продаж, поэтому снабженцу без права на продажи вопрос не
/// закрывается как «нет доступа к заказам и ценам», а вопрос о заказах торговых точек остаётся областью продаж.
/// </summary>
public class ProcurementRoutingTests
{
    private static readonly AgentConfig[] All = AgentDefaults.Departments.OrderBy(d => d.SortOrder)
        .Select(d => new AgentConfig(d.Code, d.Name, d.Role, d.Description, d.Prompt, true, null, null, null, true, d.SortOrder, d.DepartmentCode, d.RequiredPermission, d.Sources))
        .ToArray();

    /// <summary>У каждого сотрудника для пользователя открыт только источник снабжения (снабженец или кладовщик без права на продажи).</summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> OnlyProcurement =
        All.ToDictionary(a => a.Code, IReadOnlySet<string> (_) => new HashSet<string> { KnowledgeSources.Procurement });

    [Theory]
    [InlineData("Какие заказы поставщикам просрочены?")]
    [InlineData("Сравни закупочные цены на сырьё за последние три месяца")]
    [InlineData("Какие заявки на закупку ждут решения директора?")]
    [InlineData("Где мы переплачиваем?")]
    [InlineData("Что пришло на приёмку вчера и есть ли претензии?")]
    [InlineData("Сравни коммерческие предложения по заявке PR-2026-00012")]
    [InlineData("Какая просрочка поставки у ООО Альфа?")]
    [InlineData("Xom ashyo narxi qancha oshdi?")]
    [InlineData("Таъминот бўйича кечиккан буюртмалар")]
    public void Procurement_questions_go_to_supply_ai(string question) =>
        Assert.Contains("supply", RouteIntent.Agents(question, All));

    [Fact]
    public void Supplier_orders_are_not_the_sales_orders_area()
    {
        var (agents, denied) = RouteIntent.Resolve("Какие заказы поставщикам просрочены и по каким ценам?", All, null, OnlyProcurement);

        Assert.Equal(["supply"], agents);
        Assert.Empty(denied);
    }

    [Fact]
    public void Procurement_stock_is_open_to_a_keeper_without_sales_rights()
    {
        var (agents, denied) = RouteIntent.Resolve("Какие остатки на складе ниже минимума?", All, null, OnlyProcurement);

        Assert.Equal(["supply"], agents);
        Assert.Empty(denied);
    }

    [Fact]
    public void Orders_of_shops_stay_a_sales_question()
    {
        var (_, denied) = RouteIntent.Resolve("Сколько заказов сделали торговые точки сегодня?", All, null, OnlyProcurement);

        Assert.Contains(denied, d => d.Title == "Заказы, цены и акции");
    }
}
