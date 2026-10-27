import Link from "next/link";
import { ArrowRight, Trophy } from "lucide-react";
import type { Tone } from "@/lib/dashboard.mock";
import type { HrLeader, HrLeaders } from "@/lib/hr";
import { decimal, nameParts } from "@/lib/hr-timesheet";
import type { T } from "@/i18n/core";
import { Avatar, CardHeader, DashCard, outlineButton } from "./blocks";

/** Аватары остальных мест — цвет по сотруднику, чтобы не менялся от места к месту. */
const TONES: Tone[] = ["accent", "violet", "cyan", "pink", "ok"];
const toneOf = (id: string): Tone => TONES[[...id].reduce((sum, c) => sum + c.charCodeAt(0), 0) % TONES.length];

/** Медаль 2-го и 3-го места: серебро и бронза — мягкие, без hex. */
const MEDAL: Record<number, string> = { 2: "bg-ink-3/15 text-ink-2", 3: "bg-warn/12 text-warn" };

/**
 * «Лидеры месяца» (данные Verifix): сотрудники, которые за месяц ни разу не опоздали, — по отработанным часам. Первое место — отдельной
 * плашкой с кубком, второе и третье — с медалями, остальные — компактным списком под чертой. Рейтинг видят HR и руководители отделов
 * (access); data = null при доступе — API не ответил.
 */
export function LeadersCard({ data, access, month, t }: { data: HrLeaders | null; access: boolean; month: string; t: T }) {
  const leaders = data?.leaders ?? [];
  const [first, ...rest] = leaders;
  const podium = rest.slice(0, 2);
  const others = rest.slice(2);
  const empty = !access
    ? t("Рейтинг виден HR и руководителям отделов")
    : !data
      ? t("Не удалось загрузить данные Verifix")
      : !data.sync.configured
        ? t("Подключите Verifix, чтобы увидеть лидеров")
        : t("Пока никто не отработал месяц без опозданий");

  return (
    <DashCard className="flex flex-col gap-4 p-5 xl:w-[280px]">
      <CardHeader title={t("Лидеры месяца")} subtitle={t("Без опозданий · часы за {month}", { month })} />
      {!first ? (
        <div className="flex flex-1 items-center justify-center px-2 py-8 text-center text-[13px] text-ink-4">{empty}</div>
      ) : (
        <ol className="flex flex-col gap-2.5">
          <li>
            <LeaderLink leader={first} className="gap-3 border border-warn/25 bg-warn-soft px-3 py-2.5" hover="hover:border-warn/50">
              <span className="relative shrink-0">
                <Avatar initials={nameParts(first.employee).initials} tone="warn" size={36} />
                <span className="absolute -bottom-1 -right-1 grid size-[18px] place-items-center rounded-full border border-warn/25 bg-surface" title={t("Лидер месяца")}>
                  <Trophy className="size-2.5 text-warn" aria-label={t("Лидер месяца")} />
                </span>
              </span>
              <Name leader={first} strong />
              <Value leader={first} t={t} strong />
            </LeaderLink>
          </li>
          {podium.map((l) => (
            <li key={l.employeeId}>
              <LeaderLink leader={l} className="gap-2.5 px-1.5 py-1">
                <span className={`grid size-5 shrink-0 place-items-center rounded-full text-[11px] font-semibold tabular-nums ${MEDAL[l.place]}`}>{l.place}</span>
                <Avatar initials={nameParts(l.employee).initials} tone={toneOf(l.employeeId)} size={30} />
                <Name leader={l} />
                <Value leader={l} t={t} />
              </LeaderLink>
            </li>
          ))}
          {others.length > 0 && <li aria-hidden className="mx-1.5 border-t border-line" />}
          {others.map((l) => (
            <li key={l.employeeId}>
              <LeaderLink leader={l} className="gap-2.5 px-1.5 py-0.5">
                <span className="w-5 shrink-0 text-center text-[11.5px] font-medium tabular-nums text-ink-4">{l.place}</span>
                <Avatar initials={nameParts(l.employee).initials} tone={toneOf(l.employeeId)} size={26} />
                <Name leader={l} small />
                <span className="shrink-0 text-[12.5px] font-medium tabular-nums text-ink-2">{t("{n} ч", { n: decimal(l.workedMinutes / 60) })}</span>
              </LeaderLink>
            </li>
          ))}
        </ol>
      )}
      {data && first && (
        <p className="text-[11.5px] text-ink-4">{t("Без опозданий: {count} из {total}", { count: data.onTime, total: data.employees })}</p>
      )}
      {access && (
        <Link href="/hr/timesheets?sort=worked&dir=desc" className={`${outlineButton} mt-auto h-9 w-full rounded-[9px]`}>
          {t("Полный рейтинг")} <ArrowRight className="size-3.5" />
        </Link>
      )}
    </DashCard>
  );
}

function LeaderLink({ leader, className, hover = "hover:bg-muted", children }: { leader: HrLeader; className: string; hover?: string; children: React.ReactNode }) {
  return (
    <Link
      href={`/hr/employees/${leader.employeeId}`}
      title={leader.employee}
      className={`flex items-center rounded-[10px] transition-colors duration-150 ${hover} focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${className}`}
    >
      {children}
    </Link>
  );
}

/** «Фамилия И.» — карточка узкая; полное ФИО — в подсказке ссылки. */
const shortName = (full: string) => {
  const [last, first] = nameParts(full).name.split(" ");
  return first ? `${last} ${first.charAt(0)}.` : last;
};

/** Первое место — «Фамилия Имя» с переносом на вторую строку, остальные — «Фамилия И.» в одну строку. */
function Name({ leader, strong, small }: { leader: HrLeader; strong?: boolean; small?: boolean }) {
  return (
    <span className="min-w-0 flex-1">
      <span className={`block text-ink ${strong ? "line-clamp-2 text-[13.5px] font-semibold leading-snug" : `truncate ${small ? "text-[12.5px] font-medium" : "text-[13px] font-medium"}`}`}>
        {strong ? nameParts(leader.employee).name : shortName(leader.employee)}
      </span>
      {!small && leader.division && <span className="block truncate text-[11.5px] text-ink-4">{leader.division}</span>}
    </span>
  );
}

function Value({ leader, t, strong }: { leader: HrLeader; t: T; strong?: boolean }) {
  return (
    <span className="shrink-0 text-right">
      <span className={`block font-semibold tabular-nums text-ink ${strong ? "text-[15px]" : "text-[13px]"}`}>{t("{n} ч", { n: decimal(leader.workedMinutes / 60) })}</span>
      <span className="block text-[11px] tabular-nums text-ink-4" title={t("Дней с приходом вовремя")}>{t("{n} дн.", { n: leader.presentDays })}</span>
    </span>
  );
}
