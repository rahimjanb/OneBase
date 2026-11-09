using OneBase.Application.Recruitment.Documents;
using OneBase.Infrastructure.Telegram;
using Xunit;

namespace OneBase.AI.Tests;

/// <summary>Тип по содержимому и текст документов без AI: DOC, DOCX (таблицы по строкам), RTF, ODT, PDF (страницы, скан), фото.</summary>
public class ResumeDocumentTests
{
    internal static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Resumes");

    internal static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(Folder, name));

    internal static IResumeTextReader Reader() =>
        (IResumeTextReader)Activator.CreateInstance(typeof(TelegramAccountService).Assembly.GetType("OneBase.Infrastructure.Recruitment.Documents.ResumeTextReader")!)!;

    [Theory]
    [InlineData("resume.doc", "doc")]
    [InlineData("resume.docx", "docx")]
    [InlineData("resume.rtf", "rtf")]
    [InlineData("resume.odt", "odt")]
    [InlineData("resume-2pages.pdf", "pdf")]
    [InlineData("resume-scan.pdf", "pdf")]
    [InlineData("resume-photo.jpg", "jpeg")]
    public void Type_comes_from_the_content(string file, string type)
    {
        var check = FileSignatures.Check(Fixture(file), file);
        Assert.True(check.Ok, check.Problem);
        Assert.Equal(type, check.Type);
    }

    [Fact]
    public void Wrong_content_is_rejected_whatever_the_extension()
    {
        Assert.False(FileSignatures.Check(Fixture("resume.docx"), "resume.pdf").Ok); // DOCX под видом PDF
        Assert.Contains("исполняемый", FileSignatures.Check([(byte)'M', (byte)'Z', 0x90, 0, 3, 0, 0, 0], "resume.pdf").Problem);
        Assert.Contains("скрипт", FileSignatures.Check("<html><script>alert(1)</script></html>"u8.ToArray(), "resume.doc").Problem);
        Assert.False(FileSignatures.Check([1, 2, 3, 4, 5, 6, 7, 8, 0, 0], "resume.jpg").Ok);
        // Word сохраняет .doc и в формате DOCX — это не ошибка.
        Assert.Equal("docx", FileSignatures.Check(Fixture("resume.docx"), "resume.doc").Type);
    }

    [Theory]
    [InlineData("resume.doc", "doc")]
    [InlineData("resume-table.docx", "docx")]
    [InlineData("resume.rtf", "rtf")]
    [InlineData("resume.odt", "odt")]
    public void Word_rtf_and_odt_keep_text_and_tables_row_by_row(string file, string type)
    {
        var text = Reader().Read(Fixture(file), type);
        Assert.Null(text.Error);
        Assert.False(text.NeedsOcr);
        Assert.Contains("Каримов Сардор Алишерович", text.Text);
        Assert.Contains("+998 90 123 45 67", text.Text);
        Assert.Contains("Yo‘l", text.Text); // узбекская латиница с ‘
        // Таблица опыта: ячейки строки — в одной строке текста, по порядку.
        var row = text.Text!.Split('\n').Single(l => l.Contains("ООО «Логистика Плюс»"));
        Assert.Matches(@"2019–2024\s*\|\s*ООО «Логистика Плюс»\s*\|\s*Водитель-экспедитор категории C", row);
    }

    [Fact]
    public void Docx_header_and_tabs_are_kept_and_textutil_docx_reads_too()
    {
        var text = Reader().Read(Fixture("resume-table.docx"), "docx").Text!;
        Assert.StartsWith("Резюме · Karimov Sardor", text); // колонтитул — в начале
        Assert.Contains("Навыки:\tзнание города", text);
        Assert.Contains("Ташкентский автомобильно-дорожный техникум", Reader().Read(Fixture("resume.docx"), "docx").Text);
    }

    [Fact]
    public void Pdf_text_is_read_page_by_page_and_a_scan_asks_for_ocr()
    {
        var pdf = Reader().Read(Fixture("resume-2pages.pdf"), "pdf");
        Assert.Equal((2, false), (pdf.Pages, pdf.NeedsOcr));
        Assert.Contains("Каримов Сардор", pdf.Text);
        Assert.Contains("Ташкентский автомобильно-дорожный техникум", pdf.Text); // вторая страница

        var scan = Reader().Read(Fixture("resume-scan.pdf"), "pdf");
        Assert.True(scan.NeedsOcr);
        Assert.Null(scan.Error);

        var photo = Reader().Read(Fixture("resume-photo.jpg"), "jpeg");
        Assert.True(photo.NeedsOcr);
    }

    [Fact]
    public void Corrupt_documents_give_a_clear_error_and_never_throw()
    {
        var doc = Fixture("resume.doc");
        var broken = doc[..2048]; // обрезанный DOC
        Assert.Contains("повреждён", Reader().Read(broken, "doc").Error);
        var docx = Fixture("resume.docx");
        Assert.NotNull(Reader().Read(docx[..(docx.Length / 2)], "docx").Error);
        Assert.NotNull(Reader().Read(Fixture("resume-2pages.pdf")[..500], "pdf").Error);
    }
}
