import { notFound } from "next/navigation";
import { EmployeeNote } from "@/components/hr/EmployeeNote";
import { EmployeeStatusBadge, HrFilters } from "@/components/hr/HrBits";
import { Empty, HrHeader, loadHr } from "@/components/hr/HrPage";
import { HrDaysTable, HrEventsTable } from "@/components/hr/HrTables";
import { PageBody } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { dateParam, dayLabel, duration, momentLabel, qs, type HrEmployeeCard } from "@/lib/hr";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Сотрудник · HR · OneBase") };
}

type Props = { params: Promise<{ id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> };

/** Карточка сотрудника: поля Verifix (только чтение), заметка HR (локально), итоги и дни за период, отметки. */
export default async function HrEmployeePage({ params, searchParams }: Props) {
  const { id } = await params;
  const sp = await searchParams;
  const path = `/hr/employees/${id}`;
  const ctx = await loadHr(path);
  const card = /^[0-9a-f-]{36}$/i.test(id)
    ? await apiGetOrNull<HrEmployeeCard>(`/api/hr/employees/${id}${qs({ from: dateParam(sp.from), to: dateParam(sp.to) })}`, path)
    : null;
  if (!card) notFound();
  const t = await getT();
  const e = card.employee;
  const s = card.summary;

  const facts: [string, React.ReactNode][] = [
    ["ID Verifix", e.verifixId],
    ["Подразделение", e.division ?? "—"],
    ["Должность", e.job ?? "—"],
    ["График", e.schedule ?? "—"],
    ["Принят", dayLabel(e.hiringDate, true)],
    ["Уволен", e.dismissalDate ? dayLabel(e.dismissalDate, true) : "—"],
    ["Телефон", e.phone ?? "—"],
    ["Эл. почта", card.email ?? "—"],
  ];
  const tiles: [string, string, string][] = [
    ["Рабочих дней", String(s.planDays), "text-ink"],
    ["С приходом", String(s.presentDays), "text-ok"],
    ["Опозданий", s.lateDays > 0 ? `${s.lateDays} · ${duration(s.lateMinutes, t)}` : "0", s.lateDays > 0 ? "text-warn" : "text-ink"],
    ["Ранних уходов", String(s.earlyDays), s.earlyDays > 0 ? "text-warn" : "text-ink"],
    ["Отсутствий", String(s.absentDays), s.absentDays > 0 ? "text-bad" : "text-ink"],
    ["По документу", String(s.leaveDays), "text-violet-light"],
    ["Отработано", duration(s.workedMinutes, t), "text-ink"],
  ];

  return (
    <>
      <HrHeader title={e.fullName} crumb={[{ label: t("Сотрудники"), href: "/hr/employees" }, { label: e.fullName }]} />
      <PageBody>
        <div className="grid gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
          <section className="rounded-xl border border-line bg-surface p-4 sm:p-5">
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
              <h2 className="text-sm font-semibold text-ink">{t("Данные Verifix")}</h2>
              <EmployeeStatusBadge status={e.status} missing={!!e.missingSince} />
            </div>
            <dl className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-1.5 text-sm">
              {facts.map(([label, value]) => (
                <div key={label} className="contents">
                  <dt className="text-ink-3">{t(label)}</dt>
                  <dd className="min-w-0 break-words text-ink">{value}</dd>
                </div>
              ))}
            </dl>
            <p className="mt-3 text-xs text-ink-3">
              {t("Эти поля приходят из Verifix и меняются только там. Загружено впервые {first}, обновлено {last}.", {
                first: momentLabel(card.firstSyncedAt),
                last: momentLabel(card.lastSyncedAt),
              })}
              {e.missingSince && <> {t("В последней выгрузке Verifix сотрудника не было ({date}) — запись сохранена.", { date: momentLabel(e.missingSince) })}</>}
            </p>
          </section>

          <section className="rounded-xl border border-line bg-surface p-4 sm:p-5">
            <h2 className="mb-2 text-sm font-semibold text-ink">{t("Заметка HR")}</h2>
            <EmployeeNote id={e.id} note={card.note} canEdit={card.canEditNote} />
            {card.noteUpdatedAt && <p className="mt-2 text-xs text-ink-3">{t("Изменена {date}", { date: momentLabel(card.noteUpdatedAt) })}</p>}
            <p className="mt-2 text-xs text-ink-3">{t("Заметка хранится только в OneBase — синхронизация с Verifix её не меняет.")}</p>
          </section>
        </div>

        <div className="mt-5">
          <HrFilters
            fields={[
              { kind: "date", name: "from", label: "С", value: card.from },
              { kind: "date", name: "to", label: "По", value: card.to },
            ]}
          />
        </div>
        <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-4 xl:grid-cols-7">
          {tiles.map(([label, value, tone]) => (
            <div key={label} className="rounded-xl border border-line bg-surface p-3">
              <div className={`text-lg font-semibold tabular-nums ${tone}`}>{value}</div>
              <div className="text-xs text-ink-3">{t(label)}</div>
            </div>
          ))}
        </div>

        {card.days.length === 0 ? <Empty text="За период дней нет." /> : <HrDaysTable days={card.days} />}

        <h2 className="mb-2 mt-6 text-sm font-semibold text-ink">{t("Отметки за период")}</h2>
        {card.events.length === 0 ? <Empty text="Отметок за период нет." /> : <HrEventsTable events={card.events} showEmployee={false} />}
      </PageBody>
    </>
  );
}
