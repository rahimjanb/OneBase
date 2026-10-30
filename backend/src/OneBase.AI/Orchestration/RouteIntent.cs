using System.Text.RegularExpressions;
using OneBase.AI.Agents;
using OneBase.AI.Knowledge;

namespace OneBase.AI.Orchestration;

/// <summary>
/// Область вопроса, которую пользователю не открыть: нет права на её инструменты или источник не подключён. Agent — сотрудник области;
/// Title — область данных (null — весь отдел сотрудника недоступен), Sources — её источники.
/// </summary>
public sealed record DeniedArea(string Agent, string? Title, IReadOnlyList<string> Sources);

/// <summary>
/// Смысл вопроса для маршрута без модели. Без AI-сотрудников остаётся только разговор: приветствие, благодарность, прощание, вопрос о
/// возможностях консультанта. Всё остальное — вопрос о данных, и у него маршрут обязателен. Область — по словам (русский, узбекский латиницей и
/// кириллицей), словарю продаж и источникам инструментов, которые пользователю доступны у каждого сотрудника.
/// </summary>
public static partial class RouteIntent
{
    /// <summary>
    /// Область данных: источники инструментов (KnowledgeSources), слова (см. Mentions) и сотрудники, которые ведут
    /// её в первую очередь. Сотрудник подходит, только если у него для пользователя есть инструмент одного из источников области.
    /// </summary>
    internal sealed record Domain(string Title, string[] Sources, string[] Words, string[] Owners);

    internal static readonly IReadOnlyList<Domain> Domains =
    [
        new("Задачи и проекты отделов", [KnowledgeSources.WorkTasks],
            ["задач", "проект", "просроч", "поручен", "дедлайн", "vazifa", "loyiha", "topshiriq", "вазифа", "лойиҳа", "лойиха", "топшириқ", "топширик"],
            ["hr", "sales"]),
        new("Визиты, маршруты и полевая команда", [KnowledgeSources.SalesVisits, KnowledgeSources.FieldSales],
            ["визит", "маршрут", "посещени", "торговых точ", "торговые точ", "торговой точ", "tashrif", "marshrut", "ташриф"],
            ["sales"]),
        new("Заказы, цены и акции", [KnowledgeSources.SalesSecondary, KnowledgeSources.SalesPrimary],
            ["заказ", "прайс", "цена", "цены", "цену", "ценам", "ценой", "скидк", "акци", "narx", "buyurtma", "chegirma", "aksiya", "нарх", "буюртма"],
            ["sales"]),
        new("Остатки и склады", [KnowledgeSources.SalesStock, KnowledgeSources.SupplyProviders],
            ["остат", "склад", "наличи", "дозагруз", "аутсток", "хватит", "хватает", "заканчива", "ombor", "qoldiq", "zaxira", "омбор", "қолдиқ", "колдик", "захира"],
            ["supply", "sales"]),
        new("Долги и оплаты", [KnowledgeSources.FinancePayments],
            ["долг", "должен", "должны", "должник", "задолж", "дебитор", "акт сверки", "сальдо", "dso", "оплат", "qarz", "to'lov", "қарз", "карз", "тўлов", "тулов"],
            ["finance", "sales"]),
        new("P&L дилеров", [KnowledgeSources.DealerPnl],
            ["p&l", "pnl", "марж", "рентаб", "себестоим", "ebit", "foyda", "marja", "фойда", "маржа"],
            ["finance"]),
        new("Первичка, дилеры и экспорт", [KnowledgeSources.SalesPrimary, KnowledgeSources.SalesExport],
            ["первичк", "отгруз", "экспорт", "дилер", "eksport", "diler", "jo'nat", "жўнат"],
            ["sales", "finance", "supply"]),
        new("Telegram: автоотчёты и рассылки", [KnowledgeSources.Telegram],
            ["telegram", "телеграм", "автоотч", "рассылк", "tarqatma", "тарқатма"],
            ["sales", "marketing"]),
        new("Находки AI", [KnowledgeSources.AiAlerts],
            ["находк", "фокус дня", "нашел ai", "нашел ии", "нашел аи", "аномали", "topilma", "топилма"],
            ["sales", "hr", "finance", "supply"]),
        new("Документы базы знаний", [KnowledgeSources.Documents],
            ["регламент", "положение о", "положения о", "приказ", "инструкц", "=договор", "договора", "договору", "договоре", "договоры", "договоров", "документ", "контракт", "соглашени", "hujjat", "nizom", "shartnoma",
             "buyruq", "ҳужжат", "хужжат", "низом", "шартнома", "буйруқ", "буйрук"],
            ["hr", "finance", "supply", "sales", "production", "marketing"]),
    ];

    /// <summary>Отдел по умолчанию, когда область не определилась: у кого больше источников, при равенстве — в этом порядке.</summary>
    private static readonly string[] DefaultPreference = ["sales", "finance", "hr", "supply", "production", "marketing"];

    /// <summary>Строчные буквы, «ё» → «е», все апострофы узбекской латиницы → «'».</summary>
    public static string Normalize(string? text) =>
        Apostrophes().Replace((text ?? string.Empty).ToLowerInvariant().Replace('ё', 'е'), "'");

    /// <summary>
    /// Разговор, а не вопрос о данных: приветствие, благодарность, прощание, вопрос о возможностях консультанта. Либо только такие фразы
    /// (и служебные слова), либо благодарность или «понятно» без вопроса и почти без других слов («Спасибо, план понятен»).
    /// </summary>
    public static bool IsSmallTalk(string? question)
    {
        var text = Normalize(question);
        if (!SmallTalk().IsMatch(text))
        {
            return false;
        }

        var rest = Words(SmallTalk().Replace(text, " ")).Where(w => !Fillers.Contains(w)).ToList();
        if (rest.Count == 0)
        {
            return true;
        }

        return Acknowledgement().IsMatch(text) && !AsksForData(question) && rest.Count <= 2;
    }

    /// <summary>
    /// Вопрос или просьба о данных — без обязательного «?»: вопросительные слова и глаголы запроса («что», «как», «найди», «проанализируй»,
    /// «хватит ли», «выполнение плана») по-русски и по-узбекски (латиница и кириллица).
    /// </summary>
    public static bool AsksForData(string? question) => question is { Length: > 0 } q && (q.Contains('?') || DataRequest().IsMatch(Normalize(q)));

    /// <summary>Уточнение к прошлому вопросу: «А по Самарканду?», «А за прошлый месяц?», «Сравни с предыдущим», «А кто хуже всех?», «Samarqand-chi?».</summary>
    public static bool IsFollowUp(string? question)
    {
        var text = Normalize(question).Trim();
        return text.Length > 0 && Words(text).Count <= 8 && FollowUp().IsMatch(text);
    }

    /// <summary>Вопрос о компании в целом («что происходит в компании», «по всем отделам»): привлекаются все доступные сотрудники.</summary>
    public static bool IsCompanyWide(string? question) => CompanyWide().IsMatch(Normalize(question));

    /// <summary>
    /// Сотрудники по словам текста: отделы (AiRouter.Keywords и словарь продаж) и области данных. Область добавляет сотрудника, только если её
    /// не покрывает уже выбранный: у него нет инструмента её источников. sources — источники данных, доступные пользователю у каждого сотрудника;
    /// null — все сотрудники покрывают все источники. Порядок — как в available.
    /// </summary>
    public static IReadOnlyList<string> Agents(string text, IReadOnlyList<AgentConfig> available, IReadOnlyList<string>? salesWords = null,
        IReadOnlyDictionary<string, IReadOnlySet<string>>? sources = null) =>
        Resolve(text, available, salesWords, sources).Agents;

    /// <summary>То же, что Agents, и области вопроса, которые не может открыть ни один доступный сотрудник.</summary>
    internal static (IReadOnlyList<string> Agents, IReadOnlyList<DeniedArea> Denied) Resolve(string text, IReadOnlyList<AgentConfig> available,
        IReadOnlyList<string>? salesWords, IReadOnlyDictionary<string, IReadOnlySet<string>>? sources)
    {
        var selected = AiRouter.KeywordAgents(text, available, salesWords).ToList();
        var denied = new List<DeniedArea>();
        var normalized = Normalize(text);
        var tokens = Words(normalized);
        bool Covers(string code, Domain d) => sources is null || (sources.TryGetValue(code, out var s) && d.Sources.Any(s.Contains));

        foreach (var domain in Domains.Where(d => d.Words.Any(w => Mentions(normalized, tokens, w))))
        {
            if (selected.Any(code => Covers(code, domain)))
            {
                continue;
            }

            var owner = domain.Owners.FirstOrDefault(code => available.Any(a => a.Code == code) && Covers(code, domain))
                        ?? available.Select(a => a.Code).FirstOrDefault(code => Covers(code, domain));
            if (owner is null)
            {
                var agent = domain.Owners.FirstOrDefault(code => available.Any(a => a.Code == code)) ?? domain.Owners[0];
                denied.Add(new DeniedArea(agent, domain.Title, domain.Sources));
            }
            else
            {
                selected.Add(owner);
            }
        }

        var order = available.Select(a => a.Code).ToList();
        return (selected.Distinct().OrderBy(order.IndexOf).ToList(), denied);
    }

    /// <summary>Сотрудник, когда область вопроса не определилась: больше всего источников данных у пользователя, при равенстве — продажи, финансы, HR…</summary>
    public static string? DefaultAgent(IReadOnlyList<AgentConfig> available, IReadOnlyDictionary<string, IReadOnlySet<string>>? sources = null) =>
        available
            .Where(a => sources is null || (sources.TryGetValue(a.Code, out var s) && s.Count > 0))
            .OrderByDescending(a => sources?.GetValueOrDefault(a.Code)?.Count ?? 0)
            .ThenBy(a => Array.IndexOf(DefaultPreference, a.Code) is var i and >= 0 ? i : DefaultPreference.Length)
            .Select(a => a.Code)
            .FirstOrDefault();

    /// <summary>
    /// Слово упомянуто: фраза — подстрокой, «=слово» — только слово целиком («договор», но не «договорились»), основа — с начала какого-нибудь
    /// слова текста («цена» не находится в «оценка»).
    /// </summary>
    private static bool Mentions(string text, IReadOnlyList<string> tokens, string word) =>
        word.StartsWith('=') ? tokens.Contains(word[1..])
        : word.Contains(' ') || word.Contains('&') ? text.Contains(word)
        : tokens.Any(t => t.StartsWith(word, StringComparison.Ordinal));

    private static IReadOnlyList<string> Words(string text) => WordPattern().Matches(text).Select(m => m.Value).ToList();

    /// <summary>Служебные слова, которые остаются после фраз разговора и не делают его вопросом.</summary>
    private static readonly HashSet<string> Fillers =
    [
        "а", "и", "ну", "да", "вот", "же", "тебе", "вам", "мне", "нам", "большое", "огромное", "очень", "всё", "все", "это", "друг", "бот", "ai", "ии",
        "консультант", "уважаемый", "katta", "juda", "sizga", "senga", "sen", "siz", "do'stim", "катта", "жуда", "сизга", "сенга",
    ];

    [GeneratedRegex("[ʻʼ’‘`´]")]
    private static partial Regex Apostrophes();

    [GeneratedRegex(@"[\p{L}\p{N}&']+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(
        @"(?<![\p{L}])(?:" +
        // приветствие
        @"привет\p{L}*|здравств\p{L}*|добр(?:ый|ое|ой)\s+(?:день|утро|вечер|ночи)|доброе\s+утро|салам\p{L}*|хай|hello|hi|hey|как\s+дела|как\s+ты|" +
        @"assalomu\s+alaykum|assalom\p{L}*|salom\p{L}*|qalaysiz|qalaysan|ассалому\s+алайкум|салом\p{L}*|қалайсиз|калайсиз|" +
        // благодарность и «понятно»
        @"спасибо\p{L}*|благодар\p{L}*|спс|пасиб\p{L}*|rahmat\p{L}*|raxmat\p{L}*|tashakkur\p{L}*|раҳмат\p{L}*|рахмат\p{L}*|ташаккур\p{L}*|" +
        @"понятн\p{L}*|ясно|ок|окей|хорошо|отлично|супер|класс|ладно|договорились|принято|tushunarli|xo'p|yaxshi|тушунарли|хўп|яхши|" +
        // прощание
        @"пока|до\s+свидания|до\s+встречи|всего\s+доброго|хорошего\s+дня|xayr\p{L}*|ko'rishguncha|хайр\p{L}*|кўришгунча|" +
        // возможности консультанта
        @"что\s+(?:ты\s+)?умеешь|чем\s+(?:ты\s+)?(?:можешь|сможешь)\s+помочь|что\s+ты\s+(?:можешь|знаешь|такое)|кто\s+ты|как\s+тебя\s+зовут|" +
        @"какие\s+(?:у\s+тебя\s+)?возможности|как\s+(?:ты\s+)?работаешь|помощь|help|" +
        @"nima\s+qila\s+olasan|nimalar\s+qila\s+olasan|sen\s+kimsan|qanday\s+yordam\s+bera\s+olasan|нима\s+қила\s+оласан|сен\s+кимсан" +
        @")(?![\p{L}])")]
    private static partial Regex SmallTalk();

    [GeneratedRegex(@"(?<![\p{L}])(?:спасибо\p{L}*|благодар\p{L}*|спс|понятн\p{L}*|ясно|принято|хорошо|ок|окей|ладно|договорились|rahmat\p{L}*|raxmat\p{L}*|tushunarli|xo'p|рахмат\p{L}*|раҳмат\p{L}*|тушунарли|хўп|пока|xayr\p{L}*)(?![\p{L}])")]
    private static partial Regex Acknowledgement();

    [GeneratedRegex(
        @"(?<![\p{L}])(?:" +
        @"сколько|как(?:ой|ая|ое|ие|ую|их|им|ими|ов|ова|ово|овы)|что|чем|чего|как|почему|зачем|отчего|где|куда|откуда|кто|кого|кому|когда|ли|" +
        @"покажи\p{L}*|показать|дай|дайте|сравни\p{L}*|посчитай|подсчитай|рассчитай|выведи|назови|найди|найти|проанализируй|анализ\p{L}*|расскажи|объясни|" +
        @"оцени|проверь|перечисли|список|хватит|хватает|выполнени\p{L}*|динамик\p{L}*|итог\p{L}*|прогноз\p{L}*|топ|рейтинг\p{L}*|статус\p{L}*|" +
        @"nechta|qancha|qaysi|nima|nimaga|nega|qanday|qayerda|qachon|kim|ko'rsat\p{L}*|solishtir\p{L}*|tahlil\p{L}*|hisobla\p{L}*|ayt\p{L}*|bormi|" +
        @"yetadimi|bajarilish\p{L}*|нечта|қанча|канча|қайси|кайси|нима|нега|қандай|кандай|қаерда|каерда|қачон|качон|ким|кўрсат\p{L}*|курсат\p{L}*|" +
        @"солиштир\p{L}*|таҳлил\p{L}*|тахлил\p{L}*|ҳисобла\p{L}*|хисобла\p{L}*|бажарилиш\p{L}*" +
        @")(?![\p{L}])")]
    private static partial Regex DataRequest();

    [GeneratedRegex(
        @"^(?:а|и|ну\s+а|а\s+если|теперь|еще|также|тоже|сравни\s+с|сравнить\s+с|по\s+сравнению|что\s+насчет|va|endi|ва|энди)(?![\p{L}])|" +
        @"\p{L}-?chi\??$|\p{L}-?чи\??$")]
    private static partial Regex FollowUp();

    [GeneratedRegex(@"в\s+компании|по\s+компании|компани\p{L}*\s+в\s+целом|по\s+всем\s+отдел|во\s+всех\s+отдел|все\s+отдел|общая\s+картина|что\s+происходит|kompaniya|компания\s+бўйича|компанияда")]
    private static partial Regex CompanyWide();
}
