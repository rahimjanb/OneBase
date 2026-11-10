using System.Globalization;

namespace OneBase.Application.Recruitment;

/// <summary>
/// Сообщения кандидату без модели — на его языке и графике: русский, узбекский латиницей, узбекский кириллицей. Латиница не превращается в
/// кириллицу и обратно: у каждого варианта свой текст.
/// </summary>
public static class RecruiterTexts
{
    private static string P(CandidateLang l, string ru, string uzLatn, string uzCyrl) => CandidateLanguages.Pick(l, ru, uzLatn, uzCyrl);

    /// <summary>Время собеседования для кандидата: «12.11 10:00».</summary>
    public static string At(DateTimeOffset localTime) => localTime.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);

    public static string TimeZoneNote(CandidateLang l) => P(l, "время Ташкента", "Toshkent vaqti", "Тошкент вақти");

    /// <summary>Варианты времени: «1) пн 10.11, 10:00» — день недели на языке кандидата.</summary>
    public static string Slots(CandidateLang l, IEnumerable<DateTimeOffset> localStarts) =>
        string.Join('\n', localStarts.Select((s, i) => $"{i + 1}) {CandidateLanguages.Day(l, s.DayOfWeek)} {s:dd.MM}, {s:HH:mm}"));

    public static string ChooseNumber(CandidateLang l) => P(l, "Напишите номер варианта.", "Variant raqamini yozing.", "Вариант рақамини ёзинг.");

    public static string Invitation(CandidateLang l) => P(l,
        "Ваш опыт заинтересовал нас. Хотели бы пригласить вас на собеседование. Какое из предложенных времён вам удобно?",
        "Sizning nomzodingiz bizni qiziqtirdi. Sizni suhbatga taklif qilmoqchimiz. Quyidagi vaqtlardan qaysi biri sizga qulay?",
        "Сизнинг номзодингиз бизни қизиқтирди. Сизни суҳбатга таклиф қилмоқчимиз. Қуйидаги вақтлардан қайси бири сизга қулай?");

    /// <summary>Подсказка к вариантам: можно попросить другое время или отказаться.</summary>
    public static string SlotOptions(CandidateLang l) => P(l,
        "Если ни один не подходит — напишите «другое время».",
        "Hech biri to'g'ri kelmasa — «boshqa vaqt» deb yozing.",
        "Ҳеч бири тўғри келмаса — «бошқа вақт» деб ёзинг.");

    public static string SlotTaken(CandidateLang l) => P(l, "Это время только что заняли. Другие варианты:", "Bu vaqt band bo'lib qoldi. Boshqa variantlar:",
        "Бу вақт банд бўлиб қолди. Бошқа вариантлар:");

    public static string OtherSlots(CandidateLang l) => P(l, "Хорошо, вот другие варианты:", "Mayli, boshqa variantlar:", "Майли, бошқа вариантлар:");

    public static string NoSlots(CandidateLang l) => P(l,
        "Время собеседования с вами согласует сотрудник HR — он напишет здесь.",
        "Suhbat vaqtini HR xodimi siz bilan kelishadi — u shu yerga yozadi.",
        "Суҳбат вақтини HR ходими сиз билан келишади — у шу ерга ёзади.");

    public static string SlotTakenNoOther(CandidateLang l) => P(l,
        "Это время только что заняли. Сотрудник HR предложит вам другое время.",
        "Bu vaqt band bo'lib qoldi. HR xodimi sizga boshqa vaqtni taklif qiladi.",
        "Бу вақт банд бўлиб қолди. HR ходими сизга бошқа вақтни таклиф қилади.");

    /// <summary>Формат и место: «в офисе, адрес: …», «онлайн: ссылка», «по телефону».</summary>
    public static string? Where(CandidateLang l, string format, string? location, string? meetingUrl) => format switch
    {
        "online" => P(l, "Онлайн", "Onlayn", "Онлайн") + (meetingUrl is { Length: > 0 } u ? P(l, $", ссылка: {u}", $", havola: {u}", $", ҳавола: {u}") : string.Empty),
        "phone" => P(l, "По телефону", "Telefon orqali", "Телефон орқали"),
        _ => location is { Length: > 0 } a ? P(l, $"В офисе, адрес: {a}", $"Ofisda, manzil: {a}", $"Офисда, манзил: {a}") : null,
    };

    public static string Booked(CandidateLang l, string at, string? where) =>
        P(l, $"Отлично, собеседование назначено на {at} ({TimeZoneNote(l)}).", $"Ajoyib, suhbat {at} ga belgilandi ({TimeZoneNote(l)}).",
            $"Ажойиб, суҳбат {at} га белгиланди ({TimeZoneNote(l)}).")
        + (where is null ? string.Empty : $" {where}.")
        + P(l, " Напомню за сутки. Если планы изменятся — напишите сюда.", " Bir kun oldin eslataman. Rejalar o'zgarsa — shu yerga yozing.",
            " Бир кун олдин эслатаман. Режалар ўзгарса — шу ерга ёзинг.");

    /// <summary>HR предложил одно время — кандидат подтверждает «да» или просит другое.</summary>
    public static string ConfirmTime(CandidateLang l, string at, string? where) =>
        P(l, $"Предлагаем собеседование {at} ({TimeZoneNote(l)}).", $"Sizga suhbatni {at} da ({TimeZoneNote(l)}) taklif qilamiz.",
            $"Сизга суҳбатни {at} да ({TimeZoneNote(l)}) таклиф қиламиз.")
        + (where is null ? string.Empty : $" {where}.")
        + P(l, " Вам удобно? Ответьте «да» или напишите, какое время подходит.", " Sizga qulaymi? «Ha» deb javob bering yoki qaysi vaqt qulayligini yozing.",
            " Сизга қулайми? «Ҳа» деб жавоб беринг ёки қайси вақт қулайлигини ёзинг.");

    public static string Rescheduled(CandidateLang l, string at, string? where) =>
        P(l, $"Собеседование перенесено на {at} ({TimeZoneNote(l)}).", $"Suhbat {at} ga ko'chirildi ({TimeZoneNote(l)}).",
            $"Суҳбат {at} га кўчирилди ({TimeZoneNote(l)}).")
        + (where is null ? string.Empty : $" {where}.")
        + P(l, " Вам удобно? Ответьте «да» или напишите, какое время подходит.", " Sizga qulaymi? «Ha» deb javob bering yoki qaysi vaqt qulayligini yozing.",
            " Сизга қулайми? «Ҳа» деб жавоб беринг ёки қайси вақт қулайлигини ёзинг.");

    public static string ConfirmedThanks(CandidateLang l, string at) => P(l,
        $"Спасибо, ждём вас {at} ({TimeZoneNote(l)}).", $"Rahmat, sizni {at} da kutamiz ({TimeZoneNote(l)}).", $"Раҳмат, сизни {at} да кутамиз ({TimeZoneNote(l)}).");

    public static string Cancelled(CandidateLang l, string at) => P(l,
        $"Собеседование {at} отменено. Если захотите выбрать другое время — напишите сюда.",
        $"{at} dagi suhbat bekor qilindi. Boshqa vaqtni tanlamoqchi bo'lsangiz, shu yerga yozing.",
        $"{at} даги суҳбат бекор қилинди. Бошқа вақтни танламоқчи бўлсангиз, шу ерга ёзинг.");

    public static string Declined(CandidateLang l) => P(l,
        "Хорошо, приглашение отменено. Если передумаете — напишите сюда.",
        "Tushunarli, taklif bekor qilindi. Fikringiz o'zgarsa, shu yerga yozing.",
        "Тушунарли, таклиф бекор қилинди. Фикрингиз ўзгарса, шу ерга ёзинг.");

    public static string Reminder(CandidateLang l, string at, string? where) =>
        P(l, $"Напоминаем: собеседование {at} ({TimeZoneNote(l)}).", $"Eslatma: suhbat {at} da ({TimeZoneNote(l)}).", $"Эслатма: суҳбат {at} да ({TimeZoneNote(l)}).")
        + (where is null ? string.Empty : $" {where}.")
        + P(l, " Если нужно перенести — напишите сюда.", " Vaqtni o'zgartirish kerak bo'lsa, shu yerga yozing.", " Вақтни ўзгартириш керак бўлса, шу ерга ёзинг.");

    public static string FollowUp(CandidateLang l, string? name, string vacancy) => P(l,
        $"Здравствуйте{(name is null ? string.Empty : $", {name}")}! Удалось подумать о вакансии «{vacancy}»? Если остались вопросы — пишите, я на связи.",
        $"Assalomu alaykum{(name is null ? string.Empty : $", {name}")}! «{vacancy}» vakansiyasi bo'yicha o'ylab ko'rdingizmi? Savollaringiz bo'lsa, yozing — men shu yerdaman.",
        $"Ассалому алайкум{(name is null ? string.Empty : $", {name}")}! «{vacancy}» вакансияси бўйича ўйлаб кўрдингизми? Саволларингиз бўлса, ёзинг — мен шу ердаман.");

    /// <summary>Не приглашаем сами (оценка ниже порога вакансии) — без обещаний и без отказа: решают сотрудники.</summary>
    public static string PassToColleagues(CandidateLang l) => P(l,
        "Спасибо за ответы! Передаю вашу анкету коллегам — они свяжутся с вами здесь.",
        "Javoblaringiz uchun rahmat! Anketangizni hamkasblarimga topshiraman — ular shu yerda siz bilan bog'lanishadi.",
        "Жавобларингиз учун раҳмат! Анкетангизни ҳамкасбларимга топшираман — улар шу ерда сиз билан боғланишади.");

    /// <summary>Шаблон HR «Адрес офиса»: адрес и, если есть, что взять с собой.</summary>
    public static string OfficeAddress(CandidateLang l, string address, string? instructions) =>
        P(l, $"Наш офис: {address}.", $"Ofisimiz manzili: {address}.", $"Офисимиз манзили: {address}.")
        + (string.IsNullOrWhiteSpace(instructions) ? string.Empty : $" {instructions.Trim().TrimEnd('.')}.");

    /// <summary>Шаблон HR «Пригласить на собеседование» — без времени: время HR назначает сам или кандидат выбирает из вариантов.</summary>
    public static string InviteByHr(CandidateLang l, string? vacancy) => P(l,
        $"Приглашаем вас на собеседование{(vacancy is null ? string.Empty : $" на вакансию «{vacancy}»")}. Подскажите, когда вам удобно?",
        $"Sizni{(vacancy is null ? string.Empty : $" «{vacancy}» vakansiyasi bo'yicha")} suhbatga taklif qilamiz. Qachon sizga qulay?",
        $"Сизни{(vacancy is null ? string.Empty : $" «{vacancy}» вакансияси бўйича")} суҳбатга таклиф қиламиз. Қачон сизга қулай?");

    /// <summary>Шаблон HR «Отказ — вежливо».</summary>
    public static string PoliteRejection(CandidateLang l, string? vacancy) => Cap(P(l,
        $"Спасибо за интерес{(vacancy is null ? string.Empty : $" к вакансии «{vacancy}»")} и за время, которое вы нам уделили. К сожалению, сейчас мы не готовы предложить вам эту позицию. Желаем удачи в поиске работы!",
        $"{(vacancy is null ? string.Empty : $"«{vacancy}» vakansiyasiga ")}qiziqishingiz va ajratgan vaqtingiz uchun rahmat. Afsuski, hozircha bu lavozimni taklif qila olmaymiz. Ish qidirishda omad tilaymiz!",
        $"{(vacancy is null ? string.Empty : $"«{vacancy}» вакансиясига ")}қизиқишингиз ва ажратган вақтингиз учун раҳмат. Афсуски, ҳозирча бу лавозимни таклиф қила олмаймиз. Иш қидиришда омад тилаймиз!"));

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    public static string Handoff(CandidateLang l) => P(l,
        "Передаю ваш вопрос сотруднику HR — он скоро ответит вам здесь.",
        "Savolingizni HR xodimiga topshiraman — u tez orada shu yerda javob beradi.",
        "Саволингизни HR ходимига топшираман — у тез орада шу ерда жавоб беради.");

    /// <summary>Резюме получено и обрабатывается — без обещаний, что его уже изучили.</summary>
    public static string ResumeReceived(CandidateLang l) => P(l,
        "Спасибо, я получил ваше резюме. Сейчас оно обрабатывается, и мы свяжемся с вами.",
        "Rezyumeyingizni oldim. Hujjatni ko'rib chiqyapman. Sizga tez orada javob beraman.",
        "Резюмейингизни олдим. Ҳужжатни кўриб чиқяпман. Сизга тез орада жавоб бераман.");

    public static string ResumeLost(CandidateLang l) => P(l,
        "Не получилось получить файл — пришлите его, пожалуйста, ещё раз.",
        "Faylni qabul qila olmadim — iltimos, uni qayta yuboring.",
        "Файлни қабул қила олмадим — илтимос, уни қайта юборинг.");

    public static string ResumeUnreadable(CandidateLang l) => P(l,
        "Не получилось открыть файл — похоже, он повреждён. Пришлите, пожалуйста, резюме ещё раз в PDF, Word или фото.",
        "Faylni ocha olmadim — shikastlangan ko'rinadi. Iltimos, rezyumeni PDF, Word yoki rasm ko'rinishida qayta yuboring.",
        "Файлни оча олмадим — шикастланган кўринади. Илтимос, резюмени PDF, Word ёки расм кўринишида қайта юборинг.");

    public static string FileUnsupported(CandidateLang l) => P(l,
        "Этот формат файла я не открою. Пришлите резюме в PDF, Word (DOC, DOCX) или фото (JPG, PNG).",
        "Bu fayl formatini ocha olmayman. Rezyumeni PDF, Word (DOC, DOCX) yoki rasm (JPG, PNG) ko'rinishida yuboring.",
        "Бу файл форматини оча олмайман. Резюмени PDF, Word (DOC, DOCX) ёки расм (JPG, PNG) кўринишида юборинг.");

    public static string FileTooBig(CandidateLang l, long maxMb) => P(l,
        $"Файл больше {maxMb} МБ — пришлите, пожалуйста, файл поменьше или PDF.",
        $"Fayl {maxMb} MB dan katta — iltimos, kichikroq fayl yoki PDF yuboring.",
        $"Файл {maxMb} МБ дан катта — илтимос, кичикроқ файл ёки PDF юборинг.");

    /// <summary>Кто пишет: «Я виртуальный помощник отдела кадров» / имя и роль из настроек агента.</summary>
    public static string Who(CandidateLang l, string? displayName, string? intro)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return string.IsNullOrWhiteSpace(intro)
                ? P(l, "Я виртуальный помощник отдела кадров", "Men HR bo'limining virtual yordamchisiman", "Мен HR бўлимининг виртуал ёрдамчисиман")
                : P(l, $"Я — {intro}", $"Men — {intro}", $"Мен — {intro}");
        }

        return P(l, $"Меня зовут {displayName}, я {intro ?? "виртуальный помощник отдела кадров"}",
            $"Men {displayName}, {intro ?? "HR bo'limining virtual yordamchisi"}", $"Мен {displayName}, {intro ?? "HR бўлимининг виртуал ёрдамчиси"}");
    }

    /// <summary>Слово согласия, которое просим написать.</summary>
    public static string ConsentWord(CandidateLang l) => P(l, "Согласен", "Roziman", "Розиман");

    public static string Consent(CandidateLang l, string? name, string who, string consentText) => P(l,
        $"Здравствуйте{(name is null ? string.Empty : $", {name}")}! {who}. Спасибо, что написали!\n\n" +
        $"Прежде чем продолжим, нужно ваше согласие на обработку персональных данных:\n{consentText}\n\nЕсли согласны — ответьте «Согласен», и я сразу отвечу на ваш вопрос.",
        $"Assalomu alaykum{(name is null ? string.Empty : $", {name}")}! {who}. Yozganingiz uchun rahmat!\n\n" +
        $"Davom etishdan oldin — ma'lumotlaringizni qayta ishlashga roziligingiz kerak:\n{consentText}\n\nRozi bo'lsangiz, «Roziman» deb javob bering — savolingizga darhol javob beraman.",
        $"Ассалому алайкум{(name is null ? string.Empty : $", {name}")}! {who}. Ёзганингиз учун раҳмат!\n\n" +
        $"Давом этишдан олдин — маълумотларингизни қайта ишлашга розилигингиз керак:\n{consentText}\n\nРози бўлсангиз, «Розиман» деб жавоб беринг — саволингизга дарҳол жавоб бераман.");
}
