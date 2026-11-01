using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Reports;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace OneBase.AI.Tests;

/// <summary>Нагрузочная проверка — только по явному ONEBASE_PERF=1 (и ONEBASE_TEST_POSTGRES): наполнение временной базы занимает время.</summary>
public sealed class PerformanceFactAttribute : FactAttribute
{
    public PerformanceFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DataManagerDb.Variable)) || Environment.GetEnvironmentVariable("ONEBASE_PERF") != "1")
        {
            Skip = "Нагрузочная проверка снабжения — по ONEBASE_PERF=1 и ONEBASE_TEST_POSTGRES.";
        }
    }
}

/// <summary>
/// Производительность обзора и отчётов снабжения на больших объёмах — во временной базе теста (удаляется после): 300 поставщиков,
/// 20 000 заявок (60 000 позиций), 15 000 заказов (45 000 позиций) за год, 12 000 приёмок (36 000 строк, часть — с претензиями).
/// Время каждого запроса пишется в вывод теста; порог — 5 секунд.
/// </summary>
public class ProcurementPerformanceTests(DataManagerDb fixture, ITestOutputHelper output) : IClassFixture<DataManagerDb>
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(5);

    [PerformanceFact]
    public async Task Dashboard_and_reports_stay_fast_on_a_year_of_purchases()
    {
        Guid user;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
            var departments = Enumerable.Range(1, 6).Select(i => new Department { Code = $"pf-{i}", Name = $"Отдел нагрузки {i}" }).ToList();
            var staff = Enumerable.Range(1, 10).Select(i => new User { Login = $"pf-staff-{i}", FullName = $"Снабженец {i}", Department = departments[0] }).ToList();
            var initiator = new User { Login = "pf-init", FullName = "Инициатор нагрузки", Department = departments[1] };
            db.Departments.AddRange(departments);
            db.Users.AddRange(staff);
            db.Users.Add(initiator);
            await db.SaveChangesAsync();
            user = initiator.Id;
            var categories = await db.ProcurementCategories.Select(c => c.Id).ToListAsync();
            var warehouse = await db.ProcurementWarehouses.Select(w => w.Id).FirstAsync();
            string Arr(IEnumerable<Guid> ids) => $"array[{string.Join(",", ids.Select(id => $"'{id}'"))}]::uuid[]";
            var sw = Stopwatch.StartNew();
            db.Database.SetCommandTimeout(600);
            await db.Database.ExecuteSqlRawAsync($"""
                insert into procurement."Suppliers"("Id","Name","IsActive","CreatedAt","LeadTimeDays")
                select gen_random_uuid(), 'PF Поставщик ' || g, true, now(), 3 from generate_series(1, 300) g;
                create temp table pf_sup as select row_number() over (order by "Id") rn, "Id" id from procurement."Suppliers" where "Name" like 'PF %';

                create temp table pf_req as
                select g rn, gen_random_uuid() id, ({Arr(departments.Select(d => d.Id))})[1 + g % 6] dept,
                       now() - (g % 365) * interval '1 day' - interval '6 days' approved
                from generate_series(1, 20000) g;
                insert into procurement."Requests"("Id","Number","Title","DepartmentId","InitiatorId","Priority","Status","EstimatedTotal","PurchaseTotal","CreatedAt","SubmittedAt","ApprovedAt")
                select id, 'PF-R-' || lpad(rn::text, 6, '0'), 'Нагрузочная заявка ' || rn, dept, '{initiator.Id}', 'Normal',
                       case when rn % 10 = 0 then 'Fulfilled' else 'Approved' end, 1350000, 1350000, approved - interval '1 day', approved - interval '1 day', approved
                from pf_req;

                create temp table pf_item as select r.rn, k, gen_random_uuid() id, r.id request_id from pf_req r, generate_series(1, 3) k;
                insert into procurement."RequestItems"("Id","RequestId","LineNo","Name","CategoryId","UnitCode","Quantity","Status","CreatedAt","EstimatedPrice","OrderedQty","ReceivedQty")
                select i.id, i.request_id, i.k, 'Позиция ' || i.k, ({Arr(categories)})[1 + (i.rn + i.k) % {categories.Count}], 'шт', 10, 'Ordered', now(), 45000,
                       case when i.rn <= 15000 then 10 else 0 end, case when i.rn <= 15000 and i.rn % 10 not in (7, 8) then 10 else 0 end
                from pf_item i;

                create temp table pf_ord as select r.rn, gen_random_uuid() id, r.approved + interval '1 day' placed from pf_req r where r.rn <= 15000;
                insert into procurement."PurchaseOrders"("Id","Number","SupplierId","ResponsibleId","Status","Discount","VatRate","PricesIncludeVat","DeliveryCost","Subtotal",
                    "VatAmount","Total","ApprovalRound","CreatedAt","ApprovedAt","OrderedAt","DeliveredAt","ExpectedDate","OrderDate")
                select o.id, 'PF-O-' || lpad(o.rn::text, 6, '0'), s.id, ({Arr(staff.Select(u => u.Id))})[1 + o.rn % 10],
                       case when o.rn % 10 in (7, 8) then 'Ordered' when o.rn % 10 = 9 then 'Completed' else 'Delivered' end,
                       0, 12, true, 0, 1320000, 0, 1320000, 0, o.placed, o.placed, o.placed + interval '1 day',
                       case when o.rn % 10 in (7, 8) then null else o.placed + interval '4 days' + (o.rn % 3) * interval '1 day' end,
                       (o.placed + interval '5 days')::date, (o.placed + interval '1 day')::date
                from pf_ord o join pf_sup s on s.rn = 1 + o.rn % 300;
                insert into procurement."PurchaseOrderItems"("Id","OrderId","LineNo","RequestItemId","Name","UnitCode","Quantity","UnitPrice","ReceivedQty")
                select gen_random_uuid(), o.id, i.k, i.id, 'Позиция ' || i.k, 'шт', 10, 44000 + (o.rn % 5) * 1000, case when o.rn % 10 in (7, 8) then 0 else 10 end
                from pf_ord o join pf_item i on i.rn = o.rn;

                create temp table pf_doc as select o.rn, gen_random_uuid() id, o.id order_id, o.placed + interval '4 days' posted from pf_ord o where o.rn % 10 not in (7, 8);
                insert into procurement."StockDocuments"("Id","Number","Kind","WarehouseId","CreatedAt","Status","PostedAt","OrderId")
                select id, 'PF-RC-' || lpad(rn::text, 6, '0'), 'Receipt', '{warehouse}', posted, 'Posted', posted, order_id from pf_doc;
                insert into procurement."ReceiptLines"("Id","DocumentId","LineNo","OrderItemId","Received","Defective","Shortage","Accepted","ClaimStatus")
                select gen_random_uuid(), d.id, oi."LineNo", oi."Id", case when oi."LineNo" = 1 and d.rn % 20 = 0 then 11 else 10 end,
                       case when oi."LineNo" = 1 and d.rn % 20 = 0 then 1 else 0 end, 0, 10,
                       case when oi."LineNo" = 1 and d.rn % 20 = 0 then (case when d.rn % 40 = 0 then 'Resolved' else 'Open' end) else 'None' end
                from pf_doc d join procurement."PurchaseOrderItems" oi on oi."OrderId" = d.order_id;

                insert into procurement."AuditLog"("Id","DocType","DocId","Action","ToStatus","Text","CreatedAt")
                select gen_random_uuid(), 'request', id, 'issued', 'Fulfilled', 'Выдано (нагрузка)', approved + interval '6 days' from pf_req where rn % 10 = 0;
                analyze;
                """);
            output.WriteLine($"Наполнение: {sw.Elapsed.TotalSeconds:0.0} с");
        }

        var all = Permissions.All.ToHashSet();
        async Task<TimeSpan> Time(string name, Func<IServiceProvider, ProcurementContext, Task> act)
        {
            using var scope = fixture.Services.CreateScope();
            var ctx = await scope.ServiceProvider.GetRequiredService<ProcurementRequestService>().ContextAsync(user, all, CancellationToken.None);
            var sw = Stopwatch.StartNew();
            await act(scope.ServiceProvider, ctx);
            sw.Stop();
            output.WriteLine($"{name}: {sw.Elapsed.TotalMilliseconds:0} мс");
            return sw.Elapsed;
        }

        var today = FieldClock.Today;
        var month = ProcurementFilter.Of(today.AddDays(-30), today);
        var year = ProcurementFilter.Of(today.AddDays(-365), today);
        var times = new List<TimeSpan>
        {
            await Time("Обзор, 30 дней (прогрев)", (sp, c) => sp.GetRequiredService<ProcurementAnalyticsService>().DashboardAsync(c, month, default)),
            await Time("Обзор, 30 дней", (sp, c) => sp.GetRequiredService<ProcurementAnalyticsService>().DashboardAsync(c, month, default)),
            await Time("Обзор, год", (sp, c) => sp.GetRequiredService<ProcurementAnalyticsService>().DashboardAsync(c, year, default)),
        };
        foreach (var kind in ProcurementReportService.Kinds.Select(k => k.Key))
        {
            times.Add(await Time($"Отчёт «{kind}», год", (sp, c) => sp.GetRequiredService<ProcurementReportService>().BuildAsync(c, kind, year, default)));
        }

        times.Add(await Time("Excel «lines», год (10 000 строк)", async (sp, c) =>
        {
            var report = await sp.GetRequiredService<ProcurementReportService>().BuildAsync(c, "lines", year, default);
            sp.GetRequiredService<IReportWorkbookWriter>().Write(report, DateTimeOffset.UtcNow);
        }));
        times.Add(await Time("PDF «orders», год", async (sp, c) =>
        {
            var report = await sp.GetRequiredService<ProcurementReportService>().BuildAsync(c, "orders", year, default);
            sp.GetRequiredService<IReportPdfWriter>().Write(report, DateTimeOffset.UtcNow);
        }));
        Assert.All(times.Skip(1), t => Assert.True(t < Limit, $"{t.TotalSeconds:0.0} с — дольше {Limit.TotalSeconds} с"));
    }
}
