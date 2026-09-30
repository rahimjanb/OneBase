using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Gateway;
using OneBase.Application.Abstractions;
using OneBase.Domain.AI;

namespace OneBase.AI.Monitoring;

/// <summary>Пишет каждый вызов модели в ai.Usage со стоимостью по ценам модели из «Настройки → AI → Модели».</summary>
internal sealed class DbUsageRecorder(IServiceScopeFactory scopes) : IAiUsageRecorder
{
    public async Task RecordAsync(AiCallRecord record, CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var price = await db.AiModels.AsNoTracking()
            .Where(m => m.ProviderCode == record.Model.Provider && m.ModelId == record.Model.Model)
            .Select(m => new { m.InputPricePerMillion, m.OutputPricePerMillion })
            .FirstOrDefaultAsync(cancellationToken);

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
            CostUsd = Cost(record.Usage.InputTokens, record.Usage.OutputTokens, price?.InputPricePerMillion, price?.OutputPricePerMillion),
            DurationMs = record.ElapsedMs,
            Success = record.Success,
            Error = record.Error is { Length: > 1000 } e ? e[..1000] : record.Error,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Стоимость, если у модели заданы обе цены; вызов без токенов (ошибка) — 0.</summary>
    internal static decimal? Cost(int input, int output, decimal? inputPrice, decimal? outputPrice) =>
        input == 0 && output == 0 ? 0
        : inputPrice is null || outputPrice is null ? null
        : Math.Round(input / 1_000_000m * inputPrice.Value + output / 1_000_000m * outputPrice.Value, 6);
}
