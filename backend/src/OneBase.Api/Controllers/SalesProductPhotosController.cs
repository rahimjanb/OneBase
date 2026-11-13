using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Sales;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

/// <summary>
/// Фото товаров «Продаж»: список по товарам, картинки (WebP 160 / 400 / 800 px, адрес меняется с каждой загрузкой — кэш навсегда), загрузка,
/// замена, удаление и «главное». Видят все с «Продажами»; меняет право «Продажи: управление».
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/sales")]
[HasPermission(Permissions.SalesRead)]
public sealed class SalesProductPhotosController(SalesProductPhotoService photos, IFileStorage storage) : ControllerBase
{
    private bool CanEdit => User.FindAll(OneBaseClaims.Permission).Any(c => c.Value == Permissions.SalesManage);

    /// <summary>Фото товаров; ids — только этих (через запятую), без него — всех.</summary>
    [HttpGet("product-photos")]
    public async Task<ProductPhotosView> List([FromQuery] string? ids, CancellationToken ct)
    {
        var list = ids?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var id) ? id : (long?)null).Where(id => id is not null).Select(id => id!.Value).Distinct().Take(2000).ToList();
        return new ProductPhotosView(CanEdit, await photos.ListAsync(list, ct));
    }

    [HttpGet("product-photos/{fileId:guid}/{size:int}")]
    public async Task<IActionResult> File(Guid fileId, int size, CancellationToken ct)
    {
        var key = await photos.KeyAsync(fileId, size, ct);
        var buffer = new MemoryStream();
        await storage.DownloadAsync(key, buffer, ct);
        buffer.Position = 0;
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        return File(buffer, "image/webp");
    }

    /// <summary>Добавить фото товару; replace — какое фото заменить.</summary>
    [HttpPost("products/{productId:long}/photos")]
    [HasPermission(Permissions.SalesManage)]
    [RequestSizeLimit(SalesProductPhotoService.MaxBytes + 256 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = SalesProductPhotoService.MaxBytes + 256 * 1024)]
    public async Task<IReadOnlyList<ProductPhotoView>> Upload(long productId, IFormFile? file, [FromForm] Guid? replace, CancellationToken ct)
    {
        if (file is null)
        {
            throw new FieldValidationException("Выберите фото.");
        }

        await using var stream = file.OpenReadStream();
        return await photos.UploadAsync(User.GetUserId(), productId, stream, file.Length, file.ContentType, replace, ct);
    }

    [HttpDelete("product-photos/{id:guid}")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IReadOnlyList<ProductPhotoView>> Delete(Guid id, CancellationToken ct) => await photos.DeleteAsync(User.GetUserId(), id, ct);

    [HttpPost("product-photos/{id:guid}/main")]
    [HasPermission(Permissions.SalesManage)]
    public async Task<IReadOnlyList<ProductPhotoView>> Main(Guid id, CancellationToken ct) => await photos.MakeMainAsync(User.GetUserId(), id, ct);
}
