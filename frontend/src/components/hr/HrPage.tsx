import Link from "next/link";
import { redirect } from "next/navigation";
import { Alert } from "@/components/sales/bits";
import { PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { dayLabel, momentLabel, VERIFIX_JOURNAL, VERIFIX_SETTINGS, type HrContext, type HrSyncBrief } from "@/lib/hr";
import { apiGet } from "@/lib/server-api";

/** Доступ к разделу HR (решает backend): нет доступа — страница «Нет доступа» (администратору интеграций — настройки Verifix). */
export async function loadHr(path: string): Promise<HrContext> {
  const ctx = await apiGet<HrContext>("/api/hr/context", path);
  if (!ctx.hasAccess) redirect(ctx.managesIntegration ? VERIFIX_SETTINGS : `/no-access?from=${encodeURIComponent(path)}`);
  return ctx;
}

export async function HrHeader({ title, subtitle, crumb, actions }: { title: string; subtitle?: string; crumb?: { label: string; href?: string }[]; actions?: React.ReactNode }) {
  const t = await getT();
  return (
    <PageHeader
      title={t(title)}
      subtitle={subtitle ? t(subtitle) : undefined}
      breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "HR", href: "/hr" }, ...(crumb ?? [{ label: t(title) }])]}
      actions={actions}
    />
  );
}

/**
 * Насколько свежи данные Verifix: подключение, последняя синхронизация, до какого момента загружены отметки и за какие дни табель.
 * Без этого «отсутствует» нельзя отличить от «данные ещё не пришли».
 */
export async function SyncBanner({ sync, manages }: { sync: HrSyncBrief; manages: boolean }) {
  const t = await getT();
  if (!sync.configured) {
    return (
      <Alert tone="warn" className="mb-4">
        <b>{t("Verifix не подключён.")}</b>{" "}
        {manages ? (
          <>
            {t("Задайте client_id и client_secret и проверьте подключение:")}{" "}
            <Link href={VERIFIX_SETTINGS} className="text-accent-strong hover:underline">
              {t("Настройки → Интеграции → HR → Verifix")}
            </Link>
          </>
        ) : (
          t("Данные HR появятся, когда администратор подключит Verifix.")
        )}
      </Alert>
    );
  }

  if (!sync.enabled) {
    return (
      <Alert tone="warn" className="mb-4">
        <b>{t("Интеграция Verifix выключена")}</b> — {t("данные не обновляются.")}
      </Alert>
    );
  }

  const failed = sync.lastStatus === "Failed" || sync.lastStatus === "Partial";
  return (
    <Alert tone={failed ? "warn" : sync.lastSuccessAt ? "info" : "warn"} className="mb-4">
      {sync.running && <b>{t("Идёт синхронизация с Verifix…")} </b>}
      {sync.lastSuccessAt ? (
        <>
          {t("Данные Verifix: синхронизация {time}", { time: momentLabel(sync.lastSuccessAt) })}
          {sync.tracksThrough && <> · {t("отметки загружены до {time}", { time: `${dayLabel(sync.tracksThrough)} ${sync.tracksThrough.slice(11, 16)}` })}</>}
          {sync.timesheetFrom && sync.timesheetTo && <> · {t("табель с {from} по {to}", { from: dayLabel(sync.timesheetFrom), to: dayLabel(sync.timesheetTo) })}</>}
        </>
      ) : (
        <b>{t("Синхронизация ещё не выполнялась — данных пока нет.")}</b>
      )}
      {failed && (
        <span className="mt-1 block text-ink-2">
          {t("Последний запуск: {status}.", { status: t(sync.lastStatus === "Failed" ? "ошибка" : "частично") })} {sync.lastMessage ? t(sync.lastMessage) : ""}{" "}
          {manages && (
            <Link href={VERIFIX_JOURNAL} className="text-accent-strong hover:underline">
              {t("Журнал синхронизации")}
            </Link>
          )}
        </span>
      )}
    </Alert>
  );
}

/** Пустое состояние таблицы. */
export async function Empty({ text }: { text: string }) {
  const t = await getT();
  return <p className="rounded-xl border border-dashed border-line bg-surface/60 px-6 py-10 text-center text-sm text-ink-3">{t(text)}</p>;
}
