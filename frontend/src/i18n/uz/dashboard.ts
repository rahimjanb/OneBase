import type { Dict } from "../core";

/** Главная «Обзор» (ТЗ dashboard), сайдбар и шапка: интерфейс и подписи показателей из lib/dashboard.mock. Имена людей — данные. */
const dashboard: Dict = {
  // Сайдбар и шапка
  "Интеллект": "Intellekt",
  "Коммуникации": "Kommunikatsiyalar",
  "Календарь": "Kalendar",
  "Еженедельный AI-отчёт готов": "Haftalik AI hisoboti tayyor",
  "3 риска и 5 рекомендаций по отделам за неделю.": "Haftada bo'limlar bo'yicha 3 ta xavf va 5 ta tavsiya.",
  "Открыть отчёт": "Hisobotni ochish",
  "Поиск по задачам, отделам, сотрудникам…": "Vazifalar, bo'limlar, xodimlar bo'yicha qidirish…",
  "Поиск": "Qidirish",
  "Навигация": "Navigatsiya",

  // Шапка страницы
  "Компания выполняет план на": "Kompaniya rejani bajarmoqda:",
  "отдел требует внимания": "bo'lim e'tibor talab qiladi",
  "отдела требуют внимания": "bo'lim e'tibor talab qiladi",
  "отделов требуют внимания": "bo'lim e'tibor talab qiladi",
  "Экспорт": "Eksport",
  "Новая задача": "Yangi vazifa",

  // Фильтры
  "Период": "Davr",
  "7 дн": "7 kun",
  "30 дн": "30 kun",
  "Отдел": "Bo'lim",
  "Сотрудник": "Xodim",
  "Данные обновлены {n} мин назад": "Ma'lumotlar {n} daqiqa oldin yangilandi",
  "Обновить данные": "Ma'lumotlarni yangilash",

  // Фокус дня
  "AI · {n} инсайта": "AI · {n} ta xulosa",
  "Сформировано на основе данных {d} отделов и {n} активных задач": "{d} ta bo'lim va {n} ta faol vazifa ma'lumotlari asosida tuzilgan",
  "Спросить AI": "AI dan so'rash",
  "Критично": "Jiddiy",
  "Внимание": "E'tibor",
  "Возможность": "Imkoniyat",
  "Открыть": "Ochish",
  "Подробнее": "Batafsil",
  "Находки консультанта недоступны: нет права на консультанта или сервис не ответил.": "Maslahatchi topilmalari mavjud emas: maslahatchiga huquq yo'q yoki xizmat javob bermadi.",

  // KPI
  "к прошлому периоду": "o'tgan davrga nisbatan",
  "поставлено сегодня": "bugun qo'yilgan",
  "за неделю": "bir haftada",

  // Динамика
  "Динамика общего KPI": "Umumiy KPI dinamikasi",
  "По неделям · цель {n}%": "Haftalar bo'yicha · maqsad {n}%",
  "KPI": "KPI",
  "Выручка": "Tushum",
  "Среднее за период": "Davr o'rtachasi",
  "Лучшая неделя": "Eng yaxshi hafta",
  "Недель выше цели": "Maqsaddan yuqori haftalar",
  "{n} из {total}": "{total} dan {n}",
  "Цель {n}%": "Maqsad {n}%",
  "Динамика по неделям": "Haftalar bo'yicha dinamika",

  // Задачи по статусу
  "Задачи по статусу": "Vazifalar holati bo'yicha",
  "Всего {n} за 30 дней": "30 kunda jami {n}",
  "Действия": "Amallar",
  "Доля выполненных задач: {n}%": "Bajarilgan vazifalar ulushi: {n}%",
  "выполнено": "bajarilgan",
  "Выполнено": "Bajarilgan",
  "В работе": "Jarayonda",
  "На проверке": "Tekshiruvda",

  // Отделы
  "Ключевые показатели и динамика команд за 30 дней": "30 kunlik asosiy ko'rsatkichlar va jamoalar dinamikasi",
  "{n} в норме": "{n} me'yorda",
  "{n} требуют внимания": "{n} e'tibor talab qiladi",
  "Ключевая метрика · выполнение": "Asosiy ko'rsatkich · bajarilish",
  "Тренд 8 нед.": "8 haftalik trend",
  "Статус": "Holat",
  "Рук. {name}": "Rahb. {name}",
  "Выпуск продукции": "Mahsulot ishlab chiqarish",
  "Платёжная дисциплина": "To'lov intizomi",
  "Выручка за период": "Davr tushumi",
  "Квалифицированные лиды": "Malakali lidlar",
  "Поставки в срок": "Muddatida yetkazib berish",
  "Удержание команды": "Jamoani saqlab qolish",
  "План выполнен": "Reja bajarildi",
  "В норме": "Me'yorda",
  "Требует внимания": "E'tibor talab qiladi",

  // Дедлайны
  "Ближайшие дедлайны": "Yaqin muddatlar",
  "{n} открытых задач со сроком": "Muddatli {n} ta ochiq vazifa",
  "Раздел «Задачи» недоступен": "«Vazifalar» bo'limi mavjud emas",
  "Открытых задач со сроком пока нет": "Muddatli ochiq vazifalar hozircha yo'q",
  "Задачи появятся, когда будет доступ к разделу «Задачи»": "«Vazifalar» bo'limiga kirish ochilganda vazifalar paydo bo'ladi",
  "Все задачи": "Barcha vazifalar",
  "Выполнено: {title}": "Bajarildi: {title}",
  "Высокий": "Yuqori",
  "Средний": "O'rta",
  "Низкий": "Past",
  "Завтра": "Ertaga",
  "просрочено": "muddati o'tgan",

  // Лидеры и лента
  "Лидеры месяца": "Oy yetakchilari",
  "Лидер месяца": "Oy yetakchisi",
  "Без опозданий · часы за {month}": "Kechikishsiz · {month} oyidagi soatlar",
  "Без опозданий: {count} из {total}": "Kechikishsiz: {total} tadan {count} ta",
  "Дней с приходом вовремя": "O'z vaqtida kelgan kunlar",
  "Рейтинг виден HR и руководителям отделов": "Reyting HR va bo'lim rahbarlariga ko'rinadi",
  "Не удалось загрузить данные Verifix": "Verifix ma'lumotlarini yuklab bo'lmadi",
  "Подключите Verifix, чтобы увидеть лидеров": "Yetakchilarni ko'rish uchun Verifix'ni ulang",
  "Пока никто не отработал месяц без опозданий": "Hozircha hech kim oyni kechikishsiz ishlamadi",
  "Полный рейтинг": "To'liq reyting",
  "Лента активности": "Faollik lentasi",
  "Последние события": "So'nggi voqealar",
  "Событий пока нет": "Hozircha voqealar yo'q",
  "Уведомления недоступны": "Bildirishnomalar mavjud emas",
  "{n} мин": "{n} daqiqa",
  "{n} ч": "{n} soat",
  "{n} дн": "{n} kun",
};

export default dashboard;
