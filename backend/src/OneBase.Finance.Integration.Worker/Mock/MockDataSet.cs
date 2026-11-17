using System.Text.Json;
using OneBase.Finance.Integration.Contracts;
using OneBase.Finance.Integration.Worker.OneC;
using static OneBase.Finance.Integration.Worker.Mock.MockCatalog;

namespace OneBase.Finance.Integration.Worker.Mock;

/// <summary>Документ тестового источника: документ, его проводки и момент записи в «1С».</summary>
internal sealed class MockDocument
{
    public required OneCDocumentDto Doc { get; set; }
    public List<OneCEntryDto> Entries { get; set; } = [];
    public DateTimeOffset ModifiedAt { get; set; }
    public int Org { get; init; }
    public bool Deleted { get; set; }
}

/// <summary>Изменение в «журнале изменений 1С» тестового источника.</summary>
internal sealed record MockChange(long Seq, DateTimeOffset At, string EntityType, string Id, string? OrganizationId, bool Deleted, Func<object?> Data);

/// <summary>
/// Демо-данные 1С: две организации, документы по рабочим дням с начала периода до сегодняшнего момента и проводки к ним по плану счетов
/// НСБУ №21 (реализация с НДС 12 %, себестоимость, возвраты, закупки сырья, производство, оплаты покупателей и поставщикам с задержками,
/// валютный покупатель и поставщик, касса, зарплата, ЕСП, НДС и налог на прибыль, переоценка валюты, закрытие месяца на 9910).
/// Всё детерминировано (seed): одинаковые данные при каждом запуске. Остатки и обороты считаются из проводок.
/// </summary>
internal sealed class MockDataSet
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(5);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public DateOnly Start { get; }
    public DateTimeOffset Now { get; private set; }
    public List<OneCOrganizationDto> Organizations { get; } = [];
    public List<OneCAccountDto> Accounts { get; } = [];
    public List<OneCCurrencyDto> Currencies { get; } = [];
    public List<OneCCounterpartyDto> Counterparties { get; } = [];
    public List<OneCContractDto> Contracts { get; } = [];
    public List<OneCReferenceDto> Warehouses { get; } = [];
    public List<OneCReferenceDto> Departments { get; } = [];
    public Dictionary<string, List<OneCReferenceDto>> References { get; } = [];
    public List<OneCItemDto> Items { get; } = [];
    public List<OneCRateDto> Rates { get; } = [];
    public List<MockDocument> Documents { get; } = [];

    private readonly List<(DateTimeOffset At, string Type, string Id, string? Org, bool Deleted, Func<object?> Data)> _manual = [];
    private Dictionary<(int Org, DateOnly Month), List<OneCBalanceDto>>? _balances;
    private List<MockChange>? _changes;
    private readonly DateTimeOffset _referencesAt;

    public MockDataSet(MockOptions options, DateTimeOffset now)
    {
        Now = now;
        var today = DateOnly.FromDateTime(now.ToOffset(Offset).DateTime);
        Start = options.StartDate ?? new DateOnly(today.Year - 1, 1, 1);
        _referencesAt = new DateTimeOffset(Start.AddDays(-1).ToDateTime(new TimeOnly(8, 0)), Offset);
        BuildReferences();
        BuildRates(options.Seed, today);
        foreach (var org in MockCatalog.Organizations)
        {
            new Generator(this, org, options).Run(Start, today);
        }
    }

    public static DateTimeOffset At(DateOnly day, int hour, int minute) => new(day.ToDateTime(new TimeOnly(hour, minute)), Offset);

    public static DateOnly DayOf(DateTimeOffset at) => DateOnly.FromDateTime(at.ToOffset(Offset).DateTime);

    /// <summary>Документ виден, когда он уже «записан» в 1С.</summary>
    public bool Visible(MockDocument d) => d.ModifiedAt <= Now && !d.Deleted;

    public void Advance(DateTimeOffset now)
    {
        if (now > Now)
        {
            Now = now;
            _changes = null;
            _balances = null;
        }
    }

    // ---------- Справочники ----------

    private void BuildReferences()
    {
        foreach (var o in MockCatalog.Organizations)
        {
            Organizations.Add(new OneCOrganizationDto
            {
                Id = o.Id, Version = "v1", ModifiedAt = _referencesAt, Code = o.Code, Name = o.Name, FullName = "Общество с ограниченной ответственностью " + o.Name[4..],
                Inn = o.Inn, VatCode = "3260" + o.Inn[..8], AccountingCurrency = "UZS", IsActive = true,
            });
        }

        foreach (var a in MockCatalog.Accounts)
        {
            Accounts.Add(new OneCAccountDto
            {
                Id = MockCatalog.Id("acc:" + a.Code), Version = "v1", ModifiedAt = _referencesAt, Code = a.Code, Name = a.Name, ParentCode = a.Parent, Type = a.Type,
                Currency = a.Currency, Quantity = a.Quantity,
                Subconto = a.Subconto.Select((s, i) => new OneCSubcontoTypeDto(i + 1, s.Type, s.TurnoverOnly)).ToList(),
            });
        }

        foreach (var c in MockCatalog.Currencies)
        {
            Currencies.Add(new OneCCurrencyDto { Id = MockCatalog.Id("cur:" + c.Code), Version = "v1", ModifiedAt = _referencesAt, Code = c.Code, NumericCode = c.Numeric, Name = c.Name });
        }

        foreach (var p in Customers.Concat(Suppliers))
        {
            Counterparties.Add(new OneCCounterpartyDto
            {
                Id = p.Id, Version = "v1", ModifiedAt = _referencesAt, Code = p.Code, Name = p.Name, FullName = p.Name, Inn = p.Inn, Kind = p.Kind, LegalForm = p.Name.StartsWith("ЧП") ? "entrepreneur" : "legal",
                Address = "г. Ташкент", BankAccounts = [new OneCBankAccountDto($"2020800{p.Inn}", "00873", "АКБ «Демо Банк»", p.Currency)],
            });
            foreach (var org in p.Orgs)
            {
                Contracts.Add(new OneCContractDto
                {
                    Id = ContractId(p, org), Version = "v1", ModifiedAt = _referencesAt, Code = p.Code + "-" + (org + 1), Name = $"Договор № {p.Code}/{org + 1} от {Start.AddDays(-30):dd.MM.yyyy}",
                    CounterpartyId = p.Id, OrganizationId = MockCatalog.Organizations[org].Id, Number = $"{p.Code}/{org + 1}", Date = Start.AddDays(-30),
                    Kind = p.Kind == "customer" ? "with_customer" : "with_supplier", Currency = p.Currency, PaymentTermDays = p.TermDays,
                });
            }
        }

        Warehouses.AddRange(MockCatalog.Warehouses.Select(Ref));
        Departments.AddRange(MockCatalog.Departments.Select(Ref));
        References["cash_flow_items"] = CashFlowItems.Select(Ref).ToList();
        References["cost_items"] = CostItems.Select(Ref).ToList();
        References["bank_accounts"] = BankAccounts.Select(Ref).ToList();
        References["cash_desks"] = CashDesks.Select(Ref).ToList();
        foreach (var kind in OneCEntityTypes.ReferenceKinds.Where(k => !References.ContainsKey(k)))
        {
            References[kind] = [];
        }

        foreach (var p in Products.Concat(Materials))
        {
            Items.Add(new OneCItemDto
            {
                Id = p.Id, Version = "v1", ModifiedAt = _referencesAt, Code = p.Code, Name = p.Name, FullName = p.Name, Article = p.Code, Kind = p.Kind, Unit = p.Unit,
                VatRate = 12, Ikpu = "0" + p.Code[1..] + "00100000000",
            });
        }
    }

    private OneCReferenceDto Ref(MockCatalog.Ref r) => new()
    {
        Id = r.Id, Version = "v1", ModifiedAt = _referencesAt, Code = r.Code, Name = r.Name, Kind = r.Kind,
        OrganizationId = r.Org is { } o ? MockCatalog.Organizations[o].Id : null,
    };

    private void BuildRates(int seed, DateOnly today)
    {
        foreach (var c in MockCatalog.Currencies.Where(c => c.Code != "UZS"))
        {
            for (var d = Start.AddDays(-35); d <= today; d = d.AddDays(1))
            {
                var days = d.DayNumber - Start.DayNumber;
                var noise = new Random(seed ^ (d.DayNumber * 7919) ^ (Array.IndexOf(MockCatalog.Currencies, c) * 104_729)).NextDouble() - 0.5;
                var rate = c.Base * (decimal)Math.Pow(1 + (double)c.Drift, days) * (1 + (decimal)noise * 0.003m);
                Rates.Add(new OneCRateDto { Currency = c.Code, Date = d, Rate = Math.Round(rate, 2), Multiplier = c.Multiplier, Source = "ЦБ РУз" });
            }
        }
    }

    public decimal Rate(string currency, DateOnly date)
    {
        if (currency == "UZS")
        {
            return 1;
        }

        var row = Rates.LastOrDefault(r => r.Currency == currency && r.Date <= date) ?? Rates.First(r => r.Currency == currency);
        return row.Rate / row.Multiplier;
    }

    // ---------- Изменения (журнал 1С) ----------

    public IReadOnlyList<MockChange> Changes
    {
        get
        {
            if (_changes is not null)
            {
                return _changes;
            }

            var raw = new List<(DateTimeOffset At, string Type, string Id, string? Org, bool Deleted, Func<object?> Data)>();
            raw.AddRange(Organizations.Select(o => (_referencesAt, OneCEntityTypes.Organization, o.Id!, (string?)null, false, (Func<object?>)(() => o))));
            raw.AddRange(Accounts.Select(a => (_referencesAt, OneCEntityTypes.Account, a.Id!, (string?)null, false, (Func<object?>)(() => a))));
            raw.AddRange(Currencies.Select(c => (_referencesAt, OneCEntityTypes.Currency, c.Id!, (string?)null, false, (Func<object?>)(() => c))));
            raw.AddRange(Counterparties.Select(c => (c.ModifiedAt ?? _referencesAt, OneCEntityTypes.Counterparty, c.Id!, (string?)null, false, (Func<object?>)(() => c))));
            raw.AddRange(Contracts.Select(c => (_referencesAt, OneCEntityTypes.Contract, c.Id!, c.OrganizationId, false, (Func<object?>)(() => c))));
            raw.AddRange(Warehouses.Select(w => (_referencesAt, OneCEntityTypes.Warehouse, w.Id!, (string?)null, false, (Func<object?>)(() => w))));
            raw.AddRange(Departments.Select(w => (_referencesAt, OneCEntityTypes.Department, w.Id!, w.OrganizationId, false, (Func<object?>)(() => w))));
            raw.AddRange(References.SelectMany(kv => kv.Value.Select(r => (_referencesAt, OneCEntityTypes.Reference(kv.Key), r.Id!, (string?)null, false, (Func<object?>)(() => r)))));
            raw.AddRange(Items.Select(i => (_referencesAt, OneCEntityTypes.Item, i.Id!, (string?)null, false, (Func<object?>)(() => i))));
            raw.AddRange(Rates.Select(r => (new DateTimeOffset(r.Date.ToDateTime(new TimeOnly(9, 0)), Offset), OneCEntityTypes.ExchangeRate,
                OneCEntityTypes.RateKey(r.Currency!, r.Date), (string?)null, false, (Func<object?>)(() => r))));
            raw.AddRange(Documents.Where(d => !d.Deleted).Select(d => (d.ModifiedAt, OneCEntityTypes.Document, d.Doc.Id!, d.Doc.OrganizationId, false,
                (Func<object?>)(() => d.Doc with { Entries = d.Doc.Posted ? d.Entries : [] }))));
            raw.AddRange(_manual);

            // Номер изменения растёт со временем записи: мс × 1000 + порядок внутри миллисекунды — стабилен между запусками.
            _changes = raw.Where(r => r.At <= Now).OrderBy(r => r.At).ThenBy(r => r.Type, StringComparer.Ordinal).ThenBy(r => r.Id, StringComparer.Ordinal)
                .GroupBy(r => r.At.ToUnixTimeMilliseconds())
                .SelectMany(g => g.Select((r, i) => new MockChange(g.Key * 1000 + i, r.At, r.Type, r.Id, r.Org, r.Deleted, r.Data)))
                .ToList();
            return _changes;
        }
    }

    // ---------- Правки «в 1С» (для проверок): новая версия, отмена проведения, удаление ----------

    public MockDocument Document(string id) => Documents.First(d => d.Doc.Id == id);

    /// <summary>Исправление документа: суммы × factor, новая версия, проводки перестроены.</summary>
    public void EditDocument(string id, decimal factor, DateTimeOffset at)
    {
        var d = Document(id);
        var version = NextVersion(d.Doc.Version);
        d.Doc = d.Doc with
        {
            Version = version, ModifiedAt = at, Amount = Math.Round(d.Doc.Amount * factor, 2), VatAmount = d.Doc.VatAmount is { } v ? Math.Round(v * factor, 2) : null,
            AmountAccounting = d.Doc.AmountAccounting is { } a ? Math.Round(a * factor, 2) : null,
            Lines = d.Doc.Lines.Select(l => l with { Amount = Math.Round(l.Amount * factor, 2), VatAmount = l.VatAmount is { } lv ? Math.Round(lv * factor, 2) : null }).ToList(),
        };
        d.Entries = d.Entries.Select(e => e with { Amount = Math.Round(e.Amount * factor, 2) }).ToList();
        Touch(d, at);
    }

    public void UnpostDocument(string id, DateTimeOffset at)
    {
        var d = Document(id);
        d.Doc = d.Doc with { Posted = false, Version = NextVersion(d.Doc.Version), ModifiedAt = at };
        Touch(d, at);
    }

    public void DeleteDocument(string id, DateTimeOffset at)
    {
        var d = Document(id);
        d.Deleted = true;
        _manual.Add((at, OneCEntityTypes.Document, id, d.Doc.OrganizationId, true, () => null));
        Invalidate();
    }

    public void RenameCounterparty(string id, string name, DateTimeOffset at)
    {
        var index = Counterparties.FindIndex(c => c.Id == id);
        Counterparties[index] = Counterparties[index] with { Name = name, Version = NextVersion(Counterparties[index].Version), ModifiedAt = at };
        Invalidate();
    }

    private void Touch(MockDocument d, DateTimeOffset at)
    {
        d.ModifiedAt = at;
        Invalidate();
    }

    private void Invalidate()
    {
        _changes = null;
        _balances = null;
    }

    private static string NextVersion(string? version) => "v" + ((int.TryParse(version?.TrimStart('v'), out var n) ? n : 1) + 1);

    // ---------- Остатки и обороты ----------

    /// <summary>Оборотно-сальдовая ведомость организации за месяц с разворотом по субконто (кроме «только оборотных»).</summary>
    public List<OneCBalanceDto> Balances(int org, DateOnly month)
    {
        _balances ??= BuildBalances();
        return _balances.TryGetValue((org, month), out var rows) ? rows : [];
    }

    private Dictionary<(int, DateOnly), List<OneCBalanceDto>> BuildBalances()
    {
        var result = new Dictionary<(int, DateOnly), List<OneCBalanceDto>>();
        var today = DayOf(Now);
        foreach (var org in MockCatalog.Organizations)
        {
            var running = new Dictionary<string, Acc>();
            var entries = Documents.Where(d => d.Org == org.Index && Visible(d) && d.Doc.Posted)
                .SelectMany(d => d.Entries).Where(e => e.Active && e.Period is not null)
                .OrderBy(e => e.Period).ToList();
            var index = 0;
            for (var month = new DateOnly(Start.Year, Start.Month, 1); month <= today; month = month.AddMonths(1))
            {
                var next = month.AddMonths(1);
                foreach (var a in running.Values)
                {
                    a.StartMonth();
                }

                while (index < entries.Count && DayOf(entries[index].Period!.Value) < next)
                {
                    var e = entries[index++];
                    Post(running, e.Debit!, e.Amount, true);
                    Post(running, e.Credit!, e.Amount, false);
                }

                result[(org.Index, month)] = running.Values.Where(a => a.Any)
                    .Select(a => new OneCBalanceDto
                    {
                        Period = month.ToString("yyyy-MM"), OrganizationId = org.Id, Account = a.Account, Subconto = a.Subconto, Currency = a.Currency,
                        Opening = a.Amounts(a.OpenAmount, a.OpenCurrency, a.OpenQuantity),
                        Turnover = new OneCBalanceAmountsDto
                        {
                            Debit = a.TurnDebit, Credit = a.TurnCredit,
                            CurrencyDebit = a.Currency is null ? null : a.TurnCurDebit, CurrencyCredit = a.Currency is null ? null : a.TurnCurCredit,
                            QuantityDebit = a.Quantity ? a.TurnQtyDebit : null, QuantityCredit = a.Quantity ? a.TurnQtyCredit : null,
                        },
                        Closing = a.Amounts(a.Amount, a.CurrencyAmount, a.QuantityAmount),
                    })
                    .OrderBy(b => b.Account, StringComparer.Ordinal).ThenBy(b => string.Join("|", b.Subconto.Select(s => s.Id)), StringComparer.Ordinal).ToList();
            }
        }

        return result;
    }

    private static void Post(Dictionary<string, Acc> running, OneCEntrySideDto side, decimal amount, bool debit)
    {
        var account = AccountOf(side.Account!);
        var keep = side.Subconto.Where(s => !account.Subconto[s.Index - 1].TurnoverOnly).ToList();
        var key = $"{side.Account}|{string.Join("|", keep.Select(s => s.Id))}|{side.Currency}";
        if (!running.TryGetValue(key, out var acc))
        {
            acc = new Acc(side.Account!, keep, side.Currency, account.Quantity);
            running[key] = acc;
        }

        var sign = debit ? 1 : -1;
        acc.Amount += sign * amount;
        acc.CurrencyAmount += sign * (side.CurrencyAmount ?? 0);
        acc.QuantityAmount += sign * (side.Quantity ?? 0);
        if (debit)
        {
            acc.TurnDebit += amount;
            acc.TurnCurDebit += side.CurrencyAmount ?? 0;
            acc.TurnQtyDebit += side.Quantity ?? 0;
        }
        else
        {
            acc.TurnCredit += amount;
            acc.TurnCurCredit += side.CurrencyAmount ?? 0;
            acc.TurnQtyCredit += side.Quantity ?? 0;
        }
    }

    private sealed class Acc(string account, List<OneCSubcontoDto> subconto, string? currency, bool quantity)
    {
        public string Account { get; } = account;
        public List<OneCSubcontoDto> Subconto { get; } = subconto;
        public string? Currency { get; } = currency;
        public bool Quantity { get; } = quantity;
        public decimal Amount, CurrencyAmount, QuantityAmount;
        public decimal OpenAmount, OpenCurrency, OpenQuantity;
        public decimal TurnDebit, TurnCredit, TurnCurDebit, TurnCurCredit, TurnQtyDebit, TurnQtyCredit;

        public bool Any => OpenAmount != 0 || Amount != 0 || TurnDebit != 0 || TurnCredit != 0 || OpenQuantity != 0 || QuantityAmount != 0;

        public void StartMonth()
        {
            (OpenAmount, OpenCurrency, OpenQuantity) = (Amount, CurrencyAmount, QuantityAmount);
            TurnDebit = TurnCredit = TurnCurDebit = TurnCurCredit = TurnQtyDebit = TurnQtyCredit = 0;
        }

        /// <summary>Развёрнутое сальдо строки: положительное — в дебете, отрицательное — в кредите.</summary>
        public OneCBalanceAmountsDto Amounts(decimal amount, decimal cur, decimal qty) => new()
        {
            Debit = amount > 0 ? amount : 0, Credit = amount < 0 ? -amount : 0,
            CurrencyDebit = Currency is null ? null : cur > 0 ? cur : 0, CurrencyCredit = Currency is null ? null : cur < 0 ? -cur : 0,
            QuantityDebit = Quantity ? (qty > 0 ? qty : 0) : null, QuantityCredit = Quantity ? (qty < 0 ? -qty : 0) : null,
        };
    }

    public static string Serialize(object? value) => JsonSerializer.Serialize(value, Json);
}
