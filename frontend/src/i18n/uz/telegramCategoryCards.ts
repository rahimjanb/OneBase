import type { Dict } from "../core";

/**
 * «Настройки → Telegram → Автоматические отчёты → Карточки категорий» (отчёт 10): выбор категорий, расписание, получатели, предпросмотр и тест —
 * components/telegram/TelegramCategoryCards.tsx, карточка-ссылка на странице Telegram, lib/telegram-category-cards.ts.
 */
const dict: Dict = {
  "Карточки категорий": "Toifa kartochkalari",
  "10. Карточки категорий": "10. Toifa kartochkalari",
  "карточки": "kartochkalar",
  "итог прошлого рабочего дня": "oldingi ish kuni yakuni",
  "каждый день": "har kuni",
  "9:00 — итог прошлого рабочего дня, 18:30 — итог дня: по картинке на каждую выбранную категорию («Придзел», «Помадка» — в планах «Могуль») — факт, план, прогноз, АКБ, SKU, по дням, регионы или ТП, ТОП артикулов с фото. Категории — во вкладке «Карточки категорий»":
    "9:00 — oldingi ish kuni yakuni, 18:30 — kun yakuni: har bir tanlangan toifaga bittadan rasm («Придзел», «Помадка» — rejalarda «Могуль») — fakt, reja, prognoz, AKB, SKU, kunlar bo'yicha, hududlar yoki TP, rasmli TOP artikullar. Toifalar — «Toifa kartochkalari» bo'limida",
  "Выберите категории — «Придзел», «Помадка» (Могуль) и другие — и по расписанию в Telegram уйдёт картинка с полными данными по каждой: факт и план, прогноз, АКБ, SKU, по дням, регионы или ТП, ТОП артикулов с фото.":
    "Toifalarni tanlang — «Придзел», «Помадка» (Могуль) va boshqalar — jadval bo'yicha Telegramga har biri bo'yicha to'liq ma'lumotli rasm yuboriladi: fakt va reja, prognoz, AKB, SKU, kunlar bo'yicha, hududlar yoki TP, rasmli TOP artikullar.",
  "Отдельная картинка на каждую выбранную категорию с полными данными страницы категории «Продаж» в области получателя: факт и план месяца, прогноз, АКБ и дистрибуция, SKU, продажи по дням, регионы (РМ) или ТП (СВ), ТОП артикулов с фото и пропавшие артикулы. Все картинки — одним альбомом.":
    "Har bir tanlangan toifaga qabul qiluvchi doirasidagi «Savdo» toifa sahifasining to'liq ma'lumotlari bilan alohida rasm: oylik fakt va reja, prognoz, AKB va distributsiya, SKU, kunlar bo'yicha savdo, hududlar (RM) yoki TP (SV), rasmli TOP artikullar va tushib qolgan artikullar. Barcha rasmlar — bitta albomda.",
  "Категории по умолчанию": "Standart toifalar",
  "Ничего не выбрано — уйдут все {n} категорий.": "Hech narsa tanlanmagan — barcha {n} ta toifa yuboriladi.",
  "Уйдут: {list}. Получателям без своего выбора.": "Yuboriladi: {list}. O'z tanlovi bo'lmagan qabul qiluvchilarga.",
  "Когда уходит": "Qachon yuboriladi",
  "Изменить время": "Vaqtni o'zgartirish",
  "Выключить «{name}»": "«{name}»ni o'chirish",
  "Аккаунт Telegram для автоотчётов не выбран — отправки не уйдут.": "Avtohisobotlar uchun Telegram akkaunti tanlanmagan — yuborishlar ketmaydi.",
  "Кому уходит": "Kimga yuboriladi",
  "Пока никто не подписан. На вкладке «Получатели» откройте получателя (или добавьте нового) и включите отчёт 10 «Карточки категорий».":
    "Hozircha hech kim obuna bo'lmagan. «Qabul qiluvchilar» bo'limida qabul qiluvchini oching (yoki yangisini qo'shing) va 10-hisobot «Toifa kartochkalari»ni yoqing.",
  "Категории: {name}": "Toifalar: {name}",
  "Свой выбор": "O'z tanlovi",
  "Свой выбор категорий": "Toifalarni o'zi tanlash",
  "Отправить тестовые карточки на реальных данных в «{chat}»? Получателям они не уйдут.":
    "Haqiqiy ma'lumotlardagi sinov kartochkalarini «{chat}»ga yuborasizmi? Qabul qiluvchilarga ular ketmaydi.",
  "Данные — как на странице категории «Продаж», в области получателя: ТП видит только свои цифры, СВ — свой регион, РМ — свои регионы. Тест записывается в историю.":
    "Ma'lumotlar — «Savdo» toifa sahifasidagidek, qabul qiluvchi doirasida: TP faqat o'z raqamlarini, SV — o'z hududini, RM — o'z hududlarini ko'radi. Sinov tarixga yoziladi.",
  "Карточка {n}": "Kartochka {n}",
  // Отчёт 11 «Итоги дня по категориям» — в той же вкладке.
  "11. Итоги дня по категориям": "11. Toifalar bo'yicha kun yakunlari",
  "Итоги дня по категориям": "Toifalar bo'yicha kun yakunlari",
  "итог кат.": "toifa yak.",
  "9:00 — итог прошлого рабочего дня, 19:00 — итог дня: по картинке на каждую выбранную категорию — продажи за день (кг, сумма, точки, заказы), план дня и выполнение, к прошлому рабочему дню и неделе назад, по дням за две недели, регионы или ТП за день, артикулы дня с фото и что за день не продавалось. Категории — те же, что в «Карточках категорий»":
    "9:00 — oldingi ish kuni yakuni, 19:00 — kun yakuni: har bir tanlangan toifaga bittadan rasm — kunlik savdo (kg, summa, nuqtalar, buyurtmalar), kunlik reja va bajarilishi, oldingi ish kuni va bir hafta oldingiga nisbatan, ikki hafta kunlar bo'yicha, kun bo'yicha hududlar yoki TP, rasmli kun artikullari va kun davomida sotilmaganlar. Toifalar — «Toifa kartochkalari»dagi bilan bir xil",
  "Отчёт 11 «Итоги дня по категориям» — те же категории, но продажи за один день: кг, сумма, точки и заказы, план дня и выполнение, к прошлому рабочему дню и неделе назад, две недели по дням, регионы или ТП за день, артикулы дня с фото и что за день не продавалось.":
    "11-hisobot «Toifalar bo'yicha kun yakunlari» — o'sha toifalar, lekin bir kunlik savdo: kg, summa, nuqtalar va buyurtmalar, kunlik reja va bajarilishi, oldingi ish kuni va bir hafta oldingiga nisbatan, ikki hafta kunlar bo'yicha, kun bo'yicha hududlar yoki TP, rasmli kun artikullari va kun davomida sotilmaganlar.",
  "Пока никто не подписан. На вкладке «Получатели» откройте получателя (или добавьте нового) и включите отчёт 10 «Карточки категорий» или 11 «Итоги дня по категориям».":
    "Hozircha hech kim obuna bo'lmagan. «Qabul qiluvchilar» bo'limida qabul qiluvchini oching (yoki yangisini qo'shing) va 10-hisobot «Toifa kartochkalari» yoki 11-hisobot «Toifalar bo'yicha kun yakunlari»ni yoqing.",
  "отчёт {list}": "hisobot {list}",
  "Итоги дня по категориям — продажи за день по тем же категориям.": "Toifalar bo'yicha kun yakunlari — o'sha toifalar bo'yicha kunlik savdo.",
  "Выберите категории и нажмите «Предпросмотр» — карточки появятся здесь.": "Toifalarni tanlang va «Oldindan ko'rish»ni bosing — kartochkalar shu yerda chiqadi.",
};

export default dict;
