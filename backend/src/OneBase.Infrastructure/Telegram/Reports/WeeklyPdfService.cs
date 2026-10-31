using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram;
using OneBase.Application.Telegram.Reports;
using OneBase.Application.Telegram.Weekly;
using OneBase.Domain.Telegram;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Еженедельный PDF в Telegram: отчёт области (WeeklyReportBuilder) → PDF (страницы РОП / РМ / СВ, карточки ТП) → хранилище файлов → документ с
/// подписью через аккаунт автоотчётов (TelegramReportSender: те же повторы, FLOOD_WAIT и «неизвестно, дошёл ли»). «Только при полных данных» —
/// неполный отчёт по расписанию и вручную не уходит («пропущен» с причиной), тест — уходит с пометкой. Этап и подробный лог — в строке журнала;
/// файлы остаются для «скачать» и «повторить».
/// </summary>
public sealed partial class WeeklyPdfService(
    IAppDbContext db,
    WeeklyReportBuilder builder,
    IWeeklyPdfRenderer renderer,
    IFileStorage storage,
    TelegramReportSender sender,
    TimeProvider clock,
    ILogger<WeeklyPdfService> logger)
{
    public const string StageCollecting = "collecting";
    public const string StageGenerating = "generating";
    public const string StageReady = "ready";
    public const string StageSent = "sent";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Файл отправки в хранилище: что за PDF, имя для Telegram и скачивания, ключ, страниц, байт.</summary>
    public sealed record StoredFile(string Kind, string Name, string Key, int Pages, long Bytes);

    /// <summary>Готовый PDF до сохранения.</summary>
    public sealed record PdfFile(string Kind, string Name, byte[] Content, int Pages);

    public static IReadOnlyList<StoredFile> FilesOf(TelegramReportDelivery d) =>
        string.IsNullOrEmpty(d.Files) ? [] : JsonSerializer.Deserialize<List<StoredFile>>(d.Files, Json) ?? [];

    /// <summary>По какой день строить: по расписанию и вручную — последняя полная неделя пн–сб дня отправки; тест — выбранный день.</summary>
    public static DateOnly Through(TelegramReportDelivery d) => d.Trigger == "test" ? d.Period : WeeklyReportMath.ScheduledThrough(d.Period);

    /// <summary>Файлы отчёта: ТП — карточка; СВ — страницы команды (и карточки ТП, если включено); РМ и РОП — страницы области.</summary>
    public IReadOnlyList<PdfFile> Render(WeeklySupervisorReport report, string language, bool cards)
    {
        var date = report.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var name = SafeName(report.ScopeName);
        var files = new List<PdfFile>();
        if (report.Role != TelegramReportRoles.Tp)
        {
            var pdf = renderer.Render(report, language);
            files.Add(new PdfFile(WeeklyPdfKinds.Team, $"weekly-{report.Role}-{name}-{date}.pdf", pdf, Pages(pdf)));
        }

        if (report.Role == TelegramReportRoles.Tp || (cards && report.Role == TelegramReportRoles.Sv))
        {
            var pdf = renderer.RenderCards(report, language);
            files.Add(new PdfFile(WeeklyPdfKinds.Cards, $"weekly-cards-{name}-{date}.pdf", pdf, Pages(pdf)));
        }

        return files;
    }

    /// <summary>Страниц в PDF: объекты «/Type /Page» (не «/Pages»).</summary>
    public static int Pages(byte[] pdf) => PageObject().Matches(Encoding.Latin1.GetString(pdf)).Count;

    [GeneratedRegex(@"/Type\s*/Page\b")]
    private static partial Regex PageObject();

    private static string SafeName(string name) => string.Concat(name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch is ' ' or ':' ? '-' : ch));

    /// <summary>Язык PDF получателя (пусто — язык автоотчётов), настройки PDF.</summary>
    private async Task<(TelegramWeeklyPdfSettings Pdf, string Language)> SettingsAsync(TelegramReportDelivery delivery, string? language, CancellationToken ct)
    {
        var pdf = await db.TelegramWeeklyPdfSettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new TelegramWeeklyPdfSettings();
        if (language is null && delivery.SubscriptionId is { } id)
        {
            language = await db.TelegramReportSubscriptions.AsNoTracking().Where(s => s.Id == id).Select(s => s.PdfLanguage).FirstOrDefaultAsync(ct);
        }

        language ??= await db.TelegramReportSettings.AsNoTracking().Select(s => s.Language).FirstOrDefaultAsync(ct) ?? "ru";
        return (pdf, OneBase.Application.Abstractions.UiLanguage.Normalize(language) ?? "ru");
    }

    /// <summary>
    /// Одна попытка отправки: сбор данных → проверка полноты → PDF → хранилище → Telegram (подпись + документы). language — для теста (иначе —
    /// язык получателя). Область — из ключа строки: права и состав проверяет сборка отчёта по данным «Продаж».
    /// </summary>
    public Task<ReportSendResult> AttemptAsync(TelegramReportDelivery delivery, TelegramChat chat, ReportScope scope, bool retry, CancellationToken ct, bool test = false,
        string? language = null) =>
        sender.AttemptFilesAsync(delivery, chat, retry, ct, async token =>
        {
            var (pdf, lang) = await SettingsAsync(delivery, language, token);
            var (report, files) = await BuildFilesAsync(delivery, scope, pdf, lang, token);
            var incomplete = report.Incomplete;
            if (incomplete.Count > 0 && pdf.RequireCompleteData && !test)
            {
                var reason = $"Данные неполные: {string.Join(", ", incomplete.Select(k => WeeklyTexts.Source(k, "ru")))} — включено «только при полных данных».";
                AddLog(delivery, reason);
                return ReportFilesOutgoing.Skip(reason, Scope(report), Summary(report));
            }

            var outgoing = files.Select(f => new TelegramFile(f.Name, "application/pdf", f.Content)).ToList();
            delivery.Stage = StageReady;
            AddLog(delivery, $"Отправка в «{chat.Title}»: {files.Count} файл(а).");
            return new ReportFilesOutgoing(null, Scope(report), Summary(report), WeeklyTexts.Caption(report, lang, files.Count > 1, test), outgoing,
                report.DataAsOf, $"{WeeklyTexts.Caption(report, "ru", files.Count > 1, test)}", $"Еженедельный PDF · {Scope(report)}");
        }, onSent: d =>
        {
            d.Stage = StageSent;
            AddLog(d, $"Отправлено: сообщения {d.MessageIds}.");
        });

    /// <summary>Собрать отчёт и PDF строки журнала, сохранить файлы в хранилище (этап и лог — в строке).</summary>
    private async Task<(WeeklySupervisorReport Report, IReadOnlyList<PdfFile> Files)> BuildFilesAsync(TelegramReportDelivery delivery, ReportScope scope,
        TelegramWeeklyPdfSettings pdf, string language, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        delivery.Stage = StageCollecting;
        AddLog(delivery, $"Сбор данных: неделя по {Through(delivery):dd.MM.yyyy}, язык {language}.");
        await db.SaveChangesAsync(ct);

        var report = await builder.BuildAsync(scope, Through(delivery), pdf.IncludeMoney, ct);
        var incomplete = report.Incomplete;
        AddLog(delivery, $"Данные собраны за {watch.Elapsed.TotalSeconds:0.0} с: неделя {report.PeriodStart:dd.MM}–{report.PeriodEnd:dd.MM}, данные по {report.DataThrough:dd.MM}"
                         + (incomplete.Count > 0 ? $", неполные: {string.Join(", ", incomplete.Select(k => WeeklyTexts.Source(k, "ru")))}." : ", полные."));

        delivery.Stage = StageGenerating;
        watch.Restart();
        var files = Render(report, language, pdf.SvTpCards);
        var stored = new List<StoredFile>();
        foreach (var file in files)
        {
            var key = $"telegram/weekly-pdf/{delivery.Period:yyyy-MM-dd}/{delivery.Id:N}/{file.Kind}.pdf";
            using var stream = new MemoryStream(file.Content);
            await storage.PutAsync(key, stream, file.Content.Length, "application/pdf", ct);
            stored.Add(new StoredFile(file.Kind, file.Name, key, file.Pages, file.Content.Length));
        }

        delivery.Files = JsonSerializer.Serialize(stored, Json);
        delivery.Pages = files.Sum(f => f.Pages);
        delivery.Bytes = files.Sum(f => (long)f.Content.Length);
        AddLog(delivery, $"PDF за {watch.Elapsed.TotalSeconds:0.0} с: " + string.Join("; ", files.Select(f => $"{(f.Kind == WeeklyPdfKinds.Cards ? "карточки ТП" : "отчёт")} — {f.Pages} стр., {f.Content.Length / 1024} КБ"))
                         + "; сохранены в хранилище.");
        await db.SaveChangesAsync(ct);
        return (report, files);
    }

    /// <summary>«Сформировать заново»: те же область, неделя и язык — новые PDF в хранилище (прежние заменяются), без отправки.</summary>
    public async Task<IReadOnlyList<StoredFile>> RebuildAsync(Guid deliveryId, CancellationToken ct)
    {
        var delivery = await db.TelegramReportDeliveries.FirstOrDefaultAsync(d => d.Id == deliveryId, ct) ?? throw new KeyNotFoundException();
        if (!ReportCatalog.IsPdf(delivery.Report) || ReportScope.Parse(delivery.ScopeKey) is not { } scope)
        {
            throw new ReportValidationException("Сформировать заново можно только еженедельный PDF.");
        }

        if (delivery.Status is TelegramReportSender.Pending or TelegramReportSender.Sending)
        {
            throw new ReportConflictException("PDF сейчас отправляется — дождитесь результата.");
        }

        var stage = delivery.Stage;
        var (pdf, lang) = await SettingsAsync(delivery, null, ct);
        AddLog(delivery, "Сформировать заново (без отправки).");
        await BuildFilesAsync(delivery, scope, pdf, lang, ct);
        delivery.Stage = stage;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Еженедельный PDF {Delivery} сформирован заново", deliveryId);
        return FilesOf(delivery);
    }

    /// <summary>Файл отправки из хранилища — «скачать PDF» в истории.</summary>
    public async Task<(byte[] Content, string Name)> DownloadAsync(Guid deliveryId, int index, CancellationToken ct)
    {
        var delivery = await db.TelegramReportDeliveries.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deliveryId, ct) ?? throw new KeyNotFoundException();
        var file = FilesOf(delivery).ElementAtOrDefault(index) ?? throw new KeyNotFoundException("PDF этой отправки не сохранён — сформируйте заново.");
        using var buffer = new MemoryStream();
        await storage.DownloadAsync(file.Key, buffer, ct);
        return (buffer.ToArray(), file.Name);
    }

    private static string Scope(WeeklySupervisorReport r) => r.Role switch
    {
        "rop" => $"РОП · {r.ScopeName}",
        TelegramReportRoles.Rm => $"РМ · {r.ScopeName}",
        TelegramReportRoles.Tp => $"ТП · {r.ScopeName}{(r.RegionName is { } region ? $" · {region}" : "")}",
        _ => $"СВ · {r.ScopeName}{(r.SupervisorName is { } sv ? $" · {sv}" : "")}",
    };

    private static string Summary(WeeklySupervisorReport r) => r.Role == TelegramReportRoles.Tp && r.EmployeeMetrics.FirstOrDefault() is { } e
        ? $"Неделя {r.PeriodStart:dd.MM}–{r.PeriodEnd:dd.MM}: {ReportFormat.Number(e.WeekKg)} кг · месяц {ReportFormat.Percent(e.Execution)} плана"
        : $"Неделя {r.PeriodStart:dd.MM}–{r.PeriodEnd:dd.MM}: {ReportFormat.Number(r.SalesMetrics.WeekKg)} кг · месяц {ReportFormat.Percent(r.SalesMetrics.Execution)} плана";

    /// <summary>Строка лога с местным временем; лог — последние 4000 знаков.</summary>
    private void AddLog(TelegramReportDelivery d, string line)
    {
        var at = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(5)).ToString("dd.MM HH:mm:ss", CultureInfo.InvariantCulture);
        var log = string.IsNullOrEmpty(d.Log) ? $"{at} {line}" : $"{d.Log}\n{at} {line}";
        d.Log = log.Length > 4000 ? log[^4000..] : log;
    }
}
