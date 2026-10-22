"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, useTransition } from "react";
import { DatabaseZap, Loader2 } from "lucide-react";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { Modal } from "@/components/files/Modal";
import { bff } from "@/lib/bff";
import { DASH, dateTime, monthLabel, monthShort, num, pct, plural, tsvValue } from "@/lib/sales/format";
import { growthText, negative, periodParam, pnlText, type PnlDealer, type PnlDealerCard, type PnlGroup, type PnlLine, type PnlView } from "@/lib/sales/pnl";
import { Alert, Note, Section } from "./bits";
import { CopyButton } from "./DataTable";

/** Заголовок группы: РМ, иначе название; группа «other» — подпись сервера «Прочие контрагенты», переводится. */
const groupTitle = (g: PnlGroup, t: T) => g.manager ?? (g.id === "other" ? t(g.name) : g.name);

/**
 * «P&L дилеров» как во вкладке Полевого контроля: выбор месяца, итоги по РМ, «P&L таблицей» (строки — статьи, столбцы — дилеры по РМ, «Итого»)
 * и карточка дилера по месяцам с темпом роста. Значения считает сервер (PnlMath) — здесь только показ.
 */
export function PnlReport({ data, canLoad }: { data: PnlView; canLoad: boolean }) {
  const t = useT();
  const [dealer, setDealer] = useState<PnlDealer | null>(null);

  if (data.months.length === 0) return <Empty canLoad={canLoad} />;

  const dealers = data.groups.flatMap((g) => g.dealers);
  const selected = data.year != null && data.month != null ? monthLabel(data.year, data.month, t) : null;

  return (
    <>
      <div className="flex flex-wrap items-center gap-3">
        <MonthSelect data={data} />
        {data.lastLoad && (
          <span className="text-xs text-ink-3">
            {t("Последняя загрузка:")} {data.lastLoad.fileName ? `«${data.lastLoad.fileName}», ` : ""}
            {dateTime(data.lastLoad.appliedAt)}
            {data.lastLoad.appliedBy ? ` · ${data.lastLoad.appliedBy}` : ""}
          </span>
        )}
        {canLoad && (
          <Link href="/data-manager" className="ml-auto inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted max-lg:h-10">
            <DatabaseZap className="size-4 text-accent" />
            {t("Загрузить или проверить в AI Data Manager")}
          </Link>
        )}
      </div>

      {data.noExpenses && (
        <Alert tone="bad">
          {t("За {month} загружены только продажи — расходов (реестр «Прочие расходы», логистика) нет. Прибыль и EBIT месяца без расходов завышены.", {
            month: selected?.toLowerCase() ?? "",
          })}
        </Alert>
      )}

      {dealers.length === 0 ? (
        <Alert tone="info">{t("За {month} данных P&L нет. Выберите другой месяц.", { month: selected?.toLowerCase() ?? "" })}</Alert>
      ) : (
        <>
          <GroupTiles data={data} onDealer={setDealer} />
          <MonthTable data={data} onDealer={setDealer} />
        </>
      )}

      <Note>
        {t(
          "Валовая прибыль = Продажи − Себестоимость; Прибыль после продаж = Валовая − Отдел продаж и маркетинга + Ретробонусы; Доставка и склад = завод + дилер оплатил по журналу логистики, если рейсов дилера за месяц нет — компенсация по ст. 606; EBIT = Прибыль после продаж − Доставка − Административные − Прочие − Амортизация. Маржа и ROS итогов — от сумм. EAT и реинвестированная прибыль считаются, только когда загружены банковские услуги, налоги и дивиденды; ROA — данных об активах в OneBase нет. Себестоимость и расходы — как в загруженных отчётах: данные P&L загружаются только через AI Data Manager и не зависят от синхронизации с Linko.",
        )}
      </Note>

      {dealer && data.year != null && (
        <DealerCard dealer={dealer} year={data.year} month={data.month} onClose={() => setDealer(null)} />
      )}
    </>
  );
}

function Empty({ canLoad }: { canLoad: boolean }) {
  const t = useT();
  return (
    <div className="rounded-xl border border-dashed border-line bg-surface px-6 py-12 text-center">
      <DatabaseZap className="mx-auto size-8 text-ink-3" />
      <h2 className="mt-3 text-base font-semibold text-ink">{t("P&L пока не загружен")}</h2>
      <p className="mx-auto mt-1 max-w-xl text-sm text-ink-3">
        {t(
          "Данные P&L попадают в OneBase только через AI Data Manager: загрузите базу продаж (сумма дилера и завода), реестр «Прочие расходы», журнал логистики или готовую таблицу P&L — Claude сверит отчёт и подготовит план изменений, а применится он после вашего подтверждения.",
        )}
      </p>
      {canLoad ? (
        <Link href="/data-manager" className="mt-4 inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong">
          <DatabaseZap className="size-4" />
          {t("Открыть AI Data Manager")}
        </Link>
      ) : (
        <p className="mt-3 text-xs text-ink-3">{t("Загрузить данные может администратор с правом «AI Data Manager».")}</p>
      )}
    </div>
  );
}

/** Свой месяц раздела (как select «P&L» в Полевом контроле): только месяцы с загруженным P&L. */
function MonthSelect({ data }: { data: PnlView }) {
  const t = useT();
  const router = useRouter();
  const [pending, start] = useTransition();
  const value = data.year != null && data.month != null ? periodParam(data.year, data.month) : "";
  return (
    <label className="flex items-center gap-2 text-sm text-ink-2">
      {t("Месяц")}
      <select
        value={value}
        onChange={(e) => start(() => router.push(`/sales/pnl?pnl=${e.target.value}`))}
        className="h-9 rounded-lg border border-line bg-surface px-2.5 text-sm font-medium text-ink max-lg:h-10"
      >
        {data.months.map((m) => (
          <option key={periodParam(m.year, m.month)} value={periodParam(m.year, m.month)}>
            {monthLabel(m.year, m.month, t)}
            {!m.hasExpenses ? ` · ${t("без расходов")}` : !m.hasSales ? ` · ${t("без продаж")}` : ""}
          </option>
        ))}
      </select>
      {pending && <Loader2 className="size-4 animate-spin text-ink-3" />}
    </label>
  );
}

function line(lines: PnlLine[], key: string) {
  return lines.find((l) => l.key === key)!;
}

/** Плитки по РМ: число дилеров, продажи, EBIT, маржа — клик по дилеру открывает его карточку. */
function GroupTiles({ data, onDealer }: { data: PnlView; onDealer: (d: PnlDealer) => void }) {
  const t = useT();
  const ebit = line(data.lines, "ebit");
  return (
    <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3 min-[87.5rem]:grid-cols-4">
      {data.groups.map((g) => (
        <div key={g.id} className="rounded-xl border border-line bg-surface p-4 shadow-sm">
          <div className="text-base font-semibold text-ink">{groupTitle(g, t)}</div>
          <div className="text-xs text-ink-3">
            {g.manager ? `${g.name} · ` : ""}
            {num(g.dealers.length)} {t(plural(g.dealers.length, ["дилер", "дилера", "дилеров"]))}
          </div>
          <dl className="mt-3 grid grid-cols-3 gap-2 text-sm">
            <Figure label={t("Продажи")} value={pnlText(line(data.lines, "sales"), g.total.sales, t)} />
            <Figure label="EBIT" value={pnlText(ebit, g.total.ebit, t)} bad={negative(ebit, g.total.ebit)} />
            <Figure label={t("Маржа")} value={pct(g.total.margin, 1)} />
          </dl>
          <div className="mt-3 flex flex-wrap gap-1.5">
            {g.dealers.map((d) => (
              <button
                key={d.key}
                type="button"
                onClick={() => onDealer(d)}
                className={`rounded-full border px-2 py-0.5 text-xs hover:border-accent/50 hover:bg-muted ${
                  negative(ebit, d.values.ebit) ? "border-bad/30 text-bad" : "border-line text-ink-2"
                }`}
                title={`EBIT ${pnlText(ebit, d.values.ebit, t)}`}
              >
                {d.name}
              </button>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

function Figure({ label, value, bad = false }: { label: string; value: string; bad?: boolean }) {
  return (
    <div>
      <dt className="text-[11px] uppercase tracking-wide text-ink-3">{label}</dt>
      <dd className={`font-semibold tabular-nums ${bad ? "text-bad" : "text-ink"}`}>{value}</dd>
    </div>
  );
}

/** Итоговые строки — прибыль на каждом уровне: выделяются жирным. */
const result = (key: string) => key === "gross" || key === "after" || key === "ebit" || key === "eat";

/** Подпись строки: вычитаемые и расшифровки — с отступом, расшифровки мельче, итоговые — жирным. */
function LineLabel({ l }: { l: PnlLine }) {
  const t = useT();
  return (
    <span
      className={`block ${l.minus || l.detail ? "pl-4" : ""} ${l.detail ? "text-xs text-ink-3" : ""} ${
        result(l.key) ? "font-semibold text-ink" : !l.minus && !l.detail ? "font-medium text-ink" : ""
      }`}
    >
      {t(l.label)}
    </span>
  );
}

/** Точное значение для подсказки и копирования в Excel. */
const exact = (l: PnlLine, v: number | null | undefined) =>
  v == null || l.format === "na" ? DASH : l.format === "pct" ? pct(v, 2) : v.toLocaleString("ru-RU", { maximumFractionDigits: 2 });
const copyValue = (l: PnlLine, v: number | null | undefined) => (v == null || l.format === "na" ? "" : tsvValue(l.format === "pct" ? v * 100 : v));

/** «P&L таблицей · месяц»: строки — статьи, столбцы — дилеры по РМ (по убыванию продаж) и «Итого». Отрицательная прибыль — красным. */
function MonthTable({ data, onDealer }: { data: PnlView; onDealer: (d: PnlDealer) => void }) {
  const t = useT();
  const columns = data.groups.flatMap((g) => g.dealers.map((d) => ({ group: g, dealer: d })));
  const visible = data.lines.filter((l) => !l.detail || columns.some((c) => c.dealer.values[l.key] != null));
  const title = data.year != null && data.month != null ? monthLabel(data.year, data.month, t) : "";
  const tsv = () =>
    [
      [t("Строка"), ...columns.map((c) => c.dealer.name), t("Итого")].join("\t"),
      [t("РМ"), ...columns.map((c) => groupTitle(c.group, t)), ""].join("\t"),
      ...visible.map((l) => [t(l.label), ...columns.map((c) => copyValue(l, c.dealer.values[l.key])), copyValue(l, data.total[l.key])].join("\t")),
    ].join("\n");

  return (
    <Section title={t("P&L таблицей · {month}", { month: title })} hint={t("сум; клик по дилеру — его P&L по месяцам")} actions={<CopyButton onCopy={tsv} />}>
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-separate border-spacing-0 text-sm">
          <thead>
            <tr className="text-left text-[11px] uppercase tracking-wide text-ink-3">
              <th className="sticky left-0 z-10 bg-surface-solid px-4 py-1.5 font-semibold sm:px-5">{t("РМ")}</th>
              {data.groups.map((g) => (
                <th key={g.id} colSpan={g.dealers.length} className="border-l border-line px-3 py-1.5 text-center font-semibold">
                  {groupTitle(g, t)}
                </th>
              ))}
              <th className="border-l border-line px-3 py-1.5 text-right font-semibold" rowSpan={2}>
                {t("Итого")}
              </th>
            </tr>
            <tr className="text-left text-xs text-ink-2">
              <th className="sticky left-0 z-10 border-b border-line bg-surface-solid px-4 py-2 font-medium sm:px-5">{t("Строка P&L")}</th>
              {columns.map(({ group, dealer }, i) => (
                <th
                  key={dealer.key}
                  className={`border-b border-line px-3 py-2 text-right font-medium ${i === 0 || columns[i - 1].group.id !== group.id ? "border-l" : ""}`}
                >
                  <button type="button" onClick={() => onDealer(dealer)} className="max-w-40 text-right hover:text-accent-strong hover:underline">
                    <span className="block truncate">{dealer.name}</span>
                    {dealer.region && <span className="block truncate text-[11px] font-normal text-ink-3">{dealer.region}</span>}
                  </button>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {visible.map((l) => (
              <tr key={l.key}>
                <td className="sticky left-0 z-10 min-w-40 max-w-52 border-b border-line/60 bg-surface-solid px-4 py-1.5 sm:px-5 lg:max-w-none lg:whitespace-nowrap">
                  <LineLabel l={l} />
                </td>
                {columns.map(({ group, dealer }, i) => (
                  <Cell
                    key={dealer.key}
                    l={l}
                    v={dealer.values[l.key]}
                    edge={i === 0 || columns[i - 1].group.id !== group.id}
                    strong={result(l.key)}
                  />
                ))}
                <Cell l={l} v={data.total[l.key]} edge strong />
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Section>
  );
}

function Cell({ l, v, edge = false, strong = false }: { l: PnlLine; v: number | null | undefined; edge?: boolean; strong?: boolean }) {
  const t = useT();
  return (
    <td
      title={exact(l, v)}
      className={`whitespace-nowrap border-b border-line/60 px-3 py-1.5 text-right tabular-nums ${edge ? "border-l" : ""} ${
        negative(l, v) ? "text-bad" : l.detail ? "text-xs text-ink-3" : "text-ink"
      } ${strong ? "font-semibold" : ""}`}
    >
      {pnlText(l, v, t)}
    </td>
  );
}

/** Карточка дилера: месяцы года, «Итого» (проценты от сумм) и темп роста выбранного месяца к предыдущему. */
function DealerCard({ dealer, year, month, onClose }: { dealer: PnlDealer; year: number; month: number | null; onClose: () => void }) {
  const t = useT();
  const [card, setCard] = useState<PnlDealerCard | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let alive = true;
    bff<PnlDealerCard>(`sales/pnl/dealer?key=${encodeURIComponent(dealer.key)}&year=${year}${month ? `&month=${month}` : ""}`)
      .then((c) => alive && setCard(c))
      .catch((e: Error) => alive && setError(e.message));
    return () => {
      alive = false;
    };
  }, [dealer.key, year, month]);

  const visible = card ? card.lines.filter((l) => !l.detail || card.months.some((m) => m.values[l.key] != null)) : [];

  return (
    <Modal title={`${dealer.name} · P&L ${year}`} onClose={onClose} wide>
      {dealer.region && <p className="-mt-1 mb-3 text-xs text-ink-3">{t("Регион: {region}", { region: dealer.region })}</p>}
      {error && <Alert tone="bad" className="mt-0">{error}</Alert>}
      {!card && !error && (
        <div className="flex items-center gap-2 py-8 text-sm text-ink-3">
          <Loader2 className="size-4 animate-spin" />
          {t("Загрузка…")}
        </div>
      )}
      {card && (
        <div className="-mx-5 overflow-x-auto">
          <table className="w-full min-w-max border-separate border-spacing-0 text-sm">
            <thead>
              <tr className="text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="sticky left-0 z-10 border-b border-line bg-surface-solid px-5 py-2 font-semibold">{t("Строка P&L")}</th>
                {card.months.map((m) => (
                  <th key={m.month} className={`border-b border-line px-3 py-2 text-right font-semibold ${m.month === card.growthMonth ? "text-ink" : ""}`}>
                    {monthShort(m.month, t)}
                  </th>
                ))}
                <th className="border-b border-l border-line px-3 py-2 text-right font-semibold">{t("Итого")}</th>
                <th className="border-b border-line px-3 py-2 text-right font-semibold" title={card.growthBase ? t(card.growthBase) : undefined}>
                  {t("Темп роста")}
                </th>
              </tr>
            </thead>
            <tbody>
              {visible.map((l) => (
                <tr key={l.key}>
                  <td className="sticky left-0 z-10 min-w-40 max-w-52 border-b border-line/60 bg-surface-solid px-5 py-1.5 lg:max-w-none lg:whitespace-nowrap">
                    <LineLabel l={l} />
                  </td>
                  {card.months.map((m) => (
                    <Cell key={m.month} l={l} v={m.values[l.key]} strong={result(l.key)} />
                  ))}
                  <Cell l={l} v={card.total[l.key]} edge strong />
                  <td className="whitespace-nowrap border-b border-line/60 px-3 py-1.5 text-right text-xs tabular-nums text-ink-2">
                    {card.growth ? growthText(l, card.growth[l.key], t) : DASH}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="mt-2 px-5 text-xs text-ink-3">
            {card.growthBase
              ? t("Темп роста — {base}: деньги — изменение в %, маржа и ROS — разница в п. п.", { base: t(card.growthBase) })
              : t("Темп роста: за предыдущий месяц данных нет.")}
          </p>
        </div>
      )}
    </Modal>
  );
}
