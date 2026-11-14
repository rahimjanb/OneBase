"use client";

import Link from "next/link";
import { CalendarClock, ChevronRight, MessageCircleOff, PowerOff, TimerReset, TriangleAlert, UserRoundX } from "lucide-react";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import { nameOf, type Attention, type Issue, type NearSend } from "@/lib/telegram-recipients";

const WEEKDAY = ["вс", "пн", "вт", "ср", "чт", "пт", "сб"];
const EN = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
/** День недели запуска по Ташкенту: «пн». */
const weekday = (iso: string) => WEEKDAY[Math.max(0, EN.indexOf(new Intl.DateTimeFormat("en-US", { weekday: "short", timeZone: "Asia/Tashkent" }).format(new Date(iso))))];

/** «Ближайшие отправки сегодня»: ближайшие запуски по времени, что уходит и скольким. */
export function NearSendsCard({ items, onSchedule }: { items: NearSend[]; onSchedule: () => void }) {
  const t = useT();
  return (
    <section className="rounded-[16px] border border-line bg-surface px-4 py-3.5">
      <div className="mb-2 flex items-center gap-2">
        <CalendarClock aria-hidden className="size-4 text-accent-strong" />
        <h3 className="flex-1 text-[14px] font-bold text-ink">{t("Ближайшие отправки сегодня")}</h3>
        <button type="button" onClick={onSchedule} className="flex items-center gap-0.5 text-[12px] font-semibold text-accent-strong hover:underline">
          {t("Расписание")}
          <ChevronRight aria-hidden className="size-3.5" />
        </button>
      </div>
      {items.length === 0 ? (
        <p className="py-2 text-[12px] text-ink-3">{t("Ни один запуск не включён — по расписанию ничего не уходит.")}</p>
      ) : (
        <ul>
          {items.map((x, i) => {
            const when =
              x.day === "today" ? x.time : x.day === "tomorrow" ? `${x.time} ${t("завтра")}` : `${t(weekday(x.at))} ${x.time}`;
            const [head, ...rest] = x.jobs;
            const what = [`${t(head.reportTitle).replace(/^\d+\.\s*/, "")} · ${t(head.slotTitle)}`, ...rest.map((j) => t(j.reportTitle).replace(/^\d+\.\s*/, ""))].join(", ");
            return (
              <li key={x.at} className="flex items-center gap-3 border-b border-line py-2 text-[12px] last:border-b-0">
                <span className={`flex w-[92px] shrink-0 items-center gap-1.5 font-bold tabular-nums ${i === 0 ? "text-accent-strong" : "text-ink"}`}>
                  <span aria-hidden className={`size-1.5 rounded-full ${i === 0 ? "bg-accent" : "bg-ink-4"}`} />
                  {when}
                </span>
                <span className="min-w-0 flex-1 truncate text-ink-2" title={what}>
                  {what}
                </span>
                <span className="shrink-0 text-[11px] text-ink-4">
                  {x.recipients} {t(plural(x.recipients, ["получатель", "получателя", "получателей"]))}
                </span>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

/** «Требуют внимания»: без чата, ТП без СВ, срок отключения прошёл, ошибки доставки, отключённые — с переходом к фильтру или действию. */
export function AttentionCard({ a, onIssue, onEnable }: { a: Attention; onIssue: (issue: Issue) => void; onEnable: (ids: string[]) => void }) {
  const t = useT();
  const names = (list: Attention["disabled"]) => (list.length <= 2 ? list.map(nameOf).join(", ") : t("{name} и ещё {n}", { name: nameOf(list[0]), n: list.length - 1 }));
  const items = [
    a.noChat.length > 0 && {
      key: "chat",
      icon: MessageCircleOff,
      title: `${a.noChat.length} ${t(plural(a.noChat.length, ["получатель без чата", "получателя без чата", "получателей без чата"]))}`,
      sub: a.skippedNoChat > 0 ? t("из-за чатов пропущено {n} отправок за 7 дней", { n: a.skippedNoChat }) : t("отчёты им не уходят"),
      action: <Action onClick={() => onIssue("no_chat")}>{t("Привязать")}</Action>,
    },
    a.noSupervisor.length > 0 && {
      key: "sv",
      icon: UserRoundX,
      title: `${a.noSupervisor.length} ${t(plural(a.noSupervisor.length, ["ТП без супервайзера", "ТП без супервайзера", "ТП без супервайзера"]))}`,
      sub: t("у их региона нет СВР в оргструктуре «Продаж» — на картинке ТП нет супервайзера"),
      action: (
        <Link href="/sales/setup" className="flex shrink-0 items-center gap-0.5 text-[12px] font-semibold text-accent-strong hover:underline">
          {t("Назначить")}
          <ChevronRight aria-hidden className="size-3.5" />
        </Link>
      ),
    },
    a.overdue.length > 0 && {
      key: "overdue",
      icon: TimerReset,
      title: t("Срок отключения прошёл: {n}", { n: a.overdue.length }),
      sub: names(a.overdue),
      action: <Action onClick={() => onEnable(a.overdue.map((r) => r.id))}>{t("Включить")}</Action>,
    },
    a.failing.length > 0 && {
      key: "failing",
      icon: TriangleAlert,
      title: t("Ошибки доставки за 7 дней: {n}", { n: a.failing.length }),
      sub: names(a.failing),
      action: <Action onClick={() => onIssue("delivery_error")}>{t("Показать")}</Action>,
    },
    a.disabled.length > 0 && {
      key: "off",
      icon: PowerOff,
      title: `${a.disabled.length} ${t(plural(a.disabled.length, ["отключён", "отключены", "отключены"]))}`,
      sub: names(a.disabled),
      action: <Action onClick={() => onIssue("disabled")}>{t("Показать")}</Action>,
    },
  ].filter(Boolean) as { key: string; icon: typeof TriangleAlert; title: string; sub: string; action: React.ReactNode }[];
  const total = a.noChat.length + a.noSupervisor.length + a.overdue.length + a.failing.length + a.disabled.length;

  return (
    <section className="rounded-[16px] border border-line bg-surface px-4 py-3.5">
      <div className="mb-2.5 flex items-center gap-2">
        <TriangleAlert aria-hidden className="size-4 text-warn" />
        <h3 className="text-[14px] font-bold text-ink">{t("Требуют внимания")}</h3>
        {total > 0 && <span className="rounded-md bg-warn-soft px-1.5 py-0.5 text-[11px] font-bold tabular-nums text-warn">{total}</span>}
      </div>
      {items.length === 0 ? (
        <p className="py-1 text-[12px] text-ink-3">{t("Всё в порядке: у всех есть чат, никто не отключён.")}</p>
      ) : (
        <ul className="space-y-2">
          {items.map((item) => (
            <li key={item.key} className="flex items-center gap-3 rounded-[12px] bg-muted px-3 py-2.5">
              <span aria-hidden className="grid size-8 shrink-0 place-items-center rounded-[9px] bg-warn-soft text-warn">
                <item.icon className="size-4" />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[13px] font-semibold text-ink">{item.title}</span>
                <span className="block truncate text-[11px] text-ink-4">{item.sub}</span>
              </span>
              {item.action}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function Action({ onClick, children }: { onClick: () => void; children: React.ReactNode }) {
  return (
    <button type="button" onClick={onClick} className="flex shrink-0 items-center gap-0.5 text-[12px] font-semibold text-accent-strong hover:underline">
      {children}
      <ChevronRight aria-hidden className="size-3.5" />
    </button>
  );
}
