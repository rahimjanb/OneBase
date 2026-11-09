import type { Dict } from "../core";

/** «Настройки → Пользователи»: права в разделах отделов и настройках — группы, галочки, подсказки и ошибки сервера. */
const dict: Dict = {
  // Окно пользователя
  "Профиль, роль, разделы и права в них": "Profil, rol, bo'limlar va ulardagi huquqlar",
  "Роль, разделы, права и пароль действуют сразу.": "Rol, bo'limlar, huquqlar va parol darhol amal qiladi.",
  "Свои роль, разделы и права меняет другой администратор": "O'z rolingiz, bo'limlaringiz va huquqlaringizni boshqa administrator o'zgartiradi",
  "Действия в отделах и настройки": "Bo'limlardagi amallar va sozlamalar",
  "Отмеченное сотрудник видит в меню и может делать: принимать на склад, проверять качество, вести закупки и задания, открывать страницы настроек.":
    "Belgilanganini xodim menyuda ko'radi va bajara oladi: omborga qabul qilish, sifatni tekshirish, xaridlar va topshiriqlarni yuritish, sozlamalar sahifalarini ochish.",
  "У вас самих нет этого права — выдать его может администратор": "Sizning o'zingizda bu huquq yo'q — uni administrator bera oladi",
  "Нужен раздел «{name}»": "«{name}» bo'limi kerak",
  "Нужно для: {list}": "Kerak: {list} uchun",
  "Разделы или права отмечены вручную, не как у роли": "Bo'limlar yoki huquqlar qo'lda belgilangan, roldagidek emas",

  // Группы
  "Снабжение и склад": "Ta'minot va ombor",
  "Заявки, закупки, склад и контроль качества сырья: что сотрудник видит и что может делать.":
    "Arizalar, xaridlar, ombor va xomashyo sifati nazorati: xodim nimani ko'radi va nima qila oladi.",
  "Задания на выпуск, техкарты и сменные отчёты.": "Ishlab chiqarish topshiriqlari, texkartalar va smena hisobotlari.",
  "Какие страницы «Настроек» откроются сотруднику.": "Xodimga «Sozlamalar»ning qaysi sahifalari ochiladi.",

  // Права: снабжение и склад
  "Свои заявки": "O'z arizalari",
  "Создавать заявки на закупку и следить за ними — «Заявки → Мои».": "Xarid arizalarini yaratish va kuzatish — «Arizalar → Mening».",
  "Все заявки и снабжение — просмотр": "Barcha arizalar va ta'minot — ko'rish",
  "Заявки всех отделов, обзор снабжения, тендеры, поставки, поставщики, склад и отчёты — без изменений.":
    "Barcha bo'limlar arizalari, ta'minot sharhi, tenderlar, yetkazib berishlar, yetkazib beruvchilar, ombor va hisobotlar — o'zgartirishsiz.",
  "Закупки": "Xaridlar",
  "«К закупке» и «Куплено», тендеры, поставщики, обработка заявок, отмена поставок.":
    "«Xarid uchun» va «Sotib olindi», tenderlar, yetkazib beruvchilar, arizalarni qayta ishlash, yetkazib berishlarni bekor qilish.",
  "Склад": "Ombor",
  "Остатки, приход, выдача и «К выдаче», приезд машин и приёмка с весом, партии, движения, инвентаризация.":
    "Qoldiqlar, kirim, berish va «Berishga», mashinalar kelishi va og'irlik bilan qabul, partiyalar, harakatlar, inventarizatsiya.",
  "Качество сырья": "Xomashyo sifati",
  "Лаборатория: проверка документов и показателей партий, пробы, спецификации.": "Laboratoriya: partiyalar hujjatlari va ko'rsatkichlarini tekshirish, namunalar, spetsifikatsiyalar.",

  // Права: производство
  "Производство — просмотр": "Ishlab chiqarish — ko'rish",
  "Обзор, задания, техкарты, смены и отчёты — без изменений.": "Sharh, topshiriqlar, texkartalar, smenalar va hisobotlar — o'zgartirishsiz.",
  "Задания на выпуск": "Ishlab chiqarish topshiriqlari",
  "Создавать, выпускать и закрывать задания, утверждать смены.": "Topshiriqlarni yaratish, chiqarish va yopish, smenalarni tasdiqlash.",
  "Технолог": "Texnolog",
  "Техкарты: этапы, рецептура, нормы, версии и утверждение.": "Texkartalar: bosqichlar, retseptura, me'yorlar, versiyalar va tasdiqlash.",
  "Ввод смен": "Smenalarni kiritish",
  "Сменные отчёты: замесы, выпечка, брак, упаковка.": "Smena hisobotlari: qorishmalar, pishirish, yaroqsizlik, qadoqlash.",

  // Права: настройки
  "Пользователи и роли": "Foydalanuvchilar va rollar",
  "Сотрудники, роли и доступ; отделы и их руководители.": "Xodimlar, rollar va kirish; bo'limlar va ularning rahbarlari.",
  "Настройки снабжения": "Ta'minot sozlamalari",
  "Категории закупок, маршрут согласования, склады, контроль качества.": "Xarid toifalari, kelishuv yo'nalishi, omborlar, sifat nazorati.",
  "Интеграции и источники данных": "Integratsiyalar va ma'lumot manbalari",
  "Linko, Google Sheets и Verifix: подключения и обновление данных.": "Linko, Google Sheets va Verifix: ulanishlar va ma'lumotlarni yangilash.",
  "Настройки AI": "AI sozlamalari",
  "Провайдеры, ключи, модели и доступ AI-агентов.": "Provayderlar, kalitlar, modellar va AI-agentlarga kirish.",
  "Аккаунты Telegram и AI-агент в чатах.": "Telegram akkauntlari va chatlardagi AI-agent.",
  "Продажи: оргструктура и цели": "Savdo: tashkiliy tuzilma va maqsadlar",
  "Синхронизация с Linko, направления, дилеры регионов, ТП и цели.": "Linko bilan sinxronlash, yo'nalishlar, hudud dilerlari, savdo vakillari va maqsadlar.",
  "Журнал ошибок": "Xatolar jurnali",
  "Ошибки интеграций и сервера, отмена идущей синхронизации.": "Integratsiya va server xatolari, ketayotgan sinxronlashni bekor qilish.",

  // Ошибки сервера
  "Неизвестное право: {list}.": "Noma'lum huquq: {list}.",
  "Право «{name}» выдать нельзя: у вас самих его нет.": "«{name}» huquqini berib bo'lmaydi: sizning o'zingizda u yo'q.",
  "«{name}» действует только вместе с разделом «{section}» — отметьте раздел.": "«{name}» faqat «{section}» bo'limi bilan amal qiladi — bo'limni belgilang.",
  "Свои права изменить нельзя — попросите другого администратора.": "O'z huquqlaringizni o'zgartirib bo'lmaydi — boshqa administratordan so'rang.",
};

export default dict;
