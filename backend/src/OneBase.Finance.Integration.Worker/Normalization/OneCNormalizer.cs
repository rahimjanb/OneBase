using System.Globalization;
using System.Text.Json;
using OneBase.Finance.Integration.Contracts;
using OneBase.Finance.Integration.Worker.OneC;

namespace OneBase.Finance.Integration.Worker.Normalization;

/// <summary>Запись 1С, не прошедшая проверку: сохраняется целиком (Payload) в очередь ошибок OneBase — данные не теряются.</summary>
public sealed record NormalizationRejection(string EntityType, string SourceId, string Reason, JsonElement Payload);

/// <summary>Нормализованные записи одной страницы 1С — содержимое пакета для внутреннего API OneBase.</summary>
public sealed class NormalizedBatch
{
    public List<ImportOrganization> Organizations { get; } = [];
    public List<ImportAccount> Accounts { get; } = [];
    public List<ImportCurrency> Currencies { get; } = [];
    public List<ImportExchangeRate> ExchangeRates { get; } = [];
    public List<ImportCounterparty> Counterparties { get; } = [];
    public List<ImportContract> Contracts { get; } = [];
    public List<ImportReference> References { get; } = [];
    public List<ImportItem> Items { get; } = [];
    public List<ImportDocument> Documents { get; } = [];
    public List<ImportEntrySet> EntrySets { get; } = [];
    public List<ImportBalance> Balances { get; } = [];
    public List<ImportDeletion> Deletions { get; } = [];
    public List<NormalizationRejection> Rejections { get; } = [];

    /// <summary>Самая ранняя дата изменённого документа по организациям — остатки пересчитываются с её месяца.</summary>
    public Dictionary<string, DateOnly> EarliestByOrganization { get; } = [];

    public int Count => Organizations.Count + Accounts.Count + Currencies.Count + ExchangeRates.Count + Counterparties.Count + Contracts.Count
        + References.Count + Items.Count + Documents.Count + EntrySets.Count + Balances.Count + Deletions.Count;

    public ImportBatchRequest ToRequest(Guid runId, string batchKey, string stream, string? organizationId) => new()
    {
        RunId = runId, BatchKey = batchKey, Stream = stream, OrganizationId = organizationId,
        Organizations = Nullable(Organizations), Accounts = Nullable(Accounts), Currencies = Nullable(Currencies), ExchangeRates = Nullable(ExchangeRates),
        Counterparties = Nullable(Counterparties), Contracts = Nullable(Contracts), References = Nullable(References), Items = Nullable(Items),
        Documents = Nullable(Documents), EntrySets = Nullable(EntrySets), Balances = Nullable(Balances), Deletions = Nullable(Deletions),
    };

    private static List<T>? Nullable<T>(List<T> list) => list.Count == 0 ? null : list;
}

/// <summary>
/// Проверка и приведение данных 1С к формату OneBase. Ничего не пересчитывает: суммы, курсы и даты — как в 1С. Сумма в валюте учёта, если
/// 1С её не прислала, берётся только по курсу самого документа (не по текущему). Персональные данные, которые OneBase не нужны (ПИНФЛ), не
/// переносятся. Неверная запись отклоняется отдельно — остальные идут дальше.
/// </summary>
public sealed class OneCNormalizer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Year(DateOnly d) => d.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static void Reject(NormalizedBatch b, string type, string? id, string reason, object record) =>
        b.Rejections.Add(new NormalizationRejection(type, string.IsNullOrWhiteSpace(id) ? "?" : id!, reason, JsonSerializer.SerializeToElement(record, record.GetType(), Json)));

    private static bool Missing(NormalizedBatch b, string type, OneCObject o, string? name, object record)
    {
        if (string.IsNullOrWhiteSpace(o.Id))
        {
            Reject(b, type, o.Id, "нет идентификатора 1С (id)", record);
            return true;
        }

        if (name is not null && string.IsNullOrWhiteSpace(name))
        {
            Reject(b, type, o.Id, "нет наименования", record);
            return true;
        }

        return false;
    }

    public void Add(NormalizedBatch b, OneCOrganizationDto o)
    {
        if (Missing(b, OneCEntityTypes.Organization, o, o.Name ?? "", o))
        {
            return;
        }

        b.Organizations.Add(new ImportOrganization
        {
            SourceId = o.Id!, Version = o.Version, ModifiedAt = o.ModifiedAt, Deleted = o.DeletionMark, Code = o.Code, Name = o.Name!.Trim(),
            FullName = o.FullName, Inn = o.Inn?.Trim(), VatCode = o.VatCode, AccountingCurrency = Upper(o.AccountingCurrency) ?? "UZS", IsActive = o.IsActive ?? true,
        });
    }

    public void Add(NormalizedBatch b, OneCAccountDto a)
    {
        if (Missing(b, OneCEntityTypes.Account, a, a.Name ?? "", a))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(a.Code))
        {
            Reject(b, OneCEntityTypes.Account, a.Id, "нет кода счёта", a);
            return;
        }

        var type = AccountType(a.Type);
        if (type is null)
        {
            Reject(b, OneCEntityTypes.Account, a.Id, $"неизвестный вид счёта «{a.Type}»", a);
            return;
        }

        b.Accounts.Add(new ImportAccount
        {
            SourceId = a.Id!, Version = a.Version, ModifiedAt = a.ModifiedAt, Deleted = a.DeletionMark, Code = a.Code.Trim(), Name = a.Name!.Trim(),
            ParentCode = a.ParentCode, Type = type, OffBalance = a.OffBalance, IsCurrency = a.Currency, IsQuantity = a.Quantity,
            Subconto = a.Subconto.Where(s => !string.IsNullOrWhiteSpace(s.Type)).Select(s => new ImportSubcontoType { Index = s.Index, Type = s.Type!, TurnoverOnly = s.TurnoverOnly }).ToList(),
        });
    }

    /// <summary>Вид счёта: код спецификации или как его называет 1С.</summary>
    public static string? AccountType(string? type) => (type ?? "").Replace(" ", "").Replace("-", "").ToLowerInvariant() switch
    {
        "active" or "активный" or "а" => "active",
        "passive" or "пассивный" or "п" => "passive",
        "active_passive" or "activepassive" or "активнопассивный" or "ап" => "active_passive",
        _ => null,
    };

    public void Add(NormalizedBatch b, OneCCounterpartyDto c)
    {
        if (Missing(b, OneCEntityTypes.Counterparty, c, c.Name ?? "", c))
        {
            return;
        }

        b.Counterparties.Add(new ImportCounterparty
        {
            SourceId = c.Id!, Version = c.Version, ModifiedAt = c.ModifiedAt, Deleted = c.DeletionMark, Code = c.Code, Name = c.Name!.Trim(), FullName = c.FullName,
            Inn = c.Inn?.Trim(), Kind = c.Kind, LegalForm = c.LegalForm, ParentId = c.ParentId, IsGroup = c.IsGroup, Address = c.Address, Phone = c.Phone,
            BankAccounts = c.BankAccounts.Select(x => new ImportBankAccount { Account = x.Account, Mfo = x.Mfo, Bank = x.Bank, Currency = Upper(x.Currency) }).ToList(),
        });
    }

    public void Add(NormalizedBatch b, OneCContractDto c)
    {
        if (Missing(b, OneCEntityTypes.Contract, c, c.Name ?? "", c))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(c.CounterpartyId) || string.IsNullOrWhiteSpace(c.OrganizationId))
        {
            Reject(b, OneCEntityTypes.Contract, c.Id, "у договора нет контрагента или организации", c);
            return;
        }

        b.Contracts.Add(new ImportContract
        {
            SourceId = c.Id!, Version = c.Version, ModifiedAt = c.ModifiedAt, Deleted = c.DeletionMark, OrganizationId = c.OrganizationId!, CounterpartyId = c.CounterpartyId!,
            Code = c.Code, Name = c.Name!.Trim(), Number = c.Number, Date = c.Date, ValidTo = c.ValidTo, PaymentTermDays = c.PaymentTermDays,
            Kind = c.Kind is "with_customer" or "with_supplier" ? c.Kind : "other", Currency = Upper(c.Currency) ?? "UZS",
        });
    }

    public void Add(NormalizedBatch b, OneCCurrencyDto c)
    {
        var code = Upper(c.Code);
        if (Missing(b, OneCEntityTypes.Currency, c, c.Name ?? "", c))
        {
            return;
        }

        if (code is not { Length: 3 })
        {
            Reject(b, OneCEntityTypes.Currency, c.Id, $"код валюты «{c.Code}» — не ISO 4217", c);
            return;
        }

        b.Currencies.Add(new ImportCurrency { SourceId = c.Id!, Version = c.Version, ModifiedAt = c.ModifiedAt, Deleted = c.DeletionMark, Code = code, NumericCode = c.NumericCode, Name = c.Name!.Trim() });
    }

    public void Add(NormalizedBatch b, OneCRateDto r)
    {
        var code = Upper(r.Currency);
        var key = code is null ? "?" : OneCEntityTypes.RateKey(code, r.Date);
        if (code is not { Length: 3 } || r.Rate <= 0 || r.Multiplier < 1)
        {
            Reject(b, OneCEntityTypes.ExchangeRate, key, "курс без валюты, с нулевым курсом или кратностью", r);
            return;
        }

        b.ExchangeRates.Add(new ImportExchangeRate { SourceId = key, Currency = code, Date = r.Date, Rate = r.Rate, Multiplier = r.Multiplier, Source = r.Source });
    }

    /// <summary>kind — warehouse, department или reference:&lt;вид&gt;.</summary>
    public void Add(NormalizedBatch b, OneCReferenceDto r, string kind)
    {
        if (Missing(b, kind, r, r.Name ?? "", r))
        {
            return;
        }

        b.References.Add(new ImportReference
        {
            SourceId = r.Id!, Version = r.Version, ModifiedAt = r.ModifiedAt, Deleted = r.DeletionMark, Kind = kind, OrganizationId = string.IsNullOrWhiteSpace(r.OrganizationId) ? null : r.OrganizationId,
            Code = r.Code, Name = r.Name!.Trim(), ParentId = r.ParentId, IsGroup = r.IsGroup, Attributes = r.Attributes,
        });
    }

    public void Add(NormalizedBatch b, OneCItemDto i)
    {
        if (Missing(b, OneCEntityTypes.Item, i, i.Name ?? "", i))
        {
            return;
        }

        b.Items.Add(new ImportItem
        {
            SourceId = i.Id!, Version = i.Version, ModifiedAt = i.ModifiedAt, Deleted = i.DeletionMark, Code = i.Code, Name = i.Name!.Trim(), FullName = i.FullName,
            Article = i.Article, Kind = i.Kind is "product" or "material" or "goods" or "service" or "semi_finished" ? i.Kind : "other", Unit = i.Unit,
            ParentId = i.ParentId, IsGroup = i.IsGroup, VatRate = i.VatRate, Ikpu = i.Ikpu, Barcode = i.Barcode,
        });
    }

    public void Add(NormalizedBatch b, OneCDocumentDto d)
    {
        if (Missing(b, OneCEntityTypes.Document, d, null, d))
        {
            return;
        }

        var problem = string.IsNullOrWhiteSpace(d.OrganizationId) ? "нет организации"
            : string.IsNullOrWhiteSpace(d.Number) ? "нет номера"
            : d.Date is null ? "нет даты"
            : null;
        var currency = Upper(d.Currency) ?? "UZS";
        var accounting = d.AmountAccounting;
        if (problem is null && accounting is null)
        {
            if (currency == "UZS")
            {
                accounting = d.Amount;
            }
            else if (d.Rate is { Value: > 0 } rate)
            {
                // Только по курсу самого документа: исторические суммы по текущему курсу не пересчитываются.
                accounting = Math.Round(d.Amount * rate.Value / Math.Max(1, rate.Multiplier), 2);
            }
            else
            {
                problem = "валютный документ без суммы в валюте учёта и без курса";
            }
        }

        if (problem is not null)
        {
            Reject(b, OneCEntityTypes.Document, d.Id, problem, d);
            return;
        }

        var type = OneCDocumentTypes.IsKnown(d.Type) ? d.Type! : OneCDocumentTypes.Other;
        b.Documents.Add(new ImportDocument
        {
            SourceId = d.Id!, Version = d.Version, ModifiedAt = d.ModifiedAt, Deleted = d.DeletionMark, OrganizationId = d.OrganizationId!, Type = type,
            SourceType = string.IsNullOrWhiteSpace(d.SourceType) ? type : d.SourceType!, Number = d.Number!.Trim(), Date = d.Date!.Value, Posted = d.Posted,
            CounterpartyId = d.CounterpartyId, ContractId = d.ContractId, WarehouseId = d.WarehouseId, DepartmentId = d.DepartmentId, Currency = currency,
            Amount = d.Amount, VatAmount = d.VatAmount, AmountAccounting = accounting!.Value, Rate = Rate(d.Rate), DueDate = d.DueDate,
            BasisDocumentId = d.BasisDocumentId, CashFlowItemId = d.CashFlowItemId, BankAccountId = d.BankAccountId, CashDeskId = d.CashDeskId,
            Purpose = d.Purpose, Comment = d.Comment,
            Lines = d.Lines.Select(l => new ImportDocumentLine
            {
                LineNumber = l.LineNumber, ItemId = l.ItemId, Quantity = l.Quantity, Unit = l.Unit, Price = l.Price, Amount = l.Amount, VatRate = l.VatRate,
                VatAmount = l.VatAmount, CostAmount = l.CostAmount, Account = l.Account, WarehouseId = l.WarehouseId, BatchId = l.BatchId,
            }).ToList(),
        });
        Earliest(b, d.OrganizationId!, d.Date!.Value);

        // Из журнала изменений документ приходит с проводками: перепроведение — новый набор, отмена проведения — пустой.
        if (d.Entries is not null)
        {
            Add(b, new OneCEntrySetDto
            {
                Registrar = new OneCRegistrarDto(d.Id, type, d.SourceType, d.Number, d.Date), OrganizationId = d.OrganizationId, Version = d.Version,
                Entries = d.Posted && !d.DeletionMark ? d.Entries : [],
            });
        }
    }

    public void Add(NormalizedBatch b, OneCEntrySetDto s)
    {
        var id = s.Registrar?.Id;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(s.OrganizationId))
        {
            Reject(b, OneCEntityTypes.Entry, id, "у набора проводок нет регистратора или организации", s);
            return;
        }

        var entries = new List<ImportEntry>();
        foreach (var e in s.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.Debit?.Account) || string.IsNullOrWhiteSpace(e.Credit?.Account))
            {
                Reject(b, OneCEntityTypes.Entry, id, $"проводка {e.LineNumber}: нет счёта дебета или кредита", s);
                return;
            }

            var period = e.Period ?? s.Registrar!.Date;
            if (period is null)
            {
                Reject(b, OneCEntityTypes.Entry, id, $"проводка {e.LineNumber}: нет периода", s);
                return;
            }

            entries.Add(new ImportEntry
            {
                LineNumber = e.LineNumber, Period = period.Value, Active = e.Active, Amount = e.Amount, Content = e.Content, DepartmentId = e.DepartmentId,
                Debit = Side(e.Debit!), Credit = Side(e.Credit!), Rate = Rate(e.Rate),
            });
        }

        b.EntrySets.Add(new ImportEntrySet
        {
            SourceId = id!, Version = s.Version, OrganizationId = s.OrganizationId!, RegistrarType = OneCDocumentTypes.IsKnown(s.Registrar!.Type) ? s.Registrar.Type! : OneCDocumentTypes.Other,
            RegistrarSourceType = s.Registrar.SourceType, RegistrarNumber = s.Registrar.Number, RegistrarDate = s.Registrar.Date, Entries = entries,
        });
        if (s.Registrar.Date is { } date)
        {
            Earliest(b, s.OrganizationId!, date);
        }
    }

    public void Add(NormalizedBatch b, OneCBalanceDto r)
    {
        if (string.IsNullOrWhiteSpace(r.OrganizationId) || string.IsNullOrWhiteSpace(r.Account)
            || !DateOnly.TryParseExact((r.Period ?? "") + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            Reject(b, OneCEntityTypes.Balance, $"{r.Period}:{r.Account}", "строка остатков без организации, счёта или месяца ГГГГ-ММ", r);
            return;
        }

        b.Balances.Add(new ImportBalance
        {
            OrganizationId = r.OrganizationId!, Period = month, Account = r.Account!.Trim(), Currency = Upper(r.Currency),
            Subconto = Subconto(r.Subconto), Opening = Amounts(r.Opening), Turnover = Amounts(r.Turnover), Closing = Amounts(r.Closing),
        });
    }

    /// <summary>Изменение из журнала 1С: объект нужного типа или удаление.</summary>
    public void Add(NormalizedBatch b, OneCChangeDto c)
    {
        var type = c.EntityType ?? "";
        if (string.IsNullOrWhiteSpace(c.Id))
        {
            Reject(b, type, null, "изменение без идентификатора", c);
            return;
        }

        if (c.Deleted)
        {
            b.Deletions.Add(new ImportDeletion { EntityType = type == "reference" ? OneCEntityTypes.ReferencePrefix + "unknown" : type, SourceId = c.Id!, OrganizationId = c.OrganizationId });
            return;
        }

        if (c.Data is not { ValueKind: JsonValueKind.Object } data)
        {
            Reject(b, type, c.Id, "изменение без данных", c);
            return;
        }

        try
        {
            switch (type)
            {
                case OneCEntityTypes.Organization: Add(b, Read<OneCOrganizationDto>(data)); break;
                case OneCEntityTypes.Account: Add(b, Read<OneCAccountDto>(data)); break;
                case OneCEntityTypes.Counterparty: Add(b, Read<OneCCounterpartyDto>(data)); break;
                case OneCEntityTypes.Contract: Add(b, Read<OneCContractDto>(data)); break;
                case OneCEntityTypes.Currency: Add(b, Read<OneCCurrencyDto>(data)); break;
                case OneCEntityTypes.ExchangeRate: Add(b, Read<OneCRateDto>(data)); break;
                case OneCEntityTypes.Item: Add(b, Read<OneCItemDto>(data)); break;
                case OneCEntityTypes.Document: Add(b, Read<OneCDocumentDto>(data)); break;
                case OneCEntityTypes.Warehouse or OneCEntityTypes.Department: Add(b, Read<OneCReferenceDto>(data), type); break;
                case "reference":
                    var reference = Read<OneCReferenceDto>(data);
                    Add(b, reference, OneCEntityTypes.Reference(reference.Kind ?? "other"));
                    break;
                case var t when t.StartsWith(OneCEntityTypes.ReferencePrefix, StringComparison.Ordinal): Add(b, Read<OneCReferenceDto>(data), t); break;
                default: Reject(b, type, c.Id, $"неизвестный тип объекта «{type}»", c); break;
            }
        }
        catch (JsonException e)
        {
            Reject(b, type, c.Id, $"данные объекта не в формате спецификации ({e.Path})", c);
        }
    }

    private static T Read<T>(JsonElement data) => data.Deserialize<T>(Json) ?? throw new JsonException("пусто");

    private static void Earliest(NormalizedBatch b, string org, DateTimeOffset date)
    {
        var day = DateOnly.FromDateTime(date.ToOffset(TimeSpan.FromHours(5)).DateTime);
        if (!b.EarliestByOrganization.TryGetValue(org, out var current) || day < current)
        {
            b.EarliestByOrganization[org] = day;
        }
    }

    private static ImportEntrySide Side(OneCEntrySideDto s) => new()
    {
        Account = s.Account!.Trim(), Subconto = Subconto(s.Subconto), Currency = Upper(s.Currency), CurrencyAmount = s.CurrencyAmount, Quantity = s.Quantity,
    };

    private static List<ImportSubconto> Subconto(List<OneCSubcontoDto> list) =>
        list.Where(x => !string.IsNullOrWhiteSpace(x.Type)).Select(x => new ImportSubconto { Index = x.Index, Type = x.Type!, Id = x.Id, Code = x.Code, Name = x.Name }).ToList();

    private static ImportBalanceAmounts Amounts(OneCBalanceAmountsDto? a) => a is null ? new() : new()
    {
        Debit = a.Debit, Credit = a.Credit, CurrencyDebit = a.CurrencyDebit, CurrencyCredit = a.CurrencyCredit, QuantityDebit = a.QuantityDebit, QuantityCredit = a.QuantityCredit,
    };

    private static ImportRate? Rate(OneCRateRefDto? r) => r is null || r.Value <= 0 ? null : new ImportRate { Value = r.Value, Multiplier = Math.Max(1, r.Multiplier), Date = r.Date, Source = r.Source };

    private static string? Upper(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();
}
