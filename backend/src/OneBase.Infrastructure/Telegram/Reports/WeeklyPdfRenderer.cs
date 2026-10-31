using System.Globalization;
using System.Text;
using OneBase.Application.Telegram.Reports;
using OneBase.Application.Telegram.Weekly;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static OneBase.Application.Telegram.Reports.ReportFormat;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Еженедельный PDF (QuestPDF, лицензия Community — решение 31.10.2026): A4 альбомная, стиль образцов «Полевого контроля» — тёмно-синие
/// заголовки, светлые плитки KPI, таблицы с повтором шапки на новой странице, светофор, столбчатые графики (SVG), нумерация и подвал с
/// источниками и временем данных. СВ — 10 страниц; РОП и РМ — итоги по регионам, дилеры и деньги. Шрифт Inter (из сборки) — кириллица и
/// узбекская латиница. Цифр не считает: только модель WeeklySupervisorReport.
/// </summary>
public sealed partial class WeeklyPdfRenderer : IWeeklyPdfRenderer
{
    private static readonly object Init = new();
    private static bool _ready;

    public byte[] Render(WeeklySupervisorReport report, string language)
    {
        EnsureReady();
        return new Doc(report, language == "uz" ? "uz" : "ru").Create().GeneratePdf();
    }

    public IReadOnlyList<byte[]> RenderImages(WeeklySupervisorReport report, string language, int dpi = 110)
    {
        EnsureReady();
        return new Doc(report, language == "uz" ? "uz" : "ru").Create().GenerateImages(new ImageGenerationSettings { RasterDpi = dpi }).ToList();
    }

    private static void EnsureReady()
    {
        lock (Init)
        {
            if (_ready)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;
            var assembly = typeof(WeeklyPdfRenderer).Assembly;
            foreach (var name in new[] { "Regular", "Medium", "SemiBold", "Bold" })
            {
                using var stream = assembly.GetManifestResourceStream($"OneBase.Fonts.Inter-{name}.ttf")
                                   ?? throw new InvalidOperationException($"Нет шрифта Inter-{name} в сборке");
                FontManager.RegisterFontFromStream(stream);
            }

            _ready = true;
        }
    }

    private static class C
    {
        public static readonly Color Navy = Color.FromHex("#1F3864");
        public static readonly Color Ink = Color.FromHex("#1E293B");
        public static readonly Color Ink2 = Color.FromHex("#475569");
        public static readonly Color Ink3 = Color.FromHex("#94A3B8");
        public static readonly Color Card = Color.FromHex("#F3F5F9");
        public static readonly Color Border = Color.FromHex("#E2E8F0");
        public static readonly Color Total = Color.FromHex("#E3E8F2");
        public static readonly Color Green = Color.FromHex("#2E9E5B");
        public static readonly Color Amber = Color.FromHex("#D99A1E");
        public static readonly Color Red = Color.FromHex("#D64545");
        public static readonly Color Gray = Color.FromHex("#9AA7BD");
        public const string NavyHex = "#1F3864";
    }

    private enum Tone
    {
        None,
        Good,
        Warn,
        Bad,
    }

    private sealed class Doc(WeeklySupervisorReport r, string lang)
    {
        private readonly bool _group = r.Role is "rop" or "rm";

        private string T(string ru) => lang == "uz" && Uz.TryGetValue(ru, out var uz) ? uz : WeeklyTexts.T(lang, ru);

        private string F(string ru, params object?[] args) => string.Format(CultureInfo.InvariantCulture, T(ru), args.Select(a => a ?? "").ToArray());

        public Document Create() => Document.Create(doc =>
        {
            doc.Page(p => Page(p, T("Главное за неделю"), T("План/факт недели и месяца, прогноз и выводы"), Summary));
            if (!_group)
            {
                doc.Page(p => Page(p, T("Динамика"), T("Как идёт неделя по дням и где мы относительно прошлых месяцев"), Dynamics));
                doc.Page(p => Page(p, T("Рейтинг ТП"), T("Кто тянет команду и кто отстаёт"), Rating));
                doc.Page(p => Page(p, T("КПУ: что добрать до конца месяца"), T("Предварительный расчёт по Linko. «Премия» в строке ниже порога — сколько даст выход на порог"), Kpu));
                doc.Page(p => Page(p, T("Линии"), T("Что продаём и что не продаём"), Lines));
                doc.Page(p => Page(p, T("Клиенты: спящие и новые"), T("Кто не заказывает 15–30 и больше 30 дней"), Clients));
                doc.Page(p => Page(p, T("Акции"), T("Как двигаются акции по ТП и какая цель на неделю"), Promo));
            }

            doc.Page(p => Page(p, T("ДМП: стеллажи TWIZOS"), T("Где стоят, берут ли Twizos и куда ставим на неделе"), Dmp));
            doc.Page(p => Page(p, T("Товар и деньги дилера"), T("Что заказать дилеру, что упустили, как платит дилер"), Dealers));
            if (!_group)
            {
                doc.Page(p => Page(p, F("Цели на неделю {0}", Range(r.WeeklyGoals.WeekFrom, r.WeeklyGoals.WeekTo)),
                    T("Командные цели и цели каждому ТП: продажи, АКБ, акции, спящие клиенты"), Goals));
            }
        });

        // ---------- Каркас страницы ----------

        private void Page(PageDescriptor page, string title, string subtitle, Action<IContainer> content)
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(26);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily("Inter").FontSize(8.5f).FontColor(C.Ink));
            page.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(Kicker()).FontSize(7.5f).FontColor(C.Ink2).LetterSpacing(0.08f);
                    row.AutoItem().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(8).SemiBold().FontColor(C.Ink3));
                        t.CurrentPageNumber();
                        t.Span("/");
                        t.TotalPages();
                    });
                });
                col.Item().PaddingTop(2).Text(title).FontSize(19).Bold().FontColor(C.Navy);
                col.Item().Text(subtitle).FontSize(8.5f).FontColor(C.Ink2);
                col.Item().PaddingTop(5).LineHorizontal(2).LineColor(C.Navy);
            });
            page.Content().PaddingTop(10).Element(content);
            page.Footer().Column(col =>
            {
                col.Item().LineHorizontal(0.5f).LineColor(C.Border);
                col.Item().PaddingTop(3).Text(SourceLine()).FontSize(6.5f).FontColor(C.Ink3);
            });
        }

        private string Kicker()
        {
            var who = r.Role switch
            {
                "rop" => T("РОП · РЕСПУБЛИКА"),
                "rm" => $"{T("РМ")} · {r.ScopeName}",
                "tp" => $"{T("ТП")} · {r.ScopeName}",
                _ => $"{T("СВР")} {r.ScopeName}" + (r.SupervisorName is { Length: > 0 } sv ? $" · {sv}" : ""),
            };
            return $"{who} · {T("НЕДЕЛЯ")} {Range(r.PeriodStart, r.PeriodEnd)}".ToUpper(lang == "uz" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo("ru-RU"));
        }

        private string SourceLine() =>
            F("Источник: OneBase — продажи и визиты Linko, KPI Linko, акции, остатки, DSO · данные на {0}, {1} из {2} рабочих дней недели · сформирован {3}",
                Date(r.DataThrough), r.Calendar.WeekWorkDaysDone, r.Calendar.WeekWorkDays,
                r.GeneratedAt.ToOffset(TimeSpan.FromHours(5)).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture));

        // ---------- Общие элементы ----------

        private static Color ToneColor(Tone tone) => tone switch
        {
            Tone.Good => C.Green,
            Tone.Warn => C.Amber,
            Tone.Bad => C.Red,
            _ => C.Gray,
        };

        /// <summary>Светофор % плана — к темпу месяца: от темпа — зелёный, от 80% темпа — жёлтый, ниже — красный.</summary>
        private Tone PlanTone(decimal? pct)
        {
            if (pct is not { } p)
            {
                return Tone.None;
            }

            var tempo = r.Calendar.MonthTempo ?? 1;
            return p >= tempo ? Tone.Good : p >= tempo * 0.8m ? Tone.Warn : Tone.Bad;
        }

        private static Tone TargetTone(decimal? value, decimal good, decimal warn) =>
            value is not { } v ? Tone.None : v >= good ? Tone.Good : v >= warn ? Tone.Warn : Tone.Bad;

        private static Tone DeltaTone(decimal? change) => change is not { } c ? Tone.None : c >= 0 ? Tone.Good : Tone.Bad;

        private static void Tile(IContainer c, string label, string value, Tone accent, Action<TextDescriptor>? sub = null, float size = 19)
        {
            c.Background(C.Card).CornerRadius(6).BorderLeft(3).BorderColor(ToneColor(accent)).PaddingVertical(7).PaddingHorizontal(10).Column(col =>
            {
                col.Item().Text(label).FontSize(8).FontColor(C.Ink2);
                col.Item().Text(value).FontSize(size).Bold().FontColor(C.Navy);
                if (sub is not null)
                {
                    col.Item().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(C.Ink2));
                        sub(t);
                    });
                }
            });
        }

        private static void Box(IContainer c, string? title, Action<IContainer> content)
        {
            c.Border(0.6f).BorderColor(C.Border).CornerRadius(8).Padding(10).Column(col =>
            {
                if (title is not null)
                {
                    col.Item().PaddingBottom(6).Text(title).FontSize(11).Bold().FontColor(C.Navy);
                }

                col.Item().Element(content);
            });
        }

        private static void Note(IContainer c, string text) => c.PaddingTop(5).Text(text).FontSize(7.5f).FontColor(C.Ink2);

        private static void DeltaSpan(TextDescriptor t, decimal? change) =>
            t.Span(WeeklyTexts.Delta(change)).FontColor(ToneColor(DeltaTone(change))).SemiBold();

        /// <summary>Столбец таблицы: заголовок, ширина (относительная), справа ли.</summary>
        private sealed record Col(string Title, float Width = 1, bool Right = true);

        /// <summary>Ячейка: текст, цвет (светофор — точкой перед текстом или цветом текста), жирность, серый хвост («· 4 дня»).</summary>
        private sealed record Cell(string Text, Tone Dot = Tone.None, Tone Color = Tone.None, bool Bold = false, string? Muted = null);

        private static Cell Plain(string text) => new(text);

        private static void Table(IContainer c, IReadOnlyList<Col> cols, IEnumerable<IReadOnlyList<Cell>> rows, IReadOnlyList<Cell>? total = null, float fontSize = 8.5f, float padY = 3.2f)
        {
            c.Table(t =>
            {
                t.ColumnsDefinition(d =>
                {
                    foreach (var col in cols)
                    {
                        d.RelativeColumn(col.Width);
                    }
                });
                t.Header(h =>
                {
                    foreach (var col in cols)
                    {
                        var cell = h.Cell().Background(C.Navy).PaddingVertical(4).PaddingHorizontal(5).AlignMiddle();
                        (col.Right ? cell.AlignRight() : cell).Text(col.Title).FontSize(fontSize - 0.5f).SemiBold().FontColor(Colors.White);
                    }
                });

                void Line(IReadOnlyList<Cell> row, bool isTotal)
                {
                    for (var i = 0; i < cols.Count; i++)
                    {
                        var value = i < row.Count ? row[i] : Plain("");
                        var cell = t.Cell().Background(isTotal ? C.Total : Colors.White).BorderBottom(0.5f).BorderColor(C.Border).PaddingVertical(padY).PaddingHorizontal(5)
                            .AlignMiddle();
                        (cols[i].Right ? cell.AlignRight() : cell).Text(x =>
                        {
                            x.DefaultTextStyle(s => s.FontSize(fontSize));
                            if (value.Dot != Tone.None)
                            {
                                x.Span("● ").FontColor(ToneColor(value.Dot));
                            }

                            var span = x.Span(value.Text);
                            if (value.Color != Tone.None)
                            {
                                span.FontColor(ToneColor(value.Color));
                            }

                            if (value.Bold || isTotal)
                            {
                                span.Bold();
                            }

                            if (value.Muted is { Length: > 0 } muted)
                            {
                                x.Span(" " + muted).FontSize(fontSize - 1.5f).FontColor(C.Ink3);
                            }
                        });
                    }
                }

                foreach (var row in rows)
                {
                    Line(row, false);
                }

                if (total is not null)
                {
                    Line(total, true);
                }
            });
        }

        private static string Thousands(decimal? sum) => sum is { } s ? Number(Math.Round(s / 1000, 0, MidpointRounding.AwayFromZero)) : "—";

        private static string Millions(decimal? sum) => sum is { } s ? (s / 1_000_000).ToString("#,0.0", Ru) : "—";

        private static readonly NumberFormatInfo Ru = new() { NumberGroupSeparator = " ", NumberDecimalSeparator = ",", NumberGroupSizes = [3] };

        private static string Kg1(decimal? kg) => kg is { } k ? k.ToString("#,0.0", Ru) : "—";

        // ---------- 1. Главное ----------

        private void Summary(IContainer c)
        {
            var s = r.SalesMetrics;
            c.Column(col =>
            {
                col.Spacing(10);
                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(x => Tile(x, F("Неделя, кг ({0})", Range(r.PeriodStart, r.DataThrough < r.PeriodEnd ? r.DataThrough : r.PeriodEnd)),
                        Number(s.WeekKg), Tone.None, t =>
                        {
                            t.Span(T("к тем же дням пр. недели") + " ");
                            DeltaSpan(t, s.WeekVsPrev);
                            t.Span(" · " + Short(s.WeekRevenue, lang));
                        }));
                    row.RelativeItem().Element(x => Tile(x, T("Месяц, кг"), Number(s.MonthKg), PlanTone(s.Execution),
                        t => t.Span(F("план {0} · выполнено {1}", Number(s.PlanKg), Percent(s.Execution)))));
                    row.RelativeItem().Element(x => Tile(x, T("Прогноз месяца"), Number(s.ForecastKg), TargetTone(s.ForecastExecution, 1, 0.9m),
                        t => t.Span(F("{0} плана · по темпу рабочих дней", Percent(s.ForecastExecution)))));
                    row.RelativeItem().Element(x => Tile(x, T("АКБ месяца"), Number(s.MonthAkb), TargetTone(s.AkbExecution, 0.9m, 0.7m),
                        t => t.Span(s.AkbPlan is null ? T("плана АКБ в Linko нет") : F("план {0} · {1}", Number(s.AkbPlan), Percent(s.AkbExecution)))));
                    row.RelativeItem().Element(x => Tile(x, T("Сумма месяца"), Millions(s.MonthRevenue) + " " + T("млн"), Tone.None,
                        t => t.Span(F("средний чек ТТ {0}", Short(s.AvgCheck, lang)))));
                });

                if (_group)
                {
                    col.Item().Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem(3f).Element(x => Box(x, null, RegionsTable));
                        row.RelativeItem().Element(x => Box(x, T("Выводы"), Conclusions));
                    });
                    return;
                }

                col.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(x => Box(x, T("Выводы недели"), Conclusions));
                    row.RelativeItem().Element(x => Box(x, T("Нужный темп до конца месяца"), Pace));
                });
                col.Item().Element(Team);
            });
        }

        private void Conclusions(IContainer c)
        {
            var notes = r.Recommendations.Select(n => (n.Code, Text: WeeklyTexts.Note(n, lang))).Where(n => n.Text is not null).ToList();
            c.Column(col =>
            {
                col.Spacing(5);
                var i = 0;
                foreach (var (code, text) in notes)
                {
                    var head = code switch { "good" => T("Хорошо:"), "bad" => T("Плохо:"), _ => T("Фокус") };
                    var color = code switch { "good" => C.Green, "bad" => C.Red, _ => C.Navy };
                    var labeled = text!.StartsWith(head, StringComparison.Ordinal);
                    col.Item().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(9).LineHeight(1.25f));
                        t.Span($"{++i}. ");
                        if (labeled)
                        {
                            t.Span(head).FontColor(color).Bold();
                        }

                        t.Span(labeled ? text[head.Length..] : text);
                    });
                }

                foreach (var warning in r.DataWarnings.Select(w => WeeklyTexts.Note(w, lang)).Where(w => w is not null))
                {
                    col.Item().Text("⚠ " + warning).FontSize(7.5f).FontColor(C.Ink2);
                }
            });
        }

        private void Pace(IContainer c)
        {
            var s = r.SalesMetrics;
            c.Column(col =>
            {
                col.Item().Element(x => Table(x, [new(T("Цель"), 2), new(T("Осталось, кг")), new(T("Нужно в день")), new(T("Сейчас в день"))],
                [
                    [new(T("80% плана (порог КПУ)"), Dot: s.To80?.PerDay is null ? Tone.Good : s.To80.PerDay <= s.CurrentPerDayKg ? Tone.Good : Tone.Bad),
                        Plain(Number(s.To80?.Remaining)), new(Number(s.To80?.PerDay), Bold: true), Plain(Number(s.CurrentPerDayKg))],
                    [new(T("100% плана"), Dot: s.To100?.PerDay is null ? Tone.Good : s.To100.PerDay <= s.CurrentPerDayKg ? Tone.Good : Tone.Bad),
                        Plain(Number(s.To100?.Remaining)), new(Number(s.To100?.PerDay), Bold: true), Plain(Number(s.CurrentPerDayKg))],
                ]));
                col.Item().Element(x => Note(x, F("Рабочие дни: пн–сб без праздников. В месяце {0}, отработано {1}, осталось {2}. Прогноз = факт ÷ {1} × {0}.",
                    r.Calendar.MonthWorkDays, r.Calendar.MonthWorkDaysDone, r.Calendar.MonthWorkDaysLeft)));
            });
        }

        private void Team(IContainer c)
        {
            var team = r.EmployeeMetrics;
            if (team.Count is 0 or > 6)
            {
                return;
            }

            c.Column(col =>
            {
                col.Item().PaddingBottom(4).Text(T("Команда за неделю")).FontSize(11).Bold().FontColor(C.Navy);
                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    foreach (var e in team)
                    {
                        row.RelativeItem().Element(x => Tile(x, e.Name, $"{Number(e.WeekKg)} {T("кг")}", PlanTone(e.Execution), t =>
                        {
                            t.Span(T("неделя") + " ");
                            DeltaSpan(t, e.WeekVsPrev);
                            t.Span(F(" · месяц {0} плана", Percent(e.Execution)));
                            if (e.PremiumNow is { } premium)
                            {
                                t.Span(F(" · КПУ {0} тыс.", Thousands(premium)));
                            }
                        }));
                    }
                });
            });
        }

        private void RegionsTable(IContainer c)
        {
            var rows = r.Regions.Select(g => (IReadOnlyList<Cell>)
            [
                new(g.Name, Bold: true), new(g.SupervisorName ?? T("вакансия"), Color: g.SupervisorName is null ? Tone.None : Tone.None), Plain(Number(g.Sales.WeekKg)),
                new(WeeklyTexts.Delta(g.Sales.WeekVsPrev), Color: DeltaTone(g.Sales.WeekVsPrev)), Plain(Number(g.Sales.MonthKg)), Plain(Number(g.Sales.PlanKg)),
                new(Percent(g.Sales.Execution), Dot: PlanTone(g.Sales.Execution)), Plain(Percent(g.Sales.ForecastExecution)),
                new(WeeklyTexts.Delta(g.Sales.VsPrevMonth), Color: DeltaTone(g.Sales.VsPrevMonth)), Plain($"{Number(g.Sales.MonthAkb)} / {Number(g.Sales.AkbPlan)}"),
            ]).ToList();
            var s = r.SalesMetrics;
            c.Column(col =>
            {
                col.Item().Element(x => Table(x,
                    [new(T("Регион"), 1.3f, false), new(T("СВР"), 2.4f, false), new(T("Неделя, кг")), new(T("к пр. нед.")), new(T("Месяц, кг")), new(T("План")),
                        new(T("% плана")), new(T("Прогноз")), new(T("к пр. мес.*")), new(T("АКБ / план"), 1.45f)],
                    rows,
                    [new(T("Итого"), Bold: true), Plain(""), Plain(Number(s.WeekKg)), new(WeeklyTexts.Delta(s.WeekVsPrev), Color: DeltaTone(s.WeekVsPrev)), Plain(Number(s.MonthKg)),
                        Plain(Number(s.PlanKg)), new(Percent(s.Execution), Dot: PlanTone(s.Execution)), Plain(Percent(s.ForecastExecution)),
                        new(WeeklyTexts.Delta(s.VsPrevMonth), Color: DeltaTone(s.VsPrevMonth)), Plain($"{Number(s.MonthAkb)} / {Number(s.AkbPlan)}")], 7.5f, 2.6f));
                col.Item().Element(x => Note(x, F("* К прошлому месяцу за то же число рабочих дней. Светофор % плана — к темпу месяца ({0} рабочих дней прошло).",
                    Percent(r.Calendar.MonthTempo))));
            });
        }

        // ---------- 2. Динамика ----------

        private void Dynamics(IContainer c)
        {
            c.Row(row =>
            {
                row.Spacing(12);
                row.RelativeItem().Element(x => Box(x, T("Продажи по дням недели, кг"), inner => inner.Column(col =>
                {
                    col.Item().Height(360).Svg(DaysChart()).FitArea();
                    col.Item().Element(n => Note(n, T("Пунктир — сколько нужно продавать в день для 100% плана месяца. Отрицательный день — возвратов больше, чем продаж.")));
                })));
                row.RelativeItem().Element(x => Box(x, F("Выполнение плана по месяцам {0}", r.DataThrough.Year), inner => inner.Column(col =>
                {
                    col.Item().Height(360).Svg(MonthsChart()).FitArea();
                    col.Item().Element(n => Note(n, T("Серый — план, цветной — факт. Текущий месяц — по отчётный день.")));
                })));
            });
        }

        private string DaysChart()
        {
            // Пропорции ≈ блока на странице (ширина ~360 pt × высота 360) — график заполняет блок без пустоты снизу.
            const int w = 520, h = 520, top = 30, bottom = 46, left = 20, right = 20;
            var days = r.SalesMetrics.Days;
            var norm = r.SalesMetrics.DailyNormKg;
            var values = days.Select(d => d.Kg ?? 0).Append(norm ?? 0).ToList();
            var max = Math.Max(1, values.Max());
            var min = Math.Min(0, days.Min(d => d.Kg ?? 0));
            var plotH = h - top - bottom;
            double Y(decimal v) => top + plotH * (double)((max - v) / (max - min));
            var slot = (w - left - right) / (double)days.Count;
            var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {w} {h}' font-family='Inter'>");
            var zero = Y(0);
            for (var i = 0; i < days.Count; i++)
            {
                var d = days[i];
                var x = left + slot * i + slot * 0.18;
                var bw = slot * 0.64;
                var label = $"{DayName(d.Date)} {d.Date.Day}";
                svg.Append($"<text x='{F1(x + bw / 2)}' y='{h - 22}' text-anchor='middle' font-size='12' fill='#64748B'>{label}</text>");
                if (d.Kg is not { } kg)
                {
                    svg.Append($"<rect x='{F1(x)}' y='{F1(zero - 40)}' width='{F1(bw)}' height='40' rx='4' fill='none' stroke='#CBD5E1' stroke-dasharray='4 3'/>");
                    svg.Append($"<text x='{F1(x + bw / 2)}' y='{F1(zero - 46)}' text-anchor='middle' font-size='11' fill='#94A3B8'>{Esc(T("впереди"))}</text>");
                    continue;
                }

                var y = Y(Math.Max(kg, 0));
                var height = Math.Max(1, Y(Math.Min(kg, 0)) - y);
                svg.Append($"<rect x='{F1(x)}' y='{F1(y)}' width='{F1(bw)}' height='{F1(height)}' rx='4' fill='{C.NavyHex}'/>");
                svg.Append($"<text x='{F1(x + bw / 2)}' y='{F1(Math.Min(y, zero) - 6)}' text-anchor='middle' font-size='13' font-weight='700' fill='#1E293B'>{Esc(Number(Math.Round(kg)))}</text>");
            }

            if (norm is { } n)
            {
                var y = Y(n);
                svg.Append($"<line x1='{left}' y1='{F1(y)}' x2='{w - right}' y2='{F1(y)}' stroke='#D64545' stroke-width='1.5' stroke-dasharray='6 4'/>");
                svg.Append($"<text x='{w - right}' y='{F1(y - 6)}' text-anchor='end' font-size='12' fill='#475569'>{Esc(F("норма плана {0} кг/день", Number(Math.Round(n))))}</text>");
            }

            svg.Append("</svg>");
            return svg.ToString();
        }

        private string MonthsChart()
        {
            const int w = 560, h = 560, top = 34, bottom = 44, left = 10, right = 10;
            var months = r.SalesMetrics.Months.Where(m => m.FactKg is not null || m.PlanKg is not null).ToList();
            var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {w} {h}' font-family='Inter'>");
            if (months.Count == 0)
            {
                svg.Append($"<text x='{w / 2}' y='{h / 2}' text-anchor='middle' font-size='14' fill='#94A3B8'>{Esc(T("нет данных"))}</text></svg>");
                return svg.ToString();
            }

            var max = Math.Max(1, months.Max(m => Math.Max(m.PlanKg ?? 0, m.FactKg ?? 0)));
            var plotH = h - top - bottom;
            var slot = (w - left - right) / (double)months.Count;
            foreach (var (m, i) in months.Select((m, i) => (m, i)))
            {
                var x = left + slot * i + slot * 0.14;
                var bw = slot * 0.72;
                if (m.PlanKg is { } plan)
                {
                    var ph = plotH * (double)(plan / max);
                    svg.Append($"<rect x='{F1(x)}' y='{F1(top + plotH - ph)}' width='{F1(bw)}' height='{F1(ph)}' rx='4' fill='#E3E8F2'/>");
                }

                var current = m.Month == r.DataThrough.Month;
                var pct = m.PlanKg is > 0 && m.FactKg is { } f ? f / m.PlanKg.Value : (decimal?)null;
                var tone = pct is null ? Tone.None : current ? PlanTone(pct) : TargetTone(pct, 1, 0.8m);
                if (m.FactKg is { } fact)
                {
                    var fh = plotH * (double)(fact / max);
                    var fx = x + bw * 0.16;
                    svg.Append($"<rect x='{F1(fx)}' y='{F1(top + plotH - fh)}' width='{F1(bw * 0.68)}' height='{F1(fh)}' rx='3' fill='{Hex(tone)}'/>");
                    svg.Append($"<text x='{F1(x + bw / 2)}' y='{F1(top + plotH - Math.Max(fh, m.PlanKg is { } p ? plotH * (double)(p / max) : 0) - 6)}' text-anchor='middle' font-size='11' font-weight='700' fill='#1E293B'>{Esc(Percent(pct))}</text>");
                }

                svg.Append($"<text x='{F1(x + bw / 2)}' y='{h - 26}' text-anchor='middle' font-size='11' fill='#475569'>{Esc(MonthName(m.Month))}</text>");
                svg.Append($"<text x='{F1(x + bw / 2)}' y='{h - 12}' text-anchor='middle' font-size='9.5' fill='#94A3B8'>{Esc(m.FactKg is { } fk ? (fk / 1000).ToString("0.0", Ru) + " " + T("т") : "")}</text>");
            }

            svg.Append("</svg>");
            return svg.ToString();
        }

        private static string Hex(Tone tone) => tone switch { Tone.Good => "#2E9E5B", Tone.Warn => "#D99A1E", Tone.Bad => "#D64545", _ => "#9AA7BD" };

        private static string F1(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        private static string Esc(string s) => System.Security.SecurityElement.Escape(s) ?? "";

        private string DayName(DateOnly d) => lang == "uz"
            ? new[] { "Ya", "Du", "Se", "Ch", "Pa", "Ju", "Sh" }[(int)d.DayOfWeek]
            : new[] { "Вс", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб" }[(int)d.DayOfWeek];

        private string MonthName(int m) => lang == "uz"
            ? new[] { "Yan", "Fev", "Mar", "Apr", "May", "Iyn", "Iyl", "Avg", "Sen", "Okt", "Noy", "Dek" }[m - 1]
            : new[] { "Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек" }[m - 1];

        // ---------- 3. Рейтинг ТП ----------

        private void Rating(IContainer c)
        {
            var best = r.EmployeeMetrics.Where(e => e.WeekVsPrev is > 0).MaxBy(e => e.WeekVsPrev);
            var rows = r.EmployeeMetrics.Select((e, i) => (IReadOnlyList<Cell>)
            [
                Plain($"{i + 1}"), new(e.Name, Bold: true, Muted: best?.AgentId == e.AgentId ? T("лучший ТП недели") : null), Plain(Number(e.WeekKg)),
                new(WeeklyTexts.Delta(e.WeekVsPrev), Color: DeltaTone(e.WeekVsPrev)), Plain(Number(e.MonthKg)), new(Percent(e.Execution), Dot: PlanTone(e.Execution)),
                new($"{Number(e.MonthAkb)} / {Number(e.AkbPlan)}", Dot: TargetTone(e.AkbPlan is > 0 ? e.MonthAkb / e.AkbPlan : null, 0.9m, 0.7m)),
                Plain(Number(e.WeekVisits)), new(Percent(e.WeekConversion), Dot: TargetTone(e.WeekConversion, 0.7m, 0.5m)),
                new(Percent(e.WeekRouteShare), Dot: TargetTone(e.WeekRouteShare, 0.9m, 0.8m)), new(Thousands(e.PremiumNow), Bold: true),
            ]).ToList();
            c.Column(col =>
            {
                col.Spacing(10);
                col.Item().Element(x => Table(x,
                    [new("#", 0.3f, false), new(T("ТП"), 2.8f, false), new(T("Неделя, кг")), new(T("к пр. неделе")), new(T("Месяц, кг")), new(T("% плана (Linko)")),
                        new(T("АКБ / план"), 1.1f), new(T("Визиты нед.")), new(T("Конверсия нед.")), new(T("Маршрут нед.")), new(T("КПУ сейчас, тыс."))],
                    rows));
                col.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(x => Box(x, T("Как читать"), b => b.Text(F(
                        "Светофор по % плана — к темпу месяца ({0} рабочих дней прошло). АКБ — порог 90%. Конверсия — цель 70% (заказ / визит). Маршрут — визиты по маршруту / план маршрута в Linko. % плана — из KPI Linko.",
                        Percent(r.Calendar.MonthTempo))).FontSize(8.5f).LineHeight(1.3f)));
                    row.RelativeItem().Element(x => Box(x, T("Вопрос на собрание"), b => b.Text(T(
                        "Почему конверсия ниже цели 70%? Разобрать 3–5 визитов без заказа у каждого ТП: нет товара, нет денег, отказ, долг?")).FontSize(8.5f).LineHeight(1.3f)));
                });
            });
        }

        // ---------- 4. КПУ ----------

        private void Kpu(IContainer c)
        {
            if (!r.KpiMetrics.Available)
            {
                c.Element(x => Box(x, null, b => b.Text(T("KPI Linko по области не загружены — премии и пороги КПУ не показаны.")).FontColor(C.Ink2)));
                return;
            }

            var people = r.KpiMetrics.People.OrderBy(p => p.Role == "sv" ? 1 : 0).ThenByDescending(p => p.PremiumNow ?? 0).ToList();
            c.Column(col =>
            {
                col.Spacing(10);
                foreach (var pair in people.Chunk(2))
                {
                    col.Item().ShowEntire().Row(row =>
                    {
                        row.Spacing(10);
                        foreach (var p in pair)
                        {
                            row.RelativeItem().Element(x => KpuPerson(x, p));
                        }

                        if (pair.Length == 1)
                        {
                            row.RelativeItem();
                        }
                    });
                }

                if (!r.KpiMetrics.Money)
                {
                    col.Item().Element(x => Note(x, T("Премии не показаны: нет права на финансовые данные (finance.read).")));
                }
            });
        }

        private void KpuPerson(IContainer c, WeeklyKpiPerson p)
        {
            var title = (p.Role == "sv" ? T("СВР") + " " : "") + p.Name + (r.KpiMetrics.Money ? F(" · премия сейчас {0} тыс.", Thousands(p.PremiumNow)) : "");
            var cols = new List<Col> { new(T("Показатель"), 1.6f, false), new(T("Факт")), new(T("Порог")), new(T("Добрать")), new(T("В день")), new(T("Статус"), 1.4f) };
            if (r.KpiMetrics.Money)
            {
                cols.Add(new(T("Премия, тыс.")));
            }

            var rows = p.Cells.Select(k =>
            {
                var tone = k.Status switch
                {
                    WeeklyKpiStatuses.Passed => Tone.Good,
                    WeeklyKpiStatuses.Realistic => Tone.Warn,
                    WeeklyKpiStatuses.Far => Tone.Bad,
                    _ => Tone.None,
                };
                var status = k.Status switch
                {
                    WeeklyKpiStatuses.Passed => T("порог пройден"),
                    WeeklyKpiStatuses.Realistic => T("реально добрать"),
                    WeeklyKpiStatuses.Far => T("далеко"),
                    _ => T("без порога"),
                };
                string Need(decimal? v) => v is null ? "—" : k.Unit == "сум" ? Short(v, lang) : Number(Math.Ceiling(v.Value));
                var row = new List<Cell>
                {
                    Plain(k.Label), new((k.Pct * 100).ToString("0.0", Ru) + "%", Dot: tone), Plain(k.Threshold is { } t ? Percent(t) : "—"), Plain(Need(k.Need)),
                    Plain(Need(k.NeedPerDay)), new(status, Color: tone, Bold: true),
                };
                if (r.KpiMetrics.Money)
                {
                    row.Add(Plain(Thousands(k.Premium)));
                }

                return (IReadOnlyList<Cell>)row;
            }).ToList();
            c.Element(x => Box(x, title, b => Table(b, cols, rows, null, 8)));
        }

        // ---------- 5. Линии ----------

        private void Lines(IContainer c)
        {
            var p = r.ProductMetrics;
            c.Column(col =>
            {
                col.Spacing(10);
                if (p.Categories.Count > 0)
                {
                    var cols = new List<Col> { new("", 1.2f, false) };
                    cols.AddRange(p.Categories.Select(x => new Col(x.Name)));
                    col.Item().Element(x => Box(x, T("Регион: план и факт по линиям, кг"), b => Table(b, cols,
                    [
                        [Plain(T("План")), .. p.Categories.Select(x => Plain(Number(x.PlanKg)))],
                        [Plain(T("Факт")), .. p.Categories.Select(x => Plain(Number(x.FactKg)))],
                        [new(T("% плана"), Bold: true), .. p.Categories.Select(x => new Cell(Percent(x.Execution), Dot: PlanTone(x.Execution)))],
                    ])));
                }

                col.Item().Row(row =>
                {
                    row.Spacing(10);
                    var names = p.Categories.Select(x => x.Name).ToList();
                    row.RelativeItem(2.1f).Element(x => Box(x, T("ТП × линия: кг за месяц · сколько ТТ купили"), b =>
                    {
                        var cols = new List<Col> { new(T("ТП"), 2f, false) };
                        cols.AddRange(names.Select(n => new Col(n)));
                        Table(b, cols, p.Agents.Select(a => (IReadOnlyList<Cell>)
                        [
                            Plain(a.Name), .. names.Select(n => a.Categories.FirstOrDefault(cat => cat.Name == n) is { } cat
                                ? new Cell(Number(Math.Round(cat.Kg)), Muted: F("· {0} ТТ", cat.Outlets))
                                : Plain("—")),
                        ]), null, 7.5f);
                    }));
                    row.RelativeItem().Element(x => Box(x, T("Приоритетные линии (Linko), кг"), b => b.Column(cc =>
                    {
                        cc.Item().Element(t => Table(t, [new(T("ТП"), 1.8f, false), new(T("Могул")), new(T("Twizos"))], p.Agents.Select(a => (IReadOnlyList<Cell>)
                        [
                            Plain(a.Name), Prio(a.Priority.FirstOrDefault(l => l.Label == "Могуль")), Prio(a.Priority.FirstOrDefault(l => l.Label == "Twizos")),
                        ]), null, 8));
                        cc.Item().Element(n => Note(n, T("Порог 80%. План и факт — из KPI Linko.")));
                    })));
                });
            });

            Cell Prio(WeeklyPriorityLine? line) => line is null
                ? Plain("—")
                : new Cell($"{Number(Math.Round(line.Fact))} / {Number(line.Plan)}", Dot: TargetTone(line.Execution, 0.8m, 0.8m));
        }

        // ---------- 6. Клиенты ----------

        private void Clients(IContainer c)
        {
            var m = r.ClientMetrics;
            c.Column(col =>
            {
                col.Spacing(10);
                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(x => Tile(x, T("База прошлого месяца"), Number(m.Base), Tone.None, t => t.Span(T("ТТ брали в прошлом месяце"))));
                    row.RelativeItem().Element(x => Tile(x, T("Спят 15–30 дней"), Number(m.Sleeping15To30), Tone.Warn,
                        t => t.Span(F("брали в этом месяце, давно не заказывают · {0} кг", Number(Math.Round(m.Sleeping15To30Kg))))));
                    row.RelativeItem().Element(x => Tile(x, T("Спят 30+ дней"), Number(m.Sleeping30Plus), Tone.Bad,
                        t => t.Span(F("брали в прошлом месяце, в этом нет · {0}", Short(m.Sleeping30PlusRevenue, lang)))));
                    row.RelativeItem().Element(x => Tile(x, T("Новые ТТ"), Number(m.New), Tone.Good,
                        t => t.Span(F("не брали в прошлом месяце · {0}", Short(m.NewRevenue, lang)))));
                });
                col.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(x => Box(x, T("Клиенты по ТП"), b => Table(b,
                        [new(T("ТП"), 2f, false), new(T("База"), 0.8f), new(T("Купили"), 1f), new(T("Удержание"), 1.35f), new(T("Спят 15–30"), 1.2f), new(T("Спят 30+"), 1.2f), new(T("Новые"), 0.9f)],
                        m.Agents.Select(a => (IReadOnlyList<Cell>)
                        [
                            Plain(a.Name), Plain(Number(a.Base)), Plain(Number(a.Bought)), Plain(Percent(a.Retention)), new(Number(a.Sleeping15To30), Color: Tone.Warn),
                            new(Number(a.Sleeping30Plus), Color: Tone.Bad), new(Number(a.New), Color: Tone.Good),
                        ]), null, 8)));
                    row.RelativeItem(1.05f).Element(x => Box(x, F("Топ-{0} спящих ТТ по объёму — визит на этой неделе", Math.Min(12, m.Sleeping.Count)), b => Table(b,
                        [new("#", 0.45f, false), new(T("ТТ"), 2.4f, false), new(T("ТП"), 2f, false), new(T("Без заказа")), new(T("Брала, кг"))],
                        m.Sleeping.Take(12).Select((s, i) => (IReadOnlyList<Cell>)
                        [
                            Plain($"{i + 1}"), Plain(s.Name), Plain(s.AgentName ?? "—"),
                            Plain(s.Bucket == WeeklySleepBuckets.Days30Plus ? T("30+ дн.") : F("{0} дн.", s.Days)),
                            new(Kg1(s.Kg), Muted: s.Bucket == WeeklySleepBuckets.Days30Plus ? T("пр. мес.") : T("этот мес.")),
                        ]), null, 7.5f)));
                });
            });
        }

        // ---------- 7. Акции ----------

        private void Promo(IContainer c)
        {
            var p = r.PromotionMetrics;
            var goals = r.WeeklyGoals.Agents.ToDictionary(a => a.AgentId, a => a.Promo);
            var weekDays = r.Calendar.WeekWorkDaysDone;
            c.Column(col =>
            {
                col.Spacing(10);
                col.Item().Element(x => Box(x, T("Как двигаются акции по ТП"), b => b.Column(cc =>
                {
                    cc.Item().Element(t => Table(t,
                        [new(T("ТП"), 2.2f, false), new(F("ТТ по акции {0}", Range(p.PrevWeekFrom, p.PrevWeekTo))), new(F("ТТ по акции {0}", Range(r.PeriodStart, r.PeriodEnd))),
                            new(T("ТТ за месяц")), new(T("Продажи по акции, млн")), new(T("Затраты, млн")), new(T("Затраты / продажи")),
                            new(F("Цель {0}, ТТ", Range(r.WeeklyGoals.WeekFrom, r.WeeklyGoals.WeekTo)))],
                        p.Agents.Select(a => (IReadOnlyList<Cell>)
                        [
                            Plain(a.Name), Plain(Number(a.OutletsPrevWeek)), new(Number(a.OutletsWeek), Muted: F("{0} дн.", weekDays)), Plain(Number(a.OutletsMonth)),
                            Plain(Millions(a.Sale)), Plain(Millions(a.Cost)), Plain(a.CostShare is { } s ? (s * 100).ToString("0.0", Ru) + "%" : "—"),
                            new(a.AgentId is { } id && goals.TryGetValue(id, out var g) ? Number(g) : "—", Bold: true),
                        ]),
                        [Plain(T("Итого")), Plain(Number(p.OutletsPrevWeek)), Plain(Number(p.OutletsWeek)), Plain(Number(p.OutletsMonth)), Plain(Millions(p.Sale)),
                            Plain(Millions(p.Cost)), Plain(p.CostShare is { } cs ? (cs * 100).ToString("0.0", Ru) + "%" : "—"), Plain(Number(r.WeeklyGoals.Promo))]));
                    cc.Item().Element(n => Note(n, T("ТТ по акции — уникальные ТТ с заказом по любой акции (в т.ч. ручная скидка). Цель = ТТ за прошлую полную неделю × 1,2, не меньше 25 на ТП.")));
                })));
                col.Item().Element(x => Box(x, T("Акции по видам за месяц"), b => Table(b,
                    [new(T("Акция"), 3f, false), new(T("ТТ")), new(T("Продажи, млн")), new(T("Затраты, млн"))],
                    p.Kinds.Take(10).Select(k => (IReadOnlyList<Cell>)[Plain(k.Name), Plain(Number(k.Outlets)), Plain(Millions(k.Sale)), Plain(Millions(k.Cost))]), null, 8)));
            });
        }

        // ---------- 8. ДМП ----------

        private void Dmp(IContainer c) => c.Element(x => Box(x, null, b => b.Column(col =>
        {
            col.Item().Text(T("Нет данных")).FontSize(14).Bold().FontColor(C.Navy);
            col.Item().PaddingTop(4).Text(T("Стеллажи TWIZOS (получено, установлено, заказы после установки) в OneBase пока не загружаются: инвентарь Linko не синхронизируется. Загрузка будет добавлена — тогда здесь появятся установка по ТТ, дозаказы и кандидаты на новые стеллажи."))
                .FontSize(9).LineHeight(1.3f).FontColor(C.Ink2);
        })));

        // ---------- 9. Дилеры ----------

        private void Dealers(IContainer c)
        {
            c.Column(col =>
            {
                col.Spacing(10);
                if (r.DealerMetrics.Count == 0)
                {
                    col.Item().Element(x => Box(x, null, b => b.Text(T("Нет данных по дилерам области.")).FontColor(C.Ink2)));
                    return;
                }

                if (_group)
                {
                    col.Item().Element(x => Box(x, T("Остатки, отгрузка и деньги дилеров"), b => Table(b,
                        [new(T("Регион"), 1.2f, false), new(T("Дилер"), 1.5f, false), new(T("SKU в нуле*")), new(T("Отгрузить до запаса, кг")), new(T("Упущено, кг")),
                            new(T("Упущено, млн")), new(T("Долг, млн")), new(T("Просрочка, млн")), new(T("DSO, дн.")), new(T("Норма"))],
                        r.DealerMetrics.Select(d => (IReadOnlyList<Cell>)
                        [
                            new(d.Region, Bold: true), Plain(d.Dealer ?? "—"), new(Number(d.ZeroSku), Color: d.ZeroSku >= 10 ? Tone.Bad : Tone.None), Plain(Number(Math.Round(d.ShipKg))),
                            Plain(Number(d.LostKg is { } lk ? Math.Round(lk) : null)), Plain(Millions(d.LostSum)), Plain(Millions(d.Money?.Debt)),
                            new(Millions(d.Money?.Overdue), Color: d.Money?.Overdue > 0 ? Tone.Bad : Tone.None), Plain(d.Money?.Dso is { } dso ? dso.ToString("0.0", Ru) : "—"),
                            Plain(d.Money?.Norm is { } n ? $"{n}" : "—"),
                        ]), null, 7.5f)));
                    col.Item().Element(x => Note(x, T("* SKU с продажами от 1 кг в день, которых нет на складе дилера. Отгрузка — до запаса 15 дней по SKU с продажами. Упущено — оценка по дням без товара в этом месяце.")));
                    return;
                }

                foreach (var d in r.DealerMetrics)
                {
                    col.Item().Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem(1.15f).Element(x => Box(x, F("Дефицит у дилера «{0}» на {1} — меньше 7 дней продаж", d.Dealer ?? d.Region, d.StockDate is { } sd ? sd.ToString("dd.MM", CultureInfo.InvariantCulture) : "—"), b => b.Column(cc =>
                        {
                            cc.Item().Element(t => Table(t, [new(T("SKU"), 3.4f, false), new(T("Остаток, кг")), new(T("Продажи в день, кг")), new(T("Хватит, дней"))],
                                d.Deficit.Select(s => (IReadOnlyList<Cell>)
                                [
                                    Plain(s.Name), Plain(Kg1(s.StockKg)), Plain(Kg1(s.KgPerDay)),
                                    s.StockKg is not > 0 ? new Cell(T("нет"), Color: Tone.Bad, Bold: true) : new Cell(s.DaysOfCover is { } dc ? dc.ToString("0.0", Ru) : "—"),
                                ]), null, 7.5f));
                            cc.Item().Element(n => Note(n, T("«нет» — товара нет: ТП не обещает его клиенту, СВР передаёт заявку дилеру. Продажи в день — по скорости «Остатков» OneBase.")));
                        })));
                        row.RelativeItem().Column(right =>
                        {
                            right.Spacing(10);
                            right.Item().Element(x => Box(x, F("Упущено из-за аутстока, {0} (оценка)", MonthName(d.LostMonth ?? r.DataThrough.Month)), b => Table(b,
                                [new(T("SKU"), 3f, false), new(T("Дней без товара")), new(T("Упущено, кг")), new(T("млн сум"))],
                                d.Lost.Select(l => (IReadOnlyList<Cell>)[Plain(l.Name), Plain(Number(l.ZeroDays)), Plain(Number(Math.Round(l.Kg))), Plain(Millions(l.Sum))]),
                                null, 7.5f)));
                            right.Item().Element(x => Money(x, d));
                        });
                    });
                }
            });
        }

        private void Money(IContainer c, WeeklyDealerMetrics d)
        {
            if (d.Money is not { } m)
            {
                c.Element(x => Box(x, T("Деньги дилера (DSO)"), b => b.Text(r.KpiMetrics.Money
                    ? T("Нет данных DSO — сверить название дилера в реестре оплат.")
                    : T("Не показаны: нет права на финансовые данные (finance.read).")).FontColor(C.Ink2)));
                return;
            }

            c.Element(x => Box(x, T("Деньги дилера (DSO)"), b => b.Column(col =>
            {
                col.Spacing(6);
                col.Item().Row(row =>
                {
                    row.Spacing(6);
                    row.RelativeItem().Element(t => Tile(t, T("Долг"), Millions(m.Debt) + " " + T("млн"), m.Overdue > 0 ? Tone.Bad : Tone.Good,
                        s => s.Span(m.Overdue > 0 ? F("просрочка {0} млн", Millions(m.Overdue)) : T("просрочки нет")), 14));
                    row.RelativeItem().Element(t => Tile(t, T("DSO"), m.Dso is { } dso ? dso.ToString("0.0", Ru) + " " + T("дн.") : "—",
                        m.Dso is { } f && m.Norm is { } n ? (f <= n ? Tone.Good : Tone.Bad) : Tone.None, s => s.Span(m.Norm is { } nn ? F("норма {0} дн.", nn) : T("норма не задана")), 14));
                    row.RelativeItem().Element(t => Tile(t, T("Оплачено в месяце"), Millions(m.PaidMonth) + " " + T("млн"), Tone.None,
                        s => s.Span(F("отгружено {0} млн", Millions(m.ShippedMonth))), 14));
                });
                if (m.Open.Count > 0)
                {
                    col.Item().Element(x => Table(x, [new(T("Отгрузка")), new(T("Остаток, млн")), new(T("Срок оплаты"))],
                        m.Open.Take(6).Select(o => (IReadOnlyList<Cell>)
                        [
                            Plain(o.Date.ToString("dd.MM", CultureInfo.InvariantCulture)), Plain(Millions(o.Remaining)),
                            new(o.Due is { } due ? due.ToString("dd.MM", CultureInfo.InvariantCulture) : "—", Color: o.Due < r.DataThrough ? Tone.Bad : Tone.None),
                        ]), null, 8));
                }
            })));
        }

        // ---------- 10. Цели ----------

        private void Goals(IContainer c)
        {
            var g = r.WeeklyGoals;
            c.Column(col =>
            {
                col.Spacing(10);
                col.Item().Row(row =>
                {
                    row.Spacing(8);
                    row.RelativeItem().Element(x => Tile(x, T("Продажи"), g.SalesKg is { } kg ? $"{Number(kg)} {T("кг")}" : "—", Tone.None,
                        t => t.Span(g.PlanOfNextMonth ? T("по плану следующего месяца") : T("ориентир по плану этого месяца"))));
                    row.RelativeItem().Element(x => Tile(x, T("АКБ недели"), $"{Number(g.Akb)} {T("ТТ")}", Tone.None, t => t.Span(T("+10% к прошлой неделе"))));
                    row.RelativeItem().Element(x => Tile(x, T("ТТ по акциям"), Number(g.Promo), Tone.None, t => t.Span(T("+20% к прошлой неделе"))));
                    row.RelativeItem().Element(x => Tile(x, T("Вернуть спящих"), Number(g.Return15To30 + g.Return30Plus), Tone.None,
                        t => t.Span(F("{0} из 15–30 дн. · {1} из 30+", g.Return15To30, g.Return30Plus))));
                    row.RelativeItem().Element(x => Tile(x, T("ДМП"), "—", Tone.None, t => t.Span(T("нет данных о стеллажах"))));
                });
                col.Item().Element(x => Box(x, F("Цели каждому ТП на {0}", Range(g.WeekFrom, g.WeekTo)), b => b.Column(cc =>
                {
                    cc.Item().Element(t => Table(t,
                        [new(T("ТП"), 2.4f, false), new(T("Продажи, кг*")), new(T("АКБ недели, ТТ")), new(T("ТТ по акциям")), new(T("Вернуть спящих 15–30")),
                            new(T("Вернуть спящих 30+"))],
                        g.Agents.Select(a => (IReadOnlyList<Cell>)
                        [
                            new(a.Name, Bold: true), Plain(Number(a.SalesKg)), new(Number(a.Akb), Muted: F("было {0}", a.AkbWas)), new(Number(a.Promo), Muted: F("было {0}", a.PromoWas)),
                            new(Number(a.Return15To30), Muted: F("из {0}", a.Sleeping15To30)), new(Number(a.Return30Plus), Muted: F("из {0}", a.Sleeping30Plus)),
                        ])));
                    cc.Item().Element(n => Note(n, F("* План месяца ÷ {0} рабочих дней × {1}. АКБ недели = уникальные ТТ прошлой полной недели × 1,1; ТТ по акциям × 1,2 (не меньше 25); вернуть 30% спящих 15–30 дней и 20% спящих 30+. Списки ТТ — в карточке каждого ТП.",
                        r.Calendar.MonthWorkDays, g.WeekWorkDays)));
                })));
                col.Item().Row(row =>
                {
                    row.Spacing(10);
                    row.RelativeItem().Element(x => Box(x, T("Задачи СВР"), b => b.Column(list =>
                    {
                        list.Spacing(3);
                        var s = r.SalesMetrics;
                        var tasks = new List<string>();
                        if (s.To80?.PerDay is { } perDay)
                        {
                            tasks.Add(F("До конца месяца — закрыть 80% плана: {0} кг в день.", Number(Math.Round(perDay))));
                        }

                        tasks.Add(T("Собрание по этому PDF, каждому ТП — его карточка целей."));
                        tasks.Add(F("Объехать с ТП топ-{0} спящих ТТ (стр. «Клиенты»).", Math.Min(12, r.ClientMetrics.Sleeping.Count)));
                        if (r.DealerMetrics.Any(d => d.Deficit.Count > 0))
                        {
                            tasks.Add(T("Заявка дилеру на дефицитные SKU (стр. «Товар и деньги дилера»)."));
                        }

                        foreach (var (task, i) in tasks.Select((t, i) => (t, i)))
                        {
                            list.Item().Text($"{i + 1}. {task}").FontSize(9);
                        }
                    })));
                    row.RelativeItem().Element(x => Box(x, T("Товар: чего нет у дилера"), b => b.Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(9).LineHeight(1.3f));
                        var missing = r.DealerMetrics.SelectMany(d => d.Deficit).Where(s => s.StockKg is not > 0).Select(s => s.Code ?? s.Name).Distinct().Take(10).ToList();
                        t.Span(T("Не обещать клиенту (нет у дилера): ")).Bold();
                        t.Span(missing.Count == 0 ? T("дефицита нет") : string.Join(", ", missing));
                    })));
                });
            });
        }
    }

    /// <summary>Узбекские подписи рисовальщика (латиница); нет в словаре — WeeklyTexts, затем русский.</summary>
    private static readonly Dictionary<string, string> Uz = new()
    {
        ["Главное за неделю"] = "Hafta asosiylari",
        ["План/факт недели и месяца, прогноз и выводы"] = "Hafta va oy reja/fakti, prognoz va xulosalar",
        ["Динамика"] = "Dinamika",
        ["Как идёт неделя по дням и где мы относительно прошлых месяцев"] = "Hafta kunlar bo'yicha qanday o'tmoqda va o'tgan oylarga nisbatan qayerdamiz",
        ["Рейтинг ТП"] = "TP reytingi",
        ["Кто тянет команду и кто отстаёт"] = "Jamoani kim tortmoqda va kim orqada",
        ["КПУ: что добрать до конца месяца"] = "KPU: oy oxirigacha nimani yig'ish kerak",
        ["Предварительный расчёт по Linko. «Премия» в строке ниже порога — сколько даст выход на порог"] = "Linko bo'yicha dastlabki hisob. Chegaradan past qatordagi «mukofot» — chegaraga chiqish qancha beradi",
        ["Линии"] = "Liniyalar",
        ["Что продаём и что не продаём"] = "Nimani sotyapmiz va nimani sotmayapmiz",
        ["Клиенты: спящие и новые"] = "Mijozlar: uxlayotgan va yangi",
        ["Кто не заказывает 15–30 и больше 30 дней"] = "Kim 15–30 va 30 kundan ortiq buyurtma bermayapti",
        ["Акции"] = "Aksiyalar",
        ["Как двигаются акции по ТП и какая цель на неделю"] = "Aksiyalar TP bo'yicha qanday harakatlanmoqda va haftalik maqsad",
        ["ДМП: стеллажи TWIZOS"] = "DMP: TWIZOS stellajlari",
        ["Где стоят, берут ли Twizos и куда ставим на неделе"] = "Qayerda turibdi, Twizos olishyaptimi va haftada qayerga qo'yamiz",
        ["Товар и деньги дилера"] = "Diler tovari va pullari",
        ["Что заказать дилеру, что упустили, как платит дилер"] = "Dilerga nima buyurtma qilish, nima boy berildi, diler qanday to'laydi",
        ["Цели на неделю {0}"] = "{0} haftasi uchun maqsadlar",
        ["Командные цели и цели каждому ТП: продажи, АКБ, акции, спящие клиенты"] = "Jamoa va har bir TP maqsadlari: savdo, AKB, aksiyalar, uxlayotgan mijozlar",
        ["РОП · РЕСПУБЛИКА"] = "ROP · RESPUBLIKA",
        ["РМ"] = "RM",
        ["ТП"] = "TP",
        ["СВР"] = "SVR",
        ["НЕДЕЛЯ"] = "HAFTA",
        ["Источник: OneBase — продажи и визиты Linko, KPI Linko, акции, остатки, DSO · данные на {0}, {1} из {2} рабочих дней недели · сформирован {3}"] =
            "Manba: OneBase — Linko savdo va tashriflari, Linko KPI, aksiyalar, qoldiqlar, DSO · ma'lumotlar {0} holatiga, haftaning {2} ish kunidan {1} · tuzilgan {3}",
        ["Неделя, кг ({0})"] = "Hafta, kg ({0})",
        ["к тем же дням пр. недели"] = "o'tgan haftaning shu kunlariga",
        ["сум"] = "so'm",
        ["Месяц, кг"] = "Oy, kg",
        ["план {0} · выполнено {1}"] = "reja {0} · bajarildi {1}",
        ["Прогноз месяца"] = "Oy prognozi",
        ["{0} плана · по темпу рабочих дней"] = "rejaning {0} · ish kunlari sur'ati bo'yicha",
        ["АКБ месяца"] = "Oy AKB",
        ["плана АКБ в Linko нет"] = "Linko'da AKB rejasi yo'q",
        ["план {0} · {1}"] = "reja {0} · {1}",
        ["Сумма месяца"] = "Oy summasi",
        ["средний чек ТТ {0}"] = "SN o'rtacha cheki {0}",
        ["Выводы"] = "Xulosalar",
        ["Выводы недели"] = "Hafta xulosalari",
        ["Нужный темп до конца месяца"] = "Oy oxirigacha kerakli sur'at",
        ["Хорошо:"] = "Yaxshi:",
        ["Плохо:"] = "Yomon:",
        ["Фокус"] = "Diqqat",
        ["Цель"] = "Maqsad",
        ["Осталось, кг"] = "Qoldi, kg",
        ["Нужно в день"] = "Kuniga kerak",
        ["Сейчас в день"] = "Hozir kuniga",
        ["80% плана (порог КПУ)"] = "Rejaning 80% (KPU chegarasi)",
        ["100% плана"] = "Rejaning 100%",
        ["Рабочие дни: пн–сб без праздников. В месяце {0}, отработано {1}, осталось {2}. Прогноз = факт ÷ {1} × {0}."] =
            "Ish kunlari: du–sh, bayramlarsiz. Oyda {0}, ishlandi {1}, qoldi {2}. Prognoz = fakt ÷ {1} × {0}.",
        ["Команда за неделю"] = "Hafta jamoasi",
        ["кг"] = "kg",
        ["неделя"] = "hafta",
        [" · месяц {0} плана"] = " · oy rejaning {0}",
        [" · КПУ {0} тыс."] = " · KPU {0} ming",
        ["вакансия"] = "vakansiya",
        ["Регион"] = "Hudud",
        ["Неделя, кг"] = "Hafta, kg",
        ["к пр. нед."] = "o'tg. haftaga",
        ["План"] = "Reja",
        ["% плана"] = "Reja %",
        ["Прогноз"] = "Prognoz",
        ["к пр. мес.*"] = "o'tg. oyga*",
        ["АКБ / план"] = "AKB / reja",
        ["Итого"] = "Jami",
        ["* К прошлому месяцу за то же число рабочих дней. Светофор % плана — к темпу месяца ({0} рабочих дней прошло)."] =
            "* O'tgan oyga xuddi shuncha ish kuni uchun. Reja % svetofori — oy sur'atiga nisbatan (ish kunlarining {0} o'tdi).",
        ["Продажи по дням недели, кг"] = "Hafta kunlari bo'yicha savdo, kg",
        ["Пунктир — сколько нужно продавать в день для 100% плана месяца. Отрицательный день — возвратов больше, чем продаж."] =
            "Punktir — oy rejasining 100% i uchun kuniga qancha sotish kerak. Manfiy kun — qaytarishlar savdodan ko'p.",
        ["Выполнение плана по месяцам {0}"] = "{0} yil oylar bo'yicha reja bajarilishi",
        ["Серый — план, цветной — факт. Текущий месяц — по отчётный день."] = "Kulrang — reja, rangli — fakt. Joriy oy — hisobot kunigacha.",
        ["впереди"] = "oldinda",
        ["норма плана {0} кг/день"] = "reja me'yori {0} kg/kun",
        ["нет данных"] = "ma'lumot yo'q",
        ["т"] = "t",
        ["лучший ТП недели"] = "haftaning eng yaxshi TP",
        ["к пр. неделе"] = "o'tg. haftaga",
        ["% плана (Linko)"] = "Reja % (Linko)",
        ["Визиты нед."] = "Hafta tashriflari",
        ["Конверсия нед."] = "Hafta konversiyasi",
        ["Маршрут нед."] = "Hafta marshruti",
        ["КПУ сейчас, тыс."] = "KPU hozir, ming",
        ["Как читать"] = "Qanday o'qish kerak",
        ["Светофор по % плана — к темпу месяца ({0} рабочих дней прошло). АКБ — порог 90%. Конверсия — цель 70% (заказ / визит). Маршрут — визиты по маршруту / план маршрута в Linko. % плана — из KPI Linko."] =
            "Reja % svetofori — oy sur'atiga nisbatan (ish kunlarining {0} o'tdi). AKB — 90% chegara. Konversiya — maqsad 70% (buyurtma / tashrif). Marshrut — marshrut bo'yicha tashriflar / Linko marshrut rejasi. Reja % — Linko KPI dan.",
        ["Вопрос на собрание"] = "Yig'ilish uchun savol",
        ["Почему конверсия ниже цели 70%? Разобрать 3–5 визитов без заказа у каждого ТП: нет товара, нет денег, отказ, долг?"] =
            "Nega konversiya 70% maqsaddan past? Har bir TP ning buyurtmasiz 3–5 tashrifini tahlil qiling: tovar yo'q, pul yo'q, rad etish, qarz?",
        ["Премии не показаны: нет права на финансовые данные (finance.read)."] = "Mukofotlar ko'rsatilmagan: moliyaviy ma'lumotlarga huquq yo'q (finance.read).",
        [" · премия сейчас {0} тыс."] = " · hozirgi mukofot {0} ming",
        ["Показатель"] = "Ko'rsatkich",
        ["Факт"] = "Fakt",
        ["Порог"] = "Chegara",
        ["Добрать"] = "Yig'ish kerak",
        ["В день"] = "Kuniga",
        ["Статус"] = "Holat",
        ["Премия, тыс."] = "Mukofot, ming",
        ["порог пройден"] = "chegaradan o'tildi",
        ["реально добрать"] = "yig'ish mumkin",
        ["далеко"] = "uzoq",
        ["без порога"] = "chegarasiz",
        ["Регион: план и факт по линиям, кг"] = "Hudud: liniyalar bo'yicha reja va fakt, kg",
        ["ТП × линия: кг за месяц · сколько ТТ купили"] = "TP × liniya: oy uchun kg · nechta SN oldi",
        ["· {0} ТТ"] = "· {0} SN",
        ["Приоритетные линии (Linko), кг"] = "Ustuvor liniyalar (Linko), kg",
        ["Могул"] = "Mogul",
        ["Порог 80%. План и факт — из KPI Linko."] = "Chegara 80%. Reja va fakt — Linko KPI dan.",
        ["База прошлого месяца"] = "O'tgan oy bazasi",
        ["ТТ брали в прошлом месяце"] = "SN o'tgan oyda olgan",
        ["Спят 15–30 дней"] = "15–30 kun uxlayapti",
        ["брали в этом месяце, давно не заказывают · {0} кг"] = "bu oyda olgan, uzoq vaqt buyurtma bermayapti · {0} kg",
        ["Спят 30+ дней"] = "30+ kun uxlayapti",
        ["брали в прошлом месяце, в этом нет · {0}"] = "o'tgan oyda olgan, bu oyda yo'q · {0}",
        ["Новые ТТ"] = "Yangi SN",
        ["не брали в прошлом месяце · {0}"] = "o'tgan oyda olmagan · {0}",
        ["Клиенты по ТП"] = "TP bo'yicha mijozlar",
        ["База"] = "Baza",
        ["Купили"] = "Oldi",
        ["Удержание"] = "Ushlab qolish",
        ["Спят 15–30"] = "Uxlayapti 15–30",
        ["Спят 30+"] = "Uxlayapti 30+",
        ["Новые"] = "Yangi",
        ["Топ-{0} спящих ТТ по объёму — визит на этой неделе"] = "Hajm bo'yicha top-{0} uxlayotgan SN — shu hafta tashrif",
        ["ТТ"] = "SN",
        ["Без заказа"] = "Buyurtmasiz",
        ["Брала, кг"] = "Olgan, kg",
        ["30+ дн."] = "30+ kun",
        ["{0} дн."] = "{0} kun",
        ["пр. мес."] = "o'tg. oy",
        ["этот мес."] = "shu oy",
        ["Как двигаются акции по ТП"] = "Aksiyalar TP bo'yicha qanday harakatlanmoqda",
        ["ТТ по акции {0}"] = "Aksiya bo'yicha SN {0}",
        ["ТТ за месяц"] = "Oy uchun SN",
        ["Продажи по акции, млн"] = "Aksiya bo'yicha savdo, mln",
        ["Затраты, млн"] = "Xarajatlar, mln",
        ["Затраты / продажи"] = "Xarajat / savdo",
        ["Цель {0}, ТТ"] = "Maqsad {0}, SN",
        ["ТТ по акции — уникальные ТТ с заказом по любой акции (в т.ч. ручная скидка). Цель = ТТ за прошлую полную неделю × 1,2, не меньше 25 на ТП."] =
            "Aksiya bo'yicha SN — istalgan aksiya bo'yicha buyurtma bergan noyob SN (qo'lda chegirma ham). Maqsad = o'tgan to'liq hafta SN × 1,2, TP uchun kamida 25.",
        ["Акции по видам за месяц"] = "Oy uchun aksiyalar turlari bo'yicha",
        ["Акция"] = "Aksiya",
        ["Продажи, млн"] = "Savdo, mln",
        ["Нет данных"] = "Ma'lumot yo'q",
        ["Стеллажи TWIZOS (получено, установлено, заказы после установки) в OneBase пока не загружаются: инвентарь Linko не синхронизируется. Загрузка будет добавлена — тогда здесь появятся установка по ТТ, дозаказы и кандидаты на новые стеллажи."] =
            "TWIZOS stellajlari (olingan, o'rnatilgan, o'rnatishdan keyingi buyurtmalar) OneBase'ga hozircha yuklanmaydi: Linko inventari sinxronlanmaydi. Yuklash qo'shiladi — shunda bu yerda SN bo'yicha o'rnatish, qo'shimcha buyurtmalar va yangi stellajlar uchun nomzodlar paydo bo'ladi.",
        ["Нет данных по дилерам области."] = "Hudud dilerlari bo'yicha ma'lumot yo'q.",
        ["Остатки, отгрузка и деньги дилеров"] = "Diler qoldiqlari, jo'natma va pullari",
        ["Дилер"] = "Diler",
        ["SKU в нуле*"] = "Nolda SKU*",
        ["Отгрузить до запаса, кг"] = "Zaxiragacha jo'natish, kg",
        ["Упущено, кг"] = "Boy berildi, kg",
        ["Упущено, млн"] = "Boy berildi, mln",
        ["Долг, млн"] = "Qarz, mln",
        ["Просрочка, млн"] = "Muddati o'tgan, mln",
        ["DSO, дн."] = "DSO, kun",
        ["Норма"] = "Me'yor",
        ["* SKU с продажами от 1 кг в день, которых нет на складе дилера. Отгрузка — до запаса 15 дней по SKU с продажами. Упущено — оценка по дням без товара в этом месяце."] =
            "* Kuniga 1 kg dan sotiladigan, diler omborida yo'q SKU. Jo'natma — sotilayotgan SKU bo'yicha 15 kunlik zaxiragacha. Boy berilgan — shu oyda tovarsiz kunlar bo'yicha baho.",
        ["Дефицит у дилера «{0}» на {1} — меньше 7 дней продаж"] = "«{0}» dilerida {1} holatiga taqchillik — 7 kunlik savdodan kam",
        ["SKU"] = "SKU",
        ["Остаток, кг"] = "Qoldiq, kg",
        ["Продажи в день, кг"] = "Kunlik savdo, kg",
        ["Хватит, дней"] = "Yetadi, kun",
        ["нет"] = "yo'q",
        ["«нет» — товара нет: ТП не обещает его клиенту, СВР передаёт заявку дилеру. Продажи в день — по скорости «Остатков» OneBase."] =
            "«yo'q» — tovar yo'q: TP uni mijozga va'da qilmaydi, SVR dilerga ariza beradi. Kunlik savdo — OneBase «Qoldiqlar» tezligi bo'yicha.",
        ["Упущено из-за аутстока, {0} (оценка)"] = "Autstok tufayli boy berildi, {0} (baho)",
        ["Дней без товара"] = "Tovarsiz kunlar",
        ["млн сум"] = "mln so'm",
        ["Деньги дилера (DSO)"] = "Diler pullari (DSO)",
        ["Нет данных DSO — сверить название дилера в реестре оплат."] = "DSO ma'lumoti yo'q — to'lovlar reestrida diler nomini tekshiring.",
        ["Не показаны: нет права на финансовые данные (finance.read)."] = "Ko'rsatilmagan: moliyaviy ma'lumotlarga huquq yo'q (finance.read).",
        ["Долг"] = "Qarz",
        ["млн"] = "mln",
        ["просрочка {0} млн"] = "muddati o'tgan {0} mln",
        ["просрочки нет"] = "muddati o'tgan yo'q",
        ["DSO"] = "DSO",
        ["дн."] = "kun",
        ["норма {0} дн."] = "me'yor {0} kun",
        ["норма не задана"] = "me'yor berilmagan",
        ["Оплачено в месяце"] = "Oyda to'langan",
        ["отгружено {0} млн"] = "jo'natilgan {0} mln",
        ["Отгрузка"] = "Jo'natma",
        ["Остаток, млн"] = "Qoldiq, mln",
        ["Срок оплаты"] = "To'lov muddati",
        ["Продажи"] = "Savdo",
        ["по плану следующего месяца"] = "keyingi oy rejasi bo'yicha",
        ["ориентир по плану этого месяца"] = "shu oy rejasi bo'yicha mo'ljal",
        ["АКБ недели"] = "Hafta AKB",
        ["+10% к прошлой неделе"] = "o'tgan haftaga +10%",
        ["ТТ по акциям"] = "Aksiyalar bo'yicha SN",
        ["+20% к прошлой неделе"] = "o'tgan haftaga +20%",
        ["Вернуть спящих"] = "Uxlayotganlarni qaytarish",
        ["{0} из 15–30 дн. · {1} из 30+"] = "15–30 kundan {0} · 30+ dan {1}",
        ["ДМП"] = "DMP",
        ["нет данных о стеллажах"] = "stellajlar bo'yicha ma'lumot yo'q",
        ["Цели каждому ТП на {0}"] = "{0} uchun har bir TP maqsadlari",
        ["Продажи, кг*"] = "Savdo, kg*",
        ["АКБ недели, ТТ"] = "Hafta AKB, SN",
        ["Вернуть спящих 15–30"] = "Uxlayotganlarni qaytarish 15–30",
        ["Вернуть спящих 30+"] = "Uxlayotganlarni qaytarish 30+",
        ["было {0}"] = "edi {0}",
        ["из {0}"] = "{0} dan",
        ["* План месяца ÷ {0} рабочих дней × {1}. АКБ недели = уникальные ТТ прошлой полной недели × 1,1; ТТ по акциям × 1,2 (не меньше 25); вернуть 30% спящих 15–30 дней и 20% спящих 30+. Списки ТТ — в карточке каждого ТП."] =
            "* Oy rejasi ÷ {0} ish kuni × {1}. Hafta AKB = o'tgan to'liq hafta noyob SN × 1,1; aksiyalar bo'yicha SN × 1,2 (kamida 25); 15–30 kun uxlayotganlarning 30% i va 30+ dagilarning 20% ini qaytarish. SN ro'yxatlari — har bir TP kartochkasida.",
        ["Задачи СВР"] = "SVR vazifalari",
        ["До конца месяца — закрыть 80% плана: {0} кг в день."] = "Oy oxirigacha — rejaning 80% ini yopish: kuniga {0} kg.",
        ["Собрание по этому PDF, каждому ТП — его карточка целей."] = "Shu PDF bo'yicha yig'ilish, har bir TP ga — uning maqsadlar kartochkasi.",
        ["Объехать с ТП топ-{0} спящих ТТ (стр. «Клиенты»)."] = "TP bilan top-{0} uxlayotgan SN ni aylanib chiqish («Mijozlar» sahifasi).",
        ["Заявка дилеру на дефицитные SKU (стр. «Товар и деньги дилера»)."] = "Taqchil SKU bo'yicha dilerga ariza («Diler tovari va pullari» sahifasi).",
        ["Товар: чего нет у дилера"] = "Tovar: dilerda nima yo'q",
        ["Не обещать клиенту (нет у дилера): "] = "Mijozga va'da qilmang (dilerda yo'q): ",
        ["дефицита нет"] = "taqchillik yo'q",
        ["KPI Linko по области не загружены — премии и пороги КПУ не показаны."] = "Hudud bo'yicha Linko KPI yuklanmagan — mukofotlar va KPU chegaralari ko'rsatilmagan.",
    };
}
