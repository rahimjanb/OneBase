"use client";

import { useEffect, useRef, useState } from "react";
import { X } from "lucide-react";
import { Note, Section } from "./bits";
import { DataTable, NameCell, type Column } from "./DataTable";
import { delta, kg, money, num, pct } from "@/lib/sales/format";
import type { CategoryCard, SkuRow, SkuStatus } from "@/lib/sales/types";

const chip = "inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium";

function Chip({ tone, children }: { tone: "ok" | "bad" | "muted"; children: React.ReactNode }) {
  const styles = { ok: "bg-ok-soft text-ok", bad: "bg-bad-soft text-bad", muted: "bg-muted text-ink-2" };
  return (
    <span className={`${chip} ${styles[tone]}`}>
      <span className="size-1.5 rounded-full bg-current" />
      {children}
    </span>
  );
}

/** Изменение к прошлому месяцу: рост — зелёный, падение до −10% — оранжевый, сильнее — красный. */
function deltaClass(value: number | null): string {
  if (value == null) return "text-ink-3";
  return value >= 0 ? "text-ok" : value > -0.1 ? "text-warn" : "text-bad";
}

function Metric({ label, value, className = "text-ink" }: { label: string; value: React.ReactNode; className?: string }) {
  return (
    // Значения ряда — на одной линии, даже если подпись переносится на две строки.
    <div className="flex min-w-0 flex-col justify-between">
      <dt className="text-[11px] leading-tight text-ink-3">{label}</dt>
      <dd className={`mt-0.5 whitespace-nowrap font-semibold tabular-nums ${className}`}>{value}</dd>
    </div>
  );
}

function Card({ card, selected, onSelect }: { card: CategoryCard; selected: boolean; onSelect: () => void }) {
  return (
    <button
      type="button"
      onClick={onSelect}
      aria-pressed={selected}
      className={`flex h-full flex-col rounded-xl border bg-surface p-4 text-left transition-[border-color,box-shadow] hover:border-accent/40 hover:shadow-md ${
        selected ? "border-accent shadow-md ring-1 ring-accent/30" : "border-line"
      }`}
    >
      <div className="font-semibold text-ink">{card.name}</div>
      <div className="mt-0.5 text-xs text-ink-3">
        продаётся {num(card.skuSold)} из {num(card.skuTotal)} SKU
      </div>

      <div className="mt-3 h-1.5 w-full overflow-hidden rounded-full bg-muted" title={`Доля по весу ${pct(card.weightShare)}`}>
        <div className="h-full rounded-full bg-accent" style={{ width: `${Math.min(100, Math.max(0, (card.weightShare ?? 0) * 100))}%` }} />
      </div>

      <dl className="mt-3 grid grid-cols-3 gap-x-3 gap-y-2 text-xs">
        <Metric label="Факт, кг" value={kg(card.factKg)} />
        <Metric label="Доля по весу" value={pct(card.weightShare)} />
        <Metric label="Выручка" value={money(card.revenue)} />
        <Metric label="АКБ" value={num(card.akb)} />
        <Metric label="Дистрибуция" value={pct(card.distribution)} />
      </dl>

      <dl className="mt-3 grid grid-cols-3 gap-x-3 gap-y-2 border-t border-dashed border-line pt-3 text-xs">
        <Metric label="Прогноз, кг" value={kg(card.forecastKg)} />
        <Metric label="Прогноз выручки" value={money(card.forecastRevenue)} />
        <Metric label="К прошлому мес." value={delta(card.vsPrevMonth)} className={deltaClass(card.vsPrevMonth)} />
      </dl>

      <div className="mt-auto flex flex-wrap gap-1.5 pt-3">
        {card.silent === 0 ? (
          <Chip tone="ok">все продаются</Chip>
        ) : (
          <Chip tone={card.lost > 0 ? "bad" : "muted"}>молчат {num(card.silent)}</Chip>
        )}
        {card.lost > 0 && <Chip tone="bad">пропало {num(card.lost)}</Chip>}
      </div>
    </button>
  );
}

const statusView: Record<SkuStatus, { label: string; tone: "ok" | "bad" | "muted" }> = {
  selling: { label: "продаётся", tone: "ok" },
  lost: { label: "пропал", tone: "bad" },
  silent: { label: "молчит", tone: "muted" },
};

const statusOrder: Record<SkuStatus, number> = { selling: 0, lost: 1, silent: 2 };

function SkuTable({ card, onClose }: { card: CategoryCard; onClose: () => void }) {
  const columns: Column<SkuRow>[] = [
    {
      key: "name",
      label: "Артикул",
      value: (r) => r.name,
      // Длинные названия переносятся, чтобы таблица помещалась без прокрутки.
      render: (r) => (
        <span className="block max-w-[440px] whitespace-normal">
          <NameCell name={r.name} sub={r.code ? `код ${r.code}` : null} />
        </span>
      ),
    },
    {
      key: "status",
      label: "Статус",
      value: (r) => statusOrder[r.status],
      render: (r) => <Chip tone={statusView[r.status].tone}>{statusView[r.status].label}</Chip>,
    },
    { key: "fact", label: "Факт, кг", align: "right", value: (r) => r.factKg, render: (r) => kg(r.factKg) },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
    { key: "akb", label: "АКБ", align: "right", value: (r) => r.akb, render: (r) => num(r.akb) },
    { key: "dist", label: "Дистрибуция", align: "right", value: (r) => r.distribution, render: (r) => pct(r.distribution, 1) },
    { key: "prev", label: "Прошлый мес., кг", align: "right", value: (r) => r.prevMonthKg, render: (r) => kg(r.prevMonthKg) },
  ];

  return (
    <DataTable
      title={`Артикулы: ${card.name}`}
      hint={`продаётся ${num(card.skuSold)} из ${num(card.skuTotal)} SKU${card.lost > 0 ? ` · пропало ${num(card.lost)}` : ""}`}
      actions={
        <button
          type="button"
          onClick={onClose}
          className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-xs font-medium text-ink hover:bg-muted"
        >
          <X className="size-3.5" />
          Закрыть
        </button>
      }
      columns={columns}
      rows={card.skus}
      rowKey={(r) => String(r.productId)}
      note="«Пропал» — продавался в прошлом месяце, в этом нет. «Молчит» — есть в ассортименте (продавался за последние полгода), но ни в прошлом, ни в этом месяце не продавался. Дистрибуция — доля ТТ с покупкой, купивших этот артикул."
    />
  );
}

/** Карточки категорий; клик по карточке открывает таблицу артикулов категории. */
export function CategoryCards({ cards, scope }: { cards: CategoryCard[]; scope: string }) {
  const [selected, setSelected] = useState<string | null>(null);
  const tableRef = useRef<HTMLDivElement>(null);
  const card = cards.find((c) => c.id === selected) ?? null;

  useEffect(() => {
    if (selected) tableRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, [selected]);

  return (
    <>
      <Section title="Категории" hint="клик — артикулы категории">
        {cards.length === 0 ? (
          <p className="py-6 text-center text-sm text-ink-3">Продаж за период нет</p>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {cards.map((c) => (
              <Card key={c.id} card={c} selected={c.id === selected} onSelect={() => setSelected((s) => (s === c.id ? null : c.id))} />
            ))}
          </div>
        )}
        <Note>
          SKU в категории — товары, которые продавались за последние полгода (признака «товар активен» в Linko нет). «Молчат» — SKU без продаж в
          этом месяце в {scope}, из них «пропало» — продавались в прошлом месяце. Доля по весу и дистрибуция (доля ТТ с покупкой, купивших категорию) —
          от итога {scope}. Прогноз — по текущему темпу; «к прошлому мес.» — прогноз к факту всего прошлого месяца. Подтипы Linko («Помадка 0,5 кг» и
          т.п.) объединены с основной категорией.
        </Note>
      </Section>
      <div ref={tableRef} className="scroll-mt-4">
        {card && <SkuTable card={card} onClose={() => setSelected(null)} />}
      </div>
    </>
  );
}
