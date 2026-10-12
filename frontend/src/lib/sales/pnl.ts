// P&L дилеров: ответы /api/sales/pnl и форматирование значений (как во вкладке «P&L дилеров» Полевого контроля).

import { DASH, money, pct } from "./format";

export type PnlLine = { key: string; label: string; format: "money" | "pct" | "na"; minus: boolean; detail: boolean; input: boolean };
export type PnlValues = Record<string, number | null>;
export type PnlMonthRef = { year: number; month: number; hasSales: boolean; hasExpenses: boolean };
export type PnlDealer = { key: string; name: string; region: string | null; values: PnlValues };
export type PnlGroup = { id: string; name: string; manager: string | null; dealers: PnlDealer[]; total: PnlValues };
export type PnlSource = { changeSetId: string; title: string; fileName: string | null; appliedAt: string | null; appliedBy: string | null };

export type PnlView = {
  year: number | null;
  month: number | null;
  months: PnlMonthRef[];
  lines: PnlLine[];
  groups: PnlGroup[];
  total: PnlValues;
  noExpenses: boolean;
  loadedLines: string[];
  lastLoad: PnlSource | null;
};

export type PnlDealerCard = {
  key: string;
  name: string;
  region: string | null;
  year: number;
  lines: PnlLine[];
  months: { month: number; values: PnlValues }[];
  total: PnlValues;
  growthMonth: number | null;
  growthBase: string | null;
  growth: PnlValues | null;
};

/** Значение строки: деньги — сокращённо («12,19 млрд»), ноль — «—»; проценты — с одним знаком; строки без данных — «—». */
export function pnlText(line: PnlLine, value: number | null | undefined): string {
  if (line.format === "na" || value == null) return DASH;
  if (line.format === "pct") return pct(value, 1);
  return value === 0 ? DASH : money(value);
}

/** Темп роста: деньги — изменение в процентах, проценты — разница в п. п. */
export function growthText(line: PnlLine, value: number | null | undefined): string {
  if (value == null || line.format === "na") return DASH;
  const sign = value > 0 ? "+" : value < 0 ? "−" : "";
  const abs = Math.abs(value * 100).toLocaleString("ru-RU", { maximumFractionDigits: 1 });
  return line.format === "pct" ? `${sign}${abs} п. п.` : `${sign}${abs}%`;
}

/** Отрицательная прибыль — красным (расходы хранятся положительными и не краснеют). */
export const negative = (line: PnlLine, value: number | null | undefined) => !line.minus && line.format !== "na" && value != null && value < 0;

export const periodParam = (year: number, month: number) => `${year}-${String(month).padStart(2, "0")}`;
