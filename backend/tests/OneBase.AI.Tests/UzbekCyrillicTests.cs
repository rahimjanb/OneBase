using System.Text.Json;
using OneBase.Application.Abstractions;
using OneBase.Application.Telegram.Reports;

namespace OneBase.AI.Tests;

/// <summary>
/// Узбекская кириллица: перевод латиницы по правилам орфографии (oʻ → ў, gʻ → ғ, sh → ш, ya → я, e в начале → э, тутуқ → ъ), заимствования
/// (акция, сентябр, -ция), что остаётся латиницей (бренды, подстановки, идентификаторы, английские слова), язык из Accept-Language и настроек.
/// Те же правила — во frontend (src/i18n/cyrillic.ts); сверка двух реализаций на всём словаре интерфейса — по переменной ONEBASE_CYRILLIC_PARITY.
/// </summary>
public class UzbekCyrillicTests
{
    [Theory]
    [InlineData("O'zbekcha", "Ўзбекча")]
    [InlineData("Ma'lumotlar {date} holatiga", "Маълумотлар {date} ҳолатига")]
    [InlineData("Ma'lumotlar {0} holatiga — to'liq emas: {1}.", "Маълумотлар {0} ҳолатига — тўлиқ эмас: {1}.")]
    [InlineData("TPlarining AKB rejasi", "ТПларининг АКБ режаси")]
    [InlineData("PDFni yuklab olish", "PDFни юклаб олиш")]
    [InlineData("Linko'dagi oylik reja", "Linko'даги ойлик режа")]
    [InlineData("Excelga eksport", "Excelga экспорт")]
    [InlineData("P&L va P50/P95", "P&L ва P50/P95")]
    [InlineData("Ctrl+C bosing", "Ctrl+C босинг")]
    [InlineData("AI Data Manager", "AI Data Manager")]
    [InlineData("F.I.Sh. bo'yicha", "Ф.И.Ш. бўйича")]
    [InlineData("Tekshiring, eng yaxshi", "Текширинг, энг яхши")]
    [InlineData("Aksiyalar sentabr, iyun", "Акциялар сентябрь, июнь")]
    [InlineData("1-sentabrdan, iyunda", "1-сентябрдан, июнда")] // перед окончанием мягкий знак опускается
    [InlineData("Parolni kiriting, yangi parol", "Паролни киритинг, янги пароль")]
    [InlineData("Javob modeli, modellar, filtrlar", "Жавоб модели, моделлар, фильтрлар")]
    [InlineData("Menejerlar kompyuterda, kuryer va klient", "Менеджерлар компьютерда, курьер ва клиент")]
    [InlineData("Matritsa, protsent, obyekt, kesh", "Матрица, процент, объект, кэш")]
    [InlineData("EBIT operatsion foyda", "EBIT операцион фойда")]
    [InlineData("U yerda yo'q", "У ерда йўқ")]
    [InlineData("XATO: SINOV JO'NATMASI", "ХАТО: СИНОВ ЖЎНАТМАСИ")]
    [InlineData("JShShIR va QQS", "ЖШШИР ва ҚҚС")]
    [InlineData("Telegram agenti sales.read huquqi bilan", "Telegram агенти sales.read ҳуқуқи билан")]
    [InlineData("Yangilash 1Base", "Янгилаш 1Base")]
    [InlineData("Reestrga qo'shish", "Реестрга қўшиш")]
    [InlineData("Operatsiyalar va redaksiya", "Операциялар ва редакция")]
    [InlineData("e'lon, eslatma, poeziya", "эълон, эслатма, поэзия")]
    [InlineData("Hafta yakunlari 26.10–31.10.2026 · Сырдарья", "Ҳафта якунлари 26.10–31.10.2026 · Сырдарья")]
    [InlineData("3 199 dan 4 700 kg, so'm, mln, ming", "3 199 дан 4 700 кг, сўм, млн, минг")]
    [InlineData("https://1base.uz/sales va @onebase_bot", "https://1base.uz/sales ва @onebase_bot")]
    [InlineData("Uxlayotgan SN: 30+ kun, g'alaba, Yo'nalish", "Ухлаётган СН: 30+ кун, ғалаба, Йўналиш")]
    [InlineData("SVR · HAFTA 26.10", "СВР · ҲАФТА 26.10")]
    [InlineData("PEER_FLOOD xatosi, Telegram:Enabled=false", "PEER_FLOOD хатоси, Telegram:Enabled=false")]
    [InlineData("EXPORT, ARBUZ, YYYY-MM-DD, OK", "EXPORT, АРБУЗ, YYYY-MM-DD, ОК")]
    public void Latin_becomes_cyrillic_by_the_rules(string latin, string cyrillic) => Assert.Equal(cyrillic, UzbekCyrillic.Convert(latin));

    [Fact]
    public void Words_from_the_russian_original_stay_latin()
    {
        // Имена параметров инструментов («open», «days») есть и в русском оригинале — это не узбекские слова.
        const string ru = "open — открытые (по умолчанию), done — выполненные за days дней";
        Assert.Equal("open — очиқ (стандарт), done — days кунда бажарилган", UzbekCyrillic.Convert("open — ochiq (standart), done — days kunda bajarilgan", ru));
    }

    [Theory]
    [InlineData("uz-Cyrl", "uz-Cyrl")]
    [InlineData("uz-cyrl,ru;q=0.9", "uz-Cyrl")]
    [InlineData("uz", "uz")]
    [InlineData("uz-Latn-UZ", "uz")]
    [InlineData("ru-RU", "ru")]
    [InlineData(null, "ru")]
    public void Interface_language_comes_from_accept_language(string? header, string expected) => Assert.Equal(expected, UiLanguage.Parse(header));

    [Fact]
    public void Report_language_is_normalized_and_texts_follow_it()
    {
        Assert.Equal(("uz-Cyrl", "uz", "ru", (string?)null), (UiLanguage.Normalize("UZ-CYRL"), UiLanguage.Normalize(" uz "), UiLanguage.Normalize("ru"), UiLanguage.Normalize("en")));
        Assert.Equal("ТЕСТОВАЯ ОТПРАВКА", ReportTexts.Get("ru", "ТЕСТОВАЯ ОТПРАВКА"));
        Assert.Equal("SINOV JO'NATMASI", ReportTexts.Get("uz", "ТЕСТОВАЯ ОТПРАВКА"));
        Assert.Equal("СИНОВ ЖЎНАТМАСИ", ReportTexts.Get("uz-Cyrl", "ТЕСТОВАЯ ОТПРАВКА"));
        Assert.Equal("35,4 млн сўм", ReportFormat.Short(35_400_000m, "uz-Cyrl"));
        Assert.Equal("35,4 mln so'm", ReportFormat.Short(35_400_000m, "uz"));
    }

    [Fact]
    public void Excel_texts_follow_the_interface_script()
    {
        UiLanguage.Set("uz-Cyrl");
        try
        {
            Assert.Equal((true, true, "Жами"), (UiLanguage.IsUzbek, UiLanguage.IsCyrillic, UiLanguage.Uzbek("Jami")));
            Assert.Contains("кириллица", UiLanguage.DefaultAnswerLanguage);
        }
        finally
        {
            UiLanguage.Set("ru");
        }

        UiLanguage.Set("uz");
        Assert.Equal((true, false, "Jami"), (UiLanguage.IsUzbek, UiLanguage.IsCyrillic, UiLanguage.Uzbek("Jami")));
        UiLanguage.Set("ru");
    }

    /// <summary>
    /// Сверка с frontend: JSON [русский, латиница, кириллица] по всему словарю интерфейса (собирает frontend/src/i18n/cyrillic.ts). Без переменной
    /// ONEBASE_CYRILLIC_PARITY — пропуск (в CI фронтенда нет).
    /// </summary>
    [Fact]
    public void Same_result_as_the_frontend_on_the_whole_dictionary()
    {
        var path = Environment.GetEnvironmentVariable("ONEBASE_CYRILLIC_PARITY");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        // [русский, латиница, кириллица frontend]
        var rows = JsonSerializer.Deserialize<List<string[]>>(File.ReadAllText(path))!;
        var different = rows.Where(r => UzbekCyrillic.Convert(r[1], r[0]) != r[2]).Select(r => $"{r[1]}\n  ts: {r[2]}\n  cs: {UzbekCyrillic.Convert(r[1], r[0])}").Take(20).ToList();
        Assert.True(rows.Count > 5000, $"словарь: {rows.Count}");
        Assert.True(different.Count == 0, string.Join("\n", different));
    }
}
