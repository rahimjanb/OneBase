import { ParamSelect, SearchBox } from "@/components/field/DateSwitch";
import { NewTaskButton, TaskList } from "@/components/field/TaskBoard";
import { FieldPage, Pager, Tabs } from "@/components/field/ui";
import { fieldGet, fieldMe, qs, sp, type FieldSearchParams } from "@/lib/field/api";
import { priorityLabel } from "@/lib/field/labels";
import type { FieldTask, Page } from "@/lib/field/types";

export const metadata = { title: "Задачи" };

type AgentOption = { id: string; name: string; teamName: string | null; role: string };

/** Задачи: открытые / сегодня / просроченные / все; фильтр по исполнителю, приоритету, источнику (AI). */
export default async function TasksPage({ searchParams }: { searchParams: Promise<FieldSearchParams> }) {
  const params = await searchParams;
  const me = await fieldMe();
  const view = sp(params, "view") ?? "open";
  const page = Number(sp(params, "page") ?? 1) || 1;
  const filters = { assignedToId: sp(params, "agent"), priority: sp(params, "priority"), createdBy: sp(params, "by"), search: sp(params, "search") };
  const status = view === "closed" ? "closed" : view === "all" ? "all" : "open";
  const due = view === "today" ? "today" : view === "overdue" ? "overdue" : undefined;
  const [data, agents] = await Promise.all([
    fieldGet<Page<FieldTask>>(`tasks${qs({ status, due, ...filters, page, pageSize: 40 })}`, "/field/tasks"),
    me.canPlan ? fieldGet<AgentOption[]>("agents", "/field/tasks") : Promise.resolve([]),
  ]);
  const link = (changes: Record<string, string | number | null>) => {
    const q = new URLSearchParams();
    for (const [k, v] of Object.entries({ view: view === "open" ? null : view, agent: filters.assignedToId, priority: filters.priority, by: filters.createdBy, search: filters.search, page: null, ...changes })) {
      if (v !== null && v !== undefined && v !== "") q.set(k, String(v));
    }
    const s = q.toString();
    return s ? `/field/tasks?${s}` : "/field/tasks";
  };
  const options = agents.map((a) => ({ id: a.id, name: a.name, teamName: a.teamName }));
  const self = me.memberId ? [{ id: me.memberId, name: `${me.name} (мне)`, teamName: null }] : [];

  return (
    <FieldPage title="Задачи" subtitle={`${data.total} ${view === "closed" ? "закрытых" : view === "all" ? "всего" : "в списке"}`} actions={<NewTaskButton agents={me.canPlan ? options : self} />}>
      <Tabs
        items={[
          { key: "open", label: "Открытые", href: link({ view: null }) },
          { key: "today", label: "На сегодня", href: link({ view: "today" }) },
          { key: "overdue", label: "Просроченные", href: link({ view: "overdue" }) },
          { key: "closed", label: "Закрытые", href: link({ view: "closed" }) },
          { key: "all", label: "Все", href: link({ view: "all" }) },
        ]}
        current={view}
      />
      <div className="flex flex-wrap gap-2">
        <SearchBox value={filters.search ?? ""} placeholder="Поиск по задачам" />
        {agents.length > 0 && <ParamSelect param="agent" value={filters.assignedToId ?? ""} label="Исполнитель" options={[{ value: "", label: "Все исполнители" }, ...agents.map((a) => ({ value: a.id, label: a.name }))]} />}
        <ParamSelect param="priority" value={filters.priority ?? ""} label="Приоритет" options={[{ value: "", label: "Любой приоритет" }, ...(["Urgent", "High", "Medium", "Low"] as const).map((p) => ({ value: p, label: priorityLabel[p][0] }))]} />
        <ParamSelect
          param="by"
          value={filters.createdBy ?? ""}
          label="Источник"
          options={[
            { value: "", label: "Все источники" },
            { value: "Ai", label: "AI" },
            { value: "Supervisor", label: "Супервайзер" },
            { value: "Rm", label: "РМ" },
            { value: "System", label: "Система" },
            { value: "Agent", label: "Сам агент" },
          ]}
        />
      </div>
      <TaskList tasks={data.items} emptyText={view === "overdue" ? "Просроченных задач нет" : "Задач нет"} />
      <Pager page={data.page} pageSize={data.pageSize} total={data.total} href={(p) => link({ page: p })} />
    </FieldPage>
  );
}
