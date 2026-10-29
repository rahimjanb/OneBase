using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneBase.AI.Consultant;
using OneBase.AI.Monitoring;
using OneBase.AI.Providers;
using OneBase.Infrastructure.Persistence;

namespace OneBase.AI.Tests;

/// <summary>
/// Замер ответа консультанта на настоящей PostgreSQL: этапы чата и движка (через AsyncLocal) и вызовы инструментов попадают в AI-журнал
/// (ai.AuditLogs.Timings), а текст вопроса и ответа — нет.
/// </summary>
public class AiTimingsDbTests(DataManagerDb fixture) : IClassFixture<DataManagerDb>
{
    [PostgresFact]
    public async Task Answer_timings_go_to_the_ai_journal_without_question_or_answer_text()
    {
        await fixture.ResetAsync();
        fixture.Consultant.Answer = _ =>
        {
            // Как движок: этап и инструмент пишутся в замер, начатый чатом.
            using (AiTurnTimings.Current!.Stage("routing"))
            {
            }

            AiTurnTimings.Current!.Tool(new AiToolTiming("sales", "get_sales", 1, 5, 1234, true, false));
            return new ConsultantAnswer("Ответ: у Хоразма рекорд.", new ConsultantDetails([], [], "fake/consultant", false), new AiTokenUsage(10, 5));
        };

        using var scope = fixture.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<ConsultantChatService>();
        var events = new List<string>();
        await chat.SendAsync(fixture.AdminId, null, "Сколько продал Хоразм?", e =>
        {
            events.Add(e.Type);
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal("done", events[^1]);
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var log = await db.AiAuditLogs.AsNoTracking().Where(l => l.UserId == fixture.AdminId).OrderByDescending(l => l.CreatedAt).FirstAsync();
        Assert.NotNull(log.Timings);
        using var json = JsonDocument.Parse(log.Timings!);
        var stages = json.RootElement.GetProperty("stages").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ToList();
        Assert.Equal(["prepare", "answer.cache", "routing", "save"], stages);
        var tool = Assert.Single(json.RootElement.GetProperty("tools").EnumerateArray());
        Assert.Equal(("get_sales", 1234), (tool.GetProperty("tool").GetString(), tool.GetProperty("chars").GetInt32()));
        Assert.True(json.RootElement.GetProperty("totalMs").GetInt64() >= 0);
        Assert.DoesNotContain("Хоразм", log.Timings);
        Assert.DoesNotContain("рекорд", log.Timings);

        // Вне ответа консультанта замера нет.
        Assert.Null(AiTurnTimings.Current);
    }

    [PostgresFact]
    public async Task Draft_goes_to_the_chat_in_chunks_then_checking_and_done_carries_the_checked_answer()
    {
        await fixture.ResetAsync();
        var pieces = Enumerable.Range(0, 60).Select(i => $"ч{i} ").ToList();
        Func<ConsultantDraft, Task>? seenDraft = null;
        fixture.Consultant.Answer = turn =>
        {
            seenDraft = turn.Draft;
            if (turn.Draft is { } draft)
            {
                foreach (var piece in pieces)
                {
                    draft(new ConsultantDraft(ConsultantDraft.TextKind, piece)).GetAwaiter().GetResult();
                }

                draft(new ConsultantDraft(ConsultantDraft.Checking)).GetAwaiter().GetResult();
            }

            return new ConsultantAnswer("Проверенный ответ.", new ConsultantDetails([], [], "fake/consultant", false), new AiTokenUsage(10, 5));
        };

        using var scope = fixture.Services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<ConsultantChatService>();
        var events = new List<(string Type, JsonElement Data)>();
        await chat.SendAsync(fixture.AdminId, null, "Как продажи?", e =>
        {
            events.Add((e.Type, JsonSerializer.SerializeToElement(e.Data, ConsultantChatService.Json)));
            return Task.CompletedTask;
        }, CancellationToken.None);

        var drafts = events.Where(e => e.Type == "draft").Select(e => e.Data).ToList();
        var texts = drafts.Where(d => d.GetProperty("kind").GetString() == "text").Select(d => d.GetProperty("text").GetString()).ToList();
        Assert.Equal(pieces[0], texts[0]); // первый фрагмент — сразу
        Assert.True(texts.Count < pieces.Count, $"фрагментов {texts.Count} из {pieces.Count}: мелкие куски собираются");
        Assert.Equal(string.Concat(pieces), string.Concat(texts)); // ничего не потеряно и не задвоено
        Assert.Equal("checking", drafts[^1].GetProperty("kind").GetString());
        Assert.Equal(["start", "draft", "done"], events.Select(e => e.Type).Distinct());
        Assert.Equal("done", events[^1].Type);
        Assert.Equal("Проверенный ответ.", events[^1].Data.GetProperty("message").GetProperty("content").GetString());

        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var log = await db.AiAuditLogs.AsNoTracking().Where(l => l.UserId == fixture.AdminId).OrderByDescending(l => l.CreatedAt).FirstAsync();
        using var json = JsonDocument.Parse(log.Timings!);
        Assert.Equal(JsonValueKind.Number, json.RootElement.GetProperty("firstTextMs").ValueKind);

        // Telegram и другие каналы — ответ целиком, без черновика.
        await chat.AskAsync(fixture.AdminId, null, "Как продажи?", new ConsultantChannel("telegram", null, null, null, false), CancellationToken.None);
        Assert.Null(seenDraft);
    }
}
