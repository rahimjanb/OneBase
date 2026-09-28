using System.Text.Json;

namespace OneBase.AI.Tools;

/// <summary>Контекст вызова инструмента: какой агент, по чьей инициативе, глубина делегирования.</summary>
public sealed record ToolContext(string AgentCode, Guid? UserId, int Depth);

public sealed record ToolResult(bool Success, string Content, Guid? ApprovalRequestId = null)
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

    Task<ToolResult> ExecuteAsync(ToolContext context, JsonElement arguments, CancellationToken cancellationToken = default);
}
