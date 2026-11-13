// «Продажи · Ассортимент · Категория» — чистые функции экрана: название без кода Linko, порядок артикулов, поиск.
// Тест: node --test src/lib/sales/assortment-view.test.mjs.
import type { SkuRow } from "./types";

/** Название без кода в начале: в Linko «002 Кукурузные палочки…», «518-Kекс…» — код и так стоит рядом («Артикул 002»). */
export function skuName(r: Pick<SkuRow, "name" | "code">): string {
  const code = r.code?.trim();
  if (!code || !r.name.startsWith(code)) return r.name;
  const rest = r.name.slice(code.length).replace(/^[\s\-–—.]+/, "");
  return rest || r.name;
}

/** Порядок по умолчанию: по факту, кг по убыванию; «Пропал» — в конце. */
export function sortSkus<R extends Pick<SkuRow, "status" | "factKg" | "name">>(rows: R[]): R[] {
  return [...rows].sort((a, b) => Number(a.status === "lost") - Number(b.status === "lost") || b.factKg - a.factKg || a.name.localeCompare(b.name, "ru"));
}

/** Поиск по названию и артикулу, без учёта регистра. */
export function matchesSku(r: Pick<SkuRow, "name" | "code">, q: string): boolean {
  const s = q.trim().toLocaleLowerCase("ru");
  return !s || r.name.toLocaleLowerCase("ru").includes(s) || (r.code ?? "").toLocaleLowerCase("ru").includes(s);
}
