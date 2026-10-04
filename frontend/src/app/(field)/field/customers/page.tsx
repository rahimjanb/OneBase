import { ParamSelect, SearchBox } from "@/components/field/DateSwitch";
import { Chip, Empty, FieldPage, ListLink, Pager, Tabs } from "@/components/field/ui";
import { fieldGet, fieldMe, qs, sp, type FieldSearchParams } from "@/lib/field/api";
import { customerStatusLabel, priorityLabel } from "@/lib/field/labels";
import { date, money } from "@/lib/sales/format";
import type { FieldCustomerRow, Page } from "@/lib/field/types";

export const metadata = { title: "Точки" };

const attention = [
  { key: "", label: "Все" },
  { key: "decline", label: "Падение продаж" },
  { key: "lost", label: "Перестали покупать" },
  { key: "notvisited", label: "Давно не были" },
  { key: "new", label: "Без покупок" },
];

/** Торговые точки зоны: поиск, фильтры «требуют внимания», сортировка, постранично (сервер). */
export default async function CustomersPage({ searchParams }: { searchParams: Promise<FieldSearchParams> }) {
  const params = await searchParams;
  const me = await fieldMe();
  const page = Number(sp(params, "page") ?? 1) || 1;
  const filters = {
    search: sp(params, "search"),
    agentId: sp(params, "agent"),
    status: sp(params, "status"),
    priority: sp(params, "priority"),
    attention: sp(params, "attention"),
    sort: sp(params, "sort"),
  };
  const [data, agents] = await Promise.all([
    fieldGet<Page<FieldCustomerRow>>(`customers${qs({ ...filters, page, pageSize: 40 })}`, "/field/customers"),
    me.role === "Agent" ? Promise.resolve([]) : fieldGet<{ id: string; name: string; teamName: string | null; role: string }[]>("agents", "/field/customers"),
  ]);
  const link = (changes: Record<string, string | number | null>) => {
    const q = new URLSearchParams();
    for (const [k, v] of Object.entries({ search: filters.search, agent: filters.agentId, status: filters.status, priority: filters.priority, attention: filters.attention, sort: filters.sort, page: null, ...changes })) {
      if (v !== null && v !== undefined && v !== "") q.set(k, String(v));
    }
    const s = q.toString();
    return s ? `/field/customers?${s}` : "/field/customers";
  };

  return (
    <FieldPage title="Точки" subtitle={`${data.total.toLocaleString("ru-RU")} ${me.role === "Agent" ? "ваших точек" : "точек в зоне"}`}>
      <Tabs items={attention.map((a) => ({ key: a.key, label: a.label, href: link({ attention: a.key || null }) }))} current={filters.attention ?? ""} />
      <div className="flex flex-wrap items-center gap-2">
        <SearchBox value={filters.search ?? ""} placeholder="Название, адрес или id точки" />
        {agents.length > 0 && (
          <ParamSelect param="agent" value={filters.agentId ?? ""} label="Агент" options={[{ value: "", label: "Все агенты" }, ...agents.filter((a) => a.role === "Agent").map((a) => ({ value: a.id, label: a.name }))]} />
        )}
        <ParamSelect
          param="sort"
          value={filters.sort ?? ""}
          label="Сортировка"
          options={[
            { value: "", label: "Продажи 30 дней" },
            { value: "sales90", label: "Продажи 90 дней" },
            { value: "trend", label: "Сильнее упали" },
            { value: "lastvisit", label: "Давно без визита" },
            { value: "name", label: "По названию" },
          ]}
        />
        <ParamSelect
          param="priority"
          value={filters.priority ?? ""}
          label="Приоритет"
          options={[{ value: "", label: "Любой приоритет" }, ...(["Urgent", "High", "Medium", "Low"] as const).map((p) => ({ value: p, label: priorityLabel[p][0] }))]}
        />
      </div>

      {data.items.length === 0 ? (
        <Empty>Точек по этим условиям нет.</Empty>
      ) : (
        <>
          {/* Телефон — список карточек */}
          <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface lg:hidden">
            {data.items.map((c) => (
              <li key={c.marketId}>
                <ListLink
                  href={`/field/customers/${c.marketId}`}
                  title={c.name}
                  subtitle={[c.agentName, c.lastVisitDate ? `визит ${date(c.lastVisitDate)}` : "визитов нет"].filter(Boolean).join(" · ")}
                  right={
                    <>
                      <div className="font-semibold tabular-nums text-ink">{money(c.sales30)}</div>
                      {c.trendPct != null && <div className={`text-xs tabular-nums ${c.trendPct < 0 ? "text-bad" : "text-ok"}`}>{c.trendPct > 0 ? "+" : ""}{Math.round(c.trendPct)}%</div>}
                    </>
                  }
                />
              </li>
            ))}
          </ul>
          {/* Большой экран — таблица */}
          <div className="hidden overflow-x-auto rounded-xl border border-line bg-surface lg:block">
            <table className="w-full text-sm">
              <thead className="text-left text-xs uppercase tracking-wide text-ink-3">
                <tr className="border-b border-line">
                  <th className="px-4 py-2.5 font-medium">Точка</th>
                  <th className="px-2 py-2.5 font-medium">Агент</th>
                  <th className="px-2 py-2.5 text-right font-medium">7 дней</th>
                  <th className="px-2 py-2.5 text-right font-medium">30 дней</th>
                  <th className="px-2 py-2.5 text-right font-medium">90 дней</th>
                  <th className="px-2 py-2.5 text-right font-medium">Тренд</th>
                  <th className="px-2 py-2.5 font-medium">Последний визит</th>
                  <th className="px-4 py-2.5 font-medium">Статус</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((c) => (
                  <tr key={c.marketId} className="border-b border-line last:border-0 hover:bg-muted">
                    <td className="max-w-[320px] px-4 py-2.5">
                      <a href={`/field/customers/${c.marketId}`} className="block truncate font-medium text-ink hover:underline">
                        {c.name}
                      </a>
                      <div className="truncate text-xs text-ink-3">{[c.branch, c.type, c.address].filter(Boolean).join(" · ")}</div>
                    </td>
                    <td className="px-2 py-2.5 text-ink-2">{c.agentName ?? <span className="text-warn">нет агента</span>}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{money(c.sales7)}</td>
                    <td className="px-2 py-2.5 text-right font-medium tabular-nums">{money(c.sales30)}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{money(c.sales90)}</td>
                    <td className={`px-2 py-2.5 text-right tabular-nums ${c.trendPct != null && c.trendPct < -20 ? "text-bad" : c.trendPct != null && c.trendPct > 0 ? "text-ok" : "text-ink-3"}`}>
                      {c.trendPct == null ? "—" : `${c.trendPct > 0 ? "+" : ""}${Math.round(c.trendPct)}%`}
                    </td>
                    <td className="px-2 py-2.5 text-ink-2">{c.lastVisitDate ? date(c.lastVisitDate) : "—"}</td>
                    <td className="px-4 py-2.5">
                      <div className="flex flex-wrap gap-1">
                        {c.status !== "Active" && <Chip tone={customerStatusLabel[c.status][1]}>{customerStatusLabel[c.status][0]}</Chip>}
                        {c.priority !== "Medium" && <Chip tone={priorityLabel[c.priority][1]}>{priorityLabel[c.priority][0]}</Chip>}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pager page={data.page} pageSize={data.pageSize} total={data.total} href={(p) => link({ page: p })} />
        </>
      )}
    </FieldPage>
  );
}
