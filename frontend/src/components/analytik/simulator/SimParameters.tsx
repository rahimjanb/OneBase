"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";
import {
  ChartLine,
  ChevronDown,
  Crosshair,
  DatabaseZap,
  Factory,
  Layers,
  MapPin,
  MapPinPlus,
  Minus,
  Package,
  Plus,
  Truck,
  Users,
  Wallet,
  Wheat,
  X,
} from "lucide-react";
import { useT } from "@/i18n/client";
import { num } from "@/lib/analytik";
import {
  groupedParameters,
  inputParameters,
  isSet,
  paramValue,
  plural,
  setParameter,
  shortParamName,
  sliderRange,
  targetedChanges,
  valueOf,
  type BaselineView,
  type ChoiceItem,
  type KindView,
  type ParamGroupKey,
  type ParameterDefinition,
  type ScenarioIssue,
  type ScenarioKind,
  type ScenarioParameter,
  type TargetKind,
} from "@/lib/simulator";

type Icon = React.ComponentType<{ className?: string; "aria-hidden"?: boolean }>;

export const kindIcons: Record<ScenarioKind, Icon> = {
  sales: ChartLine,
  production: Factory,
  materials: Wheat,
  procurement: Truck,
  finance: Wallet,
  workforce: Users,
  expansion: MapPinPlus,
  combined: Layers,
};

/** Подписи видов на плитках (коротко, как в макете); полное название — в подсказке. */
const tileLabel: Partial<Record<ScenarioKind, string>> = { production: "Произв-во", combined: "Всё сразу" };
const chipLabel: Partial<Record<ScenarioKind, string>> = { combined: "Всё сразу" };

const groupIcons: Record<ParamGroupKey, Icon> = { sales: ChartLine, finance: Wallet, production: Factory, materials: Wheat, costs: Users, expansion: MapPinPlus };
const targetIcons: Record<TargetKind, Icon> = { region: MapPin, product: Package, item: Wheat, item_supplier: Truck, division: Users };

/** «Что меняем»: плитки 4 × 2 (компьютер). Вид задаёт, какие группы параметров видны; «Всё сразу» — все. */
export function KindTiles({ kinds, value, onChange, disabled }: { kinds: KindView[]; value: ScenarioKind; onChange: (k: ScenarioKind) => void; disabled?: boolean }) {
  const t = useT();
  const current = kinds.find((k) => k.code === value);
  return (
    <div>
      <div className="grid grid-cols-4 gap-1.5" role="radiogroup" aria-label={t("Что меняем")}>
        {kinds.map((k) => {
          const Icon = kindIcons[k.code];
          const active = k.code === value;
          return (
            <button
              key={k.code}
              type="button"
              role="radio"
              aria-checked={active}
              disabled={disabled}
              title={t(k.title)}
              onClick={() => onChange(k.code)}
              className={`flex h-[62px] flex-col items-center justify-center gap-1 rounded-xl border px-1 transition disabled:opacity-50 ${active ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-muted text-ink-3 hover:border-line-strong hover:text-ink"}`}
            >
              <Icon className="size-[17px]" aria-hidden />
              <span lang="ru" className={`w-full text-center text-[10px] leading-tight [overflow-wrap:anywhere] hyphens-auto ${active ? "font-bold" : "font-medium"}`}>
                {t(tileLabel[k.code] ?? k.title)}
              </span>
            </button>
          );
        })}
      </div>
      {current && <p className="mt-2.5 text-[11px] leading-snug text-ink-4">{t(current.description)}</p>}
    </div>
  );
}

/** Вид сценария на телефоне: чипы с горизонтальной прокруткой. */
export function KindChips({ kinds, value, onChange, disabled }: { kinds: KindView[]; value: ScenarioKind; onChange: (k: ScenarioKind) => void; disabled?: boolean }) {
  const t = useT();
  const order = [...kinds].sort((a, b) => (a.code === "combined" ? -1 : b.code === "combined" ? 1 : 0));
  return (
    <div className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1 [scrollbar-width:none] md:-mx-5 md:px-5" role="radiogroup" aria-label={t("Что меняем")}>
      {order.map((k) => {
        const Icon = kindIcons[k.code];
        const active = k.code === value;
        return (
          <button
            key={k.code}
            type="button"
            role="radio"
            aria-checked={active}
            disabled={disabled}
            onClick={() => onChange(k.code)}
            className={`inline-flex h-9 shrink-0 items-center gap-1.5 rounded-xl border px-3 text-[13px] ${active ? "border-accent bg-accent-soft font-semibold text-accent-strong" : "border-line bg-muted text-ink-2"}`}
          >
            <Icon className="size-4" aria-hidden />
            {t(chipLabel[k.code] ?? k.title)}
          </button>
        );
      })}
    </div>
  );
}

const parse = (raw: string) => Number(raw.replace(/[−–]/g, "-").replace(",", ".").replace(/[\s ]/g, ""));
const unitWords: Record<string, string> = { "%": "процентов", "п.п.": "процентных пунктов", "дн.": "дней", "смен": "смен" };
const fieldUnits = new Set(["%", "п.п.", "дн.", "коэф.", "смен"]);

/** Подсказки базы под параметром («В базе: 1%») и что значит пустое поле. */
function useHints(baseline: BaselineView | null, parameters: ScenarioParameter[]) {
  const t = useT();
  const s = baseline?.summary;
  const terms = valueOf(parameters, "input.supplier_terms");
  return {
    base: {
      "sales.discount": s?.discountShare != null ? `${t("В базе")}: ${num(s.discountShare * 100, 1)}%` : null,
      "finance.customer_terms": s?.paymentLagDays != null ? `${t("Срок оплаты в базе")}: ${num(s.paymentLagDays, 1)} ${t("дн.")}` : null,
      "production.shifts": s?.shiftsPerDay != null ? `${t("В базе")}: ${num(s.shiftsPerDay, 2)} ${t("смен в рабочий день")}` : null,
      "production.defect": s?.defectRate != null ? `${t("Брак в базе")}: ${num(s.defectRate * 100, 2)}%` : null,
      "procurement.supplier_terms": terms != null ? `${t("Сейчас")}: ${num(terms, 0)} ${t("дн.")}` : null,
    } as Record<string, string | null>,
    empty: {
      "sales.elasticity": t("Пусто — объём от цены не зависит"),
      "production.utilization": t("Пусто — до 100% мощности"),
      "production.shifts": t("Пусто — как в базе"),
    } as Record<string, string>,
  };
}

/**
 * Параметр: название, поле числа (на телефоне — степпер «− значение +») и ползунок от нуля в центре; изменённый подсвечен (точка,
 * синее поле, заливка от нуля). Поле принимает весь диапазон сервера, ползунок — удобный (±50 % …). Двойной клик по ползунку — сброс,
 * Shift + стрелки — шаг × 10.
 */
function ParamControl({ def, value, onChange, hint, empty, touch, disabled }: { def: ParameterDefinition; value: number | null; onChange: (v: number | null) => void; hint?: string | null; empty?: string; touch?: boolean; disabled?: boolean }) {
  const t = useT();
  const id = useId();
  const [draft, setDraft] = useState<string | null>(null);
  const range = sliderRange(def);
  const changed = value != null && !(def.mode === "change" && value === 0);
  const shown = value ?? (def.mode === "change" ? 0 : null);
  const text = shown == null ? "" : `${def.mode === "change" && shown > 0 ? "+" : ""}${num(shown, 2)}`;
  const clamp = (v: number) => Math.min(def.max, Math.max(def.min, v));
  const round = (v: number) => {
    const digits = (String(def.step).split(".")[1] ?? "").length;
    return Number(v.toFixed(Math.max(digits, 0)));
  };
  const commit = (raw: string) => {
    setDraft(null);
    if (raw.trim() === "" || raw.trim() === "—") return onChange(null);
    const v = parse(raw);
    if (Number.isFinite(v)) onChange(clamp(v));
  };
  const stepBy = (dir: number, times = 1) => onChange(round(clamp((shown ?? def.default ?? 0) + dir * (def.step || 1) * times)));
  const box = fieldUnits.has(def.unit) ? "w-[86px]" : "w-[136px]";
  const input = (
    <input
      id={id}
      inputMode="decimal"
      value={draft ?? text}
      placeholder="—"
      disabled={disabled}
      onChange={(e) => setDraft(e.target.value)}
      onBlur={(e) => commit(e.target.value)}
      onKeyDown={(e) => {
        if (e.key === "Enter") commit((e.target as HTMLInputElement).value);
        if (e.key === "Escape") setDraft(null);
      }}
      className={`w-full min-w-0 bg-transparent text-right font-semibold tabular-nums outline-none placeholder:text-ink-4 ${touch ? "text-center text-[14px] font-bold" : "text-[13px]"} ${changed ? "text-accent-strong" : "text-ink"}`}
    />
  );
  return (
    <div className={touch ? "py-3" : "py-2.5"}>
      <div className="flex items-center gap-2">
        {changed && <span className="size-1.5 shrink-0 rounded-full bg-accent" aria-hidden />}
        <label htmlFor={id} title={t(def.description)} className={`min-w-0 flex-1 ${touch ? "text-[14px]" : "text-[13px]"} ${changed ? "font-semibold text-ink" : "font-medium text-ink-2"}`}>
          {t(def.title)}
        </label>
        {touch ? (
          <div className={`flex h-[34px] w-[150px] shrink-0 items-center rounded-xl border bg-page ${changed ? "border-accent" : "border-line"}`}>
            <Repeat label={t("Меньше")} onStep={(n) => stepBy(-1, n)} disabled={disabled}>
              <Minus className="size-4" />
            </Repeat>
            <span className="flex min-w-0 flex-1 items-baseline justify-center gap-0.5">
              {input}
              <span className="shrink-0 text-[11px] text-ink-4">{def.unit}</span>
            </span>
            <Repeat label={t("Больше")} onStep={(n) => stepBy(1, n)} disabled={disabled}>
              <Plus className="size-4" />
            </Repeat>
          </div>
        ) : (
          <div className={`flex h-[30px] ${box} shrink-0 items-center gap-1 rounded-lg border bg-page px-2 ${changed ? "border-accent" : "border-line"}`}>
            {input}
            {def.unit && <span className="shrink-0 text-[11px] text-ink-4">{def.unit}</span>}
          </div>
        )}
      </div>
      {range && (
        <Slider def={def} range={range} value={shown} changed={changed} onChange={onChange} touch={touch} title={t(def.title)} disabled={disabled} />
      )}
      {range && touch && (
        <div className="mt-1 flex justify-between text-[10px] text-ink-4" aria-hidden>
          <span>{paramValue(range.min, { unit: def.unit, mode: range.centered ? "change" : "value" }).replace(/ /g, "")}</span>
          <span>{range.centered ? hint?.replace(/^.*?:\s*/, `${t("в базе")} `) ?? "0" : ""}</span>
          <span>{paramValue(range.max, { unit: def.unit, mode: range.centered ? "change" : "value" }).replace(/ /g, "")}</span>
        </div>
      )}
      {(!touch || !range) && (hint || (value == null && empty)) && <p className="mt-1.5 text-[11px] text-ink-4">{hint ?? empty}</p>}
    </div>
  );
}

/** Кнопка степпера: нажатие — шаг, удержание — повтор (через 400 мс, затем каждые 80 мс). */
function Repeat({ label, onStep, disabled, children }: { label: string; onStep: (times: number) => void; disabled?: boolean; children: React.ReactNode }) {
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const step = useRef(onStep);
  useEffect(() => {
    step.current = onStep;
  });
  const stop = () => {
    if (timer.current) clearTimeout(timer.current);
    timer.current = null;
  };
  useEffect(() => stop, []);
  const start = () => {
    step.current(1);
    const loop = () => {
      step.current(1);
      timer.current = setTimeout(loop, 80);
    };
    timer.current = setTimeout(loop, 400);
  };
  return (
    <button
      type="button"
      aria-label={label}
      disabled={disabled}
      onPointerDown={(e) => (e.preventDefault(), start())}
      onPointerUp={stop}
      onPointerLeave={stop}
      onPointerCancel={stop}
      onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && (e.preventDefault(), step.current(1))}
      className="grid size-[34px] shrink-0 place-items-center text-ink-2 hover:text-ink disabled:opacity-40"
    >
      {children}
    </button>
  );
}

const thumb = {
  desk: "[&::-webkit-slider-thumb]:size-4 [&::-webkit-slider-thumb]:border-[3px] [&::-moz-range-thumb]:size-4 [&::-moz-range-thumb]:border-[3px]",
  touch: "[&::-webkit-slider-thumb]:size-6 [&::-webkit-slider-thumb]:border-4 [&::-moz-range-thumb]:size-6 [&::-moz-range-thumb]:border-4",
  on: "[&::-webkit-slider-thumb]:border-accent [&::-moz-range-thumb]:border-accent",
  off: "[&::-webkit-slider-thumb]:border-line-strong [&::-moz-range-thumb]:border-line-strong",
};

function Slider({
  def,
  range,
  value,
  changed,
  onChange,
  touch,
  title,
  disabled,
}: {
  def: ParameterDefinition;
  range: { min: number; max: number; centered: boolean };
  value: number | null;
  changed: boolean;
  onChange: (v: number | null) => void;
  touch?: boolean;
  title: string;
  disabled?: boolean;
}) {
  const t = useT();
  const v = value ?? def.default;
  const clamped = Math.min(range.max, Math.max(range.min, v));
  const frac = (clamped - range.min) / (range.max - range.min);
  const origin = range.centered ? 0.5 : 0;
  const size = touch ? 24 : 16;
  // Центр ручки идёт от size/2 до (ширина − size/2): заливка и метка нуля считаются так же, чтобы совпадать с ручкой.
  const at = (f: number) => `calc(${f * 100}% + ${(0.5 - f) * size}px)`;
  const a = Math.min(frac, origin);
  const b = Math.max(frac, origin);
  const spoken = value == null || value === 0 ? "0" : `${value > 0 ? t("плюс") : t("минус")} ${num(Math.abs(value), 2)} ${t(unitWords[def.unit] ?? def.unit)}`;
  return (
    <div className={`relative mt-2 ${touch ? "h-6" : "h-4"}`}>
      <div className="pointer-events-none absolute inset-x-0 top-1/2 h-1 -translate-y-1/2 rounded-full bg-line-strong" />
      {b > a && <div className="pointer-events-none absolute top-1/2 h-1 -translate-y-1/2 rounded-full bg-accent" style={{ left: at(a), width: `calc(${(b - a) * 100}% - ${(b - a) * size}px)` }} />}
      {range.centered && <div className="pointer-events-none absolute top-1/2 h-3 w-0.5 -translate-x-1/2 -translate-y-1/2 rounded-full bg-ink-3" style={{ left: at(0.5) }} />}
      <input
        type="range"
        min={range.min}
        max={range.max}
        step={def.step || 1}
        value={clamped}
        disabled={disabled}
        aria-label={title}
        aria-valuetext={`${title}: ${spoken}`}
        onChange={(e) => onChange(Number(e.target.value))}
        onDoubleClick={() => onChange(null)}
        onKeyDown={(e) => {
          if (!e.shiftKey || !["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"].includes(e.key)) return;
          e.preventDefault();
          const dir = e.key === "ArrowRight" || e.key === "ArrowUp" ? 1 : -1;
          onChange(Math.min(def.max, Math.max(def.min, (value ?? def.default) + dir * 10 * (def.step || 1))));
        }}
        className={`absolute inset-0 h-full w-full cursor-pointer appearance-none bg-transparent focus-visible:outline-none disabled:cursor-not-allowed [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:bg-white [&::-moz-range-track]:bg-transparent [&::-webkit-slider-runnable-track]:h-full [&::-webkit-slider-runnable-track]:bg-transparent [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-white [&::-webkit-slider-thumb]:shadow-[0_1px_4px_rgb(0_0_0/0.35)] [&:focus-visible::-webkit-slider-thumb]:ring-4 [&:focus-visible::-webkit-slider-thumb]:ring-accent/30 ${touch ? thumb.touch : thumb.desk} ${changed ? thumb.on : thumb.off}`}
      />
    </div>
  );
}

function choicesOf(baseline: BaselineView | null, kind: TargetKind | null): ChoiceItem[] {
  if (!baseline || !kind) return [];
  const c = baseline.choices;
  return kind === "region" ? c.regions : kind === "product" ? c.products : kind === "item" ? c.items : kind === "item_supplier" ? c.suppliers : c.divisions;
}

/** Название цели точечного изменения по снимку (регион, товар, позиция, поставщик, подразделение). */
export function targetName(baseline: BaselineView | null) {
  return (def: ParameterDefinition, id: string) => choicesOf(baseline, def.targetKind).find((c) => c.id === id)?.name ?? id;
}

type GroupsProps = {
  catalog: ParameterDefinition[];
  kind: ScenarioKind;
  parameters: ScenarioParameter[];
  onChange: (p: ScenarioParameter[]) => void;
  baseline: BaselineView | null;
  regionName: string;
  onRegionName: (v: string) => void;
  disabled?: boolean;
  touch?: boolean;
};

/**
 * Параметры по группам-аккордеонам: квадрат-иконка (изменённая группа — синяя), название, «5 параметров · 2 изменено» или, в свёрнутой,
 * что изменено («Отсрочка оплаты поставщикам: +15 дн.»). Сначала раскрыты группы с изменениями, без изменений — первая.
 */
export function ParamGroups({ catalog, kind, parameters, onChange, baseline, regionName, onRegionName, disabled, touch }: GroupsProps) {
  const t = useT();
  const groups = useMemo(() => groupedParameters(catalog, kind), [catalog, kind]);
  const hints = useHints(baseline, parameters);
  const changedOf = (params: ParameterDefinition[]) => params.filter((d) => isSet(parameters, d));
  const [open, setOpen] = useState<Set<string>>(() => {
    const withChanges = groups.filter((g) => changedOf(g.params).length > 0).map((g) => g.key);
    return new Set(withChanges.length > 0 ? withChanges : groups.slice(0, 1).map((g) => g.key));
  });
  const toggle = (key: string) => setOpen((s) => (s.has(key) ? new Set([...s].filter((k) => k !== key)) : new Set([...s, key])));
  return (
    <div>
      {groups.map((g, gi) => {
        const Icon = groupIcons[g.key];
        const changed = changedOf(g.params);
        const isOpen = open.has(g.key);
        const first = changed[0];
        const firstValue = first ? parameters.find((p) => p.code === first.code)?.value : null;
        const meta = isOpen
          ? `${t(plural(g.params.length, "{n} параметр", "{n} параметра", "{n} параметров"), { n: g.params.length })}${changed.length ? ` · ${t("{n} изменено", { n: changed.length })}` : ""}`
          : first
            ? `${t(first.title)}${first.code === "expansion.reference" ? "" : `: ${paramValue(firstValue ?? 0, first)}`}${changed.length > 1 ? ` ${t("и ещё {n}", { n: changed.length - 1 })}` : ""}`
            : t(plural(g.params.length, "{n} параметр", "{n} параметра", "{n} параметров"), { n: g.params.length });
        return (
          <section key={g.key} className={gi > 0 || !touch ? "border-t border-line" : ""}>
            <button type="button" onClick={() => toggle(g.key)} aria-expanded={isOpen} className={`flex w-full items-center gap-2.5 text-left ${touch ? "py-3.5" : "py-3"}`}>
              <span className={`grid shrink-0 place-items-center rounded-lg ${touch ? "size-[30px]" : "size-7"} ${changed.length ? "bg-accent-soft text-accent-strong" : "bg-muted text-ink-3"}`}>
                <Icon className={touch ? "size-4" : "size-3.5"} aria-hidden />
              </span>
              <span className="min-w-0 flex-1">
                <span className={`block font-semibold text-ink ${touch ? "text-[14px]" : "text-[13px]"}`}>{t(g.title)}</span>
                <span className={`block truncate text-[11px] ${changed.length ? "text-accent-strong" : "text-ink-4"}`}>{meta}</span>
              </span>
              <ChevronDown className={`size-4 shrink-0 text-ink-3 transition ${isOpen ? "rotate-180" : ""}`} aria-hidden />
            </button>
            {isOpen && (
              <fieldset disabled={disabled} className="pb-2">
                {g.key === "expansion" && (
                  <label className="block py-2 text-[13px] font-medium text-ink-2">
                    {t("Название")}
                    <input
                      value={regionName}
                      onChange={(e) => onRegionName(e.target.value)}
                      maxLength={200}
                      placeholder={t("Новый регион")}
                      className="mt-1.5 block h-9 w-full rounded-lg border border-line bg-page px-2.5 text-[13px] font-normal text-ink placeholder:text-ink-4"
                    />
                  </label>
                )}
                {g.params.map((def) =>
                  def.code === "expansion.reference" ? (
                    <label key={def.code} className="block py-2 text-[13px] font-medium text-ink-2" title={t(def.description)}>
                      {t(def.title)}
                      <select
                        value={parameters.find((p) => p.code === def.code)?.target ?? ""}
                        onChange={(e) => onChange(e.target.value ? setParameter(parameters.filter((p) => p.code !== def.code), def, 0, e.target.value) : parameters.filter((p) => p.code !== def.code))}
                        className="mt-1.5 block h-9 w-full rounded-lg border border-line bg-page px-2.5 text-[13px] font-normal text-ink"
                      >
                        <option value="">{t("Средние по компании")}</option>
                        {choicesOf(baseline, "region").map((o) => (
                          <option key={o.id} value={o.id}>
                            {o.name}
                          </option>
                        ))}
                      </select>
                    </label>
                  ) : (
                    <ParamControl
                      key={def.code}
                      def={def}
                      value={valueOf(parameters, def.code)}
                      hint={hints.base[def.code]}
                      empty={hints.empty[def.code]}
                      touch={touch}
                      disabled={disabled}
                      onChange={(v) => onChange(setParameter(parameters, def, v))}
                    />
                  ),
                )}
              </fieldset>
            )}
          </section>
        );
      })}
    </div>
  );
}

const pickLabel: Record<TargetKind, string> = {
  region: "Выберите регион…",
  product: "Выберите товар…",
  item: "Выберите позицию сырья…",
  item_supplier: "Выберите поставщика…",
  division: "Выберите подразделение…",
};

/** Точечные изменения: добавленные — чипами («Бекобод · продажи +10%» ×), ниже форма «что / объект / значение / Добавить». */
export function TargetedChanges({ catalog, kind, parameters, onChange, baseline, disabled, touch }: Omit<GroupsProps, "regionName" | "onRegionName">) {
  const t = useT();
  const defs = useMemo(() => targetedChanges(catalog, kind), [catalog, kind]);
  const [code, setCode] = useState(defs[0]?.code ?? "");
  const [target, setTarget] = useState("");
  const [value, setValue] = useState("");
  if (defs.length === 0) return null;
  const def = defs.find((d) => d.code === code) ?? defs[0];
  const options = choicesOf(baseline, def.targetKind);
  const choiceOnly = def.min === 0 && def.max === 0;
  const names = targetName(baseline);
  const existing = parameters.filter((p) => p.target != null && defs.some((d) => d.code === p.code));
  const add = () => {
    if (!target) return;
    const v = choiceOnly ? 0 : parse(value);
    if (!choiceOnly && (!Number.isFinite(v) || value.trim() === "")) return;
    onChange(setParameter(parameters, def, v, target));
    setTarget("");
    setValue("");
  };
  const field = `rounded-lg border border-line bg-page px-2.5 text-[13px] text-ink ${touch ? "h-10" : "h-9"}`;
  return (
    <fieldset disabled={disabled} className="border-t border-line pb-1 pt-3.5">
      <h3 className="flex items-center gap-1.5 text-[13px] font-semibold text-ink">
        <Crosshair className="size-3.5 text-ink-3" aria-hidden />
        {t("Точечные изменения")}
      </h3>
      <p className="mt-0.5 text-[11px] text-ink-4">{t("Изменить показатель для одного региона, товара или сырья")}</p>
      {existing.length > 0 && (
        <ul className="mt-2.5 flex flex-wrap gap-1.5">
          {existing.map((p) => {
            const d = defs.find((x) => x.code === p.code)!;
            const Icon = targetIcons[d.targetKind!];
            return (
              <li key={`${p.code}:${p.target}`} className="inline-flex max-w-full items-center gap-1.5 rounded-lg border border-accent/30 bg-accent-soft py-1 pl-2 pr-1 text-[12px] text-ink">
                <Icon className="size-3.5 shrink-0 text-accent-strong" aria-hidden />
                <span className="truncate">
                  {names(d, p.target!)} · {t(shortParamName[d.code] ?? d.title)}
                  {d.min === 0 && d.max === 0 ? "" : ` ${paramValue(p.value, d)}`}
                </span>
                <button type="button" onClick={() => onChange(parameters.filter((x) => x !== p))} aria-label={t("Убрать")} className="grid size-5 shrink-0 place-items-center rounded text-ink-3 hover:text-bad">
                  <X className="size-3.5" />
                </button>
              </li>
            );
          })}
        </ul>
      )}
      <div className="mt-2.5 grid gap-2">
        <select value={def.code} onChange={(e) => (setCode(e.target.value), setTarget(""))} aria-label={t("Что изменить")} className={field}>
          {defs.map((d) => (
            <option key={d.code} value={d.code}>
              {t(d.title)}
            </option>
          ))}
        </select>
        <select value={target} onChange={(e) => setTarget(e.target.value)} aria-label={t("Объект")} disabled={options.length === 0} className={field}>
          <option value="">{options.length === 0 ? t("Нет вариантов в исходных данных") : t(pickLabel[def.targetKind!])}</option>
          {options.map((o) => (
            <option key={o.id} value={o.id}>
              {o.name}
              {o.hint ? ` · ${o.hint}` : ""}
            </option>
          ))}
        </select>
        <div className="flex gap-2">
          {!choiceOnly && (
            <div className={`flex min-w-0 flex-1 items-center gap-1 ${field}`}>
              <input value={value} onChange={(e) => setValue(e.target.value)} onKeyDown={(e) => e.key === "Enter" && add()} inputMode="decimal" placeholder={def.mode === "change" ? "+10" : "0"} aria-label={t("Значение")} className="w-full min-w-0 bg-transparent outline-none placeholder:text-ink-4" />
              <span className="shrink-0 text-[11px] text-ink-4">{def.unit}</span>
            </div>
          )}
          <button
            type="button"
            onClick={add}
            disabled={!target || (!choiceOnly && value.trim() === "")}
            className={`inline-flex shrink-0 items-center gap-1 rounded-lg border border-line-strong bg-muted px-3 text-[13px] font-medium text-ink hover:border-accent disabled:opacity-40 ${touch ? "h-10" : "h-9"} ${choiceOnly ? "flex-1 justify-center" : ""}`}
          >
            <Plus className="size-4" aria-hidden />
            {t("Добавить")}
          </button>
        </div>
      </div>
    </fieldset>
  );
}

const primaryInputs = ["input.cost_per_kg", "input.salary", "input.opex", "input.cash"];
const inputLabel: Record<string, string> = { "input.cost_per_kg": "Себестоимость кг" };

/**
 * «Данные, которых нет в OneBase»: себестоимость кг, средняя зарплата, расходы компании, деньги на начало; остальные — по «Ещё».
 * Значения одинаковы для базы и сценария и сразу участвуют в расчёте; в таблице результата строки с ними помечены «введено».
 */
export function MissingInputs({
  catalog,
  parameters,
  onChange,
  baseline,
  disabled,
  flat,
  highlight,
}: {
  catalog: ParameterDefinition[];
  parameters: ScenarioParameter[];
  onChange: (p: ScenarioParameter[]) => void;
  baseline: BaselineView | null;
  disabled?: boolean;
  flat?: boolean;
  highlight?: boolean;
}) {
  const t = useT();
  const inputs = useMemo(() => inputParameters(catalog), [catalog]);
  const s = baseline?.summary;
  const capacityNeeded = s != null && s.capacityKgDay == null && (s.recipeShare ?? 0) > 0;
  const entered = (d: ParameterDefinition) => parameters.some((p) => p.code === d.code);
  const primary = inputs.filter((d) => primaryInputs.includes(d.code) || entered(d) || (capacityNeeded && d.code === "input.capacity"));
  const rest = inputs.filter((d) => !primary.includes(d));
  const [more, setMore] = useState(false);
  const known: Record<string, string> = {
    "input.capacity": s?.capacityKgDay != null ? `${t("По сменам OneBase")}: ${num(s.capacityKgDay, 0)} ${t("кг/сут")}` : t("Смен нет — без ввода мощность не проверяется"),
    "input.salary": t("Зарплат вне продаж в OneBase нет"),
    "input.opex": t("Расходов компании в OneBase нет"),
    "input.cash": t("Остатка денег в OneBase нет"),
  };
  const row = (def: ParameterDefinition) =>
    def.targetKind === "division" ? (
      <label key={def.code} className="flex items-center justify-between gap-3 py-1.5 text-[12px] text-ink-2" title={t(def.description)}>
        <span className="min-w-0">{t(def.title)}</span>
        <select
          value={parameters.find((p) => p.code === def.code)?.target ?? ""}
          onChange={(e) => onChange(e.target.value ? [...parameters.filter((p) => p.code !== def.code), { code: def.code, value: 0, target: e.target.value }] : parameters.filter((p) => p.code !== def.code))}
          className="h-[30px] w-[150px] shrink-0 rounded-lg border border-line bg-page px-2 text-[12px] text-ink"
        >
          <option value="">{t("Не выбрано")}</option>
          {(baseline?.choices.divisions ?? []).map((d) => (
            <option key={d.id} value={d.id}>
              {d.name}
            </option>
          ))}
        </select>
      </label>
    ) : (
      <InputRow key={def.code} def={def} label={t(inputLabel[def.code] ?? def.title)} hint={known[def.code] ?? t(def.description)} value={valueOf(parameters, def.code)} onChange={(v) => onChange(setParameter(parameters, def, v))} />
    );
  const body = (
    <fieldset disabled={disabled}>
      {primary.map(row)}
      {rest.length > 0 && (
        <>
          {more && rest.map(row)}
          <button type="button" onClick={() => setMore(!more)} aria-expanded={more} className="mt-1 inline-flex items-center gap-1 text-[12px] font-medium text-accent-strong hover:underline">
            {more ? t("Скрыть") : t("Ещё {n}", { n: rest.length })}
            <ChevronDown className={`size-3.5 transition ${more ? "rotate-180" : ""}`} aria-hidden />
          </button>
        </>
      )}
    </fieldset>
  );
  const head = (
    <div className="mb-2 flex items-center gap-2.5">
      <span className="grid size-[30px] shrink-0 place-items-center rounded-lg bg-warn-soft text-warn">
        <DatabaseZap className="size-4" aria-hidden />
      </span>
      <span className="min-w-0">
        <span className="block text-[13px] font-bold text-ink">{t("Данные, которых нет в OneBase")}</span>
        <span className="block text-[11px] text-ink-4">{t("введите — и расчёт станет полным")}</span>
      </span>
    </div>
  );
  if (flat) {
    return (
      <section id="sim-inputs" className="border-t border-line py-3.5">
        {head}
        {body}
      </section>
    );
  }
  return (
    <section id="sim-inputs" className={`scroll-mt-4 rounded-[18px] border bg-surface p-[18px] transition-shadow ${highlight ? "border-warn shadow-[0_0_0_4px_rgb(246_169_59/0.25)]" : "border-warn/25"}`}>
      {head}
      {body}
    </section>
  );
}

/** Строка исходных данных: подпись слева, поле 136 × 30 с единицей справа; пусто — значение не используется. */
function InputRow({ def, label, hint, value, onChange }: { def: ParameterDefinition; label: string; hint: string; value: number | null; onChange: (v: number | null) => void }) {
  const id = useId();
  const [draft, setDraft] = useState<string | null>(null);
  const commit = (raw: string) => {
    setDraft(null);
    if (raw.trim() === "") return onChange(null);
    const v = parse(raw);
    if (Number.isFinite(v)) onChange(Math.min(def.max, Math.max(def.min, v)));
  };
  return (
    <div className="flex items-center justify-between gap-3 py-1.5" title={hint}>
      <label htmlFor={id} className="min-w-0 text-[12px] text-ink-2">
        {label}
      </label>
      <div className={`flex h-[30px] w-[136px] shrink-0 items-center gap-1 rounded-lg border bg-page px-2 ${value != null ? "border-violet/60" : "border-line"}`}>
        <input
          id={id}
          inputMode="decimal"
          value={draft ?? (value == null ? "" : num(value, 2))}
          placeholder="—"
          onChange={(e) => setDraft(e.target.value)}
          onFocus={() => value != null && setDraft(String(value))}
          onBlur={(e) => commit(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && commit((e.target as HTMLInputElement).value)}
          className="w-full min-w-0 bg-transparent text-right text-[12px] font-semibold tabular-nums text-ink outline-none placeholder:text-ink-4"
        />
        <span className="shrink-0 text-[10px] text-ink-4">{def.unit}</span>
      </div>
    </div>
  );
}

/** Замечания к параметрам: ошибки (расчёт не выполнен) и предупреждения (параметр не учтён). */
export function IssuesList({ issues }: { issues: ScenarioIssue[] }) {
  const t = useT();
  if (issues.length === 0) return null;
  return (
    <ul className="space-y-1.5" aria-live="polite">
      {issues.map((i, n) => (
        <li key={`${i.code}-${n}`} className={`rounded-lg px-3 py-2 text-[12px] ${i.severity === "error" ? "bg-bad-soft text-bad" : "bg-warn-soft text-warn"}`}>
          {t(i.text)}
        </li>
      ))}
    </ul>
  );
}

