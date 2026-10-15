using OneBase.Domain.Common;

namespace OneBase.Domain.AI;

/// <summary>
/// Отчёт, который AI-консультант сформировал по просьбе пользователя: листы с таблицами (jsonb) — предпросмотр в чате и файл Excel.
/// Виден только автору. Цифры — из инструментов данных OneBase с правами автора на момент создания.
/// </summary>
public class AiReport : Entity
{
    public Guid UserId { get; set; }

    /// <summary>Чат консультанта, в котором сформирован отчёт.</summary>
    public Guid? ConversationId { get; set; }

    /// <summary>AI-сотрудник, собравший отчёт (sales, finance…).</summary>
    public required string AgentCode { get; set; }

    public required string Title { get; set; }

    /// <summary>Отчёт целиком (OneBase.Application.Reports.AiReportContent) в JSON.</summary>
    public required string Content { get; set; }

    public int SheetCount { get; set; }
    public int RowCount { get; set; }
}
