using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Agents;

namespace OneBase.AI.Tools;

/// <summary>Agent-to-Agent: передать подзадачу другому AI-сотруднику через AI Director.</summary>
internal sealed class DelegateToAgentTool(IServiceProvider services) : ITool
{
    public const string ToolName = "delegate_to_agent";

    public string Name => ToolName;

    public string Description =>
        "Передать подзадачу другому AI-сотруднику и получить его ответ. " +
        "Используй, когда задача относится к зоне ответственности другого отдела.";

    public JsonElement InputSchema { get; } = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            agent = new { type = "string", @enum = AgentProfiles.All.Select(p => p.Code).ToArray() },
            task = new { type = "string", description = "Чёткая формулировка подзадачи" },
        },
        required = new[] { "agent", "task" },
    });

    public bool IsCritical => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext context, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetProperty("agent", out var agentProp) || !arguments.TryGetProperty("task", out var taskProp))
        {
            return ToolResult.Error("Нужны аргументы 'agent' и 'task'.");
        }

        var target = agentProp.GetString() ?? string.Empty;
        if (target == context.AgentCode)
        {
            return ToolResult.Error("Нельзя делегировать задачу самому себе.");
        }

        // Директор резолвится лениво: он сам зависит от инструментов.
        var director = services.GetRequiredService<IAiDirector>();
        var reply = await director.SendAsync(
            new AgentMessage(context.AgentCode, target, taskProp.GetString() ?? string.Empty, context.UserId, context.Depth + 1),
            cancellationToken);

        return ToolResult.Ok(reply.Content);
    }
}
