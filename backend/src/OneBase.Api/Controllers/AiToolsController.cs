using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Knowledge;
using OneBase.AI.Security;
using OneBase.AI.Tools;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.Audit;

namespace OneBase.Api.Controllers;

/// <summary>«Настройки → AI → Инструменты»: каталог инструментов данных, какие агенты ими пользуются, пробный запуск.</summary>
[ApiController]
[Route("api/ai/tools")]
[HasPermission(Permissions.AiSettingsManage)]
public sealed class AiToolsController(IToolRegistry tools, IAppDbContext db, IUserPermissions permissions, IAuditLogger audit) : ControllerBase
{
    public sealed record RunRequest(JsonElement? Arguments);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var grants = await db.AgentToolGrants.AsNoTracking().Where(g => g.Enabled).ToListAsync(ct);
        return Ok(tools.All
            .Where(t => t.Source is not null)
            .OrderBy(t => t.Source).ThenBy(t => t.Name)
            .Select(t => new
            {
                t.Name,
                t.Title,
                t.Description,
                t.Source,
                SourceName = KnowledgeSources.Find(t.Source!)?.Name,
                t.RequiredPermission,
                t.InputSchema,
                Agents = grants.Where(g => g.ToolName == t.Name).Select(g => g.AgentCode).OrderBy(c => c),
            }));
    }

    /// <summary>Пробный запуск инструмента от имени текущего пользователя (с его правами). Только чтение данных.</summary>
    [HttpPost("{name}/run")]
    public async Task<IActionResult> Run(string name, RunRequest request, CancellationToken ct)
    {
        var tool = tools.Find(name);
        if (tool?.Source is null)
        {
            return NotFound();
        }

        var userId = User.GetUserId();
        if (tool.RequiredPermission is { } required && !(await permissions.GetAsync(userId, ct)).Contains(required))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "У вас нет права на данные этого инструмента." });
        }

        var arguments = request.Arguments is { ValueKind: JsonValueKind.Object } a ? a : JsonSerializer.SerializeToElement(new { });
        var watch = Stopwatch.StartNew();
        var result = await tool.ExecuteAsync(new ToolContext("settings", userId, 0), arguments, ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "ai.tool.tested", "tool", name, new { result.Success }, ct);
        return Ok(new { result.Success, result.Content, result.Sources, ElapsedMs = watch.ElapsedMilliseconds });
    }
}
