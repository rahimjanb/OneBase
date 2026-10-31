import Link from "next/link";
import { AlarmClock, CalendarDays, ChevronRight, Circle, CircleCheck, Hourglass, Layers, Loader, SquareCheck, TrendingUp, type LucideIcon } from "lucide-react";
import type { T } from "@/i18n/core";
import { barHeights, duePill, taskStatus, weekDelta, type MyTasks, type Tone } from "@/lib/my-day";
import { BlockError } from "./BlockError";
import { BlockHeader, Empty, card, focusRing, toneDot, toneSoft, toneText } from "./parts";

type Tile = { key: string; label: string; value: number; tone: Tone | "total"; icon: LucideIcon; suffix?: string };

/**
 * «Мои задачи»: плитки (всего, в процессе, ожидают — на проверке или заблокированы, просрочены, выполнены за месяц) и до 5 ближайших
 * сроков — просроченные первыми. Строка ведёт в карточку задачи. На телефоне — 4 плитки 2×2 («Выполнены 5 / 12») и 3 срока.
 */
export async function TasksBlock({ data, today, locale, t }: { data: Promise<MyTasks | null>; today: string; locale: string; t: T }) {
  const tasks = await data;
  if (!tasks) return <BlockError title="Мои задачи" />;
  if (!tasks.available) return null;

  const tiles: Tile[] = [
    { key: "total", label: t("Всего"), value: tasks.total, tone: "total", icon: Layers },
    { key: "progress", label: t("В процессе"), value: tasks.inProgress, tone: "warning", icon: Loader },
    { key: "waiting", label: t("Ожидают"), value: tasks.waiting, tone: "accent", icon: Hourglass },
    { key: "overdue", label: t("Просрочены"), value: tasks.overdue, tone: "danger", icon: AlarmClock },
    { key: "done", label: t("Выполнены"), value: tasks.done, tone: "success", icon: CircleCheck, suffix: `/ ${tasks.total}` },
  ];

  return (
    <section aria-label={t("Мои задачи")} className={card}>
      <BlockHeader icon={SquareCheck} title={t("Мои задачи")} href="/tasks/my" action={t("Все задачи")} />
      {/* Пять плиток в ряд и значки — когда самой карточке хватает ширины (контейнерные запросы), иначе 2×2, как на телефоне. */}
      <div className="@container">
        <ul className="mt-4 grid grid-cols-2 gap-2.5 @min-[600px]:grid-cols-5 @min-[600px]:gap-3">
          {tiles.map((tile) => (
            <li
              key={tile.key}
              className={`rounded-xl px-3.5 py-3 @min-[600px]:rounded-[14px] @min-[600px]:px-4 @min-[600px]:py-3.5 ${
                tile.tone === "total" ? "hidden border border-line bg-page/60 @min-[600px]:block" : toneSoft[tile.tone]
              }`}
            >
              <div className="flex items-center justify-between gap-2">
                <span className="min-w-0 truncate text-[13px] text-ink-2">{tile.label}</span>
                <tile.icon aria-hidden className={`hidden size-[18px] shrink-0 @min-[760px]:block ${tile.tone === "total" ? "text-ink-3" : toneText[tile.tone]}`} strokeWidth={1.8} />
                {/* Телефон: число справа от подписи. */}
                <span className={`text-[20px] font-bold leading-none @min-[600px]:hidden ${tile.tone === "total" ? "text-ink" : toneText[tile.tone]}`}>
                  {tile.value}
                  {tile.suffix && <span className="text-[14px] font-semibold"> {tile.suffix}</span>}
                </span>
              </div>
              <p className={`mt-2 hidden text-[24px] font-bold leading-none @min-[600px]:block ${tile.tone === "total" ? "text-ink" : toneText[tile.tone]}`}>{tile.value}</p>
            </li>
          ))}
        </ul>
      </div>

      <h3 className="mt-5 text-[14px] font-semibold text-ink">{t("Ближайшие дедлайны")}</h3>
      {tasks.upcoming.length === 0 ? (
        <div className="mt-3">
          <Empty
            action={
              <Link href="/tasks/my" className={`text-[13px] font-semibold text-link ${focusRing}`}>
                {t("Все задачи")}
              </Link>
            }
          >
            {tasks.total - tasks.done > 0 ? t("Задач со сроком нет.") : t("У вас нет открытых задач.")}
          </Empty>
        </div>
      ) : (
        <ul className="@container mt-3 divide-y divide-line overflow-hidden rounded-xl border border-line">
          {tasks.upcoming.map((task, i) => {
            const status = taskStatus(task);
            const due = duePill(task, today, locale, t);
            return (
              <li key={task.id} className={i >= 3 ? "max-lg:hidden" : undefined}>
                <Link
                  href={`/tasks/task/${task.id}`}
                  className={`flex min-h-[56px] items-center gap-3 px-3.5 py-3 transition-colors hover:bg-surface-2 @min-[600px]:px-4 ${focusRing} focus-visible:ring-inset`}
                >
                  <Circle aria-hidden className="size-[22px] shrink-0 text-line-strong" strokeWidth={1.6} />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-[14.5px] font-medium text-ink">{task.title}</span>
                    {due && (
                      <span className={`mt-1 inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-[11.5px] font-semibold @min-[600px]:hidden ${duePillClass(due.tone)}`}>
                        <CalendarDays aria-hidden className="size-3" />
                        {due.label}
                      </span>
                    )}
                  </span>
                  <span className="hidden shrink-0 items-center gap-1.5 text-[13px] text-ink-2 @min-[600px]:inline-flex">
                    <span aria-hidden className={`size-[6px] rounded-full ${toneDot[status.tone]}`} />
                    {t(status.label)}
                  </span>
                  {due && (
                    <span className={`hidden shrink-0 items-center gap-1.5 rounded-lg px-2.5 py-1 text-[13px] font-semibold @min-[600px]:inline-flex ${duePillClass(due.tone)}`}>
                      <CalendarDays aria-hidden className="size-3.5" />
                      {due.label}
                    </span>
                  )}
                  <ChevronRight aria-hidden className="size-4 shrink-0 text-ink-3" />
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

const duePillClass = (tone: "danger" | "warning" | "neutral") =>
  tone === "danger" ? "bg-bad-soft text-bad" : tone === "warning" ? "bg-warn-soft text-warn" : "bg-surface-2 text-ink-2";

/**
 * «Личная эффективность» за 6 недель: выполнено в срок (только если есть выполненные задачи со сроком), завершено за неделю и мини-столбики
 * по неделям (текущая — accent). Целей в OneBase нет — их строка не выводится. Данных нет — «Недостаточно данных».
 */
export async function EfficiencyBlock({ data, t }: { data: Promise<MyTasks | null>; t: T }) {
  const tasks = await data;
  if (!tasks || !tasks.available) return null;
  const e = tasks.efficiency;
  const anyDone = e.weeks.some((w) => w.count > 0);
  const heights = barHeights(e.weeks.map((w) => w.count));
  const delta = weekDelta(e.doneThisWeek, e.doneLastWeek, t);
  const chartLabel = t("Завершено задач по неделям: {list}", { list: e.weeks.map((w) => `${w.number} — ${w.count}`).join(", ") });

  return (
    <section aria-label={t("Личная эффективность")} className={card}>
      <BlockHeader icon={TrendingUp} title={t("Личная эффективность")} short={t("Эффективность")} aside={t("последние 6 недель")} />
      {!anyDone ? (
        <div className="mt-4">
          <Empty>{t("Недостаточно данных — появятся после первых закрытых задач.")}</Empty>
        </div>
      ) : (
        <div className="@container">
          <div className="mt-4 grid grid-cols-2 gap-4 @xl:mt-5 @xl:grid-cols-[1fr_1fr_1.6fr] @xl:gap-0 @xl:divide-x @xl:divide-line">
            <div className="@xl:pr-6">
              <p className="text-[12.5px] text-ink-3 @xl:text-[13px]">{t("Выполнено в срок")}</p>
              {e.onTimePct !== null ? (
                <>
                  <p className="mt-1 text-[26px] font-bold leading-none text-ink @xl:mt-2 @xl:text-[34px]">{e.onTimePct}%</p>
                  <div
                    role="progressbar"
                    aria-valuenow={e.onTimePct}
                    aria-valuemin={0}
                    aria-valuemax={100}
                    aria-label={t("Выполнено в срок")}
                    className="mt-3 hidden h-1.5 overflow-hidden rounded-full bg-surface-2 @xl:block"
                  >
                    <div className="h-full rounded-full bg-ok" style={{ width: `${e.onTimePct}%` }} />
                  </div>
                  <p className="mt-1.5 text-[11.5px] text-ink-3 @xl:mt-2 @xl:text-[12px]">
                    {t("{n} из {total} закрытых задач — до срока", { n: e.onTime, total: e.closedWithDue })}
                  </p>
                </>
              ) : (
                <p className="mt-2 text-[12.5px] text-ink-3">{t("нет закрытых задач со сроком")}</p>
              )}
            </div>
            <div className="@xl:px-6">
              <p className="text-[12.5px] text-ink-3 @xl:text-[13px]">{t("Завершено за неделю")}</p>
              <p className="mt-1 text-[26px] font-bold leading-none text-ink @xl:mt-2 @xl:text-[34px]">{e.doneThisWeek}</p>
              <p className={`mt-1.5 inline-flex items-center gap-1 text-[11.5px] font-medium @xl:mt-2 @xl:text-[12.5px] ${toneText[delta.tone]}`}>
                {delta.tone === "success" && <TrendingUp aria-hidden className="size-3.5" />}
                {delta.label}
              </p>
            </div>
            <div className="col-span-2 @xl:col-span-1 @xl:pl-6">
              <p className="hidden text-[13px] text-ink-3 @xl:block">{t("Динамика завершённых задач")}</p>
              <div role="img" aria-label={chartLabel} className="mt-1 flex h-[72px] items-end gap-1.5 @xl:mt-3 @xl:h-[96px] @xl:gap-3">
                {e.weeks.map((w, i) => (
                  <div key={w.monday} className="flex h-full flex-1 flex-col items-center justify-end gap-1.5">
                    <div
                      className={`w-full rounded-t-[5px] ${i === e.weeks.length - 1 ? "bg-accent" : "bg-accent/30"}`}
                      style={{ height: `${heights[i]}%` }}
                      title={`${w.number}: ${w.count}`}
                    />
                    <span className={`text-[11px] ${i === e.weeks.length - 1 ? "font-bold text-ink" : "text-ink-3"}`}>{w.number}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
