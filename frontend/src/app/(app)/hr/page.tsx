import Link from "next/link";
import { CalendarClock, CircleHelp, DoorOpen, Hourglass, Palmtree, UserCheck, UserX, Users } from "lucide-react";
import { Empty, HrHeader, loadHr, SyncBanner } from "@/components/hr/HrPage";
import { Note } from "@/components/sales/bits";
import { PageBody } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { dayLabel, weekday, type HrOverview } from "@/lib/hr";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("HR · OneBase") };
}

/**
 * «Обзор HR»: сегодня по данным Verifix — активные сотрудники, пришли, опоздали, отсутствуют, по документу, ожидаются, не ясно, проходы;
 * по подразделениям и локациям. «Отсутствует» — только рабочий день по графику без документа и при полностью загруженных отметках.
 */
export default async function HrOverviewPage() {
  const ctx = await loadHr("/hr");
  const overview = await apiGet<HrOverview>("/api/hr/overview", "/hr");
  const t = await getT();
  const day = overview.date;

  const tiles = [
    { label: "Активных сотрудников", value: overview.active, icon: Users, tone: "text-ink", href: "/hr/employees" },
    { label: "Пришли сегодня", value: overview.present, icon: UserCheck, tone: "text-ok", href: `/hr/attendance?status=present` },
    { label: "Опоздали", value: overview.late, icon: CalendarClock, tone: "text-warn", href: `/hr/attendance?status=late` },
    { label: "Отсутствуют по данным системы", value: overview.absent, icon: UserX, tone: "text-bad", href: `/hr/attendance?status=absent` },
    { label: "По документу (отпуск, больничный…)", value: overview.onLeave, icon: Palmtree, tone: "text-violet-light", href: `/hr/attendance?status=leave` },
    { label: "Ожидаются", value: overview.expected, icon: Hourglass, tone: "text-accent-strong", href: `/hr/attendance?status=expected` },
    { label: "Не ясно (данных недостаточно)", value: overview.unknown, icon: CircleHelp, tone: "text-warn", href: `/hr/attendance?status=unknown` },
    { label: "Проходов сегодня", value: overview.eventsToday, icon: DoorOpen, tone: "text-ink", href: `/hr/attendance?view=events` },
  ];

  return (
    <>
      <HrHeader title="HR" subtitle="Сотрудники, посещаемость и рабочее время по данным Verifix" crumb={[{ label: t("Обзор HR") }]} />
      <PageBody>
        <SyncBanner sync={overview.sync} manages={ctx.managesIntegration} />
        {!overview.seesAll && (
          <p className="mb-3 text-xs text-ink-3">{t("Показаны сотрудники подразделений вашего отдела.")}</p>
        )}

        <h2 className="mb-2 text-sm font-semibold text-ink">
          {t("Сегодня, {date} ({weekday}), на {time}", { date: dayLabel(day, true), weekday: t(weekday(day)), time: overview.now.slice(11, 16) })}
        </h2>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {tiles.map((tile) => (
            <Link key={tile.label} href={tile.href} className="rounded-xl border border-line bg-surface p-4 hover:border-accent/40">
              <tile.icon className={`size-4 ${tile.tone}`} />
              <div className="mt-2 text-2xl font-semibold tabular-nums text-ink">{tile.value}</div>
              <div className="mt-0.5 text-xs text-ink-3">{t(tile.label)}</div>
            </Link>
          ))}
        </div>
        {(overview.dayOff > 0 || overview.noSchedule > 0 || overview.missingInVerifix > 0) && (
          <p className="mt-2 text-xs text-ink-3">
            {overview.dayOff > 0 && <>{t("Выходной по графику: {n}", { n: overview.dayOff })}. </>}
            {overview.noSchedule > 0 && <>{t("Без графика в Verifix: {n}", { n: overview.noSchedule })}. </>}
            {overview.missingInVerifix > 0 && <>{t("Не пришли в последней выгрузке Verifix (записи сохранены): {n}", { n: overview.missingInVerifix })}.</>}
          </p>
        )}

        <div className="mt-5 grid gap-4 2xl:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
          <section className="min-w-0 rounded-xl border border-line bg-surface p-4 sm:p-5">
            <h2 className="mb-3 text-sm font-semibold text-ink">{t("По подразделениям")}</h2>
            {overview.divisions.length === 0 ? (
              <Empty text="Сотрудников пока нет — данные появятся после синхронизации с Verifix." />
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[560px] text-sm">
                  <thead>
                    <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                      <th className="py-2 pr-3 font-semibold">{t("Подразделение")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("Сотрудников")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("Пришли")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("Опоздали")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("Отсутствуют")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("По документу")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("Не ясно")}</th>
                      <th className="py-2 text-right font-semibold">{t("Проходов")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line">
                    {overview.divisions.map((d) => (
                      <tr key={d.id ?? "none"}>
                        <td className="py-2 pr-3">
                          {d.id != null ? (
                            <Link href={`/hr/attendance?divisionId=${d.id}`} className="font-medium text-ink hover:text-accent-strong">
                              {d.name}
                            </Link>
                          ) : (
                            <span className="text-ink-2">{t(d.name)}</span>
                          )}
                        </td>
                        <td className="py-2 pr-3 text-right tabular-nums">{d.active}</td>
                        <td className="py-2 pr-3 text-right tabular-nums text-ok">{d.present}</td>
                        <td className={`py-2 pr-3 text-right tabular-nums ${d.late > 0 ? "text-warn" : "text-ink-3"}`}>{d.late}</td>
                        <td className={`py-2 pr-3 text-right tabular-nums ${d.absent > 0 ? "font-semibold text-bad" : "text-ink-3"}`}>{d.absent}</td>
                        <td className="py-2 pr-3 text-right tabular-nums text-ink-2">{d.onLeave}</td>
                        <td className="py-2 pr-3 text-right tabular-nums text-ink-3">{d.unknown}</td>
                        <td className="py-2 text-right tabular-nums text-ink-2">{d.events}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="min-w-0 rounded-xl border border-line bg-surface p-4 sm:p-5">
            <h2 className="mb-3 text-sm font-semibold text-ink">{t("По филиалам и локациям (проходы сегодня)")}</h2>
            {overview.locations.length === 0 ? (
              <Empty text="Сегодня проходов ещё нет." />
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                    <th className="py-2 pr-3 font-semibold">{t("Локация")}</th>
                    <th className="py-2 pr-3 text-right font-semibold">{t("Сотрудников")}</th>
                    <th className="py-2 text-right font-semibold">{t("Проходов")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {overview.locations.map((l) => (
                    <tr key={l.id ?? "none"}>
                      <td className="py-2 pr-3 text-ink">{t(l.name)}</td>
                      <td className="py-2 pr-3 text-right tabular-nums">{l.employees}</td>
                      <td className="py-2 text-right tabular-nums text-ink-2">{l.events}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        </div>

        <Note>
          {t(
            "«Отсутствует» ставится, только если день рабочий по графику Verifix, нет отпуска, больничного, командировки или отгула, прошло начало смены с допуском и отметки за это время загружены полностью. Иначе — «ожидается», «нет графика» или «не ясно». Одна отметка — ещё не рабочий день: приход засчитывается по табелю Verifix или отметке «Приход».",
          )}
        </Note>
      </PageBody>
    </>
  );
}
