using Microsoft.Extensions.Configuration;

namespace OneBase.AI.Gateway;

/// <summary>
/// Защита от платных вызовов моделей для проверок и стендов (конфигурация процесса, не настройки в базе — их не трогает).
/// Ai:Safety:EmulatorUrl — все провайдеры идут на этот адрес с ключом-заглушкой: настоящий ключ (из OneBase или окружения) не читается
/// вовсе, и сбой или отсутствие эмулятора — ошибка, а не вызов настоящего API. Ai:Safety:BlockPaidCalls — провайдеры выключены (без
/// эмулятора ни одного вызова модели). Ai:Safety:MaxCalls / MaxCostUsd — сколько вызовов и долларов процесс может потратить за время
/// работы (для разрешённых платных проверок): дошли — новые вызовы отклоняются, как при исчерпанном бюджете.
/// </summary>
public sealed record AiSafetyOptions(string? EmulatorUrl = null, bool BlockPaidCalls = false, int? MaxCalls = null, decimal? MaxCostUsd = null)
{
    public const string EmulatorKey = "emulator-no-real-key";

    public static readonly AiSafetyOptions None = new();

    public bool Emulator => !string.IsNullOrWhiteSpace(EmulatorUrl);

    public static AiSafetyOptions From(IConfiguration config)
    {
        var url = config["Ai:Safety:EmulatorUrl"]?.Trim();
        if (!string.IsNullOrEmpty(url) && !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            // Неверный адрес эмулятора — не повод звонить настоящему API: все вызовы блокируются.
            return new AiSafetyOptions(null, true);
        }

        return new AiSafetyOptions(
            string.IsNullOrEmpty(url) ? null : url.TrimEnd('/'),
            bool.TryParse(config["Ai:Safety:BlockPaidCalls"], out var block) && block,
            int.TryParse(config["Ai:Safety:MaxCalls"], out var calls) && calls >= 0 ? calls : null,
            decimal.TryParse(config["Ai:Safety:MaxCostUsd"], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture,
                out var cost) && cost >= 0 ? cost : null);
    }

    /// <summary>Подключение провайдера с учётом защиты: эмулятор — его адрес и заглушка ключа; блокировка — провайдер выключен.</summary>
    public AiProviderState Apply(AiProviderState state) =>
        Emulator
            ? state with { BaseUrl = EmulatorUrl!, CustomBaseUrl = true, Enabled = true, ApiKey = EmulatorKey, KeySource = AiKeySource.Environment, KeyHint = "эмулятор" }
            : BlockPaidCalls
                ? state with { Enabled = false, ApiKey = string.Empty, KeySource = AiKeySource.None, KeyHint = null }
                : state;
}
