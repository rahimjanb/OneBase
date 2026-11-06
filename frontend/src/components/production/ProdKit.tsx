"use client";

import { useT } from "@/i18n/client";
import {
  productionOrderStatusLabel,
  productionOrderStatusTone,
  recipeStatusLabel,
  recipeStatusTone,
  type ProductionOrderStatus,
  type RecipeStatus,
} from "@/lib/production";

export const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 disabled:opacity-60";
export const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";
export const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";
export const secondary = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";

export const newKey = () => (typeof crypto !== "undefined" && "randomUUID" in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);

export function RecipeStatusPill({ status }: { status: RecipeStatus }) {
  const t = useT();
  return <span className={`inline-flex shrink-0 rounded-md px-2 py-0.5 text-xs font-semibold leading-5 ${recipeStatusTone[status]}`}>{t(recipeStatusLabel[status])}</span>;
}

export function OrderStatusPill({ status }: { status: ProductionOrderStatus }) {
  const t = useT();
  return (
    <span className={`inline-flex shrink-0 rounded-md px-2 py-0.5 text-xs font-semibold leading-5 ${productionOrderStatusTone[status]}`}>{t(productionOrderStatusLabel[status])}</span>
  );
}

export function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <div className="flex items-start justify-between gap-4 py-2 text-sm">
      <dt className="shrink-0 text-ink-3">{t(label)}</dt>
      <dd className="min-w-0 text-right text-ink">{children}</dd>
    </div>
  );
}

export function Label({ children, optional }: { children: React.ReactNode; optional?: boolean }) {
  const t = useT();
  return (
    <span className="mb-1.5 block text-sm font-medium text-ink">
      {children}
      {optional && <span className="font-normal text-ink-3"> {t("необязательно")}</span>}
    </span>
  );
}
