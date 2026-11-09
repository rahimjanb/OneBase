"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Fragment, useEffect, useState } from "react";
import { ChevronRight, ListPlus, Loader2, Plus, Save, Trash2, Workflow, X } from "lucide-react";
import { CatalogPicker } from "@/components/requests/CatalogPicker";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { parseNumber } from "@/lib/procurement";
import { basesFor, typicalChain, unitChoices, type ProductOption, type RecipeDetail, type RecipeFormData, type StageKind } from "@/lib/production";
import { Label, card, field, newKey, primary, secondary } from "./ProdKit";
import { StageCard, type CardParams, type DefectState, type LineState, type StageState } from "./RecipeStageEditor";

const text = (n: number | null | undefined) => (n === null || n === undefined ? "" : String(n).replace(".", ","));
const num = (s: string) => (s.trim() ? parseNumber(s) : null);

const blankStage = (name: string, kind: StageKind = "Other", yieldPercent = 100): StageState => ({
  key: newKey(),
  name,
  kind,
  area: "",
  yieldPct: text(yieldPercent),
  yieldMin: "",
  yieldMax: "",
  defectBase: "Output",
  moisture: "",
  moistureUnit: "%",
  moistureTol: "",
  reworkable: false,
  comment: "",
});

const blankLine = (stageKey: string | null, kind: StageKind | null): LineState => ({
  key: newKey(),
  stageKey,
  itemId: null,
  sku: null,
  name: "",
  itemUnit: "",
  itemUnits: [],
  unit: "",
  quantity: "",
  loss: "",
  basis: basesFor(kind)[0],
  group: "",
  unitKg: "",
});

/**
 * Технологическая карта (черновик версии): карточка продукта (код, название, товар Linko, номер карты, технолог, дата начала действия, выход,
 * на который заданы нормы); конструктор этапов — свой порядок у каждого продукта, компоненты и виды брака этапа, замес и упаковка внутри
 * своих этапов; без этапов — простой состав (прежняя формула). Добавить, убрать, переставить, скопировать этап. Правка — с версией.
 */
export function RecipeForm({ initial, form }: { initial?: RecipeDetail; form: RecipeFormData }) {
  const t = useT();
  const router = useRouter();
  const units = form.units;
  const [code, setCode] = useState(initial?.code ?? "");
  const [name, setName] = useState(initial?.name ?? "");
  const [product, setProduct] = useState<{ id: number; name: string } | null>(
    initial?.linkoProductId ? { id: initial.linkoProductId, name: initial.linkoProductName ?? String(initial.linkoProductId) } : null,
  );
  const [outputName, setOutputName] = useState(initial?.outputName ?? "");
  const [outputUnit, setOutputUnit] = useState(initial?.outputUnit ?? (units.some((u) => u.code === "кг") ? "кг" : (units[0]?.code ?? "")));
  const [outputQty, setOutputQty] = useState(text(initial?.outputQuantity ?? 1000));
  const [comment, setComment] = useState(initial?.comment ?? "");
  const [techCardNo, setTechCardNo] = useState(initial?.techCardNo ?? "");
  const [technologist, setTechnologist] = useState(initial?.technologistId ?? "");
  const [effectiveFrom, setEffectiveFrom] = useState(initial?.effectiveFrom ?? "");
  const [pieceKg, setPieceKg] = useState(text(initial?.pieceKg));
  const [outputItem, setOutputItem] = useState<{ id: string; name: string; sku: string | null; unit: string } | null>(
    initial?.outputItemId ? { id: initial.outputItemId, name: initial.outputItemName ?? "", sku: null, unit: initial.outputItemUnit ?? "" } : null,
  );
  const [params, setParams] = useState<CardParams>({
    batchMin: text(initial?.batchMinKg),
    batchMax: text(initial?.batchMaxKg),
    partial: initial?.partialBatches ?? false,
    packKg: text(initial?.packKg),
    perBox: text(initial?.packsPerBox),
  });
  const [stages, setStages] = useState<StageState[]>(
    initial?.stages.map((s) => ({
      key: s.id,
      name: s.name,
      kind: s.kind,
      area: s.area ?? "",
      yieldPct: text(s.yieldPercent),
      yieldMin: text(s.yieldMinPercent),
      yieldMax: text(s.yieldMaxPercent),
      defectBase: s.defectBase,
      moisture: text(s.moistureNorm),
      moistureUnit: s.moistureUnit ?? "%",
      moistureTol: text(s.moistureTolerance),
      reworkable: s.reworkable,
      comment: s.comment ?? "",
    })) ?? [],
  );
  const [lines, setLines] = useState<LineState[]>(
    initial?.lines.map((l) => ({
      key: l.id,
      stageKey: l.stageId,
      itemId: l.itemId,
      sku: l.sku,
      name: l.name,
      itemUnit: l.unitCode,
      itemUnits: l.itemUnits ?? [],
      unit: l.lineUnit ?? l.unitCode,
      quantity: text(l.quantity),
      loss: text(l.lossPercent || null),
      basis: l.basis,
      group: l.group ?? "",
      unitKg: text(l.unitKg),
    })) ?? [],
  );
  const [defects, setDefects] = useState<DefectState[]>(
    initial?.defectTypes.map((d) => ({ key: d.id, stageKey: d.stageId, name: d.name, norm: text(d.normPercent), itemId: d.itemId, reworkable: d.reworkable })) ?? [],
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Подсказки товаров Linko для выхода.
  const [productText, setProductText] = useState("");
  const [products, setProducts] = useState<ProductOption[]>([]);
  useEffect(() => {
    if (product || productText.trim().length < 2) {
      setProducts([]);
      return;
    }
    let alive = true;
    const timer = window.setTimeout(() => {
      bff<ProductOption[]>(`production/products?q=${encodeURIComponent(productText.trim())}`)
        .then((found) => alive && setProducts(found))
        .catch(() => alive && setProducts([]));
    }, 250);
    return () => {
      alive = false;
      window.clearTimeout(timer);
    };
  }, [productText, product]);

  const kindOf = (stageKey: string | null) => stages.find((s) => s.key === stageKey)?.kind ?? null;
  const setLine = (key: string, patch: Partial<LineState>) => setLines((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  const setDefect = (key: string, patch: Partial<DefectState>) => setDefects((ds) => ds.map((d) => (d.key === key ? { ...d, ...patch } : d)));

  // Тип этапа меняет допустимые базы нормы: замес — только «на замес», упаковка — на упаковку и коробку, прочие — на выход и штуку.
  const setStage = (key: string, patch: Partial<StageState>) => {
    setStages((ss) => ss.map((s) => (s.key === key ? { ...s, ...patch } : s)));
    if (patch.kind) {
      const bases = basesFor(patch.kind);
      setLines((ls) => ls.map((l) => (l.stageKey === key && !bases.includes(l.basis) ? { ...l, basis: bases[0] } : l)));
    }
  };

  // Первый этап забирает состав без этапов — у рецептуры с этапами каждый компонент на своём этапе.
  const addStages = (added: StageState[]) => {
    if (stages.length === 0 && added.length > 0) {
      const first = added[0];
      const bases = basesFor(first.kind);
      setLines((ls) => ls.map((l) => (l.stageKey === null ? { ...l, stageKey: first.key, basis: bases.includes(l.basis) ? l.basis : bases[0] } : l)));
    }
    setStages((ss) => [...ss, ...added]);
  };

  const move = (key: string, dir: -1 | 1) =>
    setStages((ss) => {
      const i = ss.findIndex((s) => s.key === key);
      const j = i + dir;
      if (i < 0 || j < 0 || j >= ss.length) return ss;
      const next = [...ss];
      [next[i], next[j]] = [next[j], next[i]];
      return next;
    });

  // Копия этапа — сразу после него, с компонентами и браком; второй замес не нужен — копия замеса становится «Другое».
  const copy = (key: string) => {
    const source = stages.find((s) => s.key === key);
    if (!source) return;
    const kind: StageKind = source.kind === "Mixing" ? "Other" : source.kind;
    const clone = { ...source, key: newKey(), name: `${source.name} (${t("копия")})`.slice(0, 100), kind };
    const bases = basesFor(kind);
    setStages((ss) => {
      const i = ss.findIndex((s) => s.key === key);
      return [...ss.slice(0, i + 1), clone, ...ss.slice(i + 1)];
    });
    setLines((ls) => [...ls, ...ls.filter((l) => l.stageKey === key).map((l) => ({ ...l, key: newKey(), stageKey: clone.key, basis: bases.includes(l.basis) ? l.basis : bases[0] }))]);
    setDefects((ds) => [...ds, ...ds.filter((d) => d.stageKey === key).map((d) => ({ ...d, key: newKey(), stageKey: clone.key }))]);
  };

  const remove = (key: string) => {
    setStages((ss) => ss.filter((s) => s.key !== key));
    setLines((ls) => ls.filter((l) => l.stageKey !== key));
    setDefects((ds) => ds.filter((d) => d.stageKey !== key));
  };

  const output = parseNumber(outputQty);
  const unstaged = lines.filter((l) => l.stageKey === null);

  const save = async () => {
    setError(null);
    if (!initial && !code.trim()) return setError(t("Укажите код рецептуры."));
    if (!name.trim()) return setError(t("Укажите название рецептуры."));
    if (output === null || output <= 0) return setError(t("Укажите выход рецептуры — на какое количество заданы нормы."));
    for (const [i, s] of stages.entries()) {
      if (!s.name.trim()) return setError(t("Этап {n}: укажите название.", { n: i + 1 }));
      const y = parseNumber(s.yieldPct);
      if (y === null || y <= 0) return setError(t("«{name}»: нормативный выход — больше нуля.", { name: s.name }));
    }
    const filled = lines.filter((l) => l.itemId || l.quantity.trim());
    const seen = new Set<string>();
    for (const l of filled) {
      if (!l.itemId) return setError(t("Выберите позицию склада у каждого компонента."));
      const q = parseNumber(l.quantity);
      if (q === null || q <= 0) return setError(t("«{name}»: укажите норму.", { name: l.name }));
      const loss = num(l.loss) ?? 0;
      if (loss < 0 || loss > 100) return setError(t("«{name}»: потери — от 0 до 100 %.", { name: l.name }));
      const id = `${l.stageKey}|${l.itemId}`;
      if (seen.has(id)) return setError(t("«{name}» уже есть на этом этапе — одна позиция — одна строка этапа.", { name: l.name }));
      seen.add(id);
    }
    for (const d of defects) {
      if (!d.name.trim()) return setError(t("Укажите название вида брака."));
      const n = num(d.norm) ?? 0;
      if (n < 0 || n >= 100) return setError(t("Брак «{name}»: норматив — от 0 до 100 %.", { name: d.name }));
    }
    const body = {
      code: code.trim(),
      name: name.trim(),
      linkoProductId: product?.id ?? null,
      outputName: outputName.trim() || null,
      outputUnit,
      outputQuantity: output,
      comment: comment.trim() || null,
      version: initial?.version ?? null,
      techCardNo: techCardNo.trim() || null,
      technologistId: technologist || null,
      effectiveFrom: effectiveFrom || null,
      batchMinKg: num(params.batchMin),
      batchMaxKg: num(params.batchMax),
      partialBatches: params.partial,
      pieceKg: num(pieceKg),
      packKg: num(params.packKg),
      packsPerBox: num(params.perBox),
      outputItemId: outputItem?.id ?? null,
      stages: stages.map((s) => ({
        key: s.key,
        name: s.name.trim(),
        kind: s.kind,
        area: s.area.trim() || null,
        yieldPercent: parseNumber(s.yieldPct),
        yieldMinPercent: num(s.yieldMin),
        yieldMaxPercent: num(s.yieldMax),
        defectBase: s.defectBase,
        moistureNorm: num(s.moisture),
        moistureUnit: s.moistureUnit.trim() || null,
        moistureTolerance: num(s.moistureTol),
        reworkable: s.reworkable,
        comment: s.comment.trim() || null,
      })),
      lines: filled.map((l) => ({
        itemId: l.itemId,
        quantity: parseNumber(l.quantity),
        lossPercent: num(l.loss) ?? 0,
        stageKey: l.stageKey,
        basis: l.basis,
        group: l.group.trim() || null,
        unitCode: l.unit || null,
        unitKg: num(l.unitKg),
      })),
      defectTypes: defects.map((d) => ({ stageKey: d.stageKey, name: d.name.trim(), normPercent: num(d.norm) ?? 0, reworkable: d.reworkable, itemId: d.itemId })),
    };
    setBusy(true);
    try {
      if (initial) {
        await bff(`production/recipes/${initial.id}`, { method: "PUT", body: JSON.stringify(body) });
        router.push(`/production/recipes/${initial.id}`);
      } else {
        const { id } = await bff<{ id: string }>("production/recipes", { method: "POST", body: JSON.stringify(body) });
        router.push(`/production/recipes/${id}`);
      }
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-5xl space-y-4">
      <section className={card}>
        <h2 className="mb-3 text-base font-semibold text-ink">{t("Карточка продукта")}</h2>
        <div className="grid gap-3 sm:grid-cols-[180px_minmax(0,1fr)]">
          <label className="block">
            <Label>{t("Код")}</Label>
            <input value={code} onChange={(e) => setCode(e.target.value)} disabled={!!initial} maxLength={32} placeholder="CK-01" className={`${field} tabular-nums`} />
          </label>
          <label className="block">
            <Label>{t("Название")}</Label>
            <input value={name} onChange={(e) => setName(e.target.value)} maxLength={200} placeholder={t("Например: печенье глазированное")} className={field} />
          </label>
        </div>
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          <div className="relative">
            <Label optional>{t("Товар Linko (выход)")}</Label>
            {product ? (
              <div className="flex h-10 items-center gap-2 rounded-lg border border-accent/30 bg-accent-soft/50 pl-3 pr-1 text-sm">
                <span className="min-w-0 flex-1 truncate text-ink">{product.name}</span>
                <button type="button" onClick={() => setProduct(null)} aria-label={t("Убрать товар Linko")} className="grid size-8 place-items-center rounded-md text-ink-3 hover:text-ink">
                  <X className="size-4" aria-hidden />
                </button>
              </div>
            ) : (
              <input value={productText} onChange={(e) => setProductText(e.target.value)} placeholder={t("Найдите товар по названию или коду")} className={field} />
            )}
            {products.length > 0 && (
              <ul className="absolute left-0 right-0 top-full z-20 mt-1 max-h-64 overflow-y-auto rounded-xl border border-line-strong bg-surface-solid p-1 shadow-lg">
                {products.map((p) => (
                  <li key={p.id}>
                    <button
                      type="button"
                      onClick={() => {
                        setProduct({ id: p.id, name: p.name });
                        if (!outputName.trim()) setOutputName(p.name);
                        if (!name.trim()) setName(p.name);
                        setProductText("");
                      }}
                      className="block w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-muted"
                    >
                      {p.name}
                      <span className="block text-[11px] text-ink-3">{[p.code, p.unit].filter(Boolean).join(" · ")}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
          <label className="block">
            <Label optional>{t("Название выхода")}</Label>
            <input value={outputName} onChange={(e) => setOutputName(e.target.value)} maxLength={200} placeholder={t("как в рецептуре")} className={field} />
          </label>
        </div>
        <div className="mt-3 grid gap-3 sm:grid-cols-3">
          <label className="block">
            <Label optional>{t("Номер технологической карты")}</Label>
            <input value={techCardNo} onChange={(e) => setTechCardNo(e.target.value)} maxLength={32} placeholder="ТК-01" className={field} />
          </label>
          <label className="block">
            <Label optional>{t("Ответственный технолог")}</Label>
            <select value={technologist} onChange={(e) => setTechnologist(e.target.value)} className={field}>
              <option value="">{t("Не выбран")}</option>
              {form.technologists.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <Label optional>{t("Дата начала действия")}</Label>
            <input type="date" value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)} className={field} />
          </label>
        </div>
        <div className="mt-3 grid grid-cols-2 gap-3 sm:max-w-2xl sm:grid-cols-3">
          <label className="block">
            <Label>{t("Нормы на выход")}</Label>
            <input value={outputQty} onChange={(e) => setOutputQty(e.target.value)} inputMode="decimal" className={`${field} text-right tabular-nums`} />
          </label>
          <label className="block">
            <Label>{t("Единица выхода")}</Label>
            <select value={outputUnit} onChange={(e) => setOutputUnit(e.target.value)} className={field}>
              {units.map((u) => (
                <option key={u.code} value={u.code}>
                  {u.code}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <Label optional>{t("Масса штуки, кг")}</Label>
            <input value={pieceKg} onChange={(e) => setPieceKg(e.target.value)} inputMode="decimal" placeholder="0,025" className={`${field} text-right tabular-nums`} />
          </label>
        </div>
        <p className="mt-2 text-xs text-ink-3">
          {t("Нормы компонентов — на это количество готовой продукции, например на 1 000 кг. Рецептура замеса — на один замес. Дата начала действия — день утверждения, если не указана.")}
        </p>
        <div className="mt-3 sm:max-w-xl">
          <Label optional>{t("Позиция склада выхода")}</Label>
          <CatalogPicker
            name={outputItem?.name ?? ""}
            sku={outputItem?.sku ?? null}
            linked={!!outputItem}
            label={t("Позиция склада выхода")}
            onPick={(o) => setOutputItem({ id: o.id, name: o.name, sku: o.sku, unit: o.unitCode })}
            onUnlink={() => setOutputItem(null)}
          />
          <p className="mt-1 text-xs text-ink-3">
            {t("Для полуфабриката (начинка, глазурь): выход закрытого задания кладовщик приходует партией на склад, и её выдают в другие задания. Готовую продукцию указывайте, только если её учитывают на складе OneBase — иначе ею владеет Linko.")}
          </p>
        </div>
      </section>

      <section className={card} aria-labelledby="rf-chain">
        <div className="mb-3 flex flex-wrap items-center gap-2">
          <h2 id="rf-chain" className="mr-auto flex items-center gap-2 text-base font-semibold text-ink">
            <Workflow className="size-4 text-ink-3" aria-hidden /> {t("Технологическая цепочка")}
          </h2>
          {stages.length === 0 && (
            <button type="button" onClick={() => addStages(typicalChain.map((s) => blankStage(t(s.name), s.kind, s.yieldPercent)))} className={secondary}>
              <ListPlus className="size-4" aria-hidden /> {t("Типовая цепочка")}
            </button>
          )}
          <button type="button" onClick={() => addStages([blankStage(t("Новый этап"))])} disabled={stages.length >= 30} className={secondary}>
            <Plus className="size-4" aria-hidden /> {t("Этап")}
          </button>
        </div>
        {stages.length > 0 ? (
          <ol className="flex flex-wrap items-center gap-1 text-sm" aria-label={t("Порядок этапов")}>
            <li className="rounded-lg bg-muted px-2.5 py-1 text-ink-2">{t("Сырьё")}</li>
            {stages.map((s, i) => (
              <Fragment key={s.key}>
                <ChevronRight className="size-4 text-ink-4" aria-hidden />
                <li>
                  <a href={`#stage-${s.key}`} className="block rounded-lg border border-line px-2.5 py-1 text-ink hover:border-accent/40 hover:bg-muted">
                    <span className="tabular-nums text-ink-3">{i + 1}.</span> {s.name || t("Без названия")}
                  </a>
                </li>
              </Fragment>
            ))}
            <ChevronRight className="size-4 text-ink-4" aria-hidden />
            <li className="rounded-lg bg-ok-soft px-2.5 py-1 font-medium text-ok">{t("Готовая продукция")}</li>
          </ol>
        ) : null}
        {stages.length > 0 ? (
          <p className="mt-2 text-xs text-ink-3">
            {t("Выход этапа — сколько выходит от всего, что поступило на этап вместе с компонентами: выпечка 280 → 245 кг — 87,5 %. Брак — виды брака этапа; норматив этапа — их сумма.")}
          </p>
        ) : (
          <p className="text-sm text-ink-2">
            {t("Этапов нет — состав ниже считается по прежней формуле: норма × количество ÷ выход × (1 + потери). Добавьте этапы, чтобы учитывать выход, брак каждого этапа, замесы и упаковку.")}
          </p>
        )}
      </section>

      {stages.map((s, i) => (
        <StageCard
          key={s.key}
          stage={s}
          no={i + 1}
          total={stages.length}
          next={stages[i + 1]?.name ?? null}
          lines={lines.filter((l) => l.stageKey === s.key)}
          defects={defects.filter((d) => d.stageKey === s.key)}
          params={params}
          onStage={(patch) => setStage(s.key, patch)}
          onParams={(patch) => setParams((p) => ({ ...p, ...patch }))}
          onLine={setLine}
          onAddLine={() => setLines((ls) => [...ls, blankLine(s.key, s.kind)])}
          onRemoveLine={(key) => setLines((ls) => ls.filter((l) => l.key !== key))}
          onDefect={setDefect}
          onAddDefect={() => setDefects((ds) => [...ds, { key: newKey(), stageKey: s.key, name: "", norm: "", itemId: null, reworkable: s.reworkable }])}
          onRemoveDefect={(key) => setDefects((ds) => ds.filter((d) => d.key !== key))}
          onMove={(dir) => move(s.key, dir)}
          onCopy={() => copy(s.key)}
          onRemove={() => remove(s.key)}
        />
      ))}

      {stages.length === 0 && (
        <section className={card}>
          <h2 className="mb-3 text-base font-semibold text-ink">
            {t("Состав")} <span className="font-normal text-ink-3">· {t("позиции склада, норма на выход и потери")}</span>
          </h2>
          {unstaged.length > 0 && (
            <ul className="space-y-2">
              {unstaged.map((l, i) => (
                <li key={l.key} className="grid grid-cols-[minmax(0,1fr)_120px_84px_96px_40px] items-center gap-2">
                  <CatalogPicker
                    name={l.name}
                    sku={l.sku}
                    linked={!!l.itemId}
                    label={t("Позиция {n}", { n: i + 1 })}
                    onPick={(o) => setLine(l.key, { itemId: o.id, sku: o.sku, name: o.name, itemUnit: o.unitCode, unit: o.unitCode, itemUnits: o.units ?? [] })}
                    onUnlink={() => setLine(l.key, { itemId: null, sku: null, name: "", itemUnit: "", unit: "", itemUnits: [] })}
                  />
                  <input value={l.quantity} onChange={(e) => setLine(l.key, { quantity: e.target.value })} inputMode="decimal" placeholder={t("Норма")}
                    aria-label={t("Позиция {n}: норма", { n: i + 1 })} className={`${field} text-right tabular-nums`} />
                  <select value={l.unit} onChange={(e) => setLine(l.key, { unit: e.target.value })} disabled={!l.itemId} aria-label={t("Единица нормы")} className={field}>
                    {(l.itemId ? unitChoices(l.itemUnit, l.itemUnits) : [""]).map((u) => (
                      <option key={u} value={u}>
                        {u || "—"}
                      </option>
                    ))}
                  </select>
                  <input value={l.loss} onChange={(e) => setLine(l.key, { loss: e.target.value })} inputMode="decimal" placeholder="0"
                    aria-label={t("Позиция {n}: потери, %", { n: i + 1 })} className={`${field} text-right tabular-nums`} />
                  <button type="button" onClick={() => setLines((ls) => ls.filter((x) => x.key !== l.key))} aria-label={t("Удалить позицию {n}", { n: i + 1 })}
                    className="grid size-10 place-items-center rounded-lg text-ink-3 hover:text-bad">
                    <Trash2 className="size-4" aria-hidden />
                  </button>
                </li>
              ))}
            </ul>
          )}
          <button type="button" onClick={() => setLines((ls) => [...ls, blankLine(null, null)])} disabled={lines.length >= 100} className={`${secondary} mt-3`}>
            <Plus className="size-4" aria-hidden /> {t("Добавить позицию")}
          </button>
        </section>
      )}

      <section className={card}>
        <label className="block">
          <Label optional>{t("Комментарий")}</Label>
          <textarea
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            maxLength={2000}
            rows={3}
            placeholder={t("Технология, что изменилось в версии")}
            className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
          />
        </label>
      </section>

      <ErrorNote text={error} />
      <div className="sticky bottom-3 z-10 flex justify-end gap-2 rounded-2xl border border-line bg-surface-solid/95 p-2 shadow-lg backdrop-blur max-sm:bottom-20">
        <Link href={initial ? `/production/recipes/${initial.id}` : "/production/recipes"} className={`${secondary} h-10`}>
          {t("Отмена")}
        </Link>
        <button type="button" onClick={() => void save()} disabled={busy} className={`${primary} h-10`}>
          {busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Save className="size-4" aria-hidden />} {t("Сохранить черновик")}
        </button>
      </div>
    </div>
  );
}
