using System.Text.Json;
using OneBase.AI.Consultant;

namespace OneBase.AI.Tools;

/// <summary>Контекст вызова инструмента: какой агент, по чьей инициативе, глубина делегирования.</summary>
public sealed record ToolContext(string AgentCode, Guid? UserId, int Depth);

/// <summary>Результат инструмента. Sources — откуда данные (показываются пользователю как источники ответа).</summary>
public sealed record ToolResult(bool Success, string Content, Guid? ApprovalRequestId = null, IReadOnlyList<DataSource>? Sources = null)
{
    public static ToolResult Ok(string content) => new(true, content);
    public static ToolResult Error(string content) => new(false, content);
    public static ToolResult PendingApproval(Guid requestId) =>
        new(false, $"Действие отправлено на подтверждение человеку (запрос {requestId}). Сообщи пользователю, что нужно одобрение.", requestId);
}

/// <summary>
/// Инструмент, доступный агентам только через Tool Registry.
/// Агенты никогда не обращаются к базе или сервисам напрямую — только через инструменты.
/// </summary>
public interface ITool
{
    string Name { get; }
    string Description { get; }

    /// <summary>JSON Schema аргументов.</summary>
    JsonElement InputSchema { get; }

    /// <summary>Критическое действие — всегда требует Human Approval.</summary>
    bool IsCritical { get; }

    /// <summary>Право OneBase, которое нужно пользователю, чтобы AI получил эти данные. null — данных пользователя инструмент не читает.</summary>
    string? RequiredPermission => null;

    /// <summary>Права, любое из которых заменяет RequiredPermission (например, field.plan вместо field.use).</summary>
    IReadOnlyList<string>? AlternativePermissions => null;

    /// <summary>Источник знаний (KnowledgeSources), к которому относится инструмент. null — служебный инструмент.</summary>
    string? Source => null;

    /// <summary>Название для настроек AI.</summary>
    string Title => Name;

    Task<ToolResult> ExecuteAsync(ToolContext context, JsonElement arguments, CancellationToken cancellationToken = default);
}
