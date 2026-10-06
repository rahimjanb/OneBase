import Link from "next/link";
import { Alert, CollapsedSections, KpiTile, Section } from "@/components/sales/bits";
import { OutstockCalendar, OutstockMatrixTable, OutstockPairsTable, OutstockProductsTable, OutstockRegionsTable } from "@/components/sales/outstock";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { ScopeSelect } from "@/components/sales/ScopeSelect";
import { apiGet } from "@/lib/server-api";
import { date, dateTime, kg, money, monthLabel, num, pct } from "@/lib/sales/format";
import { outstockInsights, plural } from "@/lib/sales/outstock-insights";
import { apiQuery, param, periodQuery, queryWith, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { OutstockScope, OutstockView } from "@/lib/sales/types";

export const metadata = { title: "Аутсток · Продажи" };

const scopes: { key: OutstockScope; label: string }[] = [
  { key: "top", label: "Только ТОП" },
  { key: "all", label: "Все SKU" },
  { key: "rest", label: "Кроме ТОПа" },
];

function Chips<T extends string>({ items, current, href, title }: { items: readonly { key: T; label: string }[]; current: T; href: (key: T) => string; title?: string }) {
  return (
    <div className="no-scrollbar flex max-w-full overflow-x-auto rounded-lg border border-line text-sm" title={title}>
      {items.map((i) => (
        <Link key={i.key} href={href(i.key)} className={`shrink-0 whitespace-nowrap px-3 py-1.5 max-lg:py-2.5 ${current === i.key ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}>
          {i.label}
        </Link>
      ))}
    </div>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <div className="text-[10px] font-medium uppercase tracking-[0.08em] text-ink-3">{label}</div>
      <div className="font-medium tabular-nums text-ink">{value}</div>
    </div>
  );
}

/**
 * «Аутсток» (DOC-rules §9.5, DOC-filters §5): дни, когда у дилера не было товара, который он обычно продаёт, и сколько продаж на этом потеряно.
 * Всегда последний закрытый месяц (месяц перед месяцем снимка остатков) — переключателя периода нет; остаток по дням восстановлен назад от снимка Linko.
 * Область: ТОП / все / кроме ТОПа, карточки категорий — фильтр (можно несколько), «Сбросить»; регион — область (region) или календарь по дням
 * (calendar — итоги и таблицы при этом остаются по области). Все числа, включая «Выводы», считает сервер.
 */
export default async function OutstockPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const region = param(sp, "region");
  const data = await apiGet<OutstockView>(`/api/sales/outstock${apiQuery(sp, ["region", "scope", "cat", "calendar"])}`, "/sales/outstock");
  const t = data.totals;
  const selected = data.selectedCategories;
  const regionInfo = data.regions.find((r) => r.id === region);
  const calendarRegion = data.regions.find((r) => r.id === data.calendarRegionId);
  const month = monthLabel(data.year, data.month);

  // Явный закрытый месяц в адресе (year, month) переносится между ссылками; без него сервер берёт последний закрытый.
  const href = (changes: { scope?: OutstockScope; cat?: string[]; region?: string | null; calendar?: string | null }) => {
    const scope = changes.scope ?? data.scope;
    const query = queryWith(periodQuery(sp), {
      region: changes.region === undefined ? region : changes.region,
      scope: scope === "top" ? null : scope,
      cat: (changes.cat ?? selected).join(",") || null,
      calendar: changes.calendar === undefined ? data.calendarRegionId : changes.calendar,
    });
    return withQuery("/sales/outstock", query);
  };
  const toggleCategory = (name: string) => href({ cat: selected.includes(name) ? selected.filter((c) => c !== name) : [...selected, name] });
  const linkQuery = queryWith(periodQuery(sp), { region, scope: data.scope === "top" ? null : data.scope, cat: selected.join(",") || null });
  const scopeLabel = data.scope === "top" ? "ТОП-товары" : data.scope === "rest" ? "товары вне ТОПа" : "все SKU";
  const insights = outstockInsights(data);

  return (
    <SalesFrame
      title={`Аутсток · ${month}`}
      subtitle={`Дни без товара у дилеров и упущенные продажи за последний закрытый месяц${regionInfo ? ` · ${regionInfo.name}` : ""}`}
      crumbs={[{ label: "Аутсток" }]}
      sp={sp}
    >
      <div className="flex flex-wrap items-center gap-3">
        <ScopeSelect
          label="Регион"
          keys={["region"]}
          options={[{ value: "", label: "Вся республика" }, ...data.regions.map((r) => ({ value: `region:${r.id}`, label: r.name }))]}
        />
        {data.topConfigured ? (
          <Chips items={scopes} current={data.scope} href={(k) => href({ scope: k })} title={data.topHint} />
        ) : (
          <span className="rounded-lg border border-dashed border-line px-3 py-1.5 text-xs text-ink-3" title="Коды ТОП-товаров Linko задаются в настройке Sales:TopProducts; тогда появятся «Только ТОП / Все SKU / Кроме ТОПа»">
            {data.topHint}
          </span>
        )}
        {data.categories.length > 0 && <span className="text-[11px] uppercase tracking-wide text-ink-3">клик по карточке категории — фильтр (можно несколько)</span>}
        {data.canReset && (
          <Link href={href({ scope: "top", cat: [] })} className="ml-auto rounded-lg border border-line px-3 py-1.5 text-sm text-ink hover:bg-muted">
            Сбросить
          </Link>
        )}
      </div>

      {data.days === 0 ? (
        <Alert tone="info">
          Снимок остатков Linko сделан {date(data.snapshotDate)} — раньше начала {month.toLowerCase()}. Восстановить остаток на эти дни не из чего: нажмите «Обновить», когда появится
          новый снимок.
        </Alert>
      ) : (
        <>
          {data.categories.length > 0 && (
            <div className="mt-4 grid grid-cols-2 gap-3 md:grid-cols-3 min-[87.5rem]:grid-cols-5">
              {data.categories.map((c) => (
                <Link
                  key={c.name}
                  href={toggleCategory(c.name)}
                  aria-pressed={c.selected}
                  className={`block min-w-0 rounded-xl border bg-surface px-4 py-3.5 shadow-sm transition max-lg:px-3.5 max-lg:py-3 ${
                    c.selected ? "border-accent ring-1 ring-accent" : "border-line hover:border-ink-3"
                  }`}
                >
                  <div className="truncate text-sm font-semibold text-ink">{c.name}</div>
                  <div className="text-xs text-ink-3">{pct(c.share, 1)} всех потерь</div>
                  <div className="mt-2 text-[22px] font-semibold leading-none tracking-tight text-bad tabular-nums max-lg:text-xl">{money(c.lostSum)}</div>
                  <div className="mt-3 grid grid-cols-2 gap-x-3 gap-y-2 text-xs">
                    <Fact label="Упущено, кг" value={kg(c.lostKg)} />
                    <Fact label="К факту" value={pct(c.lossShare, 1)} />
                    <Fact label="Позиций" value={num(c.pairs)} />
                    <Fact label="Дней в нуле" value={num(c.zeroDays)} />
                  </div>
                </Link>
              ))}
            </div>
          )}

          <div className="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <KpiTile label="Упущено, кг" value={kg(t.lostKg)} unit="кг" tone={t.lostKg > 0 ? "bad" : "ok"} title="Дней в нуле × средние продажи в день, по всем парам «товар × регион» области">
              {pct(t.lossShare, 1)} к факту продаж
            </KpiTile>
            <KpiTile label="Упущено, сум" value={money(t.lostSum)} tone={t.lostSum > 0 ? "ink" : "ok"} title="Упущенные кг × средняя цена товара у дилера за период">
              {month} · {scopeLabel}
              {selected.length ? ` · ${selected.join(", ")}` : ""}
            </KpiTile>
            <KpiTile label="Позиций с потерями" value={num(t.pairsWithLoss)} title="Пары «товар × регион», которые хотя бы один день стояли в нуле">
              {num(t.productsWithLoss)} {plural(t.productsWithLoss, "товар", "товара", "товаров")} в {num(t.regionsWithLoss)} {plural(t.regionsWithLoss, "регионе", "регионах", "регионах")}
            </KpiTile>
            <KpiTile label="Дней в нуле" value={num(t.zeroDays)} title="Сумма дней без товара по всем парам «товар × регион»; чья потеря — по складу завода в тот же день">
              недовоз {num(t.dealerDays)} · завод {num(t.factoryDays)}
              {t.unknownDays > 0 ? ` · нет данных ${num(t.unknownDays)}` : ""}
            </KpiTile>
          </div>
          <p className="mt-3 text-xs text-ink-3">
            Период {date(data.from)} – {date(data.to)} ({num(data.days)} дн.), снимок остатков Linko на {dateTime(data.syncedAt)}. Пар «товар × регион» с продажами: {num(t.pairs)}.{" "}
            {data.topConfigured ? `${data.topHint}. ` : ""}
            {t.negativeSharePct != null ? `Клеток, ушедших в минус при расчёте назад (показаны нулём): ${num(t.negativeSharePct, 1)}%. ` : ""}
            Это оценка, а не учёт — реальные потери, скорее, немного больше.
          </p>

          {insights.length > 0 && (
            <Section title="Выводы" hint="считаются из данных и меняются вместе с фильтрами">
              <div className="space-y-3 text-sm leading-relaxed text-ink-2">
                {insights.map((i) => (
                  <div key={i.title}>
                    <div className="font-semibold text-ink">{i.title}</div>
                    <p>{i.text}</p>
                  </div>
                ))}
              </div>
            </Section>
          )}

          {calendarRegion && <OutstockCalendar pairs={data.calendar} from={data.from} days={data.days} region={calendarRegion.name} closeHref={href({ calendar: null })} />}

          <OutstockRegionsTable rows={data.byRegion} linkQuery={linkQuery} activeId={data.calendarRegionId} />
          <OutstockMatrixTable matrix={data.matrix} />
          <OutstockProductsTable rows={data.byProduct} limit={30} />
          <CollapsedSections>
            <OutstockPairsTable rows={data.pairs} from={data.from} title="Все пары с потерями" hint="товар × регион, хотя бы один день в нуле" limit={50} />
          </CollapsedSections>
        </>
      )}
    </SalesFrame>
  );
}
