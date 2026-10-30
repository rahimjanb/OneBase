using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Consultant;
using OneBase.Domain.AI;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// История и сообщения консультанта для нового экрана на PostgreSQL (ConsultantDb, без модели): у чата — источник (web / telegram), поиск —
/// по названию и тексту сообщений, у готового ответа — структура блоков, у ошибки — нет; оценка ответа — в журнал аудита, только по своему.
/// </summary>
public class ConsultantChatDbTests(ConsultantDb fixture) : IClassFixture<ConsultantDb>
{
    private const string Report = """
        Вторичка за октябрь выполнила 76,4% плана РОП, по прогнозу месяц закроется на 80,2%.

        ## Факты
        - **Выручка:** 13 446 260 699 сум (−3,8% к сентябрю)
        - **Объём:** 371 148 кг (+5,2% к тем же дням)

        ## Ключевые отклонения
        - **Урикзор и Сеть** — в октябре закупок нет

        ## Рекомендации
        1. **Вернуть Урикзор и Сеть** — встреча с закупщиками
        """;

    private async Task<(Guid Web, Guid Telegram, Guid Answer, Guid Failed)> SeedAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var web = new AiConversation { UserId = fixture.AdminId, Title = "Состояние продаж", LastMessageAt = DateTimeOffset.UtcNow };
        var telegram = new AiConversation { UserId = fixture.AdminId, Title = $"{ConsultantChatService.TelegramTitlePrefix}Salom", LastMessageAt = DateTimeOffset.UtcNow.AddMinutes(-5) };
        db.AiConversations.AddRange(web, telegram);
        db.AiMessages.Add(new AiMessage { ConversationId = web.Id, Role = AiMessageRole.User, Content = "Состояние продаж за октябрь" });
        var answer = new AiMessage { ConversationId = web.Id, Role = AiMessageRole.Assistant, Content = Report, DurationMs = 3300, CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1) };
        var failed = new AiMessage
        {
            ConversationId = telegram.Id, Role = AiMessageRole.Assistant, Status = AiMessageStatus.Failed, Content = string.Empty, Error = "Модель недоступна.",
        };
        db.AiMessages.AddRange(answer, failed);
        await db.SaveChangesAsync();
        return (web.Id, telegram.Id, answer.Id, failed.Id);
    }

    [PostgresFact]
    public async Task History_has_the_source_and_searches_titles_and_message_text()
    {
        var (web, telegram, _, _) = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var chats = scope.ServiceProvider.GetRequiredService<ConsultantChatService>();

        var all = await chats.ListAsync(fixture.AdminId, CancellationToken.None);
        Assert.Equal("web", all.Single(c => c.Id == web).Source);
        Assert.Equal("telegram", all.Single(c => c.Id == telegram).Source);

        // «урикзор» — только в тексте ответа; «SALOM» — в названии, регистр не важен.
        Assert.Equal([web], (await chats.ListAsync(fixture.AdminId, CancellationToken.None, "урикзор")).Select(c => c.Id).Where(id => id == web || id == telegram));
        Assert.Equal([telegram], (await chats.ListAsync(fixture.AdminId, CancellationToken.None, "SALOM")).Select(c => c.Id).Where(id => id == web || id == telegram));
        Assert.DoesNotContain(await chats.ListAsync(fixture.AdminId, CancellationToken.None, "нет такого слова"), c => c.Id == web || c.Id == telegram);
        // Чужие чаты не находятся.
        Assert.Empty(await chats.ListAsync(fixture.NoAccessId, CancellationToken.None, "урикзор"));
    }

    [PostgresFact]
    public async Task Completed_answer_has_report_blocks_and_failed_answer_has_none()
    {
        var (web, telegram, answer, failed) = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var chats = scope.ServiceProvider.GetRequiredService<ConsultantChatService>();

        var view = await chats.GetAsync(fixture.AdminId, web, CancellationToken.None);
        var message = Assert.Single(view!.Messages, m => m.Id == answer);
        Assert.True(message.Answer!.Report);
        Assert.IsType<SummaryBlock>(message.Answer.Blocks[0]);
        Assert.Equal(3300, message.Answer.Meta.LatencyMs);
        Assert.Equal(Report, message.Content); // Markdown ответа — тот же, для старых экранов и Telegram
        Assert.Null(Assert.Single(view.Messages, m => m.Role == "user").Answer);

        var other = await chats.GetAsync(fixture.AdminId, telegram, CancellationToken.None);
        Assert.Null(Assert.Single(other!.Messages, m => m.Id == failed).Answer);
    }

    [PostgresFact]
    public async Task Feedback_goes_to_the_audit_log_only_for_own_answers()
    {
        var (_, _, answer, _) = await SeedAsync();
        using var scope = fixture.Services.CreateScope();
        var chats = scope.ServiceProvider.GetRequiredService<ConsultantChatService>();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();

        Assert.True(await chats.FeedbackAsync(fixture.AdminId, answer, "down", "  Цифра выручки не совпадает с отчётом  ", CancellationToken.None));
        Assert.False(await chats.FeedbackAsync(fixture.NoAccessId, answer, "up", null, CancellationToken.None));
        Assert.False(await chats.FeedbackAsync(fixture.AdminId, Guid.NewGuid(), "up", null, CancellationToken.None));

        var log = await db.AuditLogs.AsNoTracking().Where(l => l.Action == "ai.answer.feedback" && l.EntityId == answer.ToString()).ToListAsync();
        var entry = Assert.Single(log);
        Assert.Equal(fixture.AdminId.ToString(), entry.ActorId);
        var data = System.Text.Json.JsonDocument.Parse(entry.Data!).RootElement;
        Assert.Equal("down", data.GetProperty("rating").GetString());
        Assert.Equal("Цифра выручки не совпадает с отчётом", data.GetProperty("comment").GetString());
    }
}
