"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { FileCheck2, FlaskConical, Loader2, Plus, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { qualityControlLabel } from "@/lib/procurement";
import { normText, specProblem, type QualitySpecsView, type SpecDraft, type SpecKind } from "@/lib/quality";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";

let seq = 0;
const blank = (kind: SpecKind): SpecDraft => ({ key: `s-${Date.now()}-${seq++}`, id: null, kind, name: "", unit: "", min: "", max: "", expected: "", method: "", required: true });
const text = (v: number | null) => (v === null ? "" : String(v).replace(".", ","));
const num = (v: string) => (v.trim() ? Number(v.replace(/\s/g, "").replace(",", ".")) : null);

/**
 * Спецификация материала: какие документы проверяют при приходе и какие показатели и нормы у лаборатории. Смотрят все, кто видит склад и
 * качество; правит лаборатория (canEdit с сервера). Убранная строка отключается — прошлые проверки хранят свою норму.
 */
export function QualitySpecSheet({ itemId, onClose }: { itemId: string; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const [view, setView] = useState<QualitySpecsView | null>(null);
  const [rows, setRows] = useState<SpecDraft[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let alive = true;
    bff<QualitySpecsView>(`quality/specs/${itemId}`)
      .then((v) => {
        if (!alive) return;
        setView(v);
        setRows(
          v.lines.map((l) => ({
            key: l.id,
            id: l.id,
            kind: l.kind,
            name: l.name,
            unit: l.unit ?? "",
            min: text(l.min),
            max: text(l.max),
            expected: l.expected ?? "",
            method: l.method ?? "",
            required: l.required,
          })),
        );
      })
      .catch((e: Error) => alive && setError(e.message));
    return () => {
      alive = false;
    };
  }, [itemId]);

  const patch = (key: string, change: Partial<SpecDraft>) => setRows((all) => all.map((r) => (r.key === key ? { ...r, ...change } : r)));
  const edit = !!view?.canEdit;

  const save = async () => {
    setError(null);
    for (const r of rows) {
      const problem = specProblem(r);
      if (problem) return setError(`${r.name.trim() ? `«${r.name.trim()}»: ` : ""}${t(problem)}`);
    }
    setBusy(true);
    try {
      await bff(`quality/specs/${itemId}`, {
        method: "PUT",
        body: JSON.stringify({
          lines: rows.map((r) => ({
            id: r.id,
            kind: r.kind,
            name: r.name.trim(),
            unit: r.kind === "Parameter" ? r.unit.trim() || null : null,
            min: r.kind === "Parameter" ? num(r.min) : null,
            max: r.kind === "Parameter" ? num(r.max) : null,
            expected: r.kind === "Parameter" ? r.expected.trim() || null : null,
            method: r.method.trim() || null,
            required: r.required,
          })),
        }),
      });
      onClose();
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const group = (kind: SpecKind) => rows.filter((r) => r.kind === kind);
  const section = (kind: SpecKind) => (
    <section className="space-y-2">
      <h3 className="flex items-center gap-2 text-sm font-semibold text-ink">
        {kind === "Document" ? <FileCheck2 className="size-4 text-ink-3" aria-hidden /> : <FlaskConical className="size-4 text-ink-3" aria-hidden />}
        {t(kind === "Document" ? "Документы при приходе" : "Показатели лаборатории")}
      </h3>
      {group(kind).length === 0 && <p className="text-sm text-ink-3">{t(kind === "Document" ? "Документы не заданы." : "Показатели не заданы.")}</p>}
      {group(kind).map((r) =>
        edit ? (
          <div key={r.key} className="rounded-xl border border-line p-2.5">
            <div className="grid gap-2 sm:grid-cols-[1fr_auto]">
              <input
                value={r.name}
                onChange={(e) => patch(r.key, { name: e.target.value })}
                maxLength={200}
                placeholder={t(kind === "Document" ? "Сертификат качества" : "Влажность")}
                aria-label={t("Название")}
                className={field}
              />
              <div className="flex items-center gap-2">
                <label className="flex items-center gap-1.5 text-xs text-ink-2">
                  <input type="checkbox" checked={r.required} onChange={(e) => patch(r.key, { required: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
                  {t("обязательно")}
                </label>
                <button type="button" onClick={() => setRows((all) => all.filter((x) => x.key !== r.key))} aria-label={t("Убрать строку")} className="grid size-9 place-items-center rounded-md text-ink-3 hover:text-bad">
                  <X className="size-4" aria-hidden />
                </button>
              </div>
            </div>
            {kind === "Parameter" && (
              <div className="mt-2 grid grid-cols-3 gap-2 sm:grid-cols-[6rem_6rem_6rem_1fr]">
                <input value={r.min} onChange={(e) => patch(r.key, { min: e.target.value })} inputMode="decimal" placeholder={t("от")} aria-label={t("Норма от")} className={`${field} tabular-nums`} />
                <input value={r.max} onChange={(e) => patch(r.key, { max: e.target.value })} inputMode="decimal" placeholder={t("до")} aria-label={t("Норма до")} className={`${field} tabular-nums`} />
                <input value={r.unit} onChange={(e) => patch(r.key, { unit: e.target.value })} maxLength={32} placeholder="%" aria-label={t("Единица")} className={field} />
                <input
                  value={r.expected}
                  onChange={(e) => patch(r.key, { expected: e.target.value })}
                  maxLength={300}
                  placeholder={t("или каким должен быть: «без посторонних запахов»")}
                  aria-label={t("Ожидаемое")}
                  className={`${field} col-span-3 sm:col-span-1`}
                />
              </div>
            )}
            <input
              value={r.method}
              onChange={(e) => patch(r.key, { method: e.target.value })}
              maxLength={300}
              placeholder={t(kind === "Document" ? "Что проверить: номер партии и срок совпадают с накладной" : "Метод: ГОСТ, прибор")}
              aria-label={t("Метод")}
              className={`${field} mt-2`}
            />
          </div>
        ) : (
          <div key={r.key} className="flex items-start justify-between gap-3 rounded-xl border border-line px-3 py-2 text-sm">
            <span className="min-w-0">
              <span className="block text-ink">
                {r.name}
                {!r.required && <span className="text-ink-3"> · {t("необязательно")}</span>}
              </span>
              {r.method && <span className="block text-xs text-ink-3">{r.method}</span>}
            </span>
            {kind === "Parameter" && (
              <span className="shrink-0 tabular-nums text-ink-2">{normText({ min: num(r.min), max: num(r.max), expected: r.expected || null, unit: r.unit || null })}</span>
            )}
          </div>
        ),
      )}
      {edit && (
        <button type="button" onClick={() => setRows((all) => [...all, blank(kind)])} className="inline-flex h-8 items-center gap-1.5 rounded-lg px-2 text-xs font-semibold text-accent-strong hover:bg-accent-soft">
          <Plus className="size-3.5" aria-hidden /> {t(kind === "Document" ? "Документ" : "Показатель")}
        </button>
      )}
    </section>
  );

  return (
    <Sheet
      open
      onClose={onClose}
      title={t("Спецификация материала")}
      subtitle={view ? `${view.sku} · ${view.name}` : undefined}
      size="xl"
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
            {t("Закрыть")}
          </button>
          {edit && (
            <button
              type="button"
              onClick={() => void save()}
              disabled={busy}
              className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50"
            >
              {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {t("Сохранить")}
            </button>
          )}
        </div>
      }
    >
      {!view && !error && (
        <p className="flex items-center gap-2 py-8 text-sm text-ink-3">
          <Loader2 className="size-4 animate-spin" aria-hidden /> {t("Загрузка")}
        </p>
      )}
      {view && (
        <div className="space-y-5">
          <p className="rounded-xl bg-muted px-3 py-2 text-sm text-ink-2">
            {t("Контроль при приходе: {mode}.", { mode: t(qualityControlLabel[view.control]).toLowerCase() })}{" "}
            {view.control === "None"
              ? t("Партии допускаются сразу — спецификация пригодится, когда контроль включат в карточке позиции.")
              : view.control === "Documents"
                ? t("Проверяют документы; показатели лаборатории не нужны.")
                : t("Сначала документы, затем показатели лаборатории.")}
          </p>
          {section("Document")}
          {section("Parameter")}
        </div>
      )}
      <div className="mt-3">
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
