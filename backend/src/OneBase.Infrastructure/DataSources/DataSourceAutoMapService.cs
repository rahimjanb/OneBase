using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Domain.Audit;
using OneBase.Domain.Integrations;

namespace OneBase.Infrastructure.DataSources;

/// <summary>Что сопоставить: раздел, таблица, листы привязки, лист для анализа (null — первый), строка заголовков, привязка (если уже есть).</summary>
public sealed record AutoMapInput(string Section, Guid ConnectionId, IReadOnlyList<string> Sheets, string? Sheet, int HeaderRow, Guid? BindingId);

/// <summary>
/// Подтверждение: решения человека по столбцам, месяц полей и расписание привязки. DryRun — только собрать поля (перенести в ручной
/// редактор), не сохраняя.
/// </summary>
public sealed record AutoMapConfirmInput(
    string Section,
    Guid ConnectionId,
    IReadOnlyList<string> Sheets,
    string? Sheet,
    int HeaderRow,
    Guid? BindingId,
    IReadOnlyList<MappingDecision> Decisions,
    IReadOnlyList<MappingFieldSetting>? Settings,
    string? Schedule,
    int? IntervalMinutes,
    string? DailyAt,
    bool Enabled,
    bool DryRun = false);

public sealed record AutoMapConfirmResult(IReadOnlyList<DataSourceFieldBinding> Fields, BindingView? Binding);

/// <summary>
/// ИИ-сопоставление столбцов Google Sheets с полями OneBase. Предложение: лист (заголовки и до 12 строк значений) → модель ИИ проекта →
/// проверка (цель из раздела, столбец из листа, поле не занято другой привязкой, факт Linko и финансы — только с проверкой, пороги
/// уверенности). Ничего не пишет. Подтверждение: решения человека → те же проверки → обычная привязка (DataSourceFieldBinding, со
/// снимком заголовков) через DataSourceService.SaveBindingAsync. Синхронизация ИИ не вызывает: она читает сохранённое сопоставление.
/// </summary>
public sealed class DataSourceAutoMapService(
    DataSourceService sources,
    DataChangeService changes,
    ISheetMappingAdvisor advisor,
    SheetMappingOptions options,
    IAuditLogger audit,
    TimeProvider clock)
{
    public async Task<SheetMappingProposal> ProposeAsync(AutoMapInput input, Guid userId, CancellationToken ct)
    {
        await sources.EnsureCanApplyAsync(userId, ct);
        var (request, sheet) = await RequestAsync(input.Section, input.ConnectionId, input.Sheets, input.Sheet, input.HeaderRow, input.BindingId, ct);
        RawMapping raw;
        try
        {
            raw = await advisor.AdviseAsync(request, userId, ct);
        }
        catch (SheetMappingException ex)
        {
            throw new DataSourceException($"ИИ-сопоставление не выполнено: {ex.Message} Ручное сопоставление работает как раньше.");
        }

        var proposal = SheetMapping.Validate(request, raw, options, SheetMapping.Stale(request, new ReportDocument([sheet])));
        await audit.LogAsync(ActorType.User, userId.ToString(), "datasource.automap.proposed", nameof(DataSourceBinding),
            input.BindingId?.ToString() ?? input.ConnectionId.ToString(), new
            {
                input.Section,
                proposal.Sheet,
                proposal.Model,
                statuses = proposal.Suggestions.GroupBy(s => s.Status).ToDictionary(g => g.Key, g => g.Count()),
                fields = proposal.Fields.Select(f => f.Field),
            }, ct);
        return proposal;
    }

    public async Task<AutoMapConfirmResult> ConfirmAsync(AutoMapConfirmInput input, Guid userId, CancellationToken ct)
    {
        await sources.EnsureCanApplyAsync(userId, ct);
        if (input.Decisions.Count == 0)
        {
            throw new DataSourceException("Не принято ни одного соответствия — отметьте столбцы, которые нужно сопоставить.");
        }

        var (request, _) = await RequestAsync(input.Section, input.ConnectionId, input.Sheets, input.Sheet, input.HeaderRow, input.BindingId, ct);
        var (fields, errors) = SheetMapping.Build(request, input.Decisions, input.Settings ?? []);
        if (errors.Count > 0)
        {
            throw new DataSourceException(string.Join(" ", errors));
        }

        if (fields.Count == 0)
        {
            throw new DataSourceException("Ни одно поле не сопоставлено полностью: проверьте столбцы региона, значения и месяц.");
        }

        // У существующей привязки поля, которых не касалось сопоставление, остаются как были.
        var result = fields.ToList();
        if (input.BindingId is { } id)
        {
            result.AddRange((await sources.BindingFieldsAsync(id, ct)).Where(old => result.All(f => f.Field != old.Field)));
        }

        if (input.DryRun)
        {
            return new AutoMapConfirmResult(result, null);
        }

        var binding = await sources.SaveBindingAsync(input.BindingId, new BindingInput(input.Section, input.ConnectionId, input.Sheets, input.HeaderRow, result,
            input.Schedule ?? DataSourceRules.Interval, input.IntervalMinutes, input.DailyAt, input.Enabled), userId, ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "datasource.automap.confirmed", nameof(DataSourceBinding), binding.Id.ToString(),
            new { input.Section, request.Sheet, decisions = input.Decisions, fields = result.Select(f => f.Field) }, ct);
        return new AutoMapConfirmResult(result, binding);
    }

    private async Task<(SheetMappingRequest Request, ReportSheet Sheet)> RequestAsync(string sectionKey, Guid connectionId, IReadOnlyList<string> sheets,
        string? sheet, int headerRow, Guid? bindingId, CancellationToken ct)
    {
        var section = DataSourceCatalog.Section(sectionKey) ?? throw new DataSourceException($"Раздела «{sectionKey}» нет.");
        if (section.Fields.Count == 0)
        {
            throw new DataSourceException($"В разделе «{section.Name}» нет полей из Google Sheets — все его данные приходят из Linko.");
        }

        if (headerRow is < 1 or > 1000)
        {
            throw new DataSourceException("Строка заголовков — от 1 до 1000.");
        }

        var (clean, sheetsError) = DataSourceRules.CleanSheets(sheets);
        if (sheetsError is not null)
        {
            throw new DataSourceException(sheetsError);
        }

        var (connection, allSheets, data) = await sources.SheetAsync(connectionId, sheet ?? clean[0], ct);
        var header = headerRow <= data.Rows.Count ? data.Rows[headerRow - 1] : [];
        var rows = data.Rows.Skip(headerRow).Where(r => r.Any(c => c.Length > 0)).Take(options.SampleRows).ToList();
        var width = Math.Min(Math.Max(header.Count, rows.Select(r => r.Count).DefaultIfEmpty(0).Max()), options.MaxColumns);
        var columns = Enumerable.Range(0, width)
            .Select(i => new SheetColumnSample(DataValues.ColumnLetter(i), i < header.Count ? header[i].Trim() : string.Empty,
                rows.Select(r => i < r.Count ? Cut(r[i]) : string.Empty).ToList()))
            .Where(c => c.Header.Length > 0 || c.Samples.Any(s => s.Length > 0))
            .ToList();
        if (columns.Count == 0)
        {
            throw new DataSourceException($"В листе «{data.Name}» нет данных в строке заголовков {headerRow} и ниже — проверьте строку заголовков.");
        }

        var taken = await sources.TakenFieldsAsync(bindingId, ct);
        var request = new SheetMappingRequest(
            section.Key,
            section.Name,
            connection,
            data.Name,
            clean.Contains(DataSourceRules.AllSheets) ? allSheets : clean,
            headerRow,
            columns,
            section.Fields.Select(f => DataSourceCatalog.Field(f)!).SelectMany(MappingRoles.For).ToList(),
            await changes.CategoriesAsync(ct),
            taken.Where(t => section.Fields.Contains(t.Key)).ToDictionary(t => t.Key, t => t.Value),
            bindingId is { } id ? await sources.BindingFieldsAsync(id, ct) : [],
            DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(DataSourceRules.CompanyOffset).DateTime));
        return (request, data);
    }

    private static string Cut(string text) => text.Length <= 60 ? text : text[..60] + "…";
}
