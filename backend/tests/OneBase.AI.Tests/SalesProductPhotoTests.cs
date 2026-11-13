using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;
using OneBase.Infrastructure.Storage;
using SkiaSharp;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Фото товаров без базы: вписывание в квадрат без прозрачных полей, WebP с прозрачностью; набор фото при сервере цел.</summary>
public class SalesProductPhotoImageTests
{
    /// <summary>PNG 400×300 с прозрачным фоном и непрозрачным кругом диаметром 200 в середине.</summary>
    internal static byte[] Cutout(SKColor color)
    {
        using var surface = SKSurface.Create(new SKImageInfo(400, 300, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        surface.Canvas.DrawCircle(200, 150, 100, paint);
        using var image = surface.Snapshot();
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    [Fact]
    public void Cutout_is_trimmed_fitted_without_enlargement_and_stays_transparent()
    {
        var fitted = new SkiaImageProcessor().FitToWebp(Cutout(SKColors.Red), [160, 400, 800], 80)!;
        Assert.Equal([(160, 160, 160), (400, 200, 200), (800, 200, 200)], fitted.Variants.Select(v => (v.Box, v.Width, v.Height)));
        Assert.Equal((200, 200), (fitted.Width, fitted.Height));

        using var codec = SKCodec.Create(SKData.CreateCopy(fitted.Variants[1].Webp));
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        using var bitmap = SKBitmap.Decode(codec);
        Assert.True(bitmap.GetPixel(2, 2).Alpha < 10, "угол круга — прозрачный");
        Assert.True(bitmap.GetPixel(100, 100).Alpha > 245, "середина — непрозрачная");
    }

    [Fact]
    public void Photo_without_alpha_keeps_its_frame_and_garbage_is_rejected()
    {
        using var surface = SKSurface.Create(new SKImageInfo(300, 200));
        surface.Canvas.Clear(SKColors.SkyBlue);
        using var jpeg = surface.Snapshot().Encode(SKEncodedImageFormat.Jpeg, 90);
        var fitted = new SkiaImageProcessor().FitToWebp(jpeg.ToArray(), [160], 80)!;
        // Большой JPEG декодер читает уменьшенным (восьмыми) — высота с точностью до пикселя.
        Assert.Equal(160, fitted.Variants[0].Width);
        Assert.InRange(fitted.Variants[0].Height, 106, 107);
        Assert.Null(new SkiaImageProcessor().FitToWebp([1, 2, 3, 4], [160], 80));
    }

    [Fact]
    public void Seed_set_next_to_the_server_has_every_file_and_size()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "OneBase.Api")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var folder = Path.Combine(dir.FullName, "src", "OneBase.Api", "SeedData", "product-photos");
        var manifest = JsonSerializer.Deserialize<ProductPhotoSeedManifest>(File.ReadAllText(Path.Combine(folder, "manifest.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(SalesProductPhotoService.Sizes, manifest.Sizes);
        var missing = manifest.Photos.Values.SelectMany(list => list).Select(p => p.File).Distinct()
            .SelectMany(f => manifest.Sizes.Select(s => $"{f}-{s}.webp")).Where(f => !File.Exists(Path.Combine(folder, f))).ToList();
        Assert.Empty(missing);
        Assert.True(manifest.Photos.Count > 200);
        Assert.Contains(manifest.Photos.Values, list => list[0].Similar && list[0].From is not null);
        Assert.All(manifest.Photos.Values.SelectMany(l => l), p => Assert.True(p.W is > 0 and <= 800 && p.H is > 0 and <= 800, p.File));
    }
}

/// <summary>
/// Фото товаров на PostgreSQL: набор при сервере — один раз на код (удалённое не возвращается, товар, появившийся позже, получает фото),
/// своя загрузка убирает похожие, замена встаёт на место заменённого, «главное», удаление — файлы из хранилища тоже.
/// </summary>
public class SalesProductPhotoDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    private const long First = 9_900_001;
    private const long Second = 9_900_002;
    private const long Late = 9_900_003;

    private static readonly Guid User = Guid.NewGuid();

    private async Task<(IServiceScope Scope, SalesProductPhotoService Service, OneBaseDbContext Db, MemoryStorage Storage)> StartAsync()
    {
        await fixture.ResetAsync();
        var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        await db.SalesProductPhotos.ExecuteDeleteAsync();
        await db.SalesProductPhotoSeeds.ExecuteDeleteAsync();
        await db.LinkoProducts.Where(p => p.Id >= First && p.Id <= Late).ExecuteDeleteAsync();
        db.LinkoProducts.AddRange(new LinkoProduct { Id = First, Name = "TST1 Конфеты 0,5 кг", Code = "TST1" }, new LinkoProduct { Id = Second, Name = "TST2 Конфеты 4,5 кг", Code = "TST2" });
        await db.SaveChangesAsync();
        return (scope, scope.ServiceProvider.GetRequiredService<SalesProductPhotoService>(), db, (MemoryStorage)scope.ServiceProvider.GetRequiredService<IFileStorage>());
    }

    /// <summary>Папка набора: файл «a» всех размеров; TST1 — своё фото, TST2 — похожее (от TST1), LATE — товара ещё нет в базе.</summary>
    private static string SeedFolder()
    {
        var folder = Directory.CreateTempSubdirectory("photos-seed-").FullName;
        var fitted = new SkiaImageProcessor().FitToWebp(SalesProductPhotoImageTests.Cutout(SKColors.Orange), SalesProductPhotoService.Sizes, 80)!;
        foreach (var v in fitted.Variants)
        {
            File.WriteAllBytes(Path.Combine(folder, $"a-{v.Box}.webp"), v.Webp);
        }

        var manifest = new
        {
            sizes = SalesProductPhotoService.Sizes,
            photos = new Dictionary<string, object[]>
            {
                ["TST1"] = [new { file = "a", w = 200, h = 200 }],
                ["TST2"] = [new { file = "a", w = 200, h = 200, similar = true, from = "TST1" }],
                ["LATE"] = [new { file = "a", w = 200, h = 200 }],
            },
        };
        File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(manifest));
        return folder;
    }

    private static MemoryStream Png(SKColor color) => new(SalesProductPhotoImageTests.Cutout(color));

    private async Task<IReadOnlyList<ProductPhotoView>> UploadAsync(SalesProductPhotoService service, long product, SKColor color, Guid? replace = null)
    {
        using var png = Png(color);
        return await service.UploadAsync(User, product, png, png.Length, "image/png", replace, default);
    }

    private static List<string> Keys(MemoryStorage storage) => storage.ListKeysAsync().ToBlockingEnumerable().Where(k => k.StartsWith("sales/product-photos/")).ToList();

    [PostgresFact]
    public async Task Seed_runs_once_per_code_and_uploads_replace_reorder_and_delete()
    {
        var (scope, service, db, storage) = await StartAsync();
        using var _ = scope;
        var folder = SeedFolder();

        // Набор: TST1 и TST2 получили фото, LATE — нет (товара нет) и не отмечен.
        Assert.Equal(2, await service.SeedAsync(folder, default));
        var all = await service.ListAsync(null, default);
        Assert.False(all[First].Single().Similar);
        Assert.Equal((true, "TST1"), (all[Second].Single().Similar, all[Second].Single().SimilarFrom));
        Assert.Equal(["TST1", "TST2"], await db.SalesProductPhotoSeeds.Select(s => s.Code).OrderBy(c => c).ToListAsync());
        Assert.Equal(6, Keys(storage).Count);
        Assert.Equal(0, await service.SeedAsync(folder, default));

        // Своё фото у товара с похожим: похожее убрано (и его файлы), своё — главное.
        var similar = all[Second].Single();
        var list = await UploadAsync(service, Second, SKColors.Blue);
        var own = Assert.Single(list);
        Assert.False(own.Similar);
        Assert.Equal((200, 200), (own.Width, own.Height));
        Assert.DoesNotContain(Keys(storage), k => k.Contains(similar.File.ToString("N")));

        // Второе фото — последним; «главное» — первым; замена первого встаёт на его место.
        list = await UploadAsync(service, Second, SKColors.Green);
        Assert.Equal(own.Id, list[0].Id);
        var second = list[1];
        list = await service.MakeMainAsync(User, second.Id, default);
        Assert.Equal([second.Id, own.Id], list.Select(p => p.Id));
        list = await UploadAsync(service, Second, SKColors.Purple, replace: second.Id);
        Assert.Equal(2, list.Count);
        Assert.Equal(own.Id, list[1].Id);
        Assert.NotEqual(second.Id, list[0].Id);
        Assert.Equal(0, await db.SalesProductPhotos.CountAsync(p => p.Id == second.Id));

        // Удаление: остальные сдвигаются, файлы удалённого — из хранилища.
        list = await service.DeleteAsync(User, list[0].Id, default);
        Assert.Equal(own.Id, Assert.Single(list).Id);
        Assert.Equal(0, (await db.SalesProductPhotos.SingleAsync(p => p.Id == own.Id)).SortOrder);

        // Удалённое вручную фото набора не возвращается; товар, появившийся позже, получает своё.
        await service.DeleteAsync(User, all[First].Single().Id, default);
        db.LinkoProducts.Add(new LinkoProduct { Id = Late, Name = "LATE Новый товар", Code = "LATE" });
        await db.SaveChangesAsync();
        Assert.Equal(1, await service.SeedAsync(folder, default));
        all = await service.ListAsync([First, Late], default);
        Assert.False(all.ContainsKey(First));
        Assert.Single(all[Late]);
        Assert.Contains(Keys(storage), k => k == SalesProductPhotoService.Key(all[Late][0].File, 800));

        // Ошибки: не картинка, нет товара, нет размера.
        using var garbage = new MemoryStream([1, 2, 3, 4]);
        await Assert.ThrowsAsync<FieldValidationException>(() => service.UploadAsync(User, First, garbage, 4, "image/png", null, default));
        await Assert.ThrowsAsync<FieldNotFoundException>(() => UploadAsync(service, 1, SKColors.Red));
        await Assert.ThrowsAsync<FieldNotFoundException>(() => service.KeyAsync(all[Late][0].File, 500, default));
        Assert.Equal(SalesProductPhotoService.Key(all[Late][0].File, 400), await service.KeyAsync(all[Late][0].File, 400, default));
        Directory.Delete(folder, true);
    }
}
