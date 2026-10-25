using OneBase.AI.Providers;

namespace OneBase.AI.Monitoring;

/// <summary>Цены модели за 1 млн токенов из «Настройки → AI → Модели»: вход, выход, чтение кэша промпта, запись в кэш. Null — не задана.</summary>
public sealed record AiModelPrices(decimal? Input, decimal? Output, decimal? CacheRead, decimal? CacheWrite);

/// <summary>Стоимость вызова по четырём тарифам провайдера. Цены вводит администратор по прайсу провайдера — API их не отдаёт.</summary>
public static class AiPricing
{
    /// <summary>
    /// Claude Haiku 5.5 тарифицируется по длине промпта: запрос длиннее 100 000 токенов (вход + кэш) оплачивается по ценам в 5 раз выше
    /// по всем четырём ставкам (прайс Anthropic от 25.10.2026). Каждый запрос считается отдельно.
    /// </summary>
    public const int HaikuLongPromptThreshold = 100_000;

    public const int HaikuLongPromptMultiplier = 5;

    /// <summary>
    /// Стоимость в USD, если у модели заданы нужные цены: вход и выход — всегда; цены кэша — только когда в вызове были кэш-токены.
    /// Вызов без токенов (ошибка) — 0; нет нужной цены — null (в отчёте такой вызов помечается «нет цены», а не считается нулём).
    /// </summary>
    public static decimal? Cost(AiTokenUsage usage, AiModelPrices? prices, string? modelId)
    {
        if (usage.PromptTokens == 0 && usage.OutputTokens == 0)
        {
            return 0;
        }

        if (prices?.Input is null || prices.Output is null)
        {
            return null;
        }

        if ((usage.CacheReadTokens > 0 && prices.CacheRead is null) || (usage.CacheWriteTokens > 0 && prices.CacheWrite is null))
        {
            return null;
        }

        var multiplier = LongPromptMultiplier(modelId, usage.PromptTokens);
        var cost = usage.InputTokens / 1_000_000m * prices.Input.Value
                   + usage.OutputTokens / 1_000_000m * prices.Output.Value
                   + usage.CacheReadTokens / 1_000_000m * (prices.CacheRead ?? 0)
                   + usage.CacheWriteTokens / 1_000_000m * (prices.CacheWrite ?? 0);
        return Math.Round(cost * multiplier, 6);
    }

    /// <summary>Множитель тарифа по длине промпта: 5 для Claude Haiku 5.5 длиннее порога, иначе 1.</summary>
    public static int LongPromptMultiplier(string? modelId, int promptTokens) =>
        modelId is not null && modelId.StartsWith("claude-haiku-5-5", StringComparison.OrdinalIgnoreCase) && promptTokens > HaikuLongPromptThreshold
            ? HaikuLongPromptMultiplier
            : 1;
}
