using OneBase.AI.Llm;
using OneBase.AI.Tools;

namespace OneBase.AI.Agents;

public sealed record AgentMessage(string From, string To, string Content, Guid? InitiatedByUserId, int Depth);

public sealed record AgentReply(string AgentCode, string Content);

/// <summary>Агент с циклом tool calling: LLM решает, какие инструменты вызвать, ToolExecutor контролирует доступ.</summary>
internal sealed class LlmAgent(AgentProfile profile, ILlmClient llm, ToolExecutor executor)
{
    private const int MaxSteps = 8;

    private const string CommonRules =
        "Действуй только через доступные инструменты. Не выдумывай данные. " +
        "Критические действия уходят на подтверждение человеку — прямо сообщай об этом.";

    public async Task<AgentReply> HandleAsync(AgentMessage message, CancellationToken cancellationToken)
    {
        var tools = await executor.GetAllowedToolsAsync(profile.Code, message.InitiatedByUserId, cancellationToken);
        var toolDefinitions = tools.Select(t => new LlmToolDefinition(t.Name, t.Description, t.InputSchema)).ToList();

        var messages = new List<LlmMessage>
        {
            new(LlmRole.System, $"{profile.Mission}\n\n{CommonRules}"),
            new(LlmRole.User, message.Content),
        };

        var context = new ToolContext(profile.Code, message.InitiatedByUserId, message.Depth);

        for (var step = 0; step < MaxSteps; step++)
        {
            var response = await llm.CompleteAsync(messages, toolDefinitions, cancellationToken);
            if (response.ToolCalls.Count == 0)
            {
                return new AgentReply(profile.Code, response.Text ?? string.Empty);
            }

            messages.Add(new LlmMessage(LlmRole.Assistant, response.Text ?? string.Empty, ToolCalls: response.ToolCalls));

            foreach (var call in response.ToolCalls)
            {
                var result = await executor.ExecuteAsync(context, call.Name, call.Arguments, cancellationToken);
                messages.Add(new LlmMessage(LlmRole.Tool, result.Content, ToolCallId: call.Id));
            }
        }

        return new AgentReply(profile.Code, "Превышен лимит шагов агента, задача не завершена.");
    }
}
