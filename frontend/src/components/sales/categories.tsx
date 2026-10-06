"use client";

import Link from "next/link";
import { Note, Section, Stat, deltaTone } from "./bits";
import { DataTable, NameCell, type Column } from "./DataTable";
import { TopChip } from "./outstock";
import { delta, kg, money, num, outletsLabel, pct, plural } from "@/lib/sales/format";
import { withQuery } from "@/lib/sales/query";
import type { AssortmentRegionRow, CategoryCard, ProductBreakdownRow, ProductView, SkuRow, SkuStatus } from "@/lib/sales/types";

const chip = "inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium";

type Tone = "ok" | "bad" | "warn" | "muted";

function Chip({ tone, children }: { tone: Tone; children: React.ReactNode }) {
  const styles = { ok: "bg-ok-soft text-ok", bad: "bg-bad-soft text-bad", warn: "bg-warn-soft text-warn", muted: "bg-muted text-ink-2" };
  return (
    <span className={`${chip} ${styles[tone]}`}>
      <span className="size-1.5 rounded-full bg-current" />
      {children}
    </span>
  );
}

const statusView: Record<SkuStatus, { label: string; tone: Tone }> = {
  selling: { label: "Продаётся", tone: "ok" },
  lost: { label: "Пропал", tone: "bad" },
  elsewhere: { label: "Не возят", tone: "warn" },
  silent: { label: "Нет продаж", tone: "muted" },
};

const statusRank: Record<SkuStatus, number> = { selling: 0, lost: 1, elsewhere: 2, silent: 3 };

/** Статус артикула; у магазина «нет продаж» — «не брал». */
export function SkuStatusChip({ status, store = false }: { status: SkuStatus; store?: boolean }) {
  const view = statusView[status];
  return <Chip tone={view.tone}>{store && status === "silent" ? "Не брал" : view.label}</Chip>;
}

/** «Нет данных»: в регионе за месяц ни одной покупки — дыра в выгрузке, а не «не возят». */
export function NoDataChip() {
  return <Chip tone="muted">нет данных</Chip>;
}

function Card({ card, href }: { card: CategoryCard; href: string }) {
  return (
    <Link
      href={href}
      className="flex h-full flex-col rounded-xl border border-line bg-surface p-4 text-left shadow-sm transition-[border-color,box-shadow] hover:border-accent/40 hover:shadow-md max-lg:active:opacity-75"
    >
      <div className="font-semibold text-ink">{card.name}</div>
      <div className="mt-0.5 text-xs text-ink-3">
        продаётся {num(card.skuSold)} из {num(card.skuTotal)} SKU
      </div>

      <div className="mt-3 h-1.5 w-full overflow-hidden rounded-full bg-muted" title={`Доля по весу ${pct(card.weightShare)}`}>
        <div className="h-full rounded-full bg-accent" style={{ width: `${Math.min(100, Math.max(0, (card.weightShare ?? 0) * 100))}%` }} />
      </div>

      <dl className="mt-3 grid grid-cols-3 gap-x-3 gap-y-2 text-xs">
        <Stat label="Факт, кг" value={kg(card.factKg)} />
        <Stat label="Доля по весу" value={pct(card.weightShare)} />
        <Stat label="Выручка" value={money(card.revenue)} />
        <Stat label="АКБ" value={num(card.akb)} />
        <Stat label="Дистрибуция" value={pct(card.distribution)} />
      </dl>

      <dl className="mt-3 grid grid-cols-3 gap-x-3 gap-y-2 border-t border-dashed border-line pt-3 text-xs">
        <Stat label="Прогноз, кг" value={kg(card.forecastKg)} />
        <Stat label="Прогноз выручки" value={money(card.forecastRevenue)} />
        <Stat label="К прошлому мес." value={delta(card.vsPrevMonth)} tone={deltaTone(card.vsPrevMonth)} />
      </dl>

      <div className="mt-auto flex flex-wrap gap-1.5 pt-3">
        {card.silent === 0 ? (
          <Chip tone="ok">все продаются</Chip>
        ) : (
          <Chip tone={card.lost > 0 ? "bad" : "muted"}>молчат {num(card.silent)}</Chip>
        )}
        {card.lost > 0 && <Chip tone="bad">пропало {num(card.lost)}</Chip>}
      </div>
    </Link>
  );
}

/**
 * Карточки категорий; клик открывает страницу категории с её плитками и артикулами.
 * query — период и охват («region=…», «agent=…»): категория открывается в том же охвате, что и карточка.
 */
export function CategoryCards({ cards, scope, query }: { cards: CategoryCard[]; scope: string; query: string }) {
  return (
    <Section title="Категории" hint="клик — артикулы категории">
      {cards.length === 0 ? (
        <p className="py-6 text-center text-sm text-ink-3">Продаж за период нет</p>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {cards.map((c) => (
            <Card key={c.id} card={c} href={withQuery(`/sales/assortment/category/${encodeURIComponent(c.id)}`, query)} />
          ))}
        </div>
      )}
      <Note>
        SKU в категории — товары, которые продавались во вторичке с 1 января по конец выбранного месяца (признака «товар активен» в Linko нет; весь
        каталог Linko и товары, которые продавал только «Завод», сюда не входят). «Продаётся» — SKU, у которого в {scope} есть точка с положительной
        строкой (кг или сумма больше нуля); «молчат» — остальные, из них «пропало» — продавались в прошлом месяце. Доля по весу и дистрибуция (доля ТТ с
        покупкой, у которых чистый вес категории больше нуля) — от итога {scope}. Прогноз — по текущему темпу; «к прошлому мес.» — прогноз к факту всего
        прошлого месяца; у закрытого месяца их нет. Подтипы Linko («Помадка 0,5 кг» и т.п.) объединены с основной категорией.
      </Note>
    </Section>
  );
}

/** Название с переносом: длинные наименования иначе растягивают таблицу. top — метка «ТОП» (товар из списка ТОП). */
function WrapName({ name, sub, top = false }: { name: string; sub?: string | null; top?: boolean }) {
  return (
    <span className="flex max-w-[340px] items-start gap-1.5 whitespace-normal max-lg:max-w-[44vw]">
      {top && (
        <span className="mt-0.5">
          <TopChip />
        </span>
      )}
      <NameCell name={name} sub={sub} />
    </span>
  );
}

/** Артикулы категории: клик — где товар идёт, а где нет. query — период и охват; mono — «Только он», точек в охвате всего. */
export function SkuTable({ card, prevLabel, query, mono }: { card: CategoryCard; prevLabel: string; query: string; mono?: number }) {
  const columns: Column<SkuRow>[] = [
    {
      key: "name",
      label: "Продукт",
      value: (r) => r.name,
      render: (r) => <WrapName name={r.name} sub={r.code ? `Артикул ${r.code}` : null} top={r.isTop} />,
    },
    { key: "status", label: "Статус", value: (r) => statusRank[r.status], render: (r) => <SkuStatusChip status={r.status} /> },
    { key: "fact", label: "Факт, кг", align: "right", value: (r) => r.factKg, render: (r) => kg(r.factKg) },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, render: (r) => money(r.revenue) },
    { key: "share", label: "Доля по весу", align: "right", value: (r) => r.weightShare, render: (r) => pct(r.weightShare, 1) },
    { key: "akb", label: "ТТ", align: "right", value: (r) => r.akb, render: (r) => num(r.akb) },
    { key: "dist", label: "Дистрибуция", align: "right", value: (r) => r.distribution, render: (r) => pct(r.distribution) },
    {
      key: "solo",
      label: "Только он",
      align: "right",
      value: (r) => r.solo,
      render: (r) =>
        r.solo > 0 ? (
          <span title={`${outletsLabel(r.solo)} ${plural(r.solo, ["купила", "купили", "купили"])} только этот SKU — ${pct(r.soloShare, 1)} его ТТ`}>
            {num(r.solo)} <span className="text-ink-3">· {pct(r.soloShare)}</span>
          </span>
        ) : (
          <span className="text-ink-3">—</span>
        ),
    },
    { key: "prev", label: `${prevLabel}, кг`, align: "right", value: (r) => r.prevMonthKg, render: (r) => kg(r.prevMonthKg) },
  ];

  return (
    <DataTable
      title="Артикулы"
      hint="клик — где товар идёт, а где нет"
      columns={columns}
      rows={card.skus}
      rowKey={(r) => String(r.productId)}
      rowHref={(r) => withQuery(`/sales/assortment/product/${r.productId}`, query)}
      note={`«Продаётся» — есть точка с положительной строкой артикула (кг или сумма больше нуля). «Пропал» — в прошлом месяце здесь продавался, в этом пока ни одной продажи. «Не возят» — здесь ноль, а по республике товар идёт. «Нет продаж» — есть в ассортименте (продавался с начала года), но в этом месяце не продаётся нигде. ТТ — точки с положительной строкой артикула; дистрибуция — их доля от ТТ с покупкой. «Только он» — точки, которые из всех SKU купили только этот, и их доля от ТТ артикула${mono != null ? `; всего таких точек ${num(mono)}` : ""}. «ТОП» — товар из списка ТОП. «${prevLabel}» — прошлый месяц целиком: текущий ещё не закончен, килограммы в лоб не сравнивать.`}
    />
  );
}

/** Колонки «По регионам» (вкладка «Ассортимент» и страница категории): регион без покупок за месяц — «нет данных» вместо счётчиков. */
export function regionColumns(): Column<AssortmentRegionRow>[] {
  const count = (r: AssortmentRegionRow, value: number, tone?: string) =>
    r.noData ? <span className="text-ink-3">—</span> : <span className={value && tone ? tone : ""}>{num(value)}</span>;
  return [
    {
      key: "name",
      label: "Регион",
      value: (r) => r.name,
      render: (r) => (
        <span className="flex items-center gap-2">
          <WrapName name={r.name} />
          {r.noData && <NoDataChip />}
        </span>
      ),
    },
    { key: "kg", label: "Факт, кг", align: "right", value: (r) => (r.noData ? null : r.kg), render: (r) => (r.noData ? "—" : kg(r.kg)) },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => (r.noData ? null : r.revenue), render: (r) => (r.noData ? "—" : money(r.revenue)) },
    { key: "sku", label: "SKU идёт", align: "right", value: (r) => (r.noData ? null : r.skuSelling), render: (r) => count(r, r.skuSelling) },
    { key: "no", label: "Не возят", align: "right", value: (r) => (r.noData ? null : r.skuNotCarried), render: (r) => count(r, r.skuNotCarried, "text-warn") },
    { key: "lost", label: "Пропало", align: "right", value: (r) => (r.noData ? null : r.skuLost), render: (r) => count(r, r.skuLost, "text-bad") },
    { key: "akb", label: "ТТ", align: "right", value: (r) => (r.noData ? null : r.akb), render: (r) => count(r, r.akb) },
  ];
}

/** «По регионам» страницы категории: клик — эта же категория в регионе. */
export function CategoryRegionsTable({ rows, categoryId, query }: { rows: AssortmentRegionRow[]; categoryId: string; query: string }) {
  const columns = regionColumns();
  const regionQuery = (id: string) => {
    const q = new URLSearchParams(query);
    q.delete("direction");
    q.set("region", id);
    return q.toString();
  };
  return (
    <DataTable
      title="По регионам"
      hint="клик — категория в регионе"
      columns={columns}
      rows={rows}
      rowKey={(r) => r.id}
      rowHref={(r) => withQuery(`/sales/assortment/category/${encodeURIComponent(categoryId)}`, regionQuery(r.id))}
      note="«SKU идёт» — сколько артикулов категории из тех, что продаются по республике в этом месяце, есть в регионе (точка с положительной строкой). «Не возят» — остальные. «Пропало» — продавались в регионе в прошлом месяце, в этом нет. ТТ — точки региона с покупкой. «Нет данных» — в регионе за месяц ни одной покупки: это почти всегда дыра в выгрузке, а не регион, который ничего не возит."
    />
  );
}

/** Где артикул идёт, а где нет: по регионам (клик — артикул в регионе), ТП региона (клик — карточка ТП) или магазинам. */
export function ProductBreakdownTable({ data, prevLabel, query }: { data: ProductView; prevLabel: string; query: string }) {
  const kind = data.breakdown;
  const nameLabel = kind === "regions" ? "Регион" : kind === "agents" ? "ТП" : "Магазин";
  const columns: Column<ProductBreakdownRow>[] = [
    { key: "name", label: nameLabel, value: (r) => r.name, render: (r) => <WrapName name={r.name} sub={r.sub} /> },
    {
      key: "status",
      label: "Статус",
      value: (r) => (r.noData ? null : statusRank[r.status]),
      render: (r) => (r.noData ? <NoDataChip /> : <SkuStatusChip status={r.status} store={kind === "stores"} />),
    },
    { key: "kg", label: "Факт, кг", align: "right", value: (r) => (r.noData ? null : r.kg), render: (r) => (r.noData ? "—" : kg(r.kg)) },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => (r.noData ? null : r.revenue), render: (r) => (r.noData ? "—" : money(r.revenue)) },
    ...(kind === "stores"
      ? []
      : ([
          {
            key: "tt",
            label: "ТТ с товаром",
            align: "right",
            value: (r) => (r.noData ? null : r.tt),
            render: (r) => (r.noData ? "—" : kind === "agents" ? `${num(r.tt)} / ${num(r.outlets)}` : num(r.tt)),
          },
          { key: "dist", label: kind === "agents" ? "Дистрибуция у ТП" : "Дистрибуция", align: "right", value: (r) => r.distribution, render: (r) => pct(r.distribution) },
        ] satisfies Column<ProductBreakdownRow>[])),
    { key: "prev", label: `${prevLabel}, кг`, align: "right", value: (r) => r.prevMonthKg, render: (r) => kg(r.prevMonthKg) },
  ];

  const period = new URLSearchParams(query);
  for (const key of ["direction", "region", "agent", "export"]) period.delete(key);
  const href = (r: ProductBreakdownRow): string | null => {
    if (kind === "regions") {
      const q = new URLSearchParams(period);
      q.set("region", r.id);
      return withQuery(`/sales/assortment/product/${data.productId}`, q.toString());
    }
    if (kind === "agents") return withQuery(`/sales/agents/${r.id}`, period.toString());
    // Магазин открывается в продажах ТП; у экспорта своей страницы магазина нет — там продажи «Завода» не видны.
    if (data.scope !== "agent") return null;
    const q = new URLSearchParams(period);
    const agent = new URLSearchParams(query).get("agent");
    if (agent) q.set("agent", agent);
    return withQuery(`/sales/stores/${r.id}`, q.toString());
  };

  const titles = {
    regions: { title: "По регионам", hint: "клик — артикул в регионе" },
    agents: { title: "По ТП", hint: "клик — карточка ТП" },
    stores: { title: "По магазинам", hint: data.scope === "agent" ? "клик — что продано в магазин" : "покупатели экспорта" },
  } as const;

  return (
    <DataTable
      title={titles[kind].title}
      hint={titles[kind].hint}
      columns={columns}
      rows={data.rows}
      rowKey={(r) => r.id}
      rowHref={href}
      limit={kind === "stores" ? 50 : undefined}
      empty="Продаж в охвате нет"
      note={
        kind === "regions"
          ? "ТТ с товаром — точки региона с положительной строкой артикула (кг или сумма больше нуля); дистрибуция — их доля от ТТ региона с покупкой. «Не возят» — в регионе ноль, а по республике идёт. «Нет данных» — в регионе за месяц ни одной покупки."
          : kind === "agents"
            ? "ТП региона с продажами в этом месяце. ТТ с товаром — сколько его точек купили артикул (положительная строка) из всех его точек с покупкой."
            : "Магазины с заказом в этом месяце и те, кто брал артикул в прошлом. «Продаётся» — строка артикула положительна; «Пропал» — брал в прошлом месяце, в этом нет; «Не брал» — ни в этом, ни в прошлом."
      }
    />
  );
}
