using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Application.Production;
using OneBase.Application.Security;
using OneBase.Domain.Identity;
using OneBase.Infrastructure.Persistence;
using SkiaSharp;

namespace OneBase.AI.Tests;

/// <summary>
/// Фото продукта техкарты на настоящем PostgreSQL (ONEBASE_TEST_POSTGRES, иначе пропускается): загружает только тот, кто ведёт техкарты;
/// не картинка и пустой файл — понятная ошибка; сервер вырезает центр 4:3 и кладёт три JPEG; фото одно на код — видно у новой версии;
/// замена удаляет прежние файлы, удаление — фото у всех версий; в истории версии — запись; счётчики фильтров списка — с учётом поиска.
/// </summary>
public class ProductionRecipePhotoDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private static readonly string[] Technologist = [Permissions.ProcurementRequest, Permissions.ProductionRecipes, Permissions.ProductionRead];
    private static readonly string[] Master = [Permissions.ProcurementRequest, Permissions.ProductionRead, Permissions.ProductionManage];
    private static readonly string[] Keeper = [Permissions.ProcurementRequest, Permissions.ProcurementWarehouse];

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

    private static ProductionRecipeService Recipes(IServiceProvider sp) => sp.GetRequiredService<ProductionRecipeService>();
    private static ProductionRecipePhotoService Photos(IServiceProvider sp) => sp.GetRequiredService<ProductionRecipePhotoService>();

    /// <summary>PNG заданного размера: левая половина красная, правая синяя — по среднему цвету видно, что кроп центральный.</summary>
    private static byte[] Png(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.Red);
        using var paint = new SKPaint { Color = SKColors.Blue };
        surface.Canvas.DrawRect(width / 2f, 0, width / 2f, height, paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static Func<IServiceProvider, ProcurementContext, Task<ProductionRecipePhotoView>> Upload(Guid recipeId, byte[] bytes, string type = "image/png") =>
        (sp, c) => Photos(sp).SaveAsync(c, recipeId, new MemoryStream(bytes), bytes.Length, type, default);

    [PostgresFact]
    public async Task Technologist_uploads_a_cropped_photo_that_follows_the_code_across_versions()
    {
        var s = Guid.NewGuid().ToString("N")[..6];
        User technologist, chief, master, keeper;
        Guid raw;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            await DbSeeder.SeedProcurementAsync(db, CancellationToken.None);
            var shop = new Department { Code = $"ph-{s}", Name = $"Цех {s}" };
            User Person(string login) => new() { Login = $"{login}-{s}", FullName = $"{login} {s}", Department = shop };
            (technologist, chief, master, keeper) = (Person("ph-tech"), Person("ph-chief"), Person("ph-mst"), Person("ph-kpr"));
            db.Users.AddRange(technologist, chief, master, keeper);
            await db.SaveChangesAsync();
            raw = await db.ProcurementCategories.Where(c => c.Code == "raw_materials").Select(c => c.Id).SingleAsync();
        }

        var flour = await As(keeper, Keeper, (sp, c) => sp.GetRequiredService<ProcurementStockService>().CreateItemAsync(c,
            new ProcurementCatalogInput($"PH-{Guid.NewGuid():N}"[..14], $"Мука {s}", raw, "кг", null, null, 0), default));
        var code = $"PH-{s}";
        var input = new ProductionRecipeInput(code, $"Кекс {s}", null, null, "кг", 1000, null, [new(flour, 1000)]);
        var v1 = await As(technologist, Technologist, (sp, c) => Recipes(sp).CreateAsync(c, input, default));

        // Права и формат: мастер фото не меняет; не картинка и пустой файл — ошибка с текстом.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => As(master, Master, Upload(v1, Png(400, 300))));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(technologist, Technologist, Upload(v1, "not an image"u8.ToArray())));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(technologist, Technologist, Upload(v1, [], "image/jpeg")));
        await Assert.ThrowsAsync<FieldValidationException>(() => As(technologist, Technologist, Upload(v1, Png(400, 300), "application/pdf")));

        // 1200 × 600 → центр 4:3 = 800 × 600 (без увеличения до 1600); превью 480 × 360. Средний цвет — поровну красного и синего.
        var photo = await As(technologist, Technologist, Upload(v1, Png(1200, 600)));
        Assert.Equal((800, 600), (photo.Width, photo.Height));
        Assert.Matches("^#[0-9A-F]{6}$", photo.Tint!);
        var storage = (MemoryStorage)fixture.Services.GetRequiredService<IFileStorage>();
        var keys = await As(master, Master, async (sp, c) => new[]
        {
            await Photos(sp).KeyAsync(c, photo.Id, 1600, default), await Photos(sp).KeyAsync(c, photo.Id, 960, default), await Photos(sp).KeyAsync(c, photo.Id, 480, default),
        });
        var sizes = new List<(int, int)>();
        foreach (var key in keys)
        {
            using var buffer = new MemoryStream();
            await storage.DownloadAsync(key, buffer);
            using var bitmap = SKBitmap.Decode(buffer.ToArray());
            sizes.Add((bitmap.Width, bitmap.Height));
        }

        Assert.Equal([(800, 600), (800, 600), (480, 360)], sizes);
        await Assert.ThrowsAsync<FieldNotFoundException>(() => As(master, Master, (sp, c) => Photos(sp).KeyAsync(c, photo.Id, 123, default)));

        // Видят все, кто видит техкарты: в списке и в карте; менять — только технолог.
        var row = (await As(master, Master, (sp, c) => Recipes(sp).ListAsync(c, code, null, default))).Items.Single();
        Assert.Equal(photo, row.Photo);
        var card = await As(master, Master, (sp, c) => Recipes(sp).GetAsync(c, v1, default));
        Assert.Equal(photo, card.Photo);
        Assert.False(card.Can.Photo);
        Assert.True((await As(technologist, Technologist, (sp, c) => Recipes(sp).GetAsync(c, v1, default))).Can.Photo);

        // Утвердили и создали новую версию — фото то же, ничего не копируется.
        await Do(technologist, Technologist, (sp, c) => Recipes(sp).SubmitAsync(c, v1, default));
        await Do(chief, Technologist, (sp, c) => Recipes(sp).ApproveAsync(c, v1, default));
        var v2 = await As(technologist, Technologist, (sp, c) => Recipes(sp).NewVersionAsync(c, v1, default));
        Assert.Equal(photo, (await As(technologist, Technologist, (sp, c) => Recipes(sp).GetAsync(c, v2, default))).Photo);

        // Заменили через черновик v2 — новый адрес у всех версий, прежние файлы удалены.
        var replaced = await As(technologist, Technologist, Upload(v2, Png(300, 400)));
        Assert.NotEqual(photo.Id, replaced.Id);
        Assert.Equal((300, 225), (replaced.Width, replaced.Height));
        Assert.Equal(replaced, (await As(master, Master, (sp, c) => Recipes(sp).GetAsync(c, v1, default))).Photo);
        var left = await storage.ListKeysAsync().ToListAsync();
        Assert.DoesNotContain(left, keys.Contains);
        await Assert.ThrowsAsync<FieldNotFoundException>(() => As(master, Master, (sp, c) => Photos(sp).KeyAsync(c, photo.Id, 960, default)));

        // Счётчики фильтров — по найденному: одна карточка, утверждённая, с черновиком.
        var list = await As(master, Master, (sp, c) => Recipes(sp).ListAsync(c, code, "draft", default));
        Assert.Equal(new ProductionRecipeCounts(1, 1, 1, 0, 0), list.Counts);
        Assert.Single(list.Items);

        // Удалили — фото нет ни у одной версии, файлов нет; в истории версий — добавление, замена, удаление.
        await Assert.ThrowsAsync<FieldForbiddenException>(() => Do(master, Master, (sp, c) => Photos(sp).DeleteAsync(c, v1, default)));
        await Do(technologist, Technologist, (sp, c) => Photos(sp).DeleteAsync(c, v1, default));
        Assert.Null((await As(master, Master, (sp, c) => Recipes(sp).GetAsync(c, v2, default))).Photo);
        Assert.Null((await As(master, Master, (sp, c) => Recipes(sp).ListAsync(c, code, null, default))).Items.Single().Photo);
        Assert.DoesNotContain(await storage.ListKeysAsync().ToListAsync(), k => k.Contains(replaced.Id.ToString("N")));
        Assert.Contains("photo.added", (await As(master, Master, (sp, c) => Recipes(sp).GetAsync(c, v1, default))).History.Select(h => h.Action));
        Assert.Equal(["created", "photo.replaced"], (await As(master, Master, (sp, c) => Recipes(sp).GetAsync(c, v2, default))).History.Select(h => h.Action));
        await Do(technologist, Technologist, (sp, c) => Photos(sp).DeleteAsync(c, v2, default)); // уже нет — ничего не происходит
    }
}
