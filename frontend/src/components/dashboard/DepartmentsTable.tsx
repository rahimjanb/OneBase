import Link from "next/link";
import { ArrowRight, ChevronRight, Factory, Handshake, Landmark, Megaphone, Timer, Truck, Users, type LucideIcon } from "lucide-react";
import type { DepartmentRow, DepartmentStatusKey } from "@/lib/dashboard.mock";
import type { T } from "@/i18n/core";
import { CardHeader, DashCard, outlineButton, toneSoft, toneText } from "./blocks";

const icons: Record<DepartmentRow["icon"], LucideIcon> = { factory: Factory, landmark: Landmark, handshake: Handshake, megaphone: Megaphone, truck: Truck, users: Users };

const statusOf: Record<DepartmentStatusKey, { label: string; text: string; bg: string; bar: string; barSoft: string }> = {
  done: { label: "План выполнен", text: "text-ok", bg: "bg-ok/10", bar: "bg-ok", barSoft: "bg-ok/25" },
  ok: { label: "В норме", text: "text-ok", bg: "bg-ok/10", bar: "bg-ok", barSoft: "bg-ok/25" },
  attention: { label: "Требует внимания", text: "text-warn", bg: "bg-warn/10", bar: "bg-warn", barSoft: "bg-warn/25" },
};

const grid = "grid grid-cols-[260px_minmax(0,1fr)_120px_110px_160px_36px] items-center";

/**
 * «Состояние отделов»: таблица в карточке — отдел и руководитель, ключевая метрика с прогресс-баром, тренд 8 недель с дельтой,
 * задачи (и просроченные), статус. Строка ведёт в раздел отдела. На телефоне вместо таблицы — список карточек (таблица там видна бы
 * только наполовину).
 */
export function DepartmentsTable({ rows, t }: { rows: DepartmentRow[]; t: T }) {
  const fine = rows.filter((r) => r.status !== "attention").length;
  const attention = rows.length - fine;
  return (
    <DashCard className="overflow-hidden">
      <CardHeader
        className="px-5 pb-4 pt-5"
        title={t("Состояние отделов")}
        subtitle={t("Ключевые показатели и динамика команд за 30 дней")}
        action={
          <>
            <span className="hidden items-center gap-4 text-[12.5px] text-ink-3 sm:flex">
              <span className="inline-flex items-center gap-1.5">
                <span className="size-1.5 rounded-full bg-ok" /> {t("{n} в норме", { n: fine })}
              </span>
              <span className="inline-flex items-center gap-1.5">
                <span className="size-1.5 rounded-full bg-warn" /> {t("{n} требуют внимания", { n: attention })}
              </span>
            </span>
            <Link href="/departments" className={`${outlineButton} h-8 rounded-lg px-3 text-xs`}>
              {t("Все отделы")} <ArrowRight className="size-3.5" />
            </Link>
          </>
        }
      />
      <ul className="divide-y divide-line border-t border-line md:hidden">
        {rows.map((r) => {
          const Icon = icons[r.icon];
          const st = statusOf[r.status];
          const delta = r.trend[r.trend.length - 1] - r.trend[0];
          return (
            <li key={r.code}>
              <Link href={r.href} className="block px-4 py-3.5 transition-colors duration-150 hover:bg-muted/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent">
                <span className="flex items-center gap-3">
                  <span className={`grid size-[34px] shrink-0 place-items-center rounded-[9px] ${toneSoft[r.tone]} ${toneText[r.tone]}`}>
                    <Icon className="size-4" strokeWidth={1.75} />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-[13.5px] font-semibold text-ink">{t(r.name)}</span>
                    {r.manager && <span className="block truncate text-[11.5px] text-ink-4">{t("Рук. {name}", { name: r.manager })}</span>}
                  </span>
                  <span className={`inline-flex shrink-0 items-center gap-1.5 rounded-[20px] px-2 py-0.5 text-[11px] font-semibold ${st.bg} ${st.text}`}>
                    <span className={`size-1.5 rounded-full ${st.bar}`} />
                    {t(st.label)}
                  </span>
                </span>
                <span className="mt-2.5 flex items-center justify-between gap-3">
                  <span className="min-w-0 truncate text-[12.5px] text-ink-3">{t(r.metric)}</span>
                  <span className="shrink-0 text-[13px] font-semibold text-ink tabular-nums">{r.value}%</span>
                </span>
                <span className="mt-1.5 block h-1.5 overflow-hidden rounded-[3px] bg-muted">
                  <span className={`block h-full rounded-[3px] ${st.bar}`} style={{ width: `${Math.min(100, r.value)}%` }} />
                </span>
                <span className="mt-2 flex items-center gap-3 text-[11.5px] text-ink-3">
                  <span>
                    {t("Задачи")}: <span className="font-semibold text-ink tabular-nums">{r.tasks ?? "—"}</span>
                  </span>
                  {r.overdue > 0 && (
                    <span className="inline-flex items-center gap-1 rounded-[5px] bg-bad/10 px-1.5 py-0.5 text-[11px] font-semibold text-bad">
                      <Timer className="size-[11px]" strokeWidth={2} /> {r.overdue}
                    </span>
                  )}
                  <span className={`ml-auto font-semibold tabular-nums ${delta >= 0 ? "text-ok" : "text-bad"}`}>
                    {t("Тренд 8 нед.")} {delta >= 0 ? `+${delta}` : `−${Math.abs(delta)}`}
                  </span>
                </span>
              </Link>
            </li>
          );
        })}
      </ul>
      <div className="hidden overflow-x-auto md:block">
        <div className="min-w-[960px]">
          <div className={`${grid} h-9 border-y border-line bg-muted/60 px-5 text-[10.5px] font-semibold uppercase tracking-[0.6px] text-ink-4`}>
            <span>{t("Отдел")}</span>
            <span className="pr-12">{t("Ключевая метрика · выполнение")}</span>
            <span>{t("Тренд 8 нед.")}</span>
            <span>{t("Задачи")}</span>
            <span>{t("Статус")}</span>
            <span />
          </div>
          {rows.map((r, index) => {
            const Icon = icons[r.icon];
            const s = statusOf[r.status];
            const min = Math.min(...r.trend);
            const max = Math.max(...r.trend);
            const delta = r.trend[r.trend.length - 1] - r.trend[0];
            return (
              <Link
                key={r.code}
                href={r.href}
                className={`${grid} h-16 px-5 transition-colors duration-150 hover:bg-muted/60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent ${
                  index < rows.length - 1 ? "border-b border-line" : ""
                }`}
              >
                <span className="flex items-center gap-3">
                  <span className={`grid size-[34px] shrink-0 place-items-center rounded-[9px] ${toneSoft[r.tone]} ${toneText[r.tone]}`}>
                    <Icon className="size-4" strokeWidth={1.75} />
                  </span>
                  <span className="min-w-0">
                    <span className="block truncate text-[13.5px] font-semibold text-ink">{t(r.name)}</span>
                    {r.manager && <span className="block truncate text-[11.5px] text-ink-4">{t("Рук. {name}", { name: r.manager })}</span>}
                  </span>
                </span>
                <span className="pr-12">
                  <span className="flex items-center justify-between gap-3">
                    <span className="truncate text-[12.5px] text-ink-3">{t(r.metric)}</span>
                    <span className="text-[13px] font-semibold text-ink tabular-nums">{r.value}%</span>
                  </span>
                  <span className="mt-1.5 block h-1.5 overflow-hidden rounded-[3px] bg-muted">
                    <span className={`block h-full rounded-[3px] ${s.bar}`} style={{ width: `${Math.min(100, r.value)}%` }} />
                  </span>
                </span>
                <span className="flex items-center gap-2">
                  <span className="flex h-6 items-end gap-[3px]" aria-hidden>
                    {r.trend.map((v, i) => (
                      <span
                        key={i}
                        className={`w-1.5 rounded-[1.5px] ${i === r.trend.length - 1 ? s.bar : s.barSoft}`}
                        style={{ height: 6 + ((v - min) / (max - min || 1)) * 18 }}
                      />
                    ))}
                  </span>
                  <span className={`text-[11.5px] font-semibold tabular-nums ${delta >= 0 ? "text-ok" : "text-bad"}`}>{delta >= 0 ? `+${delta}` : `−${Math.abs(delta)}`}</span>
                </span>
                <span className="flex items-center gap-2">
                  <span className="text-[13px] font-semibold text-ink tabular-nums">{r.tasks ?? "—"}</span>
                  {r.overdue > 0 && (
                    <span className="inline-flex items-center gap-1 rounded-[5px] bg-bad/10 px-1.5 py-0.5 text-[11px] font-semibold text-bad">
                      <Timer className="size-[11px]" strokeWidth={2} /> {r.overdue}
                    </span>
                  )}
                </span>
                <span>
                  <span className={`inline-flex items-center gap-1.5 rounded-[20px] px-2.5 py-1 text-[11.5px] font-semibold ${s.bg} ${s.text}`}>
                    <span className={`size-1.5 rounded-full ${s.bar}`} />
                    {t(s.label)}
                  </span>
                </span>
                <ChevronRight className="size-4 justify-self-end text-ink-4" />
              </Link>
            );
          })}
        </div>
      </div>
    </DashCard>
  );
}
