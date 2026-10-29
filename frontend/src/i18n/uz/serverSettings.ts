import type { Dict } from "../core";

/**
 * Тексты, которые приходят с сервера: ключ — русский текст сервера, значения в нём — {подстановки} (перевод по шаблону).
 * Раздел: пользователи и роли, интеграции (Linko), источники данных (Google Sheets, владение полями, конфликты), файлы отделов и
 * подключение Windows, Telegram (аккаунты, агент, чаты), рассылки, отделы, push.
 * Порядок важен для шаблонов: более длинный шаблон с тем же началом стоит раньше короткого (иначе короткий «съест» хвост подстановкой).
 */
const dict: Dict = {
  // ---------- Роли и права (SystemRoles) ----------
  "Администратор": "Administrator",
  "Директор": "Direktor",
  "Админ 2": "Admin 2",
  "РМ (Sales Base)": "RM (Sales Base)",
  "Супервайзер": "Supervayzer",
  "Агент": "Agent",
  "Продажи": "Savdo",
  "Финансы": "Moliya",
  "HR": "HR",
  "Производство": "Ishlab chiqarish",
  "Снабжение": "Ta'minot",
  "Маркетинг": "Marketing",
  "Полный доступ: все данные, пользователи и роли, интеграции и настройки AI.":
    "To'liq kirish: barcha ma'lumotlar, foydalanuvchilar va rollar, integratsiyalar va AI sozlamalari.",
  "Все данные отделов, консультант, журнал аудита, пользователи и роли. Интеграции и настройки AI — у администратора.":
    "Bo'limlarning barcha ma'lumotlari, maslahatchi, audit jurnali, foydalanuvchilar va rollar. Integratsiyalar va AI sozlamalari — administratorda.",
  "Файлы отдела продаж и AI Data Manager: загрузка отчётов, изменение планов, целей и дилеров регионов с подтверждением. В «Настройках» — только AI Data Manager.":
    "Savdo bo'limi fayllari va AI Data Manager: hisobotlarni yuklash, rejalar, maqsadlar va mintaqa dilerlarini tasdiqlash bilan o'zgartirish. «Sozlamalar»da — faqat AI Data Manager.",
  "Сотрудник отдела продаж: раздел «Продажи», консультант, файлы.": "Savdo bo'limi xodimi: «Savdo» bo'limi, maslahatchi, fayllar.",
  "Сотрудник финансового отдела: оплаты и продажи, консультант, файлы.": "Moliya bo'limi xodimi: to'lovlar va savdo, maslahatchi, fayllar.",
  "Сотрудник HR: кадровые данные, консультант, файлы.": "HR xodimi: kadrlar ma'lumotlari, maslahatchi, fayllar.",
  "Сотрудник производства: остатки и продажи товаров (раздел «Продажи»), консультант, файлы.":
    "Ishlab chiqarish xodimi: tovar qoldiqlari va savdosi («Savdo» bo'limi), maslahatchi, fayllar.",
  "Сотрудник снабжения: остатки, поставщики и продажи товаров (раздел «Продажи»), консультант, файлы.":
    "Ta'minot xodimi: qoldiqlar, yetkazib beruvchilar va tovar savdosi («Savdo» bo'limi), maslahatchi, fayllar.",
  "Сотрудник маркетинга: продажи, ассортимент и клиенты (раздел «Продажи»), консультант, файлы.":
    "Marketing xodimi: savdo, assortiment va mijozlar («Savdo» bo'limi), maslahatchi, fayllar.",
  "Sales Base: вся структура полевых продаж — команды, точки, маршруты, задачи, KPI, решения по рекомендациям AI; аналитика продаж и консультант.":
    "Sales Base: dala savdosining butun tuzilmasi — jamoalar, nuqtalar, marshrutlar, vazifalar, KPI, AI tavsiyalari bo'yicha qarorlar; savdo tahlili va maslahatchi.",
  "Sales Base: своя команда — агенты, точки, маршруты, визиты, задачи и KPI команды.":
    "Sales Base: o'z jamoasi — agentlar, nuqtalar, marshrutlar, tashriflar, vazifalar va jamoa KPI.",
  "Sales Base с телефона: свой маршрут, точки, визиты, задачи, продажи и KPI.": "Telefondan Sales Base: o'z marshruti, nuqtalar, tashriflar, vazifalar, savdo va KPI.",
  // Подписи прав
  "Настройки AI": "AI sozlamalari",
  "AI Data Manager: изменение данных по отчётам": "AI Data Manager: hisobotlar bo'yicha ma'lumotlarni o'zgartirish",
  "Продажи: настройки и синхронизация": "Savdo: sozlamalar va sinxronlash",
  "Файлы: просмотр": "Fayllar: ko'rish",
  "Файлы: загрузка": "Fayllar: yuklash",
  "AI-консультант": "AI maslahatchi",
  "Подтверждение действий AI": "AI harakatlarini tasdiqlash",
  "Журнал аудита": "Audit jurnali",
  "Sales Base: управление": "Sales Base: boshqaruv",
  "Sales Base: задачи и маршруты": "Sales Base: vazifalar va marshrutlar",
  "Telegram: аккаунты и AI-агент": "Telegram: akkauntlar va AI agent",
  "Рассылки в Telegram": "Telegram tarqatmalari",

  // ---------- Пользователи (UsersController) ----------
  "Пароль — не короче {n} символов.": "Parol — kamida {n} ta belgi.",
  "У этого пользователя роль с правами, которых у вас нет, — изменить его может только администратор.":
    "Bu foydalanuvchining rolida sizda yo'q huquqlar bor — uni faqat administrator o'zgartira oladi.",
  "Свою роль изменить нельзя — иначе можно потерять доступ к этому разделу. Попросите другого администратора.":
    "O'z rolingizni o'zgartirib bo'lmaydi — aks holda bu bo'limga kirish huquqini yo'qotishingiz mumkin. Boshqa administratordan so'rang.",
  "Нельзя отключить самого себя.": "O'zingizni faolsizlantira olmaysiz.",
  "Обязательные поля: логин, имя, фамилия, должность и роль.": "Majburiy maydonlar: login, ism, familiya, lavozim va rol.",
  "Логин — от 3 до 64 символов: латинские буквы, цифры, точка, дефис, подчёркивание или @.":
    "Login — 3 dan 64 tagacha belgi: lotin harflari, raqamlar, nuqta, defis, pastki chiziq yoki @.",
  "Имя и фамилия — до 100 символов, должность — до 150.": "Ism va familiya — 100 tagacha belgi, lavozim — 150 tagacha.",
  "Почта указана неверно.": "Pochta noto'g'ri ko'rsatilgan.",
  "Телефон — цифры, пробелы, скобки, дефисы и «+», от 7 до 32 символов.": "Telefon — raqamlar, bo'shliqlar, qavslar, defislar va «+», 7 dan 32 tagacha belgi.",
  "Такой роли нет.": "Bunday rol yo'q.",
  "Роль «{role}» даёт права, которых у вас нет, — назначить её может только администратор.":
    "«{role}» roli sizda yo'q huquqlarni beradi — uni faqat administrator tayinlay oladi.",
  "Логин «{login}» уже занят.": "«{login}» logini allaqachon band.",
  "Эта почта уже указана у другого пользователя.": "Bu pochta boshqa foydalanuvchida ko'rsatilgan.",
  "Свои разделы изменить нельзя — попросите другого администратора.": "O'z bo'limlaringizni o'zgartirib bo'lmaydi — boshqa administratordan so'rang.",
  "Неизвестный раздел: {keys}.": "Noma'lum bo'lim: {keys}.",
  "Раздел «{title}» выдать нельзя: у вас самих нет к нему доступа.": "«{title}» bo'limini berib bo'lmaydi: sizning o'zingizda unga kirish yo'q.",

  // ---------- Разделы пользователя (UserSections) ----------
  "Вторичка, первичка, планы, АКБ, остатки, акции, KPI; AI отвечает по продажам, ассортименту и клиентам.":
    "Ikkilamchi va birlamchi savdo, rejalar, AKB, qoldiqlar, aksiyalar, KPI; AI savdo, assortiment va mijozlar bo'yicha javob beradi.",
  "Оплаты, P&L дилеров, акт сверки, DSO, суммы KPI; AI-сотрудник финансов.":
    "To'lovlar, dilerlar P&L, solishtirma dalolatnoma, DSO, KPI summalari; moliya AI-xodimi.",
  "Сотрудники, посещаемость, опоздания и табели Verifix; HR AI по всем сотрудникам.":
    "Verifix xodimlari, davomat, kechikishlar va tabellar; barcha xodimlar bo'yicha HR AI.",
  "Задачи своего отдела и проектов; AI отвечает по задачам и просрочкам.":
    "O'z bo'limi va loyihalar vazifalari; AI vazifalar va muddati o'tganlar bo'yicha javob beradi.",
  "Файлы своего отдела — просмотр и загрузка; документы базы знаний в ответах AI.":
    "O'z bo'limi fayllari — ko'rish va yuklash; AI javoblarida bilimlar bazasi hujjatlari.",
  "Чат с консультантом и AI-сотрудниками отделов, Telegram-агент от имени пользователя.":
    "Maslahatchi va bo'limlar AI-xodimlari bilan chat, foydalanuvchi nomidan Telegram-agent.",
  "Полевая команда: задачи агентам и супервайзерам, маршруты, визиты.": "Dala jamoasi: agentlar va supervayzerlarga vazifalar, marshrutlar, tashriflar.",
  "Загрузка отчётов и изменение планов, целей и дилеров с подтверждением.": "Hisobotlarni yuklash va rejalar, maqsadlar va dilerlarni tasdiqlash bilan o'zgartirish.",
  "Рассылки в Telegram: создание, запуск и отмена.": "Telegram tarqatmalari: yaratish, ishga tushirish va bekor qilish.",

  // ---------- Интеграции: Linko ----------
  "Заказы, возвраты, визиты, торговые точки и торговые представители — источник данных раздела «Продажи».":
    "Buyurtmalar, qaytarishlar, tashriflar, savdo nuqtalari va savdo vakillari — «Savdo» bo'limining ma'lumot manbai.",
  "Адрес сервера должен начинаться с https:// — например https://имя.linko.uz": "Server manzili https:// bilan boshlanishi kerak — masalan, https://nom.linko.uz",
  "Сначала задайте адрес и токен и включите интеграцию.": "Avval manzil va tokenni kiriting hamda integratsiyani yoqing.",
  "Синхронизация уже идёт — дождитесь окончания.": "Sinxronlash allaqachon bajarilmoqda — tugashini kuting.",
  "Сначала задайте адрес и токен.": "Avval manzil va tokenni kiriting.",
  "Синхронизация уже идёт.": "Sinxronlash allaqachon bajarilmoqda.",
  "Синхронизация не идёт.": "Sinxronlash bajarilmayapti.",
  // Проверка подключения (test.message, lastTest.message, plansMessage)
  "Подключение работает.": "Ulanish ishlayapti.",
  "API планов работает.": "Rejalar API ishlayapti.",
  "Сервер Linko не принял токен планов (HTTP {status}).": "Linko serveri rejalar tokenini qabul qilmadi (HTTP {status}).",
  "API планов ответил ошибкой HTTP {status}.": "Rejalar API HTTP {status} xatosi bilan javob berdi.",
  "API планов недоступен: {error}": "Rejalar API ishlamayapti: {error}",
  "API планов не ответил или ответил не в том формате.": "Rejalar API javob bermadi yoki noto'g'ri formatda javob berdi.",
  "Укажите адрес сервера Linko, например https://имя.linko.uz": "Linko server manzilini kiriting, masalan, https://nom.linko.uz",
  "Токен не задан.": "Token kiritilmagan.",
  "Сервер Linko не принял токен (HTTP {status}).": "Linko serveri tokenni qabul qilmadi (HTTP {status}).",
  "По этому адресу нет Linko External API (HTTP 404). Проверьте адрес сервера.": "Bu manzilda Linko External API yo'q (HTTP 404). Server manzilini tekshiring.",
  "Сервер Linko ответил ошибкой HTTP {status}.": "Linko serveri HTTP {status} xatosi bilan javob berdi.",
  "Сервер недоступен: {error}": "Serverga ulanib bo'lmadi: {error}",
  "Сервер Linko не ответил за 20 секунд.": "Linko serveri 20 soniyada javob bermadi.",
  "Сервер ответил не в формате Linko API. Проверьте адрес сервера.": "Server Linko API formatida javob bermadi. Server manzilini tekshiring.",
  "Linko не настроен: задайте адрес и токен в «Настройки → Интеграции → Продажи → Linko».":
    "Linko sozlanmagan: manzil va tokenni «Sozlamalar → Integratsiyalar → Savdo → Linko» bo'limida kiriting.",
  // Ошибки загрузки данных (lastError)
  "Не задан токен API планов Linko (staff_balance).": "Linko rejalar API tokeni (staff_balance) kiritilmagan.",
  "Linko {path}: пустой ответ": "Linko {path}: bo'sh javob",
  "Linko {path}: ответ не в формате JSON API (проверьте адрес сервера)": "Linko {path}: javob JSON API formatida emas (server manzilini tekshiring)",
  "Linko {path}: сервер не принял токен (HTTP {status})": "Linko {path}: server tokenni qabul qilmadi (HTTP {status})",
  "Linko {path}: нет ответа за {n} с": "Linko {path}: {n} soniyada javob yo'q",
  "Linko {path}: сетевая ошибка ({type})": "Linko {path}: tarmoq xatosi ({type})",
  "Неизвестный справочник Linko: {entity}": "Noma'lum Linko ma'lumotnomasi: {entity}",
  "Сервер Linko изменён: данные в OneBase загружены с {from}, а подключение настроено на {to}. Чтобы не смешивать данные, нажмите «Очистить данные и загрузить заново».":
    "Linko serveri o'zgargan: OneBase'dagi ma'lumotlar {from} dan yuklangan, ulanish esa {to} ga sozlangan. Ma'lumotlar aralashmasligi uchun «Ma'lumotlarni tozalab, qaytadan yuklash» tugmasini bosing.",
  // Шаги синхронизации (progress.phase)
  "Подготовка": "Tayyorlash",
  "Очистка старых данных": "Eski ma'lumotlarni tozalash",
  "Справочники": "Ma'lumotnomalar",
  "Текущий и прошлый месяц": "Joriy va o'tgan oy",
  "Обновление": "Yangilash",
  "История": "Tarix",
  "Планы агентов": "Agentlar rejalari",
  "Планы Linko": "Linko rejalari",
  "Пересчёт отчётов": "Hisobotlarni qayta hisoblash",
  "Отмена…": "Bekor qilinmoqda…",

  // ---------- Источники данных: поля и разделы ----------
  "План РОП": "ROP rejasi",
  "План «Завод»": "«Zavod» rejasi",
  "План первички": "Birlamchi rejasi",
  "Дилер региона": "Mintaqa dileri",
  "СВР региона": "Mintaqa SVR",
  "P&L дилеров": "Dilerlar P&L",
  "Регион × категория × месяц, кг. Вторичка: выполнение, прогноз, рейтинг регионов и РМ.":
    "Mintaqa × toifa × oy, kg. Ikkilamchi: bajarilish, prognoz, mintaqalar va RM reytingi.",
  "Регион × категория × месяц, кг. Переключатель «План «Завод»» во вторичке.": "Mintaqa × toifa × oy, kg. Ikkilamchidagi «Zavod» rejasi almashtirgichi.",
  "Дилер (регион) × категория × месяц, кг. Первичка: план, выполнение, остаток.": "Diler (mintaqa) × toifa × oy, kg. Birlamchi: reja, bajarilish, qoldiq.",
  "Подпись строк первички, P&L и карточек регионов («Бобур Самарканд»).": "Birlamchi va P&L qatorlari hamda mintaqa kartochkalari yorlig'i («Бобур Самарканд»).",
  "Супервайзер региона во вторичке.": "Ikkilamchidagi mintaqa supervayzeri.",
  "Продажи, себестоимость и расходы по дилерам за месяц.": "Dilerlar bo'yicha oylik savdo, tannarx va xarajatlar.",
  // Разделы: откуда факт (facts)
  "Заказы, возвраты, визиты, точки и агенты — Linko": "Buyurtmalar, qaytarishlar, tashriflar, nuqtalar va agentlar — Linko",
  "Заказы и товары — Linko": "Buyurtmalar va tovarlar — Linko",
  "Перемещения завод → дилеры и заказы прямых клиентов — Linko": "Zavod → dilerlar ko'chirishlari va to'g'ridan-to'g'ri mijozlar buyurtmalari — Linko",
  "Остатки складов и продажи — Linko": "Ombor qoldiqlari va savdo — Linko",
  "Остатки по дням и заказы — Linko": "Kunlik qoldiqlar va buyurtmalar — Linko",
  "Планы ТП — API планов Linko": "TP rejalari — Linko rejalar API",
  "Визиты, заказы и планы ТП — Linko": "Tashriflar, buyurtmalar va TP rejalari — Linko",
  "Данные OneBase (Linko не ведёт P&L)": "OneBase ma'lumotlari (Linko P&L yuritmaydi)",
  // Месяц планов «Октябрь 2026» (monthPlans.period, уведомление о плане) — точные ключи ниже (monthYears): шаблон «Октябрь {year}»
  // узнавал бы любой текст, начинающийся с названия месяца, а «Май» короче 4 букв.
  "{period}: «{label}» ещё не загружен": "{period}: «{label}» hali yuklanmagan",
  // Контроль обмена с Linko (уведомления)
  "Обмен с Linko не работает — данные в отчётах устарели": "Linko bilan almashuv ishlamayapti — hisobotlardagi ma'lumotlar eskirgan",
  "Обмен с Linko восстановлен": "Linko bilan almashuv tiklandi",
  "Данные снова обновляются автоматически, отчёты пересчитаны.": "Ma'lumotlar yana avtomatik yangilanmoqda, hisobotlar qayta hisoblandi.",
  "Данные Linko не обновлялись с {since} (Ташкент): {what}. Ошибка: {error} OneBase повторяет синхронизацию каждые {n} мин. Проверьте токен и доступность Linko в «Настройки → Интеграции».":
    "Linko ma'lumotlari {since} (Toshkent) dan beri yangilanmagan: {what}. Xato: {error} OneBase sinxronizatsiyani har {n} daqiqada takrorlaydi. «Sozlamalar → Integratsiyalar» da token va Linko mavjudligini tekshiring.",
  "Данные Linko не обновлялись с {since} (Ташкент): {what}. OneBase повторяет синхронизацию каждые {n} мин. Проверьте токен и доступность Linko в «Настройки → Интеграции».":
    "Linko ma'lumotlari {since} (Toshkent) dan beri yangilanmagan: {what}. OneBase sinxronizatsiyani har {n} daqiqada takrorlaydi. «Sozlamalar → Integratsiyalar» da token va Linko mavjudligini tekshiring.",
  "Данные Linko ещё ни разу не загрузились: {what}. OneBase повторяет синхронизацию каждые {n} мин. Проверьте токен и доступность Linko в «Настройки → Интеграции».":
    "Linko ma'lumotlari hali bir marta ham yuklanmagan: {what}. OneBase sinxronizatsiyani har {n} daqiqada takrorlaydi. «Sozlamalar → Integratsiyalar» da token va Linko mavjudligini tekshiring.",
  "В {previous} план был по {n} регионам, на {period} — нет. Обновите таблицу Google Sheets (привязка с месяцем из столбца, из названия листа или «текущий месяц» подхватит его сама) или загрузите план через AI Data Manager.":
    "{previous} oyida reja {n} ta mintaqa bo'yicha bor edi, {period} uchun — yo'q. Google Sheets jadvalini yangilang (oy ustundan, varaq nomidan yoki «joriy oy» bo'lgan bog'lanish uni o'zi oladi) yoki rejani AI Data Manager orqali yuklang.",

  // ---------- Источники данных: период, расписание, листы ----------
  "«{field}»: последних месяцев — от 1 до {n}.": "«{field}»: oxirgi oylar — 1 dan {n} gacha.",
  "«{field}»: период — месяцы в формате ГГГГ-ММ.": "«{field}»: davr — YYYY-MM formatidagi oylar.",
  "«{field}»: начало периода позже конца.": "«{field}»: davr boshi oxiridan keyin.",
  "«{field}»: период — не больше {n} месяцев.": "«{field}»: davr — {n} oydan ko'p emas.",
  "«{field}»: период: все месяцы, текущий месяц, текущий год, последние N месяцев или диапазон.":
    "«{field}»: davr: barcha oylar, joriy oy, joriy yil, oxirgi N oy yoki oraliq.",
  "последних месяцев — от 1 до {n}": "oxirgi oylar — 1 dan {n} gacha",
  "период — месяцы в формате ГГГГ-ММ": "davr — YYYY-MM formatidagi oylar",
  "начало периода позже конца": "davr boshi oxiridan keyin",
  "период — не больше {n} месяцев": "davr — {n} oydan ko'p emas",
  "период: все месяцы, текущий месяц, текущий год, последние N месяцев или диапазон": "davr: barcha oylar, joriy oy, joriy yil, oxirgi N oy yoki oraliq",
  "Интервал — от {n} минут до 7 дней.": "Interval — {n} daqiqadan 7 kungacha.",
  "Время синхронизации — в формате ЧЧ:ММ.": "Sinxronlash vaqti — HH:MM formatida.",
  "Расписание: вручную, каждые N минут или ежедневно.": "Jadval: qo'lda, har N daqiqada yoki har kuni.",
  "Раздела «{section}» нет.": "«{section}» bo'limi yo'q.",
  "Выберите хотя бы одно поле, которое нужно обновлять из таблицы.": "Jadvaldan yangilanadigan kamida bitta maydonni tanlang.",
  "Поле «{field}» не относится к разделу «{section}».": "«{field}» maydoni «{section}» bo'limiga tegishli emas.",
  "«{field}»: укажите столбцы региона, значения (или столбцы категорий) и месяц.": "«{field}»: mintaqa, qiymat (yoki toifa ustunlari) va oy ustunlarini ko'rsating.",
  "«{field}»: укажите столбцы региона и значения.": "«{field}»: mintaqa va qiymat ustunlarini ko'rsating.",
  "«{field}»: укажите сопоставление столбцов.": "«{field}»: ustunlar moslashini ko'rsating.",
  "Поле «{field}» выбрано дважды.": "«{field}» maydoni ikki marta tanlangan.",
  "«{sheet}»: столбца «{column}» больше нет": "«{sheet}»: «{column}» ustuni endi yo'q",
  "«{sheet}»: в столбце {column} теперь «{now}», а при сопоставлении был «{saved}»": "«{sheet}»: {column} ustunida endi «{now}», moslashda esa «{saved}» edi",
  "структура листа изменилась — {problems}. Ничего не записано: откройте привязку и сопоставьте столбцы заново (можно «Сопоставить с помощью ИИ»)":
    "varaq tuzilishi o'zgardi — {problems}. Hech narsa yozilmadi: bog'lanishni oching va ustunlarni qaytadan moslang («AI yordamida moslash» mumkin)",
  "Выберите хотя бы один лист таблицы.": "Jadvalning kamida bitta varag'ini tanlang.",
  "Листов — не больше {n}; для всех листов выберите «Все листы».": "Varaqlar — {n} tadan ko'p emas; barcha varaqlar uchun «Barcha varaqlar»ni tanlang.",
  "Название листа — до 200 символов.": "Varaq nomi — 200 tagacha belgi.",
  "В таблице нет листов.": "Jadvalda varaqlar yo'q.",
  "В таблице нет листов {missing}. Листы: {list}.": "Jadvalda {missing} varaqlari yo'q. Varaqlar: {list}.",
  "из названия листа «{sheet}» не понять месяц — назовите его «Сентябрь 2026» или «09.2026»":
    "«{sheet}» varag'i nomidan oyni aniqlab bo'lmaydi — uni «Сентябрь 2026» yoki «09.2026» deb nomlang",
  "в названии листа «{sheet}» нет года — укажите год в привязке": "«{sheet}» varag'i nomida yil yo'q — yilni bog'lanishda ko'rsating",
  "Строка заголовка {row} вне листа (строк: {rows}).": "{row}-sarlavha qatori varaqdan tashqarida (qatorlar: {rows}).",

  // ---------- Источники данных: владение полями, конфликты ----------
  "поле ведёт Google Sheets «{connection}» ({sheets})": "maydonni Google Sheets «{connection}» ({sheets}) yuritadi",
  "Ручная правка": "Qo'lda tahrirlash",
  "{source}: попытка изменить «{field}» — этим полем владеет Google Sheets": "{source}: «{field}» ni o'zgartirishga urinish — bu maydon Google Sheets'ga tegishli",
  "отклонено: значение из Google Sheets сохранено": "rad etildi: Google Sheets'dagi qiymat saqlandi",
  "«{field}»: поле ведёт Google Sheets «{connection}» ({sheets}). Меняйте значения в таблице — следующая синхронизация перезаписала бы правку в OneBase. Чтобы править здесь, отключите или удалите привязку в «Настройки → Источники данных».":
    "«{field}»: maydonni Google Sheets «{connection}» ({sheets}) yuritadi. Qiymatlarni jadvalda o'zgartiring — keyingi sinxronlash OneBase'dagi tahrirni qayta yozib yuborgan bo'lardi. Bu yerda tahrirlash uchun «Sozlamalar → Ma'lumot manbalari» bo'limida bog'lanishni o'chiring yoki olib tashlang.",
  "Строка таблицы": "Jadval qatori",
  "Чужое поле": "Begona maydon",
  "{decision} · закрыт вручную": "{decision} · qo'lda yopilgan",
  "не записано: поле ждёт исправления таблицы, прежние значения OneBase сохранены": "yozilmadi: maydon jadval tuzatilishini kutmoqda, OneBase'dagi avvalgi qiymatlar saqlangan",
  "строка исправлена в таблице": "qator jadvalda tuzatildi",
  "поле убрано из привязки": "maydon bog'lanishdan olib tashlandi",
  "привязка удалена": "bog'lanish o'chirildi",
  "привязка выключена — запуск вручную": "bog'lanish o'chirilgan — qo'lda ishga tushirish",
  "«{connection}» — все листы": "«{connection}» — barcha varaqlar",
  "в таблице разные значения: {values}": "jadvalda turli qiymatlar: {values}",
  "строку нельзя записать": "qatorni yozib bo'lmaydi",
  // Карта владения: кто ведёт данные
  "Факт продаж и возвратов": "Savdo va qaytarishlar fakti",
  "заказы и возвраты: вторичка, ассортимент, SKU, прямые клиенты первички": "buyurtmalar va qaytarishlar: ikkilamchi, assortiment, SKU, birlamchining to'g'ridan-to'g'ri mijozlari",
  "визиты агентов, конверсия визита, проблемные агенты": "agentlar tashriflari, tashrif konversiyasi, muammoli agentlar",
  "Точки, агенты, товары": "Nuqtalar, agentlar, tovarlar",
  "справочники Linko: торговые точки, пользователи, товары и категории": "Linko ma'lumotnomalari: savdo nuqtalari, foydalanuvchilar, tovarlar va toifalar",
  "Остатки и перемещения": "Qoldiqlar va ko'chirishlar",
  "остатки складов, аутсток, перемещения завод → дилеры (первичка)": "ombor qoldiqlari, autstok, zavod → dilerlar ko'chirishlari (birlamchi)",
  "Оплаты": "To'lovlar",
  "платежи клиентов": "mijozlar to'lovlari",
  "Планы ТП": "TP rejalari",
  "планы агентов из API планов Linko": "Linko rejalar API'dan agentlar rejalari",
  "ещё не загружено": "hali yuklanmagan",
  "синхронизация Linko": "Linko sinxronlashi",
  "Данные OneBase": "OneBase ma'lumotlari",
  "{sheets} · раздел «{section}» · каждые {n} мин": "{sheets} · «{section}» bo'limi · har {n} daqiqada",
  "{sheets} · раздел «{section}» · ежедневно в {time}": "{sheets} · «{section}» bo'limi · har kuni {time} da",
  "{sheets} · раздел «{section}» · вручную": "{sheets} · «{section}» bo'limi · qo'lda",
  "ещё не синхронизировалось": "hali sinxronlanmagan",
  "синхронизация проверила таблицу": "sinxronlash jadvalni tekshirdi",
  "AI Data Manager или «Настройки продаж → Регионы»": "AI Data Manager yoki «Savdo sozlamalari → Mintaqalar»",
  "AI Data Manager (чат, файлы)": "AI Data Manager (chat, fayllar)",
  "через AI Data Manager не менялось": "AI Data Manager orqali o'zgartirilmagan",
  "изменено через AI Data Manager": "AI Data Manager orqali o'zgartirilgan",
  "«Настройки продаж → Цели»: конверсия визита, выручка на ТТ…": "«Savdo sozlamalari → Maqsadlar»: tashrif konversiyasi, nuqtaga tushum…",
  "Выполнение плана, отклонение, прогноз": "Reja bajarilishi, og'ish, prognoz",
  "Расчёт OneBase": "OneBase hisobi",
  "считается при показе из плана (OneBase или Google Sheets) и факта Linko — не хранится, источники его не пишут":
    "ko'rsatishda rejadan (OneBase yoki Google Sheets) va Linko faktidan hisoblanadi — saqlanmaydi, manbalar uni yozmaydi",
  "P&L: валовая прибыль, EBIT, рентабельность": "P&L: yalpi foyda, EBIT, rentabellik",
  "считается из строк P&L дилеров": "dilerlar P&L qatorlaridan hisoblanadi",

  // ---------- Источники данных: подключения Google Sheets ----------
  "Таблица используется в привязках разделов ({used}) — сначала удалите их.": "Jadval bo'limlar bog'lanishlarida ishlatilmoqda ({used}) — avval ularni o'chiring.",
  "Таблица не прочитана.": "Jadval o'qilmadi.",
  "Название — от 1 до 100 символов.": "Nomi — 1 dan 100 tagacha belgi.",
  "Это не ссылка на таблицу Google: нужна ссылка вида docs.google.com/spreadsheets/d/…": "Bu Google jadvali havolasi emas: docs.google.com/spreadsheets/d/… ko'rinishidagi havola kerak",
  "Доступ — по ссылке или сервисным аккаунтом.": "Ruxsat — havola orqali yoki xizmat akkaunti bilan.",
  "Вставьте JSON-ключ сервисного аккаунта.": "Xizmat akkauntining JSON kalitini qo'ying.",
  "Таблица читается: листов {n}.": "Jadval o'qilmoqda: {n} ta varaq.",
  "Таблица не найдена — добавьте её в «Подключения».": "Jadval topilmadi — uni «Ulanishlar»ga qo'shing.",
  "Листы таблицы неизвестны — нажмите «Проверить» у подключения.": "Jadval varaqlari noma'lum — ulanishda «Tekshirish» tugmasini bosing.",
  "Ключ сервисного аккаунта не расшифровывается (сменились ключи сервера) — вставьте его заново.":
    "Xizmat akkaunti kaliti shifrdan chiqarilmayapti (server kalitlari o'zgargan) — uni qaytadan qo'ying.",
  "Строка заголовков — от 1 до 1000.": "Sarlavhalar qatori — 1 dan 1000 gacha.",
  "«{field}» уже обновляется: {sheets} (раздел «{section}»). Одно поле — один источник.": "«{field}» allaqachon yangilanmoqda: {sheets} («{section}» bo'limi). Bitta maydon — bitta manba.",
  // Чужое поле в привязке (предупреждение ИИ-сопоставления и ошибка сохранения)
  "«{field}»: поле уже ведёт привязка «{connection}» ({sheets}) в разделе «{section}», выключена — одно поле, один источник.":
    "«{field}»: maydonni allaqachon «{connection}» ({sheets}) bog'lanishi «{section}» bo'limida yuritadi (o'chirilgan) — bitta maydon, bitta manba.",
  "«{field}»: поле уже ведёт привязка «{connection}» ({sheets}) в разделе «{section}» — одно поле, один источник.":
    "«{field}»: maydonni allaqachon «{connection}» ({sheets}) bog'lanishi «{section}» bo'limida yuritadi — bitta maydon, bitta manba.",
  "поле уже ведёт привязка «{connection}» ({sheets}) в разделе «{section}», выключена — одно поле, один источник":
    "maydonni allaqachon «{connection}» ({sheets}) bog'lanishi «{section}» bo'limida yuritadi (o'chirilgan) — bitta maydon, bitta manba",
  "поле уже ведёт привязка «{connection}» ({sheets}) в разделе «{section}» — одно поле, один источник":
    "maydonni allaqachon «{connection}» ({sheets}) bog'lanishi «{section}» bo'limida yuritadi — bitta maydon, bitta manba",

  // ---------- Источники данных: синхронизация (lastMessage, итоги полей) ----------
  "Синхронизация этой привязки уже идёт — дождитесь её окончания.": "Bu bog'lanishning sinxronlashi allaqachon bajarilmoqda — tugashini kuting.",
  "Таблица привязки удалена.": "Bog'lanish jadvali o'chirilgan.",
  "Внутренняя ошибка OneBase — подробности в журнале ошибок.": "OneBase ichki xatosi — tafsilotlar xatolar jurnalida.",
  // Ошибка по расписанию с повтором: частые ошибки Google с подстановками — отдельно (в общем шаблоне ниже они остались бы по-русски).
  "Нет доступа к таблице: поделитесь ею с {email} (читатель) или проверьте ссылку. Ответ Google: «{answer}». Повтор через {n} минут.":
    "Jadvalga ruxsat yo'q: uni {email} bilan ulashing (o'quvchi sifatida) yoki havolani tekshiring. Google javobi: «{answer}». {n} daqiqadan keyin qayta uriniladi.",
  "Нет доступа к таблице: поделитесь ею с {email} (читатель) или проверьте ссылку. Повтор через {n} минут.":
    "Jadvalga ruxsat yo'q: uni {email} bilan ulashing (o'quvchi sifatida) yoki havolani tekshiring. {n} daqiqadan keyin qayta uriniladi.",
  "Google не отдал таблицу: HTTP {status}. Ответ Google: «{answer}». Повтор через {n} минут.":
    "Google jadvalni bermadi: HTTP {status}. Google javobi: «{answer}». {n} daqiqadan keyin qayta uriniladi.",
  "Google не отдал таблицу: HTTP {status}. Повтор через {n} минут.": "Google jadvalni bermadi: HTTP {status}. {n} daqiqadan keyin qayta uriniladi.",
  "Google ограничил частоту запросов — попробуйте через минуту. Ответ Google: «{answer}». Повтор через {n} минут.":
    "Google so'rovlar chastotasini chekladi — bir daqiqadan keyin urinib ko'ring. Google javobi: «{answer}». {n} daqiqadan keyin qayta uriniladi.",
  "Нет связи с Google: {error} Повтор через {n} минут.": "Google bilan aloqa yo'q: {error} {n} daqiqadan keyin qayta uriniladi.",
  "{error} Повтор через {n} минут.": "{error} {n} daqiqadan keyin qayta uriniladi.",
  "поле больше не поддерживается": "maydon endi qo'llab-quvvatlanmaydi",
  "листа за период «{period}» в таблице ещё нет — жду, когда его добавят": "«{period}» davri uchun varaq jadvalda hali yo'q — qo'shilishini kutyapman",
  "не записано: строк с ошибками {n} — исправьте таблицу": "yozilmadi: xatoli qatorlar — {n} ta, jadvalni tuzating",
  "наступил новый месяц, а в таблице те же цифры, что записаны за {period} — жду обновления таблицы. Если план не изменился, нажмите «Синхронизировать сейчас».":
    "yangi oy boshlandi, jadvalda esa {period} uchun yozilgan o'sha raqamlar — jadval yangilanishini kutyapman. Agar reja o'zgarmagan bo'lsa, «Hozir sinxronlash» tugmasini bosing.",
  "без изменений": "o'zgarishsiz",
  "записано: новых {created}, изменено {updated}, удалено {deleted}, но проверка нашла расхождений: {n}":
    "yozildi: yangi {created}, o'zgartirildi {updated}, o'chirildi {deleted}, lekin tekshiruv {n} ta farq topdi",
  "записано: новых {created}, изменено {updated}, удалено {deleted}": "yozildi: yangi {created}, o'zgartirildi {updated}, o'chirildi {deleted}",
  "{label}: ожидалось {expected}, в базе {actual}": "{label}: kutilgan {expected}, bazada {actual}",
  "в листах нет строк со значениями плана": "varaqlarda reja qiymatlari bor qatorlar yo'q",
  "в листах нет строк с регионом и значением": "varaqlarda mintaqa va qiymat bor qatorlar yo'q",
  "в листах нет строк за период «{period}»": "varaqlarda «{period}» davri uchun qatorlar yo'q",
  "в листе {n} месяцев ({period}) — за раз можно не больше {max}: выберите в привязке нужный период поля (например, «текущий год» или «последние 3 месяца»)":
    "varaqda {n} oy ({period}) — bir martada {max} tadan ko'p bo'lmasligi kerak: bog'lanishda maydonning kerakli davrini tanlang (masalan, «joriy yil» yoki «oxirgi 3 oy»)",
  "Синхронизация записывает данные через AI Data Manager — нужно право «AI Data Manager».": "Sinxronlash ma'lumotlarni AI Data Manager orqali yozadi — «AI Data Manager» huquqi kerak.",
  // Чтение Google Sheets (lastCheckMessage, ошибки синхронизации)
  "Подключите её сервисным аккаунтом Google — тогда читаются только выбранные листы.": "Uni Google xizmat akkaunti orqali ulang — shunda faqat tanlangan varaqlar o'qiladi.",
  "Это файл Excel в Google Drive: сохраните его как таблицу Google («Файл → Сохранить как таблицу Google») — тогда читаются только выбранные листы.":
    "Bu Google Drive'dagi Excel fayli: uni Google jadvali sifatida saqlang («File → Save as Google Sheets») — shunda faqat tanlangan varaqlar o'qiladi.",
  "Включите Google Sheets API в проекте Google Cloud сервисного аккаунта (APIs & Services → Library) — тогда читаются только выбранные листы.":
    "Xizmat akkauntining Google Cloud loyihasida Google Sheets API ni yoqing (APIs & Services → Library) — shunda faqat tanlangan varaqlar o'qiladi.",
  "Лист «{sheet}» слишком большой: {rows} строк — больше {n} млн ячеек за одно чтение.": "«{sheet}» varag'i juda katta: {rows} qator — bir o'qishda {n} mln katakdan ko'p.",
  "Google вернул не все листы — попробуйте ещё раз.": "Google barcha varaqlarni qaytarmadi — qayta urinib ko'ring.",
  "Выбранные листы слишком большие — больше {n} млн ячеек. Выберите в привязке только нужные листы.":
    "Tanlangan varaqlar juda katta — {n} mln katakdan ko'p. Bog'lanishda faqat kerakli varaqlarni tanlang.",
  "Таблица не найдена — проверьте ссылку.": "Jadval topilmadi — havolani tekshiring.",
  "Таблица закрыта. Откройте доступ «Все, у кого есть ссылка — читатель» или подключите сервисный аккаунт Google.":
    "Jadval yopiq. «Anyone with the link — Viewer» (havolaga ega barcha — o'quvchi) ruxsatini oching yoki Google xizmat akkauntini ulang.",
  "Google не отдал таблицу: HTTP {status}. Ответ Google: «{answer}».": "Google jadvalni bermadi: HTTP {status}. Google javobi: «{answer}».",
  "Google не отдал таблицу: HTTP {status}.": "Google jadvalni bermadi: HTTP {status}.",
  "В проекте Google Cloud сервисного аккаунта выключен доступ к таблицам: включите Google Sheets API и Google Drive API (APIs & Services → Library).":
    "Xizmat akkauntining Google Cloud loyihasida jadvallarga kirish o'chirilgan: Google Sheets API va Google Drive API ni yoqing (APIs & Services → Library).",
  "Таблица слишком большая, чтобы читать её файлом целиком: {mb} МБ данных в листах. {hint}": "Jadval butun fayl sifatida o'qish uchun juda katta: varaqlarda {mb} MB ma'lumot. {hint}",
  "Таблицу не удалось прочитать: {error}": "Jadvalni o'qib bo'lmadi: {error}",
  "Google не принял ключ сервисного аккаунта — вставьте действующий ключ (удалённый в Google Cloud ключ не работает). Ответ Google: «{answer}».":
    "Google xizmat akkaunti kalitini qabul qilmadi — amaldagi kalitni qo'ying (Google Cloud'da o'chirilgan kalit ishlamaydi). Google javobi: «{answer}».",
  "Google не принял ключ сервисного аккаунта — вставьте действующий ключ (удалённый в Google Cloud ключ не работает).":
    "Google xizmat akkaunti kalitini qabul qilmadi — amaldagi kalitni qo'ying (Google Cloud'da o'chirilgan kalit ishlamaydi).",
  "Google не вернул токен доступа.": "Google kirish tokenini qaytarmadi.",
  "Нет доступа к таблице: поделитесь ею с {email} (читатель) или проверьте ссылку. Ответ Google: «{answer}».":
    "Jadvalga ruxsat yo'q: uni {email} bilan ulashing (o'quvchi sifatida) yoki havolani tekshiring. Google javobi: «{answer}».",
  "Нет доступа к таблице: поделитесь ею с {email} (читатель) или проверьте ссылку.": "Jadvalga ruxsat yo'q: uni {email} bilan ulashing (o'quvchi sifatida) yoki havolani tekshiring.",
  "Google ограничил частоту запросов — попробуйте через минуту. Ответ Google: «{answer}».":
    "Google so'rovlar chastotasini chekladi — bir daqiqadan keyin urinib ko'ring. Google javobi: «{answer}».",
  "Google ограничил частоту запросов — попробуйте через минуту.": "Google so'rovlar chastotasini chekladi — bir daqiqadan keyin urinib ko'ring.",
  "Нет связи с Google: {error}": "Google bilan aloqa yo'q: {error}",
  "Google не ответил вовремя — попробуйте ещё раз.": "Google o'z vaqtida javob bermadi — qayta urinib ko'ring.",
  "Таблица больше 20 МБ — для синхронизации нужна таблица поменьше.": "Jadval 20 MB dan katta — sinxronlash uchun kichikroq jadval kerak.",
  "Это не ключ сервисного аккаунта: нужен JSON с type «service_account», client_email и private_key.":
    "Bu xizmat akkaunti kaliti emas: type «service_account», client_email va private_key bor JSON kerak.",
  "Ключ сервисного аккаунта — не JSON. Скачайте ключ в Google Cloud: IAM → Сервисные аккаунты → Ключи → JSON.":
    "Xizmat akkaunti kaliti JSON emas. Kalitni Google Cloud'dan yuklab oling: IAM → Service accounts → Keys → JSON.",

  // ---------- Источники данных: ИИ-сопоставление столбцов ----------
  "ИИ-сопоставление не выполнено: {error} Ручное сопоставление работает как раньше.": "AI moslash bajarilmadi: {error} Qo'lda moslash avvalgidek ishlaydi.",
  "AI не настроен в «Настройки → AI».": "AI «Sozlamalar → AI» bo'limida sozlanmagan.",
  "ИИ ответил не в формате JSON — повторите или сопоставьте столбцы вручную.": "AI JSON formatida javob bermadi — qayta urinib ko'ring yoki ustunlarni qo'lda moslang.",
  "ИИ не вернул сопоставление столбцов — повторите или сопоставьте вручную.": "AI ustunlar moslashini qaytarmadi — qayta urinib ko'ring yoki qo'lda moslang.",
  "Не принято ни одного соответствия — отметьте столбцы, которые нужно сопоставить.": "Birorta ham moslik qabul qilinmadi — moslash kerak bo'lgan ustunlarni belgilang.",
  "Ни одно поле не сопоставлено полностью: проверьте столбцы региона, значения и месяц.": "Birorta maydon to'liq moslanmagan: mintaqa, qiymat va oy ustunlarini tekshiring.",
  "В разделе «{section}» нет полей из Google Sheets — все его данные приходят из Linko.": "«{section}» bo'limida Google Sheets maydonlari yo'q — uning barcha ma'lumotlari Linko'dan keladi.",
  "В листе «{sheet}» нет данных в строке заголовков {row} и ниже — проверьте строку заголовков.":
    "«{sheet}» varag'ida {row}-sarlavhalar qatorida va undan pastda ma'lumot yo'q — sarlavhalar qatorini tekshiring.",
  // Роли столбцов и поля целей («поле · роль»)
  "План категории": "Toifa rejasi",
  "План РОП · Регион (дилер)": "ROP rejasi · Mintaqa (diler)",
  "План РОП · Категория": "ROP rejasi · Toifa",
  "План РОП · План, кг": "ROP rejasi · Reja, kg",
  "План РОП · План категории": "ROP rejasi · Toifa rejasi",
  "План РОП · Месяц": "ROP rejasi · Oy",
  "План РОП · Год": "ROP rejasi · Yil",
  "План «Завод» · Регион (дилер)": "«Zavod» rejasi · Mintaqa (diler)",
  "План «Завод» · Категория": "«Zavod» rejasi · Toifa",
  "План «Завод» · План, кг": "«Zavod» rejasi · Reja, kg",
  "План «Завод» · План категории": "«Zavod» rejasi · Toifa rejasi",
  "План «Завод» · Месяц": "«Zavod» rejasi · Oy",
  "План «Завод» · Год": "«Zavod» rejasi · Yil",
  "План первички · Регион (дилер)": "Birlamchi rejasi · Mintaqa (diler)",
  "План первички · Категория": "Birlamchi rejasi · Toifa",
  "План первички · План, кг": "Birlamchi rejasi · Reja, kg",
  "План первички · План категории": "Birlamchi rejasi · Toifa rejasi",
  "План первички · Месяц": "Birlamchi rejasi · Oy",
  "План первички · Год": "Birlamchi rejasi · Yil",
  "Дилер региона · Регион": "Mintaqa dileri · Mintaqa",
  "Дилер региона · Дилер": "Mintaqa dileri · Diler",
  "СВР региона · Регион": "Mintaqa SVR · Mintaqa",
  "СВР региона · СВР": "Mintaqa SVR · SVR",
  "P&L дилеров · Контрагент": "Dilerlar P&L · Kontragent",
  "P&L дилеров · Статья": "Dilerlar P&L · Modda",
  "P&L дилеров · Сумма": "Dilerlar P&L · Summa",
  "P&L дилеров · Месяц": "Dilerlar P&L · Oy",
  "P&L дилеров · Год": "Dilerlar P&L · Yil",
  // Предупреждения и ошибки предложения (warnings, reason, fieldErrors, stale)
  "столбца «{column}» нет в листе": "varaqda «{column}» ustuni yo'q",
  "факт продаж и другие данные Linko из Google Sheets не пишутся — их ведёт Linko": "savdo fakti va Linko'ning boshqa ma'lumotlari Google Sheets'dan yozilmaydi — ularni Linko yuritadi",
  "поля «{key}» нет среди полей раздела «{section}» — ИИ не может создавать поля": "«{key}» maydoni «{section}» bo'limi maydonlari orasida yo'q — AI maydon yarata olmaydi",
  "категории «{category}» нет в OneBase": "«{category}» toifasi OneBase'da yo'q",
  "заголовок похож на факт, а поле — план: факт продаж ведёт Linko, проверьте": "sarlavha faktga o'xshaydi, maydon esa — reja: savdo faktini Linko yuritadi, tekshiring",
  "финансовые данные — только после проверки": "moliyaviy ma'lumotlar — faqat tekshiruvdan keyin",
  "низкая уверенность ({pct}) — не сопоставлено": "past ishonchlilik ({pct}) — moslanmagan",
  "на «{field} · {role}» претендуют столбцы {list} — выберите один": "«{field} · {role}» uchun {list} ustunlari da'vogar — bittasini tanlang",
  "в таблице есть столбцы-категории — общий план к ним не нужен (план задвоился бы)": "jadvalda toifa ustunlari bor — ularga umumiy reja kerak emas (reja ikki baravar bo'lib qolardi)",
  "для «{field}» не хватает {missing} — поле сохранится, только если его дополнить": "«{field}» uchun {missing} yetishmaydi — maydon faqat to'ldirilsa saqlanadi",
  "столбца региона": "mintaqa ustuni",
  "столбца плана": "reja ustuni",
  "столбца значения": "qiymat ustuni",
  "столбцов контрагента, статьи и суммы": "kontragent, modda va summa ustunlari",
  "месяца": "oy",
  "ИИ не нашёл подходящего поля": "AI mos maydon topmadi",
  "преобразование «{transform}» не поддерживается — значения берутся как есть": "«{transform}» o'zgartirishi qo'llab-quvvatlanmaydi — qiymatlar o'z holicha olinadi",
  "Столбца «{column}» нет в листе «{sheet}».": "«{sheet}» varag'ida «{column}» ustuni yo'q.",
  "Поля «{field}» нет среди полей раздела «{section}».": "«{field}» maydoni «{section}» bo'limi maydonlari orasida yo'q.",
  "Столбец {column}: категории «{category}» нет в OneBase.": "{column} ustuni: «{category}» toifasi OneBase'da yo'q.",
  "«{field} · {role}»: выбрано несколько столбцов ({list}).": "«{field} · {role}»: bir nechta ustun tanlangan ({list}).",
  "«{field}»: не понятно, за какой месяц значения — сопоставьте столбец месяца или выберите месяц поля вручную.":
    "«{field}»: qiymatlar qaysi oy uchunligi noma'lum — oy ustunini moslang yoki maydon oyini qo'lda tanlang.",
  "«{field}»: для одного месяца нужны год и месяц.": "«{field}»: bitta oy uchun yil va oy kerak.",
  "«{field}»: нужны столбцы региона и плана.": "«{field}»: mintaqa va reja ustunlari kerak.",
  "«{field}»: нужны столбцы региона и значения.": "«{field}»: mintaqa va qiymat ustunlari kerak.",
  "«{field}»: нужны столбцы контрагента, статьи и суммы.": "«{field}»: kontragent, modda va summa ustunlari kerak.",
  "«{field}»: сохранённое сопоставление не подходит к листу «{sheet}» — столбцы {list}": "«{field}»: saqlangan moslash «{sheet}» varag'iga mos kelmaydi — ustunlar {list}",
  "Не найдено — обновите страницу.": "Topilmadi — sahifani yangilang.",

  // ---------- Файлы отделов ----------
  "Укажите название.": "Nomini kiriting.",
  "Название — не длиннее {n} символов.": "Nomi — {n} ta belgidan uzun bo'lmasin.",
  "Название не может быть «.», «..» или заканчиваться точкой.": "Nom «.», «..» bo'la olmaydi yoki nuqta bilan tugay olmaydi.",
  "В названии нельзя использовать символы / \\ : * ? \" < > |.": "Nomda / \\ : * ? \" < > | belgilarini ishlatib bo'lmaydi.",
  "«{name}» — служебное имя Windows, выберите другое.": "«{name}» — Windows'ning xizmat nomi, boshqasini tanlang.",
  "Файл или папка не найдены.": "Fayl yoki papka topilmadi.",
  "Нет прав на изменение файлов этого отдела.": "Bu bo'lim fayllarini o'zgartirish huquqi yo'q.",
  "В папке уже есть «{name}».": "Papkada «{name}» allaqachon bor.",
  "Отдел не найден.": "Bo'lim topilmadi.",
  "Корневую папку отдела нельзя переименовать.": "Bo'limning asosiy papkasini qayta nomlab bo'lmaydi.",
  "Корневую папку отдела нельзя перенести.": "Bo'limning asosiy papkasini ko'chirib bo'lmaydi.",
  "Папку можно перенести только внутри отдела. В другой отдел — скачайте и загрузите заново.":
    "Papkani faqat bo'lim ichida ko'chirish mumkin. Boshqa bo'limga — yuklab olib, qaytadan yuklang.",
  "Папку нельзя перенести в саму себя или во вложенную папку.": "Papkani o'ziga yoki ichki papkasiga ko'chirib bo'lmaydi.",
  "Корневую папку отдела нельзя удалить.": "Bo'limning asosiy papkasini o'chirib bo'lmaydi.",
  // Общие папки, блокировки WebDAV, личная ссылка Windows
  "Нет прав на изменение файлов этой общей папки.": "Ushbu umumiy papka fayllarini o'zgartirishga huquq yo'q.",
  "Общую папку переименовывает администратор в «Общих файлах».": "Umumiy papka nomini administrator «Umumiy fayllar»da o'zgartiradi.",
  "Корневую папку нельзя перенести.": "Asosiy papkani ko'chirib bo'lmaydi.",
  "Корневую папку нельзя удалить.": "Asosiy papkani o'chirib bo'lmaydi.",
  "Папку можно перенести только внутри отдела или общей папки. В другое место — скачайте и загрузите заново.":
    "Papkani faqat bo'lim yoki umumiy papka ichida ko'chirish mumkin. Boshqa joyga — yuklab oling va qayta yuklang.",
  "«{name}» сейчас открыт для редактирования ({owner}) — изменить его можно после закрытия файла. Блокировка снимется сама не позже {date}.":
    "«{name}» hozir tahrirlash uchun ochiq ({owner}) — uni fayl yopilgandan keyin o'zgartirish mumkin. Qulf {date} dan kechiktirmay o'zi olinadi.",
  "«{name}» сейчас открыт для редактирования — изменить его можно после закрытия файла. Блокировка снимется сама не позже {date}.":
    "«{name}» hozir tahrirlash uchun ochiq — uni fayl yopilgandan keyin o'zgartirish mumkin. Qulf {date} dan kechiktirmay o'zi olinadi.",
  "Блокировка файла истекла или снята — откройте файл заново.": "Fayl qulfi muddati tugagan yoki olib tashlangan — faylni qayta oching.",
  "Снять блокировку может администратор или сотрудник с правом «Управление».": "Qulfni administrator yoki «Boshqarish» huquqiga ega xodim olib tashlashi mumkin.",
  "Имя общей папки не может начинаться с «_» или быть «k».": "Umumiy papka nomi «_» bilan boshlanishi yoki «k» bo'lishi mumkin emas.",
  "«{name}» — код отдела; выберите другое имя общей папки.": "«{name}» — bo'lim kodi; umumiy papka uchun boshqa nom tanlang.",
  "Общая папка не найдена.": "Umumiy papka topilmadi.",
  "Имя общей папки не может начинаться с «_» или быть «k» или «my».":
    "Umumiy papka nomi «_» bilan boshlanishi, «k» yoki «my» bo'lishi mumkin emas.",
  "Проводник → «Этот компьютер» → «Подключить сетевой диск», выберите букву, например X:.":
    "Provodnik → «Bu kompyuter» → «Tarmoq diskini ulash», harfni tanlang, masalan X:.",
  "В поле «Папка» вставьте адрес «Мои файлы» (или адрес одной общей папки) и отметьте «Восстанавливать подключение при входе в систему».":
    "«Papka» maydoniga «Mening fayllarim» manzilini (yoki bitta umumiy papka manzilini) qo'ying va «Tizimga kirishda ulanishni tiklash»ni belgilang.",
  "Windows спросит логин и пароль — введите свой логин OneBase и пароль от OneBase, отметьте «Запомнить учётные данные».":
    "Windows login va parolni so'raydi — OneBase loginingiz va OneBase parolini kiriting, «Hisob ma'lumotlarini eslab qolish»ni belgilang.",
  "Чтобы Word и Excel открывали и сохраняли файлы с этого диска, один раз на компьютере откройте файл «Разрешить вход Word и Excel» (.reg) и согласитесь внести его в реестр, затем перезапустите Word и Excel.":
    "Word va Excel bu diskdan fayllarni ochishi va saqlashi uchun kompyuterda bir marta «Word va Excelga kirishga ruxsat» (.reg) faylini oching va uni reestrga kiritishga rozi bo'ling, so'ng Word va Excelni qayta ishga tushiring.",
  "В общей папке есть файлы или папки (в том числе в корзине) — удалить можно только пустую.":
    "Umumiy papkada fayllar yoki papkalar bor (savatdagilar ham) — faqat bo'shini o'chirish mumkin.",
  "Доступ выдаётся сотруднику, отделу или роли.": "Kirish xodimga, bo'limga yoki rolga beriladi.",
  "Сотрудник, отдел или роль не найдены.": "Xodim, bo'lim yoki rol topilmadi.",
  "Доступ к общей папке настраивает администратор или сотрудник с правом «Управление».":
    "Umumiy papkaga kirishni administrator yoki «Boshqarish» huquqiga ega xodim sozlaydi.",
  "Общие папки создаёт и удаляет администратор.": "Umumiy papkalarni administrator yaratadi va o'chiradi.",
  "Доступ: none, read, write или manage.": "Kirish: none, read, write yoki manage.",
  "Личной ссылки нет — выпустите её.": "Shaxsiy havola yo'q — uni chiqaring.",
  "Срок личной ссылки истёк — выпустите новую.": "Shaxsiy havola muddati tugagan — yangisini chiqaring.",
  "Личная ссылка Windows — для сотрудников с доступом к файлам.": "Shaxsiy Windows havolasi — fayllarga kirishi bor xodimlar uchun.",
  "отключён": "o'chirilgan",
  "отдел": "bo'lim",
  "роль": "rol",
  "удалённый сотрудник": "o'chirilgan xodim",
  "удалённый отдел": "o'chirilgan bo'lim",
  "удалённая роль": "o'chirilgan rol",
  "Выберите свободную букву диска, например Y:.": "Bo'sh disk harfini tanlang, masalan Y:.",
  "В поле «Папка» вставьте личный адрес: «Скопировать» у поля «Личный адрес».":
    "«Papka» maydoniga shaxsiy manzilni qo'ying: «Shaxsiy manzil» maydonidagi «Nusxalash».",
  "На диске — папки ваших отделов и общие папки, к которым вам выдан доступ; Word и Excel открывают и сохраняют файлы прямо с диска.":
    "Diskda — bo'limlaringiz papkalari va sizga kirish berilgan umumiy papkalar; Word va Excel fayllarni to'g'ridan-to'g'ri diskdan ochadi va saqlaydi.",
  "Личный адрес работает как пароль и даёт ровно ваши права: не пересылайте его. Попал не туда — нажмите «Новая ссылка», прежняя перестанет работать сразу.":
    "Shaxsiy manzil parol kabi ishlaydi va aynan sizning huquqlaringizni beradi: uni boshqalarga yubormang. Noto'g'ri joyga tushsa — «Yangi havola»ni bosing, avvalgisi darhol ishlamay qoladi.",
  "Права проверяются при каждом обращении: если вас отключат или заберут доступ к папке, диск сразу покажет меньше или перестанет открываться.":
    "Huquqlar har bir murojaatda tekshiriladi: sizni o'chirib qo'yishsa yoki papkaga kirishni olib qo'yishsa, disk darhol kamroq ko'rsatadi yoki ochilmay qoladi.",
  "Пока файл открыт в Word или Excel, у других он открывается только для чтения — правки двух людей не перезапишут друг друга.":
    "Fayl Word yoki Excelda ochiq turganida, boshqalarda u faqat o'qish uchun ochiladi — ikki kishining o'zgarishlari bir-birini bosib ketmaydi.",
  "Файл больше {n} МБ.": "Fayl {n} MB dan katta.",
  "Версия файла не найдена.": "Fayl versiyasi topilmadi.",
  "Папка «{name}» в корзине — восстановите её.": "«{name}» papkasi savatda — uni tiklang.",
  "Выберите другой отдел.": "Boshqa bo'limni tanlang.",
  "Другому отделу можно дать чтение или запись.": "Boshqa bo'limga o'qish yoki yozish huquqini berish mumkin.",
  "Доступ к папкам отделов настраивает администратор.": "Bo'lim papkalariga kirishni administrator sozlaydi.",
  "Файл пришёл не полностью — загрузите ещё раз.": "Fayl to'liq kelmadi — qaytadan yuklang.",
  "Папка не найдена.": "Papka topilmadi.",
  "Файл не найден.": "Fayl topilmadi.",
  "Выберите файлы для загрузки.": "Yuklash uchun fayllarni tanlang.",
  "Доступ — none, read или write.": "Ruxsat — none, read yoki write.",
  "Сессия закончилась — войдите заново.": "Sessiya tugadi — qaytadan kiring.",
  // Предпросмотр (note)
  "Предпросмотра для этого типа нет — скачайте файл.": "Bu turdagi fayl uchun oldindan ko'rish yo'q — faylni yuklab oling.",
  "Файл больше 20 МБ — предпросмотр не строится, скачайте его.": "Fayl 20 MB dan katta — oldindan ko'rish yaratilmaydi, uni yuklab oling.",
  "Файл не удалось прочитать для предпросмотра — скачайте его.": "Faylni oldindan ko'rish uchun o'qib bo'lmadi — uni yuklab oling.",
  "Показаны первые 10 листов.": "Birinchi 10 ta varaq ko'rsatilgan.",
  "В документе нет текста.": "Hujjatda matn yo'q.",
  // Подключение папки отдела к Windows
  "У отдела уже есть подключение — смените пароль или отзовите его.": "Bo'limda allaqachon ulanish bor — parolni almashtiring yoki uni bekor qiling.",
  "У отдела нет действующего подключения.": "Bo'limda amaldagi ulanish yo'q.",
  "Не удалось подобрать свободный логин — попробуйте ещё раз.": "Bo'sh login topib bo'lmadi — qayta urinib ko'ring.",
  "Подключения к Windows создаёт и меняет администратор.": "Windows ulanishlarini administrator yaratadi va o'zgartiradi.",
  // Проверка подключения: шаги (name, detail)
  "Адрес подключения": "Ulanish manzili",
  "Логин отдела": "Bo'lim logini",
  "База данных": "Ma'lumotlar bazasi",
  "WebDAV по адресу": "Manzil bo'yicha WebDAV",
  "Хранилище (MinIO)": "Saqlash ombori (MinIO)",
  "Не задан домен (переменная FILE_STORAGE_DOMAIN → Files:PublicDomain) — Windows подключать не к чему.":
    "Domen ko'rsatilmagan (FILE_STORAGE_DOMAIN → Files:PublicDomain o'zgaruvchisi) — Windows'ni ulaydigan joy yo'q.",
  "Подключение не создано или отозвано.": "Ulanish yaratilmagan yoki bekor qilingan.",
  "Папка отдела «{name}»: папок — {n}.": "«{name}» bo'lim papkasi: papkalar — {n}.",
  "Не проверялся: нет адреса или логина.": "Tekshirilmadi: manzil yoki login yo'q.",
  "Запись, чтение и удаление проверочного объекта прошли.": "Tekshiruv obyektini yozish, o'qish va o'chirish muvaffaqiyatli o'tdi.",
  "Прочитанное не совпало с записанным.": "O'qilgan ma'lumot yozilgan bilan mos kelmadi.",
  "Ошибка хранилища: {type}.": "Saqlash ombori xatosi: {type}.",
  "{url} ответил 207: папка отдела открывается с логином {login}.": "{url} 207 bilan javob berdi: bo'lim papkasi {login} logini bilan ochiladi.",
  "{url} ответил {status} {reason} вместо 207 — проверьте маршрут Cloudflare → nginx:82.":
    "{url} 207 o'rniga {status} {reason} bilan javob berdi — Cloudflare → nginx:82 marshrutini tekshiring.",
  "{url} не отвечает ({type}) — проверьте домен в Cloudflare и nginx:82.": "{url} javob bermayapti ({type}) — Cloudflare va nginx:82 dagi domenni tekshiring.",
  // Инструкция (guide) — названия кнопок Windows оставлены как в русской Windows, в скобках — английские
  "Откройте Проводник и выберите «Этот компьютер».": "Provodnikni (Explorer) oching va «Этот компьютер» (This PC) ni tanlang.",
  "Нажмите «Подключить сетевой диск» (вверху окна; в Windows 11 — в меню «…»).":
    "«Подключить сетевой диск» (Map network drive) tugmasini bosing (oyna tepasida; Windows 11 da — «…» menyusida).",
  "Выберите свободную букву диска, например Z:.": "Bo'sh disk harfini tanlang, masalan Z:.",
  "В поле «Папка» вставьте адрес: {address}": "«Папка» (Folder) maydoniga manzilni qo'ying: {address}",
  "Отметьте «Восстанавливать подключение при входе в систему» и «Использовать другие учётные данные», нажмите «Готово».":
    "«Восстанавливать подключение при входе в систему» (Reconnect at sign-in) va «Использовать другие учётные данные» (Connect using different credentials) ni belgilang, «Готово» (Finish) tugmasini bosing.",
  "Введите логин {login} и пароль подключения, отметьте «Запомнить учётные данные» и нажмите «ОК».":
    "{login} loginini va ulanish parolini kiriting, «Запомнить учётные данные» (Remember my credentials) ni belgilang va «ОК» tugmasini bosing.",
  "Диск Z: с папкой отдела «{department}» появится в «Этом компьютере». Файлы, положенные туда, сразу видны в OneBase.":
    "«{department}» bo'lim papkasi bilan Z: diski «Этот компьютер» (This PC) da paydo bo'ladi. U yerga qo'yilgan fayllar OneBase'da darhol ko'rinadi.",
  "В поле «Папка» вставьте адрес без пароля: в окне OneBase «Подключить к Windows» — «Скопировать» у поля «Адрес без пароля».":
    "«Папка» (Folder) maydoniga parolsiz manzilni qo'ying: OneBase'ning «Windows'ga ulash» oynasida — «Parolsiz manzil» maydoni yonidagi «Nusxalash».",
  "Отметьте «Восстанавливать подключение при входе в систему» и нажмите «Готово» — логин и пароль не нужны.":
    "«Восстанавливать подключение при входе в систему» (Reconnect at sign-in) ni belgilang va «Готово» (Finish) tugmasini bosing — login va parol kerak emas.",
  "Диск Z: с папкой отдела «{department}» появится в «Этом компьютере». Файлы, положенные туда, сразу видны в OneBase; Word и Excel открывают и сохраняют их прямо с диска.":
    "«{department}» bo'lim papkasi bilan Z: diski «Этот компьютер» (This PC) da paydo bo'ladi. U yerga qo'yilgan fayllar OneBase'da darhol ko'rinadi; Word va Excel ularni to'g'ridan-to'g'ri diskdan ochadi va saqlaydi.",
  // Примечания (notes)
  "Адрес без пароля работает как пароль: не пересылайте его посторонним. Если он попал не туда, администратор нажимает «Новая ссылка» — старая перестаёт работать сразу, а диск на компьютерах отдела подключают по новой.":
    "Parolsiz manzil parol kabi ishlaydi: uni begonalarga yubormang. Agar u noto'g'ri joyga tushib qolsa, administrator «Yangi havola» ni bosadi — eskisi darhol ishlamay qoladi, bo'lim kompyuterlarida disk yangisi orqali ulanadi.",
  "Вход по логину {login} и паролю ({address}) тоже работает, но Word и Excel из Microsoft 365 по нему файлы не откроют («заблокировал доступ… метод входа, который может быть небезопасным») — подключайте диск по адресу без пароля.":
    "{login} logini va paroli bilan kirish ({address}) ham ishlaydi, lekin Microsoft 365 dagi Word va Excel u orqali fayllarni ochmaydi («заблокировал доступ… метод входа, который может быть небезопасным») — diskni parolsiz manzil orqali ulang.",
  "Адрес без пароля": "Parolsiz manzil",
  "Ссылка отдела «{department}» открывается без логина и пароля (207) — Word и Excel откроют файлы с диска.":
    "«{department}» bo'limi havolasi login va parolsiz ochiladi (207) — Word va Excel fayllarni diskdan ochadi.",
  "Ссылка ответила {status} {reason} вместо 207 — проверьте маршрут Cloudflare → nginx:82.": "Havola 207 o'rniga {status} {reason} bilan javob berdi — Cloudflare → nginx:82 marshrutini tekshiring.",
  "Ссылка не отвечает ({type}) — проверьте домен в Cloudflare и nginx:82.": "Havola javob bermayapti ({type}) — Cloudflare va nginx:82 dagi domenni tekshiring.",
  "Тот же адрес в виде пути Windows: {unc}": "Shu manzil Windows yo'li ko'rinishida: {unc}",
  "Адрес подключения не настроен: администратору нужно задать FILE_STORAGE_DOMAIN.": "Ulanish manzili sozlanmagan: administrator FILE_STORAGE_DOMAIN ni belgilashi kerak.",
  "Если Windows пишет «Указанное имя папки недействительно», запустите службу «Веб-клиент» (WebClient): Win+R → services.msc → «Веб-клиент» → «Запустить», тип запуска — «Автоматически».":
    "Agar Windows «Указанное имя папки недействительно» (The folder you entered does not appear to be valid) deb yozsa, «Веб-клиент» (WebClient) xizmatini ishga tushiring: Win+R → services.msc → «Веб-клиент» → «Запустить» (Start), ishga tushirish turi — «Автоматически» (Automatic).",
  "Windows по умолчанию открывает через сетевую папку файлы до 50 МБ. Чтобы поднять предел, выполните в PowerShell от имени администратора: reg add HKLM\\SYSTEM\\CurrentControlSet\\Services\\WebClient\\Parameters /v FileSizeLimitInBytes /t REG_DWORD /d 4294967295 /f; Restart-Service WebClient":
    "Windows standart holatda tarmoq papkasi orqali 50 MB gacha fayllarni ochadi. Chegarani oshirish uchun PowerShell'da administrator nomidan bajaring: reg add HKLM\\SYSTEM\\CurrentControlSet\\Services\\WebClient\\Parameters /v FileSizeLimitInBytes /t REG_DWORD /d 4294967295 /f; Restart-Service WebClient",
  "Через сетевую папку проходит файл до 100 МБ (ограничение Cloudflare) — большие файлы загружайте на сайте OneBase.":
    "Tarmoq papkasi orqali 100 MB gacha fayl o'tadi (Cloudflare cheklovi) — katta fayllarni OneBase saytida yuklang.",
  "Файлы, загруженные на сайте, появятся в Проводнике после обновления папки (F5).": "Saytda yuklangan fayllar papka yangilangandan keyin (F5) Provodnikda paydo bo'ladi.",

  // ---------- Журнал ошибок: постоянные части записей (у Linko и синхронизации — предупреждения и ошибки, у остального — ошибки) ----------
  "Linko {path}: HTTP {status}, попытка {attempt} из {max}": "Linko {path}: HTTP {status}, urinish {attempt} / {max}",
  "Linko {path}: {error}, попытка {attempt} из {max}": "Linko {path}: {error}, urinish {attempt} / {max}",
  "Linko {entity}: {count} ошибок в ответе": "Linko {entity}: javobda {count} ta xato",
  "Linko {entity}: ошибка синхронизации": "Linko {entity}: sinxronlash xatosi",
  "Не удалось расшифровать токен Linko — ключи Data Protection изменились. Задайте токен заново.":
    "Linko tokenini shifrdan chiqarib bo'lmadi — Data Protection kalitlari o'zgargan. Tokenni qaytadan kiriting.",
  "Linko: проверка подключения не прошла — {message}": "Linko: ulanish tekshiruvidan o'tmadi — {message}",
  "Linko: сверка с Linko не удалась": "Linko: Linko bilan solishtirib bo'lmadi",
  "Синхронизация Linko отменена (режим {mode}): {effect}": "Linko sinxronlashi bekor qilindi (rejim {mode}): {effect}",
  "данные загружены не полностью — догрузятся следующей синхронизацией": "ma'lumotlar to'liq yuklanmadi — keyingi sinxronlashda yuklanadi",
  "незавершённый шаг повторится при следующей синхронизации": "tugallanmagan bosqich keyingi sinxronlashda takrorlanadi",
  "Не удалось заранее пересчитать отчёты продаж": "Savdo hisobotlarini oldindan qayta hisoblab bo'lmadi",
  "Фоновая синхронизация Linko упала": "Linko fon sinxronlashi ishdan chiqdi",
  "Синхронизация Linko упала": "Linko sinxronlashi ishdan chiqdi",
  "Linko: синхронизация не запущена — {message}": "Linko: sinxronlash ishga tushirilmadi — {message}",
  "Данные Linko очищены перед полной загрузкой; оргструктура и планы OneBase сохранены":
    "To'liq yuklashdan oldin Linko ma'lumotlari tozalandi; OneBase tashkiliy tuzilmasi va rejalari saqlandi",
  "Источники данных: синхронизация привязки {binding} упала": "Ma'lumot manbalari: {binding} bog'lanishini sinxronlash ishdan chiqdi",
  "Источники данных: синхронизация по расписанию упала": "Ma'lumot manbalari: jadval bo'yicha sinxronlash ishdan chiqdi",
  "Telegram-агент: ошибка ответа (аккаунт {account})": "Telegram agent: javob xatosi (akkaunt {account})",
  "Telegram-агент: сообщение {message} в чате {chat} не обработано": "Telegram agent: {chat} chatidagi {message} xabari qayta ishlanmadi",
  "Рассылка {campaign}: ошибка отправки в чат {peer}": "Tarqatma {campaign}: {peer} chatiga yuborish xatosi",
  "Рассылки: очередь аккаунта {account} остановилась с ошибкой": "Tarqatmalar: {account} akkaunti navbati xato bilan to'xtadi",
  "Push: не удалось расшифровать ключ VAPID (ключи Data Protection изменились). Push-уведомления отключены; ключи не пересоздаются, чтобы не сломать подписки.":
    "Push: VAPID kalitini shifrdan chiqarib bo'lmadi (Data Protection kalitlari o'zgargan). Push-bildirishnomalar o'chirilgan; obunalar buzilmasligi uchun kalitlar qayta yaratilmaydi.",
  "Push: ошибка отправки уведомления {tag}": "Push: {tag} bildirishnomasini yuborish xatosi",
  "Консультант: ошибка ответа в чате {id}": "Maslahatchi: {id} chatida javob xatosi",
  "Консультант: ошибка чата": "Maslahatchi: chat xatosi",
  "AI Data Manager: ошибка хода в чате {id}": "AI Data Manager: {id} chatida qadam xatosi",
  "AI Data Manager: ошибка запроса": "AI Data Manager: so'rov xatosi",
  "AI-сотрудник {agent} завершился с ошибкой": "AI xodim {agent} xato bilan yakunlandi",
  "Отчёт консультанта: не удалось собрать отчёт для {agent}": "Maslahatchi hisoboti: {agent} uchun hisobot tuzib bo'lmadi",
  "Sales Base: ошибка фоновой задачи": "Sales Base: fon vazifasi xatosi",
  "Sales Base: ошибка шага «{step}»": "Sales Base: «{step}» bosqichi xatosi",

  // ---------- Push ----------
  "Push-уведомления сейчас недоступны.": "Push-bildirishnomalar hozir mavjud emas.",
  "Неверная подписка браузера.": "Brauzer obunasi noto'g'ri.",
  "Проверку можно повторить через полминуты.": "Tekshiruvni yarim daqiqadan keyin takrorlash mumkin.",

  // ---------- Telegram: ошибки Telegram (lastError аккаунта, ошибки получателей) ----------
  "Telegram ограничил частоту: подождать {n} с.": "Telegram chastotani chekladi: {n} soniya kutish kerak.",
  "Telegram ограничил аккаунт за сообщения незнакомым людям (PEER_FLOOD) — отправка остановлена.":
    "Telegram akkauntni notanish odamlarga xabar yuborgani uchun chekladi (PEER_FLOOD) — yuborish to'xtatildi.",
  "Получатель заблокировал аккаунт (или аккаунт — его).": "Qabul qiluvchi akkauntni bloklagan (yoki akkaunt — uni).",
  "Получатель запретил сообщения настройками приватности.": "Qabul qiluvchi maxfiylik sozlamalari orqali xabarlarni taqiqlagan.",
  "В этот чат аккаунту писать нельзя.": "Akkaunt bu chatga yoza olmaydi.",
  "Чат недоступен аккаунту (удалён, приватный или аккаунт исключён).": "Chat akkaunt uchun mavjud emas (o'chirilgan, yopiq yoki akkaunt chiqarib yuborilgan).",
  "Аккаунт получателя удалён.": "Qabul qiluvchining akkaunti o'chirilgan.",
  "Сообщение длиннее, чем принимает Telegram.": "Xabar Telegram qabul qiladiganidan uzunroq.",
  "Неверный код — проверьте и введите ещё раз.": "Kod noto'g'ri — tekshirib, qayta kiriting.",
  "Код истёк — нажмите «Подключить» ещё раз.": "Kod muddati o'tdi — «Ulash» tugmasini yana bosing.",
  "Неверный пароль двухэтапной защиты.": "Ikki bosqichli himoya paroli noto'g'ri.",
  "Неверный номер телефона.": "Telefon raqami noto'g'ri.",
  "Номер заблокирован в Telegram.": "Raqam Telegram'da bloklangan.",
  "Сессия Telegram недействительна (отозвана или вход с другого места) — переподключите аккаунт.":
    "Telegram sessiyasi yaroqsiz (bekor qilingan yoki boshqa joydan kirilgan) — akkauntni qayta ulang.",
  "Telegram не принял api_id/api_hash — проверьте их в «Настройки → Telegram».": "Telegram api_id/api_hash ni qabul qilmadi — ularni «Sozlamalar → Telegram» bo'limida tekshiring.",
  "Telegram временно недоступен ({rpc}).": "Telegram vaqtincha ishlamayapti ({rpc}).",
  "Telegram отклонил запрос: {rpc}.": "Telegram so'rovni rad etdi: {rpc}.",
  "Telegram не ответил за {n} с — попробуйте ещё раз; если повторяется, нажмите «Проверить».":
    "Telegram {n} soniyada javob bermadi — qayta urinib ko'ring; takrorlansa, «Tekshirish» tugmasini bosing.",
  "Вход не начат или сервер перезапускался — нажмите «Подключить» ещё раз.": "Kirish boshlanmagan yoki server qayta ishga tushgan — «Ulash» tugmasini yana bosing.",
  "Вход не выполнен: {error}": "Kirish bajarilmadi: {error}",
  "Аккаунта с этим номером нет в Telegram — сначала зарегистрируйте его в приложении Telegram.":
    "Bu raqamli akkaunt Telegram'da yo'q — avval uni Telegram ilovasida ro'yxatdan o'tkazing.",
  "Telegram просит «{step}» — такой шаг входа OneBase не поддерживает.": "Telegram «{step}» ni so'rayapti — OneBase kirishning bunday bosqichini qo'llab-quvvatlamaydi.",
  "Нет связи с Telegram: {type}": "Telegram bilan aloqa yo'q: {type}",
  "Аккаунт не подключён — нажмите «Подключить».": "Akkaunt ulanmagan — «Ulash» tugmasini bosing.",
  "Сессия Telegram недействительна — переподключите аккаунт.": "Telegram sessiyasi yaroqsiz — akkauntni qayta ulang.",
  "Не заданы api_id и api_hash — укажите их в «Настройки → Telegram» (my.telegram.org).":
    "api_id va api_hash kiritilmagan — ularni «Sozlamalar → Telegram» bo'limida ko'rsating (my.telegram.org).",
  "Telegram не подтвердил отправку за {n} с — неизвестно, дошло ли сообщение; проверьте чат.":
    "Telegram {n} soniyada yuborishni tasdiqlamadi — xabar yetib borgani noma'lum; chatni tekshiring.",
  "Связь с Telegram оборвалась во время отправки ({type}) — неизвестно, дошло ли сообщение; проверьте чат.":
    "Yuborish paytida Telegram bilan aloqa uzildi ({type}) — xabar yetib borgani noma'lum; chatni tekshiring.",
  "Нет связи с Telegram ({type}) — соединение восстановится само; повторите через минуту.":
    "Telegram bilan aloqa yo'q ({type}) — ulanish o'zi tiklanadi; bir daqiqadan keyin takrorlang.",
  "Telegram не вернул пользователя.": "Telegram foydalanuvchini qaytarmadi.",
  // Аккаунты, пользователи, настройки агента
  "У аккаунта есть рассылки — их журнал нужен. Отключите аккаунт вместо удаления.": "Akkauntda tarqatmalar bor — ularning jurnali kerak. O'chirib tashlash o'rniga akkauntni uzing.",
  "Код — цифры из сообщения Telegram.": "Kod — Telegram xabaridagi raqamlar.",
  "Введите пароль двухэтапной защиты Telegram.": "Telegram ikki bosqichli himoya parolini kiriting.",
  "Этот аккаунт Telegram уже подключён как «{name}».": "Bu Telegram akkaunti allaqachon «{name}» sifatida ulangan.",
  "Модель — в виде «провайдер/модель», например anthropic/claude-opus-5-5; пусто — основная модель.": "Model — «provayder/model» ko'rinishida, masalan anthropic/claude-opus-5-5; bo'sh — asosiy model.",
  "Укажите username Telegram, например @ivan_petrov.": "Telegram username'ini kiriting, masalan @ivan_petrov.",
  "Telegram ID — положительное число.": "Telegram ID — musbat son.",
  "Пользователь OneBase не найден или отключён.": "OneBase foydalanuvchisi topilmadi yoki o'chirilgan.",
  "Пользователь @{username} не найден в Telegram — проверьте username.": "@{username} foydalanuvchisi Telegram'da topilmadi — username'ni tekshiring.",
  "@{username} — бот, а не человек.": "@{username} — bot, odam emas.",
  "Этот пользователь Telegram уже есть в списке.": "Bu Telegram foydalanuvchisi ro'yxatda allaqachon bor.",
  "У этого пользователя OneBase нет права на AI-консультанта (или он отключён) — выберите другого.":
    "Bu OneBase foydalanuvchisida AI maslahatchi huquqi yo'q (yoki u o'chirilgan) — boshqasini tanlang.",
  "Неизвестное действие.": "Noma'lum amal.",
  "Слишком много кодов за час — попробуйте позже.": "Bir soatda juda ko'p kod so'raldi — keyinroq urinib ko'ring.",
  "Укажите название аккаунта (например, «Отдел продаж»).": "Akkaunt nomini kiriting (masalan, «Savdo bo'limi»).",
  "Номер телефона в международном формате, например +998 90 123 45 67.": "Telefon raqami xalqaro formatda, masalan +998 90 123 45 67.",
  "Username Telegram — 4–32 символа: латиница, цифры, «_».": "Telegram username — 4–32 belgi: lotin harflari, raqamlar, «_».",
  "api_id — число из my.telegram.org.": "api_id — my.telegram.org dagi son.",
  "api_hash — 32 шестнадцатеричных символа из my.telegram.org.": "api_hash — my.telegram.org dagi 32 ta o'n oltilik belgi.",
  "Укажите api_hash.": "api_hash ni kiriting.",
  // Чаты: почему писать нельзя (CannotSendReason)
  "аккаунт не может писать в этот чат": "akkaunt bu chatga yoza olmaydi",
  "чата больше нет в диалогах аккаунта": "chat endi akkaunt dialoglarida yo'q",
  "служебный чат Telegram": "Telegram xizmat chati",
  "это бот, а не человек": "bu bot, odam emas",
  "группа удалена или преобразована в супергруппу": "guruh o'chirilgan yoki superguruhga aylantirilgan",
  "аккаунт вышел из группы": "akkaunt guruhdan chiqqan",
  "в группе участникам запрещено писать": "guruhda a'zolarga yozish taqiqlangan",
  "нет доступа: аккаунт исключён или группа закрыта": "ruxsat yo'q: akkaunt chiqarib yuborilgan yoki guruh yopiq",
  "аккаунт не подписан на канал": "akkaunt kanalga obuna bo'lmagan",
  "канал: публиковать может только администратор с правом публикации": "kanal: faqat nashr qilish huquqiga ega administrator e'lon qila oladi",
  "в группе аккаунту запрещено писать": "guruhda akkauntga yozish taqiqlangan",
  // Проверка агента (ошибки, note, error)
  "Напишите вопрос.": "Savolni yozing.",
  "Чат не найден у этого аккаунта.": "Bu akkauntda chat topilmadi.",
  "Пользователь не найден в списке.": "Foydalanuvchi ro'yxatda topilmadi.",
  "Агент выключен — в Telegram сейчас он не ответил бы. Полный доступ не действует: не выбран пользователь OneBase с правом на AI-консультанта — ответ по отправленным данным.":
    "Agent o'chirilgan — hozir Telegram'da u javob bermagan bo'lardi. To'liq kirish amal qilmaydi: AI maslahatchi huquqiga ega OneBase foydalanuvchisi tanlanmagan — javob yuborilgan ma'lumotlar bo'yicha.",
  "Агент выключен — в Telegram сейчас он не ответил бы.": "Agent o'chirilgan — hozir Telegram'da u javob bermagan bo'lardi.",
  "Незнакомым агент не отвечает: включите «Отвечать всем по отправленным данным» во вкладке «Пользователи».":
    "Agent notanishlarga javob bermaydi: «Foydalanuvchilar» bo'limida «Hammaga — yuborilgan ma'lumotlar bo'yicha» ni yoqing.",
  "Пользователь выключен в списке — агент ему не отвечает.": "Foydalanuvchi ro'yxatda o'chirilgan — agent unga javob bermaydi.",
  "Полный доступ не действует: не выбран пользователь OneBase с правом на AI-консультанта — ответ по отправленным данным.":
    "To'liq kirish amal qilmaydi: AI maslahatchi huquqiga ega OneBase foydalanuvchisi tanlanmagan — javob yuborilgan ma'lumotlar bo'yicha.",
  "Чат консультанта для проверки не найден — начните проверку заново.": "Tekshiruv uchun maslahatchi chati topilmadi — tekshiruvni qaytadan boshlang.",
  "Внутренняя ошибка OneBase — подробности в журнале сервера.": "OneBase ichki xatosi — tafsilotlar server jurnalida.",

  // ---------- Рассылки ----------
  "Рассылка приостановлена: аккаунт Telegram не подключён. Подключите его и нажмите «Продолжить».":
    "Tarqatma to'xtatildi: Telegram akkaunti ulanmagan. Uni ulang va «Davom ettirish» tugmasini bosing.",
  "Рассылка приостановлена: {reason}": "Tarqatma to'xtatildi: {reason}",
  "Чат больше не найден у аккаунта.": "Chat endi akkauntda topilmadi.",
  "Неизвестно, дошло ли сообщение: сервер прервал отправку. Автоповтора нет, чтобы не отправить дважды — проверьте чат и при необходимости нажмите «Повторить ошибки».":
    "Xabar yetib borgani noma'lum: server yuborishni to'xtatdi. Ikki marta yubormaslik uchun avtomatik takrorlash yo'q — chatni tekshiring va kerak bo'lsa «Xatolarni qayta yuborish» tugmasini bosing.",
  "Выберите аккаунт Telegram.": "Telegram akkauntini tanlang.",
  "Укажите название рассылки.": "Tarqatma nomini kiriting.",
  "Текст длиннее {n} символов — столько Telegram не примет одним сообщением.": "Matn {n} belgidan uzun — Telegram buncha matnni bitta xabarda qabul qilmaydi.",
  "Время отправки уже прошло.": "Yuborish vaqti allaqachon o'tib ketgan.",
  "Рассылка идёт — сначала отмените её.": "Tarqatma yuborilmoqda — avval uni bekor qiling.",
  "Рассылка уже отправлялась — её журнал сохраняется. Удалить можно только неотправленную.":
    "Tarqatma allaqachon yuborilgan — uning jurnali saqlanadi. Faqat yuborilmagan tarqatmani o'chirish mumkin.",
  // Причины пропуска получателей
  "чат не принадлежит аккаунту рассылки": "chat tarqatma akkauntiga tegishli emas",
  "уже в списке": "ro'yxatda allaqachon bor",
  "больше {n} получателей в одной рассылке": "bitta tarqatmada {n} tadan ko'p qabul qiluvchi",
  // Файлы и запуск
  "Файл пустой или больше {n} МБ.": "Fayl bo'sh yoki {n} MB dan katta.",
  "Отчёт не найден среди ваших отчётов.": "Hisobot sizning hisobotlaringiz orasida topilmadi.",
  "Не больше {n} файлов в рассылке.": "Tarqatmada {n} tadan ko'p fayl bo'lmasligi kerak.",
  "Текст длиннее {n} символов — уйдёт отдельным сообщением перед файлом.": "Matn {n} belgidan uzun — fayldan oldin alohida xabar bo'lib ketadi.",
  "Нет ни текста, ни файлов.": "Na matn, na fayl bor.",
  "Не выбраны получатели.": "Qabul qiluvchilar tanlanmagan.",
  "В {n} чатов аккаунт больше не может писать — им отправка не удастся.": "Akkaunt endi {n} ta chatga yoza olmaydi — ularga yuborib bo'lmaydi.",
  "Отправка займёт не меньше ~{minutes} мин (пауза {seconds} с между чатами).": "Yuborish kamida ~{minutes} daqiqa davom etadi (chatlar orasida {seconds} soniya pauza).",
  "Выберите чат этого аккаунта.": "Shu akkaunt chatini tanlang.",
  "Аккаунт не может писать в этот чат.": "Akkaunt bu chatga yoza olmaydi.",
  "Нечего отправлять: нет ни текста, ни файлов.": "Yuboradigan narsa yo'q: na matn, na fayl bor.",
  "Запустить можно черновик.": "Faqat qoralamani ishga tushirish mumkin.",
  "Аккаунт Telegram не подключён.": "Telegram akkaunti ulanmagan.",
  "Повторить можно завершённую с ошибками или приостановленную рассылку.": "Faqat xatolar bilan yakunlangan yoki to'xtatilgan tarqatmani takrorlash mumkin.",
  "Ошибочных получателей нет.": "Xatoli qabul qiluvchilar yo'q.",
  "Сейчас это недоступно (статус рассылки — {status}).": "Hozir bu mumkin emas (tarqatma holati — {status}).",
  "Файл «{name}» в хранилище изменился или повреждён — рассылка его не отправит.": "Ombordagi «{name}» fayli o'zgargan yoki shikastlangan — tarqatma uni yubormaydi.",
  "Рассылка завершена — изменить её нельзя. Создайте новую.": "Tarqatma yakunlangan — uni o'zgartirib bo'lmaydi. Yangisini yarating.",
  "Рассылка уже идёт — сначала поставьте на паузу или отмените.": "Tarqatma allaqachon yuborilmoqda — avval pauza qiling yoki bekor qiling.",

  // ---------- Перенос настроек «Источников данных» (DataSourceTransfer), другие имена дилера ----------
  "Файл настроек пуст.": "Sozlamalar fayli bo'sh.",
  "Это не файл настроек «Источников данных» OneBase (или он из более новой версии).":
    "Bu OneBase «Ma'lumot manbalari» sozlamalari fayli emas (yoki yangiroq versiyadan).",
  "Перенос настроек записывает привязки и дилеров — нужно право «AI Data Manager».":
    "Sozlamalarni ko'chirish bog'lanishlar va dilerlarni yozadi — «AI Data Manager» huquqi kerak.",
  "Уже подключена как «{name}».": "Allaqachon «{name}» sifatida ulangan.",
  "Будет подключена и проверена.": "Ulanadi va tekshiriladi.",
  "Подключена, читается.": "Ulandi, o'qilmoqda.",
  "Будет создана.": "Yaratiladi.",
  "Будет обновлена.": "Yangilanadi.",
  "Будет обновлена и перейдёт на таблицу из файла.": "Yangilanadi va fayldagi jadvalga o'tadi.",
  "Таблица привязки не подключена — см. строку таблицы выше.": "Bog'lanish jadvali ulanmagan — yuqoridagi jadval qatoriga qarang.",
  "Уже задан.": "Allaqachon berilgan.",
  "Будет задан.": "Beriladi.",
  "Задан.": "Berildi.",
  "Такого региона здесь нет (филиал Linko не найден).": "Bu yerda bunday mintaqa yo'q (Linko filiali topilmadi).",
  "Дилеров ведёт привязка Google Sheets — меняется там.": "Dilerlarni Google Sheets bog'lanishi yuritadi — o'sha yerda o'zgartiriladi.",
  "Название дилера — от 1 до 200 символов.": "Diler nomi — 1 dan 200 tagacha belgi.",
  "Других имён дилера — не больше 20, каждое — не длиннее {n} символов.": "Dilerning boshqa nomlari — 20 tadan ko'p emas, har biri {n} belgidan uzun emas.",
};

/** «Октябрь 2026» → «Oktabr 2026», «октябрь 2026» → «oktabr 2026»: месяц плана с сервера (точные ключи — работают и внутри шаблонов). */
const months: [string, string][] = [
  ["Январь", "Yanvar"],
  ["Февраль", "Fevral"],
  ["Март", "Mart"],
  ["Апрель", "Aprel"],
  ["Май", "May"],
  ["Июнь", "Iyun"],
  ["Июль", "Iyul"],
  ["Август", "Avgust"],
  ["Сентябрь", "Sentabr"],
  ["Октябрь", "Oktabr"],
  ["Ноябрь", "Noyabr"],
  ["Декабрь", "Dekabr"],
];
for (let year = 2025; year <= 2030; year++) {
  for (const [ru, uz] of months) {
    dict[`${ru} ${year}`] = `${uz} ${year}`;
    dict[`${ru.toLowerCase()} ${year}`] = `${uz.toLowerCase()} ${year}`;
  }
}

export default dict;
