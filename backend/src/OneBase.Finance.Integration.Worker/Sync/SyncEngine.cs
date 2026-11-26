using System.Diagnostics;
using System.Text.Json;
using OneBase.Finance.Integration.Contracts;
using OneBase.Finance.Integration.Worker.Api;
using OneBase.Finance.Integration.Worker.Normalization;
using OneBase.Finance.Integration.Worker.OneC;

namespace OneBase.Finance.Integration.Worker.Sync;

/// <summary>Итог запуска для журнала службы и ответа на команду администратора.</summary>
public sealed record SyncOutcome(string Status, string Message, Guid? RunId = null, IReadOnlyList<RunStep>? Steps = null, object? Details = null);

/// <summary>
/// Синхронизация 1С → OneBase. Порядок обмена: проверка соединения и авторизации (ping) → запуск в OneBase (не больше одного; другая база 1С —
/// отказ) → справочники, которые ещё не загружены → изменения 1С с последнего курсора → курсы за неделю → остатки за месяцы с изменениями →
/// повтор неуспешных операций. Курсор потока сдвигается только вместе с записанным пакетом (в одной транзакции OneBase), поэтому сбой на любом
/// шаге ничего не теряет: следующий запуск продолжит с последнего записанного пакета. Пауза администратора останавливает запуск на границе пакета.
/// </summary>
public sealed class SyncEngine(
    IOneCDataProvider provider,
    IOneBaseIntegrationApi api,
    OneCNormalizer normalizer,
    RetryPolicy retry,
    IntegrationOptions options,
    TimeProvider clock,
    ILogger<SyncEngine> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan Tashkent = TimeSpan.FromHours(5);

    public Guid? CurrentRunId { get; private set; }

    /// <summary>Запуск закрыт в OneBase или пауза — завершить на границе пакета.</summary>
    private sealed class StopException(string reason) : Exception(reason);

    private sealed class Run
    {
        public Guid Id { get; init; }
        public required string Kind { get; init; }
        public int BatchSize { get; init; }
        public bool DryRun { get; init; }
        public Dictionary<(string Stream, string Org), string?> Checkpoints { get; } = [];

        /// <summary>Изменения 1С — сверкой версий (/versions), а не планом обмена.</summary>
        public bool ByVersions { get; init; }

        /// <summary>Организации, выбранные на странице «1С»; пусто — все.</summary>
        public IReadOnlyList<string> OrganizationFilter { get; init; } = [];

        /// <summary>Модули запуска — какие типы документов загружаются (OneCModules); пусто — все.</summary>
        public IReadOnlyList<string> Modules { get; init; } = [];

        /// <summary>
        /// Модуль «Бухгалтерия» включён на странице «1С»: проводки загружаемых документов и оборотно-сальдовая ведомость. Не зависит от
        /// «синхронизировать модуль» — платежи без своих проводок не загружаются.
        /// </summary>
        public bool Accounting { get; init; } = true;

        /// <summary>Типы документов включённых модулей; null — все.</summary>
        public IReadOnlyList<string>? DocumentTypes => Modules.Count == 0 ? null : Modules.SelectMany(OneCModules.TypesOf).ToList();

        public bool Allows(string? documentType) => OneCModules.Allows(Modules, documentType);
        public List<RunStep> Steps { get; } = [];
        public Dictionary<string, DateOnly> Earliest { get; } = [];
        public List<ImportRejection> Rejections { get; } = [];
        public int Batches;
        public int Errors;
        public int Created, Updated, Unchanged, Deleted, Rejected;

        public string? Cursor(string stream, string org = "") => Checkpoints.GetValueOrDefault((stream, org));
    }

    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(Tashkent).DateTime);

    // ---------- Запуск ----------

    /// <summary>Проверка 1С и начало запуска в OneBase. null — запуск не начат (outcome объясняет почему).</summary>
    private async Task<(Run? Run, SyncOutcome? Outcome)> BeginAsync(string kind, string trigger, Guid? commandId, Guid? historicalId, CancellationToken ct, bool dryRun = false,
        IReadOnlyList<string>? modules = null)
    {
        OneCPing ping;
        try
        {
            ping = await retry.ExecuteAsync("1С: проверка соединения", provider.TestConnectionAsync, ct);
        }
        catch (OneCException e)
        {
            logger.LogWarning("1С: синхронизация не начата — {Error}", e.Message);
            await api.StartRunAsync(new StartRunRequest
            {
                Kind = kind, Trigger = trigger, CommandId = commandId, HistoricalImportId = historicalId, Provider = provider.Name, WorkerId = options.WorkerId,
                FailedBeforeStart = "1С недоступна: " + e.Message,
            }, ct);
            return (null, new SyncOutcome(RunStatuses.Failed, "1С недоступна: " + e.Message));
        }

        var start = await retry.ExecuteAsync("OneBase: начало синхронизации", t => api.StartRunAsync(new StartRunRequest
        {
            Kind = kind, Trigger = trigger, CommandId = commandId, HistoricalImportId = historicalId, InfobaseId = ping.InfobaseId, InfobaseName = ping.InfobaseName,
            Provider = provider.Name, WorkerId = options.WorkerId,
        }, t), ct);
        if (!start.Started || start.RunId is null)
        {
            return (null, new SyncOutcome("Skipped", start.Message ?? start.Reason ?? "Синхронизация не начата."));
        }

        var run = new Run
        {
            Id = start.RunId.Value, Kind = kind, DryRun = dryRun, ByVersions = ByVersions(ping),
            BatchSize = Math.Clamp(start.Settings?.BatchSize ?? options.BatchSize, 50, 1000),
            OrganizationFilter = start.Settings?.OrganizationIds ?? [],
            Modules = Restrict(start.Settings?.Modules ?? [], modules),
            Accounting = start.Settings?.Modules is not { Count: > 0 } enabled || enabled.Contains(OneCModules.Accounting),
        };
        foreach (var c in start.Checkpoints)
        {
            run.Checkpoints[(c.Stream, c.OrganizationId)] = c.Cursor;
        }

        CurrentRunId = run.Id;
        logger.LogInformation("1С: запуск {Kind} {RunId} начат (база {Infobase})", kind, run.Id, ping.InfobaseName ?? ping.InfobaseId);
        return (run, null);
    }

    private async Task<SyncOutcome> FinishAsync(Run run, string status, string message, object? details = null)
    {
        CurrentRunId = null;
        try
        {
            await retry.ExecuteAsync("OneBase: итог синхронизации", t => api.FinishRunAsync(run.Id, new FinishRunRequest { Status = status, Message = message, Steps = run.Steps }, t), CancellationToken.None);
        }
        catch (Exception e)
        {
            // OneBase сам закроет запуск без пульса через 15 минут; записанные пакеты уже сохранены.
            logger.LogWarning("1С: итог запуска {RunId} не записан в OneBase — {Error}", run.Id, e.Message);
        }

        logger.LogInformation("1С: запуск {Kind} {RunId} — {Status}: {Message}", run.Kind, run.Id, status, message);
        return new SyncOutcome(status, message, run.Id, run.Steps, details);
    }

    /// <summary>Модули запуска: включённые на странице «1С», а для «синхронизировать модуль» — только выбранные из них. Пусто — все.</summary>
    private static IReadOnlyList<string> Restrict(IReadOnlyList<string> enabled, IReadOnlyList<string>? only)
    {
        if (only is not { Count: > 0 })
        {
            return enabled;
        }

        var on = enabled.Count == 0 ? OneCModules.All : enabled;
        var picked = OneCModules.Normalize(only).Where(on.Contains).ToList();
        return picked.Count == 0 ? on : picked;
    }

    private static string Summary(Run run) =>
        $"новых {run.Created}, изменено {run.Updated}, без изменений {run.Unchanged}, удалено {run.Deleted}, отклонено {run.Rejected}, пакетов {run.Batches}";

    /// <summary>Шаг с замером времени и строкой в журнале запуска.</summary>
    private async Task StepAsync(Run run, string stream, string? org, Func<Task<int>> body)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var rows = await body();
            run.Steps.Add(new RunStep { Stream = stream, OrganizationId = org, Rows = rows, DurationMs = (int)watch.ElapsedMilliseconds });
        }
        catch (StopException)
        {
            throw;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            run.Errors++;
            run.Steps.Add(new RunStep { Stream = stream, OrganizationId = org, Error = e.Message, DurationMs = (int)watch.ElapsedMilliseconds });
            if (e is OneCException { Fatal: true } or OneCException { Transient: true } or OneBaseApiException)
            {
                // 1С или OneBase недоступны, авторизация не прошла: дальше идти бессмысленно — запуск завершается, курсоры на месте.
                throw;
            }

            logger.LogWarning("1С: шаг {Stream} {Org} не выполнен — {Error}", stream, org, e.Message);
        }
    }

    // ---------- Пакеты ----------

    /// <summary>
    /// Отправка пакета: сначала — записи, не прошедшие проверку (целиком, в очередь ошибок), потом сам пакет с курсором. Ключ пакета не меняется
    /// между попытками: если ответ OneBase потерялся после записи, повтор вернёт прежний итог, а не запишет второй раз.
    /// </summary>
    private async Task<int> PostAsync(Run run, NormalizedBatch batch, string stream, string? org, ImportCheckpoint? checkpoint = null,
        BalanceSnapshot? snapshot = null, HistoricalPosition? position = null, CancellationToken ct = default)
    {
        foreach (var r in batch.Rejections)
        {
            run.Rejected++;
            run.Rejections.Add(new ImportRejection(r.EntityType, r.SourceId, r.Reason));
            if (!run.DryRun)
            {
                await retry.ExecuteAsync("OneBase: запись отклонённой записи", t => api.ReportFailureAsync(new FailureReport
                {
                    RunId = run.Id, Kind = FailureKinds.Validation, Stream = stream, OrganizationId = org, EntityType = r.EntityType, SourceId = r.SourceId,
                    Error = r.Reason, Payload = r.Payload,
                }, t), ct);
            }
        }

        foreach (var (o, day) in batch.EarliestByOrganization)
        {
            if (!run.Earliest.TryGetValue(o, out var current) || day < current)
            {
                run.Earliest[o] = day;
            }
        }

        if (batch.Count == 0 && checkpoint is null && snapshot is null && position is null)
        {
            return 0;
        }

        var request = batch.ToRequest(run.Id, $"{run.Id:N}-{++run.Batches}", stream, org) with { Checkpoint = checkpoint, Snapshot = snapshot, Historical = position };
        ImportBatchResult result;
        try
        {
            result = await retry.ExecuteAsync($"OneBase: пакет {stream}", t => api.ImportAsync(request, run.DryRun, t), ct);
        }
        catch (OneBaseApiException e) when (e.Conflict)
        {
            throw new StopException(e.Message);
        }
        catch (OneBaseApiException e) when (!run.DryRun && !e.Unauthorized)
        {
            // Пакет не записан: сохраняем его целиком — повтор отправит тот же пакет (с тем же ключом), данные не теряются.
            await TryReportAsync(new FailureReport
            {
                RunId = run.Id, Kind = FailureKinds.Batch, Stream = stream, OrganizationId = org, Period = snapshot?.Period ?? position?.Period,
                Cursor = checkpoint?.Cursor, Error = e.Message, Payload = JsonSerializer.SerializeToElement(request, Json),
            }, ct);
            throw;
        }

        run.Created += result.Created;
        run.Updated += result.Updated;
        run.Unchanged += result.Unchanged;
        run.Deleted += result.Deleted;
        run.Rejected += result.Rejected;
        run.Rejections.AddRange(result.Rejections);
        if (checkpoint is not null)
        {
            run.Checkpoints[(checkpoint.Stream, checkpoint.OrganizationId)] = checkpoint.Cursor;
        }

        var progress = await retry.ExecuteAsync("OneBase: пульс запуска", t => api.ProgressAsync(run.Id, new RunProgress(stream, Summary(run)), t), ct);
        if (progress.Stop)
        {
            throw new StopException(progress.Reason ?? "Остановлено в OneBase.");
        }

        return batch.Count;
    }

    private async Task TryReportAsync(FailureReport report, CancellationToken ct)
    {
        try
        {
            await retry.ExecuteAsync("OneBase: запись ошибки", t => api.ReportFailureAsync(report, t), ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning("1С: ошибку не удалось записать в OneBase — {Error}", e.Message);
        }
    }

    private Task<OneCPage<T>> FetchAsync<T>(string what, Func<CancellationToken, Task<OneCPage<T>>> fetch, CancellationToken ct) =>
        retry.ExecuteAsync("1С: " + what, fetch, ct);

    // ---------- Справочники ----------

    /// <summary>Справочник целиком, страницами; курсор ref:&lt;тип&gt; — после последней страницы «done».</summary>
    private async Task<int> ReferenceAsync(Run run, string type, bool force, CancellationToken ct, int? maxPages = null)
    {
        var stream = OneCStreams.Reference(type);
        var cursor = run.Cursor(stream);
        if (cursor == OneCStreams.Done && !force)
        {
            return 0;
        }

        if (cursor == OneCStreams.Done)
        {
            cursor = null;
        }

        var rows = 0;
        var pages = 0;
        do
        {
            var page = new OneCPageRequest(run.DryRun ? Math.Min(50, run.BatchSize) : run.BatchSize, cursor);
            var batch = new NormalizedBatch();
            string? next;
            bool more;
            switch (type)
            {
                case OneCEntityTypes.Organization: (next, more) = Add(batch, await FetchAsync(type, t => provider.GetOrganizationsAsync(page, t), ct), normalizer.Add); break;
                case OneCEntityTypes.Account: (next, more) = Add(batch, await FetchAsync(type, t => provider.GetAccountsAsync(page, t), ct), normalizer.Add); break;
                case OneCEntityTypes.Currency: (next, more) = Add(batch, await FetchAsync(type, t => provider.GetCurrenciesAsync(page, t), ct), normalizer.Add); break;
                case OneCEntityTypes.Counterparty: (next, more) = Add(batch, await FetchAsync(type, t => provider.GetCounterpartiesAsync(page, t), ct), normalizer.Add); break;
                case OneCEntityTypes.Contract: (next, more) = Add(batch, await FetchAsync(type, t => provider.GetContractsAsync(page, t), ct), normalizer.Add); break;
                case OneCEntityTypes.Item: (next, more) = Add(batch, await FetchAsync(type, t => provider.GetNomenclatureAsync(page, t), ct), normalizer.Add); break;
                case OneCEntityTypes.Warehouse:
                    (next, more) = Add(batch, await FetchAsync(type, t => provider.GetWarehousesAsync(page, t), ct), (b, r) => normalizer.Add(b, r, OneCEntityTypes.Warehouse));
                    break;
                case OneCEntityTypes.Department:
                    (next, more) = Add(batch, await FetchAsync(type, t => provider.GetDepartmentsAsync(page, t), ct), (b, r) => normalizer.Add(b, r, OneCEntityTypes.Department));
                    break;
                default:
                    var kind = type[OneCEntityTypes.ReferencePrefix.Length..];
                    (next, more) = Add(batch, await FetchAsync(type, t => provider.GetReferencesAsync(kind, page, t), ct), (b, r) => normalizer.Add(b, r, type));
                    break;
            }

            rows += await PostAsync(run, batch, stream, OneCEntityTypes.Shared, run.DryRun ? null : new ImportCheckpoint(stream, OneCEntityTypes.Shared, more ? next : OneCStreams.Done), ct: ct);
            cursor = more ? next : null;
            pages++;
        }
        while (cursor is not null && (maxPages is null || pages < maxPages));

        return rows;
    }

    private static (string? Next, bool More) Add<T>(NormalizedBatch batch, OneCPage<T> page, Action<NormalizedBatch, T> add)
    {
        foreach (var item in page.Items)
        {
            add(batch, item);
        }

        return (page.Page?.NextCursor, page.HasMore);
    }

    private async Task ReferencesAsync(Run run, bool force, CancellationToken ct)
    {
        foreach (var type in OneCStreams.ReferenceOrder)
        {
            await StepAsync(run, OneCStreams.Reference(type), null, () => ReferenceAsync(run, type, force, ct));
        }
    }

    // ---------- Изменения ----------

    /// <summary>
    /// Изменения 1С с последнего курсора. Первый раз (курсора нет) — 1С отдаёт текущий номер: он сохраняется, а прошлое загружает
    /// историческая загрузка. Курсор сдвигается только вместе с записанным пакетом.
    /// </summary>
    private async Task<int> ChangesAsync(Run run, CancellationToken ct, int? maxPages = null)
    {
        var cursor = run.Cursor(OneCStreams.Changes);
        if (cursor is null)
        {
            var first = await FetchAsync("изменения", t => provider.GetModifiedRecordsAsync(null, run.BatchSize, t), ct);
            if (!run.DryRun)
            {
                await PostAsync(run, new NormalizedBatch(), OneCStreams.Changes, OneCEntityTypes.Shared,
                    new ImportCheckpoint(OneCStreams.Changes, OneCEntityTypes.Shared, first.Page?.NextCursor ?? "0"), ct: ct);
            }

            return 0;
        }

        var rows = 0;
        var pages = 0;
        while (true)
        {
            OneCPage<OneCChangeDto> page;
            try
            {
                page = await FetchAsync("изменения", t => provider.GetModifiedRecordsAsync(cursor, run.BatchSize, t), ct);
            }
            catch (OneCException e) when (e.Kind == OneCErrorKind.CursorExpired)
            {
                await TryReportAsync(new FailureReport
                {
                    RunId = run.Id, Kind = FailureKinds.Fetch, Stream = OneCStreams.Changes, Cursor = cursor,
                    Error = e.Message + " Изменения с этого места 1С больше не хранит: запустите историческую загрузку за период с последней синхронизации.",
                }, ct);
                throw;
            }

            if (page.Items.Count == 0)
            {
                return rows;
            }

            var batch = new NormalizedBatch();
            foreach (var change in page.Items)
            {
                // Документы выключенных модулей пропускаются; без «Бухгалтерии» — без проводок.
                if (change is { EntityType: OneCEntityTypes.Document, Deleted: false, Data: { ValueKind: JsonValueKind.Object } data }
                    && data.TryGetProperty("type", out var t) && !run.Allows(t.GetString()))
                {
                    continue;
                }

                normalizer.Add(batch, change.EntityType == OneCEntityTypes.Document && change.Data is { } d ? change with { Data = WithoutEntries(run, OneCEntityTypes.Document, d) } : change);
            }

            var next = page.Page?.NextCursor ?? page.Items[^1].Seq.ToString(System.Globalization.CultureInfo.InvariantCulture);
            rows += await PostAsync(run, batch, OneCStreams.Changes, OneCEntityTypes.Shared, run.DryRun ? null : new ImportCheckpoint(OneCStreams.Changes, OneCEntityTypes.Shared, next), ct: ct);
            cursor = next;
            pages++;
            if (!page.HasMore || (maxPages is not null && pages >= maxPages))
            {
                return rows;
            }
        }
    }

    // ---------- Сверка версий (изменения только чтением) ----------

    /// <summary>Изменения — сверкой версий: так задано в службе или так объявила 1С (changeTracking в /ping).</summary>
    private bool ByVersions(OneCPing ping) => options.ChangeTracking.Trim().ToLowerInvariant() switch
    {
        "versions" => true,
        "changes" => false,
        _ => ping.TracksByVersions,
    };

    /// <summary>
    /// Сверка версий при синхронизации: справочники целиком, документы организаций за текущий и ReconcileMonthsBack прошлых месяцев; раз в
    /// сутки (с FullReconcileHour по Ташкенту) — ещё и все месяцы, за которые в OneBase есть документы. В 1С ничего не записывается.
    /// </summary>
    private async Task ReconcileAsync(Run run, CancellationToken ct)
    {
        foreach (var type in OneCStreams.ReferenceOrder)
        {
            await StepAsync(run, OneCStreams.VersionsPrefix + OneCStreams.Reference(type), null, () => ReconcileReferenceAsync(run, type, ct));
        }

        var current = new DateOnly(Today.Year, Today.Month, 1);
        var recent = current.AddMonths(-Math.Clamp(options.ReconcileMonthsBack, 0, 24));
        var organizations = await OrganizationsAsync(run, ct);
        foreach (var org in organizations)
        {
            await StepAsync(run, $"{OneCStreams.VersionsPrefix}{OneCEntityTypes.Document}:{recent:yyyy-MM}..{current:yyyy-MM}", org,
                () => ReconcileDocumentsAsync(run, org, recent, current.AddMonths(1).AddDays(-1), ct));
        }

        var now = clock.GetUtcNow().ToOffset(Tashkent);
        var today = Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (run.DryRun || now.Hour < Math.Clamp(options.FullReconcileHour, 0, 23) || run.Cursor(OneCStreams.VersionsFull) == today)
        {
            return;
        }

        // Ночная полная сверка: все загруженные месяцы, кроме уже сверенных выше; день сохраняется, только если все месяцы сверены.
        var errors = run.Errors;
        var months = await retry.ExecuteAsync("OneBase: месяцы с документами", api.StoredMonthsAsync, ct);
        foreach (var m in months.Where(m => organizations.Contains(m.OrganizationId) && m.Month < recent))
        {
            await StepAsync(run, $"{OneCStreams.VersionsPrefix}{OneCEntityTypes.Document}:{m.Month:yyyy-MM}", m.OrganizationId,
                () => ReconcileDocumentsAsync(run, m.OrganizationId, m.Month, m.Month.AddMonths(1).AddDays(-1), ct));
        }

        if (run.Errors == errors)
        {
            await PostAsync(run, new NormalizedBatch(), OneCStreams.VersionsFull, OneCEntityTypes.Shared,
                new ImportCheckpoint(OneCStreams.VersionsFull, OneCEntityTypes.Shared, today), ct: ct);
        }
    }

    /// <summary>Справочник: версии 1С против записей OneBase.</summary>
    private async Task<int> ReconcileReferenceAsync(Run run, string type, CancellationToken ct)
    {
        var source = await VersionsAsync(new OneCVersionQuery(type), ct);
        var stored = await retry.ExecuteAsync($"OneBase: версии {type}", t => api.StoredVersionsAsync(type, null, null, null, t), ct);
        return await ApplyVersionsAsync(run, type, OneCEntityTypes.Shared, source, stored, null, ct);
    }

    /// <summary>Документы организации за период (по дате документа): версии 1С против записей OneBase.</summary>
    private async Task<int> ReconcileDocumentsAsync(Run run, string org, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var source = await VersionsAsync(new OneCVersionQuery(OneCEntityTypes.Document, org, from, to), ct);
        var stored = await retry.ExecuteAsync("OneBase: версии документов", t => api.StoredVersionsAsync(OneCEntityTypes.Document, org, from, to, t), ct);
        return await ApplyVersionsAsync(run, OneCEntityTypes.Document, org, source, stored, from, ct);
    }

    private async Task<List<OneCVersionDto>> VersionsAsync(OneCVersionQuery query, CancellationToken ct)
    {
        var list = new List<OneCVersionDto>();
        string? cursor = null;
        do
        {
            var page = await FetchAsync("версии " + query.EntityType, t => provider.GetVersionsAsync(query, new OneCPageRequest(1000, cursor), t), ct);
            list.AddRange(page.Items.Where(v => !string.IsNullOrWhiteSpace(v.Id)));
            cursor = page.HasMore ? page.Page!.NextCursor : null;
        }
        while (cursor is not null);

        return list;
    }

    /// <summary>
    /// Разница версий: новые, с другой версией и вернувшиеся после удаления — дочитываются по ids= и записываются как изменения; записи OneBase,
    /// которых нет в 1С, сначала запрашиваются по ids= (документ мог «переехать» в другой месяц) — не вернулись — помечаются удалёнными.
    /// Историю OneBase сохраняет: удалённое не стирается.
    /// </summary>
    private async Task<int> ApplyVersionsAsync(Run run, string type, string org, List<OneCVersionDto> source, List<StoredVersion> stored, DateOnly? month,
        CancellationToken ct)
    {
        if (type == OneCEntityTypes.Document && run.Modules.Count > 0)
        {
            // Выключенные модули: их документы не загружаются, а уже загруженные не трогаются (ни правки, ни удаления).
            source = source.Where(v => run.Allows(v.Type)).ToList();
            stored = stored.Where(v => run.Allows(v.Type)).ToList();
        }

        var have = stored.GroupBy(s => s.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.OrderBy(s => s.Deleted).First(), StringComparer.Ordinal);
        var present = source.Select(v => v.Id!).ToHashSet(StringComparer.Ordinal);
        var changed = source
            .Where(v => !have.TryGetValue(v.Id!, out var s) || !string.Equals(s.Version, v.Version, StringComparison.Ordinal) || (s.Deleted && !v.DeletionMark))
            .Select(v => v.Id!).Distinct(StringComparer.Ordinal).ToList();
        var gone = have.Values.Where(s => !s.Deleted && !present.Contains(s.Id)).Select(s => s.Id).ToList();
        if (changed.Count == 0 && gone.Count == 0)
        {
            return 0;
        }

        var stream = OneCStreams.VersionsPrefix + type;
        var found = new HashSet<string>(StringComparer.Ordinal);
        var rows = 0;
        foreach (var chunk in changed.Concat(gone).Chunk(run.DryRun ? 20 : OneCHttpDataProvider.MaxIds))
        {
            var items = await retry.ExecuteAsync($"1С: {type} по идентификаторам", t => provider.GetByIdsAsync(type, chunk, t), ct);
            var batch = new NormalizedBatch();
            foreach (var item in items)
            {
                var id = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                if (id is not null)
                {
                    found.Add(id);
                }

                normalizer.Add(batch, new OneCChangeDto { EntityType = type, Id = id, OrganizationId = org.Length == 0 ? null : org, Data = WithoutEntries(run, type, item) });
            }

            rows += await PostAsync(run, batch, stream, org, ct: ct);
            if (run.DryRun)
            {
                return rows;
            }
        }

        // В 1С объекта больше нет (физически удалён): запись помечается удалённой, история и проводки остаются в OneBase.
        var deleted = changed.Concat(gone).Where(id => !found.Contains(id) && have.TryGetValue(id, out var s) && !s.Deleted).Distinct(StringComparer.Ordinal).ToList();
        if (deleted.Count > 0)
        {
            var batch = new NormalizedBatch();
            foreach (var id in deleted)
            {
                var s = have[id];
                batch.Deletions.Add(new ImportDeletion { EntityType = type, SourceId = id, OrganizationId = s.Org.Length == 0 ? null : s.Org });
            }

            if (month is { } m && org.Length > 0 && (!run.Earliest.TryGetValue(org, out var earliest) || m < earliest))
            {
                run.Earliest[org] = m; // остатки пересчитать с месяца удалённого документа
            }

            rows += await PostAsync(run, batch, stream, org, ct: ct);
        }

        return rows;
    }

    /// <summary>Модуль «Бухгалтерия» выключен — документ приходит без проводок (сами проводки в OneBase не меняются).</summary>
    private static JsonElement WithoutEntries(Run run, string type, JsonElement item)
    {
        if (run.Accounting || type != OneCEntityTypes.Document || item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("entries", out _))
        {
            return item;
        }

        var node = System.Text.Json.Nodes.JsonNode.Parse(item.GetRawText())!.AsObject();
        node.Remove("entries");
        return JsonSerializer.SerializeToElement(node);
    }

    // ---------- Курсы, остатки ----------

    private async Task<(int Rows, OneCTotals? Totals)> RatesAsync(Run run, DateOnly from, DateOnly to, string? cursor, Func<string?, HistoricalPosition?>? position, CancellationToken ct)
    {
        var rows = 0;
        OneCTotals? totals = null;
        do
        {
            var page = await FetchAsync("курсы", t => provider.GetExchangeRatesAsync(from, to, new OneCPageRequest(run.BatchSize, cursor, totals is null), t), ct);
            totals ??= page.Totals ?? new OneCTotals(null, null);
            var batch = new NormalizedBatch();
            page.Items.ForEach(r => normalizer.Add(batch, r));
            var next = page.HasMore ? page.Page!.NextCursor : null;
            rows += await PostAsync(run, batch, OneCStreams.HistoryPrefix + OneCStreams.HistoryRates, OneCEntityTypes.Shared, position: position?.Invoke(next), ct: ct);
            cursor = next;
        }
        while (cursor is not null);

        return (rows, totals);
    }

    /// <summary>Снимок оборотно-сальдовой ведомости организации за месяц; последний пакет помечает удалёнными строки, которых в снимке нет.</summary>
    private async Task<(int Rows, OneCTotals? Totals)> SnapshotAsync(Run run, string org, DateOnly month, string? cursor, Func<string?, HistoricalPosition?>? position, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var rows = 0;
        OneCTotals? totals = null;
        do
        {
            var page = await FetchAsync("остатки", t => provider.GetAccountBalancesAsync(org, month, new OneCPageRequest(run.BatchSize, cursor, totals is null), t), ct);
            totals ??= page.Totals ?? new OneCTotals(null, null);
            var batch = new NormalizedBatch();
            page.Items.ForEach(r => normalizer.Add(batch, r));
            var next = page.HasMore ? page.Page!.NextCursor : null;
            rows += await PostAsync(run, batch, OneCStreams.Balances, org, snapshot: new BalanceSnapshot(org, month, id, next is null), position: position?.Invoke(next), ct: ct);
            cursor = next;
        }
        while (cursor is not null);

        return (rows, totals);
    }

    /// <summary>Организации 1С запуска: не помеченные на удаление и выбранные на странице «1С» (пусто — все).</summary>
    private async Task<List<string>> OrganizationsAsync(Run run, CancellationToken ct) =>
        (await OrganizationsAsync(ct)).Where(o => run.OrganizationFilter.Count == 0 || run.OrganizationFilter.Contains(o)).ToList();

    private async Task<List<string>> OrganizationsAsync(CancellationToken ct)
    {
        var list = new List<string>();
        string? cursor = null;
        do
        {
            var page = await FetchAsync("организации", t => provider.GetOrganizationsAsync(new OneCPageRequest(1000, cursor), t), ct);
            list.AddRange(page.Items.Where(o => !o.DeletionMark && !string.IsNullOrWhiteSpace(o.Id)).Select(o => o.Id!));
            cursor = page.HasMore ? page.Page!.NextCursor : null;
        }
        while (cursor is not null);

        return list.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Остатки — за текущий месяц и за месяцы, где были изменения (не дальше BalancesMonthsBack).</summary>
    private async Task BalancesAsync(Run run, CancellationToken ct)
    {
        var today = Today;
        var current = new DateOnly(today.Year, today.Month, 1);
        var limit = current.AddMonths(-Math.Max(0, options.BalancesMonthsBack));
        if (!run.Accounting)
        {
            return; // модуль «Бухгалтерия» выключен — ведомость не загружается
        }

        foreach (var org in await OrganizationsAsync(run, ct))
        {
            var from = run.Earliest.TryGetValue(org, out var earliest) ? new DateOnly(earliest.Year, earliest.Month, 1) : current;
            from = from < limit ? limit : from > current ? current : from;
            for (var month = from; month <= current; month = month.AddMonths(1))
            {
                var m = month;
                await StepAsync(run, $"{OneCStreams.Balances}:{m:yyyy-MM}", org, async () => (await SnapshotAsync(run, org, m, null, null, ct)).Rows);
            }
        }
    }

    // ---------- Синхронизация по расписанию ----------

    /// <summary>Синхронизация; modules — только эти модули (документы их типов; «Бухгалтерия» — ещё и ведомость), справочники и курсы — всегда.</summary>
    public async Task<SyncOutcome> IncrementalAsync(string trigger, Guid? commandId, CancellationToken ct, IReadOnlyList<string>? modules = null)
    {
        var (run, refused) = await BeginAsync(RunKinds.Incremental, trigger, commandId, null, ct, modules: modules);
        if (run is null)
        {
            return refused!;
        }

        try
        {
            await ReferencesAsync(run, false, ct);
            var historyMissing = !run.ByVersions && run.Cursor(OneCStreams.Changes) is null;
            if (run.ByVersions)
            {
                // Только чтение: что изменилось и что удалено — по версиям объектов 1С (в 1С ничего не записывается).
                await ReconcileAsync(run, ct);
            }
            else
            {
                await StepAsync(run, OneCStreams.Changes, null, () => ChangesAsync(run, ct));
            }

            await StepAsync(run, OneCStreams.HistoryPrefix + OneCStreams.HistoryRates, null, async () => (await RatesAsync(run, Today.AddDays(-7), Today, null, null, ct)).Rows);
            await BalancesAsync(run, ct);
            await RetryDueAsync(run, ct);
            var status = run.Errors > 0 ? RunStatuses.Partial : RunStatuses.Succeeded;
            var message = Summary(run) + (historyMissing ? ". Первое подключение: изменения 1С отслеживаются с этого момента, прошлое — «Историческая загрузка»." : "");
            return await FinishAsync(run, status, run.Errors > 0 ? $"Часть шагов с ошибками ({run.Errors}); {message}" : message);
        }
        catch (StopException e)
        {
            return await FinishAsync(run, RunStatuses.Cancelled, $"{e.Message} {Summary(run)}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await FinishAsync(run, RunStatuses.Cancelled, "Служба остановлена. " + Summary(run));
            throw;
        }
        catch (Exception e)
        {
            return await FinishAsync(run, RunStatuses.Failed, $"{e.Message} Записанное сохранено ({Summary(run)}); следующий запуск продолжит с последнего курсора.");
        }
    }

    // ---------- Тестовая синхронизация ----------

    /// <summary>По странице каждого справочника, изменений и документов текущего месяца — через всю проверку OneBase, но без записи.</summary>
    public async Task<SyncOutcome> TestAsync(Guid? commandId, CancellationToken ct)
    {
        var (run, refused) = await BeginAsync(RunKinds.Test, "manual", commandId, null, ct, dryRun: true);
        if (run is null)
        {
            return refused!;
        }

        try
        {
            foreach (var type in OneCStreams.ReferenceOrder)
            {
                await StepAsync(run, OneCStreams.Reference(type), null, () => ReferenceAsync(run, type, true, ct, maxPages: 1));
            }

            var orgs = await OrganizationsAsync(run, ct);
            var month = new DateOnly(Today.Year, Today.Month, 1);
            if (run.ByVersions)
            {
                // Сверка версий: /versions и ids= — по контрагентам и документам текущего месяца (без записи, до 20 объектов на шаг).
                await StepAsync(run, OneCStreams.VersionsPrefix + OneCEntityTypes.Counterparty, null, () => ReconcileReferenceAsync(run, OneCEntityTypes.Counterparty, ct));
                foreach (var org in orgs.Take(2))
                {
                    await StepAsync(run, OneCStreams.VersionsPrefix + OneCEntityTypes.Document, org, () => ReconcileDocumentsAsync(run, org, month, Today, ct));
                }
            }
            else
            {
                await StepAsync(run, OneCStreams.Changes, null, () => ChangesAsync(run, ct, maxPages: 1));
            }

            foreach (var org in orgs.Take(2))
            {
                await StepAsync(run, OneCStreams.HistoryPrefix + OneCStreams.HistoryDocuments, org, async () =>
                {
                    var batch = new NormalizedBatch();
                    var page = await FetchAsync("документы", t => provider.GetDocumentsAsync(org, null, month, Today, new OneCPageRequest(50), t), ct);
                    page.Items.ForEach(d => normalizer.Add(batch, d));
                    return await PostAsync(run, batch, OneCStreams.HistoryPrefix + OneCStreams.HistoryDocuments, org, ct: ct);
                });
                await StepAsync(run, OneCStreams.HistoryPrefix + OneCStreams.HistoryEntries, org, async () =>
                {
                    var batch = new NormalizedBatch();
                    var page = await FetchAsync("проводки", t => provider.GetTransactionsAsync(org, month, Today, new OneCPageRequest(50), t), ct);
                    page.Items.ForEach(s => normalizer.Add(batch, s));
                    return await PostAsync(run, batch, OneCStreams.HistoryPrefix + OneCStreams.HistoryEntries, org, ct: ct);
                });
            }

            var checkedRows = run.Steps.Sum(s => s.Rows);
            var status = run.Errors > 0 || run.Rejected > 0 ? RunStatuses.Partial : RunStatuses.Succeeded;
            var message = $"Тестовая синхронизация без записи: проверено {checkedRows} записей, отклонено бы {run.Rejected}" + (run.Errors > 0 ? $", ошибок шагов {run.Errors}" : "") + ".";
            return await FinishAsync(run, status, message, new { rows = checkedRows, rejected = run.Rejections.Take(20), errors = run.Steps.Where(s => s.Error is not null).Select(s => new { s.Stream, s.Error }) });
        }
        catch (StopException e)
        {
            return await FinishAsync(run, RunStatuses.Cancelled, e.Message);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return await FinishAsync(run, RunStatuses.Failed, "Тестовая синхронизация не прошла: " + e.Message);
        }
    }

    // ---------- Историческая загрузка ----------

    /// <summary>
    /// История за период по месяцам: для каждой организации — курсы (один раз на месяц), документы, проводки, остатки. Положение сохраняется
    /// вместе с каждым пакетом; после каждого потока месяца — сверка количества и сумм с итогами 1С. Порция ограничена по времени: следующий
    /// запуск продолжит с того же места. Сбой сети — порция завершается и продолжается позже; ошибка отдельного месяца — в очередь ошибок.
    /// </summary>
    public async Task<SyncOutcome> HistoricalAsync(Guid importId, Guid? commandId, CancellationToken ct)
    {
        var state = await retry.ExecuteAsync("OneBase: историческая загрузка", t => api.HistoricalAsync(importId, t), ct);
        if (state.Status is not (HistoricalStatuses.Pending or HistoricalStatuses.Running))
        {
            return new SyncOutcome("Skipped", $"Историческая загрузка уже {state.Status}.");
        }

        var (run, refused) = await BeginAsync(RunKinds.Historical, commandId is null ? "auto" : "manual", commandId, importId, ct);
        if (run is null)
        {
            return refused!;
        }

        var deadline = clock.GetUtcNow() + TimeSpan.FromMinutes(Math.Max(1, options.HistoricalBudgetMinutes));
        // Размер пакета — из задания исторической загрузки; курсоры — те, что пришли с началом запуска.
        var begun = run;
        run = new Run { Id = begun.Id, Kind = begun.Kind, BatchSize = Math.Clamp(state.BatchSize, 50, 1000) };
        foreach (var c in begun.Checkpoints)
        {
            run.Checkpoints[c.Key] = c.Value;
        }

        try
        {
            // Курсор изменений — до истории: всё, что поменяется в 1С во время загрузки, придёт обычной синхронизацией.
            if (run.Cursor(OneCStreams.Changes) is null)
            {
                await StepAsync(run, OneCStreams.Changes, null, () => ChangesAsync(run, ct));
            }

            if (!state.ReferencesDone)
            {
                await ReferencesAsync(run, false, ct);
                if (run.Errors > 0)
                {
                    return await FinishAsync(run, RunStatuses.Partial, "Справочники загружены не полностью — история продолжится после повтора. " + Summary(run));
                }

                await retry.ExecuteAsync("OneBase: справочники загружены", t => api.HistoricalProgressAsync(importId, new HistoricalProgressRequest { ReferencesDone = true }, t), ct);
                state = await api.HistoricalAsync(importId, ct);
            }

            var orgs = state.Organizations;
            if (orgs.Count == 0)
            {
                await api.HistoricalProgressAsync(importId, new HistoricalProgressRequest { Status = HistoricalStatuses.Failed, Message = "В 1С нет организаций — загружать нечего." }, ct);
                return await FinishAsync(run, RunStatuses.Failed, "В 1С нет организаций.");
            }

            var pos = state.Position ?? new HistoricalPosition(importId, new DateOnly(state.From.Year, state.From.Month, 1), 0, OneCStreams.HistoryOrder[0], null);
            for (var month = pos.Period; month <= state.To; month = month.AddMonths(1))
            {
                var from = month < state.From ? state.From : month;
                var end = month.AddMonths(1).AddDays(-1);
                var to = end > state.To ? state.To : end;
                for (var oi = month == pos.Period ? pos.OrganizationIndex : 0; oi < orgs.Count; oi++)
                {
                    var org = orgs[oi];
                    var startStream = month == pos.Period && oi == pos.OrganizationIndex ? Math.Max(0, OneCStreams.HistoryOrder.ToList().IndexOf(pos.Stream)) : 0;
                    for (var si = startStream; si < OneCStreams.HistoryOrder.Count; si++)
                    {
                        var stream = OneCStreams.HistoryOrder[si];
                        var cursor = month == pos.Period && oi == pos.OrganizationIndex && stream == pos.Stream ? pos.Cursor : null;
                        if (stream == OneCStreams.HistoryRates && oi > 0)
                        {
                            continue; // курсы общие для базы — один раз на месяц
                        }

                        if ((stream is OneCStreams.HistoryEntries or OneCStreams.HistoryBalances) && !run.Accounting)
                        {
                            continue; // модуль «Бухгалтерия» выключен на странице «1С»
                        }

                        var (m, o, s) = (month, oi, stream);
                        HistoricalPosition At(string? next) => new(importId, m, o, s, next);
                        OneCTotals? totals = null;
                        var failed = false;
                        try
                        {
                            totals = await HistoryStreamAsync(run, stream, org, month, from, to, cursor, At, ct);
                        }
                        catch (Exception e) when (e is OneCException { Transient: false, Fatal: false })
                        {
                            // Ошибка отдельного месяца (например, 1С отклонила запрос): в очередь ошибок, загрузка идёт дальше.
                            failed = true;
                            run.Errors++;
                            run.Steps.Add(new RunStep { Stream = OneCStreams.HistoryPrefix + stream, OrganizationId = org, Error = $"{month:yyyy-MM}: {e.Message}" });
                            await TryReportAsync(new FailureReport
                            {
                                RunId = run.Id, Kind = FailureKinds.Fetch, Stream = OneCStreams.HistoryPrefix + stream, OrganizationId = org, Period = month, Error = e.Message,
                                Payload = JsonSerializer.SerializeToElement(new { from, to }, Json),
                            }, ct);
                        }

                        var nextPos = si + 1 < OneCStreams.HistoryOrder.Count ? new HistoricalPosition(importId, month, oi, OneCStreams.HistoryOrder[si + 1], null)
                            : oi + 1 < orgs.Count ? new HistoricalPosition(importId, month, oi + 1, OneCStreams.HistoryOrder[0], null)
                            : new HistoricalPosition(importId, month.AddMonths(1), 0, OneCStreams.HistoryOrder[0], null);
                        var check = failed ? null : await CheckAsync(stream, stream == OneCStreams.HistoryRates ? OneCEntityTypes.Shared : org, month, from, to, totals, ct,
                            stream == OneCStreams.HistoryDocuments ? run.DocumentTypes : null);
                        await retry.ExecuteAsync("OneBase: положение истории", t => api.HistoricalProgressAsync(importId, new HistoricalProgressRequest { Position = nextPos, Check = check }, t), ct);
                        if (clock.GetUtcNow() > deadline)
                        {
                            return await FinishAsync(run, run.Errors > 0 ? RunStatuses.Partial : RunStatuses.Succeeded,
                                $"История: загружено до {month:MM.yyyy} ({Summary(run)}); продолжение — следующей порцией.");
                        }
                    }
                }
            }

            var final = await api.HistoricalAsync(importId, ct);
            await api.HistoricalProgressAsync(importId, new HistoricalProgressRequest
            {
                Status = HistoricalStatuses.Succeeded,
                Message = $"Загружено {state.From:dd.MM.yyyy}–{state.To:dd.MM.yyyy}: {Summary(run)}" + (run.Errors > 0 ? $"; месяцев с ошибками: {run.Errors} — в «Ошибках», их можно повторить." : "."),
            }, ct);
            return await FinishAsync(run, run.Errors > 0 ? RunStatuses.Partial : RunStatuses.Succeeded, $"Историческая загрузка завершена: {Summary(run)}", final);
        }
        catch (StopException e)
        {
            return await FinishAsync(run, RunStatuses.Cancelled, $"{e.Message} Загрузка продолжится с сохранённого места.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await FinishAsync(run, RunStatuses.Cancelled, "Служба остановлена — загрузка продолжится с сохранённого места.");
            throw;
        }
        catch (Exception e)
        {
            return await FinishAsync(run, RunStatuses.Failed, $"{e.Message} Загрузка продолжится с сохранённого места ({Summary(run)}).");
        }
    }

    private async Task<OneCTotals?> HistoryStreamAsync(Run run, string stream, string org, DateOnly month, DateOnly from, DateOnly to, string? cursor,
        Func<string?, HistoricalPosition> position, CancellationToken ct)
    {
        switch (stream)
        {
            case OneCStreams.HistoryRates:
                return (await RatesAsync(run, from, to, cursor, position, ct)).Totals;
            case OneCStreams.HistoryBalances:
                return (await SnapshotAsync(run, org, month, cursor, position, ct)).Totals;
        }

        OneCTotals? totals = null;
        do
        {
            var batch = new NormalizedBatch();
            string? next;
            if (stream == OneCStreams.HistoryDocuments)
            {
                var page = await FetchAsync("документы", t => provider.GetDocumentsAsync(org, run.DocumentTypes, from, to, new OneCPageRequest(run.BatchSize, cursor, totals is null), t), ct);
                totals ??= page.Totals ?? new OneCTotals(null, null);
                page.Items.Where(d => run.Allows(d.Type)).ToList().ForEach(d => normalizer.Add(batch, d with { Entries = null }));
                next = page.HasMore ? page.Page!.NextCursor : null;
            }
            else
            {
                var page = await FetchAsync("проводки", t => provider.GetTransactionsAsync(org, from, to, new OneCPageRequest(run.BatchSize, cursor, totals is null), t), ct);
                totals ??= page.Totals ?? new OneCTotals(null, null);
                page.Items.ForEach(s => normalizer.Add(batch, s));
                next = page.HasMore ? page.Page!.NextCursor : null;
            }

            await PostAsync(run, batch, OneCStreams.HistoryPrefix + stream, org, position: position(next), ct: ct);
            cursor = next;
        }
        while (cursor is not null);

        return totals;
    }

    /// <summary>Сверка потока месяца: итоги 1С и записанное в OneBase.</summary>
    private async Task<PeriodCheck> CheckAsync(string stream, string org, DateOnly month, DateOnly from, DateOnly to, OneCTotals? expected, CancellationToken ct,
        IReadOnlyList<string>? types = null)
    {
        var imported = await retry.ExecuteAsync("OneBase: сверка", t => api.TotalsAsync(stream, org, from, to, t, types), ct);
        var countOk = expected?.Count is not { } count || count == imported.Count;
        var amountOk = expected?.Amount is not { } amount || Math.Abs(amount - imported.Amount) < 0.01m;
        return new PeriodCheck
        {
            Period = month, OrganizationId = org, Stream = stream, ExpectedCount = expected?.Count, ExpectedAmount = expected?.Amount,
            ImportedCount = imported.Count, ImportedAmount = imported.Amount, Ok = countOk && amountOk,
            Note = expected?.Count is null ? "1С не прислала итоги — сверка только по OneBase" : countOk && amountOk ? null
                : $"Расхождение: 1С {expected.Count} шт. / {expected.Amount:N2}, OneBase {imported.Count} шт. / {imported.Amount:N2}",
        };
    }

    // ---------- Повтор неуспешных операций ----------

    public async Task<SyncOutcome> RetryFailuresAsync(Guid? commandId, CancellationToken ct)
    {
        var (run, refused) = await BeginAsync(RunKinds.Retry, "manual", commandId, null, ct);
        if (run is null)
        {
            return refused!;
        }

        try
        {
            var (done, failed) = await RetryDueAsync(run, ct);
            return await FinishAsync(run, failed > 0 ? RunStatuses.Partial : RunStatuses.Succeeded, $"Повтор: удалось {done}, не удалось {failed}. {Summary(run)}");
        }
        catch (StopException e)
        {
            return await FinishAsync(run, RunStatuses.Cancelled, e.Message);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return await FinishAsync(run, RunStatuses.Failed, "Повтор не выполнен: " + e.Message);
        }
    }

    private async Task<(int Done, int Failed)> RetryDueAsync(Run run, CancellationToken ct)
    {
        var due = await retry.ExecuteAsync("OneBase: что повторить", t => api.DueFailuresAsync(50, t), ct);
        var (done, failed) = (0, 0);
        foreach (var f in due)
        {
            string? error = null;
            var watch = Stopwatch.StartNew();
            try
            {
                await RetryOneAsync(run, f, ct);
            }
            catch (StopException)
            {
                throw;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                error = e.Message;
            }

            if (error is null)
            {
                done++;
            }
            else
            {
                failed++;
            }

            run.Steps.Add(new RunStep { Stream = "retry:" + f.Stream, OrganizationId = f.OrganizationId, Rows = error is null ? 1 : 0, Error = error, DurationMs = (int)watch.ElapsedMilliseconds });
            await TryOutcomeAsync(f.Id, new FailureOutcome(error is null, error), ct);
        }

        return (done, failed);
    }

    private async Task TryOutcomeAsync(Guid id, FailureOutcome outcome, CancellationToken ct)
    {
        try
        {
            await retry.ExecuteAsync("OneBase: итог повтора", t => api.FailureOutcomeAsync(id, outcome, t), ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning("1С: итог повтора не записан — {Error}", e.Message);
        }
    }

    private async Task RetryOneAsync(Run run, FailureItem f, CancellationToken ct)
    {
        switch (f.Kind)
        {
            case FailureKinds.Batch when f.Payload is { } payload:
            {
                // Тот же пакет, тот же ключ: если он всё-таки был записан — повтор вернёт прежний итог.
                var request = payload.Deserialize<ImportBatchRequest>(Json)! with { RunId = run.Id };
                var result = await retry.ExecuteAsync("OneBase: повтор пакета", t => api.ImportAsync(request, false, t), ct);
                run.Created += result.Created;
                run.Updated += result.Updated;
                break;
            }

            case FailureKinds.Record when f.Payload is { } payload:
                await PostAsync(run, Single(f.EntityType ?? "", payload), f.Stream, f.OrganizationId, ct: ct);
                if (run.Rejections.Count > 0 && run.Rejections[^1].SourceId == f.SourceId)
                {
                    throw new InvalidOperationException("OneBase снова отклонил запись: " + run.Rejections[^1].Reason);
                }

                break;

            case FailureKinds.Validation when f.Payload is { } payload:
            {
                var batch = new NormalizedBatch();
                Renormalize(batch, f.EntityType ?? "", payload);
                if (batch.Rejections.Count > 0)
                {
                    throw new InvalidOperationException("Запись 1С по-прежнему не проходит проверку: " + batch.Rejections[0].Reason);
                }

                await PostAsync(run, batch, f.Stream, f.OrganizationId, ct: ct);
                break;
            }

            case FailureKinds.Fetch when f.Stream.StartsWith(OneCStreams.ReferencePrefix, StringComparison.Ordinal):
                await ReferenceAsync(run, f.Stream[OneCStreams.ReferencePrefix.Length..], false, ct);
                break;

            case FailureKinds.Fetch when f.Stream == OneCStreams.Changes:
                await ChangesAsync(run, ct);
                break;

            case FailureKinds.Fetch when f.Stream.StartsWith(OneCStreams.HistoryPrefix, StringComparison.Ordinal) && f.Period is { } month && f.OrganizationId is { } org:
            {
                var stream = f.Stream[OneCStreams.HistoryPrefix.Length..];
                var end = month.AddMonths(1).AddDays(-1);
                var from = f.Payload?.TryGetProperty("from", out var fp) == true ? fp.Deserialize<DateOnly>(Json) : month;
                var to = f.Payload?.TryGetProperty("to", out var tp) == true ? tp.Deserialize<DateOnly>(Json) : end;
                await HistoryStreamAsync(run, stream, org, month, from, to, null, _ => null!, ct);
                break;
            }

            case FailureKinds.Fetch when f.Stream == OneCStreams.Balances && f.Period is { } period && f.OrganizationId is { } o:
                await SnapshotAsync(run, o, period, null, null, ct);
                break;

            default:
                throw new InvalidOperationException($"Повтор для «{f.Kind} {f.Stream}» не предусмотрен — исправьте причину и запустите синхронизацию.");
        }
    }

    /// <summary>Отклонённая OneBase запись (формат OneBase) — обратно в пакет по типу.</summary>
    private static NormalizedBatch Single(string entityType, JsonElement payload)
    {
        var b = new NormalizedBatch();
        if (payload.TryGetProperty("entityType", out _) && !payload.TryGetProperty("name", out _))
        {
            b.Deletions.Add(payload.Deserialize<ImportDeletion>(Json)!);
            return b;
        }

        switch (entityType)
        {
            case OneCEntityTypes.Organization: b.Organizations.Add(payload.Deserialize<ImportOrganization>(Json)!); break;
            case OneCEntityTypes.Account: b.Accounts.Add(payload.Deserialize<ImportAccount>(Json)!); break;
            case OneCEntityTypes.Currency: b.Currencies.Add(payload.Deserialize<ImportCurrency>(Json)!); break;
            case OneCEntityTypes.ExchangeRate: b.ExchangeRates.Add(payload.Deserialize<ImportExchangeRate>(Json)!); break;
            case OneCEntityTypes.Counterparty: b.Counterparties.Add(payload.Deserialize<ImportCounterparty>(Json)!); break;
            case OneCEntityTypes.Contract: b.Contracts.Add(payload.Deserialize<ImportContract>(Json)!); break;
            case OneCEntityTypes.Item: b.Items.Add(payload.Deserialize<ImportItem>(Json)!); break;
            case OneCEntityTypes.Document: b.Documents.Add(payload.Deserialize<ImportDocument>(Json)!); break;
            case OneCEntityTypes.Entry: b.EntrySets.Add(payload.Deserialize<ImportEntrySet>(Json)!); break;
            case OneCEntityTypes.Balance: b.Balances.Add(payload.Deserialize<ImportBalance>(Json)!); break;
            default: b.References.Add(payload.Deserialize<ImportReference>(Json)!); break;
        }

        return b;
    }

    /// <summary>Запись 1С, отклонённая проверкой службы, — проверить заново (правила могли измениться).</summary>
    private void Renormalize(NormalizedBatch b, string entityType, JsonElement payload)
    {
        switch (entityType)
        {
            case OneCEntityTypes.Organization: normalizer.Add(b, payload.Deserialize<OneCOrganizationDto>(Json)!); break;
            case OneCEntityTypes.Account: normalizer.Add(b, payload.Deserialize<OneCAccountDto>(Json)!); break;
            case OneCEntityTypes.Currency: normalizer.Add(b, payload.Deserialize<OneCCurrencyDto>(Json)!); break;
            case OneCEntityTypes.ExchangeRate: normalizer.Add(b, payload.Deserialize<OneCRateDto>(Json)!); break;
            case OneCEntityTypes.Counterparty: normalizer.Add(b, payload.Deserialize<OneCCounterpartyDto>(Json)!); break;
            case OneCEntityTypes.Contract: normalizer.Add(b, payload.Deserialize<OneCContractDto>(Json)!); break;
            case OneCEntityTypes.Item: normalizer.Add(b, payload.Deserialize<OneCItemDto>(Json)!); break;
            case OneCEntityTypes.Document: normalizer.Add(b, payload.Deserialize<OneCDocumentDto>(Json)!); break;
            case OneCEntityTypes.Entry: normalizer.Add(b, payload.Deserialize<OneCEntrySetDto>(Json)!); break;
            case OneCEntityTypes.Balance: normalizer.Add(b, payload.Deserialize<OneCBalanceDto>(Json)!); break;
            default:
                if (payload.TryGetProperty("seq", out _))
                {
                    normalizer.Add(b, payload.Deserialize<OneCChangeDto>(Json)!);
                }
                else
                {
                    normalizer.Add(b, payload.Deserialize<OneCReferenceDto>(Json)!, entityType);
                }

                break;
        }
    }
}
