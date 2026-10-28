using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Security;
using OneBase.Application.Field;
using OneBase.Application.Hr;
using OneBase.Application.Sales.Metrics;

namespace OneBase.AI.Tools.Data;

/// <summary>«Лидеры месяца» (блок на главной): работающие сотрудники без опозданий за месяц — по отработанным часам. Расчёт HrService.LeadersAsync.</summary>
internal sealed class GetHrLeadersTool(HrService hr, IUserPermissions permissions) : HrTool(hr, permissions)
{
    internal const int DefaultLimit = 10;

    public override string Name => "get_hr_leaders";
    public override string Title => "HR: лидеры месяца";

    public override string Description =>
        "«Лидеры месяца» — как блок на главной OneBase: работающие сотрудники, которые за месяц ни разу не опоздали, по отработанным часам " +
        "(больше — выше, при равенстве — больше дней с приходом): место, ФИО, подразделение, должность, отработано и по графику в часах, дни с приходом " +
        "и по графику; сколько всего работающих и сколько из них без опозданий. Период — с 1-го числа по сегодня, в первые 3 дня месяца — прошлый месяц. " +
        "Вопросы: «кто лучший по дисциплине», «кто ни разу не опоздал в этом месяце», «топ сотрудников по отработанным часам».";

    public override string UsageHint =>
        "«Лучшие по дисциплине», «кто не опаздывал», «лидеры месяца» — get_hr_leaders; опоздания и отсутствия конкретных людей — get_hr_deviations.";

    public override JsonElement InputSchema { get; } = Schema(
        ("limit", new { type = "integer", description = $"Сколько лидеров: по умолчанию {DefaultLimit}, до 50." }));

    protected override Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct) => Guard(async () =>
    {
        var limit = args.Int("limit", 1, 50) ?? DefaultLimit;
        var ctx = await ContextAsync(context, ct);
        var result = await Hr.LeadersAsync(ctx, limit, ct);

        return Data(new
        {
            From = WorkFieldText.Day(result.From),
            To = WorkFieldText.Day(result.To),
            WorkingEmployees = result.Employees,
            OnTimeEmployees = result.OnTime,
            OnTimeSharePct = Pct(SalesMath.Ratio(result.OnTime, result.Employees)),
            Leaders = result.Leaders.Select(l => new
            {
                l.Place,
                l.Employee,
                l.Division,
                l.Job,
                WorkedHours = Hours(l.WorkedMinutes),
                PlanHours = Hours(l.PlanMinutes),
                l.PresentDays,
                l.PlanDays,
            }),
            Rule = "В рейтинге — работающие сотрудники с хотя бы одним приходом и без единого опоздания за период; " + Rules,
            Sync = SyncOf(result.Sync),
        }, new DataSource("Обзор · Лидеры месяца", $"{WorkFieldText.Ru(result.From)} – {WorkFieldText.Ru(result.To)}", "/"));
    });
}
