import { Ellipsis } from "lucide-react";
import type { TaskStatusSlice, Tone } from "@/lib/dashboard.mock";
import type { T } from "@/i18n/core";
import { CardHeader, DashCard, toneBg } from "./blocks";

const stroke: Record<Tone, string> = {
  accent: "stroke-accent",
  ok: "stroke-ok",
  warn: "stroke-warn",
  bad: "stroke-bad",
  violet: "stroke-violet",
  pink: "stroke-pink",
  cyan: "stroke-cyan",
  muted: "stroke-ink-3",
};

const SIZE = 180;
const R = 70; // внутренний радиус 78 % от внешнего 90: кольцо толщиной 20
const WIDTH = 20;
const C = 2 * Math.PI * R;
const GAP = (2 / 360) * C; // зазор между сегментами ~2°

/** «Задачи по статусу»: кольцо (SVG) с зазорами между сегментами, доля выполненных в центре и легенда с числами и процентами. */
export function TasksDonut({ total, done, slices, t }: { total: number; done: number; slices: TaskStatusSlice[]; t: T }) {
  const sum = slices.reduce((a, s) => a + s.count, 0) || 1;
  let offset = 0;
  const arcs = slices.map((s) => {
    const length = (s.count / sum) * C;
    const arc = { ...s, start: offset, length: Math.max(0, length - GAP) };
    offset += length;
    return arc;
  });
  const donePct = Math.round((done / Math.max(1, total)) * 100);

  return (
    <DashCard className="flex flex-col gap-5 p-5 xl:w-[380px]">
      <CardHeader
        title={t("Задачи по статусу")}
        subtitle={t("Всего {n} за 30 дней", { n: total })}
        action={
          <button type="button" aria-label={t("Действия")} className="grid size-7 place-items-center rounded-md text-ink-3 transition-colors duration-150 hover:bg-muted hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent">
            <Ellipsis className="size-4" />
          </button>
        }
      />
      <div className="relative mx-auto" style={{ width: SIZE, height: SIZE }}>
        <svg viewBox={`0 0 ${SIZE} ${SIZE}`} width={SIZE} height={SIZE} role="img" aria-label={t("Доля выполненных задач: {n}%", { n: donePct })}>
          <circle cx={SIZE / 2} cy={SIZE / 2} r={R} fill="none" strokeWidth={WIDTH} className="stroke-muted" />
          {arcs.map((a) => (
            <circle
              key={a.key}
              cx={SIZE / 2}
              cy={SIZE / 2}
              r={R}
              fill="none"
              strokeWidth={WIDTH}
              className={stroke[a.tone]}
              strokeDasharray={`${a.length} ${C - a.length}`}
              strokeDashoffset={-a.start}
              transform={`rotate(-90 ${SIZE / 2} ${SIZE / 2})`}
            />
          ))}
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <span className="text-[30px] font-semibold leading-none text-ink tabular-nums">{donePct}%</span>
          <span className="mt-1 text-xs text-ink-4">{t("выполнено")}</span>
        </div>
      </div>
      <ul className="flex flex-col gap-3">
        {slices.map((s) => (
          <li key={s.key} className="flex items-center gap-2.5 text-[13px]">
            <span className={`size-2.5 rounded-[3px] ${toneBg[s.tone]}`} aria-hidden />
            <span className="flex-1 text-ink-2">{t(s.label)}</span>
            <span className="font-semibold text-ink tabular-nums">{s.count}</span>
            <span className="w-9 text-right text-xs text-ink-4 tabular-nums">{Math.round((s.count / sum) * 100)}%</span>
          </li>
        ))}
      </ul>
    </DashCard>
  );
}
