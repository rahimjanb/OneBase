import type { Dict } from "../core";

/**
 * «Продажи → ТП → День»: куда ходил, что продал и что нет, минуты между отметками — app/(app)/sales/agents/[id]/day, lib/sales/agent-day.ts;
 * кнопка на странице ТП; замечания сервера (AgentDayService).
 */
const dict: Dict = {
  "День ТП": "TP kuni",
  "День ТП: визиты и продажи": "TP kuni: tashriflar va savdo",
  "Маршрут дня": "Kun marshruti",
  "Точки по порядку отметок визитов; затем пропущенные и предстоящие точки маршрута и заказы без визита":
    "Nuqtalar tashrif belgilari tartibida; so'ng o'tkazib yuborilgan va oldindagi marshrut nuqtalari hamda tashrifsiz buyurtmalar",
  "Не посетил": "Bormadi",
  "не посетил": "bormadi",
  "точек маршрута без визита": "tashrifsiz marshrut nuqtalari",
  "ещё впереди: {n}": "hali oldinda: {n}",
  "Визиты с заказом ÷ выполненные визиты (заказ этого ТП в этой точке в этот день)":
    "Buyurtmali tashriflar ÷ bajarilgan tashriflar (shu TP shu nuqtada shu kuni buyurtma)",
  "визитов с заказом: {n}": "buyurtmali tashriflar: {n}",
  "Продал": "Sotdi",
  "{kg} кг · заказов {n}": "{kg} kg · buyurtmalar {n}",
  "Отметки визитов": "Tashrif belgilari",
  "от первой до последней — {span}": "birinchisidan oxirgisigacha — {span}",
  "отметок меньше двух": "belgilar ikkitadan kam",
  "Между отметками": "Belgilar orasida",
  "Медиана минут между соседними отметками: время на точке вместе с дорогой": "Qo'shni belgilar orasidagi daqiqalar medianasi: nuqtadagi vaqt yo'l bilan birga",
  "дольше всего {n} — перед «{to}»": "eng uzog'i {n} — «{to}» oldidan",
  "медиана": "mediana",
  "Заказы без визита": "Tashrifsiz buyurtmalar",
  "по телефону или без отметки визита": "telefon orqali yoki tashrif belgisisiz",
  "Данные Linko": "Linko ma'lumotlari",
  "визиты загружены": "tashriflar yuklangan",
  "по маршруту {done} из {planned} · вне маршрута {off}": "marshrut bo'yicha {planned} tadan {done} · marshrutdan tashqari {off}",
  "визит": "tashrif",
  "заказ без визита": "tashrifsiz buyurtma",
  "на карте": "xaritada",
  "без заказа": "buyurtmasiz",
  "с прошлой отметки: {n}": "oldingi belgidan: {n}",
  "{n}-й визит после прошлого заказа точки": "nuqtaning oldingi buyurtmasidan keyingi {n}-tashrif",
  "последний заказ точки: {date}": "nuqtaning oxirgi buyurtmasi: {date}",
  "заказов за 120 дней не было": "120 kunda buyurtma bo'lmagan",
  "Обычно берёт: {list}": "Odatda oladi: {list}",
  "Из обычного не взяли: {list}": "Odatdagilardan olinmadi: {list}",
  "Что продал: {n}": "Nima sotdi: {n}",
  "Время на точке Linko не передаёт: у визита одна отметка — момент закрытия визита или оформления заказа. «С прошлой отметки» — время на прошлой точке вместе с дорогой.":
    "Linko nuqtadagi vaqtni bermaydi: tashrifda bitta belgi bor — tashrif yopilgan yoki buyurtma rasmiylashtirilgan payt. «Oldingi belgidan» — oldingi nuqtadagi vaqt yo'l bilan birga.",
  "День ещё идёт: точки маршрута без визита — «впереди», данные Linko — на момент последней загрузки.":
    "Kun hali davom etmoqda: tashrifsiz marshrut nuqtalari — «oldinda», Linko ma'lumotlari — oxirgi yuklash holatida.",
  "В этот день у ТП нет ни визитов, ни заказов в Linko.": "Bu kuni TPda Linkoda na tashrif, na buyurtma bor.",
};

export default dict;
