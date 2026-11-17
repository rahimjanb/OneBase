using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using OneBase.Finance.Integration.Contracts;
using OneBase.Finance.Integration.Worker.OneC;

namespace OneBase.Finance.Integration.Worker.Mock;

/// <summary>
/// Сбои тестового источника — для проверок восстановления: «1С недоступна», отказ авторизации, задержка, ошибки отдельных методов.
/// </summary>
public sealed class MockFaults
{
    private readonly ConcurrentQueue<(string Method, OneCException Error)> _queue = new();
    private readonly ConcurrentDictionary<string, int> _calls = new();

    /// <summary>Сеть до 1С недоступна — все запросы падают.</summary>
    public bool Down { get; set; }

    public bool AuthFails { get; set; }
    public TimeSpan Latency { get; set; }

    /// <summary>Следующие times вызовов метода (или «*» — любого) завершатся ошибкой этого вида.</summary>
    public void FailNext(string method, OneCErrorKind kind, int times = 1, TimeSpan? retryAfter = null)
    {
        for (var i = 0; i < times; i++)
        {
            _queue.Enqueue((method, new OneCException(kind, $"1С /{method}: имитация сбоя ({kind})", retryAfter)));
        }
    }

    public int Calls(string method) => _calls.GetValueOrDefault(method);

    internal async Task CheckAsync(string method, CancellationToken ct)
    {
        _calls.AddOrUpdate(method, 1, (_, n) => n + 1);
        if (Latency > TimeSpan.Zero)
        {
            await Task.Delay(Latency, ct);
        }

        if (Down)
        {
            throw new OneCException(OneCErrorKind.Network, $"1С /{method}: сервер не отвечает (имитация)");
        }

        if (AuthFails)
        {
            throw new OneCException(OneCErrorKind.Unauthorized, $"1С /{method}: 1С отклонила авторизацию (401, имитация)");
        }

        if (_queue.TryPeek(out var next) && (next.Method == method || next.Method == "*") && _queue.TryDequeue(out var failure))
        {
            throw failure.Error;
        }
    }
}

/// <summary>
/// Тестовый источник данных 1С: те же методы и формат, что у настоящего API по спецификации, данные — демо (<see cref="MockDataSet"/>).
/// Документы «появляются» по ходу дня (видны после времени записи), поэтому изменения есть и у работающей службы. Каждый день набор строится
/// заново на сегодняшнюю дату — данные прошлых дней при этом те же.
/// </summary>
public sealed class OneCMockDataProvider(MockOptions options, TimeProvider clock) : IOneCDataProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly object _lock = new();
    private MockDataSet? _set;

    public MockFaults Faults { get; } = new();

    public string Name => "mock";

    public string? DisplayUrl => "mock://onebase-demo-1c";

    internal MockDataSet Set
    {
        get
        {
            lock (_lock)
            {
                var now = clock.GetUtcNow();
                if (_set is null || MockDataSet.DayOf(now) != MockDataSet.DayOf(_set.Now))
                {
                    _set = new MockDataSet(options, now);
                }

                _set.Advance(now);
                return _set;
            }
        }
    }

    /// <summary>Правки «в 1С» для проверок: исправить, отменить проведение, удалить документ, переименовать контрагента.</summary>
    public void EditDocument(string id, decimal factor) => Mutate(s => s.EditDocument(id, factor, clock.GetUtcNow()));

    public void UnpostDocument(string id) => Mutate(s => s.UnpostDocument(id, clock.GetUtcNow()));

    public void DeleteDocument(string id) => Mutate(s => s.DeleteDocument(id, clock.GetUtcNow()));

    public void RenameCounterparty(string id, string name) => Mutate(s => s.RenameCounterparty(id, name, clock.GetUtcNow()));

    /// <summary>Документы организации за период (для проверок).</summary>
    public IReadOnlyList<OneCDocumentDto> DocumentsOf(string organizationId, DateOnly from, DateOnly to) =>
        Set.Documents.Where(d => Set.Visible(d) && d.Doc.OrganizationId == organizationId && InRange(d.Doc.Date, from, to)).Select(d => d.Doc).ToList();

    private void Mutate(Action<MockDataSet> action)
    {
        var set = Set;
        lock (_lock)
        {
            action(set);
        }
    }

    public async Task<OneCPing> TestConnectionAsync(CancellationToken ct)
    {
        await Faults.CheckAsync("ping", ct);
        return new OneCPing
        {
            ApiVersion = "1.0", InfobaseId = MockCatalog.InfobaseId, InfobaseName = MockCatalog.InfobaseName,
            Configuration = new OneCConfiguration("БухгалтерияДляУзбекистана (демо)", "3.0"), ServerTime = clock.GetUtcNow().ToOffset(MockDataSet.Offset),
            AccountingCurrency = "UZS", User = "OneBase_ReadOnly", ReadOnly = true,
        };
    }

    public Task<OneCPage<OneCOrganizationDto>> GetOrganizationsAsync(OneCPageRequest page, CancellationToken ct) => PageAsync("organizations", Set.Organizations, page, null, ct);

    public Task<OneCPage<OneCAccountDto>> GetAccountsAsync(OneCPageRequest page, CancellationToken ct) => PageAsync("accounts", Set.Accounts, page, null, ct);

    public Task<OneCPage<OneCCounterpartyDto>> GetCounterpartiesAsync(OneCPageRequest page, CancellationToken ct) => PageAsync("counterparties", Set.Counterparties, page, null, ct);

    public Task<OneCPage<OneCContractDto>> GetContractsAsync(OneCPageRequest page, CancellationToken ct) => PageAsync("contracts", Set.Contracts, page, null, ct);

    public Task<OneCPage<OneCCurrencyDto>> GetCurrenciesAsync(OneCPageRequest page, CancellationToken ct) => PageAsync("currencies", Set.Currencies, page, null, ct);

    public Task<OneCPage<OneCRateDto>> GetExchangeRatesAsync(DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct)
    {
        var today = MockDataSet.DayOf(clock.GetUtcNow());
        var rows = Set.Rates.Where(r => r.Date >= from && r.Date <= to && r.Date <= today).OrderBy(r => r.Date).ThenBy(r => r.Currency).ToList();
        return PageAsync("exchange-rates", rows, page, l => new OneCTotals(l.Count, null), ct);
    }

    public Task<OneCPage<OneCReferenceDto>> GetWarehousesAsync(OneCPageRequest page, CancellationToken ct) => PageAsync("warehouses", Set.Warehouses, page, null, ct);

    public Task<OneCPage<OneCReferenceDto>> GetDepartmentsAsync(OneCPageRequest page, CancellationToken ct) => PageAsync("departments", Set.Departments, page, null, ct);

    public Task<OneCPage<OneCReferenceDto>> GetReferencesAsync(string kind, OneCPageRequest page, CancellationToken ct) =>
        PageAsync("references", Set.References.GetValueOrDefault(kind) ?? [], page, null, ct);

    public Task<OneCPage<OneCItemDto>> GetNomenclatureAsync(OneCPageRequest page, CancellationToken ct) => PageAsync("items", Set.Items, page, null, ct);

    public Task<OneCPage<OneCBalanceDto>> GetAccountBalancesAsync(string organizationId, DateOnly month, OneCPageRequest page, CancellationToken ct)
    {
        var org = MockCatalog.Organizations.FirstOrDefault(o => o.Id == organizationId);
        var rows = org is null ? [] : Set.Balances(org.Index, new DateOnly(month.Year, month.Month, 1));
        return PageAsync("balances", rows, page, l => new OneCTotals(l.Count, l.Sum(b => b.Turnover?.Debit ?? 0)), ct);
    }

    public Task<OneCPage<OneCEntrySetDto>> GetTransactionsAsync(string organizationId, DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct)
    {
        var set = Set;
        var sets = set.Documents.Where(d => set.Visible(d) && d.Doc.OrganizationId == organizationId && InRange(d.Doc.Date, from, to) && (!d.Doc.Posted || d.Entries.Count > 0))
            .OrderBy(d => d.Doc.Date).ThenBy(d => d.Doc.Id, StringComparer.Ordinal)
            .Select(d => new OneCEntrySetDto
            {
                Registrar = new OneCRegistrarDto(d.Doc.Id, d.Doc.Type, d.Doc.SourceType, d.Doc.Number, d.Doc.Date),
                OrganizationId = d.Doc.OrganizationId, Version = d.Doc.Version, Entries = d.Doc.Posted ? d.Entries : [],
            }).ToList();
        return PageAsync("entries", sets, page, l => new OneCTotals(l.Sum(s => s.Entries.Count), l.Sum(s => s.Entries.Sum(e => e.Amount))), ct);
    }

    public Task<OneCPage<OneCDocumentDto>> GetDocumentsAsync(string organizationId, IReadOnlyList<string>? types, DateOnly from, DateOnly to, OneCPageRequest page, CancellationToken ct)
    {
        var set = Set;
        var docs = set.Documents.Where(d => set.Visible(d) && d.Doc.OrganizationId == organizationId && InRange(d.Doc.Date, from, to)
                && (types is null || types.Count == 0 || types.Contains(d.Doc.Type!)))
            .OrderBy(d => d.Doc.Date).ThenBy(d => d.Doc.Id, StringComparer.Ordinal).Select(d => d.Doc).ToList();
        return PageAsync("documents", docs, page, l => new OneCTotals(l.Count, l.Sum(d => d.AmountAccounting ?? 0)), ct);
    }

    public async Task<OneCPage<OneCChangeDto>> GetModifiedRecordsAsync(string? cursor, int limit, CancellationToken ct)
    {
        await Faults.CheckAsync("changes", ct);
        var changes = Set.Changes;
        if (cursor is null)
        {
            // Первое обращение: текущий номер и пустой список — история загружается отдельно, по периодам.
            return new OneCPage<OneCChangeDto> { ApiVersion = "1.0", Page = new OneCPageInfo(limit, (changes.Count == 0 ? 0 : changes[^1].Seq).ToString(CultureInfo.InvariantCulture), false) };
        }

        if (!long.TryParse(cursor, CultureInfo.InvariantCulture, out var after))
        {
            throw new OneCException(OneCErrorKind.BadRequest, "1С /changes: неверный курсор");
        }

        var items = changes.Where(c => c.Seq > after).Take(Math.Clamp(limit, 1, 1000)).ToList();
        var more = items.Count > 0 && changes[^1].Seq > items[^1].Seq;
        return new OneCPage<OneCChangeDto>
        {
            ApiVersion = "1.0",
            Items = items.Select(c => new OneCChangeDto
            {
                Seq = c.Seq, EntityType = c.EntityType, Id = c.Id, OrganizationId = c.OrganizationId, Deleted = c.Deleted,
                Data = c.Deleted ? null : JsonSerializer.SerializeToElement(c.Data(), Json),
            }).ToList(),
            Page = new OneCPageInfo(limit, items.Count == 0 ? cursor : items[^1].Seq.ToString(CultureInfo.InvariantCulture), more),
        };
    }

    private static bool InRange(DateTimeOffset? date, DateOnly from, DateOnly to) =>
        date is { } d && MockDataSet.DayOf(d) >= from && MockDataSet.DayOf(d) <= to;

    /// <summary>Страница по смещению: курсор — «o:N». Итоги — по всему отбору, если их попросили.</summary>
    private async Task<OneCPage<T>> PageAsync<T>(string method, IReadOnlyList<T> rows, OneCPageRequest page, Func<IReadOnlyList<T>, OneCTotals>? totals, CancellationToken ct)
    {
        await Faults.CheckAsync(method, ct);
        var offset = page.Cursor is { } c && c.StartsWith("o:", StringComparison.Ordinal) && int.TryParse(c[2..], CultureInfo.InvariantCulture, out var o) ? o : 0;
        var limit = Math.Clamp(page.Limit, 1, 1000);
        var items = rows.Skip(offset).Take(limit).ToList();
        var more = offset + items.Count < rows.Count;
        return new OneCPage<T>
        {
            ApiVersion = "1.0", Items = items, Page = new OneCPageInfo(limit, more ? $"o:{offset + items.Count}" : null, more),
            Totals = page.IncludeTotals ? totals?.Invoke(rows) ?? new OneCTotals(rows.Count, null) : null,
        };
    }
}
