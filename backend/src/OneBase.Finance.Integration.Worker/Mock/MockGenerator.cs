using OneBase.Finance.Integration.Contracts;
using OneBase.Finance.Integration.Worker.OneC;
using static OneBase.Finance.Integration.Worker.Mock.MockCatalog;

namespace OneBase.Finance.Integration.Worker.Mock;

/// <summary>
/// Генерация документов и проводок одной организации по дням. Состояние ведётся по ходу: долги покупателей и поставщикам (FIFO), остатки
/// материалов и продукции, валютный счёт и его учётная стоимость, обороты доходов и расходов месяца (для налога на прибыль и закрытия на 9910).
/// </summary>
internal sealed class Generator(MockDataSet set, MockCatalog.Org org, MockOptions options)
{
    private sealed class Open
    {
        public DateOnly Date;
        public decimal Uzs;
        public decimal Cur;
    }

    private readonly Dictionary<string, int> _numbers = [];
    private readonly Dictionary<int, List<Open>> _receivables = [];
    private readonly Dictionary<int, List<Open>> _payables = [];
    private readonly decimal[] _materialQty = new decimal[Materials.Length];
    private readonly decimal[] _materialCost = Materials.Select(m => m.Cost).ToArray();
    private readonly decimal[] _productQty = new decimal[Products.Length];
    private readonly decimal[] _productValue = new decimal[Products.Length];
    private readonly decimal[] _monthSold = new decimal[Products.Length];
    private readonly decimal[] _dailyUse = new decimal[Materials.Length];
    private readonly Dictionary<string, (OneCSubcontoDto? Sub, decimal Net)> _pnl = [];
    private decimal _vatCarry;
    private decimal _usd, _usdBook, _vatOut, _vatIn, _vatDue, _taxDue, _payrollDue, _esnDue, _loan, _quarterProfit, _dividendsDue;
    private Random _rng = new(0);

    private decimal Scale => (decimal)(org.Volume * Math.Max(0.05, options.Volume));

    /// <summary>Средняя себестоимость единицы продукции на складе.</summary>
    private decimal UnitCost(int i) => _productQty[i] > 0 ? _productValue[i] / _productQty[i] : Products[i].Cost;

    private int Count(double min, double max) => (int)Math.Round((min + _rng.NextDouble() * (max - min)) * org.Volume * Math.Max(0.05, options.Volume));

    public void Run(DateOnly start, DateOnly today)
    {
        _rng = new Random(options.Seed ^ (org.Index * 1_000_003));
        Opening(start);
        for (var d = start; d <= today; d = d.AddDays(1))
        {
            _rng = new Random(options.Seed ^ (d.DayNumber * 31) ^ (org.Index * 1_000_003));
            if (d.DayOfWeek != DayOfWeek.Sunday)
            {
                Day(d);
            }

            if (d.AddDays(1).Day == 1)
            {
                MonthEnd(d);
            }
        }
    }

    // ---------- Документы и проводки ----------

    private string Number(string type) => $"{org.Index + 1:00}-{(_numbers[type] = _numbers.GetValueOrDefault(type) + 1):000000}";

    private MockDocument Doc(string type, string sourceType, DateOnly day, int hour, int minute, Party? party = null, string currency = "UZS")
    {
        var at = MockDataSet.At(day, hour, minute);
        var doc = new MockDocument
        {
            Org = org.Index, ModifiedAt = at.AddMinutes(5 + _rng.Next(40)),
            Doc = new OneCDocumentDto
            {
                Id = MockCatalog.Id($"doc:{org.Index}:{type}:{day:yyyyMMdd}:{_numbers.GetValueOrDefault(type) + 1}"), Version = "v1", Type = type, SourceType = sourceType,
                Number = Number(type), Date = at, OrganizationId = org.Id, Posted = true, CounterpartyId = party?.Id,
                ContractId = party is null ? null : ContractId(party, org.Index), Currency = currency,
            },
        };
        set.Documents.Add(doc);
        return doc;
    }

    private static void Finish(MockDocument d, decimal amount, decimal accounting, decimal? vat = null, decimal? rate = null, DateOnly? due = null, List<OneCDocumentLineDto>? lines = null,
        string? warehouse = null, string? department = null, string? cashFlow = null, string? bank = null, string? cashDesk = null, string? purpose = null)
    {
        d.Doc = d.Doc with
        {
            Amount = Math.Round(amount, 2), AmountAccounting = Math.Round(accounting, 2), VatAmount = vat is null ? null : Math.Round(vat.Value, 2),
            Rate = rate is { } r ? new OneCRateRefDto(r, 1, MockDataSet.DayOf(d.Doc.Date!.Value), "ЦБ РУз") : null, DueDate = due, Lines = lines ?? [],
            WarehouseId = warehouse, DepartmentId = department, CashFlowItemId = cashFlow, BankAccountId = bank, CashDeskId = cashDesk, Purpose = purpose,
            ModifiedAt = d.ModifiedAt,
        };
    }

    private void Entry(MockDocument d, decimal amount, OneCEntrySideDto debit, OneCEntrySideDto credit, string content, decimal? rate = null)
    {
        amount = Math.Round(amount, 2);
        if (amount == 0)
        {
            return;
        }

        d.Entries.Add(new OneCEntryDto
        {
            LineNumber = d.Entries.Count + 1, Period = d.Doc.Date, Amount = amount, Content = content, Debit = debit, Credit = credit,
            Rate = rate is { } r ? new OneCRateRefDto(r, 1, MockDataSet.DayOf(d.Doc.Date!.Value), "ЦБ РУз") : null,
        });
        Track(debit, amount);
        Track(credit, -amount);
    }

    /// <summary>Обороты доходов и расходов месяца по счетам и первому субконто — для закрытия месяца на 9910.</summary>
    private void Track(OneCEntrySideDto side, decimal signed)
    {
        if (!side.Account!.StartsWith('9') || side.Account.StartsWith("99"))
        {
            return;
        }

        var sub = side.Subconto.FirstOrDefault();
        var key = side.Account + "|" + sub?.Id;
        var (_, net) = _pnl.GetValueOrDefault(key);
        _pnl[key] = (sub, net + signed);
    }

    private static OneCEntrySideDto Side(string account, IEnumerable<OneCSubcontoDto>? sub = null, string? currency = null, decimal? currencyAmount = null, decimal? quantity = null) => new()
    {
        Account = account, Subconto = sub?.ToList() ?? [], Currency = currency, CurrencyAmount = currencyAmount is null ? null : Math.Round(currencyAmount.Value, 2),
        Quantity = quantity is null ? null : Math.Round(quantity.Value, 3),
    };

    private IEnumerable<OneCSubcontoDto> Cp(Party p) =>
    [
        new(1, SubCounterparty, p.Id, p.Code, p.Name),
        new(2, SubContract, ContractId(p, org.Index), $"{p.Code}/{org.Index + 1}", $"Договор № {p.Code}/{org.Index + 1}"),
    ];

    private static IEnumerable<OneCSubcontoDto> ItemWh(Product p, MockCatalog.Ref wh) =>
        [new(1, SubItem, p.Id, p.Code, p.Name), new(2, SubWarehouse, wh.Id, wh.Code, wh.Name)];

    private static IEnumerable<OneCSubcontoDto> Item(Product p) => [new(1, SubItem, p.Id, p.Code, p.Name)];

    private IEnumerable<OneCSubcontoDto> Bank(string currency, string flow)
    {
        var bank = BankAccount(org.Index, currency);
        var cfi = CashFlow(flow);
        return [new(1, SubBankAccount, bank.Id, bank.Code, bank.Name), new(2, SubCashFlow, cfi.Id, cfi.Code, cfi.Name)];
    }

    private static IEnumerable<OneCSubcontoDto> CashSub(string flow)
    {
        var cfi = CashFlow(flow);
        return [new(1, SubCashFlow, cfi.Id, cfi.Code, cfi.Name)];
    }

    private IEnumerable<OneCSubcontoDto> Dept(string key)
    {
        var d = Department(org.Index, key);
        return [new(1, SubDepartment, d.Id, d.Code, d.Name)];
    }

    private static IEnumerable<OneCSubcontoDto> CostSub(string key)
    {
        var c = Cost(key);
        return [new(1, SubCostItem, c.Id, c.Code, c.Name)];
    }

    private IEnumerable<Party> MyCustomers => Customers.Where(c => c.Orgs.Contains(org.Index));

    private IEnumerable<Party> MySuppliers => Suppliers.Where(s => s.Orgs.Contains(org.Index));

    // ---------- Начальные остатки ----------

    private void Opening(DateOnly start)
    {
        var d = Doc(OneCDocumentTypes.Manual, "ВводНачальныхОстатков", start, 7, 30);
        var capital = org.Index == 0 ? 8_000_000_000m : 3_500_000_000m;
        Entry(d, capital, Side("5110", Bank("UZS", "other")), Side("8330"), "Уставный капитал на расчётном счёте");
        _loan = org.Index == 0 ? 6_000_000_000m : 0;
        if (_loan > 0)
        {
            Entry(d, _loan, Side("5110", Bank("UZS", "other")), Side("6810", [new OneCSubcontoDto(1, SubCounterparty, MockCatalog.Bank.Id, MockCatalog.Bank.Code, MockCatalog.Bank.Name)]), "Остаток кредита банка");
        }
        for (var i = 0; i < Materials.Length; i++)
        {
            // Дневной расход — по рецептуре на ~10 тыс. единиц выпуска; на складе — запас на 20 дней.
            _dailyUse[i] = Math.Round(Recipe[i] * 10_000m * Scale, 3);
            _materialQty[i] = Math.Round(_dailyUse[i] * 20, 0);
            Entry(d, _materialQty[i] * _materialCost[i], Side("1010", ItemWh(Materials[i], RawWarehouse), quantity: _materialQty[i]), Side("8710"), "Начальный остаток материалов");
        }

        for (var i = 0; i < Products.Length; i++)
        {
            _productQty[i] = Math.Round(6_000 * Scale, 0);
            _productValue[i] = _productQty[i] * Products[i].Cost;
            Entry(d, _productValue[i], Side("2810", ItemWh(Products[i], FinishedWarehouse), quantity: _productQty[i]), Side("8710"), "Начальный остаток продукции");
        }

        var rate = set.Rate("USD", start);
        _usd = org.Index == 0 ? 60_000 : 15_000;
        _usdBook = Math.Round(_usd * rate, 2);
        Entry(d, _usdBook, Side("5210", Bank("USD", "other"), "USD", _usd), Side("8710"), "Начальный остаток валютного счёта", rate);

        foreach (var c in MyCustomers.Where((_, i) => i % 3 == 0))
        {
            var debt = Mock.RoundTo(40_000_000m + c.Code.Sum(ch => ch) * 150_000m, 1000);
            Entry(d, debt, Side("4010", Cp(c), c.Currency == "UZS" ? null : c.Currency, c.Currency == "UZS" ? null : debt / rate), Side("8710"), "Начальная задолженность покупателя");
            Owe(_receivables, Array.IndexOf(Customers, c), start.AddDays(-25), debt, c.Currency == "UZS" ? 0 : Math.Round(debt / rate, 2));
        }

        // Прошлые доходы и расходы в начальных остатках не участвуют: закрытие не нужно.
        _pnl.Clear();
        Finish(d, 0, 0, purpose: "Ввод начальных остатков");
    }

    private static void Owe(Dictionary<int, List<Open>> book, int who, DateOnly date, decimal uzs, decimal cur)
    {
        if (!book.TryGetValue(who, out var list))
        {
            list = [];
            book[who] = list;
        }

        list.Add(new Open { Date = date, Uzs = uzs, Cur = cur });
    }

    // ---------- День ----------

    private void Day(DateOnly d)
    {
        Production(d);
        var sales = Count(4, 8);
        for (var i = 0; i < sales; i++)
        {
            Sale(d, 9 + i % 8);
        }

        if (_rng.NextDouble() < 0.05)
        {
            Return(d);
        }

        // Закупка — когда запаса меньше чем на 10 дней: заказ на 20 дней расхода.
        for (var m = 0; m < Materials.Length; m++)
        {
            if (_materialQty[m] < _dailyUse[m] * 10 && _rng.NextDouble() < 0.7)
            {
                Purchase(d, m);
            }
        }

        CustomerPayments(d);
        SupplierPayments(d);
        Expenses(d);
        Payroll(d);
        Financing(d);
        DuplicatePayment(d);
    }

    private int PickCustomer()
    {
        var list = MyCustomers.ToList();
        var total = list.Sum(c => c.Weight);
        var x = _rng.NextDouble() * total;
        foreach (var c in list)
        {
            x -= c.Weight;
            if (x <= 0)
            {
                return Array.IndexOf(Customers, c);
            }
        }

        return Array.IndexOf(Customers, list[^1]);
    }

    private void Sale(DateOnly d, int hour)
    {
        var who = PickCustomer();
        var customer = Customers[who];
        var usd = customer.Currency == "USD";
        var rate = set.Rate("USD", d);
        var doc = Doc(OneCDocumentTypes.Sale, "РеализацияТоваровУслуг", d, hour, _rng.Next(60), customer, customer.Currency);
        var lines = new List<OneCDocumentLineDto>();
        decimal netTotal = 0, vatTotal = 0;
        var count = 1 + _rng.Next(4);
        foreach (var index in Enumerable.Range(0, Products.Length).OrderBy(_ => _rng.Next()).Take(count))
        {
            var p = Products[index];
            var qty = (decimal)(p.Unit == "кг" ? 100 + _rng.Next(600) : 200 + _rng.Next(1_300));
            qty = Math.Min(qty, Math.Floor(_productQty[index]));
            if (qty <= 0)
            {
                continue;
            }

            var price = Mock.RoundTo(p.Price * (decimal)(0.97 + _rng.NextDouble() * 0.06), 100);
            var net = qty * price;
            var vat = Math.Round(net * 0.12m, 2);
            var cost = Math.Round(qty * UnitCost(index), 2);
            _productValue[index] -= cost;
            _productQty[index] -= qty;
            _monthSold[index] += qty;
            netTotal += net;
            vatTotal += vat;
            lines.Add(new OneCDocumentLineDto
            {
                LineNumber = lines.Count + 1, ItemId = p.Id, Quantity = qty, Unit = p.Unit, Price = usd ? Math.Round(price / rate, 4) : price,
                Amount = usd ? Math.Round(net / rate, 2) : net, VatRate = 12, VatAmount = usd ? Math.Round(vat / rate, 2) : vat, CostAmount = cost, Account = "2810",
                WarehouseId = FinishedWarehouse.Id,
            });
            Entry(doc, net, Side("4010", Cp(customer), usd ? "USD" : null, usd ? net / rate : null), Side("9010", Item(p)), "Выручка от реализации продукции", usd ? rate : null);
            Entry(doc, vat, Side("4010", Cp(customer), usd ? "USD" : null, usd ? vat / rate : null), Side("6410"), "НДС с реализации", usd ? rate : null);
            Entry(doc, cost, Side("9110", Item(p)), Side("2810", ItemWh(p, FinishedWarehouse), quantity: qty), "Себестоимость реализованной продукции");
        }

        var total = netTotal + vatTotal;
        _vatOut += vatTotal;
        Owe(_receivables, who, d, total, usd ? Math.Round(total / rate, 2) : 0);
        Finish(doc, usd ? total / rate : total, total, vatTotal == 0 ? null : usd ? vatTotal / rate : vatTotal, usd ? rate : null, d.AddDays(customer.TermDays), lines,
            FinishedWarehouse.Id, Department(org.Index, "sales").Id);
    }

    private void Return(DateOnly d)
    {
        var candidates = _receivables.Where(r => r.Value.Count > 0 && Customers[r.Key].Currency == "UZS").Select(r => r.Key).ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var who = candidates[_rng.Next(candidates.Count)];
        var customer = Customers[who];
        var index = _rng.Next(Products.Length);
        var p = Products[index];
        var qty = (decimal)(10 + _rng.Next(40));
        var net = qty * p.Price;
        var vat = Math.Round(net * 0.12m, 2);
        var cost = Math.Round(qty * UnitCost(index), 2);
        var doc = Doc(OneCDocumentTypes.SaleReturn, "ВозвратТоваровОтПокупателя", d, 16, _rng.Next(60), customer);
        Entry(doc, net, Side("9040", Item(p)), Side("4010", Cp(customer)), "Возврат продукции от покупателя");
        Entry(doc, vat, Side("6410"), Side("4010", Cp(customer)), "НДС по возврату");
        Entry(doc, cost, Side("2810", ItemWh(p, FinishedWarehouse), quantity: qty), Side("9110", Item(p)), "Себестоимость возвращённой продукции");
        _productQty[index] += qty;
        _productValue[index] += cost;
        _vatOut -= vat;
        Pay(_receivables[who], net + vat, 0);
        Finish(doc, net + vat, net + vat, vat, lines:
            [new OneCDocumentLineDto { LineNumber = 1, ItemId = p.Id, Quantity = qty, Unit = p.Unit, Price = p.Price, Amount = net, VatRate = 12, VatAmount = vat, CostAmount = cost, Account = "2810" }],
            warehouse: FinishedWarehouse.Id);
    }

    private void Purchase(DateOnly d, int m)
    {
        var material = Materials[m];
        var supplier = Suppliers[SupplierOf(m)];
        var usd = supplier.Currency == "USD";
        var rate = set.Rate("USD", d);
        var qty = Math.Max(1, Math.Round(_dailyUse[m] * (decimal)(18 + _rng.Next(5)), 0));
        var inflation = 1 + (d.DayNumber - set.Start.DayNumber) * 0.0002m;
        var price = Math.Round(material.Cost * inflation * (decimal)(0.96 + _rng.NextDouble() * 0.08), 2);
        var net = Math.Round(qty * price, 2);
        var vat = usd ? 0 : Math.Round(net * 0.12m, 2);
        var doc = Doc(OneCDocumentTypes.Purchase, "ПоступлениеТоваровУслуг", d, 10 + _rng.Next(7), _rng.Next(60), supplier, supplier.Currency);
        Entry(doc, net, Side("1010", ItemWh(material, RawWarehouse), quantity: qty), Side("6010", Cp(supplier), usd ? "USD" : null, usd ? net / rate : null), "Поступление материалов", usd ? rate : null);
        if (vat > 0)
        {
            Entry(doc, vat, Side("4410"), Side("6010", Cp(supplier)), "НДС к зачёту");
        }

        _materialCost[m] = (_materialQty[m] * _materialCost[m] + net) / (_materialQty[m] + qty);
        _materialQty[m] += qty;
        _vatIn += vat;
        Owe(_payables, Array.IndexOf(Suppliers, supplier), d, net + vat, usd ? Math.Round((net + vat) / rate, 2) : 0);
        Finish(doc, usd ? (net + vat) / rate : net + vat, net + vat, vat == 0 ? null : vat, usd ? rate : null, d.AddDays(supplier.TermDays),
            [new OneCDocumentLineDto { LineNumber = 1, ItemId = material.Id, Quantity = qty, Unit = material.Unit, Price = usd ? Math.Round(price / rate, 4) : price, Amount = usd ? Math.Round(net / rate, 2) : net, VatRate = vat == 0 ? 0 : 12, VatAmount = vat == 0 ? null : vat, Account = "1010", WarehouseId = RawWarehouse.Id }],
            RawWarehouse.Id);
    }

    private static readonly decimal[] Recipe = [0.45m, 0.05m, 0.2m, 0.08m, 0.1m, 0.01m, 0.1m, 0.001m, 0.03m];

    private void Production(DateOnly d)
    {
        // Выпуск — по продукции, которой на складе меньше всего.
        // Выпуск — продукции, которой на складе меньше всего (за складом в 40 тыс. единиц — не выпускается).
        var make = Enumerable.Range(0, Products.Length).Where(i => _productQty[i] < 25_000m * Scale).OrderBy(i => _productQty[i]).Take(4).ToList();
        var outputs = make.Select(i => (Index: i, Qty: Math.Round((1_200 + _rng.Next(1_600)) * Scale, 0))).ToList();
        var volume = outputs.Sum(o => o.Qty);
        var issue = Doc(OneCDocumentTypes.MaterialIssue, "ТребованиеНакладная", d, 8, _rng.Next(30));
        decimal materialsCost = 0;
        var issueLines = new List<OneCDocumentLineDto>();
        for (var m = 0; m < Materials.Length; m++)
        {
            var qty = Math.Min(Math.Round(volume * Recipe[m], 3), _materialQty[m]);
            if (qty <= 0)
            {
                continue;
            }

            var amount = Math.Round(qty * _materialCost[m], 2);
            _materialQty[m] -= qty;
            _dailyUse[m] = Math.Round(_dailyUse[m] * 0.9m + qty * 0.1m, 3);
            materialsCost += amount;
            Entry(issue, amount, Side("2010", Dept("prod")), Side("1010", ItemWh(Materials[m], RawWarehouse), quantity: qty), "Передача материалов в производство");
            issueLines.Add(new OneCDocumentLineDto { LineNumber = issueLines.Count + 1, ItemId = Materials[m].Id, Quantity = qty, Unit = Materials[m].Unit, Amount = amount, CostAmount = amount, Account = "1010", WarehouseId = RawWarehouse.Id });
        }

        Finish(issue, materialsCost, materialsCost, lines: issueLines, warehouse: RawWarehouse.Id, department: Department(org.Index, "prod").Id);

        var output = Doc(OneCDocumentTypes.Production, "ОтчетПроизводстваЗаСмену", d, 19, _rng.Next(50));
        var lines = new List<OneCDocumentLineDto>();
        foreach (var (index, qty) in outputs)
        {
            var share = volume == 0 ? 0 : Math.Round(materialsCost * qty / volume, 2);
            _productQty[index] += qty;
            _productValue[index] += share;
            Entry(output, share, Side("2810", ItemWh(Products[index], FinishedWarehouse), quantity: qty), Side("2010", Dept("prod")), "Выпуск продукции");
            lines.Add(new OneCDocumentLineDto { LineNumber = lines.Count + 1, ItemId = Products[index].Id, Quantity = qty, Unit = Products[index].Unit, Amount = share, CostAmount = share, Account = "2810", WarehouseId = FinishedWarehouse.Id });
        }

        Finish(output, lines.Sum(l => l.Amount), lines.Sum(l => l.Amount), lines: lines, warehouse: FinishedWarehouse.Id, department: Department(org.Index, "prod").Id);
    }

    /// <summary>Гасит долги FIFO (самые старые первыми); возвращает учётную стоимость погашенной части.</summary>
    private static decimal Pay(List<Open> opens, decimal uzs, decimal cur)
    {
        decimal book = 0;
        while (opens.Count > 0 && (cur > 0 ? cur > 0.005m : uzs > 0.005m))
        {
            var o = opens[0];
            if (cur > 0)
            {
                var part = Math.Min(cur, o.Cur);
                var bookPart = o.Cur == 0 ? 0 : Math.Round(o.Uzs * part / o.Cur, 2);
                o.Cur -= part;
                o.Uzs -= bookPart;
                cur -= part;
                book += bookPart;
                if (o.Cur <= 0.005m)
                {
                    opens.RemoveAt(0);
                }
            }
            else
            {
                var part = Math.Min(uzs, o.Uzs);
                o.Uzs -= part;
                uzs -= part;
                book += part;
                if (o.Uzs <= 0.005m)
                {
                    opens.RemoveAt(0);
                }
            }
        }

        return book;
    }

    private void CustomerPayments(DateOnly d)
    {
        foreach (var (who, opens) in _receivables.Where(r => r.Value.Count > 0).ToList())
        {
            var customer = Customers[who];
            var chance = customer.LatePayer ? 0.07 : 0.24;
            var lag = customer.LatePayer ? customer.TermDays + 35 : 2 + _rng.Next(customer.TermDays);
            if (_rng.NextDouble() > chance)
            {
                continue;
            }

            var due = opens.Where(o => d.DayNumber - o.Date.DayNumber >= lag).ToList();
            if (due.Count == 0)
            {
                continue;
            }

            var share = (decimal)(0.6 + _rng.NextDouble() * 0.4);
            if (customer.Currency == "USD")
            {
                var rate = set.Rate("USD", d);
                var cur = Math.Round(due.Sum(o => o.Cur) * share, 2);
                var book = Pay(opens, 0, cur);
                var paid = Math.Round(cur * rate, 2);
                var doc = Doc(OneCDocumentTypes.BankIn, "ПоступлениеНаРасчетныйСчет", d, 11, _rng.Next(60), customer, "USD");
                Entry(doc, paid, Side("5210", Bank("USD", "customers"), "USD", cur), Side("4010", Cp(customer), "USD", cur), "Оплата от покупателя (валюта)", rate);
                if (book > paid)
                {
                    Entry(doc, book - paid, Side("9620"), Side("4010", Cp(customer), "USD", 0), "Курсовая разница по расчётам с покупателем");
                }
                else if (paid > book)
                {
                    Entry(doc, paid - book, Side("4010", Cp(customer), "USD", 0), Side("9540"), "Курсовая разница по расчётам с покупателем");
                }

                _usd += cur;
                _usdBook += paid;
                Finish(doc, cur, paid, rate: rate, cashFlow: CashFlow("customers").Id, bank: BankAccount(org.Index, "USD").Id, purpose: "Оплата по договору");
                continue;
            }

            var amount = Mock.RoundTo(due.Sum(o => o.Uzs) * share, 1000);
            Pay(opens, amount, 0);
            var cash = who == 12;
            var payment = Doc(cash ? OneCDocumentTypes.CashIn : OneCDocumentTypes.BankIn, cash ? "ПриходныйКассовыйОрдер" : "ПоступлениеНаРасчетныйСчет", d, 12, _rng.Next(60), customer);
            Entry(payment, amount, cash ? Side("5010", CashSub("customers")) : Side("5110", Bank("UZS", "customers")), Side("4010", Cp(customer)), "Оплата от покупателя");
            Finish(payment, amount, amount, cashFlow: CashFlow("customers").Id, bank: cash ? null : BankAccount(org.Index, "UZS").Id,
                cashDesk: cash ? CashDesks[org.Index].Id : null, purpose: "Оплата за кондитерские изделия");
        }
    }

    private void SupplierPayments(DateOnly d)
    {
        if (_rng.NextDouble() > 0.4)
        {
            return;
        }

        foreach (var (who, opens) in _payables.Where(p => p.Value.Count > 0).ToList())
        {
            var supplier = Suppliers[who];
            var due = opens.Where(o => d.DayNumber - o.Date.DayNumber >= Math.Min(supplier.TermDays, 5 + _rng.Next(10))).ToList();
            if (due.Count == 0)
            {
                continue;
            }

            if (supplier.Currency == "USD")
            {
                var rate = set.Rate("USD", d);
                var cur = due.Sum(o => o.Cur);
                if (_usd < cur)
                {
                    // Покупка валюты на бирже: перевод между своими счетами.
                    var buy = Math.Ceiling(cur - _usd + 5_000);
                    var conv = Doc(OneCDocumentTypes.BankOut, "СписаниеСРасчетногоСчета", d, 10, 5);
                    Entry(conv, buy * rate, Side("5210", Bank("USD", "transfer"), "USD", buy), Side("5110", Bank("UZS", "transfer")), "Покупка валюты", rate);
                    _usd += buy;
                    _usdBook += Math.Round(buy * rate, 2);
                    Finish(conv, buy * rate, buy * rate, cashFlow: CashFlow("transfer").Id, bank: BankAccount(org.Index, "UZS").Id, purpose: "Покупка иностранной валюты");
                }

                var book = Pay(opens, 0, cur);
                var paid = Math.Round(cur * rate, 2);
                var doc = Doc(OneCDocumentTypes.BankOut, "СписаниеСРасчетногоСчета", d, 14, _rng.Next(60), supplier, "USD");
                Entry(doc, Math.Min(book, paid), Side("6010", Cp(supplier), "USD", cur), Side("5210", Bank("USD", "suppliers"), "USD", cur), "Оплата поставщику (валюта)", rate);
                if (paid > book)
                {
                    Entry(doc, paid - book, Side("9620"), Side("5210", Bank("USD", "suppliers"), "USD", 0), "Курсовая разница по расчётам с поставщиком");
                }
                else if (book > paid)
                {
                    Entry(doc, book - paid, Side("6010", Cp(supplier), "USD", 0), Side("9540"), "Курсовая разница по расчётам с поставщиком");
                }

                var bookOut = _usd == 0 ? 0 : Math.Round(_usdBook * cur / _usd, 2);
                _usd -= cur;
                _usdBook -= bookOut;
                Finish(doc, cur, paid, rate: rate, cashFlow: CashFlow("suppliers").Id, bank: BankAccount(org.Index, "USD").Id, purpose: "Оплата за какао-продукты");
                continue;
            }

            var amount = due.Sum(o => o.Uzs);
            Pay(opens, amount, 0);
            var payment = Doc(OneCDocumentTypes.BankOut, "СписаниеСРасчетногоСчета", d, 15, _rng.Next(60), supplier);
            Entry(payment, amount, Side("6010", Cp(supplier)), Side("5110", Bank("UZS", "suppliers")), "Оплата поставщику");
            Finish(payment, amount, amount, cashFlow: CashFlow("suppliers").Id, bank: BankAccount(org.Index, "UZS").Id, purpose: "Оплата по договору поставки");
        }
    }

    private readonly Dictionary<int, int> _duplicates = [];

    /// <summary>Раз в год — одна и та же оплата перевозчику в два рабочих дня подряд с 16 марта (ошибка бухгалтера): проверка поиска двойных платежей.</summary>
    private void DuplicatePayment(DateOnly d)
    {
        if (d.Month != 3 || d.Day < 16 || _duplicates.GetValueOrDefault(d.Year) >= 2)
        {
            return;
        }

        _duplicates[d.Year] = _duplicates.GetValueOrDefault(d.Year) + 1;

        var amount = org.Index == 0 ? 27_500_000m : 11_200_000m;
        var doc = Doc(OneCDocumentTypes.BankOut, "СписаниеСРасчетногоСчета", d, 9, 20, Logistics);
        Entry(doc, amount, Side("6010", Cp(Logistics)), Side("5110", Bank("UZS", "suppliers")), "Оплата перевозчику");
        Finish(doc, amount, amount, cashFlow: CashFlow("suppliers").Id, bank: BankAccount(org.Index, "UZS").Id, purpose: "Оплата за доставку");
    }

    private void Expenses(DateOnly d)
    {
        void Service(Party supplier, string account, string cost, decimal amount, string content)
        {
            var vat = Math.Round(amount * 0.12m, 2);
            var doc = Doc(OneCDocumentTypes.Purchase, "ПоступлениеТоваровУслуг", d, 9, 15 + _rng.Next(40), supplier);
            Entry(doc, amount, Side(account, CostSub(cost)), Side("6010", Cp(supplier)), content);
            Entry(doc, vat, Side("4410"), Side("6010", Cp(supplier)), "НДС к зачёту");
            _vatIn += vat;
            Owe(_payables, Array.IndexOf(Suppliers, supplier), d, amount + vat, 0);
            Finish(doc, amount + vat, amount + vat, vat, due: d.AddDays(supplier.TermDays), department: Department(org.Index, account == "9410" ? "sales" : "admin").Id);
        }

        var scale = (decimal)org.Volume;
        if (d.Day <= 2 && d.DayOfWeek != DayOfWeek.Sunday && !_rentMonths.Contains((d.Year, d.Month)))
        {
            _rentMonths.Add((d.Year, d.Month));
            Service(Landlord, "9420", "rent", Mock.RoundTo(85_000_000m * scale, 1000), "Аренда офиса и склада");
        }

        if (d.Day == 10)
        {
            Service(Utility, "9420", "utilities", Mock.RoundTo((30_000_000m + _rng.Next(12_000_000)) * scale, 1000), "Электроэнергия, газ, вода");
        }

        if (d.Day == 15)
        {
            // Реклама: в ноябре-декабре — сезонная кампания (рост расходов).
            var ads = d.Month is 11 or 12 ? 120_000_000m : 35_000_000m;
            Service(Advertising, "9410", "ads", Mock.RoundTo((ads + _rng.Next(8_000_000)) * scale, 1000), "Реклама и продвижение");
        }

        if (d.DayOfWeek == DayOfWeek.Monday)
        {
            Service(Logistics, "9410", "transport", Mock.RoundTo((18_000_000m + _rng.Next(9_000_000)) * scale, 1000), "Доставка продукции покупателям");
        }

        if (_rng.NextDouble() < 0.3)
        {
            var amount = Mock.RoundTo(200_000m + _rng.Next(1_300_000), 1000);
            var doc = Doc(OneCDocumentTypes.CashOut, "РасходныйКассовыйОрдер", d, 13, _rng.Next(60));
            Entry(doc, amount, Side("9420", CostSub("office")), Side("5010", CashSub("other")), "Хозяйственные расходы");
            Finish(doc, amount, amount, cashFlow: CashFlow("other").Id, cashDesk: CashDesks[org.Index].Id, purpose: "Хозяйственные нужды");
        }

        if (d.DayOfWeek == DayOfWeek.Friday)
        {
            // Сдача выручки из кассы в банк — перемещение между своими счетами.
            var amount = Mock.RoundTo(5_000_000m + _rng.Next(10_000_000), 1000);
            var doc = Doc(OneCDocumentTypes.BankIn, "ПоступлениеНаРасчетныйСчет", d, 17, 30);
            Entry(doc, amount, Side("5110", Bank("UZS", "transfer")), Side("5010", CashSub("transfer")), "Сдача наличных в банк");
            Finish(doc, amount, amount, cashFlow: CashFlow("transfer").Id, bank: BankAccount(org.Index, "UZS").Id, purpose: "Взнос наличных");
        }

        if (d.Day == 25)
        {
            var fee = Mock.RoundTo(1_200_000m + _rng.Next(600_000), 1000);
            var doc = Doc(OneCDocumentTypes.BankOut, "СписаниеСРасчетногоСчета", d, 18, 0);
            Entry(doc, fee, Side("9430", CostSub("bank")), Side("5110", Bank("UZS", "other")), "Комиссия банка");
            Finish(doc, fee, fee, cashFlow: CashFlow("other").Id, bank: BankAccount(org.Index, "UZS").Id, purpose: "Услуги банка");
        }
    }

    private readonly HashSet<(int, int)> _rentMonths = [];

    private void Financing(DateOnly d)
    {
        var bank = new[] { new OneCSubcontoDto(1, SubCounterparty, MockCatalog.Bank.Id, MockCatalog.Bank.Code, MockCatalog.Bank.Name) };
        if (d.Day == 26 && _loan > 0)
        {
            // Кредит: проценты 1,8 % в месяц от остатка и погашение 300 млн.
            var interest = Math.Round(_loan * 0.018m, 2);
            var repay = Math.Min(_loan, 300_000_000m);
            var doc = Doc(OneCDocumentTypes.BankOut, "СписаниеСРасчетногоСчета", d, 10, 40, MockCatalog.Bank);
            Entry(doc, interest, Side("9610"), Side("5110", Bank("UZS", "other")), "Проценты по кредиту");
            Entry(doc, repay, Side("6810", bank), Side("5110", Bank("UZS", "other")), "Погашение кредита");
            _loan -= repay;
            Finish(doc, interest + repay, interest + repay, cashFlow: CashFlow("other").Id, bank: BankAccount(org.Index, "UZS").Id, purpose: "Погашение кредита и процентов");
        }

        if (d.Day == 12 && _dividendsDue > 0)
        {
            var doc = Doc(OneCDocumentTypes.BankOut, "СписаниеСРасчетногоСчета", d, 11, 10);
            Entry(doc, _dividendsDue, Side("6610"), Side("5110", Bank("UZS", "other")), "Выплата дивидендов");
            Finish(doc, _dividendsDue, _dividendsDue, cashFlow: CashFlow("other").Id, bank: BankAccount(org.Index, "UZS").Id, purpose: "Дивиденды участникам");
            _dividendsDue = 0;
        }
    }

    private void Payroll(DateOnly d)
    {
        if (d.Day is 5 or 20 && _payrollDue > 0)
        {
            var amount = Math.Round(_payrollDue / (d.Day == 5 ? 2 : 1), 2);
            _payrollDue -= amount;
            var doc = Doc(OneCDocumentTypes.BankOut, "СписаниеСРасчетногоСчета", d, 11, 0);
            Entry(doc, amount, Side("6710"), Side("5110", Bank("UZS", "payroll")), "Перечисление заработной платы");
            Finish(doc, amount, amount, cashFlow: CashFlow("payroll").Id, bank: BankAccount(org.Index, "UZS").Id, purpose: "Заработная плата");
        }

        if (d.Day == 20 && (_vatDue > 0 || _taxDue > 0 || _esnDue > 0))
        {
            var doc = Doc(OneCDocumentTypes.BankOut, "СписаниеСРасчетногоСчета", d, 11, 30);
            Entry(doc, _vatDue + _taxDue, Side("6410"), Side("5110", Bank("UZS", "taxes")), "Уплата НДС и налога на прибыль");
            Entry(doc, _esnDue, Side("6520"), Side("5110", Bank("UZS", "taxes")), "Уплата социального налога");
            var total = _vatDue + _taxDue + _esnDue;
            (_vatDue, _taxDue, _esnDue) = (0, 0, 0);
            Finish(doc, total, total, cashFlow: CashFlow("taxes").Id, bank: BankAccount(org.Index, "UZS").Id, purpose: "Налоги и сборы");
        }
    }

    // ---------- Конец месяца ----------

    private void MonthEnd(DateOnly d)
    {
        var months = (d.Year - set.Start.Year) * 12 + d.Month - set.Start.Month;
        var basePayroll = (org.Index == 0 ? 950_000_000m : 360_000_000m) * (1 + months * 0.004m);
        var payroll = Doc(OneCDocumentTypes.Manual, "НачислениеЗарплаты", d, 20, 0);
        var parts = new[] { ("2010", 0.6m, (IEnumerable<OneCSubcontoDto>)Dept("prod")), ("9410", 0.2m, CostSub("payroll")), ("9420", 0.2m, CostSub("payroll")) };
        decimal production = 0;
        foreach (var (account, share, sub) in parts)
        {
            var amount = Math.Round(basePayroll * share, 2);
            var esn = Math.Round(amount * 0.12m, 2);
            Entry(payroll, amount, Side(account, sub), Side("6710"), "Начисление заработной платы");
            Entry(payroll, esn, Side(account, sub), Side("6520"), "Социальный налог");
            _esnDue += esn;
            if (account == "2010")
            {
                production = amount + esn;
            }
        }

        // Налог на доходы физлиц (12 %) удерживается из зарплаты и уплачивается вместе с налогами 20-го.
        var ndfl = Math.Round(basePayroll * 0.12m, 2);
        Entry(payroll, ndfl, Side("6710"), Side("6410"), "Налог на доходы физических лиц");
        _taxDue += ndfl;
        _payrollDue += basePayroll - ndfl;
        Finish(payroll, basePayroll, basePayroll, purpose: $"Заработная плата за {d:MM.yyyy}");

        // Расчёт себестоимости при закрытии месяца: затраты цеха (зарплата и ЕСП) — в себестоимость проданной за месяц продукции.
        var absorb = Doc(OneCDocumentTypes.Manual, "ЗакрытиеМесяца", d, 21, 0);
        var sold = _monthSold.Sum();
        for (var i = 0; i < Products.Length; i++)
        {
            var part = sold == 0 ? (i == 0 ? production : 0) : Math.Round(production * _monthSold[i] / sold, 2);
            Entry(absorb, part, Side("9110", Item(Products[i])), Side("2010", Dept("prod")), "Расчёт себестоимости: затраты производства");
            _monthSold[i] = 0;
        }

        Finish(absorb, production, production, purpose: "Распределение затрат производства");

        // Переоценка валютного счёта по курсу на конец месяца.
        var rate = set.Rate("USD", d);
        var book = Math.Round(_usd * rate, 2);
        var diff = book - _usdBook;
        if (Math.Abs(diff) >= 1)
        {
            var reval = Doc(OneCDocumentTypes.Manual, "ПереоценкаВалютныхСредств", d, 21, 30);
            if (diff > 0)
            {
                Entry(reval, diff, Side("5210", Bank("USD", "other"), "USD", 0), Side("9540"), "Курсовая разница (переоценка)", rate);
            }
            else
            {
                Entry(reval, -diff, Side("9620"), Side("5210", Bank("USD", "other"), "USD", 0), "Курсовая разница (переоценка)", rate);
            }

            _usdBook = book;
            Finish(reval, Math.Abs(diff), Math.Abs(diff), rate: rate, purpose: "Переоценка валютных средств");
        }

        // Зачёт НДС: входящий НДС (4410) уменьшает начисленный (6410); к уплате — разница, остаток входящего переходит дальше.
        var offset = Math.Min(_vatIn + _vatCarry, _vatOut);
        if (offset > 0)
        {
            var vat = Doc(OneCDocumentTypes.Manual, "ЗачетНДС", d, 22, 0);
            Entry(vat, offset, Side("6410"), Side("4410"), "Зачёт НДС");
            Finish(vat, offset, offset, purpose: $"Зачёт НДС за {d:MM.yyyy}");
        }

        _vatDue += Math.Max(0, _vatOut - offset);
        _vatCarry = _vatIn + _vatCarry - offset;
        (_vatOut, _vatIn) = (0, 0);

        var profit = -_pnl.Values.Sum(v => v.Net);
        var closing = Doc(OneCDocumentTypes.Manual, "ЗакрытиеМесяца", d, 23, 0);
        if (profit > 0)
        {
            var tax = Math.Round(profit * 0.15m, 2);
            Entry(closing, tax, Side("9810"), Side("6410"), "Налог на прибыль");
            _taxDue += tax;
        }

        // Закрытие доходов и расходов на конечный финансовый результат: отчёты OneBase такие проводки не считают.
        foreach (var (key, (sub, net)) in _pnl.ToList())
        {
            var account = key.Split('|')[0];
            var subconto = sub is null ? null : new[] { sub };
            if (net > 0)
            {
                Entry(closing, net, Side("9910"), Side(account, subconto), "Закрытие счетов доходов и расходов");
            }
            else if (net < 0)
            {
                Entry(closing, -net, Side(account, subconto), Side("9910"), "Закрытие счетов доходов и расходов");
            }
        }

        _pnl.Clear();
        Finish(closing, 0, 0, purpose: $"Закрытие месяца {d:MM.yyyy}");

        // Дивиденды раз в квартал — 30 % прибыли квартала после налога.
        _quarterProfit += profit > 0 ? profit * 0.85m : profit;
        if (d.Month % 3 == 0 && _quarterProfit > 0)
        {
            var amount = Math.Round(_quarterProfit * 0.3m, 2);
            var dividends = Doc(OneCDocumentTypes.Manual, "ОперацияБух", d, 23, 30);
            Entry(dividends, amount, Side("8710"), Side("6610"), "Начисление дивидендов за квартал");
            Finish(dividends, amount, amount, purpose: "Решение участников о дивидендах");
            _dividendsDue += amount;
            _quarterProfit = 0;
        }
    }
}
