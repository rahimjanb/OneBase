namespace OneBase.Domain.Sales;

/// <summary>
/// Строка реестра взаиморасчётов с дилером из Google Sheets (её ведёт таблица — копия листа, как копия Linko): оплата дилера
/// (Field = dealer_payments, Kind — кошелёк: «Нақд», банк) или расход по статье (Field = dealer_expenses, Kind — статья «601-Дилер бонуслари»).
/// Синхронизация привязки заменяет строки поля целиком. Для «Акта сверки» и DSO; отгрузки и возвраты — из Linko (первичка).
/// </summary>
public class SalesDealerLedgerEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Поле источника данных: dealer_payments или dealer_expenses.</summary>
    public required string Field { get; set; }

    public Guid? BindingId { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>Контрагент как в таблице («Азизбек Хоразм») и ключ сопоставления (без регистра и лишних пробелов).</summary>
    public required string Dealer { get; set; }

    public required string DealerKey { get; set; }

    /// <summary>Сумма, сум: приход минус расход (возврат денег дилеру — со знаком минус).</summary>
    public decimal Amount { get; set; }

    /// <summary>Кошелёк оплаты или статья расхода.</summary>
    public string? Kind { get; set; }

    /// <summary>Категория строки в таблице («101-Махаллий сотувдан тушум»).</summary>
    public string? Category { get; set; }

    public string? Comment { get; set; }

    /// <summary>Ячейка таблицы: «Kassa Bank!B12».</summary>
    public string? SourceRef { get; set; }
}

/// <summary>Условия дилера из Google Sheets (лист «Ostatka»): срок оплаты в днях и остаток долга на дату — начало FIFO и акта.</summary>
public class SalesDealerTerm
{
    public required string DealerKey { get; set; }
    public required string Dealer { get; set; }

    /// <summary>Срок оплаты, дней; null — договор без срока.</summary>
    public int? NormDays { get; set; }

    /// <summary>Долг дилера на OpeningDate (до первой операции реестра).</summary>
    public decimal? Opening { get; set; }

    public DateOnly? OpeningDate { get; set; }
    public Guid? BindingId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
