using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.OneC;
using OneBase.Finance.Integration.Contracts;

namespace OneBase.Application.Finance;

/// <summary>
/// Запись пакета данных 1С в OneBase. Один пакет — одна транзакция (под общей блокировкой PostgreSQL: пакеты пишутся по одному): записи,
/// журнал изменений, курсор потока, положение исторической загрузки и отметка «пакет записан». Повтор пакета с тем же ключом ничего не меняет.
/// Запись ищется по ключу источника (организация, тип, идентификатор 1С): новая — создаётся, изменённая — обновляется с журналом, та же —
/// не трогается. Неверные записи отклоняются по одной и сохраняются целиком в очередь ошибок — остальной пакет записывается.
/// </summary>
public sealed class OneCImportService(IAppDbContext db, TimeProvider clock)
{
    /// <summary>Ключ advisory-блокировки PostgreSQL: пакеты 1С пишутся по одному.</summary>
    public const long LockKey = 0x4F6E_6543_0001;

    public const int MaxRecords = 5000;
    public const int MaxEntries = 50_000;

    private const string Source = OneCEntityTypes.SourceSystem;

    public async Task<ImportBatchResult> ImportAsync(ImportBatchRequest request, bool dryRun, CancellationToken ct)
    {
        Check(request);
        var watch = Stopwatch.StartNew();
        await using var tx = await db.LockedTransactionAsync([LockKey], ct);

        var run = await db.OneCSyncRuns.FirstOrDefaultAsync(r => r.Id == request.RunId, ct)
            ?? throw new OneCConflictException("Запуск синхронизации не найден.");
        if (run.Status != RunStatuses.Running)
        {
            throw new OneCConflictException($"Запуск синхронизации уже завершён ({run.Status}) — пакет не записан.");
        }

        var previous = await db.OneCImportBatches.AsNoTracking().FirstOrDefaultAsync(b => b.BatchKey == request.BatchKey, ct);
        if (previous is not null)
        {
            return Replay(previous);
        }

        var c = new Counters(request, clock.GetUtcNow(), run.Id);
        await OrganizationsAsync(c, request.Organizations, ct);
        await AccountsAsync(c, request.Accounts, ct);
        await CurrenciesAsync(c, request.Currencies, ct);
        await RatesAsync(c, request.ExchangeRates, ct);
        await CounterpartiesAsync(c, request.Counterparties, ct);
        await ContractsAsync(c, request.Contracts, ct);
        await ReferencesAsync(c, request.References, ct);
        await ItemsAsync(c, request.Items, ct);
        await DocumentsAsync(c, request.Documents, ct);
        await EntrySetsAsync(c, request.EntrySets, ct);
        await BalancesAsync(c, request.Balances, request.Snapshot, ct);
        await DeletionsAsync(c, request.Deletions, ct);

        if (dryRun)
        {
            // Тестовая синхронизация: проверка всего пути без записи — транзакция откатывается.
            db.ChangeTracker.Clear();
            return c.Result(request.BatchKey);
        }

        foreach (var change in c.Changes)
        {
            db.OneCChanges.Add(change);
        }

        foreach (var rejection in c.Rejections)
        {
            await OneCFailures.ReportAsync(db, clock, new FailureReport
            {
                RunId = run.Id, Kind = FailureKinds.Record, Stream = request.Stream, OrganizationId = request.OrganizationId,
                EntityType = rejection.EntityType, SourceId = rejection.SourceId, Error = rejection.Reason, Payload = c.Payloads.GetValueOrDefault(rejection.SourceId),
            }, ct);
        }

        if (request.Checkpoint is { } checkpoint)
        {
            await SaveCheckpointAsync(checkpoint, request, run.Id, c.Now, ct);
        }

        if (request.Historical is { } position)
        {
            await SavePositionAsync(position, request.Count, ct);
        }

        var result = c.Result(request.BatchKey);
        db.OneCImportBatches.Add(new OneCImportBatch
        {
            BatchKey = request.BatchKey, RunId = run.Id, Stream = request.Stream, OrganizationId = request.OrganizationId ?? string.Empty,
            Records = request.Count, Created = result.Created, Updated = result.Updated, Unchanged = result.Unchanged, Deleted = result.Deleted,
            Rejected = result.Rejected, ReceivedAt = c.Now, DurationMs = (int)watch.ElapsedMilliseconds,
            RejectionsJson = result.Rejections.Count == 0 ? null : JsonSerializer.Serialize(result.Rejections, OneCFormat.Json),
        });

        run.Created += result.Created;
        run.Updated += result.Updated;
        run.Unchanged += result.Unchanged;
        run.Deleted += result.Deleted;
        run.Rejected += result.Rejected;
        run.Batches++;
        run.HeartbeatAt = c.Now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException e)
        {
            throw new InvalidOperationException("Пакет не записан (запись изменена параллельно): " + string.Join(", ", e.Entries.Select(x => $"{x.Metadata.ClrType.Name} ({x.State})")), e);
        }

        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return result;
    }

    /// <summary>Сколько записей потока организации за период лежит в OneBase — для сверки с итогами 1С.</summary>
    public async Task<ImportedTotals> TotalsAsync(string stream, string organizationId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        switch (stream)
        {
            case OneCStreams.HistoryDocuments:
            {
                var q = db.OneCDocuments.AsNoTracking().Where(d => d.SourceOrganizationId == organizationId && !d.IsDeleted && d.DocumentDate >= from && d.DocumentDate <= to);
                return new ImportedTotals(await q.CountAsync(ct), await q.SumAsync(d => (decimal?)d.AmountAccounting, ct) ?? 0);
            }
            case OneCStreams.HistoryEntries:
            {
                var q = db.OneCEntries.AsNoTracking().Where(e => e.SourceOrganizationId == organizationId && !e.IsDeleted && e.PeriodDate >= from && e.PeriodDate <= to);
                return new ImportedTotals(await q.CountAsync(ct), await q.SumAsync(e => (decimal?)e.Amount, ct) ?? 0);
            }
            case OneCStreams.HistoryBalances:
            {
                var month = OneCFormat.MonthStart(from);
                var q = db.OneCBalances.AsNoTracking().Where(b => b.SourceOrganizationId == organizationId && !b.IsDeleted && b.Period == month);
                return new ImportedTotals(await q.CountAsync(ct), await q.SumAsync(b => (decimal?)b.TurnoverDebit, ct) ?? 0);
            }
            case OneCStreams.HistoryRates:
            {
                var q = db.OneCExchangeRates.AsNoTracking().Where(r => !r.IsDeleted && r.Date >= from && r.Date <= to);
                return new ImportedTotals(await q.CountAsync(ct), 0);
            }
            default:
                throw new OneCImportException($"Неизвестный поток: {stream}.");
        }
    }

    private static void Check(ImportBatchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BatchKey) || request.BatchKey.Length > 100)
        {
            throw new OneCImportException("Ключ пакета обязателен (до 100 символов).");
        }

        if (string.IsNullOrWhiteSpace(request.Stream) || request.Stream.Length > 64)
        {
            throw new OneCImportException("Поток пакета обязателен (до 64 символов).");
        }

        if (request.OrganizationId is { Length: > 64 })
        {
            throw new OneCImportException("Идентификатор организации длиннее 64 символов.");
        }

        if (request.Count > MaxRecords)
        {
            throw new OneCImportException($"В пакете {request.Count} записей — больше {MaxRecords}. Уменьшите размер пакета.");
        }

        var entries = request.EntrySets?.Sum(s => s.Entries.Count) ?? 0;
        if (entries > MaxEntries)
        {
            throw new OneCImportException($"В пакете {entries} проводок — больше {MaxEntries}.");
        }

        if (request.Snapshot is { } snapshot && (request.Balances ?? []).Any(b => b.Period != snapshot.Period || b.OrganizationId != snapshot.OrganizationId))
        {
            throw new OneCImportException("Строки остатков не совпадают со снимком (организация или месяц).");
        }
    }

    private static ImportBatchResult Replay(OneCImportBatch batch) => new()
    {
        BatchKey = batch.BatchKey, Replayed = true, Created = batch.Created, Updated = batch.Updated, Unchanged = batch.Unchanged,
        Deleted = batch.Deleted, Rejected = batch.Rejected,
        Rejections = batch.RejectionsJson is null ? [] : JsonSerializer.Deserialize<List<ImportRejection>>(batch.RejectionsJson, OneCFormat.Json) ?? [],
    };

    // ---------- Справочники ----------

    private Task OrganizationsAsync(Counters c, List<ImportOrganization>? records, CancellationToken ct) =>
        UpsertAsync(c, db.OneCOrganizations, Valid(c, OneCEntityTypes.Organization, records, r => Need(r.Name, "наименование")),
            r => (OneCEntityTypes.Shared, OneCEntityTypes.Organization),
            (r, type, id) => new OneCOrganization { SourceEntityType = type, SourceEntityId = id, Name = r.Name },
            (r, e) =>
            {
                e.Code = r.Code; e.Name = r.Name; e.FullName = r.FullName; e.Inn = r.Inn; e.VatCode = r.VatCode;
                e.AccountingCurrency = Upper(r.AccountingCurrency) ?? "UZS"; e.IsActive = r.IsActive;
            }, null, ct);

    private Task AccountsAsync(Counters c, List<ImportAccount>? records, CancellationToken ct) =>
        UpsertAsync(c, db.OneCAccounts, Valid(c, OneCEntityTypes.Account, records, r => Need(r.Code, "код счёта") ?? Need(r.Name, "наименование")
                ?? (r.Type is "active" or "passive" or "active_passive" ? null : $"неизвестный вид счёта «{r.Type}»")),
            r => (OneCEntityTypes.Shared, OneCEntityTypes.Account),
            (r, type, id) => new OneCAccount { SourceEntityType = type, SourceEntityId = id, Code = r.Code, Name = r.Name },
            (r, e) =>
            {
                e.Code = r.Code.Trim(); e.Name = r.Name; e.ParentCode = r.ParentCode; e.Type = r.Type; e.OffBalance = r.OffBalance;
                e.IsCurrency = r.IsCurrency; e.IsQuantity = r.IsQuantity;
                e.Subconto = JsonSerializer.Serialize(r.Subconto.OrderBy(s => s.Index), OneCFormat.Json);
            }, null, ct);

    private Task CurrenciesAsync(Counters c, List<ImportCurrency>? records, CancellationToken ct) =>
        UpsertAsync(c, db.OneCCurrencies, Valid(c, OneCEntityTypes.Currency, records, r => CurrencyCode(r.Code) ?? Need(r.Name, "наименование")),
            r => (OneCEntityTypes.Shared, OneCEntityTypes.Currency),
            (r, type, id) => new OneCCurrency { SourceEntityType = type, SourceEntityId = id, Code = Upper(r.Code)!, Name = r.Name },
            (r, e) => { e.Code = Upper(r.Code)!; e.NumericCode = r.NumericCode; e.Name = r.Name; }, null, ct);

    private Task RatesAsync(Counters c, List<ImportExchangeRate>? records, CancellationToken ct) =>
        UpsertAsync(c, db.OneCExchangeRates, Valid(c, OneCEntityTypes.ExchangeRate, records, r => CurrencyCode(r.Currency)
                ?? (r.Rate > 0 ? null : "курс должен быть больше нуля") ?? (r.Multiplier >= 1 ? null : "кратность должна быть не меньше 1")
                ?? (r.SourceId == OneCEntityTypes.RateKey(r.Currency, r.Date) ? null : "ключ курса должен быть ВАЛЮТА:ГГГГ-ММ-ДД")),
            r => (OneCEntityTypes.Shared, OneCEntityTypes.ExchangeRate),
            (r, type, id) => new OneCExchangeRate { SourceEntityType = type, SourceEntityId = id, Currency = Upper(r.Currency)! },
            (r, e) => { e.Currency = Upper(r.Currency)!; e.Date = r.Date; e.Rate = r.Rate; e.Multiplier = r.Multiplier; e.RateSource = r.Source; }, null, ct);

    private Task CounterpartiesAsync(Counters c, List<ImportCounterparty>? records, CancellationToken ct) =>
        UpsertAsync(c, db.OneCCounterparties, Valid(c, OneCEntityTypes.Counterparty, records, r => Need(r.Name, "наименование")),
            r => (OneCEntityTypes.Shared, OneCEntityTypes.Counterparty),
            (r, type, id) => new OneCCounterparty { SourceEntityType = type, SourceEntityId = id, Name = r.Name },
            (r, e) =>
            {
                e.Code = r.Code; e.Name = r.Name; e.FullName = r.FullName; e.Inn = r.Inn; e.Kind = r.Kind; e.LegalForm = r.LegalForm;
                e.ParentSourceId = r.ParentId; e.IsGroup = r.IsGroup; e.Address = r.Address; e.Phone = r.Phone;
                e.BankAccounts = JsonSerializer.Serialize(r.BankAccounts, OneCFormat.Json);
            }, null, ct);

    private Task ContractsAsync(Counters c, List<ImportContract>? records, CancellationToken ct) =>
        UpsertAsync(c, db.OneCContracts, Valid(c, OneCEntityTypes.Contract, records, r => Need(r.Name, "наименование") ?? Need(r.CounterpartyId, "контрагент")
                ?? Org(c, r.OrganizationId) ?? CurrencyCode(r.Currency)),
            r => (r.OrganizationId, OneCEntityTypes.Contract),
            (r, type, id) => new OneCContract { SourceEntityType = type, SourceEntityId = id, Name = r.Name, CounterpartySourceId = r.CounterpartyId },
            (r, e) =>
            {
                e.CounterpartySourceId = r.CounterpartyId; e.Code = r.Code; e.Name = r.Name; e.Number = r.Number; e.Date = r.Date; e.Kind = r.Kind;
                e.Currency = Upper(r.Currency)!; e.PaymentTermDays = r.PaymentTermDays; e.ValidTo = r.ValidTo;
            }, null, ct);

    private Task ReferencesAsync(Counters c, List<ImportReference>? records, CancellationToken ct) =>
        UpsertAsync(c, db.OneCReferences, Valid(c, "reference", records, r => Need(r.Name, "наименование")
                ?? (r.Kind is OneCEntityTypes.Warehouse or OneCEntityTypes.Department || r.Kind.StartsWith(OneCEntityTypes.ReferencePrefix, StringComparison.Ordinal) && r.Kind.Length <= 48
                    ? null : $"неизвестный вид справочника «{r.Kind}»")
                ?? (string.IsNullOrEmpty(r.OrganizationId) ? null : Org(c, r.OrganizationId))),
            r => (r.OrganizationId ?? OneCEntityTypes.Shared, r.Kind),
            (r, type, id) => new OneCReference { SourceEntityType = type, SourceEntityId = id, Name = r.Name },
            (r, e) =>
            {
                e.Code = r.Code; e.Name = r.Name; e.ParentSourceId = r.ParentId; e.IsGroup = r.IsGroup;
                e.Attributes = r.Attributes is { Count: > 0 } ? JsonSerializer.Serialize(r.Attributes, OneCFormat.Json) : null;
            }, null, ct);

    private Task ItemsAsync(Counters c, List<ImportItem>? records, CancellationToken ct) =>
        UpsertAsync(c, db.OneCItems, Valid(c, OneCEntityTypes.Item, records, r => Need(r.Name, "наименование")),
            r => (OneCEntityTypes.Shared, OneCEntityTypes.Item),
            (r, type, id) => new OneCItem { SourceEntityType = type, SourceEntityId = id, Name = r.Name },
            (r, e) =>
            {
                e.Code = r.Code; e.Name = r.Name; e.FullName = r.FullName; e.Article = r.Article; e.Kind = r.Kind; e.Unit = r.Unit;
                e.ParentSourceId = r.ParentId; e.IsGroup = r.IsGroup; e.VatRate = r.VatRate; e.Ikpu = r.Ikpu; e.Barcode = r.Barcode;
            }, null, ct);

    // ---------- Документы ----------

    private Task DocumentsAsync(Counters c, List<ImportDocument>? records, CancellationToken ct) =>
        UpsertAsync(c, db.OneCDocuments, Valid(c, OneCEntityTypes.Document, records, r => Org(c, r.OrganizationId) ?? DocumentError(r)),
            r => (r.OrganizationId, OneCEntityTypes.Document),
            (r, type, id) => new OneCDocument { SourceEntityType = type, SourceEntityId = id, Type = r.Type, SourceType = r.SourceType, Number = r.Number },
            (r, e) =>
            {
                e.Type = r.Type; e.SourceType = r.SourceType; e.Number = r.Number; e.Date = r.Date.ToUniversalTime(); e.DocumentDate = OneCFormat.LocalDate(r.Date); e.Posted = r.Posted;
                e.CounterpartySourceId = r.CounterpartyId; e.ContractSourceId = r.ContractId; e.WarehouseSourceId = r.WarehouseId;
                e.DepartmentSourceId = r.DepartmentId; e.Currency = Upper(r.Currency)!; e.Amount = r.Amount; e.VatAmount = r.VatAmount;
                e.AmountAccounting = r.AmountAccounting; e.RateValue = r.Rate?.Value; e.RateMultiplier = r.Rate?.Multiplier;
                e.RateDate = r.Rate?.Date; e.RateSource = r.Rate?.Source; e.DueDate = r.DueDate; e.BasisSourceId = r.BasisDocumentId;
                e.CashFlowItemSourceId = r.CashFlowItemId; e.BankAccountSourceId = r.BankAccountId; e.CashDeskSourceId = r.CashDeskId;
                e.Purpose = r.Purpose; e.Comment = r.Comment;
                foreach (var old in e.Lines.ToList())
                {
                    db.OneCDocumentLines.Remove(old);
                }

                e.Lines = r.Lines.Select(l => new OneCDocumentLine
                {
                    DocumentId = e.Id, LineNumber = l.LineNumber, ItemSourceId = l.ItemId, Quantity = l.Quantity, Unit = l.Unit, Price = l.Price,
                    Amount = l.Amount, VatRate = l.VatRate, VatAmount = l.VatAmount, CostAmount = l.CostAmount, Account = l.Account,
                    WarehouseSourceId = l.WarehouseId, BatchSourceId = l.BatchId,
                }).ToList();

                // Ключ строки задан заранее — без явного Add EF принял бы новые строки существующего документа за старые (UPDATE вместо INSERT).
                db.OneCDocumentLines.AddRange(e.Lines);
            },
            async (existing, token) =>
            {
                // Строки прежних версий — чтобы заменить их новой версией документа.
                var ids = existing.Select(d => d.Id).ToList();
                if (ids.Count > 0)
                {
                    await db.OneCDocumentLines.Where(l => ids.Contains(l.DocumentId)).LoadAsync(token);
                }
            }, ct);

    private string? DocumentError(ImportDocument r) =>
        Need(r.Number, "номер") ?? Need(r.SourceType, "тип документа 1С")
        ?? (OneCDocumentTypes.IsKnown(r.Type) ? null : $"неизвестный тип документа «{r.Type}»")
        ?? Date(r.Date) ?? CurrencyCode(r.Currency)
        ?? (r.Rate is { } rate && (rate.Value <= 0 || rate.Multiplier < 1) ? "курс документа должен быть больше нуля" : null)
        ?? (r.Lines.Select(l => l.LineNumber).Distinct().Count() == r.Lines.Count ? null : "номера строк повторяются");

    // ---------- Проводки ----------

    /// <summary>
    /// Набор проводок документа заменяет прежний: совпавшие строки обновляются, новые — добавляются, пропавшие — помечаются удалёнными.
    /// Пустой набор — отмена проведения. В журнал — одна запись на набор (перепроведение или отмена) со старой и новой суммой.
    /// </summary>
    private async Task EntrySetsAsync(Counters c, List<ImportEntrySet>? records, CancellationToken ct)
    {
        var sets = Valid(c, OneCEntityTypes.Entry, records, r => Org(c, r.OrganizationId) ?? EntrySetError(r));
        if (sets.Count == 0)
        {
            return;
        }

        var registrars = sets.Select(s => s.SourceId).Distinct().ToList();
        var existing = await db.OneCEntries.Where(e => e.SourceSystem == Source && registrars.Contains(e.RegistrarSourceId)).ToListAsync(ct);
        var byRegistrar = existing.GroupBy(e => (e.SourceOrganizationId, e.RegistrarSourceId)).ToDictionary(g => g.Key, g => g.ToDictionary(e => e.LineNumber));

        foreach (var set in sets)
        {
            var org = set.OrganizationId;
            var old = byRegistrar.TryGetValue((org, set.SourceId), out var lines) ? lines : [];
            byRegistrar[(org, set.SourceId)] = old;
            var oldSum = old.Values.Where(e => !e.IsDeleted).Sum(e => e.Amount);
            var oldCount = old.Values.Count(e => !e.IsDeleted);
            var changed = false;
            var seen = new HashSet<int>();
            foreach (var entry in set.Deleted ? [] : set.Entries)
            {
                seen.Add(entry.LineNumber);
                var hash = OneCFormat.Hash(new { set.RegistrarType, set.RegistrarSourceType, set.RegistrarNumber, set.RegistrarDate, entry });
                if (old.TryGetValue(entry.LineNumber, out var e))
                {
                    if (e.ContentHash == hash && !e.IsDeleted)
                    {
                        c.Unchanged++;
                        continue;
                    }

                    if (e.IsDeleted)
                    {
                        e.IsDeleted = false;
                        e.DeletedAt = null;
                    }

                    ApplyEntry(set, entry, e);
                    Touch(c, e, set, hash);
                    c.Updated++;
                    changed = true;
                    continue;
                }

                var created = new OneCEntry
                {
                    SourceEntityType = OneCEntityTypes.Entry, SourceEntityId = OneCEntityTypes.EntryKey(set.SourceId, entry.LineNumber),
                    SourceOrganizationId = org, RegistrarSourceId = set.SourceId, RegistrarType = set.RegistrarType,
                    DebitAccount = entry.Debit.Account, CreditAccount = entry.Credit.Account, ImportedAt = c.Now,
                };
                ApplyEntry(set, entry, created);
                Touch(c, created, set, hash);
                db.OneCEntries.Add(created);
                old[entry.LineNumber] = created;
                c.Created++;
                changed |= oldCount > 0;
            }

            foreach (var gone in old.Values.Where(e => !e.IsDeleted && !seen.Contains(e.LineNumber)))
            {
                gone.IsDeleted = true;
                gone.DeletedAt = c.Now;
                gone.UpdatedAt = c.Now;
                gone.LastRunId = c.RunId;
                c.Deleted++;
                changed = true;
            }

            if (changed)
            {
                var active = old.Values.Where(e => !e.IsDeleted).ToList();
                var action = active.Count == 0 ? "cancelled" : "reposted";
                c.Log(OneCEntityTypes.Entry, org, set.SourceId, null, action, set.Version,
                    JsonSerializer.Serialize(new Dictionary<string, object?[]>
                    {
                        ["entries"] = [oldCount, active.Count], ["amount"] = [oldSum, active.Sum(e => e.Amount)],
                    }, OneCFormat.Json));
            }
        }
    }

    private static string? EntrySetError(ImportEntrySet s)
    {
        if (string.IsNullOrWhiteSpace(s.OrganizationId))
        {
            return "не указана организация";
        }

        if (s.Entries.Select(e => e.LineNumber).Distinct().Count() != s.Entries.Count)
        {
            return "номера строк проводок повторяются";
        }

        foreach (var e in s.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.Debit.Account) || string.IsNullOrWhiteSpace(e.Credit.Account))
            {
                return $"строка {e.LineNumber}: не указан счёт дебета или кредита";
            }

            if (Date(e.Period) is { } error)
            {
                return $"строка {e.LineNumber}: {error}";
            }

            if (e.Debit.Currency is { } dc && CurrencyCode(dc) is { } de)
            {
                return $"строка {e.LineNumber}: {de}";
            }

            if (e.Credit.Currency is { } cc && CurrencyCode(cc) is { } ce)
            {
                return $"строка {e.LineNumber}: {ce}";
            }
        }

        return null;
    }

    private static void ApplyEntry(ImportEntrySet set, ImportEntry entry, OneCEntry e)
    {
        e.RegistrarType = set.RegistrarType;
        e.RegistrarSourceType = set.RegistrarSourceType;
        e.RegistrarNumber = set.RegistrarNumber;
        e.RegistrarDate = set.RegistrarDate?.ToUniversalTime();
        e.LineNumber = entry.LineNumber;
        e.Period = entry.Period.ToUniversalTime();
        e.PeriodDate = OneCFormat.LocalDate(entry.Period);
        e.Active = entry.Active;
        e.Amount = entry.Amount;
        e.Content = entry.Content is { Length: > 500 } content ? content[..500] : entry.Content;
        e.DepartmentSourceId = entry.DepartmentId;
        e.DebitAccount = entry.Debit.Account.Trim();
        e.DebitSubconto = JsonSerializer.Serialize(entry.Debit.Subconto.OrderBy(s => s.Index), OneCFormat.Json);
        e.DebitCurrency = Upper(entry.Debit.Currency);
        e.DebitCurrencyAmount = entry.Debit.CurrencyAmount;
        e.DebitQuantity = entry.Debit.Quantity;
        e.DebitCounterpartyId = OneCFormat.SubcontoId(entry.Debit.Subconto, "counterparty");
        e.DebitItemId = OneCFormat.SubcontoId(entry.Debit.Subconto, "item");
        e.DebitWarehouseId = OneCFormat.SubcontoId(entry.Debit.Subconto, "warehouse");
        e.CreditAccount = entry.Credit.Account.Trim();
        e.CreditSubconto = JsonSerializer.Serialize(entry.Credit.Subconto.OrderBy(s => s.Index), OneCFormat.Json);
        e.CreditCurrency = Upper(entry.Credit.Currency);
        e.CreditCurrencyAmount = entry.Credit.CurrencyAmount;
        e.CreditQuantity = entry.Credit.Quantity;
        e.CreditCounterpartyId = OneCFormat.SubcontoId(entry.Credit.Subconto, "counterparty");
        e.CreditItemId = OneCFormat.SubcontoId(entry.Credit.Subconto, "item");
        e.CreditWarehouseId = OneCFormat.SubcontoId(entry.Credit.Subconto, "warehouse");
        e.RateValue = entry.Rate?.Value;
        e.RateMultiplier = entry.Rate?.Multiplier;
        e.RateDate = entry.Rate?.Date;
        e.RateSource = entry.Rate?.Source;
    }

    private static void Touch(Counters c, OneCEntry e, ImportEntrySet set, string hash)
    {
        e.SourceVersion = set.Version;
        e.SourceUpdatedAt = set.ModifiedAt?.ToUniversalTime();
        e.ContentHash = hash;
        e.UpdatedAt = c.Now;
        e.LastRunId = c.RunId;
    }

    // ---------- Остатки ----------

    /// <summary>
    /// Строки оборотно-сальдовой ведомости за месяц. Снимок может идти несколькими пакетами: каждая строка получает идентификатор снимка,
    /// а последний пакет (Final) помечает удалёнными строки месяца, которых в новом снимке нет.
    /// </summary>
    private async Task BalancesAsync(Counters c, List<ImportBalance>? records, BalanceSnapshot? snapshot, CancellationToken ct)
    {
        var rows = new List<(ImportBalance Record, string Key)>();
        foreach (var r in records ?? [])
        {
            var key = OneCFormat.BalanceKey(r);
            var error = Need(r.Account, "счёт") ?? (r.Period.Day == 1 ? null : "период остатков — первое число месяца")
                ?? (string.IsNullOrEmpty(r.Currency) ? null : CurrencyCode(r.Currency)) ?? Org(c, r.OrganizationId);
            if (error is not null)
            {
                c.Reject(OneCEntityTypes.Balance, key, error, r);
                continue;
            }

            rows.Add((r, key));
        }

        var snapshotId = snapshot?.SnapshotId ?? Guid.Empty;
        if (rows.Count > 0)
        {
            var keys = rows.Select(r => r.Key).Distinct().ToList();
            var existing = await db.OneCBalances.Where(b => b.SourceSystem == Source && keys.Contains(b.SourceEntityId)).ToListAsync(ct);
            var map = existing.ToDictionary(b => (b.SourceOrganizationId, b.SourceEntityId));
            foreach (var (r, key) in rows)
            {
                var hash = OneCFormat.Hash(r);
                if (map.TryGetValue((r.OrganizationId, key), out var b))
                {
                    b.SnapshotId = snapshotId;
                    if (b.ContentHash == hash && !b.IsDeleted)
                    {
                        c.Unchanged++;
                        continue;
                    }

                    var before = OneCFormat.Snapshot(b);
                    var restored = b.IsDeleted;
                    ApplyBalance(r, b);
                    b.IsDeleted = false;
                    b.DeletedAt = null;
                    b.ContentHash = hash;
                    b.UpdatedAt = c.Now;
                    b.LastRunId = c.RunId;
                    c.Updated++;
                    c.Log(OneCEntityTypes.Balance, r.OrganizationId, key, b.Id, restored ? "restored" : "updated", null,
                        OneCFormat.Diff(before, OneCFormat.Snapshot(b)));
                    continue;
                }

                var created = new OneCBalance
                {
                    SourceEntityType = OneCEntityTypes.Balance, SourceEntityId = key, SourceOrganizationId = r.OrganizationId, Account = r.Account,
                    ImportedAt = c.Now, UpdatedAt = c.Now, ContentHash = hash, LastRunId = c.RunId, SnapshotId = snapshotId,
                };
                ApplyBalance(r, created);
                db.OneCBalances.Add(created);
                map[(r.OrganizationId, key)] = created;
                c.Created++;
            }
        }

        if (snapshot is { Final: true })
        {
            await db.SaveChangesAsync(ct);
            var q = db.OneCBalances.Where(b => b.SourceSystem == Source && b.SourceOrganizationId == snapshot.OrganizationId
                && b.Period == snapshot.Period && !b.IsDeleted && b.SnapshotId != snapshot.SnapshotId);
            if (snapshot.Accounts is { Count: > 0 } accounts)
            {
                q = q.Where(b => accounts.Contains(b.Account));
            }

            foreach (var gone in await q.ToListAsync(ct))
            {
                gone.IsDeleted = true;
                gone.DeletedAt = c.Now;
                gone.UpdatedAt = c.Now;
                gone.LastRunId = c.RunId;
                c.Deleted++;
                c.Log(OneCEntityTypes.Balance, gone.SourceOrganizationId, gone.SourceEntityId, gone.Id, "deleted", null, null);
            }
        }
    }

    private static void ApplyBalance(ImportBalance r, OneCBalance b)
    {
        b.Period = r.Period;
        b.Account = r.Account.Trim();
        b.Subconto = JsonSerializer.Serialize(r.Subconto.OrderBy(s => s.Index), OneCFormat.Json);
        b.Currency = Upper(r.Currency);
        b.CounterpartyId = OneCFormat.SubcontoId(r.Subconto, "counterparty");
        b.ItemId = OneCFormat.SubcontoId(r.Subconto, "item");
        b.WarehouseId = OneCFormat.SubcontoId(r.Subconto, "warehouse");
        (b.OpeningDebit, b.OpeningCredit, b.OpeningCurrencyDebit, b.OpeningCurrencyCredit, b.OpeningQuantityDebit, b.OpeningQuantityCredit) = Parts(r.Opening);
        (b.TurnoverDebit, b.TurnoverCredit, b.TurnoverCurrencyDebit, b.TurnoverCurrencyCredit, b.TurnoverQuantityDebit, b.TurnoverQuantityCredit) = Parts(r.Turnover);
        (b.ClosingDebit, b.ClosingCredit, b.ClosingCurrencyDebit, b.ClosingCurrencyCredit, b.ClosingQuantityDebit, b.ClosingQuantityCredit) = Parts(r.Closing);
    }

    private static (decimal, decimal, decimal?, decimal?, decimal?, decimal?) Parts(ImportBalanceAmounts a) =>
        (a.Debit, a.Credit, a.CurrencyDebit, a.CurrencyCredit, a.QuantityDebit, a.QuantityCredit);

    // ---------- Удаления ----------

    private async Task DeletionsAsync(Counters c, List<ImportDeletion>? deletions, CancellationToken ct)
    {
        foreach (var d in deletions ?? [])
        {
            if (!string.IsNullOrEmpty(d.OrganizationId) && !string.IsNullOrEmpty(c.Request.OrganizationId) && d.OrganizationId != c.Request.OrganizationId)
            {
                c.Reject(d.EntityType, d.SourceId, "удаление записи другой организации в пакете организации", d);
                continue;
            }

            OneCRecord? row = d.EntityType switch
            {
                OneCEntityTypes.Organization => await Find(db.OneCOrganizations, d, ct),
                OneCEntityTypes.Account => await Find(db.OneCAccounts, d, ct),
                OneCEntityTypes.Counterparty => await Find(db.OneCCounterparties, d, ct),
                OneCEntityTypes.Contract => await Find(db.OneCContracts, d, ct),
                OneCEntityTypes.Currency => await Find(db.OneCCurrencies, d, ct),
                OneCEntityTypes.ExchangeRate => await Find(db.OneCExchangeRates, d, ct),
                OneCEntityTypes.Item => await Find(db.OneCItems, d, ct),
                OneCEntityTypes.Document => await Find(db.OneCDocuments, d, ct),
                _ when d.EntityType is OneCEntityTypes.Warehouse or OneCEntityTypes.Department
                       || d.EntityType.StartsWith(OneCEntityTypes.ReferencePrefix, StringComparison.Ordinal) =>
                    // Вид справочника при удалении 1С может не сообщить — ищем по идентификатору (UUID ссылки уникален).
                    await db.OneCReferences.FirstOrDefaultAsync(x => x.SourceSystem == Source && x.SourceEntityId == d.SourceId
                        && (d.OrganizationId == null || x.SourceOrganizationId == d.OrganizationId || x.SourceOrganizationId == ""), ct),
                _ => null,
            };

            if (row is null)
            {
                // Удалили то, чего в OneBase нет (создали и удалили между обменами) — нечего помечать.
                c.Unchanged++;
                continue;
            }

            if (!row.IsDeleted)
            {
                row.IsDeleted = true;
                row.DeletedAt = c.Now;
                row.UpdatedAt = c.Now;
                row.LastRunId = c.RunId;
                c.Deleted++;
                c.Log(d.EntityType, row.SourceOrganizationId, d.SourceId, row.Id, "deleted", null, null);
            }
            else
            {
                c.Unchanged++;
            }

            if (d.EntityType == OneCEntityTypes.Document)
            {
                // Документ удалён — его проводки тоже (история остаётся).
                var entries = await db.OneCEntries.Where(e => e.RegistrarSourceId == d.SourceId && e.SourceOrganizationId == row.SourceOrganizationId && !e.IsDeleted).ToListAsync(ct);
                foreach (var e in entries)
                {
                    e.IsDeleted = true;
                    e.DeletedAt = c.Now;
                    e.UpdatedAt = c.Now;
                    e.LastRunId = c.RunId;
                    c.Deleted++;
                }
            }
        }
    }

    private static async Task<OneCRecord?> Find<T>(DbSet<T> set, ImportDeletion d, CancellationToken ct) where T : OneCRecord
    {
        var org = d.OrganizationId ?? OneCEntityTypes.Shared;
        return await set.FirstOrDefaultAsync(x => x.SourceSystem == Source && x.SourceEntityType == d.EntityType && x.SourceEntityId == d.SourceId
            && (x.SourceOrganizationId == org || d.OrganizationId == null), ct);
    }

    // ---------- Общая запись справочников и документов ----------

    private async Task UpsertAsync<TRecord, TEntity>(Counters c, DbSet<TEntity> set, List<TRecord> records,
        Func<TRecord, (string Org, string Type)> keyOf, Func<TRecord, string, string, TEntity> create, Action<TRecord, TEntity> apply,
        Func<List<TEntity>, CancellationToken, Task>? afterLoad, CancellationToken ct)
        where TRecord : ImportRecord where TEntity : OneCRecord
    {
        if (records.Count == 0)
        {
            return;
        }

        var ids = records.Select(r => r.SourceId).Distinct().ToList();
        var types = records.Select(r => keyOf(r).Type).Distinct().ToList();
        var existing = await set.Where(x => x.SourceSystem == Source && types.Contains(x.SourceEntityType) && ids.Contains(x.SourceEntityId)).ToListAsync(ct);
        if (afterLoad is not null)
        {
            await afterLoad(existing, ct);
        }

        var map = existing.ToDictionary(x => (x.SourceOrganizationId, x.SourceEntityType, x.SourceEntityId));
        foreach (var record in records)
        {
            var (org, type) = keyOf(record);
            var hash = OneCFormat.Hash(record);
            if (!map.TryGetValue((org, type, record.SourceId), out var entity))
            {
                entity = create(record, type, record.SourceId);
                entity.SourceOrganizationId = org;
                apply(record, entity);
                entity.SourceVersion = record.Version;
                entity.SourceUpdatedAt = record.ModifiedAt?.ToUniversalTime();
                entity.ImportedAt = c.Now;
                entity.UpdatedAt = c.Now;
                entity.ContentHash = hash;
                entity.IsDeleted = record.Deleted;
                entity.DeletedAt = record.Deleted ? c.Now : null;
                entity.LastRunId = c.RunId;
                set.Add(entity);
                map[(org, type, record.SourceId)] = entity;
                c.Created++;
                continue;
            }

            if (entity.ContentHash == hash)
            {
                c.Unchanged++;
                continue;
            }

            var before = OneCFormat.Snapshot(entity);
            var wasDeleted = entity.IsDeleted;
            var wasPosted = (entity as OneCDocument)?.Posted;
            var oldLines = (entity as OneCDocument)?.Lines.Count;
            apply(record, entity);
            entity.SourceVersion = record.Version;
            entity.SourceUpdatedAt = record.ModifiedAt?.ToUniversalTime();
            entity.UpdatedAt = c.Now;
            entity.ContentHash = hash;
            entity.IsDeleted = record.Deleted;
            entity.DeletedAt = record.Deleted ? entity.DeletedAt ?? c.Now : null;
            entity.LastRunId = c.RunId;

            var extra = entity is OneCDocument doc && oldLines != doc.Lines.Count
                ? new Dictionary<string, object?[]> { ["Lines"] = [oldLines, doc.Lines.Count] }
                : null;
            var action = (wasDeleted, record.Deleted) switch
            {
                (false, true) => "deleted",
                (true, false) => "restored",
                _ when entity is OneCDocument d && wasPosted == true && !d.Posted => "cancelled",
                _ when entity is OneCDocument d && wasPosted == false && d.Posted => "reposted",
                _ => "updated",
            };
            if (action is "deleted")
            {
                c.Deleted++;
            }
            else
            {
                c.Updated++;
            }

            c.Log(type, org, record.SourceId, entity.Id, action, record.Version, OneCFormat.Diff(before, OneCFormat.Snapshot(entity), extra));
        }
    }

    /// <summary>Проверка записей: неверные — в отклонённые (с записью целиком), верные — дальше. Дубликат ключа в пакете — побеждает последний.</summary>
    private static List<TRecord> Valid<TRecord>(Counters c, string entityType, List<TRecord>? records, Func<TRecord, string?> error) where TRecord : ImportRecord
    {
        var valid = new List<TRecord>();
        foreach (var r in records ?? [])
        {
            var problem = string.IsNullOrWhiteSpace(r.SourceId) ? "нет идентификатора 1С"
                : r.SourceId.Length > 100 ? "идентификатор 1С длиннее 100 символов"
                : r.Version is { Length: > 64 } ? "версия данных длиннее 64 символов"
                : error(r);
            if (problem is not null)
            {
                c.Reject(entityType, r.SourceId ?? string.Empty, problem, r);
                continue;
            }

            valid.Add(r);
        }

        return valid;
    }

    private static string? Need(string? value, string what) => string.IsNullOrWhiteSpace(value) ? $"не указано: {what}" : null;

    private static string? CurrencyCode(string? code) =>
        code is { Length: 3 } && code.All(char.IsAsciiLetter) ? null : $"код валюты «{code}» — не трёхбуквенный ISO 4217";

    private static string? Date(DateTimeOffset date) => date.Year is >= 1990 and <= 2100 ? null : $"дата {date:yyyy-MM-dd} вне допустимого диапазона";

    /// <summary>Запись чужой организации в пакете организации — отклоняется: пакет одной организации не может изменить данные другой.</summary>
    private static string? Org(Counters c, string? organizationId) =>
        string.IsNullOrWhiteSpace(organizationId) ? "не указана организация"
        : !string.IsNullOrEmpty(c.Request.OrganizationId) && c.Request.OrganizationId != organizationId ? "запись другой организации в пакете организации"
        : null;

    private static string? Upper(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    private async Task SaveCheckpointAsync(ImportCheckpoint checkpoint, ImportBatchRequest request, Guid runId, DateTimeOffset now, CancellationToken ct)
    {
        var org = checkpoint.OrganizationId ?? string.Empty;
        var row = await db.OneCSyncCheckpoints.FirstOrDefaultAsync(x => x.Stream == checkpoint.Stream && x.OrganizationId == org, ct);
        if (row is null)
        {
            row = new OneCSyncCheckpoint { Stream = checkpoint.Stream, OrganizationId = org };
            db.OneCSyncCheckpoints.Add(row);
        }

        row.Cursor = checkpoint.Cursor;
        row.UpdatedAt = now;
        row.RunId = runId;
        row.BatchKey = request.BatchKey;
        row.Rows += request.Count;
    }

    private async Task SavePositionAsync(HistoricalPosition position, int count, CancellationToken ct)
    {
        var job = await db.OneCHistoricalImports.FirstOrDefaultAsync(j => j.Id == position.ImportId, ct)
            ?? throw new OneCImportException("Историческая загрузка не найдена.");
        job.PositionPeriod = position.Period;
        job.PositionOrganization = position.OrganizationIndex;
        job.PositionStream = position.Stream;
        job.PositionCursor = position.Cursor;
        job.Records += count;
    }

    /// <summary>Счётчики пакета, журнал изменений и отклонённые записи.</summary>
    private sealed class Counters(ImportBatchRequest request, DateTimeOffset now, Guid runId)
    {
        public ImportBatchRequest Request { get; } = request;
        public DateTimeOffset Now { get; } = now;
        public Guid RunId { get; } = runId;
        public int Created;
        public int Updated;
        public int Unchanged;
        public int Deleted;
        public List<ImportRejection> Rejections { get; } = [];
        public Dictionary<string, JsonElement?> Payloads { get; } = [];
        public List<OneCChange> Changes { get; } = [];

        public void Reject(string entityType, string sourceId, string reason, object record)
        {
            Rejections.Add(new ImportRejection(entityType, sourceId, reason));
            Payloads[sourceId] = JsonSerializer.SerializeToElement(record, record.GetType(), OneCFormat.Json);
        }

        public void Log(string entityType, string org, string sourceId, Guid? recordId, string action, string? version, string? changes) =>
            Changes.Add(new OneCChange
            {
                At = Now, RunId = RunId, BatchKey = Request.BatchKey, EntityType = entityType, SourceOrganizationId = org, SourceEntityId = sourceId,
                RecordId = recordId, Action = action, Changes = changes, SourceVersion = version,
            });

        public ImportBatchResult Result(string batchKey) => new()
        {
            BatchKey = batchKey, Created = Created, Updated = Updated, Unchanged = Unchanged, Deleted = Deleted, Rejected = Rejections.Count,
            Rejections = Rejections,
        };
    }
}
