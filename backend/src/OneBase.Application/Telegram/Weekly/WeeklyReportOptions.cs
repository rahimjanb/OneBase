using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Weekly;

/// <summary>
/// Еженедельный отчёт (раздел WeeklyReport в appsettings). Значения по умолчанию — формулы целей из образцов «Полевого контроля», утверждены
/// 31.10.2026: АКБ × 1,1; ТТ по акциям × 1,2 (не меньше 25 на ТП); вернуть 30% спящих 15–30 дней и 20% спящих 30+.
/// </summary>
public sealed class WeeklyReportOptions
{
    /// <summary>Цель АКБ недели = уникальные ТТ с покупкой за прошлую полную неделю × коэффициент.</summary>
    public decimal GoalAkbFactor { get; set; } = 1.1m;

    /// <summary>Цель ТТ по акциям = уникальные ТТ с заказом по акции за прошлую полную неделю × коэффициент, не меньше минимума.</summary>
    public decimal GoalPromoFactor { get; set; } = 1.2m;

    public int GoalPromoMin { get; set; } = 25;

    /// <summary>Доля спящих, которых нужно вернуть: 15–30 дней и 30+.</summary>
    public decimal ReturnShare15To30 { get; set; } = 0.3m;

    public decimal ReturnShare30Plus { get; set; } = 0.2m;

    /// <summary>Сколько ТТ показать в списках «верни» у ТП (крупные первыми).</summary>
    public int ReturnList15To30 { get; set; } = 4;

    public int ReturnList30Plus { get; set; } = 3;

    /// <summary>Спящие: последняя покупка от SleepFromDays дней назад (и до SleepDays — «15–30»).</summary>
    public int SleepFromDays { get; set; } = 15;

    public int SleepDays { get; set; } = 30;

    /// <summary>Топ спящих ТТ области по объёму.</summary>
    public int SleepingTop { get; set; } = 12;

    /// <summary>Дефицит у дилера: товара хватит меньше чем на столько дней продаж.</summary>
    public decimal DeficitDays { get; set; } = 7;

    /// <summary>Строк дефицита и приоритетной отгрузки на дилера.</summary>
    public int DealerRows { get; set; } = 12;

    /// <summary>КПУ: до порога меньше этого (п.п.) — «реально добрать», иначе — «далеко» (так размечены образцы).</summary>
    public decimal KpiRealisticGapPp { get; set; } = 10;

    /// <summary>Порог плана для «нужного темпа» (порог КПУ по объёму).</summary>
    public decimal PaceThreshold { get; set; } = 0.8m;

    /// <summary>Значения с настройками раздела «Отчёты PDF» поверх: пустая настройка — значение appsettings.</summary>
    public WeeklyReportOptions With(TelegramWeeklyPdfSettings? s)
    {
        var o = (WeeklyReportOptions)MemberwiseClone();
        if (s is null)
        {
            return o;
        }

        o.GoalAkbFactor = s.GoalAkbFactor ?? GoalAkbFactor;
        o.GoalPromoFactor = s.GoalPromoFactor ?? GoalPromoFactor;
        o.GoalPromoMin = s.GoalPromoMin ?? GoalPromoMin;
        o.ReturnShare15To30 = s.ReturnShare15To30 ?? ReturnShare15To30;
        o.ReturnShare30Plus = s.ReturnShare30Plus ?? ReturnShare30Plus;
        o.SleepFromDays = s.SleepFromDays ?? SleepFromDays;
        o.SleepDays = s.SleepDays ?? SleepDays;
        o.DeficitDays = s.DeficitDays ?? DeficitDays;
        return o;
    }
}
