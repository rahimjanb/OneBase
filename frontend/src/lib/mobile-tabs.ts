// Нижняя панель на телефоне: что стоит на месте «Продаж», если прав на продажи нет. Тест: node --test src/lib/mobile-tabs.test.mjs.

/** Раздел роли вместо «Продаж»: HR — «Подбор», технолог — «Технолог», снабженец — «Снабжение». */
export type RoleTab = "recruitment" | "technologist" | "procurement";

/**
 * Вкладка роли: есть права на продажи — null (остаются «Продажи»); иначе первый подходящий раздел в порядке HR → технолог → снабженец
 * (право «Подбор», «Производство: рецептуры», «Снабжение: обработка заявок»); ни одного — null (место займёт раздел из веера, как раньше).
 */
export function roleTab(permissions: readonly string[]): RoleTab | null {
  if (permissions.includes("sales.read")) return null;
  if (permissions.includes("hr.recruitment.read") || permissions.includes("hr.recruitment.manage")) return "recruitment";
  if (permissions.includes("production.recipes")) return "technologist";
  if (permissions.includes("procurement.manage")) return "procurement";
  return null;
}
