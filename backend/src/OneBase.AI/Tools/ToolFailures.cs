using System.Text.Json;
using System.Text.RegularExpressions;
using OneBase.Application.Field;

namespace OneBase.AI.Tools;

/// <summary>
/// Виды сбоев инструментов и безопасные тексты для модели и пользователя. Сообщения доменных исключений OneBase («Период — не больше 62 дней»,
/// «Регион не найден») пишутся для людей — их можно показать; текст прочих исключений (база, сеть, провайдер) может содержать служебные
/// подробности — вместо него общий текст, а само исключение — в журнал сервера.
/// </summary>
public static partial class ToolFailures
{
    /// <summary>Нет доступа у пользователя.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>Источник или инструмент не подключён (нет гранта, источник закрыт для сотрудника, данных в источнике нет совсем).</summary>
    public const string NotConnected = "not_connected";

    /// <summary>Запрошенная запись не найдена.</summary>
    public const string NotFound = "not_found";

    /// <summary>Неверные аргументы — модель исправит вызов.</summary>
    public const string InvalidArguments = "invalid_arguments";

    /// <summary>Период вне доступного: длиннее предела («Период — не больше 62 дней») или месяц, за который данных ещё нет.</summary>
    public const string OutOfRange = "out_of_range";

    /// <summary>Инструмент завершился ошибкой — данных нет из-за сбоя, а не потому, что их нет.</summary>
    public const string ToolError = "tool_error";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private const string Internal = "Инструмент завершился внутренней ошибкой OneBase — его данные не получены.";

    /// <summary>Вид сбоя по исключению.</summary>
    public static string KindOf(Exception ex) => ex switch
    {
        FieldForbiddenException or UnauthorizedAccessException => Forbidden,
        FieldNotFoundException or KeyNotFoundException => NotFound,
        FieldValidationException or ArgumentException when IsOutOfRange(ex.Message) => OutOfRange,
        FieldValidationException or ArgumentException => InvalidArguments,
        _ => ToolError,
    };

    /// <summary>
    /// Текст ошибки — об ограничении периода: «Период — не больше 62 дней», «Отрезок — не длиннее 31 дня», «Отрезок — в пределах одного месяца»,
    /// «Данных о продажах за июнь 2025 в OneBase нет. Есть: …», «… за этот период не построены».
    /// </summary>
    public static bool IsOutOfRange(string? message) => message is { Length: > 0 } m && PeriodLimit().IsMatch(m);

    [GeneratedRegex(@"(?:Период|Отрезок)\s+—\s+(?:не\s+больше|не\s+длиннее|в\s+пределах)|за\s+\p{L}+\s+\d{4}\s+в\s+OneBase\s+нет|за\s+этот\s+период\s+не\s+построен", RegexOptions.IgnoreCase)]
    private static partial Regex PeriodLimit();

    /// <summary>Текст сбоя без служебных подробностей: сообщение доменного исключения OneBase или общий текст.</summary>
    public static string SafeMessage(Exception ex) =>
        KindOf(ex) == ToolError || string.IsNullOrWhiteSpace(ex.Message) || ex is ArgumentNullException ? Internal : ex.Message;

    /// <summary>Что делать модели после сбоя.</summary>
    public static string Next(string kind) => kind switch
    {
        Forbidden => "Не используй и не угадывай эти данные; скажи, что у пользователя нет к ним доступа.",
        NotConnected => "Попробуй другой инструмент с теми же условиями; если его нет — скажи, что источник не подключён.",
        NotFound => "Проверь название, период и фильтры (найди запись поиском по справочнику) и попробуй снова.",
        OutOfRange => "Разбей период на части или возьми доступный период из сообщения; если нельзя — объясни ограничение периода.",
        InvalidArguments => "Исправь аргументы и вызови инструмент снова.",
        _ => "Попробуй другой инструмент с теми же условиями или сузь запрос; если других нет — скажи, что источник временно недоступен, а не что данных нет.",
    };
}
