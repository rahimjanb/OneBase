import type { Dict } from "../core";

/**
 * Тексты, которые приходят с сервера: ключ — русский текст сервера, значения в нём — {подстановки} (перевод по шаблону).
 * Порядок важен: из подходящих шаблонов берётся первый, поэтому более точные шаблоны стоят раньше общих.
 */
const dict: Dict = {
  // ---------- Слова, которые сервер подставляет в фразы (склонения по числу, единицы) ----------
  визит: "tashrif",
  визита: "tashrif",
  визитов: "tashrif",
  категорию: "toifa",
  янв: "yan",
  фев: "fev",
  мар: "mar",
  апр: "apr",
  июн: "iyn",
  июл: "iyl",
  авг: "avg",
  сен: "sen",
  окт: "okt",
  ноя: "noy",
  дек: "dek",

  // ---------- Проблемные агенты: подписи и пояснения флагов ----------
  "Мало данных": "Ma'lumot kam",
  "Мало с визита": "Tashrifdan kam",
  "{n} {visits} за месяц — меньше {min}, агент не оценивается.": "Oy davomida {n} {visits} — {min} tadan kam, agent baholanmaydi.",
  "Заказов ({orders}) больше, чем визитов ({visits}) — конверсия больше 100%. Конверсия не оценивается, агент исключён из медиан региона.":
    "Buyurtmalar ({orders}) tashriflardan ({visits}) ko'p — konversiya 100% dan yuqori. Konversiya baholanmaydi, agent mintaqa medianalaridan chiqarilgan.",
  "Конверсия {pct} против {median} у медианного агента региона: {orders} {ordersWord} на {visits} {visitsWord}.":
    "Konversiya {pct}, mintaqaning median agentida — {median}: {visits} {visitsWord}ga {orders} {ordersWord}.",
  "Конверсия {pct}: {orders} {ordersWord} на {visits} {visitsWord}.": "Konversiya {pct}: {visits} {visitsWord}ga {orders} {ordersWord}.",
  "{n} {visits} за месяц и ни одной продажи.": "Oy davomida {n} {visits} va birorta ham savdo yo'q.",
  // Суммы приходят как «229 тыс»: число и единица — отдельными подстановками, чтобы единица тоже перевелась.
  "{a} {aUnit} сум с визита против {b} {bUnit} у медианного агента региона.": "Tashrifdan {a} {aUnit} so'm, mintaqaning median agentida — {b} {bUnit}.",
  "{a} сум с визита против {b} {bUnit} у медианного агента региона.": "Tashrifdan {a} so'm, mintaqaning median agentida — {b} {bUnit}.",
  "{sum} сум с визита против {median} у медианного агента региона.": "Tashrifdan {sum} so'm, mintaqaning median agentida — {median}.",
  "Средний чек {a} {aUnit} против {b} {bUnit} у медианного агента региона — при {n} заказах. Конверсия при этом нормальная, значит дело не в отказах, а в том, что в заказе мало товара.":
    "O'rtacha chek {a} {aUnit}, mintaqaning median agentida — {b} {bUnit}; buyurtmalar: {n}. Konversiya esa normal, demak gap rad etishlarda emas, buyurtmada tovar kamligida.",
  "Средний чек {a} против {b} {bUnit} у медианного агента региона — при {n} заказах. Конверсия при этом нормальная, значит дело не в отказах, а в том, что в заказе мало товара.":
    "O'rtacha chek {a}, mintaqaning median agentida — {b} {bUnit}; buyurtmalar: {n}. Konversiya esa normal, demak gap rad etishlarda emas, buyurtmada tovar kamligida.",
  "Средний чек {check} против {median} у медианного агента региона — при {n} заказах. Конверсия при этом нормальная, значит дело не в отказах, а в том, что в заказе мало товара.":
    "O'rtacha chek {check}, mintaqaning median agentida — {median}; buyurtmalar: {n}. Konversiya esa normal, demak gap rad etishlarda emas, buyurtmada tovar kamligida.",
  "Продаёт {n} {categories} при цели {target}.": "{n} {categories} sotadi, maqsad — {target}.",
  "Текущий месяц — {pct} от собственного среднего. Считается с поправкой на неполный месяц: отработано {worked} из {days} дней, без поправки вышло бы {raw}.":
    "Joriy oy — o'z o'rtachasining {pct}. To'liq bo'lmagan oyga tuzatish bilan hisoblanadi: {days} kundan {worked} kun ishlangan, tuzatishsiz {raw} chiqardi.",

  // ---------- Вторичка: республика, направления, регионы, итоги ----------
  "{n} направление · {m} {regions}": "{n} yo'nalish · {m} {regions}",
  "{n} направления · {m} {regions}": "{n} yo'nalish · {m} {regions}",
  "{n} направлений · {m} {regions}": "{n} yo'nalish · {m} {regions}",
  "{m} регион": "{m} mintaqa",
  "{m} региона": "{m} mintaqa",
  "{m} регионов": "{m} mintaqa",
  "ОКБ {okb} · в маршруте {route}": "OKB {okb} · marshrutda {route}",
  "Итого с планом": "Rejali qatorlar jami",
  "Без разбивки по категориям": "Toifalarga bo'linmagan",
  "Без направления": "Yo'nalishsiz",
  "Регионы, не назначенные РМ": "RMga biriktirilmagan mintaqalar",
  "Без агента": "Agentsiz",
  "Без категории": "Toifasiz",

  // ---------- P&L дилеров: темп роста «месяц к прошлому месяцу» ----------
  "январь {y1} к декабрь {y2}": "yanvar {y1} / dekabr {y2}",
  "февраль {y1} к январь {y2}": "fevral {y1} / yanvar {y2}",
  "март {y1} к февраль {y2}": "mart {y1} / fevral {y2}",
  "апрель {y1} к март {y2}": "aprel {y1} / mart {y2}",
  "май {y1} к апрель {y2}": "may {y1} / aprel {y2}",
  "июнь {y1} к май {y2}": "iyun {y1} / may {y2}",
  "июль {y1} к июнь {y2}": "iyul {y1} / iyun {y2}",
  "август {y1} к июль {y2}": "avgust {y1} / iyul {y2}",
  "сентябрь {y1} к август {y2}": "sentabr {y1} / avgust {y2}",
  "октябрь {y1} к сентябрь {y2}": "oktabr {y1} / sentabr {y2}",
  "ноябрь {y1} к октябрь {y2}": "noyabr {y1} / oktabr {y2}",
  "декабрь {y1} к ноябрь {y2}": "dekabr {y1} / noyabr {y2}",

  // ---------- Первичка и экспорт ----------
  "Прямые клиенты завода": "Zavodning to'g'ridan-to'g'ri mijozlari",
  "{n} точка Linko": "{n} ta Linko nuqtasi",
  "{n} точки Linko": "{n} ta Linko nuqtasi",
  "{n} точек Linko": "{n} ta Linko nuqtasi",
  "Без разбивки": "Bo'linmagan",
  "план одной строкой, без категорий": "reja bitta qatorda, toifalarsiz",
  "Без товара": "Tovarsiz",
  "Заказов точек завода в другой валюте за {year} год: {n} — их вес учтён, сумма нет (курса в Linko нет).":
    "{year}-yil zavod nuqtalarining boshqa valyutadagi buyurtmalari: {n} — vazni hisobga olingan, summasi yo'q (Linko'da kurs yo'q).",
  "Строк первички после отчётного дня ({date}): {n} — в отчёт войдут, когда их день станет отчётным.":
    "Hisobot kunidan ({date}) keyingi birlamchi qatorlari: {n} — ularning kuni hisobot kuni bo'lganda hisobotga kiradi.",
  "Заказов в другой валюте за {year} год: {n} — их вес учтён, сумма нет (курса в Linko нет).":
    "{year}-yil boshqa valyutadagi buyurtmalar: {n} — vazni hisobga olingan, summasi yo'q (Linko'da kurs yo'q).",
  "Строк экспорта после отчётного дня ({date}): {n} — в отчёт войдут, когда их день станет отчётным.":
    "Hisobot kunidan ({date}) keyingi eksport qatorlari: {n} — ularning kuni hisobot kuni bo'lganda hisobotga kiradi.",

  // ---------- Остатки и аутсток: область, коробка, ТОП ----------
  "Регион не найден": "Mintaqa topilmadi",
  "РМ не найден": "RM topilmadi",
  "по отгрузкам завода: {units} × {unit} кг = {box} кг": "zavod jo'natmalari bo'yicha: {units} × {unit} kg = {box} kg",
  "в названии {box} кг — не делится на вес штуки {unit} кг, коробки не считаются; завод отгружает по одной единице":
    "nomida {box} kg — dona vazni {unit} kg ga bo'linmaydi, qutilar hisoblanmaydi; zavod bittadan birlik jo'natadi",
  "в названии вес единицы {box} кг, а не коробки — коробки не считаются; завод отгружает по одной единице":
    "nomida quti emas, birlik vazni {box} kg — qutilar hisoblanmaydi; zavod bittadan birlik jo'natadi",
  "{note}; завод отгружает по одной единице": "{note}; zavod bittadan birlik jo'natadi",
  "в названии нет веса коробки": "nomida quti vazni yo'q",
  "нет продаж — вес штуки неизвестен": "savdo yo'q — dona vazni noma'lum",
  "по названию: {box} кг, весовой товар": "nomi bo'yicha: {box} kg, vaznli tovar",
  "в названии {box} кг — не делится на вес штуки {unit} кг, коробки не считаются": "nomida {box} kg — dona vazni {unit} kg ga bo'linmaydi, qutilar hisoblanmaydi",
  "в названии вес единицы {box} кг, а не коробки — коробки не считаются": "nomida quti emas, birlik vazni {box} kg — qutilar hisoblanmaydi",
  "по названию: {box} кг = {n} × {unit} кг": "nomi bo'yicha: {box} kg = {n} × {unit} kg",
  "товара нет в справочнике": "tovar ma'lumotnomada yo'q",
  "ТОП — {n} товаров из настройки Sales:TopProducts (коды Linko)": "TOP — Sales:TopProducts sozlamasidan {n} ta tovar (Linko kodlari)",
  "ТОП-товары не настроены (Sales:TopProducts) — показаны все SKU": "TOP tovarlar sozlanmagan (Sales:TopProducts) — barcha SKU ko'rsatilgan",

  // ---------- Продажи по SKU: ошибки периода и кварталы ----------
  "Конец периода «{to}» — месяц в формате ГГГГ-ММ, например {example}.": "Davr oxiri «{to}» — oy YYYY-MM formatida bo'lishi kerak, masalan {example}.",
  "Период — до {month}.": "Davr — {month} gacha.",
  "Начало периода «{from}» — месяц в формате ГГГГ-ММ, например {example}.": "Davr boshi «{from}» — oy YYYY-MM formatida bo'lishi kerak, masalan {example}.",
  "Начало периода ({start}) позже конца ({end}).": "Davr boshi ({start}) oxiridan ({end}) keyin turibdi.",
  "Период — не длиннее {max} месяцев (выбрано {n}).": "Davr — {max} oydan uzun bo'lmasin ({n} tanlangan).",
  "Регион — идентификатор региона из списка регионов.": "Mintaqa — mintaqalar ro'yxatidagi mintaqa identifikatori.",
  "I кв. {year}: {from}–{to}": "{year}-yil I chorak: {from}–{to}",
  "I кв. {year}: {month}": "{year}-yil I chorak: {month}",
  "I кв. {year}": "{year}-yil I chorak",
  "II кв. {year}: {from}–{to}": "{year}-yil II chorak: {from}–{to}",
  "II кв. {year}: {month}": "{year}-yil II chorak: {month}",
  "II кв. {year}": "{year}-yil II chorak",
  "III кв. {year}: {from}–{to}": "{year}-yil III chorak: {from}–{to}",
  "III кв. {year}: {month}": "{year}-yil III chorak: {month}",
  "III кв. {year}": "{year}-yil III chorak",
  "IV кв. {year}: {from}–{to}": "{year}-yil IV chorak: {from}–{to}",
  "IV кв. {year}: {month}": "{year}-yil IV chorak: {month}",
  "IV кв. {year}": "{year}-yil IV chorak",

  // ---------- Настройка продаж: регионы и ошибки ----------
  "экспорт и опт — не регион вторички": "eksport va ulgurji — ikkilamchi mintaqasi emas",
  "во вторичку не входит": "ikkilamchiga kirmaydi",
  "старый филиал — считается в регионе «{region}»; в первичке — отдельный дилер со своим планом":
    "eski filial — «{region}» mintaqasida hisoblanadi; birlamchida — o'z rejasiga ega alohida diler",
  "старый филиал — считается в регионе «{region}»": "eski filial — «{region}» mintaqasida hisoblanadi",
  "Направление не найдено.": "Yo'nalish topilmadi.",
  "Укажите название направления.": "Yo'nalish nomini kiriting.",
  "Название направления — не длиннее {n} символов.": "Yo'nalish nomi — {n} belgidan uzun bo'lmasin.",
  "Тип направления — «РМ» (RegionalManager) или «Канал» (Channel).": "Yo'nalish turi — «RM» (RegionalManager) yoki «Kanal» (Channel).",
  "Имя руководителя — не длиннее {n} символов.": "Rahbar ismi — {n} belgidan uzun bo'lmasin.",
  "Направление «{name}» уже есть.": "«{name}» yo'nalishi allaqachon bor.",
  "Такого направления нет — обновите страницу.": "Bunday yo'nalish yo'q — sahifani yangilang.",
  "Имя СВР — не длиннее {n} символов.": "SVR ismi — {n} belgidan uzun bo'lmasin.",
  "Название дилера — не длиннее {n} символов.": "Diler nomi — {n} belgidan uzun bo'lmasin.",
  "Неизвестные цели: {list}": "Noma'lum maqsadlar: {list}",

  // ---------- Синхронизация с Linko: запуск, шаги, ошибки ----------
  "Linko не настроен или выключен: «Настройки → Интеграции → Продажи → Linko».": "Linko sozlanmagan yoki o'chirilgan: «Sozlamalar → Integratsiyalar → Savdo → Linko».",
  // Шаги и ошибки синхронизации Linko и проверка подключения — в serverSettings.ts (общие с «Настройки → Интеграции»).

  // Акт сверки и DSO: статьи, группы и каналы (с сервера)
  "2% кешбэк": "2% keshbek",
  "Ретробонус": "Retrobonus",
  "Скидки и акции": "Chegirma va aksiyalar",
  "ЗП супервайзеров": "Supervayzerlar ish haqi",
  "Ритейл": "Riteyl",
  "Контрагенты": "Kontragentlar",
  "Прямые клиенты и сети": "To'g'ridan-to'g'ri mijozlar va tarmoqlar",

  // Акции: выводы (шаблоны с сервера)
  "Больше всего ушло на «{promo}» — {sum}, {share} всех скидок.":
    "Eng ko'p «{promo}» ga ketdi — {sum}, barcha chegirmalarning {share}.",
  "Выше всех доля скидок к продажам у региона {region}: {pct}.":
    "Chegirmalarning savdoga ulushi eng yuqori — {region} mintaqasida: {pct}.",
  "Без акции (скидку поставили вручную) — {sum}, {share} всех скидок; больше всего у региона {region} ({regionSum}).":
    "Aksiyasiz (chegirma qo'lda qo'yilgan) — {sum}, barcha chegirmalarning {share}; eng ko'pi {region} mintaqasida ({regionSum}).",
  "Вне срока акции принято {sum}, больше всего по «{promo}».":
    "Aksiya muddatidan tashqari {sum} qabul qilingan, eng ko'pi «{promo}» bo'yicha.",
  "Лучше всего окупилась «{promo}»: продажи товаров акции {growth} к базе, на 1 сум скидки — {ret} сум дополнительной выручки.":
    "Eng yaxshi o'zini «{promo}» oqladi: aksiya tovarlari savdosi bazaga {growth}, 1 so'm chegirmaga — {ret} so'm qo'shimcha tushum.",
  "Без прироста продаж: {n} акц., скидок {sum} (крупнейшая — «{promo}»). Их стоит пересмотреть.":
    "Savdo o'smagan: {n} ta aksiya, chegirmalar {sum} (eng kattasi — «{promo}»). Ularni qayta ko'rib chiqish kerak.",
  "Ручные скидки ставят каждый месяц {months} мес. подряд — {n} агент(ов): {names}.":
    "Qo'lda chegirmani {months} oy ketma-ket har oy qo'yishadi — {n} ta agent: {names}.",
  "За {months} мес. {n} магазин(ов) почти каждый месяц покупают со скидкой (закупки {sum}); ещё {k} в месяцы со скидкой берут в 2+ раза больше — ждут акцию.":
    "{months} oy davomida {n} ta do'kon deyarli har oy chegirma bilan xarid qiladi (xaridlar {sum}); yana {k} tasi chegirmali oylarda 2+ barobar ko'p oladi — aksiyani kutadi.",
  "Скидок больше среднего ({avg}), а продажи к прошлому месяцу не выросли: {list}.":
    "Chegirmalar o'rtachadan ko'p ({avg}), savdo esa o'tgan oyga nisbatan o'smagan: {list}.",

  // KPI: показатели Linko (подписи с сервера)
  "Бамбук + Шоколад": "Bambuk + Shokolad",
  "Бамбук": "Bambuk",
  "Кекс 1 кг": "Keks 1 kg",
  "Кекс 2.5 кг": "Keks 2.5 kg",
  "Кекс": "Keks",
  "Могуль": "Mogul",
  "Печенье": "Pechene",
  "Помадка 1 кг": "Pomadka 1 kg",
  "Помадка 230 гр": "Pomadka 230 gr",
  "Помадка 420 Гр": "Pomadka 420 gr",
  "Тотал": "Total",
  "Трубочка + Печенье": "Trubochka + Pechene",
  "Трубочка": "Trubochka",
  "Фрулли": "Frulli",
  "Шоколад": "Shokolad",
};

export default dict;
