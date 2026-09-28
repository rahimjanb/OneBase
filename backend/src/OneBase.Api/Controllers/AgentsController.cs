using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.AI.Agents;
using OneBase.AI.Llm;
using OneBase.Api.Auth;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

[ApiController]
[Route("api/agents")]
[Authorize]
public sealed class AgentsController(IAiDirector director) : ControllerBase
{
    public sealed record TaskRequest(string Instruction);

    [HttpGet]
    public IEnumerable<object> List() =>
        AgentProfiles.All.Select(p => new { p.Code, p.Name, p.DepartmentCode });

    /// <summary>Поставить задачу AI Director — он сам распределит её по отделам.</summary>
    [HttpPost("director/tasks")]
    [HasPermission(Permissions.AgentsRun)]
    public async Task<ActionResult<AgentReply>> Submit(TaskRequest request, CancellationToken ct)
    {
        try
        {
            return await director.SubmitAsync(request.Instruction, User.GetUserId(), ct);
        }
        catch (LlmNotConfiguredException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
