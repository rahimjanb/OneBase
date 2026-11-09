using System.IO.Compression;
using System.Security;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Production;
using OneBase.Application.Production.Import;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Domain.Procurement;
using OneBase.Domain.Production;
using OneBase.Infrastructure.Persistence;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Синтетическая книга .xlsx для тестов: общие и встроенные строки, числа исходной записью, формулы с сохранённым значением, ошибки, скрытые листы.</summary>
internal sealed class TestXlsx
{
    private readonly List<Sheet> _sheets = [];
    private readonly List<string> _strings = [];

    public Sheet Add(string name, bool hidden = false)
    {
        var sheet = new Sheet(this, name, hidden);
        _sheets.Add(sheet);
        return sheet;
    }

    private int Shared(string text)
    {
        var i = _strings.IndexOf(text);
        if (i >= 0)
        {
            return i;
        }

        _strings.Add(text);
        return _strings.Count - 1;
    }

    internal sealed class Sheet(TestXlsx book, string name, bool hidden)
    {
        public string Name { get; } = name;
        public bool Hidden { get; } = hidden;
        public SortedDictionary<(int Row, int Col), string> Cells { get; } = new();

        private Sheet Put(string reference, string xml)
        {
            var (row, col) = XlsxBook.Address(reference)!.Value;
            Cells[(row, col)] = xml;
            return this;
        }

        public Sheet Str(string reference, string text) => Put(reference, $"<c r=\"{reference}\" t=\"s\"><v>{book.Shared(text)}</v></c>");
        public Sheet Inline(string reference, string text) => Put(reference, $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(text)}</t></is></c>");
        public Sheet Num(string reference, string raw) => Put(reference, $"<c r=\"{reference}\"><v>{raw}</v></c>");

        public Sheet Formula(string reference, string formula, string cached) =>
            Put(reference, $"<c r=\"{reference}\"><f>{SecurityElement.Escape(formula)}</f><v>{cached}</v></c>");

        public Sheet SharedFormula(string reference, string cached) => Put(reference, $"<c r=\"{reference}\"><f t=\"shared\" si=\"0\"/><v>{cached}</v></c>");

        public Sheet Bool(string reference, bool value) => Put(reference, $"<c r=\"{reference}\" t=\"b\"><v>{(value ? 1 : 0)}</v></c>");

        public Sheet Error(string reference, string error, string? formula = null) =>
            Put(reference, $"<c r=\"{reference}\" t=\"e\">{(formula is null ? "" : $"<f>{SecurityElement.Escape(formula)}</f>")}<v>{error}</v></c>");

        /// <summary>Значение: null — пусто, «=…» — формула (кэш после «|»), «#…» — ошибка, число — исходной записью, иначе — текст.</summary>
        public Sheet Value(string reference, string? value) => value switch
        {
            null => this,
            ['=', ..] when value.Split('|') is [var f, var c] => Formula(reference, f[1..], c),
            ['#', ..] => Error(reference, value),
            _ when double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _) => Num(reference, value),
            _ => Str(reference, value),
        };
    }

    public byte[] Build()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Write(string path, string xml)
            {
                using var w = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                w.Write(xml);
            }

            const string main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            const string rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            var sheets = string.Join("", _sheets.Select((s, i) =>
                $"<sheet name=\"{SecurityElement.Escape(s.Name)}\" sheetId=\"{i + 1}\"{(s.Hidden ? " state=\"hidden\"" : "")} r:id=\"rId{i + 1}\"/>"));
            Write("xl/workbook.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"{main}\" xmlns:r=\"{rel}\"><sheets>{sheets}</sheets></workbook>");
            Write("xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                string.Join("", _sheets.Select((_, i) => $"<Relationship Id=\"rId{i + 1}\" Type=\"{rel}/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>")) +
                "</Relationships>");
            // Общие строки: обычные, «богатый» текст из фрагментов и фонетическая подсказка (она в значение не входит).
            var si = string.Join("", _strings.Select(t => t.StartsWith("RICH:", StringComparison.Ordinal)
                ? $"<si><r><t>{SecurityElement.Escape(t[5..t.IndexOf('|')])}</t></r><r><t>{SecurityElement.Escape(t[(t.IndexOf('|') + 1)..])}</t></r><rPh sb=\"0\" eb=\"1\"><t>ふりがな</t></rPh></si>"
                : $"<si><t xml:space=\"preserve\">{SecurityElement.Escape(t)}</t></si>"));
            Write("xl/sharedStrings.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><sst xmlns=\"{main}\">{si}</sst>");
            foreach (var (s, i) in _sheets.Select((s, i) => (s, i)))
            {
                var rows = string.Join("", s.Cells.GroupBy(c => c.Key.Row).Select(g => $"<row r=\"{g.Key}\">{string.Join("", g.Select(c => c.Value))}</row>"));
                Write($"xl/worksheets/sheet{i + 1}.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"{main}\"><sheetData>{rows}</sheetData></worksheet>");
            }
        }

        return ms.ToArray();
    }

    /// <summary>Строка сырья листа «norma …»: артикул, название, единица, «Вес», значения по столбцам продуктов (null — пусто).</summary>
    internal sealed record Row(string? Article, string? Name, string? Unit, string Weight, params string?[] Values);

    /// <summary>
    /// Лист как в «Tex karta.xlsx»: группа в строке 1, «1 тонна учун кг» (строка 2, итоги — totals), «Потери» (строка 3), «1 кг учун нарх»
    /// (строка 5), артикулы продуктов в строке 6, заголовок в строке 7 (№, Артикул, Наименование сырья, Ед.изм., Сум, Доллар, Курс, НАХР, Вес),
    /// продукты — с J; сырьё — с строки 8.
    /// </summary>
    public Sheet Norma(string name, string group, IReadOnlyList<(string? Article, string Name)> products, IReadOnlyList<Row> rows, string?[]? totals = null,
        bool basis = true)
    {
        var sheet = Add(name);
        sheet.Str("B1", group);
        if (basis)
        {
            sheet.Str("C2", "1 тонна учун кг");
        }

        sheet.Str("C3", "Потери").Str("C5", "1 кг учун нарх").Num("B6", "45684");
        foreach (var (h, col) in new[] { "№", "Артикул", "Наименование сырья", "Ед.изм.", "Сум", "Доллар", "Курс", "НАХР", "Вес" }.Select((h, i) => (h, i + 1)))
        {
            sheet.Str(XlsxBook.Ref(7, col), h);
        }

        for (var p = 0; p < products.Count; p++)
        {
            var col = 10 + p;
            sheet.Str(XlsxBook.Ref(1, col), group);
            if (products[p].Article is { } a)
            {
                sheet.Str(XlsxBook.Ref(6, col), a);
            }

            sheet.Str(XlsxBook.Ref(7, col), products[p].Name);
            if (totals is not null && p < totals.Length)
            {
                sheet.Value(XlsxBook.Ref(2, col), totals[p]);
            }
        }

        for (var r = 0; r < rows.Count; r++)
        {
            var row = 8 + r;
            var line = rows[r];
            sheet.Num($"A{row}", (r + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (line.Article is not null)
            {
                sheet.Str($"B{row}", line.Article);
            }

            if (line.Name is not null)
            {
                sheet.Str($"C{row}", line.Name);
            }

            if (line.Unit is not null)
            {
                sheet.Str($"D{row}", line.Unit);
            }

            // «Вес» в настоящем файле — логическое ИСТИНА/ЛОЖЬ; «1»/«0» текстом — тоже бывает.
            if (line.Weight is "TRUE" or "FALSE")
            {
                sheet.Bool($"I{row}", line.Weight == "TRUE");
            }
            else
            {
                sheet.Str($"I{row}", line.Weight);
            }
            for (var p = 0; p < line.Values.Length; p++)
            {
                sheet.Value(XlsxBook.Ref(row, 10 + p), line.Values[p]);
            }
        }

        return sheet;
    }
}

/// <summary>Чтение xlsx и разбор листов рецептур без базы: точность чисел, формулы и ошибки, строки, раскладки листов.</summary>
public class RecipeWorkbookTests
{
    [Theory]
    [InlineData("0.32400000000000001", "0.324")]
    [InlineData("4.3789999999999996", "4.379")]
    [InlineData("524.35400000000004", "524.354")]
    [InlineData("1001.001001001001", "1001.001001001")]
    [InlineData("7.1400000000000005E-2", "0.0714")]
    [InlineData("1.6E-2", "0.016")]
    [InlineData("641.65", "641.65")]
    [InlineData("-0.67", "-0.67")]
    [InlineData("1000", "1000")]
    public void Excel_numbers_are_read_as_excel_shows_them_without_binary_noise(string raw, string expected)
    {
        Assert.Equal(expected, ExcelNumbers.Format(ExcelNumbers.Parse(raw)!.Value));
    }

    [Fact]
    public void Cells_keep_text_formulas_cached_values_errors_and_rich_strings()
    {
        var book = new TestXlsx();
        book.Add("Служебный", hidden: true).Str("A1", "RICH:Сах|ар").Inline("B1", "встроенный").Formula("C1", "SUM(A2:A3)", "0").Error("D1", "#REF!", "A1/#REF!")
            .SharedFormula("E1", "12.5").Num("F1", "0.32400000000000001");
        var sheet = Assert.Single(XlsxBook.Read(new MemoryStream(book.Build())));
        Assert.True(sheet.Hidden);
        Assert.Equal("Сахар", sheet[1, 1]!.Text); // фрагменты склеены, фонетическая подсказка не попала
        Assert.Equal("встроенный", sheet[1, 2]!.Text);
        Assert.Equal((XlsxCellKind.Number, "SUM(A2:A3)", 0m), (sheet[1, 3]!.Kind, sheet[1, 3]!.Formula, sheet[1, 3]!.Number!.Value));
        Assert.Equal((XlsxCellKind.Error, "#REF!", "A1/#REF!"), (sheet[1, 4]!.Kind, sheet[1, 4]!.Text, sheet[1, 4]!.Formula));
        Assert.Equal(("shared", 12.5m), (sheet[1, 5]!.Formula, sheet[1, 5]!.Number!.Value));
        Assert.Equal(("0.32400000000000001", 0.324m), (sheet[1, 6]!.Raw, sheet[1, 6]!.Number!.Value));
        Assert.Throws<XlsxFormatException>(() => XlsxBook.Read(new MemoryStream(Encoding.UTF8.GetBytes("не zip"))));
    }

    [Fact]
    public void Recipes_are_columns_components_are_rows_and_values_stay_as_in_the_file()
    {
        var book = new TestXlsx();
        book.Add("Siryo sklad aktiv oy", hidden: true).Str("A1", "Остатки");
        book.Norma("norma keks", "КЕКС", [("K-304", "Кеко Декорли"), ("K-318", "Кеко 2,5кг"), (null, "Без артикула"), (null, "Итого")],
        [
            new("101-163", "Сахар Россия", "кг", "TRUE", "228.48", "0", null, "457"),
            new("101-081", "Яйца куринные", "кг", "1", "#REF!", "25,542", "12", null),
            new("102-070", "Этикетка Кеко", "кг", "FALSE", "31.17", null, null, null),
            new("105-053", "Отход Кекс", "кг", "0", "-6", "-6", null, null),
            new("103-061", "Коробка Кеко", "шт", "0", "1000", null, null, null),
        ], totals: ["=SUMPRODUCT($I$8:$I12,J8:J12)|228.48", "#DIV/0!"]);
        var sheets = RecipeWorkbook.Parse(XlsxBook.Read(new MemoryStream(book.Build())));

        Assert.False(sheets[0].HasRecipes);
        var keks = sheets[1];
        Assert.True(keks.HasRecipes);
        Assert.Equal(7, keks.HeaderRow);
        Assert.Contains(keks.Notes, n => n.Contains("«Итого» — итог, не продукт"));
        Assert.Equal(["Кеко Декорли", "Кеко 2,5кг", "Без артикула"], keks.Recipes.Select(r => r.Name));

        var first = keks.Recipes[0];
        Assert.Equal(("K-304", "J", "КЕКС", 1000m, "кг"), (first.Article, first.Column, first.Group, first.BaseQuantity!.Value, first.BaseUnit));
        Assert.Equal(("J2", "SUMPRODUCT($I$8:$I12,J8:J12)", 228.48m), (first.Total!.Ref, first.Total.Formula, first.Total.Number!.Value));
        Assert.Equal("norma keks|K-304", first.SourceKey);
        Assert.Equal(
            [(RecipeCellKind.Number, "J8", 228.48m), (RecipeCellKind.Error, "J9", (decimal?)null), (RecipeCellKind.Number, "J10", 31.17m),
                (RecipeCellKind.Negative, "J11", -6m), (RecipeCellKind.Number, "J12", 1000m)],
            first.Lines.Select(l => (l.Kind, l.Cell, l.Value)));
        Assert.Equal(("103-061", "Коробка Кеко", "шт", "0"), (first.Lines[4].Article, first.Lines[4].Name, first.Lines[4].Unit, first.Lines[4].Weight));
        Assert.Equal(("1", "0"), (first.Lines[0].Weight, first.Lines[2].Weight)); // ИСТИНА/ЛОЖЬ в «Вес» — как 1/0

        var second = keks.Recipes[1];
        Assert.Equal([RecipeCellKind.Zero, RecipeCellKind.Text, RecipeCellKind.Negative], second.Lines.Select(l => l.Kind)); // пустые ячейки — не строки
        Assert.Equal("25,542", second.Lines[1].Text); // текст не превращается в число
        Assert.Equal(XlsxCellKind.Error, second.Total!.Kind);
        Assert.Equal("norma keks|name:Без артикула", keks.Recipes[2].SourceKey);
    }

    [Fact]
    public void Product_left_of_service_columns_is_found_and_missing_basis_is_reported()
    {
        var book = new TestXlsx();
        // Как «norma pechenye»: продукт в H, «НАХР» — в I, «Вес» — в J, остальные продукты — с K.
        var sheet = book.Add("norma pechenye");
        sheet.Str("C3", "1 тонна учун кг");
        foreach (var (h, col) in new[] { "№", "Артикул", "Наименование сырья", "Ед.изм.", "Сум", "Доллар", "Курс" }.Select((h, i) => (h, i + 1)))
        {
            sheet.Str(XlsxBook.Ref(7, col), h);
        }

        sheet.Str("H6", "105-048").Str("H7", "Сават Цветошка").Str("I7", "НАХР").Str("J7", "Вес").Str("K6", "054").Str("K7", "ЖОНДИ 054");
        sheet.Str("B8", "101-003").Str("C8", "Мука").Str("D8", "кг").Str("J8", "1").Num("H8", "641.65").Num("K8", "680.98");
        book.Norma("norma sechka", "ГЛАЗУР", [("101-073", "Сечка")], [new("101-138", "Сечка (Гуруч)", "кг", "1", "1001")], basis: false);

        var sheets = RecipeWorkbook.Parse(XlsxBook.Read(new MemoryStream(book.Build())));
        Assert.Equal([("H", "105-048", 641.65m), ("K", "054", 680.98m)], sheets[0].Recipes.Select(r => (r.Column, r.Article!, r.Lines.Single().Value!.Value)));
        Assert.All(sheets[0].Recipes, r => Assert.Equal("1", r.Lines.Single().Weight));
        Assert.Null(sheets[1].Recipes.Single().BaseQuantity);
        Assert.Contains(sheets[1].Notes, n => n.Contains("база рецептур не определена"));
    }
}

/// <summary>
/// Импорт техкарт на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): загрузка и предпросмотр, сопоставление по артикулу и
/// вручную (запоминается), решения технолога, черновики точно по Excel, повторная загрузка того же и изменённого файла, новая версия только
/// черновиком после утверждения, одновременное создание, и производственный цикл на импортированных картах с полуфабрикатом через склад.
/// </summary>
public class RecipeImportDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Technologist = [Permissions.ProcurementRequest, Permissions.ProductionRecipes, Permissions.ProductionRead];
    private static readonly string[] Master = [Permissions.ProcurementRequest, Permissions.ProductionRead, Permissions.ProductionManage];
    private static readonly string[] Operator = [Permissions.ProcurementRequest, Permissions.ProductionRead, Permissions.ProductionOperate];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];

    private sealed record Org(string S, Department Shop, User Technologist, User Chief, User Master, User Operator, User Keeper, Guid Raw, Guid Central);

    private async Task<Org> SeedAsync()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
        var shop = new Department { Code = $"ri-{s}", Name = $"Цех {s}" };
        User Person(string login, string name) => new() { Login = $"{login}-{s}", FullName = $"{name} {s}", Department = shop };
        var org = new Org(s, shop, Person("ri-tech", "Технолог"), Person("ri-chief", "Главный технолог"), Person("ri-mst", "Мастер"), Person("ri-op", "Оператор"),
            Person("ri-kpr", "Кладовщик"), Guid.Empty, Guid.Empty);
        db.Users.AddRange(org.Technologist, org.Chief, org.Master, org.Operator, org.Keeper);
        await db.SaveChangesAsync();
        return org with
        {
            Raw = await db.ProcurementCategories.Where(c => c.Code == "raw_materials").Select(c => c.Id).SingleAsync(),
            Central = await db.ProcurementWarehouses.Where(w => w.IsDefault).Select(w => w.Id).SingleAsync(),
        };
    }

    private async Task<T> As<T>(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task<T>> act)
    {
        using var scope = fixture.Services.CreateScope();
        var ctx = await scope.ServiceProvider.GetRequiredService<ProcurementRequestService>().ContextAsync(user.Id, permissions.ToHashSet(), CancellationToken.None);
        return await act(scope.ServiceProvider, ctx);
    }

    private Task Do(User user, string[] permissions, Func<IServiceProvider, ProcurementContext, Task> act) => As(user, permissions, async (sp, c) =>
    {
        await act(sp, c);
        return true;
    });

    private static RecipeImportService Imports(IServiceProvider sp) => sp.GetRequiredService<RecipeImportService>();
    private static ProductionRecipeService Recipes(IServiceProvider sp) => sp.GetRequiredService<ProductionRecipeService>();
    private static ProductionOrderService Orders(IServiceProvider sp) => sp.GetRequiredService<ProductionOrderService>();

    private Task<Guid> ItemAsync(Org org, string sku, string name, string unit = "кг", bool batches = false, int? shelf = null) =>
        As(org.Keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().CreateItemAsync(c,
            new ProcurementCatalogInput(sku, name, org.Raw, unit, null, null, 0, TracksBatches: batches, ShelfLifeDays: shelf ?? (batches ? 60 : null)), default));

    private Task<RecipeImportUploaded> UploadAsync(Org org, byte[] file, string name = "Tex karta.xlsx") =>
        As(org.Technologist, Technologist, (sp, c) => Imports(sp).UploadAsync(c, name, new MemoryStream(file), default));

    private Task<RecipeImportView> ViewAsync(Org org, Guid id) => As(org.Technologist, Technologist, (sp, c) => Imports(sp).GetAsync(c, id, default));

    private Task<RecipeImportRecipeView> DetailAsync(Org org, Guid id, Guid recipe) =>
        As(org.Technologist, Technologist, (sp, c) => Imports(sp).RecipeAsync(c, id, recipe, default));

    private async Task<(Guid Recipe, ProductionRecipe Card)> CardAsync(string code, int version = 1)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var card = await db.ProductionRecipes.AsNoTracking().Include(r => r.Lines).ThenInclude(l => l.Item).SingleAsync(r => r.Code == code && r.VersionNo == version);
        return (card.Id, card);
    }

    private async Task ApproveAsync(Org org, Guid id)
    {
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).SubmitAsync(c, id, default));
        await Do(org.Chief, Technologist, (sp, c) => Recipes(sp).ApproveAsync(c, id, default));
    }

    /// <summary>Глазурь (полуфабрикат) и печенье на 1 тонну; варианты: печенье с текстом, без позиции, дубль столбца, пустой; мука — flour.</summary>
    private static byte[] Book(string s, string flour = "641.65", bool extras = true, string group = "ПЕЧЕНЬЕ")
    {
        var book = new TestXlsx();
        book.Norma("norma polufabrikati", "ГЛАЗУР", [($"{s}-GLZ", "Глазурь кора тест")],
        [
            new($"{s}-SUG", "Сахар Россия", "кг", "1", "600.123"),
            new($"{s}-FAT", "Пальмовое масло", "кг", "1", "399.877"),
        ], totals: ["1000"]);
        (string?, string)[] products = extras
            ? [($"{s}-CK", "Печенье тест"), ($"{s}-BC", "Печенье с текстом"), ($"{s}-MS", "Печенье без позиции"), ($"{s}-CK", "Печенье тест"), ($"{s}-EM", "Пустое")]
            : [($"{s}-CK", "Печенье тест")];
        string?[] V(params string?[] v) => extras ? v : [v[0]];
        book.Norma("norma pechenye", group, products,
        [
            new($"{s}-FLR", "Мука 1-сорт", "кг", "TRUE", V(flour, "100", "100", flour, "0")),
            new($"{s}-SUG", "Сахар Россия сорт 1", "кг", "1", V("0.32400000000000001", "25,542", null, "0.32400000000000001", null)),
            new($"{s}-GLZ", "Глазурь кора тест", "кг", "1", V("233.804", null, null, "233.804", null)),
            new($"{s}-BOX", "Коробка печенье", "шт", "0", V("500", null, null, "500", null)),
            new($"{s}-FAT", "Пальмовое масло", "кг", "1", V("0", null, null, "0", null)),
            new($"{s}-WST", "Отход этикетка", "кг", "0", V("-0.67", null, null, "-0.67", null)),
            new($"{s}-X", "Добавка X", "кг", "1", V(null, null, "5", null, null)),
        ], totals: V("875.778", "125.542", "105", "875.778", "0"));
        return book.Build();
    }

    private async Task<(Guid Flour, Guid Sugar, Guid Fat, Guid Box, Guid Glaze)> CatalogAsync(Org org, bool batches = false) =>
        (await ItemAsync(org, $"{org.S}-FLR", "Мука 1-сорт"), await ItemAsync(org, $"{org.S}-SUG", "Сахар Россия", batches: batches),
            await ItemAsync(org, $"{org.S}-FAT", "Пальмовое масло", batches: batches), await ItemAsync(org, $"{org.S}-BOX", "Коробка печенье", "шт"),
            await ItemAsync(org, $"{org.S}-GLZ", "Глазурь кора тест", batches: true, shelf: 30));

    [PostgresFact]
    public async Task Upload_shows_every_cell_decisions_unblock_and_drafts_are_created_exactly_and_once()
    {
        var org = await SeedAsync();
        var (flour, sugar, _, box, glaze) = await CatalogAsync(org);
        var file = Book(org.S);

        // Только технолог.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(org.Master, Master, (sp, c) => Imports(sp).UploadAsync(c, "t.xlsx", new MemoryStream(file), default)));
        await Assert.ThrowsAsync<FieldValidationException>(() => UploadAsync(org, file, "t.xls"));

        var up = await UploadAsync(org, file);
        Assert.False(up.Existing);
        var view = await ViewAsync(org, up.Id);
        string StatusOf(string column, string sheet = "norma pechenye") => view.Recipes.Single(r => r.Sheet == sheet && r.Column == column).Status;
        Assert.Equal(("Ready", "Ready", "NeedsReview", "NeedsReview", "Duplicate", "Empty"),
            (StatusOf("J", "norma polufabrikati"), StatusOf("J"), StatusOf("K"), StatusOf("L"), StatusOf("M"), StatusOf("N")));
        var t = view.Totals;
        Assert.Equal((6, 5, 1, 1, 2, 2), (t.Recipes, t.WithComposition, t.Empty, t.Duplicates, t.Ready, t.NeedsReview));
        Assert.Equal((1, 1, 1), (t.TextCells, t.ArticlesUnmatched, t.SemiFinished));

        // Построчно: как в Excel → в карту; ноль — не входит; отрицательное — отходы, не переносится; название в каталоге — для сведения.
        var cookie = view.Recipes.Single(r => r.Column == "J" && r.Sheet == "norma pechenye");
        var detail = await DetailAsync(org, up.Id, cookie.Id);
        var lines = detail.Lines.ToDictionary(l => l.Article!);
        Assert.Equal((641.65m, "кг", "Sku"), (lines[$"{org.S}-FLR"].CardQuantity!.Value, lines[$"{org.S}-FLR"].CardUnit, lines[$"{org.S}-FLR"].Match));
        Assert.Equal(("0.32400000000000001", 0.324m, 0.324m), (lines[$"{org.S}-SUG"].Raw, lines[$"{org.S}-SUG"].Value!.Value, lines[$"{org.S}-SUG"].CardQuantity!.Value));
        Assert.Equal((500m, "шт"), (lines[$"{org.S}-BOX"].CardQuantity!.Value, lines[$"{org.S}-BOX"].CardUnit));
        Assert.Equal("0 — не входит в состав", lines[$"{org.S}-FAT"].Outcome);
        Assert.StartsWith("отрицательная", lines[$"{org.S}-WST"].Outcome);
        Assert.Contains(detail.Issues, i => i.Code == "negative" && i.Level == RecipeImportIssue.Warn);
        Assert.DoesNotContain(detail.Issues, i => i.Code == "total_mismatch"); // «Вес» (ИСТИНА и «1») сходится с итогом листа 875.778
        Assert.Contains(detail.Issues, i => i.Code == "name_differs" && i.Text.Contains("Сахар Россия"));
        var semi = await DetailAsync(org, up.Id, view.Recipes.Single(r => r.Sheet == "norma polufabrikati").Id);
        Assert.True(semi.Row.IsSemiFinished);
        Assert.Equal(glaze, semi.OutputItemId);

        // Тот же файл ещё раз — та же загрузка.
        var again = await UploadAsync(org, file, "копия.xlsx");
        Assert.Equal((up.Id, true), (again.Id, again.Existing));

        // Текст «25,542» — не исправляется сам: норма вручную только с причиной.
        var bad = view.Recipes.Single(r => r.Column == "K" && r.Sheet == "norma pechenye");
        var textLine = (await DetailAsync(org, up.Id, bad.Id)).Lines.Single(l => l.Kind == "Text");
        Assert.Contains("текст «25,542»", (await DetailAsync(org, up.Id, bad.Id)).Issues.Single(i => i.Level == RecipeImportIssue.Block).Text);
        await Assert.ThrowsAsync<FieldValidationException>(() => As(org.Technologist, Technologist, (sp, c) =>
            Imports(sp).DecideLineAsync(c, up.Id, textLine.Id, new RecipeImportLineInput(null, RecipeImportDecision.ManualValue, 25.542m, null), default)));
        var fixedBad = await As(org.Technologist, Technologist, (sp, c) => Imports(sp).DecideLineAsync(c, up.Id, textLine.Id,
            new RecipeImportLineInput(null, RecipeImportDecision.ManualValue, 25.542m, "В ячейке запятая — 25,542 кг по рецептуре технолога"), default));
        Assert.Equal("Ready", fixedBad.Row.Status);

        // Позиции нет в каталоге — завели и сопоставили артикул: все строки загрузки, и это запоминается.
        var extra = await ItemAsync(org, $"PD-{org.S}-ADD", "Добавка пищевая X");
        var mapped = await As(org.Technologist, Technologist, (sp, c) => Imports(sp).MapArticleAsync(c, up.Id, new RecipeImportMapInput($"{org.S}-X", extra), default));
        Assert.Equal("Ready", mapped.Recipes.Single(r => r.Column == "L" && r.Sheet == "norma pechenye").Status);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.True(await db.ProductionRecipeImportItemMaps.AnyAsync(m => m.Article == $"{org.S}-X" && m.ItemId == extra));
        }

        // Создать черновики: 4 карты, точно как в Excel; дубль и пустая — нет.
        var result = await As(org.Technologist, Technologist, (sp, c) => Imports(sp).ApplyAsync(c, up.Id, new RecipeImportApplyInput(null), default));
        Assert.Equal((4, 0, 0), (result.Created, result.NewVersions, result.Failed));
        var (_, card) = await CardAsync($"{org.S}-CK");
        Assert.Equal((ProductionRecipeStatus.Draft, 1000m, "кг", "Печенье тест"), (card.Status, card.OutputQuantity, card.OutputUnit, card.Name));
        Assert.Equal(new[] { (flour, 641.65m, (string?)null), (sugar, 0.324m, null), (glaze, 233.804m, null), (box, 500m, null) }.OrderBy(x => x.Item1),
            card.Lines.Select(l => (l.ItemId, l.Quantity, l.UnitCode)).OrderBy(x => x.Item1));
        Assert.All(card.Lines, l => Assert.Null(l.StageId));
        Assert.Contains("Требуется распределение по этапам технологом", card.Comment);
        Assert.Contains("Отход этикетка -0.67", card.Comment);
        Assert.Equal(glaze, (await CardAsync($"{org.S}-GLZ")).Card.OutputItemId);
        Assert.Equal(25.542m, (await CardAsync($"{org.S}-BC")).Card.Lines.Single(l => l.ItemId == sugar).Quantity);

        // Повторное нажатие — ничего нового.
        var twice = await As(org.Technologist, Technologist, (sp, c) => Imports(sp).ApplyAsync(c, up.Id, new RecipeImportApplyInput(null), default));
        Assert.Equal(0, twice.Created + twice.NewVersions);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            Assert.Equal(1, await db.ProductionRecipes.CountAsync(r => r.Code == $"{org.S}-CK"));
            Assert.Contains(await db.ProcurementAuditLogs.Where(l => l.DocType == ProductionRules.RecipeImportDoc && l.DocId == up.Id).Select(l => l.Action).ToListAsync(),
                a => a == "applied");
        }
    }

    [PostgresFact]
    public async Task Changed_file_proposes_a_new_version_draft_and_never_touches_the_approved_card()
    {
        var org = await SeedAsync();
        var (flour, _, _, _, _) = await CatalogAsync(org);
        var first = await UploadAsync(org, Book(org.S, extras: false));
        await As(org.Technologist, Technologist, (sp, c) => Imports(sp).ApplyAsync(c, first.Id, new RecipeImportApplyInput(null), default));
        var (v1, _) = await CardAsync($"{org.S}-CK");

        // Утверждает другой технолог; автор свою версию не утверждает.
        await Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).SubmitAsync(c, v1, default));
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Do(org.Technologist, Technologist, (sp, c) => Recipes(sp).ApproveAsync(c, v1, default)));
        await Do(org.Chief, Technologist, (sp, c) => Recipes(sp).ApproveAsync(c, v1, default));

        // Норма муки изменилась: черновик новой версии, видно что изменится в карте и что изменилось в файле.
        var second = await UploadAsync(org, Book(org.S, flour: "650.1", extras: false));
        Assert.NotEqual(first.Id, second.Id);
        var row = (await ViewAsync(org, second.Id)).Recipes.Single(r => r.Sheet == "norma pechenye");
        Assert.Equal(("Ready", "NewVersion"), (row.Status, row.Action));
        var detail = await DetailAsync(org, second.Id, row.Id);
        Assert.Equal(("changed", "641.65 кг", "650.1 кг"), detail.CardChanges.Where(c => c.Subject == "Мука 1-сорт").Select(c => (c.Kind, c.Before, c.After)).Single());
        Assert.Equal(("changed", "641.65", "650.1"), detail.FileChanges.Select(c => (c.Kind, c.Before, c.After)).Single());

        // Два окна одновременно — одна новая версия.
        var results = await Task.WhenAll(
            As(org.Technologist, Technologist, (sp, c) => Imports(sp).ApplyAsync(c, second.Id, new RecipeImportApplyInput([row.Id]), default)),
            As(org.Technologist, Technologist, (sp, c) => Imports(sp).ApplyAsync(c, second.Id, new RecipeImportApplyInput([row.Id]), default)));
        Assert.Equal(1, results.Sum(r => r.NewVersions + r.Created));
        var (_, active) = await CardAsync($"{org.S}-CK");
        var (v2, draft) = await CardAsync($"{org.S}-CK", 2);
        Assert.Equal((ProductionRecipeStatus.Active, 641.65m), (active.Status, active.Lines.Single(l => l.ItemId == flour).Quantity));
        Assert.Equal((ProductionRecipeStatus.Draft, 650.1m), (draft.Status, draft.Lines.Single(l => l.ItemId == flour).Quantity));
        var compare = await As(org.Technologist, Technologist, (sp, c) => Recipes(sp).CompareAsync(c, v1, v2, default));
        Assert.Contains(compare.Changes, c => c.Section == "Компонент" && c.Field == "Норма" && c.Before!.StartsWith("641,65") && c.After!.StartsWith("650,1"));

        // Тот же состав в другом файле — «без изменений»: черновик уже такой.
        var third = await UploadAsync(org, Book(org.S, flour: "650.1", extras: false, group: "ПЕЧЕНЬЕ 2026"));
        Assert.Equal("Unchanged", (await ViewAsync(org, third.Id)).Recipes.Single(r => r.Sheet == "norma pechenye").Status);
    }

    [PostgresFact]
    public async Task Imported_cards_run_the_production_cycle_and_the_semi_finished_is_written_off_once()
    {
        var org = await SeedAsync();
        var (flour, sugar, fat, box, glaze) = await CatalogAsync(org, batches: true);
        var book = new TestXlsx();
        book.Norma("norma polufabrikati", "ГЛАЗУР", [($"{org.S}-GLZ", "Глазурь кора тест")],
            [new($"{org.S}-SUG", "Сахар Россия", "кг", "1", "600"), new($"{org.S}-FAT", "Пальмовое масло", "кг", "1", "400")], totals: ["1000"]);
        book.Norma("norma pechenye", "ПЕЧЕНЬЕ", [($"{org.S}-CK", "Печенье тест")],
        [
            new($"{org.S}-FLR", "Мука 1-сорт", "кг", "1", "600"), new($"{org.S}-GLZ", "Глазурь кора тест", "кг", "1", "300"),
            new($"{org.S}-SUG", "Сахар Россия", "кг", "1", "100"), new($"{org.S}-BOX", "Коробка печенье", "шт", "0", "500"),
        ], totals: ["1000"]);
        var up = await UploadAsync(org, book.Build());
        Assert.Equal(2, (await As(org.Technologist, Technologist, (sp, c) => Imports(sp).ApplyAsync(c, up.Id, new RecipeImportApplyInput(null), default))).Created);
        var (glazeCard, _) = await CardAsync($"{org.S}-GLZ");
        var (cookieCard, _) = await CardAsync($"{org.S}-CK");
        await ApproveAsync(org, glazeCard);
        await ApproveAsync(org, cookieCard);

        // Склад: сахар двумя партиями (FEFO — сначала с ближним сроком), масло, мука, коробки.
        var stock = (IServiceProvider sp) => sp.GetRequiredService<ProcurementStockService>();
        await Do(org.Keeper, Keeper, (sp, c) => stock(sp).ReceiptAsync(c, new ProcurementStockOperationInput(
        [
            new(sugar, 300, 9_000, Batches: [new ProcurementBatchInput("S-NEAR", null, FieldClock.Today.AddDays(10), 150), new ProcurementBatchInput("S-FAR", null, FieldClock.Today.AddDays(90), 150)]),
            new(fat, 100, 20_000, Batches: [new ProcurementBatchInput("F-1", null, FieldClock.Today.AddDays(60), 100)]),
            new(flour, 1000, 6_000),
            new(box, 1000, 2_000),
        ], "Приход сырья"), default));

        // Глазурь 200 кг: сахар 120, масло 80 — по нормам из Excel на 1 тонну.
        var glazeOrder = await As(org.Master, Master, (sp, c) => Orders(sp).CreateAsync(c, new ProductionOrderInput(glazeCard, org.Shop.Id, org.Central, 200, FieldClock.Today, null, null), default));
        await Do(org.Master, Master, (sp, c) => Orders(sp).ReleaseAsync(c, glazeOrder, default));
        var g = await As(org.Master, Master, (sp, c) => Orders(sp).GetAsync(c, glazeOrder, default));
        Assert.Equal(new[] { (sugar, 120m), (fat, 80m) }.OrderBy(x => x.Item1), g.Lines.Select(l => (l.ItemId, l.Required)).OrderBy(x => x.Item1));
        await Do(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, glazeOrder, new([new(sugar, 120), new(fat, 80)]), default));
        var holding = (await As(org.Keeper, Keeper, (sp, c) => Orders(sp).GetAsync(c, glazeOrder, default))).Holdings.Where(h => h.ItemId == sugar).ToList();
        Assert.Equal([("S-NEAR", 120m)], holding.Select(h => (h.SupplierBatchNo ?? h.BatchNumber!, h.Issued))); // FEFO

        // Смена: годный выход 198 кг; утверждает мастер. Возврат 1 кг сахара в ту же партию; закрытие; выход — партией на склад.
        var shifts = (IServiceProvider sp) => sp.GetRequiredService<ProductionShiftService>();
        var shift = await As(org.Operator, Operator, (sp, c) => shifts(sp).CreateAsync(c, new ProductionShiftCreateInput(glazeOrder, FieldClock.Today), default));
        await As(org.Operator, Operator, (sp, c) => shifts(sp).SaveAsync(c, shift, new ProductionShiftInput(FieldClock.Today, 1, null, null, 198, null, null, null, [], [], [], []), default));
        await Do(org.Operator, Operator, (sp, c) => shifts(sp).CloseAsync(c, shift, default));
        await Do(org.Master, Master, (sp, c) => shifts(sp).ApproveAsync(c, shift, default));
        var nearBatch = holding.Single().BatchId!.Value;
        await Do(org.Keeper, Keeper, (sp, c) => Orders(sp).ReturnAsync(c, glazeOrder, new([new(sugar, 1, nearBatch)]), default));
        await Do(org.Master, Master, (sp, c) => Orders(sp).CompleteAsync(c, glazeOrder, new ProductionCompleteInput(198), default));
        await Do(org.Keeper, Keeper, (sp, c) => Orders(sp).ReceiveOutputAsync(c, glazeOrder, default));

        // Печенье 500 кг: глазурь 150 кг — со склада, как сырьё; сахара и масла глазури в задании печенья нет (только свой сахар 50).
        var cookieOrder = await As(org.Master, Master, (sp, c) => Orders(sp).CreateAsync(c, new ProductionOrderInput(cookieCard, org.Shop.Id, org.Central, 500, FieldClock.Today, null, null), default));
        await Do(org.Master, Master, (sp, c) => Orders(sp).ReleaseAsync(c, cookieOrder, default));
        var ck = await As(org.Master, Master, (sp, c) => Orders(sp).GetAsync(c, cookieOrder, default));
        Assert.Equal(new[] { (flour, 300m), (glaze, 150m), (sugar, 50m), (box, 250m) }.OrderBy(x => x.Item1), ck.Lines.Select(l => (l.ItemId, l.Required)).OrderBy(x => x.Item1));
        Assert.DoesNotContain(ck.Lines, l => l.ItemId == fat);
        Assert.Equal(198m, ck.Lines.Single(l => l.ItemId == glaze).Available);

        // Нехватка: задание на 1 000 кг печенья требует 300 кг глазури — на складе после выдачи останется 48; выпуск не запрещён.
        var big = await As(org.Master, Master, (sp, c) => Orders(sp).CreateAsync(c, new ProductionOrderInput(cookieCard, org.Shop.Id, org.Central, 1000, FieldClock.Today, null, null), default));
        await Do(org.Master, Master, (sp, c) => Orders(sp).ReleaseAsync(c, big, default));

        await Do(org.Keeper, Keeper, (sp, c) => Orders(sp).IssueAsync(c, cookieOrder, new([new(flour, 300), new(glaze, 150), new(sugar, 50), new(box, 250)]), default));
        var shortage = (await As(org.Master, Master, (sp, c) => Orders(sp).GetAsync(c, big, default))).Lines.Single(l => l.ItemId == glaze);
        Assert.True(shortage.Available < shortage.Required);
        Assert.Equal((300m, 48m), (shortage.Required, shortage.Available));

        var shift2 = await As(org.Operator, Operator, (sp, c) => shifts(sp).CreateAsync(c, new ProductionShiftCreateInput(cookieOrder, FieldClock.Today), default));
        await As(org.Operator, Operator, (sp, c) => shifts(sp).SaveAsync(c, shift2, new ProductionShiftInput(FieldClock.Today, 1, null, null, 500, null, null, null, [], [], [], []), default));
        await Do(org.Operator, Operator, (sp, c) => shifts(sp).CloseAsync(c, shift2, default));
        await Do(org.Master, Master, (sp, c) => shifts(sp).ApproveAsync(c, shift2, default));
        await Do(org.Master, Master, (sp, c) => Orders(sp).CompleteAsync(c, cookieOrder, new ProductionCompleteInput(500), default));
        var done = await As(org.Master, Master, (sp, c) => Orders(sp).GetAsync(c, cookieOrder, default));
        Assert.All(done.Lines, l => Assert.Equal(0m, l.Deviation ?? 0)); // расход по норме на факт
        Assert.All(done.Lines, l => Assert.False(l.Overconsumed));

        // Двойного списания нет: масло — только в задание глазури; сахар — 119 в глазурь и 50 в печенье; глазурь — 150 в печенье, остаток 48.
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var moves = await db.ProcurementStockMovements.AsNoTracking().Where(m => m.Document!.ProductionOrderId != null && new[] { sugar, fat, glaze }.Contains(m.ItemId))
            .Select(m => new { m.ItemId, m.Quantity, Order = m.Document!.ProductionOrderId!.Value }).ToListAsync();
        decimal Net(Guid item, Guid order) => -moves.Where(m => m.ItemId == item && m.Order == order).Sum(m => m.Quantity);
        Assert.Equal((80m, 0m), (Net(fat, glazeOrder), Net(fat, cookieOrder)));
        Assert.Equal((119m, 50m), (Net(sugar, glazeOrder), Net(sugar, cookieOrder)));
        Assert.Equal((-198m, 150m), (Net(glaze, glazeOrder), Net(glaze, cookieOrder))); // приход выхода (−) и выдача (+)
        Assert.Equal(48m, await db.ProcurementStockBalances.Where(b => b.ItemId == glaze).SumAsync(b => b.OnHand));
    }
}

/// <summary>Тест на настоящем файле техкарт — только если путь задан в ONEBASE_TEX_KARTA (файл в репозиторий не кладётся).</summary>
public sealed class TexKartaFactAttribute : FactAttribute
{
    public const string Variable = "ONEBASE_TEX_KARTA";

    public TexKartaFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
        {
            Skip = $"Нет {Variable} — разбор настоящего файла техкарт пропущен.";
        }
    }
}

public class TexKartaFileTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [TexKartaFact]
    public void Real_file_is_parsed_sheet_by_sheet()
    {
        using var file = File.OpenRead(Environment.GetEnvironmentVariable(TexKartaFactAttribute.Variable)!);
        var book = XlsxBook.Read(file);
        var bambuk = book.Single(s => s.Name == "norma bambuk");
        output.WriteLine("заголовок: " + string.Join(" | ", Enumerable.Range(1, 12).Select(c => $"{XlsxBook.Column(c)}={bambuk.Text(7, c)}")));
        output.WriteLine("строка 8: " + string.Join(" | ", Enumerable.Range(1, 12).Select(c => $"{XlsxBook.Column(c)}={bambuk[8, c]?.Kind}:{bambuk.Text(8, c)}")));
        var sheets = RecipeWorkbook.Parse(book);
        foreach (var s in sheets)
        {
            var lines = s.Recipes.SelectMany(r => r.Lines).ToList();
            output.WriteLine($"{s.Name} [{(s.Hidden ? "скрыт" : "виден")}] рецептуры={s.HasRecipes} продуктов={s.Recipes.Count} с составом={s.Recipes.Count(r => r.Positive.Any())} " +
                             $"норм={lines.Count(l => l.Kind == RecipeCellKind.Number)} нулей={lines.Count(l => l.Kind == RecipeCellKind.Zero)} " +
                             $"отриц={lines.Count(l => l.Kind == RecipeCellKind.Negative)} ошибок={lines.Count(l => l.Kind == RecipeCellKind.Error)} " +
                             $"текст={lines.Count(l => l.Kind == RecipeCellKind.Text)} база={s.Recipes.FirstOrDefault()?.BaseQuantity} {string.Join(" ", s.Notes)}");
        }

        var all = sheets.SelectMany(s => s.Recipes).ToList();
        output.WriteLine($"ИТОГО продуктов={all.Count} с составом={all.Count(r => r.Positive.Any())} норм={all.Sum(r => r.Positive.Count())}");
        var tooPrecise = all.SelectMany(r => r.Positive.Where(l => ExcelNumbers.Scale(l.Value!.Value) > RecipeImportAnalyzer.MaxScale).Select(l => $"{r.Sheet}!{l.Cell}={l.Value}"));
        output.WriteLine($"больше 6 знаков: {string.Join(", ", tooPrecise)}");
        foreach (var (r, l) in all.SelectMany(r => r.Lines.Where(l => l.Kind is RecipeCellKind.Text or RecipeCellKind.Error).Select(l => (r, l))))
        {
            output.WriteLine($"не число: {r.Sheet}!{l.Cell} [{r.Name}] {l.Article} «{l.Name}»: «{l.Text}» raw={l.Raw} f={l.Formula}");
        }
        Assert.Contains(sheets, s => s.HasRecipes);
    }
}
