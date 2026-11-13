"use client";

import { useMemo, useState } from "react";
import { CalendarCheck, Loader2, Plus, RefreshCw, X } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { date, num } from "@/lib/analytik";
import { directionLabel, evidenceClass, groupDecisions, outcomeLabel, type DecisionDraft, type DecisionView } from "@/lib/intelligence";
import { Card, Empty } from "../ui";

const metrics: { code: string; label: string }[] = [
  { code: "revenue", label: "Выручка" },
  { code: "kg", label: "Продажи, кг" },
  { code: "orders", label: "Заказы" },
  { code: "outlets", label: "АКБ (точки с покупкой)" },
  { code: "units", label: "Количество, ед." },
  { code: "discount", label: "Скидки" },
  { code: "payments", label: "Оплаты" },
];

const input = "h-9 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink outline-none focus:border-accent";

function Pill({ tone, children }: { tone: string; children: React.ReactNode }) {
  return <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium ${tone}`}>{children}</span>;
}

/**
 * «Аналитик → Решения»: память управленческих решений. Решение — что сделали, почему, какой показатель и как должен измениться к дате
 * проверки; исходное значение считает сервер по срезу до решения, факт и вердикт — после срока (каждый день в фоне или кнопкой).
 */
export function DecisionsView({ initial, draft, today }: { initial: DecisionView[]; draft: DecisionDraft | null; today: string }) {
  const t = useT();
  const [list, setList] = useState(initial);
  const [creating, setCreating] = useState(draft !== null);
  const groups = useMemo(() => groupDecisions(list), [list]);
  const replace = (d: DecisionView) => setList((all) => all.map((x) => (x.id === d.id ? d : x)));

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-ink-3">
          {t("Решение сравнивается с показателем до него; вердикт — «до / после», он не доказывает, что результат дало именно решение.")}
        </p>
        {!creating && (
          <button type="button" onClick={() => setCreating(true)} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-accent px-3 text-sm font-medium text-white hover:bg-accent-strong">
            <Plus className="size-4" />
            {t("Записать решение")}
          </button>
        )}
      </div>
      {creating && <DecisionForm draft={draft} today={today} onCancel={() => setCreating(false)} onSaved={(d) => (setList((all) => [d, ...all]), setCreating(false))} />}
      {list.length === 0 && !creating && <Empty text="Решений пока нет" hint="Запишите решение — система сама проверит результат в срок" />}
      {groups.due.length > 0 && <Section title="Пора проверить" items={groups.due} onChange={replace} />}
      {groups.active.length > 0 && <Section title="Ждут срока проверки" items={groups.active} onChange={replace} />}
      {groups.evaluated.length > 0 && <Section title="Проверены" items={groups.evaluated} onChange={replace} />}
      {groups.cancelled.length > 0 && <Section title="Отменены" items={groups.cancelled} onChange={replace} />}
    </div>
  );
}

function Section({ title, items, onChange }: { title: string; items: DecisionView[]; onChange: (d: DecisionView) => void }) {
  return (
    <Card title={title}>
      <ul className="grid gap-2">
        {items.map((d) => (
          <Decision key={d.id} d={d} onChange={onChange} />
        ))}
      </ul>
    </Card>
  );
}

function Decision({ d, onChange }: { d: DecisionView; onChange: (d: DecisionView) => void }) {
  const t = useT();
  const [busy, setBusy] = useState<"evaluate" | "cancel" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const outcome = d.outcome ? outcomeLabel[d.outcome] : null;
  const act = async (kind: "evaluate" | "cancel") => {
    setBusy(kind);
    setError(null);
    try {
      onChange(await bff<DecisionView>(`analytik/decisions/${d.id}/${kind}`, { method: "POST", body: kind === "cancel" ? JSON.stringify({ note: null }) : undefined }));
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(null);
    }
  };
  return (
    <li className="rounded-xl border border-line px-3 py-2.5">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium text-ink">{d.title}</p>
          <p className="mt-0.5 text-xs text-ink-3">
            {date(d.decidedOn)} · {d.authorName ?? "—"} · {d.metricTitle}
          </p>
        </div>
        {outcome && <Pill tone={outcome.tone}>{t(outcome.label)}</Pill>}
      </div>
      {d.reason && <p className="mt-1.5 text-sm text-ink-2">{d.reason}</p>}
      <dl className="mt-2 grid grid-cols-2 gap-x-4 gap-y-1 text-xs sm:grid-cols-4">
        <div>
          <dt className="text-ink-4">{t("До решения")}</dt>
          <dd className="tabular-nums text-ink">{num(d.baselineValue)}</dd>
          <dd className="text-ink-4">{date(d.baselineFrom)} — {date(d.baselineTo)}</dd>
        </div>
        <div>
          <dt className="text-ink-4">{t("Ожидание")}</dt>
          <dd className="text-ink">
            {t(directionLabel[d.expectedDirection])}
            {d.expectedChangePct != null ? ` ${Math.abs(d.expectedChangePct)} %` : ""}
          </dd>
        </div>
        <div>
          <dt className="text-ink-4">{t("Проверка")}</dt>
          <dd className="text-ink">{date(d.reviewOn)}</dd>
        </div>
        <div>
          <dt className="text-ink-4">{t("Факт")}</dt>
          <dd className="tabular-nums text-ink">
            {d.actualValue != null ? num(d.actualValue) : "—"}
            {d.actual?.changePct != null ? ` (${d.actual.changePct > 0 ? "+" : ""}${num(d.actual.changePct, 1)} % ${t("в день")})` : ""}
          </dd>
        </div>
      </dl>
      {d.actual?.text && <p className="mt-2 text-xs text-ink-2">{d.actual.text}</p>}
      {d.actual?.evidenceClass && <p className="mt-0.5 text-[11px] text-ink-4">{t(evidenceClass[d.actual.evidenceClass] ?? "")}</p>}
      {d.actual?.error && <p className="mt-1 text-xs text-bad">{d.actual.error}</p>}
      {error && <p className="mt-1 text-xs text-bad">{error}</p>}
      {d.status === "active" && (
        <div className="mt-2 flex flex-wrap gap-2">
          <button type="button" disabled={busy !== null} onClick={() => act("evaluate")} className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-line px-2.5 text-xs font-medium text-ink-2 hover:bg-muted disabled:opacity-60">
            {busy === "evaluate" ? <Loader2 className="size-3.5 animate-spin" /> : <RefreshCw className="size-3.5" />}
            {t("Проверить сейчас")}
          </button>
          <button type="button" disabled={busy !== null} onClick={() => act("cancel")} className="inline-flex h-8 items-center gap-1.5 rounded-lg px-2.5 text-xs text-ink-3 hover:bg-muted disabled:opacity-60">
            <X className="size-3.5" />
            {t("Отменить решение")}
          </button>
        </div>
      )}
    </li>
  );
}

function DecisionForm({ draft, today, onCancel, onSaved }: { draft: DecisionDraft | null; today: string; onCancel: () => void; onSaved: (d: DecisionView) => void }) {
  const t = useT();
  const plus30 = (d: string) => {
    const x = new Date(`${d}T00:00:00Z`);
    x.setUTCDate(x.getUTCDate() + 30);
    return x.toISOString().slice(0, 10);
  };
  const [title, setTitle] = useState(draft?.title ?? "");
  const [reason, setReason] = useState(draft?.reason ?? "");
  const [metric, setMetric] = useState(draft?.metric ?? "revenue");
  const [regions, setRegions] = useState(draft?.filters?.find((f) => f.dimension === "region")?.values.join(", ") ?? "");
  const [groups, setGroups] = useState(draft?.filters?.find((f) => f.dimension === "product_group")?.values.join(", ") ?? "");
  const [direction, setDirection] = useState<"up" | "down" | "keep">(draft?.expectedDirection ?? "up");
  const [change, setChange] = useState(draft?.expectedChangePct != null ? String(draft.expectedChangePct) : "");
  const [decidedOn, setDecidedOn] = useState(draft?.decidedOn ?? today);
  const [reviewOn, setReviewOn] = useState(draft?.reviewOn ?? plus30(today));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = async () => {
    setBusy(true);
    setError(null);
    const split = (s: string) => s.split(",").map((x) => x.trim()).filter(Boolean);
    const filters = [
      ...(split(regions).length ? [{ dimension: "region", op: "in", values: split(regions) }] : []),
      ...(split(groups).length ? [{ dimension: "product_group", op: "in", values: split(groups) }] : []),
    ];
    try {
      onSaved(
        await bff<DecisionView>("analytik/decisions", {
          method: "POST",
          body: JSON.stringify({
            title, description: null, reason: reason || null, decidedOn, metric: { metric, filters, segment: null }, expectedDirection: direction,
            expectedChangePct: change.trim() ? Number(change.replace(",", ".")) : null, reviewOn,
          }),
        }),
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card title="Новое решение" subtitle={draft ? "Черновик подготовил AI-аналитик — проверьте и сохраните" : "Исходное значение показателя посчитает сервер за 30 дней до решения"}>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="grid gap-1 text-xs text-ink-3 sm:col-span-2">
          {t("Решение")}
          <input className={input} value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t("Что делаем")} />
        </label>
        <label className="grid gap-1 text-xs text-ink-3 sm:col-span-2">
          {t("Почему")}
          <textarea className={`${input} h-20 py-2`} value={reason} onChange={(e) => setReason(e.target.value)} placeholder={t("Вывод исследования, данные")} />
        </label>
        <label className="grid gap-1 text-xs text-ink-3">
          {t("Показатель проверки")}
          <select className={input} value={metric} onChange={(e) => setMetric(e.target.value)}>
            {metrics.map((m) => (
              <option key={m.code} value={m.code}>{t(m.label)}</option>
            ))}
          </select>
        </label>
        <div className="grid grid-cols-2 gap-2">
          <label className="grid gap-1 text-xs text-ink-3">
            {t("Ожидание")}
            <select className={input} value={direction} onChange={(e) => setDirection(e.target.value as "up" | "down" | "keep")}>
              {(["up", "down", "keep"] as const).map((d) => (
                <option key={d} value={d}>{t(directionLabel[d])}</option>
              ))}
            </select>
          </label>
          <label className="grid gap-1 text-xs text-ink-3">
            {t("На сколько, %")}
            <input className={input} inputMode="decimal" value={change} onChange={(e) => setChange(e.target.value)} placeholder="10" />
          </label>
        </div>
        <label className="grid gap-1 text-xs text-ink-3">
          {t("Регионы (через запятую)")}
          <input className={input} value={regions} onChange={(e) => setRegions(e.target.value)} placeholder={t("пусто — вся компания")} />
        </label>
        <label className="grid gap-1 text-xs text-ink-3">
          {t("Товарные группы (через запятую)")}
          <input className={input} value={groups} onChange={(e) => setGroups(e.target.value)} placeholder={t("пусто — все группы")} />
        </label>
        <label className="grid gap-1 text-xs text-ink-3">
          {t("Дата решения")}
          <input type="date" className={input} value={decidedOn} onChange={(e) => setDecidedOn(e.target.value)} />
        </label>
        <label className="grid gap-1 text-xs text-ink-3">
          {t("Дата проверки")}
          <input type="date" className={input} value={reviewOn} onChange={(e) => setReviewOn(e.target.value)} />
        </label>
      </div>
      {error && <p className="mt-2 text-xs text-bad">{error}</p>}
      <div className="mt-3 flex flex-wrap gap-2">
        <button type="button" disabled={busy || !title.trim()} onClick={save} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-accent px-3 text-sm font-medium text-white hover:bg-accent-strong disabled:opacity-60">
          {busy ? <Loader2 className="size-4 animate-spin" /> : <CalendarCheck className="size-4" />}
          {t("Сохранить решение")}
        </button>
        <button type="button" onClick={onCancel} className="inline-flex h-9 items-center rounded-lg px-3 text-sm text-ink-3 hover:bg-muted">
          {t("Отмена")}
        </button>
      </div>
    </Card>
  );
}
