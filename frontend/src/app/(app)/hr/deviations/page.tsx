import Link from "next/link";
import { HrFilters } from "@/components/hr/HrBits";
import { Empty, HrHeader, loadHr, SyncBanner } from "@/components/hr/HrPage";
import { Note } from "@/components/sales/bits";
import { PageBody } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { addDays, clock, dateParam, dayLabel, deviationLabel, divisionOptions, duration, type HrDeviation, param, qs, todayTashkent, weekday } from "@/lib/hr";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Опоздания и отсутствия · HR · OneBase") };
}

type Search = Promise<Record<string, string | string[] | undefined>>;

const kindTone: Record<HrDeviation["kind"], string> = {
  late: "bg-warn-soft text-warn",
  early: "bg-warn-soft text-warn",
  absent: "bg-bad-soft text-bad",
  noexit: "bg-muted text-ink-2",
};

/** «Опоздания и отсутствия»: отклонения за период по графику Verifix. Дни «не ясно» сюда не попадают — их нельзя считать нарушением. */
export default async function HrDeviationsPage({ searchParams }: { searchParams: Search }) {
  const sp = await searchParams;
  const ctx = await loadHr("/hr/deviations");
  const t = await getT();
  const today = todayTashkent();
  const to = dateParam(sp.to) ?? today;
  const from = dateParam(sp.from) ?? addDays(to, -6);
  const kind = param(sp.kind);
  const list = await apiGet<HrDeviation[]>(`/api/hr/deviations${qs({ from, to, kind, divisionId: param(sp.divisionId) })}`, "/hr/deviations");
  const count = (k: HrDeviation["kind"]) => list.filter((d) => d.kind === k).length;

  return (
    <>
      <HrHeader title="Опоздания и отсутствия" subtitle="Опоздания, ранние уходы, отсутствия и дни без отметки ухода — по графику Verifix" />
      <PageBody>
        <SyncBanner sync={ctx.sync} manages={ctx.managesIntegration} />
        <HrFilters
          fields={[
            { kind: "date", name: "from", label: "С", value: from },
            { kind: "date", name: "to", label: "По", value: to },
            {
              kind: "select",
              name: "kind",
              label: "Отклонение",
              all: "Все",
              options: (Object.keys(deviationLabel) as HrDeviation["kind"][]).map((k) => ({ value: k, label: deviationLabel[k] })),
            },
            { kind: "select", name: "divisionId", label: "Подразделение", all: "Все подразделения", options: divisionOptions(ctx.divisions) },
          ]}
        />
        <div className="mb-3 flex flex-wrap gap-x-4 gap-y-1 text-sm text-ink-2">
          <span className="text-warn">{t("опозданий: {n}", { n: count("late") })}</span>
          <span className="text-warn">{t("ранних уходов: {n}", { n: count("early") })}</span>
          <span className="text-bad">{t("отсутствий: {n}", { n: count("absent") })}</span>
          <span>{t("без ухода: {n}", { n: count("noexit") })}</span>
        </div>

        {list.length === 0 ? (
          <Empty text="Отклонений за период нет." />
        ) : (
          <div className="overflow-x-auto rounded-xl border border-line bg-surface">
            <table className="w-full min-w-[860px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="px-4 py-2.5 font-semibold">{t("Дата")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Сотрудник")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Отклонение")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Сколько")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("График")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Приход")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Уход")}</th>
                  <th className="px-4 py-2.5 font-semibold">{t("Пояснение")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {list.map((d) => (
                  <tr key={`${d.date}-${d.employeeId}-${d.kind}`} className="hover:bg-muted/40">
                    <td className="px-4 py-2 tabular-nums text-ink">
                      {dayLabel(d.date)} <span className="text-ink-3">{t(weekday(d.date))}</span>
                    </td>
                    <td className="px-3 py-2">
                      <Link href={`/hr/employees/${d.employeeId}${qs({ from, to })}`} className="font-medium text-ink hover:text-accent-strong">
                        {d.employee}
                      </Link>
                      {d.division && <div className="text-xs text-ink-3">{d.division}</div>}
                    </td>
                    <td className="px-3 py-2">
                      <span className={`inline-flex whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-semibold ${kindTone[d.kind]}`}>{t(deviationLabel[d.kind])}</span>
                    </td>
                    <td className="px-3 py-2 text-right tabular-nums">{d.kind === "absent" ? (d.minutes ? t("план {time}", { time: duration(d.minutes, t) }) : "—") : duration(d.minutes, t)}</td>
                    <td className="whitespace-nowrap px-3 py-2 tabular-nums text-ink-2">{d.planBegin ? `${clock(d.planBegin)}–${clock(d.planEnd)}` : "—"}</td>
                    <td className="px-3 py-2 tabular-nums">{clock(d.firstIn)}</td>
                    <td className="px-3 py-2 tabular-nums">{clock(d.lastOut)}</td>
                    <td className="px-4 py-2 text-xs text-ink-3">{d.reason ? t(d.reason) : ""}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <Note>
          {t(
            "Опоздание — приход позже начала смены больше чем на допуск из настроек; ранний уход — последний уход раньше конца смены (после её окончания). Отсутствие — рабочий день без прихода и без документа при полностью загруженных отметках. Дни, где данных недостаточно, сюда не попадают.",
          )}
        </Note>
      </PageBody>
    </>
  );
}
