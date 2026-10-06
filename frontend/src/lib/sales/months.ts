// Месяцы «ГГГГ-ММ» в адресе («Продажи по SKU»): разбор и сравнение. Обычный модуль без «use client» — его одинаково берут
// серверная страница и клиентские компоненты (функции из «use client»-модуля на сервере вызывать нельзя).

/** «2026-03» → номер месяца для сравнения (год × 12 + месяц); null — не месяц. */
export function monthIndex(value: string | null | undefined): number | null {
  const match = /^(\d{4})-(\d{1,2})$/.exec(value?.trim() ?? "");
  if (!match) return null;
  const month = Number(match[2]);
  return month >= 1 && month <= 12 ? Number(match[1]) * 12 + month : null;
}

/** (2026, 3) → «2026-03». */
export const ym = (year: number, month: number) => `${year}-${String(month).padStart(2, "0")}`;
