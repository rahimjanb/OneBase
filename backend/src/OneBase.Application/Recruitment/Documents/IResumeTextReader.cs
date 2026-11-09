namespace OneBase.Application.Recruitment.Documents;

/// <summary>
/// Текст документа без AI: Text — что прочитано (null — ничего), Pages — страниц (PDF), Error — почему не прочитан (повреждён, защищён),
/// NeedsOcr — нужен OCR моделью (фото; PDF-скан или PDF, где у половины страниц нет текста).
/// </summary>
public sealed record DocumentText(string? Text, int? Pages, string? Error, bool NeedsOcr);

/// <summary>Текст резюме по типу содержимого (pdf, docx, doc, rtf, odt, txt, jpeg, png, webp) — реализация в Infrastructure (PdfPig и свои разборщики).</summary>
public interface IResumeTextReader
{
    DocumentText Read(byte[] content, string type);
}
