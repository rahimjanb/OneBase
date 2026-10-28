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
  "Торговые представители (ТП): план и факт в кг, выполнение, прогноз, выручка, АКБ агента и выручка с одной точки, визиты, заказы, страйк, категории, замечания. С регионом — вся команда региона; без региона — лучшие и худшие ТП республики по прогнозу выполнения.":
    "Savdo vakillari (TP): kg da reja va fakt, bajarilish, prognoz, tushum, agent AKB va bitta nuqtaga tushum, tashriflar, buyurtmalar, strayk, toifalar, e'tirozlar. Mintaqa bilan — mintaqaning butun jamoasi; mintaqasiz — bajarilish prognozi bo'yicha respublikaning eng yaxshi va eng yomon TP lari.",
  "ТП с замечаниями за месяц (относительно медианы своего региона): низкий страйк, визиты без продаж, малый чек, узкий ассортимент, падение темпа. У каждого — регион, страйк, визиты, выручка и объяснение замечаний.":
    "Oy davomida e'tirozli TP lar (o'z mintaqasi medianasiga nisbatan): past strayk, sotuvsiz tashriflar, kichik chek, tor assortiment, sur'at pasayishi. Har birida — mintaqa, strayk, tashriflar, tushum va e'tirozlar izohi.",
  "Товары и категории": "Tovarlar va toifalar",
  "Ассортимент за месяц по республике или региону: все категории (кг, выручка, доля, АКБ, дистрибуция, изменение к прошлому месяцу, «молчащие» и пропавшие SKU), лучшие и худшие товары по выручке, пропавшие SKU. Конкретный товар, категорию или бренд по названию — find_products.":
    "Respublika yoki mintaqa bo'yicha oylik assortiment: barcha toifalar (kg, tushum, ulush, AKB, distributsiya, o'tgan oyga nisbatan o'zgarish, «jim» va yo'qolgan SKU), tushum bo'yicha eng yaxshi va eng yomon tovarlar, yo'qolgan SKU. Aniq tovar, toifa yoki brend nomi bo'yicha — find_products.",
  "Клиенты (АКБ)": "Mijozlar (AKB)",
  "Клиенты — торговые точки: АКБ по месяцам года (точки с чистой покупкой > 0); по регионам республики или, с регионом, по ТП региона — база прошлого месяца, «молчат» (покупали в прошлом месяце и не купили в этом, с выручкой прошлого месяца), новые точки, АКБ и выручка текущего месяца и выручка с одной точки.":
    "Mijozlar — savdo nuqtalari: yil oylari bo'yicha AKB (sof xaridi > 0 bo'lgan nuqtalar); respublika mintaqalari bo'yicha yoki mintaqa bilan — mintaqa TP lari bo'yicha: o'tgan oy bazasi, «jim» nuqtalar (o'tgan oy xarid qilib, bu oy xarid qilmagan, o'tgan oy tushumi bilan), yangi nuqtalar, joriy oy AKB va tushumi hamda bitta nuqtaga tushum.",
  "Клиенты, которые не купили": "Xarid qilmagan mijozlar",
  "Клиенты — торговые точки, которые покупали категорию, бренд или товар в базовом периоде и ещё не купили их в выбранном месяце (или купили — status), списком: точка, регион, ТП, кг и выручка в базе, место по объёму, последняя покупка, сколько взяли в этом месяце, покупают ли другие товары, визиты в этом месяце; итоги — сколько точек в базе, сколько купили и не купили, их кг и выручка, новые точки. Примеры: «кто из клиентов по помадке, покупавших в сентябре, не купил в октябре», «кто из крупнейших клиентов по могулу ещё не взял», «какие точки перестали брать Yamelly», «кто из точек ТП … не купил шоколад». База — прошлый месяц (base_months — до 3 месяцев); largest — только N крупнейших точек базы по кг. Без category и product — все товары.":
    "Mijozlar — bazaviy davrda toifa, brend yoki tovarni olgan va tanlangan oyda hali olmagan (yoki olgan — status) savdo nuqtalari, ro'yxat bilan: nuqta, mintaqa, TP, bazadagi kg va tushum, hajm bo'yicha o'rni, oxirgi xarid, shu oyda qancha olgani, boshqa tovarlarni olayotgani, shu oydagi tashriflar; jami — bazada nechta nuqta, nechtasi oldi va olmadi, ularning kg va tushumi, yangi nuqtalar. Misollar: «sentabrda pomadka olgan mijozlardan kim oktabrda olmadi», «mogul bo'yicha eng yirik mijozlardan kim hali olmadi», «qaysi nuqtalar Yamelly olishni to'xtatdi», «TP … nuqtalaridan kim shokolad olmadi». Baza — o'tgan oy (base_months — 3 oygacha); largest — bazadagi kg bo'yicha faqat N ta eng yirik nuqta. category va product siz — barcha tovarlar.",
  "Категория или тип Linko: «помадка», «могул», «шоколад»; несколько — через запятую. Бренд или SKU — в product.":
    "Toifa yoki Linko turi: «pomadka», «mogul», «shokolad»; bir nechta — vergul bilan. Brend yoki SKU — product da.",
  "Товар, бренд или артикул: «Yamelly», «ARBUZ», «480». Вместе с category — товары внутри категории.":
    "Tovar, brend yoki artikul: «Yamelly», «ARBUZ», «480». category bilan birga — toifa ichidagi tovarlar.",
  "Месяц, в котором проверяем покупку (1–12). Без него — текущий; база — месяцы перед ним.":
    "Xaridni tekshiradigan oy (1–12). Usiz — joriy oy; baza — undan oldingi oylar.",
  "ТП: имя или id Linko — только точки, закреплённые за ним (кто продавал им больше всех в базе).":
    "TP: ismi yoki Linko id — faqat unga biriktirilgan nuqtalar (bazada ularga eng ko'p sotgan).",
  "База — сколько месяцев перед month: 1 (по умолчанию — прошлый месяц), 2 или 3.": "Baza — month dan oldingi necha oy: 1 (standart — o'tgan oy), 2 yoki 3.",
  "Только N крупнейших точек базы по кг (для «крупнейших клиентов»), 1–500.":
    "Bazadagi kg bo'yicha faqat N ta eng yirik nuqta («eng yirik mijozlar» uchun), 1–500.",
  "not_bought — ещё не купили (по умолчанию), bought — купили, all — все точки базы.":
    "not_bought — hali olmagan (standart), bought — olgan, all — bazadagi barcha nuqtalar.",
  "Сколько точек в списке, по умолчанию 30, до 50.": "Ro'yxatda nechta nuqta, standart 30, 50 tagacha.",
  // Лидеры месяца HR, находки AI, проекты отделов, прайс-листы Linko
  "HR: лидеры месяца": "HR: oy yetakchilari",
  "«Лидеры месяца» — как блок на главной OneBase: работающие сотрудники, которые за месяц ни разу не опоздали, по отработанным часам (больше — выше, при равенстве — больше дней с приходом): место, ФИО, подразделение, должность, отработано и по графику в часах, дни с приходом и по графику; сколько всего работающих и сколько из них без опозданий. Период — с 1-го числа по сегодня, в первые 3 дня месяца — прошлый месяц. Вопросы: «кто лучший по дисциплине», «кто ни разу не опоздал в этом месяце», «топ сотрудников по отработанным часам».":
    "«Oy yetakchilari» — OneBase bosh sahifasidagi blok kabi: oy davomida bir marta ham kechikmagan ishlayotgan xodimlar, ishlangan soatlar bo'yicha (ko'pi — yuqorida, teng bo'lsa — kelgan kunlari ko'pi): o'rni, F.I.Sh., bo'linma, lavozim, ishlangan va jadval bo'yicha soatlar, kelgan kunlar va jadval bo'yicha kunlar; jami ishlayotganlar va ulardan kechikmaganlar soni. Davr — oyning 1-sanasidan bugungacha, oyning dastlabki 3 kunida — o'tgan oy. Savollar: «intizom bo'yicha kim eng yaxshi», «bu oyda kim bir marta ham kechikmadi», «ishlangan soatlar bo'yicha top xodimlar».",
  "Сколько лидеров: по умолчанию 10, до 50.": "Nechta yetakchi: standart 10, 50 tagacha.",
  "Находки AI («Фокус дня»)": "AI topilmalari («Kun fokusi»)",
  "Находки проактивного анализа OneBase — «Фокус дня» на главной и «AI → Находки»: что сейчас требует внимания (критичные, предупреждения) и возможности по отделам (продажи, финансы, маркетинг, HR, производство, снабжение): заголовок, суть с цифрами, рекомендация, ссылка на страницу, когда найдено и подтверждено в последний раз; resolved — закрытые за 30 дней. Только находки, к данным которых у пользователя есть доступ. Вопросы: «что сейчас требует внимания», «какие риски», «что важного нашёл AI», «какие есть возможности».":
    "OneBase proaktiv tahlili topilmalari — bosh sahifadagi «Kun fokusi» va «AI → Topilmalar»: hozir e'tibor talab qiladigan narsalar (kritik, ogohlantirishlar) va bo'limlar bo'yicha imkoniyatlar (savdo, moliya, marketing, HR, ishlab chiqarish, ta'minot): sarlavha, raqamlar bilan mazmuni, tavsiya, sahifaga havola, qachon topilgani va oxirgi marta tasdiqlangani; resolved — 30 kun ichida yopilganlari. Faqat foydalanuvchi ma'lumotlariga kira oladigan topilmalar. Savollar: «hozir nimaga e'tibor berish kerak», «qanday xavflar bor», «AI nimani muhim deb topdi», «qanday imkoniyatlar bor».",
  "Отдел: sales, finance, marketing, hr, production, supply; all или без него — все.":
    "Bo'lim: sales, finance, marketing, hr, production, supply; all yoki usiz — hammasi.",
  "critical — критичные, warning — предупреждения, opportunity — возможности, all — все (по умолчанию).":
    "critical — kritik, warning — ogohlantirishlar, opportunity — imkoniyatlar, all — hammasi (standart).",
  "true — закрытые за последние 30 дней вместо действующих.": "true — amaldagilar o'rniga so'nggi 30 kunda yopilganlari.",
  "Сколько находок: по умолчанию 20, до 50.": "Nechta topilma: standart 20, 50 tagacha.",
  "Проекты отделов": "Bo'lim loyihalari",
  "Проекты раздела «Задачи» OneBase: название, отдел, описание, руководитель проекта (PM) и участники, задачи проекта — открыто, просрочено, на проверке, заблокировано, выполнено за 30 дней, ближайший срок. Только проекты, которые видит пользователь (проекты его отделов и те, где он участник; руководство — все). Вопросы: «какие у нас проекты», «кто ведёт проект …», «где в проектах просрочки». Задачи по отделам — get_department_tasks.":
    "OneBase «Vazifalar» bo'limi loyihalari: nomi, bo'limi, tavsifi, loyiha rahbari (PM) va ishtirokchilar, loyiha vazifalari — ochiq, muddati o'tgan, tekshiruvda, bloklangan, 30 kunda bajarilgan, eng yaqin muddat. Faqat foydalanuvchi ko'radigan loyihalar (o'z bo'limlari loyihalari va o'zi ishtirok etadiganlari; rahbariyat — hammasi). Savollar: «bizda qanday loyihalar bor», «… loyihasini kim olib boradi», «loyihalarda qayerda muddat o'tgan». Bo'limlar bo'yicha vazifalar — get_department_tasks.",
  "Часть названия проекта или имени участника. Без него — все видимые проекты.":
    "Loyiha nomi yoki ishtirokchi ismining bir qismi. Usiz — barcha ko'rinadigan loyihalar.",
  "Отдел проекта: код (sales, finance…) или название. Без него — все отделы.": "Loyiha bo'limi: kodi (sales, finance…) yoki nomi. Usiz — barcha bo'limlar.",
  "true — вместе с архивными проектами. По умолчанию — только действующие.": "true — arxivdagi loyihalar bilan birga. Standart — faqat amaldagilar.",
  "Сколько проектов: по умолчанию 20, до 50.": "Nechta loyiha: standart 20, 50 tagacha.",
  "Прайс-листы Linko": "Linko narx varaqlari",
  "Цены товаров по прайс-листам Linko (дилерский, магазинный и другие — как их ведёт Linko): по каждому найденному товару — цена в каждом прайс-листе и валюта; без товара — список прайс-листов с числом товаров. Поиск товара — product (название, бренд или артикул: «ARBUZ», «Yamelly», «480»), category (категория или тип Linko: «помадка»), price_list (часть названия прайс-листа). Это цены прайса, а не фактические: выручка и цена за кг по продажам — get_sku_sales и find_products.":
    "Linko narx varaqlari bo'yicha tovar narxlari (diler, do'kon va boshqalar — Linko qanday yuritsa): topilgan har bir tovar uchun — har bir narx varag'idagi narx va valyuta; tovarsiz — tovarlar soni bilan narx varaqlari ro'yxati. Tovar qidiruvi — product (nomi, brend yoki artikul: «ARBUZ», «Yamelly», «480»), category (toifa yoki Linko turi: «pomadka»), price_list (narx varag'i nomining bir qismi). Bu narx varag'i narxlari, haqiqiy emas: savdo bo'yicha tushum va kg narxi — get_sku_sales va find_products.",
  "Товар, бренд или артикул: «ARBUZ», «Yamelly», «480».": "Tovar, brend yoki artikul: «ARBUZ», «Yamelly», «480».",
  "Категория отчёта или тип Linko: «помадка», «шоколад», «Помадка 1 кг».": "Hisobot toifasi yoki Linko turi: «pomadka», «shokolad», «Pomadka 1 kg».",
  "Прайс-лист: часть названия. Без него — все прайс-листы.": "Narx varag'i: nomining bir qismi. Usiz — barcha narx varaqlari.",
  "Сколько товаров: по умолчанию 20, до 60.": "Nechta tovar: standart 20, 60 tagacha.",
  "Что проактивный анализ OneBase нашёл в данных отделов: критичные риски, предупреждения и возможности с рекомендациями, когда найдено и закрыто.":
    "OneBase proaktiv tahlili bo'limlar ma'lumotlarida nimani topgani: kritik xavflar, ogohlantirishlar va tavsiyali imkoniyatlar, qachon topilgani va yopilgani.",
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

  // Консультант: проверка ответа по данным (тексты сервера)
  "чисел сверено с данными: {n}": "ma'lumotlar bilan solishtirilgan sonlar: {n}",
  "Пользователь оспаривает прошлый ответ — AI-сотрудники перепроверяют данные.":
    "Foydalanuvchi oldingi javobga e'tiroz bildirmoqda — AI-xodimlar ma'lumotlarni qayta tekshiradi.",
  "Вопрос о данных компании — ответ только по данным OneBase.":
    "Savol kompaniya ma'lumotlari haqida — javob faqat OneBase ma'lumotlari bo'yicha.",
  "Точный расчёт": "Aniq hisob",
  "Источник истины": "Haqiqat manbai",
  // Инструменты детализации до записей (DrillTools.cs).
  "Карточка ТП": "TP kartochkasi",
  "Один торговый представитель за месяц — всё, что на его странице в OneBase: регион, план и факт (кг и выручка), выполнение, прогноз, АКБ, выручка и кг на точку, заказы, визиты и визиты с заказом, конверсия, сумма на визит, средний чек (с медианой региона), категории и план по ним, показатели Linko, замечания, молчащие точки (с выручкой прошлого месяца), новые точки, ассортимент, АКБ по месяцам, сравнение с теми же днями прошлого месяца.":
    "Bitta savdo vakili oy bo'yicha — OneBase'dagi sahifasidagi hamma narsa: mintaqa, reja va fakt (kg va tushum), bajarilish, prognoz, AKB, nuqtaga tushum va kg, buyurtmalar, tashriflar va buyurtmali tashriflar, konversiya, tashrifga summa, o'rtacha chek (mintaqa medianasi bilan), toifalar va ular bo'yicha reja, Linko ko'rsatkichlari, e'tirozlar, jim nuqtalar (o'tgan oy tushumi bilan), yangi nuqtalar, assortiment, oylar bo'yicha AKB, o'tgan oyning shu kunlari bilan solishtirish.",
  "Визиты ТП по точкам": "TP tashriflari nuqtalar bo'yicha",
  "Куда ходил торговый представитель: каждый визит Linko за день или отрезок до 31 дня — время, точка (название, адрес), по маршруту (в плане) или вне маршрута, выполнен ли, был ли в этот день в точке заказ этого ТП (число, сумма, кг, статус). Итоги: план, по маршруту, вне маршрута, не посещено, заказы с визитов и заказы без визита. Без дат — сегодня.":
    "Savdo vakili qayerga borgan: kun yoki 31 kungacha davr uchun Linko'ning har bir tashrifi — vaqt, nuqta (nomi, manzili), marshrut bo'yicha (rejada) yoki marshrutdan tashqari, bajarilganmi, shu kuni nuqtada shu TP buyurtmasi bo'lganmi (soni, summa, kg, holat). Jami: reja, marshrut bo'yicha, marshrutdan tashqari, borilmagan, tashriflardan buyurtmalar va tashrifsiz buyurtmalar. Sanalarsiz — bugun.",
  "Карточка торговой точки": "Savdo nuqtasi kartochkasi",
  "Торговая точка (магазин) по названию, адресу или id Linko: регион, ответственный ТП, адрес и координаты, продажи за месяц (кг, выручка, заказы, категории, товары, прошлый месяц), кто из ТП продавал, последние визиты (кто, когда, по маршруту ли) и последние заказы. Если подходит несколько точек — список, чтобы выбрать.":
    "Savdo nuqtasi (do'kon) nomi, manzili yoki Linko id bo'yicha: mintaqa, mas'ul TP, manzil va koordinatalar, oylik savdo (kg, tushum, buyurtmalar, toifalar, mahsulotlar, o'tgan oy), qaysi TP lar sotgan, oxirgi tashriflar (kim, qachon, marshrut bo'yichami) va oxirgi buyurtmalar. Bir nechta nuqta mos kelsa — tanlash uchun ro'yxat.",
  "Отдельные заказы Linko: по ТП, торговой точке, региону, статусу и дням создания (до 62 дней; без дат — последние 7) или один заказ по номеру. У заказа — дата создания и приёмки, статус, точка, ТП, сумма, скидка, вес; с lines=true или по номеру — позиции (товар, количество, возврат, цена, сумма, кг). Итоги — по всем найденным заказам, по статусам. Факт продаж OneBase считает по приёмке и статусам доставки, поэтому сумма списка может отличаться от факта.":
    "Linko'ning alohida buyurtmalari: TP, savdo nuqtasi, mintaqa, holat va yaratilgan kunlar bo'yicha (62 kungacha; sanalarsiz — oxirgi 7 kun) yoki raqami bo'yicha bitta buyurtma. Buyurtmada — yaratilgan va qabul qilingan sana, holat, nuqta, TP, summa, chegirma, vazn; lines=true yoki raqam bo'yicha — pozitsiyalar (mahsulot, miqdor, qaytarish, narx, summa, kg). Jami — topilgan barcha buyurtmalar bo'yicha, holatlar bo'yicha. OneBase savdo faktini qabul qilish va yetkazish holatlari bo'yicha hisoblaydi, shuning uchun ro'yxat summasi faktdan farq qilishi mumkin.",
  "KPI сотрудников": "Xodimlar KPI",
  "Страница «KPI»: показатели Linko по людям (ТП и супервайзеры) — план, продажи, возвраты, факт и выполнение по каждому показателю, пороги премии; с правом «Финансы» — оклад, премии (начислено и прогноз) и фонд оплаты по регионам и направлениям. Без региона и человека — итоги по регионам; с region — люди региона; с person — один человек.":
    "«KPI» sahifasi: odamlar (TP va supervayzerlar) bo'yicha Linko ko'rsatkichlari — har bir ko'rsatkich bo'yicha reja, savdo, qaytarishlar, fakt va bajarilish, mukofot chegaralari; «Moliya» huquqi bilan — mintaqalar va yo'nalishlar bo'yicha oklad, mukofotlar (hisoblangan va prognoz) va ish haqi fondi. Mintaqa va odamsiz — mintaqalar bo'yicha jami; region bilan — mintaqa odamlari; person bilan — bitta odam.",
  "Акции и скидки": "Aksiyalar va chegirmalar",
  "Страница «Акции»: скидки и бонусы вторички по акциям Linko за месяц — плитки, на что обратить внимание, выводы, акции (сумма скидки, точки, заказы), эффект акций на продажи, регионы, ручные скидки, календарь, ТП и точки с наибольшими скидками. rm — одно направление (РМ).":
    "«Aksiyalar» sahifasi: oy bo'yicha Linko aksiyalari bo'yicha ikkilamchi savdo chegirmalari va bonuslari — plitkalar, nimaga e'tibor berish kerak, xulosalar, aksiyalar (chegirma summasi, nuqtalar, buyurtmalar), aksiyalarning savdoga ta'siri, mintaqalar, qo'lda chegirmalar, kalendar, eng ko'p chegirmali TP va nuqtalar. rm — bitta yo'nalish (RM).",
  "Страница «Акт сверки»: взаиморасчёты с дилерами и контрагентами за месяц — остаток на начало, отгрузки и возвраты Linko, касса и банк, расходы завода (ретробонус, ЗП, логистика, акции, кешбэк), остаток на конец, проверки акта. С dealer — карточка контрагента с документами, оплатами и расходами по дням.":
    "«Solishtirma dalolatnoma» sahifasi: oy bo'yicha dilerlar va kontragentlar bilan o'zaro hisob-kitoblar — boshlang'ich qoldiq, Linko jo'natmalari va qaytarishlari, kassa va bank, zavod xarajatlari (retrobonus, ish haqi, logistika, aksiyalar, keshbek), oxirgi qoldiq, dalolatnoma tekshiruvlari. dealer bilan — kunlar bo'yicha hujjatlar, to'lovlar va xarajatlar bilan kontragent kartochkasi.",
  "DSO — долги дилеров": "DSO — dilerlar qarzlari",
  "Страница «DSO»: долг дилеров по FIFO (неоплаченные отгрузки), просрочка по норме дней, оплачено и отгружено за месяц, ожидаемые оплаты на неделю, дилеры (долг, просрочка, норма, факт дней), недели, оплаты по месяцам и каналы. С dealer — один дилер.":
    "«DSO» sahifasi: FIFO bo'yicha dilerlar qarzi (to'lanmagan jo'natmalar), kunlar normasi bo'yicha muddati o'tgani, oy uchun to'langan va jo'natilgan, haftaga kutilayotgan to'lovlar, dilerlar (qarz, muddati o'tgani, norma, kunlar fakti), haftalar, oylar bo'yicha to'lovlar va kanallar. dealer bilan — bitta diler.",
  // Маршрут, продажи по дням, Telegram, описания инструментов и их аргументов (Tools/Data/*.cs).
  "Визиты и маршрут":
    "Tashriflar va marshrut",
  "Маршрут и визиты за день, отрезок дней или месяц — по республике (регионы), направлению РМ (регионы), региону (каждый ТП) или одному ТП. У каждой строки: визиты — план (в маршруте), выполнено по маршруту, вне маршрута, не посещено, % выполнения плана; визиты с заказом — по маршруту и вне маршрута; продажи — заказы, выручка (сум), кг и АКБ (точки с чистой покупкой) отдельно с визитов по маршруту, с визитов вне маршрута и без визита, возвраты и итог (= факт «Продаж» за эти дни), доля выручки с маршрута, страйк (заказы ÷ выполненные визиты). agents=true — строки ТП и у республики и направления. Отрезок — в пределах одного месяца; без дат — месяц по отчётный день.":
    "Kun, kunlar oralig'i yoki oy bo'yicha marshrut va tashriflar — respublika (mintaqalar), RM yo'nalishi (mintaqalar), mintaqa (har bir TP) yoki bitta TP bo'yicha. Har bir qatorda: tashriflar — reja (marshrutda), marshrut bo'yicha bajarilgan, marshrutdan tashqari, borilmagan, rejaning bajarilishi %; buyurtmali tashriflar — marshrut bo'yicha va marshrutdan tashqari; savdo — buyurtmalar, tushum (so'm), kg va AKB (sof xaridli nuqtalar) alohida: marshrut bo'yicha tashriflardan, marshrutdan tashqari tashriflardan va tashrifsiz, qaytarishlar va jami (= shu kunlardagi «Savdo» fakti), marshrutdan tushum ulushi, strayk (buyurtmalar ÷ bajarilgan tashriflar). agents=true — respublika va yo'nalishda ham TP qatorlari. Oraliq — bitta oy ichida; sanalarsiz — hisobot kunigacha oy.",
  "Вторичные продажи за день или отрезок дней одного месяца — по республике, направлению РМ, региону или ТП: факт за отрезок (кг, выручка в сумах, заказы, АКБ — точки с чистой покупкой за отрезок, возвраты), каждый день отрезка (кг, выручка, заказы, АКБ дня, выполненные визиты) и строки регионов (у республики и направления) или ТП (у региона; agents=true — ТП и шире). Правила — как в «Продажах»: заказы − возвраты по дате приёмки, без филиала «Завод»; сегодняшний день — на время последней синхронизации Linko. Без дат — месяц по отчётный день.":
    "Bir oy ichidagi kun yoki kunlar oralig'i uchun ikkilamchi savdo — respublika, RM yo'nalishi, mintaqa yoki TP bo'yicha: oraliq fakti (kg, so'mda tushum, buyurtmalar, AKB — oraliqda sof xaridli nuqtalar, qaytarishlar), oraliqning har bir kuni (kg, tushum, buyurtmalar, kun AKB si, bajarilgan tashriflar) va mintaqalar qatorlari (respublika va yo'nalishda) yoki TP lar (mintaqada; agents=true — TP lar kengroq ham). Qoidalar — «Savdo»dagidek: qabul sanasi bo'yicha buyurtmalar − qaytarishlar, «Zavod» filialisiz; bugungi kun — Linko bilan oxirgi sinxronlash vaqtiga. Sanalarsiz — hisobot kunigacha oy.",
  "Куда ходил торговый представитель: каждый визит Linko за день или отрезок до 31 дня — время, точка (название, адрес), по маршруту (в плане) или вне маршрута, выполнен ли, был ли в этот день в точке заказ этого ТП (число, сумма, кг, статус). Итоги и разбивка по дням — полные (не зависят от длины списка визитов): план, по маршруту, вне маршрута, не посещено, визиты с заказом по маршруту и вне, заказы (число, сумма, кг, точки) с визитов по маршруту, вне маршрута и без визита. Заказы — введённые ТП в эти дни (любой статус, кроме отменённых и недоставленных). Без дат — сегодня. Итоги «Продаж» (принятые заказы, выручка, АКБ) по маршруту за отрезок — get_visits.":
    "Savdo vakili qayerga borgan: kun yoki 31 kungacha davr uchun Linko'ning har bir tashrifi — vaqt, nuqta (nomi, manzili), marshrut bo'yicha (rejada) yoki marshrutdan tashqari, bajarilganmi, shu kuni nuqtada shu TP ning buyurtmasi bo'lganmi (soni, summasi, kg, holati). Jamilar va kunlar bo'yicha taqsimot — to'liq (tashriflar ro'yxati uzunligiga bog'liq emas): reja, marshrut bo'yicha, marshrutdan tashqari, borilmagan, marshrut bo'yicha va tashqari buyurtmali tashriflar, marshrut bo'yicha tashriflardan, marshrutdan tashqari va tashrifsiz buyurtmalar (soni, summasi, kg, nuqtalar). Buyurtmalar — TP shu kunlarda kiritganlari (bekor qilingan va yetkazilmaganlardan tashqari har qanday holat). Sanalarsiz — bugun. Davr uchun marshrut bo'yicha «Savdo» jamilari (qabul qilingan buyurtmalar, tushum, AKB) — get_visits.",
  "Итоги вторичных продаж за месяц по республике, направлению РМ (с его регионами) или региону: факт и план в кг, выполнение, прогноз на конец месяца, выручка в сумах, АКБ (активные торговые точки), страйк, визиты, число ТП; сравнение с теми же днями прошлого месяца; экспорт и опт (филиал «Завод») отдельно. За день или отрезок дней — get_sales_days.":
    "Respublika, RM yo'nalishi (mintaqalari bilan) yoki mintaqa bo'yicha oylik ikkilamchi savdo yakunlari: kg da fakt va reja, bajarilish, oy oxiriga prognoz, so'mda tushum, AKB (faol savdo nuqtalari), strayk, tashriflar, TP soni; o'tgan oyning shu kunlari bilan taqqoslash; eksport va ulgurji («Zavod» filiali) alohida. Kun yoki kunlar oralig'i uchun — get_sales_days.",
  "Telegram: автоотчёты и рассылки":
    "Telegram: avtomatik hisobotlar va tarqatmalar",
  "Автоматические отчёты в Telegram: аккаунт и язык, 13 запусков шести отчётов (включён ли, время, следующий запуск, последний запуск с итогом: отправлено / ошибок / пропущено), получатели (роль РМ/СВ/ТП, область, чаты, отчёты, включён ли), отправки за последние дни по статусам и список неудачных и «неизвестно, дошёл ли» с причиной. Рассылки: название, статус, время, сколько получателей, отправлено, ошибок, в очереди, последняя ошибка. Аргумент days — за сколько дней (1–30, по умолчанию 7).":
    "Telegram'dagi avtomatik hisobotlar: akkaunt va til, oltita hisobotning 13 ta ishga tushishi (yoqilganmi, vaqt, keyingi ishga tushish, natijali oxirgi ishga tushish: yuborildi / xatolar / o'tkazib yuborildi), qabul qiluvchilar (RM/SV/TP roli, hudud, chatlar, hisobotlar, yoqilganmi), oxirgi kunlardagi yuborishlar holatlar bo'yicha va muvaffaqiyatsiz hamda «yetib bordimi — noma'lum» ro'yxati sababi bilan. Tarqatmalar: nomi, holati, vaqti, qabul qiluvchilar soni, yuborildi, xatolar, navbatda, oxirgi xato. days argumenti — necha kun uchun (1–30, standart 7).",
  "Автоматические отчёты в Telegram (расписание 13 запусков, получатели РМ/СВ/ТП, отправки и ошибки) и рассылки (статусы, доставка).":
    "Telegram'dagi avtomatik hisobotlar (13 ta ishga tushish jadvali, RM/SV/TP qabul qiluvchilari, yuborishlar va xatolar) va tarqatmalar (holatlar, yetkazish).",
  "Продажи по SKU за период":
    "Davr uchun SKU bo'yicha savdo",
  "Продажи по SKU за период из месяцев (вторичка, как страница «Продажи → Продажи по SKU»): итоги — кг, выручка в сумах, цена за кг, SKU с продажами, регионы; ABC по весу (A — первые 80% кг, B — до 95%, C — остальные), концентрация (главный SKU, доли топ-5/10/25), кг по месяцам, категории, регионы; топ SKU по весу — кг, выручка, доля, ABC, точки (сколько ТТ купили SKU за период), регионов с продажами, динамика (кг в день последнего квартала периода к первому); «новые» SKU (в первом квартале продаж не было) и падающие (−10% и больше). По умолчанию — последние 3 месяца по месяц последних данных (если они в одном квартале — 4, чтобы считалась динамика), вся страна, все SKU. Фильтры: регион, категории, ТОП, группа ABC, поиск SKU по названию или коду.":
    "Oylardan iborat davr uchun SKU bo'yicha savdo (ikkilamchi, «Savdo → SKU bo'yicha savdo» sahifasidagidek): jamilar — kg, so'mda tushum, kg narxi, savdosi bor SKU lar, mintaqalar; vazn bo'yicha ABC (A — birinchi 80% kg, B — 95% gacha, C — qolganlari), konsentratsiya (asosiy SKU, top-5/10/25 ulushlari), oylar bo'yicha kg, toifalar, mintaqalar; vazn bo'yicha top SKU — kg, tushum, ulush, ABC, nuqtalar (davrda SKU ni nechta SN sotib olgan), savdosi bor mintaqalar, dinamika (davrning oxirgi choragida kunlik kg birinchisiga nisbatan); «yangi» SKU lar (birinchi chorakda savdo bo'lmagan) va tushayotganlar (−10% va undan ko'p). Standart — oxirgi ma'lumotlar oyigacha oxirgi 3 oy (agar ular bitta chorakda bo'lsa — dinamika hisoblanishi uchun 4), butun mamlakat, barcha SKU lar. Filtrlar: mintaqa, toifalar, TOP, ABC guruhi, SKU ni nomi yoki kodi bo'yicha qidirish.",
  "Экспорт: страны и филиал «Завод»":
    "Eksport: mamlakatlar va «Zavod» filiali",
  "Экспорт за месяц (вес — кг, суммы — сум). По странам — отгрузки завода на экспорт («Первичка → Экспорт»: заказы филиала «Завод» экспортным точкам): кг, сумма, доля, точки страны, прошлый месяц и изменение, с начала года; итог экспорта, прогноз, по месяцам года, категории. Отдельно — продажи филиала «Завод» («Продажи → Экспорт и опт»: экспорт и крупный опт): кг, выручка, заказы, точки, прогноз, к прошлому месяцу, покупатели (со страной, если точка экспортная), агенты, категории, товары, суммы в другой валюте. Плана экспорта в OneBase нет. Во вторичку республики экспорт не входит.":
    "Oylik eksport (vazn — kg, summalar — so'm). Mamlakatlar bo'yicha — zavodning eksportga jo'natmalari («Birlamchi → Eksport»: «Zavod» filialining eksport nuqtalariga buyurtmalari): kg, summa, ulush, mamlakat nuqtalari, o'tgan oy va o'zgarish, yil boshidan; eksport jami, prognoz, yil oylari bo'yicha, toifalar. Alohida — «Zavod» filiali savdosi («Savdo → Eksport va ulgurji»: eksport va yirik ulgurji): kg, tushum, buyurtmalar, nuqtalar, prognoz, o'tgan oyga nisbatan, xaridorlar (nuqta eksport bo'lsa — mamlakati bilan), agentlar, toifalar, mahsulotlar, boshqa valyutadagi summalar. OneBase'da eksport rejasi yo'q. Respublika ikkilamchisiga eksport kirmaydi.",
  "P&L дилеров (страница «Продажи → P&L дилеров») — прибыль завода на дилере, все суммы в сумах: продажи (выручка), себестоимость, валовая прибыль и маржа, расходы (отдел продаж и маркетинга, ретробонусы, доставка и склад, административные, прочие, амортизация), прибыль после продаж и ROS (рентабельность продаж), EBIT, чистая прибыль EAT (если загружены банковские услуги и налоги) — итог, по РМ и по дилерам с изменением к прошлому месяцу. За месяц (year+month; без них — последний месяц с продажами и расходами, как на странице) или за период (from/to ГГГГ-ММ, либо year без month — сумма загруженных месяцев и помесячная динамика). dealer — один дилер подробно. Если P&L за месяц не загружен — ответ скажет об этом (загрузка — через AI Data Manager).":
    "Dilerlar P&L («Savdo → Dilerlar P&L» sahifasi) — zavodning dilerdagi foydasi, barcha summalar so'mda: savdo (tushum), tannarx, yalpi foyda va marja, xarajatlar (savdo va marketing bo'limi, retrobonuslar, yetkazib berish va ombor, ma'muriy, boshqa, amortizatsiya), savdodan keyingi foyda va ROS (savdo rentabelligi), EBIT, sof foyda EAT (bank xizmatlari va soliqlar yuklangan bo'lsa) — jami, RM va dilerlar bo'yicha o'tgan oyga nisbatan o'zgarish bilan. Oy uchun (year+month; ularsiz — sahifadagidek savdo va xarajatli oxirgi oy) yoki davr uchun (from/to YYYY-OO yoki month siz year — yuklangan oylar yig'indisi va oylik dinamika). dealer — bitta diler batafsil. Oy uchun P&L yuklanmagan bo'lsa — javob shuni aytadi (yuklash — AI Data Manager orqali).",
  "Рекомендуемый остаток: запас на складах регионов (кг, коробки, стоимость по входной цене дилера) и скорость продаж за последние 87 дней с поправкой на дни без товара: дни покрытия, запас на 15 и 30 дней, рекомендуемый заказ дилера, дефицит (меньше 15 дней продаж), затоварка (больше 30 дней), «не продаётся» (остаток есть, продаж нет), «нет остатка» (ни остатка, ни продаж); отдельно — склад завода (его запас меряется скоростью всей страны). Можно по одному региону.":
    "Tavsiya etilgan qoldiq: mintaqalar omborlaridagi zaxira (kg, qutilar, dilerning kirish narxi bo'yicha qiymat) va tovarsiz kunlarga tuzatish bilan oxirgi 87 kundagi savdo tezligi: qamrov kunlari, 15 va 30 kunlik zaxira, dilerga tavsiya etilgan buyurtma, taqchillik (15 kunlik savdodan kam), ortiqcha zaxira (30 kundan ko'p), «sotilmaydi» (qoldiq bor, savdo yo'q), «qoldiq yo'q» (na qoldiq, na savdo); alohida — zavod ombori (uning zaxirasi butun mamlakat tezligi bilan o'lchanadi). Bitta mintaqa bo'yicha ham mumkin.",
  "Задачи отделов":
    "Bo'limlar vazifalari",
  "Задачи отделов OneBase (раздел «Задачи»). Возвращает: по каждому отделу, доступному пользователю, — сколько задач открыто, просрочено и выполнено за последние days дней (по умолчанию 30); список задач — название, отдел, исполнитель, автор, приоритет, срок (yyyy-MM-dd), статус, просрочка в днях, дата и итог выполнения или причина отмены. Аргументы: department — код (sales, production, supply, finance, marketing, hr) или название отдела, без него — все доступные отделы; status — open (по умолчанию) / overdue / done / cancelled / all; assignee — исполнитель (имя или «me»); days; limit — сколько задач в списке (20). Задачи отдела видят его сотрудники и руководство — к чужим отделам доступа нет. Задачи торговых агентов и супервайзеров — не здесь, а в get_field_tasks.":
    "OneBase bo'limlari vazifalari («Vazifalar» bo'limi). Qaytaradi: foydalanuvchiga ochiq har bir bo'lim bo'yicha — oxirgi days kunda (standart 30) nechta vazifa ochiq, muddati o'tgan va bajarilgan; vazifalar ro'yxati — nomi, bo'lim, ijrochi, muallif, ustuvorlik, muddat (yyyy-MM-dd), holat, kunlarda kechikish, bajarilish sanasi va natijasi yoki bekor qilish sababi. Argumentlar: department — kod (sales, production, supply, finance, marketing, hr) yoki bo'lim nomi, usiz — barcha ochiq bo'limlar; status — open (standart) / overdue / done / cancelled / all; assignee — ijrochi (ism yoki «me»); days; limit — ro'yxatda nechta vazifa (20). Bo'lim vazifalarini uning xodimlari va rahbariyati ko'radi — boshqa bo'limlarga kirish yo'q. Savdo agentlari va supervayzerlar vazifalari — bu yerda emas, get_field_tasks da.",
  "Sales Base: задачи полевой команды":
    "Sales Base: dala jamoasi vazifalari",
  "Задачи полевой команды продаж (Sales Base) — агентов и супервайзеров. Возвращает: сводку — открыто, просрочено, на сегодня, отложено, поставлено AI, выполнено за days дней; разбивку по исполнителям (открыто, просрочено, выполнено); список задач — название, исполнитель и команда, торговая точка, приоритет, срок (yyyy-MM-dd), статус, просрочка в днях, кто поставил (AI-планирование, супервайзер, РМ, система, офис), причина постановки (у задач AI — причина рекомендации), итог исполнителя или причина переноса/отмены. Аргументы: status — open (по умолчанию) / overdue / today / postponed / done / cancelled; agent — исполнитель (имя или «me»); team — команда; days — окно для выполненных и отменённых (30); limit — строк (20). Агент видит свои задачи, супервайзер — своей команды, РМ — организации или своих филиалов.":
    "Savdo dala jamoasi (Sales Base) vazifalari — agentlar va supervayzerlar. Qaytaradi: xulosa — ochiq, muddati o'tgan, bugunga, kechiktirilgan, AI qo'ygan, days kunda bajarilgan; ijrochilar bo'yicha taqsimot (ochiq, muddati o'tgan, bajarilgan); vazifalar ro'yxati — nomi, ijrochi va jamoa, savdo nuqtasi, ustuvorlik, muddat (yyyy-MM-dd), holat, kunlarda kechikish, kim qo'ygan (AI-rejalashtirish, supervayzer, RM, tizim, ofis), qo'yish sababi (AI vazifalarida — tavsiya sababi), ijrochi natijasi yoki ko'chirish/bekor qilish sababi. Argumentlar: status — open (standart) / overdue / today / postponed / done / cancelled; agent — ijrochi (ism yoki «me»); team — jamoa; days — bajarilgan va bekor qilinganlar oynasi (30); limit — qatorlar (20). Agent o'z vazifalarini, supervayzer — jamoasinikini, RM — tashkilot yoki o'z filiallarinikini ko'radi.",
  "Sales Base: маршруты и визиты":
    "Sales Base: marshrutlar va tashriflar",
  "Маршруты и визиты полевой команды продаж (Sales Base) за день или период до 31 дня. С агентом (agent) — по дням: статус и источник маршрута, точки плана по порядку со статусом (посещена, пропущена с причиной, не посещена), время прибытия и ухода, результат визита (заказ, отказ, нет ЛПР…), сумма и комментарий агента, визиты вне маршрута, заказы агента в Linko за день; за период — пропущенные точки с причинами. Без агента — по команде или всей зоне: по каждому агенту точки плана, посещено, пропущено, не посещено, охват %, визиты и визиты с заказом, заказы Linko; агенты без маршрута; частые причины пропуска и пропущенные точки; отказы на визитах с комментариями. Аргументы: date (yyyy-MM-dd, по умолчанию сегодня) или from и to (yyyy-MM-dd); agent; team; limit — строк в списках (20). Агент видит только себя, супервайзер — команду, РМ — организацию или свои филиалы.":
    "Savdo dala jamoasi (Sales Base) marshrutlari va tashriflari kun yoki 31 kungacha davr uchun. Agent (agent) bilan — kunlar bo'yicha: marshrut holati va manbai, reja nuqtalari tartib bo'yicha holati bilan (borilgan, sababi bilan o'tkazib yuborilgan, borilmagan), kelish va ketish vaqti, tashrif natijasi (buyurtma, rad etish, QQS yo'q…), agent summasi va izohi, marshrutdan tashqari tashriflar, agentning kunlik Linko buyurtmalari; davr uchun — sabablari bilan o'tkazib yuborilgan nuqtalar. Agentsiz — jamoa yoki butun hudud bo'yicha: har bir agent uchun reja nuqtalari, borilgan, o'tkazib yuborilgan, borilmagan, qamrov %, tashriflar va buyurtmali tashriflar, Linko buyurtmalari; marshrutsiz agentlar; o'tkazib yuborishning tez-tez sabablari va o'tkazib yuborilgan nuqtalar; izohli tashriflardagi rad etishlar. Argumentlar: date (yyyy-MM-dd, standart bugun) yoki from va to (yyyy-MM-dd); agent; team; limit — ro'yxatlardagi qatorlar (20). Agent faqat o'zini, supervayzer — jamoani, RM — tashkilot yoki o'z filiallarini ko'radi.",
  "Sales Base: рекомендации AI-планирования":
    "Sales Base: AI-rejalashtirish tavsiyalari",
  "Рекомендации AI-планирования Sales Base: что предложено по торговым точкам и агентам и что решили супервайзер или РМ. Возвращает: сколько рекомендаций ждут решения, принято, отклонено, истекло; разбивку по видам и агентам; список — вид (падение продаж, давно не посещали, клиент перестал покупать, отставание от плана, пропущенная точка, высокий потенциал, точка без агента), заголовок, причина с цифрами, статус, агент, супервайзер, точка, приоритет, срок (yyyy-MM-dd), уверенность %, кто и когда решил, комментарий решения, создана ли задача. Аргументы: status — pending (по умолчанию) / approved / rejected / expired / decided / all; kind; agent; team; limit (20). Видят супервайзер (своя команда) и РМ; агенту рекомендации не показываются.":
    "Sales Base AI-rejalashtirish tavsiyalari: savdo nuqtalari va agentlar bo'yicha nima taklif qilingan va supervayzer yoki RM nima qaror qilgan. Qaytaradi: nechta tavsiya qaror kutmoqda, qabul qilingan, rad etilgan, muddati o'tgan; turlar va agentlar bo'yicha taqsimot; ro'yxat — turi (savdo tushishi, uzoq vaqt borilmagan, mijoz xarid qilishni to'xtatgan, rejadan orqada qolish, o'tkazib yuborilgan nuqta, yuqori salohiyat, agentsiz nuqta), sarlavha, raqamli sabab, holat, agent, supervayzer, nuqta, ustuvorlik, muddat (yyyy-MM-dd), ishonch %, kim va qachon qaror qilgan, qaror izohi, vazifa yaratilganmi. Argumentlar: status — pending (standart) / approved / rejected / expired / decided / all; kind; agent; team; limit (20). Supervayzer (o'z jamoasi) va RM ko'radi; agentga tavsiyalar ko'rsatilmaydi.",
  "Продажи товаров за любой период: кг и выручка, ABC, динамика, сколько регионов и точек берут товар.":
    "Istalgan davr uchun mahsulotlar savdosi: kg va tushum, ABC, dinamika, tovarni nechta mintaqa va nuqta oladi.",
  "Экспорт по странам (кг, сумма, точки, динамика, с начала года) и продажи филиала «Завод» (экспорт и опт) по покупателям, агентам и товарам. Плана экспорта в OneBase нет.":
    "Mamlakatlar bo'yicha eksport (kg, summa, nuqtalar, dinamika, yil boshidan) va «Zavod» filiali savdosi (eksport va ulgurji) xaridorlar, agentlar va mahsulotlar bo'yicha. OneBase'da eksport rejasi yo'q.",
  "Прибыль завода на дилерах по месяцам: продажи, себестоимость, валовая прибыль и маржа, расходы, EBIT, ROS, EAT; загружается через AI Data Manager.":
    "Zavodning dilerlardagi oylik foydasi: savdo, tannarx, yalpi foyda va marja, xarajatlar, EBIT, ROS, EAT; AI Data Manager orqali yuklanadi.",
  "Задачи отделов OneBase: открытые, просроченные и выполненные, исполнители, сроки — только отделы, доступные пользователю.":
    "OneBase bo'limlari vazifalari: ochiq, muddati o'tgan va bajarilgan, ijrochilar, muddatlar — faqat foydalanuvchiga ochiq bo'limlar.",
  "ТП (агент): имя или часть имени, кириллицей или латиницей, либо id Linko.":
    "TP (agent): ism yoki ismning bir qismi, kirill yoki lotin yozuvida, yoki Linko id.",
  "Один день ГГГГ-ММ-ДД. Вместо него можно from и to.":
    "Bitta kun YYYY-OO-KK. Uning o'rniga from va to bo'lishi mumkin.",
  "С какого дня, ГГГГ-ММ-ДД.":
    "Qaysi kundan, YYYY-OO-KK.",
  "По какой день, ГГГГ-ММ-ДД; без него — по сегодня.":
    "Qaysi kungacha, YYYY-OO-KK; usiz — bugungacha.",
  "Точка: название или часть названия, часть адреса или id Linko.":
    "Nuqta: nomi yoki nomining bir qismi, manzilning bir qismi yoki Linko id.",
  "Торговая точка: id Linko или название.":
    "Savdo nuqtasi: Linko id yoki nomi.",
  "Статус заказа Linko, например delivered, given, not_delivered.":
    "Linko buyurtmasi holati, masalan delivered, given, not_delivered.",
  "Номер (id) одного заказа — с позициями.":
    "Bitta buyurtma raqami (id) — pozitsiyalari bilan.",
  "true — с позициями заказов.":
    "true — buyurtmalar pozitsiyalari bilan.",
  "Человек: имя или часть имени.":
    "Inson: ism yoki ismning bir qismi.",
  "Направление (РМ) — как на странице «Акции»; без него — вся страна.":
    "Yo'nalish (RM) — «Aksiyalar» sahifasidagidek; usiz — butun mamlakat.",
  "Дилер или контрагент: название или часть названия.":
    "Diler yoki kontragent: nomi yoki nomining bir qismi.",
  "Дилер: название или часть названия.":
    "Diler: nomi yoki nomining bir qismi.",
  "Строки каждого ТП и у республики и направления (у региона и ТП — всегда).":
    "Respublika va yo'nalishda ham har bir TP qatorlari (mintaqa va TP da — har doim).",
  "Первый месяц периода, ГГГГ-ММ (например 2026-07). Без from — последние months месяцев по to.":
    "Davrning birinchi oyi, YYYY-OO (masalan 2026-07). from siz — to gacha oxirgi months oy.",
  "Последний месяц периода, ГГГГ-ММ. Без to — месяц последних данных (идущий, неполный).":
    "Davrning oxirgi oyi, YYYY-OO. to siz — oxirgi ma'lumotlar oyi (joriy, to'liq emas).",
  "Сколько последних месяцев взять, если from не задан: 1–24, по умолчанию 3.":
    "from berilmagan bo'lsa, nechta oxirgi oyni olish: 1–24, standart 3.",
  "Регион: id или название (например «Самарканд»). Без региона — вся страна.":
    "Mintaqa: id yoki nomi (masalan «Samarqand»). Mintaqasiz — butun mamlakat.",
  "Категории отчёта через запятую, например «Кекс,Помадка». Без категории — все.":
    "Vergul bilan hisobot toifalari, masalan «Кекс,Помадка». Toifasiz — hammasi.",
  "Поиск SKU по части названия или коду (кириллицей или латиницей): вернутся только подходящие SKU.":
    "SKU ni nomining bir qismi yoki kodi bo'yicha qidirish (kirill yoki lotin yozuvida): faqat mos SKU lar qaytadi.",
  "Список ТОП: all — все SKU (по умолчанию), only — только ТОП-товары, not — кроме ТОПа.":
    "TOP ro'yxati: all — barcha SKU (standart), only — faqat TOP-mahsulotlar, not — TOP dan tashqari.",
  "Только группа ABC: A, B или C. Без abc — все группы.":
    "Faqat ABC guruhi: A, B yoki C. abc siz — barcha guruhlar.",
  "Год, например 2026. С month — P&L месяца; без month — весь год (сумма загруженных месяцев).":
    "Yil, masalan 2026. month bilan — oy P&L i; month siz — butun yil (yuklangan oylar yig'indisi).",
  "Месяц 1–12. Без year и month — последний месяц с продажами и расходами.":
    "Oy 1–12. year va month siz — savdo va xarajatli oxirgi oy.",
  "Начало периода, ГГГГ-ММ: P&L за несколько месяцев (сумма и помесячно). Без to — по последний загруженный месяц.":
    "Davr boshi, YYYY-OO: bir necha oy uchun P&L (yig'indi va oylar bo'yicha). to siz — oxirgi yuklangan oygacha.",
  "Конец периода, ГГГГ-ММ.":
    "Davr oxiri, YYYY-OO.",
  "Дилер: имя или часть имени, или регион (например «Самарканд») — только он, со всеми строками P&L.":
    "Diler: ism yoki ismning bir qismi, yoki mintaqa (masalan «Samarqand») — faqat u, P&L ning barcha qatorlari bilan.",
  "Направление (РМ): название, имя РМ или id. Без него и без региона — республика.":
    "Yo'nalish (RM): nomi, RM ismi yoki id. Usiz va mintaqasiz — respublika.",
  "За сколько последних дней отправки (1–30, по умолчанию 7).":
    "Oxirgi necha kundagi yuborishlar (1–30, standart 7).",
  "Отдел: код (sales, production, supply, finance, marketing, hr) или название («Финансы»). Без отдела — все отделы, доступные пользователю.":
    "Bo'lim: kod (sales, production, supply, finance, marketing, hr) yoki nomi («Moliya»). Bo'limsiz — foydalanuvchiga ochiq barcha bo'limlar.",
  "open — открытые (по умолчанию), overdue — просроченные, done — выполненные за days дней, cancelled — отменённые за days дней, all — открытые и закрытые за days дней.":
    "open — ochiq (standart), overdue — muddati o'tgan, done — days kunda bajarilgan, cancelled — days kunda bekor qilingan, all — days kundagi ochiq va yopilgan.",
  "Исполнитель: имя или часть имени; «me» — сам пользователь. Без него — все исполнители.":
    "Ijrochi: ism yoki ismning bir qismi; «me» — foydalanuvchining o'zi. Usiz — barcha ijrochilar.",
  "Окно для выполненных и отменённых задач, дней: по умолчанию 30, до 365.":
    "Bajarilgan va bekor qilingan vazifalar oynasi, kun: standart 30, 365 gacha.",
  "Сколько задач вернуть в списке: по умолчанию 20, до 100.":
    "Ro'yxatda nechta vazifa qaytarish: standart 20, 100 gacha.",
  "open — открытые (по умолчанию), overdue — просроченные, today — срок сегодня, postponed — отложенные, done — выполненные за days дней, cancelled — отменённые за days дней.":
    "open — ochiq (standart), overdue — muddati o'tgan, today — muddati bugun, postponed — kechiktirilgan, done — days kunda bajarilgan, cancelled — days kunda bekor qilingan.",
  "Исполнитель: агент или супервайзер, имя или часть имени; «me» — сам пользователь. Без него — вся зона пользователя.":
    "Ijrochi: agent yoki supervayzer, ism yoki ismning bir qismi; «me» — foydalanuvchining o'zi. Usiz — foydalanuvchining butun hududi.",
  "Окно для выполненных и отменённых задач, дней: по умолчанию 30, до 180.":
    "Bajarilgan va bekor qilingan vazifalar oynasi, kun: standart 30, 180 gacha.",
  "День yyyy-MM-dd. Без даты (и без from/to) — сегодня.":
    "Kun yyyy-MM-dd. Sanasiz (va from/to siz) — bugun.",
  "Начало периода yyyy-MM-dd (вместо date). Период — до 31 дня; без to — по сегодня.":
    "Davr boshi yyyy-MM-dd (date o'rniga). Davr — 31 kungacha; to siz — bugungacha.",
  "Конец периода yyyy-MM-dd (вместо date).":
    "Davr oxiri yyyy-MM-dd (date o'rniga).",
  "Агент: имя или часть имени. С агентом — его маршрут по точкам и визиты. Агент всегда видит только себя.":
    "Agent: ism yoki ismning bir qismi. Agent bilan — uning nuqtalar bo'yicha marshruti va tashriflari. Agent har doim faqat o'zini ko'radi.",
  "Команда: название. Без агента и команды — вся зона пользователя.":
    "Jamoa: nomi. Agent va jamoasiz — foydalanuvchining butun hududi.",
  "Сколько строк в списках (пропущенные точки, визиты, отказы): по умолчанию 20, до 100.":
    "Ro'yxatlarda nechta qator (o'tkazib yuborilgan nuqtalar, tashriflar, rad etishlar): standart 20, 100 gacha.",
  "pending — ждут решения (по умолчанию), approved — принятые, rejected — отклонённые, expired — истёкшие, decided — все с решением, all — все.":
    "pending — qaror kutayotganlar (standart), approved — qabul qilinganlar, rejected — rad etilganlar, expired — muddati o'tganlar, decided — qarorli hammasi, all — hammasi.",
  "Вид: sales_decline — падение продаж, not_visited — давно не посещали, lost_customer — клиент перестал покупать, agent_behind_plan — отставание от плана, skipped_point — пропущенная точка, high_potential — высокий потенциал, reassign — точка без агента. Без вида — все.":
    "Turi: sales_decline — savdo tushishi, not_visited — uzoq vaqt borilmagan, lost_customer — mijoz xarid qilishni to'xtatgan, agent_behind_plan — rejadan orqada qolish, skipped_point — o'tkazib yuborilgan nuqta, high_potential — yuqori salohiyat, reassign — agentsiz nuqta. Turisiz — hammasi.",
  "Агент: имя или часть имени. Без агента — вся зона пользователя.":
    "Agent: ism yoki ismning bir qismi. Agentsiz — foydalanuvchining butun hududi.",
  "Команда: название. Без команды — вся зона пользователя.":
    "Jamoa: nomi. Jamoasiz — foydalanuvchining butun hududi.",
  "Сколько рекомендаций вернуть в списке: по умолчанию 20, до 100.":
    "Ro'yxatda nechta tavsiya qaytarish: standart 20, 100 gacha.",
  // HR (Verifix): источник знаний, описание HR AI и инструменты get_hr_* (Tools/Data/HrTools.cs).
  "HR: посещаемость за день":
    "HR: kunlik davomat",
  "Посещаемость сотрудников за день по данным Verifix (раздел HR): сколько активных, пришли, опоздали, отсутствуют, по документу (отпуск, больничный, командировка), ожидаются, выходной, нет графика, не ясно, отметок за день; то же по подразделениям; списки — кто отсутствует, кто опоздал (на сколько минут, приход и начало смены), кто по документу и у кого «не ясно»; когда данные Verifix синхронизированы. Аргументы: date — yyyy-MM-dd, «сегодня» (по умолчанию) или «вчера»; division — подразделение Verifix (название или ID). «Отсутствует» — рабочий день по графику Verifix без прихода и без документа при полностью загруженных отметках; «не ясно» — данных недостаточно (не прогул); «по документу» — отпуск, больничный, командировка, отгул; одна отметка без прихода — не рабочий день. Опоздание — приход позже начала смены больше допуска; ранний уход — уход раньше конца смены.":
    "Verifix ma'lumotlari bo'yicha xodimlarning kunlik davomati (HR bo'limi): nechta faol, keldi, kechikdi, kelmagan, hujjat bo'yicha (ta'til, kasallik varaqasi, xizmat safari), kutilmoqda, dam olish kuni, jadval yo'q, aniq emas, kun davomidagi belgilar; xuddi shu bo'linmalar bo'yicha; ro'yxatlar — kim kelmagan, kim kechikkan (necha daqiqaga, kelish va smena boshlanishi), kim hujjat bo'yicha va kimda «aniq emas»; Verifix ma'lumotlari qachon sinxronlangan. Argumentlar: date — yyyy-MM-dd, «bugun» (standart) yoki «kecha»; division — Verifix bo'linmasi (nomi yoki ID). «Kelmagan» — Verifix jadvali bo'yicha ish kuni, kelish va hujjat yo'q, belgilar to'liq yuklangan; «aniq emas» — ma'lumot yetarli emas (progul emas); «hujjat bo'yicha» — ta'til, kasallik varaqasi, xizmat safari, javob; kelishsiz bitta belgi — ish kuni emas. Kechikish — smena boshlanishidan ruxsat etilgan vaqtdan ko'proq kech kelish; erta ketish — smena tugashidan oldin ketish.",
  "День: yyyy-MM-dd, «сегодня» (по умолчанию) или «вчера».":
    "Kun: yyyy-MM-dd, «bugun» (standart) yoki «kecha».",
  "Подразделение Verifix: название или ID. Без него — все.":
    "Verifix bo'linmasi: nomi yoki ID. Usiz — hammasi.",
  "HR: сотрудники":
    "HR: xodimlar",
  "Сотрудники из Verifix: ФИО, ID Verifix, подразделение, должность, график, статус (работает, уволен, не принят), дата приёма и увольнения, последняя отметка, итог сегодняшнего дня; сколько человек по подразделениям. Аргументы: search — часть ФИО или ID Verifix; division — подразделение (название или ID); status — working (по умолчанию) / dismissed / not_hired / missing (пропали из Verifix) / all; sort — name (по умолчанию) / hired (сначала новые) / division; limit — сколько строк (50, до 200). Контакты (телефон, почта) не отдаются.":
    "Verifix xodimlari: F.I.Sh., Verifix ID, bo'linma, lavozim, jadval, holat (ishlaydi, ishdan bo'shagan, qabul qilinmagan), ishga qabul va bo'shash sanasi, oxirgi belgi, bugungi kun natijasi; bo'linmalar bo'yicha necha kishi. Argumentlar: search — F.I.Sh. qismi yoki Verifix ID; division — bo'linma (nomi yoki ID); status — working (standart) / dismissed / not_hired / missing (Verifix'dan yo'qolgan) / all; sort — name (standart) / hired (avval yangilar) / division; limit — nechta qator (50, 200 gacha). Kontaktlar (telefon, pochta) berilmaydi.",
  "Часть ФИО или ID Verifix.":
    "F.I.Sh. qismi yoki Verifix ID.",
  "Подразделение Verifix: название или ID.":
    "Verifix bo'linmasi: nomi yoki ID.",
  "working — работают (по умолчанию), dismissed — уволены, not_hired — не приняты, missing — пропали из Verifix, all — все.":
    "working — ishlaydi (standart), dismissed — ishdan bo'shagan, not_hired — qabul qilinmagan, missing — Verifix'dan yo'qolgan, all — hammasi.",
  "name — по ФИО (по умолчанию), hired — сначала недавно принятые, division — по подразделению.":
    "name — F.I.Sh. bo'yicha (standart), hired — avval yaqinda qabul qilinganlar, division — bo'linma bo'yicha.",
  "Сколько сотрудников вернуть: 50 по умолчанию, до 200.":
    "Nechta xodim qaytarish: standart 50, 200 gacha.",
  "HR: карточка сотрудника":
    "HR: xodim kartochkasi",
  "Карточка сотрудника по данным Verifix: подразделение, должность, график, статус, приём и увольнение, заметка HR; итоги за период (рабочих дней, с приходом, опозданий и минут, ранних уходов, отсутствий, дней по документам, отработано и план в часах) и каждый день — итог, начало и конец смены, приход, уход, опоздание, ранний уход, отработано, документ, пояснение. Аргументы: employee — ФИО (часть) или ID Verifix (обязательно); from, to — yyyy-MM-dd, по умолчанию последние 14 дней, не больше 62. «Отсутствует» — рабочий день по графику Verifix без прихода и без документа при полностью загруженных отметках; «не ясно» — данных недостаточно (не прогул); «по документу» — отпуск, больничный, командировка, отгул; одна отметка без прихода — не рабочий день. Опоздание — приход позже начала смены больше допуска; ранний уход — уход раньше конца смены.":
    "Verifix ma'lumotlari bo'yicha xodim kartochkasi: bo'linma, lavozim, jadval, holat, ishga qabul va bo'shash, HR qaydi; davr yakunlari (ish kunlari, kelgan kunlar, kechikishlar va daqiqalar, erta ketishlar, kelmasliklar, hujjat bo'yicha kunlar, soatlarda ishlangan va reja) va har bir kun — natija, smena boshlanishi va tugashi, kelish, ketish, kechikish, erta ketish, ishlangan, hujjat, izoh. Argumentlar: employee — F.I.Sh. (qismi) yoki Verifix ID (majburiy); from, to — yyyy-MM-dd, standart oxirgi 14 kun, 62 kundan ko'p emas. «Kelmagan» — Verifix jadvali bo'yicha ish kuni, kelish va hujjat yo'q, belgilar to'liq yuklangan; «aniq emas» — ma'lumot yetarli emas (progul emas); «hujjat bo'yicha» — ta'til, kasallik varaqasi, xizmat safari, javob; kelishsiz bitta belgi — ish kuni emas. Kechikish — smena boshlanishidan ruxsat etilgan vaqtdan ko'proq kech kelish; erta ketish — smena tugashidan oldin ketish.",
  "ФИО (можно часть) или ID Verifix. Обязательно.":
    "F.I.Sh. (qismi ham bo'ladi) yoki Verifix ID. Majburiy.",
  "Начало периода: yyyy-MM-dd.":
    "Davr boshi: yyyy-MM-dd.",
  "Конец периода: yyyy-MM-dd (по умолчанию сегодня).":
    "Davr oxiri: yyyy-MM-dd (standart bugun).",
  "HR: опоздания и отсутствия":
    "HR: kechikishlar va kelmasliklar",
  "Нарушения дисциплины за период по графикам Verifix: опоздания (минуты), ранние уходы, отсутствия, дни без отметки ухода. Возвращает сколько каждого вида, по сотрудникам (сколько раз и минут — от самых частых) и список случаев (дата, сотрудник, подразделение, вид, минуты, смена, приход, уход, пояснение). Аргументы: from, to — yyyy-MM-dd (по умолчанию последние 7 дней, не больше 62); kind — late / early / absent / noexit / all (по умолчанию); division — подразделение; limit — сколько случаев в списке (50). «Отсутствует» — рабочий день по графику Verifix без прихода и без документа при полностью загруженных отметках; «не ясно» — данных недостаточно (не прогул); «по документу» — отпуск, больничный, командировка, отгул; одна отметка без прихода — не рабочий день. Опоздание — приход позже начала смены больше допуска; ранний уход — уход раньше конца смены.":
    "Verifix jadvallari bo'yicha davr uchun intizom buzilishlari: kechikishlar (daqiqalar), erta ketishlar, kelmasliklar, ketish qayd etilmagan kunlar. Qaytaradi: har bir turdan nechta, xodimlar bo'yicha (necha marta va daqiqa — eng ko'pidan) va holatlar ro'yxati (sana, xodim, bo'linma, tur, daqiqalar, smena, kelish, ketish, izoh). Argumentlar: from, to — yyyy-MM-dd (standart oxirgi 7 kun, 62 kundan ko'p emas); kind — late / early / absent / noexit / all (standart); division — bo'linma; limit — ro'yxatda nechta holat (50). «Kelmagan» — Verifix jadvali bo'yicha ish kuni, kelish va hujjat yo'q, belgilar to'liq yuklangan; «aniq emas» — ma'lumot yetarli emas (progul emas); «hujjat bo'yicha» — ta'til, kasallik varaqasi, xizmat safari, javob; kelishsiz bitta belgi — ish kuni emas. Kechikish — smena boshlanishidan ruxsat etilgan vaqtdan ko'proq kech kelish; erta ketish — smena tugashidan oldin ketish.",
  "late — опоздания, early — ранние уходы, absent — отсутствия, noexit — нет ухода, all — все (по умолчанию).":
    "late — kechikishlar, early — erta ketishlar, absent — kelmasliklar, noexit — ketish qayd etilmagan, all — hammasi (standart).",
  "Сколько случаев в списке: 50 по умолчанию, до 300.":
    "Ro'yxatda nechta holat: standart 50, 300 gacha.",
  "HR: табель и отчёт":
    "HR: tabel va hisobot",
  "Табель и отчёт по рабочему времени за период по данным Verifix: итоги (сотрудников, рабочих дней по графику, с приходом, опозданий и минут, ранних уходов, отсутствий, дней по документам, «не ясно», отработано и план в часах, явка % — доля дней с приходом среди решённых рабочих, без опозданий % — доля приходов без опоздания); по подразделениям — то же; по сотрудникам — итоги периода. Аргументы: from, to — yyyy-MM-dd (по умолчанию текущий месяц, не больше 62 дней); division — подразделение; search — часть ФИО; sort — late / absent / worked / attendance / name (по умолчанию late — больше опозданий сверху); limit — сколько сотрудников (30). Для 1–5 сотрудников добавляются буквы дней табеля (Я — явка, ОП — опоздание, РУ — ранний уход, РВ — работа в выходной, ОТС — отсутствие, ОТ/Б/К/О/НО — документ, В — выходной, П — праздник, ? — не ясно).":
    "Verifix ma'lumotlari bo'yicha davr uchun ish vaqti tabeli va hisoboti: yakunlar (xodimlar, jadval bo'yicha ish kunlari, kelgan kunlar, kechikishlar va daqiqalar, erta ketishlar, kelmasliklar, hujjat bo'yicha kunlar, «aniq emas», soatlarda ishlangan va reja, kelish % — hal bo'lgan ish kunlari orasida kelgan kunlar ulushi, kechikishsiz % — kechikishsiz kelishlar ulushi); bo'linmalar bo'yicha — xuddi shunday; xodimlar bo'yicha — davr yakunlari. Argumentlar: from, to — yyyy-MM-dd (standart joriy oy, 62 kundan ko'p emas); division — bo'linma; search — F.I.Sh. qismi; sort — late / absent / worked / attendance / name (standart late — kechikishlar ko'pi yuqorida); limit — nechta xodim (30). 1–5 xodim uchun tabel kunlari harflari qo'shiladi (Я — kelish, ОП — kechikish, РУ — erta ketish, РВ — dam olish kunida ish, ОТС — kelmaslik, ОТ/Б/К/О/НО — hujjat, В — dam olish kuni, П — bayram, ? — aniq emas).",
  "Начало периода: yyyy-MM-dd (по умолчанию 1-е число текущего месяца).":
    "Davr boshi: yyyy-MM-dd (standart joriy oyning 1-sanasi).",
  "Часть ФИО сотрудника.":
    "Xodim F.I.Sh. qismi.",
  "Порядок сотрудников.":
    "Xodimlar tartibi.",
  "Сколько сотрудников вернуть: 30 по умолчанию, до 300.":
    "Nechta xodim qaytarish: standart 30, 300 gacha.",
  "HR: сотрудники и посещаемость":
    "HR: xodimlar va davomat",
  "Сотрудники из Verifix (подразделение, должность, график, статус, приём и увольнение), приходы и уходы, опоздания, ранние уходы, отсутствия, отпуска и больничные по документам Verifix, отработанные часы, табели и отчёты по подразделениям. ФОТ, кандидатов и оценок нет.":
    "Verifix xodimlari (bo'linma, lavozim, jadval, holat, ishga qabul va bo'shash), kelish va ketishlar, kechikishlar, erta ketishlar, kelmasliklar, Verifix hujjatlari bo'yicha ta'til va kasallik varaqalari, ishlangan soatlar, bo'linmalar bo'yicha tabellar va hisobotlar. Ish haqi fondi, nomzodlar va baholashlar yo'q.",
  "HR: сотрудники и подразделения (Verifix), посещаемость, опоздания, ранние уходы, отсутствия, отпуска и больничные, рабочее время и табели, приём и увольнения; команда продаж, вакансии, нагрузка и эффективность.":
    "HR: xodimlar va bo'linmalar (Verifix), davomat, kechikishlar, erta ketishlar, kelmasliklar, ta'til va kasallik varaqalari, ish vaqti va tabellar, ishga qabul va bo'shashlar; savdo jamoasi, bo'sh ish o'rinlari, ish yuki va samaradorlik.",
};

export default dict;
