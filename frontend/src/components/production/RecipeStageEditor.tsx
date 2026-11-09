"use client";

import { ArrowDown, ArrowUp, Copy, Plus, Trash2 } from "lucide-react";
import { CatalogPicker } from "@/components/requests/CatalogPicker";
import { useT } from "@/i18n/client";
import { parseNumber, quantity, type ItemUnit } from "@/lib/procurement";
import {
  basesFor,
  batchMass,
  defectBaseLabel,
  lineBasisLabel,
  lineKg,
  stageKindLabel,
  stageKinds,
  unitChoices,
  type DefectBase,
  type LineBasis,
  type StageKind,
} from "@/lib/production";
import { field, secondary } from "./ProdKit";

export type StageState = {
  key: string;
  name: string;
  kind: StageKind;
  area: string;
  yieldPct: string;
  yieldMin: string;
  yieldMax: string;
  defectBase: DefectBase;
  moisture: string;
  moistureUnit: string;
  moistureTol: string;
  reworkable: boolean;
  comment: string;
};

export type LineState = {
  key: string;
  stageKey: string | null;
  itemId: string | null;
  sku: string | null;
  name: string;
  itemUnit: string;
  itemUnits: ItemUnit[];
  unit: string;
  quantity: string;
  loss: string;
  basis: LineBasis;
  group: string;
  unitKg: string;
};

export type DefectState = { key: string; stageKey: string; name: string; norm: string; itemId: string | null; reworkable: boolean };

/** Параметры карты, которые показываются внутри этапа: замес (у этапа замеса) и упаковка (у этапа упаковки). */
export type CardParams = { batchMin: string; batchMax: string; partial: boolean; packKg: string; perBox: string };

const control = "h-9 rounded-lg border border-line bg-surface px-2.5 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const small = `${control} w-full`;
const mini = "mb-1 block text-[11px] font-medium text-ink-3";

/** Строка компонента: позиция склада, база нормы, норма с единицей, потери, группа; для единиц не массы — масса единицы. */
function LineRow({ line, kind, index, onChange, onRemove }: { line: LineState; kind: StageKind | null; index: number; onChange: (patch: Partial<LineState>) => void; onRemove: () => void }) {
  const t = useT();
  const bases = basesFor(kind);
  const kg = lineKg(line.unit, line.itemUnit, line.itemUnits, parseNumber(line.unitKg));
  const needsKg = !!line.itemId && kind !== "Packing" && lineKg(line.unit, line.itemUnit, line.itemUnits, null) === null;
  return (
    <li className="rounded-xl border border-line p-2.5 lg:border-0 lg:p-0">
      <div className="grid grid-cols-2 gap-2 lg:grid-cols-[minmax(0,1fr)_128px_112px_84px_76px_112px_36px] lg:items-center">
        <div className="col-span-2 min-w-0 lg:col-span-1">
          <CatalogPicker
            name={line.name}
            sku={line.sku}
            linked={!!line.itemId}
            label={t("Компонент {n}", { n: index + 1 })}
            onPick={(o) => onChange({ itemId: o.id, sku: o.sku, name: o.name, itemUnit: o.unitCode, unit: o.unitCode, itemUnits: o.units ?? [] })}
            onUnlink={() => onChange({ itemId: null, sku: null, name: "", itemUnit: "", unit: "", itemUnits: [] })}
          />
        </div>
        <select value={line.basis} onChange={(e) => onChange({ basis: e.target.value as LineBasis })} disabled={bases.length === 1} aria-label={t("База нормы")} className={small}>
          {bases.map((b) => (
            <option key={b} value={b}>
              {t(lineBasisLabel[b])}
            </option>
          ))}
        </select>
        <input value={line.quantity} onChange={(e) => onChange({ quantity: e.target.value })} inputMode="decimal" placeholder={t("Норма")} aria-label={t("Норма")}
          className={`${small} text-right tabular-nums`} />
        <select value={line.unit} onChange={(e) => onChange({ unit: e.target.value })} disabled={!line.itemId} aria-label={t("Единица нормы")} className={small}>
          {(line.itemId ? unitChoices(line.itemUnit, line.itemUnits) : [""]).map((u) => (
            <option key={u} value={u}>
              {u || "—"}
            </option>
          ))}
        </select>
        <input value={line.loss} onChange={(e) => onChange({ loss: e.target.value })} inputMode="decimal" placeholder="0" aria-label={t("Потери, %")}
          className={`${small} text-right tabular-nums`} />
        <input value={line.group} onChange={(e) => onChange({ group: e.target.value })} maxLength={64} placeholder={t("Группа")} aria-label={t("Группа")} className={small} />
        <button type="button" onClick={onRemove} aria-label={t("Убрать компонент {n}", { n: index + 1 })}
          className="grid size-9 place-items-center justify-self-end rounded-lg text-ink-3 hover:text-bad">
          <Trash2 className="size-4" aria-hidden />
        </button>
      </div>
      {needsKg && (
        <label className="mt-2 flex flex-wrap items-center gap-2 text-xs text-ink-2 lg:pl-1">
          {t("Масса 1 {unit}, кг", { unit: line.unit })}
          <input value={line.unitKg} onChange={(e) => onChange({ unitKg: e.target.value })} inputMode="decimal" placeholder="0,06" className={`${small} w-28 text-right tabular-nums`} />
          {kg === null && <span className="text-warn">{t("нужна для массы замеса и потока")}</span>}
        </label>
      )}
    </li>
  );
}

/**
 * Карточка этапа в конструкторе: название, тип, участок, нормативный выход с допуском, база брака, влажность, переработка; у замеса —
 * масса замеса (сумма рецептуры), мин. и макс. масса, частичный замес; у упаковки — масса упаковки и упаковок в коробке. Компоненты этапа
 * и виды брака (продукта или материала этапа). Порядок, копия, удаление.
 */
export function StageCard({
  stage,
  no,
  total,
  next,
  lines,
  defects,
  params,
  onStage,
  onParams,
  onLine,
  onAddLine,
  onRemoveLine,
  onDefect,
  onAddDefect,
  onRemoveDefect,
  onMove,
  onCopy,
  onRemove,
}: {
  stage: StageState;
  no: number;
  total: number;
  next: string | null;
  lines: LineState[];
  defects: DefectState[];
  params: CardParams;
  onStage: (patch: Partial<StageState>) => void;
  onParams: (patch: Partial<CardParams>) => void;
  onLine: (key: string, patch: Partial<LineState>) => void;
  onAddLine: () => void;
  onRemoveLine: (key: string) => void;
  onDefect: (key: string, patch: Partial<DefectState>) => void;
  onAddDefect: () => void;
  onRemoveDefect: (key: string) => void;
  onMove: (dir: -1 | 1) => void;
  onCopy: () => void;
  onRemove: () => void;
}) {
  const t = useT();
  const productDefect = defects.filter((d) => !d.itemId).reduce((sum, d) => sum + (parseNumber(d.norm) ?? 0), 0);
  const mass = stage.kind === "Mixing" ? batchMass(lines.filter((l) => l.itemId).map((l) => ({ quantity: parseNumber(l.quantity), kg: lineKg(l.unit, l.itemUnit, l.itemUnits, parseNumber(l.unitKg)) }))) : null;
  const moisture = stage.kind === "Baking" || stage.kind === "Cooling" || stage.moisture.trim() !== "";
  const materials = lines.filter((l) => l.itemId);
  return (
    <section id={`stage-${stage.key}`} className="scroll-mt-24 rounded-2xl border border-line bg-surface" aria-label={t("Этап {n}", { n: no })}>
      <div className="flex flex-wrap items-center gap-2 border-b border-line px-4 py-3">
        <span className="grid size-7 shrink-0 place-items-center rounded-lg bg-accent-soft text-sm font-semibold tabular-nums text-accent-strong">{no}</span>
        <input value={stage.name} onChange={(e) => onStage({ name: e.target.value })} maxLength={100} aria-label={t("Название этапа {n}", { n: no })}
          className="h-9 min-w-0 flex-1 basis-40 rounded-lg border border-transparent bg-transparent px-2 text-base font-semibold text-ink hover:border-line focus:border-accent focus:outline-none" />
        <select value={stage.kind} onChange={(e) => onStage({ kind: e.target.value as StageKind })} aria-label={t("Тип операции")} className={`${control} w-36`}>
          {stageKinds.map((k) => (
            <option key={k} value={k}>
              {t(stageKindLabel[k])}
            </option>
          ))}
        </select>
        <div className="ml-auto flex items-center gap-1">
          <button type="button" onClick={() => onMove(-1)} disabled={no === 1} aria-label={t("Этап выше")} className="grid size-8 place-items-center rounded-lg text-ink-3 hover:bg-muted hover:text-ink disabled:opacity-30">
            <ArrowUp className="size-4" aria-hidden />
          </button>
          <button type="button" onClick={() => onMove(1)} disabled={no === total} aria-label={t("Этап ниже")} className="grid size-8 place-items-center rounded-lg text-ink-3 hover:bg-muted hover:text-ink disabled:opacity-30">
            <ArrowDown className="size-4" aria-hidden />
          </button>
          <button type="button" onClick={onCopy} aria-label={t("Копировать этап")} className="grid size-8 place-items-center rounded-lg text-ink-3 hover:bg-muted hover:text-ink">
            <Copy className="size-4" aria-hidden />
          </button>
          <button type="button" onClick={onRemove} aria-label={t("Удалить этап")} className="grid size-8 place-items-center rounded-lg text-ink-3 hover:bg-bad-soft hover:text-bad">
            <Trash2 className="size-4" aria-hidden />
          </button>
        </div>
      </div>

      <div className="space-y-4 p-4">
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-[minmax(0,1fr)_96px_96px_96px_220px]">
          <label className="col-span-2 block sm:col-span-3 lg:col-span-1">
            <span className={mini}>{t("Участок")}</span>
            <input value={stage.area} onChange={(e) => onStage({ area: e.target.value })} maxLength={100} placeholder={t("Например: печь 1")} className={small} />
          </label>
          <label className="block">
            <span className={mini}>{t("Выход, %")}</span>
            <input value={stage.yieldPct} onChange={(e) => onStage({ yieldPct: e.target.value })} inputMode="decimal" className={`${small} text-right tabular-nums`} />
          </label>
          <label className="block">
            <span className={mini}>{t("мин., %")}</span>
            <input value={stage.yieldMin} onChange={(e) => onStage({ yieldMin: e.target.value })} inputMode="decimal" className={`${small} text-right tabular-nums`} />
          </label>
          <label className="block">
            <span className={mini}>{t("макс., %")}</span>
            <input value={stage.yieldMax} onChange={(e) => onStage({ yieldMax: e.target.value })} inputMode="decimal" className={`${small} text-right tabular-nums`} />
          </label>
          <label className="col-span-2 block sm:col-span-3 lg:col-span-1">
            <span className={mini}>{t("Брак считается")}</span>
            <select value={stage.defectBase} onChange={(e) => onStage({ defectBase: e.target.value as DefectBase })} className={small}>
              {(Object.keys(defectBaseLabel) as DefectBase[]).map((b) => (
                <option key={b} value={b}>
                  {t(defectBaseLabel[b])}
                </option>
              ))}
            </select>
          </label>
        </div>

        {moisture && (
          <div className="grid grid-cols-3 gap-2 sm:max-w-md">
            <label className="block">
              <span className={mini}>{t("Влажность, норма")}</span>
              <input value={stage.moisture} onChange={(e) => onStage({ moisture: e.target.value })} inputMode="decimal" className={`${small} text-right tabular-nums`} />
            </label>
            <label className="block">
              <span className={mini}>{t("Единица")}</span>
              <input value={stage.moistureUnit} onChange={(e) => onStage({ moistureUnit: e.target.value })} maxLength={16} placeholder="%" className={small} />
            </label>
            <label className="block">
              <span className={mini}>{t("Допуск ±")}</span>
              <input value={stage.moistureTol} onChange={(e) => onStage({ moistureTol: e.target.value })} inputMode="decimal" className={`${small} text-right tabular-nums`} />
            </label>
          </div>
        )}

        {stage.kind === "Mixing" && (
          <div className="rounded-xl bg-muted/60 p-3">
            <p className="text-sm text-ink">
              {t("Масса замеса")}: <span className="font-semibold tabular-nums">{mass === null ? t("неизвестна — укажите массу единицы") : `${quantity(mass)} ${t("кг")}`}</span>
              <span className="text-xs text-ink-3"> · {t("сумма рецептуры одного замеса")}</span>
            </p>
            <div className="mt-2 flex flex-wrap items-end gap-2">
              <label className="block w-32">
                <span className={mini}>{t("Минимум, кг")}</span>
                <input value={params.batchMin} onChange={(e) => onParams({ batchMin: e.target.value })} inputMode="decimal" placeholder={mass !== null ? quantity(mass) : ""}
                  className={`${small} text-right tabular-nums`} />
              </label>
              <label className="block w-32">
                <span className={mini}>{t("Максимум, кг")}</span>
                <input value={params.batchMax} onChange={(e) => onParams({ batchMax: e.target.value })} inputMode="decimal" placeholder={mass !== null ? quantity(mass) : ""}
                  className={`${small} text-right tabular-nums`} />
              </label>
              <label className="flex h-9 items-center gap-2 text-sm text-ink">
                <input type="checkbox" checked={params.partial} onChange={(e) => onParams({ partial: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
                {t("Можно частичный замес")}
              </label>
            </div>
          </div>
        )}

        {stage.kind === "Packing" && (
          <div className="flex flex-wrap items-end gap-2 rounded-xl bg-muted/60 p-3">
            <label className="block w-40">
              <span className={mini}>{t("Масса нетто упаковки, кг")}</span>
              <input value={params.packKg} onChange={(e) => onParams({ packKg: e.target.value })} inputMode="decimal" placeholder="0,25" className={`${small} text-right tabular-nums`} />
            </label>
            <label className="block w-40">
              <span className={mini}>{t("Упаковок в коробке")}</span>
              <input value={params.perBox} onChange={(e) => onParams({ perBox: e.target.value })} inputMode="numeric" placeholder="20" className={`${small} text-right tabular-nums`} />
            </label>
            <p className="pb-2 text-xs text-ink-3">{t("По ним считаются этикетки, плёнка и коробки на упаковку и на коробку.")}</p>
          </div>
        )}

        <div>
          <h3 className="mb-2 text-sm font-semibold text-ink">
            {stage.kind === "Mixing" ? t("Рецептура одного замеса") : t("Компоненты этапа")}
            {stage.kind !== "Mixing" && stage.kind !== "Packing" && <span className="font-normal text-ink-3"> · {t("норма на выход рецептуры")}</span>}
          </h3>
          {lines.length === 0 ? (
            <p className="text-sm text-ink-3">{stage.kind === "Mixing" ? t("Добавьте сырьё замеса.") : t("Компонентов нет — этап обрабатывает то, что пришло с предыдущего.")}</p>
          ) : (
            <ul className="space-y-2">
              <li className="hidden grid-cols-[minmax(0,1fr)_128px_112px_84px_76px_112px_36px] gap-2 px-0.5 text-[11px] text-ink-3 lg:grid" aria-hidden>
                <span>{t("Позиция склада")}</span>
                <span>{t("База нормы")}</span>
                <span className="text-right">{t("Норма")}</span>
                <span>{t("Ед.")}</span>
                <span className="text-right">{t("Потери, %")}</span>
                <span>{t("Группа")}</span>
              </li>
              {lines.map((l, i) => (
                <LineRow key={l.key} line={l} kind={stage.kind} index={i} onChange={(patch) => onLine(l.key, patch)} onRemove={() => onRemoveLine(l.key)} />
              ))}
            </ul>
          )}
          <button type="button" onClick={onAddLine} className={`${secondary} mt-2 h-8 px-3 text-xs`}>
            <Plus className="size-3.5" aria-hidden /> {t("Компонент")}
          </button>
        </div>

        <div>
          <h3 className="mb-2 text-sm font-semibold text-ink">
            {t("Виды брака")} <span className="font-normal tabular-nums text-ink-3">· {t("брак продукта на этапе: {p} %", { p: quantity(productDefect) })}</span>
          </h3>
          {defects.length > 0 && (
            <ul className="space-y-2">
              {defects.map((d) => (
                <li key={d.key} className="grid grid-cols-2 gap-2 sm:grid-cols-[minmax(0,1fr)_88px_200px_auto_36px] sm:items-center">
                  <input value={d.name} onChange={(e) => onDefect(d.key, { name: e.target.value })} maxLength={100} placeholder={t("Например: подгорело")} aria-label={t("Вид брака")}
                    className={`${small} col-span-2 sm:col-span-1`} />
                  <input value={d.norm} onChange={(e) => onDefect(d.key, { norm: e.target.value })} inputMode="decimal" placeholder="0" aria-label={t("Норматив, %")}
                    className={`${small} text-right tabular-nums`} />
                  <select value={d.itemId ?? ""} onChange={(e) => onDefect(d.key, { itemId: e.target.value || null })} aria-label={t("Чей брак")} className={small}>
                    <option value="">{t("Брак продукта")}</option>
                    {materials.map((m) => (
                      <option key={m.key} value={m.itemId!}>
                        {t("Брак материала: {name}", { name: m.name })}
                      </option>
                    ))}
                  </select>
                  <label className="flex h-9 items-center gap-2 whitespace-nowrap text-xs text-ink-2">
                    <input type="checkbox" checked={d.reworkable} onChange={(e) => onDefect(d.key, { reworkable: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
                    {t("в переработку")}
                  </label>
                  <button type="button" onClick={() => onRemoveDefect(d.key)} aria-label={t("Убрать вид брака")} className="grid size-9 place-items-center justify-self-end rounded-lg text-ink-3 hover:text-bad">
                    <Trash2 className="size-4" aria-hidden />
                  </button>
                </li>
              ))}
            </ul>
          )}
          <button type="button" onClick={onAddDefect} className={`${secondary} mt-2 h-8 px-3 text-xs`}>
            <Plus className="size-3.5" aria-hidden /> {t("Вид брака")}
          </button>
          <p className="mt-2 text-xs text-ink-3">
            {stage.defectBase === "Input"
              ? t("Брак отсеивается до обработки: компоненты этого этапа на него не тратятся.")
              : t("Брак после обработки: на него потрачены материалы этого и всех прежних этапов, но не следующих.")}
          </p>
        </div>

        <div className="flex flex-wrap items-center justify-between gap-2 border-t border-line pt-3">
          <label className="flex items-center gap-2 text-sm text-ink">
            <input type="checkbox" checked={stage.reworkable} onChange={(e) => onStage({ reworkable: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
            {t("Брак этапа можно перерабатывать")}
          </label>
          <span className="text-xs text-ink-3">{next ? t("Дальше — «{name}»", { name: next }) : t("Дальше — готовая продукция")}</span>
        </div>
      </div>
    </section>
  );
}
