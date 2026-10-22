using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;
using SkiaSharp;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Картинки автоотчёта (PNG) на сервере, без браузера: SkiaSharp и шрифт Inter (кириллица, узбекская латиница — o', g'). Формат 16:9 —
/// 1920×1080, каждая страница одного размера. Сверху — отчёт, регион, супервайзер, РМ и дата; на первой странице — показатели; дальше —
/// таблица агентов (на следующих страницах — с заголовком «продолжение»); в конце — итог, «требуют внимания» и «лучшие». Источников на
/// картинке нет. Всё в памяти: временных файлов нет.
/// </summary>
public sealed class SkiaDailyReportRenderer : IDailyReportRenderer
{
    /// <summary>Логическая ширина и высота страницы; на картинке — ×2 (1920×1080).</summary>
    public const float Width = 960;

    public const float Height = 540;

    public const float Scale = 2;

    private const float Pad = 28;
    private const float CardPad = 16;
    private const float Gap = 12;
    private const float BandHeight = 86;
    private const float StripHeight = 26;
    private const float FooterHeight = 30;
    private const float RowHeight = 34;
    private const float HeaderRowHeight = 32;
    private const float TotalRowHeight = 42;
    private const float KpiHeight = 84;

    /// <summary>В блоке «требуют внимания» / «лучшие» — не больше строк; остальное — «и ещё N».</summary>
    private const int MaxListItems = 7;

    private static readonly Lazy<Typefaces> Faces = new(Typefaces.Load);

    public IReadOnlyList<byte[]> Render(DailyReport report)
    {
        var layout = new Layout(report, Faces.Value);
        var pages = layout.Paginate(layout.Blocks());
        return pages.Select((page, i) => layout.DrawPage(page, i, pages.Count)).ToList();
    }

    /// <summary>
    /// Кусок страницы. Group — карточка, к которой он относится (фон карточки рисуется под всеми её кусками на странице); Continuation —
    /// что поставить первым на новой странице, если перенос пришёлся на этот кусок (заголовок таблицы).
    /// </summary>
    private sealed record Block(string Group, float Height, Action<SKCanvas, float> Draw, bool KeepWithNext = false, Block? Continuation = null, bool Plain = false);

    private sealed class Typefaces
    {
        public required SKTypeface Regular { get; init; }
        public required SKTypeface Medium { get; init; }
        public required SKTypeface SemiBold { get; init; }
        public required SKTypeface Bold { get; init; }

        public static Typefaces Load() => new()
        {
            Regular = Font("Inter-Regular.ttf"),
            Medium = Font("Inter-Medium.ttf"),
            SemiBold = Font("Inter-SemiBold.ttf"),
            Bold = Font("Inter-Bold.ttf"),
        };

        private static SKTypeface Font(string file)
        {
            using var stream = typeof(SkiaDailyReportRenderer).Assembly.GetManifestResourceStream($"OneBase.Fonts.{file}")
                               ?? throw new InvalidOperationException($"Шрифт {file} не найден в сборке.");
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            return SKTypeface.FromStream(copy) ?? throw new InvalidOperationException($"Шрифт {file} не читается.");
        }
    }

    private static class Palette
    {
        public static readonly SKColor Background = SKColor.Parse("#F3F5F9");
        public static readonly SKColor Card = SKColors.White;
        public static readonly SKColor CardBorder = SKColor.Parse("#E3E8EF");
        public static readonly SKColor Navy = SKColor.Parse("#0B1F3A");
        public static readonly SKColor Navy2 = SKColor.Parse("#173E70");
        public static readonly SKColor Ink = SKColor.Parse("#0E1726");
        public static readonly SKColor Ink2 = SKColor.Parse("#4A5568");
        public static readonly SKColor Ink3 = SKColor.Parse("#8A94A6");
        public static readonly SKColor Line = SKColor.Parse("#EDF0F5");
        public static readonly SKColor Zebra = SKColor.Parse("#F8FAFC");
        public static readonly SKColor Accent = SKColor.Parse("#2563EB");
        public static readonly SKColor AccentSoft = SKColor.Parse("#EEF3FF");
        public static readonly SKColor Good = SKColor.Parse("#15803D");
        public static readonly SKColor Warn = SKColor.Parse("#B45309");
        public static readonly SKColor Bad = SKColor.Parse("#DC2626");
        public static readonly SKColor Test = SKColor.Parse("#FFB020");
        public static readonly SKColor TestInk = SKColor.Parse("#3B2500");
    }

    /// <summary>Шрифты, цвета, раскладка и рисование одного отчёта.</summary>
    private sealed class Layout(DailyReport report, Typefaces faces)
    {
        private readonly string _lang = report.Language;
        private float Left => Pad + CardPad;
        private float Right => Width - Pad - CardPad;
        private float Inner => Right - Left;
        private bool Strip => report.Demo || report.Check;
        private float ContentTop => BandHeight + (Strip ? StripHeight : 0) + 14;
        private float ContentBottom => Height - FooterHeight;
        private bool SingleTeam => report.Teams.Count == 1;

        private string T(string ru) => DailyReportTexts.Get(_lang, ru);

        private string Amount(decimal? v) => v is { } x ? ReportFormat.Amount(x, report.Metric) : "—";

        private string Unit => DailyReportTexts.Unit(report.Metric, _lang);

        // ---------- Раскладка ----------

        public List<Block> Blocks()
        {
            var blocks = new List<Block> { Kpi() };
            blocks.AddRange(Table());
            if (Panels() is { } panels)
            {
                blocks.Add(panels);
            }

            if (report.Notes.Count > 0)
            {
                blocks.Add(Notes());
            }

            return blocks;
        }

        /// <summary>Блоки по страницам фиксированной высоты: блок не режется; таблица переносится по строкам с заголовком.</summary>
        public List<List<Block>> Paginate(IReadOnlyList<Block> blocks)
        {
            var room = ContentBottom - ContentTop - 4;
            var pages = new List<List<Block>> { new() };
            var used = 0f;
            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                var gap = pages[^1].Count > 0 && pages[^1][^1].Group != block.Group ? Gap : 0;
                var need = gap + block.Height + (block.KeepWithNext && i + 1 < blocks.Count ? blocks[i + 1].Height : 0);
                if (pages[^1].Count > 0 && used + need > room)
                {
                    pages.Add([]);
                    used = 0;
                    gap = 0;
                    if (block.Continuation is { } continuation)
                    {
                        pages[^1].Add(continuation);
                        used += continuation.Height;
                    }
                }

                pages[^1].Add(block);
                used += gap + block.Height;
            }

            return pages;
        }

        /// <summary>Показатели — строкой плиток: утром — план, днём и вечером — факт, выполнение, отклонение, визиты.</summary>
        private Block Kpi()
        {
            var total = report.Total;
            var tiles = new List<(string Label, string Value, string? Sub, SKColor? Color, decimal? Bar)>();
            if (!report.HasFact)
            {
                tiles.Add((T("План продаж"), total.Plan is { } p ? $"{ReportFormat.Amount(p, report.Metric)} {Unit}" : T("нет данных"), null, null, null));
                tiles.Add((T("План визитов"), total.VisitPlan?.ToString() ?? T("нет данных"), null, null, null));
                tiles.Add((T("Агентов"), total.Agents.ToString(), null, null, null));
                tiles.Add((T("Без плана"), total.AgentsWithoutPlan.ToString(), null, total.AgentsWithoutPlan > 0 ? Palette.Warn : null, null));
            }
            else
            {
                tiles.Add((T("Факт продаж"), total.Fact is { } f ? $"{ReportFormat.Amount(f, report.Metric)} {Unit}" : T("нет данных"),
                    $"{T("План")}: {Amount(total.Plan)} {Unit}", null, null));
                tiles.Add((T("Выполнение"), ReportFormat.Percent(total.Share), null, ShareColor(total.Share), total.Share ?? 0));
                tiles.Add((T("Отклонение"), total.Deviation is { } d ? ReportFormat.Signed(d, report.Metric) : "—", null, total.Deviation is { } dv ? DeviationColor(dv) : null, null));
                tiles.Add((T("Визиты: факт / план"), $"{total.VisitFact?.ToString() ?? "—"} / {total.VisitPlan?.ToString() ?? "—"}", null, null, null));
                tiles.Add((T("Агентов"), total.Agents.ToString(), null, null, null));
            }

            return new Block("kpi", KpiHeight, (c, y) =>
            {
                // Первая плитка — шире: главная цифра.
                var weights = tiles.Select((_, i) => i == 0 ? 1.7f : 1f).ToList();
                var unit = (Width - Pad * 2 - Gap * (tiles.Count - 1)) / weights.Sum();
                var x = Pad;
                for (var i = 0; i < tiles.Count; i++)
                {
                    var w = unit * weights[i];
                    var rect = new SKRect(x, y, x + w, y + KpiHeight);
                    using (var fill = Fill(Palette.Card))
                    using (var border = Stroke(Palette.CardBorder, 1))
                    {
                        c.DrawRoundRect(rect, 14, 14, fill);
                        c.DrawRoundRect(rect, 14, 14, border);
                    }

                    var tile = tiles[i];
                    TextFit(c, tile.Label, x + 14, y + 12, w - 28, 12, faces.Medium, Palette.Ink3);
                    TextFit(c, tile.Value, x + 14, y + 32, w - 28, i == 0 ? 26 : 24, faces.Bold, tile.Color ?? Palette.Ink);
                    if (tile.Sub is { } sub)
                    {
                        TextFit(c, sub, x + 14, y + 64, w - 28, 11.5f, faces.Regular, Palette.Ink2);
                    }

                    if (tile.Bar is { } share)
                    {
                        Bar(c, x + 14, y + 68, w - 28, share);
                    }

                    x += w + Gap;
                }
            });
        }

        private sealed record Column(string Title, float Width, bool RightAlign);

        private List<Column> Columns()
        {
            if (!report.HasFact)
            {
                return [new("№", 34, false), new(T("Агент"), Inner - 34 - 220 - 160, false), new(T(report.Metric == ReportMetric.Kg ? "План, кг" : "План, сум"), 220, true),
                    new(T("План визитов"), 160, true)];
            }

            return
            [
                new("№", 34, false),
                new(T("Агент"), Inner - 34 - 140 - 140 - 130 - 76 - 104, false),
                new(T(report.Metric == ReportMetric.Kg ? "План, кг" : "План, сум"), 140, true),
                new(T(report.Metric == ReportMetric.Kg ? "Факт, кг" : "Факт, сум"), 140, true),
                new(T("Откл."), 130, true),
                new("%", 76, true),
                new(T("Визиты"), 104, true),
            ];
        }

        /// <summary>Таблица: заголовок, строки агентов (несколько команд — с подзаголовком и итогом каждой), итог.</summary>
        private IEnumerable<Block> Table()
        {
            const string group = "table";
            Block Head(bool continued) => new(group, CardPad / 2 + HeaderRowHeight, (c, y) => ColumnHeader(c, y + CardPad / 2, continued), KeepWithNext: true);
            var continued = Head(true);
            yield return Head(false);

            var number = 0;
            foreach (var team in report.Teams)
            {
                if (!SingleTeam)
                {
                    yield return new Block(group, RowHeight, (c, y) =>
                    {
                        using (var fill = Fill(Palette.AccentSoft))
                        {
                            c.DrawRect(new SKRect(Left - 8, y + 3, Right + 8, y + RowHeight - 3), fill);
                        }

                        TextFit(c, $"{T("Супервайзер")}: {team.Supervisor}", Left, y + 9, Inner * 0.7f, 14, faces.SemiBold, Palette.Ink);
                        TextRight(c, $"{T("Агентов")}: {team.Totals.Agents}", Right, y + 10, 12.5f, faces.Medium, Palette.Ink2);
                    }, KeepWithNext: true, Continuation: continued);
                }

                if (team.Agents.Count == 0)
                {
                    yield return new Block(group, RowHeight, (c, y) => Text(c, T("В регионе нет агентов за этот месяц"), Left, y + 9, 14, faces.Regular, Palette.Ink3),
                        Continuation: continued);
                }

                foreach (var agent in team.Agents)
                {
                    var n = ++number;
                    yield return new Block(group, RowHeight, (c, y) => Row(c, y, n, agent), Continuation: continued);
                }

                if (!SingleTeam)
                {
                    yield return new Block(group, TotalRowHeight, (c, y) => TotalRow(c, y, $"{T("Итого по команде")} · {team.Supervisor}", team.Totals, strong: false),
                        Continuation: continued);
                }
            }

            var title = SingleTeam ? $"{T("Итого")} · {T("Агентов").ToLowerInvariant()}: {report.Total.Agents}" : $"{T("Итого по региону")} · {T("Агентов").ToLowerInvariant()}: {report.Total.Agents}";
            yield return new Block(group, TotalRowHeight + CardPad / 2, (c, y) => TotalRow(c, y, title, report.Total, strong: true), Continuation: continued);
        }

        private void ColumnHeader(SKCanvas c, float top, bool continued)
        {
            var x = Left;
            foreach (var (col, i) in Columns().Select((col, i) => (col, i)))
            {
                var title = (i == 1 && continued ? $"{col.Title} · {T("продолжение")}" : col.Title).ToUpperInvariant();
                if (col.RightAlign)
                {
                    TextRightFit(c, title, x + col.Width, top + 10, col.Width - 6, 10.5f, faces.SemiBold, Palette.Ink3);
                }
                else
                {
                    TextFit(c, title, x, top + 10, col.Width - 6, 10.5f, faces.SemiBold, Palette.Ink3);
                }

                x += col.Width;
            }

            Separator(c, top + HeaderRowHeight - 1);
        }

        private void Row(SKCanvas c, float y, int number, ReportAgentRow agent)
        {
            if (number % 2 == 0)
            {
                using var zebra = Fill(Palette.Zebra);
                c.DrawRect(new SKRect(Left - 8, y, Right + 8, y + RowHeight), zebra);
            }

            var cols = Columns();
            var top = y + 8;
            Text(c, number.ToString(), Left, top + 1, 13, faces.Medium, Palette.Ink3);
            var x = Left + cols[0].Width;
            TextFit(c, agent.Name, x, top, cols[1].Width - 10, 16, faces.Medium, Palette.Ink);
            x += cols[1].Width;
            if (!report.HasFact)
            {
                Number(c, agent.Plan is null ? T("нет плана") : Amount(agent.Plan), x + cols[2].Width, top, cols[2].Width, agent.Plan is null ? Palette.Ink3 : Palette.Ink, faces.SemiBold);
                x += cols[2].Width;
                Number(c, agent.VisitPlan?.ToString() ?? "—", x + cols[3].Width, top, cols[3].Width, agent.VisitPlan is null ? Palette.Ink3 : Palette.Ink);
                return;
            }

            Number(c, agent.Plan is null ? T("нет плана") : Amount(agent.Plan), x + cols[2].Width, top, cols[2].Width, agent.Plan is null ? Palette.Ink3 : Palette.Ink2);
            x += cols[2].Width;
            Number(c, agent.Fact is null ? "—" : Amount(agent.Fact), x + cols[3].Width, top, cols[3].Width, Palette.Ink, faces.SemiBold);
            x += cols[3].Width;
            Number(c, agent.Deviation is { } dev ? ReportFormat.Signed(dev, report.Metric) : "—", x + cols[4].Width, top, cols[4].Width,
                agent.Deviation is { } d ? DeviationColor(d) : Palette.Ink3, faces.Medium);
            x += cols[4].Width;
            Number(c, ReportFormat.Percent(agent.Share), x + cols[5].Width, top, cols[5].Width, ShareColor(agent.Share) ?? Palette.Ink3, faces.Bold);
            x += cols[5].Width;
            Number(c, $"{agent.VisitFact?.ToString() ?? "—"} / {agent.VisitPlan?.ToString() ?? "—"}", x + cols[6].Width, top, cols[6].Width, Palette.Ink2);
        }

        private void TotalRow(SKCanvas c, float y, string title, ReportTotals totals, bool strong)
        {
            using (var paint = Fill(strong ? Palette.Navy : Palette.AccentSoft))
            {
                c.DrawRoundRect(new SKRect(Left - 8, y + 4, Right + 8, y + TotalRowHeight - 4), 10, 10, paint);
            }

            var ink = strong ? SKColors.White : Palette.Ink;
            var ink2 = strong ? SKColors.White.WithAlpha(205) : Palette.Ink2;
            var cols = Columns();
            var top = y + 12;
            TextFit(c, title, Left, top, cols[0].Width + cols[1].Width - 10, 15, faces.SemiBold, ink);
            var x = Left + cols[0].Width + cols[1].Width;
            if (!report.HasFact)
            {
                Number(c, Amount(totals.Plan), x + cols[2].Width, top, cols[2].Width, ink, faces.Bold);
                x += cols[2].Width;
                Number(c, totals.VisitPlan?.ToString() ?? "—", x + cols[3].Width, top, cols[3].Width, ink, faces.Bold);
                return;
            }

            Number(c, Amount(totals.Plan), x + cols[2].Width, top, cols[2].Width, ink2, faces.SemiBold);
            x += cols[2].Width;
            Number(c, totals.Fact is null ? "—" : Amount(totals.Fact), x + cols[3].Width, top, cols[3].Width, ink, faces.Bold);
            x += cols[3].Width;
            var devColor = totals.Deviation is not { } dev ? ink2
                : !strong ? DeviationColor(dev)
                : dev >= 0 ? SKColor.Parse("#86EFAC")
                : report.Kind == TelegramReportKind.Midday ? SKColors.White.WithAlpha(170) : SKColor.Parse("#FCA5A5");
            Number(c, totals.Deviation is { } d ? ReportFormat.Signed(d, report.Metric) : "—", x + cols[4].Width, top, cols[4].Width, devColor, faces.SemiBold);
            x += cols[4].Width;
            Number(c, ReportFormat.Percent(totals.Share), x + cols[5].Width, top, cols[5].Width, strong ? SKColors.White : ShareColor(totals.Share) ?? Palette.Ink, faces.Bold);
            x += cols[5].Width;
            Number(c, $"{totals.VisitFact?.ToString() ?? "—"} / {totals.VisitPlan?.ToString() ?? "—"}", x + cols[6].Width, top, cols[6].Width, ink2, faces.SemiBold);
        }

        /// <summary>«Требуют внимания» / «Не выполнили план» и «Лучшие результаты» — рядом, по строке на агента.</summary>
        private Block? Panels()
        {
            var lists = new List<(string Title, string? Subtitle, IReadOnlyList<(string Name, string Value, SKColor Color)> Rows)>();
            if (report.Attention.Count > 0)
            {
                var (title, subtitle) = report.Kind == TelegramReportKind.Evening
                    ? (T("Не выполнили план"), (string?)null)
                    : (T("Требуют внимания"), DailyReportTexts.Format(_lang, "ниже {0}% дневного плана или нет работы", DailyReportMath.Percent(DailyReportMath.MiddayAttentionShare)));
                lists.Add((title, subtitle, report.Attention.Select(a => (SingleTeam ? a.Name : $"{a.Name} · {a.Team}", a.Reason, Palette.Warn)).ToList()));
            }

            if (report.Best.Count > 0)
            {
                lists.Add((T("Лучшие результаты"), null, report.Best.Select((b, i) => ($"{i + 1}. {b.Name}",
                    $"{ReportFormat.Percent(b.Share)} · {ReportFormat.Amount(b.Fact, report.Metric)} {Unit}", ShareColor(b.Share) ?? Palette.Ink2)).ToList()));
            }

            if (lists.Count == 0)
            {
                return null;
            }

            var rows = lists.Max(l => Math.Min(l.Rows.Count, MaxListItems) + (l.Rows.Count > MaxListItems ? 1 : 0));
            var height = CardPad + 18 + (lists.Any(l => l.Subtitle is not null) ? 16 : 0) + 6 + rows * 24 + CardPad - 6;
            return new Block("panels", height, (c, y) =>
            {
                var w = (Width - Pad * 2 - Gap * (lists.Count - 1)) / lists.Count;
                var x = Pad;
                foreach (var list in lists)
                {
                    var rect = new SKRect(x, y, x + w, y + height);
                    using (var fill = Fill(Palette.Card))
                    using (var border = Stroke(Palette.CardBorder, 1))
                    {
                        c.DrawRoundRect(rect, 16, 16, fill);
                        c.DrawRoundRect(rect, 16, 16, border);
                    }

                    var top = y + CardPad;
                    Label(c, list.Title, x + CardPad, top);
                    top += 18;
                    if (list.Subtitle is { } sub)
                    {
                        TextFit(c, sub, x + CardPad, top, w - CardPad * 2, 11, faces.Regular, Palette.Ink3);
                    }

                    top += lists.Any(l => l.Subtitle is not null) ? 22 : 6;
                    foreach (var row in list.Rows.Take(MaxListItems))
                    {
                        var valueWidth = Math.Min(Measure(row.Value, faces.SemiBold, 13.5f), w * 0.55f);
                        TextFit(c, row.Name, x + CardPad, top, w - CardPad * 2 - valueWidth - 12, 14, faces.Medium, Palette.Ink);
                        TextRightFit(c, row.Value, x + w - CardPad, top, valueWidth + 2, 13.5f, faces.SemiBold, row.Color);
                        top += 24;
                    }

                    if (list.Rows.Count > MaxListItems)
                    {
                        Text(c, DailyReportTexts.Format(_lang, "и ещё {0}", list.Rows.Count - MaxListItems), x + CardPad, top, 12.5f, faces.Regular, Palette.Ink3);
                    }

                    x += w + Gap;
                }
            }, Plain: true);
        }

        private Block Notes()
        {
            var lines = report.Notes.Select(n => Ellipsis(Clean(n), faces.Regular, 12, Width - Pad * 2)).ToList();
            return new Block("notes", lines.Count * 17 + 2, (c, y) =>
            {
                for (var i = 0; i < lines.Count; i++)
                {
                    Text(c, lines[i], Pad, y + i * 17, 12, faces.Regular, Palette.Ink2);
                }
            }, Plain: true);
        }

        // ---------- Страница ----------

        public byte[] DrawPage(List<Block> blocks, int index, int count)
        {
            var info = new SKImageInfo((int)(Width * Scale), (int)(Height * Scale), SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info) ?? throw new InvalidOperationException("Не удалось создать картинку отчёта.");
            var c = surface.Canvas;
            c.Scale(Scale);
            c.Clear(Palette.Background);
            Band(c, index, count);

            // Фон карточек: под всеми кусками одной группы на странице — одна карточка.
            var positions = new List<(Block Block, float Y)>();
            var cursor = ContentTop;
            string? previous = null;
            foreach (var block in blocks)
            {
                if (previous is not null && block.Group != previous)
                {
                    cursor += Gap;
                }

                positions.Add((block, cursor));
                cursor += block.Height;
                previous = block.Group;
            }

            foreach (var group in positions.Where(p => !p.Block.Plain && p.Block.Group != "kpi").GroupBy(p => p.Block.Group))
            {
                var first = group.First();
                var last = group.Last();
                var rect = new SKRect(Pad, first.Y, Width - Pad, last.Y + last.Block.Height);
                using var fill = Fill(Palette.Card);
                using var border = Stroke(Palette.CardBorder, 1);
                c.DrawRoundRect(rect, 16, 16, fill);
                c.DrawRoundRect(rect, 16, 16, border);
            }

            foreach (var (block, top) in positions)
            {
                block.Draw(c, top);
            }

            Footer(c, index, count);
            if (report.Demo)
            {
                Watermark(c);
            }

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        /// <summary>Шапка: отчёт и регион, супервайзер и РМ; справа — дата, время формирования и данных, страница.</summary>
        private void Band(SKCanvas c, int index, int count)
        {
            using (var paint = new SKPaint { IsAntialias = true })
            using (var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(Width, BandHeight), [Palette.Navy, Palette.Navy2], SKShaderTileMode.Clamp))
            {
                paint.Shader = shader;
                c.DrawRect(new SKRect(0, 0, Width, BandHeight), paint);
            }

            var rightWidth = 250f;
            Spaced(c, "ONEBASE", Pad, 14, 10.5f, faces.Bold, SKColors.White.WithAlpha(190), 2.3f);
            var title = DailyReportTexts.Title(report.Kind, _lang);
            using (var titleFont = Font(faces.Bold, 23))
            {
                var titleWidth = Math.Min(titleFont.MeasureText(title), 330);
                TextFit(c, title, Pad, 30, 330, 23, faces.Bold, SKColors.White);
                TextFit(c, $"·  {report.Region}", Pad + titleWidth + 10, 30, Width - Pad * 2 - rightWidth - titleWidth - 10, 23, faces.SemiBold, SKColors.White.WithAlpha(225));
            }

            var people = new List<string>();
            people.Add(SingleTeam ? $"{T("Супервайзер")}: {report.Teams[0].Supervisor}" : $"{T("Супервайзеров")}: {report.Supervisors}");
            if (report.Manager is { Length: > 0 } manager)
            {
                people.Add($"{T("РМ")}: {manager}");
            }

            TextFit(c, string.Join("   ·   ", people), Pad, 62, Width - Pad * 2 - rightWidth, 13.5f, faces.Medium, SKColors.White.WithAlpha(215));

            TextRight(c, report.Date.ToString("dd.MM.yyyy"), Width - Pad, 16, 18, faces.SemiBold, SKColors.White);
            var made = DailyReportTexts.Format(_lang, "Сформирован {0}", report.GeneratedAt.ToString("HH:mm"));
            if (report.DataAsOf is { } asOf)
            {
                made += " · " + DailyReportTexts.Format(_lang, "данные на {0}", asOf.ToString(asOf.Date == report.GeneratedAt.Date ? "HH:mm" : "dd.MM HH:mm"));
            }

            TextRightFit(c, made, Width - Pad, 42, rightWidth, 11.5f, faces.Regular, SKColors.White.WithAlpha(200));
            if (count > 1)
            {
                TextRight(c, DailyReportTexts.Format(_lang, "Стр. {0} из {1}", index + 1, count), Width - Pad, 62, 11.5f, faces.Medium, SKColors.White.WithAlpha(200));
            }

            if (Strip)
            {
                var demo = report.Demo;
                using (var paint = Fill(demo ? Palette.Test : Palette.AccentSoft))
                {
                    c.DrawRect(new SKRect(0, BandHeight, Width, BandHeight + StripHeight), paint);
                }

                var ink = demo ? Palette.TestInk : Palette.Accent;
                var label = demo ? T("ТЕСТОВЫЕ ДАННЫЕ") : T("ПРОВЕРКА НА РЕАЛЬНЫХ ДАННЫХ");
                Text(c, label, Pad, BandHeight + 6, 12, faces.Bold, ink);
                if (demo)
                {
                    var w = Measure(label, faces.Bold, 12);
                    TextFit(c, T("Имена и цифры — демонстрационные, не данные сотрудников"), Pad + w + 14, BandHeight + 7, Width - Pad * 2 - w - 14, 11, faces.Medium, ink);
                }
            }
        }

        private void Footer(SKCanvas c, int index, int count)
        {
            var y = Height - FooterHeight + 9;
            Text(c, $"OneBase · {report.GeneratedAt:dd.MM.yyyy HH:mm}", Pad, y, 10.5f, faces.Medium, Palette.Ink3);
            if (count > 1)
            {
                TextRight(c, DailyReportTexts.Format(_lang, "Стр. {0} из {1}", index + 1, count), Width - Pad, y, 10.5f, faces.Medium, Palette.Ink3);
            }
        }

        /// <summary>Демонстрационные данные — ещё и диагональная надпись по всей странице: обрезанный скриншот тоже видно.</summary>
        private void Watermark(SKCanvas c)
        {
            var text = T("ТЕСТОВЫЕ ДАННЫЕ");
            using var font = Font(faces.Bold, 40);
            using var paint = Fill(new SKColor(0xB4, 0x53, 0x09, 22));
            foreach (var x in new[] { Width * 0.27f, Width * 0.73f })
            {
                c.Save();
                c.Translate(x, Height * 0.58f);
                c.RotateDegrees(-20);
                c.DrawText(text, 0, 0, SKTextAlign.Center, font, paint);
                c.Restore();
            }
        }

        // ---------- Элементы ----------

        private void Label(SKCanvas c, string text, float x, float y) => Spaced(c, text.ToUpperInvariant(), x, y, 11, faces.SemiBold, Palette.Ink3, 0.6f);

        private void Bar(SKCanvas c, float x, float y, float width, decimal share)
        {
            using (var track = Fill(SKColor.Parse("#E8ECF3")))
            {
                c.DrawRoundRect(new SKRect(x, y, x + width, y + 6), 3, 3, track);
            }

            if (share > 0)
            {
                using var fill = Fill(ShareColor(share) ?? Palette.Accent);
                c.DrawRoundRect(new SKRect(x, y, x + Math.Max(6, (float)Math.Min(1m, share) * width), y + 6), 3, 3, fill);
            }
        }

        /// <summary>
        /// Цвет выполнения: вечером от 100% — хорошо, 90–99% — близко, 70–89% — отставание, ниже 70% — плохо. В обед день не кончился:
        /// отмечается только ниже порога внимания и выполненный план.
        /// </summary>
        private SKColor? ShareColor(decimal? share)
        {
            if (share is not { } s)
            {
                return null;
            }

            if (s >= 1)
            {
                return Palette.Good;
            }

            if (report.Kind == TelegramReportKind.Midday)
            {
                return s < DailyReportMath.MiddayAttentionShare ? Palette.Warn : null;
            }

            return s >= 0.9m ? Palette.Accent : s >= 0.7m ? Palette.Warn : Palette.Bad;
        }

        /// <summary>Отклонение: в обед день ещё идёт — минус ожидаем, он серый; вечером минус — красный. Перевыполнение — зелёное.</summary>
        private SKColor DeviationColor(decimal deviation) =>
            deviation >= 0 ? Palette.Good : report.Kind == TelegramReportKind.Midday ? Palette.Ink3 : Palette.Bad;

        private void Separator(SKCanvas c, float y)
        {
            using var paint = Stroke(Palette.Line, 1);
            c.DrawLine(Left - 8, y, Right + 8, y, paint);
        }

        private void Number(SKCanvas c, string text, float right, float top, float width, SKColor color, SKTypeface? face = null) =>
            TextRightFit(c, text, right, top, width - 8, 16, face ?? faces.Regular, color);

        // ---------- Текст ----------

        private static SKFont Font(SKTypeface face, float size) => new(face, size) { Edging = SKFontEdging.Antialias, Subpixel = true };

        private static SKPaint Fill(SKColor color) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };

        private static SKPaint Stroke(SKColor color, float width) => new() { Color = color, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = width };

        private static float TopBaseline(SKFont font, float top) => top - font.Metrics.Ascent * 0.92f;

        private float Measure(string text, SKTypeface face, float size)
        {
            using var font = Font(face, size);
            return font.MeasureText(Clean(text));
        }

        private void Text(SKCanvas c, string text, float x, float top, float size, SKTypeface face, SKColor color)
        {
            using var font = Font(face, size);
            using var paint = Fill(color);
            c.DrawText(Clean(text), x, TopBaseline(font, top), SKTextAlign.Left, font, paint);
        }

        private void TextRight(SKCanvas c, string text, float right, float top, float size, SKTypeface face, SKColor color)
        {
            using var font = Font(face, size);
            using var paint = Fill(color);
            c.DrawText(Clean(text), right, TopBaseline(font, top), SKTextAlign.Right, font, paint);
        }

        /// <summary>Текст в ширину: длинный — меньшим шрифтом (до 75%), дальше — с многоточием.</summary>
        private void TextFit(SKCanvas c, string text, float x, float top, float width, float size, SKTypeface face, SKColor color)
        {
            using var font = Fit(Clean(text), face, size, width, out var fitted);
            using var paint = Fill(color);
            c.DrawText(fitted, x, TopBaseline(font, top) + (size - font.Size) * 0.35f, SKTextAlign.Left, font, paint);
        }

        private void TextRightFit(SKCanvas c, string text, float right, float top, float width, float size, SKTypeface face, SKColor color)
        {
            using var font = Fit(Clean(text), face, size, width, out var fitted);
            using var paint = Fill(color);
            c.DrawText(fitted, right, TopBaseline(font, top) + (size - font.Size) * 0.35f, SKTextAlign.Right, font, paint);
        }

        private static SKFont Fit(string text, SKTypeface face, float size, float width, out string fitted)
        {
            var font = Font(face, size);
            while (font.MeasureText(text) > width && font.Size > size * 0.75f)
            {
                font.Size -= 0.5f;
            }

            fitted = font.MeasureText(text) <= width ? text : Ellipsis(text, font, width);
            return font;
        }

        private static string Ellipsis(string text, SKTypeface face, float size, float width)
        {
            using var font = Font(face, size);
            return font.MeasureText(text) <= width ? text : Ellipsis(text, font, width);
        }

        private static string Ellipsis(string text, SKFont font, float width)
        {
            var t = text.TrimEnd('…', ' ');
            while (t.Length > 1 && font.MeasureText(t + "…") > width)
            {
                t = t[..^1];
            }

            return t.TrimEnd() + "…";
        }

        /// <summary>Разрядка (ONEBASE, подписи блоков): Skia её не умеет — по буквам.</summary>
        private void Spaced(SKCanvas c, string text, float x, float top, float size, SKTypeface face, SKColor color, float spacing)
        {
            using var font = Font(face, size);
            using var paint = Fill(color);
            var baseline = TopBaseline(font, top);
            foreach (var ch in Clean(text))
            {
                var s = ch.ToString();
                c.DrawText(s, x, baseline, SKTextAlign.Left, font, paint);
                x += font.MeasureText(s) + spacing;
            }
        }

        /// <summary>Символы, которых нет в шрифте (эмодзи в именах Linko), убираются: вместо них были бы пустые квадраты.</summary>
        private string Clean(string text)
        {
            if (text.Length == 0)
            {
                return text;
            }

            var face = faces.Regular;
            var sb = new System.Text.StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsSurrogate(text[i]) && !char.IsSurrogatePair(text, i))
                {
                    continue; // половинка пары — испорченный символ
                }

                var cp = char.ConvertToUtf32(text, i);
                if (char.IsSurrogatePair(text, i))
                {
                    i++;
                }

                if (cp is '\n' or '\r' or '\t')
                {
                    sb.Append(' ');
                }
                else if (face.ContainsGlyph(cp))
                {
                    sb.Append(char.ConvertFromUtf32(cp));
                }
            }

            return sb.ToString();
        }
    }
}
