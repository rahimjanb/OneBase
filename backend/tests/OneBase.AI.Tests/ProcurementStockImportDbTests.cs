using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Procurement;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Загрузка движений склада из Excel (журнал склада сырья, как его ведут в таблице): заголовки на узбекском, количество текстом «1 000,00»,
/// одна позиция дважды за отправку, «Утилизатсия» и «Испарения» — списание, цех — новый отдел. Первая загрузка — начальные остатки
/// (меньше нуля — нулём), повторная — ничего нового, дописанные строки — приход, выдача и списание по датам строк; строка, на которую
/// не хватило остатка, загружается позже, когда хватит. Своя база: «первая загрузка» — на весь склад.
/// </summary>
public class ProcurementStockImportDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];

    private static readonly object[] Header = ["", "Дата заполнения", "Дата", "П/Р", "Партия", "Артикул/Хомашё", "Артикул", "Хомашё номи", "Ед. изм.", "Количество", "Цех ва склад номлари"];

    private async Task<User> SeedAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        if (!await db.Departments.AnyAsync(d => d.Code == "sales"))
        {
            db.Departments.Add(new Department { Code = "sales", Name = "Продажи" });
        }

        var keeper = new User { Login = $"keeper-{Guid.NewGuid():N}"[..20], FullName = "Кладовщик" };
        db.Users.Add(keeper);
        await db.SaveChangesAsync();
        return keeper;
    }

    private async Task<ProcurementStockImportResult> ImportAsync(User keeper, object[][] rows, bool dryRun)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Sheet1");
        object[][] all = [Header, .. rows];
        for (var r = 0; r < all.Length; r++)
        {
            for (var c = 0; c < all[r].Length; c++)
            {
                sheet.Cell(r + 1, c + 1).Value = all[r][c] is int n ? n : (string)all[r][c];
            }
        }

        // Как в настоящем журнале: формула протянута вниз далеко за последнюю строку.
        for (var r = all.Length + 1; r <= all.Length + 30; r++)
        {
            sheet.Cell(r, 13).Value = 0;
        }

        var file = new MemoryStream();
        book.SaveAs(file);
        using var scope = fixture.Services.CreateScope();
        var ctx = await scope.ServiceProvider.GetRequiredService<ProcurementRequestService>().ContextAsync(keeper.Id, Keeper.ToHashSet(), CancellationToken.None);
        return await scope.ServiceProvider.GetRequiredService<ProcurementStockImportService>()
            .ImportAsync(ctx, new MemoryStream(file.ToArray()), "Приходы расходы.xlsx", default, dryRun);
    }

    private async Task<decimal?> OnHandAsync(string sku)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        return await db.ProcurementStockBalances.Where(b => b.Item!.Sku == sku).Select(b => (decimal?)b.OnHand).FirstOrDefaultAsync();
    }

    [PostgresFact]
    public async Task Stock_import_opens_balances_then_posts_only_new_rows_as_receipts_issues_and_write_offs()
    {
        var keeper = await SeedAsync();
        var s = Guid.NewGuid().ToString("N")[..5];
        var (flour, oil, box) = ($"T{s}-001", $"T{s}-002", $"T{s}-003");
        var shop = $"Цех Тест{s}";
        object[][] history =
        [
            ["", "", "31.07.2025", "Приход", "31.07.2025", flour, flour, $"Мука {s}", "кг", "1 000,00", "Устав"],
            ["", "", "31.07.2025", "Приход", "31.07.2025", oil, oil, $"Масло {s}", "лт", 200, "Устав"],
            ["", "04.08.2025 9:49:27", "04.08.2025", "Расход", "31.07.2025", flour, flour, $"Мука {s}", "кг", 300, shop],
            ["", "04.08.2025 9:49:27", "04.08.2025", "Расход", "31.07.2025", flour, flour, $"Мука {s}", "кг", 50, shop],
            ["", "05.08.2025 10:00:00", "05.08.2025", "Расход", "31.07.2025", oil, oil, $"Масло {s}", "лт", "20,5", "Утилизатсия"],
            ["", "06.08.2025 11:00:00", "06.08.2025", "Расход", "31.07.2025", oil, oil, $"Масло {s}", "лт", 500, shop],
            ["", "06.08.2099 11:00:00", "06.08.2099", "Расход", "31.07.2025", flour, flour, $"Мука {s}", "кг", 1, shop],
            ["", "06.08.2025 12:00:00", "06.08.2025", "Приход", "06.08.2025", box, box, $"Коробка {s}", "шт", 10, "• Отдел снабжения •"],
            ["", "06.08.2025 12:00:00", "06.08.2025", "Приход", "06.08.2025", box, box, $"Коробка {s}", "кг", 3, "• Отдел снабжения •"],
        ];

        // 1. Проверка первой загрузки: начальные остатки, ничего не сохранено.
        var check = await ImportAsync(keeper, history, dryRun: true);
        Assert.Equal(("opening", true), (check.Mode, check.DryRun));
        Assert.Equal((9, 3, 1), (check.Rows, check.NewItems, check.Clamped));
        Assert.Equal(new[] { shop }, check.NewDepartments);
        Assert.Contains("Приход / расход ← «П/Р»", check.Columns);
        Assert.Contains("Цех / склад ← «Цех ва склад номлари»", check.Columns);
        Assert.Contains(check.Errors, e => e.Contains("ещё не наступила"));
        Assert.Contains(check.Errors, e => e.Contains("не совпадает с карточкой"));
        Assert.Null(await OnHandAsync(flour));

        // 2. Начальные остатки: мука 1000 − 350, масло ушло бы в минус — ноль, коробки 10; отдел-цех заведён.
        var opening = await ImportAsync(keeper, history, dryRun: false);
        Assert.Equal((7, 2), (opening.Posted, opening.Adjustments));
        Assert.Equal((650m, 0m, 10m), (await OnHandAsync(flour), await OnHandAsync(oil), await OnHandAsync(box)));
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.True(await db.Departments.AnyAsync(d => d.Name == shop));
            Assert.Equal(ProcurementStockDocumentKind.Inventory, (await db.ProcurementStockMovements.Where(m => m.Item!.Sku == flour).Select(m => m.Document!.Kind).SingleAsync()));
        }

        // 3. Тот же файл ещё раз — ничего нового.
        var again = await ImportAsync(keeper, history, dryRun: false);
        Assert.Equal(("movements", 7, 0), (again.Mode, again.Known, again.Posted));

        // 4. Дописанные строки: выдача в цех, приход, списание («Испарения»), выдача продажам; на коробки не хватает — строка пропущена.
        object[][] more =
        [
            .. history,
            ["", "07.08.2025 8:00:00", "07.08.2025", "Расход", "31.07.2025", flour, flour, $"Мука {s}", "кг", 100, shop],
            ["", "07.08.2025 9:00:00", "07.08.2025", "Приход", "07.08.2025", oil, oil, $"Масло {s}", "лт", 30, "• Отдел снабжения •"],
            ["", "07.08.2025 10:00:00", "07.08.2025", "Расход", "06.08.2025", box, box, $"Коробка {s}", "шт", 4, "Испарения"],
            ["", "07.08.2025 11:00:00", "07.08.2025", "Расход", "31.07.2025", flour, flour, $"Мука {s}", "кг", 10, "Савдо булими"],
            ["", "07.08.2025 12:00:00", "07.08.2025", "Расход", "06.08.2025", box, box, $"Коробка {s}", "шт", 50, shop],
        ];
        var preview = await ImportAsync(keeper, more, dryRun: true);
        Assert.Equal(("movements", 7, 6), (preview.Mode, preview.Known, preview.New));
        Assert.False(preview.UnknownFile);
        Assert.Equal((1, 2, 1), (preview.Receipts, preview.Issues, preview.WriteOffs));
        Assert.Contains(preview.Errors, e => e.Contains("загрузится в следующий раз"));
        Assert.Equal(650m, await OnHandAsync(flour));

        var posted = await ImportAsync(keeper, more, dryRun: false);
        Assert.Equal(4, posted.Posted);
        Assert.Equal((540m, 30m, 6m), (await OnHandAsync(flour), await OnHandAsync(oil), await OnHandAsync(box)));
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var issues = await db.ProcurementStockMovements.Where(m => m.Item!.Sku == flour && m.Kind == ProcurementMovementKind.Issue)
                .OrderBy(m => m.CreatedAt).Select(m => new { m.Quantity, m.CreatedAt, m.Document!.Receiver, Department = m.Document.Department!.Name }).ToListAsync();
            Assert.Equal(new[] { (-100m, shop, shop), (-10m, "Продажи", "Савдо булими") }, issues.Select(i => (i.Quantity, i.Department, i.Receiver!)));
            Assert.Equal(new DateOnly(2025, 8, 7), DateOnly.FromDateTime(issues[0].CreatedAt.ToOffset(TimeSpan.FromHours(5)).DateTime));
            Assert.Equal("Испарения", await db.ProcurementStockDocuments.Where(d => d.Kind == ProcurementStockDocumentKind.WriteOff && d.Comment == "Испарения").Select(d => d.Comment).SingleAsync());
        }

        // 5. Задним числом внесли приход коробок раньше выдачи — теперь пропущенная выдача проходит.
        object[][] late =
        [
            .. more,
            ["", "07.08.2025 11:30:00", "07.08.2025", "Приход", "07.08.2025", box, box, $"Коробка {s}", "шт", 100, "• Отдел снабжения •"],
        ];
        var retry = await ImportAsync(keeper, late, dryRun: false);
        Assert.Equal(2, retry.Posted);
        Assert.Equal(56m, await OnHandAsync(box));
    }

}
