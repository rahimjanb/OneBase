// Охват «Продаж» в интерфейсе (уровень и своя страница — из /api/auth/me): какие вкладки раздела видны и куда ведёт «Вторичка». Только
// интерфейс — каждый запрос всё равно проверяет сервер (чужой регион, ТП, магазин — 404, страницы страны — 403 → «Нет доступа»).
// Тест: node --test src/lib/sales-access.test.mjs.

export type SalesScopeLevel = "Organization" | "Rm" | "Supervisor" | "Agent";
export type SalesScopeInfo = { level: SalesScopeLevel; home: string };

/** Вкладки, открытые своей части оргструктуры: вторичка (своя страница), агенты, ассортимент (не ТП), планы, KPI, проблемные агенты. */
const LIMITED: Record<Exclude<SalesScopeLevel, "Organization">, readonly string[]> = {
  Rm: ["analytics", "agents", "assortment", "plans", "kpi", "problems"],
  Supervisor: ["analytics", "agents", "assortment", "plans", "kpi", "problems"],
  Agent: ["analytics", "agents", "plans", "kpi", "problems"],
};

export const isLimited = (scope: SalesScopeInfo | null | undefined): boolean => !!scope && scope.level !== "Organization";

/** «Мои регионы» — своя страница РМ и СВР с несколькими регионами (сервер отдаёт её в home). */
export const MY_REGIONS = "/sales/my";
const hasMyRegions = (scope: SalesScopeInfo | null | undefined): boolean => isLimited(scope) && scope!.home === MY_REGIONS;
const pathOf = (href: string): string => href.split("?")[0];
const queryOf = (href: string): string => href.slice(pathOf(href).length);

/** Видна ли вкладка «Продаж» при этом охвате (вся организация или нет охвата — как раньше: решают права). */
export function salesTabAllowed(tab: string, scope: SalesScopeInfo | null | undefined): boolean {
  if (!scope || scope.level === "Organization") return true;
  return LIMITED[scope.level].includes(tab);
}

/** Адрес вкладки: «Вторичка» у РМ / СВР / ТП — своя страница («Мои регионы», направление, регион, карточка ТП), остальное — как есть. */
export function salesTabHref(tab: string, href: string, scope: SalesScopeInfo | null | undefined): string {
  return tab === "analytics" && isLimited(scope) ? scope!.home : href;
}

/** Страница раздела открыта охвату? Своя страница и разрешённые вкладки — да; страницы страны — нет (их всё равно закроет сервер). */
export function salesPathAllowed(pathname: string, scope: SalesScopeInfo | null | undefined): boolean {
  if (!isLimited(scope)) return true;
  // «Мои регионы» и направление — уровень РМ (мои регионы — и СВР), регион — РМ и СВР, карточка ТП и магазин — всем (чей именно — проверяет
  // сервер).
  if (pathname === MY_REGIONS) return scope!.level === "Rm" || scope!.level === "Supervisor";
  if (pathname.startsWith("/sales/directions/")) return scope!.level === "Rm";
  if (pathname.startsWith("/sales/regions/")) return scope!.level !== "Agent";
  if (pathname.startsWith("/sales/agents/") || pathname.startsWith("/sales/stores/")) return true;
  for (const [tab, prefix] of [["agents", "/sales/agents"], ["assortment", "/sales/assortment"], ["plans", "/sales/plans"], ["kpi", "/sales/kpi"], ["problems", "/sales/problems"]] as const) {
    if (pathname === prefix || pathname.startsWith(`${prefix}/`)) return salesTabAllowed(tab, scope);
  }
  return false;
}

/**
 * Крошки страницы без ссылок, которые охвату не открыты (страна, чужой уровень): ссылка на «Нет доступа» не нужна. mine — подпись
 * «Мои регионы»: у РМ и СВР с несколькими регионами крошка страны заменяется ею (с тем же периодом).
 */
export function visibleCrumbs(
  crumbs: { label: string; href?: string }[],
  scope: SalesScopeInfo | null | undefined,
  mine?: string,
): { label: string; href?: string }[] {
  return crumbs
    .map((c) => (mine && c.href && hasMyRegions(scope) && pathOf(c.href) === "/sales/republic" ? { label: mine, href: MY_REGIONS + queryOf(c.href) } : c))
    .filter((c) => !c.href || salesPathAllowed(pathOf(c.href), scope));
}

/** «Назад»: если адрес охвату закрыт — своя страница раздела, с тем же периодом. */
export function visibleBack(href: string, scope: SalesScopeInfo | null | undefined): string {
  return salesPathAllowed(pathOf(href), scope) ? href : scope!.home + queryOf(href);
}
