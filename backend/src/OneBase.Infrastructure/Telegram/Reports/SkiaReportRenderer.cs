using OneBase.Application.Telegram.Reports;
using OneBase.Domain.Telegram;
using SkiaSharp;

namespace OneBase.Infrastructure.Telegram.Reports;

/// <summary>
/// Картинки автоотчётов (PNG) на сервере, без браузера: SkiaSharp и шрифт Inter (кириллица и узбекская латиница — o', g', sh). Формат
/// 16:9 — 1920×1080, все страницы одного размера: шапка (отчёт, область, дата, срез данных), полоса роли (РМ — синяя, СВ — зелёная,
/// ТП — оранжевая), на первой странице — плитки KPI, затем таблицы (длинная — на нескольких страницах, с повтором шапки), «требуют
/// внимания и действия», замечания о данных. Ширина колонок — по содержимому; длинные названия переносятся на вторую строку.
/// </summary>
public sealed class SkiaReportRenderer : IReportRenderer
{
    public const float Width = 960;
    public const float Height = 540;
    public const float Scale = 2;

    private const float Pad = 22;
    private const float CardPad = 12;
    private const float Gap = 10;
    private const float BandHeight = 74;
    private const float AccentHeight = 5;
    private const float StripHeight = 22;
    private const float FooterHeight = 22;
    private const float KpiHeight = 62;
    private const float TitleHeight = 26;
    private const float CellFont = 12f;
    private const float HeaderFont = 9.2f;
    private const float RowHeight = 21;
    private const float TallRowHeight = 32;

    private static readonly Lazy<Typefaces> Faces = new(Typefaces.Load);

    public IReadOnlyList<byte[]> Render(ReportDocument document)
    {
        var layout = new Layout(document, Faces.Value, compact: false);
        var pages = layout.Paginate(layout.Blocks());
        if (pages.Count > 1)
        {
            // Чуть не влезло (карточка «Требуют внимания» на почти пустой второй странице) — строки пониже, если так страниц меньше.
            var compact = new Layout(document, Faces.Value, compact: true);
            var fewer = compact.Paginate(compact.Blocks());
            if (fewer.Count < pages.Count)
            {
                (layout, pages) = (compact, fewer);
            }
        }

        return pages.Select((page, i) => layout.DrawPage(page, i, pages.Count)).ToList();
    }

    /// <summary>Кусок страницы: Group — карточка (фон под всеми её кусками на странице); Continuation — что поставить первым на новой странице.</summary>
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
            using var stream = typeof(SkiaReportRenderer).Assembly.GetManifestResourceStream($"OneBase.Fonts.{file}")
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
        public static readonly SKColor Navy = SKColor.Parse("#0F1E3C");
        public static readonly SKColor Navy2 = SKColor.Parse("#1A3561");
        public static readonly SKColor Ink = SKColor.Parse("#0E1726");
        public static readonly SKColor Ink2 = SKColor.Parse("#4A5568");
        public static readonly SKColor Ink3 = SKColor.Parse("#8A94A6");
        public static readonly SKColor Line = SKColor.Parse("#EDF0F5");
        public static readonly SKColor Zebra = SKColor.Parse("#F8FAFC");
        public static readonly SKColor Good = SKColor.Parse("#15803D");
        public static readonly SKColor Warn = SKColor.Parse("#B45309");
        public static readonly SKColor Bad = SKColor.Parse("#DC2626");
        public static readonly SKColor BadSoft = SKColor.Parse("#FDECEC");
        public static readonly SKColor Strip = SKColor.Parse("#FFE7B3");
        public static readonly SKColor StripInk = SKColor.Parse("#5A3A00");
        public static readonly SKColor Test = SKColor.Parse("#E0ECFF");
        public static readonly SKColor TestInk = SKColor.Parse("#1D4ED8");

        /// <summary>Полоса роли (ТЗ, «Память проекта»): РМ #1F4E79, СВ #2E7D32, ТП #B45F06.</summary>
        public static SKColor Role(string role) => role switch
        {
            TelegramReportRoles.Rm => SKColor.Parse("#1F4E79"),
            TelegramReportRoles.Tp => SKColor.Parse("#B45F06"),
            _ => SKColor.Parse("#2E7D32"),
        };
    }

    private sealed class Layout(ReportDocument doc, Typefaces faces, bool compact)
    {
        private float RowH => compact ? 18 : RowHeight;
        private float TallRowH => compact ? 28 : TallRowHeight;
        private float BlockGap => compact ? 7 : Gap;
        private float KpiH => compact ? 52 : KpiHeight;
        private float TitleH => compact ? 22 : TitleHeight;

        private float Left => Pad + CardPad;
        private float Right => Width - Pad - CardPad;
        private float Inner => Right - Left;
        private bool HasStrip => doc.Incomplete || doc.Test;
        private float ContentTop => BandHeight + AccentHeight + (HasStrip ? StripHeight : 0) + 10;
        private float ContentBottom => Height - FooterHeight;

        private string T(string ru) => ReportTexts.Get(doc.Language, ru);

        // ---------- Раскладка ----------

        public List<Block> Blocks()
        {
            var blocks = new List<Block>();
            if (doc.Kpis.Count > 0)
            {
                blocks.Add(Kpis());
            }

            for (var i = 0; i < doc.Tables.Count; i++)
            {
                blocks.AddRange(Table(doc.Tables[i], $"table{i}"));
            }

            if (doc.Problems.Count > 0)
            {
                blocks.AddRange(Problems());
            }

            if (doc.Notes.Count > 0)
            {
                blocks.AddRange(Notes());
            }

            return blocks;
        }

        /// <summary>Блоки по страницам фиксированной высоты: блок не режется; таблица переносится по строкам с заголовком «продолжение».</summary>
        public List<List<Block>> Paginate(IReadOnlyList<Block> blocks)
        {
            var room = ContentBottom - ContentTop - 2;
            var pages = new List<List<Block>> { new() };
            var used = 0f;
            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                var gap = pages[^1].Count > 0 && pages[^1][^1].Group != block.Group ? BlockGap : 0;
                var need = gap + block.Height;
                for (var j = i; j < blocks.Count - 1 && blocks[j].KeepWithNext; j++)
                {
                    need += blocks[j + 1].Height;
                }

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

            return pages.Where(p => p.Count > 0).ToList();
        }

        private Block Kpis()
        {
            var tiles = doc.Kpis.Take(6).ToList();
            return new Block("kpi", KpiH, (c, y) =>
            {
                var w = (Width - Pad * 2 - Gap * (tiles.Count - 1)) / tiles.Count;
                var x = Pad;
                foreach (var tile in tiles)
                {
                    var rect = new SKRect(x, y, x + w, y + KpiH);
                    using (var fill = Fill(Palette.Card))
                    using (var border = Stroke(Palette.CardBorder, 1))
                    {
                        c.DrawRoundRect(rect, 12, 12, fill);
                        c.DrawRoundRect(rect, 12, 12, border);
                    }

                    var k = KpiH / KpiHeight;
                    TextFit(c, tile.Label, x + 12, y + 9 * k, w - 24, 10.5f, faces.Medium, Palette.Ink3);
                    TextFit(c, tile.Value, x + 12, y + 24 * k, w - 24, compact ? 18 : 20, faces.Bold, ToneColor(tile.Tone) ?? Palette.Ink);
                    if (tile.Hint is { } hint)
                    {
                        TextFit(c, hint, x + 12, y + 46 * k, w - 24, 10, faces.Regular, Palette.Ink2);
                    }

                    x += w + Gap;
                }
            }, Plain: true);
        }

        /// <summary>Ширины колонок — по самому длинному значению (шапка — в две строки), лишнее место — по весам; не влезает — ужимается пропорционально.</summary>
        private float[] Widths(ReportTable table)
        {
            var n = table.Columns.Count;
            var natural = new float[n];
            for (var i = 0; i < n; i++)
            {
                var col = table.Columns[i];
                var header = HeaderLines(col.Title, float.MaxValue).Max(l => Measure(l, faces.SemiBold, HeaderFont));
                var headerTwoLines = WrapMeasure(col.Title.ToUpperInvariant(), faces.SemiBold, HeaderFont);
                var cells = table.Rows.Select(r => i < r.Count ? r[i] : null).Append(table.Total is { } t && i < t.Count ? t[i] : null)
                    .Where(x => x is not null).Select(x => Measure(x!.Text, x.Bold ? faces.SemiBold : faces.Regular, CellFont)).DefaultIfEmpty(0).Max();
                natural[i] = Math.Max(Math.Min(header, headerTwoLines), col.Right ? cells : Math.Min(cells, 260)) + 12;
            }

            var total = natural.Sum();
            if (total <= Inner)
            {
                var weights = table.Columns.Select(c => c.Weight).ToArray();
                var extra = Inner - total;
                var sum = weights.Sum();
                return natural.Select((w, i) => w + extra * weights[i] / sum).ToArray();
            }

            var factor = Inner / total;
            return natural.Select(w => w * factor).ToArray();
        }

        private IEnumerable<Block> Table(ReportTable table, string group)
        {
            var widths = Widths(table);
            var headerHeight = HeaderHeight(table, widths);
            Block Head(bool continued) => new(group, CardPad / 2 + TitleH + headerHeight, (c, y) =>
            {
                var top = y + CardPad / 2;
                var title = continued ? $"{table.Title} · {T("продолжение")}" : table.Title;
                var hintWidth = table.Hint is null ? 0 : Math.Min(Measure(table.Hint, faces.Regular, 10), Inner * 0.45f);
                TextFit(c, title, Left, top + 5, Inner - hintWidth - 12, 13.5f, faces.SemiBold, Palette.Ink);
                if (table.Hint is { } hint)
                {
                    TextRightFit(c, hint, Right, top + 8, hintWidth, 10, faces.Regular, Palette.Ink3);
                }

                Header(c, table, widths, top + TitleH, headerHeight);
            }, KeepWithNext: true);

            var continuation = Head(true);
            yield return Head(false);
            for (var r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                var index = r;
                var height = RowNeedsTwoLines(table, row, widths) ? TallRowH : RowH;
                yield return new Block(group, height, (c, y) => Row(c, table, widths, row, y, height, index), Continuation: continuation);
            }

            if (table.Total is { } total)
            {
                yield return new Block(group, RowH + 8 + CardPad / 2, (c, y) => TotalRow(c, table, widths, total, y), Continuation: continuation);
            }
            else
            {
                yield return new Block(group, CardPad / 2, (_, _) => { }, Continuation: continuation);
            }
        }

        /// <summary>Текстовая колонка сразу после числовой — с отступом: иначе «1» и «ОК» слипаются в «1 ОК».</summary>
        private static float Inset(ReportTable table, int i) => i > 0 && !table.Columns[i].Right && table.Columns[i - 1].Right ? 10 : 0;

        private float HeaderHeight(ReportTable table, float[] widths) =>
            table.Columns.Select((col, i) => HeaderLines(col.Title, widths[i] - 6).Count).Max() > 1 ? compact ? 26 : 30 : 19;

        private void Header(SKCanvas c, ReportTable table, float[] widths, float top, float height)
        {
            var x = Left;
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var col = table.Columns[i];
                var lines = HeaderLines(col.Title, widths[i] - 6);
                var lineTop = top + (height - lines.Count * 11.5f) / 2 - 1;
                foreach (var line in lines)
                {
                    if (col.Right)
                    {
                        TextRightFit(c, line, x + widths[i] - 4, lineTop, widths[i] - 6, HeaderFont, faces.SemiBold, Palette.Ink3);
                    }
                    else
                    {
                        TextFit(c, line, x + Inset(table, i), lineTop, widths[i] - 6 - Inset(table, i), HeaderFont, faces.SemiBold, Palette.Ink3);
                    }

                    lineTop += 11.5f;
                }

                x += widths[i];
            }

            using var paint = Stroke(Palette.Line, 1);
            c.DrawLine(Left - 6, top + height, Right + 6, top + height, paint);
        }

        private bool RowNeedsTwoLines(ReportTable table, IReadOnlyList<ReportCell> row, float[] widths)
        {
            for (var i = 0; i < Math.Min(row.Count, table.Columns.Count); i++)
            {
                if (!table.Columns[i].Right && Measure(row[i].Text, row[i].Bold ? faces.SemiBold : faces.Regular, CellFont) * 0.8f > widths[i] - 8)
                {
                    return true;
                }
            }

            return false;
        }

        private void Row(SKCanvas c, ReportTable table, float[] widths, IReadOnlyList<ReportCell> row, float y, float height, int index)
        {
            if (index % 2 == 1)
            {
                using var zebra = Fill(Palette.Zebra);
                c.DrawRect(new SKRect(Left - 6, y, Right + 6, y + height), zebra);
            }

            var x = Left;
            for (var i = 0; i < Math.Min(row.Count, table.Columns.Count); i++)
            {
                Cell(c, row[i], table.Columns[i], x + Inset(table, i), y, widths[i] - Inset(table, i), height, onNavy: false);
                x += widths[i];
            }
        }

        private void TotalRow(SKCanvas c, ReportTable table, float[] widths, IReadOnlyList<ReportCell> total, float y)
        {
            using (var paint = Fill(Palette.Navy))
            {
                c.DrawRoundRect(new SKRect(Left - 6, y + 3, Right + 6, y + RowH + 5), 8, 8, paint);
            }

            var x = Left;
            for (var i = 0; i < Math.Min(total.Count, table.Columns.Count); i++)
            {
                Cell(c, total[i] with { Bold = true }, table.Columns[i], x + Inset(table, i), y + 4, widths[i] - Inset(table, i), RowH, onNavy: true);
                x += widths[i];
            }
        }

        private void Cell(SKCanvas c, ReportCell cell, ReportColumn col, float x, float y, float width, float height, bool onNavy)
        {
            var face = cell.Bold ? faces.SemiBold : faces.Regular;
            var color = onNavy ? NavyTone(cell.Tone) : ToneColor(cell.Tone) ?? Palette.Ink;
            if (!onNavy && cell.Tone == ReportTone.Bad && col.Right)
            {
                using var soft = Fill(Palette.BadSoft);
                c.DrawRoundRect(new SKRect(x + 2, y + 2, x + width - 2, y + height - 2), 4, 4, soft);
                face = faces.SemiBold;
            }

            if (col.Right)
            {
                TextRightFit(c, cell.Text, x + width - 4, y + (height - CellFont) / 2 - 1, width - 6, CellFont, face, color);
                return;
            }

            // Длинное название — в две строки (высокая строка), дальше — с многоточием.
            var lines = height > RowH ? Wrap(cell.Text, face, CellFont * 0.92f, width - 8, 2) : [cell.Text];
            var size = lines.Count > 1 ? CellFont * 0.92f : CellFont;
            var top = y + (height - lines.Count * (size + 2)) / 2;
            foreach (var line in lines)
            {
                TextFit(c, line, x, top, width - 8, size, face, color);
                top += size + 2;
            }
        }

        private IEnumerable<Block> Problems()
        {
            var lines = doc.Problems.SelectMany(p => Wrap("• " + p, faces.Regular, 11.5f, Inner, 3)).ToList();
            const string group = "problems";
            yield return new Block(group, CardPad / 2 + 20, (c, y) => Spaced(c, T("Требуют внимания и действия").ToUpperInvariant(), Left, y + CardPad / 2 + 4, 10, faces.SemiBold, Palette.Ink3, 0.5f),
                KeepWithNext: true);
            foreach (var line in lines)
            {
                yield return new Block(group, 17, (c, y) => Text(c, line, Left, y, 11.5f, faces.Medium, Palette.Ink));
            }

            yield return new Block(group, CardPad / 2, (_, _) => { });
        }

        private IEnumerable<Block> Notes()
        {
            var lines = doc.Notes.SelectMany(n => Wrap("· " + n, faces.Regular, 10, Width - Pad * 2, 2)).ToList();
            foreach (var line in lines)
            {
                yield return new Block("notes", 14, (c, y) => Text(c, line, Pad, y, 10, faces.Regular, Palette.Ink2), Plain: true);
            }
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

            var positions = new List<(Block Block, float Y)>();
            var cursor = ContentTop;
            string? previous = null;
            foreach (var block in blocks)
            {
                if (previous is not null && block.Group != previous)
                {
                    cursor += BlockGap;
                }

                positions.Add((block, cursor));
                cursor += block.Height;
                previous = block.Group;
            }

            foreach (var group in positions.Where(p => !p.Block.Plain).GroupBy(p => p.Block.Group))
            {
                var first = group.First();
                var last = group.Last();
                var rect = new SKRect(Pad, first.Y, Width - Pad, last.Y + last.Block.Height);
                using var fill = Fill(Palette.Card);
                using var border = Stroke(Palette.CardBorder, 1);
                c.DrawRoundRect(rect, 14, 14, fill);
                c.DrawRoundRect(rect, 14, 14, border);
            }

            foreach (var (block, top) in positions)
            {
                block.Draw(c, top);
            }

            Footer(c, index, count);
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        /// <summary>Шапка: ONEBASE · Candy Gold, отчёт и запуск, область и РМ; справа — дата (период), срез данных, страница; ниже — полоса роли.</summary>
        private void Band(SKCanvas c, int index, int count)
        {
            using (var paint = new SKPaint { IsAntialias = true })
            using (var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(Width, BandHeight), [Palette.Navy, Palette.Navy2], SKShaderTileMode.Clamp))
            {
                paint.Shader = shader;
                c.DrawRect(new SKRect(0, 0, Width, BandHeight), paint);
            }

            var role = Palette.Role(doc.Role);
            using (var accent = Fill(role))
            {
                c.DrawRect(new SKRect(0, BandHeight, Width, BandHeight + AccentHeight), accent);
            }

            const float rightWidth = 250;
            Spaced(c, "ONEBASE · CANDY GOLD", Pad, 11, 9.5f, faces.Bold, SKColors.White.WithAlpha(180), 1.8f);
            var roleLabel = T(doc.Role switch { TelegramReportRoles.Rm => "РМ", TelegramReportRoles.Tp => "ТП", _ => "СВ" });
            var chipWidth = Measure(roleLabel, faces.Bold, 11) + 16;
            using (var chip = Fill(role))
            {
                c.DrawRoundRect(new SKRect(Pad, 27, Pad + chipWidth, 45), 9, 9, chip);
            }

            Text(c, roleLabel, Pad + 8, 30.5f, 11, faces.Bold, SKColors.White);
            TextFit(c, doc.Title, Pad + chipWidth + 10, 25, Width - Pad * 2 - rightWidth - chipWidth - 10, 18, faces.Bold, SKColors.White);
            var scope = doc.Manager is { Length: > 0 } m && !doc.Scope.Contains(m) ? $"{doc.Scope}   ·   {T("РМ")}: {m}" : doc.Scope;
            TextFit(c, scope, Pad, 52, Width - Pad * 2 - rightWidth, 13, faces.Medium, SKColors.White.WithAlpha(225));

            TextRight(c, doc.Period, Width - Pad, 12, 16, faces.SemiBold, SKColors.White);
            var made = $"{T("Сформирован")} {doc.GeneratedAt:HH:mm}";
            if (doc.DataAsOf is { } asOf)
            {
                made += $" · {T("данные на")} {asOf.ToString(asOf.Date == doc.GeneratedAt.Date ? "HH:mm" : "dd.MM HH:mm")}";
            }

            TextRightFit(c, made, Width - Pad, 35, rightWidth, 10.5f, faces.Regular, SKColors.White.WithAlpha(200));
            if (count > 1)
            {
                TextRight(c, $"{T("стр.")} {index + 1} / {count}", Width - Pad, 52, 10.5f, faces.Medium, SKColors.White.WithAlpha(200));
            }

            if (HasStrip)
            {
                var test = doc.Test;
                using (var paint = Fill(test ? Palette.Test : Palette.Strip))
                {
                    c.DrawRect(new SKRect(0, BandHeight + AccentHeight, Width, BandHeight + AccentHeight + StripHeight), paint);
                }

                var label = test ? T("ТЕСТОВАЯ ОТПРАВКА") : T("НЕПОЛНЫЕ ДАННЫЕ");
                var ink = test ? Palette.TestInk : Palette.StripInk;
                Text(c, label, Pad, BandHeight + AccentHeight + 5, 10.5f, faces.Bold, ink);
                if (!test && doc.Notes.Count > 0)
                {
                    var w = Measure(label, faces.Bold, 10.5f);
                    TextFit(c, doc.Notes[0], Pad + w + 12, BandHeight + AccentHeight + 5.5f, Width - Pad * 2 - w - 12, 10, faces.Medium, ink);
                }
            }
        }

        private void Footer(SKCanvas c, int index, int count)
        {
            var y = Height - FooterHeight + 6;
            Text(c, $"OneBase · Candy Gold · {doc.GeneratedAt:dd.MM.yyyy HH:mm}", Pad, y, 9.5f, faces.Medium, Palette.Ink3);
            if (count > 1)
            {
                TextRight(c, $"{T("стр.")} {index + 1} / {count}", Width - Pad, y, 9.5f, faces.Medium, Palette.Ink3);
            }
        }

        private static SKColor? ToneColor(ReportTone tone) => tone switch
        {
            ReportTone.Good => Palette.Good,
            ReportTone.Warn => Palette.Warn,
            ReportTone.Bad => Palette.Bad,
            ReportTone.Muted => Palette.Ink3,
            _ => null,
        };

        private static SKColor NavyTone(ReportTone tone) => tone switch
        {
            ReportTone.Good => SKColor.Parse("#86EFAC"),
            ReportTone.Warn => SKColor.Parse("#FCD34D"),
            ReportTone.Bad => SKColor.Parse("#FCA5A5"),
            ReportTone.Muted => SKColors.White.WithAlpha(160),
            _ => SKColors.White,
        };

        // ---------- Текст ----------

        private List<string> HeaderLines(string title, float width) => Wrap(title.ToUpperInvariant(), faces.SemiBold, HeaderFont, width, 2);

        /// <summary>Ширина заголовка колонки, если разбить его на две строки поровну по словам.</summary>
        private float WrapMeasure(string text, SKTypeface face, float size)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2)
            {
                return Measure(text, face, size);
            }

            var best = float.MaxValue;
            for (var i = 1; i < words.Length; i++)
            {
                var a = Measure(string.Join(' ', words[..i]), face, size);
                var b = Measure(string.Join(' ', words[i..]), face, size);
                best = Math.Min(best, Math.Max(a, b));
            }

            return best;
        }

        /// <summary>Перенос по словам в maxLines строк; последнее — с многоточием.</summary>
        private List<string> Wrap(string text, SKTypeface face, float size, float width, int maxLines)
        {
            var clean = Clean(text);
            using var font = Font(face, size);
            if (font.MeasureText(clean) <= width)
            {
                return [clean];
            }

            var lines = new List<string>();
            var current = "";
            foreach (var word in clean.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (font.MeasureText(candidate) <= width || current.Length == 0)
                {
                    current = candidate;
                    continue;
                }

                lines.Add(current);
                current = word;
                if (lines.Count == maxLines - 1)
                {
                    break;
                }
            }

            var rest = string.Join(' ', clean.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(lines.Sum(l => l.Split(' ').Length)));
            lines.Add(font.MeasureText(rest) <= width ? rest : Ellipsis(rest, font, width));
            return lines.Where(l => l.Length > 0).Take(maxLines).ToList();
        }

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
                    continue;
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
