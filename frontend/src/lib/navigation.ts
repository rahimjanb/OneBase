/**
 * Какие разделы меню кому видны. Пункт показывается, если у пользователя есть хотя бы одно из прав пункта;
 * без списка прав — всем. Используется и в меню (Sidebar, MobileNav), и на страницах, чтобы не открывать скрытый раздел по ссылке.
 */

/** Общие разделы компании (Обзор, Отделы, Задачи, Отчёты): сотрудники отделов, директор, администратор, Sales Base. */
export const workspacePermissions = ["ai.agents.run", "sales.read", "finance.read", "hr.read", "field.use", "field.plan"];

export const navPermissions: Record<string, string[]> = {
  "/": workspacePermissions,
  "/departments": workspacePermissions,
  "/tasks": workspacePermissions,
  "/reports": workspacePermissions,
  "/base": ["files.read"],
  "/consultant": ["ai.agents.run"],
  "/data-manager": ["ai.data.manage"],
  "/campaigns": ["telegram.campaigns"],
};

/** Порядок разделов в меню — по нему выбирается стартовая страница, если «Обзор» недоступен. */
const order = ["/", "/departments", "/tasks", "/base", "/consultant", "/data-manager", "/campaigns"];

export function canOpen(href: string, permissions: readonly string[]): boolean {
  const need = navPermissions[href];
  return !need || need.some((p) => permissions.includes(p));
}

/** Первый доступный раздел — куда вести с главной у ролей без общих разделов (например, «Админ 2» → «Файлы»). */
export function homeFor(permissions: readonly string[]): string {
  return order.find((href) => canOpen(href, permissions)) ?? (permissions.length > 0 ? "/settings" : "/no-access");
}
