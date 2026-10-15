using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.Pnl;
using OneBase.Application.Sales.Primary;
using OneBase.Domain.AI;
using OneBase.Domain.Sales;

namespace OneBase.Application.DataManager;

/// <summary>
/// Планы изменений AI Data Manager: подготовка (ничего не меняет в данных), просмотр, отмена и проверка после применения.
/// Применение и откат — <see cref="IDataChangeApplier"/>, только по кнопке администратора.
/// </summary>
public sealed class DataChangeService(
    IAppDbContext db,
    DataFileService files,
    SalesOptions salesOptions,
    SalesDataLoader loader,
    PrimaryService primary,
    SalesCacheSignal cacheSignal,
    PnlService pnl,
    DataManagerOptions options)
{
    /// <summary>Регионы вторички, в которые можно записать план: без «Завода», «К К Мерч» и старых филиалов.</summary>
    public Task<IReadOnlyList<DataRegionRef>> RegionsAsync(CancellationToken ct = default) => RegionsAsync(false, ct);

    /// <summary>
    /// Регионы вторички; withPrimaryDealers — ещё старые филиалы, которые в первичке отдельные дилеры (Sales:PrimaryStockRegions:
    /// «Андижон (эски) 2» — склад Икбола): для плана первички и дилера региона. Во вторичке они по-прежнему в текущем регионе.
    /// </summary>
    public async Task<IReadOnlyList<DataRegionRef>> RegionsAsync(bool withPrimaryDealers, CancellationToken ct = default)
    {
        var rows = await db.SalesRegions.AsNoTracking()
            .Select(r => new { r.Id, r.LinkoBranchId, r.Name, r.DirectionId, r.SupervisorName, r.DealerName })
            .ToListAsync(ct);
        var infos = rows
            .Where(r => !salesOptions.IsExcludedBranch(r.Name) && !salesOptions.IsIgnoredBranch(r.Name))
            .Select(r => new RegionInfo(r.Id, r.LinkoBranchId, r.Name, r.DirectionId, r.SupervisorName, r.DealerName))
            .ToList();
        var (current, _) = OldBranches.Merge(infos, salesOptions.OldBranchSuffix);
        var primaryOld = withPrimaryDealers
            ? infos.Where(r => current.All(c => c.Id != r.Id) && IsPrimaryDealerBranch(r.Name)).ToList()
            : [];
        return current.Concat(primaryOld)
            .OrderBy(r => r.Name)
            .Select(r => new DataRegionRef(r.Id, r.Name, r.Dealer, r.BranchId, r.Supervisor))
            .ToList();
    }

    /// <summary>Старый филиал, который в первичке — отдельный дилер (значение Sales:PrimaryStockRegions).</summary>
    public bool IsPrimaryDealerBranch(string regionName) =>
        salesOptions.PrimaryStockRegions.Values.Any(v => string.Equals(v.Trim(), regionName.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Категории отчёта (Sales:Categories): план хранится под первым типом товара категории.</summary>
    public async Task<IReadOnlyList<DataCategoryRef>> CategoriesAsync(CancellationToken ct = default)
    {
        var types = await db.LinkoProductTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var result = new List<DataCategoryRef>();
        foreach (var (name, list) in salesOptions.Categories)
        {
            var ids = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => long.TryParse(t, out var id) ? id : -1)
                .Where(id => id >= 0)
                .ToList();
            if (ids.Count == 0)
            {
                continue;
            }

            var aliases = salesOptions.PlanIndicatorCategories.GetValueOrDefault(name, []).ToList();
            aliases.AddRange(ids.Where(types.ContainsKey).Select(id => types[id]));
            result.Add(new DataCategoryRef(ids[0], name, aliases.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
        }

        return result;
    }

    public async Task<DataChangeSetView> PrepareRegionPlansAsync(DataActor actor, RegionPlanRequest request, CancellationToken ct = default)
    {
        DataFile? file = null;
        IReadOnlyList<PlanValue> values;
        string source;
        if (request.FileId is { } fileId)
        {
            var mapping = request.Mapping ?? throw new DataManagerException("Для файла нужно сопоставление столбцов (mapping).");
            var loaded = await files.LoadAsync(fileId, actor.UserId, ct);
            file = loaded.File;
            values = ReportExtraction.PlanValues(loaded.Document, mapping);
            source = "file";
        }
        else
        {
            values = request.Values ?? [];
            source = actor.ValuesSource;
        }

        if (values.Count == 0)
        {
            throw new DataManagerException("Значений плана не найдено: проверьте лист, строку заголовка и столбцы.");
        }

        if (values.Count > options.MaxChangeItems)
        {
            throw new DataManagerException($"Слишком много значений ({values.Count}); за раз — не больше {options.MaxChangeItems}.");
        }

        var periods = values.Where(v => v.Month is >= 1 and <= 12 && v.Year is >= 2020 and <= 2100)
            .Select(v => (v.Year, v.Month)).Distinct().ToList();
        if (periods.Count > 24)
        {
            throw new DataManagerException("В одном плане — не больше 24 месяцев.");
        }

        var regions = await RegionsAsync(request.Kind == PlanKind.Primary, ct);
        var categories = await CategoriesAsync(ct);
        var years = periods.Select(p => p.Year).Distinct().ToList();
        var existing = (await db.SalesRegionPlans.AsNoTracking()
                .Where(p => p.Kind == request.Kind && years.Contains(p.Year))
                .Select(p => new ExistingPlanRow(p.RegionId, p.Year, p.Month, p.CategoryId, p.PlanKg))
                .ToListAsync(ct))
            .Where(p => periods.Contains((p.Year, p.Month)))
            .ToList();

        var result = RegionPlanPlanner.Plan(request.Kind, request.Replace, values, regions, categories, existing);
        var impact = await ImpactAsync(request.Kind, result.Totals, ct);
        var period = periods.Count switch
        {
            0 => null,
            1 => $"{periods[0].Year:D4}-{periods[0].Month:D2}",
            _ => $"{periods.Min().Year:D4}-{periods.Min().Month:D2}…{periods.Max().Year:D4}-{periods.Max().Month:D2}",
        };

        var notes = new List<string>();
        if (request.Note is { Length: > 0 } note)
        {
            notes.Add(note.Length > 500 ? note[..500] : note);
        }

        var requestHash = Hash(DataTargets.RegionPlans, request.Kind.ToString(), request.Replace ? "replace" : "upsert",
            file?.Sha256 ?? "", JsonSerializer.Serialize(request.Mapping, DataTargets.Json),
            request.FileId is null ? JsonSerializer.Serialize(values, DataTargets.Json) : "");
        var previous = await PreviousVersionAsync(DataTargets.RegionPlans, request.Kind.ToString(), period, file, result.Items, ct);
        var title = $"{DataTargets.Label(DataTargets.RegionPlans)}: {DataEntityTypes.PlanKindLabel(request.Kind)} · "
                    + (periods.Count == 1 ? DataEntityTypes.MonthLabel(periods[0].Year, periods[0].Month) : period ?? "без периода")
                    + (file is null ? actor.ValuesLabel : $" · {file.FileName}");

        return await SaveAsync(actor, DataTargets.RegionPlans, title, period, request.Kind.ToString(), request.Replace ? "replace" : "upsert",
            file, requestHash, result.Items, result.Warnings, result.Totals, impact, previous, source, request.Mapping?.Sheet, result.SkippedEmpty,
            notes, ct);
    }

    public async Task<DataChangeSetView> PrepareTargetsAsync(DataActor actor, IReadOnlyDictionary<string, decimal?> values, string? note, CancellationToken ct = default)
    {
        if (values.Count == 0)
        {
            throw new DataManagerException("Не указано ни одной цели.");
        }

        var current = await db.SalesTargets.AsNoTracking().ToDictionaryAsync(t => t.Key, t => t.Value, ct);
        var items = TargetPlanner.Plan(values, current, "из сообщения");
        var hash = Hash(DataTargets.SalesTargets, JsonSerializer.Serialize(values.OrderBy(v => v.Key), DataTargets.Json));
        return await SaveAsync(actor, DataTargets.SalesTargets, DataTargets.Label(DataTargets.SalesTargets), null, null, "upsert", null, hash,
            items, [], [], [], null, actor.ValuesSource, null, 0, note is { Length: > 0 } ? [note] : [], ct);
    }

    public async Task<DataChangeSetView> PrepareRegionProfilesAsync(DataActor actor, IReadOnlyList<RegionProfileValue> values, Guid? fileId, string? note,
        CancellationToken ct = default)
    {
        if (values.Count == 0)
        {
            throw new DataManagerException("Не указано ни одного изменения.");
        }

        DataFile? file = null;
        if (fileId is { } id)
        {
            file = (await files.LoadAsync(id, actor.UserId, ct)).File;
        }

        var items = RegionProfilePlanner.Plan(values, await RegionsAsync(true, ct));
        var hash = Hash(DataTargets.RegionProfiles, file?.Sha256 ?? "", JsonSerializer.Serialize(values, DataTargets.Json));
        return await SaveAsync(actor, DataTargets.RegionProfiles, DataTargets.Label(DataTargets.RegionProfiles)
                                                                 + (file is null ? actor.Source is { } src ? $" · {src}" : "" : $" · {file.FileName}"), null, null, "upsert",
            file, hash, items, [], [], [], null, file is null ? actor.ValuesSource : "file", null, 0, note is { Length: > 0 } ? [note] : [], ct);
    }

    /// <summary>
    /// План изменений P&L дилеров: исходные строки (продажи, себестоимость, расходы) из отчёта или сообщения против sales.PnlValues.
    /// Расчётные строки отчёта (валовая, EBIT…) не пишутся — сверяются с расчётом OneBase.
    /// </summary>
    public async Task<DataChangeSetView> PreparePnlAsync(DataActor actor, PnlRequest request, CancellationToken ct = default)
    {
        DataFile? file = null;
        IReadOnlyList<PnlRawValue> values;
        if (request.FileId is { } fileId)
        {
            var mapping = request.Mapping ?? throw new DataManagerException("Для файла нужно сопоставление столбцов (mapping).");
            var loaded = await files.LoadAsync(fileId, actor.UserId, ct);
            file = loaded.File;
            values = PnlExtraction.Values(loaded.Document, mapping);
        }
        else
        {
            values = request.Values ?? [];
        }

        if (values.Count == 0)
        {
            throw new DataManagerException("Значений P&L не найдено: проверьте лист, строку заголовка и столбцы.");
        }

        var periods = values.Where(v => v.Month is >= 1 and <= 12 && v.Year is >= 2020 and <= 2100)
            .Select(v => (v.Year, v.Month)).Distinct().Order().ToList();
        if (periods.Count > 24)
        {
            throw new DataManagerException("В одном плане — не больше 24 месяцев.");
        }

        var years = periods.Select(p => p.Year).Distinct().ToList();
        var existing = (await db.SalesPnlValues.AsNoTracking()
                .Where(v => years.Contains(v.Year))
                .Select(v => new ExistingPnlRow(v.Year, v.Month, v.Dealer, v.DealerKey, v.Line, v.Amount))
                .ToListAsync(ct))
            .Where(v => periods.Contains((v.Year, v.Month)))
            .ToList();
        var known = (await db.SalesPnlValues.AsNoTracking()
                .GroupBy(v => v.DealerKey)
                .Select(g => new { g.Key, Dealer = g.Max(v => v.Dealer) })
                .ToListAsync(ct))
            .ToDictionary(d => d.Key, d => d.Dealer!);

        var result = PnlPlanner.Plan(request.Replace, request.Mapping?.SumRows ?? false, values, request.LineMap, request.DealerMap, existing, known);
        var items = result.Items.Where(i => i.Action != DataChangeAction.Unchanged || result.Items.Count <= options.MaxChangeItems).ToList();
        if (items.Count > options.MaxChangeItems)
        {
            throw new DataManagerException($"Слишком много строк ({items.Count}); за раз — не больше {options.MaxChangeItems}. Разбейте отчёт по месяцам.");
        }

        var period = periods.Count switch
        {
            0 => null,
            1 => $"{periods[0].Year:D4}-{periods[0].Month:D2}",
            _ => $"{periods[0].Year:D4}-{periods[0].Month:D2}…{periods[^1].Year:D4}-{periods[^1].Month:D2}",
        };
        var notes = new List<string>();
        if (request.Note is { Length: > 0 } note)
        {
            notes.Add(note.Length > 500 ? note[..500] : note);
        }

        if (items.Count < result.Items.Count)
        {
            notes.Add($"Строк без изменений: {result.Items.Count - items.Count} — в плане не показаны.");
        }

        var hash = Hash(DataTargets.Pnl, request.Replace ? "replace" : "upsert", file?.Sha256 ?? "",
            JsonSerializer.Serialize(request.Mapping, DataTargets.Json), JsonSerializer.Serialize(request.LineMap, DataTargets.Json),
            JsonSerializer.Serialize(request.DealerMap, DataTargets.Json),
            request.FileId is null ? JsonSerializer.Serialize(values, DataTargets.Json) : "");
        // та же строка P&L того же дилера уже применялась из другого файла — это новая версия отчёта
        var previous = await PreviousVersionAsync(DataTargets.Pnl, null, period, file, items, ct) is { CorrectedRows: > 0 } p ? p : null;
        var title = $"{DataTargets.Label(DataTargets.Pnl)} · "
                    + (periods.Count == 1 ? DataEntityTypes.MonthLabel(periods[0].Year, periods[0].Month) : period ?? "без периода")
                    + (file is null ? actor.ValuesLabel : $" · {file.FileName}");
        return await SaveAsync(actor, DataTargets.Pnl, title, period, null, request.Replace ? "replace" : "upsert",
            file, hash, items, result.Warnings, [], [], previous, file is null ? actor.ValuesSource : "file", request.Mapping?.Sheet, result.SkippedEmpty,
            notes, ct, result.Summary);
    }

    private async Task<DataChangeSetView> SaveAsync(
        DataActor actor,
        string target,
        string title,
        string? period,
        string? planKind,
        string mode,
        DataFile? file,
        string requestHash,
        IReadOnlyList<PlannedItem> items,
        IReadOnlyList<string> warnings,
        IReadOnlyList<RegionPlanTotal> totals,
        IReadOnlyList<ChangeImpact> impact,
        PreviousVersionInfo? previous,
        string source,
        string? sheet,
        int skippedEmpty,
        List<string> notes,
        CancellationToken ct,
        PnlSummary? pnlSummary = null)
    {
        var counts = Count(items);

        // Тот же отчёт с тем же сопоставлением уже применён — это видно в плане, новые строки не появятся (значения совпадут).
        var applied = await db.DataChangeSets.AsNoTracking()
            .Where(s => s.RequestHash == requestHash && (s.Status == DataChangeSetStatus.Applied || s.Status == DataChangeSetStatus.PartiallyRolledBack))
            .OrderByDescending(s => s.AppliedAt)
            .Select(s => new { s.Id, s.AppliedAt })
            .FirstOrDefaultAsync(ct);
        if (applied is not null)
        {
            notes.Add($"Этот же запрос уже применён {applied.AppliedAt:dd.MM.yyyy HH:mm} (операция {applied.Id}). "
                      + (counts.Applicable == 0 ? "Новых изменений нет." : $"С тех пор данные отличаются: изменений {counts.Applicable}."));
        }

        // Повторная подготовка того же плана в том же чате не плодит черновики: если строки те же — возвращается прежний.
        var drafts = await db.DataChangeSets
            .Include(s => s.Items)
            .Where(s => s.RequestHash == requestHash && s.UserId == actor.UserId && s.ConversationId == actor.ConversationId
                        && s.Status == DataChangeSetStatus.Draft)
            .ToListAsync(ct);
        foreach (var draft in drafts)
        {
            if (SameItems(draft.Items, items))
            {
                return (await GetAsync(draft.Id, ct))!;
            }

            draft.Status = DataChangeSetStatus.Cancelled;
            draft.Error = "Заменён новым планом по тому же запросу.";
            draft.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var summary = new ChangeSetSummary(counts, warnings, totals, impact, previous, source, file?.FileName, sheet, skippedEmpty, notes, pnlSummary);
        var set = new DataChangeSet
        {
            UserId = actor.UserId,
            ConversationId = actor.ConversationId,
            FileId = file?.Id,
            FileSha256 = file?.Sha256,
            Target = target,
            Title = title.Length > 300 ? title[..300] : title,
            Period = period,
            PlanKind = planKind,
            Mode = mode,
            RequestHash = requestHash,
            Summary = JsonSerializer.Serialize(summary, DataTargets.Json),
        };
        var position = 0;
        foreach (var item in items)
        {
            set.Items.Add(new DataChangeItem
            {
                Position = position++,
                EntityType = item.EntityType,
                EntityKey = item.EntityKey,
                Label = Cut(item.Label, 300),
                Action = item.Action,
                Before = item.Before,
                After = item.After,
                SourceRef = Cut(item.SourceRef, 1000),
                Message = Cut(item.Message, 1000),
                Status = item.Action is DataChangeAction.Create or DataChangeAction.Update or DataChangeAction.Delete
                    ? DataChangeItemStatus.Pending
                    : DataChangeItemStatus.Skipped,
            });
        }

        db.DataChangeSets.Add(set);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(set.Id, ct))!;
    }

    public async Task<DataChangeSetView?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var set = await db.DataChangeSets.AsNoTracking().Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (set is null)
        {
            return null;
        }

        var userIds = new[] { set.UserId, set.AppliedById ?? Guid.Empty, set.RolledBackById ?? Guid.Empty };
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var fileName = set.FileId is { } fid
            ? await db.DataFiles.AsNoTracking().Where(f => f.Id == fid).Select(f => f.FileName).FirstOrDefaultAsync(ct)
            : null;
        var counts = Count(set.Items.Select(i => new PlannedItem(i.EntityType, i.EntityKey, i.Label, i.Action, i.Before, i.After, null, null)).ToList());
        return new DataChangeSetView(
            set.Id, set.Title, set.Target, set.Status, set.Mode, set.Period, set.PlanKind, set.CreatedAt, set.UserId,
            users.GetValueOrDefault(set.UserId), set.ConversationId, set.FileId, fileName,
            JsonDocument.Parse(set.Summary).RootElement.Clone(),
            set.Items.OrderBy(i => i.Position)
                .Select(i => new DataChangeItemView(i.Id, i.Position, i.EntityType, i.Label, i.Action, i.Before, i.After, i.SourceRef, i.Message, i.Status))
                .ToList(),
            set.Items.Count,
            set.AppliedAt, set.AppliedById is { } a ? users.GetValueOrDefault(a) : null,
            set.Verification is null ? null : JsonDocument.Parse(set.Verification).RootElement.Clone(),
            set.RolledBackAt, set.RolledBackById is { } r ? users.GetValueOrDefault(r) : null,
            set.Error,
            CanApply: set.Status == DataChangeSetStatus.Draft && counts.Applicable > 0,
            CanCancel: set.Status is DataChangeSetStatus.Draft or DataChangeSetStatus.Stale,
            CanRollback: set.Status == DataChangeSetStatus.Applied);
    }

    /// <summary>Журнал операций: последние планы изменений (всех администраторов или одного чата).</summary>
    public async Task<IReadOnlyList<DataChangeSetSummaryView>> ListAsync(Guid? conversationId, int take, CancellationToken ct = default)
    {
        var query = db.DataChangeSets.AsNoTracking();
        if (conversationId is { } cid)
        {
            query = query.Where(s => s.ConversationId == cid);
        }

        var sets = await query.OrderByDescending(s => s.CreatedAt).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
        var userIds = sets.SelectMany(s => new[] { s.UserId, s.AppliedById ?? Guid.Empty }).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var fileIds = sets.Where(s => s.FileId != null).Select(s => s.FileId!.Value).Distinct().ToList();
        var fileNames = await db.DataFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.FileName, ct);
        return sets.Select(s => new DataChangeSetSummaryView(
                s.Id, s.Title, s.Target, s.Status, s.Period, s.PlanKind, s.CreatedAt, users.GetValueOrDefault(s.UserId),
                s.FileId is { } f ? fileNames.GetValueOrDefault(f) : null,
                JsonSerializer.Deserialize<ChangeSetSummary>(s.Summary, DataTargets.Json)?.Counts,
                s.AppliedAt, s.AppliedById is { } a ? users.GetValueOrDefault(a) : null, s.RolledBackAt))
            .ToList();
    }

    /// <summary>Отменить неприменённый план. Применённый не отменяется — для него есть откат.</summary>
    public async Task<DataChangeSetView> CancelAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var set = await db.DataChangeSets.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new DataManagerException("План изменений не найден.");
        if (set.Status is not (DataChangeSetStatus.Draft or DataChangeSetStatus.Stale))
        {
            throw new DataManagerException($"План в статусе {set.Status} отменить нельзя.");
        }

        set.Status = DataChangeSetStatus.Cancelled;
        set.Error = "Отменён администратором.";
        set.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    /// <summary>
    /// Проверка после применения: перечитать сохранённые значения из базы и пересчитать показатели OneBase
    /// (кэш отчётов сбрасывается) — расхождения с планом изменений попадают в результат.
    /// </summary>
    public async Task<VerificationResult> VerifyAsync(Guid id, CancellationToken ct = default)
    {
        var set = await db.DataChangeSets.Include(s => s.Items).FirstOrDefaultAsync(s => s.Id == id, ct)
                  ?? throw new DataManagerException("План изменений не найден.");
        if (set.Status is not (DataChangeSetStatus.Applied or DataChangeSetStatus.PartiallyRolledBack or DataChangeSetStatus.RolledBack))
        {
            throw new DataManagerException($"План в статусе {set.Status}: проверять нечего — он не применялся.");
        }

        var rolledBack = set.Status != DataChangeSetStatus.Applied;
        var problems = new List<StoredCheck>();
        var checkedCount = 0;
        foreach (var item in set.Items.Where(i => i.Status is DataChangeItemStatus.Applied or DataChangeItemStatus.RolledBack))
        {
            checkedCount++;
            var expected = item.Status == DataChangeItemStatus.RolledBack ? item.Before : item.Action == DataChangeAction.Delete ? null : item.After;
            var actual = await StoredValueAsync(item.EntityKey, ct);
            var ok = item.EntityType == DataEntityTypes.RegionProfile
                ? actual == expected
                : DataValues.ParseStored(actual) == DataValues.ParseStored(expected);
            if (!ok)
            {
                problems.Add(new StoredCheck(item.Label, expected, actual, false));
            }
        }

        cacheSignal.Invalidate();
        var kpi = new List<ChangeImpact>();
        var notes = new List<string>();
        List<PnlMonthTotal>? pnlMonths = null;
        var summary = JsonSerializer.Deserialize<ChangeSetSummary>(set.Summary, DataTargets.Json);
        if (set.Target == DataTargets.RegionPlans && Enum.TryParse<PlanKind>(set.PlanKind, out var kind) && summary is not null)
        {
            foreach (var group in summary.Totals.GroupBy(t => (t.Year, t.Month)).Take(3))
            {
                // после отката в расчёте должен быть прежний план
                var expected = rolledBack ? group.Select(t => t with { Before = t.After, After = t.Before }).ToList() : group.ToList();
                var check = await CurrentKpiAsync(kind, group.Key.Year, group.Key.Month, expected, projected: false, ct);
                kpi.Add(check.Impact);
                if (set.Status != DataChangeSetStatus.PartiallyRolledBack)
                {
                    problems.AddRange(check.Problems); // при частичном откате итог региона смешанный — сверяются только строки
                }
            }
        }
        else if (set.Target == DataTargets.SalesTargets)
        {
            notes.Add("Цели читаются отчётами при следующем расчёте месяца; кэш отчётов сброшен.");
        }
        else if (set.Target == DataTargets.Pnl && summary?.Pnl is { } plan)
        {
            // P&L месяца пересчитан тем же кодом, что и раздел «P&L дилеров»: итоги должны совпасть с планом изменений.
            pnlMonths = [];
            foreach (var month in plan.Months.Take(12))
            {
                var expected = rolledBack ? month.Before : month.After;
                var (_, total) = await pnl.MonthAsync(month.Year, month.Month, ct);
                pnlMonths.Add(new PnlMonthTotal(month.Year, month.Month, expected, total));
                if (set.Status == DataChangeSetStatus.PartiallyRolledBack)
                {
                    continue; // при частичном откате итог месяца смешанный — сверяются только строки
                }

                foreach (var line in PnlCheckedLines)
                {
                    var e = expected.GetValueOrDefault(line);
                    var a = total.GetValueOrDefault(line);
                    if (Math.Abs((e ?? 0) - (a ?? 0)) > 1m)
                    {
                        problems.Add(new StoredCheck($"P&L · {DataEntityTypes.MonthLabel(month.Year, month.Month)} · {PnlMath.Line(line)!.Label}: расчёт OneBase",
                            e is { } ev ? DataValues.Format(Math.Round(ev, 2)) : null, a is { } av ? DataValues.Format(Math.Round(av, 2)) : null, false));
                    }
                }
            }

            notes.Add("P&L пересчитан OneBase по загруженным исходным строкам (раздел «Продажи → P&L дилеров»).");
        }

        if (problems.Count > 0)
        {
            notes.Add("Есть расхождения: смотрите problems — значение в базе или в расчёте OneBase отличается от плана изменений.");
        }

        var result = new VerificationResult(DateTimeOffset.UtcNow, checkedCount, problems.Count, problems, kpi, notes, pnlMonths);
        set.Verification = JsonSerializer.Serialize(result, DataTargets.Json);
        set.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>Текущее значение записи по бизнес-ключу (как строка, null — записи нет).</summary>
    public async Task<string?> StoredValueAsync(string entityKey, CancellationToken ct = default)
    {
        if (entityKey.StartsWith(DataEntityTypes.RegionPlan + ":", StringComparison.Ordinal))
        {
            var k = RegionPlanPlanner.ParseKey(entityKey);
            var value = await db.SalesRegionPlans.AsNoTracking()
                .Where(p => p.RegionId == k.RegionId && p.Kind == k.Kind && p.Year == k.Year && p.Month == k.Month && p.CategoryId == k.CategoryId)
                .Select(p => (decimal?)p.PlanKg)
                .FirstOrDefaultAsync(ct);
            return value is { } v ? DataValues.Format(v) : null;
        }

        if (entityKey.StartsWith(DataEntityTypes.SalesTarget + ":", StringComparison.Ordinal))
        {
            var key = entityKey[(DataEntityTypes.SalesTarget.Length + 1)..];
            var value = await db.SalesTargets.AsNoTracking().Where(t => t.Key == key).Select(t => (decimal?)t.Value).FirstOrDefaultAsync(ct);
            return value is { } v ? DataValues.Format(v) : null;
        }

        if (entityKey.StartsWith(DataEntityTypes.RegionProfile + ":", StringComparison.Ordinal))
        {
            var (regionId, field) = RegionProfilePlanner.ParseKey(entityKey);
            var region = await db.SalesRegions.AsNoTracking().Where(r => r.Id == regionId)
                .Select(r => new { r.DealerName, r.SupervisorName }).FirstOrDefaultAsync(ct);
            return field == RegionProfilePlanner.Dealer ? region?.DealerName : region?.SupervisorName;
        }

        if (entityKey.StartsWith(DataEntityTypes.PnlValue + ":", StringComparison.Ordinal))
        {
            var k = PnlPlanner.ParseKey(entityKey);
            var dealerKey = PnlMath.DealerKey(k.Dealer);
            var value = await db.SalesPnlValues.AsNoTracking()
                .Where(v => v.Year == k.Year && v.Month == k.Month && v.Line == k.Line && v.DealerKey == dealerKey)
                .Select(v => (decimal?)v.Amount)
                .FirstOrDefaultAsync(ct);
            return value is { } v ? DataValues.Format(v) : null;
        }

        return null;
    }

    /// <summary>Оценка влияния на выполнение плана: сейчас (расчёт OneBase) и после (факт ÷ новый план).</summary>
    private async Task<IReadOnlyList<ChangeImpact>> ImpactAsync(PlanKind kind, IReadOnlyList<RegionPlanTotal> totals, CancellationToken ct)
    {
        var result = new List<ChangeImpact>();
        foreach (var group in totals.GroupBy(t => (t.Year, t.Month)).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month).Take(3))
        {
            try
            {
                result.Add((await CurrentKpiAsync(kind, group.Key.Year, group.Key.Month, group.ToList(), projected: true, ct)).Impact);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result.Add(new ChangeImpact($"{group.Key.Year:D4}-{group.Key.Month:D2}", null, null, [],
                    $"Оценку влияния посчитать не удалось: {ex.Message}"));
            }
        }

        return result;
    }

    /// <summary>
    /// Показатели месяца по расчёту OneBase. projected = true — «до» из расчёта, «после» — оценка факт ÷ новый план;
    /// false — проверка после применения: план расчёта должен совпасть с итогом плана изменений.
    /// </summary>
    private async Task<(ChangeImpact Impact, List<StoredCheck> Problems)> CurrentKpiAsync(
        PlanKind kind, int year, int month, IReadOnlyList<RegionPlanTotal> totals, bool projected, CancellationToken ct)
    {
        var period = $"{year:D4}-{month:D2}";
        var problems = new List<StoredCheck>();
        if (kind == PlanKind.Primary)
        {
            var view = await primary.GetAsync(year, month, ct);
            var delta = totals.Sum(t => (t.After ?? 0) - (t.Before ?? 0));
            var republic = new ImpactRow("Первичка, республика", projected ? view.PlanMonthKg : view.PlanMonthKg - delta,
                projected ? (view.PlanMonthKg ?? 0) + delta : view.PlanMonthKg, view.MonthTotal.Kg,
                null, null);
            return (new ChangeImpact(period, "primary", republic with
            {
                ExecutionBefore = republic.PlanBefore is > 0 ? view.MonthTotal.Kg / republic.PlanBefore : null,
                ExecutionAfter = republic.PlanAfter is > 0 ? view.MonthTotal.Kg / republic.PlanAfter : null,
            }, [], "Первичка: план месяца — сумма планов дилеров (регионов) вида «Первичка»."), problems);
        }

        var months = await loader.MonthsAsync(ct);
        if (!months.Any(m => m.Year == year && m.Month == month))
        {
            return (new ChangeImpact(period, null, null, [],
                "Продаж за этот месяц в OneBase ещё нет — выполнение не считается; план будет виден в «План на следующий месяц» и после начала продаж."),
                problems);
        }

        var analytics = await loader.LoadAsync(year, month, kind, ct);
        var group = analytics.CachedRepublic(null, null);
        var rows = group.Regions.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
        var regionsImpact = new List<ImpactRow>();
        decimal deltaPlan = 0;
        foreach (var total in totals)
        {
            rows.TryGetValue(total.RegionId.ToString(), out var row);
            var fact = row?.FactKg;
            if (projected)
            {
                deltaPlan += (total.After ?? 0) - (row?.PlanKg ?? 0);
                regionsImpact.Add(new ImpactRow(total.Region, row?.PlanKg, total.After, fact, row?.Execution,
                    total.After is > 0 && fact is { } f ? f / total.After : null));
            }
            else
            {
                var expected = total.After;
                var calculated = row?.PlanKg;
                regionsImpact.Add(new ImpactRow(total.Region, total.Before, calculated, fact, null, row?.Execution));
                if (expected is { } e && (calculated is not { } c || Math.Abs(c - e) > 0.01m))
                {
                    problems.Add(new StoredCheck($"{total.Region} · {DataEntityTypes.MonthLabel(year, month)}: план в расчёте OneBase",
                        DataValues.Format(e), calculated is { } cv ? DataValues.Format(cv) : null, false));
                }
            }
        }

        var kpi = group.Kpi;
        ImpactRow republicRow;
        string note;
        if (projected)
        {
            var source = kpi.PlanSource;
            var sameSource = string.Equals(source, kind.ToString(), StringComparison.OrdinalIgnoreCase);
            var planAfter = sameSource
                ? (kpi.PlanKg ?? 0) + deltaPlan
                : totals.Sum(t => t.After ?? 0);
            republicRow = new ImpactRow("Республика", kpi.PlanKg, planAfter, kpi.FactKg, kpi.Execution,
                planAfter > 0 ? kpi.FactKg / planAfter : null);
            note = sameSource
                ? "Оценка: выполнение = факт ÷ новый план; точные значения OneBase пересчитает после применения."
                : $"Сейчас план месяца — «{source}»; после применения месяц перейдёт на планы регионов, план республики — сумма планов регионов из этого плана.";
        }
        else
        {
            republicRow = new ImpactRow("Республика", null, kpi.PlanKg, kpi.FactKg, null, kpi.Execution);
            note = $"Пересчитано OneBase после применения: источник плана — {kpi.PlanSource}, данные по {analytics.Period.DataThrough:dd.MM.yyyy}.";
        }

        return (new ChangeImpact(period, kpi.PlanSource, republicRow, regionsImpact, note), problems);
    }

    private async Task<PreviousVersionInfo?> PreviousVersionAsync(string target, string? planKind, string? period, DataFile? file, IReadOnlyList<PlannedItem> items,
        CancellationToken ct)
    {
        if (period is null)
        {
            return null;
        }

        var previous = await db.DataChangeSets.AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.Target == target && s.PlanKind == planKind && s.Period == period
                        && s.Status == DataChangeSetStatus.Applied && (file == null || s.FileSha256 != file.Sha256))
            .OrderByDescending(s => s.AppliedAt)
            .FirstOrDefaultAsync(ct);
        if (previous is null)
        {
            return null;
        }

        var earlier = previous.Items.Where(i => i.Status == DataChangeItemStatus.Applied).Select(i => i.EntityKey).ToHashSet();
        var corrected = items.Count(i => i.Action is DataChangeAction.Update or DataChangeAction.Delete && earlier.Contains(i.EntityKey));
        var fileName = previous.FileId is { } fid
            ? await db.DataFiles.AsNoTracking().Where(f => f.Id == fid).Select(f => f.FileName).FirstOrDefaultAsync(ct)
            : null;
        return new PreviousVersionInfo(previous.Id, fileName, previous.AppliedAt, corrected);
    }

    /// <summary>Строки P&L, итоги которых сверяются после применения.</summary>
    private static readonly string[] PnlCheckedLines = [PnlMath.Sales, PnlMath.Cost, PnlMath.Gross, PnlMath.Sm, PnlMath.Ship, PnlMath.Admin, PnlMath.Other, PnlMath.Ebit];

    public static ChangeCounts Count(IReadOnlyCollection<PlannedItem> items) => new(
        items.Count(i => i.Action == DataChangeAction.Create),
        items.Count(i => i.Action == DataChangeAction.Update),
        items.Count(i => i.Action == DataChangeAction.Delete),
        items.Count(i => i.Action == DataChangeAction.Unchanged),
        items.Count(i => i.Action == DataChangeAction.Conflict));

    private static bool SameItems(IReadOnlyCollection<DataChangeItem> saved, IReadOnlyList<PlannedItem> planned) =>
        saved.Count == planned.Count
        && saved.OrderBy(i => i.Position).Zip(planned).All(p =>
            p.First.EntityKey == p.Second.EntityKey && p.First.Action == p.Second.Action && p.First.Before == p.Second.Before && p.First.After == p.Second.After);

    private static string Hash(params string[] parts) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\u001f", parts))));

    private static string? Cut(string? text, int max) => text is null ? null : text.Length > max ? text[..max] : text;
}
