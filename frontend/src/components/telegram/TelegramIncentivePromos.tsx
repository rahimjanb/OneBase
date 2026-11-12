"use client";

import { Fragment, useEffect, useState } from "react";
import { ChevronDown, ChevronRight, Loader2, Plus, RefreshCw, Trash2, X } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Note } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import {
  incentiveGifts,
  incentiveImages,
  incentivesApi,
  type IncentiveBestPromo,
  type IncentiveBudget,
  type IncentiveGiftTier,
  type IncentiveOverview,
  type IncentivePairRule,
  type IncentivePayoutRow,
  type IncentivePayouts,
  type IncentivePayoutWeek,
  type IncentiveProgram,
  type IncentivePromos,
  type IncentiveSleepingRefresh,
  type IncentiveSleepingSummary,
  type IncentiveTerritoryPromo,
  type IncentiveTwizosPromo,
  type IncentiveYamellyPromo,
} from "@/lib/telegramReports";
import { Switch } from "./TelegramReports";
import { when } from "./TelegramReportsPreview";

const day = (iso: string | null) => (iso ? new Date(`${iso}T00:00:00`).toLocaleDateString("ru-RU") : "—");
const money = (v: number) => Math.round(v).toLocaleString("ru-RU");
const cell = `${field} min-w-0`;
const num = (v: string) => (v.trim() === "" ? 0 : Number(v.replace(",", ".")) || 0);
const today = () => new Date().toLocaleDateString("sv-SE", { timeZone: "Asia/Tashkent" });

function Labeled({ label, className = "", children }: { label: string; className?: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <label className={`block ${className}`}>
      <span className="mb-1 block text-xs font-medium text-ink-2">{t(label)}</span>
      {children}
    </label>
  );
}

/** Раздел акций ноября: заголовок с переключателем «есть / нет», подсказка, поля. */
function Section({
  title,
  hint,
  on,
  onToggle,
  children,
}: {
  title: string;
  hint: string;
  on: boolean;
  onToggle: (v: boolean) => void;
  children: React.ReactNode;
}) {
  const t = useT();
  return (
    <div className="rounded-xl border border-line">
      <div className="flex items-start gap-3 px-4 py-3">
        <Switch checked={on} onChange={onToggle} label={t(title)} />
        <div className="min-w-0">
          <div className="text-sm font-medium text-ink">{t(title)}</div>
          <p className="text-xs text-ink-3">{t(hint)}</p>
        </div>
      </div>
      {on && <div className="space-y-4 border-t border-line p-4">{children}</div>}
    </div>
  );
}

const none: IncentivePromos = { twizos: null, yamelly: null, best: null, territory: null, budget: null };

/** Значения по умолчанию раздела — по ТЗ акций ноября, период — период акции. */
const defaults = (p: IncentiveProgram) => {
  const codes = p.storeSets.map((x) => x.code).filter((x): x is string => !!x);
  const has = (...c: string[]) => c.every((x) => codes.includes(x));
  const twizos: IncentiveTwizosPromo = {
    gifts: [
      { minSum: 288600, gift: "mug", name: "кружка", nameUz: "krujka", productIds: null, stock: 0 },
      { minSum: 392200, gift: "thermos", name: "термос", nameUz: "termos", productIds: null, stock: 0 },
      { minSum: 584600, gift: "kettle", name: "чайник", nameUz: "choynak", productIds: null, stock: 0 },
    ],
    pairs: has("T", "K140", "C")
      ? [
          { maxCheck: 600000, sets: ["T", "K140"] },
          { maxCheck: 1200000, sets: ["C", "K140"] },
          { maxCheck: null, sets: ["C", "T"] },
        ]
      : [],
    startSet: codes.includes("K140") ? "K140" : (codes[0] ?? ""),
    smallStartSet: codes.includes("K35") ? "K35" : (codes[0] ?? ""),
    smallCheck: 250000,
    minCheck: 60000,
    pairNewMinCheck: 300000,
    reserve: 1.3,
    conversion: 0.4,
    extraMax: 5,
    checkDays: 90,
    topDays: 90,
    targetPrize: p.tiers.at(-1)?.image ?? null,
    extraPairs: null,
  };
  const yamelly: IncentiveYamellyPromo = {
    start: p.start,
    end: p.end,
    productTypeIds: [],
    packings: [{ unitKg: 0.42, perBox: 8, name: "420 г" }],
    product: "Yamelly",
    discountPercent: 7,
    cashbackPercent: 10,
    minBoxes: 2,
    wakeShare: 0.3,
    minCashback: 26338,
    sleepingFrom: null,
    sleepingSince: null,
  };
  const best: IncentiveBestPromo = { start: p.start, end: p.end, retailMinKg: 6, wholesaleMinKg: 30, prize: 500000 };
  const territory: IncentiveTerritoryPromo = {
    start: p.start,
    end: p.end,
    territories: [],
    productTypeIds: [],
    threshold: 0.85,
    agentMinKg: 50,
    agentPrize: 1000000,
    supervisorPrize: 2000000,
  };
  const budget: IncentiveBudget = { cashback: 0, best: 0, territory: 0 };
  return { twizos, yamelly, best, territory, budget };
};

/** Типы товаров Linko: выбранные — плашками, добавить — из списка (подтипы входят сами). */
function TypePicker({ ids, overview, onChange }: { ids: number[]; overview: IncentiveOverview; onChange: (ids: number[]) => void }) {
  const t = useT();
  const names = new Map(overview.productTypes.map((x) => [x.id, x.name]));
  const roots = overview.productTypes.filter((x) => x.parentId === null);
  const options = roots.flatMap((r) => [r, ...overview.productTypes.filter((x) => x.parentId === r.id)]);
  return (
    <div className="flex flex-wrap items-center gap-2">
      {ids.map((id) => (
        <span key={id} className="inline-flex items-center gap-1 rounded-lg bg-muted px-2 py-1 text-sm text-ink">
          {names.get(id) ?? `#${id}`}
          <button aria-label={t("Убрать")} onClick={() => onChange(ids.filter((x) => x !== id))} className="text-ink-3 hover:text-ink">
            <X className="size-3.5" />
          </button>
        </span>
      ))}
      <select className={`${field} max-w-64`} value="" onChange={(e) => e.target.value && onChange([...ids, Number(e.target.value)])}>
        <option value="">{t("Добавить тип…")}</option>
        {options
          .filter((x) => !ids.includes(x.id))
          .map((x) => (
            <option key={x.id} value={x.id}>
              {x.parentId === null ? x.name : `— ${x.name}`}
            </option>
          ))}
      </select>
    </div>
  );
}

/** Пары наборов по среднему чеку: «чек до» (пусто — без верхней границы) и 1–2 набора по коду. */
function PairsEditor({ rules, codes, onChange }: { rules: IncentivePairRule[]; codes: string[]; onChange: (rules: IncentivePairRule[]) => void }) {
  const t = useT();
  const set = (i: number, patch: Partial<IncentivePairRule>) => onChange(rules.map((r, j) => (j === i ? { ...r, ...patch } : r)));
  return (
    <div className="space-y-2">
      {rules.map((rule, i) => (
        <div key={i} className="grid items-end gap-2 sm:grid-cols-[minmax(0,1fr)_100px_100px_auto]">
          <Labeled label="Средний чек до, сум">
            <input
              type="number"
              min={0}
              step={50000}
              className={cell}
              value={rule.maxCheck ?? ""}
              placeholder={t("без границы")}
              onChange={(e) => set(i, { maxCheck: e.target.value ? num(e.target.value) : null })}
            />
          </Labeled>
          {[0, 1].map((k) => (
            <Labeled key={k} label={k === 0 ? "Набор 1" : "Набор 2"}>
              <select
                className={cell}
                value={rule.sets[k] ?? ""}
                onChange={(e) => {
                  const next = [...rule.sets];
                  next[k] = e.target.value;
                  set(i, { sets: next.filter(Boolean) });
                }}
              >
                <option value="">—</option>
                {codes.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            </Labeled>
          ))}
          <button className={button} aria-label={t("Убрать")} onClick={() => onChange(rules.filter((_, j) => j !== i))}>
            <Trash2 className="size-4" />
          </button>
        </div>
      ))}
      {rules.length < 5 && (
        <button className={button} onClick={() => onChange([...rules, { maxCheck: null, sets: codes.slice(0, 1) }])}>
          <Plus className="size-4" />
          {t("Пара")}
        </button>
      )}
    </div>
  );
}

/**
 * «Акции ноября» (ТЗ «Карточки ТК и расчёт целей по акциям ноября», раздел 8 — параметры без программиста): TW-02/TW-03 Twizos (подарки по сумме
 * накладной, пары и старт, запас на отказы, приз для расчёта нормы), YM-01/YM-02 Yamelly спящим (период, товары, фасовки, скидка, кэшбек, порог,
 * цель «разбудить», правило списка спящих), TW-01, YM-03 по территориям, бюджеты. Раздел выключен — акции нет. Сохраняется отдельно от формы
 * акции, проверки — на сервере.
 */
export function PromosPanel({ program, overview, onSaved }: { program: IncentiveProgram; overview: IncentiveOverview; onSaved: (p: IncentiveProgram) => void }) {
  const t = useT();
  const initial = { ...none, ...(program.promos ?? {}) };
  const [form, setForm] = useState<IncentivePromos>(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const dirty = JSON.stringify(form) !== JSON.stringify(initial);
  const base = defaults(program);
  const codes = program.storeSets.map((x) => x.code).filter((x): x is string => !!x);
  const edit = (patch: Partial<IncentivePromos>) => (setForm((f) => ({ ...f, ...patch })), setSaved(false));
  const tw = form.twizos;
  const ym = form.yamelly;
  const best = form.best;
  const terr = form.territory;
  const budget = form.budget;
  const setTw = (patch: Partial<IncentiveTwizosPromo>) => tw && edit({ twizos: { ...tw, ...patch } });
  const setYm = (patch: Partial<IncentiveYamellyPromo>) => ym && edit({ yamelly: { ...ym, ...patch } });
  const setBest = (patch: Partial<IncentiveBestPromo>) => best && edit({ best: { ...best, ...patch } });
  const setTerr = (patch: Partial<IncentiveTerritoryPromo>) => terr && edit({ territory: { ...terr, ...patch } });
  const setBudget = (patch: Partial<IncentiveBudget>) => budget && edit({ budget: { ...budget, ...patch } });
  const setGift = (i: number, patch: Partial<IncentiveGiftTier>) => tw && setTw({ gifts: (tw.gifts ?? []).map((g, j) => (j === i ? { ...g, ...patch } : g)) });

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const result = await incentivesApi.savePromos(program.code, form);
      setForm({ ...none, ...(result.promos ?? {}) });
      onSaved(result);
      setSaved(true);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="rounded-xl border border-line">
      <div className="border-b border-line px-4 py-3">
        <h3 className="font-semibold text-ink">{t("Акции ноября")}</h3>
        <p className="text-xs text-ink-3">
          {t(
            "Параметры YM-01…03 и TW-01…03 этой акции: меняются без программиста, карточки и расчёты берут их сразу. Наборы TW-02 и пороги опта — в форме акции выше.",
          )}
        </p>
      </div>
      <div className="space-y-3 p-4">
        {codes.length === 0 && <Note>{t("У акции нет наборов с кодами (K35, K140, T, C) — пары и старт TW-02 выбирать не из чего.")}</Note>}

        <Section
          title="TW-02 · TW-03 — Twizos"
          hint="Подарок клиенту по сумме Twizos в накладной; пары тем, кто брал Twizos, старт — новым; норма в день — до приза для расчёта с запасом на отказы."
          on={!!tw}
          onToggle={(v) => edit({ twizos: v ? base.twizos : null })}
        >
          {tw && (
            <>
              <div>
                <div className="mb-2 text-sm font-medium text-ink">{t("Подарки клиенту")}</div>
                <div className="space-y-2">
                  {(tw.gifts ?? []).map((g, i) => (
                    <div key={i} className="grid items-end gap-2 sm:grid-cols-[130px_1fr_1fr_150px_160px_110px_auto]">
                      <Labeled label="Подарок">
                        <select className={cell} value={g.gift} onChange={(e) => setGift(i, { gift: e.target.value })}>
                          {Object.entries(incentiveGifts).map(([value, label]) => (
                            <option key={value} value={value}>
                              {t(label)}
                            </option>
                          ))}
                        </select>
                      </Labeled>
                      <Labeled label="Название">
                        <input className={cell} value={g.name} onChange={(e) => setGift(i, { name: e.target.value })} />
                      </Labeled>
                      <Labeled label="Название (узб.)">
                        <input className={cell} value={g.nameUz ?? ""} onChange={(e) => setGift(i, { nameUz: e.target.value || null })} />
                      </Labeled>
                      <Labeled label="Twizos в накладной от, сум">
                        <input type="number" min={0} step={100} className={cell} value={g.minSum} onChange={(e) => setGift(i, { minSum: num(e.target.value) })} />
                      </Labeled>
                      <Labeled label="Товары Linko (ID)">
                        <input
                          className={cell}
                          value={(g.productIds ?? []).join(", ")}
                          placeholder="297"
                          onChange={(e) =>
                            setGift(i, {
                              productIds: e.target.value
                                .split(/[,\s]+/)
                                .map(Number)
                                .filter((x) => x > 0),
                            })
                          }
                        />
                      </Labeled>
                      <Labeled label="Выделено, шт">
                        <input type="number" min={0} className={cell} value={g.stock} onChange={(e) => setGift(i, { stock: num(e.target.value) })} />
                      </Labeled>
                      <button className={button} aria-label={t("Убрать")} onClick={() => setTw({ gifts: (tw.gifts ?? []).filter((_, j) => j !== i) })}>
                        <Trash2 className="size-4" />
                      </button>
                    </div>
                  ))}
                  {(tw.gifts ?? []).length < 3 && (
                    <button
                      className={button}
                      onClick={() => setTw({ gifts: [...(tw.gifts ?? []), { minSum: 0, gift: "mug", name: "", nameUz: null, productIds: null, stock: 0 }] })}
                    >
                      <Plus className="size-4" />
                      {t("Подарок")}
                    </button>
                  )}
                </div>
              </div>

              <div className="grid gap-4 lg:grid-cols-2">
                <div>
                  <div className="mb-2 text-sm font-medium text-ink">{t("Пара подарков — тем, кто брал Twizos")}</div>
                  <PairsEditor rules={tw.pairs ?? []} codes={codes} onChange={(pairs) => setTw({ pairs })} />
                </div>
                <div>
                  <div className="mb-2 text-sm font-medium text-ink">{t("Пара в доборе вне маршрута")}</div>
                  <PairsEditor rules={tw.extraPairs ?? tw.pairs ?? []} codes={codes} onChange={(extraPairs) => setTw({ extraPairs })} />
                </div>
              </div>

              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                <Labeled label="Старт новому клиенту — набор">
                  <select className={cell} value={tw.startSet} onChange={(e) => setTw({ startSet: e.target.value })}>
                    {codes.map((c) => (
                      <option key={c} value={c}>
                        {c}
                      </option>
                    ))}
                  </select>
                </Labeled>
                <Labeled label="При чеке до, сум — набор поменьше">
                  <div className="flex gap-2">
                    <input type="number" min={0} step={10000} className={cell} value={tw.smallCheck} onChange={(e) => setTw({ smallCheck: num(e.target.value) })} />
                    <select className={`${cell} w-24`} value={tw.smallStartSet} onChange={(e) => setTw({ smallStartSet: e.target.value })}>
                      {codes.map((c) => (
                        <option key={c} value={c}>
                          {c}
                        </option>
                      ))}
                    </select>
                  </div>
                </Labeled>
                <Labeled label="Без цели при чеке до, сум">
                  <input type="number" min={0} step={10000} className={cell} value={tw.minCheck} onChange={(e) => setTw({ minCheck: num(e.target.value) })} />
                </Labeled>
                <Labeled label="«+ план»: новым с чеком от, сум">
                  <input type="number" min={0} step={10000} className={cell} value={tw.pairNewMinCheck} onChange={(e) => setTw({ pairNewMinCheck: num(e.target.value) })} />
                </Labeled>
                <Labeled label="Запас на отказы, %">
                  <input
                    type="number"
                    min={0}
                    max={200}
                    className={cell}
                    value={Math.round((tw.reserve - 1) * 100)}
                    onChange={(e) => setTw({ reserve: 1 + num(e.target.value) / 100 })}
                  />
                </Labeled>
                <Labeled label="Конверсия предложений, %">
                  <input type="number" min={1} max={100} className={cell} value={Math.round(tw.conversion * 100)} onChange={(e) => setTw({ conversion: num(e.target.value) / 100 })} />
                </Labeled>
                <Labeled label="Добор вне маршрута, точек">
                  <input type="number" min={0} max={20} className={cell} value={tw.extraMax} onChange={(e) => setTw({ extraMax: num(e.target.value) })} />
                </Labeled>
                <Labeled label="Приз для расчёта нормы">
                  <select className={cell} value={tw.targetPrize ?? ""} onChange={(e) => setTw({ targetPrize: e.target.value || null })}>
                    <option value="">{t("Ближайший невзятый")}</option>
                    {program.tiers.map((x) => (
                      <option key={x.image} value={x.image}>
                        {x.prize || t(incentiveImages[x.image] ?? x.image)}
                      </option>
                    ))}
                  </select>
                </Labeled>
                <Labeled label="Средний чек — за дней">
                  <input type="number" min={7} max={365} className={cell} value={tw.checkDays} onChange={(e) => setTw({ checkDays: num(e.target.value) })} />
                </Labeled>
                <Labeled label="ТОП SKU региона — за дней">
                  <input type="number" min={7} max={365} className={cell} value={tw.topDays} onChange={(e) => setTw({ topDays: num(e.target.value) })} />
                </Labeled>
              </div>
            </>
          )}
        </Section>

        <Section
          title="YM-01 · YM-02 — Yamelly спящим"
          hint="Спящей точке — скидка при целых коробках одной фасовки (скидку даёт Linko), ТП — кэшбек от суммы после скидки. Список спящих — из Linko."
          on={!!ym}
          onToggle={(v) => edit({ yamelly: v ? base.yamelly : null })}
        >
          {ym && (
            <>
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                <Labeled label="Начало">
                  <input type="date" className={cell} value={ym.start} onChange={(e) => setYm({ start: e.target.value })} />
                </Labeled>
                <Labeled label="Конец">
                  <input type="date" className={cell} value={ym.end} onChange={(e) => setYm({ end: e.target.value })} />
                </Labeled>
                <Labeled label="Товар на карточках">
                  <input className={cell} value={ym.product} onChange={(e) => setYm({ product: e.target.value })} />
                </Labeled>
                <Labeled label="Коробок одной фасовки, от">
                  <input type="number" min={1} max={50} className={cell} value={ym.minBoxes} onChange={(e) => setYm({ minBoxes: num(e.target.value) })} />
                </Labeled>
                <Labeled label="Скидка спящей точке, %">
                  <input type="number" min={0} max={100} step={0.5} className={cell} value={ym.discountPercent} onChange={(e) => setYm({ discountPercent: num(e.target.value) })} />
                </Labeled>
                <Labeled label="Кэшбек ТП, %">
                  <input type="number" min={0} max={100} step={0.5} className={cell} value={ym.cashbackPercent} onChange={(e) => setYm({ cashbackPercent: num(e.target.value) })} />
                </Labeled>
                <Labeled label="Цель «разбудить», % списка">
                  <input type="number" min={0} max={100} className={cell} value={Math.round(ym.wakeShare * 100)} onChange={(e) => setYm({ wakeShare: num(e.target.value) / 100 })} />
                </Labeled>
                <Labeled label="Минимальный кэшбек, сум">
                  <input type="number" min={0} className={cell} value={ym.minCashback} onChange={(e) => setYm({ minCashback: num(e.target.value) })} />
                </Labeled>
              </div>
              <Labeled label="Товары Yamelly (типы Linko)">
                <TypePicker ids={ym.productTypeIds ?? []} overview={overview} onChange={(productTypeIds) => setYm({ productTypeIds })} />
              </Labeled>
              <div>
                <div className="mb-2 text-sm font-medium text-ink">{t("Фасовки")}</div>
                <div className="space-y-2">
                  {(ym.packings ?? []).map((x, i) => (
                    <div key={i} className="grid items-end gap-2 sm:grid-cols-[140px_140px_1fr_auto]">
                      <Labeled label="Вес пачки, кг">
                        <input
                          type="number"
                          min={0}
                          step={0.01}
                          className={cell}
                          value={x.unitKg}
                          onChange={(e) => setYm({ packings: (ym.packings ?? []).map((y, j) => (j === i ? { ...y, unitKg: num(e.target.value) } : y)) })}
                        />
                      </Labeled>
                      <Labeled label="Пачек в коробке">
                        <input
                          type="number"
                          min={1}
                          className={cell}
                          value={x.perBox}
                          onChange={(e) => setYm({ packings: (ym.packings ?? []).map((y, j) => (j === i ? { ...y, perBox: num(e.target.value) } : y)) })}
                        />
                      </Labeled>
                      <Labeled label="Название">
                        <input
                          className={cell}
                          value={x.name}
                          onChange={(e) => setYm({ packings: (ym.packings ?? []).map((y, j) => (j === i ? { ...y, name: e.target.value } : y)) })}
                        />
                      </Labeled>
                      <button className={button} aria-label={t("Убрать")} onClick={() => setYm({ packings: (ym.packings ?? []).filter((_, j) => j !== i) })}>
                        <Trash2 className="size-4" />
                      </button>
                    </div>
                  ))}
                  <button className={button} onClick={() => setYm({ packings: [...(ym.packings ?? []), { unitKg: 1, perBox: 4, name: "1 кг" }] })}>
                    <Plus className="size-4" />
                    {t("Фасовка")}
                  </button>
                </div>
              </div>
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                <Labeled label="Спящая: брала Yamelly с">
                  <input type="date" className={cell} value={ym.sleepingFrom ?? ""} onChange={(e) => setYm({ sleepingFrom: e.target.value || null })} />
                </Labeled>
                <Labeled label="…и не брала с">
                  <input type="date" className={cell} value={ym.sleepingSince ?? ""} onChange={(e) => setYm({ sleepingSince: e.target.value || null })} />
                </Labeled>
                <p className="pb-2 text-xs text-ink-3 sm:col-span-2 sm:self-end">
                  {t("Пусто — по умолчанию: с 1 января и с 1-го числа месяца перед началом Yamelly (для 06.11 — 01.10).")}
                </p>
              </div>
            </>
          )}
        </Section>

        <Section
          title="TW-01 — лучший агент дня"
          hint="Пн–сб: больше всех кг Twizos за день в своей группе (розница / рынок и опт), не меньше порога. День закрывается в 19:00."
          on={!!best}
          onToggle={(v) => edit({ best: v ? base.best : null })}
        >
          {best && (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
              <Labeled label="Начало">
                <input type="date" className={cell} value={best.start} onChange={(e) => setBest({ start: e.target.value })} />
              </Labeled>
              <Labeled label="Конец">
                <input type="date" className={cell} value={best.end} onChange={(e) => setBest({ end: e.target.value })} />
              </Labeled>
              <Labeled label="Порог розницы, кг">
                <input type="number" min={0} step={0.5} className={cell} value={best.retailMinKg} onChange={(e) => setBest({ retailMinKg: num(e.target.value) })} />
              </Labeled>
              <Labeled label="Порог опта, кг">
                <input type="number" min={0} step={0.5} className={cell} value={best.wholesaleMinKg} onChange={(e) => setBest({ wholesaleMinKg: num(e.target.value) })} />
              </Labeled>
              <Labeled label="Приз за победу, сум">
                <input type="number" min={0} step={1000} className={cell} value={best.prize} onChange={(e) => setBest({ prize: num(e.target.value) })} />
              </Labeled>
            </div>
          )}
        </Section>

        <Section
          title="YM-03 — план по территориям"
          hint="Кг товаров плана в доставленных заказах филиалов территории против плана; выполнено — от порога."
          on={!!terr}
          onToggle={(v) => edit({ territory: v ? base.territory : null })}
        >
          {terr && (
            <>
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
                <Labeled label="Начало">
                  <input type="date" className={cell} value={terr.start} onChange={(e) => setTerr({ start: e.target.value })} />
                </Labeled>
                <Labeled label="Конец">
                  <input type="date" className={cell} value={terr.end} onChange={(e) => setTerr({ end: e.target.value })} />
                </Labeled>
                <Labeled label="Порог выполнения, %">
                  <input type="number" min={1} max={100} className={cell} value={Math.round(terr.threshold * 100)} onChange={(e) => setTerr({ threshold: num(e.target.value) / 100 })} />
                </Labeled>
                <Labeled label="ТП — от, кг">
                  <input type="number" min={0} className={cell} value={terr.agentMinKg} onChange={(e) => setTerr({ agentMinKg: num(e.target.value) })} />
                </Labeled>
                <Labeled label="Премия ТП, сум">
                  <input type="number" min={0} step={1000} className={cell} value={terr.agentPrize} onChange={(e) => setTerr({ agentPrize: num(e.target.value) })} />
                </Labeled>
                <Labeled label="Премия СВ, сум">
                  <input type="number" min={0} step={1000} className={cell} value={terr.supervisorPrize} onChange={(e) => setTerr({ supervisorPrize: num(e.target.value) })} />
                </Labeled>
              </div>
              <Labeled label="Товары плана (типы Linko)">
                <TypePicker ids={terr.productTypeIds ?? []} overview={overview} onChange={(productTypeIds) => setTerr({ productTypeIds })} />
              </Labeled>
              <div className="space-y-2">
                {(terr.territories ?? []).map((x, i) => (
                  <div key={i} className="grid items-end gap-2 sm:grid-cols-[200px_1fr_140px_auto]">
                    <Labeled label="Территория">
                      <input
                        className={cell}
                        value={x.name}
                        onChange={(e) => setTerr({ territories: (terr.territories ?? []).map((y, j) => (j === i ? { ...y, name: e.target.value } : y)) })}
                      />
                    </Labeled>
                    <Labeled label="Филиалы Linko (через запятую)">
                      <input
                        className={cell}
                        value={x.branches.join(", ")}
                        onChange={(e) =>
                          setTerr({ territories: (terr.territories ?? []).map((y, j) => (j === i ? { ...y, branches: e.target.value.split(",").map((b) => b.trim()) } : y)) })
                        }
                      />
                    </Labeled>
                    <Labeled label="План, кг">
                      <input
                        type="number"
                        min={0}
                        className={cell}
                        value={x.planKg}
                        onChange={(e) => setTerr({ territories: (terr.territories ?? []).map((y, j) => (j === i ? { ...y, planKg: num(e.target.value) } : y)) })}
                      />
                    </Labeled>
                    <button className={button} aria-label={t("Убрать")} onClick={() => setTerr({ territories: (terr.territories ?? []).filter((_, j) => j !== i) })}>
                      <Trash2 className="size-4" />
                    </button>
                  </div>
                ))}
                <button className={button} onClick={() => setTerr({ territories: [...(terr.territories ?? []), { name: "", branches: [], planKg: 0 }] })}>
                  <Plus className="size-4" />
                  {t("Территория")}
                </button>
              </div>
            </>
          )}
        </Section>

        <Section
          title="Бюджеты акций"
          hint="Для сводки руководства (понедельник 09:00): расход против плана. 0 — не задан."
          on={!!budget}
          onToggle={(v) => edit({ budget: v ? base.budget : null })}
        >
          {budget && (
            <div className="grid gap-3 sm:grid-cols-3">
              <Labeled label="Кэшбек YM-02, сум">
                <input type="number" min={0} step={100000} className={cell} value={budget.cashback} onChange={(e) => setBudget({ cashback: num(e.target.value) })} />
              </Labeled>
              <Labeled label="Призы TW-01, сум">
                <input type="number" min={0} step={100000} className={cell} value={budget.best} onChange={(e) => setBudget({ best: num(e.target.value) })} />
              </Labeled>
              <Labeled label="Премии YM-03, сум">
                <input type="number" min={0} step={100000} className={cell} value={budget.territory} onChange={(e) => setBudget({ territory: num(e.target.value) })} />
              </Labeled>
            </div>
          )}
        </Section>

        {saved && !dirty && <p className="text-sm text-ok">{t("Сохранено.")}</p>}
        {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
        <div className="flex flex-wrap gap-2">
          <button className={primary} onClick={save} disabled={busy || !dirty}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            {t("Сохранить акции ноября")}
          </button>
          {dirty && (
            <button className={button} onClick={() => setForm(initial)} disabled={busy}>
              {t("Отмена")}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

/** Список спящих YM-01 — из Linko: сколько точек, правило, с какого дня действует; пересчёт вручную (по расписанию — по субботам). */
export function SleepingPanel({ program }: { program: IncentiveProgram }) {
  const t = useT();
  const [data, setData] = useState<IncentiveSleepingSummary | null>(null);
  const [result, setResult] = useState<IncentiveSleepingRefresh | null>(null);
  const [confirm, setConfirm] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    incentivesApi
      .sleeping(program.code)
      .then(setData)
      .catch((e: Error) => setError(e.message));
  }, [program.code]);

  const refresh = async () => {
    setBusy(true);
    setError(null);
    try {
      const r = await incentivesApi.refreshSleeping(program.code);
      setResult(r);
      setData(r.summary);
      setConfirm(false);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="rounded-xl border border-line">
      <div className="flex flex-wrap items-center gap-3 border-b border-line px-4 py-3">
        <div className="min-w-0 flex-1">
          <h3 className="font-semibold text-ink">{t("Спящие точки Yamelly")}</h3>
          <p className="text-xs text-ink-3">
            {t("Из Linko: точка брала Yamelly, но давно не брала. Пересчёт — по субботам в 19:00 на следующую неделю; разбуженные остаются в списке.")}
          </p>
        </div>
        {!confirm ? (
          <button className={button} onClick={() => setConfirm(true)} disabled={busy || !data}>
            <RefreshCw className="size-4" />
            {t("Пересчитать из Linko")}
          </button>
        ) : (
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <span className="text-ink-2">{t("Изменения — с сегодняшнего дня. Пересчитать?")}</span>
            <button className={primary} onClick={refresh} disabled={busy}>
              {busy && <Loader2 className="size-4 animate-spin" />}
              {t("Пересчитать")}
            </button>
            <button className={button} onClick={() => setConfirm(false)} disabled={busy}>
              {t("Отмена")}
            </button>
          </div>
        )}
      </div>
      <div className="space-y-2 p-4 text-sm">
        {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-bad">{error}</p>}
        {!data && !error && (
          <p className="flex items-center gap-2 text-ink-3">
            <Loader2 className="size-4 animate-spin" />
            {t("Загрузка…")}
          </p>
        )}
        {data && (
          <>
            <div className="grid gap-3 sm:grid-cols-4">
              <Stat label="В списке" value={data.active.toLocaleString("ru-RU")} />
              <Stat label="У работающих ТП" value={data.withActiveAgent.toLocaleString("ru-RU")} />
              <Stat label="Исключено" value={data.removed.toLocaleString("ru-RU")} />
              <Stat label="Действует с" value={day(data.listedFrom)} />
            </div>
            <p className="text-xs text-ink-3">
              {t("Правило: брала Yamelly с {from}, не брала с {since}.", { from: day(data.boughtFrom), since: day(data.notSince) })}{" "}
              {data.computed
                ? t("Снимка ещё нет: список считается по Linko на начало акции, первая карточка по расписанию его запишет.")
                : t("Снимок изменён {time}.", { time: when(data.changedAt) })}
            </p>
            {result && (
              <p className="text-ok">
                {t("Пересчитано: добавлено {added}, исключено {removed}, возвращено {restored}, без изменений {kept}.", {
                  added: result.added,
                  removed: result.removed,
                  restored: result.restored,
                  kept: result.kept,
                })}
              </p>
            )}
          </>
        )}
      </div>
    </div>
  );
}

function Stat({ label, value, tone = "" }: { label: string; value: string; tone?: string }) {
  const t = useT();
  return (
    <div className="rounded-lg bg-muted/60 px-3 py-2">
      <div className="text-xs text-ink-3">{t(label)}</div>
      <div className={`text-lg font-semibold tabular-nums text-ink ${tone}`}>{value}</div>
    </div>
  );
}

const weekLabel = (monday: string) => {
  const start = new Date(`${monday}T00:00:00`);
  const end = new Date(start);
  end.setDate(end.getDate() + 6);
  const f = (d: Date) => d.toLocaleDateString("ru-RU", { day: "2-digit", month: "2-digit" });
  return `${f(start)}–${f(end)}`;
};

/**
 * «Кэшбек и выплаты» (ТЗ, раздел 7): по ТП — кэшбек YM-02 оформлен, ждёт доставки, подтверждён, выплачен, к выплате, можно ещё; призы TW-01. По
 * неделе бухгалтерия отмечает выплату (сумма — сколько выплачено за неделю; 0 — снять отметку): «к выплате» на карточках уменьшается.
 */
export function PayoutsPanel({ program, canUseSales }: { program: IncentiveProgram; canUseSales: boolean }) {
  const t = useT();
  const [data, setData] = useState<IncentivePayouts | null>(null);
  const [open, setOpen] = useState<number | null>(null);
  const [onlyToPay, setOnlyToPay] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    setBusy(true);
    setError(null);
    try {
      setData(await incentivesApi.payouts(program.code));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  // Раскрытый ТП остаётся на месте и после отметки выплаты (к выплате — 0), чтобы было видно «отмечено».
  const rows = (data?.rows ?? []).filter((r) => !onlyToPay || r.cashbackToPay + r.bestToPay > 0 || r.agentId === open);
  const total = (f: (r: IncentivePayoutRow) => number) => money((data?.rows ?? []).reduce((s, r) => s + f(r), 0));
  return (
    <div className="rounded-xl border border-line">
      <div className="flex flex-wrap items-center gap-3 border-b border-line px-4 py-3">
        <div className="min-w-0 flex-1">
          <h3 className="font-semibold text-ink">{t("Кэшбек и выплаты")}</h3>
          <p className="text-xs text-ink-3">
            {t("Кэшбек YM-02 — по своим спящим точкам; подтверждён доставкой — к выплате пн–вт за прошлую неделю. Призы TW-01 — за победы дня. Отметка — сколько выплачено за неделю.")}
          </p>
        </div>
        <button className={button} onClick={load} disabled={busy || !canUseSales}>
          {busy ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
          {data ? t("Обновить") : t("Показать")}
        </button>
      </div>
      <div className="p-4">
        {!canUseSales && <Note>{t("Нужно право на данные продаж (sales.read).")}</Note>}
        {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
        {data && (
          <>
            <div className="mb-3 grid gap-3 sm:grid-cols-4">
              <Stat label="Кэшбек к выплате" value={total((r) => r.cashbackToPay)} tone="text-ok" />
              <Stat label="TW-01 к выплате" value={total((r) => r.bestToPay)} tone="text-ok" />
              <Stat label="Ждёт доставки" value={total((r) => r.pending)} />
              <Stat label="Выплачено" value={total((r) => r.cashbackPaid + r.bestPaid)} />
            </div>
            <div className="mb-2 flex flex-wrap items-center gap-3 text-xs text-ink-3">
              <label className="flex items-center gap-2 text-sm text-ink">
                <Switch checked={onlyToPay} onChange={setOnlyToPay} label={t("Только с суммой к выплате")} />
                {t("Только с суммой к выплате")}
              </label>
              <span>
                {t("на {date}", { date: day(data.date) })} · {t("данные Linko на {time}", { time: when(data.dataAsOf) })}
              </span>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[900px] text-sm">
                <thead>
                  <tr className="border-b border-line text-left text-xs text-ink-3">
                    <th className="w-6 py-2" />
                    <th className="py-2 pr-2 font-medium">{t("ТП")}</th>
                    <th className="py-2 pr-2 text-right font-medium">{t("Разбужено")}</th>
                    <th className="py-2 pr-2 text-right font-medium">{t("Оформлено")}</th>
                    <th className="py-2 pr-2 text-right font-medium">{t("Ждёт доставки")}</th>
                    <th className="py-2 pr-2 text-right font-medium">{t("Подтверждено")}</th>
                    <th className="py-2 pr-2 text-right font-medium">{t("Кэшбек к выплате")}</th>
                    <th className="py-2 pr-2 text-right font-medium">{t("TW-01 побед")}</th>
                    <th className="py-2 pr-2 text-right font-medium">{t("TW-01 к выплате")}</th>
                    <th className="py-2 text-right font-medium">{t("Можно ещё")}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <Fragment key={r.agentId}>
                      <tr className="cursor-pointer border-b border-line/60 hover:bg-muted/40" onClick={() => setOpen(open === r.agentId ? null : r.agentId)}>
                        <td className="py-1.5 text-ink-3">{open === r.agentId ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}</td>
                        <td className="py-1.5 pr-2 text-ink">
                          {r.name}
                          <span className="block text-xs text-ink-3">{r.region ?? "—"}</span>
                        </td>
                        <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">
                          {r.woken} / {r.listed}
                        </td>
                        <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">{money(r.issued)}</td>
                        <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">{money(r.pending)}</td>
                        <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">{money(r.confirmed)}</td>
                        <td className={`py-1.5 pr-2 text-right font-medium tabular-nums ${r.cashbackToPay > 0 ? "text-ok" : "text-ink-3"}`}>{money(r.cashbackToPay)}</td>
                        <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">{r.bestWins}</td>
                        <td className={`py-1.5 pr-2 text-right font-medium tabular-nums ${r.bestToPay > 0 ? "text-ok" : "text-ink-3"}`}>{money(r.bestToPay)}</td>
                        <td className="py-1.5 text-right tabular-nums text-ink-3">{money(r.canEarn)}</td>
                      </tr>
                      {open === r.agentId && (
                        <tr className="border-b border-line/60 bg-muted/20">
                          <td colSpan={10} className="p-3">
                            <WeeksTable program={program} row={r} onSaved={setData} />
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  ))}
                  {rows.length === 0 && (
                    <tr>
                      <td colSpan={10} className="py-3 text-ink-3">
                        {onlyToPay ? t("Выплачивать пока нечего.") : t("Начислений пока нет.")}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

/** Недели ТП: начислено и выплачено по кэшбеку и TW-01; отметка выплаты за неделю. */
function WeeksTable({ program, row, onSaved }: { program: IncentiveProgram; row: IncentivePayoutRow; onSaved: (d: IncentivePayouts) => void }) {
  const t = useT();
  const [edit, setEdit] = useState<{ monday: string; kind: "cashback" | "best"; amount: string; paidOn: string; note: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const start = (w: IncentivePayoutWeek, kind: "cashback" | "best") =>
    setEdit({
      monday: w.monday,
      kind,
      amount: String(kind === "cashback" ? w.confirmed || w.cashbackPaid : w.bestPrize || w.bestPaid),
      paidOn: w.paidOn ?? today(),
      note: w.note ?? "",
    });

  const save = async (amount: number) => {
    if (!edit) return;
    setBusy(true);
    setError(null);
    try {
      onSaved(
        await incentivesApi.setPayout(program.code, {
          agentId: row.agentId,
          weekStart: edit.monday,
          kind: edit.kind,
          amount,
          paidOn: edit.paidOn || null,
          note: edit.note || null,
        }),
      );
      setEdit(null);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-2">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left text-xs text-ink-3">
            <th className="py-1 pr-2 font-medium">{t("Неделя")}</th>
            <th className="py-1 pr-2 text-right font-medium">{t("Разбужено")}</th>
            <th className="py-1 pr-2 text-right font-medium">{t("Оформлено")}</th>
            <th className="py-1 pr-2 text-right font-medium">{t("Подтверждено")}</th>
            <th className="py-1 pr-2 text-right font-medium">{t("Кэшбек выплачено")}</th>
            <th className="py-1 pr-2 text-right font-medium">{t("К выплате")}</th>
            <th className="py-1 pr-2" />
            <th className="py-1 pr-2 text-right font-medium">{t("TW-01: побед · приз")}</th>
            <th className="py-1 pr-2 text-right font-medium">{t("TW-01 выплачено")}</th>
            <th className="py-1" />
          </tr>
        </thead>
        <tbody>
          {row.weeks.map((w) => (
            <tr key={w.monday} className="border-t border-line/60">
              <td className="py-1.5 pr-2 text-ink">
                {weekLabel(w.monday)}
                {w.paidOn && <span className="block text-xs text-ink-3">{t("отмечено {date}", { date: day(w.paidOn) }) + (w.note ? ` · ${w.note}` : "")}</span>}
              </td>
              <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">{w.woken}</td>
              <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">{money(w.issued)}</td>
              <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">{money(w.confirmed)}</td>
              <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">{w.cashbackPaid > 0 ? money(w.cashbackPaid) : "—"}</td>
              <td className={`py-1.5 pr-2 text-right font-medium tabular-nums ${w.cashbackToPay > 0 ? "text-ok" : "text-ink-3"}`}>{money(w.cashbackToPay)}</td>
              <td className="py-1.5 pr-2">
                {(w.confirmed > 0 || w.cashbackPaid > 0) && (
                  <button className={`${button} whitespace-nowrap`} onClick={() => start(w, "cashback")}>
                    {t("Кэшбек выплачен…")}
                  </button>
                )}
              </td>
              <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">
                {w.bestWins} · {money(w.bestPrize)}
              </td>
              <td className="py-1.5 pr-2 text-right tabular-nums text-ink-2">{w.bestPaid > 0 ? money(w.bestPaid) : "—"}</td>
              <td className="py-1.5">
                {(w.bestWins > 0 || w.bestPaid > 0) && (
                  <button className={`${button} whitespace-nowrap`} onClick={() => start(w, "best")}>
                    {t("Приз выплачен…")}
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {edit && (
        <div className="flex flex-wrap items-end gap-2 rounded-lg border border-line bg-surface p-3">
          <div className="pb-2 text-sm text-ink">
            {edit.kind === "cashback" ? t("Кэшбек YM-02") : t("Приз TW-01")} · {weekLabel(edit.monday)}
          </div>
          <Labeled label="Выплачено за неделю, сум" className="w-[190px]">
            <input type="number" min={0} className={field} value={edit.amount} onChange={(e) => setEdit({ ...edit, amount: e.target.value })} />
          </Labeled>
          <Labeled label="Когда" className="w-[160px]">
            <input type="date" className={field} value={edit.paidOn} onChange={(e) => setEdit({ ...edit, paidOn: e.target.value })} />
          </Labeled>
          <Labeled label="Пометка" className="min-w-[200px] flex-1">
            <input className={field} value={edit.note} maxLength={200} onChange={(e) => setEdit({ ...edit, note: e.target.value })} />
          </Labeled>
          <button className={primary} onClick={() => save(num(edit.amount))} disabled={busy}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            {t("Отметить")}
          </button>
          <button className={button} onClick={() => save(0)} disabled={busy}>
            {t("Снять отметку")}
          </button>
          <button className={button} onClick={() => setEdit(null)} disabled={busy}>
            {t("Отмена")}
          </button>
          {error && <p className="w-full rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
        </div>
      )}
    </div>
  );
}
