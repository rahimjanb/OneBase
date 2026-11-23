using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Security;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → Telegram → Автоматические отчёты → Карточки категорий» (telegram.manage): какие категории уходят по умолчанию и свой выбор
/// получателя. Предпросмотр, тест, расписание и получатели — общие запросы автоотчётов (отчёт «11. Итоги дня по категориям», запуски category-day.*).
/// </summary>
[ApiController]
[Route("api/settings/telegram/reports/category-cards")]
[HasPermission(Permissions.TelegramManage)]
public sealed class TelegramCategoryCardsController(CategoryCardService cards) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get(CancellationToken ct) => Run(async () => Ok(await cards.GetAsync(ct)));

    /// <summary>Выбор по умолчанию: пусто — все категории.</summary>
    [HttpPut]
    public Task<IActionResult> SaveDefaults(CategoryCardsInput input, CancellationToken ct) =>
        Run(async () => Ok(await cards.SaveDefaultsAsync(input, User.GetUserId(), ct)));

    /// <summary>Свой выбор получателя: пусто — по умолчанию.</summary>
    [HttpPut("recipients/{id:guid}")]
    public Task<IActionResult> SaveRecipient(Guid id, CategoryCardsInput input, CancellationToken ct) =>
        Run(async () => Ok(await cards.SaveRecipientAsync(id, input, User.GetUserId(), ct)));

    private static async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ReportValidationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(new { error = ex.Message });
        }
    }
}
