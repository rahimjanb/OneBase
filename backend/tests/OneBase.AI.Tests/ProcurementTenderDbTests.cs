using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Журнал тендерных закупок на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): запись с позициями и победителем
/// (новый поставщик — без дублей), повтор формы, поиск и итоги, просроченная поставка, правка с историей «было → стало» и конфликтом версий,
/// поставка, отмена с причиной (отменённую не правят, в сумму она не входит), проверки формы и права (ведёт снабжение, видит и директор).
/// </summary>
public class ProcurementTenderDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Employee = [Permissions.ProcurementRequest];
    private static readonly string[] Supply = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementManage];
    private static readonly string[] Director = [Permissions.ProcurementRequest, Permissions.ProcurementRead, Permissions.ProcurementSettings];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];

    private sealed record Org(User Employee, User Supply, User Director, User Keeper, Guid Raw, string Suffix);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var dept = new Department { Code = $"tn-{s}", Name = $"Производство {s}" };
        var org = new Org(new User { Login = $"tn-emp-{s}", FullName = "Технолог", Department = dept },
            new User { Login = $"tn-sup-{s}", FullName = "Закупщик сырья", Department = dept },
            new User { Login = $"tn-dir-{s}", FullName = "Директор", Department = dept },
            new User { Login = $"tn-kpr-{s}", FullName = "Кладовщик", Department = dept }, Guid.Empty, s);
        db.Users.AddRange(org.Employee, org.Supply, org.Director, org.Keeper);
        await db.SaveChangesAsync();
        return org with { Raw = await db.ProcurementCategories.Where(c => c.Code == "raw_materials").Select(c => c.Id).SingleAsync() };
    }

    private async Task<T> As<T>(User user, string[] permissions, Func<ProcurementTenderService, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var ctx = await scope.ServiceProvider.GetRequiredService<ProcurementRequestService>().ContextAsync(user.Id, permissions.ToHashSet(), CancellationToken.None);
        return await act(scope.ServiceProvider.GetRequiredService<ProcurementTenderService>(), ctx);
    }

    private Task As(User user, string[] permissions, Func<ProcurementTenderService, ProcurementContext, Task> act) => As(user, permissions, async (t, c) =>
    {
        await act(t, c);
        return true;
    });

    private static ProcurementTenderInput Sugar(Org org, string supplier, DateOnly? delivery = null, Guid? key = null) => new(
        $"Сахар-песок на I квартал {org.Suffix}", "LOT-17", FieldClock.Today.AddDays(-10), null, supplier, org.Raw, "Д-45/2026", 4, delivery ?? FieldClock.Today.AddDays(20),
        "Победил по цене", [new($"Сахар-песок {org.Suffix}", "т", 20, 9_800_000), new("Доставка до цеха", "шт", 1, 1_500_000)], key);

    [PostgresFact]
    public async Task Tender_records_are_kept_found_edited_delivered_and_cancelled()
    {
        var org = await SeedAsync();
        var shop = $"ООО Шакар {org.Suffix}";
        var key = Guid.NewGuid();

        // Ведёт снабжение; сотрудник и кладовщик записей не добавляют и не видят.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Keeper, Keeper, (t, c) => t.CreateAsync(c, Sugar(org, shop), default)));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Employee, Employee, (t, c) => t.SearchAsync(c, new(), default)));
        var id = await As(org.Supply, Supply, (t, c) => t.CreateAsync(c, Sugar(org, shop, key: key), default));
        Assert.Equal(id, await As(org.Supply, Supply, (t, c) => t.CreateAsync(c, Sugar(org, shop, key: key), default)));

        var detail = await As(org.Director, Director, (t, c) => t.GetAsync(c, id, default));
        Assert.StartsWith("TN-", detail.Number);
        Assert.Equal((ProcurementTenderStatus.Contracted, shop, "Сырьё", 4, "Закупщик сырья"), (detail.Status, detail.SupplierName, detail.CategoryName, detail.Participants, detail.ResponsibleName));
        Assert.Equal(197_500_000m, detail.Total);
        Assert.Equal([196_000_000m, 1_500_000m], detail.Items.Select(i => i.Sum));
        Assert.False(detail.Can.Edit);
        Assert.True((await As(org.Supply, Supply, (t, c) => t.GetAsync(c, id, default))).Can.Deliver);
        Assert.Contains(detail.History, h => h.Action == "created" && h.Text.Contains(shop));

        // Второй тендер у того же поставщика (название другим регистром) — поставщик не дублируется; срок поставки прошёл — просрочена.
        var late = await As(org.Supply, Supply, (t, c) => t.CreateAsync(c, Sugar(org, shop.ToUpperInvariant(), FieldClock.Today.AddDays(-1)) with
        {
            Title = $"Какао-порошок {org.Suffix}", Items = [new($"Какао-порошок {org.Suffix}", "кг", 500, 61_000)],
        }, default));
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.Equal(1, await db.ProcurementSuppliers.CountAsync(x => x.Name.ToLower() == shop.ToLower()));
        }

        var found = await As(org.Supply, Supply, (t, c) => t.SearchAsync(c, new($"какао-порошок {org.Suffix}"), default));
        Assert.Equal(late, found.Items.Single().Id);
        Assert.True(found.Items.Single().Overdue);
        var supplierId = detail.SupplierId;
        var all = await As(org.Supply, Supply, (t, c) => t.SearchAsync(c, new(SupplierId: supplierId), default));
        Assert.Equal(2, all.Total);
        Assert.Equal(197_500_000m + 30_500_000m, all.Amount);
        Assert.True(all.CanCreate);
        Assert.Contains((await As(org.Supply, Supply, (t, c) => t.SearchAsync(c, new(Status: "overdue", SupplierId: supplierId), default))).Items, r => r.Id == late);
        Assert.DoesNotContain((await As(org.Supply, Supply, (t, c) => t.SearchAsync(c, new(Status: "overdue", SupplierId: supplierId), default))).Items, r => r.Id == id);

        // Правка — в истории «было → стало»; правка устаревшей версии — конфликт.
        await As(org.Supply, Supply, (t, c) => t.UpdateAsync(c, id, Sugar(org, shop) with { ContractNo = "Д-46/2026", Participants = 5, Version = detail.Version }, default));
        detail = await As(org.Supply, Supply, (t, c) => t.GetAsync(c, id, default));
        Assert.Contains(detail.History, h => h.Action == "updated" && h.Text.Contains("договор: Д-45/2026 → Д-46/2026") && h.Text.Contains("участников: 4 → 5"));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Supply, Supply, (t, c) =>
            t.UpdateAsync(c, id, Sugar(org, shop) with { Version = detail.Version + 1 }, default)));

        // Поставлено: не позже сегодня; повтор — без ошибки.
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Supply, Supply, (t, c) => t.DeliveredAsync(c, id, FieldClock.Today.AddDays(1), default)));
        await As(org.Supply, Supply, (t, c) => t.DeliveredAsync(c, id, null, default));
        await As(org.Supply, Supply, (t, c) => t.DeliveredAsync(c, id, null, default));
        detail = await As(org.Supply, Supply, (t, c) => t.GetAsync(c, id, default));
        Assert.Equal((ProcurementTenderStatus.Delivered, FieldClock.Today), (detail.Status, detail.DeliveredOn!.Value));
        Assert.False(detail.Can.Deliver);

        // Отмена — с причиной; отменённую не правят, в сумму журнала она не входит.
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Supply, Supply, (t, c) => t.CancelAsync(c, late, " ", default)));
        await As(org.Supply, Supply, (t, c) => t.CancelAsync(c, late, "Поставщик отказался от договора", default));
        await Assert.ThrowsAsync<FieldConflictException>(() => As(org.Supply, Supply, (t, c) => t.UpdateAsync(c, late, Sugar(org, shop), default)));
        var cancelled = await As(org.Supply, Supply, (t, c) => t.GetAsync(c, late, default));
        Assert.Equal(ProcurementTenderStatus.Cancelled, cancelled.Status);
        Assert.Contains(cancelled.History, h => h.Action == "cancelled" && h.Comment == "Поставщик отказался от договора");
        Assert.False(cancelled.Can.Edit);
        Assert.Equal(197_500_000m, (await As(org.Supply, Supply, (t, c) => t.SearchAsync(c, new(SupplierId: supplierId), default))).Amount);
    }

    [PostgresFact]
    public async Task Tender_form_needs_a_past_date_a_winner_and_priced_items()
    {
        var org = await SeedAsync();
        var shop = $"Ун-Савдо {org.Suffix}";
        Task Bad(ProcurementTenderInput input) =>
            Assert.ThrowsAsync<FieldValidationException>(() => As(org.Supply, Supply, (t, c) => t.CreateAsync(c, input, default)));

        await Bad(Sugar(org, shop) with { TenderDate = FieldClock.Today.AddDays(1) });
        await Bad(Sugar(org, shop) with { DeliveryDate = FieldClock.Today.AddDays(-30) });
        await Bad(Sugar(org, " "));
        await Bad(Sugar(org, shop) with { Title = "" });
        await Bad(Sugar(org, shop) with { Items = [] });
        await Bad(Sugar(org, shop) with { Items = [new("Мука", "кг", 100, 0)] });
        await Bad(Sugar(org, shop) with { Items = [new("Мука", "мешок?", 100, 5_000)] });
        await Bad(Sugar(org, shop) with { Participants = 0 });
        await Bad(Sugar(org, shop) with { CategoryId = null });
    }
}
