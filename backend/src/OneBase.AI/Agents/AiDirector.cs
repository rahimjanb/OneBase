using OneBase.AI.Llm;
using OneBase.AI.Tools;
using OneBase.Application.Abstractions;
using OneBase.Domain.Audit;

namespace OneBase.AI.Agents;

public interface IAiDirector
{
    /// <summary>Задача от пользователя — всегда попадает к AI Director.</summary>
    Task<AgentReply> SubmitAsync(string instruction, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Сообщение между агентами (Agent-to-Agent).</summary>
    Task<AgentReply> SendAsync(AgentMessage message, CancellationToken cancellationToken = default);
}

internal sealed class AiDirector(ILlmClient llm, ToolExecutor executor, IAuditLogger audit) : IAiDirector
{
    /// <summary>Ограничение цепочки делегирования, чтобы агенты не зациклились.</summary>
    private const int MaxDepth = 3;

    public Task<AgentReply> SubmitAsync(string instruction, Guid userId, CancellationToken cancellationToken = default) =>
        SendAsync(new AgentMessage($"user:{userId}", AgentProfiles.DirectorCode, instruction, userId, 0), cancellationToken);

    public async Task<AgentReply> SendAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        var profile = AgentProfiles.Find(message.To);
        if (profile is null)
        {
            return new AgentReply(message.To, $"Агент '{message.To}' не существует.");
        }

        if (message.Depth > MaxDepth)
        {
            return new AgentReply(profile.Code, "Слишком длинная цепочка делегирования, задача остановлена.");
        }

        await audit.LogAsync(ActorType.Agent, message.From, "ai.message.sent", "agent", profile.Code,
            new { message.Depth, message.InitiatedByUserId }, cancellationToken);

        var agent = new LlmAgent(profile, llm, executor);
        return await agent.HandleAsync(message, cancellationToken);
    }
}
