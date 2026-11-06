import type { Dict } from "../core";

/** Главная (Обзор), «Отделы», «Отчёты» и демо-показатели (lib/demo-data). Названия отделов — данные, не переводятся. */
const dict: Dict = {
  // Главная
  "Обзор · OneBase": "Umumiy ko'rinish · OneBase",
  "Доброе утро": "Xayrli tong",
  "Добрый день": "Xayrli kun",
  "Добрый вечер": "Xayrli kech",
  "Доброй ночи": "Xayrli tun",
  "Вот что происходит в компании сегодня.": "Bugun kompaniyada nimalar bo'layotgani — shu yerda.",
  "Последние 30 дней": "So'nggi 30 kun",
  "Последние 7 дней": "So'nggi 7 kun",
  "Квартал": "Chorak",
  "Год": "Yil",
  "Все отделы": "Barcha bo'limlar",
  "Все сотрудники": "Barcha xodimlar",
  "Обновлено 5 минут назад": "5 daqiqa oldin yangilandi",
  "Состояние отделов": "Bo'limlar holati",
  "Показатели и динамика команд": "Jamoalar ko'rsatkichlari va dinamikasi",
  "Фокус дня": "Kun fokusi",
  "Проактивный анализ не нашёл проблем в данных OneBase.": "Proaktiv tahlil OneBase ma'lumotlarida muammo topmadi.",
  "Открыть обзор": "Umumiy ko'rinishni ochish",
  "Открыть консультанта": "Maslahatchini ochish",

  // Категории находок AI (lib/ai — categoryNames) в «Фокусе дня»
  "Финансы": "Moliya",
  "Продажи": "Savdo",
  "Маркетинг": "Marketing",
  "Производство": "Ishlab chiqarish",
  "Снабжение": "Ta'minot",

  // Демо-показатели (lib/demo-data)
  "Общий KPI": "Umumiy KPI",
  "Выполнение планов": "Rejalar bajarilishi",
  "Активные задачи": "Faol vazifalar",
  "Просрочено": "Muddati o'tgan",
  "+2,6% к прошлому периоду": "o'tgan davrga nisbatan +2,6%",
  "+4,2% к прошлому периоду": "o'tgan davrga nisbatan +4,2%",
  "24 на сегодня": "bugunga 24 ta",
  "−3 за неделю": "hafta davomida −3",
  "Выпуск продукции": "Mahsulot ishlab chiqarish",
  "Платёжная дисциплина": "To'lov intizomi",
  "Выручка за период": "Davr tushumi",
  "Квалифицированные лиды": "Malakali lidlar",
  "Поставки в срок": "O'z vaqtidagi yetkazib berishlar",
  "Удержание команды": "Jamoani saqlab qolish",
  "Финансы и маркетинг требуют внимания. Проверьте расходы и темп поступления лидов.":
    "Moliya va marketing e'tibor talab qiladi. Xarajatlar va lidlar kelish sur'atini tekshiring.",
  "Сегодня": "Bugun",
  "Вчера": "Kecha",
  "10 мин назад": "10 daqiqa oldin",

  // Карточки отделов (components/departments)
  "Последнее обновление: {date}": "Oxirgi yangilanish: {date}",
  "файл": "fayl",
  "файла": "fayl",
  "файлов": "fayl",
  "папка": "papka",
  "папки": "papka",
  "папок": "papka",

  // «Отделы»
  "Отделы": "Bo'limlar",
  "Показатели, планы и состояние подразделений": "Bo'linmalar ko'rsatkichlari, rejalari va holati",

  // «Отчёты»
  "Отчёты": "Hisobotlar",
  "Отчёты по отделам, финансам и сотрудникам": "Bo'limlar, moliya va xodimlar bo'yicha hisobotlar",
  "Каталог отчётов по продажам, финансам, отделам и сотрудникам": "Savdo, moliya, bo'limlar va xodimlar bo'yicha hisobotlar katalogi",
  "Фильтры по периоду и подразделению": "Davr va bo'linma bo'yicha filtrlar",
  "Предпросмотр и экспорт": "Oldindan ko'rish va eksport",

  // «Отделы»: карточки, статусы и пороги, выбор месяца и вида.
  "Показатели, планы и состояние": "Ko'rsatkichlar, rejalar va holat",
  "Факт {fact} из {plan} т ({pct})": "Fakt {plan} t dan {fact} t ({pct})",
  "Факт {fact} из {plan} т ({pct}) · прогноз {forecast}": "Fakt {plan} t dan {fact} t ({pct}) · prognoz {forecast}",
  "{name}: нет данных": "{name}: ma'lumot yo'q",
  "{name}: факт {fact}, прогноз {forecast}": "{name}: fakt {fact}, prognoz {forecast}",
  "Заявки к закупке без просрочки": "Xarid uchun arizalar muddati o'tmagan",
  "К закупке {a} · просрочено {b}": "Xaridga {a} · muddati o'tgan {b}",
  "К закупке {a} · просрочено {b} · на согласовании {c}": "Xaridga {a} · muddati o'tgan {b} · kelishuvda {c}",
  "Не удалось загрузить": "Yuklab bo'lmadi",
  "Не удалось загрузить показатели отдела.": "Bo'lim ko'rsatkichlarini yuklab bo'lmadi.",
  "Нет данных за {month}": "{month} uchun ma'lumot yo'q",
  "Норма ≥ 80%, сейчас {pct}": "Me'yor ≥ 80%, hozir {pct}",
  "Открыть отдел «{name}»": "«{name}» bo'limini ochish",
  "План выполнен: {pct}": "Reja bajarildi: {pct}",
  "Плана на {month} нет": "{month} uchun reja yo'q",
  "Плана на {month} нет · факт {fact} т": "{month} uchun reja yo'q · fakt {fact} t",
  "Показатель только на сегодня — за прошлые месяцы его нет": "Ko'rsatkich faqat bugun uchun — o'tgan oylar uchun yo'q",
  "Прогноз выполнения плана": "Reja bajarilishi prognozi",
  "Сегодня отметок пока нет": "Bugun hali belgilar yo'q",
  "Сеткой": "To'r ko'rinishida",
  "Согласованное закуплено": "Kelishilgani xarid qilingan",
  "Списком": "Ro'yxat ko'rinishida",
  "Статус считается по порогам отдела: ниже цели — «Требует внимания», цель достигнута — «В норме», план уже выполнен — «План выполнен». Наведите на метку, чтобы увидеть порог; нажмите на карточку, чтобы открыть отдел.": "Holat bo'lim chegaralari bo'yicha hisoblanadi: maqsaddan past — «E'tibor talab qiladi», maqsadga erishildi — «Me'yorda», reja bajarilgan — «Reja bajarildi». Chegarani ko'rish uchun belgining ustiga olib boring; bo'limni ochish uchun kartochkani bosing.",
  "Факт {fact} из {plan} т · {pct}": "Fakt {plan} t dan {fact} t · {pct}",
  "Цель — 100% плана, выполнено {pct}": "Maqsad — rejaning 100%, bajarildi {pct}",
  "Цель — без просроченных заявок, просрочено {n}": "Maqsad — muddati o'tgan arizalarsiz, muddati o'tgan {n}",
  "Цель — прогноз от 100% плана, сейчас {pct}": "Maqsad — rejaning 100% dan prognoz, hozir {pct}",
};

export default dict;
