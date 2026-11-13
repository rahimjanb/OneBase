using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Audit;
using OneBase.Domain.Sales;

namespace OneBase.Application.Sales;

/// <summary>Фото в ответах: Id — запись (менять, удалять), File — адрес картинки (/api/sales/product-photos/{File}/{размер}).</summary>
public sealed record ProductPhotoView(Guid Id, Guid File, int Width, int Height, bool Similar, string? SimilarFrom);

/// <summary>Фото товаров: CanEdit — может ли сотрудник их менять; Products — товар Linko → фото, первое — главное.</summary>
public sealed record ProductPhotosView(bool CanEdit, IReadOnlyDictionary<long, IReadOnlyList<ProductPhotoView>> Products);

/// <summary>Набор фото при сервере (SeedData/product-photos/manifest.json): код товара Linko → файлы; первое — главное.</summary>
public sealed record ProductPhotoSeedManifest(int[] Sizes, Dictionary<string, List<ProductPhotoSeedItem>> Photos);

/// <summary>Файл набора: имя без размера («248», «126_2»), размер самого крупного варианта, «похожее» и код товара, чьё это фото.</summary>
public sealed record ProductPhotoSeedItem(string File, int W, int H, bool Similar = false, string? From = null);

/// <summary>
/// Фото товаров «Продаж»: хранятся в хранилище файлов готовыми WebP (товар вписан целиком в квадрат 160 / 400 / 800 px — карточка телефона
/// грузит 20–50 КБ), адрес меняется с каждой загрузкой — браузер кэширует картинку навсегда. Видят все, у кого есть «Продажи»; менять — право
/// «Продажи: управление» (проверяет контроллер). Своя загрузка убирает «похожие» фото товара. Первичный набор — из папки при сервере, один
/// раз на код товара (SalesProductPhotoSeeds): удалённое вручную не возвращается. Изменения — в аудит (sales.product-photo.*).
/// </summary>
public sealed class SalesProductPhotoService(IAppDbContext db, IFileStorage storage, IImageProcessor images, IAuditLogger audit, ILogger<SalesProductPhotoService> logger)
{
    public const long MaxBytes = 15 * 1024 * 1024;

    /// <summary>Квадраты вариантов: таблица и обложка, карточка (телефон @2x), просмотр на весь экран.</summary>
    public static readonly int[] Sizes = [160, 400, 800];

    private const int Quality = 80;

    private static readonly HashSet<string> Types = ["image/jpeg", "image/png", "image/webp", "image/heic", "image/heif"];

    public static string Key(Guid fileId, int size) => $"sales/product-photos/{fileId:N}-{size}.webp";

    private static ProductPhotoView View(SalesProductPhoto p) => new(p.Id, p.FileId, p.Width, p.Height, p.Similar, p.SimilarFrom);

    /// <summary>Фото товаров (все или только перечисленных), по порядку: первое — главное.</summary>
    public async Task<IReadOnlyDictionary<long, IReadOnlyList<ProductPhotoView>>> ListAsync(IReadOnlyCollection<long>? productIds, CancellationToken ct)
    {
        var query = db.SalesProductPhotos.AsNoTracking();
        if (productIds is { Count: > 0 })
        {
            query = query.Where(p => productIds.Contains(p.ProductId));
        }

        var rows = await query.OrderBy(p => p.ProductId).ThenBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToListAsync(ct);
        return rows.GroupBy(p => p.ProductId).ToDictionary(g => g.Key, g => (IReadOnlyList<ProductPhotoView>)g.Select(View).ToList());
    }

    private async Task<IReadOnlyList<ProductPhotoView>> OfProductAsync(long productId, CancellationToken ct) =>
        (await db.SalesProductPhotos.AsNoTracking().Where(p => p.ProductId == productId).OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToListAsync(ct))
        .Select(View).ToList();

    /// <summary>Ключ файла нужного размера; размера нет или фото удалено — «не найдено».</summary>
    public async Task<string> KeyAsync(Guid fileId, int size, CancellationToken ct)
    {
        if (!Sizes.Contains(size))
        {
            throw new FieldNotFoundException("Такого размера фото нет.");
        }

        if (!await db.SalesProductPhotos.AnyAsync(p => p.FileId == fileId, ct))
        {
            throw new FieldNotFoundException("Фото нет.");
        }

        return Key(fileId, size);
    }

    /// <summary>
    /// Добавить фото товару или заменить одно из его фото (replace). JPEG, PNG, WebP или HEIC (если сервер его читает), до 15 МБ. Похожие фото
    /// товара при этом убираются: своё фото — главное, если своих ещё не было.
    /// </summary>
    public async Task<IReadOnlyList<ProductPhotoView>> UploadAsync(Guid userId, long productId, Stream content, long size, string? contentType, Guid? replace,
        CancellationToken ct)
    {
        if (!await db.LinkoProducts.AnyAsync(p => p.Id == productId, ct))
        {
            throw new FieldNotFoundException("Товар не найден.");
        }

        if (size <= 0)
        {
            throw new FieldValidationException("Файл пустой — выберите фото.");
        }

        if (size > MaxBytes)
        {
            throw new FieldValidationException("Фото — до 15 МБ.");
        }

        if (contentType is not null && contentType != "application/octet-stream" && !Types.Contains(contentType.ToLowerInvariant()))
        {
            throw new FieldValidationException("Фото — JPEG, PNG, WebP или HEIC.");
        }

        var data = new byte[size];
        await content.ReadExactlyAsync(data, ct);
        var fitted = images.FitToWebp(data, Sizes, Quality)
                     ?? throw new FieldValidationException("Не удалось прочитать фото. Сохраните его как JPEG или PNG и загрузите снова.");

        var photos = await db.SalesProductPhotos.Where(p => p.ProductId == productId).OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToListAsync(ct);
        var target = replace is { } id ? photos.FirstOrDefault(p => p.Id == id) ?? throw new FieldNotFoundException("Это фото уже удалили — обновите страницу.") : null;

        var fileId = Guid.CreateVersion7();
        var keys = await PutAsync(fileId, fitted.Variants.Select(v => (v.Box, v.Webp)), ct);

        // Похожие (чужие) фото больше не нужны, заменяемое — тоже; новое встаёт на место заменённого, иначе — главным (своих нет) или последним.
        var removed = photos.Where(p => p.Similar || p == target).ToList();
        var kept = photos.Except(removed).ToList();
        var position = target is { Similar: false } ? kept.Count(p => p.SortOrder < target.SortOrder) : kept.Count;
        var photo = new SalesProductPhoto
        {
            ProductId = productId,
            FileId = fileId,
            Width = fitted.Width,
            Height = fitted.Height,
            SizeBytes = fitted.Variants.Sum(v => (long)v.Webp.Length),
            Source = "upload",
            UploadedById = userId,
        };
        kept.Insert(Math.Min(position, kept.Count), photo);
        Renumber(kept);
        db.SalesProductPhotos.RemoveRange(removed);
        db.SalesProductPhotos.Add(photo);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await DeleteQuietlyAsync(keys);
            throw;
        }

        await DeleteQuietlyAsync(removed.SelectMany(p => Sizes.Select(s => Key(p.FileId, s))));
        await audit.LogAsync(ActorType.User, userId.ToString(), target is null ? "sales.product-photo.added" : "sales.product-photo.replaced", "product", productId.ToString(),
            new { photo = photo.Id, replaced = target?.Id, removedSimilar = removed.Count(p => p.Similar), bytes = photo.SizeBytes }, ct);
        return await OfProductAsync(productId, ct);
    }

    /// <summary>Удалить фото; остальные сдвигаются (первое — главное).</summary>
    public async Task<IReadOnlyList<ProductPhotoView>> DeleteAsync(Guid userId, Guid photoId, CancellationToken ct)
    {
        var photo = await db.SalesProductPhotos.FirstOrDefaultAsync(p => p.Id == photoId, ct) ?? throw new FieldNotFoundException("Это фото уже удалили — обновите страницу.");
        var rest = await db.SalesProductPhotos.Where(p => p.ProductId == photo.ProductId && p.Id != photoId).OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToListAsync(ct);
        db.SalesProductPhotos.Remove(photo);
        Renumber(rest);
        await db.SaveChangesAsync(ct);
        await DeleteQuietlyAsync(Sizes.Select(s => Key(photo.FileId, s)));
        await audit.LogAsync(ActorType.User, userId.ToString(), "sales.product-photo.removed", "product", photo.ProductId.ToString(), new { photo = photo.Id, photo.Similar }, ct);
        return await OfProductAsync(photo.ProductId, ct);
    }

    /// <summary>Сделать фото главным: оно встаёт первым, остальные — за ним в прежнем порядке.</summary>
    public async Task<IReadOnlyList<ProductPhotoView>> MakeMainAsync(Guid userId, Guid photoId, CancellationToken ct)
    {
        var photo = await db.SalesProductPhotos.FirstOrDefaultAsync(p => p.Id == photoId, ct) ?? throw new FieldNotFoundException("Это фото уже удалили — обновите страницу.");
        var all = await db.SalesProductPhotos.Where(p => p.ProductId == photo.ProductId).OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToListAsync(ct);
        Renumber([photo, .. all.Where(p => p.Id != photoId)]);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "sales.product-photo.main", "product", photo.ProductId.ToString(), new { photo = photo.Id }, ct);
        return await OfProductAsync(photo.ProductId, ct);
    }

    /// <summary>
    /// Первичный набор фото из папки при сервере: коды, которым набор ещё не ставили и чей товар уже есть в базе. Товар, у которого фото уже
    /// есть (загрузили вручную), набор не трогает. Возвращает, скольким товарам поставлены фото. Хранилище недоступно — исключение, код не
    /// отмечается (повторится при следующем запуске).
    /// </summary>
    public async Task<int> SeedAsync(string folder, CancellationToken ct)
    {
        var manifestFile = Path.Combine(folder, "manifest.json");
        if (!File.Exists(manifestFile))
        {
            return 0;
        }

        var manifest = JsonSerializer.Deserialize<ProductPhotoSeedManifest>(await File.ReadAllTextAsync(manifestFile, ct), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (manifest is null || manifest.Photos.Count == 0)
        {
            return 0;
        }

        var applied = (await db.SalesProductPhotoSeeds.AsNoTracking().Select(s => s.Code).ToListAsync(ct)).ToHashSet();
        var pending = manifest.Photos.Keys.Where(c => !applied.Contains(c)).ToList();
        if (pending.Count == 0)
        {
            return 0;
        }

        var products = await db.LinkoProducts.AsNoTracking().Where(p => p.Code != null && pending.Contains(p.Code)).Select(p => new { p.Id, Code = p.Code! }).ToListAsync(ct);
        var withPhotos = (await db.SalesProductPhotos.AsNoTracking().Select(p => p.ProductId).Distinct().ToListAsync(ct)).ToHashSet();
        var seeded = 0;
        foreach (var group in products.GroupBy(p => p.Code))
        {
            var items = manifest.Photos[group.Key];
            foreach (var product in group.Where(p => !withPhotos.Contains(p.Id)))
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var fileId = Guid.CreateVersion7();
                    var files = new List<(int, byte[])>();
                    foreach (var s in Sizes)
                    {
                        files.Add((s, await File.ReadAllBytesAsync(Path.Combine(folder, $"{item.File}-{s}.webp"), ct)));
                    }

                    await PutAsync(fileId, files, ct);
                    db.SalesProductPhotos.Add(new SalesProductPhoto
                    {
                        ProductId = product.Id,
                        SortOrder = i,
                        FileId = fileId,
                        Width = item.W,
                        Height = item.H,
                        SizeBytes = files.Sum(f => (long)f.Item2.Length),
                        Similar = item.Similar,
                        SimilarFrom = item.From,
                        Source = "seed",
                    });
                }

                seeded++;
            }

            db.SalesProductPhotoSeeds.Add(new SalesProductPhotoSeed { Code = group.Key, AppliedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct);
        }

        if (seeded > 0)
        {
            logger.LogInformation("Фото товаров: из набора при сервере поставлены {Count} товарам", seeded);
        }

        return seeded;
    }

    private async Task<string[]> PutAsync(Guid fileId, IEnumerable<(int Box, byte[] Data)> variants, CancellationToken ct)
    {
        var keys = new List<string>();
        try
        {
            foreach (var (box, data) in variants)
            {
                var key = Key(fileId, box);
                using var stream = new MemoryStream(data, writable: false);
                await storage.PutAsync(key, stream, data.Length, "image/webp", ct);
                keys.Add(key);
            }
        }
        catch
        {
            await DeleteQuietlyAsync(keys);
            throw;
        }

        return [.. keys];
    }

    private static void Renumber(IEnumerable<SalesProductPhoto> photos)
    {
        var i = 0;
        foreach (var p in photos)
        {
            p.SortOrder = i++;
        }
    }

    private async Task DeleteQuietlyAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await storage.DeleteAsync(key);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Фото товара: не удалось удалить файл {Key}", key);
            }
        }
    }
}
