using OneBase.Application.Abstractions;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram;

/// <summary>
/// Материалы, отправленные в чат через OneBase (текст рассылки, файлы, отчёты), — единственный источник ответов в режиме «Только
/// отправленные данные». Текст режется на фрагменты с ключом аккаунт × чат: поиск по ним не видит других чатов. Файл, из которого
/// текст не извлечь, записывается без фрагментов — агент по нему не отвечает.
/// </summary>
public sealed class TelegramMaterialService(IAppDbContext db, IDocumentTextExtractor extractor)
{
    /// <summary>Больше этого текста из одного материала не берём (фрагменты — для поиска, не архив).</summary>
    public const int MaxTextLength = 200_000;

    public Task<TelegramMaterial> RecordTextAsync(Guid accountId, long peerId, string kind, string title, string text, Guid? campaignId, Guid? userId,
        CancellationToken ct = default) =>
        RecordAsync(accountId, peerId, kind, title, null, null, null, text, campaignId, userId, ct);

    /// <summary>Файл: текст извлекается, если формат поддерживается (pdf, docx, xlsx, csv, txt…).</summary>
    public async Task<TelegramMaterial> RecordFileAsync(Guid accountId, long peerId, string kind, string title, string fileName, string? objectKey,
        byte[] content, Guid? campaignId, Guid? userId, CancellationToken ct = default) =>
        await RecordAsync(accountId, peerId, kind, title, fileName, objectKey, content.LongLength, await ExtractAsync(fileName, content, ct), campaignId, userId, ct);

    /// <summary>Текст файла (null — формат не читается). Для рассылки — один раз на файл, а не на каждого получателя.</summary>
    public async Task<string?> ExtractAsync(string fileName, byte[] content, CancellationToken ct = default)
    {
        if (!extractor.Extensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            return await extractor.ExtractAsync(stream, fileName, ct);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Материал с уже извлечённым текстом (text null — файл есть, но отвечать по нему нельзя).</summary>
    public async Task<TelegramMaterial> RecordAsync(Guid accountId, long peerId, string kind, string title, string? fileName, string? objectKey, long? size,
        string? text, Guid? campaignId, Guid? userId, CancellationToken ct = default)
    {
        var material = new TelegramMaterial
        {
            AccountId = accountId,
            PeerId = peerId,
            Kind = kind,
            Title = title.Length > 300 ? title[..300] : title,
            FileName = fileName,
            ObjectKey = objectKey,
            SizeBytes = size,
            CampaignId = campaignId,
            CreatedById = userId,
        };
        db.TelegramMaterials.Add(material);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var chunks = TelegramMaterials.Chunks(text.Length > MaxTextLength ? text[..MaxTextLength] : text);
            for (var i = 0; i < chunks.Count; i++)
            {
                db.TelegramMaterialChunks.Add(new TelegramMaterialChunk
                {
                    MaterialId = material.Id,
                    AccountId = accountId,
                    PeerId = peerId,
                    Position = i,
                    Text = chunks[i],
                });
            }

            material.HasText = chunks.Count > 0;
        }

        await db.SaveChangesAsync(ct);
        return material;
    }
}
