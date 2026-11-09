"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";
import { AlertTriangle, BadgeCheck, Ban, CheckCircle2, CloudOff, FilePen, History, Loader2, Lock, Plus, Trash2, Undo2 } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { parseNumber, quantity, stampRu } from "@/lib/procurement";
import {
  bakeResult,
  dayRu,
  reworkLabel,
  shiftStatusLabel,
  shiftStatusTone,
  stageKindLabel,
  type Named,
  type ReworkStatus,
  type ShiftDetail,
  type ShiftMaterialKind,
} from "@/lib/production";
import { newKey, primary, secondary } from "./ProdKit";
import { RecipeCalcResult } from "./RecipeCalcResult";

type StageEntry = { done: boolean; inputKg: string; outputKg: string; moisture: string; reworkKg: string; reason: string };
type DefectEntry = { key: string; stageNo: number; defectTypeId: string | null; name: string; kg: string; rework: ReworkStatus };
type Entry = {
  goodOutput: string;
  packs: string;
  boxes: string;
  line: string;
  supervisorId: string;
  comment: string;
  reason: string;
  batches: string[];
  stages: Record<number, StageEntry>;
  defects: DefectEntry[];
  /** Материалы: ключ «вид:этап:позиция» → количество в единице склада. */
  materials: Record<string, string>;
};

const text = (n: number | null | undefined) => (n === null || n === undefined ? "" : String(n).replace(".", ","));
const num = (s: string) => (s.trim() ? parseNumber(s) : null);
const big = "h-11 w-full rounded-lg border border-line bg-surface px-3 text-base text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 disabled:opacity-70";
const mini = "mb-1 block text-xs font-medium text-ink-2";
const materialKey = (kind: ShiftMaterialKind, stageNo: number | null, itemId: string) => `${kind}:${stageNo ?? ""}:${itemId}`;

function fromDetail(d: ShiftDetail): Entry {
  return {
    goodOutput: text(d.goodOutput),
    packs: text(d.packs),
    boxes: text(d.boxes),
    line: d.line ?? "",
    supervisorId: d.supervisorId ?? "",
    comment: d.comment ?? "",
    reason: d.reason ?? "",
    batches: d.batches.map(text),
    stages: Object.fromEntries(
      d.template.map((t) => {
        const s = d.stages.find((x) => x.stageNo === t.no);
        return [t.no, { done: s?.done ?? false, inputKg: text(s?.inputKg), outputKg: text(s?.outputKg), moisture: text(s?.moisture), reworkKg: text(s?.reworkKg), reason: s?.reason ?? "" }];
      }),
    ),
    defects: d.defects.map((x) => ({ key: newKey(), stageNo: x.stageNo, defectTypeId: x.defectTypeId, name: x.name ?? "", kg: text(x.quantityKg), rework: x.rework ?? "Forbidden" })),
    materials: Object.fromEntries(d.materials.map((m) => [materialKey(m.kind, m.stageNo, m.itemId), text(m.quantity)])),
  };
}

function toBody(e: Entry, d: ShiftDetail, version: number) {
  return {
    shiftDate: d.shiftDate,
    shiftNo: d.shiftNo,
    line: e.line.trim() || null,
    supervisorId: e.supervisorId || null,
    goodOutput: num(e.goodOutput),
    packs: num(e.packs),
    boxes: num(e.boxes),
    comment: e.comment.trim() || null,
    reason: e.reason.trim() || null,
    batches: e.batches.map(num).filter((x): x is number => x !== null && x > 0),
    stages: Object.entries(e.stages).map(([no, s]) => ({
      stageNo: Number(no),
      done: s.done,
      inputKg: num(s.inputKg),
      outputKg: num(s.outputKg),
      moisture: num(s.moisture),
      reworkKg: num(s.reworkKg),
      reason: s.reason.trim() || null,
    })),
    defects: e.defects.filter((x) => (num(x.kg) ?? 0) > 0).map((x) => ({ stageNo: x.stageNo, defectTypeId: x.defectTypeId, name: x.name || null, quantityKg: num(x.kg), rework: x.rework })),
    materials: Object.entries(e.materials)
      .map(([k, v]) => {
        const [kind, stage, itemId] = k.split(":");
        return { itemId, kind, stageNo: stage ? Number(stage) : null, quantity: num(v) };
      })
      .filter((m) => (m.quantity ?? 0) > 0),
    version,
  };
}

/**
 * Сменный отчёт (экран оператора, удобен на планшете): итог смены (годный выход, упаковки, коробки), фактические замесы, по каждому этапу —
 * выпечка до и после (выход, потеря, отклонение от нормы; вне допуска — причина), влажность, брак по видам и переработка, брак и взвешивание
 * материалов, отметка «этап заполнен». Ввод сохраняется сам (через секунду после правки) и копией в браузере — обновление страницы его не
 * теряет. Справа — чего не хватает для закрытия и расчёт по результатам. Утверждённая смена — только просмотр; исправления — корректировкой.
 */
export function ShiftView({ shift, people }: { shift: ShiftDetail; people: Named[] }) {
  const t = useT();
  const router = useRouter();
  const editable = shift.can.edit;
  const storageKey = `onebase:shift:${shift.id}`;
  const [entry, setEntry] = useState<Entry>(() => fromDetail(shift));
  const [server, setServer] = useState(shift);
  const [state, setState] = useState<"saved" | "dirty" | "saving" | "offline" | "conflict">("saved");
  const [savedAt, setSavedAt] = useState<string | null>(null);
  const [restored, setRestored] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dialog, setDialog] = useState<"return" | "cancel" | null>(null);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const version = useRef(shift.version);
  const latest = useRef(entry);
  const timer = useRef<number | null>(null);
  const inFlight = useRef<Promise<boolean> | null>(null);
  latest.current = entry;

  // Несохранённое в браузере с той же версией смены — восстановить (обновление страницы, обрыв связи).
  useEffect(() => {
    if (!editable) return;
    try {
      const raw = window.localStorage.getItem(storageKey);
      if (!raw) return;
      const saved = JSON.parse(raw) as { version: number; entry: Entry };
      if (saved.version === shift.version) {
        setEntry(saved.entry);
        setRestored(true);
        setState("dirty");
      } else window.localStorage.removeItem(storageKey);
    } catch {
      window.localStorage.removeItem(storageKey);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const save = useCallback(async (): Promise<boolean> => {
    if (inFlight.current) await inFlight.current;
    const run = (async () => {
      setState("saving");
      try {
        const { version: next } = await bff<{ version: number }>(`production/shifts/${shift.id}`, { method: "PUT", body: JSON.stringify(toBody(latest.current, shift, version.current)) });
        version.current = next;
        window.localStorage.removeItem(storageKey);
        setSavedAt(new Date().toISOString());
        setState("saved");
        setError(null);
        setServer(await bff<ShiftDetail>(`production/shifts/${shift.id}`));
        return true;
      } catch (e) {
        const message = e instanceof Error ? e.message : String(e);
        setError(message);
        setState(/изменили/.test(message) ? "conflict" : /fetch|network|сеть/i.test(message) ? "offline" : "dirty");
        return false;
      } finally {
        inFlight.current = null;
      }
    })();
    inFlight.current = run;
    return run;
  }, [shift, storageKey]);

  const change = (patch: (e: Entry) => Entry) => {
    if (!editable || state === "conflict") return;
    setEntry((e) => {
      const next = patch(e);
      window.localStorage.setItem(storageKey, JSON.stringify({ version: version.current, entry: next }));
      return next;
    });
    setState("dirty");
    if (timer.current) window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => void save(), 1200);
  };

  useEffect(() => {
    const warn = (e: BeforeUnloadEvent) => {
      if (state === "dirty" || state === "saving") e.preventDefault();
    };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [state]);

  const act = async (path: string, body?: object) => {
    setBusy(true);
    setError(null);
    try {
      if (path === "close" && state !== "saved" && !(await save())) return;
      const result = await bff<{ id?: string }>(`production/shifts/${shift.id}/${path}`, { method: "POST", body: JSON.stringify(body ?? {}) });
      setDialog(null);
      if (path === "correct" && result?.id) router.push(`/production/shifts/${result.id}`);
      else router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const setStage = (no: number, patch: Partial<StageEntry>) => change((e) => ({ ...e, stages: { ...e.stages, [no]: { ...e.stages[no], ...patch } } }));
  const hasMixing = shift.template.some((s) => s.kind === "Mixing");
  const hasPacking = shift.template.some((s) => s.kind === "Packing");
  const hasBoxes = shift.template.some((s) => s.materials.some((m) => m.basis === "Box"));
  const batchTotal = entry.batches.reduce((sum, b) => sum + (num(b) ?? 0), 0);
  // Единицы склада — коды справочника («кг», «шт»): на узбекском показываются переводом, если он есть.
  const unit = t(shift.outputUnit);

  const status = (
    <span className="inline-flex items-center gap-1.5 text-xs text-ink-3" aria-live="polite">
      {state === "saving" && (
        <>
          <Loader2 className="size-3.5 animate-spin" aria-hidden /> {t("Сохраняем…")}
        </>
      )}
      {state === "saved" && savedAt && (
        <>
          <CheckCircle2 className="size-3.5 text-ok" aria-hidden /> {t("Сохранено {time}", { time: stampRu(savedAt).split(" ").pop() ?? "" })}
        </>
      )}
      {state === "dirty" && t("Есть несохранённые изменения")}
      {state === "offline" && (
        <span className="inline-flex items-center gap-1 text-warn">
          <CloudOff className="size-3.5" aria-hidden /> {t("Нет связи — ввод сохранён в браузере и отправится позже")}
        </span>
      )}
    </span>
  );

  const actions = (
    <div className="flex flex-wrap items-center gap-2">
      {shift.can.cancel && (
        <button type="button" onClick={() => setDialog("cancel")} className={secondary}>
          <Ban className="size-4" aria-hidden /> {t("Отменить")}
        </button>
      )}
      {shift.can.correct && (
        <button type="button" onClick={() => void act("correct")} disabled={busy} className={secondary}>
          <FilePen className="size-4" aria-hidden /> {t("Корректировка")}
        </button>
      )}
      {shift.can.return && (
        <button type="button" onClick={() => setDialog("return")} className={secondary}>
          <Undo2 className="size-4" aria-hidden /> {t("Вернуть на ввод")}
        </button>
      )}
      {editable && (
        <button type="button" onClick={() => void act("close")} disabled={busy || server.problems.length > 0 || state === "conflict"} className={primary}>
          {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Lock className="size-4" aria-hidden />} {t("Закрыть смену")}
        </button>
      )}
      {shift.can.approve && (
        <button type="button" onClick={() => void act("approve")} disabled={busy} className={primary}>
          <BadgeCheck className="size-4" aria-hidden /> {t("Утвердить")}
        </button>
      )}
    </div>
  );

  return (
    <>
      <PageHeader
        title={`${shift.number} · ${shift.recipeName}`}
        subtitle={
          <span className="inline-flex flex-wrap items-center gap-2">
            <span>
              {dayRu(shift.shiftDate)} · {t("смена {n}", { n: shift.shiftNo })} · {shift.operatorName}
            </span>
            <span className={`inline-flex rounded-md px-2 py-0.5 text-xs font-semibold ${shiftStatusTone[shift.status]}`}>{t(shiftStatusLabel[shift.status])}</span>
            {editable && status}
          </span>
        }
        back="/production/shifts"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Отделы"), href: "/departments" },
          { label: t("Производство"), href: "/production" },
          { label: t("Смены"), href: "/production/shifts" },
          { label: shift.number },
        ]}
        actions={<span className="max-lg:hidden">{actions}</span>}
      />
      <PageBody>
        <div className="mx-auto max-w-6xl space-y-4">
          <div className="lg:hidden">{actions}</div>
          {restored && <p className="rounded-xl border border-accent/30 bg-accent-soft px-4 py-2.5 text-sm text-ink">{t("Восстановлен несохранённый ввод из браузера — он сохранится сам.")}</p>}
          {shift.kind === "Correction" && (
            <p className="rounded-xl border border-warn/40 bg-warn-soft px-4 py-2.5 text-sm text-ink">
              {t("Корректировка смены {n}: после утверждения заменит её в расчётах.", { n: shift.correctsNumber ?? "" })}{" "}
              {shift.correctsId && (
                <Link href={`/production/shifts/${shift.correctsId}`} className="font-medium underline">
                  {t("Открыть исправляемую")}
                </Link>
              )}
            </p>
          )}
          {shift.correctionId && shift.status !== "Draft" && (
            <p className="rounded-xl border border-line bg-muted px-4 py-2.5 text-sm text-ink-2">
              {shift.status === "Superseded" ? t("Смена заменена корректировкой.") : t("По смене есть корректировка.")}{" "}
              <Link href={`/production/shifts/${shift.correctionId}`} className="font-medium text-accent-strong underline">
                {t("Открыть корректировку")}
              </Link>
            </p>
          )}
          <ErrorNote text={error} />

          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
            <fieldset disabled={!editable || state === "conflict"} className="min-w-0 space-y-4">
              <section className="rounded-2xl border border-line bg-surface p-4">
                <h2 className="mb-3 text-base font-semibold text-ink">{t("Итог смены")}</h2>
                <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                  <label className="col-span-2 block sm:col-span-1">
                    <span className={mini}>{t("Годный выход, {unit}", { unit })}</span>
                    <input value={entry.goodOutput} onChange={(e) => change((x) => ({ ...x, goodOutput: e.target.value }))} inputMode="decimal" className={`${big} text-right font-semibold tabular-nums`} />
                  </label>
                  {hasPacking && (
                    <label className="block">
                      <span className={mini}>{t("Упаковано, упаковок")}</span>
                      <input value={entry.packs} onChange={(e) => change((x) => ({ ...x, packs: e.target.value }))} inputMode="decimal" className={`${big} text-right tabular-nums`} />
                    </label>
                  )}
                  {hasBoxes && (
                    <label className="block">
                      <span className={mini}>{t("Коробок")}</span>
                      <input value={entry.boxes} onChange={(e) => change((x) => ({ ...x, boxes: e.target.value }))} inputMode="decimal" className={`${big} text-right tabular-nums`} />
                    </label>
                  )}
                  <label className="block">
                    <span className={mini}>{t("Линия")}</span>
                    <input value={entry.line} onChange={(e) => change((x) => ({ ...x, line: e.target.value }))} maxLength={100} className={big} />
                  </label>
                </div>
                <div className="mt-3 grid gap-3 sm:grid-cols-2">
                  <label className="block">
                    <span className={mini}>{t("Начальник смены")}</span>
                    <select value={entry.supervisorId} onChange={(e) => change((x) => ({ ...x, supervisorId: e.target.value }))} className={big}>
                      <option value="">{t("Не выбран")}</option>
                      {shift.supervisorId && !people.some((p) => p.id === shift.supervisorId) && <option value={shift.supervisorId}>{shift.supervisorName}</option>}
                      {people.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  <p className="self-end pb-2 text-xs text-ink-3">
                    {t("Задание {n}: план {q}", { n: shift.orderNumber, q: `${quantity(shift.plannedQty)} ${unit}` })} ·{" "}
                    <Link href={`/production/orders/${shift.orderId}`} className="text-accent-strong hover:underline">
                      {t("открыть")}
                    </Link>
                  </p>
                </div>
                {shift.kind === "Correction" && (
                  <label className="mt-3 block">
                    <span className={mini}>{t("Причина исправления")}</span>
                    <input value={entry.reason} onChange={(e) => change((x) => ({ ...x, reason: e.target.value }))} maxLength={1000} className={big} />
                  </label>
                )}
              </section>

              {hasMixing && (
                <section className="rounded-2xl border border-line bg-surface p-4">
                  <h2 className="mb-1 text-base font-semibold text-ink">{t("Замесы")}</h2>
                  <p className="mb-3 text-xs text-ink-3">
                    {shift.standardBatchKg !== null && t("Стандартный замес {kg} кг. ", { kg: quantity(shift.standardBatchKg) })}
                    {t("Замесов: {n}, всего {kg} кг", { n: entry.batches.filter((b) => (num(b) ?? 0) > 0).length, kg: quantity(Math.round(batchTotal * 1000) / 1000) })}
                  </p>
                  <ul className="grid grid-cols-2 gap-2 sm:grid-cols-4">
                    {entry.batches.map((b, i) => (
                      <li key={i} className="flex items-center gap-1">
                        <span className="w-6 text-right text-xs tabular-nums text-ink-3">{i + 1}</span>
                        <input value={b} onChange={(e) => change((x) => ({ ...x, batches: x.batches.map((v, j) => (j === i ? e.target.value : v)) }))} inputMode="decimal"
                          aria-label={t("Замес {n}, кг", { n: i + 1 })} className={`${big} text-right tabular-nums`} />
                        <button type="button" onClick={() => change((x) => ({ ...x, batches: x.batches.filter((_, j) => j !== i) }))} aria-label={t("Убрать замес {n}", { n: i + 1 })}
                          className="grid size-9 shrink-0 place-items-center rounded-lg text-ink-3 hover:text-bad">
                          <Trash2 className="size-4" aria-hidden />
                        </button>
                      </li>
                    ))}
                  </ul>
                  <div className="mt-2 flex flex-wrap gap-2">
                    {shift.standardBatchKg !== null && (
                      <button type="button" onClick={() => change((x) => ({ ...x, batches: [...x.batches, text(shift.standardBatchKg)] }))} className={`${secondary} h-10`}>
                        <Plus className="size-4" aria-hidden /> {t("Замес {kg} кг", { kg: quantity(shift.standardBatchKg) })}
                      </button>
                    )}
                    <button type="button" onClick={() => change((x) => ({ ...x, batches: [...x.batches, ""] }))} className={`${secondary} h-10`}>
                      <Plus className="size-4" aria-hidden /> {t("Другой массы")}
                    </button>
                  </div>
                </section>
              )}

              {shift.template.map((info) => {
                const s = entry.stages[info.no];
                const inKg = num(s.inputKg);
                const outKg = num(s.outputKg);
                const bake = info.kind === "Baking" && inKg && inKg > 0 && outKg !== null ? bakeResult(inKg, outKg, info.yieldPercent, info.yieldMinPercent, info.yieldMaxPercent) : null;
                const moisture = num(s.moisture);
                const moistureOff = moisture !== null && info.moistureNorm !== null && info.moistureTolerance !== null && Math.abs(moisture - info.moistureNorm) > info.moistureTolerance;
                const productTypes = info.defectTypes.filter((d) => !d.itemId);
                const defects = entry.defects.filter((d) => d.stageNo === info.no);
                const stageDefect = defects.reduce((sum, d) => sum + (num(d.kg) ?? 0), 0);
                return (
                  <section key={info.no} className={`rounded-2xl border bg-surface p-4 ${s.done ? "border-ok/40" : "border-line"}`} aria-label={info.name}>
                    <div className="mb-3 flex flex-wrap items-center gap-2">
                      <span className="grid size-7 place-items-center rounded-lg bg-accent-soft text-sm font-semibold tabular-nums text-accent-strong">{info.no}</span>
                      <h2 className="text-base font-semibold text-ink">{info.name}</h2>
                      {t(stageKindLabel[info.kind]) !== info.name && <span className="text-xs text-ink-3">{t(stageKindLabel[info.kind])}</span>}
                      <label className={`ml-auto inline-flex h-10 cursor-pointer items-center gap-2 rounded-lg border px-3 text-sm font-medium ${s.done ? "border-ok/40 bg-ok-soft text-ok" : "border-line text-ink"}`}>
                        <input type="checkbox" checked={s.done} onChange={(e) => setStage(info.no, { done: e.target.checked })} className="size-5 accent-[var(--color-ok)]" />
                        {t("Этап заполнен")}
                      </label>
                    </div>

                    {info.kind === "Baking" && (
                      <div className="mb-3 rounded-xl bg-muted/60 p-3">
                        <div className="grid grid-cols-2 gap-3 sm:max-w-md">
                          <label className="block">
                            <span className={mini}>{t("До выпечки, кг")}</span>
                            <input value={s.inputKg} onChange={(e) => setStage(info.no, { inputKg: e.target.value })} inputMode="decimal" className={`${big} text-right tabular-nums`} />
                          </label>
                          <label className="block">
                            <span className={mini}>{t("После выпечки, кг")}</span>
                            <input value={s.outputKg} onChange={(e) => setStage(info.no, { outputKg: e.target.value })} inputMode="decimal" className={`${big} text-right tabular-nums`} />
                          </label>
                        </div>
                        {bake && (
                          <p className={`mt-2 text-sm tabular-nums ${bake.outOfRange ? "font-medium text-bad" : "text-ink-2"}`}>
                            {t("Выход {y} % (норма {n} %{range}) · потеря {kg} кг ({p} %)", {
                              y: quantity(bake.yieldPercent),
                              n: quantity(info.yieldPercent),
                              range: info.yieldMinPercent !== null || info.yieldMaxPercent !== null ? `, ${quantity(info.yieldMinPercent ?? 0)}–${quantity(info.yieldMaxPercent ?? 0)}` : "",
                              kg: quantity(bake.lossKg),
                              p: quantity(bake.lossPercent),
                            })}
                            {bake.outOfRange && (
                              <span className="ml-1 inline-flex items-center gap-1">
                                <AlertTriangle className="size-3.5" aria-hidden /> {t("вне допуска")}
                              </span>
                            )}
                          </p>
                        )}
                        {(bake?.outOfRange || s.reason) && (
                          <label className="mt-2 block">
                            <span className={mini}>{t("Причина отклонения")}</span>
                            <input value={s.reason} onChange={(e) => setStage(info.no, { reason: e.target.value })} maxLength={1000} placeholder={t("Например: пересушили, печь перегрелась")} className={big} />
                          </label>
                        )}
                      </div>
                    )}

                    <div className="mb-3 flex flex-wrap gap-3">
                      {info.moistureNorm !== null && (
                        <label className="block w-44">
                          <span className={mini}>
                            {t("Влажность, {unit}", { unit: info.moistureUnit ?? "%" })} · {t("норма {n}", { n: quantity(info.moistureNorm) })}
                            {info.moistureTolerance !== null && ` ± ${quantity(info.moistureTolerance)}`}
                          </span>
                          <input value={s.moisture} onChange={(e) => setStage(info.no, { moisture: e.target.value })} inputMode="decimal" className={`${big} text-right tabular-nums ${moistureOff ? "border-bad" : ""}`} />
                        </label>
                      )}
                      <label className="block w-44">
                        <span className={mini}>{t("Из переработки, кг")}</span>
                        <input value={s.reworkKg} onChange={(e) => setStage(info.no, { reworkKg: e.target.value })} inputMode="decimal" placeholder="0" className={`${big} text-right tabular-nums`} />
                      </label>
                    </div>

                    <h3 className="mb-2 text-sm font-semibold text-ink">
                      {t("Брак продукта")}{" "}
                      <span className="font-normal tabular-nums text-ink-3">
                        · {quantity(Math.round(stageDefect * 1000) / 1000)} {t("кг")}
                        {info.defectPercent > 0 && ` · ${t("норматив {p} %", { p: quantity(info.defectPercent) })}`}
                      </span>
                    </h3>
                    {defects.length > 0 && (
                      <ul className="mb-2 space-y-2">
                        {defects.map((d) => {
                          const type = productTypes.find((x) => x.id === d.defectTypeId);
                          const reworkable = type?.reworkable ?? info.reworkable;
                          return (
                            <li key={d.key} className="grid grid-cols-[minmax(0,1fr)_104px_40px] gap-2 xl:grid-cols-[minmax(0,1fr)_120px_210px_40px]">
                              <select value={d.defectTypeId ?? ""} onChange={(e) => change((x) => ({ ...x, defects: x.defects.map((y) => (y.key === d.key ? { ...y, defectTypeId: e.target.value || null, rework: "Forbidden" } : y)) }))}
                                aria-label={t("Вид брака")} className={big}>
                                {productTypes.map((p) => (
                                  <option key={p.id} value={p.id}>
                                    {p.name}
                                  </option>
                                ))}
                                {d.defectTypeId === null && <option value="">{d.name || t("Другой")}</option>}
                              </select>
                              <input value={d.kg} onChange={(e) => change((x) => ({ ...x, defects: x.defects.map((y) => (y.key === d.key ? { ...y, kg: e.target.value } : y)) }))}
                                inputMode="decimal" placeholder={t("кг")} aria-label={t("Брак, кг")} className={`${big} text-right tabular-nums`} />
                              <select value={reworkable ? d.rework : "Forbidden"} disabled={!reworkable}
                                onChange={(e) => change((x) => ({ ...x, defects: x.defects.map((y) => (y.key === d.key ? { ...y, rework: e.target.value as ReworkStatus } : y)) }))}
                                aria-label={t("Переработка")} className={`${big} col-span-2 text-sm xl:col-span-1 max-xl:order-last`}>
                                {(reworkable ? (["Allowed", "Sent"] as const) : (["Forbidden"] as const)).map((r) => (
                                  <option key={r} value={r}>
                                    {t(reworkLabel[r])}
                                  </option>
                                ))}
                              </select>
                              <button type="button" onClick={() => change((x) => ({ ...x, defects: x.defects.filter((y) => y.key !== d.key) }))} aria-label={t("Убрать брак")}
                                className="grid size-11 place-items-center rounded-lg text-ink-3 hover:text-bad">
                                <Trash2 className="size-4" aria-hidden />
                              </button>
                            </li>
                          );
                        })}
                      </ul>
                    )}
                    {productTypes.length > 0 ? (
                      <button type="button"
                        onClick={() => change((x) => ({ ...x, defects: [...x.defects, { key: newKey(), stageNo: info.no, defectTypeId: productTypes[0].id, name: "", kg: "", rework: productTypes[0].reworkable ? "Allowed" : "Forbidden" }] }))}
                        className={`${secondary} h-10`}>
                        <Plus className="size-4" aria-hidden /> {t("Брак")}
                      </button>
                    ) : (
                      <p className="text-xs text-ink-3">{t("Видов брака продукта на этапе нет — технолог задаёт их в карте.")}</p>
                    )}

                    {info.materials.length > 0 && (
                      <div className="mt-4">
                        <h3 className="mb-2 text-sm font-semibold text-ink">
                          {t("Материалы этапа")} <span className="font-normal text-ink-3">· {t("брак и взвешенный расход, если есть")}</span>
                        </h3>
                        <ul className="space-y-2">
                          {info.materials.map((m) => (
                            <li key={m.itemId} className="grid grid-cols-2 items-end gap-2 sm:grid-cols-[minmax(0,1fr)_150px_150px]">
                              <span className="col-span-2 text-sm text-ink sm:col-span-1 sm:pb-3">{m.name}</span>
                              {(["Defect", "Weighed"] as const).map((kind) => {
                                const k = materialKey(kind, info.no, m.itemId);
                                return (
                                  <label key={kind} className="block">
                                    <span className={mini}>{kind === "Defect" ? t("Брак, {unit}", { unit: t(m.unitCode) }) : t("Взвешено, {unit}", { unit: t(m.unitCode) })}</span>
                                    <input value={entry.materials[k] ?? ""} onChange={(e) => change((x) => ({ ...x, materials: { ...x.materials, [k]: e.target.value } }))} inputMode="decimal"
                                      placeholder="—" className={`${big} text-right tabular-nums`} />
                                  </label>
                                );
                              })}
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}
                  </section>
                );
              })}

              {shift.template.length === 0 && (shift.ingredients?.length ?? 0) > 0 && (
                <section className="rounded-2xl border border-line bg-surface p-4" aria-label={t("Сырьё")}>
                  <h2 className="mb-2 text-base font-semibold text-ink">
                    {t("Сырьё")} <span className="text-sm font-normal text-ink-3">· {t("взвешенный расход, если взвешивали")}</span>
                  </h2>
                  <ul className="space-y-2">
                    {shift.ingredients!.map((m) => {
                      const k = materialKey("Weighed", null, m.itemId);
                      return (
                        <li key={m.itemId} className="grid grid-cols-2 items-end gap-2 sm:grid-cols-[minmax(0,1fr)_150px]">
                          <span className="text-sm text-ink sm:pb-3">{m.name}</span>
                          <label className="block">
                            <span className={mini}>{t("Взвешено, {unit}", { unit: t(m.unitCode) })}</span>
                            <input value={entry.materials[k] ?? ""} onChange={(e) => change((x) => ({ ...x, materials: { ...x.materials, [k]: e.target.value } }))} inputMode="decimal"
                              placeholder="—" className={`${big} text-right tabular-nums`} />
                          </label>
                        </li>
                      );
                    })}
                  </ul>
                </section>
              )}

              <section className="rounded-2xl border border-line bg-surface p-4">
                <label className="block">
                  <span className={mini}>{t("Комментарий к смене")}</span>
                  <textarea value={entry.comment} onChange={(e) => change((x) => ({ ...x, comment: e.target.value }))} maxLength={2000} rows={3}
                    className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-base text-ink focus:border-accent focus:outline-none disabled:opacity-70" />
                </label>
              </section>
            </fieldset>

            <div className="space-y-4">
              {editable && (
                <section className={`rounded-2xl border p-4 ${server.problems.length > 0 ? "border-warn/40 bg-warn-soft" : "border-ok/40 bg-ok-soft"}`}>
                  <h2 className="mb-1 text-sm font-semibold text-ink">{server.problems.length > 0 ? t("Чтобы закрыть смену") : t("Смену можно закрыть")}</h2>
                  {server.problems.length > 0 ? (
                    <ul className="list-disc space-y-0.5 pl-5 text-sm text-ink">
                      {server.problems.map((p) => (
                        <li key={p}>{t(p)}</li>
                      ))}
                    </ul>
                  ) : (
                    <p className="text-sm text-ink-2">{t("Все данные внесены. После закрытия смену утверждает мастер или начальник производства.")}</p>
                  )}
                  {state !== "saved" && <p className="mt-2 text-xs text-ink-3">{t("Проверка обновится после сохранения.")}</p>}
                </section>
              )}
              <section className="rounded-2xl border border-line bg-surface p-4 text-sm">
                <h2 className="mb-2 text-base font-semibold text-ink">{t("Смена")}</h2>
                <dl className="space-y-1.5">
                  <div className="flex justify-between gap-2"><dt className="text-ink-3">{t("Задание")}</dt><dd><Link href={`/production/orders/${shift.orderId}`} className="text-accent-strong hover:underline">{shift.orderNumber}</Link></dd></div>
                  <div className="flex justify-between gap-2"><dt className="text-ink-3">{t("Рецептура")}</dt><dd className="text-right"><Link href={`/production/recipes/${shift.recipeId}`} className="text-accent-strong hover:underline">{shift.recipeCode} v{shift.recipeVersion}</Link></dd></div>
                  <div className="flex justify-between gap-2"><dt className="text-ink-3">{t("Цех")}</dt><dd className="text-right text-ink">{shift.departmentName}</dd></div>
                  {shift.closedAt && <div className="flex justify-between gap-2"><dt className="text-ink-3">{t("Закрыта")}</dt><dd className="text-right text-ink">{stampRu(shift.closedAt)}<span className="block text-xs text-ink-3">{shift.closedByName}</span></dd></div>}
                  {shift.approvedAt && <div className="flex justify-between gap-2"><dt className="text-ink-3">{t("Утверждена")}</dt><dd className="text-right text-ink">{stampRu(shift.approvedAt)}<span className="block text-xs text-ink-3">{shift.approvedByName}</span></dd></div>}
                </dl>
              </section>
              <section className="rounded-2xl border border-line bg-surface p-4">
                <h2 className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
                  <History className="size-4 text-ink-3" aria-hidden /> {t("История")}
                </h2>
                <ol className="space-y-3 border-l border-line pl-4">
                  {[...shift.history].reverse().map((e) => (
                    <li key={e.id} className="text-sm">
                      <p className="text-ink">{t(e.text)}</p>
                      {e.comment && <p className="mt-1 rounded-lg bg-muted px-2.5 py-1.5 text-xs text-ink-2">{e.comment}</p>}
                      <p className="text-xs text-ink-3">
                        {e.actorName} · {stampRu(e.createdAt)}
                      </p>
                    </li>
                  ))}
                </ol>
              </section>
            </div>
          </div>

          <details className="rounded-2xl border border-line bg-surface p-4" open={!editable}>
            <summary className="cursor-pointer text-base font-semibold text-ink">
              {t("Расчёт по результатам смены")} <span className="text-sm font-normal text-ink-3">· {t("норма на то, что сделала смена, с фактическим браком")}</span>
            </summary>
            <div className="mt-3">
              <RecipeCalcResult calc={server.calculation} />
            </div>
          </details>
        </div>
      </PageBody>

      <Sheet
        open={dialog !== null}
        onClose={() => setDialog(null)}
        title={dialog === "cancel" ? t("Отменить смену") : t("Вернуть смену на ввод")}
        subtitle={shift.number}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setDialog(null)} className={secondary}>
              {t("Закрыть")}
            </button>
            <button type="button" onClick={() => void act(dialog === "cancel" ? "cancel" : "return", { comment: note.trim() || null })} disabled={busy} className={primary}>
              {busy && <Loader2 className="size-4 animate-spin" aria-hidden />} {dialog === "cancel" ? t("Отменить смену") : t("Вернуть")}
            </button>
          </div>
        }
      >
        <textarea value={note} onChange={(e) => setNote(e.target.value)} maxLength={1000} rows={3} placeholder={dialog === "cancel" ? t("Почему отменяется") : t("Что исправить")}
          className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink" />
        <div className="mt-3">
          <ErrorNote text={error} />
        </div>
      </Sheet>
    </>
  );
}
