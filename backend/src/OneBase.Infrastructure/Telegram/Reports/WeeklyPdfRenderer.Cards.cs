using System.Globalization;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Telegram.Weekly;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static OneBase.Application.Telegram.Reports.ReportFormat;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Карточки ТП (образец «карточки ТП Сырдарья»): страница под телефон 9:16, по две на ТП — «итоги недели» (неделя, месяц по KPI Linko, АКБ,
/// премия КПУ, визиты и заказы, ТТ по акции, что добрать по КПУ) и «цели на неделю» (продажи, АКБ, акции, кого вернуть, ДМП, что
/// предлагать и чего не обещать). Только цифры этого ТП из модели отчёта.
/// </summary>
public sealed partial class WeeklyPdfRenderer
{
    public byte[] RenderCards(WeeklySupervisorReport report, string language)
    {
        EnsureReady();
        return new Cards(report, Lang(language)).Create().GeneratePdf();
    }

    public IReadOnlyList<byte[]> RenderCardImages(WeeklySupervisorReport report, string language, int dpi = 110)
    {
        EnsureReady();
        return new Cards(report, Lang(language)).Create().GenerateImages(new ImageGenerationSettings { RasterDpi = dpi }).ToList();
    }

    /// <summary>Порог «конверсия визит → заказ», как в рейтинге ТП.</summary>
    private const decimal ConversionGoal = 0.7m;

    private sealed class Cards(WeeklySupervisorReport r, string lang)
    {
        private static readonly NumberFormatInfo Ru = new() { NumberGroupSeparator = " ", NumberDecimalSeparator = ",", NumberGroupSizes = [3] };

        private string T(string ru) => lang != "ru" && Uz.TryGetValue(ru, out var uz) ? FromUz(lang, uz, ru) : WeeklyTexts.T(lang, ru);

        private string F(string ru, params object?[] args) => string.Format(CultureInfo.InvariantCulture, T(ru), args.Select(a => a ?? "").ToArray());

        public Document Create() => Document.Create(doc =>
        {
            if (r.EmployeeMetrics.Count == 0)
            {
                doc.Page(p => Page(p, T("Карточки ТП"), T("В области нет ТП с данными за неделю."), _ => { }));
                return;
            }

            foreach (var e in r.EmployeeMetrics)
            {
                var goal = r.WeeklyGoals.Agents.FirstOrDefault(g => g.AgentId == e.AgentId);
                doc.Page(p => Page(p, F("1 · Итоги недели {0}", Range(r.PeriodStart, r.DataThrough < r.PeriodEnd ? r.DataThrough : r.PeriodEnd)), e.Name, c => Week(c, e)));
                doc.Page(p => Page(p, F("2 · Цели на неделю {0}", Range(r.WeeklyGoals.WeekFrom, r.WeeklyGoals.WeekTo)), e.Name, c => Goals(c, goal)));
            }
        });

        private static string Range(DateOnly from, DateOnly to) => $"{from:dd.MM}–{to:dd.MM.yyyy}";

        private void Page(PageDescriptor page, string kicker, string name, Action<IContainer> content)
        {
            page.Size(420, 746);
            page.Margin(22);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily("Inter").FontSize(10).FontColor(C.Ink));
            page.Header().Column(col =>
            {
                col.Item().Text(kicker.ToUpperInvariant()).FontSize(8).FontColor(C.Ink2).LetterSpacing(0.08f);
                col.Item().PaddingTop(2).Text(name).FontSize(19).Bold().FontColor(C.Navy);
                var who = string.Join(" · ", new[] { r.RegionName, r.SupervisorName is { Length: > 0 } sv ? F("СВР {0}", sv) : null }.Where(x => !string.IsNullOrWhiteSpace(x)));
                if (who.Length > 0)
                {
                    col.Item().Text(who).FontSize(10).FontColor(C.Ink2);
                }

                col.Item().PaddingTop(6).PaddingBottom(10).LineHorizontal(2).LineColor(C.Navy);
            });
            page.Content().Element(content);
            page.Footer().Row(row =>
            {
                row.RelativeItem().Text(F("Данные на {0} · сформирован {1}", r.DataThrough.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    r.GeneratedAt.ToOffset(TimeSpan.FromHours(5)).ToString("dd.MM HH:mm", CultureInfo.InvariantCulture))).FontSize(7).FontColor(C.Ink3);
                row.AutoItem().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7).FontColor(C.Ink3));
                    t.CurrentPageNumber();
                    t.Span("/");
                    t.TotalPages();
                });
            });
        }

        // ---------- 1. Итоги недели ----------

        private void Week(IContainer c, WeeklyEmployeeMetrics e)
        {
            var kpu = r.KpiMetrics.People.FirstOrDefault(p => p.Id == e.AgentId);
            var promo = r.PromotionMetrics.Agents.FirstOrDefault(a => a.AgentId == e.AgentId);
            c.Column(col =>
            {
                col.Spacing(8);
                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(x => Tile(x, T("Неделя, кг"), Number(e.WeekKg), e.WeekVsPrev is { } d ? (d >= 0 ? Tone.Good : Tone.Bad) : Tone.None, t =>
                    {
                        t.Span(T("к пр. неделе") + " ");
                        t.Span(WeeklyTexts.Delta(e.WeekVsPrev)).FontColor(ToneColor(e.WeekVsPrev is { } dd ? (dd >= 0 ? Tone.Good : Tone.Bad) : Tone.None)).SemiBold();
                    }));
                    row.RelativeItem().Element(x => Tile(x, T("Месяц, % плана"), Percent(e.Execution), PlanTone(e.Execution), t => t.Span(e.LinkoPlan is { } plan
                        ? F("{0} из {1} {2}", Amount(e.LinkoFact, e.LinkoUnit), Amount(plan, e.LinkoUnit), Unit(e.LinkoUnit))
                        : T("плана в Linko нет"))));
                });
                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(x => Tile(x, T("АКБ недели"), Number(e.WeekAkb), e.AkbPlan is > 0 && e.MonthAkb / e.AkbPlan.Value >= 0.9m ? Tone.Good : Tone.Warn,
                        t => t.Span(F("месяц {0} из {1}", Number(e.MonthAkb), Number(e.AkbPlan)))));
                    row.RelativeItem().Element(x => Tile(x, T("Премия КПУ"), r.KpiMetrics.Money ? F("{0} тыс.", Thousands(e.PremiumNow ?? kpu?.PremiumNow)) : "—",
                        (e.PremiumNow ?? kpu?.PremiumNow) is > 0 ? Tone.Good : Tone.Bad,
                        t => t.Span(r.KpiMetrics.Money ? F("максимум {0} тыс.", Thousands(e.PremiumMax ?? kpu?.PremiumMax)) : T("суммы скрыты"))));
                });
                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(x => Tile(x, T("Визиты / заказы"), $"{Number(e.WeekVisits)} / {Number(e.WeekOrders)}",
                        e.WeekConversion is { } cv ? (cv >= ConversionGoal ? Tone.Good : Tone.Bad) : Tone.None,
                        t => t.Span(F("конверсия {0} · цель {1}", Percent(e.WeekConversion), Percent(ConversionGoal)))));
                    row.RelativeItem().Element(x => Tile(x, T("ТТ по акции"), Number(promo?.OutletsWeek ?? 0), Tone.None,
                        t => t.Span(F("прошлая неделя {0}", Number(promo?.OutletsPrevWeek ?? 0)))));
                });

                col.Item().PaddingTop(6).Text(KpuTitle()).FontSize(11).Bold().FontColor(C.Navy);
                if (kpu is null || kpu.Cells.Count == 0)
                {
                    col.Item().Text(T("KPI Linko для этого ТП не загружены.")).FontSize(9).FontColor(C.Ink2);
                    return;
                }

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(2.2f);
                        cd.RelativeColumn();
                        cd.RelativeColumn();
                        cd.RelativeColumn(1.2f);
                    });
                    table.Header(h =>
                    {
                        foreach (var (title, right) in new[] { (T("Показатель"), false), (T("Факт"), true), (T("Порог"), true), (T("Добрать"), true) })
                        {
                            var cell = h.Cell().Background(C.Navy).PaddingVertical(4).PaddingHorizontal(6);
                            (right ? cell.AlignRight() : cell).Text(title).FontSize(8.5f).SemiBold().FontColor(Colors.White);
                        }
                    });
                    foreach (var k in kpu.Cells)
                    {
                        IContainer Cell() => table.Cell().BorderBottom(0.5f).BorderColor(C.Border).PaddingVertical(4).PaddingHorizontal(6);
                        Cell().Text(T(k.Label)).FontSize(9);
                        Cell().AlignRight().Text(t =>
                        {
                            t.Span("● ").FontColor(ToneColor(KpiTone(k.Status))).FontSize(8);
                            t.Span(Percent(k.Pct)).FontSize(9);
                        });
                        Cell().AlignRight().Text(Percent(k.Threshold)).FontSize(9);
                        Cell().AlignRight().Text(k.Status == WeeklyKpiStatuses.Passed ? T("порог взят") : Need(k)).FontSize(9).SemiBold();
                    }
                });
            });
        }

        private string KpuTitle()
        {
            var monthEnd = new DateOnly(r.DataThrough.Year, r.DataThrough.Month, 1).AddMonths(1).AddDays(-1);
            return r.DataThrough < monthEnd
                ? F("КПУ: что добрать за {0}–{1}", r.DataThrough.AddDays(1).ToString("dd", CultureInfo.InvariantCulture), monthEnd.ToString("dd.MM", CultureInfo.InvariantCulture))
                : T("КПУ месяца");
        }

        private string Need(WeeklyKpiCell k) => k.Need is not { } need ? "—" : k.Unit == KpiMath.Sum ? F("{0} млн", (need / 1_000_000).ToString("#,0.0", Ru)) : Number(Math.Ceiling(need));

        private static Tone KpiTone(string status) => status switch
        {
            WeeklyKpiStatuses.Passed => Tone.Good,
            WeeklyKpiStatuses.Realistic => Tone.Warn,
            WeeklyKpiStatuses.Far => Tone.Bad,
            _ => Tone.None,
        };

        /// <summary>% плана к темпу месяца: не ниже темпа — зелёный, до 10 п.п. ниже — жёлтый, дальше — красный.</summary>
        private Tone PlanTone(decimal? pct) => pct is not { } p ? Tone.None : p >= r.Calendar.MonthTempo ? Tone.Good : p >= r.Calendar.MonthTempo - 0.1m ? Tone.Warn : Tone.Bad;

        private static string Amount(decimal? value, string? unit) => unit == KpiMath.Sum ? (value is { } v ? (v / 1_000_000).ToString("#,0.0", Ru) : "—") : Number(value);

        private string Unit(string? unit) => unit == KpiMath.Sum ? T("млн сум") : T("кг");

        private static string Thousands(decimal? sum) => sum is { } s ? Number(Math.Round(s / 1000, 0, MidpointRounding.AwayFromZero)) : "—";

        // ---------- 2. Цели на неделю ----------

        private void Goals(IContainer c, WeeklyAgentGoal? g)
        {
            c.Column(col =>
            {
                col.Spacing(4);
                if (g is null)
                {
                    col.Item().Text(T("Целей на неделю для этого ТП нет — нет данных прошлой недели.")).FontColor(C.Ink2);
                    return;
                }

                void Goal(string label, string value, string? was = null) => col.Item().BorderBottom(0.5f).BorderColor(C.Border).PaddingVertical(6).Row(row =>
                {
                    row.RelativeItem().Text(label).FontSize(10.5f);
                    row.AutoItem().Text(t =>
                    {
                        t.Span(value).FontSize(11).Bold().FontColor(C.Navy);
                        if (was is not null)
                        {
                            t.Span(" " + was).FontSize(8).FontColor(C.Ink3);
                        }
                    });
                });

                Goal(T("Продажи"), g.SalesKg is { } kg ? F("{0} кг", Number(kg))
                    : g.SalesSum is { } sum ? $"{Amount(sum, KpiMath.Sum)} {Unit(KpiMath.Sum)}"
                    : T("плана в Linko нет"));
                Goal(T("АКБ недели"), F("{0} ТТ", Number(g.Akb)), F("было {0}", Number(g.AkbWas)));
                Goal(T("ТТ с заказом по акции"), Number(g.Promo), F("было {0}", Number(g.PromoWas)));
                Goal(T("Вернуть спящих ТТ"), Number(g.Return15To30 + g.Return30Plus), $"{Number(g.Return15To30)} + {Number(g.Return30Plus)}");
                Goal(T("ДМП: установить / дозаказ"), r.DmpMetrics.Available ? "—" : T("нет данных"));

                List(col, T("Верни: спят 15–30 дней"), g.ReturnList15To30.Select(m => (m.Name, F("{0} дн. · {1} кг", m.Days, Kg1(m.Kg)))).ToList());
                List(col, T("Верни: спят 30+ дней"), g.ReturnList30Plus.Select(m => (m.Name, F("30+ дн. · {0} кг", Kg1(m.Kg)))).ToList());
                List(col, T("ДМП"), [(T("данных о стеллажах в OneBase пока нет — загрузка инвентаря Linko будет добавлена"), "")]);

                var kinds = r.PromotionMetrics.Kinds.Where(k => k.PromoId > 0).OrderByDescending(k => k.Outlets).Take(4).Select(k => k.Name).ToList();
                var dealer = r.DealerMetrics.FirstOrDefault();
                var missing = Missing(dealer?.Deficit.Where(d => d.StockKg is not > 0).Take(8).ToList() ?? []);
                col.Item().PaddingTop(12).Background(C.Navy).CornerRadius(8).Padding(12).Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.White));
                    t.Span(T("Предлагай:") + " ").Bold();
                    t.Span(kinds.Count > 0 ? string.Join(", ", kinds) + "." : T("акций с заказами в этом месяце нет."));
                    t.Line("");
                    t.Span(T("Не обещай (нет у дилера):") + " ").Bold();
                    t.Span(dealer is null || dealer.StockDate is null ? T("остатков дилера в OneBase нет.")
                        : missing.Count > 0 ? string.Join(", ", missing) + "." : T("всё в наличии."));
                });
            });
        }

        private void List(ColumnDescriptor col, string title, IReadOnlyList<(string Name, string Muted)> items)
        {
            col.Item().PaddingTop(10).Text(title).FontSize(11).Bold().FontColor(C.Navy);
            if (items.Count == 0)
            {
                col.Item().PaddingLeft(10).Text(T("никого")).FontSize(9.5f).FontColor(C.Ink3);
                return;
            }

            foreach (var (name, muted) in items)
            {
                col.Item().PaddingLeft(10).Text(t =>
                {
                    t.Span("•  ").FontColor(C.Ink2);
                    t.Span(name).FontSize(10);
                    if (muted.Length > 0)
                    {
                        t.Span(" · " + muted).FontSize(7.5f).FontColor(C.Ink3);
                    }
                });
            }
        }

        /// <summary>«Не обещай»: короткие названия (два слова) с кодами через «/» — «Вафельные трубочки 235/231/511, KEKO джем 319».</summary>
        private static List<string> Missing(IReadOnlyList<WeeklyDealerSku> skus) => skus
            .Select(s => (Name: ShortName(s), s.Code))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Any(x => !string.IsNullOrEmpty(x.Code)) ? $"{g.Key} {string.Join('/', g.Select(x => x.Code).Where(c => !string.IsNullOrEmpty(c)).Distinct())}" : g.Key)
            .ToList();

        private static string ShortName(WeeklyDealerSku sku)
        {
            var words = sku.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).SkipWhile(w => w == sku.Code).Take(2).ToList();
            while (words.Count > 1 && words[^1].Length <= 2)
            {
                words.RemoveAt(words.Count - 1); // «Шоколад со» → «Шоколад»
            }

            return string.Join(' ', words).TrimEnd(',', '.');
        }

        private static string Kg1(decimal? kg) => kg is { } k ? k.ToString("#,0.0", Ru) : "—";

        private static void Tile(IContainer c, string label, string value, Tone accent, Action<TextDescriptor> sub)
        {
            c.Background(C.Card).CornerRadius(8).BorderLeft(4).BorderColor(ToneColor(accent)).PaddingVertical(9).PaddingHorizontal(11).Column(col =>
            {
                col.Item().Text(label).FontSize(9).FontColor(C.Ink2);
                col.Item().Text(value).FontSize(20).Bold().FontColor(C.Navy);
                col.Item().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(8).FontColor(C.Ink2));
                    sub(t);
                });
            });
        }

        private static Color ToneColor(Tone tone) => tone switch { Tone.Good => C.Green, Tone.Warn => C.Amber, Tone.Bad => C.Red, _ => C.Gray };

        private static readonly Dictionary<string, string> Uz = new()
        {
            ["Карточки ТП"] = "TP kartochkalari",
            ["В области нет ТП с данными за неделю."] = "Hududda hafta bo'yicha ma'lumoti bor TP yo'q.",
            ["1 · Итоги недели {0}"] = "1 · Hafta yakunlari {0}",
            ["2 · Цели на неделю {0}"] = "2 · Hafta maqsadlari {0}",
            ["СВР {0}"] = "SVR {0}",
            ["Данные на {0} · сформирован {1}"] = "Ma'lumotlar {0} holatiga · tuzilgan {1}",
            ["Неделя, кг"] = "Hafta, kg",
            ["к пр. неделе"] = "o'tgan haftaga",
            ["Месяц, % плана"] = "Oy, reja %",
            ["{0} из {1} {2}"] = "{1} {2} dan {0}",
            ["плана в Linko нет"] = "Linko'da reja yo'q",
            ["АКБ недели"] = "Hafta AKB",
            ["месяц {0} из {1}"] = "oy: {1} dan {0}",
            ["Премия КПУ"] = "KPU mukofoti",
            ["{0} тыс."] = "{0} ming",
            ["максимум {0} тыс."] = "maksimum {0} ming",
            ["суммы скрыты"] = "summalar yashirilgan",
            ["Визиты / заказы"] = "Tashriflar / buyurtmalar",
            ["конверсия {0} · цель {1}"] = "konversiya {0} · maqsad {1}",
            ["ТТ по акции"] = "Aksiya bo'yicha SN",
            ["прошлая неделя {0}"] = "o'tgan hafta {0}",
            ["KPI Linko для этого ТП не загружены."] = "Bu TP uchun Linko KPI yuklanmagan.",
            ["Показатель"] = "Ko'rsatkich",
            ["Факт"] = "Fakt",
            ["Порог"] = "Chegara",
            ["Добрать"] = "Yetkazish kerak",
            ["порог взят"] = "chegara olindi",
            ["КПУ: что добрать за {0}–{1}"] = "KPU: {0}–{1} da nimani yetkazish kerak",
            ["КПУ месяца"] = "Oy KPU",
            ["{0} млн"] = "{0} mln",
            ["млн сум"] = "mln so'm",
            ["кг"] = "kg",
            ["Тотал"] = "Total",
            ["Тотал (сумма)"] = "Total (summa)",
            ["Могуль"] = "Mogul",
            ["Целей на неделю для этого ТП нет — нет данных прошлой недели."] = "Bu TP uchun hafta maqsadlari yo'q — o'tgan hafta ma'lumotlari yo'q.",
            ["Продажи"] = "Savdo",
            ["{0} кг"] = "{0} kg",
            ["{0} ТТ"] = "{0} SN",
            ["было {0}"] = "edi {0}",
            ["ТТ с заказом по акции"] = "Aksiya bo'yicha buyurtmali SN",
            ["Вернуть спящих ТТ"] = "Uxlayotgan SNlarni qaytarish",
            ["ДМП: установить / дозаказ"] = "DMP: o'rnatish / qo'shimcha buyurtma",
            ["нет данных"] = "ma'lumot yo'q",
            ["Верни: спят 15–30 дней"] = "Qaytar: 15–30 kun uxlayapti",
            ["Верни: спят 30+ дней"] = "Qaytar: 30+ kun uxlayapti",
            ["{0} дн. · {1} кг"] = "{0} kun · {1} kg",
            ["30+ дн. · {0} кг"] = "30+ kun · {0} kg",
            ["ДМП"] = "DMP",
            ["данных о стеллажах в OneBase пока нет — загрузка инвентаря Linko будет добавлена"] = "OneBase'da stellajlar bo'yicha ma'lumot hozircha yo'q — Linko inventarini yuklash qo'shiladi",
            ["Предлагай:"] = "Taklif qil:",
            ["акций с заказами в этом месяце нет."] = "bu oyda buyurtmali aksiyalar yo'q.",
            ["Не обещай (нет у дилера):"] = "Va'da berma (dilerda yo'q):",
            ["остатков дилера в OneBase нет."] = "OneBase'da diler qoldiqlari yo'q.",
            ["всё в наличии."] = "hammasi bor.",
            ["никого"] = "hech kim",
        };
    }
}
