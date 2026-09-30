namespace OneBase.Domain.AI;

/// <summary>
/// Настройки AI-сотрудника (Finance, Sales, Marketing, HR, Production, Supply) или главного консультанта (director).
/// Пустые поля модели — берётся основная модель из общих настроек AI.
/// </summary>
public class AiAgent
{
    public required string Code { get; set; }
    public required string Name { get; set; }

    /// <summary>Роль в компании: «Финансовый директор», «Директор по продажам».</summary>
    public string? Role { get; set; }

    public string? Description { get; set; }

    /// <summary>Инструкция агенту; пусто — инструкция по умолчанию.</summary>
    public string? SystemPrompt { get; set; }

    public string? ProviderCode { get; set; }
    public string? ModelId { get; set; }
    public double? Temperature { get; set; }
    public int? MaxOutputTokens { get; set; }

    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }

    public string? DepartmentCode { get; set; }

    /// <summary>Право OneBase, без которого пользователь не получает ответов этого AI-сотрудника.</summary>
    public string? RequiredPermission { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>Источник знаний, открытый AI-сотруднику: данные продаж, остатки, оплаты, документы…</summary>
public class AiAgentKnowledgeSource
{
    public required string AgentCode { get; set; }
    public required string SourceCode { get; set; }
    public bool Enabled { get; set; } = true;
}
