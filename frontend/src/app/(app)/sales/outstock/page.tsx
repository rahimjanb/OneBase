import Link from "next/link";
import { Alert, CollapsedSections, KpiTile, Section } from "@/components/sales/bits";
import { OutstockCalendar, OutstockMatrixTable, OutstockPairsTable, OutstockProductsTable, OutstockRegionsTable } from "@/components/sales/outstock";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { MonthSelect, ScopeSelect } from "@/components/sales/ScopeSelect";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { date, dateTime, dayMonth, kg, money, monthLabel, num, pct } from "@/lib/sales/format";
import { outstockInsights, plural } from "@/lib/sales/outstock-insights";
import { apiQuery, param, periodQuery, queryWith, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { OutstockScope, OutstockView } from "@/lib/sales/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Аутсток · Продажи") };
}

const scopes: { key: OutstockScope; label: string }[] = [
  { key: "top", label: "Только ТОП" },
  { key: "all", label: "Все SKU" },
  { key: "rest", label: "Кроме ТОПа" },
];

async function Chips<T extends string>({ items, current, href, title }: { items: readonly { key: T; label: string }[]; current: T; href: (key: T) => string; title?: string }) {
  const t = await getT();
  return (
    <div className="no-scrollbar flex max-w-full overflow-x-auto rounded-lg border border-line text-sm" title={title}>
      {items.map((i) => (
        <Link key={i.key} href={href(i.key)} className={`shrink-0 whitespace-nowrap px-3 py-1.5 max-lg:py-2.5 ${current === i.key ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}>
          {t(i.label)}
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
  const tr = await getT();
  const sp = await searchParams;
  const region = param(sp, "region");
  const data = await apiGet<OutstockView>(`/api/sales/outstock${apiQuery(sp, ["region", "scope", "cat", "calendar"])}`, "/sales/outstock");
  const t = data.totals;
  const selected = data.selectedCategories;
  const regionInfo = data.regions.find((r) => r.id === region);
  const calendarRegion = data.regions.find((r) => r.id === data.calendarRegionId);
  const month = monthLabel(data.year, data.month, tr).replace(/^\S+/, (w) => tr(w));
  // Месяцы аутстока: идущий (по дату снимка) и шесть закрытых до него. Остаток восстанавливается назад от снимка Linko, поэтому чем
  // старше месяц, тем длиннее путь назад и ниже точность (доля клеток «ушедших в минус» — в примечании под плитками).
  const [snapYear, snapMonth] = data.snapshotDate.split("-").map(Number);
  const monthOptions = Array.from({ length: 7 }, (_, i) => {
    const d = new Date(Date.UTC(snapYear, snapMonth - 1 - i, 1));
    const [y, m] = [d.getUTCFullYear(), d.getUTCMonth() + 1];
    const label = monthLabel(y, m, tr).replace(/^\S+/, (w) => tr(w));
    return { value: `${y}-${m}`, label: i === 0 ? tr("{month} — идёт, по {date}", { month: label, date: dayMonth(data.snapshotDate) }) : label };
  });
  const partial = data.days < data.daysInMonth;
  const older = (snapYear - data.year) * 12 + (snapMonth - data.month) > 1;

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
  const scopeLabel = data.scope === "top" ? tr("ТОП-товары") : data.scope === "rest" ? tr("товары вне ТОПа") : tr("все SKU");
  const insights = outstockInsights(data, tr);

  return (
    <SalesFrame
      title={`${tr("Аутсток")} · ${month}`}
      subtitle={`${partial ? tr("Дни без товара у дилеров и упущенные продажи: месяц идёт, 1–{day} число", { day: num(data.days) }) : tr("Дни без товара у дилеров и упущенные продажи за месяц")}${regionInfo ? ` · ${regionInfo.name}` : ""}`}
      crumbs={[{ label: tr("Аутсток") }]}
      sp={sp}
    >
      <div className="flex flex-wrap items-center gap-3">
        <MonthSelect label={tr("Месяц")} value={`${data.year}-${data.month}`} options={monthOptions} />
        <ScopeSelect
          label={tr("Регион")}
          keys={["region"]}
          options={[{ value: "", label: tr("Вся республика") }, ...data.regions.map((r) => ({ value: `region:${r.id}`, label: r.name }))]}
        />
        {data.topConfigured ? (
          <Chips items={scopes} current={data.scope} href={(k) => href({ scope: k })} title={tr(data.topHint)} />
        ) : (
          <span className="rounded-lg border border-dashed border-line px-3 py-1.5 text-xs text-ink-3" title={tr("Коды ТОП-товаров Linko задаются в настройке Sales:TopProducts; тогда появятся «Только ТОП / Все SKU / Кроме ТОПа»")}>
            {tr(data.topHint)}
          </span>
        )}
        {data.categories.length > 0 && <span className="text-[11px] uppercase tracking-wide text-ink-3">{tr("клик по карточке категории — фильтр (можно несколько)")}</span>}
        {data.canReset && (
          <Link href={href({ scope: "top", cat: [] })} className="ml-auto rounded-lg border border-line px-3 py-1.5 text-sm text-ink hover:bg-muted">
            {tr("Сбросить")}
          </Link>
        )}
      </div>

      {older && data.days > 0 && (
        <p className="mt-2 text-xs text-ink-3">
          {tr("Остаток за {month} восстановлен назад от снимка Linko {date}: чем старше месяц, тем длиннее путь назад и ниже точность — смотрите долю клеток, ушедших в минус, под плитками.", {
            month: month.toLowerCase(),
            date: date(data.snapshotDate),
          })}
        </p>
      )}

      {data.days === 0 ? (
        <Alert tone="info">
          {tr("Снимок остатков Linko сделан {date} — раньше начала {month}. Восстановить остаток на эти дни не из чего: нажмите «Обновить», когда появится новый снимок.", {
            date: date(data.snapshotDate),
            month: month.toLowerCase(),
          })}
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
                  <div className="text-xs text-ink-3">{tr("{pct} всех потерь", { pct: pct(c.share, 1) })}</div>
                  <div className="mt-2 text-[1.375rem] font-semibold leading-none tracking-tight text-bad tabular-nums max-lg:text-xl">{money(c.lostSum, tr)}</div>
                  <div className="mt-3 grid grid-cols-2 gap-x-3 gap-y-2 text-xs">
                    <Fact label={tr("Упущено, кг")} value={kg(c.lostKg)} />
                    <Fact label={tr("К факту")} value={pct(c.lossShare, 1)} />
                    <Fact label={tr("Позиций")} value={num(c.pairs)} />
                    <Fact label={tr("Дней в нуле")} value={num(c.zeroDays)} />
                  </div>
                </Link>
              ))}
            </div>
          )}

          <div className="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <KpiTile
              label={tr("Упущено, кг")}
              value={kg(t.lostKg)}
              unit={tr("кг")}
              tone={t.lostKg > 0 ? "bad" : "ok"}
              title={tr("Дней в нуле × средние продажи в день, по всем парам «товар × регион» области")}
            >
              {tr("{pct} к факту продаж", { pct: pct(t.lossShare, 1) })}
            </KpiTile>
            <KpiTile label={tr("Упущено, сум")} value={money(t.lostSum, tr)} tone={t.lostSum > 0 ? "ink" : "ok"} title={tr("Упущенные кг × средняя цена товара у дилера за период")}>
              {month} · {scopeLabel}
              {selected.length ? ` · ${selected.join(", ")}` : ""}
            </KpiTile>
            <KpiTile label={tr("Позиций с потерями")} value={num(t.pairsWithLoss)} title={tr("Пары «товар × регион», которые хотя бы один день стояли в нуле")}>
              {tr("{products} {productWord} в {regions} {regionWord}", {
                products: num(t.productsWithLoss),
                productWord: plural(t.productsWithLoss, "товар", "товара", "товаров"),
                regions: num(t.regionsWithLoss),
                regionWord: plural(t.regionsWithLoss, "регионе", "регионах", "регионах"),
              })}
            </KpiTile>
            <KpiTile
              label={tr("Дней в нуле")}
              value={num(t.zeroDays)}
              title={tr("Сумма дней без товара по всем парам «товар × регион»; чья потеря — по складу завода в тот же день")}
            >
              {tr("недовоз {dealer} · завод {factory}", { dealer: num(t.dealerDays), factory: num(t.factoryDays) })}
              {t.unknownDays > 0 ? tr(" · нет данных {n}", { n: num(t.unknownDays) }) : ""}
            </KpiTile>
          </div>
          <p className="mt-3 text-xs text-ink-3">
            {tr("Период {from} – {to} ({days} дн.), снимок остатков Linko на {syncedAt}. Пар «товар × регион» с продажами: {pairs}.", {
              from: date(data.from),
              to: date(data.to),
              days: num(data.days),
              syncedAt: dateTime(data.syncedAt),
              pairs: num(t.pairs),
            })}{" "}
            {data.topConfigured ? `${tr(data.topHint)}. ` : ""}
            {t.negativeSharePct != null ? tr("Клеток, ушедших в минус при расчёте назад (показаны нулём): {n}%. ", { n: num(t.negativeSharePct, 1) }) : ""}
            {tr("Это оценка, а не учёт — реальные потери, скорее, немного больше.")}
          </p>

          {insights.length > 0 && (
            <Section title={tr("Выводы")} hint={tr("считаются из данных и меняются вместе с фильтрами")}>
              <div className="space-y-3 text-sm leading-relaxed text-ink-2">
                {insights.map((i) => (
                  <div key={i.title}>
                    <div className="font-semibold text-ink">{tr(i.title)}</div>
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
            <OutstockPairsTable rows={data.pairs} from={data.from} title={tr("Все пары с потерями")} hint={tr("товар × регион, хотя бы один день в нуле")} limit={50} />
          </CollapsedSections>
        </>
      )}
    </SalesFrame>
  );
}
