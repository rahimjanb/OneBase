import type { Dict } from "../core";

/** «Технолог → Техкарты»: список с фото продукта (сетка, список, телефон), блок «Фото продукта» в карточке и форме; тексты сервера о фото. */
const dict: Dict = {
  // Шапка, поиск, фильтры, вид и сортировка
  "Производство · Технолог": "Ishlab chiqarish · Texnolog",
  "Код, название или номер карты": "Kod, nom yoki karta raqami",
  "Статус техкарты": "Texkarta holati",
  "Вид списка": "Ro'yxat ko'rinishi",
  "Сетка": "To'r",
  "Список": "Ro'yxat",
  "Сортировка": "Saralash",
  "Сначала новые": "Avval yangilar",
  "По названию": "Nomi bo'yicha",
  "По коду": "Kod bo'yicha",
  "техкарта": "texkarta",
  "техкарты": "texkarta",
  "техкарт": "texkarta",

  // Карточка
  "Выход: нормы на {q}": "Chiqish: me'yorlar {q} uchun",
  "на {q}": "{q} uchun",
  "{n} комп.": "{n} komp.",
  "Этапы": "Bosqichlar",
  "Компоненты": "Komponentlar",
  "Действует": "Amal qiladi",
  "с {date}": "{date} dan",

  // Пустые состояния
  "Техкарт пока нет": "Hozircha texkartalar yo'q",
  "Ничего не найдено": "Hech narsa topilmadi",
  "Измените поиск или состояние техкарты.": "Qidiruvni yoki texkarta holatini o'zgartiring.",
  "Сбросить фильтры": "Filtrlarni tozalash",

  // Фото продукта
  "Фото продукта": "Mahsulot fotosi",
  "Фото не добавлено": "Foto qo'shilmagan",
  "Загрузить фото": "Fotoni yuklash",
  "Загрузить фото для «{name}»": "«{name}» uchun fotoni yuklash",
  "Добавить фото": "Foto qo'shish",
  "Загружаем…": "Yuklanmoqda…",
  "Загрузка фото": "Foto yuklanmoqda",
  "Фото обновлено": "Foto yangilandi",
  "Фото удалено": "Foto o'chirildi",
  "Не удалось загрузить фото": "Fotoni yuklab bo'lmadi",
  "Фото не загрузилось": "Foto yuklanmadi",
  "Фото не загрузилось ({status})": "Foto yuklanmadi ({status})",
  "Нет связи с сервером OneBase — фото не загрузилось. Проверьте связь и попробуйте ещё раз.":
    "OneBase serveri bilan aloqa yo'q — foto yuklanmadi. Aloqani tekshiring va qayta urinib ko'ring.",
  "или перетащите файл сюда · JPEG, PNG, WebP, HEIC до 10 МБ": "yoki faylni shu yerga torting · JPEG, PNG, WebP, HEIC 10 MB gacha",
  "Отпустите, чтобы загрузить": "Yuklash uchun qo'yib yuboring",
  "Камера": "Kamera",
  "Галерея": "Galereya",
  "Заменить": "Almashtirish",
  "Удалить фото?": "Foto o'chirilsinmi?",
  "Удалить фото у всех версий?": "Foto barcha versiyalardan o'chirilsinmi?",
  "Фото сохранится вместе с техкартой.": "Foto texkarta bilan birga saqlanadi.",
  "Одно фото на продукт — у всех версий техкарты. Кадр 4:3 вырезается по центру.":
    "Har bir mahsulotga bitta foto — texkartaning barcha versiyalarida. 4:3 kadr markazdan kesib olinadi.",
  "Техкарта создана, но фото не загрузилось: {error}": "Texkarta yaratildi, lekin foto yuklanmadi: {error}",

  // Тексты сервера: проверки, ошибки, история версии
  "Файл пустой — выберите фото.": "Fayl bo'sh — fotoni tanlang.",
  "Фото — до 10 МБ.": "Foto — 10 MB gacha.",
  "Фото — JPEG, PNG, WebP или HEIC.": "Foto — JPEG, PNG, WebP yoki HEIC.",
  "Фото техкарты меняет технолог — нужно право «Производство: рецептуры».":
    "Texkarta fotosini texnolog o'zgartiradi — «Ishlab chiqarish: retsepturalar» huquqi kerak.",
  "Не удалось прочитать фото. Сохраните его как JPEG или PNG и загрузите снова.":
    "Fotoni o'qib bo'lmadi. Uni JPEG yoki PNG sifatida saqlang va qayta yuklang.",
  "Фото этой техкарты как раз меняют в другом окне — обновите страницу.": "Bu texkarta fotosi hozir boshqa oynada o'zgartirilmoqda — sahifani yangilang.",
  "Такого размера фото нет.": "Bunday o'lchamdagi foto yo'q.",
  "Добавлено фото продукта": "Mahsulot fotosi qo'shildi",
  "Фото продукта заменено": "Mahsulot fotosi almashtirildi",
  "Фото продукта удалено": "Mahsulot fotosi o'chirildi",
};

export default dict;
