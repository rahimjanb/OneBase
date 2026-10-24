"use client";

import { useCallback, useEffect, useState } from "react";
import { Loader2, Plus, RefreshCw, Trash2 } from "lucide-react";
import { button, danger, field, primary } from "@/components/ai/form";
import { Alert, Note } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { num } from "@/lib/sales/format";
import { dailyPlanStatus, dailyPlansApi, type ReportDailyPlan, type ReportDailyPlans, type ReportHoliday } from "@/lib/telegramReports";

const isoDay = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
const sum = (v: number | null | undefined) => (v == null ? "—" : num(v));
const when = (iso: string) => new Date(iso).toLocaleString("ru-RU", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });

/**
 * Дневные планы ТП для «ТП без заказов»: план дня = (план месяца − факт по вчера) ÷ оставшиеся рабочие дни; фиксируется утром и не
 * меняется до конца дня. Здесь — что зафиксировано на день, история пересчётов, пересчёт с причиной и справочник праздников.
 */
export function TelegramDailyPlans({ canUseSales }: { canUseSales: boolean }) {
  const t = useT();
  const [date, setDate] = useState(() => isoDay(new Date()));
  const [data, setData] = useState<ReportDailyPlans | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [history, setHistory] = useState(false);

  const load = useCallback(async () => {
    if (!canUseSales) return;
    setError(null);
    try {
      setData(await dailyPlansApi.list(date));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [date, canUseSales]);

  useEffect(() => {
    load();
  }, [load]);

  const recalc = async (agentId: number | null) => {
    const reason = window.prompt(t("Причина пересчёта (останется в истории):"))?.trim();
    if (!reason) return;
    setBusy(true);
    setError(null);
    try {
      setData(await dailyPlansApi.recalculate({ date, agentId, reason }));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const rows = history ? [...(data?.items ?? []), ...(data?.history ?? [])] : (data?.items ?? []);
  const trigger: Record<string, string> = { morning: "утром", report: "при первом отчёте", admin: "пересчёт", "plan-appeared": "план появился в Linko" };

  return (
    <div className="space-y-5">
      <Note>
        {t(
          "План на день для «ТП без заказов» = (план месяца в Linko − факт по вчера включительно) ÷ оставшиеся рабочие дни месяца (пн–сб без праздников, сегодня включительно). Считается один раз утром с 08:00 и не меняется до конца дня: отчёты 13:00, 17:00 и 18:00 берут это же значение. Нет плана месяца — «—», а не ноль; план выполнен — «выполнен»; воскресенье или праздник — план дня не считается.",
        )}
      </Note>
      {!canUseSales && <Alert tone="warn" className="mt-0">{t("Нет права на данные продаж (sales.read): дневные планы недоступны.")}</Alert>}
      {error && <Alert tone="bad" className="mt-0">{error}</Alert>}

      <div className="flex flex-wrap items-end gap-2">
        <label className="block">
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("День")}</span>
          <input type="date" className={field.replace("w-full", "w-auto")} value={date} onChange={(e) => setDate(e.target.value)} />
        </label>
        <button className={button} onClick={load} disabled={!canUseSales}>
          <RefreshCw className="size-4" /> {t("Обновить")}
        </button>
        <button className={primary} onClick={() => recalc(null)} disabled={busy || !canUseSales}>
          {busy ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />} {t("Пересчитать всех с причиной")}
        </button>
        <label className="ml-auto flex items-center gap-2 text-sm text-ink-2">
          <input type="checkbox" checked={history} onChange={(e) => setHistory(e.target.checked)} /> {t("Показать историю пересчётов")}
        </label>
      </div>

      {data && (
        <p className="text-sm text-ink-2">
          {data.workingDay
            ? t("{date}: рабочий день, до конца месяца {n} рабочих дн. (включая этот). Зафиксировано планов: {count}.", { date, n: data.workDaysLeft, count: data.items.length })
            : t("{date}: выходной или праздник — план на день не считается.", { date })}
        </p>
      )}

      <div className="overflow-x-auto rounded-xl border border-line">
        <table className="min-w-full text-sm">
          <thead className="bg-surface-2 text-left text-xs uppercase tracking-wide text-ink-3">
            <tr>
              <th className="px-3 py-2">{t("ТП")}</th>
              <th className="px-3 py-2">{t("Регион")}</th>
              <th className="px-3 py-2 text-right">{t("План месяца, сум")}</th>
              <th className="px-3 py-2 text-right">{t("Факт по вчера, сум")}</th>
              <th className="px-3 py-2 text-right">{t("Остаток, сум")}</th>
              <th className="px-3 py-2 text-right">{t("Рабочих дней")}</th>
              <th className="px-3 py-2 text-right">{t("План на день, сум")}</th>
              <th className="px-3 py-2">{t("Статус")}</th>
              <th className="px-3 py-2">{t("Посчитан")}</th>
              <th className="px-3 py-2"></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {rows.length === 0 && (
              <tr>
                <td colSpan={10} className="px-3 py-6 text-center text-ink-3">
                  {t("На этот день планов ещё нет: они считаются утром с 08:00 или при первом отчёте «ТП без заказов».")}
                </td>
              </tr>
            )}
            {rows.map((p: ReportDailyPlan) => (
              <tr key={p.id} className={p.supersededAt ? "text-ink-3" : ""}>
                <td className="px-3 py-2 font-medium text-ink">{p.agent}</td>
                <td className="px-3 py-2">{p.region ?? "—"}</td>
                <td className="px-3 py-2 text-right tabular-nums">{sum(p.monthPlanSum)}</td>
                <td className="px-3 py-2 text-right tabular-nums">{num(p.factBeforeSum)}</td>
                <td className="px-3 py-2 text-right tabular-nums">{sum(p.remainingSum)}</td>
                <td className="px-3 py-2 text-right tabular-nums">{p.workDaysLeft}</td>
                <td className="px-3 py-2 text-right font-semibold tabular-nums text-ink">{p.status === "done" ? "0" : sum(p.dailyPlanSum)}</td>
                <td className="px-3 py-2">
                  <span className={`rounded-md px-1.5 py-0.5 text-xs ${dailyPlanStatus[p.status].className}`} title={p.note ? t(p.note) : undefined}>
                    {t(dailyPlanStatus[p.status].label)}
                  </span>
                  {p.note && <div className="text-xs text-ink-3">{t(p.note)}</div>}
                </td>
                <td className="px-3 py-2 text-xs text-ink-3">
                  {when(p.computedAt)} · {t(trigger[p.trigger] ?? p.trigger)}
                  {p.by ? ` · ${p.by}` : ""}
                  {p.reason ? ` · ${p.reason}` : ""}
                  {p.supersededAt ? ` · ${t("заменён {time}", { time: when(p.supersededAt) })}` : ""}
                </td>
                <td className="px-3 py-2 text-right">
                  {!p.supersededAt && (
                    <button className={button} onClick={() => recalc(p.agentId)} disabled={busy || !canUseSales} title={t("Пересчитать этого ТП с причиной")}>
                      <RefreshCw className="size-3.5" />
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Holidays year={Number(date.slice(0, 4))} canEdit={canUseSales} onChanged={load} />
    </div>
  );
}

/** Справочник праздников OneBase: нерабочие дни помимо воскресений; хайиты вносятся вручную по указу на год. */
function Holidays({ year, canEdit, onChanged }: { year: number; canEdit: boolean; onChanged: () => Promise<void> }) {
  const t = useT();
  const [items, setItems] = useState<ReportHoliday[]>([]);
  const [draft, setDraft] = useState({ date: "", name: "" });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setItems(await dailyPlansApi.holidays(year));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [year]);

  useEffect(() => {
    load();
  }, [load]);

  const add = async () => {
    setBusy(true);
    setError(null);
    try {
      await dailyPlansApi.addHoliday({ date: draft.date, name: draft.name });
      setDraft({ date: "", name: "" });
      await Promise.all([load(), onChanged()]);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const remove = async (h: ReportHoliday) => {
    if (!window.confirm(t("Убрать «{name}» ({date}) из справочника праздников?", { name: h.name, date: h.date }))) return;
    setError(null);
    try {
      await dailyPlansApi.deleteHoliday(h.id);
      await Promise.all([load(), onChanged()]);
    } catch (e) {
      setError((e as Error).message);
    }
  };

  return (
    <section className="rounded-xl border border-line">
      <div className="border-b border-line px-4 py-3">
        <h3 className="font-semibold text-ink">{t("Праздники {year}", { year })}</h3>
        <p className="text-xs text-ink-3">
          {t("Нерабочие дни помимо воскресений: в праздник план дня не считается, и он не входит в оставшиеся рабочие дни. Государственные праздники с постоянной датой внесены; Рамазан и Курбан хайит добавьте по указу на год.")}
        </p>
      </div>
      {error && <Alert tone="bad" className="mx-4 mt-3">{error}</Alert>}
      <ul className="divide-y divide-line">
        {items.length === 0 && <li className="px-4 py-3 text-sm text-ink-3">{t("Праздников на этот год нет.")}</li>}
        {items.map((h) => (
          <li key={h.id} className="flex items-center gap-3 px-4 py-2 text-sm">
            <span className="w-24 tabular-nums text-ink-2">{h.date.split("-").reverse().join(".")}</span>
            <span className="flex-1 text-ink">{h.name}</span>
            {canEdit && (
              <button className={danger} onClick={() => remove(h)} title={t("Убрать")}>
                <Trash2 className="size-3.5" />
              </button>
            )}
          </li>
        ))}
      </ul>
      {canEdit && (
        <div className="flex flex-wrap items-end gap-2 border-t border-line px-4 py-3">
          <input type="date" className={field.replace("w-full", "w-auto")} value={draft.date} onChange={(e) => setDraft({ ...draft, date: e.target.value })} aria-label={t("Дата")} />
          <input className={field.replace("w-full", "w-64")} placeholder={t("Название праздника")} value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
          <button className={primary} onClick={add} disabled={busy || !draft.date || !draft.name.trim()}>
            {busy ? <Loader2 className="size-4 animate-spin" /> : <Plus className="size-4" />} {t("Добавить праздник")}
          </button>
        </div>
      )}
    </section>
  );
}
