import { Factory, Handshake, Landmark, Megaphone, Truck, Users, type LucideIcon } from "lucide-react";

/** «Настройки → Отделы» (GET /api/settings/departments): отделы, руководители, маршрут заявок снабжения (GET /api/procurement/settings). */

export type DepartmentPerson = { id: string; name: string; position: string | null; department: string | null };

export type DepartmentCard = { code: string; name: string; users: number; heads: DepartmentPerson[] };

export type DepartmentsView = { departments: DepartmentCard[]; canEditHeads: boolean; canEditSupply: boolean };

/** Настройки снабжения: маршрут согласования заявок (финансист, директор, лимит без директора, сум), допуск и эскалация. */
export type SupplySettings = {
  reapprovalTolerancePercent: number;
  escalateHeadDays: number;
  escalateDirectorDays: number;
  financeApproverId: string | null;
  directorApproverId: string | null;
  directorLimit: number;
  financeApproverName: string | null;
  directorApproverName: string | null;
};

export const departmentIcons: Record<string, LucideIcon> = { sales: Handshake, production: Factory, supply: Truck, finance: Landmark, marketing: Megaphone, hr: Users };

/** Что настраивается в отделе — подпись карточки и страницы; продажи настраиваются в своём разделе. */
export const departmentAbout: Record<string, string> = {
  supply: "Маршрут согласования заявок на закупку: руководитель отдела, финансист, директор и лимит покупки без директора.",
  sales: "Оргструктура продаж, СВР, дилеры и цели — в разделе «Продажи: оргструктура и цели».",
  production: "Руководители отдела: согласуют заявки сотрудников и управляют задачами отдела.",
  finance: "Руководители отдела: согласуют заявки сотрудников и управляют задачами отдела.",
  marketing: "Руководители отдела: согласуют заявки сотрудников и управляют задачами отдела.",
  hr: "Руководители отдела: согласуют заявки сотрудников и управляют задачами отдела.",
};

/** «5 000 000» → 5000000; не число — null. */
export const parseSum = (text: string): number | null => {
  const digits = text.replace(/[\s  ]/g, "");
  return /^\d{1,13}$/.test(digits) ? Number(digits) : null;
};

/** 5000000 → «5 000 000» (неразрывные пробелы: число не переносится посередине). */
export const groupSum = (value: number) => String(Math.trunc(value)).replace(/\B(?=(\d{3})+(?!\d))/g, "\u00a0");
