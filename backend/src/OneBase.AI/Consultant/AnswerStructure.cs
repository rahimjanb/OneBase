using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OneBase.AI.Agents;

namespace OneBase.AI.Consultant;

/// <summary>Сводка ответа для чипов и подвала: сколько отделов, фактов и проблем, рекомендаций; модель, время ответа, по какой день данные.</summary>
public sealed record AnswerMeta(int Departments, int Facts, int Recommendations, string? Model, long LatencyMs, string? DataAsOf);

/// <summary>Блок структурированного ответа; type — вид блока (summary, kpis, facts, deviations, recommendations, markdown).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SummaryBlock), "summary")]
[JsonDerivedType(typeof(KpisBlock), "kpis")]
[JsonDerivedType(typeof(FactsBlock), "facts")]
[JsonDerivedType(typeof(DeviationsBlock), "deviations")]
[JsonDerivedType(typeof(RecommendationsBlock), "recommendations")]
[JsonDerivedType(typeof(MarkdownBlock), "markdown")]
public abstract record AnswerBlock;

/// <summary>План и факт (и прогноз) одной единицы — для полосы выполнения в «Главном».</summary>
public sealed record AnswerProgress(string Label, decimal Fact, decimal Plan, decimal? Forecast, string Unit, string? ForecastLabel);

public sealed record SummaryBlock(string Text, AnswerProgress? Progress) : AnswerBlock;

/// <summary>Tone: good — показатель улучшился, bad — ухудшился, warn — ниже цели, neutral — без изменения.</summary>
public sealed record KpiItem(string Label, string Value, string? Delta, string Tone);

public sealed record KpisBlock(IReadOnlyList<KpiItem> Items) : AnswerBlock;

public sealed record FactDelta(string Value, string? Caption, string Tone);

public sealed record FactRow(string Label, string? Value, IReadOnlyList<FactDelta> Deltas);

public sealed record FactsBlock(string? Title, IReadOnlyList<FactRow> Rows) : AnswerBlock;

/// <summary>Severity: high — серьёзное отклонение (сильное падение, выпадение клиента, сбой), medium — остальное.</summary>
public sealed record DeviationItem(string Title, string Text, string Severity);

public sealed record DeviationsBlock(IReadOnlyList<DeviationItem> Items) : AnswerBlock;

/// <summary>Рекомендация: название, пояснение, ответственный, срок (yyyy-MM-dd) и отдел задач (код), если их можно определить.</summary>
public sealed record RecommendationItem(string Title, string? Text, string? Owner, string? DueDate, string? Department);

public sealed record RecommendationsBlock(IReadOnlyList<RecommendationItem> Items, int Total) : AnswerBlock;

public sealed record MarkdownBlock(string Text) : AnswerBlock;

/// <summary>
/// Ответ консультанта для экрана: Report = true — карточка отчёта из блоков; false — ответ показывается Markdown-ом как раньше (простые
/// ответы, приветствия, ответы без разделов). FollowUps — 2–4 уточняющих вопроса на языке ответа.
/// </summary>
public sealed record StructuredAnswer(AnswerMeta Meta, IReadOnlyList<AnswerBlock> Blocks, IReadOnlyList<string> FollowUps, bool Report);

/// <summary>
/// Структура ответа консультанта — постобработкой готового ответа, без промптов и вызовов модели: Markdown делится на разделы, разделы
/// «Главное», «Факты», «Отклонения/Проблемы», «Рекомендации» (по-русски и по-узбекски) превращаются в блоки, остальное («Анализ»,
/// «Прогноз», текст без раздела) остаётся Markdown-блоками в том же порядке — ни одна строка ответа не теряется. Цифры — только из текста
/// ответа и результатов AI-сотрудников. Цвет изменения решает сервер по смыслу показателя: рост выручки — хорошо, рост просрочки — плохо,
/// отставание от цели — предупреждение.
/// </summary>
public static partial class AnswerStructure
{
    /// <summary>Ответ короче — простой (приветствие, уточнение): без карточки отчёта.</summary>
    private const int MinReportLength = 160;

    private const int MaxKpis = 4;
    private const int MaxFollowUps = 4;

    private enum Kind
    {
        Lead,
        Summary,
        Facts,
        Deviations,
        Recommendations,
        Other,
    }

    private sealed record Section(Kind Kind, string? Heading, List<string> Lines);

    public static StructuredAnswer Build(string content, ConsultantDetails? details, long durationMs, DateTimeOffset createdAt)
    {
        var meta = Meta(details, durationMs);
        var text = (content ?? string.Empty).Replace("\r\n", "\n").Trim();
        var plain = new StructuredAnswer(meta, [new MarkdownBlock(text)], [], false);
        if (text.Length < MinReportLength)
        {
            return plain;
        }

        var sections = Sections(text);
        if (!sections.Any(s => s.Kind is Kind.Facts or Kind.Deviations or Kind.Recommendations))
        {
            return plain;
        }

        var blocks = new List<AnswerBlock>();
        var factRows = new List<FactRow>();
        string? summary = null;
        string? title = null;
        var summaryRest = new List<string>();

        // «Главное» — раздел с таким названием, иначе первый абзац текста до первого раздела (или под заголовком-названием ответа).
        var explicitSummary = sections.Any(s => s.Kind == Kind.Summary);
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var lead = !explicitSummary && i == 0 && section.Kind is Kind.Lead or Kind.Other;
            switch (section.Kind)
            {
                case Kind.Summary when summary is null:
                case Kind.Lead or Kind.Other when lead:
                {
                    var (first, rest) = FirstParagraph(section.Lines);
                    summary = first;
                    title = section.Kind == Kind.Other ? section.Heading : null;
                    summaryRest.AddRange(rest);
                    break;
                }

                case Kind.Facts:
                {
                    var (rows, rest) = Facts(section.Lines);
                    if (rows.Count > 0)
                    {
                        factRows.AddRange(rows);
                        blocks.Add(new FactsBlock(null, rows));
                    }

                    AddMarkdown(blocks, rows.Count > 0 ? null : section.Heading, rest);
                    break;
                }

                case Kind.Deviations:
                {
                    var (items, rest) = Items(section.Lines);
                    if (items.Count > 0)
                    {
                        blocks.Add(new DeviationsBlock(items.Select(i => new DeviationItem(i.Title, i.Text, Severity(i.Title + " " + i.Text))).ToList()));
                    }

                    AddMarkdown(blocks, items.Count > 0 ? null : section.Heading, rest);
                    break;
                }

                case Kind.Recommendations:
                {
                    var (items, rest) = Items(section.Lines);
                    if (items.Count > 0)
                    {
                        var department = SingleDepartment(details);
                        var recommendations = items.Select(i => Recommendation(i.Title, i.Text, department, createdAt)).ToList();
                        blocks.Add(new RecommendationsBlock(recommendations, recommendations.Count));
                    }

                    AddMarkdown(blocks, items.Count > 0 ? null : section.Heading, rest);
                    break;
                }

                default:
                    AddMarkdown(blocks, section.Heading, section.Lines);
                    break;
            }
        }

        var head = new List<AnswerBlock>();
        if (title is not null)
        {
            head.Add(new MarkdownBlock(title));
        }

        if (summary is not null)
        {
            head.Add(new SummaryBlock(summary, Progress(factRows, details)));
        }

        if (summaryRest.Any(l => l.Trim().Length > 0))
        {
            head.Add(new MarkdownBlock(string.Join('\n', summaryRest).Trim()));
        }

        var kpis = Kpis(factRows, details);
        if (kpis.Count >= 2)
        {
            head.Add(new KpisBlock(kpis));
        }

        blocks.InsertRange(0, head);
        var language = AnswerLanguages.Of(text);
        return new StructuredAnswer(meta, blocks, FollowUps(blocks, details, language), true);
    }

    /// <summary>
    /// Текст для «Озвучить» у отчёта: «Главное» и рекомендации (так задумано ТЗ: не зачитывать таблицы цифр). Простой ответ — целиком.
    /// </summary>
    public static string SpeechText(StructuredAnswer answer, string content)
    {
        if (!answer.Report)
        {
            return content;
        }

        var sb = new StringBuilder();
        if (answer.Blocks.OfType<SummaryBlock>().FirstOrDefault() is { } summary)
        {
            sb.AppendLine(summary.Text);
        }

        var items = answer.Blocks.OfType<RecommendationsBlock>().SelectMany(b => b.Items).ToList();
        if (items.Count > 0)
        {
            string[] heading = ["Что сделать в первую очередь", "Birinchi navbatda nima qilish kerak", "Биринчи навбатда нима қилиш керак", "What to do first"];
            var lang = AnswerLanguages.Of(content) switch
            {
                AnswerLanguage.UzLatn => 1,
                AnswerLanguage.UzCyrl => 2,
                AnswerLanguage.Other => 3,
                _ => 0,
            };
            sb.AppendLine();
            sb.AppendLine($"{heading[lang]}:");
            for (var i = 0; i < items.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {items[i].Title}{(items[i].Text is { Length: > 0 } t ? $". {t}" : "")}.");
            }
        }

        return sb.Length == 0 ? content : sb.ToString().Trim();
    }

    // ---------- Разделы ----------

    /// <summary>Разделы ответа: заголовок Markdown (#…) или строка из одного полужирного названия («**Факты**», «**Рекомендации:**»).</summary>
    private static List<Section> Sections(string text)
    {
        var sections = new List<Section> { new(Kind.Lead, null, []) };
        foreach (var line in text.Split('\n'))
        {
            if (HeadingOf(line) is { } heading)
            {
                var kind = KindOf(heading);
                sections.Add(new Section(kind == Kind.Lead ? Kind.Other : kind, line.Trim(), []));
                continue;
            }

            sections[^1].Lines.Add(line);
        }

        return sections.Where(s => s.Heading is not null || s.Lines.Any(l => l.Trim().Length > 0)).ToList();
    }

    private static string? HeadingOf(string line)
    {
        if (Heading().Match(line) is { Success: true } h)
        {
            return h.Groups["title"].Value;
        }

        return BoldHeading().Match(line) is { Success: true } b ? b.Groups["title"].Value : null;
    }

    private static Kind KindOf(string heading)
    {
        var h = Normalize(heading);
        if (SummaryTitle().IsMatch(h))
        {
            return Kind.Summary;
        }

        if (RecommendationsTitle().IsMatch(h))
        {
            return Kind.Recommendations;
        }

        if (DeviationsTitle().IsMatch(h))
        {
            return Kind.Deviations;
        }

        return FactsTitle().IsMatch(h) ? Kind.Facts : Kind.Other;
    }

    /// <summary>Название раздела без разметки, эмодзи, нумерации и двоеточия, строчными.</summary>
    private static string Normalize(string heading)
    {
        var sb = new StringBuilder();
        foreach (var c in heading.ToLowerInvariant())
        {
            sb.Append(char.IsLetterOrDigit(c) || c is '\'' or 'ʻ' or '’' ? c : ' ');
        }

        return Spaces().Replace(LeadingNumber().Replace(sb.ToString(), " "), " ").Trim().Replace('ʻ', '\'').Replace('’', '\'');
    }

    private static (string? First, List<string> Others) FirstParagraph(List<string> lines)
    {
        var start = lines.FindIndex(l => l.Trim().Length > 0);
        if (start < 0 || IsBullet(lines[start]) || lines[start].TrimStart().StartsWith('|'))
        {
            return (null, lines);
        }

        var end = start;
        while (end < lines.Count && lines[end].Trim().Length > 0 && !IsBullet(lines[end]) && !lines[end].TrimStart().StartsWith('|'))
        {
            end++;
        }

        var first = string.Join(' ', lines.Skip(start).Take(end - start).Select(l => l.Trim()));
        return (first, lines.Skip(end).ToList());
    }

    private static void AddMarkdown(List<AnswerBlock> blocks, string? heading, IEnumerable<string> lines)
    {
        var body = string.Join('\n', lines).Trim();
        if (body.Length == 0 && heading is null)
        {
            return;
        }

        var text = heading is null ? body : body.Length == 0 ? string.Empty : $"{heading}\n\n{body}";
        if (text.Length > 0)
        {
            blocks.Add(new MarkdownBlock(text));
        }
    }

    // ---------- Факты ----------

    /// <summary>Строки фактов из таблицы «показатель | значение | изменения…» или из списка «Показатель: значение (−2,3% к …)»; остальное — rest.</summary>
    private static (List<FactRow> Rows, List<string> Others) Facts(List<string> lines)
    {
        var rows = new List<FactRow>();
        var rest = new List<string>();
        var table = new List<string>();

        void FlushTable()
        {
            if (table.Count == 0)
            {
                return;
            }

            if (TableFacts(table) is { } parsed)
            {
                rows.AddRange(parsed);
            }
            else
            {
                rest.AddRange(table);
            }

            table.Clear();
        }

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith('|'))
            {
                table.Add(line);
                continue;
            }

            FlushTable();
            if (Bullet().Match(line) is { Success: true } b && b.Groups["indent"].Length < 2 && FactOf(b.Groups["text"].Value) is { } row)
            {
                rows.Add(row);
            }
            else
            {
                rest.Add(line);
            }
        }

        FlushTable();
        return (rows, rest);
    }

    private static FactRow? FactOf(string item)
    {
        var (label, value) = SplitLabel(item);
        if (label is null || value is null || label.Length > 80)
        {
            return null;
        }

        var (clean, deltas) = Deltas(value, label);
        return clean.Length is 0 or > 90 && deltas.Count == 0 ? null : new FactRow(label, clean.Length == 0 ? null : clean, deltas);
    }

    /// <summary>Таблица фактов: первая колонка — показатель, одна колонка значения и любые колонки изменений. Иначе (сравнительная таблица) — null.</summary>
    private static List<FactRow>? TableFacts(List<string> lines)
    {
        var cells = lines.Select(Cells).ToList();
        if (cells.Count < 3 || !cells[1].All(c => TableRule().IsMatch(c)))
        {
            return null;
        }

        var header = cells[0];
        var body = cells.Skip(2).Where(r => r.Count > 0).ToList();
        if (header.Count < 2 || body.Any(r => r.Count != header.Count))
        {
            return null;
        }

        var deltaColumns = Enumerable.Range(1, header.Count - 1)
            .Where(i => body.All(r => r[i].Length == 0 || WholeDelta().IsMatch(r[i])))
            .ToHashSet();
        var valueColumns = Enumerable.Range(1, header.Count - 1).Where(i => !deltaColumns.Contains(i)).ToList();
        if (valueColumns.Count != 1)
        {
            return null;
        }

        var rows = new List<FactRow>();
        foreach (var r in body)
        {
            var label = Strip(r[0]);
            var (value, deltas) = Deltas(r[valueColumns[0]], label);
            foreach (var i in deltaColumns.Order())
            {
                if (r[i].Length > 0)
                {
                    var caption = HeaderCaption(header[i]);
                    deltas.AddRange(Deltas(r[i], label).Deltas.Select(d => d with { Caption = d.Caption ?? caption, Tone = Tone(d.Value, d.Caption ?? caption, label) }));
                }
            }

            rows.Add(new FactRow(label, value.Length == 0 ? null : value, deltas));
        }

        return rows;
    }

    private static List<string> Cells(string line)
    {
        var t = line.Trim();
        if (t.StartsWith('|'))
        {
            t = t[1..];
        }

        if (t.EndsWith('|'))
        {
            t = t[..^1];
        }

        return t.Split('|').Select(c => c.Trim()).ToList();
    }

    /// <summary>Подпись колонки изменения: «Изменение к сентябрю» → «к сентябрю»; «Δ» и «Изменение» без базы — без подписи.</summary>
    private static string? HeaderCaption(string header)
    {
        var h = Strip(header);
        if (CaptionStart().Match(h) is { Success: true } m)
        {
            return h[m.Index..].Trim();
        }

        return h.Length == 0 || h is "Δ" or "∆" || h.StartsWith("Изменен", StringComparison.OrdinalIgnoreCase) || h.StartsWith("Динамик", StringComparison.OrdinalIgnoreCase)
            ? null
            : h.ToLowerInvariant();
    }

    /// <summary>Изменения в тексте значения (−2,3% к полному сентябрю, +5,2 п.п. …) — отдельно; остаток — значение без пустых скобок.</summary>
    private static (string Value, List<FactDelta> Deltas) Deltas(string text, string label)
    {
        // Изменение относится к значению, только если стоит сразу за числом, в скобках или через запятую. «Самарканд +12%, Бухара +8%» — изменения
        // разных частей: их не отрывают от названий, значение остаётся текстом целиком.
        var matches = Delta().Matches(text);
        if (matches.Count == 0 || matches.Any(m => !AttachedDelta().IsMatch(text[..m.Index])))
        {
            return (Spaces().Replace(Strip(text), " ").Trim(), []);
        }

        var deltas = new List<FactDelta>();
        var value = Delta().Replace(text, m =>
        {
            var number = m.Groups["sign"].Value is "+" ? "+" : "−";
            var unit = m.Groups["unit"].Value;
            var formatted = $"{number}{m.Groups["num"].Value.Trim()}{(unit.StartsWith('%') ? "%" : $" {unit}")}";
            var caption = m.Groups["cap"].Success ? Strip(m.Groups["cap"].Value).Trim() : null;
            deltas.Add(new FactDelta(formatted, string.IsNullOrEmpty(caption) ? null : caption, Tone(formatted, caption, label)));
            return string.Empty;
        });
        value = Strip(EmptyParens().Replace(value, string.Empty));
        value = value.Trim(' ', ',', ';', '—', '–', '-', ':').Replace("( ", "(").Replace(" )", ")");
        return (Spaces().Replace(value, " "), deltas);
    }

    /// <summary>
    /// Цвет изменения: рост показателя, где больше — хуже (просрочка, долг, возвраты, опоздания, отсутствия, дефицит, расходы), — плохо;
    /// отставание от цели или плана — предупреждение; иначе рост — хорошо, падение — плохо; ноль — нейтрально.
    /// </summary>
    internal static string Tone(string delta, string? caption, string label)
    {
        var number = Number().Match(delta);
        if (number.Success && decimal.TryParse(number.Value.Replace(',', '.').Replace(" ", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) && n == 0)
        {
            return "neutral";
        }

        var negative = delta.StartsWith('−') || delta.StartsWith('-') || delta.StartsWith('–');
        if (negative && caption is not null && TargetCaption().IsMatch(caption))
        {
            return "warn";
        }

        var worse = InverseMetric().IsMatch(label);
        return negative == worse ? "good" : "bad";
    }

    // ---------- Плитки и прогресс ----------

    /// <summary>До 4 плиток: главные показатели (выручка, объём, АКБ, страйк, выполнение…) из фактов; без фактов — показатели AI-сотрудников.</summary>
    private static List<KpiItem> Kpis(List<FactRow> facts, ConsultantDetails? details)
    {
        var candidates = facts.Where(r => r.Value is { Length: > 0 and <= 28 } v && v.Any(char.IsDigit)).ToList();
        if (candidates.Count >= 2)
        {
            var ordered = candidates.Where(r => KpiMetric().IsMatch(r.Label)).Concat(candidates.Where(r => !KpiMetric().IsMatch(r.Label))).Take(MaxKpis);
            return ordered.Select(r => new KpiItem(r.Label, Compact(r.Value!), r.Deltas.FirstOrDefault() is { } d ? $"{d.Value}{(d.Caption is null ? "" : $" {d.Caption}")}" : null,
                r.Deltas.FirstOrDefault()?.Tone ?? "neutral")).ToList();
        }

        return Metrics(details).Take(MaxKpis).Select(m => new KpiItem(m.Name, Compact(m.Unit is null ? m.Value : $"{m.Value} {m.Unit}"), null, "neutral")).ToList();
    }

    private static IEnumerable<Metric> Metrics(ConsultantDetails? details) =>
        details?.Agents.Where(a => a.Status is "completed" or "partial").SelectMany(a => a.Metrics) ?? [];

    /// <summary>Полоса «Главного»: план и факт (и прогноз) одной единицы из фактов ответа или показателей сотрудников.</summary>
    private static AnswerProgress? Progress(List<FactRow> facts, ConsultantDetails? details)
    {
        var candidates = facts.Where(r => r.Value is not null).Select(r => (Label: r.Label, Value: r.Value!))
            .Concat(Metrics(details).Select(m => (Label: m.Name, Value: m.Unit is null ? m.Value : $"{m.Value} {m.Unit}")))
            .Select(c => (c.Label, Parsed: Amount(c.Value)))
            .Where(c => c.Parsed is { Unit.Length: > 0 } p && p.Unit != "%")
            .ToList();
        var plan = candidates.FirstOrDefault(c => PlanLabel().IsMatch(c.Label) && !RateLabel().IsMatch(c.Label));
        if (plan.Parsed is not { } p || p.Value <= 0)
        {
            return null;
        }

        var fact = candidates.FirstOrDefault(c => FactLabel().IsMatch(c.Label) && !ForecastLabel().IsMatch(c.Label) && !PlanLabel().IsMatch(c.Label) && c.Parsed!.Value.Unit == p.Unit);
        if (fact.Parsed is not { } f)
        {
            return null;
        }

        var forecast = candidates.FirstOrDefault(c => ForecastLabel().IsMatch(c.Label) && c.Parsed!.Value.Unit == p.Unit);
        return new AnswerProgress(fact.Label, f.Value, p.Value, forecast.Parsed?.Value, p.Unit, forecast.Parsed is null ? null : forecast.Label);
    }

    /// <summary>Число с единицей: «485 645 кг» → (485645, «кг»); «13 446 260 699 сум» → (13446260699, «сум»).</summary>
    internal static (decimal Value, string Unit)? Amount(string text)
    {
        var m = AmountPattern().Match(text);
        if (!m.Success || !decimal.TryParse(SpaceChars().Replace(m.Groups["num"].Value, string.Empty).Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return (value, m.Groups["unit"].Value.Trim().ToLowerInvariant());
    }

    /// <summary>Большие суммы короче: «13 446 260 699 сум» → «13,45 млрд сум», «558 900 000 сум» → «558,9 млн сум».</summary>
    internal static string Compact(string value)
    {
        if (Amount(value) is not { } a || a.Value < 10_000_000 || AmountPattern().Match(value) is not { Success: true } m || m.Index > 0)
        {
            return value;
        }

        var uz = UzLetters().IsMatch(value);
        var (scaled, word) = a.Value >= 1_000_000_000 ? (a.Value / 1_000_000_000, uz ? "mlrd" : "млрд") : (a.Value / 1_000_000, uz ? "mln" : "млн");
        var rounded = Math.Round(scaled, scaled >= 100 ? 1 : 2);
        var number = rounded.ToString(rounded == Math.Floor(rounded) ? "0" : "0.##", CultureInfo.InvariantCulture).Replace('.', ',');
        return $"{number} {word}{(a.Unit.Length > 0 ? $" {a.Unit}" : "")}";
    }

    // ---------- Отклонения и рекомендации ----------

    /// <summary>Пункты списка: «**Название** — пояснение», «Название — пояснение», «Название: пояснение»; вложенные пункты — к пояснению.</summary>
    private static (List<(string Title, string Text)> Items, List<string> Others) Items(List<string> lines)
    {
        var items = new List<(string Title, string Text)>();
        var rest = new List<string>();
        foreach (var line in lines)
        {
            if (Bullet().Match(line) is not { Success: true } b)
            {
                if (line.Trim().Length > 0 && items.Count > 0 && rest.Count == 0 && line.StartsWith("  "))
                {
                    items[^1] = (items[^1].Title, $"{items[^1].Text} {line.Trim()}".Trim());
                }
                else
                {
                    rest.Add(line);
                }

                continue;
            }

            var item = b.Groups["text"].Value.Trim();
            if (b.Groups["indent"].Length >= 2 && items.Count > 0)
            {
                items[^1] = (items[^1].Title, $"{items[^1].Text}{(items[^1].Text.Length > 0 ? "; " : "")}{Strip(item)}");
                continue;
            }

            var (title, text) = SplitLabel(item);
            items.Add(title is null ? (Strip(item), string.Empty) : (title, Strip(text ?? string.Empty)));
        }

        return (items, rest);
    }

    private static string Severity(string text)
    {
        foreach (Match m in Delta().Matches(text))
        {
            if (m.Groups["sign"].Value is not "+" && decimal.TryParse(SpaceChars().Replace(m.Groups["num"].Value, string.Empty).Replace(',', '.'), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var n) && n >= 10)
            {
                return "high";
            }
        }

        return SevereWords().IsMatch(text) ? "high" : "medium";
    }

    private static RecommendationItem Recommendation(string title, string text, string? department, DateTimeOffset createdAt)
    {
        string? owner = null;
        var body = OwnerPattern().Replace(text, m =>
        {
            owner = m.Groups["owner"].Value.Trim().TrimEnd('.');
            return string.Empty;
        }).Trim().Trim(';', ',').Trim();

        string? due = null;
        if (DuePattern().Match($"{title} {text}") is { Success: true } d
            && int.TryParse(d.Groups["d"].Value, out var day) && int.TryParse(d.Groups["m"].Value, out var month) && month is >= 1 and <= 12 && day is >= 1 and <= 31)
        {
            var year = d.Groups["y"].Success ? int.Parse(d.Groups["y"].Value.Length == 2 ? "20" + d.Groups["y"].Value : d.Groups["y"].Value, CultureInfo.InvariantCulture) : createdAt.Year;
            if (day <= DateTime.DaysInMonth(year, month))
            {
                var date = new DateOnly(year, month, day);
                // «до 5.11» в конце октября — этого года; в декабре «до 10.01» — следующего.
                if (!d.Groups["y"].Success && date < DateOnly.FromDateTime(createdAt.UtcDateTime).AddDays(-31))
                {
                    date = date.AddYears(1);
                }

                due = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
        }

        return new RecommendationItem(title, body.Length == 0 ? null : body, owner, due, DepartmentOf(owner) ?? department);
    }

    /// <summary>Отдел задач по ответственному («РОП», «супервайзер» — продажи; «бухгалтерия» — финансы…).</summary>
    private static string? DepartmentOf(string? owner) => owner is null ? null : Normalize(owner) switch
    {
        var o when SalesOwner().IsMatch(o) => "sales",
        var o when o.Contains("финанс") || o.Contains("бухгалт") || o.Contains("moliya") => "finance",
        var o when o.Contains("маркетинг") || o.Contains("marketing") => "marketing",
        var o when o.Contains("hr") || o.Contains("кадр") || o.Contains("персонал") => "hr",
        var o when o.Contains("производ") || o.Contains("цех") || o.Contains("ishlab") => "production",
        var o when o.Contains("снабжен") || o.Contains("закуп") || o.Contains("склад") || o.Contains("ta'minot") => "supply",
        _ => null,
    };

    /// <summary>Отдел, если ответ дал один AI-сотрудник отдела.</summary>
    private static string? SingleDepartment(ConsultantDetails? details)
    {
        var departments = details?.Agents.Where(a => a.Status is "completed" or "partial")
            .Select(a => AgentDefaults.Find(a.Agent)?.DepartmentCode)
            .Where(c => c is not null)
            .Distinct()
            .ToList();
        return departments is { Count: 1 } ? departments[0] : null;
    }

    // ---------- Сводка и уточняющие вопросы ----------

    private static AnswerMeta Meta(ConsultantDetails? details, long durationMs)
    {
        var agents = details?.Agents.Where(a => a.Status is "completed" or "partial" or "no_data").ToList() ?? [];
        string? dataAsOf = null;
        foreach (var s in details?.Sources ?? [])
        {
            foreach (Match m in DatePattern().Matches($"{s.Title} {s.Period}"))
            {
                if (DateOnly.TryParseExact(m.Value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                {
                    var iso = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    dataAsOf = dataAsOf is null || string.CompareOrdinal(iso, dataAsOf) > 0 ? iso : dataAsOf;
                }
            }
        }

        return new AnswerMeta(agents.Count, agents.Sum(a => a.Findings.Count + a.Problems.Count), agents.Sum(a => a.Recommendations.Count), details?.Model, durationMs, dataAsOf);
    }

    /// <summary>Уточняющие вопросы на языке ответа: по серьёзным отклонениям и по отделам, ответившим на вопрос. Без вызова модели.</summary>
    private static List<string> FollowUps(List<AnswerBlock> blocks, ConsultantDetails? details, AnswerLanguage language)
    {
        var lang = language switch
        {
            AnswerLanguage.UzLatn => 1,
            AnswerLanguage.UzCyrl => 2,
            AnswerLanguage.Other => 3,
            _ => 0,
        };
        string[] more = ["Подробнее: {0}", "Batafsil: {0}", "Батафсил: {0}", "More on {0}"];
        var result = new List<string>();
        var deviations = blocks.OfType<DeviationsBlock>().SelectMany(b => b.Items).OrderBy(i => i.Severity == "high" ? 0 : 1).Take(2);
        foreach (var d in deviations.Where(d => d.Title.Length is > 0 and <= 48))
        {
            result.Add(string.Format(CultureInfo.InvariantCulture, more[lang], d.Title));
        }

        var departments = details?.Agents.Where(a => a.Status is "completed" or "partial").Select(a => a.Agent).Distinct() ?? [];
        foreach (var agent in departments)
        {
            if (DepartmentQuestions.TryGetValue(agent, out var questions))
            {
                result.AddRange(questions.Select(q => q[lang]));
            }
        }

        return result.Distinct().Take(MaxFollowUps).ToList();
    }

    private static readonly Dictionary<string, string[][]> DepartmentQuestions = new()
    {
        ["sales"] =
        [
            ["Покажи по регионам", "Hududlar bo'yicha ko'rsat", "Ҳудудлар бўйича кўрсат", "Show by region"],
            ["Сравни с прошлым годом", "O'tgan yil bilan solishtir", "Ўтган йил билан солиштир", "Compare with last year"],
        ],
        ["finance"] =
        [
            ["Покажи дебиторку по клиентам", "Debitorlikni mijozlar bo'yicha ko'rsat", "Дебиторликни мижозлар бўйича кўрсат", "Show receivables by client"],
            ["Сравни с прошлым месяцем", "O'tgan oy bilan solishtir", "Ўтган ой билан солиштир", "Compare with last month"],
        ],
        ["supply"] =
        [
            ["Где заканчиваются остатки?", "Qayerda qoldiq tugayapti?", "Қаерда қолдиқ тугаяпти?", "Where is stock running out?"],
        ],
        ["hr"] =
        [
            ["Кто чаще всех опаздывает?", "Kim eng ko'p kechikadi?", "Ким энг кўп кечикади?", "Who is late most often?"],
        ],
        ["marketing"] =
        [
            ["Какие акции работают лучше всего?", "Qaysi aksiyalar eng yaxshi ishlayapti?", "Қайси акциялар энг яхши ишлаяпти?", "Which promotions work best?"],
        ],
        ["production"] =
        [
            ["Как выполняется план производства?", "Ishlab chiqarish rejasi qanday bajarilyapti?", "Ишлаб чиқариш режаси қандай бажариляпти?", "How is the production plan going?"],
        ],
    };

    // ---------- Общее ----------

    /// <summary>«**Название** — текст», «Название — текст», «Название: текст» → (название, текст); иначе (null, null).</summary>
    private static (string? Label, string? Value) SplitLabel(string item)
    {
        if (BoldLead().Match(item) is { Success: true } b)
        {
            var rest = b.Groups["rest"].Value.Trim();
            return (Strip(b.Groups["bold"].Value).TrimEnd(':').Trim(), rest.Length == 0 ? null : rest);
        }

        if (DashSplit().Match(item) is { Success: true } d)
        {
            return (Strip(d.Groups["label"].Value), d.Groups["value"].Value.Trim());
        }

        return ColonSplit().Match(item) is { Success: true } c ? (Strip(c.Groups["label"].Value), c.Groups["value"].Value.Trim()) : (null, null);
    }

    private static bool IsBullet(string line) => Bullet().IsMatch(line);

    /// <summary>Текст без разметки выделения: **, __, `, *.</summary>
    private static string Strip(string text) => Emphasis().Replace(text, string.Empty).Trim();

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+(?<title>.+?)\s*#*\s*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^\s*(?:\*\*|__)(?<title>[^*_\n]{2,60}?)(?:\*\*|__)\s*:?\s*$")]
    private static partial Regex BoldHeading();

    [GeneratedRegex(@"^(?<indent>\s*)(?:[-*•]|\d{1,2}[.)])\s+(?<text>.+)$")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^\s*:?-{2,}:?\s*$")]
    private static partial Regex TableRule();

    [GeneratedRegex(@"^(?:главное|итог\w*|кратко|коротко|вывод\w*|резюме|суть|основное|asosiy\w*|xulosa\w*|qisqacha|асосий\w*|хулоса\w*|қисқача|summary|key takeaways?|bottom line)\b")]
    private static partial Regex SummaryTitle();

    [GeneratedRegex(@"^(?:факты|ключевые (?:факты|цифры|показатели)|цифры|показатели|основные (?:цифры|показатели)|главные цифры|данные|faktlar|asosiy (?:raqamlar|ko'rsatkichlar)|raqamlar|ko'rsatkichlar|фактлар|рақамлар|кўрсаткичлар|facts|key (?:facts|figures|metrics)|figures)\b")]
    private static partial Regex FactsTitle();

    [GeneratedRegex(@"^(?:ключевые )?(?:отклонени\w*|проблем\w*|риск\w*|что тянет вниз|узкие места|muammo\w*|chetlanish\w*|xavf\w*|муаммо\w*|четланиш\w*|хавф\w*|problems?|issues|risks?|deviations?)\b")]
    private static partial Regex DeviationsTitle();

    [GeneratedRegex(@"^(?:рекомендац\w*|что (?:сделать|делать)\w*|действия|следующие шаги|первоочередные (?:действия|шаги)|tavsiya\w*|nima qilish kerak|тавсия\w*|нима қилиш керак|recommendations?|next steps|actions)\b")]
    private static partial Regex RecommendationsTitle();

    [GeneratedRegex(@"^\s*\d+[.)]?\s+")]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"[\s  ]")]
    private static partial Regex SpaceChars();

    /// <summary>Изменение со знаком: −2,3% / +5,2 п.п. / -19,0 %, с подписью «к …», «от …», «до цели …», «vs …».</summary>
    [GeneratedRegex(@"(?<![\w.,])(?<sign>[+\-−–])\s?(?<num>\d{1,3}(?:[   ]\d{3})+(?:[.,]\d+)?|\d+(?:[.,]\d+)?)\s?(?<unit>%|п\.\s?п\.?|pp\b|p\.p\.|f\.p\.)(?:\s*(?<cap>(?:к|ко|от|до|против|чем|относительно|vs\.?)\s(?:(?!\s(?:и|and|va)\s[+\-−–]\d)[^,;()\n])+?|[\p{L}'’ ]{2,40}?(?:ga|га)\s+(?:nisbatan|нисбатан)))?(?=\s*(?:[,;)\n]|$)|\s+[—–-]\s|\s+(?:и|and|va)\s+[+\-−–]\d)")]
    private static partial Regex Delta();

    /// <summary>Текст перед изменением: начало, скобка, запятая, «и», процент, число или число с короткой единицей («371 148 кг»).</summary>
    [GeneratedRegex(@"(?:^|[(,;%]|\d|\d\s?[\p{L}.']{1,8}|\s(?:и|and|va))\s*$")]
    private static partial Regex AttachedDelta();

    [GeneratedRegex(@"^\s*[+\-−–]\s?\d[\d\s  ]*(?:[.,]\d+)?\s?(?:%|п\.\s?п\.?|pp|p\.p\.)\s*(?:\S.*)?$")]
    private static partial Regex WholeDelta();

    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex Number();

    [GeneratedRegex(@"\(\s*(?:(?:[,;]|и|and|va)\s*)*\)")]
    private static partial Regex EmptyParens();

    [GeneratedRegex(@"(?:^|\s)(?:к|ко|от|до|против|vs\.?)\s", RegexOptions.IgnoreCase)]
    private static partial Regex CaptionStart();

    [GeneratedRegex(@"цел|план|норм|target|goal|maqsad|reja|мақсад|режа", RegexOptions.IgnoreCase)]
    private static partial Regex TargetCaption();

    /// <summary>Показатели, у которых рост — плохо.</summary>
    [GeneratedRegex(@"просроч|долг|дебитор|задолж|возврат|отток|потер|расход|затрат|издерж|себестоим|опоздан|отсутств|прогул|дефицит|недостач|брак|жалоб|отказ|списан|текучест|увольн|out.?of.?stock|муддати\s*ўтган|qarz|kechik|qaytar|xarajat|кечик|қарз|қайтар|харажат", RegexOptions.IgnoreCase)]
    private static partial Regex InverseMetric();

    [GeneratedRegex(@"выручк|оборот|объ[её]м|продаж|факт|акб|активн\w* точ|страйк|выполнен|заказ|дебитор|остат|явк|опоздан|tushum|savdo|hajm|bajar|daromad|тушум|савдо|ҳажм|бажар", RegexOptions.IgnoreCase)]
    private static partial Regex KpiMetric();

    [GeneratedRegex(@"(?:^|\s)план\w*|(?:^|\s)reja\w*|режа", RegexOptions.IgnoreCase)]
    private static partial Regex PlanLabel();

    [GeneratedRegex(@"%|выполнен|bajar|бажар|остаток|разрыв", RegexOptions.IgnoreCase)]
    private static partial Regex RateLabel();

    [GeneratedRegex(@"факт|объ[её]м|hajm|fakt|ҳажм", RegexOptions.IgnoreCase)]
    private static partial Regex FactLabel();

    [GeneratedRegex(@"прогноз|ожидаем|prognoz|прогнози", RegexOptions.IgnoreCase)]
    private static partial Regex ForecastLabel();

    [GeneratedRegex(@"^\s*(?<num>\d{1,3}(?:[   ]\d{3})+(?:[.,]\d+)?|\d+(?:[.,]\d+)?)\s*(?<unit>%|[\p{L}'.]+(?:\s[\p{L}'.]+)?)?")]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"[a-z]'|so'm|mln|mlrd", RegexOptions.IgnoreCase)]
    private static partial Regex UzLetters();

    [GeneratedRegex(@"паден|упал|упала|упали|сниз\w* на \d|выпал|выпад|нет закуп|не закуп|закупок нет|нет заказ|заказов нет|прекрат|остановил|потер|сбой|критич|срыв|сорва|просроч|не выполн|tushib ket|to'xta|тушиб кет|тўхта", RegexOptions.IgnoreCase)]
    private static partial Regex SevereWords();

    [GeneratedRegex(@"\s*[(\[]?\s*(?:ответственн\w*|отв\.|исполнител\w*|кто)\s*[:—–-]\s*(?<owner>[^)\];\n]+)[)\]]?", RegexOptions.IgnoreCase)]
    private static partial Regex OwnerPattern();

    [GeneratedRegex(@"(?<!\p{L})(?:до|срок(?:\s*[:—-])?)\s*(?<d>\d{1,2})\.(?<m>\d{1,2})(?:\.(?<y>\d{4}|\d{2}))?(?![\d,])(?!\s*(?:млн|млрд|тыс|%|кг|сум|т\b))", RegexOptions.IgnoreCase)]
    private static partial Regex DuePattern();

    [GeneratedRegex(@"\b\d{2}\.\d{2}\.\d{4}\b")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"роп|супервайз|продаж|торгов|агент|тп\b|sotuv|savdo|сотув|савдо")]
    private static partial Regex SalesOwner();

    [GeneratedRegex(@"^\s*(?:\*\*|__)(?<bold>[^*_]{1,80}?)(?:\*\*|__)\s*[:—–-]?\s*(?<rest>.*)$")]
    private static partial Regex BoldLead();

    [GeneratedRegex(@"^(?<label>[^—–:]{2,80}?)\s+[—–]\s+(?<value>.+)$")]
    private static partial Regex DashSplit();

    [GeneratedRegex(@"^(?<label>[^:]{2,80}?):\s+(?<value>.+)$")]
    private static partial Regex ColonSplit();

    [GeneratedRegex(@"\*\*|__|`|(?<!\w)\*(?!\s)|(?<!\s)\*(?!\w)")]
    private static partial Regex Emphasis();
}
