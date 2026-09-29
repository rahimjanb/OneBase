namespace OneBase.Domain.Sales;

// Копия складских и справочных данных Linko (только чтение из Linko): склады, остатки, перемещения,
// а также платежи, прайс-листы, поставщики, валюты и договоры.

public class LinkoStock
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public string? Code { get; set; }
    public string? Address { get; set; }
    public decimal Tm { get; set; }
}

/// <summary>Остаток товара на складе — в ШТУКАХ (единица учёта Linko), не в килограммах и не в коробках.</summary>
public class LinkoProductBalance
{
    public long ProductId { get; set; }
    public long StockId { get; set; }
    public decimal Balance { get; set; }
    public decimal Tm { get; set; }
}

/// <summary>Перемещение между складами. Завод → склад региона — это отгрузка дилеру (первичка).</summary>
public class LinkoStockTransfer
{
    public long Id { get; set; }
    public required string Status { get; set; }
    public long? FromStockId { get; set; }
    public long? ToStockId { get; set; }

    /// <summary>Время создания — без часового пояса, как отдаёт Linko.</summary>
    public DateTime? CreatedAt { get; set; }
    public DateOnly? CreatedDate { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public DateTime? GivenAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateOnly? AcceptedDate { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal TotalPrice { get; set; }
    public long? PriceListId { get; set; }
    public long? CurrencyId { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal Tm { get; set; }

    public List<LinkoStockTransferLine> Lines { get; set; } = [];
}

public class LinkoStockTransferLine
{
    public long Id { get; set; }
    public long TransferId { get; set; }
    public long? ProductId { get; set; }
    public decimal Amount { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal TotalWeightNetto { get; set; }
    public decimal Price { get; set; }
    public decimal TotalPrice { get; set; }
}

/// <summary>Платёж (оплата от торговой точки). Удалённые платежи Linko тоже отдаёт — с IsDelete.</summary>
public class LinkoPayment
{
    public long Id { get; set; }
    public decimal Amount { get; set; }
    public long? CurrencyId { get; set; }
    public string? CurrencyName { get; set; }
    public long? MarketId { get; set; }
    public string? PaymentType { get; set; }
    public string? Type { get; set; }
    public string? Status { get; set; }
    public long? UserId { get; set; }
    public DateOnly? CreatedDate { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public long? OrderId { get; set; }
    public bool IsDelete { get; set; }
    public decimal Tm { get; set; }
}

public class LinkoPriceList
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public long? CurrencyId { get; set; }
    public string? Code { get; set; }
    public decimal Tm { get; set; }
}

public class LinkoPriceListItem
{
    public long Id { get; set; }
    public long? ProductId { get; set; }
    public long? PriceListId { get; set; }
    public decimal Price { get; set; }
    public long? CurrencyId { get; set; }
    public decimal Tm { get; set; }
}

public class LinkoProvider
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public decimal Tm { get; set; }
}

public class LinkoCurrency
{
    public long Id { get; set; }
    public required string Name { get; set; }
}

public class LinkoContract
{
    public long Id { get; set; }
    public DateOnly? Date { get; set; }
    public string? Number { get; set; }
    public string? Status { get; set; }
    public bool IsDelete { get; set; }
    public decimal Tm { get; set; }
}
