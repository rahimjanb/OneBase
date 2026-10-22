using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Reports;

/// <summary>
/// Подписи картинок и текстов автоотчётов: ключ — русский текст (как в интерфейсе OneBase), узбекский (латиница) — перевод. Имена агентов,
/// регионов и команд не переводятся — как в Linko и Sales Base. Нет перевода — русский текст.
/// </summary>
public static class DailyReportTexts
{
    private static readonly Dictionary<string, string> Uz = new()
    {
        ["ПЛАН НА ДЕНЬ"] = "KUNLIK REJA",
        ["ПЛАН / ФАКТ"] = "REJA / FAKT",
        ["ИТОГИ ДНЯ"] = "KUN YAKUNI",
        ["План на день"] = "Kunlik reja",
        ["План / факт"] = "Reja / fakt",
        ["Итоги дня"] = "Kun yakuni",
        ["Дата"] = "Sana",
        ["Сформирован"] = "Tuzilgan",
        ["РМ"] = "RM",
        ["Команда"] = "Jamoa",
        ["Общий план региона"] = "Hudud umumiy rejasi",
        ["План / факт региона"] = "Hudud rejasi / fakti",
        ["Итоги региона"] = "Hudud yakuni",
        ["Итог региона"] = "Hudud yakuni",
        ["План продаж"] = "Savdo rejasi",
        ["Факт продаж"] = "Savdo fakti",
        ["Выполнение"] = "Bajarilish",
        ["Отклонение"] = "Farq",
        ["План визитов"] = "Tashriflar rejasi",
        ["Визиты: факт / план"] = "Tashriflar: fakt / reja",
        ["Супервайзеров"] = "Supervayzerlar",
        ["Агентов"] = "Agentlar",
        ["Супервайзер"] = "Supervayzer",
        ["Агент"] = "Agent",
        ["План"] = "Reja",
        ["Факт"] = "Fakt",
        ["Откл."] = "Farq",
        ["Визиты"] = "Tashrif",
        ["План, сум"] = "Reja, so'm",
        ["Факт, сум"] = "Fakt, so'm",
        ["План, кг"] = "Reja, kg",
        ["Факт, кг"] = "Fakt, kg",
        ["сум"] = "so'm",
        ["кг"] = "kg",
        ["Итого по команде"] = "Jamoa bo'yicha jami",
        ["Итого по региону"] = "Hudud bo'yicha jami",
        ["Требуют внимания"] = "E'tibor talab qiladi",
        ["Лучшие результаты"] = "Eng yaxshi natijalar",
        ["Не выполнили план"] = "Rejani bajarmaganlar",
        ["нет данных"] = "ma'lumot yo'q",
        ["нет плана"] = "reja yo'q",
        ["ТЕСТОВЫЕ ДАННЫЕ"] = "TEST MA'LUMOTLARI",
        ["ПРОВЕРКА НА РЕАЛЬНЫХ ДАННЫХ"] = "HAQIQIY MA'LUMOTLARDA TEKSHIRUV",
        ["Имена и цифры — демонстрационные, не данные сотрудников"] = "Ismlar va raqamlar — namunaviy, xodimlar ma'lumotlari emas",
        ["Стр. {0} из {1}"] = "{0}/{1}-sahifa",
        ["продолжение"] = "davomi",
        ["Источники"] = "Manbalar",
        ["данные на {0}"] = "{0} holatiga",
        ["нет заказов и визитов"] = "buyurtma va tashrif yo'q",
        ["{0}% дневного плана"] = "kunlik rejaning {0}%",
        ["причина не зафиксирована"] = "sabab qayd etilmagan",
        ["ниже {0}% дневного плана или нет работы"] = "kunlik rejaning {0}% dan past yoki ish yo'q",
        ["без плана в источнике: {0}"] = "manbada rejasi yo'q: {0}",
        ["В команде нет агентов"] = "Jamoada agent yo'q",
        ["Планы — Linko (staff_balance), месячный ÷ рабочие дни (пн–сб)"] = "Rejalar — Linko (staff_balance), oylik ÷ ish kunlari (du–sha)",
        ["Продажи — заказы, принятые агентом за день (Linko)"] = "Savdo — agent kun davomida qabul qilgan buyurtmalar (Linko)",
        ["Визиты — план Linko; факт — Linko и Sales Base"] = "Tashriflar — Linko rejasi; fakt — Linko va Sales Base",
        ["Состав команд — Sales Base"] = "Jamoalar tarkibi — Sales Base",
        ["пропущено точек: {0}"] = "o'tkazib yuborilgan nuqtalar: {0}",
        ["отказов: {0}"] = "rad etishlar: {0}",
        ["закрыто: {0}"] = "yopiq: {0}",
        ["нет ЛПР: {0}"] = "qaror qiluvchi yo'q: {0}",
        ["Нет данных о плане в Linko у агентов: {0}"] = "Linko'da rejasi yo'q agentlar: {0}",
        ["План визитов Linko на этот день не загружен — визиты без плана"] = "Bu kun uchun Linko tashrif rejasi yuklanmagan — tashriflar rejasiz",
        ["Агент учтён один раз: {0} (две карточки на одного сотрудника Linko)"] = "Agent bir marta hisoblandi: {0} (bitta Linko xodimiga ikki karta)",
        ["Агенты без сотрудника Linko — без плана и факта: {0}"] = "Linko xodimi biriktirilmagan agentlar — reja va faktsiz: {0}",
        ["Факт — по данным Linko на {0}; позже принятые заказы войдут в следующий отчёт"] = "Fakt — Linko'ning {0} holatidagi ma'lumotlari; keyin qabul qilingan buyurtmalar keyingi hisobotga kiradi",
        ["Региональный менеджер"] = "Hududiy menejer",
        ["Примечания"] = "Izohlar",
        ["Продажи за этот день ещё не загружены (последнее обновление {0}) — факт не показан"] = "Bu kun savdosi hali yuklanmagan (oxirgi yangilanish {0}) — fakt ko'rsatilmagan",
        ["Продажи за этот день ещё не загружены — факт не показан"] = "Bu kun savdosi hali yuklanmagan — fakt ko'rsatilmagan",
        ["План визитов на этот день не загружен"] = "Bu kun uchun tashriflar rejasi yuklanmagan",
        ["В регионе нет агентов за этот месяц"] = "Bu oyda hududda agentlar yo'q",
        ["Нет плана у агентов: {0}"] = "Rejasi yo'q agentlar: {0}",
        ["Регион, супервайзер и агенты — «Продажи» OneBase"] = "Hudud, supervayzer va agentlar — OneBase «Savdo»",
        ["не указан"] = "ko'rsatilmagan",
        ["Итого"] = "Jami",
        ["Без плана"] = "Rejasiz",
        ["и ещё {0}"] = "yana {0}",
        ["План визитов, шт"] = "Tashriflar rejasi",
        ["Факт / план визитов"] = "Tashriflar fakt / reja",
        ["Сформирован {0}"] = "Tuzilgan {0}",
        ["без супервайзера"] = "supervayzersiz",
    };

    public static string Get(string language, string ru) => language == "uz" && Uz.TryGetValue(ru, out var uz) ? uz : ru;

    public static string Format(string language, string ru, params object[] args) => string.Format(System.Globalization.CultureInfo.InvariantCulture, Get(language, ru), args);

    /// <summary>Заголовок картинки: ПЛАН НА ДЕНЬ / ПЛАН / ФАКТ / ИТОГИ ДНЯ.</summary>
    public static string Title(TelegramReportKind kind, string language) => Get(language, kind switch
    {
        TelegramReportKind.Morning => "ПЛАН НА ДЕНЬ",
        TelegramReportKind.Midday => "ПЛАН / ФАКТ",
        _ => "ИТОГИ ДНЯ",
    });

    /// <summary>Название вида отчёта обычным регистром: «План на день».</summary>
    public static string Name(TelegramReportKind kind, string language) => Get(language, kind switch
    {
        TelegramReportKind.Morning => "План на день",
        TelegramReportKind.Midday => "План / факт",
        _ => "Итоги дня",
    });

    public static string Unit(ReportMetric metric, string language) => Get(language, metric == ReportMetric.Kg ? "кг" : "сум");
}
