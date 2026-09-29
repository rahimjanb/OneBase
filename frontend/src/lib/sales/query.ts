import type { ScopeKind } from "./types";

export type SalesSearchParams = Record<string, string | string[] | undefined>;

const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

/** Параметры периода, которые переносятся между уровнями: year, month, plan. */
export function periodQuery(sp: SalesSearchParams): string {
  const q = new URLSearchParams();
  for (const key of ["year", "month", "plan"]) {
    const value = first(sp[key]);
    if (value) q.set(key, value);
  }
  return q.toString();
}

/** Запрос к API: период + дополнительные параметры страницы. */
export function apiQuery(sp: SalesSearchParams, extra: string[] = []): string {
  const q = new URLSearchParams(periodQuery(sp));
  for (const key of extra) {
    const value = first(sp[key]);
    if (value) q.set(key, value);
  }
  const s = q.toString();
  return s ? `?${s}` : "";
}

export const withQuery = (path: string, query: string) => (query ? `${path}?${query}` : path);

/** Охват страниц категории и артикула: откуда пришли (без параметров — республика). */
export const scopeKeys = ["direction", "region", "agent", "export"] as const;

/** Период + охват из адреса страницы категории или артикула — для ссылок на соседние уровни в том же охвате. */
export function scopedQuery(sp: SalesSearchParams): string {
  const q = new URLSearchParams(periodQuery(sp));
  for (const key of scopeKeys) {
    const value = first(sp[key]);
    if (value) q.set(key, value);
  }
  return q.toString();
}

/** «Все категории»: страница с карточками категорий в том же охвате. */
export function categoriesHref(kind: ScopeKind, sp: SalesSearchParams): string {
  const query = periodQuery(sp);
  switch (kind) {
    case "agent":
      return withQuery(`/sales/agents/${first(sp.agent)}`, query);
    case "export":
      return withQuery("/sales/export", query);
    case "region":
      return withQuery("/sales/assortment", queryWith(query, { region: first(sp.region) }));
    case "direction":
      return withQuery("/sales/assortment", queryWith(query, { direction: first(sp.direction) }));
    default:
      return withQuery("/sales/assortment", query);
  }
}

/** Строка адреса + параметры; пустые значения не пишутся. */
export function queryWith(query: string, params: Record<string, string | number | boolean | null | undefined>): string {
  const q = new URLSearchParams(query);
  for (const [key, value] of Object.entries(params)) {
    if (value == null || value === false || value === "") q.delete(key);
    else q.set(key, String(value));
  }
  return q.toString();
}

export const param = (sp: SalesSearchParams, key: string) => first(sp[key]);
