"use client";

import { Fragment, useCallback, useEffect, useMemo, useState } from "react";
import { ChevronLeft, ChevronRight, Eye, Loader2, RefreshCw, RotateCcw, Send } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Note } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { chatTypeLabel } from "@/lib/telegram";
import {
  deliveryStatus,
  reportRoles,
  reportTypes,
  reportsApi,
  roleOf,
  runStatus,
  triggerLabel,
  type ReportHistory,
  type ReportHistoryQuery,
  type ReportOverview,
  type ReportPreview,
  type ReportPreviewInput,
  type ReportRole,
  type ReportSendResult,
} from "@/lib/telegramReports";

export const when = (iso: string | null) => (iso ? new Date(iso).toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" }) : "—");

const day = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString("ru-RU");

/** Цветная метка роли: РМ — синий, СВ — зелёный, ТП — оранжевый (как полоса на картинке). */
export function RoleChip({ role }: { role: string }) {
  const t = useT();
  const r = roleOf(role);
  return <span className={`inline-flex h-5 items-center rounded-full px-2 text-[11px] font-bold ${r.className}`}>{t(r.label)}</span>;
}

/** Область получателя: РМ — направление или вся страна; СВ — регион и супервайзер; ТП — регион и торговый представитель. */
export type Scope = { role: ReportRole; directionId: string | null; regionId: string | null; supervisorId: number | null; agentId: number | null };

export const defaultScope = (data: ReportOverview, role: ReportRole = "sv"): Scope => {
  const region = data.regions[0];
  return {
    role,
    directionId: null,
    regionId: role === "rm" ? null : (region?.id ?? null),
    supervisorId: role === "sv" ? (region?.supervisors[0]?.id ?? null) : null,
    agentId: role === "tp" ? (region?.agents[0]?.id ?? null) : null,
  };
};

export function ScopePicker({ data, scope, onChange }: { data: ReportOverview; scope: Scope; onChange: (s: Scope) => void }) {
  const t = useT();
  const region = data.regions.find((r) => r.id === scope.regionId);
  const direction = data.directions.find((d) => d.id === scope.directionId);
  const setRole = (role: ReportRole) => onChange({ ...defaultScope(data, role), regionId: role === "rm" ? null : (scope.regionId ?? data.regions[0]?.id ?? null) });
  const pickRegion = (regionId: string) => {
    const r = data.regions.find((x) => x.id === regionId);
    onChange({ ...scope, regionId, supervisorId: scope.role === "sv" ? (r?.supervisors[0]?.id ?? null) : null, agentId: scope.role === "tp" ? (r?.agents[0]?.id ?? null) : null });
  };

  return (
    <div className="space-y-3">
      <div>
        <span className="mb-1.5 block text-sm font-medium text-ink">{t("Роль")}</span>
        <div className="grid grid-cols-3 gap-1 rounded-lg bg-muted p-1">
          {reportRoles.map((r) => (
            <button
              key={r.key}
              type="button"
              onClick={() => setRole(r.key)}
              title={t(r.hint)}
              className={`rounded-md px-2 py-1.5 text-xs font-semibold ${scope.role === r.key ? "bg-surface text-ink shadow-sm" : "text-ink-3 hover:text-ink"}`}
            >
              {t(r.label)}
            </button>
          ))}
        </div>
        <span className="mt-1 block text-xs text-ink-3">{t(roleOf(scope.role).hint)}</span>
      </div>

      {scope.role === "rm" ? (
        <label className="block">
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Направление")}</span>
          <select className={field} value={scope.directionId ?? ""} onChange={(e) => onChange({ ...scope, directionId: e.target.value || null })}>
            <option value="">{t("Вся страна")}</option>
            {data.directions.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
                {d.manager ? ` · ${d.manager}` : ""}
              </option>
            ))}
          </select>
          <span className="mt-1 block text-xs text-ink-3">
            {direction
              ? t("Регионов в направлении: {n}", { n: data.regions.filter((r) => r.directionId === direction.id).length })
              : t("Все регионы: {n}", { n: data.regions.length })}
          </span>
        </label>
      ) : (
        <>
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("Регион")}</span>
            <select className={field} value={scope.regionId ?? ""} onChange={(e) => pickRegion(e.target.value)}>
              {!region && <option value="">{t("Выберите регион")}</option>}
              {data.regions.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                </option>
              ))}
            </select>
            {region?.manager && <span className="mt-1 block text-xs text-ink-3">{t("РМ")}: {region.manager}</span>}
          </label>
          {scope.role === "sv" ? (
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-ink-2">{t("Супервайзер")}</span>
              <select
                className={field}
                value={scope.supervisorId ?? ""}
                onChange={(e) => onChange({ ...scope, supervisorId: e.target.value ? Number(e.target.value) : null })}
                disabled={!region}
              >
                {region?.supervisors.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
                <option value="">{region?.supervisorName ? t("СВР из оргструктуры: {name}", { name: region.supervisorName }) : t("Без супервайзера")}</option>
              </select>
              <span className="mt-1 block text-xs text-ink-3">{t("ТП в отчёте — команда региона в «Продажах»: {n}", { n: region?.agents.length ?? 0 })}</span>
            </label>
          ) : (
            <label className="block">
              <span className="mb-1 block text-xs font-medium text-ink-2">{t("Торговый представитель")}</span>
              <select className={field} value={scope.agentId ?? ""} onChange={(e) => onChange({ ...scope, agentId: e.target.value ? Number(e.target.value) : null })} disabled={!region}>
                {!scope.agentId && <option value="">{t("Выберите ТП")}</option>}
                {region?.agents.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name}
                  </option>
                ))}
              </select>
            </label>
          )}
        </>
      )}
    </div>
  );
}

/** Код запуска → подпись: «1. Дневной план · 13:00 — промежуточный итог». */
function JobSelect({ data, value, role, onChange }: { data: ReportOverview; value: string; role: ReportRole; onChange: (code: string) => void }) {
  const t = useT();
  return (
    <select className={field} value={value} onChange={(e) => onChange(e.target.value)}>
      {reportTypes.map((type) => (
        <optgroup key={type.key} label={t(type.title)}>
          {data.jobs
            .filter((j) => j.report === type.key)
            .map((j) => (
              <option key={j.code} value={j.code} disabled={!j.roles.includes(role)}>
                {j.time} — {t(j.slotTitle)}
                {!j.roles.includes(role) ? ` (${t("только ТП")})` : ""}
              </option>
            ))}
        </optgroup>
      ))}
    </select>
  );
}

/**
 * Предпросмотр и тест на реальных данных OneBase: запуск, роль и область (или готовый получатель), день и время формирования, язык.
 * Предпросмотр ничего не отправляет; тест уходит только в выбранный чат с пометкой «ТЕСТОВАЯ ОТПРАВКА».
 */
export function ReportPreviewPanel({ data }: { data: ReportOverview }) {
  const t = useT();
  const [job, setJob] = useState("daily.midday");
  const [recipientId, setRecipientId] = useState("");
  const [scope, setScope] = useState<Scope>(() => defaultScope(data));
  const [date, setDate] = useState("");
  const [time, setTime] = useState("");
  const [language, setLanguage] = useState<"ru" | "uz">(data.settings.language);
  const [chatId, setChatId] = useState("");
  const [preview, setPreview] = useState<ReportPreview | null>(null);
  const [result, setResult] = useState<ReportSendResult | null>(null);
  const [busy, setBusy] = useState<"preview" | "test" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const recipient = data.recipients.find((r) => r.id === recipientId);
  const role = recipient?.role ?? scope.role;
  const info = data.jobs.find((j) => j.code === job);
  const fits = !!info?.roles.includes(role);

  const input = (): ReportPreviewInput => ({
    job,
    role: recipient ? null : scope.role,
    directionId: recipient ? null : scope.directionId,
    regionId: recipient ? null : scope.regionId,
    supervisorId: recipient ? null : scope.supervisorId,
    agentId: recipient ? null : scope.agentId,
    recipientId: recipient?.id ?? null,
    date: date || null,
    time: time || null,
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
    const chat = data.chats.find((c) => c.id === chatId);
    if (!window.confirm(t("Отправить тестовый отчёт на реальных данных в «{chat}»? Получателям он не уйдёт.", { chat: chat?.title ?? "" }))) return;
    setBusy("test");
    setError(null);
    setResult(null);
    try {
      const sent = await reportsApi.test(input(), chatId);
      if ("conflict" in sent) throw new Error(sent.conflict);
      setResult(sent);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="grid gap-6 xl:grid-cols-[420px_minmax(0,1fr)]">
      <div>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Отчёт и запуск")}</span>
          <JobSelect data={data} value={job} role={role} onChange={setJob} />
        </label>

        <label className="mt-4 block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Для кого")}</span>
          <select className={field} value={recipientId} onChange={(e) => setRecipientId(e.target.value)}>
            <option value="">{t("Выбрать роль и область")}</option>
            {data.recipients.map((r) => (
              <option key={r.id} value={r.id}>
                {t(roleOf(r.role).label)} · {r.name}
              </option>
            ))}
          </select>
        </label>
        {!recipient && (
          <div className="mt-3">
            <ScopePicker data={data} scope={scope} onChange={setScope} />
          </div>
        )}

        <div className="mt-3 grid grid-cols-3 gap-3">
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("День")}</span>
            <input type="date" className={field} value={date} onChange={(e) => setDate(e.target.value)} />
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("Срез на")}</span>
            <input type="time" className={field} value={time} onChange={(e) => setTime(e.target.value)} />
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("Язык")}</span>
            <select className={field} value={language} onChange={(e) => setLanguage(e.target.value as "ru" | "uz")}>
              <option value="ru">{t("Русский")}</option>
              <option value="uz">{t("Узбекский")}</option>
            </select>
          </label>
        </div>
        <p className="mt-1 text-xs text-ink-3">{t("Пусто — сегодня и сейчас; прошлый день — на время запуска.")}</p>
        {!fits && <p className="mt-2 rounded-lg bg-warn-soft px-3 py-2 text-xs text-warn">{t("Этот запуск — не для выбранной роли (напоминание 17:00 — только ТП).")}</p>}

        <button className={`${primary} mt-4`} onClick={show} disabled={busy !== null || !data.canUseSales || !fits}>
          {busy === "preview" ? <Loader2 className="size-4 animate-spin" /> : <Eye className="size-4" />}
          {t("Предпросмотр")}
        </button>

        <div className="mt-6 border-t border-line pt-4">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Тестовая отправка")}</span>
          <select className={field} value={chatId} onChange={(e) => setChatId(e.target.value)} disabled={!data.settings.accountId}>
            <option value="">{data.settings.accountId ? t("Выберите чат или группу") : t("Сначала выберите аккаунт Telegram")}</option>
            {data.chats.map((c) => (
              <option key={c.id} value={c.id} disabled={!c.canSend}>
                {c.title} · {t(chatTypeLabel[c.type])}
                {c.linkedUser ? ` · ${c.linkedUser}` : ""}
              </option>
            ))}
          </select>
          <button className={`${button} mt-3`} onClick={test} disabled={busy !== null || !chatId || !data.canUseSales || !fits}>
            {busy === "test" ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
            {t("Отправить тест в этот чат")}
          </button>
          {!data.telegramEnabled && <p className="mt-2 text-xs text-ink-3">{t("В этой среде Telegram выключен — тестовая отправка вернёт ошибку.")}</p>}
        </div>

        {result && (
          <p className={`mt-4 rounded-lg px-3 py-2 text-sm ${result.status === "sent" ? "bg-ok-soft text-ok" : result.status === "skipped" ? "bg-muted text-ink-2" : "bg-bad-soft text-bad"}`}>
            {result.status === "sent"
              ? t("Отправлено в «{chat}»: {pages} стр.", { chat: result.recipientTitle, pages: result.pages })
              : `${t(deliveryStatus[result.status].label)}: ${t(result.error ?? "")}`}
          </p>
        )}
        {error && <p className="mt-4 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
        <Note>
          {t(
            "Данные — только реальные данные OneBase (заказы, визиты и планы Linko, оргструктура «Продаж»). Область проверяет сервер: ТП видит только свои цифры, СВ — свой регион, РМ — регионы направления. Тест записывается в историю и не мешает отправке по расписанию.",
          )}
        </Note>
      </div>

      <div className="min-w-0">
        {preview ? (
          <div>
            {!preview.applies && (
              <p className="mb-2 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">
                {t("По расписанию не уйдёт: {reason}", { reason: t(preview.skipReason ?? "") })}
              </p>
            )}
            {preview.incomplete && <p className="mb-2 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">{t("Данные неполные — на картинке есть пометка.")}</p>}
            <p className="whitespace-pre-line rounded-lg bg-muted px-3 py-2 text-sm text-ink">{preview.caption}</p>
            <p className="mt-2 text-xs text-ink-3">{t("Картинок: {n} · {kb} КБ", { n: preview.pages.length, kb: Math.round(preview.bytes / 1024) })}</p>
            {preview.notes.length > 0 && <p className="mt-2 text-xs text-ink-2">{preview.notes.join(" · ")}</p>}
            <div className="mt-3 grid gap-4 2xl:grid-cols-2">
              {preview.pages.map((src, i) => (
                // eslint-disable-next-line @next/next/no-img-element -- картинка из ответа сервера (data:), оптимизатор Next не нужен
                <img key={i} src={src} alt={t("Страница {n}", { n: i + 1 })} className="w-full rounded-xl border border-line shadow-sm" />
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

const isoDay = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;

/** История: отправки с фильтрами (период, отчёт или запуск, регион, статус), повтор неудачных; ниже — запуски расписания с итогами. */
export function ReportHistoryPanel({ data, onChanged }: { data: ReportOverview; onChanged: () => Promise<void> }) {
  const t = useT();
  const [query, setQuery] = useState<ReportHistoryQuery>(() => {
    const today = new Date();
    return { from: isoDay(new Date(today.getTime() - 7 * 86_400_000)), to: isoDay(today), report: "", regionId: "", status: "", page: 1 };
  });
  const [history, setHistory] = useState<ReportHistory | null>(null);
  const [open, setOpen] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const set = (patch: Partial<ReportHistoryQuery>) => setQuery((q) => ({ ...q, page: 1, ...patch }));

  const load = useCallback(async () => {
    setError(null);
    try {
      setHistory(await reportsApi.history(query));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [query]);

  useEffect(() => {
    load();
  }, [load]);

  const retry = async (id: string, unknown: boolean) => {
    if (unknown && !window.confirm(t("Telegram мог принять отчёт до тайм-аута. Проверьте чат: если отчёта там нет — повторить?"))) return;
    setBusyId(id);
    setError(null);
    setMessage(null);
    try {
      const result = await reportsApi.retry(id, unknown);
      if ("conflict" in result) throw new Error(result.conflict);
      setMessage(
        result.status === "sent"
          ? t("Отправлено в «{chat}».", { chat: result.recipientTitle })
          : result.status === "pending"
            ? t("Временная ошибка Telegram — повторим в {time}.", { time: when(result.nextAttemptAt) })
            : `${t(deliveryStatus[result.status].label)}: ${t(result.error ?? "")}`,
      );
      await Promise.all([load(), onChanged()]);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusyId(null);
    }
  };

  const pages = history ? Math.max(1, Math.ceil(history.total / history.pageSize)) : 1;
  const narrow = field.replace("w-full", "w-auto");
  const reportOptions = useMemo(
    () => reportTypes.map((type) => ({ type, jobs: data.jobs.filter((j) => j.report === type.key) })),
    [data.jobs],
  );

  return (
    <div>
      <div className="flex flex-wrap items-end gap-2">
        <label className="block">
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("С")}</span>
          <input type="date" className={narrow} value={query.from} onChange={(e) => set({ from: e.target.value })} />
        </label>
        <label className="block">
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("По")}</span>
          <input type="date" className={narrow} value={query.to} onChange={(e) => set({ to: e.target.value })} />
        </label>
        <select className={narrow} value={query.report} onChange={(e) => set({ report: e.target.value })} aria-label={t("Отчёт")}>
          <option value="">{t("Все отчёты")}</option>
          {reportOptions.map(({ type, jobs }) => (
            <optgroup key={type.key} label={t(type.title)}>
              <option value={type.key}>{t("Все запуски")}</option>
              {jobs.map((j) => (
                <option key={j.code} value={j.code}>
                  {j.time} — {t(j.slotTitle)}
                </option>
              ))}
            </optgroup>
          ))}
        </select>
        <select className={narrow} value={query.regionId} onChange={(e) => set({ regionId: e.target.value })} aria-label={t("Регион")}>
          <option value="">{t("Все регионы")}</option>
          {data.regions.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </select>
        <select className={narrow} value={query.status} onChange={(e) => set({ status: e.target.value })} aria-label={t("Статус")}>
          <option value="">{t("Все статусы")}</option>
          {Object.entries(deliveryStatus).map(([key, v]) => (
            <option key={key} value={key}>
              {t(v.label)}
            </option>
          ))}
        </select>
        <button className={button} onClick={load}>
          <RefreshCw className="size-4" />
          {t("Обновить")}
        </button>
      </div>
      {message && <p className="mt-3 rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">{message}</p>}
      {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

      <div className="mt-4 overflow-x-auto">
        {history === null ? (
          <Loader2 className="size-5 animate-spin text-ink-3" />
        ) : history.items.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Отправок за этот период нет.")}</p>
        ) : (
          <table className="w-full min-w-[980px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">{t("Когда")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Отчёт")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Получатель")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Область")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Статус")}</th>
                <th className="py-2 font-semibold" />
              </tr>
            </thead>
            <tbody>
              {history.items.map((r) => {
                const job = data.jobs.find((j) => j.code === r.report);
                return (
                  <Fragment key={r.id}>
                    <tr className="cursor-pointer border-b border-line align-top hover:bg-muted/50" onClick={() => setOpen(open === r.id ? null : r.id)}>
                      <td className="whitespace-nowrap py-2 pr-3 text-ink-2">{when(r.sentAt ?? r.createdAt)}</td>
                      <td className="py-2 pr-3">
                        <span className="block text-ink">{job ? `${t(job.reportTitle)} · ${job.time}` : t(r.reportTitle)}</span>
                        <span className="text-xs text-ink-3">
                          {day(r.period)} · {t(triggerLabel[r.trigger])}
                        </span>
                      </td>
                      <td className="py-2 pr-3 text-ink">
                        <span className="flex items-center gap-2">
                          {r.role && <RoleChip role={r.role} />}
                          {r.recipientTitle}
                        </span>
                      </td>
                      <td className="py-2 pr-3 text-ink-2">{r.scopeTitle}</td>
                      <td className="py-2 pr-3">
                        <span className={deliveryStatus[r.status].className}>{t(deliveryStatus[r.status].label)}</span>
                        {r.error && <span className="block max-w-[320px] truncate text-xs text-ink-3">{t(r.error)}</span>}
                      </td>
                      <td className="py-2 text-right" onClick={(e) => e.stopPropagation()}>
                        {r.canRetry && (
                          <button className={button} onClick={() => retry(r.id, r.status === "unknown")} disabled={busyId === r.id} title={t("Повторить отправку")}>
                            {busyId === r.id ? <Loader2 className="size-4 animate-spin" /> : <RotateCcw className="size-4" />}
                            {t("Повторить")}
                          </button>
                        )}
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
                            {r.durationMs != null && ` · ${t("{s} с", { s: (r.durationMs / 1000).toFixed(1) })}`}
                            {r.dataAsOf && ` · ${t("данные Linko на {time}", { time: when(r.dataAsOf) })}`}
                            {r.nextAttemptAt && r.status === "pending" && ` · ${t("повтор в {time}", { time: when(r.nextAttemptAt) })}`}
                            {r.by && ` · ${t("отправил {name}", { name: r.by })}`}
                          </p>
                        </td>
                      </tr>
                    )}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
      {history && pages > 1 && (
        <div className="mt-3 flex items-center justify-end gap-2 text-sm text-ink-2">
          <button className={button} onClick={() => setQuery((q) => ({ ...q, page: q.page - 1 }))} disabled={query.page <= 1} aria-label={t("Назад")}>
            <ChevronLeft className="size-4" />
          </button>
          {t("Стр. {page} из {pages} · всего {total}", { page: query.page, pages, total: history.total })}
          <button className={button} onClick={() => setQuery((q) => ({ ...q, page: q.page + 1 }))} disabled={query.page >= pages} aria-label={t("Вперёд")}>
            <ChevronRight className="size-4" />
          </button>
        </div>
      )}

      {history && history.runs.length > 0 && (
        <div className="mt-6">
          <h3 className="text-sm font-semibold text-ink">{t("Запуски по расписанию")}</h3>
          <div className="mt-2 overflow-x-auto">
            <table className="w-full min-w-[760px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="py-2 pr-3 font-semibold">{t("Запуск")}</th>
                  <th className="py-2 pr-3 font-semibold">{t("День")}</th>
                  <th className="py-2 pr-3 font-semibold">{t("Итог")}</th>
                  <th className="py-2 pr-3 font-semibold">{t("Отправлено / ошибок / пропущено")}</th>
                  <th className="py-2 font-semibold">{t("Данные Linko на")}</th>
                </tr>
              </thead>
              <tbody>
                {history.runs.map((run) => {
                  const job = data.jobs.find((j) => j.code === run.job);
                  return (
                    <tr key={run.id} className="border-b border-line align-top">
                      <td className="py-2 pr-3 text-ink">{job ? `${t(job.reportTitle)} · ${job.time} — ${t(job.slotTitle)}` : t(run.title)}</td>
                      <td className="whitespace-nowrap py-2 pr-3 text-ink-2">{day(run.period)}</td>
                      <td className="py-2 pr-3">
                        <span className={runStatus[run.status].className}>{t(runStatus[run.status].label)}</span>
                        {run.error && <span className="block max-w-[360px] text-xs text-ink-3">{t(run.error)}</span>}
                      </td>
                      <td className="py-2 pr-3 text-ink-2">
                        {run.sent} / {run.failed} / {run.skipped} {t("из {n}", { n: run.planned })}
                      </td>
                      <td className="py-2 text-ink-3">{when(run.dataAsOf)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

