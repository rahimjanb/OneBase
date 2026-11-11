using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Procurement;
using OneBase.Domain.Procurement;
using OneBase.Domain.Production;

namespace OneBase.Application.Production;

/// <summary>Фото продукта в ответах: FileId — адрес картинки (/api/production/recipe-photos/{FileId}/{размер}), размер кадра и средний цвет.</summary>
public sealed record ProductionRecipePhotoView(Guid Id, int Width, int Height, string? Tint);

/// <summary>
/// Фото продукта техкарты: одно на код — у всех версий (и у новых, и у созданных импортом) одно фото. Загружает и удаляет тот, кто ведёт
/// техкарты (право «Производство: рецептуры»), в любом статусе версии: фото не меняет технологию, утверждения не требует. Видят — все, кто
/// видит техкарты. Сервер сам вырезает центр 4:3 и готовит три размера JPEG (1600, 960, 480 px), EXIF не сохраняется. Каждое изменение —
/// в истории той версии, через которую его сделали.
/// </summary>
public sealed class ProductionRecipePhotoService(IAppDbContext db, IFileStorage storage, IImageProcessor images)
{
    public const long MaxBytes = 10 * 1024 * 1024;

    /// <summary>Ширины вариантов: оригинал (форма техкарты), превью карточки на компьютере и @2x телефона, превью @1x.</summary>
    public static readonly int[] Widths = [1600, 960, 480];

    private static readonly HashSet<string> Types = ["image/jpeg", "image/png", "image/webp", "image/heic", "image/heif"];

    public static void RequireView(ProcurementContext ctx)
    {
        if (!ctx.SeesProduction)
        {
            throw new FieldForbiddenException("Рецептуры видят производство, технолог, кладовщик, снабжение и директор.");
        }
    }

    private static void RequireEdit(ProcurementContext ctx)
    {
        if (!ctx.Recipes)
        {
            throw new FieldForbiddenException("Фото техкарты меняет технолог — нужно право «Производство: рецептуры».");
        }
    }

    public static ProductionRecipePhotoView View(ProductionRecipePhoto p) => new(p.FileId, p.Width, p.Height, p.Tint);

    /// <summary>
    /// Загрузить или заменить фото продукта техкарты (через любую её версию). Тип — JPEG, PNG, WebP или HEIC (если сервер его читает), до 10 МБ.
    /// Прежние файлы удаляются после сохранения ссылки на новые.
    /// </summary>
    public async Task<ProductionRecipePhotoView> SaveAsync(ProcurementContext ctx, Guid recipeId, Stream content, long size, string? contentType, CancellationToken ct)
    {
        RequireEdit(ctx);
        var recipe = await db.ProductionRecipes.AsNoTracking().Where(r => r.Id == recipeId).Select(r => new { r.Id, r.Code, r.VersionNo }).FirstOrDefaultAsync(ct)
                     ?? throw new FieldNotFoundException("Рецептура не найдена.");
        if (size <= 0)
        {
            throw new FieldValidationException("Файл пустой — выберите фото.");
        }

        if (size > MaxBytes)
        {
            throw new FieldValidationException("Фото — до 10 МБ.");
        }

        if (contentType is not null && contentType != "application/octet-stream" && !Types.Contains(contentType.ToLowerInvariant()))
        {
            throw new FieldValidationException("Фото — JPEG, PNG, WebP или HEIC.");
        }

        var data = new byte[size];
        await content.ReadExactlyAsync(data, ct);
        var processed = images.CropToJpeg(data, 4, 3, Widths, 85)
                        ?? throw new FieldValidationException("Не удалось прочитать фото. Сохраните его как JPEG или PNG и загрузите снова.");

        var fileId = Guid.CreateVersion7();
        var prefix = $"production/recipe-photos/{fileId:N}";
        var keys = new string[processed.Variants.Count];
        for (var i = 0; i < processed.Variants.Count; i++)
        {
            var variant = processed.Variants[i];
            keys[i] = $"{prefix}-{Widths[i]}.jpg";
            using var stream = new MemoryStream(variant.Jpeg, writable: false);
            await storage.PutAsync(keys[i], stream, variant.Jpeg.Length, "image/jpeg", ct);
        }

        var photo = await db.ProductionRecipePhotos.FirstOrDefaultAsync(p => p.Code == recipe.Code, ct);
        var old = photo is null ? [] : new[] { photo.ObjectKey, photo.ThumbKey, photo.SmallKey };
        if (photo is null)
        {
            photo = new ProductionRecipePhoto { Code = recipe.Code, ObjectKey = keys[0], ThumbKey = keys[1], SmallKey = keys[2] };
            db.ProductionRecipePhotos.Add(photo);
        }
        else
        {
            photo.ObjectKey = keys[0];
            photo.ThumbKey = keys[1];
            photo.SmallKey = keys[2];
            photo.UpdatedAt = DateTimeOffset.UtcNow;
        }

        photo.FileId = fileId;
        photo.Width = processed.Width;
        photo.Height = processed.Height;
        photo.SizeBytes = processed.Variants[0].Jpeg.Length;
        photo.Tint = processed.Tint;
        photo.UploadedById = ctx.UserId;
        Log(ctx, recipe.Id, old.Length == 0 ? "photo.added" : "photo.replaced", old.Length == 0 ? "Добавлено фото продукта" : "Фото продукта заменено");
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is System.Data.Common.DbException { SqlState: "23505" })
        {
            await DeleteQuietlyAsync(keys);
            throw new FieldConflictException("Фото этой техкарты как раз меняют в другом окне — обновите страницу.");
        }

        await DeleteQuietlyAsync(old);
        return View(photo);
    }

    /// <summary>Удалить фото продукта техкарты (у всех версий).</summary>
    public async Task DeleteAsync(ProcurementContext ctx, Guid recipeId, CancellationToken ct)
    {
        RequireEdit(ctx);
        var recipe = await db.ProductionRecipes.AsNoTracking().Where(r => r.Id == recipeId).Select(r => new { r.Id, r.Code }).FirstOrDefaultAsync(ct)
                     ?? throw new FieldNotFoundException("Рецептура не найдена.");
        var photo = await db.ProductionRecipePhotos.FirstOrDefaultAsync(p => p.Code == recipe.Code, ct);
        if (photo is null)
        {
            return;
        }

        db.ProductionRecipePhotos.Remove(photo);
        Log(ctx, recipe.Id, "photo.removed", "Фото продукта удалено");
        await db.SaveChangesAsync(ct);
        await DeleteQuietlyAsync([photo.ObjectKey, photo.ThumbKey, photo.SmallKey]);
    }

    /// <summary>Ключ файла нужного размера: 1600 — оригинал, 960, 480 — превью; другой размер или нет фото — «не найдено».</summary>
    public async Task<string> KeyAsync(ProcurementContext ctx, Guid fileId, int width, CancellationToken ct)
    {
        RequireView(ctx);
        var photo = await db.ProductionRecipePhotos.AsNoTracking().FirstOrDefaultAsync(p => p.FileId == fileId, ct) ?? throw new FieldNotFoundException("Фото нет.");
        return width switch
        {
            1600 => photo.ObjectKey,
            960 => photo.ThumbKey,
            480 => photo.SmallKey,
            _ => throw new FieldNotFoundException("Такого размера фото нет."),
        };
    }

    private async Task DeleteQuietlyAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception)
            {
                // Старый файл не удалился — не страшно: ссылки на него в базе уже нет.
            }
        }
    }

    private void Log(ProcurementContext ctx, Guid id, string action, string text) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog { DocType = ProductionRules.RecipeDoc, DocId = id, ActorId = ctx.UserId, Action = action, Text = text });
}
