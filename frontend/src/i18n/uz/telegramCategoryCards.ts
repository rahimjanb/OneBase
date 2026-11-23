import type { Dict } from "../core";

/**
 * «Настройки → Telegram → Автоматические отчёты → Карточки категорий» (отчёт 11): выбор категорий, расписание, получатели, предпросмотр и тест —
 * components/telegram/TelegramCategoryCards.tsx, карточка-ссылка на странице Telegram, lib/telegram-category-cards.ts.
 */
const dict: Dict = {
  "Карточки категорий": "Toifa kartochkalari",
  "итог прошлого рабочего дня": "oldingi ish kuni yakuni",
  "каждый день": "har kuni",
  "Выберите категории — «Придзел», «Помадка» (Могуль) и другие — и по расписанию в Telegram уйдёт картинка по каждой только за день: кг, сумма, точки и заказы, артикулы, план дня и выполнение, регионы или ТП, артикулы дня с фото.":
    "Toifalarni tanlang — «Придзел», «Помадка» (Могуль) va boshqalar — jadval bo'yicha Telegramga har biri bo'yicha faqat kunlik rasm yuboriladi: kg, summa, nuqtalar va buyurtmalar, artikullar, kunlik reja va bajarilishi, hududlar yoki TP, rasmli kun artikullari.",
  "Отдельная картинка на каждую выбранную категорию — только за один день, без месяца, в области получателя: кг, сумма, точки и заказы, артикулы, цена за кг, план дня и выполнение, регионы (РМ) или ТП (СВ) за день, артикулы дня с фото. Все картинки — одним альбомом.":
    "Har bir tanlangan toifaga alohida rasm — faqat bir kunlik, oysiz, qabul qiluvchi doirasida: kg, summa, nuqtalar va buyurtmalar, artikullar, kg narxi, kunlik reja va bajarilishi, kun bo'yicha hududlar (RM) yoki TP (SV), rasmli kun artikullari. Barcha rasmlar — bitta albomda.",
  "Пока никто не подписан. На вкладке «Получатели» откройте получателя (или добавьте нового) и включите отчёт 11 «Итоги дня по категориям».":
    "Hozircha hech kim obuna bo'lmagan. «Qabul qiluvchilar» bo'limida qabul qiluvchini oching (yoki yangisini qo'shing) va 11-hisobot «Toifalar bo'yicha kun yakunlari»ni yoqing.",
  "Категории по умолчанию": "Standart toifalar",
  "Ничего не выбрано — уйдут все {n} категорий.": "Hech narsa tanlanmagan — barcha {n} ta toifa yuboriladi.",
  "Уйдут: {list}. Получателям без своего выбора.": "Yuboriladi: {list}. O'z tanlovi bo'lmagan qabul qiluvchilarga.",
  "Когда уходит": "Qachon yuboriladi",
  "Изменить время": "Vaqtni o'zgartirish",
  "Выключить «{name}»": "«{name}»ni o'chirish",
  "Аккаунт Telegram для автоотчётов не выбран — отправки не уйдут.": "Avtohisobotlar uchun Telegram akkaunti tanlanmagan — yuborishlar ketmaydi.",
  "Кому уходит": "Kimga yuboriladi",
  "Категории: {name}": "Toifalar: {name}",
  "Свой выбор": "O'z tanlovi",
  "Свой выбор категорий": "Toifalarni o'zi tanlash",
  "Отправить тестовые карточки на реальных данных в «{chat}»? Получателям они не уйдут.":
    "Haqiqiy ma'lumotlardagi sinov kartochkalarini «{chat}»ga yuborasizmi? Qabul qiluvchilarga ular ketmaydi.",
  "Данные — как на странице категории «Продаж», в области получателя: ТП видит только свои цифры, СВ — свой регион, РМ — свои регионы. Тест записывается в историю.":
    "Ma'lumotlar — «Savdo» toifa sahifasidagidek, qabul qiluvchi doirasida: TP faqat o'z raqamlarini, SV — o'z hududini, RM — o'z hududlarini ko'radi. Sinov tarixga yoziladi.",
  "Карточка {n}": "Kartochka {n}",
  // Отчёт 11 «Итоги дня по категориям».
  "11. Итоги дня по категориям": "11. Toifalar bo'yicha kun yakunlari",
  "Итоги дня по категориям": "Toifalar bo'yicha kun yakunlari",
  "итог кат.": "toifa yak.",
  "9:00 — итог прошлого рабочего дня, 19:00 — итог дня: по картинке на каждую выбранную категорию — продажи только за день, без месяца (кг, сумма, точки, заказы, артикулы, цена за кг), план дня и выполнение, регионы или ТП за день, артикулы дня с фото. Категории — те же, что в «Карточках категорий»":
    "9:00 — oldingi ish kuni yakuni, 19:00 — kun yakuni: har bir tanlangan toifaga bittadan rasm — faqat kunlik savdo, oysiz (kg, summa, nuqtalar, buyurtmalar, artikullar, kg narxi), kunlik reja va bajarilishi, kun bo'yicha hududlar yoki TP, rasmli kun artikullari. Toifalar — «Toifa kartochkalari»dagi bilan bir xil",
  "Выберите категории и нажмите «Предпросмотр» — карточки появятся здесь.": "Toifalarni tanlang va «Oldindan ko'rish»ni bosing — kartochkalar shu yerda chiqadi.",
};

export default dict;
