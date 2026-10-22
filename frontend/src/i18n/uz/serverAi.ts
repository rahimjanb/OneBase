import type { Dict } from "../core";

/**
 * Тексты, которые приходят с сервера: ключ — русский текст сервера, значения в нём — {подстановки} (перевод по шаблону).
 * Раздел AI: консультант, AI-сотрудники, AI Dashboard, база знаний, голос, AI Data Manager, «Настройки → AI».
 * Порядок важен для шаблонов: длинный (уточнённый) шаблон стоит раньше короткого, который тоже узнал бы этот текст.
 * Ответы моделей не переводятся — они и так приходят на языке интерфейса.
 */
const dict: Dict = {
  // ── Консультант: готовность, ошибки чата ──
  "Главный консультант выключен администратором в «Настройки → AI → Агенты».":
    "Bosh maslahatchini administrator «Sozlamalar → AI → Agentlar» bo'limida o'chirib qo'ygan.",
  "Чат не найден.": "Chat topilmadi.",
  "Ответ не найден.": "Javob topilmadi.",
  "Название не может быть пустым.": "Nom bo'sh bo'lishi mumkin emas.",
  "Вопрос — от 1 до {n} символов.": "Savol — 1 dan {n} gacha belgi.",
  "Модель вернула пустой ответ.": "Model bo'sh javob qaytardi.",
  "Ответ не получен: время ожидания истекло или сервер останавливается.": "Javob olinmadi: kutish vaqti tugadi yoki server to'xtamoqda.",
  "Не удалось получить ответ из-за внутренней ошибки OneBase. Подробности — в журнале сервера.":
    "OneBase ichki xatosi tufayli javob olinmadi. Tafsilotlar — server jurnalida.",
  "Внутренняя ошибка OneBase. Подробности — в журнале сервера.": "OneBase ichki xatosi. Tafsilotlar — server jurnalida.",

  // Ход ответа: пометки шагов и выбор AI-сотрудников
  "прошлых анализов в чате: {n}": "chatdagi oldingi tahlillar: {n}",
  "без AI-сотрудников": "AI xodimlarsiz",
  "модель недоступна": "model mavjud emas",
  "Формирую отчёт Excel": "Excel hisoboti tayyorlanmoqda",
  "Нет доступных AI-сотрудников.": "Mavjud AI xodimlar yo'q.",
  "Выбор по ключевым словам вопроса (маршрутизатор не ответил разборчиво).":
    "Savoldagi kalit so'zlar bo'yicha tanlandi (marshrutlovchi tushunarli javob bermadi).",

  // AI-сотрудник не отработал
  "AI-сотрудника «{code}» нет.": "«{code}» AI xodimi yo'q.",
  "AI-сотрудник выключен в настройках AI.": "AI xodim AI sozlamalarida o'chirilgan.",
  "У вас нет доступа к данным этого отдела.": "Sizda bu bo'lim ma'lumotlariga ruxsat yo'q.",
  "Превышен лимит шагов агента.": "Agent qadamlari limitidan oshib ketdi.",
  "Агент не завершил анализ за отведённое число шагов.": "Agent ajratilgan qadamlar sonida tahlilni tugatmadi.",
  "Внутренняя ошибка OneBase при работе сотрудника.": "Xodim ishlayotganda OneBase ichki xatosi yuz berdi.",

  // ── AI-сотрудники по умолчанию ──
  "Главный консультант": "Bosh maslahatchi",
  "CEO-консультант": "CEO-maslahatchi",
  "Понимает вопрос, решает, каких AI-сотрудников привлечь, и собирает их результаты в единый управленческий ответ.":
    "Savolni tushunadi, qaysi AI xodimlarni jalb qilishni hal qiladi va ularning natijalarini yagona boshqaruv javobiga jamlaydi.",
  "Финансовый директор": "Moliya direktori",
  "Финансы: выручка, оплаты, расходы, прибыль, бюджеты, финансовые KPI и риски.":
    "Moliya: tushum, to'lovlar, xarajatlar, foyda, byudjetlar, moliyaviy KPI va xatarlar.",
  "Директор по продажам": "Savdo direktori",
  "Продажи: факт и план, регионы, агенты, клиенты (АКБ), товары и категории, динамика и причины снижения.":
    "Savdo: fakt va reja, mintaqalar, agentlar, mijozlar (AKB), tovarlar va toifalar, dinamika va pasayish sabablari.",
  "Директор по маркетингу": "Marketing direktori",
  "Маркетинг: кампании, лиды, каналы, стоимость привлечения; продажи товаров и категорий как результат.":
    "Marketing: kampaniyalar, lidlar, kanallar, jalb qilish narxi; natija sifatida tovarlar va toifalar savdosi.",
  "HR-директор": "HR direktori",
  "HR: сотрудники, подразделения, вакансии, нагрузка, эффективность и текучесть.":
    "HR: xodimlar, bo'linmalar, bo'sh ish o'rinlari, ish yuki, samaradorlik va kadrlar qo'nimsizligi.",
  "Директор по производству": "Ishlab chiqarish direktori",
  "Производство: план и факт выпуска, простои, загрузка; отгрузки завода и остатки как косвенные данные.":
    "Ishlab chiqarish: ishlab chiqarish rejasi va fakti, to'xtab qolishlar, yuklama; bilvosita ma'lumot sifatida zavod jo'natmalari va qoldiqlar.",
  "Директор по снабжению": "Ta'minot direktori",
  "Снабжение: остатки, дефицит и затоварка, поставщики, закупки и цены закупки.":
    "Ta'minot: qoldiqlar, taqchillik va ortiqcha zaxira, yetkazib beruvchilar, xaridlar va xarid narxlari.",

  // Права пользователя (кому доступен сотрудник)
  "Журнал аудита": "Audit jurnali",

  // ── Источники знаний («Настройки → AI → Агенты») ──
  "Продажи (вторичка)": "Savdo (ikkilamchi)",
  "Факт продаж в кг и сумах, возвраты, АКБ, категории и SKU — по республике, регионам, агентам, торговым точкам.":
    "Savdo fakti kg va so'mda, qaytarishlar, AKB, toifalar va SKU — respublika, mintaqalar, agentlar, savdo nuqtalari bo'yicha.",
  "Продажи: обзор, республика, регионы, агенты, ассортимент, проблемные агенты":
    "Savdo: umumiy ko'rinish, respublika, mintaqalar, agentlar, assortiment, muammoli agentlar",
  "Визиты торговых представителей, визиты с заказом и без, конверсия (страйк).":
    "Savdo vakillarining tashriflari, buyurtmali va buyurtmasiz tashriflar, konversiya (strayk).",
  "Продажи: агенты, проблемные агенты": "Savdo: agentlar, muammoli agentlar",
  "Планы продаж": "Savdo rejalari",
  "План РОП регионов по категориям (без него на месяц — сумма планов ТП), планы ТП из API планов Linko (вес, АКБ, сумма), выполнение и прогноз.":
    "Mintaqalarning toifalar bo'yicha ROP rejasi (u bo'lmasa — oy uchun TP rejalari yig'indisi), Linko rejalar API sidan TP rejalari (vazn, AKB, summa), bajarilish va prognoz.",
  "Продажи: планы, регионы": "Savdo: rejalar, mintaqalar",
  "Команда продаж": "Savdo jamoasi",
  "Торговые представители и их должности, регионы, вакансии оргструктуры продаж.":
    "Savdo vakillari va ularning lavozimlari, mintaqalar, savdo tuzilmasidagi bo'sh ish o'rinlari.",
  "Продажи: регионы, команда": "Savdo: mintaqalar, jamoa",
  "Отгрузки завода дилерам и точкам завода (базары, сети) в кг, коробках и сумах, нетто возвратов, план первички.":
    "Zavodning dilerlarga va zavod nuqtalariga (bozorlar, tarmoqlar) jo'natmalari kg, quti va so'mda, qaytarishlar chegirilgan holda, birlamchi reja.",
  "Продажи: первичка": "Savdo: birlamchi",
  "Остатки на складах": "Omborlardagi qoldiqlar",
  "Остатки на складах регионов и завода, дни покрытия, дефицит и затоварка.":
    "Mintaqalar va zavod omborlaridagi qoldiqlar, qoplash kunlari, taqchillik va ortiqcha zaxira.",
  "Продажи: остатки": "Savdo: qoldiqlar",
  "Sales Base: полевая команда": "Sales Base: dala jamoasi",
  "Работа агентов за день: заказы против плана, визиты, маршруты, задачи, статусы и проблемы агентов, рекомендации AI-планирования.":
    "Agentlarning kunlik ishi: rejaga nisbatan buyurtmalar, tashriflar, marshrutlar, vazifalar, agentlar holati va muammolari, AI rejalashtirish tavsiyalari.",
  "Sales Base: дашборд команды": "Sales Base: jamoa dashbordi",
  "Оплаты": "To'lovlar",
  "Оплаты торговых точек из Linko: наличные и банк, принятые и нет. Расходов, прибыли и бюджетов в OneBase нет.":
    "Linko dan savdo nuqtalari to'lovlari: naqd va bank, qabul qilingan va qilinmagan. OneBase da xarajatlar, foyda va byudjetlar yo'q.",
  "Справочник поставщиков Linko (только названия; закупок и цен закупки в OneBase нет).":
    "Linko yetkazib beruvchilar ma'lumotnomasi (faqat nomlari; OneBase da xaridlar va xarid narxlari yo'q).",
  "Документы, загруженные в базу знаний AI: регламенты, отчёты, выгрузки Excel и CSV.":
    "AI bilimlar bazasiga yuklangan hujjatlar: reglamentlar, hisobotlar, Excel va CSV eksportlari.",
  "Настройки → AI → База знаний": "Sozlamalar → AI → Bilimlar bazasi",

  // ── Инструменты AI-сотрудников: названия (они же пометки шагов консультанта) и описания ──
  "Sales Base: работа команды за день": "Sales Base: jamoaning kunlik ishi",
  "Полевые продажи (Sales Base) за день: заказы агентов против дневного плана, выполнение месячного плана, визиты, маршруты (посещено/пропущено), задачи (открыто/просрочено), статус каждого агента (не начал/на маршруте/завершил/проблема) и блок «требует внимания», а также рекомендации AI, ждущие решения (падение продаж точек, давно не посещали, отставание от плана). Вопросы: «почему команда не выполняет план», «кто хуже выполняет маршрут», «какие точки требуют визита», «кому дать больше точек».":
    "Dala savdosi (Sales Base) kun bo'yicha: agentlar buyurtmalari kunlik rejaga nisbatan, oylik reja bajarilishi, tashriflar, marshrutlar (borildi/o'tkazib yuborildi), vazifalar (ochiq/muddati o'tgan), har bir agent holati (boshlamagan/marshrutda/tugatgan/muammo) va «e'tibor talab qiladi» bloki, shuningdek qaror kutayotgan AI tavsiyalari (nuqtalar savdosi pasayishi, uzoq vaqt borilmagan, rejadan orqada qolish). Savollar: «nega jamoa rejani bajarmayapti», «kim marshrutni yomonroq bajaryapti», «qaysi nuqtalarga tashrif kerak», «kimga ko'proq nuqta berish kerak».",
  "Поиск по документам базы знаний компании (регламенты, отчёты, выгрузки Excel и CSV), доступным пользователю: возвращает подходящие фрагменты с названием документа. Цитируй фрагменты и называй документ как источник.":
    "Kompaniya bilimlar bazasidagi foydalanuvchiga ochiq hujjatlar (reglamentlar, hisobotlar, Excel va CSV eksportlari) bo'yicha qidiruv: mos fragmentlarni hujjat nomi bilan qaytaradi. Fragmentlardan iqtibos keltiring va hujjatni manba sifatida ko'rsating.",
  "Аутсток: дни без товара и упущенные продажи": "Autstok: tovarsiz kunlar va boy berilgan savdo",
  "Аутсток за месяц: сколько недопродали из-за того, что у дилера не было товара (остаток по дням восстановлен назад от снимка Linko). Итоги и «к факту» (упущенные кг к проданным), потери по категориям, по регионам с дилером и тройкой самых дорогих дыр, по товарам, ядро потерь (пары «товар × регион», дающие 80% упущенного), хронические пары, чья потеря — недовоз дилеру (на заводе товар был) или завод. По умолчанию — ТОП-товары; scope=all — все SKU. По республике или региону.":
    "Oy uchun autstok: dilerda tovar bo'lmagani sababli qancha kam sotilgani (kunlik qoldiq Linko suratidan orqaga qarab tiklangan). Yakunlar va «faktga nisbatan» (boy berilgan kg sotilganga nisbatan), toifalar bo'yicha, diler va eng qimmat uchta bo'shliq ko'rsatilgan mintaqalar bo'yicha, tovarlar bo'yicha yo'qotishlar, yo'qotishlar yadrosi (boy berilganning 80% ini beradigan «tovar × mintaqa» juftliklari), surunkali juftliklar — yo'qotish dilerga yetkazilmaganidanmi (zavodda tovar bor edi) yoki zavoddanmi. Standart — TOP tovarlar; scope=all — barcha SKU. Respublika yoki mintaqa bo'yicha.",
  "Поиск товара или категории": "Tovar yoki toifani qidirish",
  "Найти категорию, тип или товар (SKU) по названию, части названия, бренду или артикулу — кириллицей или латиницей («помадка», «POMADKA», «Yamelly», «130»). Возвращает продажи найденного за месяц по республике или региону: у категории — итоги и её SKU со статусами, у товара — факт, ТТ с товаром, дистрибуция, прошлый месяц и разбивка по регионам. Вызывай всегда, когда в вопросе есть название товара, категории или бренда.":
    "Toifa, tur yoki tovarni (SKU) nomi, nomining bir qismi, brendi yoki artikuli bo'yicha topish — kirill yoki lotin yozuvida («помадка», «POMADKA», «Yamelly», «130»). Topilgan narsaning oylik savdosini respublika yoki mintaqa bo'yicha qaytaradi: toifada — yakunlar va uning holatlari bilan SKU lari, tovarda — fakt, tovar bor SN, distributsiya, o'tgan oy va mintaqalar bo'yicha taqsimot. Savolda tovar, toifa yoki brend nomi bo'lsa, har doim chaqiring.",
  "Итоги продаж за месяц": "Oylik savdo yakunlari",
  "Итоги вторичных продаж за месяц по республике или региону: факт и план в кг, выполнение, прогноз на конец месяца, выручка в сумах, АКБ (активные торговые точки), страйк, визиты, число ТП; сравнение с теми же днями прошлого месяца; экспорт и опт (филиал «Завод») отдельно.":
    "Respublika yoki mintaqa bo'yicha ikkilamchi savdoning oylik yakunlari: kg da fakt va reja, bajarilish, oy oxiriga prognoz, so'mda tushum, AKB (faol savdo nuqtalari), strayk, tashriflar, TP soni; o'tgan oyning shu kunlari bilan solishtirish; eksport va ulgurji («Zavod» filiali) alohida.",
  "Продажи по регионам": "Mintaqalar bo'yicha savdo",
  "Продажи по регионам (филиалам) за месяц: план, факт, выполнение и прогноз в кг, выручка, АКБ, страйк, число ТП, проблемные агенты. Отсортировано по прогнозу выполнения плана — сначала отстающие.":
    "Mintaqalar (filiallar) bo'yicha oylik savdo: kg da reja, fakt, bajarilish va prognoz, tushum, AKB, strayk, TP soni, muammoli agentlar. Reja bajarilishi prognozi bo'yicha saralangan — avval orqada qolganlar.",
  "Продажи по торговым представителям": "Savdo vakillari bo'yicha savdo",
  "Торговые представители (ТП): план и факт в кг, выполнение, прогноз, выручка, визиты, заказы, страйк, категории, замечания. С регионом — вся команда региона; без региона — лучшие и худшие ТП республики по прогнозу выполнения.":
    "Savdo vakillari (TP): kg da reja va fakt, bajarilish, prognoz, tushum, tashriflar, buyurtmalar, strayk, toifalar, e'tirozlar. Mintaqa bilan — mintaqaning butun jamoasi; mintaqasiz — bajarilish prognozi bo'yicha respublikaning eng yaxshi va eng yomon TP lari.",
  "ТП с замечаниями за месяц (относительно медианы своего региона): низкий страйк, визиты без продаж, малый чек, узкий ассортимент, падение темпа. У каждого — регион, страйк, визиты, выручка и объяснение замечаний.":
    "Oy davomida e'tirozli TP lar (o'z mintaqasi medianasiga nisbatan): past strayk, sotuvsiz tashriflar, kichik chek, tor assortiment, sur'at pasayishi. Har birida — mintaqa, strayk, tashriflar, tushum va e'tirozlar izohi.",
  "Товары и категории": "Tovarlar va toifalar",
  "Ассортимент за месяц по республике или региону: все категории (кг, выручка, доля, АКБ, дистрибуция, изменение к прошлому месяцу, «молчащие» и пропавшие SKU), лучшие и худшие товары по выручке, пропавшие SKU. Конкретный товар, категорию или бренд по названию — find_products.":
    "Respublika yoki mintaqa bo'yicha oylik assortiment: barcha toifalar (kg, tushum, ulush, AKB, distributsiya, o'tgan oyga nisbatan o'zgarish, «jim» va yo'qolgan SKU), tushum bo'yicha eng yaxshi va eng yomon tovarlar, yo'qolgan SKU. Aniq tovar, toifa yoki brend nomi bo'yicha — find_products.",
  "Клиенты (АКБ)": "Mijozlar (AKB)",
  "Клиенты — торговые точки: АКБ по месяцам года (точки с чистой покупкой > 0), точки, которые покупали в прошлом месяце и не купили в этом («молчат», с выручкой прошлого месяца), новые точки — по республике или по регионам.":
    "Mijozlar — savdo nuqtalari: yil oylari bo'yicha AKB (sof xaridi > 0 bo'lgan nuqtalar), o'tgan oy xarid qilib, bu oy xarid qilmagan nuqtalar («jim», o'tgan oy tushumi bilan), yangi nuqtalar — respublika yoki mintaqalar bo'yicha.",
  "Визиты ТП за месяц по регионам: план визитов, выполненные (в плане и вне), заказы с визитов и их сумма, непосещённые точки, страйк (заказы ТП, принятые в месяце ÷ выполненные визиты ТП; бывает больше 100%), визиты без заказа (визиты − заказы), визиты не ТП отдельно.":
    "Mintaqalar bo'yicha TP larning oylik tashriflari: tashriflar rejasi, bajarilganlari (rejadagi va rejadan tashqari), tashriflardan buyurtmalar va ularning summasi, borilmagan nuqtalar, strayk (oyda qabul qilingan TP buyurtmalari ÷ TP ning bajarilgan tashriflari; 100% dan oshishi mumkin), buyurtmasiz tashriflar (tashriflar − buyurtmalar), TP bo'lmaganlar tashriflari alohida.",
  "Планы и KPI продаж": "Savdo rejalari va KPI",
  "Планы продаж из Linko за месяц и их выполнение: план и факт по весу и выручке — всего и по регионам, число ТП с планом, KPI-показатели.":
    "Linko dan oylik savdo rejalari va ularning bajarilishi: vazn va tushum bo'yicha reja va fakt — jami va mintaqalar bo'yicha, rejasi bor TP soni, KPI ko'rsatkichlari.",
  "Динамика продаж по месяцам": "Oylar bo'yicha savdo dinamikasi",
  "Продажи по месяцам — для сравнения периодов: факт и план в кг, выполнение, выручка, АКБ, страйк за последние N месяцев (по республике или региону). Последний месяц может быть неполным — смотри dataThrough.":
    "Oylar bo'yicha savdo — davrlarni solishtirish uchun: so'nggi N oy uchun kg da fakt va reja, bajarilish, tushum, AKB, strayk (respublika yoki mintaqa bo'yicha). Oxirgi oy to'liq bo'lmasligi mumkin — dataThrough ga qarang.",
  "Рекомендуемый остаток на складах": "Omborlardagi tavsiya etilgan qoldiq",
  "Рекомендуемый остаток: запас на складах регионов (кг, коробки, стоимость по входной цене дилера) и скорость продаж за 90 дней: дни покрытия, запас на 15 и 30 дней, дефицит (меньше 15 дней продаж), затоварка (больше 30 дней), «не продаётся» (остаток есть, продаж нет); отдельно — склад завода (его запас меряется скоростью всей страны). Можно по одному региону.":
    "Tavsiya etilgan qoldiq: mintaqa omborlaridagi zaxira (kg, quti, dilerning kirim narxi bo'yicha qiymati) va 90 kunlik savdo tezligi: qoplash kunlari, 15 va 30 kunlik zaxira, taqchillik (15 kunlik savdodan kam), ortiqcha zaxira (30 kundan ko'p), «sotilmaydi» (qoldiq bor, savdo yo'q); alohida — zavod ombori (uning zaxirasi butun mamlakat tezligi bilan o'lchanadi). Bitta mintaqa bo'yicha ham mumkin.",
  "Первичка: отгрузки завода дилерам": "Birlamchi: zavodning dilerlarga jo'natmalari",
  "Первичка — отгрузки завода дилерам и точкам завода (базары, сети, фирменный магазин): за месяц (кг, коробки, сумма дилера — по ней дилер берёт товар у завода, и продажа магазинам — по прайсу, по которому дилер продаёт дальше), прогноз по дате данных, план первички (если загружен в OneBase), по месяцам года, с начала года, по контрагентам и категориям, возвраты (уже вычтены). Это спрос дилеров, а не выпуск производства.":
    "Birlamchi — zavodning dilerlarga va zavod nuqtalariga (bozorlar, tarmoqlar, firma do'koni) jo'natmalari: oy uchun (kg, quti, diler summasi — diler tovarni zavoddan shu narxda oladi, va do'konlarga sotuv — diler keyin sotadigan prays bo'yicha), ma'lumotlar sanasi bo'yicha prognoz, birlamchi reja (OneBase ga yuklangan bo'lsa), yil oylari bo'yicha, yil boshidan, kontragentlar va toifalar bo'yicha, qaytarishlar (allaqachon chegirilgan). Bu ishlab chiqarish hajmi emas, dilerlar talabi.",
  "Оплаты торговых точек": "Savdo nuqtalari to'lovlari",
  "Оплаты торговых точек из Linko по месяцам: сумма и число оплат, наличные и банк, принятые и не принятые; за выбранный месяц — по филиалам. Это поступления от клиентов, а не расходы и не прибыль.":
    "Linko dan oylar bo'yicha savdo nuqtalari to'lovlari: to'lovlar summasi va soni, naqd va bank, qabul qilingan va qilinmagan; tanlangan oy uchun — filiallar bo'yicha. Bu mijozlardan tushumlar, xarajat ham, foyda ham emas.",
  "Справочник поставщиков из Linko (только названия). Заказов поставщикам, закупочных цен и сроков поставки в OneBase нет.":
    "Linko dan yetkazib beruvchilar ma'lumotnomasi (faqat nomlari). OneBase da yetkazib beruvchilarga buyurtmalar, xarid narxlari va yetkazib berish muddatlari yo'q.",

  // Аргументы инструментов («Настройки → AI → Инструменты»)
  "Год, например 2026. Без года — год последних данных.": "Yil, masalan 2026. Yilsiz — oxirgi ma'lumotlar yili.",
  "Месяц 1–12. Без месяца — месяц последних данных (текущий).": "Oy 1–12. Oysiz — oxirgi ma'lumotlar oyi (joriy).",
  "Регион: id или название (например «Самарканд»). Без региона — вся республика.":
    "Mintaqa: id yoki nomi (masalan «Самарканд»). Mintaqasiz — butun respublika.",
  "День yyyy-MM-dd. Без даты — сегодня.": "Kun yyyy-MM-dd. Sanasiz — bugun.",
  "Команда: название (например «Ташкент»). Без команды — вся зона пользователя.":
    "Jamoa: nomi (masalan «Ташкент»). Jamoasiz — foydalanuvchining butun hududi.",
  "Что искать — ключевые слова или вопрос.": "Nimani qidirish — kalit so'zlar yoki savol.",
  "Сколько фрагментов вернуть (по умолчанию 6, до 12).": "Nechta fragment qaytarish (standart 6, 12 gacha).",
  "Какие товары: «top» — только ТОП-товары (так по умолчанию и на странице), «all» — все SKU, «rest» — кроме ТОПа.":
    "Qaysi tovarlar: «top» — faqat TOP tovarlar (standart va sahifadagidek), «all» — barcha SKU, «rest» — TOP dan tashqari.",
  "Что искать: название или часть названия категории, типа, товара, бренда или артикул.":
    "Nimani qidirish: toifa, tur, tovar, brend nomi yoki uning bir qismi yoxud artikul.",
  "Сколько лучших и худших ТП вернуть без региона (по умолчанию 10, до 30).":
    "Mintaqasiz nechta eng yaxshi va eng yomon TP qaytarish (standart 10, 30 gacha).",
  "Сколько последних месяцев (по умолчанию 6, до 12).": "So'nggi necha oy (standart 6, 12 gacha).",

  // Ошибки инструментов (пробный запуск в настройках)
  "Аргумент {name} должен быть целым числом.": "{name} argumenti butun son bo'lishi kerak.",
  "Аргумент {name} — от {min} до {max}.": "{name} argumenti — {min} dan {max} gacha.",
  "Данные Sales Base отдаются только от имени пользователя.": "Sales Base ma'lumotlari faqat foydalanuvchi nomidan beriladi.",
  "Обзор команды доступен супервайзеру и РМ.": "Jamoa sharhi supervayzer va RM uchun ochiq.",
  "Нужен аргумент query — что искать.": "query argumenti kerak — nimani qidirish.",
  "Поиск по базе знаний выполняется только от имени пользователя.": "Bilimlar bazasidan qidiruv faqat foydalanuvchi nomidan bajariladi.",
  "Укажите query — название или часть названия товара или категории.": "query ni ko'rsating — tovar yoki toifa nomi yoxud uning bir qismi.",
  "Данных о продажах за {month} {year} в OneBase нет. Есть: {list}.": "OneBase da {month} {year} uchun savdo ma'lumotlari yo'q. Bor: {list}.",
  "Региона «{region}» нет. Регионы: {list}.": "«{region}» mintaqasi yo'q. Mintaqalar: {list}.",
  "Склада региона «{region}» нет. Склады регионов: {list}.": "«{region}» mintaqasi ombori yo'q. Mintaqa omborlari: {list}.",
  "Оплат за {month} {year} нет. Оплаты есть с {fromMonth} {fromYear} по {toMonth} {toYear}.":
    "{month} {year} uchun to'lovlar yo'q. To'lovlar {fromMonth} {fromYear} dan {toMonth} {toYear} gacha bor.",
  "У вас нет права на данные этого инструмента.": "Sizda bu vosita ma'lumotlariga huquq yo'q.",

  // ── Источники данных ответа (чипы «Источники:») — уточнённые шаблоны раньше общего «OneBase → Продажи → {region}» ──
  "OneBase → Продажи → Республика": "OneBase → Savdo → Respublika",
  "OneBase → Продажи → Республика → Регионы": "OneBase → Savdo → Respublika → Mintaqalar",
  "OneBase → Продажи → Республика → Визиты": "OneBase → Savdo → Respublika → Tashriflar",
  "OneBase → Продажи → Республика по месяцам": "OneBase → Savdo → Respublika oylar bo'yicha",
  "OneBase → Продажи → Регионы → Команда ТП": "OneBase → Savdo → Mintaqalar → TP jamoasi",
  "OneBase → Продажи → Проблемные агенты": "OneBase → Savdo → Muammoli agentlar",
  "OneBase → Продажи → Ассортимент": "OneBase → Savdo → Assortiment",
  "OneBase → Продажи → Планы": "OneBase → Savdo → Rejalar",
  "OneBase → Продажи → Рек. остаток": "OneBase → Savdo → Tavsiya zaxira",
  "OneBase → Продажи → Первичка": "OneBase → Savdo → Birlamchi",
  "OneBase → Продажи → Аутсток": "OneBase → Savdo → Autstok",
  "Linko → Оплаты (зеркало OneBase)": "Linko → To'lovlar (OneBase nusxasi)",
  "Linko → Поставщики (зеркало OneBase)": "Linko → Yetkazib beruvchilar (OneBase nusxasi)",
  "Sales Base → Дашборд команды": "Sales Base → Jamoa dashbordi",
  "OneBase → Продажи → Ассортимент → {name}": "OneBase → Savdo → Assortiment → {name}",
  "OneBase → Продажи → Аутсток → {scope}": "OneBase → Savdo → Autstok → {scope}",
  "OneBase → Продажи → Рек. остаток → {region}": "OneBase → Savdo → Tavsiya zaxira → {region}",
  "OneBase → Продажи → {region} по месяцам": "OneBase → Savdo → {region} oylar bo'yicha",
  "OneBase → Продажи → {region}": "OneBase → Savdo → {region}",
  "База знаний → {title}": "Bilimlar bazasi → {title}",
  // Период источника («октябрь 2026, данные по 19.10.2026») — в самом конце словаря: шаблон широкий.

  // ── Провайдеры и модели: готовность AI, ошибки, проверки ──
  "AI выключен администратором.": "AI administrator tomonidan o'chirilgan.",
  "AI выключен в «Настройки → AI».": "AI «Sozlamalar → AI» bo'limida o'chirilgan.",
  "AI не настроен в «Настройки → AI».": "AI «Sozlamalar → AI» bo'limida sozlanmagan.",
  "AI не настроен: администратор должен выбрать основную модель в «Настройки → AI».":
    "AI sozlanmagan: administrator «Sozlamalar → AI» bo'limida asosiy modelni tanlashi kerak.",
  "AI не настроен: провайдер основной модели не поддерживается.": "AI sozlanmagan: asosiy model provayderi qo'llab-quvvatlanmaydi.",
  "AI не настроен: нет ключа API {provider}. Администратор добавляет его в «Настройки → AI → Провайдеры».":
    "AI sozlanmagan: {provider} API kaliti yo'q. Administrator uni «Sozlamalar → AI → Provayderlar» bo'limida qo'shadi.",
  "AI не настроен: администратор должен добавить ключ провайдера и выбрать модель в «Настройки → AI».":
    "AI sozlanmagan: administrator provayder kalitini qo'shishi va «Sozlamalar → AI» bo'limida modelni tanlashi kerak.",
  "Провайдер {provider} выключен в «Настройки → AI → Провайдеры».": "{provider} provayderi «Sozlamalar → AI → Provayderlar» bo'limida o'chirilgan.",
  "Провайдер {provider} выключен в настройках AI.": "{provider} provayderi AI sozlamalarida o'chirilgan.",
  "Провайдер «{provider}» не поддерживается.": "«{provider}» provayderi qo'llab-quvvatlanmaydi.",
  "Провайдер {provider} для эмбеддингов не настроен.": "Embeddinglar uchun {provider} provayderi sozlanmagan.",
  "Нет ключа API {provider} — добавьте его в «Настройки → AI → Провайдеры».": "{provider} API kaliti yo'q — uni «Sozlamalar → AI → Provayderlar» bo'limida qo'shing.",
  "Ключ API {provider} не задан — добавьте его в «Настройки → AI → Провайдеры».":
    "{provider} API kaliti kiritilmagan — uni «Sozlamalar → AI → Provayderlar» bo'limida qo'shing.",
  "Основная модель: {primary} Резервная модель: {fallback}": "Asosiy model: {primary} Zaxira model: {fallback}",
  "Модель {model} израсходовала лимит токенов ответа ({max}) и не дописала ответ — у reasoning-моделей часть лимита уходит на рассуждения. Увеличьте «Максимум токенов ответа» в «Настройки → AI».":
    "{model} modeli javob tokenlari limitini ({max}) sarflab, javobni oxirigacha yozmadi — reasoning modellarda limitning bir qismi mulohazaga ketadi. «Sozlamalar → AI» bo'limida «Javobdagi tokenlar chegarasi»ni oshiring.",
  "Модель эмбеддингов не выбрана в «Настройки → AI».": "«Sozlamalar → AI» bo'limida embedding modeli tanlanmagan.",
  "{provider} не строит эмбеддинги — выберите модель эмбеддингов другого провайдера.":
    "{provider} embedding qurmaydi — boshqa provayderning embedding modelini tanlang.",
  "{provider} не строит эмбеддинги — выберите модель OpenAI.": "{provider} embedding qurmaydi — OpenAI modelini tanlang.",
  "{provider} не ответил за {s} с.": "{provider} {s} soniya ichida javob bermadi.",
  "Нет связи с {provider}: {error}": "{provider} bilan aloqa yo'q: {error}",
  "{provider} вернул ответ не в формате JSON — проверьте адрес API.": "{provider} JSON formatida bo'lmagan javob qaytardi — API manzilini tekshiring.",
  // Ответ провайдера с ошибкой: с подробностями провайдера (раньше) и без них
  "Ключ API не принят провайдером — проверьте ключ в «Настройки → AI». Ответ провайдера: {detail}":
    "Provayder API kalitini qabul qilmadi — «Sozlamalar → AI» bo'limida kalitni tekshiring. Provayder javobi: {detail}",
  "Ключ API не принят провайдером — проверьте ключ в «Настройки → AI».": "Provayder API kalitini qabul qilmadi — «Sozlamalar → AI» bo'limida kalitni tekshiring.",
  "Провайдер отказал в доступе: у ключа нет прав на эту модель или запрос. Ответ провайдера: {detail}":
    "Provayder ruxsat bermadi: kalitda bu model yoki so'rov uchun huquq yo'q. Provayder javobi: {detail}",
  "Провайдер отказал в доступе: у ключа нет прав на эту модель или запрос.": "Provayder ruxsat bermadi: kalitda bu model yoki so'rov uchun huquq yo'q.",
  "Модель или адрес API не найдены. Ответ провайдера: {detail}": "Model yoki API manzili topilmadi. Provayder javobi: {detail}",
  "Модель или адрес API не найдены.": "Model yoki API manzili topilmadi.",
  "Провайдер не дождался запроса (HTTP 408). Ответ провайдера: {detail}": "Provayder so'rovni kutib turmadi (HTTP 408). Provayder javobi: {detail}",
  "Провайдер не дождался запроса (HTTP 408).": "Provayder so'rovni kutib turmadi (HTTP 408).",
  "Превышен лимит запросов или закончился баланс у провайдера. Ответ провайдера: {detail}":
    "So'rovlar limiti oshdi yoki provayderdagi balans tugadi. Provayder javobi: {detail}",
  "Превышен лимит запросов или закончился баланс у провайдера.": "So'rovlar limiti oshdi yoki provayderdagi balans tugadi.",
  "Провайдер временно недоступен (HTTP {status}). Ответ провайдера: {detail}": "Provayder vaqtincha ishlamayapti (HTTP {status}). Provayder javobi: {detail}",
  "Провайдер временно недоступен (HTTP {status}).": "Provayder vaqtincha ishlamayapti (HTTP {status}).",
  "Провайдер отклонил запрос (HTTP {status}). Ответ провайдера: {detail}": "Provayder so'rovni rad etdi (HTTP {status}). Provayder javobi: {detail}",
  "Провайдер отклонил запрос (HTTP {status}).": "Provayder so'rovni rad etdi (HTTP {status}).",
  // Проверка ключа и модели
  "Подключение к {provider} работает: доступно моделей — {n}.": "{provider} ga ulanish ishlayapti: mavjud modellar — {n}.",
  "Сначала сохраните ключ API {provider}.": "Avval {provider} API kalitini saqlang.",
  "Основная модель не ответила, ответила резервная {model}.": "Asosiy model javob bermadi, zaxira {model} javob berdi.",
  "Модель {model} отвечает.": "{model} modeli javob beryapti.",
  // Сохранение настроек
  "Адрес API должен начинаться с https://": "API manzili https:// bilan boshlanishi kerak",
  "Цена не может быть отрицательной.": "Narx manfiy bo'lishi mumkin emas.",
  "Температура — от 0 до 1 (так её принимают и OpenAI, и Anthropic).": "Harorat — 0 dan 1 gacha (OpenAI ham, Anthropic ham shunday qabul qiladi).",
  "Температура — от 0 до 1.": "Harorat — 0 dan 1 gacha.",
  "Максимум токенов ответа — от 256 до 64 000.": "Javobdagi tokenlar chegarasi — 256 dan 64 000 gacha.",
  "Глубина рассуждений — low, medium, high или по умолчанию модели.": "Mulohaza chuqurligi — low, medium, high yoki model standarti.",
  "Провайдер основной модели не поддерживается.": "Asosiy model provayderi qo'llab-quvvatlanmaydi.",
  "Провайдер резервной модели не поддерживается.": "Zaxira model provayderi qo'llab-quvvatlanmaydi.",
  "Провайдер маршрутизации модели не поддерживается.": "Marshrutlash modeli provayderi qo'llab-quvvatlanmaydi.",
  "Провайдер эмбеддингов модели не поддерживается.": "Embedding modeli provayderi qo'llab-quvvatlanmaydi.",
  "Модели {model} нет в списке {provider} — обновите список моделей в разделе «Модели».":
    "{model} modeli {provider} ro'yxatida yo'q — «Modellar» bo'limida modellar ro'yxatini yangilang.",
  "Модели {model} нет в списке моделей.": "{model} modeli modellar ro'yxatida yo'q.",
  "Задача — от 1 до 4000 символов.": "Vazifa — 1 dan 4000 gacha belgi.",
  "Название — от 1 до 100 символов.": "Nomi — 1 dan 100 tagacha belgi.",
  "Название — от 1 до 300 символов.": "Nomi — 1 dan 300 gacha belgi.",
  "Неизвестное право OneBase.": "Noma'lum OneBase huquqi.",
  "Неизвестный отдел.": "Noma'lum bo'lim.",
  "Инструкция слишком длинная (больше 20 000 символов).": "Yo'riqnoma juda uzun (20 000 belgidan ortiq).",
  "Неизвестный источник знаний {source}.": "Noma'lum bilim manbai: {source}.",
  // Учёт и журнал AI
  "удалённый пользователь": "o'chirilgan foydalanuvchi",
  "система": "tizim",

  // ── База знаний ──
  "Файл пустой.": "Fayl bo'sh.",
  "Этот файл уже есть в базе знаний: «{title}».": "Bu fayl bilimlar bazasida allaqachon bor: «{title}».",
  "Не удалось прочитать файл — возможно, он повреждён или защищён паролем.": "Faylni o'qib bo'lmadi — ehtimol, u shikastlangan yoki parol bilan himoyalangan.",
  "В файле нет текста.": "Faylda matn yo'q.",
  "Модель эмбеддингов не выбрана в «Настройки → AI → Общие».": "«Sozlamalar → AI → Umumiy» bo'limida embedding modeli tanlanmagan.",
  "Семантический поиск недоступен: векторное хранилище не ответило. Работает полнотекстовый поиск.":
    "Semantik qidiruv mavjud emas: vektor ombori javob bermadi. To'liq matnli qidiruv ishlaydi.",
  "Семантический поиск недоступен: {error}": "Semantik qidiruv mavjud emas: {error}",
  "PDF пока не читается: сохраните документ как .docx или .txt и загрузите снова.": "PDF hozircha o'qilmaydi: hujjatni .docx yoki .txt sifatida saqlab, qayta yuklang.",
  "Старый формат Office не читается: сохраните файл как .docx или .xlsx.": "Office ning eski formati o'qilmaydi: faylni .docx yoki .xlsx sifatida saqlang.",
  "Файл .docx повреждён: нет word/document.xml.": ".docx fayli shikastlangan: word/document.xml yo'q.",

  // ── Голос консультанта ──
  "Для голоса нужен ключ OpenAI: «Настройки → AI → Провайдеры → OpenAI». Пока работает голос браузера, если он есть.":
    "Ovoz uchun OpenAI kaliti kerak: «Sozlamalar → AI → Provayderlar → OpenAI». Hozircha brauzer ovozi ishlaydi (agar u bo'lsa).",
  "Запись больше {n} МБ — говорите короче.": "Yozuv {n} MB dan katta — qisqaroq gapiring.",
  "Формат записи {type} не поддерживается.": "{type} yozuv formati qo'llab-quvvatlanmaydi.",
  "В ответе нечего читать вслух.": "Javobda ovoz chiqarib o'qiladigan narsa yo'q.",
  "Части {n} нет: всего {total}.": "{n}-qism yo'q: jami {total} ta.",
  "Голос не настроен: нужен ключ OpenAI в «Настройки → AI → Провайдеры».": "Ovoz sozlanmagan: «Sozlamalar → AI → Provayderlar» bo'limida OpenAI kaliti kerak.",

  // ── AI Dashboard: находки проактивного анализа ──
  // Заголовки
  "Продажи в регионе {region} снизились на {pct}": "{region} mintaqasida savdo {pct} ga pasaydi",
  "{region}: план месяца не выполнен — {pct}": "{region}: oylik reja bajarilmadi — {pct}",
  "{region}: план месяца под угрозой — прогноз {pct}": "{region}: oylik reja xavf ostida — prognoz {pct}",
  "Ещё {n} регионов не дотянули до 90% плана": "Yana {n} ta mintaqa rejaning 90% iga yetmadi",
  "Ещё {n} регионов идут ниже 90% плана": "Yana {n} ta mintaqa rejaning 90% idan past bormoqda",
  "{region} перевыполняет план — прогноз {pct}": "{region} rejani oshirib bajarmoqda — prognoz {pct}",
  "План продаж компании не выполнен — {pct}": "Kompaniyaning savdo rejasi bajarilmadi — {pct}",
  "План продаж компании под угрозой — прогноз {pct}": "Kompaniyaning savdo rejasi xavf ostida — prognoz {pct}",
  "Торговых представителей с критичными замечаниями: {n}": "Jiddiy e'tirozli savdo vakillari: {n}",
  "{region}: {n} SKU продаются в других регионах, но не здесь": "{region}: {n} ta SKU boshqa mintaqalarda sotiladi, bu yerda esa yo'q",
  "Запасы заканчиваются: {n} SKU меньше чем на 15 дней продаж": "Zaxiralar tugayapti: {n} ta SKU 15 kunlik savdodan kam",
  "{n} SKU лежат на складах без продаж": "{n} ta SKU omborlarda sotuvsiz turibdi",
  "Первичка отстаёт от плана — прогноз {pct}": "Birlamchi rejadan orqada — prognoz {pct}",
  // Описания (длинные варианты — раньше короткого «План …, факт … с … по ….»)
  "с {from} по {to}: {now} против {before} за те же дни прошлого месяца (−{drop}). Выручка {sum} ({sumDelta}), АКБ {akb} ({akbDelta}).":
    "{from} dan {to} gacha: {now}, o'tgan oyning shu kunlarida — {before} (−{drop}). Tushum {sum} ({sumDelta}), AKB {akb} ({akbDelta}).",
  "План {plan}, факт {fact} ({pct}) с {from} по {to}, прогноз {forecast}. Регионов с прогнозом ниже 90%: {n}.":
    "Reja {plan}, fakt {fact} ({pct}), {from} dan {to} gacha, prognoz {forecast}. Prognozi 90% dan past mintaqalar: {n}.",
  "План {plan}, факт {fact} ({pct}) с {from} по {to}. Регионов с прогнозом ниже 90%: {n}.":
    "Reja {plan}, fakt {fact} ({pct}), {from} dan {to} gacha. Prognozi 90% dan past mintaqalar: {n}.",
  "План {plan}, факт {fact} ({pct}) с {from} по {to}; при текущем темпе к концу месяца — {forecast}.":
    "Reja {plan}, fakt {fact} ({pct}), {from} dan {to} gacha; joriy sur'atda oy oxiriga — {forecast}.",
  "План {plan}, факт {fact} ({pct}) с {from} по {to}.": "Reja {plan}, fakt {fact} ({pct}), {from} dan {to} gacha.",
  "План {plan}, прогноз {forecast}; АКБ {akb}, страйк {strike}.": "Reja {plan}, prognoz {forecast}; AKB {akb}, strayk {strike}.",
  "Критичных — {critical}, с риском — {risk} из {total} ТП ({month} {year}): низкий страйк, визиты без продаж, падение темпа.":
    "{total} ta TP dan jiddiy — {critical}, xavfli — {risk} ({month} {year}): past strayk, sotuvsiz tashriflar, sur'at pasayishi.",
  "В регионе продаётся {n} SKU; ещё {m} SKU, которые берут в других регионах, здесь в этом месяце не продавались.":
    "Mintaqada {n} ta SKU sotilmoqda; boshqa mintaqalarda olinadigan yana {m} ta SKU bu oy bu yerda sotilmagan.",
  "Меньше недели продаж — {n} SKU. Самые срочные: {names}.": "Bir haftalik savdodan kam — {n} ta SKU. Eng shoshilinchlari: {names}.",
  "Остаток есть, а продаж за {days} дней нет (данные на {day}). Затоварка (больше 30 дней продаж) — ещё {n} SKU.":
    "Qoldiq bor, lekin {days} kun davomida savdo yo'q ({day} holatiga). Ortiqcha zaxira (30 kunlik savdodan ko'p) — yana {n} ta SKU.",
  "Отгружено дилерам {shipped} из плана {plan} ({month} {year}), прогноз {forecast}.":
    "Dilerlarga {plan} rejadan {shipped} jo'natildi ({month} {year}), prognoz {forecast}.",
  // Рекомендации
  "Разобрать с руководителем региона, какие ТП и категории дали снижение; проверить проблемных агентов и «молчащие» точки региона.":
    "Mintaqa rahbari bilan qaysi TP va toifalar pasayish berganini tahlil qiling; muammoli agentlar va mintaqaning «jim» nuqtalarini tekshiring.",
  "Разобрать причины по ТП и категориям региона и заложить меры в план следующего месяца.":
    "Mintaqaning TP va toifalari bo'yicha sabablarni tahlil qiling va choralarni keyingi oy rejasiga kiriting.",
  "Проверить выполнение плана по ТП региона и категориям; определить, где добрать объём до конца месяца.":
    "Mintaqa TP lari va toifalari bo'yicha reja bajarilishini tekshiring; oy oxirigacha hajmni qayerdan to'ldirish mumkinligini aniqlang.",
  "Сравнить с регионами, выполняющими план: страйк, АКБ на агента, ширина ассортимента.":
    "Rejani bajarayotgan mintaqalar bilan solishtiring: strayk, agentga AKB, assortiment kengligi.",
  "Разобрать практики региона для других регионов; проверить, не занижен ли план.":
    "Mintaqa tajribasini boshqa mintaqalar uchun o'rganing; reja past qo'yilmaganini tekshiring.",
  "Сосредоточиться на регионах с наибольшим отставанием в кг; проверить дефицит на складах этих регионов.":
    "Kg bo'yicha eng ko'p orqada qolgan mintaqalarga e'tibor qarating; bu mintaqalar omborlaridagi taqchillikni tekshiring.",
  "Разобрать список проблемных агентов с руководителями регионов, начиная с критичных.":
    "Muammoli agentlar ro'yxatini mintaqa rahbarlari bilan jiddiylaridan boshlab ko'rib chiqing.",
  "Проверить наличие этих SKU на складе региона и включить их в задачи ТП.": "Bu SKU lar mintaqa omborida borligini tekshiring va ularni TP vazifalariga kiriting.",
  "Заказать первичку по дефицитным SKU у завода; проверить остаток завода по этим товарам.":
    "Taqchil SKU lar bo'yicha zavoddan birlamchi buyurtma qiling; bu tovarlar bo'yicha zavod qoldig'ini tekshiring.",
  "Решить судьбу неликвида: перераспределить между регионами, акция или возврат на завод.":
    "Likvidsiz tovar taqdirini hal qiling: mintaqalar o'rtasida qayta taqsimlash, aksiya yoki zavodga qaytarish.",
  "Сверить заявки дилеров с остатками их складов; проверить, каким дилерам не отгружено по плану.":
    "Dilerlar buyurtmalarini ularning ombor qoldiqlari bilan solishtiring; qaysi dilerlarga reja bo'yicha jo'natilmaganini tekshiring.",

  // ── Отчёт Excel консультанта: названия, которые сервер ставит сам ──
  "отчёт::Лист {n}": "{n}-varaq",
  "отчёт::Колонка {n}": "{n}-ustun",
  "Значение": "Qiymat",

  // ── AI Data Manager: готовность, ошибки модели и чата ──
  "Нет ключа Claude API: добавьте ключ Anthropic в «Настройки → AI → Провайдеры» или переменную ANTHROPIC_API_KEY на сервере.":
    "Claude API kaliti yo'q: Anthropic kalitini «Sozlamalar → AI → Provayderlar» bo'limida yoki serverdagi ANTHROPIC_API_KEY o'zgaruvchisida qo'shing.",
  "Нет ключа Claude API (Anthropic) — см. «Настройки → AI → Провайдеры».": "Claude API (Anthropic) kaliti yo'q — «Sozlamalar → AI → Provayderlar» ga qarang.",
  "Claude API не принял ключ. Проверьте ключ Anthropic в «Настройки → AI → Провайдеры».":
    "Claude API kalitni qabul qilmadi. «Sozlamalar → AI → Provayderlar» bo'limida Anthropic kalitini tekshiring.",
  "У ключа Anthropic нет доступа к этой модели или функции.": "Anthropic kalitida bu model yoki funksiyaga ruxsat yo'q.",
  "Модель не найдена в Claude API — проверьте DataManager:Model.": "Model Claude API da topilmadi — DataManager:Model ni tekshiring.",
  "Claude API временно ограничил запросы (лимит). Повторите через минуту.": "Claude API so'rovlarni vaqtincha chekladi (limit). Bir daqiqadan keyin takrorlang.",
  "Claude API отклонил запрос (неверный формат). Подробности — в журнале сервера.": "Claude API so'rovni rad etdi (noto'g'ri format). Tafsilotlar — server jurnalida.",
  "Claude API временно недоступен. Повторите позже.": "Claude API vaqtincha ishlamayapti. Keyinroq takrorlang.",
  "Нет связи с Claude API.": "Claude API bilan aloqa yo'q.",
  "Claude API не ответил вовремя.": "Claude API o'z vaqtida javob bermadi.",
  "Ошибка Claude API. Подробности — в журнале сервера.": "Claude API xatosi. Tafsilotlar — server jurnalida.",
  "К одному сообщению — не больше {n} файлов.": "Bitta xabarga — ko'pi bilan {n} ta fayl.",
  "Часть файлов не найдена среди ваших загрузок.": "Fayllarning bir qismi yuklamalaringiz orasida topilmadi.",
  "В этом чате ещё выполняется предыдущий запрос — дождитесь ответа.": "Bu chatda oldingi so'rov hali bajarilmoqda — javobni kuting.",
  "Ход занял больше {n} мин и остановлен.": "Ish {n} daqiqadan ko'p davom etdi va to'xtatildi.",
  "Сообщение — от 1 до {n} символов.": "Xabar — 1 dan {n} gacha belgi.",
  "Файл не выбран или пустой.": "Fayl tanlanmagan yoki bo'sh.",
  "План изменений не найден.": "O'zgarishlar rejasi topilmadi.",
  // Служебные вставки в ответ (отдельными абзацами)
  "Готово.": "Tayyor.",
  "Ответ не получен.": "Javob olinmadi.",
  "Модель отказалась выполнять этот запрос.": "Model bu so'rovni bajarishdan bosh tortdi.",
  "_(ответ обрезан по длине — попросите продолжить)_": "_(javob uzunlik bo'yicha qirqildi — davom ettirishni so'rang)_",
  "_(достигнут лимит шагов за один ход — напишите «продолжи»)_": "_(bitta javobdagi qadamlar limitiga yetildi — «davom et» deb yozing)_",

  // Шаги: инструменты AI Data Manager и пометки
  "Повтор запроса к Claude": "Claude ga so'rov takrorlanmoqda",
  "Что можно менять": "Nimani o'zgartirish mumkin",
  "Файлы чата": "Chat fayllari",
  "Таблицы Google Sheets": "Google Sheets jadvallari",
  "Синхронизация Google Sheets": "Google Sheets sinxronizatsiyasi",
  "Чтение Google Sheets": "Google Sheets ni o'qish",
  "Чтение отчёта": "Hisobotni o'qish",
  "Текущие планы": "Joriy rejalar",
  "Показатели продаж": "Savdo ko'rsatkichlari",
  "Сверка факта": "Faktni solishtirish",
  "Цели KPI": "KPI maqsadlari",
  "План изменений: планы регионов": "O'zgarishlar rejasi: mintaqalar rejalari",
  "План изменений: цели KPI": "O'zgarishlar rejasi: KPI maqsadlari",
  "План изменений: дилеры и СВР": "O'zgarishlar rejasi: dilerlar va SVR",
  "Сверка P&L с первичкой": "P&L ni birlamchi bilan solishtirish",
  "План изменений: P&L дилеров": "O'zgarishlar rejasi: dilerlar P&L",
  "План изменений": "O'zgarishlar rejasi",
  "Проверка результата": "Natijani tekshirish",
  "Отмена плана": "Rejani bekor qilish",
  "нет такого инструмента": "bunday vosita yo'q",
  "нет права": "huquq yo'q",
  "неверные аргументы": "noto'g'ri argumentlar",
  "{field} обязателен": "{field} majburiy",
  "для файла нужен mapping": "fayl uchun mapping kerak",
  "нужен fileId с mapping или rows": "mapping bilan fileId yoki rows kerak",
  "values — объект «ключ → число»": "values — «kalit → son» obyekti",
  "changes — массив": "changes — massiv",
  "kind — rop, factory или primary (получено «{value}»)": "kind — rop, factory yoki primary («{value}» olindi)",
  "Продаж за {period} в OneBase нет. Есть месяцы: {list}": "OneBase da {period} uchun savdo yo'q. Mavjud oylar: {list}",
  "Продаж за {period} в OneBase нет.": "OneBase da {period} uchun savdo yo'q.",
  "Режим replace удаляет строки месяца, которых нет в отчёте. Сначала спросите администратора, полный ли отчёт за период, и передайте completePeriod = true только после явного «да».":
    "replace rejimi oyning hisobotda yo'q qatorlarini o'chiradi. Avval administratordan hisobot davr uchun to'liqmi, deb so'rang va completePeriod = true ni faqat aniq «ha» dan keyin bering.",
  "Режим replace удаляет значения месяца, которых нет в отчёте. Сначала спросите администратора, полный ли отчёт за период, и передайте completePeriod = true только после явного «да».":
    "replace rejimi oyning hisobotda yo'q qiymatlarini o'chiradi. Avval administratordan hisobot davr uchun to'liqmi, deb so'rang va completePeriod = true ni faqat aniq «ha» dan keyin bering.",

  // Загрузка и разбор отчёта (уточнённый шаблон «Формат … Старый .xls …» — раньше общего)
  "Имя файла — от 1 до 255 символов.": "Fayl nomi — 1 dan 255 gacha belgi.",
  "Формат {ext} не поддерживается. Можно: {list}. Старый .xls сохраните в Excel как .xlsx.":
    "{ext} formati qo'llab-quvvatlanmaydi. Mumkin: {list}. Eski .xls ni Excel da .xlsx sifatida saqlang.",
  "Формат {ext} не поддерживается. Можно: {list}.": "{ext} formati qo'llab-quvvatlanmaydi. Mumkin: {list}.",
  "Старый формат .xls не читается: сохраните файл в Excel как .xlsx.": "Eski .xls formati o'qilmaydi: faylni Excel da .xlsx sifatida saqlang.",
  "Файл не удалось прочитать: возможно, он повреждён или защищён паролем.": "Faylni o'qib bo'lmadi: ehtimol, u shikastlangan yoki parol bilan himoyalangan.",
  "Файл {id} не найден среди ваших загрузок.": "{id} fayli yuklamalaringiz orasida topilmadi.",
  "В файле нет данных: все листы пустые.": "Faylda ma'lumot yo'q: barcha varaqlar bo'sh.",
  "В файле нет таблиц.": "Faylda jadvallar yo'q.",
  "Листа «{sheet}» нет. Листы: {list}.": "«{sheet}» varag'i yo'q. Varaqlar: {list}.",
  "Столбец «{column}» не найден ни по букве, ни по заголовку в строке {row}.": "«{column}» ustuni {row}-qatorda na harf, na sarlavha bo'yicha topilmadi.",
  "Строка заголовка {row} вне листа (строк: {n}).": "{row}-sarlavha qatori varaqdan tashqarida (qatorlar: {n}).",
  "Множитель единиц должен быть больше нуля (кг — 1, тонны — 1000).": "Birlik ko'paytuvchisi noldan katta bo'lishi kerak (kg — 1, tonna — 1000).",
  "Множитель единиц должен быть больше нуля (сум — 1, тыс. сум — 1000).": "Birlik ko'paytuvchisi noldan katta bo'lishi kerak (so'm — 1, ming so'm — 1000).",
  "Укажите столбцы значений (valueColumns) или столбец значения (valueColumn) с категорией (categoryColumn).":
    "Qiymatlar ustunlarini (valueColumns) yoki toifa (categoryColumn) bilan qiymat ustunini (valueColumn) ko'rsating.",
  "Не указан месяц: ни столбцом (monthColumn), ни значением (month).": "Oy ko'rsatilmagan: na ustun (monthColumn), na qiymat (month) bilan.",
  "Для таблицы «статьи в строках» нужен столбец статей (lineColumn).": "«Moddalar qatorlarda» jadvali uchun moddalar ustuni (lineColumn) kerak.",
  "Для таблицы «статьи в строках» укажите год и месяц (year, month).": "«Moddalar qatorlarda» jadvali uchun yil va oyni (year, month) ko'rsating.",
  "Укажите столбец контрагента (dealerColumn).": "Kontragent ustunini (dealerColumn) ko'rsating.",
  "Укажите столбцы строк P&L (valueColumns) или столбец статьи (lineColumn) со столбцом суммы (valueColumn).":
    "P&L qatorlari ustunlarini (valueColumns) yoki summa ustuni (valueColumn) bilan modda ustunini (lineColumn) ko'rsating.",
  "Для столбца возврата (returnColumn) укажите значения, которые означают возврат (returnValues).":
    "Qaytarish ustuni (returnColumn) uchun qaytarishni bildiradigan qiymatlarni (returnValues) ko'rsating.",
  "lineMap: «{line}» — нет такой строки P&L. Исходные строки: {list}.": "lineMap: «{line}» — bunday P&L qatori yo'q. Dastlabki qatorlar: {list}.",

  // Подготовка плана изменений: ошибки
  "Для файла нужно сопоставление столбцов (mapping).": "Fayl uchun ustunlar moslashuvi (mapping) kerak.",
  "Значений плана не найдено: проверьте лист, строку заголовка и столбцы.": "Reja qiymatlari topilmadi: varaq, sarlavha qatori va ustunlarni tekshiring.",
  "Значений P&L не найдено: проверьте лист, строку заголовка и столбцы.": "P&L qiymatlari topilmadi: varaq, sarlavha qatori va ustunlarni tekshiring.",
  "Слишком много значений ({n}); за раз — не больше {max}.": "Qiymatlar juda ko'p ({n}); bir martada — ko'pi bilan {max} ta.",
  "Слишком много строк ({n}); за раз — не больше {max}. Разбейте отчёт по месяцам.": "Qatorlar juda ko'p ({n}); bir martada — ko'pi bilan {max} ta. Hisobotni oylarga bo'ling.",
  "В одном плане — не больше 24 месяцев.": "Bitta rejada — ko'pi bilan 24 oy.",
  "Не указано ни одной цели.": "Birorta ham maqsad ko'rsatilmagan.",
  "Не указано ни одного изменения.": "Birorta ham o'zgarish ko'rsatilmagan.",
  "Период fromMonth…toMonth: {error}.": "fromMonth…toMonth davri: {error}.",
  "В отчёте нет строк за период {from} — {to}.": "Hisobotda {from} — {to} davri uchun qatorlar yo'q.",
  "План в статусе {status} отменить нельзя.": "{status} holatidagi rejani bekor qilib bo'lmaydi.",
  "План в статусе {status}: проверять нечего — он не применялся.": "{status} holatidagi reja: tekshiradigan narsa yo'q — u qo'llanmagan.",
  "План в статусе {status} применить нельзя.": "{status} holatidagi rejani qo'llab bo'lmaydi.",
  "Откатить можно только применённый план (сейчас {status}).": "Faqat qo'llangan rejani orqaga qaytarish mumkin (hozir {status}).",

  // План изменений: названия («Планы регионов: план РОП · октябрь 2026 · отчёт.xlsx»)
  "Планы регионов": "Mintaqalar rejalari",
  "Дилеры и СВР регионов": "Mintaqalar dilerlari va SVR",
  "план «Завод»": "«Zavod» rejasi",
  "план первички": "birlamchi reja",
  "из сообщения": "xabardan",
  "без периода": "davrsiz",
  "Планы регионов: {kind} · без периода · {source}": "Mintaqalar rejalari: {kind} · davrsiz · {source}",
  "Планы регионов: {kind} · {month} {year} · {source}": "Mintaqalar rejalari: {kind} · {month} {year} · {source}",
  "Планы регионов: {kind} · {period} · {source}": "Mintaqalar rejalari: {kind} · {period} · {source}",
  "P&L дилеров · без периода · {source}": "Dilerlar P&L · davrsiz · {source}",
  "P&L дилеров · {month} {year} · {source}": "Dilerlar P&L · {month} {year} · {source}",
  "P&L дилеров · {period} · {source}": "Dilerlar P&L · {period} · {source}",
  "Дилеры и СВР регионов · {source}": "Mintaqalar dilerlari va SVR · {source}",

  // План изменений: строки («Самарканд · Кекс · октябрь 2026 · план РОП»), цели, дилеры и СВР
  "итог без разбивки": "bo'linmagan jami",
  "{region} · {category} · {month} {year} · план РОП": "{region} · {category} · {month} {year} · ROP rejasi",
  "{region} · {category} · {month} {year} · план «Завод»": "{region} · {category} · {month} {year} · «Zavod» rejasi",
  "{region} · {category} · {month} {year} · план первички": "{region} · {category} · {month} {year} · birlamchi reja",
  "{region} · дилер": "{region} · diler",
  "{region} · СВР": "{region} · SVR",
  "Цель: конверсия визита (доля 0–1)": "Maqsad: tashrif konversiyasi (ulush 0–1)",
  "Цель: выручка на торговую точку, сум": "Maqsad: savdo nuqtasiga tushum, so'm",
  "Цель: АКБ на агента, точек": "Maqsad: agentga AKB, nuqta",
  "Цель: категорий на торговую точку": "Maqsad: savdo nuqtasiga toifalar",

  // План изменений: пояснения к строкам (столбец «Источник и комментарий»)
  "не число: «{value}»": "son emas: «{value}»",
  "неверный период: {period}": "noto'g'ri davr: {period}",
  "план не может быть меньше нуля": "reja noldan kichik bo'lishi mumkin emas",
  "регион не указан": "mintaqa ko'rsatilmagan",
  "«{text}» подходит к нескольким регионам: {list}": "«{text}» bir nechta mintaqaga mos keladi: {list}",
  "регион «{text}» не найден в OneBase": "«{text}» mintaqasi OneBase da topilmadi",
  "«{text}» подходит к нескольким категориям: {list}": "«{text}» bir nechta toifaga mos keladi: {list}",
  "категория «{text}» не найдена (категории отчёта: {list})": "«{text}» toifasi topilmadi (hisobot toifalari: {list})",
  "в отчёте несколько разных значений: {values} — если их нужно сложить (реестр, база продаж), подготовьте план с sumRows = true":
    "hisobotda bir nechta turli qiymatlar: {values} — agar ularni qo'shish kerak bo'lsa (reestr, savdo bazasi), rejani sumRows = true bilan tayyorlang",
  "в отчёте несколько разных значений: {values}": "hisobotda bir nechta turli qiymatlar: {values}",
  "строка повторяется в отчёте с тем же значением": "qator hisobotda xuddi shu qiymat bilan takrorlanadi",
  "в отчёте 0 — строка плана удаляется": "hisobotda 0 — reja qatori o'chiriladi",
  "в отчёте 0 — значение удаляется": "hisobotda 0 — qiymat o'chiriladi",
  "значение уже такое": "qiymat allaqachon shunday",
  "строки нет в отчёте, а отчёт отмечен как полный за месяц — удаляется": "qator hisobotda yo'q, hisobot esa oy uchun to'liq deb belgilangan — o'chiriladi",
  "значения нет в отчёте, а отчёт отмечен как полный за месяц — удаляется": "qiymat hisobotda yo'q, hisobot esa oy uchun to'liq deb belgilangan — o'chiriladi",
  "такой цели нет; есть: {list}": "bunday maqsad yo'q; bor: {list}",
  "значение должно быть от {min} до {max}": "qiymat {min} dan {max} gacha bo'lishi kerak",
  "поле — dealer или supervisor": "maydon — dealer yoki supervisor",
  "не длиннее {n} символов": "{n} belgidan uzun bo'lmasin",
  "контрагент не указан": "kontragent ko'rsatilmagan",
  "имя контрагента длиннее {n} символов": "kontragent nomi {n} belgidan uzun",
  "строка P&L «{line}» не распознана — укажите её в lineMap": "«{line}» P&L qatori tanilmadi — uni lineMap da ko'rsating",
  "сумма {n} строк отчёта": "hisobotning {n} ta qatori yig'indisi",

  // План изменений: предупреждения и пояснения
  "Конфликтов: {n}. Эти строки не будут применены — уточните их и подготовьте план заново.":
    "Ziddiyatlar: {n}. Bu qatorlar qo'llanmaydi — ularni aniqlashtiring va rejani qaytadan tayyorlang.",
  "{region} · {month} {year}: есть и итог без разбивки ({total} кг), и категории (сумма {sum} кг) — в выполнении OneBase берёт итог, категории остаются только в плане по категориям.":
    "{region} · {month} {year}: bo'linmagan jami ({total} kg) ham, toifalar (yig'indisi {sum} kg) ham bor — bajarilishda OneBase jamini oladi, toifalar faqat toifalar bo'yicha rejada qoladi.",
  "{month} {year}: сейчас планов регионов вида «{kind}» нет — план месяца считается из планов ТП Linko. После применения весь месяц перейдёт на планы регионов.":
    "{month} {year}: hozir «{kind}» turidagi mintaqa rejalari yo'q — oylik reja Linko TP rejalaridan hisoblanadi. Qo'llangandan keyin butun oy mintaqalar rejalariga o'tadi.",
  "{month} {year}: без плана останутся {n} рег.: {list} (их факт войдёт в выполнение РМ и республики, план — нет).":
    "{month} {year}: {n} ta mintaqa rejasiz qoladi: {list} (ularning fakti RM va respublika bajarilishiga kiradi, rejasi — yo'q).",
  "Полная замена: затронуты только строки P&L из отчёта ({lines}) за его месяцы; удаляется значений: {n}.":
    "To'liq almashtirish: hisobot oylari uchun faqat undagi P&L qatorlari ({lines}) o'zgaradi; o'chiriladigan qiymatlar: {n}.",
  "Расчётные строки отчёта ({n} знач.) совпали с расчётом OneBase.": "Hisobotning hisoblangan qatorlari ({n} ta qiymat) OneBase hisobi bilan mos keldi.",
  "Расчётные строки отчёта не совпадают с расчётом OneBase: {n} из {total} — смотрите сверку. Расчётные строки не загружаются: их считает OneBase по исходным.":
    "Hisobotning hisoblangan qatorlari OneBase hisobi bilan mos kelmaydi: {total} tadan {n} ta — solishtirishga qarang. Hisoblangan qatorlar yuklanmaydi: ularni OneBase dastlabki qatorlar bo'yicha hisoblaydi.",
  "Новые контрагенты, которых ещё не было в P&L: {list}. Если это тот же дилер под другим именем — сопоставьте его через dealerMap.":
    "P&L da hali bo'lmagan yangi kontragentlar: {list}. Agar bu boshqa nomdagi o'sha diler bo'lsa — uni dealerMap orqali moslang.",
  "Строк без изменений: {n} — в плане не показаны.": "O'zgarishsiz qatorlar: {n} — rejada ko'rsatilmagan.",
  "Этот же запрос уже применён {date} (операция {id}). Новых изменений нет.": "Aynan shu so'rov {date} da qo'llangan (amal {id}). Yangi o'zgarishlar yo'q.",
  "Этот же запрос уже применён {date} (операция {id}). С тех пор данные отличаются: изменений {n}.":
    "Aynan shu so'rov {date} da qo'llangan (amal {id}). O'shandan beri ma'lumotlar farq qiladi: o'zgarishlar {n}.",
  "Заменён новым планом по тому же запросу.": "Xuddi shu so'rov bo'yicha yangi reja bilan almashtirildi.",
  "Отменён администратором.": "Administrator tomonidan bekor qilingan.",

  // Как изменятся показатели (оценка влияния)
  "Первичка, республика": "Birlamchi, respublika",
  "Оценку влияния посчитать не удалось: {error}": "Ta'sirni hisoblab bo'lmadi: {error}",
  "Первичка: план месяца — сумма планов дилеров (регионов) вида «Первичка».": "Birlamchi: oylik reja — «Birlamchi» turidagi dilerlar (mintaqalar) rejalari yig'indisi.",
  "Продаж за этот месяц в OneBase ещё нет — выполнение не считается; план будет виден в «План на следующий месяц» и после начала продаж.":
    "OneBase da bu oy uchun savdo hali yo'q — bajarilish hisoblanmaydi; reja «Keyingi oy rejasi» bo'limida va savdo boshlangandan keyin ko'rinadi.",
  "Оценка: выполнение = факт ÷ новый план; точные значения OneBase пересчитает после применения.":
    "Taxmin: bajarilish = fakt ÷ yangi reja; aniq qiymatlarni OneBase qo'llangandan keyin qayta hisoblaydi.",
  "Сейчас план месяца — «{source}»; после применения месяц перейдёт на планы регионов, план республики — сумма планов регионов из этого плана.":
    "Hozir oylik reja — «{source}»; qo'llangandan keyin oy mintaqalar rejalariga o'tadi, respublika rejasi — shu rejadagi mintaqalar rejalari yig'indisi.",
  "Пересчитано OneBase после применения: источник плана — {source}, данные по {date}.":
    "Qo'llangandan keyin OneBase qayta hisobladi: reja manbai — {source}, {date} holatiga ma'lumot.",

  // Применение и откат: итог, события в чате
  "Администратор подтвердил план. Применено строк: {n}. Проверка: {checked} стр., расхождений {mismatches}.":
    "Administrator rejani tasdiqladi. Qo'llangan qatorlar: {n}. Tekshiruv: {checked} qator, farqlar {mismatches}.",
  "Администратор подтвердил план. Применено строк: {n}.": "Administrator rejani tasdiqladi. Qo'llangan qatorlar: {n}.",
  "Применено строк: {n}.": "Qo'llangan qatorlar: {n}.",
  "План уже применён — повторно не применяется.": "Reja allaqachon qo'llangan — qayta qo'llanmaydi.",
  "План не применён: Данные изменились после подготовки плана ({n} стр.). Подготовьте план заново.":
    "Reja qo'llanmadi: reja tayyorlangandan keyin ma'lumotlar o'zgardi ({n} qator). Rejani qaytadan tayyorlang.",
  "План не применён: {reason}": "Reja qo'llanmadi: {reason}",
  "Данные изменились после подготовки плана ({n} стр.). Подготовьте план заново.":
    "Reja tayyorlangandan keyin ma'lumotlar o'zgardi ({n} qator). Rejani qaytadan tayyorlang.",
  "Администратор откатил план. Откачено строк: {n}; оставлено из-за более новых изменений: {kept}. {problems}":
    "Administrator rejani orqaga qaytardi. Qaytarilgan qatorlar: {n}; yangiroq o'zgarishlar sababli qoldirilgan: {kept}. {problems}",
  "Администратор откатил план. Откачено строк: {n}.": "Administrator rejani orqaga qaytardi. Qaytarilgan qatorlar: {n}.",
  "Администратор откатил план. План уже откачен.": "Administrator rejani orqaga qaytardi. Reja allaqachon orqaga qaytarilgan.",
  "Откачено строк: {n}; оставлено из-за более новых изменений: {kept}.": "Qaytarilgan qatorlar: {n}; yangiroq o'zgarishlar sababli qoldirilgan: {kept}.",
  "Откачено строк: {n}.": "Qaytarilgan qatorlar: {n}.",
  "План уже откачен.": "Reja allaqachon orqaga qaytarilgan.",
  "Администратор отменил план «{title}» — он не будет применён.": "Administrator «{title}» rejasini bekor qildi — u qo'llanmaydi.",
  "{label}: в плане «было» {before}, сейчас {current}": "{label}: rejada «avval» {before}, hozir {current}",
  "{label}: ожидалось {expected}, сейчас {current} — оставлено как есть": "{label}: kutilgan {expected}, hozir {current} — o'z holicha qoldirildi",
  "В плане нет строк для применения.": "Rejada qo'llash uchun qatorlar yo'q.",
  "Недостаточно прав: нужно право «AI Data Manager».": "Huquq yetarli emas: «AI Data Manager» huquqi kerak.",
  "План сейчас применяется или откатывается — дождитесь окончания.": "Reja hozir qo'llanmoqda yoki orqaga qaytarilmoqda — tugashini kuting.",
  "Эти данные одновременно меняли в другом месте. Ничего не записано — повторите действие.":
    "Bu ma'lumotlar bir vaqtda boshqa joyda o'zgartirildi. Hech narsa yozilmadi — amalni takrorlang.",
  "Ошибка базы данных — ничего не записано. Подробности в журнале сервера.": "Ma'lumotlar bazasi xatosi — hech narsa yozilmadi. Tafsilotlar server jurnalida.",
  "Регион плана удалён — подготовьте план заново.": "Reja mintaqasi o'chirilgan — rejani qaytadan tayyorlang.",
  "Регион удалён — подготовьте план заново.": "Mintaqa o'chirilgan — rejani qaytadan tayyorlang.",
  "Неизвестный вид записи: {type}": "Noma'lum yozuv turi: {type}",

  // Проверка после применения
  "Цели читаются отчётами при следующем расчёте месяца; кэш отчётов сброшен.":
    "Maqsadlarni hisobotlar oyning keyingi hisob-kitobida o'qiydi; hisobotlar keshi tozalandi.",
  "P&L пересчитан OneBase по загруженным исходным строкам (раздел «Продажи → P&L дилеров»).":
    "P&L OneBase tomonidan yuklangan dastlabki qatorlar bo'yicha qayta hisoblandi («Savdo → Dilerlar P&L» bo'limi).",
  "Есть расхождения: смотрите problems — значение в базе или в расчёте OneBase отличается от плана изменений.":
    "Farqlar bor: problems ga qarang — bazadagi yoki OneBase hisobidagi qiymat o'zgarishlar rejasidan farq qiladi.",
  "P&L · {month} {year} · {line}: расчёт OneBase": "P&L · {month} {year} · {line}: OneBase hisobi",
  "{region} · {month} {year}: план в расчёте OneBase": "{region} · {month} {year}: OneBase hisobidagi reja",

  // Сопоставление столбцов Google Sheets подсказкой AI
  "ИИ ответил не в формате JSON — повторите или сопоставьте столбцы вручную.": "AI JSON formatida javob bermadi — qayta urinib ko'ring yoki ustunlarni qo'lda moslang.",
  "ИИ не вернул сопоставление столбцов — повторите или сопоставьте вручную.": "AI ustunlar moslashini qaytarmadi — qayta urinib ko'ring yoki qo'lda moslang.",

  // Широкие шаблоны — последними, чтобы не перехватить уточнённые выше.
  // Период источника данных: «октябрь 2026, данные по 19.10.2026».
  "{month} {year}, данные по {date}": "{month} {year}, {date} holatiga ma'lumot",
};

export default dict;
