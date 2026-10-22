import type { Dict } from "../core";

/** Оболочка: меню, шапка, нижняя панель, уведомления, разрешения устройства, вход, ошибки, общие компоненты. */
const dict: Dict = {
  // Меню и нижняя панель
  "OneBase — на главную": "OneBase — bosh sahifaga",
  "Рабочее пространство": "Ish maydoni",
  "Помощь и поддержка": "Yordam va qo'llab-quvvatlash",
  "Раздел в разработке": "Bo'lim ishlab chiqilmoqda",
  "скоро": "tez orada",
  "Обзор": "Umumiy ko'rinish",
  "Главная": "Bosh sahifa",
  "Отделы": "Bo'limlar",
  "Задачи": "Vazifalar",
  "Отчёты": "Hisobotlar",
  "Файлы": "Fayllar",
  "AI-консультант": "AI maslahatchi",
  "Консультант": "Maslahatchi",
  "AI Data Manager": "AI Data Manager",
  "Данные": "Ma'lumotlar",
  "Рассылки": "Tarqatmalar",
  "Новая рассылка": "Yangi tarqatma",
  "Настройки": "Sozlamalar",
  "Продажи": "Savdo",
  "Профиль": "Profil",
  "Выйти": "Chiqish",
  "Основные разделы": "Asosiy bo'limlar",
  "Разделы и действия": "Bo'limlar va amallar",
  "Закрыть меню": "Menyuni yopish",
  "Открыть меню разделов": "Bo'limlar menyusini ochish",
  "Разделы продаж": "Savdo bo'limlari",
  "Язык интерфейса": "Interfeys tili",
  "Светлая тема": "Yorug' mavzu",
  "Тёмная тема": "Qorong'i mavzu",
  "Назад": "Orqaga",
  "Навигация": "Navigatsiya",
  "Закрыть": "Yopish",
  "Здесь появится:": "Bu yerda paydo bo'ladi:",
  "План выполнен": "Reja bajarildi",
  "В норме": "Me'yorda",
  "Требует внимания": "E'tibor talab qiladi",

  // Обновление жестом
  "Обновляем": "Yangilanmoqda",
  "Отпустите, чтобы обновить": "Yangilash uchun qo'yib yuboring",
  "Потяните, чтобы обновить": "Yangilash uchun torting",

  // Уведомления
  "Уведомления": "Bildirishnomalar",
  "Уведомления: {n} новых": "Bildirishnomalar: {n} ta yangi",
  "Прочитать все": "Hammasini o'qilgan deb belgilash",
  "Уведомлений пока нет": "Hozircha bildirishnomalar yo'q",
  "Уведомления и геолокация на этом устройстве": "Ushbu qurilmada bildirishnomalar va geolokatsiya",
  "Готово: уведомления включены на этом устройстве.": "Tayyor: ushbu qurilmada bildirishnomalar yoqildi.",
  "Чтобы получать уведомления на iPhone, установите {app}: в Safari «Поделиться» → «На экран „Домой“» и открывайте с иконки.":
    "iPhone'da bildirishnomalarni olish uchun {app} ni o'rnating: Safari'da «Ulashish» → «Bosh ekranga» va belgidan oching.",
  "Чтобы получать уведомления на телефоне, установите {app}: в Safari «Поделиться» → «На экран „Домой“» и открывайте с иконки.":
    "Telefonda bildirishnomalarni olish uchun {app} ni o'rnating: Safari'da «Ulashish» → «Bosh ekranga» va belgidan oching.",
  "Включить": "Yoqish",
  "Не сейчас": "Hozir emas",
  "Скрыть": "Yashirish",

  // Разрешения устройства
  "Уведомления и геолокация": "Bildirishnomalar va geolokatsiya",
  "Уведомления о задачах": "Vazifalar haqida bildirishnomalar",
  "нужна установка": "o'rnatish kerak",
  "включены": "yoqilgan",
  "запрещены": "taqiqlangan",
  "выключены": "o'chirilgan",
  "Уведомления включены на этом устройстве.": "Ushbu qurilmada bildirishnomalar yoqildi.",
  "Уведомления запрещены — как включить, написано ниже.": "Bildirishnomalar taqiqlangan — qanday yoqish quyida yozilgan.",
  "Не удалось подписаться — попробуйте ещё раз.": "Obuna bo'lib bo'lmadi — qayta urinib ko'ring.",
  "Уведомления на этом устройстве отключены.": "Ushbu qurilmada bildirishnomalar o'chirildi.",
  "Проверочное уведомление отправлено — оно придёт через несколько секунд.": "Sinov bildirishnomasi yuborildi — u bir necha soniyada keladi.",
  "Устройств с включёнными уведомлениями нет.": "Bildirishnomalari yoqilgan qurilmalar yo'q.",
  "Не удалось отправить проверку.": "Sinovni yuborib bo'lmadi.",
  "На iPhone уведомления приходят только в установленное приложение: в Safari нажмите «Поделиться» → «На экран „Домой“» и открывайте {app} с иконки.":
    "iPhone'da bildirishnomalar faqat o'rnatilgan ilovaga keladi: Safari'da «Ulashish» → «Bosh ekranga» ni bosing va {app} ni belgidan oching.",
  "Этот браузер не поддерживает уведомления. Откройте {app} в Chrome или установите приложение.":
    "Bu brauzer bildirishnomalarni qo'llab-quvvatlamaydi. {app} ni Chrome'da oching yoki ilovani o'rnating.",
  "Отправляем…": "Yuborilmoqda…",
  "Проверить": "Tekshirish",
  "Отключить": "O'chirish",
  "Включаем…": "Yoqilmoqda…",
  "Включить уведомления": "Bildirishnomalarni yoqish",
  "Настройки iPhone → «Уведомления» → {app} → «Допуск уведомлений». Если вы отказали в окне разрешения, удалите приложение с экрана «Домой» и добавьте заново.":
    "iPhone sozlamalari → «Bildirishnomalar» → {app} → «Bildirishnomalarga ruxsat». Agar ruxsat oynasida rad etgan bo'lsangiz, ilovani bosh ekrandan o'chirib, qaytadan qo'shing.",
  "Chrome: значок слева от адреса → «Разрешения» → «Уведомления» → «Разрешить». В установленном приложении: долгое нажатие на иконку → «О приложении» → «Уведомления».":
    "Chrome: manzil chap tomonidagi belgi → «Ruxsatlar» → «Bildirishnomalar» → «Ruxsat berish». O'rnatilgan ilovada: belgini uzoq bosing → «Ilova haqida» → «Bildirishnomalar».",
  "Геолокация": "Geolokatsiya",
  "разрешена": "ruxsat berilgan",
  "запрещена": "taqiqlangan",
  "нет": "yo'q",
  "не задана": "belgilanmagan",
  "Геолокация разрешена.": "Geolokatsiyaga ruxsat berildi.",
  "Геолокация запрещена — как включить, написано ниже.": "Geolokatsiya taqiqlangan — qanday yoqish quyida yozilgan.",
  "Не удалось определить местоположение — проверьте, что GPS включён.": "Joylashuvni aniqlab bo'lmadi — GPS yoqilganini tekshiring.",
  "Нужна, чтобы отметить визит у точки и построить маршрут от вашего места.": "Nuqtada tashrifni belgilash va joylashuvingizdan marshrut tuzish uchun kerak.",
  "Определяем…": "Aniqlanmoqda…",
  "Разрешить геолокацию": "Geolokatsiyaga ruxsat berish",
  "В окне разрешения выберите «Разрешить при использовании сайта», а не «Только в этот раз» — тогда спрашивать больше не будут.":
    "Ruxsat oynasida «Faqat shu safar» emas, «Saytdan foydalanishda ruxsat berish» ni tanlang — shunda boshqa so'ralmaydi.",
  "Настройки iPhone → «Конфиденциальность» → «Службы геолокации» → «Сайты Safari» → «При использовании». В Safari: «аА» → «Настройки веб-сайта» → «Геопозиция» → «Разрешить».":
    "iPhone sozlamalari → «Maxfiylik» → «Geolokatsiya xizmatlari» → «Safari saytlari» → «Foydalanish vaqtida». Safari'da: «aA» → «Veb-sayt sozlamalari» → «Joylashuv» → «Ruxsat berish».",
  "Chrome: значок слева от адреса → «Разрешения» → «Местоположение» → «Разрешить». Ещё в Android: «Настройки» → «Приложения» → Chrome → «Разрешения» → «Местоположение» → «Разрешить только во время использования». После этого обновите страницу.":
    "Chrome: manzil chap tomonidagi belgi → «Ruxsatlar» → «Joylashuv» → «Ruxsat berish». Android'da yana: «Sozlamalar» → «Ilovalar» → Chrome → «Ruxsatlar» → «Joylashuv» → «Faqat foydalanish vaqtida ruxsat berish». Shundan keyin sahifani yangilang.",

  // Вход
  "Вход": "Kirish",
  "Вход · OneBase": "Kirish · OneBase",
  "Логин или почта": "Login yoki pochta",
  "Пароль": "Parol",
  "Входим…": "Kirilmoqda…",
  "Войти": "Kirish",
  "Сервер OneBase недоступен. Попробуйте позже.": "OneBase serveri mavjud emas. Keyinroq urinib ko'ring.",
  "Неверный логин или пароль.": "Login yoki parol noto'g'ri.",
  "Слишком много попыток. Подождите минуту.": "Urinishlar juda ko'p. Bir daqiqa kuting.",
  "Ошибка входа ({status}).": "Kirish xatosi ({status}).",

  // Ошибки и доступ
  "Не удалось загрузить страницу": "Sahifani yuklab bo'lmadi",
  "Сервер OneBase не ответил или вернул ошибку. Попробуйте ещё раз через несколько секунд.":
    "OneBase serveri javob bermadi yoki xato qaytardi. Bir necha soniyadan keyin qayta urinib ko'ring.",
  "Код ошибки: {code}": "Xato kodi: {code}",
  "Попробовать снова": "Qayta urinish",
  "Нет доступа · OneBase": "Ruxsat yo'q · OneBase",
  "Недостаточно прав": "Huquqlar yetarli emas",
  "Недостаточно прав.": "Huquqlar yetarli emas.",
  "У вашей учётной записи нет доступа к этому разделу. Попросите администратора выдать нужное право или войдите под другим пользователем.":
    "Hisobingizda bu bo'limga ruxsat yo'q. Administratordan kerakli huquqni so'rang yoki boshqa foydalanuvchi sifatida kiring.",
  "Войти под другим пользователем": "Boshqa foydalanuvchi sifatida kirish",
  "На главную": "Bosh sahifaga",
  "Нет связи с сервером OneBase — ответ не пришёл. Обновите страницу и проверьте, не выполнилось ли действие, затем повторите.":
    "OneBase serveri bilan aloqa yo'q — javob kelmadi. Sahifani yangilang, amal bajarilmaganini tekshiring va qayta urinib ko'ring.",
  "Ошибка {status}": "Xato {status}",
  "Сервер OneBase ответил HTTP {status}": "OneBase serveri HTTP {status} javobini qaytardi",

  // Общие слова (lib/format)
  "файл": "fayl",
  "файла": "fayl",
  "файлов": "fayl",
  "папка": "papka",
  "папки": "papka",
  "папок": "papka",
  "Доброе утро": "Xayrli tong",
  "Добрый день": "Xayrli kun",
  "Добрый вечер": "Xayrli kech",
  "Доброй ночи": "Xayrli tun",
};

export default dict;
