"use client";

import { CircleAlert, CircleCheck } from "lucide-react";
import { button } from "@/components/ai/form";
import { useT } from "@/i18n/client";
import { isPdfJob, reportRoles, runStatus, telegramStatus, type ReportOverview, type TelegramStatus } from "@/lib/telegramReports";
import { RoleChip, when } from "./TelegramReportsPreview";

export type ReportsTab = "overview" | "schedule" | "recipients" | "preview" | "pdf" | "dailyplan" | "history";

/** Данные Linko старше этого — «устарели» (как у еженедельного отчёта). */
const StaleHours = 6;

function Card({ title, action, children }: { title: string; action?: React.ReactNode; children: React.ReactNode }) {
  const t = useT();
  return (
    <div className="rounded-xl border border-line">
      <div className="flex items-center justify-between gap-3 border-b border-line px-4 py-3">
        <h3 className="font-semibold text-ink">{t(title)}</h3>
        {action}
      </div>
      <div className="p-4 text-sm">{children}</div>
    </div>
  );
}

function Check({ ok, label, detail }: { ok: boolean; label: string; detail?: string }) {
  const t = useT();
  return (
    <li className="flex items-start gap-2">
      {ok ? <CircleCheck className="mt-0.5 size-4 shrink-0 text-ok" /> : <CircleAlert className="mt-0.5 size-4 shrink-0 text-warn" />}
      <span>
        <span className="text-ink">{t(label)}</span>
        {detail && <span className="block text-xs text-ink-3">{detail}</span>}
      </span>
    </li>
  );
}

/**
 * «Обзор» автоотчётов: готово ли к отправке (аккаунт, Telegram в этой среде, право на продажи, свежесть заказов и визитов Linko),
 * ближайшие и последние запуски, получатели по ролям и привязке Telegram, регионы без СВ-получателя, состояние еженедельного PDF.
 */
export function ReportsOverviewPanel({ data, onTab }: { data: ReportOverview; onTab: (tab: ReportsTab) => void }) {
  const t = useT();
  const now = Date.now();
  const account = data.accounts.find((a) => a.id === data.settings.accountId);
  const fresh = (iso: string | null) => !!iso && now - new Date(iso).getTime() < StaleHours * 3_600_000;
  const linko = (iso: string | null) => (iso ? t("обновлены {time}", { time: when(iso) }) : t("ни разу не обновлялись"));

  const next = data.jobs.filter((j) => j.enabled && j.nextRunAt).sort((a, b) => a.nextRunAt!.localeCompare(b.nextRunAt!)).slice(0, 6);
  const last = data.jobs.filter((j) => j.lastRun).sort((a, b) => b.lastRun!.startedAt.localeCompare(a.lastRun!.startedAt)).slice(0, 6);

  const enabled = data.recipients.filter((r) => r.enabled);
  const problems = enabled.filter((r) => r.problem).length;
  const byStatus = Object.keys(telegramStatus).map((s) => ({ key: s as TelegramStatus, n: enabled.filter((r) => r.telegramStatus === s).length })).filter((x) => x.n > 0);
  const svRegions = new Set(enabled.filter((r) => r.role === "sv").map((r) => r.regionId));
  const pdfJob = data.jobs.find(isPdfJob);
  const pdfRegions = new Set(enabled.filter((r) => r.role === "sv" && pdfJob && r.reports.includes(pdfJob.code)).map((r) => r.regionId));
  const noSv = data.regions.filter((r) => !svRegions.has(r.id));
  const noPdf = data.regions.filter((r) => !pdfRegions.has(r.id));
  const agents = data.regions.reduce((n, r) => n + r.agents.length, 0);
  const tpRecipients = new Set(enabled.filter((r) => r.role === "tp").map((r) => r.agentId)).size;
  const formulasChanged = JSON.stringify(data.weeklyPdf.formulas) !== JSON.stringify(data.weeklyPdf.defaults);
  const names = (list: { name: string }[]) => list.slice(0, 8).map((r) => r.name).join(", ") + (list.length > 8 ? ` ${t("и ещё {n}", { n: list.length - 8 })}` : "");

  return (
    <div className="grid gap-4 lg:grid-cols-2 2xl:grid-cols-3">
      <Card title="Готовность">
        <ul className="space-y-2">
          <Check
            ok={!!account && account.status === "Connected"}
            label="Аккаунт Telegram"
            detail={account ? `${account.name}${account.username ? ` · @${account.username}` : ""}${account.status !== "Connected" ? ` · ${t("не подключён")}` : ""}` : t("не выбран — выберите выше")}
          />
          <Check ok={data.telegramEnabled} label="Отправка из этой среды" detail={data.telegramEnabled ? t("включена") : t("Telegram выключен здесь — отправляет сервер OneBase")} />
          <Check ok={data.canUseSales} label="Данные продаж (sales.read)" detail={data.canUseSales ? t("есть") : t("нет права — отчёты на реальных данных недоступны")} />
          <Check ok={fresh(data.freshness.orders)} label="Заказы Linko" detail={linko(data.freshness.orders)} />
          <Check ok={fresh(data.freshness.visits)} label="Визиты Linko" detail={linko(data.freshness.visits)} />
        </ul>
      </Card>

      <Card
        title="Ближайшие запуски"
        action={
          <button className={button} onClick={() => onTab("schedule")}>
            {t("Расписание")}
          </button>
        }
      >
        {next.length === 0 ? (
          <p className="text-ink-3">{t("Ни один запуск не включён — по расписанию ничего не уходит.")}</p>
        ) : (
          <ul className="space-y-2">
            {next.map((j) => (
              <li key={j.code} className="flex justify-between gap-3">
                <span className="min-w-0">
                  <span className="block truncate text-ink">
                    {t(j.reportTitle)} · {j.time}
                  </span>
                  <span className="text-xs text-ink-3">
                    {t(j.slotTitle)} · {t("получателей: {n}", { n: j.recipients })}
                  </span>
                </span>
                <span className="shrink-0 text-xs text-ink-2">{when(j.nextRunAt)}</span>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card
        title="Последние запуски"
        action={
          <button className={button} onClick={() => onTab("history")}>
            {t("История")}
          </button>
        }
      >
        {last.length === 0 ? (
          <p className="text-ink-3">{t("Запусков по расписанию ещё не было.")}</p>
        ) : (
          <ul className="space-y-2">
            {last.map((j) => (
              <li key={j.code} className="flex justify-between gap-3">
                <span className="min-w-0">
                  <span className="block truncate text-ink">
                    {t(j.reportTitle)} · {j.time}
                  </span>
                  <span className="text-xs text-ink-3">
                    {when(j.lastRun!.startedAt)}
                    {j.lastRun!.planned > 0 && ` · ${t("отправлено {sent} из {planned}", { sent: j.lastRun!.sent, planned: j.lastRun!.planned })}`}
                  </span>
                </span>
                <span className={`shrink-0 text-xs font-medium ${runStatus[j.lastRun!.status].className}`}>{t(runStatus[j.lastRun!.status].label)}</span>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card
        title="Получатели"
        action={
          <button className={button} onClick={() => onTab("recipients")}>
            {t("Получатели")}
          </button>
        }
      >
        <div className="flex flex-wrap gap-4">
          {reportRoles.map((r) => (
            <span key={r.key} className="flex items-center gap-2">
              <RoleChip role={r.key} />
              <span className="text-ink">
                {enabled.filter((x) => x.role === r.key).length}
                <span className="text-ink-3"> / {data.recipients.filter((x) => x.role === r.key).length}</span>
              </span>
            </span>
          ))}
        </div>
        <p className="mt-2 text-xs text-ink-3">{t("Включённые / все.")}</p>
        {byStatus.length > 0 && (
          <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-xs">
            {byStatus.map((s) => (
              <li key={s.key} className={telegramStatus[s.key].className}>
                {t(telegramStatus[s.key].label)}: {s.n}
              </li>
            ))}
          </ul>
        )}
        {problems > 0 && <p className="mt-3 rounded-lg bg-warn-soft px-3 py-2 text-xs text-warn">{t("С проблемой: {n} — отчёт им сейчас не уйдёт.", { n: problems })}</p>}
      </Card>

      <Card title="Покрытие регионов">
        <ul className="space-y-3">
          <li>
            <span className="text-ink">{t("Регионы без СВ-получателя: {n} из {total}", { n: noSv.length, total: data.regions.length })}</span>
            {noSv.length > 0 && <span className="block text-xs text-ink-3">{names(noSv)}</span>}
          </li>
          <li>
            <span className="text-ink">{t("Регионы без СВ-получателя PDF: {n} из {total}", { n: noPdf.length, total: data.regions.length })}</span>
            {noPdf.length > 0 && noPdf.length < data.regions.length && <span className="block text-xs text-ink-3">{names(noPdf)}</span>}
          </li>
          <li className="text-ink">{t("ТП-получателей: {n} из {total} ТП в «Продажах»", { n: tpRecipients, total: agents })}</li>
        </ul>
      </Card>

      <Card
        title="Еженедельный PDF"
        action={
          <button className={button} onClick={() => onTab("pdf")}>
            {t("Отчёты PDF")}
          </button>
        }
      >
        <ul className="space-y-2">
          {pdfJob && (
            <Check
              ok={pdfJob.enabled}
              label="Отправка по расписанию"
              detail={pdfJob.enabled ? (pdfJob.nextRunAt ? t("следующая: {time}", { time: when(pdfJob.nextRunAt) }) : t("включена")) : t("выключена — включите на вкладке «Расписание»")}
            />
          )}
          {pdfJob && (
            <li className="text-ink-2">
              {t("Запуск: {time}, дни — {days}; получателей: {n}", {
                time: pdfJob.time,
                days: pdfJob.days.length === 1 && pdfJob.days[0] === 1 ? t("понедельник") : pdfJob.days.length,
                n: pdfJob.recipients,
              })}
            </li>
          )}
          <li className="text-ink-2">
            {data.weeklyPdf.requireCompleteData ? t("Только при полных данных — включено.") : t("Только при полных данных — выключено: неполный PDF уйдёт с пометкой.")}
          </li>
          <li className="text-ink-2">{formulasChanged ? t("Формулы целей изменены — не как в образцах.") : t("Формулы целей — как в образцах.")}</li>
        </ul>
      </Card>
    </div>
  );
}
