/**
 * Какие разделы меню кому видны. Пункт показывается, если у пользователя есть хотя бы одно из прав пункта;
 * без списка прав — всем. Используется и в меню (Sidebar, MobileNav), и на страницах, чтобы не открывать скрытый раздел по ссылке.
 */

/** Общие разделы компании (Обзор, Отделы, Задачи, Отчёты): сотрудники отделов, директор, администратор, Sales Base. */
export const workspacePermissions = ["ai.agents.run", "sales.read", "finance.read", "hr.read", "work.tasks", "field.use", "field.plan"];

export const navPermissions: Record<string, string[]> = {
  "/": workspacePermissions,
  // «Отделы» — вход и в «Продажи», «Снабжение», HR: виден всем, кто может открыть хотя бы один из них.
  "/departments": [...workspacePermissions, "procurement.warehouse", "procurement.read", "procurement.manage", "integrations.manage", "hr.department", "hr.requests"],
  // Цифровой двойник — общий раздел компании; что видно в карточках, решают права разделов (backend).
  "/twin": workspacePermissions,
  // Задачи — раздел пользователя (галочка «Задачи» в «Пользователях»); что из задач видно, решает backend.
  "/tasks": ["work.tasks"],
  // Заявки на закупку: свои — у всех ролей отделов; все — снабжению и директору; что видно, решает backend.
  "/requests": ["procurement.request", "procurement.read", "procurement.manage", "procurement.settings", "procurement.warehouse"],
  // Склад снабжения: кладовщик, снабжение, директор; склад и партии смотрит и лаборатория.
  "/procurement": ["procurement.warehouse", "procurement.read", "procurement.manage", "quality.check"],
  // «Производство»: задания и рецептуры (производство, технолог), контроль качества (лаборатория), кладовщик, снабжение, директор.
  "/production": ["production.read", "production.manage", "production.recipes", "production.operate", "quality.check", "procurement.warehouse", "procurement.read", "procurement.manage"],
  // HR: право «HR», администратор интеграций или руководитель отдела с привязанными подразделениями Verifix (hr.department — не право,
  // а признак из /api/auth/me; доступ к данным всё равно проверяет backend).
  // Подбор: право «Подбор» или заявки на сотрудника (hr.requests — признак из /api/auth/me для руководителей отделов).
  "/hr": ["hr.read", "integrations.manage", "hr.department", "hr.requests", "hr.recruitment.read", "hr.recruitment.manage"],
  "/reports": workspacePermissions,
  "/base": ["files.read"],
  "/consultant": ["ai.agents.run"],
  "/data-manager": ["ai.data.manage"],
  "/campaigns": ["telegram.campaigns"],
};

/** Порядок разделов в меню — по нему выбирается стартовая страница, если «Обзор» недоступен. */
const order = ["/", "/twin", "/departments", "/tasks", "/hr", "/base", "/consultant", "/data-manager", "/campaigns", "/requests", "/procurement", "/production"];

export function canOpen(href: string, permissions: readonly string[]): boolean {
  const need = navPermissions[href];
  return !need || need.some((p) => permissions.includes(p));
}

/** Свой раздел отдела в OneBase; у остальных отделов рабочий раздел — их задачи. Производство — задания на выпуск, рецептуры, качество сырья. */
const departmentSections: Record<string, string> = { sales: "/sales", hr: "/hr", supply: "/procurement", production: "/production" };

/**
 * Куда ведёт кнопка отдела: свой раздел («Продажи», HR, «Снабжение»), иначе задачи отдела; нет доступа — следующий доступный, и только в крайнем случае —
 * файлы отдела в «Общей базе».
 */
export function departmentHref(code: string, permissions: readonly string[]): string {
  const own = departmentSections[code];
  if (own && canOpen(own, permissions)) return own;
  if (canOpen("/tasks", permissions)) return `/tasks/${code}`;
  return canOpen("/base", permissions) ? `/base/${code}` : (own ?? "/departments");
}

/** Первый доступный раздел — куда вести с главной у ролей без общих разделов (например, «Админ 2» → «Файлы»). */
export function homeFor(permissions: readonly string[]): string {
  return order.find((href) => canOpen(href, permissions)) ?? (permissions.length > 0 ? "/settings" : "/no-access");
}
