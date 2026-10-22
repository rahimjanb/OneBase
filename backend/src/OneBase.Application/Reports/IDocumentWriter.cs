namespace OneBase.Application.Reports;

/// <summary>
/// Документ Word (docx) из Markdown, который написал AI: заголовки, абзацы, списки, жирный и курсив, таблицы, цитаты, код.
/// Так Telegram-агент присылает подготовленные письма, планы, разборы и отчёты без Excel — файлом, а не текстом в чат.
/// </summary>
public interface IDocumentWriter
{
    byte[] Write(string title, string markdown, DateTimeOffset createdAt);
}
