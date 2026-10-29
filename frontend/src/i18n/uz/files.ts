import type { Dict } from "../core";

/** «Файлы»: обзор отделов, папки отдела, диалоги файлов, подключение папки к Windows (lib/files). */
const dict: Dict = {
  // Контекст «файл::» — у файла «Имя» значит название (у человека «Имя» — «Ism»).
  "файл::Имя": "Nomi",
  // Страницы
  "Файлы": "Fayllar",
  "Файлы отделов — на сайте и в подключённой папке Windows, это одно хранилище": "Bo'lim fayllari — saytda va ulangan Windows papkasida, bu bitta saqlash joyi",
  "Файлы отдела «{name}»": "«{name}» bo'limi fayllari",
  "Папка отдела «{name}»": "«{name}» bo'limi papkasi",

  // Обзор отделов
  "Windows подключён": "Windows ulangan",
  "Windows отозван": "Windows bekor qilingan",
  "Поиск файлов: имя, тип, папка, отдел, автор": "Fayllarni qidirish: nomi, turi, papka, bo'lim, muallif",
  "Ничего не найдено": "Hech narsa topilmadi",
  "Нет отделов, к файлам которых у вас есть доступ": "Fayllariga ruxsatingiz bor bo'limlar yo'q",
  "Отдел": "Bo'lim",
  "Доступ": "Ruxsat",
  "Содержимое": "Tarkibi",
  "Размер": "Hajmi",
  "Изменён": "O'zgartirilgan",
  "файлов пока нет": "hozircha fayllar yo'q",
  "файл": "fayl",
  "файла": "fayl",
  "файлов": "fayl",
  "папка": "papka",
  "папки": "papka",
  "папок": "papka",

  // lib/files: доступ, даты, загрузка
  "нет доступа": "ruxsat yo'q",
  "только чтение": "faqat o'qish",
  "чтение и запись": "o'qish va yozish",
  "администратор": "administrator",
  "Сегодня, {time}": "Bugun, {time}",
  "Вчера, {time}": "Kecha, {time}",
  "Нет связи с сервером OneBase — файл не загружен.": "OneBase serveri bilan aloqa yo'q — fayl yuklanmadi.",
  "Ошибка {status}": "Xato {status}",
  "Файл слишком большой для загрузки через сайт.": "Fayl sayt orqali yuklash uchun juda katta.",
  "Все": "Hammasi",
  "Документы": "Hujjatlar",
  "Таблицы": "Jadvallar",
  "Картинки": "Rasmlar",
  "Другое": "Boshqa",
  "Документ": "Hujjat",
  "Таблица": "Jadval",
  "Картинка": "Rasm",
  "Файл": "Fayl",

  // Папка отдела
  "Путь к папке": "Papkaga yo'l",
  "Поиск в отделе «{name}»": "«{name}» bo'limida qidirish",
  "К файлам": "Fayllarga",
  "Корзина": "Savat",
  "Новая папка": "Yangi papka",
  "Загрузить": "Yuklash",
  "Загрузка: {names}": "Yuklanmoqda: {names}",
  "{loaded} из {total}": "{loaded} / {total}",
  "Скрыть": "Yashirish",
  "перетащите файлы сюда, чтобы загрузить": "yuklash uchun fayllarni shu yerga torting",
  "Имя": "Ism",
  "Тип": "Turi",
  "Папка": "Papka",
  "{n} эл.": "{n} ta element",
  "пусто": "bo'sh",
  "Переименовать": "Nomini o'zgartirish",
  "Перенести": "Ko'chirish",
  "В корзину": "Savatga",
  "Папку «{name}» со всем содержимым — в корзину?": "«{name}» papkasi butun tarkibi bilan savatga o'tkazilsinmi?",
  "Просмотр": "Ko'rish",
  "Скачать": "Yuklab olish",
  "Версии": "Versiyalar",
  "«{name}» — в корзину?": "«{name}» savatga o'tkazilsinmi?",
  "Папка пустая — загрузите файлы или перетащите их сюда": "Papka bo'sh — fayllarni yuklang yoki shu yerga torting",
  "Папка пустая": "Papka bo'sh",
  "Создать": "Yaratish",
  "Переименовать файл": "Fayl nomini o'zgartirish",
  "Переименовать папку": "Papka nomini o'zgartirish",
  "Сохранить": "Saqlash",
  "Действия": "Amallar",
  "Корзина пустая": "Savat bo'sh",
  "удалено {date}": "o'chirilgan: {date}",
  "Восстановить": "Tiklash",

  // Диалоги
  "Закрыть": "Yopish",
  "Отмена": "Bekor qilish",
  "Перенести «{name}»": "«{name}» ni ko'chirish",
  "Вложенных папок нет": "Ichki papkalar yo'q",
  "Перенести в «{name}»": "«{name}» ga ko'chirish",
  "Версии «{name}»": "«{name}» versiyalari",
  "текущая": "joriy",
  "Скачать версию {n}": "{n}-versiyani yuklab olish",
  "Лист пустой": "Varaq bo'sh",
  "Показаны первые строки и столбцы — весь файл откройте после скачивания.": "Birinchi qatorlar va ustunlar ko'rsatilgan — butun faylni yuklab olgandan keyin oching.",
  "Показано начало документа.": "Hujjatning boshi ko'rsatilgan.",

  // Подключение папки к Windows
  "Подключено": "Ulangan",
  "Не настроено": "Sozlanmagan",
  "Отозвано": "Bekor qilingan",
  "Подключить к Windows": "Windows'ga ulash",
  "Подключение «{name}» к Windows": "«{name}» ni Windows'ga ulash",
  "Подключение «{name}» к Windows ({type})": "«{name}» ni Windows'ga ulash ({type})",
  "Адрес: {value}": "Manzil: {value}",
  "Путь Windows: {value}": "Windows yo'li: {value}",
  "Логин: {value}": "Login: {value}",
  "Пароль: {value}": "Parol: {value}",
  "Как подключить папку отдела «{name}» к Windows": "«{name}» bo'limi papkasini Windows'ga qanday ulash mumkin",
  "Способ: {value}": "Usul: {value}",
  "Пароль: выдаёт OneBase в окне «Подключить к Windows» (в файл не записывается).": "Parol: OneBase «Windows'ga ulash» oynasida beradi (faylga yozilmaydi).",
  "Адрес без пароля: выдаёт OneBase в окне «Подключить к Windows» (в файл не записывается).":
    "Parolsiz manzil: OneBase «Windows'ga ulash» oynasida beradi (faylga yozilmaydi).",
  "Папка отдела «{name}» — подключить сетевым диском в Windows, логин и пароль не нужны.":
    "«{name}» bo'limi papkasi — Windows'da tarmoq diski sifatida ulang, login va parol kerak emas.",
  "Шаг {n}. {step}": "{n}-qadam. {step}",
  "Если не получается:": "Agar ishlamasa:",
  "Подключение {name} к Windows.txt": "{name} Windows ulanishi.txt",
  "последняя активность: {date}": "oxirgi faollik: {date}",
  "Адрес подключения ещё не настроен на сервере. Пока его нет, подключить папку к Windows нельзя — файлы отдела доступны здесь, на сайте.":
    "Ulanish manzili serverda hali sozlanmagan. U bo'lmaguncha papkani Windows'ga ulab bo'lmaydi — bo'lim fayllari shu yerda, saytda mavjud.",
  "В {env} на сервере: {domain}, затем {command}.": "Serverdagi {env} faylida: {domain}, so'ng {command}.",
  "В Cloudflare Zero Trust → Tunnels → Public Hostname: {host} → {target} и правило кэша «Bypass» для этого домена.":
    "Cloudflare Zero Trust → Tunnels → Public Hostname'da: {host} → {target} va shu domen uchun «Bypass» kesh qoidasi.",
  "Здесь: «Создать подключение» → «Проверить подключение» — все шаги должны быть ✓.":
    "Shu yerda: «Ulanish yaratish» → «Ulanishni tekshirish» — barcha qadamlar ✓ bo'lishi kerak.",
  "У вас доступ к отделу только на чтение — подключение даёт запись, поэтому логин и пароль показываются сотрудникам отдела с правом записи и администраторам. Файлы отдела доступны здесь, на сайте.":
    "Sizda bo'limga faqat o'qish ruxsati bor — ulanish yozish huquqini beradi, shuning uchun login va parol yozish huquqi bor bo'lim xodimlari va administratorlarga ko'rsatiladi. Bo'lim fayllari shu yerda, saytda mavjud.",
  "Подключение отозвано — вход по старому логину не работает.": "Ulanish bekor qilingan — eski login bilan kirish ishlamaydi.",
  "Для отдела ещё не создано подключение.": "Bo'lim uchun hali ulanish yaratilmagan.",
  "Создайте его кнопкой ниже.": "Uni quyidagi tugma bilan yarating.",
  "Попросите администратора OneBase его создать.": "OneBase administratoridan uni yaratishni so'rang.",
  "Адрес папки": "Papka manzili",
  "Путь Windows": "Windows yo'li",
  "Логин": "Login",
  "Пароль": "Parol",
  "Показать": "Ko'rsatish",
  "Скопировать всё": "Hammasini nusxalash",
  "Скопировать для сотрудников": "Xodimlar uchun nusxalash",
  "Скачать инструкцию": "Yo'riqnomani yuklab olish",
  "Адрес без пароля": "Parolsiz manzil",
  "Диск подключается по этому адресу без логина и пароля, Word и Excel открывают файлы сразу. Адрес работает как пароль — отправляйте его только сотрудникам отдела.":
    "Disk shu manzil orqali login va parolsiz ulanadi, Word va Excel fayllarni darhol ochadi. Manzil parol kabi ishlaydi — uni faqat bo'lim xodimlariga yuboring.",
  "Вход по логину и паролю": "Login va parol bilan kirish",
  "Для Проводника. Word и Excel из Microsoft 365 по паролю файлы не открывают — для работы с документами подключайте диск по адресу без пароля.":
    "Provodnik (Explorer) uchun. Microsoft 365 dagi Word va Excel parol bilan fayllarni ochmaydi — hujjatlar bilan ishlash uchun diskni parolsiz manzil orqali ulang.",
  "Выпустить новую ссылку? Старая перестанет работать сразу — на компьютерах отдела диск нужно будет подключить по новой.":
    "Yangi havola chiqarilsinmi? Eskisi darhol ishlamay qoladi — bo'lim kompyuterlarida diskni yangisi orqali qayta ulash kerak bo'ladi.",
  "Новая ссылка": "Yangi havola",
  "Администратор": "Administrator",
  "Создать подключение": "Ulanish yaratish",
  "Сменить пароль? Старый перестанет работать сразу — на подключённых компьютерах нужно будет ввести новый.":
    "Parol almashtirilsinmi? Eskisi darhol ishlamay qoladi — ulangan kompyuterlarda yangisini kiritish kerak bo'ladi.",
  "Сменить пароль": "Parolni almashtirish",
  "Отозвать подключение? Все компьютеры отдела — и по ссылке, и по логину — сразу потеряют доступ к папке.":
    "Ulanish bekor qilinsinmi? Bo'limning barcha kompyuterlari — havola orqali ham, login orqali ham — papkaga kirish huquqini darhol yo'qotadi.",
  "Отозвать": "Bekor qilish",
  "Проверить подключение": "Ulanishni tekshirish",
  "Создано": "Yaratilgan",
  "Пароль сменён": "Parol almashtirilgan",
  "Если не получается": "Agar ishlamasa",
  "Скопировать: {label}": "Nusxalash: {label}",
  "Скопировано": "Nusxalandi",
  "Скопировать": "Nusxalash",
  "Доступ других отделов к папке": "Boshqa bo'limlarning papkaga ruxsati",
  "Только на сайте и с их собственным логином: подключение Windows этого отдела им не выдаётся.":
    "Faqat saytda va o'z loginlari bilan: bu bo'limning Windows ulanishi ularga berilmaydi.",
  // Общие файлы, блокировки, личный диск Windows
  "Общие файлы": "Umumiy fayllar",
  "Новая общая папка": "Yangi umumiy papka",
  "Папки для нескольких отделов и сотрудников: видны только тем, кому выдан доступ, и не смешиваются с папками отделов.":
    "Bir nechta bo'lim va xodimlar uchun papkalar: faqat kirish berilganlarga ko'rinadi va bo'lim papkalari bilan aralashmaydi.",
  "Общих папок пока нет — создайте первую и выдайте доступ": "Hozircha umumiy papkalar yo'q — birinchisini yarating va kirish bering",
  "Общая папка — для сотрудников и отделов, которым выдан доступ": "Umumiy papka — kirish berilgan xodimlar va bo'limlar uchun",
  "Папка в общей папке «{name}»": "«{name}» umumiy papkasidagi papka",
  "Поиск в папке «{name}»": "«{name}» papkasida qidirish",
  "просмотр": "ko'rish",
  "редактирование": "tahrirlash",
  "управление": "boshqarish",
  "Доступ к папке «{name}»": "«{name}» papkasiga kirish",
  "Папку видят только те, кому выдан доступ. Права проверяются при каждом обращении — и на сайте, и с диска Windows.":
    "Papkani faqat kirish berilganlar ko'radi. Huquqlar har bir murojaatda tekshiriladi — saytda ham, Windows diskida ham.",
  "Доступ пока никому не выдан": "Kirish hali hech kimga berilmagan",
  "Доступ: {name}": "Kirish: {name}",
  "убрать доступ": "kirishni olib tashlash",
  "Выдать доступ": "Kirish berish",
  "Сотруднику": "Xodimga",
  "Отделу": "Bo'limga",
  "Выберите сотрудника": "Xodimni tanlang",
  "Выдать": "Berish",
  "Просмотр — открыть и скачать файлы.": "Ko'rish — fayllarni ochish va yuklab olish.",
  "Редактирование — ещё загружать, менять, переносить и удалять в корзину (нужно право на запись файлов в роли).":
    "Tahrirlash — bundan tashqari yuklash, o'zgartirish, ko'chirish va savatga o'chirish (rolda fayllarga yozish huquqi kerak).",
  "Управление — ещё настраивать доступ к этой папке.": "Boshqarish — bundan tashqari ushbu papkaga kirishni sozlash.",
  "Отдел получает доступ для всех своих сотрудников и для подключения Windows этого отдела.":
    "Bo'lim kirishni barcha xodimlari va ushbu bo'limning Windows ulanishi uchun oladi.",
  "Название папки": "Papka nomi",
  "После переименования диск Windows открывает папку по новому имени.": "Nomi o'zgargach, Windows diski papkani yangi nom bilan ochadi.",
  "Удалить общую папку «{name}»? Удалить можно только пустую — без файлов и папок, в том числе в корзине.":
    "«{name}» umumiy papkasi o'chirilsinmi? Faqat bo'sh papkani o'chirish mumkin — fayl va papkalarsiz, savatdagilar ham.",
  "Удалить пустую папку": "Bo'sh papkani o'chirish",
  "Действует до {date}.": "{date} gacha amal qiladi.",
  "Общие папки отдела": "Bo'limning umumiy papkalari",
  "Открываются по той же ссылке — отдельным сетевым диском по своему адресу.": "Xuddi shu havola orqali ochiladi — o'z manzili bo'yicha alohida tarmoq diski sifatida.",
  "Не выпущена": "Chiqarilmagan",
  "Срок истёк": "Muddati tugagan",
  "Мой диск Windows": "Mening Windows diskim",
  "действует до {date}": "{date} gacha amal qiladi",
  "Один сетевой диск со всем, что вам доступно: папками ваших отделов и общими папками. Права — ваши, не больше: забрали доступ — диск сразу покажет меньше.":
    "Sizga ochiq hamma narsa bitta tarmoq diskida: bo'limlaringiz papkalari va umumiy papkalar. Huquqlar faqat sizniki: kirish olib qo'yilsa, disk darhol kamroq ko'rsatadi.",
  "Адрес подключения ещё не настроен на сервере. Пока его нет, подключить диск к Windows нельзя — файлы доступны здесь, на сайте.":
    "Ulanish manzili serverda hali sozlanmagan. U bo'lmaguncha diskni Windowsga ulab bo'lmaydi — fayllar shu yerda, saytda mavjud.",
  "Личный адрес": "Shaxsiy manzil",
  "Только папка «{name}»": "Faqat «{name}» papkasi",
  "Срок ссылки истёк — диск больше не открывается. Выпустите новую и подключите диск по ней.":
    "Havola muddati tugadi — disk endi ochilmaydi. Yangisini chiqaring va diskni u orqali ulang.",
  "Выпустить новую ссылку? Прежняя перестанет работать сразу — диск нужно будет подключить по новой.":
    "Yangi havola chiqarilsinmi? Avvalgisi darhol ishlashdan to'xtaydi — diskni yangisi orqali ulash kerak bo'ladi.",
  "Выпустить ссылку": "Havola chiqarish",
  "Отключить личную ссылку? Диск на компьютерах перестанет открываться сразу.": "Shaxsiy havola o'chirilsinmi? Kompyuterlardagi disk darhol ochilmay qoladi.",
  "На диске": "Diskda",
  "Важно": "Muhim",
  "Открыт для редактирования до {time} — пока он открыт, другие видят его только для чтения":
    "{time} gacha tahrirlash uchun ochiq — ochiq turganida boshqalar uni faqat o'qish uchun ko'radi",
  "открыт: {owner}": "ochiq: {owner}",
  "открыт для редактирования": "tahrirlash uchun ochiq",
  "Снять блокировку «{name}»? Это нужно, если Word или Excel завис и файл не освободился. Если файл ещё открыт, его правки могут не сохраниться.":
    "«{name}» qulfi olib tashlansinmi? Bu Word yoki Excel qotib qolib, fayl bo'shamaganda kerak. Fayl hali ochiq bo'lsa, undagi o'zgarishlar saqlanmasligi mumkin.",
  "Вход по логину и паролю OneBase":
    "OneBase login va paroli bilan kirish",
  "Без ссылки: Windows спросит ваш логин и пароль от OneBase. Права — ваши, проверяются при каждом обращении.":
    "Havolasiz: Windows OneBase login va parolingizni so'raydi. Huquqlar — sizniki, har bir murojaatda tekshiriladi.",
  "Мои файлы":
    "Mening fayllarim",
  "Разрешить вход Word и Excel ({host}).reg":
    "Word va Excelga kirishga ruxsat ({host}).reg",
  "Разрешить вход Word и Excel (.reg)":
    "Word va Excelga kirishga ruxsat (.reg)",
  "Microsoft 365 блокирует вход Word и Excel по паролю на любой сервер, кроме разрешённых. Файл добавляет {host} в разрешённые для вашей учётной записи Windows (политика basichostallowlist); для всех компьютеров сразу это делает администратор групповой политикой.":
    "Microsoft 365 Word va Excelning parol bilan kirishini ruxsat etilganlardan boshqa har qanday serverga bloklaydi. Fayl {host} ni Windows hisobingiz uchun ruxsat etilganlarga qo'shadi (basichostallowlist siyosati); barcha kompyuterlar uchun birdaniga buni administrator guruh siyosati orqali qiladi.",
};

export default dict;
