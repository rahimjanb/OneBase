import { Alert, CollapsedSections, KpiTile } from "@/components/sales/bits";
import { OutstockGroupTable, OutstockPairsTable } from "@/components/sales/outstock";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { ScopeSelect } from "@/components/sales/ScopeSelect";
import { apiGet } from "@/lib/server-api";
import { date, dateTime, kg, money, monthLabel, num, pct } from "@/lib/sales/format";
import { apiQuery, param, periodQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { OutstockView } from "@/lib/sales/types";

export const metadata = { title: "Аутсток · Продажи" };

/**
 * «Аутсток»: дни, когда у дилера не было товара, который он обычно продаёт, и сколько продаж на этом потеряно.
 * Остаток по дням восстановлен назад от снимка Linko; период — месяц, как во вторичке.
 */
export default async function OutstockPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const region = param(sp, "region");
  const data = await apiGet<OutstockView>(`/api/sales/outstock${apiQuery(sp, ["region"])}`, "/sales/outstock");
  const q = periodQuery(sp);
  const t = data.totals;
  const regionName = data.regions.find((r) => r.id === region)?.name;
  const core = data.pairs.filter((p) => p.core);
  const withLoss = data.pairs.filter((p) => p.zeroDays > 0);
  const dealerShare = t.lostSum > 0 ? t.dealerLossSum / t.lostSum : null;

  return (
    <SalesFrame
      title="Аутсток"
      subtitle={`Дни без товара у дилеров и упущенные продажи за ${monthLabel(data.year, data.month).toLowerCase()}${regionName ? ` · ${regionName}` : ""}`}
      crumbs={[{ label: "Аутсток" }]}
      sp={sp}
    >
      <div className="flex flex-wrap items-center gap-3">
        <ScopeSelect
          label="Регион"
          keys={["region"]}
          options={[{ value: "", label: "Вся республика" }, ...data.regions.map((r) => ({ value: `region:${r.id}`, label: r.name }))]}
        />
      </div>

      {data.days === 0 ? (
        <Alert tone="info">
          Снимок остатков Linko сделан {date(data.snapshotDate)} — раньше начала {monthLabel(data.year, data.month).toLowerCase()}. Восстановить остаток на эти дни не из чего:
          выберите месяц, который уже начался к дате снимка, или нажмите «Обновить».
        </Alert>
      ) : (
        <>
          <div className="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-3 min-[87.5rem]:grid-cols-5">
            <KpiTile label="Упущено" value={money(t.lostSum)} unit="сум" tone={t.lostSum > 0 ? "bad" : "ok"} title="Дней в нуле × средние продажи в день × средняя цена товара у дилера">
              {kg(t.lostKg)} кг за {num(data.days)} дн.
            </KpiTile>
            <KpiTile label="Ядро потерь" value={num(t.corePairs)} unit={`из ${num(t.pairsWithLoss)} пар`} tone="accent" title="Самые дорогие пары «товар × регион», которые вместе дают 80% упущенного">
              {t.corePairs > 0 ? `дают ${money(t.coreSum)} — 80% потерь` : "потерь нет"}
            </KpiTile>
            <KpiTile label="Хронических" value={num(t.chronic)} unit="пар" tone={t.chronic > 0 ? "bad" : "ok"} title="В нуле половину периода и дольше">
              в нуле от {num(Math.ceil(data.days / 2))} дн. из {num(data.days)}
            </KpiTile>
            <KpiTile label="Недовоз" value={dealerShare == null ? "—" : pct(dealerShare)} tone={dealerShare == null ? "muted" : dealerShare >= 0.5 ? "warn" : "ink"} title="Доля потерь, когда на заводе товар в тот день был">
              {data.factoryKnown ? `${money(t.dealerLossSum)} · завод ${money(t.factoryLossSum)}` : "склад завода в Linko не найден"}
            </KpiTile>
            <KpiTile label="Точность" value={t.negativeSharePct == null ? "—" : `${num(t.negativeSharePct, 1)}%`} tone={t.negativeSharePct != null && t.negativeSharePct > 5 ? "warn" : "muted"} title="Доля клеток «товар × регион × день», ушедших в минус при расчёте назад: даты приёмки и проводки не совпали">
              клеток в минусе — считаются нулём
            </KpiTile>
          </div>
          <p className="mt-3 text-xs text-ink-3">
            Период {date(data.from)} – {date(data.to)} ({num(data.days)} дн.), снимок остатков Linko на {dateTime(data.syncedAt)}. Пар «товар × регион» с продажами: {num(t.pairs)},
            с днями в нуле: {num(t.pairsWithLoss)}. Это оценка, а не учёт — реальные потери, скорее, немного больше.
          </p>

          <OutstockPairsTable rows={core} from={data.from} title="Ядро потерь" hint={`${num(t.corePairs)} пар дают 80% упущенного — закрыть дыры в них значит вернуть 4/5 потерь`} />
          <OutstockGroupTable
            rows={data.byRegion}
            title="По регионам"
            hint="клик — аутсток региона"
            nameLabel="Регион"
            linkQuery={q}
          />
          <CollapsedSections>
            <OutstockGroupTable rows={data.byProduct} title="По товарам" nameLabel="Товар" limit={30} />
            <OutstockPairsTable rows={withLoss} from={data.from} title="Все пары с потерями" hint="товар × регион, хотя бы один день в нуле" limit={50} showRegion />
          </CollapsedSections>
        </>
      )}
    </SalesFrame>
  );
}
