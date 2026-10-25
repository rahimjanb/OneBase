import Link from "next/link";
import { ArrowRight } from "lucide-react";
import type { Leader } from "@/lib/dashboard.mock";
import type { T } from "@/i18n/core";
import { Avatar, CardHeader, DashCard, outlineButton } from "./blocks";

/** «Лидеры месяца»: сотрудники по индивидуальному KPI (появятся с KPI сотрудников; пока список пуст), внизу — полный рейтинг. */
export function LeadersCard({ leaders, t }: { leaders: Leader[]; t: T }) {
  return (
    <DashCard className="flex flex-col gap-4 p-5 xl:w-[280px]">
      <CardHeader title={t("Лидеры месяца")} subtitle={t("По индивидуальному KPI")} />
      {leaders.length === 0 ? (
        <div className="flex flex-1 items-center justify-center px-2 py-8 text-center text-[13px] text-ink-4">{t("Рейтинг появится, когда будут подключены KPI сотрудников")}</div>
      ) : (
        <ol className="flex flex-col gap-3.5">
          {leaders.map((l, i) => (
            <li key={l.name} className="flex items-center gap-3">
              <span className={`w-3.5 text-xs font-semibold tabular-nums ${i === 0 ? "text-warn" : "text-ink-4"}`}>{i + 1}</span>
              <Avatar initials={l.initials} tone={l.tone} size={32} />
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[13px] font-medium text-ink">{l.name}</span>
                <span className="block truncate text-[11.5px] text-ink-4">{t(l.department)}</span>
              </span>
              <span className={`text-[13px] font-semibold tabular-nums ${i === 0 ? "text-ok" : "text-ink"}`}>{l.kpi}</span>
            </li>
          ))}
        </ol>
      )}
      <Link href="/sales/kpi" className={`${outlineButton} mt-auto h-9 w-full rounded-[9px]`}>
        {t("Полный рейтинг")} <ArrowRight className="size-3.5" />
      </Link>
    </DashCard>
  );
}
