using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Gateway;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;

namespace OneBase.AI.Monitoring;

/// <summary>Пишет каждый вызов модели в ai.Usage со стоимостью по ценам модели из «Настройки → AI → Модели».</summary>
internal sealed class DbUsageRecorder(IServiceScopeFactory scopes, IAiLimits limits) : IAiUsageRecorder
{
    public async Task RecordAsync(AiCallRecord record, CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var prices = await db.AiModels.AsNoTracking()
            .Where(m => m.ProviderCode == record.Model.Provider && m.ModelId == record.Model.Model)
            .Select(m => new AiModelPrices(m.InputPricePerMillion, m.OutputPricePerMillion, m.CacheReadPricePerMillion, m.CacheWritePricePerMillion))
            .FirstOrDefaultAsync(cancellationToken);

        var cost = AiPricing.Cost(record.Usage, prices, record.Model.Model);
        db.AiUsage.Add(new AiUsage
        {
            UserId = record.Context.UserId,
            AgentCode = record.Context.AgentCode,
            ConversationId = record.Context.ConversationId,
            Purpose = record.Context.Purpose,
            Provider = record.Model.Provider,
            Model = record.Model.Model,
            InputTokens = record.Usage.InputTokens,
            OutputTokens = record.Usage.OutputTokens,
            CacheReadTokens = record.Usage.CacheReadTokens,
            CacheWriteTokens = record.Usage.CacheWriteTokens,
            CostUsd = cost,
            DurationMs = record.ElapsedMs,
            Success = record.Success,
            Error = record.Error is { Length: > 1000 } e ? e[..1000] : record.Error,
        });
        await db.SaveChangesAsync(cancellationToken);

        // Траты против бюджета: обновить и при переходе порога уведомить администраторов AI.
        if (cost is > 0)
        {
            await limits.SpentAsync(cost.Value, cancellationToken);
        }
    }
}
