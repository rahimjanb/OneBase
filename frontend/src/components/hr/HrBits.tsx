"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useRef } from "react";
import { useT } from "@/i18n/client";
import { dayCodeTone, dayStatusLabel, dayStatusTone, employeeStatusLabel, syncStatusLabel, syncStatusTone, VERIFIX_JOURNAL, VERIFIX_SETTINGS, type HrDay, type HrDayStatus, type HrSyncStatus } from "@/lib/hr";

/** Вкладки интеграции Verifix в «Настройки → Интеграции → HR»: подключение и синхронизация, журнал синхронизации. */
export function VerifixTabs() {
  const t = useT();
  const pathname = usePathname();
  const tabs = [
    { href: VERIFIX_SETTINGS, label: "Подключение и синхронизация" },
    { href: VERIFIX_JOURNAL, label: "Журнал синхронизации" },
  ];
  return (
    <nav aria-label={t("Разделы интеграции")} className="-mx-4 mb-5 flex gap-1 overflow-x-auto border-b border-line px-4 sm:mx-0 sm:px-0">
      {tabs.map((tab) => (
        <Link
          key={tab.href}
          href={tab.href}
          aria-current={pathname === tab.href ? "page" : undefined}
          className={`-mb-px inline-flex shrink-0 items-center border-b-2 px-3 py-2.5 text-sm font-medium transition-colors ${
            pathname === tab.href ? "border-accent text-accent-strong" : "border-transparent text-ink-2 hover:text-ink"
          }`}
        >
          {t(tab.label)}
        </Link>
      ))}
    </nav>
  );
}

const pill = "inline-flex shrink-0 items-center whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-semibold";

export function DayStatusBadge({ status, late = false }: { status: HrDayStatus; late?: boolean }) {
  const t = useT();
  if (status === "Present" && late) return <span className={`${pill} bg-warn-soft text-warn`}>{t("Опоздал")}</span>;
  return <span className={`${pill} ${dayStatusTone[status]}`}>{t(dayStatusLabel[status])}</span>;
}

/** Итог дня одной строкой: статус, отклонения, документ. */
export function DayBadges({ day }: { day: HrDay }) {
  const t = useT();
  return (
    <span className="inline-flex flex-wrap items-center gap-1.5">
      <DayStatusBadge status={day.status} late={day.late} />
      {day.earlyLeave && <span className={`${pill} bg-warn-soft text-warn`}>{t("Ранний уход")}</span>}
      {day.noExit && <span className={`${pill} bg-muted text-ink-2`}>{t("Нет ухода")}</span>}
      {day.workedOnDayOff && <span className={`${pill} bg-accent/10 text-accent-strong`}>{t("В нерабочий день")}</span>}
      {day.leaveName && <span className={`${pill} bg-violet/15 text-violet-light`}>{day.leaveName}</span>}
    </span>
  );
}

export function EmployeeStatusBadge({ status, missing }: { status: string; missing?: boolean }) {
  const t = useT();
  return (
    <span className="inline-flex flex-wrap items-center gap-1.5">
      <span className={`${pill} ${status === "W" ? "bg-ok-soft text-ok" : "bg-muted text-ink-2"}`}>{t(employeeStatusLabel[status] ?? status)}</span>
      {missing && <span className={`${pill} bg-warn-soft text-warn`} title={t("Не пришёл в последней выгрузке Verifix — запись сохранена")}>{t("нет в Verifix")}</span>}
    </span>
  );
}

export function SyncStatusBadge({ status }: { status: HrSyncStatus }) {
  const t = useT();
  return <span className={`${pill} ${syncStatusTone[status]}`}>{t(syncStatusLabel[status])}</span>;
}

export function DayCode({ code }: { code: string }) {
  return <span className={`inline-grid h-6 min-w-7 place-items-center rounded px-1 text-[11px] font-semibold ${dayCodeTone(code)}`}>{code}</span>;
}

export const control =
  "h-9 rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";

type FilterField =
  | { kind: "date"; name: string; label: string; value?: string }
  | { kind: "select"; name: string; label: string; value?: string; options: { value: string; label: string }[]; all?: string }
  | { kind: "search"; name: string; label: string; value?: string; placeholder?: string };

/**
 * Фильтры страницы — обычная GET-форма: адрес страницы хранит фильтр (можно отправить ссылку), выбор в списке или дате применяется сразу,
 * поиск — по Enter или кнопке.
 */
export function HrFilters({ fields, keep = {} }: { fields: FilterField[]; keep?: Record<string, string | undefined> }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const search = useSearchParams();
  const form = useRef<HTMLFormElement>(null);

  const apply = () => {
    if (!form.current) return;
    const data = new FormData(form.current);
    const params = new URLSearchParams();
    for (const [key, value] of data.entries()) if (typeof value === "string" && value !== "") params.set(key, value);
    router.push(`${pathname}${params.size > 0 ? `?${params}` : ""}`);
  };

  return (
    <form
      ref={form}
      className="mb-4 flex flex-wrap items-end gap-2"
      onSubmit={(e) => {
        e.preventDefault();
        apply();
      }}
    >
      {Object.entries(keep).map(([k, v]) => (v ? <input key={k} type="hidden" name={k} value={v} /> : null))}
      {fields.map((f) => (
        <label key={f.name} className="flex min-w-0 flex-col gap-1 text-xs text-ink-3">
          {t(f.label)}
          {f.kind === "date" ? (
            <input type="date" name={f.name} defaultValue={f.value ?? search.get(f.name) ?? ""} onChange={apply} className={control} />
          ) : f.kind === "select" ? (
            <select name={f.name} defaultValue={f.value ?? search.get(f.name) ?? ""} onChange={apply} className={`${control} max-w-64`}>
              {f.all !== undefined && <option value="">{t(f.all)}</option>}
              {f.options.map((o) => (
                <option key={o.value} value={o.value}>
                  {t(o.label)}
                </option>
              ))}
            </select>
          ) : (
            <input type="search" name={f.name} defaultValue={f.value ?? search.get(f.name) ?? ""} placeholder={f.placeholder ? t(f.placeholder) : undefined} className={`${control} w-56 max-w-full`} />
          )}
        </label>
      ))}
      <button type="submit" className="h-9 rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
        {t("Показать")}
      </button>
    </form>
  );
}

export function Pager({ page, pageSize, total }: { page: number; pageSize: number; total: number }) {
  const t = useT();
  const pathname = usePathname();
  const search = useSearchParams();
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (pages <= 1) return null;
  const href = (p: number) => {
    const params = new URLSearchParams(search.toString());
    params.set("page", String(p));
    return `${pathname}?${params}`;
  };
  const link = "inline-flex h-8 items-center rounded-lg border border-line bg-surface px-3 text-sm text-ink hover:bg-muted";
  return (
    <div className="mt-3 flex items-center justify-between gap-2 text-sm text-ink-3">
      <span>{t("Стр. {page} из {pages} · всего {total}", { page, pages, total })}</span>
      <span className="flex gap-2">
        {page > 1 && (
          <Link href={href(page - 1)} className={link}>
            {t("Назад")}
          </Link>
        )}
        {page < pages && (
          <Link href={href(page + 1)} className={link}>
            {t("Дальше")}
          </Link>
        )}
      </span>
    </div>
  );
}
