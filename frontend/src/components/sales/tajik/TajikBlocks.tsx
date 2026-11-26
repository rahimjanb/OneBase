import Link from "next/link";
import { Alert, KpiTile, Stat, SummaryCard, deltaTone } from "@/components/sales/bits";
import { getT } from "@/i18n/server";
import { dateTime, dayMonth, delta, kg, monthLabel, num, pct } from "@/lib/sales/format";
import { withQuery } from "@/lib/sales/query";
import {
  NO_REGION,
  TAJIK_BASE,
  somoni,
  tajikHref,
  type TajikCategoryRow,
  type TajikCategoryShare,
  type TajikCompare,
  type TajikOverview,
  type TajikRegionRow,
  type TajikSyncInfo,
  type TajikTotals,
} from "@/lib/sales/tajik";

// Цвета категорий на полосе состава: по порядку, токены темы (видны в светлой и тёмной).
const BAR = ["bg-accent", "bg-ok", "bg-warn", "bg-bad", "bg-ink-3", "bg-accent/50", "bg-ok/50", "bg-warn/50", "bg-bad/50"];

/**
 * Четыре плитки: вес, сумма, точки, ТП — с «к прошлому месяцу за те же дни», если есть с чем сравнить. store — плитки точки: вес, сумма,
 * накладные, товары (без сравнения).
 */
export async function TajikKpis({ totals, prev, store = false }: { totals: TajikTotals; prev: TajikCompare | null; store?: boolean }) {
  const t = await getT();
  const window = prev ? `${dayMonth(prev.from)}–${dayMonth(prev.to)}` : null;
  return (
    <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      <KpiTile label={t("Факт, кг")} value={kg(totals.kg)} tone={prev ? deltaTone(prev.vsKg) : "ink"}>
        {store ? t("{n} ед. товара", { n: num(totals.qty) }) : prev ? t("{delta} к {window} прошлого месяца", { delta: delta(prev.vsKg), window: window! }) : t("сравнить не с чем — в 1С нет прошлого месяца")}
      </KpiTile>
      <KpiTile label={t("Сумма")} value={somoni(totals.amount)} unit={t("сомони")}>
        {totals.discount > 0 ? t("скидки {sum} сомони", { sum: somoni(totals.discount) }) : t("без скидок")}
      </KpiTile>
      {store ? (
        <>
          <KpiTile label={t("Накладных")} value={num(totals.documents)} />
          <KpiTile label={t("Товаров")} value={num(totals.products)} />
        </>
      ) : (
        <>
          <KpiTile label={t("Точек с покупкой")} value={num(totals.stores)}>
            {t("{n} накладных", { n: num(totals.documents) })}
          </KpiTile>
          <KpiTile label={t("ТП")} value={num(totals.agents)}>
            {t("{n} товаров", { n: num(totals.products) })}
          </KpiTile>
        </>
      )}
    </div>
  );
}

/** Полоса состава по категориям (доли веса). */
export async function ShareBar({ shares }: { shares: TajikCategoryShare[] }) {
  const t = await getT();
  if (shares.length === 0) return <div className="h-2 rounded-full bg-muted" />;
  return (
    <div className="flex h-2 overflow-hidden rounded-full bg-muted" role="img" aria-label={shares.map((s) => `${t(s.name)} ${pct(s.share)}`).join(", ")}>
      {shares.map((s, i) => (
        <span key={s.name} className={BAR[i % BAR.length]} style={{ width: `${Math.max(0, (s.share ?? 0) * 100)}%` }} title={`${t(s.name)} ${pct(s.share)}`} />
      ))}
    </div>
  );
}

/** «Все регионы» карточками, как в «Республике»: вес, сумма, доля, к прошлому месяцу, точки и ТП, состав по категориям. */
export async function TajikRegionCards({ rows, query }: { rows: TajikRegionRow[]; query: string }) {
  const t = await getT();
  if (rows.length === 0) return null;
  return (
    <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
      {rows.map((r) => (
        <SummaryCard
          key={r.id}
          title={r.id === NO_REGION ? t(r.name) : r.name}
          subtitle={`${t("{n} точек", { n: num(r.stores) })} · ${t("{n} ТП", { n: num(r.agents) })}`}
          href={tajikHref(`${TAJIK_BASE}/regions/${r.id}`, query)}
        >
          <div className="mt-4">
            <ShareBar shares={r.categories} />
          </div>
          <dl className="mt-4 grid grid-cols-2 gap-x-6 gap-y-3">
            <Stat label={t("Факт, кг")} value={kg(r.kg)} />
            <Stat label={t("Доля")} value={pct(r.share)} />
            <Stat label={t("Сумма, сомони")} value={somoni(r.amount)} />
            <Stat label={t("К прошл. мес.")} value={delta(r.vsPrev)} tone={deltaTone(r.vsPrev)} />
          </dl>
          <p className="mt-3 line-clamp-2 text-xs text-ink-3">
            {r.categories
              .slice(0, 4)
              .map((c) => `${t(c.name)} ${pct(c.share)}`)
              .join(" · ")}
          </p>
        </SummaryCard>
      ))}
    </div>
  );
}

/**
 * Категории плитками: вес, доля, сумма, точки. hrefOf — ссылка плитки (на странице региона — фильтр по категории); active — выбранная.
 */
export async function TajikCategoryTiles({
  rows,
  hrefOf,
  active,
  allHref,
}: {
  rows: TajikCategoryRow[];
  hrefOf?: (name: string) => string;
  active?: string | null;
  allHref?: string;
}) {
  const t = await getT();
  if (rows.length === 0) return null;
  const tile = (selected: boolean) =>
    `block rounded-xl border bg-surface px-4 py-3 shadow-sm transition-colors ${selected ? "border-accent ring-2 ring-accent/20" : "border-line hover:border-accent/40"}`;
  return (
    <div className="mt-6">
      <h2 className="text-lg font-semibold text-ink">{t("Категории")}</h2>
      <div className="mt-3 grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
        {allHref && (
          <Link href={allHref} className={tile(!active)} aria-current={!active ? "true" : undefined}>
            <div className="text-sm font-semibold text-ink">{t("Все категории")}</div>
            <div className="mt-1 text-xs text-ink-3">{t("{kg} кг", { kg: kg(rows.reduce((s, r) => s + r.kg, 0)) })}</div>
          </Link>
        )}
        {rows.map((c) => {
          const body = (
            <>
              <div className="flex items-baseline justify-between gap-2">
                <span className="truncate text-sm font-semibold text-ink">{t(c.name)}</span>
                <span className="text-xs tabular-nums text-ink-3">{pct(c.share)}</span>
              </div>
              <div className="mt-1 text-lg font-semibold tabular-nums text-ink">
                {kg(c.kg)} <span className="text-xs font-normal text-ink-3">{t("кг")}</span>
              </div>
              <div className="mt-0.5 text-xs text-ink-3">
                {somoni(c.amount)} {t("сомони")} · {t("{n} точек", { n: num(c.stores) })}
              </div>
            </>
          );
          return hrefOf ? (
            <Link key={c.name} href={hrefOf(c.name)} className={tile(active === c.name)} aria-current={active === c.name ? "true" : undefined}>
              {body}
            </Link>
          ) : (
            <div key={c.name} className={tile(false)}>
              {body}
            </div>
          );
        })}
      </div>
    </div>
  );
}

/** Состояние загрузки из 1С: не настроено, выключено, последняя ошибка — предупреждением; иначе — строка «обновлено …». */
export async function TajikSyncNotice({ sync, canSetup }: { sync: TajikSyncInfo; canSetup: boolean }) {
  const t = await getT();
  const settings = "/settings/integrations/sales/tj1c";
  if (!sync.configured) {
    return (
      <Alert tone="info">
        {t("Подключение к 1С Таджикистана не настроено: нужны адрес API и ключ доступа.")}{" "}
        {canSetup && (
          <Link href={settings} className="font-medium text-accent-strong hover:underline">
            {t("Настроить")}
          </Link>
        )}
      </Alert>
    );
  }
  if (!sync.enabled) {
    return <Alert tone="info">{t("Загрузка из 1С Таджикистана выключена — данные не обновляются.")}</Alert>;
  }
  if (sync.lastStatus === "failed" || sync.lastStatus === "partial") {
    return (
      <Alert tone={sync.lastStatus === "failed" ? "bad" : "warn"}>
        {t("Последняя загрузка из 1С Таджикистана {when}: {error}", { when: dateTime(sync.lastRunAt), error: t(sync.lastError ?? "") })}{" "}
        {sync.lastSuccessAt && t("Данные — на {when}.", { when: dateTime(sync.lastSuccessAt) })}
      </Alert>
    );
  }
  return sync.lastSuccessAt ? (
    <p className="mt-3 text-xs text-ink-3">{t("Данные 1С Таджикистана обновлены {when}", { when: dateTime(sync.lastSuccessAt) })}</p>
  ) : null;
}

/**
 * Карточка «Таджикистан» на странице «Экспорт и опт»: итоги месяца по 1С дилера и регионы с долей веса; вся карточка ведёт в раздел
 * «Таджикистан» (все регионы, точки, категории, остаток).
 */
export async function TajikExportCard({ data, query, className = "mt-6" }: { data: TajikOverview; query: string; className?: string }) {
  const t = await getT();
  const p = data.period;
  const regions = data.regions.filter((r) => r.kg > 0);
  return (
    <div className={className}>
      <SummaryCard
        title={t("Таджикистан — продажи дилера по 1С")}
        subtitle={
          data.totals.documents === 0
            ? t("За месяц продаж в 1С Таджикистана нет")
            : `${monthLabel(p.year, p.month, t)}${p.dataThrough && !p.closed ? ` · ${t("данные по {date}", { date: dayMonth(p.dataThrough) })}` : ""} · ${t("суммы в сомони")}`
        }
        href={withQuery(TAJIK_BASE, query)}
      >
        {data.totals.documents > 0 && (
          <>
            <dl className="mt-4 grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-4">
              <Stat label={t("Факт, кг")} value={kg(data.totals.kg)} />
              <Stat label={t("Сумма, сомони")} value={somoni(data.totals.amount)} />
              <Stat label={t("Точек с покупкой")} value={num(data.totals.stores)} />
              <Stat label={t("К прошл. мес.")} value={delta(data.prev?.vsKg)} tone={deltaTone(data.prev?.vsKg)} />
            </dl>
            <ul className="mt-4 grid gap-x-6 gap-y-2 sm:grid-cols-2 xl:grid-cols-3">
              {regions.slice(0, 9).map((r) => (
                <li key={r.id} className="min-w-0">
                  <div className="flex items-baseline justify-between gap-2 text-sm">
                    <span className="truncate text-ink">{r.id === NO_REGION ? t(r.name) : r.name}</span>
                    <span className="shrink-0 tabular-nums text-ink-2">
                      {kg(r.kg)} {t("кг")} · {pct(r.share)}
                    </span>
                  </div>
                  <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-muted">
                    <span className="block h-full bg-accent" style={{ width: `${Math.max(2, (r.share ?? 0) * 100)}%` }} />
                  </div>
                </li>
              ))}
            </ul>
            <p className="mt-3 text-xs text-ink-3">
              {regions.length > 9 ? t("и ещё {n} регионов · ", { n: num(regions.length - 9) }) : ""}
              {t("Все регионы, точки и категории — в разделе «Таджикистан»")}
            </p>
          </>
        )}
      </SummaryCard>
    </div>
  );
}
