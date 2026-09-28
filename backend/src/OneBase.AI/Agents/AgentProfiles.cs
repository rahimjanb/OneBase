namespace OneBase.AI.Agents;

public sealed record AgentProfile(string Code, string Name, string? DepartmentCode, string Mission);

/// <summary>AI-сотрудники OneBase: AI Director и отделы под ним.</summary>
public static class AgentProfiles
{
    public const string DirectorCode = "director";

    public static readonly AgentProfile Director = new(DirectorCode, "AI Director", null,
        "Ты — AI Director компании. Разбираешь задачу, решаешь, какие отделы задействовать, " +
        "делегируешь им подзадачи и собираешь итоговый ответ.");

    public static readonly IReadOnlyList<AgentProfile> Departments =
    [
        new("hr", "HR AI", "hr", "Ты — HR-сотрудник: найм, адаптация, кадровые документы, отпуска."),
        new("sales", "Sales AI", "sales", "Ты — сотрудник отдела продаж: лиды, сделки, коммерческие предложения, клиенты."),
        new("production", "Production AI", "production", "Ты — сотрудник производства: планирование, загрузка мощностей, контроль выпуска."),
        new("finance", "Finance AI", "finance", "Ты — финансист: бюджет, платежи, счета, отчётность."),
        new("supply", "Supply AI", "supply", "Ты — снабженец: закупки, поставщики, складские остатки."),
        new("marketing", "Marketing AI", "marketing", "Ты — маркетолог: кампании, контент, аналитика каналов."),
    ];

    public static readonly IReadOnlyList<AgentProfile> All = [Director, .. Departments];

    public static AgentProfile? Find(string code) => All.FirstOrDefault(p => p.Code == code);
}
