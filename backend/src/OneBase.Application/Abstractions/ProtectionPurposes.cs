namespace OneBase.Application.Abstractions;

/// <summary>
/// Назначения (purpose) ASP.NET Data Protection для секретов в базе — в одном месте: проверка восстановления расшифровывает каждый вид
/// секрета теми же назначениями, что и код, который его сохранил. Менять строки нельзя: с другим назначением сохранённые секреты не
/// расшифруются.
/// </summary>
public static class ProtectionPurposes
{
    public const string LinkoToken = "OneBase.Integrations.Linko.Token";
    public const string LinkoPlanToken = "OneBase.Integrations.Linko.PlanToken";
    public const string VerifixClientSecret = "OneBase.Integrations.Verifix.ClientSecret";
    public const string TelegramApiHash = "OneBase.Telegram.ApiHash";
    public const string TelegramSession = "OneBase.Telegram.Session";
    public const string TelegramPhone = "OneBase.Telegram.Phone";
    public const string GoogleServiceAccount = "OneBase.DataSources.GoogleServiceAccount";
    public const string FileConnectionPassword = "OneBase.Files.ConnectionPassword";
    public const string FileConnectionLink = "OneBase.Files.ConnectionLink";
    public const string PushVapid = "OneBase.Push.Vapid";
    public const string HhClientSecret = "OneBase.Recruitment.Hh.ClientSecret";
    public const string HhTokens = "OneBase.Recruitment.Hh.Tokens";

    /// <summary>Ключ API провайдера моделей (openai, anthropic) — у каждого провайдера своё назначение.</summary>
    public static string AiProviderKey(string providerCode) => $"OneBase.AI.ProviderKey.{providerCode}";
}
