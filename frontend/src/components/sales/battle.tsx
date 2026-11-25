"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { Download, Loader2, RefreshCw } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  AGENT_CARDS,
  LEDGER_STATUS,
  MARKS,
  SEGMENTS,
  TEAM_CARDS,
  setText,
  share,
  type BattleBalances,
  type BattleLedgerRow,
  type BattleOverview,
  type BattleQuota,
  type BattleRoute,
  type BattleStore,
  type BattleTeamRow,
  type CardSlot,
  type Mark,
} from "@/lib/sales/battle";

type Tab = "overview" | "route" | "team" | "cashback" | "stands" | "cards" | "sources";

const TABS: { key: Tab; label: string }[] = [
  { key: "overview", label: "Обзор" },
  { key: "route", label: "Маршрут и цели" },
  { key: "team", label: "Команды и регионы" },
  { key: "cashback", label: "Кешбек ТП" },
  { key: "stands", label: "Подарки и стойки" },
  { key: "cards", label: "Карточки" },
  { key: "sources", label: "Источники и настройки" },
];

const SLOT_LABELS: Record<CardSlot, string> = {
  morning: "08:30 Маршрут дня",
  midday: "13:00 Полдень",
  evening: "18:00 Итог дня",
  week: "Сб 18:00 Итог недели",
  month: "Итог месяца",
  team: "СВР 18:30 Итог дня команды",
  zone: "РМ сб 19:00 Итог недели зоны",
};

const money = (v: number) => Math.round(v).toLocaleString("ru-RU");
const kg = (v: number) => (Math.round(v * 10) / 10).toLocaleString("ru-RU");
const day = (iso: string | null) => (iso ? new Date(`${iso.slice(0, 10)}T00:00:00`).toLocaleDateString("ru-RU") : "—");
const when = (iso: string | null) => (iso ? new Date(iso).toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" }) : "—");

function Tile({ label, value, sub, tone = "text-ink" }: { label: string; value: string; sub?: string; tone?: string }) {
  const t = useT();
  return (
    <div className="min-w-0 rounded-xl border border-line bg-surface px-4 py-3 shadow-sm">
      <div className="text-xs font-medium text-ink-3">{t(label)}</div>
      <div className={`mt-1 text-2xl font-semibold tabular-nums ${tone}`}>{value}</div>
      {sub && <div className="mt-1 text-xs text-ink-3">{sub}</div>}
    </div>
  );
}

function Panel({ title, note, children }: { title: string; note?: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <section className="mt-4 rounded-xl border border-line bg-surface shadow-sm">
      <div className="flex flex-wrap items-baseline justify-between gap-2 border-b border-line px-4 py-3">
        <h3 className="font-semibold text-ink">{t(title)}</h3>
        {note && <span className="text-xs text-ink-3">{note}</span>}
      </div>
      <div className="overflow-x-auto p-4">{children}</div>
    </section>
  );
}

function ErrorText({ text }: { text: string | null }) {
  return text ? <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{text}</p> : null;
}

function MarkText({ mark }: { mark: Mark | null | undefined }) {
  const t = useT();
  if (!mark) return null;
  return <span className={`text-xs font-semibold ${MARKS[mark].className}`}>{t(MARKS[mark].label)}</span>;
}

/** Таблица «Кешбек · баланс»: Могул и Twizos отдельно. */
function Balances({ balances, title = "Кешбек · баланс", note }: { balances: BattleBalances; title?: string; note?: string }) {
  const t = useT();
  const rows: [string, BattleBalances["mogul"]][] = [
    ["Могул · 10%", balances.mogul],
    ["Twizos · 5–10%", balances.twizos],
  ];
  return (
    <Panel title={title} note={note}>
      <table className="w-full min-w-[640px] text-sm">
        <thead>
          <tr className="border-b border-line text-left text-xs text-ink-3">
            <th className="py-2 pr-2 font-medium">{t("Акция")}</th>
            <th className="py-2 pr-2 text-right font-medium">{t("Оформлено")}</th>
            <th className="py-2 pr-2 text-right font-medium">{t("Ждёт доставки")}</th>
            <th className="py-2 pr-2 text-right font-medium">{t("Доставлено")}</th>
            <th className="py-2 pr-2 text-right font-medium">{t("Выплачено")}</th>
            <th className="py-2 pr-2 text-right font-medium">{t("К выплате")}</th>
            <th className="py-2 text-right font-medium">{t("Можно ещё")}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(([label, b]) => (
            <tr key={label} className="border-b border-line/60">
              <td className="py-2 pr-2 font-medium text-ink">{t(label)}</td>
              <td className="py-2 pr-2 text-right tabular-nums">{money(b.issued)}</td>
              <td className="py-2 pr-2 text-right tabular-nums">{money(b.pending)}</td>
              <td className="py-2 pr-2 text-right tabular-nums">{money(b.confirmed)}</td>
              <td className="py-2 pr-2 text-right tabular-nums">{money(b.paid)}</td>
              <td className="py-2 pr-2 text-right font-semibold tabular-nums text-ok">{money(b.toPay)}</td>
              <td className="py-2 text-right tabular-nums text-warn">{b.canEarn === null ? "—" : money(b.canEarn)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </Panel>
  );
}

/**
 * «Продажи → Поле боя»: обзор, маршрут ТП с целями Могул и Twizos (✓ ½ ×), команды и регионы, журнал кешбека (два баланса), подарки и стойки,
 * карточки PNG как в Telegram (просмотр и выгрузка), источники данных. Все цифры считает сервер в охвате пользователя.
 */
export function BattleView({ overview }: { overview: BattleOverview }) {
  const t = useT();
  const [tab, setTab] = useState<Tab>("overview");
  const [date, setDate] = useState(overview.date);
  const [agent, setAgent] = useState<number | null>(overview.agents[0]?.id ?? null);
  const [region, setRegion] = useState<string>(overview.regions[0]?.name ?? "");
  const agents = overview.agents;

  return (
    <div>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-wrap gap-1 rounded-xl border border-line bg-surface p-1">
          {TABS.map((x) => (
            <button
              key={x.key}
              onClick={() => setTab(x.key)}
              className={`rounded-lg px-3 py-1.5 text-sm ${tab === x.key ? "bg-muted font-semibold text-ink shadow-sm ring-1 ring-line" : "text-ink-2 hover:bg-muted"}`}
            >
              {t(x.label)}
            </button>
          ))}
        </div>
        <label className="text-xs text-ink-3">
          {t("День")}
          <input type="date" className={`${field} mt-1 block w-[160px]`} value={date} onChange={(e) => e.target.value && setDate(e.target.value)} />
        </label>
      </div>
      <p className="mt-2 text-xs text-ink-3">
        {overview.name} · {t("цели v2 {from}–{to}", { from: day(overview.start), to: day(overview.end) })} · {overview.scope} ·{" "}
        {t("данные Linko на {time}", { time: when(overview.dataAsOf) })}
        {!overview.enabled && ` · ${t("акция выключена — карточки не уходят по расписанию")}`}
      </p>

      {tab === "overview" && <Overview data={overview} />}
      {tab === "route" && <RouteTab agents={agents} agent={agent} setAgent={setAgent} date={date} />}
      {tab === "team" && <TeamTab regions={overview.regions.map((r) => r.name)} region={region} setRegion={setRegion} date={date} />}
      {tab === "cashback" && <CashbackTab agents={agents} date={date} />}
      {tab === "stands" && <StandsTab date={date} />}
      {tab === "cards" && <CardsTab agents={agents} regions={overview.regions.map((r) => r.name)} date={date} />}
      {tab === "sources" && <SourcesTab data={overview} />}
    </div>
  );
}

function Overview({ data }: { data: BattleOverview }) {
  const t = useT();
  return (
    <>
      <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Tile label="Спящие Могул разбужено" value={`${data.woken} / ${data.listed}`} sub={t("{pct}% списка", { pct: share(data.woken, data.listed) })} tone="text-[#D6336C]" />
        <Tile label="Twizos клиентов с начала целей" value={money(data.twizosClients)} sub={t("новых {n} · стоек {s}", { n: data.twizosNew, s: data.stands })} tone="text-[#E8731A]" />
        <Tile label="Кешбек Могул к выплате" value={money(data.balances.mogul.toPay)} sub={t("оформлено {sum}", { sum: money(data.balances.mogul.issued) })} tone="text-ok" />
        <Tile label="Кешбек Twizos к выплате" value={money(data.balances.twizos.toPay)} sub={t("оформлено {sum}", { sum: money(data.balances.twizos.issued) })} tone="text-ok" />
      </div>
      <Balances balances={data.balances} note={t("по {date}", { date: day(data.date) })} />
      <Panel title="Регионы">
        <table className="w-full min-w-[760px] text-sm">
          <thead>
            <tr className="border-b border-line text-left text-xs text-ink-3">
              <th className="py-2 pr-2 font-medium">{t("Регион")}</th>
              <th className="py-2 pr-2 text-right font-medium">{t("ТП")}</th>
              <th className="py-2 pr-2 text-right font-medium">{t("Спящие Могул")}</th>
              <th className="py-2 pr-2 text-right font-medium">{t("Стойки: склад · цель · квота ТП")}</th>
              <th className="py-2 pr-2 text-right font-medium">{t("Могул к выплате")}</th>
              <th className="py-2 text-right font-medium">{t("Twizos к выплате")}</th>
            </tr>
          </thead>
          <tbody>
            {data.regions.map((r) => (
              <tr key={r.name} className="border-b border-line/60">
                <td className="py-2 pr-2 font-medium text-ink">{r.name}</td>
                <td className="py-2 pr-2 text-right tabular-nums">{r.agents}</td>
                <td className="py-2 pr-2 text-right tabular-nums">
                  {r.woken} / {r.listed} · {share(r.woken, r.listed)}%
                </td>
                <td className="py-2 pr-2 text-right tabular-nums">{r.stands ? `${r.stands.stock ?? "—"} · ${r.stands.target} · ${r.stands.perAgent}` : "—"}</td>
                <td className="py-2 pr-2 text-right tabular-nums">{money(r.balances.mogul.toPay)}</td>
                <td className="py-2 text-right tabular-nums">{money(r.balances.twizos.toPay)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Panel>
    </>
  );
}

function AgentSelect({ agents, agent, setAgent }: { agents: BattleOverview["agents"]; agent: number | null; setAgent: (id: number) => void }) {
  const t = useT();
  return (
    <label className="text-xs text-ink-3">
      {t("ТП")}
      <select className={`${field} mt-1 block min-w-[260px]`} value={agent ?? ""} onChange={(e) => setAgent(Number(e.target.value))}>
        {agents.map((a) => (
          <option key={a.id} value={a.id}>
            {a.name} · {a.region}
          </option>
        ))}
      </select>
    </label>
  );
}

function useLoad<T>(path: string | null) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (!path) return;
    let alive = true;
    setBusy(true);
    setError(null);
    bff<T>(path)
      .then((d) => alive && setData(d))
      .catch((e) => alive && setError((e as Error).message))
      .finally(() => alive && setBusy(false));
    return () => {
      alive = false;
    };
  }, [path]);
  return { data, error, busy };
}

function GoalCell({ s }: { s: BattleStore }) {
  const t = useT();
  const m = s.goal?.mogul;
  const tw = s.goal?.twizos;
  if (!m && !tw) return <span className="text-xs text-ink-3">{t("без акции · обычный заказ")}</span>;
  return (
    <div className="space-y-1.5">
      {m && (
        <div>
          <span className="rounded bg-[#D6336C] px-1.5 py-0.5 text-[11px] font-semibold text-white">{t("Могул · спящий −7%")}</span>{" "}
          <span className="text-xs text-ink-2">
            {t("{avg} → {target} кг/мес · {boxes} кор. {box} · {sum} → {after}", {
              avg: kg(m.avgKg),
              target: kg(m.targetKg),
              boxes: m.boxes,
              box: m.box,
              sum: money(m.sum),
              after: money(m.afterDiscount),
            })}
          </span>{" "}
          <span className="text-xs font-semibold text-ok">+{money(m.cashback)}</span> <MarkText mark={s.fact?.mogulStatus} />
        </div>
      )}
      {tw && (
        <div>
          <span className={`rounded px-1.5 py-0.5 text-[11px] font-semibold ${SEGMENTS[tw.segment].className}`}>
            Twizos · {t(SEGMENTS[tw.segment].label)} · {tw.percent}%
          </span>{" "}
          <span className="text-xs text-ink-2">
            {setText(tw.set)} · {money(tw.sum)} · {kg(tw.kg)} {t("кг")}
            {tw.avgInvoice ? ` · ${t("обычно {sum}", { sum: money(tw.avgInvoice) })}` : ""}
          </span>{" "}
          <span className="text-xs font-semibold text-ok">+{money(tw.cashback)}</span> <MarkText mark={s.fact?.twizosStatus} />
        </div>
      )}
    </div>
  );
}

function RouteTab({ agents, agent, setAgent, date }: { agents: BattleOverview["agents"]; agent: number | null; setAgent: (id: number) => void; date: string }) {
  const t = useT();
  const { data, error, busy } = useLoad<BattleRoute>(agent ? `sales/battle/agents/${agent}/route?date=${date}` : null);
  const tt = data?.totals;
  return (
    <>
      <div className="mt-4 flex flex-wrap items-end gap-3">
        <AgentSelect agents={agents} agent={agent} setAgent={setAgent} />
        {busy && <Loader2 className="mb-2 size-4 animate-spin text-ink-3" />}
      </div>
      <ErrorText text={error} />
      {data && tt && (
        <>
          <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Tile label="Спящие Могул" value={`${tt.woken} / ${tt.mogulGoals}`} sub={t("кешбек до {sum}", { sum: money(tt.mogulGoalCashback) })} tone="text-[#D6336C]" />
            <Tile label="Twizos клиентов" value={`${tt.twizosClients} / ${tt.twizosGoals}`} sub={`${kg(tt.twizosKg)} ${t("кг")}`} tone="text-[#E8731A]" />
            <Tile label="Точек пройдено" value={`${tt.passed} / ${tt.stores}`} sub={t("в плане Linko {n}", { n: data.route.routePoints })} />
            <Tile label="Кешбек дня" value={money(tt.cashback)} sub={t("можно ещё {sum}", { sum: money(tt.mogulLeft + tt.twizosLeft) })} tone="text-ok" />
          </div>
          <Panel title="Маршрут дня" note={t("порядок — по коду ТТ · цели ставит утренняя карточка")}>
            <table className="w-full min-w-[860px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-xs text-ink-3">
                  <th className="py-2 pr-2 font-medium">№</th>
                  <th className="py-2 pr-2 font-medium">{t("Торговая точка")}</th>
                  <th className="py-2 pr-2 text-right font-medium">{t("Средний чек")}</th>
                  <th className="py-2 pr-2 font-medium">{t("Цели")}</th>
                  <th className="py-2 font-medium">{t("Итог")}</th>
                </tr>
              </thead>
              <tbody>
                {data.route.stores.map((s) => (
                  <tr key={s.marketId} className="border-b border-line/60 align-top">
                    <td className="py-2 pr-2 tabular-nums text-ink-3">{s.order}</td>
                    <td className="py-2 pr-2 text-ink">
                      {s.name}
                      <span className="block text-xs text-ink-3">ID {s.marketId}</span>
                    </td>
                    <td className="py-2 pr-2 text-right tabular-nums text-ink-2">{money(s.avgCheck)}</td>
                    <td className="py-2 pr-2">
                      <GoalCell s={s} />
                    </td>
                    <td className="py-2">
                      <MarkText mark={s.status} />
                    </td>
                  </tr>
                ))}
                {data.route.stores.length === 0 && (
                  <tr>
                    <td colSpan={5} className="py-3 text-ink-3">
                      {t("Маршрута на этот день нет: в Linko нет плана визитов и точек ТП.")}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </Panel>
          {(data.route.outOfRoute ?? []).length > 0 && (
            <Panel title="Вне маршрута — продажи по акции, засчитаны">
              <ul className="space-y-1 text-sm">
                {(data.route.outOfRoute ?? []).map((o) => (
                  <li key={o.marketId}>
                    {o.name} · Twizos {money(o.sum)} · {kg(o.kg)} {t("кг")} · {t("кешбек")} +{money((o.fact?.twizosCashback ?? 0) + (o.fact?.mogulCashback ?? 0))}
                  </li>
                ))}
              </ul>
            </Panel>
          )}
          <Balances balances={data.balances} note={t("по {date}", { date: day(data.date) })} />
        </>
      )}
    </>
  );
}

function TeamTab({ regions, region, setRegion, date }: { regions: string[]; region: string; setRegion: (r: string) => void; date: string }) {
  const t = useT();
  const { data, error, busy } = useLoad<BattleTeamRow[]>(region ? `sales/battle/team?region=${encodeURIComponent(region)}&date=${date}` : null);
  return (
    <>
      <div className="mt-4 flex flex-wrap items-end gap-3">
        <label className="text-xs text-ink-3">
          {t("Регион")}
          <select className={`${field} mt-1 block min-w-[200px]`} value={region} onChange={(e) => setRegion(e.target.value)}>
            {regions.map((r) => (
              <option key={r}>{r}</option>
            ))}
          </select>
        </label>
        {busy && <Loader2 className="mb-2 size-4 animate-spin text-ink-3" />}
      </div>
      <ErrorText text={error} />
      {data && (
        <Panel title="Команда сегодня" note={t("сортировка по кешбеку дня")}>
          <table className="w-full min-w-[820px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-xs text-ink-3">
                <th className="py-2 pr-2 font-medium">{t("ТП")}</th>
                <th className="py-2 pr-2 text-right font-medium">{t("Спящие")}</th>
                <th className="py-2 pr-2 text-right font-medium">{t("Twizos клиентов")}</th>
                <th className="py-2 pr-2 text-right font-medium">{t("Пройдено")}</th>
                <th className="py-2 pr-2 text-right font-medium">{t("Кешбек Могул")}</th>
                <th className="py-2 pr-2 text-right font-medium">{t("Кешбек Twizos")}</th>
                <th className="py-2 text-right font-medium">{t("Отказы")}</th>
              </tr>
            </thead>
            <tbody>
              {data.map((r) => (
                <tr key={r.agent.id} className="border-b border-line/60">
                  <td className="py-2 pr-2 text-ink">{r.agent.name}</td>
                  <td className="py-2 pr-2 text-right tabular-nums">
                    {r.totals.woken}/{r.totals.mogulGoals}
                  </td>
                  <td className="py-2 pr-2 text-right tabular-nums">
                    {r.totals.twizosClients}/{r.totals.twizosGoals}
                  </td>
                  <td className="py-2 pr-2 text-right tabular-nums">
                    {r.totals.passed}/{r.totals.stores}
                  </td>
                  <td className="py-2 pr-2 text-right tabular-nums">{money(r.totals.mogulCashback)}</td>
                  <td className="py-2 pr-2 text-right font-medium tabular-nums text-ok">{money(r.totals.twizosCashback)}</td>
                  <td className="py-2 text-right tabular-nums text-bad">{r.totals.refused.length}</td>
                </tr>
              ))}
              {data.length === 0 && (
                <tr>
                  <td colSpan={7} className="py-3 text-ink-3">
                    {t("ТП в регионе нет.")}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </Panel>
      )}
    </>
  );
}

function CashbackTab({ agents, date }: { agents: BattleOverview["agents"]; date: string }) {
  const t = useT();
  const [agent, setAgent] = useState<string>("");
  const [kind, setKind] = useState<string>("");
  const from = useMemo(() => {
    const d = new Date(`${date}T00:00:00`);
    d.setDate(d.getDate() - 30);
    return d.toLocaleDateString("sv-SE");
  }, [date]);
  const { data, error, busy } = useLoad<BattleLedgerRow[]>(`sales/battle/ledger?from=${from}&to=${date}${agent ? `&agent=${agent}` : ""}${kind ? `&kind=${kind}` : ""}`);
  const sum = (f: (r: BattleLedgerRow) => number) => money((data ?? []).filter((r) => r.status !== "cancelled").reduce((s, r) => s + f(r), 0));
  return (
    <>
      <div className="mt-4 flex flex-wrap items-end gap-3">
        <label className="text-xs text-ink-3">
          {t("ТП")}
          <select className={`${field} mt-1 block min-w-[240px]`} value={agent} onChange={(e) => setAgent(e.target.value)}>
            <option value="">{t("все в охвате")}</option>
            {agents.map((a) => (
              <option key={a.id} value={a.id}>
                {a.name} · {a.region}
              </option>
            ))}
          </select>
        </label>
        <label className="text-xs text-ink-3">
          {t("Кешбек")}
          <select className={`${field} mt-1 block`} value={kind} onChange={(e) => setKind(e.target.value)}>
            <option value="">{t("Могул и Twizos")}</option>
            <option value="mogul">{t("Могул")}</option>
            <option value="twizos">Twizos</option>
          </select>
        </label>
        {busy && <Loader2 className="mb-2 size-4 animate-spin text-ink-3" />}
      </div>
      <ErrorText text={error} />
      {data && (
        <Panel title="Журнал начислений" note={t("{n} заказов за 30 дней · оформлено {issued} · сейчас {amount}", { n: data.length, issued: sum((r) => r.issued), amount: sum((r) => r.amount) })}>
          <table className="w-full min-w-[980px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-xs text-ink-3">
                <th className="py-2 pr-2 font-medium">{t("Заказ")}</th>
                <th className="py-2 pr-2 font-medium">{t("ТП")}</th>
                <th className="py-2 pr-2 font-medium">{t("Торговая точка")}</th>
                <th className="py-2 pr-2 font-medium">{t("Вид · сегмент")}</th>
                <th className="py-2 pr-2 text-right font-medium">{t("Сумма")}</th>
                <th className="py-2 pr-2 text-right font-medium">{t("Кешбек")}</th>
                <th className="py-2 font-medium">{t("Статус")}</th>
              </tr>
            </thead>
            <tbody>
              {data.map((r) => (
                <tr key={`${r.kind}-${r.orderId}`} className="border-b border-line/60">
                  <td className="py-1.5 pr-2 tabular-nums">
                    {r.orderId}
                    <span className="block text-xs text-ink-3">{day(r.orderDate)}</span>
                  </td>
                  <td className="py-1.5 pr-2 text-ink-2">{r.agent}</td>
                  <td className="py-1.5 pr-2 text-ink">{r.market}</td>
                  <td className="py-1.5 pr-2">
                    {r.kind === "mogul" ? (
                      <span className="rounded bg-[#D6336C] px-1.5 py-0.5 text-[11px] font-semibold text-white">{t("Могул")} · {r.percent}%</span>
                    ) : (
                      <span className={`rounded px-1.5 py-0.5 text-[11px] font-semibold ${SEGMENTS[(r.segment as keyof typeof SEGMENTS) ?? "new"]?.className ?? ""}`}>
                        Twizos · {t(SEGMENTS[r.segment as keyof typeof SEGMENTS]?.label ?? r.segment)} · {r.percent}%{r.stand ? ` · ${t("стойка")}` : ""}
                      </span>
                    )}
                  </td>
                  <td className="py-1.5 pr-2 text-right tabular-nums">{money(r.netSum)}</td>
                  <td className="py-1.5 pr-2 text-right font-medium tabular-nums">{money(r.amount)}</td>
                  <td className={`py-1.5 text-xs ${r.status === "confirmed" ? "text-ok" : r.status === "cancelled" ? "text-bad" : "text-ink-3"}`}>
                    {t(LEDGER_STATUS[r.status])}
                    {r.deliveredOn && ` · ${day(r.deliveredOn)}`}
                  </td>
                </tr>
              ))}
              {data.length === 0 && (
                <tr>
                  <td colSpan={7} className="py-3 text-ink-3">
                    {t("Начислений за 30 дней нет.")}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </Panel>
      )}
    </>
  );
}

function StandsTab({ date }: { date: string }) {
  const t = useT();
  const { data, error, busy } = useLoad<BattleQuota[]>(`sales/battle/stands?date=${date}`);
  return (
    <>
      {busy && <Loader2 className="mt-4 size-4 animate-spin text-ink-3" />}
      <ErrorText text={error} />
      {data && (
        <Panel title="Квота стоек Twizos" note={t("цель региона — остаток на складе дилера (ДМП, Linko); 0 → 40; квота ТП = вверх(цель ÷ ТП)")}>
          <table className="w-full min-w-[600px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-xs text-ink-3">
                <th className="py-2 pr-2 font-medium">{t("Регион")}</th>
                <th className="py-2 pr-2 text-right font-medium">{t("На складе")}</th>
                <th className="py-2 pr-2 text-right font-medium">{t("Цель стоек")}</th>
                <th className="py-2 pr-2 text-right font-medium">{t("ТП")}</th>
                <th className="py-2 text-right font-medium">{t("Квота ТП")}</th>
              </tr>
            </thead>
            <tbody>
              {data.map((q) => (
                <tr key={q.region} className="border-b border-line/60">
                  <td className="py-2 pr-2 font-medium text-ink">{q.region}</td>
                  <td className="py-2 pr-2 text-right tabular-nums">
                    {q.stock ?? "—"}
                    {q.manual && <span className="ml-1 text-xs text-ink-3">{t("вручную")}</span>}
                  </td>
                  <td className="py-2 pr-2 text-right tabular-nums">{q.target}</td>
                  <td className="py-2 pr-2 text-right tabular-nums">{q.agents}</td>
                  <td className="py-2 text-right font-semibold tabular-nums">{q.perAgent}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="mt-3 text-xs text-ink-3">
            {t("Выданные подарки и остатки кружек, термосов и чайников — в карточке СВР «Итог дня команды» и в разделе «ДМП».")}{" "}
            <Link href="/sales/dmp" className="underline">
              {t("ДМП")}
            </Link>
          </p>
        </Panel>
      )}
    </>
  );
}

function CardsTab({ agents, regions, date }: { agents: BattleOverview["agents"]; regions: string[]; date: string }) {
  const t = useT();
  const [slot, setSlot] = useState<CardSlot>("morning");
  const [agent, setAgent] = useState<number | null>(agents[0]?.id ?? null);
  const [region, setRegion] = useState(regions[0] ?? "");
  const [lang, setLang] = useState("ru");
  const [loading, setLoading] = useState(true);
  const team = TEAM_CARDS.includes(slot);
  const src = `/bff/api/sales/battle/card?slot=${slot}&date=${date}&lang=${lang}${team ? `&region=${encodeURIComponent(region)}` : agent ? `&agent=${agent}` : ""}`;
  useEffect(() => setLoading(true), [src]);
  return (
    <>
      <div className="mt-4 flex flex-wrap items-end gap-3">
        <label className="text-xs text-ink-3">
          {t("Карточка")}
          <select className={`${field} mt-1 block`} value={slot} onChange={(e) => setSlot(e.target.value as CardSlot)}>
            {[...AGENT_CARDS, ...TEAM_CARDS].map((s) => (
              <option key={s} value={s}>
                {t(SLOT_LABELS[s])}
              </option>
            ))}
          </select>
        </label>
        {team ? (
          <label className="text-xs text-ink-3">
            {t("Регион")}
            <select className={`${field} mt-1 block min-w-[200px]`} value={region} onChange={(e) => setRegion(e.target.value)}>
              {regions.map((r) => (
                <option key={r}>{r}</option>
              ))}
            </select>
          </label>
        ) : (
          <AgentSelect agents={agents} agent={agent} setAgent={setAgent} />
        )}
        <label className="text-xs text-ink-3">
          {t("Язык")}
          <select className={`${field} mt-1 block`} value={lang} onChange={(e) => setLang(e.target.value)}>
            <option value="ru">Русский</option>
            <option value="uz">O‘zbekcha</option>
            <option value="uz-Cyrl">Ўзбекча</option>
          </select>
        </label>
        <a className={button} href={src} download={`pole-boya-${slot}-${date}.png`}>
          <Download className="size-4" />
          {t("Скачать PNG")}
        </a>
      </div>
      <p className="mt-2 text-xs text-ink-3">
        {t("Та же картинка, что уходит в Telegram файлом без сжатия (1080 px). Цели дня здесь не записываются. Неделя — за неделю дня, месяц — с начала акции.")}
      </p>
      <div className="mt-4 overflow-hidden rounded-xl border border-line bg-[#FFF8EC]">
        {loading && (
          <p className="flex items-center gap-2 p-4 text-sm text-ink-3">
            <Loader2 className="size-4 animate-spin" />
            {t("Рисуем карточку…")}
          </p>
        )}
        {/* eslint-disable-next-line @next/next/no-img-element -- PNG с сервера, не статика */}
        <img key={src} src={src} alt={t(SLOT_LABELS[slot])} className="mx-auto block w-full max-w-[540px]" onLoad={() => setLoading(false)} onError={() => setLoading(false)} />
      </div>
    </>
  );
}

function SourcesTab({ data }: { data: BattleOverview }) {
  const t = useT();
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const refresh = async () => {
    setBusy(true);
    setError(null);
    try {
      const r = await bff<{ added: number; updated: number; mogul: number; twizos: number }>("sales/battle/ledger/refresh", { method: "POST" });
      setResult(t("Журнал обновлён: добавлено {a}, изменено {u}; Могул {m}, Twizos {tw} заказов.", { a: r.added, u: r.updated, m: r.mogul, tw: r.twizos }));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };
  const rows: [string, string, string][] = [
    ["RITM / Linko", t("точки, ТП, план визитов, заказы, SKU, скидки, подарки, стойка Twizos (товар «Стеллаж TWIZOS»)"), t("данные на {time}", { time: when(data.dataAsOf) })],
    ["1BASE (OneBase)", t("доставка (статус и дата приёмки), возвраты — из Linko"), t("с заказами")],
    [t("ДМП / Полевой контроль"), t("остаток стоек на складе дилера — остаток «Стеллаж TWIZOS» на складе региона в Linko; ручная поправка — в параметрах"), t("с остатками Linko")],
    [t("Журнал кешбека"), t("начисления Могул и Twizos по заказам: строка на заказ, без повторов при синхронизации"), t("{n} строк", { n: data.ledgerRows })],
    [t("Бухгалтерия"), t("отметки «выплачено» по неделям — «Настройки → Telegram → Автоматические отчёты → Акции → Кэшбек и выплаты»"), t("вручную")],
  ];
  return (
    <>
      <Panel title="Источники данных">
        <table className="w-full min-w-[720px] text-sm">
          <tbody>
            {rows.map(([name, what, state]) => (
              <tr key={name} className="border-b border-line/60 align-top">
                <td className="py-2 pr-3 font-medium text-ink">{name}</td>
                <td className="py-2 pr-3 text-ink-2">{what}</td>
                <td className="py-2 text-right text-xs text-ink-3">{state}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <button className={button} onClick={refresh} disabled={busy}>
            {busy ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
            {t("Обновить журнал кешбека")}
          </button>
          <span className="text-xs text-ink-3">{t("сам обновляется перед карточками и в 19:00; кнопка — только при охвате «вся организация»")}</span>
        </div>
        {result && <p className="mt-2 text-sm text-ok">{result}</p>}
        <ErrorText text={error} />
      </Panel>
      <Panel title="Настройки акций">
        <p className="text-sm text-ink-2">
          {t("Параметры целей v2 (прирост, пороги чека, кешбек 5 / 8 / 10%, коробки и цены, наборы и лестница, стойки, регионы без Могула), расписание карточек и получатели — во вкладке «Акции» автоотчётов Telegram. Каждое сохранение — в журнале аудита.")}
        </p>
        <Link href="/settings/telegram/reports?tab=incentives" className={`${primary} mt-3 inline-flex`}>
          {t("Открыть настройки акций")}
        </Link>
      </Panel>
    </>
  );
}
