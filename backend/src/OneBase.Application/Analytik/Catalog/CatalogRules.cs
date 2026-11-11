using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OneBase.Application.Security;

namespace OneBase.Application.Analytik.Catalog;

/// <summary>
/// Доступ к таблице в аналитическом движке: модуль, open/closed, право (несколько — через «|», любое из них) и правило ограничения строк
/// для пользователей с неполной областью продаж (CatalogRules.ScopeKinds).
/// </summary>
public sealed record TablePolicy(string Module, string Access, string? RequiredPermission, string? ScopeRule = null)
{
    public bool Closed => Access == CatalogAccess.Closed;
}

public static class CatalogAccess
{
    public const string Open = "open";
    public const string Personal = "personal";
    public const string Closed = "closed";
}

public static class Sensitivity
{
    public const string None = "none";
    public const string Personal = "personal";
    public const string Secret = "secret";
    public const string Internal = "internal";

    /// <summary>Колонку можно выбирать, фильтровать и группировать в движке.</summary>
    public static bool Visible(string sensitivity) => sensitivity == None;
}

/// <summary>Бизнес-роли колонок.</summary>
public static class ColumnRoles
{
    public const string Id = "id";
    public const string Reference = "reference";
    public const string Date = "date";
    public const string DateTime = "datetime";
    public const string Money = "money";
    public const string Quantity = "quantity";
    public const string Weight = "weight";
    public const string Percent = "percent";
    public const string Count = "count";
    public const string Flag = "flag";
    public const string Status = "status";
    public const string Name = "name";
    public const string Code = "code";
    public const string Text = "text";
    public const string Json = "json";
    public const string Number = "number";
    public const string Other = "other";

    public static bool IsNumeric(string role) => role is Money or Quantity or Weight or Percent or Count or Number;
    public static bool IsTemporal(string role) => role is Date or DateTime;
}

/// <summary>
/// Правила каталога без обращения к базе: модуль и доступ таблицы, чувствительность и бизнес-роль колонки, главная дата, отпечаток структуры.
/// Новая таблица известной схемы получает правила схемы сама (без программирования); новая схема — закрыта, пока администратор не опишет её.
/// </summary>
public static partial class CatalogRules
{
    /// <summary>
    /// Правило ограничения строк — список «вид:колонка» через «;». Виды: branch (филиал Linko), region (sales.Regions.Id), direction
    /// (sales.Directions.Id), market (точка Linko — её филиал и ответственный), agent (ТП, linko.Users.Id), parent (строка видна, если видна
    /// родительская: «parent:OrderId&gt;linko.Orders»), ref (справочник без ограничения). Нет правила — таблица не видна пользователю с неполной
    /// областью продаж.
    /// </summary>
    public static readonly IReadOnlyList<string> ScopeKinds = ["branch", "region", "direction", "market", "agent", "parent", "ref"];

    private static readonly IReadOnlyDictionary<string, string> SalesScopes = new Dictionary<string, string>
    {
        ["linko.Orders"] = "branch:BranchId;agent:AgentId",
        ["linko.OrderReturns"] = "branch:BranchId;agent:AgentId",
        ["linko.OrderLines"] = "parent:OrderId>linko.Orders",
        ["linko.OrderReturnLines"] = "parent:ReturnId>linko.OrderReturns",
        ["linko.Visits"] = "market:MarketId;agent:UserId",
        ["linko.Markets"] = "branch:BranchId;agent:ResponsibleAgentId",
        ["linko.Payments"] = "market:MarketId;agent:UserId",
        ["linko.KpiPlans"] = "agent:UserId",
        ["linko.Users"] = "ref",
        ["linko.Products"] = "ref",
        ["linko.ProductTypes"] = "ref",
        ["linko.PriceLists"] = "ref",
        ["linko.PriceListItems"] = "ref",
        ["linko.Promotions"] = "ref",
        ["linko.PromotionProducts"] = "ref",
        ["linko.Currencies"] = "ref",
        ["linko.Borders"] = "ref",
        ["linko.Providers"] = "ref",
        ["linko.SyncState"] = "ref",
        ["sales.Regions"] = "region:Id",
        ["sales.Directions"] = "direction:Id",
        ["sales.RegionPlans"] = "region:RegionId",
        ["sales.AgentPlans"] = "agent:LinkoUserId",
        ["sales.StaffPlans"] = "agent:LinkoUserId",
        ["sales.Holidays"] = "ref",
        ["sales.Targets"] = "ref",
        ["battle.Goals"] = "agent:AgentId;market:MarketId",
        ["incentive.RouteTargets"] = "agent:AgentId;market:MarketId",
    };

    private const string ProcurementAny = Permissions.ProcurementRead + "|" + Permissions.ProcurementManage;
    private const string ProductionAny = Permissions.ProductionRead + "|" + Permissions.ProductionManage;
    private const string RecruitmentAny = Permissions.HrRecruitmentRead + "|" + Permissions.HrRecruitmentManage;

    /// <summary>Таблицы, закрытые для движка целиком: секреты, сессии, личная переписка и чаты, файлы с правами по папкам.</summary>
    private static readonly HashSet<string> ClosedTables = new(StringComparer.Ordinal)
    {
        "public.__EFMigrationsHistory", "public.DataProtectionKeys", "public.UserSessions", "public.FileLocks", "public.FileConnections",
        "public.FileUserLinks", "public.Integrations", "public.DataSourceConnections", "public.Files", "public.Folders", "public.FileVersions",
        "public.ResourcePermissions",
        "ai.Providers", "ai.ClaudeSwitchBackup", "ai.Conversations", "ai.Messages", "ai.Memory", "ai.DataConversations", "ai.DataMessages",
        "ai.DailyPlans", "ai.Reports", "ai.KnowledgeDocuments", "ai.KnowledgeChunks", "ai.DialogAgentReplies",
        "telegram.Accounts", "telegram.LinkCodes", "telegram.ConversationMessages", "telegram.Conversations", "telegram.IncomingMessages",
        "telegram.OutgoingMessages", "telegram.Chats", "telegram.Materials", "telegram.MaterialChunks", "telegram.UserPermissions",
        "telegram.AgentExecutions", "telegram.CampaignAttachments",
        "recruitment.HhConnection", "recruitment.CandidateContacts", "recruitment.CandidateResumes", "recruitment.CandidateConsents",
        "recruitment.DialogReads", "recruitment.Leads",
        "work.PushKeys", "work.PushSubscriptions", "work.Notifications", "work.CalendarEvents", "work.CalendarEventParticipants",
        "sales.UserScopes",
    };

    /// <summary>Отдельное право таблицы (иначе — право схемы).</summary>
    private static readonly IReadOnlyDictionary<string, (string Module, string? Permission)> TableOverrides = new Dictionary<string, (string, string?)>
    {
        ["linko.Payments"] = ("finance", Permissions.FinanceRead),
        ["sales.PnlValues"] = ("finance", Permissions.FinanceRead),
        ["sales.DealerLedger"] = ("finance", Permissions.FinanceRead),
        ["sales.DealerTerms"] = ("finance", Permissions.FinanceRead),
        ["ai.Alerts"] = ("ai", Permissions.AgentsRun),
        ["ai.DataChangeSets"] = ("ai", Permissions.DataManage),
        ["ai.DataChangeItems"] = ("ai", Permissions.DataManage),
        ["ai.DataFiles"] = ("ai", Permissions.DataManage),
        ["public.Departments"] = ("system", null),
        ["public.Users"] = ("system", Permissions.UsersManage),
        ["public.Roles"] = ("system", Permissions.UsersManage),
        ["public.RolePermission"] = ("system", Permissions.UsersManage),
        ["public.UserRole"] = ("system", Permissions.UsersManage),
        ["public.AuditLogs"] = ("system", Permissions.AuditRead),
        ["public.SystemLogs"] = ("system", Permissions.LogsRead),
        ["public.DataSourceBindings"] = ("system", Permissions.IntegrationsManage),
        ["public.DataSourceSyncRuns"] = ("system", Permissions.IntegrationsManage),
        ["public.DataSourceConflicts"] = ("system", Permissions.IntegrationsManage),
        ["public.ApprovalRequests"] = ("ai", Permissions.ApprovalsDecide),
        ["public.AgentToolGrants"] = ("ai", Permissions.AiSettingsManage),
        ["procurement.AuditLog"] = ("procurement", Permissions.ProcurementManage),
        ["analytik.CatalogTables"] = ("analytik", null),
        ["analytik.CatalogColumns"] = ("analytik", null),
        ["analytik.CatalogRelations"] = ("analytik", null),
        ["analytik.CatalogRuns"] = ("analytik", null),
        ["analytik.Annotations"] = ("analytik", null),
    };

    /// <summary>Модуль и право по схеме. Схемы, которой здесь нет, движок не открывает, пока администратор её не опишет.</summary>
    private static readonly IReadOnlyDictionary<string, (string Module, string? Permission)> Schemas = new Dictionary<string, (string, string?)>
    {
        ["linko"] = ("sales", Permissions.SalesRead),
        ["sales"] = ("sales", Permissions.SalesRead),
        ["battle"] = ("sales", Permissions.SalesRead),
        ["incentive"] = ("sales", Permissions.SalesRead),
        // Sales Base: у агента и супервайзера своя область (FieldScope) — общий движок открывает поле только тем, кто видит всю команду.
        ["field"] = ("field", Permissions.FieldManage),
        ["procurement"] = ("procurement", ProcurementAny),
        ["production"] = ("production", ProductionAny),
        ["hr"] = ("hr", Permissions.HrRead),
        ["recruitment"] = ("hr", RecruitmentAny),
        // Задачи видны по отделам (WorkTaskService) — общий движок отдаёт их только управляющим Аналитиком.
        ["work"] = ("work", Permissions.AnalytikManage),
        ["telegram"] = ("telegram", Permissions.TelegramManage),
        ["ai"] = ("ai", Permissions.AiSettingsManage),
        ["public"] = ("system", Permissions.UsersManage),
        ["twin"] = ("twin", null),
        ["analytik"] = ("analytik", Permissions.AnalytikManage),
    };

    public static TablePolicy Policy(string schema, string table)
    {
        var full = $"{schema}.{table}";
        if (ClosedTables.Contains(full))
        {
            return new TablePolicy(Schemas.TryGetValue(schema, out var s) ? s.Module : "other", CatalogAccess.Closed, null);
        }

        var scope = SalesScopes.GetValueOrDefault(full);
        if (TableOverrides.TryGetValue(full, out var o))
        {
            return new TablePolicy(o.Module, CatalogAccess.Open, o.Permission, scope);
        }

        return Schemas.TryGetValue(schema, out var known)
            ? new TablePolicy(known.Module, CatalogAccess.Open, known.Permission, scope)
            : new TablePolicy("other", CatalogAccess.Closed, null);
    }

    /// <summary>Модули для фильтра каталога (подписи — в интерфейсе).</summary>
    public static readonly IReadOnlyList<string> Modules =
        ["sales", "finance", "field", "procurement", "production", "hr", "work", "telegram", "ai", "system", "twin", "analytik", "other"];

    // ---- чувствительность колонок ----

    [GeneratedRegex(@"^Protected|PasswordHash$|^Password$|Secret$|SecretHash$|TokenHash$|^Token$|AccessToken$|RefreshToken$|PrivateKey$|AccessHash$|CodeHash$|StateHash$|ApiKey$")]
    private static partial Regex SecretName();

    [GeneratedRegex(@"Hint$|ObjectKey$|PhotoKey$|FileKey$|ClientKey$|IdempotencyKey$|SourceHash$|InputHash$|FileHash$|RequestHash$|DedupKey$|PublicKey$")]
    private static partial Regex InternalName();

    [GeneratedRegex(@"^(Phone|Phones|Email|ContactEmail|ServiceAccountEmail|Passport\w*|Pinfl|BirthDate|DateOfBirth|Username|FromUsername|ChatUsername|TelegramUserId|TelegramPeerId|PeerId|ChatPeerId|FromUserId|AccessHash|Lat|Lon|Latitude|Longitude)$")]
    private static partial Regex PersonalName();

    private static readonly HashSet<string> PersonalSchemas = new(StringComparer.Ordinal) { "hr", "recruitment", "public", "telegram" };

    public static string SensitivityOf(string schema, string table, string column)
    {
        if (SecretName().IsMatch(column))
        {
            return Sensitivity.Secret;
        }

        if (InternalName().IsMatch(column))
        {
            return Sensitivity.Internal;
        }

        // Координаты точек продаж — данные бизнеса, а не человека; координаты в остальных схемах (визиты Sales Base с GPS агента) — персональные.
        if ((column is "Lat" or "Lon") && schema == "linko" && table is "Markets")
        {
            return Sensitivity.None;
        }

        if (PersonalName().IsMatch(column))
        {
            return Sensitivity.Personal;
        }

        if (PersonalSchemas.Contains(schema) && column is "Address" or "Note" or "Notes" or "Comment" or "MiddleName")
        {
            return Sensitivity.Personal;
        }

        return Sensitivity.None;
    }

    // ---- бизнес-роли колонок ----

    [GeneratedRegex(@"Percent|Pct$|Rate$|Ratio$|Share$|Yield")]
    private static partial Regex PercentName();

    [GeneratedRegex(@"Weight|Kg$|^Kg|Mass|Netto|Brutto")]
    private static partial Regex WeightName();

    [GeneratedRegex(@"Price|Cost|Sum$|^Sum|TotalPrice|Revenue|Profit|Discount|Budget|Payment|Debt|Expense|Income|Margin|Ebit|Salary|Vat$|Money|Total$")]
    private static partial Regex MoneyName();

    [GeneratedRegex(@"Qty|Quantity|Amount|Balance|OnHand|Reserved|Issued|Received|Declared|Remaining|Returned|Accepted|Defective|Shortage|Output|Packs|Boxes|Pieces")]
    private static partial Regex QuantityName();

    [GeneratedRegex(@"Count$|Days$|Hours$|Minutes$|Seconds$|Ms$|Steps$|Rows$|Tokens$|Attempts$|Occurrences$|^No$|VersionNo$|Order$")]
    private static partial Regex CountName();

    [GeneratedRegex(@"(^|[a-z])(Status|State|Kind|Type|Stage|Level|Mode|Basis|Severity|Category|Source|Channel|Purpose|Role)$")]
    private static partial Regex StatusName();

    [GeneratedRegex(@"(^|[a-z])(Name|Title|FullName|FirstName|LastName|SecondName|MiddleName|Label)$")]
    private static partial Regex NameName();

    [GeneratedRegex(@"(^|[a-z])(Code|Sku|Number|Inn|Key|Barcode|Article|Prefix)$")]
    private static partial Regex CodeName();

    public static string RoleOf(SchemaColumn c, bool primaryKey)
    {
        var type = c.DataType.ToLowerInvariant();
        if (type == "boolean")
        {
            return ColumnRoles.Flag;
        }

        if (type is "jsonb" or "json")
        {
            return ColumnRoles.Json;
        }

        if (primaryKey && (c.Name == "Id" || c.Name.EndsWith("Id", StringComparison.Ordinal)))
        {
            return ColumnRoles.Id;
        }

        if (c.Name.EndsWith("Id", StringComparison.Ordinal) && IsKeyType(type))
        {
            return c.Name == "Id" ? ColumnRoles.Id : ColumnRoles.Reference;
        }

        if (type == "date")
        {
            return ColumnRoles.Date;
        }

        if (type.StartsWith("timestamp", StringComparison.Ordinal))
        {
            return ColumnRoles.DateTime;
        }

        if (IsNumericType(type))
        {
            return PercentName().IsMatch(c.Name) ? ColumnRoles.Percent
                : WeightName().IsMatch(c.Name) ? ColumnRoles.Weight
                : MoneyName().IsMatch(c.Name) ? ColumnRoles.Money
                : QuantityName().IsMatch(c.Name) ? ColumnRoles.Quantity
                : CountName().IsMatch(c.Name) || type is "integer" or "smallint" ? ColumnRoles.Count
                : ColumnRoles.Number;
        }

        if (IsTextType(type))
        {
            return StatusName().IsMatch(c.Name) ? ColumnRoles.Status
                : NameName().IsMatch(c.Name) ? ColumnRoles.Name
                : CodeName().IsMatch(c.Name) ? ColumnRoles.Code
                : ColumnRoles.Text;
        }

        return ColumnRoles.Other;
    }

    public static bool IsKeyType(string type) => type is "bigint" or "integer" or "smallint" or "uuid";

    public static bool IsNumericType(string type) =>
        type is "bigint" or "integer" or "smallint" or "real" or "double precision" or "money" || type.StartsWith("numeric", StringComparison.Ordinal);

    public static bool IsTextType(string type) =>
        type is "text" or "citext" || type.StartsWith("character varying", StringComparison.Ordinal) || type.StartsWith("character", StringComparison.Ordinal);

    /// <summary>Главная колонка времени таблицы: по известным именам, иначе первая дата, иначе первая отметка времени, кроме служебных.</summary>
    public static string? DateColumnOf(SchemaTable t)
    {
        string[] preferred =
        [
            "CreatedDate", "Date", "Day", "WorkDate", "DocDate", "AcceptedDate", "PlannedDate", "ShiftDate", "ReceivedOn", "DeliveryDate",
            "ExpectedDate", "PeriodStart", "StartsAt", "DetectedAt", "OccurredAt", "StartedAt", "CreatedAt",
        ];
        foreach (var name in preferred)
        {
            if (t.Column(name) is { } c && (c.DataType == "date" || c.DataType.StartsWith("timestamp", StringComparison.Ordinal)))
            {
                return c.Name;
            }
        }

        return t.Columns.FirstOrDefault(c => c.DataType == "date")?.Name
            ?? t.Columns.FirstOrDefault(c => c.DataType.StartsWith("timestamp", StringComparison.Ordinal)
                && c.Name is not ("UpdatedAt" or "LastSyncedAt" or "LastSeenAt" or "ArchivedAt"))?.Name;
    }

    /// <summary>«OrderReturnLines» → «order return lines» — подпись без описания.</summary>
    public static string Words(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (i > 0 && char.IsUpper(ch) && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
            {
                sb.Append(' ');
            }

            sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString().Replace('_', ' ').Trim();
    }

    /// <summary>Отпечаток структуры таблицы: колонки, типы, NULL, первичный и внешние ключи.</summary>
    public static string Fingerprint(SchemaTable t)
    {
        var sb = new StringBuilder();
        sb.Append(t.Kind).Append('|');
        foreach (var c in t.Columns.OrderBy(c => c.Ordinal))
        {
            sb.Append(c.Name).Append(':').Append(c.DataType).Append(c.Nullable ? "?" : "").Append(';');
        }

        sb.Append("|pk:").Append(string.Join(",", t.PrimaryKey));
        foreach (var fk in t.ForeignKeys.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            sb.Append("|fk:").Append(string.Join(",", fk.Columns)).Append('>').Append(fk.RefSchema).Append('.').Append(fk.RefTable);
        }

        return Hash(sb.ToString());
    }

    /// <summary>Отпечаток всей схемы — по отпечаткам таблиц.</summary>
    public static string Fingerprint(IEnumerable<SchemaTable> tables) =>
        Hash(string.Join("\n", tables.OrderBy(t => t.FullName, StringComparer.Ordinal).Select(t => $"{t.FullName}={Fingerprint(t)}")));

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
}
