"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Check, CheckCircle2, CircleDashed, FileCheck2, FlaskConical, Gavel, Loader2, X, XCircle } from "lucide-react";
import { DocumentsBlock } from "@/components/procurement/ProcKit";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { batchQualityLabel, dateRu, qualityCheckStatusLabel, qualityControlLabel, quantity, stampRu } from "@/lib/procurement";
import { measured, normText, parameterPassed, qualitySteps, stepState, type QualityCheckCard, type QualityResult, type QualitySpec } from "@/lib/quality";
import { CheckStatus } from "./QualityQueue";
import { QualitySpecSheet } from "./QualitySpecSheet";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const card = "rounded-xl border border-line bg-surface p-4";
const primaryBtn = "inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

/** Итог строки: ✓ в норме, ✗ нет, — не проверено. */
function Mark({ passed }: { passed: boolean | null }) {
  const t = useT();
  if (passed === true) return <CheckCircle2 className="size-4 shrink-0 text-ok" aria-label={t("в порядке")} />;
  if (passed === false) return <XCircle className="size-4 shrink-0 text-bad" aria-label={t("не в порядке")} />;
  return <CircleDashed className="size-4 shrink-0 text-ink-3" aria-label={t("не проверено")} />;
}

/** Выбор «да / нет» для документа или качественного показателя. */
function YesNo({ value, onChange, yes, no }: { value: boolean | null; onChange: (v: boolean) => void; yes: string; no: string }) {
  const t = useT();
  return (
    <div className="inline-flex shrink-0 rounded-lg bg-muted p-0.5">
      <button
        type="button"
        onClick={() => onChange(true)}
        className={`inline-flex h-8 items-center gap-1 rounded-md px-2.5 text-xs font-medium ${value === true ? "bg-surface text-ok shadow-sm" : "text-ink-2 hover:text-ink"}`}
      >
        <Check className="size-3.5" aria-hidden /> {t(yes)}
      </button>
      <button
        type="button"
        onClick={() => onChange(false)}
        className={`inline-flex h-8 items-center gap-1 rounded-md px-2.5 text-xs font-medium ${value === false ? "bg-surface text-bad shadow-sm" : "text-ink-2 hover:text-ink"}`}
      >
        <X className="size-3.5" aria-hidden /> {t(no)}
      </button>
    </div>
  );
}

/** Результаты шага (снимок нормы на момент проверки). */
function Results({ rows }: { rows: QualityResult[] }) {
  const t = useT();
  if (rows.length === 0) return null;
  return (
    <ul className="divide-y divide-line rounded-lg border border-line">
      {rows.map((r) => (
        <li key={`${r.kind}-${r.lineNo}`} className="flex items-center gap-3 px-3 py-2 text-sm">
          <Mark passed={r.passed} />
          <span className="min-w-0 flex-1">
            <span className="block text-ink">{r.name}</span>
            {r.kind === "Parameter" && <span className="block text-xs text-ink-3">{t("норма {norm}", { norm: normText(r) })}</span>}
          </span>
          {r.kind === "Parameter" && (
            <span className={`shrink-0 tabular-nums ${r.passed === false ? "font-semibold text-bad" : "text-ink"}`}>
              {r.value !== null
                ? `${quantity(r.value)}${r.unit ? ` ${r.unit}` : ""}`
                : (r.text ?? (r.passed === null ? "—" : t(r.passed ? "соответствует" : "не соответствует")))}
            </span>
          )}
        </li>
      ))}
    </ul>
  );
}

/**
 * Проверка качества партии: полоса шагов, данные партии, шаг «документы» (отметки по спецификации), «лаборатория» (показатели с нормой,
 * проба — списывается с партии), «решение» (допустить или забраковать — назначенный), документы проверки и партии, история. Кнопки — по
 * правам с сервера; сервер проверяет порядок шагов и права при каждом действии.
 */
export function QualityCheckView({ card: data }: { card: QualityCheckCard }) {
  const t = useT();
  const c = data.check;
  const [spec, setSpec] = useState(false);
  const docs = data.results.filter((r) => r.kind === "Document");
  const params = data.results.filter((r) => r.kind === "Parameter");
  const steps = qualitySteps(c.control);
  const stepIcon = { Documents: FileCheck2, Lab: FlaskConical, Decision: Gavel } as const;

  return (
    <div className="space-y-5">
      <div className={`${card} flex flex-wrap items-center gap-3`}>
        <ol className="flex flex-1 flex-wrap items-center gap-2">
          {steps.map((s, i) => {
            const state = stepState(c.control, c.status, s);
            const Icon = stepIcon[s as keyof typeof stepIcon];
            return (
              <li key={s} className="flex items-center gap-2">
                {i > 0 && <span className="h-px w-5 bg-line" aria-hidden />}
                <span
                  className={`inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1 text-sm ${
                    state === "done" ? "bg-ok-soft text-ok" : state === "current" ? "bg-accent-soft font-semibold text-accent-strong" : "bg-muted text-ink-3"
                  }`}
                >
                  <Icon className="size-4" aria-hidden /> {t(qualityCheckStatusLabel[s])}
                </span>
              </li>
            );
          })}
        </ol>
        <CheckStatus row={c} />
      </div>

      <div className="grid gap-5 lg:grid-cols-[1fr_2fr]">
        <div className="space-y-5">
          <section className={card}>
            <dl className="space-y-2 text-sm">
              {[
                {
                  label: "Партия",
                  value: c.batchId ? <Link href={`/procurement/batches/${c.batchId}`} className="font-medium text-accent-strong hover:underline">{c.batchNumber}</Link> : null,
                },
                {
                  label: "Поставка",
                  value: c.arrivalId ? (
                    <>
                      <Link href={`/procurement/arrivals/${c.arrivalId}`} className="font-medium tabular-nums text-accent-strong hover:underline">
                        {c.arrivalNumber}
                      </Link>
                      {!c.batchId && <span className="text-ink-3"> · {t("товар в машине, на склад ещё не принят")}</span>}
                    </>
                  ) : null,
                },
                { label: "Номер партии производителя", value: c.supplierBatchNo },
                { label: "Позиция", value: `${c.itemName} · ${c.sku}` },
                { label: "Поставщик", value: c.supplierName },
                { label: c.batchId ? "Пришло" : "По накладной", value: `${quantity(c.received)} ${c.unitCode} · ${c.warehouseName}` },
                { label: "Сейчас на складах", value: c.batchId ? `${quantity(c.onHand)} ${c.unitCode}` : null },
                { label: "Годна до", value: c.expiresOn ? dateRu(c.expiresOn) : t("не указан") },
                { label: "Контроль", value: t(qualityControlLabel[c.control]) },
                { label: "Условия хранения", value: data.storageConditions },
                { label: "Проверка заведена", value: stampRu(c.createdAt) },
              ]
                .filter((f) => f.value)
                .map((f) => (
                  <div key={f.label}>
                    <dt className="text-xs text-ink-3">{t(f.label)}</dt>
                    <dd className="text-ink">{f.value}</dd>
                  </div>
                ))}
            </dl>
            <button type="button" onClick={() => setSpec(true)} className="mt-3 inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted">
              {t("Спецификация материала")}
            </button>
          </section>
          <section>
            <h2 className="mb-2 text-sm font-semibold text-ink">{t("Документы проверки")}</h2>
            <div className={card}>
              <DocumentsBlock owner="qc" id={c.id} documents={data.documents} can={data.can.attach} kinds={["lab_report", "certificate", "declaration", "other"]} />
            </div>
          </section>
          {c.batchId && (
            <section>
              <h2 className="mb-2 text-sm font-semibold text-ink">{t("Документы партии")}</h2>
              <div className={card}>
                <DocumentsBlock owner="batch" id={c.batchId} documents={data.batchDocuments} can={data.can.attach} kinds={["certificate", "declaration", "lab_report", "other"]} />
              </div>
            </section>
          )}
          {data.history.length > 0 && (
            <section>
              <h2 className="mb-2 text-sm font-semibold text-ink">{t("История")}</h2>
              <ul className={`${card} space-y-2 text-sm`}>
                {data.history.map((h) => (
                  <li key={h.id}>
                    <span className="block text-ink">{t(h.text)}</span>
                    <span className="block text-xs text-ink-3">{[h.actorName, stampRu(h.createdAt), h.comment].filter(Boolean).join(" · ")}</span>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </div>

        <div className="space-y-5">
          <DocumentsStep data={data} done={docs} />
          {c.control === "Lab" && <LabStep data={data} done={params} />}
          <DecisionStep data={data} />
        </div>
      </div>
      {spec && <QualitySpecSheet itemId={c.itemId} onClose={() => setSpec(false)} />}
    </div>
  );
}

/** Шаг «документы»: что проверено и кем; лаборатории — форма (по документам спецификации или итог целиком). */
function DocumentsStep({ data, done }: { data: QualityCheckCard; done: QualityResult[] }) {
  const t = useT();
  const router = useRouter();
  const specs = data.specs.filter((s) => s.kind === "Document");
  const [marks, setMarks] = useState<Record<string, boolean | null>>(() =>
    Object.fromEntries(specs.map((s) => [s.id, done.find((r) => r.specId === s.id)?.passed ?? null])),
  );
  const [ok, setOk] = useState<boolean | null>(data.check.documentsOk);
  const [note, setNote] = useState(data.documentsNote ?? "");
  const [editing, setEditing] = useState(data.can.documents && data.check.documentsOk === null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const checked = data.check.documentsOk !== null;
  const result = specs.length > 0 ? (specs.some((s) => marks[s.id] === false) ? false : specs.filter((s) => s.required).every((s) => marks[s.id] === true) ? true : null) : ok;

  const save = async () => {
    setError(null);
    const missing = specs.find((s) => s.required && marks[s.id] == null);
    if (missing) return setError(t("«{name}»: отметьте, есть ли документ и в порядке ли он.", { name: missing.name }));
    if (specs.length === 0 && ok === null) return setError(t("Отметьте, в порядке ли документы партии."));
    if (result === false && !note.trim()) return setError(t("Документы не в порядке — напишите, что именно (нет сертификата, не совпадает номер партии или срок)."));
    setBusy(true);
    try {
      await bff(`quality/checks/${data.check.id}/documents`, {
        method: "PUT",
        body: JSON.stringify({
          items: specs.filter((s) => marks[s.id] != null).map((s) => ({ specId: s.id, passed: marks[s.id] })),
          ok: specs.length === 0 ? ok : null,
          note: note.trim() || null,
          version: data.version,
        }),
      });
      setEditing(false);
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className={card}>
      <h2 className="flex flex-wrap items-center justify-between gap-2 text-sm font-semibold text-ink">
        <span className="inline-flex items-center gap-2">
          <FileCheck2 className="size-4 text-ink-3" aria-hidden /> {t("1. Документы")}
        </span>
        {checked && (
          <span className={`text-xs font-medium ${data.check.documentsOk ? "text-ok" : "text-bad"}`}>{t(data.check.documentsOk ? "в порядке" : "не в порядке")}</span>
        )}
      </h2>
      {checked && !editing && (
        <div className="mt-3 space-y-2">
          <Results rows={done} />
          {data.documentsNote && <p className="rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{data.documentsNote}</p>}
          <p className="text-xs text-ink-3">{[data.documentsByName, data.documentsAt ? stampRu(data.documentsAt) : null].filter(Boolean).join(" · ")}</p>
          {data.can.documents && (
            <button type="button" onClick={() => setEditing(true)} className="text-sm font-medium text-accent-strong hover:underline">
              {t("Проверить заново")}
            </button>
          )}
        </div>
      )}
      {!checked && !data.can.documents && <p className="mt-2 text-sm text-ink-3">{t("Ждёт лабораторию: проверка сертификата, декларации и того, что номер партии и срок совпадают с накладной.")}</p>}
      {editing && (
        <div className="mt-3 space-y-3">
          {specs.length > 0 ? (
            <ul className="space-y-2">
              {specs.map((s: QualitySpec) => (
                <li key={s.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-line px-3 py-2">
                  <span className="min-w-0 text-sm">
                    <span className="block text-ink">
                      {s.name}
                      {!s.required && <span className="text-ink-3"> · {t("необязательно")}</span>}
                    </span>
                    {s.method && <span className="block text-xs text-ink-3">{s.method}</span>}
                  </span>
                  <YesNo value={marks[s.id] ?? null} onChange={(v) => setMarks((m) => ({ ...m, [s.id]: v }))} yes="в порядке" no="нет" />
                </li>
              ))}
            </ul>
          ) : (
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-line px-3 py-2 text-sm">
              <span className="text-ink-2">{t("Документы партии (сертификат, декларация) есть и совпадают с накладной?")}</span>
              <YesNo value={ok} onChange={setOk} yes="в порядке" no="нет" />
            </div>
          )}
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">
              {t("Замечание")} {result !== false && <span className="font-normal text-ink-3">{t("необязательно")}</span>}
            </span>
            <textarea value={note} onChange={(e) => setNote(e.target.value)} maxLength={2000} rows={2} className={`${field} h-auto py-2.5`} />
          </label>
          <ErrorNote text={error} />
          <div className="flex flex-wrap gap-2">
            <button type="button" onClick={() => void save()} disabled={busy} className={primaryBtn}>
              {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {t("Сохранить проверку документов")}
            </button>
            {checked && (
              <button type="button" onClick={() => setEditing(false)} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
                {t("Отмена")}
              </button>
            )}
          </div>
        </div>
      )}
    </section>
  );
}

/** Шаг «лаборатория»: показатели (норма, значение, итог), проба; лаборатории — форма после документов в порядке. */
function LabStep({ data, done }: { data: QualityCheckCard; done: QualityResult[] }) {
  const t = useT();
  const router = useRouter();
  const c = data.check;
  const specs = data.specs.filter((s) => s.kind === "Parameter");
  const prior = (s: QualitySpec) => done.find((r) => r.specId === s.id);
  const [values, setValues] = useState<Record<string, string>>(() =>
    Object.fromEntries(specs.map((s) => [s.id, prior(s)?.value != null ? String(prior(s)!.value).replace(".", ",") : ""])),
  );
  const [marks, setMarks] = useState<Record<string, boolean | null>>(() => Object.fromEntries(specs.map((s) => [s.id, measured(s) ? null : (prior(s)?.passed ?? null)])));
  const [texts, setTexts] = useState<Record<string, string>>(() => Object.fromEntries(specs.map((s) => [s.id, prior(s)?.text ?? ""])));
  const [ok, setOk] = useState<boolean | null>(c.labOk);
  const [note, setNote] = useState(data.labNote ?? "");
  const [sample, setSample] = useState(data.sampleQty !== null ? String(data.sampleQty).replace(".", ",") : "");
  const stocked = data.warehouses.filter((w) => w.onHand > 0);
  const [warehouse, setWarehouse] = useState(stocked.find((w) => w.warehouseId === c.warehouseId)?.warehouseId ?? stocked[0]?.warehouseId ?? c.warehouseId);
  const [editing, setEditing] = useState(data.can.lab && c.labOk === null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const checked = c.labOk !== null;

  const save = async () => {
    setError(null);
    for (const s of specs) {
      const passed = parameterPassed(s, values[s.id] ?? "", marks[s.id] ?? null);
      if (s.required && passed === null) return setError(t(measured(s) ? "«{name}»: укажите значение показателя." : "«{name}»: отметьте, соответствует ли показатель.", { name: s.name }));
    }
    if (specs.length === 0 && ok === null) return setError(t("Отметьте итог лаборатории: соответствует или нет."));
    const qty = sample.trim() ? Number(sample.replace(/\s/g, "").replace(",", ".")) : null;
    if (qty !== null && (!Number.isFinite(qty) || qty < 0)) return setError(t("Проба — больше нуля, не больше трёх знаков после запятой."));
    setBusy(true);
    try {
      await bff(`quality/checks/${c.id}/lab`, {
        method: "PUT",
        body: JSON.stringify({
          items: specs.map((s) =>
            measured(s)
              ? { specId: s.id, value: values[s.id]?.trim() ? Number(values[s.id].replace(/\s/g, "").replace(",", ".")) : null, text: texts[s.id]?.trim() || null }
              : { specId: s.id, passed: marks[s.id], text: texts[s.id]?.trim() || null },
          ),
          ok: specs.length === 0 ? ok : null,
          note: note.trim() || null,
          sampleQty: qty,
          sampleWarehouseId: qty ? warehouse : null,
          version: data.version,
        }),
      });
      setEditing(false);
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className={card}>
      <h2 className="flex flex-wrap items-center justify-between gap-2 text-sm font-semibold text-ink">
        <span className="inline-flex items-center gap-2">
          <FlaskConical className="size-4 text-ink-3" aria-hidden /> {t("2. Лаборатория")}
        </span>
        {checked && <span className={`text-xs font-medium ${c.labOk ? "text-ok" : "text-bad"}`}>{t(c.labOk ? "соответствует" : "не соответствует")}</span>}
      </h2>
      {!checked && !editing && (
        <p className="mt-2 text-sm text-ink-3">
          {c.documentsOk === null
            ? t("После проверки документов.")
            : c.documentsOk === false
              ? t("Документы не в порядке — лаборатория не нужна, партию обычно бракуют.")
              : t("Ждёт лабораторию: отбор пробы и показатели по спецификации.")}
        </p>
      )}
      {checked && !editing && (
        <div className="mt-3 space-y-2">
          <Results rows={done} />
          {data.sampleQty !== null && (
            <p className="text-sm text-ink-2">
              {t("Проба: {n} {unit}", { n: quantity(data.sampleQty), unit: c.unitCode })}
              {data.sampleDocumentNumber && <span className="text-ink-3"> · {t("списано {doc}", { doc: data.sampleDocumentNumber })}</span>}
            </p>
          )}
          {data.labNote && <p className="rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{data.labNote}</p>}
          <p className="text-xs text-ink-3">{[data.labByName, data.labAt ? stampRu(data.labAt) : null].filter(Boolean).join(" · ")}</p>
          {data.can.lab && (
            <button type="button" onClick={() => setEditing(true)} className="text-sm font-medium text-accent-strong hover:underline">
              {t("Поправить результаты")}
            </button>
          )}
        </div>
      )}
      {editing && (
        <div className="mt-3 space-y-3">
          {specs.length > 0 ? (
            <ul className="space-y-2">
              {specs.map((s) => {
                const passed = parameterPassed(s, values[s.id] ?? "", marks[s.id] ?? null);
                return (
                  <li key={s.id} className="rounded-lg border border-line px-3 py-2">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <span className="min-w-0 text-sm">
                        <span className="block text-ink">
                          {s.name}
                          {!s.required && <span className="text-ink-3"> · {t("необязательно")}</span>}
                        </span>
                        <span className="block text-xs text-ink-3">{[t("норма {norm}", { norm: normText(s) }), s.method].filter(Boolean).join(" · ")}</span>
                      </span>
                      {measured(s) ? (
                        <span className="flex items-center gap-2">
                          <input
                            value={values[s.id] ?? ""}
                            onChange={(e) => setValues((v) => ({ ...v, [s.id]: e.target.value }))}
                            inputMode="decimal"
                            aria-label={t("Значение «{name}»", { name: s.name })}
                            className={`${field.replace("w-full ", "")} w-28 text-right tabular-nums ${passed === false ? "border-bad text-bad" : ""}`}
                          />
                          <span className="w-8 text-xs text-ink-3">{s.unit}</span>
                          <Mark passed={passed} />
                        </span>
                      ) : (
                        <YesNo value={marks[s.id] ?? null} onChange={(v) => setMarks((m) => ({ ...m, [s.id]: v }))} yes="соответствует" no="нет" />
                      )}
                    </div>
                    {passed === false && <p className="mt-1 text-xs text-bad">{t("Вне нормы — решение всё равно за назначенным по качеству.")}</p>}
                  </li>
                );
              })}
            </ul>
          ) : (
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-line px-3 py-2 text-sm">
              <span className="text-ink-2">{t("Показатели в спецификации не заданы — итог анализа:")}</span>
              <YesNo value={ok} onChange={setOk} yes="соответствует" no="нет" />
            </div>
          )}
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-ink">
                {t("Проба, {unit}", { unit: c.unitCode })}{" "}
                <span className="font-normal text-ink-3">{t(c.batchId ? "спишется с партии" : "из машины — со склада не списывается")}</span>
              </span>
              <input
                value={sample}
                onChange={(e) => setSample(e.target.value)}
                disabled={data.sampleQty !== null}
                inputMode="decimal"
                placeholder="0"
                className={`${field} tabular-nums disabled:bg-muted`}
              />
            </label>
            {c.batchId && stocked.length > 1 && data.sampleQty === null && (
              <label className="block">
                <span className="mb-1.5 block text-sm font-medium text-ink">{t("Склад пробы")}</span>
                <select value={warehouse} onChange={(e) => setWarehouse(e.target.value)} className={field}>
                  {stocked.map((w) => (
                    <option key={w.warehouseId} value={w.warehouseId}>
                      {`${w.name} · ${quantity(w.onHand)} ${c.unitCode}`}
                    </option>
                  ))}
                </select>
              </label>
            )}
          </div>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">
              {t("Заключение")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
            </span>
            <textarea value={note} onChange={(e) => setNote(e.target.value)} maxLength={2000} rows={2} className={`${field} h-auto py-2.5`} />
          </label>
          <ErrorNote text={error} />
          <div className="flex flex-wrap gap-2">
            <button type="button" onClick={() => void save()} disabled={busy} className={primaryBtn}>
              {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {t("Сохранить результаты")}
            </button>
            {checked && (
              <button type="button" onClick={() => setEditing(false)} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
                {t("Отмена")}
              </button>
            )}
          </div>
        </div>
      )}
    </section>
  );
}

/** Шаг «решение»: принятое решение или — назначенному — «допустить» (после всех шагов) и «забраковать» (с комментарием). */
function DecisionStep({ data }: { data: QualityCheckCard }) {
  const t = useT();
  const router = useRouter();
  const c = data.check;
  const [comment, setComment] = useState("");
  const [busy, setBusy] = useState<"approve" | "reject" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const deviates = c.documentsOk === false || c.labOk === false;
  const step = c.control === "Lab" ? "3" : "2";

  const decide = async (decision: "approve" | "reject") => {
    setError(null);
    if (decision === "reject" && !comment.trim()) return setError(t("Напишите, почему партия забракована."));
    if (decision === "approve" && deviates && !comment.trim()) return setError(t("Партия с отклонениями (документы или показатели) — чтобы допустить, напишите почему."));
    setBusy(decision);
    try {
      await bff(`quality/checks/${c.id}/decide`, { method: "POST", body: JSON.stringify({ decision, comment: comment.trim() || null, version: data.version }) });
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(null);
    }
  };

  return (
    <section className={card}>
      <h2 className="flex items-center gap-2 text-sm font-semibold text-ink">
        <Gavel className="size-4 text-ink-3" aria-hidden /> {t("{n}. Решение", { n: step })}
      </h2>
      {c.status === "Decided" && c.decision ? (
        <div className="mt-3 space-y-1 text-sm">
          <p className={`font-semibold ${c.decision === "Approved" ? "text-ok" : "text-bad"}`}>
            {t(c.batchId ? (c.decision === "Approved" ? "Партия допущена" : "Партия забракована") : c.decision === "Approved" ? "Допущено к приёмке" : "Забраковано — вернуть поставщику")}
          </p>
          {data.decisionComment && <p className="text-ink-2">{data.decisionComment}</p>}
          <p className="text-xs text-ink-3">{[c.decidedByName, c.decidedAt ? stampRu(c.decidedAt) : null].filter(Boolean).join(" · ")}</p>
          {c.decision === "Rejected" && (
            <p className="text-xs text-ink-3">
              {t(c.batchId ? "Кладовщик возвращает партию поставщику или списывает — в карточке партии." : "На склад не принимается — кладовщик возвращает поставщику с машиной.")}
            </p>
          )}
          {c.decision === "Approved" && !c.batchId && c.arrivalId && <p className="text-xs text-ink-3">{t("Кладовщик взвесит и примет на склад — партия появится при приёмке.")}</p>}
        </div>
      ) : c.status === "Cancelled" ? (
        <p className="mt-2 text-sm text-ink-3">{data.decisionComment ?? t("Проверка отменена.")}</p>
      ) : data.can.approve || data.can.reject ? (
        <div className="mt-3 space-y-3">
          {!data.can.approve && <p className="text-sm text-ink-3">{t("Допустить можно после всех проверок; забраковать — уже сейчас.")}</p>}
          {deviates && data.can.approve && (
            <p className="rounded-lg bg-warn-soft px-3 py-2 text-sm text-ink">{t("Есть отклонения: документы или показатели не в норме. Допуск — только с комментарием.")}</p>
          )}
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Комментарий")}</span>
            <textarea value={comment} onChange={(e) => setComment(e.target.value)} maxLength={2000} rows={2} className={`${field} h-auto py-2.5`} />
          </label>
          <ErrorNote text={error} />
          <div className="flex flex-wrap gap-2">
            {data.can.approve && (
              <button type="button" onClick={() => void decide("approve")} disabled={busy !== null} className={primaryBtn}>
                {busy === "approve" && <Loader2 className="size-4 animate-spin" aria-hidden />}
                <CheckCircle2 className="size-4" aria-hidden /> {t("Допустить")}
              </button>
            )}
            <button
              type="button"
              onClick={() => void decide("reject")}
              disabled={busy !== null}
              className="inline-flex h-10 items-center gap-2 rounded-lg border border-bad/40 bg-surface px-4 text-sm font-semibold text-bad hover:bg-bad-soft disabled:opacity-50"
            >
              {busy === "reject" && <Loader2 className="size-4 animate-spin" aria-hidden />}
              <XCircle className="size-4" aria-hidden /> {t("Забраковать")}
            </button>
          </div>
        </div>
      ) : (
        <p className="mt-2 text-sm text-ink-3">
          {c.status === "Decision"
            ? t("Ждёт решения назначенного по качеству.")
            : c.batchId
              ? t("Решение — после проверок. Партия до него {state} и не выдаётся.", { state: t(batchQualityLabel.Pending).toLowerCase() })
              : t("Решение — после проверок. Товар ждёт в машине: на склад его примут после допуска.")}
        </p>
      )}
    </section>
  );
}
