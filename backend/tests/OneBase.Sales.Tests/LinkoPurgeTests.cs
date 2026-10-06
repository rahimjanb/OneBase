using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Linko;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Sales.Tests;

/// <summary>
/// «Очистить данные и загрузить заново» (LinkoSyncService.PurgeAsync): что удаляется, а что остаётся — по модели БД;
/// регионы, которые остаются, после загрузки сверяются с филиалами заказов (EnsureRegionsAsync → SyncRegions).
/// </summary>
public class LinkoPurgeTests
{
    [Fact]
    public void Reset_clears_the_linko_copy_and_keeps_onebase_structure_and_plans()
    {
        // Модель БД строится без подключения к серверу.
        using var db = new OneBaseDbContext(new DbContextOptionsBuilder<OneBaseDbContext>().UseNpgsql("Host=localhost").Options);
        static string Table(IReadOnlyEntityType e) => $"{e.GetSchema()}.\"{e.GetTableName()}\"";
        var entities = db.Model.GetEntityTypes().Where(e => e.GetTableName() is not null).ToList();
        var purged = LinkoSyncService.PurgedTables.ToHashSet();

        // Копия Linko очищается вся (и только существующие таблицы); из схемы sales — только планы ТП из API планов Linko.
        var mirror = entities.Where(e => e.GetSchema() == "linko").Select(Table).ToHashSet();
        Assert.Equal(mirror.Order(), purged.Where(t => t.StartsWith("linko.")).Order());
        Assert.Equal(["sales.\"StaffPlans\""], purged.Where(t => !t.StartsWith("linko.")));

        // Данные OneBase остаются: направления, регионы (РМ, СВР, дилер), планы РОП и «Завод», планы и профили агентов, цели.
        foreach (var owned in new[] { typeof(SalesDirection), typeof(SalesRegion), typeof(SalesRegionPlan), typeof(SalesAgentPlan), typeof(SalesAgentProfile), typeof(SalesTarget) })
        {
            Assert.DoesNotContain(Table(db.Model.FindEntityType(owned)!), purged);
        }

        // Планы регионов удаляются вместе с регионом (каскад) — поэтому регионы при очистке не удаляются.
        var regionPlan = Assert.Single(db.Model.FindEntityType(typeof(SalesRegionPlan))!.GetForeignKeys());
        Assert.Equal((typeof(SalesRegion), DeleteBehavior.Cascade), (regionPlan.PrincipalEntityType.ClrType, regionPlan.DeleteBehavior));

        // TRUNCATE без CASCADE: на очищаемые таблицы не ссылается ничего, что остаётся.
        Assert.DoesNotContain(
            entities.Where(e => !purged.Contains(Table(e))).SelectMany(e => e.GetForeignKeys()),
            fk => purged.Contains(Table(fk.PrincipalEntityType)));
    }

    [Fact]
    public void Kept_region_takes_the_branch_name_of_the_latest_order_and_keeps_onebase_data()
    {
        // Регионы при перезагрузке не удаляются, поэтому переименование филиала в Linko должно доходить до региона.
        var direction = Guid.NewGuid();
        var renamed = new SalesRegion { LinkoBranchId = 7, Name = "Олмалик", DirectionId = direction, SupervisorName = "Каримов", DealerName = "ООО Олмалик" };
        var same = new SalesRegion { LinkoBranchId = 8, Name = "Термез" };
        var noName = new SalesRegion { LinkoBranchId = 9, Name = "Карши" };
        var now = new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);

        var added = LinkoSyncService.SyncRegions(
            [renamed, same, noName],
            [
                new(7, "Олмалик (Ангрен)", "Олмалик"),
                new(8, "Термез", "Термез"),
                new(9, null, "Карши"), // в последнем заказе названия нет — название не трогаем
                new(10, null, "Денов"),
                new(11, " ", null),
            ],
            now);

        Assert.Equal(("Олмалик (Ангрен)", direction, "Каримов", "ООО Олмалик", now), (renamed.Name, renamed.DirectionId!.Value, renamed.SupervisorName, renamed.DealerName, renamed.UpdatedAt!.Value));
        Assert.True(same.Name == "Термез" && same.UpdatedAt is null);
        Assert.True(noName.Name == "Карши" && noName.UpdatedAt is null);
        Assert.Equal([(10L, "Денов"), (11L, "Филиал 11")], added.Select(r => (r.LinkoBranchId, r.Name)));
        Assert.All(added, r => Assert.Null(r.DirectionId)); // новый филиал — без направления, пока его не назначат
    }
}
