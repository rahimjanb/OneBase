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

export const param = (sp: SalesSearchParams, key: string) => first(sp[key]);
