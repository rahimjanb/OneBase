using OneBase.Domain.Common;

namespace OneBase.Domain.Sales;

/// <summary>
/// Значение P&L дилера за месяц: одна исходная строка отчёта (продажи, себестоимость, расходы по группам, доставка…), сум.
/// Итоги (валовая прибыль, маржа, EBIT…) не хранятся — их считает OneBase (PnlMath). Данные загружаются через AI Data Manager;
/// в Linko их нет, синхронизация их не трогает.
/// </summary>
public class SalesPnlValue : Entity
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>Контрагент (дилер, сеть, прямой клиент) — как в отчёте.</summary>
    public required string Dealer { get; set; }

    /// <summary>Имя для сравнения: нижний регистр, без лишних пробелов и кавычек. Ключ строки вместе с годом, месяцем и строкой P&L.</summary>
    public required string DealerKey { get; set; }

    /// <summary>Строка P&L: sales, cost, sm, retro, ship_plant, ship_dealer, ship_606, admin, other, amort, bank, tax, div.</summary>
    public required string Line { get; set; }

    public decimal Amount { get; set; }
}
