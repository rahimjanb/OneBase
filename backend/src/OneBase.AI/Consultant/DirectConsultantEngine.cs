using OneBase.AI.Gateway;
using OneBase.AI.Llm;

namespace OneBase.AI.Consultant;

/// <summary>Консультант без AI-сотрудников и инструментов: отвечает основной моделью, к данным OneBase доступа не имеет.</summary>
internal sealed class DirectConsultantEngine(IAiGateway gateway) : IConsultantEngine
{
    private const string System =
        """
        Ты — главный AI-консультант компании в системе OneBase.
        Сейчас у тебя нет доступа к данным OneBase: ни к продажам, ни к финансам, ни к другим отделам.
        На вопросы о показателях компании отвечай, что в OneBase нет достаточных данных для точного ответа, и объясни, какие данные понадобятся.
        На общие вопросы (методики, как считать показатель, как организовать процесс) отвечай по существу.
        """;

    public async Task<ConsultantAnswer> AnswerAsync(ConsultantTurn turn, Func<ConsultantProgress, Task> progress, CancellationToken cancellationToken)
    {
        await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Running));
        List<LlmMessage> messages = [new(LlmRole.System, $"{System}\n\n{ConsultantPrompts.QualityRules}"), .. turn.History, new(LlmRole.User, turn.Question)];
        var result = await gateway.CompleteAsync(messages, [], new AiCallOptions
        {
            Context = new AiCallContext(turn.UserId, "director", turn.ConversationId, "consultant.answer"),
        }, cancellationToken);
        await progress(new ConsultantProgress("compose", null, null, ProgressStatus.Done));

        return new ConsultantAnswer(
            result.Text?.Trim() ?? string.Empty,
            new ConsultantDetails([], [], result.Model.ToString(), result.UsedFallback),
            result.Usage);
    }
}
