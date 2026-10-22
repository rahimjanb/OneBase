using System.Text.RegularExpressions;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;

namespace OneBase.Application.DataSources;

/// <summary>
/// Цель сопоставления — роль столбца в поле OneBase: «{поле}.{роль}» (plan_primary.value, dealer.region, pnl.amount…). Только реальные поля
/// раздела из DataSourceCatalog: ИИ не может назначить столбец полю, которого нет. Financial — финансовые данные (P&L): всегда с проверкой.
/// </summary>
public sealed record MappingTarget(string Key, string Field, string FieldLabel, string Role, string RoleLabel, string Description, bool Required, bool Financial);

public static class MappingRoles
{
    public const string Region = "region";
    public const string Category = "category";
    public const string Value = "value";
    public const string CategoryValue = "category_value";
    public const string Month = "month";
    public const string Year = "year";
    public const string Dealer = "dealer";
    public const string Line = "line";
    public const string Amount = "amount";
    public const string Date = "date";
    public const string Kind = "kind";
    public const string CategoryName = "category_name";
    public const string Comment = "comment";
    public const string Norm = "norm";
    public const string Opening = "opening";

    public static IReadOnlyList<MappingTarget> For(DataSourceField field)
    {
        MappingTarget T(string role, string label, string description, bool required = false) =>
            new($"{field.Key}.{role}", field.Key, field.Label, role, label, description, required, field.Kind is "pnl" or "ledger" or "terms");
        return field.Kind switch
        {
            "plan" =>
            [
                T(Region, "Регион (дилер)", "регион OneBase, дилер региона или ID филиала Linko — строка плана относится к нему", true),
                T(Category, "Категория", "категория товара строки, если таблица длинная (строка — регион × категория)"),
                T(Value, "План, кг", "значение плана в кг (если в тоннах — transformType tons_to_kg)"),
                T(CategoryValue, "План категории", "столбец-категория широкой таблицы (каждая категория — свой столбец): план этой категории; укажите category"),
                T(Month, "Месяц", "месяц строки: «сентябрь», «09.2026», «2026-09» или дата"),
                T(Year, "Год", "год строки, если в месяце его нет"),
            ],
            "profile" =>
            [
                T(Region, "Регион", "регион OneBase, к которому относится значение", true),
                T(Value, field.Key == DataSourceCatalog.Dealer ? "Дилер" : "СВР",
                    field.Key == DataSourceCatalog.Dealer ? "дилер региона (подпись строк первички и P&L)" : "супервайзер (СВР) региона", true),
            ],
            "ledger" =>
            [
                T(Date, "Дата", "дата операции (день)", true),
                T(Dealer, "Контрагент", "дилер или клиент завода, к которому относится операция", true),
                T(Amount, "Сумма", "сумма операции в сумах (приход минус расход; валюта — по курсу)", true),
                T(Kind, field.Key == DataSourceCatalog.DealerPayments ? "Касса / банк" : "Статья",
                    field.Key == DataSourceCatalog.DealerPayments ? "кошелёк: наличные («Нақд») или банк" : "статья расхода: «601-Дилер бонуслари», «606-…»"),
                T(CategoryName, "Категория", "категория строки — по ней отбираются нужные (например, «101-…» — поступления от дилеров)"),
                T(Comment, "Комментарий", "пояснение к операции"),
            ],
            "balance" =>
            [
                T(Dealer, "Контрагент", "дилер или клиент завода", true),
                T(Amount, "Остаток", "долг контрагента на начало месяца (остаток на начало)", true),
            ],
            "terms" =>
            [
                T(Dealer, "Контрагент", "дилер или клиент завода", true),
                T(Norm, "Срок оплаты, дней", "норма оплаты по договору; пусто — без срока"),
                T(Opening, "Остаток долга", "долг контрагента на дату начала реестра"),
            ],
            _ =>
            [
                T(Dealer, "Контрагент", "дилер (контрагент) P&L", true),
                T(Line, "Статья", "статья P&L: продажи, себестоимость, расходы…", true),
                T(Amount, "Сумма", "сумма статьи за месяц", true),
                T(Month, "Месяц", "месяц строки"),
                T(Year, "Год", "год строки, если в месяце его нет"),
            ],
        };
    }
}

/// <summary>Столбец листа для анализа: буква, заголовок и несколько значений.</summary>
public sealed record SheetColumnSample(string Letter, string Header, IReadOnlyList<string> Samples);

/// <summary>
/// Что ИИ видит при сопоставлении: таблица и лист, заголовки и примеры значений, допустимые цели раздела, категории OneBase, поля,
/// которые уже ведут другие привязки (Blocked: поле → правило), и текущее сопоставление привязки.
/// </summary>
public sealed record SheetMappingRequest(
    string Section,
    string SectionName,
    string Spreadsheet,
    string Sheet,
    IReadOnlyList<string> Sheets,
    int HeaderRow,
    IReadOnlyList<SheetColumnSample> Columns,
    IReadOnlyList<MappingTarget> Targets,
    IReadOnlyList<DataCategoryRef> Categories,
    IReadOnlyDictionary<string, string> Blocked,
    IReadOnlyList<DataSourceFieldBinding> Existing,
    DateOnly Today);

/// <summary>Предложение модели — до проверки.</summary>
public sealed record RawSuggestion(string? SourceColumn, string? TargetField, double? Confidence, string? Reason, string? TransformType, string? Category,
    bool RequiresConfirmation = false, IReadOnlyList<string>? Warnings = null);

/// <summary>Откуда месяц у поля: column — столбец, sheet — название листа, current — текущий месяц, fixed — один месяц (Year, Month).</summary>
public sealed record RawFieldSetting(string? Field, string? MonthSource, int? Year, int? Month);

/// <summary>Ответ модели: предложения по столбцам, настройки полей, заметки; Model — какая модель ответила.</summary>
public sealed record RawMapping(IReadOnlyList<RawSuggestion> Suggestions, IReadOnlyList<RawFieldSetting> Fields, IReadOnlyList<string> Notes, string Model);

public static class MappingStatuses
{
    /// <summary>Уверенность высокая, проверки пройдены — выбрано сразу.</summary>
    public const string Accepted = "accepted";

    /// <summary>Нужна проверка человеком: средняя уверенность, неоднозначно, финансовое поле, похоже на факт.</summary>
    public const string Review = "review";

    /// <summary>Подходящего поля нет или уверенность низкая.</summary>
    public const string Unmatched = "unmatched";

    /// <summary>Отклонено: поля нет в OneBase (или в разделе), столбца нет в листе, неизвестная категория.</summary>
    public const string Rejected = "rejected";

    /// <summary>Поле ведёт другая привязка Google Sheets — не перезаписываем её.</summary>
    public const string Blocked = "blocked";
}

public sealed record MappingSuggestion(
    string SourceColumn,
    string Header,
    string? TargetField,
    string? FieldLabel,
    string? RoleLabel,
    string? Category,
    double Confidence,
    string Reason,
    string TransformType,
    bool RequiresConfirmation,
    string Status,
    IReadOnlyList<string> Warnings);

/// <summary>Месяц поля (откуда) и период — какие месяцы листа брать (null — по умолчанию «текущий год»).</summary>
public sealed record MappingFieldSetting(string Field, string MonthSource, int? Year, int? Month, PeriodFilter? Period = null);

/// <summary>Цель для выбора в интерфейсе: ключ, подпись «План первички · Регион», и почему недоступна (поле ведёт другая привязка).</summary>
public sealed record MappingTargetView(string Key, string Field, string Label, string Role, string? Blocked);

/// <summary>Решение человека по столбцу: принятое (или исправленное) соответствие.</summary>
public sealed record MappingDecision(string SourceColumn, string TargetField, string? Category = null, string? TransformType = null);

/// <summary>
/// Предложение ИИ после проверки. Fields — привязка из принятых сразу соответствий (что сохранится, если подтвердить как есть);
/// Stale — какие сохранённые сопоставления привязки больше не подходят к листу.
/// </summary>
public sealed record SheetMappingProposal(
    string Sheet,
    IReadOnlyList<MappingSuggestion> Suggestions,
    IReadOnlyList<MappingFieldSetting> FieldSettings,
    IReadOnlyList<DataSourceFieldBinding> Fields,
    IReadOnlyList<string> FieldErrors,
    IReadOnlyList<MappingTargetView> Targets,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Stale,
    string Model,
    double AcceptConfidence,
    double ReviewConfidence);

/// <summary>Пороги уверенности (секция DataSources:AutoMap): от Accept — выбрано сразу, от Review — «проверьте», ниже — не сопоставлено.</summary>
public sealed class SheetMappingOptions
{
    public const string Section = "DataSources:AutoMap";

    public double AcceptConfidence { get; set; } = 0.85;
    public double ReviewConfidence { get; set; } = 0.5;

    /// <summary>Строк значений под заголовком, которые видит ИИ.</summary>
    public int SampleRows { get; set; } = 12;

    /// <summary>Столбцов, которые видит ИИ.</summary>
    public int MaxColumns { get; set; } = 40;
}

/// <summary>ИИ недоступен или ответил не по формату — текст для администратора.</summary>
public sealed class SheetMappingException(string message) : Exception(message);

/// <summary>Сопоставление столбцов листа с полями OneBase моделью ИИ проекта. Реализация — OneBase.AI (AI Gateway).</summary>
public interface ISheetMappingAdvisor
{
    /// <exception cref="SheetMappingException">ИИ не настроен, недоступен или ответил не JSON.</exception>
    Task<RawMapping> AdviseAsync(SheetMappingRequest request, Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Проверка ответа ИИ и сборка привязки. Модель только предлагает: цель — из списка раздела, столбец — из листа, поле, которое ведёт другая
/// привязка, — заблокировано, похожее на факт (факт ведёт Linko) и финансовое — только с проверкой человеком, неоднозначное — тоже.
/// Сохраняется привязка в обычной модели DataSourceFieldBinding — той же, что у ручного редактора.
/// </summary>
public static partial class SheetMapping
{
    public static readonly IReadOnlySet<string> Transforms = new HashSet<string> { "none", "tons_to_kg", "month_text", "date" };
    public static readonly IReadOnlySet<string> MonthSources = new HashSet<string> { "column", "sheet", "current", "fixed" };

    public static IReadOnlyList<MappingTargetView> TargetViews(SheetMappingRequest request) =>
        request.Targets.Select(t => new MappingTargetView(t.Key, t.Field, $"{t.FieldLabel} · {t.RoleLabel}", t.Role, request.Blocked.GetValueOrDefault(t.Field))).ToList();

    public static SheetMappingProposal Validate(SheetMappingRequest request, RawMapping raw, SheetMappingOptions options, IReadOnlyList<string>? stale = null)
    {
        var targets = request.Targets.ToDictionary(t => t.Key, StringComparer.OrdinalIgnoreCase);
        var result = new List<MappingSuggestion>();
        foreach (var s in raw.Suggestions)
        {
            var column = Column(request, s.SourceColumn);
            var warnings = (s.Warnings ?? []).Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim()).ToList();
            var confidence = Math.Clamp(s.Confidence ?? 0, 0, 1);
            var reason = string.IsNullOrWhiteSpace(s.Reason) ? "—" : s.Reason.Trim();
            var key = s.TargetField?.Trim();
            if (column is null)
            {
                result.Add(new MappingSuggestion(s.SourceColumn ?? "?", "", key, null, null, null, confidence, reason, "none", true, MappingStatuses.Rejected,
                    [.. warnings, $"столбца «{s.SourceColumn}» нет в листе"]));
                continue;
            }

            if (string.IsNullOrEmpty(key) || key is "null" or "none" or "unmatched")
            {
                result.Add(Unmatched(column, reason, warnings));
                continue;
            }

            if (!targets.TryGetValue(key, out var target))
            {
                warnings.Add(LooksLikeFact(key) || LooksLikeFact(column.Header)
                    ? "факт продаж и другие данные Linko из Google Sheets не пишутся — их ведёт Linko"
                    : $"поля «{key}» нет среди полей раздела «{request.SectionName}» — ИИ не может создавать поля");
                result.Add(new MappingSuggestion(column.Letter, column.Header, key, null, null, null, confidence, reason, "none", true, MappingStatuses.Rejected, warnings));
                continue;
            }

            string? category = null;
            if (target.Role == MappingRoles.CategoryValue)
            {
                category = CategoryName(request, s.Category);
                if (category is null)
                {
                    warnings.Add($"категории «{s.Category}» нет в OneBase");
                    result.Add(Suggestion(column, target, null, confidence, reason, "none", true, MappingStatuses.Rejected, warnings));
                    continue;
                }
            }

            if (request.Blocked.TryGetValue(target.Field, out var rule))
            {
                warnings.Add(rule);
                result.Add(Suggestion(column, target, category, confidence, reason, "none", true, MappingStatuses.Blocked, warnings));
                continue;
            }

            var transform = Transform(s.TransformType, target, warnings);
            var mustReview = s.RequiresConfirmation || target.Financial;
            if (LooksLikeFact(column.Header) && target.Role is MappingRoles.Value or MappingRoles.CategoryValue)
            {
                mustReview = true;
                warnings.Add("заголовок похож на факт, а поле — план: факт продаж ведёт Linko, проверьте");
            }

            if (target.Financial)
            {
                warnings.Add("финансовые данные — только после проверки");
            }

            var status = confidence >= options.AcceptConfidence && !mustReview ? MappingStatuses.Accepted
                : confidence >= options.ReviewConfidence ? MappingStatuses.Review
                : MappingStatuses.Unmatched;
            if (status == MappingStatuses.Unmatched)
            {
                warnings.Add($"низкая уверенность ({confidence:P0}) — не сопоставлено");
            }

            result.Add(Suggestion(column, target, category, confidence, reason, transform, status != MappingStatuses.Accepted, status, warnings));
        }

        // Одна роль поля — один столбец: несколько претендентов — выбирает человек.
        foreach (var group in result.Where(r => r.Status is MappingStatuses.Accepted or MappingStatuses.Review)
                     .GroupBy(r => (r.TargetField!.ToLowerInvariant(), r.Category?.ToLowerInvariant()))
                     .Where(g => g.Select(r => r.SourceColumn).Distinct().Count() > 1))
        {
            foreach (var r in group.ToList())
            {
                result[result.IndexOf(r)] = r with
                {
                    Status = MappingStatuses.Review,
                    RequiresConfirmation = true,
                    Warnings = [.. r.Warnings, $"на «{r.FieldLabel} · {r.RoleLabel}» претендуют столбцы {string.Join(", ", group.Select(x => x.SourceColumn))} — выберите один"],
                };
            }
        }

        // Итог при столбцах-категориях задвоил бы план; неполное поле (нет региона, плана, месяца) сохранилось бы без данных — сразу не выбираются.
        var settings = Settings(request, raw.Fields);
        foreach (var group in result.Where(r => r.Status == MappingStatuses.Accepted).GroupBy(r => targets[r.TargetField!].Field).ToList())
        {
            var field = DataSourceCatalog.Field(group.Key)!;
            var roles = group.Select(r => targets[r.TargetField!].Role).ToHashSet();
            if (roles.Contains(MappingRoles.CategoryValue) && roles.Contains(MappingRoles.Value))
            {
                foreach (var r in group.Where(r => targets[r.TargetField!].Role == MappingRoles.Value))
                {
                    result[result.IndexOf(r)] = r with
                    {
                        Status = MappingStatuses.Review, RequiresConfirmation = true,
                        Warnings = [.. r.Warnings, "в таблице есть столбцы-категории — общий план к ним не нужен (план задвоился бы)"],
                    };
                }

                roles.Remove(MappingRoles.Value);
            }

            if (Incomplete(field, roles, settings.FirstOrDefault(s => s.Field == field.Key)) is { } missing)
            {
                foreach (var r in result.Where(r => r.Status == MappingStatuses.Accepted && targets[r.TargetField!].Field == field.Key).ToList())
                {
                    result[result.IndexOf(r)] = r with
                    {
                        Status = MappingStatuses.Review, RequiresConfirmation = true,
                        Warnings = [.. r.Warnings, $"для «{field.Label}» не хватает {missing} — поле сохранится, только если его дополнить"],
                    };
                }
            }
        }

        // Столбцы, о которых ИИ промолчал, — тоже в списке: их можно сопоставить вручную.
        foreach (var column in request.Columns.Where(c => c.Header.Length > 0 && result.All(r => r.SourceColumn != c.Letter)))
        {
            result.Add(Unmatched(column, "ИИ не нашёл подходящего поля", []));
        }

        var decisions = result.Where(r => r.Status == MappingStatuses.Accepted)
            .Select(r => new MappingDecision(r.SourceColumn, r.TargetField!, r.Category, r.TransformType))
            .ToList();
        IReadOnlyList<DataSourceFieldBinding> fields = [];
        IReadOnlyList<string> errors = [];
        if (decisions.Count > 0)
        {
            (fields, errors) = Build(request, decisions, settings);
        }

        return new SheetMappingProposal(request.Sheet, result.OrderBy(r => LetterOrder(r.SourceColumn)).ToList(), settings, fields, errors, TargetViews(request),
            request.Categories.Select(c => c.Name).ToList(), raw.Notes, stale ?? [], raw.Model, options.AcceptConfidence, options.ReviewConfidence);
    }

    /// <summary>
    /// Привязка из решений человека (или принятых сразу предложений): по каждому полю — его сопоставление столбцов в той же модели, что у
    /// ручного редактора, и снимок заголовков. Поле, которому не хватает обязательных столбцов или месяца, не собирается — ошибка поля.
    /// Решение с несуществующей целью, чужим полем или столбцом не из листа — ошибка.
    /// </summary>
    public static (IReadOnlyList<DataSourceFieldBinding> Fields, IReadOnlyList<string> Errors) Build(SheetMappingRequest request,
        IReadOnlyList<MappingDecision> decisions, IReadOnlyList<MappingFieldSetting> settings)
    {
        var targets = request.Targets.ToDictionary(t => t.Key, StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var chosen = new List<(SheetColumnSample Column, MappingTarget Target, string? Category, string Transform)>();
        foreach (var d in decisions)
        {
            var column = Column(request, d.SourceColumn);
            if (column is null)
            {
                errors.Add($"Столбца «{d.SourceColumn}» нет в листе «{request.Sheet}».");
                continue;
            }

            if (!targets.TryGetValue(d.TargetField?.Trim() ?? "", out var target))
            {
                errors.Add($"Поля «{d.TargetField}» нет среди полей раздела «{request.SectionName}».");
                continue;
            }

            if (request.Blocked.TryGetValue(target.Field, out var rule))
            {
                errors.Add($"«{target.FieldLabel}»: {rule}.");
                continue;
            }

            string? category = null;
            if (target.Role == MappingRoles.CategoryValue && (category = CategoryName(request, d.Category)) is null)
            {
                errors.Add($"Столбец {column.Letter}: категории «{d.Category}» нет в OneBase.");
                continue;
            }

            chosen.Add((column, target, category, Transform(d.TransformType, target, [])));
        }

        var fields = new List<DataSourceFieldBinding>();
        foreach (var group in chosen.GroupBy(c => c.Target.Field))
        {
            var field = DataSourceCatalog.Field(group.Key)!;
            var duplicate = group.Where(c => c.Target.Role != MappingRoles.CategoryValue).GroupBy(c => c.Target.Role).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is not null)
            {
                errors.Add($"«{field.Label} · {duplicate.First().Target.RoleLabel}»: выбрано несколько столбцов ({string.Join(", ", duplicate.Select(c => c.Column.Letter))}).");
                continue;
            }

            var headers = new Dictionary<string, string>();
            string? Ref(string role)
            {
                var c = group.FirstOrDefault(x => x.Target.Role == role).Column;
                if (c is null)
                {
                    return null;
                }

                var reference = Reference(request, c);
                headers[reference] = c.Header;
                return reference;
            }

            var setting = settings.FirstOrDefault(s => s.Field == field.Key);
            var month = Ref(MappingRoles.Month);
            var monthSource = month is not null ? "column" : setting?.MonthSource;
            if (field.Kind is "plan" or "pnl" && monthSource is null)
            {
                errors.Add($"«{field.Label}»: не понятно, за какой месяц значения — сопоставьте столбец месяца или выберите месяц поля вручную.");
                continue;
            }

            var fixedMonth = monthSource == "fixed";
            if (fixedMonth && (setting?.Year is null || setting.Month is not (>= 1 and <= 12)))
            {
                errors.Add($"«{field.Label}»: для одного месяца нужны год и месяц.");
                continue;
            }

            var tons = group.Any(c => c.Transform == "tons_to_kg");
            // Какие месяцы листа брать: по умолчанию текущий год — история за годы не тянется, план сам переходит на новый год.
            var period = monthSource is "column" or "sheet" ? setting?.Period ?? new PeriodFilter(DataSourceRules.PeriodYear) : null;
            if (DataSourceRules.PeriodError(period) is { } periodError)
            {
                errors.Add($"«{field.Label}»: {periodError}.");
                continue;
            }

            switch (field.Kind)
            {
                case "plan":
                {
                    var region = Ref(MappingRoles.Region);
                    var wide = group.Where(c => c.Target.Role == MappingRoles.CategoryValue)
                        .Select(c =>
                        {
                            var reference = Reference(request, c.Column);
                            headers[reference] = c.Column.Header;
                            return new PlanColumn(reference, c.Category);
                        })
                        .ToList();
                    var value = wide.Count > 0 ? null : Ref(MappingRoles.Value);
                    if (region is null || (value is null && wide.Count == 0))
                    {
                        errors.Add($"«{field.Label}»: нужны столбцы региона и плана.");
                        continue;
                    }

                    var plan = new PlanFieldMapping(region, wide.Count > 0 ? null : Ref(MappingRoles.Category), value, wide.Count > 0 ? wide : null,
                        monthSource == "column" ? month : null, monthSource == "current" ? null : Ref(MappingRoles.Year),
                        fixedMonth ? setting!.Year : monthSource == "column" ? request.Today.Year : setting?.Year, fixedMonth ? setting!.Month : null, tons ? 1000m : 1m);
                    fields.Add(new DataSourceFieldBinding(field.Key, plan, Replace: true, MonthFromSheet: monthSource == "sheet",
                        CurrentMonth: monthSource == "current", Headers: headers, Period: period));
                    break;
                }
                case "profile":
                {
                    var region = Ref(MappingRoles.Region);
                    var value = Ref(MappingRoles.Value);
                    if (region is null || value is null)
                    {
                        errors.Add($"«{field.Label}»: нужны столбцы региона и значения.");
                        continue;
                    }

                    fields.Add(new DataSourceFieldBinding(field.Key, Profile: new ProfileFieldMapping(region, value), Replace: false, Headers: headers));
                    break;
                }
                case "ledger":
                {
                    var date = Ref(MappingRoles.Date);
                    var dealer = Ref(MappingRoles.Dealer);
                    var amount = Ref(MappingRoles.Amount);
                    if (date is null || dealer is null || amount is null)
                    {
                        errors.Add($"«{field.Label}»: нужны столбцы даты, контрагента и суммы.");
                        continue;
                    }

                    fields.Add(new DataSourceFieldBinding(field.Key, Replace: true, Headers: headers,
                        Ledger: new LedgerFieldMapping(date, dealer, amount, Ref(MappingRoles.Kind), Ref(MappingRoles.CategoryName), null, Ref(MappingRoles.Comment))));
                    break;
                }
                case "balance":
                {
                    var dealer = Ref(MappingRoles.Dealer);
                    var amount = Ref(MappingRoles.Amount);
                    if (dealer is null || amount is null)
                    {
                        errors.Add($"«{field.Label}»: нужны столбцы контрагента и остатка.");
                        continue;
                    }

                    fields.Add(new DataSourceFieldBinding(field.Key, Replace: true, Headers: headers,
                        Balance: new BalanceFieldMapping(dealer, amount, null, new DateOnly(request.Today.Year, request.Today.Month, 1))));
                    break;
                }
                case "terms":
                {
                    var dealer = Ref(MappingRoles.Dealer);
                    var norm = Ref(MappingRoles.Norm);
                    var opening = Ref(MappingRoles.Opening);
                    if (dealer is null || (norm is null && opening is null))
                    {
                        errors.Add($"«{field.Label}»: нужны столбцы контрагента и срока оплаты или остатка.");
                        continue;
                    }

                    fields.Add(new DataSourceFieldBinding(field.Key, Replace: true, Headers: headers,
                        Terms: new TermsFieldMapping(dealer, norm, opening, opening is null ? null : new DateOnly(request.Today.Year, 1, 1))));
                    break;
                }
                default:
                {
                    var dealer = Ref(MappingRoles.Dealer);
                    var line = Ref(MappingRoles.Line);
                    var amount = Ref(MappingRoles.Amount);
                    if (dealer is null || line is null || amount is null)
                    {
                        errors.Add($"«{field.Label}»: нужны столбцы контрагента, статьи и суммы.");
                        continue;
                    }

                    var pnl = new PnlSheetMapping(null, request.HeaderRow, dealer, null, line, amount, monthSource == "current" ? null : Ref(MappingRoles.Year),
                        monthSource == "column" ? month : null, fixedMonth ? setting!.Year : monthSource == "column" ? request.Today.Year : null,
                        fixedMonth ? setting!.Month : null, tons ? 1000m : 1m);
                    fields.Add(new DataSourceFieldBinding(field.Key, Pnl: pnl, Replace: true, MonthFromSheet: monthSource == "sheet",
                        CurrentMonth: monthSource == "current", Headers: headers, Period: period));
                    break;
                }
            }
        }

        return (fields, errors);
    }

    /// <summary>Чего не хватает полю среди выбранных ролей (null — поле полное): региона, значения, месяца (столбцом или настройкой поля).</summary>
    private static string? Incomplete(DataSourceField field, IReadOnlySet<string> roles, MappingFieldSetting? setting)
    {
        var month = roles.Contains(MappingRoles.Month) || setting is { MonthSource: not "column" };
        return field.Kind switch
        {
            "plan" => !roles.Contains(MappingRoles.Region) ? "столбца региона"
                : !roles.Contains(MappingRoles.Value) && !roles.Contains(MappingRoles.CategoryValue) ? "столбца плана"
                : !month ? "месяца" : null,
            "profile" => !roles.Contains(MappingRoles.Region) ? "столбца региона" : !roles.Contains(MappingRoles.Value) ? "столбца значения" : null,
            "ledger" => !roles.Contains(MappingRoles.Date) || !roles.Contains(MappingRoles.Dealer) || !roles.Contains(MappingRoles.Amount)
                ? "столбцов даты, контрагента и суммы" : null,
            "balance" => !roles.Contains(MappingRoles.Dealer) || !roles.Contains(MappingRoles.Amount) ? "столбцов контрагента и остатка" : null,
            "terms" => !roles.Contains(MappingRoles.Dealer) ? "столбца контрагента"
                : !roles.Contains(MappingRoles.Norm) && !roles.Contains(MappingRoles.Opening) ? "столбца срока оплаты или остатка" : null,
            _ => !roles.Contains(MappingRoles.Dealer) || !roles.Contains(MappingRoles.Line) || !roles.Contains(MappingRoles.Amount) ? "столбцов контрагента, статьи и суммы"
                : !month ? "месяца" : null,
        };
    }

    /// <summary>Какие сохранённые сопоставления привязки больше не подходят к листу (столбца нет, в букве другой заголовок).</summary>
    public static IReadOnlyList<string> Stale(SheetMappingRequest request, ReportDocument document)
    {
        var stale = new List<string>();
        foreach (var f in request.Existing)
        {
            var label = DataSourceCatalog.Field(f.Field)?.Label ?? f.Field;
            var references = f.Headers?.Keys.ToList() ?? References(f);
            var check = f.Headers is { Count: > 0 } ? f : f with { Headers = references.ToDictionary(r => r, r => r) };
            if (DataSourceRules.StructureError(document, [request.Sheet], request.HeaderRow, check) is not null)
            {
                stale.Add($"«{label}»: сохранённое сопоставление не подходит к листу «{request.Sheet}» — столбцы {string.Join(", ", references.Select(r => $"«{r}»"))}");
            }
        }

        return stale;
    }

    private static List<string> References(DataSourceFieldBinding f) =>
        new[]
            {
                f.Plan?.RegionColumn, f.Plan?.CategoryColumn, f.Plan?.ValueColumn, f.Plan?.MonthColumn, f.Plan?.YearColumn, f.Profile?.RegionColumn,
                f.Profile?.ValueColumn, f.Pnl?.DealerColumn, f.Pnl?.LineColumn, f.Pnl?.ValueColumn, f.Pnl?.MonthColumn, f.Pnl?.YearColumn,
                f.Ledger?.DateColumn, f.Ledger?.DealerColumn, f.Ledger?.AmountColumn, f.Ledger?.KindColumn, f.Ledger?.CategoryColumn,
                f.Ledger?.CommentColumn, f.Terms?.DealerColumn, f.Terms?.NormColumn, f.Terms?.OpeningColumn, f.Balance?.DealerColumn, f.Balance?.AmountColumn,
            }
            .Concat(f.Plan?.ValueColumns?.Select(c => c.Column) ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r) && DataValues.ColumnIndex(r) is null)
            .Select(r => r!)
            .Distinct()
            .ToList();

    private static IReadOnlyList<MappingFieldSetting> Settings(SheetMappingRequest request, IReadOnlyList<RawFieldSetting> raw)
    {
        var fields = request.Targets.Select(t => t.Field).Distinct().ToHashSet();
        return raw.Where(s => s.Field is not null && fields.Contains(s.Field) && s.MonthSource is not null && MonthSources.Contains(s.MonthSource))
            .GroupBy(s => s.Field!)
            .Select(g => g.First())
            .Select(s => new MappingFieldSetting(s.Field!, s.MonthSource!, s.Year is >= 2020 and <= 2100 ? s.Year : null, s.Month is >= 1 and <= 12 ? s.Month : null))
            .ToList();
    }

    /// <summary>Столбец сопоставления — как у ручного редактора: заголовок, если он есть и не повторяется, иначе буква.</summary>
    private static string Reference(SheetMappingRequest request, SheetColumnSample column)
    {
        var name = column.Header.Trim();
        var unique = name.Length > 0 && request.Columns.Count(c => DataValues.Normalize(c.Header) == DataValues.Normalize(name)) == 1;
        return unique ? name : column.Letter;
    }

    /// <summary>Столбец по букве или заголовку.</summary>
    private static SheetColumnSample? Column(SheetMappingRequest request, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var t = text.Trim();
        return request.Columns.FirstOrDefault(c => string.Equals(c.Letter, t, StringComparison.OrdinalIgnoreCase))
               ?? request.Columns.FirstOrDefault(c => c.Header.Length > 0 && DataValues.Normalize(c.Header) == DataValues.Normalize(t));
    }

    /// <summary>
    /// Категория OneBase по названию из листа или ответа ИИ: точное название или синоним (тип товара Linko, «могул» у помадки), иначе — основа
    /// синонима в названии («МОГУЛЬ» → Помадка, «ТРУБОЧКА» → Трубочки). Подходит к нескольким категориям — null: решает человек.
    /// </summary>
    public static string? CategoryName(IReadOnlyList<DataCategoryRef> categories, string? text)
    {
        var name = DataValues.Normalize(text);
        if (name.Length == 0)
        {
            return null;
        }

        var exact = categories.FirstOrDefault(c => DataValues.Normalize(c.Name) == name || c.Aliases.Any(a => DataValues.Normalize(a) == name));
        if (exact is not null)
        {
            return exact.Name;
        }

        var byStem = categories
            .Where(c => new[] { c.Name }.Concat(c.Aliases).Select(a => DataValues.Normalize(a)).Any(a => a.Length >= 4 && (name.Contains(a) || a.Contains(name) && name.Length >= 4)))
            .ToList();
        return byStem.Count == 1 ? byStem[0].Name : null;
    }

    private static string? CategoryName(SheetMappingRequest request, string? text) => CategoryName(request.Categories, text);

    private static string Transform(string? transform, MappingTarget target, List<string> warnings)
    {
        var t = (transform ?? "none").Trim().ToLowerInvariant();
        if (!Transforms.Contains(t))
        {
            warnings.Add($"преобразование «{transform}» не поддерживается — значения берутся как есть");
            return "none";
        }

        if (t == "tons_to_kg" && target.Role is not (MappingRoles.Value or MappingRoles.CategoryValue))
        {
            return "none";
        }

        return t;
    }

    private static MappingSuggestion Suggestion(SheetColumnSample column, MappingTarget target, string? category, double confidence, string reason,
        string transform, bool requiresConfirmation, string status, IReadOnlyList<string> warnings) =>
        new(column.Letter, column.Header, target.Key, target.FieldLabel, target.RoleLabel, category, confidence, reason, transform, requiresConfirmation, status, warnings);

    private static MappingSuggestion Unmatched(SheetColumnSample column, string reason, IReadOnlyList<string> warnings) =>
        new(column.Letter, column.Header, null, null, null, null, 0, reason, "none", false, MappingStatuses.Unmatched, warnings);

    private static bool LooksLikeFact(string? text) => text is not null && FactWords().IsMatch(text);

    private static int LetterOrder(string column) => DataValues.ColumnIndex(column) ?? int.MaxValue;

    [GeneratedRegex(@"факт|fact|actual|выполнен|отгружен|продано|sold|linko", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FactWords();
}
