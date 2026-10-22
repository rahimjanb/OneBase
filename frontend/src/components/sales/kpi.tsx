"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { ChevronRight, Loader2, Lock, MapPin } from "lucide-react";
import { Alert, KpiTile, Note } from "./bits";
import { CopyButton } from "./DataTable";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { bff } from "@/lib/bff";
import { plural } from "@/lib/format";
import { dateTime, kg, money, num, pct, tsvValue } from "@/lib/sales/format";
import type { KpiCell, KpiMoney, KpiPerson, KpiRegion, KpiTone, KpiView } from "@/lib/sales/types";

// Раздел «KPI»: KPI-показатели Linko по людям. Цифры, премии и прогноз считает сервер (SalesAnalytics.Kpi); страница переключает режим
// «на сегодня / прогноз» и рисует. Деньги приходят только с правом «Финансы».

type Mode = "now" | "fc";

const toneText: Record<KpiTone, string> = { ok: "text-ok", warn: "text-warn", bad: "text-bad" };
const toneBar: Record<KpiTone, string> = { ok: "bg-ok", warn: "bg-warn", bad: "bg-bad" };

const moneyOf = (r: { now: KpiMoney; forecast: KpiMoney }, mode: Mode) => (mode === "fc" ? r.forecast : r.now);
const sumText = (value: number | null | undefined, t: T) => (value == null ? "—" : money(value, t));

/** Факт и план в единице показателя: сумы — сокращённо, кг — целыми, АКБ и штуки — целыми. */
const valueText = (value: number, unit: string, t: T) => (unit === "сум" ? money(value, t) : num(value, 0));

export function KpiReport({ data, canAssign }: { data: KpiView; canAssign: boolean }) {
  const t = useT();
  const [mode, setMode] = useState<Mode>("now");
  const [open, setOpen] = useState<Set<string>>(() => new Set(data.regions.filter((r) => r.id === "none").map((r) => r.id)));
  const total = moneyOf(data, mode);

  if (data.regions.length === 0) {
    return (
      <Alert tone="info" className="mt-2">
        {t("KPI за этот месяц в Linko нет — показатели появятся после пересчёта планов в Linko.")}
      </Alert>
    );
  }

  const openRegion = (id: string) => {
    setOpen((s) => new Set(s).add(id));
    requestAnimationFrame(() => document.getElementById(`kpi-${id}`)?.scrollIntoView({ behavior: "smooth", block: "start" }));
  };

  const byId = new Map(data.regions.map((r) => [r.id, r]));
  const unassigned = data.regions.find((r) => r.id === "none");

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex rounded-lg border border-line text-sm" role="group" aria-label={t("Режим")}>
          {(["now", "fc"] as const).map((m) => (
            <button
              key={m}
              type="button"
              onClick={() => setMode(m)}
              aria-pressed={mode === m}
              disabled={m === "fc" && !data.running}
              title={m === "fc" && !data.running ? t("Месяц закрыт — прогноз не нужен") : undefined}
              className={`px-3 py-1.5 first:rounded-l-lg last:rounded-r-lg disabled:opacity-40 max-lg:py-2.5 ${mode === m ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}
            >
              {m === "now" ? t("На сегодня") : t("Прогноз на месяц")}
            </button>
          ))}
        </div>
        <span className="text-xs text-ink-3">
          {data.loadedAt && t("Пересчёт Linko загружен {time}", { time: dateTime(data.loadedAt) })}
          {mode === "fc" && ` · ${t("прогноз: факт ÷ {share} месяца", { share: pct(data.share, 1) })}`}
        </span>
      </div>

      <div className="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile label={t("Факт ТП")} value={kg(mode === "fc" && data.forecastKgTp != null ? data.forecastKgTp : data.factKgTp)} unit={t("кг")}>
          {mode === "fc" ? t("прогноз вторички ТП на месяц") : t("вторичка {n} ТП за месяц", { n: num(data.tpCount) })}
        </KpiTile>
        {data.money ? (
          <>
            <KpiTile label={t("Премии")} value={sumText(total.premium, t)} tone={total.premium ? "ok" : "muted"}>
              {t("ТП {tp} · СВР {svr}", { tp: sumText(total.premiumTp, t), svr: sumText(total.premiumSvr, t) })}
            </KpiTile>
            <KpiTile label={t("Фонд оплаты")} value={sumText(total.payroll, t)}>
              {t("оклад плюс премии по Linko")}
            </KpiTile>
          </>
        ) : (
          <KpiTile label={t("Премии и оклады")} value="—" tone="muted">
            {t("видны с правом «Финансы»")}
          </KpiTile>
        )}
        <KpiTile label={t("Регионы")} value={num(data.regions.filter((r) => r.id !== "none").length)} tone={unassigned || data.missingRegions.length ? "warn" : "ink"}>
          {unassigned
            ? t("{n} без региона — укажите ниже", { n: num(unassigned.people.length) })
            : data.missingRegions.length
              ? t("без KPI в Linko: {n}", { n: num(data.missingRegions.length) })
              : t("у всех людей регион определён")}
        </KpiTile>
      </div>

      {!data.money && (
        <p className="mt-3 flex items-center gap-2 text-xs text-ink-3">
          <Lock className="size-3.5" aria-hidden />
          {t("Оклады, бонусы и премии скрыты: нужно право «Финансы». Выполнение и факт видны всем с доступом к продажам.")}
        </p>
      )}

      <RegionSummary data={data} mode={mode} onOpen={openRegion} />

      {data.directions.map((d) => (
        <section key={d.name} className="mt-8">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <div>
              <div className="text-[11px] font-medium uppercase tracking-[0.08em] text-ink-3">{t("Направление")}</div>
              <h2 className="text-lg font-semibold text-ink">{t(d.name)}</h2>
            </div>
            <div className="flex flex-wrap gap-2 text-xs">
              <span className="rounded-full bg-muted px-2.5 py-1 text-ink-2">
                {num(d.regionIds.length)} {t(plural(d.regionIds.length, ["регион", "региона", "регионов"]))} · {num(d.tp)} {t("ТП")}
              </span>
              {data.money && (
                <>
                  <span className="rounded-full bg-muted px-2.5 py-1 text-ink-2">
                    {t("Премии")}: {sumText(mode === "fc" ? d.premiumForecast : d.premiumNow, t)}
                  </span>
                  <span className="rounded-full bg-accent-soft px-2.5 py-1 font-medium text-accent-strong">
                    {t("Фонд оплаты")}: {sumText(mode === "fc" ? d.payrollForecast : d.payrollNow, t)}
                  </span>
                </>
              )}
            </div>
          </div>
          {d.regionIds.map((id) => {
            const region = byId.get(id);
            return region ? (
              <RegionCard
                key={id}
                region={region}
                mode={mode}
                money={data.money}
                open={open.has(id)}
                onToggle={() => setOpen((s) => (s.has(id) ? new Set([...s].filter((x) => x !== id)) : new Set(s).add(id)))}
              />
            ) : null;
          })}
        </section>
      ))}

      {unassigned && (
        <section className="mt-8">
          <div className="text-[11px] font-medium uppercase tracking-[0.08em] text-warn">{t("Требует внимания")}</div>
          <h2 className="text-lg font-semibold text-ink">{t("Без региона")}</h2>
          <p className="mt-1 text-sm text-ink-2">
            {t("У этих людей в Linko нет филиала, а по продажам, визитам и названиям регион не определился. Укажите регион — он сохранится в профиле и подтянется во всём разделе продаж.")}
          </p>
          <RegionCard region={unassigned} mode={mode} money={data.money} open onToggle={() => {}} assign={canAssign ? data.regionOptions : undefined} />
        </section>
      )}

      {data.missingRegions.length > 0 && (
        <Note>{t("Регионы с продажами, у которых в Linko нет KPI за месяц: {list}. Для них премия не считается.", { list: data.missingRegions.join(", ") })}</Note>
      )}
      <Note>
        {t(
          "KPI — из Linko (API планов): у каждого человека свой набор показателей. В ячейке — выполнение, под ним факт / план и премия. Премия — по уровням показателя в Linko: от порога уровня (обычно 80%, АКБ — 90%) — бонус × выполнение, выше 100% не растёт, до порога — 0. «На сегодня» — премия, начисленная Linko; «Прогноз» — факт растянут на месяц текущим темпом (АКБ не растягивается — точки не складываются по дням) и премия пересчитана по тем же уровням. Зарплата = оклад + премии. Факт ТП — вторичка агента в кг, факт СВР — вторичка всех ТП региона.",
        )}
      </Note>
    </>
  );
}

/** «По регионам»: строки — регионы по фонду оплаты; клик раскрывает карточку региона ниже. */
function RegionSummary({ data, mode, onOpen }: { data: KpiView; mode: Mode; onOpen: (id: string) => void }) {
  const t = useT();
  const rows = data.regions;
  const th = "whitespace-nowrap px-3 py-2 text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const td = "px-3 py-2.5 text-right tabular-nums";
  const sep = "border-l border-line";
  const totals = moneyOf(data, mode);

  const copy = () => {
    const head = [t("Регион"), t("Направление"), t("СВР"), t("ТП"), t("Факт СВР, кг"), t("Факт ТП, кг")];
    const moneyHead = data.money ? [t("Премии СВР"), t("ФОТ СВР"), t("Премии ТП"), t("ФОТ ТП"), t("Фонд оплаты"), t("Премии всего")] : [];
    return [
      [...head, ...moneyHead].join("\t"),
      ...rows.map((r) => {
        const m = moneyOf(r, mode);
        return [r.name, r.direction ?? "", r.svr, r.tp, r.factKgSvr, r.factKgTp, ...(data.money ? [m.premiumSvr, m.payrollSvr, m.premiumTp, m.payrollTp, m.payroll, m.premium] : [])]
          .map((v) => tsvValue(v))
          .join("\t");
      }),
    ].join("\n");
  };

  return (
    <section className="mt-6 rounded-xl border border-line bg-surface p-4 sm:p-5">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
          <h2 className="text-base font-semibold text-ink">{t("По регионам")}</h2>
          <span className="text-xs text-ink-3">{t("нажмите на регион — откроется его карточка")}</span>
        </div>
        <CopyButton onCopy={copy} />
      </div>
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className={`${th} sticky left-0 z-[1] bg-surface-solid pl-4 text-left sm:pl-5`}>{t("Регион")}</th>
              <th className={`${th} text-right`}>{t("СВР")}</th>
              <th className={`${th} text-right`}>{t("ТП")}</th>
              <th className={`${th} ${sep} text-right`}>{t("Факт СВР, кг")}</th>
              {data.money && <th className={`${th} text-right`}>{t("Премии СВР")}</th>}
              {data.money && <th className={`${th} text-right`}>{t("ФОТ СВР")}</th>}
              <th className={`${th} ${sep} text-right`}>{t("Факт ТП, кг")}</th>
              {data.money && <th className={`${th} text-right`}>{t("Премии ТП")}</th>}
              {data.money && <th className={`${th} text-right`}>{t("ФОТ ТП")}</th>}
              {data.money && <th className={`${th} border-l-2 border-line text-right`}>{t("Фонд оплаты")}</th>}
              {data.money && <th className={`${th} pr-4 text-right sm:pr-5`}>{t("Премии всего")}</th>}
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => {
              const m = moneyOf(r, mode);
              return (
                <tr key={r.id} onClick={() => onOpen(r.id)} className="group/row cursor-pointer border-b border-line hover:bg-muted">
                  <td className="sticky left-0 z-[1] bg-surface-solid px-3 py-2.5 pl-4 group-hover/row:bg-muted-solid sm:pl-5">
                    <span className={`block font-medium ${r.id === "none" ? "text-warn" : "text-ink"}`}>{t(r.name)}</span>
                    <span className="block text-xs text-ink-3">
                      {[r.direction ? t(r.direction) : null, r.unit === "сум" ? t("план в сумах") : t("план в кг")].filter(Boolean).join(" · ")}
                    </span>
                  </td>
                  <td className={td}>{num(r.svr)}</td>
                  <td className={td}>{num(r.tp)}</td>
                  <td className={`${td} ${sep}`}>{r.svr ? kg(r.factKgSvr) : "—"}</td>
                  {data.money && <td className={td}>{sumText(m.premiumSvr, t)}</td>}
                  {data.money && <td className={td}>{sumText(m.payrollSvr, t)}</td>}
                  <td className={`${td} ${sep}`}>{kg(r.factKgTp)}</td>
                  {data.money && <td className={td}>{sumText(m.premiumTp, t)}</td>}
                  {data.money && <td className={td}>{sumText(m.payrollTp, t)}</td>}
                  {data.money && <td className={`${td} border-l-2 border-line font-semibold`}>{sumText(m.payroll, t)}</td>}
                  {data.money && <td className={`${td} pr-4 font-semibold sm:pr-5 ${m.premium ? "text-ok" : ""}`}>{sumText(m.premium, t)}</td>}
                </tr>
              );
            })}
          </tbody>
          <tfoot>
            <tr className="font-semibold text-ink">
              <td className="sticky left-0 z-[1] bg-surface-solid px-3 py-2.5 pl-4 sm:pl-5">{t("Итого")}</td>
              <td className={td}>{num(data.svrCount)}</td>
              <td className={td}>{num(data.tpCount)}</td>
              <td className={`${td} ${sep}`}>{kg(rows.reduce((a, r) => a + r.factKgSvr, 0))}</td>
              {data.money && <td className={td}>{sumText(totals.premiumSvr, t)}</td>}
              {data.money && <td className={td}>{sumText(totals.payrollSvr, t)}</td>}
              <td className={`${td} ${sep}`}>{kg(rows.reduce((a, r) => a + r.factKgTp, 0))}</td>
              {data.money && <td className={td}>{sumText(totals.premiumTp, t)}</td>}
              {data.money && <td className={td}>{sumText(totals.payrollTp, t)}</td>}
              {data.money && <td className={`${td} border-l-2 border-line`}>{sumText(totals.payroll, t)}</td>}
              {data.money && <td className={`${td} pr-4 sm:pr-5`}>{sumText(totals.premium, t)}</td>}
            </tr>
          </tfoot>
        </table>
      </div>
    </section>
  );
}

/** Карточка региона: люди × показатели; заголовок с итогами раскрывает таблицу. */
function RegionCard({
  region,
  mode,
  money: withMoney,
  open,
  onToggle,
  assign,
}: {
  region: KpiRegion;
  mode: Mode;
  money: boolean;
  open: boolean;
  onToggle: () => void;
  assign?: { id: string; name: string }[];
}) {
  const t = useT();
  const m = moneyOf(region, mode);
  const th = "whitespace-nowrap px-3 py-2 text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const fc = mode === "fc";

  return (
    <div id={`kpi-${region.id}`} className="mt-3 scroll-mt-24 rounded-xl border border-line bg-surface">
      <button type="button" onClick={onToggle} aria-expanded={open} className="flex w-full flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3 text-left max-lg:min-h-11 sm:px-5">
        <ChevronRight className={`size-4 shrink-0 text-ink-3 transition-transform ${open ? "rotate-90" : ""}`} />
        <span className="min-w-0 flex-1">
          <span className="block font-semibold text-ink">{t(region.name)}</span>
          <span className="block text-xs text-ink-3">
            {[
              region.svr ? `${num(region.svr)} ${t("СВР")}` : null,
              `${num(region.tp)} ${t("ТП")}`,
              region.unit === "сум" ? t("план в сумах") : t("план в кг"),
              t("факт ТП {kg} кг", { kg: kg(region.factKgTp) }),
            ]
              .filter(Boolean)
              .join(" · ")}
          </span>
        </span>
        {withMoney && (
          <span className="flex flex-wrap gap-2 text-xs">
            <span className="rounded-full bg-muted px-2.5 py-1 text-ink-2">
              {t("Премии")}: <b className={m.premium ? "text-ok" : "text-ink"}>{sumText(m.premium, t)}</b>
            </span>
            <span className="rounded-full bg-muted px-2.5 py-1 text-ink-2">
              {t("Фонд оплаты")}: <b className="text-ink">{sumText(m.payroll, t)}</b>
            </span>
          </span>
        )}
      </button>
      {open && (
        <div className="-mx-px overflow-x-auto border-t border-line">
          <table className="w-full min-w-max border-collapse text-sm">
            <thead>
              <tr className="border-b border-line">
                <th className={`${th} sticky left-0 z-[1] bg-surface-solid pl-4 text-left sm:pl-5`}>{t("Сотрудник")}</th>
                {withMoney && <th className={`${th} text-right`}>{t("Оклад")}</th>}
                {withMoney && <th className={`${th} text-right`} title={t("Премия при 100% по всем показателям")}>{t("Бонус 100%")}</th>}
                {region.columns.map((c) => (
                  <th key={c.key} className={`${th} border-l border-line text-center`}>
                    {t(c.label)}
                    {c.key !== c.label && <span className="ml-1 font-normal normal-case">{t(c.unit)}</span>}
                  </th>
                ))}
                {withMoney && <th className={`${th} border-l-2 border-line text-right`}>{t("Премия")}</th>}
                {withMoney && <th className={`${th} pr-4 text-right sm:pr-5`}>{t("Зарплата")}</th>}
              </tr>
            </thead>
            <tbody>
              {region.people.map((p) => (
                <tr key={p.id} className={`border-b border-line ${p.role === "СВР" ? "bg-muted/60" : ""}`}>
                  <td className={`sticky left-0 z-[1] px-3 py-2.5 pl-4 align-top sm:pl-5 ${p.role === "СВР" ? "bg-muted-solid" : "bg-surface-solid"}`}>
                    <PersonCell person={p} assign={assign} />
                  </td>
                  {withMoney && <td className="px-3 py-2.5 text-right align-top tabular-nums text-ink-2">{sumText(p.fix, t)}</td>}
                  {withMoney && <td className="px-3 py-2.5 text-right align-top tabular-nums text-ink-2">{sumText(p.bonus100, t)}</td>}
                  {region.columns.map((c) => {
                    const cell = p.cells.find((x) => x.key === c.key);
                    return (
                      <td key={c.key} className="border-l border-line px-3 py-2 align-top">
                        {cell && cell.plan ? <Cell cell={cell} forecast={fc} money={withMoney} /> : <span className="block text-center text-ink-3">·</span>}
                      </td>
                    );
                  })}
                  {withMoney && (
                    <td className="border-l-2 border-line px-3 py-2.5 text-right align-top font-semibold tabular-nums">
                      <span className={(fc ? p.premiumForecast : p.premiumNow) ? "text-ok" : "text-ink"}>{sumText(fc ? p.premiumForecast : p.premiumNow, t)}</span>
                    </td>
                  )}
                  {withMoney && <td className="px-3 py-2.5 pr-4 text-right align-top font-semibold tabular-nums sm:pr-5">{sumText(fc ? p.salaryForecast : p.salaryNow, t)}</td>}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function PersonCell({ person: p, assign }: { person: KpiPerson; assign?: { id: string; name: string }[] }) {
  const t = useT();
  const sources: Record<KpiPerson["regionSource"], string> = {
    profile: "регион указан вручную",
    supervisor: "по «СВР региона»",
    sales: "регион по продажам и визитам",
    name: "регион по названию в Linko",
    none: "регион не определён",
  };
  return (
    <span className="block min-w-[180px] max-w-[260px]">
      <span className="flex items-center gap-1.5">
        <span className="font-medium text-ink">{p.name}</span>
        {p.role === "СВР" && <span className="rounded bg-accent-soft px-1.5 py-px text-[10px] font-semibold uppercase text-accent-strong">{t("СВР")}</span>}
      </span>
      <span className="block text-xs text-ink-3" title={t(sources[p.regionSource])}>
        ID {p.id} · {kg(p.factKg)} {t("кг")}
        {p.regionSource !== "sales" && p.regionSource !== "none" && (
          <>
            {" "}
            · <MapPin className="inline size-3" aria-hidden /> {t(sources[p.regionSource])}
          </>
        )}
      </span>
      {assign && <AssignRegion person={p} options={assign} />}
    </span>
  );
}

/** Регион человеку без региона: профиль агента OneBase (PUT /api/sales/kpi/region), после сохранения раздел пересчитывается. */
function AssignRegion({ person, options }: { person: KpiPerson; options: { id: string; name: string }[] }) {
  const t = useT();
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  return (
    <span className="mt-1.5 flex items-center gap-2">
      <select
        defaultValue=""
        disabled={busy}
        onChange={async (e) => {
          if (!e.target.value) return;
          setBusy(true);
          setError(null);
          try {
            await bff("sales/kpi/region", { method: "PUT", body: JSON.stringify({ linkoUserId: person.id, regionId: e.target.value }) });
            router.refresh();
          } catch (err) {
            setError((err as Error).message);
            setBusy(false);
          }
        }}
        aria-label={t("Регион: {name}", { name: person.name })}
        className="h-8 min-w-0 flex-1 rounded-md border border-line bg-surface px-2 text-xs text-ink max-lg:h-10"
      >
        <option value="">{t("Указать регион…")}</option>
        {options.map((o) => (
          <option key={o.id} value={o.id}>
            {o.name}
          </option>
        ))}
      </select>
      {busy && <Loader2 className="size-3.5 animate-spin text-ink-3" />}
      {error && <span className="text-xs text-bad">{error}</span>}
    </span>
  );
}

/** Ячейка показателя: выполнение (цвет по порогу премии), полоса, факт / план и премия; в подсказке — название, порог и бонус. */
function Cell({ cell: c, forecast, money: withMoney }: { cell: KpiCell; forecast: boolean; money: boolean }) {
  const t = useT();
  const ratio = forecast ? c.pctForecast : c.pct;
  const tone = forecast ? c.toneForecast : c.tone;
  const fact = forecast ? c.factForecast : c.fact;
  const premium = forecast ? c.premiumForecast : c.premiumNow;
  const tip = [
    c.name,
    t("порог премии: {thr}", { thr: c.threshold == null ? t("нет") : pct(c.threshold) }),
    withMoney && c.bonus100 != null ? t("Бонус 100%: {sum}", { sum: money(c.bonus100, t) }) : null,
    t("продажи {sale} · возвраты {ret}", { sale: valueText(c.sale, c.unit, t), ret: valueText(c.return, c.unit, t) }),
  ]
    .filter(Boolean)
    .join("\n");
  return (
    <span className="block min-w-[92px] text-center" title={tip}>
      <span className={`block text-base font-semibold leading-tight tabular-nums ${toneText[tone]}`}>{pct(ratio)}</span>
      <span className="mx-auto mt-1 block h-1 w-14 overflow-hidden rounded-full bg-muted">
        <span className={`block h-full rounded-full ${toneBar[tone]}`} style={{ width: `${Math.min(100, Math.max(0, ratio * 100))}%` }} />
      </span>
      <span className="mt-1 block text-[11px] tabular-nums text-ink-3">
        {valueText(fact, c.unit, t)} / {valueText(c.plan, c.unit, t)}
      </span>
      {withMoney && <span className={`block text-[11px] font-medium tabular-nums ${premium ? "text-ok" : "text-ink-3"}`}>{premium ? money(premium, t) : "—"}</span>}
    </span>
  );
}

