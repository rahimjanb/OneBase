"use client";

import Link from "next/link";
import { Factory, Megaphone, ShoppingCart, TrendingDown, TrendingUp, Truck, Users, Wallet, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import { fmt, signed, type MobileDepartment } from "@/lib/dashboard-mobile";
import { useHomeFilters } from "./Filters";
import m from "./mobile.module.css";
import { card, focusRing, press, SectionHeader } from "./ui";

const icons: Record<string, LucideIcon> = { finance: Wallet, sales: ShoppingCart, production: Factory, supply: Truck, hr: Users, marketing: Megaphone };

/**
 * «Отделы»: одна карточка со списком — ключевая метрика, изменение за неделю, полоса выполнения и статус словами (не только цветом).
 * Сначала отделы, которым нужно внимание, затем остальные; внутри — от слабого показателя к сильному. Тап — страница отдела.
 */
export function DepartmentsCard({ departments }: { departments: MobileDepartment[] }) {
  const t = useT();
  const [filters] = useHomeFilters();
  const scope = filters.departments.length ? departments.filter((d) => filters.departments.includes(d.code)) : departments;
  const rows = [...scope].sort((a, b) => (a.status === b.status ? a.value - b.value : a.status === "attention" ? -1 : 1));
  const attention = rows.filter((d) => d.status === "attention").length;
  const fine = rows.length - attention;

  return (
    <section className="flex flex-col gap-[12px]">
      <SectionHeader title={t("Отделы")} href="/departments" action={t("Все")}>
        {rows.length > 0 && (
          <span className="flex items-center gap-[10px] text-[12px] text-(--ts-muted)">
            {fine > 0 && (
              <span className="inline-flex items-center gap-[5px]">
                <span aria-hidden className="size-[6px] rounded-full bg-(--ts-success)" />
                {t("{n} в норме", { n: fine })}
              </span>
            )}
            {attention > 0 && (
              <span className="inline-flex items-center gap-[5px]">
                <span aria-hidden className="size-[6px] rounded-full bg-(--ts-warning)" />
                {t("{n} внимание", { n: attention })}
              </span>
            )}
          </span>
        )}
      </SectionHeader>

      {rows.length === 0 ? (
        <p className={`${card} px-[18px] py-[22px] text-center text-[14px] text-(--ts-muted)`}>{t("Нет данных по отделам")}</p>
      ) : (
        <ul className={`${card} overflow-hidden`}>
          {rows.map((d, i) => {
            const Icon = icons[d.code] ?? Factory;
            const warn = d.status === "attention";
            const Trend = d.trendWeek >= 0 ? TrendingUp : TrendingDown;
            return (
              <li key={d.code} className={i > 0 ? "border-t border-(--ts-border)" : ""}>
                <Link href={d.href} className={`block px-[16px] py-[14px] ${press} ${focusRing}`}>
                  <span className="flex items-center gap-[12px]">
                    <span
                      className={`grid size-[38px] shrink-0 place-items-center rounded-[11px] ${warn ? "bg-(--ts-warning-soft) text-(--ts-warning)" : "bg-(--ts-surface-2) text-(--ts-text-2)"}`}
                    >
                      <Icon aria-hidden className="size-[18px]" strokeWidth={2} />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-[15px] font-semibold text-(--ts-text)">{t(d.name)}</span>
                      <span className="block truncate text-[12px] text-(--ts-muted)">{t(d.metric)}</span>
                    </span>
                    <span className="flex shrink-0 flex-col items-end gap-[3px]">
                      <span className="text-[16px] font-bold leading-none text-(--ts-text)">{fmt(d.value)}%</span>
                      <span className={`inline-flex items-center gap-[3px] text-[11px] font-semibold ${d.trendWeek >= 0 ? "text-(--ts-success)" : "text-(--ts-danger)"}`}>
                        <Trend aria-hidden className="size-[12px]" strokeWidth={2.4} />
                        {t("{n} нед.", { n: signed(d.trendWeek) })}
                      </span>
                    </span>
                  </span>
                  <span className="mt-[10px] flex items-center gap-[12px]">
                    <span className="h-[6px] min-w-0 flex-1 overflow-hidden rounded-full bg-(--ts-surface-2)">
                      <span
                        className={`block h-full rounded-full ${m.fill} ${warn ? "bg-(--ts-warning)" : "bg-(--ts-success)"}`}
                        style={{ width: `${Math.min(100, d.value)}%`, animationDelay: `${i * 40}ms` }}
                      />
                    </span>
                    <span
                      className={`shrink-0 rounded-[6px] px-[7px] py-[3px] text-[11px] font-semibold ${warn ? "bg-(--ts-warning-soft) text-(--ts-warning)" : "bg-(--ts-success-soft) text-(--ts-success)"}`}
                    >
                      {warn ? t("Внимание") : t("В норме")}
                    </span>
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
