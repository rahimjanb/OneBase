using System.Text.RegularExpressions;
using OneBase.Application.DataManager;

namespace OneBase.Application.Sales.Pnl;

/// <summary>Вид строки P&L: деньги (сум), процент или показатель, для которого в OneBase нет данных («—»).</summary>
public enum PnlFormat
{
    Money,
    Percent,
    NotAvailable,
}

/// <param name="Minus">Вычитается из прибыли: печатается с отступом, значение хранится положительным.</param>
/// <param name="Detail">Расшифровка («в т. ч.»): в EBIT повторно не входит.</param>
/// <param name="Input">Исходная строка — загружается из отчёта; остальные OneBase считает по формулам.</param>
public sealed record PnlLine(string Key, string Label, PnlFormat Format, bool Minus, bool Detail, bool Input, IReadOnlyList<string> Aliases);

/// <summary>
/// P&L дилеров (как во вкладке «P&L дилеров» Полевого контроля): прибыль завода на дилере за месяц — выручка от отгрузок дилеру
/// минус расходы завода, отнесённые на дилера. Исходные строки загружаются через AI Data Manager; итоги считаются здесь и только здесь.
/// <list type="bullet">
/// <item>Валовая прибыль = Продажи − Себестоимость; Маржа = Валовая ÷ Продажи;</item>
/// <item>Прибыль после продаж = Валовая − Отдел продаж и маркетинга + Ретробонусы; ROS = Прибыль после продаж ÷ Продажи;</item>
/// <item>Доставка и склад = завод оплатил + дилер оплатил (журнал логистики); если рейсов дилера за месяц нет — компенсация по ст. 606;</item>
/// <item>EBIT = Прибыль после продаж − Доставка − Административные − Прочие − Амортизация;</item>
/// <item>EAT = EBIT − Банковские услуги − Налоги — только если за месяц загружены банковские услуги или налоги;
/// Реинвестированная = EAT − Дивиденды — только если загружены дивиденды; ROA — нет данных об активах.</item>
/// </list>
/// Итоги (группа, месяц, год) — суммы денежных строк дилеров; проценты итога — от сумм (взвешенно, не среднее процентов).
/// </summary>
public static partial class PnlMath
{
    public const string Sales = "sales";
    public const string Cost = "cost";
    public const string Gross = "gross";
    public const string Margin = "margin";
    public const string Sm = "sm";
    public const string Retro = "retro";
    public const string After = "after";
    public const string Ros = "ros";
    public const string Ship = "ship";
    public const string ShipPlant = "ship_plant";
    public const string ShipDealer = "ship_dealer";
    public const string Ship606 = "ship_606";
    public const string Admin = "admin";
    public const string Other = "other";
    public const string Amort = "amort";
    public const string Ebit = "ebit";
    public const string Roa = "roa";
    public const string Bank = "bank";
    public const string Tax = "tax";
    public const string Eat = "eat";
    public const string Div = "div";
    public const string Reinv = "reinv";

    /// <summary>Строки в порядке отображения.</summary>
    public static readonly IReadOnlyList<PnlLine> Lines =
    [
        new(Sales, "Продажи", PnlFormat.Money, false, false, true, ["продажи", "выручка", "сумма дилера", "сумма диллера", "sales", "revenue"]),
        new(Cost, "Себестоимость товара", PnlFormat.Money, true, false, true, ["себестоимость товара", "себестоимость", "сумма завода", "таннарх", "cost", "cogs"]),
        new(Gross, "Валовая прибыль", PnlFormat.Money, false, false, false, ["валовая прибыль", "валовая", "gross profit", "gross"]),
        new(Margin, "Маржа", PnlFormat.Percent, false, false, false, ["маржа", "валовая маржа", "margin"]),
        new(Sm, "Минус Отдел продаж и маркетинга", PnlFormat.Money, true, false, true,
            ["отдел продаж и маркетинга", "расходы отдела продаж и маркетинга", "продажи и маркетинг", "sales and marketing"]),
        new(Retro, "Плюс Ретробонусы от поставщиков", PnlFormat.Money, false, false, true, ["ретробонусы от поставщиков", "ретробонусы", "ретро бонусы"]),
        new(After, "Прибыль после продаж", PnlFormat.Money, false, false, false, ["прибыль после продаж"]),
        new(Ros, "ROS рентабельность продаж", PnlFormat.Percent, false, false, false, ["ros", "рентабельность продаж"]),
        new(Ship, "Доставка и склад", PnlFormat.Money, true, false, false, ["доставка и склад", "доставка", "логистика и склад"]),
        new(ShipPlant, "в т. ч. завод оплатил (журнал логистики)", PnlFormat.Money, false, true, true,
            ["завод оплатил", "завод тулади", "оплатил завод", "доставка завод"]),
        new(ShipDealer, "в т. ч. дилер оплатил (журнал логистики)", PnlFormat.Money, false, true, true,
            ["дилер оплатил", "дилер тулади", "диллер тулади", "оплатил дилер", "доставка дилер"]),
        new(Ship606, "справочно: компенсировано дилеру по ст. 606", PnlFormat.Money, false, true, true,
            ["компенсировано дилеру по ст 606", "компенсировано дилеру", "статья 606", "ст 606", "606"]),
        new(Admin, "Административные расходы", PnlFormat.Money, true, false, true, ["административные расходы", "административные", "admin"]),
        new(Other, "Прочие расходы", PnlFormat.Money, true, false, true, ["прочие расходы", "прочие", "other"]),
        new(Amort, "Амортизация", PnlFormat.Money, true, false, true, ["амортизация", "depreciation"]),
        new(Ebit, "EBIT операционная прибыль", PnlFormat.Money, false, false, false, ["ebit", "операционная прибыль"]),
        new(Roa, "ROA рентабельность активов", PnlFormat.NotAvailable, false, false, false, ["roa", "рентабельность активов"]),
        new(Bank, "Банковские услуги", PnlFormat.Money, true, false, true, ["банковские услуги", "банк"]),
        new(Tax, "Налоги", PnlFormat.Money, true, false, true, ["налоги", "налог"]),
        new(Eat, "EAT чистая прибыль", PnlFormat.Money, false, false, false, ["eat", "чистая прибыль"]),
        new(Div, "Выплата дивидендов", PnlFormat.Money, true, false, true, ["выплата дивидендов", "дивиденды"]),
        new(Reinv, "Реинвестированная прибыль", PnlFormat.Money, false, false, false, ["реинвестированная прибыль"]),
    ];

    private static readonly Dictionary<string, PnlLine> ByKey = Lines.ToDictionary(l => l.Key);

    public static readonly IReadOnlyList<string> Inputs = Lines.Where(l => l.Input).Select(l => l.Key).ToList();

    /// <summary>Строки расходов: месяц без них помечается «без расходов».</summary>
    public static readonly IReadOnlySet<string> Expenses = new HashSet<string> { Sm, ShipPlant, ShipDealer, Ship606, Admin, Other, Amort };

    public static PnlLine? Line(string key) => ByKey.GetValueOrDefault(key);

    public static bool IsInput(string key) => ByKey.TryGetValue(key, out var line) && line.Input;

    /// <summary>
    /// Статьи реестра «Прочие расходы» → строки P&L (BUCKET Полевого контроля): 601–604, 607–610 — отдел продаж и маркетинга,
    /// 606 — логистика (справочно и фолбэк доставки), 611, 331, 334, 339 — административные; остальные статьи — прочие расходы.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Articles = new Dictionary<string, string>
    {
        ["601"] = Sm, ["602"] = Sm, ["603"] = Sm, ["604"] = Sm, ["607"] = Sm, ["608"] = Sm, ["609"] = Sm, ["610"] = Sm,
        ["606"] = Ship606,
        ["611"] = Admin, ["331"] = Admin, ["334"] = Admin, ["339"] = Admin,
    };

    /// <summary>
    /// Строка P&L по тексту отчёта: ключ строки («sales»), код статьи реестра («601-Дилер бонуслари» → sm, неизвестная статья → other)
    /// или подпись («Себестоимость товара», «Минус Отдел продаж…»). null — не распознано.
    /// </summary>
    public static string? FindLine(string? text)
    {
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return null;
        }

        if (ByKey.ContainsKey(raw.ToLowerInvariant()))
        {
            return raw.ToLowerInvariant();
        }

        var article = ArticleCode().Match(raw);
        if (article.Success)
        {
            return Articles.GetValueOrDefault(article.Groups[1].Value, Other);
        }

        var name = Prefix().Replace(Words(raw), "").Trim();
        if (name.Length == 0)
        {
            return null;
        }

        // самое длинное совпадение: «прибыль после продаж» раньше «продажи»
        return Lines
            .SelectMany(l => l.Aliases.Append(l.Label).Select(a => (l.Key, Alias: Words(a))))
            .Where(a => name == a.Alias || name.StartsWith(a.Alias + " ", StringComparison.Ordinal))
            .OrderByDescending(a => a.Alias.Length)
            .Select(a => a.Key)
            .FirstOrDefault();
    }

    /// <summary>Код статьи реестра в начале текста («601-Дилер бонуслари» → «601»), иначе null.</summary>
    public static string? ArticleOf(string? text) => ArticleCode().Match(text ?? string.Empty) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Текст для сравнения подписей: нижний регистр, только буквы, цифры и одиночные пробелы.</summary>
    private static string Words(string text) =>
        string.Join(' ', NonWord().Replace(DataValues.Normalize(text), " ").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Ключ контрагента: регистр, ё, кавычки и пробелы не различаются.</summary>
    public static string DealerKey(string dealer) => DataValues.Normalize(dealer);

    /// <summary>Имя контрагента для показа: без лишних пробелов.</summary>
    public static string DealerName(string dealer) => string.Join(' ', dealer.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Все строки P&L одного дилера за месяц из исходных значений. scope — исходные строки, загруженные за месяц хотя бы у одного дилера:
    /// EAT считается, только если в месяце есть банковские услуги или налоги, реинвестированная — если есть дивиденды.
    /// </summary>
    public static Dictionary<string, decimal?> Compute(IReadOnlyDictionary<string, decimal> inputs, IReadOnlySet<string> scope)
    {
        bool Has(string k) => inputs.ContainsKey(k);
        decimal V(string k) => inputs.GetValueOrDefault(k);

        var r = Lines.ToDictionary(l => l.Key, l => l.Input && Has(l.Key) ? V(l.Key) : (decimal?)null);
        var any = inputs.Count > 0;
        if (!any)
        {
            return r;
        }

        var sales = V(Sales);
        var gross = sales - V(Cost);
        var after = gross - V(Sm) + V(Retro);
        decimal? ship = Has(ShipPlant) || Has(ShipDealer) ? V(ShipPlant) + V(ShipDealer) : Has(Ship606) ? V(Ship606) : null;
        var ebit = after - (ship ?? 0) - V(Admin) - V(Other) - V(Amort);

        r[Gross] = gross;
        r[Margin] = sales != 0 ? gross / sales : null;
        r[After] = after;
        r[Ros] = sales != 0 ? after / sales : null;
        r[Ship] = ship;
        r[Ebit] = ebit;
        decimal? eat = scope.Contains(Bank) || scope.Contains(Tax) ? ebit - V(Bank) - V(Tax) : null;
        r[Eat] = eat;
        r[Reinv] = eat is { } e && scope.Contains(Div) ? e - V(Div) : null;
        return r;
    }

    /// <summary>Итог нескольких дилеров или месяцев: денежные строки — суммы (null — ни у кого нет), проценты — от сумм.</summary>
    public static Dictionary<string, decimal?> Sum(IEnumerable<IReadOnlyDictionary<string, decimal?>> parts)
    {
        var list = parts.ToList();
        var r = Lines.ToDictionary(l => l.Key, l => l.Format == PnlFormat.Money
            ? list.Select(p => p.GetValueOrDefault(l.Key)).Where(v => v is not null).Aggregate((decimal?)null, (acc, v) => (acc ?? 0) + v)
            : null);
        var sales = r[Sales] ?? 0;
        r[Margin] = sales != 0 && r[Gross] is { } g ? g / sales : null;
        r[Ros] = sales != 0 && r[After] is { } a ? a / sales : null;
        return r;
    }

    /// <summary>Темп роста месяца к предыдущему: деньги — (y − x) ÷ |x|, проценты — разница (доля; ×100 — п. п.).</summary>
    public static Dictionary<string, decimal?> Growth(IReadOnlyDictionary<string, decimal?> current, IReadOnlyDictionary<string, decimal?> previous) =>
        Lines.ToDictionary(l => l.Key, l =>
        {
            var y = current.GetValueOrDefault(l.Key);
            var x = previous.GetValueOrDefault(l.Key);
            return l.Format switch
            {
                PnlFormat.Money when y is { } a && x is { } b && b != 0 => (a - b) / Math.Abs(b),
                PnlFormat.Percent when y is { } a && x is { } b => a - b,
                _ => (decimal?)null,
            };
        });

    [GeneratedRegex(@"^\s*(\d{3})\s*(?:[-–—.:]|\s|$)")]
    private static partial Regex ArticleCode();

    [GeneratedRegex(@"^(минус|плюс|в т ч|в том числе|справочно|итого)\s+")]
    private static partial Regex Prefix();

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex NonWord();
}
