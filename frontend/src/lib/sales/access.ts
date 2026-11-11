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

/** Видна ли вкладка «Продаж» при этом охвате (вся организация или нет охвата — как раньше: решают права). */
export function salesTabAllowed(tab: string, scope: SalesScopeInfo | null | undefined): boolean {
  if (!scope || scope.level === "Organization") return true;
  return LIMITED[scope.level].includes(tab);
}

/** Адрес вкладки: «Вторичка» у РМ / СВР / ТП — своя страница (направление, регион, карточка ТП), остальное — как есть. */
export function salesTabHref(tab: string, href: string, scope: SalesScopeInfo | null | undefined): string {
  return tab === "analytics" && isLimited(scope) ? scope!.home : href;
}

/** Страница раздела открыта охвату? Своя страница и разрешённые вкладки — да; страницы страны — нет (их всё равно закроет сервер). */
export function salesPathAllowed(pathname: string, scope: SalesScopeInfo | null | undefined): boolean {
  if (!isLimited(scope)) return true;
  // Направление — уровень РМ, регион — РМ и СВР, карточка ТП и магазин — всем (чей именно — проверяет сервер).
  if (pathname.startsWith("/sales/directions/")) return scope!.level === "Rm";
  if (pathname.startsWith("/sales/regions/")) return scope!.level !== "Agent";
  if (pathname.startsWith("/sales/agents/") || pathname.startsWith("/sales/stores/")) return true;
  for (const [tab, prefix] of [["agents", "/sales/agents"], ["assortment", "/sales/assortment"], ["plans", "/sales/plans"], ["kpi", "/sales/kpi"], ["problems", "/sales/problems"]] as const) {
    if (pathname === prefix || pathname.startsWith(`${prefix}/`)) return salesTabAllowed(tab, scope);
  }
  return false;
}

/** Крошки страницы без ссылок, которые охвату не открыты (страна, чужой уровень): ссылка на «Нет доступа» не нужна. */
export function visibleCrumbs(crumbs: { label: string; href?: string }[], scope: SalesScopeInfo | null | undefined): { label: string; href?: string }[] {
  return crumbs.filter((c) => !c.href || salesPathAllowed(c.href.split("?")[0], scope));
}

/** «Назад»: если адрес охвату закрыт — своя страница раздела. */
export function visibleBack(href: string, scope: SalesScopeInfo | null | undefined): string {
  return salesPathAllowed(href.split("?")[0], scope) ? href : scope!.home;
}
