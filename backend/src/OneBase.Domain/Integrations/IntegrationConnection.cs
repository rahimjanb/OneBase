namespace OneBase.Domain.Integrations;

/// <summary>
/// Подключение к внешней системе (например, Linko SFA для отдела «Продажи»).
/// Секрет хранится только в зашифрованном виде (ASP.NET Data Protection) и наружу не отдаётся.
/// </summary>
public class IntegrationConnection
{
    /// <summary>Код интеграции: "linko".</summary>
    public required string Code { get; set; }

    /// <summary>Отдел, к которому относится интеграция: "sales".</summary>
    public required string DepartmentCode { get; set; }

    public bool Enabled { get; set; } = true;
    public string? BaseUrl { get; set; }

    /// <summary>Зашифрованный токен.</summary>
    public string? ProtectedSecret { get; set; }

    /// <summary>Последние символы токена — чтобы в интерфейсе было видно, какой токен сохранён.</summary>
    public string? SecretHint { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }

    public DateTimeOffset? LastTestAt { get; set; }
    public bool? LastTestOk { get; set; }
    public string? LastTestMessage { get; set; }
}
