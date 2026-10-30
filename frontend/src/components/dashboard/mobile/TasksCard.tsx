"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { AlarmClock } from "lucide-react";
import { startNavProgress } from "@/components/shell/NavProgress";
import { useT } from "@/i18n/client";
import type { Tone } from "@/lib/dashboard.mock";
import { fmt, type MobileTasks } from "@/lib/dashboard-mobile";
import { plural } from "@/lib/format";
import { useHomeFilters } from "./Filters";
import m from "./mobile.module.css";
import { card, focusRing, press, SectionHeader } from "./ui";

const SIZE = 120;
const WIDTH = 13;
const R = (SIZE - WIDTH) / 2;
const C = 2 * Math.PI * R;
const GAP = (2 / 360) * C;

const color: Partial<Record<Tone, string>> = { ok: "var(--ts-success)", accent: "var(--ts-accent)", violet: "var(--ts-violet)", bad: "var(--ts-danger)" };
const square: Partial<Record<Tone, string>> = { ok: "bg-(--ts-success)", accent: "bg-(--ts-accent)", violet: "bg-(--ts-violet)", bad: "bg-(--ts-danger)" };
/** Список задач с этим статусом (раздел «Задачи → Все»). */
const statusHref: Record<string, string> = {
  done: "/tasks/all?status=Done",
  progress: "/tasks/all?status=New,InProgress",
  review: "/tasks/all?status=Review",
  overdue: "/tasks/all?status=overdue",
};

/**
 * «Задачи» (раздел «Задачи», доступные пользователю): кольцо — выполненные за месяц, в работе и на проверке (зазор 2°, старт сверху по
 * часовой), доля выполненных в центре и легенда с числами; просроченные — часть открытых, отдельной строкой. Тап по сегменту или строке —
 * список задач с этим статусом. Есть просрочки — плашка с отделом, где их больше всего (из выбранных в фильтре). Нет доступа — блока нет.
 */
export function TasksCard({ tasks }: { tasks: MobileTasks | null }) {
  const t = useT();
  const router = useRouter();
  const [filters] = useHomeFilters();
  if (!tasks) return null;
  const sum = tasks.slices.reduce((a, s) => a + s.count, 0);
  const donePct = Math.round((tasks.done / Math.max(1, tasks.total)) * 100);
  const overdue = tasks.overdue;
  const scope = filters.departments.length ? tasks.departments.filter((d) => filters.departments.includes(d.code)) : tasks.departments;
  const top = scope.reduce<MobileTasks["departments"][number] | null>((best, d) => (d.overdue > (best?.overdue ?? 0) ? d : best), null);

  let offset = 0;
  const arcs = tasks.slices.map((s) => {
    const length = sum ? (s.count / sum) * C : 0;
    const arc = { ...s, start: offset, length: Math.max(0, length - (length > GAP ? GAP : 0)) };
    offset += length;
    return arc;
  });

  const open = (href: string) => {
    startNavProgress();
    router.push(href);
  };

  const described = [...tasks.slices.map((s) => `${t(s.label).toLowerCase()} ${s.count}`), `${t("Просрочено").toLowerCase()} ${overdue}`].join(", ");

  return (
    <section className="flex flex-col gap-[12px]">
      <SectionHeader title={t("Задачи")} href="/tasks/all" action={t("Все {n}", { n: fmt(tasks.total) })} />
      {sum === 0 && overdue === 0 ? (
        <p className={`${card} px-[18px] py-[22px] text-center text-[14px] text-(--ts-muted)`}>{t("Нет задач за период")}</p>
      ) : (
        <div className={`${card} p-[18px]`}>
          <div className="flex items-center gap-[20px]">
            <div className="relative shrink-0" style={{ width: SIZE, height: SIZE }}>
              <svg viewBox={`0 0 ${SIZE} ${SIZE}`} width={SIZE} height={SIZE} role="img" aria-label={t("Задачи: {list}", { list: described })}>
                <circle cx={SIZE / 2} cy={SIZE / 2} r={R} fill="none" strokeWidth={WIDTH} stroke="var(--mh-track)" />
                {arcs.map((a) => (
                  <circle
                    key={a.key}
                    cx={SIZE / 2}
                    cy={SIZE / 2}
                    r={R}
                    fill="none"
                    strokeWidth={WIDTH}
                    stroke={color[a.tone] ?? "var(--ts-muted)"}
                    strokeDasharray={`${a.length} ${C - a.length}`}
                    strokeDashoffset={-a.start}
                    transform={`rotate(-90 ${SIZE / 2} ${SIZE / 2})`}
                    className={`cursor-pointer ${m.arc}`}
                    style={{ "--c": C } as React.CSSProperties}
                    onClick={() => statusHref[a.key] && open(statusHref[a.key])}
                  />
                ))}
              </svg>
              <span aria-hidden className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
                <span className="text-[26px] font-bold leading-none tracking-[-0.5px] text-(--ts-text)">{donePct}%</span>
                <span className="mt-[4px] text-[11px] text-(--ts-muted)">{t("выполнено")}</span>
              </span>
            </div>
            <ul className="flex min-w-0 flex-1 flex-col">
              {tasks.slices.map((s) => (
                <li key={s.key}>
                  <Link href={statusHref[s.key] ?? "/tasks/all"} className={`flex min-h-[30px] items-center gap-[8px] rounded-[8px] text-[13px] ${press} ${focusRing}`}>
                    <span aria-hidden className={`size-[8px] shrink-0 rounded-[2px] ${square[s.tone] ?? "bg-(--ts-muted)"}`} />
                    <span className="min-w-0 flex-1 truncate text-(--ts-text-2)">{t(s.label)}</span>
                    <span className="font-bold text-(--ts-text)">{fmt(s.count)}</span>
                  </Link>
                </li>
              ))}
              <li>
                <Link href={statusHref.overdue} className={`flex min-h-[30px] items-center gap-[8px] rounded-[8px] text-[13px] ${press} ${focusRing}`}>
                  <AlarmClock aria-hidden className="size-[11px] shrink-0 text-(--ts-danger)" strokeWidth={2.4} />
                  <span className="min-w-0 flex-1 truncate text-(--ts-text-2)">{t("Просрочено")}</span>
                  <span className={`font-bold ${overdue > 0 ? "text-(--ts-danger)" : "text-(--ts-text)"}`}>{fmt(overdue)}</span>
                </Link>
              </li>
            </ul>
          </div>

          {overdue > 0 && (
            <Link
              href={statusHref.overdue}
              className={`mt-[16px] flex items-center gap-[10px] rounded-[12px] bg-(--ts-danger-soft) px-[14px] py-[11px] ${press} ${focusRing}`}
            >
              <AlarmClock aria-hidden className="size-[18px] shrink-0 text-(--ts-danger)" strokeWidth={2} />
              <span className="min-w-0 flex-1 text-[13px] leading-[1.35] text-(--ts-text)">
                {overdue} {t(plural(overdue, ["задача просрочена", "задачи просрочены", "задач просрочено"]))}
                {top && ` — ${t("{n} в отделе «{name}»", { n: top.overdue, name: top.name })}`}
              </span>
              <span className="shrink-0 text-[13px] font-semibold text-(--ts-danger)">{t("Открыть")}</span>
            </Link>
          )}
        </div>
      )}
    </section>
  );
}
