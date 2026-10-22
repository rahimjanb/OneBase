"use client";

import { Fragment, useCallback, useEffect, useState } from "react";
import { Eye, Loader2, RefreshCw, Send } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Note } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import {
  deliveryStatus,
  reportKinds,
  reportsApi,
  triggerLabel,
  type ReportDelivery,
  type ReportKind,
  type ReportOverview,
  type ReportPreview,
  type ReportPreviewInput,
  type ReportSendResult,
} from "@/lib/telegramReports";

const when = (iso: string | null) => (iso ? new Date(iso).toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" }) : "—");

/**
 * Предпросмотр картинок и тестовая отправка. По умолчанию — демонстрационные данные («Супервайзер 1», «Агент 1», вымышленные цифры, на
 * картинке «ТЕСТОВЫЕ ДАННЫЕ»); реальные — только явно, по региону или команде в вашей области и только получателю с правами.
 */
export function ReportPreviewPanel({ data }: { data: ReportOverview }) {
  const t = useT();
  const [kind, setKind] = useState<ReportKind>("Morning");
  const [mode, setMode] = useState<"demo" | "real">("demo");
  const [region, setRegion] = useState("");
  const [agents, setAgents] = useState(8);
  const [regionId, setRegionId] = useState<string>(data.regions[0]?.id ?? "");
  const [supervisorId, setSupervisorId] = useState<number | null>(data.regions[0]?.supervisors[0]?.id ?? null);
  const [metric, setMetric] = useState<"sum" | "kg">(data.schedules.find((s) => s.kind === "Morning")?.metric ?? "sum");
  const [language, setLanguage] = useState<"ru" | "uz">(data.settings.language);
  const [chatId, setChatId] = useState("");
  const [preview, setPreview] = useState<ReportPreview | null>(null);
  const [result, setResult] = useState<ReportSendResult | null>(null);
  const [busy, setBusy] = useState<"preview" | "test" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const canReal = data.canUseSales && data.regions.length > 0;
  const realRegion = data.regions.find((r) => r.id === regionId);

  const input = (): ReportPreviewInput => ({
    kind,
    mode,
    region: mode === "demo" ? region.trim() || null : null,
    agents: mode === "demo" ? agents : null,
    subscriptionId: null,
    regionId: mode === "real" ? regionId || null : null,
    supervisorId: mode === "real" ? supervisorId : null,
    metric,
    language,
  });

  const show = async () => {
    setBusy("preview");
    setError(null);
    setResult(null);
    try {
      setPreview(await reportsApi.preview(input()));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  const test = async () => {
    const target = data.chats.find((c) => c.id === chatId);
    if (mode === "real" && !window.confirm(t("Отправить реальные показатели в «{chat}»?", { chat: target?.title ?? "" }))) return;
    setBusy("test");
    setError(null);
    setResult(null);
    try {
      setResult(await reportsApi.test(input(), chatId));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="grid gap-6 xl:grid-cols-[420px_minmax(0,1fr)]">
      <div>
        <span className="mb-1.5 block text-sm font-medium text-ink">{t("Отчёт")}</span>
        <div className="grid grid-cols-3 gap-1 rounded-lg bg-muted p-1">
          {reportKinds.map((k) => (
            <button
              key={k.key}
              type="button"
              onClick={() => setKind(k.key)}
              className={`rounded-md px-2 py-1.5 text-xs font-semibold ${kind === k.key ? "bg-surface text-ink shadow-sm" : "text-ink-3 hover:text-ink"}`}
            >
              {t(k.label)}
            </button>
          ))}
        </div>

        <span className="mb-1.5 mt-4 block text-sm font-medium text-ink">{t("Данные")}</span>
        <div className="space-y-2">
          <label className="flex items-start gap-2 text-sm text-ink">
            <input type="radio" checked={mode === "demo"} onChange={() => setMode("demo")} className="mt-0.5 size-4 accent-[var(--color-accent)]" />
            <span>
              {t("Демонстрационные")}
              <span className="block text-xs text-ink-3">{t("Вымышленные имена и цифры, на картинке «ТЕСТОВЫЕ ДАННЫЕ»")}</span>
            </span>
          </label>
          <label className={`flex items-start gap-2 text-sm ${canReal ? "text-ink" : "text-ink-3"}`}>
            <input type="radio" checked={mode === "real"} onChange={() => setMode("real")} disabled={!canReal} className="mt-0.5 size-4 accent-[var(--color-accent)]" />
            <span>
              {t("Проверка на реальных данных")}
              <span className="block text-xs text-ink-3">{t("Регион и супервайзер из «Продаж»; на картинке «ПРОВЕРКА НА РЕАЛЬНЫХ ДАННЫХ»")}</span>
            </span>
          </label>
        </div>

        {mode === "demo" ? (
          <div className="mt-4 grid grid-cols-2 gap-3">
            <label className="col-span-2 block">
              <span className="mb-1 block text-xs font-medium text-ink-2">{t("Регион")}</span>
              <input className={field} value={region} onChange={(e) => setRegion(e.target.value)} placeholder={t("Ташкентский регион")} maxLength={80} />
            </label>
            <label className="col-span-2 block">
              <span className="mb-1 block text-xs font-medium text-ink-2">{t("Агентов")}</span>
              <input type="number" min={1} max={25} className={field} value={agents} onChange={(e) => setAgents(Math.min(25, Math.max(1, Number(e.target.value) || 1)))} />
            </label>
          </div>
        ) : (
          <div className="mt-4 grid gap-3">
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-ink-2">{t("Регион")}</span>
              <select
                className={field}
                value={regionId}
                onChange={(e) => (setRegionId(e.target.value), setSupervisorId(data.regions.find((r) => r.id === e.target.value)?.supervisors[0]?.id ?? null))}
              >
                {data.regions.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-ink-2">{t("Супервайзер")}</span>
              <select className={field} value={supervisorId ?? ""} onChange={(e) => setSupervisorId(e.target.value ? Number(e.target.value) : null)}>
                {realRegion?.supervisors.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
                <option value="">{realRegion?.supervisorName ? t("СВР из оргструктуры: {name}", { name: realRegion.supervisorName }) : t("Без супервайзера")}</option>
              </select>
            </label>
          </div>
        )}

        <div className="mt-3 grid grid-cols-2 gap-3">
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("Показатель")}</span>
            <select className={field} value={metric} onChange={(e) => setMetric(e.target.value as "sum" | "kg")}>
              <option value="sum">{t("Сумы")}</option>
              <option value="kg">{t("Килограммы")}</option>
            </select>
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("Язык картинок")}</span>
            <select className={field} value={language} onChange={(e) => setLanguage(e.target.value as "ru" | "uz")}>
              <option value="ru">{t("Русский")}</option>
              <option value="uz">{t("Узбекский (латиница)")}</option>
            </select>
          </label>
        </div>

        <button className={`${primary} mt-4`} onClick={show} disabled={busy !== null || (mode === "real" && !regionId)}>
          {busy === "preview" ? <Loader2 className="size-4 animate-spin" /> : <Eye className="size-4" />}
          {t("Предпросмотр")}
        </button>

        <div className="mt-6 border-t border-line pt-4">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Тестовая отправка")}</span>
          <select className={field} value={chatId} onChange={(e) => setChatId(e.target.value)} disabled={!data.settings.accountId}>
            <option value="">{data.settings.accountId ? t("Выберите чат или группу") : t("Сначала выберите аккаунт Telegram")}</option>
            {data.chats.map((c) => (
              <option key={c.id} value={c.id} disabled={!c.canSend}>
                {c.title}
                {` · ${t(c.type === "Group" ? "Группа" : c.type === "Channel" ? "Канал" : "Личный")}`}
              </option>
            ))}
          </select>
          <button className={`${button} mt-3`} onClick={test} disabled={busy !== null || !chatId}>
            {busy === "test" ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
            {t("Отправить тестовый отчёт")}
          </button>
          {!data.telegramEnabled && <p className="mt-2 text-xs text-ink-3">{t("В этой среде Telegram выключен — тестовая отправка вернёт ошибку.")}</p>}
        </div>

        {result && (
          <p className={`mt-4 rounded-lg px-3 py-2 text-sm ${result.status === "sent" ? "bg-ok-soft text-ok" : result.status === "pending" ? "bg-warn-soft text-warn" : "bg-bad-soft text-bad"}`}>
            {result.status === "sent"
              ? t("Отправлено в «{chat}»: {pages} стр.", { chat: result.recipientTitle, pages: result.pages })
              : `${t(deliveryStatus[result.status].label)}: ${t(result.error ?? "")}`}
          </p>
        )}
        {error && <p className="mt-4 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
        <Note>
          {t(
            "Предпросмотр ничего не отправляет. Тест уходит одним сообщением в выбранный чат и записывается в журнал с режимом данных; на демонстрационных картинках всегда стоит «ТЕСТОВЫЕ ДАННЫЕ».",
          )}
        </Note>
      </div>

      <div className="min-w-0">
        {preview ? (
          <div>
            <p className="whitespace-pre-line rounded-lg bg-muted px-3 py-2 text-sm text-ink">{preview.caption}</p>
            <p className="mt-2 text-xs text-ink-3">
              {t("Картинок: {n} · {kb} КБ", { n: preview.pages.length, kb: Math.round(preview.bytes / 1024) })}
            </p>
            {preview.problems.length > 0 && <p className="mt-2 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{preview.problems.join(" ")}</p>}
            <div className="mt-3 flex flex-wrap gap-4">
              {preview.pages.map((src, i) => (
                // eslint-disable-next-line @next/next/no-img-element -- картинка из ответа сервера (data:), оптимизатор Next не нужен
                <img key={i} src={src} alt={t("Страница {n}", { n: i + 1 })} className="w-full max-w-[400px] rounded-xl border border-line shadow-sm" />
              ))}
            </div>
          </div>
        ) : (
          <div className="grid h-full min-h-[240px] place-items-center rounded-xl border border-dashed border-line text-sm text-ink-3">
            {t("Нажмите «Предпросмотр» — картинки появятся здесь.")}
          </div>
        )}
      </div>
    </div>
  );
}

/** Журнал отправок: когда, какой отчёт, кому, регион и команда, режим данных, статус и причина ошибки. */
export function ReportJournal() {
  const t = useT();
  const [rows, setRows] = useState<ReportDelivery[] | null>(null);
  const [kind, setKind] = useState("");
  const [status, setStatus] = useState("");
  const [mode, setMode] = useState("");
  const [open, setOpen] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setRows(await reportsApi.deliveries(kind, status, mode));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [kind, status, mode]);

  useEffect(() => {
    load();
  }, [load]);

  const narrow = field.replace("w-full", "w-auto");
  return (
    <div>
      <div className="flex flex-wrap items-center gap-2">
        <select className={narrow} value={kind} onChange={(e) => setKind(e.target.value)} aria-label={t("Отчёт")}>
          <option value="">{t("Все отчёты")}</option>
          {reportKinds.map((k) => (
            <option key={k.key} value={k.key}>
              {t(k.label)}
            </option>
          ))}
        </select>
        <select className={narrow} value={status} onChange={(e) => setStatus(e.target.value)} aria-label={t("Статус")}>
          <option value="">{t("Все статусы")}</option>
          {Object.entries(deliveryStatus).map(([key, v]) => (
            <option key={key} value={key}>
              {t(v.label)}
            </option>
          ))}
        </select>
        <select className={narrow} value={mode} onChange={(e) => setMode(e.target.value)} aria-label={t("Данные")}>
          <option value="">{t("Все данные")}</option>
          <option value="real">{t("Реальные")}</option>
          <option value="demo">{t("Тестовые")}</option>
        </select>
        <button className={button} onClick={load}>
          <RefreshCw className="size-4" />
          {t("Обновить")}
        </button>
      </div>
      {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      <div className="mt-4 overflow-x-auto">
        {rows === null ? (
          <Loader2 className="size-5 animate-spin text-ink-3" />
        ) : rows.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Отправок пока нет.")}</p>
        ) : (
          <table className="w-full min-w-[900px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">{t("Когда")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Отчёт")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Получатель")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Регион и команда")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Данные")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Статус")}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <Fragment key={r.id}>
                  <tr className="cursor-pointer border-b border-line align-top hover:bg-muted/50" onClick={() => setOpen(open === r.id ? null : r.id)}>
                    <td className="whitespace-nowrap py-2 pr-3 text-ink-2">{when(r.sentAt ?? r.createdAt)}</td>
                    <td className="py-2 pr-3">
                      <span className="block text-ink">{t(reportKinds.find((k) => k.key === r.kind)?.label ?? r.kind)}</span>
                      <span className="text-xs text-ink-3">
                        {new Date(r.period).toLocaleDateString("ru-RU")} · {t(triggerLabel[r.trigger])}
                      </span>
                    </td>
                    <td className="py-2 pr-3 text-ink">{r.recipientTitle}</td>
                    <td className="py-2 pr-3 text-ink-2">{r.scopeTitle}</td>
                    <td className="py-2 pr-3">
                      <span className={r.dataMode === "demo" ? "rounded-full bg-warn-soft px-2 py-0.5 text-xs font-medium text-warn" : "text-xs text-ink-2"}>
                        {r.dataMode === "demo" ? t("Тестовые") : t("Реальные")}
                      </span>
                    </td>
                    <td className="py-2 pr-3">
                      <span className={deliveryStatus[r.status].className}>{t(deliveryStatus[r.status].label)}</span>
                      {r.error && <span className="block max-w-[320px] truncate text-xs text-ink-3">{t(r.error)}</span>}
                    </td>
                  </tr>
                  {open === r.id && (
                    <tr className="border-b border-line bg-muted/30">
                      <td colSpan={6} className="px-3 py-3 text-sm text-ink-2">
                        {r.summary && <p className="text-ink">{r.summary}</p>}
                        {r.error && <p className="mt-1 text-bad">{t(r.error)}</p>}
                        <p className="mt-1 text-xs text-ink-3">
                          {t("Попыток: {n}", { n: r.attempts })}
                          {r.pages > 0 && ` · ${t("картинок: {n}, {kb} КБ", { n: r.pages, kb: Math.round(r.bytes / 1024) })}`}
                          {r.dataAsOf && ` · ${t("данные Linko на {time}", { time: when(r.dataAsOf) })}`}
                          {r.nextAttemptAt && r.status === "pending" && ` · ${t("повтор в {time}", { time: when(r.nextAttemptAt) })}`}
                          {r.by && ` · ${t("отправил {name}", { name: r.by })}`}
                        </p>
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
