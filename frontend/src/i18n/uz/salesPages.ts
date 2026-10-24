import type { Dict } from "../core";

/**
 * Страницы раздела «Продажи» (app/(app)/sales/**): заголовки, плитки, подсказки, заметки и «Как считается».
 * Термины — как в «Полевом контроле» (fieldControl.ts); здесь только то, чего там нет.
 */
const dict: Dict = {
  // Общее: загрузка, месяцы, склонения
  "Загрузка": "Yuklanmoqda",
  "январь": "yanvar",
  "февраль": "fevral",
  "март": "mart",
  "апрель": "aprel",
  "май": "may",
  "июнь": "iyun",
  "июль": "iyul",
  "август": "avgust",
  "сентябрь": "sentabr",
  "октябрь": "oktabr",
  "ноябрь": "noyabr",
  "декабрь": "dekabr",
  "Январь": "Yanvar",
  "Февраль": "Fevral",
  "Март": "Mart",
  "Апрель": "Aprel",
  "Май": "May",
  "Июнь": "Iyun",
  "Июль": "Iyul",
  "Август": "Avgust",
  "Сентябрь": "Sentabr",
  "Октябрь": "Oktabr",
  "Ноябрь": "Noyabr",
  "Декабрь": "Dekabr",
  "января": "yanvar",
  "февраля": "fevral",
  "марта": "mart",
  "апреля": "aprel",
  "мая": "may",
  "июня": "iyun",
  "июля": "iyul",
  "августа": "avgust",
  "сентября": "sentabr",
  "октября": "oktabr",
  "ноября": "noyabr",
  "декабря": "dekabr",
  "Янв": "Yan",
  "Фев": "Fev",
  "Мар": "Mar",
  "Апр": "Apr",
  "Июн": "Iyn",
  "Июл": "Iyl",
  "Авг": "Avg",
  "Сен": "Sen",
  "Окт": "Okt",
  "Ноя": "Noy",
  "Дек": "Dek",
  "заказ": "buyurtma",
  "заказа": "buyurtma",
  "заказов": "buyurtma",

  // Ошибка раздела
  "Не удалось загрузить аналитику продаж": "Savdo tahlilini yuklab bo'lmadi",
  "Сервер OneBase не ответил или вернул ошибку. Данные в системе при этом сохранены":
    "OneBase serveri javob bermadi yoki xato qaytardi. Tizimdagi ma'lumotlar saqlangan",
  " — последние удачные данные Linko по {date}": " — Linko'ning {date} holatidagi oxirgi muvaffaqiyatli ma'lumotlari",
  "Последняя синхронизация с Linko завершилась с ошибкой — подробности в «Настройки → Интеграции → Продажи → Linko».":
    "Linko bilan oxirgi sinxronlash xato bilan tugadi — tafsilotlar «Sozlamalar → Integratsiyalar → Savdo → Linko» bo'limida.",
  "Код ошибки: {code}": "Xato kodi: {code}",
  "Попробовать снова": "Qayta urinish",

  // Вторичка, Республика, настройки, P&L
  "Продажи · OneBase": "Savdo · OneBase",
  "План, факт и работа торговых представителей": "Reja, fakt va savdo vakillari ishi",
  "Республика · Продажи": "Respublika · Savdo",
  "республике": "respublika",
  "направлении": "yo'nalish",
  "регионе": "mintaqa",
  "экспорте": "eksport",
  "продажах этого ТП": "shu TP savdosi",
  "Настройки продаж · OneBase": "Savdo sozlamalari · OneBase",
  "Настройки продаж": "Savdo sozlamalari",
  "Синхронизация, оргструктура (РМ, каналы, СВР и дилеры регионов), справочник ТП и цели":
    "Sinxronlash, tashkiliy tuzilma (RM, kanallar, SVR va mintaqa dilerlari), TP ma'lumotnomasi va maqsadlar",
  "P&L дилеров · Продажи": "Dilerlar P&L · Savdo",
  "Прибыль завода на дилере за месяц, сум": "Zavodning diler bo'yicha oylik foydasi, so'm",

  // Первичка
  "Первичка · Продажи": "Birlamchi · Savdo",
  "Первичка · Экспорт · Продажи": "Birlamchi · Eksport · Savdo",
  "Первичка · Республика · Продажи": "Birlamchi · Respublika · Savdo",
  "Доля всей отгрузки завода": "Zavodning umumiy jo'natmasidagi ulush",
  "{pct} всей отгрузки завода": "zavodning umumiy jo'natmasidan {pct}",
  "Завод — дилерам и прямым клиентам · {year} год с начала года · выберите, что смотреть":
    "Zavod — dilerlar va to'g'ridan-to'g'ri mijozlarga · {year}-yil, yil boshidan · nimani ko'rishni tanlang",
  "завод → дилеры и прямые клиенты · {n} контрагентов": "zavod → dilerlar va to'g'ridan-to'g'ri mijozlar · {n} ta kontragent",
  "завод → экспорт · {n} стран · {orders}": "zavod → eksport · {n} ta davlat · {orders}",
  "Республика — перемещения Linko со склада завода на склады дилеров и заказы прямых клиентов завода (базары, сети, фирменный магазин), нетто возвратов. Экспорт — заказы филиала «Завод» торговым точкам с типом EXPORT; страна — по названию или адресу точки в Linko.":
    "Respublika — Linko'da zavod omboridan dilerlar omborlariga ko'chirishlar va zavodning to'g'ridan-to'g'ri mijozlari (bozorlar, tarmoqlar, firma do'koni) buyurtmalari, qaytarishlar ayirilgan. Eksport — «Zavod» filialining EXPORT turidagi savdo nuqtalariga buyurtmalari; davlat — Linko'dagi nuqta nomi yoki manzili bo'yicha.",
  "Завод — на экспорт": "Zavod — eksportga",
  "Завод — дилерам и прямым клиентам": "Zavod — dilerlar va to'g'ridan-to'g'ri mijozlarga",
  "данные по {date}": "{date} holatiga ma'lumot",
  "месяц закрыт": "oy yopilgan",
  "отгрузок нет": "jo'natma yo'q",

  // Экспорт и опт
  "Экспорт и опт · Продажи": "Eksport va ulgurji · Savdo",
  "Экспорт и опт": "Eksport va ulgurji",
  "Филиал «Завод» в Linko — экспорт и крупный опт. Во вторичку не входит, иначе завысил бы факт республики.":
    "Linko'dagi «Zavod» filiali — eksport va yirik ulgurji. Ikkilamchiga kirmaydi, aks holda respublika faktini oshirib yuborardi.",
  "За месяц продаж филиала «Завод» в Linko нет.": "Bu oyda Linko'da «Zavod» filiali savdosi yo'q.",
  "прогноз {kg} кг": "prognoz {kg} kg",
  "точек с отгрузкой": "jo'natmali nuqtalar",
  "К прошлому месяцу": "O'tgan oyga nisbatan",
  "Прогноз месяца к прошлому месяцу — только у идущего месяца, как у карточек категорий":
    "Oy prognozi o'tgan oyga nisbatan — faqat joriy oyda, toifa kartalaridagidek",
  "Прогноз месяца к факту прошлого месяца": "Oy prognozi o'tgan oy faktiga nisbatan",
  "прошлый месяц {kg} кг": "o'tgan oy {kg} kg",
  "Кроме сумов:": "So'mdan tashqari:",
  "Курса в данных нет — эти суммы в выручку не сложены, вес заказов учтён.":
    "Ma'lumotlarda kurs yo'q — bu summalar tushumga qo'shilmagan, buyurtmalar vazni hisobga olingan.",
  "Разбивки по странам в Linko External API нет: в артефакте «Полевого контроля» она шла из отдельных файлов по странам. Здесь — то, что Linko отдаёт по филиалу «Завод»: покупатели, агенты и товары. Суммы — в валюте заказов, без пересчёта.":
    "Linko External API'da davlatlar bo'yicha bo'linma yo'q: «Dala nazorati» artefaktida u davlatlar bo'yicha alohida fayllardan olingan. Bu yerda — Linko «Zavod» filiali bo'yicha beradigan ma'lumot: xaridorlar, agentlar va tovarlar. Summalar — buyurtmalar valyutasida, qayta hisoblanmagan.",

  // Планы
  "Планы · Продажи": "Rejalar · Savdo",
  "Планы": "Rejalar",
  "Все планы из Linko за {month} — обновляются автоматически после каждого пересчёта":
    "Linko'dagi {month} uchun barcha rejalar — har bir qayta hisobdan keyin avtomatik yangilanadi",
  "факт по Linko {kg} кг": "Linko bo'yicha fakt {kg} kg",
  " · {pct} плана": " · reja bajarilishi {pct}",
  "План по выручке": "Tushum bo'yicha reja",
  "факт по Linko {sum}": "Linko bo'yicha fakt {sum}",
  "денежных планов нет": "pul rejalari yo'q",
  "ТП с планом": "Rejali TP",
  "показателей всего {n}": "jami ko'rsatkichlar: {n}",
  "Планы супервайзеров": "Supervayzerlar rejalari",
  "планы команд — в суммы не входят": "jamoa rejalari — yig'indiga kirmaydi",
  "Планы торговых представителей": "Savdo vakillari rejalari",
  "нажмите на строку — все показатели агента": "qatorni bosing — agentning barcha ko'rsatkichlari",
  "План ТП в кг — сумма весовых показателей Linko; план по выручке — показатели «сумма продаж»; АКБ — план активных клиентов. Факт и выполнение здесь — по расчёту Linko. Во «Вторичке» план региона и республики — план РОП или «Завод»; эти планы там — план ТП, а для месяца без планов регионов — и план подразделений.":
    "kg dagi TP rejasi — Linko vazn ko'rsatkichlari yig'indisi; tushum bo'yicha reja — «savdo summasi» ko'rsatkichlari; AKB — faol mijozlar rejasi. Bu yerda fakt va bajarilish — Linko hisobi bo'yicha. «Ikkilamchi»da mintaqa va respublika rejasi — ROP yoki «Zavod» rejasi; bu rejalar u yerda — TP rejasi, mintaqa rejalari bo'lmagan oyda esa — bo'linmalar rejasi ham.",
  "планы их команд": "ularning jamoalari rejalari",
  "Эти планы не складываются с планами агентов, иначе план задвоился бы. Список должностей, которые считаются планами команд, — в настройке сервера Sales:StaffPlanExcludeJobs.":
    "Bu rejalar agentlar rejalari bilan qo'shilmaydi, aks holda reja ikki baravar bo'lib qolardi. Jamoa rejasi hisoblanadigan lavozimlar ro'yxati — Sales:StaffPlanExcludeJobs server sozlamasida.",

  // Проблемные агенты
  "Проблемные агенты · Продажи": "Muammoli agentlar · Savdo",
  "Кого проверить в первую очередь — по тяжести замечаний": "Kimni birinchi navbatda tekshirish kerak — izohlar og'irligi bo'yicha",
  "факта": "fakt",
  "не привязаны к агентам — в заказах Linko нет агента. Рейтинг считается по остальным":
    "agentlarga bog'lanmagan — Linko buyurtmalarida agent yo'q. Reyting qolgan miqdor bo'yicha hisoblanadi:",
  "Без региона": "Mintaqasiz",
  "Место в списке по тяжести замечаний ({score})": "Izohlar og'irligi bo'yicha ro'yxatdagi o'rni ({score})",
  "Агентов с замечаниями не найдено": "Izohli agentlar topilmadi",
  "Агент оценивается, если у него за месяц не меньше 20 визитов, иначе — метка «мало данных». Конверсия — заказы, принятые в месяце ÷ выполненные визиты. Сравнение — с медианой своего региона по ТП с 20+ визитами, без вакансий и без агентов, у которых заказов больше, чем визитов (у них — риск «данные не сходятся», низкая конверсия не оценивается). Порядок — по тяжести замечаний, как в «Полевом контроле»: 10 за критичное, 4 за риск, плюс (1 − конверсия) × 3 у оцениваемых; номер в углу — место в этом списке. Пороги критериев задаются в настройках сервера (Sales:Flags).":
    "Agent oyiga kamida 20 ta tashrif bo'lsa baholanadi, aks holda — «ma'lumot kam» belgisi. Konversiya — oyda qabul qilingan buyurtmalar ÷ bajarilgan tashriflar. Solishtirish — o'z mintaqasidagi 20+ tashrifli TP lar medianasi bilan, bo'sh o'rinlarsiz va buyurtmalari tashriflaridan ko'p agentlarsiz (ularda — «ma'lumotlar mos kelmaydi» xavfi, past konversiya baholanmaydi). Tartib — izohlar og'irligi bo'yicha, «Dala nazorati»dagidek: kritik uchun 10, xavf uchun 4, baholanadiganlarda qo'shimcha (1 − konversiya) × 3; burchakdagi raqam — shu ro'yxatdagi o'rin. Mezonlar chegaralari server sozlamalarida beriladi (Sales:Flags).",

  // Ассортимент, категория, артикул
  "Ассортимент · Продажи": "Assortiment · Savdo",
  "Вся республика": "Butun respublika",
  "{name} — целиком": "{name} — to'liq",
  "Что и где продаётся: категории, артикулы, дистрибуция": "Nima va qayerda sotilmoqda: toifalar, artikullar, distributsiya",
  "прошлый месяц целиком: {kg} кг": "o'tgan oy to'liq: {kg} kg",
  "в прошлом месяце продаж не было": "o'tgan oyda savdo bo'lmagan",
  "Из SKU, которые продавались во вторичке с 1 января по конец месяца; «продаётся» — есть точка с положительной строкой":
    "1-yanvardan oy oxirigacha ikkilamchida sotilgan SKU lardan; «sotilmoqda» — qatori musbat nuqta bor",
  "пропало {n}": "yo'qoldi: {n}",
  "«Только он» — точки, которые из всех SKU купили ровно один": "«Yolg'iz» — barcha SKU dan aynan bittasini sotib olgan nuqtalar",
  "точек с покупкой за месяц · только один SKU — {n}": "oy davomida xarid qilgan nuqtalar · faqat bitta SKU — {n}",
  "Регион подробнее — на его странице во «Вторичке»:": "Mintaqa haqida batafsil — uning «Ikkilamchi»dagi sahifasida:",
  "все регионы": "barcha mintaqalar",
  "Категория · Продажи": "Toifa · Savdo",
  "дистрибуция {pct}": "distributsiya {pct}",
  "В охвате «{scope}» продаж категории «{name}» ни в этом, ни в прошлом месяце нет.":
    "«{scope}» qamrovida «{name}» toifasi savdosi bu oyda ham, o'tgan oyda ham yo'q.",
  "Артикул · Продажи": "Artikul · Savdo",
  "Статус в охвате:": "Qamrovdagi holat:",
  "Товар из списка ТОП": "TOP ro'yxatidagi tovar",
  "Точки с положительной строкой товара: кг или сумма больше нуля": "Tovar qatori musbat nuqtalar: kg yoki summa noldan katta",
  "{pct} из {n} ТТ с покупкой": "xarid qilgan {n} nuqtadan {pct}",
  "Точки, которые из всех SKU купили только этот; всего таких точек в охвате — {n}":
    "Barcha SKU dan faqat shuni sotib olgan nuqtalar; qamrovda bunday nuqtalar jami — {n}",
  "{pct} его ТТ · всего таких точек {n}": "uning nuqtalaridan {pct} · bunday nuqtalar jami {n}",
  "всего таких точек {n}": "bunday nuqtalar jami {n}",
  "выручка ÷ факт, кг": "tushum ÷ fakt, kg",

  // Магазин, регион, ТП
  "ТП: {name}": "TP: {name}",
  "разных артикулов в заказах": "buyurtmalardagi turli artikullar",
  "Прошлый месяц": "O'tgan oy",
  "прошлый месяц: {kg} кг": "o'tgan oy: {kg} kg",
  "В этом месяце точка ничего не купила": "Bu oyda nuqta hech narsa sotib olmadi",
  "СВР: {name}": "SVR: {name}",
  "дилер: {name}": "diler: {name}",
  "СВР и дилер не указаны — их можно задать в настройках продаж": "SVR va diler ko'rsatilmagan — ularni savdo sozlamalarida kiritish mumkin",
  "{month}, 1–{day} числа": "{month}, 1–{day}-kunlar",
  "база — {month}, кто пока молчит": "baza — {month}, hozircha jim turganlar",
  "Замечаний нет: показатели в пределах нормы региона.": "Izohlar yo'q: ko'rsatkichlar mintaqa me'yori doirasida.",
  "в кг плана нет; в Linko — план по выручке {sum} сум": "kg da reja yo'q; Linko'da — tushum bo'yicha reja {sum} so'm",
  "в кг плана нет; в Linko план в штуках — см. «Планы Linko» ниже": "kg da reja yo'q; Linko'da reja donada — quyidagi «Linko rejalari»ga qarang",
  "в кг плана нет; в Linko только другие показатели — см. «Планы Linko» ниже":
    "kg da reja yo'q; Linko'da faqat boshqa ko'rsatkichlar — quyidagi «Linko rejalari»ga qarang",
  "в Linko плана на {month} нет": "Linko'da {month} uchun reja yo'q",
  "выполнено {pct}": "bajarildi: {pct}",
  "{pct} плана ({sum})": "reja bajarilishi {pct} ({sum})",
  "{n} визитов, в день которых в этой точке введён заказ": "{n} ta tashrif — o'sha kuni shu nuqtada buyurtma kiritilgan",
  "{orders} за месяц": "oy davomida {orders}",
  "Заказы, принятые в месяце ÷ выполненные визиты; бывает больше 100%": "Oyda qabul qilingan buyurtmalar ÷ bajarilgan tashriflar; 100% dan oshishi mumkin",
  "медиана региона {value}": "mintaqa medianasi {value}",
  "{sum} сум с точки": "nuqtadan {sum} so'm",
  "покупок за месяц нет": "oy davomida xarid yo'q",
  "из {n} в плане": "rejadagi {n} tadan",
  "цель — {n} на активную точку": "maqsad — faol nuqtaga {n}",
  "с поправкой на {worked} из {days} дней": "{days} kundan {worked} kunga tuzatilgan",

  // Аутсток
  "Аутсток · Продажи": "Autstok · Savdo",
  "ТОП-товары": "TOP tovarlar",
  "товары вне ТОПа": "TOPdan tashqari tovarlar",
  "все SKU": "barcha SKU",
  "Дни без товара у дилеров и упущенные продажи за последний закрытый месяц":
    "Dilerlarda tovarsiz kunlar va oxirgi yopilgan oydagi boy berilgan savdo",
  "Коды ТОП-товаров Linko задаются в настройке Sales:TopProducts; тогда появятся «Только ТОП / Все SKU / Кроме ТОПа»":
    "Linko TOP tovarlari kodlari Sales:TopProducts sozlamasida beriladi; shunda «Faqat TOP / Barcha SKU / TOPdan tashqari» paydo bo'ladi",
  "Снимок остатков Linko сделан {date} — раньше начала {month}. Восстановить остаток на эти дни не из чего: нажмите «Обновить», когда появится новый снимок.":
    "Linko qoldiqlari kesimi {date} da olingan — {month} boshlanishidan oldin. Bu kunlar uchun qoldiqni tiklashga asos yo'q: yangi kesim paydo bo'lganda «Yangilash» tugmasini bosing.",
  "{pct} всех потерь": "barcha yo'qotishlarning {pct}",
  "Дней в нуле × средние продажи в день, по всем парам «товар × регион» области":
    "Nolda turgan kunlar × kunlik o'rtacha savdo, qamrovdagi barcha «tovar × mintaqa» juftlari bo'yicha",
  "{pct} к факту продаж": "savdo faktiga nisbatan {pct}",
  "Упущенные кг × средняя цена товара у дилера за период": "Yo'qotilgan kg × davr uchun dilerdagi tovarning o'rtacha narxi",
  "Пары «товар × регион», которые хотя бы один день стояли в нуле": "Kamida bir kun nolda turgan «tovar × mintaqa» juftlari",
  "{products} {productWord} в {regions} {regionWord}": "{regions} ta mintaqada {products} ta tovar",
  "Сумма дней без товара по всем парам «товар × регион»; чья потеря — по складу завода в тот же день":
    "Barcha «tovar × mintaqa» juftlari bo'yicha tovarsiz kunlar yig'indisi; kimning yo'qotishi — o'sha kungi zavod ombori bo'yicha",
  "недовоз {dealer} · завод {factory}": "yetkazilmagan {dealer} · zavod {factory}",
  " · нет данных {n}": " · ma'lumot yo'q {n}",
  "Период {from} – {to} ({days} дн.), снимок остатков Linko на {syncedAt}. Пар «товар × регион» с продажами: {pairs}.":
    "Davr {from} – {to} ({days} kun), Linko qoldiqlari kesimi {syncedAt} holatiga. Savdosi bor «tovar × mintaqa» juftlari: {pairs}.",
  "Клеток, ушедших в минус при расчёте назад (показаны нулём): {n}%. ": "Orqaga hisoblashda minusga tushgan kataklar (nol bilan ko'rsatilgan): {n}%. ",
  "Это оценка, а не учёт — реальные потери, скорее, немного больше.": "Bu aniq hisob emas, baho — haqiqiy yo'qotishlar, ehtimol, biroz ko'proq.",
  "Хронические дыры": "Surunkali bo'shliqlar",
  "Все пары с потерями": "Yo'qotishli barcha juftlar",
  "товар × регион, хотя бы один день в нуле": "tovar × mintaqa, kamida bir kun nolda",

  // Продажи по SKU
  "Продажи по SKU · Продажи": "SKU bo'yicha sotuv · Savdo",
  "Вторичка по SKU за выбранный период": "Tanlangan davr uchun SKU bo'yicha ikkilamchi",
  "Сбросить период": "Davrni tozalash",
  "вся страна": "butun mamlakat",
  "Вторичка по SKU: кг, сумма, АКБ, ABC": "SKU bo'yicha ikkilamchi: kg, summa, AKB, ABC",
  "ТОП — коды товаров из настройки Sales:TopProducts": "TOP — Sales:TopProducts sozlamasidagi tovar kodlari",
  "ТОП-товары не настроены (Sales:TopProducts)": "TOP tovarlar sozlanmagan (Sales:TopProducts)",
  "ABC считается внутри выборки: регион, ТОП, категории": "ABC tanlov ichida hisoblanadi: mintaqa, TOP, toifalar",
  "категории: {list}": "toifalar: {list}",
  "клик по карточке категории — фильтр (можно несколько); карточки учитывают регион, ТОП и ABC":
    "toifa kartochkasini bosing — filtr (bir nechtasini tanlash mumkin); kartochkalar mintaqa, TOP va ABC ni hisobga oladi",
  "Сбросить фильтры": "Filtrlarni tozalash",
  "{n} сум за кг": "kg uchun {n} so'm",
  "группа A — {n} SKU (80% веса)": "A guruhi — {n} SKU (vaznning 80%)",
  " · регионов {n}": " · mintaqalar: {n}",
  "выбранные категории — {pct} веса карточек": "tanlangan toifalar — kartochkalar vaznining {pct}",
  "Нетто: заказы по дате приёмки минус возвраты по дате возврата, без «Завода» и «К К Мерч»; только категории отчёта":
    "Netto: qabul sanasi bo'yicha buyurtmalar minus qaytarish sanasi bo'yicha qaytarishlar, «Zavod» va «К К Мерч»siz; faqat hisobot toifalari",
  " (ещё {n} {products} других типов Linko — бонус, подарки — не {verb})": " (Linko'ning boshqa turlaridagi yana {n} ta tovar — bonus, sovg'alar — kirmaydi)",
  " Последний месяц не закрыт — данные по {date}.": " Oxirgi oy yopilmagan — ma'lumotlar {date} holatiga.",

  // Рекомендуемый остаток: страница
  "Рекомендуемый остаток · Продажи": "Tavsiya etilgan qoldiq · Savdo",
  "штуки": "dona",
  "Список": "Ro'yxat",
  "вход дилеру": "dilerga kirim",
  "Запас дилеров по складам: хватит ли до поставки, что заказать и сколько он стоит":
    "Omborlar bo'yicha dilerlar zaxirasi: yetkazib berishgacha yetadimi, nimani buyurtma qilish va u qancha turadi",
  "Единица: кг, коробки (где вес коробки известен), деньги по входу дилера, штуки как в Linko":
    "Birlik: kg, quti (quti vazni ma'lum bo'lsa), diler kirim narxidagi pul, Linko'dagidek dona",
  "все категории": "barcha toifalar",
  "Остатки ещё не загружены из Linko — они появятся после ближайшей синхронизации (или нажмите «Обновить»).":
    "Qoldiqlar hali Linko'dan yuklanmagan — ular navbatdagi sinxronlashdan keyin paydo bo'ladi (yoki «Yangilash» tugmasini bosing).",
  "Штуки Linko × вес единицы; коробки — где вес коробки известен": "Linko donalari × birlik vazni; qutilar — quti vazni ma'lum bo'lsa",
  "{boxes} коробок · {skus} SKU": "{boxes} quti · {skus} SKU",
  "Штуки × цена за единицу учёта из прайса «{priceList}», действовавшая на {date}":
    "Dona × «{priceList}» praysidagi hisob birligi narxi, {date} holatiga amalda bo'lgan",
  "по входу дилера на {date}": "{date} holatiga diler kirim narxida",
  " · без цены {n} SKU": " · narxsiz {n} SKU",
  "Скорость завода — продажи всей страны": "Zavod tezligi — butun mamlakat savdosi",
  " · бронь {kg} кг": " · bron {kg} kg",
  "Бронь (неотгруженные заказы и перемещения, перемещения в пути) — на {time}.":
    "Bron (jo'natilmagan buyurtmalar va ko'chirishlar, yo'ldagi ko'chirishlar) — {time} holatiga.",
  "Доступный остаток (остаток − бронь) ÷ продажи в день с поправкой на аутсток": "Mavjud qoldiq (qoldiq − bron) ÷ autstokka tuzatilgan kunlik savdo",
  "у экспорта своей скорости продаж нет": "eksportning o'z savdo tezligi yo'q",
  "{kg} кг в день": "kuniga {kg} kg",
  " (без поправки на аутсток {kg})": " (autstok tuzatishisiz {kg})",
  "Хватит меньше чем на 15 дней продаж": "15 kunlik savdodan kamga yetadi",
  "затоварка — {overstock}, не продаётся — {dead}": "ortiqcha — {overstock}, sotilmaydi — {dead}",
  "Сумма рекомендуемых заказов всех дилеров минус остаток на заводе": "Barcha dilerlar tavsiya buyurtmalari yig'indisi minus zavoddagi qoldiq",
  "Скорость с поправкой на аутсток × 15 дней − доступный остаток (остаток − бронь), вверх до целой коробки; 0 — хватает":
    "Autstokka tuzatilgan tezlik × 15 kun − mavjud qoldiq (qoldiq − bron), butun qutigacha yuqoriga yaxlitlangan; 0 — yetadi",
  "у экспорта заказа нет": "eksportda buyurtma yo'q",
  "{sum} сум · {skus} SKU": "{sum} so'm · {skus} SKU",
  " · до запаса на 15 дней": " · 15 kunlik zaxiragacha",
  "{sum} сум по входной цене": "kirim narxida {sum} so'm",
  "не хватает {kg} кг ({skus} SKU)": "{kg} kg yetishmaydi ({skus} SKU)",
  " · хватит на {days} дн. по скорости всей страны": " · butun mamlakat tezligida {days} kunga yetadi",
  " · в итог страны не входит": " · mamlakat jamiga kirmaydi",
  "склад завода не найден": "zavod ombori topilmadi",
  "Остатки на {syncedAt} (снимок Linko {snapshot}). Скорость — вторичка нетто возвратов за {days} дн. ({from} – {to}) с поправкой на дни в нуле за {month}: по стране {raw} → {corrected} кг в день ({change}, пар с поправкой — {pairs}). Вес единицы — по строкам заказов с {unitFrom}; цена — прайс «{priceList}» на {priceAsOf}.":
    "Qoldiqlar {syncedAt} holatiga (Linko kesimi {snapshot}). Tezlik — {days} kunlik ikkilamchi, qaytarishlar ayirilgan ({from} – {to}), {month} oyida nolda turgan kunlarga tuzatilgan: mamlakat bo'yicha kuniga {raw} → {corrected} kg ({change}, tuzatilgan juftlar — {pairs}). Birlik vazni — {unitFrom} dan buyurtma qatorlari bo'yicha; narx — «{priceList}» praysi, {priceAsOf} holatiga.",
  "У {n} SKU продаж за год не было — вес из названия, значения помечены «≈». ":
    "{n} ta SKU bir yil davomida sotilmagan — vazn nomidan olingan, qiymatlar «≈» bilan belgilangan. ",
  "{n} SKU нет в прайсе — в стоимость не вошли. ": "{n} ta SKU praysda yo'q — qiymatga kirmagan. ",
  "Вне таблицы: {outside} SKU вне категорий отчёта, {noWeight} без веса единицы.":
    "Jadvaldan tashqari: hisobot toifalaridan tashqari {outside} SKU, birlik vaznisiz {noWeight}.",
  "Клик — только эта категория; повторный клик снимает выбор": "Bosing — faqat shu toifa; qayta bosish tanlovni bekor qiladi",
  "Рек. заказ": "Tavsiya buyurtma",
  "По этим фильтрам товаров нет.": "Bu filtrlar bo'yicha tovar yo'q.",
  "все данные — из Linko; правила — DOC §9.4": "barcha ma'lumotlar — Linko'dan; qoidalar — DOC §9.4",
  "— снимок Linko на дату последней загрузки, а не итог месяца, поэтому переключателя периода нет. «Вся страна» — только склады дилеров (по одному на регион справочника); склады старых филиалов, «Основной», интеграционные — в «Прочих складах»; завод и экспорт — отдельные охваты и в итог страны не входят.":
    "— oxirgi yuklash sanasidagi Linko kesimi, oy yakuni emas, shuning uchun davr almashtirgichi yo'q. «Butun mamlakat» — faqat diler omborlari (ma'lumotnomadagi har bir mintaqaga bittadan); eski filiallar omborlari, «Основной», integratsiya omborlari — «Boshqa omborlar»da; zavod va eksport — alohida qamrovlar va mamlakat jamiga kirmaydi.",
  "Продажи в день": "Kunlik savdo",
  "— вторичка нетто возвратов за {days} дн. до последнего полного дня в регионе склада, делённая на ({days} − дни в нуле этого товара в регионе за последний закрытый месяц, не больше 30): дни, когда товара не было, продаж не занижают. «Хватит» — остаток ÷ скорость; дефицит — меньше 15 дней, норма 15–30, затоварка — больше 30, «не продаётся» — остаток лежит, а продаж за базу не было.":
    "— ombor mintaqasida oxirgi to'liq kungacha {days} kunlik ikkilamchi (qaytarishlar ayirilgan), ({days} − oxirgi yopilgan oyda shu tovarning mintaqada nolda turgan kunlari, 30 dan ko'p emas) ga bo'linadi: tovar bo'lmagan kunlar savdoni kamaytirmaydi. «Yetadi» — qoldiq ÷ tezlik; taqchil — 15 kundan kam, me'yor 15–30, ortiqcha — 30 dan ko'p, «sotilmaydi» — qoldiq bor, lekin baza davrida savdo bo'lmagan.",
  "Рек. заказ дилера": "Dilerning tavsiya buyurtmasi",
  "= скорость с поправкой × 15 − остаток, вверх до целой коробки (0 — хватает); деньги — по входной цене.":
    "= tuzatilgan tezlik × 15 − qoldiq, butun qutigacha yuqoriga (0 — yetadi); pul — kirim narxida.",
  "Заказ у завода": "Zavoddagi buyurtma",
  "= сумма заказов всех дилеров (при любом охвате) − остаток завода: сколько не хватает, чтобы отгрузить всем. Своего запаса «на 15 дней» у завода и экспорта нет.":
    "= barcha dilerlar buyurtmalari yig'indisi (har qanday qamrovda) − zavod qoldig'i: hammaga jo'natish uchun qancha yetishmaydi. Zavod va eksportda o'z «15 kunlik» zaxirasi yo'q.",
  "Единица учёта, коробка и цена.": "Hisob birligi, quti va narx.",
  "Остаток и цена в Linko — за единицу учёта (кг у весового, штука или шоубокс у штучного). Кг = штуки × вес единицы (Σ веса ÷ Σ количества по строкам заказов с 1-го числа закрытого месяца, без свежих продаж — за год, без продаж — из названия, «≈»). Коробка — из названия (у весового любой вес, у штучного — если в коробке целое число штук), иначе по отгрузкам завода (целыми коробками). Стоимость — штуки × цена прайса «{priceList}», действовавшая на начало текущего месяца: повышение, введённое в этом месяце, остаток не переоценивает.":
    "Linko'da qoldiq va narx — hisob birligi uchun (vaznli tovarda kg, donali tovarda dona yoki shoubox). Kg = dona × birlik vazni (Σ vazn ÷ Σ miqdor, yopilgan oyning 1-sanasidan buyurtma qatorlari bo'yicha; yangi savdo bo'lmasa — bir yillik, savdo bo'lmasa — nomidan, «≈»). Quti — nomidan (vaznli tovarda istalgan vazn, donali tovarda — qutida butun son dona bo'lsa), aks holda zavod jo'natmalari bo'yicha (butun qutilarda). Qiymat — dona × joriy oy boshida amalda bo'lgan «{priceList}» praysi narxi: shu oyda kiritilgan narx oshishi qoldiqni qayta baholamaydi.",
  "Умножать штуки на вес коробки нельзя — это завышает остаток во столько раз, сколько штук в коробке. Фильтры применяет сервер: плитки, итоги и карточки категорий — по отфильтрованным строкам.":
    "Donani quti vazniga ko'paytirib bo'lmaydi — bu qoldiqni qutidagi dona soni marta oshirib yuboradi. Filtrlarni server qo'llaydi: plitkalar, jamilar va toifa kartochkalari — filtrlangan qatorlar bo'yicha.",

  // Рекомендуемый остаток: фильтры и таблицы
  "Поиск по товарам": "Tovarlar bo'yicha qidiruv",
  "Охват": "Qamrov",
  "Вся страна (склады дилеров)": "Butun mamlakat (diler omborlari)",
  "Отдельные склады": "Alohida omborlar",
  "Фасовка:": "Qadoq:",
  "все веса": "barcha og'irliklar",
  "дефицит": "taqchil",
  "затоварка": "ortiqcha",
  "не продаётся": "sotilmaydi",
  "норма": "me'yor",
  "нет веса": "vazn yo'q",
  "нет остатка": "qoldiq yo'q",
  // Аутсток: выбор месяца
  "{month} — идёт, по {date}": "{month} — davom etmoqda, {date} gacha",
  "Дни без товара у дилеров и упущенные продажи: месяц идёт, 1–{day} число": "Dilerlarda tovarsiz kunlar va boy berilgan savdo: oy davom etmoqda, 1–{day}-kunlar",
  "Дни без товара у дилеров и упущенные продажи за месяц": "Dilerlarda tovarsiz kunlar va oy davomida boy berilgan savdo",
  "Остаток за {month} восстановлен назад от снимка Linko {date}: чем старше месяц, тем длиннее путь назад и ниже точность — смотрите долю клеток, ушедших в минус, под плитками.": "{month} uchun qoldiq Linko'ning {date} dagi kesimidan orqaga tiklangan: oy qanchalik eski bo'lsa, orqaga yo'l shunchalik uzun va aniqlik past — plitkalar ostidagi minusga ketgan kataklar ulushiga qarang.",
  // Автозаказ на «Рек. остатке»
  "Автозаказ": "Avtobuyurtma",
  "Заказ на склад региона в коробках: рекомендация, ваша правка и акт приёма-передачи в Excel":
    "Hudud omboriga qutilarda buyurtma: tavsiya, sizning tuzatishingiz va Excel'da qabul qilish-topshirish dalolatnomasi",
  "Склад региона": "Hudud ombori",
  "Дата акта": "Dalolatnoma sanasi",
  "Рекомендованные": "Tavsiya etilganlar",
  "Все товары": "Barcha tovarlar",
  "Изменённые": "O'zgartirilganlar",
  "Название, код или категория": "Nomi, kodi yoki toifasi",
  "Вес": "Vazn",
  "Изменено вручную: {n}": "Qo'lda o'zgartirilgan: {n}",
  "Вернуть рекомендацию": "Tavsiyaga qaytarish",
  "Рекомендация": "Tavsiya",
  "Коробки": "Qutilar",
  "Количество": "Miqdori",
  "Цена, сум": "Narxi, so'm",
  "Ед. изм.": "O'lch. birligi",
  "Правок пока нет": "Hozircha tahrirlar yo'q",
  "Рекомендованного заказа нет — откройте «Все товары», чтобы добавить вручную": "Tavsiya etilgan buyurtma yo'q — qo'lda qo'shish uchun «Barcha tovarlar»ni oching",
  "{n} {unit} в коробке": "qutida {n} {unit}",
  "кор.": "quti",
  "Остатки завода и склада и рекомендация: кг, коробки (по весу коробки), штуки (единицы Linko) или деньги (по прайсу)": "Zavod va ombor qoldiqlari hamda tavsiya: kg, qutilar (quti vazni bo'yicha), dona (Linko birliklari) yoki pul (prays bo'yicha)",
  "Остаток завода не загружен": "Zavod qoldig'i yuklanmagan",
  "Доступно на складе завода: остаток минус бронь (заказы и перемещения, которые ещё не отгружены, и перемещения в пути к дилерам). В акт не попадает.": "Zavod omborida mavjud: qoldiq minus bron (hali jo'natilmagan buyurtmalar va ko'chirishlar hamda dilerlarga yo'ldagi ko'chirishlar). Dalolatnomaga kirmaydi.",
  "Доступно на складе региона: остаток минус бронь (заказы магазинов, которые ещё не отгружены, и перемещения в пути); от него считается рекомендация": "Mintaqa omborida mavjud: qoldiq minus bron (hali jo'natilmagan do'kon buyurtmalari va yo'ldagi ko'chirishlar); tavsiya shundan hisoblanadi",
  "Завод, доступно, {unit}": "Zavod, mavjud, {unit}",
  "Склад, доступно, {unit}": "Ombor, mavjud, {unit}",
  "Остаток {stock} кг, бронь {reserved} кг": "Qoldiq {stock} kg, bron {reserved} kg",
  "доступно на складе": "omborda mavjud",
  "на заводе": "zavodda",
  "{n} кор.": "{n} quti",
  "рек. {n}": "tavs. {n}",
  "Меньше на коробку": "Bir quti kamroq",
  "Больше на коробку": "Bir quti ko'proq",
  "Коробок: {name}": "Qutilar: {name}",
  "Рекомендация — заказ, который доводит доступный остаток склада (остаток − бронь: заказы магазинов, которые ещё не отгружены, и перемещения в пути) до запаса на {days} дн. продаж, в целых коробках. Цена — прайс «{priceList}».":
    "Tavsiya — omborning mavjud qoldig'ini (qoldiq − bron: hali jo'natilmagan do'kon buyurtmalari va yo'ldagi ko'chirishlar) {days} kunlik savdo zaxirasigacha butun qutilarda yetkazadigan buyurtma. Narx — «{priceList}» prays-varag'i.",
  "Без веса коробки и потому без автозаказа: {n} {products}.": "Quti vazni yo'qligi sababli avtobuyurtmasiz: {n} {products}.",
  "на заводе доступно {n} кг": "zavodda mavjud {n} kg",
  "Остаток завода {stock} кг, из них в брони {reserved} кг": "Zavod qoldig'i {stock} kg, shundan bronda {reserved} kg",
  "Копировать для Excel": "Excel uchun nusxalash",
  "Таблица скопирована — вставьте в Excel": "Jadval nusxalandi — Excel'ga qo'ying",
  "Не удалось скопировать": "Nusxalab bo'lmadi",
  "В заказе нет ни одной коробки": "Buyurtmada birorta ham quti yo'q",
  "Акт приёма-передачи": "Qabul-topshirish dalolatnomasi",
  "Не выбран регион.": "Hudud tanlanmagan.",
  "Регион не найден среди складов дилеров.": "Hudud dilerlar omborlari orasida topilmadi.",
  "В акте нет ни одной коробки.": "Dalolatnomada birorta ham quti yo'q.",
  "Складов дилеров нет — остатки ещё не загружены из Linko.": "Dilerlar omborlari yo'q — qoldiqlar hali Linko'dan yuklanmagan.",
  // «Собрать в Excel» на «Рек. остатке»
  "Собрать в Excel": "Excel'ga yig'ish",
  "Строки — как на странице: {scope}, {n} SKU с текущими фильтрами. Отметьте, какие столбцы собрать в отчёт.": "Qatorlar — sahifadagidek: {scope}, joriy filtrlar bilan {n} SKU. Hisobotga qaysi ustunlarni yig'ishni belgilang.",
  "Выбрать все": "Hammasini tanlash",
  "Как в таблице": "Jadvaldagidek",
  "Добавить лист «По складам»": "«Omborlar bo'yicha» varag'ini qo'shish",
  "Каждый товар по заводу и складам дилеров, где есть остаток или продажи, — с выбранными столбцами.": "Har bir tovar — zavod va qoldig'i yoki savdosi bor diler omborlari bo'yicha, tanlangan ustunlar bilan.",
  "Выбрано столбцов: {n}": "Tanlangan ustunlar: {n}",
  "Готовим файл…": "Fayl tayyorlanmoqda…",
  "Код": "Kod",
  "Фасовка, кг": "Qadoq, kg",
  "Вес штуки, кг": "Dona og'irligi, kg",
  "Вес коробки, кг": "Quti og'irligi, kg",
  "Остаток, кг": "Qoldiq, kg",
  "Остаток, коробки": "Qoldiq, quti",
  "Остаток, штуки": "Qoldiq, dona",
  "Бронь, кг": "Bron, kg",
  "Доступно, кг": "Mavjud, kg",
  "Продажи и запас": "Savdo va zaxira",
  "Продажи в день без поправки на аутсток, кг": "Autstoksiz kunlik savdo, kg",
  "Дней без товара в прошлом месяце (лист «По складам»)": "O'tgan oyda tovarsiz kunlar («Omborlar bo'yicha» varag'i)",
  "Запас на 15 дн., кг": "15 kunlik zaxira, kg",
  "Запас на 30 дн., кг": "30 kunlik zaxira, kg",
  "Цена входа, сум": "Kirim narxi, so'm",
  "Сумма запаса, сум": "Zaxira summasi, so'm",
  "Рек. заказ, кг": "Tavsiya buyurtma, kg",
  "Рек. заказ, коробки": "Tavsiya buyurtma, quti",
  "Рек. заказ, сум": "Tavsiya buyurtma, so'm",
  "С поправкой на аутсток{zero}: без поправки {kg} кг в день": "Autstokka tuzatilgan{zero}: tuzatishsiz kuniga {kg} kg",
  " ({n} дн. в нуле в закрытом месяце)": " (yopilgan oyda {n} kun nolda)",
  "По строкам заказов с 1-го числа закрытого месяца.": "Yopilgan oyning 1-sanasidan buyurtma qatorlari bo'yicha.",
  "Свежих продаж нет — по строкам заказов за год.": "Yangi savdo yo'q — yillik buyurtma qatorlari bo'yicha.",
  "Продаж за год не было — вес из названия (оценка).": "Bir yil davomida savdo bo'lmagan — vazn nomidan (taxminiy).",
  "Веса единицы нет.": "Birlik vazni yo'q.",
  " Коробка: {note}.": " Quti: {note}.",
  "По складам": "Omborlar bo'yicha",
  "Склад": "Ombor",
  "Остаток, {unit}": "Qoldiq, {unit}",
  "В день, {unit}": "Kuniga, {unit}",
  "15 дн., {unit}": "15 kun, {unit}",
  "Заказ, {unit}": "Buyurtma, {unit}",
  "Заказ у завода — заказы всех дилеров минус его остаток; скорость — по всей стране":
    "Zavoddagi buyurtma — barcha dilerlar buyurtmalari minus uning qoldig'i; tezlik — butun mamlakat bo'yicha",
  "(завод)": "(zavod)",
  "Не хватает на заводе, кг": "Zavodda yetishmaydi, kg",
  "код {code}": "kod {code}",
  "Фасовка": "Qadoq",
  "веса в названии нет": "nomida vazn yo'q",
  "Вес штуки": "Dona vazni",
  "15 дн., кг": "15 kun, kg",
  "30 дн., кг": "30 kun, kg",
  "Цена входа": "Kirim narxi",
  "Товара нет в прайсе «{priceList}»": "Tovar «{priceList}» praysida yo'q",
  "За единицу учёта, прайс «{priceList}» на начало месяца": "Hisob birligi uchun, «{priceList}» praysi, oy boshiga",
  "Сумма запаса": "Zaxira summasi",
  "Не хватает на заводе, {unit}": "Zavodda yetishmaydi, {unit}",
  "Рек. заказ, {unit}": "Tavsiya buyurtma, {unit}",
  "клик по строке — разрез по складам": "qatorni bosing — omborlar kesimi",
  "склад завода: скорость — продажи всей страны, заказ — чего не хватает, чтобы отгрузить всем дилерам":
    "zavod ombori: tezlik — butun mamlakat savdosi, buyurtma — barcha dilerlarga jo'natish uchun nima yetishmaydi",
  "склад экспорта: своей скорости продаж нет": "eksport ombori: o'z savdo tezligi yo'q",
  "Кг = штуки × вес штуки (Σ веса ÷ Σ количества по строкам заказов с 1-го числа закрытого месяца, иначе за год, иначе из названия — ≈). Коробки = кг ÷ вес коробки: из названия (у весового товара любой, у штучного — если в коробке целое число штук), иначе по отгрузкам завода. Сумма запаса = штуки × входная цена дилера из прайса «{priceList}» на начало месяца. «В день» — вторичка нетто возвратов за {days} дн. ÷ ({days} − дни в нуле за закрытый месяц, не больше 30) — звёздочка там, где поправка сработала. Бронь (под остатком) — «Забронировано» и «В пути» Linko: заказы и перемещения, которые оформлены, но ещё не отгружены, и перемещения, отданные, но ещё не принятые получателем; «хватит, дн.», статус и рек. заказ считаются от доступного: остаток − бронь. Рек. заказ = скорость × 15 − доступный остаток, вверх до целой коробки; у завода — заказы всех дилеров минус весь его остаток (бронь завода — отгрузки по тем же заказам). Дефицит — меньше 15 дней, норма 15–30, затоварка — больше 30, «не продаётся» — остаток есть, продаж за базу нет.":
    "Kg = dona × dona vazni (Σ vazn ÷ Σ miqdor, yopilgan oyning 1-sanasidan buyurtma qatorlari bo'yicha, aks holda bir yillik, aks holda nomidan — ≈). Qutilar = kg ÷ quti vazni: nomidan (vaznli tovarda istalgan, donali tovarda — qutida butun son dona bo'lsa), aks holda zavod jo'natmalari bo'yicha. Zaxira summasi = dona × «{priceList}» praysidagi dilerning oy boshidagi kirim narxi. «Kuniga» — {days} kunlik ikkilamchi (qaytarishlar ayirilgan) ÷ ({days} − yopilgan oyda nolda turgan kunlar, 30 dan ko'p emas) — tuzatish ishlagan joyda yulduzcha. Bron (qoldiq ostida) — Linko'dagi «Bron qilingan» va «Yo'lda»: rasmiylashtirilgan, lekin hali jo'natilmagan buyurtmalar va ko'chirishlar hamda jo'natilgan, lekin qabul qiluvchi hali qabul qilmagan ko'chirishlar; «yetadi, kun», holat va tavsiya buyurtma mavjud qoldiqdan hisoblanadi: qoldiq − bron. Tavsiya buyurtma = tezlik × 15 − mavjud qoldiq, butun qutigacha yuqoriga; zavodda — barcha dilerlar buyurtmalari minus uning butun qoldig'i (zavod broni — o'sha buyurtmalar bo'yicha jo'natmalar). Taqchil — 15 kundan kam, me'yor 15–30, ortiqcha — 30 dan ko'p, «sotilmaydi» — qoldiq bor, baza davrida savdo yo'q.",
  "в день, {unit}": "kuniga, {unit}",
  "заказ, {unit}": "buyurtma, {unit}",
  "Рекомендуемый заказ: до запаса на 15 дней от доступного остатка (остаток − бронь), вверх до целой коробки; у завода — заказы дилеров минус весь его остаток":
    "Tavsiya buyurtma: mavjud qoldiqdan (qoldiq − bron) 15 kunlik zaxiragacha, butun qutigacha yuqoriga; zavodda — dilerlar buyurtmalari minus uning butun qoldig'i",
  "Заказ у завода: заказы всех дилеров минус остаток на заводе": "Zavoddagi buyurtma: barcha dilerlar buyurtmalari minus zavoddagi qoldiq",
  "на каждый склад: остаток, продажи в день, на сколько хватит, запас на 15 дней и рекомендуемый заказ":
    "har bir ombor bo'yicha: qoldiq, kunlik savdo, necha kunga yetadi, 15 kunlik zaxira va tavsiya buyurtma",
  "Итого · {n} SKU": "Jami · {n} SKU",
  "бронь {n}": "bron {n}",
  "бронь {r} кг · доступно {f} кг": "bron {r} kg · mavjud {f} kg",
  "Клетка — склад: остаток в выбранной единице (под ним — бронь: заказы и перемещения, которые ещё не отгружены, и перемещения в пути), продажи в день (звёздочка — с поправкой на аутсток), на сколько дней хватит доступного (остаток − бронь), запас на 15 дней и рекомендуемый заказ от доступного (пусто — докупать не нужно). Завод в «Всего» не входит: это тот же товар до отгрузки; его заказ — заказы всех дилеров минус весь остаток на заводе. Итоги считает сервер по отфильтрованным строкам. «—» в сортировке всегда внизу.":
    "Katak — ombor: tanlangan birlikdagi qoldiq (ostida — bron: hali jo'natilmagan buyurtmalar va ko'chirishlar hamda yo'ldagi ko'chirishlar), kunlik savdo (yulduzcha — autstokka tuzatilgan), mavjud qoldiq (qoldiq − bron) necha kunga yetadi, 15 kunlik zaxira va mavjud qoldiqdan tavsiya buyurtma (bo'sh — qo'shimcha xarid kerak emas). Zavod «Jami»ga kirmaydi: bu jo'natilgunga qadar o'sha tovar; uning buyurtmasi — barcha dilerlar buyurtmalari minus zavoddagi butun qoldiq. Jamilarni server filtrlangan qatorlar bo'yicha hisoblaydi. Saralashda «—» doim pastda.",
  "вне категорий отчёта": "hisobot toifalaridan tashqari",
  "нет веса единицы": "birlik vazni yo'q",
  "Тип Linko": "Linko turi",
  "Почему не в таблице": "Nega jadvalda yo'q",
  "У дилеров, шт.": "Dilerlarda, dona",
  "На заводе, шт.": "Zavodda, dona",
  "Вне таблицы": "Jadvaldan tashqari",
  "вне категорий отчёта — {outside}, без веса единицы — {noWeight}": "hisobot toifalaridan tashqari — {outside}, birlik vaznisiz — {noWeight}",
  "Все товары с остатком — в таблице": "Qoldig'i bor barcha tovarlar — jadvalda",
  "Бонусные товары и подарочные наборы (типы Linko вне категорий отчёта) в остаток не входят — как в «Полевом контроле». Товар без веса единицы (нет продаж за год и фасовки в названии) в кг не пересчитать, поэтому он тоже вне таблицы и итогов.":
    "Bonus tovarlar va sovg'a to'plamlari (hisobot toifalaridan tashqari Linko turlari) qoldiqqa kirmaydi — «Dala nazorati»dagidek. Birlik vazni yo'q tovarni (bir yil savdo yo'q, nomida qadoq yo'q) kg ga o'tkazib bo'lmaydi, shuning uchun u ham jadval va jamilardan tashqarida.",
  "Чей склад": "Kimning ombori",
  "Штук": "Dona",
  "Прочие склады": "Boshqa omborlar",
  "в «Всю страну» не входят": "«Butun mamlakat»ga kirmaydi",
  "Все склады с остатком — склады дилеров, завод или экспорт": "Qoldig'i bor barcha omborlar — diler omborlari, zavod yoki eksport",
  "Склады старых филиалов («Жиззах (эски)»), интеграционные, «Основной», «Коканд бозор» и прочие без своего региона в справочнике. «Вся страна» — только 19 складов дилеров, как в «Полевом контроле»; «Чей склад» — регион по Sales:StockRegionAliases, если склад к нему относится.":
    "Eski filiallar omborlari («Жиззах (эски)»), integratsiya omborlari, «Основной», «Коканд бозор» va ma'lumotnomada o'z mintaqasi yo'q boshqalar. «Butun mamlakat» — faqat 19 ta diler ombori, «Dala nazorati»dagidek; «Kimning ombori» — Sales:StockRegionAliases bo'yicha mintaqa, agar ombor unga tegishli bo'lsa.",

  // Как считается: шапка и источники данных
  "Как считается · Продажи": "Qanday hisoblanadi · Savdo",
  "Правила расчёта вторички, первички и остатков — и что в данных Linko устроено иначе, чем кажется":
    "Ikkilamchi, birlamchi va qoldiqlarni hisoblash qoidalari — va Linko ma'lumotlarida nima ko'ringanidan boshqacha tuzilgan",
  "Где используются": "Qayerda ishlatiladi",
  "Сотрудники": "Xodimlar",
  "Торговые точки": "Savdo nuqtalari",
  "Товары и типы": "Tovarlar va turlar",
  "Склады и остатки": "Omborlar va qoldiqlar",
  "Перемещения": "Ko'chirishlar",
  "Платежи, прайс-листы, поставщики, валюты, договоры": "To'lovlar, prays-varaqlar, yetkazib beruvchilar, valyutalar, shartnomalar",
  "Планы агентов": "Agentlar rejalari",
  "Планы РОП и «Завод», оргструктура (РМ, СВР, дилеры)": "ROP va «Zavod» rejalari, tashkiliy tuzilma (RM, SVR, dilerlar)",
  "API планов Linko (staff_balance)": "Linko rejalar API (staff_balance)",
  "нет в Linko — данные OneBase": "Linko'da yo'q — OneBase ma'lumotlari",
  "факт, АКБ, календари, ассортимент, «Продажи по SKU», магазины; прямые клиенты и экспорт в «Первичке»":
    "fakt, AKB, kalendarlar, assortiment, «SKU bo'yicha sotuv», do'konlar; «Birlamchi»da to'g'ridan-to'g'ri mijozlar va eksport",
  "вычитаются по строкам документа": "hujjat qatorlari bo'yicha ayiriladi",
  "страйк, визиты без заказа, календарь визитов": "strayk, buyurtmasiz tashriflar, tashriflar kalendari",
  "ТП, должности (доставщики не ТП)": "TP, lavozimlar (yetkazib beruvchilar TP emas)",
  "названия, регион точки": "nomlar, nuqta mintaqasi",
  "категории отчёта": "hisobot toifalari",
  "вкладка «Рек. остаток»": "«Tavsiya zaxira» bo'limi",
  "вкладка «Первичка»: отгрузки дилерам и возвраты": "«Birlamchi» bo'limi: dilerlarga jo'natmalar va qaytarishlar",
  "Бронь складов": "Omborlar broni",
  "«Рек. остаток» и автозаказ: бронь и доступный остаток (остаток − бронь)": "«Tavsiya zaxira» va avtobuyurtma: bron va mavjud qoldiq (qoldiq − bron)",
  "загружаются для сверки, в отчётах пока не используются": "solishtirish uchun yuklanadi, hisobotlarda hozircha ishlatilmaydi",
  "вкладка «Планы», план ТП; план подразделений, если планов регионов на месяц нет":
    "«Rejalar» bo'limi, TP rejasi; oyga mintaqa rejalari bo'lmasa — bo'linmalar rejasi",
  "план регионов и категорий, карточки РМ, дилер в «Первичке» и «Аутстоке»":
    "mintaqalar va toifalar rejasi, RM kartalari, «Birlamchi» va «Autstok»dagi diler",

  // Как считается: разделы
  "Факт продаж (вторичка)": "Savdo fakti (ikkilamchi)",
  "Планы, оргструктура и вакансии": "Rejalar, tashkiliy tuzilma va bo'sh o'rinlar",
  "план РОП и «Завод», РМ, СВР, дилеры — данные OneBase; планы ТП — из Linko":
    "ROP va «Zavod» rejasi, RM, SVR, dilerlar — OneBase ma'lumotlari; TP rejalari — Linko'dan",
  "Визиты и страйк": "Tashriflar va strayk",
  "Календари, «по месяцам» и сбросы": "Kalendarlar, «oylar bo'yicha» va tozalashlar",
  "по документации «Полевого контроля»: §9.1 и фильтры §1, §12": "«Dala nazorati» hujjatlari bo'yicha: §9.1 va filtrlar §1, §12",
  "Ассортимент и «Продажи по SKU»": "Assortiment va «SKU bo'yicha sotuv»",
  "по документации «Полевого контроля»: §9.2, §9.6": "«Dala nazorati» hujjatlari bo'yicha: §9.2, §9.6",
  "Первичка и остатки": "Birlamchi va qoldiqlar",
  "Источники данных": "Ma'lumot manbalari",
  "браузер к Linko не обращается — только сервер OneBase": "brauzer Linko'ga murojaat qilmaydi — faqat OneBase serveri",
  "Что показала сверка с API": "API bilan solishtirish nimani ko'rsatdi",
  "расхождения с прежним описанием интеграции": "integratsiyaning avvalgi tavsifidan farqlar",
  "Сверка с «Полевым контролем»": "«Dala nazorati» bilan solishtirish",
  "по регионам, v174 от 05.10.2026": "mintaqalar bo'yicha, v174, 05.10.2026",
  "Чего в Linko нет": "Linko'da nima yo'q",

  // Как считается: факт продаж
  "Факт = заказы − возвраты.": "Fakt = buyurtmalar − qaytarishlar.",
  "Заказ входит, если статус «доставлен» (delivered) или «отдан» (given), дата приёмки магазином (accepted_time) попадает в месяц и не позже отчётного дня, а филиал — не «Завод» и не «К К Мерч». «Отдан» — магазин товар принял, а статус в Linko не переставили; у отменённых и недоставленных приёмки нет. Вес и сумма заказа — по строкам документа, и в плитках, и в «Плане и факте по месяцам».":
    "Buyurtma kiradi, agar holati «yetkazildi» (delivered) yoki «berildi» (given) bo'lsa, do'kon qabul qilgan sana (accepted_time) shu oyga to'g'ri kelsa va hisobot kunidan kech bo'lmasa, filial esa «Zavod» ham, «К К Мерч» ham bo'lmasa. «Berildi» — do'kon tovarni qabul qilgan, lekin Linko'da holat o'zgartirilmagan; bekor qilingan va yetkazilmaganlarda qabul yo'q. Buyurtma vazni va summasi — hujjat qatorlari bo'yicha, plitkalarda ham, «Oylar bo'yicha reja va fakt»da ham.",
  "Отчётный день": "Hisobot kuni",
  "— сегодня по ташкентскому времени: приёмки идущего дня видны сразу — на время последней синхронизации Linko (каждые 30 минут); приёмки этого месяца с датой в будущем войдут в отчёт в свой день, а пока видны в «Качестве данных» — только у идущего месяца. Неполный сегодняшний день в темпе считается своей долей: сколько продано сегодня к среднему дню (не больше целого дня) — поэтому утром прогноз и «к прошлому месяцу» не проседают; сегодняшние визиты «в ожидании» ещё впереди и в «не посещено» не входят. Чип «данные по ДД.ММ» в шапке — отчётный день идущего месяца; у закрытого месяца его нет.":
    "— Toshkent vaqti bo'yicha bugun: joriy kun qabullari darhol ko'rinadi — Linko'ning oxirgi sinxronizatsiyasi vaqtiga (har 30 daqiqada); shu oyning kelajak sanali qabullari hisobotga o'z kunida kiradi, hozircha esa «Ma'lumotlar sifati»da ko'rinadi — faqat joriy oyda. To'liq bo'lmagan bugungi kun sur'atda o'z ulushi bilan hisoblanadi: bugun o'rtacha kunga nisbatan qancha sotilgani (butun kundan ko'p emas) — shuning uchun ertalab prognoz va «o'tgan oyga» pasaymaydi; bugungi «kutilmoqda» tashriflari hali oldinda va «tashrif qilinmagan»ga kirmaydi. Sarlavhadagi «ma'lumotlar KK.OO gacha» chipi — joriy oyning hisobot kuni; yopilgan oyda u yo'q.",
  "на конец месяца (факт ÷ прошедшие дни × дней в месяце) — только у идущего месяца; «к прошлому месяцу» у категорий и «Экспорта и опта» — это прогноз к прошлому месяцу, поэтому у закрытого месяца его тоже нет.":
    "oy oxiriga (fakt ÷ o'tgan kunlar × oydagi kunlar) — faqat joriy oyda; toifalar va «Eksport va ulgurji»dagi «o'tgan oyga» — bu o'tgan oyga nisbatan prognoz, shuning uchun yopilgan oyda u ham yo'q.",
  "Дата продажи — приёмка (accepted_time), а не плановая доставка.": "Sotuv sanasi — qabul (accepted_time), rejadagi yetkazish emas.",
  "Плановую доставку назначают и на воскресенье, а принимают в понедельник. accepted_time Linko отдаёт по местному времени (Ташкент): у заказов, не менявшихся после приёмки, он ровно на 5 часов больше времени изменения в UTC.":
    "Rejadagi yetkazish yakshanbaga ham belgilanadi, qabul esa dushanbada bo'ladi. Linko accepted_time ni mahalliy vaqtda (Toshkent) beradi: qabuldan keyin o'zgarmagan buyurtmalarda u UTC dagi o'zgartirish vaqtidan roppa-rosa 5 soat ko'p.",
  "— статус «delivered», дата создания возврата в месяце (другой даты у возврата нет), филиал не «Завод». Вес и сумма берутся по строкам документа: у части возвратов вес в шапке нулевой, а товар лежит в строках. Возврат уменьшает кг и сумму того региона и агента, где он оформлен.":
    "— holati «delivered», qaytarish yaratilgan sana shu oyda (qaytarishning boshqa sanasi yo'q), filial «Zavod» emas. Vazn va summa hujjat qatorlaridan olinadi: ba'zi qaytarishlarda sarlavhadagi vazn nol, tovar esa qatorlarda. Qaytarish u rasmiylashtirilgan mintaqa va agentning kg va summasini kamaytiradi.",
  "Филиал «Завод»": "«Zavod» filiali",
  "— экспорт и крупный опт, не регион: во вторичку не входит и показывается отдельной плиткой «Экспорт и опт».":
    "— eksport va yirik ulgurji, mintaqa emas: ikkilamchiga kirmaydi va alohida «Eksport va ulgurji» plitkasida ko'rsatiladi.",
  "Филиал «К К Мерч»": "«К К Мерч» filiali",
  "(сетевые магазины, заказы оформляет админ) во вторичку не входит совсем: ни региона, ни «Экспорта и опта», ни визитов, остатков и аутстока.":
    "(tarmoq do'konlari, buyurtmalarni admin rasmiylashtiradi) ikkilamchiga umuman kirmaydi: na mintaqa, na «Eksport va ulgurji», na tashriflar, qoldiqlar va autstok.",
  "Регионы-каналы «Урикзор» (Базар) и «Сети» (КА)": "«Урикзор» (Bozor) va «Сети» (KA) kanal-mintaqalari",
  "— точки завода: Низомиддин и Мурод бозор/Д63; Хавас, Макро, Магнум, Энг якин маркет, Барака маркет, Болажон Сити молл, Зарафшон, Молочный домик, Assorti. Их заказы из филиалов «Завод» и «К К Мерч» (статусы «доставлен» и «отдан») входят во вторичку отдельными регионами":
    "— zavod nuqtalari: Низомиддин va Мурод бозор/Д63; Хавас, Макро, Магнум, Энг якин маркет, Барака маркет, Болажон Сити молл, Зарафшон, Молочный домик, Assorti. Ularning «Zavod» va «К К Мерч» filiallaridan buyurtmalari («yetkazildi» va «berildi» holatlari) ikkilamchiga alohida mintaqalar sifatida kiradi",
  "только когда месяц закрыт": "faqat oy yopilganda",
  "; в идущем месяце они пустые. «Урикзор» считается с августа 2026. В АКБ эти точки не входят, плана у них нет.":
    "; joriy oyda ular bo'sh. «Урикзор» 2026-yil avgustdan hisoblanadi. Bu nuqtalar AKB ga kirmaydi, ularda reja yo'q.",
  "— число уникальных торговых точек (по ID точки), у которых чистый вес за месяц (продажи минус возвраты) больше нуля. Точка, вернувшая всю покупку, в базу месяца не входит; точка, купившая у двух ТП, считается один раз — так написано в документации «Полевого контроля».":
    "— oy davomida sof vazni (savdo minus qaytarishlar) noldan katta bo'lgan noyob savdo nuqtalari soni (nuqta ID si bo'yicha). Butun xaridini qaytargan nuqta oy bazasiga kirmaydi; ikki TP dan xarid qilgan nuqta bir marta sanaladi — «Dala nazorati» hujjatlarida shunday yozilgan.",
  "— пользователь Linko с должностью «Агент»: это и есть справочник ТП (РМ, СВР и дилеры — оргструктура OneBase, см. ниже). Регион ТП — филиал, где у него больше продаж.":
    "— «Агент» lavozimidagi Linko foydalanuvchisi: TP ma'lumotnomasi aynan shu (RM, SVR va dilerlar — OneBase tashkiliy tuzilmasi, quyida qarang). TP mintaqasi — u ko'proq sotgan filial.",
  "= АКБ ÷ число ТП месяца без вакансий — всех, у кого в месяце есть продажи, визиты или план, а не только продававших. Операторы, супервайзеры и админы оформляют заказы, но ТП не считаются: их продажи входят в итоги, а в численность ТП, медианы региона и «Проблемных агентов» — нет.":
    "= AKB ÷ oydagi bo'sh o'rinlarsiz TP soni — faqat sotganlar emas, oyda savdosi, tashrifi yoki rejasi bor barchasi. Operatorlar, supervayzerlar va adminlar buyurtma rasmiylashtiradi, lekin TP hisoblanmaydi: ularning savdosi jamilarga kiradi, TP soni, mintaqa medianalari va «Muammoli agentlar»ga esa — yo'q.",
  "Старые филиалы Linko": "Linko'ning eski filiallari",
  "(«Жиззах (эски)», «Андижон (эски) 1», «Андижон (эски) 2») считаются в текущем регионе с тем же названием: точки перенесли в новый филиал, а продажи прошлых месяцев остались на старом.":
    "(«Жиззах (эски)», «Андижон (эски) 1», «Андижон (эски) 2») xuddi shu nomdagi joriy mintaqada hisoblanadi: nuqtalar yangi filialga ko'chirilgan, o'tgan oylar savdosi esa eskisida qolgan.",
  "— по типу товара: Бамбук, Кекс, Трубочки, Печенье, Шоколад, Песочный, Придзел (в Linko — «Снек», Twizos) и Помадка (все фасовки одной категорией). Типы вне этого списка (импорт, бонус, оборудование) в итог входят, но в карточки категорий — нет; они видны в блоке «Качество данных».":
    "— tovar turi bo'yicha: Бамбук, Кекс, Трубочки, Печенье, Шоколад, Песочный, Придзел (Linko'da — «Снек», Twizos) va Помадка (barcha qadoqlar bitta toifada). Bu ro'yxatdan tashqari turlar (import, bonus, uskunalar) jamiga kiradi, lekin toifa kartalariga — yo'q; ular «Ma'lumotlar sifati» blokida ko'rinadi.",

  // Как считается: планы, оргструктура, вакансии
  "План — РОП (по умолчанию) или «Завод»": "Reja — ROP (standart) yoki «Zavod»",
  ": переключатель «План» в полосе раздела на страницах вторички, республики, РМ и региона. Это планы регионов по категориям, кг за месяц, загруженные в OneBase из «Полевого контроля» (РОП — январь–сентябрь 2026, «Завод» — сентябрь). Выполнение = факт ÷ план: у региона — весь его факт, у РМ и республики — весь факт их регионов ÷ сумма планов регионов (регион без своего плана добавляет факт, но не план: июнь 2026 — 325 995,93 ÷ 387 900 = 84,04%, хотя у Олмалика плана РОП нет); прогноз выполнения = прогноз ÷ план, только у идущего месяца.":
    ": ikkilamchi, respublika, RM va mintaqa sahifalarida bo'lim panelidagi «Reja» almashtirgichi. Bu mintaqalarning toifalar bo'yicha oylik kg rejalari, OneBase'ga «Dala nazorati»dan yuklangan (ROP — 2026-yil yanvar–sentabr, «Zavod» — sentabr). Bajarilish = fakt ÷ reja: mintaqada — uning butun fakti, RM va respublikada — mintaqalarining butun fakti ÷ mintaqalar rejalari yig'indisi (o'z rejasi yo'q mintaqa faktni qo'shadi, rejani emas: 2026-yil iyun — 325 995,93 ÷ 387 900 = 84,04%, garchi Олмалик uchun ROP rejasi bo'lmasa ham); bajarilish prognozi = prognoz ÷ reja, faqat joriy oyda.",
  "Нет плана выбранного вида на месяц": "Oyga tanlangan turdagi reja yo'q",
  "— план региона и республики берётся из Linko: сумма весовых планов ТП (staff_balance, product_sales_weight), факт — продажи этих ТП; плитка выполнения пишет «плана РОП на этот месяц нет — план ТП из Linko» (или «плана «Завод» …»). Переключатель предлагает только планы, заведённые на месяц. Два источника в одном месяце не смешиваются: регион без плана РОП в месяце с планом РОП остаётся без плана, его факт входит в выполнение РМ и республики. В «Плане и факте по месяцам» у каждого месяца свой источник.":
    "— mintaqa va respublika rejasi Linko'dan olinadi: TP vazn rejalari yig'indisi (staff_balance, product_sales_weight), fakt — shu TP lar savdosi; bajarilish plitkasida «bu oyga ROP rejasi yo'q — TP rejasi Linko'dan» (yoki ««Zavod» rejasi …») yoziladi. Almashtirgich faqat oyga kiritilgan rejalarni taklif qiladi. Bir oyda ikki manba aralashmaydi: ROP rejasi bor oyda ROP rejasi yo'q mintaqa rejasiz qoladi, uning fakti RM va respublika bajarilishiga kiradi. «Oylar bo'yicha reja va fakt»da har oyning o'z manbasi bor.",
  "— сумма плана «регион × категория» по регионам охвата: типы товаров Linko в плане сводятся к категориям отчёта (фасовки помадки — одна категория, «Снек» — Придзел). Факт — все продажи категории в охвате. Выполнение, «осталось», прогноз к плану (от 100% — зелёный, от 90% — оранжевый, ниже — красный) и итог считает сервер. Без планов регионов — по весовым показателям ТП в Linko: «Могуль + Шоколад» — Помадка + Шоколад одной строкой. Серая строка «плана нет» — категория продаётся, а плана на неё нет.":
    "— qamrov mintaqalari bo'yicha «mintaqa × toifa» rejasi yig'indisi: rejadagi Linko tovar turlari hisobot toifalariga keltiriladi (помадка qadoqlari — bitta toifa, «Снек» — Придзел). Fakt — qamrovdagi toifaning barcha savdosi. Bajarilish, «qoldi», rejaga prognoz (100% dan — yashil, 90% dan — to'q sariq, pastroq — qizil) va jamini server hisoblaydi. Mintaqa rejalari bo'lmasa — Linko'dagi TP vazn ko'rsatkichlari bo'yicha: «Могуль + Шоколад» — Помадка + Шоколад bitta qatorda. Kulrang «reja yo'q» qatori — toifa sotilmoqda, lekin unga reja yo'q.",
  "План ТП": "TP rejasi",
  "— в карточке ТП и в команде региона всегда его весовые показатели в Linko; плана ТП по категориям из планов регионов нет.":
    "— TP kartasida va mintaqa jamoasida doim uning Linko'dagi vazn ko'rsatkichlari; mintaqa rejalaridan toifalar bo'yicha TP rejasi yo'q.",
  "План на следующий месяц": "Keyingi oy rejasi",
  "— планы регионов выбранного вида на следующий месяц, если они уже есть, иначе — планы ТП из Linko; разницу с текущим месяцем считает сервер.":
    "— keyingi oy uchun tanlangan turdagi mintaqa rejalari, agar ular allaqachon bo'lsa, aks holda — Linko'dagi TP rejalari; joriy oy bilan farqni server hisoblaydi.",
  "Оргструктура": "Tashkiliy tuzilma",
  "— направления (РМ 1–4 и каналы: Базар, ключевые клиенты), у региона — направление, СВР и дилер. В Linko её нет — она ведётся в «Настройках продаж». По ней — карточки и страницы РМ, охват РМ в «Ассортименте», фильтр РМ в «Проблемных агентах», дилер в «Первичке» и «Аутстоке». «Очистить данные и загрузить заново» не удаляет ни её, ни планы РОП и «Завод»: регион остаётся и после загрузки находит свой филиал Linko, а название берёт из последнего заказа филиала (переименовали филиал в Linko — переименуется и регион).":
    "— yo'nalishlar (RM 1–4 va kanallar: Bozor, asosiy mijozlar), mintaqada — yo'nalish, SVR va diler. Linko'da u yo'q — «Savdo sozlamalari»da yuritiladi. Unga ko'ra — RM kartalari va sahifalari, «Assortiment»dagi RM qamrovi, «Muammoli agentlar»dagi RM filtri, «Birlamchi» va «Autstok»dagi diler. «Ma'lumotlarni tozalash va qayta yuklash» na uni, na ROP va «Zavod» rejalarini o'chirmaydi: mintaqa qoladi va yuklashdan keyin o'z Linko filialini topadi, nomini esa filialning oxirgi buyurtmasidan oladi (Linko'da filial qayta nomlansa — mintaqa ham qayta nomlanadi).",
  "Вакансия": "Bo'sh o'rin",
  "— пользователь Linko со словом «вакан» в имени («вакант», «Вакан …») или с ID 0. Её продажи входят в итоги, её визиты и заказы — в конверсию подразделения, но в рейтинг, медианы и численность ТП — нет; включить в рейтинг можно галочкой.":
    "— ismida «вакан» so'zi bor («вакант», «Вакан …») yoki ID si 0 bo'lgan Linko foydalanuvchisi. Uning savdosi jamilarga, tashrif va buyurtmalari — bo'linma konversiyasiga kiradi, lekin reyting, medianalar va TP soniga — yo'q; reytingga belgi qo'yib qo'shish mumkin.",
  "«Продают, но не заведены в справочнике»": "«Sotadi, lekin ma'lumotnomaga kiritilmagan»",
  "— продающие пользователи Linko без должности «Агент» и не вакансии. Их продажи в итогах региона есть, в команде ТП — нет.":
    "— «Агент» lavozimi yo'q va bo'sh o'rin bo'lmagan, savdo qiladigan Linko foydalanuvchilari. Ularning savdosi mintaqa jamilarida bor, TP jamoasida — yo'q.",

  // Как считается: визиты и страйк
  "Визиты загружаются из Linko API (visits/), считаются выполненные.": "Tashriflar Linko API dan (visits/) yuklanadi, bajarilganlari sanaladi.",
  "Страйк (конверсия) = заказы ТП, принятые в месяце ÷ их выполненные визиты":
    "Strayk (konversiya) = oyda qabul qilingan TP buyurtmalari ÷ ularning bajarilgan tashriflari",
  "— заказы те же, что дали факт (статусы, отчётный день, без «Завода»); может быть больше 100%. «Визиты без заказа» = визиты − заказы (у агента бывает меньше нуля).":
    "— buyurtmalar faktni bergan buyurtmalarning o'zi (holatlar, hisobot kuni, «Zavod»siz); 100% dan oshishi mumkin. «Buyurtmasiz tashriflar» = tashriflar − buyurtmalar (agentda noldan kam bo'lishi mumkin).",
  "У региона и республики визиты и заказы — сумма по ТП (должность «Агент», вакансии тоже), как в таблице «Команда ТП». Визиты остальных — операторов, супервайзеров, пользователей вне справочника — показаны отдельно («не ТП») и в конверсию не входят. Регион без визитов ТП — страйк «—».":
    "Mintaqa va respublikada tashrif va buyurtmalar — TP lar bo'yicha yig'indi («Агент» lavozimi, bo'sh o'rinlar ham), «TP jamoasi» jadvalidagidek. Qolganlarning — operatorlar, supervayzerlar, ma'lumotnomadan tashqari foydalanuvchilarning — tashriflari alohida ko'rsatiladi («TP emas») va konversiyaga kirmaydi. TP tashrifi yo'q mintaqada — strayk «—».",
  "— строки те же ТП, что в плитках и «Команде ТП» (должность «Агент» и вакансии); выполненные визиты остальных (операторы, супервайзеры) показаны числом «не ТП» и в строки не входят. План ТП — его визиты по маршруту за выбранные дни (и невыполненные тоже), «по маршруту» / «вне маршрута» — выполненные визиты по плану / без плана. Заказы — принятые в эти дни (те же, что дали факт) и стоят по дню приёмки; заказ «с визита по маршруту», если":
    "— qatorlar plitkalar va «TP jamoasi»dagi TP larning o'zi («Агент» lavozimi va bo'sh o'rinlar); qolganlarning (operatorlar, supervayzerlar) bajarilgan tashriflari «TP emas» soni bilan ko'rsatiladi va qatorlarga kirmaydi. TP rejasi — tanlangan kunlardagi marshrut bo'yicha tashriflari (bajarilmaganlari ham), «marshrut bo'yicha» / «marshrutdan tashqari» — reja bo'yicha / rejasiz bajarilgan tashriflar. Buyurtmalar — shu kunlarda qabul qilinganlar (faktni berganlarning o'zi) va qabul kuni bo'yicha turadi; buyurtma «marshrut tashrifidan» hisoblanadi, agar",
  "в день его ввода": "u kiritilgan kuni",
  "(created_date) в этот магазин был выполненный визит по плану — любого ТП, «вне маршрута» — если только визит без плана, иначе — «без визита». «Не посещено» = план − по маршруту (не меньше нуля) и «% плана» = по маршруту ÷ план — по итогам самой строки: у региона и республики — по суммарному плану и суммарным визитам, а не сумма недоборов ТП. Дни — «с 1-го по N-е» (по умолчанию по отчётный день) или один день; дни после отчётного помечены «факта нет». Всё, включая строку «Итого», считает сервер. Плана визитов из недельной таблицы «Полевого контроля» в OneBase нет — план здесь всегда маршрут Linko.":
    "(created_date) shu do'konga reja bo'yicha bajarilgan tashrif bo'lsa — istalgan TP niki, «marshrutdan tashqari» — faqat rejasiz tashrif bo'lsa, aks holda — «tashrifsiz». «Tashrif qilinmagan» = reja − marshrut bo'yicha (noldan kam emas) va «reja %» = marshrut bo'yicha ÷ reja — qatorning o'z jamisi bo'yicha: mintaqa va respublikada — umumiy reja va umumiy tashriflar bo'yicha, TP kamomadlari yig'indisi emas. Kunlar — «1-dan N-gacha» (standart — hisobot kunigacha) yoki bitta kun; hisobot kunidan keyingi kunlar «fakt yo'q» deb belgilangan. Hammasini, «Jami» qatorini ham, server hisoblaydi. «Dala nazorati» haftalik jadvalidagi tashriflar rejasi OneBase'da yo'q — bu yerda reja doim Linko marshruti.",
  "Визит «с заказом» в карточке ТП — заказ того же агента в той же точке в день ввода заказа: приёмка обычно на сутки позже визита.":
    "TP kartasidagi «buyurtmali» tashrif — buyurtma kiritilgan kuni o'sha nuqtada o'sha agentning buyurtmasi: qabul odatda tashrifdan bir kun keyin.",
  "Доставщики, курьеры и экспедиторы — не ТП: их «визиты» — это доставки, в конверсию и «Проблемных агентов» они не входят.":
    "Yetkazib beruvchilar, kuryerlar va ekspeditorlar — TP emas: ularning «tashriflari» — bu yetkazishlar, ular konversiyaga va «Muammoli agentlar»ga kirmaydi.",
  "Агент оценивается при 20 и более визитах за месяц; сравнение — с медианой своего региона по ТП с 20+ визитами, без вакансий и без агентов, у которых заказов больше, чем визитов. Заказов больше, чем визитов, — риск «данные не сходятся», низкая конверсия у такого агента не оценивается. Мелкий чек — чек ниже половины медианы при конверсии от 25% и от 20 заказах. «Категорий» у агента — только категории отчёта («бонус» и т.п. не считаются).":
    "Agent oyiga 20 va undan ortiq tashrifda baholanadi; solishtirish — o'z mintaqasidagi 20+ tashrifli TP lar medianasi bilan, bo'sh o'rinlarsiz va buyurtmalari tashriflaridan ko'p agentlarsiz. Buyurtmalar tashriflardan ko'p — «ma'lumotlar mos kelmaydi» xavfi, bunday agentda past konversiya baholanmaydi. Mayda chek — konversiya 25% dan va buyurtmalar 20 tadan kam bo'lmaganda medianining yarmidan past chek. Agentdagi «Toifalar» — faqat hisobot toifalari («bonus» va h.k. sanalmaydi).",
  "Порядок «Проблемных агентов»": "«Muammoli agentlar» tartibi",
  "— по тяжести замечаний, как в «Полевом контроле» (в его документации правила нет, взято из кода): 10 за критичное, 4 за риск, плюс (1 − страйк, не больше 1) × 3 у оцениваемых; при равной тяжести выше тот, у кого меньше выручка, вакансии — последними. Номер в углу карточки — место в этом списке; тяжесть и список считает сервер.":
    "— izohlar og'irligi bo'yicha, «Dala nazorati»dagidek (uning hujjatlarida qoida yo'q, koddan olingan): kritik uchun 10, xavf uchun 4, baholanadiganlarda qo'shimcha (1 − strayk, 1 dan ko'p emas) × 3; og'irlik teng bo'lsa, tushumi kamrog'i yuqorida, bo'sh o'rinlar — oxirida. Karta burchagidagi raqam — shu ro'yxatdagi o'rin; og'irlik va ro'yxatni server hisoblaydi.",
  "Плашки «% от цели»: от 95% цели — зелёная, от 75% — оранжевая, ниже — красная. Выполнение плана, прогноз выполнения и «% плана» визитов: от 90% — зелёный, от 60% — оранжевый, ниже — красный; прогноз к плану по категориям: от 100% — зелёный, от 90% — оранжевый. Уровень цвета отдаёт сервер, интерфейс только раскрашивает.":
    "«Maqsaddan %» belgilari: maqsadning 95% idan — yashil, 75% dan — to'q sariq, pastroq — qizil. Reja bajarilishi, bajarilish prognozi va tashriflar «reja %»: 90% dan — yashil, 60% dan — to'q sariq, pastroq — qizil; toifalar bo'yicha rejaga prognoz: 100% dan — yashil, 90% dan — to'q sariq. Rang darajasini server beradi, interfeys faqat bo'yaydi.",

  // Как считается: календари
  "Календарь месяца": "Oy kalendari",
  "— по ТП (регион) и по регионам (республика, РМ): столбец — день, меры кг / деньги / АКБ / АКБ по категории (например, Придзел). Кг и деньги: сумма дней равна итогу месяца; АКБ в итогах — уникальные точки за период, а не сумма по дням; дни после отчётного — «нет данных».":
    "— TP bo'yicha (mintaqa) va mintaqalar bo'yicha (respublika, RM): ustun — kun, o'lchovlar kg / pul / AKB / toifa bo'yicha AKB (masalan, Придзел). Kg va pul: kunlar yig'indisi oy jamisiga teng; jamilardagi AKB — davrdagi noyob nuqtalar, kunlar yig'indisi emas; hisobot kunidan keyingi kunlar — «ma'lumot yo'q».",
  "«АКБ по месяцам»": "«Oylar bo'yicha AKB»",
  "— месяцы с января по выбранный; АКБ, кг и сум по одним и тем же строкам (чистые — минус возвраты) и среднее за месяц по месяцам с данными (идущий месяц в счёт) — всё с сервера. «Песочный» на графике скрыт (настройка Sales:AkbChartHiddenCategories), линий категорий не больше семи — самые крупные по весу, остальные есть в карточках категорий; итог — все продажи подразделения. Переключатель АКБ / кг / сум общий для всех карточек «по месяцам» на всех страницах раздела и живёт до перезагрузки страницы.":
    "— yanvardan tanlangan oygacha bo'lgan oylar; AKB, kg va so'm bir xil qatorlar bo'yicha (sof — qaytarishlar ayirilgan) va ma'lumotli oylar bo'yicha oylik o'rtacha (joriy oy ham hisobga olinadi) — hammasi serverdan. Grafikda «Песочный» yashirilgan (Sales:AkbChartHiddenCategories sozlamasi), toifa chiziqlari yettitadan ko'p emas — vazn bo'yicha eng yiriklari, qolganlari toifa kartalarida bor; jami — bo'linmaning barcha savdosi. AKB / kg / so'm almashtirgichi bo'limning barcha sahifalaridagi barcha «oylar bo'yicha» kartalar uchun umumiy va sahifa qayta yuklanguncha saqlanadi.",
  "«Из чего складывается объём»": "«Hajm nimadan tashkil topgan»",
  "у региона — доля категории в выручке за месяц: выручка категории ÷ выручка всех категорий региона (чистая).":
    "mintaqada — toifaning oylik tushumdagi ulushi: toifa tushumi ÷ mintaqaning barcha toifalari tushumi (sof).",
  "Смена месяца": "Oyni almashtirish",
  "на странице ТП или магазина уходит к его региону (нет региона — к республике), план РОП / «Завод» сохраняется; на остальных страницах страница остаётся, а период календаря визитов (с … по …) и разбор дня первички сбрасываются. Раскрытые секции, сортировки и раскрытые строки таблиц привязаны к месяцу: после смены месяца старых выборов нет.":
    "TP yoki do'kon sahifasida uning mintaqasiga o'tkazadi (mintaqa yo'q bo'lsa — respublikaga), ROP / «Zavod» rejasi saqlanadi; boshqa sahifalarda sahifa o'zgarmaydi, tashriflar kalendari davri (… dan … gacha) va birlamchi kun tahlili esa tozalanadi. Ochilgan bo'limlar, saralashlar va jadvallarning ochilgan qatorlari oyga bog'langan: oy almashgandan keyin eski tanlovlar qolmaydi.",

  // Как считается: ассортимент
  "У точки есть товар, если её чистая строка по нему положительна": "Nuqtada tovar bor, agar u bo'yicha sof qatori musbat bo'lsa",
  "— кг или сумма больше нуля (заказы минус возвраты, по всем ТП точки). ТТ артикула — такие точки, каждая один раз, даже если купила у двух ТП. Артикул «продаётся», если такая точка есть хотя бы одна: возврат в одной точке не отменяет продажу в другой. Дистрибуция — доля ТТ с покупкой, у которых строка товара положительна. АКБ категории — точки, у которых чистый вес категории больше нуля.":
    "— kg yoki summa noldan katta (buyurtmalar minus qaytarishlar, nuqtaning barcha TP lari bo'yicha). Artikul nuqtalari — shunday nuqtalar, har biri bir marta, hatto ikki TP dan xarid qilgan bo'lsa ham. Artikul «sotilmoqda», agar kamida bitta shunday nuqta bo'lsa: bir nuqtadagi qaytarish boshqasidagi savdoni bekor qilmaydi. Distributsiya — tovar qatori musbat bo'lgan xaridli nuqtalar ulushi. Toifa AKB si — toifaning sof vazni noldan katta bo'lgan nuqtalar.",
  "«Продаётся N из M SKU»": "«M SKU dan N tasi sotilmoqda»",
  ": M — SKU категории с продажами во вторичке с 1 января по конец выбранного месяца (весь каталог Linko и товары, которые продавал только «Завод», не входят); «молчат» = M − N, из них «пропало» — продавались в прошлом месяце.":
    ": M — 1-yanvardan tanlangan oy oxirigacha ikkilamchida savdosi bo'lgan toifa SKU lari (butun Linko katalogi va faqat «Zavod» sotgan tovarlar kirmaydi); «jim» = M − N, ulardan «yo'qoldi» — o'tgan oyda sotilganlar.",
  "«Только он»": "«Yolg'iz»",
  "— точки, которые из всех SKU купили ровно один (положительная строка только по нему): у артикула — сколько таких точек держатся на нём, у охвата — всего таких точек.":
    "— barcha SKU dan aynan bittasini sotib olgan nuqtalar (musbat qator faqat u bo'yicha): artikulda — shunday nuqtalardan nechtasi unga tayanadi, qamrovda — bunday nuqtalar jami.",
  "Матрица «товар × регион»": "«Tovar × mintaqa» matritsasi",
  "— 20 товаров с наибольшей выручкой; клетка — дистрибуция в регионе, «Всего» — по всему охвату (все ТТ с товаром ÷ все ТТ с покупкой). Красный — товара в регионе нет, оранжевый — меньше 40% от «Всего». Матрица и таблица регионов — только если в охвате два региона и больше; регион без покупок за месяц — «нет данных».":
    "— eng ko'p tushumli 20 ta tovar; katak — mintaqadagi distributsiya, «Jami» — butun qamrov bo'yicha (tovarli barcha nuqtalar ÷ xaridli barcha nuqtalar). Qizil — mintaqada tovar yo'q, to'q sariq — «Jami»ning 40% idan kam. Matritsa va mintaqalar jadvali — faqat qamrovda ikki va undan ko'p mintaqa bo'lsa; oy davomida xaridsiz mintaqa — «ma'lumot yo'q».",
  "«Товары»": "«Mahsulotlar»",
  "— только категории отчёта (бонусы и подарки — отдельным числом).":
    "— faqat hisobot toifalari (bonuslar va sovg'alar — alohida son bilan).",
  "у магазина — по весу. Метка": "do'konda — vazn bo'yicha. Belgi",
  "«ТОП»": "«TOP»",
  "— 59 кодов списка ТОП «Полевого контроля» (настройка Sales:TopProducts).": "— «Dala nazorati» TOP ro'yxatining 59 ta kodi (Sales:TopProducts sozlamasi).",
  "«Продажи по SKU»": "«SKU bo'yicha sotuv»",
  "— тот же факт вторички за любой отрезок месяцев (не длиннее 24): кг и сумма нетто, АКБ SKU — точки с положительной строкой хотя бы в одном месяце отрезка. ABC считается внутри выборки (регион, ТОП, категории): по убыванию кг, A — первые 80% веса, B — до 95%, C — хвост. Динамика — кг в день в последнем квартале отрезка к первому. Январь–сентябрь 2026: ABC совпадает с «Полевым контролем» у всех общих SKU; разница в килограммах — его дыры (январь, июнь) и старые деньги возвратов сентября.":
    "— istalgan oylar oralig'i (24 tadan uzun emas) uchun ikkilamchining o'sha fakti: kg va summa netto, SKU AKB si — oraliqning kamida bitta oyida musbat qatorli nuqtalar. ABC tanlov ichida hisoblanadi (mintaqa, TOP, toifalar): kg kamayishi bo'yicha, A — vaznning birinchi 80%, B — 95% gacha, C — dum. Dinamika — oraliqning oxirgi choragidagi kunlik kg birinchisiga nisbatan. 2026-yil yanvar–sentabr: ABC barcha umumiy SKU larda «Dala nazorati» bilan mos keladi; kilogrammdagi farq — uning bo'shliqlari (yanvar, iyun) va sentabr qaytarishlarining eski pullari.",
  "Где «Полевой контроль» расходится со своей документацией, считается по документации: он требует ещё и положительного итога точки (август, SKU 235 — 2 038 точек против 2 036), берёт в M каталог всех месяцев выгрузки и SKU одного «Завода» (196), а матрицу — по точкам с любой строкой товара.":
    "«Dala nazorati» o'z hujjatlaridan farq qiladigan joyda hujjatlar bo'yicha hisoblanadi: u nuqtaning musbat jamisini ham talab qiladi (avgust, SKU 235 — unda 2 038 nuqta, bizda 2 036), M ga yuklamaning barcha oylari katalogini va faqat «Zavod» SKU larini (196) oladi, matritsani esa — tovarning istalgan qatori bor nuqtalar bo'yicha.",

  // Как считается: первичка и остатки
  "Первичка — отгрузка завода контрагентам, только из Linko.": "Birlamchi — zavodning kontragentlarga jo'natmasi, faqat Linko'dan.",
  "Дилеры — перемещения со склада завода на склады дилеров в статусах «отдано» или «принято», дата — доставка (с ней сходится день таблицы первички «Полевого контроля»), без неё — выдача. Прямые клиенты завода — заказы филиалов «Завод» и «К К Мерч» точкам не экспортного типа (базары, сети, фирменный магазин) в статусах «доставлен» и «отдан», дата — создание заказа; «Дегустатсия + Акция» не входит. Склад «Экспорт» — отдельно. В «Полевом контроле» первичка — таблица ГП с теми же контрагентами: август 2026 у нас 408 994 кг против 408 388 (+0,15%), сентябрь 369 524 против 369 405 (+0,03%).":
    "Dilerlar — «berildi» yoki «qabul qilindi» holatlaridagi zavod omboridan diler omborlariga ko'chirishlar, sana — yetkazish («Dala nazorati» birlamchi jadvalidagi kun u bilan mos keladi), u bo'lmasa — berish. Zavodning to'g'ridan-to'g'ri mijozlari — «Zavod» va «К К Мерч» filiallarining eksport turida bo'lmagan nuqtalarga (bozorlar, tarmoqlar, firma do'koni) «yetkazildi» va «berildi» holatlaridagi buyurtmalari, sana — buyurtma yaratilishi; «Дегустатсия + Акция» kirmaydi. «Экспорт» ombori — alohida. «Dala nazorati»da birlamchi — o'sha kontragentlar bilan TM jadvali: 2026-yil avgust bizda 408 994 kg, unda 408 388 (+0,15%), sentabr 369 524, unda 369 405 (+0,03%).",
  "Возвраты вычитаются": "Qaytarishlar ayiriladi",
  ": перемещения со складов дилеров на завод и возвраты по заказам точек — строки с минусом, поэтому месяцы, дилеры, категории, календарь и «С начала года» — нетто; плитка «Возвраты» показывает их отдельно. Склады сводятся к регионам («Коканд бозор» — Коканд, «эски»-склады Андижона и Жиззаха — в своих регионах), подпись строки — дилер региона из «Настроек продаж».":
    ": diler omborlaridan zavodga ko'chirishlar va nuqtalar buyurtmalari bo'yicha qaytarishlar — minusli qatorlar, shuning uchun oylar, dilerlar, toifalar, kalendar va «Yil boshidan» — netto; «Qaytarish» plitkasi ularni alohida ko'rsatadi. Omborlar mintaqalarga keltiriladi («Коканд бозор» — Коканд, Андижон va Жиззах «эски» omborlari — o'z mintaqalarida), qator yozuvi — «Savdo sozlamalari»dagi mintaqa dileri.",
  "Деньги": "Pul",
  ": «Сумма (цена дилера)» — сумма перемещения или заказа, по ней дилер берёт товар у завода (это «сумма дилера» таблицы первички); «по цене продажи дилера» — прайс «Дилердан чикиш нарх». Цены завода в Linko нет, поэтому «суммы завода» нет. Коробки — по весу коробки из названия. План первички — из OneBase (вид «Первичка»), сейчас не загружен. Прогноз — по дате данных (последний день с отгрузкой, не позже сегодня и последней синхронизации). Клиенты по месяцам — контрагенты с нетто отгрузки больше нуля. Календарь отгрузок — период «с … по …», день и контрагент; всё считает сервер.":
    ": «Summa (diler narxi)» — ko'chirish yoki buyurtma summasi, diler zavoddan tovarni shu narxda oladi (bu birlamchi jadvalidagi «diler summasi»); «diler sotuv narxida» — «Дилердан чикиш нарх» praysi. Linko'da zavod narxi yo'q, shuning uchun «zavod summasi» yo'q. Qutilar — nomidagi quti vazni bo'yicha. Birlamchi rejasi — OneBase'dan («Birlamchi» turi), hozir yuklanmagan. Prognoz — ma'lumotlar sanasi bo'yicha (bugundan va oxirgi sinxronlashdan kech bo'lmagan oxirgi jo'natmali kun). Oylar bo'yicha mijozlar — netto jo'natmasi noldan katta kontragentlar. Jo'natmalar kalendari — «… dan … gacha» davr, kun va kontragent; hammasini server hisoblaydi.",
  "— заказы филиала «Завод» торговым точкам с типом EXPORT (только статус «доставлен» — так экспорт сверен до килограмма; дата — приёмка), минус возвраты. Страна — по названию или адресу точки в Linko («Daler Tojikiston» → Таджикистан); Россия — по точкам, как в «Полевом контроле»: «Россия (Дагестан)» и «Россия (Уфа)». Коробок у экспорта нет. Перемещения на склад «Экспорт» — внутреннее движение склада, в экспорт не входят.":
    "— «Zavod» filialining EXPORT turidagi savdo nuqtalariga buyurtmalari (faqat «yetkazildi» holati — eksport shunday qilib kilogrammgacha solishtirilgan; sana — qabul), minus qaytarishlar. Davlat — Linko'dagi nuqta nomi yoki manzili bo'yicha («Daler Tojikiston» → Таджикистан); Rossiya — nuqtalar bo'yicha, «Dala nazorati»dagidek: «Россия (Дагестан)» va «Россия (Уфа)». Eksportda quti yo'q. «Экспорт» omboriga ko'chirishlar — omborning ichki harakati, eksportga kirmaydi.",
  "Рекомендуемый остаток — снимок на дату загрузки, а не итог месяца": "Tavsiya etilgan qoldiq — oy yakuni emas, yuklash sanasidagi kesim",
  ", поэтому периода у вкладки нет. Остатки Linko — в штуках (единицах учёта). Кг = штуки × вес штуки; вес штуки = Σ веса ÷ Σ количества по строкам заказов за год, а если продаж не было — из фасовки в названии с пометкой «≈». Коробки = кг ÷ вес коробки из названия, и только если в коробке целое число штук. Умножать штуки на вес коробки нельзя — это завысит остаток во столько раз, сколько штук в коробке.":
    ", shuning uchun bo'limda davr yo'q. Linko qoldiqlari — donada (hisob birliklarida). Kg = dona × dona vazni; dona vazni = Σ vazn ÷ Σ miqdor, bir yillik buyurtma qatorlari bo'yicha, savdo bo'lmagan bo'lsa — nomidagi qadoqdan, «≈» belgisi bilan. Qutilar = kg ÷ nomidagi quti vazni, va faqat qutida butun son dona bo'lsa. Donani quti vazniga ko'paytirib bo'lmaydi — bu qoldiqni qutidagi dona soni marta oshirib yuboradi.",
  "= штуки × входная цена дилера из прайса «Дилерга кириш нарх» (последняя по времени строка товара). Цена в Linko стоит за ту же единицу учёта, что и остаток, поэтому вес единицы в сумме не участвует и ошибка «цена за кг вместо цены за штуку» невозможна.":
    "= dona × «Дилерга кириш нарх» praysidagi dilerning kirim narxi (tovarning vaqt bo'yicha oxirgi qatori). Linko'da narx qoldiq bilan bir xil hisob birligi uchun, shuning uchun birlik vazni summada qatnashmaydi va «dona narxi o'rniga kg narxi» xatosi bo'lishi mumkin emas.",
  "Продажи в день — вторичка (доставленные и отданные заказы, нетто возвратов) за последние 87 дней до вчера в регионе склада, с поправкой на аутсток: делится на дни, когда товар был (дни без товара — из аутстока за прошлый месяц, не больше 30). Запас на 15 и 30 дней — скорость × горизонт, «хватит» — остаток ÷ скорость. Дефицит — меньше 15 дней продаж, затоварка — больше 30, «не продаётся» — остаток есть, а продаж за 87 дней нет, «нет остатка» — нет ни остатка, ни продаж. Рекомендуемый заказ — до запаса на 15 дней, вверх до целой коробки; ТОП-товару, которого у дилера нет вовсе, — стартовый заказ 10 коробок. Брони («заказ есть, отгрузки нет») в OneBase нет: Linko External API её не отдаёт.":
    "Kunlik savdo — ombor mintaqasida kechagacha oxirgi 87 kunlik ikkilamchi (yetkazilgan va berilgan buyurtmalar, qaytarishlarsiz), autstokka tuzatilgan: tovar bo'lgan kunlarga bo'linadi (tovarsiz kunlar — o'tgan oy autstokidan, 30 dan ko'p emas). 15 va 30 kunlik zaxira — tezlik × muddat, «yetadi» — qoldiq ÷ tezlik. Taqchil — 15 kunlik savdodan kam, ortiqcha — 30 dan ko'p, «sotilmaydi» — qoldiq bor, lekin 87 kunda savdo yo'q, «qoldiq yo'q» — na qoldiq, na savdo. Tavsiya buyurtma — 15 kunlik zaxiragacha, butun qutigacha yuqoriga; dilerda umuman yo'q TOP-tovarga — 10 quti boshlang'ich buyurtma. Bron («buyurtma bor, jo'natilmagan») OneBase'da yo'q: Linko External API uni bermaydi.",
  "Склад региона — склад с тем же названием, что у региона. Остальные склады («эски», «интеграция», «Основной»…) в остаток регионов не складываются и показаны отдельно. Остаток завода в итог страны не входит (товар ещё не отгружен дилерам), а его запас меряется скоростью продаж всей страны.":
    "Mintaqa ombori — mintaqa bilan bir xil nomdagi ombor. Boshqa omborlar («эски», «интеграция», «Основной»…) mintaqalar qoldig'iga qo'shilmaydi va alohida ko'rsatiladi. Zavod qoldig'i mamlakat jamiga kirmaydi (tovar hali dilerlarga jo'natilmagan), uning zaxirasi esa butun mamlakat savdo tezligi bilan o'lchanadi.",
  "— дни, когда у дилера не было товара, который он в периоде продавал. Остаток по дням восстановлен назад от снимка Linko: утро = вечер + продано (заказы «доставлен» и «отдан» по дате приёмки, как во вторичке) − пришло (перемещения на склад дилера — с завода, от других дилеров и складов — по дате приёмки; минус отправленное со склада дилера — заводу и другим дилерам — по дате выдачи; плюс возвраты магазинов дилеру); средние продажи и цена — по тем же заказам; минус считается нулём и учитывается в точности. Упущено = дней в нуле × средние продажи в день × средняя цена у дилера; ядро — пары, дающие 80% потерь; хронический — в нуле полпериода и дольше; недовоз — на заводе в тот день товар был (остаток завода — так же назад по перемещениям; выпуска цехов в Linko нет, поэтому доля завода — оценка снизу). По умолчанию показаны ТОП-товары — 59 кодов списка ТОП «Полевого контроля» (Sales:TopProducts; без настройки — категории отчёта); карточки категорий — фильтр.":
    "— dilerda davr ichida sotgan tovari bo'lmagan kunlar. Kunlik qoldiq Linko kesimidan orqaga tiklangan: ertalab = kechqurun + sotilgan (qabul sanasi bo'yicha «yetkazildi» va «berildi» buyurtmalar, ikkilamchidagidek) − kelgan (diler omboriga ko'chirishlar — zavoddan, boshqa dilerlar va omborlardan — qabul sanasi bo'yicha; minus diler omboridan jo'natilgan — zavodga va boshqa dilerlarga — berish sanasi bo'yicha; plyus do'konlarning dilerga qaytarishlari); o'rtacha savdo va narx — o'sha buyurtmalar bo'yicha; minus nol deb hisoblanadi va aniqlikda hisobga olinadi. Yo'qotilgan = nolda turgan kunlar × kunlik o'rtacha savdo × dilerdagi o'rtacha narx; yadro — yo'qotishlarning 80% ini beradigan juftlar; surunkali — davrning yarmi va undan ko'p nolda; yetkazilmagan — o'sha kuni zavodda tovar bor edi (zavod qoldig'i — xuddi shunday ko'chirishlar bo'yicha orqaga; Linko'da sexlar ishlab chiqarishi yo'q, shuning uchun zavod ulushi — pastki baho). Standart bo'yicha TOP tovarlar ko'rsatiladi — «Dala nazorati» TOP ro'yxatining 59 ta kodi (Sales:TopProducts; sozlamasiz — hisobot toifalari); toifa kartalari — filtr.",

  // Как считается: сверка с API и «Полевым контролем», чего нет в Linko
  "Визиты в API есть": "API da tashriflar bor",
  "(visits/, visits_count/), выгрузка из интерфейса для них не нужна.": "(visits/, visits_count/), ular uchun interfeysdan yuklab olish kerak emas.",
  "Ссылка «возврат → заказ» есть": "«Qaytarish → buyurtma» havolasi bor",
  ": у строк возврата заполнены order_id и order_product_id. Правило при этом не менялось — возврат вычитается по своей дате.":
    ": qaytarish qatorlarida order_id va order_product_id to'ldirilgan. Qoida bunda o'zgarmagan — qaytarish o'z sanasi bo'yicha ayiriladi.",
  "Лимит страницы возвратов сейчас 1000, как и у остальных ресурсов (не 500). OneBase запрашивает возвраты по 500 и сам замечает, если сервер отдаёт страницу меньше запрошенной.":
    "Qaytarishlar sahifasi limiti hozir 1000, boshqa resurslardagidek (500 emas). OneBase qaytarishlarni 500 tadan so'raydi va server so'ralganidan kichik sahifa qaytarsa, buni o'zi sezadi.",
  "Остаток (balance) приходит строкой «0.000000000» — разбирается как число.": "Qoldiq (balance) «0.000000000» satri ko'rinishida keladi — son sifatida o'qiladi.",
  "Фильтр is_synced на сервере сейчас ни на что не влияет: заказы отдаются все.": "Serverdagi is_synced filtri hozir hech narsaga ta'sir qilmaydi: barcha buyurtmalar beriladi.",
  "Правила — по документации «Полевого контроля»": "Qoidalar — «Dala nazorati» hujjatlari bo'yicha",
  ": дата приёмки, статусы «доставлен» и «отдан», возвраты по строкам, без «Завода» и «К К Мерч», старые филиалы в своих регионах. Август: 389 507 кг против 389 523 по 19 регионам; АКБ за август — 13 025 точек и там и там, по каждому региону.":
    ": qabul sanasi, «yetkazildi» va «berildi» holatlari, qatorlar bo'yicha qaytarishlar, «Zavod» va «К К Мерч»siz, eski filiallar o'z mintaqalarida. Avgust: 19 mintaqa bo'yicha 389 507 kg, unda 389 523; avgust AKB si — ikkalasida ham 13 025 nuqta, har bir mintaqa bo'yicha.",
  "Сентябрь: 365 112 кг против 365 055": "Sentabr: 365 112 kg, unda 365 055",
  "по 19 регионам. Разница — отклонения эталона от его же документации, которые здесь не повторяются: окно выгрузки по дате создания (+44 кг, Бухоро — заказы, созданные до 20 августа) и вес шапки заказа вместо строк (+13 кг, Самарканд). АКБ — по правилу «нетто больше нуля»: плитки v174 за сентябрь и октябрь считают точки с любым заказом (13 703) — это расходится и с его документацией, и с его августом.":
    "19 mintaqa bo'yicha. Farq — etalonning o'z hujjatlaridan chetlanishlari, ular bu yerda takrorlanmaydi: yaratilish sanasi bo'yicha yuklama oynasi (+44 kg, Бухоро — 20-avgustgacha yaratilgan buyurtmalar) va qatorlar o'rniga buyurtma sarlavhasi vazni (+13 kg, Самарканд). AKB — «netto noldan katta» qoidasi bo'yicha: v174 ning sentabr va oktabr plitkalari istalgan buyurtmali nuqtalarni sanaydi (13 703) — bu uning hujjatlariga ham, avgustiga ham zid.",
  "План РОП за сентябрь — 484 937 кг, «Завод» — 470 002 кг": "Sentabr ROP rejasi — 484 937 kg, «Zavod» — 470 002 kg",
  ", по каждому региону и категории — как в v174. Выполнение регионов совпадает, кроме Бухоро и Самарканда — там разница факта из-за дефектов эталона выше; республика — 75,3% плана РОП.":
    ", har bir mintaqa va toifa bo'yicha — v174 dagidek. Mintaqalar bajarilishi mos keladi, Бухоро va Самарканд bundan mustasno — u yerda fakt farqi yuqorida aytilgan etalon nuqsonlari tufayli; respublika — ROP rejasining 75,3%.",
  "«Урикзор» (базар) и «Сети»": "«Урикзор» (bozor) va «Сети»",
  "идут в «Полевой контроль» из таблицы, в Linko их нет (в августе — 14 628 кг Урикзора).": "«Dala nazorati»ga jadvaldan keladi, Linko'da ular yo'q (avgustda Урикзор — 14 628 kg).",
  "Дыры в данных «Полевого контроля»": "«Dala nazorati» ma'lumotlaridagi bo'shliqlar",
  "здесь не повторяются: в июне в нём нет Андижона и Жиззаха (в Linko 4 471 и 4 712 кг), в январе — старого филиала Андижона (1 342 кг).":
    "bu yerda takrorlanmaydi: iyunda unda Андижон va Жиззах yo'q (Linko'da 4 471 va 4 712 kg), yanvarda — Андижон eski filiali (1 342 kg).",
  "Планов регионов (РОП, «Завод»), РМ, СВР и дилеров в Linko нет — это данные OneBase. На месяц без планов регионов план региона и республики — сумма планов ТП по весу из API планов Linko (staff_balance); у ТП без плана в Linko план не выдумывается. Командные планы супервайзеров в Linko к региону не привязаны — в план региона они не входят и видны во вкладке «Планы».":
    "Mintaqa rejalari (ROP, «Zavod»), RM, SVR va dilerlar Linko'da yo'q — bu OneBase ma'lumotlari. Mintaqa rejalari bo'lmagan oyda mintaqa va respublika rejasi — Linko rejalar API dan (staff_balance) TP larning vazn bo'yicha rejalari yig'indisi; Linko'da rejasi yo'q TP uchun reja to'qib chiqarilmaydi. Supervayzerlarning jamoa rejalari Linko'da mintaqaga bog'lanmagan — ular mintaqa rejasiga kirmaydi va «Rejalar» bo'limida ko'rinadi.",
  "Плана первички, цены завода (по ней в «Полевом контроле» «сумма завода»), премий и ФОТ — в External API этих данных нет. Страны у экспорта тоже нет отдельным полем: она берётся из названия или адреса экспортной точки.":
    "Birlamchi rejasi, zavod narxi («Dala nazorati»da u bo'yicha «zavod summasi»), mukofotlar va ish haqi fondi — External API'da bu ma'lumotlar yo'q. Eksportda davlat ham alohida maydon emas: u eksport nuqtasining nomi yoki manzilidan olinadi.",
  "Даты реализации у возврата и веса штуки в справочнике товаров — нет; вес штуки считается из заказов.":
    "Qaytarishda sotuv sanasi, tovarlar ma'lumotnomasida esa dona vazni yo'q; dona vazni buyurtmalardan hisoblanadi.",
  "Запись в Linko (sync_*, отметки «синхронизировано») OneBase не выполняет: отметка скрывает документы из выдачи для других интеграций, например 1С.":
    "OneBase Linko'ga yozmaydi (sync_*, «sinxronlangan» belgilari): belgi hujjatlarni boshqa integratsiyalar, masalan 1C uchun berilmadan yashiradi.",

  // KPI
  "KPI · Продажи": "KPI · Savdo",
  "Премии и выполнение KPI из Linko за {month} · {tp} ТП и {svr} СВР":
    "Linko'dan {month} uchun KPI mukofotlari va bajarilishi · {tp} TP va {svr} SVR",
  "KPI за этот месяц в Linko нет — показатели появятся после пересчёта планов в Linko.":
    "Bu oy uchun Linko'da KPI yo'q — ko'rsatkichlar Linko'da rejalar qayta hisoblangandan keyin paydo bo'ladi.",
  "Месяц закрыт — прогноз не нужен": "Oy yopilgan — prognoz kerak emas",
  "Прогноз на месяц": "Oy uchun prognoz",
  "Пересчёт Linko загружен {time}": "Linko qayta hisobi {time} da yuklangan",
  "прогноз: факт ÷ {share} месяца": "prognoz: fakt ÷ oyning {share} qismi",
  "прогноз вторички ТП на месяц": "TP ikkilamchi savdosining oylik prognozi",
  "вторичка {n} ТП за месяц": "{n} TP ning oylik ikkilamchi savdosi",
  "Премии": "Mukofotlar",
  "ТП {tp} · СВР {svr}": "TP {tp} · SVR {svr}",
  "оклад плюс премии по Linko": "oklad va Linko bo'yicha mukofotlar",
  "Премии и оклады": "Mukofot va okladlar",
  "видны с правом «Финансы»": "«Moliya» huquqi bilan ko'rinadi",
  "{n} без региона — укажите ниже": "{n} kishi mintaqasiz — quyida ko'rsating",
  "без KPI в Linko: {n}": "Linko'da KPI yo'q: {n}",
  "у всех людей регион определён": "hammaning mintaqasi aniqlangan",
  "Оклады, бонусы и премии скрыты: нужно право «Финансы». Выполнение и факт видны всем с доступом к продажам.":
    "Oklad, bonus va mukofotlar yashirin: «Moliya» huquqi kerak. Bajarilish va fakt savdoga kirishi bor hammaga ko'rinadi.",
  "У этих людей в Linko нет филиала, а по продажам, визитам и названиям регион не определился. Укажите регион — он сохранится в профиле и подтянется во всём разделе продаж.":
    "Bu kishilarning Linko'da filiali yo'q, savdo, tashrif va nomlar bo'yicha ham mintaqa aniqlanmadi. Mintaqani ko'rsating — u profilda saqlanadi va butun savdo bo'limida qo'llanadi.",
  "Регионы с продажами, у которых в Linko нет KPI за месяц: {list}. Для них премия не считается.":
    "Savdosi bor, lekin Linko'da oylik KPI yo'q mintaqalar: {list}. Ular uchun mukofot hisoblanmaydi.",
  "KPI — из Linko (API планов): у каждого человека свой набор показателей. В ячейке — выполнение, под ним факт / план и премия. Премия — по уровням показателя в Linko: от порога уровня (обычно 80%, АКБ — 90%) — бонус × выполнение, выше 100% не растёт, до порога — 0. «На сегодня» — премия, начисленная Linko; «Прогноз» — факт растянут на месяц текущим темпом (АКБ не растягивается — точки не складываются по дням) и премия пересчитана по тем же уровням. Зарплата = оклад + премии. Факт ТП — вторичка агента в кг, факт СВР — вторичка всех ТП региона.":
    "KPI — Linko'dan (rejalar API): har bir kishining o'z ko'rsatkichlari bor. Katakda — bajarilish, ostida fakt / reja va mukofot. Mukofot — Linko'dagi ko'rsatkich darajalari bo'yicha: daraja chegarasidan (odatda 80%, AKB — 90%) — bonus × bajarilish, 100% dan oshmaydi, chegaragacha — 0. «Bugunga» — Linko hisoblagan mukofot; «Prognoz» — fakt joriy sur'atda oyga cho'zilgan (AKB cho'zilmaydi — nuqtalar kunlar bo'yicha qo'shilmaydi) va mukofot o'sha darajalar bo'yicha qayta hisoblangan. Ish haqi = oklad + mukofotlar. TP fakti — agentning kg dagi ikkilamchi savdosi, SVR fakti — mintaqadagi barcha TP ning ikkilamchi savdosi.",
  "Факт СВР, кг": "SVR fakti, kg",
  "Факт ТП, кг": "TP fakti, kg",
  "Премии СВР": "SVR mukofotlari",
  "Премии ТП": "TP mukofotlari",
  "Премии всего": "Jami mukofotlar",
  "нажмите на регион — откроется его карточка": "mintaqani bosing — uning kartasi ochiladi",
  "план в сумах": "reja so'mda",
  "план в кг": "reja kg da",
  "факт ТП {kg} кг": "TP fakti {kg} kg",
  "Оклад": "Oklad",
  "Премия при 100% по всем показателям": "Barcha ko'rsatkichlar 100% bo'lgandagi mukofot",
  "регион указан вручную": "mintaqa qo'lda ko'rsatilgan",
  "по «СВР региона»": "«Mintaqa SVR» bo'yicha",
  "регион по продажам и визитам": "mintaqa savdo va tashriflar bo'yicha",
  "регион по названию в Linko": "mintaqa Linko'dagi nom bo'yicha",
  "регион не определён": "mintaqa aniqlanmagan",
  "Регион: {name}": "Mintaqa: {name}",
  "Указать регион…": "Mintaqani ko'rsatish…",
  "порог премии: {thr}": "mukofot chegarasi: {thr}",
  "Бонус 100%: {sum}": "Bonus 100%: {sum}",
  "продажи {sale} · возвраты {ret}": "savdo {sale} · qaytarmalar {ret}",

  // Акции
  "Акции · Продажи": "Aksiyalar · Savdo",
  "Скидки и бонусы вторички по акциям Linko: сколько стоили, где и кому, окупились ли":
    "Linko aksiyalari bo'yicha ikkilamchi savdo chegirma va bonuslari: qancha turdi, qayerda va kimga, o'zini oqladimi",
  "Строк со скидками в Linko за год нет — раздел заполнится после синхронизации заказов.":
    "Linko'da yil davomida chegirmali qatorlar yo'q — bo'lim buyurtmalar sinxronlangandan keyin to'ladi.",
  "Акция: {name}": "Aksiya: {name}",
  "Отмечено акций: {n}": "Belgilangan aksiyalar: {n}",
  "Заказы Linko загружены {time}": "Linko buyurtmalari {time} da yuklangan",
  "продаж за месяц нет": "oylik savdo yo'q",
  "{pct} к продажам за 1–{day}-е (дальше вторички пока нет)":
    "1–{day}-sana savdosiga {pct} (keyingi ikkilamchi savdo hali yo'q)",
  "{pct} к продажам": "savdoga {pct}",
  "акция": "aksiya",
  "акции": "aksiya",
  "акций": "aksiya",
  "{pct} всех скидок — поставили вручную": "barcha chegirmalarning {pct} — qo'lda qo'yilgan",
  "приёмка раньше начала или позже конца акции больше чем на 2 дня":
    "qabul aksiya boshlanishidan oldin yoki tugaganidan keyin 2 kundan ortiq",
  "заказы со скидкой отданы, но ещё не доставлены": "chegirmali buyurtmalar berilgan, lekin hali yetkazilmagan",
  "Внимание: ручные скидки выше 1% от продаж — {n}": "Diqqat: qo'lda chegirma savdoning 1% idan yuqori — {n}",
  "{pct} от продаж": "savdoning {pct}",
  "{k} из {m} мес. со скидкой": "{m} oydan {k} oy chegirma bilan",
  "и ещё {n} — в таблице «Ручные скидки — кто ставит»": "yana {n} — «Qo'lda chegirma — kim qo'yadi» jadvalida",
  "Источник — заказы Linko: доставленные, по дате приёмки магазином; сумма — скидка строки заказа (у бонуса — стоимость бонусного товара) за вычетом возвратов. Акцию строки Linko в API не отдаёт — она определяется по справочнику акций Linko: акция, действующая в день заказа, где этот товар со скидкой того же процента (или бонусный), с учётом региона в названии. Одновременные акции с теми же товарами и процентом, различающиеся только списками точек («Могул 8% \"A\" / \"S\"»), показаны одной строкой. «Без акции» — скидка, которую поставили вручную. «Вне срока» — приёмка раньше начала или позже конца акции больше чем на 2 дня. Продажи — вторичка региона и агента за месяц.":
    "Manba — Linko buyurtmalari: yetkazilganlari, do'kon qabul qilgan sana bo'yicha; summa — buyurtma qatori chegirmasi (bonusda — bonus tovar qiymati) qaytarmalar chegirilgan holda. Linko API qator aksiyasini bermaydi — u Linko aksiyalar ma'lumotnomasidan aniqlanadi: buyurtma kuni amal qilgan, shu tovar xuddi shu foizli chegirma bilan (yoki bonus) bo'lgan aksiya, nomidagi mintaqa hisobga olinadi. Bir xil tovar va foizli, faqat nuqtalar ro'yxati bilan farq qiladigan bir vaqtdagi aksiyalar («Mogul 8% \"A\" / \"S\"») bitta qatorda ko'rsatilgan. «Aksiyasiz» — qo'lda qo'yilgan chegirma. «Muddatdan tashqari» — qabul aksiya boshlanishidan oldin yoki tugaganidan keyin 2 kundan ortiq. Savdo — mintaqa va agentning oylik ikkilamchi savdosi.",
  "нажмите на акцию — отметить, можно несколько; вся страница считается по отмеченным":
    "aksiyani bosing — belgilang, bir nechtasini ham; butun sahifa belgilanganlar bo'yicha hisoblanadi",
  "Итого по отмеченным ({n})": "Belgilanganlar bo'yicha jami ({n})",
  "кг в день товаров акции в её регионах против среднего за 3 месяца до старта, с поправкой на тренд остальных товаров; продажи по {day}-е; нажмите — отметить":
    "aksiya tovarlarining uning mintaqalaridagi kunlik kg i boshlanishdan oldingi 3 oy o'rtachasiga nisbatan, boshqa tovarlar trendi hisobga olingan; savdo {day}-sanagacha; bosing — belgilang",
  "по {day}-е; нажмите на регион — агенты, на агента — его акции, на акцию — магазины":
    "{day}-sanagacha; mintaqani bosing — agentlar, agentni — uning aksiyalari, aksiyani — do'konlar",
  "Продажи по акции, АКБ по акции и затраты — без ручных скидок и по тот же день, что продажи. Доля и % затрат — к продажам региона.":
    "Aksiya savdosi, aksiya AKB si va xarajatlar — qo'lda chegirmalarsiz va savdo bilan bir kungacha. Ulush va xarajat % — mintaqa savdosiga.",
  "{agent} — акции": "{agent} — aksiyalar",
  "{promo} — {n}": "{promo} — {n}",
  "магазин": "do'kon",
  "магазина": "do'kon",
  "магазинов": "do'kon",
  "скидки без акции по агентам; % — к продажам агента за месяц; справа — последние месяцы":
    "agentlar bo'yicha aksiyasiz chegirmalar; % — agentning oylik savdosiga; o'ngda — oxirgi oylar",
  "Календарь акций по дням": "Kunlar bo'yicha aksiyalar taqvimi",
  "{n} дн. · столбец — день приёмки": "{n} kun · ustun — qabul kuni",
  "регионы → ТП": "mintaqalar → TP",
  "продажа": "savdo",
  "первые 100 из {n} по сумме": "summa bo'yicha {n} tadan birinchi 100 tasi",
  "за {m} мес.; «всегда со скидкой» — в ≥80% месяцев с закупкой была скидка ({only}); «под акцию» — ≥2 мес. без скидки, а со скидкой берёт в 2+ раза больше ({wait}); выделены месяцы со скидкой":
    "{m} oy uchun; «doim chegirma bilan» — xarid bo'lgan oylarning ≥80% ida chegirma bo'lgan ({only}); «aksiyani kutadi» — ≥2 oy chegirmasiz, chegirma bilan esa 2+ barobar ko'p oladi ({wait}); chegirmali oylar ajratilgan",
  "Скидки": "Chegirmalar",

  // Акт сверки
  "Акт сверки · Продажи": "Solishtirma dalolatnoma · Savdo",
  "Взаиморасчёты с дилерами и клиентами за месяц: остаток, отгрузка, оплаты, расходы завода":
    "Dilerlar va mijozlar bilan oylik o'zaro hisob-kitob: qoldiq, jo'natma, to'lovlar, zavod xarajatlari",
  "(по {day}-е)": "({day}-sanagacha)",
  "Оплат и остатков дилеров в OneBase ещё нет — в Linko их нет. Привяжите таблицы: «Настройки → Источники данных → Акт сверки»: «Оплаты дилеров» (Kassa Bank), «Расходы по дилерам» (Прочие расходы), «Остатки дилеров на дату» (акт бухгалтерии).":
    "OneBase'da hali dilerlar to'lovlari va qoldiqlari yo'q — Linko'da ular yo'q. Jadvallarni bog'lang: «Sozlamalar → Ma'lumot manbalari → Solishtirma dalolatnoma»: «Dilerlar to'lovlari» (Kassa Bank), «Dilerlar bo'yicha xarajatlar» (Boshqa xarajatlar), «Sanaga dilerlar qoldiqlari» (buxgalteriya dalolatnomasi).",
  "Открыть источники данных": "Ma'lumot manbalarini ochish",
  "остаток — акт бухгалтерии на {date}": "qoldiq — {date} holatiga buxgalteriya dalolatnomasi",
  "оплаты по {date}": "to'lovlar {date} gacha",
  "расходы по {date}": "xarajatlar {date} gacha",
  "отгрузки Linko по {date}": "Linko jo'natmalari {date} gacha",
  "долг контрагентов заводу на 1-е число": "kontragentlarning 1-sanaga zavodga qarzi",
  "возврат {sum}": "qaytarma {sum}",
  "касса {cash} · банк {bank}": "kassa {cash} · bank {bank}",
  "расходы завода на контрагентов {sum}": "zavodning kontragentlarga xarajatlari {sum}",
  "Остаток на конец = остаток на начало + отгрузка − возврат − касса − банк − расходы завода на контрагента. Отгрузка и возврат — первичка из Linko (перемещения на склады дилеров и заказы прямых клиентов, сумма Linko); касса и банк — реестр оплат (Kassa Bank); расходы — реестр «Прочие расходы» по статьям: 601 — ретробонус, 602 — ЗП супервайзеров, 603/604 — прочие, 606 — логистика, 609 — скидки и акции; кешбэк 2% — за отгрузки, оплаченные (FIFO) не позже нормы, ретробонус — 601 за вычетом кешбэка. Остаток на начало месяца акта бухгалтерии — опора; остальные месяцы — по цепочке.":
    "Oy oxiriga qoldiq = oy boshiga qoldiq + jo'natma − qaytarma − kassa − bank − zavodning kontragentga xarajatlari. Jo'natma va qaytarma — Linko'dan birlamchi savdo (dilerlar omborlariga ko'chirishlar va to'g'ridan-to'g'ri mijozlar buyurtmalari, Linko summasi); kassa va bank — to'lovlar reyestri (Kassa Bank); xarajatlar — «Boshqa xarajatlar» reyestri moddalar bo'yicha: 601 — retrobonus, 602 — supervayzerlar ish haqi, 603/604 — boshqalar, 606 — logistika, 609 — chegirma va aksiyalar; 2% keshbek — me'yordan kech qolmay (FIFO) to'langan jo'natmalar uchun, retrobonus — 601 dan keshbek chegirilgani. Buxgalteriya dalolatnomasidagi oy boshiga qoldiq — tayanch; qolgan oylar — zanjir bo'yicha.",
  "нажмите на строку — расшифровка: отгрузки, оплаты и расходы":
    "qatorni bosing — tafsilot: jo'natmalar, to'lovlar va xarajatlar",
  "остатка из акта бухгалтерии нет": "buxgalteriya dalolatnomasida qoldiq yo'q",
  "остаток на начало {open} → на конец {close}": "oy boshiga qoldiq {open} → oxiriga {close}",
  "норма оплаты {n} дн.": "to'lov me'yori {n} kun",
  "Отгрузки и возвраты (Linko)": "Jo'natma va qaytarmalar (Linko)",
  "Оплаты (Kassa Bank)": "To'lovlar (Kassa Bank)",
  "Расходы (реестр)": "Xarajatlar (reyestr)",
  "Кешбэк 2% по FIFO (норма {n} дн.)": "FIFO bo'yicha 2% keshbek (me'yor {n} kun)",
  "Кешбэк больше проведённого по 601 на {sum} — разница в расходах акта.":
    "Keshbek 601 bo'yicha o'tkazilgandan {sum} ko'p — farq dalolatnoma xarajatlarida.",
  "перемещение № {n}": "ko'chirish № {n}",
  "заказ № {n}": "buyurtma № {n}",
  "Расходы за {month} в реестр ещё не внесены (последняя строка — {date}): расходы месяца — только кешбэк по FIFO.":
    "{month} xarajatlari reyestrga hali kiritilmagan (oxirgi qator — {date}): oy xarajatlari — faqat FIFO bo'yicha keshbek.",
  "Реестр расходов внесён по {date}: расходы после этой даты в акт ещё не попали.":
    "Xarajatlar reyestri {date} gacha kiritilgan: bu sanadan keyingi xarajatlar dalolatnomaga hali tushmagan.",
  "Без остатка из акта бухгалтерии — остаток считается от начала реестров (может отличаться): {n}.":
    "Buxgalteriya dalolatnomasidan qoldiqsiz — qoldiq reyestrlar boshidan hisoblanadi (farq qilishi mumkin): {n}.",
  "Отгрузки Linko, а в таблицах контрагента с таким именем нет (оплаты и остаток не найдены): {n}.":
    "Linko jo'natmalari bor, jadvallarda esa bunday nomli kontragent yo'q (to'lov va qoldiq topilmadi): {n}.",
  "Оплаты есть, а отгрузок Linko под этим именем нет (контрагент таблиц не сопоставлен с дилером региона или клиентом): {n}.":
    "To'lovlar bor, bu nom bilan Linko jo'natmalari yo'q (jadvaldagi kontragent mintaqa dileri yoki mijoz bilan solishtirilmagan): {n}.",
  "Отгрузки или оплаты месяца у контрагентов, которых нет в акте бухгалтерии (в акт не входят): {n}.":
    "Buxgalteriya dalolatnomasida yo'q kontragentlarning oylik jo'natma yoki to'lovlari (dalolatnomaga kirmaydi): {n}.",
  "Кешбэк по FIFO больше, чем проведено по статье 601 (разница добавлена в расходы акта): {n}.":
    "FIFO bo'yicha keshbek 601 moddasi bo'yicha o'tkazilgandan ko'p (farq dalolatnoma xarajatlariga qo'shilgan): {n}.",

  // DSO
  "DSO · Продажи": "DSO · Savdo",
  "Долги дилеров, просрочка и когда ждать оплату": "Dilerlar qarzlari, muddati o'tganlar va to'lovni qachon kutish kerak",
  "Оплат дилеров в OneBase ещё нет — в Linko их нет. Привяжите таблицу оплат: «Настройки → Источники данных → DSO»: «Оплаты дилеров» (Kassa Bank), «Условия оплаты дилеров» (Ostatka), «Расходы по дилерам» (Прочие расходы).":
    "OneBase'da hali dilerlar to'lovlari yo'q — Linko'da ular yo'q. To'lovlar jadvalini bog'lang: «Sozlamalar → Ma'lumot manbalari → DSO»: «Dilerlar to'lovlari» (Kassa Bank), «Dilerlar to'lov me'yorlari» (Ostatka), «Dilerlar bo'yicha xarajatlar» (Boshqa xarajatlar).",
  "отгрузки Linko и оплаты по {date}": "Linko jo'natmalari va to'lovlar {date} gacha",
  "последняя оплата в реестре — {date}": "reyestrdagi oxirgi to'lov — {date}",
  "дни считаются от даты данных": "kunlar ma'lumotlar sanasidan hisoblanadi",
  "Дебиторка (ДЗ)": "Debitorlik qarzi (DQ)",
  "{n} неоплаченных отгрузок · {m} дилеров": "{n} ta to'lanmagan jo'natma · {m} ta diler",
  "Просрочено (ПДЗ)": "Muddati o'tgan (MDQ)",
  "{share} долга дилеров со сроком · {n} дилеров": "muddatli dilerlar qarzining {share} · {n} ta diler",
  "по норме; по темпу оплат ждём {sum}": "me'yor bo'yicha; to'lov sur'ati bo'yicha {sum} kutamiz",
  "Оплачено с {date}": "{date} dan to'langan",
  "отгружено {sum}": "jo'natilgan {sum}",
  "Отгрузки и возвраты — первичка из Linko (перемещения на склады дилеров и заказы прямых клиентов, сумма Linko), оплаты — реестр Kassa Bank (касса и банк), норма оплаты — лист Ostatka (не задана — {n} дн., «*»). Оплаты, возвраты и «Прочие расходы» гасят самые старые отгрузки (FIFO); долг на дату акта бухгалтерии раскладывается на последние отгрузки до неё. Просрочка — отгрузка + норма уже прошла. «По темпу оплат» — отгрузка + фактический DSO дилера: средний срок, за который он закрывал отгрузки последние {d} дн. (взвешен по сумме). Дилеры без отгрузок дольше 60 дней — «не работают с нами»: долг к взысканию, в прогноз не входит.":
    "Jo'natma va qaytarmalar — Linko'dan birlamchi savdo (dilerlar omborlariga ko'chirishlar va to'g'ridan-to'g'ri mijozlar buyurtmalari, Linko summasi), to'lovlar — Kassa Bank reyestri (kassa va bank), to'lov me'yori — Ostatka varag'i (berilmagan — {n} kun, «*»). To'lovlar, qaytarmalar va «Boshqa xarajatlar» eng eski jo'natmalarni yopadi (FIFO); buxgalteriya dalolatnomasi sanasidagi qarz undan oldingi oxirgi jo'natmalarga taqsimlanadi. Muddat o'tishi — jo'natma + me'yor o'tib ketgan. «To'lov sur'ati bo'yicha» — jo'natma + dilerning haqiqiy DSO si: oxirgi {d} kunda jo'natmalarni yopgan o'rtacha muddati (summa bo'yicha tortilgan). 60 kundan ortiq jo'natmasiz dilerlar — «biz bilan ishlamaydi»: qarz undiriladi, prognozga kirmaydi.",
  "договор без нормы — просрочки нет, ожидаемая дата — по их темпу оплат":
    "me'yorsiz shartnoma — muddat o'tishi yo'q, kutilayotgan sana — ularning to'lov sur'ati bo'yicha",
  "отгрузок больше 60 дней нет — долг к взысканию, в прогноз не входит":
    "60 kundan ortiq jo'natma yo'q — qarz undiriladi, prognozga kirmaydi",
  "Норма, дн.": "Me'yor, kun",
  "Факт DSO, дн.": "Haqiqiy DSO, kun",
  "Долг": "Qarz",
  "Старая отгрузка": "Eski jo'natma",
  "Ближайший срок": "Eng yaqin muddat",
  "Ожидаем оплату": "To'lovni kutamiz",
  "Последняя оплата": "Oxirgi to'lov",
  "нажмите на дилера — неоплаченные отгрузки и когда ждать оплату; * — норма не задана, взята {n} дн.":
    "dilerni bosing — to'lanmagan jo'natmalar va to'lovni qachon kutish; * — me'yor berilmagan, {n} kun olingan",
  "{date}, {n} дн.": "{date}, {n} kun",
  "{n} дн. назад": "{n} kun oldin",
  "отгр. {sum}": "jo'n. {sum}",
  "Срок по норме": "Me'yor bo'yicha muddat",
  "Просрочка, дн.": "Muddat o'tishi, kun",
  "{date} · задерживается": "{date} · kechikmoqda",
  "По норме": "Me'yor bo'yicha",
  "По темпу оплат": "To'lov sur'ati bo'yicha",
  "по норме — срок по договору; по темпу — по фактическому DSO дилера":
    "me'yor bo'yicha — shartnomadagi muddat; sur'at bo'yicha — dilerning haqiqiy DSO si bo'yicha",
  "Итого по норме {sum}": "Me'yor bo'yicha jami {sum}",
  "по темпу {sum}: касса {cash}, банк {bank}": "sur'at bo'yicha {sum}: kassa {cash}, bank {bank}",
  "по норме": "me'yor bo'yicha",
  "по темпу": "sur'at bo'yicha",
  "Поступило": "Tushgan",
  "Поступило − отгружено": "Tushgan − jo'natilgan",
  "Поступило / отгружено": "Tushgan / jo'natilgan",
  "Отгрузки и поступления по каналам": "Kanallar bo'yicha jo'natma va tushumlar",
  "{month}, 1–{day} число: отгрузки Linko и оплаты Kassa Bank":
    "{month}, 1–{day}-sana: Linko jo'natmalari va Kassa Bank to'lovlari",
  "По число": "Sanagacha",
  "по {day}-е": "{day}-sanagacha",
  "Оплаты по месяцам: касса и банк": "Oylar bo'yicha to'lovlar: kassa va bank",
  "Kassa Bank — поступления от контрагентов; синим — касса (наличные)":
    "Kassa Bank — kontragentlardan tushumlar; ko'k — kassa (naqd)",
  "по {date}": "{date} gacha",
  "Всё с {month}": "{month} dan beri hammasi",
  "Оплаты по контрагентам: касса и банк": "Kontragentlar bo'yicha to'lovlar: kassa va bank",
  "С начала {year}": "{year} boshidan",
  "Неоплаченные отгрузки": "To'lanmagan jo'natmalar",
  "дилеры со сроком, по сроку оплаты; красным — срок по норме прошёл":
    "muddatli dilerlar, to'lov muddati bo'yicha; qizil — me'yor bo'yicha muddat o'tgan",
};

export default dict;
