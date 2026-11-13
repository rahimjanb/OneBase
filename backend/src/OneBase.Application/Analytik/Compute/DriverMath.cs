namespace OneBase.Application.Analytik.Compute;

/// <summary>Вклад участника в изменение: было, стало, изменение, доля в чистом изменении (null — чистое изменение около нуля), доля в сумме |изменений|, вклад в рост итога (п.п.).</summary>
public sealed record DriverItem(string Key, double A, double B, double Delta, double? ChangePct, double? ShareOfNet, double ShareOfGross, double? ContributionPp);

/// <summary>
/// Разложение изменения итога по участникам: итоги, изменение, участники по убыванию |изменения|, кто тянет вниз и вверх, концентрация
/// (доля |изменений| у трёх крупнейших).
/// </summary>
public sealed record DriverResult(double TotalA, double TotalB, double Delta, double? ChangePct, IReadOnlyList<DriverItem> Items, double Top3ShareOfGross);

/// <summary>
/// Разложение изменения выручки «цена — объём — ассортимент» (PVM) для позиций, продававшихся в оба периода, плюс новые и выбывшие позиции.
/// Сумма эффектов в точности равна изменению выручки.
/// </summary>
public sealed record PvmResult(double RevenueA, double RevenueB, double Delta, double Volume, double Mix, double Price, double NewItems, double LostItems,
    int Common, int New, int Lost, IReadOnlyList<PvmItem> Items);

/// <summary>Позиция в PVM: объём и выручка в оба периода, цена, эффект цены позиции.</summary>
public sealed record PvmItem(string Key, double QtyA, double QtyB, double? PriceA, double? PriceB, double PriceEffect, string Status);

/// <summary>Driver / Contribution Analysis: кто дал изменение показателя и за счёт чего (цена, объём, ассортимент).</summary>
public static class DriverMath
{
    public static DriverResult Contribution(IReadOnlyList<(string Key, double A, double B)> members)
    {
        StatMath.Require(members.Count > 0, "Нет участников для разложения изменения.");
        var totalA = members.Sum(m => m.A);
        var totalB = members.Sum(m => m.B);
        var delta = totalB - totalA;
        var gross = members.Sum(m => Math.Abs(m.B - m.A));
        var netDefined = Math.Abs(delta) > 1e-9 * Math.Max(1, gross) && Math.Abs(delta) >= 0.01 * gross;
        var items = members.Select(m =>
            {
                var d = m.B - m.A;
                return new DriverItem(m.Key, m.A, m.B, d, m.A != 0 ? d / Math.Abs(m.A) * 100 : null, netDefined ? d / delta : null,
                    gross > 0 ? Math.Abs(d) / gross : 0, totalA != 0 ? d / Math.Abs(totalA) * 100 : null);
            })
            .OrderByDescending(i => Math.Abs(i.Delta)).ThenBy(i => i.Key, StringComparer.Ordinal).ToList();
        var top3 = items.Take(3).Sum(i => i.ShareOfGross);
        return new DriverResult(totalA, totalB, delta, totalA != 0 ? delta / Math.Abs(totalA) * 100 : null, items, top3);
    }

    /// <summary>
    /// PVM по позициям с объёмом в оба периода (C): объём = (V_B − V_A)·P̄_A; ассортимент = Σ q_B·p_A − V_B·P̄_A; цена = Σ q_B·(p_B − p_A);
    /// плюс выручка новых позиций (есть только в B) и минус выручка выбывших (только в A). P̄_A — средняя цена A по общим позициям.
    /// </summary>
    public static PvmResult PriceVolumeMix(IReadOnlyList<(string Key, double QtyA, double RevA, double QtyB, double RevB)> items)
    {
        StatMath.Require(items.Count > 0, "Нет позиций для разложения выручки.");
        var common = items.Where(i => i.QtyA > 0 && i.QtyB > 0).ToList();
        var newItems = items.Where(i => i.QtyA <= 0 && i.QtyB > 0).ToList();
        var lost = items.Where(i => i.QtyA > 0 && i.QtyB <= 0).ToList();
        // Позиции без объёма в обоих периодах (только возвраты или скидки) — их выручка идёт в «новые / выбывшие» по знаку периода.
        var neither = items.Where(i => i.QtyA <= 0 && i.QtyB <= 0).ToList();
        var revA = items.Sum(i => i.RevA);
        var revB = items.Sum(i => i.RevB);

        double volume = 0, mix = 0, price = 0;
        if (common.Count > 0)
        {
            var vA = common.Sum(i => i.QtyA);
            var vB = common.Sum(i => i.QtyB);
            var rA = common.Sum(i => i.RevA);
            var avgA = rA / vA;
            volume = (vB - vA) * avgA;
            mix = common.Sum(i => i.QtyB * (i.RevA / i.QtyA)) - vB * avgA;
            price = common.Sum(i => i.QtyB * (i.RevB / i.QtyB - i.RevA / i.QtyA));
        }

        var newEffect = newItems.Sum(i => i.RevB) - newItems.Sum(i => i.RevA) + neither.Sum(i => i.RevB);
        var lostEffect = -(lost.Sum(i => i.RevA) - lost.Sum(i => i.RevB)) - neither.Sum(i => i.RevA);
        var rows = items.Select(i =>
        {
            var status = i.QtyA > 0 && i.QtyB > 0 ? "common" : i.QtyA <= 0 && i.QtyB > 0 ? "new" : i.QtyB <= 0 && i.QtyA > 0 ? "lost" : "none";
            double? pa = i.QtyA > 0 ? i.RevA / i.QtyA : null;
            double? pb = i.QtyB > 0 ? i.RevB / i.QtyB : null;
            return new PvmItem(i.Key, i.QtyA, i.QtyB, pa, pb, status == "common" ? i.QtyB * (pb!.Value - pa!.Value) : 0, status);
        }).OrderByDescending(r => Math.Abs(r.PriceEffect)).ThenBy(r => r.Key, StringComparer.Ordinal).ToList();
        return new PvmResult(revA, revB, revB - revA, volume, mix, price, newEffect, lostEffect, common.Count, newItems.Count, lost.Count, rows);
    }
}
