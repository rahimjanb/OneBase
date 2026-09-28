import Link from "next/link";
import { ArrowLeft, ChartColumn, Target, TriangleAlert } from "lucide-react";
import { PageBody, PageHeader, type Crumb } from "@/components/shell/PageHeader";
import { SalesToolbar } from "./SalesToolbar";
import { SyncButton } from "./SyncButton";
import { apiGet } from "@/lib/server-api";
import { dateTime } from "@/lib/sales/format";
import { param, periodQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { Period, SalesMonth, SyncStatus } from "@/lib/sales/types";

export type SalesTab = "analytics" | "plans" | "problems" | "setup";

/** Разделы продаж — кнопки, каждая открывает свою страницу. Настройки продаж — в «Настройки → Интеграции → Linko». */
const tabs: { key: SalesTab; label: string; href: string; icon: typeof ChartColumn }[] = [
  { key: "analytics", label: "Аналитика", href: "/sales", icon: ChartColumn },
  { key: "plans", label: "Планы", href: "/sales/plans", icon: Target },
  { key: "problems", label: "Проблемные агенты", href: "/sales/problems", icon: TriangleAlert },
];

/** Чип «данные по …» — время последней успешной синхронизации с Linko. */
function DataChip({ status }: { status: SyncStatus }) {
  if (!status.configured) {
    return (
      <Link href="/settings/integrations/sales/linko" className="rounded-full bg-warn-soft px-3 py-1 text-xs font-medium text-warn hover:underline">
        Linko не настроен — подключить
      </Link>
    );
  }
  if (status.hasErrors) {
    return (
      <span className="rounded-full bg-warn-soft px-3 py-1 text-xs font-medium text-warn" title="Последняя синхронизация с Linko завершилась с ошибкой — подробности в «Настройках»">
        Синхронизация с ошибкой · данные по {dateTime(status.dataAsOf)}
      </span>
    );
  }
  return (
    <span className="rounded-full border border-line bg-surface px-3 py-1 text-xs tabular-nums text-ink-2">
данные по {dateTime(status.dataAsOf)}
    </span>
  );
}

/** Оболочка страниц раздела «Продажи»: шапка, крошки, вкладки, период, «← Назад». */
export async function SalesFrame({
  title,
  subtitle,
  crumbs = [],
  back,
  tab = "analytics",
  period,
  sp,
  returnTo,
  children,
}: {
  title: string;
  subtitle?: string;
  crumbs?: Crumb[];
  back?: string;
  tab?: SalesTab;
  period?: Period;
  sp: SalesSearchParams;
  returnTo: string;
  children: React.ReactNode;
}) {
  const [status, months] = await Promise.all([
    apiGet<SyncStatus>("/api/sales/status", returnTo),
    apiGet<SalesMonth[]>("/api/sales/months", returnTo),
  ]);

  const query = periodQuery(sp);
  const year = period?.year ?? Number(param(sp, "year") ?? months[0]?.year ?? new Date().getFullYear());
  const month = period?.month ?? Number(param(sp, "month") ?? months[0]?.month ?? new Date().getMonth() + 1);

  return (
    <>
      <PageHeader
        title={title}
        subtitle={subtitle}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Продажи", href: query ? `/sales?${query}` : "/sales" }, ...crumbs]}
      />
      <PageBody>
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line pb-3">
          <nav className="flex flex-wrap gap-2" aria-label="Разделы продаж">
            {tabs.map((t) => {
              const active = tab === t.key;
              return (
                <Link
                  key={t.key}
                  href={query ? `${t.href}?${query}` : t.href}
                  aria-current={active ? "page" : undefined}
                  className={`inline-flex h-9 items-center gap-2 whitespace-nowrap rounded-lg border px-4 text-sm font-medium shadow-sm transition-colors ${
                    active
                      ? "border-accent bg-accent text-white hover:bg-accent-strong"
                      : "border-line bg-surface text-ink hover:border-accent/40 hover:bg-muted"
                  }`}
                >
                  <t.icon className="size-4" />
                  {t.label}
                </Link>
              );
            })}
          </nav>
          <div className="flex flex-wrap items-center gap-2">
            {tab !== "setup" && <SalesToolbar months={months} year={year} month={month} plan={param(sp, "plan") ?? "Rop"} />}
            <DataChip status={status} />
            <SyncButton initial={status} />
          </div>
        </div>

        {back && (
          <Link href={back} className="mt-4 inline-flex items-center gap-1.5 rounded-lg border border-line bg-surface px-3 py-1.5 text-xs font-medium text-ink hover:bg-muted">
            <ArrowLeft className="size-3.5" />
            Назад
          </Link>
        )}

        <div className="mt-4">{children}</div>
      </PageBody>
    </>
  );
}
