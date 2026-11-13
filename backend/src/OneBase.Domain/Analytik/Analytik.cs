using OneBase.Domain.Common;

namespace OneBase.Domain.Analytik;

/// <summary>
/// Таблица (или представление) базы OneBase в каталоге Аналитика. Строку создаёт и обновляет обнаружение схемы (pg_catalog): новые таблицы
/// появляются сами, исчезнувшие помечаются RemovedAt (не удаляются — история и ссылки журнала запросов сохраняются). Title и Description —
/// бизнес-смысл: из знаний OneBase (код), ручного описания администратора или эвристики; Access и RequiredPermission решают, кому таблица
/// доступна в аналитическом движке.
/// </summary>
public class CatalogTable : Entity
{
    public required string Schema { get; set; }
    public required string Name { get; set; }

    /// <summary>table | view | matview | partitioned.</summary>
    public required string Kind { get; set; }

    /// <summary>Бизнес-модуль: sales, finance, field, procurement, production, hr, recruitment, work, telegram, ai, system, twin, incentive, other.</summary>
    public required string Module { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Откуда описание: knowledge (знания OneBase в коде), manual (администратор), auto (по именам колонок).</summary>
    public string DescriptionSource { get; set; } = "auto";

    /// <summary>Комментарий PostgreSQL (COMMENT ON TABLE), если есть.</summary>
    public string? Comment { get; set; }

    /// <summary>Оценка числа строк (pg_class.reltuples; −1 у таблиц без ANALYZE — тогда 0).</summary>
    public long RowEstimate { get; set; }

    /// <summary>Точное число строк — для небольших таблиц (до порога), иначе null.</summary>
    public long? RowCount { get; set; }

    public long SizeBytes { get; set; }
    public int ColumnCount { get; set; }

    /// <summary>open — по праву модуля; personal — есть персональные данные (колонки скрыты); closed — недоступна движку совсем.</summary>
    public required string Access { get; set; }

    /// <summary>Право OneBase, без которого таблица в движке не видна. null при Access = open — достаточно права раздела «Аналитик».</summary>
    public string? RequiredPermission { get; set; }

    /// <summary>Правило ограничения строк (область продаж): sales.region, sales.agent, sales.market… null — без ограничения строк.</summary>
    public string? ScopeRule { get; set; }

    /// <summary>Главная колонка времени (для периода и динамики).</summary>
    public string? DateColumn { get; set; }

    /// <summary>Колонки первичного ключа через запятую.</summary>
    public string? PrimaryKey { get; set; }

    /// <summary>Отпечаток структуры (колонки, типы, ключи): изменился — таблица изменилась.</summary>
    public required string Fingerprint { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? ChangedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }

    /// <summary>Когда обновлялись число строк и размер.</summary>
    public DateTimeOffset? StatsAt { get; set; }

    public List<CatalogColumn> Columns { get; set; } = [];
}

/// <summary>Колонка таблицы каталога: тип, ключ, бизнес-роль (дата, сумма, количество, ссылка…) и чувствительность.</summary>
public class CatalogColumn : Entity
{
    public Guid TableId { get; set; }
    public CatalogTable? Table { get; set; }

    public required string Name { get; set; }
    public int Ordinal { get; set; }
    public required string DataType { get; set; }
    public bool Nullable { get; set; }
    public bool IsPrimaryKey { get; set; }

    /// <summary>Есть индекс, который начинается с этой колонки.</summary>
    public bool Indexed { get; set; }

    /// <summary>Значения уникальны (первичный ключ из одной колонки или уникальный индекс): соединение по ней не размножает строки.</summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Бизнес-роль: id, reference, date, datetime, money, quantity, weight, percent, count, flag, status, name, code, text, json, number, other.
    /// </summary>
    public required string Role { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }

    /// <summary>none | personal (контакты, адреса — скрыты) | secret (пароли, ключи, токены — недоступны никогда) | internal (служебные ключи хранилищ).</summary>
    public required string Sensitivity { get; set; }

    public string? Comment { get; set; }
}

/// <summary>
/// Связь между таблицами: внешний ключ (fk), выведенная по имени колонки (inferred) или заданная описанием (manual). Выведенная и заданная связь
/// используются для соединений только после проверки данными (Status = confirmed): доля значений колонки, которые нашлись в целевой таблице.
/// </summary>
public class CatalogRelation : Entity
{
    public required string FromSchema { get; set; }
    public required string FromTable { get; set; }

    /// <summary>Колонка (или колонки через запятую у составного ключа).</summary>
    public required string FromColumn { get; set; }

    public required string ToSchema { get; set; }
    public required string ToTable { get; set; }
    public required string ToColumn { get; set; }

    /// <summary>
    /// Условие на целевую таблицу для составного справочника: «Kind=division» — строка hr.References вида division. Колонка проверяется по
    /// каталогу, значение уходит параметром. null — без условия.
    /// </summary>
    public string? TargetFilter { get; set; }

    /// <summary>fk | inferred | manual.</summary>
    public required string Kind { get; set; }

    public string? ConstraintName { get; set; }

    /// <summary>confirmed | unverified | rejected.</summary>
    public required string Status { get; set; }

    /// <summary>Доля проверенных значений, найденных в целевой таблице (0–1); у внешнего ключа — 1.</summary>
    public decimal? MatchRatio { get; set; }

    /// <summary>Сколько различных значений проверено.</summary>
    public int? Checked { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
}

/// <summary>Запуск обнаружения схемы: что найдено и что изменилось (Changes — jsonb: added/changed/removed с именами).</summary>
public class CatalogRun : Entity
{
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>startup | schedule | manual.</summary>
    public required string Trigger { get; set; }

    public string? Fingerprint { get; set; }
    public int Tables { get; set; }
    public int Columns { get; set; }
    public int Relations { get; set; }
    public int Added { get; set; }
    public int Changed { get; set; }
    public int Removed { get; set; }

    /// <summary>Схема не изменилась с прошлого запуска — структура не перечитывалась (обновлены только числа строк).</summary>
    public bool Unchanged { get; set; }

    public string? Changes { get; set; }
    public string? Error { get; set; }
    public long DurationMs { get; set; }
    public Guid? StartedById { get; set; }
}

/// <summary>
/// Семантическое описание, которое добавил администратор: смысл таблицы, колонки, связи или показателя. Хранится отдельно от каталога —
/// переживает переобнаружение и пересоздание таблицы. Target: «schema.table», «schema.table.column», связь «schema.table.column>schema.table.column»
/// или код показателя.
/// </summary>
public class CatalogAnnotation : Entity
{
    /// <summary>table | column | relation | metric.</summary>
    public required string Kind { get; set; }

    public required string Target { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Module { get; set; }

    /// <summary>open | personal | closed — переопределяет доступ таблицы (только сужение или явное открытие администратором).</summary>
    public string? Access { get; set; }

    public string? RequiredPermission { get; set; }

    /// <summary>none | personal | secret | internal — переопределяет чувствительность колонки.</summary>
    public string? Sensitivity { get; set; }

    public Guid? UpdatedById { get; set; }
}

/// <summary>
/// Аналитический запрос движка — для проверяемости: кто, откуда (чат, панель, фоновая задача), план, построенный SQL, параметры, таблицы,
/// область данных пользователя, сколько строк и времени, ошибка или отказ.
/// </summary>
public class AnalytikQuery : Entity
{
    public Guid? UserId { get; set; }

    /// <summary>Развёртывание (компания): OneBase однопользовательский по компаниям — значение из Analytik:Tenant.</summary>
    public required string Tenant { get; set; }

    /// <summary>chat | dashboard | api | job.</summary>
    public required string Origin { get; set; }

    public string? AgentCode { get; set; }
    public Guid? ConversationId { get; set; }

    /// <summary>Типизированный план (jsonb).</summary>
    public required string Plan { get; set; }

    public string? Sql { get; set; }

    /// <summary>Значения параметров (jsonb).</summary>
    public string? Parameters { get; set; }

    /// <summary>Использованные таблицы через запятую.</summary>
    public string? Tables { get; set; }

    /// <summary>Применённое ограничение строк (область продаж) — текстом.</summary>
    public string? Scope { get; set; }

    /// <summary>ok | denied | invalid | error | timeout.</summary>
    public required string Status { get; set; }

    public int RowCount { get; set; }
    public bool Truncated { get; set; }
    public long DurationMs { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Находка движка рисков и отклонений: что случилось, масштаб, последствия, рекомендации, доказательства. Причина — только подтверждённая данными
/// (CauseConfirmed); остальное — гипотезы. Key — одна активная находка на правило и объект: повтор обновляет её, исчезнувшее условие — закрывает.
/// NotifiedAt/NotifiedSeverity — кому и когда сообщено: повторное уведомление — только при росте критичности.
/// </summary>
public class AnalytikInsight : Entity
{
    /// <summary>risk | anomaly | quality | opportunity.</summary>
    public required string Kind { get; set; }

    public required string Module { get; set; }
    public required string Rule { get; set; }
    public required string Key { get; set; }

    /// <summary>critical | warning | info.</summary>
    public required string Severity { get; set; }

    public required string Title { get; set; }
    public required string Description { get; set; }
    public string? Cause { get; set; }
    public bool CauseConfirmed { get; set; }

    /// <summary>Гипотезы о причинах (jsonb, массив строк) — не подтверждены данными.</summary>
    public string? Hypotheses { get; set; }

    /// <summary>Масштаб: число и единица (кг, сумы, дни, %, позиций).</summary>
    public decimal? Impact { get; set; }
    public string? ImpactUnit { get; set; }

    public string? Consequences { get; set; }

    /// <summary>Рекомендуемые действия (jsonb, массив строк).</summary>
    public string? Recommendations { get; set; }

    /// <summary>Доказательства (jsonb): показатели, источники, период, ссылки.</summary>
    public string? Evidence { get; set; }

    public string? Href { get; set; }

    /// <summary>Право, без которого находка не показывается.</summary>
    public string? RequiredPermission { get; set; }

    public DateTimeOffset DetectedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public int Occurrences { get; set; } = 1;
    public DateTimeOffset? NotifiedAt { get; set; }
    public string? NotifiedSeverity { get; set; }

    /// <summary>Уверенность в причине после авторасследования (0–1); null — не расследовалась.</summary>
    public decimal? Confidence { get; set; }

    /// <summary>Класс вывода о причине: observed | statistical | tested_causal | assumption.</summary>
    public string? EvidenceClass { get; set; }

    /// <summary>Авторасследование (jsonb): вклад регионов, групп, агентов, проверки, источники.</summary>
    public string? Investigation { get; set; }

    public DateTimeOffset? InvestigatedAt { get; set; }
}

/// <summary>Сделанный прогноз — чтобы потом сравнить с фактом и показать точность прошлых прогнозов.</summary>
public class AnalytikForecast : Entity
{
    /// <summary>Код ряда: sales.revenue, sales.kg, procurement.consumption:{itemId}…</summary>
    public required string Metric { get; set; }

    /// <summary>Разрез (регион, позиция); пусто — итог.</summary>
    public string Dimension { get; set; } = "";

    /// <summary>day | week | month.</summary>
    public required string Granularity { get; set; }

    public required string Method { get; set; }
    public DateOnly MadeOn { get; set; }
    public DateOnly TargetDate { get; set; }
    public int Horizon { get; set; }
    public decimal Predicted { get; set; }
    public decimal Lower { get; set; }
    public decimal Upper { get; set; }
    public decimal? Actual { get; set; }
    public DateTimeOffset? EvaluatedAt { get; set; }
}

/// <summary>
/// Управленческий отчёт: вид, период, содержимое (jsonb — разделы с показателями, таблицами и правом каждого раздела). Разделы, на которые у
/// читателя нет прав, ему не отдаются.
/// </summary>
public class AnalytikReport : Entity
{
    /// <summary>daily | weekly | monthly | sales | production | inventory | procurement | finance | hr | health.</summary>
    public required string Kind { get; set; }

    public required string Title { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public required string Content { get; set; }

    /// <summary>Кто сформировал; null — фоновая задача.</summary>
    public Guid? CreatedById { get; set; }

    public Guid? JobRunId { get; set; }
}

/// <summary>Запуск фоновой задачи Аналитика: одна задача на период (уникально Job + PeriodKey) — повторный запуск того же периода не выполняется.</summary>
public class AnalytikJobRun : Entity
{
    /// <summary>refresh (30 мин) | hourly | daily | weekly | monthly | catalog.</summary>
    public required string Job { get; set; }

    /// <summary>Период: «2026-11-11T16:30», «2026-11-11T16», «2026-11-11», «2026-W46», «2026-11».</summary>
    public required string PeriodKey { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>running | ok | failed.</summary>
    public required string Status { get; set; }

    public string? Summary { get; set; }
    public string? Error { get; set; }
}

/// <summary>Дневное значение показателя (агрегат) — история для трендов, отклонений и прогнозов без пересчёта всей базы.</summary>
public class AnalytikMetricSnapshot : Entity
{
    public DateOnly Date { get; set; }
    public required string Metric { get; set; }

    /// <summary>Разрез; пусто — итог.</summary>
    public string Dimension { get; set; } = "";

    public decimal Value { get; set; }
    public string? Unit { get; set; }
    public DateTimeOffset ComputedAt { get; set; }
}

/// <summary>
/// Снимок исходных данных сценария What-If (симулятор): кто и когда снял, базовый период, дата данных, разделы автора и его область продаж,
/// версия формул, источники и их свежесть, данные (jsonb — ScenarioBaselineData), хеш данных. Pinned — снимок сохранён в сценарии (текущий
/// или прежний расчёт); незакреплённые снимки (черновые расчёты экрана) удаляются через 2 дня.
/// </summary>
public class AnalytikScenarioBaseline : Entity
{
    public required string Tenant { get; set; }
    public Guid? CreatedById { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public DateOnly AsOf { get; set; }
    public required string FormulaVersion { get; set; }

    /// <summary>Разделы, вошедшие в снимок (sales, production, materials, finance, workforce), через запятую.</summary>
    public required string Sections { get; set; }

    /// <summary>Область продаж автора: «организация», регионы или агент — ключ для проверки, покрывают ли права другого пользователя снимок.</summary>
    public required string ScopeKey { get; set; }

    public string? SalesScope { get; set; }
    public required string Data { get; set; }
    public required string Sources { get; set; }
    public required string Hash { get; set; }
    public bool Pinned { get; set; }
}

/// <summary>
/// Сценарий What-If: название, вид, набор (базовый, оптимистичный, пессимистичный, свой), автор, статус (active, archived), снимок исходных
/// данных, горизонт, параметры и результат (jsonb), главный вывод, AI-анализ, версия формул, история пересчётов (jsonb), «скопирован из».
/// Сценарий никогда не меняет бизнес-данные: только читает снимок. Version — xmin.
/// </summary>
public class AnalytikScenario : Entity
{
    public const string Active = "active";
    public const string Archived = "archived";

    public required string Tenant { get; set; }
    public Guid OwnerId { get; set; }
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public required string Preset { get; set; }
    public string Status { get; set; } = Active;
    public Guid BaselineId { get; set; }
    public AnalytikScenarioBaseline? Baseline { get; set; }
    public int HorizonDays { get; set; }
    public string? NewRegionName { get; set; }
    public required string Parameters { get; set; }
    public string? Result { get; set; }
    public string? Headline { get; set; }
    public string? AiAnalysis { get; set; }
    public DateTimeOffset? AiAt { get; set; }
    public DateTimeOffset? CalculatedAt { get; set; }
    public required string FormulaVersion { get; set; }
    public string History { get; set; } = "[]";
    public int Revision { get; set; } = 1;
    public Guid? CopiedFromId { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public uint Version { get; set; }
}
