import type { Dict } from "../core";

/**
 * Тексты, которые приходят с сервера: ключ — русский текст сервера, значения в нём — {подстановки} (перевод по шаблону).
 * Sales Base (Application/Field, FieldController, FieldWorkController), задачи отделов (Application/Work, WorkController), push.
 *
 * Порядок важен: шаблоны проверяются сверху вниз, первый подошедший побеждает. Поэтому частные варианты стоят выше общих:
 * суммы с «млн / тыс / млрд» — выше варианта без единицы, уведомления с названием задачи от AI — выше «Новая задача: {title}».
 * Заголовок «Требует внимания» («Агент {имя}») ключом не задан: шаблон «Агент {name}» перехватывал бы любые тексты «Агент …»
 * (в том числе других разделов) — его собирает components/field/views.tsx из имени агента.
 */
const dict: Dict = {
  // ── Ошибки: доступ и общие ──
  "Не найдено.": "Topilmadi.",
  "Вас ещё не добавили в Sales Base. Обратитесь к РМ или администратору.": "Sizni hali Sales Base'ga qo'shishmagan. RM yoki administratorga murojaat qiling.",
  "Ваш доступ к Sales Base отключён.": "Sales Base'ga kirish huquqingiz o'chirilgan.",
  "У вашей роли нет доступа к Sales Base.": "Rolingizga Sales Base'ga kirish huquqi berilmagan.",
  "Это уже изменили одновременно с вами (например, повторное нажатие) — обновите страницу.":
    "Buni siz bilan bir vaqtda o'zgartirishdi (masalan, tugma qayta bosilgan) — sahifani yangilang.",
  "Справочник Sales Base не загрузился.": "Sales Base ma'lumotnomasi yuklanmadi.",

  // ── Ошибки: точки ──
  "Точка не найдена.": "Nuqta topilmadi.",
  "Менять карточку точки может её агент, супервайзер или РМ.": "Nuqta kartasini uning agenti, supervayzer yoki RM o'zgartira oladi.",
  "Приоритет, статус и план точки меняет супервайзер или РМ.": "Nuqtaning ustuvorligi, holati va rejasini supervayzer yoki RM o'zgartiradi.",
  "План точки — от 0 до 100 млрд сум.": "Nuqta rejasi — 0 dan 100 mlrd so'mgacha.",
  "Назначить точку можно только агенту своей команды.": "Nuqtani faqat o'z jamoangiz agentiga biriktirish mumkin.",
  "Новый агент не найден или отключён.": "Yangi agent topilmadi yoki o'chirilgan.",
  "Точку ведёт агент другой команды.": "Nuqtani boshqa jamoa agenti olib boradi.",
  "Точку уже ведёт этот агент.": "Nuqtani allaqachon shu agent olib boradi.",

  // ── Ошибки: агенты, дашборд, KPI ──
  "Укажите агента.": "Agentni tanlang.",
  "Агент не найден.": "Agent topilmadi.",
  "Дашборд команды — для супервайзера и РМ.": "Jamoa dashbordi — supervayzer va RM uchun.",
  "Год — от 2000 до 2100.": "Yil — 2000 dan 2100 gacha.",

  // ── Ошибки: команды и участники ──
  "Укажите имя участника (до 200 символов).": "Ishtirokchi ismini kiriting (200 belgigacha).",
  "Телефон — до 32 символов.": "Telefon — 32 belgigacha.",
  "Такой команды нет.": "Bunday jamoa yo'q.",
  "Участник не найден.": "Ishtirokchi topilmadi.",
  "Свою роль, доступ, филиалы, команду и привязку к Linko изменить нельзя — попросите другого РМ или администратора.":
    "O'z rolingiz, kirish huquqi, filiallar, jamoa va Linko bog'lanishini o'zgartirib bo'lmaydi — boshqa RM yoki administratordan so'rang.",
  "РМ добавляет и меняет управляющий всей организацией.": "RMni butun tashkilotni boshqaradigan rahbar qo'shadi va o'zgartiradi.",
  "Выберите команду своего филиала.": "O'z filialingiz jamoasini tanlang.",
  "Этот сотрудник Linko работает в другом филиале.": "Bu Linko xodimi boshqa filialda ishlaydi.",
  "Сотрудник Linko не найден.": "Linko xodimi topilmadi.",
  "Этот сотрудник Linko уже есть в Sales Base.": "Bu Linko xodimi Sales Base'da allaqachon bor.",
  "Укажите название команды (до 200 символов).": "Jamoa nomini kiriting (200 belgigacha).",
  "Команда не найдена.": "Jamoa topilmadi.",
  "Выберите свой филиал.": "O'z filialingizni tanlang.",
  "Во главе команды может быть только супервайзер.": "Jamoa rahbari faqat supervayzer bo'lishi mumkin.",
  "Этот супервайзер ведёт команды другого филиала — назначает РМ всей организации.":
    "Bu supervayzer boshqa filial jamoalarini boshqaradi — uni butun tashkilot RMi tayinlaydi.",
  "Этот пользователь уже привязан к другому участнику.": "Bu foydalanuvchi allaqachon boshqa ishtirokchiga bog'langan.",
  "Состав Sales Base меняет РМ или администратор.": "Sales Base tarkibini RM yoki administrator o'zgartiradi.",
  "Это делает РМ всей организации или администратор.": "Buni butun tashkilot RMi yoki administrator qiladi.",

  // ── Ошибки: вход и пароль участника (FieldController) ──
  "Свой пароль меняйте в профиле OneBase.": "O'z parolingizni OneBase profilida o'zgartiring.",
  "Пароль — не короче {n} символов.": "Parol — kamida {n} ta belgi.",
  "У этого пользователя права, которых у вас нет, — пароль меняет администратор.": "Bu foydalanuvchida sizda yo'q huquqlar bor — parolni administrator o'zgartiradi.",
  "Этот вход — не только для Sales Base: пароль меняет администратор пользователей.":
    "Bu kirish faqat Sales Base uchun emas: parolni foydalanuvchilar administratori o'zgartiradi.",
  "Вход отключён администратором — включает администратор пользователей.": "Kirish administrator tomonidan o'chirilgan — uni foydalanuvchilar administratori yoqadi.",
  "Роль «{role}» не найдена — перезапустите API, роли создаются при запуске.": "«{role}» roli topilmadi — API ni qayta ishga tushiring, rollar ishga tushirishda yaratiladi.",
  "Роль «{role}» даёт права, которых у вас нет.": "«{role}» roli sizda yo'q huquqlarni beradi.",
  "Логин — от 3 до 64 символов: латинские буквы, цифры, точка, дефис, подчёркивание или @.":
    "Login — 3 dan 64 tagacha belgi: lotin harflari, raqamlar, nuqta, defis, pastki chiziq yoki @.",
  "Логин «{login}» уже занят.": "«{login}» logini allaqachon band.",
  "Администратор": "Administrator",

  // ── Ошибки: настройки Sales Base ──
  "Проверьте значения: радиус 10–5000 м, допуск GPS 0–2000 м, точек 1–100, визит 1–240 мин, скорость 5–120 км/ч, давно не посещали 1–180 дн., падение 5–95%, отставание 1–100 п.п.":
    "Qiymatlarni tekshiring: radius 10–5000 m, GPS noaniqligi 0–2000 m, nuqtalar 1–100, tashrif 1–240 daq., tezlik 5–120 km/soat, uzoq vaqt tashrif qilinmagan 1–180 kun, pasayish 5–95%, orqada qolish 1–100 f.p.",

  // ── Ошибки: маршруты ──
  "Маршрут не найден.": "Marshrut topilmadi.",
  "Маршрут на этот день уже есть — перестроить его может супервайзер или РМ.": "Bu kunga marshrut allaqachon bor — uni supervayzer yoki RM qayta tuzishi mumkin.",
  "Состав маршрута меняет супервайзер команды или РМ.": "Marshrut tarkibini jamoa supervayzeri yoki RM o'zgartiradi.",
  "В маршруте — не больше 200 точек.": "Marshrutda ko'pi bilan 200 ta nuqta bo'ladi.",
  "Часть точек вне вашей зоны.": "Nuqtalarning bir qismi sizning hududingizdan tashqarida.",
  "Точка маршрута не найдена.": "Marshrut nuqtasi topilmadi.",
  "Точка уже посещена или визит идёт.": "Nuqtaga allaqachon tashrif qilingan yoki tashrif davom etmoqda.",
  "Менять маршрут может супервайзер команды или РМ.": "Marshrutni jamoa supervayzeri yoki RM o'zgartira oladi.",

  // ── Ошибки: задачи (Sales Base и отделы) ──
  "Задача не найдена.": "Vazifa topilmadi.",
  "Укажите исполнителя.": "Ijrochini tanlang.",
  "Поставить задачу можно агенту своей команды.": "Vazifani o'z jamoangiz agentiga berish mumkin.",
  "Менять задачу может тот, кто её поставил, супервайзер или РМ.": "Vazifani uni bergan kishi, supervayzer yoki RM o'zgartira oladi.",
  "Передать задачу можно только участнику своей зоны.": "Vazifani faqat o'z hududingiz ishtirokchisiga topshirish mumkin.",
  "Нельзя перевести задачу из «{from}» в «{to}».": "Vazifani «{from}» holatidan «{to}» holatiga o'tkazib bo'lmaydi.",
  "Перенести можно на сегодня или позже.": "Bugunga yoki keyingi kunlarga ko'chirish mumkin.",
  "Название задачи — от 1 до 300 символов.": "Vazifa nomi — 1 dan 300 belgigacha.",
  "Описание — до 4000 символов.": "Tavsif — 4000 belgigacha.",
  "Срок не может быть в прошлом.": "Muddat o'tgan sana bo'lishi mumkin emas.",
  "Отдел не найден.": "Bo'lim topilmadi.",
  "Задачи этого отдела видят его сотрудники и руководство.": "Bu bo'lim vazifalarini uning xodimlari va rahbariyat ko'radi.",
  "Исполнитель — действующий сотрудник этого отдела.": "Ijrochi — shu bo'limning faol xodimi bo'lishi kerak.",
  "Менять задачу может её автор или руководство.": "Vazifani uning muallifi yoki rahbariyat o'zgartira oladi.",
  "Так изменить статус задачи нельзя.": "Vazifa holatini bunday o'zgartirib bo'lmaydi.",

  // ── Ошибки: визиты, фото, совместные выезды ──
  "Визит не найден.": "Tashrif topilmadi.",
  "Визит начинает участник Sales Base.": "Tashrifni Sales Base ishtirokchisi boshlaydi.",
  "Сначала завершите начатый визит.": "Avval boshlangan tashrifni yakunlang.",
  "Неверные координаты.": "Koordinatalar noto'g'ri.",
  "Точка маршрута не совпадает с визитом.": "Marshrut nuqtasi tashrifga mos kelmaydi.",
  "Вы не участник этого совместного выезда.": "Siz bu birgalikdagi chiqish ishtirokchisi emassiz.",
  "Визит уже завершён.": "Tashrif allaqachon yakunlangan.",
  "Сумма — от 0 до 10 млрд сум.": "Summa — 0 dan 10 mlrd so'mgacha.",
  "Фото добавляется во время визита или в течение 30 минут после него.": "Foto tashrif vaqtida yoki undan keyin 30 daqiqa ichida qo'shiladi.",
  "Фото — до 10 МБ.": "Foto — 10 MB gacha.",
  "Фото — JPEG, PNG, WebP или HEIC.": "Foto — JPEG, PNG, WebP yoki HEIC.",
  "Фото нет.": "Foto yo'q.",
  "Выберите фото.": "Fotoni tanlang.",
  "Совместный выезд назначает супервайзер или РМ.": "Birgalikdagi chiqishni supervayzer yoki RM tayinlaydi.",
  "Укажите цель выезда (до 500 символов).": "Chiqish maqsadini kiriting (500 belgigacha).",
  "В совместном выезде — от 2 до 6 участников.": "Birgalikdagi chiqishda 2 tadan 6 tagacha ishtirokchi bo'ladi.",
  "Участники — только из вашей зоны.": "Ishtirokchilar — faqat sizning hududingizdan.",
  "Дата выезда — сегодня или позже.": "Chiqish sanasi — bugun yoki keyinroq.",
  "Совместный выезд не найден.": "Birgalikdagi chiqish topilmadi.",
  "Итог выезда записывает участник, супервайзер или РМ.": "Chiqish natijasini ishtirokchi, supervayzer yoki RM yozadi.",

  // ── Ошибки: рекомендации AI ──
  "Рекомендации обновляет супервайзер или РМ.": "Tavsiyalarni supervayzer yoki RM yangilaydi.",
  "Рекомендация не найдена.": "Tavsiya topilmadi.",
  "По рекомендации уже принято решение.": "Tavsiya bo'yicha qaror allaqachon qabul qilingan.",
  "Решение по рекомендации принимает супервайзер команды или РМ.": "Tavsiya bo'yicha qarorni jamoa supervayzeri yoki RM qabul qiladi.",
  "Исполнитель — только агент вашей зоны.": "Ijrochi — faqat sizning hududingiz agenti.",
  "Исполнитель больше не в вашей зоне или отключён — выберите другого агента.": "Ijrochi endi sizning hududingizda emas yoki o'chirilgan — boshqa agentni tanlang.",

  // ── Ошибки: push-уведомления ──
  "Push-уведомления сейчас недоступны.": "Push-bildirishnomalar hozir mavjud emas.",
  "Неверная подписка браузера.": "Brauzer obunasi noto'g'ri.",
  "Проверку можно повторить через полминуты.": "Tekshiruvni yarim daqiqadan keyin takrorlash mumkin.",

  // ── «Требует внимания» (attention.details) и пояснение статуса агента (statusNote — части через « · ») ──
  "Выполнение {pct}%": "Bajarilish {pct}%",
  "{n} точки не посещены": "{n} ta nuqtaga tashrif qilinmagan",
  "{n} задачи просрочены": "{n} ta vazifa muddati o'tgan",
  "нет визитов и заказов": "tashriflar va buyurtmalar yo'q",
  "{n} визит(а) далеко от точки": "{n} ta tashrif nuqtadan uzoqda",
  "пропущено точек: {n}": "o'tkazib yuborilgan nuqtalar: {n}",
  "просрочено задач: {n}": "muddati o'tgan vazifalar: {n}",
  "отставание от темпа {n} п.п.": "sur'atdan {n} f.p. orqada",
  "визитов далеко от точки: {n}": "nuqtadan uzoqdagi tashriflar: {n}",

  // ── История визитов Linko в карточке точки, решения по рекомендациям, автор задачи ──
  "Выполнен": "Bajarilgan",
  "Не выполнен": "Bajarilmagan",
  "Запланирован": "Rejalashtirilgan",
  "в плане": "rejada",
  "вне плана": "rejadan tashqari",
  "AI (авто)": "AI (avto)",
  "Автопланирование": "Avtorejalashtirish",
  "Система": "Tizim",

  // ── Уведомления с названием задачи от AI или системы (выше общих «Новая задача: {title}» и т. п.) ──
  "Новая задача: Посетить «{name}» в течение 2 дней": "Yangi vazifa: «{name}» nuqtasiga 2 kun ichida tashrif qilish",
  "Новая задача: Вернуть клиента «{name}»": "Yangi vazifa: «{name}» mijozini qaytarish",
  "Новая задача: Давно не посещали «{name}»": "Yangi vazifa: «{name}» nuqtasiga uzoq vaqt tashrif qilinmagan",
  "Новая задача: Агент {name}: выполнение плана {share}%": "Yangi vazifa: Agent {name}: reja bajarilishi {share}%",
  "Новая задача: Повторить визит в «{name}»": "Yangi vazifa: «{name}» nuqtasiga qayta tashrif qilish",
  "Новая задача: Повторный визит: {name}": "Yangi vazifa: Takroriy tashrif: {name}",
  "Просрочена задача: Посетить «{name}» в течение 2 дней": "Vazifa muddati o'tdi: «{name}» nuqtasiga 2 kun ichida tashrif qilish",
  "Просрочена задача: Вернуть клиента «{name}»": "Vazifa muddati o'tdi: «{name}» mijozini qaytarish",
  "Просрочена задача: Давно не посещали «{name}»": "Vazifa muddati o'tdi: «{name}» nuqtasiga uzoq vaqt tashrif qilinmagan",
  "Просрочена задача: Агент {name}: выполнение плана {share}%": "Vazifa muddati o'tdi: Agent {name}: reja bajarilishi {share}%",
  "Просрочена задача: Повторить визит в «{name}»": "Vazifa muddati o'tdi: «{name}» nuqtasiga qayta tashrif qilish",
  "Просрочена задача: Повторный визит: {name}": "Vazifa muddati o'tdi: Takroriy tashrif: {name}",
  "Выполнена задача: Посетить «{name}» в течение 2 дней": "Vazifa bajarildi: «{name}» nuqtasiga 2 kun ichida tashrif qilish",
  "Выполнена задача: Вернуть клиента «{name}»": "Vazifa bajarildi: «{name}» mijozini qaytarish",
  "Выполнена задача: Давно не посещали «{name}»": "Vazifa bajarildi: «{name}» nuqtasiga uzoq vaqt tashrif qilinmagan",
  "Выполнена задача: Агент {name}: выполнение плана {share}%": "Vazifa bajarildi: Agent {name}: reja bajarilishi {share}%",
  "Выполнена задача: Повторить визит в «{name}»": "Vazifa bajarildi: «{name}» nuqtasiga qayta tashrif qilish",
  "Выполнена задача: Повторный визит: {name}": "Vazifa bajarildi: Takroriy tashrif: {name}",
  "Задача подтверждена: Посетить «{name}» в течение 2 дней": "Vazifa tasdiqlandi: «{name}» nuqtasiga 2 kun ichida tashrif qilish",
  "Задача подтверждена: Вернуть клиента «{name}»": "Vazifa tasdiqlandi: «{name}» mijozini qaytarish",
  "Задача подтверждена: Давно не посещали «{name}»": "Vazifa tasdiqlandi: «{name}» nuqtasiga uzoq vaqt tashrif qilinmagan",
  "Задача подтверждена: Агент {name}: выполнение плана {share}%": "Vazifa tasdiqlandi: Agent {name}: reja bajarilishi {share}%",
  "Задача подтверждена: Повторить визит в «{name}»": "Vazifa tasdiqlandi: «{name}» nuqtasiga qayta tashrif qilish",
  "Задача подтверждена: Повторный визит: {name}": "Vazifa tasdiqlandi: Takroriy tashrif: {name}",

  // ── Уведомления: задачи (Sales Base и отделы) ──
  "Новая задача: {title}": "Yangi vazifa: {title}",
  "Упоминание: {title}": "Sizni tilga olishdi: {title}",
  "Вам передана задача: {title}": "Sizga vazifa topshirildi: {title}",
  "Задача «{title}» передана другому исполнителю": "«{title}» vazifasi boshqa ijrochiga topshirildi",
  "Изменена задача: {title}": "Vazifa o'zgartirildi: {title}",
  "Задача изменена: {title}": "Vazifa o'zgartirildi: {title}",
  "Выполнена задача: {title}": "Vazifa bajarildi: {title}",
  "Задача подтверждена: {title}": "Vazifa tasdiqlandi: {title}",
  "Задача перенесена: {title}": "Vazifa ko'chirildi: {title}",
  "Агент перенёс задачу: {title}": "Agent vazifani ko'chirdi: {title}",
  "Перенесена задача: {title}": "Vazifa ko'chirildi: {title}",
  "Задача отменена: {title}": "Vazifa bekor qilindi: {title}",
  "Задача возвращена в работу: {title}": "Vazifa ishga qaytarildi: {title}",
  "Просрочена задача: {title}": "Vazifa muddati o'tdi: {title}",
  "Срок: {date}": "Muddat: {date}",
  "Срок был {date}.": "Muddat {date} edi.",

  // ── Уведомления: точки, маршруты, визиты, AI, проверка push ──
  "Вам назначена точка «{name}»": "Sizga «{name}» nuqtasi biriktirildi",
  "Назначил: {actor}. Точка добавлена в сегодняшний маршрут.": "Biriktirdi: {actor}. Nuqta bugungi marshrutga qo'shildi.",
  "Назначил: {actor}.": "Biriktirdi: {actor}.",
  "Точка «{name}» передана агенту {agent}": "«{name}» nuqtasi {agent} agentiga topshirildi",
  "Изменил: {actor}.": "O'zgartirdi: {actor}.",
  "Маршрут на {date}: {n} точек": "{date} uchun marshrut: {n} ta nuqta",
  "Супервайзер изменил маршрут на {date}": "Supervayzer {date} uchun marshrutni o'zgartirdi",
  "Агент отметил точку закрытой": "Agent nuqtani yopilgan deb belgiladi",
  "Совместный выезд {date} в {time}": "Birgalikdagi chiqish {date}, soat {time}",
  "Совместный выезд {date}": "Birgalikdagi chiqish {date}",
  "AI: {n} новых рекомендаций по команде": "AI: jamoa bo'yicha {n} ta yangi tavsiya",
  "Проверка уведомлений": "Bildirishnomalarni tekshirish",
  "Если вы видите это на телефоне — уведомления работают.": "Buni telefonda ko'rayotgan bo'lsangiz — bildirishnomalar ishlayapti.",

  // ── Рекомендации AI-планирования и задачи из них: заголовки ──
  "Посетить «{name}» в течение 2 дней": "«{name}» nuqtasiga 2 kun ichida tashrif qilish",
  "Вернуть клиента «{name}»": "«{name}» mijozini qaytarish",
  "Давно не посещали «{name}»": "«{name}» nuqtasiga uzoq vaqt tashrif qilinmagan",
  "Агент {name}: выполнение плана {share}%": "Agent {name}: reja bajarilishi {share}%",
  "Повторить визит в «{name}»": "«{name}» nuqtasiga qayta tashrif qilish",
  "Назначить «{name}» агенту {agent}": "«{name}» nuqtasini {agent} agentiga biriktirish",
  "Повторный визит: точка {id}": "Takroriy tashrif: nuqta {id}",
  "Повторный визит: {name}": "Takroriy tashrif: {name}",

  // ── Рекомендации AI-планирования: причины. Суммы приходят как «1,5 млн» — сначала варианты с единицей, потом без неё ──
  // Падение продаж
  "Продажи точки за 30 дней снизились на {pct}%: {from} млрд → {to} млрд сум.":
    "Nuqtaning 30 kunlik savdosi {pct}% ga kamaydi: {from} mlrd → {to} mlrd so'm.",
  "Продажи точки за 30 дней снизились на {pct}%: {from} млрд → {to} млн сум.":
    "Nuqtaning 30 kunlik savdosi {pct}% ga kamaydi: {from} mlrd → {to} mln so'm.",
  "Продажи точки за 30 дней снизились на {pct}%: {from} млрд → {to} тыс сум.":
    "Nuqtaning 30 kunlik savdosi {pct}% ga kamaydi: {from} mlrd → {to} ming so'm.",
  "Продажи точки за 30 дней снизились на {pct}%: {from} млн → {to} млн сум.":
    "Nuqtaning 30 kunlik savdosi {pct}% ga kamaydi: {from} mln → {to} mln so'm.",
  "Продажи точки за 30 дней снизились на {pct}%: {from} млн → {to} тыс сум.":
    "Nuqtaning 30 kunlik savdosi {pct}% ga kamaydi: {from} mln → {to} ming so'm.",
  "Продажи точки за 30 дней снизились на {pct}%: {from} тыс → {to} тыс сум.":
    "Nuqtaning 30 kunlik savdosi {pct}% ga kamaydi: {from} ming → {to} ming so'm.",
  "Продажи точки за 30 дней снизились на {pct}%: {from} → {to} сум.":
    "Nuqtaning 30 kunlik savdosi {pct}% ga kamaydi: {from} → {to} so'm.",
  // Клиент перестал покупать
  "Точка не покупает {days} дн., а за прошлые 30 дней купила на {sum} млрд сум.":
    "Nuqta {days} kundan beri xarid qilmayapti, oldingi 30 kunda esa {sum} mlrd so'mlik xarid qilgan.",
  "Точка не покупает {days} дн., а за прошлые 30 дней купила на {sum} млн сум.":
    "Nuqta {days} kundan beri xarid qilmayapti, oldingi 30 kunda esa {sum} mln so'mlik xarid qilgan.",
  "Точка не покупает {days} дн., а за прошлые 30 дней купила на {sum} тыс сум.":
    "Nuqta {days} kundan beri xarid qilmayapti, oldingi 30 kunda esa {sum} ming so'mlik xarid qilgan.",
  "Точка не покупает {days} дн., а за прошлые 30 дней купила на {sum} сум.":
    "Nuqta {days} kundan beri xarid qilmayapti, oldingi 30 kunda esa {sum} so'mlik xarid qilgan.",
  "Точка не покупает больше 30 дней, а за прошлые 30 дней купила на {sum} млрд сум.":
    "Nuqta 30 kundan ortiq xarid qilmayapti, oldingi 30 kunda esa {sum} mlrd so'mlik xarid qilgan.",
  "Точка не покупает больше 30 дней, а за прошлые 30 дней купила на {sum} млн сум.":
    "Nuqta 30 kundan ortiq xarid qilmayapti, oldingi 30 kunda esa {sum} mln so'mlik xarid qilgan.",
  "Точка не покупает больше 30 дней, а за прошлые 30 дней купила на {sum} тыс сум.":
    "Nuqta 30 kundan ortiq xarid qilmayapti, oldingi 30 kunda esa {sum} ming so'mlik xarid qilgan.",
  "Точка не покупает больше 30 дней, а за прошлые 30 дней купила на {sum} сум.":
    "Nuqta 30 kundan ortiq xarid qilmayapti, oldingi 30 kunda esa {sum} so'mlik xarid qilgan.",
  // Давно не посещали
  "Последний визит {days} дн. назад; в среднем точка покупает на {sum} млрд сум в месяц.":
    "Oxirgi tashrif {days} kun oldin bo'lgan; nuqta oyiga o'rtacha {sum} mlrd so'mlik xarid qiladi.",
  "Последний визит {days} дн. назад; в среднем точка покупает на {sum} млн сум в месяц.":
    "Oxirgi tashrif {days} kun oldin bo'lgan; nuqta oyiga o'rtacha {sum} mln so'mlik xarid qiladi.",
  "Последний визит {days} дн. назад; в среднем точка покупает на {sum} тыс сум в месяц.":
    "Oxirgi tashrif {days} kun oldin bo'lgan; nuqta oyiga o'rtacha {sum} ming so'mlik xarid qiladi.",
  "Последний визит {days} дн. назад; в среднем точка покупает на {sum} сум в месяц.":
    "Oxirgi tashrif {days} kun oldin bo'lgan; nuqta oyiga o'rtacha {sum} so'mlik xarid qiladi.",
  "Визитов нет; в среднем точка покупает на {sum} млрд сум в месяц.": "Tashriflar yo'q; nuqta oyiga o'rtacha {sum} mlrd so'mlik xarid qiladi.",
  "Визитов нет; в среднем точка покупает на {sum} млн сум в месяц.": "Tashriflar yo'q; nuqta oyiga o'rtacha {sum} mln so'mlik xarid qiladi.",
  "Визитов нет; в среднем точка покупает на {sum} тыс сум в месяц.": "Tashriflar yo'q; nuqta oyiga o'rtacha {sum} ming so'mlik xarid qiladi.",
  "Визитов нет; в среднем точка покупает на {sum} сум в месяц.": "Tashriflar yo'q; nuqta oyiga o'rtacha {sum} so'mlik xarid qiladi.",
  // Агент отстаёт от плана
  "К {date} выполнено {share}% месячного плана при ожидаемых {expected}%: {fact} из {plan} кг. Нужен разбор маршрута или совместный выезд.":
    "{date} gacha oylik rejaning {share}% bajarildi, kutilgani {expected}%: {fact} / {plan} kg. Marshrutni tahlil qilish yoki birgalikdagi chiqish kerak.",
  // Пропущенная точка ({note} — причина пропуска, переводится, если это быстрый выбор)
  "Точка была в маршруте {date}, но визит не состоялся: {note}": "Nuqta {date} kungi marshrutda edi, lekin tashrif bo'lmadi: {note}",
  "Точка была в маршруте {date}, но визит не состоялся.": "Nuqta {date} kungi marshrutda edi, lekin tashrif bo'lmadi.",
  // Точка без агента: сначала причина «ответственный — не действующий агент», потом любая ({reason} — по словарю)
  "Ответственный в Linko — {name}, он не действующий агент Sales Base. За 90 дней точка купила на {sum} млрд сум. Предлагаемый агент работает в этом филиале.":
    "Linko'dagi mas'ul — {name}, u Sales Base'ning faol agenti emas. 90 kunda nuqta {sum} mlrd so'mlik xarid qilgan. Taklif etilgan agent shu filialda ishlaydi.",
  "Ответственный в Linko — {name}, он не действующий агент Sales Base. За 90 дней точка купила на {sum} млн сум. Предлагаемый агент работает в этом филиале.":
    "Linko'dagi mas'ul — {name}, u Sales Base'ning faol agenti emas. 90 kunda nuqta {sum} mln so'mlik xarid qilgan. Taklif etilgan agent shu filialda ishlaydi.",
  "Ответственный в Linko — {name}, он не действующий агент Sales Base. За 90 дней точка купила на {sum} тыс сум. Предлагаемый агент работает в этом филиале.":
    "Linko'dagi mas'ul — {name}, u Sales Base'ning faol agenti emas. 90 kunda nuqta {sum} ming so'mlik xarid qilgan. Taklif etilgan agent shu filialda ishlaydi.",
  "Ответственный в Linko — {name}, он не действующий агент Sales Base. За 90 дней точка купила на {sum} сум. Предлагаемый агент работает в этом филиале.":
    "Linko'dagi mas'ul — {name}, u Sales Base'ning faol agenti emas. 90 kunda nuqta {sum} so'mlik xarid qilgan. Taklif etilgan agent shu filialda ishlaydi.",
  "{reason}. За 90 дней точка купила на {sum} млрд сум. Предлагаемый агент работает в этом филиале.":
    "{reason}. 90 kunda nuqta {sum} mlrd so'mlik xarid qilgan. Taklif etilgan agent shu filialda ishlaydi.",
  "{reason}. За 90 дней точка купила на {sum} млн сум. Предлагаемый агент работает в этом филиале.":
    "{reason}. 90 kunda nuqta {sum} mln so'mlik xarid qilgan. Taklif etilgan agent shu filialda ishlaydi.",
  "{reason}. За 90 дней точка купила на {sum} тыс сум. Предлагаемый агент работает в этом филиале.":
    "{reason}. 90 kunda nuqta {sum} ming so'mlik xarid qilgan. Taklif etilgan agent shu filialda ishlaydi.",
  "{reason}. За 90 дней точка купила на {sum} сум. Предлагаемый агент работает в этом филиале.":
    "{reason}. 90 kunda nuqta {sum} so'mlik xarid qilgan. Taklif etilgan agent shu filialda ishlaydi.",
  "В Linko у точки нет ответственного агента": "Linko'da nuqtaning mas'ul agenti yo'q",
};

export default dict;
