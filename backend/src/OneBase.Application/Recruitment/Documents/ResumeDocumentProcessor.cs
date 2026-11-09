using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Recruitment.Jobs;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Outbox;
using OneBase.Application.Telegram.Routing;
using OneBase.Domain.Audit;
using OneBase.Domain.Recruitment;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Recruitment.Documents;

/// <summary>Вложение сообщения Telegram: откуда скачать и что о нём известно до скачивания.</summary>
public sealed record TelegramDocument(Guid AccountId, long PeerId, int MessageId, string FileName, string? MimeType, long Size);

public enum ReceiveOutcome
{
    Accepted,

    /// <summary>Не документ (стикер, голосовое, видео) — молча пропускаем.</summary>
    Ignored,

    /// <summary>Документ формата, который не читаем (Excel, архив, HEIC) — просим прислать PDF, Word или фото.</summary>
    Unsupported,

    TooBig,

    /// <summary>У кандидата уже MaxDocuments документов — исключение для HR.</summary>
    Limit,
}

public sealed record ReceiveResult(ReceiveOutcome Outcome, Guid? ResumeId);

/// <summary>
/// Документы кандидата (ResumeDocumentProcessor): оригинал сохраняется до любой обработки и никогда не теряется, разбор — в фоне, по очередям.
/// <list type="number">
/// <item>Получен (Telegram): запись о документе и задание hr.resume.download — ответ кандидату не ждёт скачивания.</item>
/// <item>Скачан → оригинал в MinIO (<c>recruitment/resumes/{кандидат}/{документ}</c>) → проверка содержимого по сигнатуре: не тот тип,
/// программа или скрипт — «отклонён», оригинал сохранён, обработки нет.</item>
/// <item>hr.resume.ocr: текст без AI (PDF, DOCX с таблицами, DOC, RTF, ODT, TXT); фото и сканы — OCR моделью (если включено и есть согласие
/// кандидата: распознавание — передача данных в Claude).</item>
/// <item>hr.resume.analysis: разбор резюме и сводка (<see cref="ResumeAnalysisJob"/>), дополнение карточки, оценка под вакансию.</item>
/// </list>
/// Тот же файл второй раз — тот же документ (оригинал один); другая версия — новый документ, прежние остаются.
/// </summary>
public sealed class ResumeDocumentProcessor(
    IAppDbContext db,
    IFileStorage storage,
    ITelegramGateway telegram,
    IResumeTextReader reader,
    IEnumerable<IDocumentVisionReader> vision,
    RecruitmentJobQueue jobs,
    RecruitmentExceptions exceptions,
    TelegramOutbox outbox,
    IAuditLogger audit,
    TimeProvider clock,
    ILogger<ResumeDocumentProcessor> logger)
{
    /// <summary>Документов у кандидата — не больше (все версии хранятся; дальше — HR разбирается, не спам ли это).</summary>
    public const int MaxDocuments = 50;

    /// <summary>Расширения документов, которые не читаем: на них просим PDF, Word или фото. Остальное (стикеры, голос, видео) пропускаем молча.</summary>
    private static readonly HashSet<string> OtherDocuments = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".rar", ".7z", ".pages", ".numbers", ".key", ".heic", ".heif", ".tif", ".tiff", ".bmp", ".gif", ".djvu",
        ".html", ".htm", ".xps", ".epub",
    };

    public static string ObjectKey(Guid candidateId, Guid documentId) => $"recruitment/resumes/{candidateId:N}/{documentId:N}";

    // ---------- Приём ----------

    /// <summary>Вложение из Telegram: запись «получен» и задание скачать — сразу, без ожидания; формат и размер проверяются по сведениям Telegram.</summary>
    public async Task<ReceiveResult> ReceiveTelegramAsync(Guid candidateId, TelegramDocument file, CancellationToken ct)
    {
        var name = SafeFileName(file.FileName);
        var extension = Path.GetExtension(name);
        if (name.StartsWith("sticker", StringComparison.OrdinalIgnoreCase) || file.MimeType is { } mime && (mime.StartsWith("audio/", StringComparison.Ordinal)
                || mime.StartsWith("video/", StringComparison.Ordinal) || mime == "application/x-tgsticker"))
        {
            return new ReceiveResult(ReceiveOutcome.Ignored, null);
        }

        if (!RecruitmentRules.ResumeExtensions.Contains(extension))
        {
            return new ReceiveResult(OtherDocuments.Contains(extension) ? ReceiveOutcome.Unsupported : ReceiveOutcome.Ignored, null);
        }

        if (file.Size > RecruitmentRules.MaxResumeBytes)
        {
            return new ReceiveResult(ReceiveOutcome.TooBig, null);
        }

        if (await db.CandidateResumes.CountAsync(r => r.CandidateId == candidateId, ct) >= MaxDocuments)
        {
            await exceptions.RaiseAsync("technical", $"Кандидат прислал больше {MaxDocuments} файлов — новые не сохраняются. Проверьте переписку.", candidateId,
                dedupKey: $"docs-limit:{candidateId:N}", ct: ct);
            return new ReceiveResult(ReceiveOutcome.Limit, null);
        }

        var resume = new CandidateResume
        {
            CandidateId = candidateId,
            ApplicationId = await OpenApplicationAsync(candidateId, ct),
            FileName = name,
            ContentType = Clip(file.MimeType, 200) ?? "application/octet-stream",
            SizeBytes = file.Size,
            ObjectKey = string.Empty,
            Source = "telegram",
            Status = DocumentStatus.Received,
            TelegramAccountId = file.AccountId,
            TelegramPeerId = file.PeerId,
            TelegramMessageId = file.MessageId,
        };
        resume.ObjectKey = ObjectKey(candidateId, resume.Id);
        db.CandidateResumes.Add(resume);
        await db.SaveChangesAsync(ct);
        await jobs.EnqueueAsync(RecruitmentQueues.Download, $"download:{resume.Id:N}", resume.Id, ct: ct);
        await audit.LogAsync(ActorType.System, "recruitment", "recruitment.document.received", nameof(Candidate), candidateId.ToString(),
            new { document = resume.Id, source = "telegram", resume.SizeBytes }, ct);
        return new ReceiveResult(ReceiveOutcome.Accepted, resume.Id);
    }

    /// <summary>
    /// Файл, загруженный HR или пришедший из HH: оригинал — сразу в хранилище, разбор — в фоне. Тот же файл у кандидата — конфликт (он уже есть).
    /// </summary>
    public async Task<Guid> UploadAsync(Guid candidateId, RecruitmentUpload upload, string source, Guid? actorId, CancellationToken ct)
    {
        if (!await db.Candidates.AnyAsync(c => c.Id == candidateId, ct))
        {
            throw new FieldNotFoundException("Кандидат не найден (возможно, его данные уже удалены).");
        }

        if (upload.SizeBytes <= 0)
        {
            throw new FieldValidationException("Файл пустой.");
        }

        if (upload.SizeBytes > RecruitmentRules.MaxResumeBytes)
        {
            throw new FieldValidationException($"Документ больше {RecruitmentRules.MaxResumeBytes / 1024 / 1024} МБ.");
        }

        var name = SafeFileName(upload.FileName);
        if (!RecruitmentRules.ResumeExtensions.Contains(Path.GetExtension(name)))
        {
            throw new FieldValidationException("Документ — PDF, Word (DOCX, DOC), RTF, ODT, TXT или фото (JPG, PNG, WEBP).");
        }

        if (await db.CandidateResumes.CountAsync(r => r.CandidateId == candidateId, ct) >= MaxDocuments)
        {
            throw new FieldValidationException($"У кандидата — до {MaxDocuments} документов. Удалите старые.");
        }

        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        if (buffer.Length > RecruitmentRules.MaxResumeBytes)
        {
            throw new FieldValidationException($"Документ больше {RecruitmentRules.MaxResumeBytes / 1024 / 1024} МБ.");
        }

        var bytes = buffer.ToArray();
        var sha = Sha(bytes);
        if (await db.CandidateResumes.AnyAsync(r => r.CandidateId == candidateId && r.Sha256 == sha, ct))
        {
            throw new FieldConflictException("Этот файл у кандидата уже есть.");
        }

        var resume = new CandidateResume
        {
            CandidateId = candidateId,
            ApplicationId = await OpenApplicationAsync(candidateId, ct),
            FileName = name,
            ContentType = "application/octet-stream",
            SizeBytes = bytes.Length,
            ObjectKey = string.Empty,
            Source = source,
            UploadedById = actorId,
            Status = DocumentStatus.Received,
        };
        resume.ObjectKey = ObjectKey(candidateId, resume.Id);
        db.CandidateResumes.Add(resume);
        var id = await StoreAsync(resume, bytes, ct);
        return id;
    }

    // ---------- Скачивание и сохранение оригинала ----------

    /// <summary>hr.resume.download: скачать из Telegram и сохранить. Telegram не отдал файл (ссылка пропала после перезапуска) — окончательная ошибка.</summary>
    public async Task DownloadAsync(Guid resumeId, CancellationToken ct)
    {
        var resume = await db.CandidateResumes.FirstOrDefaultAsync(r => r.Id == resumeId, ct);
        if (resume is not { Status: DocumentStatus.Received } || resume.TelegramAccountId is not { } account || resume.TelegramPeerId is not { } peer
            || resume.TelegramMessageId is not { } message)
        {
            return; // уже сохранён, удалён или не из Telegram
        }

        var bytes = await telegram.DownloadAsync(account, peer, message, ct)
                    ?? throw new RecruitmentJobFailure("Telegram не отдал файл (сервер перезапускался или сообщение удалено) — попросите кандидата прислать его ещё раз.");
        if (bytes.LongLength > RecruitmentRules.MaxResumeBytes)
        {
            throw new RecruitmentJobFailure($"Файл больше {RecruitmentRules.MaxResumeBytes / 1024 / 1024} МБ.");
        }

        await StoreAsync(resume, bytes, ct);
    }

    /// <summary>
    /// Оригинал — в хранилище до любой обработки; затем проверка содержимого. Тот же файл у кандидата уже есть — эта запись не нужна: сообщения
    /// переписки ссылаются на прежний документ (оригинал один, ничего не теряется).
    /// </summary>
    private async Task<Guid> StoreAsync(CandidateResume resume, byte[] bytes, CancellationToken ct)
    {
        var sha = Sha(bytes);
        var same = await db.CandidateResumes.AsNoTracking()
            .Where(r => r.CandidateId == resume.CandidateId && r.Sha256 == sha && r.Id != resume.Id).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
        if (same is { } existing)
        {
            await RelinkAsync(resume, existing, ct);
            return existing;
        }

        var check = FileSignatures.Check(bytes, resume.FileName);
        var contentType = check.Ok ? check.ContentType : "application/octet-stream";
        await storage.PutAsync(resume.ObjectKey, new MemoryStream(bytes, writable: false), bytes.LongLength, contentType, ct);

        resume.Sha256 = sha;
        resume.SizeBytes = bytes.LongLength;
        resume.ContentType = contentType;
        resume.DetectedType = check.Type;
        resume.UpdatedAt = clock.GetUtcNow();
        if (check.Ok)
        {
            resume.Status = DocumentStatus.Stored;
            resume.OcrStatus = DocumentStepStatus.Pending;
            resume.AnalysisStatus = DocumentStepStatus.Pending;
            resume.ProcessingError = check.HasMacros ? "В документе есть макросы — они не выполняются, читается только текст." : null;
        }
        else
        {
            resume.Status = DocumentStatus.Rejected;
            resume.OcrStatus = DocumentStepStatus.Skipped;
            resume.AnalysisStatus = DocumentStepStatus.Skipped;
            resume.ProcessingError = check.Problem;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Тот же файл сохранили одновременно (вторая доставка) — оригинал уже есть под другим документом.
            db.ChangeTracker.Clear();
            var other = await db.CandidateResumes.AsNoTracking().Where(r => r.CandidateId == resume.CandidateId && r.Sha256 == sha).Select(r => r.Id).FirstAsync(ct);
            var mine = await db.CandidateResumes.FirstOrDefaultAsync(r => r.Id == resume.Id, ct);
            if (mine is not null)
            {
                await RelinkAsync(mine, other, ct);
            }

            await storage.DeleteAsync(resume.ObjectKey, ct); // копия того же файла под ненужным ключом
            return other;
        }

        await audit.LogAsync(ActorType.System, "recruitment", "recruitment.document.stored", nameof(Candidate), resume.CandidateId.ToString(),
            new { document = resume.Id, resume.DetectedType, status = resume.Status.ToString() }, ct);
        if (check.Ok)
        {
            await jobs.EnqueueAsync(RecruitmentQueues.Ocr, $"ocr:{resume.Id:N}", resume.Id, ct: ct);
        }
        else
        {
            await exceptions.RaiseAsync("technical", $"Документ «{resume.FileName}» отклонён: {check.Problem}", resume.CandidateId, null, null,
                $"doc-rejected:{resume.Id:N}", ct);
            await NotifyCandidateAsync(resume, RecruiterTexts.FileUnsupported, $"doc-rejected:{resume.Id:N}", ct);
        }

        return resume.Id;
    }

    private async Task RelinkAsync(CandidateResume duplicate, Guid existing, CancellationToken ct)
    {
        await db.TelegramConversationMessages.Where(m => m.ResumeId == duplicate.Id).ExecuteUpdateAsync(u => u.SetProperty(m => m.ResumeId, existing), ct);
        await db.CandidateResumes.Where(r => r.Id == duplicate.Id).ExecuteDeleteAsync(ct);
        await audit.LogAsync(ActorType.System, "recruitment", "recruitment.document.same_file", nameof(Candidate), duplicate.CandidateId.ToString(),
            new { document = existing }, ct);
    }

    // ---------- Текст и OCR ----------

    /// <summary>
    /// hr.resume.ocr: текст без AI; нужен OCR (фото, скан) — модель, если «Распознавать фото и сканы» включено и кандидат дал согласие (или
    /// документ загрузил HR). Повреждённый файл — ошибка документа, оригинал остаётся, HR — исключение, кандидату — просьба прислать в другом
    /// формате. forceOcr — «Распознать заново (AI)»: модель читает и PDF с текстовым слоем.
    /// </summary>
    public async Task ExtractAsync(Guid resumeId, bool forceOcr, CancellationToken ct)
    {
        var resume = await db.CandidateResumes.FirstOrDefaultAsync(r => r.Id == resumeId, ct);
        if (resume is null || resume.Status is DocumentStatus.Received or DocumentStatus.Rejected || resume.DetectedType is null
            || resume.OcrStatus is not (DocumentStepStatus.Pending or DocumentStepStatus.WaitingConsent))
        {
            return;
        }

        resume.Status = DocumentStatus.Processing;
        await db.SaveChangesAsync(ct);
        var bytes = await DownloadOriginalAsync(resume, ct);
        var read = reader.Read(bytes, resume.DetectedType);
        resume.PageCount = read.Pages ?? resume.PageCount;
        if (read.Error is not null)
        {
            resume.OcrStatus = DocumentStepStatus.Failed;
            resume.AnalysisStatus = DocumentStepStatus.Skipped;
            resume.TextError = Clip(read.Error, 500);
            await FailAsync(resume, read.Error, ct);
            return;
        }

        var ai = vision.FirstOrDefault();
        if ((read.NeedsOcr || forceOcr) && ai is not null && ai.Supports("." + Extension(resume.DetectedType)))
        {
            var settings = await db.RecruitmentSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1, ct) ?? new RecruitmentSettings();
            if (!settings.ReadScans && !forceOcr)
            {
                resume.Text = Clip(read.Text, RecruitmentRules.MaxResumeText);
                resume.OcrStatus = DocumentStepStatus.Skipped;
                resume.TextError = read.Text is null ? "Фото и сканы не распознаются: включите «Распознавать фото и сканы» в настройках подбора." : null;
                await NextAsync(resume, ct);
                return;
            }

            if (!await MayUseAiAsync(resume, ct))
            {
                // Распознавание моделью — после согласия кандидата (ReleaseConsentAsync); текстовый слой, что есть, — уже сейчас.
                resume.Text ??= Clip(read.Text, RecruitmentRules.MaxResumeText);
                resume.OcrStatus = DocumentStepStatus.WaitingConsent;
                await db.SaveChangesAsync(ct);
                return;
            }

            var reading = await ai.ReadAsync(bytes, resume.FileName.EndsWith("." + Extension(resume.DetectedType), StringComparison.OrdinalIgnoreCase)
                ? resume.FileName
                : $"{Path.GetFileNameWithoutExtension(resume.FileName)}.{Extension(resume.DetectedType)}", "hr.resume.read", ct);
            resume.DocumentType = reading.Unavailable ? resume.DocumentType : reading.DocumentType; // модель не читала — тип не известен
            if (reading.Text is { Length: > 0 } recognized)
            {
                resume.Text = Clip(recognized, RecruitmentRules.MaxResumeText);
                resume.TextByAi = true;
                resume.TextError = null;
                resume.OcrStatus = DocumentStepStatus.Done;
            }
            else if (reading.DocumentType == "id_document")
            {
                // Паспорт и ID — текст не сохраняется и не разбирается.
                resume.Text = null;
                resume.OcrStatus = DocumentStepStatus.Done;
                resume.AnalysisStatus = DocumentStepStatus.NotNeeded;
                resume.TextError = reading.Error;
            }
            else if (reading.Unavailable)
            {
                // AI не настроен или платные вызовы выключены: файл цел, просто не распознан — без тревоги для HR («Распознать заново» потом).
                resume.Text = Clip(read.Text, RecruitmentRules.MaxResumeText);
                resume.OcrStatus = DocumentStepStatus.Skipped;
                resume.TextError = read.Text is null ? Clip($"{(resume.DetectedType == "pdf" ? "Скан PDF" : "Фото")} — распознавание недоступно: {reading.Error}", 500) : null;
            }
            else if (reading.Error is { } error && error.StartsWith("AI не смог", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(error); // сбой модели — повторим позже
            }
            else
            {
                resume.Text = Clip(read.Text, RecruitmentRules.MaxResumeText);
                resume.OcrStatus = read.Text is null ? DocumentStepStatus.Failed : DocumentStepStatus.Done;
                var what = resume.DetectedType == "pdf" ? "Скан PDF не распознан" : "Фото не распознано";
                resume.TextError = read.Text is null ? Clip($"{what}: {reading.Error ?? "AI не нашёл в файле текста."}", 500) : null;
                if (read.Text is null)
                {
                    resume.AnalysisStatus = DocumentStepStatus.Skipped;
                    await FailAsync(resume, $"Текст не распознан: {reading.Error}", ct);
                    return;
                }
            }
        }
        else if (read.NeedsOcr && read.Text is null)
        {
            resume.OcrStatus = DocumentStepStatus.Skipped;
            resume.AnalysisStatus = DocumentStepStatus.Skipped;
            resume.TextError = "Фото или скан — распознавание недоступно (AI не подключён).";
        }
        else
        {
            resume.Text = Clip(read.Text, RecruitmentRules.MaxResumeText);
            resume.TextByAi = false;
            resume.TextError = null;
            resume.OcrStatus = DocumentStepStatus.NotNeeded;
        }

        await NextAsync(resume, ct);
    }

    /// <summary>Текст есть — задание на разбор; нет — документ готов (что можно, сделано).</summary>
    private async Task NextAsync(CandidateResume resume, CancellationToken ct)
    {
        resume.UpdatedAt = clock.GetUtcNow();
        if (resume.Text is not null && resume.AnalysisStatus is DocumentStepStatus.Pending or DocumentStepStatus.WaitingConsent)
        {
            resume.AnalysisStatus = DocumentStepStatus.Pending;
            await db.SaveChangesAsync(ct);
            await jobs.EnqueueAsync(RecruitmentQueues.Analysis, $"analysis:{resume.Id:N}", resume.Id, ct: ct);
            return;
        }

        if (resume.AnalysisStatus == DocumentStepStatus.Pending)
        {
            resume.AnalysisStatus = DocumentStepStatus.Skipped;
        }

        resume.Status = DocumentStatus.Done;
        resume.ProcessedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Документ не обработан: статус «ошибка», причина — HR (исключение), кандидату из Telegram — просьба прислать иначе. Оригинал не трогается.</summary>
    public async Task FailAsync(CandidateResume resume, string reason, CancellationToken ct)
    {
        resume.Status = DocumentStatus.Failed;
        resume.ProcessingError = Clip(reason, 500);
        resume.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await exceptions.RaiseAsync("technical", $"Документ «{resume.FileName}» не обработан: {reason} Оригинал сохранён — его можно открыть в карточке кандидата.",
            resume.CandidateId, null, null, $"doc-failed:{resume.Id:N}", ct);
        await NotifyCandidateAsync(resume, resume.Sha256 is null ? RecruiterTexts.ResumeLost : RecruiterTexts.ResumeUnreadable, $"doc-failed:{resume.Id:N}", ct);
        await audit.LogAsync(ActorType.System, "recruitment", "recruitment.document.failed", nameof(Candidate), resume.CandidateId.ToString(),
            new { document = resume.Id, reason = Clip(reason, 200) }, ct);
    }

    /// <summary>Внешний AI читает данные кандидата: можно, если кандидат дал согласие или документ загрузил HR (решение HR).</summary>
    public async Task<bool> MayUseAiAsync(CandidateResume resume, CancellationToken ct) =>
        resume.Source == "manual" || await db.CandidateConsents.AnyAsync(c => c.CandidateId == resume.CandidateId && c.WithdrawnAt == null, ct);

    /// <summary>Кандидат дал согласие — документы, ждавшие его, распознаются и разбираются.</summary>
    public async Task ReleaseConsentAsync(Guid candidateId, CancellationToken ct)
    {
        var waiting = await db.CandidateResumes.AsNoTracking()
            .Where(r => r.CandidateId == candidateId && (r.OcrStatus == DocumentStepStatus.WaitingConsent || r.AnalysisStatus == DocumentStepStatus.WaitingConsent))
            .Select(r => new { r.Id, r.OcrStatus }).ToListAsync(ct);
        foreach (var r in waiting)
        {
            await (r.OcrStatus == DocumentStepStatus.WaitingConsent
                ? jobs.EnqueueAsync(RecruitmentQueues.Ocr, $"ocr:{r.Id:N}", r.Id, ct: ct)
                : jobs.EnqueueAsync(RecruitmentQueues.Analysis, $"analysis:{r.Id:N}", r.Id, ct: ct));
        }
    }

    // ---------- Повторная обработка и сверка ----------

    /// <summary>
    /// «Обработать заново» (HR): документ из Telegram, ещё не скачанный, — скачать снова; сохранённый — заново текст (forceOcr — моделью) и разбор.
    /// Отклонённый (не тот тип) не обрабатывается.
    /// </summary>
    public async Task ReprocessAsync(Guid resumeId, bool forceOcr, Guid actorId, CancellationToken ct)
    {
        var resume = await db.CandidateResumes.FirstOrDefaultAsync(r => r.Id == resumeId, ct) ?? throw new FieldNotFoundException("Документ не найден.");
        if (resume.Status == DocumentStatus.Rejected)
        {
            throw new FieldValidationException($"Документ отклонён: {resume.ProcessingError} Его можно только скачать.");
        }

        if (resume.Status == DocumentStatus.Received)
        {
            await jobs.EnqueueAsync(RecruitmentQueues.Download, $"download:{resume.Id:N}", resume.Id, ct: ct);
        }
        else
        {
            if (forceOcr && (vision.FirstOrDefault() is not { } ai || !ai.Supports("." + Extension(resume.DetectedType ?? string.Empty))))
            {
                throw new FieldValidationException("AI распознаёт только фото (JPG, PNG, WEBP) и PDF.");
            }

            resume.Status = DocumentStatus.Stored;
            resume.OcrStatus = DocumentStepStatus.Pending;
            resume.AnalysisStatus = resume.DocumentType == "id_document" && !forceOcr ? DocumentStepStatus.NotNeeded : DocumentStepStatus.Pending;
            resume.ProcessingError = null;
            resume.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            await jobs.EnqueueAsync(RecruitmentQueues.Ocr, $"ocr:{resume.Id:N}", resume.Id, forceOcr ? JsonSerializer.Serialize(new { forceOcr }) : null, ct: ct);
        }

        await audit.LogAsync(ActorType.User, actorId.ToString(), "recruitment.document.reprocess", nameof(Candidate), resume.CandidateId.ToString(),
            new { document = resume.Id, forceOcr }, ct);
    }

    /// <summary>
    /// Сверка (раз в минуту): документ застрял без задания (сервер упал между записью и постановкой в очередь) — задание ставится снова.
    /// Возвращает, сколько поставлено.
    /// </summary>
    public async Task<int> ReconcileAsync(CancellationToken ct)
    {
        var before = clock.GetUtcNow().AddMinutes(-2);
        var active = db.RecruitmentJobs.Where(j => j.Status == RecruitmentJobStatus.Pending || j.Status == RecruitmentJobStatus.Running);
        var stuck = await db.CandidateResumes.AsNoTracking()
            .Where(r => (r.UpdatedAt ?? r.CreatedAt) < before
                        && (r.Status == DocumentStatus.Received || r.Status == DocumentStatus.Stored
                            || (r.Status == DocumentStatus.Processing && (r.OcrStatus == DocumentStepStatus.Pending || r.AnalysisStatus == DocumentStepStatus.Pending)))
                        && !active.Any(j => j.RelatedId == r.Id)
                        && !db.RecruitmentJobs.Any(j => j.RelatedId == r.Id && j.Status == RecruitmentJobStatus.Failed && j.FinishedAt > before))
            .Select(r => new { r.Id, r.Status, r.OcrStatus }).Take(100).ToListAsync(ct);
        var queued = 0;
        foreach (var r in stuck)
        {
            var (queue, key) = r.Status == DocumentStatus.Received ? (RecruitmentQueues.Download, $"download:{r.Id:N}")
                : r.OcrStatus == DocumentStepStatus.Pending ? (RecruitmentQueues.Ocr, $"ocr:{r.Id:N}")
                : (RecruitmentQueues.Analysis, $"analysis:{r.Id:N}");
            if (await jobs.EnqueueAsync(queue, key, r.Id, ct: ct))
            {
                queued++;
            }
        }

        return queued;
    }

    // ---------- Общее ----------

    public async Task<byte[]> DownloadOriginalAsync(CandidateResume resume, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await storage.DownloadAsync(resume.ObjectKey, buffer, ct);
        return buffer.ToArray();
    }

    /// <summary>Сообщение кандидату о документе (на его языке) — в его диалог HR, если диалог ведёт AI; диалог ведёт человек — решает он.</summary>
    public async Task NotifyCandidateAsync(CandidateResume resume, Func<CandidateLang, string> text, string key, CancellationToken ct)
    {
        if (resume.TelegramAccountId is not { } account || resume.TelegramPeerId is not { } peer)
        {
            return;
        }

        var conversation = await db.TelegramConversations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.AccountId == account && c.PeerId == peer && c.Department == TelegramDepartments.Hr, ct);
        if (conversation is not { Mode: TelegramConversationMode.Ai })
        {
            return;
        }

        var candidate = await db.Candidates.AsNoTracking().Where(c => c.Id == resume.CandidateId).Select(c => new { c.Language, c.PreferredScript }).FirstOrDefaultAsync(ct);
        var lang = CandidateLanguages.Of(candidate?.Language, candidate?.PreferredScript) ?? CandidateLang.Ru;
        try
        {
            await outbox.EnqueueAsync(new OutboxRequest(account, peer, conversation.Id, TelegramDepartments.Hr, "document", text(lang), key, "ai"), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Подбор: сообщение кандидату о документе {Document} не поставлено в очередь", resume.Id);
        }
    }

    private async Task<Guid?> OpenApplicationAsync(Guid candidateId, CancellationToken ct) =>
        await db.CandidateApplications.AsNoTracking().Where(a => a.CandidateId == candidateId && a.ClosedAt == null).OrderByDescending(a => a.StageChangedAt)
            .Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);

    public static string Extension(string type) => type switch
    {
        "jpeg" => "jpg",
        _ => type,
    };

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string? Clip(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..max];

    public static string SafeFileName(string? name)
    {
        var file = Path.GetFileName(name?.Trim() ?? string.Empty);
        foreach (var bad in Path.GetInvalidFileNameChars())
        {
            file = file.Replace(bad, '_');
        }

        file = file.Length <= 200 ? file : file[^200..];
        return string.IsNullOrWhiteSpace(file) ? "document" : file;
    }
}

/// <summary>hr.resume.download — скачать вложение из Telegram и сохранить оригинал. Провал — документ «ошибка», HR и кандидату сообщается.</summary>
public sealed class ResumeDownloadJob(ResumeDocumentProcessor processor, IAppDbContext db) : IRecruitmentJobHandler
{
    public string Queue => RecruitmentQueues.Download;

    public Task RunAsync(RecruitmentJob job, CancellationToken ct) => processor.DownloadAsync(job.RelatedId, ct);

    public async Task OnFailedAsync(RecruitmentJob job, string error, CancellationToken ct)
    {
        if (await db.CandidateResumes.FirstOrDefaultAsync(r => r.Id == job.RelatedId, ct) is { Status: DocumentStatus.Received } resume)
        {
            await processor.FailAsync(resume, error, ct);
        }
    }
}

/// <summary>hr.resume.ocr — текст документа (текстовый слой или OCR моделью).</summary>
public sealed class ResumeOcrJob(ResumeDocumentProcessor processor, IAppDbContext db) : IRecruitmentJobHandler
{
    public string Queue => RecruitmentQueues.Ocr;

    public Task RunAsync(RecruitmentJob job, CancellationToken ct) =>
        processor.ExtractAsync(job.RelatedId, job.Payload?.Contains("\"forceOcr\":true", StringComparison.Ordinal) == true, ct);

    public async Task OnFailedAsync(RecruitmentJob job, string error, CancellationToken ct)
    {
        if (await db.CandidateResumes.FirstOrDefaultAsync(r => r.Id == job.RelatedId, ct) is { } resume && resume.Status != DocumentStatus.Done)
        {
            resume.OcrStatus = DocumentStepStatus.Failed;
            resume.AnalysisStatus = DocumentStepStatus.Skipped;
            await processor.FailAsync(resume, $"Текст не прочитан: {error}", ct);
        }
    }
}
