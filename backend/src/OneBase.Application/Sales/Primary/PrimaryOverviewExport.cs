using System.Globalization;
using System.Text.Json;
using OneBase.Application.Abstractions;
using OneBase.Application.Reports;

namespace OneBase.Application.Sales.Primary;

/// <summary>
/// «Excel» на обзоре «Первички»: лист «Направления» (республика, экспорт и итог: тонны, сумма, доли, контрагенты или страны, документы, сумма
/// за тонну) и «По месяцам» — помесячно за выбранный период. Те же цифры, что на странице.
/// </summary>
public static class PrimaryOverviewExport
{
    public static AiReportContent Build(PrimaryOverview view)
    {
        var uz = UiLanguage.IsUzbek;
        string L(string ru, string latin) => uz ? UiLanguage.Uzbek(latin) : ru;
        var label = PrimaryOverviewMath.Label(view.Period);

        var directions = new AiReportSheet(
            L("Направления", "Yo'nalishlar"),
            [
                new AiReportColumn(L("Направление", "Yo'nalish"), ReportFormats.Text),
                new AiReportColumn(L("Тонны", "Tonna"), ReportFormats.Number),
                new AiReportColumn(L("Сумма, сум", "Summa, so'm"), ReportFormats.Money),
                new AiReportColumn(L("Доля по тоннам", "Tonna bo'yicha ulush"), ReportFormats.Percent),
                new AiReportColumn(L("Доля по сумме", "Summa bo'yicha ulush"), ReportFormats.Percent),
                new AiReportColumn(L("Контрагенты / страны", "Kontragentlar / mamlakatlar"), ReportFormats.Integer, Sum: false),
                new AiReportColumn(L("Документы / заказы", "Hujjatlar / buyurtmalar"), ReportFormats.Integer),
                new AiReportColumn(L("Сумма за тонну, сум", "Tonna uchun summa, so'm"), ReportFormats.Money, Sum: false),
            ],
            view.Directions.Select(d => (IReadOnlyList<JsonElement>)
            [
                Json(d.Id == "republic" ? L("Республика", "Respublika") : L("Экспорт", "Eksport")),
                Json(Math.Round(d.Tons, 3)),
                Json(d.Amount),
                Json(d.ShareTons is { } st ? Math.Round(st * 100, 1) : null),
                Json(d.ShareAmount is { } sa ? Math.Round(sa * 100, 1) : null),
                Json(d.Counterparties ?? d.Countries),
                Json(d.Orders),
                Json(d.AvgPricePerTon),
            ]).ToList(),
            Total: true,
            Note: L(
                "Республика — перемещения Linko со склада завода на склады дилеров и заказы прямых клиентов завода (базары, сети, фирменный магазин), нетто возвратов; контрагенты — дилеры и прямые клиенты. Экспорт — заказы филиала «Завод» торговым точкам с типом EXPORT, нетто возвратов; страна — по названию или адресу точки; заказы в другой валюте дают вес без суммы.",
                "Respublika — Linko'da zavod omboridan diler omborlariga ko'chirishlar va zavodning to'g'ridan-to'g'ri mijozlari buyurtmalari (bozorlar, tarmoqlar, firma do'koni), qaytarishlarsiz. Eksport — «Zavod» filialining EXPORT turidagi savdo nuqtalariga buyurtmalari, qaytarishlarsiz; mamlakat — nuqta nomi yoki manzili bo'yicha; boshqa valyutadagi buyurtmalar vazni summasiz."));

        var months = new AiReportSheet(
            L("По месяцам", "Oylar bo'yicha"),
            [
                new AiReportColumn(L("Месяц", "Oy"), ReportFormats.Text),
                new AiReportColumn(L("Республика, т", "Respublika, t"), ReportFormats.Number),
                new AiReportColumn(L("Республика, сум", "Respublika, so'm"), ReportFormats.Money),
                new AiReportColumn(L("Экспорт, т", "Eksport, t"), ReportFormats.Number),
                new AiReportColumn(L("Экспорт, сум", "Eksport, so'm"), ReportFormats.Money),
                new AiReportColumn(L("Всего, т", "Jami, t"), ReportFormats.Number),
                new AiReportColumn(L("Всего, сум", "Jami, so'm"), ReportFormats.Money),
            ],
            view.Months.Select(m => (IReadOnlyList<JsonElement>)
            [
                Json(new DateOnly(m.Year, m.Month, 1).ToString("MM.yyyy", CultureInfo.InvariantCulture)),
                Json(Math.Round(m.RepublicTons, 3)),
                Json(m.RepublicAmount),
                Json(Math.Round(m.ExportTons, 3)),
                Json(m.ExportAmount),
                Json(Math.Round(m.RepublicTons + m.ExportTons, 3)),
                Json(m.RepublicAmount + m.ExportAmount),
            ]).ToList(),
            Total: true);

        return new AiReportContent(L($"Первичка — {label}", $"Birlamchi — {label}"), [directions, months],
            [L($"первичка OneBase (Linko), данные по {view.DataThrough:dd.MM.yyyy}", $"OneBase birlamchi (Linko), {view.DataThrough:dd.MM.yyyy} gacha ma'lumotlar")]);
    }

    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value);
}
