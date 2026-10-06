import Link from "next/link";
import { Alert, KpiTile } from "@/components/sales/bits";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { ScopeSelect } from "@/components/sales/ScopeSelect";
import { SkuCategoryMatrix, SkuFacts, SkuMonthsTable, SkuRegionMatrix, SkuSalesPeriod, SkuSalesTable, SkuTopRegions } from "@/components/sales/sku-sales";
import { ApiRequestError, apiGet } from "@/lib/server-api";
import { date, kg, money, monthLabel, monthShort, num, pct, plural, productsLabel } from "@/lib/sales/format";
import { monthIndex } from "@/lib/sales/months";
import { param, queryWith, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { SalesMonth, SkuSalesView } from "@/lib/sales/types";

export const metadata = { title: "Продажи по SKU · Продажи" };

const views = [
  { key: "sum", label: "SKU итого" },
  { key: "reg", label: "По регионам" },
  { key: "mon", label: "По месяцам" },
  { key: "cat", label: "Категории × регионы" },
  { key: "top", label: "Топ-10 регионов" },
  { key: "notes", label: "Выводы" },
] as const;

type View = (typeof views)[number]["key"];

const tops = [
  { key: "all", label: "Все SKU" },
  { key: "only", label: "Только ТОП" },
  { key: "not", label: "Кроме ТОПа" },
] as const;

const abcs = [
  { key: "all", label: "ABC" },
  { key: "A", label: "A" },
  { key: "B", label: "B" },
  { key: "C", label: "C" },
] as const;

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

/** Текст ошибки из ответа 400 ({ error: «…» }). */
function errorText(error: ApiRequestError): string {
  try {
    const body = JSON.parse(error.message) as { error?: string };
    return body.error ?? error.message;
  } catch {
    return error.message;
  }
}

/** Страница с сервера; ошибка в параметрах (400) — текст для пользователя, остальные ошибки — на страницу ошибки раздела. */
async function load(query: string): Promise<{ data: SkuSalesView | null; error: string | null }> {
  try {
    return { data: await apiGet<SkuSalesView>(withQuery("/api/sales/sku-sales", query), "/sales/sku-sales"), error: null };
  } catch (e) {
    if (!(e instanceof ApiRequestError) || e.status !== 400) throw e;
    return { data: null, error: errorText(e) };
  }
}

/** «Янв–окт 2026», «Сентябрь 2026», «Ноя 2025 – фев 2026». */
function periodName(data: SkuSalesView): string {
  const first = data.months[0];
  const last = data.months[data.months.length - 1];
  if (!first || !last) return `${data.from} – ${data.to}`;
  if (data.months.length === 1) return monthLabel(first.year, first.month);
  return first.year === last.year
    ? `${monthShort(first.month)}–${monthShort(last.month).toLowerCase()} ${last.year}`
    : `${monthShort(first.month)} ${first.year} – ${monthShort(last.month).toLowerCase()} ${last.year}`;
}

/**
 * «Продажи по SKU» (DOC §9.6, DOC-filters §6): вторичка по SKU за любой отрезок месяцев — кг, сумма, АКБ SKU, ABC, регионы, месяцы, выводы.
 * Новых источников нет: тот же факт, что во «Вторичке», за несколько месяцев. Период, регион, ТОП, ABC, категории и раздел — параметры адреса;
 * все числа считает сервер.
 */
export default async function SkuSalesPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  let from = param(sp, "from");
  let to = param(sp, "to");
  const fi = monthIndex(from);
  const ti = monthIndex(to);
  if (fi != null && ti != null && fi > ti) [from, to] = [to, from]; // перепутанные границы меняются местами

  const view: View = views.find((v) => v.key === param(sp, "view"))?.key ?? "sum";
  const api = queryWith("", { from, to, region: param(sp, "region"), top: param(sp, "top"), abc: param(sp, "abc"), cat: param(sp, "cat") });
  const [months, { data, error }] = await Promise.all([apiGet<SalesMonth[]>("/api/sales/months", "/sales/sku-sales"), load(api)]);

  if (!data) {
    return (
      <SalesFrame title="Продажи по SKU" subtitle="Вторичка по SKU за выбранный период" crumbs={[{ label: "Продажи по SKU" }]} sp={sp}>
        <Alert tone="bad">
          {error}{" "}
          <Link className="text-accent hover:underline" href="/sales/sku-sales">
            Сбросить период
          </Link>
        </Alert>
      </SalesFrame>
    );
  }

  const d = data;
  const t = d.totals;
  // Ссылки фильтров — с нормализованными значениями из ответа: по умолчанию (все, ABC) в адрес не пишутся.
  const base = {
    from: d.from,
    to: d.to,
    region: d.regionId,
    top: d.top === "all" ? null : d.top,
    abc: d.abc === "all" ? null : d.abc,
    cat: d.selectedCategories.join(",") || null,
    view: view === "sum" ? null : view,
  };
  const href = (changes: Partial<Record<keyof typeof base, string | null>>) => withQuery("/sales/sku-sales", queryWith("", { ...base, ...changes }));
  const toggleCategory = (name: string) =>
    href({ cat: (d.selectedCategories.includes(name) ? d.selectedCategories.filter((c) => c !== name) : [...d.selectedCategories, name]).join(",") || null });
  const filtered = d.regionId != null || d.top !== "all" || d.abc !== "all" || d.selectedCategories.length > 0;
  const period = periodName(d);
  const partial = d.months.some((m) => m.partial && m.days > 0);
  const scopeName = d.regionName ?? "вся страна";

  return (
    <SalesFrame
      title="Продажи по SKU"
      subtitle={`Вторичка по SKU: кг, сумма, АКБ, ABC · ${period} · ${scopeName}`}
      crumbs={[{ label: "Продажи по SKU" }]}
      sp={sp}
    >
      <div className="flex flex-wrap items-center gap-3">
        <SkuSalesPeriod months={months} from={d.from} to={d.to} />
        <ScopeSelect label="Регион" keys={["region"]} options={[{ value: "", label: "Вся страна" }, ...d.regions.map((r) => ({ value: `region:${r.id}`, label: r.name }))]} />
      </div>
      <div className="mt-3 flex flex-wrap items-center gap-3">
        {d.topConfigured ? (
          <Chips items={tops} current={d.top} href={(k) => href({ top: k === "all" ? null : k })} title="ТОП — коды товаров из настройки Sales:TopProducts" />
        ) : (
          <span className="rounded-lg border border-dashed border-line px-3 py-1.5 text-xs text-ink-3">ТОП-товары не настроены (Sales:TopProducts)</span>
        )}
        <Chips items={abcs} current={d.abc} href={(k) => href({ abc: k === "all" ? null : k })} title="ABC считается внутри выборки: регион, ТОП, категории" />
        <span className="text-[11px] uppercase tracking-wide text-ink-3">
          {d.selectedCategories.length ? `категории: ${d.selectedCategories.join(", ")}` : "клик по карточке категории — фильтр (можно несколько); карточки учитывают регион, ТОП и ABC"}
        </span>
        {filtered && (
          <Link href={href({ region: null, top: null, abc: null, cat: null })} className="ml-auto rounded-lg border border-line px-3 py-1.5 text-sm text-ink hover:bg-muted">
            Сбросить фильтры
          </Link>
        )}
      </div>

      <div className="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile label="Кг" value={kg(t.kg)} unit="кг">
          {period} · {scopeName}
        </KpiTile>
        <KpiTile label="Сумма" value={money(t.revenue)} unit="сум">
          {num(t.pricePerKg)} сум за кг
        </KpiTile>
        <KpiTile label="SKU с продажами" value={num(t.skus)}>
          группа A — {num(t.skusA)} SKU (80% веса){d.regionId == null ? ` · регионов ${num(t.regions)}` : ""}
        </KpiTile>
        <KpiTile label="Топ-5 SKU" value={pct(t.top5Share)}>
          {d.selectedCategories.length ? `выбранные категории — ${pct(t.shareOfAll, 1)} веса карточек` : "доля в весе"}
        </KpiTile>
      </div>
      <p className="mt-3 text-xs text-ink-3">
        Нетто: заказы по дате приёмки минус возвраты по дате возврата, без «Завода» и «К К Мерч»; только восемь категорий отчёта
        {d.outsideReport > 0 ? ` (ещё ${productsLabel(d.outsideReport)} других типов Linko — бонус, подарки — не ${plural(d.outsideReport, ["входит", "входят", "входят"])})` : ""}.
        {partial && d.dataThrough ? ` Последний месяц не закрыт — данные по ${date(d.dataThrough)}.` : ""}
      </p>

      {d.categories.length > 0 && (
        <div className="mt-4 grid grid-cols-2 gap-3 md:grid-cols-3 min-[87.5rem]:grid-cols-4">
          {d.categories.map((c) => (
            <Link
              key={c.name}
              href={toggleCategory(c.name)}
              aria-pressed={c.selected}
              className={`block min-w-0 rounded-xl border bg-surface px-4 py-3.5 shadow-sm transition max-lg:px-3.5 max-lg:py-3 ${
                c.selected ? "border-accent ring-1 ring-accent" : "border-line hover:border-ink-3"
              }`}
            >
              <div className="truncate text-sm font-semibold text-ink">{c.name}</div>
              <div className="text-xs text-ink-3">{num(c.skus)} SKU</div>
              <div className="mt-2 text-[22px] font-semibold leading-none tracking-tight text-ink tabular-nums max-lg:text-xl">
                {kg(c.kg)} <span className="text-xs font-normal text-ink-3">кг</span>
              </div>
              <div className="mt-3 grid grid-cols-3 gap-x-3 gap-y-2 text-xs">
                <Fact label="Доля кг" value={pct(c.kgShare, 1)} />
                <Fact label="Доля денег" value={pct(c.revenueShare, 1)} />
                <Fact label="Цена, сум/кг" value={num(c.pricePerKg)} />
              </div>
            </Link>
          ))}
        </div>
      )}

      <div className="mt-6 flex flex-wrap items-center gap-3">
        <Chips items={views} current={view} href={(k) => href({ view: k === "sum" ? null : k })} />
      </div>

      {view === "sum" && <SkuSalesTable data={d} />}
      {view === "reg" && <SkuRegionMatrix data={d} />}
      {view === "mon" && <SkuMonthsTable data={d} />}
      {view === "cat" && <SkuCategoryMatrix data={d} />}
      {view === "top" && <SkuTopRegions data={d} />}
      {view === "notes" && <SkuFacts data={d} period={period} />}
    </SalesFrame>
  );
}
