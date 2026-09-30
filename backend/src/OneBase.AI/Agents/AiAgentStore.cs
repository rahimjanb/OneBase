using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Gateway;
using OneBase.AI.Tools;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;

namespace OneBase.AI.Agents;

/// <summary>Действующие настройки AI-сотрудника: из БД, пустые поля — по умолчанию.</summary>
public sealed record AgentConfig(
    string Code,
    string Name,
    string? Role,
    string? Description,
    string Prompt,
    bool PromptIsDefault,
    AiModelRef? Model,
    double? Temperature,
    int? MaxOutputTokens,
    bool Enabled,
    int SortOrder,
    string? DepartmentCode,
    string? RequiredPermission,
    IReadOnlyList<string> Sources)
{
    public bool IsConsultant => Code == AgentDefaults.Consultant;
}

public sealed class AiAgentStore(IAppDbContext db)
{
    public async Task<IReadOnlyList<AgentConfig>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.AiAgents.AsNoTracking().ToListAsync(ct);
        var sources = await db.AiAgentKnowledgeSources.AsNoTracking().Where(s => s.Enabled).ToListAsync(ct);
        var codes = rows.Select(r => r.Code).Union(AgentDefaults.All.Select(d => d.Code));
        return codes
            .Select(code => Config(code, rows.FirstOrDefault(r => r.Code == code), rows.Count == 0 ? null : sources.Where(s => s.AgentCode == code).Select(s => s.SourceCode).ToList()))
            .OfType<AgentConfig>()
            .OrderBy(a => a.SortOrder)
            .ToList();
    }

    public async Task<AgentConfig?> GetAsync(string code, CancellationToken ct = default)
    {
        var row = await db.AiAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Code == code, ct);
        var sources = row is null
            ? null
            : await db.AiAgentKnowledgeSources.AsNoTracking().Where(s => s.AgentCode == code && s.Enabled).Select(s => s.SourceCode).ToListAsync(ct);
        return Config(code, row, sources);
    }

    /// <summary>Без строки в БД (сидер ещё не отработал) — настройки по умолчанию.</summary>
    private static AgentConfig? Config(string code, AiAgent? row, IReadOnlyList<string>? sources)
    {
        var d = AgentDefaults.Find(code);
        if (row is null && d is null)
        {
            return null;
        }

        var prompt = !string.IsNullOrWhiteSpace(row?.SystemPrompt) ? row!.SystemPrompt! : d?.Prompt ?? string.Empty;
        return new AgentConfig(
            code,
            row?.Name ?? d!.Name,
            row is null ? d!.Role : row.Role,
            row is null ? d!.Description : row.Description,
            prompt.Trim(),
            string.IsNullOrWhiteSpace(row?.SystemPrompt),
            !string.IsNullOrWhiteSpace(row?.ProviderCode) && !string.IsNullOrWhiteSpace(row.ModelId) ? new AiModelRef(row.ProviderCode, row.ModelId) : null,
            row?.Temperature,
            row?.MaxOutputTokens,
            row?.Enabled ?? true,
            row?.SortOrder ?? d!.SortOrder,
            row is null ? d!.DepartmentCode : row.DepartmentCode,
            row is null ? d!.RequiredPermission : row.RequiredPermission,
            sources ?? d?.Sources ?? []);
    }
}

/// <summary>
/// Создаёт AI-сотрудников, их источники знаний и гранты инструментов по умолчанию.
/// Добавляет только недостающее: выключенное администратором заново не включается.
/// </summary>
public static class AiSeeder
{
    public static async Task SeedAsync(IServiceProvider rootServices, CancellationToken ct = default)
    {
        using var scope = rootServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var tools = scope.ServiceProvider.GetRequiredService<IToolRegistry>().All;

        var agents = await db.AiAgents.Select(a => a.Code).ToListAsync(ct);
        var sources = await db.AiAgentKnowledgeSources.Select(s => new { s.AgentCode, s.SourceCode }).ToListAsync(ct);
        var grants = await db.AgentToolGrants.Select(g => new { g.AgentCode, g.ToolName }).ToListAsync(ct);

        foreach (var d in AgentDefaults.All)
        {
            if (!agents.Contains(d.Code))
            {
                db.AiAgents.Add(new AiAgent
                {
                    Code = d.Code,
                    Name = d.Name,
                    Role = d.Role,
                    Description = d.Description,
                    DepartmentCode = d.DepartmentCode,
                    RequiredPermission = d.RequiredPermission,
                    SortOrder = d.SortOrder,
                });
            }

            foreach (var source in d.Sources.Where(s => !sources.Any(x => x.AgentCode == d.Code && x.SourceCode == s)))
            {
                db.AiAgentKnowledgeSources.Add(new AiAgentKnowledgeSource { AgentCode = d.Code, SourceCode = source });
            }

            foreach (var tool in tools.Where(t => t.Source is { } s && d.Sources.Contains(s)))
            {
                if (!grants.Any(g => g.AgentCode == d.Code && g.ToolName == tool.Name))
                {
                    db.AgentToolGrants.Add(new AgentToolGrant { AgentCode = d.Code, ToolName = tool.Name });
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
