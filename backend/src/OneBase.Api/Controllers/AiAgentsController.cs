using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Agents;
using OneBase.AI.Gateway;
using OneBase.AI.Knowledge;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.AI.Security;
using OneBase.AI.Tools;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;

namespace OneBase.Api.Controllers;

/// <summary>AI-сотрудники: список, запуск одного сотрудника, настройки («Настройки → AI → Агенты»).</summary>
[ApiController]
[Route("api/ai/agents")]
public sealed class AiAgentsController(
    IAppDbContext db,
    AiAgentStore agents,
    AgentRunner runner,
    IToolRegistry tools,
    IUserPermissions permissions,
    IAuditLogger audit) : ControllerBase
{
    public sealed record ExecuteRequest(string Task);

    public sealed record ToolInput(string Name, bool Enabled, bool RequiresApproval);

    public sealed record AgentInput(
        string Name,
        string? Role,
        string? Description,
        string? SystemPrompt,
        AiSettingsController.ModelRefInput? Model,
        double? Temperature,
        int? MaxOutputTokens,
        bool Enabled,
        string? RequiredPermission,
        IReadOnlyList<string> Sources,
        IReadOnlyList<ToolInput> Tools);

    /// <summary>Подписи прав для выбора «Кому доступен сотрудник».</summary>
    private static readonly Dictionary<string, string> PermissionLabels = new()
    {
        [Permissions.SalesRead] = "Просмотр продаж",
        [Permissions.FinanceRead] = "Финансовые данные",
        [Permissions.HrRead] = "Кадровые данные",
        [Permissions.FilesRead] = "Просмотр файлов",
        [Permissions.AuditRead] = "Журнал аудита",
    };

    [HttpGet]
    [HasPermission(Permissions.AgentsRun)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var own = await permissions.GetAsync(User.GetUserId(), ct);
        var list = await agents.ListAsync(ct);
        return Ok(list.Select(a => new
        {
            a.Code,
            a.Name,
            a.Role,
            a.Description,
            a.Enabled,
            a.IsConsultant,
            Available = a.Enabled && (a.RequiredPermission is null || own.Contains(a.RequiredPermission)),
        }));
    }

    /// <summary>Задача одному AI-сотруднику без консультанта — для проверки и отладки.</summary>
    [HttpPost("{code}/execute")]
    [HasPermission(Permissions.AgentsRun)]
    public async Task<IActionResult> Execute(string code, ExecuteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Task) || request.Task.Length > 4000)
        {
            return BadRequest(new { error = "Задача — от 1 до 4000 символов." });
        }

        if (await agents.GetAsync(code, ct) is not { IsConsultant: false })
        {
            return NotFound();
        }

        try
        {
            var run = await runner.RunAsync(new AgentTask(code, request.Task.Trim(), User.GetUserId()), null, ct);
            await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.agent.executed", "ai_agent", code,
                new { run.Result.Status, run.Result.ToolsUsed, run.Model, run.Usage.InputTokens, run.Usage.OutputTokens }, ct);
            return Ok(new { run.Result, run.Usage, run.Model });
        }
        catch (LlmNotConfiguredException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (AiProviderException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpGet("{code}/settings")]
    [HasPermission(Permissions.AiSettingsManage)]
    public async Task<IActionResult> Settings(string code, CancellationToken ct) =>
        await SettingsViewAsync(code, ct) is { } view ? Ok(view) : NotFound();

    [HttpGet("settings")]
    [HasPermission(Permissions.AiSettingsManage)]
    public async Task<IActionResult> AllSettings(CancellationToken ct)
    {
        var list = await agents.ListAsync(ct);
        var grants = await db.AgentToolGrants.AsNoTracking().Where(g => g.Enabled).ToListAsync(ct);
        return Ok(list.Select(a => new
        {
            a.Code,
            a.Name,
            a.Role,
            a.Description,
            a.Enabled,
            a.IsConsultant,
            a.Model,
            a.RequiredPermission,
            Sources = a.Sources.Count,
            Tools = grants.Count(g => g.AgentCode == a.Code && tools.Find(g.ToolName)?.Source is not null),
            a.PromptIsDefault,
        }));
    }

    [HttpPut("{code}")]
    [HasPermission(Permissions.AiSettingsManage)]
    public async Task<IActionResult> Save(string code, AgentInput input, CancellationToken ct)
    {
        var row = await db.AiAgents.FirstOrDefaultAsync(a => a.Code == code, ct);
        if (row is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100)
        {
            return BadRequest(new { error = "Название — от 1 до 100 символов." });
        }

        if (input.Temperature is < 0 or > 1)
        {
            return BadRequest(new { error = "Температура — от 0 до 1." });
        }

        if (input.MaxOutputTokens is < 256 or > 64000)
        {
            return BadRequest(new { error = "Максимум токенов ответа — от 256 до 64 000." });
        }

        if (input.RequiredPermission is { Length: > 0 } perm && !Permissions.All.Contains(perm))
        {
            return BadRequest(new { error = "Неизвестное право OneBase." });
        }

        if (input.SystemPrompt is { Length: > 20000 })
        {
            return BadRequest(new { error = "Инструкция слишком длинная (больше 20 000 символов)." });
        }

        var unknownSource = input.Sources.FirstOrDefault(s => KnowledgeSources.Find(s) is null);
        if (unknownSource is not null)
        {
            return BadRequest(new { error = $"Неизвестный источник знаний {unknownSource}." });
        }

        var model = string.IsNullOrWhiteSpace(input.Model?.Provider) || string.IsNullOrWhiteSpace(input.Model.Model)
            ? null
            : new AiModelRef(input.Model.Provider.Trim(), input.Model.Model.Trim());
        if (model is not null && !await db.AiModels.AnyAsync(m => m.ProviderCode == model.Provider && m.ModelId == model.Model, ct))
        {
            return BadRequest(new { error = $"Модели {model.Model} нет в списке моделей." });
        }

        var defaults = AgentDefaults.Find(code);
        var prompt = input.SystemPrompt?.Trim();
        row.Name = input.Name.Trim();
        row.Role = input.Role?.Trim();
        row.Description = input.Description?.Trim();
        row.SystemPrompt = string.IsNullOrEmpty(prompt) || prompt == defaults?.Prompt.Trim() ? null : prompt;
        row.ProviderCode = model?.Provider;
        row.ModelId = model?.Model;
        row.Temperature = input.Temperature;
        row.MaxOutputTokens = input.MaxOutputTokens;
        row.Enabled = input.Enabled;
        row.RequiredPermission = string.IsNullOrWhiteSpace(input.RequiredPermission) ? null : input.RequiredPermission;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedById = User.GetUserId();

        var sourceRows = await db.AiAgentKnowledgeSources.Where(s => s.AgentCode == code).ToListAsync(ct);
        foreach (var source in KnowledgeSources.All)
        {
            var enabled = input.Sources.Contains(source.Code);
            var existing = sourceRows.FirstOrDefault(s => s.SourceCode == source.Code);
            if (existing is null)
            {
                if (enabled)
                {
                    db.AiAgentKnowledgeSources.Add(new AiAgentKnowledgeSource { AgentCode = code, SourceCode = source.Code });
                }
            }
            else
            {
                existing.Enabled = enabled;
            }
        }

        var grantRows = await db.AgentToolGrants.Where(g => g.AgentCode == code).ToListAsync(ct);
        foreach (var t in input.Tools)
        {
            if (tools.Find(t.Name) is null)
            {
                continue;
            }

            var grant = grantRows.FirstOrDefault(g => g.ToolName == t.Name);
            if (grant is null)
            {
                if (t.Enabled)
                {
                    db.AgentToolGrants.Add(new AgentToolGrant { AgentCode = code, ToolName = t.Name, RequiresApproval = t.RequiresApproval });
                }
            }
            else
            {
                grant.Enabled = t.Enabled;
                grant.RequiresApproval = t.RequiresApproval;
                grant.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.agent.updated", "ai_agent", code,
            new
            {
                input.Enabled,
                model = model?.ToString(),
                input.Temperature,
                input.MaxOutputTokens,
                promptChanged = row.SystemPrompt is not null,
                input.RequiredPermission,
                input.Sources,
                tools = input.Tools.Where(t => t.Enabled).Select(t => t.Name),
            }, ct);
        return Ok(await SettingsViewAsync(code, ct));
    }

    private async Task<object?> SettingsViewAsync(string code, CancellationToken ct)
    {
        var agent = await agents.GetAsync(code, ct);
        if (agent is null)
        {
            return null;
        }

        var row = await db.AiAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Code == code, ct);
        var grants = await db.AgentToolGrants.AsNoTracking().Where(g => g.AgentCode == code).ToListAsync(ct);
        return new
        {
            agent.Code,
            agent.Name,
            agent.Role,
            agent.Description,
            agent.IsConsultant,
            SystemPrompt = agent.Prompt,
            DefaultPrompt = AgentDefaults.Find(code)?.Prompt.Trim(),
            agent.PromptIsDefault,
            agent.Model,
            agent.Temperature,
            agent.MaxOutputTokens,
            agent.Enabled,
            agent.RequiredPermission,
            UpdatedAt = row?.UpdatedAt,
            Permissions = PermissionLabels.Select(p => new { Code = p.Key, Label = p.Value }),
            Sources = KnowledgeSources.All.Select(s => new
            {
                s.Code,
                s.Name,
                s.Description,
                s.Tables,
                s.Reports,
                s.RequiredPermission,
                Enabled = agent.Sources.Contains(s.Code),
            }),
            Tools = tools.All
                .Where(t => t.Source is not null)
                .OrderBy(t => t.Source).ThenBy(t => t.Name)
                .Select(t =>
                {
                    var grant = grants.FirstOrDefault(g => g.ToolName == t.Name);
                    return new
                    {
                        t.Name,
                        t.Title,
                        t.Description,
                        t.Source,
                        t.RequiredPermission,
                        Enabled = grant?.Enabled ?? false,
                        RequiresApproval = grant?.RequiresApproval ?? false,
                    };
                }),
        };
    }
}
